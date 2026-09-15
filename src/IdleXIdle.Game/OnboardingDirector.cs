using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>
/// WHEN a lesson is spoken, and when the game shuts up.
/// </summary>
/// <remarks>
/// <para>
/// The split this class exists to hold: <b>Core decides what is TRUE, the director decides what is
/// SAID.</b> <see cref="OnboardingLessons"/> is a pure catalogue over facts — no time, no frames, no
/// rectangles — and everything in here is the other half: quiet periods, what counts as a bad moment
/// to interrupt, which lesson is currently on screen, and the counters a telemetry sink reads.
/// </para>
/// <para>
/// Nothing here can reach the simulation. The fight resolves headlessly, offline and at whatever speed
/// it likes; a lesson never delays a wave, never waits for an animation and never gates a fact. If
/// this whole class were deleted the game would still play correctly — it would simply say nothing.
/// </para>
/// <para>
/// <b>ONE AT A TIME, AND NOT DURING A REWARD.</b> Two prompts at once teaches a player to read
/// neither, and the loudest moments in this game — a chest cracking, a champion falling, a region
/// conquered — are exactly when a queued lesson would land on top of something the player is already
/// looking at. So the director holds a QUIET clock that a reward beat starts, and refuses to speak
/// while a modal production surface is up unless the lesson belongs to that surface.
/// </para>
/// </remarks>
public sealed class OnboardingDirector
{
    /// <summary>Silence after a lesson is satisfied, so a deed's reward is not stepped on by the next ask.</summary>
    public const float QuietAfterLesson = 3.0f;

    /// <summary>Silence after a reward beat the game raised on its own — a chest, a conquest, a fall.</summary>
    public const float QuietAfterReward = 2.5f;

    /// <summary>How long a raised OBSERVE beat stands on screen. It asks for nothing, so it leaves by itself.</summary>
    public const float ObserveDwell = 6.0f;

    private float _quiet;
    private OnboardingLessonId? _showing;
    private OnboardingLessonId? _raised;
    private float _raisedFor;
    private readonly HashSet<OnboardingLessonId> _raisedEver = new();
    private readonly HashSet<OnboardingLessonId> _muted = new();
    private readonly Dictionary<OnboardingLessonId, int> _eligibleSeen = new();
    private readonly Dictionary<OnboardingLessonId, int> _shownCount = new();
    private readonly HashSet<OnboardingLessonId> _completedSeen = new();

    /// <summary>Seconds the current lesson has been on screen — the stall figure telemetry wants.</summary>
    public float ShowingFor { get; private set; }

    /// <summary>The lesson to present right now, or null for silence.</summary>
    public OnboardingLessonId? Showing => _showing;

    /// <summary>Is the game deliberately quiet at this instant?</summary>
    public bool Quiet => _quiet > 0f;

    /// <summary>Start a quiet period — call it on a reward beat, not on every frame of one.</summary>
    public void Hush(float seconds) => _quiet = MathF.Max(_quiet, seconds);

    /// <summary>
    /// Silence one lesson's PRESENTATION until the game restarts.
    /// </summary>
    /// <remarks>
    /// This is what a card's × does, and it is deliberately not completion: the fact stays false, the
    /// deed stays undone, and every gate that reads it is untouched. A player who wants the game to
    /// stop coaching entirely has SKIP GUIDANCE, which is one global setting rather than a dozen
    /// little lies about what they have learned.
    /// </remarks>
    public void Mute(OnboardingLessonId id) => _muted.Add(id);

