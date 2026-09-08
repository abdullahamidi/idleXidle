"""Measure where a frame texture's ornaments are, so the slicer never stretches them.

    python tools/measure_frames.py [png ...]          (no args: every panel, bar and button frame)

For each axis the script reads the edge band, elects the PLAIN rail (the medoid of a sample of
column profiles), and reports the corner ornament's extent from each edge and the centre ornament's
span. This is the reference implementation of UiKit.Measure / UiKit.FrameSpec — keep the two in step:
the game measures the same thing at runtime, and `RH_UI_FRAMES=1` prints what it measured.
"""
import os
import struct
import sys
import zlib


def read_png(path):
    with open(path, 'rb') as f:
        data = f.read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos = 8
    w = h = None
    idat = b''
    bitdepth = colortype = None
    palette = None
    while pos < len(data):
        n = struct.unpack('>I', data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + n]
        if typ == b'IHDR':
            w, h, bitdepth, colortype = struct.unpack('>IIBB', body[:10])
        elif typ == b'IDAT':
            idat += body
        elif typ == b'PLTE':
            palette = body
        pos += 12 + n
    raw = zlib.decompress(idat)
    bpp = {6: 4, 2: 3, 3: 1, 4: 2}.get(colortype, 1)
    assert bitdepth == 8, f'{path}: bitdepth {bitdepth}'
    stride = w * bpp
    px = []
    prev = bytearray(stride)
    i = 0
    for _ in range(h):
        flt = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if flt == 1:
                line[x] = (line[x] + a) & 255
            elif flt == 2:
                line[x] = (line[x] + b) & 255
            elif flt == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif flt == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        prev = line
        row = []
        for x in range(w):
            if colortype == 6:
                r, g, b_, a_ = line[x * 4:x * 4 + 4]
            elif colortype == 2:
                r, g, b_ = line[x * 3:x * 3 + 3]; a_ = 255
            elif colortype == 3:
                idx = line[x]; r, g, b_ = palette[idx * 3:idx * 3 + 3]; a_ = 255
            elif colortype == 4:
                r = g = b_ = line[x * 2]; a_ = line[x * 2 + 1]
            else:
                r = g = b_ = line[x]; a_ = 255
            row.append((r, g, b_, a_))
        px.append(row)
    return w, h, px


def profiles(w, h, px, band, horizontal):
    """Per column (or row): (alpha, luma) down the band, smoothed over the neighbouring column each side."""
    n = w if horizontal else h
    out = []
    for i in range(n):
        col = []
        for k in range(band):
            a = l = c = 0
            for d in (-1, 0, 1):
                j = i + d
                if j < 0 or j >= n:
                    continue
                r, g, b, al = px[k][j] if horizontal else px[j][k]
                a += al; l += (r * 3 + g * 6 + b) // 10; c += 1
            col.append((a // c, l // c))
        out.append(col)
    return out


def ndiff(p, q):
    return sum(1 for (a1, l1), (a2, l2) in zip(p, q) if abs(l1 - l2) > 48 or abs(a1 - a2) > 64)


def measure_axis(w, h, px, band, horizontal):
    """(corner, orn0, orn1, mask) — corner -1 when no plain run exists (the game falls back to 40 px)."""
    profs = profiles(w, h, px, band, horizontal)
    n = len(profs)
    sample = list(range(0, n, max(1, n // 64)))
    best, ref = None, 0
    for i in sample:
        s = sum(ndiff(profs[i], profs[j]) for j in sample)
        if best is None or s < best:
            best, ref = s, i
    limit = max(2, round(band * 0.10))
    plain = [ndiff(p, profs[ref]) <= limit for p in profs]

    def corner_from(left):
        run = 0
        for step in range(n):
            i = step if left else n - 1 - step
            run = run + 1 if plain[i] else 0
            if run >= 4:
                return step - 3
        return n
    corner = max(corner_from(True), corner_from(False))
    if corner >= n // 2 - 2:
        return -1, n // 2, n // 2, ''.join('.' if p else '#' for p in plain)
    span = (0, n // 2, n // 2)
    i = corner
    while i < n - corner:
        if plain[i]:
            i += 1
            continue
        j = i
        while j < n - corner and not plain[j]:
            j += 1
        if j - i > span[0]:
            span = (j - i, i, j)
        i = j
    o0, o1 = (span[1], span[2]) if span[0] >= 3 else (n // 2, n // 2)
    return corner, o0, o1, ''.join('.' if p else '#' for p in plain)


def measure(path):
    w, h, px = read_png(path)
    band_y = min(h, max(12, h // 4))
    strip = w >= 2 * h or h <= band_y * 2 + 8
    cx, ox0, ox1, mx = measure_axis(w, h, px, band_y, True)
    if strip:
        return w, h, cx, ox0, ox1, h, h // 2, h // 2, mx, ''
    band_x = min(w, max(12, w // 4))
    cy, oy0, oy1, my = measure_axis(w, h, px, band_x, False)
    return w, h, cx, ox0, ox1, cy, oy0, oy1, mx, my


def main(paths, verbose=False):
    for p in paths:
        w, h, cx, ox0, ox1, cy, oy0, oy1, mx, my = measure(p)
        print(f'{os.path.basename(p):30s} {w}x{h} corner={cx}x{cy} top=[{ox0},{ox1}) side=[{oy0},{oy1})')
        if verbose:
            print('   x:', mx)
            if my:
                print('   y:', my)


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if a != '-v']
    verbose = '-v' in sys.argv
    if not args:
        root = 'assets/art/UI'
        args = ([os.path.join(root, 'panels', f) for f in sorted(os.listdir(os.path.join(root, 'panels')))]
                + [os.path.join(root, 'bars', f) for f in sorted(os.listdir(os.path.join(root, 'bars'))) if 'frame' in f]
                + [os.path.join(root, 'buttons', f) for f in sorted(os.listdir(os.path.join(root, 'buttons')))
                   if f.startswith(('ui_button_', 'ui_tab_')) and 'icon' not in f])
    main(args, verbose)
