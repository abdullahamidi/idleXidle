using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// WHAT THE PLAYER EQUIPS IS WHAT ENTERS COMBAT — the kind caps, end to end.
/// </summary>
/// <remarks>
/// <para>
/// The bug (playtest 2026-09-22): two Active skills could be configured in BUILD at capacity 2, and on
/// HUNT one of them was simply missing. The cause was two legality rules that never met — the screen
/// grouped slots by an active/passive/active/passive ladder, <see cref="Build.Equip"/> enforced the
/// ladder's per-kind budget, and <c>BuildComposer</c> DISCARDED the refusal. So a skill vanished
/// between the screen and the fight with nothing anywhere able to report it.
/// </para>
/// <para>
/// It was never only about two Actives. The same discarded bool dropped a second PASSIVE at capacity 3,
/// and at capacity 1 the passive budget was a hard zero — so six of the ten champions, the ones whose
/// signature is a Field or a Reaction, composed with NO SKILLS AT ALL. This file covers the family.
/// </para>
/// <para>
/// The rule now: unlocks buy TOTAL capacity, a skill brings its own kind, and a build holds at most
/// two of each. A slot has no kind.
/// </para>
/// </remarks>
public class SkillKindCapsTest
{
    private static readonly Character Seeker = CharacterRoster.Get("seeker");

    /// <summary>A champion whose signature takes NO beat — six of the ten are like this.</summary>
    private static readonly Character Tower = CharacterRoster.Get("tower");

    private static PlayerLoadout Loadout(int capacity, Character? character, params string[] skillIds)
    {
        var l = new PlayerLoadout { SkillCapacity = capacity };
        if (character?.SignatureSkillId is { } sig) l.SignatureSkillId = sig;
        foreach (var id in skillIds)
        {
            var slot = l.AddSkill();
            Assert.True(slot >= 0, $"no slot for {id} at capacity {capacity}");
            Assert.True(l.SetSkill(slot, id), $"{id} was refused at capacity {capacity}");
        }
        return l;
    }

    private static Build Compose(PlayerLoadout l, Character? character = null)
        => l.ToBuild(Taught.Everything(), character);

    private static IReadOnlyList<string> Ids(Build b) => b.Skills.Select(s => s.Def.Id).ToList();

    // ── CAPACITY 2: TWO ACTIVES. The reported bug. ───────────────────────────────────────────────

    [Fact]
    public void test_two_actives_at_capacity_two_are_accepted_and_both_reach_the_composed_build()
    {
        var loadout = Loadout(2, null, "hammer_blow", "volley_spray");

        // THE EDITOR ACCEPTED BOTH — it always did; that was never the half that was broken.
        Assert.Equal(new[] { "hammer_blow", "volley_spray" }, loadout.Skills.Select(s => s.SkillId));

        // ...AND NOW SO DOES THE FIGHT. Before the fix this was one skill: Build.Equip refused the
        // second active against a budget of one and the composer threw the answer away.
        var build = Compose(loadout);
        Assert.Equal(new[] { "hammer_blow", "volley_spray" }, Ids(build));
        Assert.Equal(2, build.ActiveCount);

        // ...and nothing was refused in silence. This list is the diagnostic the composer never had.
        Assert.Empty(build.RefusedSkills);
    }

