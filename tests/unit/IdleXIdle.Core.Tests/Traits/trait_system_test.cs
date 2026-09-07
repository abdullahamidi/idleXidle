using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Traits;

/// <summary>
/// THE RULES OF THE TRAIT SYSTEM (BRIEF §100), each one asserted rather than believed.
/// </summary>
/// <remarks>
/// <para>
/// Deterministic discovery, no lottery, nothing missable, discovery account-wide and the loadout per
/// character, exactly three worn, swapping free and reversible, and — the one the whole design turns
/// on — <b>no undiscovered trait may leak its condition</b> (§28, §39, LAW 8).
/// </para>
/// <para>
/// The last test in the file is the anti-dormancy guard and is the most important one here: every
/// counter the catalogue reads must actually RISE when a wave feeds the ledger. A rule whose counter
/// nothing writes is unsatisfiable, and an unsatisfiable rule is a trait that can never be earned —
/// which is exactly how <c>StyleActivations[Style.Sign]</c> was found to be zero for every build that
/// has ever run (see <c>WaveMetrics.MarkCasts</c>).
/// </para>
/// </remarks>
public class trait_system_test
{
    private static readonly TraitFirst Somewhere = new("seeker", "cinderworks", 12);

    private static TraitLedger Everything()
    {
        var l = new TraitLedger();
        foreach (var def in TraitCatalogue.All) l.Discover(def.Id, Somewhere);
        return l;
    }

    /// <summary>A wave that did a little of everything, so every fight-fed counter has something to add.</summary>
    private static TraitWaveFacts BusyWave() => new(
        ChampionMaxHealth: 1_000,
        HeavyHits: 3,
        Overkill: 5_000f,   // five pools: enough that GREAT WASTE (four) rises too
        ShieldGained: 1_500f,
        ShieldBreaks: 2,
        ShieldAbsorbed: 1_200f,
        Healed: 900,
        ReflectedDamage: 800f,
        LowestHealthFraction: 0.05f,
        HealthLost: 0,
        CreaturesKilled: 7,
        MarkCasts: 4,
        CarriedWideSkill: true,
        CreaturesPresent: 4,
        CritPercent: 60f,
        PureSourceBuild: true,
        DistinctSources: 4,
        VowsKept: 2);

    // ── DETERMINISTIC, AND NEVER A LOTTERY (§29, §30, LAW 8) ──────────────────────────────────────

    [Fact]
    public void test_every_rule_is_a_counter_and_a_number_and_nothing_else()
    {
        // A rule that is DATA cannot be random: there is no draw to make and no branch to take. This
        // is LAW 8 held structurally rather than by inspection — the type itself forbids a lambda.
        foreach (var def in TraitCatalogue.All)
        {
            Assert.True(Enum.IsDefined(def.Discovery.Counter),
                $"{def.Name}: reads a counter that is not in the closed set.");
            Assert.True(def.Discovery.Threshold > 0d,
                $"{def.Name}: a threshold of {def.Discovery.Threshold} would awaken on an empty account.");
        }
    }

    [Fact]
    public void test_the_same_account_discovers_the_same_traits_every_time()
    {
        // Run the identical history through two fresh ledgers. Same in, same out — twice, because
        // "deterministic" is a claim about repetition, not about one run.
        IReadOnlyList<string> Once()
        {
            var ledger = new TraitLedger();
            var woke = new List<string>();
            for (var w = 0; w < 12; w++)
                woke.AddRange(TraitDiscovery.OnWaveCleared(ledger, BusyWave(), TraitAccount.None, Somewhere));
            return woke;
        }

        Assert.Equal(Once(), Once());
    }

    [Fact]
    public void test_nothing_can_be_missed_because_every_rule_is_rechecked()
    {
        // A threshold crossed while the player was not looking. Nothing is evaluated during the run;
        // the ledger is simply fed, and one later pass awakens everything owed. No moment to be
        // present for, no event to catch, nothing missable (§30).
        var ledger = new TraitLedger();
        ledger.Add(TraitCounter.ShieldBreaks, 999);

        var woke = TraitDiscovery.Evaluate(ledger, TraitAccount.None, Somewhere);
        Assert.Contains("t_scar_tissue", woke);

        // And it is awakened ONCE. A second pass says nothing, which is what keeps the reveal rare.
        Assert.Empty(TraitDiscovery.Evaluate(ledger, TraitAccount.None, Somewhere));
        Assert.True(ledger.Has("t_scar_tissue"));
    }

