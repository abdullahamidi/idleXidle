#!/bin/bash
# THROWAWAY (2026-09-26): the JAWS concept animatics A/B/C (prototypes/jaws-concepts/animatic.py, unchanged, not polished)
# drawn over the study's BEST plate (key-pose strip + lunge + the F2 flash, the answer on). The anchor is the front
# whelp's front-lower point AT CONTACT, where the lunge has carried it: the Swarm box is 252 px wide and the lunge is
# 0.15 of it on the contact frame, so ~38 px left of its resting 1122.
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
for C in A B C; do
  python prototypes/jaws-concepts/animatic.py build/shots/bite/best_plate build/shots/bite/concepts/$C $C "7000:${ANCHOR:-1084,810}"
done
