using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Builds;

/// <summary>
/// Everything a build can change about the character, in one bundle.
/// </summary>
/// <remarks>
/// <para>
/// Items, passive nodes and skills all contribute to the same two things, and they are deliberately
/// two rather than one:
/// </para>
/// <list type="bullet">
/// <item><b>Numbers</b> (this record) — multiplied together. "How much".</item>
/// <item><b>Triggers</b> (<see cref="BuildTrigger"/>) — asked about at a moment. "And then what".</item>
/// </list>
/// <para>
/// Keeping them apart is the lesson from <see cref="GearMods"/>, which was a four-float struct and so
/// <i>by construction</i> could only ever produce numbers — which made the design's behaviour-changing
/// effects unbuildable until a separate trigger type existed. A build system needs both from day one
/// or it becomes a spreadsheet with no verbs.
/// </para>
/// </remarks>
public readonly record struct BuildMods(
    float Damage,
    float Health,
    float SkillRate,
    float Haul,
    float Rarity)
{
    public static readonly BuildMods None = new(1f, 1f, 1f, 1f, 1f);

    /// <summary>Multiplicative. Additive stacking is how idle games end up at 400,000%.</summary>
    public BuildMods Combine(BuildMods o) => new(
        Damage * o.Damage, Health * o.Health, SkillRate * o.SkillRate, Haul * o.Haul, Rarity * o.Rarity);

    public static BuildMods Sum(IEnumerable<BuildMods> all)
    {
        ArgumentNullException.ThrowIfNull(all);
        return all.Aggregate(None, (a, b) => a.Combine(b));
    }
}

/// <summary>
/// A behaviour a build turns on. The sim ASKS about these; it does not multiply by them.
/// </summary>
/// <remarks>
/// This is the vocabulary shared by keystones, item enchantments and skills — one enum, so a keystone
/// and an item can grant the same behaviour and the sim only implements it once.
/// </remarks>
public enum BuildTrigger
{
    /// <summary>On a kill: strike again.</summary>
    Splinter,

    /// <summary>On a kill: a spare core.</summary>
    Harvest,

    /// <summary>Skills also poison.</summary>
    Venom,

    /// <summary>Near death: a richer haul.</summary>
    Desperation,

    /// <summary>The first killing blow each expedition leaves you at 1 HP.</summary>
    Undying,

    /// <summary>Cannot be healed at all — see BloodMagic in the keystone catalog.</summary>
    NoHealing,

    /// <summary>Every skill fires twice, at reduced power.</summary>
    Echo,

    /// <summary>Damage scales with how much health is MISSING.</summary>
    Bloodlust,

    /// <summary>Damage scales with how much health is PRESENT — the mirror of <see cref="Bloodlust"/>.</summary>
    Zeal,

    // ── Form-combo triggers, granted by item enchantments. Each is dead weight without its Form. ─────
    /// <summary>PROJECTILE fires one extra time.</summary>
    Overdraw,

    /// <summary>MARK's amplify window lasts far longer.</summary>
    Linger,

    /// <summary>AURA ticks faster.</summary>
    Radiance,

    /// <summary>STRIKE deals far more to a weakened enemy — a finisher.</summary>
    Execute,

    /// <summary>TRAP re-arms far faster, punishing every bite.</summary>
    Coiled,

    /// <summary>TRANSFORMATION leeches far more health.</summary>
    Siphon,
}

/// <summary>
/// A KEYSTONE: a passive that changes how the character works, and always costs something.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keystones are what make a build a build.</b> An attribute node makes you 4% stronger and every
/// player takes every one they can reach; a keystone makes you a different character and most players
/// must refuse it. That refusal is the whole game — it is the same rule that made gear traits and Vows
/// into decisions rather than sorting exercises, applied to the tree.
/// </para>
/// <para>
/// So every keystone here is a TRADE, enforced by test: it must give something and take something.
/// A keystone with no drawback is an attribute node wearing a hat, and it will be taken by everyone,
/// and it will make every build identical — which is exactly the failure this game keeps rediscovering
/// (a decision with a strictly-correct answer is not a decision).
/// </para>
/// </remarks>
public sealed record Keystone
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>What it does and what it costs, in the player's words. Both halves, always.</summary>
    public required string Blurb { get; init; }

    /// <summary>The numbers it moves. Must contain at least one value BELOW 1 — see the class remarks.</summary>
    public BuildMods Mods { get; init; } = BuildMods.None;

    /// <summary>Behaviours it turns on.</summary>
    public IReadOnlyList<BuildTrigger> Grants { get; init; } = Array.Empty<BuildTrigger>();
}

