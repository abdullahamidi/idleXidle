# Auto-Battler Research — Findings & Pivot Direction

**Status:** evidence base is THIN. 25 claims went to 3-vote adversarial verification; **6 survived, 19
were refuted.** Only **Loop Hero** and **Super Auto Pets** (plus Riot's VFX Style Guide) produced
anything that held up. Nothing survived on Idle Champions, AFK Arena, Idle Heroes, Melvor, Soda
Dungeon, Backpack Battles, Siralim, Monster Sanctuary, Vampire Survivors, Mechabellum or TFT.

Treat this as **a deep read of two exemplars, not a genre survey.**

Explicitly *unanswered*: **charm/personality** (every claim refuted 0-3), **offline/session structure**,
and **how to expose numbers without becoming a spreadsheet**. Also: "spreadsheet UI causes churn" was
**refuted** — that was our assumption and it has no evidence behind it.

---

## The three findings that survived

### 1. Visual fatigue is an attention-HIERARCHY failure, not an effect-count problem `high`

Riot's public VFX Style Guide (2017): *"Each visual effect should match its level of importance to
gameplay."* It names our exact failure: *"if a champion's basic attack effect had the same visual
importance as her ultimate, it can confuse the player… and makes the ultimate less satisfying."*
Stated goal: *"minimize visual clutter"*; artists carry *"the heavy burden of restraint."*

Timing (verbatim, 3-0): *"ALL EFFECTS SHOULD HAVE ANTICIPATION AND DISSIPATION"*, and a whole section
on *"Reducing the Amount of Time Effects Stay on Screen"* — *"We intentionally minimize an effect's
linger duration to reduce visual noise."*

**Implementation correction:** the lever is **total lifetime**, not a snap cut. Riot's curve is main
action in the **first quarter** of the effect's life, then fade across the remaining three quarters.
Do NOT ship 2-frame cutoffs.

**For us:** rank every event and assign a loudness tier. Basic hit = quiet tick, no text. Only
tier-defining events (crit, break, kill) earn anticipation + a callout. *We had flat loudness — the
exact diagnosed condition.*

> **Killer line for the pivot:** in an auto-battler, **anticipation is the only thing that lets a
> spectator predict** — and prediction is what converts *watching* into *reading*. If the roster fights
> itself, telegraphs stop being a combat mechanic and become the entire reason the fight is watchable.

**Honest limits:** the fatigue link is OUR extrapolation — Riot's stated concerns are confusion and
noise, never eye strain or long sessions. LoL is reactive; we aren't. Transfers on attention-economy
grounds, not proof.

### 2. Loop Hero: the unit of play is the LOOP, and depth is two coupled ratchets `high`

Strongest material here (datamined `.ini` values + direct dev interviews).

- **System-side ratchet:** the loop counter raises loot level **and** enemy lethality *from the same
  variable* — **asymmetrically**. Enemies compound ~**5%/loop**; item level rises linearly at
  **loop × 0.8**. So **your gear falls behind BY DESIGN**, which forces a cash-out decision.
- **Player-side ratchet:** you *self-impose* difficulty by placing tiles, judging how much your
  auto-hero can survive. Devs: *"balancing… to gain resources and up the difficulty without sending
  your hero to certain doom."*
- **Why the loop is the unit:** the exit is priced by *where* you leave — **100%** of resources kept at
  camp (loop boundary), **60%** retreating mid-loop, **30%** on death. One meaningful decision per loop.
- **Adjacency is what stops it collapsing:** specific pairings (Meadow beside another tile = +50%
  healing) are what devs credit for changing decisions. Without adjacency it degenerates into one
  "how much can I take" slider.

**The dissent we must heed** (Game-Wisdom): Loop Hero **under-rewards** its risk, so *"you always want
to play it safe"* — and the tension evaporates. **This is the single most likely way our corruption
ratchet fails.**

### 3. Super Auto Pets: the atomic unit is a SHOP TURN, and the budget is the whole trick `medium`

~10 gold per turn (**not carried over**), pets 3g, **5 slots**, roll 1g, **freeze free**; run = ~10 wins.

**Correction from verification:** the budget is **not fixed** — Swan/Goat/Grapes modify it, and Magpie
*breaks the no-carryover rule outright*. Gold generation is a whole build archetype.

> **The bounded budget is what makes gear a decision instead of arithmetic.** With an unbounded budget,
> max stats always dominate. **This is precisely our "I just click the best weapon" bug.**

Depth lives in the **verb set**, not the buy: roll (costs), **freeze (free — free actions with
opportunity cost are where mastery lives)**, sell, combine-to-level.

Also: SAP wins by **depth from a SMALL rule set**, not system count — and has since drifted toward
"too many changes patch to patch" as it added packs. **A direct warning about our own trajectory.**

---

## What this says about OUR game

1. **Our unit of play is the fight. It should never be.** Loop Hero = the loop. SAP = the shop turn.
   Ours is "press R." That alone explains "3 buttons, win, press R."
2. **Our optimization has no budget, so "click the best weapon" is the *correct* play.** We didn't fail
   to teach min/maxing; we built a game with no tradeoff in it.
3. **Nothing in our game is ever at risk.** Retreat = "NOTHING LOST." Conquest is permanent. Prestige
   never resets. Corruption is one-way. **No stakes → no tension.** Loop Hero's entire engine is a
   priced exit; we have no exit and nothing to price.
4. **Our corruption ratchet pays in bigger numbers** → it will land exactly on the Loop Hero failure
   mode: players settle at a safe floor and never touch it. It must pay **in kind** — new synergies,
   new evolution branches, new gear archetypes.
5. **We have too many systems already** (5 roles, branching evolution, 4-role puzzle, gear rarity, 3
   regions, corruption, prestige). The SAP lesson says **make the existing rules interact**, don't add
   a seventh system.

---

## Proposed pivot shape

- **Unit of play = an EXPEDITION**, not a fight. Roster auto-runs a sequence of encounters. You decide
  at the **boundary**, not during.
- **Price the exit.** Bank now (keep everything) / push deeper (better rewards, risk the haul). That
  asymmetry IS the tension, and it's the thing we currently have zero of.
- **Bounded build budget** per expedition → gear/roles become tradeoffs instead of "equip bigger."
- **Verb set over stats:** reroll the offer (costs), **lock/freeze a candidate (free)**, dissolve a
  creature into evolution progress.
- **Adjacency/skill-order as the depth layer** — our 4-role composition is the natural home. Role
  adjacency and skill-order triggers should produce *emergent* effects, not additive stats.
- **Corruption pays in kind**, never in numbers.
- **Anticipation in the auto-fight** is what makes it watchable — telegraphs become spectator grammar.

---

## Open questions the research could NOT answer

1. **Where does charm come from in this genre?** Every claim refuted. Needs a dedicated second pass.
   This is the playtester's complaint we're least equipped to answer.
2. **Does a $10–15 paid Steam game even WANT offline accrual** — or does offline progress remove the
   reason to return? No monetized re-engagement to lean on. Genuinely open.
3. **How do you expose WHY the auto-fight resolved that way without becoming a spreadsheet?** Zero
   evidence either way. **This is our single biggest pivot risk:** optimization requires an explanatory
   surface, and that surface is exactly what turns into a wall of numbers.
4. **Does Loop Hero's ratchet even transfer to us?** Its ratchet is *within-run*, resets per expedition,
   and its tension depends on *losing progress*. We have a non-resetting prestige tree, permanent
   conquest, and a persistent roster. **So what is our loop boundary, what is actually at risk there —
   and if nothing is lost, what supplies the tension?**
