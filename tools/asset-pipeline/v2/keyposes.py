#!/usr/bin/env python3
"""keyposes -- assemble an AUTHORED action clip from controlled key poses (ADR-011).

    python tools/asset-pipeline/v2/keyposes.py seeker_spray

An important action is not "eight frames of an attack" from a generator. It is a handful of KEY POSES,
each posed on purpose (PixelLab's animate_with_skeleton_v3: every frame is drawn from ONE reference image of
the champion, so his identity holds, while the pose is exactly the skeleton sent), assembled here with:

  * the champion's OWN idle transform, measured, not re-fitted: the new clip's figure is placed exactly where
    his idle strip's figure is (same scale, same ground line, same top), so the switch from idle into the
    action and back never pops;
  * a transparent key done here, not by the generator: skeleton frames generated with no_background keyed out
    the Seeker's dark cloak (its colour sat next to the generator's own background key), so the accepted pass
    was generated on an opaque black ground and keyed by flooding the border, keeping the largest figure and
    giving back the black outline ring the flood took;
  * a TIMING FILE written beside the strip (<strip>.clip.json): per-frame durations, the key-moment markers
    and the hand sockets. The durations are the animator's, not the strip's frame count.

Every generation, accepted or rejected, is recorded in ACTIONS below with the reason.
"""
from __future__ import annotations

import io
import json
import os
import subprocess
import sys
import urllib.request
from collections import deque

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
IMG = "https://api.pixellab.ai/mcp/images/{job}/download?index={i}"
CHAR = "https://backblaze.pixellab.ai/file/pixellab-characters/a5e233f5-b48a-4bf3-8ed5-ea649a71f38d/{char}/animations/{anim}/south-east/{i}.png"

