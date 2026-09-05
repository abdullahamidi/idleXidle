using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// Every reinforcement must CHANGE THE FIGHT. Seventy-two of them, checked one at a time.
/// </summary>
/// <remarks>
/// <para>
/// The same guarantee <see cref="VariationLivenessTests"/> makes for the twenty-four variations, and
/// it matters more here: a reinforcement is the FOURTH thing a skill buys, so a dead one is a wall
/// the player climbed for sixty-four waves to reach. They shipped for one commit carrying no
/// <c>Modify</c> at all — declared, displayed, and doing nothing — which is exactly the failure this
/// file exists to make impossible to repeat.
/// </para>
/// <para>
/// The baseline is the SAME skill with the SAME variation and the reinforcement NOT bought, so what
/// moves is the reinforcement alone. As with the variations, whether it moves damage or health is
/// deliberately unspecified: HOLLOW and GAUNT buy neither, they buy bites that never land.
/// </para>
/// </remarks>
public class ReinforcementLivenessTests
{
    private readonly ITestOutputHelper _out;
    public ReinforcementLivenessTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> EveryReinforcement()
        => SkillCatalogue.All.SelectMany(d => d.Variations.SelectMany(
               v => v.Reinforcements.Select(r => new object[] { d.Id, v.Name, r.Name })));

