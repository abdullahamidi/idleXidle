# SYSTEMS REFACTOR — REPORT (brief §117, 47 items)

Status: COMPLETE for phases 1-9, except where an item says otherwise in its own words. Every claim
below is either a file you can open or a number that was measured; where something did NOT get done it
says so, and says why, rather than going quiet.

Commits are on `feat/hunter-cutout-rig`. The plan and its eleven decisions are in `PLAN.md`; what the
code actually was before any of this is in `AUDIT.md`.

## Signature Skills

1. **The final signature for every character** — ten authored skills, one per champion, in
   `SkillCatalogue`. THE SEEKER: HARD HANDS. THE ANVIL: HARDFACE. THE CHORUS: GRAVE SONG. THE
   METRONOME: CLOCKWORK. THE UNBROKEN: HOLD FAST. THE FALLING TOWER: SLOW FALL. THE QUIVER: BACKDRAW.
   THE THORNWALL: NARROWS. THE OATHBOUND: OATHMARK. THE MAGPIE: PAYING WORK. Commit `3b7a841`.
2. **Style and Kind** — Hammer ×3 (HARD HANDS Active, HARDFACE Active, SLOW FALL Field), Snare ×2
   (HOLD FAST Field, NARROWS Reaction), Volley ×2 (CLOCKWORK Active, BACKDRAW Reaction), Field ×1
   (GRAVE SONG), Sign ×1 (OATHMARK Reaction), Drain ×1 (PAYING WORK Active). Four Actives, three
   Fields, three Reactions — deliberately spread after a judge found one panel had put four Reactions
   in ten, which made "something happens when you are bitten" the house identity instead of a
   character's.
3. **Relationship with the Innate** — the interaction is the design, and a signature that restated its
   innate was rejected (§5). The clearest case is THE SEEKER: EVEN HAND is a flat multiplier read one
   line below the gate that returns early for a basic attack, so the innate provably cannot touch the
   swing — and HARD HANDS is built entirely on the swing. The two halves partition the champion's
   damage with no overlap, by construction rather than by tuning.
4. **Ownership implementation** — `SkillDef.OwnerCharacterId`: null for the twelve shared skills, a
   character id for the ten signatures. A field on the definition rather than a naming rule or a side
   table, because ownership decides what the composer carries into a fight and so has to live where
   the composer already looks. `BuildComposer` checks it separately from access, since a set of ids
   cannot say why an id is in it. Commit `dc439a5`.
5. **Progression behaviour** — `SkillProgress` is keyed by skill id and knows nothing about who was in
   the chair, so a signature's levels survive every character switch by construction. A switch empties
   a slot holding the previous champion's signature and says so; nothing is ever converted into
   another signature. Commit `08116a9`.

## Mastery

6. **The final skill-access rule** — a shared skill is available while its road node is allocated and
   unavailable when it is not. `MasteryTree.AvailableSkills()` is a pure function of the taken set.
   The permanent latch is deleted, and so is the host line that latched the active champion's own
   skill into it every frame — the account-wide leak §9 forbids, which was already in every existing
   save. Commit `338bd5a`.
   **This required moving the skill roads off the specialisations** (PLAN D11). Only one specialisation
   may be taken at a time and every road hung off one, so a hunter could reach exactly two shared
   skills; the code said as much in its own comment. A road now hangs off its branch's first minor, so
   a first taught skill costs six points instead of fifteen and a career reaches all twelve.
7. **Respec behaviour** — it lists what it will unequip before it commits, then does it, then says
   every level is kept. The list is DERIVED: it builds the tree a respec actually leaves and asks that
   tree, so it cannot drift from the respec code. Never blocked because a skill is equipped.
8. **Loadout repair** — after a respec, exactly the affected slots are cleared. `LoadoutRepair` answers
   "may this slot fight?" in the composer's own order (ownership, then current access), so the screen
   and the fight cannot disagree.
9. **Experience persistence** — untouched by any of it. Access and experience live in different
   objects, which is what makes LAW 4 structural rather than a promise two systems must keep in step.


## Traits

10. **New architecture** — `src/IdleXIdle.Core/Traits/`, six files and no tree. `TraitDef` is
    `(Id, Name, Line, Flavour, Tag, Discovery, Shape)` where `Shape` is a
    `Func<TraitContext, SkillShape>` — the same delta shape a skill variation returns, so a trait
    reaches the simulation through a channel that already existed rather than through a new one.
    `TraitLedger` holds what the ACCOUNT has discovered and what each CHARACTER wears;
    `TraitDiscovery` + `TraitWatch` decide when something awakens; `TraitEffects` applies it.
