# IDLExIDLE — itch.io page kit

Everything the itch.io project page needs to look like the game instead of a form. Images come
from `python tools/marketing/make_itch_page.py` (layout over the shipped UI art — re-run after any
icon or logo change); the body text is `description.html`; the colours are the game's own palette
(`UiKit.cs`). Butler pushes builds only — the page itself is filled in by hand, once, in ~15 minutes.

| File | Where it goes | Size |
|---|---|---|
| `header.png` | Edit theme → **Banner** (and tick *Hide title* — the banner carries the wordmark) | 1920×600 |
| `background.png` | Edit theme → **Background image**, *Fixed*, *no repeat*, position *top center* | 1920×1400 |
| `loop.png` … `footer.png` | the Description body, at the spots `description.html` marks | 1200 wide |
| `../capsules/itch_cover.png` | Edit project → **Cover image** (unchanged) | 630×500 |
| `../screenshots/*.png` | Edit project → **Screenshots**, in the order `../store-page.md` §5 gives | 1920×1080 |

## 1. Theme (Edit theme, top of the project page)

The page is the game's void ink with vellum text and hearth gold links — the same three colours
the HUD uses, so a screenshot and the page around it read as one thing.

| Setting | Value |
|---|---|
| Background colour | `#14101A` (void ink — also what shows past the background image's bottom edge) |
| Background image | `background.png` · Repeat: **none** · Fixed: **on** · Position: **top center** |
| Banner | `header.png` · Layout: *header at the top, full width* · **Hide the page title** |
| Text colour | `#EDE3C8` (vellum) |
| Link colour | `#F0A830` (hearth gold) |
| Button colour | `#F0A830` |
| Button text colour | `#14101A` |
| Font | a serif if itch offers one you like (*Merriweather* reads closest); the wordmark and every heading image already carry Cinzel, so the body font is free to be plain |
| Screenshots | *sidebar* — they sit beside the text and stay visible on desktop |
| Layout | *centered* (the strips are 1200 px wide and scale down; 960 px column is the target) |

Custom CSS is a per-project permission on itch (theme editor → *Request custom CSS*). When it is
granted, paste `custom.css` — it pulls Cinzel from Google Fonts for the headings, frames the
screenshot sidebar in gold, and dims the itch chrome. The page does not depend on it.

## 2. The body (Edit project → Description)

1. Open the editor, press the `</>` (HTML) button, paste `description.html` whole.
2. Switch back to the visual editor. Every `<img>` shows as a broken image — click each, delete it,
   and insert the named file from this folder with the toolbar's image button at the same spot
   (top to bottom: `loop`, `section_fight`, `section_build`, `forms`, `section_forge`,
   `section_world`, `regions`, `section_champions`, `classes`, `section_warren`, `section_traits`,
   `footer`). Twelve images, ~1.5 MB total.
   *Faster:* upload all twelve first (image button → upload), copy each `img.itch.zone` URL into
   `description.html`'s `src` attributes, then paste the HTML once.
3. Leave the images at their natural width (do not set a fixed pixel width) so they shrink on phones.
4. Save → *View page* → check it at phone width too. The section images are 1200 px wide with
   44 px titles, so at 360 px the titles are still ~13 px — readable; every sentence that matters is
   real text under them, not baked into an image.

## 3. Short text and metadata (Edit project, top)

- **Short description:** *Your champion fights while you are away. You decide what it becomes.*
- **Tags:** `idle`, `auto-battler`, `incremental`, `rpg`, `singleplayer`, `offline`, `dark-fantasy`,
  `build-crafting`, `monogame` · Genre *Strategy* · Made with *MonoGame* · Inputs *Keyboard, Mouse*
  · Average session *A few minutes*.
- Community: comments **on**; pin one asking for the **COPY FEEDBACK CODE** (during play, Escape or the gear at the
  top right opens SETTINGS; on the title screen, Escape quits the game).

## 4. Keeping it current

Every strip is baked from the catalogues by hand-written lists in `make_itch_page.py` (the ten
champions, the six regions, the six styles). When a champion, region or style is added, add it there
and re-run — the page has no other source of truth. The copy in `description.html` is the page's;
`../store-page.md` §2 still holds the older Steam draft and says so.
