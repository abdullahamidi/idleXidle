#!/usr/bin/env python3
"""The light an effect strip puts on screen, frame by frame, at the size and timing the fight gives it.

    py tools/fx_energy.py                    # every strip in assets/art/VFX, one line each
    py tools/fx_energy.py <strip.png>...     # these strips, with the full per-frame table

WHAT IT MEASURES. Under the VFX blend contract (ADR-009) a texel adds `luminance x alpha` of light, once,
and the fight draws an effect at roughly 0.3x its 512-px frame. So the light is averaged over 4x4 blocks:
a 128-cell grid that stands in for the drawn size. A needle one source pixel wide averages down to almost
nothing here, exactly as it does on screen, even though its 512-px frame "has partial alpha". Every
frame is then weighted by the renderer's own tail fade (VfxPlayer.Anim.Fade, applied through
VfxBlend.Light: light k^6 over the last 35 % of life), because a pop the renderer already hides is not
one the player sees.

Per frame:
  energy   total light, as a share of a fully white frame (fade-weighted)
  bright   cells whose light is at least BRIGHT: a core the eye can find at combat size
  core     bright cells within CORE_R of the frame centre (fade-weighted)
  radius   light-weighted distance from the centre, as a share of the half-frame (for the table)

Two readings are built on the curve:

  LIVE      The impact must be readable. Every frame from the first to the last one still holding
            WINDOW of the peak energy (the impact window) needs at least LIVE_MIN bright cells. The fade
            after the window is exempt, so a burst may dim to nothing, but a strip that is faint for its
            whole life fails. Calibrated 2026-09-23: the needle candidate that vanished in the arena
            had 50 in its weakest impact frame; the weakest of all 67 production strips has 192 (fx_magpie_trap). Hard
            rule in check_fx_edges.py.

  ONE PEAK  An IMPACT has one peak. After the primary peak, two things read as a second hit:
            REGROW   the energy climbs back by more than REGROW of the peak from its lowest point since;
            RE-FORM  the light gathers back at the CENTRE: the core climbs back by at least
                     REFORM_CELLS cells, and by at least REFORM_REL of the core at the peak.
            The regenerated Seeker strike that was rejected on 2026-09-23 re-formed 84 cells (2.6x):
            a compact second starburst ~300 ms after the blow. It is a DIAGNOSTIC, not a gate. Marks
            close on purpose (anvil_mark's brackets lock into an X), traps and fields are held shapes, and a
            PNG cannot say which grammar it was drawn to. check_fx_edges.py prints it as a warning for
            the impact forms.

Pure python on tools/asset-pipeline/pixelpng.py, like every gate here (`py` has no Pillow or numpy).
Samples every 2nd pixel on both axes inside each block; the curve's shape does not hide between them.
"""
import glob
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "asset-pipeline"))
import pixelpng  # noqa: E402

BLOCK = 4             # 512 -> 128 cells: the ~0.28x the strike is drawn at
BRIGHT = 0.40         # cell light that reads as a core at combat size
CORE_R = 0.30         # the centre, as a share of the half-frame
LIVE_MIN = 150        # bright cells every impact-window frame must keep
WINDOW = 0.50         # the impact window runs while energy >= this share of the peak
REGROW = 0.25         # energy climbing back, as a share of the peak
REFORM_CELLS = 48     # core cells climbing back ...
REFORM_REL = 1.0      # ... and as a multiple of the core at the peak
TAIL = 0.35           # VfxPlayer.Anim.Fade: the last 35 % of life fades as k^3 ...
TAIL_POWER = 6        # ... and VfxBlend.Light squares the draw opacity, so light goes as k^6

# The forms whose grammar IS one impact: the ONE PEAK reading applies to these.
IMPACT_FORMS = ("strike", "hit", "crit", "weakhit", "death", "shield_break")


class Frame:
    __slots__ = ("energy", "bright", "core", "radius", "fade")

    def __init__(self, energy, bright, core, radius, fade):
        self.energy, self.bright, self.core, self.radius, self.fade = energy, bright, core, radius, fade


def fade_light(frame, frames):
    """The renderer's tail-fade light factor at this frame's midpoint."""
    t = (frame + 0.5) / frames
    if t <= 1 - TAIL:
        return 1.0
    return ((1 - t) / TAIL) ** TAIL_POWER