    [Fact]
    public void test_every_trait_in_the_catalogue_can_actually_be_earned()
    {
        // Drive each rule's own counter to its own threshold and check the trait awakens. A rule
        // nothing can satisfy is a trait that does not exist, however good the card reads.
        foreach (var def in TraitCatalogue.All)
        {
            var ledger = new TraitLedger();
            var account = TraitAccount.None;
            switch (def.Discovery.Counter)
            {
                case TraitCounter.BossesFelled: account = account with { BossesFelled = (int)def.Discovery.Threshold }; break;
                case TraitCounter.RunsWithVowKept: account = account with { RunsWithVowKept = (int)def.Discovery.Threshold }; break;
                case TraitCounter.SetsCompleted: account = account with { SetsCompleted = (int)def.Discovery.Threshold }; break;
                case TraitCounter.RegionsConquered: account = account with { RegionsConquered = (int)def.Discovery.Threshold }; break;
                case TraitCounter.RegionsMastered: account = account with { RegionsMastered = (int)def.Discovery.Threshold }; break;
                case TraitCounter.WallStreak: account = account with { WallStreak = (int)def.Discovery.Threshold }; break;
                default: ledger.Add(def.Discovery.Counter, def.Discovery.Threshold); break;
            }

            var woke = TraitDiscovery.Evaluate(ledger, account, Somewhere);
            Assert.True(woke.Contains(def.Id),
                $"{def.Name} did not awaken at its own threshold ({def.Discovery.Counter} = {def.Discovery.Threshold}).");
        }
    }

    [Fact]
    public void test_a_counter_one_step_short_awakens_nothing()
    {
        // The other half of the rule: the threshold is a real edge, not decoration.
        var ledger = new TraitLedger();
        ledger.Add(TraitCounter.ShieldBreaks, 39);
        Assert.Empty(TraitDiscovery.Evaluate(ledger, TraitAccount.None, Somewhere));
        ledger.Add(TraitCounter.ShieldBreaks, 1);
        Assert.Contains("t_scar_tissue", TraitDiscovery.Evaluate(ledger, TraitAccount.None, Somewhere));
    }

    [Fact]
    public void test_a_counter_never_goes_backwards()
    {
        // Monotone is what makes "nothing can be missed" true. A negative contribution is IGNORED,
        // never applied — an account cannot be walked back into having not earned something.
        var ledger = new TraitLedger();
        ledger.Add(TraitCounter.CreaturesKilled, 100);
        ledger.Add(TraitCounter.CreaturesKilled, -100);
        ledger.Add(TraitCounter.CreaturesKilled, double.NaN);
        Assert.Equal(100d, ledger.Of(TraitCounter.CreaturesKilled));
    }

    // ── ACCOUNT-WIDE DISCOVERY, PER-CHARACTER LOADOUT (§25, §26, LAWS 6-7) ────────────────────────

    [Fact]
    public void test_discovery_is_account_wide_and_the_loadout_is_per_character()
    {
        // The failure §25 names is ten parallel careers: a second champion re-earning the collection.
        var ledger = Everything();

        Assert.True(ledger.Equip("seeker", "t_last_word"));
        Assert.True(ledger.Equip("seeker", "t_scar_tissue"));
        Assert.True(ledger.Equip("magpie", "t_spill"));

        // One discovered set, shared.
        Assert.True(ledger.Has("t_last_word"));
        Assert.Equal(TraitCatalogue.All.Count, ledger.DiscoveredCount);

        // Two different threes.
        Assert.Equal(new[] { "t_last_word", "t_scar_tissue" }, ledger.LoadoutOf("seeker"));
        Assert.Equal(new[] { "t_spill" }, ledger.LoadoutOf("magpie"));
        Assert.False(ledger.IsEquipped("magpie", "t_last_word"));
    }

