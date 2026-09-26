#!/usr/bin/env python3
"""build_evidence.py -- ENEMY ATTACK READABILITY + HIT FEEDBACK STUDY (2026-09-26), THROWAWAY.

    PYTHONUTF8=1 python prototypes/bite-readability/build_evidence.py <out dir> [<blind dir>]

Reads the takes film.sh filmed into build/shots/bite/ (and the JAWS concept animatics rendered over the best plate
by render_concepts_on_best.sh) and writes the review media: true-speed films (muted and with the fight's own sound),
2x2 grids of the same fight frames under different treatments, every-frame sheets at play size, the flash close-ups,
and the neutral-named flipbooks the blind readers get.
"""
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
B = os.path.join(REPO, "build", "shots", "bite")
FA = os.path.join(REPO, "tools", "asset-pipeline", "film_audio.py")
FRAME = 1000.0 / 60.0
ARENA = "380,410,1500,670"          # the stage as played, the legacy "SNARE" callout (y < 410) off
GRID = "480,440,1280,460"           # champion to the front whelps
CLOSE = (960, 620, 1340, 900)       # the front whelp, 1:1 (it lunges ~38 px left of its resting place)
CONTACT_MS = 7000                   # the pack's bite in every normal-tempo take; the first frame that shows it is 7017
SWING_MS = 6700                     # the Seeker's swing on the front whelp
py = sys.executable
try:
    FONT = ImageFont.truetype("arial.ttf", 18)
    SMALL = ImageFont.truetype("arial.ttf", 14)
except OSError:
    FONT = SMALL = ImageFont.load_default()


def ffmpeg():
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def read(log):
    shots, events = {}, []
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) > 4 and p[0] == "present":
            if p[3] == "shot":
                shots[int(p[4])] = (float(p[1]), float(p[2]))
            elif p[3] == "event":
                events.append((float(p[2]), p[4], p[5:]))
    return shots, events


def sounds_of(log):
    s = []
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) > 4 and p[0] == "present" and p[3] == "sound":
            s.append((float(p[1]), p))
    return s


