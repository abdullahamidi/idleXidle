using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Economy;

namespace IdleXIdle.Core.Builds;

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

    /// <summary>Your STRIKES spend the whole CHARGE pool for bonus force — the CHARGE spender.</summary>
    /// <remarks>
    /// CHARGE is the shared stack primitive: every skill cast stores one point, and nothing reads
    /// the pool unless a keystone does. REND is what turns the pool into a rhythm — pool casts,
    /// then dump them through a Strike. Dead without a Strike in the build, on purpose: a spender
    /// with nothing to spend through is a keystone you should not have socketed.
    /// </remarks>
    Rend,

    /// <summary>The CHARGE pool holds twice as much. Means nothing without a spender.</summary>
    Capacitor,

    /// <summary>Every bite you take stores CHARGE — the wall that winds the spring.</summary>
    Dynamo,

    /// <summary>Clearing a wave with a FULL pool yields a spare core — the holder's reward.</summary>
    /// <remarks>In direct tension with <see cref="Rend"/>: one wants the pool dumped, one wants it kept.</remarks>
    Lodestone,

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
/// One equipped skill, RESOLVED: the catalogue definition with its variation and reinforcement
/// deltas already applied, the element it is made of, and the Vow sworn on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Def is final.</b> The composer applies the player's variation and its bought
/// reinforcements exactly once, at composition — the fight reads dials off this one record and
/// never asks who set them. The Form-era shape (a WovenAbility plus a cooldown plus a slot flag)
/// went with the Form enum itself (P3-final, 2026-08-31): a skill's identity is its catalogue id,
/// its cadence lives on the def (Beats / IntervalMs / RearmMs), and whether it costs the champion
/// an action is the def's own kind.
/// </para>
/// <para>
/// SOURCE rides beside the def rather than on it because the element is the player's — the chosen
/// variation owns it, the woven element is the fallback before that choice — so one def can be
/// woven as two different elements by two players.
/// </para>
/// <para>
/// <see cref="Variation"/> is WHICH variation the skill was taken as, or null while it is unchosen.
/// The def is already resolved, so nothing reads the variation's deltas twice; it is here so a
/// reader that needs to know whether <see cref="Source"/> was a DECISION or a default can tell — the
/// only Source lever the player has is a variation (<c>PlayerLoadout</c> seeds every woven slot BODY
/// and the weave screen sets no Source of its own), which is the rule VOW OF THE PURE's proof already
/// lives by (<see cref="VowTemptation.EveryWovenSourceChosen"/>). The fight reads it once a wave, through
/// <see cref="Build.ChosenSingleSource"/> (THE SINGLE NOTE, the mastery PURE node) and
/// <see cref="Build.DistinctChosenSources"/> (MANY TONGUES); the trait ledger through <see cref="Build.PureSource"/>.
/// </para>
/// </remarks>
public sealed record EquippedSkill(SkillDef Def, Source Source, Vow? Vow = null, SkillVariation? Variation = null)
{
    /// <summary>Does this skill cost the champion its action? The def's own kind decides.</summary>
    public bool TakesABeat => Def.TakesABeat;
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
    /// This is also the CEILING. It was once only a floor: the trait tree sold a fifth slot
    /// (<c>weave_5</c>), and that slot bought a THIRD ACTIVE — <c>ActiveSlotsFor(5)</c> is 3 — which
    /// pushed beat demand back toward the number the slot rework existed to bring down. The fifth slot
    /// is removed, and it is the one capability this refactor deliberately takes away.
    /// </remarks>
    public const int SkillSlots = 4;

