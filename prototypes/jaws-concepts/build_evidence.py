#!/usr/bin/env python3
"""build_evidence.py -- the JAWS concept study's review media, from the rendered animatics (render_concepts.sh).

    python prototypes/jaws-concepts/build_evidence.py <out dir> [<blind dir>]

THROWAWAY (prototype). Reads build/shots/jaws/plates/ (the clean plates) and build/shots/jaws/concepts/{A,B,C}/ (the
concepts drawn over them) and writes, per concept: true speed muted, true speed with the temp cue, a 4x slow close
view, a frame sheet, the repeated-trigger loop, and the overlaps with SPRAY and HARD HANDS; plus muted 2x2 grids
(no JAWS / A / B / C) of the same moments. With a blind dir it also writes the neutral-named frame strips the blind
readers are shown (the reflected number, which names the skill, painted out; the skill bar cropped off).
"""
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PL = os.path.join(REPO, "build", "shots", "jaws", "plates")
CO = os.path.join(REPO, "build", "shots", "jaws", "concepts")
FA = os.path.join(REPO, "tools", "asset-pipeline", "film_audio.py")
MUSIC = "music_arena_shadow"
FRAME = 1000.0 / 60.0
ARENA = "380,410,1500,670"        # the whole stage as played, the skill bar kept, the legacy "SNARE" callout (y 348-381) off
GRID = "480,440,1280,460"         # champion to the front imps, for the 2x2 comparisons
CLOSE = "560,560,760,340"         # the Seeker's belt to the front imp, 1:1, for the slow views
BOX = (960, 640, 1300, 900)       # the frame sheet's 1:1 box round the front imp
NAMES = {"N": "NO JAWS (the fight as filmed, the concept layer off)", "A": "A  SHADOW JAWS",
         "B": "B  SHADOW TETHER COUNTER", "C": "C  TRAP SIGIL"}
PHASES = {
    "A": [(-45, "-"), (-40, "FORM"), (-17, "OPEN"), (-1, "SLAM"), (8, "REBOUND"), (25, "LOCK"), (100, "BREAK"), (185, "-")],
    "B": [(-81, "-"), (-80, "STIR"), (-50, "LASH"), (-1, "CATCH"), (8, "RECOIL"), (70, "REEL / FADE"), (165, "-")],
    "C": [(-81, "-"), (-80, "TELEGRAPH"), (-40, "CONVERGE"), (-1, "CONTACT"), (8, "HOLD"), (60, "FRACTURE"), (150, "-")],
}
py = sys.executable
try:
    FONT = ImageFont.truetype("arial.ttf", 18)
    SMALL = ImageFont.truetype("arial.ttf", 14)
except OSError:
    FONT = SMALL = ImageFont.load_default()


def ffmpeg():
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def src(concept, plate):
    return os.path.join(PL, plate) if concept == "N" else os.path.join(CO, concept, plate)


def read(log):
    shots, bites = {}, {}
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) > 4 and p[0] == "present":
            if p[3] == "shot":
                shots[int(p[4])] = (float(p[1]), float(p[2]))
            elif p[3] == "event" and p[4] == "EnemyStrike":
                kv = dict(x.split("=", 1) for x in p[5:] if "=" in x)
                bites.setdefault(int(kv["at"]), []).append((float(p[1]), float(p[2])))
    lo, hi = min(w for w, _ in shots.values()) - 200, max(w for w, _ in shots.values()) + 200
    return shots, {ms: next(o for o in occ if lo <= o[0] <= hi) for ms, occ in bites.items()
                   if any(lo <= o[0] <= hi for o in occ)}


def phase(concept, t):
    name = "-"
    for t0, n in PHASES.get(concept, []):
        if t >= t0:
            name = n
    return name


