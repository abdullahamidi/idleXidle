# Overnight Report — Resonance Hunter

**For you, in the morning.** Everything below is committed and pushed to `main`. Build is clean under
`-warnaserror`; **253 unit + 3 integration tests green**. Every visual change was screenshot-verified.

---

## TL;DR

Latest first: you said the game felt like a **calculator**, and that combat was **"one button like a
dumb"** with no tactics — and that neither manual nor auto had a point. That was the real problem, and
it's now fixed at the mechanics level, not the paint level (see **Combat** below). Before that, the
scene-quality pass:

1. **Combat is now a READ with depth + game-feel** — the headline change. Details below.
2. **Fixed the Warren mess** — rebuilt around four clean **role stations**.
3. **Fixed the farm mechanic** — hatching was broken (see below), now it works and matters.
4. **Made the last text screens into scenes** — Memory Dust → **star constellation**; Forge → **loot-card grid**.
5. **5 tiny sprites** will fully-art the warren (spec below).

Two things need YOU, not more code: **audio WAVs** and — most of all now — a **combat playtest** to tune it.

---

## Combat — the big one

**The problem was real, not cosmetic.** All three defences worked, interrupt was strictly best, and the
weak point opened on a *random* part — so the optimal play was "press F every telegraph," and aiming did
nothing. It played like a calculator because there was nothing to *read* or *decide*.

**Now it's a read with a tactical arc:**
- **Each attack demands ONE correct defence**, shown big and colour-coded: **HEAVY (red) → DODGE**,
  **FAST (blue) → INTERRUPT**, **NORMAL (amber) → BLOCK**. Right read = no damage + a weak point opens
  on **the exact limb that swung**. Wrong read = you graze and open nothing. Reading the tell is the skill.
- **Break the limb that attacked** to shut off that attack. Break them all → the creature is **DISARMED**
  and you finish it freely. Targeting is now tactical.
- **FLOW**: a streak of perfect reads; at ×3, weak-point hits **crit**. A miss resets it — risk/reward.
- **Game-feel**: hit-stop (the sim freezes on impact), screen shake, a gold flash on a perfect read, a
  red vignette when hit, creature hit-flash, and punchy callouts — **PERFECT! / CRIT! / BREAK! / GRAZED**.
  Impact, not numbers, so it never feels like a spreadsheet.
- Results now shows **PERFECT ×N** and **BEST FLOW ×M**, so playing manually visibly pays.

**What I need from you here:** a play session. The *mechanic* is in and tested, but the *tuning* — windup
lengths, how punishing a graze is, how fast limbs break, FLOW thresholds — I can't feel. Tell me where it's
too easy / too twitchy / unclear and I'll tune it.

---

## The farm was actually broken, not just ugly

This is the important one. "So many hatches spawn the same thing… useless" was literally true:

- **Every hatch was hardcoded to produce an Attacker.** And the evolution tree only reaches
  Attacker/Defender/Producer — so **Crafter and Support were impossible to ever obtain.**
- The farm's entire point is the **four-role composition puzzle** (Generator + Crafter + Defender +
  Support = an "Optimized" farm). With no way to get a Crafter or Support, **that puzzle could never be
  completed.** The farm couldn't do the one thing it exists to do.

**Fix:** hatching now rolls a varied Source *and* Role (`Creature.HatchRandom`, tested — every role is
obtainable, and a random roster can now actually complete the chain). Each core you crack is now a
chance to fill a role you're missing. That's the reason to hatch.

---

## What the scenes look like now

| Screen | Before | Now |
|---|---|---|
| **The Warren** (farm) | Two panels of text + colliding labels | **Four role stations** — each a stall holding that role's creature sprite or glowing "NEED". The puzzle reads at a glance; idle creatures wait in a pen you hatch into; production floats up as it happens. |
| **Memory Dust** | A vertical text list on a decorative starfield | A real **constellation** — upgrades are stars wired by their prerequisites, lit gold when owned, click to light. |
| **The Forge** | 13 rows of "WEAPON 12g" | A **loot-card grid** — rarity-framed item icons over the anvil backdrop. |
| Title / Combat / World Map / Help | (already good) | Left as-is; all read as scenes. |

The Warren also merges what used to be two colliding rows (a creature row *and* a separate role-chain
row) into one thing: the stations ARE the chain.

---

## New this session (the bigger picture, in case you're catching up)

Beyond the scene work, earlier in the session the game also gained:

- **Batch-3 art wired** — the Hunter sprite, Cinderworks (Machine) + Umbral (Shadow) creatures & bosses,
  themed arena backgrounds, world-map region emblems. No greyboxes left.
- **Corruption endgame** — once you conquer all three regions, DEEPEN THE CORRUPTION (map button / D):
  a one-way ratchet that makes the whole world tougher and richer, forever. The idle late-game.
- **Audio spine** — a `SoundBank` with every combat/ability/progression cue and per-screen music wired.
  Silent until the WAVs exist (see below).
- **Verdant variety** — two spare Nature creatures (Thorn Stalker, Oak Bulwark) added to the hunt pool.

Full history is in the git log; each commit message explains its change.

---

## Assets I need from you (specs written, priority order)

1. **Warren workers — 5 sprites** → `design/art/asset-generation-delta.md`. **Highest value / lowest
   effort.** Five generic role creatures (`crea_worker_attacker/defender/support/crafter/producer`),
   drawn light/greyscale so the engine can tint them by element. These five art the ENTIRE warren (any
   source, any role) — today, creatures without specific art show as a plain coloured blob.
2. **Audio — WAVs** → `design/audio/asset-generation-audio.md` (batch 4). The code plays them the moment
   they exist. This is the single biggest missing pillar; the game is currently silent.
3. *(Optional)* unique species art for Body/Mind/Spirit sources and the Crafter role — pure flavour, the
   workers cover function.

---

## What needs your judgement, not my code

- **Balance & fun.** Every number is still an unplaytested placeholder — hatch rates, corruption
  scaling, mastery thresholds, loot values, ability costs. I can't feel the game; you can. A play
  session with "this is too slow / too fast / unclear" notes is worth more than any code I can write
  blind.
- **Is the Warren clear now?** I think the four-station model makes the composition puzzle obvious, but
  you're the one who called the last version a mess — a sanity check would help.

---

## Health check

- Build: clean, Release, `-warnaserror`.
- Tests: 253 unit + 3 integration, all green (~90 ms).
- No known crashes. Save/load round-trips (incl. world + corruption). Audio is safe on machines with no
  sound device and disabled in headless runs.
- Everything committed & pushed to `github.com/abdullahamidi/idleXidle` (main).
