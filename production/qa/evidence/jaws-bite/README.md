# JAWS, a frontal Shadow bite: the review package (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/4a9WdmEBai3Q4bCWkNZaqP

**The owner's review** of the side-view maw, in Turkish: "Hayır, yanlış anlamışsın. Sen yandan ısıran kötü bir çene
çizmişsin. Benim dediğim yine önceki perspektif ile önden ısıran ama daha efektif ve güzel bir çeneydi." In English:
no, you misunderstood. You drew a bad jaw biting from the side. I meant the earlier perspective, biting from the front,
but a more effective and beautiful jaw. The owner gave a reference: Roni Kangaskorte's "Bite VFX" on ArtStation
(https://www.artstation.com/artwork/WBB86N).

**The reference**, read frame by frame at 30 fps: an upper crown of fangs appears, with two long curved canines at its
corners and smaller leaf-shaped teeth between them. A smaller lower row appears below it, wide apart. The rows hold,
charging their colour from white to pink to red and orange. Then they slam together. On the impact the teeth vanish
into a bright star flash with a coloured diagonal slash, a ring, radial speed lines and flying shards, which fade out
over about 330 ms.

**What is in the build.**
- **Eight parts, composed at runtime.** `tools/asset-pipeline/v2/seeker_bite.py` draws them procedurally from one seed,
  with no PixelLab. The upper crown has two canines that bow out and hook their points in, and five packed leaf-shaped
  teeth that overlap. The lower row has four leaf-shaped teeth whose points rise between the upper points, and a taller
  tooth at each end that the canines close outside of. Each row comes in six states, from mist to crisp. The impact
  parts are a flash with six fat rays, a slash, a ring, speed lines, torn splinters and a smoke haze. All parts are
  white or grey, and the recipe tints them along the Shadow palette.
- **Where it bites.** The bite sits on the front of the creature, where its head is, because every creature faces the
  Seeker. The open rows frame the head: the crown sits below the creature's health bar and the lower row above the
  skill dock. When one answer bites two creatures, the front one gets the full bite. The other gets a smaller bite on
  its own head, snapping at the same moment, whose impact is only a flash and splinters.
- **Layers.** The teeth, their glow, the splinters and the haze are drawn over the creatures and under the Seeker. In
  HARD HANDS she stands in front of the teeth. The flash, the slash, the ring and the speed lines are light and are drawn
  over everything.
- **The phrase**, in ms after the first frame that shows the bite:

| ms | what happens |
|---|---|
| 0–100 | the rows condense out of mist over six frames, pale lavender-grey, at 80 % strength |
| 100–317 | the teeth charge through violet to magenta and grow to full strength |
| 150–233 | the rows part a quarter further, the wind-up before the slam |
| 233–317 | the rows slam together, accelerating; the largest step is the snap frame itself |
| 317 (333 on screen) | SNAP: the rows interlock white-hot, the brightest moment; the bite sound; a kill's fall |
| 317–350 | the teeth break into the impact; the flash swells and holds for two frames, then shrinks to a hot point before it fades; a magenta slash strikes across it, longer than the flash |
| 367 (383 on screen) | the number appears above the creature |
| 317–677 | the ring grows to well past the crown while still bright, the speed lines burst out, the splinters fly out with the ring and fade last, a violet haze spreads behind |

- **Unchanged:** the skill-id recipe, the real reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering, the SPRAY and HARD HANDS overlaps, and the one bite cue. The creature's own sprite still
  never flashes, and the number still comes after the snap. A critical answer also plays the game's general critical
  cue with its number, as every skill's critical does. Each target draws at most 10 sprites, and the trace records 0
  allocations. 806 game tests and 1888 Core tests pass, and the asset-consumer gate passes. The side-view maw's strip
  moved to `tools/asset-pipeline/v2/keypose_sources/history/`.

**Verified** by two judge rounds and a confirmation round against the reference frames. Each round had vision judges
at close and gameplay scale and an adversarial code review. The first rounds found these defects:
- the teeth read as crystal spikes
- the bite fell between two heads of the pack
- the fangs appearing were brighter than the snap
- a one-frame condensation that read as a pop
- a wind-up too small to see
- an impact smaller than the jaw
- no slash
- splinters that faded early
- a dark puff lost against the pack
- a fused double crown when two creatures were bitten
- the glow drawn over the champion
- smaller code issues

The confirmation round found these, all fixed too:
- the slash hidden under the star's own rays
- the flash's tail a big grey star
- the ring still inside the crown while it was bright
- a skipped condensation state
- a glow that whitened the formed teeth
- a delayed second bite whose creature fell before its own rows shut

Each was fixed, and each one a test can hold is pinned by a test.

**Known on this recorded bite:** the pack's own lunge art ends between +116 and +150, and it switches to its next pose
near +500. The previous builds' takes do the same at those moments; JAWS does not cause it. The large red ring that
pulses around the Seeker is her Aura, not JAWS.

| # | File | What |
|---|---|---|
| 1 | `01_*` | PREVIOUS: the side-view maw, true speed, sound |
| 2 | `02_*` | NEW: the frontal Shadow bite, true speed, sound |
| 3 | `03_*` | new, muted |
| 4 | `04_*` | close crop, true speed |
| 5 | `05_*` | frame sheet: MIST APPEARS, CONDENSING, FANGS FORMED, CHARGING, WIND-UP, CLOSING, SNAP, IMPACT, BURST, FADING, SPLINTERS LAST (new rows over previous rows) |
| 6 | `06_*` | repeated JAWS at fast tempo; some answers bite two creatures, one bite each |
| 7 | `07_*` | during SPRAY |
| 8 | `08_*` | during HARD HANDS |
| — | `09_trace.md` | the playhead trace: the snap and cue at +333, the number at +383, 10 sprites at most, alloc 0 |

The reference's own frames are not stored here; the review page shows them beside ours.

**Acceptance.** The first true-speed viewing of film 02 reads as Shadow fangs, seen from the front, that form out of
mist, charge, bite that creature and burst.
