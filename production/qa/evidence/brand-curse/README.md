# BRAND as a CURSE: three direction concepts (ADR-011, the MARK reference; owner's brief 2026-10-01)

The owner changed direction: BRAND is no longer a sign on one body point but a persistent CURSE / SHADOW AFFLICTION the
enemy visibly suffers. This is a direction-selection pass: three concepts, filmed in real combat at every BRAND moment
(apply, idle, deepen, SPRAWL, transfer, mixed combat), on dark and pale hosts, a large bruiser and a boss. Not accepted,
no sound, not final art.

Review page: https://claude.ai/artifact/P7ZBWqXwpiihaMX9cDqYkN

| | Concept | The curse is |
|---|---|---|
| A | Living Shadow Corruption | dark-violet veins spreading under the skin from the seat, a slow internal beat, shadow wisps leaking |
| B | Withering Curse | the body drained toward ash, rot blotches and dry cracks, falling ash |
| C | Shadow Possession | a shadow mass moving inside; a dark double that does not fit the body, slamming in / tearing out |

Recommendation: C as the base (unprimed readers: "cursed", "a shadow blight, not an attack", "darkness seeping"; no
symbol read; the best transfer / spread story), keeping the double inside the body at rest and borrowing A's legible depth.

Code: `src/IdleXIdle.Game/Presentation/CursePrototype.cs` (dev-only, `RH_BRAND_CONCEPT=A|B|C`): BRAND's own timeline
from `MarkPerformance`; each afflicted creature's frame written into a stencil (stock `AlphaTestEffect`) so the curse is
clipped to its silhouette (the canvas gets a stencil only when a concept is set). Takes: `build/shots/brand/kA|kB|kC`
(`RH_BRAND_CONCEPT=X bash tools/asset-pipeline/films_brand.sh kX apply long etch etch_clean sprawl press light_etch
fam_nature_bruiser boss_void_reaper`); `PYTHONUTF8=1 python tools/asset-pipeline/brand_curse_evidence.py k
build/shots/brand/scale <out>` builds this folder. Game 845 / Core 1901 green.
