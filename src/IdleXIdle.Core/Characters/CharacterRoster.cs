using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;

namespace IdleXIdle.Core.Characters;

/// <summary>
/// The ten playable characters.
/// </summary>
/// <remarks>
/// <para>
/// The roster is laid out on the MASTERY TREE, so it reads as a map rather than a list: four
/// characters stand on the four opposed roads, three on bridges between adjacent roads exactly where
/// the tree puts its own, one is the neutral starter, and two stand off the tree entirely. A player who
/// has read the tree already knows what half of this roster does.
/// </para>
/// <para>
/// Every passive is a value the SIM reads — a <see cref="BuildMods"/> multiplier, a
/// <see cref="SkillShape"/> field, or a <see cref="BuildTrigger"/> the battle asks about. None of them
/// is screen text. That rule is not stylistic: the single most expensive bug in this project's history
/// was <c>BuildMods.Rarity</c>, resolved and carried and summed by correct code at every step and read
/// by nothing at all for the whole of development.
/// </para>
/// <para>
/// Unlocks come in two tiers per class — see <see cref="ClassTier"/>. The FIRST of each class follows
/// conquest (or is the starter); the SECOND waits on a quest with a real demand behind it, because a
/// roster that arrives whole in the first hours (playtest 2026-08-26: "all the characters unlock far
/// too easily") is a roster with nothing left to go and get. Every quest here asks for something NO
/// conquest gives — waves well past the conquest line, chests, kept Vows — and never "conquer N
/// regions": the whole-map quest was a conquest wearing a quest's name, and it handed the second
/// Bulwark over one region after the first (playtest 2026-08-26: "both Bulwarks unlock at the same
/// time — silly"). <c>ChampionTiersTest</c> holds every quest to that.
/// </para>
/// <para>
/// The ROSTER screen lays the ten out by CLASS, not by catalogue order — see <see cref="Grid"/>: one
/// column per class, the FIRST above the SECOND (playtest 2026-08-26: "characters of the same class
/// should sit one under the other"). The screen only walks the cells; the arrangement is data here so
/// a test can hold it.
/// </para>
/// </remarks>
public static class CharacterRoster
{
    public const string StarterId = "seeker";

