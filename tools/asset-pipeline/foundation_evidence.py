#!/usr/bin/env python3
"""foundation_evidence.py -- the bite foundation's review battery (ADR-012), from the takes foundation_films.sh filmed.

    PYTHONUTF8=1 python tools/asset-pipeline/foundation_evidence.py <out dir> [<blind dir>]

OLD is the foundation study's own takes (build/shots/bite/att_current, F0, repF0, artA...: the crouch strip, the
contact-time shove, the full-white flash, filmed from the same seed); NEW is build/shots/foundation/. Writes the
numbered films, grids and sheets the review lists, the trace report, and (with a blind dir) the neutral flipbooks.
"""
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFont

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OLD = os.path.join(REPO, "build", "shots", "bite")
NEW = os.path.join(REPO, "build", "shots", "foundation")
FA = os.path.join(REPO, "tools", "asset-pipeline", "film_audio.py")
TL = os.path.join(REPO, "tools", "asset-pipeline", "bite_timeline.py")
ARENA = "380,410,1500,670"
GRID = "480,440,1280,460"
CLOSE_WHELP = (940, 600, 1300, 900)
CLOSE_SEEKER = (430, 540, 790, 900)
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
    shots = {}
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) > 4 and p[0] == "present" and p[3] == "shot":
            shots[int(p[4])] = (float(p[1]), float(p[2]))
    return shots


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
    def __init__(self, prefix, log, tmp):
        self.prefix, self.log, self.tmp = prefix, log, tmp

    def done(self):
        shutil.rmtree(self.tmp, ignore_errors=True)


def take(pre, lo=None, hi=None, label=None, crop=None):
    shots = read(pre + ".log")
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


def film(pre, out, label, *extra, lo=None, hi=None, crop=ARENA):
    t = take(pre, lo, hi, label, crop)
    print(run(FA, t.log, t.prefix, out, "--crop", crop, *extra))
    t.done()


def grid(pres, labels, out, crop, fps=60, shrink=0.6, lo=None, hi=None, tol=20):
    x, y, w, h = map(int, crop.split(","))
    tmp = tempfile.mkdtemp()
    shots = [read(p + ".log") for p in pres]
    cols = 2 if len(pres) > 2 else len(pres)
    rows = (len(pres) + cols - 1) // cols
    n = 0
    for k in sorted(shots[0]):
        ph = shots[0][k][1]
        if (lo is not None and ph < lo) or (hi is not None and ph > hi):
            continue
        img = Image.new("RGB", (cols * w + (cols - 1) * 6, rows * h + (rows - 1) * 6), (0, 0, 0))
        d = ImageDraw.Draw(img)
        ok = True
        for j, (pre, label, sh) in enumerate(zip(pres, labels, shots)):
            i = min(sh, key=lambda q: abs(sh[q][1] - ph))
            if abs(sh[i][1] - ph) > tol:
                ok = False
                break
            cell = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop((x, y, x + w, y + h))
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


def sheet(pres, labels, out, t0, lo, hi, box, per_row=16, scale=1.0, note=""):
    bw, bh = int((box[2] - box[0]) * scale), int((box[3] - box[1]) * scale)
    rows = []
    for pre in pres:
        shots = read(pre + ".log")
        rows.append((pre, [(i, ph - t0) for i, (w, ph) in sorted(shots.items()) if lo - 1 <= ph - t0 <= hi + 1]))
    n = max(len(fr) for _, fr in rows)
    lines = (n + per_row - 1) // per_row
    img = Image.new("RGB", (per_row * (bw + 6) + 6, len(rows) * lines * (bh + 26) + 50), (20, 17, 14))
    d = ImageDraw.Draw(img)
    yy = 6
    for (pre, fr), label in zip(rows, labels):
        for k, (i, t) in enumerate(fr):
            r, c = divmod(k, per_row)
            x, y = 6 + c * (bw + 6), yy + r * (bh + 26)
            im = Image.open(f"{pre}_{i:02d}.png").convert("RGB").crop(box)
            if scale != 1.0:
                im = im.resize((bw, bh), Image.LANCZOS)
            d.text((x, y), f"{t:+.0f}" + (f"  {label}" if k == 0 else ""), fill=(235, 200, 120), font=SMALL)
            img.paste(im, (x, y + 20))
        yy += lines * (bh + 26)
    d.text((6, img.height - 40), note, fill=(200, 180, 150), font=SMALL)
    img.save(out)
    print(os.path.basename(out))


