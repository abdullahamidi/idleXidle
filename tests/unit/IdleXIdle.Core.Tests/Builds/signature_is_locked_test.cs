using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE CHAMPION'S OWN SKILL HOLDS SLOT ONE, and the player cannot take it out.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-09-09: <i>"the idea of making the character's signature skill changeable is
/// definitely wrong. That skill must remain unchangeable."</i> They were reporting the shipped
/// behaviour. Nothing in Core or Game pinned it: <c>RemoveSkill</c> deleted the slot,
/// <c>ClearSkill</c> emptied it, <c>SetSkill</c> wrote over it and <c>MoveSkill</c> dragged it off the
/// top — and the screen advertised the last one ("FIRST IN LINE — IT WINS EVERY TIED BEAT"). The
/// identity was drawn (a gold SIGNATURE heading, a pinned library tile) and the rule was absent.
/// </para>
/// <para>
/// Slot ONE specifically, because slot order is a combat decision: the champion takes one action per
/// beat and casts the first READY skill in slot order, so the top of the list wins every tie.
/// </para>
/// </remarks>
public class signature_is_locked_test
{
    private static readonly Character Seeker = CharacterRoster.Get("seeker");

    /// <summary>A loadout carrying the champion's signature plus one shared skill, host-configured.</summary>
    /// <remarks>
    /// THE SIGNATURE COUNTS AGAINST THE ACTIVE CAP, so the shared skills here are one Active and one
    /// Passive rather than two Actives. The Seeker's HARD HANDS takes a beat, and a build holds at most
    /// two skills that do — so this champion has exactly one Active left to spend, and a fixture that
    /// wove two would be building a loadout the model now refuses. These tests are about the signature
    /// being PINNED, not about the caps; the kinds are chosen so the pinning is what is under test.
    /// </remarks>
    private static PlayerLoadout Woven(params string[] shared)
    {
        var loadout = new PlayerLoadout();
        loadout.SkillCapacity = PlayerLoadout.MaxSkills;
        loadout.SignatureSkillId = Seeker.SignatureSkillId;

        var first = loadout.AddSkill();
        loadout.SetSkill(first, Seeker.SignatureSkillId!);
        foreach (var id in shared)
        {
            var s = loadout.AddSkill();
            loadout.SetSkill(s, id);
        }
        return loadout;
    }

    private static string SigId => Seeker.SignatureSkillId!;

    [Fact]
    public void test_the_signature_starts_in_slot_one_and_is_named()
    {
        var loadout = Woven("hammer_blow");
        Assert.Equal(0, loadout.SignatureSlot);
        Assert.True(loadout.IsSignatureSlot(0));
        Assert.False(loadout.IsSignatureSlot(1));
    }

    [Fact]
    public void test_the_signature_slot_cannot_be_emptied_or_removed()
    {
        var loadout = Woven("hammer_blow");

        Assert.False(loadout.ClearSkill(0));
        Assert.Equal(SigId, loadout.Skills[0].SkillId);

        loadout.RemoveSkill(0);
        Assert.Equal(2, loadout.Skills.Count);
        Assert.Equal(SigId, loadout.Skills[0].SkillId);

        // ...and the slot beside it is still perfectly ordinary.
        Assert.True(loadout.ClearSkill(1));
        Assert.Null(loadout.Skills[1].SkillId);
    }

    [Fact]
    public void test_nothing_can_be_written_over_the_signature()
    {
        var loadout = Woven("hammer_blow");

        Assert.False(loadout.SetSkill(0, "volley_spray"));
        Assert.Equal(SigId, loadout.Skills[0].SkillId);

        // Writing the signature back into its own slot is a no-op, not a refusal.
        Assert.True(loadout.SetSkill(0, SigId));
        Assert.Equal(SigId, loadout.Skills[0].SkillId);
    }

