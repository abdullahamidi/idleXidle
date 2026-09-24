using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// Turns a socket authored on a strip FRAME into a point in the ARENA — with exactly the transform that drew
/// that frame (ADR-011). Pure arithmetic over a resolved <see cref="SpriteFrame"/>.
/// </summary>
/// <remarks>
/// The sprite is drawn by <c>UiKit.ResolveFrame</c>: one frame of the strip, cropped by the strip's measured
/// pads, scaled to the actor's box and anchored bottom-centre. A socket that is converted by any other rule —
/// the body's centre plus an offset, as projectiles used to start — lands somewhere near the hand, and "near
/// the hand" is exactly the gap a thrown knife cannot have. So this reads the same Src and Dest the draw used.
/// </remarks>
public static class ActorSocketMap
{
    /// <summary>The arena point of <paramref name="socket"/> on the frame drawn as <paramref name="frame"/>.</summary>
    /// <param name="frameSize">The strip's square frame size in texture pixels (its height).</param>
    /// <param name="frameIndex">Which frame of the strip <paramref name="frame"/> shows.</param>
    public static Vector2 ToArena(SpriteFrame frame, int frameSize, int frameIndex, ActionSocket socket)
        => ToArena(frame.Src, frame.Dest, frame.Effects.HasFlag(SpriteEffects.FlipHorizontally), frameSize, frameIndex, socket);

    /// <summary>The arena point of <paramref name="socket"/> for a frame drawn from <paramref name="src"/> into <paramref name="dest"/>.</summary>
    public static Vector2 ToArena(Rectangle src, Rectangle dest, bool flipped, int frameSize, int frameIndex, ActionSocket socket)
    {
        var inFrameX = socket.X * frameSize - (src.X - frameIndex * frameSize);
        var inFrameY = socket.Y * frameSize - src.Y;
        var sx = dest.Width / (float)System.Math.Max(1, src.Width);
        var sy = dest.Height / (float)System.Math.Max(1, src.Height);
        var x = flipped ? dest.Right - inFrameX * sx : dest.X + inFrameX * sx;
        return new Vector2(x, dest.Y + inFrameY * sy);
    }

    /// <summary>The socket's prop direction in the arena, in radians (a flipped frame mirrors it).</summary>
    public static float AngleInArena(ActionSocket socket, bool flipped)
    {
        var a = MathHelper.ToRadians(socket.AngleDegrees);
        return flipped ? MathHelper.Pi - a : a;
    }

    /// <summary>How many arena pixels one strip texture pixel covers in this draw: the sprite's own scale.</summary>
    public static float DrawScale(SpriteFrame frame) => frame.Dest.Height / (float)System.Math.Max(1, frame.Src.Height);
}
