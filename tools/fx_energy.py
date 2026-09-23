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

  PER ARCHETYPE (2026-09-23 cohort). A strip's archetype comes from spec.json effects.archetypes, and it
            gets the reading its motion was drawn to. None of them is a gate:
            IMPACT      ONE PEAK (above)
            PROJECTILE  FLIGHT: no centroid jump, no light swing between frames (a reset or second launch)
            TRAP/MARK   SETTLE: after establishing, no fade-and-return (a blink or a second placement)
            FIELD/AURA, SHIELD and every DUAL strip: LOOP, unweighted because held effects never fade.
                        No silhouette reset between frames, the 7 -> 0 seam no worse than a normal step, and
                        light swinging no more than LOOP_SWING (the renderer does the breathing).
            On the cohort it caught the shield loop that flashed 3.1x, and it passed the mark whose old
            strip broke its seam.

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

# Shape continuity is read on a coarse 32-cell grid (4 x 4 of the combat cells): a cell is OCCUPIED when
# its mean light reaches OCC_LIGHT. Coarse on purpose — a splinter moving one cell is continuity, not a reset.
OCC_BLOCK = 4
OCC_LIGHT = 0.08
FLIGHT_JUMP = 0.25     # PROJECTILE: centroid moving more than this share of the frame in one step
FLIGHT_SWING = 2.0     # PROJECTILE: raw energy changing by more than this factor in one step
SETTLE_LOW = 0.50      # TRAP/MARK: after establishing, dropping below this share of its peak ...
SETTLE_BACK = 0.75     # ... and climbing back above this share reads as a second placement / a blink
LOOP_RESET = 0.35      # HELD: consecutive silhouettes overlapping less than this (IoU) is a reset
LOOP_SEAM = 0.60       # HELD: the 7 -> 0 seam overlapping less than this share of the median step
LOOP_SWING = 2.0       # HELD: raw energy max/min beyond this is a pulse too big to read as breathing


