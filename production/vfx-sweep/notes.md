# Remaining-skill presentation sweep: running notes

Each work package appends its section at the end: what it did, files, decisions, deviations, open issues, test counts.

---

## Phase 0 / P0.1: reference regression baseline at ae20a7fd + films_sweep.sh (2026-10-03)

Tooling only. No C# touched.

### What was done
- `tools/asset-pipeline/action_regression.py` takes `--refs` now. Without the flag the old behaviour is unchanged:
  ACTION + the SPRAY / HARD HANDS voices, and only `root` is exempt when it is in one film only. With `--refs` the
  comparison also covers every `reaction-*` line (`reaction-draw` without `draws=`), `field-wave/-draw/-cue`,
  `mark-wave/-draw/-cue` (without `draws=` / `batches=`), and the sounds whose key starts with sfx_seeker_jaws /
  _press / _brand. Every Draw-sampled kind (`root`, `*-draw`) is reported and not counted when only one film has the
  moment. Callout, flash, number, vfx-spawn and generic sound lines are left out. The docstring is updated.
- `tools/asset-pipeline/films_sweep.sh` is new: `take <name> <hunter> <build|-> <variation|-> <seek|-> <count> <stride>
  [ENV=VAL...]`, plus `--take "SPEC"` for one-off takes, and the flags `--mp4`, `--keep` and `--dry`. Each take runs with
  RH_SHOT_SEED=7 and RH_PRESENT_TRACE=1, its log goes to RH_SEQ_LOG, and the output goes to
  build/shots/sweep/<tag>/<name>. The script carries the films_brand DISK GUARD word for word. It also refuses a
  count > 150, an unknown take name and a malformed take. Every take is checked before any filming starts. Between
  takes the script stops once build/shots/sweep is over 500 MB. After each take it runs `find <dir> -name '*.png' -delete`
  unless `--keep` is given. In P0.1 the `build` column maps to RH_SHOT_SWAP and the `seek` column to RH_SHOT_T. Each
  mapping is a one-line function (`build_env` / `seek_env`), so P0.2 only changes those two lines.
  `RH_SHOT_MODE=<mode>` among the ENV values selects the capture mode.
- The reference take set: ref_seeker, ref_fast, ref_brand, ref_brand_press, ref_jaws_kill (RH_SHOT_ENEMY=20,420, which
  is the pose of jaws-base evidence 12a/b). Also before_multi and before_bleed.
- A/A: every ref take was filmed twice at the clean HEAD ae20a7fd. All five pairs now read **IDENTICAL** under `--refs`
  (the default mode was IDENTICAL from the start). The logs are in
  `production/qa/evidence/vfx-sweep/phase0-baseline/` (`<take>_a.log`, `<take>_b.log`, README with the table).
