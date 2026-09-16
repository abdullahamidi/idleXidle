using System;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Quests;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Traits;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// The WORDS of a dispatch, rendered at display time from the catalogues. Never persisted: a row
/// stores the event and its subject, and the catalogue that owns the subject says what it is called
/// — so a renamed trait reads by its new name, and a retired one reads plainly instead of as an id.
/// </summary>
/// <remarks>
/// <para>
/// A headline is uppercase, like every notice in the game. A body is MEANT to be a sentence the
/// surface may wrap, and for eight of the eleven kinds it is: Unlock, Gem, Trait, Champion, Quest,
/// Region, Socket and Migration all read as prose.
/// </para>
/// <para>
/// <b>Three kinds still shout, and this says so rather than pretending otherwise.</b>
/// <see cref="DispatchKind.Keystone"/> renders <c>Keystone.Blurb</c> ("DOUBLE DAMAGE. HALF HEALTH.")
/// behind a rung prefix this file authors in the same register ("VERDANT HOLLOW IS PARTLY
/// MASTERED."); <see cref="DispatchKind.Vow"/> renders <c>Vow.ProofLine</c>;
/// <see cref="DispatchKind.Set"/> renders <c>CapstoneName + " ACTIVE"</c>. All three are catalogue
/// strings authored ALL-CAPS for the retired toast, and BUILD and TRAITS still print the same
/// strings — so they cannot simply be lower-cased here without changing those screens too. The
/// reading pane therefore holds letters that speak beside letters that shout, in one ink and one
/// size. Fixing it needs a per-kind sentence-case rendering and is a pass of its own; until then this
/// remark is the record, so nobody reads the rule above as a description of the output.
/// </para>
/// <para>
/// Nothing here promises a price the screen it points at could refuse — the first gem's free socket
/// is a live rule the Forge states itself.
/// </para>
/// </remarks>
public static class DispatchCopy
{
    /// <summary>The one line that says what happened. Never empty, never a raw id.</summary>
    public static string Headline(Dispatch d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return d.Kind switch
        {
            DispatchKind.Unlock => ScreenOf(d) is { } screen ? $"NEW — {Unlocks.Headline(screen)}" : "A NEW SCREEN OPENED",
            DispatchKind.Gem => "A GEM DROPPED",
            DispatchKind.Trait => TraitCatalogue.Find(d.SubjectId) is { } t ? $"A TRAIT HAS AWAKENED — {t.Name}" : "A TRAIT HAS AWAKENED",
            DispatchKind.Vow => VowHeadline(d),
            DispatchKind.Keystone => Keystones.ById(d.SubjectId) is { } k ? $"NEW KEYSTONE — {k.Name}" : "A NEW KEYSTONE",
            DispatchKind.Champion => CharacterRoster.Find(d.SubjectId ?? "") is { } c ? $"{c.Name} JOINS YOU" : "A HUNTER JOINS YOU",
            DispatchKind.Quest => QuestCatalogue.Find(d.SubjectId) is { } q ? $"QUEST COMPLETE — {q.Name}" : "QUEST COMPLETE",
            DispatchKind.Set => SourceOf(d) is { } s ? $"{ElementSets.Name(s)} COMPLETE" : "A SET IS COMPLETE",
            DispatchKind.Region => Regions.Find(d.RegionId ?? "") is { } r ? $"{r.Name} CONQUERED" : "A REGION IS CONQUERED",
            DispatchKind.Socket => "A KEYSTONE SOCKET OPENS",
            DispatchKind.Migration => d.SubjectId switch
            {
                DispatchKeys.MigrationKeystones => d.Count is int n && n > 1
                    ? $"THE WORLD HAS TAUGHT YOU {n} KEYSTONES"
                    : "THE WORLD HAS TAUGHT YOU A KEYSTONE",
                DispatchKeys.MigrationFifthSlot => "THE FIFTH SKILL SLOT IS GONE",
                _ => "THE GAME HAS CHANGED",
            },
            _ => "NEWS",
        };
    }

