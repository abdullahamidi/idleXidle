using System;
using System.Collections.Generic;

namespace IdleXIdle.Game.Presentation;

/// <summary>The moment of an action an archetype cue voices (design.md section 7).</summary>
public enum ArchetypeMoment
{
    /// <summary>A contact: a hit family, throttled at <see cref="ArchetypeCues.HitGapMs"/>.</summary>
    Hit,

    /// <summary>A release (a throw, a push of air), on the release frame.</summary>
    Release,

    /// <summary>A field's tick, throttled at <see cref="ArchetypeCues.TickGapMs"/>.</summary>
    Tick,

    /// <summary>A commit (the body's wind-up into an action).</summary>
    Commit,
}

/// <summary>
/// One ARCHETYPE cue: the material sound a resolution chain falls back to before the generic cue, and the volume band its
/// consumers play it in (the band the file is measured against its ceiling at: <c>make_archetypes.py</c>,
/// <c>sweep_audio_hierarchy_test</c>).
/// </summary>
/// <param name="Key">The cue's file name, without extension, in <c>assets/audio/combat/</c>.</param>
/// <param name="Moment">The moment it voices.</param>
/// <param name="Material">What it is made of (the design's words).</param>
/// <param name="MinVolume">The quietest volume a consumer plays it at.</param>
/// <param name="MaxVolume">The loudest volume a consumer plays it at: the level checked against <paramref name="Ceiling"/>.</param>
/// <param name="Ceiling">The closed reference cue it must stay under.</param>
/// <param name="CeilingVolume">The volume that reference plays at.</param>
/// <param name="Consumers">Who plays it: the Phase-1 performers, or the later items whose chains name it.</param>
public sealed record ArchetypeCue(string Key, ArchetypeMoment Moment, string Material, float MinVolume, float MaxVolume,
                                  string Ceiling, float CeilingVolume, string Consumers);

/// <summary>
/// THE EIGHT ARCHETYPE CUES (the remaining-skill sweep, design.md section 7: "archetypes first, identity second"). Every
/// item resolves its sound most specific first, <c>sfx_&lt;champ&gt;_&lt;skill&gt;_&lt;moment&gt;</c> -&gt;
/// <c>sfx_&lt;skill&gt;_&lt;moment&gt;</c> -&gt; ARCHETYPE -&gt; generic, so these give every item a non-generic sound from its
/// first film. Built from CC0 recorded foley by <c>tools/asset-pipeline/make_archetypes.py</c>; pinned as SHIPPED (never
/// human-approved) by <c>archetype_cue_test</c>.
/// </summary>
/// <remarks>
/// Five have Phase-1 performers (the ten basic attacks' contact and release chains, <see cref="SwingRecipes"/>). Three are
/// built now, per the design's order, ahead of their first consumers: <see cref="AirRelease"/> (PULSE / REPAY / DRINK's
/// intake, Phases 2-3), <see cref="FieldTick"/> (MIRE / WILT's deepen, Phase 2) and <see cref="ClothCommit"/> (every commit
/// until its own, Phase 3). This table names them so the consumer gate (<c>tools/check_asset_consumers.py</c>) and the
/// hierarchy test read one list. No archetype is a Seeker cue: the closed references keep their own files, which resolve
/// first in their chains.
/// </remarks>
public static class ArchetypeCues
{
    /// <summary>A padded fist.</summary>
    public const string FistHit = "sfx_fist_hit";

    /// <summary>A short cut into leather.</summary>
    public const string BladeHit = "sfx_blade_hit";

    /// <summary>A stone on packed earth.</summary>
    public const string StoneHit = "sfx_stone_hit";

    /// <summary>A wooden thump with a rim.</summary>
    public const string WoodHit = "sfx_wood_hit";

    /// <summary>A cloth whip + air.</summary>
    public const string ThrowRelease = "sfx_throw_release";

    /// <summary>An air push, no tone.</summary>
    public const string AirRelease = "sfx_air_release";

    /// <summary>A quiet material settle.</summary>
    public const string FieldTick = "sfx_field_tick";

    /// <summary>A boot plant + cloth.</summary>
    public const string ClothCommit = "sfx_cloth_commit";

    /// <summary>The minimum gap between two starts of a HIT family's cue, ms (design.md section 7's mix rules).</summary>
    public const long HitGapMs = 60;