- BEFORE films for P0.6: `production/qa/evidence/vfx-sweep/phase0-before/before_multi.{mp4,log}` and
  `before_bleed.{mp4,log}` (150 x 2, fightmulti; before_bleed is WEEP in JAWS's slot). The bleed trace shows the strobe
  as 19 `sfx_hit` asks in 5 s, against 9 in before_multi, with decaying Strike amounts 40/35/31/27/... All frames were
  deleted.

### Decisions (design.md is silent on these)
- **Fields excluded because they differ A/A.** Only these, each named with its reason in the script:
  - mark-draw `ticks=` and `flush=`: these are wall-clock Stopwatch time.
  - field-draw `shape=`: PRESS's front rectangle is latched in Draw from the target's pose on the first drawn frame
    after launch. When a catch-up tick skips that Draw, the latch happens one frame later.
- **Held as zero / non-zero instead of the exact value:**
  - mark-draw `alloc=`: the first curse frame's bytes include lazy runtime work (25752 vs 1128 A/A). Every steady frame is 0.
  - field-draw `alloc=`: a running total that jumps 8-14 MB on a few frames, and the frame it lands on depends on Draw
    sampling.
  - Every other field is compared exactly. That includes reaction-draw `sprites=` and `alloc=`, which were stable.
- **Repeated Draw samples count once under --refs.** A Draw-sampled line repeated at one playhead is one sample. After
  ref_jaws_kill's killing answer the playhead freezes and Draw repeats different numbers of times in each run. A Draw
  can be repeated as well as skipped.
- **Subject keys under --refs.** A reference line whose first field is a `name=value` measurement has no subject. This
  applies to field-draw `u=` and reaction-draw `alive=`. If those values were used as the key, a changed value would
  show up as "a moment in one film only" and would not be counted as a difference.
- **Negative controls:** ref_seeker vs ref_brand reports 131 differences. One reaction-draw `sprites=` edit is caught,
  and so is one JAWS cue volume edit. A `draws=` edit and a generic `sfx_hit` edit are ignored, as intended.
- **Full-disk self-test.** The free-space threshold can be RAISED with `SWEEP_GUARD_FREE_MB`, never lowered. That is how
  the full-disk refusal was proved without filling the disk.
- **The 500 MB stop is measured on build/shots/sweep**, which is this script's own temp space. The 2 GB start guard
  stays on all of build/shots.
- `sound_throttle.json` was not generated. Section 7 puts it with `sound_throttle_table_test.cs`, and section 8
  schedules both in Phase 6. That is C#, and this package is tooling only.

### Refusals verified
These all exit 1: no tag, no takes ("name the takes to film"), an unknown take, `--take "big seeker - - - 151 1"` (151
frames), and a full disk (`SWEEP_GUARD_FREE_MB=999999999`).

### Open issues
- **PRESS allocates 8-14 MB in the combat frame.** The field-draw `alloc=` running total jumps by about 13 MB on the
  wave's first field frames in ref_fast (3700-4017, 5717 and 8017). This looks like a first-use load or texture
  creation inside the field's measured Draw window. It goes against the sweep's "no large asset creation in the combat
  frame" rule. PRESS is CLOSED, so this was not touched; whoever builds `FieldLayers` in Phase 2 should look at it.
- **PRESS's front shape is latched in Draw** (see the shape= decision above). This is a sampling dependency, not a
  gameplay one. It is recorded here and left alone.
- `capture_seq.sh` prints "captured 100 frames" for 150-frame takes because its count glob only matches 2-digit
  names. All 150 frames are written and rendered (film_audio reports 150). The message is cosmetic.

### Disk
build/shots peaked at 43 MB (sweep/ 5 MB, PNG count 0 after every take). The drive had about 55 GB free at the end.
Committed evidence: phase0-baseline 1.1 MB of logs, phase0-before 3.3 MB (two mp4s and two logs).

### Tests
`dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors. Core.Tests 1901/1901 and Game.Tests
918/918 passed (unchanged; no C# touched). `bash tools/check_all.sh`: all gates green.

---

## Phase 0 / P0.2: the rig (RH_SHOT_BUILD, RH_SHOT_SEEK, RH_SHOT_KEYSTONES) + sound_throttle.json (2026-10-03)

Nothing committed. The C# is rig-only: every new path is inert unless its RH_SHOT_* variable is set. The one exception
is SoundBank, which now exposes what it already had (behaviour unchanged).

### What was done
- **`src/IdleXIdle.Game/Rig/ShotBuild.cs`** (pure).
  - `Parse(spec)` returns `(SkillId, Source?)` in slot order. It throws on an empty spec or entry, `a@b@c`, an unknown
    Source, or a numeric Source.
  - `Resolve` turns each id into its catalogue def and throws on an unknown id.
  - `Refusal(defs, signatureId, capacity)` refuses more skills than capacity, a duplicate, a missing signature, a
    signature outside slot one, and anything `Build.WouldRefuse` refuses. The text names the counts, e.g. "1 Active
    (volley_spray) and 3 passives (sig_quiver_backdraw, hammer_press, snare_jaws)".
  - `Mismatch(asked, live)` holds the live run's `EquippedSkill`s against the asked build: the same ids in the same slots
    with the same Sources.
- **`src/IdleXIdle.Game/Rig/ShotSeek.cs`** (pure).
  - `Parse(spec)` and `Find(events, slotOf)` return the ms of the matched event, or null.
  - The grammar is `skill:<id>[#n][+struck|+unstruck][+ontick]`, `aura:<id>[#n]`, `down[#n]` (EnemyDown),
    `hit:<HitSource>[#n]`, `downing[#n]` (an EnemyStrike sharing its ms with Down or Undying), `beat:<n>` and
    `event:<BattleEventKind>[#n]`. `#n` may close the head or the modifiers.
  - A Skill's batch is the events after it at its ms, up to the next Skill or Aura. That is the LAST-OWNER rule from
    section 3 / critique 17: a Strike after PRESS's Aura on a shared ms is the Aura's.
- **`src/IdleXIdle.Game/Game1.ShotBuildRig.cs`** (new partial).
  - `RefuseUnreadShotDials(sm)` runs right after RH_SHOT_HUNTER's EnsureSignature. It throws when RH_SHOT_BUILD or
    RH_SHOT_KEYSTONES is set on a fixture that wears no full loadout, and when RH_SHOT_SEEK is set on one that reads no
    seek.
  - `ApplyShotBuild()` runs in the fight block, before RH_SHOT_SWAP is read. It throws if RH_SHOT_SWAP is also set, and
    on any refusal. It then raises SkillCapacity to at least 4, clears every non-signature slot, adds slots, seats or
    pins the signature in slot one, and calls SetSkill / SetSource per entry in order. Any false return throws. Slots past
    the build are removed.
  - `ApplyShotKeystones()` reads RH_SHOT_KEYSTONES=<id>[,<id>] (see Decisions).
  - `VerifyShotBuild` registers `HuntScreen.DevCheckRunAtShutter`.
  - `ArmShotSeek()` sits next to RH_SHOT_BITE and is refused beside RH_SHOT_T / RH_SHOT_BITE. Its lead is `ShotLead()`
    (RH_SHOT_LEAD).
- **`HuntScreen.cs`**.
  - The pending seek is now a finder over the wave, `(Find, Lead, MustFind)`.
  - `DevSeekBefore(pick, lead)` keeps "no event: no seek" for the fixtures' own picks (the shield break, fightmulti's
    SPRAY, the bite).
  - The new `DevSeekBefore(find, lead, mustFind)` throws when the wave has no such event.
  - `DevCheckRunAtShutter` runs on the seek's gated frame (ShotAtFrame - 2), before the seek, and throws on a reason.
  - `DevRunSkills()` and `DevComposedKeystoneIds()` are new; the latter uses the same ToBuild call as ComposeBuild.
- **`SoundBank.cs`**.
  - `ThrottleTable` (read-only MinGapMs), `DefaultMinGapMs` (90) and `RepeatHalfLifeMs` (250) are public. Play's
    arithmetic now names the half-life; its value is unchanged.
  - `Unthrottled` is one declared, sorted list, built from the sources: sfx_reveal_tick, every key of
    `ActionRecipes.SeekerSpray.ContactTickCues` (sfx_seeker_spray_tick plus its fallback sfx_blade_tick), and the cues of
    every kind `MarkRecipe.Unthrottled` names (sfx_seeker_brand_infect / _ash). MarkRecipe's doc comment now points here.
- **`tools/asset-pipeline/sound_throttle.json`** is generated by `sound_throttle_table_test.cs` (sorted keys, LF, no
  BOM). The test FAILS when the committed file was missing or stale, and rewrites it, so the next run is green.
  - **`film_audio.py`** loads it. MIN_REPEAT_MS, MIN_GAP_MS, UNTHROTTLED and the literal 250 are deleted. Keys are
    compared without case, like the bank's OrdinalIgnoreCase dictionary.
- **`films_sweep.sh`**.
  - `build` is RH_SHOT_BUILD. `seek` is RH_SHOT_SEEK, except that a plain number stays RH_SHOT_T.
  - A failed take reports capture_seq's exit code and prints the game's `InvalidOperationException` line from the
    take's log.
  - The ref takes declare their whole build (SEEKER / BRAND_BASE / BRAND_PRESS). before_bleed keeps its swap as an ENV
    value, because that is how it was filmed at ae20a7fd.
- **`brand_audio_test.cs`** (one closed-reference test). It pinned film_audio's hand-written UNTHROTTLED set. It now
  reads the same set from sound_throttle.json's `unthrottled` list, plus a check that film_audio reads that file. The
  assertion is unchanged: every key the game plays unthrottled is exempt in the render, and no other key is. No
  approved cue, picture or pin was touched.

### DONE WHEN, measured (evidence: `production/qa/evidence/vfx-sweep/phase0-rig/`, logs + README, 0.5 MB)
- `RH_SHOT_HUNTER=quiver RH_SHOT_BUILD=sig_quiver_backdraw,hammer_press@Body,snare_jaws@Shadow,volley_spray@Mind`
  exits 1 with "...(PassivesFull): it holds 1 Active (volley_spray) and 3 passives (...)".
- A legal quiver build films with BACKDRAW in slot 0: `sig_quiver_backdraw,volley_spray@Mind,hammer_press@Body,hammer_blow@Body`,
  `RH_SHOT_SEEK=skill:sig_quiver_backdraw`, `RH_SHOT_ENEMY=300,9`. The log reads "verified at the shutter: slot 0
  sig_quiver_backdraw@Body ...", the trace has `event Skill slot=0 at=3300`, and the shutter lands on it.
- `RH_SHOT_SEEK=hit:Reflect` on the default fixture exits 1 with "found no such event in this wave (116 events)".
- RH_SHOT_BUILD set beside RH_SHOT_SWAP exits 1.
- RH_SHOT_KEYSTONES=weaver,venomancer,rend films and is verified. A typo exits 1 and lists the known ids.
- **The five ref takes, posed through RH_SHOT_BUILD, are IDENTICAL under `--refs` against BOTH baseline films (`_a` and
  `_b`).** The fight is identical event for event.
  - The A/A pairs stay IDENTICAL.
  - The negative controls are still caught: ref_seeker vs ref_brand gives 132 differences; a field-draw body-y edit and
    a removed alloc jump each give 1.
- One short take was rendered with `--mp4` through film_audio.py reading the JSON (2 sound asks in the window).

### Decisions (design.md is silent)
- **The signature must be in slot one.** The game's `PinSignature` locks it there, so a build that names it elsewhere is
  one the game cannot produce, and it is refused.
- **A Source left out is Body**, a new slot's own (`AddSkill`) and the starter signature's. A chosen variation owns its
  Source (`BuildComposer`). If no @Source was named that is accepted; if another Source was named, the take is refused
  and the message names the variation's Source.
- **The live-run check runs AT THE SHUTTER, not at DevStart (measured).** Checked at DevStart, the Quiver's run was
  missing BACKDRAW. The fixture composes before the frame pushes the posed champion to the hunt screen, so the signature
  is not "taught" yet. The host restarts the run on its first live frame, and the filmed run carries it. So the check
  runs on the seek's gated frame against the run that is filmed. It is warmed (JIT) once on the fixture's frame.
- **RH_SHOT_KEYSTONES was added.** No dial could socket a keystone on a fight fixture: only `fightstatus` did, with
  undying + rend hard-coded. The new dial:
  - seeds the discovered set as fightstatus does, rebuilds the menus, raises the sockets to the count and toggles each
    keystone;
  - refuses an unknown id, more than 3 ids, a duplicate, or a socket the loadout declines;
  - is verified on the composed build at the shutter.
  It covers the WEAVER / VENOM / CHARGE poses (weaver, venomancer, rend / capacitor / dynamo / lodestone). THORNS is
  not a keystone id.
- **sound_throttle.json was made now, early.** Design section 7 puts it in Phase 6, but films_sweep renders audio
  through film_audio.py from Phase 0 on.
- **`--refs` refined (`action_regression.py`) for three sampling effects found while re-filming.** Each is reported and
  not counted, and the script's docstring now describes them.
  1. When the shorter list of Draw-sampled lines at one moment is the longer list with samples left out, that is a
     skipped Draw.
  2. field-draw and mark-draw lines at a FROZEN final playhead are wall-clock sampling. In ref_jaws_kill, PRESS keeps
     drawing after the killing answer, and an unchanged no-build re-film differed there too. reaction-draw stays strict.
  3. field-draw `body=` keeps y / w / h and drops x, and `alloc>0` is held per FILM, not per moment. Both come from
     ref_fast's cold start, where x carries the host creature's bite lunge, which advances on frame dt. Re-filmed
     through RH_SHOT_BUILD (twice), `enemy-root x=9` vs 6 at one playhead gave body 1121 vs 1118, and the 14 MB
     first-use jump landed at 3700 instead of 3783. The events were identical.
  Before these refinements, a no-build re-film at the new code was IDENTICAL for ref_fast. The rig's extra fixture work
  shifts the cold-start catch-up pattern; it changes sampling, not the game.

### Open issues
- **The bite lunge advances on frame dt.** `HuntScreen.LungePx` reads the windup timer, so a creature's x at a given
  playhead depends on the catch-up pattern. It is presentation-only and pre-existing; noted for the Phase 1 swing work.
  PRESS's 8-14 MB first-use allocation (P0.1) is still open.
- **`capture_seq.sh` ignores the game's exit code and fails only on "no frames".** A refusal is still non-zero end to
  end, because a throwing take writes no frames, and films_sweep now prints the refusal. A refusal raised after frame 60
  would not be caught, but none is: the seek and the check both run at ShotAtFrame - 2.
- **An ad-hoc `--take` named like a table take films both takes, and the table take is overwritten.** This is
  pre-existing. Name ad-hoc takes apart.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Game.Tests **941/941**: 918 plus 23 new (capture_rig_shot_build_test 9, capture_rig_shot_seek_test 8,
  sound_throttle_table_test 6).
- Core.Tests **1901/1901** (no Core change).
- `bash tools/check_all.sh`: all gates green.

### Disk
build/shots peaked at 49 MB. The PNG count was 0 after every take. Every p02* temp folder was deleted. About 58 GB free.

---

## Phase 0 / P0.3: the generic Strike path - quiet derived hits, one cue per batch ms, the last-owner rule (2026-10-03)

Nothing committed. Presentation only (no Core change, no fight number moves: every ref take's `event` list is identical).

### What was done
- **`src/IdleXIdle.Game/Presentation/GenericHits.cs`** (new, pure).
  - `GenericHits.IsQuietHit(HitSource?)`: Bleed / Reflect / Carry / Deadweight -> true; Swing, Primary, Other, null -> false.
  - `StrikeOwner` (readonly struct: `Kind` None|Aura|Skill, `Slot`, `AtMs`), `After(e)` (an Aura or Skill event becomes
    the owner, anything else keeps it), `Owns(strike)` (a FromSkill Strike at the owner's ms), `IsAuraTick(strike)`.
  - `NumberGrade` { Quiet, Plain, Skill, Major }. Major is reserved and prints at skill grade until Phase 3.
- **`HuntScreen.cs`**, the Strike case and its batch locals.
  - `auraAtMs` is gone. `var lastOwner = StrikeOwner.None;` is updated by the Aura case and FIRST in the Skill case
    (before its early slot-range break). `auraTick = lastOwner.IsAuraTick(e)`.
  - `quietHit = GenericHits.IsQuietHit(e.Hit)` is read before the thud, the flash and the puff. A quiet hit: no
    `sfx_hit`, no flash (the generic F2 branch gains `!quietHit`), no ImpactWeak puff, no `sfx_crit`; number at QUIET
    grade; the fold predicate gains `GenericHits.IsQuietHit(o.Hit) != quietHit` so a derived hit never sums into a blow.
  - One generic `sfx_hit` per batch ms: `_hitVoicedAtMs` (field, compared by ms, reset in BeginWave since every wave's
    clock restarts at 0). A performed / reaction / aura / quiet Strike neither voices nor marks the ms, so the first
    QUALIFYING Strike's volume is the one heard. `sfx_crit` likewise once per ms (`_critVoicedAtMs`).
  - The JAWS pins stay green unedited: the new guards are a prefix
    (`if (!auraTick && !quietHit && !voicedThisMs && !performedHit && !reactionHit) Sound?.Play("sfx_hit"...` and
    `if (!auraTick && !quietHit && !performedHit && !reactionHit && (_strikeCount++ & 1) == 0)`).
  - `SpawnDamage(..., NumberGrade grade, ...)` replaces the `bool skill`. Quiet = `DamagePx / 2`, alpha 0.70, no
    shadow. `Callout` gained `Alpha` (default 1 through an explicit `Callout() { Text = ""; }` constructor) and `Bare`
    (drawn with `TextBig`, not `ShadowText`). The reaction echo and the aura total pass Skill / Plain as before.
  - Trace: a new `quiet-hit` line (slot, amount, hit, at) per derived Strike; the `number` line appends `grade=Quiet`
    ONLY for a quiet number, so every other number line is byte-identical to the baseline's.
- **`tests/unit/IdleXIdle.Game.Tests/hunt_generic_hits_test.cs`** (11 cases): the four quiet sources (theory), swing /
  primary / other / null not quiet, Aura-then-Skill on one ms (the cast's Strikes are the cast's), an aura alone owns its
  ticks, text pins (IsQuietHit read before thud / flash / puff; one sfx_hit site guarded by `voicedThisMs`; `auraAtMs`
  absent; `lastOwner.After(e)` before the Skill case's first statement), and a zero-allocation loop over a synthetic
  batch (1000 runs: 0 bytes; one thud per ms, the field's four blows stay ticks, three quiet hits).

### DONE WHEN, measured (evidence `production/qa/evidence/vfx-sweep/phase0-p03/`, logs + README, 0.8 MB)
- Ref takes vs BOTH baselines (`--refs`): ref_fast, ref_brand, ref_brand_press, ref_jaws_kill IDENTICAL on the first
  film. ref_seeker: run 1 had 1 difference (`reaction-clamp` clamp/body x 1194 vs 1195 at 7333), run 2 had 24 (the
  HARD HANDS release's `launch` x / `reach` +2 px and the `root` x after it, from 8083), run 3 IDENTICAL vs both.
  ref_seeker holds NO quiet hit and the fight is identical event for event in all three; the only behavioural change
  in it is fewer generic sfx_hit / sfx_crit asks. The moving x is the TARGET creature's (the bite lunge on frame dt, the
  P0.2 open issue), so this is sampling, not the change. The P0.2 rig film of ref_seeker vs run 1 shows the same
  1194/1195 difference. Recorded, not hidden: `--refs` does not yet treat reaction-clamp / release x as host-x sampling.
- before_bleed's build through RH_SHOT_BUILD (`sig_seeker_hard_hands,volley_spray,hammer_press@Body,volley_weep@Shadow`,
  fightmulti, 20x30; the CLUSTER variation owns SPRAY's Source, so `@Mind` is refused and left out): 20 bleed-only
  batches, 0 `flash`, 0 generic `sfx_hit`, all numbers `grade=Quiet`. The before film flashed and thudded on all 10 of
  its bleed-only batches.
- fightmulti's SPRAY batch on the Seeker (5200, three Strikes on slot 0) is all PERFORMED: 0 generic sfx_hit before and
  after. The multi-creature claim is proven on the generic path instead: the Quiver (no recipe) in fightmulti, 7 batches
  of 2-7 non-performed skill Strikes at one ms, exactly 1 sfx_hit each (`p03_quiver_multi.log`).

### Decisions (design.md silent)
- **The owner is updated FIRST in the Skill case**, before `if (e.Slot < 0 ...) break;`, so a skill with an out-of-range
  slot still ends an aura's ownership of the ms.
- **A quiet hit owned by an Aura still joins the tick's total** (`auraTick` is decided before `quietHit`, as the task's
  formula says): a carry out of a field kill is part of that tick's number. It never flashes either way.
- **A quiet critical prints in the crit colour at quiet size and plays no `sfx_crit`** (quiet = no sound).
- **sfx_crit is also once per ms** (the task's "likewise"); a second critical on another creature at the same ms is silent.
- **The design's "the Aura precedes the cast" is not always the order**: the skill loop runs by SLOT, so a field in a
  later slot ticks AFTER the casts of earlier slots (the Quiver take: Skill slot 1, Skill slot 0, then Aura slot 2 at
  2000). The last-owner rule is exact in both orders because the sim emits each owner's Strikes right after its event.
- **Ad-hoc take names**: p03_bleed / p03_quiver_multi (named apart from the table's takes, per P0.2's open issue).

### Open issues
- `action_regression.py --refs` still counts host-x sampling in `reaction-clamp` and the HARD HANDS `release` launch /
  reach (ref_seeker, 1 in 3 films clean). Same root cause as P0.2's field-draw body x: `HuntScreen.LungePx` advances on
  frame dt. Either make the lunge read the playhead (presentation fix, Phase 1 swing work) or extend HOST_X; not done
  here because it touches the regression's strictness on CLOSED references.
- Quiet numbers have no caption yet (CARRIED is Phase 3), and Major is not drawn differently yet.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Game.Tests **952/952**: 941 plus 11 new (hunt_generic_hits_test). jaws_reaction_test, press_field_test, brand_*_test,
  action_presentation_test, melee_action_test unchanged and green.
- Core.Tests **1901/1901** (no Core change).
- `bash tools/check_all.sh`: all gates green.

### Disk
build/shots/sweep peaked at 6 MB; PNG count 0 after every take; the p03* temp folders were deleted. About 42 GB free.

---

## Phase 0 / P0.4: the generic Skill path - callouts, phantoms, no-damage reactions, the WEAVER echo flag, no trap clip (2026-10-03)

Nothing committed. Presentation only. The one Core edit is the removal of the dead `WaveReplay.LastTrapBefore`, a
presentation query with no caller left. No fight number moves: every ref take's `event` list is identical.

### What was done
- **`Presentation/GenericHits.cs`** (pure, extended):
  - `CalloutText(def)` gives an Active's `def.Name` and null for a Field or a Reaction.
  - `DealsDamage(def)` is `BasePower > 0 && Effect != Amplify`.
  - `IsStruck(batch, i)`: a Strike at the Skill's ms, before the next Skill or Aura at that ms (the last-owner bound).
  - `IsPhantom(def, struck)` is `!struck && DealsDamage`.
  - `IsEcho(batch, i, reactionSlots)`: a Skill sharing its ms with an EARLIER Skill of ANOTHER slot. Reaction slots
    neither are echoes nor make them.
  - `EchoScale = 0.6`.
- **`HuntScreen.cs`**:
  - `CalloutFor(SkillDef) -> (string Text, Color Color)?` (the ink is `StyleInk(style)`, the old colours), used at the
    beat and at the performed release (`Voice`). Reactions and fields say nothing. The `calloutOk` /
    `ReactionRecipes.CalloutOverride` read is gone from the hunt.
  - Skill case: `reactionRecipe` is computed first, then `struck = GenericHits.IsStruck(batch, bi)`.
    - A PHANTOM that is not a presented reaction skips everything: no UiMotion flash, callout, PlaySkillVfx, sfx_cast
      or thud. It keeps `SkillCastsSeen` / `LastSkillCastAtMs` and the empty released-fx range, and traces `skill-skip`.
    - An ECHO traces `echo slot=`. It gets no callout and no sfx_cast (the sfx_cast line is wrapped in `if (!echo)`),
      and `PlaySkillVfx(..., echo)` scales the profile's `RelativeScale` and the premultiplied tint by 0.6.
    - The pinned pitched reaction thud line is wrapped, unedited, in `if (struck) { ... }`.
  - UpdateChampionClip: the reaction trap commit is deleted, along with `TrapClipGraceMs` and a stray orphaned
    `<summary>` above it.
  - `ReactionSlots()` is NOT dead. It still excludes reactions from the clip picker, the handoff and the rig's next
    performed cast, so it stays.
- **`ActorClips.cs`**: a Reaction contributes no clip. An Active whose ClipKey is `trap` (REPAY) still loads it.
- **`WaveReplay.LastTrapBefore`** removed, since it has no caller left. jaws_reaction_test's `DoesNotContain("LastTrapBefore")`
  over the dock is unaffected.

### Echo and the clip picker (no code needed)
`NextSkillEventAfter` returns the FIRST Skill after the playhead. A woven echo shares its original's ms and comes after
it, and the original is never a reaction slot. So the picker, the handoff and `ActionTargets.StruckBy` never reach the
echo (StruckBy stops at the next Skill). A test pins this behaviour.

### Decisions (design.md silent or in tension)
- **The Seeker's callout TEXT changes** from VOLLEY to SPRAY and from HAMMER to HARD HANDS, per design section 3's
  generic rule. This is not a reference redesign: callouts are excluded from `--refs`.
- **A presented reaction is exempt from the phantom skip.** JAWS emits its Skill and then strikes only when
  `trapRaw > 0`, so an IRON-stopped bite is a damaging reaction with no Strike. Skipping it would change the closed JAWS
  picture. A legacy reaction with a stopped bite is skipped, as the design's generic rule says.
- **`struck` is bounded by the next owner at the same ms**, not by the old castTarget scan (`<= AtMs + 1`, first Strike
  of anyone). Core never emits a +1 ms Strike. Under the old scan, a phantom followed by another skill's blows at its
  ms would have read as struck. `castTarget` itself is unchanged, to keep the references byte-identical.
- **An echo keeps the dock tile's UiMotion pulse**, because the woven slot did act. It also keeps its numbers, flashes
  and Strike sounds, which the P0.3 one-thud-per-ms rule already collapses.
- **The ECHO keystone's second cast (same slot) is not a woven echo.** 5.32 covers WEAVER only; cohort C's ECHO pass
  is Phase 5.
- **IsEcho takes the reaction slots**, so a reaction answering a bite on a cast's ms is never an echo and never makes
  the cast one. The task's bare `IsEcho(batch, index)` still works, since the third parameter is optional.

### Test edits forced by the removed trap path (recorded)
- `actor_clips_test.test_a_reaction_commits_to_the_trap_clip` is now `test_a_legacy_reaction_loads_no_trap_clip`, with
  DoesNotContain where it had Contains.
- `jaws_reaction_test.test_a_presented_reaction_never_commits_the_post_bite_trap_clip` is a deviation from "closed test
  files unchanged". It pinned the TEXT of the deleted loop (JAWS's `continue` inside `float? lastTrap = null; ...`), so
  it could not stay green once the loop was gone. Its name and intent are kept. It now asserts the stronger form: no
  `float? lastTrap` and no `CommitPlainClip("trap"` anywhere in the hunt. Every other jaws test is untouched, including
  `test_a_presented_reaction_loads_no_lay_a_trap_clip_and_repay_still_does` and the :720 pin.

### DONE WHEN, measured (evidence `production/qa/evidence/vfx-sweep/phase0-p04/`, logs + README, 0.7 MB)
- `--refs` for all five ref takes against BOTH baselines (`_a`, `_b`): **IDENTICAL x 10** on the first film.
- p04_quiver (BUILD `sig_quiver_backdraw,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`, seek
  `skill:sig_quiver_backdraw+unstruck`): at 12400 the batch is SPRAY Skill (slot 2), its Strike, the last EnemyDown,
  then BACKDRAW's Skill (slot 0). BACKDRAW gets `skill-skip` and no callout, vfx-spawn or sfx_cast. The one callout
  (SPRAY) and the one sfx_cast at that ms are slot 2's.
- p04_oathbound (OATHMARK + the same three): 4 OATHMARK answers, 0 `clip-start trap`, 0 pitched sfx_hit.
- p04_weaver (seeker fixture + RH_SHOT_KEYSTONES=weaver, extra): HARD HANDS at 11200 woven into SPRAY gives
  `echo slot=1`, one callout (HARD HANDS at its release), and no second sfx_cast.

### Open issues
- `ReactionRecipes.CalloutOverride` (RH_REACTION_CALLOUT, a JAWS with/without comparison toggle) is now inert. Nothing
  reads it, because reactions never call out. It is left in the closed ReactionRecipe.cs; remove it when that file is
  next touched with approval.
- A woven SPRAY / HARD HANDS echo falls to the plain legacy path at x0.6 (the `fx_seeker_projectile` strip). Keeping a
  recipe's look at echo scale is Phase 5's performance-class Echo mode.
- REPAY's and the Marks' zero-strike casts still draw their legacy cast. That is Phase 5's fizzle, as the task says.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Game.Tests **970/970**: 952 plus 18 new (hunt_generic_skill_test: the 10-case callout theory plus 8 facts, including
  a zero-allocation loop over IsStruck / IsEcho and text pins for the skip and echo gates). The 2 edited tests are
  listed above.
- Core.Tests **1901/1901**.
- `bash tools/check_all.sh`: all gates green.

### Disk
build/shots/sweep peaked at 6 MB. The PNG count was 0 after every take, and the p04 temp folder was deleted.
About 42 GB free.

---

## Phase 0 / P0.5: the generic heal receive and the ShieldGained claim (2026-10-03)

Nothing committed. Presentation only (no Core change; every ref take's `event` list is identical).

### What was done
- **`Presentation/HealReceive.cs`** (new, pure, fixed size): `Add(amount, ms)` (a zero heal is ignored), `Update(playheadMs,
  out flushAmount)` (the sum, once, 400 ms after the FIRST unprinted heal), `Flush(out)` (a wave's end), `Reset()`,
  `Age(ms)` (see Decisions) and `GlowAlpha(playheadMs)` (0.25 at the LATEST heal, quadratic ease-out to 0 at 300 ms).
- **`Presentation/ClaimedMs.cs`** (new): a fixed int[16] of claimed fight ms (`Claim`, `IsClaimed`, `Clear`; a full table
  overwrites its oldest claim). **`Presentation/ShieldGainShow.cs`** (new): `ShieldGainShow.For(atMs, claimed)` ->
  Rim always; Cue / OneShot / Number only for an unclaimed gain at ms > 0.
- **`HuntScreen.cs`**:
  - fields `_healReceive`, `_shieldClaims`, `_healClaims`; `internal ClaimShieldGain(int atMs)` and `internal
    ClaimHeal(int atMs)`. BeginWave prints the open sum (`FlushHealReceive`), resets the receive and clears both claim
    tables; a posed seek's rebuild resets the receive (the heals before it land silently, like every other beat).
  - Heal case: `if (!_healClaims.IsClaimed(e.AtMs)) _healReceive.Add(e.Amount, e.AtMs);` and nothing else: no
    PlayFx(HealColumn), no per-event Say, no sound.
  - After the batch: `if (_healReceive.Update(_playheadMs, out var healed)) SayHealed(healed);` (`+N` in Verdant, gated
    by ShowDamageNumbers).
  - Light pass: `healGlow` (0 when Downed) widens the pass condition; `DrawHealGlow` draws `fxp_flash_soft` (eager VFX
    part, no load in the frame) in 7FCB4A x alpha through `_vfx.BeginLight` (VfxBlend.PremultipliedAdditive), centred at
    0.40 of `_champDrawBox`'s height, 0.9 of its width, so it rides a lunge. Inside the existing `!ShotNoVfx` gate.
  - ShieldGained case: `var show = ShieldGainShow.For(e.AtMs, _shieldClaims.IsClaimed(e.AtMs));` The rim flare
    (`UiMotion.Flash(ShieldGainKey, ...)`) is unconditional; the cue, the one-shot and the `+N SHIELD` follow `show`.
    Absorb, break, the barrier and the RH_SHOT_SHIELDFX pose block are untouched.
- **`Vfx/VfxProfiles.cs`**: `HealColumn` removed from the class and from `All` (a comment says where the heal went).
  `fx_heal` stays on disk and in AssetLibrary's alias table (design section 6: its legacy move goes with the other
  retirements); check_asset_consumers stays green through that alias.
- **`vfx_contract_test.cs:179`**: HealColumn dropped from the pinned "follows his lunge" array (not a closed-reference
  test); `test_every_profile_is_named_by_a_live_spawn_site` green.

### Decisions (design.md silent)
- **The heal's time is the EVENT's ms** (`e.AtMs`), not the frame's playhead, so the window and the glow read the fight
  clock deterministically whatever the catch-up pattern.
- **The wave's last heal prints with the clear.** When the wave ends the playhead rests (no fight event reads it), so a
  heal on the wave's last ms waited ~1 s for the next BeginWave (seen in the first DRINK take: `+57` at the next wave's
  open). The break branch calls `FlushHealReceive()` (a no-op once printed).
- **The glow fades on the break's clock.** At a resting playhead `GlowAlpha` would hang lit for the whole break; the
  break's no-action branch calls `_healReceive.Age(dt * 1000 * speed)`, which moves the latest heal back in time (the
  sum's window is not moved). When an action still plays out, the playhead itself advances and nothing is aged.
- **The wave-open gain keeps the rim flare** ("the bar rim flare ALWAYS stays"): it is the bar's own transition, the
  barrier simply up. It loses the cue and the one-shot (it already had no callout).
- **Claims belong to a wave**: cleared at the top of BeginWave, so a recipe built later in BeginWave (Phase 2/3 layers
  are built there from the wave's events) can claim the wave's ms ahead of their presentation.
- **ClaimHeal has no claimer yet.** It is consumed in Phase 3 (DRINK, PAYING WORK, HOLD FAST's DEEP ROOTS, WILT's SUP,
  MIRE's Nature heal, TRICKLE). ClaimShieldGain's only live use in Phase 0 is no claim at all: the wave-open rule is
  `ShieldGainShow.For`'s ms-0 clause; HOLD FAST / BANKED claim in Phase 3.
- **The +N callout and the glow are not delayed by +280 ms** (that is the stream's arrival; the generic receive has no
  stream, design 4 RETURN).

### DONE WHEN, measured (evidence `production/qa/evidence/vfx-sweep/phase0-p05/`, logs + README, 0.7 MB)
- Ref takes, filmed at the final code, vs BOTH baselines under `--refs`: **IDENTICAL x 10** on the first film. None of
  the five fixtures holds a Heal or a ShieldGained, so no draws= bound needed excluding.
- p05_drink (seeker, BUILD `sig_seeker_hard_hands,drain_drink@Nature,hammer_press@Body,snare_jaws@Shadow`, seek
  `skill:drain_drink`, RH_SHOT_ENEMY=1500,25): two Heals at 9700 (46 + 11) -> ONE `+57` at playhead 10113; one Heal at
  18700 -> `+15` at 19113. 0 `fx_heal` vfx-spawns, no heal sound. (RH_SHOT_ENEMY=3000,60 was refused loudly: the wave
  has no DRINK cast, the Seeker falls first.)
- p05_holdfast (unbroken, RH_SHOT_VARIATION=sig_unbroken_hold_fast:BREASTWORK+GROUNDWORK, 150 x 8): the at=0
  ShieldGained (5) has no `sfx_shield_gain`, no `shield.gain` spawn, no callout; the mid-wave gains keep cue 0.40 +
  one-shot + `+10 SHIELD` (unclaimed until Phase 3).

### Open issues
- **`hunt_generic_hits_test.test_a_synthetic_batch_through_the_pure_helpers_allocates_nothing...` (P0.3) failed ONCE**
  in the first full Game.Tests run of this package and passed in isolation and in two later full runs. It is the
  1000-iteration GC.GetAllocatedBytesForCurrentThread loop; likely runtime tier-up work landing on the test thread.
  Not touched here (no new code on its path); if it recurs, warm it longer or measure the minimum of several runs.
- The chest glow's look (size 0.9 of the width, 0.25 peak) has not been looked at on a still yet; it is a soft part, in the
  light pass, so it cannot have a rectangle edge, but a contact-sheet look belongs with the Phase 3 RETURN work.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Game.Tests **980/980**: 970 plus 10 new (heal_receive_test 6: the summed number, a new sum after the window, the glow
  peak / ease / 300 ms / break ageing, the 10 s zero-allocation loop, the Heal-case text pin, the hunt's flush / light
  pass / BeginWave / break pins; shield_claim_test 4: claimed gain, wave-open gain, unclaimed gain + absorb / break
  untouched, the claim table). vfx_contract_test (1 edit, above) and vfx_shield_test green.
- Core.Tests **1901/1901** (no Core change).
- `bash tools/check_all.sh`: all gates green.

### Disk
build/shots/sweep peaked at 6 MB; PNG count 0 after every take; the p05 temp logs were copied to evidence and deleted.

---

## Phase 0 / P0.6: Phase 0 acceptance - regression, film, gates, evidence (2026-10-03)

Nothing committed. No new features: the only file edit outside evidence is seven `after_*` take lines in
`films_sweep.sh`. No C#, no Core, no asset touched.

### What was done
- **(a) Reference regression.** The five ref takes were filmed at the final Phase 0 code (`films_sweep.sh p06 ...`)
  and held with `action_regression.py --refs` against BOTH baseline films: **IDENTICAL x 10, all on the first film**.
  The `event` lines match in order in every pair (ref_fast's after-film traces 4 more events than `_a`, the same 4
  `_b` has: window coverage). The after-traces and the full comparison output are in
  `production/qa/evidence/vfx-sweep/phase0/` (`ref_*.log`, `regression.txt`).
- **(b) The film.** Seven after takes were added to `films_sweep.sh` and filmed one at a time with `--mp4`
  (150 frames each, frames deleted after each mp4): after_multi and after_bleed (the before films' exact poses),
  after_default (the default fixture, 10 s), after_backdraw (the phantom), after_weaver (the echo), after_drink (the
  summed heal), after_holdfast (the wave-open plate). The sound comes from each trace through film_audio.py and
  sound_throttle.json. Each mp4 was re-encoded at 720p, CRF 28: 0.39-1.5 MB each, 4.3 MB in total. All are committed,
  so no contact sheets were needed. The evidence README has the measured table per take:
  - after_bleed: 10 bleed-only batches. Before, there were 10 flashes and 10 Strike thuds on them; now there are 0 of
    each. sfx_hit asks went from 19 to 9, and all 10 numbers print at grade=Quiet.
  - Every take: at most 1 Strike sfx_hit per batch ms.
  - BACKDRAW: 1 skill-skip. WEAVER: 1 echo. DRINK: 1 `+57` (46 + 11) and 0 fx_heal spawns.
  - HOLD FAST: the wave-open gain plays 0 sfx_shield_gain; the mid-wave gains play 5.
  - Callouts read SPRAY / HARD HANDS / BLOW / DRINK; nothing for fields or reactions.
  - Alloc per layer: reaction-draw is 0 on every frame but one (see below); field-draw is PRESS's known first-use jump,
    the same as before.
  Two frames were looked at as stills (after_bleed, after_drink): judged ready.
- **(c) Gates.** `bash tools/check_all.sh`: all green. `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0
  warnings, 0 errors. Core.Tests 1901/1901, Game.Tests 980/980.
- **(d)** `git diff ae20a7fd -- assets/audio` is empty and `git status assets/audio` is clean. The jaws, press and brand
  SHA-256 pins are green.

### Phase 0 summary: the decisions (all recorded in their package sections above)
- **The throttle table was generated early** (P0.2). Design section 7/8 put `sound_throttle.json` in Phase 6, but
  films_sweep renders audio through film_audio.py from Phase 0 on. It is generated from SoundBank by
  `sound_throttle_table_test`, and film_audio's hand-written mirror is deleted.
- **The callout text changed on the Seeker's actives** (P0.4): VOLLEY is now SPRAY and HAMMER is now HARD HANDS, per
  section 3's rule that an Active says its own name. Callouts are excluded from `--refs`, so the references hold.
- **One sfx_hit per batch ms, at the FIRST qualifying Strike's volume** (P0.3). Performed, reaction, aura and quiet
  Strikes neither voice nor mark the ms. sfx_crit is also once per ms.
- **The phantom rule is scoped to damaging skills** (P0.4): `IsPhantom = !struck && DealsDamage` (BasePower > 0 and
  not Amplify), so a buff or mark cast with no Strike still draws. A presented reaction (JAWS stopped by IRON) is
  exempt, so the closed JAWS picture is unchanged.
- **The heal claim hook stays unconsumed until Phase 3** (P0.5). `ClaimHeal` has no caller yet. `ClaimShieldGain`'s
  only Phase 0 rule is the wave-open (ms 0) clause in `ShieldGainShow.For`.
- **The quiet number grade** (P0.3): `DamagePx / 2`, alpha 0.70, no shadow (`Callout.Bare`). A quiet crit keeps the
  crit colour and plays no sfx_crit. Major is reserved and prints at Skill grade until Phase 3.
- Also: the last-owner rule (P0.3); the heal window runs on the event's ms and the wave's last heal prints with the
  clear (P0.5); the signature must sit in slot one, and the live build is verified at the shutter (P0.2).
- This package: the default fixture's "before" is the P0.1 baseline trace `ref_seeker_a.log`, not a new before film.
  Filming one would need a second checkout and a content build at ae20a7fd, and the trace already holds every number
  in the table.

### Phase 0 deviations (from the package sections)
- `jaws_reaction_test.test_a_presented_reaction_never_commits_the_post_bite_trap_clip` was rewritten (P0.4). It pinned
  the text of the deleted trap loop; its intent is kept in a stronger form. `brand_audio_test` now reads the
  unthrottled set from sound_throttle.json (P0.2); the assertion is unchanged. No approved cue, picture or pin moved.
- `action_regression.py --refs` was loosened for Draw-sampling effects (P0.1/P0.2): repeated samples, skipped Draws,
  frozen-playhead field/mark draws, and field-draw body x / alloc>0 held per film. Each relaxation is named in the
  script and leaves the negative controls caught.
- `WaveReplay.LastTrapBefore` was removed as dead code (P0.4). This is the only Core edit, and it is presentation-only.

### Open issues (carried to later phases; nothing fixed here)
- **Content mismatches, reported, not fixed:** REPAY CARRIED's card says 10 % but the dial is 0.05
  (SkillCatalogue.cs:482-483 vs SoloBattle.cs:1205-1206). DRINK SIPHON's card says the per-wave limit doubles, but the
  variation only doubles Lifesteal; the ceiling is raised only by BuildTrigger.Siphon. Both are design calls for the
  owner, not presentation.
- **BACKDRAW: a Core `alive > 0` gate is recommended.** On the wave's last kill the Skill event is emitted with no
  Strike (no alive gate, SoloBattle.cs:1919-1923). Presentation now skips it (skill-skip), but the arm is spent for
  nothing. This is a separate gameplay decision, with a fingerprint test, if taken.
- PRESS's 13-14 MB first-use allocation inside the field's Draw window (P0.1) is unchanged in every after take. It is
  for Phase 2's FieldLayers.
- **after_multi showed one reaction-draw frame of 5032 bytes** (playhead 1233). A trace-only re-film had all 73
  frames at 0, so it is runtime lazy work and not repeatable. Watch for it in Phase 4's reaction work.
- The bite lunge advances on frame dt (`HuntScreen.LungePx`), which makes host x sampling-dependent (P0.2/P0.3). This
  is for the Phase 1 swing work.
- `hunt_generic_hits_test`'s zero-allocation loop failed once in P0.5. It was green in this package's full run.
- **The DRINK cast still plays the legacy `fx_seeker_transformation_strip8_512` (cast.transformation).** On the
  after_drink still it reads as a green rectangle around the hunter, with hard edges. It is pre-existing and replaced
  by DRINK's REACH mode in Phase 3; it is a reason not to judge DRINK from this film.
- `ReactionRecipes.CalloutOverride` is inert; remove it when ReactionRecipe.cs is next touched with approval (P0.4).
- `capture_seq.sh` prints "captured 100 frames" for 150-frame takes (cosmetic, P0.1).
- `build/shots/brand/` (not this sweep's folder) still holds 41 PNGs (`_body_points`, `scale`) from the BRAND work.
  They were left alone.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Core.Tests **1901/1901**, Game.Tests **980/980** (Phase 0 total: 918 -> 980, +62 new; no Core test added because
  Core is unchanged).
- `bash tools/check_all.sh`: all gates green.

### Disk
build/shots/sweep peaked at 22 MB. The PNG count was 0 after every take and is 0 now. The p06 and p06b temp folders
were deleted, so build/shots/sweep holds 5 MB of earlier packages' logs. build/shots is 39 MB and build/tmp 3 MB, far
under 500 MB of temp. Committed evidence: phase0/ is 5.1 MB (7 mp4s, 12 logs, regression.txt, README). The drive has
about 37 GB free.

---

## Phase 0 / review film for the presentation director (2026-10-03)

No C#, no Core and no asset were touched.

### What was done
- Ten takes for the director, who cannot watch video, plus one trace-only take, in
  `production/vfx-sweep/review/phase0/` (about 17 MB). INDEX.md lists every take: its item, its files, what to look
  for, and a numbers table counted from the traces.
- Each take has an 8-cell contact strip at true-speed spacing (half-size arena crops, decisive cell outlined), one or
  two full 1920x1080 stills, a trace excerpt with the per-layer alloc summary, the full log, and a 720p CRF 28 mp4
  with sound. PULSE also has a 0.25x render of its tick (40 frames at 60 fps).
- The BEFORE takes (before_multi, before_bleed) are the P0.1 ae20a7fd films. Their frames had been deleted, so their
  strips and stills are read back out of those mp4s at film_audio's trace-clock offsets.

### Files
- NEW `tools/asset-pipeline/review_take.py`: QA tooling. It takes a traced take (PNG frames, or an mp4 for a before
  film) and writes the strip, stills and trace excerpt.
- `tools/asset-pipeline/films_sweep.sh`: ten `r_*` take lines were added (r_after_multi, r_after_bleed,
  r_after_fast, r_after_pulse, r_after_pulse_slow, r_after_backdraw, r_after_oathmark, r_after_drink,
  r_after_holdfast, r_weaver_trace).

### Decisions and deviations
- **PULSE on a MIRE tick does not exist at seeds 7, 8 or 9.** RH_SHOT_SEED does not move the fixture's wave (214
  events every time), and enemy baselines 1400-4500 do not create one either. With TEMPO taken up to BLITZ
  (`RH_SHOT_TAKE=bite,swift,quick,road_sign_2,brisk,rhythm,blitz`, seed 7), PULSE casts at 9000 on MIRE's tick. That
  is committed in the take line.
- **REPAY CARRIED is a reinforcement of BANKED**: the variation is `snare_repay:BANKED+CARRIED`. CARRIED alone is
  refused by the progression.
- **BACKDRAW's "low health"** is `RH_SHOT_ENEMY=800,9`. At 300 the wave ends before the arm. At 1100 there is no
  unstruck BACKDRAW. At 800, SPRAY's last kill at 9800 is shared with BACKDRAW's unstruck Skill.
- **"Fast biters"** is `RH_SHOT_ARCHETYPE=Swarm` (the wave's own archetype anyway).
- r_after_multi keeps the fixture build (`-`). `RH_SHOT_BUILD` with `volley_spray@Mind` is refused under fightmulti,
  because the fixture's CLUSTER variation owns SPRAY's Source (Body).
- The five refs were not refilmed. No .cs file changed after P0.6's ref traces, and `--refs` on them is still
  IDENTICAL x 10. after_fast (the ref_fast pose at 60 fps) is also IDENTICAL under `--refs` against
  `ref_fast_a.log`.

### Findings (for the director; not fixed in this package)
- **The last-owner rule fails for the NUMBERS (after_pulse_on_mire, 9000).**
  - PULSE prints `-158 x2 / -155 x2 / -138 x2`: its 134 / 143 / 124 merged with MIRE's same-ms Strikes of
    24 / 12 / 14.
  - MIRE's tick total then prints those 50 again at 9500, so MIRE's damage is shown twice.
  - Flash and thud do follow the rule (3 flashes, one sfx_hit).
  - The number accumulator batches same-ms Strikes per slot without regard to the owner. This needs a Phase 0
    follow-up fix, with a test, before Phase 2's field work.
- The before_multi pose cannot show "N thuds on one cast", because the Seeker's SPRAY batch is PERFORMED (0 generic
  thuds before the fix too). The thud rule shows on the bleed and fast takes instead.
- The before_multi film saved no frame between 7830 and 8297 (a capture gap at ae20a7fd).
- Generic paths that are still pending and visible in these films (later phases, not Phase 0):
  - DRINK's legacy hard-edged green `cast.transformation` column.
  - JAWS@Spirit's `fx_unbroken_trap` row spikes on the unbroken.
  - PRESS's generic pink ring on non-Seeker champions.
  - The quiver's projectile, launched on the ms its hit already resolved.

### Disk
build/shots/sweep peaked at 148 MB during a take. It holds 0 PNGs now, and its review0 folder is emptied with
find -delete. build/shots is 43 MB and build/tmp 3 MB.

### Tests
Not run: no code changed, only a QA script and take lines.

---

## Phase 0 / correction pass from the director's review of the footage (2026-10-03)

Nothing was committed. Core is untouched.

### What was done (all mandatory items, and every polish item except the ones declined below)
- **The number fold stops at the next owner** (semantic-wrong). The fold loop moved into the pure
  `GenericHits.Fold(batch, index, summed, out hits)`. It breaks at the next Aura or Skill event at the ms, the same
  bound `IsStruck` uses, so no aura-owned Strike is ever put in `_summed`. Refilmed: PULSE prints its own
  134 / 143 / 124 with hits=1, and MIRE's tick total prints once, as 50.
- **The tick total flushes ON the tick.** `if (_auraTotal > 0) FlushAuraTotal();` now runs after the batch loop, next
  to the heal update. The +500 ms wall-clock flush is deleted. MIRE's 9000 total now prints at 9000.
- **performedHit and reactionHit follow the last-owner rule** (semantic-wrong). They now require
  `lastOwner.IsSkillsBlow(e, performer.SkillSlot)` (and the reaction's slot, via a new `reactionHitSlot` local). The
  new `StrikeOwner.IsSkillsBlow` means: owned, the owner is a Skill, and the slot matches. A WEAVER echo's Strikes are
  `echoHit` (the Skill case records `echoAtMs` / `echoSlot`). They take the generic path at echo scale: the flash is
  `UsualFlash.Peak x EchoScale (0.6)`, and the thud is `0.22 x GenericHits.EchoCueScale (0.5)`, still one per ms.
  - Refilmed WITH frames (`r_weaver`). At 11200 HARD HANDS' 0.55 / 120 flash is on slot 0 only. The echo's slots
    1-3 flash at 0.27 / 80 and ask one sfx_hit at 0.11.
- **The enemy number lane is anchored to the drawn silhouette** (polish, done).
  - `NumberLane` (new, pure) holds the positions. Plain / skill / crit numbers start at `HeadY` = the drawn top
    - `Space(16)` (clear of the life pip) - CritPx.
  - Quiet numbers start at `QuietY` = drawn top + 0.4 h, in their own `EnemyQuiet` lane, 3 deep.
  - Both read `_actors.TryBounds(Creature(slot))`, the bounds `CreatureCentreX` already uses. The old row lane is
    kept only as the fallback for the moment before any frame has drawn.
- **Each body stacks its own numbers** (a decision made while filming). The first refilm showed that the global enemy
  stack climbs ACROSS creatures: three creatures struck at once printed a staircase. `Callout.Owner` (the creature
  slot) is now set, and `StackSlot(lane, deep, owner)` counts only that creature's live numbers. StackSlot is also a
  loop now instead of a LINQ `Count` with a closure, so it allocates nothing.
- **The quiet grade keeps the one-pixel drop shadow** (`Bare = false`). This is the fallback the director allowed: not
  an outline. Born at 0.3 h, the `-35` landed on the whelp's back line in a crouch and rose onto stone. At 0.4 h with
  the shadow it reads on the body (after_bleed_fixed_still_9033). The size and alpha (DamagePx / 2, 70 %) are
  unchanged.
- **The heal's "+N" is a number at the chest** (polish). It spawns at the chest-glow point on `_champDrawBox`
  (0.40 h), at DamagePx, in Verdant, in its own `HunterBody` lane, traced as `heal-number`. It no longer goes through
  Say, so the Say lane carries names only (plus SHIELD BROKEN / UNDYING / "+N SHIELD", which were left alone).
  - It prints only when the sum is at least `HealReceive.NumberShare` (0.01) of `_champ.MaxHealth`, with a floor of 1
    (`HealReceive.NumberFloor` / `ShowsNumber`). MIRE's 1-point heal no longer prints `+1`; DRINK's 57 prints.
- **No cast breath for reactions** (polish). `sfx_cast` is now inside `if (!echo && !isReaction)`, the gate
  CalloutFor uses. OATHMARK's open plays only the bite's own thud.
- **A legacy reaction's pitched +0.25 thud marks `_hitVoicedAtMs`**, so its own Strike's 0.22 does not ask. Trace at
  HOLD FAST 1000: one +0.25 thud, no sfx_cast, no 0.22.
- **The chest glow, looked at.** This is a QA-only take, `after_drink_glow_novfxcol_fixed`, with
  `RH_SHOT_STRIP_FILES` blanking `fx_seeker_transformation_strip8_512` (`RH_SHOT_NOVFX` hides the glow too).
  - The glow at 0.25 reads as a faint green haze on the chest: present, but subtle.
  - The Verdant `+57` on his green-brown tunic is low-contrast.
  - Neither was retuned. Both are Phase 3's RETURN arrival to judge. Not signed off.

### Files
- `src/IdleXIdle.Game/HuntScreen.cs`: the Strike / Skill cases, `EnemyNumberY`, `StackSlot`, `SayHealed`, the
  batch-end aura flush, `Callout.Owner`, and the new lanes.
- `src/IdleXIdle.Game/Presentation/GenericHits.cs`: `Fold`, `EchoCueScale`, `StrikeOwner.IsSkillsBlow`.
- `src/IdleXIdle.Game/Presentation/HealReceive.cs`: `NumberShare`, `NumberFloor`, `ShowsNumber`.
- NEW `src/IdleXIdle.Game/Presentation/NumberLane.cs`.
- Tests:
  - `hunt_generic_hits_test.cs`: both orders (Skill-then-Aura, Aura-then-Skill), the multi-hit fold up to an echo,
    zero allocation of the fold, the tick flush on the batch, the quiet number inside the body (3 bodies x 3 stacks),
    the head anchor, and the per-creature stack pins.
  - `hunt_generic_skill_test.cs`: an echo's Strikes are not performed, the owner-slot / echo-scale pins, the reaction
    breath gate and the voiced ms, and zero allocation of the owner reads.
  - `heal_receive_test.cs`: the 1 % floor, a 1-point tick prints nothing, and SayHealed is not Say.
- QA tooling:
  - `films_sweep.sh`: new take lines `r_weaver` (150 x 2) and `r_holdfast_trace`.
  - `review_take.py`: keeps `heal-number` rows.
- Evidence:
  - `production/qa/evidence/vfx-sweep/phase0-fix/`: the five ref traces + regression.txt, 0.5 MB.
  - `production/vfx-sweep/review/phase0/*_fixed*` and the INDEX.md addendum.

### Decisions and deviations
- QuietBodyShare is **0.4**, not the review's ~0.3, for the reason above. The geometry test pins "inside, never above
  the drawn top".
- **The enemy-lane move applies to every enemy number, JAWS' answer included** ("-1 JAWS" now prints over its
  creature's pip). It is the director's "one move". The number's position is not in any `--refs` line, and JAWS'
  picture, cue and number timing are unchanged.
- **No closed-reference test was edited.** The two thud lines keep the exact one-line shapes that `jaws_reaction_test`
  pins (the voiced-ms mark is a second `if` line). The first build of this pass broke that pin, and it was restored by
  reformatting the code, not by editing the test.
- PULSE's slow 0.25x render was not refilmed. Only its numbers changed, and the new full-speed take has every frame at
  2x stride around 9000.

### Declined (and why)
- **All legacy pictures** (PULSE's fx_aura, DRINK's column, PRESS's wall-clock ring on other champions, HOLD FAST's
  spikes, JAWS@Spirit's spikes, the shield-gain ring, the baked chest glow, the whelp's smoke lunge). They are
  scheduled for Phases 1-4 or closed, as the review says.
- "+N SHIELD" still goes through Say. The review names it but asks only that the heal move. Phase 3's shield claim
  replaces it.

### Open issues
- **The life pip (and so the head numbers) float 50-70 px above crouching whelps.** The drawn bounds are the idle
  reference strip's, which includes the tallest pose. The quiet number compensates (0.4 h). A per-frame body point
  (the .mark.json points exist for the Seeker strips) would fix both, but it is a separate change to the pip.
- The quiet glyph (DamagePx / 2, about 18 px) reads, but small. The size is design.md's.
- `py` (the launcher) has no imageio_ffmpeg here, so film_audio under `films_sweep.sh --mp4` produced no mp4 in this
  session. My helper re-ran it under `python`. `films_sweep.sh` was not changed.

### Measured
- `--refs` against both ae20a7fd baselines: **IDENTICAL x 10** (ref_seeker, ref_fast, ref_brand, ref_brand_press,
  ref_jaws_kill).
- after_pulse_on_mire_fixed at 9000:
  - `number slot=1/2/3 amount=134/143/124 hits=1`, then `number slot=1 amount=50` on the same ms.
  - One sfx_hit at 0.22.
  - No heal-number in the take.
- weaver_fixed at 11200: one flash at peak 0.55 (slot 0), three at 0.27, and one sfx_hit at 0.11.
- after_oathmark_fixed: 0 sfx_cast.
- after_drink_heal_fixed: `heal-number amount=57` at 10100. There is no `callout +57`.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Game.Tests **999/999** (980 -> 999, +19). Core.Tests **1901/1901**.
- `bash tools/check_all.sh`: all gates green. `git status assets/audio` is clean, and the SHA-256 pins are green.

### Disk
- Every take was filmed one at a time, and its PNGs were deleted right after its pieces were made.
  build/shots/sweep has 0 PNGs; it peaked at 31 MB.
- build/shots is 44 MB and build/tmp 3 MB.
- The review folder grew to 30 MB, with 44 `_fixed` files: 7 mp4s at 720p CRF 28, plus the strips and stills.

## Phase 0 / verify and commit (2026-10-03)

The director's verdict on the `_fixed` evidence: mandatory items 1-5 RESOLVED (the fold stops at the next owner, the
tick total prints on the tick once, the performed / reaction contact only for the owner's slot, no cast breath and no
double thud on reactions, the five references held). Polish items: numbers born on the body with per-creature stacks
RESOLVED; the quiet number inside the body RESOLVED (marginal), accepted for Phase 0 with the per-frame body point as
the real fix.

### What was done
- **No code change in this package.** The heal `+N` at the chest is already a shadowed number in its own HunterBody
  lane with the 1 % floor (correction pass). What is left is the Verdant-on-tunic contrast and the faint glow. That is a
  colour / art call for Phase 3's RETURN arrival, not a small clear fix, so it is recorded as open (below).
- **The references were re-filmed on the final code.** HuntScreen.cs / NumberLane.cs were edited after
  `phase0-fix/regression.txt` was written (22:35 against 22:40). The five ref takes were filmed again
  (`films_sweep.sh commitcheck ...`) and held against BOTH ae20a7fd baselines: **IDENTICAL x 10**. The traces and the
  output are in `production/qa/evidence/vfx-sweep/phase0-commit/` (0.5 MB).
- **Prototype / dev selectors checked:** nothing new. There is no candidate or concept switch in src. RH_SHOT_BUILD /
  RH_SHOT_SEEK are QA capture rig, kept by design.md section 9.
- **The one Core diff** is the removal of `WaveReplay.LastTrapBefore`. It was presentation-only and lost its last
  caller when P0.4 retired the opportunistic trap clip. No gameplay number moves; Core.Tests are green.

### Open (carried)
- A per-frame body point for the life pip and the head / quiet numbers, done together (the director: "not now").
- The heal `+N` contrast (Verdant on the green-brown tunic) and the 0.25 chest glow: Phase 3.
- `+N SHIELD` still goes through Say. Phase 3's shield claim replaces it.
- Everything in the earlier Open issues lists of P0.5 / P0.6 / the correction pass.

### Tests
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors.
- Game.Tests **999/999**, Core.Tests **1901/1901**.
- `bash tools/check_all.sh`: all gates green. `git status assets/audio` is clean, and the SHA-256 pins are green.

### Disk
- build/shots/sweep has 0 PNGs (6 MB of logs). build/shots is 44 MB in all; the 41 PNGs left in build/shots/brand are
  earlier BRAND work. build/tmp is under 3 MB.

## Phase 1 / P1.1: swing timing data, clip_markers / strip_bake, the four baked-VFX cleans (2026-10-03)

Nothing changes on screen. Not committed.

### What was done
- **NEW `tools/asset-pipeline/v2/clip_markers.py`** (`propose` / `write` / `sheet`; runs under `python`, which has numpy).
  - `propose` writes a draft `.draft.clip.json` and an 8-cell sheet with the socket drawn on it to `build/tmp/clip_markers/`.
    It never touches assets.
  - Markers come from frame-delta energy: the biggest premultiplied change between frames 2 and 6 is the contact /
    release, the frame before it is the commit, the most pulled-back earlier frame is the anticipation, recovery is
    +2 and settle is the last frame.
  - Sockets come from the brightest moving blob: the brightest fifth of the texels that differ from frame 0, the blob
    with the most light weighted toward the front, and the centroid of its brightest texels.
  - `write` writes the REVIEWED table (`clip_markers_reviewed.json`, data beside the tool) as the committed file. The
    file carries `"place": "own"` and a `source` block: the tool, the raw `proposed` markers and sockets, and `eye`
    (every value judged by eye, with why). `sheet` draws the committed files for evidence.
- **The ten `assets/art/Animations/Roster/<id>_attack/char_<id>_attack_strip8_512.clip.json`**, each with frameMs[8],
  elastic[8] (elastic = before the commit, and from the recovery on), all five markers, the socket on the anchor frame
  and its neighbours, `"place": "own"` and `source`. Totals are 600-730 ms, with the anchor 240-430 ms in.

  | id | anchor | commit | recovery | socket (frames) |
  |---|---|---|---|---|
  | seeker | contact 4 | 2 | 6 | StrikeHand 3-5 |
  | anvil | contact 5 | 4 | 6 | StrikeHand 4-6 |
  | metronome | contact 2 | 1 | 6 | StrikeHand 1-3 |
  | tower | contact 5 | 3 | 6 | StrikeHand 4-6 (the hammer head) |
  | thornwall | contact 3 | 2 | 5 | StrikeHand 2-4 (the shield boss) |
  | magpie | contact 3 | 2 | 5 | StrikeHand 2-4 |
  | quiver | release 4 | 3 | 5 | BowHand 3-5 |
  | chorus | release 5 | 4 | 6 | ThrowHand 4-6 |
  | unbroken | release 5 | 4 | 6 | ThrowHand 4-6 |
  | oathbound | contact 4 | 3 | 6 | LashHand 2-5 |

- **THE INERTNESS GUARD.** `ActionClipTiming.Parse` reads an optional `"place": "own"` as `ActionClipTiming.PlaceOwn`.
  Only the string "own" counts. It survives FitBefore / FitRecovery / CutAt. `HuntScreen.PlacedAs` now keys only a
  timing with `PlaceOwn: false` onto the idle. `action_joins.py` honours the same field, so its placement arithmetic
  still matches the renderer.
- **NEW `tools/asset-pipeline/v2/strip_bake.py`** (`clean` / `check` / `sheet`).
  - KEY: a strict hue core, kept only in pieces of at least `min_area` texels, grown THROUGH fringe-coloured texels
    (connected only). Optional extras: `reach` (fringe tint near the core, for the Unbroken's glow across dark seams) and
    `detached` (small pieces cut off from the figure: a flash's specks, the flying charms).
  - FILL: each keyed piece is filled from the best-matching DONOR frame and shift (matched on the ring round the piece,
    never through a texel keyed there). Where the donor is opaque the texel takes its colour (the body behind the flash);
    where the donor is clear the texel is cleared.
  - Passes repeat until nothing is keyed. A later pass also takes core flecks of any size within 28 texels of where the
    effect was. This is how a charm joined to the glow is only seen as detached once the glow is gone.
  - FEATHER: one final pass over the whole cut (cleared against the original). Opaque texels within one SOURCE pixel
    (the strip's detected art-pixel block, at least 2 texels) of a cleared texel get their alpha capped at
    255 x distance / (block + 1).
  - `check` fails while any core piece >= min_area (or a detached piece) is left in the cleaned frames, or while a
    full-alpha texel touches a texel the clean cleared ("hard cut"). The before is `git show 702576d6`.
  - `clean` is idempotent: a second run writes nothing.
  - The four strips were cleaned: thornwall (white flash, 0-based frames 2-4), magpie (neon arc and glowing blade, 3-7),
    chorus (blue glow and flying charms, 5-7), unbroken (orange glow, fireball and spark, 4-6). The originals are in git
    history.
- **Tests:** NEW `tests/unit/IdleXIdle.Game.Tests/swing_timing_test.cs`, 66 tests. Per champion (6 theories x 10):
  - parses with 8 frames and the expected anchor;
  - marker order (commit < contact < recovery <= settle for blows and reaches; commit < release < recovery for missiles;
    settle = 7);
  - the commit-to-anchor frames are not elastic;
  - the named socket exists on anchor-1, the anchor and anchor+1;
  - PlaceOwn;
  - the `source` block names clip_markers.py, with `proposed` and `eye`.

  Plus 6 facts: Parse takes only the word "own"; PlaceOwn survives every refit; HARD HANDS / SPRAY stay placed as the
  idle; ActionClipLibrary loads all ten; a text pin that PlacedAs reads `is { PlaceOwn: false }`; no recipe resolves
  `attack` for any of the ten, and TryAuthored still resolves through ActionRecipes.For and the recipe's own strip.
  The action_presentation_test pin is unedited.

### Decisions
- **"place": "own" (the inertness decision).** The attack strips keep their own measured placement, as today. A timing
  file is data for the swing performance (P1.2+), not a re-placement. If one of them were keyed onto the idle, it would
  draw at the idle's scale and move the champion envelope that PRESS and the other champion-side effects measure.
- **The clean does not move the figure.** Removing the arc / flash widened the measured side pad of two strips
  (thornwall 48 -> 56 px, magpie 28 -> 53). The side crop is symmetric about the centre, so every remaining texel draws
  where it did. The top and bottom pads are unchanged on all four, so the scale is unchanged too. The envelope of those
  two champions shrinks only where the baked effect was.
- **Chorus: the charm in her fingers on frame 4 is KEPT** ("the bundle in the hand = the charms she holds"). Only the
  charms in the air (5-7) and the glow go. Her release is therefore 5, not 4, so the object in the air is the one that
  was in the hand.
- **Magpie: the green "blade" at her hand is part of the baked effect and is removed with the arc.** The hand is now a
  bare fist on 3, 4 and 7; the slash is the recipe's smear (design.md 5.23). See Open.
- **The Thornwall's flash frames are 0-based 2-4.** design.md's "frames 3-5" is 1-based; the sheet shows the flash
  on 2, 3 and 4.
- **StrikeHand on the Tower and the Thornwall is the striking face** (the hammer head, the shield boss), not the hand
  that holds it. This is the same convention as HARD HANDS' "fist's striking edge": the socket is where the contact lands.
- **Markers judged by eye rather than taken from the proposal** (each one is also in its file's `source.eye`).
  - Every socket on all ten strips. The proposal found a torso, a head, a cloak or a belt on most frames; it only
    ever served as a rough region.
  - seeker: contact 4 (proposed 3; design.md), anticipation 1 (0), recovery 6 (5).
  - anvil: contact 5 (4), anticipation 2 (0), commit 4 (3).
  - metronome: recovery 6 (4). The contact 2 proposal was kept.
  - tower: contact 5 (6), anticipation 1 (4), commit 3 (5).
  - thornwall: contact 3 (2).
  - magpie: contact 3 (2).
  - quiver: release 4 (2), commit 3 (1).
  - chorus: release 5 (4), anticipation 2 (0), commit 4 (3).
  - unbroken: release 5 (3), anticipation 3 (0), commit 4 (2).
  - oathbound: contact 4 (5, design.md), anticipation 1 (0), commit 3 (4).
- Timing values are authored in the spirit of cohort A (routine, short, the commit-to-anchor frames rigid). design.md
  gives no frame durations for the basic attacks; they will be tuned when the swing performance is filmed.

### Verified
- `strip_bake.py check`: OK x 4 (0 baked texels left, 0 hard-cut texels).
- The before/after sheets were looked at: no flash, arc, charm or glow left, and no hard cut. Residue left on purpose:
  the Unbroken's frame 4 tunic below the belt keeps a warm brown-red cloth shading (natural leather tones on clean
  frames too, not the glow's orange), and steel highlights stay.
- **The five ref takes, re-filmed (`films_sweep.sh p1p1 ...`), are IDENTICAL x 10 under `--refs` against BOTH ae20a7fd
  baselines.** The output is in `phase1-p1/regression.txt`.
- Thornwall trace take (default fixture, 20 x 30), with the original strip restored from git for `tw_before` and the
  cleaned one for `tw_after`: the champ-frame lines are identical, and every other trace line is identical apart from
  the sampled playhead (`phase1-p1/tw_trace_diff.txt`).
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror`: 0 warnings, 0 errors. (The first build after the edit
  reported 1 error and no message, and the immediate rebuild was clean; it looks like a transient OneDrive file lock.)
- Game.Tests **1065/1065** (999 + 66). Core.Tests **1901/1901**. `bash tools/check_all.sh`: all gates green.
  `assets/audio` is untouched.

### Evidence
`production/qa/evidence/vfx-sweep/phase1-p1/` (2.6 MB):
- the ten `<id>_attack_markers.png` (cell 150) and the four `<id>_attack_before_after.png` (cell 160; before, then after
  on a dark and a light ground);
- the five ref traces and `regression.txt`;
- `tw_before.log`, `tw_after.log` and `tw_trace_diff.txt`.

### Open issues
- **The Magpie's dagger.** With the baked green blade gone, the "dagger nick" has no visible blade in the hand. Either
  the swing recipe's slash carries it, or a small steel blade prop / one key-pose edit gives her a dagger (P1.2 or later
  to judge on film).
- **The Chorus's frame 4** shows the held charm while the timing's commit is 4. The prop launches on 5 from ThrowHand,
  so there is no double; P1.2 must not draw the prop in the hand on frame 4 as well.
- **The basic-attack timings are not on screen yet**, so the frame durations are unjudged in motion. The first swing
  film (P1.2) is where they get tuned.
- The proposal heuristic is weak for sockets on full-body motion. It is a starting region; the sheet review is the
  real step.

### Disk
Every take was filmed one at a time, and the script deleted its frames. build/shots/sweep has 0 PNGs; build/shots is
45 MB. The draft sheets in build/tmp/clip_markers were deleted; build/tmp is under 5 MB.

## Phase 1 / P1.2: the champion-agnostic tier (ByAnySkill) for JAWS, PRESS and BRAND, and the outlined number (2026-10-04)

Not committed.

### What was done
- **NEW `src/IdleXIdle.Game/Presentation/RecipeTier.cs`**:
  - `RecipeTierKind { None, Own, Agnostic }` and `RecipeFamily { Reaction, Field, Mark }`.
  - `RecipeTier.Of(family, characterId, skillId)` is the pure helper. It applies the family's switches, so a family
    that is off reads None.
  - `RecipeTier.Resolve` (internal) is the one shared lookup: BySkill, then ByAnySkill. It is allocation-free.
  - `IGlyphPass` and `NumberOutline` (`For(tier, glow)`, `Draw<TPass>(ref pass, ...)`, `Hex`).
- **`ReactionRecipes` / `FieldRecipes` / `MarkRecipes`** each gain `ByAnySkill`: `snare_jaws` -> SeekerJaws,
  `hammer_press` -> SeekerPress, `sign_brand` -> SeekerBrand.
  - These are the SAME instances. There is no Source parameter, no colour table and no cue change. The cues stay
    `sfx_seeker_jaws_bite`, `sfx_seeker_press_tick` and the seven `sfx_seeker_brand_*`, one file each.
  - `For(characterId, skillId)` keeps its signature. It goes through `RecipeTier.Resolve(ActionRecipes.Enabled &&
    Enabled, ...)`, so RH_ACTION_RECIPES=0 and RH_*_RECIPES=0 still switch off both tiers.
  - New `TierOf(characterId, skillId)` on each family.
- **`ActionRecipes`** gains `ByAnySkill`, IN ITS ORDER: BySkill -> ByAnySkill -> ByForm -> legacy. It holds NO entry
  (`AgnosticCount` = 0). The Phase 4 hazard is written beside it.
- **THE SOURCE IN THE NUMBER (HuntScreen).**
  - `Callout.Outline` (Color?) and an optional `outline` on `SpawnDamage`. The `number` trace line gains
    `\toutline=RRGGBB` ONLY when an outline is set, so every existing line is byte-identical.
  - `AgnosticOutline(skillSlot)` returns `NumberOutline.For(tier, SourceGlow(slot Source))`. The tier comes from the
    reaction family for a Reaction, and from the field or the mark family for a Field.
  - It is applied in two places:
    - the reaction answer number, both the held `ReactionEcho` Number (new `Outline` field) and the immediate spawn
      when the reaction is already answered;
    - the field / mark tick total (`FlushAuraTotal`). The new `_auraTotalSlot` takes the tick's owner (`lastOwner.Slot`)
      when the ms changes.
  - The draw lives in the existing number pass (`DrawCallouts`). An outlined callout draws its shadow, then
    `NumberOutline.Draw` with a `CalloutGlyphs` struct (four one-pixel neighbours in the outline, then the glyphs in the
    ink). Nothing boxes and nothing allocates. Callouts without an outline take the old code path, unchanged.
- **`films_sweep.sh`**: three trace-only takes (20 x 30): `p12_anvil_jaws`, `p12_chorus_press`, `p12_tower_brand`.

### Decisions
- **Agnostic only.** The outline applies ONLY when the recipe resolved through the Agnostic tier. The Seeker's own
  JAWS / PRESS / BRAND numbers are closed pictures and stay bare (the refs carry no `outline=`, 0 of 5).
  - The outline colour is `SourceGlow`, the slot's Source light. It is the same outline that P1.3's FIRST BEAT and
    Phase 5's EMPOWERED-HIT will use.
  - The shadow is kept under an outlined number: shadow, then outline, then glyphs.
  - The alpha follows the callout's fade x Alpha, as the ink does.
- **The action tier is empty in Phase 1.** SPRAY-agnostic needs each champion's own `projectile` strip timing and
  missile, and both are Phase 4's. HARD HANDS gets no agnostic entry, ever.
- **PHASE 4 HAZARD.** `ByAnySkill["volley_spray"]` sits ABOVE ByForm. The Seeker's SPRAY resolves today through
  `ByForm[("seeker","projectile")]`, so Phase 4 must add `BySkill[("seeker","volley_spray")] = SeekerSpray` FIRST.
  Otherwise the Seeker's closed SPRAY resolves to the agnostic recipe. This is written in ActionRecipe.cs and pinned
  by a source-order test.
- **Two fields ticking on one ms** (for example PRESS + BRAND) still fold into one total, as before. The outline is
  taken from the first owner at that ms. In practice this never matters (see Open).
- `RecipeTier.Of` is enum-keyed, `Of(RecipeFamily, character, skill)`. The action family is left out of it, because
  its lookup also needs the clip and effect keys and has no agnostic entry yet.

### Recorded deviations (closed tests)
- The three recorded inversions were made, each kept as "resolves to the same instance", with nothing else on the line
  changed:
  - `jaws_reaction_test.cs:134` (magpie, snare_jaws);
  - `press_field_test.cs:51` (oathbound, hammer_press);
  - `brand_mark_test.cs:58` (oathbound, sign_brand).
- **A FOURTH pin had the same intent and was not in the list: `brand_mark_test.cs:81`.** It read
  `FieldRoles.Choose([BRAND], "oathbound") == (-1, -1, 0)`, "another hunter's BRAND keeps the old way". The agnostic
  tier necessarily makes it (-1, 0, -1), the same mark, so it was inverted the same way.
  - Its comment on line 78 ("another hunter's BRAND keeps the old way") was left as written, because no other line was
    to be touched. It is stale now; fix it with the next edit to that file.
- No other line of the closed tests changed. The SHA-256 pins are green.
- **P1.1 carry-over fixed.** `swing_timing_test.cs` failed `-warnaserror` with xUnit1026: theory
  `test_attack_markers_are_in_order_for_their_kind` never used `socket`. That error was the "1 error, no message"
  that P1.1 put down to a OneDrive lock: an incremental build hid it, and `--no-incremental` showed it. The fix is one
  added line, `Assert.False(string.IsNullOrEmpty(socket), ...)`.

### Verified (evidence `production/qa/evidence/vfx-sweep/phase1-p2/`, logs only, 0.8 MB)
- **p12_anvil_jaws**: anvil, `sig_anvil_hardface,volley_spray@Mind,snare_jaws@Shadow,field_mire@Nature`.
  - 3 `reaction-spawn seeker.jaws` and 3 `reaction-number`.
  - 0 `fx_anvil_trap`, and no trap clip (the clip-starts are only attack / projectile / strike).
  - The answer numbers read `outline=9B7BFF` (Shadow).
  - `belt=0,0` and `alloc=0` look the same as on the Seeker's ref.
- **p12_chorus_press**: chorus, `sig_chorus_grave_song,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`.
  - 2 `field-wave seeker.press`, 247 `field-draw`, 5 `field-cue`.
  - No `fx_press`. GRAVE SONG keeps the held aura, and its tick numbers are bare (it is not a closed reference).
- **p12_tower_brand**: tower, `sig_tower_slow_fall,sign_brand@Machine,volley_spray@Mind,hammer_blow@Body`.
  - 2 `mark-wave seeker.brand`, 556 `mark-draw`, 10 `mark-cue`.
- **Refs.** The five ref takes were re-filmed at this code and are **IDENTICAL x 10 under `--refs`** against both
  ae20a7fd baselines (`phase1-p2/regression.txt`).
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1106/1106** (1065 + 41 new). Core.Tests **1901/1901**. `bash tools/check_all.sh`: all gates green.
  `git status assets/audio` is clean.

### Tests (new)
- **`recipe_agnostic_test.cs`** (34 cases):
  - `agnostic_recipes_resolve_on_every_champion_test`, one per CharacterRoster champion. It checks the same instance in
    each family, no cross-family resolution, and the tier (Own on the Seeker, Agnostic elsewhere).
  - The Seeker's five pairs are unchanged: SPRAY via ByForm, HARD HANDS via BySkill, the three via BySkill (Own).
  - HARD HANDS and SPRAY resolve on no other champion.
  - The action tier is empty and in order (BySkill < ByAnySkill < ByForm in the source; the hazard note is present).
  - REPAY / WEEP / CALL resolve to null in every family on every champion. The signatures resolve to null in every
    family, and on their own champion no signature but HARD HANDS has an action recipe.
  - The switch: `Resolve(enabled:false)` gives null / None on both tiers. The RH_* flags are read once per process,
    so a source pin shows each family passes `ActionRecipes.Enabled && Enabled` to both For and TierOf.
  - `FieldRoles.Choose` on a chorus with GRAVE SONG + PRESS gives PRESS performed and GRAVE SONG held.
- **`number_outline_test.cs`** (7 cases):
  - only Agnostic takes an outline;
  - every non-Seeker champion's JAWS / PRESS / BRAND would be outlined, and the Seeker's never;
  - the draw is 4 neighbours, then the ink on top;
  - a 10 000-iteration loop of tier lookup + outline draw allocates 0 bytes;
  - screen pins (both reaction paths, the aura total, the trace field, the draw in the number pass).

### Open issues
- **PRESS and BRAND print no number of their own.** PRESS's tick is a defence Break and BRAND's an Amplify (amount 0),
  so their tick totals never exist. The agnostic outline is wired for them (FlushAuraTotal), but only JAWS shows it on
  screen. design.md says the Source shows "in the NUMBER ... nowhere else", so on PRESS / BRAND elsewhere the slot's
  Source is currently not visible at all. No number was invented, because that would be a new presented fact. For the
  director: either accept this, or give the agnostic PRESS / BRAND a Source cue in a later phase (BRAND's amplified
  hits on the branded creature would be the natural carrier).
- `r_holdfast_trace` / `r_after_holdfast` (Unbroken with `snare_jaws@Spirit`) now perform the agnostic JAWS instead of
  the legacy reaction. They are not reference takes, but their Phase 0 logs no longer match a re-film. This is
  expected and is not a regression.
- The stale comment at `brand_mark_test.cs:78` (see the deviations above).

### Disk
- Each take was filmed one at a time, and its frames were deleted by the script (0 PNGs). The p1p2 logs were copied to
  evidence and the build copies deleted.
- build/shots is 45 MB and build/tmp 2.5 MB.

## Phase 1 / P1.3: the swing performance, the six melee basics, FIRST BEAT, fxp_dust_soft (2026-10-04)

Not committed.

### What was done
- **NEW `Presentation/SwingRecipe.cs`**: `SwingKind {StepIn, Missile, Reach}`, `SwingImpactLook` (slash / slivers / flash /
  flat ring / sparks / dust, every extent a share of the CREATURE's height; `Extent`, `LifeMs`), `SwingRecipe` (Id, Kind,
  ClipKey `attack`, AnchorMarker, Commit/SettleMarker, HandSocket, StepInShare, ContactPoint, Impact, ContactCues,
  ContactVolume 0.36, ReleaseCues / ReleaseVolume 0.17, TravelMs, TargetFlash 0.30 / 110 / rise 0, Weight Ordinary) and
  `SwingRecipes.For(characterId)`, gated only by `ActionRecipes.Enabled`. A separate tier: no skill id is a key, and
  `ActionRecipes.For(.., "attack", ..)` is still null.
  - seeker 25 %, slash 0.30 + 2 slivers, `sfx_seeker_swing_hit -> sfx_blade_hit -> sfx_hit`;
  - anvil 20 %, compressed flash 0.28 + flat ring 0.12 -> 0.30, `-> sfx_fist_hit`;
  - metronome 25 %, compressed flash 0.24 + 2 sparks, `-> sfx_fist_hit`;
  - tower 20 %, flat ring 0.14 -> 0.35 + dust 0.32 (contact point low on the body: the slam comes down), `-> sfx_stone_hit`;
  - thornwall 20 %, a broad flash 0.35 for 33.3 ms (two display frames), `-> sfx_wood_hit`;
  - magpie 25 %, thin bright slash 0.24 (100 ms) + 1 sliver, `-> sfx_blade_hit`.
- **NEW `Presentation/SwingClock.cs`** (pure, allocation-free): FrameMs / FrameStart / FrameAt / StepIn.
- **NEW `Presentation/SwingPerformance.cs`**: one reused instance per HuntScreen (`_swing`), `Begin` per swing, `Retime`
  on the handoff, `Contact` (true once per swing ms), `Compose(playhead, light)` into a fixed 32-sprite array,
  impacts in a 4-slot ring aged by the playhead. `SwingSprite` / `SwingPart` / `KeyOf`.
- **NEW `Presentation/FirstBeat.cs`**: Core's struckOnce mirrored (`Cross`, `Reset`, `Applies`, `Outline` = white).
- **`ActionTargets.SwungAt(events, atMs, int[] into)`**: the Strikes at the ms with `Hit == Swing`, distinct slots, no allocation.
- **HuntScreen**:
  - `CommitSwing` right after the plain clip's commit (and its unchanged `clip-start` line), when `SwingRecipes.For` exists
    AND `char_<id>_attack` has its `.clip.json` with the anchor marker. The row gap = the target's visible left edge minus
    the champion's visible right edge, read once from the published bodies (`TryBody`, ADR-006). Trace `swing-start`.
  - `PlanHandoff` hands the envelope's new exit to the swing (`_swing.Retime`) for every fit.
  - `DrawChampion` takes the frame from the swing clock while `SwingOnFigure`.
  - `LayoutActors`: `step` is added to `_champDrawBox` BESIDE `push`; the `root` line still logs `push`. Trace `swing-step`
    on change.
  - Strike case: `swingHit = e.Hit == HitSource.Swing && _swing.IsBeat(e.AtMs)`. `SwingContact` runs BEFORE the thud line,
    plays the recipe chain through `PlayFirst` (SoundBank.Resolve) once per swing ms (not lead: it takes a duck, never makes
    one) and marks `_hitVoicedAtMs`; the flash is the recipe's 0.30 / 110 / 0; the puff is skipped
    (`if (!swingHit) PlayFx(...)`) while the blow still takes its turn in `_strikeCount`, so every other blow's puff falls
    where it did; the 40 px `_champLunge` is set only when `!swingHit`. Trace `swing-contact`. No callout.
  - Draw: material (dust) right after `_performance?.DrawMaterial(b)`; light inside the existing `BeginLight` block, after
    the field's light, before `EndLight` (the block also opens for `swingLit`). Trace `swing-draw sprites= alloc=`.
  - `ActionStillPlaying` includes `_swing.Drawing(...)`: a wave-ending swing's contact plays out (<= 250 ms) instead of
    freezing lit. `BeginWave` resets `_swing` and `_firstBeat`.
  - FIRST BEAT: `_firstBeat.Cross(e)` for EVERY Strike, first thing in the case; the outline goes on the number (both the
    immediate spawn and the held reaction echo) as `AgnosticOutline(..) ?? FirstBeatOutline(firstBeat)`.
- **NEW part `assets/art/VFX/parts/fxp_dust_soft.png`** (256 px, untinted grey-brown, straight alpha, peak 0.82) drawn by
  **NEW `tools/asset-pipeline/v2/fxp_dust_soft.py`** (numpy/PIL: fixed lobes, fixed value noise, an elliptic envelope that
  is zero 24 px inside the canvas; `--check` proves the file is the script's; no PixelLab).
- **`films_sweep.sh`**: six takes `p13_<id>` (beat:1, 150 x 2; each build `sig,volley_spray@Mind,hammer_press@Body`).
- **`action_regression.py`**: under `--refs`, `reaction-clamp belt=` is held by its y only (`CHAMP_X`; see Deviations).

### Decisions
- **THE PLAIN ENVELOPE OWNS TIME, THE AUTHORED FILE OWNS THE FRAMES** (the decision for "a step-in clip hands off like any
  plain clip"). The swing is committed exactly as the plain clip always was: start, speed, settle, PlanHandoff /
  FitRecovery / CutAt, the yield and the reservation are untouched. So every clip-start / handoff / yield / clip-end line
  cannot move, and neither can the starts of SPRAY and HARD HANDS behind a swing (ref_fast's SPRAY start depends on the
  outgoing swing's Floor exit, 5248 > ideal 5200). Only WHICH authored frame is drawn changes: frames before the anchor
  fill [start, beat - travel], the anchor and the rest fill [beat - travel, planned exit].
  - design.md's "weighted by frameMs" is applied through the clip's own ELASTIC flags. Spare time before the anchor
    stretches the elastic wind-up by frameMs (or holds the opening pose when nothing there is elastic). Spare time after
    the anchor is held on the settle frame, as the plain clip's settle is. Short time is taken from the elastic frames
    first (by frameMs), then from the rigid ones (by frameMs).
  - The anchor's start is the beat EXACTLY (no float sum in between).
- **The step-in**: 0 to the commit frame; u^2 (accelerating into the hit) up to `share x gap` at the anchor time;
  smoothstep back to 0 by the settle frame's start or by `exit - 2 display frames`, whichever is first; 0 at and after the
  exit, for Natural, Floor, Compressed and Cut fits (a Cut a hair after the contact is home on the first instant after the
  beat). It reads the playhead only.
- **The legacy 40 px push is the QA twin.** It is set only for a swing that is not performed: RH_ACTION_RECIPES=0, no
  timing file, a champion with no swing recipe (the four missile / reach champions until their packages), or a beat the
  figure did not swing (a yielded beat keeps the old behaviour, so nothing behind a yield moves).
- **The contact cue resolves to `sfx_hit` at 0.36 today**: none of `sfx_<id>_swing_hit` or the archetype cues exist yet
  (a later package builds them). It is the swing's OWN cue through its chain (traced `swing-contact cue=sfx_hit`), not the
  generic thud: the generic `sfx_hit` 0.38 line is gone on every swing ms.
- **A basic attack has no Source.** Its contact light is the recipe's material light (steel, knuckle, stone, wood), pale,
  through `VfxBlend.Light`. There is no weapon-edge glint (the strips carry no edge mask); light is on the contact only.
- **FIRST BEAT's predicate is Core's struckOnce exactly.** SoloBattle.cs:1469 / :1765: a creature joins the set only on a
  `HitSource.Primary` landing, and the multiplier is read in Amp's skill-tree half (after `if (skillDef is null) return
  m;`). So the SWING IS NEVER DOUBLED and never spends it, and carries / bleeds / reflects / DEADWEIGHT neither take nor
  spend it. The white outline goes on the first PRIMARY Strike per creature slot per wave: a skill hit, not a swing.
  design.md 5.20 lists FIRST BEAT beside the basic attack; Core says the skill hits carry it, and the screen follows Core.
  - It applies when `Character.Shape.FirstHitMultiplier > 1` (the Metronome's 2; the Tower's 0.85 MOMENTUM is not outlined).
  - An agnostic Source outline (JAWS / PRESS / BRAND elsewhere) wins over the white one on the same number.
  - Edge: a Primary landing that bypasses Amp (SoloBattle :2892) joins Core's set undoubled; the screen would outline it.
- **Dust**: design.md section 6 lists `fxp_dust_soft` without a phase; it is made here because the Tower's contact needs it.
- The first swing of a take seeked to the wave's first frame may measure gap 0 (the bodies are not published yet). It is
  then performed with no step (ref_fast 3600: `gap=0 peak=0`).

### Deviations
- **`reaction-clamp belt=` x is no longer compared under `--refs`** (`action_regression.py`, `CHAMP_X`; y still held).
  - `belt` is the CHAMPION's drawn belt point (`TryChampionBody`), and only the RH_SHOT_SOCKETS overlay draws it; JAWS'
    teeth, clamp, ring and number never read it.
  - Its x carries the champion's draw push, which P1.3 changes by design: in ref_fast at 7333 the baseline's belt=687 is
    647 + the legacy 40 px post-hit lunge (the swing landed at 7300); now it is 756 = 647 + the step-in on its way home.
  - That was the ONLY differing reference line, against both baselines. The precedent is P0.2's HOST_X for field-draw body
    x. The JAWS picture is unchanged.
- **fxp_dust_soft fails `check_fx_edges.py` LIVE** (6 bright cells; LIVE needs 150). SOFT 25.6 % and EDGE 0 pass, as
  required. LIVE measures LIGHT at combat size, and the dust is an alpha-blended material (the existing fxp_flash_soft part
  fails LIVE too). The gate is not in check_all.
- `reaction-draw draws=` (an upper bound, dropped under --refs) now counts the swing's sprites too.

### Verified (evidence `production/qa/evidence/vfx-sweep/phase1-p3/`, 2.1 MB: 12 JPG stills, the take logs, regression.txt, melee_takes.txt, check_swing.py)
- **The five ref takes, re-filmed at this code, are IDENTICAL x 10 under `--refs`** against both ae20a7fd baselines.
  ref_brand's first film had one transient `mark-draw alloc>0` at 5367 (ticks=1819: a CPU hiccup inside the curse's own
  draw, not a swing frame). The re-film is IDENTICAL x 2; both runs are in regression.txt.
- Against P1.2's ref_seeker the only changed kinds are champ-frame, flash, sound (the generic 0.38 thud becomes the
  swing's 0.36 cue), vfx-spawn (the fx_weakhit puffs are gone), reaction-draw (draws=), one draw-sampled enemy-root, and
  the new swing-* lines. Every SPRAY / HARD HANDS clip-start, release, contact, root and handoff line is unchanged.
- **Six melee takes** (`melee_takes.txt`):
  - every swing-contact is on a swing Strike's exact ms (0 off), one per ms;
  - `swing-step` is 0 before every attack clip-end (0 non-zero);
  - 0 generic `sfx_hit` 0.38 and 0 `fx_weakhit` on a swing ms;
  - `swing-draw alloc=0` on every frame (36 / 39 / 18 / 55 / 7 / 32 frames);
  - the contact frame is 4 / 5 / 2 / 5 / 3 / 3.
- **Metronome**: `outline=FFFFFF` on exactly the first Primary hit of each slot: slots 0, 1 and 2 at 700 (CLOCKWORK's free
  opening cast), slot 3 at 2200 (584 against 227-301 on the slots already struck: Core's doubling). The seek's re-crossed
  batch at 713 and every later hit are bare.
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1135/1135** (1106 + 29). Core.Tests **1901/1901**. `bash tools/check_all.sh`: all gates green.
  `git status assets/audio` is clean.
- The last code edit after the films reordered one `||` condition to keep heal_receive_test's pin. It is
  boolean-identical, so the takes were not re-filmed.

### Tests (new)
- **`swing_melee_test.cs`** (28 cases):
  - the T1 table per champion (volume 0.34-0.38, flash 0.30 / 110 / 0, extent <= 0.35, life <= 250, Ordinary, no release
    cue, the cue chain) and design.md's contact pictures;
  - SwingRecipes never resolves a skill, and the action tier never resolves `attack`;
  - the contact frame on the beat at 2500 / 1500 / 792 ms beats and on a late start; frames forward-only and filling the
    envelope exactly; the elastic distribution;
  - the step-in peak per champion, 0 at the commit and home at the exit for Natural / Floor / Compressed / Cut; missiles
    and reaches never step;
  - one cue per swing ms and a picture per struck creature; SwungAt; 200 swings x 30 frames allocate 0 bytes;
  - text pins: no generic thud / puff / push, the T1 flash, the cue never lead, the commit inside the plain envelope, the
    step beside the root, the draw order (material after the figures, light inside BeginLight / EndLight).
- **`number_outline_test.cs`**: `test_first_beat_outlines_the_first_primary_hit_per_creature_white` (Core's rule, reset
  per wave, 0 allocation, screen pins). Its two P1.2 pins were updated for the `?? FirstBeatOutline(firstBeat)` tail.

### Open issues
- The cues are `sfx_hit` until the archetype and per-champion swing cues are built (a later package).
- **The Tower's slam lands at his own feet** in his strip. At a 20 % step-in the hammer is still far from the creature, so
  the contact picture AT the creature reads detached. design.md's risk note (the 20 % cap) holds it there; the director
  should judge on film (a larger step for the Tower, or a key-pose edit).
- The contact pictures are quiet by design (T1); the Seeker's slash is thin and short at combat size. To judge on film.
- The published champion ENVELOPE still allows only the legacy 40 px lunge, while a step-in peaks at ~90 px (25 % of a
  ~360 px gap), so the inspector keep-out does not cover the step's peak. Left as is: changing the envelope would move every
  champion-side effect that measures it (P1.1's inertness rule).
- A rewind inside a wave does not un-cross FIRST BEAT's creatures (the set resets per wave only).
- The stale comment at `brand_mark_test.cs:78` (P1.2) is still open.

### Disk
- Every take was filmed one at a time. The six melee takes kept their frames only long enough to cut two stills each,
  then `find build/shots/sweep -name '*.png' -delete` (0 PNGs left). The logs were copied to evidence and the build copies
  deleted. build/shots is 45 MB and build/tmp 3 MB.

## Phase 1 / P1.4: the missile and reach basics (quiver, chorus, unbroken, oathbound) and their four props (2026-10-04)

Not committed.

### What was done
- **Four props in `assets/art/Props`**, each a MATERIAL (untinted) plus an `_edge` emissive mask, drawn in the hand-drawn
  smooth style by new v2 scripts. Each script has `--check`, which proves the file on disk is that script's output.
  - The shared kit is **NEW `tools/asset-pipeline/v2/props_smooth.py`**:
    - polygons drawn at 8x and box-filtered (premultiplied), so edges are smooth;
    - a soft near-black outline;
    - a faint outline-coloured FRINGE under the material, so the silhouette ends in a ramp of partial alpha;
    - a square canvas with a clear margin, because check_fx_edges.py only measures square frames;
    - check_fx_edges' EDGE and SOFT rules run before anything is written;
    - the content box is printed, and is pinned in C# as `ProjectileLook.HeadContent`.
  - `quiver_arrow.py` -> `prop_quiver_arrow`: 80x80 canvas, content box 4,32,73,16. An ash shaft, a leaf head, grey vanes,
    bindings. The mask covers the head's bevel and tip, plus a faint sheen along the shaft.
  - `unbroken_chip.py` -> `prop_unbroken_chip`: 56x56 canvas, content box 7,15,41,27. A split warm-grey wedge with two
    cracks. The mask is the fracture ridge.
    - The first film drew it at 40 px and it read as a pebble. It was grown 1.4x and re-filmed.
  - `oathbound_hook.py` -> `prop_oathbound_hook`: 48x48 canvas, content box 3,15,37,23. A forged hook in the chain body's
    iron: an eye ring, a shank, a swept tapering bend, and a barb. The mask is the outer rim and the point.
  - `chorus_charm.py` -> `prop_chorus_charm`: 48x48 canvas, content box 3,12,42,24. A knobbed bone with a cord wrap and the
    cord's small loop. The mask is a faint top edge.
    - **ONE PixelLab generation and no retry.** It was `create_image_pixen` at 64 px, seed 1404, job
      `befc30a0-7b75-4953-9ef6-7d03dcfe2bc2`, Creator asset `6f8698ed-...`. Its prompt and the proportions measured from it
      are recorded in the script's source block.
    - The generation itself is not shipped or committed; it was deleted from build/tmp. The script redraws its silhouette
      at the game's size and in the Chorus's palette: a dull cord, not the generation's saturated red.
  - **check_fx_edges.py: SOFT and EDGE pass on all eight files.** Edge is 0 on every file. Soft is 5.5 / 2.25 / 9.0 / 6.25 /
    8.7 / 6.1 / 7.6 / 3.5 %. Every file fails LIVE, which is the same precedent as fxp_dust_soft in P1.3: LIVE measures
    impact light at combat size, and these are objects and masks. LIVE is not in check_all. The output is in
    `phase1-p4/fx_edges.txt`.
- **`Vfx/ProjectileLook.cs`**:
  - New `HeadContent` (`Rectangle?`). It holds the object's box on a square canvas; null means the texture's pad decides,
    which is SPRAY's path.
  - New looks `ProjectileLooks.QuiverArrow`, `ChorusCharm` and `UnbrokenChip`. Each has the prop as its head (material plus
    edge), the runtime trail, a glint and a directional contact, all as data.
    - QuiverArrow: wobble 1.5 degrees, a thin pale wake, and a forward ImpactSlash 0.55 (the "short forward slash", carried
      along its real line).
    - ChorusCharm: a tumble of 14 degrees, almost no wake, EdgeBrightness 0.2, and a small flash.
    - UnbrokenChip: a turn of 10 degrees, a faint wake, and a small flash.
    - All three have `ImpactShards = 0`. The debris (slivers, chips) is the swing's contact picture at the creature.
  - **The looks are NOT strip-keyed.** `ProjectileLooks.All` still holds only `fx_seeker_projectile_strip8_512`.
- **`Vfx/ProjectileVisual.cs`**:
  - A reusable constructor `(look, seed, sparkCapacity, shardCapacity)`.
  - `Reset(look, seed)` re-initialises the instance without allocating, and refuses a look that does not fit its capacity.
  - `PlaceDriven(..., Rectangle content, ...)` takes nullable textures. A flight with missing art is still placed, driven and
    landed; it just draws nothing (this is what makes it testable).
  - Spark and shard counts come from the look (`SparkCount` / `ShardCount`), which equal the array lengths for SPRAY's
    own instances.
  - SPRAY's constructor and its pad `PlaceDriven` are byte-for-byte unchanged, and a test pins this.
- **`Presentation/SwingRecipe.cs`**:
  - `SwingImpactLook` gains `Chips` / `ChipKey` / `ChipSize` / `ChipTravel`, which are counted in Extent and LifeMs.
  - `SwingRecipe` gains `Missile`, `FlightBulge`, `Departure`, `FlightLight` and `Reach` (a `ReachLook`).
  - Four new entries:
    - **Quiver**: Missile, release 4, BowHand, TravelMs 200. The contact is the arrow's forward slash. Cues
      `sfx_quiver_swing_hit -> sfx_blade_hit -> sfx_hit`; release `sfx_quiver_loose -> sfx_throw_release` at 0.18.
    - **Chorus**: Missile, release 5, ThrowHand, TravelMs 200, ONE charm, and no prop drawn in the hand. The contact is the
      charm's small flash plus 3 pale bone slivers. Cue `-> sfx_wood_hit`; release `sfx_chorus_toss -> sfx_throw_release`
      at 0.16.
    - **Unbroken**: Missile, release 5, ThrowHand, TravelMs 220, a LOB (`FlightBulge -0.10` of his height, Departure 0).
      The contact is the chip's small flash plus 2 untinted chips of `prop_unbroken_chip`, which are material and turn as
      they fly. Cue `-> sfx_stone_hit`; release `sfx_unbroken_toss -> sfx_throw_release` at 0.16.
    - **Oathbound**: Reach, contact 4, LashHand. The `ReachLook` is the chain body plus the hook, with the hook's pivot at
      its eye (10.5, 22). The contact is a flash of 0.30 plus 1 spark. Cue `-> sfx_fist_hit`.
- **`Presentation/SwingPerformance.cs`**: the MISSILE mode and the REACH aim.
  - **NEW `ISwingStage`** (`TrySwingFrame` / `TrySwingTarget` / `SwingTexture` / `SwingCasterHeight`) and
    **`SwingStep`** (Released, ReleaseAt, Landed).
  - The performance holds **ONE pre-built `ProjectileVisual` per possible target** (8 in all), built in its constructor and
    re-initialised per flight with `Reset`.
  - `Update(playhead, dt, stage)` runs before the frame's events are crossed:
    - at `ReleaseMs` it places one flight per swung creature, from the hand socket on the release frame, with the tip
      meeting the creature's contact point;
    - it drives each flight along `ProjectileMotion.ThrowPosition` (the bulge makes the lob);
    - it lands every flight on the first update at or after the beat;
    - it reports Released once and Landed once.
  - **Release and clamp.** `ReleaseMs = AnchorAtMs = max(start, beat - TravelMs)`. When the plain start is later than that
    (a late or fast start), the release CLAMPS to the start and `FlightMs` shortens, so the contact is always on the beat.
    The clamp is exposed as `ReleaseClamped` and traced as `clamp=1`.
  - The body never moves (`PeakPx` is 0 for non-StepIn kinds). A swing whose clip left the figure before its release
    throws nothing.
  - `Drawing` includes the flights in the air or fading, and the strand.
  - Chips are composed in the material pass (`SwingPart.Chip`, with `SwingSprite.Key` set to the chip prop).
  - **Allocation fix:** `TryHand` maps the socket with `(Effects & FlipHorizontally) != 0`, not `Enum.HasFlag`. Un-tiered
    code boxed HasFlag at 24 bytes a call, and the alloc tests caught it.
- **NEW `Presentation/ReachStrand.cs`**, shared (Phase 3's DRINK REACH mode is just another `ReachLook`):
  - `ReachLook` holds: StrandKey (default `prop_seeker_chain_body`), EndKey, EndEdgeKey, EndPivot, EndEdgeBrightness,
    Thickness (0.022 of the actor's height), Sag 0.12, RecoilMs 120 and TautHoldMs (one display frame).
  - The strand leaves the hand at the first lash frame's start (the first frame that authors the socket; frame 2 on the
    Oathbound). Its reach goes as u squared (it flicks out) while its slack falls to 0.
  - It is **TAUT exactly on the beat**: Reach is 1 and Slack is 0, held one display frame. It then recoils as (1 - v)
    squared over **120 ms** while the slack returns.
  - `Compose` places 12 segments along a sagging line from the hand to the tip; the end object rides the tip, turned along
    the last segment. Fixed arrays, no allocation.
  - The screen aims it every update, from the LashHand socket on the frame on the figure (or the nearest frame that has the
    socket) to the first swung creature's contact point.
- **HuntScreen**:
  - It implements `ISwingStage`. Its strip key `_swingStripKey` is resolved once at CommitSwing, so there is no per-frame
    string (Character.StripKeys allocates).
  - **`UpdateSwing(dt)`** runs right after `UpdatePerformance(dt)` on both the fight clock and the wave-clear break. It plays
    ONE release cue per swing through the chain (`PlayFirst`, not lead, no duck, no callout).
  - New trace lines:
    - `swing-release` (at, release, beat, flight, clamp, flights, x, y, cue, vol);
    - `swing-land`;
    - `swing-strand`, once per swing on the frame that presents the beat (taut, reach, slack, out, home, hand, target).
  - **`DrawSwingObjects`** runs inside `DrawSwing`, inside the existing alloc measurement:
    - material: each flight's prop, the chain's segments (`prop_seeker_chain_body`, untinted `Color.White`, stretched along
      each segment), and the hook;
    - light: each flight's edge, trail, glint and contact, and the hook's edge (`VfxBlend.Light`, 0.3).
  - The contact cue stays `SwingContact`'s, one per swing ms. No generic thud, no puff and no 40 px push on these swings
    (the P1.3 rules, unchanged).
- **`films_sweep.sh`**: four takes `p14_quiver` / `p14_chorus` / `p14_unbroken` / `p14_oathbound` (beat:1, 150 x 2). The
  disk guard is untouched.
- `swing_melee_test.cs`: its "the four are later packages" assertion now says all ten perform and none of the four steps in.

### Decisions
- **One owner per contact piece.**
  - Pieces drawn along the flight's REAL line belong to the look: the arrow's forward slash and the missiles' small flash.
  - Debris seated at the creature belongs to the swing recipe, sized in shares of the creature's height and so checkable
    against T1's 0.35: the charm's 3 slivers and the chip's 2 chips.
  - The looks throw no shards, so nothing is drawn twice.
- **A missile's light is its material's own, pale** (`FlightLight`: steel, bone, flint). A basic attack has no Source.
  `EdgeBrightness` is 0.2-0.35, so the charm is bone in the air, never glowing baked art.
- **No prop is drawn in the hand for any of the three.** The Quiver's strip nocks its own arrow, the Chorus's bundle is the
  charms she holds (design.md), and the Unbroken's hand is drawn closed. The flight appears AT the hand on the release frame.
- **The taut hold (one display frame).** Without it, the frame that presents the beat (the playhead crosses it 0-16 ms
  late) would already show the recoil started, so taut-on-the-beat would never be on screen. The recoil is the full 120 ms
  after that hold. Traced: taut=1 on 3 of 3 beats.
- **The strand is the chain body as it exists:** a smooth iron cross-section, stretched, not drawn as links. design.md
  5.27 names exactly this prop as the shared iron material. It reads as an iron tether in the stills.
- **Release cues** resolve to nothing today (`cue=-`): neither `sfx_<id>_loose/_toss` nor `sfx_throw_release` exists yet.
  The release is silent until the archetype package builds `sfx_throw_release`; nothing was invented. The contact cue
  resolves to `sfx_hit` at 0.36, as for the melee basics.
- The PixelLab budget: 1 generation spent (the charm), 0 retries.

### Verified (evidence `production/qa/evidence/vfx-sweep/phase1-p4/`, 1.4 MB: the take logs, the five ref logs, regression.txt, check_missile.py, stills.py, fx_edges.txt, props_sheet.jpg, five JPG still sheets)
- **Four trace takes** (beat:1, 150 x 2, one at a time; `check_missile.py` over each log):
  - **quiver**: 3 releases, all at beat - 200 with 0 clamps, released on the first frame at the release.
  - **chorus**: 3 releases, all at beat - 200.
  - **unbroken**: 3 releases, all at beat - **220**.
  - On all three: 1 flight per release, every land on the frame that presents the beat, every swing-contact on a swing
    Strike's exact ms (0 off, 0 duplicates).
  - **oathbound**: 3 swing-strand lines, **taut=1 on all 3** (at 0 and 13 ms after the beat: the frame that presents it),
    out from frame 2's start, home = beat + one frame + 120. Contact frame 4.
  - **No `fx_*_projectile` / `fx_*_strike` spawn on any swing.** The one spawn in each take is SPRAY@Mind's own Skill (the
    legacy cast until Phase 4's SPRAY-agnostic).
  - **`swing-draw alloc=0` on every frame** (82 / 91 / 96 / 72 frames).
- **Looked at by eye** (`p14_*_flight.jpg`, `p14_oathbound_lash.jpg`, `p14_unbroken_chip_fullres_2113.jpg`,
  `props_sheet.jpg`):
  - the arrow reads as an arrow in flight;
  - the charm as a small bone (not glowing);
  - the chip as a grey stone (after the 1.4x grow);
  - the chain as an iron tether with the hook at the creature on the beat, then coming home;
  - no rectangle edge anywhere.
- **The five ref takes, re-filmed at this code, are IDENTICAL x 10 under `--refs`** against both ae20a7fd baselines, all on
  the first film (`phase1-p4/regression.txt`).
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1160/1160** (1135 + 25). Core.Tests **1901/1901**.
  - One full run had a transient failure of `HuntGenericHitsTest...allocates_nothing...`. It passes alone and passed on the
    next full run (the same tiered-JIT effect as the HasFlag finding; not touched).
- `bash tools/check_all.sh`: all gates green. `check_asset_consumers --list` reaches all eight new files as literals, plus
  `prop_seeker_chain_body` (which had no live consumer before). `git status assets/audio` is clean.
- `python tools/asset-pipeline/v2/<prop>.py --check` passes on all four.

### Tests (new)
- **`swing_missile_test.cs`** (18 cases):
  - the T1 table per missile: kind, anchor, socket, travel, volumes, flash, extent, the cue chains, the look's prop and
    edge keys, and quiet edge light;
  - design.md's contacts (the forward slash; 3 slivers; 2 chips and a lob; the looks are not strip-keyed);
  - the prop files, their `_edge` masks and scripts, and the recorded job id;
  - **release exactly TravelMs before the beat when there is room, the clamp otherwise** (three TEMPOs plus a late start),
    with the release frame shown from the release on, the envelope filled exactly, and no step;
  - **one flight per swung target, ONE release and ONE land**, landed on the frame presenting the beat, each tip at its
    creature, from the hand socket;
  - nothing before the release, in the air until the beat, one contact cue per ms, and reset clears the flights;
  - a clip off the figure throws nothing;
  - the lob bows up and the arrow flies level;
  - **200 swings x 45 frames with flights allocate 0 bytes**;
  - ProjectileVisual.Reset x 1000 allocates 0, refuses an oversized look, and SPRAY's path is pinned;
  - screen pins: Update order on both clocks, the release cue not lead, the traces, the cached strip key, and the draw;
  - **the text pin: no `PlayFx(` / `_projectile` / `_strike` / `FxFor(` / `_vfx.` in any swing method or swing class**, and
    the puff is still kept off a performed swing.
- **`swing_reach_test.cs`** (7 cases):
  - the recipe (T1, chain plus hook, recoil 120);
  - **taut exactly on the beat**, not 1 ms before, flicking out from the first lash frame, at every TEMPO and on a late start;
  - **the one-frame hold, then the 120 ms recoil** (Reach 0.25 at +60), home and no longer drawing;
  - **the body offset always 0**;
  - from the LashHand socket to the contact point, straight when taut, sagging before, the hook at the actor's scale;
  - **0 bytes over 200 lashes x 45 frames**;
  - screen pins: the untinted chain, the hook's edge, the trace, and no Oathbound name inside the shared class.

### Open issues
- **The Quiver's strip keeps its own nocked arrow on frame 5** (recovery), so for a frame or two the bow still shows an arrow
  after the flying one has left. This is a strip pose matter (a key-pose edit of frames 5-6, or a strip_bake-style clean
  keyed on the arrow); it is not done here.
- **Release cues are silent** until `sfx_throw_release` (archetype) and the per-champion loose / toss cues exist.
- The chain is a smooth rod at combat size, not links. If the director wants links, the existing `prop_seeker_chain_link`
  could be stamped along the strand (one more sprite per ~link) in a later polish pass.
- `SwingPart.Chip`'s `KeyOf` default is `prop_unbroken_chip`. Any other chip-throwing recipe must set `ChipKey`, which it
  does through `SwingSprite.Key`.
- Carried: the stale comment at `brand_mark_test.cs:78` (P1.2); the Tower's detached slam and the step-in envelope note
  (P1.3).

### Disk
- Every take was filmed one at a time. Frames were kept only to cut the stills, then deleted with
  `find build/shots/sweep -name '*.png' -delete` (0 PNGs left). The logs were copied to evidence and the build copies
  deleted.
- build/shots is 45 MB and build/tmp 3 MB. The PixelLab download was deleted.

## Phase 1 / P1.5: the eight ARCHETYPE cues, the throttle rows, the SHIPPED pins and the hierarchy test (2026-10-04)

Not committed.

### What was done
- **NEW `tools/asset-pipeline/make_archetypes.py`** (the make_press_tick.py pattern): `--extract <dir>` once, then a plain
  run builds all eight into `assets/audio/combat/`, writes `foley/archetypes/SOURCES.md` + `archetype_manifest.json`, and
  measures. ONE build per cue, no candidates, no selector, no history folder. It imports the DSP from make_jaws_bite (SR,
  band, norm, place, read_wav, sat_os, true_peak, weight, write_wav) and K-weighting / max1 from make_press_tick, read
  only; neither file nor any approved cue was edited. Byte-reproducible (fixed seeds; the SHA-256 of all eight matched on
  a second run, and again after the `--evidence` run).
  - `sfx_fist_hit`: a punching bag + a body blow, a little knuckle snap (top bus 0.22), a small synthesised sub (the only
    synthesis, as in the stone). 117 ms.
  - `sfx_blade_hit`: a knife into chicken + a leather strap on skin, a faint steel edge from a knife stab's top band. 75 ms.
  - `sfx_stone_hit`: a heavy stone impact + a brick on soil, a soil click, a pebble tick +28 ms, a small sub. 90 ms.
  - `sfx_wood_hit`: a crate thump + a barrel knock, a wooden shield's face, a metal bucket's rim +12 ms at 0.35. 157 ms.
  - `sfx_throw_release`: a thrown swish swelling into a clothes-whip + a shirt snap at 60 ms. 194 ms.
  - `sfx_air_release`: a puff of smoke + a puff of air + an air burst's hiss. 191 ms, no tone (below).
  - `sfx_field_tick`: two pebbles settling 45 ms apart over a grain of poured gravel and a gravel shift, nothing above
    7 kHz, no crack. 178 ms.
  - `sfx_cloth_commit`: a clothing ruffle 12 ms ahead, a boot plant + a stomp on T, a second ruffle after. 142 ms.
- **Foley**: 24 CC0 1.0 Freesound recordings, 27 excerpts (476 KB) in `tools/asset-pipeline/foley/archetypes/`. Each
  licence was checked on the sound's own page (the page links the CC0 1.0 deed) before the download; every one of the 35
  candidates fetched was CC0. Downloaded as the hq preview into `build/tmp/arch`, converted to mono 16-bit 44.1 kHz, and
  deleted after the extract (0 files left; build/tmp is 2.5 MB).
- **NEW `src/IdleXIdle.Game/Presentation/ArchetypeCues.cs`**: the eight keys as constants, `ArchetypeMoment`
  (Hit / Release / Tick / Commit), and `All`: each cue's material, its consumers' volume band, its ceiling (a closed
  reference's file and volume) and its consumers in words. `HitGapMs` 60, `TickGapMs` 90, `ThrottleRows()`, `Find`. This is
  the table the consumer gate and the hierarchy test read; it names the three cues with no Phase-1 performer.
- **`SoundBank.cs`**: the hand-written gaps are now `HandTunedGaps`, and `MinGapMs` = those + `ArchetypeCues.ThrottleRows()`
  (declared after them: static initialisers run in text order). New rows: the four hit archetypes and the ten
  `sfx_<id>_swing_hit` identity keys at 60, `sfx_field_tick` at 90. Releases and the commit keep the 90 default.
  `sound_throttle_table_test` failed once ("stale ... regenerated now") and was green on the second run; the JSON now has
  24 gap rows.
- **`films_sweep.sh`**: five take lines `p15_seeker / _quiver / _anvil / _tower / _thornwall` (beat:1, 150 x 2).
- **NEW tests**: `archetype_cue_test.cs` (15) and `sweep_audio_hierarchy_test.cs` (6); see Tests.

### Decisions (design.md silent)
- **The mastering target is the ceiling's own measure, not a guess.** Each file is scaled so that its K-weighted loudest
  50 ms, as played at its mastered volume x 0.8, lands on a target (true peak capped at 0.37, which can only make it
  quieter):
  - hits at 0.36: -31.3 dB, which is 3.8 under SPRAY's contact and 7.8 under HARD HANDS'. At the T1 band's top, 0.38, a
    hit is still 3.3 under SPRAY.
  - throw release at 0.17: -37.0 (6.9 under SPRAY's release). A basic's release plays on every swing.
  - air release at PULSE's 0.24: -35.0 (4.9 under SPRAY's release).
  - field tick at the tick ceiling 0.34: -33.0 (1.8 under PRESS's tick at 0.28). This reads the task's "<= PRESS (0.28 x
    file) + 0.06" in its stricter form: at 0.34 the file sits under PRESS at PRESS's own 0.28. At its consumers' 0.14-0.18
    it is about 7 dB under.
  - cloth commit at BLOW's 0.30: -35.0 (1.7 under HARD HANDS' commit at 0.34).
- **The ceilings for the three non-hit families** come from the design's levels: releases under SPRAY's release (0.40),
  the commit under HARD HANDS' commit (0.34), the tick under PRESS. The design has no explicit release or commit ceiling.
- **"No tone" is measured.** `tonality()` is the strongest line between 80 Hz and 4 kHz over the median of its +-1/2-octave
  neighbourhood, on a 1/48-octave grid. The air release reads 11.2 dB. PRESS's approved "air, not a tone" reads ~14. The
  air release is required to read below PRESS's, and does.
- **The basic attacks' own identity keys get their 60 ms row now** (`sfx_<id>_swing_hit`). They are hit families, and their
  rows are in place before the files exist. `sfx_seeker_swing_hit` is the only `sfx_seeker_*` key in the table: it is the
  Seeker's basic attack, not a closed reference.
- **The levels JSON lives beside the script** (`tools/asset-pipeline/archetype_levels.json`, with the SHA-256 of the bytes it
  measured). The table is in the evidence (`archetype_levels.md`, from `--evidence <dir>`).
- **The hierarchy test's effective level** is the raw loudest 50 ms RMS (every-sample sliding window) of the resolved file x
  the volume, read from the repo's files. It does not depend on the bank's audio device. The K-weighted figures are the
  script's; the test pins the raw rule the design states.

### Verified (evidence `production/qa/evidence/vfx-sweep/phase1-p5/`, 2.9 MB)
- **The levels table** (`archetype_levels.md`): all eight are under their ceilings (see Decisions for the figures).
- **In the fight's own mix** (`mix_in_the_fight.md`, by the PRESS method: each voice's own share = film_audio's render
  minus the render with that voice silenced):
  - every archetype hit's own K50 is -31.7 to -31.8;
  - SPRAY's contact in the Seeker's fight is -28.0. The loudest archetype hit is **3.6 dB under** the SPRAY contact heard
    in the Seeker's fight;
  - the throw release is -37.1.
- **Swing trace takes** through film_audio.py with sound_throttle.json (`swing_cues.txt`, 5 mp4s re-encoded at 720p /
  CRF 28, 0.4-0.5 MB each):
  - seeker 3 x `sfx_blade_hit` 0.36;
  - quiver 3 x `sfx_blade_hit` 0.36 + 3 x `sfx_throw_release` 0.18;
  - anvil 3 x `sfx_fist_hit` 0.36;
  - tower 3 x `sfx_stone_hit` 0.36;
  - thornwall 3 x `sfx_wood_hit` 0.36.
  - Every `swing-contact` / `swing-release` line names the archetype, and each sound line follows it.
- **The five ref takes, re-filmed at this code, are IDENTICAL x 10 under `--refs`** against both ae20a7fd baselines
  (`regression.txt`). The sfx_seeker_* sound lines are part of that comparison.
- `git diff ae20a7fd -- assets/audio` is empty for tracked files. `git status assets/audio` shows exactly the 8 new files.
  The jaws / press / brand SHA-256 pins are green.
- `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1181/1181** (1160 + 21). Core.Tests **1901/1901**.
- `bash tools/check_all.sh`: all gates green. `check_asset_consumers` reaches the eight new files as literals in
  `ArchetypeCues.cs` and the recipes.

### Tests (new)
- **`archetype_cue_test.cs`** (15):
  - **`test_<key>_is_shipped` x 8**: each file's SHA-256. The message says a change is a deliberate commit, never a
    regeneration.
  - the table names the eight in the design's order;
  - no key starts with `sfx_seeker_`, and every ceiling is a closed reference's existing file;
  - **no candidate or selector**: one file per key under assets/audio, no `audio_history/archetypes`, no
    APPROVED / BUILDS / HISTORY / ARCHIVE / `candidate(` in the builder, no `RH_ARCHETYPE_` and no `<key>_` variant in src;
  - the manifest and the levels JSON name these bytes, all pass, and each was mastered inside its consumers' band;
  - every excerpt has a SOURCES.md row with CC0 1.0, its Freesound page and a cue that reads it. No raw download sits
    beside them;
  - **SPRAY's contact / release / tick and HARD HANDS' hit / commit resolve to their own files first**, and the archetype
    is second in each chain;
  - the throttle: hits 60, the tick 90, releases and commit on the default, every swing identity key 60, the hand-tuned
    rows unchanged.
- **`sweep_audio_hierarchy_test.cs`** (6):
  - the references' volumes the rules name (0.50 / 0.55 / 0.40 / 0.34 / PRESS 0.28);
  - every SwingRecipe: contact 0.34-0.38, Ordinary; a missile's release 0.16-0.18; no release on a blow;
  - every swing chain resolves to an existing file, never `sfx_hit`: its own key or a Hit archetype, and a release to its
    own key or `sfx_throw_release`;
  - **every T1 swing's effective level is under SPRAY's and HARD HANDS' contact**, and every release under its own contact;
  - every archetype at the TOP of its band is under its ceiling (hits also under both contacts). Pinned bands: the tick at
    PRESS + 0.06, the commit at 0.34, the throw release at 0.18;
  - the two swing voice lines in HuntScreen are not lead, and SwingContact makes no duck.

### Deviations
- None from the scope. `sound_throttle.json` gained rows for the ten `sfx_<id>_swing_hit` keys, which do not exist as files
  yet (recorded above). That is beyond the eight, and in the design's rule.

### RECORD: the three with no Phase-1 performer
`sfx_air_release`, `sfx_field_tick` and `sfx_cloth_commit` are built now, per design.md section 7 (archetypes first). Their
first consumers:
- `sfx_air_release`: PULSE / REPAY / DRINK (Phases 2-3);
- `sfx_field_tick`: MIRE / WILT (Phase 2);
- `sfx_cloth_commit`: the commits (Phase 3).

`ArchetypeCues.All` names them, which satisfies check_asset_consumers and is the list the hierarchy test reads.

### Open issues
- **The pack's bite is now louder than the champion's basic attack.** In every p15 take, the generic `sfx_hit` at 0.30 (the
  creatures' bites on the champion) measures -29.2 K50 own. The T1 swing hits measure -31.7. Before this package the swing
  played `sfx_hit` at 0.36 (-27.1). The design puts basics at T1 "takes the duck" and does not tier the enemy's hit. The
  director should judge it in the film: either accept it, or bring the enemy bite under T1 in Phase 6.
- **Only the ear can confirm the material reads.** The cues were chosen and shaped by measurement: onsets, band split,
  decay, tonality. Nobody listened to them in this session. The five p15 mp4s are the review set.
- The fist and the stone are true-peak limited (-8.6 / -8.9 dBTP): their effective level is a little under the target,
  never over it.
- Carried from P1.4: the Quiver's strip keeps its own nocked arrow on frame 5. The release cue is no longer silent: it is
  `sfx_throw_release`.

### Disk
- The raw downloads (35 MB) were deleted from build/tmp/arch with `find -delete`.
- Takes were filmed one at a time with `--keep`, rendered, and their PNGs deleted right after each (0 left). The build
  copies of the logs / wav / mp4 were deleted after the evidence copy.
- build/shots is 45 MB and build/tmp 2.5 MB.

## Phase 1 / P1.6: the basic attacks' IDENTITY cue family (10 swing hits + 3 releases), LungePx on the playhead (2026-10-04)

Not committed.

### What was done
- **NEW `tools/asset-pipeline/make_swing_cues.py`**, by P1.5's method.
  - `--extract <dir>` runs once; a plain run builds all 13 into `assets/audio/combat/`, writes `foley/swing_hits/SOURCES.md`
    + `swing_manifest.json` and `swing_cue_levels.json`, and measures.
  - ONE build per cue: no candidates, no selector, no history folder.
  - It imports the DSP from make_jaws_bite (SR, band, norm, place, read_wav, sat_os, write_wav), P1.5's helpers from
    make_archetypes (at_hit, k50, master, sha256, shape, shaped, sub, tail) and max1 from make_press_tick. All read only:
    none of those files, and no approved or archetype cue, was edited.
  - Byte-reproducible: the SHA-256 of all 13 matched across three runs (the third was the `--evidence` run).
  - The cues (design.md section 7's materials):
    - `sfx_seeker_swing_hit`: a knife slicing flesh on a leather thud, a belt's leather snap, a faint sword-draw steel
      edge (top bus 0.22).
    - `sfx_anvil_swing_hit`: a heavy-bag punch, a dropped weight's low thud, a small synthesised sub. No snap, no top.
    - `sfx_metronome_swing_hit`: a single knock on wood and a wood knock, a wood block's click highpassed to a sharp
      click. 71 ms.
    - `sfx_tower_swing_hit`: a heavy thud on the ground and a rock into dirt, a gravel tick (a boot on gravel) +30 ms.
    - `sfx_thornwall_swing_hit`: a shield's thump, a wooden face struck, a metal rattle +10 ms.
    - `sfx_magpie_swing_hit`: a knife stab-and-pull, a knife nick's top, a 30 ms cloth rip.
    - `sfx_quiver_swing_hit`: an arrow's thwack into a target, an arrow-impact shaft knock, a small wood piece's clack +10 ms.
    - `sfx_quiver_loose`: a bowstring's low thwang and the string snap of an arrow release.
    - `sfx_chorus_swing_hit`: a drum skin's slap (the hide) under three bone knocks at 0 / 16 / 38 ms.
    - `sfx_chorus_toss`: a small throwing whip and a little whoosh.
    - `sfx_unbroken_swing_hit`: a dry rock knock, a rock-hit chip, a pebble scatter +15 ms.
    - `sfx_unbroken_toss`: a hard swing's low push and a dark whoosh, nothing above 5 kHz.
    - `sfx_oathbound_swing_hit`: a real steel chain-whip crack, two chain-rustle links at +30 / +70 ms.
- **Foley**:
  - 34 CC0 1.0 Freesound recordings, 37 excerpts (508 KB) in `tools/asset-pipeline/foley/swing_hits/`.
  - Each licence was checked on the sound's own page before the download. The helper refused any page whose only licence
    link was not the CC0 1.0 deed. All 45 candidates fetched were CC0.
  - Downloaded as the hq preview into `build/tmp/swing`, converted to mono 16-bit 44.1 kHz (imageio_ffmpeg), and deleted
    after the extract.
  - No recording is one an archetype is made of. The builder refuses a shared Freesound page, and the test checks it again.
- **`ArchetypeCues.ThrottleRows`** also yields each basic attack's own RELEASE key (`sfx_quiver_loose`, `sfx_chorus_toss`,
  `sfx_unbroken_toss`) at 60.
  - The ten `sfx_<id>_swing_hit` rows existed since P1.5.
  - `sound_throttle.json` was regenerated (the table test failed once with "stale", then went green) and now has 27 gap rows.
- **`HuntScreen.LungePx`** (the OPTIONAL gated item, KEPT, see Verified): the creatures' follow-through reads the playhead
  since the bite landed (`_playheadMs - _recoilFromMs`, the recoil's own clock), no longer `_enemySinceHit` (frame dt).
  - The dt clock now only ends a follow-through that a resting playhead cannot end (at 2 x 0.3 s).
  - Nothing else reads it differently: the attack clip phase and `SinceChampionHit` are unchanged.
- **`films_sweep.sh`**: ten take lines `p16_<champion>` (beat:1, 150 x 2).
- **NEW `swing_cue_test.cs`** (19) and 13 `[Theory]` rows in `sweep_audio_hierarchy_test`; see Tests.
- The swing chains were not touched: every SwingRecipe already named its identity key first (P1.3 / P1.4). The files
  existing is the whole switch.

### Decisions (design.md silent)
- **The mastering targets are the archetypes' own levels**, so swapping an archetype for an identity cue moves the mix by
  nothing:
  - every hit at 0.36 is mastered to K50 -31.3 (3.8 dB under SPRAY's contact, 7.8 under HARD HANDS');
  - every release, at its recipe's own volume (0.18 / 0.16 / 0.16), to -37.0 (6.9 under SPRAY's release, 5.0-5.7 under
    its own hit).
  - The metronome (-31.4) and the unbroken (-32.0) are true-peak limited, so they sit a little lower.
  - Two very sharp knocks (the knuckle, the rock) were given body with `sat_os` (drive 3) before the layer. Without it the
    true-peak cap left them 2-5 dB quieter than the family.
- **"Short" is measured on the file.** A hit's file is 0.22 s, and its audible length (to -40 dB) is 71-144 ms. A release's
  file is 0.17 s (the task's 0.16-0.18 s).
  - "No reverb tail": the last 30 ms are at least 30 dB under the loudest 50 ms. Hits read -71 to -220; releases -35 to -46.
- **"Distinct in material from its archetype" is measured two ways.**
  - The recordings are disjoint from the archetypes'.
  - Each hit's 1/3-octave profile (100 Hz-12.5 kHz) is at least 3 dB RMS from the archetype it replaces. Measured:
    3.6 (thornwall vs wood) to 22.1 (oathbound vs fist).
  - Thornwall is the closest. Both are a wooden thump with a rim, as the design wrote them. Its own materials are a
    shield's thump and a steel rattle, not a crate and a bucket.
- **The releases get the swing's 60 ms throttle row.** The task says so. A release plays once per swing, on the swing's
  beat. The archetype releases (`sfx_throw_release`, `sfx_air_release`) keep the bank's 90 default, as P1.5 decided.
- **Release onsets**:
  - the bowstring twang is placed at 6 ms (the string lets go on the release frame);
  - the two tosses at 45 ms (the swing swells into the release, as `sfx_throw_release`'s 60 ms does).
- **`sfx_seeker_swing_hit` against action_regression.**
  - The key starts with none of `REF_VOICES` (`sfx_seeker_spray`, `_hard_hands`, `_jaws`, `_press`, `_brand`). This was
    confirmed in Python against the module's own tuple.
  - `swing_cue_test` pins the two tuple lines verbatim, so they cannot be widened silently.

### Verified (evidence `production/qa/evidence/vfx-sweep/phase1-p6/`, 4.5 MB)
- **Levels** (`swing_cue_levels.md`): all 13 pass.
  - Hits are K50 -31.3 to -32.0 at 0.36, at least 3.8 under SPRAY and 7.8 under HARD HANDS.
  - Releases are -37.0, 6.9 under SPRAY's release and 5.0-5.7 under their own hits.
- **In the fight's own mix** (`mix_in_the_fight.md`, the PRESS method, own share):
  - every identity hit -31.6 to -32.4, every release -37.6 to -37.7;
  - SPRAY's contact in the Seeker's fight -28.0. The loudest identity hit is **3.6 dB under it**.
- **Trace takes, one per champion** (`swing_cues.txt`, `check_swing_cues.py`): every swing-contact names
  `sfx_<champ>_swing_hit` at 0.36, and every swing-release names the champion's own cue: `sfx_quiver_loose` 0.18,
  `sfx_chorus_toss` 0.16, `sfx_unbroken_toss` 0.16. Each has **exactly one** `sound` line on its ms. ALL OK.
  - 3 swings in each take; 2 in metronome's (its first beat at 700 is not a swing in that fixture).
  - The takes were filmed before the LungePx change. Sound lines do not depend on it.
- **LungePx gate: KEPT.** The five ref takes were filmed twice at the final code.
  - Run A and run B are each IDENTICAL x 10 under `--refs` against both ae20a7fd baselines (`regression.txt`, 20 of 20).
  - The leader's traced lunge x at each playhead (`lunge_playhead.txt`):
    - before: P1.4 vs P1.5 differed at ref_brand 600 and ref_jaws_kill 583 (x=6 vs x=9: the P0.2 sampling);
    - after: run A vs run B differ at **0** playheads in all five.
  - ref_jaws_kill now logs 115 lunge lines instead of 139: the follow-through no longer re-samples on catch-up frames.
- `git diff -- assets/audio` is empty for tracked files. `git status assets/audio` shows the 8 P1.5 files and the 13 new ones.
- The JAWS / PRESS / BRAND SHA-256 pins and the eight archetype pins are green.
- `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1213/1213** (1181 + 19 + 13 theory rows). Core.Tests **1901/1901**.
- `bash tools/check_all.sh`: all gates green.

### Tests (new)
- **`swing_cue_test.cs`** (19):
  - **`test_<key>_is_shipped` x 13**: SHA-256, SHIPPED level.
  - every SwingRecipe's chain resolves to its own champion's file first (contact `sfx_<id>_swing_hit`, the Hit archetype
    second, `sfx_hit` last; a release to its own key, `sfx_throw_release` second). The recipe keys ARE the pinned 13.
  - no identity key is a closed reference voice: action_regression's two tuples are pinned verbatim, and no key is an
    archetype.
  - short, dry, mastered in band:
    - the levels JSON names these bytes and passes;
    - file length: a hit 0.05-0.25 s, a release 0.16-0.18 s;
    - tail <= -30 dB;
    - the volume each was mastered at is its recipe's own;
    - every ceiling is at least 3 dB above;
    - a hit sits at least 3 dB from its archetype.
  - every excerpt has a CC0 SOURCES.md row with its Freesound page and is read by a cue. No raw download is committed. The
    manifest names the bytes. No recording is an archetype's.
  - no candidate or selector exists: one file per key, no `audio_history/swing_hits`, no candidate words in the builder,
    no `RH_SWING_CUE` and no `<key>_` in src.
  - the 13 are at 60 in the bank and in `sound_throttle.json`; `sfx_throw_release` keeps the default.
- **`sweep_audio_hierarchy_test`**: `test_each_identity_swing_cue_is_its_chains_file_and_sits_under_the_references` (13
  theory rows):
  - the file exists;
  - a hit at 0.36, and at 0.38, is under SPRAY's and HARD HANDS' contacts (raw effective level);
  - a release is 0.16-0.18, under its own hit at 0.36 and under SPRAY's release.

### Deviations
- None from the scope.

### Open issues
- **Only the ear can confirm the materials.** As in P1.5, the cues were chosen by measurement: onsets, band split, decay,
  distance from the archetype. Nobody listened to them. The ten p16 mp4s are the review set.
  - Expect the most debate on the thornwall (closest to its archetype), the seeker (a hiss-heavy cut: 47 % of its energy
    above 4 kHz) and the anvil (no top at all, by design).
- **The pack's bite (`sfx_hit` 0.30, own -29.2) is still louder than every basic attack (-31.6 to -32.4).** This is carried
  from P1.5 and is unchanged: the identity cues hold the archetypes' level. It is the director's call in Phase 6.
- The LungePx change is presentation-only and pre-existing in kind (ADR-012's curve is untouched; only its clock moved
  from frame dt to the playhead). At a steady 60 fps the two clocks agree.

### Disk
- The raw downloads (11 MB, 45 files) were deleted from build/tmp/swing with `find -delete`, and the folder removed.
- Takes were filmed one at a time with `--keep`, rendered, and their PNGs deleted right after each. The build copies of
  the logs and mp4s were deleted after the evidence copy. The p6la / p6lb ref logs were deleted after the regression.
- build/shots is 45 MB and build/tmp 3 MB.

## Phase 1 / P1.7: acceptance - the film, the regression, evidence, the Phase 1 summary (2026-10-04)

Not committed (the work package's closing instruction: "Do NOT commit"; the commit is the orchestrator's next step).

### What was done
- **`films_sweep.sh`**: eleven `p17_*` take lines (Part 1 A at the fastest TEMPO + chapters 01-10, design.md 8 / 9). The
  DISK GUARD is untouched (films_brand.sh's text, only the script's own name in the message, as since P0.1).
- **The film**, one take at a time with `--keep --mp4`, each <= 150 frames, frames deleted right after its pieces:
  `production/vfx-sweep/review/phase1/` (16 MB): 11 mp4s at 720p CRF 28 with the traced sound (film_audio.py +
  sound_throttle.json), the 0.25x `p17_c01_seeker_slow.mp4` from the same frames, an 8-cell true-speed strip, one full
  still at the decisive swing contact, a trace excerpt with the per-layer alloc summary and the full log per take, and
  INDEX.md with the numbers table and what to look for.
- **The final reference regression**: the five ref takes re-filmed at the final code: **IDENTICAL x 10** under `--refs`
  against both ae20a7fd baselines (`production/qa/evidence/vfx-sweep/phase1/regression.txt`).
- **Evidence** `production/qa/evidence/vfx-sweep/phase1/` (1.2 MB): the 11 take traces, the 5 ref traces, regression.txt,
  `measure_phase1.py` -> `measured.md` (the per-take table: swings, contact == Strike ms, step-in peak vs row gap,
  flashes, cues per ms, releases, swing-draw sprites and alloc), fx_edges.txt, gates_assets.txt, leftover_selectors.txt,
  README.md.

### Decisions (P1.7)
- **Eleven takes, not ten.** design.md section 8 names "Part 1 segment A at TEMPO 60 plus the ten swing chapters"; A is
  the eleventh (150 x 1, 2.5 s, as r_after_fast). Its 30 s duration is not possible under the 150-frame cap.
- **Chapter 01 at true 60 fps** (150 x 1, two swings) so its marked (slow) render is made from the same frames; the
  other chapters are 150 x 2 (5 s, 30 fps; design.md's 6 s is not possible under the cap at 30 fps).
- **The chapters seek beat 2 (lead 0.6 s), not beat 1.** A seek rebuilds the replay after the live run has already
  voiced beat 1's swing: on the replay that swing DRAWS but its `swing-contact` line and its cue are gone (the swing's
  once-per-ms voice guard survives the rebuild), and a bare `beat:1` seek saves its first frame just after the contact.
  Beat 2 onward is live and clean. Recorded as an open issue (rig), not fixed here (no new features).
- **The BRAND swap in the magpie take**: design.md 9's active-signature build is signature + BLOW + PRESS + JAWS@Shadow.
  The magpie's take has BRAND@Shadow in JAWS's slot (still 2 Active + 2 Passive) so the agnostic BRAND, which landed in
  Phase 1 (P1.2), is on film once; the magpie is the champion design.md's Part 1 E gives BRAND to, and JAWS-agnostic is
  filmed in the anvil and metronome chapters (Shadow outline 9B7BFF on their answers).
- RH_SHOT_CREATURES (design.md's "creatures 3") was NOT used: it hides creatures that still fight, so a swing could
  target an undrawn creature. The fixture's own wave is filmed.

### Phase 1 summary (P1.1-P1.7)
- **Built**: ten `attack` `.clip.json` timing files + the four baked-VFX cleans (P1.1); `RecipeTier` and the ByAnySkill
  tier for JAWS / PRESS / BRAND with the Source-outlined number (P1.2); `SwingRecipe` / `SwingClock` /
  `SwingPerformance`, the six melee basics with their step-in, FIRST BEAT, `fxp_dust_soft` (P1.3); the four missile /
  reach basics, `ReachStrand` and the four props (P1.4); the eight archetype cues and the hierarchy test (P1.5); the
  thirteen identity cues and LungePx on the playhead (P1.6); the film and the acceptance (P1.7).
- **Decisions carried as the phase's rules**:
  - **The envelope owns time, the authored file owns the frames** (P1.3): a swing is committed exactly as the plain clip
    was (start, speed, settle, handoff, yield untouched); only WHICH authored frame is drawn changes. This is why SPRAY's
    and HARD HANDS' starts behind a swing cannot move and the references stay identical.
  - **The 'place own' rule** (P1.1): `"place": "own"` in a timing file (`ActionClipTiming.PlaceOwn`) is the inertness
    guard; without it a timing file places as the idle, so adding the ten files moved nothing on screen.
  - **The action ByAnySkill tier is empty in Phase 1** (P1.2): SPRAY-agnostic needs each champion's `projectile` timing
    and missile (Phase 4); HARD HANDS never gets an agnostic entry.
  - **The dust part** (P1.3): `fxp_dust_soft` was made in Phase 1 (design.md 6 gives it no phase) because the Tower's
    contact needs it; procedural, no PixelLab. It and the four props pass SOFT + EDGE; LIVE fails by nature (materials).
  - **The three consumer-less archetypes** (P1.5): `sfx_air_release`, `sfx_field_tick`, `sfx_cloth_commit` are built now
    (archetypes first) and reached through `ArchetypeCues.All`; their first performers are PULSE / REPAY / DRINK, MIRE /
    WILT and the Phase 3 commits.
  - Levels: every basic hit 0.36 at K50 about -31.3 (3.8 dB under SPRAY's contact, 7.8 under HARD HANDS'); releases
    0.16-0.18 at -37.0; all 21 cues SHIPPED-pinned by SHA-256, none HUMAN-APPROVED (reserved for the owner).
- **Deviations over the phase**:
  - **The three closed-test edits** (P1.2): `jaws_reaction_test.cs:134`, `press_field_test.cs:51`,
    `brand_mark_test.cs:58` inverted from "another hunter keeps the old way" to "resolves to the same instance", plus a
    fourth with the same intent (`brand_mark_test.cs:81`); nothing else on those lines; the SHA-256 pins untouched.
  - `action_regression.py --refs` holds JAWS' `reaction-clamp belt=` by y only (P1.3): x is the champion's draw push,
    which the step-in changes by design; only the RH_SHOT_SOCKETS overlay reads it.
  - The first P1.1 "1 error with no message" was an xUnit1026 hidden by incremental builds (fixed in P1.2).

### Verified (final code)
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1213 / 1213**, Core.Tests **1901 / 1901** (no tests added in P1.7: no game code changed). The JAWS /
  PRESS / BRAND tests (174, the SHA-256 pins among them) green.
- `bash tools/check_all.sh`: all gates green. `check_asset_consumers`: no new orphans, the 9 art files and 21 cues reached.
- `check_fx_edges.py` SOFT + EDGE pass on `fxp_dust_soft` and the four props with their `_edge` masks (edge 0).
- `git diff ae20a7fd -- assets/audio` empty for tracked files; `git status assets/audio` = exactly the 21 new cues.
- No leftover selector: the set of RH_* names in src/*.cs is identical to 702576d6's (the QA rig RH_SHOT_BUILD / SEEK
  came with Phase 0); no candidate words in the new Phase 1 sources; the legacy swing (40 px push + fx_weakhit puff) is
  reached only when no swing is performed (RH_ACTION_RECIPES=0, no timing file / anchor, or a yielded beat); 0 generic
  0.38 thuds and 0 puffs on any swing ms in the eleven takes.
- In the film: every swing contact on its Strike's exact ms (31 of 31), one swing cue per swing ms, every swing flash
  0.30 / 110, step-in peaks 20 / 25 % of the row gap and home (0) at every attack clip-end (0 of 39 non-zero), the
  missiles' releases at beat - 200 / 220, the chain taut on 3 of 3 beats, swing-draw alloc 0 on every frame.

### Open issues (carried to Phase 2+)
- **PRESS allocates ~13 MB on first use in the combat frame** (`field-draw alloc` max 13 107 864 in the takes; P0.1).
  PRESS is closed; whoever builds `FieldLayers` (Phase 2) should move the load to warm-up.
- **The per-frame body point** (Phase 0): the life pip and head numbers float above crouching creatures; the director
  said "not now". Still open.
- **The SPRAY-agnostic Seeker hazard** (Phase 4): `ByAnySkill["volley_spray"]` sits above ByForm, so Phase 4 must add
  `BySkill[("seeker","volley_spray")]` FIRST or the Seeker's closed SPRAY resolves to the agnostic recipe (pinned by a
  source-order test).
- **DEADWEIGHT's lump** (design.md 5.31, listed beside the anvil's basic): not in Phase 1; it needs the per-creature
  `AfflictionLayer` (Phase 5).
- **Rig: a seek that rewinds over an already-voiced swing ms** leaves that swing silent and untraced on the replay
  (see Decisions). Films seek beat 2; the fix belongs to the rig (reset the swing voice guard on the replay rebuild).
- **FIRST BEAT's white outline is not on screen in the metronome chapter**: it falls on CLOCKWORK's opening cast at 700,
  before the window (the beat-1 seek problem above). It is in the take's log (3 numbers, outline=FFFFFF).
- From P1.3-P1.6, for the director's eye and ear: the Tower's slam lands far from the creature at 20 %; the Quiver strip's
  own nocked arrow on frame 5; the pack's bite (`sfx_hit` 0.30, -29.2) louder than every basic hit (-31.6 to -32.4); none
  of the 21 cues has been listened to; agnostic PRESS / BRAND print no number, so their slot Source is not shown; the
  stale comment at `brand_mark_test.cs:78`; the step-in peak (~65-130 px) beyond the published 40 px envelope.

### Disk
- Every take filmed one at a time; its PNGs deleted right after its pieces (`find build/shots/sweep -name '*.png'
  -delete`): build/shots/sweep holds **0 PNGs**. The ref takes deleted their frames themselves; the build copies of all
  logs and mp4s were deleted after the copies.
- Review folder 16 MB (limit 25), evidence 1.2 MB. build/shots 45 MB (pre-existing; 41 PNGs outside sweep), build/tmp
  2.6 MB. 53 GB free on C:.

## Phase 1 / review film: the director's p1_* plan (2026-10-04)

Not committed (the orchestrator commits).

### What was done
- **`films_sweep.sh`**: ten `p1_*` take lines (A + 02-10) per the director's film plan: A seeks `skill:volley_spray` with
  a 2.2 s lead at the fastest TEMPO (150 x 2); the chapters seek `beat:1` with a 0.6 s lead (150 x 2); every take its
  whole four-slot build. Disk guard untouched.
- **Two seek-rig fixes** (needed for `beat:1`; the P1.7 open issue "a seek that rewinds over an already-voiced swing ms"):
  `SwingPerformance.ForgetVoiced()` is called in HuntScreen's DevSeek replay rebuild, so the re-crossed beat-1 swing is
  voiced and traced; the same block now `_firstBeat.Reset()`s and re-`Cross`es the silently advanced events, so FIRST
  BEAT's outline appears on the replay (it was missing: the live run had consumed the struck set). Rig path only.
  Test: `swing_melee_test.test_a_rewound_replay_voices_an_already_voiced_swing_ms_again`.
- `review_take.py`: the trace excerpt now keeps `swing-start/-contact/-release/-land/-strand` and `yield` rows.
- The film: `production/vfx-sweep/review/phase1/` (INDEX.md; ~18 MB new, the P1.7 film moved to `p17_superseded/`,
  34 MB total): per take a 720p CRF 28 mp4 with traced sound, an 8-cell strip (67 ms spacing), 2 stills, the trace
  excerpt and the log; 0.25x `_slow.mp4` for A (the first whole swing = chapter 01), 07 quiver and 10 oathbound, cut
  from the same frames (a shot-filtered log into film_audio.py --slow 4).
- Regression: the five refs re-filmed at the final code, IDENTICAL x 10 against both ae20a7fd baselines
  (`production/qa/evidence/vfx-sweep/phase1/regression.txt`, ref logs replaced; `measured_p1.md` added).

### Decisions
- A uses `$TEMPO` (ref_fast's own RH_SHOT_TAKE, which includes `volley`) since the plan says "posed like ref_fast".
- Anvil: no RH_SHOT_ENEMY tuning; JAWS answers the pack's bites at 1000 and 4000 inside the window as is.
- Chapter 01 is A's slow render (the plan's "from the SAME frames"); no separate 60 fps chapter-01 take.

### Observations for the director (not fixed: filming task)
- Tower: the hammer ends at his feet while dust + ring are at the creature (~250 px apart) - the P1.3 detachment stands.
- Metronome: beat 1 is CLOCKWORK's cast, so FIRST BEAT's outline is on the cast's three numbers at 700, never a swing.
- Oathbound strand: out at beat-204, home at beat+137 (design names a 120 ms recoil).
- `mark-draw` allocates 1 128 B on one frame at BRAND-agnostic's first use on the magpie (not steady state; new).
- PRESS `field-draw` first-use 13-14 MB on 4 frames (carried issue).
- The fixture save's "WELCOME BACK" offline notice covers the arena's top-left in every take.

### Verified
- Release `-warnaserror --no-incremental` 0/0; Game.Tests 1214/1214, Core.Tests 1901/1901.
- swing-draw / reaction-draw alloc 0 on every frame of all ten takes; every swing contact on its Strike ms (31 of 31).

### Disk
- One take at a time, PNGs deleted after each (`find build/shots/sweep -name '*.png' -delete`): 0 PNGs left; build/shots/sweep 7 MB.

## Phase 1 / correction pass: the director's review of the real footage (2026-10-04)

Not committed (the orchestrator commits). Every required correction is done, and every polish item is done.

### What was done
- **(1) REQUIRED, generic path: a quiet hit is never a reaction's answer.** `HuntScreen.cs` (the Strike case) echoed
  every Strike a presented reaction owned at its ms as the reaction's answer: skill grade, its word, and its Source
  outline. So the Anvil's DEADWEIGHT release printed "-26 JAWS". The fix is one pure decision,
  `GenericHits.Look(quietHit, skill, word, outline)` -> `NumberLook(Grade, Word, Outline)`: a quiet hit is
  `Quiet` / null / null. Both branches use it. When the answer is held, the number still waits for the snap, so the drop
  still follows the shown bite. The `ReactionEcho` record now carries a `NumberGrade Grade` instead of `bool Skill`.
  `ReleaseEchoes` prints that grade, and a quiet echo never plays `sfx_crit`. The trace line already said
  `grade=Quiet`. Film: `amount=26 ... grade=Quiet` (no outline) at 1367, beside `amount=2 skill=True outline=9B7BFF`.
  The JAWS pins, references and Core are untouched.
- **(2) REQUIRED, the Tower: a hand repaint of frames 4-6** (`tools/asset-pipeline/v2/tower_slam_forward.py`). The
  body comes from his settle frame (7); its upright hammer head is cut away and the cut edge outlined. The head is frame 1's
  hammer head (rune face), mirrored, on a drawn haft in the strip's wood, and turned along the arc: up-forward on 4, level
  at knee-to-waist height past the lead foot on 5 (contact), dipping on 6. The script always starts from 702576d6, so it
  is idempotent. StrikeHand was re-authored to the head's striking face: 4 [0.88, 0.46, -39], 5 [0.93, 0.64, 0],
  6 [0.91, 0.72, 18]. Contact is still frame 5 and the step is still 20 %. No bigger step and no ground line.
- **(3) REQUIRED, the Magpie: the dagger painted back** (`tools/asset-pipeline/v2/magpie_dagger.py`). One steel dagger
  (16 x 7 source px x3, the Seeker knife's density: outline, steel shades, a bevel highlight, a brass guard; the grip is in
  the fist). Its guard and blade are painted over her fist on frames 2-7: forward on the thrust (2, 3), carried back
  (4-6), low on the settle (7). The base is strip_bake's cleaned strip, cached as
  `keypose_sources/magpie_attack_cleaned_strip8_512.png` so the script is idempotent.
- **Polish, all done:**
  - NumberOutline halo: `NumberOutline.DrawHaloed` draws 8 passes in the halo colour 1 px outside the colour stroke,
    then the stroke and the ink (13 passes, no allocation). The screen draws every outlined number with
    `UiKit.Ink * 0.9` as the halo, so FIRST BEAT now (and CALL / OATHMARK / REND later) has a dark ring.
  - `SwingRecipe.StepInPower` (default 2; `SwingClock.StepIn(..., power)`, exactly `u*u` when it is 2, so every
    other champion is unchanged) set to 3 for the Thornwall and the Anvil.
  - The Quiver: `tools/asset-pipeline/v2/quiver_one_arrow.py` clears the nocked arrow on frame 5 and the hanging one on
    frame 6. Both were over clear air, and no body texel was touched.
  - The Unbroken's chip: `HeadLength` 0.08 -> 0.11 (another 1.4x, about the charm's 40 px), `EdgeBrightness` 0.25 -> 0.35.
  - The Oathbound's links: `ReachLook.LinkKey` / `LinkHeight` (1.9 x thickness) / `LinkStep` (0.72 of a link).
    `prop_seeker_chain_link`'s face-on and edge-on cells are stamped alternately along the strand's pieces by arc length,
    over the iron body (`HuntScreen.DrawStrandLinks`). Only the Oathbound sets it, and DRINK's REACH will choose its own.
- **`strip_bake.py check`'s hard-cut count** now counts only texels the clean left UNCHANGED. The dagger's own drawn
  outline beside the cleared air was being counted as a "cut" (89). All four strips report OK, 0.
- **Re-filmed only the affected takes** (02, 03, 04, 05, 06, 07, 09, 10). Same `p1_*` lines, tag `phase1_fixed`, one
  at a time, frames deleted after each. Pieces in `production/vfx-sweep/review/phase1/` as `<take>_fixed.*` (stills
  re-saved at q78); INDEX.md has a "Correction pass" table. Logs and evidence are in
  `production/qa/evidence/vfx-sweep/phase1_fixed/` (856 KB + the 8 take logs): the 5 ref logs, regression.txt, and the
  before / after sheets for the tower and the magpie.

### Decisions (design.md silent)
- **Tower: a hand repaint, not PixelLab.** Inpaint keeps unmasked bytes but only up to 256 px (the strip is 512), and
  `edit_image` redraws the figure and needs the frame as a URL or inline base64, which this rig cannot hand it. The
  repaint keeps every body pixel the generator drew. Using frame 7's body on 4-6 (the hammer, not the stance, carries a
  60-100 ms swing) is the trade. The upright head in 5 and 6 hid his lead leg, so those frames could not be re-used.
- **Magpie: painted into the strip, not a runtime prop at the socket.** It is one object drawn into the art. A runtime
  prop would need sockets on 5-7 and a second draw path for a pose the strip already shows. The StrikeHand sockets (2-4)
  are unchanged.
- The Tower recipe's `ForceDegrees` 90 / `ContactPoint` y 0.72 were kept: the head still arrives down-and-forward onto
  the body's lower half, and the dust/ring are the review's accepted contact picture.
- Quiver frame 4 still shows the arrow on the string. 4 is the release frame (the arrow leaves its socket there), and the
  review named only 5.
- FIRST BEAT: the halo is the review's device. White on white ink now reads as a heavier, dark-ringed glyph. That is all
  a white accent can do on white ink; a coloured accent would separate more, but the colour is design.md's.

### Declined / not done
- None of the review's items. Not pulled forward (Phase 2 / 6, as the review said): HOLD FAST's gain rings (still bury
  the chip at 2100), the pack's bite level (Phase 6 mix pass, target ~0.24), and the 21 cue materials (the owner's ear
  in the showcase).

### Verified
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests **1225 / 1225** (+11: `reaction_quiet_answer_test` 8, the halo test, the StepInPower test, the chain-link
  test; `HuntGenericHitsTest` and `number_outline_test` source assertions updated to the new call sites). Core.Tests
  **1901 / 1901** (Core untouched).
- `bash tools/check_all.sh`: all gates green (no new orphans; `prop_seeker_chain_link` is now also reached by C#).
  `strip_bake.py check`: OK x4.
- The five references, re-filmed at the corrected code: **IDENTICAL x10** against both ae20a7fd baselines.
- In the eight `_fixed` takes: `swing-draw` alloc 0 on every frame. Every swing contact is on its Strike ms (contact
  frames unchanged: tower 5, magpie 3, thornwall 3, ...). The Oathbound's swing-draw is now at most 42 sprites per frame
  (16 before): the ~26 links come from one texture, so they batch as one draw.

### Open issues (carried)
- PRESS's 13-14 MB first-use alloc (`field-draw` max 14 193 328 in the oathbound take), the per-frame body point, the
  step-in beyond the 40 px envelope (accepted), and the stale `brand_mark_test.cs:78` comment: unchanged.
- The Tower's hammer head is still ~200 px short of the creature at 20 %. It now points at the row, which was the
  correction; closing the gap would need root motion (a signature privilege) and is not a basic's.
- The Unbroken's chip must be judged again after Phase 2's HOLD FAST claim (the review's instruction).

### Disk
- One take at a time, PNGs deleted after each (`find build/shots/sweep -name '*.png' -delete`): 0 PNGs left.
  build/shots/sweep 21 MB, build/tmp 3 MB, 53 GB free. The review folder is 55 MB (the `_fixed` set is 21.8 MB of it).

## Phase 1 - verification and commit (2026-10-04)

### Director's verdict on the correction pass
- All three mandatory items RESOLVED and accepted: (1) a quiet hit is never a reaction's answer (the Anvil's DEADWEIGHT
  release is a quiet grey "26", the headline "-2 JAWS"); (2) the Tower's hammer comes over and points at the row (contact
  frame 5 at 700); (3) the Magpie's dagger painted back (contact frame 3 at 700).
- Polish all resolved: FIRST BEAT halo, Thornwall StepInPower 3, Quiver one arrow, Oathbound chain links. The Unbroken's
  chip is bigger and brighter but HOLD FAST's rings still bury it: not a defect of this pass, re-judge after Phase 2's
  HOLD FAST claim. "Phase 1 is accepted on this evidence. Nothing is still wrong; no re-film needed."
- So no fix was applied in this step.

### Verified before the commit
- Release `dotnet build IdleXIdle.sln -c Release -warnaserror --no-incremental`: 0 warnings, 0 errors.
- Game.Tests 1225 / 1225, Core.Tests 1901 / 1901.
- `bash tools/check_all.sh`: all gates green.
- Closed references: the JAWS / PRESS / BRAND / SPRAY / HARD HANDS tests green (included in the 1225; the SHA-256 cue
  pins untouched; no approved cue in `assets/audio` modified; `src/IdleXIdle.Core` untouched). The `--refs` regression
  in `production/qa/evidence/vfx-sweep/phase1_fixed/regression.txt`: IDENTICAL x10 against both ae20a7fd baselines.
- No prototype or candidate selector introduced: the only `RH_*` names in the diff are the existing QA switches
  (`RH_ACTION_RECIPES`, `RH_FIELD_RECIPES`, `RH_MARK_RECIPES`, `RH_REACTION_RECIPES`), named in doc comments.
- Disk: 0 PNGs under build/shots and build/tmp (the stale BRAND scale / body-point PNGs from 2026-09-30 deleted too;
  their evidence is committed). build/shots 24 MB, build/tmp 2.5 MB, 53 GB free.

### Open (carried into Phase 2+)
- The Unbroken's chip vs HOLD FAST's gain rings (re-judge after Phase 2's HOLD FAST claim).
- PRESS's 13-14 MB first-use alloc (`field-draw`), the per-frame body point for numbers, the stale
  `brand_mark_test.cs:78` comment.
- The Tower's hammer head ~200 px short of the creature at the basic's 20 % step (accepted; root motion is a signature
  privilege).
- The pack's bite level (Phase 6 mix pass, target ~0.24) and the 21 cue materials (the owner's ear in the showcase).
