# Build Identity — Proposal

**v2 (2026-08-20): the designer named the true north — Hunter x Hunter's Nen system.** "Oyuncunun
seçtiği uzmanlık alanı, yeteneklerini doğrudan etkilemeli. Yetenekleri aslında bir uzmanlığın ürünü
olmalı." Discovery: the game already carries the Nen skeleton — six Forms map to the six categories,
the affinity hexagon EXISTS and is live in the sim (AffinityFactor x2.0/x1.15/x0.75/x0.45, sourced
from the mastery tree's Specialisation), and Vows are the restriction rule. What was missing was
VISIBILITY and the buy-back rule; both shipped in 7546c4a (discipline named on BUILD, verdicts on
every Form cell and woven slot, and A SWORN VOW PULLS AN OFF-DISCIPLINE SKILL ONE RING CLOSER).

Remaining moves below are now read THROUGH the Nen frame: signatures = each Source's Hatsu flavour;
move 2's capstones = advanced techniques of a discipline; the water-divination moment = making the
FIRST Specialisation pick a ceremony. Move ordering unchanged; the ceremony is a cheap, high-value
insert before move 1.

**Raised:** playtest 2026-08-20, item 5. **Status:** MOVES 1+2 SHIPPED (2026-08-20) — signatures
live for all six Sources, matchup shrunk to x1.15, CHARGE + four keystones (REND / DYNAMO /
CAPACITOR / LODESTONE) one per dust road, THE ATTUNEMENT ceremony and the hexagon diagram on the
tree page. The capstone REDESIGN half of move 2 was judged unnecessary on inspection: the ring-4
masteries (OVERWHELM / EVERYWHERE / FIRST STRIKE / ENDLESS) are already rule-changers. Move 3
(bands over counters on the MAP) remains open.

> "6 adet element 'X, Y'den üstündür, Y kasarken X'e döndür' gibi bir mantık çok basit kalıyor.
> Bunun yerine oyuncu gerçekten hoşuna giden buildi yapıp bunun min/max'ı için uğraşmalı."

## Diagnosis

The game already owns FIVE build axes — Source (element), Form, mastery branch (Weight / Spread /
Tempo / Endure), keystones + Vows, and the item layer (prefix / enchant / gems / family). The problem
is that only ONE of them talks to the fight through a *rule* (the mastery shape); the element talks
through a single matchup multiplier, which reduces the deepest-looking axis to "switch to the
counter". A build feels owned when its axes **interact**, and when at least one axis changes *how*
the fight plays rather than *how much*.

## Proposal, in three moves (ordered; each shippable alone)

### 1. Elements become signatures, matchup becomes seasoning

Shrink the elemental matchup multiplier (e.g. 1.25x → ~1.10x) so countering is a nudge, not the
strategy. Give each Source a **signature rider** that fires whenever a skill of that Source lands —
small, mechanical, and composable:

| Source  | Signature (rider on hit)                                          | Fantasy |
|---------|-------------------------------------------------------------------|---------|
| Body    | stacks **RUPTURE** — the target takes +X% from EXECUTE-range hits  | butcher |
| Shadow  | +crit chance against targets under 50% health                     | assassin |
| Mind    | extends any open MARK window by Yms                                | tactician |
| Nature  | leeches Z% of the hit as healing                                   | druid |
| Machine | strips A flat armour for the wave (stacking, capped)              | engineer |
| Spirit  | the NEXT other-Source skill echoes at B% power                    | conductor |

Why this works here: riders are the same shape as the existing **BuildTrigger/enchant Needs** system
(Linger, Siphon, Venom...), so the sim already has the seams. A "Shadow-crit engine" or a
"Spirit-echo rotation" is now a build you *assemble*, not a colour you swap. Region matchups stay as
a **travel decision** (where do I hunt), not a combat decision.

### 2. One shared resource primitive: STACKS the axes can bend

Introduce 1-2 named, visible stack primitives (start with RUPTURE from Body and CHARGE — "every cast
adds 1; some payoffs spend all of it"). Then let the OTHER axes bend them:

- Mastery capstones become **rule-changers**, not percentages: Weight capstone "REND: your Strike
  spends all CHARGE, +X% per point"; Tempo capstone "every 5th cast is free and instant"; Spread
  capstone "your riders also hit adjacent creatures"; Endure capstone "taking a hit grants CHARGE".
- Keystones and Vows read the same words: a Vow like "NEVER HEAL — your riders are 40% stronger"
  turns Nature-leech builds off and glass builds on. A keystone "your CHARGE cap is doubled" is a
  min/max lever, not a stat stick.
- The gem roadmap the redesign left open ("ileride daha karmaşık özellikler") plugs in here:
  conditional gems ("+crit inside a MARK window", "+damage while at full CHARGE") speak the same
  language instead of inventing a new one.

Min/max then has a real texture: pick a signature, pick the branch whose capstone bends it, pick
Vows that pay for what your build never does anyway, and hunt items whose enchants/gems feed the
same loop. That is "the build I like, tuned hard" — the thing the playtest asked for.

### 3. Bands over counters

The creature bands (Swarm / Bruiser / Caster...) already ask build questions ("do I have Spread?",
"can I burst?"). Lean matchup pressure onto BANDS, not elements: regions advertise their band mix on
the MAP, so "this region punishes single-target" is the strategic layer, and the element choice stays
expressive rather than corrective.

## What NOT to do

- Do not add a seventh element or a bigger matchup table — more of the same shallow axis.
- Do not make riders large; they are glue, not the payload. If a rider alone beats a capstone, the
  capstone axis dies.
- Do not gate riders behind items at first — they must be free identity, or new players never see
  the system the game is named after.

## Cost estimate

Move 1 is a Core + tests change (~a session, the sim already has trigger seams) plus tooltips.
Move 2 is the big one (capstone redesign touches MasteryCatalog + balance sweeps). Move 3 is mostly
MAP copy + region tuning. Recommended order: 1 → 3 → 2, playtesting between each.
