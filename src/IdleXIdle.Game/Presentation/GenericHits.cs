using System.Collections.Generic;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// THE GENERIC STRIKE PATH'S DECISIONS (the remaining-skill sweep, design.md section 3 and Phase 0): which blows are QUIET
/// (derived damage that never flashes, puffs or thuds) and which Aura or Skill event OWNS a Strike on a shared
/// millisecond. Pure, so the hunt's batch loop and the tests ask the same question.
/// </summary>
/// <remarks>
/// <para>
/// QUIET DERIVED HITS. A bleed of the poison pool, a thorn sent back at a biter, an overkill carried to the next creature
/// and a DEADWEIGHT release are CONSEQUENCES of a blow, not blows: they used to flash the creature, puff and thud like a
/// swing, which on a VENOM build strobed the front creature twice a second (the "bleed strobe"). They print a number at
/// QUIET grade and nothing else.
/// </para>
/// <para>
/// THE LAST-OWNER RULE. The fight's field fork emits a field's Aura and its Strikes before a cast's Skill and its Strikes
/// on the same millisecond (SoloBattle's skill loop: a Field ticks where it stands in the loop, an Active takes the beat
/// after), so a Strike belongs to the most recent Aura or Skill at its millisecond. A PULSE cast on a MIRE tick keeps
/// MIRE's Strikes as MIRE's and PULSE's as PULSE's; the old "was there an Aura at this ms?" read PULSE's as tick blows.
/// </para>
/// </remarks>
public static class GenericHits
{
    /// <summary>
    /// True for derived damage, which is presented at quiet grade with no flash, no puff and no generic thud:
    /// <see cref="HitSource.Bleed"/>, <see cref="HitSource.Reflect"/>, <see cref="HitSource.Carry"/> and
    /// <see cref="HitSource.Deadweight"/>. A swing, a skill's own hit, <see cref="HitSource.Other"/> and an event without a
    /// provenance are not quiet.
    /// </summary>
    public static bool IsQuietHit(HitSource? hit)
        => hit is HitSource.Bleed or HitSource.Reflect or HitSource.Carry or HitSource.Deadweight;

    /// <summary>
    /// HOW A BLOW'S NUMBER IS PRINTED once its grade, caption and outline have been asked separately (the Phase 1 review):
    /// a QUIET derived hit is a consequence, so it prints at <see cref="NumberGrade.Quiet"/> with no word and no outline
    /// whatever produced it, even when it shares a presented reaction's millisecond (a DEADWEIGHT release or a bleed tick
    /// on the bite's ms printed "-26 JAWS" in the reaction's Source outline beside the real "-2 JAWS"); any other blow
    /// keeps <paramref name="skill"/>'s grade, <paramref name="word"/> and <paramref name="outline"/>.
    /// </summary>
    /// <param name="quietHit">The blow is derived damage (<see cref="IsQuietHit"/>).</param>
    /// <param name="skill">The blow is a skill's own hit at its cast's millisecond.</param>
    /// <param name="word">The caption the blow would print (a reaction's name), or null.</param>
    /// <param name="outline">The outline the blow would take (a Source, FIRST BEAT's white), or null.</param>
    public static NumberLook Look(bool quietHit, bool skill, string? word, Color? outline)
        => quietHit ? new NumberLook(NumberGrade.Quiet, null, null)
                    : new NumberLook(skill ? NumberGrade.Skill : NumberGrade.Plain, word, outline);

    /// <summary>The size and brightness an ECHO is drawn at against its own cast (design.md 5.32: x0.6).</summary>
    public const float EchoScale = 0.6f;

    /// <summary>How loud an ECHO's cues are against its own cast's (design.md 5.32: x0.5).</summary>
    public const float EchoCueScale = 0.5f;

