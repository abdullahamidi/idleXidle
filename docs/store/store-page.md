# IDLExIDLE — Store Page Pack

Everything a store page needs, ready to paste. Written for the itch.io pre-alpha page first (now the
alpha page, since 2026-09-17) and
reused for the Steam page later (the copy is the same; only the asset sizes differ).

Assets live beside this file:

| Folder | What | Made by |
|---|---|---|
| `docs/store/capsules/` | every capsule at the sizes Steam and itch.io require | `python tools/marketing/make_capsules.py` |
| `docs/store/screenshots/` | twelve 1920×1080 screenshots of the current build (re-shot 2026-09-17) | `bash tools/marketing/store_screenshots.sh` |
| `build/release/IDLExIDLE-win64/` | the self-contained Windows build folder that butler pushes | `bash tools/publish/itch_push.sh abdullahamidi/idlexidle` |

---

## 1. Names and one-liners

- **Title:** IDLExIDLE
- **Tagline (under the title):** Your champion fights while you are away. You decide what it becomes.
- **Short description (itch "short text" / Steam short description, ≤ 300 characters):**
  > An offline idle auto-battler. Your champion clears waves on its own — even while the game is closed. You never swing the sword: you weave the build, forge the gear, and choose which road to walk when the world grows harder. Nothing is banked, nothing is lost.
- **Turkish short (itch.io lets you add a second language block):**
  > Çevrimdışı bir idle otomatik-savaş oyunu. Şampiyonun dalgaları kendi başına temizler — oyun kapalıyken bile. Kılıcı sen sallamazsın: build'i sen örer, ekipmanı sen dövmede işler, dünya zorlaştıkça hangi yolu yürüyeceğine sen karar verirsin. Oyun şimdilik yalnızca İngilizce; Türkçe arayüz yolda.

## 2. Long description (the page body)

> **itch.io uses `docs/store/itch/` instead (2026-08-26).** That folder holds the styled page: the
> banner, the page background, twelve baked strips (the loop, section headers, the six styles, the
> five classes with their ten champions, the six regions), the paste-ready `description.html`, the
> theme colours and a `custom.css`. Its copy is the current one (conquest at wave twenty, checkpoints
> paid in Memory Dust, traits you awaken by playing, five classes). The text below is the older Steam draft.

> **The fight runs itself. The choices are yours.**
>
> Your champion stands in the arena and fights wave after wave — on every screen, and while the game is closed. Every wave it clears pays out at once. Every fifth is a boss. When it falls, it gets back up and starts again, from the first wave or from a checkpoint you paid for. What it earned stays earned.
>
> **Weave a build, not a rotation.** Twelve shared skills across six styles — HAMMER crushes, VOLLEY rains, SNARE answers, SIGN amplifies, FIELD ticks, DRAIN feeds — and one SIGNATURE skill for each champion. As a skill levels from use, you choose one of its two variations, which gives it a SOURCE. Then you choose two of that variation's three reinforcements. Add Vows — rules you accept for extra power. Most pay only while your build keeps the rule. Two always pay, and always cost you something. Add Keystones that trade one thing for another. Then a Mastery tree of four directions and six Specialisations decides how your skills fight.
>
> **Gear that reads honestly.** Every item shows its real numbers. The Forge upgrades, re-rolls, sockets gems and salvages. It asks before you sell or salvage an item, unless you switch that question off. It always asks before you set or crush a gem. From the sixth upgrade on, an upgrade can slip and drop the item one level and one step. The button shows the chance of success first.
>
> **A world that gets harder on purpose.** Six regions, each clearly tougher than the last. Deepen the Corruption for richer rewards and enemies that wear it. Climb back down when it bites.
>
> **A Warren that works while you sleep.** Facilities produce Gleam, Memory Dust and Forge materials while you are away. Come back, spend, leave.
>
> **Characteristics you awaken, never buy.** Twenty-six traits open from what you have actually done — how you fight, what you survive, where you go — and each hunter wears three. Memory Dust buys the Warren and the right to start a descent deeper.
>
> Made by one person. Offline, no account, no ads, no in-game purchases — you buy it once and everything that changes the fight is in the box.