    /// <summary>What to do about it, or what it does — one sentence the surface may wrap. Empty only when there is nothing to add.</summary>
    public static string Body(Dispatch d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return d.Kind switch
        {
            DispatchKind.Unlock => ScreenOf(d) is { } screen ? Unlocks.OpenedLine(screen) : "It is on the rail.",
            DispatchKind.Gem => "The Forge's SOCKET tab sets it into an item.",
            DispatchKind.Trait => TraitCatalogue.Find(d.SubjectId) is { } t ? t.Flavour : "Read what it does on the TRAITS screen.",
            DispatchKind.Vow => Vows.ById(d.SubjectId) is { } v && v.ProofLine.Length > 0 ? v.ProofLine : "It is on the BUILD screen.",
            DispatchKind.Keystone => Keystones.ById(d.SubjectId) is { } k ? KeystoneRevealDetail(k) : "It is on the BUILD screen, ready to wear.",
            DispatchKind.Champion => "Switch hunter on the ROSTER screen.",
            DispatchKind.Quest => QuestCatalogue.Find(d.SubjectId) is { } q ? q.Demand : "Its reward is yours.",
            DispatchKind.Set => SourceOf(d) is { } s ? $"{ElementSets.CapstoneName(s)} ACTIVE" : "Five pieces worn; its bonus is active.",
            DispatchKind.Region => "The next region is open. Travel there on the MAP when you are ready.",
            DispatchKind.Socket => "Go to the BUILD screen to wear your new keystone.",
            DispatchKind.Migration => d.SubjectId switch
            {
                DispatchKeys.MigrationKeystones => (d.Count is int n && n > 1 ? "They are" : "It is")
                                                   + " on the BUILD screen, ready to wear. You find more by conquering and mastering regions.",
                DispatchKeys.MigrationFifthSlot => "It was taken out of your build. It keeps its level — put it back any time in place of another skill.",
                _ => "",
            },
            _ => "",
        };
    }

    /// <summary>
    /// How a Vow arrived, which is not one sentence but two.
    /// </summary>
    /// <remarks>
    /// A GRANTED Vow is HANDED to the player — it comes with the BUILD screen so the system is
    /// discoverable at all — and "A VOW HAS REVEALED ITSELF" would be a lie about what they did: they
    /// did nothing, the game gave it to them. Every other Vow is PROVED, by having already kept its
    /// rule without it, and that one really did reveal itself.
    /// <para>
    /// One KEY for both (<c>vow.&lt;id&gt;</c>), because a Vow is discovered once however it arrives and
    /// the inbox must refuse the second telling — the sentence branches, the dedupe does not.
    /// </para>
    /// </remarks>
    private static string VowHeadline(Dispatch d)
    {
        if (Vows.ById(d.SubjectId) is not { } v) return "A VOW HAS REVEALED ITSELF";
        return v.Proof == VowProof.Granted
            ? $"A VOW IS OFFERED TO YOU — {v.Name}"
            : $"A VOW HAS REVEALED ITSELF — {v.Name}";
    }

    /// <summary>
    /// The reveal a found keystone gets: where it came from, then the FIRST sentence of what it does,
    /// and a pointer to the BUILD screen when there is more.
    /// </summary>
    /// <remarks>
    /// ONE SENTENCE-MAKER FOR ALL FOUR PRODUCERS — a conquest, the two mastery rungs and the
    /// corruption — so a keystone arrives with the same shape however it was earned. It has to say
    /// what the keystone DOES: the word CAPACITOR alone teaches nobody anything, and the longest of
    /// these sentences is 164 characters, which is why the reading pane wraps rather than truncates.
    /// Pure, so the pane that shows it and the test that measures it read the same string.
    /// </remarks>
    public static string KeystoneRevealDetail(Keystone k)
    {
        ArgumentNullException.ThrowIfNull(k);
        var where = Keystones.SourceOf(k.Id) is { } src ? RungReached(src) : "";
        var blurb = k.Blurb.Trim();
        var cut = blurb.IndexOf(". ", StringComparison.Ordinal);
        var first = cut > 0 ? blurb[..(cut + 1)] : blurb;
        var detail = where.Length > 0 ? $"{where} {first}" : first;
        return first.Length < blurb.Length ? $"{detail} THE BUILD SCREEN SAYS THE REST." : detail;
    }

    /// <summary>"VERDANT HOLLOW IS PARTLY MASTERED." — the sentence a mastery-rung reveal opens with.</summary>
    private static string RungReached(KeystoneSource src)
    {
        if (src.Rung == WorldRung.Corruption) return "THE CORRUPTION HAS DEEPENED.";
        if (src.RegionId is not { } id || Regions.Find(id) is not { } def) return "";
        return $"{def.Name} IS {Keystones.RungName(src.Rung)}.";
    }

    // The subject of an Unlock row is an Activity NAME, read forward across renames the way the
    // explained list is — so a row written when TRAINING was still STATS renders as TRAINING.
    private static Activity? ScreenOf(Dispatch d)
        => d.SubjectId is { Length: > 0 } name
           && Enum.TryParse<Activity>(Onboarding.ModernScreenKey(name), ignoreCase: true, out var screen)
           && Enum.IsDefined(screen)
            ? screen
            : null;

    private static Source? SourceOf(Dispatch d)
        => d.SubjectId is { Length: > 0 } name
           && Enum.TryParse<Source>(name, ignoreCase: true, out var source)
           && Enum.IsDefined(source)
            ? source
            : null;
}
