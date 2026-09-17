#!/usr/bin/env python3
"""Every asset on disk must have a live consumer — the mirror of check_asset_keys.

    python3 tools/check_asset_consumers.py                   gate: fail on any orphan not in the baseline
    python3 tools/check_asset_consumers.py --list            print every asset with how it is reached
    python3 tools/check_asset_consumers.py --write-baseline  rewrite tools/asset_orphans_baseline.txt

check_asset_keys asks "does every key the code names exist on disk?"; this asks the other
question, "does every file on disk get named by the code?" — the UI polish brief's rule that
no generated art or cue may sit in the tree without a consumer (§96-§97, §126 item 46). An
orphan is not a crash; it is a promise the pipeline made and the screen never kept, and the
generated-asset ledger (tools/asset-pipeline/file_generated.py) is only as honest as this.

HOW A FILE IS REACHED. Keys are flat basenames (AssetLibrary strips the directory and the
extension). A basename counts as consumed when any of these holds:

  1. it appears as a literal anywhere in src/ (a "key" string, a Has()/Get() call, a table);
  2. it is the TARGET of an AssetLibrary alias whose source key is a literal (the shield bar
     reuses the mana art through ui_bar_shield_fill -> ui_bar_mana_fill, say);
  3. it is built at runtime from a family the code interpolates: the file's name starts with
     the literal head of some $"head{...}" string in src/ (boss_{region}, icon_set_{capstone},
     source_{element}, music_arena_{theme}, char_{name}_{clip} ...), or reduces through a strip
     (…_strip<N>_<size>), a direction (…_sw) or a frame (…_01) suffix to a stem that is;
  4. a content list names it: a .json / .md / .txt under assets/ or design/ that the game reads.

THE BASELINE. The tree carries art from before the solo-build pivot — squad-era creature icons,
nav tabs for dropped features, an unused button kit — that nothing draws. Deleting it is a
decision for the desk, not for a gate, so tools/asset_orphans_baseline.txt lists what was
already orphaned when this gate arrived (2026-09-02) and the gate fails only on a NEW orphan:
art or a cue generated since that never found its consumer. A baseline entry that gains a
consumer or is deleted is reported so the list shrinks honestly; it never fails the gate.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src")
ART = os.path.join(ROOT, "assets", "art")
AUDIO = os.path.join(ROOT, "assets", "audio")
DATA_DIRS = [os.path.join(ROOT, "assets"), os.path.join(ROOT, "design")]
BASELINE = os.path.join(ROOT, "tools", "asset_orphans_baseline.txt")

# Files that are pipeline inputs, not game assets: sources the generators read, contact sheets,
# and the ledger itself. They are consumed by tools/, not by the game.
NOT_GAME_ASSETS = re.compile(r"(/|\\)(source|_source|sources|sheets|contact|raw|scratch)(/|\\)|\.(md|json|txt|py)$", re.I)


def basename(path: str) -> str:
    return os.path.splitext(os.path.basename(path))[0]


def rel(path: str) -> str:
    return os.path.relpath(path, ROOT).replace("\\", "/")


def sources() -> str:
    parts = []
    for path in glob.glob(os.path.join(SRC, "**", "*.cs"), recursive=True):
        text = open(path, encoding="utf-8").read()
        text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
        text = re.sub(r"^[ \t]*//.*$", "", text, flags=re.M)
        parts.append(text)
    return "\n".join(parts)


def data_text() -> str:
    parts = []
    for d in DATA_DIRS:
        for ext in ("*.json", "*.md", "*.txt", "*.csv"):
            for path in glob.glob(os.path.join(d, "**", ext), recursive=True):
                try:
                    parts.append(open(path, encoding="utf-8", errors="ignore").read())
                except OSError:
                    pass
    return "\n".join(parts)


def aliases(src: str) -> dict:
    block = re.search(r"Aliases\s*(?::|=)[^{]*\{(.*?)\n    \};", src, re.S)
    return dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', block.group(1))) if block else {}


STRIP = re.compile(r"^(.*?)_strip\d+(?:_\d+)?$")
DIRECTION = re.compile(r"^(.*?)_(?:n|ne|e|se|s|sw|w|nw)$")
FRAME = re.compile(r"^(.*?)_(?:\d{1,3}|f\d{1,3})$")
# An actor's clips: `verdant_swarm_idle_strip8_512` -> `verdant_swarm_idle` (STRIP) -> `verdant_swarm`,
# which is the literal the presentation catalogue carries (EnemyPresentation's rows and boss rows). Without
# this reducer every enemy and boss strip was an orphan the baseline had to carry by hand.
CLIP = re.compile(r"^(.*?)_(?:idle|attack|death|cast|strike|projectile|mark|trap|transformation)$")


def heads(src: str) -> set:
    """The literal heads of every interpolated string: $"boss_{region}" -> "boss_"."""
    found = set(re.findall(r'\$"([a-z0-9_]{3,})\{', src))
    found.update(re.findall(r'"([a-z0-9_]{3,}_)"\s*\+', src))   # "icon_set_" + name
    return found


def reach() -> list:
    """(relative path, how-or-None) for every game asset."""
    src = sources()
    data = data_text()
    literals = set(re.findall(r'"([a-z0-9_]+)"', src))
    alias = aliases(src)
    alias_targets = {v for k, v in alias.items() if k in literals}
    family_heads = heads(src)

    files = [p for p in glob.glob(os.path.join(ART, "**", "*.png"), recursive=True)
             + glob.glob(os.path.join(AUDIO, "**", "*.wav"), recursive=True)
             + glob.glob(os.path.join(AUDIO, "**", "*.ogg"), recursive=True)
             if not NOT_GAME_ASSETS.search(p)]

    out = []
    for path in sorted(files):
        key = basename(path)
        how = None
        if key in literals:
            how = "literal"
        elif key in alias_targets:
            how = "alias target"
        else:
            stems = {key}
            for rx in (STRIP, DIRECTION, FRAME, CLIP):
                for s in list(stems):
                    m = rx.match(s)
                    if m:
                        stems.add(m.group(1))
            for s in stems:
                if s in literals or s in alias_targets:
                    how = f"stem {s}"
                    break
                head = next((h for h in family_heads if s.startswith(h)), None)
                if head:
                    how = f"family {head}{{...}}"
                    break
            if how is None and re.search(r"\b" + re.escape(key) + r"\b", data):
                how = "content list"
        out.append((rel(path), how))
    return out


def read_baseline() -> set:
    if not os.path.exists(BASELINE):
        return set()
    return {ln.strip() for ln in open(BASELINE, encoding="utf-8") if ln.strip() and not ln.startswith("#")}


def main() -> int:
    reached = reach()
    orphans = [p for p, how in reached if how is None]

    if "--list" in sys.argv:
        for p, how in reached:
            print(f"{how or 'ORPHAN':>22}  {p}")
        print(f"\n{len(orphans)} orphan(s) of {len(reached)} game assets")
        return 0

    if "--write-baseline" in sys.argv:
        with open(BASELINE, "w", encoding="utf-8", newline="\n") as f:
            f.write("# Assets nothing consumed when tools/check_asset_consumers.py arrived (2026-09-02).\n")
            f.write("# Pre-pivot debt: delete at the desk, or wire it. A NEW orphan is not allowed on this list\n")
            f.write("# without saying why here; the gate fails on any orphan the list does not name.\n")
            for p in orphans:
                f.write(p + "\n")
        print(f"baseline written: {len(orphans)} orphan(s) -> {rel(BASELINE)}")
        return 0

    baseline = read_baseline()
    new = [p for p in orphans if p not in baseline]
    healed = sorted(p for p in baseline if p not in orphans)
    if healed:
        print(f"note: {len(healed)} baseline orphan(s) now consumed or gone — drop them from "
              f"{rel(BASELINE)}:")
        for p in healed:
            print("   " + p)
    if new:
        print(f"\n{len(new)} NEW asset(s) with no consumer — wire them, or delete them:\n")
        for p in new:
            print("   " + p)
        print(f"\n({len(orphans) - len(new)} older orphan(s) are carried by {rel(BASELINE)}.)")
        return 1
    print(f"no new orphans: every asset generated since the baseline is reached by the code "
          f"({len(orphans)} pre-pivot orphan(s) still carried by {rel(BASELINE)}).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
