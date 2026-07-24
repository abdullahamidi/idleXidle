# Asset Generation Prompt — Resonance Hunter

Use this to generate art that matches the game's locked visual identity. Every value here is pulled
from `design/art/art-bible.md` and the running code — do not improvise palette, canvas, or filtering.

> **How to use:** paste the **Master Style Block** into any image-generation prompt, then append one
> **Asset Order** (a specific creature part, item, or icon). Generate at the stated pixel size on a
> **transparent background**. Import as PNG; the pipeline point-samples and integer-scales, so pixels
> must be crisp and 1:1 — never anti-aliased or upscaled.

---

## Master Style Block (paste this first, every time)

```
2D pixel art, flat indexed color, hard 1px edges, NO anti-aliasing, NO gradients, NO soft shading.
Transparent background. Designed for a 480x270 virtual canvas rendered at integer scale with
point/nearest-neighbor sampling — pixels must be crisp and readable at 3x zoom.

Strict 10-color-maximum palette per sprite. Every shape has an unbroken dark contour line in
Void Ink (#1B1620) — the contour is a LINE, never a fill. Read the shape by silhouette alone.

Locked palette (use ONLY these plus the one assigned Source color ramp):
  Void Ink        #1B1620  — every contour and seam line (never a fill)
  Bone Parchment  #E8DFC8  — neutral light, default/common surfaces, UI chrome
  Cold Slate      #57616F  — flattened neutral shading, "bones", broken/inactive states
  Hearth Gold     #F0A830  — EARNED states ONLY (mastery, Legendary, capture seal). Never decorative.
  Ember Threat    #D8483A  — danger and opportunity: vulnerability glyphs, telegraphs, threat
  Corruption Bloom #8B3F82 — sour violet "wrongness", corruption bleed only

Aesthetic: stark, legible, glyph-like. Think a tarot woodcut crossed with a clean 16-color
puzzle game — mechanical clarity over decorative rendering. Color is NEVER the only channel that
carries meaning; shape and value must carry it too.
```

---

## The two rules that make a creature "correct"

A creature's look is not free-form. Two axes are load-bearing and readable in under a second:

**Source = EDGE QUALITY + one color ramp.** Pick exactly one:

| Source | Edge quality of the contour | Color ramp (4 steps: highlight → base → shadow → deep) around |
|---|---|---|
| Body | Convex, smooth, symmetric rounded arcs; musculature bulges | Blood Crimson `#8C2E42` |
| Mind | Hard, faceted, crystalline straight cuts; mirrored halves | Crystal Cyan `#3FA9C9` |
| Nature | Branching, asymmetric thorns/forks/flares (non-repeating) | Moss Green `#5C8A3A` |
| Machine | Orthogonal, rigid right angles; stacked bolted plates | Rust Iron `#9C5A32` |
| Shadow | Torn, discontinuous; jagged bite-notches, frayed trailing edges | Umbra Indigo `#2E2438` |
| Spirit | Soft, dissolving; contour thins into tapering wisps at extremities | Wisp Lavender `#C7BFE0` |

**Role = MASS DISTRIBUTION.** Pick exactly one:

| Role | Silhouette massing |
|---|---|
| Attacker | Forward-weighted, asymmetric — a single leading point/blade-limb the mass leans into |
| Defender | Bottom-heavy, symmetric, broad base — widest at the base, thickest plating at center |
| Support | Two connected forms — a main body + a smaller satellite joined by a visible tether |
| Crafter | Compact core with many small manipulator-shapes; no single dominant limb |
| Producer | One dominant swollen sac/pod/hopper mass; highest volume-to-limb ratio |

So "a Nature Attacker" = branching thorny asymmetric edges, Moss Green ramp, forward-leaning mass
with a leading thorn-limb. That combination alone should identify it with the color turned off.

---

## Critical technical constraint: creatures are built from SEPARATE PARTS

The game animates creatures with a **cutout rig** — each body part is its own sprite, rotated about a
pivot at runtime. **Do not generate a single whole-creature image.** Generate each part as its own
sprite so the rig can move them. For the MVP creatures the parts are:

- `torso` (the core mass, ~26×14 px), `upper_arm` / `forearm` (a limb chain, ~22×8 and ~20×6 px),
  and the targetable parts the fight uses: `left_claw`, `right_claw`, `flank`, and `core`.
- The **`core`** is the vital center. Render it in **Corruption Bloom `#8B3F82`** for a hostile wild
  creature (the sour "still corrupted" read). It never breaks.
