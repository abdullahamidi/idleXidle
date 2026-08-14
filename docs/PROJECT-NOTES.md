# Project Notes

**What this is.** The accumulated working knowledge of this project — what was tried, what
failed, and why things are the way they are. It is the committed snapshot of
`production/session-state/active.md`, which is gitignored by design (ephemeral, per-machine)
and would otherwise be lost the moment work moves to another computer.

**How to use it.** Read the handover section immediately below to pick up where the last
session stopped. Everything after that is history, newest first, and it is written to be
searched rather than read start to finish — most sections exist because something took a
long time to work out and would cost the same again.

**Keeping it current.** `active.md` stays the live scratchpad. Re-snapshot this file at the
end of a session that produced knowledge worth keeping:

```bash
python3 - <<'EOF'
import re
s = open('production/session-state/active.md', encoding='utf-8').read()
s = re.sub(r'<!-- STATUS -->.*?<!-- /STATUS -->\n\n', '', s, flags=re.S)
s = s.replace('# Active Session State\n\n', '', 1)
head = open('docs/PROJECT-NOTES.md', encoding='utf-8').read().split('---\n\n', 1)[0]
open('docs/PROJECT-NOTES.md', 'w', encoding='utf-8').write(head + '---\n\n' + s)
EOF
```

A machine-switch checklist, since the repo cannot carry everything:

- **In the repo, nothing to do:** all source, all 460 art files, all 13 audio files, tests,
  tools, docs.
- **Not in the repo, carry it yourself:** `tools/asset-pipeline/.env` — the PixelLab API
  token. Only needed to generate NEW art; the game runs without it.
- **Not in the repo, regenerated:** `bin/`, `obj/`, `production/qa/evidence/*.png`.

---

## START HERE (handover, 2026-08-14 — second session, closed cleanly)

**Everything is committed and pushed** on `feat/hunter-cutout-rig`. Working tree clean,
**611/611 tests pass**, `bash tools/check_all.sh` is green, and the screenshot rig works.
Nothing is half-finished.

### The environment changed — read this before running anything

This repo moved to a **native-Windows checkout (Git Bash)**. The old notes below describe
Windows+WSL and are still true THERE, but on a Git Bash machine every one of their
workarounds is wrong. `tools/shellenv.sh` now resolves the toolchain by PROBING instead of
naming, and every script in `tools/` sources it — so `bash tools/check_all.sh`,
`tools/asset-pipeline/capture.sh` and `tools/run.sh` all just work on either machine.
Do not re-add `wslpath`, `/mnt/c/...`, `pgrep` or a bare `python3` to a script.

> **`python3` is not the interpreter on Windows.** Windows keeps an App Execution Alias at
> `WindowsApps\python3.exe` that prints a localized "Python was not found" and exits — it
> resolves, it is executable, and it is not Python. The python.org installer meanwhile
> installs `python.exe` and **no** `python3.exe`. The working interpreter has the wrong name
> and the broken one has the right name, so only RUNNING a candidate tells them apart.
> Python 3.12 was installed on this machine via winget; the pipeline needs no pip packages
> (`pixelpng.py` is a hand-rolled stdlib PNG codec).

`.gitattributes` now pins `*.sh` to `eol=lf`, because `core.autocrlf=true` was writing CRLF
into the working tree. **Do not run `git add --renormalize .`** — the repo stores many `.md`
and `.cs` blobs as CRLF, and renormalising restages ~400 files whose only diff is line
endings.

### What this session did

1. **`a1cd235` toolchain portability** — the above. It was blocking: `check_all.sh` died on
   "Python was not found" and `capture.sh` on `wslpath: command not found`, which took the
   screenshot rig with it, and that rig is the only visual verification this project has.
2. **`2f5e9c2` roster parity, measured** — answers the old candidate #1 below.
3. **`b707d2d` fight callout legibility** — the damage numbers had three independent
   reasons to overlap. Fixed and screenshot-verified.
4. **`3ca8bf5` the six arena music beds** — `music_arena_{theme}` had a working lookup, a
   working fallback, and no files, so the branch had never once evaluated true and all six
   regions shared one bed. One bed per region Source now. The gate was extended to cover the
   family: it reads the themes out of `Regions.cs`, because an interpolated key is invisible
   to the literal-key regex AND a missing bed fails *quietly* — it plays the generic combat
   track, which is exactly what a region without a bed is supposed to sound like.

### THE ROSTER IS BALANCED — that question is closed

`tests/unit/.../Characters/roster_parity_test.cs` runs every character through one gauntlet
and prints the table on every run (`--logger "console;verbosity=detailed"`). Every character
clears faster than a no-character control on the same Form, **1.12x to 1.82x**, with THE
SEEKER correctly last (it is documented as a floor, not a build). THE ANVIL 1.36x and THE
MAGPIE 1.34x — the two the old handover asked about — land on top of each other, and THE
MAGPIE keeps haul 1.35 / rarity 1.20 on top. Nobody is dominant, nobody is dead weight.

**Four fixtures were wrong before one was right**, all recorded in that file so nobody
rebuilds them: damage is bounded by enemy health so it measures overkill and not power (use
TIME); a four-Mark build swings nothing because Mark is an amplifier; the 120s tick ceiling
is an anti-hang guard that saturates the clock; and shrinking the fixture instead pushes
every effect below the 100ms tick quantum.

### THE FORM GAP — measured, raised, and DECIDED: intended, do not touch

The same harness measured single-target throughput per Form, and the spread is **19.2x**:

| Form | dmg/sec into one armoured target |
|---|---|
| Mark (1 Mark + 3 Strike) | 338 |
| Strike | 254 |
| Trap | 245 |
| Transformation | 89 |
| Projectile | 32 |
| **Aura** | **18** |

Aura clears a ten-creature swarm in 3.0s — the fastest in the game, and the honest shape of a
spread Form. But bosses are a flat `base x 1.06^wave x 2.2` for everyone, so an Aura build
needs ~14x longer on **every** boss, and boss waves come every 5th wave. A wave that runs out
the 120s ceiling is handled exactly like a death (`SoloExpeditionScreen`, the `else` branch):
red flash, run over, restart at wave 1. So a committed Aura build does not hit a soft wall —
it hits a **hard progression ceiling, burns the slowest possible 120s reaching it, and is
told nothing about why.**

**The user was asked and ruled: this is intended. Do not "fix" it.** The 19.2x is the honest
price of the best swarm clear in the game, and learning to mix the build is the player's
job. Nothing here is a defect — the table is kept because it took a harness to produce and
because the next person to see an Aura run stall will otherwise file it as a bug.

The one thing NOT ruled on is whether the stall should stay silent, since that was bundled
into the question rather than asked separately. Leaving it alone is consistent with the
ruling; if a future playtest finds the failure illegible, the cheap move is a line of
feedback on the stall, not a change to the curve.

## START HERE (handover, 2026-08-14 — first session, closed cleanly)

**Everything is committed and pushed** on `feat/hunter-cutout-rig`. Working tree clean,
**606/606 tests pass**, and **`bash tools/check_all.sh` is green.** Nothing is half-finished
and there is no in-flight work to recover.

### Run this first

```bash
bash tools/check_all.sh                      # font + asset/audio key gates, ~2s
WINDIR="$(wslpath -w "$PWD")" && cmd.exe /c "cd /d $WINDIR && dotnet test tests\unit\ResonanceHunter.Core.Tests --nologo"
```

**Environment — check this first on a new machine.** On the machine these notes were
written on (Windows + WSL), `dotnet` and `git push` only worked through `cmd.exe`: there was
no Linux SDK, and WSL's git failed GitHub's certificate.

```bash
WINDIR="$(wslpath -w "$PWD")" && cmd.exe /c "cd /d $WINDIR && dotnet test ..."
```

On a native Windows or native Linux machine, just run `dotnet` and `git` directly — try
that first and only reach for the `cmd.exe` wrapper if the direct call fails.

Captures work anywhere the game builds: `bash tools/asset-pipeline/capture.sh <mode>
shot_x.png`, with a **repo-relative** output path (an absolute one fails with a bare
"capture failed"). `RH_SHOT_MOUSE=x,y` poses a hover — coordinates are in 480-space, so
invert `Game1.ToOverlay` to aim at a screen rect. `RH_SHOT_T` freezes an animation.

