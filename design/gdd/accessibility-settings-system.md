# Accessibility Settings System: IDLExIDLE

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | accessibility-specialist |
| **Status** | Complete — authored autonomously, no user available this session (per this session's stated constraint). Ambiguities resolved using `design/art/art-bible.md` (§4.5, §4.6, §5.6, §7.1–§7.7), `design/gdd/input-targeting-system.md`, `design/gdd/save-load-persistence.md`, `design/gdd/game-concept.md`, and `.claude/docs/technical-preferences.md` as authority; every resolution is flagged inline as an assumption below. |
| **Priority / Tier** | MVP — Feature layer (`design/gdd/systems-index.md` #9) |
| **Depends On** | `input-targeting-system` (aim-assist, cycle timing, exact field names), `save-load-persistence` (settings-persistence pattern — see Assumption A1 for how, not just whether) |
| **Depended On By** | `combat-hud`, `forge-ui`, `creature-roster-ui`, `automation-config-ui`, `loot-filter-ui`, `region-view-world-map-ui`, `creature-ai-telegraph-system`, `audio-system`, `combat-encounter-system` (see §6) |

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | Accessibility settings persist in their **own file** (`accessibility_settings.json`), not as a fifth section inside `save-load-persistence.md`'s `save.json`. It reuses that GDD's atomic temp-then-replace write pattern and independent-versioning philosophy, but is written eagerly on every settings change rather than gated behind gameplay chapter-close/periodic triggers, and survives a "New Game" or corrupted-save quarantine untouched. | Settings must persist before any save game exists (a player can open Settings from the main menu before starting a game) and must feel instant when changed — waiting on a 120-second periodic autosave or a `boss_kill_confirmed` event for a UI-scale change to stick would break the "the empire never disappears" trust `save-load-persistence.md` establishes for *game* data, and conflating "preferences" with "game progress" would force special-casing every reset/quarantine/new-game path to protect this section. Separating the files removes that special-casing entirely. |
| A2 | Settings are **global, not per-profile.** | `game-concept.md` confirms single-player only with no social/multi-user layer; `save-load-persistence.md` independently locked a single-slot, no-profile save model for the same reason. There is nothing to be "per-profile" of. |
| A3 | "Colorblind mode" is reinterpreted as an opt-in **CVD-simulation/grayscale preview tool** (`colorblind_preview_mode`), not a corrective palette filter. | Art-bible §4.6 establishes the game is colorblind-safe *by construction* — shape, edge quality, mass distribution, and value-steps already carry every mechanically important state without color. A corrective filter has nothing left to correct. A preview tool gives players (and QA) a way to self-verify that claim, which is a more honest and more useful thing to ship than a filter solving an already-solved problem. |
| A4 | The HUD-space glyph minimum floor is set to **16px at a 1080p reference resolution**, matching art-bible §8.2's glyph-micro / chrome-icon native asset size exactly. | Those assets are already authored at 16px; setting the floor any lower would be meaningless (nothing renders smaller than its native asset), and setting it higher would force a re-authoring pass this document has no authority to demand. 16px is the natural, already-locked floor. |
| A5 | High-contrast mode is scoped to **non-text chrome interaction-state contrast only** (hover/focus/selected/disabled/drag-preview value-steps and border weights), not body text. | Formula 4 (§4) computes the baseline body-text pairing (Bone Parchment on Void Ink) at ≈13.38:1 — already far past both AA (4.5:1) and AAA (7:1). There is no real text-contrast problem for a "high contrast" mode to solve. The genuine gap is WCAG SC 1.4.11 (Non-text Contrast) for interaction states, whose exact differentiating hex/weight values are not authored in any GDD yet — that is where this setting's mandate actually belongs. |
| A6 | One-handed play and Xbox Adaptive Controller support are treated as **emergent properties** of full input remapping plus the already-locked no-stick-cursor, cycle-and-confirm architecture, not bespoke dedicated modes. | Building and QA-validating a hand-authored one-handed key layout or certifying against physical XAC hardware is out of scope for a design document with no hardware access this session. Full remapping plus a small, discrete-button-only input surface (no analog stick required for core combat, per art-bible §7.7's binding decision) already makes both achievable without a dedicated preset. |
| A7 | A low-stakes practice mode and a general combat-speed slider are **explicitly recommended but scoped out** of this system's settings surface. | Both are gameplay-flow features requiring `combat-encounter-system`'s own encounter/consequence model, which does not exist yet. This document defines the *settings surface*; a practice mode is a *game mode*, not a toggle, and belongs to the system that owns encounters. |
| A8 | No discrete Easy/Normal/Hard difficulty system is proposed. | This system's own aim-assist and cycle-timing sliders, combined with the already-locked, non-adjustable telegraph wind-up floor (art-bible §7.7), are judged to already cover the accessibility-relevant portion of "difficulty" that Pillar 1's reading-not-reflexes philosophy cares about. A difficulty system for non-accessibility (challenge/score) reasons is a separate game-design decision outside this document's mandate. |

---

## 1. Overview

The accessibility settings system is the Meta-tier settings surface that makes every accessibility
commitment already locked elsewhere in this project's design — art-bible §7.7's binding
requirements, `input-targeting-system.md`'s aim-assist and cycle-timing parameters, and the
standing accessibility guidelines this specialist enforces — into concrete, player-facing,
persisted settings. It owns the settings menu's data model (not its visual layout, which is
`ux-designer`/`ui-programmer` territory at `/ux-design`), the safe range and default for every
accessibility-relevant value in the game, the persistence mechanism that survives application
restarts, and a set of binding requirements this system imposes on every other UI and gameplay
system rather than mechanics it implements itself. It explicitly does **not** re-derive the
colorblind-safety architecture (art-bible §4.6, already locked), the cycle-and-confirm targeting
model or hitbox padding math (`input-targeting-system.md`, already locked), or the telegraph
wind-up floor value (owned by `creature-ai-telegraph-system`) — this document's job is the
**settings surface and the rules governing it**, not a re-litigation of decisions already made.

---

## 2. Player Fantasy

> **Reading the fight correctly is the entire skill this game tests — and that promise is a lie for
> any player it doesn't actually hold for.**

`input-targeting-system.md`'s Player Fantasy already stated this once for targeting specifically;
this document states it for the whole game. Pillar 1 (Precision Over Reflexes) claims that mastery
in IDLExIDLE is expressed through pattern recognition, not movement or twitch reflexes — a
design thesis this game markets as fairer, calmer, and more cerebral than the genre's reflex-testing
norm. That claim is not a marketing flourish this system merely supports; it is a claim this system
is the mechanism that makes *true*. A game whose thesis is "success is a reading skill, never a
reflex test" is not accessible-by-default just because it lacks twitch mechanics — a player with a
worn mouse and no fine motor precision still loses to a hardware limitation if hitbox padding isn't
generous enough; a player with tremor still can't play at all if cycle-and-confirm doesn't exist as
a first-class path; a player who processes fast visual change poorly still gets blindsided by a
telegraph with no audio equivalent; a player scaling their 4K monitor's UI still can't read a HUD
glyph that was only ever tested at 1080p. Every one of those failures would mean the "reading skill,
not reflex test" promise was accidentally reflex-testing someone anyway, through a door the design
never intended to leave open.

This system exists so that door stays closed. Concretely, it guarantees:

- **The read is available to every player, regardless of how they perceive it.** Colorblind-safe by
  construction (art-bible §4.6) plus a self-verification tool (§3.2) means no player has to take the
  game's word for it. A guaranteed HUD-space text and glyph legibility floor (§3.2, §4) means no
  display configuration silently shrinks the information below readable.
- **Acting on a correct read is never gated by a body or a device.** Full remapping, cycle-and-confirm
  timing control, and an aim-assist strength the player owns entirely (§3.3) mean a correct read
  reliably becomes a correct action, the same promise `input-targeting-system.md` already made for
  targeting specifically, now extended to every input surface in the game.
- **The moment that demands a reaction is never faster than a player can perceive it, and never
  silent for a player who can't hear it.** The fixed telegraph wind-up floor (owned elsewhere,
  enforced here as non-negotiable) plus a redundant audio pre-cue and a guaranteed always-present
  visual channel (§3.4) mean no player's sensory profile determines whether the core loop is even
  playable.
- **This system does not exempt itself.** A settings menu that is itself unreadable, unnavigable by
  keyboard, or low-contrast would be a betrayal of everything it configures — §3.8 holds this menu to
  the same bar it sets for the rest of the game.

Accessibility here is not charity layered on top of the game. It is Pillar 1, made real instead of
accidental, for every player attempting to read it.

---

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This system defines the **settings data model, defaults, ranges, persistence, and the binding
requirements it imposes on other systems.** It is explicitly silent on:

- The visual layout, navigation flow, and menu chrome of the Settings screen itself — that is
  `ux-designer`'s job at `/ux-design`, constrained by this document's §3.8.
- The mechanics being configured: hit-testing, cycle-order sorting, and the magnetism pull formula
  belong entirely to `input-targeting-system.md`; telegraph timing and the wind-up floor value belong
  entirely to `creature-ai-telegraph-system`; audio mixing/DSP implementation
  belongs to `audio-system`. This system only ever defines the **knob**, its safe
  range, and its default — never the math or asset behind it, except where that math is this
  system's own (UI scale, contrast).
- Combat balance and difficulty tuning generally — see Assumption A8.

### 3.1 Persistence Architecture

Per Assumption A1, accessibility settings live in a dedicated file, independent of
`save-load-persistence.md`'s `save.json`:

- **Path**: `%AppData%/ResonanceHunter/accessibility_settings.json` (same OS special-folder
  resolution as `save-load-persistence.md`'s save file, for the same cross-platform-by-default
  reasoning — Windows primary per `game-concept.md`'s stated platform, but not hardcoded to it).
- **Format**: a single JSON document (`System.Text.Json`, UTF-8), with its own envelope:

```jsonc
{
  "settings_schema_version": 1,
  "settings": {
    "ui_scale_percent": 100,
    "high_contrast_mode": false,
    "colorblind_preview_mode": "off",
    "keybind_map": { "cycle_next": "GamepadRightBumper", "cycle_prev": "GamepadLeftBumper",
                      "confirm": "GamepadA", "...": "..." },
    "magnetism_strength": 0.0,
    "cycle_initial_delay_ms": 350,
    "cycle_repeat_interval_ms": 120,
    "controller_vibration_enabled": true,
    "reduced_motion_enabled": false,
    "telegraph_audio_precue_enabled": true,
    "volume_master": 100, "volume_music": 100, "volume_sfx": 100,
    "volume_dialogue": 100, "volume_ui": 100,
    "audio_loudness_normalization_enabled": false,
    "mono_audio_enabled": false,
    "simplified_ui_density": false
  }
}
```

- **Versioning**: `settings_schema_version` follows the exact additive-vs-breaking policy
  `save-load-persistence.md` established for its own `schema_version` — adding an optional field
  never bumps it; renaming, removing, or re-typing an existing field does. This is an independent
  counter; it has no relationship to `save.json`'s `schema_version` or any section's
  `section_schema_version`.
- **Write triggers**: eager, debounced ~500ms after the last change to any setting, and
  unconditionally on Settings-menu close. **Never** gated behind a chapter-close event, the periodic
  autosave interval, or the existence of a save game — a player who has never started a game can
  still open Settings and have changes stick.
- **Write mechanism**: reuses `save-load-persistence.md`'s temp-file-then-atomic-replace pattern
  (same crash-safety rationale: a write in flight must never corrupt the last-good file). No `.bak`
  rotation is required — unlike a lost game save, a lost settings file degrades gracefully to
  defaults (§5, Edge Case #9), which is an acceptable, low-stakes fallback this file's small,
  low-value footprint justifies.
- **Load behavior**: on application launch, before the settings menu or main menu can be interacted
  with, this file is read and validated. If absent (first launch) or a version mismatch is detected,
  the runtime settings state initializes to the defaults listed in §7 — never a partial or garbled
  state, and never blocking launch past a human-imperceptible delay (this file's size is trivial
  compared to `save.json`).
- **Independence from `save.json`**: deleting, quarantining, or starting a fresh game via
  `save-load-persistence.md`'s mechanisms never touches this file. A player's chosen UI scale, key
  bindings, and volume levels survive a corrupted or reset game save.

### 3.2 Visual Accessibility Settings

| Setting | Type | Range | Default | What It Affects |
|---|---|---|---|---|
| `ui_scale_percent` | int (%) | 100–200 | 100 | Scales Data-face text, chrome icons, borders, and glyph-grammar UI content across every UI-space screen (combat HUD's corner cluster, Forge, Creature Roster, Automation Config, Loot Filters, Region View). Does **not** scale the Arena's virtual-canvas pixel art (integer-locked separately, `technical-preferences.md`) or any diegetic Hunter/creature content — those are gameplay-world assets, not chrome. See Formula 1/2 (§4). |
| `high_contrast_mode` | bool | off / on | off | Widens the value-step delta and border weight between chrome interaction states (hover, keyboard-focus, selected, disabled, drag-preview/drop-target) within the existing three-color chrome budget (Void Ink / Bone Parchment / Cold Slate, art-bible §4.5). Does **not** alter glyph-content hex values (Source colors, Ember Threat, Hearth Gold, Corruption Bloom all stay exactly as authored) and does not touch body text color — see Formula 4 and §5 Edge Case #3 for why. |
| `colorblind_preview_mode` | enum | `off` \| `protanopia` \| `deuteranopia` \| `tritanopia` \| `grayscale` | `off` | A single screen-space post-process applied to the fully composited final frame (Arena + UI layers together, not per-asset), approximating each CVD type (standard simulation matrices) or full desaturation for `grayscale`. Self-verification tool, not a corrective filter — see Assumption A3. The `grayscale` option is also this system's literal implementation of Acceptance Criterion #4 (§8) and the design test embedded throughout art-bible §3–4 ("cover it in gray and confirm it still reads"). |
| *(non-tunable floor, not itself a setting)* `HUD_GLYPH_MIN_PX_AT_1080P = 16` | constant | fixed | 16px | Every glyph-grammar container and chrome icon rendered in UI space must never render below this size at any `ui_scale_percent`/resolution combination — see Formula 1/2's clamp (§4). Independent of art-bible §5.6's 12–16px *world-space* floor, which is tied to creature sprite scale and does not transfer to UI space (per this document's mandate, art-bible §7.7). |

**On colorblind mode, argued plainly**: art-bible §4.6 audits every place two meanings could
visually converge for a colorblind player and finds, in every case, a surviving non-color cue —
shape, edge quality, mass distribution, value-step, or discrete ring-count. Because color is never
the sole channel for anything mechanically important by design, a dedicated corrective color-remap
mode has no defect to correct. Building one anyway would either (a) do nothing, because the
non-color cues already carry the information, or (b) actively fight the art system by remapping
Source colors that are deliberately *not* load-bearing (art-bible §4.2: "reinforcing, not
load-bearing... color's job is to reward a closer look, not to be required for a correct one"),
risking a false signal that a shifted hue means something it doesn't. The honest, useful thing to
ship instead is the preview tool above: it lets a player (or QA) confirm the colorblind-safety claim
for themselves rather than asking them to trust it.

**Remaining risk from §4.6's audit table**: rarity tiers are the one entry §4.6 itself flags as "a
fine discrimination task for any viewer, harder for low-vision/CVD players" even with the ring-count
backup. This system does not add a new rarity-specific setting — the backup art-bible §4.6 already
specifies (frame ring-count as the mechanical signal, item tooltip text as final fallback) is
sufficient, *provided* that tooltip text meets this system's own text-legibility floor (§3.8) and is
never itself color-only. Acceptance Criterion #14 (§8) makes this an explicit, testable requirement
rather than an assumed-safe leftover.

### 3.3 Motor Accessibility Settings

| Setting | Type | Range | Default | What It Affects |
|---|---|---|---|---|
| `keybind_map` | dict\<action_id, binding\> | any valid key/mouse-button/gamepad-button per platform | engine defaults (gamepad: bumpers/D-pad for cycle, face button for confirm, per art-bible §7.7) | Full remapping for keyboard, mouse, and gamepad. MVP-required action IDs, reusing `input-targeting-system.md`'s exact names: `cycle_next`, `cycle_prev`, `confirm` (§3.4 of that document). Forward-reserved slots for `combat-encounter-system`'s eventual actions (`ability_1`, `ability_2`, `ability_3`, `ultimate`, `dodge_block`) — this framework is built now because `input-targeting-system` already needs it, and `combat-encounter-system` registers into the same table when it exists rather than this system inventing a second remapping mechanism later. |
| `magnetism_strength` | float | 0.0–1.0 | 0.0 (off) | **Exact field name from `input-targeting-system.md` Formula 2.** The Settings menu displays this as a 0–100% slider (display-only linear mapping; the persisted value is the raw float, unmodified — see Formula 3, §4). Mouse-only; no effect on the cycle-and-confirm path (`input-targeting-system.md` §5, Edge Case #8). |
| `cycle_initial_delay_ms` | int (ms) | 200–600 | 350 | **Exact field name from `input-targeting-system.md` §7.** Time a cycle input must be held before auto-repeat begins. Exposed here per `input-targeting-system.md`'s explicit delegation ("`accessibility-settings-system` is expected to expose... the global tuning constants in §7") and the standing motor-accessibility guideline of adjustable hold duration. |
| `cycle_repeat_interval_ms` | int (ms) | 60–250 | 120 | **Exact field name from `input-targeting-system.md` §7.** Time between auto-repeated cycle steps while held. Same rationale as above — adjustable repeat delay. |
| `controller_vibration_enabled` | bool | off / on | on | Toggles gamepad rumble/haptic feedback entirely. A comfort/sensory-sensitivity setting, not strictly motor, but grouped here as the input-device-adjacent toggle. |

**Explicitly NOT exposed as player-facing settings** (stated here for clarity and to prevent a
future system from accidentally exposing them): `padding_base_px`, `padding_inverse_scalar`,
`reference_part_size_px`, `padding_min_px`, `padding_max_px` (`input-targeting-system.md` §3.6,
Formula 1 — hitbox padding is mandatory, always-on, and a global design constant per art-bible §7.7,
never a player toggle), and `magnetism_radius_px` (`input-targeting-system.md` §7 — designer-tuned
only; that document flags only `magnetism_strength` as "the one knob in this table meant to be
end-user-configurable," and this document honors that distinction exactly).

**One-handed play and adaptive controller support**: per Assumption A6, neither gets a dedicated
mode. The full `keybind_map` above lets any player remap every action onto keys reachable with one
hand, and the cycle-and-confirm architecture (art-bible §7.7's binding decision — no analog stick
required for core combat) means the entire combat input surface is a small set of discrete buttons,
which is exactly the input shape the Xbox Adaptive Controller and most switch-adapted input devices
are built to expose. MonoGame's standard `GamePad` API surfaces any XInput-compatible device without
special-case code; this document flags XAC compatibility as an expected consequence of the
architecture, to be confirmed by hardware-in-hand QA once a build exists (see §8, open item).

**No simultaneous multi-button requirement**: a binding requirement this system imposes on
`combat-encounter-system` when it is designed (§3.7) — no ability or interaction may require two or
more inputs pressed at the same instant without a toggle alternative. Nothing in the currently
locked design (targeting, cycling, confirming) violates this; it is stated here as a constraint the
next system to add inputs must honor.

**QTEs**: none exist in the currently locked design. Stated preemptively as a binding requirement
(§3.7): if `combat-encounter-system` ever introduces a timed quick-input event, it must ship with a
skip or auto-complete option from day one, not retrofitted later.

**Adjustable game speed / practice mode**: per Assumption A7, out of this system's settings surface.
Recommended strongly as a coordination note to whoever designs `combat-encounter-system` next — see
§6 and §3.6.

### 3.4 Motion Sensitivity Settings

| Setting | Type | Range | Default | What It Affects |
|---|---|---|---|---|
| `reduced_motion_enabled` | bool | off / on | off | Suppresses **ambient/decorative** motion independently of the core mechanic. See exact suppression list below. |
| `telegraph_audio_precue_enabled` | bool | off / on | **on** | Enables a redundant audio cue on `creature-ai-telegraph-system`'s slow-build/hard-cutoff curve (owned by that system; this is the player-facing toggle for it). Defaults on because it is explicitly an accessibility *assist*, not a balance change — leaving it on costs nothing for players who don't need it and helps a broader audience (including players who simply react faster to audio) than an opt-in default would. |

**Explicitly NOT exposed, stated for clarity**: the telegraph wind-up duration floor itself. It is
owned entirely by `creature-ai-telegraph-system`, fixed regardless of difficulty scaling, and this
system provides **no control, slider, or setting of any kind that could push it downward** — not
even indirectly through a "faster telegraphs" difficulty toggle, because none is proposed (Assumption
A8). This is stated as a binding constraint in §3.7.

**`reduced_motion_enabled` suppression list — exactly what is and isn't suppressible, and why**:

| Suppressed when enabled | Never suppressed | Why the line falls here |
|---|---|---|
| Ambient particle drift (art-bible §8.2 VFX ambient layer; §6.6 corruption particle work) | The telegraph wind-up pulse itself (art-bible §2, §3.5) | The telegraph pulse **is** the core mechanic's only signal — Pillar 1's entire combat read depends on it. Suppressing it wouldn't reduce motion sensitivity risk, it would remove the game. |
| Idle-view flicker (the unconquered region's unstable ambient wash, art-bible §2, §4.4) | Work-loop pulses (art-bible §5.3 — Healthy/Blocked/Starved states) | These are information-bearing, exactly like the telegraph — a Starved creature's distress cadence is the *only* signal of that state (no HUD widget exists for it, per art-bible §7.4). Suppressing it would hide real information, not reduce nausea risk. |
| Menu/UI transitions — slide, fade, easing (art-bible §7.3, "Standard menu transitions") | Ceremony-tier motion — the Forge's Vow-binding molten-to-cooled transition, the Mastery Transition's light sweep (art-bible §2, §7.6) | These are rare (once per boss kill, once per Vow bind), occur at natural pause points chosen by the player, are never continuous or looping, and carry essential feedback about a significant one-time event. Motion-sensitivity risk is driven overwhelmingly by *continuous, repetitive, or large-field* motion (WCAG's own framing for animation-from-interaction concerns) — a one-shot ceremony beat the player triggers themselves is a fundamentally different risk profile than a looping ambient effect running in the background of every screen. |
| Any future camera-shake/screen-shake effect (forward binding requirement — none is currently designed, but if `combat-encounter-system` or `audio-system` adds one, it must gate on this flag) | — | Camera shake is a well-established motion-sickness trigger not yet present in any locked design; flagged now so it isn't shipped un-gated later. |

This resolves the specific scenario asked of this document: **reduced-motion is on, and the
telegraph — the mechanic that requires motion to read — cannot be disabled.** That is not a gap; it
is the deliberate boundary. Reduced-motion targets *decorative* motion a player never needs to act
on. The telegraph's mitigation for motion-sensitive players is not "turn it off" — it's the
non-adjustable wind-up floor (guarantees a slow, readable build regardless of difficulty) plus the
audio pre-cue (an alternate channel for players who process fast visual change poorly), both already
covered above. A player who needs *both* a slower visual read *and* has no benefit from the audio
channel (e.g. profoundly Deaf and highly motion-sensitive) is not fully served by any combination of
settings this document can define without also slowing combat's core pacing — that residual gap is
acknowledged honestly rather than papered over; the wind-up floor is this game's best available
mitigation for that player today, and a future game-speed or practice-mode feature
(`combat-encounter-system`'s eventual design, Assumption A7) is the more complete fix, not a setting
this GDD can conjure on its own.

### 3.5 Auditory Accessibility Settings

| Setting | Type | Range | Default | What It Affects |
|---|---|---|---|---|
| `volume_master` | int (%) | 0–100 | 100 | Global output level, applied on top of all other volume sliders. |
| `volume_music` | int (%) | 0–100 | 100 | Region ambient themes (`game-concept.md`'s Technical Considerations: "ambient region themes"). |
| `volume_sfx` | int (%) | 0–100 | 100 | Combat, part-break, telegraph audio cues, UI feedback sounds. |
| `volume_dialogue` | int (%) | 0–100 | 100 | Reserved for a future dialogue/narrative system (`game-concept.md`: narrative explicitly N/A for MVP). Functions as a no-op fader until that content exists — shipped now so no save-format or settings-schema change is needed when it does. |
| `volume_ui` | int (%) | 0–100 | 100 | Menu/HUD interaction sounds, separate from in-world SFX. |
| `audio_loudness_normalization_enabled` | bool | off / on | off | Applies a limiter/compressor to cap peak transient audio (boss-fall stingers, ultimate activation) so no single sound spikes dramatically above the current mix — "disable sudden loud sounds" per the standing guideline. Off by default because it alters the audio director's intended mix; opt-in. |
| `mono_audio_enabled` | bool | off / on | off | Sums stereo output to mono for single-speaker or hearing-aid users. |

**Subtitles/closed captions — scoped honestly**: `game-concept.md` states narrative is explicitly
N/A for MVP and no dialogue system exists. A traditional dialogue-subtitle system with speaker
identification is therefore **not applicable in MVP scope** — there is no dialogue to caption. This
is flagged as a gap to revisit if/when a narrative or dialogue system is authored (`game-concept.md`'s
own flagged Relatedness gap), not a current oversight.

**The functional-audio-cue audit this task asked for, done explicitly**: `game-concept.md`'s
Technical Considerations calls telegraph/weak-point audio cues "functionally important, not just
decorative." Every functional audio cue currently in the locked design has a guaranteed visual
equivalent already:

- **Telegraph audio pre-cue** (§3.4) — visual equivalent is the telegraph wind-up pulse itself,
  which is always present regardless of any audio setting (art-bible §2, §3.5's hero-shape
  hierarchy). A Deaf player loses nothing mechanically important by having audio disabled entirely.
- **Part-break audio payoff** (`game-concept.md`: "hit-stop and audio payoff on part-breaks") —
  visual equivalent is the crack/glyph escalation rendered directly on the creature's own part-seam
  (art-bible §7.5: "no HUD element at all... reuses 3.3's crack grammar running in reverse"), which
  is diegetic and always visible.

**Binding requirement this system imposes going forward** (§3.7): no future gameplay-critical audio
cue may ship without a redundant visual or haptic equivalent. This extends the standard
dialogue-captioning requirement to functional sound effects generally — the correct scope for a game
whose audio needs are explicitly "functional, not just decorative."

### 3.6 Cognitive Accessibility Settings

| Setting | Type | Range | Default | What It Affects |
|---|---|---|---|---|
| `simplified_ui_density` | bool | off / on | off | Collapses secondary/advanced information on the game's dense systemic screens — Loot Filters, Automation Config, Creature Roster (art-bible §3.4/§7.6's named high-density utility screens) — down to primary decision-relevant columns only, with advanced detail expandable on demand rather than always-visible. |

**Why this is the right MVP-scope cognitive-accessibility lever, and the only one added**:
art-bible §3.4 and §7.6 already establish these three screens as maximum-density, zero-ornament
utility interfaces by design — their complexity is entirely informational, not decorative, which
means density reduction is the *only* lever available to reduce cognitive load on them (there is no
frame ornament to strip, per those sections' own design test). Given `game-concept.md` explicitly
compares this game's itemization/automation depth to Path of Exile — a genre benchmark for
cognitively dense systemic UI — this setting is a justified, real need, not gold-plating. It is
scoped as a single boolean rather than a granular per-column configuration system to keep MVP
implementation cost proportionate; the exact "primary vs. advanced" column split for each screen is
delegated to that screen's own future GDD (`loot-filter-ui`, `automation-config-ui`,
`creature-roster-ui`), which must reference this document back and honor the flag.

**Combat HUD density**: not addressed by a setting, because there is very little to reduce.
Art-bible §7.5 already computed the combat HUD's "net footprint" as a small corner cluster plus the
belt-charm zone the Hunter already carries — deliberately minimized before this document exists. No
additional combat-HUD density control is proposed.

**Consistent UI layout/navigation** and **objective/quest reminders**: the first is enforced as a
binding requirement on all UI systems (§3.7), not a toggle. The second is **not applicable in MVP
scope** — no quest/objective-log system exists in the currently locked design (`game-concept.md`
describes region/expedition/boss progression, not a discrete quest log); flagged for reconsideration
if one is added later.

**Tutorial replay**: owned by `onboarding-tutorial-system` (`systems-index.md` #24).
Coordination note only: if that system implements a replay entry point, this settings menu should
surface access to it. Not specified further here since the tutorial system itself doesn't exist yet.

**Pause available at all times**: stated as a binding requirement on `combat-encounter-system` (§3.7)
and an acceptance criterion (§8), not a toggle — there is no reason, in a single-player game with no
online/competitive constraint, for pause to ever be unavailable.

**Low-stakes practice mode**: per Assumption A7, recommended but explicitly out of this document's
scope. `game-concept.md`'s own framing already reduces failure cost structurally ("a botched capture
attempt simply becomes a kill instead of a hard fail state," "encounters are short... failure costs
little time"), which softens the need somewhat — but a genuine consequence-free practice space for
learning telegraph patterns remains a strong recommendation for whoever designs
`combat-encounter-system` next (see §6).

**Difficulty options affecting cognitive load (fewer enemies, longer timers)**: per Assumption A8, no
discrete difficulty system is proposed here. This system's aim-assist slider, cycle-timing sliders,
and the non-adjustable telegraph wind-up floor already provide a continuous accessibility-relevant
assist range without needing a separate "Easy/Normal/Hard" selector. A difficulty system for
non-accessibility reasons (challenge tiers, score modifiers) is a distinct game-design decision
outside this document's mandate.

### 3.7 Binding Requirements Enforced on Other Systems

These are not settings — they are constraints this system's existence and design imposes on every
other system, restated here as a single enforceable checklist (each cites the WCAG criterion it
operationalizes where one directly applies):

1. **Visible keyboard-focus indicator is mandatory** (SC 2.4.7 Focus Visible) on every interactive
   element in every UI system, built from value-steps and border weight within the three-color
   chrome budget — never a fourth hue. Enforced on: `combat-hud`, `forge-ui`, `creature-roster-ui`,
   `automation-config-ui`, `loot-filter-ui`, `region-view-world-map-ui`.
2. **Every drag-to-reorder or drag-to-assign pattern requires a non-drag keyboard equivalent**
   (loot-filter priority, creature-to-slot assignment) — a numeric priority field or explicit
   move-up/move-down/assign-via-dropdown controls. Drag-and-drop alone fails keyboard-only play.
   Enforced on: `loot-filter-ui`, `creature-roster-ui`, `automation-config-ui`.
3. **Every interactive element must be reachable by keyboard or gamepad navigation alone** (SC 2.1.1
   Keyboard), with no functionality gated behind mouse-hover-only interaction. Enforced universally.
4. **Color is never the sole differentiator for any UI state** (SC 1.4.1 Use of Color) — a
   settings-menu regression check specifically for the UI layer, since art-bible §4.6 already
   guarantees this for world/glyph content; this system exists partly to catch a chrome-layer
   backslide (e.g. a checkbox that is only "checked" via a color swap).
5. **All Data-face text must remain legible with no clipping or overlap up to `ui_scale_percent =
   200`** (SC 1.4.4 Resize Text). Enforced on every UI system; each screen's own future design must
   layout-test at maximum scale.
6. **The telegraph wind-up floor is never exposed as a downward-adjustable setting**, in this system
   or any other. `creature-ai-telegraph-system` owns the value; this system enforces its absence from
   the settings surface entirely.
7. **Hitbox padding minimums are never exposed as a player-facing setting**, in this system or any
   other. `input-targeting-system.md` owns the constants; they remain always-on and designer-tuned
   only.
8. **No ability or interaction requires simultaneous multi-button input without a toggle
   alternative.** Enforced on `combat-encounter-system` when it is designed.
9. **No timed quick-input event ships without a skip or auto-complete option.** Preemptive
   requirement on `combat-encounter-system`.
10. **No gameplay-critical audio cue ships without a redundant visual or haptic equivalent.**
    Enforced on `audio-system` and `creature-ai-telegraph-system`.
11. **Pause must be available at all times during single-player play**, with all timers (including
    telegraph windows) fully frozen, not merely visually paused. Enforced on
    `combat-encounter-system`.
12. **Any future camera-shake or screen-shake effect must gate on `reduced_motion_enabled`.**
    Preemptive requirement on any system that adds one.

### 3.8 The Settings Menu's Own Accessibility

The screen that configures accessibility cannot itself be an accessibility failure. This system
binds the future `/ux-design` pass for the Settings screen to the same standards it enforces
elsewhere: full keyboard/gamepad navigability with a visible focus indicator (§3.7 item 1), text at
or above this document's own minimum size floor before any `ui_scale_percent` multiplier is applied
(Body/Data workhorse tier ≥18px logical size at 1080p/100% scale, matching the standard baseline;
Micro/Caption tier is exempt from the hard floor only where a glyph carries primary identification
and the text is confirmatory, per art-bible §7.1's own rule), and every control's state (toggle
on/off, slider value, active binding) legible with `colorblind_preview_mode = grayscale` enabled —
this screen must pass its own audit before it ships.

---

## 4. Formulas

### Formula 1 — UI Scale, Continuous (Data-Face Text, Chrome Icons, Borders, Glyph-Grammar UI Content)

```
ResScale = H_out / H_ref
S = ResScale × (ui_scale_percent / 100)
RenderedSize_px(el) = ReferenceSize_px(el) × S
FinalRenderedSize_px(el) = max( RenderedSize_px(el), HUD_GLYPH_MIN_PX_AT_1080P )   [glyph-grammar containers and chrome icons only]
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `H_out` | int | > 0, px | Output display/window height in physical pixels. |
| `H_ref` | int | fixed, 1080 | Reference resolution height, matching this system's own text-size floor and art-bible's asset-authoring baseline. |
| `ResScale` | float | > 0 | Output-resolution scale factor relative to the 1080p reference. |
| `ui_scale_percent` | int | [100, 200] | Player-facing setting (§3.2). |
| `S` | float | ≥ 1.0 at 100% scale on ≥1080p displays | Combined scale factor. |
| `ReferenceSize_px(el)` | float | > 0 | The element's authored size at 1080p/100% scale (e.g. 16px chrome icon, 18px body text, per art-bible §8.2 and this document's own text floor). |
| `RenderedSize_px(el)` | float | > 0 | Continuous, unclamped rendered size. |
| `HUD_GLYPH_MIN_PX_AT_1080P` | constant | fixed, 16 | Non-tunable floor (Assumption A4). Applies only to glyph-grammar containers and chrome icons, not to arbitrary chrome decoration. |
| `FinalRenderedSize_px(el)` | float | ≥ `HUD_GLYPH_MIN_PX_AT_1080P` for floor-bound elements | Output actually drawn. |

**This is deliberately continuous, not integer-snapped** — the Data face and chrome icons use a
scalable (non-bitmap) rendering path specifically so they can do this (art-bible §7.1's hybrid
typography decision: "a scalable data face is accepted as the one sanctioned break from the
all-pixel rendering rule... because dense tabular legibility and text-scaling accessibility outrank
visual purity"). This formula is that decision's payoff made concrete.

**Worked example 1** (sanity check, 1080p / 100% scale): `H_out = 1080`, `ui_scale_percent = 100` →
`ResScale = 1.0`, `S = 1.0`. A 16px chrome icon: `RenderedSize_px = 16 × 1.0 = 16px`, matching its
native art-bible §8.2 authored size exactly.

**Worked example 2** (the floor's actual purpose — a sub-1080p window): `H_out = 720`,
`ui_scale_percent = 100` → `ResScale = 720/1080 = 0.6667`, `S = 0.6667`. A 16px glyph-micro icon:
`RenderedSize_px = 16 × 0.6667 = 10.67px` — below the 16px floor. `FinalRenderedSize_px = max(10.67,
16) = 16px`. The floor exists precisely for this case: since `ui_scale_percent` can never go below
100 (§7), the only way a floor-bound element could shrink below 16px is a window smaller than the
1080p reference, and the clamp guarantees it never does, at the accepted cost of that element no
longer being strictly proportional to the rest of the UI at that window size.

**Worked example 3** (high-resolution, scaled up): `H_out = 2160` (4K), `ui_scale_percent = 150` →
`ResScale = 2.0`, `S = 3.0`. A 16px chrome icon: `RenderedSize_px = 16 × 3.0 = 48px`.

### Formula 2 — UI Scale, Integer-Snapped (Display-Face Bitmap Font Only)

```
DisplayMultiplier = max(1, round(S))
RenderedSize_px(display_el) = ReferenceSize_px(display_el) × DisplayMultiplier
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `S` | float | from Formula 1 | Same combined scale factor. |
| `DisplayMultiplier` | int | ≥ 1 | Nearest integer multiple, floor-clamped at 1. |
| `RenderedSize_px(display_el)` | int | ≥ `ReferenceSize_px(display_el)` | Output size for pixel bitmap display-face text only (screen titles, ability/ultimate names, rarity tier names — art-bible §7.1). |

**Why this formula exists separately from Formula 1**: the pixel bitmap display face cannot use
continuous scaling — `technical-preferences.md`'s Forbidden Patterns explicitly bans
non-integer/bilinear scaling on pixel art (the same rule that governs rotated creature-part
sprites, art-bible §8.5), because it reintroduces blur or stair-stepping. Snapping to the nearest
integer multiple preserves the pixel-perfect look this game's rendering pipeline otherwise requires
everywhere.

**Worked example**: `S = 3.0` (from Formula 1's Worked Example 3). `DisplayMultiplier =
max(1, round(3.0)) = 3`. A 32px ceremony title: `RenderedSize_px = 32 × 3 = 96px`, a clean 3×
integer multiple with no blur.

### Formula 3 — Aim-Assist Display Mapping

```
DisplayPercent = magnetism_strength × 100
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `magnetism_strength` | float | [0.0, 1.0] | The exact persisted/consumed value — `input-targeting-system.md`'s Formula 2 field, unmodified. |
| `DisplayPercent` | int | [0, 100] | Presentation-only value shown on the Settings menu's Aim Assist slider. |

This is the only math this document defines for aim-assist. The actual pull behavior —
`effective_click_pos = raw_cursor_pos + (nearest_target_anchor_pos − raw_cursor_pos) ×
magnetism_strength`, applied only within `magnetism_radius_px` — is `input-targeting-system.md`
Formula 2's exclusively, referenced here by name and not redefined, per this document's explicit
instruction to avoid re-litigating a decision already locked elsewhere.

### Formula 4 — WCAG Contrast Ratio (Relative Luminance)

```
lin(c) = c/12.92                         if c ≤ 0.03928
       = ((c + 0.055) / 1.055) ^ 2.4     otherwise          (c = channel value / 255)

L = 0.2126 × lin(R) + 0.7152 × lin(G) + 0.0722 × lin(B)

ContrastRatio = (L_lighter + 0.05) / (L_darker + 0.05)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `c` | float | [0, 1] | A single sRGB channel (R, G, or B), normalized from its 0–255 value. |
| `lin(c)` | float | [0, 1] | Linearized channel value (undoes the sRGB gamma curve). |
| `L` | float | [0, 1] | Relative luminance of a color. |
| `L_lighter`, `L_darker` | float | [0, 1] | The higher/lower of two colors' luminance values. |
| `ContrastRatio` | float | [1, 21] | WCAG contrast ratio between two colors. AA text minimum: 4.5:1 (SC 1.4.3). AA non-text UI component minimum: 3:1 (SC 1.4.11, exempting inactive/disabled components). |

**Worked example 1 — baseline chrome text pairing** (Bone Parchment `#E8DFC8` on Void Ink
`#1B1620`, art-bible §4.5's default body text): `ContrastRatio ≈ 13.38:1` — verified computationally
this session. This is not merely "comfortably clears 4.5:1" as art-bible §4.5 states; it clears the
AAA threshold (7:1) by nearly a factor of 2. **This is the finding behind Assumption A5**: there is
no body-text contrast problem for a high-contrast mode to solve.

**Worked example 2 — the disabled-state check** (Cold Slate `#57616F` on Void Ink `#1B1620`,
art-bible §4.5's disabled/inactive chrome pairing): `ContrastRatio ≈ 2.83:1` — below the 3:1 general
UI-component minimum. This is **not a violation**: WCAG SC 1.4.11 explicitly exempts inactive/disabled
components from its contrast requirement, and art-bible §4.5 restricts Cold Slate to exactly that
use ("disabled/inactive states"). Verified safe by exemption, not by oversight — stated explicitly
here so the exemption is a documented, deliberate finding rather than an unexamined gap.

**The genuine gap this leaves**: any *active* interaction state (hover, keyboard-focus, selected,
drag-preview) is not exempt from SC 1.4.11 and must independently clear 3:1 against its neighboring
state — but no GDD (including this one) has yet authored the exact hex/value-step differentiation
`ui-programmer` will use for those states, since the UI systems that need them are all still
"Not Started" per `systems-index.md`. This document cannot compute a ratio for values that don't
exist yet; instead it states the binding requirement (§3.7 item 1, §8 Acceptance Criterion) that
whatever values are chosen must pass this exact formula, and defines `high_contrast_mode`'s job as
widening the delta between them when enabled — proposed concrete defaults: baseline keyboard-focus
border weight 2px (logical, scales per Formula 1) at 100% scale, high-contrast 3px, both extending
art-bible §3.1's "1–2px leading" seam-weight convention up rather than reusing it verbatim, since a
focus indicator needs to be *more* visible than a passive seam line, not equally subtle.

### Non-Formulaic Settings (stated explicitly rather than padded)

The five volume sliders (`volume_master/music/sfx/dialogue/ui`), `mono_audio_enabled`,
`audio_loudness_normalization_enabled`, `controller_vibration_enabled`, `reduced_motion_enabled`,
`telegraph_audio_precue_enabled`, and `simplified_ui_density` are direct boolean/linear pass-through
values with no derived math — each is consumed as-is by its owning system (`audio-system` for the
volume/audio group, `creature-ai-telegraph-system` for the pre-cue and motion suppression,
respective UI systems for density). `keybind_map` is a lookup table, not a formula. `cycle_initial_
delay_ms` and `cycle_repeat_interval_ms` are consumed directly by `input-targeting-system.md`'s
already-defined auto-repeat mechanism (§3.4 of that document) with no additional transformation here.

---

## 5. Edge Cases

1. **`ui_scale_percent` is set high enough that HUD elements would overlap.** Per §3.7 item 5, every
   UI system's layout must be tested and must not overlap or clip at `ui_scale_percent = 200` — this
   is a binding requirement on layout, not a runtime edge case this system resolves dynamically. For
   the combat HUD's screen-space corner cluster specifically, the binding requirement is stronger:
   it must reserve a screen margin proportional to `ui_scale_percent` (not a fixed pixel margin), so
   scaling it up can never encroach into the Arena's central creature/vulnerability-glyph viewing
   area, even at maximum scale. For dense list-based screens (Loot Filters, Automation Config,
   Roster), the resolution is reflow, not overlap: content may become vertically scrollable, and a
   tabular data grid specifically may become horizontally scrollable (an accepted exception, matching
   the general reflow principle behind SC 1.4.10, which explicitly permits 2D scrolling for data
   tables) — but text must never truncate silently or render on top of other controls.
2. **`reduced_motion_enabled` is on, and the telegraph pulse — the mechanic that requires motion to
   read — cannot be disabled.** Resolved in full in §3.4: this is the deliberate, justified boundary
   of the setting, not a gap. The telegraph pulse is never suppressed; only ambient/decorative motion
   is. The wind-up floor and audio pre-cue are the mitigations available to motion-sensitive players
   for the pulse itself.
3. **`high_contrast_mode` conflicts with art-bible §4.5's three-color chrome budget.** It does not,
   by design: the setting only widens value-step deltas and border weights *within* the existing
   Void Ink / Bone Parchment / Cold Slate role system (Assumption A5) — it never introduces a fourth
   hue, never touches glyph-content colors, and never touches body text (already verified at
   ≈13.38:1, Formula 4). No conflict exists because the setting was scoped from the start to operate
   entirely inside the locked budget.
4. **`magnetism_strength` is set to maximum (1.0) — does this trivialize Pillar 1?** No, and this is
   argued explicitly rather than asserted: magnetism only pulls the cursor toward the *nearest* valid
   target's anchor, and only when the cursor is already within `magnetism_radius_px` (default 25,
   virtual-canvas px — small relative to a 96–160px standard creature) of some target
   (`input-targeting-system.md` Formula 2). It never selects which part is correct, never reveals
   vulnerability state, and never moves the cursor across the creature toward a distant target. The
   skill Pillar 1 tests — recognizing *which* part is exposed — remains fully required at any
   magnetism strength, because the setting only ever assists *acquisition* of a target the player has
   already navigated toward. A genuine, honestly-stated risk runs the *other* direction: at maximum
   strength, two valid targets both within radius of each other could cause a snap to the nearer
   (wrong) one even when the player aimed correctly for the intended part — magnetism can occasionally
   work against precision, never substitute for the reading skill itself. This mirrors exactly the
   reasoning `input-targeting-system.md`'s Player Fantasy already used to justify mandatory hitbox
   padding as non-Pillar-1-violating; magnetism is simply a stronger, optional, fully toggle-off-able
   version of the same category of assist. There is also no competitive-fairness concern to weigh
   against this, since the game is single-player with no leaderboards (`game-concept.md`).
5. **A conflicting key binding is entered** (e.g. the player tries to bind `confirm` to the same key
   already bound to `cycle_next`). The remapping UI rejects the new binding with a visible conflict
   warning and requires the player to either choose a different key or explicitly reassign the
   conflicting action first — never silently overwrites, and never allows two active (non-mouse-hover)
   actions on an identical key/button simultaneously.
6. **The persisted `accessibility_settings.json` is corrupted or unreadable.** Unlike a corrupted game
   save, no `.bak` recovery is required — the file falls back to the full default set (§7) without
   crashing the application, and a fresh write on the next settings change repairs it. This asymmetry
   with `save-load-persistence.md`'s more elaborate corrupted-save handling is intentional: losing a
   settings file is low-stakes (defaults are always a safe, playable fallback), while losing a game
   save is not.
7. **A future patch narrows a tuning knob's safe range** (e.g. `cycle_repeat_interval_ms`'s minimum
   is raised from 60 to 80 for balance reasons) and a player's persisted value now falls outside it.
   On load, any out-of-range persisted value is clamped to the nearest bound of the new safe range,
   never rejected outright and never left silently invalid — consistent with
   `save-load-persistence.md`'s broader philosophy of graceful, additive schema evolution rather than
   hard failure on minor drift.
8. **A player is both profoundly Deaf and highly motion-sensitive.** Acknowledged honestly in §3.4
   as a residual gap no combination of this document's settings fully closes — the wind-up floor
   (universal, non-adjustable, benefits this player too) is the strongest mitigation currently
   available; a future game-speed or practice-mode feature (Assumption A7, `combat-encounter-system`'s
   eventual scope) is the more complete fix, not something this GDD can invent in isolation.
9. **`accessibility_settings.json` does not exist yet** (true first launch, or a player who deleted
   it manually). Identical to "no save file" in `save-load-persistence.md`'s Load Behavior — not an
   error; the runtime state initializes to §7's documented defaults.
10. **`colorblind_preview_mode` is active at the same time as `high_contrast_mode` and
    `reduced_motion_enabled`.** All three are independent, orthogonal settings applied in the same
    composited-frame post-process pass with no ordering conflict — `colorblind_preview_mode` operates
    on final pixel color, `high_contrast_mode` operates on chrome value-steps/borders pre-composite,
    and `reduced_motion_enabled` gates which animations play at all. None of the three can produce an
    invalid or contradictory combined state.
11. **A player sets `ui_scale_percent` to 200% on a screen with `simplified_ui_density = off`,
    creating maximum on-screen density and maximum element size simultaneously** on a dense screen
    like Loot Filters. Resolved by Edge Case #1's reflow requirement — this is exactly the scenario
    that requirement exists for; `simplified_ui_density` remains available and recommended as the
    complementary setting for a player who hits this combination, but is never auto-forced on by a
    high `ui_scale_percent` value (settings remain independently player-controlled, never silently
    linked).
12. **The window is resized below the 1080p reference height mid-session** (e.g. the player drags a
    windowed-mode game window smaller while playing). Formula 1's `ResScale` recomputes live on the
    next frame; the `HUD_GLYPH_MIN_PX_AT_1080P` floor clamp applies continuously, so glyph-grammar
    containers and chrome icons never drop below 16px even during an active resize, per Formula 1's
    Worked Example 2.
13. **`telegraph_audio_precue_enabled` is turned off by a player who is also relying on it as their
    primary channel** (e.g. a low-vision player who finds the audio easier to react to than the
    visual pulse, and disables it by mistake or misunderstanding). No special handling is proposed
    beyond the setting being clearly labeled and easily re-enabled — the visual telegraph pulse
    remains the guaranteed always-on primary channel regardless (§3.4), so no player is ever left
    with zero signal even if this toggle is misconfigured.

---

## 6. Dependencies

### Depends On

- **`input-targeting-system`** (`design/gdd/input-targeting-system.md`) — reads and exposes, by
  exact field name: `magnetism_strength` (Formula 2 of that document; player-facing here, §3.3),
  `cycle_initial_delay_ms`, `cycle_repeat_interval_ms` (§7 of that document; player-facing here,
  §3.3), the `cycle_next` / `cycle_prev` / `confirm` action identifiers (§3.4 of that document,
  which explicitly delegates "exact key/button bindings are `accessibility-settings-system`'s
  concern" — this document is that delegation's fulfillment). Also references, by exact field name,
  the constants this system explicitly does **not** expose: `padding_base_px`,
  `padding_inverse_scalar`, `reference_part_size_px`, `padding_min_px`, `padding_max_px`,
  `magnetism_radius_px` (§3.3).
- **`save-load-persistence`** (`design/gdd/save-load-persistence.md`) — does not store data inside
  that system's `save.json`, per Assumption A1, but directly reuses its atomic
  temp-file-then-atomic-replace write pattern and additive-vs-breaking schema-versioning philosophy
  for this system's own independent `accessibility_settings.json` file (§3.1). **Flagged gap, not
  self-edited**: `save-load-persistence.md`'s own document does not currently mention this system or
  this file at all (it only lists `hunter_build`, `creature_roster`, `forge_inventory`,
  `region_states` as its four MVP-reserved sections, and this document deliberately does not add a
  fifth). Per this session's file discipline, `save-load-persistence.md` is not edited here; this
  dependency and its resolution (separate file, not a fifth section) are documented fully in this
  GDD instead, for a future centralized pass to reconcile.
- **`game-concept.md`** — Pillar 1 (Precision Over Reflexes) frames this entire document's Player
  Fantasy (§2); the single-player, no-profile scope (§2.1) informs Assumption A2; the MVP Definition
  and Session-Level loop's "clean chapter close" language informed Assumption A1's reasoning for why
  settings should NOT share `save-load-persistence.md`'s trigger-gated write cadence.
- **`design/art/art-bible.md`** §4.5 (chrome palette, contrast baseline), §4.6 (colorblind-safety
  architecture and audit table), §5.6 (world-space glyph floor — explicitly does not transfer),
  §7.1 (hybrid typography, enabling text scaling), §7.3 (HUD/UI motion budget, informing the
  reduced-motion suppression list), §7.5 (combat HUD footprint), §7.6 (ornamentation tier by
  screen), §7.7 (the binding accessibility requirements this document operationalizes into
  settings) — the primary source of truth this document implements against.

### Depended On By

- **`combat-hud`, `forge-ui`, `creature-roster-ui`, `automation-config-ui`, `loot-filter-ui`,
  `region-view-world-map-ui`** — each must implement the keyboard-focus
  indicator, non-drag keyboard equivalents, `ui_scale_percent` responsiveness (Formula 1/2),
  `high_contrast_mode`'s value-step widening, `colorblind_preview_mode`'s compatibility, and
  `simplified_ui_density`'s column-collapse behavior this document defines as binding requirements
  (§3.7). Each of those systems' own GDD, when authored, must reference this document back.
- **`creature-ai-telegraph-system`** — must read `telegraph_audio_precue_enabled`
  to decide whether to play its redundant audio cue, and `reduced_motion_enabled` to decide whether
  to suppress its own ambient/decorative motion layers, while treating its own wind-up floor constant
  as authoritative and immune to any request from this system to lower it (§3.4, §3.7 item 6). That
  GDD must reference this document back and confirm the exact contract assumed here.
- **`audio-system`** — implements the five volume sliders, loudness normalization,
  and mono-audio downmix this document specifies as the settings surface (§3.5), and is bound by
  §3.7 item 10's redundant-visual-equivalent requirement for any future audio cue it adds. Must
  reference this document back.
- **`combat-encounter-system`** — must register its own bindable actions (3
  abilities, ultimate, dodge/block) into this system's `keybind_map` framework (§3.3) rather than
  inventing a second remapping mechanism; must honor the no-simultaneous-multi-button, no-QTE-without-
  skip, and always-available-pause requirements (§3.7 items 8, 9, 11); and is the natural future owner
  of the practice-mode and game-speed features this document explicitly recommends but does not
  implement (Assumption A7). Must reference this document back.
- **`onboarding-tutorial-system`** — coordination note only (§3.6): if it implements
  a tutorial-replay entry point, this settings menu should surface access to it.

### `systems-index.md` Gaps (flagged, not self-edited)

Per this session's file discipline, `design/gdd/systems-index.md` is not edited by this document.
Two gaps are flagged here for a future centralized update, following the precedent
`input-targeting-system.md` set for its own `animation-rig-system` correction:

1. `systems-index.md`'s Dependency Map currently lists `accessibility-settings-system` as depending
   only on `input-targeting-system`, with no recorded "Depended On By" edges at all. This document
   establishes six new UI-system edges plus edges to `creature-ai-telegraph-system`, `audio-system`,
   and `combat-encounter-system`, all listed above.
2. No `settings-menu-ui` system is enumerated anywhere in `systems-index.md`'s Systems Enumeration.
   This document owns the settings *data model*; the settings *screen* itself needs a UI-system owner
   the same way `forge-ui` owns the Forge's screen. This is an observation for the next systems-index
   revision, not a blocking gap for this document.

---

## 7. Tuning Knobs

| Knob | Field | Type | Safe Range | Default | Player-Tunable? | Notes |
|---|---|---|---|---|---|---|
| UI scale | `ui_scale_percent` | int % | 100–200 | 100 | **Yes** | SC 1.4.4. See Formula 1/2. |
| HUD-space glyph floor | `HUD_GLYPH_MIN_PX_AT_1080P` | constant px | fixed | 16 | **No — hard floor** | Matches art-bible §8.2's glyph-micro/chrome-icon native asset size. Independent of the world-space 12–16px floor (art-bible §5.6), which does not transfer. |
| High contrast mode | `high_contrast_mode` | bool | off/on | off | **Yes** | Scoped to non-text chrome interaction-state contrast only (Assumption A5). |
| Focus indicator border weight | (derived, not independently exposed) | px | 2 baseline / 3 under high-contrast | 2 | Indirectly, via `high_contrast_mode` | Proposed default for `ui-programmer`; extends art-bible §3.1's seam-weight convention upward. |
| Colorblind preview | `colorblind_preview_mode` | enum | off/protanopia/deuteranopia/tritanopia/grayscale | off | **Yes** | Self-verification tool, not a corrective filter (Assumption A3). |
| Aim-assist strength | `magnetism_strength` | float | 0.0–1.0 | 0.0 (off) | **Yes** | Exact field, `input-targeting-system.md` Formula 2. Displayed as 0–100% (Formula 3). |
| Magnetism radius | `magnetism_radius_px` | float px | 10–50 | 25 | **No — designer-tuned only** | `input-targeting-system.md` §7; not exposed here at all. |
| Hitbox padding constants | `padding_base_px`, `padding_inverse_scalar`, `reference_part_size_px`, `padding_min_px`, `padding_max_px` | — | — | — | **No — mandatory, always-on** | `input-targeting-system.md` §3.6/§4 Formula 1; never exposed as a player setting under any circumstance. |
| Cycle auto-repeat delay | `cycle_initial_delay_ms` | int ms | 200–600 | 350 | **Yes** | Exact field, `input-targeting-system.md` §7. |
| Cycle auto-repeat interval | `cycle_repeat_interval_ms` | int ms | 60–250 | 120 | **Yes** | Exact field, `input-targeting-system.md` §7. |
| Controller vibration | `controller_vibration_enabled` | bool | off/on | on | **Yes** | Comfort/sensory setting. |
| Telegraph wind-up floor | (owned by `creature-ai-telegraph-system`) | — | — | — | **No — hard floor, not represented as a setting here at all** | This system provides no control of any kind, direct or indirect, that could lower it. SC 2.3.1; SC 2.2.1's "essential timing" exception justifies non-adjustability. |
| Telegraph audio pre-cue | `telegraph_audio_precue_enabled` | bool | off/on | **on** | **Yes** | Defaults on — pure assist, not a balance change. |
| Reduced motion | `reduced_motion_enabled` | bool | off/on | off | **Yes** | Suppresses ambient/decorative motion only; see §3.4's exact suppression list. |
| Volume — master/music/sfx/dialogue/ui | `volume_master`, `volume_music`, `volume_sfx`, `volume_dialogue`, `volume_ui` | int % (each) | 0–100 | 100 (each) | **Yes** | Five independent sliders. |
| Loudness normalization | `audio_loudness_normalization_enabled` | bool | off/on | off | **Yes** | Opt-in; alters the authored mix. |
| Mono audio | `mono_audio_enabled` | bool | off/on | off | **Yes** | |
| Simplified UI density | `simplified_ui_density` | bool | off/on | off | **Yes** | Loot Filters, Automation Config, Creature Roster only. |
| Key/button bindings | `keybind_map` | dict | any valid per-platform binding | engine defaults | **Yes** | Full remap framework; MVP action set: `cycle_next`, `cycle_prev`, `confirm`. Forward-reserved for `combat-encounter-system`'s future actions. |
| Settings-file write debounce | (implementation constant, not player-facing) | ms | fixed | ~500 | **No** | §3.1; internal write-batching only. |
| `settings_schema_version` | (current build value) | int | increments by 1, no upper bound | 1 (initial) | **No** | Gates this file's own load compatibility, independent of `save.json`'s versioning. |

---

## 8. Acceptance Criteria

1. **Full-keyboard completability**: for a fixture play session covering targeting (cycle-and-confirm,
   per `input-targeting-system.md`'s own Acceptance Criterion #1), menu navigation across every UI
   system, and settings adjustment, the entire game is completable start-to-finish with zero mouse or
   pointer device connected or used — verified by a scripted or manual walkthrough exercising every
   interactive element via keyboard alone and confirming no element is unreachable.
2. **Full-gamepad completability**: identical to Criterion 1, substituting a single connected gamepad
   (no keyboard, no mouse) as the sole input device — every interactive element, including settings
   menu controls, must be reachable and operable.
3. **No flashing above 3Hz at any difficulty setting**: for the full range of any future difficulty or
   telegraph-speed scaling `creature-ai-telegraph-system` implements, the wind-up floor this system
   enforces the non-adjustability of guarantees no region ever crosses the WCAG SC 2.3.1
   three-flashes-per-second threshold — verified by `creature-ai-telegraph-system`'s own test suite
   confirming the floor value itself never permits a sub-threshold cycle time, and by this system's
   test suite confirming no settings-surface control exists anywhere that could override that floor
   downward (a static-analysis/code-review check, mirroring `input-targeting-system.md`'s own
   analog-stick-ban verification method, §8 Criterion 3 of that document).
4. **Color-disabled legibility**: with `colorblind_preview_mode = grayscale` enabled, every
   mechanically important state across a fixture combat encounter and a fixture pass through every
   dense UI screen (Loot Filters, Automation Config, Creature Roster) remains fully distinguishable —
   verified against art-bible §3–4's own "cover it in gray" design tests, run as an explicit QA pass
   with this setting active rather than assumed compliant.
5. Given `ui_scale_percent = 100`, `H_out = 1080`, Formula 1 computes `FinalRenderedSize_px` for a
   16px chrome icon to exactly `16`, matching Worked Example 1 (§4).
6. Given `ui_scale_percent = 100`, `H_out = 720`, Formula 1 computes `FinalRenderedSize_px` for a
   16px glyph-micro icon to exactly `16` (clamped up from the unclamped `10.67`), matching Worked
   Example 2 (§4).
7. Given `ui_scale_percent = 150`, `H_out = 2160`, Formula 2 computes `DisplayMultiplier` to exactly
   `3` and a 32px ceremony title's rendered size to exactly `96`, matching the worked example (§4).
8. Formula 4 computes the contrast ratio of Bone Parchment (`#E8DFC8`) on Void Ink (`#1B1620`) to
   `13.38 ± 0.01`, and Cold Slate (`#57616F`) on Void Ink to `2.83 ± 0.01`, matching this document's
   verified values exactly (§4).
9. Setting `magnetism_strength = 1.0` and re-running `input-targeting-system.md`'s own Formula 2
   worked-example fixture produces the expected snap-to-anchor behavior for a cursor within
   `magnetism_radius_px`, with no change to which `part_id` is considered valid or vulnerable —
   confirming (per Edge Case #4) that maximum aim-assist affects only acquisition, never target
   selection or vulnerability state.
10. Enabling `reduced_motion_enabled` suppresses ambient particle drift, idle-view flicker, and
    menu/UI transition motion in a fixture scene, while the telegraph wind-up pulse, work-loop
    pulses, and any ceremony-tier one-shot transition (Vow-binding, Mastery Transition sweep) remain
    fully animated and unaffected — verified against the exact suppression list in §3.4.
11. Attempting to bind two of `cycle_next`/`cycle_prev`/`confirm` to the identical key/button is
    rejected by the remapping UI with a visible conflict warning, and no binding state change is
    persisted until the conflict is resolved.
12. Writing a settings change and immediately quitting the application (simulating save-on-exit)
    results in `accessibility_settings.json` reflecting the new value on next launch, with no
    dependency on any chapter-close event or the periodic autosave interval having fired.
13. Simulating a corrupted or missing `accessibility_settings.json` on launch results in the full
    default value set from §7 being loaded, with no crash and no partial/garbled settings state.
14. A rarity-tier tooltip's text rendering is verified to meet this document's own text-legibility
    floor (§3.8) and to never be the sole channel for rarity information — confirming the "remaining
    risk" flagged in §3.2 (rarity discrimination) is actually mitigated, not merely assumed to be.
15. A static-analysis/code-review check confirms no code path exposes `padding_base_px`,
    `padding_inverse_scalar`, `reference_part_size_px`, `padding_min_px`, `padding_max_px`, or
    `magnetism_radius_px` as a player-facing settings control anywhere in the settings menu's data
    model — confirming §3.7 items 6 and 7 hold in the implementation, not just in this document.
16. Pausing is confirmed available at every point during a fixture combat encounter (including mid-
    telegraph-window), and all telegraph/vulnerability-window timers are confirmed fully frozen
    (not merely visually paused) for the duration of the pause — verified once `combat-encounter-system`
    exists to test against; flagged here as the acceptance bar that system's own test suite must meet.
