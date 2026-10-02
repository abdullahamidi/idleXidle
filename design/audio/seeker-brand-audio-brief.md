# Audio brief: the Seeker's BRAND (the curse's cue family)

> **Status: APPROVED - Candidate A, SUBTLE / INTERNAL (the owner, 2026-10-02).** A is BRAND's canonical cue family and
> the only set in `assets/audio/combat/`; its seven files' bytes are pinned by SHA-256 (`brand_audio_test.cs`,
> `tools/asset-pipeline/foley/brand_curse/brand_audio_manifest.json`): a different file is a new approval, never a
> regeneration. B and C are REJECTED and archive-only (`tools/asset-pipeline/audio_history/brand_curse/B/`, `C/`).
> BRAND's visual direction and its production renderer (ADR-013) are APPROVED and LOCKED. Review page
> https://claude.ai/artifact/2r2SwCi6RujexjZzRvYhqg, evidence `production/qa/evidence/brand-audio/`. Provenance and
> licence (12 CC0 1.0 Freesound recordings, checked 2026-10-02): `tools/asset-pipeline/foley/brand_curse/SOURCES.md`.

## What BRAND is, to the ear

A supernatural curse **inside the victim**: it enters, takes hold, spreads deeper through the same body, and leaves or
propagates. It is NOT a projectile impact, a melee hit, a bite, a magical explosion, an ambient aura or a UI debuff
notification. No intelligible voice, no words.

- **Audible:** apply, deepen, SPRAWL propagation, transfer (leave + awaken), the curse's collapse on a death.
- **Silent:** the idle persistent state. No loop, no ambience, no idle accent.
- **Subordinate:** every BRAND cue is quieter than SPRAY, HARD HANDS, JAWS and PRESS's crush, and never `lead`.

## One family, seven files

Every cue is built by `tools/asset-pipeline/make_brand_cues.py` from ONE small source family of real CC0 foley
(`tools/asset-pipeline/foley/brand_curse/SOURCES.md`): the same spectral texture, the same low internal resonance and
the same dry corruption layer recur in every file; timing and intensity vary. Mono 16-bit 44.1 kHz, true peak <= 0.37.

