# Save & Load Persistence

> **SUPERSEDED 2026-09-01 by the live save layer — one versioned SaveGame record, lenient by name (Persistence/SaveGame.cs).** Describes the per-section save-envelope design that never shipped model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

> **Status**: Draft (authored autonomously — see Author's Note below)
> **Created**: 2026-07-14
> **Last Updated**: 2026-07-14
> **System Tier**: Foundation (category: Persistence)
> **Priority**: MVP
> **Source Concept**: `design/gdd/game-concept.md`
> **Systems Index Entry**: `design/gdd/systems-index.md` (#3, Foundation Layer — no dependencies)

> **Author's note (autonomous session)**: This GDD was authored without per-section
> user approval, per this session's stated time constraint (`production/session-state/active.md`).
> Ambiguities were resolved directly against game-concept.md's pillars and MVP Definition, not
> left as placeholders. The calls most worth flagging for review later:
>
> - **Single save slot, autosave-only.** No manual save UI, no save slots, no cloud sync.
>   Matches game-concept.md's single-player-only scope and the MVP bar of "basic save/resume."
> - **Combat encounters are explicitly non-persistent.** Never checkpointed mid-fight. Keeps this
>   Foundation system decoupled from `combat-encounter-system` and matches the
>   "clean chapter close" language in game-concept.md's Session-Level loop.
> - **JSON over binary.** Prioritizes debuggability during active MVP development over raw
>   performance, which is a non-issue at this save file's scope. Revisit at Alpha if file size or
>   parse time becomes a real problem.
> - **Per-section schema versioning, not one whole-file version.** Lets each future system's GDD
>   evolve its own save data independently without forcing a full-file migration bump for
>   unrelated changes. This is the single biggest structural decision in this document.
> - **The elapsed-time mechanism records raw wall-clock deltas now; it does not decide how those
>   deltas become gameplay.** That consumption logic (offline-progression math, gain caps) is
>   explicitly deferred to `region-mastery-automation-system`. This GDD's job stops at "the number
>   is computed correctly and available" — see Formulas §3.

---

## Overview

`save-load-persistence` is the Foundation-tier serialization framework that lets Resonance
Hunter's game state survive being closed and reopened. It owns the save file's envelope format (a
versioned wrapper around independently-versioned per-system data sections), the triggers that
cause a save to happen, the compatibility/versioning policy for reading old saves against new game
builds, and — most importantly for the game's future automation systems — a wall-clock timestamp
mechanism that other systems can use to measure real-world elapsed time against a persisted state,
without requiring a save-format change when they start consuming it. It does **not** define what
any other system stores; each owning system (creature roster, region mastery, Forge inventory,
Hunter build) designs and versions the contents of its own reserved section independently,
referencing this GDD's envelope contract.

---

## Player Fantasy

Persistence's entire job is to be invisible when it works. IDLExIDLE's core fantasy is
building a world that keeps running without you (game-concept.md's Core Fantasy) — an empire of
mastered regions, an evolving creature roster, a Hunter build shaped by hard-won trade-offs. None
of that means anything if closing the game can lose it. The promise this system makes is simple
and unconditional: **the empire you're building never disappears.** You should be able to close
IDLExIDLE mid-expedition, mid-Forge-session, or right after a boss kill, walk away for five
minutes or five days, and come back to find exactly what you left — no anxiety about "did that
save," no manual save-scumming ritual, no fine print.

This directly serves the Autonomy and Competence needs called out in game-concept.md's Player
Motivation Profile: the player invested real skill (a boss kill, a precise part-break streak) and
real decisions (a Forge merge, a job assignment) to earn that state, and persistence's only
acceptable behavior is to honor that investment completely.

Unlike most of this game's systems, this one is not trying to be *felt*. Pillar-driven systems
like the Resonance Glyph visual layer earn their keep by being seen and read at a glance;
`save-load-persistence` earns its keep by disappearing so completely that the player never has a
reason to think about it. Where it does surface — a small, quiet "Saved" flicker after a
chapter-close moment (a UI hook this GDD requires but does not design) — that surfacing exists
purely to reinforce confidence, not to demand attention. It supports the Submission aesthetic (the
low-stress background layer, MDA priority 5 in game-concept.md) that lets the player fully relax
into the idle/automation half of the game's identity.

---

## Detailed Rules

### Save Triggers

Three trigger types, all automatic. There is no manual "Save Game" affordance in MVP — saving is
never the player's job.

1. **Chapter-close event autosave.** Fires immediately when the game raises one of a data-driven
   list of "chapter-close" event names (see Tuning Knobs). The MVP list, taken directly from
   game-concept.md's Session-Level loop ("boss kill → forge pass → team assignment — a clean
   chapter close"):
   - `boss_kill_confirmed`
   - `forge_pass_committed`
   - `automation_team_assigned`

   This list is data, not code — new chapter-close points added by future systems (e.g. a
   Vertical-Slice-tier hybrid-forge confirmation) register their event name without a code change,
   per `.claude/docs/coding-standards.md`'s data-driven rule.

2. **Periodic safety-net autosave.** Fires on a wall-clock interval (see Formula 1) so that a
   session which never hits a chapter-close event for a long stretch — plausible given
   game-concept.md's 30–120 minute Session-Level sessions — still bounds its worst-case loss
   window. Only active once at least one save has already occurred this session (i.e., it does not
   fire while idling on a menu with nothing yet worth saving).

3. **Save-on-exit.** Triggered by a clean application-exit request (window close, in-game "Quit to
   Desktop," or an OS shutdown/logoff signal if the platform surfaces one). Unlike the other two
   triggers, this save is **synchronous** — it briefly blocks application exit to guarantee
   completion. That block is capped at `exit_save_timeout_ms` (see Tuning Knobs); if the save
   hasn't completed by the timeout, exit proceeds anyway and the failure is logged. Trapping the
   player in a hung "Saving…" screen on their way out is a worse violation of the "close with
   confidence" fantasy than losing a few seconds of state.

Chapter-close and periodic autosaves execute **asynchronously** (background write) so they never
cost frame budget on the main thread — this is an implementation requirement for whichever engine
specialist implements it (per `.claude/docs/technical-preferences.md`'s 60 FPS / 16.6 ms budget),
not a gameplay rule, but it's stated here because it constrains trigger design: a trigger firing
does not mean the write has completed in the same frame.

**Combat encounters are not checkpointed.** None of the three triggers above fire mid-encounter,
and no in-progress encounter state is ever written to the save file. A combat encounter (owned by
`combat-encounter-system`) is treated as ephemeral, in-memory-only state. If the
application closes — crash, force-kill, or an exit somehow triggered mid-fight — that encounter's
progress is discarded; on next load the player resumes at the last completed chapter-close
boundary (region/expedition selection), never mid-fight. This keeps this Foundation system fully
decoupled from a real-time combat system that doesn't exist yet, and matches game-concept.md's own
framing of what a "clean chapter close" is.

### Save File Structure (Top-Level Contract)

The save file is a single JSON document (`System.Text.Json`, UTF-8) with two parts: a fixed
**envelope** owned by this system, and a `sections` map where each entry is an opaque payload
owned and versioned by the system that defines it. This system persists whatever a section owner
writes into its slot verbatim — it does not interpret, validate, or design the contents of any
section beyond its own `section_schema_version` header.

```jsonc
{
  "schema_version": 1,                        // envelope contract version (this GDD owns this)
  "save_metadata": {
    "save_id": "b1a2c3d4-...",                 // GUID, generated once, stable across saves
    "game_version": "0.1.0",                   // build version string, for bug-report correlation
    "created_at": 1752460800000,               // epoch ms UTC, first save this file ever existed
    "last_saved_at": 1752461000000             // epoch ms UTC, most recent successful save
  },
  "sections": {
    "hunter_build": {
      "section_schema_version": 1,
      "data": { /* owned by resonance-weaving-system / vow-condition-tracking  */ }
    },
    "creature_roster": {
      "section_schema_version": 1,
      "data": { /* owned by creature-data-schema / creature-jobs-evolution-system  */ }
    },
    "forge_inventory": {
      "section_schema_version": 1,
      "data": { /* owned by item-data-schema / the-forge-system  */ }
    },
    "region_states": {
      "section_schema_version": 1,
      "data": {
        "<region_id>": {
          "last_ticked_at": 1752461000000,     // epoch ms UTC — see Elapsed-Time Mechanism below
          "mastery_state": { /* owned by region-mastery-automation-system  */ }
        }
      }
    }
  }
}
```

MVP requires exactly the four reserved sections shown above (`hunter_build`, `creature_roster`,
`forge_inventory`, `region_states`) because those are the four state categories game-concept.md's
MVP Definition and Session-Level loop describe as persistent. A `region_states` entry is created
lazily — the first time the player enters a region (starts an expedition) — not pre-populated for
every region that exists in content data. Unvisited regions simply have no entry; static region
*definitions* (what a region contains) live in content data, not save data.

**Additive vs. breaking changes.** Adding a new optional field to the envelope or to a section's
`data` payload does **not** require bumping `schema_version` or that section's
`section_schema_version`. Renaming, removing, retyping, or changing the semantic meaning of an
existing field **does** require a bump of the affected scope — the whole-file `schema_version` for
envelope changes, or only the specific section's `section_schema_version` for changes confined to
that section's payload. This is precisely why sections are versioned independently: a change to
`forge_inventory`'s shape must not force every other section (and every save file, regardless of
whether it even uses the Forge) to be treated as incompatible.

### Versioning Scheme

- `schema_version`: integer, starts at 1, owned by this GDD. Bumped only on breaking envelope
  changes (the shape of `save_metadata` or the `sections` wrapper itself, not any individual
  section's `data`).
- `section_schema_version`: integer, starts at 1 per section, owned independently by each section's
  defining system. Bumped only on breaking changes to that section's `data` shape.
- **Policy: no backward migration is guaranteed during MVP or Vertical Slice.** This is a solo/
  small-team project under active development (game-concept.md: Large, 12–24+ month full vision,
  MVP first) — the save format **will** change repeatedly during this period, and writing
  migration code against a format that's still churning is wasted effort. If a save's
  `schema_version` or any present section's `section_schema_version` does not match what the
  running build expects, the build does not attempt to read or transform that data (see Load
  Behavior below).
- **Migration support begins at Alpha.** At that point a `migrations/` registry maps
  `(section_name, old_version) → transform`, applied sequentially on load before a section is
  handed to its owning system. This is a forward-looking commitment, not implemented by this GDD.
- This policy is an explicit, honest scope decision, not an oversight: it means internal
  playtesters during MVP/Vertical Slice should expect that updating the build can invalidate their
  save. That is an acceptable cost at this stage; it stops being acceptable at Alpha, which is why
  migration support starts there.

### Load Behavior

On application launch, before the main gameplay loop starts:

1. If no save file exists at the expected path, start a fresh game — each system's own default
   initializer populates its section. This is the normal first-run case, not an error.
2. If a save file exists, read the envelope and check compatibility (Formula 2). Loading blocks
   the launch sequence (acceptable — this happens once, before the frame loop begins, and the
   expected file size at this game's content volume is small enough that parse time is
   negligible against a human-perceptible loading transition).
3. If compatible, each present section's `data` is handed to its owning system to hydrate runtime
   state. A section that is entirely absent from an otherwise-compatible file (e.g. a save from
   before a given system existed) is treated the same as "no save file" for that section only —
   the owning system falls back to its own default initializer for just that slot.
4. If incompatible (Formula 2 is false) and the build is pre-Alpha (no migration support), the
   file is **not** deleted. It is renamed/quarantined (see Edge Cases) and a fresh game starts.
   Post-Alpha, the migration chain is attempted first; if migration itself fails, the same
   quarantine-and-fresh-start fallback applies.
5. Save file location: a single fixed path under the OS's per-user application-data directory
   (`Environment.SpecialFolder.ApplicationData` in .NET, e.g. `%AppData%/ResonanceHunter/save.json`
   on Windows — game-concept.md's stated platform is PC/Steam/Epic, so Windows is primary, but
   .NET's special-folder resolution is cross-platform by default, which costs nothing to leave
   correct now). There is exactly one save file — no slot number, no profile selection, per this
   GDD's explicit single-slot scope decision.

### Elapsed-Time / Timestamp Mechanism

This is the load-bearing piece of this GDD: the data hook that lets `region-mastery-automation-
system` build real offline/concurrent-automation progression later **without a save-format
migration** when it does. Per game-concept.md's MVP Definition, full offline-progression
simulation is explicitly out of scope right now — this system's job is only to make sure the raw
timestamp data exists, is recorded correctly, and is never lost, so that job is a pure consumption
exercise later rather than a schema retrofit.

Two clocks, both wall-clock (UTC epoch milliseconds, stored as a 64-bit integer — avoids timezone/
DST ambiguity and the 32-bit epoch-seconds rollover risk; the byte cost of milliseconds over
seconds precision is negligible at this save file's scale, so there's no reason to under-specify
it):

- **`last_saved_at`** (global, in `save_metadata`). Updated on every successful save, of any
  trigger type. Represents "when was the file on disk last written." On load, `now_ms -
  last_saved_at` tells you how long the application was closed.
- **`last_ticked_at`** (per-region, inside each `region_states` entry). Updated by whichever system
  owns "ticking" that region's automation state whenever that state is actually advanced —
  whether because the player is actively viewing that region, because an in-session background
  tick fired for a region running automation elsewhere (game-concept.md's Session-Level loop:
  "Configure an automated farming team for a previously mastered region… while you push into the
  next frontier"), or later, because a catch-up computation ran. This GDD does **not** define when
  or how often that update happens — that cadence is `region-mastery-automation-system`'s own
  design — it only guarantees the field exists, is persisted faithfully, and is timestamped on the
  same clock basis as everything else in the file.

Both clocks use the same basis deliberately: this game has no separate "paused game time" concept
to reconcile (it is a continuous real-time world, not turn-based), so wall-clock time is exactly
the quantity the design already wants — "automation runs while you're elsewhere," whether
elsewhere means another region view in the same session or the application being closed entirely.

**What this system does NOT do**: it does not compute production, currency, or loot from an
elapsed-time delta. It computes and exposes the delta (Formula 3) faithfully; turning that delta
into gameplay resources — including whether and how far it applies to a fully-closed-application
gap versus only an in-session gap, and any upper bound on how much elapsed time is "worth" — is
entirely `region-mastery-automation-system`'s decision, made when that GDD is authored.

---

## Formulas

Persistence is mostly pure mechanism, but three checks are genuinely formulaic and load-bearing
enough to specify precisely rather than describe in prose.

### Formula 1 — Periodic Autosave Trigger

```
Trigger_periodic = (Now_ms − LastSave_ms) ≥ (I_autosave × 1000)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| Now_ms | int64 | unbounded, increasing | Current wall-clock time, epoch ms UTC |
| LastSave_ms | int64 | unbounded, increasing | Epoch ms UTC of the most recently completed save (any trigger type) |
| I_autosave | float (seconds) | 30–600 (tuning knob) | Configured periodic autosave interval |
| Trigger_periodic | bool | {true, false} | Whether a periodic autosave should fire on this check |

**Output range**: boolean — deterministic, re-evaluated on a fixed check cadence (e.g. once per
second; the check itself is cheap and not frame-budget-sensitive).

**Worked example**: `I_autosave = 120`. The last save completed at `LastSave_ms = 1,752,461,340,000`.
At `Now_ms = 1,752,461,460,000` (120,000 ms later), `(1,752,461,460,000 − 1,752,461,340,000) =
120,000 ≥ 120,000` → `Trigger_periodic = true`. A save fires and `LastSave_ms` resets to the new
save's completion time.

### Formula 2 — Schema Compatibility Check

```
IsCompatible = (V_file = V_build) ∧ (∀ s ∈ Sections(file): Vs_file(s) = Vs_build(s))
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| V_file | int | ≥ 1 | `schema_version` read from the save file's envelope |
| V_build | int | ≥ 1 | `schema_version` the running build expects |
| Sections(file) | set of strings | subset of {hunter_build, creature_roster, forge_inventory, region_states, …} | Section names actually present in the loaded file |
| Vs_file(s) | int | ≥ 1 | `section_schema_version` recorded for section `s` in the file |
| Vs_build(s) | int | ≥ 1 | `section_schema_version` the running build expects for section `s` |
| IsCompatible | bool | {true, false} | Whether the save may be loaded without a migration step |

**Output range**: boolean. Evaluated once per load attempt, before any section's `data` is handed
to its owning system.

**Worked example**: Build expects `V_build = 1` and section versions `{hunter_build: 1,
creature_roster: 1, forge_inventory: 1, region_states: 1}`. A save file has `V_file = 1` and
identical section versions → `IsCompatible = true`, normal load proceeds. Later, a build change
bumps `region_states`'s expected version to 2 (its shape changed) but an old save file still has
`region_states: 1` → `IsCompatible = false`, even though every other section still matches — only
that one file's incompatibility triggers the fallback behavior in Edge Cases, and per the
per-section versioning design, `hunter_build`, `creature_roster`, and `forge_inventory` did not
need to be touched at all to make this change.

### Formula 3 — Elapsed Time Delta (the future offline-progression hook)

```
ElapsedMs(x) = max(0, Now_ms − LastTickedAt_ms(x))
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| x | identifier | any timestamped entity adopting this pattern (MVP: a `region_id`) | The section/entity whose elapsed time is being measured |
| Now_ms | int64 | unbounded, increasing | Wall-clock time at the moment of computation (on load, or whenever the owning system next ticks `x`) |
| LastTickedAt_ms(x) | int64 | unbounded, increasing | The persisted `last_ticked_at` value for `x` |
| ElapsedMs(x) | int64 | [0, +∞) | Milliseconds of wall-clock time since `x` was last advanced, floor-clamped at 0 |

**Output range**: `[0, +∞)` milliseconds. Floor-clamped at 0 as a persistence-layer sanity rule
(protects against a rolled-back system clock producing a nonsensical negative duration — see Edge
Cases). Deliberately **not** upper-clamped by this GDD: any gameplay-facing cap on how much
elapsed time is "worth" (e.g., capping offline-progression gains at some maximum duration) is a
balance decision owned by `region-mastery-automation-system`, made when that system is designed —
clamping it here would bake a not-yet-designed balance number into the Foundation layer.

**Worked example**: The player closes the game at `Now_ms = 1,752,461,000,000` with region
`emberfall_reach` last ticked at `LastTickedAt_ms = 1,752,460,700,000` (5 minutes earlier,
mid-session). Reopening the app 3 hours later, `Now_ms = 1,752,471,800,000`.
`ElapsedMs("emberfall_reach") = max(0, 1,752,471,800,000 − 1,752,460,700,000) = 11,100,000` ms
(≈ 185 minutes, ~3 h 5 min). This raw delta is computed and available on load; per MVP scope
(full offline-progression simulation deferred, game-concept.md's MVP Definition), nothing consumes
it yet — it is simply correct and waiting for `region-mastery-automation-system` to exist.

---

## Edge Cases

- **Corrupted save file (unreadable/malformed JSON).** Detected at load via JSON parse failure or
  missing required envelope fields (`schema_version`, `save_metadata`). The load falls back to the
  most recent `.bak` backup (see Tuning Knobs — `backup_retention_count`). If the backup is also
  unreadable, both files are quarantined (renamed, not deleted — see below) and a fresh game
  starts. The player is shown a clear message that their save could not be loaded. No exception
  propagates to crash the application in either case.
- **Crash mid-save.** Writes use a temp-file-then-atomic-replace pattern: the full new save state
  is written to a temp file in the **same directory** as the target (cross-volume renames are not
  atomic — writing to a different volume would break this guarantee), then that temp file
  atomically replaces the primary save file, with the previous primary rotated to `.bak` as part of
  the same atomic operation (.NET's `File.Replace` performs exactly this in one call on Windows;
  an equivalent atomic replace-with-backup should be used per platform). A crash before the replace
  completes leaves the pre-existing primary save file fully intact — the in-flight save attempt is
  simply lost, not the file it was replacing. A crash during the replace itself cannot leave a
  half-written primary, because the replace operation is atomic at the OS/filesystem level.
- **Schema-version mismatch, older save vs. newer build.** Per the stated MVP/Vertical-Slice
  policy (no migration guaranteed), the mismatched file is not read. It is renamed with an
  `.incompatible-v<N>` suffix (never deleted) so a developer can inspect or manually recover it,
  and a fresh game starts. Post-Alpha, migration is attempted first; if the migration chain fails
  partway, the same quarantine-and-fresh-start fallback applies rather than leaving the player in
  an ambiguous half-migrated state.
- **Schema-version mismatch, newer save vs. older build** (e.g. a build rollback, or a branch
  running an older client against a save from a newer one). Treated identically to the case above
  — `V_file > V_build` or any `Vs_file(s) > Vs_build(s)` still fails Formula 2, and no
  forward-compatibility guessing is attempted. Quarantine-and-fresh-start applies the same way.
- **System clock is wrong.** Affects both `last_saved_at` and `last_ticked_at` deltas.
  - Clock rolled backward (delta would be negative): Formula 3 floor-clamps to 0 — treated as "no
    time elapsed," which is the safest default (grants no benefit, cannot be exploited, does not
    unfairly punish the player either).
  - Clock jumped implausibly far forward: this GDD records the raw timestamp faithfully and does
    not clamp it — an upper-bound clamp on what an implausible delta is allowed to be "worth" in
    gameplay terms is explicitly `region-mastery-automation-system`'s responsibility (see Formula
    3's output range note and the Tuning Knobs entry below), because that clamp is a balance value,
    not a persistence-layer sanity rule.
- **No save file exists.** Normal first-launch case, not an error — see Load Behavior step 1.
- **Save directory unwritable (permissions, read-only medium, disk full).** The save attempt fails
  without crashing the application. The failure is surfaced as an event/flag this system emits (a
  hook for whichever UI owns the "Saved" indicator to instead show a "save failed" state — the UI
  itself is out of this GDD's scope). Because writes go to a temp file first, a failed write never
  touches the existing valid primary save file — only progress since the last successful save is
  at risk, never prior progress.
- **Concurrent save triggers** (e.g. the periodic timer fires while an event-triggered save from a
  boss kill is still in flight). Saves are serialized — never two concurrent writers to the same
  file. A trigger that fires while a save is already in progress is coalesced into a single
  follow-up save that runs immediately after the in-flight one completes, rather than running in
  parallel.
- **Application force-killed or loses power** (not a clean exit — save-on-exit never fires). The
  most recent completed autosave (chapter-close or periodic) is what's restored on next load;
  anything after that point is lost. This is an accepted, bounded trade-off — the periodic
  autosave interval (Tuning Knobs) directly bounds the worst-case loss window — not a bug to fix
  here.

---

## Dependencies

**Depends On**: None. This is a Foundation-tier system (`design/gdd/systems-index.md`'s Dependency
Map, Foundation Layer) — it must remain implementable before `creature-data-schema`,
`item-data-schema`, or any gameplay system exists in detail, and it does not read or assume the
shape of any other system's data beyond the generic `data` + `section_schema_version` envelope.

**Depended On By**:

- **`region-mastery-automation-system`** (explicit edge in `systems-index.md`) — critically depends
  on this GDD's elapsed-time mechanism (`last_ticked_at`, Formula 3) to build offline- and
  concurrent-automation progression on top of, without a future save-format migration. This is the
  single most important consumer this GDD was designed for.
- **`accessibility-settings-system`** — does not store data inside `save.json` (its own Assumption
  A1), but directly reuses this document's atomic temp-file-then-atomic-replace write pattern and
  additive-vs-breaking schema-versioning philosophy for its own independent
  `accessibility_settings.json` file (that document's own §6). Confirmed here, bidirectionally.
- **`settings-menu-ui`** — per `systems-index.md`'s entry #25, depends on this system directly; in
  practice the dependency is indirect, riding on `accessibility-settings-system`'s reuse of this
  document's write pattern rather than touching `save.json` itself (that document's own §6). Never
  writes to or reads this document's save sections directly.
- **`hunter-progression-system`** — must persist `ranks_purchased[stat]` (9 integers) per save (that
  document's own §6); assumes but does not specify the persistence mechanism beyond this document's
  generic contract. A new, additive save section, not a modification to `hunter_build`,
  `creature_roster`, `forge_inventory`, or `region_states`.
- **`memory-dust-prestige-system`** — persists `lifetime_memory_dust_earned`,
  `lifetime_memory_dust_spent`, the per-`(region_id, milestone_event)` claimed-milestone set, and the
  `unlocks_owned` set (that document's own §6) as one additive, independently-versioned save section
  following this document's `section_schema_version` pattern — a new section, not a modification to
  any existing one, so it introduces no envelope-level `schema_version` bump.
- **Every other system with persistent state** will integrate with this GDD's save-file contract
  once it is written, even though `systems-index.md`'s dependency graph does not draw an explicit
  edge to `save-load-persistence` for most of them yet (only `region-mastery-automation-system`
  lists it today, because it's the only one whose GDD has a concrete reason to reference the
  timestamp mechanism specifically). Anticipated future integrators, by MVP-reserved section:
  - `hunter_build` — owned by `resonance-weaving-system` / `vow-condition-tracking`
  - `creature_roster` — owned by `creature-data-schema` / `creature-jobs-evolution-system`
  - `forge_inventory` — owned by `item-data-schema` / `the-forge-system`
  - `region_states` — owned by `region-mastery-automation-system`
- **`combat-encounter-system`** does **not** integrate with this system directly
  — per this GDD's Detailed Rules (Save Triggers), encounters are explicitly non-persistent.

---

## Tuning Knobs

| Knob | Default | Safe Range | Affects | Notes |
|------|---------|------------|---------|-------|
| `autosave_interval_seconds` | 120 | 30–600 | Worst-case progress-loss window on crash/force-kill vs. write frequency/I-O overhead | Must run as a background/async write so it never costs frame budget (16.6 ms target per `technical-preferences.md`). Below 30s risks noticeable I/O overhead as save-file size grows with roster/region content; above 600s meaningfully erodes the "close with confidence" fantasy given 10–30 min active session bursts (game-concept.md) |
| `chapter_close_trigger_events` | `[boss_kill_confirmed, forge_pass_committed, automation_team_assigned]` | data-driven list, 0–N entries | Which gameplay events force an immediate save | Extend when future systems add new "clean chapter close" points (e.g. a Vertical-Slice hybrid-forge confirmation) — no code change required |
| `exit_save_timeout_ms` | 3000 | 500–5000 | How long a clean application exit blocks waiting for the final save before giving up | Below ~500ms risks the save never completing on slower disks; above ~5000ms risks the exit feeling hung to the player |
| `backup_retention_count` | 1 | 1–3 | How many rotated `.bak` copies are kept for corrupted-save recovery | Higher = more recovery safety net; disk cost is negligible at this save file's scale |
| `timestamp_precision` | milliseconds (epoch, UTC) | fixed (ms or s) | Granularity of every `last_saved_at` / `last_ticked_at` value | Locked technical decision, not a live-tunable — listed here because changing it later is a breaking envelope change requiring a `schema_version` bump |
| `schema_version` (current build value) | 1 (initial) | increments by 1, no upper bound | Gates load-compatibility (Formula 2) | Bump only on breaking envelope changes; section-local changes bump only that section's `section_schema_version` instead — see Detailed Rules' additive-vs-breaking rule |
| `offline_elapsed_upper_clamp` | **not set — explicitly deferred** | N/A in this GDD | Would cap how much `ElapsedMs` (Formula 3) can ever be "worth" in gameplay terms | Intentionally not defined here. Owned by `region-mastery-automation-system`. Listed in this table only to make the scope boundary explicit and easy to find for whoever designs that system next |

---

## Acceptance Criteria

1. Closing the game via a clean exit and reopening it restores the `hunter_build`,
   `creature_roster`, `forge_inventory`, and `region_states` sections to exactly the state at the
   last successful save — no data loss for a clean-exit scenario. (Verifiable today with stub
   section payloads since the owning systems don't exist yet; re-verify end-to-end once they do.)
2. A save is written after each of the three MVP chapter-close trigger events (`boss_kill_confirmed`,
   `forge_pass_committed`, `automation_team_assigned`) — verified by `save_metadata.last_saved_at`
   advancing immediately after each triggering action, without waiting for the periodic interval.
3. With no chapter-close event fired, a periodic autosave still occurs at the configured
   `autosave_interval_seconds` (± acceptable scheduler jitter) during an active session.
4. Given a save file whose `schema_version` differs from the running build's expected value, on
   load the game does not crash, starts a fresh game state, and the original file still exists on
   disk under an `.incompatible-v<N>` suffix (not deleted).
5. Given a save file with deliberately truncated/invalid JSON, on load the game does not crash,
   falls back to a valid `.bak` backup if one exists, or starts a fresh game state if it doesn't —
   in both cases, no unhandled exception reaches the player.
6. Simulating process termination after a save's temp file is fully written but before the atomic
   replace completes leaves the pre-existing primary save file fully intact and loadable on the
   next launch.
7. Two save triggers fired within the same check window (e.g. the periodic timer and a chapter-
   close event coincide) result in exactly one write executing at a time — the resulting file
   parses as valid JSON, with no interleaved/corrupted output.
8. Given a region's persisted `last_ticked_at` and a known `now`, the computed `ElapsedMs` exactly
   equals `now_ms − last_ticked_at_ms` for a forward-clock case, and clamps to `0` for a
   backward-clock (negative-delta) case.
9. With no save file present (first launch), the game starts with each system's documented default
   state and does not error.
10. Writing a save and immediately loading it produces a runtime state deep-equal, section by
    section, to the pre-save state, for every section type registered at the time of the test
    (round-trip fidelity).

---
