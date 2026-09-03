using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// The keystone catalog — the nodes that make one build a different game from another.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one is a trade, and that is enforced by test.</b> This game has rediscovered the same law
/// four times now — with gear ("I just click the best weapon"), with slot order ("everybody puts the
/// tank in front"), with Vows, and with the old palette: <i>a choice with a strictly-correct answer is
/// not a choice</i>. An upgrade everyone takes is not a build decision, it is a tax on clicking.
/// </para>
/// <para>
/// So a keystone must be REFUSABLE. Most players should look at BLOOD MAGIC, want it, and not take it.
/// That moment is the entire design.
/// </para>
/// <para>
/// They are also deliberately mutually tense: GLASS CANNON and IRONCLAD pull in opposite directions;
/// BLOOD MAGIC punishes the healing that ATTRITION rewards. You cannot take them all, and not because
/// a budget says so — because they contradict each other.
/// </para>
/// </remarks>
public static partial class Keystones
{
    public static IReadOnlyList<Keystone> Catalog { get; } = new List<Keystone>
    {
        new()
        {
            Id = "glass_cannon", Name = "GLASS CANNON",
            Blurb = "DOUBLE DAMAGE. HALF HEALTH.",
            Mods = new BuildMods(Damage: 2.0f, Health: 0.5f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
        },
        new()
        {
            Id = "ironclad", Name = "IRONCLAD",
            Blurb = "DOUBLE HEALTH. YOUR SKILLS COME BACK HALF AS OFTEN.",
            Mods = new BuildMods(Damage: 1f, Health: 2.0f, SkillRate: 0.5f, Haul: 1f, Rarity: 1f),
        },
        new()
        {
            Id = "blood_magic", Name = "BLOOD MAGIC",
            Blurb = "SKILLS COME BACK TWICE AS FAST. YOU CANNOT BE HEALED.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 2.0f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.NoHealing },
        },
        new()
        {
            Id = "bloodlust", Name = "BLOODLUST",
            Blurb = "THE CLOSER TO DEATH, THE HARDER YOU HIT. YOU TAKE 25% MORE.",
            Mods = new BuildMods(Damage: 1f, Health: 0.75f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Bloodlust },
        },
        new()
        {
            Id = "echo", Name = "ECHO",
            Blurb = "EVERY SKILL FIRES TWICE — EACH AT 60%.",
            Mods = new BuildMods(Damage: 0.6f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Echo },
        },
        new()
        {
            Id = "greed", Name = "GREED",
            Blurb = "DOUBLE LOOT. YOU HIT 30% SOFTER.",
            Mods = new BuildMods(Damage: 0.7f, Health: 1f, SkillRate: 1f, Haul: 2.0f, Rarity: 1f),
        },
        new()
        {
            Id = "discerning_eye", Name = "DISCERNING EYE",
            Blurb = "FAR RARER FINDS. HALF THE LOOT.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 1f, Haul: 0.5f, Rarity: 2.0f),
        },
        new()
        {
            Id = "reaper", Name = "REAPER",
            Blurb = "EVERY KILL GIVES RICHER LOOT. YOUR SKILLS COME BACK 25% SLOWER.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 0.75f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Splinter },
        },
        new()
        {
            Id = "undying", Name = "UNDYING",
            Blurb = "THE FIRST KILLING BLOW EACH RUN LEAVES YOU ON 1 HEALTH. YOU HIT 15% SOFTER.",
            Mods = new BuildMods(Damage: 0.85f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Undying },
        },
        new()
        {
            Id = "venomancer", Name = "VENOMANCER",
            Blurb = "YOUR SKILLS POISON. THEY HIT 20% SOFTER.",
            Mods = new BuildMods(Damage: 0.8f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Venom },
        },
        new()
        {
            // The mirror of BLOODLUST, and its opposite build: BLOODLUST wants you at the edge, JUGGERNAUT
            // wants you untouched. It denies healing, so you keep the bonus by NOT being hit — a fortress
            // that hits hardest before the first blow lands. It pairs with THE UNBROKEN vow and IRONCLAD,
            // and refuses every TRANSFORMATION/SIPHON sustain build, which is what makes it a real choice.
            Id = "juggernaut", Name = "JUGGERNAUT",
            Blurb = "THE FULLER YOUR HEALTH, THE HARDER YOU HIT. YOU CANNOT BE HEALED.",
            Grants = new[] { BuildTrigger.Zeal, BuildTrigger.NoHealing },
        },
        new()
        {
            // The loot build's keystone — trade raw killing power for a flood of rarity. Refuses the
            // damage keystones; wants the FORTUNE road and a GREED/DISCERNING-EYE haul build.
            Id = "fortune", Name = "FORTUNE",
            Blurb = "DOUBLE LOOT RARITY. YOU HIT 25% SOFTER.",
            Mods = new BuildMods(Damage: 0.75f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 2.0f),
        },
        new()
        {
            // The immovable fortress — even tougher than IRONCLAD, and it pays for it in cadence. Pure
            // trade, no trigger: triple health, but skills far slower than IRONCLAD's — so it does NOT
            // dominate IRONCLAD (more health, worse cadence). Pulls hard against GLASS CANNON/ECHO.
            Id = "titan", Name = "TITAN",
            Blurb = "TRIPLE HEALTH. YOUR SKILLS COME BACK 60% SLOWER.",
            Mods = new BuildMods(Damage: 1f, Health: 3.0f, SkillRate: 0.4f, Haul: 1f, Rarity: 1f),
        },
        new()
        {
            // THE AVARICE TERMINAL. The path that ends here buys no combat power at all — that is what
            // makes choosing it a real decision — and this is what stops it being a dead end: the haul a
            // Greed build stacks becomes hit size. It pays for it in RARITY, the loot build's other half,
            // so taking it means choosing between being rich and being dangerous rather than getting both.
            Id = "hoarder", Name = "HOARDER",
            Blurb = "MORE LOOT MEANS HARDER HITS. RARE FINDS COME HALF AS OFTEN.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 0.5f),
            Grants = new[] { BuildTrigger.Hoarder },
        },
        new()
        {
            // THE CHARGE SPENDER. CHARGE is the game's one shared stack primitive: every cast
            // stores a point, and only a reader makes the pool real. REND turns a rotation into a
            // rhythm — pool through the other skills, dump through the Strike — and pays for the
            // spikes with a flat tax on everything. Wants TEMPO (faster pooling) or DYNAMO (bites
            // pool too); refuses builds with no Strike at all.
            Id = "rend", Name = "REND",
            Blurb = "EVERY SKILL USE STORES 1 CHARGE, UP TO 10. YOUR STRIKES SPEND IT ALL, +5% PER CHARGE. ALL HITS 10% SOFTER.",
            Mods = new BuildMods(Damage: 0.9f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Rend },
        },
        new()
        {
            // The bender: it changes what the pool CAN be, not what anything does. Deliberately
            // inert without REND — the blurb says so — because a cap on a pool nobody spends is
            // the exact dormant shape this game hunts, and here it must be a visible player error.
            Id = "capacitor", Name = "CAPACITOR",
            Blurb = "YOUR CHARGE POOL HOLDS 20, NOT 10. LODESTONE THEN NEEDS ALL 20. SKILLS COME BACK 15% SLOWER. USELESS UNLESS A KEYSTONE USES THE POOL.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 0.85f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Capacitor },
        },
        new()
        {
            // The wall that winds the spring: being hit feeds the pool, so an ENDURE chassis can
            // fuel a REND spike without racing TEMPO for cast count. Pays in the same currency it
            // farms — you take more of every bite you are milking.
            Id = "dynamo", Name = "DYNAMO",
            Blurb = "EVERY HIT YOU TAKE STORES 2 CHARGE. YOU TAKE 15% MORE.",
            Mods = new BuildMods(Damage: 1f, Health: 0.85f, SkillRate: 1f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Dynamo },
        },
        new()
        {
            // The HOLDER — the direct rival of REND: one wants the pool dumped through a Strike,
            // one wants it carried whole to the clear. Socketing both is a rhythm argument the
            // player has to settle in the rotation, which is exactly the min/max texture CHARGE
            // exists to create. The AVARICE entry in the set: it pays in loot, and costs rarity.
            Id = "lodestone", Name = "LODESTONE",
            Blurb = "CLEAR A WAVE WITH A FULL CHARGE POOL TO GET A SPARE CORE. RARE FINDS COME 20% LESS OFTEN.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 0.8f),
            Grants = new[] { BuildTrigger.Lodestone },
        },
        new()
        {
            // THE ARTIFICE TERMINAL. Strange rather than large: one slot answers two of the content's
            // four demands, which no other thing in the game can do. The cadence price is what keeps it
            // from being simply "more damage" — a Weaver casts less often and covers more ground.
            Id = "weaver", Name = "WEAVER",
            Blurb = "EVERY SKILL ALSO FIRES THE NEXT SKILL IN YOUR BUILD, AT 45%. SKILLS RETURN 30% SLOWER.",
            Mods = new BuildMods(Damage: 1f, Health: 1f, SkillRate: 0.70f, Haul: 1f, Rarity: 1f),
            Grants = new[] { BuildTrigger.Weaver },
        },
    };

    public static Keystone? ById(string? id) => id is null ? null : Catalog.FirstOrDefault(k => k.Id == id);

    /// <summary>
    /// Does this keystone actually cost something? The law of the catalog, in one method.
    /// </summary>
    /// <remarks>
    /// Exposed so a test can ask it of every entry rather than trusting the author — including the
    /// author six months from now who adds "+20% damage, no downside" at 2am because it seemed fine.
    /// </remarks>
    public static bool IsATrade(Keystone k)
    {
        ArgumentNullException.ThrowIfNull(k);
        var m = k.Mods;
        var fields = new[] { m.Damage, m.Health, m.SkillRate, m.Haul, m.Rarity };

        var gives = fields.Any(f => f > 1f) || k.Grants.Count > 0;
        var costs = fields.Any(f => f < 1f) || k.Grants.Contains(BuildTrigger.NoHealing);
        return gives && costs;
    }
}
