# Where the game is explaining in words what it could show

> **Status**: Proposal · **Written**: 2026-09-09 · **Asked for by**: the designer, during the
> typography pass — *"tell me the places you find with too much text, where a visual explanation
> would be simpler, and we will polish them in the next session."*

Two of these are already done, because they were unarguable. The rest are listed and nothing has
been changed. Each entry says what is on screen now, what the picture would be, and what it costs.

The measurement behind the list: **384 single-line strings of 55 characters or more** reach the
player, and they are not spread evenly.

| File | Long lines | What they are |
|---|---:|---|
| `SkillCatalogue.cs` | 105 | every skill, variation and reinforcement's effect |
| `Onboarding.cs` | 63 | tour cards, hints, reveal notices |
| `TraitCatalogue.cs` | 29 | what each trait does |
| `Vows.cs` | 26 | what each vow demands and pays |
| `MasteryCatalog.cs` | 12 | node effects |
| `Regions.cs` | 12 | region blurbs |
| `Tutorial.cs` · `CharacterRoster.cs` · `ElementSets.cs` | 11 each | rungs, innates, set bonuses |

---

## DONE — the two that were not a judgement call

### 1. The Warren's header said two numbers in three sentences

**Was.** Three wrapped sentences: what share of live pay the camp keeps while the game is closed,
what each level adds to that share, and how long a closed game it can hold — the last one printed
whether or not there had been an absence to report.

**Now.** Two meters. Each bar's full length is that figure's real ceiling, so *how much better can
this get* is answered by the shape instead of by a clause saying UP TO. Both figures stay printed
beside their bars.

Both quantities are bounded and both move with every facility level, which is exactly the shape a
bar is for. The foot row is now only about a real absence, and says nothing when there has not been
one.

### 2. A Warren card advertised its upgrade as a downgrade

Not a text-for-picture swap, but found while looking at one, and worse than what I was looking for.
A NURSERY at level 18 read **+92 /min** over **NEXT LEVEL +91.7 /min**. Two figures for the same
quantity on the same card, formatted by two different rules — the top one rounded to whole numbers,
the bottom one dropped to tenths when both rounded together.

Now one call formats both, walking from whole numbers to hundredths and stopping at the first
precision that tells them apart. Every card reads as a genuine increase. It also fixes the same
fault under ten a minute, which the old rule never covered at all.

---

## PROPOSED — ranked by what they would buy

### 3. A skill's shape is described three times in prose · `SkillCatalogue.cs`, 105 lines

The heaviest concentration of text in the game, and the one the player reads most often. A skill
carries a base sentence, two variation sentences and six reinforcement sentences, each of which
restates the same handful of dials in words:

> *"Fires on a clock, not on a count: three shots across the wave every 7s. No skill rate bonus
> makes the clock come round sooner."*
>
> *"Every shot lands twice on two enemies instead of once on three, each a sixth heavier, and the
> clock is half a second shorter."*

**The picture.** A small fixed diagram on the skill card — targets across, hits per target as pips,
and the beat as a row of notches — redrawn for the variation under the cursor. The player then reads
the *change* as a change of shape and only reads the sentence for the parts a shape cannot carry.

**Cost.** The largest item here. The dials already exist on `SkillDef` and the fight already reads
them, so nothing has to be authored: the diagram is a projection of the definition. The risk is that
a shape which does not match the sentence would be a new way to be wrong, so it needs a gate that
sweeps the catalogue the way `check_skill_doc.py` already does.

### 4. TRAINING explains each stat in three paragraphs, and the third is the table underneath

Every stat's card ends with a sentence like *"With your weapon, keystones and tree counted in, one
basic attack hits for 96. This is the number the fight uses."* — directly above a CURRENT STATE
block that reads **NOW 96 · AFTER ONE RANK 98**. The same number, twice, three lines apart.

**The change.** Delete the third paragraph on the stats where the state block already carries the
figure. This is a deletion, not a picture, and it is the cheapest item on the list.

**Why it is not already done.** Two of the seven stats put something in that third line the state
block does not carry, so it is a per-stat judgement about what the player loses, and that is yours.

### 5. DEFENSE explains a formula in a sentence

> *"DEFENSE shrinks every hit you take. The rule: damage is multiplied by 100, then divided by 100
> plus your DEFENSE."*

A player cannot use this. What they want to know is whether the next rank is worth buying, and the
answer is a curve that flattens.

**The picture.** A small curve with the player's position marked on it, and the next rank marked a
step along. It would say *diminishing* without the word, which is the one fact the sentence never
states.

### 6. The chest reveal's item cells carry a rules line each

Every drop prints its name and its verbs. What a player actually wants at that moment is *is this
better than what I am wearing* — which the game computes, and which the EQUIP button now carries as
a verdict word.

**The picture.** The verdict as a shape: one arrow up or down against the worn piece, in the ink the
game already uses for better and worse, on the item's own cell. The row of drops then reads as a row
of verdicts before a single word is read.

### 7. The tour cards are forty screens of prose

Item 2 of the previous playtest already asked for this — *"find more intuitive, non-text ways to
explain things"* — and the answer that session was to make the player perform the deed instead
(`TutorialStep.SetAGem`). That worked, and it only covers one card.

**The picture.** The cards that describe a *place* (the nav rail, the currency pills, the toolbar)
could be an arrow and three words rather than three sentences. The cards that describe an *action*
should follow the gem's example and become a deed.

**Cost.** Moderate, and it touches the vocabulary ledger — a card that stops saying a word may be
the card the ledger names as that word's introducer, and the test will say so.

### 8. Region blurbs are two sentences where one is a combat fact

`Regions.cs` gives each region a blurb whose second sentence carries what `CombatBias` does:

> *"It hits slowly and it hits hard, and it can afford to wait."*

**The picture.** Two small dials on the region card — weight and pace — beside the sentence's first
half, which is the one that carries the place rather than the numbers. The bias is already a typed
value, so the dials cannot drift from the fight.

### 9. Vows state a demand and a payment in one sentence each · 26 of them

> A demand is a condition with a state — met, not met, how close — and a payment is a multiplier.
> Both are drawable. The screen already draws a seal per vow and tints it; the seal could carry the
> progress toward the demand rather than only its verdict.

### 10. The locked skill tile's last two characters

The one truncation the type pass could not remove: `LOCKED · LEVEL 3` in a 140-pixel tile at the
150% profile, which cuts to `LOCKED · LEVE…`. It is at the ladder's floor already, so the fix is not
a smaller size.

**The picture.** The kept level becomes a small gold chip in the tile's corner and the line reads
just `LOCKED`. That satisfies the law the line exists for — no word may imply the levels are gone —
in less room than the sentence needs.

---

## Not on this list, and why

**Item and affix text.** `ItemPresentation.Rows` already owns every item number with its stat word,
and the Gear inspector already draws them as rows with values. That is a table, not prose.

**The fight's callouts.** Three sizes of floating number and a word. Already the most visual thing in
the game.

**The trait sigils.** Twenty-six constellations drawn from each trait's own id, plus a sentence. The
picture is already there; the sentence is what it does, which no shape can carry.

**Keystone text.** Nine long lines, but each is a *trade* stated in two clauses, and a trade with a
cost is the one thing this game should always spell out.
