#!/usr/bin/env python3
"""press_audio_evidence.py -- PRESS's final audio (ADR-011, the owner's brief 2026-09-28: the picture is APPROVED at
fab25919; the tick gets ONE composite cue -- pressure release, compression thump, defence crack -- synced to the crush,
under SPRAY / HARD HANDS / JAWS in the mix). The review: PRESS at true speed with each candidate, muted, the cue alone
(and a sheet of its layers), repeated ticks (in the fight, and alone at 2 s and at an upgraded 0.7 s), and PRESS beside
SPRAY, HARD HANDS and JAWS; plus the mix measured in the fight's own soundtrack.

    bash tools/asset-pipeline/films_press.sh <tag> normal repeated fast yield
    PYTHONUTF8=1 python tools/asset-pipeline/make_press_tick.py
    PYTHONUTF8=1 python tools/asset-pipeline/press_audio_evidence.py <tag> <out dir>
"""
import os
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw

from foundation_evidence import REPO, SMALL, film

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_press_tick import BUILDS, CANDIDATES, CUE_THUMP_MS, KEY, RECOMMENDED, SR, TICK_VOLUME, band, k_weight, max1, measure, read_wav  # noqa: E402

CAND = {k: os.path.join(CANDIDATES, f"{KEY}_{k}_{name}.wav") for k, (name, _) in BUILDS.items()}
LABEL = {"A": "A balanced", "B": "B heavier pressure", "C": "C clearer defence break"}


