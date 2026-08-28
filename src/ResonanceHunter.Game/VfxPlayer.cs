using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ResonanceHunter.Client;

/// <summary>
/// Plays one-shot sprite-strip animations at screen positions — hit sparks, breaks, casts, death.
/// </summary>
/// <remarks>
/// <para>
/// Drives the impact effects of the solo fight — a hit spark on every blow, a burst on a Form's cast,
/// a death puff when the enemy or the champion drops. It sat unwired for several commits (its only
/// caller, manual combat, was deleted) until <see cref="SoloExpeditionScreen"/> picked it up.
/// </para>
/// <para>
/// A strip is a horizontal PNG of equal-width frames; frame height = image height, frame count =
/// width / height-derived count passed at spawn. Effects are point-sampled, integer-scaled, centered
/// on their spawn point, and removed when they finish. A missing effect asset is simply skipped, so
/// the game degrades to no-VFX rather than crashing.
/// </para>
/// </remarks>
public sealed class VfxPlayer
{
    private sealed class Anim
    {
        public required Texture2D Sheet { get; init; }
        public required int FrameW { get; init; }
        public required int FrameH { get; init; }
        public required int Frames { get; init; }
        public required int CenterX { get; set; }
        public required int CenterY { get; set; }

        /// <summary>Where the effect ENDS. Equal to the centre for everything that stays put.</summary>
        /// <remarks>
        /// A projectile used to be played at the MIDPOINT between the champion and its target and simply
        /// appeared there — the designer's words, 2026-08-28: "projectile efektlerinin gitme animasyonu
        /// yok, direkt düşmanın üstünde çıkıyor". The strips are authored as IN-PLACE motion (a spin, a
        /// tumble) and that stays right: crossing the gap is the RENDERER's job, because only the renderer
        /// knows where the champion and the target actually are this frame.
        /// </remarks>
        public int ToX { get; init; }
        public int ToY { get; init; }

        /// <summary>The effect's position now — its start, eased toward its end across its life.</summary>
        /// <remarks>
        /// Eased out, not linear: a thrown thing leaves fast and arrives slowing, and a linear crossing
        /// read as a sliding decal. The clock is <see cref="Life"/>, so the travel finishes exactly as the
        /// last frame does however fast the strip is played.
        /// </remarks>
        public (int X, int Y) At
        {
            get
            {
                // A HELD effect never travels: it is re-placed by its owner every frame, and it has no
                // end point to ease toward. Without this it eased toward the DEFAULT ToX/ToY of 0 and the
                // aura spent the fight drawn at (-249,-249) — off the top-left corner, which is why it
                // looked like it had simply stopped existing.
                if (Loop || (ToX == CenterX && ToY == CenterY)) return (CenterX, CenterY);
                var t = Life;
                t = 1f - (1f - t) * (1f - t);
                return (CenterX + (int)((ToX - CenterX) * t), CenterY + (int)((ToY - CenterY) * t));
            }
        }
        /// <summary>Display multiplier of the 104 px base unit. FLOAT — the aura needs 4.4, not 4 or 5.</summary>
        public required float Scale { get; init; }

        /// <summary>How much the effect swells over its life: 1 = the authored size, 4 = four times it by the last frame.</summary>
        public float GrowTo { get; init; } = 1f;

        /// <summary>0 at the first frame, 1 at the last — the growth's clock.</summary>
        public float Life => Math.Clamp(Elapsed / MathF.Max(0.0001f, SecondsPerFrame * Frames), 0f, 1f);
        public required float SecondsPerFrame { get; init; }
        public required Color Tint { get; set; }
        public float Elapsed;

        /// <summary>A HELD effect loops and never ends; a fired one clamps on its last frame and dies.</summary>
        public bool Loop { get; init; }

        public int CurrentFrame => Loop
            ? (int)(Elapsed / SecondsPerFrame) % Math.Max(1, Frames)
            : Math.Min(Frames - 1, (int)(Elapsed / SecondsPerFrame));
        public bool Done => !Loop && Elapsed >= SecondsPerFrame * Frames;