/// <summary>
/// One equipped skill: the player's own Source x Form x Vow, plus how fast it comes back.
/// </summary>
/// <remarks>
/// The roster is gone, so a skill is no longer something a creature's Role hands you — it is something
/// the player WOVE. That is what <c>Core.Abilities</c> was always for, and it is why Form finally
/// matters: a build is a choice of Forms, and the Vow on each is what it cost to have them.
/// </remarks>
public sealed record EquippedSkill(WovenAbility Ability, int CooldownMs)
{
    public string Name => Ability.Name;
    public Form Form => Ability.Form;
    public Source Source => Ability.Source;
    public Vow? Vow => Ability.Vow;
}

/// <summary>
/// THE BUILD: one character's skills, keystones and gear, resolved into what the sim needs.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole game now. There is no roster, no squad, no slot order — the player has one
/// character, and every decision they make is here: which skills they wove, which keystones they
/// accepted, what they are wearing. The expedition reads this and runs itself.
/// </para>
/// <para>
/// It is a pure projection: it owns no state and decides nothing on its own. Feed it the same skills,
/// keystones and gear and it produces the same numbers, every time — which is what makes a build
/// something a player can reason about instead of something they have to test.
/// </para>
/// </remarks>
public sealed class Build
{
    /// <summary>How many skills a character may have woven at once.</summary>
    /// <remarks>
    /// Four, because a build must be a CHOICE of Forms. Six Forms and six slots would mean everyone
    /// carries everything and the Form axis collapses — the same way an unbounded gear budget collapsed
    /// "which item" into "the biggest number".
    /// </remarks>
    public const int SkillSlots = 4;

    /// <summary>
    /// The character's Form AFFINITY — the Nen-hexagon axis. Null means unchosen (everything neutral).
    /// </summary>
    /// <remarks>
    /// Set from the player's loadout. The sim reads it through <see cref="FormBehaviour.AffinityFactor"/>
    /// to reward committing to your affinity's Form and to make splashing its opposite cost something.
    /// </remarks>
    public Form? Affinity { get; set; }

    private readonly List<EquippedSkill> _skills = new();
    private readonly List<Keystone> _keystones = new();

    public IReadOnlyList<EquippedSkill> Skills => _skills;
    public IReadOnlyList<Keystone> Keystones => _keystones;

    /// <summary>Equip a skill. Returns false when the slots are full — a bounded budget is the point.</summary>
    public bool Weave(EquippedSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (_skills.Count >= SkillSlots) return false;
        _skills.Add(skill);
        return true;
    }

    public bool Unweave(string abilityName) => _skills.RemoveAll(s => s.Name == abilityName) > 0;

    /// <summary>
    /// How many keystones a build may socket at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three, and this number is the only thing keeping the passive tree honest.</b> The Dust tree is
    /// COMPLETABLE by design — a player can buy every node and see the horizon. That is a promise worth
    /// keeping, but it collides head-on with keystones: if owning a keystone meant wearing it, then a
    /// finished tree would wear all ten, and since the catalog is built from opposed pairs they would
    /// very nearly cancel. Every completed build would arrive at the same mediocre generalist, and
    /// <c>test_every_keystone_costs_something</c> would be guarding a door with no wall around it.
    /// </para>
    /// <para>
    /// So the tree TEACHES and the build CHOOSES — the split this codebase already uses for Vows, where
    /// <c>vow_study_1</c> buys knowledge of Patience and each ability still picks its own Vow. Dust buys
    /// access; the refusal happens here, permanently, every time the player looks at three sockets and
    /// ten keystones.
    /// </para>
    /// </remarks>
    public const int KeystoneSlots = 3;

    /// <summary>Socket a keystone. False when the sockets are full or it is already worn.</summary>
    public bool Take(Keystone k)
    {
        ArgumentNullException.ThrowIfNull(k);
        if (_keystones.Any(x => x.Id == k.Id)) return false;
        if (_keystones.Count >= KeystoneSlots) return false;
        _keystones.Add(k);
        return true;
    }

    /// <summary>Unsocket a keystone. Free and reversible — unlike buying the node that taught it.</summary>
    public bool Drop(string keystoneId) => _keystones.RemoveAll(k => k.Id == keystoneId) > 0;