### Bullet list (Steam "About this game" feature list / itch page sidebar)

- Auto-battler that keeps fighting while the game is closed
- Weave a build from 12 shared skills in 6 styles, plus each champion's own SIGNATURE skill. A skill's chosen variation gives it one of 6 sources. Choose from 13 vows.
- 10 champions, each with an always-on passive that reshapes your build
- Mastery tree: four directions, six Specialisations, one discipline per hunter
- A forge with upgrades, re-rolls, sockets and gems — every stat shown in real numbers
- Six regions, a five-rung Corruption ladder you can climb up and down
- Warren: eight facilities make Gleam, Memory Dust and Forge materials, while you play and while you are away
- 26 characteristics that awaken from what you have done — three worn at a time
- Offline. No account. No ads. No in-game purchases.

## 3. Tags and metadata

- **Genre:** Idle · Auto-battler · RPG · Strategy
- **itch.io tags:** `idle`, `auto-battler`, `incremental`, `pixel-art`, `rpg`, `singleplayer`, `offline`, `dark-fantasy`, `build-crafting`, `monogame`
- **Steam tags (pick up to 20 later):** Idler, Auto Battler, RPG, Pixel Graphics, Dark Fantasy, Singleplayer, Loot, Character Customization, Strategy, Casual, Relaxing, 2D, Indie, Early Access (no Clicker: nothing in the game is earned by clicking).
- **Keep `pixel-art` in the itch list.** The arena, the enemies and the bosses are pixel art (`design/art/arena-art-contract.md`). The tag list in `docs/store/itch/README.md` §3 leaves it out and should add it back.
- **Steam features:** Single-player · Steam Cloud (later) · Family Sharing
- **Languages:** English (interface, full audio N/A — no voice) — Turkish to follow
- **Age rating:** stylised fantasy violence, no blood, no text chat → PEGI 7 / ESRB E10+ territory (fill the questionnaire honestly)
- **Developer / Publisher:** your name or studio name
- **Release state:** Alpha (itch; pre-alpha until 2026-09-17) → Early Access (Steam)

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

All twelve come from `bash tools/marketing/store_screenshots.sh`: capture-rig fixtures posed as a
progressed account sees them (every screen open, no coach cards), at UI SCALE 100 %.

1. `fight.png` — QUIVER's arrows landing a critical on a swarm in Umbral Reach
2. `region.png` — Cinderworks: the forge golems of the second region
3. `boss.png` — a boss wave, the region corrupted (the Fevered Crystal Lich)
4. `build.png` — THE BUILD: four skill slots, the skill tree, keystones, SWEAR A VOW
5. `forge.png` — the four-tab forge with the materials wallet
6. `mastery.png` — the mastery tree at the frontier, taken nodes and their names
7. `map.png` — six regions conquered, and the corruption ladder
8. `traits.png` — eighteen traits awakened, three worn
9. `gear.png` — the champion's gear and an item's real numbers
10. `roster.png` — ten champions in five classes
11. `warren.png` — the camp that works while you are away
12. `title.png` — the title screen (last, not first — the logo is already on the capsule)

## 6. Pricing (from `design/monetization.md`)

- itch.io alpha: **no payments** (restricted / password page), "Donate" off.
- Steam Early Access: $6.99 → 1.0 at $9.99; regional matrix; parity across stores.

---

## 7. itch.io — step by step (10 minutes)

1. **Create the project:** itch.io → Dashboard → *Create new project*.
   - Title: `IDLExIDLE` · Project URL: `idlexidle` · Classification: *Games* · Kind: *Downloadable*.
   - Short description / tagline: section 1.
   - Pricing: *No payments* (alpha).
