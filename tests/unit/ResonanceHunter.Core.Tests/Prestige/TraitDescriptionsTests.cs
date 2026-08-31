using System;
using System.Linq;
using System.Text.RegularExpressions;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Prestige;
using Xunit;

namespace ResonanceHunter.Core.Tests.Prestige;

/// <summary>
/// The plain-words rewrite of 2026-08-26: every trait description is at most two short sentences a
/// twelve-year-old could read — what it does, with the number, and at most one thing to know.
/// </summary>
/// <remarks>
/// The owner reads the game in English as a second language and asked for the traits "explained with
/// plain words". The previous descriptions were placement and flavour ("The second step down the Ruin
/// road.", "A quiet mark on your record") that said nothing about the fight. These tests hold the new
/// shape so a content pass cannot drift back: a length cap, a sentence cap, a banned-word list, the
/// font gate, and — for the nodes whose whole effect is a number or a keystone — that the description
/// says so in the same words the rest of the game uses.
/// </remarks>
public class TraitDescriptionsTests
{
    /// <summary>The metaphors and engine words the rewrite removed. None may come back.</summary>
    private static readonly string[] BannedWords = { "road", "bargain", "amplifier", "expansion", "convenience" };

    /// <summary>What the font can draw: ASCII, and exactly these extras.</summary>
    private const string FontExtras = "·×—–→←‹›…";

    private static int Sentences(string text) => text.Count(ch => ch is '.' or '!' or '?');

    [Fact]
    public void test_every_description_is_at_most_two_short_sentences()
    {
        foreach (var u in MemoryDustTree.Catalog)
        {
            var d = u.Description;
            Assert.False(string.IsNullOrWhiteSpace(d), $"{u.Id} has no description");
            Assert.True(d.Length <= 140, $"{u.Id}'s description is {d.Length} characters: {d}");
            Assert.True(Sentences(d) <= 2, $"{u.Id}'s description has more than two sentences: {d}");
            Assert.EndsWith(".", d);
            Assert.True(char.IsUpper(d[0]), $"{u.Id}'s description does not start a sentence: {d}");
        }
    }

    [Fact]
    public void test_no_description_uses_a_banned_word()
    {
        foreach (var u in MemoryDustTree.Catalog)
            foreach (var word in BannedWords)
                Assert.False(Regex.IsMatch(u.Description, $@"\b{word}s?\b", RegexOptions.IgnoreCase),
                    $"{u.Id} says \"{word}\": {u.Description}");
    }

    [Fact]
    public void test_every_description_passes_the_font_gate()
    {
        foreach (var u in MemoryDustTree.Catalog)
            foreach (var ch in u.Description)
                Assert.True(ch < 128 || FontExtras.Contains(ch), $"{u.Id}'s description uses a glyph the font cannot draw: '{ch}'");
    }

    [Fact]
    public void test_an_attribute_node_opens_with_the_number_its_mods_move()
    {
        // The number is said ONCE, in the description, in exactly the words the generator would use —
        // so the catalogue and the sim cannot disagree, and the panel never prints it twice.
        foreach (var u in MemoryDustTree.Catalog.Where(u => u.Mods != BuildMods.None))
            Assert.StartsWith(MemoryDustText.ModsSentence(u.Mods), u.Description);
    }

    [Fact]
    public void test_a_keystone_gate_says_it_lets_you_wear_the_keystone_on_the_build_screen()
    {
        foreach (var gate in MemoryDustTree.Catalog.Where(u => u.GrantsKeystone is not null))
        {
            var keystone = Keystones.ById(gate.GrantsKeystone)!;
            Assert.StartsWith($"Lets you wear the keystone {keystone.Name} on the Build screen.", gate.Description);
        }
    }

    [Fact]
    public void test_a_terminal_warns_that_no_second_branch_can_follow()
    {
        // The one thing to know before spending twelve points: after this you cannot finish another
        // branch. Every twelve-point node says so; nothing cheaper does.
        foreach (var u in MemoryDustTree.Catalog)
        {
            var warns = u.Description.Contains("cannot afford to finish another branch", StringComparison.Ordinal);
            Assert.Equal(u.Cost >= 12, warns);
        }
    }

    [Theory]
    [InlineData("socket_2", "You can wear two keystones at once instead of one.")]
    [InlineData("ruin_edge_2", "Your hits do 6% more damage.")]
    [InlineData("ks_bloodlust", "Lets you wear the keystone BLOODLUST on the Build screen.")]
    [InlineData("efficient_forge", "Salvaging an item gives you 15% more material. Selling still pays more than salvaging.")]
    [InlineData("artifice_vows", "Every vow you take pays 25% more.")]
    public void test_a_description_says_what_the_node_does(string id, string expected)
        => Assert.Equal(expected, MemoryDustTree.Catalog.First(u => u.Id == id).Description);

    [Fact]
    public void test_the_sheet_reads_what_it_does_then_cost_then_needs_then_permanent()
    {
        // The detail panel prints these four in this order; the sheet is the same words in the same
        // order, so the panel and the test hold one sentence.
        var sheet = MemoryDustText.Sheet(MemoryDustTree.Catalog.First(u => u.Id == "ks_bloodlust"));
        var does = sheet.IndexOf("Lets you wear the keystone BLOODLUST", StringComparison.Ordinal);
        var costs = sheet.IndexOf("Costs 6 trait points.", StringComparison.Ordinal);
        var needs = sheet.IndexOf("You need KEYSTONE — GLASS CANNON first.", StringComparison.Ordinal);
        var permanent = sheet.IndexOf(MemoryDustText.Permanence, StringComparison.Ordinal);
        Assert.True(does >= 0 && does < costs && costs < needs && needs < permanent, sheet);
        Assert.Equal("Permanent. Never resets.", MemoryDustText.Permanence);
    }
}