11. **Final catalogue** — 26, pinned by `test_every_trait_in_the_catalogue_has_a_liveness_test`, which
    fails if a trait is added without one. PLAN.md records the four cut from the design's thirty and
    why. Two were re-authored on 2026-09-04 against the build-level Vow model — see item 47.
12. **Hidden discovery rules** — deterministic, account-wide, and never shown before they fire.
    `TraitDiscovery` reads a `TraitWaveFacts` struct per wave against named thresholds
    (`HighCritPercent` 35, `BrinkShare` 0.20, `WideTargets` 3, `MotleyElements` 4, and so on). An
    undiscovered trait shows as `???` with a count — a total, not a checklist, because a player who
    cannot tell "there is more here" from "this is all there is" stops looking.
13. **Three-slot per-character loadout** — `TraitCatalogue.SlotsPerCharacter = 3`, enforced in
    `TraitLedger.Equip`/`EquipInto`. Discovery is the ACCOUNT's; wearing is the CHARACTER's, which is
    the split that lets a second champion feel different without a second grind.
14. **UI redesign** — the TRAITS screen: three worn at the top, the collection as a constellation
    board below, `???` for the undiscovered, one reading panel on the right. Photographed at
    `build/shots/trait_t_kept_word.png` and `trait_t_weight_of_vows.png`.
15. **Removed old tree systems** — **PARTLY. This is the one item the refactor did not finish, and the
    honest state is below.**

    *Gone from the player's reach:* the Memory tree screen is not in the nav and `_showDustTree` is set
    only by the capture rig, so no player can open it. Nothing on it can be bought in a live game.

    *Gone from the model:* all 19 keystones and all 13 vows left it for the world
    (`KeystoneSources`, vow discovery), both keystone sockets and the vow capacity left it for
    `Unlocks`, and its two loot filters left it for the Warren. `LegacyTraitTree.Read` converts an old
    save's node list into those grants as FLOORS and UNIONS — safe to run on every load forever, which
    is what makes it testable rather than one-shot — and `SaveSystem.RestoreWarren` applies the Warren
    half.

    *Still there:* **12 nodes still carry `Mods`** — live passive stat power read by
    `DustEffects.TreeMods` inside `BuildComposer` — plus `efficient_forge` (+15 % dismantle return)
    and `recall_1..4` (+5 % mastery rate each). No legacy grant covers these three, so deleting the
    tree today would quietly take power away from every returning player who bought them.

    *Why it stopped here:* converting a legacy stat purchase into the new vocabulary is a DESIGN
    decision, not an implementation detail — there is no 1:1 sink for it, and inventing one
    unilaterally is how a refactor grows a system nobody asked for. The two transitional
    `MorePermissive` ORs in `Game1` say in their own comments that they go when the tree does.
    See item 47.

## Vows

16. **Discovery rules** — a Vow reveals itself when the build keeps its rule ONCE without having sworn
    it (`Vows.Revealed`), which is proof-before-reward: the player demonstrates the restriction, and
    the game then offers to pay for it. Account-wide, and shown as `N OF 13 VOWS FOUND`.
17. **Capacity progression** — `Unlocks.VowCapacity`: 0 below wave 5, then 1, a second at
    `SecondVowConquests` (2 regions), a third at `ThirdVowConquests` (4). **Independent of skill
    slots** — before this the cap was a side effect of `PlayerLoadout.SkillCapacity`, so a capacity
    nobody granted moved when a different system's ceiling moved.
18. **Tutorial access** — the first vow arrives with the BUILD screen at wave 5, and
    `Unlocks.NextVowNote` always names the next step, so capacity is never a rule the player is
    refused by without being told.

    **Evaluation is centralised.** A vow is validated ONCE against the whole build (`Vows.IsActive`),
    contributes ONCE however many rows record it (`Build.Vows` deduplicates), and every kept vow's
    bonus sums under one ceiling (`Vows.CombinedFactor`, `VowTuning.CombinedBonusCeiling`). The BUILD
    screen reads that same composed list, so the screen and the fight cannot disagree — including the
    vow THE KEPT WORD lends a hunter who swore nothing, which is shown as LENT rather than left as a
    mystery. A slot is still where a vow is STORED, because that is the save shape and where a player
    swears one; nothing says a vow belongs to, pays for, or is judged per slot.