def write_wav(path, x):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(np.round(np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


def cue_sheet(out):
    """Each candidate: its waveform (the layers marked: pressure before the thump, the thump at 40 ms, the crack) and a
    spectrogram, over the same 200 ms."""
    W, H, pad = 900, 170, 30
    img = Image.new("RGB", (W + 2 * pad, 3 * (2 * H + 50) + 20), (20, 17, 14))
    d = ImageDraw.Draw(img)
    for r, k in enumerate("ABC"):
        y = read_wav(CAND[k])
        m = measure(y)
        y0 = 10 + r * (2 * H + 50)
        d.text((pad, y0), f"{LABEL[k]}   thump {m['thump_ms']:.0f} ms, crack +{m['gap_ms']:.0f} ms, pressure {m['pre_db']:.1f} dB, crack {m['crack_db']:.1f} dB "
                         f"(against the loudest 50 ms)   {'(recommended)' if k == RECOMMENDED else ''}", fill=(245, 220, 150), font=SMALL)
        span = 0.2
        n = int(span * SR)
        yy = np.zeros(n)
        yy[:min(n, len(y))] = y[:n]
        top = y0 + 22
        for i in range(W):
            seg = yy[int(i / W * n):int((i + 1) / W * n) + 1]
            a = np.abs(seg).max() / max(1e-9, np.abs(yy).max())
            d.line((pad + i, top + H / 2 - a * H / 2, pad + i, top + H / 2 + a * H / 2), fill=(176, 146, 238))
        for t, name, col in ((m["thump_ms"], "THUMP", (240, 200, 120)), (m["thump_ms"] + m["gap_ms"], "CRACK", (236, 224, 255))):
            x = pad + t / 1000 / span * W
            d.line((x, top, x, top + H), fill=col)
            d.text((x + 3, top + 2 + (14 if name == "CRACK" else 0)), name, fill=col, font=SMALL)
        d.text((pad + 3, top + H - 16), "PRESSURE", fill=(150, 112, 214), font=SMALL)
        # spectrogram: 4 ms hops, 256-point frames, 30 Hz..16 kHz on a log axis
        stop = top + H + 6
        hop, win = int(0.002 * SR), 512
        cols = []
        for j in range(0, n - win, hop):
            f = np.abs(np.fft.rfft(yy[j:j + win] * np.hanning(win)))
            cols.append(20 * np.log10(f + 1e-7))
        S = np.array(cols).T
        freqs = np.fft.rfftfreq(win, 1 / SR)
        spec = Image.new("RGB", (W, H))
        px = spec.load()
        vmax = S.max()
        for yi in range(H):
            f = 30 * (16000 / 30) ** (1 - yi / (H - 1))
            fi = int(np.clip(np.searchsorted(freqs, f), 0, len(freqs) - 1))
            row = S[fi]
            for xi in range(W):
                v = np.clip((row[min(len(row) - 1, int(xi / W * len(row)))] - vmax + 70) / 70, 0, 1)
                px[xi, yi] = (int(40 + 200 * v), int(30 + 150 * v ** 1.5), int(60 + 190 * v ** 0.8))
        img.paste(spec, (pad, stop))
        for f in (100, 1000, 4000):
            yl = stop + (1 - np.log(f / 30) / np.log(16000 / 30)) * (H - 1)
            d.text((pad + W + 3, yl - 7), f"{f if f < 1000 else str(f // 1000) + 'k'}", fill=(200, 190, 170), font=SMALL)
    img.save(out)
    print(os.path.basename(out), img.size)


def repeated(k, spacing, count, out):
    """The cue alone, `count` times, `spacing` s apart, with the game's small per-play pitch variation (+-0.03 octave)."""
    y = read_wav(CAND[k])
    rng = np.random.default_rng(7)
    buf = np.zeros(int((spacing * count + 0.5) * SR))
    for i in range(count):
        r = 2 ** (rng.uniform(-0.03, 0.03))
        idx = np.arange(0, len(y) - 1, r)
        v = np.interp(idx, np.arange(len(y)), y)
        a = int((0.2 + i * spacing) * SR)
        buf[a:a + len(v)] += v * TICK_VOLUME * 0.8              # TickVolume x the SFX master, as in the game
    write_wav(out, buf)
    print(os.path.basename(out))


def as_played(out):
    """The three candidates, then SPRAY's contact, HARD HANDS' impact and JAWS' bite, 1.2 s apart, each at the level the
    game plays it (the cue file x its play volume x the SFX master 0.8): which is louder, and what each one says."""
    combat = os.path.join(REPO, "assets", "audio", "combat")
    voices = [(CAND[k], TICK_VOLUME) for k in "ABC"] + [(os.path.join(combat, key + ".wav"), vol) for key, vol in
              (("sfx_seeker_spray_hit", 0.50), ("sfx_seeker_hard_hands_hit", 0.55), ("sfx_seeker_jaws_bite", 0.42))]
    buf = np.zeros(int((1.2 * len(voices) + 0.6) * SR))
    for i, (path, vol) in enumerate(voices):
        y = read_wav(path) * vol * 0.8
        a = int((0.3 + i * 1.2) * SR)
        buf[a:a + len(y)] += y[:len(buf) - a]
    write_wav(out, buf)
    print(os.path.basename(out))


def context_levels(tag, out_md):
    """PRESS in the fight's own soundtrack (film_audio's render of the game's mix decisions: volume x the SFX master x any
    duck). The soundtrack is rendered twice, with PRESS's cue and with it silenced: the difference is PRESS's OWN share of
    the mix (in this seed the pack's own bite lands on every PRESS tick, so the whole mix there is two voices), and the
    track without it gives the other voices' levels."""
    import subprocess
    here = os.path.dirname(out_md)
    silent = os.path.join(here, "_silent.wav")
    write_wav(silent, np.zeros(int(0.2 * SR)))
    pre = lambda k: os.path.join(REPO, "build", "shots", "press", tag, k)   # noqa: E731
    fa = os.path.join(REPO, "tools", "asset-pipeline", "film_audio.py")

    def render(take, *extra):
        wav, mp4 = os.path.join(here, "_mix.wav"), os.path.join(here, "_mix.mp4")
        subprocess.run([sys.executable, fa, pre(take) + ".log", pre(take), mp4, "--wav", wav, *extra], check=True, capture_output=True)
        with wave.open(wav) as w:
            sr, ch, n = w.getframerate(), w.getnchannels(), w.getnframes()
            a = np.frombuffer(w.readframes(n), dtype="<i2").astype(float).reshape(-1, ch).mean(1) / 32768
        os.remove(wav)
        os.remove(mp4)
        return a, sr

    def loud(a, sr, c, span=0.15):
        seg = a[max(0, c - int(0.03 * sr)):c + int(span * sr)]
        w50 = int(0.05 * sr)
        return 20 * np.log10(max(1e-9, max(np.sqrt((seg[j:j + w50] ** 2).mean()) for j in range(0, max(1, len(seg) - w50), int(0.002 * sr)))))

    def played(path, volume, pitch=0.0):
        y = read_wav(path)
        if pitch:
            y = np.interp(np.arange(0, len(y) - 1, 2 ** pitch), np.arange(len(y)), y)
        y = y * volume * 0.8
        return 20 * np.log10(max1(y, 0.05)), 20 * np.log10(max1(k_weight(y), 0.05))

    combat = os.path.join(REPO, "assets", "audio", "combat")
    lines = ["# PRESS in the mix", "", "## Each voice alone, as the game plays it", "",
             "The loudest 50 ms (dBFS) of each cue file x its play volume x the SFX master (0.8), raw and K-weighted (ITU-R",
             "BS.1770: how loud it is heard; the raw figure under-reads bright cues and over-reads sub-heavy ones).", "",
             "| voice | volume | raw | K-weighted |", "|---|---|---|---|"]
    for k in "ABC":
        r_, k_ = played(CAND[k], TICK_VOLUME)
        lines.append(f"| PRESS {LABEL[k]} | {TICK_VOLUME} | {r_:.1f} dB | {k_:.1f} dB |")
    for name, key, vol, pitch in (("SPRAY contact", "sfx_seeker_spray_hit", 0.50, 0.0), ("HARD HANDS contact", "sfx_seeker_hard_hands_hit", 0.55, 0.0),
                                  ("JAWS bite", "sfx_seeker_jaws_bite", 0.42, 0.0),
                                  ("the pack's own bite on the champion (a generic hit, on every PRESS tick in this seed)", "sfx_hit", 0.30, -0.25)):
        r_, k_ = played(os.path.join(combat, key + ".wav"), vol, pitch)
        lines.append(f"| {name} | {vol} | {r_:.1f} dB | {k_:.1f} dB |")
    lines += ["", "## In the fight (film_audio's render of the game's sound asks, recommended cue)", "",
              "PRESS's OWN level is the difference between the soundtrack with its cue and with it silenced; 'the moment' is",
              "the whole mix at that instant (PRESS with whatever else sounds). Raw loudest 50 ms, dBFS.", "",
              "| take | moment | voice | alone | the moment |", "|---|---|---|---|---|"]
    moments = {"normal": [(6000, "PRESS tick", True)],
               "repeated": [(8000, "PRESS QUIET tick (an action's contact close by: x0.6)", True)],
               "fast": [(1400, "SPRAY contact", False), (2000, "PRESS tick (600 ms after SPRAY)", True), (3600, "HARD HANDS contact", False),
                        (4000, "PRESS QUIET tick (400 ms after HARD HANDS, the champion still performing it: x0.6)", True)],
               "yield": [(10000, "PRESS tick giving way to JAWS (x0.5)", True), (10333, "JAWS bite", False)]}
    for take, marks in moments.items():
        a, sr = render(take)
        b, _ = render(take, "--cue", f"{KEY}={silent}")
        t0 = start_ms(pre(take))
        for ms, name, press in marks:
            c = int((ms - 20 - t0) / 1000 * sr) if press else int((ms - t0) / 1000 * sr)
            level = loud(a - b, sr, c) if press else loud(b, sr, c, 0.45)   # JAWS' weight peaks 120-330 ms in
            moment = loud(a, sr, c, 0.15 if press else 0.45)
            lines.append(f"| {take} | {ms} | {name} | {level:.1f} dB | {moment:.1f} dB |")
    os.remove(silent)
    open(out_md, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(os.path.basename(out_md))


def start_ms(prefix):
    from foundation_evidence import read
    shots = read(prefix + ".log")
    return min(v[1] for v in shots.values())


def main():
    tag, out = sys.argv[1], os.path.abspath(sys.argv[2])
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith((".mp4", ".png", ".md", ".wav")) and f[0].isdigit():
            os.remove(os.path.join(out, f))
    n = lambda k: os.path.join(REPO, "build", "shots", "press", tag, k)   # noqa: E731
    cue = lambda k: ("--cue", f"{KEY}={CAND[k]}")                           # noqa: E731
    for k in "ABC":
        film(n("normal"), f"{out}/01_{k}_true_speed_sound.mp4", f"PRESS with cue {LABEL[k]}", *cue(k))
    film(n("normal"), f"{out}/02_muted.mp4", "PRESS, muted", "--mute")
    for k in "ABC":
        write_wav(f"{out}/03_{k}_cue_alone.wav", read_wav(CAND[k]))
    cue_sheet(f"{out}/03_cue_layers_A_B_C.png")
    as_played(f"{out}/03_as_played_A_B_C_spray_hard_hands_jaws.wav")
    for k in "ABC":
        film(n("repeated"), f"{out}/04_{k}_repeated_ticks_sound.mp4", f"repeated ticks, cue {LABEL[k]} (normal, quiet near an action, giving way to JAWS)", *cue(k))
        repeated(k, 2.0, 6, f"{out}/04_{k}_alone_every_2s.wav")
        repeated(k, 0.7, 10, f"{out}/04_{k}_alone_every_0.7s.wav")
    film(n("fast"), f"{out}/05_with_spray_sound.mp4", "SPRAY at 1400, then the PRESS tick at 2000 (cue A)", *cue("A"), lo=880, hi=2500)
    film(n("fast"), f"{out}/06_with_hard_hands_sound.mp4", "HARD HANDS at 3600, then the PRESS tick at 4000 (cue A)", *cue("A"), lo=3100, hi=4700)
    film(n("yield"), f"{out}/07_with_jaws_sound.mp4", "the PRESS tick at 10000 giving way while JAWS bites the same creature (cue A)", *cue("A"))
    context_levels(tag, f"{out}/08_mix_in_the_fight.md")
    lines = [l.rstrip("\n") for l in open(n("normal") + ".log", encoding="utf-8", errors="replace") if "\tfield-cue" in l or "\tsound\t" in l]
    open(f"{out}/09_trace_sound.md", "w", encoding="utf-8").write("# The normal take's sound asks and PRESS's cue on the playhead\n\n```\n" + "\n".join(lines) + "\n```\n")
    print("done ->", out)


if __name__ == "__main__":
    main()
