using System;
using System.Collections.Generic;
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
        public required int CenterX { get; init; }
        public required int CenterY { get; init; }
        public required int Scale { get; init; }
        public required float SecondsPerFrame { get; init; }
        public required Color Tint { get; init; }
        public float Elapsed;

        public int CurrentFrame => Math.Min(Frames - 1, (int)(Elapsed / SecondsPerFrame));
        public bool Done => Elapsed >= SecondsPerFrame * Frames;

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

    /// <summary>When set (the Hunt arena pass), the additive + restore batches use it so the glow is scissor-
    /// clipped to the arena along with everything else. Null elsewhere = default (unclipped) rasterizer.</summary>
    public RasterizerState? Rasterizer;

    /// <summary>Counter-scale of the canvas batch this player draws into: 4 for 480-logical screens, 1 for
    /// screens authored in true 1920 coords. The Draw transform and the per-effect size derive from it so the
    /// on-screen (physical) size stays constant regardless of which scale the caller is drawing at.</summary>
    public int Scale = 4;

    public VfxPlayer(AssetLibrary assets) => _assets = assets;

    /// <summary>
    /// Spawn a strip animation centered at (x, y). Frame width is inferred from the sheet height
    /// (frames are square) unless <paramref name="frameW"/> is given for non-square strips.
    /// </summary>
    public void Play(string key, int x, int y, int scale = 2, float fps = 18f, Color? tint = null, int frameW = 0,
                     float delay = 0f)
    {
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
            Scale = Math.Max(1, scale),
            SecondsPerFrame = 1f / MathF.Max(1f, fps),
            Tint = tint ?? Color.White,
            // A negative start is a DELAY: the effect exists but is not drawn until its clock crosses
            // zero. The death plume uses it to rise after the creature's own fall, not over it.
            Elapsed = -MathF.Max(0f, delay),
        });
    }

    public void Update(float dt)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].Elapsed += dt;
            if (_active[i].Done) _active.RemoveAt(i);
        }
    }

    public void Draw(SpriteBatch b)
    {
        if (_active.Count == 0) return;
        // Additive sub-pass. These are radial GLOW effects; in the caller's AlphaBlend batch their soft edges
        // read as hard ring OUTLINES (the "reticles" bug). Drawn additively they glow and layer as intended.
        // End the caller's batch, run additive, then restore AlphaBlend for the HUD that draws after us. The
        // transform mirrors the caller's canvas scale (Scale) so effects land where the fight authored them.
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, Rasterizer, null, Matrix.CreateScale(Scale));
        foreach (var a in _active)
        {
            if (a.Elapsed < 0f) continue;   // still in its delay
            var src = new Rectangle(a.CurrentFrame * a.FrameW, 0, a.FrameW, a.FrameH);
            // `Scale` (the effect's) is a display multiplier of a base logical unit, NOT a factor on the raw
            // frame: package_06 frames are 512 px, so multiplying them directly would fill the screen. The
            // base unit is 104 physical px (26 at the old ×4 canvas); dividing by the canvas Scale keeps the
            // on-screen size constant (×4 canvas → 26, ×1 canvas → 104). Aspect kept.
            var h = a.Scale * 104 / Scale;
            var w = h * a.FrameW / a.FrameH;
            b.Draw(a.Sheet, new Rectangle(a.CenterX - w / 2, a.CenterY - h / 2, w, h), src, a.Tint * a.Fade);
        }
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, Rasterizer, null, Matrix.CreateScale(Scale));
    }

    public void Clear() => _active.Clear();
}