    /// <summary>
    /// SAY THIS, BECAUSE IT JUST HAPPENED.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three beats in this game cannot be chosen by the selector, because they are not things the
    /// player can be asked to do: the hunter's own signature firing, the first boss, the first region
    /// falling. They are moments — they happen once, they are over in a second, and the only useful
    /// thing onboarding can do with them is name what the player is already looking at. That is what
    /// OBSERVE means, and it is why those three are <c>Eligible => false</c> in the catalogue: the game
    /// raises them at the instant they occur, or they are not raised at all.
    /// </para>
    /// <para>
    /// The caller decides WHETHER — it holds the fact ("this is the first boss") — and this decides
    /// WHEN, which is: once ever, never over a surface the player is reading, and for a fixed dwell
    /// after which it leaves without being dismissed. It grants nothing and completes nothing; if the
    /// beat is never raised the game plays identically and one sentence goes unsaid.
    /// </para>
    /// </remarks>
    public void Raise(OnboardingLessonId id)
    {
        if (!_raisedEver.Add(id)) return;
        _raised = id;
        _raisedFor = ObserveDwell;
    }

    /// <summary>Clear every mute and the clock — a fresh game teaches from the beginning again.</summary>
    public void Reset()
    {
        _quiet = 0f;
        _showing = null;
        _raised = null;
        _raisedFor = 0f;
        ShowingFor = 0f;
        _raisedEver.Clear();
        _muted.Clear();
        _eligibleSeen.Clear();
        _shownCount.Clear();
        _completedSeen.Clear();
    }

    /// <summary>
    /// What the game is doing that a lesson must not talk over.
    /// </summary>
    /// <param name="RewardUp">A chest reveal, a welcome panel — a surface the player is reading.</param>
    /// <param name="ModalUp">Settings, help, a tour: production UI that owns the input.</param>
    /// <param name="ReportUp">The run log is open. The fall lessons belong here; nothing else does.</param>
    public readonly record struct Busy(bool RewardUp, bool ModalUp, bool ReportUp);

    /// <summary>
    /// RIG ONLY: hold one lesson on screen so its presentation can be photographed.
    /// </summary>
    /// <remarks>
    /// Every lesson is a state that depends on facts a capture fixture does not have — a fresh
    /// account, a fall, a gem in the bag, an offline payout — so without this most of the catalogue
    /// could never be looked at, at any UI density. That is this project's own recurring fault, and
    /// the reason a posed state is worth a field: a lesson nobody has photographed is a lesson nobody
    /// has checked the copy, the brackets or the reflow of. Null in play, always.
    /// </remarks>
    public OnboardingLessonId? Forced { get; set; }

    /// <summary>
    /// Advance the clock and choose at most one lesson.
    /// </summary>
    /// <remarks>
    /// Called once per frame from the host's Update with the facts it already has. It is a pure
    /// function of (facts, busy, clock) — no side effects on the game, so a frame where nothing is
    /// eligible costs one dictionary walk over twenty enum values.
    /// </remarks>
    public void Update(float dt, LessonFacts facts, Busy busy)
    {
        if (Forced is { } posed) { _showing = posed; ShowingFor += dt; return; }

        _quiet = MathF.Max(0f, _quiet - dt);

        // COMPLETION IS WATCHED, NOT ASSUMED. A lesson satisfied while it was on screen starts the
        // quiet period, which is what stops the next ask from landing on top of the reward for this
        // one. It also fires the completion counter exactly once.
        foreach (var id in OnboardingLessons.All)
        {
            if (!OnboardingLessons.Completed(id, facts) || !_completedSeen.Add(id)) continue;
            if (_showing == id) Hush(QuietAfterLesson);
        }

        if (facts.GuidanceOff) { _showing = null; _raised = null; ShowingFor = 0f; return; }

        foreach (var id in OnboardingLessons.EligibleNow(facts))
            if (!_eligibleSeen.ContainsKey(id)) _eligibleSeen[id] = 1;

        // ── A RAISED BEAT OWNS THE SLOT WHILE IT LASTS. ──────────────────────────────────────────
        //
        // It is not chosen against the others and does not compete on priority: the game said this
        // happened, so this is what the card says until its dwell runs out. It still waits for a
        // surface the player is reading — a beat that arrives under the conquest panel is a beat
        // nobody saw — and it holds its dwell while it waits rather than burning it behind the panel.
        if (_raised is { } beat)
        {
            if (busy.RewardUp || busy.ModalUp || busy.ReportUp)
            {
                if (_showing is not null) { _showing = null; ShowingFor = 0f; }
                return;
            }
            _raisedFor -= dt;
            if (_raisedFor > 0f)
            {
                if (_showing != beat) { _showing = beat; ShowingFor = 0f; Count(beat); }
                else ShowingFor += dt;
                return;
            }
            // Its time is up. A short silence after it, so the next ASK is not read as part of it.
            _raised = null;
            _showing = null;
            ShowingFor = 0f;
            Hush(QuietAfterReward);
            return;
        }

        var next = OnboardingLessons.Next(facts, _muted);

        // ── WHEN NOT TO SPEAK. ───────────────────────────────────────────────────────────────────
        //
        // A lesson that BELONGS to the moment is allowed through it — READ THE LOG has to be sayable
        // while the log is what the player is being sent to, and MAKE ONE CHANGE has to survive the
        // report being open. Everything else waits for the surface to close.
        if (next is { } id2 && !BelongsToTheMoment(id2, busy))
        {
            if (busy.RewardUp || busy.ModalUp || busy.ReportUp) next = null;
        }
        // ...AND NOTHING IS SAID OVER A MODAL, not even a lesson that belongs to the moment. Those
        // exemptions were written for the run log and the reward panel, and they let READ THE LOG through
        // the authored opening's own ModalUp — mid-opening, right after the tutorial boss's first win
        // (autoplayed opening, 2026-09-11). It waits, and is said when the modal hands the game back.
        if (busy.ModalUp) next = null;
        if (_quiet > 0f) next = null;

        if (next != _showing)
        {
            _showing = next;
            ShowingFor = 0f;
            if (next is { } shown) Count(shown);
        }
        else if (_showing is not null) ShowingFor += dt;
    }

