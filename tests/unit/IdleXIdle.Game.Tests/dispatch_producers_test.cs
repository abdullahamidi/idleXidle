using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BACKGROUND NEWS LEFT THE TOAST CHANNEL. Every durable discovery — a screen opening, a quest, a
/// champion, a keystone, a Vow, a trait, the first gem, the first socket, a set, a conquest, a
/// migration — is a keyed DISPATCH that waits in the inbox. The global toast keeps only what answers
/// something the player just did on the screen they are looking at.
/// </summary>
/// <remarks>
/// <para>
/// The rule this file exists for is ONE SURFACE PER EVENT: never a toast AND a letter for the same
/// thing. A toast crosses whatever the player is reading and is gone in six seconds; a letter is
/// kept, deduped and read when they choose. The failure this guards against is the easy one — a
/// producer migrated to a dispatch with its <c>PostNotice</c> left in place, so the news now arrives
/// twice and interrupts as loudly as before.
/// </para>
/// <para>
/// <see cref="Game1"/> needs MonoGame to instantiate, so the host half is pinned STRUCTURALLY by
/// reading its source — the shape <c>host_input_gates_test</c> and <c>attention_owner_test</c> use.
/// The inbox half is real logic and is driven directly.
/// </para>
/// </remarks>
public class DispatchProducersTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Game1Source() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");

    private static string DispatchesSource() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Dispatches.cs")).Replace("\r\n", "\n");

    /// <summary>
    /// THE ONLY TOASTS LEFT ARE DIRECT FEEDBACK. Each is listed by the copy it opens with, because a
    /// copy string is what a reader can check against the screen: SET ACTIVE says who you are now,
    /// and the rig poses that same plate on two fixtures. Anything else on this channel is news that
    /// has escaped back onto it.
    /// </summary>
    [Fact]
    public void test_the_notice_channel_carries_nothing_but_direct_feedback()
    {
        var game = Game1Source();
        // Call sites only — the declaration's own parameter list is not a producer.
        var heads = Regex.Matches(game, @"PostNotice\(\s*([^,]+),")
                         .Select(m => m.Groups[1].Value.Trim())
                         .Where(a => !a.StartsWith("string ", StringComparison.Ordinal))
                         .ToList();
        Assert.Equal(
            new[]
            {
                "$\"YOU ARE {who.ToUpperInvariant()}\"",   // SET ACTIVE on the ROSTER screen
                "\"YOU ARE THE SEEKER\"",                  // rig: `rosterswitch`
                "\"YOU ARE THE ANVIL\"",                   // rig: RH_SHOT_NOTICE, on any fixture
            }.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            heads.OrderBy(s => s, StringComparer.Ordinal).ToList());
    }

    /// <summary>The awakening plate is gone: a trait's arrival is a letter, and six of them are a list.</summary>
    [Fact]
    public void test_no_awakening_plate_survives_anywhere_in_the_host()
    {
        var game = Game1Source();
        foreach (var gone in new[]
                 {
                     "PostAwakening", "Awakening", "TRAITS HAVE AWAKENED", "A TRAIT HAS AWAKENED",
                     "PostKeystoneReveal", "PostVowReveal", "sfx_trait_lit",
                 })
            Assert.False(game.Contains(gone, StringComparison.Ordinal),
                         $"Game1.cs still carries `{gone}` — the awakening plate was only half removed.");
    }

    /// <summary>
    /// NO SCREEN STILL BUILDS TOAST COPY FOR NEWS IT NO LONGER SENDS THERE.
    /// </summary>
    /// <remarks>
    /// The GEAR screen handed the host two ready-made lines for a five-piece completion. The host
    /// posts a letter now and the words are rendered by <see cref="DispatchCopy"/>, so the builder
    /// became production code with no caller — which compiles, ships, and keeps a second copy of a
    /// sentence that can drift from the one the player actually reads.
    /// </remarks>
    [Fact]
    public void test_no_screen_still_builds_copy_for_a_surface_the_product_does_not_have()
    {
        var gear = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "GearScreen.cs")).Replace("\r\n", "\n");
        Assert.DoesNotContain("ConsumeNotice", gear, StringComparison.Ordinal);
        // The two lines themselves, by the shape they were built in. (CapstoneName survives as an
        // icon key on this screen, which is a name and not a sentence.)
        Assert.DoesNotContain("COMPLETE\\n", gear, StringComparison.Ordinal);
        Assert.DoesNotContain("} ACTIVE\"", gear, StringComparison.Ordinal);
        // The event itself is what the screen still reports, and the host still reads it.
        Assert.Contains("public string? ConsumeCompletedSet()", gear, StringComparison.Ordinal);
        Assert.Contains("_gear.ConsumeCompletedSet()", Game1Source(), StringComparison.Ordinal);
    }

    /// <summary>Every background producer builds a TYPED letter through the factory for its kind.</summary>
    [Fact]
    public void test_every_background_producer_posts_a_typed_dispatch()
    {
        var game = Game1Source();
        foreach (var factory in new[]
                 {
                     "Dispatches.Unlock(", "Dispatches.Quest(", "Dispatches.Champion(", "Dispatches.Keystone(",
                     "Dispatches.Vow(", "Dispatches.Trait(", "Dispatches.Gem(", "Dispatches.Socket(",
                     "Dispatches.Set(", "Dispatches.Region(", "Dispatches.Migration(",
                 })
            Assert.True(game.Contains("PostDispatch(" + factory, StringComparison.Ordinal),
                        $"no producer posts `{factory}` — that kind of news has no letter.");
    }

    /// <summary>
    /// A screen the opening walks the player into is marked KNOWN, never posted: being shown a screen
    /// and then told it opened is the same sentence twice.
    /// </summary>
    [Fact]
    public void test_a_screen_the_opening_walks_into_is_known_and_never_posted()
    {
        var game = Game1Source();
        Assert.Contains("&& !OpeningWalksInto(opened))\n                    PostDispatch(Dispatches.Unlock(opened, SaveFile.NowMs));\n"
                        + "                else\n                    KnowDispatch(DispatchKeys.Unlock(opened));",
                        game, StringComparison.Ordinal);
    }

    /// <summary>The ROSTER tile's NEW mark IS an unread champion letter — so it survives a restart.</summary>
    [Fact]
    public void test_the_roster_mark_is_an_unread_champion_letter()
    {
        var inbox = new Inbox();
        Assert.Null(Game1.UnreadChampion(inbox.Rows));

        inbox.Post(Dispatches.Region("verdant_hollow", 1_000));
        Assert.Null(Game1.UnreadChampion(inbox.Rows));   // other news is not a champion

        inbox.Post(Dispatches.Champion("anvil", 2_000));
        var waiting = Game1.UnreadChampion(inbox.Rows);
        Assert.NotNull(waiting);
        Assert.Equal("anvil", waiting!.SubjectId);
        Assert.Equal(CharacterRoster.Get("anvil").Name, CharacterRoster.Find(waiting.SubjectId!)!.Name);

        // Reading it is what clears the mark — and the read is a persisted fact, not a session flag.
        Assert.True(inbox.MarkRead(DispatchKeys.HunterJoined("anvil")));
        Assert.Null(Game1.UnreadChampion(inbox.Rows));
    }

    /// <summary>The newest waiting champion is the one the rail's hint names.</summary>
    [Fact]
    public void test_the_newest_waiting_champion_is_the_one_named()
    {
        var inbox = new Inbox();
        inbox.Post(Dispatches.Champion("anvil", 1_000));
        inbox.Post(Dispatches.Champion("chorus", 2_000));
        Assert.Equal("chorus", Game1.UnreadChampion(inbox.Rows)!.SubjectId);
    }

    /// <summary>The mark is derived from the inbox, never from a session flag beside it.</summary>
    [Fact]
    public void test_no_session_flag_stands_beside_the_champion_letter()
    {
        var game = Game1Source();
        foreach (var gone in new[] { "_rosterNews", "_rosterNewName", "_rosterBaselined" })
            Assert.False(game.Contains(gone, StringComparison.Ordinal),
                         $"Game1.cs still carries `{gone}` — the roster mark has two sources of truth.");
        Assert.Contains("private bool RosterNews => UnreadChampion(_inbox.Rows) is not null;", game, StringComparison.Ordinal);
        Assert.Contains("internal static Dispatch? UnreadChampion(", DispatchesSource(), StringComparison.Ordinal);
    }

    /// <summary>
    /// SIX AWAKENINGS ARE SIX LETTERS, and the list is what summarises them. The combined plate named
    /// none of the traits it was announcing; a row per trait names every one.
    /// </summary>
    [Fact]
    public void test_many_awakenings_at_once_are_many_letters_and_no_summary()
    {
        var inbox = new Inbox();
        var woke = new[] { "t_scar_tissue", "t_last_word", "t_deep_cut" };
        foreach (var (id, i) in woke.Select((id, i) => (id, i)))
            Assert.True(inbox.Post(Dispatches.Trait(id, 1_000 + i)));

        Assert.Equal(woke.Length, inbox.Count);
        Assert.Equal(woke.Length, inbox.Unread);
        Assert.All(inbox.Rows, row => Assert.Equal(DispatchKind.Trait, row.Kind));
        // ...and the same load a second time tells none of it again.
        foreach (var id in woke) Assert.False(inbox.Post(Dispatches.Trait(id, 9_000)));
        Assert.Equal(woke.Length, inbox.Count);
    }

    /// <summary>Posting the same event twice — from either of its two producers — is one row.</summary>
    [Theory]
    [MemberData(nameof(EveryLetter))]
    public void test_a_producer_that_fires_twice_writes_one_row(Dispatch first, Dispatch again)
    {
        var inbox = new Inbox();
        Assert.True(inbox.Post(first));
        Assert.False(inbox.Post(again));
        Assert.Equal(1, inbox.Count);
        Assert.Equal(1, inbox.Unread);
    }

    public static IEnumerable<object[]> EveryLetter() => new[]
    {
        new object[] { Dispatches.Unlock(Activity.Vault, 1), Dispatches.Unlock(Activity.Vault, 2) },
        new object[] { Dispatches.Quest("q_cinder_deep", 1), Dispatches.Quest("q_cinder_deep", 2) },
        new object[] { Dispatches.Champion("anvil", 1), Dispatches.Champion("anvil", 2) },
        // The two keystone producers — a mastery rung and a conquest — are one event and one key.
        new object[] { Dispatches.Keystone("echo", 1), Dispatches.Keystone("echo", 2) },
        // ...as are the Vow that is offered and the Vow that proves itself.
        new object[] { Dispatches.Vow("vow_singular", 1), Dispatches.Vow("vow_singular", 2) },
        new object[] { Dispatches.Trait("t_scar_tissue", 1), Dispatches.Trait("t_scar_tissue", 2) },
        new object[] { Dispatches.Gem(1), Dispatches.Gem(2) },
        new object[] { Dispatches.Socket(1), Dispatches.Socket(2) },
        new object[] { Dispatches.Set(IdleXIdle.Core.Sources.Source.Nature, 1), Dispatches.Set(IdleXIdle.Core.Sources.Source.Nature, 2) },
        new object[] { Dispatches.Region("verdant_hollow", 1), Dispatches.Region("verdant_hollow", 2) },
        new object[] { Dispatches.Migration(DispatchKeys.MigrationKeystones, 1, 3), Dispatches.Migration(DispatchKeys.MigrationKeystones, 2, 9) },
    };
}