    [Fact]
    public void test_exactly_three_traits_are_worn_and_the_cap_lives_below_the_screen()
    {
        // The three slots ARE the opportunity cost (§35) — almost none of the twenty-six carries a
        // downside — so the cap has to be real rather than a rule the UI politely follows.
        var ledger = Everything();
        var ids = TraitCatalogue.All.Take(4).Select(t => t.Id).ToList();

        Assert.True(ledger.Equip("seeker", ids[0]));
        Assert.True(ledger.Equip("seeker", ids[1]));
        Assert.True(ledger.Equip("seeker", ids[2]));
        Assert.False(ledger.Equip("seeker", ids[3]));
        Assert.Equal(3, ledger.LoadoutOf("seeker").Count);
        Assert.Equal(3, TraitCatalogue.SlotsPerCharacter);

        // No duplicate, on the same path the duplicate-skill rule lives on.
        Assert.False(ledger.Equip("seeker", ids[0]));
        Assert.Equal(3, ledger.LoadoutOf("seeker").Count);
    }

    [Fact]
    public void test_an_undiscovered_trait_cannot_be_worn()
    {
        var ledger = new TraitLedger();
        Assert.False(ledger.Equip("seeker", "t_last_word"));
        Assert.False(ledger.EquipInto("seeker", 0, "t_last_word"));
        Assert.Empty(ledger.LoadoutOf("seeker"));

        // And an id from nowhere is refused rather than stored.
        Assert.False(ledger.Discover("t_not_a_trait", Somewhere));
        Assert.Equal(0, ledger.DiscoveredCount);
    }

    [Fact]
    public void test_swapping_is_free_and_reversible_and_the_discovery_is_untouched()
    {
        // §27: nothing is spent, nothing is lost, and taking a trait off does not un-awaken it.
        var ledger = Everything();
        ledger.Equip("seeker", "t_last_word");

        Assert.True(ledger.Unequip("seeker", "t_last_word"));
        Assert.True(ledger.Has("t_last_word"));
        Assert.True(ledger.Equip("seeker", "t_last_word"));
        Assert.True(ledger.IsEquipped("seeker", "t_last_word"));

        // The screen's one button, both ways.
        Assert.True(ledger.Toggle("seeker", "t_last_word"));
        Assert.False(ledger.IsEquipped("seeker", "t_last_word"));
        Assert.True(ledger.Toggle("seeker", "t_last_word"));
        Assert.True(ledger.IsEquipped("seeker", "t_last_word"));
    }

    [Fact]
    public void test_putting_a_trait_into_a_full_slot_replaces_it_and_never_makes_a_fourth()
    {
        var ledger = Everything();
        var ids = TraitCatalogue.All.Take(4).Select(t => t.Id).ToList();
        ledger.Equip("seeker", ids[0]);
        ledger.Equip("seeker", ids[1]);
        ledger.Equip("seeker", ids[2]);

        Assert.True(ledger.EquipInto("seeker", 1, ids[3]));
        Assert.Equal(new[] { ids[0], ids[3], ids[2] }, ledger.LoadoutOf("seeker"));
        Assert.Equal(3, ledger.LoadoutOf("seeker").Count);

        // Moving one that is already worn is a MOVE, not a second copy.
        Assert.True(ledger.EquipInto("seeker", 2, ids[0]));
        Assert.Equal(3, ledger.LoadoutOf("seeker").Count);
        Assert.Equal(ledger.LoadoutOf("seeker").Distinct().Count(), ledger.LoadoutOf("seeker").Count);
    }

    // ── THE SAVE (§96) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_account_survives_a_save_and_a_load_unchanged()
    {
        var before = Everything();
        before.Add(TraitCounter.ShieldBreaks, 41);
        before.Add(TraitCounter.OverkillPools, 9.5);
        before.Equip("seeker", "t_last_word");
        before.Equip("magpie", "t_spill");
        before.Equip("magpie", "t_scar_tissue");

        var after = new TraitLedger();
        after.Restore(before.Discovered, before.SaveTally(), before.SaveLoadouts(), before.SaveProvenance());

        Assert.Equal(before.Discovered, after.Discovered);
        Assert.Equal(41d, after.Of(TraitCounter.ShieldBreaks));
        Assert.Equal(9.5d, after.Of(TraitCounter.OverkillPools));
        Assert.Equal(before.LoadoutOf("seeker"), after.LoadoutOf("seeker"));
        Assert.Equal(before.LoadoutOf("magpie"), after.LoadoutOf("magpie"));
        Assert.Equal(before.FirstOf("t_last_word"), after.FirstOf("t_last_word"));
    }

