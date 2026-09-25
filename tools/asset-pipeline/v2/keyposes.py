#!/usr/bin/env python3
"""keyposes -- assemble an AUTHORED action clip from controlled key poses (ADR-011).

    python tools/asset-pipeline/v2/keyposes.py seeker_spray
    python tools/asset-pipeline/v2/keyposes.py seeker_hard_hands

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
    and the hand sockets. The durations are the animator's, not the strip's frame count;
  * for a pass the generator drew on a LIGHT ground ("ground": "light", HARD HANDS): the ground keyed from the
    border, and the blanks it painted in the ground's own colour (his face, his sleeves) repaired from the
    reference itself (fix_ground_colours); the accepted sources cached in keypose_sources/<job>/ so the strip
    can be rebuilt after PixelLab's links expire.

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

# HARD HANDS: the half-rise job's frame used as the recovery (its head sits midway between the follow-through's and
# the idle's, so the rise is two even steps, not one jump)
HALF_RISE = 1
# HARD HANDS timing (2026-09-25). Contact is frame 4, 380 ms in. The leap (f3) is the lunge: 120 ms for the body to
# cross to the target, accelerating into the blow. The contact and the follow-through (f4-f5) are PROTECTED: the blow
# is seen landing and the weight driving through, whatever the tempo. Only the half-rise and the exit (f6-f7) are
# recovery a handoff may compress, and the exit IS the idle's first frame, so there is nothing to hide.
HH_TIMING = {
    "frameMs": [70, 100, 90, 120, 80, 90, 120, 100],
    "elastic": [True, True, False, False, False, False, True, True],
    "markers": {"anticipation": 1, "commit": 3, "contact": 4, "recovery": 6, "settle": 7},
    # the striking fist: [x, y] as fractions of the 512 frame (the fist's striking edge, measured on the assembled
    # strip), then the force's direction in screen degrees (the forearm's line: over the top on the leap, driving
    # down-forward on the contact and through it)
    "sockets": {"3": {"StrikeHand": [0.764, 0.291, -22]},
                "4": {"StrikeHand": [0.813, 0.711, 54]},
                "5": {"StrikeHand": [0.824, 0.775, 56]}},
}

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
    # THE SEEKER's HARD HANDS (2026-09-25): a leaping overhand HAMMER-FIST. Skeletons are recorded in
    # keypose_skeletons/seeker_hard_hands.json; the accepted sources are cached in keypose_sources/ so the strip
    # can be rebuilt after PixelLab's links expire (SPRAY's already have).
    "seeker_hard_hands": {
        "strip": "assets/art/Animations/Roster/seeker_strike/char_seeker_strike_strip8_512.png",
        "character": "22e6d4d3-a470-462c-a39c-bfb67315e08a",
        "idle_anim": "27373220-70eb-4ba3-abc0-29c1ac50c9ad",
        "idle_strip": "assets/art/Animations/Roster/seeker_idle/char_seeker_idle_strip8_512.png",
        "skeletons": "keypose_skeletons/seeker_hard_hands.json",
        "jobs": {
            "09f4ba84-9fac-436c-b3e7-966723ca1f37": "REJECTED (3 gens): pass A, the eight hammer-fist skeletons. Good poses, but the face came out a "
                                                    "blank WHITE MASK in every frame (identity drift) and the generator chose a light blue-grey ground.",
            "8e439cfb-631f-4692-a2a5-64599c70b182": "ACCEPTED with deterministic fixes (3 gens): pass B, same skeletons, 'pure black background' asked. "
                                                    "It again drew a light ground (two frames white) and a blank white face: every other colour is "
                                                    "the reference's own palette. Fixed here, not regenerated: the ground keyed from the border, the "
                                                    "face TRANSPLANTED from the reference idle frame into the head box (placed by the skeleton's "
                                                    "nose joint), and white elsewhere (sleeve highlights on the white-ground frames) set to his "
                                                    "sleeve's lightest grey. Frames 0-5 used; its upright frame 6 dropped (see the next job).",
            "e21c6425-b733-45b5-a699-d140d549c013": "USED FOR FRAME 6 (2 gens): three half-rise skeletons between the low follow-through and "
                                                    "idle. Pass B went from its lowest pose straight to upright (the head ~20 source px in one "
                                                    "frame, the 'crouch -> instant standing' the brief forbids); the half-rise bridges it.",
        },
        # (source, index, shift_y): pass B 0-5, the half-rise, then the idle's own frame 0 (the exit pose). No shift:
        # the ready pose's boots land on the idle frame's exact pixels (measured on the strip).
        "frames": [("8e439cfb-631f-4692-a2a5-64599c70b182", 0, 0),   # ready: the guard, fists up
                   ("8e439cfb-631f-4692-a2a5-64599c70b182", 1, 0),   # anticipation: weight back, the fist drawn up
                   ("8e439cfb-631f-4692-a2a5-64599c70b182", 2, 0),   # loaded: the fist cocked overhead (held)
                   ("8e439cfb-631f-4692-a2a5-64599c70b182", 3, 0),   # commit: the leap
                   ("8e439cfb-631f-4692-a2a5-64599c70b182", 4, 0),   # CONTACT: the fist driven low into the target
                   ("8e439cfb-631f-4692-a2a5-64599c70b182", 5, 0),   # follow-through: driven through, lowest
                   ("e21c6425-b733-45b5-a699-d140d549c013", HALF_RISE, 0),  # recovery: the half-rise, pushing back up
                   ("idle", 0, 0)],                                    # exit: exactly the idle's first frame
        "ground": "light",
        # the skeleton frame each strip frame was posed from (its nose places the face transplant)
        "posed_as": {0: ("pass", 0), 1: ("pass", 1), 2: ("pass", 2), 3: ("pass", 3), 4: ("pass", 4), 5: ("pass", 5),
                     6: ("halfrise", HALF_RISE)},
        "timing": HH_TIMING,
    },
}

DARK = 10


def fetch(url: str) -> Image.Image:
    data = subprocess.run(["curl", "-s", "--retry", "3", url], check=True, capture_output=True).stdout
    return Image.open(io.BytesIO(data)).convert("RGBA")


SOURCES = os.path.join(HERE, "keypose_sources")


def fetch_frame(job: str, i: int) -> Image.Image:
    """A generated key-pose frame, from the repo's cache (keypose_sources/<job>/<i>.png) or PixelLab once."""
    path = os.path.join(SOURCES, job, f"{i}.png")
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        fetch(IMG.format(job=job, i=i)).save(path)
    return Image.open(path).convert("RGBA")


