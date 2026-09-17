using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Core.Progression;

/// <summary>What kind of news a dispatch carries — which catalogue its subject is read from at display time.</summary>
/// <remarks>
/// Persisted by NAME (<c>SavedDispatch.Kind</c>), so a member inserted here can never turn a saved
/// letter into a different kind of letter; a name this build does not know is dropped on read.
/// </remarks>
public enum DispatchKind
{
    /// <summary>A screen opened on the rail. Subject: the <see cref="Activity"/> NAME.</summary>
    Unlock,

    /// <summary>The first gem. No subject.</summary>
    Gem,

    /// <summary>A characteristic awoke. Subject: a <c>TraitCatalogue</c> id.</summary>
    Trait,

    /// <summary>A Vow revealed itself. Subject: a <c>Vows</c> id.</summary>
    Vow,

    /// <summary>The world taught a keystone. Subject: a <c>Keystones</c> id.</summary>
    Keystone,

    /// <summary>A hunter joined. Subject: a <c>CharacterRoster</c> id.</summary>
    Champion,

    /// <summary>A quest was completed. Subject: a <c>QuestCatalogue</c> id.</summary>
    Quest,

    /// <summary>A five-piece set was completed. Subject: a <see cref="Source"/> NAME.</summary>
    Set,

    /// <summary>A region was conquered. RegionId: a <c>Regions</c> id.</summary>
    Region,

    /// <summary>A keystone socket opened. No subject.</summary>
    Socket,

    /// <summary>
    /// The game changed under a returning player. Subject: which change
    /// (<see cref="DispatchKeys.MigrationKeystones"/>, <see cref="DispatchKeys.MigrationFifthSlot"/>);
    /// Count: how many things it touched.
    /// </summary>
    Migration,
}

/// <summary>
/// One letter in the inbox: a semantic event with a STABLE KEY, when it was posted, whether it has
/// been read, and the flat columns that name its subject.
/// </summary>
/// <remarks>
/// <para>
/// The Hunter is away on expeditions; important news reaches the player like letters waiting to be
/// read. A dispatch answers "what important thing happened in my world" — the Expedition Log answers
/// "why did this run stop". It is rare, persistent and deduplicated: <see cref="Key"/> names the
/// EVENT, so two producers describing one event collapse to one message and a reload never replays
/// a discovery already told.
/// </para>
/// <para>
/// No copy. The words are rendered at display time by <see cref="DispatchCopy"/> from the catalogue
/// that owns the subject, so a renamed trait reads by its new name and a retired one reads plainly.
/// <see cref="Read"/> is presentation state and nothing else: acknowledging a letter fabricates no
/// fact, and no gameplay rule reads it.
/// </para>
/// </remarks>
/// <param name="Key">The event's stable key — see <see cref="DispatchKeys"/>.</param>
/// <param name="Kind">Which catalogue the subject lives in.</param>
/// <param name="AtMs">UTC epoch milliseconds when it was posted. Never fabricated: a seeded key has no row.</param>
/// <param name="Read">Has the player opened it? Presentation only.</param>
/// <param name="SubjectId">The catalogue id (or name) the kind says it is, or null for a kind with no subject.</param>
/// <param name="RegionId">The region the event happened in, where the kind has one.</param>
/// <param name="Count">How many of something, where the kind counts.</param>
public sealed record Dispatch(
    string Key,
    DispatchKind Kind,
    long AtMs,
    bool Read,
    string? SubjectId = null,
    string? RegionId = null,
    int? Count = null);

/// <summary>
/// THE STABLE KEYS. A key names the event, not the words: two producers describing one event build
/// the same key, and the inbox refuses the second. Ids are the catalogues' own; an activity or a
/// source travels lowercase. A saved key is read forward across renames by
/// <see cref="Dispatches.ModernDispatchKey"/>.
/// </summary>
public static class DispatchKeys
{
    /// <summary><c>unlock.training</c> — a screen opened on the rail.</summary>
    public static string Unlock(Activity screen) => $"unlock.{screen.ToString().ToLowerInvariant()}";

    /// <summary><c>gem.first</c> — the first gem ever held.</summary>
    public const string GemFirst = "gem.first";

    /// <summary><c>socket.first</c> — the first keystone socket, which the first conquest opens.</summary>
    public const string SocketFirst = "socket.first";

