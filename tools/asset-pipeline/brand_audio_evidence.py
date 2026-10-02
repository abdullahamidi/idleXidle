#!/usr/bin/env python3
"""brand_audio_evidence.py -- BRAND's final audio review films (design/audio/seeker-brand-audio-brief.md), SMALL.

    # one take at a time: film it (from a Bash shell: launched from Python the game's trace log comes out EMPTY),
    # render its segment (which deletes the take's frames), then the next
    for n in 1 2 3 4 5 6 7; do
      take=$(PYTHONUTF8=1 python tools/asset-pipeline/brand_audio_evidence.py <tag> <out dir> --take-of $n)
      bash tools/asset-pipeline/films_brand.sh <tag> $take
      PYTHONUTF8=1 python tools/asset-pipeline/brand_audio_evidence.py <tag> <out dir> --only $n
    done
    PYTHONUTF8=1 python tools/asset-pipeline/brand_audio_evidence.py <tag> <out dir> --concat-only   # + verify

Seven true-speed segments from the seeded fight (films_brand.sh's trimmed `ba_*` takes), each rendered FOUR times from
the same frames and the same crop (muted; candidate A, B, C via film_audio --cue for all seven BRAND keys from
tools/asset-pipeline/audio_history/brand_curse/<X>/), then concatenated per version:

    01_candidate_A.mp4  02_candidate_B.mp4  03_candidate_C.mp4  04_visual_reference_muted.mp4 (no audio track)

DISK (the owner's rule: the frames filled C: twice): ONE take at a time. For each segment the take is filmed (films_brand.sh, from Bash), its frames
are cropped and labelled once into a scratch folder, the take's own frames are deleted at once (3-digit names too), the
four renders are made, then the scratch frames are deleted. Only the segment MP4s (a few MB) and the trace logs stay.
The approved JAWS / PRESS cues and every reference sound come from assets/ untouched; only the BRAND keys are swapped.
"""
import argparse
import glob
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FA = os.path.join(REPO, "tools", "asset-pipeline", "film_audio.py")
HIST = os.path.join(REPO, "tools", "asset-pipeline", "audio_history", "brand_curse")
WORK = os.path.join(REPO, "build", "tmp", "brandaudio", "films")
KEYS = ["apply", "deepen", "deepen_deep", "infect", "leave", "awaken", "ash"]
CROP = (380, 410, 1500, 670)   # foundation_evidence.ARENA: the whole row and the hunter, the same for every segment
VERSIONS = ["A", "B", "C", "muted"]
FINAL = {"A": "01_candidate_A.mp4", "B": "02_candidate_B.mp4", "C": "03_candidate_C.mp4",
         "muted": "04_visual_reference_muted.mp4"}

# n, take (films_brand.sh), label, playhead window [lo, hi] ms. Each window opens after the previous sound's tail and
# closes >= 400 ms after its last BRAND cue's start + length (or just before the next BRAND event, named below).
SEGMENTS = [
    (1, "ba_apply", "1  BRAND apply (first tick 2.0 s)", 1750, 3050),
    (2, "ba_deepen12", "2  ETCH deepen 1 -> 2 (tick 4.0 s, shown 4.38 s)", 3800, 5150),       # leave at 5200
    (3, "ba_deepen23", "3  ETCH deepen 2 -> 3 (shown 6.38 s), then the host falls", 6100, 7480),  # awaken at 7533
    (4, "ba_sprawl", "4  SPRAWL: source apply, then the row infects", 1750, 3050),
    (5, "ba_transfer", "5  transfer: leave 6.7 s, gap, awaken 7.52 s", 6450, 8185),              # next leave at 8200
    (6, "ba_death", "6  cursed death, no transfer (wave-ending kill 17.2 s)", 16900, 18300),
    (7, "ba_mixed", "7  mixed: SPRAY, PRESS, BRAND leave + awaken", 4850, 8170),                 # HARD HANDS at 8200
]
py = sys.executable
try:
    FONT = ImageFont.truetype("arial.ttf", 22)
except OSError:
    FONT = ImageFont.load_default()


def ffmpeg():
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def run(cmd, **kw):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", **kw)
    if r.returncode != 0:
        print(r.stdout[-3000:], r.stderr[-3000:])
        raise SystemExit(f"failed: {cmd[:4]}")
    return r.stdout.strip()


def delete_frames(prefix):
    """Every PNG of a take (2- and 3-digit frame names, the filmstrip sheet): find -delete, never rm -rf."""
    n = 0
    for p in glob.glob(prefix + "_*.png"):
        os.remove(p)
        n += 1
    return n


def take_rows(log):
    shots, sounds = {}, []
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) > 4 and p[0] == "present":
            if p[3] == "shot":
                shots[int(p[4])] = (float(p[1]), float(p[2]))
            elif p[3] == "sound":
                sounds.append(line if line.endswith("\n") else line + "\n")
    return shots, sounds


def stage(prefix, lo, hi, label, scratch):
    """Crop + label the window's frames ONCE into `scratch` (f_NN.png + f.log carrying every sound row)."""
    os.makedirs(scratch, exist_ok=True)
    delete_frames(os.path.join(scratch, "f"))
    shots, sounds = take_rows(prefix + ".log")
    x, y, w, h = CROP
    out, k = [], 0
    for i, (clock, ph) in sorted(shots.items()):
        if ph < lo or ph > hi:
            continue
        im = Image.open(f"{prefix}_{i:02d}.png").convert("RGB").crop((x, y, x + w, y + h))
        d = ImageDraw.Draw(im)
        tw = d.textlength(label, font=FONT)
        d.rectangle((8, 8, 24 + tw, 40), fill=(20, 17, 14))
        d.text((16, 11), label, fill=(235, 200, 120), font=FONT)
        im.save(os.path.join(scratch, f"f_{k:02d}.png"), compress_level=3)
        out.append(f"present\t{clock:.1f}\t{ph:.1f}\tshot\t{k}\n")
        k += 1
    if k < 10:
        raise SystemExit(f"{prefix}: only {k} frames in [{lo}, {hi}] - the take missed its window")
    out += sounds
    open(os.path.join(scratch, "f.log"), "w", encoding="utf-8").writelines(out)
    return k


