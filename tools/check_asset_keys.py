#!/usr/bin/env python3
"""Every asset key the game asks for by name must exist on disk.

    python3 tools/check_asset_keys.py

Same failure family as the font gate, one layer up. `AssetLibrary.Get` returns null for a
key it does not have, and every caller is written to fall back to a coloured rectangle or
to nothing at all — deliberately, so a missing file can never crash a screen. The cost of
that kindness is that a typo, a renamed file or a deleted sprite is INVISIBLE: the screen
still draws, just without the art, and it looks like a design choice.

Keys are flat basenames (AssetLibrary strips the directory and the extension), so this
walks assets/art for every image, applies the same alias table the loader uses, and
compares against every literal handed to Get / GetFirst / Has / SpriteFit / AnimSprite and
friends.

WHAT IT CANNOT SEE, and does not pretend to: keys built at runtime from data —
$"boss_{region}", Character.StripKey(clip), the per-trait item glyphs. Those are covered by
their own tests and by the roster fixtures. This catches the hand-typed ones, which is
where typos live.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, "src", "IdleXIdle.Game")
ART = os.path.join(ROOT, "assets", "art")
AUDIO = os.path.join(ROOT, "assets", "audio")

# The call sites that take a key. Anything else that grows one should be added here.
CALLS = re.compile(
    r'\b(?:Assets\.Get|Assets\.GetFirst|Assets\.Has|Get|GetFirst|Has|SpriteFit|Sprite|'
    r'SpriteGrounded|AnimSprite|Background|BarArt|Panel|PanelNine|Icon|_vfx\.Play)\s*\(\s*([^)]*)')
# `_vfx.Play` is VfxPlayer.Play — the 2026-08-22 art pass keys every combat effect by name (fx_strike,
# fx_hit ...) through the alias table, and an effect nothing can find is the house failure mode: it fails
# soft and the fight simply has no flash where one was promised. Qualified on purpose: a bare `Play`
# also matches SoundBank.Play, whose keys are audio and are checked by SOUND_CALL below.
LITERAL = re.compile(r'"([a-z0-9_]+)"')


def on_disk() -> set:
    keys = set()
    for path in glob.glob(os.path.join(ART, "**", "*.png"), recursive=True):
        keys.add(os.path.splitext(os.path.basename(path))[0])
    return keys


def aliases() -> dict:
    """The loader's migration table, parsed out of AssetLibrary rather than duplicated."""
    src = open(os.path.join(GAME, "AssetLibrary.cs"), encoding="utf-8").read()
    block = re.search(r"Aliases\s*(?::|=)[^{]*\{(.*?)\n    \};", src, re.S)
    if not block:
        return {}
    return dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', block.group(1)))


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"^[ \t]*//.*$", "", text, flags=re.M)


def cues() -> set:
    return {os.path.splitext(os.path.basename(p))[0]
            for p in glob.glob(os.path.join(AUDIO, "**", "*.wav"), recursive=True)}


SOUND_CALL = re.compile(r'\b(?:PlayFirst|Play|PlayMusic)\s*\(([^)]*)')


