using System;
using System.Globalization;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Quests;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Traits;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// The WORDS of a dispatch, rendered at display time from the catalogues. Never persisted: a row
/// stores the event and its subject, and the catalogue that owns the subject says what it is called
/// — so a renamed trait reads by its new name, and a retired one reads plainly instead of as an id.
/// </summary>
/// <remarks>
/// <para>
/// A headline is uppercase, like every notice in the game. A body is a sentence the surface may wrap,
/// and every kind reads as one: Unlock, Gem, Trait, Champion, Quest, Region, Socket and Migration
/// carry lines that were always prose, and the three that used to shout do not any more.
/// </para>
/// <para>
/// <b>Keystone, Vow and Set bodies are written here from TYPED data, not copied from a catalogue
/// string.</b> <c>Keystone.Blurb</c> and <c>ElementSets.CapstoneName</c> are authored ALL-CAPS for the
/// BUILD and GEAR screens, which still print them as written (<c>Vow.ProofLine</c> and
/// <c>Vow.RevealLine</c> were written for the retired toast and are dormant: nothing renders them); a
/// letter that pasted them in would sit beside letters that speak, in one ink and one size, and shout.
/// So <see cref="DispatchKind.Keystone"/> renders a rung sentence, then the gift AND the cost, then a
/// pointer (<see cref="KeystoneRevealDetail"/>); <see cref="DispatchKind.Vow"/> renders how the Vow
/// was earned — the granted rule, the waves cleared with the demand kept and the temptation refused,
/// or the conduct that proved a static Vow — then a pointer; <see cref="DispatchKind.Set"/> renders
/// the worn count, the capstone's name and the capstone rung's own sentence, which the set catalogue
/// already writes as prose. Proper nouns stay as the catalogue spells them (VERDANT HOLLOW, GLASS
/// CANNON, BUILD); nothing is lower-cased, and the catalogue strings are not touched.
/// </para>
/// <para>
/// <b>Every number in a body is formatted from the field the fight reads</b> — <c>Keystone.Mods</c>,
/// <c>Vow.ProofWaves</c>, <c>Vow.Threshold</c>, <see cref="Vows.Multiplier"/>, the CHARGE constants
/// on <see cref="SoloBattle"/> — never retyped from a blurb, so a retune cannot leave the letter lying.
/// The keystone sentences are a per-id table (nineteen shapes of trade do not fall out of five
/// multipliers), and a keystone the table does not know falls back to its Blurb, which the liveness
/// test refuses; a retired subject still reads plainly and never as an id.
/// </para>
/// <para>
/// Nothing here promises a price the screen it points at could refuse — the first gem's free socket
/// is a live rule the Forge states itself.
/// </para>
/// </remarks>
public static class DispatchCopy
{
    /// <summary>The one line that says what happened. Never empty, never a raw id.</summary>
    public static string Headline(Dispatch d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return d.Kind switch
        {
            DispatchKind.Unlock => ScreenOf(d) is { } screen ? $"NEW — {Unlocks.Headline(screen)}" : "A NEW SCREEN OPENED",
            DispatchKind.Gem => "A GEM DROPPED",
            DispatchKind.Trait => TraitCatalogue.Find(d.SubjectId) is { } t ? $"A TRAIT HAS AWAKENED — {t.Name}" : "A TRAIT HAS AWAKENED",
            DispatchKind.Vow => VowHeadline(d),
            DispatchKind.Keystone => Keystones.ById(d.SubjectId) is { } k ? $"NEW KEYSTONE — {k.Name}" : "A NEW KEYSTONE",
            DispatchKind.Champion => CharacterRoster.Find(d.SubjectId ?? "") is { } c ? $"{c.Name} JOINS YOU" : "A HUNTER JOINS YOU",
            DispatchKind.Quest => QuestCatalogue.Find(d.SubjectId) is { } q ? $"QUEST COMPLETE — {q.Name}" : "QUEST COMPLETE",
            DispatchKind.Set => SourceOf(d) is { } s ? $"{ElementSets.Name(s)} COMPLETE" : "A SET IS COMPLETE",
            DispatchKind.Region => Regions.Find(d.RegionId ?? "") is { } r ? $"{r.Name} CONQUERED" : "A REGION IS CONQUERED",
            DispatchKind.Socket => "A KEYSTONE SOCKET OPENS",
            DispatchKind.Migration => d.SubjectId switch
            {
                DispatchKeys.MigrationKeystones => d.Count is int n && n > 1
                    ? $"THE WORLD HAS TAUGHT YOU {n} KEYSTONES"
                    : "THE WORLD HAS TAUGHT YOU A KEYSTONE",
                DispatchKeys.MigrationFifthSlot => "THE FIFTH SKILL SLOT IS GONE",
                DispatchKeys.MigrationSkillKinds => "A SKILL LEFT YOUR BUILD",
                _ => "THE GAME HAS CHANGED",
            },
            _ => "NEWS",
        };
    }

