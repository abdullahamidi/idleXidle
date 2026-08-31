# Audio System: IDLExIDLE

> **SUPERSEDED 2026-09-01 by src Game/SoundBank.cs + design/audio/asset-generation-audio.md.** Describes the manual-combat telegraph audio model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | audio-director |
| **Status** | Complete — authored autonomously, no user available this session (auto mode, task explicitly instructs "do not ask questions, write early"). Ambiguities resolved using `design/gdd/creature-ai-telegraph-system.md`, `design/gdd/accessibility-settings-system.md`, `design/gdd/combat-encounter-system.md`, `design/gdd/region-mastery-automation-system.md`, `design/gdd/creature-jobs-evolution-system.md`, `design/art/art-bible.md` (§§2, 3.1–3.5, 4.2–4.6, 5.3, 6.3, 7.7), and `design/gdd/game-concept.md` as authority; every resolution is flagged inline as an assumption below, matching this project's established convention for autonomously-authored documents. |
| **Priority / Tier** | MVP — Polish layer (`design/gdd/systems-index.md` #23). **Mechanically load-bearing, not decorative**: per `game-concept.md`'s own audio-needs framing ("telegraph/weak-point audio cues are functionally important") and `creature-ai-telegraph-system` §3.9's binding accessibility requirement, this system's telegraph pre-cue is a second, parallel information channel to the glyph system — not a mood layer riding on top of it. |
| **Depends On** | `creature-ai-telegraph-system` (the telegraph curve, `t_eased`, and the `TELEGRAPH_WINDUP_FLOOR_MS = 600` floor), `combat-encounter-system` (discrete combat events), `region-mastery-automation-system` (the Mastery Transition sequence and region state), `creature-jobs-evolution-system` (work-state signals and per-role tick cadence), `accessibility-settings-system` (volume buses, the pre-cue toggle, mono/normalization settings) |
| **Depended On By** | None (leaf system — confirmed against every dependency's own "Depended On By" table; `creature-jobs-evolution-system` is the one exception, flagged in §6). |

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Active Hunting and Region Boss Fights carry no looping melodic/rhythmic music bed.** Combat's ambient layer is a low-prominence, harmonically-neutral continuation of the region's own ambient drone, suppressed to a conquest-state-neutral treatment for the duration of the encounter. | Directly extends art-bible §4.4's already-locked visual rule ("during Active Hunting, ambient conquest-color is suppressed to Cold Slate neutral regardless of whether the region is actually unconquered or mastered... the temperature story never competes with the vulnerability-glyph read") into audio. A busy musical score would compete in the same frequency/attention space as the telegraph pre-cue's pitch/tempo ramp — directly undermining the one channel this document is bound to keep legible. Art-bible §2 independently confirms combat's mood is "coiled... never frenetic screen-noise," which a music bed risks violating. |
| A2 | **The telegraph pre-cue is silent outside an active telegraph** — it does not attempt an audio equivalent of the vulnerability glyph's always-on `resting_intensity` glow. | The glyph's resting state communicates an *absence* of danger; rendering that absence as a persistent quiet hum for every creature on screen would be sensory clutter with multiple hostiles in view and would dull the very onset transient a player needs to notice. Presence-of-sound = telegraph active, absence-of-sound = safe is a stronger, simpler binary than trying to sonify "nothing is happening." |
| A3 | **The idle soundscape is a sonification layer, not a simulation driver** — its audio-trigger timing is quantized to a shared grid (§4 Formula 2) that may lag a creature's actual production tick by up to half a grid slot. This is explicitly *not* held to the telegraph pre-cue's zero-tolerance, same-frame contract. | `creature-jobs-evolution-system`'s production ticks (Formula 5) are a balance-and-simulation concern; nothing in that document requires audio-perfect synchronization, and forcing it would reintroduce exactly the cacophony problem this document is tasked with solving. The distinction is stated explicitly so no future system mistakes idle-soundscape timing for a mechanically binding contract the way the telegraph pre-cue is. |
| A4 | **Bus routing**: the telegraph pre-cue and the idle soundscape both route under the already-locked `volume_sfx` bus; region ambient themes and the Mastery Transition's musical swell route under `volume_music`. No new top-level bus is introduced. | `accessibility-settings-system` §3.5 already locks exactly five volume sliders and explicitly assigns `volume_sfx` to "Combat, part-break, telegraph audio cues, UI feedback sounds" and `volume_music` to "Region ambient themes." This document's job is to route correctly into that already-approved architecture, not invent a sixth slider — and it directly satisfies the task's "independent volume buses so a player can boost mechanical cues relative to music" requirement for free, since Music and SFX are already independent faders. |
| A5 | **The Source sonic palette (§3.2) is shared verbatim between the telegraph pre-cue's short articulation and the region theme's sustained arrangement** — the same instrument family and sonic character, played two different ways at two different scopes. | Directly extends art-bible §3.3's "the crack never moves, it only closes" principle — the same shape-idea operating at two different scales of the game — into audio, and is the literal mechanism that answers the task's "the audio and visual languages should feel like the same idea in two media" instruction. |
| A6 | **Blocked creatures are not silent** — they hold a static, non-pulsing, low-level sustain of their role's timbre rather than dropping to true silence. | Matches art-bible §5.3's Blocked description exactly ("holds at a steady, non-pulsing glow — lit but frozen," "the glow goes flat, not brighter") — the creature is still lit, just not producing. A silent creature would read as *absent*, not *stalled*; a held, unchanging tone reads as *present but not working*, which is the correct signal, and its rhythmic pulse still visibly (audibly) drops out of the region's groove, which is what makes it detectable (§4 Formula 2, §5 Edge Case #1). |
| A7 | **Region Boss Fights get a stepped ambient-intensity escalation per `Phase` transition**, never a new musical cue — a discrete step up in the neutral combat bed's loudness/filter-openness, not a change of material. | Matches art-bible's Region Boss Fight mood ("Escalating — begins at standard-encounter tension and builds in discrete steps, each phase transition raising the visual and emotional stakes") and its resonance-halo signature element, while staying inside A1's "no music in combat" constraint — escalation is expressed as intensity, not melody. |
| A8 | **Bidirectionality gap, not self-edited**: `creature-jobs-evolution-system`'s own §6 "Depended On By" list does not currently include `audio-system`, unlike every other dependency this document relies on (all four others already list `audio-system` in their own tables, confirmed by direct read). Per this session's file-discipline instruction (write only `design/gdd/audio-system.md`), this gap is documented here rather than silently fixed, following the exact precedent set by `onboarding-tutorial-system.md`'s own flagged gap against `creature-ai-telegraph-system.md`. | Keeps the dependency graph honest without violating the single-file-write constraint this session operates under. |

---

## 1. Overview

The audio system defines IDLExIDLE's complete sonic identity and, uniquely among this
game's audio systems, carries **mechanical weight equal to the visual layer for one specific
signal**: the telegraph pre-cue. Because `creature-ai-telegraph-system` §3.9 locks a redundant
audio pre-cue on the telegraph's slow-build/hard-cutoff curve as a binding accessibility
requirement — not a balance change, not decoration — this document treats audio as a **second,
parallel information channel to the glyph system**, not a mood layer riding on top of combat. It
defines: the telegraph pre-cue's exact curve and hard-cutoff behavior; six distinct Source
timbres that map one-to-one onto the visual edge-quality language already locked in
art-bible §3.1; the combat audio event set (part-breaks, weak-point hits, the defensive triad,
Resonance, the Ultimate); the Mastery Transition's audio arc (the game's emotional thesis
rendered in sound); a **generative** idle soundscape driven by live work-state data rather than a
looping track; region themes derived from each region's dominant Source; the mix/bus structure
and priority-ducking rules that keep all of the above legible at once; and the accessibility
bindings that guarantee every mechanically important cue this document defines has a complete,
standalone visual equivalent already locked elsewhere. It does not create audio assets, does not
write audio engine/DSP code (`gameplay-programmer`/`engine-programmer`'s job), and does not alter
audio middleware without `technical-director` sign-off.

## 2. Player Fantasy

> **Sound as a second way of seeing. The hunter who hears the attack coming.**

Every other system in this project's sound design serves atmosphere. This one serves survival.
A player reading this game's combat purely by ear — because they are Blind, because they process
fast visual change poorly, because the screen is dim, or because they have simply learned to
listen — must be able to do everything a sighted player can do: recognize that an attack is
building, judge how much time remains, tell roughly what kind of attack it is, and commit a
block, a dodge, or an interrupt with the same confidence `creature-ai-telegraph-system`'s own
Player Fantasy describes — "the calm of a hunter who has already read the pattern... not the
panic of someone reacting blind." This document exists so that sentence is never accidentally
false for a player relying on their ears instead of their eyes.

Concretely, this system's Player Fantasy guarantees:

- **The wind-up is audible before it is dangerous.** A rising tone and an accelerating pulse are
  not sound effects layered on top of a fight — they *are* the fight's clock, for a player who
  cannot or does not want to read it visually.
- **The moment demanding action is unmistakable.** The pre-cue's hard cutoff — a full-volume ramp
  replaced, in the same instant, by dead silence — is as sharp and as legible an "act now" signal
  as the glyph's own brightness snap. Neither channel is allowed to soften or blur that instant.
- **A region full of working creatures sounds alive, not idle.** Just as art-bible §5.3 refuses to
  let a bound creature's baseline appearance be a parked sprite, this system refuses to let a
  healthy, automated region be silent. Its rhythm is the sound of the player's own economy
  breathing — and when a creature is struggling, the rhythm audibly breaks, the same way a missed
  beat in a song is the first thing anyone notices.
- **The moment a boss falls is the game's warmest sound**, not its quietest. The Mastery
  Transition's audio arc is this game's thesis — automation as reward, not shutdown — rendered
  entirely in sound: a held breath, then a swell that grows outward, never inward.
- **Every mechanically important sound has an equally complete twin in light and shape.** A Deaf
  player, or any player with audio fully disabled, never loses information — only atmosphere.
  This document treats that guarantee as load-bearing, not aspirational (§3.9, §5).

## 3. Detailed Rules

### 3.1 Scope and Boundaries

This document defines the audio design and mix behavior for every game state. It does **not**
define: audio engine implementation, DSP code, or middleware integration (engineering); the
telegraph curve's timing values or the `TELEGRAPH_WINDUP_FLOOR_MS`/window-ratio constants
(`creature-ai-telegraph-system` owns those; this document consumes them by reference); combat's
damage/hit-resolution rules (`combat-encounter-system`); work-state transition logic
(`creature-jobs-evolution-system`); or the settings surface/persistence for any volume slider or
toggle named below (`accessibility-settings-system`, already locked). Every numeric value in this
document that overlaps a value owned elsewhere is restated, never redefined.

### 3.2 The Six Source Timbres

Per art-bible §3.1, Source identity is legible purely from edge-quality — Body's convex smooth
arcs, Mind's hard faceted cuts, Nature's branching asymmetry, Machine's orthogonal rigidity,
Shadow's torn discontinuity, Spirit's soft dissolving wisps — deliberately independent of color.
This section is that same axis, translated into sound. Per A5, one palette serves two scopes: a
short articulation for the telegraph pre-cue (§3.3) and a sustained arrangement for region themes
(§3.7) — the same sonic character, played two different ways.

| Source | Edge Quality (art-bible §3.1) | Telegraph Pre-Cue Voice (short articulation) | Region Theme Instrumentation (sustained) | Why This Maps |
|---|---|---|---|---|
| **Body** | Convex, smooth, symmetric arcs | A continuous portamento glide — a bowed/breath tone (cello- or voice-adjacent) sliding smoothly upward with no steps | Warm string/breath ensemble, legato phrasing, a slow sub-bass pulse under the arrangement | A glide has no edges — it is the sonic equivalent of an unbroken, rounded contour. |
| **Mind** | Hard, faceted, symmetric cuts | A quantized, stepped pitch rise — discrete pure/bell tones (glass/mallet) climbing in clean, equal steps, never a slide | Crystalline mallet and glass textures, clean arpeggios, precise, unwavering meter | Discrete steps are the audio equivalent of a faceted cut — no glide between them, ever. |
| **Nature** | Branching, asymmetric outgrowths | Layered, irregularly-timed pulses (wood/rattle/branch-snap texture) with deliberately non-metronomic micro-timing | Woodwind, string harmonics, and found-object percussion in loose, non-metric rubato with overlapping asymmetric polyrhythm | Irregular, non-repeating timing is the sonic analog of irregular, non-repeating branching. |
| **Machine** | Orthogonal, rigid plates | A strict metronomic ratchet/click, perfectly regular, square/pulse-wave timbre, rising in rigid discrete steps like a tightening gear | Metallic percussion, modular-sequencer pulse, strict unyielding meter, industrial drone | Perfect regularity and hard-edged waveforms mirror right angles and bolted plates. |
| **Shadow** | Torn, discontinuous edges | A gated/bitcrushed texture with brief dropouts and stutters inside the ramp itself — the tone is interrupted, not smooth | Granular/glitch texture over a sub-bass drone, sparse dissonant clusters, irregular silences | The pre-cue's own continuity is torn, exactly as the silhouette's contour is. |
| **Spirit** | Soft, dissolving wisps | An airy, breathy pad with soft attack and a long reverb tail that blurs its own edges | Airy pads, breath/choir synths, high reverb, sparse melodic fragments that dissolve rather than resolve | A tone whose edges dissolve into reverb is the direct sonic translation of a contour that thins into wisps. |

**The universal shape is identical across all six** — every Source's telegraph voice still rises
in pitch, gain, and pulse-tempo on the exact same `t_eased`-driven curve (§4 Formula 1) and cuts
at the exact same instant. Timbre is the **secondary, non-load-bearing** "which element" tell —
the audio mirror of §3.3's hue drift, which art-bible §4.3 itself already calls non-load-bearing.
*Design test, audio version of art-bible §4.3's own gray-filter test*: strip every pre-cue down to
a sine-wave click track (same rise/cutoff, no timbre) — a player must still be able to tell "safe"
from "about to hit me" with zero information loss. Only the *which-element* read is lost, exactly
as covering the glyph's hue in gray only loses the *which-element* read there.

### 3.3 The Telegraph Audio Pre-Cue (Mechanically Critical)

This is the single most important sound in the game. `creature-ai-telegraph-system` §3.9 requires
it to track the visual brightness ramp's exact `t_eased` value — not an independently computed
approximation — and cut at the exact same `elapsed_ms = windup_duration_ms` instant. This
document's design satisfies that contract with three simultaneous, stacked layers, all driven by
the one shared `t_eased` value:

1. **Tone layer (pitch + gain ramp)** — a continuous tone rises in both pitch and loudness across
   the full wind-up, using the Source's assigned voice from §3.2. Rising pitch is chosen because
   it is one of the most cross-culturally legible tension-escalation cues that exists (siren,
   dive-bomb, heart-rate-monitor alarm) — a player needs zero training to feel "this is
   building toward something."
2. **Pulse layer (tempo acceleration)** — a discrete rhythmic tick, voiced in the same Source
   timbre, whose *interval* shrinks across the wind-up (§4 Formula 1, Part C) — the tick
   audibly speeds up, exactly like an accelerating heartbeat monitor. This is the layer this
   document adds *beyond* what the parent document explicitly names ("volume/pitch ramp"),
   because rhythm-acceleration is an audio-native precision tool pitch alone cannot match: humans
   judge relative tempo change with very high resolution, and — critically — §4 Formula 1's
   worked example proves multiple audibly-shrinking pulses fit even inside the 600ms
   accessibility floor. This is the layer doing the heaviest lifting for a player with no visual
   channel at all.
3. **Hard cutoff** — at `elapsed_ms = windup_duration_ms` exactly, both layers stop **in the same
   audio buffer, with zero release envelope and zero reverb/delay tail** — a true dry stop, not a
   fast fade. This mirrors the visual's same-frame snap-to-resting exactly, and is stricter than
   the visual in one respect: because audio has a decay/tail problem the visual doesn't (a
   glyph can't "ring" after it stops glowing, but a reverberant tone can), the pre-cue's send to
   any reverb/delay bus is explicitly muted in the same instant, not merely the dry signal —
   otherwise a smeared, ambiguous "is it still happening?" tail would undermine the exact
   unmistakability this cue exists to guarantee.

**Why the cutoff can never be mistaken for part of the ramp**: both layers are strictly
monotonic — pitch, gain, and pulse-tempo only ever increase, never plateau or dip, for the entire
wind-up (mirroring Formula 2's monotonic brightness in the parent document). Any drop or stop a
listener hears is therefore, by construction, never ambiguous — it can only be the cutoff, never a
natural variation in the ramp itself.

**Proof this supports audio-only play**: a player with the screen off (or profoundly reliant on
audio) hears a rising tone and an accelerating tick from the instant a wind-up starts. They do not
need to know the attack's exact `avoidance_options` or window ratios to act correctly — the same
way a sighted player does not need to see numeric countdown text to react to a brightening glyph.
What they need, and what this cue provides, is a continuously legible sense of "how much time is
left" (via pitch/gain height and pulse speed) and an unambiguous "the window to act is now/almost
gone" signal as the ramp nears its ceiling and the pulse nears its fastest rate — precisely the
information a sighted player extracts from watching the glyph brighten and accelerate toward peak.
Acceptance Criterion #1 (§8) makes this a testable requirement, not an assertion.

### 3.4 Combat Audio Events

Sourced from `combat-encounter-system` §6's own explicit event list ("basic attack fire,
ability/ultimate cast, block/dodge/interrupt success or failure, part-break, Kill/Capture/Retreat
outcome").

| Event | Design | Reward Gradient Alignment |
|---|---|---|
| **Basic attack lands** | A light, punchy transient, Source-neutral (the Hunter's own weapon voice, not the target's Source). | Baseline. |
| **Ability hit lands** | A slightly larger transient than a basic hit, layered with a hint of the cast ability's own Form voice (owned by `resonance-weaving-system`, referenced not redefined here). | Above basic. |
| **Weak-point / vulnerability hit** | Any hit landing on a currently-vulnerable part (`vulnerability_damage_multiplier` applies, `combat-encounter-system` §4 Formula 3) plays a brightened, higher-transient variant of the normal hit sound — an audible "that counted extra" accent, distinct in timbre, not just louder. | Confirms the reward window is live. |
| **Part-break** | The satisfaction moment art-bible names explicitly ("hit-stop and audio payoff"). A brief hit-stop (~80–120ms, a near-total time-freeze on the combat simulation's presentation layer only — owned by whichever system implements hit-stop, this document only specifies the audio riding on it) synced to a layered crack/shatter transient plus a one-shot Source-timbre accent (§3.2's telegraph-voice character, not the sustained region-theme version) confirming *which* Source the broken part carried. A distinct rising "confirm" stinger follows, separate from the impact itself, celebrating the break rather than just registering it. | The single loudest, most rewarding non-Ultimate sound in combat — matching its role as a fight-shaping tactical payoff. |
| **Block success** | A dampened, muffled thud — solid, safe, deliberately unexciting. | Lowest reward, by design (A5 of `creature-ai-telegraph-system`). |
| **Dodge success** | A bright, airy whoosh — a clean "miss" sound, satisfying but brief. | Mid reward. |
| **Interrupt success** | The pre-cue's own hard cutoff (already silence, §3.3) is immediately followed by the most dramatic of the three confirmations — a sharp cancel/shatter stinger, distinctly more elaborate than dodge's whoosh, reinforcing the reward gradient (interrupt > dodge > block) sonically as well as mechanically. | Highest reward — the loudest, most distinct confirmation of the triad. |
| **Resonance gain (any trigger)** | A small pitched tick whose pitch itself rises as `current_resonance` fills toward `resonance_cap` — a continuous, satisfying "meter filling" cue independent of which trigger caused the gain. | Scales with progress toward the Ultimate. |
| **Ultimate activation** | A unique, signature stinger, distinct from every other combat sound in the game. Triggers a brief spotlight duck (§3.8) on all lower-priority buses so the stinger reads with full clarity, then the meter-fill tick resets to its lowest pitch. | The single largest audio moment combat produces short of the Mastery Transition. |
| **Kill / Capture / Retreat outcome** | Kill (standard) plays a short defeat cue matching its 1500ms `RESOLVING` window; Kill (boss) hands off entirely to the Mastery Transition arc (§3.5); Capture plays a placeholder beat identical in shape to a standard Kill until `rare-creature-capture-system` specifies its own (matching that document's own placeholder framing); Retreat plays a subdued, non-punishing exit cue — never a failure stinger, matching `combat-encounter-system` §3.8's "Retreat, not death" framing. | Retreat is deliberately *not* punishing in sound, mirroring its zero-penalty design. |

### 3.5 The Mastery Transition Audio Arc

Art-bible §2 calls this "the single most important beat in the game's visual language" —
"coronation, not shutdown." This is that beat's audio thesis. It rides on
`region-mastery-automation-system` §3.7's locked mechanical sequence, inside
`combat-encounter-system`'s own 3000ms boss `RESOLVING` window (§3.9 there). Three phases, sharing
that window's proportions (exact frame-sync to the cinematic's own keyframes — e.g. the precise
instant of glyph inversion — is implementation-time collaboration with whichever system authors
the transition cinematic; this document specifies the audio arc's shape and proportional timing,
not a frame-locked script):

1. **The held breath (~0–25% of the window, ≈0–750ms).** The fight's last sound — whatever
   confirmed the killing blow (§3.4) — resolves, and combat's neutral bed (A1) cuts hard, dropping
   to near-total silence or a single sustained low drone. This is the "Released — a held-breath
   exhale" mood stated directly: nothing swells yet. The player is meant to feel the fight
   actually stop before the reward begins.
2. **The outward swell (~25–85% of the window, ≈750–2550ms).** The region's own ambient theme
   (§3.7) begins morphing live, in real time, from its unconquered treatment toward its mastered
   treatment — the *same* Source-instrument material (A5), not a separate cue, cross-fading
   in harmony, dynamic range, and stereo width. This is the load-bearing choice the visual
   locks and this document mirrors exactly: the swell's stereo field widens and its reverb sends
   grow *outward* (mono-leaning → wide) while its register gains higher overtones (opening a
   low-pass filter upward) — direction is everything here, precisely matching art-bible §2's "the
   light must grow outward and upward, never shrink or contract inward. Contraction reads as
   loss; expansion reads as growth." A contracting, narrowing, or darkening swell would be a
   direct violation of this document's one binding creative rule for this beat. At the visual's
   own glyph-inversion instant (wherever the cinematic places it inside this phase), a precise,
   discrete bell/chime hit lands inside the swell — two harmonics resolving into consonance,
   mirroring the Ward seal's closed-double-ring container (art-bible §3.2) — the single sharpest,
   most intentional sound in the whole arc, marking the exact moment the boss's own glyph becomes
   the seal.
3. **The settle (~85–100%+ of the window, ≈2550–3000ms, continuing past the handoff).** The swell
   resolves fully into the region's stable mastered theme loop (§3.7), with no hard cut or silence
   gap at the moment control returns to the player — the theme simply continues, now fully
   mastered, into the region hub. As this phase resolves, the very first idle-soundscape work-pulse
   (§3.6) from the just-bound creature is allowed to fade in as a coda — the Mastery Transition's
   ending *is* the idle soundscape's beginning, the same creature whose defeat the player just
   heard now audibly starting its first work cycle.

### 3.6 The Generative Idle Soundscape

Per A3, this is a **sonification layer driven by live work-state data**, not a looping ambient
track. Every Healthy, Blocked, and Starved creature contributes a voice; the region's aggregate
sound is emergent, never pre-composed.

- **Healthy**: plays its Role's work-tick pulse (matching art-bible §5.3's per-Role verb and
  `creature-jobs-evolution-system` §7's cadence ordering) at its own cadence, quantized onto a
  shared rhythmic grid (§4 Formula 2) so many creatures' pulses lock into a coherent, danceable
  texture rather than colliding randomly. Each Role occupies a distinct register/texture so
  simultaneous pulses from different Roles never mask each other (frequency-domain separation,
  a standard mixing technique):
  - **Crafter** *(fidgets, fastest)* — high register, short plucky/clicky texture (plucked
    string or mallet).
  - **Attacker** *(strikes)* — mid-low register, a single punchy percussive hit.
  - **Support** *(orbits)* — mid register, a gentle sweeping whoosh, panned left-right across its
    voice to suggest the satellite's orbit spatially.
  - **Defender** *(braces, rarer)* — low register, a low sustained thud/creak at the brace-flex
    peak.
  - **Producer** *(breathes, slowest)* — very low register, a soft, slow swell-and-release breath.
- **Blocked** (A6): drops out of the rhythmic grid entirely — no pulse fires — but the creature's
  Role voice continues as a faint, static, unchanging sustain at low volume. The missing pulse in
  an otherwise-locked groove is what makes it detectable (a well-established auditory "oddball"
  effect — the ear is highly sensitive to a beat that fails to land in an established rhythm).
- **Starved**: deliberately **not** quantized to the shared grid — an irregular, off-grid distress
  motif, using a small pitch-rise blip that faintly borrows the telegraph pre-cue's own
  tension-signaling language (reusing the game's established "needs your attention" vocabulary,
  exactly as art-bible §5.3 reuses Ember Threat color for Starved, rather than the game's
  ambient-color system inventing a second one). Starved voices are exempt from the density cap and
  ducking in §4 Formula 2 — they are alerts, not ambience, and must always be audible.
- **No creatures assigned / a freshly Mastered region with an empty team**: the idle soundscape
  has nothing to generate. The region's ambient theme (§3.7) plays alone — silence from the
  work-pulse layer is not an error state, it is an accurate, honest representation of "nothing is
  working yet."

### 3.7 Region Themes

Per art-bible §6.3, a region's identity is carried primarily by its dominant elemental Source.
This document's music mirrors that decision exactly: **a region's ambient theme is built entirely
from its dominant Source's sustained instrumentation** (§3.2's region-theme column) — no separate,
independent musical-genre-per-biome system, matching the same "Source-first, biome falls out as a
consequence" logic art-bible already locked for visual design.

**The Temperature Law in music**, extending art-bible §4.4's warm↔cool axis:

| | Unconquered | Mastered |
|---|---|---|
| Harmony | Minor/dissonant, unresolved | The *same* melodic seed, resolved to consonance |
| Mix | Filtered/muffled (low-pass), narrow stereo, thin dynamic range | Full-band, wide stereo, fuller dynamic range |
| Tempo/rhythm | Sparse, irregular, rubato-leaning | Stable, settled meter |
| Arrangement density | Sparse — few layers | Fuller — more of the Source's instrument family unlocked |
| Texture | A faint noise/grain bed (Corruption-adjacent) | Added high-frequency shimmer/overtone (Hearth-Gold-adjacent brightness) |

Per art-bible §3.3's "the crack never moves, it only closes," the **musical material never
changes between the two states — only its treatment does**, exactly like the sigil-scar's
identical crack path rendered two ways. This is not a new theme replacing an old one; it is the
same theme finding its resolution, which is precisely what the Mastery Transition's swell (§3.5)
performs live, in real time, as the crossfade between these two treatments.

**Suppression during combat (A1)**: exactly like art-bible §4.4's own visual rule, a region's
Unconquered/Mastered treatment is an *establishing-shot and idle-view* signal, never a
*combat-frame* signal — during Active Hunting, the region theme is suppressed to a
conquest-state-neutral bed regardless of the region's actual state, returning the instant the
encounter ends (§5 Edge Case #7).

### 3.8 Mix and Bus Structure

Per A4, this system routes into the exact five buses `accessibility-settings-system` §3.1 already
locks — no new top-level bus:

```
Master
 ├─ Music (volume_music)   — region ambient themes (both treatments), Mastery Transition swell,
 │                            Forge/Creature-Management ambient beds
 ├─ SFX (volume_sfx)
 │    ├─ Combat             — hits, part-breaks, defensive-triad confirmations, Resonance ticks,
 │    │                       Ultimate stinger, encounter-outcome cues
 │    ├─ Telegraph Pre-Cue  — the mechanically critical channel (§3.3); shares the SFX fader but
 │    │                       carries its own internal priority (below)
 │    └─ Idle Soundscape    — generative work-pulse layer (§3.6)
 ├─ UI (volume_ui)          — menu/HUD interaction sounds
 └─ Dialogue (volume_dialogue) — reserved, no-op in MVP scope, per accessibility-settings-system §3.5
```

**Priority-ducking tiers** (highest to lowest; while any event in Tier N is active, every bus
content in Tiers > N ducks by `tier_duck_db`, §7, then releases smoothly, ≈150ms release, to avoid
audible pumping):

| Tier | Content | Ducked By Anything? |
|---|---|---|
| **0** | Telegraph pre-cue | **Never.** Tier 0 ducks everything below it and is itself immune — the one hard rule of this entire mix. |
| 1 | Combat confirmations (hits, part-breaks, defensive triad, Resonance, Ultimate stinger) | By Tier 0 only. |
| 2 | Idle soundscape — Starved distress voices | By Tiers 0–1. |
| 3 | Idle soundscape — Healthy pulses / regional hum bed | By Tiers 0–2. |
| 4 | Region ambient theme / combat neutral bed / Mastery Transition swell | By Tiers 0–3. |

The Ultimate's spotlight duck (§3.4) operates within Tier 1's own internal priority (it ducks
Tiers 2–4, never Tier 0) — a currently-active telegraph pre-cue and an Ultimate cast can
legitimately sound at once without conflict, since one is a sustained ramp and the other a short
transient occupying a different time-domain of the mix.

### 3.9 Accessibility Bindings (Restated, Not Redefined)

Per `accessibility-settings-system` §3.7 item 10 ("No gameplay-critical audio cue ships without a
redundant visual or haptic equivalent") — a binding requirement imposed on this document, honored
explicitly:

- **Telegraph pre-cue** → the visual telegraph pulse (`creature-ai-telegraph-system` §3.7) is
  always present, on the same curve, regardless of any audio setting. A Deaf player, or any player
  with `volume_master = 0`, loses zero mechanical information.
- **Part-break payoff** → the crack/glyph escalation rendered directly on the creature's own
  part-seam (art-bible §7.5) is diegetic and always visible, independent of the audio payoff.
- **Idle soundscape (Healthy/Blocked/Starved)** → art-bible §5.3's pose/motion + glyph-color table
  is a complete, standalone visual signal set; this document's audio is a redundant, not
  exclusive, channel onto the same three states.
- **`telegraph_audio_precue_enabled`** (default **on**) is the player-facing toggle for §3.3,
  owned by `accessibility-settings-system`; this document supplies the sound, not the setting.
- **`volume_master/music/sfx/ui`**, **`mono_audio_enabled`**, and
  **`audio_loudness_normalization_enabled`** are consumed as direct pass-through values (§7);
  this document is the system that must honor them correctly (§5 Edge Case #10).

## 4. Formulas

Both formulas below reuse the project's round-half-up convention for integer outputs (millisecond
timings, voice counts); pitch/gain curve outputs remain unrounded floats, matching
`creature-ai-telegraph-system` §4's own rounding scope.

### Formula 1 — Telegraph Audio Curve (Pitch, Gain, Pulse-Tempo, Hard Cutoff)

**Part A/B — Tone layer (pitch and gain), sharing `t_eased` with the visual channel exactly:**

```
t_eased = [identical value computed by creature-ai-telegraph-system Formula 2 — never
           independently recomputed by this system]

precue_pitch_semitones(t) = precue_pitch_floor_semitones
                           + (precue_pitch_ceiling_semitones − precue_pitch_floor_semitones) × t_eased

precue_gain_db(t) = precue_gain_floor_db
                   + (precue_gain_ceiling_db − precue_gain_floor_db) × t_eased
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `t_eased` | float | `[0, 1]` | Shared with the visual brightness curve — the single source of truth for both channels. |
| `precue_pitch_floor_semitones` / `precue_pitch_ceiling_semitones` | float | tuning, default `0` / `12` (one octave); safe range `6–19` (§7) | Relative pitch offset from the pre-cue voice's base note. |
| `precue_gain_floor_db` / `precue_gain_ceiling_db` | float (dB) | tuning, default `−18` / `0`, relative to the pre-cue's own internal submix | Loudness envelope within the pre-cue's own signal chain, further shaped downstream by bus/master faders. |

**Worked example** (`windup_duration_ms = 1800`, matching `creature-ai-telegraph-system` Formula
2's own worked example for direct comparability), `elapsed_ms = 900` (the 50% mark, `quad_ease_in`
default curve, `t_eased = 0.25` per that document's own worked math):

```
precue_pitch_semitones = 0 + 12 × 0.25 = 3.0 semitones above base
precue_gain_db          = −18 + 18 × 0.25 = −13.5 dB

At elapsed_ms = 1800 (cutoff): t_eased = 1.0
precue_pitch_semitones = 12.0 (one full octave above base)
precue_gain_db          = 0 dB (full prominence)
— then, in the same audio buffer, both layers and any reverb/delay send cut to full silence.
```

**Part C — Pulse layer (tempo acceleration), the layer beyond the parent document's literal text,
justified in §3.3:**

```
pulse_interval_ms(t) = precue_pulse_interval_start_ms
                      − (precue_pulse_interval_start_ms − precue_pulse_interval_floor_ms) × t_eased

Scheduling: a pulse fires at elapsed_ms = 0; each subsequent pulse fires
pulse_interval_ms(t_eased at the previous pulse's elapsed_ms) later, until a scheduled pulse would
land at or past windup_duration_ms, at which point no further pulse fires (the hard cutoff, not a
pulse, is the final event).
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `precue_pulse_interval_start_ms` | int | tuning, default `150`; safe range `80–300` (§7) | Interval between the first two pulses (slowest, at telegraph start). |
| `precue_pulse_interval_floor_ms` | int | tuning, default `40`; safe range `25–80` (§7) | Interval floor as `t_eased → 1` (fastest, just before cutoff). |

**Worked example A — the accessibility-floor proof** (`windup_duration_ms = 600`, the fixed
`TELEGRAPH_WINDUP_FLOOR_MS`, `quad_ease_in`): pulses fire at `elapsed_ms ≈ 0, 150, 293, 417, 514,
583` — six discrete pulses, with successive intervals `150, 143, 124, 97, 69 ms`, each one
audibly shorter than the last, entirely inside the 600ms floor. This is the direct, worked proof
this document owes, mirroring `creature-ai-telegraph-system` Formula 1's own Worked Example B: even
at maximum difficulty, the tempo-acceleration cue remains legible, never collapsing to zero or one
pulse.

**Worked example B — a generous wind-up** (`windup_duration_ms = 1800`, same curve): the first six
pulses land at `elapsed_ms ≈ 0, 150, 299, 446, 589, 727` with only gently shrinking intervals
(`150, 149, 147, 143, 138 ms`) — matching the "slow build" the visual channel also performs — and
the interval compression accelerates sharply only in the final ~15% of the wind-up, producing a
distinct closing "trill" immediately before cutoff, the audio equivalent of the visual curve's own
late-curve acceleration (Formula 2's `quad_ease_in` shape in the parent document).

**Output range**: `pulse_interval_ms(t)` is bounded to
`[precue_pulse_interval_floor_ms, precue_pulse_interval_start_ms]` for any `t_eased ∈ [0,1]`,
guaranteeing the pulse layer never produces an unbounded/instantaneous buzz nor stalls at a fixed
rate — it always audibly accelerates.

### Formula 2 — Idle Soundscape Pulse-Cadence Mixing (the Cacophony Problem, Solved)

**Part A — Grid quantization** (decouples audio trigger timing from the simulation tick, per A3):

```
scheduled_grid_slot(creature) = round(creature_work_tick_time_ms ÷ grid_slot_ms) × grid_slot_ms
audio_trigger_time_ms(creature) = scheduled_grid_slot(creature) + jitter_ms
jitter_ms = uniform_random(−precue_jitter_max_ms, +precue_jitter_max_ms)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `creature_work_tick_time_ms` | int | from `creature-jobs-evolution-system` Formula 5 | The creature's own, unmodified production-tick timestamp. Never altered by this formula. |
| `grid_slot_ms` | int | tuning, default `250`; safe range `150–400` (§7) | The shared rhythmic grid every Healthy voice snaps onto — the mechanism that turns independent per-creature timers into one coherent groove instead of random collisions. |
| `precue_jitter_max_ms` | int | tuning, default `20`; safe range `0–40` (§7) | Small per-voice randomization so the grid doesn't sound robotically over-quantized; kept well below `grid_slot_ms/2` so it never reintroduces collision risk. |
| `audio_trigger_time_ms` | int | — | The actual moment the voice sounds. Applies to Healthy voices only — Blocked contributes no pulse (A6) and Starved is deliberately off-grid (§3.6). |

This snap costs at most `grid_slot_ms / 2` (125ms at default) of perceptual lag between the
simulation completing a tick and the sound representing it — explicitly acceptable per A3, since
the idle soundscape is a sonification layer, not a mechanically binding channel like the
telegraph pre-cue's zero-tolerance, same-frame contract (§3.3).

**Part B — Polyphony cap and density-aware gain (the cacophony fix itself):**

```
active_healthy_voices_this_slot = count of Healthy creatures region-wide whose
                                   audio_trigger_time_ms falls in the current grid slot

if active_healthy_voices_this_slot ≤ max_concurrent_work_pulse_voices:
    every voice in the slot plays, each at:
        voice_gain_db(n) = base_role_gain_db − duck_db_per_doubling × log2(max(1, n))
    where n = active_healthy_voices_this_slot

else:
    the max_concurrent_work_pulse_voices creatures least-recently-heard (round-robin fairness)
    play individually at voice_gain_db(max_concurrent_work_pulse_voices);
    every remaining creature in the slot is folded into one continuous, low-level "regional hum"
    bed layer instead of receiving an individual discrete voice.
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `max_concurrent_work_pulse_voices` | int | tuning, default `8`; safe range `4–16` (§7) | Hard polyphony ceiling for Healthy discrete voices, region-wide, per grid slot. |
| `duck_db_per_doubling` | float (dB) | tuning, default `3`; safe range `2–6` (§7) | Standard polyphony-aware loudness compensation — each doubling of concurrent voices reduces each individual voice's gain, keeping the group's aggregate acoustic energy from scaling linearly with creature count. |
| `base_role_gain_db` | float | authored per Role (§7) | The Role's authored gain at `n = 1` (a single, isolated voice plays at full authored prominence — no ducking applied when nothing else competes for the slot). |

**Worked example — the adversarial worst case**: a fully mastered region has 20 assigned
creatures, including 12 Crafters (the fastest cadence, `work_tick_interval_seconds = 8`, per
`creature-jobs-evolution-system` §7 — the highest collision risk in the roster). In the
statistically ordinary case, 12 independently-offset 8-second cycles spread across
`8000 ÷ 250 = 32` grid slots average `12 ÷ 32 ≈ 0.375` creatures per slot — collisions are rare.
Constructing the worst possible case regardless — all 12 Crafters assigned at the exact same
instant, producing perfectly aligned ticks — still resolves cleanly: `12 > 8`
(`max_concurrent_work_pulse_voices`), so exactly 8 play as discrete voices
(`voice_gain_db(8) = base_role_gain_db − 3 × log2(8) = base_role_gain_db − 9 dB`) and the remaining
4 fold into the regional hum bed rather than each competing as a full discrete voice. Even a total,
adversarial unison collision of 12 identically-timed creatures never produces more than 8
simultaneous discrete voices plus one soft aggregate pad — this is the concrete, worked proof this
document owes for the cacophony problem, and the basis for Acceptance Criterion #4 (§8).

## 5. Edge Cases

1. **Many creatures (up to and including a full 20-creature region) pulse simultaneously.**
   Resolved entirely by Formula 2: the shared rhythmic grid, the hard polyphony cap
   (`max_concurrent_work_pulse_voices`), and logarithmic density-aware gain reduction together
   guarantee the number of simultaneous discrete voices never exceeds the cap and the aggregate
   loudness never scales linearly with creature count, proven for the adversarial worst case above.
2. **Overlapping telegraphs from multiple attacks.** The currently locked encounter model is
   single-hostile-creature-per-encounter with a single `active_telegraph` runtime field
   (`creature-ai-telegraph-system` §3.13.1) — true simultaneous multi-creature telegraph overlap
   does not occur in the current design, so no conflict exists today. Stated as a forward-looking
   binding requirement regardless, per this project's established preemptive-constraint convention
   (`accessibility-settings-system` §3.7 items 8–9, 12): should a future system introduce
   multi-hostile encounters, only the single most temporally urgent telegraph (soonest to cutoff)
   may hold Tier 0 mix priority at any instant; any other concurrent telegraph ducks to Tier 1
   prominence — a subordinate but still audible "secondary tell" — mirroring the visual hero-shape
   hierarchy's own rule (art-bible §3.5) that only one vulnerability glyph is ever the hero shape
   at a time.
3. **Audio disabled entirely (`volume_master = 0`, or the system is muted at the OS/hardware
   level).** The game remains fully playable and fully completable. Every mechanically important
   cue this document defines — the telegraph pre-cue, the part-break payoff, the idle
   soundscape's Blocked/Starved signal — has a complete, standalone visual equivalent that is
   never gated by any audio setting (§3.9). No progress, encounter, or automation state can ever
   become unreadable purely from audio being off. This is Acceptance Criterion #2 (§8).
4. **A Deaf player.** Identical resolution to Edge Case #3, restated from the player's perspective
   rather than the setting's: every visual channel this document's audio mirrors is already
   guaranteed complete on its own by `creature-ai-telegraph-system` (the telegraph pulse),
   `combat-encounter-system`/art-bible §7.5 (the part-break crack), and art-bible §5.3 (the
   Healthy/Blocked/Starved pose-and-glyph table) — this document adds a redundant, not
   exclusive, channel to each.
5. **Telegraph pre-cue audio during the Mastery Transition's music swell.** Cannot arise as a
   sustained conflict: the pre-cue's own hard-cutoff rule (§3.3) guarantees zero release and zero
   reverb tail the instant a telegraph resolves — including the hard-cancel case where a killing
   blow lands mid-telegraph (`creature-ai-telegraph-system` Edge Case #5, "the encounter ends
   immediately... any in-progress telegraph is cancelled without a cutoff/hit resolution" — this
   document's pre-cue is cut in that same instant, matching the "hard-cut immediately, no blend"
   convention that document already establishes for the visual `Clip`). Because there is never a
   lingering tail to begin with, there is nothing left to be masked by, or to bleed into, the
   swell that begins in that same instant — the mechanical cue's job is already finished before
   the swell's job begins. Acceptance Criterion #9 (§8) verifies this with zero audible overlap.
6. **Ultimate activation's spotlight duck occurring while a telegraph pre-cue is simultaneously
   active.** Resolved by the tier table (§3.8): the Ultimate's duck operates on Tiers 2–4 only and
   never touches Tier 0. Both sounds can legitimately play at full prominence at once — the pre-cue
   is a sustained ramp, the Ultimate stinger a short transient, occupying different time-domains of
   the mix rather than competing for the same moment.
7. **The player enters combat while a region's ambient theme is mid-phrase.** The theme suppresses
   to the conquest-state-neutral combat bed (A1, §3.7) at the exact instant `ENGAGING` begins
   (`combat-encounter-system` §3.2), regardless of where in its phrase the theme was — a hard
   crossfade (≈300–500ms, §7), not a wait for a musical bar line, since combat's mechanical clock
   does not wait for music either. The full region theme resumes, from wherever real time has
   carried its loop position forward to, the instant the encounter ends.
8. **A creature transitions Healthy → Blocked → Starved (or any reordering) mid pulse-cycle.** The
   idle soundscape re-evaluates each creature's audio behavior once per grid slot (§4 Formula 2),
   never mid-slot — a state change is reflected starting at the *next* scheduled grid slot for that
   creature, never causing a glitch, an overlapping double-voice, or an audible cut mid-tone. This
   mirrors `creature-jobs-evolution-system`'s own once-per-tick evaluation cadence for the
   underlying state itself.
9. **`telegraph_audio_precue_enabled` is turned off by a player relying on it.** Identical
   resolution to `accessibility-settings-system` Edge Case #13, restated here for completeness: no
   special handling beyond a clearly labeled, easily re-enabled toggle — the visual telegraph pulse
   remains the guaranteed always-on primary channel regardless, so no player is ever left with zero
   signal even if this toggle is misconfigured.
10. **`mono_audio_enabled` or `audio_loudness_normalization_enabled` interacts with the telegraph
    pre-cue or the idle soundscape.** Both settings are binding constraints on this document's own
    mix design, not merely pass-through toggles to ignore: the pre-cue's Source-timbre content
    (§3.2) must never rely on stereo phase-cancellation tricks that could partially or fully cancel
    when summed to mono — every pre-cue voice is authored mono-compatible by construction (a
    binding authoring requirement, §7); and `audio_loudness_normalization_enabled`'s
    limiter/compressor (`accessibility-settings-system` §3.5) must never be allowed to smear the
    pre-cue's hard cutoff's transient edge — the pre-cue channel is exempted from that limiter's
    attack/release smoothing specifically at the cutoff instant (a sidechain-key exemption,
    engineering-owned but specified here as a binding requirement), so the "act now" instant stays
    exactly as sharp with normalization on as off.
11. **A bound creature's Role changes via evolution mid-session
    (`creature-jobs-evolution-system`).** The idle soundscape re-resolves that creature's Role
    voice and cadence the next time its `work_state` is evaluated (matching Edge Case #8's
    once-per-slot cadence) — no redesign or special-case audio asset is required, since Role
    voices are a fixed vocabulary of five (§3.6), not a per-creature asset.
12. **A freshly mastered region with automation unlocked but no team assigned yet.** Resolved
    explicitly in §3.6: the idle soundscape has nothing to generate and produces no work-pulse
    audio at all; the region's ambient theme (§3.7) continues to play alone. This is not a bug or a
    silent failure state — it is an honest, correct representation of "nothing is working yet,"
    matching Pillar 3's own framing that idle-with-no-team is a real, different state from
    idle-while-producing.

## 6. Dependencies

### Depends On

- **`creature-ai-telegraph-system`** (`design/gdd/creature-ai-telegraph-system.md`) — the
  telegraph's `t_eased` value (§4 Formula 2 there), the `TELEGRAPH_WINDUP_FLOOR_MS = 600` and
  `TELEGRAPH_MIN_RESET_GAP_MS = 150` constants (§3.8 there), the six-value `Source` enum, and the
  §3.9 timing contract this document's Formula 1 satisfies. That document's §6 already lists
  `audio-system` in its own "Depended On By" table — bidirectionality confirmed.
- **`combat-encounter-system`** (`design/gdd/combat-encounter-system.md`) — the exact combat event
  list this document's §3.4 consumes (basic attack fire, ability/ultimate cast, block/dodge/
  interrupt success or failure, part-break, Kill/Capture/Retreat outcome and their `RESOLVING`
  durations, §3.9 there), and the defensive triad's reward-gradient mechanics (§3.5 there). That
  document's §6 already lists `audio-system` — bidirectionality confirmed.
- **`region-mastery-automation-system`** (`design/gdd/region-mastery-automation-system.md`) — the
  Mastery Transition's mechanical sequence and its 3000ms boss `RESOLVING` window (§3.7 there,
  cross-referencing `combat-encounter-system` §3.9), `region_state`, and the Blocked/Starved
  work-state input signals it supplies to `creature-jobs-evolution-system` (§3.4 there). That
  document's §6 already lists `audio-system` — bidirectionality confirmed.
- **`creature-jobs-evolution-system`** (`design/gdd/creature-jobs-evolution-system.md`) — the
  Healthy/Blocked/Starved `work_state` machine (§3.5, Formula 6 there) and the per-Role work-tick
  cadence (`work_tick_interval_seconds`, §7 there: Crafter 8s, Attacker/Support 15s, Defender 25s,
  Producer 40s) this document's Formula 2 is built directly against. **Bidirectionality gap,
  flagged not self-edited (A8)**: that document's own §6 "Depended On By" list does not currently
  include `audio-system`, unlike the other three dependencies above. Documented here for a future
  centralized pass, following the exact precedent `onboarding-tutorial-system.md` already set for
  the same kind of gap against `creature-ai-telegraph-system.md`.
- **`accessibility-settings-system`** (`design/gdd/accessibility-settings-system.md`) — the five
  locked volume buses (§3.1, §3.5 there), the `telegraph_audio_precue_enabled` toggle (default on,
  §3.4 there), `mono_audio_enabled`, and `audio_loudness_normalization_enabled`. That document's §6
  already lists `audio-system` — bidirectionality confirmed.

### Depended On By

None. This is a leaf system — confirmed by direct read against every dependency above (four of
five already list `audio-system` in their own tables; the fifth's gap is flagged in A8, not
silently assumed).

### Adjacent Systems (informational, not a dependency in either direction)

- **`design/art/art-bible.md`** — §§2–4 (mood, shape/edge-quality grammar, color system) are this
  document's entire visual-to-audio translation source (§3.2, §3.5, §3.7); §5.3 (work-loop spec)
  and §7.7 (accessibility requirements) are restated, never redefined, throughout §3.6 and §3.9.
- **`design/gdd/game-concept.md`** — the audio-needs framing ("Moderate — telegraph/weak-point
  audio cues are functionally important, not just decorative, plus ambient region themes") this
  document's Overview and Player Fantasy directly answer.

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| **Telegraph pre-cue timing contract itself (§3.3, §4 Formula 1's use of shared `t_eased` and same-instant hard cutoff)** | — | **NON-TUNABLE.** Locked to `creature-ai-telegraph-system`'s visual curve, exactly like that document's own `TELEGRAPH_WINDUP_FLOOR_MS`. No setting, difficulty scale, or content author may desync it. | Accessibility parity between the audio and visual channels. |
| `precue_pitch_floor_semitones` / `precue_pitch_ceiling_semitones` | `0` / `12` | `6–19` (ceiling only; floor stays `0`) | How far the pre-cue's pitch rises across a wind-up. Wider = more dramatic, but must never exceed a comfortable listening range. |
| `precue_gain_floor_db` / `precue_gain_ceiling_db` | `−18` / `0` | floor `−24` to `−12`; ceiling fixed at `0` | The pre-cue's own internal loudness envelope, pre-bus-fader. |
| `precue_pulse_interval_start_ms` | `150` | `80–300` | Perceived urgency at telegraph onset — lower start = a more anxious opening. |
| `precue_pulse_interval_floor_ms` | `40` | `25–80` | Maximum pulse speed near cutoff — must stay resolvable as discrete beats, not a solid tone (below ~20ms risks fusing into a buzz). |
| `grid_slot_ms` | `250` | `150–400` | Idle soundscape's rhythmic quantization grid — smaller = tighter but more collision-prone; larger = looser but safer against cacophony. |
| `precue_jitter_max_ms` | `20` | `0–40` | Idle soundscape's anti-robotic randomization — must stay well below `grid_slot_ms/2`. |
| `max_concurrent_work_pulse_voices` | `8` | `4–16` | The idle soundscape's hard polyphony ceiling — directly trades density-of-detail against cacophony risk. |
| `duck_db_per_doubling` | `3 dB` | `2–6 dB` | How aggressively simultaneous idle voices duck each other. |
| `tier_duck_db` (mix priority ducking, §3.8) | `−6 dB` | `−3 to −9 dB` | How strongly a higher-priority tier suppresses lower tiers. |
| Combat neutral-bed suppression crossfade | `300–500 ms` | `150–800 ms` | How abruptly the region theme drops to combat-neutral on `ENGAGING` (§5 Edge Case #7). |
| Part-break hit-stop duration | `80–120 ms` | `50–150 ms` | The freeze-frame the part-break payoff rides on (owned jointly with whichever system implements hit-stop). |
| Mastery Transition phase proportions | `25% / 60% / 15%` of the 3000ms boss `RESOLVING` window | held-breath `15–35%`, swell `50–70%`, settle `remainder` | The arc's pacing — the swell's outward-only direction rule itself is non-tunable (§3.5). |
| Bus routing (`volume_music` / `volume_sfx` assignments, §3.8) | as specified (A4) | **NON-TUNABLE without `technical-director` sign-off** — reflects `accessibility-settings-system`'s already-locked 5-bus architecture, per this agent's own constraint against altering audio middleware without approval. | Which slider controls which content category. |

## 8. Acceptance Criteria

### Mechanically Critical (telegraph pre-cue)

1. **Audio-only block/dodge/interrupt.** A tester, with the screen off (or the visual telegraph
   disabled for test purposes only), can correctly time a block, a dodge, or an interrupt against
   a telegraphed attack using the audio pre-cue alone, at both the `TELEGRAPH_WINDUP_FLOOR_MS`
   (600ms) tier and at least one longer (≥1500ms) tier, across a minimum of 20 trials per tier at
   ≥80% success — matching the accessibility bar `creature-ai-telegraph-system` already sets for
   the visual channel's own worked-example proof.
2. **Full completability with audio disabled.** A tester can complete a full encounter loop — an
   engage, a telegraphed attack correctly avoided, a part broken, a Kill — with `volume_master = 0`
   for the entire session, with zero loss of mechanically necessary information, verified against
   §3.9's visual-equivalence table.
3. **Same-instant hard cutoff, zero tail.** An audio-analysis pass on a captured pre-cue confirms
   both the tone and pulse layers, including any reverb/delay send, reach full silence within one
   audio buffer of `elapsed_ms = windup_duration_ms`, with no measurable decay tail beyond that
   buffer.
4. **Idle soundscape polyphony cap holds under adversarial load.** In a region with 20 assigned
   Healthy creatures, including a worst-case scenario of ≥10 creatures sharing an identical
   production cadence and offset, an audio-event log confirms no single grid slot ever produces
   more than `max_concurrent_work_pulse_voices` discrete voices, for a minimum of 200 consecutive
   grid slots.
5. **Blocked/Starved detectability.** In a region of 20 Healthy creatures plus one Starved (and,
   separately, plus one Blocked) creature, a listening test (or an onset/spectral-analysis
   substitute) confirms the Starved creature's off-grid distress motif is audibly distinguishable
   from the Healthy ambience bed, and the Blocked creature's missing on-grid pulse is detectable as
   an absence within its Role's own cadence window, in both cases without reference to the visual
   channel.
6. **Source timbre distinguishability.** A blind A/B listening test across the six telegraph
   pre-cue voices (§3.2), stripped of any visual context, achieves ≥90% correct Source
   identification across a panel of at least 5 listeners after a single short training pass —
   confirming the six timbres are instantly distinguishable even before considering overlap.
7. **Region theme Source parity.** A listener familiar with §3.2's telegraph voices can correctly
   identify a region's dominant Source from its ambient theme alone, at better-than-chance
   accuracy, confirming the shared-palette design (A5) actually reads as one idea across both
   contexts.
8. **Mastery Transition direction test.** A spectral/stereo-field analysis of the Mastery
   Transition's swell (§3.5, Phase 2) confirms stereo width, reverb send level, and high-frequency
   content all increase monotonically across the phase — never narrow, dampen, or darken at any
   point — matching the visual's non-negotiable outward-only rule.
9. **Zero pre-cue/swell overlap.** Triggering an encounter-ending killing blow mid-telegraph (a
   deliberate test case) confirms the pre-cue's hard cutoff and the Mastery Transition's opening
   held-breath beat begin in the same audio frame with no audible bleed or double-signal.
10. **Mix independence.** Independently varying `volume_music` and `volume_sfx` (with `volume_master`
    fixed) confirms a player can raise the telegraph pre-cue's relative prominence against the
    region theme, or vice versa, without needing any additional slider — verifying A4's bus-routing
    claim directly.
11. **Mono-sum safety.** Enabling `mono_audio_enabled` and re-running Acceptance Criterion #1's
    trial set produces no measurable drop in pass rate, confirming no pre-cue voice relies on a
    stereo effect that cancels in mono.
12. **Ultimate/Tier-0 coexistence.** Triggering an Ultimate activation while a telegraph pre-cue is
    simultaneously active confirms both remain at full, undimmed prominence per §3.8's tier rule,
    with no audible masking of either.

---

## Registry-Worthy Items (informational — not written to any file this session)

- **New GDD authored**: `design/gdd/audio-system.md` (this document). Recommend adding a row to
  `design/gdd/systems-index.md` if not already present in a form matching this document's final
  scope (an "inferred" row already exists at index #23; its `File` column currently reads `—` and
  should be updated to point here).
- **Cross-document consideration for `design/gdd/creature-jobs-evolution-system.md`**: its own §6
  "Depended On By" table does not yet list `audio-system`, unlike every other document this GDD
  depends on. Flagged in this document's §6 (A8) as a bidirectionality gap for a future
  centralized pass — not self-edited here per this session's single-file-write constraint.
- **Cross-document consideration for `design/gdd/systems-index.md`**: the same gap applies there —
  row #23's `Depends On` column currently lists only `combat-encounter-system,
  region-mastery-automation-system`, omitting `creature-ai-telegraph-system`,
  `accessibility-settings-system`, and `creature-jobs-evolution-system`, all four of which this
  document establishes as real dependencies.
- **Forward note for whichever system implements hit-stop** (not yet named in any GDD read this
  session): §3.4's part-break payoff and §7's tuning knob both specify an 80–120ms hit-stop this
  document's audio rides on; whichever system owns hit-stop implementation should reference this
  document by name when authored.
- **Forward note for whichever system authors the Mastery Transition's cinematic keyframes** (not
  yet named in any GDD): §3.5's audio arc is specified in proportional phase timing, not
  frame-locked; the glyph-inversion sync chime needs a concrete keyframe handshake once that system
  exists.
