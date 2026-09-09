using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// NEVER NAME A THING BEFORE THE PLAYER HAS MET IT.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-09-09, and the player asked for it as a RULE: <i>"The tutorial mentions 'crystals',
/// yet crystals haven't been introduced previously. We must NEVER mention something before
/// introducing it; that should be a rule."</i>
/// </para>
/// <para>
/// So it is one, and this is where it lives. The ledger below is every term the onboarding is allowed
/// to use and the card that introduces it — and a term with no ledger entry may not appear in a tour
/// card at all. The offenders that produced the complaint are recorded in the ledger's comments: a
/// Crystal has no currency pill anywhere (the three pills are Scrap, Memory Dust and Gleam; Essence,
/// Core and Crystal live on the Forge, which is normally still shut when the TRAINING tour runs), and
/// "mastery points" appeared on the intro's sixth card while the MASTERY screen was locked behind
/// earning three of them.
/// </para>
/// <para>
/// <b>This test is deliberately narrow.</b> It reads the TOURS, which are the modal first-run
/// explanation and the only place the player is a guaranteed beginner. Rungs, hints and reveal notices
/// arrive on the screen they are about, at a moment the player produced, and are covered by their own
/// tests.
/// </para>
/// </remarks>
public class onboarding_vocabulary_test
{
    private readonly ITestOutputHelper _out;

