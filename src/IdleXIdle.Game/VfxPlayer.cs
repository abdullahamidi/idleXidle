using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>
/// Draws the fight's effects — every one of them placed by the VFX contract, never by a pixel offset.
/// </summary>
/// <remarks>
/// <para>
/// A caller names a <see cref="VfxProfile"/> and a <see cref="VfxSubject"/>. It does not name a
/// position and it does not name a size. The rectangle is resolved inside the draw pass, against the
/// visual bounds the screen published for that subject THIS frame — which is what makes an effect
/// follow the champion's 40-px lunge, land on the creature the event named rather than on the one that
/// stood there last frame, and be the same fraction of every hunter from THE OATHBOUND's 162-px
/// silhouette to QUIVER's 373.
/// </para>
/// <para>
/// What this replaced: <c>Play(key, x, y, scale)</c>, where <c>scale</c> was a multiplier of a 104-px
/// base unit and every caller worked its own pixels out — five integer size buckets for creatures,
/// four for a bolt, a hand-tuned <c>Bottom - 156</c> for the heal, and two held effects that
/// re-derived the renderer's own sizing formula minus its canvas-scale term.
/// </para>
/// <para>
/// A missing effect asset is still a silent no-op at runtime — the game must degrade to no-VFX rather
/// than crash — but it is no longer INVISIBLE: every profile's key is a literal in one table and
/// <c>vfx_asset_test</c> walks it. That silence is what hid PRESS, WEEP and WILT drawing nothing at all.
/// </para>
/// </remarks>
public sealed class VfxPlayer
{
    /// <summary>One live effect: its profile, its subject, its clock, and its resolved rectangle.</summary>
    private sealed class Anim
    {
        public required VfxProfile Profile { get; init; }
        public required string Key { get; init; }
        public required Texture2D Sheet { get; init; }
        public required int FrameW { get; init; }
        public required int FrameH { get; init; }
        public required int Frames { get; init; }
        public required VfxSubject Subject { get; set; }
        public VfxSubject? TravelTo { get; init; }
        public required Color Tint { get; set; }
        public required float SecondsPerFrame { get; init; }
        public float Elapsed;

        /// <summary>Its place in the order effects were launched (1, 2, 3 …) — see <see cref="Launched"/>.</summary>
        public long Seq;

        /// <summary>How long this has waited for a subject nobody published. Its own clock does not run.</summary>
        public float Waiting;

        /// <summary>Where the effect resolved to. Recomputed every frame when the profile is Pinned.</summary>
        public VfxPlacement Placement;
        public bool Resolved;
        public bool OverBudget;

        /// <summary>
        /// The ratio the placement would have had if the budget had not clamped it.
        /// </summary>
        /// <remarks>
        /// The DRAWN ratio is useless as a diagnosis: it is 1.25 for every over-budget effect by
        /// construction, so a debug view reading it printed "ratio 1.25 OVER BUDGET" — a number
        /// inside the band next to a verdict saying it is not, and its red branch could never fire.
        /// This is the number the dump prints and the asset order is written from.
        /// </remarks>
        public float HonestRatio;

        /// <summary>The content centre it starts at, and the one it eases toward. Equal unless it travels.</summary>
        public Point From, To;

        public bool Loop => Profile.Lifetime == VfxLifetime.Held;

        /// <summary>0 at the first frame, 1 at the last — the travel's and the fade's clock.</summary>
        public float Life => Math.Clamp(Elapsed / MathF.Max(0.0001f, SecondsPerFrame * Frames), 0f, 1f);

        public int CurrentFrame => Loop
            ? (int)(Elapsed / SecondsPerFrame) % Math.Max(1, Frames)
            : Math.Min(Frames - 1, (int)(Elapsed / SecondsPerFrame));

        public bool Done => !Loop && Elapsed >= SecondsPerFrame * Frames;