    /// <summary><c>trait.&lt;id&gt;</c></summary>
    public static string Trait(string traitId) => $"trait.{traitId}";

    /// <summary><c>vow.&lt;id&gt;</c></summary>
    public static string Vow(string vowId) => $"vow.{vowId}";

    /// <summary><c>keystone.&lt;id&gt;</c></summary>
    public static string Keystone(string keystoneId) => $"keystone.{keystoneId}";

    /// <summary><c>hunter.&lt;id&gt;.joined</c></summary>
    public static string HunterJoined(string characterId) => $"hunter.{characterId}.joined";

    /// <summary><c>quest.&lt;id&gt;.complete</c></summary>
    public static string QuestComplete(string questId) => $"quest.{questId}.complete";

    /// <summary><c>set.&lt;source&gt;.complete</c> — a five-piece set, by its Source.</summary>
    public static string SetComplete(Source source) => SetComplete(source.ToString());

    /// <summary><c>set.&lt;source&gt;.complete</c>, from the Source NAME the save stores (<c>CompletedSets</c>).</summary>
    public static string SetComplete(string sourceName) => $"set.{sourceName.ToLowerInvariant()}.complete";

    /// <summary><c>region.&lt;id&gt;.conquered</c></summary>
    public static string RegionConquered(string regionId) => $"region.{regionId}.conquered";

    /// <summary><c>migration.&lt;what&gt;</c> — a change the game made under a returning player's feet.</summary>
    public static string Migration(string what) => $"migration.{what}";

    /// <summary>The migration that moved keystones off the trait tree and onto the world.</summary>
    public const string MigrationKeystones = "keystones";

    /// <summary>The migration that removed the fifth skill slot.</summary>
    public const string MigrationFifthSlot = "fifth_slot";
}

/// <summary>
/// The account's inbox: the rows it holds, and every key it KNOWS. The inbox IS the dedupe — a key
/// posted once is refused for ever after, whether its row is still here or not.
/// </summary>
/// <remarks>
/// <para>
/// Two halves with different lifetimes. <see cref="Known"/> is monotone and small — one string per
/// event the account has ever been told or seeded as having lived. <see cref="Rows"/> are the
/// visible letters, capped at <see cref="Cap"/>: the oldest READ rows are pruned first and an unread
/// row is never pruned, so the cap yields rather than throw away a letter nobody has opened. A
/// pruned row's key stays known, which is what stops old news from arriving twice.
/// </para>
/// <para>
/// A plain model with no device — safe to restore from <c>Initialize</c>, like the trait ledger — and
/// keyed on modern keys only: every way in reads a key forward across renames.
/// </para>
/// </remarks>
public sealed class Inbox
{
    /// <summary>
    /// How many rows the inbox keeps. Sixty is a season of news — eleven screens, six regions, the
    /// traits and keystones of a long career — and a bound the byte-stable save test can live with.
    /// </summary>
    public const int Cap = 60;

    private readonly List<Dispatch> _ledger = new();
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private List<Dispatch>? _newestFirst;

    /// <summary>The rows in CREATED order — what the save writes. The order is meaning; it is never re-sorted.</summary>
    public IReadOnlyList<Dispatch> Ledger => _ledger;

    /// <summary>The rows newest first — what a list surface shows.</summary>
    public IReadOnlyList<Dispatch> Rows => _newestFirst ??= Enumerable.Reverse(_ledger).ToList();

    /// <summary>Every key the account knows: posted once, or seeded as already lived. A superset of the rows' keys.</summary>
    public IReadOnlyCollection<string> Known => _known;

    /// <summary>How many rows are held.</summary>
    public int Count => _ledger.Count;

    /// <summary>How many rows have not been read — the number on the envelope.</summary>
    public int Unread
    {
        get
        {
            var n = 0;
            foreach (var row in _ledger) if (!row.Read) n++;
            return n;
        }
    }

    /// <summary>Has this key been told or seeded? Read forward across renames.</summary>
    public bool IsKnown(string key) => _known.Contains(Dispatches.ModernDispatchKey(key));

    /// <summary>
    /// Post a dispatch. True if it was news and a row was added; false — and nothing changed — if
    /// the account already knew the key, whatever the payload said.
    /// </summary>
    public bool Post(Dispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (string.IsNullOrWhiteSpace(dispatch.Key)) throw new ArgumentException("a dispatch needs a key", nameof(dispatch));
        var key = Dispatches.ModernDispatchKey(dispatch.Key);
        if (!_known.Add(key)) return false;
        _ledger.Add(key == dispatch.Key ? dispatch : dispatch with { Key = key });
        _newestFirst = null;
        Prune();
        return true;
    }

