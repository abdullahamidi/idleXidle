# IDLExIDLE

**An offline idle auto-battler.** Your champion fights wave after wave on its own — on every screen,
and while the game is closed. You never swing the sword. You read what the fight is telling you and
you decide what the champion becomes: which skills it weaves (one at first, up to four), which style
it commits to, which promises it swears, and what it wears. Then you watch a descent go deeper than
the last one.

## The loop

1. **Hunt.** A descent runs continuously: waves arrive, the champion fights, and every wave it
   clears pays GLEAM and one crafting material. Every fifth wave is a boss. Only bosses drop chests
   of items, and about one boss in five drops one (your first boss always does). Cooldowns carry
   from one wave to the next, so a descent is one long fight rather than a series of rounds. Twenty
   waves conquer a region and open the next.
2. **Spend.** GLEAM buys training ranks. Loot is worn, merged, upgraded, socketed or salvaged.
3. **Decide.** Mastery points, keystones, vows and traits reshape what the champion is, not just how
   big its numbers are.
4. **Go deeper.** The threat curve grows faster than the reward curve — the gap is the whole design.

## Controls

You can play with the mouse alone. Left-click chooses. Right-click opens an item's menu on the GEAR
screen, sells a gem in the FORGE, and gives a node back on the MASTERY tree. The wheel scrolls any
list that is too long. On the MASTERY tree the wheel zooms, and you drag to move the tree. The rail on
the left switches screens. A tile with a chain on it is a screen that is not open yet. It opens as
you play. The gear-wheel button at the top right opens Settings.

The keyboard is optional. Press F1 for a list of the main keys. Esc opens Settings (on the title
screen, Esc quits the game). F10 also opens Settings, M opens your letters (DISPATCHES), and L opens
the expedition log. Each screen has a letter: H HUNT, C GEAR, V TRAINING, B BUILD, E MASTERY,
K VAULT, F FORGE, A WARREN, W MAP, P TRAITS, R ROSTER. You cannot change the keys yet, and there is
no controller support yet. Nothing is timed and nothing needs fast reactions.

## Key systems

- **Styles and Skills** — twelve skills across six styles (HAMMER, VOLLEY, SNARE, SIGN, FIELD,
  DRAIN). Each has variations and reinforcements bought a level at a time, so two players running
  the same skill are rarely running the same skill.
- **Characters and Signatures** — ten champions, each with one innate rule and one signature skill.
  THE ANVIL leaves a third of a hit in an enemy that survives it, and lands it with the next hit.
  THE THORNWALL takes less damage from every hit, and its traps are stronger. Its signature skill
  answers the bites it takes with hits of its own. The innate is identity, not a number to allocate.
- **Mastery** — a four-branch tree (RESONANCE, TEMPO, ENDURE, LOOT) plus bridges. Road nodes teach
  shared skills: keep the node and the skill is available, give the node back and it locks again,
  while the levels that skill earned are kept for good.
- **Traits** — 26 of them, never bought. Each one wakes when you have done something often enough:
  landing heavy hits, healing, having your shield broken, keeping vows, completing gear sets,
  conquering regions, even losing three descents in a row in one region to the same kind of enemy.
  The rule behind each trait stays hidden until it wakes.
- **Vows** — a promise about your whole build, for example "every skill is the same style" or
  "wear no boots". While the build keeps the promise, every skill hits harder. Break it
  and the vow pays nothing. Two vows have no rule: they always pay, and they always cost you (you
  take more damage, or you have less health). The first vow is given to you. Most of the others
  appear after you keep their rule, without swearing them, for 10 to 20 waves. You can swear one
  vow from wave 5, two after two conquests, and three after four conquests.
- **Gear** — eight slots, rolled affixes, a prefix that trades one stat against another, an enchant
  that needs the right build to matter, and sockets for stat gems.
- **The Warren** — an idle camp that pays a share of what hunting pays, so time away is worth
  something and time played is worth more.
- **Regions** — six sources of corruption, each with its own ladder, roster and drop profile.

## Running it