        /// <summary>
        /// The offset from the resolved rectangle to where the effect is NOW — zero unless it travels.
        /// </summary>
        /// <remarks>
        /// Eased out, not linear: a thrown thing leaves fast and arrives slowing, and a linear crossing
        /// read as a sliding decal. The clock is <see cref="Life"/>, so the travel finishes exactly as
        /// the last frame does however fast the strip is played. A HELD effect never travels — it is
        /// re-placed by its owner every frame and has no end point to ease toward.
        /// </remarks>
        public Point Drift
        {
            get
            {
                if (Loop || (To.X == From.X && To.Y == From.Y)) return Point.Zero;
                var t = Life;
                t = 1f - (1f - t) * (1f - t);
                return new Point((int)((To.X - From.X) * t), (int)((To.Y - From.Y) * t));
            }
        }

        /// <summary>
        /// 1 for most of the clip, easing to 0 across its final third — so every effect ENDS.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The strips do not agree on how to finish. Measured per frame as a fraction of the frame that
        /// is opaque, some decay then POP BACK on the last frame, some flash once more after reaching
        /// empty, and two are loops played once as a one-shot. So half the effects in the fight ended by
        /// simply vanishing at full brightness. A fade fixes every shape of ending at once — a decaying
        /// burst gets a soft tail, a looping flame gets an exit, and a stray final frame is dim enough
        /// not to read as a second hit.
        /// </para>
        /// <para>
        /// Cubed rather than linear because these draw ADDITIVELY: additive alpha reads far brighter
        /// than its number suggests, so a linear ramp still looks like a hard cut at the end. The fade
        /// rides the draw colour's opacity, which <see cref="VfxBlend.Light"/> applies squared — so the
        /// light of the tail is k⁶, which is what this curve was tuned against.
        /// </para>
        /// </remarks>
        public float Fade
        {
            get
            {
                if (Loop) return 1f;   // a held effect never fades — the caller owns its brightness
                var total = SecondsPerFrame * Frames;
                if (total <= 0f) return 1f;
                const float tail = 0.35f;
                var t = Math.Clamp(Elapsed / total, 0f, 1f);
                if (t <= 1f - tail) return 1f;
                var k = (1f - t) / tail;
                return k * k * k;
            }
        }
    }

    /// <summary>Identity of a HELD effect: its profile AND its subject.</summary>
    /// <remarks>
    /// It used to be the asset key alone, so two persistent effects resolving to the same strip would
    /// silently collapse into one instance with one position and one tint. That worked only by luck —
    /// today's field and barrier happen to use different art. A per-creature persistent state would
    /// have hit it on the first swarm wave.
    /// </remarks>
    private readonly record struct HoldId(string ProfileId, VfxSubject Subject);

    private readonly UiKit _ui;
    private readonly List<Anim> _active = new();

    /// <summary>
    /// Effects that are HELD rather than fired: refreshed every frame by their owner, looping, never fading.
    /// </summary>
    /// <remarks>
    /// The field is not an event, it is a STATE — "sürekli açık olacak, asla sönmeyecek" (designer,
    /// 2026-08-28). Spawning a one-shot on every tick could only ever look like a thing that flashes and
    /// dies, however the strip was drawn, because a fired effect is defined by ending. A held effect is
    /// owned by its caller: it exists exactly as long as the caller keeps asking for it, at whatever
    /// brightness the caller passes THIS frame, which is what lets the fight peak it on a hit.
    /// </remarks>
    private readonly Dictionary<HoldId, Anim> _held = new();
    private readonly HashSet<HoldId> _heldThisFrame = new();

    /// <summary>Where the figures are this frame. Without it nothing can resolve and nothing draws.</summary>
    public IVfxBoundsSource? Bounds;

    /// <summary>When set (the Hunt arena pass), the additive + restore batches use it so the glow is scissor-
    /// clipped to the arena along with everything else. Null elsewhere = default (unclipped) rasterizer.</summary>
    public RasterizerState? Rasterizer;

