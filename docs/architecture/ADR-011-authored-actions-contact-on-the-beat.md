# ADR-011 — An action is performed, and its contact is the beat

| Field | Value |
|---|---|
| **Status** | Proposed. The Seeker SPRAY slice is built (2026-09-24), and its art, animation and audio polish pass is filmed (`production/qa/evidence/action-presentation-slice/polish/`). It stays Proposed until the owner has watched the polish films and the three SPRAY cues have passed a listening test (`design/audio/seeker-spray-audio-brief.md`). No other action uses it until then. |
| **Date** | 2026-09-24 |
| **Deciders** | user (approved the discovery; decided one knife per struck enemy, a physical knife scale, CONTACT on the beat, and a thrown-blade travel) + lead-programmer, technical-artist |
| **Related** | ADR-009 (the light every emissive layer draws through), ADR-010 (the projectile composite this reuses), `production/qa/evidence/action-presentation-audit/` (the measured problem) |
| **Enforced by** | `tests/unit/IdleXIdle.Game.Tests/action_presentation_test.cs` |
| **Scope** | champion actions that have a recipe in `ActionRecipes` AND a timing file beside their strip. Today: the Seeker's `projectile` Form (SPRAY). |

## Summary

The fight still resolves every hit at its beat, and nothing about that changes. What changes is the presentation around the beat:

- **Contact on the beat.** A recipe makes the champion's clip start early enough that the thrown object leaves the hand one flight before the beat. It then lands **on** the beat, on the same frame as the fight's own hit feedback: health, flash, number and death.
- **Authored frame timing.** The clip plays by its own timing file: per-frame durations, named markers, and hand sockets.
- **Sockets.** A socket is a point on a frame, turned into an arena point with the same transform that draws the sprite.
- **The same object in the hand and in the air.** The held prop and the flying object are one texture, drawn at one scale.

## Context

The audit (`production/qa/evidence/action-presentation-audit/`) measured the Seeker's SPRAY frame by frame:

- **Target feedback came early.** The enemy flashed, lost health, showed its number and made its hit sound 683 ms before the knife arrived.
- **One knife for many hits.** SPRAY hit four enemies, but one knife flew.
- **No release.** The throw clip kept the knife in the rear hand and punched with the empty one.
- **A knife from nowhere.** The projectile appeared in front of the torso at about 5× the hand knife's size.
- **Coasting.** The knife arrived at 33 % of its launch speed.
- **One contact frame for every clip.** Every clip of every champion contacted on frame 5 of 8, with equal frame lengths.
- **Early ending.** The clip cut to a random idle frame before the knife landed.
- **Scattered clocks and generic sound.** Presentation ran on five clocks, using three generic sounds.

## Decision

1. **Contact is the beat.** For a damaging projectile, the fight's event timestamp is the moment the object lands.
   - `ActionPerformance.Schedule` starts the clip at `beat − travel − time-to-release`, so the release lands `TravelMs` before the beat.
   - A late start compresses only the clip's **elastic** frames before the release. The release is never slowed.
   - **A recovery gives way to a performed wind-up** (polish pass). A plain clip whose blow has already landed is only recovering. It yields the figure on the last frame the performed cast can still start with its minimum wind-up (`ActionPerformance.MinLeadMs`: the flight, the rigid frames, and the elastic frames at their 35 % floor).
     - Without this, a fast TEMPO build's clips ran on to ~100 ms before the next beat. SPRAY committed at its release: no wind-up, and a ~100 ms flight.
     - The timing model is unchanged. Only which clip holds the figure changes. A clip that has not landed its blow is never cut, and an authored clip is never cut.
   - Melee actions (future recipes) put their contact frame on the beat. Non-damaging actions may name another semantic marker.
   - There is **no** deferred-health or deferred-death buffer. Because contact is on the beat, the fight's feedback is already at the right time.

2. **Authored timing** (`ActionClipTiming`, `<strip>.clip.json`):
   - per-frame durations;
   - elastic flags;
   - markers (`anticipation`, `commit`, `release`, `recovery`, `settle`);
   - sockets per frame.

   **The follow-through outlasts the flight.** The release frame plus the follow-through frame equal `TravelMs` plus one or two 60 fps frames. The open hand is still reaching toward the pack on the frame the knives land, and the recovery starts just after. The SPRAY: 50 + 230 ms.
   - The first build ended the follow-through 67 ms before the contact, so the arm was already down when the blades hit.
   - Exactly `TravelMs` was tried next, and the arm dropped on the contact frame itself.

   A strip without a timing file keeps the old uniform 5/8 behaviour exactly. The clip's last frame is the idle's first pose, and the idle then **restarts from frame 0** (`_anim − _idleFrom`) instead of resuming at a random phase.

