using System;
using System.Runtime.InteropServices;

namespace ResonanceHunter.Client;

/// <summary>
/// Clipboard access through the SDL2 the DesktopGL runtime already ships — share codes travel by
/// copy-paste, and MonoGame 3.8 exposes no clipboard API of its own.
/// </summary>
/// <remarks>
/// Failure is soft on purpose: a missing native library turns copy into a no-op and paste into an
/// empty string, and the UI's own "clipboard is empty" message covers it — a share button must
/// never be able to crash the game.
/// </remarks>
internal static class ClipboardInterop
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetClipboardText();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_free(IntPtr mem);

    public static bool TrySet(string text)
    {
        try { return SDL_SetClipboardText(text ?? "") == 0; }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }

    public static string Get()
    {
        try
        {
            var p = SDL_GetClipboardText();
            if (p == IntPtr.Zero) return "";
            var s = Marshal.PtrToStringUTF8(p) ?? "";
            SDL_free(p);
            return s;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return ""; }
    }
}
