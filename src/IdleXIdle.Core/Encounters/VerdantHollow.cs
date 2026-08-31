using System.Collections.Generic;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// The single MVP region. Content is illustrative and unbalanced pending playtest — every number here
/// is a placeholder that survived internal consistency, which is not the same as being fun.
/// </summary>
/// <remarks>
/// Par values are NOT hardcoded magic numbers: each is <see cref="EncounterSpawner.DerivePar"/>
/// evaluated at the template's own anchor tier. A test asserts they reproduce the design document's
/// published worked examples exactly (15s / 13s / 24s / 591s).
/// </remarks>
public static class VerdantHollow
{
    public const string RegionId = "verdant_hollow";

    public static IReadOnlyList<EncounterTemplate> Templates { get; } = Build();

    private static IReadOnlyList<EncounterTemplate> Build()
    {
        var t = SpawnTuning.Default;

        return new List<EncounterTemplate>
        {
            // The attacker slot draws between two similar creatures for variety — par is derived from
            // the FIRST (Whelp), so the published worked example is unchanged.
            Template("enc_verdanthollow_whelp_std",
                tierBase: 1, weight: 10, isBoss: false, t,
                new CreatureTemplate { Id = "nature_atk_whelp_std", BaseHealth = 80 },
                new CreatureTemplate { Id = "nature_atk_stalker_std", BaseHealth = 85 }),

            Template("enc_verdanthollow_mossling_std",
                tierBase: 1, weight: 10, isBoss: false, t,
                new CreatureTemplate { Id = "nature_sup_mossling_std", BaseHealth = 65 }),

            Template("enc_verdanthollow_bramblehide_std",
                tierBase: 2, weight: 8, isBoss: false, t,
                new CreatureTemplate { Id = "nature_def_bramblehide_std", BaseHealth = 110 },
                new CreatureTemplate { Id = "nature_def_bulwark_std", BaseHealth = 115 }),

            // The boss. Excluded from the random draw and from every automated tick, by construction.
            Template("enc_verdanthollow_thornmaw_boss",
                tierBase: 4, weight: 0, isBoss: true, t,
                new CreatureTemplate { Id = "nature_atk_thornmaw_boss", BaseHealth = 1800 }),
        };
    }

    private static EncounterTemplate Template(
        string id, int tierBase, float weight, bool isBoss, SpawnTuning tuning,
        params CreatureTemplate[] pool) => new()
        {
            TemplateId = id,
            RegionId = RegionId,
            IsBoss = isBoss,
            CreaturePool = pool,
            PowerTierBase = tierBase,
            SelectionWeight = weight,
            // Par anchors on the first creature in the pool — the canonical one for this slot.
            ParClearTimeSeconds = EncounterSpawner.DerivePar(pool[0].BaseHealth, tierBase, isBoss, tuning),
        };
}
