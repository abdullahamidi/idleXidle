#!/bin/bash
# THROWAWAY (JAWS concept study, 2026-09-25). Render the three concept animatics (animatic.py) over every plate that
# film_plates.sh filmed. The triggers are the seeded fight's real JAWS answers (the bite's ms) and the attacker's
# front-lower point, read off the production traces. A trigger outside a take's film is skipped.
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
P=build/shots/jaws/plates
for C in A B C; do
  O=build/shots/jaws/concepts/$C
  python prototypes/jaws-concepts/animatic.py $P/plate_normal $O $C "7000:1122,810"
  python prototypes/jaws-concepts/animatic.py $P/plate_spray $O $C "1000:1122,805"
  python prototypes/jaws-concepts/animatic.py $P/plate_hh $O $C "13000:1530,812"
  python prototypes/jaws-concepts/animatic.py $P/plate_rep_a $O $C "1000:1122,805" "3000:1122,813"
  python prototypes/jaws-concepts/animatic.py $P/plate_rep_b $O $C "1000:1122,805" "3000:1122,813" "5000:1122,801"
  python prototypes/jaws-concepts/animatic.py $P/plate_rep_c $O $C "3000:1122,813" "5000:1122,801"
done