    /// <summary>
    /// Mark a key as KNOWN without a row: no letter, no timestamp, nothing to read — only that a
    /// later post of it is not news. What the migration seed writes. True if it was not known before.
    /// </summary>
    public bool Know(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return _known.Add(Dispatches.ModernDispatchKey(key));
    }

    /// <summary><see cref="Know(string)"/> for many keys; how many were new.</summary>
    public int Know(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var added = 0;
        foreach (var key in keys) if (Know(key)) added++;
        return added;
    }

    /// <summary>Mark one row read. True if it was unread; false if already read or not held. Nothing else changes.</summary>
    public bool MarkRead(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        key = Dispatches.ModernDispatchKey(key);
        for (var i = 0; i < _ledger.Count; i++)
        {
            if (_ledger[i].Key != key) continue;
            if (_ledger[i].Read) return false;
            _ledger[i] = _ledger[i] with { Read = true };
            _newestFirst = null;
            return true;
        }
        return false;
    }

    /// <summary>Mark every row read; how many changed. Order, keys and payloads are untouched.</summary>
    public int MarkAllRead()
    {
        var changed = 0;
        for (var i = 0; i < _ledger.Count; i++)
        {
            if (_ledger[i].Read) continue;
            _ledger[i] = _ledger[i] with { Read = true };
            changed++;
        }
        if (changed > 0) _newestFirst = null;
        return changed;
    }

    /// <summary>Forget everything — rows and known keys. A new career has been told nothing.</summary>
    public void Clear()
    {
        _ledger.Clear();
        _known.Clear();
        _newestFirst = null;
    }

    /// <summary>
    /// Replace the inbox with what a save carries. Rows first — one per key, the first in file order
    /// wins — then the known keys, so a row's own key never refuses the row that carries it.
    /// </summary>
    public void Restore(IEnumerable<Dispatch>? rows, IEnumerable<string>? known)
    {
        Clear();
        if (rows is not null) foreach (var row in rows) Post(row);
        if (known is not null) Know(known);
    }

    /// <summary>The rows for the save, in created order, columns flat.</summary>
    public List<SavedDispatch> ToSave()
        => _ledger.Select(r => new SavedDispatch
        {
            Key = r.Key,
            Kind = r.Kind.ToString(),
            AtMs = r.AtMs,
            Read = r.Read,
            SubjectId = r.SubjectId,
            RegionId = r.RegionId,
            Count = r.Count,
        }).ToList();

    /// <summary>The known keys for the save, in a stable order.</summary>
    public List<string> KnownToSave() => _known.OrderBy(k => k, StringComparer.Ordinal).ToList();

    // THE OLDEST READ ROW GOES FIRST, and an unread row never goes: a letter nobody has opened is not
    // the inbox's to throw away, so with everything unread the cap yields. Known is untouched — a
    // pruned row's key stays known for ever, which is what stops old news re-posting.
    private void Prune()
    {
        while (_ledger.Count > Cap)
        {
            var oldestRead = _ledger.FindIndex(r => r.Read);
            if (oldestRead < 0) return;
            _ledger.RemoveAt(oldestRead);
        }
    }
}

/// <summary>
/// The inbox's rules that are not the inbox's own: how a key is read forward, how a letter is made,
/// and how a save from before the inbox is told what it already knows.
/// </summary>
public static class Dispatches
{
    // ── MIGRATION ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// THE FIRST SAVE VERSION WHOSE FILES CARRY AN INBOX. Frozen forever.
    /// </summary>
    /// <remarks>
    /// A literal, never <c>SaveGame.CurrentVersion</c> — the fourth constant in this family to carry
    /// that warning, and for the same reason as <c>OnboardingLessons.FirstVersionWithFallLoopFacts</c>:
    /// written against CurrentVersion, the next bump would start seeding every version-8 file — files
    /// whose inbox is honest — and every unread letter a player had not yet opened would be marked
    /// as already known. The number names a moment in this project's history, and that moment does
    /// not move when the format does.
    /// </remarks>
    public const int FirstVersionWithInbox = 8;

