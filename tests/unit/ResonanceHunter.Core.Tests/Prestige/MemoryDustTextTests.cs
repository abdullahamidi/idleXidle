using System;
using System.Linq;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Prestige;
using Xunit;

namespace ResonanceHunter.Core.Tests.Prestige;

/// <summary>
/// The plain-English text of a trait. The playtest asked for traits that explain themselves; this is
/// the helper that does the explaining, and these pin what "explains itself" means.
/// </summary>
public class MemoryDustTextTests
{
    private static MemoryDustUnlock Node(string id) => MemoryDustTree.Catalog.First(u => u.Id == id);

    // ── Keystone gates say what the keystone DOES ─────────────────────────────────────────────

    [Fact]
    public void test_memory_dust_text_a_keystone_gate_quotes_the_keystones_own_blurb()
    {
        // Arrange: KEYSTONE — GLASS CANNON teaches GLASS CANNON, whose blurb is "DOUBLE DAMAGE. HALF HEALTH."
        var gate = Node("ks_glass_cannon");
        var keystone = Keystones.ById(gate.GrantsKeystone)!;

        // Act
        var text = MemoryDustText.Describe(gate);

        // Assert: the name, the blurb in sentence case, and the learn-is-not-wear reminder.
        Assert.Contains($"Learn the keystone {keystone.Name}:", text);
        Assert.Contains("double damage. Half health.", text);
        Assert.Contains(MemoryDustText.KeystoneReminder, text);
        Assert.DoesNotContain("DOUBLE DAMAGE", text);   // no shouting at paragraph length
    }

    [Fact]
    public void test_memory_dust_text_every_keystone_gate_in_the_catalogue_explains_its_keystone()
    {
        // Twenty of thirty-nine nodes used to say "LEARN <KEYSTONE>." and nothing else. Every gate's text
        // must now carry its keystone's blurb — that is the whole complaint this helper answers.
        foreach (var gate in MemoryDustTree.Catalog.Where(u => u.GrantsKeystone is not null))
        {
            var keystone = Keystones.ById(gate.GrantsKeystone)!;
            var text = MemoryDustText.Describe(gate);

            Assert.Contains(keystone.Name, text);
            // The blurb's first word, lower-cased, must appear — i.e. the blurb was quoted, not just named.
            var firstWord = keystone.Blurb.Split(' ')[0].ToLowerInvariant();
            Assert.Contains(firstWord, text.ToLowerInvariant());
        }
    }

    // ── Attribute nodes say the NUMBER, and the number comes from the Mods ────────────────────

    [Fact]
    public void test_memory_dust_text_mods_sentence_states_the_real_percentages()
    {
        // Arrange / Act
        var harder = MemoryDustText.ModsSentence(new BuildMods(1.06f, 1f, 1f, 1f, 1f));
        var tougher = MemoryDustText.ModsSentence(new BuildMods(1f, 1.08f, 1f, 1f, 1f));
        var faster = MemoryDustText.ModsSentence(new BuildMods(1f, 1f, 1.12f, 1f, 1f));
        var richer = MemoryDustText.ModsSentence(new BuildMods(1f, 1f, 1f, 1.08f, 1.05f));
        var softer = MemoryDustText.ModsSentence(new BuildMods(0.75f, 1f, 1f, 1f, 1f));

        // Assert: plain English, the number said outright, one sentence per field that moved.
        Assert.Equal("Every skill hits 6% harder.", harder);
        Assert.Equal("Your champion has 8% more health.", tougher);
        Assert.Equal("Your skills come back 12% faster.", faster);
        Assert.Equal("You bring back 8% more loot. Rare finds come 5% more often.", richer);
        Assert.Equal("Every skill hits 25% softer.", softer);
    }

    [Fact]
    public void test_memory_dust_text_no_mods_means_no_numbers_sentence()
    {
        Assert.Equal("", MemoryDustText.ModsSentence(BuildMods.None));
        // A pure gate's text is its description alone — nothing invented.
        Assert.Equal("A second keystone socket. You can wear two keystones at once instead of one.",
                     MemoryDustText.Describe(Node("socket_2")));
    }

    [Fact]
    public void test_memory_dust_text_every_attribute_node_in_the_catalogue_prints_its_number()
    {
        // The sentence is generated FROM the Mods, so the catalogue cannot hand-type a number that
        // disagrees with the sim. Check that every node carrying Mods gets a percentage in its text,
        // and that the percentage is the one the multiplier implies.
        var attribute = MemoryDustTree.Catalog.Where(u => u.Mods != BuildMods.None).ToList();
        Assert.NotEmpty(attribute);

        foreach (var u in attribute)
        {
            var text = MemoryDustText.Describe(u);
            var moved = new[] { u.Mods.Damage, u.Mods.Health, u.Mods.SkillRate, u.Mods.Haul, u.Mods.Rarity }
                .Where(m => MemoryDustText.Percent(m) != 0)
                .ToList();
            Assert.NotEmpty(moved);
            foreach (var m in moved)
                Assert.Contains($"{MemoryDustText.Percent(m)}%", text);
        }
    }