### What this session was, in one line

A correctness sweep, not a feature push. **The through-line: this game fails soft
everywhere** — a missing glyph draws nothing, a missing texture falls back to a rectangle,
a missing sound plays silence, and none of it ever throws. Right at runtime, and it means a
defect is invisible until somebody looks. Most of what follows was found by looking.

### The backlog is EMPTY

Every item that was open at the start of this session is closed, including all eight from
the multi-agent audit. There is no agreed next task — the next session picks a direction.
Candidates, none of them committed to:

1. ~~**Balance the roster.**~~ **DONE — see the newer handover above.** Measured: the roster
   is balanced (1.12x–1.82x), THE ANVIL and THE MAGPIE are worth the same. The measurement
   turned up a separate open question about the six Forms, which is recorded up there.
2. ~~**The arena's per-theme music variant.**~~ **DONE — `3ca8bf5`, see above.**
3. **`design/` has drifted** in places from the code this session changed — `vows.md` and
   `characters.md` were updated, others were not audited.
4. **A playtest.** Nobody has actually played a long session since the balance pass; the
   depth curve is measured but not felt.

### Known, unfixed, deliberate

- `production/qa/evidence/*.png` — the `shot_*` prefix IS the policy; see `.gitignore`.
- Menu screens leave a band of scene background below the content (uniform overlay inset).
- The mastery tree's node icons are small at default zoom; readable but dense.


## THE LAST THREE ATTACK STRIPS (2026-08-14) — DONE

TOWER, QUIVER and OATHBOUND had no attack clip: theirs failed `measure_strip.py`
and were deleted, so all three swung on their idle. Re-rolling did not fix it —
TOWER went **0 for 6**, every failure on scale drift (14.8% to 48.3% against a 12%
cap) while every PASSING attack in the roster sits between 1.3% and 8.6%.

The cause was in the prompt, and the gate's numbers pointed at it. One attack action
served all ten: *"swinging forward to strike and then recovering to a stand"*. Three
poses cannot perform it —

- TOWER rests its maul's head **on the ground between its feet** and leans on it, so
  "swing" orders the model to hoist a weapon off the floor; the silhouette grows by
  half doing it.
- QUIVER holds an unstrung longbow **like a staff**.
- OATHBOUND's hands are **clasped and bound together**, and its eyes are sealed.

The seven that passed all have free hands and a neutral stance. `build_spec.py` now
carries an `ATTACK_ACTION` override per character — drive the maul DOWN, draw the
bow, thrust the bound hands. **TOWER then passed on attempt 1**, OATHBOUND on 1,
QUIVER on 3.

> Two wrong turns worth not repeating. I first guessed the attack `pad_fraction`
> (0.50) was too tight — it is not; attack already gets MORE margin than idle (0.62),
> and the number is smaller because the fraction is how much of the canvas the figure
> fills. Then I thought a big weapon legitimately swings the bounding box and the 12%
> cap was an idle-shaped rule wrongly applied — measuring the seven accepted attacks
> killed that too (all under 9%).

**And a gate I did NOT add.** Watching the strips, TOWER looked like it barely moved
and OATHBOUND like seven still frames plus one pop, so I measured frame-to-frame
silhouette change across all twenty clips to justify a motion floor and a pop ceiling.
The data refused: TOWER's attack has the highest mean motion in the roster (0.235),
and OATHBOUND's pop ratio (2.62) sits between ANVIL's 2.57 and UNBROKEN's 2.79, both
already accepted. My eye was reading a downscaled strip. Thresholds set from an
impression rather than from the known-good distribution are how a gate starts lying —
"does it read as the action" stays an advisory human check, which is what
`coding-standards.md` already says for Visual/Feel work.

## THE GATES (2026-08-14) — `bash tools/check_all.sh`

This game **fails soft everywhere, by design**: a missing glyph draws nothing, a missing
texture falls back to a coloured rectangle, and neither ever throws. That is right at
runtime — a missing file must never crash a screen — and it means a mistake is invisible
until somebody looks at the pixels. These scripts are the "somebody looks" step, automated.

- `check_font_coverage.py` — every drawn character against a conservative proven set. See
  the section below for the ↔ that shipped.
- `check_asset_keys.py` — every hand-typed asset key against the 460 images on disk, plus
  the 43-entry migration alias table against its own targets, plus every `sfx_*` cue. An
  alias whose target was deleted is the same hole one indirection further along, and it is
  LIKELIER than a typo: the alias exists precisely because that file was renamed once already.

**And ALL SIX music tracks.** `SoundBank` has had working looping music since it was
written — `SoundEffectInstance` with `IsLooped`, a track per screen, even a per-theme arena
variant with a fallback — and never had a file. Every screen has been silent.
`make_music.py` writes six 20-second ambient beds. Drones, not tunes: a bad melody is worse
than silence, because it draws attention and then repeats every twenty seconds for the
hundreds of hours an idle game is open.

The hard constraint is the LOOP. `IsLooped` restarts with no crossfade, so every frequency
and every LFO is locked to a whole number of cycles across the buffer (`f = cycles/DURATION`),
which puts the waveform at the same phase at both ends.

> **The seam check was wrong twice, both times measuring the signal instead of the defect.**
> `|first − last|` flagged bright tracks. The same figure over the buffer's MEAN step flagged
> `music_constellation` at 4.2× — while its join of 1880 LSB sat neatly between the steps
> either side of it, 2112 and 1574. The honest ruler is the immediate neighbourhood: a
> continuous join reads ~0.9 of its adjacent steps. All six read 0.87–0.93. Calibrated by
> unlocking the frequencies on purpose, which reads 48×.

**Five of the game's seven sound cues did not exist.** `sfx_click`, `sfx_forge`,
`sfx_levelup`, `sfx_conquer` and `sfx_deepen` were called for the whole of development and
played silence, because `SoundBank` no-ops on a name it does not have. `make_sfx.py`
synthesises all seven now — a bell is a sum of decaying partials, so there is nothing to
license and re-tuning one is editing a number. Shapes chosen to mean something: CLICK is
0.11s and quiet because it plays hundreds of times an hour; CONQUER hangs on a MAJOR chord
and DEEPEN falls into a minor one, because taking a region is a reward and deepening the
corruption is a price.

Both were calibrated by breaking them on purpose — a deleted glyph and an invented key —
and confirming each fails with the file and line. A gate nobody has watched fail is a gate
nobody knows the state of.

**Currently green, including the derived keys the scripts cannot see** (checked by hand:
six regions × boss art, ten characters × base sprite, twenty animation strips).

## CHARACTERS THAT DRAW AS NOTHING (2026-08-14)

The mastery tree's footer read **"WEIGHT SPREAD AND TEMPO ENDURE ARE OPPOSED"**. The string
is `"WEIGHT ↔ SPREAD AND TEMPO ↔ ENDURE ARE OPPOSED"`. The font has no `↔`, a missing glyph
renders as NOTHING — not a box, not a question mark — so the sentence closed over the holes
and made a different, wrong claim about the game's own design: four branches mutually
opposed, rather than two pairs. It reads perfectly in the source, which is why it survived.

**The renderer is a SYSTEM font.** `SmoothFont` loads `bahnschrift.ttf` or `segoeui.ttf` off
the player's machine and falls back to `PixelFont` only when neither loads. So glyph coverage
is not ours to guarantee and varies by machine.

> I fixed this wrong first: added `↔` to `PixelFont` and re-captured, and it still drew as
> nothing — because PixelFont is the FALLBACK, not the renderer. A glyph can be in our table
> and still vanish on the path every player is on.

`tools/check_font_coverage.py` is the gate. Its allowed set is deliberately conservative —
ASCII plus seven marks **observed rendering in real captures** (`· × — – → ← ‹`) — and it
flags anything else even if the fallback has it. Calibrated by deleting a glyph and
confirming it fails with the file and line. Two strings changed to suit it:

- the footer now reads "WEIGHT OPPOSES SPREAD. TEMPO OPPOSES ENDURE."
- the inventory's upgrade badge was `▲`, so **the entire "this beats what you are wearing"
  signal rode on an unverified glyph**. It is "UP" now, matching the Forge grid, which had
  been using the word all along.