    /// <summary>What to do about it, or what it does — sentences the surface may wrap. Empty only when there is nothing to add.</summary>
    public static string Body(Dispatch d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return d.Kind switch
        {
            DispatchKind.Unlock => ScreenOf(d) is { } screen ? Unlocks.OpenedLine(screen) : "It is on the rail.",
            DispatchKind.Gem => "The Forge's SOCKET tab sets it into an item.",
            DispatchKind.Trait => TraitCatalogue.Find(d.SubjectId) is { } t ? t.Flavour : "Read what it does on the TRAITS screen.",
            DispatchKind.Vow => Vows.ById(d.SubjectId) is { } v ? VowBody(v) : "It is on the BUILD screen.",
            DispatchKind.Keystone => Keystones.ById(d.SubjectId) is { } k ? KeystoneRevealDetail(k) : "It is on the BUILD screen, ready to wear.",
            DispatchKind.Champion => "Switch hunter on the ROSTER screen.",
            DispatchKind.Quest => QuestCatalogue.Find(d.SubjectId) is { } q ? q.Demand : "Its reward is yours.",
            DispatchKind.Set => SourceOf(d) is { } s ? SetBody(s) : "Five pieces worn; its bonus is active.",
            DispatchKind.Region => "The next region is open. Travel there on the MAP when you are ready.",
            DispatchKind.Socket => "Go to the BUILD screen to wear your new keystone.",
            DispatchKind.Migration => d.SubjectId switch
            {
                DispatchKeys.MigrationKeystones => (d.Count is int n && n > 1 ? "They are" : "It is")
                                                   + " on the BUILD screen, ready to wear. You find more by conquering and mastering regions.",
                DispatchKeys.MigrationFifthSlot => "It was taken out of your build. It keeps its level — put it back any time in place of another skill.",
                DispatchKeys.MigrationSkillKinds => "A build holds at most two skills that take an action and two that do not. One was taken out. It keeps its level — put it back any time in place of another skill.",
                _ => "",
            },
            _ => "",
        };
    }

    /// <summary>
    /// How a Vow arrived, which is not one sentence but two.
    /// </summary>
    /// <remarks>
    /// A GRANTED Vow is HANDED to the player — it comes with the BUILD screen so the system is
    /// discoverable at all — and "A VOW HAS REVEALED ITSELF" would be a lie about what they did: they
    /// did nothing, the game gave it to them. Every other Vow is PROVED, by having already kept its
    /// rule without it, and that one really did reveal itself.
    /// <para>
    /// One KEY for both (<c>vow.&lt;id&gt;</c>), because a Vow is discovered once however it arrives and
    /// the inbox must refuse the second telling — the sentence branches, the dedupe does not.
    /// </para>
    /// </remarks>
    private static string VowHeadline(Dispatch d)
    {
        if (Vows.ById(d.SubjectId) is not { } v) return "A VOW HAS REVEALED ITSELF";
        return v.Proof == VowProof.Granted
            ? $"A VOW IS OFFERED TO YOU — {v.Name}"
            : $"A VOW HAS REVEALED ITSELF — {v.Name}";
    }