3. **Sockets** (`ActionSocket`, `ActorSocketMap`):
   - A socket is a fraction of the square strip frame plus the direction a held prop points.
   - It is converted with the `Src`/`Dest` of the exact `SpriteFrame` the draw resolves.
   - `RH_SHOT_SOCKETS=1` draws the live socket and every blade's first position.
   - A later bone rig replaces the lookup, not the recipe.

4. **The performance** (`ActionPerformance`, one at a time, on the fight **playhead**):
   - reads the struck enemies from the fight's own resolved events (`ActionTargets.StruckBy`);
   - draws the held bundle at the hand socket until the release;
   - at the release marker, starts one blade per struck enemy from the bundle's fan positions;
   - flies them with a near-constant thrown-blade profile (`ProjectileMotion.ThrowPosition`, 6 % departure shaping) that bows apart so the fan reads;
   - lands them all on the beat.

   It runs **before** the frame's events are crossed, so the blades land on the frame the hits are presented.

   **The release accent** is a short smear. It shows only the last 18 % of the hand's path from the coil (`SmearTail`), lifted over the head (`SmearLift`). It starts ON the release frame and fades over 90 ms. The first version drew the whole path from the coil, which ran through the head, and at combat size it read as a beam from the eye.

5. **Colour roles.**
   - The flying object is drawn in two layers: its **material** (steel, untinted, alpha-blended) and its **emissive** edge (Source-coloured light).
   - The trail, glint, sparks and contact are Source-coloured light.
   - The character motif is the object's shape: the Seeker's kunai-style throwing knife.

6. **Impact priority.**
   - A performed hit replaces the generic hit puff and the generic hit sound, because they describe the same blow.
   - The enemy's flash and number stay. The recipe sets the flash's strength (`TargetFlash`, 0.38 for a four-to-five-target fan), so the pack does not turn solid white.
   - The recipe also sets the flash's shape (`TargetFlashRise` 0, `TargetFlashMs` 130). A thrown blade has already arrived on its contact frame, so its flash peaks there and is gone in 130 ms.
   - The fight's usual flash rises over the first fifth of 200 ms, so a swing's white arrives inside the blow. That rise put the SPRAY pack's peak 40 ms after the knives and kept it grey for 200 ms, the largest change on screen after the hit.
   - The contact is directional: a hot slash carried through the target along the incoming line, a small flash, and slivers thrown mostly forward.

7. **One focal point.** While the champion is performing, an ordinary bite on him is drawn at 0.55 opacity. Its number is unchanged, and its sound is ducked (below).

8. **Audio** (`SoundBank.PlayFirst(keys, volume, pitch, pan, vary, lead)`):
   - Release and contact cues resolve from the most specific to the most generic: champion + action, then archetype, then the generic cue.
   - They are played by the performance at **its** release and contact, with a gentle pan (±0.3).
   - **One contact** sound plays for the whole fan. A fan of three or more adds at most two quiet **ticks** (`ContactTicks`, volume 0.2) at the OUTERMOST blades, 18 and 36 ms after the contact, panned to them. Five knives are heard as one contact with width, never as five impacts.
   - A performed cast plays no `sfx_cast` at the beat and no generic `sfx_hit`.
   - **The mix** (`DuckOthers`, `DuckTailMs`): from the release until 160 ms after the contact, every other one-shot plays at 45 % (`SoundBank.Duck`). That covers an enemy's bite, another skill's cast, a critical's cue and a death. The performance's own cues play as `lead` and are never ducked. It is a duck, not a mute, because those sounds are still combat information. `Game1.Update` resets the duck every frame, so leaving the hunt mid-throw cannot leave another screen ducked.
   - The SPRAY's own cues exist (`sfx_seeker_spray_release/_hit/_tick`, synthesized by `tools/asset-pipeline/make_action_sfx.py`). They are measured, but no one has listened to them.

9. **The callout at the release.** The skill's name is shown at the release: presentation only. The skill tile's pulse, the cooldown and every piece of state stay on the gameplay event.

10. **Key poses, not generated actions.** An important action's frames come from controlled key poses: PixelLab `animate_with_skeleton_v3`, where every frame is drawn from one reference image of the champion with the pose sent as a skeleton. They are assembled by `tools/asset-pipeline/v2/keyposes.py`:
    - with the champion's own idle transform (pixel-exact, so there is no pop at either end);
    - with a key done by the tool, not the generator;
    - with the timing file written beside the strip;
    - with every PixelLab job, accepted or rejected, recorded.

    Props are drawn deterministically (`seeker_knife.py`).

## Consequences

