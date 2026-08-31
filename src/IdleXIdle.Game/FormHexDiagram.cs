using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Game;

/// <summary>
/// The six-STYLE hexagon — the diagram of who you are.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately NOT the inspiration's divination scene (no vessel, no liquid, no leaf — telif
/// hassasiyeti): this game's language is the WEAVE, so attunement draws as six seals on a loom,
/// the native one bound to the others by threads whose weight is the hexagon's own verdict.
/// </para>
/// <para>
/// Factors come straight from <see cref="StyleAffinity.Factor"/>, so the diagram cannot
/// drift from the sim. With no native style it draws quiet and unclaimed.
/// </para>
/// <para>
/// <b>IT WAS PLOTTED, NOT DRAWN</b> (playtest 2026-08-30: <i>"you framed the specialisation chart,
/// good — but its content is still careless"</i>). It was six bare icons floating at the corners of
/// a 2 px wireframe, in a game whose every other surface is hand-painted. What it is made of now:
/// </para>
/// <list type="bullet">
///   <item><description>a FIELD — a dark inlaid well with a raised bezel, a bronze hairline and four
///   corner ticks (<see cref="Field"/>), so the chart sits ON something;</description></item>
///   <item><description>RAILS, not lines — every spoke is a dark bed with a lit core, tapered along
///   its length: the ring swells at mid-span and thins into each seal, and a thread leaves your own
///   seal at full weight and arrives thin (<see cref="Rail"/>);</description></item>
///   <item><description>SEALS — each Form's icon inlaid in <c>ui_medallion_hex</c> on its own dark
///   bed, the attuned one lit gold inside a halo and a ring of bound threads, the other five
///   dimmed by how far they are from it.</description></item>
/// </list>
/// <para>
/// ONE DRAWING AT TWO SIZES: the 150 px ceremony and the ~60 px docked chart. Every weight below is
/// a function of <c>radius</c> through <c>scale</c>, so neither is a hand-tuned copy of the other.
/// </para>
/// </remarks>
internal static class FormHexDiagram
{
    private static readonly Color Gold = new(0xF0, 0xB2, 0x4A);
    private static readonly Color Met = new(0x5F, 0xC8, 0x8F);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Ember = new(0xC0, 0x5C, 0x50);

    // ── The field: a bezel, a well, a hairline. ──
    private static readonly Color Bezel = new(0x2E, 0x27, 0x38);
    private static readonly Color Well = new(0x0B, 0x09, 0x11);
    private static readonly Color Hairline = new(0x8A, 0x6A, 0x3C);

    // ── The rails: every spoke is a dark bed under a lit core. ──
    private static readonly Color RailBed = new(0x0A, 0x07, 0x0C);
    private static readonly Color RingRail = new(0x6E, 0x56, 0x33);

    /// <summary>The stone a seal is set into, so a rail never runs visibly under an icon.</summary>
    private static readonly Color SealBed = new(0x13, 0x0F, 0x1B);

    /// <summary>The attuned seal's bed — the same stone, warmed, so gold has something to sit on.</summary>
    private static readonly Color NativeBed = new(0x2A, 0x1D, 0x0C);

    /// <summary>
    /// How far a seal's own furniture reaches from its centre, as a fraction of the seal's edge: the
    /// medallion is half of it, the bound ring rides just outside that. Labels are measured from here,
    /// so a name never lands on the ring a seal wears.
    /// </summary>
    private const float RimFraction = 0.62f;

    /// <summary>The Form's own art inside the medallion, as a fraction of the seal's edge.</summary>
    private const float IconFraction = 0.60f;

    /// <summary>What a seal that is NOT yours is worth on the page: present, quiet, not competing.</summary>
    private const float DimSeal = 0.58f;

    /// <summary>The air between a name and its factor when the two share one line.</summary>
    private const int Gutter = 8;

