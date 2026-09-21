using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>
/// THE AUTHORED OPENING, AS THE HOST PLAYS IT: the facts it reads, the holds it applies, the input it
/// takes, and the one surface it draws.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OpeningScript"/> is the writing and <see cref="OpeningDirector"/> is the cursor; both are
/// testable without a graphics device and neither can touch the game. This half is the part that can:
/// it gathers the frame's facts, hands them to the cursor, and then obeys whatever the cursor says —
/// stop the fight, park the replay ahead of a beat's wind-up, keep the next wave off the stage, open a
/// screen ahead of its unlock, narrow the pointer to one production rectangle.
/// </para>
/// <para>
/// It is a separate file rather than another thousand lines of <c>Game1.cs</c> because the opening is a
/// mode: while it runs, several of this class's ordinary rules are suspended, and the suspensions
/// should be readable in one place instead of scattered through a nine-thousand-line frame.
/// </para>
/// </remarks>
public partial class Game1
{
    // ── STATE ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>How many wave rewards this career has been paid, ever. The FIRST one is a lesson.</summary>
    /// <remarks>
    /// Counted rather than inferred from the purse: gleam arrives from the Warren, from offline time
    /// and from a chest as well as from a wave, and "the pile went up" is a different fact from "your
    /// Hunter cleared something and was paid for it", which is the one the beat is about. It is also
    /// the opening's count of waves the player has SEEN cleared: the descent counts its depth when a
    /// wave is pushed, which is a whole wave before anyone watches it end.
    /// </remarks>
    private int _rewardsCredited;

    /// <summary>Seconds left of the arrival's held tableau.</summary>
    private float _arrivalDwell;

    /// <summary>How long the world stands still on arrival before the first word is said, in seconds.</summary>
    /// <remarks>
    /// The champion is on the stage and nothing is happening to it. Long enough to be a shot and short
    /// enough that a returning player does not read it as a hang; the fight behind it is frozen, so
    /// nothing is lost to it and nothing accrues during it.
    /// </remarks>
    private const float ArrivalDwellSeconds = 1.7f;

    /// <summary>The stage the host has already run the entry effects for.</summary>
    private OpeningStage _openingWas = OpeningStage.NotStarted;

    /// <summary>Has the illustrated prologue been played (or skipped) for this career?</summary>
    private bool _prologueSeen;

    /// <summary>Which prologue beat is on screen, and how long it has been there.</summary>
    private int _prologueBeat;
    private float _prologueClock;

    /// <summary>Has the one-time welcome gift already come from the tutorial boss?</summary>
    private bool _welcomeGiftGranted;

    /// <summary>Contextual tutorials finished by name — carried across a save so a later build can read it.</summary>
    private readonly HashSet<string> _tutorialsDone = new(StringComparer.Ordinal);

    /// <summary>The opening's card, its button, and the prologue's two — hit-tested next frame.</summary>
    private Rectangle _openingCard;
    private Rectangle _openingButton;
    private Rectangle _prologueNext;
    private Rectangle _prologueSkip;

    /// <summary>Seconds of full scrim left on a LIVE beat, which cannot hold one indefinitely.</summary>
    private float _openingBlaze;

    /// <summary>How long a live beat's scrim holds before it fades off the running fight.</summary>
    private const float OpeningBlazeSeconds = 3.2f;

    /// <summary>The fade's own length — the tail of the blaze, eased out.</summary>
    private const float OpeningBlazeFade = 0.7f;

    /// <summary>The opening's scrim weight over <see cref="FocusRenderer.Ink"/>. The heaviest: while it holds the player, it is the only thing.</summary>
    private const float OpeningScrimWeight = 0.80f;

    /// <summary>The opening's scrim as a flat fill, for the surfaces with nothing to light (the BEGIN gate).</summary>
    private static readonly Color OpeningScrim = FocusRenderer.Ink * OpeningScrimWeight;

    /// <summary>How far every lit rectangle is grown past its control, so the frame art sits inside the light.</summary>
    private const int SpotlightHalo = 10;

    /// <summary>Is the opening the thing on screen right now?</summary>
    /// <remarks>
    /// Never over the settings or help panel. Settings is where the opening can be ended, and a scrim
    /// that covered the one panel able to end it would be the soft lock this whole design forbids.
    /// </remarks>
    private bool OpeningUp => _opening.Running && !_showTitle && !_showSettings && !_showHelp;

    /// <summary>
    /// RIG ONLY: <c>RH_SHOT_OPENING=&lt;OpeningStage&gt;</c> — the authored beat to pose, or null.
    /// </summary>
    /// <remarks>
    /// An unknown name THROWS with the list of stages rather than posing nothing, because a capture
    /// that silently photographs the ordinary game is worse than no capture: it goes into the baseline
    /// as evidence that a beat was looked at.
    /// </remarks>
    private static OpeningStage? PosedOpeningStage
    {
        get
        {
            if (!CaptureRig) return null;
            if (Environment.GetEnvironmentVariable("RH_SHOT_OPENING") is not { Length: > 0 } name) return null;
            if (string.Equals(name, "live", StringComparison.OrdinalIgnoreCase)) return null;   // not a pose: it runs
            if (Enum.TryParse<OpeningStage>(name, true, out var stage) && OpeningScript.Running(stage)) return stage;
            throw new InvalidOperationException(
                $"RH_SHOT_OPENING='{name}' is not a running OpeningStage. Known: "
                + string.Join(", ", OpeningScript.Steps.Select(s => s.Stage)) + ".");
        }
    }

    /// <summary>RIG ONLY: <c>RH_SHOT_BEAT=&lt;n&gt;</c> — which prologue beat the shutter wants.</summary>
    private static int PosedPrologueBeat
        => int.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_BEAT"), out var n)
            ? Math.Clamp(n, 0, OpeningScript.Prologue.Count - 1)
            : 0;