    public static IReadOnlyList<Character> All { get; } = new List<Character>
    {
        // ── The starter ───────────────────────────────────────────────────────────────────────────
        new()
        {
            Id = "seeker", Name = "THE SEEKER",
            SignatureSkillId = "sig_seeker_hard_hands",   // HARD HANDS — the champion with no discipline fights with its hands
            Class = ItemClass.Wanderer, Tier = ClassTier.First,
            Blurb = "Walked every road far enough to know none of them is home.",
            Lean = null,
            PassiveName = "EVEN HAND",
            PassiveText = "Any road fits you. Every skill hits 8% harder.",
            // Deliberately the dullest passive in the roster, and deliberately the one that is never
            // wrong. A starter has to be playable before the player understands anything, which means
            // it cannot be a build — it has to be a floor.
            Shape = new SkillShape { HitSize = 1.08f },
            Unlock = CharacterUnlock.Start,
        },

        // ── The four roads ────────────────────────────────────────────────────────────────────────
        new()
        {
            Id = "anvil", Name = "THE ANVIL",
            SignatureSkillId = "sig_anvil_hardface",   // HARDFACE — every enemy that goes down leaves the next one softer
            Class = ItemClass.Warden, Tier = ClassTier.First,
            Blurb = "Hits things until they are a different shape.",
            Lean = Branch.Resonance,
            PassiveName = "DEADWEIGHT",
            PassiveText = "A third of the damage left over from a kill hits the next enemy.",
            // A heavy hitter's structural weakness is a swarm: an enormous hit on a small
            // creature throws most of itself away. This does not fix that — it refunds a third,
            // the most a character passive should do to a playstyle's actual price.
            Shape = new SkillShape { OverkillCarry = 0.33f },
            Unlock = CharacterUnlock.Conquest("cinderworks"),
        },
        new()
        {
            Id = "chorus", Name = "THE CHORUS",
            SignatureSkillId = "sig_chorus_grave_song",   // GRAVE SONG — the charms get louder for every enemy the wave has lost
            Class = ItemClass.Ranger, Tier = ClassTier.First,
            Blurb = "Never speaks. The charms do it.",
            Lean = Branch.Loot,
            PassiveName = "MANY MOUTHS",
            PassiveText = "+5% damage for each enemy alive in the wave.",
            // Scales with the crowd a many-target build is for and evaporates in a boss room —
            // the honest shape of that playstyle rather than a flat bonus wearing its colours.
            Shape = new SkillShape { PerCreatureBonus = 0.05f },
            Unlock = CharacterUnlock.Conquest("umbral_reach"),
        },
        new()
        {
            Id = "metronome", Name = "THE METRONOME",
            SignatureSkillId = "sig_metronome_clockwork",   // CLOCKWORK — the one skill counted in seconds, which nothing can hurry
            Class = ItemClass.Mystic, Tier = ClassTier.First,
            Blurb = "Keeps time. The fight is what happens between the beats.",
            Lean = Branch.Tempo,
            PassiveName = "FIRST BEAT",
            // 2026-08-23: this said "free", which reads as a resource cost — the game has no mana and
            // never had one. FreeOpeningCast waives the WAIT (the 700ms wave-opening pause, and the
            // one-cooldown wait before a skill's opener), and FirstHitMultiplier doubles the first hit
            // landed on EACH enemy, not the first cast. Both halves now say what they really do.
            PassiveText = "The wave's first cast has no wait. Your first hit on each enemy is doubled.",
            Shape = new SkillShape { FreeOpeningCast = true, FirstHitMultiplier = 2f },
            Unlock = CharacterUnlock.Conquest("marrow_wastes"),
        },
        new()
        {
            Id = "unbroken", Name = "THE UNBROKEN",
            SignatureSkillId = "sig_unbroken_hold_fast",   // HOLD FAST — a wall put back up every two seconds, for free
            Class = ItemClass.Bulwark, Tier = ClassTier.First,
            Blurb = "Has been killed. Declined.",
            Lean = Branch.Endure,
            PassiveName = "SECOND WIND",
            PassiveText = "Once per expedition, a hit that would kill you leaves you at 1 health.",
            Grants = new[] { BuildTrigger.Undying },
            Unlock = CharacterUnlock.Conquest("still_archive"),
        },

        // ── The bridges, where the tree puts its own ──────────────────────────────────────────────
        new()
        {
            Id = "tower", Name = "THE FALLING TOWER",
            SignatureSkillId = "sig_tower_slow_fall",   // SLOW FALL — stones that arrive on their own clock, never on an action
            Class = ItemClass.Warden, Tier = ClassTier.Second,
            Blurb = "Slow. Arrives anyway.",
            Lean = Branch.Resonance,
            PassiveName = "MOMENTUM",
            PassiveText = "Your first hit on each enemy is weaker. Every hit after it is stronger.",
            // A heavy-hit/Tempo bridge stated as a trade rather than a bonus. It is the exact inverse of
            // ALPHA in the mastery tree, so the two cancel — which is the point: this character is for
            // players who did not walk that node.
            Shape = new SkillShape { FirstHitMultiplier = 0.85f, LaterHitMultiplier = 1.35f },
            // SECOND WARDEN. Was the last region's conquest; now the depth quest in the first Warden's
            // own region, so the road to the second Warden runs through the place the first one came from.
            Unlock = CharacterUnlock.Quest("q_cinder_deep", "Reach wave 50 in Cinderworks"),
        },
        new()
        {
            Id = "quiver", Name = "THE QUIVER",
            SignatureSkillId = "sig_quiver_backdraw",   // BACKDRAW — a death sends arrows that never needed an action
            Class = ItemClass.Mystic, Tier = ClassTier.Second,
            Blurb = "Counts arrows the way other people count breaths.",
            Lean = Branch.Tempo,
            PassiveName = "LOOSE AGAIN",
            // BOTH halves of the passive, on the card: the shape's +10% skill rate shipped
            // undisclosed — a number the sim read on every cast that no screen ever admitted to.
            PassiveText = "A kill sends the next shot immediately, and your skills come back 10% sooner.",
            // LooseAgain, not Splinter. The card reads "a kill sends the next shot immediately" and the
            // grant was SPLINTER, whose own blurb is "on kill: richer loot" — an action-economy promise
            // paid out as a loot bonus, on the passive a player unlocks by finishing a quest for it.
            Grants = new[] { BuildTrigger.LooseAgain },
            Shape = new SkillShape { SkillRate = 1.10f },
            // SECOND MYSTIC. Gate history: depth 20 (free at conquest), thirty chests (moved to
            // the loot champion), wave 60 in the Hollow (the catalogue's THIRD depth quest). P10
            // finally asks for the thing this champion IS: practice with VOLLEY skills, read from
            // the same tally that levels them.
            Unlock = CharacterUnlock.Quest("q_quiver_volleys", "Clear 150 waves with a VOLLEY skill equipped"),
        },
        new()
        {
            Id = "thornwall", Name = "THE THORNWALL",
            SignatureSkillId = "sig_thornwall_narrows",   // NARROWS — a fixed answer that grows with every bite it has answered
            Class = ItemClass.Bulwark, Tier = ClassTier.Second,
            Blurb = "Stands where the road narrows, and lets it narrow further.",
            Lean = Branch.Endure,
            PassiveName = "REPRISAL",
            PassiveText = "Every hit you take does less. Every trap you set does more.",
            // BOTH sentences of the card. "Every trap you set does more" used to be implemented by
            // the legacy Form aptitude and nothing else — deleting that without this line would have
            // turned half the card into a lie no test catches.
            Shape = new SkillShape
            {
                FlatDamageReduction = 6f, DamageTaken = 0.90f,
                StylePower = new Dictionary<Style, float> { [Style.Snare] = 1.35f },
            },
            // SECOND BULWARK. Was the FIRST region's conquest — the earliest unlock in the game — then
            // "conquer every region". That was still a conquest: THE UNBROKEN opens on the fifth
            // region's conquest and the whole map is the sixth, one region later, the same effort again
            // (playtest 2026-08-26: "both Bulwarks unlock at the same time — silly. Bulwark stands for
            // endurance"). So the gate is endurance: HOLD wave 80 in the Body region, four times the
            // conquest line, which no conquest anywhere can hand over.
            Unlock = CharacterUnlock.Quest("q_marrow_hold", "Hold wave 80 in Marrow Wastes"),
        },

        // ── Off the tree ──────────────────────────────────────────────────────────────────────────
        new()
        {
            Id = "oathbound", Name = "THE OATHBOUND",
            SignatureSkillId = "sig_oathbound_oathmark",   // OATHMARK — the only amplifier whose price is being hit
            Class = ItemClass.Ranger, Tier = ClassTier.Second,
            Blurb = "Gave up their eyes for a better bargain.",
            Lean = null,
            PassiveName = "TWICE SWORN",
            PassiveText = "Vows pay far more. Your Marks last longer and hit harder.",
            // The only character built around a SYSTEM rather than a branch. A player with no Vows
            // sworn gets the Mark aptitude and nothing else, which is the correct price for a passive
            // that doubles down on a choice they have not made.
            // BOTH clauses of the sentence. "Vows pay far more" had no field to write to until
            // SkillShape.VowPowerMultiplier existed, so the half of this passive the character is
            // NAMED for did nothing at all — on the one character built around a system rather than
            // a branch, and the one gated behind the quest that teaches Vows.
            Shape = new SkillShape
            {
                VowPowerMultiplier = 1.5f, AmplifyWindowMultiplier = 1.5f, AmplifyPowerBonus = 0.25f,
            },
            // SECOND RANGER. One kept Vow was the old gate; three is a habit rather than an accident.
            Unlock = CharacterUnlock.Quest("q_three_vows", "Finish three descents with a Vow's demand still met"),
        },
        new()
        {
            Id = "magpie", Name = "THE MAGPIE",
            SignatureSkillId = "sig_magpie_paying_work",   // PAYING WORK — aimed at the one wave the Magpie actually wants
            Class = ItemClass.Wanderer, Tier = ClassTier.Second,
            Blurb = "Fights for the pockets, not the glory.",
            Lean = null,
            PassiveName = "FULL POCKETS",
            PassiveText = "All the loot you bring home is worth more. Rare finds show up more often.",
            // Haul and Rarity, which the AVARICE road also buys — so this character is the cheap
            // version of a thirty-point path, and the road stays worth walking because it goes further.
            Mods = new BuildMods(1f, 1f, 1f, 1.35f, 1.20f),
            // SECOND WANDERER. Was Cinderworks' conquest; then wave 60 in the Hollow; then thirty
            // chests — the one gate in the game hostage to a drop roll (a flat 20% boss chest).
            // P10 counts the BOSSES the chests came from instead: every fifth wave holds one,
            // deterministically, and the pockets still fill along the way.
            Unlock = CharacterUnlock.Quest("q_magpie_bosses", "Fell 40 bosses"),
        },
    };

