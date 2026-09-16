using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>
/// THE OPENING, PLAYED FROM A FRESH SAVE BY A HAND THAT IS NOT THERE — and written down as it happens.
/// Dev only: every entry point is inert without its variable.
/// </summary>
/// <remarks>
/// <para>
/// <b>RH_OPENING_AUTOPLAY=&lt;frames&gt;</b> plays a brand-new career through the authored opening with
/// the REAL input path. The hand is a synthetic <see cref="MouseState"/> handed to the same field the
/// real mouse fills, and its cursor goes through the same frame transform (ReadCursor), so a click lands
/// on whatever the screen drew and is hit-tested by the screen's own code: BEGIN THE HUNT on the title,
/// NEXT through the prologue, CONTINUE on every paused card, the lit rail tile, the lit chest, the lit
/// item, EQUIP. It never touches the director or a screen's state. It waits &lt;frames&gt; after a moment
/// begins — reading time — before it clicks; a click that moves nothing is logged and tried again after
/// another wait, and three dead clicks, or ninety seconds on one stage, end the run as a STALL that names
/// the stage. That is the soft-lock detector a unit test cannot be.
/// </para>
/// <para>
/// <b>RH_OPENING_TRACE=&lt;file&gt;</b> writes one frame-stamped line per thing that happened — a stage, a
/// wave beginning, a creature falling, the last fall finishing, the hold engaging and letting go, a cast
/// crossing, a click, a selection, an equip. A filmstrip can show that a card came after a fall; only the
/// trace can say it came after the LAST frame of it. tools/check_opening_trace.py reads it back and fails
/// on every order the 2026-09-11 playtest pass exists to fix.
/// </para>
/// <para>
/// <b>RH_OPENING_FILM=&lt;dir&gt;</b> saves the canvas at the moments the trace marks: each stage as it
/// begins and again just before it is answered, and dense bursts over the first clear, the Signature hold
/// and release, the boss's fall, the chain breaks and the GEAR clicks.
/// </para>
/// </remarks>
public partial class Game1
{
    /// <remarks>
    /// Only with <c>RH_SAVE_DIR</c> set: the hand plays a FRESH career and saves as it goes, and without
    /// an isolated save directory that career would be the developer's own (review 2026-09-11).
    /// </remarks>
    private static readonly int AutoplayDwell =
        int.TryParse(Environment.GetEnvironmentVariable("RH_OPENING_AUTOPLAY"), out var dwell) && dwell > 0
        && Environment.GetEnvironmentVariable("RH_SAVE_DIR") is { Length: > 0 }
            ? dwell : 0;

    /// <summary>A hard ceiling on the whole run — fifteen minutes — for a hang no stage-level stall can see.</summary>
    private const int AutoplayMaxFrames = 60 * 60 * 15;

    /// <summary>Is the autoplayed hand driving the mouse?</summary>
    private static bool Autoplay => AutoplayDwell > 0;

    private static readonly string? OpeningTracePath = RigVariable("RH_OPENING_TRACE");
    private static readonly string? OpeningFilmDir = RigVariable("RH_OPENING_FILM");

    /// <summary>Is any part of the opening rig on?</summary>
    private static bool OpeningRigOn => Autoplay || OpeningTracePath is not null || OpeningFilmDir is not null;

