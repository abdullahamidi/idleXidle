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

    public static void Draw(UiKit ui, SpriteBatch b, Point centre, int radius, Form? native, bool showFactors)
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

            var node = new Rectangle((int)pos[i].X - 17, (int)pos[i].Y - 17, 34, 34);
            if (!ui.Icon(b, $"icon_form_{f.ToString().ToLowerInvariant()}", node, col))
                ui.Diamond(b, node, col);

            // The BOUND seal: a ring of short threads around your own Form.
            if (isNative)
                for (var k = 0; k < 12; k++)
                {
                    var a0 = MathF.PI * 2f * k / 12f;
                    var a1 = MathF.PI * 2f * (k + 0.55f) / 12f;
                    ui.LineSeg(b,
                        pos[i] + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * 26f,
                        pos[i] + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * 26f, 2.5f, Gold);
                }

            // Labels sit OUTWARD from the ring so ten-letter names never cross the threads — far
            // enough out that the bound seal's own ring (r=26) never runs through its factor line.
            var lp = new Vector2(centre.X, centre.Y) + dir[i] * (radius + 58);
            ui.TextCenter(b, f.ToString().ToUpperInvariant(), (int)lp.X, (int)lp.Y - 10, col);
            if (showFactors && native is not null)
                ui.TextCenter(b, isNative ? "x2.0 — YOURS" : $"x{factor:0.0#}", (int)lp.X, (int)lp.Y + 12,
                              isNative ? Gold : col * 0.9f);
        }
    }
}
