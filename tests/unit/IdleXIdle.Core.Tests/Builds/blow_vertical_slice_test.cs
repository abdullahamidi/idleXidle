using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE BLOW VERTICAL SLICE (refactor brief §16): one skill walked through EVERY layer of the
/// resolution chain — base skill, variation, Source, reinforcement, mastery, character, gear,
/// set, vow — through the REAL composition path (<see cref="PlayerLoadout.ToBuild"/>), into the
/// REAL fight (<see cref="DamageBench"/> drives <c>SoloBattle.ResolveWave</c>), with no central
/// combat branch naming the skill.
/// </summary>
/// <remarks>
/// <para>
/// This file exists because the 2026-08-31 audit's #2 finding was that the whole
/// variation/reinforcement layer was DORMANT in shipped play: 97 liveness rows passed by handing
/// <c>Progress</c> straight to the composer, while the hunt composed without it — built, tested,
/// green, and never running, the exact species this project hunts. So the slice goes through
/// <c>PlayerLoadout.ToBuild(..., progress)</c>, the hunt's own door, and its companion assertion
/// is that the door DEFAULTS SHUT: composing without progress must fight at the base line.
/// </para>
/// <para>
/// Layer arithmetic is asserted on the resolved <see cref="SkillDef"/> (deltas compose in order);
/// the FIGHT is asserted as an ordering (each layer's bench reading beats the build without it),
/// because exact damage numbers belong to the balance sweeps, not here.
/// </para>
/// </remarks>
public class BlowVerticalSliceTest
{
    private static PlayerLoadout LoadoutWithBlow(string? vowId = null)
    {
        var l = new PlayerLoadout { SkillCapacity = 1 };
        var slot = l.AddSkill();
        l.SetSkill(slot, "hammer_blow");
        if (vowId is not null) l.SetVow(slot, vowId, Vows.Catalog);
        return l;
    }

    private static SkillProgress Chosen(params string[] reinforcements)
    {
        var progress = new SkillProgress();
        var blow = SkillCatalogue.ById("hammer_blow");
        // Enough waves for the variation and every requested reinforcement (levels come from use).
        progress.Restore(new[] { ("hammer_blow", SkillProgress.UsesForLevel(4), (string?)null,
                                  (System.Collections.Generic.IReadOnlyList<string>)Array.Empty<string>()) });
        Assert.True(progress.ChooseVariation(blow, "FLATTEN"));
        foreach (var r in reinforcements)
            Assert.True(progress.TakeReinforcement(blow, r), $"could not take {r}");
        return progress;
    }

    [Fact]
    public void test_the_resolved_blow_carries_every_chosen_layer_as_data()
    {
        // Arrange — BLOW, its FLATTEN variation (Source BODY, defence ignore), and TOLL (+50%).
        var loadout = LoadoutWithBlow();
        var progress = Chosen("TOLL");

        // Act — through the hunt's own composition door.
        var build = loadout.ToBuild(new MemoryDustTree(), Taught.Everything(), character: null, progress);

        // Assert — the fight reads ONE resolved SkillDef; every layer is a delta on it.
        var sk = Assert.Single(build.Skills);
        Assert.Equal("hammer_blow", sk.Def.Id);
        Assert.True(sk.Def.DefenceIgnore, "FLATTEN's defence ignore did not reach the resolved skill");
        Assert.Equal(1.5f, sk.Def.DamageMultiplier, precision: 3);   // TOLL
        Assert.Equal(Source.Body, sk.Source);                        // the variation OWNS the Source
        Assert.Equal(500f, sk.Def.BasePower, precision: 1);          // the skill owns its number
    }

    [Fact]
    public void test_without_progress_the_same_loadout_fights_at_the_base_line()
    {
        // The dormant-layer regression guard: the hunt composes with progress since P4, and if any
        // caller forgets it again, the build silently reverts to exactly this.
        var build = LoadoutWithBlow().ToBuild(new MemoryDustTree(), Taught.Everything(), character: null);

        var sk = Assert.Single(build.Skills);
        Assert.False(sk.Def.DefenceIgnore);
        Assert.Equal(1f, sk.Def.DamageMultiplier, precision: 3);
    }

