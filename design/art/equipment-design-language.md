# Equipment Visual Design Language

> **Purpose**: so an item's *look* reads its *mechanics* — a crushing weapon looks heavy, a precise one
> looks needle-sharp, a protective charm looks plated, an evasive one looks light and slippery. This maps
> every real in-game item property to a concrete visual direction you can author to.
> **Style**: the locked rich-vector direction (jewel-tone gradient, faceted, rim light, no outline).
> **Canvas**: 512×512, transparent, icon fills ~72% centred (a `frame_<rarity>` sits behind it).

---

## 0. What actually defines an item (the things you're dressing)

Every item is built from six axes. Two are the *soul* (drive the silhouette), the rest are accents:

| Axis | Count | Drives | Where it comes from |
|---|---|---|---|
| **Slot** | 8 | the base shape | weapon / charm / focus / helm / chest / gloves / boots / ring |
| **Trait** (the *trade*) | 10 | **the dominant motif** | id-derived; one of the slot's pool (§8) |
| **Element** | 6 | **palette & material** | the item's Source |
| **Enchant** (Rare+) | 11 | a build-trigger *accent* | id-derived; one of the slot's pool (§8) |
| **Affixes** | 6 stats | tiny stat accents | rolled, 0–4 by rarity |
| **Rarity** | 5 | ornamentation tier + frame | Common → Legendary |

**The trait is the item's soul.** It's a *trade* (every trait gives something and costs something), and it's
what your examples are about. The element is the skin (colour/material). The enchant is a small glyph that
says "this rewires a build". Design an icon as: **slot silhouette → shaped by its trait → skinned in its
element → marked with its enchant → framed by its rarity.**

---

## 1. Your four examples → the game's real properties

Your instinct is exactly right; here's how it lands on the actual mechanics:

| You said | The game's property | Visual direction |
|---|---|---|
| "ateş hasarı vurdurma" (deals damage over time) | **Venom** enchant (skills poison) / the damage traits **Keen·Heavy·Savage** | hot, aggressive, dripping — embered edges or venom-green ichor |
| "delici" (piercing) | **Keen** trait (clean, precise hits) | needle-thin, honed, a single perfect point; minimal mass |
| "defansını arttıran" (extra defense) | **Warding** trait / the **Defense** affix | thick plate, riveted, heavy bevels, a bulwark silhouette |
| "kaçınma sağlayan" (evasion) | **Swift** trait (fast, hits land softer) | light, aerodynamic, soft/slick material — ribbon-thin, wind-swept |

So the rule is: **the trait is the mechanic you draw.** Below is the full dictionary.

---

## 2. The 6 elements — palette & material (the skin over everything)

Author the icon's *form* neutral-to-mid value; the element sets its colour and surface. (I can also tint a
neutral icon per-element in-engine — tell me if you'd rather author one neutral set and let code colour it.)

| Element | Hex | "Feel" | Material / surface | Signature motif |
|---|---|---|---|---|
| **Body** | `#D6485C` kermes | blood, sinew, muscle | warm bone & red meat, raw | veins, tusk, heartbeat pulse |
| **Mind** | `#74C6E8` lapis | thought, psionics | cool glass, crystal, circuitry-of-light | eyes, geometric sigils, lattice |
| **Nature** | `#48B888` verdigris | growth, rot, life | living wood, moss, chitin | vines, leaves, thorns, spores |
| **Machine** | `#BC7840` bole | forge, gears, industry | bronze, brass, oiled iron | cogs, rivets, pistons, vents |
| **Shadow** | `#52457E` woad | void, secrecy | smoked obsidian, ink, dusk-violet | smoke wisps, cracks of dark, fangs |
| **Spirit** | `#DCD4EC` bone-ash | soul, memory, the pale | porcelain, ash, cold light | halos, wisps, filigree, ghost-glow |

---

## 3. The 10 traits — the TRADE (the dominant motif)

Each trait is `benefit / cost`. **Let the silhouette carry the trade** — a Heavy weapon should look like it
would tire your arm; a Swift one like it barely has weight. Traits are pooled by slot (§8), so a given slot
only ever shows its four.