    // ── FACTS ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Everything an authored beat might be waiting for, gathered once from production state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not one of these is a tutorial variable. The arrival's settling is the fight screen's own run
    /// having started plus a dwell nothing else can see; the enemy standing is the hold the fight
    /// already waits on before anyone swings; the signature being imminent is the replay's own report
    /// that the barrier is holding it.
    /// </para>
    /// <para>
    /// <b>Two kinds of fact, never confused.</b> A GAMEPLAY fact — a reward credited, an item worn — is
    /// true the frame it happens. A PRESENTATION fact — the clear has been shown, the released cast has
    /// played — is true once the player has SEEN it happen, and the fight screen is the only thing that
    /// knows when that is. Every card about a moment waits on the second kind; the playtest of
    /// 2026-09-11 was two cards that waited on the first.
    /// </para>
    /// </remarks>
    private OpeningFacts OpeningFactsNow() => new(
        Screen: ScreenActivity(),
        ArrivalSettled: _expedition.RunStarted && _arrivalDwell <= 0f,
        EnemySettled: _expedition.EnemySettled,
        RewardsCredited: _rewardsCredited,
        ClearShown: _expedition.ClearShown,
        // HELD, AND NOTHING STILL FALLING. The barrier parks the cast wherever the wave has got to, which
        // can be a moment after another creature's killing blow; a card raised there froze that death's
        // dissolve under the words for as long as the card was read (seen at 100 %, 2026-09-11). The
        // replay stays parked while the fight's own presentation plays the fall out — GLEAM's rule.
        SignatureHeld: _expedition.ReplayHeld && _expedition.HoldBeforeKind == BattleEventKind.Skill && _expedition.FallsPlayed,
        // ...and never waits on a release that was never made: a hold and its release live on the fight
        // screen and die with the process, so with no hold in play there is nothing left to land.
        SignatureLanded: _expedition.ReleasedBeatPlayed || !_expedition.HoldInPlay,
        BossSettled: _expedition.BossOnStage && _expedition.EnemySettled,
        BossesFelled: _bossesFelled,
        ChestsOpened: _forge?.ChestsOpened ?? 0,
        ItemSelected: OpeningItemPicked(),
        ItemsWorn: Enum.GetValues<GearSlot>().Count(sl => _hunter.Worn(sl) is not null),
        // THE SCREEN'S OWN GATE, not a wave count copied out of it — the same call the rail makes.
        TrainingOpen: Unlocks.IsOpen(Activity.Training, GuideUnlockFacts()),
        // AND THE ROW'S OWN ANSWER. Hunter.CanTrain is what the TRAIN button obeys (rank cap AND
        // price), so the beat cannot arrive over a panel that would refuse every row on it. ANY stat,
        // because the deed is "make a choice" and naming one here would be making it.
        CanAffordFirstRank: Enum.GetValues<HunterStat>().Any(_hunter.CanTrain),
        StatsTrained: Enum.GetValues<HunterStat>().Sum(_hunter.RankOf));

    /// <summary>
    /// Is the item the GEAR beats are about the GEAR screen's own selection, put there by the player?
    /// </summary>
    /// <remarks>
    /// Read, never written: the screen's selection and whether a player's gesture made it are the
    /// screen's (<see cref="GearScreen.PickedItemId"/>), and nothing in the opening can set either. So
    /// the step cannot move on while the inspector shows nothing, and it cannot be moved on by anything
    /// but the real click on the real cell.
    /// </remarks>
    private bool OpeningItemPicked()
        => _gear is { PickedItemId: { } picked } && OpeningLitItem() is { } lit && picked == lit;

    /// <summary>
    /// The item the GEAR beats are about: the welcome gift — or, if some other chest was opened in its
    /// place, the first piece in the bag this Hunter can wear. Null when there is nothing to point at.
    /// </summary>
    /// <remarks>
    /// The fallback is not decoration. The tutorial boss's own chest roll can drop a second chest beside
    /// the gift, and an opening that could only ever point at the gift would then be waiting on an item
    /// the player does not have.
    /// </remarks>
    private string? OpeningLitItem()
    {
        if (_forge is null || _gear is null) return null;
        var welcome = GiftChests.Welcome.Items[0].InstanceId;
        if (_forge.Inventory.Any(i => i.InstanceId == welcome)) return welcome;
        var wearer = _gear.Character;   // the champion the doll shows, which is the one being played
        return _forge.Inventory.FirstOrDefault(i => Gear.SlotFor(i.BaseType) is not null && Gear.CanWear(wearer, i))?.InstanceId;
    }

    /// <summary>
    /// Is the running opening about to walk the player into this screen itself?
    /// </summary>
    /// <remarks>
    /// Then the screen's unlock notice is not posted. It would wait out the opening behind its held
    /// clock and arrive after it, telling the player to "read it before you crack it" about a chest
    /// they have just opened. A player who skipped the tutorial is not being walked anywhere, and
    /// hears it.
    /// </remarks>
    private bool OpeningWalksInto(Activity screen)
        => _opening.Running
           && OpeningScript.Steps.Any(s => s.Mode == TutorialStepMode.ForceNavigate && s.Screen == screen);

    /// <summary>
    /// The chest the VAULT beat is about: the welcome gift while it is unopened, else the best chest held.
    /// </summary>
    private Chest? OpeningLitChest()
        => _forge is null ? null
           : _forge.UnopenedChests.FirstOrDefault(c => string.Equals(c.Gift, GiftChests.WelcomeKey, StringComparison.OrdinalIgnoreCase))
             ?? ChestDossiers.BestFirst(_forge.UnopenedChests).FirstOrDefault();

