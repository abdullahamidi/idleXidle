# Monetization — Decision Record

| Field | Value |
|---|---|
| **Status** | Decided in principle (2026-08-24) — details to be revisited before the store page |
| **Model** | Premium (one-time purchase) + cosmetic-only DLC after 1.0 |
| **Explicitly rejected** | Microtransactions, ads, time-skips, free-to-play, any paid power |

## The decision

Resonance Hunter is sold once, at a fair price, and everything that affects the fight is in the box.
After 1.0, optional DLC may add COSMETICS and the SOUNDTRACK — never power, never time.

## Why (each reason is structural, not taste)

1. **The game is offline by design.** No servers, no accounts, and the save is plain JSON on the
   player's own disk. Anything sold as an in-game purchase could be granted by editing a text file —
   protecting a store would require the account system and servers this project deliberately rejected.
2. **The grind is the game, and it must stay honest.** The progression curve (training growth 1.13,
   a ~347-hour lifetime sink, "nothing is banked, nothing is lost") is tuned for how the game FEELS.
   A free-to-play model earns by selling waiting, which re-tunes every curve around "when does the
   player pay" instead of "when is this fun". That would invert the design.
3. **The PC/Steam audience punishes idle + microtransactions.** The genre's successful PC comparables
   are cheap premium: Melvor Idle (premium ~$10), Vampire Survivors (cheap premium + DLC growth).
4. **Solo-developer scope.** One SKU, no storefront backend, no live-ops obligations — matches the
   offline-only content decision already on record.

## Price band (to confirm after pre-alpha tells us the real content hours)

- **Early Access entry: $6.99** (working figure; band $5.99-7.99).
- **1.0: $9.99** (one visible step up at launch — reward for early buyers, honest anchor for late ones).
- Never below $4.99 (perceived value, refund/review dynamics).
- Regional prices follow Steam's recommended matrix (including Türkiye).
- Price parity across stores (Steam / Epic / itch.io) — no store undercuts another.

## The funnel (free things that feed the paid thing)

1. **Steam Playtest** (free child app) — the pre-alpha/beta channel; no user reviews, no keys to manage.
2. **Free demo** — a fixed slice: roughly the first region through its first conquest, the forge's
   opening, the first weave. Exact cut to be defined together. The demo build carries the same
   COPY FEEDBACK CODE button.
3. **Steam Next Fest** with the demo — the main wishlist lever for a solo developer.
4. **Early Access → 1.0** with the price step.

## Post-1.0 revenue (cosmetic only — the power line is absolute)

- **Supporter Pack DLC ($2.99-4.99):** champion palettes/outfit variants, arena colour themes,
  effect colour sets. The PixelLab asset pipeline (tools/asset-pipeline/v2) makes these cheap to
  produce; every piece goes through the arena art contract like all other art.
- **Soundtrack DLC** — the music beds as files.
- A content **expansion DLC** (new region chain + champions) is possible much later; it must add a
  parallel track, not gate the existing curve.

## Open questions (for the detailed talk, "zamanı gelince")

1. Final EA price and the 1.0 step — confirm after pre-alpha measures real content hours.
2. The demo slice's exact boundary (what unlocks are inside; where the "buy" moment lands).
3. **Music licensing:** the music beds came from an asset package — confirm the licence permits
   selling them as a standalone soundtrack DLC before announcing one. (The SFX are generated
   in-repo and are ours; the fonts are SIL OFL and ship with their licences already.)
4. Whether the mobile question is ever reopened — if it is, it is a separate SKU with its own model
   decision, and this document does not govern it. The PC version's model does not bend for it.