    /// <summary>The minimum gap between two starts of a TICK's cue, ms (design.md section 7's mix rules).</summary>
    public const long TickGapMs = 90;

    /// <summary>
    /// The eight, in the design's order, with their consumers' volume bands and ceilings (design.md sections 3 and 7): a
    /// hit is T1 (0.34-0.38) under SPRAY's contact (0.50) and HARD HANDS' hit (0.55); a basic attack's release 0.16-0.18
    /// and the air push's consumers 0.22-0.26 under SPRAY's release (0.40); a field tick at most 0.34 (PRESS 0.28 + 0.06)
    /// under PRESS's tick; a commit 0.28-0.34 under HARD HANDS' commit (0.34).
    /// </summary>
    public static readonly IReadOnlyList<ArchetypeCue> All = new ArchetypeCue[]
    {
        new(FistHit, ArchetypeMoment.Hit, "a padded fist", 0.34f, 0.38f, "sfx_seeker_hard_hands_hit", 0.55f,
            "the anvil, metronome and oathbound swings (HARD HANDS resolves its own hit first)"),
        new(BladeHit, ArchetypeMoment.Hit, "a short cut into leather", 0.34f, 0.38f, "sfx_seeker_spray_hit", 0.50f,
            "the seeker, magpie and quiver swings (SPRAY resolves its own hit first)"),
        new(StoneHit, ArchetypeMoment.Hit, "a stone on packed earth", 0.34f, 0.38f, "sfx_seeker_spray_hit", 0.50f,
            "the tower and unbroken swings"),
        new(WoodHit, ArchetypeMoment.Hit, "a wooden thump with a rim", 0.34f, 0.38f, "sfx_seeker_spray_hit", 0.50f,
            "the thornwall and chorus swings"),
        new(ThrowRelease, ArchetypeMoment.Release, "a cloth whip + air", 0.16f, 0.18f, "sfx_seeker_spray_release", 0.40f,
            "the quiver, chorus and unbroken releases (SPRAY resolves its own release first)"),
        new(AirRelease, ArchetypeMoment.Release, "an air push, no tone", 0.22f, 0.26f, "sfx_seeker_spray_release", 0.40f,
            "PULSE's release, REPAY's release, DRINK's intake (Phases 2-3)"),
        new(FieldTick, ArchetypeMoment.Tick, "a quiet material settle", 0.14f, 0.34f, "sfx_seeker_press_tick", 0.28f,
            "MIRE's and WILT's deepen (Phase 2), HOLD FAST's set until its own"),
        new(ClothCommit, ArchetypeMoment.Commit, "a boot plant + cloth", 0.28f, 0.34f, "sfx_seeker_hard_hands_commit", 0.34f,
            "every commit until its own (Phase 3)"),
    };

    /// <summary>The archetype named <paramref name="key"/>, or null.</summary>
    public static ArchetypeCue? Find(string key)
    {
        foreach (var cue in All)
            if (string.Equals(cue.Key, key, StringComparison.Ordinal)) return cue;
        return null;
    }

    /// <summary>
    /// The per-cue throttle rows this table adds to <see cref="SoundBank"/>'s (design.md section 7: hit families 60 ms,
    /// ticks 90 ms): every archetype hit and tick, every basic attack's own contact cue (its identity cue is a hit family
    /// too), and every basic attack's own release cue (<c>sfx_quiver_loose</c>, <c>sfx_chorus_toss</c>,
    /// <c>sfx_unbroken_toss</c>: one per swing, on the swing's beat, so at the swing's 60). The archetype releases and the
    /// commit keep the bank's default.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, long>> ThrottleRows()
    {
        foreach (var cue in All)
            if (cue.Moment == ArchetypeMoment.Hit) yield return new(cue.Key, HitGapMs);
            else if (cue.Moment == ArchetypeMoment.Tick) yield return new(cue.Key, TickGapMs);
        foreach (var recipe in SwingRecipes.All.Values)
        {
            if (recipe.ContactCues.Count > 0 && Find(recipe.ContactCues[0]) is null && recipe.ContactCues[0] != "sfx_hit")
                yield return new(recipe.ContactCues[0], HitGapMs);
            if (recipe.ReleaseCues.Count > 0 && Find(recipe.ReleaseCues[0]) is null)
                yield return new(recipe.ReleaseCues[0], HitGapMs);
        }
    }
}