    // ── THE LOADED CAREER ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decide where in the opening a loaded career actually stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The version decides, never the progression.</b> A file written before
    /// <c>OpeningScript.FirstVersionWithOpeningState</c> carries no cursor at all, and the question
    /// "has this player lived the opening?" has to be answered some other way — from what they have
    /// done. A file at or past it is believed exactly as written, including the brand-new career that
    /// quit during the arrival, which is the case a progression guess gets wrong. And a v6 cursor is
    /// read in v6's own numbering (<see cref="OpeningScript.StageOf(int, int)"/>).
    /// </para>
    /// <para>
    /// <b>A believed cursor is still not resumed on a moment that is gone.</b> A beat about something
    /// the fight or a screen was showing — the replay held before the first cast, the cast playing out,
    /// a chest's reveal — is resumed on the wait that sets it up again
    /// (<see cref="OpeningScript.ResumeStage"/>). Done here and not in the director's Restore, which the
    /// capture rig uses to pose exact stages.
    /// </para>
    /// <para>
    /// <b>And the cursor is still not the truth.</b> Even a believed cursor is walked forward over
    /// every beat whose deed the save can prove was already performed, so a career that opened the
    /// chest and quit before the autosave caught it is never asked to open it again.
    /// </para>
    /// </remarks>
    private void SeedOpening()
    {
        // THE RIG NEVER PLAYS THE OPENING. Every fixture dresses a save and photographs a screen; an
        // authored beat over it would be in every capture the project has. RH_SHOT_OPENING is the one
        // exception, and it is the whole reason the opening can be looked at — see PosedOpeningStage.
        // RH_SHOT_OPENING=live LETS IT ACTUALLY RUN under a harness. Every other harness path either
        // skips the opening or freezes one beat of it for the shutter — so the LIVE machine, the thing
        // a player meets, was the one part of this with no headless validation at all. Read outside
        // the CaptureRig branch because the boot check is a different harness with no RH_SHOT.
        if (string.Equals(Environment.GetEnvironmentVariable("RH_SHOT_OPENING"), "live", StringComparison.OrdinalIgnoreCase))
        {
            _opening.Begin();
            _openingWas = _opening.Stage;
            return;
        }

        if (CaptureRig)
        {
            if (PosedOpeningStage is { } posed) _opening.Restore(posed);
            else { _opening.SkipToEnd(); _prologueSeen = true; }
            _openingWas = _opening.Stage;
            return;
        }

        if (_saveVersionSeen < OpeningScript.FirstVersionWithOpeningState)
        {
            var lived = OpeningScript.SeedOpeningAsLived(GuideUnlockFacts(), _saveVersionSeen);
            if (lived) { _opening.SkipToEnd(); _prologueSeen = true; }
            else _opening.Begin();   // an old file with nothing done is a player who never started
            _openingWas = _opening.Stage;
            return;
        }

        _opening.Restore(OpeningScript.ResumeStage(OpeningScript.StageOf(_pendingOpeningStage, _saveVersionSeen)));
        _opening.FastForwardOverDoneDeeds(OpeningFactsNow());
        _openingWas = _opening.Stage;
        _pendingOpeningStage = 0;
    }

    // ── THE FRAME ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Run the opening for this frame: take its input, step it on, and apply what the beat holds.
    /// </summary>
    /// <remarks>
    /// Called EARLY — before the fight advances, before the Warren ticks and before the input authority
    /// is recomputed — because everything downstream reads the decision made here. A beat that means to
    /// freeze the game has to have said so before the game moves.
    /// </remarks>
    private void UpdateOpening(float dt)
    {
        // ── THE SHUTTER POSES A BEAT AND THE BEAT HOLDS STILL FOR IT. ───────────────────────────
        //
        // A state no capture can pose has never been looked at, and this codebase has been bitten by
        // that repeatedly. So the rig can park the cursor on any authored stage — and the cursor is
        // then FROZEN, because half of these beats are gated on a fact the fixture's own dressing
        // satisfies (an enemy standing, a chest in the vault), and a posed stage that advanced before
        // frame sixty would photograph the next one. Nothing else changes: the holds, the grants and
        // the whole surface below run exactly as they do in play.
        if (PosedOpeningStage is { } pose)
        {
            if (_opening.Stage != pose) { _opening.Restore(pose); _openingWas = OpeningStage.NotStarted; }
            EnterOpeningStage();
            _prologueBeat = PosedPrologueBeat;
            _arrivalDwell = 0f;
            _openingBlaze = OpeningBlazeSeconds;
            _prologueClock = 1f;
            ApplyOpeningHolds();
            return;
        }

        EnterOpeningStage();

        if (!_opening.Running)
        {
            ReleaseOpeningHolds();
            return;
        }

        // The arrival's own clock only runs once there is something to look at.
        if (_arrivalDwell > 0f && _expedition.RunStarted) _arrivalDwell = Math.Max(0f, _arrivalDwell - dt);
        if (_openingBlaze > 0f) _openingBlaze = Math.Max(0f, _openingBlaze - dt);
        if (_opening.Stage == OpeningStage.Prologue) _prologueClock += dt;

        TakeOpeningInput();
        _opening.Update(OpeningFactsNow());
        EnterOpeningStage();

        // WHAT THE BEAT HOLDS — decided AFTER this frame's press and this frame's facts, and read by
        // the fight, the Warren and the rail further down this same frame. It used to be written
        // before the press was read, so the frame a card was answered still ran under that card's
        // hold, and the release reached the fight a frame late.
        if (!_opening.Running) { ReleaseOpeningHolds(); return; }
        ApplyOpeningHolds();

        // A BEAT THAT HOLDS THE PLAYER STANDS ON ITS OWN SCREEN. Its card is drawn only there and every
        // other control is dead, so a paused or forced beat whose screen is not the one on top was a room
        // with no door: IntroduceChest met on the VAULT the player had just walked into, or a reload into
        // "SELECT THE ITEM" that lands on the HUNT (adversarial review, 2026-09-11). The opening takes the
        // player there instead — the one navigation it makes on its own, and only ever to the screen the
        // beat is about. A forced NAVIGATION is exempt: its screen is the tile the player must press.
        if (_opening.Mode is TutorialStepMode.PauseExplain or TutorialStepMode.ForceAction
            && _opening.Screen is { } own && !_showTitle && !_showSettings && !_showHelp
            && ScreenActivity() != own && Array.IndexOf(NavActivity, own) is var slot and >= 0 && NavUnlocked(slot))
            OpenNav(slot);
    }

    /// <summary>Write what the current beat holds where the systems further down this frame read it.</summary>
    private void ApplyOpeningHolds()
    {
        _expedition.HoldBeforeKind = _opening.HoldsReplayBefore;
        // Only after the clear the beat is about: the boss beat holds a BOSS wave's clear, never the
        // wave-one clear of a Hunter who lost to it and started again (see HoldsNextWaveAfter).
        _expedition.HoldNextWave = _opening.HoldsNextWaveAfter(bossWave: _expedition.BossOnStage);
        _openingGrants.Clear();
        if (_opening.GrantsScreen is { } grant) _openingGrants.Add(grant);
        if (_gear is not null)
        {
            // THE INSPECTOR WAITS FOR THE PICK. While the player is being sent to GEAR and asked to
            // click the item, the screen chooses nothing on its own — so the click visibly fills an
            // empty inspector, and a selection the screen made is never mistaken for one they made.
            _gear.AutoSelect = _opening.Stage is not (OpeningStage.ForceGear or OpeningStage.ForceItemSelect);
            // ...and no piece can be carried while the opening owns the pointer: a drop on the doll is
            // an equip, and it would wear the item past the step that asked for a click.
            _gear.AllowCarry = !_opening.OwnsInput;
        }
        // THE REVEAL STAYS UP WHILE "YOUR FIRST ITEM" IS READ — the card lights it and talks about it,
        // and the reveal's own clock would otherwise dissolve it under the words.
        if (_forge is not null) _forge.HoldRevealOpen = _opening.Stage == OpeningStage.IntroduceItem;
    }