    [Fact]
    public void test_the_signature_cannot_be_dragged_off_the_top_and_nothing_can_be_dragged_onto_it()
    {
        var loadout = Woven("hammer_blow", "sign_brand");

        Assert.False(loadout.MoveSkill(0, 2));               // it will not leave
        Assert.Equal(SigId, loadout.Skills[0].SkillId);

        // A DRAG TO THE TOP LANDS UNDER IT rather than being refused. The player asked for "as early
        // as possible", and the earliest place that exists is second — answering that with nothing at
        // all would read as a broken drag rather than as a rule.
        Assert.True(loadout.MoveSkill(2, 0));
        Assert.Equal(SigId, loadout.Skills[0].SkillId);
        Assert.Equal("sign_brand", loadout.Skills[1].SkillId);
        Assert.Equal("hammer_blow", loadout.Skills[2].SkillId);

        // The other two still reorder freely against each other.
        Assert.True(loadout.MoveSkill(2, 1));
        Assert.Equal("hammer_blow", loadout.Skills[1].SkillId);
        Assert.Equal("sign_brand", loadout.Skills[2].SkillId);
    }

    [Fact]
    public void test_a_save_that_put_the_signature_lower_is_lifted_by_the_repair()
    {
        // Every save written before this rule can have the signature anywhere — and so could the old
        // repair, which placed it in "the first EMPTY slot". The load-time repair pins it.
        var loadout = new PlayerLoadout { SkillCapacity = PlayerLoadout.MaxSkills };
        var a = loadout.AddSkill(); loadout.SetSkill(a, "hammer_blow");
        var b = loadout.AddSkill(); loadout.SetSkill(b, "sign_brand");
        var c = loadout.AddSkill(); loadout.SetSkill(c, SigId);
        Assert.Equal(2, loadout.IndexOfSkill(SigId));

        loadout.SignatureSkillId = SigId;
        LoadoutRepair.EnsureSignature(loadout, Seeker);

        Assert.Equal(0, loadout.SignatureSlot);
        // Nothing was dropped — the list rotated.
        Assert.Equal(new[] { SigId, "hammer_blow", "sign_brand" },
                     loadout.Skills.Select(s => s.SkillId).ToArray());
    }

    [Fact]
    public void test_the_repair_places_a_missing_signature_at_the_top()
    {
        var loadout = new PlayerLoadout { SkillCapacity = PlayerLoadout.MaxSkills, SignatureSkillId = SigId };
        var a = loadout.AddSkill(); loadout.SetSkill(a, "hammer_blow");

        LoadoutRepair.EnsureSignature(loadout, Seeker);

        Assert.Equal(0, loadout.SignatureSlot);
        Assert.Equal("hammer_blow", loadout.Skills[1].SkillId);
    }

    [Fact]
    public void test_a_loadout_with_no_signature_named_is_unconstrained()
    {
        // A bench, a test, or a save being rebuilt before the host has said who is active. Every verb
        // behaves exactly as it did before the rule existed.
        var loadout = new PlayerLoadout { SkillCapacity = PlayerLoadout.MaxSkills };
        var a = loadout.AddSkill(); loadout.SetSkill(a, "hammer_blow");
        var b = loadout.AddSkill(); loadout.SetSkill(b, "sign_brand");

        Assert.Equal(-1, loadout.SignatureSlot);
        Assert.True(loadout.ClearSkill(0));
        Assert.True(loadout.MoveSkill(1, 0));
    }

    [Fact]
    public void test_a_champion_switch_can_still_take_the_previous_signature_out()
    {
        // The verbs refuse to move whatever the loadout is CURRENTLY told is the signature, so the
        // host must name the incoming champion before the switch repair runs. Naming the outgoing one
        // would leave a foreign signature welded into slot one, and the composer refuses to fight it.
        var anvil = CharacterRoster.Get("anvil");
        var loadout = Woven("hammer_blow");
        Assert.Equal(SigId, loadout.Skills[0].SkillId);

        loadout.SignatureSkillId = anvil.SignatureSkillId;      // what Game1.RepairForSwitch does first
        var shed = LoadoutRepair.ShedForeignSignatures(loadout, anvil);
        LoadoutRepair.EnsureSignature(loadout, anvil);

        Assert.NotEmpty(shed);
        Assert.Equal(anvil.SignatureSkillId, loadout.Skills[0].SkillId);
        Assert.Equal(0, loadout.SignatureSlot);
        Assert.False(loadout.HasSkill(SigId));
    }
}
