"""Compose every store capsule for IDLExIDLE from the shipped title art.

Inputs (all in-repo): the title background (assets/art/Environments/screens/bg_title.png,
1920x1080), the logo lockup (assets/art/BrandingSymbols/branding/logo_idlexidle_full.png), and
the raw emblem (assets/art/BrandingSymbols/branding/preview/emblem_idlexidle.png).

Outputs docs/store/capsules/<name>.png at the sizes Steam and itch.io ask for (checked 2026-08):
  steam_header        920 x 430      steam_small       462 x 174
  steam_main         1232 x 706      steam_vertical    748 x 896
  steam_library_cap   600 x 900      steam_library_hero 3840 x 1240 (art only, no words)
  steam_library_logo 1280 x 720 (transparent, words only)
  steam_page_bg      1438 x 810 (art only, darkened)
  itch_cover          630 x 500      itch_banner       960 x 300

Rule of the composition: the background is cropped to the target aspect around its centre (the
cathedral's vanishing point), darkened a little where words sit, and the lockup is placed at a
width that keeps IDLExIDLE readable at the small sizes. Vertical formats stack the emblem above
the wordmark. Nothing here is hand-drawn; it is layout, so it is a script and re-runs in seconds.

Usage: python tools/marketing/make_capsules.py
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

BG = 'assets/art/Environments/screens/bg_title.png'
LOGO = 'assets/art/BrandingSymbols/branding/logo_idlexidle_full.png'
EMBLEM = 'assets/art/BrandingSymbols/branding/preview/emblem_idlexidle.png'
OUT = 'docs/store/capsules'
FONT = 'assets/fonts/Cinzel.ttf'

GOLD_TOP = (246, 216, 136)
GOLD_BOT = (196, 138, 44)
OUTLINE = (42, 26, 12)

bg = Image.open(BG).convert('RGBA')
logo = Image.open(LOGO).convert('RGBA')
emblem = Image.open(EMBLEM).convert('RGBA')
emblem = emblem.crop(emblem.getbbox())


def cover_crop(src: Image.Image, w: int, h: int, focus=(0.5, 0.42)) -> Image.Image:
    """Scale-to-cover then crop to w x h around a focus point (fractions of the source)."""
    sw, sh = src.size
    scale = max(w / sw, h / sh)
    scaled = src.resize((round(sw * scale), round(sh * scale)), Image.LANCZOS)
    cx, cy = scaled.width * focus[0], scaled.height * focus[1]
    x0 = int(min(max(cx - w / 2, 0), scaled.width - w))
    y0 = int(min(max(cy - h / 2, 0), scaled.height - h))
    return scaled.crop((x0, y0, x0 + w, y0 + h))


def darken(img: Image.Image, amount: float, band=None) -> Image.Image:
    """Multiply the whole image (or a vertical band with soft edges) toward black."""
    w, h = img.size
    mask = Image.new('L', (w, h), int(255 * amount))
    if band is not None:
        y0, y1 = int(h * band[0]), int(h * band[1])
        mask = Image.new('L', (w, h), 0)
        d = ImageDraw.Draw(mask)
        d.rectangle([0, y0, w, y1], fill=int(255 * amount))
        mask = mask.filter(ImageFilter.GaussianBlur(max(8, h // 24)))
    black = Image.new('RGBA', (w, h), (6, 5, 10, 255))
    return Image.composite(black, img, mask)


def fit_width(img: Image.Image, w: int) -> Image.Image:
    h = round(img.height * w / img.width)
    return img.resize((w, h), Image.LANCZOS)


def fit_height(img: Image.Image, h: int) -> Image.Image:
    w = round(img.width * h / img.height)
    return img.resize((w, h), Image.LANCZOS)


def bake_word(word: str, px: int) -> Image.Image:
    font = ImageFont.truetype(FONT, px)
    tmp = ImageDraw.Draw(Image.new('RGBA', (10, 10)))
    x0, y0, x1, y1 = tmp.textbbox((0, 0), word, font=font)
    w, h = x1 - x0, y1 - y0
    pad = max(6, px // 12)
    img = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = max(2, px // 40)
    for dx in range(-r, r + 1):
        for dy in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r + 1:
                d.text((pad - x0 + dx, pad - y0 + dy), word, font=font, fill=OUTLINE)
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).text((pad - x0, pad - y0), word, font=font, fill=255)
    grad = Image.new('RGBA', img.size)
    gd = ImageDraw.Draw(grad)
    for y in range(img.size[1]):
        t = y / max(1, img.size[1] - 1)
        c = tuple(int(GOLD_TOP[i] + (GOLD_BOT[i] - GOLD_TOP[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (img.size[0], y)], fill=c)
    img.paste(grad, (0, 0), mask)
    return img


def stacked_lockup(width: int) -> Image.Image:
    """Big emblem above the wordmark, for tall formats. The wordmark keeps its own small emblem as
    the X between the words — "IDLE IDLE" without it read as two words, not a name."""
    emb = fit_width(emblem, int(width * 0.66))
    # Words: IDLE <mini emblem> IDLE, scaled so the line is ~96% of width
    px = max(24, int(width * 0.26))
    left = bake_word('IDLE', px)
    right = bake_word('IDLE', px)
    mini = fit_height(emblem, int(left.height * 1.15))
    gap = max(4, px // 8)
    line_w = left.width + gap + mini.width + gap + right.width
    if line_w > width * 0.96:
        s = width * 0.96 / line_w
        left = left.resize((round(left.width * s), round(left.height * s)), Image.LANCZOS)
        right = right.resize((round(right.width * s), round(right.height * s)), Image.LANCZOS)
        mini = mini.resize((round(mini.width * s), round(mini.height * s)), Image.LANCZOS)
        gap = round(gap * s)
        line_w = left.width + gap + mini.width + gap + right.width
    line_h = max(left.height, mini.height)
    space = max(8, width // 18)
    canvas = Image.new('RGBA', (width, emb.height + space + line_h), (0, 0, 0, 0))
    canvas.alpha_composite(emb, ((width - emb.width) // 2, 0))
    x = (width - line_w) // 2
    y = emb.height + space
    canvas.alpha_composite(left, (x, y + (line_h - left.height) // 2))
    canvas.alpha_composite(mini, (x + left.width + gap, y + (line_h - mini.height) // 2))
    canvas.alpha_composite(right, (x + left.width + gap + mini.width + gap, y + (line_h - right.height) // 2))
    return canvas


def glow_behind(canvas: Image.Image, art: Image.Image, pos, radius=None):
    r = radius or max(6, art.height // 10)
    g = art.split()[3].filter(ImageFilter.GaussianBlur(r))
    glow = Image.new('RGBA', art.size, (240, 168, 48, 0))
    glow.putalpha(g.point(lambda a: a * 60 // 255))
    canvas.alpha_composite(glow, pos)


def landscape(w: int, h: int, logo_frac: float, logo_y: float, dark=0.35, sub=None) -> Image.Image:
    img = cover_crop(bg, w, h)
    img = darken(img, dark, band=(logo_y - 0.22, logo_y + 0.22))
    lk = fit_width(logo, int(w * logo_frac))
    pos = ((w - lk.width) // 2, int(h * logo_y) - lk.height // 2)
    glow_behind(img, lk, pos)
    img.alpha_composite(lk, pos)
    if sub:
        px = max(14, int(h * 0.055))
        s = bake_word(sub, px)
        img.alpha_composite(s, ((w - s.width) // 2, pos[1] + lk.height + max(6, h // 40)))
    return img


def portrait(w: int, h: int, logo_frac: float, logo_y: float, dark=0.4, sub=None) -> Image.Image:
    img = cover_crop(bg, w, h, focus=(0.5, 0.40))
    img = darken(img, dark, band=(logo_y - 0.25, logo_y + 0.25))
    lk = stacked_lockup(int(w * logo_frac))
    pos = ((w - lk.width) // 2, int(h * logo_y) - lk.height // 2)
    glow_behind(img, lk, pos)
    img.alpha_composite(lk, pos)
    if sub:
        px = max(14, int(w * 0.05))
        s = bake_word(sub, px)
        img.alpha_composite(s, ((w - s.width) // 2, pos[1] + lk.height + max(6, h // 40)))
    return img


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    out = {}
    tag = 'AN IDLE AUTO-BATTLER'
    out['steam_header'] = landscape(920, 430, 0.74, 0.46, sub=tag)
    out['steam_small'] = landscape(462, 174, 0.82, 0.50)
    out['steam_main'] = landscape(1232, 706, 0.70, 0.44, sub=tag)
    out['steam_vertical'] = portrait(748, 896, 0.80, 0.40, sub=tag)
    out['steam_library_cap'] = portrait(600, 900, 0.82, 0.40)
    # Library hero: art only — no words (Steam overlays the logo). Slightly darkened centre-left.
    hero = cover_crop(bg, 3840, 1240, focus=(0.5, 0.45))
    out['steam_library_hero'] = darken(hero, 0.18)
    # Library logo: transparent, words only, 1280x720 — the lockup centred with air around it.
    lg = Image.new('RGBA', (1280, 720), (0, 0, 0, 0))
    lk = fit_width(logo, 1100)
    lg.alpha_composite(lk, ((1280 - lk.width) // 2, (720 - lk.height) // 2))
    out['steam_library_logo'] = lg
    out['steam_page_bg'] = darken(cover_crop(bg, 1438, 810), 0.55)
    out['itch_cover'] = landscape(630, 500, 0.86, 0.44, sub=tag)
    out['itch_banner'] = landscape(960, 300, 0.56, 0.50)
    for name, img in out.items():
        p = os.path.join(OUT, name + '.png')
        img.convert('RGBA').save(p, optimize=True)
        print(f'{name:22s} {img.size[0]}x{img.size[1]}  {os.path.getsize(p) / 1e6:.2f} MB')


if __name__ == '__main__':
    main()
