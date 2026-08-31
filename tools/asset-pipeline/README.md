# Asset Pipeline

Generates missing game art from a declarative manifest, using PixelLab, and files
it where `AssetLibrary` will find it.

## Why each piece exists

**PixelLab returns fully opaque images.** Verified by byte-level decode: even with
`no_background=true` and the API reporting `transparent: True`, every pixel comes
back alpha 255 on a flat background field. Dropping that straight into
`assets/art` renders a solid rectangle over the UI, because `AssetLibrary`
premultiplies and draws with `BlendState.AlphaBlend`. So every generated image
goes through `knockout.py` before it is filed.

**Knockout is a border flood-fill, not a chroma-key.** The background colour also
occurs inside the subject. A chroma-key punches holes through those pixels; a
flood-fill seeded from the canvas border can only reach background actually
connected to the outside.

**Alpha is written straight, not premultiplied** — `AssetLibrary.Premultiply()`
does that at load time, and doing it here too would double-apply it.

## Layout

| File | Role |
|---|---|
| `manifest.json` | Declarative spec for still images: one entry per asset. **Edit this to add art.** |
| `animations.json` | Declarative spec for 8-frame strips (enemy animations + VFX). |
| `generate.py` | Unattended path — generates every missing still via the REST API. |
| `animate.py` | Builds animation strips via `/animate-with-text-v3`. |
| `ingest.py` | Interactive path — files images already generated through the PixelLab MCP tools. |
| `audit.py` | Diffs asset keys the C# references against PNGs on disk. |
| `knockout.py` | Background removal, canvas normalisation, validation stats. |
| `pixellab_client.py` | REST client: token loading, retry/backoff, sync image generation. |
| `pixelpng.py` | Dependency-free PNG read/write (Pillow is unavailable here). |
| `.env` | **Gitignored.** Holds `PIXELLAB_API_TOKEN`. Never move this into a tracked file. |

## Animations and effects

```bash
python3 tools/asset-pipeline/animate.py --dry-run
python3 tools/asset-pipeline/animate.py
```

Two kinds of entry in `animations.json`:

- **`source`** — animate an existing sprite. Used for enemies, so the new motion
  stays on-model instead of inventing a second design for a creature the player
  has been fighting all game. Costs 8 generations.
- **`prompt`** — no sprite exists (VFX), so frame 1 is generated first and then
  animated. Costs 9.

Strip layout is dictated by `UiKit.cs:156-165` and `VfxPlayer.cs:65-66`: frames
are assumed **square**, in a single row at y=0, so `frameCount = width / height`.
A width that is not a whole multiple of the height makes the renderer silently
refuse to draw.

Two details worth keeping:

- The API caps the input frame at **256×256** but the runtime convention is
  `_strip8_512`. Frames are upscaled ×2 nearest-neighbour — an integer scale on
  pixel art is visually lossless and keeps the filename honest.
- The endpoint returns **`frame_count + 1`** images; the last closes the loop
  back to the first. Only the first 8 are kept, which is what makes the loop
  seamless instead of hitching on a duplicated frame.

## Adding new content

1. Add an entry to `manifest.json`:

   ```json
   {
     "key": "icon_region_new_place",
     "dest": "assets/art/UI/icons/regions",
     "style": "medallion",
     "subject": "one large brass astrolabe filling the center"
   }
   ```

2. `python3 tools/asset-pipeline/generate.py`

Anything already on disk is skipped, so the command is safe to re-run and only
ever produces what is genuinely missing.

## Automatic generation

`.claude/hooks/generate-assets.sh` runs on every `Write`/`Edit` to
`manifest.json`, client `.cs` files, or design docs. If assets are missing it
launches `generate.py` **detached** (generation takes minutes; a blocking hook
would time out) and logs to `.staging/generate.log`.

Set the token to arm it:

```bash
export PIXELLAB_API_TOKEN='<your-token>'
```

Without the token the hook is advisory only — it prints what is missing and
spends nothing. A lock file prevents overlapping runs.

## Prompt lessons learned

These were established empirically and are worth preserving:

- **One subject, not a scene.** "a golden urn overflowing with coins beside a
  wheat sheaf" produced an *empty frame*; "a tall stack of golden coins filling
  the center" produced the intended icon. Compound subjects make the model render
  the container and drop the contents.
- **Avoid cruciform descriptions.** "hammer crossed over an anvil" and "ribcage"
  both came back as a Christian cross. Say "resting on top of" and pick an
  unambiguous silhouette.
