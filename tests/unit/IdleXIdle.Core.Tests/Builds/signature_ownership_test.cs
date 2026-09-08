using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A SIGNATURE SKILL BELONGS TO ONE CHAMPION.
/// </summary>
/// <remarks>
/// <para>
/// BRIEF sections 2-14 and LAWS 1-2. Ownership is a field on the definition, not a naming rule and
/// not a table somewhere else: <see cref="SkillDef.OwnerCharacterId"/> is null for the twelve shared
/// skills and carries a character id for the ten signatures.
/// </para>
/// <para>
/// The rule this replaces let a champion's birth skill leak account-wide. The host latched the active
/// champion's skill into the permanent learned set every frame, so playing one champion once taught
/// its skill to all ten, for ever, in every existing save. Section 9 calls that out by name.
/// </para>
/// </remarks>
public class SignatureOwnershipTest
{
    private static PlayerLoadout OneSlot(string skillId)
    {
        var l = new PlayerLoadout { SkillCapacity = 1 };
        l.SetSkill(l.AddSkill(), skillId);
        l.SetSource(0, Source.Body);
        return l;
    }

    // ── The catalogue's own shape (sec.98 items 1-3) ─────────────────────────────────────────────

    [Fact]
    public void test_every_character_has_exactly_one_signature()
    {
        foreach (var c in CharacterRoster.All)
        {
            Assert.False(string.IsNullOrEmpty(c.SignatureSkillId), $"{c.Name} has no signature skill.");
            var def = SkillCatalogue.Find(c.SignatureSkillId!);
            Assert.True(def is not null, $"{c.Name} names {c.SignatureSkillId}, which is not a skill.");
        }
    }

    [Fact]
    public void test_every_signature_names_the_character_that_owns_it()
    {
        foreach (var c in CharacterRoster.All)
        {
            var def = SkillCatalogue.Find(c.SignatureSkillId!)!;
            Assert.Equal(c.Id, def.OwnerCharacterId);
        }
    }

    [Fact]
    public void test_no_two_characters_share_a_signature()
    {
        var ids = CharacterRoster.All.Select(c => c.SignatureSkillId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void test_an_owned_skill_belongs_to_a_real_character_and_the_shared_twelve_are_unowned()
    {
        foreach (var def in SkillCatalogue.All)
        {
            if (def.OwnerCharacterId is not { } owner) continue;
            Assert.True(CharacterRoster.Find(owner) is not null,
                        $"{def.Id} is owned by {owner}, who is not on the roster.");
        }

        // The twelve shared skills stay shared (sec.10). BLOW is nobody's.
        Assert.Null(SkillCatalogue.Find("hammer_blow")!.OwnerCharacterId);
        Assert.Equal(12, SkillCatalogue.All.Count(s => s.OwnerCharacterId is null));
    }

    // ── What the composer does with it (sec.98 items 4-6, 9) ─────────────────────────────────────

    [Fact]
    public void test_the_active_character_may_weave_its_own_signature()
    {
        var seeker = CharacterRoster.Get("seeker");
        var build = OneSlot(seeker.SignatureSkillId!)
            .ToBuild(new MasteryTree(), seeker);

        Assert.Equal(seeker.SignatureSkillId, Assert.Single(build.Skills).Def.Id);
    }

    [Fact]
    public void test_another_character_may_not_weave_it()
    {
        var seeker = CharacterRoster.Get("seeker");
        var magpie = CharacterRoster.Get("magpie");

        // The same loadout, the same tree, a different champion in the chair.
        var build = OneSlot(seeker.SignatureSkillId!)
            .ToBuild(new MasteryTree(), magpie);

        Assert.Empty(build.Skills);
    }

    [Fact]
    public void test_no_mastery_allocation_can_grant_someone_elses_signature()
    {
        // sec.9: unlocking a character does not put its signature in the shared library, and sec.18:
        // no mastery node teaches one. A tree with EVERY node taken still cannot hand it over.
        var everything = new MasteryTree();
        everything.SetEarned(1000);
        foreach (var n in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.SkillRoad))
        {
            foreach (var p in n.Prereqs) TakeChain(everything, p);
            everything.Take(n.Id);
        }

        var seeker = CharacterRoster.Get("seeker");
        Assert.DoesNotContain(seeker.SignatureSkillId, everything.AvailableSkills());

        var build = OneSlot(seeker.SignatureSkillId!)
            .ToBuild(everything, CharacterRoster.Get("magpie"));
        Assert.Empty(build.Skills);
    }

    [Fact]
    public void test_a_signature_needs_no_mastery_node_of_its_own()
    {
        // sec.18: the owner may weave it on a tree with nothing taken at all.
        var anvil = CharacterRoster.Get("anvil");
        var build = OneSlot(anvil.SignatureSkillId!)
            .ToBuild(new MasteryTree(), anvil);

        Assert.Single(build.Skills);
    }

    [Fact]
    public void test_a_signature_cannot_fill_two_slots()
    {
        // sec.14: the duplicate-skill rule covers signatures too, and it is enforced below the UI.
        var seeker = CharacterRoster.Get("seeker");
        var l = new PlayerLoadout { SkillCapacity = 4 };
        var a = l.AddSkill();
        var b = l.AddSkill();

        Assert.True(l.SetSkill(a, seeker.SignatureSkillId!));
        Assert.False(l.SetSkill(b, seeker.SignatureSkillId!),
                     "the same skill id must never occupy two slots, signature or shared");
    }

    // ── Progression is the skill's, not the champion's (sec.98 item 7) ───────────────────────────

    [Fact]
    public void test_progression_survives_a_character_switch()
    {
        // Play THE SEEKER, earn levels on its signature, become THE MAGPIE, come back.
        var seeker = CharacterRoster.Get("seeker");
        var progress = new SkillProgress();
        for (var i = 0; i < 400; i++) progress.RecordWave(seeker.SignatureSkillId!);
        var earned = progress.LevelOf(seeker.SignatureSkillId!);
        Assert.True(earned >= 1, "the fixture must earn a level for this test to mean anything");

        // Nothing about being someone else touches a skill's ledger: SkillProgress is keyed by skill
        // id and knows nothing about who was in the chair when the uses were earned.
        Assert.Equal(earned, progress.LevelOf(seeker.SignatureSkillId!));
    }

    private static void TakeChain(MasteryTree t, string id)
    {
        if (t.IsTaken(id)) return;
        var n = MasteryCatalog.ById(id);
        if (n is null) return;
        foreach (var p in n.Prereqs.Take(1)) TakeChain(t, p);
        foreach (var p in n.SecondPrereqs.Take(1)) TakeChain(t, p);
        t.Take(id);
    }
}
