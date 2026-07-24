# Reference Games — Scenes & UI

Resonance Hunter is a **genre hybrid**, so no single game is "the" reference — it pulls from precision
action combat, creature collection, idle/incremental, and roguelite map games at once. This doc names
the best reference per part, and — because our recurring problem is "don't be a TEXT HELL" — compares
them all on one axis: **does the screen read as a SCENE, or as a SPREADSHEET?**

---

## The one axis that matters for us: Scene ⇄ Spreadsheet

Every systems-heavy game sits somewhere on this line. Our whole UI direction is "drag every screen
toward the scene end without losing the depth."

```
SCENE (a place you look at)                                    SPREADSHEET (a table you read)
|--------------------------------------------------------------------------------------------|
Punch-Out!!   Loop Hero   Monster Sanctuary   Hades   Slay the Spire | Siralim   Melvor Idle   NGU / Kittens
Vampire Surv.                                                        |          Idle Champions(menus)  Antimatter Dim.
```

The trap: the games with the **deepest systems** (Melvor, Siralim, NGU) live at the spreadsheet end,
and it's tempting to copy their UI because they're the closest by *mechanics*. Don't. Copy their
systems; copy the **scene-end** games' presentation.

---

## The four north stars

| Game | Why it's a north star | What to take | What to avoid |
|---|---|---|---|
| **Monster Hunter** | The hunt loop *is* our combat loop: read a telegraph, hit weak points, break parts, craft gear from what you broke. | Part-break feedback, weak-point payoff, "the monster is the level." | Its menu-heavy crafting trees and busy 3D HUD. |
| **Siralim Ultimate** | The closest to our Warren: deep creature collection + team **composition/synergy** + endless scaling + pixel art. | The composition-beats-power philosophy; endless "corruption"-style scaling. | Its team UI is a menu wall — reviewers call changing builds "overwhelming." This is our cautionary tale. |
| **Loop Hero** | Proof a systems-dense game can have a **minimal, characterful pixel UI** that still feels like a place. | Restraint: few words, icon-first, everything diegetic. | Nothing — this is the "how to stay visual" model. |
| **Melvor Idle** | The closest by *mechanics* (idle RPG, offline gains, tons of systems) and the clearest warning. | Its offline-progress and idle-loop structure. | **Everything about its look** — it is tabs of tables. This is exactly what you keep telling me not to build. |

---

## Screen-by-screen comparison

### Combat (the two-sided precision fight)
| Reference | How it does the scene | Verdict for us |
|---|---|---|
| **Punch-Out!!** | One big readable enemy, telegraphed tells, dodge/counter opens a weak point. HUD is almost nothing. | **Our exact model.** Keep the enemy big and central, telegraphs unmistakable, HUD minimal. We already do this. |
| **Sekiro** | Deflection builds posture → posture break is the kill. Defense literally *is* offense. | The feel of our core pillar. Make the defend→open-weak-point moment the juiciest thing on screen. |
| **Monster Hunter** | Weak points + part breaks, but a cluttered 3D HUD and a camera to fight. | Take the mechanics (parts, breaks), not the clutter. Our flat 2D read is *clearer* than MH — a strength. |

**Takeaway:** we're closest to Punch-Out with Monster Hunter's part system. That's a strong, uncommon
combination — lean into readability as the differentiator.

### The Warren (creature farm + composition)
| Reference | How it does the scene | Verdict for us |
|---|---|---|
| **Monster Sanctuary** | Monsters shown as animated sprites; combat and teams are "transparent" — you always see what's going on. | The model to emulate: **sprite-forward, legible.** Our four role stations are close in spirit. |
| **Siralim Ultimate** | Team of 6 with deep synergy, but managed through dense menus/lists. | Depth to aspire to, UI to avoid. It's the game we're most like and most at risk of becoming. |
| **Pokémon (party + box)** | 6 party slots + a grid "box" of the rest. | We already use this metaphor: 4 role **stations** (party) + the **pen** (box). Good lineage. |
| **Stardew Valley (barn/coop)** | Animals physically inhabit a visible building; you walk in and they're *there*. | The "creatures live in the den" feeling. Our stations should read as stalls in a real warren, not slots in a form. |

**Takeaway:** the four-station den = Pokémon's party slots + Stardew's "they live in a place," rendered
with Monster Sanctuary's sprite-clarity. Steer away from Siralim's menu density.