- **`text_guidance_scale` 9** for open styling, **12** when the subject keeps
  drifting.
- **Author at 256px.** Icons draw at 36–96px, so 256 downscales cleanly and stays
  legible; `pixflux` caps at 400px per side anyway.

## Full-screen backgrounds

`pixflux` caps at 400px/side, which is not enough for a 1920×1080 background.
`pixen` reaches **768**, and that makes the maths work:

> author at **640×360**, upscale **×3** → exactly **1920×1080**

An integer nearest-neighbour upscale is visually lossless on pixel art — each
source pixel becomes an exact 3×3 block, no new colours, no resampled edges. The
result is then drawn 1:1 into the 1920×1080 render target, so `LinearClamp`
never gets a chance to soften it. A non-integer scale (e.g. 768→1920 at 2.5×)
would smear every edge.

Manifest entry for a background:

```json
{
  "key": "bg_forge",
  "dest": "assets/art/Environments/screens",
  "style": "background",
  "subject": "a dark underground smithy, banked forge coals ...",
  "model": "pixen", "width": 640, "height": 360, "upscale": 3, "knockout": false
}
```

`knockout: false` matters — backgrounds are opaque scenes, and the knockout /
trim path would crop the composition to its "content" and shave off the edges.

Keep them **dark**. The art bible puts background last in the hero-shape
hierarchy (ambient only), and dense tabular UI sits on top. The shipped five
measure 25–54 mean luminance out of 255.

## Constraints worth knowing

- **Max 8 concurrent jobs**, and recently-finished jobs still count toward the
  ceiling for a short while. `MAX_IN_FLIGHT` is set to 6 for headroom.
- **`pixflux` caps at 400×400.** This is why no `bg_*` background is in the
  manifest: those are 1920×1080, and a 400px source upscaled 4.8× is mush.
  Backgrounds need a different tool — see below.
- **Keys are flat basenames.** `AssetLibrary` maps *filename without extension*
  to texture, case-insensitively, so `key` must be unique across all of
  `assets/art/**`. There are currently 98 basename collisions in the tree; the
  winner is whichever the directory walk reaches last.
- **Forbidden path substrings.** `AssetLibrary.ValidateRuntimeAssetPath` throws
  at startup on any path containing `/preview/`, `source_reference`,
  `source_sheet`, `concept`, `contact_sheet`, `runtime_assets_preview`,
  `poster`, `showcase`, `mood_board`, or `pitchboard`. `generate.py` refuses to
  write such a path. Note `concept` is a bare substring — a folder named
  `conceptual/` is enough to kill the game.

## Deliberately NOT generated here

Per the art direction review, pixel art is appropriate for the small UI/icon
layer and **not** for these, which need a painterly pipeline matching the ~1,300
existing smooth assets:

- The Hunter champion and the 16 `hunter_part_*` cutout rig parts
- Enemy and boss sprites and animation strips
- Any 1920×1080 background, overlay, or parallax layer
  (`bg_forge`, `bg_warren`, `bg_regionmap`, `bg_constellation`, `bg_title`)
- Item icons at the 200px Forge preview or 192px Gear hero size

## v2 — the 2026-08-22 arena consistency pass (`v2/`)

The stage art (ten champions, six enemies, six bosses, thirteen effects) was regenerated as ONE set
through the PixelLab MCP tools — characters with explicit rotations (`create_character` v3, side
view; south-east for the champion so it faces RIGHT, south-west for enemies/bosses so they face
LEFT), `animate_character` v3 clips on that one rotation, `create_image_pixen` + `animate_image`
for non-humanoid creatures and every effect. Contract: `design/art/arena-art-contract.md`.

| File | Role |
|---|---|
| `v2/spec.json` | every figure/effect: look, route, size, clip texts — the prompts that made the set |
| `v2/rhart.py` | Pillow toolkit: fetch, strip (union-bbox assembly), gate, sheet, static |
| `v2/clip.py` | one generated clip → 8×512 strip + gate, from a character animation or an animate_image job |
| `v2/file_assets.py` | staging → `assets/art`, derived statics, retirement of the superseded art |

Generation runs through MCP (no REST token needed); the staging root is
`tools/asset-pipeline/.staging/v2/<group>/` (gitignored). The `manifest.json` / `animations.json` /
`build_spec.py` path above is the v1 pipeline and still drives the UI/icon layer; its enemy/boss/roster
animation entries are superseded by v2 and are kept for history only.
