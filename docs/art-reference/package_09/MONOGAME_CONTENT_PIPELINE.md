# MONOGAME CONTENT PIPELINE SETUP

## Using the generated MGCB project

1. Install the matching MonoGame templates and MGCB Editor for your MonoGame version.
2. Open `Content/ResonanceHunterAssets.mgcb`.
3. Confirm that the output and intermediate directories match your project.
4. Build the MGCB project.
5. Reference the generated `.xnb` files from your game through `Content.Load<T>()`.

## Important texture processor settings

The generated MGCB file uses:

- Color key disabled
- Mipmaps disabled
- Premultiplied alpha enabled
- Power-of-two resizing disabled
- Texture format `Color`

These settings are appropriate for 2D UI, sprites, and VFX. If a specific platform has memory pressure, consider compressed formats only after testing alpha quality.

## Content build performance

The complete bundle is large. During development, you can create smaller MGCB files by category and build only the screen or region you are currently working on.