    public onboarding_vocabulary_test(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// Every term that must be introduced before it is used, and the card that introduces it.
    /// </summary>
    /// <remarks>
    /// A null introducer means nothing in the tours introduces it and nothing may say it: each of
    /// these named a system, a currency or a control the player had no way to have met, and a
    /// three-sentence card cannot introduce any of them properly — they are taught where they live.
    /// A named introducer is an exemption for exactly one card, and the test below checks that card
    /// still says the word, so a row can never quietly become a blanket licence.
    /// </remarks>
    private static readonly (string Term, TourTarget? IntroducedBy)[] Ledger =
    {
        // Nothing introduces these, and a three-sentence card cannot: they are taught where they live.
        ("CRYSTAL", null),        // no pill; the Forge is shut when the TRAINING tour runs at wave 3
        ("ERRAND", null),         // named a thing with no referent anywhere in the game
        ("OPEN ALL", null),       // the button reads OPEN THE CHEST on the guaranteed one-chest state
        ("CHART", null),          // arrived from nowhere on the Forge tour
        ("DESCENT", null),        // undefined vocabulary on the intro's third card
        ("TRAIT POINT", null),    // the tree that spent them is deleted
        ("RESPEC", null),         // the screens say TAKE EVERY POINT BACK

        // ...and this one HAS an introducer: the card that points at the MASTERY tile is where the
        // player is told the tree exists and what earns the way in, and it reads that sentence
        // straight from Unlocks.Requirement so the card and the gate cannot disagree. Named anywhere
        // EARLIER it is a forward reference, which is what the intro's sixth card was doing with it.
        ("MASTERY POINT", TourTarget.MasteryTile),
    };

    /// <summary>Every tour card in the game, screen by screen, plus the gem tour's two.</summary>
    private static IEnumerable<(string Screen, TourStep Step)> AllCards()
    {
        foreach (var screen in Enum.GetValues<Activity>())
            foreach (var step in Onboarding.TourFor(screen))
                yield return (screen.ToString(), step);
        foreach (var free in new[] { false, true })
            foreach (var step in Onboarding.GemTourFor(free))
                yield return ($"Gem(free:{free})", step);
    }

    [Fact]
    public void test_no_tour_card_names_a_thing_the_player_has_not_met()
    {
        var offences = new List<string>();
        foreach (var (screen, step) in AllCards())
        foreach (var (term, introducer) in Ledger)
        {
            if (introducer == step.Target) continue;   // this is the card that introduces it
            if (step.Body.Contains(term, StringComparison.OrdinalIgnoreCase)
                || step.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                offences.Add($"{screen}/{step.Target} says \"{term}\": {step.Body}");
        }

        foreach (var o in offences) _out.WriteLine(o);
        Assert.True(offences.Count == 0,
                    "a tour card names something the player has no way to have met:\n" + string.Join("\n", offences));
    }

    [Fact]
    public void test_a_ledgered_term_is_still_introduced_by_the_card_that_claims_to()
    {
        // THE LEDGER IS ONLY WORTH SOMETHING IF ITS ENTRIES ARE TRUE. An introducer that stopped
        // saying the word would silently turn its ledger row into a blanket exemption — the term
        // banned nowhere and taught nowhere, which is worse than the bug this file exists for.
        foreach (var (term, introducer) in Ledger)
        {
            if (introducer is not { } target) continue;
            var card = AllCards().Select(c => c.Step).FirstOrDefault(s => s.Target == target);
            Assert.True(card.Body.Contains(term, StringComparison.OrdinalIgnoreCase),
                        $"{target} is the ledger's introducer for \"{term}\" and no longer says it");
        }
    }

    [Fact]
    public void test_the_gleam_card_is_about_gleam_and_says_so_once()
    {
        // THE FIRST REPORTED BUG. The card is titled GLEAM and its light framed all three currency
        // pills. The copy is held to naming ONE resource, and the rectangle it lights is now read from
        // the row the host draws (Game1.GleamPillRect) rather than hand-written — the test for the
        // rectangle is that there is no second copy of the arithmetic to drift.
        var card = Onboarding.TourFor(Activity.Hunt).Single(s => s.Target == TourTarget.CurrencyPills);

        Assert.Contains("Gleam", card.Body, StringComparison.Ordinal);
        foreach (var other in new[] { "SCRAP", "MEMORY DUST", "ESSENCE", "CORE" })
            Assert.DoesNotContain(other, card.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_the_vault_card_does_not_name_a_button_the_first_visit_cannot_show()
    {
        // THE SECOND REPORTED BUG. The tour's first visit is guaranteed to be the ONE-chest state (the
        // welcome gift), where the Vault draws OPEN THE CHEST — so a card promising OPEN ALL described
        // a control that was not on the screen.
        var card = Onboarding.TourFor(Activity.Vault).Single(s => s.Target == TourTarget.VaultButtons);

        Assert.DoesNotContain("OPEN ALL", card.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chest", card.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_the_player_is_told_that_a_wave_is_not_the_last_one_again()
    {
        // ITEM 7: "each wave is randomized, but we never mention this fact." Forty tour cards, eight
        // rungs, seven hints and eleven reveal lines said nothing about it. It belongs on the card
        // that describes what a wave IS.
        var card = Onboarding.TourFor(Activity.Hunt).Single(s => s.Target == TourTarget.Enemies);
        Assert.Contains("no two waves", card.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_the_deed_rung_waits_for_the_deed_and_not_for_the_drop()
    {
        // "We need to force the player to interact with certain elements... require the player to
        // equip the gem so they learn the mechanic." Holding a gem must NOT satisfy it; setting one
        // must. And it must stay silent while there is no gem to set, or it is a lesson about nothing.
        var noGem = new TutorialFacts(WavesCleared: 20, DeepestWave: 20, StatsTrained: 1,
                                      ItemsOwned: 1, ItemsWorn: 1, Gleam: 500);
        Assert.NotEqual(TutorialStep.SetAGem, Tutorial.Showing(noGem));

        var holding = noGem with { GemsHeld = 1 };
        Assert.Equal(TutorialStep.SetAGem, Tutorial.Showing(holding));

        var set = holding with { GemsHeld = 0, GemsSet = 1 };
        Assert.NotEqual(TutorialStep.SetAGem, Tutorial.Showing(set));

        // ...and it points at the screen the deed is done on.
        Assert.Equal(Activity.Forge, Tutorial.Sends(TutorialStep.SetAGem, holding));
    }

    [Fact]
    public void test_the_deed_rung_can_always_be_escaped()
    {
        // An idle game whose thesis is that it plays without you must never TRAP anybody. The rung is
        // a dismissible card, not a gate: closing it by hand skips it for good, and the fight goes on
        // behind it either way.
        var holding = new TutorialFacts(WavesCleared: 20, DeepestWave: 20, StatsTrained: 1,
                                        ItemsOwned: 1, ItemsWorn: 1, GemsHeld: 1);
        Assert.Equal(TutorialStep.SetAGem, Tutorial.Showing(holding));
        Assert.NotEqual(TutorialStep.SetAGem,
                        Tutorial.Showing(holding, new[] { TutorialStep.SetAGem.ToString() }));
    }
}
