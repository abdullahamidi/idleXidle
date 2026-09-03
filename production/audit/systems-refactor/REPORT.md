# SYSTEMS REFACTOR — REPORT (brief §117, 47 items)

Status: filled as each phase lands. `[pending]` means not done yet, and says which phase owns it.
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

10. **New architecture** — `[pending — phase 4]`
11. **Final catalogue** — `[pending — phase 4]` (26 traits; PLAN.md records the four cut from the
    design's thirty and why)
12. **Hidden discovery rules** — `[pending — phase 4]`
13. **Three-slot per-character loadout** — `[pending — phase 4]`
14. **UI redesign** — `[pending — phase 4]`
15. **Removed old tree systems** — `[pending — phase 4, after phase 5 lands its replacements]`

## Vows

16. **Discovery rules** — `[pending — phase 5]`
17. **Capacity progression** — `[pending — phase 5]`
18. **Tutorial access** — `[pending — phase 5]`

## Keystones

19. **Acquisition mapping** — `[pending — phase 5]`
20. **Slot progression** — `[pending — phase 5]`

## Structural unlocks

21. **Skill slots** — `[pending — phase 5]`
22. **Forge** — the brief's §55 premise is false and the plan says so: the Forge screen was never
    behind a trait purchase. `Activity.Forge` opens on chests or two owned items. What IS bought is
    `efficient_forge` and `auto_merge`, and a node NAME that claims to open the Forge. `[the rest
    pending — phase 5]`
23. **Auto-sell** — `[pending — phase 5]`
24. **Other moved capabilities** — `[pending — phase 5]`

## VFX

25. **Anchor system** — `[pending — phase 6]`
26. **Visual-bounds system** — `[pending — phase 6]`
27. **Relative scale** — `[pending — phase 6]`
28. **Offsets and layers** — `[pending — phase 6]`
29. **Shield result** — `[pending — phase 6]`
30. **Debug tooling** — `[pending — phase 6]`

## UI quality

31. **Nine-slice changes** — `[pending — phase 7]`
32. **Frame replacements** — `[pending — phase 7]`
33. **Equipped frame fix** — `[pending — phase 7]`
34. **Inventory polish** — `[pending — phase 8]`

## PixelLab

35. **Generated assets** — ten signature skill icons, one per champion. Commit `99f0389`.
36. **Paths** — `assets/art/UI/icons/skills/icon_skill_sig_<character>_<skill>.png`, beside the twelve
    shipped skill icons and following their key convention, so the three existing draw sites (library
    tile, loadout slot, skill-tree header) picked them up with no code change.
37. **Actual rendered sizes** — authored at 192², drawn at `UiMetrics.Control(36)`: 36 px at 100 %,
    45 at 125 %, 54 at 150 %. That is the same source-to-draw ratio the shipped twelve have, and the
    direction is downscale, which is safe. Measured rather than eyeballed: at 36 px the ten paint
    25–71 % of their square against the shipped twelve's 10–61 %, so the new set sits inside the
    family's range with more floor than its thinnest existing member.
38. **Rejected and regenerated** — four. A balance scale, a longbow and a plumb bob all passed at full
    size and failed at 36 px: each was thin or elongated, so trimming and centring left a smudge, a
    scratch and a dot. They came back as a coin purse, a packed quiver and a round carved weight —
    compact subjects that fill the square, which every shipped icon is. A fourth was regenerated to
    avoid a silhouette collision and its replacement failed the same thinness test, so the original
    stands: a mildly familiar shape costs less than an unreadable one. Before and after contact
    sheets: `build/shots/sig_icons_realsize.png` and `sig_icons_final.png`.
39. **Unused assets removed** — none to remove: every generated file is filed at exactly one path by
    `file_generated.py`, and staging is gitignored. `tools/check_asset_consumers.py` proves it — all
    ten are reached by the code, and the baseline of pre-pivot orphans did not grow.

## Persistence

40. **Save migrations** — `[pending — phases 4 and 5 carry the large ones]`. So far: `LearnedSkills` is
    still WRITTEN and no longer READ, for one version, so a player who rolls back to the previous
    build does not lose their skills on the way out.
41. **Preserved old capabilities** — `[pending — phase 5]`
42. **Removed obsolete fields** — `[pending — phases 4 and 5]`

## Validation

43. **Tests** — Core 1493, Game 191, Integration 2, all green at `08116a9`. New in this refactor:
    mastery access (9), signature ownership (9), loadout repair (13), starter loadout (4), the signature
    library and its locked copy (19), respec repair (10), the kill-reaction dispatch (5). The liveness
    suites grew from 72 reinforcements to 132, every one proved to move damage, health, shield or
    healing in a real fight.
44. **Fixtures** — new rig dials for states nothing could photograph: `RH_SHOT_PICK` (a locked skill in
    the inspector), `RH_SHOT_RESPEC` (the armed warning), `RH_SHOT_SHED` (a real character switch).
45. **Screenshots** — the BUILD library, the locked state, the respec warning and the switch toast at
    100/125/150; the icon contact sheets at real size.
46. **Build** — 0 errors, 0 warnings across the solution; all nine gates green; boot green.
47. **Remaining balance and design debt** — `[to complete at the end]`. Known so far: the point economy
    was tuned against the OLD access rule, where a road bought once was kept for ever. Under the new
    rule a career's ~66 points buy about twelve roads at six points for the first of a style and five
    for its second, which is the intended shape, but it has not been played.