def run(*a):
    r = subprocess.run([py, *a], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(r.stdout, r.stderr)
        raise SystemExit(f"failed: {a}")
    return r.stdout.strip()


class Take:
    def __init__(self, prefix, log=None, tmp=None):
        self.prefix, self.log, self.tmp = prefix, log or prefix + ".log", tmp

    def done(self):
        if self.tmp:
            shutil.rmtree(self.tmp, ignore_errors=True)


def take(name, lo=None, hi=None, label=None, crop=None, src_dir=B):
    """A take's frames (optionally a playhead window), labelled, on its own wall clock; its sounds ride along."""
    pre = os.path.join(src_dir, name)
    shots, _ = read(pre + ".log")
    tmp = tempfile.mkdtemp()
    out, k = [], 0
    for i, (w, ph) in sorted(shots.items()):
        if (lo is not None and ph < lo) or (hi is not None and ph > hi):
            continue
        im = Image.open(f"{pre}_{i:02d}.png").convert("RGB")
        if label:
            x, y = [int(v) for v in crop.split(",")][:2] if crop else (0, 0)
            d = ImageDraw.Draw(im)
            tw = d.textlength(label, font=FONT)
            d.rectangle((x + 8, y + 8, x + 20 + tw, y + 34), fill=(20, 17, 14))
            d.text((x + 14, y + 11), label, fill=(235, 200, 120), font=FONT)
        im.save(os.path.join(tmp, f"f_{k:02d}.png"), compress_level=1)
        out.append(f"present\t{w:.1f}\t{ph:.1f}\tshot\t{k}\n")
        k += 1
    for w, p in sounds_of(pre + ".log"):
        out.append("present\t" + f"{w:.1f}\t" + "\t".join(p[2:]) + "\n")
    log = os.path.join(tmp, "f.log")
    open(log, "w", encoding="utf-8").writelines(out)
    return Take(os.path.join(tmp, "f"), log, tmp)


def film(t: Take, out, crop, *extra):
    print(run(FA, t.log, t.prefix, out, "--crop", crop, *extra))


def grid(names, labels, out, crop, fps=60, shrink=0.6, lo=None, hi=None, src_dir=B, tol=20):
    """2x2 (or 1xN) of takes, frames PAIRED BY PLAYHEAD to the first take's, muted."""
    x, y, w, h = map(int, crop.split(","))
    tmp = tempfile.mkdtemp()
    shots = [read(os.path.join(src_dir, n) + ".log")[0] for n in names]
    cols = 2 if len(names) > 2 else len(names)
    rows = (len(names) + cols - 1) // cols
    n = 0
    for k in sorted(shots[0]):
        ph = shots[0][k][1]
        if (lo is not None and ph < lo) or (hi is not None and ph > hi):
            continue
        img = Image.new("RGB", (cols * w + (cols - 1) * 6, rows * h + (rows - 1) * 6), (0, 0, 0))
        d = ImageDraw.Draw(img)
        ok = True
        for j, (name, label, sh) in enumerate(zip(names, labels, shots)):
            i = min(sh, key=lambda q: abs(sh[q][1] - ph))
            if abs(sh[i][1] - ph) > tol:
                ok = False
                break
            cell = Image.open(f"{os.path.join(src_dir, name)}_{i:02d}.png").convert("RGB").crop((x, y, x + w, y + h))
            cx, cy = (j % cols) * (w + 6), (j // cols) * (h + 6)
            img.paste(cell, (cx, cy))
            tw = d.textlength(label, font=FONT)
            d.rectangle((cx + 6, cy + 6, cx + 16 + tw, cy + 30), fill=(20, 17, 14))
            d.text((cx + 11, cy + 9), label, fill=(235, 200, 120), font=FONT)
        if not ok:
            continue
        W2, H2 = int(img.width * shrink) // 2 * 2, int(img.height * shrink) // 2 * 2
        img.resize((W2, H2), Image.LANCZOS).save(f"{tmp}/g_{n:04d}.png", compress_level=1)
        n += 1
    subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-framerate", str(fps), "-i", f"{tmp}/g_%04d.png",
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", out], check=True)
    shutil.rmtree(tmp, ignore_errors=True)
    print(f"{os.path.basename(out)}: {n} frames at {fps} fps")


def sheet(names, labels, out, t0=CONTACT_MS + 17, lo=-720, hi=300, step=1, box=CLOSE, scale=1.0, per_row=None, note=""):
    """Rows of takes, every frame (or every step-th) from lo to hi ms round t0, at play size, each frame timed."""
    bw, bh = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    rows = []
    for name in names:
        shots, _ = read(os.path.join(B, name) + ".log")
        fr = [(i, ph - t0) for i, (w, ph) in sorted(shots.items()) if lo - 1 <= ph - t0 <= hi + 1][::step]
        rows.append((name, fr))
    n = max(len(fr) for _, fr in rows)
    per_row = per_row or n
    lines = (n + per_row - 1) // per_row
    img = Image.new("RGB", (per_row * (bw + 6) + 6, len(rows) * lines * (bh + 26) + 50), (20, 17, 14))
    d = ImageDraw.Draw(img)
    yy = 6
    for (name, fr), label in zip(rows, labels):
        for k, (i, t) in enumerate(fr):
            r, c = divmod(k, per_row)
            x, y = 6 + c * (bw + 6), yy + r * (bh + 26)
            im = Image.open(f"{os.path.join(B, name)}_{i:02d}.png").convert("RGB").crop(box)
            if scale != 1.0:
                im = im.resize((bw, bh), Image.LANCZOS)
            d.text((x, y), f"{t:+.0f}" + (f"  {label}" if k == 0 else ""), fill=(235, 200, 120), font=SMALL)
            img.paste(im, (x, y + 20))
        yy += lines * (bh + 26)
    d.text((6, img.height - 40), note, fill=(200, 180, 150), font=SMALL)
    img.save(out)
    print(os.path.basename(out))


def flipbook(name, out, t0, lo=-800, hi=400, crop=(440, 522, 1340, 900), cols=5, scale=0.5, src_dir=B):
    """The blind readers' clip: EVERY frame of the window in reading order, small, no names; the words that name a
    skill are out of the crop (y 522..900)."""
    pre = os.path.join(src_dir, name)
    shots, _ = read(pre + ".log")
    fr = [i for i, (w, ph) in sorted(shots.items()) if lo - 1 <= ph - t0 <= hi + 1]
    x0, y0, x1, y1 = crop
    cw, ch = int((x1 - x0) * scale), int((y1 - y0) * scale)
    rows = (len(fr) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cw + 4) + 4, rows * (ch + 18) + 4), (16, 14, 12))
    d = ImageDraw.Draw(img)
    for k, i in enumerate(fr):
        r, c = divmod(k, cols)
        x, y = 4 + c * (cw + 4), 4 + r * (ch + 18)
        d.text((x, y), str(k + 1), fill=(200, 190, 170), font=SMALL)
        img.paste(Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(crop).resize((cw, ch), Image.LANCZOS), (x, y + 16))
    img.save(out)
    return len(fr)