    /// <summary>Settings' FIGHT EFFECTS switch. Off = Play() is a no-op; the creature clips still run.</summary>
    public bool Enabled = true;

    /// <summary>
    /// The rasterizer the caller's batch is reopened with after an effects pass. Each pass ends the
    /// caller's batch, draws additively with <see cref="Rasterizer"/>, then reopens — and it used to
    /// reopen with the same (null) rasterizer, so everything the caller drew AFTER an effect (callouts,
    /// the red flash, the overlay) silently lost the arena scissor whenever an effect was in flight.
    /// </summary>
    public RasterizerState? RestoreRasterizer;

    /// <summary>
    /// <c>RH_VFX_DUMP=1</c>: print one line per effect the first time it resolves.
    /// </summary>
    /// <remarks>
    /// The numbers first, the picture second — the capture rig's own history says that is the only way
    /// these get believed. A screenshot cannot tell you that a dome is 0.32x the champion's height; a
    /// line saying <c>ratio=1.881 OVER</c> can.
    /// </remarks>
    public static readonly bool DumpEnabled =
        Environment.GetEnvironmentVariable("RH_VFX_DUMP") is "1" or "true";

    /// <summary>What the debug view draws: every live effect as it actually resolved.</summary>
    /// <remarks>
    /// <paramref name="NativeRatio"/> is the UNCLAMPED ratio — the size the design asked for against
    /// the size the strip was drawn at. The clamped one is 1.25 for every over-budget effect and so
    /// diagnoses nothing; this is the number the dump prints and the art order is written from.
    /// </remarks>
    public readonly record struct DebugItem(string Id, string Key, VfxSubject Subject, VfxLayer Layer,
                                            Rectangle Frame, Rectangle Content, Point Anchor,
                                            float NativeRatio, bool Held, bool OverBudget);

    private readonly List<DebugItem> _debug = new();

    /// <summary>The effects resolved on the last drawn frame, in draw order. Empty unless something drew.</summary>
    public IReadOnlyList<DebugItem> DebugItems => _debug;

    public VfxPlayer(UiKit ui) => _ui = ui;

    /// <summary>How long an effect waits for a subject nobody published before it is dropped.</summary>
    /// <remarks>
    /// An unresolvable subject used to be a magic fallback — <c>(_rowCentreX, _rowTopY + 110)</c>, an
    /// imaginary row 110 px down. Waiting is the honest answer: the effect holds its clock and resolves
    /// on the first frame it can. The grace exists so a spawn aimed at a slot that never draws (a wave
    /// that ended under it) cannot leak.
    /// </remarks>
    private const float WaitingGraceSeconds = 0.5f;

    /// <summary>
    /// Fire a one-shot effect on <paramref name="subject"/>.
    /// </summary>
    /// <param name="p">The placement rule.</param>
    /// <param name="assetKey">
    /// The resolved strip key — the caller expands the profile's base key for the active character
    /// (HuntScreen.FxFor), and the held field passes the casting skill's own art.
    /// </param>
    /// <param name="subject">Whose bounds it is placed against.</param>
    /// <param name="tint">Per-cast colour: the Source glow, the shield's steel, the field's brightness.</param>
    /// <param name="travelTo">For <see cref="VfxTravel.ToTarget"/>, the subject it crosses to.</param>
    /// <param name="fps">Overrides the profile's frame rate. Only a fixture needs it.</param>
    public void Play(VfxProfile p, string assetKey, VfxSubject subject, Color tint,
                     VfxSubject? travelTo = null, float? fps = null)
    {
        if (!Enabled) return;
        if (Spawn(p, assetKey, subject, tint, travelTo, fps) is { } a)
        {
            a.Seq = ++_launched;
            _active.Add(a);
        }
    }

    private long _launched;

