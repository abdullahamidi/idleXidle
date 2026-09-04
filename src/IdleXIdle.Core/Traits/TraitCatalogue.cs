using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Core.Traits;

/// <summary>What a trait is ABOUT — the one word the collection groups it under.</summary>
/// <remarks>
/// Not a filter for its own sake: the catalogue covers sixteen behaviours on purpose (BRIEF §36), and
/// a tag is how the screen can show that without a checklist. It never appears on an undiscovered
/// trait — an unknown trait's view model carries nothing at all (LAW 8).
/// </remarks>
public enum TraitTag
{
    HeavyHits, Waste, Reach, Critical, Shield, LowHealth, Healing, Reflect,
    Kills, Marks, Vows, Elements, Sets, Regions, Failure, Survival,
}

/// <summary>
/// The account facts and world facts a trait may read when it is COMPOSED — never inside the fight.
/// </summary>
/// <remarks>
/// Four traits are conditional on the world rather than on the wave: THE MATCHED SUIT reads the set
/// being worn, HOMEGROUND and THE STUDIED PLACE read the region, WHAT KILLED YOU reads the last three
/// descents. Resolving them here is what keeps <see cref="SoloBattle"/> from learning about regions,
/// sets or the run log — the fight receives a plain number and cannot tell where it came from.
/// </remarks>
/// <param name="RegionConquered">Has the region this run is walking already been conquered?</param>
/// <param name="RegionsMastered">How many regions stand at full mastery.</param>
/// <param name="SetElement">The element of the five-piece set being worn, if one is complete.</param>
/// <param name="LastWallArchetype">The kind of creature that ended the last three descents, if they agree.</param>
public readonly record struct TraitContext(
    bool RegionConquered = false,
    int RegionsMastered = 0,
    Source? SetElement = null,
    Archetype? LastWallArchetype = null)
{
    /// <summary>No world at all — a probe, or a bench. Every world-conditional trait composes to nothing.</summary>
    public static TraitContext None => new();
}

/// <summary>
/// A trait's hidden discovery rule: ONE counter and ONE number.
/// </summary>
/// <remarks>
/// <para>
/// <b>Data, not a lambda</b>, and that is the whole of LAW 8. A rule that is a key and a threshold is
/// deterministic by construction — there is no draw to make, no branch to take and nothing to be
/// unlucky about (§30). It is also inspectable, which is what lets the test suite prove every rule is
/// monotone and reachable rather than believe it.
/// </para>
/// <para>
/// The player never sees either half. <see cref="TraitCounter"/> and this threshold live in Core and
/// never reach a view model; an undiscovered trait's view model carries no fields at all, so there is
/// nothing a screen COULD leak (§28, §39, §40).
/// </para>
/// </remarks>
/// <param name="Counter">Which accumulated fact this rule reads.</param>
/// <param name="Threshold">The value at or above which the trait awakens.</param>
public readonly record struct TraitRule(TraitCounter Counter, double Threshold);

/// <summary>
/// The closed set of facts a discovery rule may read. Sixteen fed by the fight, six by the account.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed on purpose.</b> §29 says "do not store full combat history when a bounded counter is
/// sufficient", and the way to keep that true is to make the ledger a dictionary whose key set is
/// declared here: a key not on this list is dropped on load, so a rule cannot quietly grow a
/// twenty-third accumulator that nobody migrates.
/// </para>
/// <para>
/// Every one is MONOTONE — nothing ever decreases — so no trait can be lost, and no rule needs a
/// window, a streak that resets, or a moment the player has to be present for. The one apparent
/// exception, <see cref="WallStreak"/>, is derived fresh from the saved run log rather than
/// accumulated, and is the deliberate failure-fed rule §36 asks for.
/// </para>
/// </remarks>
public enum TraitCounter
{
    // ── Fed by the fight, one wave at a time (TraitDiscovery.OnWaveCleared) ──────────────────────

