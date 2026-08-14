# Shared toolchain resolution for every script in tools/. Source it, do not run it:
#
#     . "$(dirname "${BASH_SOURCE[0]}")/shellenv.sh"
#
# WHY THIS EXISTS. The scripts here were written on Windows+WSL and hard-coded three
# things that are true only there: the interpreter is named `python3`, the .NET SDK lives
# at /mnt/c/Program Files/dotnet and must be reached through cmd.exe, and `wslpath`
# converts a path. Move the repo to a native-Windows checkout — same machine, Git Bash
# instead of WSL — and all three are wrong at once: the gates die on "Python not found",
# and capture.sh dies on `wslpath: command not found`, which took the screenshot rig (the
# project's only visual verification) with it.
#
# The fix is to PROBE rather than to NAME. Each helper below tries the candidates in order
# and keeps the first that actually answers, so the same script runs unmodified from WSL,
# Git Bash, or a Linux checkout.
#
# Provides: py <args>        a working Python 3
#           winpath <path>   a path in Windows form (for anything handed to a Windows exe)
#           dn <args>        the .NET SDK, with $RH_ENV applied as environment
#           have_native_dotnet / have_wsl   for the rare branch that must know

# --- Python -----------------------------------------------------------------------
#
# `command -v python3` is NOT proof of an interpreter on Windows. Windows ships an App
# Execution Alias at %LOCALAPPDATA%\Microsoft\WindowsApps\python3.exe whose entire job is
# to print a localized "Python was not found" and send you to the Store — it resolves,
# it is executable, and it is not Python. Meanwhile the python.org installer lays down
# python.exe and NO python3.exe, so the working interpreter is the one with the wrong
# name and the broken one has the right name. Only running it can tell them apart.
_rh_pick_python() {
  local cand
  for cand in python3 python "py -3" \
              "$LOCALAPPDATA/Programs/Python/Python313/python.exe" \
              "$LOCALAPPDATA/Programs/Python/Python312/python.exe" \
              "/c/Program Files/Python313/python.exe" \
              "/c/Program Files/Python312/python.exe"; do
    # Unquoted on purpose: "py -3" has to split into command + argument.
    if $cand -c "import sys; sys.exit(0 if sys.version_info[0] == 3 else 1)" >/dev/null 2>&1; then
      read -r -a _RH_PY <<<"$cand"
      return 0
    fi
  done
  return 1
}
_rh_pick_python || {
  echo "tools/shellenv.sh: no working Python 3 found." >&2
  echo "  Install one (winget install --id Python.Python.3.12 -e) or put it on PATH." >&2
  echo "  Note: a bare 'python3' on Windows is usually the Store stub, not an interpreter." >&2
  return 1 2>/dev/null || exit 1
}
py() { "${_RH_PY[@]}" "$@"; }

# --- Paths ------------------------------------------------------------------------
#
# Anything handed to a Windows process (dotnet, cmd.exe, RH_SHOT) needs C:\form, even
# when the shell is using /mnt/c or /c. wslpath is WSL-only; cygpath is Git-Bash-only;
# a native Windows shell already has the right form.
have_wsl() { command -v wslpath >/dev/null 2>&1; }
winpath() {
  if command -v wslpath >/dev/null 2>&1; then wslpath -w "$1"
  elif command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"
  else printf '%s\n' "$1"
  fi
}

# --- Processes ---------------------------------------------------------------------
#
# The pipeline's "wait for generation to drain" loops need to ask whether a job is still
# running. `pgrep` is not present in Git Bash, and a missing command returns non-zero —
# which a `while proc_running ...` loop reads as "nothing is running" and falls straight
# through to derive_statics, cutting statics from half-written strips. So this refuses to
# answer rather than guess wrong.
#
# Match on the SCRIPT name, never on the interpreter: the process is `python3 -u ...` on
# one machine and `python.exe -u ...` on another.
proc_running() {
  local pat="$1"
  if command -v pgrep >/dev/null 2>&1; then
    pgrep -f "$pat" >/dev/null 2>&1
    return
  fi
  if command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -Command \
      "exit ((Get-CimInstance Win32_Process | Where-Object { \$_.CommandLine -match '$pat' } | Measure-Object).Count -eq 0)" \
      >/dev/null 2>&1
    return
  fi
  echo "shellenv: cannot enumerate processes (no pgrep, no powershell.exe)." >&2
  echo "  Refusing to assume the generation jobs finished — that would cut statics from" >&2
  echo "  half-written strips. Wait for them by hand, then re-run with the wait removed." >&2
  exit 1
}

# --- .NET -------------------------------------------------------------------------
#
# From WSL there is no Linux SDK and no way to open a window, so the call is handed to
# cmd.exe. From Git Bash the SDK is on PATH and direct invocation is both simpler and
# quotes better — so prefer it whenever it actually runs.
have_native_dotnet() { command -v dotnet >/dev/null 2>&1 && dotnet --version >/dev/null 2>&1; }

# Environment for `dn`, as NAME=VALUE entries. Set it per call site:
#     RH_ENV=(RH_SHOT="$out" RH_SHOT_MODE=fight); dn run --project ...
RH_ENV=()

dn() {
  if have_native_dotnet; then
    if [ ${#RH_ENV[@]} -gt 0 ]; then env "${RH_ENV[@]}" dotnet "$@"; else dotnet "$@"; fi
  else
    # cmd.exe has no per-command env prefix, so each variable becomes its own `set`.
    # The `&&` must hug the value with no space before it or the space joins the value.
    local prefix="" kv
    for kv in ${RH_ENV[@]+"${RH_ENV[@]}"}; do prefix+="set $kv&& "; done
    local args="$*"
    # Backslashes: cmd.exe wants src\Foo, and forward slashes survive as project paths
    # anyway, so only the leading `cd /d` target needs conversion.
    cmd.exe /c "cd /d $(winpath "$PWD") && ${prefix}dotnet $args"
  fi
}
