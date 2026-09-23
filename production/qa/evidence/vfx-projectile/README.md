# Projectile-motion pilot — `fx_seeker_projectile` (2026-09-23)

**Branch** `fix/vfx-fade` (not merged) · **Scope** the Seeker's projectile only · **Decision it tests** projectiles
need two motion layers: WORLD motion (the renderer flies the strip) and INTERNAL motion (the strip itself
visibly changes).

## 1. The runtime already animates projectiles (proof: `01_runtime_digit_proof.png`)

`cast.projectile` plays the strip at **8 fps**, and the flight (`Anim.Drift`) runs on the **same clock**
(`Life`), eased out. So the 8 frames play exactly once over the 1.0 s flight. To prove it rather than read
it, a throwaway strip whose frames are the digits 0–7 was filmed through SPRAY: **0 → 1 → 2 → 3 → 4 → 5**
appear in order, about 125 ms each, while the strip crosses the gap. Frames 6–7 sit inside the renderer's
tail fade and are effectively unseen. The committed strip was restored with git afterwards. **No runtime
change was needed.** The cohort knife looked static because its eight frames were nearly identical.

## 2–3. Candidates — 11 generations (4,365 → 4,354)

| Job | How | Verdict |
|---|---|---|
| `ccf65102` | open-ended: tumble + glint + speed streak | **Rejected**: stays level (turn 7.6°), and from frame 4 the blade tip bends and curls, so the object changes shape |
| `6948b14a` | `edit_image_pixen`: the same knife tilted ~45° point-down | used as B's pinned last frame (1 generation) |
| `7ea56119` | pinned: level → tilted | **Selected**: the same knife in every frame, tumbling. It went the long way round (nose up through vertical); about 110° is visible in frames 0–5 |

The shape is the cohort's knife (`cd704393`), so no new shape was generated.

## 4–5. Old vs new, true speed, same world trajectory (`02`–`05`)

- **World view (`02`):** the old strip is a sword-sized blade that glides without changing. The new
  one is a knife that **tumbles as it flies**: it lifts from level toward vertical while crossing the gap.
- **Frozen view (`03`, `04`):** a window that follows each projectile, so world motion cancels. **Old:
  the same picture in every frame. New: a visibly rotating knife.** Frozen at one world position, it is
  plainly alive.
- **Internal motion, measured on the strip over the six visible frames** (`fx_energy.internal_motion`,
  reported and never enforced):

| Strip | Silhouette change / step | Turn | Glint travel |
|---|---|---|---|
| old (pre-cohort) | 0.205 (its tail flicker only) | 0.7° | 0.236 |
| cohort knife `17f76b3d` | 0.072 | 1.4° | 0.027 |
| **new knife `7ea56119`** | **0.602** | **~110° seen** (the metric reads 150: the axis angle wraps at vertical) | 0.046 |

**What the player sees:** a real tumble, end over end in its first part. **What it does not have:** a
travelling glint and a speed streak. The pinned interpolation ignored that half of the prompt.

## 6. Trail

None was produced, so its effect on readability is untested here. The rotation alone carries the
motion.

## A consequence of the placement contract you should know about

A projectile is sized so its content box's HEIGHT is 0.28 × the hunter's height, and the content box is
the union of all eight frames. A level knife's box is only the blade's width, so the old knife was blown up
to ~320 px long, about 75 % of the hunter. Once the knife rotates, the union box grows tall and the whole
knife shrinks to fit: the new one is ~120–150 px (frame 376 → 174 px, ratio 0.74 → 0.34). That is closer
to a real thrown knife, but it is a visible size change. **Any readable rotation couples to size this way**
unless projectile sizing moves to a length basis, which is a runtime decision and was not made here.

## 7. Gates

EDGE 0 · SOFT 10.33 % · LIVE 883 (≥ 150) · no FLIGHT warning · 4096×512, 8 frames · lookup
`cast.projectile key=fx_seeker_projectile_strip8_512` · `-warnaserror` 0/0 · Core 1,850 · Game 712 ·
Integration 2 · `check_all.sh` green · library 56 of 67 failing (unchanged).