## Keystones

19. **Acquisition mapping** — `KeystoneSources`, 19 sources over four world rungs: 10 on **Conquest**
    (hold wave 20 in a region), 10 **PartlyMastered**, 10 **FullyMastered**, 5 **Corruption**. The
    world teaches them; the tree no longer does.
20. **Slot progression** — `Unlocks.KeystoneSockets`, floored by what the save already earned and by
    what it is already WEARING, so a returning player is never handed fewer sockets than the keystones
    on their plate.

    **The reveal fits its presentation.** The MAP strip carries a headline only —
    `<REGION> CONQUERED!  NEW KEYSTONE — <NAME>.` — pinned at one line and 75 characters or fewer at
    UI SCALE 150 for every region the world can produce (`MapStripTests`). The full description goes to
    the notice toast, which WRAPS and whose plate height follows its text (`KeystoneNoticeTests` pins
    that every reveal body fits `NoticeBodyLines`).

## Structural unlocks

21. **Skill slots** — `Unlocks.SkillSlots`: 1, +1 with the BUILD screen at wave 5, +1 at wave 12, +1 on
    the first conquest. Four, not five: the fifth is removed loudly — the fifth woven row is unwoven,
    every level it earned is kept, and the player is told.
22. **Forge** — the brief's §55 premise is false and the plan said so: the Forge screen was never
    behind a trait purchase. `Activity.Forge` opens on chests or two owned items. What IS bought on the
    tree is `efficient_forge` and `auto_merge`, and a node NAME that claims to open the Forge.
    Auto-merge moved to HOARD VAULTS level 2; `efficient_forge` is part of the remainder in item 15.
23. **Auto-sell** — the Warren's job now: `WarrenAutomation.AutoSellAtOrBelow` — SCAVENGER RUNS level 2
    sells Commons, level 4 Uncommons. A legacy save's tree filters are converted to that facility's
    level by `LegacyTraitTree` and applied by `SaveSystem.RestoreWarren` as a floor.
24. **Other moved capabilities** — both keystone sockets and the vow capacity to `Unlocks`, whose law
    is "derived, never stored" and whose every fact only ever grows. Paying for structural capacity is
    not a choice worth offering: a player who spends a point on A BASIC SOCKET has expressed nothing.

## VFX

25. **Anchor system** — `VfxAnchor`: `Center`, `Head`, `Standing`. Semantic, so a profile names a place
    on a body rather than an offset from a box.
26. **Visual-bounds system** — `VfxSubjectKind` (`Champion`, `Creature`, `EnemyRow`) resolves to the
    subject's VISIBLE bounds, not its layout rectangle. This is the load-bearing part: champions differ
    by 2.30x in drawn width (THE OATHBOUND 162 px, QUIVER 373) while drawing within 4 px of the same
    height, so anything measured against a box is wrong on eight of the ten.
27. **Relative scale** — `VfxBasis`: `SubjectHeight` or `SubjectWidth`, with `RelativeScale` a multiple
    of it. One authored ratio, correct on every silhouette, with no per-character number anywhere.
28. **Offsets and layers** — offsets are NORMALISED (`OffsetX` in subject widths, `OffsetY` in heights),
    so the same number means the same thing on every body. `VfxLayer`: `GroundUnder`, `BehindSubject`,
    `OnSubject`, `Overhead`. Plus `VfxFacing`, `VfxFollow`, `VfxLifetime`, `VfxTravel`. 20 profiles.
29. **Shield result** — **PASSES on all four silhouettes.** `fx_shield` was regenerated and fills 1.00
    of all eight frames (was 0.492), so the barrier resolves to 474x474 on THE SEEKER and THE MAGPIE
    and 477x477 on QUIVER and THE OATHBOUND — ratio 0.925/0.932 `ok`, a DOWNSCALE, where before it
    needed 1.88x and was clamped to 1.25. That is 1.15x the hunter's 412 px visible height: the
    authored relative scale met exactly rather than defended. The ring closes above the crown and below
    the soles, and on THE MAGPIE it clears the backpack. Evidence:
    `production/qa/evidence/vfx-shield-acceptance.md`, `build/shots/acceptance_{seeker,magpie}.png`.