    /// <summary>Hits worth <see cref="SoloBattle.HeavyHitFraction"/> or more of what they struck.</summary>
    HeavyHits,
    /// <summary>Damage wasted past a kill, counted in champion pools.</summary>
    OverkillPools,
    /// <summary>Shield GRANTED, in pools. Not the same fact as shield spent.</summary>
    ShieldGainedPools,
    /// <summary>Times the shield reached zero under a bite.</summary>
    ShieldBreaks,
    /// <summary>Shield SPENT against bites, in pools.</summary>
    ShieldAbsorbedPools,
    /// <summary>Health restored inside a fight, in pools.</summary>
    HealedPools,
    /// <summary>Damage sent back at biters, in pools.</summary>
    ReflectedPools,
    /// <summary>Waves cleared having fallen to a fifth of the pool or below.</summary>
    BrinkWaves,
    /// <summary>Waves cleared without losing a point of health.</summary>
    UntouchedWaves,
    /// <summary>Waves cleared carrying a skill that reaches three or more creatures.</summary>
    WideWaves,
    /// <summary>Waves cleared with critical chance at or above <see cref="TraitDiscovery.HighCritPercent"/>.</summary>
    HighCritWaves,
    /// <summary>Creatures killed.</summary>
    CreaturesKilled,
    /// <summary>Marks cast — every Amplify skill, counted where its window opens.</summary>
    MarkCasts,
    /// <summary>Waves cleared while keeping two or more vows at once.</summary>
    ManyVowWaves,
    /// <summary>Waves cleared with every woven skill on one element.</summary>
    PureWaves,
    /// <summary>Waves cleared carrying four or more distinct elements.</summary>
    MotleyWaves,

    // ── Read off the account, which already keeps them (TraitDiscovery evaluates on every pass) ──

    /// <summary>Bosses felled. A boss wave is always one creature.</summary>
    BossesFelled,
    /// <summary>Descents finished with a vow's demand still met.</summary>
    RunsWithVowKept,
    /// <summary>Five-piece element sets completed.</summary>
    SetsCompleted,
    /// <summary>Regions conquered.</summary>
    RegionsConquered,
    /// <summary>Regions brought to full mastery.</summary>
    RegionsMastered,
    /// <summary>How many of the most recent descents ended in one region against one kind of creature.</summary>
    WallStreak,
}

/// <summary>
/// ONE characteristic: a sentence, a dial, a hidden rule, and the flavour its awakening is announced with.
/// </summary>
/// <param name="Id">The stable save id. Never shown.</param>
/// <param name="Name">What the player calls it.</param>
/// <param name="Line">WHAT IT DOES, in one plain sentence the player can hold.</param>
/// <param name="Flavour">The line the awakening plate says under the name.</param>
/// <param name="Tag">The behaviour it belongs to.</param>
/// <param name="Discovery">The hidden rule. Data, and never rendered.</param>
/// <param name="Shape">
/// What equipping it contributes to the build, given the world. Almost every trait ignores the
/// context entirely and returns a fixed set of dials; the four world-conditional ones read it.
/// </param>
public sealed record TraitDef(
    string Id,
    string Name,
    string Line,
    string Flavour,
    TraitTag Tag,
    TraitRule Discovery,
    Func<TraitContext, SkillShape> Shape);

/// <summary>
/// THE TWENTY-SIX TRAITS — characteristics the account discovers by living, never buys.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a second mastery tree</b> (BRIEF §22, §34, LAW 9). Not one of the twenty-six is a
/// bare multiplier on damage, health, rate, haul or rarity: every one is a rule with a condition — a
/// threshold on a hit, a position in the wave, a state you are standing in, a world fact, or a
/// relationship between two things the fight already tracks. Training, gear and mastery own broad
/// numeric scaling, and a trait that read no condition would belong on the mastery tree instead.
/// </para>
/// <para>
/// <b>Discovery is account-wide; the loadout is per character</b> (§25, §26, LAWS 6-7). Nothing here
/// carries a cost, and no currency reads it: <see cref="TraitDiscovery"/> is the only writer of the
/// discovered set. Three slots per champion are the opportunity cost (§35), which is why almost none
/// of these carries a downside.
/// </para>
/// <para>
/// <b>Twenty-six, not thirty.</b> The design drafted thirty; four were cut as the design's own
/// cheapest — THE LONG SWING (the closest thing in it to a bare multiplier), DRUMBEAT (a ramp
/// SETTLING WEIGHT already provides), THE RETURNED BLOW (a third shield trait) and THE SETTLED
/// GROUND. Twenty-six still covers every one of the sixteen behaviours §36 lists.
/// </para>
/// </remarks>
public static class TraitCatalogue
{
    /// <summary>Exactly three characteristics may be worn at once (§26, LAW 7).</summary>
    public const int SlotsPerCharacter = 3;

