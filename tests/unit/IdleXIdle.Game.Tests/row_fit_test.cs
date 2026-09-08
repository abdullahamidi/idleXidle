using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A ROW'S LABEL AND ITS FIGURE MAY NEVER SHARE A PIXEL — the ladder that keeps them apart.
/// </summary>
/// <remarks>
/// <para>
/// The Gear inspector drew a row's label at the margin and its value right-aligned at the column's
/// edge, reserving nothing: at UI SCALE 150 % a built-in row's "CRITICAL CHANCE  ·  BUILT IN" ran
/// straight through its own "+6.0%". The fix is a layout law — reserve the value, measure what is
/// left, reduce the label into that — and the reduction is <see cref="UiKit.TryFitLabel"/>, which the
/// Forge's compare row already used.
/// </para>
/// <para>
/// These tests drive the ladder over a SYNTHETIC measure rather than the shipped font: what can
/// regress here is the ORDER of the reduction and the promise that a fitted label really fits, and
/// both are geometry. The pixels themselves are proved by the 100/125/150 captures, which is the only
/// place the real font and the real column width meet.
/// </para>
/// </remarks>
public class row_fit_test
{
    /// <summary>A measure with no font behind it: every character is one unit wide.</summary>
    private static readonly Func<string, int> PerChar = s => s.Length;

    private static readonly string[] StatWords =
        Enum.GetValues<AffixStat>().Select(ItemModifiers.Word).Distinct().ToArray();

    [Fact]
    public void test_a_label_that_fits_is_left_exactly_as_it_is()
    {
        Assert.True(UiKit.TryFitLabel("DAMAGE", room: 6, PerChar, out var fitted));
        Assert.Equal("DAMAGE", fitted);

        Assert.True(UiKit.TryFitLabel("", room: 0, PerChar, out var empty));
        Assert.Equal("", empty);
    }

    [Fact]
    public void test_a_built_in_row_gives_up_its_caption_before_any_of_its_stat_word()
    {
        var label = "CRITICAL CHANCE" + UiKit.BuiltInSuffix;

        // Room for the stat word but not the caption: the caption goes, whole, and the stat word is intact.
        Assert.True(UiKit.TryFitLabel(label, room: "CRITICAL CHANCE".Length, PerChar, out var fitted));
        Assert.Equal("CRITICAL CHANCE", fitted);
        Assert.DoesNotContain(ItemPresentation.BuiltInCaption, fitted, StringComparison.Ordinal);

        // Room for everything: nothing is given up.
        Assert.True(UiKit.TryFitLabel(label, room: label.Length, PerChar, out var whole));
        Assert.Equal(label, whole);
    }

    [Fact]
    public void test_the_ladder_refuses_rather_than_cutting_the_stat_word()
    {
        // One character short of the bare stat word — there is nothing left to give up, so the caller
        // is told NO instead of being handed "CRITICAL CHAN…". The Gear inspector answers that with a
        // second line; the Forge's fixed-height compare row answers it with a cut, and only the caller
        // knows which it can afford.
        var label = "CRITICAL CHANCE" + UiKit.BuiltInSuffix;
        Assert.False(UiKit.TryFitLabel(label, room: "CRITICAL CHANCE".Length - 1, PerChar, out _));
        Assert.False(UiKit.TryFitLabel("SKILL RATE", room: 3, PerChar, out _));

        // A column with no room at all is a refusal, not a crash or an empty pass.
        Assert.False(UiKit.TryFitLabel("DAMAGE", room: 0, PerChar, out _));
        Assert.False(UiKit.TryFitLabel("DAMAGE", room: -20, PerChar, out _));
    }

    [Fact]
    public void test_a_fitted_label_always_fits_the_room_it_was_given()
    {
        // THE PROMISE THE ROW RELIES ON. Every live row label the Gear inspector composes, against every
        // width from nothing to plenty: a TRUE answer must measure inside the room, or the value it was
        // measured against is being overprinted — which is the bug this ladder exists to end.
        var labels = new List<string>();
        foreach (var word in StatWords)
        {
            labels.Add(word);
            labels.Add(word + UiKit.BuiltInSuffix);
        }
        labels.AddRange(new[] { "ITEM POWER", "SET GEMS", "FORTUNE GEM", "ITEM POWER" + UiKit.BuiltInSuffix });

        var fits = 0;
        foreach (var label in labels)
        for (var room = 0; room <= label.Length + 4; room++)
        {
            if (!UiKit.TryFitLabel(label, room, PerChar, out var fitted)) continue;
            fits++;
            Assert.True(PerChar(fitted) <= room, $"\"{fitted}\" was accepted into room {room} but measures {PerChar(fitted)}");
            // What comes back is the label or the label minus its caption — never a third thing.
            Assert.True(fitted == label || label == fitted + UiKit.BuiltInSuffix,
                        $"\"{label}\" reduced to \"{fitted}\" — the ladder invented a label");
        }
        Assert.True(fits > 100, $"only {fits} widths fitted — the sweep is not exercising the ladder");
    }

    [Fact]
    public void test_every_built_in_row_in_the_catalogue_keeps_its_stat_word_on_a_narrow_column()
    {
        // The real labels, from the real rows: a Common piece of every wearable type, at the narrowest
        // room a stat word can survive in. The caption is the only thing that may be spent.
        foreach (var type in new[]
                 {
                     ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Helm,
                     ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
                 })
        {
            var item = new ItemInstance
            {
                InstanceId = $"row_{type}", BaseType = type, Rarity = Rarity.Common,
                SellValue = 10, ItemLevel = 20, Family = type == ItemBaseType.Weapon ? 0 : null,
            };
            foreach (var row in ItemPresentation.Rows(item).Where(r => r.Kind == ItemRowKind.BuiltIn))
            {
                var label = row.Label + UiKit.BuiltInSuffix;
                Assert.True(UiKit.TryFitLabel(label, row.Label.Length, PerChar, out var fitted));
                Assert.Equal(row.Label, fitted);
                Assert.Contains(fitted, StatWords);
            }
        }
    }
}
