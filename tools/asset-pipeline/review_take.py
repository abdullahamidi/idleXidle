#!/usr/bin/env python3
"""review_take -- turn one traced capture take into the pieces a reviewer who cannot watch video can read.

    py tools/asset-pipeline/review_take.py --log take.log --name after_bleed --out <dir> --at 5500
        (--frames build/shots/sweep/<tag>/<name> | --mp4 film.mp4)
        [--step 1] [--before 3] [--stills 5500,5530] [--window -400,900] [--title "..."] [--crop x,y,w,h]

Writes into <dir>:
  <name>_strip.jpg   8 frames at TRUE-SPEED spacing (every --step-th saved frame) around the decisive playhead --at,
                     arena crops at half size, each cell labelled with its shot number, playhead ms and its offset
                     from --at; the cell holding --at is outlined.
  <name>_still_<ms>.jpg   a FULL frame (1920x1080) at each --stills playhead (the nearest saved frame). From an --mp4
                     source (a BEFORE film whose frames were deleted) the still is the film's arena crop.
  <name>_trace.txt   the trace rows of the window (--window ms around --at): fight events (not Beat), sounds, callouts,
                     numbers, flashes, vfx spawns, the reaction / field / mark / projectile event rows and the sweep's
                     own rows (quiet-hit, skill-skip, echo), each with the nearest saved frame; then, per *-draw layer,
                     the alloc= summary of the WHOLE take (lines / non-zero / max bytes).

The frame <-> playhead map is the trace's own `shot N` lines (RH_PRESENT_TRACE=1). An mp4 frame is read at the same
trace-clock offset film_audio.py rendered it at, so a BEFORE film's frames line up with its log.
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw

KEEP = {"event", "sound", "callout", "number", "flash", "vfx-spawn", "contact", "contact-tick", "release", "handoff",
        "clip-start", "reaction-spawn", "reaction-cue", "reaction-number", "reaction-end", "reaction-clamp",
        "field-wave", "field-cue", "mark-wave", "mark-cue", "curse-seat", "proj-contact", "quiet-hit", "skill-skip",
        "echo", "enemy-commit", "heal-number"}


def ffmpeg() -> str:
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        return "ffmpeg"


def parse(log: str):
    rows, shots = [], {}
    with open(log, encoding="utf-8", errors="replace") as f:
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) < 4 or p[0] != "present":
                continue
            try:
                clock, ph = float(p[1]), float(p[2])
            except ValueError:
                continue
            if p[3] == "shot":
                shots[int(p[4])] = (clock, ph)
            rows.append((clock, ph, p[3], p[4:]))
    return rows, shots


def nearest_shot(shots: dict, ph: float) -> int:
    return min(shots, key=lambda i: (abs(shots[i][1] - ph), i))


def frame_png(prefix: str, i: int) -> str:
    for name in (f"{prefix}_{i:02d}.png", f"{prefix}_{i}.png", f"{prefix}_{i:03d}.png"):
        if os.path.exists(name):
            return name
    raise SystemExit(f"review_take: no frame {i} for {prefix}")


def frame_mp4(mp4: str, t: float, tmp: str) -> Image.Image:
    out = os.path.join(tmp, f"f{int(t * 1000)}.png")
    subprocess.run([ffmpeg(), "-v", "error", "-y", "-ss", f"{t:.4f}", "-i", mp4, "-frames:v", "1", out], check=True)
    return Image.open(out).convert("RGB")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(prog="review_take")
    ap.add_argument("--log", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--at", type=float, required=True, help="the decisive playhead (ms)")
    ap.add_argument("--frames")
    ap.add_argument("--mp4")
    ap.add_argument("--step", type=int, default=1, help="saved frames between strip cells")
    ap.add_argument("--before", type=int, default=3, help="cells before the decisive one")
    ap.add_argument("--stills", default="")
    ap.add_argument("--window", default="-400,900")
    ap.add_argument("--crop", default="480,20,1090,1040")
    ap.add_argument("--title", default="")
    a = ap.parse_args(argv)
    if bool(a.frames) == bool(a.mp4):
        sys.exit("review_take: give exactly one of --frames / --mp4")
    os.makedirs(a.out, exist_ok=True)
    rows, shots = parse(a.log)
    if not shots:
        sys.exit("review_take: no `shot` lines (film with RH_PRESENT_TRACE=1)")
    first = min(shots)
    cx, cy, cw, ch = (int(v) for v in a.crop.split(","))
    tmp = tempfile.mkdtemp(prefix="review_take_")

    def image(i: int, full: bool) -> Image.Image:
        if a.frames:
            im = Image.open(frame_png(a.frames, i)).convert("RGB")
            return im if full else im.crop((cx, cy, cx + cw, cy + ch))
        return frame_mp4(a.mp4, (shots[i][0] - shots[first][0]) / 1000.0, tmp)  # the film is the arena crop already

    # (a) the contact strip: 8 cells, true-speed spacing, the decisive frame outlined
    centre = nearest_shot(shots, a.at)
    idx = [centre + (k - a.before) * a.step for k in range(8)]
    lo, hi = min(shots), max(shots)
    shift = max(0, lo - idx[0]) - max(0, idx[-1] - hi)
    idx = [i + shift for i in idx]
    scale = 0.5
    w, h = int(cw * scale), int(ch * scale)
    head = 26
    sheet = Image.new("RGB", (4 * w, 2 * h + head), (18, 14, 22))
    d = ImageDraw.Draw(sheet)
    span = shots[idx[1]][1] - shots[idx[0]][1]
    d.text((6, 6), f"{a.name}  {a.title}  |  decisive playhead {a.at:.0f} ms  |  cell spacing ~{span:.0f} ms of game "
                   f"(true speed)", fill=(240, 220, 160))
    for k, i in enumerate(idx):
        im = image(i, False).resize((w, h), Image.BILINEAR)
        ox, oy = (k % 4) * w, head + (k // 4) * h
        sheet.paste(im, (ox, oy))
        ph = shots[i][1]
        lab = f"shot {i}  {ph:.0f} ms  ({ph - a.at:+.0f})"
        d.rectangle([ox, oy, ox + 7 * len(lab) + 8, oy + 18], fill=(0, 0, 0))
        d.text((ox + 4, oy + 4), lab, fill=(240, 220, 160))
        if i == centre:
            d.rectangle([ox + 1, oy + 1, ox + w - 2, oy + h - 2], outline=(255, 210, 60), width=3)
        else:
            d.rectangle([ox, oy, ox + w - 1, oy + h - 1], outline=(60, 50, 70))
    strip = os.path.join(a.out, f"{a.name}_strip.jpg")
    sheet.save(strip, quality=86)

    # (b) the stills
    made = [strip]
    for s in [x for x in a.stills.split(",") if x.strip()]:
        i = nearest_shot(shots, float(s))
        p = os.path.join(a.out, f"{a.name}_still_{shots[i][1]:.0f}.jpg")
        image(i, True).save(p, quality=90)
        made.append(p)

    # (c) the trace excerpt
    w0, w1 = (float(v) for v in a.window.split(","))
    lines = [f"# {a.name}: trace rows {a.at + w0:.0f}..{a.at + w1:.0f} ms of playhead (decisive {a.at:.0f}); "
             f"columns: playhead, nearest saved frame, kind, fields", f"# source: {os.path.basename(a.log)}"]
    for clock, ph, kind, f in rows:
        if kind not in KEEP or not (a.at + w0 <= ph <= a.at + w1):
            continue
        if kind == "event" and f and f[0] == "Beat":
            continue
        lines.append(f"{ph:7.0f}  shot {nearest_shot(shots, ph):3d}  {kind:15s} " + "  ".join(f))
    lines.append("")
    lines.append("# per-layer alloc= over the WHOLE take (lines / non-zero / max bytes)")
    layers: dict[str, list[int]] = {}
    for _, _, kind, f in rows:
        if kind.endswith("-draw"):
            al = [int(x.split("=", 1)[1]) for x in f if x.startswith("alloc=")]
            if al:
                layers.setdefault(kind, []).append(al[0])
    if not layers:
        lines.append("(no layer reports alloc=)")
    for k, v in sorted(layers.items()):
        lines.append(f"{k:15s} {len(v)} / {sum(1 for x in v if x)} / {max(v)}")
    txt = os.path.join(a.out, f"{a.name}_trace.txt")
    with open(txt, "w", encoding="utf-8", newline="\n") as fo:
        fo.write("\n".join(lines) + "\n")
    made.append(txt)
    for p in os.listdir(tmp):
        os.remove(os.path.join(tmp, p))
    os.rmdir(tmp)
    print("\n".join(made))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