    /// <summary>A trait whose dials never look at the world — which is twenty-two of the twenty-six.</summary>
    private static Func<TraitContext, SkillShape> Fixed(TraitRules rules)
    {
        var shape = SkillShape.None with { Traits = rules };
        return _ => shape;
    }

    private static TraitRule At(TraitCounter counter, double threshold) => new(counter, threshold);

    public static IReadOnlyList<TraitDef> All { get; } = new List<TraitDef>
    {
        // ── HEAVY HITS ───────────────────────────────────────────────────────────────────────────
        new("t_deep_cut", "DEEP CUT",
            "A hit that takes a fifth of an enemy's health breaks 12 defence off it for the rest of the wave.",
            "What is struck hard enough stops closing.",
            TraitTag.HeavyHits,
            // You awaken it by hitting hard enough to matter; what awakens is hitting hard enough to
            // break plate. The fight counts a heavy hit whether or not the trait is worn — the
            // threshold is SoloBattle.HeavyHitFraction, a constant, not one of the trait's own dials.
            At(TraitCounter.HeavyHits, 250),
            Fixed(new TraitRules(HeavyHitArmourStrip: 12f))),

        // ── WASTE ────────────────────────────────────────────────────────────────────────────────
        new("t_spill", "THE SPILL",
            "A quarter of the damage wasted past a kill comes back to you as shield.",
            "Nothing is wasted that the body is willing to keep.",
            TraitTag.Waste,
            // Waste teaches the body to keep something back.
            At(TraitCounter.OverkillPools, 8),
            Fixed(new TraitRules(OverkillToShield: 0.25f))),

        new("t_carrion_weight", "CARRION WEIGHT",
            "Half of the damage wasted past a kill bleeds into the rest of the wave.",
            "What falls does not stop falling.",
            TraitTag.Waste,
            // The deeper rung of the same lesson: the waste stops falling on the floor and starts
            // falling on the wave.
            At(TraitCounter.OverkillPools, 25),
            Fixed(new TraitRules(OverkillToBleed: 0.5f))),

        // ── REACH ────────────────────────────────────────────────────────────────────────────────
        new("t_spreading_fire", "SPREADING FIRE",
            "A cast that already reaches three enemies reaches one more.",
            "A fire that has reached three has already reached the fourth.",
            TraitTag.Reach,
            // Committing to reach awakens one more creature of reach.
            At(TraitCounter.WideWaves, 200),
            Fixed(new TraitRules(WideCastAtTargets: 3, WideCastExtraTargets: 1))),

        // ── CRITICAL ─────────────────────────────────────────────────────────────────────────────
        new("t_certain_hand", "THE CERTAIN HAND",
            "The first skill hit of each wave always lands a critical hit, and hits twice as hard as a critical usually does.",
            "Once a wave, the hand does not guess.",
            TraitTag.Critical,
            // Investing in chance awakens one hit a wave where chance does not apply.
            At(TraitCounter.HighCritWaves, 30),
            Fixed(new TraitRules(FirstHitOfWaveCritMultiplier: 2f))),

        new("t_opened_vein", "THE OPENED VEIN",
            "Your critical hits leave the enemy bleeding.",
            "The cut you meant to make goes on being made.",
            TraitTag.Critical,
            // Once criticals are your whole game, they stop being only damage.
            At(TraitCounter.HighCritWaves, 100),
            Fixed(new TraitRules(CritToBleed: 0.5f))),

        // ── SHIELD ───────────────────────────────────────────────────────────────────────────────
        new("t_scar_tissue", "SCAR TISSUE",
            "When your shield breaks, the next shield you gain this wave is 50% larger.",
            "The body remembers what survives.",
            TraitTag.Shield,
            // Breaking teaches re-armouring.
            At(TraitCounter.ShieldBreaks, 40),
            Fixed(new TraitRules(ShieldAfterBreakBonus: 0.5f))),

        new("t_standing_plate", "STANDING PLATE",
            "Half of the shield you are still holding when a wave ends is carried into the next one.",
            "Plate does not forget the shape it was beaten into.",
            TraitTag.Shield,
            // Absorbing a great deal awakens something shield-shaped. HALF the carry, not all of it:
            // ShieldRules states that a shield accumulating while nothing happens would make standing
            // still the strongest defensive play, and halving keeps that invariant nearly intact.
            At(TraitCounter.ShieldAbsorbedPools, 20),
            Fixed(new TraitRules(ShieldCarryFraction: 0.5f))),

        new("t_answering_wall", "THE ANSWERING WALL",
            "While you hold a shield, every enemy that bites you takes back a tenth of its own bite.",
            "A wall that has been struck long enough learns to strike.",
            TraitTag.Shield,
            // Wearing plate long enough awakens plate that answers.
            At(TraitCounter.ShieldGainedPools, 30),
            Fixed(new TraitRules(ShieldedReflectFraction: 0.10f))),

        // ── LOW HEALTH ───────────────────────────────────────────────────────────────────────────
        new("t_last_breath", "LAST BREATH",
            "At or below a quarter of your health, your skills come back a third sooner.",
            "Nothing is quicker than the thing that is nearly gone.",
            TraitTag.LowHealth,
            // Living at the brink teaches you to act faster there.
            At(TraitCounter.BrinkWaves, 25),
            Fixed(new TraitRules(LowHealthShare: 0.25f, LowHealthRateBonus: 0.33f))),

        new("t_thin_line", "THE THIN LINE",
            "The first bite that would kill you each wave deals half instead.",
            "There is always one blow you do not take whole.",
            TraitTag.LowHealth,
            // The deeper rung: the body stops merely acting faster and starts flinching from the blow.
            At(TraitCounter.BrinkWaves, 60),
            Fixed(new TraitRules(LethalBiteShare: 0.5f))),

        // ── HEALING ──────────────────────────────────────────────────────────────────────────────
        new("t_practised_flesh", "PRACTISED FLESH",
            "Every heal raises this wave's healing limit by 1%, up to a quarter more.",
            "Flesh that has been mended often mends further.",
            TraitTag.Healing,
            // Healing a great deal awakens room to be healed more.
            At(TraitCounter.HealedPools, 25),
            Fixed(new TraitRules(HealCeilingPerHeal: 0.01f, HealCeilingPerHealCap: 0.25f))),

        new("t_given_hand", "THE GIVEN HAND",
            "Healing you cannot use at full health is thrown at the enemy instead.",
            "What you cannot hold, you give away hard.",
            TraitTag.Healing,
            // Past the point where more healing helps, healing has to go somewhere else.
            At(TraitCounter.HealedPools, 50),
            Fixed(new TraitRules(OverhealToDamage: 1f))),

        // ── REFLECT ──────────────────────────────────────────────────────────────────────────────
        new("t_mirror", "THE MIRROR",
            "Every bite you have taken this wave makes your reflected damage 15% stronger, up to double.",
            "Every blow returned teaches the return.",
            TraitTag.Reflect,
            // Reflecting a great deal awakens a deeper reflection.
            At(TraitCounter.ReflectedPools, 6),
            Fixed(new TraitRules(ReflectRampPerBite: 0.15f, ReflectRampCap: 1f))),

        // ── KILLS ────────────────────────────────────────────────────────────────────────────────
        new("t_settling_weight", "SETTLING WEIGHT",
            "Every enemy that has died this wave makes your hits 6% heavier, up to a third.",
            "The dead make the ground firmer to swing from.",
            TraitTag.Kills,
            // Emptying waves awakens a reward that grows as a wave empties.
            At(TraitCounter.CreaturesKilled, 2_000),
            Fixed(new TraitRules(PowerPerDeadEnemy: 0.06f, PowerPerDeadCap: 0.33f))),

        new("t_last_word", "LAST WORD",
            "You deal 30% more damage to the last enemy left alive in a wave.",
            "The one left standing is the one you have been practising on.",
            TraitTag.Kills,
            // A boss wave is always one creature: sixty fights that were only ever the last enemy
            // alive awaken an edge against the last enemy alive.
            At(TraitCounter.BossesFelled, 60),
            Fixed(new TraitRules(LastEnemyBonus: 0.30f))),

        // ── MARKS ────────────────────────────────────────────────────────────────────────────────
        new("t_lingering_mark", "THE LINGERING MARK",
            "A mark you cast holds one second longer for every enemy alive when it opens.",
            "A mark drawn before a crowd is slow to fade.",
            TraitTag.Marks,
            // Keeping a mark up awakens a mark that keeps itself up. Counted off WaveMetrics.MarkCasts
            // and NOT off StyleActivations[Sign]: that ledger is written inside LandSpread, so only a
            // cast that deals something reaches it, and every SIGN skill in the catalogue is an
            // Amplify that deals nothing — the rule would have been unsatisfiable by any build.
            At(TraitCounter.MarkCasts, 200),
            Fixed(new TraitRules(AmplifyWindowPerEnemyMs: 1_000))),

        // ── VOWS ─────────────────────────────────────────────────────────────────────────────────
        //
        // A vow is a promise about the WHOLE BUILD: sworn once, judged once against everything the
        // hunter is carrying, and paid once. Both traits here are written to that, and both pay only
        // for a promise being KEPT — which is the law the entire vow system rests on.
        new("t_kept_word", "THE KEPT WORD",
            "If you have sworn no vows, the weakest vow you have found still holds you, and still pays.",
            "You kept your word so long that a promise holds you without being spoken.",
            TraitTag.Vows,
            // Keeping your word, descent after descent, awakens a word that keeps you when you have
            // given none. It pays only a hunter who has sworn nothing at all, so it is a real decision
            // about the build rather than a free rider on a promise already made.
            At(TraitCounter.RunsWithVowKept, 8),
            Fixed(new TraitRules(UnswornBuildBorrowsWeakestVow: true))),

        new("t_weight_of_vows", "THE WEIGHT OF VOWS",
            "Each vow you are keeping after the first makes all of your vows pay 20% more.",
            "One promise is a rule. Three are a life.",
            TraitTag.Vows,
            // Carrying more than one promise at once awakens the reward for carrying them. A vow whose
            // rule the build is breaking counts for nothing here and pays nothing anywhere else, so
            // this can only be earned — and only ever paid — by promises actually kept. It needs the
            // vow capacity a milestone grants before it can be earned at all, which is the point: it
            // is the trait that makes the second and third promise worth the restriction they cost.
            At(TraitCounter.ManyVowWaves, 40),
            Fixed(new TraitRules(VowPayPerExtraKeptVow: 0.20f))),

        // ── ELEMENTS ─────────────────────────────────────────────────────────────────────────────
        new("t_single_note", "THE SINGLE NOTE",
            "If every skill you carry shares one element, your first cast each wave strikes twice.",
            "One voice, said twice, is louder than two.",
            TraitTag.Elements,
            // Committing to one voice awakens the voice saying it twice.
            At(TraitCounter.PureWaves, 60),
            Fixed(new TraitRules(OneSourceRepeatsFirstCast: true))),

        new("t_many_tongues", "MANY TONGUES",
            "Carrying four different elements makes every matchup a strong one.",
            "Speak to all of them and none of them answers badly.",
            TraitTag.Elements,
            // Speaking to every element awakens every element answering well.
            At(TraitCounter.MotleyWaves, 60),
            Fixed(new TraitRules(AllMatchupsStrongAtSources: 4))),

        // ── SETS ─────────────────────────────────────────────────────────────────────────────────
        new("t_matched_suit", "THE MATCHED SUIT",
            "Skills that share the element of the full set you are wearing deal 20% more damage.",
            "What you wear and what you do finally agree.",
            TraitTag.Sets,
            // The set already decided what you are; this makes your skills agree. The NARROW reading:
            // forcing every skill to the set's element would take the player's own choice away and
            // argue with PURE, THE SINGLE NOTE and all six element signatures at once.
            At(TraitCounter.SetsCompleted, 1),
            ctx => ctx.SetElement is { } element
                ? SkillShape.None with { Traits = new TraitRules(MatchedSuitSource: element, MatchedSuitBonus: 0.20f) }
                : SkillShape.None),

        // ── REGIONS ──────────────────────────────────────────────────────────────────────────────
        new("t_homeground", "HOMEGROUND",
            "In a region you have conquered, your first cast of each wave has no wait.",
            "You are not a visitor here any more.",
            TraitTag.Regions,
            // You are not a visitor here any more; the wave does not get its free breath.
            // REUSES SkillShape.FreeOpeningCast — the dial PREPARATION already turns, so this trait
            // adds no new field and no new read site.
            At(TraitCounter.RegionsConquered, 2),
            ctx => ctx.RegionConquered ? SkillShape.None with { FreeOpeningCast = true } : SkillShape.None),

        new("t_studied_place", "THE STUDIED PLACE",
            "For every region you have mastered, an enemy that dies leaves the wave biting 3% softer, down to a tenth softer.",
            "You have learned how the things here come apart.",
            TraitTag.Regions,
            // Knowing a place is knowing how its creatures come apart.
            At(TraitCounter.RegionsMastered, 1),
            ctx => ctx.RegionsMastered <= 0
                ? SkillShape.None
                : SkillShape.None with
                {
                    Traits = new TraitRules(
                        WeakenPerDeadEnemy: 0.03f * ctx.RegionsMastered,
                        WeakenPerDeadCap: 0.10f),
                }),

        // ── FAILURE ──────────────────────────────────────────────────────────────────────────────
        new("t_what_killed_you", "WHAT KILLED YOU",
            "The kind of enemy that ended your last descents takes 20% more damage from you.",
            "What beat you three times changed you.",
            TraitTag.Failure,
            // The purest statement of the whole system, and the one rule failure feeds. THREE
            // CONSECUTIVE DEATHS IN ONE REGION: across regions the sentence reads oddly — angry at
            // the armoured of one place because the armoured of another killed you.
            At(TraitCounter.WallStreak, 3),
            ctx => ctx.LastWallArchetype is { } wall
                ? SkillShape.None with { Traits = new TraitRules(GrudgeArchetype: wall, GrudgeBonus: 0.20f) }
                : SkillShape.None),

        // ── SURVIVAL ─────────────────────────────────────────────────────────────────────────────
        new("t_unbroken_thread", "THE UNBROKEN THREAD",
            "While you are at full health, the first bite of each wave is stopped outright.",
            "Whole is a thing you can stay.",
            TraitTag.Survival,
            // Going untouched awakens staying untouched.
            At(TraitCounter.UntouchedWaves, 30),
            Fixed(new TraitRules(PreventFirstBiteAtFullHealth: true))),
    };