    /// <summary>
    /// THE FIELD the chart is drawn on — a dark well, inlaid.
    /// </summary>
    /// <remarks>
    /// Public and shared by both call sites on purpose: the ceremony used to have no field at all (the
    /// hexagon floated on the modal's black interior) while the docked chart had a flat rectangle with
    /// a 2 px outline. Two different answers to "what is this chart standing on" is exactly the drift
    /// the house standard exists to close.
    /// </remarks>
    public static void Field(UiKit ui, SpriteBatch b, Rectangle r)
    {
        ui.Fill(b, r, Bezel);                                  // the raised lip
        ui.Fill(b, Shrink(r, 3), Well);                        // the recess it holds
        Outline(ui, b, Shrink(r, 8), Hairline * 0.55f);        // the hairline inside it

        // FOUR CORNER TICKS — the mark that says "this is a plate", the same language the house frames
        // speak in their corners. Short: a long one stops reading as a corner and starts reading as a
        // rule that failed to reach the other side.
        var reach = Math.Clamp(r.Width / 30, 9, 16);
        var tick = Gold * 0.55f;
        foreach (var (x, dx) in new[] { (r.X + 8, 1), (r.Right - 9, -1) })
            foreach (var (y, dy) in new[] { (r.Y + 8, 1), (r.Bottom - 9, -1) })
            {
                ui.Fill(b, new Rectangle(dx > 0 ? x : x - reach + 2, y, reach, 2), tick);
                ui.Fill(b, new Rectangle(x, dy > 0 ? y : y - reach + 2, 2, reach), tick);
            }
    }

