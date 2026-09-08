using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE SIGNATURE IS EQUIPPED FOR THE PLAYER.
/// </summary>
/// <remarks>
/// Release polish, 2026-09-05. A champion's own skill goes into the build on a switch and on a fresh
/// open — into the first empty slot, never over a shared skill the player chose, and never into another
/// champion's build. <see cref="LoadoutRepair.EnsureSignature"/> is the one rule; the host calls it
/// after <see cref="LoadoutRepair.ShedForeignSignatures"/> on every switch and once after a load.
/// </remarks>
public class SignatureAutoEquipTest
{
    private static readonly Character Seeker = CharacterRoster.Get("seeker");
    private static readonly Character Anvil = CharacterRoster.Get("anvil");

    private static string Sig(Character c) => c.SignatureSkillId!;

    private static PlayerLoadout Woven(int capacity, params string[] skillIds)
    {
        var l = new PlayerLoadout { SkillCapacity = capacity };
        foreach (var id in skillIds)
        {
            var slot = l.AddSkill();
            l.SetSource(slot, Source.Body);
            if (id.Length > 0) l.SetSkill(slot, id);
        }
        return l;
    }

    [Fact]
    public void test_empty_loadout_gets_the_signature_in_a_new_slot()
    {
        var l = new PlayerLoadout { SkillCapacity = 1 };

        var slot = LoadoutRepair.EnsureSignature(l, Seeker);

        Assert.Equal(0, slot);
        Assert.Equal(Sig(Seeker), l.Skills[0].SkillId);
    }

    [Fact]
    public void test_an_emptied_slot_is_where_the_signature_lands()
    {
        // The switch: Seeker's signature shed from slot 0, shared skills kept in 1 and 2.
        var l = Woven(3, Sig(Seeker), "hammer_blow", "snare_jaws");
        LoadoutRepair.ShedForeignSignatures(l, Anvil);
        Assert.Null(l.Skills[0].SkillId);

        var slot = LoadoutRepair.EnsureSignature(l, Anvil);

        Assert.Equal(0, slot);
        Assert.Equal(Sig(Anvil), l.Skills[0].SkillId);
        Assert.Equal("hammer_blow", l.Skills[1].SkillId);   // the player's shared picks are untouched
        Assert.Equal("snare_jaws", l.Skills[2].SkillId);
        Assert.Equal(3, l.Skills.Count);                    // no slot was added past the one that was free
    }

    [Fact]
    public void test_already_woven_is_a_no_op()
    {
        var l = Woven(2, "hammer_blow", Sig(Seeker));

        Assert.Equal(-1, LoadoutRepair.EnsureSignature(l, Seeker));
        Assert.Equal(1, l.IndexOfSkill(Sig(Seeker)));      // and it did not move
    }

    [Fact]
    public void test_a_full_build_of_shared_skills_is_not_overwritten()
    {
        var l = Woven(2, "hammer_blow", "snare_jaws");

        Assert.Equal(-1, LoadoutRepair.EnsureSignature(l, Seeker));
        Assert.Equal(new[] { "hammer_blow", "snare_jaws" }, l.Skills.Select(s => s.SkillId));
    }

    [Fact]
    public void test_capacity_headroom_opens_a_slot_for_it()
    {
        var l = Woven(2, "hammer_blow");

        var slot = LoadoutRepair.EnsureSignature(l, Seeker);

        Assert.Equal(1, slot);
        Assert.Equal(2, l.Skills.Count);
        Assert.Equal(Sig(Seeker), l.Skills[1].SkillId);
    }

    [Fact]
    public void test_no_champion_places_nothing()
    {
        var l = new PlayerLoadout { SkillCapacity = 2 };

        Assert.Equal(-1, LoadoutRepair.EnsureSignature(l, null));
        Assert.Empty(l.Skills);
    }

    [Fact]
    public void test_every_champion_receives_only_its_own_signature()
    {
        foreach (var c in CharacterRoster.All)
        {
            var l = new PlayerLoadout { SkillCapacity = 1 };
            var slot = LoadoutRepair.EnsureSignature(l, c);
            Assert.Equal(0, slot);
            var def = SkillCatalogue.Find(l.Skills[0].SkillId!);
            Assert.NotNull(def);
            Assert.Equal(c.Id, def!.OwnerCharacterId);
            // And the composer honours it: the placed skill is one the fight will carry.
            Assert.True(LoadoutRepair.CanUse(def, new System.Collections.Generic.HashSet<string>(), c));
        }
    }

    [Fact]
    public void test_switch_then_ensure_never_leaves_a_foreign_signature()
    {
        var l = Woven(2, Sig(Seeker), "hammer_blow");
        LoadoutRepair.ShedForeignSignatures(l, Anvil);
        LoadoutRepair.EnsureSignature(l, Anvil);

        Assert.DoesNotContain(Sig(Seeker), l.Skills.Select(s => s.SkillId));
        Assert.Contains(Sig(Anvil), l.Skills.Select(s => s.SkillId));
        Assert.Empty(LoadoutRepair.UnusableSlots(l, new System.Collections.Generic.HashSet<string> { "hammer_blow" }, Anvil));
    }
}