Unzip and run `IDLExIDLE.exe`. It is self-contained: no runtime, no installer, no dependencies. The
first launch opens full screen, at your monitor's size. To play in a window, open Settings, set MODE
to WINDOWED, then pick a WINDOW SIZE (1920 × 1080 at first, or the nearest size that fits your
screen). The game is always drawn as one 1920 × 1080 page, scaled to fit, with dark bars if your
screen has a different shape.

UI SCALE can be 100 %, 125 %, 150 % or AUTO. It starts at 100 %. It is a density setting: the page
stays the same size, and the text, rows and buttons grow. AUTO picks 125 % in a small window.

## Saves

The game writes one save automatically:

```
%LOCALAPPDATA%\IDLExIDLE\save.json
```

**To evaluate from a clean start,** open Settings and use DANGER ZONE → START A NEW GAME (click it
twice). This deletes the save and starts a fresh account. Your display and sound settings stay.

Or close the game and delete (or rename) the folder above. This also resets your display and sound
settings, because they are kept in the same folder (`display.txt`). If you ever played a build from
before 8 September 2026, also delete `%LOCALAPPDATA%\ResonanceHunter`. If you do not, the next
launch moves that old save into the new folder and loads it.

`save.bak` beside the save is the previous good save. If the save file is damaged, the game loads
`save.bak` by itself and keeps the damaged file as `save.corrupt-<time>.json`.

While the game is closed, your champion keeps earning GLEAM, at 30 % of what live play pays. A new
account is paid for up to 2 hours away. After your first conquest the Warren opens. Each Warren
level you buy adds 6 minutes (up to 12 hours) and raises the share (up to 60 %). The Warren's own
work is paid for the same time. Time away gives no chests, no records and no skill levels. When you
come back, the next descent starts from the wave your champion reached while you were away, but
never past your best wave in that region.

## This build

- **Alpha.** The version is `0.1.0-alpha` plus the first 8 characters of the commit id, for example
  `0.1.0-alpha+a6fc3c9d`. The itch.io build version and the BUILD line in Settings and on the title
  screen both read this way. Every feedback code carries the full version, with the whole commit id,
  which starts with the same 8 characters. So a tester's report and the build page agree. (On the
  pre-alpha builds, the itch.io version was only the commit id, and the game showed `1.0.0+` and the
  commit id.)
- Windows x64, self-contained, Release configuration.

## Known, non-blocking

- **Alpha content depth.** Six regions and ten champions are in. You start with one champion,
  THE SEEKER. Four more join, one each when you conquer the second, third, fourth and fifth regions,
  and the last five join through quests. The ROSTER screen opens when your second champion joins.
  There is no seventh region yet. After all six regions are conquered, a DEEPER button appears on the
  MAP. It makes the whole world harder, in five steps (CORRUPTION 1 / 5 to 5 / 5), and SHALLOWER
  steps back down. Each step uses the same six regions, with tougher enemies and bigger rewards.
  Past wave 20, a region keeps sending waves that grow stronger.
- **Sound is simple placeholder work.** Scripts made every sound and every music track, and none is
  final. There are about 30 sound effects (hits, casts, bosses, the Forge, chests, rewards, menus).
  There is looping music: one track each for the title, the Forge, the Warren, the MAP and the
  TRAITS screen, and one track for each region, which plays during the hunt and on the other
  screens. Change the volume of each in Settings (EFFECTS VOLUME and MUSIC VOLUME).
- **Memory.** A fight uses about 680 MB of graphics memory for art (textures). At start-up, art
  uses about 440 MB. The fight figure is above the 512 MB the project plans for. Art loads the first
  time it is needed and stays loaded until you close the game. So the figure grows as you visit more
  regions and meet more bosses and champions. With every piece of art loaded, it would be about
  2.2 GB.
- **Keys and controllers.** You cannot change the keys yet, and there is no controller support yet.
- At UI SCALE 150 % the Forge shows fewer rows at once and scrolls; every action and every figure is
  reachable, and this is a deliberate readability-over-density choice.
- At UI SCALE 150 % the Settings panel scrolls. The last switch, GUIDANCE, is cut off until you
  scroll down.
