@echo off
REM Double-click this to play. Builds first, then launches.
REM
REM It exists because "how do I start it" should not be a question with a wrong answer. The bash
REM scripts in tools/ need a bash; this needs nothing but the .NET SDK, and it runs from wherever
REM the repo happens to sit because it cd's to its own folder rather than to a remembered path.
REM
REM If the window closes instantly, the error is on screen above this line — the PAUSE at the
REM bottom is there so a crash cannot vanish before it is read.

cd /d "%~dp0"

echo Building...
dotnet build src\IdleXIdle.Game -v q --nologo
if errorlevel 1 (
    echo.
    echo BUILD FAILED - the game was not started. The compiler errors are above.
    pause
    exit /b 1
)

echo Starting IDLExIDLE...
dotnet run --project src\IdleXIdle.Game --no-build
if errorlevel 1 (
    echo.
    echo The game exited with an error. The details are above.
    pause
    exit /b 1
)
