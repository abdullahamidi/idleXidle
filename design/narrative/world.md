# The World of IDLExIDLE

> **Status**: Current · **Created**: 2026-09-09 · **Owns**: what everything in this game IS.
> Every claim here is checked against the code that implements it, and the sections say which file.
> Where the fiction and the code disagree, **the code is right and this document is the bug.**

Playtest, 2026-09-09: *"Let's write a story for the game and ensure that descriptions, return
messages, hunter info, etc., all align with this lore."*

There was no story. `design/art/art-bible.md` states it plainly — *"Narrative is deliberately thin for
MVP. Environment art builds history through systems and scars, not plot or text"* — and the world was
whatever a reader could reconstruct from names. That is not nothing: the mechanics already imply a
coherent place, and it had simply never been said out loud. This document says it, and it invents as
little as it can get away with.

---

## 1. The spine, in one paragraph

The world runs on six pressures. Where one of them lies too close to the surface it rewrites what is
above it into itself, so an older order built a network of **joints** to hold those places level. A
joint works by *remembering*: it holds the shape the place above it should have and keeps asserting
it. The network failed. The joints tore, and what rises through a torn one is **corruption** — a
Source coming up through a place that has forgotten what it was.

A **Hunter** is a person the network learned by heart. She goes down and she **stands**, and standing
long enough is what makes a joint remember. That is why conquest is holding a depth rather than
killing anything, and why an auto-battler is the right shape for this game: the Hunter is not a
weapon, she is the reference the joint measures itself against.

---

## 2. The fall, and why she gets up

**This is the loop's central event and it had no fiction at all.** The game says only *"WHEN IT
FALLS, IT GETS UP AND GOES AGAIN. YOU LOSE NOTHING."*

A Hunter is bound into the network. It has her written into the groove the way it has a hillside, and
when she dies it re-asserts her the way it re-asserts a wall — because putting things back is the
only thing it was ever built to do. Three rules keep this from being cheap:

- **It restores the shape, not the walk.** Everything the network holds comes back — training, gear,
  the build, the tree, what she has sworn. The one thing it never held is *how far down she got*, so
  "nothing is lost, only time" is a literal description of what a memory can restore.
- **Nothing about it is kind.** It does not notice her. It would do the same for a wall.
- **Every fall is one more thing a failing seam has to hold.** That is what `FELL 4 TIMES` on the
  return screen is actually counting.

The screen's own line is the one this is written to fit: **FELL AT WAVE 40 — NOTHING IS LOST.**

---

## 3. Corruption is pressure, and a joint is a valve

Two shipped behaviours had to be reconciled, and this reading makes both true rather than choosing:

- A torn joint **leaks**. That is local, per region, and mastery re-seals it — the art bible's
  reading (*"a failed ward network... corruption tore an existing seam open"*).
- A whole network can be **opened on purpose**. That is global, voluntary and reversible — the
  runtime's reading (`Regions.cs`: `CanDeepenCorruption` requires every region conquered;
  `CanEaseCorruption` is always allowed).

You cannot open a valve you cannot close, which is exactly why deepening waits for the whole world
and easing never does. `PeakCorruptionTier` never falls because **the network remembers having been
opened**, which is also why the deepening award and WEAVER key off the peak rather than the current
tier.

**First you stop the leaks. Then you open the taps yourself.**

---

## 4. The six Sources

BODY, NATURE, MACHINE, MIND, SHADOW, SPIRIT: six load-bearing pressures the world is made of. Not
gods, not elements in a lookup table. The ring — each strong against the next two (`SourceMatchup`) —
*is* the fiction: there is no strongest Source for the same reason there is no strongest direction.

Six regions, one per Source: the six worst places, where the natural balance is thinnest and the
joints were built.

**A region's roster is its own Source and two others** (`BandCycles.RosterFor`), and the pairs are
authored per region rather than derived from the ring — Cinderworks leaks Nature and Shadow, the
Marrow Wastes leak Spirit and Nature. The fiction is that a torn joint lets its neighbours on the
*line* through, not its neighbours on the wheel; which neighbours is a fact about how the network was
laid, and the table is the record of it.