    private static readonly Dictionary<string, TraitDef> ById =
        All.ToDictionary(t => t.Id, StringComparer.Ordinal);

    /// <summary>The trait with this id, or null. An id from an older save that no longer exists returns null.</summary>
    public static TraitDef? Find(string? id)
        => id is not null && ById.TryGetValue(id, out var def) ? def : null;

    /// <summary>The trait with this id. Throws — for fixtures and for code that has already checked.</summary>
    public static TraitDef ById_(string id) => ById[id];

    /// <summary>Is this a live catalogue id? The ledger drops everything else on load.</summary>
    public static bool Known(string? id) => id is not null && ById.ContainsKey(id);

    /// <summary>Catalogue order, which is the order the collection is drawn in. Unknown ids sort last.</summary>
    public static int OrderOf(string id)
    {
        for (var i = 0; i < All.Count; i++)
            if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return i;
        return int.MaxValue;
    }

    /// <summary>What the collection calls a tag. Plain words, no abbreviations.</summary>
    public static string TagName(TraitTag tag) => tag switch
    {
        TraitTag.HeavyHits => "HEAVY HITS",
        TraitTag.Waste => "WASTED DAMAGE",
        TraitTag.Reach => "REACH",
        TraitTag.Critical => "CRITICAL HITS",
        TraitTag.Shield => "SHIELD",
        TraitTag.LowHealth => "LOW HEALTH",
        TraitTag.Healing => "HEALING",
        TraitTag.Reflect => "REFLECTED DAMAGE",
        TraitTag.Kills => "KILLS",
        TraitTag.Marks => "MARKS",
        TraitTag.Vows => "VOWS",
        TraitTag.Elements => "ELEMENTS",
        TraitTag.Sets => "GEAR SETS",
        TraitTag.Regions => "REGIONS",
        TraitTag.Failure => "FAILURE",
        TraitTag.Survival => "SURVIVAL",
        _ => "",
    };
}