    [Fact]
    public void test_a_save_is_an_input_like_any_other_and_is_re_clamped_on_the_way_in()
    {
        // A hand-edited or older save must not be able to put four traits on a champion, wear one
        // that was never awakened, or grow the ledger a counter this build does not know about.
        var ledger = new TraitLedger();
        ledger.Restore(
            new[] { "t_last_word", "t_spill", "t_scar_tissue", "t_no_such_trait" },
            new[]
            {
                new KeyValuePair<string, double>("ShieldBreaks", 12d),
                new KeyValuePair<string, double>("SomeCounterFromTheFuture", 900d),
            },
            new[]
            {
                ("seeker", (IReadOnlyList<string>)new List<string>
                    { "t_last_word", "t_spill", "t_scar_tissue", "t_deep_cut", "t_last_word" }),
            });

        Assert.Equal(3, ledger.DiscoveredCount);
        Assert.False(ledger.Has("t_no_such_trait"));
        Assert.Equal(12d, ledger.Of(TraitCounter.ShieldBreaks));
        // Three, no duplicate, and not the one that was never awakened.
        Assert.Equal(new[] { "t_last_word", "t_spill", "t_scar_tissue" }, ledger.LoadoutOf("seeker"));
    }

    [Fact]
    public void test_an_established_save_awakens_exactly_the_six_account_fed_traits_and_no_more()
    {
        // §96: "do not automatically unlock all new Traits because the old tree was progressed." Six
        // rules read facts every established save already carries, and those six — and ONLY those —
        // are the explicit mapping §96 allows. The host shows them as one combined plate rather than
        // six ceremonies in a second, which is the other half of §32.
        var ledger = new TraitLedger();
        var established = new TraitAccount(
            BossesFelled: 400, RunsWithVowKept: 50, SetsCompleted: 4,
            RegionsConquered: 6, RegionsMastered: 4, WallStreak: 5);

        var woke = TraitDiscovery.Evaluate(ledger, established, Somewhere);

        Assert.Equal(
            new[] { "t_last_word", "t_kept_word", "t_matched_suit", "t_homeground", "t_studied_place", "t_what_killed_you" }
                .OrderBy(x => x, StringComparer.Ordinal),
            woke.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void test_a_brand_new_account_awakens_nothing_at_all()
    {
        Assert.Empty(TraitDiscovery.Evaluate(new TraitLedger(), TraitAccount.None, Somewhere));
    }

    // ── THE ONE FAILURE-FED RULE ─────────────────────────────────────────────────────────────────

    [Fact]
    public void test_what_killed_you_counts_three_deaths_in_one_region_to_one_kind_of_creature()
    {
        RunReport Fell(string region, Archetype wall, WaveOutcome outcome = WaveOutcome.Wiped) => new()
        {
            RegionId = region, Depth = 20, IsRecord = false, Outcome = outcome,
            WallWave = 20, WallArchetype = wall, WallAffixes = Array.Empty<Affix>(), WallCreatures = 3,
            AbsorbedFraction = 0f, AverageHitSize = 1f, TargetsPerActivation = 1f, CreaturesPerWave = 3f,
            HealthLostPerWaveFraction = 0.1f, SecondsPerWave = 10f, SampledWaves = 20,
        };

        // Three in a row, one region, one wall.
        var (streak, wall) = TraitDiscovery.WallStreakOf(new[]
        {
            Fell("cinderworks", Archetype.Armoured),
            Fell("cinderworks", Archetype.Armoured),
            Fell("cinderworks", Archetype.Armoured),
        });
        Assert.Equal(3, streak);
        Assert.Equal(Archetype.Armoured, wall);

        // A different REGION breaks it — "the armoured of one place because the armoured of another
        // killed you" is the sentence this rule refuses to say.
        Assert.Equal(2, TraitDiscovery.WallStreakOf(new[]
        {
            Fell("cinderworks", Archetype.Armoured),
            Fell("cinderworks", Archetype.Armoured),
            Fell("marrow", Archetype.Armoured),
        }).Streak);

        // A different KIND breaks it.
        Assert.Equal(1, TraitDiscovery.WallStreakOf(new[]
        {
            Fell("cinderworks", Archetype.Armoured),
            Fell("cinderworks", Archetype.Swarm),
        }).Streak);

        // And a run that did NOT end in a death is not a wall at all.
        Assert.Equal(0, TraitDiscovery.WallStreakOf(new[]
        {
            Fell("cinderworks", Archetype.Armoured, WaveOutcome.Cleared),
            Fell("cinderworks", Archetype.Armoured),
        }).Streak);
    }

    // ── NOTHING LEAKS THE CONDITION (§28, §39, §40, LAW 8) ────────────────────────────────────────

    [Fact]
    public void test_no_player_facing_string_names_a_counter_or_a_threshold()
    {
        // The player never sees a condition, a counter or a progress figure. The way to guarantee it
        // is that the rule is not in anything the screen renders — so the three strings a trait shows
        // are checked against its own rule here, and the screen has no third place to get it from.
        var counterNames = Enum.GetNames<TraitCounter>();

        foreach (var def in TraitCatalogue.All)
        {
            var shown = def.Name + " " + def.Line + " " + def.Flavour + " " + TraitCatalogue.TagName(def.Tag);

            foreach (var counter in counterNames)
                Assert.DoesNotContain(counter, shown, StringComparison.OrdinalIgnoreCase);

            // The threshold is the number a checklist would show. It may not appear in the copy —
            // which also rules out "40 / 40" and "absorb 20 pools" ever being written into a card.
            var threshold = ((long)def.Discovery.Threshold).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (threshold.Length >= 2)
                Assert.DoesNotContain(threshold, shown, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void test_a_trait_costs_nothing_and_no_currency_can_reach_it()
    {
        // LAW 5, held structurally: there is no cost on a TraitDef to read, so no screen can offer
        // one for sale even by accident. If a price is ever added, this test is where it is argued.
        var priced = typeof(TraitDef).GetProperties()
            .Select(p => p.Name)
            .Where(n => n.Contains("Cost", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Price", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Dust", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Point", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(priced.Count == 0, $"A trait has acquired a price: {string.Join(", ", priced)}.");
    }

    // ── THE ANTI-DORMANCY GUARD ──────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_fight_fed_counter_actually_rises_when_a_wave_feeds_the_ledger()
    {
        // THE MOST IMPORTANT TEST IN THIS FILE. A rule reading a counter nothing writes is a trait
        // that can never be earned, which is this project's named failure wearing a new coat — and it
        // had already happened once here: StyleActivations[Style.Sign] is zero for every build ever
        // run, because every SIGN skill is an Amplify that lands nothing, so THE LINGERING MARK was
        // unearnable until WaveMetrics.MarkCasts was added. One busy wave, and every counter the
        // fight is supposed to feed must move.
        var ledger = new TraitLedger();
        TraitDiscovery.OnWaveCleared(ledger, BusyWave(), TraitAccount.None, Somewhere);

        var accountFed = new[]
        {
            TraitCounter.BossesFelled, TraitCounter.RunsWithVowKept, TraitCounter.SetsCompleted,
            TraitCounter.RegionsConquered, TraitCounter.RegionsMastered, TraitCounter.WallStreak,
        };

        foreach (var counter in Enum.GetValues<TraitCounter>())
        {
            if (accountFed.Contains(counter)) continue;
            Assert.True(ledger.Of(counter) > 0d,
                $"{counter} stayed at zero after a wave that did everything — no rule reading it can " +
                "ever be satisfied. Either the fight is not writing it or the wave facts are not carrying it.");
        }
    }

    [Fact]
    public void test_every_counter_in_the_closed_set_is_read_by_at_least_one_trait()
    {
        // The other direction: a counter no rule reads is an accumulator nobody spends, which is the
        // same dead weight in the save that the trait-point economy was.
        var read = TraitCatalogue.All.Select(t => t.Discovery.Counter).Distinct().ToList();
        var unread = Enum.GetValues<TraitCounter>().Except(read).ToList();
        Assert.True(unread.Count == 0,
            $"These counters are accumulated and never read: {string.Join(", ", unread)}.");
    }
}
