
# Notes: shared-a

I read the Core and Game code only. Nothing was edited. Main files:
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SkillCatalogue.cs: BLOW at 390, REPAY at 454, CALL at 516, WEEP at 623.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SoloBattle.cs.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/HuntScreen.cs: Strike case at 2219, Skill case at 2345, ShieldGained at 2431, PlaySkillVfx at 2595, UpdateChampionClip at 6433-6506, FxFor at 7195, inspector line at 2785.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/Vfx/VfxProfiles.cs: 157-262.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/Presentation/ActionRecipe.cs: 505-528.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/Presentation/ReactionRecipe.cs: 457-466.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/ActorClips.cs: 54-58.

None of the four skills has a recipe. Each one plays the old generic route: the Form's plain clip, then PlaySkillVfx with the per-character Form strip, then sfx_cast, then the style callout, then the generic hit handling.

Cross-cutting findings:
1. WEEP is effectively dormant on screen. It never emits a Skill event because the Kill-reaction dispatch requires BasePower > 0 (SoloBattle.cs:1897). So fx_weep and the CastRain profile are never played, and the 'trap' clip ActorClips loads for it is never used. Its whole on-screen life is anonymous HitSource.Bleed Strikes every 500 ms on the front enemy. Each one brings a thud, a flash and a number, and none of it can be told apart from VENOM.
2. Core reports no bank, pool, charge or empowerment state for REPAY, WEEP or CALL. Specifically: no REPAY bank events, no WEEP pool events, no SPEND charge-spend or close events, no FOUNDATION priming event, and Strike events carry no 'amplified' flag. An honest presentation probably needs information-only Core reports, fingerprint-proven the way BRAND's Marked and JAWS' ReactionArmed were.
3. Dial/text mismatch: REPAY's CARRIED card says 10% of max health, but the code sets WaveStartShieldFraction to 0.05 (SkillCatalogue.cs:482-483).
4. SPEND makes the enemy inspector read 'FOR 600s', and it keeps showing MARKED after the count closes the window. STEADY reads the tick ceiling instead.
5. BLOW must stay isolated from HARD HANDS. They share the Strike Form and the 'strike' FxKey (ADR-011:814, 936), so BLOW needs its own BySkill recipe and its own strip.
6. Of the four, only CALL emits Marked. BRAND's per-wave builder reads a Marked that falls on the same millisecond as a BRAND Aura tick (HuntScreen.cs:1603-1606), so a CALL cast on that exact tick could be misread as BRAND's depth.

Showcase: all four can be posed through the existing harness. Use RH_SHOT_SWAP (Game1.cs:2899-2918):
- volley_spray:hammer_blow, volley_spray:snare_repay and volley_spray:sign_call for the active slot.
- snare_jaws:volley_weep for the reaction slot.

Variations are posed with RH_SHOT_VARIATION (2920-2937). WEEP has no Skill event to seek to, so its seek has to aim at an EnemyDown or a Bleed Strike instead.

# Notes: shared-b

Read-only inventory. Key files: Core C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SkillCatalogue.cs:655-790 (the four SkillDefs), C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SoloBattle.cs (Field fork 2240-2478, cast path 2489-2999, bite resolution 3062-3111, Heal 2119-2187, LandSpread 2021-2116). Game: C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/HuntScreen.cs (field roles 1509-1516, event switch 2213-2479, PlaySkillVfx 2595-2616, held aura 7344-7389), Vfx/VfxProfiles.cs:141-259, Presentation/MarkRecipe.cs:409-425 (FieldRoles).

Cross-cutting findings that matter for all four:
1. None of the four has a recipe. ActionRecipes, FieldRecipes, MarkRecipes and ReactionRecipes are keyed only for the closed five, so all four use the legacy paths.
2. FieldRoles allows ONE performed field and ONE held field. HoldAura returns early whenever a performed field (PRESS) exists (HuntScreen.cs:7381). So MIRE or WILT beside PRESS draws nothing, and MIRE plus WILT draws only the first. This must be redesigned before MIRE and WILT recipes can coexist with PRESS and BRAND.
3. The held aura's flare runs on a wall-clock 1 s metronome (PulseAuraOnClock), not on the Aura events, so it drifts from the real ticks (and from RADIANCE's 600 ms).
4. The slow (Slowed) and attack break (AttackBreak) are visible only in the hover inspector.
5. PULSE uses the same fx_aura art as MIRE's held field. If a Field tick shares PULSE's cast ms, PULSE's Strikes are misclassified as aura blows (HuntScreen.cs:2216, 2234).
6. Heal presentation is the generic HealColumn plus '+N' per Heal event. MIRE (Nature source), WILT/SUP and DRINK can fire it every second or twice per cast. Once the per-wave ceiling is spent, no event is emitted.
7. Possible card/implementation mismatch: DRINK's SIPHON variation says the per-wave healing limit doubles, but it only sets Lifesteal x2. The ceiling is raised only by BuildTrigger.Siphon (SoloBattle.cs:1140, HealTuning.cs:129-134), and the multiplier there is 1.5, not 2.
8. Assets: shared fx_aura (a flame bubble), fx_wilt (a ring with drops), fx_transformation, and per-seeker fx_seeker_transformation plus char_seeker_transformation. There is no seeker 'aura' clip (it falls back to char_seeker_cast). There are no skill-specific sounds for any of the four; only sfx_cast and sfx_hit are used.
9. Showcase tooling exists: RH_SHOT_SWAP and RH_SHOT_VARIATION (Game1.cs:2899-2936), and the fixtures fightaura and fightinspect already weave MIRE and WILT.

# Notes: sig-a

I read all five skills' Core and Game paths. None of them has a recipe: ActionRecipes BySkill/ByForm (ActionRecipe.cs:505-528), FieldRecipes (FieldRecipe.cs:379-389), ReactionRecipes (ReactionRecipe.cs:459-470) and MarkRecipes (MarkRecipe.cs:385-396) contain Seeker entries only. All five are owner-locked signatures (BuildComposer.cs:194, LoadoutRepair.cs:40), so each needs only its own champion's presentation. RH_SHOT_HUNTER=<id> already poses the champion with its signature (Game1.cs:2451-2467). RH_SHOT_VARIATION=<skill>:<VAR>+<REINF> poses variations (Game1.cs:2920-2936).