    public static Character Get(string id) => Find(id) ?? All.First(c => c.Id == StarterId);

    public static Character? Find(string id) => All.FirstOrDefault(c => c.Id == id);

    // ── The roster as a grid ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The five classes in the order the ROSTER screen lays out its columns, left to right: the
    /// starter's class first, then the four roads in the order the first-tier champions stand on them.
    /// </summary>
    /// <remarks>
    /// This is the order the top row already had when the grid was catalogue order (SEEKER, ANVIL,
    /// CHORUS, METRONOME, UNBROKEN); pinning it here means the class columns did not move under a
    /// player who had learned where each card stood.
    /// </remarks>
    public static IReadOnlyList<ItemClass> ClassColumns { get; } = new[]
    {
        ItemClass.Wanderer, ItemClass.Warden, ItemClass.Ranger, ItemClass.Mystic, ItemClass.Bulwark,
    };

    /// <summary>
    /// The roster laid out the way the ROSTER screen draws it: one column per class in
    /// <see cref="ClassColumns"/> order, the FIRST of the class on row 0 and the SECOND directly
    /// beneath on row 1. Walked row by row, left to right.
    /// </summary>
    /// <remarks>
    /// Playtest (2026-08-26): "characters of the same class should sit one under the other." Before
    /// this the screen drew the catalogue five to a row, so the second row was WARDEN, MYSTIC, BULWARK,
    /// RANGER, WANDERER under a first row of WANDERER, WARDEN, RANGER, MYSTIC, BULWARK — the pairs were
    /// on the screen and nowhere near each other.
    /// </remarks>
    public static IReadOnlyList<RosterCell> Grid { get; } = BuildGrid();

    /// <summary>A class's two champions, the FIRST before the SECOND.</summary>
    public static IReadOnlyList<Character> ByClass(ItemClass cls) =>
        All.Where(c => c.Class == cls).OrderBy(c => c.Tier).ToList();

    private static IReadOnlyList<RosterCell> BuildGrid()
    {
        var cells = new List<RosterCell>();
        foreach (var tier in new[] { ClassTier.First, ClassTier.Second })
            for (var column = 0; column < ClassColumns.Count; column++)
            {
                var cls = ClassColumns[column];
                // Single, not First: a class with two FIRSTs or none is a roster bug, and the grid is
                // the one place every class is looked at by tier, so it is the right place to fail.
                var champion = All.Single(c => c.Class == cls && c.Tier == tier);
                cells.Add(new RosterCell(champion, column, (int)tier));
            }
        return cells;
    }
}

/// <summary>One champion's place on the ROSTER grid: which class column, and which tier row.</summary>
/// <param name="Character">The champion on the card.</param>
/// <param name="Column">The class column, an index into <see cref="CharacterRoster.ClassColumns"/>.</param>
/// <param name="Row">0 for the FIRST of the class, 1 for the SECOND beneath it.</param>
public readonly record struct RosterCell(Character Character, int Column, int Row);
