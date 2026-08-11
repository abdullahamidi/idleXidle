"""Background knockout and canvas normalisation for generated sprites.

PixelLab returns fully opaque images even when ``no_background=True`` is
requested — verified by byte-level decode: every pixel comes back alpha 255 on a
flat background field. The subject is drawn over a single uniform colour, so the
background is recoverable exactly rather than approximately.

Knockout is a **border flood-fill**, not a chroma-key. That distinction matters:
the background colour also occurs *inside* the subject (112 px in the reference
probe). A chroma-key would punch holes through those; a flood-fill seeded from
the canvas border can only reach pixels actually connected to the outside.

Alpha is written **straight, not premultiplied** — ``AssetLibrary.Premultiply()``
does that at load time, and premultiplying here would double-apply it.
"""

from __future__ import annotations

from collections import deque

from pixelpng import Image

# Generated pixel art has hard edges, so the background field is near-uniform.
# A small tolerance absorbs the generator's dithering without bleeding into the
# subject's own light tones.
DEFAULT_TOLERANCE = 12


def _close(a: tuple[int, int, int], b: tuple[int, int, int], tol: int) -> bool:
    return abs(a[0] - b[0]) <= tol and abs(a[1] - b[1]) <= tol and abs(a[2] - b[2]) <= tol


def detect_background(img: Image) -> tuple[int, int, int]:
    """Most common colour along the canvas border — the background field."""
    counts: dict[tuple[int, int, int], int] = {}
    for x in range(img.w):
        for y in (0, img.h - 1):
            c = img.get(x, y)[:3]
            counts[c] = counts.get(c, 0) + 1
    for y in range(img.h):
        for x in (0, img.w - 1):
            c = img.get(x, y)[:3]
            counts[c] = counts.get(c, 0) + 1
    return max(counts.items(), key=lambda kv: kv[1])[0]


def knockout(
    img: Image,
    tolerance: int = DEFAULT_TOLERANCE,
    bg: tuple[int, int, int] | None = None,
) -> tuple[Image, dict]:
    """Make the border-connected background transparent.

    Returns the modified image and a stats dict. The image is modified in place
    and also returned for convenience.
    """
    if bg is None:
        bg = detect_background(img)

    w, h = img.w, img.h
    seen = bytearray(w * h)
    q: deque[tuple[int, int]] = deque()

    def seed(x: int, y: int) -> None:
        i = y * w + x
        if not seen[i] and _close(img.get(x, y)[:3], bg, tolerance):
            seen[i] = 1
            q.append((x, y))

    for x in range(w):
        seed(x, 0)
        seed(x, h - 1)
    for y in range(h):
        seed(0, y)
        seed(w - 1, y)

    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h:
                seed(nx, ny)

    cleared = 0
    for i in range(w * h):
        if seen[i]:
            img.px[i * 4 + 3] = 0
            cleared += 1

    # Pixels matching the background colour that survived are interior — the
    # flood fill could not reach them. Report them so a bad tolerance is visible
    # rather than silent.
    interior = sum(
        1
        for i in range(w * h)
        if not seen[i] and _close((img.px[i * 4], img.px[i * 4 + 1], img.px[i * 4 + 2]), bg, tolerance)
    )

    return img, {
        "background": "#%02X%02X%02X" % bg,
        "cleared_px": cleared,
        "cleared_pct": round(100.0 * cleared / (w * h), 1),
        "interior_bg_px": interior,
    }


def content_bbox(img: Image, alpha_floor: int = 8) -> tuple[int, int, int, int] | None:
    """Bounding box (x0, y0, x1, y1) inclusive of pixels above ``alpha_floor``."""
    x0, y0, x1, y1 = img.w, img.h, -1, -1
    for y in range(img.h):
        row = y * img.w
        for x in range(img.w):
            if img.px[(row + x) * 4 + 3] > alpha_floor:
                if x < x0:
                    x0 = x
                if x > x1:
                    x1 = x
                if y < y0:
                    y0 = y
                if y > y1:
                    y1 = y
    return None if x1 < 0 else (x0, y0, x1, y1)