    /// <summary>
    /// A tree with every style road walked — the probe means to weave any of the twelve.
    /// </summary>
    /// <remarks>
    /// Six skills are taught by the mastery tree (design §5), so a fixture holding a bare tree can
    /// only ever weave the other six and a probe over all twelve would report half of them dormant
    /// for a reason that has nothing to do with what it is measuring.
    /// </remarks>
    internal static MasteryTree EveryRoadWalked()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        // THE ROAD NODES ONLY, without the specialisations that gate them. RestoreTaken does not
        // walk prerequisites, which is what makes that possible — and it has to be done, because a
        // specialisation also grants a trigger and sets an Affinity, and taking all six turned the
        // fixture into a champion with six enchantments. CLUSTER measured as dormant under it: the
        // VOLLEY specialisation's extra target had already covered the reach it gives up.
        tree.RestoreTaken(MasteryCatalog.Nodes
            .Where(x => x.Kind == MasteryKind.SkillRoad)
            .Select(x => x.Id), repair: false);
        return tree;
    }

    private static Build Build(SkillDef def, string variation, string? reinforcement, string? partnerId = null)
    {
        var progress = new SkillProgress();
        for (var i = 0; i < SkillProgress.UsesForLevel(SkillProgress.MaxLevel); i++) progress.RecordWave(def.Id);
        Assert.True(progress.ChooseVariation(def, variation), $"could not take {variation}");
        if (reinforcement is not null)
            Assert.True(progress.TakeReinforcement(def, reinforcement), $"could not buy {reinforcement}");

        // The skill under test, named by its id, beside a plain BLOW so the champion can still fight
        // when the skill itself deals nothing — the same shape the variation file uses. A named
        // skill brings its own kind, so it lands in the slot it demands.
        var picks = new List<BuildComposer.SkillPick>
        {
            new(Source.Body, null, SkillId: partnerId ?? SkillCatalogue.ActiveOf(Style.Hammer).Id),
            new(Source.Body, null, SkillId: def.Id),
        };

        // THE OWNER SITS IN THE CHAIR FOR A SIGNATURE, and for nothing else. Ten of the skills
        // measured here belong to one champion each, and the composer refuses a signature to anybody
        // else — so a null character would compose NO skill at all and the whole set would measure as
        // dormant for a reason that has nothing to do with the design. The ownership rule is not
        // weakened: the fixture supplies the champion the rule requires. The shared twelve keep the
        // null character, so no shipped measurement moved.
        return BuildComposer.Compose(EveryRoadWalked(),
                                     character: CharacterRoster.Find(def.OwnerCharacterId ?? ""),
                                     skills: picks, keystoneIds: Array.Empty<string>(),
                                     slotCapacity: 4, progress: progress);
    }

    /// <summary>
    /// Every channel a reinforcement can move, not just the two the variations needed.
    /// </summary>
    /// <remarks>
    /// Damage and health alone are not enough here, and the difference is not pedantry: BROOK buys
    /// healing and STANDING buys shield, and against a fixture champion with room to spare BOTH are
    /// invisible in end-of-run health — the extra heal overheals and the bigger shield absorbs a bite
    /// that the smaller one already absorbed. Reading the heal and shield the fight actually published
    /// measures the reinforcement instead of the fixture's headroom. The champion also STARTS at half
    /// its pool, so a heal always has somewhere to go.
    /// </remarks>
    private readonly record struct Outcome(long Dealt, long Healed, long Shielded, int Kept, int Reached);

    /// <summary>
    /// TWO REGIONS, and the reinforcement has to move one of them.
    /// </summary>
    /// <remarks>
    /// Neither depth alone is honest. In a shallow one the champion out-heals the wave outright, so SUP
    /// fills its pool every wave and 1% a pulse and 2% are the same number; in a deep one it dies in the
    /// second wave, and a defence break that needs ten seconds of standing still to matter never gets
    /// them. Both are places a player goes, and a reinforcement that changes the fight in either is a
    /// reinforcement that can be felt — demanding both would be demanding that every purchase be good
    /// at every depth, which is the opposite of what a build is for.
    /// </remarks>
    /// <remarks>
    /// A FOURTH depth was added for the same reason as the third. At 260 and 620 a champion carrying
    /// WILT/SUP ends every wave at full health, so its pulse heal overheals and 1% a pulse and 1.5% a
    /// pulse refill the same empty space — BALM measured as dormant against a fixture that was never
    /// hurt. 1600 is a wave that wins the exchange, which is the only place a bigger heal is a bigger
    /// heal.
    /// </remarks>
    private static readonly (float Power, float Pressure)[] Regions =
        { (260f, 26f), (620f, 62f), (1600f, 160f) };

    /// <summary>
    /// A THIRD scenario, because a conditional reinforcement must be measured where its condition is
    /// true (brief §65).
    /// </summary>
    /// <remarks>
    /// The two regions above start the champion at HALF its pool against a wave it can only just
    /// clear, and that fixture cannot pose several of the rules this rework adds: RIPE asks to be
    /// above 90% health, BALM needs somewhere to heal, and REMNANT, BALANCE, CLEANUP and PUNCH THROUGH
    /// all need enemies that actually DIE mid-wave. Measured against a fixture that satisfies none of
    /// them, every one of them read as dormant — which would have been the fixture reporting on
    /// itself.
    /// </remarks>
    private const float ShallowPower = 90f, ShallowPressure = 9f;

    private static Outcome Fight(Build build, (float Power, float Pressure) region)
    {
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        // FULL health in the shallow scenario, half in the deep ones: one asks what a healthy hunter
        // does with a wave it is beating, the others what a hurt one does with a wave that is beating it.
        var shallow = region.Power <= ShallowPower;
        var champ = shallow
            ? new Champion { MaxHealth = pool * 40, Health = pool * 40 }
            : new Champion { MaxHealth = pool * 40, Health = pool * 20 };
        var run = new SoloExpedition(build, champ, hunter, region.Power, region.Pressure,
                                     ExpeditionTuning.Default, new Random(19));

        long dealt = 0, healed = 0, shielded = 0;
        for (var w = 0; w < 6; w++)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
            foreach (var e in run.LastWaveEvents)
                switch (e.Kind)
                {
                    case BattleEventKind.Strike: dealt += e.Amount; break;
                    case BattleEventKind.Heal: healed += e.Amount; break;
                    case BattleEventKind.ShieldGained: shielded += e.Amount; break;
                }
        }
        return new Outcome(dealt, healed, shielded, champ.Health, run.Wave);
    }

    [Theory]
    [MemberData(nameof(EveryReinforcement))]
    public void test_buying_a_reinforcement_changes_the_fight(string skillId, string variation, string name)
    {
        var def = SkillCatalogue.ById(skillId);
        var moved = false;
        foreach (var region in Regions.Append((Power: ShallowPower, Pressure: ShallowPressure)))
        {
            // THE PARTNER MATTERS FOR A RULE COUNTED IN HITS. Beside a six-beat BLOW barely three
            // damaging hits land inside a six-second window, so SPEND's "3 hits" and "5 hits" are the
            // same window and COUNT reads as dormant — the fixture reporting on itself, not on the
            // purchase. The shallow run pairs with SPRAY, which lands five arrows a cast.
            var partner = region.Power <= ShallowPower ? "volley_spray" : null;
            var plain = Build(def, variation, null, partner);
            var bought = Build(def, variation, name, partner);
            var without = Fight(plain, region);
            var with = Fight(bought, region);
            _out.WriteLine($"{def.Name} / {variation} / {name} @{region.Power:0}: "
                           + $"dealt {without.Dealt} -> {with.Dealt}, kept {without.Kept} -> {with.Kept}, "
                           + $"healed {without.Healed} -> {with.Healed}, shielded {without.Shielded} -> {with.Shielded}, "
                           + $"wave {without.Reached} -> {with.Reached}");
            moved |= without != with;
        }

        Assert.True(moved,
            $"{def.Name} / {variation} / {name} changed NOTHING at either depth — the same damage dealt, "
            + "the same health kept, the same healing and shield against the same variation. It is a "
            + "level the player earned over sixty-four waves, spent, and cannot feel.");
    }

    [Fact]
    public void test_every_reinforcement_declares_a_delta()
    {
        // The cheap check that would have caught the commit where all seventy-two shipped inert. The
        // theory above is the real proof; this one names the gap before a fight has to find it.
        var mute = SkillCatalogue.All
            .SelectMany(d => d.Variations.SelectMany(v => v.Reinforcements.Select(r => (d, v, r))))
            .Where(t => t.r.Modify is null)
            .Select(t => $"{t.d.Name}/{t.v.Name}/{t.r.Name}")
            .ToList();
        Assert.True(mute.Count == 0, "reinforcements with no Modify: " + string.Join(", ", mute));
        // 132: the shared twelve's seventy-two, plus sixty for the ten signatures (P2 of the
        // systems refactor). Was 72.
        Assert.Equal(132, SkillCatalogue.All.SelectMany(d => d.Variations).Sum(v => v.Reinforcements.Count));
    }
}