    /// <summary>
    /// Everything multiplied together: keystones, worn gear, and trained attributes.
    /// </summary>
    /// <remarks>
    /// Gear's <see cref="GearMods"/> is folded in here rather than read separately by the sim, so there
    /// is exactly ONE place that knows what a character's damage is. The alternative — the sim asking
    /// gear and passives and keystones in three different places — is how a number ends up applied
    /// twice, or not at all.
    /// </remarks>
    public BuildMods Resolve(Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);

        var fromKeystones = BuildMods.Sum(_keystones.Select(k => k.Mods));

        var g = hunter.WornMods;
        var fromGear = new BuildMods(g.Damage, g.Health, g.SkillRate, g.Haul, 1f);

        var fromStats = new BuildMods(
            Damage: hunter.SquadDamageMultiplier,
            Health: hunter.SquadHealthMultiplier,
            SkillRate: hunter.SquadSkillRate,
            Haul: hunter.HaulMultiplier,
            Rarity: 1f);

        // Gear's mods are ALREADY inside the hunter's Squad* multipliers. Folding fromGear in as well
        // would bill every trait twice — a Legendary HEAVY weapon would read as +96% damage squared.
        _ = fromGear;

        return fromKeystones.Combine(fromStats).Combine(PassiveMods);
    }

    /// <summary>
    /// What the bought passive-tree attribute nodes are worth. Set by whoever owns the Dust tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain property rather than a <c>Resolve(hunter, tree)</c> parameter, because Core's build layer
    /// has no business knowing what a <c>MemoryDustTree</c> is — the dependency points one way, from
    /// Prestige into Builds, and this keeps it there.
    /// </para>
    /// <para>
    /// Fed by <c>DustEffects.TreeMods</c>. Left at <see cref="BuildMods.None"/> it changes nothing, which
    /// is the correct reading for a character who has bought no nodes — and, less comfortably, also the
    /// reading for a wire someone forgot to connect. <c>test_the_passive_tree_reaches_the_fight</c> is
    /// what tells those two apart.
    /// </para>
    /// </remarks>
    public BuildMods PassiveMods { get; set; } = BuildMods.None;

    /// <summary>
    /// Triggers granted by things the Build doesn't own directly — the MASTERY TREE's notables. Folded
    /// into <see cref="Triggers"/> alongside keystones and worn enchantments.
    /// </summary>
    /// <remarks>
    /// A separate channel, like <see cref="PassiveMods"/>, so Core.Builds needn't know what a
    /// <c>MasteryTree</c> is — the caller resolves the tree and hands the result in.
    /// </remarks>
    public IReadOnlySet<BuildTrigger> ExtraTriggers { get; set; } = new HashSet<BuildTrigger>();

    /// <summary>Every behaviour this build turns on — from keystones and from worn gear alike.</summary>
    /// <remarks>
    /// A HashSet: two sources granting the same trigger is one trigger, not two. UNDYING from a
    /// keystone and UNDYING from a charm must not mean you get up twice.
    /// </remarks>
    public IReadOnlySet<BuildTrigger> Triggers(Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);

        var set = new HashSet<BuildTrigger>(_keystones.SelectMany(k => k.Grants));
        set.UnionWith(ExtraTriggers);                       // MASTERY TREE notables
        foreach (var e in hunter.WornEnchantments)
            if (FromEnchant(e.Kind) is { } t) set.Add(t);
        return set;
    }

    /// <summary>An item's enchantment and a keystone speak the same language.</summary>
    private static BuildTrigger? FromEnchant(EnchantKind kind) => kind switch
    {
        EnchantKind.Splinter => BuildTrigger.Splinter,
        EnchantKind.Harvest => BuildTrigger.Harvest,
        EnchantKind.Venom => BuildTrigger.Venom,
        EnchantKind.Desperation => BuildTrigger.Desperation,
        EnchantKind.Undying => BuildTrigger.Undying,
        EnchantKind.Overdraw => BuildTrigger.Overdraw,
        EnchantKind.Linger => BuildTrigger.Linger,
        EnchantKind.Radiance => BuildTrigger.Radiance,
        EnchantKind.Execute => BuildTrigger.Execute,
        EnchantKind.Coiled => BuildTrigger.Coiled,
        EnchantKind.Siphon => BuildTrigger.Siphon,
        _ => null,
    };
}
