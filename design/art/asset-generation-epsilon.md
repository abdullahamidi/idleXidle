# Asset Generation — Epsilon: **THE INKED RELIQUARY**

**This batch replaces the art style of the entire game.** Batches 1–4 built a 480×270 point-sampled
pixel-art game. This one re-draws it **hand-drawn**: heavy ink linework, flat reserved colour,
candlelit parchment. Every previous batch's output is superseded.

> **REVISION 2 — read this even if you read revision 1.** The first batch came back looking flat and
> dead, and it was THIS DOCUMENT'S FAULT, in three measurable ways. Everything below is corrected:
>
> 1. **There are now TWO style blocks (§1).** There was one, and it was a creature block. Applied to a
>    background it forbade every technique that makes a background work. **Figures are inked; places
>    are painted.**
> 2. **The "backgrounds L ≤ 33" rule is DELETED (§2.4).** It crushed a real forest painting into 28 of
>    255 luminance values. The engine dims the arena now. Paint at full range.
> 3. **A creature is not a ramp (§2.6).** "A 4-step ramp of the Source hex" produced creatures that
>    measured 73%% one green + 26%% ink + **0%% everything else**. Bone, and an accent, are not optional.
>
> Also: creatures are now authored at **224×320** (was 128×184) and the arena layout moved to make
> that real — see §3.1a.

---

## 0. READ THIS FIRST — what the game is, and what NOT to draw

**The game is an auto-battler.** You muster up to 5 creatures in an ordered line; they fight rising
waves *without you*; you decide when to bank the haul or push deeper. Every fifth wave is a boss.

This matters because **the game changed and the old art specs did not.** Three things previous
batches asked for are now unreachable by any player, and the code that would draw them is deleted.
Do not spend a single asset on:

| ❌ DO NOT DRAW | Why |
|---|---|
| **The Hunter character** (`hunter.png`, `hunter_attack.png`) | The Hunter is a **commander who never appears on screen**. He has not swung a weapon since the pivot. `hunter.png` exists in the repo and **is loaded by nothing**. The previous batch called it "TOP PRIORITY" — it is now an orphan. |
| **Part-break / damage-state decals** | Part-targeting was manual combat's mechanic. Deleted. Nothing breaks parts. |
| **Vulnerability / weak-point glyphs** | Same — the "weak point window" is gone. A Vow that watched for one was deleted from the game for exactly this reason. |
| **Capture / taming art** | Capture was never built. |

