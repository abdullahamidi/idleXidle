# IDLExIDLE — Store Page Pack

Everything a store page needs, ready to paste. Written for the itch.io pre-alpha page first and
reused for the Steam page later (the copy is the same; only the asset sizes differ).

Assets live beside this file:

| Folder | What | Made by |
|---|---|---|
| `docs/store/capsules/` | every capsule at the sizes Steam and itch.io require | `python tools/marketing/make_capsules.py` |
| `docs/store/screenshots/` | nine 1920×1080 screenshots from the capture rig | `bash tools/asset-pipeline/capture.sh <mode> docs/store/screenshots/<mode>.png` |
| `build/release/IDLExIDLE-prealpha-<sha>-win64.zip` | the self-contained Windows build | `dotnet publish … --self-contained` (see the session log) |

---

## 1. Names and one-liners

- **Title:** IDLExIDLE
- **Tagline (under the title):** Your champion fights while you are away. You decide what it becomes.
- **Short description (itch "short text" / Steam short description, ≤ 300 characters):**
  > An offline idle auto-battler. Your champion clears waves on its own — even while the game is closed. You never swing the sword: you weave the build, forge the gear, and choose which road to walk when the world grows harder. Nothing is banked, nothing is lost.
- **Turkish short (itch.io lets you add a second language block):**
  > Çevrimdışı bir idle otomatik-savaş oyunu. Şampiyonun dalgaları kendi başına temizler — oyun kapalıyken bile. Kılıcı sen sallamazsın: build'i sen örer, ekipmanı sen dövmede işler, dünya zorlaştıkça hangi yolu yürüyeceğine sen karar verirsin.

## 2. Long description (the page body)

> **The fight runs itself. The choices are yours.**
>
> Your champion stands in the arena and fights wave after wave — on every screen, and while the game is closed. Every wave it clears pays out at once. Every fifth is a boss. When it falls, it gets back up and pushes on. Nothing is banked, nothing is lost.
>
> **Weave a build, not a rotation.** Skills are woven from a SOURCE (what it is made of) and a FORM (what it does): a Body Strike, a Shadow Projectile, a Nature Aura. Add Vows — rules you accept for extra power that only pay while your build keeps the rule — and Keystones that trade one thing for another. Then a Mastery tree of four directions and six Specialisations decides how your skills fight.
>
> **Gear that reads honestly.** Every item shows its real numbers. The Forge upgrades, re-rolls, sockets and breaks down — and asks before it does anything you cannot undo.
>
> **A world that gets harder on purpose.** Six regions, each clearly tougher than the last. Deepen the Corruption for richer rewards and enemies that wear it. Climb back down when it bites.
>
> **A Warren that works while you sleep.** Facilities produce Gleam, Insight and Memory Dust while you are away. Come back, spend, leave.
>
> **Traits that never reset.** Memory Dust buys permanent traits along four roads — Ruin, Aegis, Artifice, Avarice — and the capstone you choose closes the other three forever.
>
> Made by one person. Offline, no account, no ads, no in-game purchases — you buy it once and everything that changes the fight is in the box.

### Bullet list (Steam "About this game" feature list / itch page sidebar)

- Auto-battler that keeps fighting while the game is closed
- Weave skills from Source × Form × Vow — 6 sources, 6 forms, 12 vows
- 10 champions, each with an always-on passive that reshapes your build
- Mastery tree: four directions, six Specialisations, one discipline per hunter
- A forge with upgrades, re-rolls, sockets and gems — every stat shown in real numbers
- Six regions, a five-rung Corruption ladder you can climb up and down
- Warren: idle production of three currencies while you are away
- 51 permanent traits along four roads — and a capstone you cannot take back
- Offline. No account. No ads. No in-game purchases.

## 3. Tags and metadata

- **Genre:** Idle · Auto-battler · RPG · Strategy
- **itch.io tags:** `idle`, `auto-battler`, `incremental`, `pixel-art`, `rpg`, `singleplayer`, `offline`, `dark-fantasy`, `build-crafting`, `monogame`
- **Steam tags (pick up to 20 later):** Idler, Auto Battler, Clicker, RPG, Pixel Graphics, Dark Fantasy, Singleplayer, Loot, Character Customization, Strategy, Casual, Relaxing, 2D, Indie, Early Access
- **Steam features:** Single-player · Steam Cloud (later) · Family Sharing
- **Languages:** English (interface, full audio N/A — no voice) — Turkish to follow
- **Age rating:** stylised fantasy violence, no blood, no text chat → PEGI 7 / ESRB E10+ territory (fill the questionnaire honestly)
- **Developer / Publisher:** your name or studio name
- **Release state:** Pre-alpha (itch, restricted) → Early Access (Steam)

## 4. System requirements (Windows, self-contained build)

