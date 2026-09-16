# Dispatches

> **Quick design** — a small system with a large blast radius, specified at the eight required
> sections rather than at full GDD length. The architecture is `docs/architecture/ADR-007`; the code
> is `src/IdleXIdle.Core/Progression/Dispatches.cs` and `DispatchCopy.cs`, surfaced by
> `src/IdleXIdle.Game/Game1.Dispatches.cs`. Written 2026-09-15 (attention ownership pass).

## 1. Overview

DISPATCHES is the game's inbox for **account news** — everything that happens somewhere other than
the screen the player is on. A screen unlocking, a quest finishing, a champion joining, a keystone
taught, a Vow offered or proved, a trait waking, the first gem, a set completed, a region conquered:
each writes one letter, keyed by the event, kept until it is read. Letters are typed rows, never
persisted text — the words are rendered from the catalogues at the moment they are shown, so a
renamed keystone reads correctly in a letter written months earlier. The envelope in the chrome
carries an unread mark that holds until the letters are read; **M** opens the reading surface. The
notice toast keeps what it is actually good at: the answer to something the player just did, on the
screen they did it on.

## 2. Player Fantasy

**Letters waiting while the Hunter is away.** The Hunter descends without you; the world goes on
without you. Coming back to a mark on the envelope is the idle game's own pleasure — something
happened, and it is still there. Nothing is shouted across a screen you chose to be looking at, and
nothing important is gone because you were reading something else for six seconds. The player's
relationship with the news is theirs to schedule: open it now, open it in an hour, open it never and
still see every fact on its own screen.

## 3. Detailed Rules

**What is a dispatch.** A row: a stable `Key`, a `Kind` (Unlock, Gem, Trait, Vow, Keystone, Champion,
Quest, Set, Region, Socket, Migration), the millisecond it arrived, whether it has been read, and up
to three typed payload columns (`SubjectId`, `RegionId`, `Count`). No product text is stored.
`DispatchCopy.Headline` / `Body` render the letter from its Kind and subject against the live
catalogues, and every Kind has a plain fallback ("A TRAIT HAS AWAKENED") for a subject the
catalogues no longer carry. A raw id never reaches the player.

**Keys.** One semantic key per event, so the same event can only ever be told once:
`unlock.<screen>` · `quest.<id>.complete` · `hunter.<id>.joined` · `keystone.<id>` · `vow.<id>` ·
`trait.<id>` · `set.<source>.complete` · `region.<id>.conquered` · `gem.first` · `socket.first` ·
`migration.<what>`. A key that has been used — by a posted letter or by the migration seed — is
**Known**, and a second post under it writes nothing and returns false. A producer may therefore ask
every frame; the inbox is the dedupe, not the producer.

**A Vow's key does not branch, its sentence does.** A Vow is discovered once however it arrives, so
both producers write `vow.<id>`; the headline reads "A VOW IS OFFERED TO YOU — X" for a granted Vow
and "A VOW HAS REVEALED ITSELF — X" for a discovered one.

**Crash safety is a contract, not a hope.** Every producer meets one of three conditions: the letter
is **asked for from a fact the save already carries** and re-derived each frame (`socket.first`,
`gem.first`, `set.*`, `keystone.*`); or it is **posted in the same frame that fact is saved**
(quest, champion, conquest); or it is **self-healing because the fact is not saved until the letter
is** (screen reveals, Vows, trait awakenings). A crash between the event and the write leaves the
game re-deriving the event on the next load.

**Read and unread.** A letter is read by opening it in the reading pane — deliberately, with a click
or ENTER, never by the panel merely being open. `MARK ALL READ` (mouse, or **R**) reads the lot. One
exception: walking to the ROSTER screen marks champion letters read, because the letter's one
sentence is "switch hunter on the ROSTER screen", so arriving there *is* reading it. The envelope's
unread mark is state: it holds across sessions until the letters are read.

**Cap.** The inbox holds 60 rows. Over the cap the **oldest read** row is dropped; if nothing is
read, the cap yields and the inbox grows rather than discard an unread letter. `Known` keys are never
pruned, so a pruned letter's event can never arrive again as news.

