using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;

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
            Blurb = "Walked every road far enough to know none of them is home.",
            Lean = null, Aptitude = null,
            PassiveName = "EVEN HAND",
            PassiveText = "No road favours you and none refuses you. Every skill hits 8% harder.",
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
            Blurb = "Hits things until they are a different shape.",
            Lean = Branch.Weight, Aptitude = Form.Strike,
            PassiveName = "DEADWEIGHT",
            PassiveText = "A third of a hit's overkill carries to the next living creature.",
            // Weight's structural weakness is a swarm: an enormous hit on a small creature throws most
            // of itself away. This does not fix that — it refunds a third of it, which is the most a
            // character passive should do to a branch's actual price.
            Shape = new SkillShape { OverkillCarry = 0.33f },
            Unlock = CharacterUnlock.Conquest("cinderworks"),
        },
        new()
        {
            Id = "chorus", Name = "THE CHORUS",
            Blurb = "Never speaks. The charms do it.",
            Lean = Branch.Spread, Aptitude = Form.Aura,
            PassiveName = "MANY MOUTHS",
            PassiveText = "+5% damage for every living creature in the wave.",
            // Scales with the thing Spread is for and evaporates in a boss room, which is the honest
            // shape of the branch rather than a flat bonus wearing its colours.
            Shape = new SkillShape { PerCreatureBonus = 0.05f },
            Unlock = CharacterUnlock.Conquest("umbral_reach"),
        },
        new()
        {
            Id = "metronome", Name = "THE METRONOME",
            Blurb = "Keeps time. The fight is what happens between the beats.",
            Lean = Branch.Tempo, Aptitude = Form.Projectile,
            PassiveName = "FIRST BEAT",
            PassiveText = "The opening cast of every wave is free, and lands at double force.",
            Shape = new SkillShape { FreeOpeningCast = true, FirstHitMultiplier = 2f },
            Unlock = CharacterUnlock.Conquest("marrow_wastes"),
        },
        new()
        {
            Id = "unbroken", Name = "THE UNBROKEN",
            Blurb = "Has been killed. Declined.",
            Lean = Branch.Endure, Aptitude = Form.Transformation,
            PassiveName = "SECOND WIND",
            PassiveText = "The first killing blow of an expedition leaves you standing at 1 health.",
            Grants = new[] { BuildTrigger.Undying },
            Unlock = CharacterUnlock.Conquest("still_archive"),
        },

        // ── The bridges, where the tree puts its own ──────────────────────────────────────────────
        new()
        {
            Id = "tower", Name = "THE FALLING TOWER",
            Blurb = "Slow. Arrives anyway.",
            Lean = Branch.Weight, Aptitude = Form.Strike, AptitudePower = 1.15f,
            PassiveName = "MOMENTUM",
            PassiveText = "Hits grow as a fight goes on: every blow after the first on a creature is "
                          + "stronger, and the first is weaker.",
            // A Weight+Tempo bridge stated as a trade rather than a bonus. It is the exact inverse of
            // ALPHA in the mastery tree, so the two cancel — which is the point: this character is for
            // players who did not walk that node.
            Shape = new SkillShape { FirstHitMultiplier = 0.85f, LaterHitMultiplier = 1.35f },
            Unlock = CharacterUnlock.Conquest("pale_choir"),
        },
        new()
        {
            Id = "quiver", Name = "THE QUIVER",
            Blurb = "Counts arrows the way other people count breaths.",
            Lean = Branch.Tempo, Aptitude = Form.Projectile, AptitudePower = 1.35f,
            PassiveName = "LOOSE AGAIN",
            PassiveText = "A kill sends the next shot immediately.",
            Grants = new[] { BuildTrigger.Splinter },
            Shape = new SkillShape { SkillRate = 1.10f },
            Unlock = CharacterUnlock.Quest("q_hollow_hunt", "Finish the hunt in the Verdant Hollow"),
        },
        new()
        {
            Id = "thornwall", Name = "THE THORNWALL",
            Blurb = "Stands where the road narrows, and lets it narrow further.",
            Lean = Branch.Endure, Aptitude = Form.Trap, AptitudePower = 1.35f,
            PassiveName = "REPRISAL",
            PassiveText = "Every bite you take is worth less, and every trap you set is worth more.",
            Shape = new SkillShape { FlatDamageReduction = 6f, DamageTaken = 0.90f },
            Unlock = CharacterUnlock.Conquest("verdant_hollow"),
        },

        // ── Off the tree ──────────────────────────────────────────────────────────────────────────
        new()
        {
            Id = "oathbound", Name = "THE OATHBOUND",
            Blurb = "Gave up their eyes for a better bargain.",
            Lean = null, Aptitude = Form.Mark, AptitudePower = 1.30f,
            PassiveName = "TWICE SWORN",
            PassiveText = "Vows pay far more, and the Mark window they buy lasts longer.",
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
            Blurb = "Fights for the pockets, not the glory.",
            Lean = null, Aptitude = Form.Trap,
            PassiveName = "FULL POCKETS",
            PassiveText = "Everything you drag home is worth more, and the good things come up more often.",
            // Haul and Rarity, which the AVARICE road also buys — so this character is the cheap
            // version of a thirty-point path, and the road stays worth walking because it goes further.
            Mods = new BuildMods(1f, 1f, 1f, 1.35f, 1.20f),
            Unlock = CharacterUnlock.Conquest("cinderworks"),
        },
    };

    public static Character Get(string id) => Find(id) ?? All.First(c => c.Id == StarterId);

    public static Character? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}
