using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ResonanceHunter.Client;

/// <summary>
/// A 5x7 bitmap font, defined in code and rasterized into a single texture at startup.
/// </summary>
/// <remarks>
/// MonoGame ships no font. The two normal options both have costs we do not want yet: a
/// <c>SpriteFont</c> needs an MGCB content build and a TTF asset in the repo, and FontStashSharp adds
/// a dependency for something a 480x270 pixel-art game does not need. A hand-authored 5x7 face costs
/// nothing, has no build step, and — being pixels — sits natively on the virtual canvas without any
/// resampling.
///
/// Glyphs are written as ASCII art below precisely so they are legible and editable by a human. If a
/// letter looks wrong on screen, you can see why by looking at it here.
/// </remarks>
public sealed class PixelFont
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    public const int Tracking = 1; // gap between glyphs

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, int> _index = new();

    public PixelFont(GraphicsDevice device)
    {
        var glyphs = Glyphs;
        var count = glyphs.Count;

        var data = new Color[count * GlyphWidth * GlyphHeight];
        var atlasWidth = count * GlyphWidth;

        var col = 0;
        foreach (var (ch, rows) in glyphs)
        {
            _index[ch] = col;

            for (var y = 0; y < GlyphHeight; y++)
            for (var x = 0; x < GlyphWidth; x++)
            {
                var on = y < rows.Length && x < rows[y].Length && rows[y][x] == '#';
                data[y * atlasWidth + col * GlyphWidth + x] = on ? Color.White : Color.Transparent;
            }

            col++;
        }

        _atlas = new Texture2D(device, atlasWidth, GlyphHeight);
        _atlas.SetData(data);
    }

    /// <summary>Width in canvas pixels that <paramref name="text"/> will occupy.</summary>
    public static int Measure(string text, int scale = 1)
        => text.Length == 0 ? 0 : (text.Length * (GlyphWidth + Tracking) - Tracking) * scale;

    public void Draw(SpriteBatch batch, string text, int x, int y, Color color, int scale = 1)
    {
        ArgumentNullException.ThrowIfNull(text);

        var cursor = x;

        foreach (var raw in text)
        {
            var ch = char.ToUpperInvariant(raw);

            if (ch != ' ' && _index.TryGetValue(ch, out var col))
            {
                batch.Draw(
                    _atlas,
                    new Rectangle(cursor, y, GlyphWidth * scale, GlyphHeight * scale),
                    new Rectangle(col * GlyphWidth, 0, GlyphWidth, GlyphHeight),
                    color);
            }

            cursor += (GlyphWidth + Tracking) * scale;
        }
    }

    /// <summary>Draw right-aligned so numbers do not jitter as they change width.</summary>
    public void DrawRight(SpriteBatch batch, string text, int right, int y, Color color, int scale = 1)
        => Draw(batch, text, right - Measure(text, scale), y, color, scale);

    public void DrawCentered(SpriteBatch batch, string text, int centerX, int y, Color color, int scale = 1)
        => Draw(batch, text, centerX - Measure(text, scale) / 2, y, color, scale);

    private static Dictionary<char, string[]> Glyphs => new()
    {
        ['A'] = [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['B'] = ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
        ['C'] = [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
        ['D'] = ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ['E'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
        ['F'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
        ['G'] = [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
        ['H'] = ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['I'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
        ['J'] = ["..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."],
        ['K'] = ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
        ['L'] = ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
        ['M'] = ["#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#"],
        ['N'] = ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
        ['O'] = [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['P'] = ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
        ['Q'] = [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
        ['R'] = ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
        ['S'] = [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
        ['T'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['U'] = ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['V'] = ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
        ['W'] = ["#...#", "#...#", "#...#", "#...#", "#.#.#", "##.##", "#...#"],
        ['X'] = ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
        ['Y'] = ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
        ['Z'] = ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],

        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
        ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = ["#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],

        ['.'] = [".....", ".....", ".....", ".....", ".....", ".##..", ".##.."],
        [','] = [".....", ".....", ".....", ".....", ".##..", ".##..", "..#.."],
        [':'] = [".....", ".##..", ".##..", ".....", ".##..", ".##..", "....."],
        ['-'] = [".....", ".....", ".....", "#####", ".....", ".....", "....."],

        // ── Typography the UI has been writing since long before it could draw it ──────────────────
        // An unknown glyph still advances the cursor, so it renders as a SILENT GAP rather than a
        // visible tofu box — which is why this went unnoticed. Player-facing text across every screen
        // was quietly losing punctuation: "DISARMED — FINISH IT" read as "DISARMED   FINISH IT", the
        // combat hint "STRING PERFECT READS → FLOW → CRITS" lost both arrows, and "PERFECT ×3" read as
        // "PERFECT 3". Add the glyph, don't rewrite the strings — the strings were right.
        ['—'] = [".....", ".....", ".....", "#####", ".....", ".....", "....."],   // em dash
        ['–'] = [".....", ".....", ".....", ".###.", ".....", ".....", "....."],   // en dash
        ['·'] = [".....", ".....", ".....", "..#..", ".....", ".....", "....."],   // separator
        ['×'] = [".....", ".....", ".#.#.", "..#..", ".#.#.", ".....", "....."],   // times, not the letter X
        ['→'] = [".....", "..#..", "...#.", "#####", "...#.", "..#..", "....."],
        ['←'] = [".....", "..#..", ".#...", "#####", ".#...", "..#..", "....."],
        // The three that were being DRAWN and were not here. A char with no glyph renders as nothing,
        // silently, so the sentence just closes over the hole:
        //   ↔  BuildScreen's "WEIGHT ↔ SPREAD AND TEMPO ↔ ENDURE ARE OPPOSED" read as
        //      "WEIGHT SPREAD AND TEMPO ENDURE ARE OPPOSED", which is a different and wrong claim.
        //   ▲  the inventory's UPGRADE badge — the whole "this beats what you are wearing" signal was
        //      invisible, and looked exactly like having no upgrades in the bag.
        //   ‹  the tree's BACK affordance.
        ['↔'] = [".....", ".....", ".#.#.", "#####", ".#.#.", ".....", "....."],
        ['▲'] = [".....", "..#..", "..#..", ".###.", ".###.", "#####", "....."],
        ['‹'] = ["...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#."],
        ['›'] = [".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..."],
        ['+'] = [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
        ['/'] = ["....#", "....#", "...#.", "..#..", ".#...", "#....", "#...."],
        ['%'] = ["##..#", "##.#.", "...#.", "..#..", ".#...", "#.###", "..###"],
        ['!'] = ["..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."],
        ['?'] = [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
        ['('] = ["...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#."],
        [')'] = [".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..."],
        ['['] = ["..###", "..#..", "..#..", "..#..", "..#..", "..#..", "..###"],
        [']'] = ["###..", "..#..", "..#..", "..#..", "..#..", "..#..", "###.."],
        ['>'] = ["#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#...."],
        ['<'] = ["....#", "...#.", "..#..", ".#...", "..#..", "...#.", "....#"],
        ['='] = [".....", ".....", "#####", ".....", "#####", ".....", "....."],
        ['*'] = [".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....."],
        ['\''] = ["..#..", "..#..", ".....", ".....", ".....", ".....", "....."],
        ['_'] = [".....", ".....", ".....", ".....", ".....", ".....", "#####"],
    };
}