def key_light(im: Image.Image) -> Image.Image:
    """The generator's LIGHT ground (blue-grey, or white) flooded from the border; the figure's own colours kept."""
    w, h = im.size
    px = im.load()
    corner = px[0, 0][:3]
    ground = lambda p: sum(abs(a - b) for a, b in zip(p[:3], corner)) <= 24 or min(p[:3]) >= 238
    bg = [[False] * w for _ in range(h)]
    q = deque((x, y) for x in range(w) for y in (0, h - 1) if ground(px[x, y]))
    q.extend((x, y) for y in range(h) for x in (0, w - 1) if ground(px[x, y]))
    for x, y in q: bg[y][x] = True
    while q:
        x, y = q.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not bg[ny][nx] and ground(px[nx, ny]):
                bg[ny][nx] = True; q.append((nx, ny))
    out = im.copy()
    op = out.load()
    for y in range(h):
        for x in range(w):
            if bg[y][x]: op[x, y] = (0, 0, 0, 0)
    return out


SKIN = {(237, 171, 131), (229, 158, 121), (225, 139, 106), (200, 103, 95), (193, 117, 87), (152, 75, 66)}
SLEEVE_LIGHT = (186, 179, 150)   # his sleeve's lightest grey in the reference
OUTLINE = (12, 7, 15)            # his outline in the reference (every silhouette pixel there is near-black)
GROUND_IN_PALETTE = (186, 205, 220)   # the generator's light ground: also six glint pixels of his own palette