### Weapon traits
| Trait | Trade | Draw it as |
|---|---|---|
| **Keen** | cleaner hits, *no drawback* | the **piercer** — needle-thin, a single honed point, minimal ornament, surgical |
| **Heavy** | far harder hits, skills slower | the **crusher** — huge head, blunt mass, thick haft, it *sags* with weight |
| **Swift** | skills faster, hits softer | the **evader** — ribbon-light, streamlined, slotted/hollowed to shed weight, wind-lines |
| **Savage** | brutal hits, squad frailer | the **feral** — jagged, serrated, barbed, cracked, blood-slick, asymmetric |

### Charm traits
| Trait | Trade | Draw it as |
|---|---|---|
| **Warding** | tougher squad, hits softer | the **aegis** — a plated ward-sigil, layered, riveted, a closed protective shell |
| **Vital** | tougher squad, *no drawback* | the **lifebead** — a warm swelling core, a heart/seed, gently radiant, rounded |
| **Greedy** | richer haul, frailer squad | the **hoard** — gold, coins, a gem too big for its setting, dripping avarice |
| **Wild** | everything up, health *hard* down | the **gamble** — unstable, over-charged, cracking at the seams, arcing energy |

### Focus traits (also the default pool for Helm/Chest/Gloves/Boots/Ring — §8)
| Trait | Trade | Draw it as |
|---|---|---|
| **Attuned** | skills faster, *no drawback* | the **resonator** — clean concentric rings, a tuned harmonic, calm and balanced |
| **Focused** | skills *far* faster, hits softer | the **lens** — a tight converging eye/beam, everything narrowing to one point |
| **Swift** | (as above) | the **evader** motif, in the slot's silhouette |
| **Greedy** | (as above) | the **hoard** motif, in the slot's silhouette |

*(“No-drawback” traits — Keen, Vital, Attuned — are the deliberately weaker ones; keep them elegant and
restrained rather than showy. The showy, dangerous look belongs to the high-ceiling trades: Heavy, Savage,
Wild, Focused.)*

---

## 4. The 11 enchants — the build-trigger accent (Rare and better only)

An enchant is a **small secondary glyph/effect** on the item (a corner emblem, an aura, an inlay) — it says
"this rewires a build". Six of them are **Form-combo** enchants: they only matter if the player runs that
Form, so give each a glyph that echoes its Form's icon.

| Enchant | Slot pool | Effect | Accent motif |
|---|---|---|---|
| **Splinter** | weapon | on wave clear: richer loot | shattering shard, a burst of loot-glints |
| **Harvest** | weapon | on wave clear: a spare core | a budding seed / spare core pip |
| **Venom** | weapon | skills poison over time | dripping green ichor, a toxic sheen |
| **Undying** | charm / ring | survive a fatal blow once | a phoenix ember / unbroken loop |
| **Desperation** | charm / ring | near death: haul swells | a cornered, last-stand spark; cracked-but-blazing |
| **Siphon** | charm / ring | Transformation leeches 2× | draining tendrils pulling inward (echoes MORPH) |
| **Overdraw** | focus / armour | Volley fires +1 | an extra drawn projectile (echoes VOLLEY) |
| **Linger** | focus / armour | Mark lasts longer | a persistent after-glow brand (echoes MARK) |
| **Radiance** | focus / armour | Aura ticks faster | fast concentric pulse rings (echoes AURA) |
| **Execute** | focus / armour | Strike executes the low | a finisher edge / guillotine notch (echoes STRIKE) |
| **Coiled** | focus / armour | Trap re-arms fast | a wound spring / snap-jaw (echoes TRAP) |

---

## 5. The 6 affixes — minor stat accents (optional, subtle)

Affixes are small rolled bonuses (0–4 of them by rarity). They don't need to reshape the icon — at most a
tiny inlaid gem/rune in the item's element colour hints at them. If you want cues:

| Affix | Cue |
|---|---|
| **Damage** | a red edge-glint / ember chip |
| **Health** | a green swelling bead |
| **SkillRate** | a blue tuned ring |
| **Haul** | a gold fleck |
| **Crit** | a sharp white spark |
| **Defense** | a grey plate stud |

---

## 6. Rarity — ornamentation tier (the frame already carries the colour)

The `frame_<rarity>` behind the icon sets the rarity colour. Escalate the icon's **ornamentation and glow**
with rarity, never its readability:

