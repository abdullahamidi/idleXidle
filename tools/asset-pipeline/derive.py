"""Derive asset variants from a base sprite instead of generating them.

Most of this library's assets are transforms of a smaller set of base sprites:
a silhouette is the base filled flat, a portrait is a crop of it, an animation
frame is one column of a strip. Generating each of those separately would cost
hundreds of generations AND lose the guarantee that a creature's silhouette
actually matches its sprite.

Every function here is pure image maths — no API calls, no cost.
"""

from __future__ import annotations

from pixelpng import Image

VOID_INK = (0x1B, 0x16, 0x20)


def _blank(w: int, h: int) -> Image:
    return Image(w, h)


def bbox(img: Image, alpha_floor: int = 8) -> tuple[int, int, int, int] | None:
    """Content bounds.

    Pulls the alpha plane out with a strided slice and scans it with ``max`` /
    ``bytes.find``, both of which run in C. The obvious nested-loop version is
    ~1M interpreter steps on a 1024x1024 boss frame and was slow enough to
    look like a hang.
    """
    w, h = img.w, img.h
    alpha = bytes(img.px[3::4])
    y0 = y1 = -1
    x0, x1 = w, -1
    for y in range(h):
        row = alpha[y * w : (y + 1) * w]
        if max(row) <= alpha_floor:
            continue
        if y0 < 0:
            y0 = y
        y1 = y
        # First/last column above the floor, found without a Python loop.
        lo = 0
        while lo < w and row[lo] <= alpha_floor:
            lo += 1
        hi = w - 1
        while hi >= 0 and row[hi] <= alpha_floor:
            hi -= 1
        if lo < x0:
            x0 = lo
        if hi > x1:
            x1 = hi
    return None if y1 < 0 else (x0, y0, x1, y1)


def _gather(src: Image, out: Image, sx_map: list[int], sy_map: list[int], ox: int = 0, oy: int = 0) -> None:
    """Copy src pixels into out using precomputed index maps.

    Index maths is hoisted out of the inner loop and each pixel is moved as one
    4-byte slice assignment, which keeps the per-pixel work in C.
    """
    spx, opx = src.px, out.px
    sw, ow = src.w, out.w
    for y, sy in enumerate(sy_map):
        srow = sy * sw
        orow = (oy + y) * ow + ox
        for x, sx in enumerate(sx_map):
            s = (srow + sx) * 4
            o = (orow + x) * 4
            opx[o : o + 4] = spx[s : s + 4]


