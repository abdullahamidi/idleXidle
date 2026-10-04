#!/usr/bin/env python3
"""mix_in_the_fight.py -- the basic attacks' IDENTITY cues in the fight's OWN soundtrack (P1.6, the PRESS method: press_audio_evidence's
context_levels). Each p16 take's soundtrack is rendered by film_audio.py from the trace (volume x the SFX master x any
duck, the bank's throttle from sound_throttle.json); then once more per voice with that voice silenced: the difference is
the voice's OWN share of the mix. For every ask of a voice, its own loudest 50 ms K-weighted over the next 250 ms, and the
whole mix's at that moment.

    python production/qa/evidence/vfx-sweep/phase1-p6/mix_in_the_fight.py production/qa/evidence/vfx-sweep/phase1-p6 > mix_in_the_fight.md
"""
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "tools", "asset-pipeline"))
from make_press_tick import k_weight  # noqa: E402

FA = os.path.join(REPO, "tools", "asset-pipeline", "film_audio.py")
HITS = ['sfx_seeker_swing_hit', 'sfx_anvil_swing_hit', 'sfx_metronome_swing_hit', 'sfx_tower_swing_hit', 'sfx_thornwall_swing_hit', 'sfx_magpie_swing_hit', 'sfx_quiver_swing_hit', 'sfx_chorus_swing_hit', 'sfx_unbroken_swing_hit', 'sfx_oathbound_swing_hit']
RELEASES = ['sfx_quiver_loose', 'sfx_chorus_toss', 'sfx_unbroken_toss']
VOICES = HITS + RELEASES + ["sfx_seeker_spray_hit", "sfx_seeker_hard_hands_hit", "sfx_seeker_press_tick", "sfx_hit"]
TAKES = ['p16_seeker', 'p16_anvil', 'p16_metronome', 'p16_tower', 'p16_thornwall', 'p16_magpie', 'p16_quiver', 'p16_chorus', 'p16_unbroken', 'p16_oathbound']


def render(prefix, tmp, *extra):
    wav, mp4 = os.path.join(tmp, "mix.wav"), os.path.join(tmp, "mix.mp4")
    subprocess.run([sys.executable, FA, prefix + ".log", prefix, mp4, "--wav", wav, *extra], check=False, capture_output=True)   # the frames are gone: the mp4 fails, the wav is written first
    with wave.open(wav) as w:
        sr, ch, n = w.getframerate(), w.getnchannels(), w.getnframes()
        a = np.frombuffer(w.readframes(n), dtype="<i2").astype(float).reshape(-1, ch).mean(1) / 32768
    for f in (wav, mp4):
        if os.path.exists(f):
            os.remove(f)
    return a, sr


def asks(log):
    t0, out = None, []
    for line in open(log, encoding="utf-8", errors="replace"):
        p = line.rstrip("\n").split("\t")
        if len(p) < 5 or p[0] != "present":
            continue
        if p[3] == "shot" and t0 is None:
            t0 = float(p[1])
        elif p[3] == "sound":
            kv = dict(x.split("=", 1) for x in p[5:] if "=" in x)
            out.append((float(p[1]), p[2], p[4], float(kv.get("vol", 1)) * float(kv.get("duck", 1))))
    return t0, out


def k50(a, sr, c):
    """Loudest 50 ms K-weighted RMS in [c, c + 250 ms] (with a 60 ms filter pre-roll), dB."""
    s0 = max(0, c - int(0.06 * sr))
    seg = k_weight(a[s0:c + int(0.25 * sr)])[c - s0:]
    w = int(0.05 * sr)
    if len(seg) < w:
        return -120.0
    acc = np.concatenate([[0.0], np.cumsum(seg * seg)])
    return 10 * np.log10(max(1e-12, ((acc[w:] - acc[:-w]) / w).max()))


def main():
    root = sys.argv[1]
    print("# The archetype cues in the fight's own mix (P1.5)\n")
    print("film_audio.py's render of each take's sound asks (sound_throttle.json applied). 'own' = the voice's share (the mix minus")
    print("the mix with that voice silenced); 'moment' = the whole mix. Loudest 50 ms K-weighted over the 250 ms after the ask, dB.\n")
    print("| take | trace ms | voice | asked vol | own K50 | moment K50 |")
    print("|---|---|---|---|---|---|")
    summary = {}
    with tempfile.TemporaryDirectory() as tmp:
        silent = os.path.join(tmp, "silent.wav")
        with wave.open(silent, "wb") as w:
            w.setnchannels(1), w.setsampwidth(2), w.setframerate(44100), w.writeframes(b"\0\0" * 4410)
        for take in TAKES:
            prefix = os.path.join(root, take)
            if not os.path.exists(prefix + ".log"):
                continue
            t0, sounds = asks(prefix + ".log")
            full, sr = render(prefix, tmp)
            present = sorted({k for _, _, k, _ in sounds if k in VOICES})
            own = {k: full - render(prefix, tmp, "--cue", f"{k}={silent}")[0] for k in present}
            seen = set()
            for clock, ms, key, vol in sounds:
                if key not in own:
                    continue
                c = int((clock - t0) / 1000 * sr)
                if c < 0 or c >= len(full) or (key, ms) in seen:
                    continue
                seen.add((key, ms))
                o, m = k50(own[key], sr, c), k50(full, sr, c)
                if o < -90:          # dropped by the throttle, or outside the film window
                    continue
                summary.setdefault(key, []).append(o)
                print(f"| {take} | {ms} | `{key}` | {vol:.2f} | {o:.1f} | {m:.1f} |")
    print("\n## Per voice (own K50, dB: loudest / quietest ask)\n")
    print("| voice | asks | loudest | quietest |")
    print("|---|---|---|---|")
    for k in VOICES:
        if k in summary:
            print(f"| `{k}` | {len(summary[k])} | {max(summary[k]):.1f} | {min(summary[k]):.1f} |")
    arche = [max(summary[k]) for k in HITS if k in summary]
    refs = [min(summary[k]) for k in ("sfx_seeker_spray_hit", "sfx_seeker_hard_hands_hit") if k in summary]
    if arche and refs:
        print(f"\nThe loudest identity swing hit in any take sits {min(refs) - max(arche):.1f} dB under the quietest SPRAY / HARD HANDS "
              f"contact heard in the Seeker's fight.")


if __name__ == "__main__":
    main()