    /// <summary>The opening is not running: nothing is held, nothing is granted, the screens are their own.</summary>
    private void ReleaseOpeningHolds()
    {
        _expedition.HoldBeforeKind = null;
        _expedition.HoldNextWave = false;
        _openingGrants.Clear();
        if (_gear is not null) { _gear.AutoSelect = true; _gear.AllowCarry = true; }
        if (_forge is not null) _forge.HoldRevealOpen = false;
    }

    /// <summary>Side effects owed to a stage the cursor has just moved onto.</summary>
    private void EnterOpeningStage()
    {
        if (_opening.Stage == _openingWas) return;
        var was = _openingWas;
        _openingWas = _opening.Stage;

        _arrivalDwell = _opening.Stage == OpeningStage.Arrival ? ArrivalDwellSeconds : 0f;
        _openingBlaze = OpeningBlazeSeconds;
        _openingCard = _openingButton = Rectangle.Empty;

        // THE PROLOGUE IS ONE-SHOT, and it is marked read on the way OUT of it rather than on the way
        // in: a career that quit halfway through the story has not seen the story.
        if (was == OpeningStage.Prologue) { _prologueSeen = true; Save(); }

        // "YOUR FIRST ITEM" IS ABOUT THE REVEAL, AND ITS CONTINUE ENDS IT. The chest's reveal card stops
        // its clock under the pointer, so a hand resting on CONTINUE kept it up through the forced walk to
        // GEAR — where it covered the very cell "SELECT THE ITEM" lights, with its own SELL and EQUIP
        // buttons under the brackets (seen at 125 %, 2026-09-11). The reveal's own skip, not a new path.
        if (was == OpeningStage.IntroduceItem) _forge?.AdvanceReveal();

        // BACK TO THE HUNT, AND THE ACKNOWLEDGEMENT IS THE WAY BACK. The last card of a chapter says
        // where the player goes next, so answering it goes there — the only navigation the opening
        // makes for the player, and only once they have pressed for it. A SKIP from any other beat
        // leaves them where they stand.
        //
        // TWICE NOW: the Training chapter ends the same way the gear chapter does. Its last card is
        // read on the TRAINING screen and the beat after it is the wait for the tutorial boss, which
        // happens on the HUNT — so a player left standing in TRAINING would be watching a screen while
        // the thing the next card is about arrived behind it.
        if (was == OpeningStage.ShowTrained && _opening.Stage == OpeningStage.AwaitBoss && !CaptureRig) OpenNav(0);
        if (was == OpeningStage.ShowEquipped && _opening.Stage == OpeningStage.Complete && !CaptureRig) OpenNav(0);

        // AND THE END IS WRITTEN DOWN IMMEDIATELY. The autosave is ten seconds wide and the last beat
        // of the opening is followed by the player walking away satisfied.
        //
        // ...AND THE BOOT LINE IS SPENT WITH IT. Its clock was held for the whole opening, and "NO SAVE
        // FOUND. STARTING FRESH." two minutes into a career, over BACK TO THE HUNT, is news about nothing.
        if (_opening.Stage == OpeningStage.Complete) { _bootTimer = 0f; Save(); }
    }

    // ── INPUT ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The one press an authored beat accepts, and the two doors that stay open behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Raw key and mouse edges, like the tour's: <c>Pressed()</c> and <c>MouseClicked</c> both read
    /// <c>_swallowInput</c>, which this beat is the reason for. Everything not named here is spent —
    /// a click that lands anywhere else does nothing at all, which is what makes the lit control the
    /// only control.
    /// </para>
    /// <para>
    /// <b>The two doors.</b> ESCAPE and the settings gear still work, on every beat, because Settings is
    /// where the opening can be ended and a tutorial you cannot leave is a worse product than no
    /// tutorial. Neither grants anything; see the GUIDANCE switch.
    /// </para>
    /// </remarks>
    private void TakeOpeningInput()
    {
        if (!_opening.OwnsInput || CaptureRig) return;


        var escape = KeyEdge(Keys.Escape);
        // THE WAY BACK: with the panel up, Escape closes it — one layer per press; an open list was
        // shut at the top of Update and spent the edge. (It used to re-open on every press, so a panel
        // opened from a beat could only be closed with its ×.) Nothing else of the beat's runs under one.
        if (_showSettings || _showHelp)
        {
            if (escape && !_settingsEscSpent && _showSettings) { _showSettings = false; _settingsEscSpent = true; }
            return;
        }
        // THE WAY OUT, first and unconditionally.
        if (escape || (_clicked && SettingsGear.Contains(ChromeMouse)))
        {
            _showSettings = true;
            _settingsEscSpent = true;
            _modalOpenedNow = true;   // one edge, one action: the panel must not also act on it
            _sound.Play("sfx_click", 0.6f);
            return;
        }

        var keyGo = (_keys.IsKeyDown(Keys.Space) && _prevKeys.IsKeyUp(Keys.Space))
                    || (_keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter));

        if (_opening.Stage == OpeningStage.Prologue)
        {
            if (_clicked && _prologueSkip.Contains(ChromeMouse)) { SkipPrologue(); return; }
            if (keyGo || (_clicked && _prologueNext.Contains(ChromeMouse))) NextPrologueBeat();
            return;
        }

        // The BEGIN gate keeps its own plate: it is the title's button, not an explanatory card, and
        // the player has not been taught the acknowledgement grammar yet.
        if (_opening.Stage == OpeningStage.AwaitBegin)
        {
            if (keyGo || (_clicked && _openingButton.Contains(ChromeMouse)))
            {
                _opening.Acknowledge();
                _sound.Play("sfx_click", 0.7f);
            }
            return;
        }