| Key | Moment | Structure | Length | Take-hold transient at |
|---|---|---|---|---|
| `sfx_seeker_brand_apply` | the first infection (the first territory blooms) | reverse / suction lead-in -> dark internal take-hold (body resonance) -> short spectral / violet corruption tail | ~650-800 ms | **180 ms** |
| `sfx_seeker_brand_deepen` | depth 1 -> 2 (and any deepen to 2) | the existing corruption wakes -> a short low under-skin crawl / dry scrape / muffled hiss (the visual's 170 ms travel) -> a short take-hold at the new territory | ~600-750 ms | **230 ms** (wake at 0-80, travel 0-170, take-hold on the first new bloom at +170, its transient ~+230) |
| `sfx_seeker_brand_deepen_deep` | any deepen that reaches depth 3 | `deepen` plus one extra corruption layer, slightly more low-mid body, a slightly longer tail; never louder | ~700-850 ms | **230 ms** |
| `sfx_seeker_brand_infect` | SPRAWL: one victim takes the curse | a short, quieter relative of the apply's take-hold (no lead-in) | ~200-300 ms | **15 ms** |
| `sfx_seeker_brand_leave` | a cursed host falls (transfer or final collapse) | a brief curse flare -> inward collapse / suction -> soft ash / shadow exhale; secondary to the death sound | ~500-700 ms | **0-20 ms** (the flare on the fall) |
| `sfx_seeker_brand_awaken` | transfer: the curse awakens in the new host | a delayed internal inhale -> a short take-hold as the territories bloom | ~450-600 ms | **120 ms** |
| `sfx_seeker_brand_ash` | the Ash-Burn material accent, layered on take-holds and leaves | a dry crumble / brittle internal crack / dusty exhale, ~150-250 ms | ~200 ms | **0-10 ms** |

The game asks each cue so that its take-hold transient lands on the picture's decisive frame: `start = moment - (transient
offset)`, with the 60 Hz step taken into account as PRESS does.

## When each cue plays (presentation timeline, never gameplay)

All cue moments are computed **once at BeginWave** from the same deterministic mark schedule the curse is drawn from
(`MarkPerformance`), so sound and picture cannot disagree. Each cue fires **once** (an armed flag per cue: re-armed when
the playhead moves back before it, skipped if more than 30 ms late after a seek or hitch), exactly like PRESS's `CueDue`.

- **Apply:** the first tick (the first territory is born on the tick). Transient at tick + ~60 (the flare peaks 60-160).
- **Deepen:** each SHOWN stage step up `At` (the same step the curse blooms from). One cue per step, never one per new
  territory: a multi-depth jump is ONE `deepen_deep` cue. A SPRAWL row's ripple (one tick, ~60 ms per place, however
  long the row) is ONE deepen; two shown steps of one creature are always two cues. Skipped if the host falls before
  the first new territory is born (`At + 170`).
- **SPRAWL:** the source's own apply is the source activation; then ONE `infect` per victim at its landing (`land`),
  in the presentation's stagger, each a little quieter and a semitone-or-less lower than the one before, so the row
  reads as one phrase travelling away. Never four full applies. Unthrottled (they are 40 ms apart) but bounded to the
  victims of that hop chain.
- **Transfer:** `leave` on the old host's fall F (with the death); silence through the gap; `awaken` at the landing
  `lands` (Reform). No travel sound, no beam.
- **Cursed death without transfer:** `leave` alone (the same file).
- **Several cursed hosts falling on one frame** (a SPRAWL row kill): ONE `leave` for that moment.
- **Ash-Burn accent:** on apply / deepen / infect / awaken / leave, if the host's baked Ash-Burn weight `burn > 0`,
  `sfx_seeker_brand_ash` is added at the take-hold with volume `x burn`, and the main cue loses `0.25 x burn` of its
  volume. A generic, continuous rule of the baked host data; never a name.
- **Catch-up deepen during an arrival:** two distinct events (arrival then deepen) -> two cues, never two of the same.
- **Wave end:** a final `leave` holds the break open through `StillLeaving` (already true for the picture).

## Mix

- Volumes target (K-weighted loudest 50 ms, x SFX master 0.8): apply ~ -35 dB, deepen ~ -36, deepen_deep ~ -35.5,
  awaken ~ -36, infect ~ -39 each, leave ~ -37 (under `sfx_enemy_down`), ash accent ~ -41. References: PRESS tick
  -31.3, SPRAY -27.5, JAWS -26.0, HARD HANDS -23.5. (The integration's mix check, 2026-10-02, lowered the first
  targets by 1 dB for every cue alike so the loudest case, an apply on a host with no Ash-Burn, sits >= 3 dB under
  PRESS's tick; `MarkRecipe` volumes 0.183 / 0.163 / 0.173 / 0.163 / 0.116 / 0.145 / 0.092.)
- The Ash accent follows the cue it rides on: it starts on the take-hold, up to ~225 ms after its cue, so it is heard
  at the share (quiet / yield / duck / the bank's repeat throttle) its cue was asked at, never louder than the layer it
  accents. A cue that never played (skipped late after a seek or hitch, or dropped by the throttle) takes its accent
  with it: the crumble never plays alone. At any burn the accent sits >= 2 dB under its cue
  (`MarkRecipe.AshShareUnder`: x0.75 under an infect, x0.94 under a leave, whole under the rest).
- Never `lead`: the authored action's duck (0.45) applies to BRAND automatically.
- Like PRESS: a cue within [-700, +400] ms of a presented JAWS snap plays at x0.5; within [-200, +350] ms of an
  action Skill or while the champion performs, x0.6; when already ducked, the duck alone (never duck x quiet).
  When yielding, the Ash accent is omitted (the least important layer).

## The owner's decision (2026-10-02)

Three candidates were built on the same visuals, timing, keys and loudness targets, so the owner compared character,
not level:

- **A - SUBTLE / INTERNAL (APPROVED, canonical):** breath, suction, internal shadow movement, dry corruption; little
  overt magic.
- **B - SUPERNATURAL (rejected, archive-only):** spectral violet energy, reversed textures, corrupted resonance. Its
  bright supernatural material competes with the combat voices and can make a transfer read like spell casting.
- **C - ORGANIC / ASH (rejected, archive-only):** dry internal crackle, ash crumble, corrupted tissue, low body
  resonance. Its physical crackle overlaps PRESS / JAWS, and its deepen reads like another impact.

`make_brand_cues.py` builds A by default (into `assets/audio/combat/` and `audio_history/brand_curse/A/`, refusing any
rebuild whose bytes differ from the pin); B and C build only into `audio_history/brand_curse/<letter>/`, with no option
to install them. The game has no candidate selector (tested).

### Pinned SHA-256 (Candidate A)

| cue | SHA-256 |
|---|---|
| `sfx_seeker_brand_apply` | `87bc3b624e3171f31959c8e622927f68c921ec0ffc45ac8274c0d646c6fe4779` |
| `sfx_seeker_brand_deepen` | `688ea4eac54d971c522d1cf7eabd502bce44699e1dc25daea5bbcb3fd2af46af` |
| `sfx_seeker_brand_deepen_deep` | `a82ec0eb948db46d6cc0c7c354c7daf8792542aae2acb61e56fc1b89b54d4fc2` |
| `sfx_seeker_brand_infect` | `48cc643de86f34e4e492598949d3fe77ade4520f5b924888125a25184d0b819f` |
| `sfx_seeker_brand_leave` | `696361cf23e8d8ae1011eeb787da3348ae2e92c80494e014f8d292944880f3b0` |
| `sfx_seeker_brand_awaken` | `81e8cc582fa9149b6652441b7b506d06f06e34906a59ba92fb04b5164744e05c` |
| `sfx_seeker_brand_ash` | `bb771c68e3afc3795788bc8aae01fa53011dc92620c6c02c717846678acf2b34` |

### Small-speaker risk (a documented hardware sanity-check item)

A is low and internal by design (0.48-0.71 of each main cue's energy sits under 150 Hz), and laptop / small speakers
barely reproduce that band, so a cue may lose definition there. Check A on small speakers. The owner's narrowly scoped
**permitted correction**, if that check finds A unintelligible: preserve A's character and envelope; do not raise its
combat dominance (it stays under SPRAY, HARD HANDS, JAWS and PRESS's crush); add only enough mid information for
intelligibility; never migrate toward B's 4-5 kHz character; re-review with the owner before any pinned byte changes.