def main() -> int:
    have = on_disk()
    alias = aliases()
    missing = {}

    for path in sorted(glob.glob(os.path.join(GAME, "*.cs"))):
        text = strip_comments(open(path, encoding="utf-8").read())
        for line_no, line in enumerate(text.split("\n"), 1):
            for call in CALLS.finditer(line):
                for key in LITERAL.findall(call.group(1)):
                    # A bare word with no underscore is almost never an asset key — it is a
                    # label, a mode name, a format. Keys in this project are all snake_case.
                    if "_" not in key:
                        continue
                    resolved = alias.get(key, key)
                    if resolved in have or key in have:
                        continue
                    missing.setdefault(key, []).append(
                        f"{os.path.basename(path)}:{line_no}")

    # AND THE SOUND CUES AND MUSIC BEDS, which fail exactly the same way: SoundBank no-ops on a
    # name it does not have. Five of the seven cues and ALL SIX music tracks were called for the
    # whole of development and played silence — the looping player was written, wired per screen,
    # given a per-theme arena variant with a fallback, and never given a file.
    wavs = cues()
    for path in sorted(glob.glob(os.path.join(GAME, "*.cs"))):
        text = strip_comments(open(path, encoding="utf-8").read())
        for line_no, line in enumerate(text.split("\n"), 1):
            for call in SOUND_CALL.finditer(line):
                for key in re.findall(r'"((?:sfx|music)_[a-z0-9_]+)"', call.group(1)):
                    if key not in wavs:
                        missing.setdefault(key, []).append(
                            f"{os.path.basename(path)}:{line_no}")

    # AND THE ONE INTERPOLATED FAMILY THAT FAILS QUIETLY INSTEAD OF LOUDLY.
    #
    # Everything above only sees fully-literal keys, and the docstring is honest that runtime-built
    # names are out of scope because their own tests cover them. `music_arena_{theme}` is the
    # exception worth hard-coding, because it does not merely fail — it SUCCEEDS, quietly, into the
    # fallback. Game1.UpdateMusic reads `Has($"music_arena_{theme}") ? ... : "music_combat"`, so a
    # missing or misnamed bed is not silence anyone would notice; it is the generic combat track,
    # which is exactly what a region without a bed is supposed to sound like. That branch went
    # untaken for the whole of development and nothing said so.
    #
    # The themes come from Regions.cs, so adding a seventh region fails this the day it lands
    # rather than the day someone notices two regions share a bed.
    regions_cs = os.path.join(ROOT, "src", "IdleXIdle.Core", "Encounters", "Regions.cs")
    if os.path.exists(regions_cs):
        themes = set(re.findall(r"Source\.([A-Z][a-z]+)", open(regions_cs, encoding="utf-8").read()))
        for theme in sorted(themes):
            key = f"music_arena_{theme.lower()}"
            if key not in wavs:
                missing.setdefault(key, []).append("Game1.cs (UpdateMusic, per-theme arena bed)")

    # AND THE SOURCE GLYPHS on the vault's chest cards. VaultScreen.SourceGlyphKey builds
    # source_<element> for the six Source values and falls back to a coloured diamond when the file
    # is absent — the same quiet success as the class icons below. All six shipped with the item
    # pack, so any one going missing is a regression, not a pending delivery: every Source named in
    # Regions.cs must have its glyph.
    for theme in sorted(themes) if os.path.exists(regions_cs) else []:
        key = f"source_{theme.lower()}"
        if key not in have:
            missing.setdefault(key, []).append("VaultScreen.cs (SourceGlyphKey, per-element chest glyph)")

    # AND THE GEAR CLASS ICONS, the second interpolated family. ItemClasses.IconKey builds
    # icon_class_<class> for the five ItemClass values, and UiKit.ClassIcon draws a diamond in the
    # class colour when the file is absent — so, like the arena beds, a missing one does not fail, it
    # succeeds quietly into the fallback. The art is owed (2026-08-26) and may land after the code:
    # while NONE of the five exists the gate says so and passes, because the fallback is the design
    # until the art ships; the moment ANY of them exists all five must, because four badges with
    # painted icons and one with a diamond is the "unfinished" look the stat rows had.
    classes_cs = os.path.join(ROOT, "src", "IdleXIdle.Core", "Economy", "ItemClasses.cs")
    if os.path.exists(classes_cs):
        names = re.findall(r"^\s+(\w+),\s*$", re.search(r"public enum ItemClass\s*\{(.*?)\}",
                           open(classes_cs, encoding="utf-8").read(), re.S).group(1), re.M)
        wanted = [f"icon_class_{n.lower()}" for n in names]
        shipped = [k for k in wanted if k in have]
        if not shipped:
            print(f"note: no gear class icon has shipped yet ({', '.join(wanted)}); "
                  "UiKit.ClassIcon draws the class-colour diamond until they do.")
        else:
            for key in wanted:
                if key not in have:
                    missing.setdefault(key, []).append(
                        f"ItemClasses.IconKey (class icons: {len(shipped)}/{len(wanted)} shipped)")

    # AND THE ALIAS TABLE ITSELF. A migration alias whose TARGET has been deleted is the same
    # silent hole one indirection further along, and it is likelier than a typo — the alias
    # exists precisely because the file it points at was renamed once already.
    for key, target in sorted(alias.items()):
        if target not in have:
            missing.setdefault(f"{key} -> {target}", []).append("AssetLibrary.cs (alias table)")

    if not missing:
        print(f"every hand-typed asset key resolves ({len(have)} images, "
              f"{len(alias)} aliases, {len(cues())} cues).")
        return 0

    print("KEYS WITH NO FILE — these draw a fallback shape, or play silence:\n")
    for key, where in sorted(missing.items()):
        seen = ", ".join(dict.fromkeys(where))[:110]
        print(f"  {key:<34} {seen}")
    print("\nEither the art is missing, the name drifted, or the key is built at runtime "
          "and this gate cannot see it — in which case say so here.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
