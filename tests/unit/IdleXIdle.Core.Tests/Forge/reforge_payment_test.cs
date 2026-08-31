using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using Xunit;

// Tests.Forging, not Tests.Forge — see ReforgeTests for why the namespace name matters.
namespace IdleXIdle.Core.Tests.Forging;

/// <summary>
/// Who pays for a re-roll: the REFORGE CHART if you hold one, else the material — and never both.
/// </summary>
/// <remarks>
/// Playtest (2026-08-23): <i>"RE-ROLL said 110 Core; I had 139; I pressed many times and only on the
/// 5th or 6th was 110 deducted."</i> Not a bug in the arithmetic — the first presses were each paid by
/// a REFORGE CHART the player did not know they held, while the price line kept saying 110 CORE. The
/// payment decision lived in the screen, untestable; it lives in <see cref="Reforge.PayWith"/> now, and
/// these pin what it must do so the screen can simply REPORT it.
/// </remarks>
public class ReforgePaymentTest
{
    private static readonly ReforgeTuning T = ReforgeTuning.Default;

    [Fact]
    public void test_a_reforge_with_a_chart_spends_the_chart_and_no_material()
    {
        var h = new Hunter();
        h.AddCharter(Charter.Reforge, 2);
        h.AddMaterial(Material.Core, 139);

        var cost = T.EnchantCostFor(Rarity.Epic);
        Assert.True(Reforge.CanPay(h, Material.Core, cost));

        var pay = Reforge.PayWith(h, Material.Core, cost);

        Assert.True(pay.Paid);
        Assert.True(pay.UsedChart);
        Assert.Equal(0, pay.MaterialSpent);
        Assert.Equal(1, h.CharterCount(Charter.Reforge));
        Assert.Equal(139, h.MaterialOf(Material.Core));   // the 139 is exactly where it was
    }

    [Fact]
    public void test_a_reforge_without_a_chart_spends_the_material()
    {
        var h = new Hunter();
        h.AddMaterial(Material.Core, 139);

        var cost = T.EnchantCostFor(Rarity.Epic);
        var pay = Reforge.PayWith(h, Material.Core, cost);

        Assert.True(pay.Paid);
        Assert.False(pay.UsedChart);
        Assert.Equal(cost, pay.MaterialSpent);
        Assert.Equal(139 - cost, h.MaterialOf(Material.Core));
        Assert.Equal(0, h.CharterCount(Charter.Reforge));
    }

    [Fact]
    public void test_a_chart_holder_with_no_material_can_still_pay()
    {
        // The screen's old enable check read only the material, so this exact player saw a greyed
        // button for a press that would have been free.
        var h = new Hunter();
        h.AddCharter(Charter.Reforge, 1);

        Assert.True(Reforge.CanPay(h, Material.Crystal, T.EnchantCostFor(Rarity.Legendary)));
        var pay = Reforge.PayWith(h, Material.Crystal, T.EnchantCostFor(Rarity.Legendary));

        Assert.True(pay.Paid);
        Assert.True(pay.UsedChart);
        Assert.Equal(0, h.MaterialOf(Material.Crystal));
    }

    [Fact]
    public void test_with_neither_chart_nor_material_nothing_is_taken()
    {
        var h = new Hunter();
        h.AddMaterial(Material.Core, 40);   // short of 110

        var cost = T.EnchantCostFor(Rarity.Epic);
        Assert.False(Reforge.CanPay(h, Material.Core, cost));

        var pay = Reforge.PayWith(h, Material.Core, cost);

        Assert.False(pay.Paid);
        Assert.False(pay.UsedChart);
        Assert.Equal(0, pay.MaterialSpent);
        Assert.Equal(40, h.MaterialOf(Material.Core));
    }

    [Fact]
    public void test_the_chart_is_preferred_over_the_material_when_both_are_held()
    {
        // Unchanged from the old screen rule, and pinned so a refactor cannot quietly flip it: the
        // chart is the rarer thing, but it exists to be spent on exactly this, and spending the
        // material first would leave the paper sitting unused forever.
        var h = new Hunter();
        h.AddCharter(Charter.Reforge, 1);
        h.AddMaterial(Material.Core, 500);

        var pay = Reforge.PayWith(h, Material.Core, T.EnchantCostFor(Rarity.Rare));

        Assert.True(pay.UsedChart);
        Assert.Equal(500, h.MaterialOf(Material.Core));
        Assert.Equal(0, h.CharterCount(Charter.Reforge));
    }

    [Fact]
    public void test_legendaries_re_roll_with_crystal_and_everything_else_with_core()
    {
        // The rule Material.cs documents, now in code the screen reads rather than a private copy.
        Assert.Equal(Material.Crystal, Reforge.EnchantMaterial(Rarity.Legendary));
        Assert.Equal(Material.Core, Reforge.EnchantMaterial(Rarity.Epic));
        Assert.Equal(Material.Core, Reforge.EnchantMaterial(Rarity.Rare));
    }
}