The two Actives (HARDFACE, CLOCKWORK) go through the legacy cast path: a plain Form clip (char_<id>_<clip>_strip8_512) and PlaySkillVfx with FxFor = fx_<id>_<fxkey>_strip8_512 when it exists, else fx_<key> (HuntScreen.cs:7192-7199). That draws ONE effect on the first Strike's creature, whatever the target count. The cast plays the generic sfx_cast and sfx_hit.

The three Fields (GRAVE SONG, HOLD FAST, SLOW FALL) emit no Skill event. Their only picture is the HELD aura behind the champion (FieldRoles.Choose then HoldAura, HuntScreen.cs:1508-1516 and 7376-7390), drawn with their FxKey art:
- GRAVE SONG uses the shared fx_aura.
- HOLD FAST uses fx_unbroken_trap, a spiked snare.
- SLOW FALL uses fx_tower_strike, a falling stone drawn on the champion.

That held aura brightens on a FIXED 1 s clock (AuraPulseSeconds, HuntScreen.cs:7316, 7344-7349), not on the skill's real interval. Field hits are silent: no flash and no puff, just one summed number over the front creature (HuntScreen.cs:2229-2260, FlushAuraTotal). Only the first plain field in a build gets the held aura.

Some state changes have no event and no moment on screen:
- HARDFACE's kill-strip has Break and DefenceNow events but no case in the event switch; it shows only as the standing badge (HuntScreen.cs:4157).
- CHANTRY's weaker bite emits no event at all.
- HOLD FAST's tick at the shield cap emits only Aura.

Asset reads:
- fx_anvil_strike is a crescent moon, which misreads as a hammer.
- fx_metronome_projectile is almost static, which fails the PROJECTILE internal-motion contract.
- fx_tower_strike is a good single falling-stone impact, but it is used as a looping aura.
- fx_unbroken_trap is a spike row, the wrong object for a shield.

None of the five has its own sound.

Suggested order: SLOW FALL and HOLD FAST first, because their current pictures are actively wrong, then CLOCKWORK, HARDFACE and GRAVE SONG.

Key files:
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SkillCatalogue.cs
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SoloBattle.cs
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Expeditions/WaveModel.cs
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/HuntScreen.cs
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/Vfx/VfxProfiles.cs
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/Presentation/MarkRecipe.cs (FieldRoles)
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tools/asset-pipeline/v2/spec.json

# Notes: sig-b

I read the code only and edited nothing. Line references are against fix/vfx-fade at ae20a7fd.

1. **All five closed recipes are keyed to "seeker" only.** That covers ActionRecipe.cs:505-513, ReactionRecipe.cs:459-461, FieldRecipe.cs:379-381 and MarkRecipe.cs:385-387. So SPRAY, HARD HANDS, JAWS, PRESS and BRAND fall back to the plain path on the other nine champions. This report does not cover the other five signatures (HARDFACE, GRAVE SONG, CLOCKWORK, HOLD FAST, SLOW FALL).

2. **The basic attack is not the SPRAY path, for any champion.** SPRAY is `ByForm[("seeker","projectile")]`, so it only applies to the Seeker's Projectile-Form skills. Every champion's MIGHT swing is the same Core hit (SoloBattle.cs:3002-3027). It plays as a plain 8-frame "attack" clip with no timing file:
   - the clip takes 0.55 of a beat, with contact at 5/8;
   - a 40 px push decays after contact;
   - the hit uses `sfx_hit` 0.38, the usual flash and a `fx_weakhit` puff on every other blow.
   - `ActionRecipes.For` only resolves skill casts, so the swing needs a new lookup tier.
   - No champion's basic attack has a final presentation.
   - The attack strips imply different actions per champion: sword slash (Seeker), bow shot with nothing flying (Quiver), punch (Anvil, Metronome), thrown charms (Chorus), a glow burst (Unbroken, action unclear), a hammer slam at his own feet (Tower), a shield bash with a baked flash (Thornwall), a chain whip (Oathbound), a dagger with a baked green arc (Magpie).

3. **The "trap" clip for reactions probably almost never plays.** It only commits when `beatMs` is null, meaning no further swing or cast exists in the wave (HuntScreen.cs:6453-6470). BACKDRAW, NARROWS and OATHMARK therefore effectively never move the figure mid-wave. Even when the clip does play, it is the wrong action for each skill. This is a dormant-feature candidate; a `PresentTrace` 'clip-start trap' run would confirm it.

4. **BACKDRAW fires on the wave's last kill.** There is no alive>0 gate, so the Skill event is emitted and the arm spent with no Strikes (SoloBattle.cs:1895-1932, 2061). The screen then plays a phantom callout, effect and sounds aimed at `TargetSlot()`.

5. **The Marked event's payload is the opposite of its doc.** Slot carries the percent and Amount the window (SoloBattle.cs:3372; WaveReplay.cs:519-521), while WaveModel.cs:434 says the reverse.
   - The window can also be stretched without a new Marked event (the MIND signature, SoloBattle.cs:2980-2985), so a presented close read from the event alone can come early.

6. **Generic presentation that misleads for these skills:**
   - The callout prints the style word (VOLLEY/SNARE/SIGN/DRAIN), not the skill name (HuntScreen.cs:1197-1205).
   - Reactions get the `sfx_hit` +0.25 pitch thud even when they deal no damage (OATHMARK, HuntScreen.cs:2387).
   - Effects are placed by ClipKey through `VfxProfiles.ForSkill`. So NARROWS, which hits one target, draws a row-wide ground ring. OATHMARK, which covers the whole wave, draws one overhead ring. PAYING WORK, which hits one enemy, draws a gem shower on the Magpie herself.
   - The per-champion `fx_<id>_<key>` strips are 2026-09-01 white line-art one-shots: a quiver arrow, a barbed-wire ring, a chain ring and a gem cluster. Under ADR-010/011 they can serve as reference or parts only.

7. **Sounds and strip timing.** None of the four signatures has its own sound. None of these champions' strips has a .clip.json; only the Seeker's projectile and hard_hands strips have one.

# Notes: infra

INFRASTRUCTURE INVENTORY FOR THE PRESENTATION SWEEP (read-only; branch fix/vfx-fade @ ae20a7fd). Paths are relative to C:/Users/sanan/OneDrive/Masaüstü/idleXidle.

