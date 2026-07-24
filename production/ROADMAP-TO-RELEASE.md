# Roadmap to Release — Resonance Hunter

**Target:** Paid on Steam, ~$10–15.
**Sequencing:** Playtest-gated. Nothing downstream starts until the loop is proven fun.

---

## Where we actually are

**A mechanically complete alpha.** Every designed system is built and tested — combat, three regions,
conquest, the corruption endgame, the Warren, Forge, Memory Dust, evolution, capture, save/offline.
255 unit + 3 integration green, clean Release build under `-warnaserror`, CI green.

**What that does NOT mean:** it is not a product. Audited state:

| | Status |
|---|---|
| Playtested for fun | **Never** |
| Audio | **0 files** — the game is silent |
| Settings / options menu | **Does not exist** |
| Gamepad | **Not implemented** (tech spec promises full cycle-and-confirm) |
| Steamworks | **Nothing** |
| Performance profiled | **Never** (60 fps / 16.6 ms budget unmeasured) |

At $10–15 the bar is "a finished product." We are several phases from that, and the first one is free.

---

## Phase 0 — THE FUN GATE  ⟵ *we are here*

**The only question: is the loop fun for 30–45 minutes?**

Every balance number in this game is a placeholder that survived internal consistency — which is *not*
the same as being fun. Until the loop is proven, every hour spent on audio, Steam, or content is a bet
on an unvalidated foundation. This costs one evening and gates everything below.

**Exit criteria:** you want to keep playing without being asked. Or: we learn exactly where it dies.

See **The playtest protocol** at the bottom — that's the next action.

---

## Phase 1 — Tune to fun (playtest-driven)

Iterate: play → note → tune → play. **This loop *is* the game.** Everything the playtest flags:
combat windups, graze cost, limb-break speed, FLOW thresholds, hatch rates, mastery curve, corruption
scaling, loot values, the Gleam economy.

Tools: `/playtest-report`, `/balance-check`.
**Exit:** the loop holds for 45 minutes and you'd play it again tomorrow.

---

## Phase 2 — Make it a product (the refund-preventers)

Non-negotiable for a **paid** Steam release. Their *absence* is what earns bad reviews.

1. **Audio** — the single most visible gap. Spec is written and every cue is already wired in code
   (`design/audio/asset-generation-audio.md`); it needs WAVs. Silence reads as "unfinished" in seconds.
2. **Settings menu** — does not exist today. Needs: master/SFX/music volume, fullscreen + resolution +
   integer scale, **key rebinding**, colorblind mode, text size, and a **screen-shake toggle**
   (you found out the hard way why that one matters).
3. **Gamepad** — full support incl. the cycle-and-confirm targeting your own tech spec mandates. Also
   the gate for **Steam Deck Verified**, which is meaningful discovery for this genre.
4. **Accessibility** — colorblind-safe palettes, remapping, text scaling. Anti-strobe floor is already
   respected in combat.
5. **Art** — the last 5 warren worker sprites (`design/art/asset-generation-delta.md`).

---

## Phase 3 — Content to justify the price

**This is likely the biggest cost, and the biggest risk to the price point.**

A $10–15 idle RPG promises tens of hours of long-tail progression. Today:

| Content | Now |
|---|---|
| Regions | 3 |
| Creature species | ~11 |
| Abilities | 3 |
| Memory Dust nodes | 19 |
| Evolution trees | 1 (Nature) |

That is thin for the price. **Scope this only after Phase 0** — the playtest tells us which axis is
actually compelling (more regions? deeper evolution? more abilities?), and we expand *that* one rather
than everything.

---

## Phase 4 — Platform & store

- **Steamworks**: achievements, cloud saves, rich presence, Steam Input.
- **Store page**: capsule art, 5+ screenshots, **trailer**, description, tags.
- **Wishlists**: start the store page EARLY, not at launch. Wishlist count at launch drives Steam's
  algorithm more than almost anything else. A demo build is worth serious consideration for this genre.
- **Build pipeline**: depots, branches, versioning.
- **Legal**: EULA, privacy policy, credits, third-party licences (MonoGame MS-PL, FontStashSharp,
  MonoGame.Extended, any fonts).

---

## Phase 5 — Harden

- **Performance profile** against the 60 fps / 16.6 ms budget — never measured.
- **Soak test** — idle games run for hours, and the farms tick every frame across all regions. Leaks
  and numeric drift surface only over long sessions. (`/soak-test`)
- **Save versioning + migration** — you *will* patch post-launch and players will have long-lived
  saves. The save already survives a round trip; it needs a migration story.
- Bug bash, regression suite, `/launch-checklist`, `/gate-check`.

---

## Honest risks

1. **Fun is unproven.** Everything above is a bet until Phase 0 clears. This is the #1 risk by a mile.
2. **Content volume vs price.** $10–15 is a real promise. Phase 3 may be larger than all other phases.
3. **Audio is a different skill** from the art you've been generating well. Budget for it.
4. **Scope creep is the failure mode.** We already have more systems than most shipped idle games. The
   path to release is subtracting uncertainty, not adding features. Freeze scope after Phase 1.
5. **Marketing.** A good game with no wishlists sells nothing. Store page work starts in parallel with
   Phase 2, not at the end.

---

## The playtest protocol — do this next

Delete the save first so you start clean:
`rm -f "$LOCALAPPDATA/ResonanceHunter/save.json"` then `dotnet run --project src/ResonanceHunter.Game`

**Play 30–45 minutes.** Do roughly: 10+ fights, hatch and staff the Warren, conquer Verdant Hollow,
travel to Cinderworks, try a boss, open Memory Dust.

**Note the minute-mark whenever you feel bored, confused, or annoyed.** That timestamp is the single
most valuable thing you can give me.

### Combat (the big one)
- Can you tell HEAVY/FAST/NORMAL apart fast enough to react? Is the colour read or the word read?
- Does the required-defence prompt feel *fair*, or like whack-a-mole?
- Windups: too long (boring) or too short (unfair)?
- Does a **graze** feel like a fair punishment or a cheap shot?
- Do you *want* to build FLOW? Does the crit payoff land?
- Does breaking a limb feel worth it — do you notice its attacks stopping?
- Is **DISARMED → finish** satisfying, or anticlimactic?
- **Do 10 fights in a row get boring?** ← the real test.

### The Warren
- Do you understand what it's *for* within 30 seconds?
- Is filling the four roles satisfying, or busywork?
- Does its output feel meaningful next to hunting?

### Progression
- Does training/Gleam feel meaningful?
- Does conquering Verdant feel *earned*?
- Does Cinderworks feel genuinely different to fight?
- Does deepening the Corruption tempt you?

### Overall
- At what minute did you get bored?
- What did you not understand?
- What did you want to do that you couldn't?
