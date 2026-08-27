using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;

namespace ResonanceHunter.Client;

/// <summary>
/// The six-Form hexagon, drawn from primitives — the diagram of who you are.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately NOT the inspiration's divination scene (no vessel, no liquid, no leaf — telif
/// hassasiyeti): this game's language is the WEAVE, so attunement draws as six seals on a loom,
/// the native one bound to the others by threads whose weight is the hexagon's own verdict.
/// </para>
/// <para>
/// Factors come straight from <see cref="FormBehaviour.AffinityFactor"/>, so the diagram cannot
/// drift from the sim. With no native Form it draws quiet and unclaimed.
/// </para>
/// </remarks>
internal static class FormHexDiagram
{
    private static readonly Color Gold = new(0xF0, 0xB2, 0x4A);
    private static readonly Color Met = new(0x5F, 0xC8, 0x8F);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Ember = new(0xC0, 0x5C, 0x50);
    private static readonly Color Thread = new(0x3A, 0x34, 0x46);

    /// <param name="labelGap">
    /// The clear air between a Form's SEAL and its own name — not, as it used to be, a fixed distance
    /// from the hexagon's centre. Measuring from the seal is what lets one routine draw a 150 px
    /// ceremony and a 76 px docked chart without either one's labels landing on its own icons.
    /// </param>
    /// <param name="labelPx">The size of a Form's name. A rung, never a literal.</param>
    /// <param name="factorPx">
    /// The size of the multiplier under a name. One rung below <paramref name="labelPx"/> by default,
    /// because the name is what you look for and the number is what you then read.
    /// </param>
    /// <param name="sealPx">
    /// The edge of a Form's seal. It was a flat 34 at every radius, which is right for the ceremony's
    /// 150 px hexagon and far too heavy for a docked chart half that size — the seals crowded the ring
    /// and the two side names ran into them.
    /// </param>
    public static void Draw(UiKit ui, SpriteBatch b, Point centre, int radius, Form? native, bool showFactors,
                            int labelGap = 26, int labelPx = UiTypography.Body,
                            int factorPx = UiTypography.Secondary, int sealPx = 34)
    {
        var forms = Enum.GetValues<Form>();
        var pos = new Vector2[forms.Length];
        var dir = new Vector2[forms.Length];
        for (var i = 0; i < forms.Length; i++)
        {
            var a = -MathF.PI / 2f + MathF.PI * 2f * i / forms.Length;
            dir[i] = new Vector2(MathF.Cos(a), MathF.Sin(a));
            pos[i] = new Vector2(centre.X, centre.Y) + dir[i] * radius;
        }

        // The loom's own ring — quiet threads between neighbours.
        for (var i = 0; i < forms.Length; i++)
            ui.LineSeg(b, pos[i], pos[(i + 1) % forms.Length], 2f, Thread);

        // The native seal's threads to every other Form, weighted by the verdict: gold to the
        // neighbours it reaches easily, thin ember to the Forms that resist it.
        if (native is { } nat)
        {
            var ni = Array.IndexOf(forms, nat);
            for (var i = 0; i < forms.Length; i++)
            {
                if (i == ni) continue;
                var f = FormBehaviour.AffinityFactor(nat, forms[i]);
                var col = f >= 1.14f ? Gold * 0.85f : f >= 0.74f ? Slate * 0.6f : Ember * 0.5f;
                ui.LineSeg(b, pos[ni], pos[i], f >= 1.14f ? 3f : 2f, col);
            }
        }

        for (var i = 0; i < forms.Length; i++)
        {
            var f = forms[i];
            var isNative = native == f;
            var factor = native is { } n2 ? FormBehaviour.AffinityFactor(n2, f) : 1f;
            var col = native is null ? Slate
                : factor >= 1.99f ? Gold
                : factor >= 1.14f ? Met
                : factor >= 0.74f ? Slate
                : Ember;

            var half = sealPx / 2;
            var node = new Rectangle((int)pos[i].X - half, (int)pos[i].Y - half, sealPx, sealPx);
            if (!ui.Icon(b, $"icon_form_{f.ToString().ToLowerInvariant()}", node, col))
                ui.Diamond(b, node, col);

            // The BOUND seal: a ring of short threads around your own Form. Its radius follows the
            // seal it encircles, or a smaller chart would draw the ceremony's ring around a smaller
            // icon and the two would no longer be the same object.
            var sealRing = sealPx * 0.76f;
            if (isNative)
                for (var k = 0; k < 12; k++)
                {
                    var a0 = MathF.PI * 2f * k / 12f;
                    var a1 = MathF.PI * 2f * (k + 0.55f) / 12f;
                    ui.LineSeg(b,
                        pos[i] + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * sealRing,
                        pos[i] + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * sealRing, 2.5f, Gold);
                }

            // ── THE NAMES SIT ON THEIR OWN POINTS. ───────────────────────────────────────────────
            //
            // All six used to be CENTRED on a point <c>radius + 58</c> out along the outward ray, and
            // that is only symmetrical if the six words are the same length — which they are not:
            // TRANSFORMATION is fourteen letters and AURA is four. So the chart leaned visibly west,
            // the long left-hand names reached past the panel holding them, and the short right-hand
            // ones floated away from the seals they belong to. Worse, a fixed 58 is a distance in a
            // diagram whose radius is a parameter, so at any size but the ceremony's the two lines
            // either drifted off or landed ON the seal (the docked chart drew "x2.0 — YOURS" straight
            // through the bound ring).
            //
            // A label belongs to a POINT, so it is placed against that point's own seal: the block
            // starts <paramref name="labelGap"/> px clear of the ring and grows AWAY from the centre.
            // The two on the left end where their seal begins, the two on the right start there, and
            // the top and bottom — whose points really are on the vertical axis — stay centred and
            // stack, nearest line first.
            var name = f.ToString().ToUpperInvariant();
            var factorText = isNative ? "x2.0 — YOURS" : $"x{factor:0.0#}";
            var factorCol = isNative ? Gold : col * 0.9f;
            var showFactor = showFactors && native is not null;

            if (MathF.Abs(dir[i].X) < 0.5f)          // the north and south points
            {
                var edge = pos[i].Y + MathF.Sign(dir[i].Y) * (sealRing + labelGap);
                // Above the diagram the block grows upward, so the FACTOR is the line nearest the
                // seal and the name rides on top of it; below, the order is the reading order.
                var nameY = dir[i].Y < 0f
                    ? edge - labelPx - (showFactor ? factorPx + 4 : 0)
                    : edge;
                ui.TextCenterBig(b, name, (int)pos[i].X, (int)nameY, col, labelPx);
                if (showFactor)
                    ui.TextCenterBig(b, factorText, (int)pos[i].X, (int)(nameY + labelPx + 4), factorCol, factorPx);
            }
            else                                     // the four shoulder points
            {
                var x = (int)(pos[i].X + MathF.Sign(dir[i].X) * (half + labelGap));
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
}