**The surface.** The envelope sits in the chrome chain left of the `?` (right to left: gear · ? ·
envelope · capsules), on every screen, and hides under any attention owner above the coach's rung —
the `?`'s RUNG, but the owner alone: the `?` also needs the screen to have a tour, and the envelope
never does, because it is the same envelope everywhere. Click it or press **M**. The panel is a modal: newest-first list on the left with
its unread dots and region kickers, reading pane on the right, the count (`2 UNREAD · 7 KEPT`) beside
the close, `MARK ALL READ — R` at the foot. UP/DOWN move the highlight, ENTER opens, the wheel
scrolls, Escape or M closes. Selection is held by **key**, never by row index.

**Arrival.** One halo pulse behind the envelope and one quiet cue (`sfx_dispatch`, the softest in the
bank), both **deferred until nothing at all has a claim on the frame** — not merely until the coach's
rung clears. A letter is a message, and a notice toast or a locked-tile refusal is a message the
player has just asked for, so the two must not land together; the cue is audible, which would stack it
on the toast's. Both are skipped under Reduced Motion — the unread mark carries it. (The rail's own
ceremonies still wait only for the coach's rung: they are silent and share no pixels with the toast.
The mark itself never waits for anything — it is state.)

**One lesson.** `FirstDispatchOpened`: offered once a letter is unread, completed only by really
opening the surface, never by closing the card.

**What stays a toast.** Direct feedback, on the screen that produced it: a champion switch, a locked
tile's refusal, `RepairForSwitch`'s shed line, the boot/save line, COPIED.

## 4. Formulas

There is no arithmetic in this system beyond two rules:

| | |
|---|---|
| **Cap** | `while (Count > 60) drop the oldest row with Read == true; stop if none is read` |
| **Unread mark** | `Unread = count of rows with Read == false`; the envelope's mark is drawn when `Unread >= 1` |

Everything else is set membership: a key is Known or it is not.

## 5. Edge Cases

| Case | Behaviour |
|---|---|
| **A save from before the inbox (v < 8)** | Its whole history is seeded **Known with no rows** — every screen open, region conquered, trait, keystone, Vow, quest, champion and set it holds, plus `gem.first`, `socket.first` and the two `migration.*` one-shots. The veteran's first boot shows an empty inbox and no mark. |
| **A save at v8 or later** | Believed exactly as written. An unread letter stays unread; nothing is seeded. |
| **A letter arrives while the panel is open** | It appears at the top of the list, unread. The open letter and the highlight are held by key, so neither moves and nothing is read that was not opened. |
| **The open letter is pruned** | The pane falls back to the newest letter rather than silently showing whatever took that row. |
| **The same event fires twice** (both Vow producers, both keystone sites, a re-derived producer asking every frame) | One letter. The second post is refused by the key. |
| **A screen the authored opening walks the player into** | Marked Known, not posted: being shown a screen and then told it opened is the same sentence twice. |
| **A letter arrives while something owns the frame** | The row is written immediately (it is state). The pulse and the cue wait; the mark appears when the envelope next paints. |
| **A letter arrives on the title screen** | Same rule — the cue fires on the first free frame of play. |
| **A subject leaves its catalogue** (a retired Vow, a renamed screen) | The letter renders its Kind's plain fallback. `unlock.*` keys are additionally read forward through `Onboarding.ModernScreenKey`, so a renamed screen's old key resolves rather than duplicating. |
| **An unknown `Kind` in the save** | Dropped on load. A bad row never crashes a boot. |
| **Duplicate keys in the save** | Collapse to the first. |
| **Under the capture rig** | The inbox is **written** — it is state, not a flourish. Only its surfacing is posed. `dispatchesempty` is the one mode that refuses posts, because an empty state with two letters in it is not an empty state. |
| **`migration.keystones` / `migration.fifth_slot`** | Effectively dormant: every pre-v8 file is seeded knowing them, and a v8 file has its keystones saved, so nothing re-derives them. The Kind stays valid with no live producer, which is intended, not a bug. |

## 6. Dependencies

- **Attention ownership** (`docs/architecture/ADR-007`) — the envelope's visibility, the arrival's
  deferral, and the rule that put this system here at all.
- **Save/load persistence** (`design/gdd/save-load-persistence.md`) — `SaveGame` v8's three columns,
  and `Dispatches.FirstVersionWithInbox = 8` as a frozen literal.
- **Onboarding** (`design/gdd/onboarding-tutorial-system.md`) — the `FirstDispatchOpened` lesson,
  `TourTarget.DispatchIcon`, and `LessonFacts.DispatchesUnread` / `DispatchesOpenedEver`.