The art bible (`design/art/art-bible.md`) is still the constitution and §1–§4 of it are **excellent
and binding** — but it was written pre-pivot, so its §3.1 ("targetable clickable zones"), §5.1–5.2
(the Hunter's on-screen presence, Vow scars on *his* body) describe deleted systems. Where this
document and the bible disagree, **this document wins**, and I say why each time.

### The one thing the bible got exactly right, which you must not break

> **Read the world, don't just watch it: every mechanically important state renders as a distinct
> glyph — never a colour swap alone.**

Shape carries category. Edge quality carries Source. Mass carries Role. Colour *reinforces*, never
carries alone. This makes the game colourblind-safe **by construction** rather than by retrofit.
Hand-drawn ink is *better* at this than pixel art ever was — a torn edge and a faceted edge are
trivially distinguishable in ink and nearly impossible at 32px.

---

## 1. TWO STYLE BLOCKS — and using the wrong one is what went wrong last time

> **The first batch came back looking flat and dead, and it was this document's fault.** There was ONE
> style block, and it was a *creature* style block. Applied to a background it forbade every technique
> that makes a background work. Never paste the creature block into a background prompt again.
>
> **The governing split — this is the whole idea:**
>
> ## FIGURES ARE INKED. PLACES ARE PAINTED.
>
> Ink is not decoration; **ink is the signal that says "this is a thing that matters."** The moment a
> tree has the same black contour as a creature, ink stops meaning anything and the screen becomes a
> soup of outlines. This is exactly what Darkest Dungeon does: its characters are hard-inked cutouts;
> its environments are painted. Take that.

### 1A. THE CREATURE / ITEM / UI BLOCK — for anything with a transparent background

```
STYLE: "Inked Reliquary" — hand-drawn 2D game art, in the manner of a medieval bestiary plate:
an inked figure on nothing. Heavy confident black ink linework (woodcut / Albrecht Durer / Harry
Clarke), filled with flat matte colour. Inked plate first, colour second. Not painterly, not
rendered, not glossy, not 3D, not vector.

LINE: ONE unbroken heavy outer contour (3-4px at authored size), clearly heavier than any interior
line. Interior detail is a thinner leading line of the same iron-gall ink (#14101A), never a
coloured line. Hand-made weight variation and slight tremble — drawn, not vectored. Never pure
black. Never anti-aliased into mush.

COLOUR: flat fills, hard steps, NO gradients on the body. But NOT monochrome — see MATERIALS.

MATERIALS (this is what makes it a creature and not a coloured shape):
  - Its SOURCE hue is the flesh: roughly 50-60% of the figure. Dominant, never the whole thing.
  - HARD PARTS are bone, in vellum #EDE3C8 — horns, claws, teeth, beak, plate, shell edges.
    Bone against flesh is most of what "detailed" actually means.
  - ONE ACCENT, small and saturated: the eye, and the Source-organ at its core. This is the only
    place a contrasting hue is allowed, which is exactly why it draws the eye.
  - Optional secondary material appropriate to the Source: wood, rusted iron, chitin, cloth.
  Target 3-4 distinct hue families. A figure that is one hue plus ink is a silhouette, not a beast.

LIGHT: single warm candlelit key from upper-left; deep unlit shadow where it does not reach.
TEXTURE: faint dry-brush / aged-paper tooth over flat fills. Grain, not noise.
EYES: ONE pale slit in vellum #EDE3C8. Locked across every creature in the game.
MOOD: grim, hand-made, occult, worn. Beautiful danger. Strange, dignified, worth keeping.
      Never cute, never chibi, never anime, never neon, never "flat vector mobile game".
BACKGROUND: fully transparent. True alpha. No white box, no checkerboard, no baked drop-shadow.
```

### 1B. THE BACKGROUND BLOCK — for arenas and screens. **A PLACE, not a page.**

```
STYLE: a hand-PAINTED place, seen through candlelight and depth. Digital painting, soft and
atmospheric, in the spirit of Darkest Dungeon's environments. This is NOT the creature style:
there is no heavy ink outline here, and there must not be. Ink is reserved for creatures — it is
how the player's eye finds them. A painted world behind inked figures.

SMOOTH IS THE POINT: gradients are WANTED. Atmospheric perspective, aerial haze, soft falloff,
volumetric light shafts, fog gathering in the distance, colour shifting cool as it recedes.
Nothing here is a flat fill.

DEPTH IN THREE LAYERS:
  - FAR: hazy, low contrast, cool, almost dissolved. Barely more than a suggestion.
  - MID: the world itself. Where the readable content lives.
  - NEAR/FOREGROUND: the bottom third of the frame. Darker, larger, out of focus, framing the
    fight from below. This is a real layer, not empty ground.

LIGHT: one warm candlelit source, and the darkness it fails to reach. Let light DO something —
pool on the ground, catch an edge, shaft through a canopy. Contrast between lit and unlit is the
whole mood.

PAINT AT FULL RANGE. Bright where it is lit, black where it is not. Do NOT pre-darken the image:
the engine dims the arena at draw time, so a dim asset gets dimmed twice and dies.

MOOD: grim, old, overgrown, quietly wrong. A place that was something else once. Beautiful, and
not safe.

NO characters. NO creatures. NO text. NO UI. NO heavy black outlines on anything.
```

**Why 1B exists at all**: the first arena was authored to a rule that demanded mean luminance ≤ 33,
and it obeyed. The finished painting occupied **27.9 of 255 luminance values — 11% of the range.**
There was a real forest in the file; none of it survived. That rule is deleted. **The engine dims the
arena now** (`Game1.ArenaDim`), so you paint it bright and beautiful and I darken it to taste — a
number I can tune, instead of damage baked in forever.

---

## 2. THE PALETTE — **REBUILT**. Locked.

> **This replaces the art bible's §4 palette.** The bible's palette is retired, and not on taste — it
> **fails the bible's own greyscale test**, measurably. I checked before replacing it.

### 2.1 Why the old palette had to go — measured, not felt

Sorted by luminance, the old twelve pile up in the middle:

| Old colour | Hex | Luminance |
|---|---|---|
| Void Ink | `#1B1620` | 24 |
| **Shadow** | `#2E2438` | **40** ← only **16** above the ink it is outlined with |
| Body | `#8C2E42` | 67 |
| Corruption Bloom | `#8B3F82` | 84 |
| **Cold Slate** | `#57616F` | **96** |
| **Machine** | `#9C5A32` | **101** |
| **Ember Threat** | `#D8483A` | **102** |
| Nature | `#5C8A3A` | 122 |
| Mind | `#3FA9C9` | 149 |
| Hearth Gold | `#F0A830` | 175 |
| Spirit | `#C7BFE0` | 195 |
| Bone Parchment | `#E8DFC8` | 223 |

**Four colliding pairs**, and the worst is fatal:

- **Machine vs Ember Threat: delta 0.** Identical luminance. In greyscale — i.e. for a player with
  achromatopsia, or anyone glancing — **the industrial creature and the danger colour are the same
  colour.** The bible's own Master Rule says colour is never the only channel for anything the player
  must react to. Here it was the *only* channel, and it wasn't even that.
- Cold Slate vs Machine: 5. Cold Slate vs Ember: 6. Cold Slate vs Corruption Bloom: 12.
- **Shadow is 16 from Void Ink.** A Shadow creature drawn with an ink contour is one flat black mass.

And it explains the muddiness you reacted to. Read the old bible's own notes: Body is *"pushed deep so
it never reads as Ember Threat"*; Machine is *"kept muddy and desaturated specifically so it never
competes with Hearth Gold"*. **Those are not design decisions, they are apologies.** The palette was
overcrowded in the warm range, and every colour got desaturated to hide the collisions. That is why it
looks like mud: **it is mud on purpose, to cover a structural fault.**

Fix the structure and the colours can be jewels again.

### 2.2 The fix: a VALUE LADDER of manuscript pigments

Two changes. First, the pigments are **real illuminated-manuscript colours** — which is what the style
actually is, so the palette and the technique finally agree. Second and more importantly, **each Source
owns a luminance band**, so greyscale alone separates all six.

| Source | Pigment | Hex | **L** | **Edge quality — the real signal** |
|---|---|---|---|---|
| **Shadow** | Woad / indigo | `#52457E` | **76** | **Torn, discontinuous edges** |
| **Body** | Kermes / madder | `#D6485C` | **104** | **Convex, smooth, symmetric arcs** |
| **Machine** | Bole / iron oxide | `#BC7840` | **130** | **Orthogonal, rigid plates** |
| **Nature** | Verdigris | `#48B888` | **157** | **Branching, asymmetric outgrowths** |
| **Mind** | Lapis / azure | `#74C6E8` | **183** | **Hard, faceted, symmetric cuts** |
| **Spirit** | Bone ash | `#DCD4EC` | **215** | **Soft, dissolving wisps** |

**Worst pair separation: 26.** (Old palette's worst: **0**.) Every Source is now distinguishable from
every other **with the colour removed entirely.**

Note what moved and why:
- **Shadow is no longer near-black.** "Light removed" is now carried by *torn edges and a cold violet*,
  not by low value. A dark creature on a dark ground is invisible; that is not atmosphere, it is a bug.
- **Nature is verdigris, not leaf-green** — a copper blue-green. It reads at distance, and it stops
  Nature creatures dissolving into a Nature arena.
- **Machine is bole (red-earth), 52 luminance clear of Gold Leaf.** It no longer needs to be muddy to
  avoid gold; it is separated by *value*, so it can be a real colour.

### 2.3 The core six

| Colour | Pigment | Hex | Job | Rule |
|---|---|---|---|---|
| **Iron Gall** | oak-gall ink | `#14101A` | Every line, every seam | **Never a fill.** This is the leading. |
| **Vellum** | prepared skin | `#EDE3C8` | Ground, UI chrome, resting text | The "nothing is happening" default. |
| **Gold Leaf** | gold | `#F7C24A` | **EARNED — mastery, ownership** | **Reserved. Never decorative.** |
| **Vermilion** | cinnabar | `#E04B2E` | Danger | **VFX and UI only — never a creature fill.** |
| **Tyrian** | murex purple | `#6B2461` | Wrongness / enchantment | Sour, off-harmony. Not regal. |
| **Stone** | — | `#3E4652` | Architecture, dim structure | Backgrounds and chrome. |

**The Gold Leaf rule is still the most important rule in the palette.** Spent like a medieval glazier's
gold-ruby: on the one thing that matters, never spread. If everything is gold, nothing is earned.

**Vermilion is exempt from the ladder** — deliberately. Every saturated red-orange lands near L≈120, so
danger would always collide with *something*. It is exempt because **it is never a creature's body**:
the same sprite is ally and enemy (§4.6), so threat is carried by position, scale and motion. Vermilion
lives in VFX and UI, where an ink contour separates it from whatever it sits on.

### 2.4 ⚠ SEPARATION — and the rule I got badly wrong

> ## Creatures **L ≥ 76**.  Backgrounds: **paint them at FULL RANGE.**
> **The ENGINE dims the arena. You never pre-darken anything.**

**This replaces a rule that did real damage, and the damage is worth understanding**, because the
instinct behind it will come back if I don't write down why it was wrong.

The old rule said *"backgrounds L ≤ 33"*. It was aimed at something true — a creature must read against
its arena. The first hand-drawn arena obeyed it exactly, and the result:

> **The entire painting occupied 27.9 of 255 luminance values. Mean 21.6. Eleven percent of the range.**

There was a real forest in that file — trees, canopy, ground, stones. I recovered the crushed range to
check. None of it survived. **Nobody can make something beautiful in 28 values.**

The rule was right about the goal and **wrong about where to enforce it**:

| Pre-baked by the artist | Applied by the engine |
|---|---|
| Destroys the source, permanently | Source stays at full range |
| Cannot be tuned | One number, tunable any time |
| Costs the artist everything | Costs one multiply |

`Game1.ArenaDim` now darkens the arena at draw time. **So paint it bright, deep and beautiful.** If it
comes out too bright in-game, that is my number to move, not your painting to ruin.

The creature floor stays: **every Source is L ≥ 76**, which is why Shadow is woad and not near-black
(§2.2). That half was always sound.

### 2.5 The 4-step ramp

Each Source's hex above is the **base** step of that creature's **flesh**. Build the other three from it:

| Step | Relative | Purpose |
|---|---|---|
| **Highlight** | ~1.35× | The candlelit key from upper-left. **Give this real area** — the first assets used only 9% and made the ink do all the work. |
| **Base** | the hex above | The flesh's identity. |
| **Shadow** | ~0.72× | Form turning away. |
| **Deep shadow** | ~0.50× | Contact shadow, crevices. |

### 2.6 ⚠ THE RAMP IS NOT THE WHOLE CREATURE — the other mistake I made

The first two creatures came back as green blobs. I measured them:

| | Nature Attacker | Nature Defender |
|---|---|---|
| Hue 154° (the Source ramp) | **73%** | **78%** |
| Iron gall ink | 26% | 21% |
| **Every other hue family** | **0%** | **0%** |

**That is not a creature. It is a silhouette filled in.** And it is precisely what the old §1 asked
for: *"flat fills in a 4-step ramp per subject"*, full stop. Four steps of one green, obeyed exactly.

**A ramp of one hue can never look detailed, and it can never look unique**, because every creature of
a Source comes out the same colour as every other. The Nature Attacker and the Nature Defender were
literally the same six greens in different outlines.

So the ramp is the **flesh only, ~50–60% of the figure**. The rest is what makes it a beast:

| Layer | Colour | Area | This is where "detailed" comes from |
|---|---|---|---|
| **Flesh** | the Source ramp | 50–60% | Its identity and its element. |
| **Hard parts** | **Vellum `#EDE3C8`** + a shadow step | 15–25% | **Horns, claws, teeth, beak, plate, shell rims, tusks.** Bone against flesh is most of what detail *is*. Bone is also the one material the eye reads instantly at any size. |
| **Ink** | `#14101A` | ~20% | Contour and interior leading. |
| **Accent** | ONE contrasting hue | **2–5%** | The **eye** and the **Source-organ** at its core. Tiny. Saturated. The only contrasting hue permitted — which is exactly *why* it commands the eye. |
| **Secondary** *(optional)* | material-appropriate | 0–10% | Wood or thorn for Nature; rust for Machine; chitin for Shadow; cloth or bandage for Body. |

**Target 3–4 hue families per creature.** Not one. The test is in §12.

---

## 3. TECHNICAL CONTRACT — non-negotiable

### 3.1 Resolution: **author at 4× the listed size**

The game's layout lives in a **480×270 logical grid** (every button, every slot, every panel is
positioned in it). That grid is not changing — rewriting it would mean re-laying-out every screen.
What changes is **pixel density**: the render target goes to **1920×1080** and every draw is scaled
4×, so an asset authored at 4× lands at exactly 1:1 native density with no filtering.

> **Hand-drawn cannot survive at 480×270.** Point-sampled linework at that size *is* pixel art —
> there is no room for a 2px ink contour and an interior seam on a 32px creature. This is the whole
> reason the style change requires the resolution change. On the code side it is a small change (a
> render-target size, a scale matrix, and switching from `PointClamp` to `LinearClamp`); on the art
> side it is everything.

**So: every size in §4–§9 below is the LOGICAL size. Multiply by 4 when you draw.**

| Thing | **AUTHOR AT** | Why that number |
|---|---|---|
| **A creature** | **224×320** | ↓ see below |
| A full-screen background | **1920×1080** | 480×270 logical × 4 |
| A 16×16 icon | **64×64** | |
| A 5-frame 32px VFX strip | **640×128** | 5 frames of 128×128 |

### 3.1a Why creatures are 224×320 — **this changed, and the arena changed with it**

**The same sprite is used at two sizes**, and it must be authored for the LARGER one:

| Use | Logical box | Sprite lands at | Scale |
|---|---|---|---|
| Enemy (the big one) | 88×88 | 224×320 physical | **1:1** ✅ |
| Ally slot | 42×64 | 168×240 physical | 0.75× **downscale** ✅ |

Upscaling hand-drawn art is soft; downscaling is clean. Author once at the enemy's size and the ally
slot gets a free, crisp reduction.

**The arena layout moved to make this true.** Creatures were 32×46 logical — 17% of the frame height —
and the fight sat at y=96..142 of a 270-tall canvas, leaving **47% of the screen as empty ground**. No
background can look good carrying that much dead space, and no prompt could have fixed it: it was a
layout bug wearing an art problem's clothes. Now:

- **A ground line at y=182 (67% of the canvas).** Every creature, ally and enemy, stands on it.
- **Slots are 42×64** — roughly +90% area. The creatures are what the player *watches*; they were too
  small for any authored detail to survive.
- **The bottom third is FOREGROUND**, and §1B asks you to paint it as a real layer.

### 3.1b Generate big, deliver at size

**Do not generate at 224×320.** Generate at **896×1280** (4× the delivery) and downsample. A generator
given a small canvas produces a small idea — it spends its pixels on the silhouette and has none left
for a claw. Generate with room, then reduce: the detail survives the reduction and the line tightens.

This is the single cheapest thing on this page for making creatures look "more detailed".

#### Why 4×, with evidence rather than taste

I checked what shipping hand-drawn 2D games actually do. **"Author at 2–4× the displayed size" is
not my invention — it is the industry's default:**

| Game | Displayed | Authored | Ratio |
|---|---|---|---|
| **Slay the Spire** | card image **250×190** | portrait **500×380** | **2×** |
| **Slay the Spire 2** | card **~250×190** (atlased) | portrait **1000×760** | **4×** |
| **Inscryption** | card base **125×190**, portrait window **114×94** | — | authored tiny, deliberately |

*(Sizes from the BaseMod modding API and datamined asset rips; verified directly. Slay the Spire's
design resolution is almost certainly 1920×1080 with a single uniform scale multiplier — strongly
inferred from a uniform `Settings.scale`, a 1080p ceiling, and modders treating non-1080p as "the
deviation" — but **not** something I could cite a dev statement for. Treat it as well-supported, not
proven.)*

**Slay the Spire 2 authors at exactly 4× what it displays.** That is the number in this document,
arrived at independently, and it is reassuring to find it already shipping.

**The counter-example is the instructive one.** Inscryption authors its cards at **125×190** — an
absurdly small canvas — and its developers have said much of the art effort went into **legibility
at low resolution**, to the point of *removing text from cards entirely* and letting icons cover the
portrait. That is what a low canvas costs you: not ugliness, but **the loss of the ability to say
things**. It is the same trap as 480×270 hand-drawn, and it is why the resolution moves.

**One more, on aspect ratio**: Vampire Survivors' developer keeps hard black letterbox bars rather
than support ultrawide, because *"playing on something like a 21:9 screen makes the enemies spawn way
too far from the character."* The play field **is** the balance. Our 16:9 is likewise locked and
letterboxed for the same reason — do not design any asset that assumes it can bleed off-frame.

### 3.2 Format

- **PNG-32, straight alpha, transparent background.** No white box. No checkerboard. No drop
  shadow baked in (the game composites its own ground shadow).
- **No baked-in UI frames** around creatures or items — the game draws frames.
- **Trim tightly** to the subject with ~2px (logical) bleed. Do not centre a small subject in a
  large empty canvas.
- **No text baked into any asset**, ever. All text is rendered by the game (localisation, and the
  font is changing).

### 3.3 Naming: `snake_case`, exactly as listed

The game loads assets **by filename**. A typo = a silent greybox. `crea_nature_atk_whelp_torso.png`
is a contract, not a suggestion. Every filename in this document is exact.

**Greyboxing is safe**: any missing asset draws a flat placeholder shape. Nothing crashes. Drop
files in and they appear next launch. **You can deliver this batch incrementally.**

---

## 4. CREATURES — the heart of it (biggest job)

### 4.1 ⚠ ONE SPRITE PER CREATURE. **Do not draw body parts.**

I checked the code before writing this, and the answer surprised me:

> **The game loads `{name}_torso` and NOTHING ELSE.** It does not composite `_core`, `_flank`,
> `_left_claw` or any other part. There are **69 part files already in this repo that nothing
> draws.** A previous batch generated them for a cutout rig that was built as a Tab-key tech demo
> and never wired into the game.

So the honest instruction is: **draw each creature as ONE complete sprite**, named `_torso`. That
single file is what appears in the fight, in the Warren, everywhere.

**Why not just wire the rig?** Because I will not ask you to draw 150 files on the promise that I
wire something afterwards. That promise is exactly what produced the 69 dead files. **Draw the 30
single sprites; they work the day they land.** If the rig gets wired later, parts become a Phase 2
ask against a system that provably runs — see §10.

The game still animates a single sprite: it lunges on attack, recoils on hit, bobs at idle, flashes
when shielded and dissolves on death, all by transforming the whole sprite. That is real motion, and
it costs you one file per creature.

### 4.2 Contract per creature — exactly one file

| File | What | Author at |
|---|---|---|
| `crea_<source>_<role>_<variant>_torso.png` | **The whole creature**, complete, one piece, transparent background. | **224×320** |

- **Full body in frame**, standing on an implied ground line at the bottom edge.
- **3/4 view facing RIGHT** (allies). Enemies face LEFT.
- No baked drop-shadow — the game draws its own ground shadow.
- **The outer contour is one unbroken silhouette.** Interior detail is leading lines *inside* it; the
  silhouette never notches inward. *(From bible §3.1. Its stated reason — "targetable clickable
  zones" — is dead with manual combat. The rule stays for a better reason: cohesion. A creature must
  read as one living thing.)*
- **Vary the internal masses** so the eye can count them pre-attentively. No two adjacent masses the
  same size and shape.

### 4.3 Role carries mass — this is a gameplay signal

Five creatures stand in a line and fight without input. **The player must read the line at a
glance.** Role is the most important thing to read, so Role owns *mass distribution*:

| Role | Mass | Silhouette test | HP |
|---|---|---|---|
| **Attacker** | Mass **forward and high** — reaching, weapon-ward. | Reads as *lunging* even standing still. | 0.9× |
| **Defender** | Mass **low, wide, planted**. Broadest silhouette in the game. | Reads as *a wall*. | **1.9×** — it really is a tank |
| **Support** | Mass **upward and open** — raised, offering. | Reads as *giving*. | 0.8× |
| **Crafter** | Mass **inward, busy** — hunched over its own hands/tools. | Reads as *working*. | 0.7× (glass) |
| **Producer** | Mass **rounded, heavy, bottom-set** — a vessel. | Reads as *full*. | 0.7× (glass) |

*Design test*: **black out any creature to a pure silhouette. If you cannot name its Role, redraw
it.** This is the single most important test in the whole document.

### 4.4 The roster: 6 Sources × 5 Roles = 30 species

Every Source now has a full evolution tree reaching all five Roles, so **every cell is reachable in
game**. Names are authored — use them, they are what the game displays.

| Source | Attacker | Defender | Support | Crafter | Producer |
|---|---|---|---|---|---|
| **Nature** | THORNSTALKER | BULWARK MOSS | MOSSLING | SAPWRIGHT | SPORELING |
| **Machine** | RIVET WARDEN | PLATED BULWARK | MEND DRONE | COGWRIGHT | BOILER ENGINE |
| **Shadow** | NIGHT STALKER | GLOOM WARD | PALE WISP | SILK WEAVER | SPORE OF NIGHT |
| **Body** | SINEW BRUTE | BONE WALL | PULSE KIN | MARROW SMITH | BROOD MOTHER |
| **Mind** | LANCE OF THOUGHT | CALM AEGIS | QUIET CHORUS | SCHEMA WRIGHT | IDEA BLOOM |
| **Spirit** | ECHO FANG | VIGIL SHADE | SOLACE SHADE | RITE KEEPER | EMBER FONT |

Plus **six root forms** (what everything hatches as, before it evolves):

`VERDANT WHELP` (Nature) · `SCRAP WHELP` (Machine) · `DUSK WHELP` (Shadow) · `RAW WHELP` (Body) ·
`STILL WHELP` (Mind) · `FAINT WHELP` (Spirit)

### 4.5 ⚠ **THE ROSTER IS NOW 12 + 5, NOT 30.** The exact filenames.

**This changed.** With bone, accents and real linework, each creature is a genuine piece of work — and
30 of those, done properly, is a wall. **Twelve done well beats thirty done as colour swatches.**

That is affordable because **the game already has a graceful fallback, and it is good**: any cell with
no species art draws a **generic role worker tinted by its Source colour**. So all 30 cells still
render, and always did — the ✅/➕ tables in earlier revisions overstated the problem.

| Tier | What | Files | Covers |
|---|---|---|---|
| **HERO** | Hand-drawn species. Full colour, distinctive, untinted. | **12** | The creatures you look at for 90% of the game |
| **GENERIC** | One per Role. Neutral-toned, **tinted by Source at draw time.** | **5** | All 30 cells, gracefully |

#### The 12 heroes — pick these because they are what a player actually SEES

| | Attacker | Defender | Support | Crafter | Producer |
|---|---|---|---|---|---|
| **Nature** ★ region 1 | `crea_nature_atk_whelp` ✅ | `crea_nature_def_bramble` ✅ | `crea_nature_sup_mossling` | `crea_nature_crf_sapwright` | `crea_nature_prd_sporeling` |
| **Machine** ★ region 2 | `crea_machine_atk_warden` | `crea_machine_def_bulwark` | `crea_machine_sup_drone` | `crea_machine_crf_cogwright` | `crea_machine_prd_boiler` |
| **Shadow** ★ region 3 | `crea_shadow_atk_stalker` | `crea_shadow_def_bulwark` | — generic — | — generic — | — generic — |

✅ = done (rev 2, and they are good). All get `_torso.png` appended, all **224×320**.

**Why these twelve**: Nature is where every player starts and spends the most hours. Machine is region
2. Shadow's Attacker and Defender are what you *fight* in region 3. **Body, Mind and Spirit never
theme a region** — they only ever appear as creatures you hatch, standing in a line, where a tinted
generic reads perfectly well.

#### The 5 generics — **do not skip these; they carry 18 of the 30 cells**

| File | Role | Author at |
|---|---|---|
| `crea_worker_attacker_torso.png` | Attacker | 224×320 |
| `crea_worker_defender_torso.png` | Defender | 224×320 |
| `crea_worker_support_torso.png` | Support | 224×320 |
| `crea_worker_crafter_torso.png` | Crafter | 224×320 |
| `crea_worker_producer_torso.png` | Producer | 224×320 |

> **⚠ These are drawn DIFFERENTLY from the heroes, and this is the one place §2.6 is suspended.**
>
> **The engine multiplies these by a Source colour**, so they must be authored **NEUTRAL** — a pale
> bone-grey flesh (`#C8C2B4`-ish ramp) with the usual iron-gall contour. Painted verdigris, they would
> come out verdigris×verdigris and turn to mud.
>
> - **Flesh: neutral bone-grey.** The tint supplies the Source colour.
> - **Ink contour: normal.** Iron gall survives a tint (it is nearly black; anything × nearly-black is
>   nearly-black).
> - **NO accent eye, NO coloured bone.** Anything you colour gets multiplied. Keep it monochrome
>   *on purpose* — this is the exception that proves §2.6's rule.
> - **Silhouette is everything.** These are the only thing distinguishing 18 cells, so §4.3's mass
>   language must be unmistakable: the Defender low and wide, the Attacker forward and high.
>
> A tinted generic *should* read as "an ordinary Body attacker" next to a hero's "a THORNSTALKER".
> That contrast is a feature: it makes the heroes feel like somebody.

### 4.6 Enemies — ⚠ **there are no enemy assets. Do not draw any.**

My first draft asked for `enemy_*` and boss files. **Nothing loads them.** I found this when the first
creature went in:

> **The enemy is drawn from the SAME sprite as your squad's creatures.** The code maps a region to a
> `crea_*` key — Verdant Hollow's enemy *is* `crea_nature_atk_whelp_torso.png`, the same file the
> squad's Nature Attacker uses. There is no separate enemy art, and **no separate boss art either**;
> a boss is the same sprite, tougher.

The first asset immediately proved the cost of this: **the enemy stood with its back to the fight**,
because every creature is drawn facing right and nothing flipped it. Invisible while creatures were
32px blobs with no discernible front; obvious the second one had a readable head. **I fixed it — the
enemy is now flipped horizontally.**

**What this means for you:** every creature you draw is *also* an enemy, mirrored. So:

- **Draw it facing RIGHT.** The game mirrors it when it is the enemy.
- **Do not put text, asymmetric branding, or anything that breaks when mirrored** on a creature.
- **The Ember-Threat-only-on-enemies rule from my draft is void** — the same sprite is both. Threat
  is carried by *position and scale* (the enemy is alone, on the right, larger), not by colour.

If we later want enemies to be their own creatures rather than mirrored allies, that is a code change
I own, and it becomes a separate batch — against a system that provably runs.

---

## 5. VOW MARKS — a NEW asset class ➕ (I wire it; draw it)

> **Status: nothing loads these yet.** Unlike §8/§9, I am *not* cutting them — they are the highest
> value new art in this batch, and the systems behind them shipped this week. I will wire them.
> Draw them in the priority order at §11 (tier 5), not first.


**Vows are the one thing in the game the player authors.** A creature's Source is fixed at hatch and
its Role is evolution-gated — but the player chooses its **Vow**: a restriction accepted in exchange
for power ("only while below 40% health", "only in the front slot", "it permanently loses 15% of its
maximum health").

By the Master Rule, **a sworn Vow must be visible on the creature.** The bible put "Vow scars" on the
Hunter's body — that is now wrong, because the Hunter is not in the fight and Vows belong to
creatures. **This is the bible's own principle, relocated to where the mechanic actually lives.**

Six small **overlay decals**, drawn to sit on a creature's torso. Transparent, no creature under them.

| File | Vow | Reads as |
|---|---|---|
| `vow_bloodied.png` | VOW OF THE BLOODIED — only below 40% health | A cracked, bleeding sigil |
| `vow_boss_bound.png` | VOW OF THE BOUND — only against bosses | A chained/sealed ring |
| `vow_vanguard.png` | VOW OF THE VANGUARD — only in the front slot | A forward-pointing spearhead brand |
| `vow_patience.png` | VOW OF PATIENCE — only after 10s | An hourglass / slow spiral |
| `vow_fragility.png` | VOW OF FRAGILITY — takes +12.5% damage | A hairline-fracture web |
| `vow_reckless_offering.png` | RECKLESS OFFERING — loses 15% max health | A cut-open offering bowl |

**Author at 48×48** (12×12 logical). Drawn in **Void Ink line + Hearth Gold fill** — a Vow is
*earned*, and gold is the earned colour. They must read at 12px: **container + one mark**, nothing
more. Test: shrink to 12px. Still nameable? Ship it.

---

## 6. ITEMS, WEAPONS & GEAR

### 6.1 What an item now is

An item has **four** things the player reads: base type, rarity, **element**, and **trait/
enchantment**. All four must be visible.

### 6.2 Item glyphs (base type)

`item_glyph_<type>.png` — **author at 64×64**, on transparent.

| File | Type | Shape identity |
|---|---|---|
| `item_glyph_weapon.png` | Weapon | A bladed/hafted tool — aggressive, forward |
| `item_glyph_charm.png` | Charm | A hung, bound trinket — enclosed, protective |
| `item_glyph_focus.png` | Focus | A lens/prism/eye — open, attentive |
| `item_glyph_material.png` | Material | Raw, unworked stuff — irregular |
| `item_glyph_core.png` | Creature core | An egg/seed — the only *living* item |

**Weapons deserve real attention** — they are the power fantasy. Draw them as *instruments a
tracker-alchemist made*, not as heroic swords: hafted, bound with cord, uneven, personal.

### 6.3 Rarity frames

`item_frame_<rarity>.png` — **author at 224×176** (56×44 logical), a 9-slice-safe frame.

The bible **rejects the genre rainbow.** Rarity is a **bone→gold ramp plus countable rings** — you
can *count* how rare a thing is, not just recognise a colour. This is the colourblind-safe rule
again, and it is not optional.

| File | Rarity | Frame |
|---|---|---|
| `item_frame_common.png` | Common | Bare Bone Parchment line. **0 rings.** |
| `item_frame_uncommon.png` | Uncommon | Bone, slightly heavier. **1 ring** at the corners. |
| `item_frame_rare.png` | Rare | Warmer bone. **2 rings.** |
| `item_frame_epic.png` | Epic | Bone→gold transition. **3 rings.** |
| `item_frame_legendary.png` | Legendary | Full **Hearth Gold**, ornate. **4 rings.** |

### 6.4 Element tags ➕ (I wire it; draw it)

> **Status: nothing loads these yet — I wire them.** Same as §5: new, valuable, worth the effort.

An item now carries its **region's element** (Cinderworks loot is Machine), and matched elements
carry through a merge. The player must be able to spot a matched trio at a glance.

`elem_tag_<source>.png` — **author at 40×40** (10×10 logical), a small corner tag for an item card.
Six files, one per Source, using that Source's **edge quality** (§2.2) — the tag for Shadow has torn
edges; Mind's is faceted. **Not six coloured dots.**

### 6.5 Enchantment inlays ➕ (I wire it; draw it)

> **Status: nothing loads these yet — I wire them.**

Rare+ items carry an enchantment that changes what *happens* (not a number). Five small inlay marks
that sit **on** an item glyph, showing it is enchanted.

`ench_<kind>.png` — **author at 32×32** (8×8 logical).

| File | Enchantment | Reads as |
|---|---|---|
| `ench_splinter.png` | On kill: hits again | A splitting/forking mark |
| `ench_harvest.png` | On kill: spare core | A seed/bud mark |
| `ench_venom.png` | Skills poison | A dripping mark |
| `ench_desperation.png` | Near death: bigger haul | A hungry/grasping mark |
| `ench_undying.png` | Survives one fatal blow | An unbroken ring |

Drawn in **Tyrian `#6B2461`** — an enchantment is *power with something wrong in it*.
This deliberately distinguishes them from Vow marks (gold, earned) at a glance.

---

## 7. BACKGROUNDS

`bg_<name>.png` — **author at 1920×1080**.

Every screen is a **place**, not a menu. The user has said three times: *"every scene must be a real
2D pixel-art game scene, not text/tables."* That directive now means **a real hand-drawn scene.**

| File | Screen | Direction |
|---|---|---|
| `bg_title.png` | Title | The reliquary's frontispiece. The most beautiful single image in the game — it is the first thing anyone sees. Candlelit, ornate, ink-heavy. |
| `bg_arena_verdant.png` | Verdant Hollow fights | **Nature**: overgrown ruin, branching growth, damp green dark. |
| `bg_arena_machine.png` | Cinderworks fights | **Machine**: rusted foundry, orthogonal plates, ember-lit iron. |
| `bg_arena_shadow.png` | Umbral Reach fights | **Shadow**: torn dark, light *removed*, indigo void. |
| `bg_forge.png` | The Forge | An anvil and a fire. Warm, working, the game's one *safe* room. |
| `bg_warren.png` | The Warren | Where creatures live and work. Domestic, dim, alive — four work-stalls implied. |
| `bg_constellation.png` | Memory Dust | A star-chart on parchment. Nothing here ever resets — it should feel *permanent*. |
| `bg_regionmap.png` | World map | An inked map of three regions. Hand-drawn cartography, sea-monster energy. |

### 7.1 Arena composition — **THE GROUND LINE**

The last version of this said *"keep the fight band quiet, detail belongs at the top and bottom"* and
got exactly what it asked for: **literal horizontal bands of flat colour.** My words, your stripes.
Here is what I actually meant, said properly.

**Everything hangs off one line.** In `bg_arena_*`, at **1920×1080**:

```
  y=0                 ┌─────────────────────────────────────┐
                      │  FAR: canopy, sky, haze.            │   the world receding.
                      │  Low contrast, cool, dissolved.     │   atmospheric perspective.
  y≈430               ├─────────────────────────────────────┤
                      │  MID: the place itself.             │   trees, ruins, structure.
                      │  Where light does its work.         │   this is the painting.
  y=728  ═══ GROUND ══╪═════════════════════════════════════╡  ← EVERY creature stands HERE
                      │  NEAR / FOREGROUND.                 │   a REAL layer:
                      │  Darker, larger, OUT OF FOCUS.      │   roots, stones, grass, mist.
  y=1080              └─────────────────────────────────────┘   frames the fight from below.
```

- **The ground line is at y=728** (logical y=182). Non-negotiable — the engine puts every creature's
  feet there. Paint the ground *meeting* that line: contact, not a stripe.
- **Squad on the LEFT** (x≈64–970 at 4×), **enemy on the RIGHT** (x≈1184–1536). Keep the busiest
  detail out of those two zones — but "keep it quiet" means **low CONTRAST, not no content**. Fog,
  falloff and darkness quiet an area. A flat band does not; it just looks broken.
- **The bottom third is a foreground layer, not empty ground.** It used to be 47% of dead screen and it
  is the single biggest reason the arena read as unfinished.

**Depth is what makes it a place.** Three layers, each cooler, hazier and lower-contrast than the one
in front. That is the whole trick, and it is impossible with flat fills — which is why §1B exists.

**Paint at FULL RANGE. The engine dims.** See §2.4.

---

## 8. UI & MENUS

`ui_<name>.png`. The bible's **chrome vs content** split holds: chrome is Bone Parchment + Void Ink
and stays quiet; content is where colour lives.

**This is the complete list the code loads. There are no others.** I checked; my first draft of this
document invented six that nothing reads.

| File | Author at | What |
|---|---|---|
| `ui_panel.png` | 96×96 | **9-slice** panel — 32px corners at 4×. Parchment surface, ink border, subtly aged. The most-seen asset in the game. |
| `ui_panel_gold.png` | 96×96 | Same, Hearth Gold border. The *earned / selected / affordable* state. |
| `ui_button.png` | 96×72 | 9-slice button, resting. |
| `ui_button_active.png` | 96×72 | **The only other button state.** Covers hover AND pressed AND selected. Make it unambiguous. |
| `ui_bar_frame.png` | 64×24 | Bar container. |
| `ui_bar_fill.png` | 64×24 | Bar fill. **The game tints this** (green heal, ember damage, gold haul) — draw it **neutral white/bone**, flat, no colour of its own. |
| `ui_keycap.png` | 48×48 | A keyboard keycap for shortcut hints. |
| `ui_gleam_coin.png` | 48×48 | Currency mark. |
| `ui_memory_dust.png` | 36×36 | Dust mark (prestige). |

> **A disabled button is drawn by the code**, not by an asset — it tints `ui_button` down. Do not
> author one. Same for the selection reticle and modal scrims: both are drawn procedurally.

**A note on the 9-slice panels**: they stretch. Draw the **corners** with all the character (worn,
inked, slightly irregular) and keep the **edges and centre plain enough to tile without a visible
seam**. A gorgeous panel that tiles badly is worse than a plain one.

### 8.1 Icons

`icon_<group>_<name>.png` — **author at 64×64**.

| Group | Files | Status | Notes |
|---|---|---|---|
| **Role** | `icon_role_attacker` `icon_role_defender` `icon_role_support` `icon_role_crafter` `icon_role_producer` | ✅ **live** | **Must match §4.3's mass language.** The Defender icon is low and wide; the Attacker's is forward and high. The icon is a *miniature of the silhouette rule* — if the icon and the creature disagree about what a Defender is, the game is lying. |
| **Region** | `icon_region_verdant` `icon_region_cinderworks` `icon_region_umbral` | ✅ **live** | World-map emblems. Each is its region's Source, as a heraldic mark. |
| **Logo** | `logo_title` | ✅ **live** | Author at **1280×360**. The game's wordmark on the title screen. |

> **I removed `icon_source_*`, `icon_skill_*` and `icon_nav_*` from this document.** Nothing loads
> them — the game currently renders Sources, skills and nav as text. They would be dead files. If
> you think any of them is worth having, say so and I will wire it *first*.

**Icon test**: shrink to 16px. If it is not nameable, it is decoration, not an icon.

---

## 9. VFX / EFFECTS

`vfx_<name>.png` — **horizontal strip, square frames, transparent.**

Format is a hard contract: **frame height = strip height; frames sit left→right; frame width =
frame height unless stated.** A 5-frame 32-logical-px effect is authored **640×128** (5 × 128px
frames).

### 9.1 The rule that matters most

The bible and Riot's public VFX guidance agree: **loudness must be budgeted to gameplay importance**,
and an effect's **total lifetime must be short** with its main action in the **first quarter**, then
fade. In an auto-battler the player is *reading* the fight — an effect that outstays its beat is
noise that hides the next one.

**Anticipation is the only thing that lets a spectator predict.** The enemy's wind-up is the single
most important VFX in the game. Everything else should be quieter than you think.

**Exactly eight strips are played by the game.** These eight and no others:

| File | Frames | Author at | Fires when | Loudness |
|---|---|---|---|---|
| `vfx_hit.png` | 4 | 512×128 | Every ordinary blow | **QUIETEST THING IN THE GAME** — it fires several times a second, all game long. If this is loud, the fight is unreadable. |
| `vfx_weakhit.png` | 4 | 512×128 | A blow into a bad Source matchup | **Very quiet**, and visibly *feeble* — this is feedback that you brought the wrong creature. |
| `vfx_crit.png` | 5 | 640×128 | A big blow (REND's companion) | Loud. **Hearth Gold** — it is the good thing happening. |
| `vfx_ability_ruinstrike.png` | 6 | 768×128 | **REND** — the Attacker's triple-damage skill | Loud. **Ember**. The drumbeat of every fight. |
| `vfx_interrupt.png` | 5 | 640×128 | **BULWARK** — the Defender braces | Medium. **Steel blue.** |
| `vfx_levelup.png` | 6 | 768×128 | **MEND** — the Support heals | Medium. **Green** — the one green in the palette, reserved for healing. |
| `vfx_capture.png` | 6 | 768×128 | **BLOOM** — a spare core buds | Medium. **Corruption Bloom violet.** |
| `vfx_death.png` | 6 | 768×128 | A creature dissolving | Medium. It should read as *dissolving*, not exploding. |

> **`vfx_block.png` and `vfx_break.png` already exist in the repo and nothing plays them** — two more
> dead files from an earlier batch. Do not redraw them. I also cut `vfx_telegraph` and `vfx_gleam`
> from my first draft for the same reason: the enemy wind-up is drawn procedurally (a lunge and a
> colour shift), not from a strip.

**Names vs meaning**: `vfx_capture` and `vfx_levelup` are named after mechanics that no longer exist
(capture was never built; there is no level-up). **Draw what the "Fires when" column says, not what
the filename says.** The names are frozen because the code loads them; the meanings moved.

**Draw effects as INK, not as light.** The temptation with hand-drawn VFX is glow. Resist it: these
are drawn marks — slashes, cracks, blooms of ink — that *appear and dissolve*. Bloom is a narrow
additive layer the engine adds over crisp art, never the base look.

---

## 10. ANIMATION — what you do and do not draw

**You draw two kinds of moving thing, and neither is a character animation frame:**

1. **VFX strips** (§9) — genuine frame-by-frame. Eight of them. This is where hand-drawn motion goes.
2. **Nothing else.**

**Creatures are animated by the code transforming the single sprite** you draw: it lunges forward on
attack, recoils and flashes on being hit, bobs gently at idle, tints steel when shielded, and fades
out on death. You draw one neutral standing pose; the game supplies the motion.

**Therefore: draw creatures STANDING, neutral, weight settled.** Not mid-swing, not mid-roar. A
creature drawn lunging is already animated — wrongly, permanently, in every frame including its idle.

### If we later want real creature animation

The honest options, so you know the ceiling:

- **A cutout rig** (parts + code-driven joints) — this is what Darkest Dungeon does, via **Spine**,
  with a separate PNG per body part. Our repo has a rig *spike* on the Tab key and 69 part files, and
  **the game never used any of it**. Reviving it is a real code project, not a drawing project.
- **Frame-by-frame** for 30 species is ~1,200 hand-drawn frames and roughly 700 MB. Not happening.

**Do not draw parts on spec.** If the rig gets wired and proves out on one creature, parts become a
Phase 2 ask with a working system behind it. That ordering is the whole lesson of the 69 dead files.

---

## 11. PRIORITY ORDER

Deliver in this order. **Each tier alone is a visible jump, and the game runs at every step** (missing
assets greybox safely).

| # | Tier | Files | Why |
|---|---|---|---|
| **1** | **ONE creature** — `crea_nature_atk_whelp_torso.png` | 1 | **Do this first and STOP.** One sprite proves the style, the palette, the 4× density and the transparency contract. Send it; I will put it in the game and screenshot it back to you. Getting this wrong thirty times is the only real failure mode here. |
| **2** | `bg_arena_verdant` + `ui_panel` + `ui_panel_gold` + `ui_button` + `ui_button_active` | 5 | The style *in situ*: a real creature, in a real fight, on real chrome. This is the go/no-go on the whole direction. |
| **3** | The rest of Nature (4 more) + a Nature enemy + a Nature boss | 6 | **One complete region, playable end to end in the new style.** The first genuinely satisfying milestone. |
| **4** | `ui_bar_frame` `ui_bar_fill` `ui_keycap` `ui_gleam_coin` `ui_memory_dust` + 5 role icons + 3 region icons + 5 item glyphs + 5 rarity frames | 23 | The screens stop being half-old, half-new. All ✅ live keys — every file appears immediately. |
| **5** | **Vow marks (6) + element tags (6) + enchantment inlays (5)** | 17 | ➕ I wire these. Small, cheap, and they make three invisible systems visible. Highest value-per-pixel in the batch. |
| **6** | The 8 VFX strips | 8 | The fight gains its punctuation. |
| **7** | Machine + Shadow: 10 creatures, 2 arenas, 2 enemies, 2 bosses | 16 | Regions 2 and 3. |
| **8** | Body / Mind / Spirit: 15 creatures + 6 root whelps | 21 | The long tail. Reachable only via hatching and evolution, so least urgent — but this is what makes the roster feel deep. |
| **9** | `bg_forge` `bg_warren` `bg_constellation` `bg_regionmap` | 4 | The other rooms. |
| **10** | `bg_title` + `logo_title` | 2 | **Last, deliberately.** The first thing players see should be drawn once the style is certain, not while it is being discovered. |

**Total: ~103 files.** (My first draft implied ~250 — most of which nothing would have loaded.)

---

## 12. THE TESTS — apply to everything

Before any asset ships, it passes all five:

1. **Silhouette test** — black it out. Can you name its Role/category? *(§4.3)*
2. **16px test** — shrink any icon/glyph to 16px. Still nameable? *(§8.1)*
3. **Greyscale test** — desaturate it. Is every mechanically important thing still distinguishable?
   **If it fails, colour was doing a job that shape should be doing.** *(the Master Rule)*
4. **Gold test** — is there Hearth Gold in it? Did the player *earn* that? If not, remove it. *(§2.1)*
5. **THE ARENA TEST** — put it on its OWN region's background and squint. A Nature creature in the
   Nature arena is the worst case for contrast and the first thing a player ever sees. If the ramp
   sits at the background's luminance, the ink and the rim-light are carrying the whole read alone —
   measure it, do not eyeball it. The first asset came in at **1.58:1** on its base step and
   **1.02:1** on its deep shadow. *(learned from Tier 1)*
6. **THE MONOCHROME TEST** — count the hue families in the figure. **Under 3 = it is a silhouette, not
   a creature**, and no amount of linework will save it. The first two creatures came in at ONE hue
   plus ink: 73% and 78% green, 0% everything else. Bone, and one accent. *(§2.6)*
7. **THE RANGE TEST — backgrounds only** — measure min and max luminance. If the whole painting lives
   inside a narrow band, it is dead on arrival. The first arena occupied **28 of 255 values**. Paint
   the full range; the engine dims. *(§2.4)*
8. **The line test** — is the outer contour clearly heavier than every interior line, and is every
   line the same near-black ink? *(§1)*

---

## 13. WHAT I AM CHANGING ON THE CODE SIDE

So you know what to expect, and what not to worry about:

- Render target 480×270 → **1920×1080**, with a 4× scale matrix. Every screen keeps its existing
  480×270 layout coordinates untouched.
- `SamplerState.PointClamp` → **`LinearClamp`**, and the integer-scale-only rule is **retired for
  this style** (it existed to protect pixel art; there is no pixel art to protect).
- `PixelFont` → a **real typeface**. The bible §7.1 requires a scalable data font and the tech
  preferences already anticipated FontStashSharp for exactly this. **You do not need to draw a
  font** — but if you want to propose a typeface pairing (one display face for headers with real
  character, one highly-legible face for numbers), that would be welcome.
- The Source label tints in code get reconciled against §2.2.

**None of this blocks you.** Author to §3.1 and the assets will be correct whenever the code lands.

---

## 14. WHERE WE ACTUALLY ARE — Tier 1 + 2 reviewed, and what was learned

Two creatures, one arena and the UI kit landed. The verdict: **the STYLE is right; the SPEC was
wrong**, and the spec has been corrected above. Here is the evidence, so the mistakes stay dead.

### 14.1 What the assets got right — keep doing this

| | Result |
|---|---|
| Dimensions, format, transparency | ✅ exact, every file |
| Iron gall contour | ✅ **21–26% of each creature.** The hardest thing to get right, and it is right. |
| Flat hard-stepped fills, no gradients on the body | ✅ |
| Facing right | ✅ |
| **The eye** | ✅ **LOCKED**: one pale slit in Vellum `#EDE3C8`. It is exactly the "strange, dignified, worth keeping" register. **Hold it across all 30.** |
| **Role reads from silhouette** | ✅ **The Defender ANSWERED my open question.** A low wide planted dome vs a raised reaching quadruped — they are unmistakable at a glance. §4.3's mass language works. |

### 14.2 What the spec broke — all three are fixed above

1. **The arena was crushed to 28 of 255 luminance values** by my "L ≤ 33" rule. A real forest — trees,
   canopy, ground, stones — invisible. → **§2.4: deleted. The engine dims now. Paint at full range.**
2. **The creatures are 73–78% one hue + ink, 0% everything else** — because I asked for "a 4-step ramp
   of the Source hex" and got exactly that. → **§2.6: the ramp is FLESH ONLY, 50–60%. Bone hard parts,
   one accent, 3–4 hue families.**
3. **"Keep the fight band quiet" produced literal horizontal bands.** → **§7.1: it is a GROUND LINE and
   three depth layers, not zones. Quiet means low contrast, not no content.**

And one that was not the spec's fault but mattered as much:

4. **The fight sat in the top half with 47% of the screen as empty ground.** No background could carry
   that. **Fixed in code**: a ground line at 67% of the frame, creatures ~90% larger, the bottom third
   is now a foreground layer you get to paint.

---

## 15. WHAT TO DO NEXT — regenerate, in this order

**Everything already delivered needs redoing against the corrected spec.** That is my fault, not yours,
and it is much cheaper now than at asset #30 — which is exactly why the batch was gated at one.

| # | Do this | The one thing that matters |
|---|---|---|
| **1** | **`bg_arena_verdant.png`** — repaint | **§1B.** A painted place. Full range. Gradients, haze, depth, light shafts. NO ink outlines. Ground line at y=728. A real foreground in the bottom third. This is the biggest single visible jump available. |
| **2** | **`crea_nature_atk_whelp_torso.png`** — redraw at **224×320** | **§2.6.** Verdigris flesh ~55%, **bone horns/claws/teeth**, ONE saturated accent eye. Generate at 896×1280, deliver at 224×320. |
| **3** | **`crea_nature_def_bramble_torso.png`** — redraw at **224×320** | Same. Its silhouette already works — keep it, dress it. |
| **4** | Send those three and **STOP.** | I will put them in and screenshot back. |

Then, and only then, the rest of Nature → the UI kit → Vow marks → VFX → Machine/Shadow → the long tail.

---

## 16. STILL OPEN — your call

1. **Is 30 species the right ask**, or fewer species drawn better? With bone and accents each creature
   is now a real piece of work. I would rather have 12 that look like Darkest Dungeon than 30 that look
   like a colour swatch — but the game reaches all 30 cells, so the cost of fewer is greyboxes.
2. **The typeface.** `PixelFont` has to go — it is a pixel font in a hand-drawn game, and at 4× it is
   the last thing on screen that still looks like the old style. Propose a pairing: one display face
   with real character for headers, one ruthlessly legible face for numbers.
