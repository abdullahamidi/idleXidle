using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ResonanceHunter.Core.Builds;

namespace ResonanceHunter.Core.Prestige;

/// <summary>
/// The plain-English text of a trait — what the TRAITS screen prints when a node is read.
/// </summary>
/// <remarks>
/// <para>
/// Written for the playtest note of 2026-08-23 ("traitleri ... daha açıklayıcı ... bir hale getirelim":
/// make the traits more explanatory). Before this, the screen printed the node's hand-written
/// Description and nothing else, and twenty of thirty-nine of those said "LEARN &lt;KEYSTONE&gt;." —
/// the keystone's own Blurb, the one sentence that says what it actually does, was never read by the
/// one screen where the player decides whether to buy it.
/// </para>
/// <para>
/// So a trait's text is COMPOSED here from three owners, each stated once:
/// the node's Description (flavour and placement, hand-written), the keystone's Blurb (what the
/// keystone does, owned by <see cref="Keystones"/>), and a sentence GENERATED from the node's
/// <see cref="BuildMods"/> (the real numbers, owned by the Mods and nowhere else — a catalogue that
/// hand-typed "6% harder" beside a 1.06f would drift the first time somebody retuned one of them).
/// </para>
/// <para>
/// Lives in Core rather than on the screen so it can be tested without a window, and so any other
/// surface that ever quotes a trait (a codex, a share code, a tooltip) prints the same sentence.
/// The reader plays in English as a second language: full sentences, no abbreviations, the number
/// said outright.
/// </para>
/// </remarks>
public static class MemoryDustText
{
    /// <summary>
    /// Everything the player needs to know about a node, as one or more plain sentences.
    /// </summary>
    /// <remarks>
    /// Order: what and where (Description) — what the keystone does, if it teaches one — the reminder
    /// that LEARNING a keystone is not WEARING it — the numbers it moves, if any. Each part is a
    /// complete sentence ending in a full stop, so the screen can wrap the whole thing as one paragraph.
    /// </remarks>
    public static string Describe(MemoryDustUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        var parts = new List<string>();

        var description = Tidy(unlock.Description);
        if (description.Length > 0) parts.Add(description);

        if (unlock.GrantsKeystone is not null)
        {
            parts.Add(KeystoneSentence(unlock.GrantsKeystone));
            parts.Add(KeystoneReminder);
        }

        var mods = ModsSentence(unlock.Mods);
        if (mods.Length > 0) parts.Add(mods);

        return string.Join(" ", parts);
    }