def run(*a):
    r = subprocess.run([py, *a], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(r.stdout, r.stderr)
        raise SystemExit(f"failed: {a}")
    return r.stdout.strip()


# ── film material: a (possibly windowed, stitched, labelled) frame set + its trace, then film_audio ───────────
class Take:
    """A frame set on disk (prefix_NN.png) and its trace; a temp copy when windowed, stitched or labelled."""

    def __init__(self, prefix, log=None, tmp=None):
        self.prefix, self.log, self.tmp = prefix, log or prefix + ".log", tmp

    def done(self):
        if self.tmp:
            shutil.rmtree(self.tmp, ignore_errors=True)


def derive(frames, sounds, label=None, crop=None, marks=None):
    """A new Take from (image path, film clock ms, playhead) frames and (clock, raw sound line fields) sounds; the
    trace's clock column is the film clock, so film_audio lines the sounds up with the frames exactly."""
    tmp = tempfile.mkdtemp()
    out = []
    for k, (path, clock, ph) in enumerate(frames):
        im = Image.open(path).convert("RGB")
        if label:
            x, y = [int(v) for v in crop.split(",")][:2] if crop else (0, 0)
            d = ImageDraw.Draw(im)
            text = label + ((" - " + marks(ph)) if marks else "")
            w = d.textlength(text, font=FONT)
            d.rectangle((x + 8, y + 8, x + 20 + w, y + 34), fill=(20, 17, 14))
            d.text((x + 14, y + 11), text, fill=(235, 200, 120), font=FONT)
        im.save(os.path.join(tmp, f"f_{k:02d}.png"), compress_level=1)
        out.append(f"present\t{clock:.1f}\t{ph:.1f}\tshot\t{k}\n")
    for clock, fields in sorted(sounds, key=lambda s: s[0]):
        out.append("present\t" + f"{clock:.1f}\t" + "\t".join(fields[2:]) + "\n")
    log = os.path.join(tmp, "f.log")
    open(log, "w", encoding="utf-8").writelines(out)
    return Take(os.path.join(tmp, "f"), log, tmp)


def sounds_of(log):
    s = []
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) > 4 and p[0] == "present" and p[3] == "sound":
            s.append((float(p[1]), p))
    return s


def take(concept, plate, lo=None, hi=None, label=None, crop=None, marks=None):
    """One plate's frames (optionally a playhead window), relabelled on the film clock (its own wall clock)."""
    pre = src(concept, plate)
    shots, _ = read(pre + ".log")
    frames = [(f"{pre}_{i:02d}.png", w, ph) for i, (w, ph) in sorted(shots.items())
              if (lo is None or ph >= lo) and (hi is None or ph <= hi)]
    return derive(frames, sounds_of(pre + ".log"), label, crop, marks)


def stitched(concept, plates, label=None, crop=None, marks=None):
    """Consecutive takes joined by PLAYHEAD (an overlap is dropped, a gap is a cut); each take's sounds ride its frames."""
    frames, sounds, last = [], [], -1e9
    for plate in plates:
        pre = src(concept, plate)
        shots, _ = read(pre + ".log")
        own = [(i, w, ph) for i, (w, ph) in sorted(shots.items()) if ph > last + 1]
        if not own:
            continue
        base = len(frames)
        # the film clock runs one frame per frame; a sound rides the frame it was asked before (its wall-clock lead kept)
        for i, w, ph in own:
            frames.append((f"{pre}_{i:02d}.png", len(frames) * FRAME, ph))
        prev_last, last = last, own[-1][2]
        for w, p in sounds_of(pre + ".log"):
            ph = float(p[2])
            if not ((prev_last < -1e8 or ph > prev_last) and ph <= last + FRAME):
                continue
            k = max((j for j, (_, fw, _) in enumerate(own) if fw <= w), default=0)
            sounds.append(((base + k) * FRAME + (w - own[k][1]), p))
    return derive(frames, sounds, label, crop, marks)


def film(t: Take, out, crop, *extra):
    print(run(FA, t.log, t.prefix, out, "--crop", crop, *extra))


