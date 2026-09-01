# Playtest Guide — IDLExIDLE

A script to follow while playing, with the exact keys, what *should* happen, and the specific
questions I most need answered. Every key here is verified against the current build.

```bash
dotnet run --project src/IdleXIdle.Game
```

The whole point of this build is to find out whether the **loop is fun**. It is flat greybox shapes
with a hand-built font on purpose — judge feel and clarity, not looks. Jot notes against the
❓ questions as you go; a one-word reaction is useful.

---

## Full control reference (verified against the code)

**In a fight**
| Key | Does |
|---|---|
| **Click a part** | Aim your next auto-attack there (does NOT attack — attacks are automatic) |
| **Space** | Dodge the incoming attack (0 damage, opens a weak point) |
| **F** | Interrupt the incoming attack (0 damage, opens a weak point — the best answer) |
| **Left Shift** | Block (always available, still takes some damage) |
| **1 / 2 / 3** | Cast an ability (spends Resonance, earned by defending) |
| **C** | Capture a rare creature (only when it's warded to 1 HP) |

**Results screen (after a fight)**
| Key | Does |
|---|---|
| **Q / E** | Select a stat to train |
| **Enter** | Buy one Training rank with Gleam |
| **R** | Next hunt · **B** fight the boss · **F** open the Forge |

**Screens (from anywhere)**
| Key | Does |
|---|---|
| **A** | Automation / idle farm · **P** Memory Dust · **Tab** animation-rig demo · **F1** help · **Esc** quit |

**Forge** (open with F on results): Up/Down move · Space select for merge · **M** merge · **S** sell · **D** dismantle
**Automation** (A): Up/Down select · Enter assign · **H** hatch a core · **F** feed selected · **V** evolve selected · **+/-** automation stage
**Memory Dust** (P): Up/Down select · Enter buy

---

## Scenario 1 — The first fight (the tutorial)

A 5-step tutorial banner appears and advances as you *do* each thing (not on a timer).

1. **Click the FLANK.** The "SELECTED" readout should change; your auto-attacks now land there.
2. Wait for the red **INCOMING ATTACK!** panel and its filling bar. Before it fills, press **F**
   (interrupt). You should take no damage, and a body part should turn **gold** — that's a WEAK POINT.
3. **Click the gold weak point.** Your next hits there should do visibly more damage.
4. Keep hitting one limb until its bar reaches 100% and it reads **BROKEN**.
5. Drop the creature's health to zero.

- ❓ **Did the tutorial make sense one step at a time, or did any step confuse you?**
- ❓ **Did "defence creates the opening" click — that dodging/interrupting is how you get weak points?**
- ❓ **Does the 1.2-second auto-attack rhythm feel good, or sluggish and passive?**
- ❓ Try clicking a part **very fast** vs. once. Damage should be identical. Did that feel right, or did
  you *expect* clicking faster to help?

---

## Scenario 2 — Get something for it (Forge + Training)

After the kill you land on the results screen with an **efficiency %** and **loot**.

1. Note the efficiency bar. The **100% = PAR** mark is a fixed white line. Remember roughly where the
   bar landed.
2. Press **F** to open the Forge. Your loot is listed by rarity with sell values.
3. **Sell** a couple of items (S) to bank Gleam. Then try a **merge**: press Space on three items of
   the **same rarity**, then **M**. Read the "XXg IN → YYg OUT" line first.
4. Press **F** to leave the Forge, then **Q/E** to pick a stat and **Enter** to buy a Training rank.
5. Press **R** and fight again — you should hit slightly harder / survive slightly better.

- ❓ **Does merging read as a decision or a punishment?** (It always outputs less total value than the
  three inputs — on purpose. Is that clear, or does it feel like being cheated?)
- ❓ Is it obvious what to spend Gleam on, and does Training visibly change the next fight?
- ❓ Do the three loot options (sell now / merge up / dismantle for parts) feel like a real choice?

---

## Scenario 3 — Mastery and the idle farm (press **A**)

1. Fight ~5–10 times. Each kill drops a **creature core** (marked ALWAYS DROPS).
2. Press **A** for Automation. Press **H** a few times to hatch cores into creatures.
3. Select creatures (Up/Down) and **Enter** to assign them. Watch the **TEAM QUALITY** and the
   four-box **GENERATOR / CRAFTER / DEFENDER / SUPPORT** checklist.
4. Try assigning **only your strongest creatures ignoring roles**, note the team-quality %. Then swap
   to **one of each role** — quality should jump even at lower total power.
5. Assign at least one **Attacker**, set stage to 3 (press **+**), and watch "LAST SECOND" produce
   kills and Gleam. Leave it a minute.

- ❓ **Did the composition puzzle land** — that a balanced 4-role team beats a stack of strong ones?
- ❓ Is the idle-efficiency readout (a % of PAR, fixed 100% mark) understandable, or just numbers?
- ❓ Does earning automation feel like a reward, or like the game playing itself?

**Offline check:** with a farm assigned at stage 3, **quit (Esc)**, wait ~1 minute, and relaunch. A
gold **"WELCOME BACK — your farm ran for X"** line should greet you.
- ❓ Did returning to offline progress feel good?

---

## Scenario 4 — Evolution (press **A**, then feed & evolve)

1. In Automation, select a hatched creature. It starts as an **ATTACKER** at the Whelp root, and its
   evolution branches are listed with hints.
2. Press **F** repeatedly to feed it materials (also: fighting actively feeds all your creatures and
   tallies part-breaks). Watch a branch's condition get met (its bullet turns gold).
3. When "READY TO EVOLVE [V]" shows, press **V**. Its Role and name should change.

- ❓ Was it clear that **how you play shapes what a creature becomes** (same start → different Roles)?
- ❓ Are the branch hints readable, or cryptic?

---

## Scenario 5 — Rare capture (opportunistic)

Roughly 1 in 16 standard creatures spawns **RARE!** (gold tag by its name).

1. When you get one, fight it down normally. On the killing blow it will **not die** — the Capture
   Ward clamps it to **1 HP** and a gold prompt offers **C to capture**.
2. Press **C**. It may take a few tries (capture is never guaranteed). On success it joins your roster.
3. Alternatively: hit its **CORE** to kill it instead of capturing — that always works.

- ❓ Did the Ward feel like a *mercy that gives you a choice*, or a confusing "why won't it die"?
- ❓ Fighting a boss (B): confirm it's **never** tagged rare and never offers capture. (By design.)

---

## Scenario 6 — Memory Dust (press **P**)

Reach a new region-mastery level (farm a while, or fight a lot) to earn **Memory Dust**.

1. Press **P**. You'll see a finite tree: **X / 19 unlocks, Y / 660 Dust** — a visible horizon.
2. Buy a root unlock (Up/Down, Enter). Note it's permanent — no respec.

- ❓ Does "nothing resets, and you can see the end" feel different from a normal prestige grind?

---

## The big questions (please answer even if you skip scenarios)

1. **Is the core hunt loop fun?** Dodge/interrupt → open a weak point → punish it. Yes / no / almost.
2. **What was the most confusing moment** in your whole session?
3. **What was the best moment** — the thing that made you go "oh, nice"?
4. **Would you press "one more fight"?** If not, what was missing?
5. Anything that felt **broken, unfair, or pointless**?

---

## Known limitations (not bugs — don't report these)

- Everything is flat shapes and text; there is no art, animation, or sound yet.
- Only one region (Verdant Hollow) exists.
- Every balance number is an unplaytested placeholder — imbalance is *expected*, and your sense of
  *which* numbers feel wrong is exactly the useful signal.
- Automation stages are freely switchable here (+/-) for testing; in the real game they're earned.