    /// <summary>How many skills THIS build may weave. Never more than <see cref="SkillSlots"/>.</summary>
    /// <remarks>
    /// An instance value rather than the const above, because a champion EARNS its slots one at a time
    /// and a build must be able to report one, two or three of them honestly.
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
        // CLAMPED AT FOUR, both ends. A build is two skills that take an action and two that do not;
        // there is no longer anything in the game that sells a fifth, and a stale save that asks for
        // one is answered with four rather than quietly re-opening a slot that has been removed.
        set => _slotCapacity = Math.Clamp(value, 1, SkillSlots);
    }

    private int _slotCapacity = SkillSlots;

    /// <summary>
    /// How many ACTIVE skills — ones that cost the champion an action — a build may carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rework's target is <b>two</b>, against two passive slots. Beat demand with four actives is
    /// about 0.80 (Strike 0.17 + Projectile 0.25 + Mark 0.19 + Transformation 0.19), so four beats in
    /// five were somebody's cast and the plain swing almost never played. Two actives takes it to
    /// about 0.44 <i>without moving a single cooldown</i>, which is why the knob that had no room
    /// left in it stops mattering.
    /// </para>
    /// <para>
    /// <b>It is deliberately still four here.</b> Stage 2 of the rework installs the machinery — the
    /// per-kind capacities and the enforcement in <see cref="Weave"/> — with the numbers left where
    /// they are, so the whole suite stays green while the mechanism is proven. Flipping this to 2
    /// is stage 2b, and it lands together with the balance re-measurement that has to come with it:
    /// halving the actives roughly halves cast output, so <c>AutoAttackDamage</c> and
    /// <c>FormBaseValue</c> move with it. See <c>design/gdd/skill-slots-and-skill-trees.md</c> §10.
    /// </para>
    /// </remarks>
    public int ActiveCapacity
    {
        get => Math.Max(_activeCapacity ?? SlotCapacity, ActiveCount);
        set => _activeCapacity = Math.Max(1, value);
    }

    /// <summary>How many PASSIVE skills — ones that never cost an action — a build may carry.</summary>
    public int PassiveCapacity
    {
        get => Math.Max(_passiveCapacity ?? SlotCapacity, PassiveCount);
        set => _passiveCapacity = Math.Max(0, value);
    }

    // UNSET MEANS "THE WHOLE BUDGET", not a constant. A build handed a fifth slot by the trait spine
    // raises SlotCapacity, and a per-kind cap frozen at the old constant would clamp that fifth slot
    // off — which is the exact bug FIFTH WEAVE already had once (see SlotCapacity's note). A build
    // composed for a real player has both set (see ActiveSlotsFor); one built bare in a test keeps
    // the undivided budget, so a test that only cares about damage need not learn the slot rules.
    private int? _activeCapacity;
    private int? _passiveCapacity;

    /// <summary>
    /// How many of a build's slots are ACTIVE, given the total it has earned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unlock order is <b>active, passive, active, passive</b>, so a full four-slot build is
    /// 2 + 2 and the SECOND slot a player ever earns already teaches that the two kinds are
    /// different. A character that has earned one slot gets an active, because a build with nothing
    /// but a Field never chooses an action at all.
    /// </para>
    /// <para>
    /// The fifth slot the trait spine sells falls to the active side here. §11 wants it to become the
    /// player's own choice of a third active or a third passive — that is a workbench decision and a
    /// save field, and it is deliberately not invented in this pass. It matters because a third
    /// active pushes beat demand back toward 0.6, which is a real cost the player should be electing
    /// rather than being handed.
    /// </para>
    /// </remarks>
    public static int ActiveSlotsFor(int totalSlots) => (Math.Max(1, totalSlots) + 1) / 2;

    /// <summary>The passive half of <see cref="ActiveSlotsFor"/>.</summary>
    public static int PassiveSlotsFor(int totalSlots) => Math.Max(1, totalSlots) - ActiveSlotsFor(totalSlots);

    /// <summary>Woven skills that cost the champion an action.</summary>
    public int ActiveCount => _skills.Count(s => s.TakesABeat);

    /// <summary>Woven skills that never cost an action — Fields and Reactions.</summary>
    public int PassiveCount => _skills.Count(s => !s.TakesABeat);

    /// <summary>
    /// The share of beats a build's actives demand, 0..1 — and therefore how little is left for the
    /// champion's own swing.
    /// </summary>
    /// <remarks>
    /// An upper bound rather than a measurement: two ready skills contend for one beat and the loser
    /// waits, so the realised share is a little under this. It is the number the whole rework exists
    /// to bring down, and it is exposed so a test can assert on it instead of a human counting swings
    /// in a capture.
    /// </remarks>
    public float BeatDemand
    {
        get
        {
            var demand = 0f;
            foreach (var s in _skills)
                if (s.TakesABeat && s.Def.Beats > 0) demand += 1f / s.Def.Beats;
            return Math.Min(1f, demand);
        }
    }

    /// <summary>
    /// The hunter's STYLE affinity — the attunement axis. Null means unchosen (everything neutral).
    /// </summary>
    /// <remarks>
    /// Set from the mastery tree's specialisation. The sim reads it through
    /// <see cref="StyleAffinity.Factor"/> to reward committing to your discipline and to make
    /// splashing its opposite cost something.
    /// </remarks>
    public Style? Affinity { get; set; }

    private readonly List<EquippedSkill> _skills = new();
    private readonly List<Keystone> _keystones = new();

    public IReadOnlyList<EquippedSkill> Skills => _skills;
    public IReadOnlyList<Keystone> Keystones => _keystones;

    /// <summary>
    /// How many woven skills it takes before "every one of them draws the same Source" describes a
    /// decision about the build rather than the one skill that happens to be in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two, read off the unlock ladder rather than picked. A champion starts with ONE slot and its own
    /// signature in it (<c>Unlocks.SkillSlots</c>, <c>PlayerLoadout.Starter</c>); the signature's own
    /// variation, choosable at level 1, is the first Source decision a player can make, and one decided
    /// skill still agrees with itself. The second skill is the first thing there is to agree WITH — and
    /// on the shipped ladder it arrives late: the second slot opens at wave 5, but every shared skill is
    /// behind a mastery road costing seven points (three trunk minors and the road, <c>MasteryCatalog</c>),
    /// which is around the first conquest and the start of the second region, not the first sitting.
    /// </para>
    /// </remarks>
    public const int PureSourceMinimumSkills = 2;

    /// <summary>
    /// The one Source this build is COMMITTED to, or null when it is not committed to any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Committed means three things at once: at least <see cref="PureSourceMinimumSkills"/> woven
    /// skills; every woven skill's Source CHOSEN, which is to say its variation taken
    /// (<see cref="EquippedSkill.Variation"/> not null); and every woven skill on the same Source.
    /// The last two are <see cref="ChosenSingleSource"/>; this adds the floor. The difference is the
    /// difference between PAYING a choice and DISCOVERING a trait from it: a lone chosen skill is a
    /// Source decision and the fight may pay it, but it is not yet a commitment between skills, and
    /// the trait's counter waits for one.
    /// </para>
    /// <para>
    /// <b>Chosen, not merely resolved.</b> The composer resolves a skill's Source as "the variation's,
    /// else the woven pick's" — and the woven pick is BODY on every slot the shipped game ever weaves,
    /// because <c>PlayerLoadout.AddSkill</c> seeds it and the weave screen offers no lever on it. So a
    /// signature and a fresh shared skill agree on BODY before the player has decided anything, and a
    /// reading that counted resolved Sources would call that pre-decision window a commitment — the
    /// same fact-without-a-decision this property exists to refuse, one rung up. The game already has
    /// one rule for when a single Source was a decision and not a default: VOW OF THE PURE's proof
    /// demands <see cref="VowTemptation.EveryWovenSourceChosen"/>, every woven skill with a chosen
    /// variation. This borrows exactly that clause and adds the two-skill minimum on top — the vow's
    /// proof has no minimum and a one-skill build with its variation chosen does prove it, so the two
    /// are NOT interchangeable: never route the vow's temptation through this property.
    /// </para>
    /// <para>
    /// Every woven skill takes part, whatever its kind. A Field or a Reaction draws its Source exactly as
    /// an Active does — the Source riders and the matchup read it on every hit it lands — and a
    /// champion's own signature is a woven skill like the shared twelve. An empty slot is not in
    /// <see cref="Skills"/> and neither is a skill the composer refused (a road the mastery no longer
    /// reaches, another champion's signature, an id the catalogue does not know), so none of those can
    /// count as a skill here or invent a Source. A skill woven and not yet chosen is in the build and
    /// fights at its default Source, and it holds the commitment open until it is chosen: the vow's rule,
    /// and the honest one, since the player has not yet said what that skill is made of.
    /// </para>
    /// <para>
    /// This is the ONE reading of "a pure-Source build" for anything that wants to RECOGNISE the choice
    /// — the trait ledger's PURE waves read it and nothing else may re-derive it. It is deliberately not
    /// the predicate the fight PAYS: THE SINGLE NOTE's payout and the mastery PURE node read
    /// <see cref="ChosenSingleSource"/> — chosen, defaults excluded, and a lone chosen skill counts.
    /// Recognition is that predicate behind the two-skill floor, so it is the stricter of the two by
    /// construction — every build this names is one the payout fires on — and a player can never
    /// awaken the trait on a build it would refuse to pay.
    /// </para>
    /// </remarks>
    public Source? PureSource
        => _skills.Count >= PureSourceMinimumSkills ? ChosenSingleSource : null;

    /// <summary>
    /// The one Source every woven skill was CHOSEN to draw — or null: no skills, a skill whose Source
    /// is still the default, or two skills chosen apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what "every skill you carry shares one source" means when the game PAYS for it</b> —
    /// THE SINGLE NOTE's first-cast echo and the mastery PURE node — and it is deliberately looser than
    /// <see cref="PureSource"/>: one skill whose variation is taken is a build committed to a Source
    /// on purpose, and it may be paid for that purpose. It is deliberately stricter than "every
    /// resolved Source agrees": a slot is seeded BODY before its variation is chosen and the weave
    /// screen offers no lever on it, so a fresh pair agrees on BODY by implementation, not by decision.
    /// A default proves nothing about a Source, so a build with any unchosen skill draws from no single
    /// chosen Source at all.
    /// </para>
    /// <para>
    /// CHOSEN SOURCE != RESOLVED DEFAULT SOURCE. The resolved Source (<see cref="EquippedSkill.Source"/>)
    /// is what a hit actually carries — the riders and the matchup read it, and a default-BODY skill
    /// really does lay a wound. The readers here are the ones that ask what the PLAYER decided.
    /// </para>
    /// </remarks>
    public Source? ChosenSingleSource
    {
        get
        {
            if (_skills.Count == 0) return null;
            var source = _skills[0].Source;
            foreach (var s in _skills)
            {
                if (s.Variation is null) return null;   // a default, not a decision
                if (s.Source != source) return null;
            }
            return source;
        }
    }

    /// <summary>
    /// How many different Sources the woven skills were CHOSEN to draw. A skill whose Source is still
    /// the default counts for none of them.
    /// </summary>
    /// <remarks>
    /// The reading for "carrying four different sources" — MANY TONGUES' payout and the MOTLEY waves
    /// that awaken it. Four voices means four decisions; a slot still sitting on the BODY seed is not a
    /// fourth voice however different its default happens to be from the other three.
    /// </remarks>
    public int DistinctChosenSources
    {
        get
        {
            var seen = 0;   // a bit per Source — six of them, and no allocation on the fight's path
            foreach (var s in _skills)
                if (s.Variation is not null) seen |= 1 << (int)s.Source;
            return System.Numerics.BitOperations.PopCount((uint)seen);
        }
    }

    /// <summary>
    /// The Vows this build has sworn — each one ONCE, however many times it was named.
    /// </summary>
    /// <remarks>
    /// A Vow is a promise about the BUILD, and a promise made twice is one promise. The workbench
    /// records it against the skill the hunter swore it at — that is the save shape — but nothing
    /// downstream counts it twice and nothing judges it a skill at a time: the bonus, the fragility
    /// bill, the health price and the TITHE enchantment all read this list, and every one of them is
    /// answered against the whole build. Vow CAPACITY (<c>Unlocks.VowCapacity</c>) counts exactly
    /// these, which is what makes it a number a milestone can grant.
    /// </remarks>
    public IReadOnlyList<Vow> Vows
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<Vow>();
            foreach (var sk in _skills)
                if (sk.Vow is { } v && seen.Add(v.Id)) list.Add(v);
            if (BorrowedVow is { } lent && seen.Add(lent.Id)) list.Add(lent);
            return list;
        }
    }

    /// <summary>
    /// A vow this build did not swear and is paid for anyway — THE KEPT WORD's loan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It joins <see cref="Vows"/>, so every consumer bills and pays it exactly like a sworn one: the
    /// combined factor, the fragility, the health price, TITHE. A trait that needed its own parallel
    /// path through four price sites would be four chances for them to disagree.
    /// </para>
    /// <para>
    /// A vow is a promise about the BUILD, so the loan is a build-level fact: <c>BuildComposer</c>
    /// decides it once, at compose time, where both halves of the question are in scope — the vows
    /// the account has FOUND, and whether the hunter has sworn any. The fight never asks.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Settable, like <see cref="Equip"/> beside it: a build is assembled by whoever is composing it,
    /// and the liveness suite composes builds by hand precisely so it can pose one state against
    /// another. <c>BuildComposer</c> is the only production writer.
    /// </remarks>
    public Vow? BorrowedVow { get; set; }

    /// <summary>
    /// Equip a skill. Returns false when the slots are full — a bounded budget is the point — or when
    /// the same skill is already equipped (LAW 13: one slot per skill; <see cref="Unequip"/> removes by
    /// id and has always assumed it).
    /// </summary>
    public bool Equip(EquippedSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (_skills.Count >= SlotCapacity) return false;
        foreach (var worn in _skills)
            if (worn.Def.Id == skill.Def.Id) return false;
        // PER-KIND CAPACITY. An active costs the champion an action and a passive never does, so they
        // are two different budgets and a build cannot spend one on the other. Enforced from the
        // moment the field exists rather than left switched off, because a capacity nothing checks is
        // exactly the dormant-feature failure this codebase keeps producing — FIFTH WEAVE was bought,
        // persisted, resolved, displayed and then clamped off at the last step.
        if (skill.TakesABeat)
        {
            if (ActiveCount >= ActiveCapacity) return false;
        }
        else if (PassiveCount >= PassiveCapacity) return false;
        _skills.Add(skill);
        return true;
    }

    /// <summary>Unequip a skill by its catalogue id — the only name a skill has left.</summary>
    public bool Unequip(string skillId) => _skills.RemoveAll(s => s.Def.Id == skillId) > 0;

    /// <summary>
    /// How many keystones a build may socket at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three, and this number is the only thing keeping the keystone catalogue honest.</b> The world
    /// teaches all NINETEEN — one for taking a region, one for knowing it, one for mastering it, and one
    /// past the end of the world — so a player who walks the whole map ends up knowing every doctrine in
    /// the game. If knowing one meant wearing it they would wear all nineteen, and since the catalogue is
    /// built from opposed pairs they would very nearly cancel: every finished build would arrive at the
    /// same mediocre generalist, and <c>test_every_keystone_costs_something</c> would be guarding a door
    /// with no wall around it.
    /// </para>
    /// <para>
    /// So the WORLD teaches and the BUILD chooses — the split this codebase also uses for Vows. Conquest
    /// buys knowledge; the refusal happens here, permanently, every time the player looks at three
    /// sockets and nineteen keystones. The sockets themselves are earned from the world too
    /// (<c>Unlocks.KeystoneSockets</c>), never bought with a choice-currency.
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
    /// The CHARACTER's own passive multipliers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to carry the retired Memory tree's twelve attribute nodes as well, combined with the
    /// character's. The tree is deleted and those modifiers went with it — raw numbers are Training and
    /// Gear's, specialisation is Mastery's, behaviour is Traits'. What is left is the champion's own,
    /// which is authored on the roster and visible on its card.
    /// </para>
    /// <para>
    /// Left at <see cref="BuildMods.None"/> it changes nothing, which is the correct reading for a
    /// character with no passive of its own.
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