    [Fact]
    public void test_both_actives_at_capacity_two_really_cast_in_a_real_battle()
    {
        // COLLECTION COUNTS ARE NOT COMBAT. A skill can be in Build.Skills and never take a beat, so
        // this drives the real SoloExpedition and reads the wave's own events back.
        //
        // ASSERTED BY SLOT, NOT BY COUNT. A Skill event carries the slot that cast it, and some build
        // effects emit a Skill event for a slot that did not itself act — counting events would pass
        // on one skill firing twice. Two DISTINCT slots, each mapped back to its own id, is the claim.
        var loadout = Loadout(2, null, "hammer_blow", "volley_spray");
        var build = Compose(loadout);
        Assert.Equal(2, build.Skills.Count);

        var cast = CastingSlots(build, waves: 12, seed: 17);

        var byId = cast.Select(slot => build.Skills[slot].Def.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "hammer_blow", "volley_spray" }, byId);
    }

    /// <summary>Every slot that produced a Skill event of its own across a few waves.</summary>
    private static SortedSet<int> CastingSlots(Build build, int waves, int seed)
    {
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 9f,
                                     ExpeditionTuning.Default, new Random(seed));

        var slots = new SortedSet<int>();
        for (var w = 0; w < waves; w++)
        {
            run.RefreshPool();
            run.PushWave();
            var events = run.LastWaveEvents;
            if (events.Count == 0) break;
            foreach (var e in events)
                if (e.Kind == BattleEventKind.Skill && e.Slot >= 0 && e.Slot < build.Skills.Count)
                    slots.Add(e.Slot);
        }
        return slots;
    }

    // ── CAPACITY 2: TWO PASSIVES. The half nobody reported. ──────────────────────────────────────

    [Fact]
    public void test_two_passives_at_capacity_two_both_reach_the_composed_build()
    {
        var loadout = Loadout(2, null, "sign_brand", "field_mire");
        var build = Compose(loadout);

        Assert.Equal(new[] { "sign_brand", "field_mire" }, Ids(build));
        Assert.Equal(2, build.PassiveCount);
        Assert.Empty(build.RefusedSkills);
    }

    // ── THE PASSIVE SIGNATURE. Six of the ten champions. ─────────────────────────────────────────

    [Fact]
    public void test_a_passive_signature_survives_composition_at_capacity_one()
    {
        // THE WORST CASE THE OLD SPLIT PRODUCED, and it was silent: PassiveSlotsFor(1) was
        // total - ActiveSlotsFor(1) = 0, so a champion whose only skill takes no beat composed with
        // nothing at all. Every passive-signature champion is swept, because this was a property of
        // the arithmetic rather than of any one of them.
        foreach (var champion in CharacterRoster.All)
        {
            if (champion.SignatureSkillId is not { } sig || SkillCatalogue.Find(sig) is not { } def) continue;
            if (def.TakesABeat) continue;

            var loadout = Loadout(1, champion, sig);
            var build = Compose(loadout, champion);

            Assert.Equal(new[] { sig }, Ids(build));
            Assert.Empty(build.RefusedSkills);
        }
    }

    [Fact]
    public void test_a_passive_signature_plus_one_more_passive_is_legal_up_to_the_global_cap()
    {
        // At capacity 3 the old passive budget was ONE, so THE TOWER lost a second passive with a
        // slot still free. Two is the cap now, and the third is refused by the rule rather than
        // dropped by the composer.
        var loadout = Loadout(3, Tower, Tower.SignatureSkillId!, "sign_brand", "hammer_blow");
        var build = Compose(loadout, Tower);

        Assert.Equal(3, build.Skills.Count);
        Assert.Equal(2, build.PassiveCount);
        Assert.Equal(1, build.ActiveCount);
        Assert.Empty(build.RefusedSkills);

        // ...and a THIRD passive is refused at the decision surface, signature included in the count.
        var third = loadout.AddSkill();
        Assert.True(third < 0 || !loadout.SetSkill(third, "field_mire"));
    }

    // ── THE MIXED BUILD, unchanged. ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_one_active_and_one_passive_at_capacity_two_is_unchanged()
    {
        // The configuration that always worked. It has to keep working, and for the same reason —
        // not because the caps happen to allow it.
        var loadout = Loadout(2, null, "hammer_blow", "sign_brand");
        var build = Compose(loadout);

        Assert.Equal(new[] { "hammer_blow", "sign_brand" }, Ids(build));
        Assert.Equal(1, build.ActiveCount);
        Assert.Equal(1, build.PassiveCount);
        Assert.Empty(build.RefusedSkills);
    }

    // ── THE GLOBAL CAP AT FULL CAPACITY. ─────────────────────────────────────────────────────────

    [Fact]
    public void test_capacity_four_holds_two_of_each_and_refuses_a_third_of_either()
    {
        var loadout = Loadout(4, null, "hammer_blow", "volley_spray", "sign_brand", "field_mire");
        var build = Compose(loadout);

        Assert.Equal(4, build.Skills.Count);
        Assert.Equal(2, build.ActiveCount);
        Assert.Equal(2, build.PassiveCount);
        Assert.Empty(build.RefusedSkills);

        // A THIRD ACTIVE IS REFUSED — with a reason, not by being dropped later.
        var full = Loadout(4, null, "hammer_blow", "volley_spray");
        var free = full.AddSkill();
        Assert.Equal(SkillRefusal.ActivesFull, full.RefusalFor(free, SkillCatalogue.Find("snare_repay")!));
        Assert.False(full.SetSkill(free, "snare_repay"));

        // ...AND SO IS A THIRD PASSIVE.
        var quiet = Loadout(4, null, "sign_brand", "field_mire");
        var spare = quiet.AddSkill();
        Assert.Equal(SkillRefusal.PassivesFull, quiet.RefusalFor(spare, SkillCatalogue.Find("hammer_press")!));
        Assert.False(quiet.SetSkill(spare, "hammer_press"));
    }

    [Fact]
    public void test_a_slot_being_repointed_does_not_count_its_own_occupant()
    {
        // Swapping one Active for another must not read as a third. The slot under the pen frees its
        // own kind first, or every full build would be uneditable.
        var loadout = Loadout(2, null, "hammer_blow", "volley_spray");
        Assert.True(loadout.SetSkill(1, "snare_repay"));
        Assert.Equal(new[] { "hammer_blow", "snare_repay" }, loadout.Skills.Select(s => s.SkillId));
    }

    // ── SAVE REPAIR. Configurations the old editor allowed. ──────────────────────────────────────

    [Fact]
    public void test_a_save_with_three_actives_keeps_the_first_two_and_drops_only_the_excess()
    {
        // The old BUILD screen enforced no kind rule, so this save is real: three Actives at capacity
        // 4, of which the fight had silently been running two all along.
        var loadout = new PlayerLoadout { SkillCapacity = 4 };
        loadout.Restore(Rows("hammer_blow", "volley_spray", "snare_repay", "sign_brand"), keystoneIds: Array.Empty<string>());

        // THE EXCESS IS THE LAST OF ITS KIND, and the slots behind it keep their positions.
        Assert.Equal(new string?[] { "hammer_blow", "volley_spray", null, "sign_brand" },
                     loadout.Skills.Select(s => s.SkillId));
        Assert.Equal(new[] { "snare_repay" }, loadout.Repaired);

        // ...and what composes is exactly the repaired loadout, with nothing refused.
        var build = Compose(loadout);
        Assert.Equal(new[] { "hammer_blow", "volley_spray", "sign_brand" }, Ids(build));
        Assert.Empty(build.RefusedSkills);
    }

    [Fact]
    public void test_a_repair_never_takes_the_champions_own_skill()
    {
        // PRIORITY ONE. THE TOWER's signature takes no beat, so a save holding it plus two more
        // passives is over the cap — and the one thing that may not go is the champion itself.
        var loadout = new PlayerLoadout { SkillCapacity = 4, SignatureSkillId = Tower.SignatureSkillId };
        loadout.Restore(Rows("sign_brand", "field_mire", Tower.SignatureSkillId!, "hammer_blow"), keystoneIds: Array.Empty<string>());

        Assert.Contains(Tower.SignatureSkillId, loadout.Skills.Select(s => s.SkillId));
        Assert.Equal(new[] { "field_mire" }, loadout.Repaired);   // the LAST excess passive, not the first
        Assert.Equal(new string?[] { "sign_brand", null, Tower.SignatureSkillId, "hammer_blow" },
                     loadout.Skills.Select(s => s.SkillId));
    }

    [Fact]
    public void test_a_legal_save_is_not_touched_and_the_repair_is_idempotent()
    {
        // PRIORITY FIVE, and the reason this needs no save version: re-running the repair changes
        // nothing, so a second load neither edits the build nor tells the player again.
        var legal = new PlayerLoadout { SkillCapacity = 4 };
        legal.Restore(Rows("hammer_blow", "volley_spray", "sign_brand", "field_mire"), keystoneIds: Array.Empty<string>());
        Assert.Empty(legal.Repaired);

        // ...and a save that DID need repairing settles after one pass: reloading what the repair
        // produced reports nothing further to remove.
        var broken = new PlayerLoadout { SkillCapacity = 4 };
        broken.Restore(Rows("hammer_blow", "volley_spray", "snare_repay", "sign_brand"), keystoneIds: Array.Empty<string>());
        Assert.Single(broken.Repaired);

        // Re-saved and re-loaded: an emptied slot carries no id, so Restore does not raise a row for
        // it — what must hold is that the SKILLS are the same ones and that nothing further is taken.
        var again = new PlayerLoadout { SkillCapacity = 4 };
        again.Restore(broken.Skills.Where(s => s.SkillId is not null)
                             .Select(s => (s.SkillId, (string?)s.Source.ToString(), (string?)null, s.VowId, s.Passive)),
                      keystoneIds: Array.Empty<string>());
        Assert.Empty(again.Repaired);
        Assert.Equal(broken.Skills.Where(s => s.SkillId is not null).Select(s => s.SkillId),
                     again.Skills.Where(s => s.SkillId is not null).Select(s => s.SkillId));
    }

    /// <summary>Saved rows in the shape <c>PlayerLoadout.Restore</c> takes, v3-style (the id is the identity).</summary>
    private static IEnumerable<(string? SkillId, string? Source, string? Form, string? VowId, bool? Passive)> Rows(
        params string[] ids)
        => ids.Select(id => ((string?)id, (string?)Source.Body.ToString(), (string?)null, (string?)null,
                             (bool?)(SkillCatalogue.Find(id) is { } d && !d.TakesABeat)));
}