### Memory Dust (prestige tree)
| Reference | How it does the scene | Verdict for us |
|---|---|---|
| **Path of Exile passive tree** | A vast **constellation** of ~1,300 nodes; allocate along connected paths; owned nodes light up. | The visual *language* is exactly ours (stars, paths, lit = owned). But PoE is overwhelming — we want its look at 1/50th the size. |
| **Hades — Mirror of Night** | A small, clean board of paired upgrades you toggle; approachable, never intimidating. | The right **scale and clarity** for us. Aim between PoE (aesthetic) and Hades (legibility). |
| **Slay the Spire (map)** | Not a tree, but the same "lit path through nodes" grammar. | Confirms the node-and-line language reads instantly. |

**Takeaway:** we already rebuilt Dust as a PoE-style constellation at Hades scale. That's the correct
target; keep it small and legible, resist adding a hundred nodes.

### The Forge (loot)
| Reference | How it does the scene | Verdict for us |
|---|---|---|
| **Diablo / Path of Exile inventory** | Grid of item icons, rarity color-coded, tooltip on select. | **Our new card grid is this.** Rarity color + icon-first is the genre-standard, correctly applied. |
| **Loop Hero** | Tiny inventory, drag-to-equip, extremely few actions. | The minimalism to hold onto — resist adding forge sub-menus. |
| **Monster Hunter (crafting)** | Deep craft trees from parts, but menu-driven. | We deliberately *don't* need this depth. Sell / merge / dismantle is enough. |

**Takeaway:** the Forge is now genre-correct (Diablo grid) with Loop Hero restraint. Don't let it grow
menus.

### World Map (region conquest)
| Reference | How it does the scene | Verdict for us |
|---|---|---|
| **Slay the Spire (act map)** | Branching node path, clear "you are here," locked vs available nodes, path lines. | Our exact model, currently **linear**. If we ever want more depth, branch it like StS. |
| **Darkest Dungeon (estate/map)** | The map is a *place* with texture and mood, not just nodes. | Our region emblems + parchment map lean this way; push the mood further in polish. |

**Takeaway:** node-clarity from Slay the Spire, place-mood from Darkest Dungeon. We have the bones.

### Idle presentation (the farm runs while you're away)
| Reference | How it does the scene | Verdict for us |
|---|---|---|
| **Idle Champions** | Champions auto-fight in a visible lane — you *watch* the idle happen. | Show the idle, don't just tally it. Our floating production toast + working creatures is this instinct. |
| **Leaf Blower Revolution / Melvor** | Offline-gain popups and number tickers in menus. | Take the offline-summary idea, present it as a scene beat (our "welcome back" + toast), not a table. |
| **Cookie Clicker** | One giant clickable + a rain of numbers. Iconic, but text-forward. | The engagement lesson (visible, satisfying feedback); not the text presentation. |

**Takeaway:** make the idle *visible* (creatures working, production rising) rather than a stat readout.

---

## Master comparison

| Game | Scene↔Sheet | Pixel art | Systems depth | Idle | What RH borrows |
|---|---|---|---|---|---|
| Punch-Out!! | Scene | Yes | Low | No | Combat readability model |
| Sekiro | Scene | No | Med | No | "Defense creates offense" pillar |
| Monster Hunter | Mid | No | High | No | Part-break / weak-point / hunt→craft loop |
| Monster Sanctuary | Scene | Yes | High | No | Sprite-forward creature/team clarity |
| Siralim Ultimate | Sheet-ish | Yes | Very high | No | Composition depth + endless scaling (UI: avoid) |
| Loop Hero | Scene | Yes | High | Semi | Minimal-but-deep pixel UI restraint |
| Path of Exile | Sheet-ish | No | Very high | No | Constellation prestige-tree language |
| Hades | Scene | No | Med | No | Upgrade-board scale & clarity |
| Slay the Spire | Scene | No | High | No | Node-map world traversal |
| Melvor Idle | Sheet | No | Very high | Yes | Idle/offline structure (UI: the warning) |
| Idle Champions | Mid | No | Med | Yes | Visible idle combat lane |

---

## The five concrete "steals"

1. **Punch-Out's restraint in combat** — enemy big and central, HUD near-invisible, the read is the game.
2. **Monster Sanctuary's sprite-forward team screen** — never a list where a sprite would do (the Warren).
3. **Hades-scale, PoE-styled prestige** — a *small* constellation that stays legible (Memory Dust).
4. **Diablo's rarity-coded icon grid** for loot; **Loop Hero's restraint** so it never grows menus (Forge).
5. **Idle Champions' "watch it happen"** — make idle output visible on the scene, not tallied in text.

## The one anti-pattern to remember
**Melvor Idle / Siralim menus** are what "TEXT HELL" looks like when a great systems game ships a
spreadsheet UI. Every time a screen starts becoming rows of `NAME  VALUE`, that's the smell — reach for
the scene-end reference instead.