---

## 5. Why waves come

Pressure has to take a shape, and it takes whatever is nearest — bone, gear, or nothing solid at all.
It never stops, because the pressure never stops: there is no garrison to destroy, and killing fixes
nothing. **Standing does.**

Every fifth wave the pressure gathers into one shape instead of many; that is a **boss**. A **chest**
is what a joint gives up when its best attempt fails, which is why only a boss can drop one and why
the region decides what is inside. About one boss in five leaves one (`ChestTuning.DropChance`).

Most of what comes up has no name, because it has no history. The fallback the arena draws for a
creature it cannot name is **CORRUPTED**, and that is honest.

---

## 6. Gleam, Memory Dust, and the two currencies

**Gleam** is what a corrected joint gives off — the world briefly being sure of itself, collected.
It explains what the economy already does: every cleared wave pays instantly with no roll, because a
correction either happened or it did not; and it is spent on Training and the Forge, because the
world's certainty becomes your own shape.

**Memory Dust** is the remembering itself, shed fine by a joint that has been asserted thousands of
times. It buys Warren upgrades, because the camp runs on the same material the network does. And it
buys a deeper start, which is the fiction's best moment: **a checkpoint is paying the world to
remember you were already down there.** `Checkpoints.cs`'s own line — *"Dust buys back the waves you
have already walked"* — becomes literally true, and the skipped waves pay no haul because you only
asserted that you walked them.

---

## 7. The Warren

The settlement dug into the first joint that ever held, in the Verdant Hollow. **People, not
animals**: nothing hatches, nothing is bred. Its people work the joint, and what the joint sheds is
what they bring up.

Its production is **continuous** — it does not wait for you to leave, and the code does not model it
that way. What the camp does specially while you are gone is **hold the mend**: `OfflineCamp` caps
how much of an absence counts (two hours fresh, twelve at most) and at what share of live pay
(30% to 60%). Both grow with **facility levels bought**, not with an abstract "Warren level", which
is what the shipped line means: *THE CAMP HELD 2H OF THE 9h 40m AWAY — EACH WARREN LEVEL HOLDS MORE.*

Facility names are unchanged except one. **BREEDING CHAMBER → SOUNDING CHAMBER**: it cannot be read
any way but livestock, and its Essence now comes from striking the old works and holding the note.
The enum member — the save key — is untouched.

---

## 8. A Vow, a Keystone, a Trait

**A Vow** is sworn to the work, and the joint is what hears it. The network was itself a promise its
builders could not keep; a Vow is a smaller promise a Hunter *can* keep, made where the big one
broke. Nobody is on the other end. It pays because a joint reads a kept promise as an assertion of
shape — the same thing standing in a wave is, smaller and slower. That is why a Vow's demand is
answered at the workbench before the descent, and why nothing hears a promise the moment it is made
(`ProofWaves`).

**A Keystone** is one rule out of the old method — a doctrine its builders would unbalance everything
else to obey. Six joints were laid under six different rules, and a joint torn open and mended by
hand makes its own logic legible. Every keystone is a trade, because that is what a doctrine is.
Keystones are free and the SOCKET is scarce: reading a rule is not living by one.

**A Trait** is what the work leaves in a person. It cannot be bought, which is why the game says so
outright — *THEY AWAKEN, THEY ARE NOT BOUGHT*.

---

## 9. The Hunter, and the ten

**HUNTER is the canon word.** The UI already says it thirty-three times; CHAMPION survives only as a
C# type name and in comments, the way `AttackPower` survives behind the word MIGHT. Nothing needs
sweeping.

The ten are **people**; Hunter is the role. They have no personal names, only the shape they cut,
because the network remembers shapes and never cared what anyone was called. Switching costs nothing
and resets nothing because the training, the gear, the tree and the sworn promises belong to the
*network*, not the person: the joint holds them, and whoever is bound in wears what it holds.

