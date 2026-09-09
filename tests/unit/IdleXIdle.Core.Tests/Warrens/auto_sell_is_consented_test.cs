using System;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Warrens;

/// <summary>
/// THE FACILITY UNLOCKS THE SALE. THE PLAYER TURNS IT ON.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-09-09: <i>"It sold an item obtained from a chest even though the item filter wasn't
/// active."</i> Both halves were true, which is why the report reads as a contradiction. The CHEST
/// FILTER — the only thing the game calls a filter in a place the player can reach — keeps or drops
/// whole CHESTS and has never sold anything; its default is keep-everything and it pays Scrap for
/// what it drops. The sale came from SCAVENGER RUNS, a Warren facility whose level-2 upgrade is
/// bought for its production and carried auto-sell as an unannounced rider, with no off switch
/// anywhere in the game and no line at the moment of sale.
/// </para>
/// <para>
/// This file pins the two halves that were confused: the chest filter cannot sell, and the facility
/// level only says what is POSSIBLE. The consent itself lives on the save (<c>AutoSellOn</c>, default
/// false even for a save long past the level) and is read by the host, which is where the tests for
/// the switch's wiring live.
/// </para>
/// </remarks>
public class auto_sell_is_consented_test
{
    private static Warren At(FacilityKind kind, int level)
    {
        var w = new Warren();
        w.Restore(1, 0, new System.Collections.Generic.Dictionary<FacilityKind, int> { [kind] = level });
        return w;
    }

    [Fact]
    public void test_a_fresh_save_has_not_agreed_to_anything()
    {
        // The field a returning player inherits. Absent on every save written before the switch
        // existed, and an absent bool reads false — which is the answer this change wants.
        Assert.False(new SaveGame().AutoSellOn);
    }

    [Fact]
    public void test_the_facility_note_offers_the_capability_rather_than_announcing_the_behaviour()
    {
        var t = new Warren().Tuning;
        var atCommon = At(FacilityKind.ScavengerRuns, t.AutoSellCommonLevel);
        var note = WarrenAutomation.NoteFor(atCommon, FacilityKind.ScavengerRuns, t.AutoSellCommonLevel);

        // It must not promise a sale the player has not agreed to...
        Assert.DoesNotContain("WILL SELL", note, StringComparison.Ordinal);
        // ...and it must say where the switch is, or the capability is as invisible as the rider was.
        Assert.Contains("CHEST FILTER", note, StringComparison.Ordinal);
        Assert.Contains("CAN SELL", note, StringComparison.Ordinal);

        var active = WarrenAutomation.ActiveNoteFor(atCommon, FacilityKind.ScavengerRuns);
        Assert.Contains("CAN SELL", active, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_uncommon_rung_still_promises_that_rare_is_kept()
    {
        var t = new Warren().Tuning;
        var note = WarrenAutomation.NoteFor(At(FacilityKind.ScavengerRuns, t.AutoSellUncommonLevel),
                                            FacilityKind.ScavengerRuns, t.AutoSellUncommonLevel);
        Assert.Contains("RARE AND BETTER ARE ALWAYS KEPT", note, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_chest_filter_is_not_the_thing_that_sells()
    {
        // The system the player looked at. Its default keeps every chest, and its refusal pays Scrap
        // and lands no item at all — so it cannot be the thing that sold one, at any setting.
        var chest = new Chest { Rarity = Rarity.Common, Tier = 1 };
        Assert.True(Chests.PassesKeepFilter(chest, 0, System.Array.Empty<ItemBaseType>()));

        var dropped = new Chest { Rarity = Rarity.Common, Tier = 1 };
        Assert.False(Chests.PassesKeepFilter(dropped, minTier: 5, System.Array.Empty<ItemBaseType>()));
        Assert.True(Chests.FilterCompensation(dropped) > 0, "a refused chest pays materials, never Gleam");
    }
}