    /// <summary>The sequence number of the last effect <see cref="Play"/> launched — 0 before the first.</summary>
    /// <remarks>
    /// Read it either side of a call to know which effects that call launched, then ask
    /// <see cref="AnyPlaying"/> whether any of them is still on screen. It is how the fight waits for
    /// ONE cast's own effect to finish without keeping a timer that could drift from the strip's length.
    /// </remarks>
    public long Launched => _launched;

    /// <summary>
    /// Is any effect launched after <paramref name="after"/>, up to and including <paramref name="upTo"/>,
    /// still playing? False for an empty range, and after <see cref="Clear"/>.
    /// </summary>
    public bool AnyPlaying(long after, long upTo)
    {
        if (upTo <= after) return false;
        foreach (var a in _active)
            if (a.Seq > after && a.Seq <= upTo) return true;
        return false;
    }

    /// <summary>
    /// Keep a looping effect alive for this frame, on this subject, at this brightness.
    /// </summary>
    /// <remarks>
    /// Call it every frame the effect should exist; stop calling it and it is gone on the next Update.
    /// There is no Stop() on purpose — a held effect that outlives its reason is exactly the kind of
    /// thing that gets left on screen when a wave ends, which this codebase has shipped before.
    /// </remarks>
    public void Hold(VfxProfile p, string assetKey, VfxSubject subject, Color tint)
    {
        if (!Enabled) return;
        ArgumentNullException.ThrowIfNull(p);
        var id = new HoldId(p.Id, subject);
        if (!_held.TryGetValue(id, out var a) || a.Key != assetKey)
        {
            if (Spawn(p, assetKey, subject, tint, null, null) is not { } fresh) return;
            _held[id] = a = fresh;
        }
        _heldThisFrame.Add(id);
        a.Subject = subject;
        a.Tint = tint;      // live: the fight brightens it on a hit and dims it back between
    }

    private Anim? Spawn(VfxProfile p, string assetKey, VfxSubject subject, Color tint,
                        VfxSubject? travelTo, float? fps)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (_ui.Assets.Get(assetKey) is not { } sheet) return null;

        // The frame count is DECLARED by the profile, never inferred from the aspect. Every fx strip on
        // disk is 4096x512, and the old player read the count out of that ratio — so a strip regenerated
        // at another aspect would have been sliced wrong with no error anywhere.
        var frames = Math.Max(1, p.Frames);
        var frameW = Math.Max(1, sheet.Width / frames);
        if (sheet.Width % frames != 0)
            System.Diagnostics.Debug.WriteLine(
                $"VFX strip '{assetKey}' is {sheet.Width}px wide, which is not {frames} whole frames (profile {p.Id}).");

