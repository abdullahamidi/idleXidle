# JAWS identity pass: the blind semantic check

The owner's check (identity brief §19): show the world prop by itself, with no skill name, no icon, no particles and no
Source colour, and ask **"What object is this?"** The intended answer is roughly "trap", "clamp" or "mechanical snare".
"Mouth", "robot head", "crocodile" or "dinosaur" means the art still fails.

## How it was run

- **Readers.** Each image went to a FRESH agent with no project context: Claude Sonnet, Opus or Haiku, one image per agent.
  - The file had a neutral name (`obj_N.png`).
  - The reader was told to read only that file and search nothing.
  - The prompt never says trap, clamp, jaws or chain.
- **Questions.**
  1. What object is this? (first impression, 1 to 4 words)
  2. Up to three alternative readings.
  3. Does any part look like a face, an eye, a mouth, a skull or an animal's head?
  4. Which shape features drove the reading?
- **Images.** Two states, open and shut, on the arena's floor tone.
  - Top row: play size, about 70 px long.
  - Bottom row: the same pictures enlarged 3×.
  - Iron colours only: no particles, no Source light.
  - Images: `24_blind_check_images_current_vs_identity.png`.
- **Limits.** A model's first reading stands in for a player's glance. It is a repeatable proxy, not a playtest. The
  owner's own look is the real check.

## The control: the CURRENT (polish-pass) head, in colour

| Reader | First impression | Alternatives | Face / eye / mouth? |
|---|---|---|---|
| Sonnet | "a pair of chomping jaws / mandibles" | bear-trap jaw, crab claw, Venus flytrap head | "Yes: it reads as an animal head. The small dark circle is an eye, and the two curved tooth-lined arcs form a mouth" |

This confirms the owner's diagnosis, so the method can tell a head from a device.

The same head as a pure black silhouette read as "open pincer jaws / bite trap". The crocodile does not come from the
outline alone. It comes from the value pattern: the round lit hub sits where an eye would be, and the long shaded jaws
read as a snout. The final check is therefore done in colour.

## The three silhouettes, before any pixel polish (`20_silhouettes_A_B_C_at_play_size.png`)

| Candidate | First impression | Alternatives | Verdict |
|---|---|---|---|
| A: compact bear-trap clamp (a base plate with two hinge knuckles, D-shaped toothed jaws, a leaf spring, a shackle) | **"bear trap jaws"** | animal skull or jawbone, crescent pincer, open trap or mouth | **chosen** |
| B: industrial spring clamp (crossed levers, coil at the pivot, pads) | "ninja throwing star (shuriken), spinning" | crossed daggers or scissors, rotor, claw | rejected |
| C: chain mantrap (a round spring drum, one pivot, crescent arms) | "crab claw / pincer" | bear-trap jaw, open mouth with teeth, C-clamp | rejected (a creature part) |

## Iterating A in colour (every round with fresh readers)

| Round | What changed | Readings (first, then alternatives) | What it taught |
|---|---|---|---|
| A1 (a V yoke and a coil rod behind the plate, round knuckles) | the first drawing | Sonnet: "crossbow / mechanical claw trap", with **"eye socket"** at a knuckle notch. Opus: "mechanical claw (grabber)", crossbow second | The V struts plus the ribbed rod read as a crossbow stock, and a round knuckle over the C read as an eye |
| A2 (a riveted spring box, the coil in a side window) | not sent to readers | our own look: the small window with vertical coil bars read as a **robot's visor** | never put a slot of bars on the box's face |
| A3 (the coil moved OUTSIDE along the box's top, a vertical rivet strap, square hinge brackets) | a box, no rod | Sonnet: "mechanical claw / pincer", **no face**. Opus: "robotic claw, mechanical grabber", weakly mouth-like arms | a machine first |
| A4 (bigger, hand-placed, interleaved teeth; a 2×2 dark pin in each bracket) | clean teeth | Sonnet: **"bear trap / clamp jaw"**, with the pin as a possible **"eye socket"**. Opus: "robotic claw / gripper". Haiku: **"mechanical trap jaw"** | the dark square pin in a lit square is an eye |
| **A5 FINAL** (the pin is a single small dark rivet in a plain flange) | this pass's art | see below | |

## The FINAL prop (`obj_10`)

| Reader | First impression | Alternatives | Face / eye / mouth? |
|---|---|---|---|
| Sonnet | **"mechanical bear trap / clamp jaws"** | robotic pincer claw, stylised crab pincer, sci-fi trap device | "the gap between [the arms] reads mouth-like", and "the small rounded top piece could pass as an eye or head knob"; "a hinged trap/jaw shape rather than a face" |
| Opus | **"a robotic claw or grabber"** | a pincer trap or clamp on a metal housing, beetle or ant mandibles, horseshoe magnet | "Yes, weakly": the serrated arms could read as an open mouth or mandibles. "The boxy block doesn't read as a head, and I see no eye or skull… reads as a machine part more than a creature" |
| Haiku | **"trap with opening jaw"** | claw or gripper device, lobster trap, mechanical scissors | the arms "suggest an opening/closing jaw or bite mechanism" |

## Verdict

- **Every first impression is now a device:**
  - "mechanical bear trap / clamp jaws";
  - "robotic claw / grabber";
  - "trap with opening jaw".
- **None is a mouth, a robot head, a crocodile or a dinosaur.** The control was "chomping jaws / mandibles" with an eye.
- **Two of three name the intended object** (bear trap / clamp; trap). The third names a close mechanical neighbour, a
  claw or grabber, and lists "a pincer trap or clamp on a metal housing" second.
- **A weak residue remains, and it is inherent.** An open C made of two toothed arms still reads a little like a mouth
  or mandibles to some readers. Any two-armed clamp seen in profile has an opening. In play, the caught limb fills that
  opening: the arms close on the creature and the gap is its body. See `26_in_game_current_vs_identity_shut_on_the_limb.png` and the close-crop films (`02a`, `02c`).
- **The owner's eye is still the check.** This record is supporting evidence, not the acceptance.