# ── muted grids: no JAWS / A / B / C, the same frame of the same fight ──────────────────────────────────────────
def grid(takes, out, crop, fps=60, shrink=0.6):
    x, y, w, h = map(int, crop.split(","))
    tmp = tempfile.mkdtemp()
    shots = [read(t.log)[0] for t in takes]
    n = 0
    for k in sorted(shots[0]):
        cells = [Image.open(f"{t.prefix}_{k:02d}.png").convert("RGB").crop((x, y, x + w, y + h)) for t in takes]
        img = Image.new("RGB", (2 * w + 6, 2 * h + 6), (0, 0, 0))
        d = ImageDraw.Draw(img)
        for j, (cell, c) in enumerate(zip(cells, "NABC")):
            cx, cy = (j % 2) * (w + 6), (j // 2) * (h + 6)
            img.paste(cell, (cx, cy))
            label = NAMES[c] if c != "N" else "NO JAWS"
            tw = d.textlength(label, font=FONT)
            d.rectangle((cx + 6, cy + 6, cx + 16 + tw, cy + 30), fill=(20, 17, 14))
            d.text((cx + 11, cy + 9), label, fill=(235, 200, 120), font=FONT)
        W2, H2 = int(img.width * shrink) // 2 * 2, int(img.height * shrink) // 2 * 2
        img.resize((W2, H2), Image.LANCZOS).save(f"{tmp}/g_{n:04d}.png", compress_level=1)
        n += 1
    subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-framerate", str(fps), "-i", f"{tmp}/g_%04d.png",
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", out], check=True)
    shutil.rmtree(tmp, ignore_errors=True)
    print(f"{os.path.basename(out)}: {n} frames at {fps} fps")


# ── the frame sheet: every frame from -67 to +183, 1:1 and the middle at 2x, each named by its key pose ─────────
def frame_sheet(concept, out, plate="plate_normal", ms=7000, lo=-67, hi=183, per_row=8):
    pre = src(concept, plate)
    shots, bites = read(pre + ".log")
    t0 = bites[ms][1]
    frames = [(i, ph - t0) for i, (w, ph) in sorted(shots.items()) if lo - 1 <= ph - t0 <= hi + 1]
    bw, bh = BOX[2] - BOX[0], BOX[3] - BOX[1]
    rows = (len(frames) + per_row - 1) // per_row
    img = Image.new("RGB", (per_row * (bw + 8) + 8, rows * (2 * bh + 60) + 70), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for k, (i, t) in enumerate(frames):
        r, c = divmod(k, per_row)
        x, y = 8 + c * (bw + 8), 8 + r * (2 * bh + 60)
        im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(BOX)
        d.text((x, y), f"{t:+.0f} ms  {phase(concept, t)}", fill=(235, 200, 120), font=FONT)
        img.paste(im, (x, y + 24))
        mid = im.resize((bw * 2, bh * 2), Image.NEAREST).crop((bw // 2, bh // 2, bw // 2 + bw, bh // 2 + bh))
        img.paste(mid, (x, y + 28 + bh))
    d.text((8, img.height - 56), f"{NAMES[concept]}: every frame, 60 fps (16.7 ms apart). Upper: 1:1 as played. Lower: the "
                                 f"middle at 2x. t = 0 is the frame that first shows the bite.", fill=(235, 200, 120), font=FONT)
    d.text((8, img.height - 30), "The white body and the pale puff from +17 are the fight's own hit flash and hit puff "
                                 "(the same under every concept).", fill=(200, 180, 150), font=SMALL)
    img.save(out)
    print(os.path.basename(out))


# ── the blind strips: consecutive frames, no names; the crop keeps the words that name the skill out of view ─────
#   The reflected number reads "-7 JAWS CRITICAL" (y 479-515, and it only rises) and the skill bar names JAWS "ON BITE"
#   (y >= 900), so the strip is cut to y 522..900. Nothing is painted over; the tip of the Seeker's hood is cut at rest.
def blind_strip(concept, plate, ms, out, crop=(440, 522, 1340, 900), lo=-100, hi=200, cols=4, scale=0.5):
    pre = src(concept, plate)
    shots, bites = read(pre + ".log")
    t0 = bites[ms][1]
    frames = [i for i, (w, ph) in sorted(shots.items()) if lo - 1 <= ph - t0 <= hi + 1]
    x0, y0, x1, y1 = crop
    cw, ch = int((x1 - x0) * scale), int((y1 - y0) * scale)
    rows = (len(frames) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cw + 6) + 6, rows * (ch + 26) + 6), (16, 14, 12))
    d = ImageDraw.Draw(img)
    for k, i in enumerate(frames):
        im = Image.open(f"{pre}_{i:02d}.png").convert("RGB")
        im = im.crop(crop).resize((cw, ch), Image.LANCZOS)
        r, c = divmod(k, cols)
        x, y = 6 + c * (cw + 6), 6 + r * (ch + 26)
        d.text((x, y + 2), f"frame {k + 1}", fill=(230, 220, 200), font=SMALL)
        img.paste(im, (x, y + 20))
    img.save(out)
    return len(frames)


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    if "--blind-only" in sys.argv:
        blind_strips(blind)
        return
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png")):
            os.remove(os.path.join(out, f))
    # 01 TRUE SPEED, MUTED: the 2x2 grid, then each concept alone on the whole stage
    ts = [take(c, "plate_normal") for c in "NABC"]
    grid(ts, f"{out}/01_true_speed_grid_MUTED.mp4", GRID)
    for c, t in zip("NABC", ts):
        t.done()
    for c in "ABC":
        t = take(c, "plate_normal", label=NAMES[c], crop=ARENA)
        film(t, f"{out}/01{c}_true_speed_MUTED.mp4", ARENA, "--mute")
        # 02 TRUE SPEED with the TEMP cue (the dry-steel snap, its clack on the SLAM frame; the legacy JAWS cues out)
        film(t, f"{out}/02{c}_true_speed_temp_sound.mp4", ARENA)
        t.done()
    t = take("N", "plate_normal", label="NO JAWS (the fight as filmed, the concept layer off)", crop=ARENA)
    film(t, f"{out}/02N_no_jaws_reference_sound.mp4", ARENA)
    t.done()

    # 03 4x SLOW, the close view (belt to target), a window round the bite (7017 is t = 0)
    for c in "ABC":
        t = take(c, "plate_normal", lo=6850, hi=7300, label=NAMES[c], crop=CLOSE)
        film(t, f"{out}/03{c}_close_slow_4x.mp4", CLOSE, "--slow", "4")
        t.done()
    ts = [take(c, "plate_normal", lo=6850, hi=7300) for c in "NABC"]
    grid(ts, f"{out}/03_close_grid_slow_4x_MUTED.mp4", CLOSE, fps=15, shrink=1.0)
    for t in ts:
        t.done()

    # 04 FRAME SHEETS
    for c in "ABC":
        frame_sheet(c, f"{out}/04{c}_frame_sheet.png")

    # 05 REPEATED TRIGGER LOOP: three consecutive takes, JAWS at 1000 / 3000 / 5000 (one 150 ms cut at 4050 -> 4200)
    reps = ["plate_rep_a", "plate_rep_b", "plate_rep_c"]
    ts = [stitched(c, reps) for c in "NABC"]
    grid(ts, f"{out}/05_repeated_loop_grid_MUTED.mp4", GRID)
    for t in ts:
        t.done()
    for c in "ABC":
        t = stitched(c, reps, label=NAMES[c], crop=ARENA)
        film(t, f"{out}/05{c}_repeated_loop_temp_sound_music.mp4", ARENA, "--music", MUSIC)
        t.done()

    # 06 during SPRAY (JAWS at 1000, SPRAY releases at 1150, lands 1400), 07 during HARD HANDS (JAWS 13000, leap 13083)
    for n, plate, what in (("06", "plate_spray", "during_spray"), ("07", "plate_hh", "during_hard_hands")):
        ts = [take(c, plate) for c in "NABC"]
        grid(ts, f"{out}/{n}_{what}_grid_MUTED.mp4", GRID)
        for t in ts:
            t.done()
        for c in "ABC":
            t = take(c, plate, label=NAMES[c], crop=ARENA)
            film(t, f"{out}/{n}{c}_{what}_temp_sound.mp4", ARENA)
            t.done()

    if blind:
        blind_strips(blind)
    print("done ->", out)


BLIND = {"k2": ("B", "plate_normal", 7000), "k5": ("N", "plate_normal", 7000), "k7": ("C", "plate_normal", 7000),
         "k9": ("A", "plate_normal", 7000), "m3": ("C", "plate_hh", 13000), "m4": ("A", "plate_hh", 13000),
         "m8": ("B", "plate_hh", 13000)}


def blind_strips(blind):
    os.makedirs(blind, exist_ok=True)
    for code, (c, plate, ms) in BLIND.items():       # neutral names: the reader sees only a clip code
        if plate == "plate_normal":
            n = blind_strip(c, plate, ms, f"{blind}/clip_{code}.png")
        else:   # the HARD HANDS leap lands at +200: the window runs past it; the stage is wider
            n = blind_strip(c, plate, ms, f"{blind}/clip_{code}.png", crop=(440, 522, 1760, 900), hi=250, scale=0.42)
        print(f"blind clip_{code}: {n} frames")


if __name__ == "__main__":
    main()
