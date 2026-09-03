using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE LOADOUT IS REPAIRED, NEVER PUNISHED.
/// </summary>
/// <remarks>
/// <para>
/// Two events can now leave a slot holding a skill the fight will not carry: a respec that takes back the
/// road a shared skill was unlocked by (BRIEF sec.19), and a champion switch that leaves the previous
/// champion's signature behind (sec.13). Both used to end the same way — the composer skipped the slot and
/// nothing on screen said so, which is the dormant-slot failure with the player's own build inside it.
/// </para>
/// <para>
/// What every test here also pins is the half that must NOT happen: the slot survives, the levels survive,
/// and nothing is converted into anything (LAW 4).
/// </para>
/// </remarks>
public class LoadoutRepairTest
{
    private static readonly Character Seeker = CharacterRoster.Get("seeker");
    private static readonly Character Anvil = CharacterRoster.Get("anvil");

    private static PlayerLoadout Woven(params string[] skillIds)
    {
        var l = new PlayerLoadout { SkillCapacity = 4 };
        foreach (var id in skillIds)
        {
            var slot = l.AddSkill();
            l.SetSource(slot, Source.Body);
            l.SetSkill(slot, id);
        }
        return l;
    }

    private static IReadOnlySet<string> Access(params string[] ids) => ids.ToHashSet(System.StringComparer.Ordinal);

    // ── The slot verb itself ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_clearing_a_slot_empties_it_and_keeps_it()
    {
        var l = Woven("hammer_blow", "snare_jaws");

        Assert.True(l.ClearSkill(0));

        Assert.Equal(2, l.Skills.Count);            // the slot is still the player's
        Assert.Null(l.Skills[0].SkillId);           // and it is empty
        Assert.Equal("snare_jaws", l.Skills[1].SkillId);
    }

    [Fact]
    public void test_clearing_an_already_empty_slot_changes_nothing()
    {
        var l = new PlayerLoadout { SkillCapacity = 4 };
        l.AddSkill();

        Assert.False(l.ClearSkill(0));
        Assert.False(l.ClearSkill(7));
        Assert.Single(l.Skills);
    }

    // ── What a champion may use, which is the composer's rule and nobody else's ───────────────────

    [Fact]
    public void test_a_shared_skill_needs_its_current_node()
    {
        var blow = SkillCatalogue.Find("hammer_blow")!;

        Assert.True(LoadoutRepair.CanUse(blow, Access("hammer_blow"), Seeker));
        Assert.False(LoadoutRepair.CanUse(blow, Access(), Seeker));
    }

    [Fact]
    public void test_a_signature_needs_its_owner_and_never_a_node()
    {
        var sig = SkillCatalogue.Find(Seeker.SignatureSkillId!)!;

        // No access at all, and it still composes — a signature is exempt from mastery (sec.18).
        Assert.True(LoadoutRepair.CanUse(sig, Access(), Seeker));
        // And the whole catalogue unlocked will not give it to anybody else (LAW 1).
        Assert.False(LoadoutRepair.CanUse(sig, SkillCatalogue.All.Select(d => d.Id).ToHashSet(), Anvil));
        Assert.False(LoadoutRepair.CanUse(sig, Access(), null));
    }

    // ── A respec: warn on exactly these, then clear exactly these ─────────────────────────────────

    [Fact]
    public void test_the_respec_warning_names_the_shared_skills_that_lose_their_node()
    {
        var l = Woven("hammer_blow", "snare_jaws", Seeker.SignatureSkillId!);
        var afterRespec = new MasteryTree();
        afterRespec.RestoreTaken(System.Array.Empty<string>());

        var losing = LoadoutRepair.UnusableSkills(l, afterRespec.AvailableSkills(), Seeker);

        Assert.Equal(new[] { "hammer_blow", "snare_jaws" }, losing.Select(d => d.Id));
    }

    [Fact]
    public void test_the_repair_clears_those_slots_and_only_those()
    {
        var l = Woven("hammer_blow", "snare_jaws", Seeker.SignatureSkillId!);

        var cleared = LoadoutRepair.Repair(l, Access("snare_jaws"), Seeker);

        Assert.Equal(new[] { "hammer_blow" }, cleared.Select(d => d.Id));
        Assert.Null(l.Skills[0].SkillId);
        Assert.Equal("snare_jaws", l.Skills[1].SkillId);          // its node is still taken
        Assert.Equal(Seeker.SignatureSkillId, l.Skills[2].SkillId); // exempt from mastery
        Assert.Equal(3, l.Skills.Count);                           // and nobody lost a slot
    }