=== 1. HOW A NON-RECIPE SKILL IS PRESENTED TODAY (the "legacy" path) ===
The event loop is in HuntScreen.cs, starting around L2180. It reads `_replay.Advance(_playheadMs)` and switches on BattleEventKind (enum in src/IdleXIdle.Core/Expeditions/WaveModel.cs:375): Strike, EnemyStrike, Down, Heal, EnemyDown, Skill, Charge, Aura, Beat, ShieldGained/Absorbed/Broken, Undying, Break, DefenceNow, AttackBreak, Slowed, Staggered, Marked, DeadweightStored/Released.

- Skill event (HuntScreen.cs:2345-2391). A non-performed, non-reaction-recipe cast does four things.
  * It calls `PlaySkillVfx(castDef, castSk.Source, castTarget)` at L2380. castTarget is the first Strike slot in the same batch.
  * It plays `Sound?.Play("sfx_cast", 0.42f, vary 0.06)` at L2382.
  * A Reaction without a recipe also gets `sfx_hit` at 0.40 with pitch +0.25 (L2387).
  * Callout `Say(text, colour)` comes from CalloutFor(Style), gated by the ShowSkillCallouts setting.
  * Strikes at the same ms are graded "skill"; a reaction's are graded "reaction" and printed under the skill's name.
- PlaySkillVfx (L2595-2617) picks a profile with `VfxProfiles.ForSkill(def)` and a subject from the profile (Champion, EnemyRow or Creature(target)). A ToTarget profile also travels to the creature. The tint is `SourceGlow(source)` (L2631: Body E84A5E, Mind 5AC8E8, Nature 7FCB4A, Machine E88E3C, Shadow 9B7BFF, Spirit E6E0FF). The asset is `FxFor(def)`.
- FxFor (L7192-7199) returns `fx_{Character.Id}_{fxKey}_strip8_512` if the asset exists, else the shared `fx_{fxKey}`. Short aliases fx_strike, fx_aura and so on map to *_strip8_512 in AssetLibrary.cs:263-286.
- VfxProfiles.ForSkill (src/IdleXIdle.Game/Vfx/VfxProfiles.cs:249-262) checks the FxKey override first; the only entry is "weep" → CastRain. Otherwise it goes by ClipKey:
  * projectile → CastProjectile (Champion, 0.28h, OffsetX 0.42, Forward, ToTarget, 8 fps)
  * aura → CastAura (Champion, 1.05h, 12 fps)
  * trap → CastTrap (EnemyRow, Standing, 0.80 of row WIDTH, GroundUnder)
  * mark → CastMark (Creature, Head, 0.45h, Overhead, OffsetY -0.10)
  * transformation → CastTransformation (Champion, 1.05h)
  * everything else → CastStrike (Creature, Center, 0.55h, 12 fps)
  * CastRain is Creature/Head, 0.90h, Overhead.
- Strike event (L2219-2331).
  * A champion swing (FromSkill=false) sets `_champLunge = 1` (40 px lunge, L480 and L3608) and plays sfx_hit at 0.38. A skill blow plays sfx_hit at 0.22. Aura ticks are silent.
  * Numbers are summed per creature per ms ("-N ×k").
  * Crits play sfx_crit 0.46.
  * The target gets the hit flash `_hitFlash` with UsualFlash = BitePresentation.UsualFlash (L7266).
  * Every OTHER blow plays `ImpactWeak` (fx_weakhit, 0.32h, tinted Steel) (L2327-2330). It is suppressed for aura ticks and for performed or reaction hits.
- Aura event (L2215) only marks auraAtMs. The tick's blows print ONE summed number (FlushAuraTotal), with no puff, sound or flash.
- Field skills with no field or mark recipe:
  * They are drawn as ONE held loop: `HoldAura()` (L7376-7390) → `_vfx.Hold(VfxProfiles.FieldAura, FxFor(_auraFxKey ?? "aura"), Champion, colour*level)`.
  * FieldAura (VfxProfiles.cs:215) is Standing, 1.10h, BehindSubject, Pinned, Held.
  * Brightness = AuraRest 0.38 + spike to AuraPeak 1.0 decaying over 0.42 s, cubed. The spike runs on its OWN 1 s clock (`PulseAuraOnClock`, AuraPulseSeconds=1, L7316/7344), NOT on the Aura events, so the picture is not synced to the damage tick.
  * Only the FIRST non-recipe Field gets the held aura (FieldRoles.Choose `held`, MarkRecipe.cs:409-427). A second non-recipe field is invisible.
  * The colour is SourceColor[heldSk.Source] (L1516-1517).
- Heal event → HealColumn (fx_heal, Standing 0.95h, Verdant) plus a "+N" callout and no sound (L2393-2400). Undying → ShieldUndying in Gold. Down → sfx_champ_down plus DeathChampion. EnemyDown → PresentEnemyDown (L6800): sfx_enemy_down 0.36 or sfx_boss_down 0.46, DeathBossBurst (fx_crit) on a boss, DeathCreature (fx_death, 0.45 s delay).
- Shield events (L2432-2477): sfx_shield_gain 0.40 + ShieldGain, sfx_shield_hit 0.26 + ShieldAbsorb, sfx_shield_break 0.58 + PlayShieldBreak (fx_shield_break). There is a standing barrier `_vfx.Hold(ShieldBarrier, FxFor("shield"))` (L7430).
- EnemyStrike: sfx_hit at 0.30 pitched -0.25, plus HunterHit number. No VFX (ADR-012).
- Champion clips:
  * A swing plays "attack". An Active cast plays its ClipKey through `Character.StripKeys(clip)` (src/IdleXIdle.Core/Characters/Character.cs:186-203): the own strip, then the generic (strike→attack; projectile/mark/transformation/aura→cast; trap→none).
  * "aura" has NO per-character strip on disk, so PULSE always plays "cast".
  * A Reaction WITHOUT a reaction recipe commits a plain "trap" clip after the bite (HuntScreen.cs:6452-6467, TrapClipGraceMs 380), whatever its ClipKey. WEEP, BACKDRAW, NARROWS and OATHMARK all play *_trap.
  * Field skills emit no Skill event and therefore no clip.
  * Plain clips use 8 equal frames at ChampionFps 8 (ClipMs 1000), contact on frame 5 (`ActionClipTiming.Plain`/`Uniform`), sped up to fit the beat (ClipShareOfBeat 0.55, SkillClipShareOfBeat 0.90, settle ≤300 ms).