def trim_and_center(img: Image, size: int, pad: int = 2) -> Image:
    """Crop to content, then centre it on a square ``size`` canvas.

    Keeps a uniform margin so icons drawn at different scales share an optical
    weight, and guarantees the output canvas is exactly the size the runtime
    expects. Downscaling is nearest-neighbour to preserve hard pixel edges.
    """
    box = content_bbox(img)
    if box is None:
        return Image(size, size)
    x0, y0, x1, y1 = box
    cw, ch = x1 - x0 + 1, y1 - y0 + 1

    budget = size - 2 * pad
    scale = min(budget / cw, budget / ch, 1.0)
    dw, dh = max(1, round(cw * scale)), max(1, round(ch * scale))

    out = Image(size, size)
    ox, oy = (size - dw) // 2, (size - dh) // 2
    for y in range(dh):
        sy = y0 + min(ch - 1, int(y / scale)) if scale < 1.0 else y0 + y
        for x in range(dw):
            sx = x0 + min(cw - 1, int(x / scale)) if scale < 1.0 else x0 + x
            r, g, b, a = img.get(sx, sy)
            out.set(ox + x, oy + y, r, g, b, a)
    return out


def upscale_integer(img: Image, factor: int) -> Image:
    """Replicate each pixel ``factor`` times per axis.

    An integer nearest-neighbour upscale is visually lossless on pixel art —
    every source pixel becomes an exact NxN block with no new colours and no
    resampled edges. This is how a 640x360 generation becomes a true 1920x1080
    background that the renderer then draws 1:1 into its render target, so
    LinearClamp never gets the chance to soften it.
    """
    out = Image(img.w * factor, img.h * factor)
    for y in range(img.h):
        for x in range(img.w):
            r, g, b, a = img.get(x, y)
            for dy in range(factor):
                base = ((y * factor + dy) * out.w + x * factor) * 4
                for dx in range(factor):
                    o = base + dx * 4
                    out.px[o] = r
                    out.px[o + 1] = g
                    out.px[o + 2] = b
                    out.px[o + 3] = a
    return out


def fit_canvas(img: Image, width: int, height: int) -> Image:
    """Crop to content and fit it into a ``width`` x ``height`` canvas.

    Unlike :func:`trim_and_center` this preserves a non-square target, which
    matters for stretched UI art such as bar troughs and fills: forcing those
    onto a square canvas leaves most of the image empty and the runtime then
    stretches that emptiness across the widget.
    """
    box = content_bbox(img)
    out = Image(width, height)
    if box is None:
        return out
    x0, y0, x1, y1 = box
    cw, ch = x1 - x0 + 1, y1 - y0 + 1

    # Fill the canvas on both axes; bar art is stretched at draw time anyway, so
    # matching the target aspect exactly is what keeps the trim from distorting.
    for y in range(height):
        sy = y0 + min(ch - 1, int(y * ch / height))
        for x in range(width):
            sx = x0 + min(cw - 1, int(x * cw / width))
            r, g, b, a = img.get(sx, sy)
            out.set(x, y, r, g, b, a)
    return out


def stats(img: Image) -> dict:
    """Summary used by the pipeline's validation gate.

    Uses strided slices and ``bytes.count`` so the work happens in C. The
    per-pixel version took tens of seconds on a 4096x512 animation strip (2M
    pixels) and made the generator look stalled when it was only counting.
    """
    total = img.w * img.h
    alpha = bytes(img.px[3::4])
    transparent = alpha.count(0)
    # Colour variety is only ever used as a sanity signal, so sample rather than
    # walk every pixel of a multi-megapixel strip.
    step = max(1, total // 20000)
    colors = {
        (img.px[i * 4], img.px[i * 4 + 1], img.px[i * 4 + 2])
        for i in range(0, total, step)
        if img.px[i * 4 + 3]
    }
    return {
        "size": f"{img.w}x{img.h}",
        "transparent_pct": round(100.0 * transparent / total, 1),
        "distinct_alphas": len(set(alpha)),
        "distinct_colors": len(colors),
    }