    /// <summary>
    /// A saved key, read forward across renames. An <c>unlock.</c> key is built from an
    /// <see cref="Activity"/> name, and the explained list already reads those forward
    /// (<see cref="Onboarding.ModernScreenKey"/>: "Stats" became Training) — so the key goes through
    /// the same table, and the two can never disagree about what a screen is now called.
    /// </summary>
    public static string ModernDispatchKey(string saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        const string prefix = "unlock.";
        if (!saved.StartsWith(prefix, StringComparison.Ordinal) || saved.Length == prefix.Length) return saved;
        var name = saved[prefix.Length..];
        var modern = Onboarding.ModernScreenKey(char.ToUpperInvariant(name[0]) + name[1..]);
        return prefix + modern.ToLowerInvariant();
    }

    /// <summary>
    /// Every key a file from before the inbox has already lived, read from the same facts the game
    /// restores: the screens it has open, its conquests, discovered traits, keystones (the persisted
    /// latch and the legacy tree's grant), Vows (and the legacy tree's), quests done, the hunters it
    /// banked (or the legacy roster when it banked none), the sets it completed, and whether it has
    /// ever held a gem or opened a socket.
    /// </summary>
    /// <param name="save">The file as loaded.</param>
    /// <param name="activities">The rail's revealed set at load — what the save remembered plus what its gates open.</param>
    public static IReadOnlyList<string> KnownFrom(SaveGame save, IEnumerable<Activity> activities)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(activities);
        var keys = new List<string>();

        // The Hunt is the game, never news.
        foreach (var screen in activities)
            if (screen != Activity.Hunt) keys.Add(DispatchKeys.Unlock(screen));

        foreach (var id in save.ConqueredRegions) keys.Add(DispatchKeys.RegionConquered(id));
        foreach (var id in save.DiscoveredTraits) keys.Add(DispatchKeys.Trait(id));

        // What the old trait tree had bought is what the loader unions in; the seed reads the same table.
        var legacy = LegacyTraitTree.Read(save.MemoryDustUnlocks);
        foreach (var id in save.DiscoveredKeystoneIds.Concat(legacy.Keystones)) keys.Add(DispatchKeys.Keystone(id));
        foreach (var id in save.DiscoveredVowIds.Concat(legacy.Vows)) keys.Add(DispatchKeys.Vow(id));

        foreach (var id in save.QuestsDone) keys.Add(DispatchKeys.QuestComplete(id));