## THE SWEEP, ROUND TWO (2026-08-14)

**THE QUIVER's passive did something other than what its card said.** "LOOSE AGAIN — a kill
sends the next shot immediately", and the character granted `Splinter`, whose own blurb is
"on kill: richer loot". An action-economy promise paid out as a loot bonus — on the passive
a player unlocks by finishing a quest specifically to get it. `BuildTrigger.LooseAgain`
exists now and clears `champ.ReadyAt` on every creature killed, so the next skill in the
rotation goes out at once. Worth most in a Swarm and nothing at all on a boss, which is the
right shape for a Projectile specialist.

> The test measured CAST COUNT first and read 8 against 8. A wave ends when the last
> creature dies, so eight creatures take eight killing casts however fast they arrive —
> the metric was counting the fixture, not the passive. It measures CLEAR TIME now.

> And `TriggerLivenessTests.test_every_trigger_is_claimed_by_a_test_in_this_file` caught the
> new trigger immediately and refused to pass until its proof was named in the roll-call.
> That guard is doing exactly the job it was written for.

**The trait diagram's long wires are dashed.** ATTUNEMENT wants one node from the head of
every road, so its edge crosses a third of the diagram whichever prerequisite is nearest.
Solid, at any alpha, a line that long reads as STRUCTURE — as though the two roads it cuts
between were joined — and the eye follows it instead of the four roads. Threshold is
measured in the diagram's own rung (2.2×), not in pixels, so it follows the layout.

## A CAPTURE SWEEP OF THE SCREENS NOBODY HAD LOOKED AT (2026-08-14)

Having found the same defect class — text drawn through text, information absent — on
FORGE, WEAVE, BUILD, WARREN and GEAR, I captured the screens I had not reviewed. Two more.

**The damage numbers were drawn ON the enemy's health bar.** `SpawnDamage` spawned at
`EnemyBox.Y + 8..40`, which is exactly where the wave's health bar sits, so a hit printed
"-203" through the bar and the next printed "-344" through the first — two unreadable
numbers and an unreadable bar, at the one moment the player is watching to see how the
fight is going. They spawn above it now and STACK, using the same counter the wave
callouts already had, so a flurry reads as a column instead of a smear.

**The run report announced "NEW RECORD — DEPTH 52" directly above its own
"DEPTH 53.0 → 52.0 (−1.0)".** The fixture forced `isRecord: true`. A fixture that lies
cannot catch the bug it poses for — and the live path had exactly this bug until the day
before, announcing a record on the first run of every session. It passes the real
comparison now, and the capture shows a red "DEPTH 52" with no record claim.