2. **Uploads:** run `bash tools/publish/itch_push.sh abdullahamidi/idlexidle`. It needs `BUTLER_API_KEY` in your environment. The channel is `windows` unless you name another. The script does the whole upload, in this order:
   - It empties `build/release/IDLExIDLE-win64`.
   - It publishes the game into that folder as a self-contained Windows build (`win-x64`). Players need nothing else installed.
   - It copies `docs/publisher-notes.md` into the folder as `README.md`.
   - It starts the game once. If the title screen does not draw, it stops and uploads nothing.
   - It pushes the folder with butler to the `windows` channel.

   The build version is `0.1.0-alpha+` and the first 8 characters of the commit id, for example `0.1.0-alpha+a6fc3c9d`. The `0.1.0-alpha` part comes from `Directory.Build.props`. The game's BUILD line, in Settings and on the title screen, shows the same stamp, so testers can name their build. Old pre-alpha builds look different. On itch.io, their version is only the commit id. In the game, their BUILD line shows `1.0.0+` and the commit id. Do not upload a zip by hand. On the Uploads list, check once that the `windows` channel is marked **Windows**.
3. **Description:** paste `docs/store/itch/description.html` in HTML mode, then place its twelve images, as `docs/store/itch/README.md` §2 says. Section 2 in this file is the older Steam draft. Do not paste it on itch.
4. **Metadata:** Genre *Strategy* (itch has no Idle genre), tags from section 3, *Made with: MonoGame*, *Average session: A few minutes* (idle games are checked in on), *Inputs: Keyboard, Mouse*, *Accessibility: color-blind friendly* only if you believe it.
5. **Images:** Cover image → `capsules/itch_cover.png` (630×500). Screenshots → the section 5 files, in that order, after you take them again. Banner and background: *Edit theme* → `itch/header.png` (1920×600) and `itch/background.png`, as `docs/store/itch/README.md` §1 says.
6. **Visibility & access:** *Restricted* + a password (send `https://<you>.itch.io/idlexidle?password=<pw>` to testers — the password prefills), or *Draft* while you type. Switch to *Public* only when you want strangers.
7. **Community:** turn comments **on** — the cheapest feedback channel you have — and pin one comment asking testers to paste their **COPY FEEDBACK CODE** (Settings in-game).
8. Save → View page → check it on a phone width too (itch traffic is very mobile).

**Pushing a new build:** butler is installed and `BUTLER_API_KEY` is set in your user environment. One command builds, checks and pushes: `bash tools/publish/itch_push.sh abdullahamidi/idlexidle`. Do not call `butler push` by hand, because the script also cleans the folder, adds the README and refuses a build that cannot draw its title. The page text and images still need your hands, because itch has no API for them.

## 8. Steam — what to prepare now, so the 30-day clock is not the bottleneck

1. **Steamworks onboarding** ($100 Steam Direct, identity + bank + tax W-8BEN) — start it now; 5–10 business days for verification, then the 30-day wait from payment before a first release.
2. **Store page assets** — all sizes already in `docs/store/capsules/`:
   - Header capsule 920×430 · Small capsule 462×174 · Main capsule 1232×706 · Vertical capsule 748×896
   - Library capsule 600×900 · Library hero 3840×1240 (art only) · Library logo 1280×720 (transparent, words only) · Page background 1438×810
   - Screenshots ≥ 5 at 1920×1080 (section 5)
   - A trailer is not required for the page to go live but is required to be featured — plan a 30–60 s capture of the arena, the forge and the tree once the animation pass settles.
3. **Store text:** sections 1–4 paste straight into the Steamworks store editor (short description, About this game, features, system requirements).
4. **Early Access questionnaire:** why EA (a long-grind idle needs real hours of play data to tune), how long (a season, then 1.0), how the full version differs (more regions and champions, the balance pass, Turkish), current state (the whole loop is playable; six regions, ten champions, the forge, the warren, the traits), price plan (section 6), how the community is involved (feedback codes + the itch page).
5. **Steam Playtest:** once the app exists, create the free Playtest child app from *Associated Packages & DLC* — the testing channel (alpha today) with no reviews and no keys.

## 9. Things NOT to write on the page

- No review scores, awards, or discount claims on capsules (Steam rejects them).
- No promises of multiplayer, leaderboards, guilds or a market — those were dropped on purpose (`future-content-offline-only`).
- No "coming soon" features that are not built.
