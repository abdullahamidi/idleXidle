#!/usr/bin/env python3
"""film_audio -- turn a TRACED capture film into a review video, with the sound the game asked for.

    python tools/asset-pipeline/film_audio.py <film.log> <frames prefix> <out.mp4> [--slow 4] [--mute]
                                              [--music music_arena_nature] [--crop x,y,w,h] [--wav out.wav]
                                              [--cue sfx_key=path/to/old.wav ...]

The capture rig runs the game SILENT (no audio device), so a film has no sound. But with RH_PRESENT_TRACE=1
the game logs every sound it ASKS for (SoundBank.Play logs before its enabled check: key, volume, pitch,
pan) on the same clock as every saved frame (`shot N`). This renders that list with the real cue files into
a soundtrack aligned to the frames and muxes it into an MP4, so a film can be judged with its audio.

WHAT IT REPRODUCES, AND WHAT IT CANNOT:
  * the cue, its volume x the SFX master (0.8) x the mix duck an authored action applied (`duck=`), its
    pitch (octaves, by resampling), its pan (a balance law: centre = full level in both ears);
  * the per-cue repeat THROTTLE of SoundBank.Play (the min gap and the quieter-when-recent rule) for every
    cue but the unthrottled ones (the SPRAY tick), judged on the trace clock, not the wall clock, from
    tools/asset-pipeline/sound_throttle.json (generated from SoundBank by sound_throttle_table_test);
  * the region's music bed at the music master (0.5), from an arbitrary loop point (--music; off by default).
  NOT the per-play random pitch `vary` (it is applied after the log line and is not logged), and not the
  voice limit of the audio device. It is a RENDER of the game's mix decisions, not a recording of a device:
  the ear test on real hardware is still the only proof that the sound is right.

--slow N plays the frames N times slower and the audio N times slower (pitch down, like tape), so a slow
view keeps its sync. --mute writes the same video with no audio track. --cue KEY=PATH renders that cue from another
file (a BEFORE film heard with the sound it had then, after the cue in assets/ was replaced).
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
AUDIO = os.path.join(REPO, "assets", "audio")
RATE = 44100
SFX_MASTER, MUSIC_MASTER = 0.8, 0.5
# THE BANK'S THROTTLE, READ FROM THE GENERATED TABLE (never mirrored by hand): sound_throttle.json is written from
# SoundBank itself by tests/unit/IdleXIdle.Game.Tests/sound_throttle_table_test.cs, which fails when it is stale.
# It holds the default gap, the per-cue gaps, the quieter-when-recent half-life and every cue played with throttle:false.
THROTTLE_JSON = os.path.join(HERE, "sound_throttle.json")
with open(THROTTLE_JSON, encoding="utf-8") as _f:
    _THROTTLE = json.load(_f)
MIN_REPEAT_MS = float(_THROTTLE["default_min_gap_ms"])
MIN_GAPS = {k.lower(): float(v) for k, v in _THROTTLE["min_gap_ms"].items()}   # the bank's lookup ignores case
HALF_LIFE_MS = float(_THROTTLE["repeat_half_life_ms"])
FREE_OF_THROTTLE = {k.lower() for k in _THROTTLE["unthrottled"]}


def ffmpeg() -> str:
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        return "ffmpeg"


CUE_FILES: dict[str, str] = {}   # --cue overrides: key -> a wav outside assets/


def load(key: str, cache: dict) -> np.ndarray | None:
    """A cue as float32 [n, 2] at RATE, or None when no file has that name."""
    if key in cache:
        return cache[key]
    path = CUE_FILES.get(key)
    for root, _, files in ([] if path else os.walk(AUDIO)):
        if key + ".wav" in files:
            path = os.path.join(root, key + ".wav")
            break
    data = None
    if path:
        with wave.open(path) as w:
            ch, sr, sw, n = w.getnchannels(), w.getframerate(), w.getsampwidth(), w.getnframes()
            raw = w.readframes(n)
        a = np.frombuffer(raw, dtype={1: np.uint8, 2: np.int16, 4: np.int32}[sw]).astype(np.float32)
        a = (a - 128) / 128 if sw == 1 else a / float(2 ** (8 * sw - 1))
        a = a.reshape(-1, ch)
        if ch == 1:
            a = np.repeat(a, 2, axis=1)
        if sr != RATE:
            a = resample(a, RATE / sr)
        data = a[:, :2]
    cache[key] = data
    return data


def resample(a: np.ndarray, factor: float) -> np.ndarray:
    """Linear resample by `factor` (2.0 = twice as many samples = an octave down at the same rate)."""
    n = max(1, int(round(len(a) * factor)))
    x = np.linspace(0, len(a) - 1, n)
    return np.stack([np.interp(x, np.arange(len(a)), a[:, c]) for c in range(a.shape[1])], axis=1).astype(np.float32)


def parse(log: str):
    shots, sounds = {}, []
    with open(log, encoding="utf-8", errors="replace") as f:
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) < 5 or p[0] != "present":
                continue
            clock = float(p[1])
            if p[3] == "shot":
                shots[int(p[4])] = clock
            elif p[3] == "sound":
                kv = dict(x.split("=", 1) for x in p[5:] if "=" in x)
                # `duck` is the mix duck an authored action applied to this (non-lead) sound; absent = 1
                vol = float(kv.get("vol", 1)) * float(kv.get("duck", 1))
                sounds.append((clock, p[4], vol, float(kv.get("pitch", 0)), float(kv.get("pan", 0))))
    return shots, sounds


def render(shots: dict, sounds: list, slow: float, music: str | None) -> np.ndarray:
    t0 = shots[min(shots)]
    t1 = shots[max(shots)] + 1000.0 / 60.0
    dur_ms = (t1 - t0) * slow
    out = np.zeros((int(dur_ms / 1000 * RATE) + RATE, 2), dtype=np.float32)
    cache: dict = {}
    recent: dict = {}
    for clock, key, vol, pitch, pan in sounds:
        # the throttle runs on EVERY ask, before the film window, exactly as the bank would have
        if key.lower() not in FREE_OF_THROTTLE:
            if key in recent:
                last, r = recent[key]
                since = clock - last
                if since < MIN_GAPS.get(key.lower(), MIN_REPEAT_MS):
                    continue
                r = r * 0.5 ** (since / HALF_LIFE_MS) + 1.0
                recent[key] = (clock, r)
                vol /= r ** 0.5
            else:
                recent[key] = (clock, 1.0)
        if clock < t0 - 1500 or clock > t1:
            continue
        cue = load(key, cache)
        if cue is None:
            continue
        cue = resample(cue, 2.0 ** (-pitch) * slow) if (pitch or slow != 1) else cue
        gain = min(1.0, vol * SFX_MASTER)
        pan = max(-1.0, min(1.0, pan))
        lr = np.array([min(1.0, 1.0 - pan), min(1.0, 1.0 + pan)], dtype=np.float32) * gain
        at = int((clock - t0) * slow / 1000 * RATE)
        s0, c0 = max(0, at), max(0, -at)
        n = min(len(cue) - c0, len(out) - s0)
        if n > 0:
            out[s0:s0 + n] += cue[c0:c0 + n] * lr
    if music:
        bed = load(music, cache)
        if bed is None:
            sys.exit(f"no music cue named {music}")
        if slow != 1:
            bed = resample(bed, slow)
        reps = int(np.ceil(len(out) / len(bed)))
        out += np.tile(bed, (reps, 1))[: len(out)] * MUSIC_MASTER
    out = out[: int(dur_ms / 1000 * RATE)]
    return np.tanh(out * 1.0).astype(np.float32)  # a soft ceiling; the game's own mix never reaches it


def write_wav(path: str, a: np.ndarray) -> None:
    pcm = (np.clip(a, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(pcm.tobytes())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("log"); ap.add_argument("prefix"); ap.add_argument("out")
    ap.add_argument("--slow", type=float, default=1.0)
    ap.add_argument("--mute", action="store_true")
    ap.add_argument("--music", default=None)
    ap.add_argument("--crop", default="480,20,1090,1040")
    ap.add_argument("--wav", default=None, help="also keep the rendered soundtrack here")
    ap.add_argument("--cue", action="append", default=[], help="KEY=PATH: render this cue from another wav")
    a = ap.parse_args()
    for spec in a.cue:
        key, _, path = spec.partition("=")
        CUE_FILES[key] = path
    shots, sounds = parse(a.log)
    if not shots:
        sys.exit("no `shot` lines: film with RH_PRESENT_TRACE=1")
    steps = sorted(shots)
    gaps = np.diff([shots[i] for i in steps]) if len(steps) > 1 else np.array([1000 / 60])
    frame_ms = float(np.mean(gaps))
    fps = 1000.0 / frame_ms / a.slow
    x, y, w, h = (int(v) for v in a.crop.split(","))
    # EVERY FRAME ON THE TRACE CLOCK: a capture's shots are not evenly spaced (16-50 ms), and a constant input rate (their
    # mean) drifted the picture up to ~60 ms against the sound, which IS on the trace clock -- each shot lasts until the
    # next one's clock (concat durations), resampled to an even 60 fps
    listing = tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False, encoding="utf-8")
    for j, i in enumerate(steps):
        dur = (shots[steps[j + 1]] - shots[i] if j + 1 < len(steps) else frame_ms) / 1000.0 * a.slow
        # 'option framerate 1000': the image demuxer's default 1/25 s timebase rounds every duration to 40 ms and
        # drops half the shots (the fold and shut frames among them); a millisecond timebase keeps each on its clock
        listing.write(f"file '{os.path.abspath(a.prefix + f'_{i:02d}.png').replace(chr(92), '/')}'\noption framerate 1000\nduration {dur:.6f}\n")
    listing.write(f"file '{os.path.abspath(a.prefix + f'_{steps[-1]:02d}.png').replace(chr(92), '/')}'\noption framerate 1000\n")
    listing.close()
    cmd = [ffmpeg(), "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listing.name]
    tmp = None
    if not a.mute:
        tmp = tempfile.NamedTemporaryFile(suffix=".wav", delete=False).name
        track = render(shots, sounds, a.slow, a.music)
        write_wav(tmp, track)
        if a.wav:
            write_wav(a.wav, track)
        cmd += ["-i", tmp, "-c:a", "aac", "-b:a", "192k", "-shortest"]
    cmd += ["-vf", f"fps=60,crop={w}:{h}:{x}:{y}", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", a.out]
    subprocess.run(cmd, check=True)
    os.unlink(listing.name)
    if tmp:
        os.unlink(tmp)
    heard = sum(1 for c, *_ in sounds if shots[steps[0]] <= c <= shots[steps[-1]])
    print(f"{a.out}: {len(steps)} frames on the trace clock (their mean {frame_ms:.1f} ms of game each), at 60 fps, "
          f"{'no audio' if a.mute else f'{heard} sound asks in the window'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
