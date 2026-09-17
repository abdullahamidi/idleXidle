using System;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE GEAR SCREEN'S FEEDBACK STATE MACHINE (UI polish brief §22–§38, §48–§52). Every pulse, the GEAR
/// POWER tick and the one-time set-completion notice are decided by <see cref="GearFeedback"/>, which
/// holds no UiKit and does no drawing — so the states a single posed capture cannot photograph (a number
/// PART WAY through its run, a pulse at its peak, a set completing for the second time and staying quiet)
/// are driven and asserted here instead, at t = 0, mid and end.
/// </summary>
/// <remarks>
/// <see cref="UiMotion"/>'s stores are process-wide, so every test clears them and puts Reduced Motion
/// back the way it found it. Parallelisation is off for this assembly (AssemblyInfo.cs).
/// </remarks>
public class gear_feedback_test : IDisposable
{
    private static readonly GearSlot[] FiveSlots =
        { GearSlot.Weapon, GearSlot.Helm, GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots };

    public gear_feedback_test()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
    }

    public void Dispose()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        GC.SuppressFinalize(this);
    }

    private static ItemBaseType TypeFor(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => ItemBaseType.Weapon, GearSlot.Helm => ItemBaseType.Helm,
        GearSlot.Chest => ItemBaseType.Chest, GearSlot.Gloves => ItemBaseType.Gloves,
        GearSlot.Boots => ItemBaseType.Boots, GearSlot.Charm => ItemBaseType.Charm,
        GearSlot.Focus => ItemBaseType.AbilityFocus, _ => ItemBaseType.Ring,
    };

    /// <summary>A plain piece for a slot, of one element — the same shape the Core set tests wear.</summary>
    private static ItemInstance Piece(GearSlot slot, Source? element, int level = 20) => new()
    {
        InstanceId = $"fb-{element}-{slot}", BaseType = TypeFor(slot), Rarity = Rarity.Rare,
        SellValue = 10, ItemLevel = level, Element = element, Family = 0,
    };

    /// <summary>One frame of the host's clock: <see cref="UiMotion.Tick"/> at the fixed step.</summary>
    private static void Frames(int n) { for (var i = 0; i < n; i++) UiMotion.Tick(1f / 60f); }

    // ── PRIMING: the first look is a snapshot, not an event ──────────────────────────────────────────

    [Fact]
    public void test_the_first_look_at_a_dressed_hunter_fires_nothing()
    {
        var hunter = new Hunter();
        foreach (var s in FiveSlots) hunter.Equip(Piece(s, Source.Nature));
        var fb = new GearFeedback();

        fb.Observe(hunter);

        Assert.Equal(0f, fb.SlotPulse(GearSlot.Chest));
        Assert.Equal(0f, fb.PowerEmphasis);
        Assert.Equal(hunter.PowerRating, fb.DisplayedPower);
        Assert.Null(fb.ConsumeCompletedSet());
        Assert.Empty(fb.CompletedSets);
    }

    [Fact]
    public void test_a_quiet_look_takes_the_snapshot_without_firing()
    {
        var hunter = new Hunter();
        var fb = new GearFeedback();
        fb.Observe(hunter);

        // The screen was away — the Forge or the Roster moved the gear. Re-priming, not celebrating.
        hunter.Equip(Piece(GearSlot.Helm, Source.Nature));
        fb.Observe(hunter, quiet: true);

        Assert.Equal(0f, fb.SlotPulse(GearSlot.Helm));
        Assert.Equal(hunter.PowerRating, fb.DisplayedPower);
    }

    // ── THE SLOT PULSE ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_piece_arriving_pulses_its_own_slot_once_and_no_other()
    {
        var hunter = new Hunter();
        var fb = new GearFeedback();
        fb.Observe(hunter);

        hunter.Equip(Piece(GearSlot.Boots, Source.Nature));
        Assert.True(fb.Observe(hunter));

        Assert.Equal(1f, fb.SlotPulse(GearSlot.Boots), 3);
        Assert.Equal(0f, fb.SlotPulse(GearSlot.Helm));

        // A second look with nothing changed must not re-arm it — a pulse plays once per event.
        Frames(5);
        var mid = fb.SlotPulse(GearSlot.Boots);
        Assert.InRange(mid, 0.01f, 0.99f);
        Assert.False(fb.Observe(hunter));
        Assert.Equal(mid, fb.SlotPulse(GearSlot.Boots), 3);

        // And it ends, on its own, inside one Transition.
        Frames((int)Math.Ceiling(UiMotion.Transition * 60f));
        Assert.Equal(0f, fb.SlotPulse(GearSlot.Boots));
    }

    [Fact]
    public void test_taking_a_piece_off_does_not_pulse_the_slot_it_left()
    {
        var hunter = new Hunter();
        hunter.Equip(Piece(GearSlot.Chest, Source.Nature));
        var fb = new GearFeedback();
        fb.Observe(hunter);

        hunter.Unequip(GearSlot.Chest);
        fb.Observe(hunter);

        Assert.Equal(0f, fb.SlotPulse(GearSlot.Chest));
    }

    // ── THE GEAR POWER TICK, at t = 0, mid and end (§36) ─────────────────────────────────────────────

    [Fact]
    public void test_gear_power_runs_from_the_old_number_to_the_new_over_one_transition()
    {
        var hunter = new Hunter();
        var fb = new GearFeedback();
        fb.Observe(hunter);
        var before = hunter.PowerRating;

        hunter.Equip(Piece(GearSlot.Weapon, Source.Nature, level: 60));
        var after = hunter.PowerRating;
        Assert.True(after > before, "the fixture piece must actually raise GEAR POWER");
        fb.Observe(hunter);

        // t = 0: the readout still says what it said, and the digits are at full emphasis.
        Assert.Equal(before, fb.DisplayedPower);
        Assert.Equal(1f, fb.PowerEmphasis, 3);

        // mid: strictly on the way there, never past either end.
        Frames((int)Math.Round(UiMotion.Transition * 60f / 2f));
        Assert.InRange(fb.DisplayedPower, before + 1, after - 1);
        Assert.InRange(fb.PowerEmphasis, 0.01f, 0.99f);

        // end: the true value, and the emphasis gone.
        Frames((int)Math.Ceiling(UiMotion.Transition * 60f));
        Assert.Equal(after, fb.DisplayedPower);
        Assert.Equal(0f, fb.PowerEmphasis);
    }

    [Fact]
    public void test_a_second_change_mid_run_continues_from_the_number_on_screen()
    {
        var hunter = new Hunter();
        var fb = new GearFeedback();
        fb.Observe(hunter);
        var before = hunter.PowerRating;

        hunter.Equip(Piece(GearSlot.Weapon, Source.Nature, level: 60));
        fb.Observe(hunter);
        Frames(5);
        var onScreen = fb.DisplayedPower;

        hunter.Equip(Piece(GearSlot.Helm, Source.Nature, level: 60));
        fb.Observe(hunter);

        // It resumes from what the player was reading, not from the value two changes ago.
        Assert.Equal(onScreen, fb.DisplayedPower);
        Assert.True(onScreen > before);
        Frames((int)Math.Ceiling(UiMotion.Transition * 60f));
        Assert.Equal(hunter.PowerRating, fb.DisplayedPower);
    }

    [Fact]
    public void test_reduced_motion_shows_the_true_number_at_once_and_the_same_end_state()
    {
        var hunter = new Hunter();
        var fb = new GearFeedback();
        fb.Observe(hunter);

        UiMotion.Reduced = true;
        hunter.Equip(Piece(GearSlot.Weapon, Source.Nature, level: 60));
        fb.Observe(hunter);

        // No run at all — the state change is instant, and it is the SAME state the run would have ended on.
        Assert.Equal(hunter.PowerRating, fb.DisplayedPower);
        Frames((int)Math.Ceiling(UiMotion.Transition * 60f));
        Assert.Equal(hunter.PowerRating, fb.DisplayedPower);
    }

    // ── THE SET LADDER'S RUNGS (§51) ─────────────────────────────────────────────────────────────────

    [Fact]
    public void test_each_rung_reveals_the_moment_it_is_reached_and_never_again()
    {
        var hunter = new Hunter();
        var fb = new GearFeedback();
        fb.Observe(hunter);

        foreach (var (slot, i) in FiveSlots.Select((s, i) => (s, i + 1)))
        {
            hunter.Equip(Piece(slot, Source.Nature));
            fb.Observe(hunter);
            foreach (var rung in ElementSets.Rungs)
                Assert.Equal(rung == i ? 1f : 0f, fb.RungPulse(Source.Nature, rung), 3);
            Frames((int)Math.Ceiling(UiMotion.Reward * 60f));
        }

        // Every rung is reached; another look re-arms nothing.
        fb.Observe(hunter);
        foreach (var rung in ElementSets.Rungs) Assert.Equal(0f, fb.RungPulse(Source.Nature, rung));
    }

    [Fact]
    public void test_the_reveal_lasts_one_reward_and_ends_on_its_own()
    {
        var hunter = new Hunter();
        foreach (var s in FiveSlots.Take(4)) hunter.Equip(Piece(s, Source.Nature));
        var fb = new GearFeedback();
        fb.Observe(hunter);

        hunter.Equip(Piece(FiveSlots[4], Source.Nature));
        fb.Observe(hunter);

        Assert.Equal(1f, fb.RungPulse(Source.Nature, 5), 3);
        Frames((int)Math.Round(UiMotion.Reward * 60f / 2f));
        Assert.InRange(fb.RungPulse(Source.Nature, 5), 0.01f, 0.99f);
        Frames((int)Math.Ceiling(UiMotion.Reward * 60f));
        Assert.Equal(0f, fb.RungPulse(Source.Nature, 5));
    }

    // ── THE FIRST FIVE-PIECE COMPLETION, once per set, ever (§52) ────────────────────────────────────

    /// <summary>
    /// THE FIRST COMPLETION NAMES THE SET AND ITS CAPSTONE — in the LETTER the host posts for it.
    /// </summary>
    /// <remarks>
    /// The screen used to hand back the two lines itself, for a toast. It hands back the EVENT now —
    /// the set's name — and <see cref="DispatchCopy"/> renders the words from the set, so the copy is
    /// asserted where it is written rather than in two places that could drift. The invariant this
    /// test has always held is unchanged: the completion is reported exactly once, and reading it
    /// clears it.
    /// </remarks>
    [Fact]
    public void test_the_first_completion_names_the_set_and_its_capstone_in_the_letter_it_posts()
    {
        var hunter = new Hunter();
        foreach (var s in FiveSlots.Take(4)) hunter.Equip(Piece(s, Source.Nature));
        var fb = new GearFeedback();
        fb.Observe(hunter);

        hunter.Equip(Piece(FiveSlots[4], Source.Nature));
        fb.Observe(hunter);

        var completed = fb.ConsumeCompletedSet();
        Assert.Equal(Source.Nature.ToString(), completed);
        Assert.Contains(Source.Nature.ToString(), fb.CompletedSets);

        // What the player reads, built from that name the way the host builds it.
        var letter = Dispatches.Set(Source.Nature, 1_000);
        Assert.Equal(DispatchKeys.SetComplete(Source.Nature), letter.Key);
        Assert.Equal(ElementSets.Name(Source.Nature) + " COMPLETE", DispatchCopy.Headline(letter));
        // The body names the capstone and quotes its rung's own sentence, as prose — never the
        // retired toast's "OVERGROWTH ACTIVE".
        var body = DispatchCopy.Body(letter);
        Assert.Contains(ElementSets.CapstoneName(Source.Nature), body, StringComparison.Ordinal);
        Assert.Contains(ElementSets.TiersOf(Source.Nature)[^1].Line, body, StringComparison.Ordinal);

        // Read once, gone — the host sounds the reward on it, and a second read must not sound it twice.
        Assert.Null(fb.ConsumeCompletedSet());
    }

    [Fact]
    public void test_a_set_already_completed_never_announces_itself_again()
    {
        var hunter = new Hunter();
        foreach (var s in FiveSlots.Take(4)) hunter.Equip(Piece(s, Source.Nature));
        var fb = new GearFeedback();
        fb.Observe(hunter);

        hunter.Equip(Piece(FiveSlots[4], Source.Nature));
        fb.Observe(hunter);
        Assert.Equal(Source.Nature.ToString(), fb.ConsumeCompletedSet());   // the host wrote it to the save

        // Off and on again — the same fifth piece, the same set. The rung still reveals; the toast does not.
        hunter.Unequip(FiveSlots[4]);
        fb.Observe(hunter);
        Frames((int)Math.Ceiling(UiMotion.Reward * 60f));
        hunter.Equip(Piece(FiveSlots[4], Source.Nature));
        fb.Observe(hunter);

        Assert.Equal(1f, fb.RungPulse(Source.Nature, 5), 3);
        Assert.Null(fb.ConsumeCompletedSet());
        Assert.Single(fb.CompletedSets);
    }

    [Fact]
    public void test_a_set_completed_in_an_earlier_session_stays_quiet_after_a_restore()
    {
        var hunter = new Hunter();
        foreach (var s in FiveSlots.Take(4)) hunter.Equip(Piece(s, Source.Nature));
        var fb = new GearFeedback();
        fb.RestoreCompletedSets(new[] { Source.Nature.ToString() });   // what the save held
        fb.Observe(hunter);

        hunter.Equip(Piece(FiveSlots[4], Source.Nature));
        fb.Observe(hunter);

        Assert.Null(fb.ConsumeCompletedSet());
        Assert.Single(fb.CompletedSets);
    }

    [Fact]
    public void test_reduced_motion_still_completes_the_set_and_still_reports_it()
    {
        UiMotion.Reduced = true;
        var hunter = new Hunter();
        foreach (var s in FiveSlots.Take(4)) hunter.Equip(Piece(s, Source.Nature));
        var fb = new GearFeedback();
        fb.Observe(hunter);

        hunter.Equip(Piece(FiveSlots[4], Source.Nature));
        fb.Observe(hunter);

        Assert.Equal(Source.Nature.ToString(), fb.ConsumeCompletedSet());
    }

    // ── THE SOUND CUE: raised at the moment, read once, cleared (§86 — the host owns audio) ──────────

    [Fact]
    public void test_a_cue_is_read_once_and_then_gone()
    {
        var fb = new GearFeedback();
        Assert.Null(fb.ConsumeCue());
        fb.Cue("sfx_equip");
        Assert.Equal("sfx_equip", fb.ConsumeCue());
        Assert.Null(fb.ConsumeCue());
    }
}