    /// <summary>One more presentation of this lesson, for the ledger.</summary>
    private void Count(OnboardingLessonId id) => _shownCount[id] = _shownCount.GetValueOrDefault(id) + 1;

    /// <summary>Is this lesson about the very surface that is up?</summary>
    private static bool BelongsToTheMoment(OnboardingLessonId id, Busy busy)
        => id switch
        {
            // The fall loop's two lessons live on and around the run log: the first sends the player
            // INTO it, and the second is what the report is for.
            OnboardingLessonId.FirstFailureReport => busy.ReportUp || !busy.RewardUp,
            OnboardingLessonId.FirstPostFailureChange => true,
            // ...and the camp's lesson is raised BY the return panel, so it may speak over one.
            OnboardingLessonId.FirstWarrenReturn => busy.RewardUp,
            _ => false,
        };

    // ── TELEMETRY ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One line per lesson the session touched: eligible, shown, completed, muted.
    /// </summary>
    /// <remarks>
    /// Deliberately a counter dump rather than an analytics dependency — the project has no event sink
    /// and this task is not the place to add one. It is printed by the capture rig's ledger dial, so a
    /// playtest can answer the question that actually matters: not "did they finish the tutorial", but
    /// <b>did they open their first failure report, change something, and go back down</b>.
    /// </remarks>
    public IReadOnlyList<string> Telemetry(LessonFacts facts)
    {
        var rows = new List<string>();
        foreach (var id in OnboardingLessons.All)
        {
            var eligible = _eligibleSeen.ContainsKey(id);
            var shown = _shownCount.GetValueOrDefault(id);
            var done = OnboardingLessons.Completed(id, facts);
            if (!eligible && shown == 0 && !done) continue;
            rows.Add($"lesson\tid={id}\teligible={eligible}\tshown={shown}\tcompleted={done}"
                     + $"\tmuted={_muted.Contains(id)}\tskipped={facts.GuidanceOff}");
        }
        // THE MILESTONE THAT MATTERS, said once and in one line.
        rows.Add($"lesson\tftue_loop_lived={facts.ReportOpenedEver && facts.ChangedAfterFall && facts.RetriedAfterChange}"
                 + $"\tfalls={facts.Falls}\tdeepest={facts.DeepestWave}");
        return rows;
    }
}