        // ── AN EXPLANATORY CARD TAKES ANY KEY AND ANY CLICK, AND SPENDS THE EDGE. ────────────────
        //
        // One grammar for every explanation, so the player never has to work out whether the lit thing
        // is a button: it is not, on any explanatory beat, and the card says so in the one sentence it
        // ends with. ESCAPE and the gear are already gone above — they are the way out, not a way on —
        // and a bare modifier is not an acknowledgement, because a player reaching for Alt-Tab has not
        // read anything.
        //
        // THIS SHAPE WAS SHIPPED ONCE AND REVERTED. The optional tour advanced on any click and any key
        // anywhere, and the playtest (2026-09-09) was "clicking outside the screen also fast-forwards
        // it" — every gold ring in the intro was a button that did the wrong thing, because the control
        // under the ring was still live and pressing it skipped the explanation instead of using it.
        // What makes it safe here is the other half of this pass, and it is not optional: while a card
        // is up the production UI is PRESENTATION ONLY (PointerWithheldFromScreens), so there is no
        // control under the light to press, and the edge is spent below so the next beat cannot take it.
        if (_opening.WantsAcknowledgement && (AnyKeyPressed() || _clicked))
        {
            _opening.Acknowledge();
            // ONE EDGE, AT MOST ONE ACTION (ADR-006 §3). The cursor moves on THIS frame, and the beat it
            // moves to may be a forced one whose control is under the pointer right now — the VAULT tile
            // lit beneath "A CHEST DROPPED", the EQUIP button beneath "ITEM STATS". HandleNavClick and
            // ForcedScreenClick both read the raw edge, so without this latch the click that closed the
            // explanation would also perform the deed the next card is about to ask for.
            _openingAckSpent = true;
            _sound.Play("sfx_click", 0.7f);
        }
    }

    /// <summary>
    /// The acknowledging press is SPENT: nothing downstream may read this frame's edge as a deed.
    /// </summary>
    /// <remarks>
    /// The opening's own <c>_modalOpenedNow</c>, and it exists for the same reason that field does:
    /// <c>_swallowInput</c> cannot do this job, because the forced paths deliberately read the RAW edge
    /// so the lit control can take a click the swallow would have eaten. Cleared at the top of every
    /// Update beside the other one-frame latches.
    /// </remarks>
    private bool _openingAckSpent;

    /// <summary>One beat on, or out of the prologue at its end.</summary>
    private void NextPrologueBeat()
    {
        _sound.Play("sfx_click", 0.6f);
        if (_prologueBeat + 1 < OpeningScript.Prologue.Count)
        {
            _prologueBeat++;
            _prologueClock = 0f;
            return;
        }
        _opening.Acknowledge();
    }

    /// <summary>
    /// SKIP CINEMATIC — and it skips the CINEMATIC.
    /// </summary>
    /// <remarks>
    /// Deliberately not SKIP TUTORIAL. A player who does not want the story very often does want to be
    /// shown the game, and a single control that did both would make the story a hostage. This one
    /// lands on BEGIN THE HUNT with the whole opening still ahead of it; the other lives in Settings.
    /// </remarks>
    private void SkipPrologue()
    {
        _prologueBeat = OpeningScript.Prologue.Count - 1;
        _opening.Acknowledge();
        _sound.Play("sfx_click", 0.6f);
    }

    /// <summary>
    /// The click a FORCED step lets through to the screen that owns the control.
    /// </summary>
    /// <remarks>
    /// The rectangle is the one the light is cut from, which is the one the screen drew, which is the
    /// one the screen hit-tests — the same rectangle three times, at every UI density, because it is
    /// asked for once. There is no tutorial-only button in this design, so the deed the player performs
    /// IS the deed the game records, and a step that were somehow lit over the wrong control would take
    /// no click at all rather than take one on the tutorial's behalf. The light's HALO is not part of
    /// the control (<see cref="ClickableOf"/>).
    /// </remarks>
    /// <summary>
    /// Does the screen under this beat get no pointer at all — the presentation-only rule?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A lit thing is pressable or it is not, and the player must never have to guess.</b> While an
    /// explanation owns the frame nothing underneath it is pressable, so nothing underneath it may
    /// react to the cursor: no hover wash, no button glow, no 2 px press, no tooltip, no lift. The
    /// mechanism is the tour's, extended (<c>ReadCursor</c>) — the screens are simply handed an
    /// off-page cursor, so every hit-test they already do answers "not here" without a single screen
    /// learning that a tutorial exists.
    /// </para>
    /// <para>
    /// <b>A FORCED ACTION is the one beat that keeps it</b>, and keeps it only where the deed is: the
    /// pointer reaches the screen exactly inside the rectangle the light is cut from and the click is
    /// tested against (<see cref="ClickableOf"/>), and nowhere else. So the lit control hovers like the
    /// real control it is, and the dozen controls around it — equally unpressable — stay quiet. Same
    /// rectangle for the light, the hover and the click, asked for once.
    /// </para>
    /// <para>
    /// A forced NAVIGATION withholds it too: what is live there is a rail tile, which is chrome and
    /// reads <c>ChromeMouse</c> (see <see cref="NavTileTakesPointer"/>), so the screen behind it has
    /// nothing to offer either.
    /// </para>
    /// </remarks>
    private bool PointerWithheldFromScreens
        => PointerWithheld(_opening.Running && _opening.OwnsInput,
                           _opening.ForcedTarget is not null,
                           () => OpeningHoles().Any(h => ClickableOf(h).Contains(ChromeMouse)));

    /// <summary>
    /// THE RULE ITSELF, with nothing of the host in it: is the pointer withheld from the screen?
    /// </summary>
    /// <param name="beatOwnsInput">A beat has the controls — anything but a live one that is not settling.</param>
    /// <param name="deedIsForced">That beat is a <see cref="TutorialStepMode.ForceAction"/>: one real control is live.</param>
    /// <param name="cursorOnLitControl">
    /// Is the cursor inside the rectangle the light is cut from and the click is tested against?
    /// Deferred, because it is only asked on the one beat that can answer it — resolving a spotlight
    /// costs a screen's layout, and every other branch decides without it.
    /// </param>
    /// <remarks>
    /// Pure, so the truth table can be looked at directly rather than inferred from a wiring assertion.
    /// The three rows that matter: an EXPLANATION withholds it everywhere, a FORCED NAVIGATION
    /// withholds it from the screen (what is live there is a rail tile, which is chrome), and a FORCED
    /// ACTION grants it exactly on the control the deed needs and nowhere else on the page.
    /// </remarks>
    internal static bool PointerWithheld(bool beatOwnsInput, bool deedIsForced, Func<bool> cursorOnLitControl)
    {
        if (!beatOwnsInput) return false;
        if (!deedIsForced) return true;
        return !cursorOnLitControl();
    }

    /// <summary>May this rail tile react to the cursor — is it a tile the player could actually press?</summary>
    /// <remarks>
    /// The rail is chrome and reads <c>ChromeMouse</c>, which stays live so the opening's own card and
    /// its forced tile can be hit-tested — so the rail needs its own answer rather than inheriting the
    /// screens' blanked pointer. Without this, "A CHEST DROPPED" lit the VAULT tile while it was still
    /// only being POINTED at, and the tile painted its gold hover wash under a mouse that
    /// <c>HandleNavClick</c> would refuse: the exact "it looks like a button and does nothing" the
    /// uniform grammar exists to end.
    /// </remarks>
    private bool NavTileTakesPointer(Activity activity)
        => NavTileTakesPointer(_opening.Running && _opening.OwnsInput, _opening.ForcedNav, activity);

    /// <summary>The rail's half of the same rule, pure: only the ONE forced tile may react.</summary>
    /// <param name="beatOwnsInput">A beat has the controls.</param>
    /// <param name="forcedNav">The one tile a forced navigation makes live, or null.</param>
    /// <param name="tile">The tile being drawn.</param>
    internal static bool NavTileTakesPointer(bool beatOwnsInput, Activity? forcedNav, Activity tile)
        => !beatOwnsInput || forcedNav == tile;

    private bool ForcedScreenClick()
        => _clicked && !_openingAckSpent && !_showSettings && !_showHelp && !WelcomeUp
           && _opening.ForcedTarget is not null
           && OpeningHoles().Any(h => ClickableOf(h).Contains(ChromeMouse));

    /// <summary>The CONTROL inside a lit rectangle: the light less its halo.</summary>
    /// <remarks>
    /// The halo is for the eye — the frame art sits inside the light. A forced click that counted it
    /// reached whatever lay in that ring: under "EQUIP IT", a one-to-three-pixel strip of the UPGRADE /
    /// REFORGE / SALVAGE row, one click from sending the player to the FORGE with no card to follow
    /// (adversarial review, 2026-09-11). So the click is tested against the control itself.
    /// </remarks>
    internal static Rectangle ClickableOf(Rectangle lit)
    {
        lit.Inflate(-SpotlightHalo, -SpotlightHalo);
        return lit;
    }

    // ── WHAT IS LIT ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The production rectangles this beat is about, in canvas space — empty when it is about nothing.
    /// </summary>
    /// <remarks>
    /// Three sources, all of them the game's own. A forced navigation lights the RAIL TILE it wants,
    /// where it stands, this frame. A beat aimed at the rail lights the tile the chain is heading for
    /// (<c>OpeningScript.NextForcedScreen</c>) rather than the whole rail, because "open it in the
    /// Vault" is about one tile. Everything else asks the screen that owns the target, through the
    /// same <see cref="TourSpotlights"/> the tours and the coach use — including the targets only the
    /// host can name (the item's cell, the chest's card, the reveal), which it resolves there too.
    /// </remarks>
    private Rectangle[] OpeningHoles()
    {
        if (_opening.Step is not { } step) return Array.Empty<Rectangle>();

        if (step.Mode == TutorialStepMode.ForceNavigate && step.Screen is { } want) return NavTile(want);
        if (step.Target == TourTarget.NavRail)
            return OpeningScript.NextForcedScreen(step.Stage) is { } soon ? NavTile(soon) : Array.Empty<Rectangle>();
        if (step.Target is not { } target) return Array.Empty<Rectangle>();
        if (step.Screen is { } on && on != ScreenActivity()) return Array.Empty<Rectangle>();

        var own = TourSpotlights(ScreenActivity(), target);
        // The whole-canvas answer means the screen does not know this target: light nothing rather than
        // darken the page and cut a hole the size of the page.
        return own.Any(h => h.Width >= 1900 && h.Height >= 1060) ? Array.Empty<Rectangle>() : own;
    }

    /// <summary>One rail tile, where it stands right now — or nothing, if the rail is not showing it.</summary>
    private Rectangle[] NavTile(Activity activity)
    {
        var slot = NavSlots().IndexOf(Array.IndexOf(NavActivity, activity));
        return slot >= 0 ? new[] { NavHexRect(slot) } : Array.Empty<Rectangle>();
    }

    // ── THE SURFACE ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The opening's one drawn surface: the prologue, the BEGIN gate, or a beat's light and card.
    /// </summary>
    /// <remarks>
    /// Drawn last of the chrome, over the rail and the pills, for the reason a tour is: while a beat
    /// holds the player it is the only thing being asked of them. A LIVE beat is the exception it looks
    /// like — its scrim BLAZES and fades, leaving the brackets and the card over a fight that never
    /// stopped, because a permanently dark screen over a running game is intolerable.
    /// </remarks>
    private void DrawOpening()
    {
        if (!OpeningUp) return;

        switch (_opening.Stage)
        {
            case OpeningStage.Prologue: DrawPrologue(); return;
            case OpeningStage.AwaitBegin: DrawBeginGate(); return;
        }

        if (_opening.Step is not { } step || step.Title.Length == 0) return;
        // A narrated beat belongs to its screen. Wandering off during a LIVE one is allowed; the words
        // simply wait, because a card about the Hunter's health on the FORGE screen explains nothing.
        // A FORCED NAVIGATION IS THE EXCEPTION, and it is not really one: its Screen is the
        // DESTINATION, so by definition it is drawn on the screen the player has not left yet.
        if (step.Mode != TutorialStepMode.ForceNavigate
            && step.Screen is { } on && on != ScreenActivity()) return;

        var holes = OpeningHoles();
        var live = step.Mode == TutorialStepMode.LiveExplain;
        // A LIVE BEAT WITH NOTHING TO LIGHT DARKENS NOTHING. The full-canvas scrim is the modal
        // beats' answer to "the card is the only thing here"; over a running fight it is a black
        // screen with a caption, which is worse than no scrim at all.
        var weight = live ? Math.Clamp(_openingBlaze / OpeningBlazeFade, 0f, 1f) : 1f;
        var scrim = OpeningScrimWeight * (live && !UiMotion.Reduced ? UiMotion.Smooth(weight) : weight);
        if (holes.Length > 0)
        {
            // THE LIGHT IS CUT TO THE THING (FocusRenderer): the Hunter and the pack by their own
            // silhouettes, a panel or a rail tile by a soft plate. The rectangles are still the ones the
            // forced click and the card use (ForcedScreenClick, DrawOpeningCard); only the picture changed.
            FocusShapes(ScreenActivity(), step.Target ?? TourTarget.NavRail, holes, _focusShapes);
            DrawFocus(scrim);
        }
        else if (scrim > 0f && !live)
            _ui.Fill(_batch, new Rectangle(0, 0, UiKit.Page.Width, UiKit.Page.Height), FocusRenderer.Ink * scrim);

        DrawOpeningCard(step, holes);
    }

    /// <summary>The beat's words, beside the light, with the one control that ends it.</summary>
    private void DrawOpeningCard(OpeningStep step, Rectangle[] holes)
    {
        var pad = UiMetrics.Space(24);
        var width = Math.Min(UiMetrics.Control(TourCardWidth), UiKit.Page.Width / 2);
        var body = _ui.WrapBig(step.Body, width - pad * 2, UiTypography.Body);
        var bodyTop = pad + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
        var acts = _opening.WantsAcknowledgement || step.Mode == TutorialStepMode.ForceNavigate
                   || step.Mode == TutorialStepMode.ForceAction;
        var btnH = UiMetrics.Control(44);
        var footer = acts ? UiMetrics.Space(16) + btnH : 0;
        var height = bodyTop + Math.Max(1, body.Count) * UiTypography.Pitch(UiTypography.Body) + footer + pad;

        // Off the chrome row it is not pointing at, and off the fight's skill dock.
        var avoid = new List<Rectangle> { new(NavRailWidth, 0, 1920 - NavRailWidth, ChromeRowBottom + UiMetrics.Space(8)) };
        if (ScreenActivity() == Activity.Hunt) avoid.Add(HuntScreen.DockRect);
        _openingCard = holes.Length > 0
            ? TourCardRect(holes, avoid, width, height)
            : new Rectangle((UiKit.Page.Width + NavRailWidth - width) / 2, (UiKit.Page.Height - height) / 2, width, height);

        _ui.Fill(_batch, _openingCard, SlotGround);
        _ui.Plate(_batch, _openingCard, UiInk.Accent);
        _ui.TextBig(_batch, step.Title, _openingCard.X + pad, _openingCard.Y + pad, UiInk.Accent, UiTypography.Headline);

        var y = _openingCard.Y + bodyTop;
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, _openingCard.X + pad, y, UiInk.Primary, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        if (!acts) { _openingButton = Rectangle.Empty; return; }

        // ── NEITHER KIND OF BEAT HAS A BUTTON. ──────────────────────────────────────────────────
        //
        // An explanatory beat used to carry a CONTINUE plate, and it was the only thing telling the
        // player which of the two identical-looking pictures they were looking at: the same scrim, the
        // same halo, the same card — one wanting a press on a plate, the other a press on the lit
        // control. A first-time player cannot be asked to read a footer to find out whether a spotlight
        // is clickable. So there is ONE prompt for every explanation and a different one for every
        // deed, and the words say which hand to use rather than which control to find.
        //
        // The forced beat never had a button and still does not: its button is the lit control, and a
        // second one beside the card would be the tutorial-only control this design does not have.
        _openingButton = Rectangle.Empty;
        var footerY = _openingCard.Bottom - pad - btnH
                      + (btnH - UiTypography.Pitch(UiTypography.Secondary)) / 2 + UiMetrics.Space(6);
        _ui.TextBig(_batch,
                    _opening.WantsAcknowledgement ? AcknowledgePrompt : "CLICK WHAT IS HIGHLIGHTED",
                    _openingCard.X + pad, footerY, UiInk.Secondary, UiTypography.Secondary);
    }

    /// <summary>
    /// The one sentence every explanatory beat ends with.
    /// </summary>
    /// <remarks>
    /// Spelled out, both halves, and no abbreviation: the player this is written for reads the game in
    /// a second language, and "PRESS ANY KEY" alone leaves a mouse user hunting for a button that is
    /// not there. Naming both inputs is also the accessibility contract — the keyboard is never the
    /// lesser path, it is the first one named.
    /// </remarks>
    internal const string AcknowledgePrompt = "PRESS ANY KEY OR CLICK TO CONTINUE";

    /// <summary>
    /// THE PROLOGUE: six beats over a dark ground, once per career.
    /// </summary>
    /// <remarks>
    /// It says nothing about a button, a screen or a resource — every word of it is
    /// <c>design/narrative/world.md</c>, and the teaching starts on the other side of BEGIN THE HUNT.
    /// Each beat stands on its own illustration (<see cref="PrologueArt"/>), composed with the subject
    /// in the upper half and a quiet lower third, because the title and the lines sit at
    /// <c>page.Height / 2 + 40</c> over the picture, and SKIP lives in the bottom band.
    /// </remarks>
    private void DrawPrologue()
    {
        var page = new Rectangle(0, 0, UiKit.Page.Width, UiKit.Page.Height);
        _ui.Fill(_batch, page, new Color(0x05, 0x04, 0x09));

        var index = Math.Clamp(_prologueBeat, 0, OpeningScript.Prologue.Count - 1);
        var beat = OpeningScript.Prologue[index];

        // A BEAT FADES UP. Reduced Motion cuts instead — a fade is a UI transition, the one class the
        // accessibility table suppresses outright — and holds the camera still below.
        var up = Math.Clamp(_prologueClock / 0.45f, 0f, 1f);
        var ink = UiMotion.Reduced ? 1f : UiMotion.Smooth(up);

        // THE PICTURE, AND A SLOW CAMERA OVER IT. Each beat is shown against its own plate
        // (PrologueArt), a 1920×1080 still drawn 10 % over the canvas so the slow push never reveals
        // an edge. Reduced Motion holds the frame still.
        var drift = UiMotion.Reduced ? 0.5f : Math.Clamp(_prologueClock / 11f, 0f, 1f);
        _ui.BackgroundPanned(_batch, PrologueArt.For(beat), 1.10f, 0.30f + 0.34f * drift,
                             new Color(150, 148, 158) * ink);
        _ui.Fill(_batch, page, new Color(0x05, 0x04, 0x09) * 0.34f);

        // The letterbox: the frame the illustration hangs in.
        var band = UiMetrics.Control(120);
        _ui.Fill(_batch, new Rectangle(0, 0, page.Width, band), new Color(0x02, 0x02, 0x04));
        _ui.Fill(_batch, new Rectangle(0, page.Height - band, page.Width, band), new Color(0x02, 0x02, 0x04));

        var pad = UiMetrics.Space(40);
        var width = Math.Min(UiMetrics.Control(900), page.Width - pad * 4);
        var lines = _ui.WrapBig(beat.Body, width, UiTypography.Body);
        var titleY = page.Height / 2 + UiMetrics.Space(40);

        // A SOFT SHADE UNDER THE WORDS. The plates are busy exactly where the story is told — a tower's
        // rune bands, a plain of six glows — and a subtitle laid straight on them was guessed at rather
        // than read. Two soft ellipses (the fighters' contact shadow, stretched) darken the art behind
        // the title and the lines only; the picture keeps its full value everywhere else.
        var blockH = UiTypography.Pitch(UiTypography.ScreenTitle) + UiMetrics.Space(18)
                     + lines.Count * UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(24) + UiMetrics.Control(8);
        var textW = _ui.MeasureBig(beat.Title, UiTypography.ScreenTitle, TextFace.Display);
        foreach (var line in lines) textW = Math.Max(textW, _ui.MeasureBig(line, UiTypography.Body));
        var shadeY = titleY + blockH / 2;
        _ui.GroundShadow(_batch, page.Width / 2, shadeY, textW * 2 + UiMetrics.Space(160), blockH * 3, 0.55f * ink);
        _ui.GroundShadow(_batch, page.Width / 2, shadeY, textW + UiMetrics.Space(200), blockH * 2, 0.45f * ink);

        _ui.TextCenterBig(_batch, beat.Title, page.Width / 2, titleY, UiInk.Accent * ink, UiTypography.ScreenTitle, TextFace.Display);

        var y = titleY + UiTypography.Pitch(UiTypography.ScreenTitle) + UiMetrics.Space(18);
        foreach (var line in lines)
        {
            _ui.TextCenterBig(_batch, line, page.Width / 2, y, UiInk.Primary * ink, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        // How far through the story we are — six dots, because six unexplained fades is a hang.
        var dot = UiMetrics.Control(8);
        var gap = UiMetrics.Space(10);
        var run = OpeningScript.Prologue.Count * dot + (OpeningScript.Prologue.Count - 1) * gap;
        for (var i = 0; i < OpeningScript.Prologue.Count; i++)
            _ui.Fill(_batch, new Rectangle(page.Width / 2 - run / 2 + i * (dot + gap), y + UiMetrics.Space(24), dot, dot),
                     i <= _prologueBeat ? UiInk.Accent : UiInk.Secondary * 0.5f);

        var btnH = UiMetrics.Control(48);
        var btnW = UiMetrics.Control(220);
        var last = _prologueBeat + 1 >= OpeningScript.Prologue.Count;
        _prologueNext = new Rectangle(page.Width / 2 - btnW / 2, page.Height - band - UiMetrics.Space(28) - btnH, btnW, btnH);
        _ui.Button(_batch, _prologueNext, last ? "BEGIN" : "NEXT", ChromeMouse, false, true, ButtonStyle.Primary);

        _prologueSkip = new Rectangle(page.Width - UiMetrics.Space(40) - UiMetrics.Control(240),
                                      page.Height - band + UiMetrics.Space(28), UiMetrics.Control(240), UiMetrics.Control(40));
        _ui.Button(_batch, _prologueSkip, "SKIP CINEMATIC", ChromeMouse, false);
    }

    /// <summary>
    /// THE GATE: the deliberate press that starts the career.
    /// </summary>
    /// <remarks>
    /// A fresh save used to land in a fight already in progress with a seven-second toast over it. The
    /// press is the point — the first thing the player does in this game is choose to go down. Its line
    /// is plain: the story has just ended, and from here every word is about what is on the screen.
    /// </remarks>
    private void DrawBeginGate()
    {
        var page = new Rectangle(0, 0, UiKit.Page.Width, UiKit.Page.Height);
        _ui.Fill(_batch, page, OpeningScrim);

        var pad = UiMetrics.Space(40);
        var width = Math.Min(UiMetrics.Control(760), page.Width - pad * 4);
        var body = _ui.WrapBig("This is where your hunt begins.", width - pad * 2, UiTypography.Body);
        var btnH = UiMetrics.Control(56);
        var height = pad + UiTypography.Pitch(UiTypography.ScreenTitle) + UiMetrics.Space(14)
                     + body.Count * UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(28) + btnH + pad;
        _openingCard = new Rectangle((page.Width + NavRailWidth - width) / 2, (page.Height - height) / 2, width, height);

        _ui.Fill(_batch, _openingCard, SlotGround);
        _ui.Plate(_batch, _openingCard, UiInk.Accent);
        _ui.TextCenterBig(_batch, "VERDANT HOLLOW", _openingCard.Center.X, _openingCard.Y + pad,
                          UiInk.Accent, UiTypography.ScreenTitle, TextFace.Display);
        var y = _openingCard.Y + pad + UiTypography.Pitch(UiTypography.ScreenTitle) + UiMetrics.Space(14);
        foreach (var line in body)
        {
            _ui.TextCenterBig(_batch, line, _openingCard.Center.X, y, UiInk.Primary, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        var btnW = Math.Min(_openingCard.Width - pad * 2, UiMetrics.Control(320));
        _openingButton = new Rectangle(_openingCard.Center.X - btnW / 2, _openingCard.Bottom - pad - btnH, btnW, btnH);
        _ui.Button(_batch, _openingButton, "BEGIN THE HUNT", ChromeMouse, false, true, ButtonStyle.Primary);
    }
}