| Rarity | Ornamentation |
|---|---|
| Common | plain form, matte, no glow |
| Uncommon | one accent, faint rim light |
| Rare | clean inlay + a soft elemental glow; **first rarity that shows an enchant accent** |
| Epic | layered ornament, stronger rim + inner glow, small particles |
| Legendary | full filigree, molten/animated glow, an unmistakable "event" silhouette |

---

## 7. Per-slot base silhouettes (the shape before the trait bends it)

| Slot | Base silhouette |
|---|---|
| Weapon | a bladed/hafted arm — sword/axe/spear reads at icon size |
| Charm | a hung talisman/amulet on a cord |
| Focus | a floating sigil/orb/lens (the ability catalyst) |
| Helm | a face-forward headpiece |
| Chest | a torso cuirass/robe front |
| Gloves | a paired gauntlet/bracer |
| Boots | a paired greave/boot |
| Ring | a band with a set stone |

---

## 8. Which trait & enchant can appear on which slot (author only valid combos)

The code only ever rolls these — don't author combinations that can't occur.

| Slot | Trait pool | Enchant pool (Rare+) |
|---|---|---|
| **Weapon** | Keen · Heavy · Swift · Savage | Splinter · Venom · Harvest |
| **Charm** | Warding · Vital · Greedy · Wild | Undying · Desperation · Siphon |
| **Ring** | Attuned · Focused · Swift · Greedy | Undying · Desperation · Siphon |
| **Focus** | Attuned · Focused · Swift · Greedy | Linger · Radiance · Overdraw · Execute · Coiled |
| **Helm · Chest · Gloves · Boots** | Attuned · Focused · Swift · Greedy | Linger · Radiance · Overdraw · Execute · Coiled |

*(Note: today all armour + Ring + Focus share the Focus **trait** pool. If you'd like armour to have its own
armour-flavoured traits — e.g. a defensive pool for Chest/Helm — say so and I'll add a real pool; it's a
small code change and would make the "defense = heavy plate" reading land on the pieces where it belongs.)*

---

## 9. How to author it without drawing thousands (recommendation)

Full slot × trait × element × enchant × rarity is thousands of icons — don't. The tractable, mechanic-
reflective model:

1. **Trait drives the icon.** Author **`item_<slot>_<trait>.png`** — the slot silhouette bent by its trait
   (§3, §7). That's the set that makes a Heavy weapon look heavy. Count ≈ **32** (Weapon 4 + Charm 4 + the
   6 Focus-pool slots × 4). Filenames use the enum names lowercased, e.g. `item_weapon_heavy`,
   `item_charm_warding`, `item_focus_attuned`, `item_helm_swift`.
2. **Element = palette.** Author the trait icons **neutral** (grey/white, mid-value) and I tint them per
   element in-engine — one neutral set, six colours for free. *Or*, if you want bespoke element art, author
   `item_<slot>_<trait>_<element>` and I'll use it when present (no substitution — a missing one falls back
   to the neutral+tint).
3. **Enchant = a shared accent glyph.** Author **`ench_<kind>.png`** (11 small 256² glyphs, §4). I overlay
   the item's enchant glyph in a corner on Rare+ items. Reused across every slot — 11 files, not per-item.
4. **Rarity = the frame** (done) + your ornamentation escalation inside the icon (§6).

**Total new ask for full expression ≈ 32 trait icons + 11 enchant glyphs = 43 files** (plus optional bespoke
element art). That replaces today's 8 generic `item_*` icons and makes every drop read its mechanics.

When you've authored a batch, I'll wire the selection (`item_<slot>_<trait>` → element tint → enchant
accent → rarity frame), keeping the strict no-substitution rule: any icon you haven't drawn yet keeps the
current generic `item_<slot>` art until it exists.

---

## Quick reference — everything at a glance
- **Slots (8):** weapon charm focus helm chest gloves boots ring
- **Traits (10):** Keen Heavy Swift Savage · Warding Vital Greedy Wild · Attuned Focused
- **Enchants (11):** Splinter Harvest Venom · Undying Desperation Siphon · Overdraw Linger Radiance Execute Coiled
- **Elements (6):** Body Mind Nature Machine Shadow Spirit
- **Affixes (6):** Damage Health SkillRate Haul Crit Defense
- **Rarity (5):** Common Uncommon Rare Epic Legendary