- VfxPlayer (src/IdleXIdle.Game/VfxPlayer.cs) behaviour:
  * Play/Hold/Spawn (L259-351) use a profile-declared frame count of 8 and always slice 4096x512 strips.
  * The fade is the last 35 %, cubed (L123-136).
  * DriftOf eases travel out (L525).
  * Effects are drawn in two premultiplied-additive passes, DrawUnder and DrawOver (L435-519, `VfxBlend.PremultipliedAdditive`, `VfxBlend.Light(tint*fade)`, ADR-009).
  * A travelling strip with a `ProjectileLooks.For(key)` entry is drawn as a composite. The only entry is fx_seeker_projectile_strip8_512 → SeekerKnife (Vfx/ProjectileLook.cs:211-214).
  * BeginLight/EndLight (L584-595) is used by the recipe layers.
  * A missing asset is a silent no-op.

CURRENT LEGACY MAPPING (from SkillCatalogue FxKey/ClipKey/Kind; per-skill Core semantics are for the per-skill rows):
- hammer_blow (Active, strike/strike): CastStrike on the struck creature with fx_{char}_strike; clip *_strike; sfx_cast plus sfx_hit 0.22 per blow.
- snare_repay (Active, trap/trap): CastTrap under the row with fx_{char}_trap; clip *_trap.
- sign_call (Active, mark/mark): CastMark over the creature's head with fx_{char}_mark; clip *_mark.
- volley_weep (Reaction, projectile/weep): CastRain with fx_weep (no per-char strip); clip *_trap after the bite; sfx_cast plus pitched-up sfx_hit.
- field_pulse (Active, aura/aura): CastAura on the champion with fx_aura (no per-char aura strip); clip falls back to *_cast.
- field_mire (Field, aura): held FieldAura with fx_aura.
- drain_drink (Active, transformation/transformation): CastTransformation on the champion with fx_{char}_transformation; clip *_transformation; heals use HealColumn.
- drain_wilt (Field, wilt): held fx_wilt.
- Signatures:
  * sig_anvil_hardface (Active, strike): fx_anvil_strike
  * sig_chorus_grave_song (Field, aura): held fx_aura
  * sig_metronome_clockwork (Active, projectile): CastProjectile flying fx_metronome_projectile as a strip
  * sig_unbroken_hold_fast (Field, FxKey trap): held FieldAura with fx_unbroken_trap, a ground burst drawn as a 1.10h halo
  * sig_tower_slow_fall (Field, FxKey strike): held FieldAura with fx_tower_strike
  * sig_quiver_backdraw (Reaction, projectile): CastProjectile with fx_quiver_projectile; clip *_trap
  * sig_thornwall_narrows (Reaction, trap): CastTrap with fx_thornwall_trap
  * sig_oathbound_oathmark (Reaction, mark): CastMark with fx_oathbound_mark
  * sig_magpie_paying_work (Active, transformation): fx_magpie_transformation
- The five closed recipes are keyed to "seeker" ONLY.
  * PRESS on any other champion is the held FieldAura with fx_press.
  * BRAND elsewhere is the held aura with fx_{char}_mark or fx_mark.
  * SPRAY elsewhere is CastProjectile with fx_{char}_projectile.
  * JAWS elsewhere is CastTrap with fx_{char}_trap plus the trap clip.
  * HARD HANDS is seeker-only.

=== ALPHA AUDIT (measured with python numpy+PIL from the PNGs) ===
Scripts are in the scratchpad (alpha_audit.py and alpha_audit2.py, under C:/Users/sanan/AppData/Local/Temp/claude/.../scratchpad). I also ran tools/check_fx_edges.py: "57 of 67 VFX strips end on a rectangle, cannot fade, or cannot be seen". All strips are 4096x512 RGBA in 8 frames. No file has an opaque background: every corner alpha is 0 and no strip has a filled frame box. The dominant defect is ONE-BIT alpha, which means hard stamped edges.

- A) The 50 per-character strips fx_{anvil,chorus,magpie,metronome,oathbound,quiver,thornwall,tower,unbroken}_{mark,projectile,strike,transformation,trap}, plus fx_seeker_projectile and fx_seeker_transformation.
  * Findings:
    - Each has exactly 2 alpha values (0/255) and 0 % partial alpha.
    - They are greyscale (saturation 0), with opaque fill luminance 96-255, i.e. flat grey stamps.
    - Thousands of 0→255 steps per frame.
    - Mostly CONSTANT coverage across frames, meaning loops played as one-shots with no peak.
  * Specific faults:
    - seeker_transformation: 26 % of the border ring at 255. It is cut flat by its canvas and has a 76 % straight bbox edge.
    - metronome_mark: border 255 and content crossing frame seams (5 % seam), which bleeds into the neighbour frame.
    - quiver_projectile: border 255.
    - tower_mark (63 %) and tower_strike (64 %): straight solid bbox edges, read as boxes.
    - Barely animated (mean frame delta under 4/255): magpie_trap (1.1 % coverage, nearly invisible), unbroken_transformation, tower_transformation, magpie_projectile, anvil_trap, anvil_projectile, anvil_transformation, tower_strike.
    - LOOP resets or light swings (fx_energy warnings): anvil_mark, magpie_mark, quiver_mark, unbroken_mark.
    - Last-frame POP: magpie_mark, oathbound_mark, quiver_projectile, unbroken_mark, unbroken_strike.
  * VERDICT: REPLACE all of them. A one-bit stamp cannot be repaired by feathering, and they are the art every signature and every non-seeker shared skill uses today.
