using System;
using System.Collections.Generic;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>
/// The dedicated illustration behind each prologue beat, looked up by the beat's id.
/// </summary>
/// <remarks>
/// <para>
/// Art KEYS live here in the host, never in the script: <see cref="OpeningScript"/> is copy, and copy
/// that named a PNG would tie the writing to the pipeline. Every beat has a plate of its own under
/// <c>assets/art/Environments/prologue</c> — <c>plate_prologue_&lt;id&gt;</c> — generated on
/// 2026-09-16 as one family through the house plate recipe (pixen 640×360, ×3 nearest; the prompts
/// and job ids are in <c>tools/asset-pipeline/v2/spec.json</c> under <c>prologue</c>). Each is composed
/// with its subject in the upper half and a quiet lower third, because the title and the lines sit
/// at <c>page.Height / 2 + 40</c> over the picture, and each carries no text of its own.
/// </para>
/// <para>
/// <b>There is no fallback to an environment plate.</b> The prologue used to borrow the constellation,
/// the region map and three arenas; a beat that reached for one of those again would be a story
/// illustrated by the wrong picture, so an id with no plate throws here and
/// <c>prologue_art_test</c> proves the throw is unreachable for the script as written.
/// </para>
/// </remarks>
public static class PrologueArt
{
    /// <summary>Every plate key starts with this, which is how the asset gates find the family.</summary>
    public const string Prefix = "plate_prologue_";

    // One literal per beat, on purpose: tools/check_asset_keys.py reads this file for the family, and
    // tools/check_asset_consumers.py reaches the PNGs through these strings.
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pressures"] = "plate_prologue_pressures",   // six glows rising through a cracked plain
        ["joints"] = "plate_prologue_joints",         // two ringed rune towers driven into a mountainside
        ["tear"] = "plate_prologue_tear",             // one of them split, violet bleeding up the crack
        ["hunter"] = "plate_prologue_hunter",         // a hooded figure alone at the lip of the chasm, under the moon
        ["stands"] = "plate_prologue_stands",         // deep underground, the tower's runes turning warm
        ["hollow"] = "plate_prologue_hollow",         // the mossy hollow, a lantern-lit camp dug into the roots
    };

    /// <summary>The plate key for a beat. Throws for a beat the catalogue does not know — never a stand-in.</summary>
    public static string For(OpeningScript.PrologueBeat beat)
        => Keys.TryGetValue(beat.Id, out var key)
            ? key
            : throw new InvalidOperationException(
                $"Prologue beat '{beat.Id}' has no plate. Add it to PrologueArt and file {Prefix}{beat.Id}.png.");

    /// <summary>Every plate key the catalogue carries, for the tests and the asset gates.</summary>
    public static IReadOnlyCollection<string> All => (IReadOnlyCollection<string>)Keys.Values;
}