ACTIONS = {
    "seeker_spray": {
        "strip": "assets/art/Animations/Roster/seeker_projectile/char_seeker_projectile_strip8_512.png",
        "character": "22e6d4d3-a470-462c-a39c-bfb67315e08a",
        "idle_anim": "27373220-70eb-4ba3-abc0-29c1ac50c9ad",       # the SE idle his idle strip is built from
        "idle_strip": "assets/art/Animations/Roster/seeker_idle/char_seeker_idle_strip8_512.png",
        "jobs": {
            "f66bc55f-ffd7-4446-9695-d8c97e43a8b1": "REJECTED (3 gens): skeleton pass A, no_background. Identity and follow-through good, "
                                                    "but the generator keyed the dark cloak out as background (fragments in ready/anticipation/return) "
                                                    "and left a stray blob behind the head on the release.",
            "e7d49c06-789c-4baa-b2d8-e4fe273975e6": "ACCEPTED (3 gens): skeleton pass B, opaque black ground keyed here. Solid cloak in every frame; "
                                                    "frames 0-6 used, frame 7 replaced by the original idle frame 0 for a seamless return.",
            "6b80e1bf-cff1-46d4-82ed-7f845080aeca": "USED FOR THE HAND ONLY (1 gen, edit_image_pixen of pass B #3): opened the release fist into a flicked, "
                                                    "spread hand. The edit also redrew ~7% of the body and painted three knives, so only the hand "
                                                    "region is taken, and the whole hand is recoloured to his glove (the edit drew bare skin).",
            "047b1f4e-1c92-4098-b4fa-27f3e6e18ddf": "USED FOR THE HAND ONLY (1 gen, edit of pass B #4): an open, relaxed follow-through hand. The edit "
                                                    "also redrew the other arm, so only the forward hand region is taken, recoloured the same way.",
        },
        # (source, index, shift_y): the key pose each strip frame comes from
        "frames": [("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 0, -1),   # ready: the hand goes to the belt
                   ("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 1, -1),   # anticipation: the bundle cocked by the shoulder
                   ("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 2, -1),   # extreme anticipation: held, high behind
                   ("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 3, -1),   # release: the arm whipped forward, the hand FLICKED OPEN
                   ("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 4, -1),   # follow-through: the hand still open, the cloak flares after
                   ("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 5, -1),   # overshoot
                   ("e7d49c06-789c-4baa-b2d8-e4fe273975e6", 6, -1),   # recovery: the cloak settling last
                   ("idle", 0, 0)],                                    # return: exactly the idle's first frame
        # HAND PATCHES: (frame index in "frames", edit job, box in source px) — the edit's pixels inside the box
        # replace the key pose's, and every skin pixel of the hand takes his GLOVE: he is gloved in every other frame.
        "patches": [(3, "6b80e1bf-cff1-46d4-82ed-7f845080aeca", (110, 40, 137, 78)),
                    (4, "047b1f4e-1c92-4098-b4fa-27f3e6e18ddf", (116, 72, 137, 95))],
        "timing": {
            # f3 (release) 50 + f4 (follow-through) 230 = the 250 ms flight plus two frames: the open hand is still
            # reaching toward the pack ON the frame the knives land, and the recovery (f5-f7) starts just after.
            # It used to start 67 ms BEFORE the contact (f4 was 120), so the arm was already down when the blades
            # hit. Exactly 250 was tried: the arm then dropped on the contact frame itself. Recovery 140/140/120
            # became 90/90/110, so the clip is exactly as long as before (920 ms) and the tempo budget is unchanged.
            "frameMs": [90, 110, 150, 50, 230, 90, 90, 110],
            "elastic": [True, True, False, False, False, False, True, True],
            "markers": {"anticipation": 1, "commit": 2, "release": 3, "recovery": 6, "settle": 7},
            # the throwing hand on the frames that hold the bundle, and where it lets go:
            # [x, y] as fractions of the 512 frame, then the direction the held blades point (screen degrees)
            "sockets": {"1": {"ThrowHand": [0.368, 0.413, -115]},
                        "2": {"ThrowHand": [0.344, 0.324, -140]},
                        "3": {"ThrowHand": [0.731, 0.395, 4]}},
        },
    },
}

DARK = 10


def fetch(url: str) -> Image.Image:
    data = subprocess.run(["curl", "-s", "--retry", "3", url], check=True, capture_output=True).stdout
    return Image.open(io.BytesIO(data)).convert("RGBA")


def key_black(im: Image.Image) -> Image.Image:
    """Flood the border over near-black, keep the largest figure, give back the outline ring the flood took."""
    w, h = im.size
    px = im.load()
    nb = lambda x, y: max(px[x, y][:3]) <= DARK
    bg = [[False] * w for _ in range(h)]
    q = deque((x, y) for x in range(w) for y in (0, h - 1) if nb(x, y))
    q.extend((x, y) for y in range(h) for x in (0, w - 1) if nb(x, y))
    for x, y in q: bg[y][x] = True
    while q:
        x, y = q.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not bg[ny][nx] and nb(nx, ny):
                bg[ny][nx] = True; q.append((nx, ny))
    seen = [[False] * w for _ in range(h)]
    best: list = []
    for y in range(h):
        for x in range(w):
            if bg[y][x] or seen[y][x]: continue
            comp, qq = [], deque([(x, y)]); seen[y][x] = True
            while qq:
                cx, cy = qq.popleft(); comp.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not bg[ny][nx] and not seen[ny][nx]:
                            seen[ny][nx] = True; qq.append((nx, ny))
            if len(comp) > len(best): best = comp
    keep = [[False] * w for _ in range(h)]
    out = Image.new("RGBA", (w, h))
    op = out.load()
    for x, y in best:
        keep[y][x] = True; op[x, y] = px[x, y]
    for y in range(h):
        for x in range(w):
            if bg[y][x] and not keep[y][x] and any(0 <= x + dx < w and 0 <= y + dy < h and keep[y + dy][x + dx]
                                                   for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                op[x, y] = (px[x, y][0], px[x, y][1], px[x, y][2], 255)
    return out


# His glove, darkest first: the colours his gloved fists use in the key poses (sampled from frame 2), plus one
# highlight a step above his brightest glove pixel so an OPEN hand's fingers still separate. The first polish build
# gloved only the back of the hand, a fingerless glove. Every other frame of his, idle included, shows a full
# glove, so the release hand changed costume for 250 ms. At combat size the gloved fingers still read as a
# spread hand (the outline carries it).
GLOVE = [(63, 33, 34), (83, 47, 37), (102, 54, 51), (126, 65, 57), (148, 84, 66)]


def patch_hand(base: Image.Image, edit: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    """Take the edit's pixels inside `box` onto the approved key pose, and glove the whole hand."""
    out = base.copy()
    x0, y0, x1, y1 = box
    out.paste(edit.crop(box), (x0, y0))
    px = out.load()
    lum = lambda p: 0.3 * p[0] + 0.59 * p[1] + 0.11 * p[2]
    skin = [(x, y) for y in range(y0, y1) for x in range(x0, x1)
            if px[x, y][3] > 0 and px[x, y][0] > 140 and px[x, y][0] > px[x, y][1] > px[x, y][2]]
    if not skin:
        return out
    ls = [lum(px[x, y]) for x, y in skin]
    lo, hi = min(ls), max(ls)
    for (x, y), l in zip(skin, ls):
        t = (l - lo) / max(1e-6, hi - lo)
        px[x, y] = GLOVE[min(len(GLOVE) - 1, int(t * len(GLOVE)))] + (px[x, y][3],)
    return out


def idle_transform(idle_src: Image.Image, idle_strip: Image.Image) -> tuple[int, int, int]:
    """The idle strip's placement of its 180 px source: (scale, offset x, offset y), verified pixel-exact."""
    sb, tb = idle_src.getbbox(), idle_strip.crop((0, 0, 512, 512)).getbbox()
    for k in (3, 2, 4):
        ox, oy = tb[0] - sb[0] * k, tb[1] - sb[1] * k
        c = Image.new("RGBA", (512, 512))
        c.paste(idle_src.resize((idle_src.width * k, idle_src.height * k), Image.NEAREST), (ox, oy))
        if c.tobytes() == idle_strip.crop((0, 0, 512, 512)).tobytes():
            return k, ox, oy
    raise SystemExit("the idle strip is not a plain nearest upscale of its source; refusing to guess its transform")


def main() -> int:
    name = sys.argv[1] if len(sys.argv) > 1 else "seeker_spray"
    a = ACTIONS[name]
    idle_src = fetch(CHAR.format(char=a["character"], anim=a["idle_anim"], i=0))
    idle_strip = Image.open(os.path.join(REPO, a["idle_strip"])).convert("RGBA")
    k, ox, oy = idle_transform(idle_src, idle_strip)
    print(f"idle transform: x{k}, offset ({ox},{oy}) - pixel-exact")
    cache: dict = {}
    patches = {i: (job, box) for i, job, box in a.get("patches", [])}
    strip = Image.new("RGBA", (512 * len(a["frames"]), 512))
    for i, (src, idx, shift) in enumerate(a["frames"]):
        if src == "idle":
            im = fetch(CHAR.format(char=a["character"], anim=a["idle_anim"], i=idx))
        else:
            key = (src, idx)
            if key not in cache:
                cache[key] = key_black(fetch(IMG.format(job=src, i=idx)))
            im = cache[key]
        if i in patches:
            job, box = patches[i]
            im = patch_hand(im, fetch(f"https://api.pixellab.ai/mcp/images/{job}/download"), box)
        up = im.resize((im.width * k, im.height * k), Image.NEAREST)
        frame = Image.new("RGBA", (512, 512))
        frame.paste(up, (ox, oy + shift * k), up)
        strip.paste(frame, (i * 512, 0))
        print(f"  frame {i}: {src[:8]}#{idx} shift {shift} bbox {frame.getbbox()}")
    out = os.path.join(REPO, a["strip"])
    strip.save(out)
    timing_path = out[:-len(".png")] + ".clip.json"
    doc = dict(a["timing"])
    doc["source"] = {"tool": "tools/asset-pipeline/v2/keyposes.py " + name, "jobs": a["jobs"]}
    with open(timing_path, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1)
    print(f"wrote {os.path.relpath(out, REPO)} and {os.path.relpath(timing_path, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
