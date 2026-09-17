# Alpha copy audit — 2026-09-17

Before the first **alpha** push to itch.io (0.1.0-alpha), every factual claim in the release copy
was checked against the code: 217 claims across `docs/publisher-notes.md` (it ships inside the build
as `README.md`), `docs/store/itch/description.html` (the itch page body), `docs/store/store-page.md`
and the Status paragraph of `README.md`.

How: one reader per document listed the claims and checked each one in the source. A second,
independent reviewer tried to refute every reported mismatch. The JSON files here hold the
**upheld** findings only (claim, code evidence, the verified replacement, and the reviewer's note).
Twelve of the 56 reported mismatches were refuted and are not listed.

| File | Upheld findings |
|---|---|
| `publisher-notes.json` | 13 |
| `itch-description.json` | 13 |
| `store-and-readme.json` | 18 |

The corrections were applied by a second pass (one editor per document, then a reviewer that
re-checked every changed sentence against the code and fixed seven more errors in place).

## What the audit found in the GAME, not the copy

- **The Roster opened on the first conquest and said "Another hunter joined you"** when nobody had
  joined. THE THORNWALL moved from the first region to a quest on 2026-08-26, so the first
  conquest-earned champion is THE ANVIL, on the second region. Fixed in `b42a22f`: the Roster opens
  when a second champion joins.
- **The Settings caption said COPY FEEDBACK CODE "SENDS THE DEVELOPER"** your data. The button only
  copies a code. Fixed, with "EVERY KEY IS LISTED IN HELP (F1)" (the sheet lists the main keys only).
- **The build id disagreed** between the itch.io version (7-character commit) and the game's BUILD
  line (8 characters). `tools/publish/itch_push.sh` now uses `--short=8`.

## Baked images

Seven strips in `docs/store/itch/` carried old words (the Form-era skill names, "fifty-one traits",
"while you sleep", "the first of each class opens by conquest"). The strings were fixed in
`tools/marketing/make_itch_page.py` and the seven strips re-baked (see the note on the missing body
font at the top of that script). The itch page itself has no API: the owner pastes
`description.html` and uploads the images by hand.

Screenshots of the Settings fixes: `production/qa/evidence/alpha-release/`.