def scale_nearest(img: Image, w: int, h: int) -> Image:
    """Nearest-neighbour resample — keeps pixel-art edges hard at any ratio."""
    out = Image(w, h)
    sx_map = [min(img.w - 1, x * img.w // w) for x in range(w)]
    sy_map = [min(img.h - 1, y * img.h // h) for y in range(h)]
    _gather(img, out, sx_map, sy_map)
    return out


def fit_square(img: Image, size: int, pad_frac: float = 0.06) -> Image:
    """Crop to content and centre it on a square canvas of ``size``.

    This is the `normalized/` convention: gameplay draws from uniform square
    canvases so every creature shares a baseline and ground line.
    """
    box = bbox(img)
    out = _blank(size, size)
    if box is None:
        return out
    x0, y0, x1, y1 = box
    cw, ch = x1 - x0 + 1, y1 - y0 + 1
    budget = int(size * (1 - 2 * pad_frac))
    scale = min(budget / cw, budget / ch)
    dw, dh = max(1, int(cw * scale)), max(1, int(ch * scale))
    ox, oy = (size - dw) // 2, (size - dh) // 2
    sx_map = [x0 + min(cw - 1, int(x / scale)) for x in range(dw)]
    sy_map = [y0 + min(ch - 1, int(y / scale)) for y in range(dh)]
    _gather(img, out, sx_map, sy_map, ox, oy)
    return out


def silhouette(img: Image, size: int | None = None, colour=VOID_INK) -> Image:
    """Flat fill of the subject's shape — used for the targeting/telegraph pass.

    Deriving this guarantees the silhouette matches the sprite exactly, which a
    separately-generated one never would.
    """
    src = img if size is None else fit_square(img, size)
    out = Image(src.w, src.h)
    for i in range(src.w * src.h):
        a = src.px[i * 4 + 3]
        if a > 8:
            out.px[i * 4] = colour[0]
            out.px[i * 4 + 1] = colour[1]
            out.px[i * 4 + 2] = colour[2]
            out.px[i * 4 + 3] = a
    return out


def ground_shadow(img: Image, w: int, h: int, opacity: int = 110) -> Image:
    """A soft elliptical contact shadow scaled to the subject's footprint."""
    out = Image(w, h)
    box = bbox(img)
    if box is None:
        return out
    x0, _y0, x1, _y1 = box
    span = max(1, x1 - x0 + 1) / img.w
    rx = max(2.0, span * w * 0.5 * 0.92)
    ry = max(1.5, rx * 0.30)
    cx, cy = w / 2.0, h / 2.0
    for y in range(h):
        for x in range(w):
            nx = (x + 0.5 - cx) / rx
            ny = (y + 0.5 - cy) / ry
            d = nx * nx + ny * ny
            if d <= 1.0:
                # Fade toward the rim so the contact point reads as the darkest.
                a = int(opacity * (1.0 - d) ** 0.65)
                if a > 0:
                    o = (y * w + x) * 4
                    out.px[o] = VOID_INK[0]
                    out.px[o + 1] = VOID_INK[1]
                    out.px[o + 2] = VOID_INK[2]
                    out.px[o + 3] = min(255, a)
    return out


def portrait(img: Image, w: int, h: int, head_frac: float = 0.46) -> Image:
    """Crop the upper part of the subject and fit it to a portrait canvas."""
    box = bbox(img)
    if box is None:
        return _blank(w, h)
    x0, y0, x1, y1 = box
    ch = y1 - y0 + 1
    crop_h = max(1, int(ch * head_frac))
    cw = x1 - x0 + 1

    # Widen the crop to the target aspect so the head is not distorted.
    want_w = max(1, int(crop_h * (w / h)))
    cx = (x0 + x1) // 2
    sx0 = max(0, cx - want_w // 2)
    sx1 = min(img.w - 1, sx0 + want_w - 1)
    sx0 = max(0, sx1 - want_w + 1)
    if want_w < cw:
        sx0, sx1 = x0, x1

    out = Image(w, h)
    sw = sx1 - sx0 + 1
    sx_map = [sx0 + min(sw - 1, x * sw // w) for x in range(w)]
    sy_map = [y0 + min(crop_h - 1, y * crop_h // h) for y in range(h)]
    _gather(img, out, sx_map, sy_map)
    return out


def slice_strip(strip: Image) -> list[Image]:
    """Split a horizontal strip into its square frames.

    Mirrors the runtime's own decode (`frameCount = width / height`), so if this
    returns the wrong count the renderer would have refused the strip anyway.
    """
    n = strip.w // strip.h
    if n < 1 or strip.w % strip.h:
        raise ValueError(f"strip {strip.w}x{strip.h} is not a whole number of square frames")
    size = strip.h
    frames = []
    for i in range(n):
        cell = Image(size, size)
        for y in range(size):
            src = (y * strip.w + i * size) * 4
            cell.px[y * size * 4 : (y + 1) * size * 4] = strip.px[src : src + size * 4]
        frames.append(cell)
    return frames


def tint(img: Image, colour, strength: float = 1.0) -> Image:
    """Multiply-tint, preserving alpha. Used for state/element recolours."""
    out = Image(img.w, img.h, bytearray(img.px))
    for i in range(img.w * img.h):
        a = out.px[i * 4 + 3]
        if not a:
            continue
        for c in range(3):
            src = out.px[i * 4 + c]
            out.px[i * 4 + c] = int(src * (1 - strength) + src * colour[c] / 255 * strength)
    return out