    // ── KEYSTONE ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The reveal a found keystone gets: where it came from, what it gives, what it costs, and where
    /// it now is — three or four plain sentences.
    /// </summary>
    /// <remarks>
    /// ONE SENTENCE-MAKER FOR ALL FOUR PRODUCERS — a conquest, the two mastery rungs and the
    /// corruption — so a keystone arrives with the same shape however it was earned. It has to say
    /// what the keystone DOES, both halves: the word CAPACITOR alone teaches nobody anything, and a
    /// gift with its price left off is the one sentence a trade must never be told in. The cost is
    /// never cut for room; the reading pane wraps rather than truncates, and the fit is measured at
    /// every density profile against the pane's real rectangle. Pure, so the pane that shows it and the
    /// test that measures it read the same string.
    /// </remarks>
    public static string KeystoneRevealDetail(Keystone k)
    {
        ArgumentNullException.ThrowIfNull(k);
        var reached = Keystones.SourceOf(k.Id) is { } src ? RungReached(src) : "";
        var opening = reached.Length > 0
            ? $"{reached}, and it has taught you {k.Name}."
            : $"You have learned {k.Name}.";
        return $"{opening} {KeystoneProse(k)} {KeystonePointer}";
    }

    /// <summary>Where the keystone now waits. Appended unchanged; the pane wraps a long letter rather than cutting it.</summary>
    private const string KeystonePointer = "It is on the BUILD screen.";

    /// <summary>"VERDANT HOLLOW is partly mastered" — the clause a reveal opens with, no full stop.</summary>
    private static string RungReached(KeystoneSource src)
    {
        if (src.Rung == WorldRung.Corruption) return "The corruption has deepened";
        if (src.RegionId is not { } id || Regions.Find(id) is not { } def) return "";
        return src.Rung switch
        {
            WorldRung.Conquest => $"{def.Name} is conquered",
            WorldRung.PartlyMastered => $"{def.Name} is partly mastered",
            WorldRung.FullyMastered => $"{def.Name} is fully mastered",
            _ => "",
        };
    }