30. **Debug tooling** — `RH_SHOT_MODE=vfxdebug` draws subject bounds, frame vs content, anchor, offset
    and ratio; `RH_VFX_DUMP=1` prints the same numbers as text (the artifact that can be acted on — the
    screenshot only says whether it looks plausible); `RH_VFX_BUDGET=1` measures every profile against
    every strip it can wear. `tools/asset-pipeline/vfx_dump.sh` captures both at once.

## UI quality

31. **Nine-slice changes** — none were needed, and that is the finding. `UiKit.NineSlice` already draws
    corners at `dstC = srcC / scale` — never magnified — with edges tiling in one direction and the
    centre filling. Measured on every screen that draws them, `ui_panel_square`, `ui_panel_vertical`
    and `ui_panel_medium` all report `src 40x40 dst 40x40 ratio 1.000`.
32. **Frame replacements** — none. Section 74's premise that the EQUIPPED frame is "enlarged beyond its
    intended scale" did not survive measurement: the frame is not magnified at all. A ledger says so;
    an assurance would not have.
33. **Equipped frame fix** — the real fault on that screen was two others, both found by measuring:
    the paper doll drew the hunter at ratio **1.327 OVER** (a 398 px figure painted 528 px tall) while
    the same strip in the fight drew at 1.08, because the doll's box takes whatever vertical room is
    left and so asked for a size the art cannot supply. `UiKit.AnimSprite` caps at
    `UiKit.RasterCeiling` (1.25) — in the PRIMITIVE, because shrinking one box is a number the next
    screen would have to rediscover. And `GEAR POWER` was printed under the corner scrollwork, reading
    "GEAR POWE": `UiKit.ContentRightAt`/`ContentLeftAt` answer for the ornament band (120 px deep,
    96 px in, measured off a rendered frame) where `ContentRight` answers for the side rail.
    Ledger: `production/qa/evidence/ui-raster-ledger.md`. **All eleven screens: 0 over budget.**
34. **Inventory polish** — INVENTORY was a bare `Plate` between an ornate EQUIPPED and a quiet-framed
    DETAIL: the middle of three columns the only one with no frame, which reads as unfinished rather
    than as deliberately quiet. It wears the house quiet frame now, its header sits on
    `UiKit.CaptionTop`, and its footer anchors to `UiKit.ContentBottom` — measured from the raw
    rectangle, the stacked footer at UI SCALE 150 printed RIGHT-CLICK FOR MORE across the frame's own
    bottom rail. Tabs, grid, rarity borders, lock badges and item states were already coherent and were
    left alone. Verified at 100 and 150.

## PixelLab

35. **Generated assets** — ten signature skill icons, one per champion (commit `99f0389`), and
    `fx_shield` regenerated as a closed ring filling its whole frame.
36. **Paths** — `assets/art/UI/icons/skills/icon_skill_sig_<character>_<skill>.png`, beside the twelve
    shipped skill icons and following their key convention, so the three existing draw sites (library
    tile, loadout slot, skill-tree header) picked them up with no code change.
    `assets/art/VFX/shield/fx_shield_strip8_512.png` for the barrier.
37. **Actual rendered sizes** — icons authored at 192 square, drawn at `UiMetrics.Control(36)`: 36 px at
    100 %, 45 at 125 %, 54 at 150 % — the same source-to-draw ratio the shipped twelve have, and the
    direction is downscale. Measured rather than eyeballed: at 36 px the ten paint 25-71 % of their
    square against the shipped twelve's 10-61 %. The shield strip is 8 frames of 512 square, drawn at
    474 — also a downscale.
38. **Rejected and regenerated** — four icons. A balance scale, a longbow and a plumb bob all passed at
    full size and failed at 36 px: each was thin or elongated, so trimming and centring left a smudge,
    a scratch and a dot. They came back as a coin purse, a packed quiver and a round carved weight.
    A fourth was regenerated to avoid a silhouette collision and its replacement failed the same
    thinness test, so the original stands: a mildly familiar shape costs less than an unreadable one.
    Contact sheets: `build/shots/sig_icons_realsize.png`, `sig_icons_final.png`.
39. **Unused assets removed** — none to remove: every generated file is filed at exactly one path by
    `file_generated.py`, and staging is gitignored. `tools/check_asset_consumers.py` proves it — all
    are reached by the code, and the baseline of pre-pivot orphans did not grow.

## Persistence