        return new Anim
        {
            Profile = p,
            Key = assetKey,
            Sheet = sheet,
            FrameW = frameW,
            FrameH = sheet.Height,
            Frames = frames,
            Subject = subject,
            TravelTo = travelTo,
            Tint = tint,
            SecondsPerFrame = 1f / MathF.Max(1f, fps ?? p.Fps),
            // A negative start is a DELAY: the effect exists but is not drawn until its clock crosses
            // zero. The death plume uses it to rise after the creature's own fall, not over it.
            Elapsed = -MathF.Max(0f, p.DelaySeconds),
        };
    }

    public void Update(float dt)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var a = _active[i];
            // An effect that has never found its subject holds its own clock — it must not fade out
            // while waiting for the frame it can finally be placed on.
            if (!a.Resolved && a.Elapsed >= 0f)
            {
                a.Waiting += dt;
                if (a.Waiting > WaitingGraceSeconds) _active.RemoveAt(i);
                continue;
            }
            a.Elapsed += dt;
            if (a.Done) _active.RemoveAt(i);
        }
        // A held effect lives only as long as someone asked for it THIS frame.
        foreach (var id in _held.Keys.ToList())
        {
            if (!_heldThisFrame.Contains(id)) { _held.Remove(id); continue; }
            _held[id].Elapsed += dt;
        }
        _heldThisFrame.Clear();
    }

    /// <summary>Effects that sit UNDER the figures: the ground ring, and the field behind the body.</summary>
    public void DrawUnder(SpriteBatch b) => DrawPass(b, under: true);

    /// <summary>Effects that sit OVER the figures: every blow, and the overhead sigils above them.</summary>
    public void DrawOver(SpriteBatch b) => DrawPass(b, under: false);

    /// <summary>
    /// Which of the two passes a layer belongs to: the two UNDER tiers draw before the figures.
    /// </summary>
    /// <remarks>
    /// Public because the partition IS the guarantee — §68 asks that the barrier not render behind the
    /// background nor over every HUD element, and that is an ordering fact a screenshot cannot prove.
    /// </remarks>
    public static bool IsUnderLayer(VfxLayer l) => l is VfxLayer.GroundUnder or VfxLayer.BehindSubject;

    /// <summary>
    /// One of the two additive sub-passes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Splitting the effects into two passes is what gives the brief's §68 layer vocabulary anything to
    /// mean: before this, one flat list drew after both figures, so there was no GroundUnder and no
    /// BehindSubject and the shield could only ever be over the champion. The batch cost is at most two
    /// more Begin/End pairs a frame — well inside the 10–20 planning budget — and each pass early-outs
    /// when its own layers are empty.
    /// </para>
    /// <para>
    /// THE PASS IS DELIBERATELY UNSCISSORED. The arena sets <c>Rasterizer = null</c> on purpose: a clip
    /// edge cut bursts flat against an invisible rectangle and "gave the arena away". The rail panels
    /// drawn afterwards cover anything that strays. Do NOT re-scissor while widening effects.
    /// </para>
    /// </remarks>
    private void DrawPass(SpriteBatch b, bool under)
    {
        if (under) _debug.Clear();   // the under pass runs first, so it owns the reset

        // Held first, then one-shots in spawn order, then ordered by layer — so the standing barrier
        // sits under the blows that land on it, exactly as it did before.
        var list = _held.Values.Concat(_active)
            .Where(a => IsUnderLayer(a.Profile.Layer) == under && a.Elapsed >= 0f)
            .OrderBy(a => (int)a.Profile.Layer)
            .ToList();
        if (list.Count == 0) return;

        // Additive. These are radial GLOW effects; in the caller's AlphaBlend batch their soft edges read
        // as hard ring OUTLINES (the "reticles" bug). End the caller's batch, run additive, then restore
        // AlphaBlend for what draws after — with the arena's own rasterizer, so callouts and the overlay
        // keep the arena's edge whether or not a burst is live.
        // PREMULTIPLIED additive, and it must stay that: the textures are premultiplied at load, so the
        // stock source-alpha additive state would square every soft pixel's alpha. See VfxBlend / ADR-009.
        var opened = false;
        foreach (var a in list)
        {
            if (!Resolve(a)) continue;
            if (!opened)
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, VfxBlend.PremultipliedAdditive, SamplerState.LinearClamp, null, Rasterizer);
                opened = true;
            }
            var src = new Rectangle(a.CurrentFrame * a.FrameW, 0, a.FrameW, a.FrameH);
            var drift = a.Drift;
            var dest = a.Placement.Frame;
            dest.Offset(drift);
            b.Draw(a.Sheet, dest, src, VfxBlend.Light(a.Tint * a.Fade));

            var content = a.Placement.Content;
            content.Offset(drift);
            _debug.Add(new DebugItem(a.Profile.Id, a.Key, a.Subject, a.Profile.Layer, dest, content,
                                     a.Placement.Anchor + drift, a.HonestRatio, a.Loop, a.OverBudget));
        }
        if (!opened) return;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RestoreRasterizer ?? Rasterizer);
    }

    /// <summary>
    /// Turn a subject into pixels. Detached effects resolve once; Pinned ones re-resolve every frame.
    /// </summary>
    private bool Resolve(Anim a)
    {
        if (a.Resolved && a.Profile.Follow == VfxFollow.Detached) return true;
        if (Bounds is null || !Bounds.TryBounds(a.Subject, out var s)) return a.Resolved;

        var content = _ui.Content(a.Key, a.Frames);
        // TWICE, and on purpose: `honest` is the size the design ASKED for and is what the ratio is
        // reported from, while the drawn placement is clamped to the §73 budget. A badly sized asset
        // therefore shows up as a number in the dump and a red caption in the debug view — never as a
        // giant runtime multiplier quietly making a 512-px strip 963 px tall (LAW 16).
        var honest = VfxResolver.Resolve(a.Profile, s, content, a.FrameW, a.FrameH);
        a.Placement = VfxResolver.Resolve(a.Profile, s, content, a.FrameW, a.FrameH, clampToBudget: true);
        a.HonestRatio = honest.NativeRatio;
        a.OverBudget = VfxBudget.Of(honest.NativeRatio) == VfxBudget.Verdict.Over;

        var centre = a.Placement.Content.Center;
        if (!a.Resolved)
        {
            a.From = centre;
            a.To = centre;
            if (a.Profile.Travel == VfxTravel.ToTarget && a.TravelTo is { } t
                && Bounds.TryBounds(t, out var target))
                a.To = target.Rect.Center;
            a.Resolved = true;
            if (DumpEnabled) Dump(a, honest.NativeRatio);
        }
        else
        {
            a.From = centre;   // Pinned: the rectangle moved with the figure
        }
        return true;
    }

    /// <summary>
    /// One tab-separated line per effect, the first time it resolves — the primary artifact of §70.
    /// </summary>
    private static void Dump(Anim a, float honestRatio)
    {
        var p = a.Profile;
        var verdict = VfxBudget.Of(honestRatio) switch
        {
            VfxBudget.Verdict.Over => "OVER",
            VfxBudget.Verdict.Under => "under",
            _ => "ok",
        };
        Console.WriteLine(
            $"vfx\tid={p.Id}\tkey={a.Key}\tsubj={a.Subject}\tanchor={p.Anchor}"
            + $"\toff={p.OffsetX:0.00}w,{p.OffsetY:0.00}h"
            + $"\tscale={p.RelativeScale:0.00}{(p.Basis == VfxBasis.SubjectHeight ? "h" : "w")}"
            + $"\tframe={a.Placement.Frame.X},{a.Placement.Frame.Y},{a.Placement.Frame.Width},{a.Placement.Frame.Height}"
            + $"\tcontent={a.Placement.Content.X},{a.Placement.Content.Y},{a.Placement.Content.Width},{a.Placement.Content.Height}"
            + $"\tratio={honestRatio:0.000}\t{verdict}\tlayer={p.Layer}\theld={(a.Loop ? 1 : 0)}");
    }

    /// <summary>
    /// FIXTURE ONLY: jump every live effect forward, even one that has not been placed yet.
    /// </summary>
    /// <remarks>
    /// <see cref="Update"/> deliberately holds an unresolved effect's clock so it cannot fade out while
    /// waiting for the frame it can be placed on — but the shield fixture
    /// (<c>RH_SHOT_SHIELDFX=break</c>) fires a burst and immediately winds it to the middle of its own
    /// strip, before anything has drawn. That is a capture pose, not a game state, so it says so.
    /// </remarks>
    public void FixtureAdvance(float seconds)
    {
        foreach (var a in _active) a.Elapsed += seconds;
        foreach (var a in _held.Values) a.Elapsed += seconds;
    }

    /// <summary>Drop everything, fired and held alike — a replay rebuild must leave nothing standing.</summary>
    public void Clear()
    {
        _active.Clear();
        _held.Clear();
        _heldThisFrame.Clear();
        _debug.Clear();
    }
}