| | Minimum |
|---|---|
| OS | Windows 10 64-bit (Windows 11 tested) |
| Processor | Any dual-core x64 from the last decade |
| Memory | 2 GB RAM |
| Graphics | Anything with OpenGL 3.0 (DesktopGL) — integrated graphics are fine |
| Storage | 300 MB |
| Display | 1280×720 minimum; 1920×1080 native |
| Notes | No internet needed. Unsigned exe — Windows SmartScreen shows "More info → Run anyway" on first launch. |

## 5. Screenshot order (first three are what most people see)

1. `fight.png` — the arena mid-fight (the game)
2. `boss.png` — the Crystal Lich boss wave
3. `forge.png` — the four-tab forge with the materials wallet
4. `buildtree.png` — the mastery tree with its four direction headers
5. `map.png` — the world map and the corruption ladder
6. `dust.png` — the trait tree
7. `character.png` — gear screen
8. `warren.png` — the warren dashboard
9. `title.png` — the title screen (last, not first — the logo is already on the capsule)

## 6. Pricing (from `design/monetization.md`)

- itch.io pre-alpha: **no payments** (restricted / password page), "Donate" off.
- Steam Early Access: $6.99 → 1.0 at $9.99; regional matrix; parity across stores.

---

## 7. itch.io — step by step (10 minutes)

1. **Create the project:** itch.io → Dashboard → *Create new project*.
   - Title: `IDLExIDLE` · Project URL: `idlexidle` · Classification: *Games* · Kind: *Downloadable*.
   - Short description / tagline: section 1.
   - Pricing: *No payments* (pre-alpha).
2. **Uploads:** upload `IDLExIDLE-prealpha-<sha>-win64.zip`. Tick **Windows** as the platform. Label it `Pre-alpha — <sha>` so testers can name their build.
3. **Description:** paste section 2 (itch's editor takes the paragraphs; bold the lead lines). Add the Turkish short under a `---` line if you want a TR block.
4. **Metadata:** Genre *Strategy* (itch has no Idle genre), tags from section 3, *Made with: MonoGame*, *Average session: A few minutes* (idle games are checked in on), *Inputs: Keyboard, Mouse*, *Accessibility: color-blind friendly* only if you believe it.
5. **Images:** Cover image → `capsules/itch_cover.png` (630×500). Screenshots → the nine files from section 5 in that order. Optional banner: *Edit theme → Banner* → `capsules/itch_banner.png`.
6. **Visibility & access:** *Restricted* + a password (send `https://<you>.itch.io/idlexidle?password=<pw>` to testers — the password prefills), or *Draft* while you type. Switch to *Public* only when you want strangers.
7. **Community:** turn comments **on** — the cheapest feedback channel you have — and pin one comment asking testers to paste their **COPY FEEDBACK CODE** (Settings in-game).
8. Save → View page → check it on a phone width too (itch traffic is very mobile).

**Handing me the uploads:** if you make an itch.io API key (Settings → API keys), tell me the key exists in the environment as `BUTLER_API_KEY` and I will install butler and push every future build with one command (`butler push build/release/IDLExIDLE-win64 <you>/idlexidle:windows --userversion <sha>`). The page text and images still need your hands — itch has no API for them.

## 8. Steam — what to prepare now, so the 30-day clock is not the bottleneck

1. **Steamworks onboarding** ($100 Steam Direct, identity + bank + tax W-8BEN) — start it now; 5–10 business days for verification, then the 30-day wait from payment before a first release.
2. **Store page assets** — all sizes already in `docs/store/capsules/`:
   - Header capsule 920×430 · Small capsule 462×174 · Main capsule 1232×706 · Vertical capsule 748×896
   - Library capsule 600×900 · Library hero 3840×1240 (art only) · Library logo 1280×720 (transparent, words only) · Page background 1438×810
   - Screenshots ≥ 5 at 1920×1080 (section 5)
   - A trailer is not required for the page to go live but is required to be featured — plan a 30–60 s capture of the arena, the forge and the tree once the animation pass settles.
3. **Store text:** sections 1–4 paste straight into the Steamworks store editor (short description, About this game, features, system requirements).
4. **Early Access questionnaire:** why EA (a long-grind idle needs real hours of play data to tune), how long (a season, then 1.0), how the full version differs (more regions and champions, the balance pass, Turkish), current state (the whole loop is playable; six regions, ten champions, the forge, the warren, the traits), price plan (section 6), how the community is involved (feedback codes + the itch page).
5. **Steam Playtest:** once the app exists, create the free Playtest child app from *Associated Packages & DLC* — the pre-alpha channel with no reviews and no keys.

## 9. Things NOT to write on the page

- No review scores, awards, or discount claims on capsules (Steam rejects them).
- No promises of multiplayer, leaderboards, guilds or a market — those were dropped on purpose (`future-content-offline-only`).
- No "coming soon" features that are not built.
