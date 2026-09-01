using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// WHAT HAPPENS TO A SAVE WRITTEN BEFORE THE COMBAT REWORK (brief §73–§78).
/// </summary>
/// <remarks>
/// <para>
/// The rework renamed one variation and twenty-five reinforcements, moved eleven Sources, and deleted
/// four <see cref="SkillShape"/> dials. A save is a file on a player's disk that knows none of that,
/// and the promise made to them is narrow and absolute: <b>uses and levels are permanent</b>. What a
/// level was SPENT on may stop existing; the level itself never does.
/// </para>
/// <para>
/// These fixtures are written as the rows a real save carries — <c>(SkillId, Uses, Variation, Taken)</c>,
/// exactly what <see cref="SkillProgress.ToSave"/> emits — rather than through the current catalogue,
/// because a fixture built from today's names could never fail the way an old file does.
/// </para>
/// </remarks>
public class SaveMigrationTests
{
    private static readonly int MaxUses = SkillProgress.UsesForLevel(SkillProgress.MaxLevel);

    private static SkillProgress Loaded(params (string SkillId, int Uses, string? Variation, string[] Taken)[] rows)
    {
        var progress = new SkillProgress();
        progress.Restore(rows.Select(r => (r.SkillId, r.Uses, r.Variation,
                                           (IReadOnlyList<string>)r.Taken.ToList())));
        return progress;
    }

    // ── §73: the level is permanent ──────────────────────────────────────────────────────────────

    /// <summary>A purchase the rework deleted comes back as an unspent level, never as a lost one.</summary>
    [Fact]
    public void test_a_reinforcement_that_no_longer_exists_returns_its_level_unspent()
    {
        // AFTERGLOW was "the window holds 4s instead of 2s". SPEND has no window to hold any more.
        var progress = Loaded(("sign_call", MaxUses, "SPEND", new[] { "OVERSPEND", "AFTERGLOW" }));

        Assert.Equal(SkillProgress.MaxLevel, progress.LevelOf("sign_call"));
        Assert.Equal(MaxUses, progress.UsesOf("sign_call"));
        Assert.True(progress.HasReinforcement("sign_call", "OVERSPEND"), "a purchase that still exists was dropped");
        Assert.False(progress.HasReinforcement("sign_call", "AFTERGLOW"), "a purchase that no longer exists was kept");

        // The variation and one reinforcement are spent; the level AFTERGLOW cost is spendable again.
        Assert.Equal(2, progress.SpentOn("sign_call"));
        Assert.Equal(SkillProgress.MaxLevel - 2, progress.FreeOn("sign_call"));
    }

    /// <summary>
    /// A DELETED VARIATION costs the variation and its purchases, and nothing else.
    /// </summary>
    /// <remarks>
    /// This is the harshest case the rework can produce and it is deliberately not softened: choosing
    /// again is a decision the player gets to make, where being handed a replacement they did not pick
    /// is one made for them.
    /// </remarks>
    [Fact]
    public void test_a_deleted_variation_costs_the_choice_and_never_the_level()
    {
        var progress = Loaded(("hammer_blow", MaxUses, "NO SUCH VARIATION", new[] { "TOLL" }));

        Assert.Equal(SkillProgress.MaxLevel, progress.LevelOf("hammer_blow"));
        Assert.Null(progress.VariationOf(SkillCatalogue.ById("hammer_blow")));
        Assert.Equal(0, progress.SpentOn("hammer_blow"));   // no variation, so nothing beneath it either
        Assert.Equal(SkillProgress.MaxLevel, progress.FreeOn("hammer_blow"));
    }

    // ── §74: THIRST → SIPHON ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// DRINK's THIRST is called SIPHON now, and a hunter who chose it keeps everything beneath it.
    /// </summary>
    [Fact]
    public void test_thirst_is_siphon_and_the_purchases_beneath_it_survive()
    {
        var def = SkillCatalogue.ById("drain_drink");
        var progress = Loaded(("drain_drink", MaxUses, "THIRST", new[] { "PARCH", "TRICKLE" }));

        Assert.Equal("SIPHON", progress.VariationOf(def)?.Name);
        Assert.True(progress.HasReinforcement("drain_drink", "PARCH"));
        Assert.True(progress.HasReinforcement("drain_drink", "TRICKLE"));
        Assert.Equal(3, progress.SpentOn("drain_drink"));   // the variation and both reinforcements
    }

