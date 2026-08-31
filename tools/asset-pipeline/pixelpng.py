"""Minimal dependency-free PNG read/write for the asset pipeline.

Pillow is not available in this environment (no pip, no passwordless apt), and the
asset pipeline only ever needs 8-bit RGBA round-tripping, so a focused reader and
writer are cheaper than vendoring an imaging library.

Supported on read: 8-bit greyscale / RGB / palette / greyscale+alpha / RGBA,
with all five PNG scanline filters. Everything is normalised to RGBA8 so callers
never branch on colour type. Interlaced (Adam7) images are rejected outright
rather than silently mis-decoded.

Written files are always 8-bit RGBA, non-interlaced, filter 0.
"""

from __future__ import annotations

import struct
import zlib

PNG_MAGIC = b"\x89PNG\r\n\x1a\n"

# colour type -> samples per pixel
_CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


class Image:
    """An 8-bit RGBA raster. ``px`` is a bytearray of length ``w * h * 4``."""

    __slots__ = ("w", "h", "px")

    def __init__(self, w: int, h: int, px: bytearray | None = None):
        self.w = w
        self.h = h
        self.px = px if px is not None else bytearray(w * h * 4)

    def idx(self, x: int, y: int) -> int:
        return (y * self.w + x) * 4

    def get(self, x: int, y: int) -> tuple[int, int, int, int]:
        o = self.idx(x, y)
        return self.px[o], self.px[o + 1], self.px[o + 2], self.px[o + 3]

    def set(self, x: int, y: int, r: int, g: int, b: int, a: int) -> None:
        o = self.idx(x, y)
        self.px[o] = r
        self.px[o + 1] = g
        self.px[o + 2] = b
        self.px[o + 3] = a

    def copy(self) -> "Image":
        return Image(self.w, self.h, bytearray(self.px))


def _unfilter(raw: bytes, w: int, h: int, channels: int) -> bytearray:
    """Reverse PNG scanline filtering. Returns h*w*channels bytes."""
    stride = w * channels
    bpp = channels  # 8-bit only, so bytes-per-pixel == channels
    out = bytearray(stride * h)
    prev = bytearray(stride)
    pos = 0
    for y in range(h):
        ftype = raw[pos]
        pos += 1
        line = bytearray(raw[pos : pos + stride])
        pos += stride
        if ftype == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 255
        elif ftype == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif ftype == 3:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 255
        elif ftype == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        elif ftype != 0:
            raise ValueError(f"unknown PNG filter type {ftype} on row {y}")
        out[y * stride : (y + 1) * stride] = line
        prev = line
    return out


def read(path: str) -> Image:
    """Read a PNG into an RGBA8 :class:`Image`."""
    with open(path, "rb") as fh:
        data = fh.read()
    if data[:8] != PNG_MAGIC:
        raise ValueError(f"not a PNG: {path}")

    idat = bytearray()
    plte = b""
    trns = b""
    w = h = depth = ctype = None
    i = 8
    while i < len(data):
        (length,) = struct.unpack(">I", data[i : i + 4])
        ctag = data[i + 4 : i + 8]
        chunk = data[i + 8 : i + 8 + length]
        i += 12 + length
        if ctag == b"IHDR":
            w, h, depth, ctype, _comp, _filt, interlace = struct.unpack(">IIBBBBB", chunk[:13])
            if interlace:
                raise ValueError(f"interlaced PNG unsupported: {path}")
            if depth != 8:
                raise ValueError(f"only 8-bit PNGs supported (got {depth}-bit): {path}")
        elif ctag == b"IDAT":
            idat += chunk
        elif ctag == b"PLTE":
            plte = chunk
        elif ctag == b"tRNS":
            trns = chunk
        elif ctag == b"IEND":
            break

    if w is None:
        raise ValueError(f"missing IHDR: {path}")

    channels = _CHANNELS[ctype]
    flat = _unfilter(zlib.decompress(bytes(idat)), w, h, channels)

    img = Image(w, h)
    out = img.px
    n = w * h
    if ctype == 6:  # RGBA
        out[:] = flat
    elif ctype == 2:  # RGB
        for p in range(n):
            s, d = p * 3, p * 4
            out[d] = flat[s]
            out[d + 1] = flat[s + 1]
            out[d + 2] = flat[s + 2]
            out[d + 3] = 255
    elif ctype == 0:  # greyscale
        for p in range(n):
            v = flat[p]
            d = p * 4
            out[d] = out[d + 1] = out[d + 2] = v
            out[d + 3] = 255
    elif ctype == 4:  # greyscale + alpha
        for p in range(n):
            s, d = p * 2, p * 4
            out[d] = out[d + 1] = out[d + 2] = flat[s]
            out[d + 3] = flat[s + 1]
    elif ctype == 3:  # palette
        for p in range(n):
            pi = flat[p]
            s, d = pi * 3, p * 4
            out[d] = plte[s]
            out[d + 1] = plte[s + 1]
            out[d + 2] = plte[s + 2]
            out[d + 3] = trns[pi] if pi < len(trns) else 255
    else:
        raise ValueError(f"unsupported colour type {ctype}: {path}")
    return img


def write(path: str, img: Image, compress_level: int = 9) -> None:
    """Write an RGBA8 PNG (non-interlaced, filter 0)."""
    stride = img.w * 4
    raw = bytearray()
    for y in range(img.h):
        raw.append(0)  # filter: None
        raw += img.px[y * stride : (y + 1) * stride]

    def chunk(tag: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + tag
            + payload
            + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", img.w, img.h, 8, 6, 0, 0, 0)
    body = (
        PNG_MAGIC
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(bytes(raw), compress_level))
        + chunk(b"IEND", b"")
    )
    with open(path, "wb") as fh:
        fh.write(body)