    /// <param name="labelGap">
    /// The clear air between a Form's SEAL and its own name — not, as it used to be, a fixed distance
    /// from the hexagon's centre. Measuring from the seal is what lets one routine draw a 150 px
    /// ceremony and a docked chart half that size without either one's labels landing on its own icons.
    /// </param>
    /// <param name="labelPx">The size of a Form's name. A rung, never a literal.</param>
    /// <param name="factorPx">
    /// The size of the multiplier under a name. One rung below <paramref name="labelPx"/> by default,
    /// because the name is what you look for and the number is what you then read.
    /// </param>
    /// <param name="sealPx">
    /// The edge of a Form's seal — the medallion, not the art inside it. It was a flat 34 at every
    /// radius, which is right for the ceremony's 150 px hexagon and far too heavy for a docked chart
    /// half that size.
    /// </param>
    public static void Draw(UiKit ui, SpriteBatch b, Point centre, int radius, Style? native, bool showFactors,
                            int labelGap = 26, int labelPx = UiTypography.Body,
                            int factorPx = UiTypography.Secondary, int sealPx = 34)
    {
        var forms = Enum.GetValues<Style>();
        var pos = new Vector2[forms.Length];
        var dir = new Vector2[forms.Length];
        for (var i = 0; i < forms.Length; i++)
        {
            var a = -MathF.PI / 2f + MathF.PI * 2f * i / forms.Length;
            dir[i] = new Vector2(MathF.Cos(a), MathF.Sin(a));
            pos[i] = new Vector2(centre.X, centre.Y) + dir[i] * radius;
        }

        // EVERY WEIGHT ON THE PAGE IS A FUNCTION OF THIS. The floor is what keeps the docked chart from
        // drawing hairlines: at 0.4 of the ceremony a 5 px rail would come out at 2 and read as the
        // wireframe this drawing replaced.
        var scale = Math.Clamp(radius / 150f, 0.70f, 1f);
        var sealHalf = sealPx / 2f;
        var rim = sealPx * RimFraction;

        // ── THE LOOM'S OWN RING — six tapered rails, thin where they meet a seal. ──
        for (var i = 0; i < forms.Length; i++)
            Rail(ui, b, pos[i], pos[(i + 1) % forms.Length], sealHalf + 1f, sealHalf + 1f,
                 1.4f, 1.5f + 3.8f * scale, RingRail, spindle: true);

        // ── YOUR THREADS — from your seal to every other, weighted by the verdict. ──
        if (native is { } nat)
        {
            var ni = Array.IndexOf(forms, nat);
            for (var i = 0; i < forms.Length; i++)
            {
                if (i == ni) continue;
                var f = StyleAffinity.Factor(nat, forms[i]);
                var (col, weight) = f >= 1.14f ? (Gold * 0.88f, 2.6f + 3.0f * scale)
                                  : f >= 0.74f ? (Slate * 0.60f, 1.8f + 1.8f * scale)
                                               : (Ember * 0.58f, 1.4f + 1.2f * scale);
                // Leaves YOUR seal at full weight and arrives thin: the thread is yours, and the eye
                // reads the direction without being told.
                // It clears YOUR seal's bound ring at this end and only the medallion at the other:
                // a thread that stopped short of the seal it arrives at reads as a broken wire.
                Rail(ui, b, pos[ni], pos[i], rim + 2f, sealHalf + 1f, weight, 1.2f, col, spindle: false);
            }
        }

        for (var i = 0; i < forms.Length; i++)
        {
            var f = forms[i];
            var isNative = native == f;
            var factor = native is { } n2 ? StyleAffinity.Factor(n2, f) : 1f;
            var col = native is null ? Slate
                : factor >= 1.99f ? Gold
                : factor >= 1.14f ? Met
                : factor >= 0.74f ? Slate
                : Ember;

            // ── THE SEAL ──
            // The bed sits INSIDE the medallion's own outline (0.80, not 0.90): the frame art is an
            // octagon and the bed is a hexagon, so a bed as wide as its frame pokes its two points out
            // of the sides and the seal reads as two shapes fighting.
            if (isNative) Halo(ui, b, pos[i], sealPx);
            ui.Hex(b, Box(pos[i], sealPx * 0.80f), isNative ? NativeBed : SealBed);

            var med = Box(pos[i], sealPx);
            if (!ui.Icon(b, "ui_medallion_hex", med, isNative ? Gold : col * DimSeal))
                Outline(ui, b, med, isNative ? Gold : col * DimSeal);

            // THE ART KEEPS ITS OWN COLOURS. Tinting a hand-painted icon to the affinity colour is how
            // the old chart said "far Form" — and it turned six illustrations into six silhouettes. The
            // medallion carries the colour now and the art carries only the light: full for yours,
            // banked down by distance for the rest.
            var art = Box(pos[i], sealPx * IconFraction);
            var lit = Color.White * (isNative ? 1f : factor >= 1.14f ? 0.88f : factor >= 0.74f ? 0.70f : 0.52f);
            if (!ui.Icon(b, IconKey(f), art, lit))
                ui.Diamond(b, art, col);

            // The BOUND seal: a ring of short threads around your own Form, just outside the medallion.
            if (isNative)
                for (var k = 0; k < 12; k++)
                {
                    var a0 = MathF.PI * 2f * k / 12f;
                    var a1 = MathF.PI * 2f * (k + 0.55f) / 12f;
                    ui.LineSeg(b, pos[i] + Ray(a0) * rim, pos[i] + Ray(a1) * rim,
                               1.4f + 1.6f * scale, Gold * 0.9f);
                }

            // ── THE NAMES SIT ON THEIR OWN POINTS. ───────────────────────────────────────────────
            //
            // All six used to be CENTRED on a point <c>radius + 58</c> out along the outward ray, and
            // that is only symmetrical if the six words are the same length — which they are not:
            // TRANSFORMATION is fourteen letters and AURA is four. So the chart leaned visibly west,
            // the long left-hand names reached past the panel holding them, and the short right-hand
            // ones floated away from the seals they belong to. Worse, a fixed 58 is a distance in a
            // diagram whose radius is a parameter, so at any size but the ceremony's the two lines
            // either drifted off or landed ON the seal.
            //
            // A label belongs to a POINT, so it is placed against that point's own seal: it starts
            // <paramref name="labelGap"/> px clear of the seal's rim and grows AWAY from the centre.
            //
            // AND IT SPENDS THE AXIS ITS POINT CAN AFFORD. The north and south points have the whole
            // width of the field either side of them and almost nothing above or below; the four
            // shoulders have the opposite. So north and south set their name and their factor SIDE BY
            // SIDE on one line, and the shoulders stack them. Two lines at the poles is what used to
            // cap the hexagon's radius at 58 in a 266 px field — a third of the drawing spent on
            // stacking two words nobody needed stacked.
            var name = f.ToString().ToUpperInvariant();
            var factorText = isNative ? "x2.0 — YOURS" : $"x{factor:0.0#}";
            var factorCol = isNative ? Gold : col * 0.9f;
            var showFactor = showFactors && native is not null;

            if (MathF.Abs(dir[i].X) < 0.5f)          // the north and south points
            {
                var nameW = ui.MeasureBig(name, labelPx);
                var facW = showFactor ? ui.MeasureBig(factorText, factorPx) : 0;
                var run = nameW + (showFactor ? Gutter + facW : 0);
                var x0 = (int)(pos[i].X - run / 2f);
                var y = (int)(dir[i].Y < 0f
                    ? pos[i].Y - (rim + labelGap) - labelPx
                    : pos[i].Y + rim + labelGap);
                ui.TextBig(b, name, x0, y, col, labelPx);
                if (showFactor)
                    ui.TextBig(b, factorText, x0 + nameW + Gutter, y + (labelPx - factorPx) / 2,
                               factorCol, factorPx);
            }
            else                                     // the four shoulder points
            {
                var x = (int)(pos[i].X + MathF.Sign(dir[i].X) * (rim + labelGap));
                var nameY = (int)pos[i].Y - labelPx - 2;   // the pair straddles the point's own line
                if (dir[i].X > 0f)
                {
                    ui.TextBig(b, name, x, nameY, col, labelPx);
                    if (showFactor) ui.TextBig(b, factorText, x, (int)pos[i].Y + 2, factorCol, factorPx);
                }
                else
                {
                    ui.TextRightBig(b, name, x, nameY, col, labelPx);
                    if (showFactor) ui.TextRightBig(b, factorText, x, (int)pos[i].Y + 2, factorCol, factorPx);
                }
            }
        }
    }