    /// <summary>
    /// ONE CREATURE, ONE INSTANT, ONE NUMBER, ONE OWNER: the Strike at <paramref name="index"/> folded with the later
    /// Strikes at its millisecond that hit the same slot with the same provenance (swing or skill), the same crit roll and
    /// the same quietness, UP TO THE NEXT OWNER: an Aura or a Skill event at that millisecond ends the fold, because its
    /// blows are its own (the last-owner rule; a PULSE cast on a MIRE tick printed MIRE's damage inside PULSE's number).
    /// </summary>
    /// <param name="batch">The frame's events, in time order.</param>
    /// <param name="index">The Strike whose number this is.</param>
    /// <param name="summed">Receives the indices folded in (they print no number of their own); may be null.</param>
    /// <param name="hits">How many Strikes the number stands for (1 when nothing folded).</param>
    /// <returns>The folded total.</returns>
    public static int Fold(IReadOnlyList<BattleEvent> batch, int index, ISet<int>? summed, out int hits)
    {
        hits = 0;
        if (batch is null || index < 0 || index >= batch.Count) return 0;
        var e = batch[index];
        var quiet = IsQuietHit(e.Hit);
        var total = e.Amount;
        hits = 1;
        for (var k = index + 1; k < batch.Count; k++)
        {
            var o = batch[k];
            if (o.AtMs != e.AtMs) break;                                             // the batch is in time order
            if (o.Kind is BattleEventKind.Aura or BattleEventKind.Skill) break;      // the next owner's blows are its own
            if (o.Kind != BattleEventKind.Strike || o.Slot != e.Slot || o.Amount <= 0) continue;
            if (o.FromSkill != e.FromSkill) continue;   // a swing and a cast stay separate
            if (o.Crit != e.Crit) continue;             // ...and so do a critical and a plain hit
            if (IsQuietHit(o.Hit) != quiet) continue;   // ...and a derived hit and a real one
            total += o.Amount;
            hits++;
            summed?.Add(k);
        }
        return total;
    }

    /// <summary>
    /// True when <paramref name="def"/> deals damage when it acts: a positive base power on anything but an amplifier.
    /// A zero-power line (REPAY's bank, PRESS, WILT) and a Mark deal nothing of their own.
    /// </summary>
    public static bool DealsDamage(SkillDef def)
        => def is not null && def.BasePower > 0f && def.Effect != SkillEffect.Amplify;

    /// <summary>
    /// True when the Skill event at <paramref name="index"/> struck: a Strike follows it at its own millisecond before the
    /// next Skill or Aura event there (the last-owner bound: a later owner's blows are not this skill's).
    /// </summary>
    public static bool IsStruck(IReadOnlyList<BattleEvent> batch, int index)
    {
        if (batch is null || index < 0 || index >= batch.Count) return false;
        var at = batch[index].AtMs;
        for (var k = index + 1; k < batch.Count; k++)
        {
            var o = batch[k];
            if (o.AtMs != at) break;                                             // the batch is in time order
            if (o.Kind is BattleEventKind.Skill or BattleEventKind.Aura) break;  // the next owner's
            if (o.Kind == BattleEventKind.Strike) return true;
        }
        return false;
    }

    /// <summary>
    /// THE PHANTOM: a damaging skill (<see cref="DealsDamage"/>) whose Skill event struck nothing. BACKDRAW answers the
    /// wave's last kill with arrows for survivors that do not exist; the fight records the cast, and the screen draws
    /// nothing for it (no callout, no effect, no cue, no tile pulse).
    /// </summary>
    public static bool IsPhantom(SkillDef def, bool struck) => !struck && DealsDamage(def);

    /// <summary>
    /// THE WEAVER ECHO (design.md 5.32, the generic half): the Skill event at <paramref name="index"/> shares its
    /// millisecond with an EARLIER Skill event of another slot in the batch. The fight weaves a cast into the next skill
    /// in the loadout on the same millisecond, right after the cast's own blows; that second Skill is the echo. A
    /// reaction (<paramref name="reactionSlots"/>) answers a bite and is neither an echo nor the cast one echoes, and a
    /// second cast of the SAME slot (the ECHO keystone) is not a woven echo.
    /// </summary>
    public static bool IsEcho(IReadOnlyList<BattleEvent> batch, int index, IReadOnlySet<int>? reactionSlots = null)
    {
        if (batch is null || index <= 0 || index >= batch.Count) return false;
        var e = batch[index];
        if (e.Kind != BattleEventKind.Skill || reactionSlots?.Contains(e.Slot) == true) return false;
        for (var k = index - 1; k >= 0; k--)
        {
            var o = batch[k];
            if (o.AtMs != e.AtMs) break;
            if (o.Kind == BattleEventKind.Skill && o.Slot != e.Slot && reactionSlots?.Contains(o.Slot) != true) return true;
        }
        return false;
    }

    /// <summary>
    /// What an arena callout says when <paramref name="def"/> acts: an ACTIVE says its own name (BLOW, REPAY, SPRAY,
    /// PAYING WORK); a Field or a Reaction says nothing (null), as JAWS / PRESS / BRAND do. The style words (HAMMER,
    /// VOLLEY, FIELD, SNARE, SIGN, DRAIN) are not arena words (design.md section 3).
    /// </summary>
    public static string? CalloutText(SkillDef def)
        => def is { Kind: SkillKind.Active } ? def.Name : null;
}

