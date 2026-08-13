using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ResonanceHunter.Client;

/// <summary>
/// Dev preview (RH_SHOT_MODE=rig, or F8 in-game): the assembled Hunter cutout rig at rest, so part
/// layout, pivots and joint connections can be checked in isolation. Not part of the shipping game.
/// </summary>
/// <remarks>
/// This screen used to carry its OWN copy of the assembly maths — bounds, fit, back-to-front paint —
/// and that copy drifted. It knew nothing about <see cref="HunterPart.RestAngle"/>, so once the source
/// figure moved to an A-pose the preview showed the character standing with its arms out while the game
/// drew them down. A preview that does not go through the shipping path is worse than no preview: it
/// is a second opinion with no authority, and it cost real debugging time here. It now calls the same
/// renderer the battle screen calls, and does nothing but frame it.
/// </remarks>
public sealed class HunterRigScreen
{
    private static readonly Color Bg = new(0x24, 0x20, 0x2C);
    private static readonly Color Panel = new(0x30, 0x2B, 0x3A);
    private static readonly Color Marker = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ink = new(0xE8, 0xDF, 0xC8);

    private readonly UiKit _ui;
    private readonly HunterRigRenderer _renderer;

    /// <summary>Draw the joint dots. Toggled off for a clean beauty shot.</summary>
    public bool ShowMarkers = true;

    public HunterRigScreen(UiKit ui)
    {
        _ui = ui;
        _renderer = new HunterRigRenderer(ui);
    }

    public void Draw(SpriteBatch b)
    {
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Bg);

        var box = new Rectangle(960 - 380, 60, 760, 980);
        _ui.Fill(b, box, Panel);

        _renderer.ShowMarkers = ShowMarkers;
        _renderer.Draw(b, box, pose: null, Color.White, groundToBottom: false);

        _ui.TextCenterBig(b, "HUNTER RIG - REST POSE", 960, 16, Marker, 30);
        _ui.TextCenter(b, $"{HunterRig.Parts.Count} parts", 960, 1044, Ink);
    }
}
