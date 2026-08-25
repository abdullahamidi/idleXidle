using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Characters;

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
/// Unlocks follow conquest, which already exists, with quest gates declared for two characters so the
/// second mechanism does not have to be bolted on later. See <see cref="UnlockKind"/>.
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
            Class = ItemClass.Wanderer,
            Blurb = "Walked every road far enough to know none of them is home.",
            Lean = null, Aptitude = null,
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
            Class = ItemClass.Warden,
            Blurb = "Hits things until they are a different shape.",
            Lean = Branch.Weight, Aptitude = Form.Strike,
            PassiveName = "DEADWEIGHT",
            PassiveText = "A third of the damage left over from a kill hits the next enemy.",
            // Weight's structural weakness is a swarm: an enormous hit on a small creature throws most
            // of itself away. This does not fix that — it refunds a third of it, which is the most a
            // character passive should do to a branch's actual price.
            Shape = new SkillShape { OverkillCarry = 0.33f },
            Unlock = CharacterUnlock.Conquest("cinderworks"),
        },
        new()
        {
            Id = "chorus", Name = "THE CHORUS",
            Class = ItemClass.Ranger,
            Blurb = "Never speaks. The charms do it.",
            Lean = Branch.Spread, Aptitude = Form.Aura,
            PassiveName = "MANY MOUTHS",
            PassiveText = "+5% damage for each enemy alive in the wave.",
            // Scales with the thing Spread is for and evaporates in a boss room, which is the honest
            // shape of the branch rather than a flat bonus wearing its colours.
            Shape = new SkillShape { PerCreatureBonus = 0.05f },
            Unlock = CharacterUnlock.Conquest("umbral_reach"),
        },
        new()
        {
            Id = "metronome", Name = "THE METRONOME",
            Class = ItemClass.Mystic,
            Blurb = "Keeps time. The fight is what happens between the beats.",
            Lean = Branch.Tempo, Aptitude = Form.Projectile,
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
            Class = ItemClass.Bulwark,
            Blurb = "Has been killed. Declined.",
            Lean = Branch.Endure, Aptitude = Form.Transformation,
            PassiveName = "SECOND WIND",
            PassiveText = "Once per expedition, a hit that would kill you leaves you at 1 health.",
            Grants = new[] { BuildTrigger.Undying },
            Unlock = CharacterUnlock.Conquest("still_archive"),
        },

        // ── The bridges, where the tree puts its own ──────────────────────────────────────────────
        new()
        {
            Id = "tower", Name = "THE FALLING TOWER",
            Class = ItemClass.Warden,
            Blurb = "Slow. Arrives anyway.",
            Lean = Branch.Weight, Aptitude = Form.Strike, AptitudePower = 1.15f,
            PassiveName = "MOMENTUM",
            PassiveText = "Your first hit on each enemy is weaker. Every hit after it is stronger.",
            // A Weight+Tempo bridge stated as a trade rather than a bonus. It is the exact inverse of
            // ALPHA in the mastery tree, so the two cancel — which is the point: this character is for
            // players who did not walk that node.
            Shape = new SkillShape { FirstHitMultiplier = 0.85f, LaterHitMultiplier = 1.35f },
            Unlock = CharacterUnlock.Conquest("pale_choir"),
        },
        new()
        {
            Id = "quiver", Name = "THE QUIVER",
            Class = ItemClass.Mystic,
            Blurb = "Counts arrows the way other people count breaths.",
            Lean = Branch.Tempo, Aptitude = Form.Projectile, AptitudePower = 1.35f,
            PassiveName = "LOOSE AGAIN",
            PassiveText = "A kill sends the next shot immediately.",
            // LooseAgain, not Splinter. The card reads "a kill sends the next shot immediately" and the
            // grant was SPLINTER, whose own blurb is "on kill: richer loot" — an action-economy promise
            // paid out as a loot bonus, on the passive a player unlocks by finishing a quest for it.
            Grants = new[] { BuildTrigger.LooseAgain },
            Shape = new SkillShape { SkillRate = 1.10f },
            Unlock = CharacterUnlock.Quest("q_hollow_hunt", "Finish the hunt in the Verdant Hollow"),
        },
        new()
        {
            Id = "thornwall", Name = "THE THORNWALL",
            Class = ItemClass.Bulwark,
            Blurb = "Stands where the road narrows, and lets it narrow further.",
            Lean = Branch.Endure, Aptitude = Form.Trap, AptitudePower = 1.35f,
            PassiveName = "REPRISAL",
            PassiveText = "Every hit you take does less. Every trap you set does more.",
            Shape = new SkillShape { FlatDamageReduction = 6f, DamageTaken = 0.90f },
            Unlock = CharacterUnlock.Conquest("verdant_hollow"),
        },

        // ── Off the tree ──────────────────────────────────────────────────────────────────────────
        new()
        {
            Id = "oathbound", Name = "THE OATHBOUND",
            Class = ItemClass.Ranger,
            Blurb = "Gave up their eyes for a better bargain.",
            Lean = null, Aptitude = Form.Mark, AptitudePower = 1.30f,
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
                VowPowerMultiplier = 1.5f, MarkWindowMultiplier = 1.5f, MarkPowerBonus = 0.25f,
            },
            Unlock = CharacterUnlock.Quest("q_first_vow", "Swear and keep a Vow through a full descent"),
        },
        new()
        {
            Id = "magpie", Name = "THE MAGPIE",
            Class = ItemClass.Wanderer,
            Blurb = "Fights for the pockets, not the glory.",
            Lean = null, Aptitude = Form.Trap,
            PassiveName = "FULL POCKETS",
            PassiveText = "All the loot you bring home is worth more. Rare finds show up more often.",
            // Haul and Rarity, which the AVARICE road also buys — so this character is the cheap
            // version of a thirty-point path, and the road stays worth walking because it goes further.
            Mods = new BuildMods(1f, 1f, 1f, 1.35f, 1.20f),
            Unlock = CharacterUnlock.Conquest("cinderworks"),
        },
    };

    public static Character Get(string id) => Find(id) ?? All.First(c => c.Id == StarterId);

    public static Character? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}
