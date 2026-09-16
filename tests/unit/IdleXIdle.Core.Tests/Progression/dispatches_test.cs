using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Quests;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// DISPATCHES — the account's inbox of news. A dispatch is a semantic event with a STABLE KEY, told
/// once and never twice; reading one changes presentation and nothing else; the inbox IS the dedupe.
/// Copy is rendered at display time from the catalogues, never persisted.
/// </summary>
public class DispatchesTest
{
    private const long Now = 1_700_000_000_000L;

    // ── 1. ONCE, NEVER TWICE ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_key_is_posted_once_and_never_twice()
    {
        var inbox = new Inbox();

        Assert.True(inbox.Post(Dispatches.Region("verdant_hollow", Now)));
        Assert.False(inbox.Post(Dispatches.Region("verdant_hollow", Now + 1)));

        Assert.Single(inbox.Rows);
        Assert.Equal(1, inbox.Unread);
        Assert.True(inbox.IsKnown(DispatchKeys.RegionConquered("verdant_hollow")));
    }

    [Fact]
    public void test_a_second_post_with_a_different_payload_but_the_same_key_is_ignored()
    {
        // Two producers describing the same event differently still collapse to ONE message, and the
        // first telling is the one kept — a duplicate never rewrites what the player was told.
        var inbox = new Inbox();
        var first = new Dispatch("trait.t_deep_cut", DispatchKind.Trait, Now, Read: false, SubjectId: "t_deep_cut");
        var again = first with { AtMs = Now + 5000, Read = true, SubjectId = "t_other", Count = 3 };

        Assert.True(inbox.Post(first));
        Assert.False(inbox.Post(again));

        Assert.Equal(first, inbox.Rows.Single());
        Assert.Equal(1, inbox.Unread);
    }

    [Fact]
    public void test_knowing_a_key_creates_no_row_and_refuses_the_post()
    {
        // KNOWN is the monotone half; ROWS are the visible half. A seeded key has no row, no
        // timestamp and nothing to read — it only stops the news from arriving as news.
        var inbox = new Inbox();

        Assert.True(inbox.Know(DispatchKeys.QuestComplete("q_cinder_deep")));
        Assert.False(inbox.Know(DispatchKeys.QuestComplete("q_cinder_deep")));

        Assert.Empty(inbox.Rows);
        Assert.Equal(0, inbox.Unread);
        Assert.False(inbox.Post(Dispatches.Quest("q_cinder_deep", Now)));
        Assert.Empty(inbox.Rows);
    }

    // ── 2. READING CHANGES READ AND NOTHING ELSE ─────────────────────────────────────────────────

    [Fact]
    public void test_mark_read_changes_read_and_nothing_else()
    {
        var inbox = new Inbox();
        inbox.Post(Dispatches.Quest("q_cinder_deep", Now));
        inbox.Post(Dispatches.Champion("anvil", Now + 1));
        var before = inbox.Ledger.ToList();
        Assert.Equal(2, inbox.Unread);

        Assert.True(inbox.MarkRead(DispatchKeys.QuestComplete("q_cinder_deep")));
        Assert.False(inbox.MarkRead(DispatchKeys.QuestComplete("q_cinder_deep")));   // already read: no change
        Assert.False(inbox.MarkRead("quest.nothing.complete"));                       // unknown: no change, no row

        Assert.Equal(1, inbox.Unread);
        Assert.Equal(2, inbox.Count);
        Assert.Equal(before[0] with { Read = true }, inbox.Ledger[0]);
        Assert.Equal(before[1], inbox.Ledger[1]);

        Assert.Equal(1, inbox.MarkAllRead());
        Assert.Equal(0, inbox.MarkAllRead());
        Assert.Equal(0, inbox.Unread);
        Assert.Equal(2, inbox.Count);
        Assert.Equal(before.Select(r => r.Key), inbox.Ledger.Select(r => r.Key));   // order untouched
        Assert.Equal(2, inbox.Known.Count);                                          // known untouched
    }