    private static string? RigVariable(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    /// <summary>How long one moment may run before the hand calls it a soft lock: ninety seconds.</summary>
    private const int AutoplayStallFrames = 60 * 90;

    /// <summary>...and how long a LIVE beat may run, which can include the Hunter falling and trying again.</summary>
    private const int AutoplayLiveStallFrames = 60 * 300;

    /// <summary>Clicks spent on one moment before three dead ones are called a soft lock.</summary>
    private const int AutoplayMaxClicks = 3;

    /// <summary>Frames the run keeps going after the opening completes — the return to the hunt, on film.</summary>
    private const int AutoplayTailFrames = 150;

    /// <summary>Where the hand rests when there is nothing to press: off the canvas, over nothing.</summary>
    private static readonly Vector2 RestingHand = new(-4096f, -4096f);

    private MouseState _autoMouse;
    private Vector2? _autoCursor;
    private int _rigFrame;

    /// <summary>
    /// The MOMENT the hand is answering: the stage, whether the title is up, and — inside the one-stage
    /// prologue — which of its six beats. A NEXT click moves the beat and never the stage, so the stage
    /// alone would read five good clicks as five dead ones.
    /// </summary>
    private (OpeningStage Stage, bool Title, int Beat) _autoOn = ((OpeningStage)(-1), false, -1);
    private int _autoOnFrames;
    private int _autoClicks;
    private int _autoTail = -1;
    private readonly List<string> _trace = new();
    private readonly List<string> _filmTags = new();
    private readonly List<(string Tag, int From, int Until, int Every)> _filmBursts = new();

    // THE LAST FRAME'S READINGS, compared against this frame's to find what changed.
    private OpeningStage? _trStage;
    private string? _trScreen;
    private int _trBeat = -1;
    private int _trWaves = -1, _trDowns = -1, _trCasts = -1, _trRewards = -1, _trChests = -1, _trWorn = -1;
    private bool _trFalls, _trShown, _trHeld, _trLanded, _trHoldNext, _trCard, _trDowned;
    private string _trSelected = "?", _trPicked = "?", _trLesson = "-", _trNotice = "-", _trDispatch = "-";

    /// <summary>The notice toast the last Draw actually painted, or null — set by DrawNoticeToast.</summary>
    private string? _rigNoticeDrawn;

    /// <summary>How a dispatch was SURFACED on the last frame, or null — set where the arrival is spent.</summary>
    /// <remarks>
    /// Posting a letter is not surfacing it: the inbox may fill all through the opening and that is
    /// correct, because nothing about it is on screen. What must never happen while the opening runs
    /// is the envelope PULSING, the DISPATCHES lesson lighting, or the list standing open — three
    /// things the player would have to look at instead of the thing they are being shown.
    /// </remarks>
    private string? _rigDispatchSurfaced;

    /// <summary>One frame of the rig: note what the last frame did, then decide the hand for this one.</summary>
    private void OpeningRigFrame()
    {
        _rigFrame++;
        if (Autoplay && _rigFrame > AutoplayMaxFrames)
        {
            Trace($"STALL frame-cap {AutoplayMaxFrames} stage={_opening.Stage} title={_showTitle}");
            FinishAutoplay(4);
        }
        TraceOpeningFacts();
        foreach (var (tag, from, until, every) in _filmBursts)
            if (_rigFrame >= from && _rigFrame < until && (_rigFrame - from) % every == 0) Film(tag);
        _filmBursts.RemoveAll(b => _rigFrame >= b.Until);
        if (Autoplay) _autoMouse = AutoplayHand();
        if (_rigFrame % 300 == 0) FlushOpeningTrace();
    }

    private void Trace(string line) => _trace.Add($"F {_rigFrame} {line}");

    private void Film(string tag)
    {
        if (OpeningFilmDir is not null) _filmTags.Add(tag);
    }

    /// <summary>Photograph <paramref name="tag"/> every <paramref name="every"/> frames, starting <paramref name="start"/> frames from now.</summary>
    private void Burst(string tag, int start, int length, int every)
    {
        if (OpeningFilmDir is null) return;
        var from = _rigFrame + Math.Max(0, start);
        _filmBursts.Add((tag, from, from + Math.Max(1, length), Math.Max(1, every)));
    }

    /// <summary>A mark set from outside the rig — the rail's chain break, on the frame it is armed.</summary>
    private void OpeningRigMark(string line, string? filmTag = null, int burstFrames = 0, int every = 2)
    {
        if (!OpeningRigOn) return;
        Trace(line);
        if (filmTag is not null) Burst(filmTag, 0, burstFrames, every);
    }

    private void TraceOpeningFacts()
    {
        var stage = _opening.Stage;
        if (stage != _trStage)
        {
            Trace($"STAGE {stage}");
            _trStage = stage;
            Film($"stage_{stage}");
            // ...and once more just before the hand answers it: the card as a reader sees it.
            var read = Autoplay ? Math.Max(2, AutoplayDwell - 6) : 60;
            Burst($"read_{stage}", read, 1, 1);
        }
        if (stage == OpeningStage.Prologue && _prologueBeat != _trBeat)
        {
            Trace($"PROLOGUE_BEAT {_prologueBeat}");
            _trBeat = _prologueBeat;
            Burst($"prologue_{_prologueBeat}", Autoplay ? Math.Max(2, AutoplayDwell - 6) : 60, 1, 1);
        }

        var screen = _showTitle ? "Title" : ScreenActivity().ToString();
        if (screen != _trScreen) { Trace($"SCREEN {screen}"); _trScreen = screen; }

        var h = _expedition;
        if (h.WavesBegun != _trWaves) { Trace($"WAVE_BEGIN wave={h.WaveShown}"); _trWaves = h.WavesBegun; }
        if (h.EnemyDownsSeen != _trDowns)
        {
            if (_trDowns >= 0)
            {
                Trace($"ENEMY_DOWN n={h.EnemyDownsSeen} wave={h.WaveShown} playhead={h.PlayheadMs:0} boss={h.BossOnStage}");
                // THE FIRST CLEAR AND THE BOSS'S FALL, ON FILM: every fourth frame until well past the card.
                if (stage is OpeningStage.AwaitFirstReward or OpeningStage.AwaitBossFelled) Burst($"fall_{stage}", 0, 200, 4);
            }
            _trDowns = h.EnemyDownsSeen;
        }
        if (h.ChampionDowned && !_trDowned) Burst("hunter_down", 0, 240, 8);   // what the player sees when it falls: the collapse, the black, the stage back
        Edge(h.ChampionDowned, ref _trDowned, $"HUNTER_DOWN wave={h.WaveShown} boss={h.BossOnStage}", "HUNTER_UP");
        Edge(h.FallsPlayed, ref _trFalls, "FALLS_PLAYED", "FALLS_PENDING");
        Edge(h.ClearShown, ref _trShown, "CLEAR_SHOWN", null);
        Edge(h.HoldNextWave, ref _trHoldNext, "HOLD_NEXT_WAVE on", "HOLD_NEXT_WAVE off");
        if (_rewardsCredited != _trRewards) { if (_trRewards >= 0) Trace($"REWARD n={_rewardsCredited} wave={h.WaveShown}"); _trRewards = _rewardsCredited; }

        if (h.ReplayHeld != _trHeld)
        {
            if (h.ReplayHeld)
            {
                Trace($"HELD at={h.HeldEventAtMs} playhead={h.PlayheadMs:0} wave={h.WaveShown} kind={h.HoldBeforeKind}");
                if (h.HoldBeforeKind == BattleEventKind.Skill) { Film("sig_held"); Burst("sig_card", 1, 240, 20); }
            }
            else Trace($"HELD_END playhead={h.PlayheadMs:0}");
            _trHeld = h.ReplayHeld;
        }
        if (h.SkillCastsSeen != _trCasts)
        {
            if (_trCasts >= 0) Trace($"SKILL at={h.LastSkillCastAtMs} wave={h.WaveShown} n={h.SkillCastsSeen} playhead={h.PlayheadMs:0}");
            _trCasts = h.SkillCastsSeen;
        }
        Edge(h.ReleasedBeatPlayed, ref _trLanded, "RELEASED_PLAYED", null);

        var chests = _forge?.ChestsOpened ?? 0;
        if (chests != _trChests) { if (_trChests >= 0) Trace($"CHEST_OPENED n={chests}"); _trChests = chests; }

        var sel = _gear?.SelectedItemId ?? "-";
        var picked = _gear?.PickedItemId ?? "-";
        if (sel != _trSelected || picked != _trPicked)
        {
            Trace($"GEAR_SELECTED id={sel} picked={picked} screen={screen}");
            (_trSelected, _trPicked) = (sel, picked);
        }
        var worn = Enum.GetValues<GearSlot>().Count(sl => _hunter.Worn(sl) is not null);
        if (worn != _trWorn) { if (_trWorn >= 0) Trace($"WORN n={worn}"); _trWorn = worn; }

        // THE COACH, TOO: a lesson the opening already taught must never be said again after it.
        var lesson = _coach.Showing?.ToString() ?? "-";
        if (lesson != _trLesson) { if (lesson != "-") Trace($"LESSON {lesson} screen={screen}"); _trLesson = lesson; }
        // ...and the notice toast, as painted (only its arrivals: a catch-up frame paints nothing).
        if (_rigNoticeDrawn is { Length: > 0 } notice && notice != _trNotice) { Trace($"NOTICE {notice}"); _trNotice = notice; }
        _rigNoticeDrawn = null;
        // ...and a DISPATCH the moment it is SURFACED rather than merely posted: the envelope's
        // arrival pulse, the lesson that points at it, or the reading panel standing open.
        var mail = _rigDispatchSurfaced
                   ?? (_showDispatches ? "list" : null)
                   ?? (_coach.Showing == OnboardingLessonId.FirstDispatchOpened ? "lesson" : null)
                   ?? "-";
        if (mail != _trDispatch) { if (mail != "-") Trace($"DISPATCH {mail}"); _trDispatch = mail; }
        _rigDispatchSurfaced = null;

        var card = OpeningUp && !_openingCard.IsEmpty;
        if (card != _trCard)
        {
            if (card) Trace($"CARD {stage} {_openingCard.X},{_openingCard.Y},{_openingCard.Width},{_openingCard.Height}");
            else Trace("CARD_GONE");
            _trCard = card;
        }
    }

    private void Edge(bool now, ref bool was, string rise, string? fall)
    {
        if (now == was) return;
        if (now) Trace(rise); else if (fall is not null) Trace(fall);
        was = now;
    }

    /// <summary>The hand for this frame: where it rests, and whether it presses.</summary>
    private MouseState AutoplayHand()
    {
        var on = (_opening.Stage, _showTitle, _opening.Stage == OpeningStage.Prologue ? _prologueBeat : -1);
        if (on != _autoOn) { _autoOn = on; _autoOnFrames = 0; _autoClicks = 0; }
        _autoOnFrames++;

        // DONE. The opening is over: let the return to the hunt play on film, then leave.
        if (!_showTitle && _opening.Stage == OpeningStage.Complete)
        {
            if (_autoTail < 0) { _autoTail = AutoplayTailFrames; Trace("COMPLETE"); Burst("after", 1, AutoplayTailFrames, 30); }
            _autoCursor = RestingHand;
            if (--_autoTail == 0) FinishAutoplay(0);
            return Hand(pressed: false);
        }

        var target = AutoplayTarget();
        _autoCursor = target ?? RestingHand;
        var press = false;
        if (target is { } t && _autoOnFrames >= AutoplayDwell * (_autoClicks + 1) && _autoClicks < AutoplayMaxClicks)
        {
            press = true;
            _autoClicks++;
            var what = _showTitle ? "Title" : _opening.Stage.ToString();
            Trace($"CLICK {what} at={t.X:0},{t.Y:0} n={_autoClicks}");
            if (_opening.Stage is OpeningStage.IntroduceSignature or OpeningStage.ForceChestOpen
                or OpeningStage.ForceItemSelect or OpeningStage.ForceEquip or OpeningStage.ShowEquipped)
                Burst($"click_{what}", 0, 120, 3);
        }

        // A LIVE BEAT MAY LEGITIMATELY TAKE A WHILE: the fresh Hunter can lose to the tutorial boss and
        // walk the first five waves again (measured 2026-09-11). Five minutes there, ninety seconds on
        // any beat that is waiting for the hand.
        var stallAt = target is null ? AutoplayLiveStallFrames : AutoplayStallFrames;
        if (_opening.Running
            && (_autoOnFrames > stallAt
                || (_autoClicks >= AutoplayMaxClicks && _autoOnFrames > AutoplayDwell * (AutoplayMaxClicks + 2))))
        {
            Trace($"STALL {_opening.Stage} frames={_autoOnFrames} clicks={_autoClicks}");
            FinishAutoplay(3);
        }
        return Hand(press);
    }

    /// <summary>
    /// The one control the current moment asks for, in canvas space — the same rectangle the screen drew
    /// and will hit-test — or null when nothing is being asked (a live beat, a wait).
    /// </summary>
    private Vector2? AutoplayTarget()
    {
        // The title's first plate: BEGIN THE HUNT on a fresh career (DrawTitle lays it at 640,608 640x96).
        if (_showTitle) return new Vector2(960f, 656f);
        if (_showSettings || _showHelp || !_opening.Running) return null;
        switch (_opening.Stage)
        {
            case OpeningStage.Prologue: return Centre(_prologueNext);
            case OpeningStage.AwaitBegin: return Centre(_openingButton);
        }
        if (_opening.WantsAcknowledgement) return Centre(_openingButton);
        if (_opening.ForcedNav is { } nav) return NavTile(nav) is { Length: > 0 } tile ? Centre(tile[0]) : null;
        if (_opening.ForcedTarget is not null) return OpeningHoles() is { Length: > 0 } holes ? Centre(holes[0]) : null;
        return null;
    }

    private static Vector2? Centre(Rectangle r) => r.IsEmpty ? null : new Vector2(r.Center.X, r.Center.Y);

    private static MouseState Hand(bool pressed)
        => new(0, 0, 0, pressed ? ButtonState.Pressed : ButtonState.Released,
               ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private void FinishAutoplay(int code)
    {
        Trace(code == 0 ? "EXIT ok" : $"EXIT {code}");
        FlushOpeningTrace();
        Environment.ExitCode = code;
        Exit();
    }

    private void FlushOpeningTrace()
    {
        if (OpeningTracePath is null) return;
        var dir = Path.GetDirectoryName(OpeningTracePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllLines(OpeningTracePath, _trace);
    }

    /// <summary>Save the canvas if a moment asked for it this frame. Called after the frame is drawn.</summary>
    private void OpeningFilmShot()
    {
        if (OpeningFilmDir is null || _filmTags.Count == 0) return;
        var name = $"{_rigFrame:D5}_{string.Join("+", _filmTags.Distinct())}.png";
        _filmTags.Clear();
        Directory.CreateDirectory(OpeningFilmDir);
        using var fs = File.Create(Path.Combine(OpeningFilmDir, name));
        _canvas.SaveAsPng(fs, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
    }
}
