"""Compose the itch.io page kit for IDLExIDLE from the shipped UI art.

The itch.io page is a rich-text body plus a theme (banner, background, colours). Nothing on it can
be pushed by butler, so this script bakes every IMAGE the page needs and `docs/store/itch/README.md`
says where each one goes. Everything is layout over art the game already ships — the gold nine-slice
frames, the medallion icons, the Cinzel wordmark — so the page looks like the game, not like a
template. Re-runs in seconds.

Outputs docs/store/itch/:
  header.png              1920 x 600   the page banner (emblem, wordmark, tagline) — 2x of itch's 960 column
  background.png          1920 x 1400  fixed page background: void ink with a gold dawn at the top
  loop.png                1200 x 230   FIGHT -> SPEND -> LEAVE, the three-medallion loop strip
  section_<key>.png       1200 x 150   one ornate section header per body section (icon + title + rule)
  classes.png             1200 x 400   the five classes, their colours and their two champions as portraits
  regions.png             1200 x 300   the six regions in order
  forms.png               1200 x 260   the six skill STYLES (drawn with the Form-era icons)
  footer.png              1200 x 170   MADE BY ONE PERSON · OFFLINE · NO ACCOUNT · NO ADS

Usage: python tools/marketing/make_itch_page.py
"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

OUT = 'docs/store/itch'
FONT_TITLE = 'assets/fonts/Cinzel.ttf'
# The page kit's body face is IBM Plex Sans Condensed, which left assets/fonts on 2026-09-09 (b46188a,
# when the game moved to Spectral). A re-bake needs the two files back, e.g.
#   git show b46188a~1:assets/fonts/IBMPlexSansCondensed-SemiBold.ttf > <somewhere>/…SemiBold.ttf
# and FreeType cannot open a path with non-ASCII characters on Windows, so hand ImageFont.truetype the
# bytes (io.BytesIO). The 2026-09-17 alpha re-bake did exactly that for the seven strips it changed.
FONT_BODY = 'assets/fonts/IBMPlexSansCondensed-SemiBold.ttf'
FONT_BODY_REG = 'assets/fonts/IBMPlexSansCondensed-Regular.ttf'

BG_TITLE = 'assets/art/Environments/screens/bg_title.png'
LOGO = 'assets/art/BrandingSymbols/branding/logo_idlexidle_full.png'
EMBLEM = 'assets/art/BrandingSymbols/branding/preview/emblem_idlexidle.png'
PANEL_WIDE = 'assets/art/UI/panels/ui_panel_modal_wide.png'
DIVIDER = 'assets/art/UI/decor/ui_divider_long.png'
MEDALLION = 'assets/art/UI/icons/ui_medallion_round.png'
ICONS = 'assets/art/UI/icons'
CLASS_ICONS = 'assets/art/Characters/Hunter/icons/class'
PORTRAITS = 'assets/art/Characters/Roster/portraits'

# The game's palette (UiKit.cs) — the page must not invent a second one.
VOID = (0x14, 0x10, 0x1A)
INK = (0x1B, 0x16, 0x20)
PANEL = (0x0A, 0x08, 0x10)
VELLUM = (0xED, 0xE3, 0xC8)
VELLUM_DIM = (0xB8, 0xAE, 0x96)
GOLD = (0xF0, 0xA8, 0x30)
GOLD_TOP = (246, 216, 136)
GOLD_BOT = (196, 138, 44)
OUTLINE = (42, 26, 12)
CLASS_COLOURS = {
    'warden': (0xD6, 0x48, 0x5C),
    'ranger': (0x48, 0xB8, 0x88),
    'mystic': (0x74, 0xC6, 0xE8),
    'bulwark': (0xC0, 0x6E, 0xE0),
    'wanderer': (0xB4, 0xB8, 0x62),
}

W = 1200  # every in-body strip is this wide; itch scales it to its column


# ───────────────────────────── primitives ─────────────────────────────

def font(path: str, px: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, px)


def fit_width(img: Image.Image, w: int) -> Image.Image:
    return img.resize((w, round(img.height * w / img.width)), Image.LANCZOS)


def fit_height(img: Image.Image, h: int) -> Image.Image:
    return img.resize((round(img.width * h / img.height), h), Image.LANCZOS)


def fit_box(img: Image.Image, size: int) -> Image.Image:
    img = img.crop(img.getbbox()) if img.getbbox() else img
    s = size / max(img.size)
    return img.resize((max(1, round(img.width * s)), max(1, round(img.height * s))), Image.LANCZOS)


def gold_text(text: str, px: int, path=FONT_TITLE, top=GOLD_TOP, bot=GOLD_BOT, outline=True) -> Image.Image:
    """The capsule wordmark treatment: a vertical gold gradient through the glyphs, dark outline."""
    f = font(path, px)
    tmp = ImageDraw.Draw(Image.new('RGBA', (10, 10)))
    x0, y0, x1, y1 = tmp.textbbox((0, 0), text, font=f)
    w, h = x1 - x0, y1 - y0
    pad = max(4, px // 10)
    img = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if outline:
        r = max(1, px // 36)
        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r + 1:
                    d.text((pad - x0 + dx, pad - y0 + dy), text, font=f, fill=OUTLINE)
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).text((pad - x0, pad - y0), text, font=f, fill=255)
    grad = Image.new('RGBA', img.size)
    gd = ImageDraw.Draw(grad)
    for y in range(img.size[1]):
        t = y / max(1, img.size[1] - 1)
        c = tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (img.size[0], y)], fill=c)
    img.paste(grad, (0, 0), mask)
    return img


def plain_text(text: str, px: int, colour=VELLUM, path=FONT_BODY) -> Image.Image:
    f = font(path, px)
    tmp = ImageDraw.Draw(Image.new('RGBA', (10, 10)))
    x0, y0, x1, y1 = tmp.textbbox((0, 0), text, font=f)
    img = Image.new('RGBA', (x1 - x0 + 4, y1 - y0 + 4), (0, 0, 0, 0))
    ImageDraw.Draw(img).text((2 - x0, 2 - y0), text, font=f, fill=colour + (255,))
    return img


def nine_slice(src: Image.Image, w: int, h: int, corner: int) -> Image.Image:
    """Draw the ornate frame at w x h: corners kept, edges stretched along one axis, centre filled."""
    sw, sh = src.size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    c = corner

    def part(box, dst):
        piece = src.crop(box)
        if piece.size != (dst[2] - dst[0], dst[3] - dst[1]):
            piece = piece.resize((max(1, dst[2] - dst[0]), max(1, dst[3] - dst[1])), Image.LANCZOS)
        out.alpha_composite(piece, (dst[0], dst[1]))

    part((c, c, sw - c, sh - c), (c, c, w - c, h - c))              # centre
    part((0, 0, c, c), (0, 0, c, c))                                # corners
    part((sw - c, 0, sw, c), (w - c, 0, w, c))
    part((0, sh - c, c, sh), (0, h - c, c, h))
    part((sw - c, sh - c, sw, sh), (w - c, h - c, w, h))
    part((c, 0, sw - c, c), (c, 0, w - c, c))                       # edges
    part((c, sh - c, sw - c, sh), (c, h - c, w - c, h))
    part((0, c, c, sh - c), (0, c, c, h - c))
    part((sw - c, c, sw, sh - c), (w - c, c, w, h - c))
    return out


def glow(canvas: Image.Image, art: Image.Image, pos, radius: int, colour=(240, 168, 48), strength=70):
    """A soft colour halo behind art. The blur runs on a canvas padded by 3x the radius so the halo
    fades out on its own — blurring inside the art's own box clips it into a faint square."""
    pad = radius * 3
    a = Image.new('L', (art.width + pad * 2, art.height + pad * 2), 0)
    a.paste(art.split()[3], (pad, pad))
    g = a.filter(ImageFilter.GaussianBlur(radius))
    layer = Image.new('RGBA', a.size, colour + (0,))
    layer.putalpha(g.point(lambda v: v * strength // 255))
    canvas.alpha_composite(layer, (pos[0] - pad, pos[1] - pad))


def medallion_icon(icon: Image.Image, size: int, ring=True) -> Image.Image:
    """An icon on a dark disc inside the gold ring — the game's medallion treatment for flat icons."""
    out = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    inset = size // 14
    d.ellipse([inset, inset, size - inset, size - inset], fill=PANEL + (255,))
    art = fit_box(icon, int(size * 0.58))
    out.alpha_composite(art, ((size - art.width) // 2, (size - art.height) // 2))
    if ring:
        r = fit_box(Image.open(MEDALLION).convert('RGBA'), size)
        out.alpha_composite(r, ((size - r.width) // 2, (size - r.height) // 2))
    return out


def load_icon(rel: str) -> Image.Image:
    return Image.open(rel).convert('RGBA')


def portrait(champion_id: str, size: int, ring_colour) -> Image.Image:
    """A roster portrait cut to a disc with a thin ring in the class colour."""
    src = fit_box(load_icon(f'{PORTRAITS}/char_{champion_id}_portrait.png'), size)
    out = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    mask = Image.new('L', (size, size), 0)
    ImageDraw.Draw(mask).ellipse([3, 3, size - 3, size - 3], fill=255)
    art = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    art.alpha_composite(src, ((size - src.width) // 2, (size - src.height) // 2))
    out.paste(art, (0, 0), mask)
    d = ImageDraw.Draw(out)
    d.ellipse([1, 1, size - 2, size - 2], outline=ring_colour + (255,), width=3)
    d.ellipse([4, 4, size - 5, size - 5], outline=OUTLINE + (200,), width=1)
    return out


def disc(img: Image.Image) -> Image.Image:
    """Cut a square icon to its inscribed circle — the class badges ship on a dark square plate."""
    size = min(img.size)
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).ellipse([(img.width - size) // 2, (img.height - size) // 2,
                                  (img.width + size) // 2 - 1, (img.height + size) // 2 - 1], fill=255)
    out = Image.new('RGBA', img.size, (0, 0, 0, 0))
    out.paste(img, (0, 0), mask)
    # the badges carry a faint square plate under the medallion; drop anything that translucent
    out.putalpha(out.split()[3].point(lambda a: a if a > 150 else 0))
    return out


def arrow(px: int, glyph: str = '→') -> Image.Image:
    """Cinzel carries no arrows; the body font does (the game's own font gate allows → and ›)."""
    return plain_text(glyph, px, GOLD, FONT_BODY)


def divider(width: int, height: int = 24) -> Image.Image:
    """The long divider at a width: its CENTRE ornament kept at scale, the plain rule on each side
    stretched outward (a 3-slice with the fixed piece in the middle, not at the ends)."""
    src = fit_height(load_icon(DIVIDER), height)
    sw = src.width
    mid = int(sw * 0.42)               # the ornament spans the middle ~40% of the art
    x0 = (sw - mid) // 2
    centre_piece = src.crop((x0, 0, x0 + mid, height))
    if width <= sw:
        return fit_width(src, width)
    out = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    side = (width - mid) // 2
    out.alpha_composite(src.crop((0, 0, x0, height)).resize((side, height), Image.LANCZOS), (0, 0))
    out.alpha_composite(centre_piece, (side, 0))
    out.alpha_composite(src.crop((x0 + mid, 0, sw, height)).resize((width - side - mid, height), Image.LANCZOS), (side + mid, 0))
    return out


def rule(width: int) -> Image.Image:
    """A plain gold hairline that fades at both ends — cheaper than the ornament, for sub-rules."""
    img = Image.new('RGBA', (width, 3), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for x in range(width):
        t = x / max(1, width - 1)
        a = int(255 * min(1.0, 4 * min(t, 1 - t)))
        d.line([(x, 1), (x, 1)], fill=GOLD + (a,))
    return img


def card(w: int, h: int) -> Image.Image:
    """The wide modal frame over the void — the game's panel, as a page card."""
    base = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(base).rounded_rectangle([6, 6, w - 6, h - 6], radius=10, fill=PANEL + (235,))
    base.alpha_composite(nine_slice(load_icon(PANEL_WIDE), w, h, 52))
    return base


def centre(canvas: Image.Image, art: Image.Image, cx: int, cy: int):
    canvas.alpha_composite(art, (int(cx - art.width / 2), int(cy - art.height / 2)))


# ───────────────────────────── pieces ─────────────────────────────

def header() -> Image.Image:
    """1920x600: the cathedral, dark where words sit, the emblem left of the wordmark, the tagline."""
    w, h = 1920, 600
    bg = Image.open(BG_TITLE).convert('RGBA')
    scale = max(w / bg.width, h / bg.height)
    bg = bg.resize((round(bg.width * scale), round(bg.height * scale)), Image.LANCZOS)
    x0 = (bg.width - w) // 2
    y0 = int(bg.height * 0.42 - h / 2)
    y0 = min(max(y0, 0), bg.height - h)
    img = bg.crop((x0, y0, x0 + w, y0 + h))
    # darken toward the bottom and the centre band so both the words and the page below sit in ink
    mask = Image.new('L', (w, h), 0)
    md = ImageDraw.Draw(mask)
    for y in range(h):
        t = y / (h - 1)
        md.line([(0, y), (w, y)], fill=int(255 * (0.30 + 0.55 * t)))
    band = Image.new('L', (w, h), 0)
    ImageDraw.Draw(band).rectangle([0, int(h * 0.22), w, int(h * 0.80)], fill=120)
    band = band.filter(ImageFilter.GaussianBlur(60))
    mask = ImageChops.lighter(mask, band)
    ink = Image.new('RGBA', (w, h), VOID + (255,))
    img = Image.composite(ink, img, mask)

    logo = fit_width(Image.open(LOGO).convert('RGBA'), 1000)
    pos = ((w - logo.width) // 2, int(h * 0.44) - logo.height // 2)
    glow(img, logo, pos, 28, strength=80)
    img.alpha_composite(logo, pos)
    tag = gold_text('YOUR CHAMPION FIGHTS WHILE YOU ARE AWAY', 34)
    centre(img, tag, w // 2, pos[1] + logo.height + 40)
    sub = plain_text('AN OFFLINE IDLE AUTO-BATTLER  ·  ALPHA', 24, VELLUM_DIM)
    centre(img, sub, w // 2, pos[1] + logo.height + 86)
    dv = divider(560, 22)
    centre(img, dv, w // 2, h - 40)
    return img


def background() -> Image.Image:
    """1920x1400 fixed page background: void ink, a faint gold dawn at the top, the emblem as a watermark."""
    w, h = 1920, 1400
    img = Image.new('RGBA', (w, h), VOID + (255,))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(INK[i] + (VOID[i] - INK[i]) * min(1.0, t * 1.6)) for i in range(3))
        d.line([(0, y), (w, y)], fill=c + (255,))
    dawn = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    dd = ImageDraw.Draw(dawn)
    dd.ellipse([w // 2 - 900, -520, w // 2 + 900, 260], fill=GOLD + (36,))
    dawn = dawn.filter(ImageFilter.GaussianBlur(120))
    img.alpha_composite(dawn)
    emblem = fit_box(Image.open(EMBLEM).convert('RGBA'), 900)
    wm = emblem.copy()
    wm.putalpha(wm.split()[3].point(lambda a: a * 9 // 255))
    img.alpha_composite(wm, ((w - wm.width) // 2, h - wm.height - 60))
    return img


def section(key: str, title: str, icon: Image.Image, kicker: str, medallion=True) -> Image.Image:
    """1200x150: [medallion icon]  TITLE over a hairline, the kicker in small vellum under it."""
    h = 150
    img = Image.new('RGBA', (W, h), (0, 0, 0, 0))
    ic = medallion_icon(icon, 118) if medallion else fit_box(icon, 118)
    glow(img, ic, (10, (h - ic.height) // 2), 18, strength=60)
    img.alpha_composite(ic, (10, (h - ic.height) // 2))
    x = 156
    t = gold_text(title, 44)
    img.alpha_composite(t, (x, 22))
    img.alpha_composite(rule(W - x - 10), (x, 22 + t.height + 8))
    k = plain_text(kicker, 25, VELLUM_DIM, FONT_BODY_REG)
    img.alpha_composite(k, (x + 2, 22 + t.height + 20))
    return img


def loop_strip() -> Image.Image:
    """1200x230: FIGHT -> SPEND -> LEAVE, three medallions with the arrows the game's own HUD uses."""
    h = 230
    img = card(W, h)
    steps = [
        ('FIGHT', 'It clears waves on its own', load_icon(f'{ICONS}/nav/nav_hunt.png'), False),
        ('SPEND', 'Gear, build, traits, warren', load_icon(f'{ICONS}/nav/icon_nav_forge.png'), True),
        ('LEAVE', 'It keeps fighting while you are gone', load_icon(f'{ICONS}/blessings/icon_blessing_convenience.png'), False),
    ]
    cols = [W * (2 * i + 1) // 6 for i in range(3)]
    for (name, line, icon, ring), cx in zip(steps, cols):
        ic = medallion_icon(icon, 112, ring=ring) if ring else fit_box(icon, 112)
        centre(img, ic, cx, 78)
        centre(img, gold_text(name, 30), cx, 158)
        centre(img, plain_text(line, 21, VELLUM_DIM, FONT_BODY_REG), cx, 192)
    ar = arrow(56)
    for i in range(2):
        centre(img, ar, (cols[i] + cols[i + 1]) // 2, 78)
    return img


def classes_strip() -> Image.Image:
    """1200x400: five class badges, the class colour as a bar, the two champions as portraits."""
    h = 400
    img = card(W, h)
    # (id, NAME, how it joins). THE SEEKER is the starter (CharacterRoster: CharacterUnlock.Start);
    # the other four firsts each need their own region conquered; every second is a quest.
    classes = [
        ('warden', 'WARDEN', ('anvil', 'THE ANVIL', 'CONQUEST'), ('tower', 'THE FALLING TOWER', 'QUEST')),
        ('ranger', 'RANGER', ('chorus', 'THE CHORUS', 'CONQUEST'), ('oathbound', 'THE OATHBOUND', 'QUEST')),
        ('mystic', 'MYSTIC', ('metronome', 'THE METRONOME', 'CONQUEST'), ('quiver', 'THE QUIVER', 'QUEST')),
        ('bulwark', 'BULWARK', ('unbroken', 'THE UNBROKEN', 'CONQUEST'), ('thornwall', 'THE THORNWALL', 'QUEST')),
        ('wanderer', 'WANDERER', ('seeker', 'THE SEEKER', 'START'), ('magpie', 'THE MAGPIE', 'QUEST')),
    ]
    cw = (W - 80) // 5
    for i, (key, name, first, second) in enumerate(classes):
        cx = 40 + cw * i + cw // 2
        colour = CLASS_COLOURS[key]
        badge = disc(fit_box(load_icon(f'{CLASS_ICONS}/icon_class_{key}.png'), 84))
        glow(img, badge, (cx - badge.width // 2, 34), 14, colour, 90)
        centre(img, badge, cx, 76)
        centre(img, gold_text(name, 26), cx, 140)
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([cx - 56, 162, cx + 56, 166], radius=2, fill=colour + (255,))
        for j, (cid, cname, how) in enumerate((first, second)):
            px = cx - 54 + j * 108
            centre(img, portrait(cid, 86, colour), px, 228)
            rest = cname[4:] if cname.startswith('THE ') else cname
            centre(img, plain_text('THE', 11, VELLUM_DIM, FONT_BODY_REG), px, 284)
            centre(img, plain_text(rest, 15, VELLUM), px, 302)
            centre(img, plain_text(how, 12, colour, FONT_BODY), px, 326)
    centre(img, plain_text('THE SEEKER FROM THE START  ·  FOUR BY CONQUEST  ·  FIVE BY A QUEST', 16, VELLUM_DIM, FONT_BODY_REG), W // 2, 364)
    return img


def regions_strip() -> Image.Image:
    """1200x300: the six regions in the order the world opens them."""
    h = 300
    img = card(W, h)
    regions = [
        ('verdant', 'VERDANT HOLLOW', 'Nature'),
        ('cinderworks', 'CINDERWORKS', 'Machine'),
        ('umbral', 'UMBRAL REACH', 'Shadow'),
        ('marrow_wastes', 'MARROW WASTES', 'Body'),
        ('still_archive', 'THE STILL ARCHIVE', 'Mind'),
        ('pale_choir', 'THE PALE CHOIR', 'Spirit'),
    ]
    cw = (W - 60) // 6
    for i, (key, name, theme) in enumerate(regions):
        cx = 30 + cw * i + cw // 2
        ic = fit_box(load_icon(f'{ICONS}/regions/icon_region_{key}.png'), 136)
        glow(img, ic, (cx - ic.width // 2, 34), 14, strength=50)
        centre(img, ic, cx, 102)
        # two-line names when they would overflow the column
        words = name.split(' ')
        if len(words) > 1:
            split = 2 if len(words) == 3 else 1
            centre(img, gold_text(' '.join(words[:split]), 21), cx, 196)
            centre(img, gold_text(' '.join(words[split:]), 21), cx, 222)
        else:
            centre(img, gold_text(name, 21), cx, 208)
        centre(img, plain_text(theme.upper(), 17, VELLUM_DIM, FONT_BODY_REG), cx, 254)
        if i < 5:
            centre(img, arrow(30, '›'), 30 + cw * (i + 1), 100)
    return img


def forms_strip() -> Image.Image:
    """1200x260: the six STYLES a skill can have, on the icons the game draws them with."""
    h = 260
    img = card(W, h)
    # (icon, STYLE, what it does). The icons keep their Form-era file names; the game reads each one as
    # the style next to it (StyleAffinityDiagram.IconKey). Order matches the page's alt text.
    forms = [
        ('strike', 'HAMMER', 'one huge blow'),
        ('projectile', 'VOLLEY', 'many small hits'),
        ('trap', 'SNARE', 'being hit works for you'),
        ('mark', 'SIGN', 'boosts everything else'),
        ('aura', 'FIELD', 'hits every enemy, always'),
        ('transformation', 'DRAIN', 'turns damage into health'),
    ]
    cw = (W - 60) // 6
    for i, (key, name, line) in enumerate(forms):
        cx = 30 + cw * i + cw // 2
        ic = medallion_icon(load_icon(f'{ICONS}/forms/icon_form_{key}.png'), 118)
        centre(img, ic, cx, 92)
        centre(img, gold_text(name, 20 if len(name) < 12 else 16), cx, 176)
        centre(img, plain_text(line, 17, VELLUM_DIM, FONT_BODY_REG), cx, 210)
    return img


def footer() -> Image.Image:
    """1200x170: the promise line under an ornament, the emblem small and centred."""
    h = 170
    img = Image.new('RGBA', (W, h), (0, 0, 0, 0))
    centre(img, divider(720, 22), W // 2, 22)
    em = fit_box(Image.open(EMBLEM).convert('RGBA'), 64)
    centre(img, em, W // 2, 68)
    centre(img, gold_text('MADE BY ONE PERSON', 26), W // 2, 118)
    centre(img, plain_text('OFFLINE  ·  NO ACCOUNT  ·  NO ADS  ·  NO IN-GAME PURCHASES', 20, VELLUM_DIM, FONT_BODY_REG), W // 2, 150)
    return img


SECTIONS = [
    ('fight', 'THE FIGHT RUNS ITSELF', f'{ICONS}/nav/nav_hunt.png', 'On every screen — and for hours after you leave.', False),
    ('build', 'WEAVE A BUILD, NOT A ROTATION', f'{ICONS}/forms/icon_form_aura.png', 'Skills, keystones and Vows. Then a Mastery tree decides how it fights.', True),
    ('forge', 'A FORGE THAT READS HONESTLY', f'{ICONS}/nav/icon_nav_forge.png', 'Every item shows its real numbers. Every upgrade shows its chance.', True),
    ('world', 'SIX REGIONS, CONQUERED AT WAVE TWENTY', f'{ICONS}/regions/icon_region_umbral.png', 'Past the twentieth wave the counter reads OVERWAVE — and Memory Dust buys a head start.', False),
    ('champions', 'TEN CHAMPIONS, FIVE CLASSES', 'portrait:seeker', 'One always-on passive each. The second of every class must be earned.', False),
    ('warren', 'A WARREN THAT WORKS WHEN YOU LEAVE', f'{ICONS}/nav/icon_nav_warren.png', 'Gleam and Memory Dust, produced while you are away.', True),
    ('traits', 'TRAITS YOU AWAKEN', f'{ICONS}/nav/nav_prestige.png',   # the TRAITS rail tile's own icon (roads are gone)
     'Twenty-six of them, earned by how you play. Wear three. Change them for free.', True),
]


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    out = {
        'header': header(),
        'background': background(),
        'loop': loop_strip(),
        'classes': classes_strip(),
        'regions': regions_strip(),
        'forms': forms_strip(),
        'footer': footer(),
    }
    for key, title, icon, kicker, medal in SECTIONS:
        art = portrait(icon.split(':')[1], 118, GOLD) if icon.startswith('portrait:') else load_icon(icon)
        out[f'section_{key}'] = section(key, title, art, kicker, medal)
    for name, img in out.items():
        p = os.path.join(OUT, name + '.png')
        img.convert('RGBA').save(p, optimize=True)
        print(f'{name:18s} {img.size[0]}x{img.size[1]}  {os.path.getsize(p) / 1e6:.2f} MB')


if __name__ == '__main__':
    main()
