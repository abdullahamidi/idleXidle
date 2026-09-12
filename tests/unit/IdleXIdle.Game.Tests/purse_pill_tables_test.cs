using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using IdleXIdle.Game;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// EVERY PER-PILL TABLE HAS ONE ENTRY PER PILL. A short one is a crash, not a missing label.
/// </summary>
/// <remarks>
/// <para>
/// <b>The crash this exists to stop coming back.</b> The currency row grew from three pills to six when
/// ESSENCE, CORE and CRYSTAL arrived, and <c>Game1.PillGainFloor</c> stayed three entries long.
/// <c>TickChromeMotion</c> indexes it by the pill, inside Update, so the first time an account's Essence,
/// Core or Crystal rose while the row was past its warm-up, <c>PillGainFloor[3]</c> threw
/// <c>IndexOutOfRangeException</c> and the process died — mid-Update, with no handler anywhere above it.
/// </para>
/// <para>
/// <b>Why nothing caught it.</b> The indices exercised in practice were 0, 1 and 2: Gleam, Dust and
/// Scrap are paid from wave one. The other three only move on a salvage, and nothing in the harness ever
/// reached one — the boot soak's fresh champion died on wave 5 before a chest, and a capture fixture
/// installs its materials during the pills' warm-up window on purpose, so no screenshot could reach it
/// either. It surfaced the day the first boss became winnable and a soak walked far enough to salvage.
/// That is the failure mode this project keeps meeting: code that is green because nothing runs it.
/// </para>
/// <para>
/// So this test asserts the SHAPE rather than the values — a length relation the compiler will not check
/// and no screenshot can show, for every table the row indexes.
/// </para>
/// </remarks>
public class PursePillTablesTest
{
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly ITestOutputHelper _out;

    public PursePillTablesTest(ITestOutputHelper output) => _out = output;

    /// <summary>Every static table on Game1 that the pill row indexes by pill.</summary>
    /// <remarks>
    /// Named rather than discovered, so ADDING a table without adding it here is a visible omission
    /// rather than a silent gap — and so the test says which table is short when it fails.
    /// </remarks>
    public static TheoryData<string> Tables() => new()
    {
        "PillGainFloor", "PurseOrder", "PurseIcon", "PurseInk", "PurseSource",
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void test_every_per_pill_table_is_as_long_as_the_pill_row(string name)
    {
        var count = (int)typeof(Game1).GetField("PurseCount", Statics)!.GetValue(null)!;
        var field = typeof(Game1).GetField(name, Statics);
        Assert.NotNull(field);
        var table = (ICollection)field!.GetValue(null)!;
        _out.WriteLine($"{name}: {table.Count} entries against PurseCount {count}");
        Assert.Equal(count, table.Count);
    }

    /// <summary>
    /// THE ORDER TABLE NAMES EVERY PILL EXACTLY ONCE — it is a permutation, so the row can neither drop
    /// a currency nor draw one twice.
    /// </summary>
    [Fact]
    public void test_the_pill_order_is_a_permutation_of_every_pill()
    {
        var count = (int)typeof(Game1).GetField("PurseCount", Statics)!.GetValue(null)!;
        var order = (int[])typeof(Game1).GetField("PurseOrder", Statics)!.GetValue(null)!;
        Assert.Equal(Enumerable.Range(0, count).ToHashSet(), order.ToHashSet());
        Assert.Equal(order.Length, order.Distinct().Count());
    }

    /// <summary>
    /// AND EVERY FLOOR IS A REAL ONE. A zero floor would announce every rounding wobble; a floor larger
    /// than the drop it guards would mean the pill never speaks at all — which is why the three rarest
    /// materials are 1 and not the Scrap pill's 25.
    /// </summary>
    [Fact]
    public void test_every_pill_floor_is_at_least_one()
    {
        var floors = (long[])typeof(Game1).GetField("PillGainFloor", Statics)!.GetValue(null)!;
        Assert.All(floors, f => Assert.True(f >= 1, $"a pill floor of {f} announces a gain of nothing"));
    }
}