class Frame:
    """One frame's readings. `energy`/`core` carry the one-shot tail fade; `raw`, the centroid and the
    occupancy do not, because a HELD effect loops and never fades, and shape continuity is a property
    of the art, not of how brightly the renderer happens to be drawing it."""
    __slots__ = ("energy", "bright", "core", "radius", "fade", "raw", "cx", "cy", "occ", "angle", "gx", "gy")

    def __init__(self, energy, bright, core, radius, fade, raw=0.0, cx=0.5, cy=0.5, occ=frozenset(),
                 angle=0.0, gx=0.5, gy=0.5):
        self.energy, self.bright, self.core, self.radius, self.fade = energy, bright, core, radius, fade
        self.raw, self.cx, self.cy, self.occ = raw, cx, cy, occ
        self.angle, self.gx, self.gy = angle, gx, gy


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
        total = wr = sx = sy = sxx = syy = sxy = 0.0
        hot = []
        bright = core = 0
        coarse = {}
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
                sx += light * (cx + 0.5)
                sy += light * (cy + 0.5)
                sxx += light * (cx + 0.5) ** 2
                syy += light * (cy + 0.5) ** 2
                sxy += light * (cx + 0.5) * (cy + 0.5)
                hot.append((light, cx + 0.5, cy + 0.5))
                key = (cx // OCC_BLOCK, cy // OCC_BLOCK)
                coarse[key] = coarse.get(key, 0.0) + light
                if light >= BRIGHT:
                    bright += 1
                    if r < CORE_R:
                        core += 1
        occ_min = OCC_LIGHT * OCC_BLOCK * OCC_BLOCK
        angle, gx, gy = 0.0, 0.5, 0.5
        if total:
            mx, my = sx / total, sy / total
            cxx, cyy, cxy = sxx / total - mx * mx, syy / total - my * my, sxy / total - mx * my
            angle = math.degrees(0.5 * math.atan2(2 * cxy, cxx - cyy))
            # THE GLINT: the centroid of the brightest tenth of the lit cells
            hot.sort(reverse=True)
            top = hot[:max(1, len(hot) // 10)]
            tl = sum(h[0] for h in top)
            gx, gy = sum(h[0] * h[1] for h in top) / tl / cells, sum(h[0] * h[2] for h in top) / tl / cells
        out.append(Frame(w * total / (cells * cells), bright, w * core, wr / total if total else 0.0, w,
                         raw=total / (cells * cells),
                         cx=sx / total / cells if total else 0.5, cy=sy / total / cells if total else 0.5,
                         occ=frozenset(k for k, v in coarse.items() if v >= occ_min),
                         angle=angle, gx=gx, gy=gy))
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
    return "IMPACT" in archetypes_of(path)


def _iou(a, b):
    return len(a & b) / len(a | b) if (a or b) else 1.0


def flight_findings(frames):
    """PROJECTILE: the renderer flies the strip, so the art must hold one continuous pose — no jump, no reset."""
    found = []
    for k in range(1, len(frames)):
        a, b = frames[k - 1], frames[k]
        jump = math.hypot(b.cx - a.cx, b.cy - a.cy)
        if jump > FLIGHT_JUMP:
            found.append(f"FLIGHT: the object jumps {jump:.2f} of the frame between frames {k - 1} and {k}")
            break
        lo, hi = sorted((a.raw, b.raw))
        if lo > 0 and hi / lo > FLIGHT_SWING:
            found.append(f"FLIGHT: its light changes {hi / lo:.1f}x between frames {k - 1} and {k} (a reset or a second launch)")
            break
    return found


def settle_findings(frames):
    """TRAP/MARK: establish, then hold. A drop well below the peak and a climb back reads as a second placement."""
    raw = [fr.raw for fr in frames]
    peak = max(raw)
    if peak <= 0:
        return []
    est = next(i for i, v in enumerate(raw) if v >= SETTLE_BACK * peak)
    low = raw[est]
    for k in range(est + 1, len(raw)):
        low = min(low, raw[k])
        if low < SETTLE_LOW * peak and raw[k] >= SETTLE_BACK * peak:
            return [f"SETTLE: it fades to {low / peak:.2f} of its peak and comes back at frame {k} (a blink or a second placement)"]
    return []


def loop_findings(frames):
    """HELD (field, shield, and every DUAL strip): one silhouette that breathes and loops, 7 flowing into 0."""
    found = []
    n = len(frames)
    steps = [_iou(frames[k].occ, frames[k + 1].occ) for k in range(n - 1)]
    seam = _iou(frames[-1].occ, frames[0].occ)
    worst = min(steps) if steps else 1.0
    if worst < LOOP_RESET:
        k = steps.index(worst)
        found.append(f"LOOP: the silhouette resets between frames {k} and {k + 1} (overlap {worst:.2f})")
    med = sorted(steps)[len(steps) // 2] if steps else 1.0
    if seam < LOOP_SEAM * med:
        found.append(f"LOOP: the seam 7 -> 0 breaks (overlap {seam:.2f} against a typical step of {med:.2f})")
    raw = [fr.raw for fr in frames]
    if min(raw) > 0 and max(raw) / min(raw) > LOOP_SWING:
        found.append(f"LOOP: its light swings {max(raw) / min(raw):.1f}x across the loop (more than breathing)")
    return found


VISIBLE_FRAMES = 6    # PROJECTILE: frames 0-5; 6-7 sit in the renderer's tail fade (measured with a digit strip)


def internal_motion(frames):
    """How much a projectile's picture changes on its own, over the frames the player actually sees.

    Reported, never enforced (2026-09-23 projectile pilot): `change` is the mean silhouette change
    between consecutive frames (1 - overlap of the combat-scale occupancy), `turn` the range of its
    principal-axis angle in degrees, and `glint` how far its brightest tenth travels, as a share of the
    frame. A picture that only translates scores ~0 on all three.
    """
    vis = frames[:VISIBLE_FRAMES]
    steps = [1.0 - _iou(vis[k].occ, vis[k + 1].occ) for k in range(len(vis) - 1)]
    angles = [f.angle for f in vis]
    glint = max(math.hypot(a.gx - b.gx, a.gy - b.gy) for a in vis for b in vis)
    return {"change": sum(steps) / len(steps) if steps else 0.0,
            "turn": max(angles) - min(angles), "glint": glint}


_SPEC = None


def archetypes_of(path):
    """The archetype(s) a strip is drawn to, from spec.json effects.archetypes (the taxonomy of 2026-09-23).
    A DUAL strip is held AND fired, so it answers to both of its readings."""
    global _SPEC
    if _SPEC is None:
        import json
        here = os.path.dirname(os.path.abspath(__file__))
        with open(os.path.join(here, "asset-pipeline", "v2", "spec.json"), encoding="utf-8") as fh:
            _SPEC = json.load(fh)["effects"]["archetypes"]
    key = os.path.basename(path).replace("_strip8_512.png", "")
    found = [name for name, a in _SPEC.items() if isinstance(a, dict)
             and (key in a.get("keys", []) or any(key.endswith(s) for s in a.get("suffixes", [])))]
    if key in _SPEC.get("dual", []):
        found.append("HELD")
    return found or ["IMPACT"]


READINGS = {"IMPACT": one_peak_findings, "PROJECTILE": flight_findings, "TRAP/MARK": settle_findings,
            "FIELD/AURA": loop_findings, "SHIELD/BARRIER": loop_findings, "HELD": loop_findings, "OTHER": None}


def findings_for(path, frames):
    """Every archetype-appropriate diagnostic for this strip. Contextual evidence, never a pass/fail."""
    out = []
    for arch in archetypes_of(path):
        fn = READINGS.get(arch)
        if fn is not None:
            out.extend(m for m in fn(frames) if m not in out)
    return out


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
        live, peak = live_problems(frames), findings_for(p, frames)
        tag = ("LIVE! " if live else "      ") + ("WARN  " if peak else "      ") + f"{'+'.join(archetypes_of(p)):24}"
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