- **The catalogues the copy is rendered from** — `Unlocks`, `TraitCatalogue`, `Vows`, `Keystones`,
  `CharacterRoster`, `QuestCatalogue`, `ElementSets`, `Regions`.
- **The thirteen producers' own systems** — regions and conquest, quests, characters, mastery
  keystones, Vows, traits, gems and sockets, element sets, screen unlocks.
- **Audio** (`design/gdd/audio-system.md`) — `sfx_dispatch`, peak 0.17, `MinGapMs` 900.

## 7. Tuning Knobs

| Knob | Value | Range | What it does |
|---|---|---|---|
| `Inbox.Cap` | 60 | 20–200 | How many letters are kept. Read rows go first; unread rows are never dropped. |
| `Dispatches.FirstVersionWithInbox` | 8 | **frozen** | The save version at which the inbox exists. Never re-point this at `CurrentVersion`. |
| `sfx_dispatch` peak | 0.17 | 0.10–0.30 | The arrival cue's loudness. Background news must not sound like a reward (`sfx_levelup` is 1.00). |
| `SoundBank.MinGapMs["sfx_dispatch"]` | 900 | 500–2000 | One cue when a conquest or a migration posts several letters in one frame. |
| Arrival pulse | `UiMotion.Transition` (0.18 s) | a named band | The halo behind the envelope. Skipped under Reduced Motion. |
| Unread dot | `Control(12)` | ≥ 8 px | The mark's size, matching the panel rows' own dot. No breath — the rail already has one breathing dot. |
| `FirstDispatchOpened` priority | 28 | 1–100 | Below `FirstWarrenReturn` (30), above the back half (26). |

## 8. Acceptance Criteria

| # | Criterion | How it is proved |
|---|---|---|
| AC1 | A background event writes exactly one letter, whatever its producer does, however many frames it asks on | `dispatches_test` (once-never-twice; same key, different payload, ignored); `dispatch_producers_test` |
| AC2 | No background event posts a notice toast; the toast carries only direct feedback | `dispatch_producers_test` (the channel scan over every `PostNotice` site) |
| AC3 | A letter's words are rendered from the catalogues, never persisted, and a missing subject falls back without printing an id | `dispatches_test` (a Theory over all eleven Kinds, plus the fallback cases) |
| AC4 | A granted Vow reads "A VOW IS OFFERED TO YOU"; a discovered one reads "A VOW HAS REVEALED ITSELF"; the key is the same | `dispatches_test.test_a_vow_letter_says_whether_it_was_offered_or_proved`; `production/qa/evidence/dispatches/dispatch_vow_{granted,proved}_*.png` |
| AC5 | A pre-v8 save opens with an empty inbox, no unread mark, and its history Known | `dispatches_save_test`; `check_boot.sh`'s v4 and v5 lanes (`unread=0 … seeded=True`, `known` non-empty) |
| AC6 | A v8 save is believed as written — its unread letter survives a boot, and nothing is seeded | `check_boot.sh`'s v8 negative-control lane; `dispatches_save_test` |
| AC7 | The cap drops the oldest **read** row and never an unread one | `dispatches_test` (cap prunes; `Cap + 1` unread rows all survive) |
| AC8 | Opening the panel reads nothing; a letter is read only by opening it | `check_boot.sh`'s screen walk (it opens DISPATCHES and the unread letter must survive); `catch_up_tick_test` |
| AC9 | A letter arriving mid-read neither swaps the pane nor marks itself read | `catch_up_tick_test.test_a_letter_arriving_mid_read_neither_swaps_the_pane_nor_reads_itself` |
| AC10 | Every control in the panel is reachable by mouse and by keyboard, at 100 / 125 / 150 % | `chrome_reflow_test`, `page_layout_test`; `production/qa/evidence/dispatches/dispatches_*.png` |
| AC11 | The envelope and its mark do not paint while anything above the coach's rung owns the frame | `attention_owner_test`; `production/qa/evidence/dispatches/chestdispatch_during_*.png` |
| AC12 | No dispatch is surfaced over the authored opening | `check_opening_trace.py` ("no dispatch was surfaced over the opening", plus its non-vacuity wiring check) |
| AC13 | The arrival cue is the quietest in the bank and fires at most once per 900 ms | `make_sfx.py --measure` (peak 0.17); `SoundBank.MinGapMs` |