        /// <summary>
        /// 1 for most of the clip, easing to 0 across its final third — so every effect ENDS.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The strips do not agree on how to finish, and until now none of them had to. Measured, per
        /// frame, as a fraction of the frame that is opaque:
        /// </para>
        /// <code>
        ///   impact_gold   32 38 42 24 10  4  4 [17]   decays, then POPS BACK on the last frame
        ///   impact_crit   18 24 29 19  9  3  0 [ 5]   decays to empty, then flashes once more
        ///   death_dissolve 23 24 24 23 22 22 21  21   a LOOP, played once as a one-shot
        ///   levelup        8  8  8  8  8  8  9   9    also a loop
        /// </code>
        /// <para>
        /// So half the effects in the fight ended by simply vanishing at full brightness, and two of
        /// them flickered back on after they had finished. A fade is the one change that fixes every
        /// shape of ending at once — a decaying burst gets a soft tail, a looping flame gets an exit,
        /// and a stray final frame is dim enough not to read as a second hit.
        /// </para>
        /// <para>
        /// Cubed rather than linear because these draw ADDITIVELY: additive alpha reads far brighter
        /// than its number suggests, so a linear ramp still looks like a hard cut at the end.
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

    private readonly AssetLibrary _assets;
    private readonly List<Anim> _active = new();

    /// <summary>
    /// Effects that are HELD rather than fired: refreshed every frame by their owner, looping, never fading.
    /// </summary>
    /// <remarks>
    /// The aura is not an event, it is a STATE — "sürekli açık olacak, asla sönmeyecek" (designer,
    /// 2026-08-28). Spawning a one-shot on every tick could only ever look like a thing that flashes and
    /// dies, however the strip was drawn, because a fired effect is defined by ending. A held effect is
    /// owned by its caller: it exists exactly as long as the caller keeps asking for it, at whatever
    /// brightness the caller passes THIS frame, which is what lets the fight peak it on a hit.
    /// </remarks>
    private readonly Dictionary<string, Anim> _held = new();
    private readonly HashSet<string> _heldThisFrame = new();

    /// <summary>When set (the Hunt arena pass), the additive + restore batches use it so the glow is scissor-
    /// clipped to the arena along with everything else. Null elsewhere = default (unclipped) rasterizer.</summary>
    public RasterizerState? Rasterizer;

    /// <summary>Settings' FIGHT EFFECTS switch. Off = Play() is a no-op; the creature clips still run.</summary>
    public bool Enabled = true;

    /// <summary>
    /// The rasterizer the caller's batch is reopened with after the effects pass. Draw() ends the caller's
    /// batch, draws additively with <see cref="Rasterizer"/>, then reopens — and it used to reopen with the
    /// same (null) rasterizer, so everything the caller drew AFTER an effect (callouts, the red flash, the
    /// overlay) silently lost the arena scissor whenever an effect was in flight (review 2026-08-23).
    /// </summary>
    public RasterizerState? RestoreRasterizer;

    /// <summary>Counter-scale of the canvas batch this player draws into: 4 for 480-logical screens, 1 for
    /// screens authored in true 1920 coords. The Draw transform and the per-effect size derive from it so the
    /// on-screen (physical) size stays constant regardless of which scale the caller is drawing at.</summary>
    public int Scale = 4;

    public VfxPlayer(AssetLibrary assets) => _assets = assets;

    /// <summary>
    /// Spawn a strip animation centered at (x, y). Frame width is inferred from the sheet height
    /// (frames are square) unless <paramref name="frameW"/> is given for non-square strips.
    /// </summary>
    public void Play(string key, int x, int y, float scale = 2f, float fps = 18f, Color? tint = null, int frameW = 0,
                     float delay = 0f, float growTo = 1f, int? toX = null, int? toY = null)
    {
        if (!Enabled) return;
        if (_assets.Get(key) is not { } sheet) return;

        var fw = frameW > 0 ? frameW : sheet.Height; // default: square frames
        var frames = Math.Max(1, sheet.Width / fw);

        _active.Add(new Anim
        {
            Sheet = sheet,
            FrameW = fw,
            FrameH = sheet.Height,
            Frames = frames,
            CenterX = x,
            CenterY = y,
            ToX = toX ?? x,
            ToY = toY ?? y,
            Scale = MathF.Max(1f, scale),
            GrowTo = MathF.Max(1f, growTo),
            SecondsPerFrame = 1f / MathF.Max(1f, fps),
            Tint = tint ?? Color.White,
            // A negative start is a DELAY: the effect exists but is not drawn until its clock crosses
            // zero. The death plume uses it to rise after the creature's own fall, not over it.
            Elapsed = -MathF.Max(0f, delay),
        });
    }