    /// <summary>
    /// What a keystone gives and what it costs, as spoken sentences — every digit from
    /// <see cref="Keystone.Mods"/> or a <see cref="SoloBattle"/> constant.
    /// </summary>
    /// <remarks>
    /// A table by id rather than a generator over the five multipliers: the triggers are what make
    /// most of these a different game (ECHO's second cast, REND's pool, UNDYING's last point) and no
    /// generator can say them. The numbers are still never typed here — a keystone retuned on its
    /// Mods changes its letter with it, and the liveness test proves each one is named. A keystone
    /// this table has not met yet falls back to its Blurb, complete but shouting, and the same test
    /// refuses that, so the fallback is a guard and not a route.
    /// </remarks>
    private static string KeystoneProse(Keystone k)
    {
        var m = k.Mods;
        return k.Id switch
        {
            "glass_cannon" => "Double your damage, but you keep only half your maximum Health.",
            "ironclad" => "Double your maximum Health, but your skills come back half as often.",
            "blood_magic" => "Your skills come back twice as fast, but nothing can heal you.",
            "bloodlust" => $"The closer you are to death, the harder you hit, but your maximum Health is {Pct(1f - m.Health)} lower.",
            "echo" => $"Every skill fires twice, but each firing does only {Pct(m.Damage)} of its damage.",
            "greed" => $"Double your loot, but you hit {Pct(1f - m.Damage)} softer.",
            "discerning_eye" => "Double the rarity of your finds, but you get only half the loot.",
            "reaper" => $"Every wave you clear gives richer loot, but your skills come back {Pct(1f - m.SkillRate)} slower.",
            "undying" => $"The first killing blow of each run leaves you alive on your last point of Health, but you hit {Pct(1f - m.Damage)} softer.",
            "venomancer" => $"Your skills poison what they hit, but they hit {Pct(1f - m.Damage)} softer.",
            "juggernaut" => "The fuller your Health, the harder you hit, but nothing can heal you.",
            "fortune" => $"Double the rarity of your loot, but you hit {Pct(1f - m.Damage)} softer.",
            "titan" => $"Triple your maximum Health, but your skills come back {Pct(1f - m.SkillRate)} slower.",
            "hoarder" => "More loot means harder hits, but rare finds come half as often.",
            "rend" => $"Every skill you use stores a charge, up to {SoloBattle.ChargeCap}, and your strikes spend the whole pool for "
                      + $"{Pct(SoloBattle.ChargeRendPerPoint)} more damage per charge. Every hit is {Pct(1f - m.Damage)} softer.",
            "capacitor" => $"Your charge pool holds {SoloBattle.ChargeCapExtended} instead of {SoloBattle.ChargeCap}, and LODESTONE then needs all "
                           + $"{SoloBattle.ChargeCapExtended}. Your skills come back {Pct(1f - m.SkillRate)} slower, and it does nothing unless another keystone uses the pool.",
            "dynamo" => $"Every hit you take stores {SoloBattle.ChargeDynamoPerBite} charge, but your maximum Health is {Pct(1f - m.Health)} lower.",
            "lodestone" => $"Clear a wave with a full charge pool and you get a spare core, but rare finds come {Pct(1f - m.Rarity)} less often.",
            "weaver" => $"Every skill also fires the next skill in your build, at {Pct(SoloBattle.WeaverEchoFraction)} of its damage, "
                        + $"but your skills come back {Pct(1f - m.SkillRate)} slower.",
            _ => k.Blurb.Trim(),
        };
    }

    // ── VOW ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// How the Vow was earned, then where it is. A granted Vow states its rule and its pay; a proved
    /// Vow states what the player did — the waves, the demand kept, the temptation refused.
    /// </summary>
    /// <remarks>
    /// The proof sentence is the ONLY place a discovery rule is ever stated, and it is stated after the
    /// fact — that is how hidden conditions and a discoverable system coexist. It is built from the
    /// Vow's typed demand, threshold, bare slot and temptation, mirroring the BUILD screen's own
    /// per-demand phrases as sentences, so the two never disagree about what the rule was. The two
    /// conduct Vows have no demand to keep and branch by id exactly as <see cref="Vows.RuleHeld"/>
    /// does: RECKLESS OFFERING is proved by a keystone that already cost maximum Health, FRAGILITY by
    /// anything that already raised damage taken.
    /// </remarks>
    private static string VowBody(Vow v)
    {
        var earned = v.Proof switch
        {
            VowProof.Granted => GrantedRule(v),
            VowProof.Conduct => ConductProof(v),
            _ => DemandProof(v),
        };
        return $"{earned} {v.Name} is on the BUILD screen.";
    }

    /// <summary>The rule a handed-over Vow asks, and what it pays — the whole grammar in two sentences.</summary>
    private static string GrantedRule(Vow v)
    {
        var pays = $"every skill hits {Pct(Vows.Multiplier(v) - 1f)} harder";
        return v.Demand switch
        {
            VowDemand.EverySlotFilled => $"Fill every skill slot you own and {pays}. Leave one empty and the Vow pays nothing.",
            VowDemand.None => $"It is always on, and {pays}.",
            _ => $"Descend {Kept(v)} and {pays}. Break the rule and the Vow pays nothing.",
        };
    }

    /// <summary>"You cleared 15 waves with no boots, though you owned a pair."</summary>
    private static string DemandProof(Vow v)
        => $"You cleared {v.ProofWaves} waves {Kept(v)}{Tempted(v)}.";

    /// <summary>The two static-cost Vows: you had already agreed to be hurt, from somewhere else.</summary>
    private static string ConductProof(Vow v)
        => v.Id == "vow_reckless_offering"
            ? $"You cleared {v.ProofWaves} waves wearing a keystone that had already cost you maximum Health."
            : $"You cleared {v.ProofWaves} waves carrying something that already made you take more damage.";