- **Timing** (polish pass, measured from the trace; `tools/asset-pipeline/action_timeline.py`). The fight's beat is t = 0. Everything that happens on it is presented on the first 60 fps frame after it (+17 ms). That frame carries the knives, health, flash, number and contact sound together.
  - Clip start −583 ms, anticipation −500 ms, extreme hold −383 ms (150 ms).
  - Release −233 ms: the pose, the sound and the blades on one frame. The flight is 250 ms.
  - Contact +17 ms. The follow-through holds through the contact frame.
  - The recovery starts about two frames after the contact; the idle restarts at +333 ms. The clip is still 920 ms (90/110/150/50/230/90/90/110).
- **Cost** (re-measured in the polish pass: unchanged for four knives; a real five-knife cast peaks at 85 sprites and at most 50 effects-pass draw calls). A four-knife fan peaks at 60 sprites. At its busiest frames (contact: slash, slivers and flash for every blade)
  the effects pass rises from 16–17 draw calls to at most 39, which is +22 including the one extra Begin/End of its light
  pass. The cause is texture switches per blade. Drawing each layer across all the blades (all trails, then all
  heads) would bring that down to about +6; nothing does that yet, and 39 is well inside the "low hundreds" budget.
  Its per-frame path is loops over fixed arrays (the blades, their trail rings and particles are allocated once, at the
  release); its allocations were not separately measured.
- **Switches:**
  - `RH_ACTION_RECIPES=0` plays every action the old way, for old-versus-new films.
  - `RH_SHOT_NOVFX=1` and `RH_SHOT_NOCHAMP=1` are the review views.
  - `RH_PRESENT_TRACE=1` logs release, contact and the performance's cost.

## ADR Dependencies

- **ADR-006 (Draw must not consume input).** The performance advances in Update, before the frame's events are crossed. Draw only reads its state.
- **ADR-009 (the premultiplied additive blend).** Every emissive layer (edge, trail, glint, contact) draws through `VfxBlend.Light` in a `VfxBlend.PremultipliedAdditive` batch opened by `VfxPlayer.BeginLight`.
- **ADR-010 (the composite projectile).** Its layers (trail, glint, sparks, directional impact) are reused. Its size rule (a share of the caster's height) is replaced, for performed actions, by the prop's own pixel scale.

## Engine Compatibility

MonoGame 3.8.4.1. It uses only `SpriteBatch` (Deferred; one extra Begin/End per frame while a throw is live), `SamplerState.LinearClamp`, `GraphicsDevice.Metrics` (trace only) and `SoundEffect.Play` with pan. No new engine API. The timing files are plain JSON copied by the csproj.

## GDD Requirements Addressed

None directly: this is presentation, and the fight's rules and numbers do not change. It serves the art contract (`design/art/arena-art-contract.md` §3.8) and the owner's action-presentation brief (2026-09-24).

## Known limits (reported, not solved)

- **Audio is unverified by ear.** The three SPRAY cues are synthesized candidates: measured, wired, and rendered into the review films from the trace (`tools/asset-pipeline/film_audio.py`). The capture rig is silent, and no one has listened to them on real hardware. `design/audio/seeker-spray-audio-brief.md` is the contract a replacement must meet.
- **The hand is an edit.** The release and follow-through hands are PixelLab `edit_image_pixen` results, taken only inside a hand box (the edits also redrew part of the body, painted three knives and redrew the other arm). The edit drew bare skin. The whole hand is recoloured to his glove, as he is gloved in every other frame, idle included. A first build gloved only the back of the hand, so for 250 ms he wore a fingerless glove. At combat size it reads as an open, flicked hand. At 3× the seam between the edit and the pose is visible.
- **Fast TEMPO snaps once.** The recovery cut happens on the last frame SPRAY can still wind up. On the RHYTHM + VOLLEY build, that frame falls between the swing's frames 6 and 7, and the figure snaps from the swing's low lunge to the upright ready pose in one frame. It is one frame, and it replaces a SPRAY with no wind-up and a 100 ms flight. Shortening the ready frame's floor would move the cut, but that is a timing-model change the owner ruled out for this pass.
- **Other actions.** HARD HANDS, the other Seeker clips and every other champion are untouched. The old strips still disagree about the Seeker's weapon.
- **The held field (fixed in the polish pass).** The pink rectangle was `fx_press`: a white weight slab that the field aura tinted and held behind him. It is now line art (`tools/asset-pipeline/v2/press_field.py`), awaiting the owner's look.

## Alternatives rejected

- **Release on the beat, feedback held until contact.** This was the discovery's first proposal. The owner chose contact on the beat, which needs no deferred health, death or number buffer.
- **An eight-frame generated throw.** This was the old clip. It is what put the knife in the wrong hand and punched with the other.
- **Drawing the knives into the character frames.** The flying object could then never be guaranteed to be the same object at the same size. The prop is a runtime sprite at the hand socket, so it is.
