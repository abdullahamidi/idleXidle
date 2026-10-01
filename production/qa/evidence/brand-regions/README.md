# BRAND, Concept A LIVING SHADOW CORRUPTION: the territory pass (ADR-011; owner's brief from 9aa407fd)

The curse is SEPARATE REGIONS OF THE BODY BECOMING CORRUPTED, never a vein network: depth 1 one infected territory,
depth 2 a second elsewhere on the body, depth 3 three or four. The host's own material changes in each territory. No
sound, the prototype pipeline, not accepted.

Review page: https://claude.ai/artifact/S68LvpL6kc5ah53pqmrGkG

| Part | What |
|---|---|
| Territories | `CursePrototype.TerritoryAtlas` (4 variants, 160 x 160): an irregular smoky region with satellite blotches (never a disc), a softer wider drain mask, 2-4 short vein fragments ending inside it, the dark mottled tissue, a restrained light from inside (thin violet fissures, tapered runs, a broken bright edge on one side), and its bloom in 6 frames outward from the infection's centre |
| Depth | `TerritoriesAt`: 1 / 2 / up to 4 (at least 3 where the body holds them); no persistent connection; a deepen's propagation is a brief shadow travelling under the skin, then the new territory blooms while the old ones react |
| Seats | `TerritorySeats`, from each host's own silhouette: in its mass (`Thickness`), apart (a gap of ~1 territory), never three in a row on an upright body, clear of the head band, the hem / feet and (upright) outside the core column (sleeves, wings, weapons); the first takes hold where the host VISIBLY CHANGES (`Visible`: the colour it loses), within reach of where the curse entered; a gentler pass guarantees a third on a thin body; LOCKED per creature at its first seating |
| Host material | `DrainPixel` / `DrainQuad`: a drained copy of the host (its colour gone, its folds flattened round a mean pushed AWAY from its own: a dark host to ash, a pale one darker), faded in through each territory's mask with the stock `DualTextureEffect`; `DrainStrength` from the host's colourfulness, native violet and darkness; a faint contamination tint, never violet on a violet host |
| Host rule | `Measure` / `HostLook`: brightness, colourfulness, native violet, once per strip; the light restrained on violet or dark colourless hosts; never a name |
| Motion | a wisp now and then from each territory, two at its bloom; a slow swell of the tissue; each territory's light breathes at its own pace; a dying host's territories flare, collapse and smoke out |
| Tests | `brand_curse_test` (12): depth = territories; apart and never in a row; a thin body still shows three; no long feature; a region never a band; light in tapered pieces never beads; a territory holds tissue and restrained light; the bloom grows outward and ends on the whole region; the drained material loses its colour, keeps its shading, pushes away from the host's mean; the host look; the pocket off a thin limb; the first territory where the host visibly changes |

Takes: `build/shots/brand/kB9` (this pass) and `build/shots/brand/kA5` (9aa407fd);
`PYTHONUTF8=1 python tools/asset-pipeline/brand_regions_evidence.py kB9 kA5 build/shots/brand/scale <out>` builds this folder
(the 14_* fresh-read sheets are kept).

OUTCOME: final fresh reads (6 readers, stills + true-speed clips, all nine hosts) "something affecting it": pale wisp 6/6,
Verdant bruiser 6/6, Thorn Regent 6/6, Crystal Lich 5/6 (no sash / stole read), Forge Colossus 5/6, black whelp 4/6,
Lumen Angel 3/6, Spirit Matron 1/6 (3 mixed: at depth 1 the haze sits where its claw hangs, "a spell"), Void Reaper
0/6 ("its own dark aura"); depth read as MORE OF THE BODY corrupted by every reader in every round. Open: dark
colourless casters (a different, non-violet damage cue would be a design decision); the head band on hosts with
antlers or raised wings (a territory can take the face).