/// <summary>Which kind of event owns the skill Strikes at its millisecond (see <see cref="StrikeOwner"/>).</summary>
public enum StrikeOwnerKind
{
    /// <summary>Nothing has claimed a millisecond yet.</summary>
    None,

    /// <summary>A field's tick (<see cref="BattleEventKind.Aura"/>): its Strikes are tick blows.</summary>
    Aura,

    /// <summary>A skill acting (<see cref="BattleEventKind.Skill"/>: a cast or a reaction's answer): its Strikes are its blows.</summary>
    Skill,
}

/// <summary>
/// The LAST OWNER of a batch: the most recent Aura or Skill event, its slot and its millisecond. A skill Strike at that
/// millisecond is that event's blow (<see cref="Owns"/>). A value type the batch loop keeps in a local: no allocation.
/// </summary>
public readonly struct StrikeOwner
{
    /// <summary>Creates an owner of the given kind for a slot at a millisecond.</summary>
    public StrikeOwner(StrikeOwnerKind kind, int slot, int atMs)
    {
        Kind = kind;
        Slot = slot;
        AtMs = atMs;
    }

    /// <summary>No owner: owns nothing.</summary>
    public static StrikeOwner None => new(StrikeOwnerKind.None, -1, -1);

    /// <summary>What the owning event was.</summary>
    public StrikeOwnerKind Kind { get; }

    /// <summary>The owning event's slot: the equipped skill that ticked or acted.</summary>
    public int Slot { get; }

    /// <summary>The owning event's millisecond.</summary>
    public int AtMs { get; }

    /// <summary>
    /// The owner after <paramref name="e"/>: an Aura or a Skill event becomes the new owner; any other event leaves it.
    /// </summary>
    public StrikeOwner After(BattleEvent e) => e.Kind switch
    {
        BattleEventKind.Aura => new StrikeOwner(StrikeOwnerKind.Aura, e.Slot, e.AtMs),
        BattleEventKind.Skill => new StrikeOwner(StrikeOwnerKind.Skill, e.Slot, e.AtMs),
        _ => this,
    };

    /// <summary>
    /// True when <paramref name="strike"/> is a skill's Strike (<see cref="BattleEvent.FromSkill"/>) at this owner's
    /// millisecond. A swing is nobody's; a Strike at another millisecond is not this owner's.
    /// </summary>
    public bool Owns(BattleEvent strike)
        => Kind != StrikeOwnerKind.None && strike.Kind == BattleEventKind.Strike && strike.FromSkill && strike.AtMs == AtMs;

    /// <summary>True when <paramref name="strike"/> is a field's tick blow: owned, and the owner is an Aura.</summary>
    public bool IsAuraTick(BattleEvent strike) => Kind == StrikeOwnerKind.Aura && Owns(strike);

    /// <summary>
    /// True when <paramref name="strike"/> is the blow of the Skill event of <paramref name="slot"/>: owned, the owner is a
    /// Skill, and it is that slot's. A performed action's contact or a presented reaction's answer is ONLY its own skill's
    /// blows: a WEAVER echo shares the beat's millisecond by construction, and its Strikes are the echo's, not the action's.
    /// </summary>
    public bool IsSkillsBlow(BattleEvent strike, int slot) => Kind == StrikeOwnerKind.Skill && Slot == slot && Owns(strike);
}

/// <summary>A damage number's grade, caption and outline, decided together by <see cref="GenericHits.Look"/>.</summary>
/// <param name="Grade">How loud the number prints.</param>
/// <param name="Word">The caption beside it, or null.</param>
/// <param name="Outline">Its outline colour, or null for none.</param>
public readonly record struct NumberLook(NumberGrade Grade, string? Word, Color? Outline);

/// <summary>
/// How loud a damage number is printed (design.md section 1: quiet / skill / major grade).
/// </summary>
public enum NumberGrade
{
    /// <summary>Derived damage (bleed, reflect, carry, DEADWEIGHT): half the plain size, 70 % alpha, no outline.</summary>
    Quiet,

    /// <summary>The basic attack's number, and a field tick's summed total.</summary>
    Plain,

    /// <summary>A skill's own hit, a size up from the plain number.</summary>
    Skill,

    /// <summary>
    /// Reserved for the major grade (skill size with a Source outline and a one-word caption: FINISHED, CARRIED; Phase 3).
    /// Until then it prints at skill grade.
    /// </summary>
    Major,
}