- B) Shared legacy strips:
  * fx_strike, fx_mark, fx_hit, fx_weakhit: one-bit or ~0-1 % partial.
    - fx_weakhit has 13.8 % of opaque pixels near-black, which add nothing under additive.
    - fx_weakhit and fx_hit are dark (mean luminance 106 for hit).
    - VERDICT: REPLACE. fx_weakhit is the every-other-blow puff for all basic attacks. fx_hit is no longer spawned by any profile; it remains only as an alias.
  * fx_trap (3.2 % partial), fx_transformation (2.2 %), fx_crit (2.0 %): pass SOFT barely, decay correctly (ONE PEAK), no edge hits, straight alpha with RGB/A of about 5. VERDICT: REPAIR at most (soften pass) or replace for quality.
  * fx_death (0.64 %), fx_heal (1.05 %), fx_projectile (0.57 %, FLIGHT warning 2.1x light jump at frame 0→1), fx_shield_break (0.02 %): fail SOFT. VERDICT: REPAIR (feather/soften, fxclips post-pass) or REPLACE. fx_projectile is a FLIGHT replace candidate.
  * fx_aura (91 % partial), fx_press (80 %), fx_shield (100 %), fx_seeker_mark (99.8 %), fx_seeker_strike (100 %), fx_seeker_trap (100 %): clean soft alpha, straight (RGB/A of 1.7-2.9), no border. VERDICT: GOOD technically. fx_aura is the held field for MIRE, GRAVE SONG and PULSE.
  * fx_wilt and fx_weep: DOUBLE-PREMULTIPLICATION BUG. Every partial pixel stores RGB == A (sampled: wilt [100,100,100,100], weep [245,245,245,245]; luminance/alpha correlation 1.00). AssetLibrary premultiplies at load (AssetLibrary.cs:124/176/400/482-490), so on screen RGB becomes A²/255 and soft edges come out dark and dim.
    - Fix wilt: set RGB=255 where A>0, a deterministic file fix → REPAIR.
    - Fix weep: it also has alpha 255 on the frame border (0.7 % of the ring), only 21 alpha levels and 2.4 % coverage → REPLACE, or at minimum REPAIR both faults.
  * fx_bind_chain: used only by LoadoutScreen.cs:2426 (alpha-blended, not combat). It is one-bit with 27.6 % near-black opaque pixels. Out of combat scope.
- C) Parts (assets/art/VFX/parts):
  * All of these are soft and good: fxp_flash_soft, fxp_ring_soft, fxp_spark_dot, fxp_glint_star, fxp_trail_soft, fxp_shard_sliver, fxp_seeker_knife_head, and the seeker_bite_*/press_* parts.
  * fxp_trail_soft has 25 % border at 253 by design (a stretched trail), which is fine.
  * fxp_shard_sliver touches its border (3.8 % at 40) and is a tiny part, so OK.
  * press_clamp/wave/fold are premultiplied-like (RGB<=A in 91-94 % of pixels) with luminance-alpha correlation 0.97. They are the accepted pixel-hard PRESS parts; do not touch.
  * The generic parts (flash_soft, ring_soft, spark_dot, glint_star, shard_sliver, trail_soft) are reusable for new composite recipes (MeleeImpactLook defaults, ActionRecipe.cs:134-163).

=== 2. RECIPE SYSTEMS (src/IdleXIdle.Game/Presentation) ===
All lookups are gated by `ActionRecipes.Enabled` (RH_ACTION_RECIPES=0 turns them all off) plus per-family switches: RH_REACTION_RECIPES, RH_FIELD_RECIPES, RH_MARK_RECIPES.

- ActionRecipes (ActionRecipe.cs:229-297)
  * Lookup: `For(characterId, skillId, clipKey, effectKey)`. It tries 1) BySkill[(char, SkillDef.Id)], then 2) ByForm[(char, clipKey)] if the recipe's EffectKeys is null or contains def.FxKey, then 3) null (legacy).
  * Current entries:
    - BySkill: ("seeker","sig_seeker_hard_hands") → SeekerHardHands (MeleeActionRecipe, ClipKey "hard_hands")
    - ByForm: ("seeker","projectile") → SeekerSpray (ProjectileActionRecipe, EffectKeys {"projectile"}, so WEEP is excluded)
  * Interface IActionRecipe (L18-59) fields:
    - Id, ClipKey, EffectKeys, ReleaseMarker, HandSocket, TravelMs
    - Release/Contact/ContactTick cues, volumes and pitches; ContactTicks, ContactTickSpacingMs, PanWidth
    - DuckOthers, DuckTailMs, Weight (Quiet/Ordinary/Skill/Signature/Major)
    - TargetFlash, TargetFlashMs, TargetFlashRise, ReplacesGenericHit, CalloutAtRelease
  * Two concrete performance classes:
    - ProjectileActionRecipe → ActionPerformance (ActionPerformance.cs). It throws a prop (PropKey/PropEdgeKey/PropPivot, PropBundle fan, PropFanDegrees, FanBulge, Departure), flies it with a composite ProjectileLook (head, trail, glint, spark, shard, flash), voices release and contact plus contact ticks, has smear settings, and puts one blade per struck enemy (ActionTargets.StruckBy).
    - MeleeActionRecipe → MeleePerformance (MeleePerformance.cs). It has commit/contact/recovery markers, lunge root motion (LungeBack/Overshoot/Accel, ReturnShare, MinReturnMs, a hop), speed lines, and a MeleeImpactLook (flash, ring, shards, chips, sparks, gravity).
    - Any other recipe type throws (HuntScreen.cs:6680-6685).
  * How a recipe is performed:
    - TryAuthored (HuntScreen.cs:6697-6710) requires a recipe AND the recipe's own strip (`Character.StripKeys(r.ClipKey)`) AND a `*.clip.json` with the ReleaseMarker. Otherwise the skill stays legacy.
    - TryCommitPerformance → ActionPerformance.Schedule puts contact on the beat (release = beat − TravelMs).
    - ActionHandoff fits recovery; PlanHandoff runs at L6620-6650.
    - The duck is applied every frame (L6794).
    - Voice is at L7005-7040.
  * Generic hits give way to the performance when ReplacesGenericHit is set.
- ReactionRecipes (ReactionRecipe.cs:442-477)
  * BySkill only (no shared tier). Today: ("seeker","snare_jaws") → SeekerJaws.
  * ReactionRecipe (L51-438) is a frontal bite: upper and lower fang parts with cells, mist, flash, ring, streaks, shards, timing (Appear/WindUp/Close/Snap 316.6 ms), colours, SnapCues/Volume 0.42, Callout, TargetFlash 0 and NumberDelayMs 40.
  * ReactionPerformance (562 lines) is JAWS-specific in shape.
  * SpawnReaction is at HuntScreen.cs:6939. The number, death and flash are deferred to the snap through _reactionEchoes and _deathDeferred.
  * A presented reaction never takes the figure (no trap clip; L6458).
- FieldRecipes (FieldRecipe.cs:374-394)
  * ("seeker","hammer_press") → SeekerPress.
  * FieldRecipe holds field/wave/fold/clamp part keys and cells, contract/launch/spring/travel/crush timing, colours, Yield/Quiet windows, TickCues/TickVolume 0.28 and the Cue* timing.
  * FieldPerformance is built per wave from Aura events for the field's slot plus the Break target (HuntScreen.cs:1521-1557). It has DrawUnder, DrawMaterial and DrawLight plus a Squash buckle on creatures, and VoiceField is at L7042.