    [Fact]
    public void test_rows_read_newest_first_and_the_ledger_in_created_order()
    {
        var inbox = new Inbox();
        inbox.Post(Dispatches.Gem(Now));
        inbox.Post(Dispatches.Socket(Now + 1));
        inbox.Post(Dispatches.Set(Source.Machine, Now + 2));

        Assert.Equal(new[] { DispatchKind.Gem, DispatchKind.Socket, DispatchKind.Set }, inbox.Ledger.Select(r => r.Kind));
        Assert.Equal(new[] { DispatchKind.Set, DispatchKind.Socket, DispatchKind.Gem }, inbox.Rows.Select(r => r.Kind));
    }

    // ── 3. THE CAP ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_cap_prunes_the_oldest_read_rows_and_never_an_unread_row_or_a_known_key()
    {
        var inbox = new Inbox();
        for (var i = 0; i < Inbox.Cap; i++) inbox.Post(Dispatches.Trait($"t_{i:D3}", Now + i));
        for (var i = 0; i < 10; i++) inbox.MarkRead(DispatchKeys.Trait($"t_{i:D3}"));
        Assert.Equal(Inbox.Cap, inbox.Count);

        for (var i = 0; i < 5; i++) Assert.True(inbox.Post(Dispatches.Vow($"v_{i}", Now + 100 + i)));

        Assert.Equal(Inbox.Cap, inbox.Count);
        // The five OLDEST read rows went; the five younger read rows stayed; nothing unread moved.
        for (var i = 0; i < 5; i++) Assert.DoesNotContain(inbox.Rows, r => r.Key == DispatchKeys.Trait($"t_{i:D3}"));
        for (var i = 5; i < 10; i++) Assert.Contains(inbox.Rows, r => r.Key == DispatchKeys.Trait($"t_{i:D3}"));
        Assert.Equal(Inbox.Cap - 5, inbox.Unread);
        // ...and every key ever posted is still KNOWN, so a pruned row can never come back as news.
        Assert.Equal(Inbox.Cap + 5, inbox.Known.Count);
        Assert.False(inbox.Post(Dispatches.Trait("t_000", Now + 999)));
    }

    [Fact]
    public void test_the_cap_yields_rather_than_throw_away_an_unread_letter()
    {
        var inbox = new Inbox();
        for (var i = 0; i <= Inbox.Cap; i++) inbox.Post(Dispatches.Trait($"t_{i:D3}", Now + i));

        Assert.Equal(Inbox.Cap + 1, inbox.Count);
        Assert.Equal(Inbox.Cap + 1, inbox.Unread);
    }

    // ── 4. COPY IS RENDERED, NEVER STORED ────────────────────────────────────────────────────────

    public static IEnumerable<object[]> EveryKind()
        => Enum.GetValues<DispatchKind>().Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void test_copy_renders_a_headline_and_a_body_for_every_kind(DispatchKind kind)
    {
        var d = Sample(kind);
        Assert.Equal(kind, d.Kind);
        Assert.False(string.IsNullOrWhiteSpace(DispatchCopy.Headline(d)), $"{kind} has no headline");
        Assert.False(string.IsNullOrWhiteSpace(DispatchCopy.Body(d)), $"{kind} has no body");
    }

