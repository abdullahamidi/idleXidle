# IDLExIDLE

**An offline idle auto-battler.** Your champion fights wave after wave on its own — on every screen,
and while the game is closed. You never swing the sword. You read what the fight is telling you and
you decide what the champion becomes: which four skills it weaves, which style it commits to, which
promises it swears, and what it wears. Then you watch a descent go deeper than the last one.

## The loop

1. **Hunt.** A descent runs continuously: waves arrive, the champion fights, and every clear pays
   Gleam and hauls loot. Cooldowns carry from one wave to the next, so a descent is one long fight
   rather than a series of rounds. Twenty waves conquer a region and open the next.
2. **Spend.** Gleam buys training ranks. Loot is worn, merged, upgraded, socketed or salvaged.
3. **Decide.** Mastery points, keystones, vows and traits reshape what the champion is, not just how
   big its numbers are.
4. **Go deeper.** The threat curve grows faster than the reward curve — the gap is the whole design.

## Controls

Mouse only. Left-click chooses, right-click opens an item's menu, the wheel scrolls any column that
overflows. The left rail switches screens. Nothing is timed and nothing needs reflexes.

## Key systems

- **Styles and Skills** — twelve skills across six styles (HAMMER, VOLLEY, SNARE, SIGN, FIELD,
  DRAIN). Each has variations and reinforcements bought a level at a time, so two players running
  the same skill are rarely running the same skill.
- **Characters and Signatures** — ten champions, each with one innate rule and one signature skill.
  THE ANVIL leaves a third of a hit in an enemy that survives it, and lands it with the next hit.
  THE THORNWALL answers what strikes it. The innate is identity, not a number to allocate.
- **Mastery** — a four-branch tree (RESONANCE, TEMPO, ENDURE, LOOT) plus bridges. Road nodes teach
  shared skills: keep the node and the skill is available, give the node back and it locks again,
  while the levels that skill earned are kept for good.
- **Traits** — discovered by playing a style rather than bought, and read off the fight itself.
- **Vows** — a promise sworn onto a skill that pays only while the promise is kept.
- **Gear** — eight slots, rolled affixes, a prefix that trades one stat against another, an enchant
  that needs the right build to matter, and sockets for stat gems.
- **The Warren** — an idle camp that pays a share of what hunting pays, so time away is worth
  something and time played is worth more.
- **Regions** — six sources of corruption, each with its own ladder, roster and drop profile.

## Running it

Unzip and run `IDLExIDLE.exe`. It is self-contained: no runtime, no installer, no dependencies. The
window opens at 1920×1080 and the UI SCALE setting (100 / 125 / 150 %) is a density profile — the
page stays the same size and the type, rows and controls grow.

## Saves

The game writes one save automatically:

```
%LOCALAPPDATA%\ResonanceHunter\save.json
```

The folder keeps the project's former name on purpose so that existing testers' saves survive the
rename; nothing inside the game shows that word. **To evaluate from a clean start,** close the game
and delete (or rename) that folder — the next launch begins a fresh account. `save.bak` beside it is
the previous good save and is restored automatically if a save is ever truncated.

Progress is credited while the game is closed, up to a cap that grows with the Warren.

## This build

- Version / build id: the git short SHA is stamped as the itch.io user version and shown in feedback
  codes, so a tester's report and the build page always agree.
- Windows x64, self-contained, Release configuration.

## Known, non-blocking

- **Pre-alpha content depth.** Six regions and ten champions are in; the deep ladder past the sixth
  region is generated rather than authored.
- **Audio** is a small placeholder set — hit, cast and reward cues only. No music yet.
- At UI SCALE 150 % the Forge shows fewer rows at once and scrolls; every action and every figure is
  reachable, and this is a deliberate readability-over-density choice.
