#!/usr/bin/env python3
"""quiver_one_arrow -- take the generator's nocked arrow out of the Quiver's attack strip after the release.

    python tools/asset-pipeline/v2/quiver_one_arrow.py

ADR-011's one object in the hand and in the air (the Phase 1 review, item 07): the strip's frames 5 and 6 still drew
the arrow the generator nocked (by the bow hand on 5, hanging under it on 6) while prop_quiver_arrow, released from the
BowHand socket on frame 4, was already in flight, so for 1-2 frames the bow held an arrow while one flew. Both remnants
lie over clear air beside the hand, so they are cleared (alpha 0); no body texel is touched. Idempotent.
"""
from __future__ import annotations

import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
REL = "assets/art/Animations/Roster/quiver_attack/char_quiver_attack_strip8_512.png"
F = 512

# frame -> boxes (frame-local x0, y0, x1, y1, exclusive) of the leftover arrow, measured on the strip
REMNANTS = {
    5: [(372, 286, 401, 307)],   # the arrow still at the bow hand, pointing at the row
    6: [(328, 354, 347, 387)],   # the arrow hanging under the bow grip
}


def main() -> None:
    path = os.path.join(REPO, REL)
    img = Image.open(path).convert("RGBA")
    px = img.load()
    cleared = 0
    for frame, boxes in REMNANTS.items():
        for x0, y0, x1, y1 in boxes:
            for y in range(y0, y1):
                for x in range(frame * F + x0, frame * F + x1):
                    if px[x, y][3]:
                        px[x, y] = (0, 0, 0, 0)
                        cleared += 1
    img.save(path, optimize=True)
    print(f"cleared {cleared} texels")


if __name__ == "__main__":
    main()