    private static Dispatch Sample(DispatchKind kind) => kind switch
    {
        DispatchKind.Unlock => Dispatches.Unlock(Activity.Training, Now),
        DispatchKind.Gem => Dispatches.Gem(Now),
        DispatchKind.Trait => Dispatches.Trait(TraitCatalogue.All[0].Id, Now),
        DispatchKind.Vow => Dispatches.Vow(Vows.Catalog[0].Id, Now),
        DispatchKind.Keystone => Dispatches.Keystone(Keystones.Catalog[0].Id, Now),
        DispatchKind.Champion => Dispatches.Champion(CharacterRoster.All[0].Id, Now),
        DispatchKind.Quest => Dispatches.Quest(QuestCatalogue.All[0].Id, Now),
        DispatchKind.Set => Dispatches.Set(Source.Machine, Now),
        DispatchKind.Region => Dispatches.Region(Regions.All[0].Id, Now),
        DispatchKind.Socket => Dispatches.Socket(Now),
        DispatchKind.Migration => Dispatches.Migration(DispatchKeys.MigrationKeystones, Now, count: 3),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    [Fact]
    public void test_copy_names_the_subject_from_its_catalogue()
    {
        Assert.Contains("VERDANT HOLLOW", DispatchCopy.Headline(Dispatches.Region("verdant_hollow", Now)));
        Assert.Contains("THE ANVIL", DispatchCopy.Headline(Dispatches.Champion("anvil", Now)));
        Assert.Contains("IRONCLAD", DispatchCopy.Headline(Dispatches.Keystone("ironclad", Now)));
        Assert.Contains("MACHINE SET", DispatchCopy.Headline(Dispatches.Set(Source.Machine, Now)));
        Assert.Contains("TRAINING", DispatchCopy.Headline(Dispatches.Unlock(Activity.Training, Now)));
        Assert.Equal(Unlocks.OpenedLine(Activity.Training), DispatchCopy.Body(Dispatches.Unlock(Activity.Training, Now)));
        Assert.Contains("3 KEYSTONES", DispatchCopy.Headline(Dispatches.Migration(DispatchKeys.MigrationKeystones, Now, count: 3)));
    }

    /// <summary>
    /// A VOW SAYS HOW IT ARRIVED. One key for both producers — a Vow is discovered once, however it
    /// came — but not one sentence: a GRANTED Vow is handed to the player with the BUILD screen, and
    /// telling them it "revealed itself" credits them with a discovery they did not make. Every other
    /// Vow is proved by having already kept its rule, and that one really did reveal itself.
    /// </summary>
    [Fact]
    public void test_a_vow_letter_says_whether_it_was_offered_or_proved()
    {
        Assert.NotEmpty(Vows.Granted);
        Assert.NotEmpty(Vows.Discoverable);

        foreach (var vow in Vows.Granted)
        {
            var head = DispatchCopy.Headline(Dispatches.Vow(vow.Id, Now));
            Assert.Equal($"A VOW IS OFFERED TO YOU — {vow.Name}", head);
        }

        foreach (var vow in Vows.Discoverable)
        {
            var head = DispatchCopy.Headline(Dispatches.Vow(vow.Id, Now));
            Assert.Equal($"A VOW HAS REVEALED ITSELF — {vow.Name}", head);
        }

        // ...and both are still ONE key, so the two producers can never tell the same Vow twice.
        var granted = Vows.Granted[0];
        var inbox = new Inbox();
        Assert.True(inbox.Post(Dispatches.Vow(granted.Id, Now)));
        Assert.False(inbox.Post(Dispatches.Vow(granted.Id, Now + 1)));
        Assert.Equal(1, inbox.Count);

        // A Vow the catalogue no longer holds still reads as a Vow, and never as a raw id.
        var gone = DispatchCopy.Headline(Dispatches.Vow("vow_that_left", Now));
        Assert.Equal("A VOW HAS REVEALED ITSELF", gone);
        Assert.DoesNotContain("vow_that_left", gone);
    }

    [Fact]
    public void test_copy_falls_back_plainly_when_a_subject_has_left_the_catalogue()
    {
        // A trait retired, a region renamed, a quest removed: the row is still a letter the player was
        // sent. It renders with a plain line and the raw id never reaches the player; it never throws.
        var gone = new[]
        {
            new Dispatch("trait.t_gone", DispatchKind.Trait, Now, false, SubjectId: "t_gone"),
            new Dispatch("vow.v_gone", DispatchKind.Vow, Now, false, SubjectId: "v_gone"),
            new Dispatch("keystone.k_gone", DispatchKind.Keystone, Now, false, SubjectId: "k_gone"),
            new Dispatch("hunter.h_gone.joined", DispatchKind.Champion, Now, false, SubjectId: "h_gone"),
            new Dispatch("quest.q_gone.complete", DispatchKind.Quest, Now, false, SubjectId: "q_gone"),
            new Dispatch("set.gone.complete", DispatchKind.Set, Now, false, SubjectId: "gone"),
            new Dispatch("region.r_gone.conquered", DispatchKind.Region, Now, false, RegionId: "r_gone"),
            new Dispatch("unlock.gone", DispatchKind.Unlock, Now, false, SubjectId: "Gone"),
            new Dispatch("migration.gone", DispatchKind.Migration, Now, false, SubjectId: "gone"),
            new Dispatch("trait.nothing", DispatchKind.Trait, Now, false),   // no subject at all
        };
        foreach (var d in gone)
        {
            var headline = DispatchCopy.Headline(d);
            Assert.False(string.IsNullOrWhiteSpace(headline), $"{d.Key} has no fallback headline");
            Assert.DoesNotContain("gone", headline, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(DispatchCopy.Body(d));
        }
    }

    // ── 5. KEYS RENAME FORWARD ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_saved_key_is_read_forward_across_a_screen_rename()
    {
        // The explained list already reads "Stats" forward as Training (Onboarding.ModernScreenKey);
        // an unlock key built from the same name has to follow it, or a rename would make a screen a
        // returning player has used for hours arrive as news.
        Assert.Equal("unlock.training", DispatchKeys.Unlock(Activity.Training));
        Assert.Equal("unlock.training", Dispatches.ModernDispatchKey("unlock.stats"));
        Assert.Equal("unlock.training", Dispatches.ModernDispatchKey("unlock.training"));
        Assert.Equal("trait.t_deep_cut", Dispatches.ModernDispatchKey("trait.t_deep_cut"));
        Assert.Equal("region.verdant_hollow.conquered", Dispatches.ModernDispatchKey("region.verdant_hollow.conquered"));

        // ...and the inbox reads it forward on the way in, so the modern producer is refused as known
        // and the old row still renders as the screen it now is.
        var inbox = new Inbox();
        inbox.Restore(new[] { new Dispatch("unlock.stats", DispatchKind.Unlock, Now, true, SubjectId: "Stats") },
                      new[] { "unlock.stats" });
        Assert.True(inbox.IsKnown("unlock.training"));
        Assert.False(inbox.Post(Dispatches.Unlock(Activity.Training, Now)));
        Assert.Equal("unlock.training", inbox.Rows.Single().Key);
        Assert.Contains("TRAINING", DispatchCopy.Headline(inbox.Rows.Single()));
    }

    // ── 6. THE MIGRATION ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_inbox_migration_version_is_frozen_at_eight()
    {
        // IT MUST NEVER BE SaveGame.CurrentVersion. Written that way, the next bump would start
        // seeding every version-8 file — files whose inbox is honest — and every unread letter a
        // player had not yet opened would be marked as already known. The number names a moment in
        // this project's history, and that moment does not move when the format does.
        Assert.Equal(8, Dispatches.FirstVersionWithInbox);
        Assert.True(Dispatches.FirstVersionWithInbox <= SaveGame.CurrentVersion,
                    "the inbox cannot first be persisted in a version that does not exist yet");
    }

    [Fact]
    public void test_a_save_written_by_this_build_is_believed_and_never_seeded()
    {
        // THE VERSION IS THE TEST. A file at or past FirstVersionWithInbox carries an honest inbox —
        // including a brand-new career that conquered a region and reloaded before reading the letter
        // about it, which is exactly the case a progression guess gets wrong.
        var inbox = new Inbox();
        var fresh = new SaveGame { SavedAtMs = Now, ConqueredRegions = new() { "verdant_hollow" } };
        Assert.Equal(SaveGame.CurrentVersion, fresh.Version);

        Assert.False(Dispatches.SeedKnown(fresh, new[] { Activity.Training }, inbox));
        Assert.Empty(inbox.Known);
        Assert.Empty(inbox.Rows);

        var atEight = fresh with { Version = Dispatches.FirstVersionWithInbox };
        Assert.False(Dispatches.SeedKnown(atEight, new[] { Activity.Training }, inbox));
        Assert.Empty(inbox.Known);

        // The same file one version older has never been told anything, and is the veteran.
        var older = fresh with { Version = Dispatches.FirstVersionWithInbox - 1 };
        Assert.True(Dispatches.SeedKnown(older, new[] { Activity.Hunt, Activity.Training }, inbox));
        Assert.Contains(DispatchKeys.RegionConquered("verdant_hollow"), inbox.Known);
        Assert.Contains(DispatchKeys.Unlock(Activity.Training), inbox.Known);
        Assert.DoesNotContain(DispatchKeys.Unlock(Activity.Hunt), inbox.Known);   // the Hunt is never news
        Assert.Empty(inbox.Rows);
        Assert.Equal(0, inbox.Unread);
    }

    [Fact]
    public void test_an_old_file_with_nothing_behind_it_is_seeded_with_only_the_hunter_it_started_with()
    {
        // The seed reads facts, not progression: a file that has done nothing knows nothing — except
        // that its starter is on the roster, which is true of every save there has ever been
        // (LegacyUnlocks seeds the starter unconditionally, exactly as RestoreCharacters does).
        var inbox = new Inbox();
        var empty = new SaveGame { SavedAtMs = Now, Version = Dispatches.FirstVersionWithInbox - 1 };

        Assert.True(Dispatches.SeedKnown(empty, new[] { Activity.Hunt }, inbox));

        Assert.Equal(
            new[]
            {
                DispatchKeys.HunterJoined(CharacterRoster.StarterId),
                DispatchKeys.Migration(DispatchKeys.MigrationKeystones),
                DispatchKeys.Migration(DispatchKeys.MigrationFifthSlot),
            }.OrderBy(k => k, StringComparer.Ordinal),
            inbox.Known.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(inbox.Rows);
    }

    /// <summary>
    /// THE TWO ONE-SHOTS THE GAME OWES A RETURNING PLAYER ARE SEEDED KNOWN, whatever the file holds.
    /// </summary>
    /// <remarks>
    /// Both fire on the FIRST pass of a file whose keystones moved house — which is every file from
    /// before the inbox — so without this the migration they have already lived through, silently,
    /// on an earlier launch would arrive as unread mail on the very boot that introduced the inbox.
    /// They are the one family whose key is not derived from a fact: nothing in the save records
    /// having been told, so the version is the only honest answer.
    /// </remarks>
    [Fact]
    public void test_the_migration_one_shots_are_seeded_known_for_every_old_file()
    {
        foreach (var save in new[]
                 {
                     new SaveGame { SavedAtMs = Now, Version = Dispatches.FirstVersionWithInbox - 1 },
                     new SaveGame
                     {
                         SavedAtMs = Now, Version = Dispatches.FirstVersionWithInbox - 1,
                         ConqueredRegions = new List<string> { "verdant_hollow" },
                     },
                 })
        {
            var inbox = new Inbox();
            Assert.True(Dispatches.SeedKnown(save, new[] { Activity.Hunt }, inbox));
            Assert.Contains(DispatchKeys.Migration(DispatchKeys.MigrationKeystones), inbox.Known);
            Assert.Contains(DispatchKeys.Migration(DispatchKeys.MigrationFifthSlot), inbox.Known);
            Assert.Empty(inbox.Rows);
        }
    }

    // ── 7. THE LETTERS SPEAK ─────────────────────────────────────────────────────────────────────
    //
    //     Keystone, Vow and Set bodies used to paste a catalogue string authored ALL-CAPS for the
    //     retired toast, so three kinds of letter shouted beside eight that spoke. They are written
    //     from typed data now; these four tests are the proof, and the fourth proves the catalogue
    //     strings themselves were left alone for the screens that still print them.

    /// <summary>Every keystone, Vow and set letter, with the name its catalogue gives the subject.</summary>
    private static IEnumerable<(string Subject, string Id, string Name, string Body)> SpokenLetters()
    {
        foreach (var k in Keystones.Catalog)
            yield return ("keystone", k.Id, k.Name, DispatchCopy.Body(Dispatches.Keystone(k.Id, Now)));
        foreach (var v in Vows.Catalog)
            yield return ("vow", v.Id, v.Name, DispatchCopy.Body(Dispatches.Vow(v.Id, Now)));
        foreach (var s in Enum.GetValues<Source>())
            yield return ("set", s.ToString(), ElementSets.Name(s), DispatchCopy.Body(Dispatches.Set(s, Now)));
    }

    [Fact]
    public void test_keystone_vow_and_set_letters_read_as_sentences()
    {
        var letters = SpokenLetters().ToList();
        Assert.Equal(Keystones.Catalog.Count + Vows.Catalog.Count + Enum.GetValues<Source>().Length, letters.Count);

        foreach (var (subject, id, name, body) in letters)
        {
            Assert.False(string.IsNullOrWhiteSpace(body), $"{subject} {id} has no body");
            Assert.True(body != body.ToUpperInvariant(), $"{subject} {id} still shouts: \"{body}\"");
            Assert.EndsWith(".", body, StringComparison.Ordinal);
            Assert.Contains(name, body, StringComparison.Ordinal);
            Assert.DoesNotContain(id, body, StringComparison.Ordinal);
            Assert.DoesNotContain("  ", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The ways a multiplier may honestly be spoken: x2 is "double" or "twice", x0.5 is "half", and
    /// anything else is the whole percentage it cuts ("30% softer") or the whole percentage it keeps
    /// ("each at 60%"). Whichever the sentence chose, the number is the field's and not a retyped one.
    /// </summary>
    private static string[] Spellings(float multiplier)
    {
        if (Math.Abs(multiplier - 2f) < 0.0001f) return new[] { "double", "twice" };
        if (Math.Abs(multiplier - 3f) < 0.0001f) return new[] { "triple" };
        if (Math.Abs(multiplier - 0.5f) < 0.0001f) return new[] { "half" };
        return new[] { WholePercent(Math.Abs(1f - multiplier)), WholePercent(multiplier) };
    }

    private static string WholePercent(float fraction)
        => MathF.Round(fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

    [Fact]
    public void test_a_keystone_letter_names_every_number_it_moves()
    {
        foreach (var k in Keystones.Catalog)
        {
            var body = DispatchCopy.Body(Dispatches.Keystone(k.Id, Now));

            // No catalogue keystone falls through to the fallback arm, which is the Blurb, shouting.
            Assert.DoesNotContain(k.Blurb.Trim(), body, StringComparison.Ordinal);

            var m = k.Mods;
            foreach (var (field, value) in new[]
                     {
                         ("Damage", m.Damage), ("Health", m.Health), ("SkillRate", m.SkillRate),
                         ("Haul", m.Haul), ("Rarity", m.Rarity),
                     })
            {
                if (Math.Abs(value - 1f) < 0.0001f) continue;
                var spellings = Spellings(value);
                Assert.True(spellings.Any(s => body.Contains(s, StringComparison.OrdinalIgnoreCase)),
                            $"{k.Id}: {field} x{value} is never named — expected one of [{string.Join(", ", spellings)}] in \"{body}\"");
            }

            // The CHARGE keystones and WEAVER carry numbers that are not on Mods at all: they are the
            // fight's own constants, and the letter must read them from there.
            if (k.Grants.Contains(BuildTrigger.Rend))
            {
                Assert.Contains($"up to {SoloBattle.ChargeCap}", body, StringComparison.Ordinal);
                Assert.Contains(WholePercent(SoloBattle.ChargeRendPerPoint), body, StringComparison.Ordinal);
            }
            if (k.Grants.Contains(BuildTrigger.Capacitor))
                Assert.Contains($"{SoloBattle.ChargeCapExtended} instead of {SoloBattle.ChargeCap}", body, StringComparison.Ordinal);
            if (k.Grants.Contains(BuildTrigger.Dynamo))
                Assert.Contains($"stores {SoloBattle.ChargeDynamoPerBite} charge", body, StringComparison.Ordinal);
            if (k.Grants.Contains(BuildTrigger.Weaver))
                Assert.Contains(WholePercent(SoloBattle.WeaverEchoFraction), body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void test_a_vow_letter_counts_its_own_proof_waves()
    {
        var conduct = new List<string>();
        foreach (var v in Vows.Catalog)
        {
            var body = DispatchCopy.Body(Dispatches.Vow(v.Id, Now));
            switch (v.Proof)
            {
                case VowProof.Demand:
                    Assert.Contains($"{v.ProofWaves} waves", body, StringComparison.Ordinal);
                    if (v.Demand is VowDemand.CadenceAtOrBelow or VowDemand.CadenceAtOrAbove)
                        Assert.Contains(v.Threshold.ToString("0.00", CultureInfo.InvariantCulture) + "x", body, StringComparison.Ordinal);
                    break;
                case VowProof.Conduct:
                    Assert.Contains($"{v.ProofWaves} waves", body, StringComparison.Ordinal);
                    conduct.Add(body);
                    break;
                case VowProof.Granted:
                    // A granted Vow was not proved, so it counts no waves; it states its pay instead.
                    Assert.Contains(WholePercent(Vows.Multiplier(v) - 1f), body, StringComparison.Ordinal);
                    Assert.DoesNotContain("waves", body, StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(v.Proof), v.Proof, null);
            }
        }

        // The two conduct Vows have no demand to keep, and branch by id the way Vows.RuleHeld does —
        // so their letters must say two different things.
        Assert.Equal(Vows.Catalog.Count(v => v.Proof == VowProof.Conduct), conduct.Count);
        Assert.True(conduct.Count >= 2, "the catalogue no longer holds two conduct Vows");
        Assert.Equal(conduct.Count, conduct.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// House caps: every letter uppercase, with the one lowercase the catalogue allows — the
    /// multiplier unit after a digit, "1.00x".
    /// </summary>
    private static bool IsHouseCaps(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (!char.IsLetter(c) || char.IsUpper(c)) continue;
            if (c == 'x' && i > 0 && char.IsDigit(s[i - 1])) continue;
            return false;
        }
        return true;
    }

    [Fact]
    public void test_the_catalogue_strings_are_untouched()
    {
        // BUILD, TRAITS and GEAR still print these as written. The letter's prose is rendered beside
        // them, not carved out of them.
        foreach (var k in Keystones.Catalog)
            Assert.True(IsHouseCaps(k.Blurb), $"{k.Id}: Blurb is no longer house caps — \"{k.Blurb}\"");
        foreach (var v in Vows.Catalog)
        {
            Assert.True(IsHouseCaps(v.ProofLine), $"{v.Id}: ProofLine is no longer house caps — \"{v.ProofLine}\"");
            Assert.True(IsHouseCaps(v.Description), $"{v.Id}: Description is no longer house caps — \"{v.Description}\"");
        }
        foreach (var s in Enum.GetValues<Source>())
            Assert.True(IsHouseCaps(ElementSets.CapstoneName(s)), $"{s}: CapstoneName is no longer house caps");
    }
}
