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

    /// <summary>A KILL readies every skill at once — the next shot goes out immediately.</summary>
    /// <remarks>
    /// Written because THE QUIVER's card said it and nothing did it. The character granted
    /// <see cref="Splinter"/>, whose own blurb is "on kill: richer loot", so the passive a player
    /// unlocks by finishing a quest promised an action-economy payoff and delivered a loot one — two
    /// different sentences, neither of them the one on the card.
    ///
    /// Worth most in a Swarm band and nothing at all against a single creature, which is exactly the
    /// shape a Projectile specialist's passive should have: it rewards the build that can convert one
    /// kill into the next, and it is dead weight on a boss.
    /// </remarks>
    LooseAgain,

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

    // NOTE — the keystone/Vow combo enchantments (FERVOUR, REVERB, BULWARK, TITHE) deliberately have
    // NO entry here. A BuildTrigger earns its place by being something more than one source can grant
    // and the sim can ASK about: VENOM comes from a keystone or a weapon, UNDYING from a keystone, a
    // charm or a character. Those four come from exactly one place, an item, and what the sim needs
    // from them is a magnitude rather than a yes — so they are read straight off the worn enchantments
    // like Venom's and Harvest's strengths are. Adding them here would have put four values in this
    // enum that nothing ever reads, which is precisely the shape of failure
    // TriggerLivenessTests exists to refuse. Their own guard lives in EnchantmentsTests.

    /// <summary>
    /// HOARDER — the AVARICE terminal. Haul becomes force.
    /// </summary>
    /// <remarks>
    /// The path it ends buys no combat power at all, which is what makes it a real choice; the terminal
    /// is what stops it being a dead end. Without this, an Avarice hunter's reward for a whole permanent
    /// path is money, and money's only use is gear they could have had by pushing depth instead.
    /// </remarks>
    Hoarder,

    /// <summary>
    /// WEAVER — the ARTIFICE terminal. Every skill also fires as the NEXT Form in the loadout.
    /// </summary>
    /// <remarks>
    /// The design's "two Forms in one slot", expressed with the loadout that exists. It is the path for
    /// players who want their build to do something strange rather than something large, and it is the
    /// only thing in the game that lets a single slot answer two of the content's four demands.
    /// </remarks>
    Weaver,
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
    /// <summary>How many skills a character STARTS able to weave.</summary>
    /// <remarks>
    /// Four, because a build must be a CHOICE of Forms. Six Forms and six slots would mean everyone
    /// carries everything and the Form axis collapses — the same way an unbounded gear budget collapsed
    /// "which item" into "the biggest number".
    ///
    /// This is the FLOOR, not the rule. The trait spine sells a fifth (<c>weave_5</c>); see
    /// <see cref="SlotCapacity"/>, which is what <see cref="Weave"/> actually enforces.
    /// </remarks>
    public const int SkillSlots = 4;

    /// <summary>How many skills THIS build may weave — four, or five once the spine has sold the fifth.</summary>
    /// <remarks>
    /// An instance value rather than the const above, because the const was the whole bug. FIFTH WEAVE
    /// is a three-point node on the trait spine; <c>DustEffects.SkillSlots</c> returned 5 for a player
    /// who owned it, the host wrote that into <c>PlayerLoadout.SkillCapacity</c>, and then <c>AddSkill</c>
    /// took <c>Math.Min(MaxSkills, SkillCapacity)</c> against a hard 4 and threw it away. The node was
    /// bought, persisted, resolved, displayed — and clamped off at the last step, which is this
    /// codebase's signature failure wearing a different hat.
    ///
    /// <b>The floor is WHAT IS ALREADY WOVEN, not <see cref="SkillSlots"/>.</b> It used to be the
    /// constant 4, on the reasoning that a build handed a smaller capacity would silently unweave skills
    /// the player already had — which was sound while every player had four slots from the first frame.
    ///
    /// The gradual-unlock pass ended that: a new champion has ONE slot and earns the rest. Against a
    /// hard floor of 4 the build reported four slots to a player who had one, and the consequence was
    /// not cosmetic — VOW OF COMPLETION demands "no skill slot is empty", so it compared 1 woven against
    /// 4 slots and was UNMEETABLE for the entire onboarding, reading UNMET on the Weave screen for a
    /// build that in fact had no empty slot at all.
    ///
    /// Keeping the real protection and dropping the wrong constant: the capacity can never report fewer
    /// slots than there are skills in them, so nothing is ever unwoven, and it is otherwise honest.
    /// </remarks>
    public int SlotCapacity
    {
        get => Math.Max(_slotCapacity, _skills.Count);
        set => _slotCapacity = Math.Max(1, value);
    }

    private int _slotCapacity = SkillSlots;

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
        if (_skills.Count >= SlotCapacity) return false;
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

    /// <summary>
    /// How the skill tree changes the WAY this build fights, as opposed to how big its numbers are.
    /// </summary>
    /// <remarks>
    /// A third channel alongside <see cref="PassiveMods"/> and <see cref="ExtraTriggers"/>, set by whoever
    /// owns the MasteryTree, for the same reason: Core.Builds must not know what a MasteryTree is.
    ///
    /// Left at <see cref="SkillShape.None"/> it changes nothing — which is the correct reading for an
    /// unallocated tree and also the reading for a wire someone forgot to connect. The tests in
    /// SkillShapeBattleTests are what tell those two apart, and they exist because exactly that silent
    /// failure left the entire loot-rarity chain inert for the whole of development.
    /// </remarks>
    public SkillShape Shape { get; set; } = SkillShape.None;

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
        // FERVOUR / REVERB / BULWARK / TITHE fall through to null on purpose — see the note in the
        // BuildTrigger enum. They are magnitudes, not answers to a yes/no question.
        _ => null,
    };
}