def flipbook(pre, out, t0, lo=-800, hi=400, crop=(440, 522, 1340, 900), cols=5, scale=0.5):
    shots = read(pre + ".log")
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


def main():
    out = os.path.abspath(sys.argv[1])
    blind = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png")):
            os.remove(os.path.join(out, f))
    o = lambda n: os.path.join(OLD, n)     # noqa: E731
    n = lambda m: os.path.join(NEW, m)     # noqa: E731
    OLDL, NEWL = "OLD: crouch strip, shove on contact, full-white flash", "NEW: bite strip, front-led lunge, recoil, quiet flash"

    # ENEMY ATTACK 1-7
    grid([o("att_current"), n("new")], [OLDL, NEWL], f"{out}/01_enemy_attack_old_vs_new_true_speed_MUTED.mp4", GRID)
    film(o("att_current"), f"{out}/01a_enemy_attack_OLD_true_speed_sound.mp4", OLDL)
    film(n("new"), f"{out}/02_enemy_attack_NEW_true_speed_sound.mp4", NEWL)
    film(n("new_bare"), f"{out}/03_enemy_attack_NEW_bare_no_tint_no_vfx_MUTED.mp4", "NEW, tint and effects OFF (the acceptance view)", "--mute")
    film(n("new_sil"), f"{out}/04_enemy_attack_NEW_silhouette_MUTED.mp4", "NEW, silhouette", "--mute")
    grid([o("att_bare"), n("new_bare"), n("new_noroot"), n("new_sil")],
         ["OLD bare", "NEW bare", "NEW bare, lunge OFF (the strip alone)", "NEW silhouette"], f"{out}/03b_enemy_attack_bare_views_grid_MUTED.mp4", GRID)
    shutil.copy(os.path.join(REPO, "tools", "asset-pipeline", "v2", "keypose_sources", "umbral_swarm_bite_sheet.png"), f"{out}/05_key_poses_crouch_vs_bite.png")
    sheet([o("att_bare"), n("new_bare"), n("new_sil")], ["OLD bare", "NEW bare", "NEW silhouette"], f"{out}/05b_enemy_attack_every_frame_1to1.png",
          7017, -720, 300, CLOSE_WHELP, note="Every frame at play size, -720 .. +300 ms round the first frame that shows the bite (t 0).")
    open(f"{out}/06_root_motion_trace.md", "w", encoding="utf-8").write(
        "# The bite's sentence on the playhead (tools/asset-pipeline/bite_timeline.py)\n\n## normal TEMPO, the bite at 7000 (`new`)\n\n"
        + run(TL, n("new") + ".log", "--bite", "7017") + "\n\n## fast TEMPO, the bite at 2000 during SPRAY's flight (`rep`)\n\n"
        + run(TL, n("rep") + ".log", "--bite", "2017") + "\n\n## fast TEMPO, the bite at 13000, HARD HANDS leaping (`hh`)\n\n"
        + run(TL, n("hh") + ".log", "--bite", "13017") + "\n\n## the boss fixture, the bite at 3000 with the Seeker idle (`boss`)\n\n"
        + run(TL, n("boss") + ".log", "--bite", "3017") + "\n")
    film(n("new"), f"{out}/07_pack_attack_four_whelps_true_speed_sound.mp4", "NEW: the pack, front-led lockstep", crop="900,410,980,670")

    # RECEIVER 8-12
    film(n("boss"), f"{out}/08_receiver_idle_hit_boss_bite_sound.mp4", "hit while idle (the boss fixture's bite at 3000)", lo=2500, hi=3400)
    film(n("new"), f"{out}/09_receiver_hit_during_basic_swing_sound.mp4", "hit 300 ms after the swing's beat (7000)", lo=6400, hi=7500)
    film(n("rep"), f"{out}/10_receiver_hit_during_spray_sound.mp4", "hits during SPRAY (1000 in its wind-up, 2000 in its flight)")
    film(n("hh"), f"{out}/11_receiver_hit_during_hard_hands_sound.mp4", "hit at 13000, HARD HANDS leaps at 13083")
    grid([n("new"), n("new_norecoil")], [NEWL, "NEW, recoil OFF"], f"{out}/12_receiver_recoil_on_vs_off_MUTED.mp4", "430,540,360,360", shrink=1.0, lo=6900, hi=7300)
    film(n("rep"), f"{out}/12b_receiver_repeated_bites_fast_tempo_sound.mp4", "repeated bites, fast TEMPO")
    sheet([n("new"), n("new_norecoil")], ["NEW", "recoil OFF"], f"{out}/12c_receiver_every_frame_1to1.png", 7017, -50, 150, CLOSE_SEEKER, per_row=13,
          note="Every frame at play size round the bite: the Seeker with and without the recoil and the contact accent.")

    # FLASH 13-18
    grid([n("new_oldflash"), n("new")], ["OLD flash (1.0, 200 ms) on the NEW foundation", "NEW flash (0.45, 80 ms)"], f"{out}/13_flash_old_vs_new_dark_whelp_MUTED.mp4", GRID)
    grid([n("new_oldflash"), n("new")], ["OLD flash", "NEW flash"], f"{out}/14_flash_dark_whelp_close_slow_4x_MUTED.mp4", "940,600,360,300", fps=15, shrink=1.0, lo=6650, hi=7250)
    grid([n("light_oldflash"), n("light")], ["OLD flash, light body", "NEW flash, light body"], f"{out}/15_flash_light_creature_MUTED.mp4", GRID)
    film(n("boss"), f"{out}/16_flash_boss_true_speed_sound.mp4", "the boss (pinned alive), NEW flash", lo=1900, hi=2600)
    film(n("new"), f"{out}/17_crit_jaws_answer_critical_sound.mp4", "a CRITICAL: JAWS' answer at 7000 (-7 JAWS CRITICAL)", lo=6850, hi=7500)
    film(n("kill"), f"{out}/18_killing_hit_true_speed_sound.mp4", "the killing hit on slot 0 (5100), fast TEMPO", lo=4700, hi=5800)
    sheet([n("kill")], ["kill"], f"{out}/18b_killing_hit_every_frame.png", 5117, -100, 400, (940, 600, 1300, 900), per_row=15, scale=0.75,
          note="Every frame from the killing hit: contact, the quiet flash, the fall.")

    # FULL SENTENCE 19-21
    film(n("new"), f"{out}/19_full_sentence_true_speed_sound.mp4", "enemy attack -> Seeker reaction, true speed")
    film(n("new"), f"{out}/20_full_sentence_true_speed_MUTED.mp4", "enemy attack -> Seeker reaction, muted", "--mute")
    film(n("rep"), f"{out}/21_repeated_combat_fast_tempo_music.mp4", "repeated normal combat, fast TEMPO", "--music", "music_arena_shadow")
    grid([o("repF0"), n("rep")], [OLDL, NEWL], f"{out}/21b_repeated_combat_old_vs_new_MUTED.mp4", GRID)

    # JAWS RECHECK 22-24
    C = os.path.join(NEW, "concepts")
    grid([n("new"), os.path.join(C, "A", "new"), os.path.join(C, "B", "new"), os.path.join(C, "C", "new")],
         ["the new foundation, no concept", "A Shadow Jaws", "B Shadow Tether", "C Trap Sigil"], f"{out}/22_jaws_concepts_over_new_foundation_grid_MUTED.mp4", GRID)
    for c, num in (("A", 22), ("B", 23), ("C", 24)):
        film(os.path.join(C, c, "new"), f"{out}/{num}{c}_jaws_concept_{c}_over_new_foundation_sound.mp4", f"concept {c} over the new foundation")

    if blind:
        os.makedirs(blind, exist_ok=True)
        for code, pre in (("r3", n("new")), ("r6", n("new_bare")), ("r1", os.path.join(C, "A", "new")), ("r8", os.path.join(C, "B", "new")),
                          ("r5", os.path.join(C, "C", "new"))):
            print("blind", code, flipbook(pre, f"{blind}/clip_{code}.png", 7017))
    print("done ->", out)


if __name__ == "__main__":
    main()