**Gear is not universal, and the fiction must not say it is.** Five classes, and a Hunter wears their
own — `ItemClasses` refuses the rest, and the roster screen says so. Five ways of standing in a seam,
which is what keeps FIRST OF THE WARDENS true.

**RESONANCE** is how loudly you speak in a Source. MIGHT is a body hitting a thing — the basic swing
carries no Source, no signature and no matchup — while a skill is a Hunter drawing on a Source, and
RESONANCE is how much of it answers. That is exactly why the stat lifts woven skills and never the
auto-attack. The word outlived the game's old title honestly.

---

## 10. The six regions, as the map now says them

Promoted from C# comments to `RegionDefinition.Blurb` and drawn on the map's detail column. The
second sentence of each carries what `CombatBias` actually does, so the sentence and the fight cannot
drift.

| Region | Blurb |
|---|---|
| **VERDANT HOLLOW** (Nature, balanced) | The first joint that ever held, and the one the camp was dug into. What rises here comes evenly, and teaches you what even feels like. |
| **CINDERWORKS** (Machine, heavy) | A works that went on making after there was nobody left to make for. It hits slowly and it hits hard, and it can afford to wait. |
| **UMBRAL REACH** (Shadow, fast) | The seam here tore wide and low, and what comes through never gathers. Many small bites, quickly, from more directions than you have answers. |
| **MARROW WASTES** (Body, heavy) | Bone took the shape the pressure asked for and kept it. Everything here is heavy, and it swings like it has nothing to spend. |
| **THE STILL ARCHIVE** (Mind, fast) | Where the network kept what it knew. The knowing is still here and it is awake. It cuts precisely, and often, and it does not need to be strong. |
| **THE PALE CHOIR** (Spirit, balanced) | The last joint on the line, and the one nobody came back from mending. It asks for everything at once and it does not stop asking. |

**No mechanic is invented.** An earlier draft gave Cinderworks a miss chance; there is no miss chance
anywhere in the model, and the line was cut.

---

## 11. The return screen's voice

`WelcomeSummary.Story()`. Every line is a figure the model actually produced — no NOTABLE block, for
the reason the file was written with: offline levels no skills, drops no items and completes no sets,
and a line the model cannot back is a lie. A line whose figure is zero is not printed.

```
YOU WERE GONE {AWAY}. THE HUNTER KEPT STANDING.
{WAVES} WAVES HELD. THE WORLD PAID {GLEAM} GLEAM FOR THEM.
{N} OF THEM GATHERED INTO SINGLE SHAPES. ALL OF THEM FELL.
IT FELL {FALLS} TIMES, AND EACH TIME THE WORLD PUT IT BACK.
DEEPEST IT STOOD: WAVE {DEEPEST}.
THE WARREN WORKED THROUGH ALL OF IT — {WARREN PARTS}.
PICKING UP AT WAVE {RESUME}
```

The first line is emitted for any credited absence and is true of the credited part of it, which is
the part the panel is about; the camp's own sentence beneath says when the absence outran what the
camp could hold.

---

## 12. What this document does NOT license

- **Renaming an id.** Sources are persisted by name; region, quest, vow, trait, keystone and champion
  ids are save keys. Names are free; ids are not.
- **Breaking a pinned string.** Exact-match tests hold the champion tier lines, the quest demands,
  the onboarding hints (including `AN UPGRADE IS AFFORDABLE — NURSERY`), the item-class lines, the
  item presentation labels, the enchant blurbs, `THE WORLD IS YOURS.` and more. `tools/check_skill_doc.py`
  regenerates the skill tables from the catalogue; run it with `--write` after any skill rename.
- **Blowing a copy budget.** Enchant blurbs cap at 50 characters, chest dossier lines at 52, tour
  card bodies at `MaxBodyChars`, and the Warren's rail throws if its copy needs more than two lines.
- **Treating a STALL as a death.** `WaveOutcome.Stalled` is the tick ceiling, not a fall; the log
  tints it gold and its comment says *the hunter stood*.