    [Fact]
    public void test_a_repair_never_touches_what_the_skill_has_earned()
    {
        // LAW 4, and the reason it is structural rather than a promise: SkillProgress is keyed by
        // skill id and this file never mentions it.
        var progress = new SkillProgress();
        for (var i = 0; i < 400; i++) progress.RecordWave("hammer_blow");
        var level = progress.LevelOf("hammer_blow");
        Assert.True(level >= 1, "the fixture must earn a level for this test to mean anything");

        var l = Woven("hammer_blow");
        LoadoutRepair.Repair(l, Access(), Seeker);

        Assert.Null(l.Skills[0].SkillId);
        Assert.Equal(level, progress.LevelOf("hammer_blow"));
    }

    // ── A champion switch: the foreign signature, and nothing else ────────────────────────────────

    [Fact]
    public void test_a_switch_sheds_the_previous_champions_signature()
    {
        var l = Woven(Seeker.SignatureSkillId!, "snare_jaws");

        var cleared = LoadoutRepair.ShedForeignSignatures(l, Anvil);

        Assert.Equal(new[] { Seeker.SignatureSkillId }, cleared.Select(d => d.Id));
        Assert.Null(l.Skills[0].SkillId);
        Assert.Equal("snare_jaws", l.Skills[1].SkillId);
    }

    [Fact]
    public void test_a_switch_keeps_the_new_champions_own_signature()
    {
        var l = Woven(Anvil.SignatureSkillId!);

        Assert.Empty(LoadoutRepair.ShedForeignSignatures(l, Anvil));
        Assert.Equal(Anvil.SignatureSkillId, l.Skills[0].SkillId);
    }

    [Fact]
    public void test_a_switch_never_touches_a_shared_skill_even_a_locked_one()
    {
        // A switch is not a respec. hammer_blow may well be locked here — no tree is consulted — and
        // it keeps its slot, where the BUILD screen reads it LOCKED with the level it kept (sec.20).
        var l = Woven("hammer_blow", Seeker.SignatureSkillId!);

        LoadoutRepair.ShedForeignSignatures(l, Anvil);

        Assert.Equal("hammer_blow", l.Skills[0].SkillId);
        Assert.Null(l.Skills[1].SkillId);
    }

    [Fact]
    public void test_a_switch_never_converts_one_signature_into_another()
    {
        // The emptied slot stays empty. Putting the new champion's signature in it would be the game
        // making a build decision for the player, which sec.13 forbids in as many words.
        var l = Woven(Seeker.SignatureSkillId!);

        LoadoutRepair.ShedForeignSignatures(l, Anvil);

        Assert.Null(l.Skills[0].SkillId);
        Assert.DoesNotContain(l.Skills, s => s.SkillId == Anvil.SignatureSkillId);
    }

    [Fact]
    public void test_every_champions_signature_is_shed_by_every_other_champion()
    {
        // The whole roster, both ways round: ten signatures, and no pair where one champion may hold
        // another's. A single mis-typed owner id would otherwise hide in one card nobody photographs.
        foreach (var owner in CharacterRoster.All)
            foreach (var other in CharacterRoster.All)
            {
                var l = Woven(owner.SignatureSkillId!);
                var cleared = LoadoutRepair.ShedForeignSignatures(l, other);
                if (owner.Id == other.Id) Assert.Empty(cleared);
                else Assert.Single(cleared);
            }
    }

    // ── The composer agrees, which is the only reason any of the above is worth asserting ─────────

    [Fact]
    public void test_the_repair_leaves_exactly_the_slots_the_composer_would_carry()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        tree.RestoreTaken(new[] { "road_snare" });

        var l = Woven("hammer_blow", "snare_jaws", Seeker.SignatureSkillId!, Anvil.SignatureSkillId!);
        LoadoutRepair.Repair(l, tree.AvailableSkills(), Seeker);

        var composed = l.ToBuild(new MemoryDustTree(), tree, Seeker).Skills.Select(s => s.Def.Id).ToList();
        var kept = l.Skills.Where(s => s.SkillId is not null).Select(s => s.SkillId!).ToList();

        Assert.Equal(kept, composed);
        Assert.Equal(new[] { "snare_jaws", Seeker.SignatureSkillId }, kept);
    }
}
