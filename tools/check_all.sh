#!/bin/bash
# Every gate that does not need a build. Run before a commit that touches UI or assets.
#
# These exist because the game FAILS SOFT by design: a missing glyph draws nothing, a
# missing texture falls back to a coloured rectangle, and neither ever throws. That is the
# right behaviour at runtime and it means a mistake is invisible until someone looks at the
# pixels. These scripts are the "someone looks" step, automated.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 1

fail=0
for gate in check_font_coverage.py check_font_digits.py check_ui_type.py check_asset_keys.py check_init_order.py check_nav_gates.py check_mouse_space.py; do
  echo "── $gate"
  py "tools/$gate" | sed 's/^/   /' || fail=1
  [ "${PIPESTATUS[0]}" -eq 0 ] || fail=1
done

[ $fail -eq 0 ] && echo "all gates green" || echo "GATES FAILED"
exit $fail
