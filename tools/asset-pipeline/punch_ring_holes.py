#!/usr/bin/env python3
"""Punch the FINGER HOLE out of a ring sprite that ships with it painted in.

WHY THIS EXISTS
    Every ring in `assets/art/ItemsLoot/traits/ring` arrived with the one part of the
    sprite that has to be see-through — the hole the finger goes through — filled with
    a flat opaque white or grey. On the paper doll, in the bag and on the item card
    that reads as a white blob stuck to the item, which is the whole of the playtest
    note "the item art needs transparent backgrounds, especially the rings". Every
    other folder under ItemsLoot is clean; this was only ever the rings.

WHY IT IS NOT A COLOUR TEST
    A bright highlight inside a GEM is also a pale enclosed blob, and clearing one
    cuts a window through the stone — which is exactly what the first version of this
    script did to WARDING. Three facts separate a hole from a highlight, and all three
    have to hold:

      * the blob is FLAT — a painted fill has no gradient (value spread <= 0.15)
      * the blob is UNSATURATED — grey through white, never tinted (sat <= 0.06)
      * what SURROUNDS it is dark metal — the band (mean value <= 0.15).
        This is the one that does the work: a gem highlight is ringed by bright
        colour (WARDING's reads 0.95), a finger hole by the band (0.04 - 0.10).

    Run with --dry first. It prints every candidate and its three numbers, so a blob
    that is kept or punched can be argued with rather than trusted.

USAGE
    python tools/asset-pipeline/punch_ring_holes.py [dir] [--dry]

    Idempotent: a sprite whose hole is already transparent has no candidate blob and
    is left untouched, so re-running after new ring art only fixes the new ones.
"""
from PIL import Image
import colorsys
import glob
import os
import sys
from collections import deque

MIN_HOLE = 300          # px; smaller enclosed fills are detail, not a hole
MAX_BLOB_SAT = 0.06     # grey through white
MAX_BLOB_SPREAD = 0.15  # a painted fill has no gradient
MAX_RING_VAL = 0.15     # what surrounds a hole is the band, and the band is dark

NB4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
NB8 = NB4 + ((1, 1), (1, -1), (-1, 1), (-1, -1))


def flat_fill(p):
    """Opaque, and grey through white — the two ways this art paints a filled hole."""
    r, g, b, a = p
    return a > 200 and (max(r, g, b) - min(r, g, b)) <= 30 and min(r, g, b) >= 90


def hsv(p):
    r, g, b, _ = p
    return colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)


def enclosed_blobs(im):
    """Every flat-fill region that does not touch the image border, largest first."""
    w, h = im.size
    px = im.load()
    seen = [[False] * h for _ in range(w)]
    found = []
    for x in range(w):
        for y in range(h):
            if seen[x][y] or not flat_fill(px[x, y]):
                continue
            queue = deque([(x, y)])
            seen[x][y] = True
            cells, touches = [], False
            while queue:
                cx, cy = queue.popleft()
                cells.append((cx, cy))
                if cx == 0 or cy == 0 or cx == w - 1 or cy == h - 1:
                    touches = True
                for dx, dy in NB4:
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and flat_fill(px[nx, ny]):
                        seen[nx][ny] = True
                        queue.append((nx, ny))
            if not touches and len(cells) >= MIN_HOLE:
                found.append(cells)
    found.sort(key=lambda c: -len(c))
    return found


def measure(im, cells):
    """(blob saturation, blob value spread, surrounding value) for one candidate."""
    w, h = im.size
    px = im.load()
    inside = set(cells)
    ring = set()
    for (x, y) in cells:
        for dx, dy in NB8:
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in inside and px[nx, ny][3] > 200:
                ring.add((nx, ny))
    blob = [hsv(px[c]) for c in cells]
    around = [hsv(px[c]) for c in ring] or [(0.0, 0.0, 1.0)]
    values = [v[2] for v in blob]
    return (sum(v[1] for v in blob) / len(blob),
            max(values) - min(values),
            sum(v[2] for v in around) / len(around))


def punch(path, write=True, verbose=True):
    im = Image.open(path).convert('RGBA')
    w, h = im.size
    px = im.load()
    cleared_total = 0
    for cells in enclosed_blobs(im):
        blob_sat, spread, ring_val = measure(im, cells)
        is_hole = blob_sat <= MAX_BLOB_SAT and spread <= MAX_BLOB_SPREAD and ring_val <= MAX_RING_VAL
        if verbose:
            print('    %5d px  sat=%.3f spread=%.2f surround=%.2f  ->  %s'
                  % (len(cells), blob_sat, spread, ring_val, 'PUNCHED' if is_hole else 'kept'))
        if not is_hole:
            continue

        # The fill's own mean colour, so the antialiased rim around it is cleared by
        # LIKENESS rather than by a fixed brightness — a white hole and a grey one leave
        # different fringes behind.
        mean = [0, 0, 0]
        for (x, y) in cells:
            p = px[x, y]
            mean = [mean[0] + p[0], mean[1] + p[1], mean[2] + p[2]]
        mean = [c / len(cells) for c in mean]

        def like(p):
            return p[3] > 0 and max(abs(p[0] - mean[0]), abs(p[1] - mean[1]), abs(p[2] - mean[2])) <= 58

        cleared = set(cells)
        for (x, y) in cells:
            px[x, y] = (0, 0, 0, 0)
        for _ in range(2):
            rim = []
            for (x, y) in cleared:
                for dx, dy in NB8:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in cleared and like(px[nx, ny]):
                        rim.append((nx, ny))
            for (x, y) in rim:
                px[x, y] = (0, 0, 0, 0)
                cleared.add((x, y))
        cleared_total += len(cleared)

    if write and cleared_total:
        im.save(path)
    return cleared_total


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    root = args[0] if args else 'assets/art/ItemsLoot/traits/ring'
    write = '--dry' not in sys.argv
    changed = 0
    for path in sorted(glob.glob(os.path.join(root, '*.png'))):
        print(os.path.basename(path))
        n = punch(path, write=write)
        if n:
            changed += 1
            print('  -> %d px cleared%s' % (n, '' if write else ' (dry run)'))
    print('%d sprite(s) %s' % (changed, 'rewritten' if write else 'would change'))


if __name__ == '__main__':
    main()