def front_whelp_metrics(name, out_md, t0=CONTACT_MS + 17):
    """Where the front whelp's body IS, frame by frame: the leftmost column and the top row of its dark mass inside
    the close box, relative to the contact frame. From the BARE silhouette take: no tint, no effects."""
    pre = os.path.join(B, name)
    shots, _ = read(pre + ".log")
    x0, y0, x1, y1 = CLOSE
    lines = ["| t (ms) | left edge px | top px | note |", "|---|---|---|---|"]
    prev = None
    for i, (w, ph) in sorted(shots.items()):
        t = ph - t0
        if t < -760 or t > 320:
            continue
        a = np.asarray(Image.open(f"{pre}_{i:02d}.png").convert("RGB")).astype(int)[y0:y1, x0:x1]
        # the silhouette colour (150,146,160) +-8, and only the LEFT-most 230 px of the box (the front creature)
        m = (abs(a[:, :, 0] - 150) < 9) & (abs(a[:, :, 1] - 146) < 9) & (abs(a[:, :, 2] - 160) < 9)
        m[:, 230:] = False
        ys, xs = np.nonzero(m)
        if len(xs) < 200:
            continue
        left, top = int(xs.min()), int(ys.min())
        note = ""
        if prev is not None:
            dl, dt = left - prev[0], top - prev[1]
            if abs(dl) >= 6 or abs(dt) >= 6:
                note = f"moved {dl:+d} x, {dt:+d} y"
        if abs(t) < 9:
            note = (note + " " if note else "") + "CONTACT"
        lines.append(f"| {t:+.0f} | {left} | {top} | {note} |")
        prev = (left, top)
    open(out_md, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(os.path.basename(out_md))


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png")) and not f.startswith("00_"):
            os.remove(os.path.join(out, f))
    CUR, NT, BARE, SIL = "CURRENT (tint + effects)", "no ember tint", "BARE: no tint, no effects", "SILHOUETTE only"
    A, RB, RF, RO = "A: key poses, feet fixed (bare)", "B: key poses + lunge (bare)", "B-front: front leads (bare)", "lunge only, current strip (bare)"

    # 01 the bite as it is: true speed, the four views (sound on the current one: the fight's own bite thud)
    grid(["att_current", "att_notint", "att_bare", "att_sil"], [CUR, NT, BARE, SIL], f"{out}/01_bite_current_views_grid_MUTED.mp4", GRID)
    t = take("att_current", label=CUR, crop=ARENA); film(t, f"{out}/01a_bite_current_true_speed_sound.mp4", ARENA); t.done()
    t = take("att_bare", label=BARE, crop=ARENA); film(t, f"{out}/01b_bite_bare_true_speed_MUTED.mp4", ARENA, "--mute"); t.done()
    t = take("att_sil", label=SIL, crop=ARENA); film(t, f"{out}/01c_bite_silhouette_true_speed_MUTED.mp4", ARENA, "--mute"); t.done()
    sheet(["att_bare"], ["current strip, bare"], f"{out}/01d_bite_current_every_frame_1to1.png", per_row=16,
          note="Every frame at play size, -720 .. +300 ms round the first frame that shows the bite (t 0). No tint, no effects.")
    sheet(["att_sil"], ["silhouette"], f"{out}/01e_bite_current_silhouette_every_frame.png", per_row=16, scale=0.6)
    front_whelp_metrics("att_sil", f"{out}/01f_front_whelp_body_metrics.md")

    # 02 / 03 the prototypes: A art-only, B art + lunge, bare and full; the pack question (front-led)
    grid(["att_bare", "artA_bare", "rootB_bare", "rootF_bare"], [BARE, A, RB, RF], f"{out}/02_bite_prototypes_grid_bare_MUTED.mp4", GRID)
    grid(["att_bare", "rootonly_bare", "artA_bare", "rootB_bare"], [BARE, RO, A, RB], f"{out}/02b_bite_art_vs_lunge_grid_bare_MUTED.mp4", GRID)
    grid(["att_bare", "rootB_bare", "rootB2_bare", "rootF_bare"], [BARE, RB, "B x2: twice the travel (diagnostic)", RF],
         f"{out}/02f_bite_lunge_amount_grid_bare_MUTED.mp4", GRID)
    t = take("rootB2_full", label="B x2: key poses + twice the lunge, tint + effects (diagnostic)", crop=ARENA)
    film(t, f"{out}/03c_bite_B_x2_full_true_speed_sound.mp4", ARENA); t.done()
    t = take("artA_bare", label=A, crop=ARENA); film(t, f"{out}/02c_bite_A_art_only_bare_MUTED.mp4", ARENA, "--mute"); t.done()
    t = take("rootB_bare", label=RB, crop=ARENA); film(t, f"{out}/02d_bite_B_art_plus_lunge_bare_MUTED.mp4", ARENA, "--mute"); t.done()
    t = take("rootF_bare", label=RF, crop=ARENA); film(t, f"{out}/02e_bite_B_front_leads_bare_MUTED.mp4", ARENA, "--mute"); t.done()
    grid(["att_current", "artA_full", "rootB_full", "rootF_bare"], [CUR, "A, with tint + effects", "B, with tint + effects", RF],
         f"{out}/03_bite_prototypes_grid_full_MUTED.mp4", GRID)
    t = take("rootB_full", label="B: key poses + lunge, tint + effects", crop=ARENA); film(t, f"{out}/03a_bite_B_full_true_speed_sound.mp4", ARENA); t.done()
    t = take("artA_full", label="A: key poses, tint + effects", crop=ARENA); film(t, f"{out}/03b_bite_A_full_true_speed_sound.mp4", ARENA); t.done()
    sheet(["att_bare", "artA_bare", "rootB_bare", "rootB2_bare"], ["current", "A", "B", "B x2"], f"{out}/04_bite_current_A_B_every_frame_1to1.png", per_row=16,
          note="Every frame at play size, -720 .. +300 ms round t 0: the current strip, A (key poses, feet fixed), B (key poses + the lunge), B with twice the travel.")
    shutil.copy(os.path.join(B, "proto", "attack_strip_current_vs_proto.png"), f"{out}/04b_attack_strip_current_vs_proto.png")

    # 05..08 the flash: one hit under four grammars (the swing at 6700 and JAWS' answer at 7000)
    F = ["F0 CURRENT: full white, ~200 ms", "F1 short: 2 strong frames", "F2 low shaped: 0.45 peak, 80 ms", "F3 rim + contact accent"]
    grid(["F0", "F1", "F2", "F3"], F, f"{out}/05_flash_dark_whelp_grid_MUTED.mp4", GRID)
    grid(["F0", "F1", "F2", "F3"], F, f"{out}/05b_flash_dark_whelp_close_slow_4x_MUTED.mp4", "960,620,380,280", fps=15, shrink=1.0, lo=6650, hi=7250)
    for f in ("F0", "F1", "F2", "F3"):
        t = take(f, label=F[int(f[1])], crop=ARENA); film(t, f"{out}/05{f}_flash_dark_whelp_true_speed_sound.mp4", ARENA); t.done()
    sheet(["F0", "F1", "F2", "F3"], F, f"{out}/06_flash_every_frame_swing_and_answer.png", t0=SWING_MS + 17, lo=-17, hi=400, per_row=13, scale=0.75,
          note="Every frame at 0.75x from the swing's contact (t 0 = 6717) to past the bite (t +300 = 7017): the swing's flash, then the answer's flash.")
    grid(["lightF0", "lightF1", "lightF2", "lightF3"], F, f"{out}/07_flash_light_body_grid_MUTED.mp4", GRID)
    grid(["bossF0", "bossF1", "bossF2", "bossF3"], F, f"{out}/07b_flash_boss_grid_MUTED.mp4", GRID)
    grid(["repF0", "repF1", "repF2", "repF3"], F, f"{out}/08_flash_repeated_fast_tempo_grid_MUTED.mp4", GRID)
    for f in ("F0", "F2", "F3"):
        t = take("rep" + f, label=F[int(f[1])] + " - fast TEMPO", crop=ARENA); film(t, f"{out}/08{f}_flash_repeated_true_speed_sound.mp4", ARENA); t.done()

    # 09 the optional pose hold, 10 the best attack + best flash
    grid(["best_plate", "hold50"], ["B + F2, no hold", "B + F2, 50 ms pose hold"], f"{out}/09_pose_hold_50ms_comparison_MUTED.mp4", GRID)
    t = take("best_plate", label="BEST: B (key poses + lunge) + F2 flash, the answer on", crop=ARENA)
    film(t, f"{out}/10_best_attack_best_flash_true_speed_sound.mp4", ARENA)
    film(t, f"{out}/10b_best_attack_best_flash_true_speed_MUTED.mp4", ARENA, "--mute"); t.done()
    grid(["F0", "best_plate", "best_plateF3", "att_current"], ["CURRENT fight (F0)", "B + F2", "B + F3", "current, answer hidden"],
         f"{out}/10c_current_vs_best_grid_MUTED.mp4", GRID)
    t = take("best_rep", label="B + F2, fast TEMPO", crop=ARENA); film(t, f"{out}/10d_best_repeated_fast_tempo_sound.mp4", ARENA, "--music", "music_arena_shadow"); t.done()

    # 11 the JAWS concepts over the best plate (render_concepts_on_best.sh must have run)
    CB = os.path.join(B, "concepts")
    if os.path.isdir(CB):
        names = ["best_plate", "A/best_plate", "B/best_plate", "C/best_plate"]
        ok = all(os.path.exists(os.path.join(CB if "/" in n else B, n + ".log")) for n in names)
        if ok:
            tmpd = tempfile.mkdtemp()
            for n in names[1:]:
                c = n.split("/")[0]
                for f in os.listdir(os.path.join(CB, c)):
                    if f.startswith("best_plate"):
                        shutil.copy(os.path.join(CB, c, f), os.path.join(tmpd, f.replace("best_plate", "concept" + c)))
            shutil.copy(os.path.join(B, "best_plate.log"), os.path.join(tmpd, "best_plate.log"))
            for f in os.listdir(B):
                if f.startswith("best_plate_"):
                    shutil.copy(os.path.join(B, f), os.path.join(tmpd, f))
            grid(["best_plate", "conceptA", "conceptB", "conceptC"], ["improved baseline, no concept", "A Shadow Jaws", "B Shadow Tether", "C Trap Sigil"],
                 f"{out}/11_jaws_concepts_over_best_grid_MUTED.mp4", GRID, src_dir=tmpd)
            for c in "ABC":
                t = take("concept" + c, label=f"concept {c} over the improved baseline", crop=ARENA, src_dir=tmpd)
                film(t, f"{out}/11{c}_jaws_concept_{c}_over_best_true_speed_sound.mp4", ARENA); t.done()
            if blind:
                os.makedirs(blind, exist_ok=True)
                for code, n in (("q4", "conceptA"), ("q6", "conceptB"), ("q1", "conceptC")):
                    print("blind", code, flipbook(n, f"{blind}/clip_{code}.png", CONTACT_MS + 17, src_dir=tmpd))
            shutil.rmtree(tmpd, ignore_errors=True)

    if blind:
        os.makedirs(blind, exist_ok=True)
        for code, n in (("p2", "att_current"), ("p7", "rootB_full"), ("p5", "artA_full"), ("p9", "best_plate"), ("p3", "F0"), ("p8", "F3"),
                        ("p4", "rootB2_full")):
            print("blind", code, flipbook(n, f"{blind}/clip_{code}.png", CONTACT_MS + 17))
    print("done ->", out)


if __name__ == "__main__":
    main()