    /// <summary>
    /// The whole sheet for a node, as the detail panel reads it: what it does (with the real numbers),
    /// what it costs, what it needs first, and that it is permanent.
    /// </summary>
    /// <remarks>
    /// Playtest, 2026-08-25: the detail text must state the number, the cost, the prerequisite and
    /// "permanent — never resets". <see cref="Describe"/> carries the first; this adds the other three
    /// from the same catalogue facts, so a screen, a tooltip or a share code can print one paragraph
    /// that a test can hold complete.
    /// </remarks>
    /// <param name="unlock">The node.</param>
    /// <param name="nameOf">Resolves a prerequisite id to its printed name; null falls back to the id.</param>
    public static string Sheet(MemoryDustUnlock unlock, Func<string, string?>? nameOf = null)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        return string.Join(" ", new[]
        {
            Describe(unlock), CostSentence(unlock), RequiresSentence(unlock, nameOf), Permanence,
        }.Where(s => s.Length > 0));
    }

    /// <summary>The one fact every node shares, said once: it never resets.</summary>
    public const string Permanence =
        "Permanent — once learned it never resets, not on death and not on a new region.";

    /// <summary>"Costs 6 trait points." — the whole words, never an abbreviation.</summary>
    public static string CostSentence(MemoryDustUnlock unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        return $"Costs {unlock.Cost} trait {(unlock.Cost == 1 ? "point" : "points")}.";
    }

    /// <summary>
    /// "You need HARDER HITS I first." — every prerequisite by name; or the fact that there is none.
    /// </summary>
    public static string RequiresSentence(MemoryDustUnlock unlock, Func<string, string?>? nameOf = null)
    {
        ArgumentNullException.ThrowIfNull(unlock);
        if (unlock.Requires.Count == 0) return "You need nothing first — you can start here.";

        var names = unlock.Requires.Select(id => nameOf?.Invoke(id) ?? CatalogueName(id) ?? id).ToList();
        var list = names.Count == 1 ? names[0]
                 : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
        return $"You need {list} first.";
    }

    private static string? CatalogueName(string id)
        => MemoryDustTree.Catalog.FirstOrDefault(u => u.Id == id)?.Name;

    /// <summary>
    /// The one line a keystone gate needs: the keystone's name and its own blurb, as a sentence.
    /// </summary>
    /// <example>"Learn the keystone GLASS CANNON: double damage. Half health."</example>
    public static string KeystoneSentence(string keystoneId)
    {
        ArgumentNullException.ThrowIfNull(keystoneId);
        var k = Keystones.ById(keystoneId);
        if (k is null) return $"Learn the keystone {keystoneId.ToUpperInvariant()}.";

        var blurb = SentenceCase(k.Blurb, lowerFirst: true);
        return blurb.Length == 0
            ? $"Learn the keystone {k.Name}."
            : $"Learn the keystone {k.Name}: {blurb}";
    }

    /// <summary>
    /// The distinction the whole tree rests on, said once per keystone node: the tree TEACHES, the
    /// build WEARS. See <see cref="Build.KeystoneSlots"/>.
    /// </summary>
    public const string KeystoneReminder =
        "Learning a keystone does not put it on. You pick which learned keystones to wear on the Build screen, one per socket.";

    /// <summary>
    /// The numbers a node moves, as sentences with the real percentages. Empty for <see cref="BuildMods.None"/>.
    /// </summary>
    /// <remarks>
    /// Generated from the multipliers so the text can never disagree with the sim. Only the fields
    /// that differ from 1 are mentioned, each in the player's words: skills hit harder or softer, the
    /// champion has more or less health, skills come back faster or slower, you bring back more or
    /// less loot, rare finds come more or less often.
    /// </remarks>
    public static string ModsSentence(BuildMods mods)
    {
        var parts = new List<string>();

        Add(parts, mods.Damage, more: "Every skill hits {0}% harder.", less: "Every skill hits {0}% softer.");
        Add(parts, mods.Health, more: "Your champion has {0}% more health.", less: "Your champion has {0}% less health.");
        Add(parts, mods.SkillRate, more: "Your skills come back {0}% faster.", less: "Your skills come back {0}% slower.");
        Add(parts, mods.Haul, more: "You bring back {0}% more loot.", less: "You bring back {0}% less loot.");
        Add(parts, mods.Rarity, more: "Rare finds come {0}% more often.", less: "Rare finds come {0}% less often.");

        return string.Join(" ", parts);
    }

    /// <summary>The percentage a multiplier moves a number by, as the player would say it (1.06 → 6, 0.75 → 25).</summary>
    public static int Percent(float multiplier)
        => (int)MathF.Round(MathF.Abs(multiplier - 1f) * 100f);

    private static void Add(List<string> parts, float multiplier, string more, string less)
    {
        var pct = Percent(multiplier);
        if (pct == 0) return;
        parts.Add(string.Format(CultureInfo.InvariantCulture, multiplier > 1f ? more : less, pct));
    }

    /// <summary>
    /// What kind of thing a node is, in words a player would use — never the enum's name.
    /// </summary>
    /// <remarks>
    /// AMPLIFIER / EXPANSION / CONVENIENCE describe what a node does to the ENGINE. They were printed
    /// on the detail panel as a category line, and they told the reader nothing about the decision
    /// in front of them. These say what the node does for the PLAYER.
    /// </remarks>
    public static string EffectInPlainWords(UnlockEffect effect) => effect switch
    {
        UnlockEffect.Amplifier => "A small permanent boost",
        UnlockEffect.Expansion => "Opens more of the game",
        _ => "Saves you effort",
    };

    /// <summary>
    /// A SHOUTY catalogue string as a sentence: "DOUBLE DAMAGE. HALF HEALTH." → "Double damage. Half health."
    /// </summary>
    /// <remarks>
    /// <para>
    /// The blurbs are authored in capitals — the house style for a card — and at paragraph length
    /// capitals read as shouting and slow an ESL reader down. So: lower-case, then capitalise the
    /// start of every sentence. With <paramref name="lowerFirst"/> the first sentence stays lower-case,
    /// for text that continues a sentence after a colon.
    /// </para>
    /// <para>
    /// Words that are NAMES keep their capitals: a keystone's own name (LODESTONE, when another
    /// keystone's blurb mentions it) and CHARGE, the pool the CHARGE keystones share. Numbers and
    /// symbols pass through untouched.
    /// </para>
    /// </remarks>
    public static string SentenceCase(string shouty, bool lowerFirst = false)
    {
        if (string.IsNullOrWhiteSpace(shouty)) return "";

        var words = shouty.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        var startOfSentence = !lowerFirst;

        foreach (var raw in words)
        {
            if (sb.Length > 0) sb.Append(' ');

            // A name keeps its capitals, but its possessive does not: LODESTONE'S → LODESTONE's.
            var word = KeepsItsCapitals(raw) ? raw.Replace("'S", "'s", StringComparison.Ordinal) : raw.ToLowerInvariant();
            if (startOfSentence) word = CapitaliseFirstLetter(word);
            sb.Append(word);

            startOfSentence = raw.EndsWith('.') || raw.EndsWith('!') || raw.EndsWith('?');
        }

        return sb.ToString();
    }

    /// <summary>Trim a hand-written description and make sure it ends in a full stop, so parts join cleanly.</summary>
    private static string Tidy(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return "";
        return t[^1] is '.' or '!' or '?' ? t : t + ".";
    }

    private static readonly HashSet<string> NamesThatKeepCapitals = Keystones.Catalog
        .Select(k => k.Name)
        .Where(n => !n.Contains(' '))
        .Append("CHARGE")
        .ToHashSet(StringComparer.Ordinal);

    private static bool KeepsItsCapitals(string word)
    {
        // Strip punctuation and a possessive so "LODESTONE'S" and "CHARGE," still match their name.
        var core = word.TrimEnd('.', ',', ':', ';', '!', '?', ')', '(').Trim('(', ')');
        if (core.EndsWith("'S", StringComparison.Ordinal)) core = core[..^2];
        return NamesThatKeepCapitals.Contains(core);
    }

    private static string CapitaliseFirstLetter(string word)
    {
        for (var i = 0; i < word.Length; i++)
            if (char.IsLetter(word[i]))
                return word[..i] + char.ToUpperInvariant(word[i]) + word[(i + 1)..];
        return word;
    }
}