        // The banked roster, or — for a file from before it was banked — the rule RestoreCharacters
        // seeds it by, so no hunter the loader hands back arrives as a stranger.
        var hunters = save.UnlockedCharacters.Count > 0
            ? save.UnlockedCharacters
            : LegacyUnlocks.Seed(
                save.ConqueredRegions,
                save.QuestsDone,
                save.RegionFarms.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.Max(f => f.BestDepth)),
                save.RunsWithVowKept);
        foreach (var id in hunters) keys.Add(DispatchKeys.HunterJoined(id));

        foreach (var name in save.CompletedSets) keys.Add(DispatchKeys.SetComplete(name));

        if (HasHeldAGem(save)) keys.Add(DispatchKeys.GemFirst);
        if (HasASocket(save, legacy)) keys.Add(DispatchKeys.SocketFirst);

        // THE TWO ONE-SHOTS, UNCONDITIONALLY. Both are announcements the game owes a file whose
        // keystones moved off the trait tree and onto the world, and both fire on the FIRST pass of
        // any such file — which is every file older than the inbox. Nothing in a save records having
        // been told, so no fact can answer this one: the version is the answer, and a returning
        // player must not be handed mail about a change they have already been living with.
        keys.Add(DispatchKeys.Migration(DispatchKeys.MigrationKeystones));
        keys.Add(DispatchKeys.Migration(DispatchKeys.MigrationFifthSlot));
        return keys;
    }

    /// <summary>
    /// Tell a save from before the inbox what it already knows, so nothing it has lived arrives as
    /// news. True if the file was seeded; false — and nothing changed — for a file that carries an
    /// inbox and is believed as written.
    /// </summary>
    /// <param name="save">The file as LOADED — its own Version, not the build's.</param>
    /// <param name="activities">The rail's revealed set at load.</param>
    /// <param name="inbox">The inbox to seed. Gains keys only: no rows, no timestamps, nothing unread.</param>
    /// <remarks>
    /// The sibling of <c>OnboardingLessons.SeedFallLoopAsLived</c> and <c>OpeningScript.SeedOpeningAsLived</c>,
    /// and it carries the same law: <b>the version is the test</b>. A file written before the inbox
    /// existed cannot carry one, and a veteran with three regions conquered must not open on twenty
    /// unread letters about a life already led. A file at or past <see cref="FirstVersionWithInbox"/>
    /// is believed exactly as written — including a brand-new career that conquered a region and
    /// reloaded before reading the letter about it, which is the case a progression guess gets wrong,
    /// and including an honestly empty inbox.
    /// </remarks>
    public static bool SeedKnown(SaveGame save, IEnumerable<Activity> activities, Inbox inbox)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(inbox);
        if (save.Version >= FirstVersionWithInbox) return false;
        inbox.Know(KnownFrom(save, activities));
        return true;
    }

    private static bool HasHeldAGem(SaveGame save)
        => save.FreeSocketUsed
           || save.ExplainedScreens.Contains(Onboarding.GemTourKey)
           || save.Inventory.Any(i => i.Gems.Count > 0
                                      || string.Equals(i.BaseType, nameof(ItemBaseType.Gem), StringComparison.Ordinal));

    private static bool HasASocket(SaveGame save, LegacyTraitGrants legacy)
        => save.KeystoneSocketsEarned >= 1
           || legacy.KeystoneSockets >= 1
           || save.SocketedKeystoneIds.Count > 0
           || save.ConqueredRegions.Count > 0;   // the first conquest opens the first socket

    // ── LETTERS ──────────────────────────────────────────────────────────────────────────────────
    //
    // One factory per kind, so a producer cannot pair a key with the wrong kind or leave the subject
    // the copy needs out of the row. Every letter is posted UNREAD.

    /// <summary>A screen opened on the rail.</summary>
    public static Dispatch Unlock(Activity screen, long atMs)
        => new(DispatchKeys.Unlock(screen), DispatchKind.Unlock, atMs, Read: false, SubjectId: screen.ToString());

    /// <summary>The first gem.</summary>
    public static Dispatch Gem(long atMs) => new(DispatchKeys.GemFirst, DispatchKind.Gem, atMs, Read: false);

    /// <summary>A characteristic awoke.</summary>
    public static Dispatch Trait(string traitId, long atMs)
        => new(DispatchKeys.Trait(traitId), DispatchKind.Trait, atMs, Read: false, SubjectId: traitId);

    /// <summary>A Vow revealed itself.</summary>
    public static Dispatch Vow(string vowId, long atMs)
        => new(DispatchKeys.Vow(vowId), DispatchKind.Vow, atMs, Read: false, SubjectId: vowId);

    /// <summary>The world taught a keystone.</summary>
    public static Dispatch Keystone(string keystoneId, long atMs)
        => new(DispatchKeys.Keystone(keystoneId), DispatchKind.Keystone, atMs, Read: false, SubjectId: keystoneId);

    /// <summary>A hunter joined.</summary>
    public static Dispatch Champion(string characterId, long atMs)
        => new(DispatchKeys.HunterJoined(characterId), DispatchKind.Champion, atMs, Read: false, SubjectId: characterId);

    /// <summary>A quest was completed.</summary>
    public static Dispatch Quest(string questId, long atMs)
        => new(DispatchKeys.QuestComplete(questId), DispatchKind.Quest, atMs, Read: false, SubjectId: questId);

    /// <summary>A five-piece set was completed.</summary>
    public static Dispatch Set(Source source, long atMs)
        => new(DispatchKeys.SetComplete(source), DispatchKind.Set, atMs, Read: false, SubjectId: source.ToString());

    /// <summary>A region was conquered.</summary>
    public static Dispatch Region(string regionId, long atMs)
        => new(DispatchKeys.RegionConquered(regionId), DispatchKind.Region, atMs, Read: false, RegionId: regionId);

    /// <summary>A keystone socket opened.</summary>
    public static Dispatch Socket(long atMs) => new(DispatchKeys.SocketFirst, DispatchKind.Socket, atMs, Read: false);

    /// <summary>The game changed under a returning player: which change, and how many things it touched.</summary>
    public static Dispatch Migration(string what, long atMs, int? count = null)
        => new(DispatchKeys.Migration(what), DispatchKind.Migration, atMs, Read: false, SubjectId: what, Count: count);
}