- MarkRecipes (MarkRecipe.cs:380-406)
  * ("seeker","sign_brand") → SeekerBrand.
  * MarkPerformance is built from Aura ticks + Marked percent + EnemyDown falls + AmplifyWholeWave (L1566-1647).
  * It is drawn as a Curse (Presentation/Curse/*, one shader pass per afflicted creature, baked CurseHostData), and MarkVoice schedules cues.
- FieldRoles.Choose (MarkRecipe.cs:409-427) returns (Performed, Mark, Held) slots: the first Field with a field recipe, the first with a mark recipe, and the first with neither.
- HOW TO ADD A NEW SKILL RECIPE
  * Add a recipe instance and a BySkill entry keyed by (champion id, SkillDef.Id). Use the ByForm tier only for a deliberately shared Form recipe, and then also set EffectKeys.
  * An action recipe needs its own strip `char_{id}_{clip}_strip8_512.png` plus `.clip.json` under assets/art/Animations/Roster/{id}_{clip}/. The JSON needs frameMs[8], elastic[8], markers (anticipation, commit, release|contact, recovery, settle) and per-frame sockets {"frame": {"ThrowHand"|"StrikeHand": [x,y,deg]}}. This is parsed by ActionClipTiming.Parse (ActionClipTiming.cs) and loaded once by ActionClipLibrary (it scans assets/art for *.clip.json).
  * New fxp_ parts go in assets/art/VFX/parts (keyed by filename).
  * A recipe of a new KIND (for example a heal/self-buff, a projectile that rains down, a delayed fall, or a shield field) needs a new performance class and a new switch arm. Today's types cover: thrown projectile, melee lunge, frontal reaction bite, a field pressure front with crush, and a curse on a body.
  * Every recipe is per champion. Sharing PRESS or JAWS across champions means adding (char, skill) entries, or a champion-agnostic tier that does not exist today.
  * Test pins to mirror:
    - only_listed_actions_are_performed (action_presentation_test.cs:326)
    - press_is_the_seekers_field_recipe_and_only_press (press_field_test.cs:46)
    - the_jaws_recipe_belongs_to_the_skill_and_repay_keeps_its_own (jaws_reaction_test.cs:123)
    - a_signature_action_belongs_to_its_skill (melee_action_test.cs:221)

=== 3. CHAMPIONS ===
- Roster (src/IdleXIdle.Core/Characters/CharacterRoster.cs:44-220), id → signature:
  * seeker → sig_seeker_hard_hands (the starter)
  * anvil → sig_anvil_hardface
  * chorus → sig_chorus_grave_song
  * metronome → sig_metronome_clockwork
  * unbroken → sig_unbroken_hold_fast
  * tower → sig_tower_slow_fall
  * quiver → sig_quiver_backdraw
  * thornwall → sig_thornwall_narrows
  * oathbound → sig_oathbound_oathmark
  * magpie → sig_magpie_paying_work
- Strips (assets/art/Animations/Roster/{id}_{clip}/char_{id}_{clip}_strip8_512.png, 8 frames of 512):
  * Every champion has attack, cast, death, idle, mark, projectile, strike, transformation and trap.
  * The seeker also has hard_hands.
  * There is NO "aura" strip for anyone, so it falls back to cast.
- Only 2 timing files exist: seeker_projectile (markers anticipation1, commit2, release3, recovery6, settle7; ThrowHand sockets on frames 1-3) and seeker_hard_hands (commit3, contact4, recovery6, settle7; StrikeHand sockets on frames 3-5). Every other clip plays the uniform plain timing.
- Sockets map from a strip frame to the arena through ActorSocketMap.ToArena (ActorSocketMap.cs), using the same Src/Dest the draw used (UiKit.ResolveFrame through IActionStage.TryActorFrame, HuntScreen.cs:7098-7111).
- Props exist only for the seeker (assets/art/Props: prop_seeker_throwing_knife(_edge), chain, jaws).
- Silhouette widths differ 2.3x (OATHBOUND 162 px to QUIVER 373 px) while heights match within 4 px. The VFX contract is height-based (VfxProfiles.cs:21-27).

=== 4. CAPTURE / SHOWCASE RIG ===
- Entry points:
  * RH_SHOT=<png> (Game1.cs:938, 8533) writes frame 60 then exits.
  * RH_SHOT_SEQ="count,stride[,start|@N]" (L8544-8571) writes a filmstrip <path>_NN.png. ResetElapsedTime makes the stride exact. @N starts 1 s before the first performed cast that strikes N or more.
  * RH_SHOT_MODE picks the fixture (fight, fightinspect, fightmulti, fightshield(broken), fightstatus, vfxdebug, and others; L2425-2431, 3100-3215).
- Default fight fixture (L2830-2850): the seeker with HARD HANDS (signature) + volley_spray@Mind + hammer_press@Body + snare_jaws@Shadow, every SkillRoad node taken, mastery earned 9999.
- Dials:
  * RH_SHOT_HUNTER=<id> (L2451): Restore + SignatureSkillId + ShedForeignSignatures + EnsureSignature.
  * RH_SHOT_SWAP=<fixtureSkill>:<newSkill>[@Source],... (L2899-2919): loudly refused if the build rule refuses it.
  * RH_SHOT_VARIATION=<skill>:<VAR>[+<REINF>...] (L2920-2937): earned through RecordWave and ChooseVariation/TakeReinforcement.
  * RH_SHOT_TAKE=<mastery nodes> (L2943).
  * RH_SHOT_TRAIN=<Stat>:<ranks>.
  * RH_SHOT_SOURCE=<Source>, the region family (L2974).
  * RH_SHOT_ARCHETYPE=<Archetype> (L3005).
  * RH_SHOT_BOSS=<art key>, one of six bosses (L2998).
  * RH_SHOT_CREATURES=<n>, presentation only (L2989).
  * RH_SHOT_ENEMY=<health>,<bite> (L7636), pinned through _shotEnemyBaseline because the host restarts a DevStart run on its first live frame.
  * RH_SHOT_SEED=<n> seeds the descent RNG and the crits (HuntScreen.cs:7960).
  * RH_SHOT_T=<seconds> seeks (DevSeek; earlier beats land silently).
  * RH_SHOT_BITE=1 / RH_SHOT_LEAD seek just before a bite or event (DevSeekBefore(predicate, lead)).
  * RH_SHOT_DUMP writes .events.txt, .actors.txt and .assets.txt.
  * Others: RH_SHOT_NOVFX, NOCHAMP, NOSTRIP, SOCKETS, SHIELDFX, SWING, NORECOIL, NOROOT.
  * RH_SHOT_SHOWCASE=1 only turns guidance off for store screenshots (Game1.cs:8706). There is NO multi-skill showcase mode.
- Tools (tools/asset-pipeline):
  * capture.sh takes one shot.
  * capture_seq.sh [mode count stride out start] builds, runs with RH_ENV, and calls v2/filmstrip.py. RH_SEQ_LOG keeps stdout.
  * films_brand.sh / films_press.sh / foundation_films.sh are per-reference take scripts. Their pattern is `film name swap T count stride start ENV...` with RH_SHOT_SEED=7 and RH_PRESENT_TRACE=1, plus a DISK GUARD (refuse if build/shots holds over 2 GB or less than 20 GB is free; take names are required).
  * film_audio.py <log> <prefix> <out.mp4> [--slow --mute --music --crop --wav --cue] renders the soundtrack from the trace's `sound` lines (key, vol, pitch, pan, duck) aligned to `shot N`, with the real wavs, and muxes it with ffmpeg.
    - It reproduces the throttle on the trace clock: MIN_GAP_MS and UNTHROTTLED (film_audio.py:43-49) are a hand-kept MIRROR of SoundBank.MinGapMs (SoundBank.cs:120-136) and MarkRecipe.Unthrottled.
    - It does not reproduce the per-play `vary` or the voice limit.
- SOUND IS OFF UNDER THE RIG: `new SoundBank(disable: RH_SHOT is set)` (Game1.cs:1969). Keys are still logged before the enabled check (SoundBank.cs:167), so "real audio" in films means the trace render.
- Determinism: fixed-step updates, SoundBank's `vary` Random(0x5EED), and RH_SHOT_SEED for crits. The SoundBank throttle in the LIVE game uses Environment.TickCount64 (wall clock), so a live run's throttling is not frame-deterministic.
- MISSING for a deterministic multi-skill SHOWCASE:
  * a) A scripted segment list in ONE process: hunter, build swaps, variations, enemy baseline, source/archetype/boss/creature count, seed, the event to seek, and duration. Today every dial is applied once at fixture time before DevStart, and the run then plays one expedition.
  * b) A host hook to re-pose between segments: re-run the hunter Restore plus loadout repair, SetSkill/SetSource, ChooseVariation, then DevStart with a new baseline and seed, and clear _vfx, _performance, _field, _mark and _curse. The asset warm-up (AssetLibrary deferred loads, MarkPoints.Warm, CurseHostData.Warm) must happen in Update.
  * c) A seek-to-event per segment, generalising DevSeekBefore / `@N`, e.g. "first Skill event of slot X" or "first Aura of slot X".
  * d) Segment titles or captions, plus a trace marker for each segment so film_audio and filmstrip can cut it.
  * e) Real-time audio, either by allowing SoundBank in a showcase mode (not RH_SHOT) with throttling on the game clock, or by keeping the trace-render path and moving the throttle table to one shared source (generated or tested against SoundBank) instead of the hand mirror.
  * f) A per-skill "pose" registry: which fixture slot to swap, which enemies, and which variation shows the skill best. The per-skill rows' showcase_needs would feed it.
  * g) The disk guard and frame cleanup built into the generic film script.

=== 5. AUDIO ===
- Combat files (assets/audio/combat):
  * Generic: sfx_boss, sfx_boss_down, sfx_cast, sfx_champ_down, sfx_crit, sfx_enemy_down, sfx_hit, sfx_shield_break.
  * Accepted seeker cues: sfx_seeker_spray_release, sfx_seeker_spray_hit, sfx_seeker_spray_tick, sfx_seeker_hard_hands_commit, sfx_seeker_hard_hands_hit, sfx_seeker_jaws_bite, sfx_seeker_press_tick, and seven sfx_seeker_brand_* (apply, deepen, deepen_deep, infect, leave, awaken, ash). Each is pinned by SHA-256 in its test.
- UI files (assets/audio/ui) include sfx_shield_gain, sfx_shield_hit and sfx_trait_lit, all used in HUNT. Music is in assets/audio/music, with music_arena_<source>.
- The 17 remaining skills use ONLY generic cues:
  * sfx_cast 0.42 on a cast
  * sfx_hit 0.22 per skill blow
  * reactions add sfx_hit 0.40 at pitch +0.25
  * crits sfx_crit 0.46
  * fields are silent (aura ticks make no sound)
  * heals are silent
  * HOLD FAST's shield uses the sfx_shield_* family through the shield events
  * the skill-ready ping is sfx_trait_lit 0.16 for Beats>2 (L2127)
- Fallback cue chains already reference non-existent archetype cues: sfx_throw_release, sfx_blade_hit, sfx_blade_tick, sfx_fist_commit, sfx_fist_hit. SoundBank.Resolve walks the chain.
- The basic attack uses sfx_hit 0.38. An enemy bite is sfx_hit 0.30 at pitch -0.25.
- Volume hierarchy (all × SfxVolume master 0.8):
  * shield_break 0.58, champ_down 0.62, boss 0.58
  * HARD HANDS hit 0.55 / commit 0.34
  * SPRAY hit 0.50 / release 0.40 / tick 0.20
  * crit 0.46, boss_down 0.46, JAWS bite 0.42, cast 0.42, shield_gain 0.40, sfx_hit swing 0.38, enemy_down 0.36, enemy-bite thud 0.30
  * PRESS tick 0.28 (×0.6 quiet, ×0.5 when yielding to JAWS, CueStartMs -20, never lead)
  * shield_hit 0.26, skill blow 0.22
  * BRAND 0.092-0.183 (apply 0.183, deepen 0.163, deepen_deep 0.173, awaken 0.163, leave 0.145, infect 0.116, ash 0.092)
  * trait_lit 0.16
- Mix controls:
  * Duck: SoundBank.Duck is reset to 1 each frame (Game1.cs:2331) and set from the performance's DuckAt (HuntScreen.cs:6794; DuckOthers 0.45 from release to the contact ring + DuckTailMs 160). `lead:true` bypasses the duck.
  * Throttle: MinRepeatMs 90; per-key gaps sfx_hit 60, enemy_down 140, boss_down 220, champ_down 300, shield_hit 60, shield_break 300; repeat attenuation 1/sqrt(recent) with a 250 ms half-life. Unthrottled cues: the SPRAY tick, BRAND infect and ash.
  * Pan(x, width): ±width across the arena (L7094).
- Cue provenance convention: new cues are built from CC0 recorded foley matched to an owner-named reference. Synthesis was rejected. Candidates go in tools/asset-pipeline/audio_history/, and the approved cue is pinned by SHA-256.

=== 6. PERF INSTRUMENTATION ===
- RH_PRESENT_TRACE=1: `present\t<clockMs>\t<playheadMs>\t<kind>\t<detail>` lines from PresentTrace.cs. Kinds include:
  * event, vfx-spawn, proj-head/contact/residue/strip, flash, sound, clip-start/end, handoff, yield, shot, field-wave, mark-wave
  * perf-draw (sprites=, draws=, released=)
  * reaction-draw (alive, sprites, draws upper bound, alloc=)
  * field-draw (u, target, sprites, squash, shape, body, alloc=)
  * mark-draw (hosts, stage, front, depth, sprites, alloc=, voice=, draws=, batches=, ticks=, flush=)
  * The measurements are GC.GetAllocatedBytesForCurrentThread and GraphicsDevice.Metrics.DrawCount (HuntScreen.cs:2950-3048, 3770, 6909, 6974, 7067-7088).
- RH_VFX_METRICS=1: one vfx-metrics line per frame (flights, landed, sprites, trailSamples, passDraws, compositeBytes) (VfxPlayer.cs:159-170, 511-518).
- RH_VFX_DUMP=1 dumps each effect's resolved frame, content, ratio and OVER/ok (VfxPlayer.cs:218, 621-637). RH_VFX_BUDGET exists. RH_VFX_COMPOSITE=0 is the strip fallback.
- RH_ASSET_TRACE and tools/asset-pipeline/asset_usage.sh answer "is this texture used".
- RH_UI_BUDGET and RH_UI_TEXT are raster and truncation ledgers.
- KNOWN ALLOCATION GAP on the generic path:
  * VfxPlayer.DrawPass builds `_held.Values.Concat(_active).Where(...).OrderBy(...).ToList()` on every pass that has effects (L441-444) plus `_landed.Any` (L445).
  * Update calls `_held.Keys.ToList()` every frame (L395).
  * Spawn allocates an Anim per Play.
  * The recipe layers are zero-alloc tested; the legacy VFX path is not.
- Batch budget: each VfxPlayer pass is up to two extra Begin/End pairs, and BeginLight adds one more.

=== 7. TEST CONVENTIONS FOR PRESENTATION ===
- Location and naming: tests/unit/IdleXIdle.Game.Tests/<system>_<feature>_test.cs with `test_<scenario>_<expected>` methods (xUnit). dotnet test output is Turkish (grep Başarılı/Başarısız).
- Recipe isolation pins:
  * only_listed_actions_are_performed
  * <skill>_is_the_seekers_<kind>_recipe_and_only_<skill>
  * a recipe belongs to its SKILL, not its Form (REPAY keeps its own; BLOW is not HARD HANDS)
- ZERO-ALLOC tests: measure GC.GetAllocatedBytesForCurrentThread across a simulated playhead loop and assert 0.
  * press_field_test.cs:270 test_the_field_allocates_nothing_frame_to_frame
  * jaws_reaction_test.cs:646
  * brand_audio_test.cs:538
- TEXT PINS: the test reads source text (File.ReadAllText of HuntScreen.cs or the performance file) and asserts Contains / DoesNotContain on exact call lines, to prove wiring and draw-pass order. See press_field_test.cs:293-344 and jaws :519, :712.
- AUDIO PINS: the shipped cue's SHA-256 is pinned (press_field_test.cs:451; brand_audio_test.cs:618), and the tests also check:
  * no rejected candidate ships under assets/audio
  * there is no candidate selector
  * the cue order and volume hierarchy (brand_audio_test.cs:381 "every brand cue is quieter than press, jaws and the action contacts")
- Timing tests assert contact on the beat, number delay after the snap, and the duck window (action_presentation_test.cs:177, 237).
- VFX contract tests (vfx_contract_test.cs):
  * every profile is named by a live spawn site
  * every skill resolves to a profile
  * the WEEP override
  * every field skill's art is reachable
  * no spawn site computes a pixel
  * the effects pass stays unscissored
- Other related tests: vfx_blend_contract_test (premultiplied additive), vfx_shield_test (measured content boxes), and actor_clips_test (the strip fallback order).
- LIVENESS: Core liveness tests at two depths (variation_liveness_test, reinforcement_liveness_test, TriggerLivenessTests) prove a dial changes the fight. A presentation liveness check, i.e. whether the new recipe actually executes in the posed film, is shown by the trace lines (clip-start ... recipe=, field-wave, mark-wave).
- Asset gates: tools/check_fx_edges.py (EDGE, SOFT and LIVE rules; NOT in check_all.sh because the library fails it), tools/fx_energy.py (temporal ONE PEAK / FLIGHT / SETTLE / LOOP), tools/check_asset_keys.py, and tools/check_asset_consumers.py.
- The regeneration pipeline is tools/asset-pipeline/v2/fxclips.py: whiten → glow → soften → feather, with spec.json prompt rules. The memory notes say projectiles are composed from parts, never generated as motion.

KEY RISKS FOR THE SWEEP
- All 50 per-character effect strips and the shared strike/mark/hit/weakhit strips are one-bit stamps and need replacing. Recipes should compose from soft parts the way the accepted references do.
- fx_wilt and fx_weep are double-premultiplied (a cheap repair), and weep is also border-cut.
- Held fields pulse on a 1 s wall clock, not on their Aura tick, and only one non-recipe field is visible.
- Every recipe is seeker-only. The other nine champions have no clip.json, no sockets and no props, so a performed action on them needs authored timing and sockets first.
- No showcase sequencer exists, and the capture rig is silent: audio review goes through the trace and film_audio.py, whose throttle table is hand-mirrored from SoundBank.