    /// <summary>The demand, as the clause of a sentence: "with no keystone in use".</summary>
    private static string Kept(Vow v) => v.Demand switch
    {
        VowDemand.SingleStyle => "carrying one style of skill only",
        VowDemand.SingleSource => "with every skill drawing one source",
        VowDemand.EverySlotFilled => "with no empty skill slot",
        VowDemand.NoCritInvestment => "with your critical chance untouched",
        VowDemand.CadenceAtOrBelow => $"with your skill rate at {Rate(v.Threshold)} or below",
        VowDemand.CadenceAtOrAbove => $"with your skill rate at {Rate(v.Threshold)} or above",
        VowDemand.NoDefence => "with no defence at all",
        VowDemand.NoKeystone => "with no keystone in use",
        VowDemand.SlotLeftBare => $"with no {BareWord(v.Bare)}",
        _ => "keeping its rule",
    };

    /// <summary>What the player owned and refused, as the clause that makes a kept rule a restriction.</summary>
    private static string Tempted(Vow v) => v.Temptation switch
    {
        VowTemptation.AnAffixInHand => v.TemptationAffix switch
        {
            AffixStat.Crit => ", though you owned gear that would have raised it",
            AffixStat.SkillRate => ", though you owned gear that would have made your skills come back sooner",
            AffixStat.Defense => ", though you owned gear that would have given you some",
            _ => ", though you owned gear carrying the very thing you refused",
        },
        VowTemptation.GearForTheBareSlot => v.Bare is BareSlot.Boots or BareSlot.Gloves
            ? ", though you owned a pair"
            : ", though you owned one",
        VowTemptation.TwoStylesInReach => ", though your mastery reached skills of more than one style",
        VowTemptation.EveryWovenSourceChosen => ", and you chose each of those sources yourself",
        VowTemptation.AKeystoneKnown => ", though the world had already handed you a keystone",
        _ => "",
    };

    private static string BareWord(BareSlot slot) => slot switch
    {
        BareSlot.Boots => "boots",
        BareSlot.Gloves => "gloves",
        BareSlot.Helm => "helm",
        BareSlot.Ring => "ring",
        BareSlot.Charm => "charm",
        _ => "gear in that slot",
    };

    // ── SET ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The worn count, the capstone's name, and the capstone rung's own sentence — which the set
    /// catalogue already writes as prose, so it is quoted rather than rewritten.
    /// </summary>
    private static string SetBody(Source s)
    {
        var top = ElementSets.TiersOf(s)[^1];
        return $"You wear {top.Pieces} pieces of the {ElementSets.Name(s)}, so {ElementSets.CapstoneName(s)} is active. {top.Line}";
    }

    // ── NUMBERS ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>A fraction as a whole percentage, invariant: 0.15 reads "15%" on every machine.</summary>
    private static string Pct(float fraction)
        => MathF.Round(fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A skill-rate threshold the way the BUILD screen prints it: "1.40x".</summary>
    private static string Rate(float threshold)
        => threshold.ToString("0.00", CultureInfo.InvariantCulture) + "x";

    // The subject of an Unlock row is an Activity NAME, read forward across renames the way the
    // explained list is — so a row written when TRAINING was still STATS renders as TRAINING.
    private static Activity? ScreenOf(Dispatch d)
        => d.SubjectId is { Length: > 0 } name
           && Enum.TryParse<Activity>(Onboarding.ModernScreenKey(name), ignoreCase: true, out var screen)
           && Enum.IsDefined(screen)
            ? screen
            : null;

    private static Source? SourceOf(Dispatch d)
        => d.SubjectId is { Length: > 0 } name
           && Enum.TryParse<Source>(name, ignoreCase: true, out var source)
           && Enum.IsDefined(source)
            ? source
            : null;
}
