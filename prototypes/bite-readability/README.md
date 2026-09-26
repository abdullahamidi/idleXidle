# Prototype: enemy attack readability + hit feedback foundation

**Status: concluded (2026-09-26). Findings below. Awaiting the owner's direction.**
- Every change in `src/` for this study is a CAPTURE FIXTURE, inert without its environment variable (the game never
  sets one). No Core result, no SPRAY, no HARD HANDS, no production draw path changed. Nothing here ships.

## Hypothesis

The JAWS concept study's control (no JAWS at all) showed that viewers do not see the creature attack: the bite reads
as "a red burst on the hunter", and the fight's full-white hit flash erases what is drawn on the struck creature.
Both are shared combat-language defects upstream of JAWS. The study asks:
1. With tint and effects removed, does the Gloom Whelp's BODY perform an attack? If not, is it the art (key poses),
   the spacing (timing), the root motion (travel), or all three?
2. Which hit-flash grammar confirms "THIS target was struck HERE" without hiding what struck it, and still reads on
   the darkest body in the game, a normal body and the boss?
3. Do the JAWS concepts A/B/C read better on an improved baseline (readable bite + a flash that does not white-out)?

## How to run

Build the game once (`dotnet build src/IdleXIdle.Game`). Then, from the repo root:

```
python prototypes/bite-readability/make_attack_strip.py build/shots/bite/proto   # the key-pose strip (deterministic edits)
bash prototypes/bite-readability/film.sh                                          # ~30 seeded takes -> build/shots/bite/
bash prototypes/bite-readability/render_concepts_on_best.sh                       # JAWS A/B/C over the best plate
PYTHONUTF8=1 python prototypes/bite-readability/build_evidence.py production/qa/evidence/bite-readability <blind dir>
```

The fixture switches (all capture-only; see the comment block by `ShotNoTint` in `HuntScreen.cs`):
`RH_SHOT_NOTINT`, `RH_SHOT_SIL`, `RH_SHOT_NOANSWER`, `RH_SHOT_FLASH=F1|F2|F3`, `RH_SHOT_BITE=root|rootfront`,
`RH_SHOT_HITSTOP=<ms>`, and `RH_SHOT_STRIP_FILES=key=path;...` in `AssetLibrary.cs`.

## Findings

Review page: https://claude.ai/artifact/SVooAk2RdeRUEYpUKksnPV. Evidence: `production/qa/evidence/bite-readability/`.

1. **The bite does not read because the body never strikes.** The whelp's attack strip is a crouch in place: the
   head sinks over frames 1-3, the contact frame equals the frame before it, the centroid never moves, and the only
   fast motion is standing back up AFTER the hit. The row's existing 40 px lunge starts ON the contact frame (a
   recoil grammar). The receiver does nothing: a centred red burst on the Seeker, no flinch, no direction. The
   Seeker's own 200 ms white flash covers the whelp's wind-up.
2. **Art, spacing, root motion and the receiver: all of them.** Key poses alone: "shuffle a hair forward". Poses +
   a 0.15-width lunge: "shuffled in place". Twice that: "crept a bit closer". Only with the lunge AND the F2 flash
   did a reader see "a small lunge, then the champion lit up red". Every reader: "he doesn't flinch".
3. **The flash: F2 (peak 0.45, ~80 ms) won.** F0 is a blank cut-out for ~100 ms and hides what is on the creature;
   F1 blinks; F3's rim idea is right but this prototype's double rim read as a white outline.
4. **JAWS on the improved baseline: half.** A and C now read as automatic (a ward going off by itself); the cause
   still does not, because the bite still does not. B still reads as a cast.
5. **Concept D is not yet justified.** First: a contact pose + a real approach for the whelp (front-led), and a
   directional hurt reaction on the Seeker; then Part D again.
6. The boss could not be posed (the boss fixture dies on the first blow, before the rig's first frame).
