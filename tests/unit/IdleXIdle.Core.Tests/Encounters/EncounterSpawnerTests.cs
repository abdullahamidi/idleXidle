using System.Linq;
using IdleXIdle.Core.Encounters;
using Xunit;

namespace IdleXIdle.Core.Tests.Encounters;

/// <summary>
/// The LIVE half of the old spawner: par-time derivation and its reference-fight helpers, which the region
/// content still uses to stamp each template's <c>ParClearTimeSeconds</c>.
/// </summary>
/// <remarks>
/// The spawn/roll machinery (SpawnTrigger, AssignPowerTier, StandardPool, the whole EncounterSpawner
/// instance) belonged to the retired manual-combat model — nothing in the game rolls encounters that way any
/// more (the champion auto-fights waves), so those tests went with it. What survives are the three PURE
/// STATIC helpers (DerivePar, EffectiveMaxHealth, ReferenceTeamEffectiveDps) that Regions/VerdantHollow call.
/// </remarks>
public class EncounterSpawnerTests
{
    private static readonly SpawnTuning Tuning = SpawnTuning.Default;

    // ── Formula 4: par derivation. These are the design document's own published numbers. ──────────
    [Theory]
    [InlineData("enc_verdanthollow_whelp_std", 15)]
    [InlineData("enc_verdanthollow_mossling_std", 13)]
    [InlineData("enc_verdanthollow_bramblehide_std", 24)]
    [InlineData("enc_verdanthollow_thornmaw_boss", 591)]
    public void test_par_clear_times_reproduce_the_design_documents_worked_examples(string id, int expectedPar)
    {
        var template = VerdantHollow.Templates.Single(t => t.TemplateId == id);
        Assert.Equal(expectedPar, template.ParClearTimeSeconds);
    }

    /// <summary>Health scales with the rolled tier, via creature-data-schema Formula 1.</summary>
    [Fact]
    public void test_effective_health_scales_with_power_tier()
    {
        Assert.Equal(80, EncounterSpawner.EffectiveMaxHealth(80, 1, Tuning));   // no scaling at tier 1
        Assert.Equal(92, EncounterSpawner.EffectiveMaxHealth(80, 2, Tuning));   // 80 * 1.15
        Assert.Equal(308, EncounterSpawner.EffectiveMaxHealth(80, 20, Tuning)); // 80 * 3.85
    }

    /// <summary>The reference team's DPS erodes with tier, and never hits zero.</summary>
    [Fact]
    public void test_reference_team_dps_falls_off_but_never_reaches_zero()
    {
        Assert.Equal(6.0f, EncounterSpawner.ReferenceTeamEffectiveDps(1, Tuning), precision: 2);
        Assert.Equal(5.1f, EncounterSpawner.ReferenceTeamEffectiveDps(4, Tuning), precision: 2);
        Assert.True(EncounterSpawner.ReferenceTeamEffectiveDps(20, Tuning) >= Tuning.ReferenceTeamDpsFloor);
    }
}
