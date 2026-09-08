using System;
using System.IO;
using System.Text;

// THE CONSOLE IS UTF-8 TEXT (2026-09-06). Left to itself on Windows, a redirected stdout/stderr is
// written in the console's ANSI code page (1254 here), and the runtime's own unhandled-exception
// printer follows suit — so a capture log holding this repo's path (Masaüstü), a × or an — came out
// as ISO-8859 bytes that grep called a binary file. Out and Error are rebuilt over the standard
// handles as UTF-8 without a BOM from the first line (Console.OutputEncoding cannot be set: this is
// a windowed app with no console, and the setter wants one), and a crash is printed HERE, through
// them, with its whole trace, then the process exits 1: the failure stays explicit, in text every
// tool can read.
var utf8 = new UTF8Encoding(false);
Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
try
{
    using var game = new IdleXIdle.Game.Game1();
    game.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unhandled exception. {ex}");
    Console.Error.Flush();
    return 1;
}
return 0;