def curve(src):
    """Per-frame readings for a strip of square frames (a path or a pixelpng image)."""
    img = pixelpng.read(src) if isinstance(src, str) else src
    fw = img.h
    if fw <= 0 or img.w % fw:
        raise ValueError(f"{img.w}x{img.h} is not a whole number of square frames")
    px, W = img.px, img.w
    n = img.w // fw
    cells = fw // BLOCK
    half = fw / 2.0
    out = []
    for f in range(n):
        x0 = f * fw
        w = fade_light(f, n)
        total = wr = 0.0
        bright = core = 0
        for cy in range(cells):
            by = cy * BLOCK
            ddy = (cy + 0.5) * BLOCK - half
            for cx in range(cells):
                bx = x0 + cx * BLOCK
                s = 0
                for dy in (0, 2):
                    row = ((by + dy) * W + bx) * 4
                    for dx in (0, 8):          # 2 px apart, 4 bytes a pixel
                        i = row + dx
                        a = px[i + 3]
                        if a:
                            s += (px[i] * 299 + px[i + 1] * 587 + px[i + 2] * 114) * a
                if not s:
                    continue
                light = s / (4 * 1000 * 255 * 255)      # mean of 4 samples, 0..1
                ddx = (cx + 0.5) * BLOCK - half
                r = math.sqrt(ddx * ddx + ddy * ddy) / half
                total += light
                wr += light * r
                if light >= BRIGHT:
                    bright += 1
                    if r < CORE_R:
                        core += 1
        out.append(Frame(w * total / (cells * cells), bright, w * core, wr / total if total else 0.0, w))
    return out


def impact_window(frames):
    """Frames 0..N, where N is the last frame still holding WINDOW of the peak energy."""
    peak = max(fr.energy for fr in frames)
    return range(max(i for i, fr in enumerate(frames) if fr.energy >= WINDOW * peak) + 1) if peak > 0 else range(0)


def live_problems(frames):
    """LIVE: the impact window must keep a combat-scale core."""
    if max(fr.energy for fr in frames) <= 0:
        return ["no light at all"]
    dim = [i for i in impact_window(frames) if frames[i].bright < LIVE_MIN]
    if dim:
        worst = min(frames[i].bright for i in dim)
        return [f"impact frame(s) {dim} keep only {worst} bright cells at combat size (needs {LIVE_MIN}) "
                f"- too faint to read in the fight"]
    return []


def one_peak_findings(frames):
    """ONE PEAK: a second hit after the primary peak - energy climbing back, or light re-forming at the centre."""
    found = []
    e = [fr.energy for fr in frames]
    p = e.index(max(e))
    low = e[p]
    for k in range(p + 1, len(e)):
        low = min(low, e[k])
        if e[k] - low > REGROW * e[p]:
            found.append(f"REGROW: energy climbs back {100 * (e[k] - low) / e[p]:.0f}% of the peak at frame {k}")
            break
    c = [fr.core for fr in frames]
    ref = max(c[p], 0.05 * frames[p].bright * frames[p].fade, 1.0)
    low = c[p]
    for k in range(p + 1, len(c)):
        low = min(low, c[k])
        if c[k] - low >= REFORM_CELLS and c[k] - low >= REFORM_REL * ref:
            found.append(f"RE-FORM: the light gathers back at the centre at frame {k} "
                         f"(+{c[k] - low:.0f} core cells, {(c[k] - low) / ref:.1f}x the core at the peak)")
            break
    return found


def is_impact(path):
    """Whether a strip's key names an impact form (fx_<form> or fx_<char>_<form>)."""
    key = os.path.basename(path).replace("_strip8_512.png", "")
    return any(key == f"fx_{f}" or key.endswith(f"_{f}") for f in IMPACT_FORMS)


def spark(values, top=None):
    """A one-line sparkline, so a curve reads in a terminal."""
    bars = " .:-=+*#%@"
    top = top if top else (max(values) or 1)
    return "".join(bars[min(9, int(9 * v / top + 0.5))] for v in values)


def main(argv=None):
    named = list(sys.argv[1:] if argv is None else argv)
    paths = named or sorted(glob.glob("assets/art/VFX/*/fx_*_strip8_512.png"))
    for p in paths:
        frames = curve(p)
        live, peak = live_problems(frames), one_peak_findings(frames)
        tag = ("LIVE! " if live else "      ") + (("2HIT! " if is_impact(p) else "2hit? ") if peak else "      ")
        print(f"{tag} energy[{spark([fr.energy for fr in frames])}] core[{spark([fr.core for fr in frames])}] "
              f"impact-window bright min {min((frames[i].bright for i in impact_window(frames)), default=0):5}  "
              f"{os.path.relpath(p)}")
        if named:
            print("        frame   energy   bright     core   radius   fade")
            for i, fr in enumerate(frames):
                print(f"        {i:5}  {fr.energy:7.4f}  {fr.bright:7}  {fr.core:7.1f}  {fr.radius:7.3f}  {fr.fade:5.3f}")
            for msg in live + peak:
                print(f"        - {msg}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
