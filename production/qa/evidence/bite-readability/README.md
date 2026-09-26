# Enemy attack readability + hit feedback foundation: evidence (2026-09-26)

**A study only. Nothing ships.** Every change in `src/` is a capture fixture, inert without its environment variable;
no Core result, no SPRAY, no HARD HANDS, no production draw path changed. JAWS stays paused; Concept D is not built.

**The fight.** One seeded fight (`RH_SHOT_SEED=7`), the JAWS review's: the Seeker against four GLOOM WHELPS in Umbral
Reach, normal TEMPO. Every normal-tempo take is 99 frames from ~6500 ms: the Seeker's swing lands on the front whelp
at 6700 (150), the pack bites at 7000, JAWS' answer lands on the same frame. The reaction layer is OFF, so the answer
is the reflected number and flash on the bite's frame; the legacy row ring was held out of the build output for the
takes (restored, byte-identical). In the attack takes (Part A/B) the answer is hidden (`RH_SHOT_NOANSWER=1`) so the
whelp's own act is what is on screen.

| File | What |
|---|---|
| `00_current_bite_timeline.md` | the current bite measured: what Core's bite is, how the clip is timed, what each frame does |
| `01_bite_current_views_grid_MUTED.mp4`, `01a`-`01c` | the bite as it is: tint + effects / no tint / bare / silhouette, true speed |
| `01d`, `01e`, `01f` | every frame at play size (bare, silhouette), and the front whelp's body metrics per frame |
| `02_*`, `02b`, `02f`, `02c`-`02e` | the prototypes, bare: A key poses (feet fixed), B key poses + lunge, B front-led, lunge-only, B with twice the travel |
| `03_*`, `03a`-`03c` | the same with tint and effects on |
| `04_*.png`, `04b` | every frame at play size: current, A, B, B x2; the strip current vs prototype |
| `05_*`, `05F0`-`05F3`, `05b` | the flash: F0 current / F1 short / F2 low shaped / F3 rim + accent, on the dark whelp, true speed and 4x close |
| `06_*.png` | every frame of the swing's flash and the answer's flash under F0-F3 |
| `07_*` | the flash on a light body (Choir swarm strips standing in). The boss could not be posed: the boss fixture dies on the first blow, before the rig films its first frame |
| `08_*`, `08F0`/`08F2`/`08F3` | repeated hits at fast TEMPO under F0-F3 |
| `09_*` | the optional 50 ms pose hold beside no hold (never on by default) |
| `10_*`, `10b`-`10d` | the best attack + the best flash (B + F2), the answer on; current vs best; repeated |
| `11_*`, `11A`-`11C` | the JAWS concepts A/B/C (unchanged) over the best plate |
| `12_blind_reads.md` | the blind reads: who saw what |

**Review page:** https://claude.ai/artifact/SVooAk2RdeRUEYpUKksnPV

**Reproduce:** `prototypes/bite-readability/README.md`.
