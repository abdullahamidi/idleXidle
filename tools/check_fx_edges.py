#!/usr/bin/env python3
"""Every VFX strip must fade into the scene instead of ending on a rectangle.

    python3 tools/check_fx_edges.py                 # every strip in assets/art/VFX
    python3 tools/check_fx_edges.py <strip.png>...  # just these (a candidate, before install)

THREE RULES. The second is the one the old library fails; the third is the one a soft candidate fails.

  EDGE   No frame may carry visible alpha on its outer border ring. An effect whose rays run off the
         canvas is cut flat by it, and under additive blending that cut is a straight bright line on
         a dark floor — the artifact players have now reported twice ("efekt bir dikdörtgen şeklinde
         çıkıp kaybolduğu çok belli oluyor", 2026-08-23; and again 2026-09-22, on THE SEEKER's own
         signature, whose strip had 12.4 % of its border ring at full opacity).

  SOFT   A frame must contain some PARTIAL alpha. This is the rule that actually catches the defect.
         The generator returns ONE-BIT art: measured across the library, 55 of 67 strips had exactly
         two alpha values, 0 and 255, and 1.0 % of all effect pixels carried anything in between. A
         shape with no partial alpha cannot fade into anything, so EVERY edge of it is a step —
         including the ones nowhere near the border, which the EDGE rule above cannot see. The trap
         strips' hard edges sat 77-112 px inside the frame, where `feather_fx.py`'s outer-band ramp
         never reached.

  LIVE   The impact must be READABLE AT COMBAT SIZE (added 2026-09-23). A strip can pass both rules above
         and still be invisible in the fight: soft haze over one-pixel needles has partial alpha and no
         edge, and at the 0.3x the arena draws it at there is nothing left to see. The regenerated Seeker
         strike did exactly that. tools/fx_energy.py measures the light a frame puts on screen at combat
         scale, and every frame of the impact window must keep a core (see LIVE there). The fade after
         the window is exempt.

A fourth reading is PRINTED, NOT ENFORCED: fx_energy's ONE PEAK, a second hit after the first (the light
climbing back, or gathering back at the centre). It is shown as a warning for the IMPACT forms only,
because marks lock on by closing, traps and fields hold a shape, and a PNG cannot say which grammar it
was drawn to. On the 2026-09-23 library it flagged the rejected Seeker pilot and nothing else among the
impact strips; that is too little evidence for a hard rule yet.

WHAT THIS DOES NOT PROVE. It is a contract check, not an art review: it cannot tell a beautiful
burst from an ugly one, and it cannot tell that a "trap" is drawn as a drum rather than as the
shards its own prompt asked for. It only refuses the mechanical faults that make an effect read
as a pasted rectangle or as nothing at all. Judge the look on a contact sheet (`rhart.py sheet`), and
the motion in the arena at play speed (`capture_seq.sh`, one 16.7 ms step per picture).

COST. The border ring is read in full — it is the thing under test. The partial-alpha sample is
strided, because one-bit-ness is a global property of a generated strip and does not hide in the
pixels between the samples; a full scan of the library is 140 M pixels. It is meant for CI once the
library passes, and is NOT wired into check_all.sh yet (2026-09-23: 60 of 67 strips still fail SOFT).

Pure python: tools/asset-pipeline/pixelpng.py is the dependency-free PNG reader every gate shares,
because `py` on this machine has no Pillow.
"""
import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "asset-pipeline"))
import pixelpng  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fx_energy  # noqa: E402

# The alpha floor the renderer itself treats as "something is here" (ItemArtMetrics uses the same 8).
FLOOR = 8
# A border pixel above this is a visible cut, not a stray sampling artifact.
EDGE_MAX = 24
# Every 4th pixel on both axes: enough to characterise a strip's alpha vocabulary, 16x cheaper.
STRIDE = 4
# Below this share of partial-alpha pixels a frame has no falloff worth the name. The feathered
# strips that read correctly measure 20-40 %; the one-bit strips measure exactly 0.
SOFT_MIN = 0.02


def frames_of(img, path):
    """The strip's frame width, from the square-frame convention the filenames state."""
    if img.h <= 0 or img.w % img.h:
        return None, f"{img.w}x{img.h} is not a whole number of square frames"
    return img.h, None


def check(path):
    img = pixelpng.read(path)
    fw, why = frames_of(img, path)
    if why:
        return [why]
    n = img.w // fw
    problems = []

    # The alpha byte read straight out of the buffer: img.get() builds a 4-tuple per pixel, and this
    # gate touches a few million of them.
    px, W = img.px, img.w
    worst_edge = 0
    soft = 0
    seen = 0
    for f in range(n):
        x0 = f * fw
        # EDGE — the full border ring of this frame.
        top = (0 * W + x0) * 4 + 3
        bot = ((img.h - 1) * W + x0) * 4 + 3
        for i in range(fw):
            a = px[top + i * 4]
            if a > worst_edge:
                worst_edge = a
            a = px[bot + i * 4]
            if a > worst_edge:
                worst_edge = a
        for y in range(img.h):
            row = (y * W) * 4 + 3
            a = px[row + x0 * 4]
            if a > worst_edge:
                worst_edge = a
            a = px[row + (x0 + fw - 1) * 4]
            if a > worst_edge:
                worst_edge = a
        # SOFT — a strided sample of the interior.
        for y in range(0, img.h, STRIDE):
            row = (y * W) * 4 + 3
            for x in range(0, fw, STRIDE):
                a = px[row + (x0 + x) * 4]
                seen += 1
                if FLOOR < a < 248:
                    soft += 1

    share = soft / seen if seen else 0.0
    if worst_edge > EDGE_MAX:
        problems.append(f"alpha {worst_edge} on the frame border — the effect is cut flat by its canvas")
    if share < SOFT_MIN:
        problems.append(f"only {share * 100:.2f}% partial alpha — one-bit art cannot fade (needs {SOFT_MIN * 100:.0f}%)")
    frames = fx_energy.curve(img)
    problems.extend(fx_energy.live_problems(frames))
    warnings = fx_energy.one_peak_findings(frames) if fx_energy.is_impact(path) else []
    return problems, worst_edge, share, warnings


def main(argv=None):
    # Named strips are measured instead of the library — how a single regenerated candidate is
    # compared against the production strip it would replace, before either is installed.
    named = list(sys.argv[1:] if argv is None else argv)
    paths = named or sorted(glob.glob("assets/art/VFX/*/fx_*_strip8_512.png"))
    if not paths:
        print("   no VFX strips found", file=sys.stderr)
        return 1
    bad = 0
    print(f"   {'edge':>5}{'soft%':>8}  asset")
    for p in paths:
        got = check(p)
        if isinstance(got, list):
            print(f"   FAIL  {p}: {got[0]}")
            bad += 1
            continue
        problems, edge, share, warnings = got
        mark = "" if not problems else "   <== " + "; ".join(problems)
        if problems:
            bad += 1
        print(f"   {edge:>5}{share * 100:>7.2f}%  {os.path.relpath(p)}{mark}")
        for w in warnings:
            print(f"                 warning (not enforced) {w}")
    total = len(paths)
    if bad:
        print(f"\n   {bad} of {total} VFX strips end on a rectangle, cannot fade, or cannot be seen.")
        print("   Regenerate through tools/asset-pipeline/v2/fxclips.py, whose post-pass is")
        print("   whiten -> glow -> soften -> feather. Do NOT re-scissor at runtime.")
        return 1
    print(f"   all {total} VFX strips fade before their frame edge and keep a readable core.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