    /// <summary>
    /// A RAIL: a dark bed with a lit core, tapered along its length and held clear of both seals.
    /// </summary>
    /// <remarks>
    /// One <c>LineSeg</c> cannot taper — it is a single rotated quad — so a rail is a short run of
    /// them with the width walked between the two ends. The bed is laid in one pass and the core in a
    /// second so the joints never show: interleaved, each segment's bed would be drawn over the
    /// previous segment's core.
    /// </remarks>
    /// <param name="spindle">
    /// True for a rail that belongs to nobody (the ring): it swells at mid-span and thins into both
    /// seals. False for one that has an owner (a thread): it walks from <paramref name="thickFrom"/>
    /// down to <paramref name="thickTo"/>, so the heavy end is the end it comes from.
    /// </param>
    private static void Rail(UiKit ui, SpriteBatch b, Vector2 from, Vector2 to,
                             float clearFrom, float clearTo, float thickFrom, float thickTo,
                             Color core, bool spindle)
    {
        var d = to - from;
        var len = d.Length();
        if (len <= clearFrom + clearTo + 4f) return;
        var u = d / len;
        var a = from + u * clearFrom;
        var c = to - u * clearTo;

        const int Steps = 12;
        for (var pass = 0; pass < 2; pass++)
            for (var i = 0; i < Steps; i++)
            {
                var s0 = i / (float)Steps;
                var s1 = (i + 1) / (float)Steps;
                var mid = (s0 + s1) * 0.5f;
                var w = spindle
                    ? MathHelper.Lerp(thickFrom, thickTo, MathF.Sin(mid * MathF.PI))
                    : MathHelper.Lerp(thickFrom, thickTo, mid);
                ui.LineSeg(b, Vector2.Lerp(a, c, s0), Vector2.Lerp(a, c, s1),
                           pass == 0 ? w + 2.4f : w, pass == 0 ? RailBed : core);
            }
    }

    /// <summary>
    /// The light around the seal that is yours — stacked hexagons at a low alpha, so the accumulation
    /// is a gradient rather than a ring. (There is no glow texture in the kit: <c>_blob</c> is a
    /// premultiplied BLACK falloff, which tints to black whatever colour it is given.)
    /// </summary>
    private static void Halo(UiKit ui, SpriteBatch b, Vector2 p, float sealPx)
    {
        for (var i = 0; i < 12; i++)
            ui.Hex(b, Box(p, sealPx * (2.50f - i * 0.12f)), Gold * 0.022f);
    }

    /// <summary>
    /// The seal art for a style — the Form-era icons, re-read as the styles they always depicted.
    /// The PNGs keep their names (renaming forty assets is churn); the mapping is the meaning.
    /// </summary>
    private static string IconKey(Style s) => s switch
    {
        Style.Hammer => "icon_form_strike",
        Style.Snare => "icon_form_trap",
        Style.Sign => "icon_form_mark",
        Style.Volley => "icon_form_projectile",
        Style.Field => "icon_form_aura",
        _ => "icon_form_transformation",
    };

    private static Vector2 Ray(float a) => new(MathF.Cos(a), MathF.Sin(a));

    private static Rectangle Box(Vector2 p, float size)
        => new((int)MathF.Round(p.X - size / 2f), (int)MathF.Round(p.Y - size / 2f),
               Math.Max(1, (int)MathF.Round(size)), Math.Max(1, (int)MathF.Round(size)));

    private static Rectangle Shrink(Rectangle r, int by)
        => new(r.X + by, r.Y + by, Math.Max(1, r.Width - by * 2), Math.Max(1, r.Height - by * 2));

    private static void Outline(UiKit ui, SpriteBatch b, Rectangle r, Color c)
    {
        ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 1), c);
        ui.Fill(b, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        ui.Fill(b, new Rectangle(r.X, r.Y, 1, r.Height), c);
        ui.Fill(b, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
