using System.Linq;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using Xunit;

namespace ResonanceHunter.Core.Tests.Characters;

/// <summary>
/// The ROSTER grid: one column per class, the FIRST of the class above the SECOND.
/// </summary>
/// <remarks>
/// Playtest (2026-08-26): "characters of the same class should sit one under the other." The screen
/// used to draw the catalogue five to a row, which put the pairs on the screen and nowhere near each
/// other. The arrangement is data in <see cref="CharacterRoster.Grid"/> so the screen only walks it,
/// and these hold the data to the sentence.
/// </remarks>
public class RosterGridTest
{
    [Fact]
    public void test_roster_grid_stacks_each_class_in_one_column_first_above_second()
    {
        // Arrange
        var grid = CharacterRoster.Grid;

        // Act / Assert — every column holds one class, and its two cells are the two tiers in order.
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var cls = CharacterRoster.ClassColumns[column];
            var cells = grid.Where(cell => cell.Column == column).OrderBy(cell => cell.Row).ToList();

            Assert.Equal(2, cells.Count);
            Assert.All(cells, cell => Assert.Equal(cls, cell.Character.Class));
            Assert.Equal(0, cells[0].Row);
            Assert.Equal(ClassTier.First, cells[0].Character.Tier);
            Assert.Equal(1, cells[1].Row);
            Assert.Equal(ClassTier.Second, cells[1].Character.Tier);
        }
    }

    [Fact]
    public void test_roster_grid_holds_every_champion_exactly_once()
    {
        var ids = CharacterRoster.Grid.Select(cell => cell.Character.Id).ToList();

        Assert.Equal(CharacterRoster.All.Count, ids.Count);
        Assert.Equal(CharacterRoster.All.Select(c => c.Id).OrderBy(id => id), ids.OrderBy(id => id));
    }

    [Fact]
    public void test_roster_grid_walks_row_by_row_left_to_right()
    {
        // The screen phases each card's breathing by its index in this walk, and the click test walks
        // it too — so the walk has to be the reading order: the top row, then the bottom row.
        var expected = CharacterRoster.Grid.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList();
        Assert.Equal(expected, CharacterRoster.Grid);
    }

    [Fact]
    public void test_roster_grid_columns_keep_the_order_the_top_row_always_had()
    {
        // WANDERER, WARDEN, RANGER, MYSTIC, BULWARK — the order the catalogue's five FIRSTs stand in,
        // so the class columns did not move under a player who had learned where each card was.
        var firstsInCatalogueOrder = CharacterRoster.All
            .Where(c => c.Tier == ClassTier.First)
            .Select(c => c.Class)
            .ToList();
        Assert.Equal(firstsInCatalogueOrder, CharacterRoster.ClassColumns);
        Assert.Equal(ItemClass.Wanderer, CharacterRoster.ClassColumns[0]);   // the starter's column is first
    }

    [Fact]
    public void test_roster_grid_names_every_class_exactly_once()
    {
        var classes = ItemClasses.All.Select(d => d.Class).OrderBy(c => c);
        Assert.Equal(classes, CharacterRoster.ClassColumns.OrderBy(c => c));
    }

    [Fact]
    public void test_roster_by_class_lists_the_first_before_the_second()
    {
        foreach (var cls in CharacterRoster.ClassColumns)
        {
            var pair = CharacterRoster.ByClass(cls);
            Assert.Equal(2, pair.Count);
            Assert.Equal(ClassTier.First, pair[0].Tier);
            Assert.Equal(ClassTier.Second, pair[1].Tier);
        }
        Assert.Equal(new[] { "unbroken", "thornwall" }, CharacterRoster.ByClass(ItemClass.Bulwark).Select(c => c.Id));
    }
}
