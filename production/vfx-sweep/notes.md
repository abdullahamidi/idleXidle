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