def fix_ground_colours(im: Image.Image, reference: Image.Image, nose: tuple[float, float]) -> Image.Image:
    """
    Undo what the generator's LIGHT ground did to the figure, deterministically, keeping every pose pixel:

      * blank areas: pure white, and the ground's own colour enclosed inside the figure (that colour is in his
        palette, six pixels of glint, so the generator used it to FILL his sleeves and face; the border flood cannot
        reach an enclosed area). The one blank region nearest the pose's nose joint, inside a head box around it, is
        his FACE: the reference's own face is transplanted there, aligned on the region's right edge and top (he
        faces right; the hood frames the face on the left). Every other blank is a sleeve (a raised arm crosses the
        head box on the leap, so the box alone painted a patch of sleeve as skin): his sleeve's lightest grey.
      * the outline: drawn against a light ground, the silhouette's edge pixels came out light grey, a pale halo at
        play size. The reference's outline is near-black on every edge pixel, so every light edge pixel becomes his
        outline colour.
    """
    out = im.copy()
    px = out.load()
    w, h = out.size
    nx, ny = nose[0] * w, nose[1] * h
    in_head = lambda x, y: nx - 11 <= x <= nx + 6 and ny - 8 <= y <= ny + 8
    blank = lambda p: min(p[:3]) >= 238 or p[:3] == GROUND_IN_PALETTE
    white = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0 and blank(px[x, y])]
    regions, seen = [], set()
    for start in white:
        if start in seen or not in_head(*start):
            continue
        region, q = [], deque([start])
        seen.add(start)
        while q:
            x, y = q.popleft()
            region.append((x, y))
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n not in seen and in_head(*n) and 0 <= n[0] < w and 0 <= n[1] < h and px[n][3] > 0 and blank(px[n]):
                    seen.add(n); q.append(n)
        regions.append(region)
    near = lambda r: min((x - nx) ** 2 + (y - ny) ** 2 for x, y in r)
    face = min(regions, key=near) if regions else []
    face_set = set(face)
    for x, y in white:
        if (x, y) not in face_set: px[x, y] = SLEEVE_LIGHT + (255,)
    if face:
        rp = reference.load()
        skin = [(x, y) for y in range(reference.height // 2) for x in range(reference.width) if rp[x, y][:3] in SKIN]
        rx0, ry0 = min(x for x, _ in skin), min(y for _, y in skin)
        rx1, ry1 = max(x for x, _ in skin), max(y for _, y in skin)
        dx, dy = rx1 - max(x for x, _ in face), ry0 - min(y for _, y in face)
        for x, y in face:
            sx, sy = x + dx, y + dy
            src = rp[sx, sy] if 0 <= sx < reference.width and 0 <= sy < reference.height else (0, 0, 0, 0)
            feature = rx0 <= sx <= rx1 and ry0 <= sy <= ry1 and src[3] > 0 and max(src[:3]) < 60
            px[x, y] = (src[:3] if src[:3] in SKIN or feature else (229, 158, 121)) + (255,)
    edge = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0 and max(px[x, y][:3]) > 60
            and any(not (0 <= x + dx < w and 0 <= y + dy < h) or px[x + dx, y + dy][3] == 0
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in edge:
        px[x, y] = OUTLINE + (255,)
    return out


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
    noses = {}
    if "skeletons" in a:
        sk = json.load(open(os.path.join(HERE, a["skeletons"]), encoding="utf-8"))
        sets = {"pass": list(sk["frames"].values()), "halfrise": list(sk["halfrise"].values())}
        noses = {i: tuple(sets[kind][n]["NOSE"]) for i, (kind, n) in a.get("posed_as", {}).items()}
    strip = Image.new("RGBA", (512 * len(a["frames"]), 512))
    for i, (src, idx, shift) in enumerate(a["frames"]):
        if src == "idle":
            im = fetch(CHAR.format(char=a["character"], anim=a["idle_anim"], i=idx))
        else:
            key = (src, idx)
            if key not in cache:
                if a.get("ground") == "light":
                    cache[key] = fix_ground_colours(key_light(fetch_frame(src, idx)), idle_src, noses[i]) if i in noses \
                        else key_light(fetch_frame(src, idx))
                else:
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