    /// <summary>
    /// Keep a looping effect alive for this frame at this position and brightness.
    /// </summary>
    /// <remarks>
    /// Call it every frame the effect should exist; stop calling it and it is gone on the next Update.
    /// There is no Stop() on purpose — a held effect that outlives its reason is exactly the kind of
    /// thing that gets left on screen when a wave ends, which this codebase has shipped before.
    /// </remarks>
    public void Hold(string key, int x, int y, float scale, float fps, Color tint)
    {
        if (!Enabled) return;
        if (_assets.Get(key) is not { } sheet) return;
        _heldThisFrame.Add(key);
        if (!_held.TryGetValue(key, out var a) || !ReferenceEquals(a.Sheet, sheet))
        {
            _held[key] = a = new Anim
            {
                Sheet = sheet, FrameW = sheet.Height, FrameH = sheet.Height,
                Frames = Math.Max(1, sheet.Width / sheet.Height),
                CenterX = x, CenterY = y, Scale = MathF.Max(1f, scale),
                SecondsPerFrame = 1f / MathF.Max(1f, fps), Tint = tint, Loop = true,
                ToX = x, ToY = y,
            };
        }
        a.CenterX = x;
        a.CenterY = y;
        a.Tint = tint;      // live: the fight brightens it on a hit and dims it back between
    }

    public void Update(float dt)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].Elapsed += dt;
            if (_active[i].Done) _active.RemoveAt(i);
        }
        // A held effect lives only as long as someone asked for it THIS frame.
        foreach (var key in _held.Keys.ToList())
        {
            if (!_heldThisFrame.Contains(key)) { _held.Remove(key); continue; }
            _held[key].Elapsed += dt;
        }
        _heldThisFrame.Clear();
    }

    public void Draw(SpriteBatch b)
    {
        if (_active.Count == 0 && _held.Count == 0) return;
        // Additive sub-pass. These are radial GLOW effects; in the caller's AlphaBlend batch their soft edges
        // read as hard ring OUTLINES (the "reticles" bug). Drawn additively they glow and layer as intended.
        // End the caller's batch, run additive, then restore AlphaBlend for the HUD that draws after us. The
        // transform mirrors the caller's canvas scale (Scale) so effects land where the fight authored them.
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, Rasterizer, null, Matrix.CreateScale(Scale));
        foreach (var a in _held.Values.Concat(_active))   // held first: the aura sits UNDER the blows
        {
            if (a.Elapsed < 0f) continue;   // still in its delay
            var src = new Rectangle(a.CurrentFrame * a.FrameW, 0, a.FrameW, a.FrameH);
            // `Scale` (the effect's) is a display multiplier of a base logical unit, NOT a factor on the raw
            // frame: package_06 frames are 512 px, so multiplying them directly would fill the screen. The
            // base unit is 104 physical px (26 at the old ×4 canvas); dividing by the canvas Scale keeps the
            // on-screen size constant (×4 canvas → 26, ×1 canvas → 104). Aspect kept.
            // GROWTH (2026-08-29): an effect may swell across its life — the aura's pulse leaves the
            // champion and reaches the enemy row, which is what makes it read as a blow rather than a
            // decoration ("the circle should widen as far as the enemies so there is a hit feeling").
            var h = (int)MathF.Round(a.Scale * 104 * (a.GrowTo <= 1f ? 1f : 1f + (a.GrowTo - 1f) * a.Life) / Scale);
            var w = h * a.FrameW / a.FrameH;
            var (ax, ay) = a.At;
            b.Draw(a.Sheet, new Rectangle(ax - w / 2, ay - h / 2, w, h), src, a.Tint * a.Fade);
        }
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RestoreRasterizer ?? Rasterizer, null, Matrix.CreateScale(Scale));
    }

    /// <summary>Drop everything, fired and held alike — a wave boundary must leave nothing standing.</summary>
    public void Clear()
    {
        _active.Clear();
        _held.Clear();
        _heldThisFrame.Clear();
    }
}