- Each part sits in its own contour so it reads as detachable — part-break is shown by the part
  turning to **Cold Slate `#57616F`** and cracking, never by hiding it.

Each part sprite: transparent background, its own Void Ink contour, pivot implied at one end (the
attachment point). Keep the pivot end squared so it seats cleanly against its parent.

---

## Asset Orders (append ONE to the Master Style Block)

### MVP creatures (Verdant Hollow — all Nature, Moss Green `#5C8A3A` ramp)

1. **Verdant Whelp** — Nature **Attacker**, tier 1. Small, forward-leaning, a single thorn-limb it
   leads with. Generate parts: `torso`, `left_claw` (a thorn-hook), `right_claw`, `flank`, `core`
   (Corruption Bloom). ~40px tall overall.
2. **Mossling** — Nature **Support**, tier 1. Main mossy body + a small tethered satellite spore.
3. **Bramblehide** — Nature **Defender**, tier 2. Bottom-heavy, broad thorned base, thick center bark
   plating.
4. **Thornmaw (BOSS)** — Nature **Attacker**, tier 4. Large, menacing, multiple thorn-blades, a
   pronounced maw. This one may use up to 6–8 parts. Bosses are ceremonial — make it read as the
   apex of the region.

### Evolution forms (same Nature ramp, show clear lineage from the Whelp)

5. **Thornstalker** — Whelp evolved down the aggressive path. Nature **Attacker**, bigger, more
   blade-limbs, sharper lean.
6. **Bulwark Moss** — Whelp evolved down the defensive path. Nature **Defender**, broad mossy shield-mass.
7. **Sporeling** — Whelp evolved down the patient path. Nature **Producer**, one swollen spore-sac.

### Items (32×32 icon each, transparent, one glyph-like object)

- **Rarity is shown by a COUNTABLE RING FRAME, not just color** — this is the mechanical signal:
  - Common: plain Void Ink border, **no ring**, Bone Parchment fill
  - Uncommon: **1 ring**, soft green `#6EC87A`
  - Rare: **2 rings**, blue `#4A90D9`
  - Epic: **3 rings**, Corruption Bloom `#8B3F82`
  - Legendary: **full concentric-ring "ward-seal" ornament + radiating flourish**, Hearth Gold `#F0A830`
- Item glyphs to generate: **Weapon** (a stylized blade/tool glyph), **Charm** (a hanging sigil),
  **Material** (a raw ore/leaf cluster), **Ability Focus** (a woven knot), **Creature Core** (a
  faceted seed — always framed in Ember Threat so it reads as "always drops, always matters").

### UI / HUD glyphs (16×16, single-color on transparent, must survive at 16px)

- Weak-point marker (Ember Threat diamond), dodge charge, block shield, interrupt burst,
  Resonance node (Corruption Bloom), Gleam coin (Hearth Gold), Memory Dust mote (Bone Parchment),
  mastery sigil (Hearth Gold, ring-count = mastery level).

### The Vow scar (signature element — Pillar 4)

- A small glyph mark that overlays a piece of gear when a Vow binds to it. It should read as a
  **scar layered on top** of the item's existing glyph — a permanent mark of a restriction accepted.
  Ember Threat line-work, never a fill.

---

## Do / Don't checklist

- ✅ Transparent background, exact pixel size, 1:1 (no upscaling, no AA).
- ✅ ≤10 colors per sprite, all from the locked palette + one Source ramp.
- ✅ Unbroken Void Ink contour on every shape.
- ✅ Separate part sprites for creatures (not one merged image).
- ✅ Hearth Gold ONLY on earned/mastery/Legendary things.
- ❌ No gradients, glows, soft shadows, bloom, or lens effects.
- ❌ No drop-shadows baked into the sprite.
- ❌ No color as the sole difference between two states — shape/value must differ too.
- ❌ Don't invent new palette colors "to make it pop".

---

## After you generate: where files go & how integration works

Put generated PNGs under `assets/art/creatures/`, `assets/art/items/`, `assets/art/ui/`.
Suggested naming (matches the art bible's convention): `crea_[source]_[role]_[variant]_[part].png`,
e.g. `crea_nature_atk_whelp_torso.png`.

**Heads-up on integration:** the current build draws flat colored rectangles, not textures — so
dropping PNGs in will not auto-display them yet. Wiring real sprites in needs a small code pass
(load textures via the content pipeline, map each creature's parts to its rig bones, swap the
rectangle draws for `Texture2D` draws). That is a deliberate next step, gated on you liking the loop
first. When you have assets, tell me and I'll do that pass — the rig and part model already exist, so
it is mechanical.
```
```
