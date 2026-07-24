# Asset Generation — Delta ("staff the warren")

A tiny, high-leverage batch. The Farm ("The Warren") now shows every creature as a sprite, but
creatures whose exact Source+Role has no art fall back to a flat coloured blob. **Five** generic
"worker" sprites — one per Role — fix that for the *entire* warren: the code tints each by the
creature's Source colour, so five files cover all 6 sources × 5 roles (30 combinations).

> Same style as every batch: 480×270 world, point-sampled, hard edges, locked palette. See
> [`asset-generation-prompt.md`](asset-generation-prompt.md).

---

## How the code uses these (important)

For each farm creature the game picks a sprite in this order:
1. A species-specific torso if one exists (the combat creatures — Whelp, Warden, etc.).
2. **Else `crea_worker_<role>.png`, TINTED by the creature's Source colour.** ← this batch
3. Else a flat coloured blob (the current fallback).

So these worker sprites must be drawn to **tint well**: draw them in **near-white / light grey with a
dark contour**, so multiplying by a colour (green for Nature, red for Body, indigo for Shadow, etc.)
reads as that element. Think "greyscale creature that the engine paints." Avoid strong local colour —
it will fight the tint.

---

## The five workers  →  `assets/art/creatures/`

**Transparent PNG, ~30×30**, single sprite each (no parts — these are simple standees, not the rigged
combat creatures). Light/greyscale body + hard dark outline, one small readable silhouette. The Role's
**mass distribution** should match the art bible so the silhouette *reads* as its job even before you
see the role glyph the UI stamps under it.

| File | Role | Silhouette direction (art-bible mass) |
|---|---|---|
| `crea_worker_attacker.png` | Attacker | Forward-leaning, a blade/claw mass at the front. Lean, aggressive stance. |
| `crea_worker_defender.png` | Defender | Broad, low, heavy — a shell/shield mass. Wide and planted. |
| `crea_worker_support.png` | Support | A core body with a small satellite/antenna offset — "tender/helper". Light. |
| `crea_worker_crafter.png` | Crafter | Many small prongs/tools — busy, fiddly silhouette. Compact. |
| `crea_worker_producer.png` | Producer | One swollen sac/belly mass — round, slow, generative. |

That's the whole batch. Drop them in `assets/art/creatures/` and every warren worker gets a proper,
element-tinted body next launch — no code change.

---

## Optional, lower priority

- **Species creatures for the other sources** (Body / Mind / Spirit) and the **Crafter** role, if you
  want the combat/farm creatures to be unique rather than tinted generics. Same part-set convention as
  [`asset-generation-gamma.md`](asset-generation-gamma.md) §3–4. Not needed — the workers above cover
  the warren; this is pure flavour.
- **`bg_arena_body` / `bg_arena_mind` / `bg_arena_spirit`** arena backgrounds, only if regions of those
  themes get added later (there are none today).

---

## Still outstanding from earlier batches

- **Audio** (batch 4, [`../audio/asset-generation-audio.md`](../audio/asset-generation-audio.md)) — the
  SoundBank and every cue are wired; it just needs the WAVs. Highest-impact remaining asset work.