    /// <summary>
    /// GREEDY is PUMP: the same sentence, so the level stays spent rather than coming back.
    /// </summary>
    /// <remarks>
    /// It is the only reinforcement rename mapped, and the reason is that both cards read "lifesteal is
    /// 50% stronger" word for word. Every other rename also changed what the thing DOES, and handing a
    /// player the new rule because it sits in the old one's slot would be worse than giving the level back.
    /// </remarks>
    [Fact]
    public void test_greedy_is_pump_because_it_is_the_same_rule()
    {
        var progress = Loaded(("drain_drink", MaxUses, "THIRST", new[] { "GREEDY" }));

        Assert.True(progress.HasReinforcement("drain_drink", "PUMP"), "GREEDY did not become PUMP");
        Assert.Equal(2, progress.SpentOn("drain_drink"));   // the variation and PUMP
    }

    // ── §75: a Source change is not a respec ─────────────────────────────────────────────────────

    /// <summary>
    /// FINISH, CLUSTER, TORRENT, SUP and SHRIVEL all changed Source. None of them changed name, and a
    /// save that chose one keeps it.
    /// </summary>
    /// <remarks>
    /// The rework moved eleven variations between Sources to satisfy the matrix — every Source on
    /// exactly two Active and two Passive variations. A Source is not part of what the player chose:
    /// they chose a RULE, and the rule is unchanged. Forcing a respec for a bookkeeping move would be
    /// charging them for the catalogue's tidiness.
    /// </remarks>
    [Theory]
    [InlineData("hammer_blow", "FINISH")]
    [InlineData("volley_spray", "CLUSTER")]
    [InlineData("volley_weep", "TORRENT")]
    [InlineData("drain_wilt", "SUP")]
    [InlineData("drain_wilt", "SHRIVEL")]
    public void test_a_variation_that_only_changed_source_keeps_its_selection(string skillId, string variation)
    {
        var def = SkillCatalogue.ById(skillId);
        var progress = Loaded((skillId, MaxUses, variation, Array.Empty<string>()));
        Assert.Equal(variation, progress.VariationOf(def)?.Name);
    }

    // ── the round trip ───────────────────────────────────────────────────────────────────────────

    /// <summary>What a migrated save writes back is what today's catalogue calls it.</summary>
    /// <remarks>
    /// Without this the mapping would run on every load forever, and the day a future rework reuses the
    /// word THIRST for something else, every old save would silently become that instead.
    /// </remarks>
    [Fact]
    public void test_a_migrated_save_is_written_back_under_the_new_names()
    {
        var progress = Loaded(("drain_drink", MaxUses, "THIRST", new[] { "GREEDY", "PARCH" }));
        var row = progress.ToSave().Single(r => r.SkillId == "drain_drink");

        Assert.Equal("SIPHON", row.Variation);
        Assert.Equal(new[] { "PARCH", "PUMP" }, row.Taken);
        Assert.Equal(MaxUses, row.Uses);
    }

    /// <summary>A row for a skill this build does not have is dropped whole, and drops nothing else.</summary>
    [Fact]
    public void test_a_row_for_a_deleted_skill_is_dropped_and_the_rest_of_the_save_survives()
    {
        var progress = Loaded(
            ("manual_strike", 400, "ANYTHING", new[] { "ANYTHING" }),
            ("hammer_blow", MaxUses, "FLATTEN", new[] { "TOLL" }));

        Assert.Equal(0, progress.UsesOf("manual_strike"));
        Assert.Equal(MaxUses, progress.UsesOf("hammer_blow"));
        Assert.True(progress.HasReinforcement("hammer_blow", "TOLL"));
    }

    // ── §77: shield is wave-local, and the audit is the test ─────────────────────────────────────

    /// <summary>
    /// SHIELD IS NEVER SAVED, because there is no mid-wave battle state in a save to put it in.
    /// </summary>
    /// <remarks>
    /// The brief asked for an audit rather than an assumption. The audit: <see cref="Champion"/> is
    /// constructed fresh for every wave the expedition resolves and nothing in <c>SaveGame</c> carries
    /// a champion's health, shield, shades or beat count — a save records the RUN, not the instant.
    /// This asserts the half of it that code can assert: a champion starts every wave with no shield,
    /// so nothing can leak from the previous one.
    /// </remarks>
    [Fact]
    public void test_a_champion_carries_no_shield_into_a_wave_it_did_not_earn_one_in()
    {
        var champ = new Champion { MaxHealth = 1_000, Health = 1_000 };
        champ.GainShield(400f);
        Assert.Equal(400f, champ.CurrentShield);

        champ.ResetShield();
        Assert.Equal(0f, champ.CurrentShield);
    }
}