    [Fact]
    public void test_each_layer_of_the_slice_moves_the_real_fight()
    {
        // Arrange/Act — the same loadout fought against an ARMOURED unkillable dummy as each
        // layer joins (DamageBench's own dummy has no armour, and FLATTEN's whole purchase is
        // defence ignore — a bench that cannot see it would certify the layer dormant).
        var tree = new MemoryDustTree();
        var mastery = Taught.Everything();
        var seeker = CharacterRoster.Get("seeker");   // EVEN HAND: HitSize 1.08

        float Bench(SkillProgress? progress, Character? character, Hunter hunter)
        {
            var build = LoadoutWithBlow().ToBuild(tree, mastery, character, progress);
            var champ = new Champion { MaxHealth = 1_000_000, Health = 1_000_000 };
            var creatures = new[] { WaveCreature.Single(1e9f, 0.01f, defense: 60f) };
            var metrics = new WaveMetrics();
            SoloBattle.ResolveWave(champ, build, hunter, creatures, enemyIntervalMs: 100_000,
                ExpeditionTuning.Default, new Random(9), metrics: metrics);
            return metrics.DeliveredDamage;
        }

        var baseline = Bench(null, null, new Hunter());
        var withVariation = Bench(Chosen(), null, new Hunter());          // FLATTEN
        var withReinforcement = Bench(Chosen("TOLL"), null, new Hunter());
        var withCharacter = Bench(Chosen("TOLL"), seeker, new Hunter());

        // GEAR + SET: a blade favours HAMMER (StylePower x1.25), and two worn pieces of one element
        // light the set's first rung (+8% to that Source's skills — BLOW's FLATTEN is BODY).
        var geared = new Hunter();
        geared.Equip(new ItemInstance
        {
            InstanceId = "w1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Common, SellValue = 1,
            Family = 0, Element = Source.Body,   // family 0 = blade: {Hammer, Drain}
        });
        geared.Equip(new ItemInstance
        {
            InstanceId = "r1", BaseType = ItemBaseType.Ring, Rarity = Rarity.Common, SellValue = 1,
            Element = Source.Body,
        });
        var withGearAndSet = Bench(Chosen("TOLL"), seeker, geared);

        // Assert — every layer buys a strictly better fight, through the one composition path.
        Assert.True(withVariation > baseline, "FLATTEN did not move the fight (armoured dummy)");
        Assert.True(withReinforcement > withVariation, "TOLL did not move the fight");
        Assert.True(withCharacter > withReinforcement, "the character's passive did not move the fight");
        Assert.True(withGearAndSet > withCharacter, "gear favour + the set rung did not move the fight");
    }

    [Fact]
    public void test_a_met_vow_pays_and_an_unmet_one_does_not()
    {
        // VOW OF COMPLETION demands every slot filled — met by this one-slot build; then the same
        // vow judged against an empty second slot is unmet and pays nothing. Both through the real
        // composition, neither through a special case.
        var met = LoadoutWithBlow(vowId: "vow_complete")
            .ToBuild(new MemoryDustTree(), Taught.Everything(), character: null, Chosen("TOLL"));
        var unmetLoadout = LoadoutWithBlow(vowId: "vow_complete");
        unmetLoadout.SkillCapacity = 2;
        unmetLoadout.AddSkill();   // an EMPTY second slot the vow can see... but an added slot is a
        unmetLoadout.RemoveSkill(1);            // woven default — remove it so the slot stays empty
        var unmet = unmetLoadout.ToBuild(new MemoryDustTree(), Taught.Everything(), character: null, Chosen("TOLL"));

        var metDps = DamageBench.Measure(met, new Hunter()).Dps;
        var unmetDps = DamageBench.Measure(unmet, new Hunter()).Dps;
        Assert.True(metDps > unmetDps,
            $"a MET vow ({metDps:F0}) must out-damage the same build with the vow UNMET ({unmetDps:F0})");
    }

    [Fact]
    public void test_no_central_branch_names_a_normal_skill()
    {
        // The battle loop understands semantics, not content ids (design law 2). The catalogue and
        // the composer may speak ids; the fight may not.
        var repo = AppContext.BaseDirectory;
        var dir = new System.IO.DirectoryInfo(repo);
        while (dir is not null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var battle = System.IO.File.ReadAllText(
            System.IO.Path.Combine(dir!.FullName, "src", "IdleXIdle.Core", "Builds", "SoloBattle.cs"));
        foreach (var def in SkillCatalogue.All)
            Assert.DoesNotContain($"\"{def.Id}\"", battle);
    }
}