40. **Save migrations** — `LegacyTraitTree.Read` is the large one: 19 keystones, 13 vows, 2 sockets and
    2 loot filters, all as FLOORS and UNIONS so running it on every load forever grants nothing a first
    pass did not. Applied in the load path before anything can be written back, and by
    `SaveSystem.RestoreWarren` for the facility half. `LearnedSkills` is still WRITTEN and no longer
    READ, for one version, so a player who rolls back does not lose their skills on the way out.
41. **Preserved old capabilities** — every keystone, vow, socket and loot filter an old save bought.
    The one entitlement deliberately removed is the fifth skill slot, removed LOUDLY (item 21).
    `save.MemoryDustUnlocks` must keep round-tripping for as long as item 15's remainder stands — the
    ten-second autosave destroys any field the build stops writing.
42. **Removed obsolete fields** — `MasteryTree._learned` and its `RestoreLearned`/`LearnSkill` pair,
    which latched the active champion's own skill into an account-wide set every frame. `SkillDef`'s
    Form-era knobs. `BrokenVowShare` and `Vows.CombinedFactor`'s `brokenShare` parameter, deleted with
    THE PRICE PAID rather than migrated.

## Validation

43. **Tests** — Core **1612**, Game **246**, Integration **2**, all green. New in this refactor:
    mastery access (9), signature ownership (9), loadout repair (13), starter loadout (4), the
    signature library and its locked copy (19), respec repair (10), kill-reaction dispatch (5), vow
    capacity, vow discovery, keystone world source, warren automation, trait-tree migration, the map
    strip (8) and the keystone notice (2). The liveness suites grew from 72 reinforcements to 132;
    `trait_liveness_test` covers all 26 traits and fails if one is added without a fight that proves it
    moves damage, health, shield or healing.
44. **Fixtures** — new dials for states nothing could photograph: `RH_SHOT_PICK` (a locked skill),
    `RH_SHOT_RESPEC` (the armed warning), `RH_SHOT_SHED` (a character switch), `RH_SHOT_HUNTER` (pose
    any champion), `RH_SHOT_SHIELDFX` (a shield moment at its peak), `RH_SHOT_CONQUEST` (a later
    conquest's headline), `RH_SHOT_SWORN` (swear vows with the list CLOSED — the only way to photograph
    the vow READING), `RH_UI_BUDGET` (the raster ledger).

    **Four dials that accepted values they could not honour now abort.** Each returned a plausible
    screenshot of the DEFAULT state under the filename of the state that was asked for:
    `RH_SHOT_HUNTER` posed the default champion on a typo, so a cross-silhouette proof could photograph
    one silhouette twice; `RH_SHOT_SHIELDFX` posed nothing (this one bit — `hold` is not a shield
    moment and it produced usable-looking output); `RH_SHOT_CONQUEST` posed the SHORTEST headline when
    the dial exists for the longest; `RH_SHOT_TRAIT` posed no reading at all for any trait outside the
    fixture's eleven, which is why neither re-authored vow trait had ever been looked at.
45. **Screenshots** — the BUILD library, the locked state, the respec warning, the switch toast and the
    build-level vow reading; the TRAITS cards for both re-authored traits; the shield on four
    silhouettes plus the debug view; the GEAR screen at 100 and 150; the icon contact sheets at real
    size. Under `build/shots/`.
46. **Build** — 0 errors, 0 warnings across the solution; all eleven gates green (`tools/check_all.sh`);
    boot green, reads and writes and reads back what it wrote (`tools/check_boot.sh`).
47. **Remaining balance and design debt** — three things, named rather than left to be discovered:

    1. **The 12 stat nodes on the dormant tree** (item 15). They need a design call on what a legacy
       stat purchase becomes in the new vocabulary before the tree can be deleted. Until then the tree
       is a legacy-power carrier that no player can reach or add to, and `efficient_forge` and
       `recall_1..4` ride along with it.
    2. **The mastery point economy was tuned against the OLD access rule**, where a road bought once
       was kept for ever. Under the new rule a career's ~66 points buy about twelve roads at six for
       the first of a style and five for its second, which is the intended shape — but it has not been
       played.
    3. **`cast.trap` and `field.aura` are over the VFX asset-scale budget** (ratios 1.37-3.80 and 1.44).
       They are art orders in `vfx-asset-scale-ledger.md`, not numbers to turn, and are unrelated to
       the shield.