    // ── The sheet: number, cost, prerequisite, permanence ─────────────────────────────────────

    [Fact]
    public void test_memory_dust_text_sheet_states_the_number_the_cost_the_prerequisite_and_permanence()
    {
        // Playtest, 2026-08-25: the detail text must say the number, the cost, what comes first, and
        // that nothing here ever resets. HARDER HITS II: +6% damage, 3 points, needs BLOOD MAGIC.
        var node = Node("ruin_edge_2");
        var sheet = MemoryDustText.Sheet(node);

        Assert.Contains("6% harder", sheet);
        Assert.Contains("Costs 3 trait points.", sheet);
        Assert.Contains("You need KEYSTONE — BLOOD MAGIC first.", sheet);
        Assert.Contains("never resets", sheet);
        Assert.Contains(MemoryDustText.Permanence, sheet);

        // A root says so; a single point is singular; several prerequisites are listed with "and".
        Assert.Contains("You need nothing first", MemoryDustText.Sheet(Node("socket_2")));
        Assert.Equal("Costs 1 trait point.", MemoryDustText.CostSentence(Node("recall_1")));
        var mark = MemoryDustText.RequiresSentence(Node("attunement"));
        Assert.StartsWith("You need ", mark);
        Assert.Contains(" and KEYSTONE SOCKET III first.", mark);
        foreach (var reqId in Node("attunement").Requires) Assert.Contains(Node(reqId).Name, mark);

        // Every node's sheet carries all four facts, and the permanence line is one plain sentence.
        foreach (var u in MemoryDustTree.Catalog)
        {
            var s = MemoryDustText.Sheet(u);
            Assert.Contains("Costs ", s);
            Assert.Contains("You need ", s);
            Assert.EndsWith(MemoryDustText.Permanence, s);
        }
    }

    // ── The text is for a reader, not a card ──────────────────────────────────────────────────

    [Fact]
    public void test_memory_dust_text_every_node_reads_as_sentences_in_plain_english()
    {
        // The reader plays in English as a second language. Every node's text must be complete
        // sentences (ends in a full stop), must not be all capitals, and must not use the genre
        // abbreviations the UI copy rule forbids.
        var forbidden = new[] { " PT ", " PTS", "AMP ", "CDR", "DPS", "HP ", "XP " };

        foreach (var u in MemoryDustTree.Catalog)
        {
            var text = MemoryDustText.Describe(u);

            Assert.False(string.IsNullOrWhiteSpace(text), $"{u.Id} has no text");
            Assert.EndsWith(".", text);
            Assert.False(text.ToUpperInvariant() == text, $"{u.Id} shouts: {text}");
            foreach (var bad in forbidden)
                Assert.DoesNotContain(bad, " " + text + " ");
            // Descriptions are authored as sentences now, not as cards to be re-cased at draw time.
            Assert.True(char.IsUpper(u.Description[0]), $"{u.Id}'s description does not start a sentence");
        }
    }

    [Fact]
    public void test_memory_dust_text_sentence_case_keeps_names_and_numbers()
    {
        // Arrange: a blurb that names another keystone and the CHARGE pool, with a number in it.
        const string blurb = "YOUR CHARGE POOL HOLDS 20 INSTEAD OF 10 — LODESTONE'S FULL-POOL BAR RISES WITH IT. SKILLS RETURN 15% SLOWER.";

        // Act
        var cased = MemoryDustText.SentenceCase(blurb);
        var continued = MemoryDustText.SentenceCase("DOUBLE DAMAGE. HALF HEALTH.", lowerFirst: true);

        // Assert
        Assert.Equal("Your CHARGE pool holds 20 instead of 10 — LODESTONE's full-pool bar rises with it. Skills return 15% slower.", cased);
        Assert.Equal("double damage. Half health.", continued);
    }

    [Fact]
    public void test_memory_dust_text_effect_words_are_plain_not_the_enum()
    {
        foreach (var e in Enum.GetValues<UnlockEffect>())
        {
            var words = MemoryDustText.EffectInPlainWords(e);
            Assert.DoesNotContain(e.ToString().ToUpperInvariant(), words.ToUpperInvariant());
            Assert.True(words.Split(' ').Length >= 3, $"{e}: '{words}' is a label, not an explanation");
        }
    }
}