Reviewed and found clean: MAP, REFORGE (it already prints "HEAVY / DMG+102% SKL-20%" and
the enchant's sentence), the right-click item menu, BOSS, SETTINGS, EXPEDITION.

> **I listed WORLD as clean in that commit without opening the image.** It was not. The
> conquest announcement — the line that tells you a new region exists — was drawn at
> `MapCanvas.Bottom - 30`, on the map panel's bottom ornament, and the string is wider than
> the map, so it ran out through the frames of both panels beside it with no background
> behind any of it. It has its own band below the panels now. Do not write "reviewed" for a
> capture you did not look at.

**The HELP screen was lying about the game.** It is the only place a new player is told how
any of this works, and: it stated a flat "4 WOVEN SKILLS" and "3 KEYSTONE SOCKETS" when the
spine sells the fifth weave and a player STARTS with one socket; and its key list was
missing **H (HUNT)**, **R (ROSTER)** — ten characters and a quest chain that nothing told
anyone the key for — and **L (the expedition log)**. Both numbers are read from the live
loadout now, so the help cannot drift from what the player actually has.

## THE PLAYER'S ITEM REPORT (2026-08-14) — FOUR THINGS, FIXED

Reported: armour lowers my power, I cannot read what items do, chests feel hidden,
and hovering an item shows nothing.

**1. "Wearing armour lowers my power" — the NUMBER was wrong, not the armour.**
`PowerRating` multiplied hit size and ignored CADENCE. Half the armour traits buy one
with the other (SWIFT x0.90 dmg for x1.10 rate; FOCUSED x0.85 for x1.20), so the rating
read a cadence piece as a straight loss — and EQUIP BEST, which sorts by it, would have
handed back the weaker item. Measured against median depth over 40 runs:

| worn | old power | new power | depth |
|---|---|---|---|
| weapon only | 1270 | 1370 | 41 |
| + chest WARDING | 1295 | 1385 | 43 |
| + chest SWIFT | **1186** | 1923 | **49** |
| + chest FOCUSED | **1187** | 1982 | **47** |
| + chest VITAL | 1419 | 1526 | 44 |
| + full WARDING set | 2201 | 2369 | 53 |

The old column ranks the two BEST pieces last. **What was NOT wrong** (checked, because
the first diagnosis was that it was): armour is not weak, and the armour slots not
feeding Defense/MaxHealth directly is not a bug — every piece measured goes DEEPER.

**2b. And the PANEL itself, not just the hover.** The card was only half the answer: the
player was reading the ITEM DETAIL panel when they said they could not tell what items do,
and that panel still printed "TRAIT — KEEN". It now carries both blurbs. Making room meant
finding a collision that was already there — with four affixes and a trait row the stat list
reached y=813 while the comparison strip was clamped to a CEILING of 796, which is not a
clamp but a scheduled overlap. The hero block came down 280 → 150, the row pitch 38 → 34,
and the comparison strip moved ABOVE the sentences: it answers "should I put this on", which
is the question the panel is open to settle, and it had been last in a list whose length is
data.

**2 + 4. Item stats unreadable / no hover.** One fix: `ItemTooltip`, a shared card on
hover, used by the GEAR grid, the worn slots, the Forge bag and the SALVAGE grid. The
text was not small — it was ABSENT: the detail panel printed "TRAIT — KEEN", a name and
not an effect, while `GearTraits.BlurbOf` has held the sentence all along. The card shows
name, rarity/slot/iL/source, item power, the UPGRADE/DOWNGRADE delta against what is
worn, affixes, and trait + enchant WITH their blurbs. It compares via
`PowerContribution`, never `ItemScore`, so it cannot disagree with what equipping does.
Also fixed the collision that rendered "CRIT CHANCE2.7%" — that row is built right to
left now, so the name gets what the numbers leave.

**3. Chests felt hidden.** They were: three clicks deep (FORGE → SALVAGE → a small
toolbar row) with nothing anywhere else saying one was waiting. The FORGE nav tile now
carries a count badge. And the reveal is a SEQUENCE rather than a card that appears —
the chest rattles harder and harder under a deepening scrim, a plotted ring bursts out
of its own edge with a white flash, then the card springs open with an overshoot, items
drop in one at a time, and the materials count up.

**Not done, and worth recording as a dead end:** I offered to wire the hover card into the
fight screen's loot flow next. There is no loot flow there to wire it to — the fight screen
shows the champion, the creatures, the woven skills and counter panels, and no item surface
at all. Chests are the only loot it produces and their arrival was already announced
(`FlashChest` → "BOSS DOWN — CHEST DROPPED! (F — FORGE)", 2.4s). Check the surface exists
before proposing to put something on it.

`DevPoseReveal` + `RH_SHOT_T` freeze the reveal so a capture can inspect a 0.2s beat;
without it every shot lands at the same ~1s in. **Apply the pose AFTER the fixture opens
its chest** — opening resets the reveal clock, and applying it earlier silently pins
every capture to t=0. That cost three identical screenshots before I noticed.

## THREE DEAD OR BACKWARDS AFFIXES (2026-08-14) — FIXED

Found by a four-lens audit aimed at this project's own recurring failure ("a node that
reads nothing"). All three passed the entire 578-test suite, because **nothing had ever
driven an affix through `SoloExpedition.PushWave` and looked at the far side** — the
battle tests hand-build their creatures, and the seam the defects lived in is exactly
the re-mint `PushWave` does to apply an affix.

**1. PLATED erased the archetype it armours.** The re-mint copied MaxHealth, Health,
Damage, Defense and Source and dropped `Archetype`. That tag has exactly ONE reader:
SIEGE, "+45% to ARMOURED, -25% to everything else". Null is not Armoured, so on a PLATED
wave — the armoured wave, the wave the affix exists to make more armoured — the
anti-armour Greater applied its PENALTY. Measured at the fix: 13,494 damage against
5,285, a **2.55x** swing that had been running the wrong way. The NUMBERS append had the
same omission. Both now copy the tag.

**2. SWIFT made the creatures bite LESS often.** The bite fired on `ms % interval == 0`
while the loop steps in `TickMs` (100ms), so the true gap was `lcm(tick, interval)`:

| bias | plain | SWIFT wanted | SWIFT got |
|---|---|---|---|
| Normal 1500 | 1500ms | 1050ms | **2100ms** |
| Heavy 2200 | 2200ms | 1540ms | **7700ms** (5x fewer) |
| Fast 1000 | 1000ms | 700ms | 700ms (correct, by luck of the divisor) |

Now accumulates a `nextBite`, exactly as the auto-attack ten lines above always did.

**3. `Bands.SustainMultiplier` had no caller.** ENDLESS ("leech and regeneration
halved") computed its 0.5 and nothing read it. Now applied at `Heal()` — the single
funnel all four in-battle heal sites already pass through — and to `BetweenWaveRegen`.
Deliberately NOT applied to `FullHealBetweenWaves`: that is a reset, not regeneration,
and halving a binary is a design change rather than a repair. (Name collision to watch:
the mastery node `endless` is `FullHealBetweenWaves`; `Affix.Endless` is the band.)

`tests/unit/.../Encounters/AffixLivenessTests.cs` is the guard, and it asserts DIRECTION
rather than magnitudes so band tuning cannot silently invert one again.

> The SIEGE test passed for the WRONG REASON first: the creature had 4,000 health, both
> builds killed it, so both "dealt" ~4,000 and the test cleared by 1.4% of rounding
> noise — it would have passed with the bug still in. A fixture that saturates its own
> metric proves nothing. The creature is now unkillable inside the window.

### Still open from the same audit — verified, not yet fixed

- ~~**FIFTH WEAVE is bought and discarded.**~~ FIXED (2026-08-14). `Build.SlotCapacity`
  is an instance value now, floored at `Build.SkillSlots`; `Weave` enforces it,
  `PlayerLoadout.ToBuild` sets it, and `AddSkill`/`Restore` stopped clamping against the
  const. `SoloBattle.DescribeBuild` reports the BUILD's capacity, so VOW OF COMPLETION
  ("no empty skill slot") no longer counts as met while an empty fifth slot is on screen.
  The host sets the capacity BEFORE `_loadout.Restore`, or a saved fifth skill is dropped
  on every load and nobody ever sees it again.

  Two layout consequences, both fixed and both invisible until a fifth slot existed:
  - WEAVE's three panels were 718 tall and the third keystone chip already cleared the
    interior by 12px at FOUR slots; a fifth pushed it 68px through the frame. All three
    are 800 now, everything below the slot list hangs off the capacity, and the Vow
    column got its fifth row back.
  - BUILD's auto-skill cards were laid out at a fixed pitch of 182, which fitted exactly
    four and put a fifth at x=1180..1348 — off a panel that ends at 1200 and across the
    passives column. They divide the panel width now.

  Both capture fixtures (`build`, `weave`) buy `weave_5` on purpose: a fixture that poses
  the easy case is a fixture that would not have caught any of this. `weave_5` requires
  `socket_2`, and trait points must come from `RestoreConquered`/`RestoreCorruption` —
  `SetEarned` alone is stamped back to zero before the screen draws.
- ~~**The keystone chip list is capped at three.**~~ FIXED. Three is the SOCKET count, and it
  was being used as a LIST LENGTH — a player who learned a fourth keystone could never see
  it, let alone choose it over the three the catalogue happened to order first. The chips
  are a scrolling WINDOW now (`KeystoneRows`, `_keystoneScroll`, wheel over the slots
  panel), with an `[1-3 of 6]` indicator that appears only when there is something below
  the fold. Sockets stay scarce; which keystone fills them is the decision the roads sell.

  Geometry measured against the WORST case, not the current one: at five slots the old
  102/48/44 row ended at y=912 against an interior that closes at 904, so the third chip
  was drawn on the frame. 94/46/42 fits both capacities.

  **Not posed by a capture**, and worth knowing why: four keystones plus the fifth weave
  costs more than the 22 trait points a fully-conquered corruption-16 career can reach, so
  the overflow indicator has no reachable fixture today. The `weave` fixture poses the
  three-row window full instead.
- ~~**SPLINTER terminates in `Haul.Quality`, which nothing reads.**~~ FIXED. The chest now
  REMEMBERS ITS RUN: `Chest.RunTilt` carries the descent's accumulated quality and
  multiplies into the same `buildTilt` the build's own rarity uses. That gives the chain a
  destination — kill more, the chest that run earned rolls rarer — and makes REAPER (which
  charges −25% skill rate for the trigger) a socket that pays instead of a pure cost. The
  Splinter weapon enchant and THE QUIVER's grant come alive with it.

  Neutral is **1.0, never 0**, in both `Chest.RunTilt` and `SavedChest.RunTilt`, and the
  restore maps `<= 0` back to 1: it multiplies, so an old chest deserialised without the
  field has to open exactly as it used to. A 0 default would have quietly made every chest
  a player was holding at upgrade time worthless — the kind of migration bug nobody
  reports, because it reads as bad luck.

  Note the bound: `Haul.Plus` takes the MAX quality across waves, not the sum, so
  SPLINTER's value is capped by the best single wave (1 kill = +0.15, a five-creature wave
  = +0.75). Bounded by design; worth knowing before tuning it.
- ~~**THE OATHBOUND's "Vows pay far more" has no field to write to.**~~ FIXED.
  `SkillShape.VowPowerMultiplier` exists now and scales **the Vow's BONUS, not the factor**
  — a Vow worth x1.90 pays +0.90, and at 1.5 it pays +1.35. Scaling the whole factor would
  have paid out on a build with NO Vow sworn: a flat damage bonus wearing a Vow's name,
  worth most to the player ignoring the system the character is about.

  `test_every_character_changes_something_the_sim_reads` did NOT catch this, and could not:
  it passes if any ONE of the four channels is non-default, and OATHBOUND's Shape carried
  its Mark half. **A passive with two promises needs a test per promise.**
- ~~**`_bestDepth` is never restored.**~~ FIXED. The screen kept its own counter, starting
  at 0 every launch and restored by nothing, so `Wave >= _bestDepth` was true on the first
  run of every session — a wave-3 death announced NEW RECORD after a career of forty, and
  `Log.Add` saved it that way, so the lie outlived the session. It was per-SCREEN, not
  per-REGION, so it was also wrong after any region switch. The right figure was already
  persisted and already per-region (`RegionFarm.BestDepth`); the host feeds it in now.

  Two traps in the fix, both found by reading the call order rather than by running it:
  `>=` had to become `>` (equalling your best is not beating it), and the value has to be
  LATCHED at run start — the host calls `RecordDepth(_expedition.Deepest)` every frame, so
  compared live a run that beat 40 and died at 45 would find the record already reading 45
  and report nothing. The opposite failure, equally invisible.
- ~~**CHESTS OPENED is never saved.**~~ FIXED. It sat on STATS between HIGHEST WAVE and
  MASTERY POINTS, both persisted, and read zero after every launch. `ChestsCredited` is
  persisted WITH it, because CRAFTER is paid on the delta — saving the total alone would
  re-credit a whole restored career on the first frame after every load.
- ~~**STATS recomputes the sim's crit formula.**~~ FIXED. It summed the trained stat and
  the gear affixes and stopped, dropping `SkillShape.BonusCritPercent` — every crit node
  on the mastery tree was real in the fight and invisible on the page that exists to state
  the player's numbers. Crit damage was a hand-copy of the sim's two constants. Both now
  call `SoloBattle.CritChance` / `CritMultiplier`.
- ~~**The enemy health bar's denominator is a pre-composition estimate.**~~ FIXED. It was
  `_enemyBaseHealth * EnemyScale(wave + 1)`, computed BEFORE `PushWave` and therefore blind
  to everything `PushWave` decides — the archetype's health multiplier, PLATED thickening,
  NUMBERS adding a creature. Now summed from `LastWaveCreatures`. Measured: over 30 waves
  of one run, the estimate matched the truth **zero times**. The bar is the only readout of
  how a wave is going, and it was drawn against a number the simulation never used.

## THE BALANCE PASS (2026-08-14) — DONE

Three things were wrong. Only one of them was in the game.

**1. The mastery tree was a ranking, not four choices.** ENDURE won all six regions at
1.52x the worst branch. The cause was that `EnemyScaleBase` and enemy DAMAGE compounded
at the same 1.06, so no build ever stalled, so nothing ended a run except dying, so
depth measured survival alone. Fixed by splitting the knob: `EnemyDamageScaleBase =
1.04f`. Ratio 1.52 -> **1.33**, damage branches compressed to 39/41/43 against ENDURE 52.

Two wrong answers were measured FIRST and are recorded in `WaveModel`'s remarks so
nobody retries them:
- Raising HEALTH growth (the approved lever) makes it WORSE — 1.52 -> 1.72. Longer
  fights feed sustain. Reverted.
- No single ENDURE field is the cause. Knocking out leech / absorb / regen / health one
  at a time costs 0-7 waves, because ENDURE is not dying at all — it is STALL-bound at
  ~52 while the others die at 29-41.

**2. Static-cost Vows charged their price PER SKILL.** FRAGILITY's card said +12.5%
damage taken; a four-skill weave paid `1.125^4` = +60%. RECKLESS said -15% health and
took `0.85^4` = -48%. The benefit never compounded, so both were the only Vows in the
game worth NEGATIVE depth. Fixed with `SoloBattle.DistinctVows` — one Vow, one price,
one place that decides it. All 13 Vows now pay.

**3. THE HARNESS ITSELF WAS THE THIRD COPY.** `DepthOn` open-coded
`Math.Max(60, hunter.MaxHealth)` and never called `VowHealthMultiplier`, so every
measurement ran with Vow health prices unpaid and build health multipliers discarded.
That is why health looked worthless and RECKLESS looked good. All three mint sites now
call `SoloBattle.ChampionHealth`.

> I briefly concluded `SkillShape.MaxHealth` was a field nothing read and wrote that up
> as a shipped bug. It was NOT — `VowHealthMultiplier` already folded it in. The only
> broken reader was my own test harness. Check `git diff` before naming something a bug.

`tests/unit/.../Builds/BalanceSweepTests.cs` is the permanent gate: branch ratio <= 1.40,
every Vow positive while its demand is met, four trait roads comparable, plus the mint
composition test. Fixture rules that cost real time to find:
- Measure GEARED (`BuildMods(3.0f, 1.4f, ...)`). An ungeared fixture is damage-starved
  and hands the win to any defence branch — it reads 2.53x where geared reads 1.33x.
- `MidCareerHunter()`, never `new Hunter()`: untrained floors every branch at depth 4.
- Vary by **RunIndex**, not the injected `Random` — composition comes from
  `Bands.Seed(region, wave, RunIndex)`, so 40 seeds gave 40 identical runs.
- Every Vow needs its OWN satisfying fixture. Against one generic build, five Vows look
  worthless because a conditional Vow pays nothing while unmet.

`SoloExpeditionTests.test_health_and_damage_scales_agree_off_boss_waves` was replaced by
`test_enemy_damage_grows_more_slowly_than_enemy_health` — the old one asserted the exact
coupling that caused problem 1.

578/578 pass.

## THE TRAIT TREE IS A DIAGRAM NOW (2026-08-14)

`PrestigeScreen`'s grid of forty-four cards is gone. The grid was READABLE — every card said its
name, cost and state — and still told the player nothing, because the one thing the tree is FOR
cannot be written on a card: two roads cost sixty points against a career that earns about
thirty-four. Four terminals side by side at the top, each printing `0/4 · 30 PTS`, say it before a
word is read.

- `TraitTreeLayout` is an AUTHORED table, not a computed layout. The mastery tree computes because
  it is a regular cross; this tree is one spine of five unequal chains with four roads hanging off
  three different anchors, and a generic layout would tidy that irregularity instead of showing it.
- **The spine hangs DOWN, the roads climb UP** from the exact node that gates them, so a road
  visibly depends on the spine rather than floating beside it.
- **ONE EDGE PER NODE, to its nearest prerequisite** — the mastery tree's rule, adopted for the
  identical reason. COMPLETE ATTUNEMENT requires one node from all four road heads plus a socket;
  drawing all five made it a spider with legs across every other chain.
- The overview panel is deleted with the grid. It existed to carry a road FILTER and a table of road
  prices, and a drawn tree answers both.
- Hover reads, click pins — the detail panel follows the pointer.

Road label drifts are tuned so the four terminals land evenly: the first pass put AEGIS and ARTIFICE
1.2 units apart and the labels printed through each other as "AEGARTIFICE".

## QUESTS (2026-08-14)

Two quests, because two characters were gated behind quest ids that nothing defined — a roster
shipped with 2 of 10 permanently unobtainable. `design/gdd/quests.md`.

Same idiom as Vow demands: an enum goal plus a threshold, read by a pure function over a flat
snapshot (`QuestProgress`). Progress is DERIVED every frame like everything else in this game —
with **one** exception:

**THE FIRST VOW is LATCHED.** Every other goal reads a fact still true when you look at it; a run's
Vow is gone the moment the run ends. `Game1` increments `_runsWithVowKept` on the one frame
`_expedition.LogDirty` fires, and that counter is saved. It tests **kept, not sworn** — the Vow's
demand against the same `WeaveContext` the sim judged it by, because a Vow pays nothing while unmet
and counting sworn-but-unmet Vows would hand THE OATHBOUND to someone who never used the system it
rewards.

Quests are evaluated BEFORE `CharacterState.Refresh`, so finishing one unlocks its character on the
same frame rather than the next.

Two tests guard the failure that caused this: every quest-gated character names a quest that exists,
and every quest gates something.

## THE WEAVE (2026-08-13)

`WeaveScreen` replaces the `< VALUE >` cycling column that used to sit beside the
mastery tree. Reached by **T**, by "WEAVE SKILLS" on the BUILD overview, or by the
tree page's toggle.

**The point of it is the Vow column.** `Weaving.IsActive` was already correct and
already ran — inside the simulation, which is to say after the player had descended,
where they could not see it. `SoloBattle.DescribeBuild` is pure and public, so the
screen asks the same question against the live loadout and prints MET / UNMET beside
every studied Vow, updating as you edit. A Vow whose condition you cannot check before
you leave is a coin flip wearing a decision's clothes.

`BuildScreen.DrawSidebar` is DELETED, not left behind a flag — two editors for one
loadout is the parallel-systems failure this codebase has been bitten by twice.
Keystone sockets moved with it; losing them would have been a silent regression, since
the trait tree keeps selling a payoff that then has nowhere to be equipped.

## DEAD ART WITH A LIVE MANIFEST ENTRY DOES NOT STAY DEAD

`assets/art/rig/` was deleted with the rig — and a plain `generate.py` run put the whole
megabyte back the same afternoon, because `build_spec.py` still listed fifty
`gear_<slot>_<trait>` pieces and the drawn pauldron. A manifest entry is a standing
order to regenerate. Both are now removed from the SPEC. When art is retired, retire its
spec entry in the same commit.


## THE RIG IS GONE. Characters are FLAT SPRITES. (2026-08-13)

The cutout rig is deleted — `HunterRig`, `HunterRigRenderer`, `HunterRigScreen`,
`RigSpikeScreen`, `cut_rig_parts.py`, `measure_rig_source.py`, `roll_rig_source.sh`,
`rigpose.py` and `assets/art/rig/`. It articulated real limbs and charged for it in
constraints that reached back into the ART: every source body had to stand in a wide
A-pose with a measurable gap under each armpit and between the legs, hold nothing, and
wear nothing that hung. With one character that is a curiosity; with ten it is the whole
art direction, decided by the renderer. **Characters may now carry weapons, wear capes,
and be asymmetric.**

Worn gear no longer shows on the character either (removed one commit earlier). The
appearance is FIXED per character; gear is a sheet of numbers.

## ANIMATION STRIPS — and a wrong diagnosis, corrected

**The earlier entry here was wrong and has been deleted.** It said the animation endpoint
re-frames its input to a waist-up bust and cannot be told otherwise. It does not.

What actually happened: the first clip was animated from `char_seeker_base` at a moment
when that base sprite was ITSELF a bust — the `hero` style had returned one and "full
body" in the prompt had not been enough to stop it. The clip was a faithful animation of a
cropped design. The test was confounded and the endpoint took the blame.

`pad_fraction` (animate.py:`pad_to_fraction`) letterboxes the source so its content
occupies a chosen fraction of a square canvas, and the endpoint PRESERVES that framing.
0.62 is the working value.

### The strips are gated now — measure_strip.py

Twenty clips is past the number anyone checks by eye, and eyeing them is how the bust
survived a whole session. `measure_strip.py` reads each frame's alpha and fails a strip on:

- **framing** — foot-width ratio against THE CHARACTER'S OWN BASE SPRITE, not an absolute.
  An absolute threshold flunked OATHBOUND and THORNWALL, whose robe hems are genuinely as
  wide as their shoulders. Their clips were fine; the ruler was wrong.
- **scale drift** — >12% and the figure pulses instead of breathing. One clip measured 40%.
- **baseline drift** — `UiKit.AnimSprite` grounds a strip by ONE offset for the whole clip,
  so wandering feet slide.
- **disconnected blobs** — the animator hallucinates spare parts. One attack clip carried
  seventeen. `animate.py` now runs `keep_largest_component` PER FRAME (not per strip — the
  eight frames are eight separate blobs, and the largest is one frame).

`roll_strip.sh` re-rolls against that gate and keeps the winner, restoring the previous
file if every attempt fails. Same call as the rig sources: the variance is in the model,
and with a gate, rolling beats arguing.

Baseline measurement: of the first twenty clips, **twelve passed and six failed** (two more
were the robe false-positives).

### An ATTACK is letterboxed harder than an IDLE

`pad_fraction` is 0.62 for idle and **0.50 for attack**. Measured: four rolls of the seeker
attack came back a waist-up bust at 0.62 while its idle passed at the same value on the
second roll. A swing is a more cinematic instruction than standing still and the model
answers it with a closer shot, so the attack needs more margin to crop into.

### A rejected strip is DELETED, not kept

`AnimSprite` plays whatever is on disk and knows nothing about the gate, so a clip left
in place after failing is a rejection that never takes effect. `roll_strip.sh` removes a
strip that survives no attempt, and the draw sites fall back clip -> idle -> base sprite:
the character stands there as their approved design instead of pulsing or dragging loose
blobs across the arena. **No clip is better than a broken clip.**

Final state: **17 of 20 strips shipped, 0 failing.** The three that never passed are all
ATTACK clips (QUIVER, THE FALLING TOWER, THE OATHBOUND); those three characters use their
idle in the arena while swinging, which is a smaller loss than a broken swing.

### AnimSprite measures its own headroom

A negative `topCrop` means "measure it" — `UiKit.AnimSprite` then uses `TopPadFraction`,
which scans the WHOLE strip and reports the LEAST headroom any frame has, so cropping can
never cut into the figure on the one frame that raises its arms. Without it, the letterbox
that makes the generation work would render the champion two thirds the size the layout
asked for. All three character draw sites pass -1.

### A base sprite can come back cropped too

`char_seeker_base` arrived cut at the belt and the arena drew a torso standing on the
floor. "full body" in the prompt is not enough — the `hero` style now names the FEET
("down to both feet, with both legs and both boots fully in frame"), which is what gets
the legs. Measurable: the width of the bottom 12% of content against the widest row.
0.97 is a bust, 0.36 is a person. Worth a gate if more characters are added.


## Standing instruction (from the user, this session)

> "Our main job shouldn't be masking anyways. We can generate assets, we need to
> generate assets that fit our situation."

**The rule: when art does not fit, GENERATE THE RIGHT ASSET.** Do not mask, crop
or tint your way around a piece drawn for another purpose. Borrowed pixels carry
the wrong lighting and the wrong perspective, and no mask fixes either.

### The strongest form of the rule: regenerate the SOURCE, not the symptom

The cutout rig was sliced out of `hunter_idle` — frame 0 of an animation strip.
Every hack in the cutter existed because of that one choice: a sword baked across
three limbs (4,000+ px of erasing), a cape no rectangle could separate from an arm
(two static "cloak" bones), and limbs pressed against the torso with no measurable
boundary. `hunter_rig_base` is authored FOR the cutter — a gap of background under
each armpit, a gap between the legs, empty hands, nothing draped — and the erase
tables are now EMPTY. Coverage was exact on the first try.

The cost is real and worth knowing: gear heights are rig pixels, so a new source
means re-tuning every binding, and the A-pose that makes limbs cuttable needs
`HunterPart.RestAngle` to stand naturally.

### What that rule taught us this pass

- **Name the OBJECT, not its context.** Every prompt containing "pauldron" or
  "shoulder" returned a full cuirass — those words are learned attached to a body.
  Describing the shape instead ("scallop-shaped plate, three overlapping riveted
  bands fanning outward") returned the piece alone. Same lesson as the trait looks,
  which had to become materials ("deep violet arcane metal") rather than effects.
- **The style template can fight the subject.** `ui_bar_*_fill` was authored under
  the `chrome` style, whose prompt literally asks for ornate gold trim — so every
  "fill" came back as another empty ornate FRAME, which is why the boss bar showed
  no fill at all. Fills needed their own `barfill` style. Likewise the Warren
  facility icons under `medallion` came back as gold rings around an empty centre.
  `generate.py` now accepts a literal `prompt` for the one-offs that need it.
- **Full-bleed art must be generated with `no_background=False`.** Asked for
  "nothing but colour" on a transparent background, the model returns nothing.
- **Symmetric armour comes from ONE asset, mirrored.** Generating both shoulders
  independently gave two different materials.
- **Prompts must be written in the POSITIVE.** "NO cloak, NO cape, NO weapon" came
  back caped and armed, twice. Describing the silhouette you want and then saying
  what the clothing IS works. Negatives appear to act as emphasis, not exclusion.
- **Place worn art by its CONTENT box, never its canvas.** Across the ten boot
  variants the empty margin ranges from 6% to 18%, so any binding calibrated
  against the canvas is right for exactly one trait (`UiKit.ContentPad`).
- **A drawn part needs resizing into rig space.** `cut_rig_parts.DRAWN` trims,
  box-resamples to a width in SOURCE-sprite pixels and pivots by fraction, so a
  128px generation drops into a rig that draws at native size.

## Verification is non-negotiable

```bash
bash tools/asset-pipeline/capture.sh <mode> <out.png>
```

Modes: fight **fightgear** boss expedition forge build character stats warren map
dust world region2 region3 conquered lootforge reforge vow hybrid rig vfx help
settings

`fightgear` was added this pass: it dresses the Hunter before the fight opens, so
worn equipment can be verified from a capture at all (a fresh save wears nothing).

`capture.sh` BUILDS FIRST. Asset PNGs reach the output directory as a build step,
so a `--no-build` capture renders whatever art was there last time — that verified
a stale portrait once and looked exactly like a code bug.

Captures are the source of truth. The offscreen compositor (`scene_preview.py`) is
useful for measuring but has missed real bugs five times now.

Evidence copies of the approved frames live in `production/qa/evidence/`.
Working captures at the repo root are gitignored (`shot_*.png`).

## Scene checklist — ALL APPROVED

- [x] HUNT — grounding, vertical nav, ornate control rail, rig, gear on bones,
      drawn pauldrons, soft contact shadows, named enemies with framed bars
- [x] BOSS — bar cleared the stage header and Hunter HUD it drew through; name
      moved off the molten fill; anchor pulled in from the scissor edge
- [x] GEAR — paper doll is the live rig, so it shows worn equipment
- [x] STATS
- [x] BUILD — passive panel content moved inside the frame's crests
- [x] FORGE — insets widened; two unfittable strings shortened
- [x] SALVAGE (lootforge) — grid inside the panel; chest reveal framed
- [x] WARREN — eight drawn facility icons replacing eight tinted hexagons
- [x] MAP — real parchment chart (it had always been a black void: the opaque
      panel was drawn OVER the backdrop, and the backdrop spilled 154px sideways)
- [x] DUST — grid inside the panel; three category icons replacing one blob
- [x] HELP, SETTINGS

## Conventions settled

- **Menu screens draw through one inset transform** (`Game1.OverlayScale`,
  `Game1.ToOverlay`) so they clear the 180px vertical nav rail. The fight screen is
  excluded — its layout was rebuilt around the rail directly.
- **`UiKit.PanelCorner` (40) / `PanelInner()`** — panel art reaches ~40px in.
  Content inset by less sits on the filigree. `PanelInner` clamps for short panels.
- **Buttons use the shared font** (`UiKit.Button` → `Text2`), not the pixel font.
- **Gold is a SURFACE, not an ink, on gold.** Where a label lands on a gold fill it
  moves off it (boss name above the bar) or becomes gold-on-dark (settings rows).
- Rig gear binds to BONES; pivots are fractions of the gear texture.
- Worn gear doubles as the inventory icon (`ForgeScreen.TraitGlyph`).
- **Boxes must be DISJOINT and TILING.** Overlap draws a limb twice (four arms);
  a gap draws nothing at all (a band sawn through the shoulders). `cut_rig_parts.py`
  reports any opaque source pixel no box claims.
- **A dev preview must call the shipping path.** HunterRigScreen kept its own copy
  of the assembly maths and showed a character the game never drew.
- Rig body parts are CUT from `hunter_rig_base` on disjoint measured columns and are
  deliberately absent from the manifest — a `--force` run would otherwise overwrite
  the rig with independently invented limbs.
- Clip angles are NEGATED in `MapPose` — screen Y is down, so forward is negative.
- Culture is pinned to invariant at startup (the HUD read "TEMPO 4,94x").

## Redesign progress (2026-08-13)

Design docs are written and approved: `design/gdd/game-flow.md`,
`skill-and-trait-trees.md`, `regions-and-rosters.md`. Implementation so far:

- **Wave model** — waves hold a COMPOSITION of creatures, not one health pool.
  Archetypes (Swarm/Armoured/Caster/Bruiser), flat per-hit armour, `Form.Targets`.
- **Bands** — ten-wave bands, ten affixes, six authored region cycles, the
  counter-band rule (signature in 2 of 5, exactly 1 counter band). FNV-1a seeding,
  NOT `HashCode.Combine` (per-process randomised — a restart re-rolled the wave).
- **Post-run report** — the only place an automatic game can teach. Diagnosis, never
  prescription; measurements scoped to the LAST BAND.
- **Point income** — skill points come from FIRST-TIME depth, per region, and from
  nothing else. Warren facilities cap at `floor(deepestDepth / 5)`.
- **Three dead systems wired** — `Hunter.Train` (Stats rows train now),
  `BuildMods.Rarity` (reaches the chest roll, own tilt ceiling), and the Warren's
  mastery pool no longer funds the build tree.
- **Skill tree rebuilt** — four opposed branches (Weight/Spread/Tempo/Endure), ring
  pricing 1/3/5/8, bridges and Form specialisations at 6, total 184 against ~60
  earnable. `SkillShape` carries the shape changes; SoloBattle reads every field.

- **Trait tree rebuilt** — spends TRAIT POINTS (conquests + corruption tiers +
  mastery goals, ~34), never Memory Dust, which the Warren mints while the game is
  closed. A cheap structural spine (sockets 1->3, fifth weave, vows, filters, forge)
  plus RUIN / AEGIS / AVARICE / ARTIFICE at 4/6/8/12. One terminal reachable, two
  not. `HOARDER` and `WEAVER` authored and wired. Screen renamed DUST -> TRAITS.

- **Vows rewritten** — a Vow demands something of the BUILD (one Form, no crit, no
  boots), never of the fight. Three families: build shape, stat shape, gear-slot
  sacrifice. `EXPECTED UPTIME` -> `SEVERITY`. Design: `design/gdd/vows.md`.
- **PowerRating** reads the multipliers the sim reads (was ignoring gear traits and
  DAMAGE/HEALTH affixes entirely).

- **Item / Forge review** — gear traits now scale with REFINE level (they read only
  rarity, so refining a Common helm/boots/gloves/ring changed literally nothing);
  the REFORGE row shows what the trait DOES, from its own blurb rather than the
  inventory card's (which folds in the weapon multiplier and printed "DMG+1637%").

### Still to do

Nothing from the redesign backlog. Every item in `game-flow.md` §8 and both trees
are implemented, wired and covered. Natural next candidates, none of them agreed:

1. The TRAITS screen is a road-grouped grid, not a spine-and-paths diagram.
2. `MergeRecipe` / SALVAGE hub layout was flagged in an earlier audit and left.
3. No balance pass has been run against the finished trees + Vows together.
3. The TRAITS screen is still a flat filtered grid, not a spine-and-paths diagram.
   It reads correctly but does not show the four roads.

### Lessons that cost time

- A node that reads nothing is the project's recurring failure. `BuildMods.Rarity`
  was resolved, carried and summed by correct code at every step and read by NOTHING
  for the whole of development. Always test the END of a chain.
- A test fixture that saturates its own metric proves nothing — six shape tests
  "failed" because the champion died and HealthLost pinned at the pool size.
- Comparing two runs off one seeded RNG stream is NOT a paired comparison: a changed
  weight shifts how many draws each roll consumes and everything downstream
  decorrelates. Use many independent seeds.
- Measure the TAIL, not the mean, when a floor guarantees most of the distribution.

## UX pass from the user's review (2026-08-13) — RESUME HERE

The user reviewed the built game and gave a concrete list. Four items are done and
committed; three remain and are the next work.

### Done

- **Forge `< >` cycling removed.** Left column is two panels: the three verbs, then a
  clickable bag list (rarity bar, icon, name, level, WORN). Wheel serves whichever
  list the pointer is over. `ForgeScreen.DrawBag`.
- **Right-click item menu** in GEAR: EQUIP / UPGRADE / REFORGE / SALVAGE. EQUIP is
  local; the other three call `ForgeScreen.FocusFor(id, mode)` and switch overlays.
  `CharacterScreen.DrawItemMenu`, `ItemAction` enum, `Game1.MouseRightClicked`.
- **Vow discoverability.** The skill card's third row now reads "VOW · NONE — SWEAR
  ONE" and hovering prints the Vow's full demand. `BuildScreen.DrawCell(label)`.
- **Expedition log.** `Core/Expeditions/RunLog.cs`, saved via `SaveGame.RunLog`,
  opened with **L**, stepped with ‹ ›. Reuses `DrawReportPanel`.

### Done — the user's own words

1. **"treenin resimsiz olması"** — DONE. Three channels, one fact each: the FRAME's
   shape is the KIND, the GLYPH inside it is the BRANCH, and the tint on both is the
   STATE. Seven frames (`ui_node_start/minor/notable/greater/mastery/bridge/spec`) and
   four glyphs (`icon_branch_*`) cover all thirty-odd nodes, so a node added to the
   catalogue arrives already drawn. `BuildScreen.DrawNode`, `KindFrame`, `BranchGlyph`.

   Earlier this pass — **"sıkışmış" is fixed.** The tree is a WORLD (radius 1500 around
   0,0) plus a CAMERA (`_pan`, `_zoom`, `Screen()`, `World()`, `ZoomAt`, `ClampPan`).
   Drag to pan, wheel to zoom on the pointer, A/D, +/-, HOME. Default zoom 0.30 frames
   the whole tree. The detail is a docked card (`NodePanel`), not a column.

2. **"Traits ekranı çok basit ve kalitesiz... heyecan uyandırması gerekiyor"** — DONE.
   Taking a trait now runs a six-beat flourish (`PrestigeScreen.DrawFlourish`): the page
   dims, a flash, two or three plotted shockwaves, a double burst, a FACE, and a
   full-width name banner. A terminal gets the long version — 2.2s, its own emblem, a
   third ring, and "A ROAD ENDS HERE"; every other trait gets 1.0s and its ROAD's glyph.
   Plus screen shake, a real sound, road glyphs on every row/card/detail, and terminal
   art in the grid and the detail panel.

### Capture modes

`buildtree`, `itemmenu`, `runlog`, `reforge`, `character`, `fightreport`, plus three
added this pass: **`buildzoom`**, **`traitlit`**, **`traitterm`**. Run:
`bash tools/asset-pipeline/capture.sh <mode> shot_out.png [dial]`
(repo-relative out path only; the script builds first).

The third argument is that mode's one dial — the tree camera's zoom for `buildzoom`
(0.55 frames the mastery plaques, 0.95 the minors), or seconds into the flourish for
`traitlit` / `traitterm`. Both reach the game as `RH_SHOT_ZOOM` / `RH_SHOT_T`.

## RUNNING THE GAME, and the blind spot that hid a crash

`dotnet run --project src/ResonanceHunter.Game` **fails from a WSL shell** — there is
no Linux .NET SDK in this distro, only the Windows one at `/mnt/c/Program Files/dotnet`.
Use `bash tools/run.sh` (which hands it to cmd.exe, exactly as `capture.sh` does), or
run the plain command from PowerShell/cmd.

**A CAPTURE CANNOT SEE THE SAVE-LOAD PATH.** `Game1.LoadOrStartFresh` returns at its
first line when `RH_SHOT` is set, so a shot never reads the player's save — deliberate,
so a screenshot can never entangle with real progress. The cost is that every line in
that method is invisible to the entire verification rig.

That is how the game shipped this session **unable to start for anyone who had ever
saved**: the RunLog restore (990e1cb) reached through `_expedition.Log` from
`LoadOrStartFresh`, which runs in `Initialize()` — and every screen is constructed in
`LoadContent()`, which has not run yet. `NullReferenceException` before the window
opened. A first launch has no save and returns early, so it began on the SECOND launch,
and 550 green tests plus twenty captures all passed over it.

**The rule the method already states, in its own comment: anything restored from a save
belongs in a `_pending*` field, applied in LoadContent.** After touching that method,
`bash tools/run.sh` with a real save present. Nothing else exercises it.

## Verification hard-won this pass — READ BEFORE TRUSTING A CAPTURE

- **A fixture that sets a TOTAL is overwritten before it is drawn.** Both trees derive
  their points from world progress every frame inside `UpdateExpedition`, which runs
  BEFORE the prestige/build early-returns. `_mastery.SetEarned(24)` and
  `_dust.SetEarned(22)` were both stamped back down one frame later, so `buildtree` had
  been capturing `POINTS 0` and `dust` a career with zero points and forty-four LOCKED
  cards. Set the INPUTS (`RestoreConquered`, `RestoreCorruption`, `RestoreBestDepth`)
  **and** the total, because the derivation has not run yet when the fixture buys things.
- **The `dust` fixture bought six ids that no longer exist** (`might_1`, `grit_2`,
  `blood_1`, …) — every `Purchase` silently returned false for the whole trait-tree
  rebuild. The screen was being reviewed on a screenshot of its own empty state.
- **An animation fixture must FREEZE.** The rig renders sixty frames then saves, so an
  un-frozen pose advances a full second and captures the moment after it ended
  (`PrestigeScreen._litFrozen`).
- **This batch blends PREMULTIPLIED.** Build every fade as `colour * float`, never
  `new Color(r, g, b, someByte)` — the latter leaves RGB at full strength and the blend
  reads it as "add all of this", which turned the first flourish capture into a solid
  amber sheet with the whole screen faintly visible through it.

### Layout facts learned from captures — do not re-derive

- `UiKit.Panel` **scales** its frame art to the rectangle, so a SHORT panel has a
  proportionally taller ornament band. A title that reads at +18 on a 730px panel is
  buried on a 330px one; use +40. Left-aligning to dodge the centre medallion moves
  it into the CORNER ornament, which is worse.
- Side ornaments eat ~70px: content at +40 runs underneath them.
- The panel's bottom edge is ornate; nothing drawn there is readable.

## Asset lessons added this pass

- **When the right asset is a FORMULA, do not generate one.** Two attempts at a
  shockwave ring both returned a SUN — a filled disc with a corona — even after refusing
  "disc, filled circle, ball, sphere, orb, sun, moon, planet, coin" by name. A hole is
  not a subject the model can be argued into drawing. `PrestigeScreen.Ring` plots the
  circumference instead, which also stays crisp at any radius where a scaled 256px
  sprite would go soft exactly when it is biggest.
- **Six failed rolls means stop rolling.** `icon_branch_spread` ("one line that becomes
  three") came back as a feathered trident, a single arrow, a spread-winged phoenix, a
  crescent moon, a trident on a SHIELD (Endure's glyph — the worst collision available)
  and a corner bracket. The shape has no name of its own, so every phrasing landed on
  the nearest one that does. `draw_branch_spread.py` authors it: four segments and three
  triangles. It is deliberately ABSENT from the manifest, like the cut rig parts, so a
  `--force` run cannot overwrite it with a seventh guess.
- **The style prefix can be the thing fighting you** — again. "Heraldic emblem" is a
  phrase whose whole neighbourhood is shields, beasts and weapons.
- **The cheapest thing must LOOK the cheapest.** `ui_node_minor`'s first roll came back
  studded and gold-flecked, more ornate than the Notable that costs three times as much,
  inverting the price ladder the shapes exist to state. Its second was a bangle in
  three-quarter perspective, because "ring" on its own is jewellery.

## Audio exists now

`assets/audio` held nothing but a README, so `sfx_click`, `sfx_forge`, `sfx_conquer`
and `sfx_levelup` had been silent for the whole of development. `make_sfx.py`
synthesises `sfx_trait_lit` (0.95s two-note chime) and `sfx_trait_terminal` (2.1s low
impact + minor chord) — 16-bit mono PCM at 44.1kHz, which is what
`SoundEffect.FromStream` wants. A bell is a sum of decaying partials; nothing to
license, nothing to knock out, and re-tuning it is editing a number.

`SoundBank` finds them recursively and keys on the bare filename, and the csproj
already copies `assets/audio/**/*.wav`. The same script is where the other four cues
should go.

## Known, deliberately left

- **BUILD's passives panel** overlaps `SPIRIT RESONANCE` with the `KEYSTONES` header.
  Pre-existing, visible in `shot_v_build.png`, untouched by this pass.
- ~~`production/qa/evidence/*.png` is ignored by the repo-wide `shot_*.png` rule.~~
  RESOLVED (2026-08-14) as a documentation gap, not a policy bug. There was already a
  working convention and nobody had written it down: **the prefix is the policy.** A
  capture taken to look at something is named `shot_*` and is ignored wherever it lands;
  a capture KEPT as a story's evidence is given a real name and is committed. That is
  exactly how `armour_tiers.png` and `roster_designs.png` got tracked while nineteen
  others did not. Now stated in `.gitignore` beside the rule.

  The negation was considered and rejected: `evidence/` holds 21 files and 15 MB, and
  nineteen are this week's iteration captures. A trail that keeps everything is not a
  trail — and committing 15 MB of scratch to make two files reachable is the wrong
  trade.

- Menu screens leave a band of scene background below the content, a consequence of
  the uniform inset. It reads as a page laid over the scene; not treated as a bug.
- ~~SALVAGE's chest toolbar and its footer hint sit outside any panel.~~ DONE
  (2026-08-14). Three floating groups are now in panels and both columns end level
  at y=956: the toolbar joined the loot panel (which starts at 72 and swallows it),
  the two mode buttons joined the merge tray, and the message + hints got a footer
  panel of their own. The four rectangles are NAMED (`LootPanel`, `ActionPanel`,
  `TrayPanel`, `FooterPanel`) — they were open-coded at their use sites, which is
  why three things could sit outside them and each still look right on its own line.
  Also fixed: the enchantment row was 12px into the second affix row, so a
  four-affix item rendered "+6% HP" through "TRANSFORM LEECHES 2X".