def cue_args(letter):
    a = []
    for key in KEYS:
        path = os.path.join(HIST, letter, f"sfx_seeker_brand_{key}.wav")
        if not os.path.isfile(path):
            raise SystemExit(f"missing candidate file {path}")
        a += ["--cue", f"sfx_seeker_brand_{key}={path}"]
    return a


def segment(tag, n, take, label, lo, hi):
    pre = os.path.join(REPO, "build", "shots", "brand", tag, take)
    if not os.path.isfile(pre + ".log") or not glob.glob(pre + "_*.png"):
        raise SystemExit(f"film the take first: bash tools/asset-pipeline/films_brand.sh {tag} {take}")
    scratch = os.path.join(WORK, "scratch")
    k = stage(pre, lo, hi, label, scratch)
    print(f"segment {n} ({take}): {k} frames staged; deleted {delete_frames(pre)} take frames")
    seg = os.path.join(WORK, "seg")
    os.makedirs(seg, exist_ok=True)
    flog, fpre = os.path.join(scratch, "f.log"), os.path.join(scratch, "f")
    crop = f"0,0,{CROP[2]},{CROP[3]}"
    for v in VERSIONS:
        extra = ["--mute"] if v == "muted" else cue_args(v)
        print(run([py, FA, flog, fpre, os.path.join(seg, f"s{n}_{v}.mp4"), "--crop", crop, *extra]))
    delete_frames(fpre)


def concat(out_dir):
    seg = os.path.join(WORK, "seg")
    os.makedirs(out_dir, exist_ok=True)
    for v in VERSIONS:
        ins, chain = [], ""
        for j, s in enumerate(SEGMENTS):
            ins += ["-i", os.path.join(seg, f"s{s[0]}_{v}.mp4")]
            chain += f"[{j}:v]" + ("" if v == "muted" else f"[{j}:a]")
        n = len(SEGMENTS)
        if v == "muted":
            fc, maps, acodec = chain + f"concat=n={n}:v=1:a=0[v]", ["-map", "[v]"], ["-an"]
        else:
            fc = chain + f"concat=n={n}:v=1:a=1[v][a]"
            maps, acodec = ["-map", "[v]", "-map", "[a]"], ["-c:a", "aac", "-b:a", "160k"]
        out = os.path.join(out_dir, FINAL[v])
        run([ffmpeg(), "-y", "-loglevel", "error", *ins, "-filter_complex", fc, *maps, "-c:v", "libx264",
             "-pix_fmt", "yuv420p", "-crf", "24", "-preset", "slow", *acodec, "-movflags", "+faststart", out])
        print(f"{FINAL[v]}: {os.path.getsize(out) / 1e6:.2f} MB")


def verify(out_dir):
    """Each film: duration, audio stream present (A/B/C) or absent (muted), and the audio's loudness per segment."""
    ok = True
    for v in VERSIONS:
        path = os.path.join(out_dir, FINAL[v])
        info = subprocess.run([ffmpeg(), "-i", path], capture_output=True, text=True, encoding="utf-8",
                              errors="replace").stderr
        has_audio = "Audio:" in info
        dur = [l.split("Duration:")[1].split(",")[0].strip() for l in info.splitlines() if "Duration:" in l]
        line = f"{FINAL[v]}: {os.path.getsize(path) / 1e6:.2f} MB, {dur[0] if dur else '?'}, audio={has_audio}"
        if v == "muted":
            ok &= not has_audio
        else:
            raw = subprocess.run([ffmpeg(), "-v", "error", "-i", path, "-f", "s16le", "-ac", "1", "-ar", "44100", "-"],
                                 capture_output=True).stdout
            a = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768
            peak = float(np.abs(a).max()) if len(a) else 0.0
            rms = float(np.sqrt(np.mean(a ** 2))) if len(a) else 0.0
            line += f", peak {20 * np.log10(max(peak, 1e-9)):.1f} dBFS, rms {20 * np.log10(max(rms, 1e-9)):.1f} dBFS"
            ok &= has_audio and peak > 0.01
        print(line)
    if not ok:
        raise SystemExit("verify FAILED")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("tag")
    ap.add_argument("out")
    ap.add_argument("--only", type=int, action="append", default=[])
    ap.add_argument("--take-of", type=int, default=0, help="print segment N's films_brand.sh take name and exit")
    ap.add_argument("--concat-only", action="store_true")
    ap.add_argument("--verify-only", action="store_true")
    a = ap.parse_args()
    if a.take_of:
        print(next(s[1] for s in SEGMENTS if s[0] == a.take_of))
        return
    out = os.path.join(REPO, a.out) if not os.path.isabs(a.out) else a.out
    if not (a.concat_only or a.verify_only):
        for s in SEGMENTS:
            if not a.only or s[0] in a.only:
                segment(a.tag, *s)
    if a.only and not a.concat_only:
        return   # a partial run: the other segments' MP4s may not exist yet
    if not a.verify_only:
        concat(out)
    verify(out)


if __name__ == "__main__":
    main()
