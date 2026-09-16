# ADR-007 — One surface at a time: the attention owner, and the inbox background news goes to instead

| Field | Value |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-15 |
| **Deciders** | technical-director + ux-designer (autonomous session, per standing user instruction) |
| **Related** | ADR-006 (Update owns every edge; both halves read one geometry), ADR-005 (UI SCALE is a density profile, the cursor is mapped once, motion has one vocabulary), ADR-001 (Core owns the arithmetic; the Game assembly owns MonoGame types) |
| **Enforced by** | `tests/unit/IdleXIdle.Game.Tests/attention_owner_test.cs`, `host_input_gates_test.cs`, `dispatch_producers_test.cs`, `notice_toast_honesty_test.cs`, `nav_chain_test.cs`; `tests/unit/IdleXIdle.Core.Tests/Progression/dispatches_test.cs`, `Persistence/dispatches_save_test.cs`; `tools/check_boot.sh` (three save lanes), `tools/check_opening_trace.py`, `tools/check_all.sh` |
| **Scope** | the Game assembly's foreground surfaces and the clocks under them, plus the Core inbox (`Dispatches`) and its save columns. Nothing in this decision reaches gameplay arithmetic. |

## Summary

Nine surfaces could speak over the same frame — a chest reveal, a coach light, a slot card, three
toast channels, a fall, the open log, the authored opening — and each one kept its own hand-written
list of the others it yielded to, so news crossed a screen the player was reading and clocks expired
behind whatever covered them. The host now computes **one** `AttentionOwner` per Update, every
foreground painter and every transient clock asks that one field "is anything above me up?", and the
thirteen producers of *background* news were moved off the toast entirely onto a persistent, typed
inbox (DISPATCHES) that waits to be read.

## Context

### The problem

The product rule this pass exists to keep is one sentence:

> **The game must not ask the player to look at two things at once. If the player is already looking
> at a meaningful event, new information waits.**

Three things broke it.

**1. Every gate kept its own list.** `CoachLightsIt` named `_tourActive || _showTitle || _showSettings
|| _showHelp || WelcomeUp || LogOpen`, then `RevealActive`, then `DeathTransitionUp`.
`HuntLessonShowing` named a different eight. `SlotShowing` named six. `DrawNoticeToast` named seven
and also asked `ReserveNoticeLane` and `DrawBootToast` to agree with it by hand. Six hand-listed
predicates for one question, and a surface added to the game was a surface nobody remembered to add
to five of the lists. `NoticeToastHolds` had the ranking upside down: a six-second toast silenced a
lesson the player was meant to act on.

**2. Background news behaved like foreground UI.** A screen unlocking, a quest finishing, a champion
joining, a keystone taught, a Vow offered, a trait waking, the first gem, a set completed, a region
conquered — thirteen producers, all of them things that happened *somewhere else*, all of them posting
a plate across whatever screen the player had chosen to be on, all of them gone in six seconds
whether or not anyone read them. Two of them (`PostAwakening`'s combined plate, the conquest
keystone's four-line reveal) could not physically fit their sentence in the plate's two-line budget
and were being truncated.

**3. A fall was announced twice and read nowhere.** The old fall plate — `RECOVERING — BACK TO WAVE
N`, three verdict rows, a `GO AGAIN` door — stood over the arena after every death in an *idle*
auto-battler, where dying is a normal part of the loop. It taught a FirstRetry lesson for pressing a
button the game was going to press by itself, and the run's own report was already being written to
the EXPEDITION LOG on the same frame.

### Constraints

- **No framework.** No event bus, no notification system, no ECS, no retained UI library. The brief
  forbade all four by name, and ADR-006's own rejection of "a retained UI framework, MVVM, or an
  event bus" applies unchanged: the architecture is otherwise working.
- **MonoGame 3.8.4.1** ships no UI, no modal stack, no z-ordered surface manager. Whatever decides
  who is on top has to be ours.
- **ADR-006 holds throughout.** Update owns every edge; Draw paints. Anything this decision adds to a
  Draw must be a read of state Update already settled.
- **Offline parity.** `OfflineHunt` charges `Descent.DownedSeconds` per fall while the game is shut.
  Anything the live fall does must fit inside that beat, or a session played with the window open
  earns a different number of waves from one played with it closed.
- **The save is versioned and migrated by version**, never by field presence (`SaveGame`'s own rule).

## Decision

### 1. One owner, one field, one assignment site

`AttentionOwner` (`src/IdleXIdle.Game/AttentionOwner.cs`) is an enum whose **order is the ranking**:

```
None < Feedback < Coach < Report < Death < Reveal < Modal < Opening
```

| Tier | What it is | What it looks like |
|---|---|---|
| `None` | nothing has a claim | the page is the whole picture |
| `Feedback` | transient answer to something the player just did | a notice toast, a locked-tile refusal, the settings panel's COPIED line |
| `Coach` | the coach is lighting a control | scrim, brackets, a card beside them; the hint slot and the slot note are this rung too |
| `Report` | the EXPEDITION LOG is open | a full-screen read the player chose |
| `Death` | the death transition is on the page | the collapse, the black, the stage coming back |
| `Reveal` | a chest reveal | the one moment the game asks the player to stop and look |
| `Modal` | a production modal or confirmation | title, settings, help, a tour, the welcome, DISPATCHES, an attunement, the vault's stall, a SELL/SALVAGE question |
| `Opening` | the authored opening is running | nothing else speaks |

The host holds one field, `private AttentionOwner _attention;`, assigned **exactly once**, in
`Game1.Update`, at the point where the frame's flags are final: after the F1/F10 lines, the welcome
keys, the gear/learn/hint click block, the nav/L/T handling and `_forge.TickReveal(...)`, and before
`UpdateExpedition` runs the fight, the coach and the director. Every reader asks one question:

```csharp
private bool AttentionOwnedAbove(AttentionOwner tier) => _attention > tier;
```

`attention_owner_test` pins the enum's order by reflection, pins that
`\b_attention\s*=(?!=)` matches exactly once across every `Game1*.cs`, and pins the site's place in
the frame (the L handling < `TickReveal` < the assignment < the authored-hold line).

**Two facts are read one frame late, deliberately.** The death transition and the coach's choice both
tick inside `UpdateExpedition`, which runs *after* the site; reading them again later would be a
second owner. At 60 fps one frame is a frame nobody sees. The clock ticks at the top of `Update`
likewise read last frame's owner.

**The death owns the frame only while it is ON the page** (`!OverlayActive && DeathTransitionUp`).
The fight ticks on every screen, so a fall behind the Forge is not something the player is looking
at; without that term a background death nulled the coach's selection for ~2 s and re-armed the rail
tile's blaze on return.

**The coach owns the frame only when something is actually lit** (`_coach.Showing is { } aimed &&
CoachHoles(aimed).Length > 0`). A beat raised about a fight control *aims* while the player is on the
Forge, but paints no hole there — and a light that paints nothing must not hold the news back. This
was found by a picture, not by reading: the first cut used "aims", and news stayed queued for ever
behind a spotlight that did not exist.

### 2. The priority rule, stated once, for the surfaces that share a rung

Ranks are compared, never enumerated. Two rules follow from that and are easy to get wrong:

- **A gate guards at the rung of the thing it paints, and `AttentionOwnedAbove` is strictly greater.**
  So a surface AT the coach's rung is not blocked by `AttentionOwnedAbove(Coach)` — the coach does not
  outrank itself. The WARREN photographed exactly that: a lit TRAINING lesson (scrim, brackets, card)
  with the screen's own `AN UPGRADE IS AFFORDABLE` slot note standing over the same page. The slot is
  where a lesson goes when it is *not* lit, so it guards at `Feedback`, one rung below.
- **Surfaces that share one painted place answer at ONE rung.** `SlotShowing()` is the only question
  anyone may ask about the hint slot, and `ScreenBannerShowing()` — the note it may contain — answers
  at the same `Feedback` rung. They disagreed until 2026-09-16 (banner at `Coach`, slot at
  `Feedback`), and the difference is how a gold outline reached the BUILD screen's first empty row
  with no sentence anywhere on the screen to explain it. Both now guard at `Feedback`; the slot is
  still the banner's only caller, and `attention_owner_test` pins both.

The `?` (LEARN THIS SCREEN) and the envelope guard at `Coach`: live over a lit lesson, gone under the
open log and everything above it. That includes the death transition, so the `?` and the envelope
blink out for the ~2 s of a fall on the hunt. This was ruled, not overlooked: an exception ("gone
under Report and above, *except* Death") is the hand-listing this decision exists to delete.

### 3. What waits, what expires, and what is skipped

A surface that loses the frame does one of three things, and which one is a product judgement per
surface, written at the site:

| Behaviour | Surfaces | Why |
|---|---|---|
| **Pauses** — does not paint, and its clock holds | the notice toast (and the next one is not dequeued, so its cue is not spent either), the boot toast, the coach's spotlight and card, the hint slot, the director's `_quiet` | It is information. A clock that burns behind a cover is news that expired unseen. |
| **Expires** — does not paint, clock runs | the locked-tile refusal (`_lockedTimer`) and the settings panel's COPIED line (`_feedbackToastTimer`) | Each answers a press the player has just made and knows about. A refusal is over the moment it was seen, or not; the COPIED line's clock ticks on every screen including the title, because the panel opens there too. |
| **Deferred once** — latched, fires exactly once when the frame frees | the nav rail's unlock CHAIN BREAK, the envelope's arrival pulse and cue, the currency pills' banked "+N" | A once-per-screen-per-career ceremony spent on nobody is the only one that career had. The pills' accumulator IS the wait: it keeps filling and empties into one badge at its true size, on the first free frame that also carries a gain. A spend in between cancels it. |

**Which rung a deferred flourish waits at is a decision per flourish, not one rule.** Two of the three
wait for the coach's rung to clear (`!AttentionOwnedAbove(Coach)`); the envelope's arrival waits for a
frame **nothing** has a claim on (`!AttentionOwnedAbove(None)`), and it is the only gate in the host
that asks that question. The reason is the spec's own ranking — direct feedback outranks background
news — read literally: a letter arriving IS a message, a notice toast or a locked-tile refusal is a
message the player has just asked for, and the letter's flourish is *audible*, so firing it under a
toast stacks two cues and two claims on the eye. The rail's chain break is silent, plays on the rail
rather than in the toast's own band, and is the only one that career will ever get, so holding it
behind a six-second plate that shares no pixels with it would cost more than it saves. The NEW dot's
halo is a two-second cycle with nothing to defer at all.

> **The `Feedback` rung is load-bearing only through that one gate.** Every other reader asks
> `AttentionOwnedAbove(Feedback)` or `(Coach)`, `>= Modal`, or `== Death` — none of which can tell
> `Feedback` from `None`. So the tier earns its place in the enum by ranking the toast *below* the
> coach (which is what stops a toast silencing a lesson, the inversion `NoticeToastHolds` had), and by
> this one gate. A future surface that must yield to direct feedback asks the same question.
> Note also that `_feedbackToastTimer` is painted only inside `DrawSettings`, so as an owner term it
> can raise `Feedback` only in the ≤ 4 s window after the panel closes — a state in which the line it
> stands for is no longer on screen at all.
| **Skipped** — never plays | the HUNT tile's hurt and cleared washes, the NEW dot's breathing halo | The bite is over. A red wash landing a minute later reports a fight that has moved on. The halo is a two-second cycle, not a one-shot, so there is nothing to defer — it simply resumes. |
| **Untouched** — state, not motion | the NEW dot itself, the rail's count badges, the pills' printed figures, the HUNT tile's health bar | "You have not looked" stays true under every owner. |

`UiMotion.Tick` is **never** paused: what waits is the arm, so every hover ease in the game is
unaffected.

The capsule's tint (`_pillFlash`) is deliberately *not* gated, and the reason is written where it is
armed: the printed figure walks to its new value under every owner, so a rim saying "this moved"
reports nothing the number beside it is not already showing. The "+N" is a new thing appearing beside
the chain, which is why that one waits.

### 4. The fall is a transition, not a plate

The fall plate is gone — `FallPlate`, `FallDoor`, `FellBanner`, `RestartWave`, `DevRunToRegroup`, the
`fightregroup` capture mode and the `RECOVERING — BACK TO WAVE N` header branch with them — and the
FirstRetry lesson with it. Two presented failure lessons remain (`READ THE LOG`, `MAKE ONE CHANGE`)
and the third fact, `RetriedAfterChange`, is recorded silently for the `ftue_loop_lived` ledger line.

What replaces it, entirely inside the existing 1.6 s `Descent.DownedSeconds` beat:

| From the fall frame | What is on screen | Clock |
|---|---|---|
| 0 → 1.25 s | **Readable death.** The red `_deathFlash` (unchanged, behind SCREEN FLASH), the death clip, then its clamped last frame. The HUD is painted **and live**: the medallion hovers, tips and opens the log as at any other time. | `_downedTimer` 1.6 → 0.35 |
| 1.25 → 1.6 s | **Fade out**, α 0 → 1 over `DeathTransition.FadeOut` (`UiMotion.Reward`, 0.35 s) | `_downedTimer` 0.35 → 0 |
| 1.6 s | **The restart, at full black.** `StartRun` on the frame `_downedTimer <= 0`, exactly where it always was. | `_deathFadeIn` armed |
| 1.6 → 1.7 s | **Black held**, `DeathTransition.Hold` (`UiMotion.Fast`, 0.10 s); the new wave's entrance is already walking in underneath | `_deathFadeIn` 0.45 → 0.35 |
| 1.7 → 2.05 s | **Fade in**, α 1 → 0 over `DeathTransition.FadeIn` (`UiMotion.Reward`, 0.35 s) | `_deathFadeIn` 0.35 → 0 |

**Why inside `DownedSeconds`: economy parity.** `OfflineHunt` charges exactly `Descent.DownedSeconds`
per fall. `StartRun` still fires on the frame `_downedTimer` reaches zero, so a session played with
the window open and one played with it closed pay the same for a death — the transition rides *inside*
the beat the simulation already budgets. Only the lift (0.45 s) extends past the restart, and it is
paint alone: the new descent is already running under it.

**No new motion band.** Every duration is a named `UiMotion` value (ADR-005's one vocabulary).

**The black is HuntScreen-owned**, painted as one full-canvas fill in the fight's unclipped HUD pass
after the red flash. Host chrome (rail, pills, gear) paints over it and keeps working, because its
input is not blocked. The fight's own HUD refuses input **exactly while the fill is visible** —
`DeathTransition.Covers(alpha) => alpha > 0f` — and Draw parks the cursor under the same predicate,
so a refused medallion cannot lift, whiten or offer a tip through the black. That is ADR-006's rule
read in both directions: a control whose input is blocked must not paint as interactable, *and* the
readable phase paints the medallion, so the readable phase must answer it.

A beat of quiet (`OnboardingDirector.QuietAfterReward`) is hushed on the edge the on-page transition
ends, so `READ THE LOG` lands once the arena is legible rather than on the first lit frame.

### 5. Reduced Motion

The transition is a **UI transition** in `accessibility-settings-system.md`'s suppression table
("Menu/UI transitions — slide, fade, easing"), so it is suppressed: a hard **cut** to black where the
fade would begin, the black through the restart and the `Hold`, a hard cut back. `FadeInSeconds(true)
= Hold`, so the transition *ends* at the cut and there is no 0.35 s tail where input is refused on a
visible stage. End state identical; nothing is lost but the easing.

**It is not a flash hazard.** Within roughly one second the screen shows two events — the red death
flash and the cut to black — against WCAG SC 2.3.1's three-flashes-per-second threshold (the same
threshold that `art-bible`'s telegraph wind-up floor cites for the same reason). Two events in a
second is comfortably clear, and under Reduced Motion the fades collapse to cuts, not to strobes.

### 6. DISPATCHES: background news is a letter, not a plate

**The channel split.** A message belongs on the toast if it is the **answer to something the player
just did, on the screen they did it on**: a champion switch (`YOU ARE THE ANVIL`), a locked-tile
refusal, `RepairForSwitch`'s shed line, the boot/save line, COPIED. Everything else — thirteen
producers — is *account news that happened somewhere else*, and goes to the inbox.

**The model is typed rows, not text** (`src/IdleXIdle.Core/Progression/Dispatches.cs`):

```csharp
enum DispatchKind { Unlock, Gem, Trait, Vow, Keystone, Champion, Quest, Set, Region, Socket, Migration }

sealed record Dispatch(string Key, DispatchKind Kind, long AtMs, bool Read,
                       string? SubjectId = null, string? RegionId = null, int? Count = null);
```

No product text is ever persisted. `DispatchCopy.Headline(d)` / `Body(d)` render from the catalogues
at display time, so a renamed keystone, a re-worded quest or a retuned Vow reads correctly in a
letter written six months earlier, and a subject no longer in a catalogue falls back to a plain
sentence rather than printing a raw id. No `Dictionary<string, object>` anywhere: the payload is three
nullable, strongly typed columns.

**Semantic keys are the dedupe.** `DispatchKeys` builds one stable key per event —
`unlock.<screen>`, `quest.<id>.complete`, `hunter.<id>.joined`, `keystone.<id>`, `vow.<id>`,
`trait.<id>`, `set.<source>.complete`, `region.<id>.conquered`, `gem.first`, `socket.first`,
`migration.<what>`. `Inbox.Post` returns false and writes no row if the key is already **Known**, so a
producer may ask every frame and the inbox tells it once. `Inbox.Know(key)` records a key as known
with **no row** — for news the account lived but must not be told.

That is what makes the producers crash-safe, and it is the contract every one of them meets: each
letter is either **asked for from a fact the file already carries** (`socket.first` from the earned
socket count, `gem.first` from gems held, `set.*` from `CompletedSets`, `keystone.*` from the freshly
derived list), or **posted in the same frame that fact is saved** (quest, champion, conquest), or
**self-healing because the fact is not saved until the letter is** (a screen reveal, a Vow, a trait
awakening).

**Cap and prune.** `Inbox.Cap = 60`. Over the cap the **oldest READ** row is dropped; if none is read,
the cap *yields* rather than throw away an unread letter. `Known` is never pruned, so a pruned row's
key can never re-post as news.

**Read and unread.** A letter is read by opening it in the reading pane, or — for a champion —
by walking to the ROSTER screen, whose one sentence is "switch hunter on the ROSTER screen". That is
the only place a dispatch is marked read by something other than the pane, and it is deliberate.

**Persistence: SaveGame v7 → v8.** Three columns after `TutorialsDone`: `List<SavedDispatch>
Dispatches`, `List<string> KnownDispatchKeys`, `bool DispatchesOpenedEver`. The DTO is flat and
nullable-column, `Kind` is written by NAME, an unknown Kind is dropped on load rather than crashing
the boot, and duplicate keys collapse to the first.

**The migration rule: seeded as known, never as unread.** `Dispatches.FirstVersionWithInbox = 8` is a
frozen literal — never `SaveGame.CurrentVersion` — the fourth constant in that family. A file **below**
8 has its history read into `Known` with **no rows**: every screen already open, every region
conquered, every trait, keystone, Vow, quest, champion and set it holds, plus `gem.first`,
`socket.first` and the two `migration.*` one-shots. A file **at or past** 8 is believed as written: an
unread letter stays unread and nothing is seeded. So a veteran's first boot on v8 opens on an empty
inbox and no unread mark, and the next real event is the first letter they get. A fresh save is seeded
nothing and is told everything, in order, as it happens.

The cost, stated: a veteran never receives the one class of "first" news a fresh player gets. That is
the correct trade — the alternative is a wall of sixty letters about a life already led — and nothing
is lost mechanically, because every fact is visible on its own screen.

**The surface.** The envelope joins the chrome chain left of the `?` (right to left: gear · ? ·
envelope · capsules), guards at the `?`'s RUNG but on the owner alone — `DispatchesOffered() =>
!AttentionOwnedAbove(AttentionOwner.Coach)`, where the `?` additionally needs the screen to have a
tour — and opens with **M** or a click. The panel is a host modal (`Game1.Dispatches.cs` for layout and paint; the input lives in
`Game1.Update`): a newest-first list on the left, a reading pane on the right, `MARK ALL READ — R` at
the foot, Escape or M to close. The pane starts **empty** and a letter opens only on a click or ENTER
— opening the panel must not mark the one letter waiting as read before a word of it has been read.
Selection is held **by key**, never by row index, because `Inbox.Rows` is newest-first and an arrival
shifts every index: a letter arriving mid-read would otherwise have moved the pane onto it and marked
it read. The unread mark on the envelope is a **state**, not a pulse: it holds until the letters are
read.

One onboarding lesson, `FirstDispatchOpened` — eligible on `DispatchesUnread >= 1`, completed only by
`DispatchesOpenedEver` (the real open, never a closed card), priority 28, `SoftGuide`, `Sends: null`
because the envelope is on every screen and is no screen.

### 7. Input (ADR-006, carried unchanged)

Nothing here moves an edge out of `Update`. Concretely:

- Every new control — the envelope, the panel's rows, its close, `MARK ALL READ`, the wheel, M, R,
  UP/DOWN, ENTER, Escape — is decided in `Update`. `DrawDispatches` and `DrawDispatchButton` name no
  edge, write no read-mark, call no `Save()`.
- **One geometry, read twice**: `DispatchesFrameNow()` and `DispatchRowAt(...)` are the single
  resolvers the paint and the hit-test share; `DispatchIndexOf(rows, key)` is the single resolver for
  "which row is this letter".
- **Nothing clickable paints while it is blocked**, and its converse: the rail is no longer painted
  over the open log (it used to paint at full brightness above the log's scrim while refusing every
  click); the fight HUD parks its cursor exactly while the black covers it and is fully live during
  the readable beat; the `?` and the envelope are not painted on frames their input is refused.
- `HostModalUp => _showSettings || _showHelp || _showDispatches` replaced every site that spelled the
  first two by hand, so the panel could not be added to thirteen places and forgotten in the
  fourteenth.
- `check_draw_purity.py` is green with zero excused sites, before and after.

### 8. The rig tells the truth

A capture must photograph the game's own answer. Dials that pose a deferred flourish —
`RH_SHOT_BREAK` (the chain break) and `RH_SHOT_MOTION` (the banked "+N") — ask the same owner
question the real arm asks, so a fixture that poses one under a chest reveal is answered with the
chrome the player would actually have been shown. Dials and modes whose surface was deleted are
retired loudly rather than left forwarding into nothing: `RH_SHOT_WAKE` and the `keystonenotice` mode
carry retirement notes in `capture.sh` that point at where the same thing is read now, and
`fightregroup`'s removal is pinned by name in `capture_rig_modes_test`, which fails if it comes back.
The inbox is **written** under the rig, because it is state; only its surfacing is posed.

## Alternatives considered

**An event bus / notification framework.** The obvious shape: producers raise events, a manager owns
priority, queueing and lifetimes, surfaces subscribe. Rejected, and forbidden by the brief. The
priority question here is answered by a single `>` on an enum, evaluated once a frame; a bus would add
a registration lifecycle, a subscription graph and an ordering problem of its own to replace one
comparison. It also moves the *reason* a surface waits out of the surface and into a table, which is
the opposite of what made the six hand-listed predicates survivable in review — every gate in this
design says at its own site which rung it guards at and why.

**A z-ordered modal stack.** Genuinely models "who is on top", and would have to model paint order,
input capture and lifetime per surface. The game already has a fixed paint order that works (three
batches, chrome last); what it lacked was a single answer to "may I speak", which is one field.

**Keep the toast, add a priority number to it.** Cheapest, and it fixes nothing: the toast's problem
is not its ordering, it is that a six-second plate is the wrong shape for news whose sentence does not
fit in two lines and which the player may want again tomorrow. Two of the thirteen producers were
already being truncated.

**Persist the deferred flourishes.** A chain break latched under a modal, quit before it plays, is
lost for that career. Persisting the latch would replay a ceremony on the next load, which is exactly
what `nav_chain_test` forbids. Accepted the loss: the tile is open and the letter is in the inbox
either way.

**Keep the fall plate and merely gate it.** Leaves an announcement over an arena for an event that is
part of the loop, and leaves `GO AGAIN` teaching a button the game presses itself.

## Consequences

### Positive

- One question answers "may I speak", and it is written in one enum and one comparison. A surface
  added to the game asks the same question, at its own rung, at its own site.
- News survives being missed. A letter waits until it is read; a toast clock that would have burned
  behind a chest reveal now holds and lands when the reveal closes.
- Two sentences the toast could not hold are now read in full — the conquest keystone's reveal and
  the trait awakening, which is now **named** per trait where the combined plate named none of them.
- A champion who joined while the game was closed is told. The old load-frame skip that suppressed it
  went with the session flag it was built on.
- A fall is quieter and faster to read, and costs the same as an offline fall to the frame.

### Negative

- **The `?` and the envelope blink out for ~2 s on every fall on the hunt** (Coach rung, Death
  outranks it). Reversible in one line if the blink is ever judged worse than the rule.
- **The locked-tile refusal can be lost.** A player who clicks a locked tile while a lesson lights
  another tile hears `sfx_error` and sees nothing. Ruled: a refusal is Feedback tier and may expire.
- **A deferred ceremony can be lost for good** (above).
- **The rail's "the fight is alive over there" ticks are weaker** during long stretches inside modals.
  The tile's health bar still carries it every frame.
- **The chrome chain got 70 px longer** (see Risks).
- **Two producers ask the inbox every frame** — a short interpolated key and a `HashSet` lookup, beside
  a `List<Keystone>` that method already allocates each frame. Cheap, and recorded rather than hidden.
- **One-frame seams**, all accepted: the clock ticks and `ReserveNoticeLane` read last frame's owner;
  the death and the coach's choice are last frame's at the site.

### Neutral

- `Busy` gained a `DeathUp` term and `BelongsToTheMoment` lost two exemptions
  (`FirstFailureReport`, `FirstPostFailureChange`), which the owner now covers.
- `SaveGame.CurrentVersion` is 8, so every v7 file is copied aside by `SnapshotBeforeUpgrade` on
  first load.
- `check_boot.sh` grew three inbox assertions (v4, v5 and a v8 negative control).

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| A new surface is added and guards at the wrong rung (or at its own rung with `AttentionOwnedAbove`, which does not block it) | Medium | Two cards on one screen — the exact defect this ADR removes | §2's rule, stated at both sites and pinned by `attention_owner_test`; the capture matrix photographs every owner state at three densities |
| `Inbox.Cap` is reached by a career that never opens DISPATCHES (unread rows are never pruned) | Low | The list grows past 60 and the save file with it | Deliberate: an unread letter is never thrown away. Recorded as a remaining defect; a future pass may age unread rows out |
| The chrome chain overruns `PillChainMinLeft` and the screen title reads through the capsule gaps | **Occurring** | Cosmetic; a legible title fragment among the currency figures at 100/125/150 | Pre-existing (the chain was 625 px past the advisory floor before this pass; the envelope added 70). Compaction cannot reach it — it drops decimals only, and the width is six ornate capsules. The fix is fewer capsules, a compact capsule, or a title that yields: a chrome decision, deliberately out of this pass's scope. Photographed in `production/qa/evidence/dispatches/producers/chain_*_150.png` |
| A rail-targeted lesson's light is nearly invisible (the coach's scrim paints before `DrawHexNav`, so the rail is neither dimmed nor brightened) | **Occurring** | A lesson pointing at the rail is weakly lit | Pre-existing paint-order work on the chrome; reported with its measurement (the TRAINING label is 169,165,158 lit and unlit, while the arena falls 82 → 31) rather than fixed by reordering the draw chain in this pass |

## Performance implications

| Metric | Before | After | Budget |
|---|---|---|---|
| CPU (frame time) | — | one enum comparison per gate, one owner derivation per Update; a few dozen short interpolated keys and `HashSet` lookups per frame in the re-derived producers | 16.6 ms |
| Memory | — | the inbox is ≤ 60 records of one string key, an enum, a long and three nullables, plus a `HashSet<string>` of known keys | 512 MB texture ceiling, unaffected |
| Draw calls | unchanged | the envelope is one more icon in an existing chrome batch; the panel is a modal, drawn only while open | ~10–20 `SpriteBatch` boundaries |
| Load time | unchanged | the v7→v8 seed is one pass over facts the loader already reads | — |

## Migration plan

1. `SaveGame.CurrentVersion` 7 → 8; `SnapshotBeforeUpgrade` copies every v7 file aside on first load.
2. `LoadOrStartFresh` restores rows and known keys and parks the save; `SeedExplained` calls
   `Dispatches.SeedKnown(save, activities, inbox)` only when `_saveVersionSeen <
   Dispatches.FirstVersionWithInbox`, after `Reveal.Restore` has settled which screens are open.
3. `Save()`'s `with` block writes all three columns.
4. **Verify**: `check_boot.sh`'s three lanes — a v4 and a v5 file must print `unread=0 … seeded=True`
   with a non-empty `known` and a `save.pre-v8-*.json` snapshot; a hand-written v8 file must print
   `seeded=False` with its seeded key still unread.

**Rollback**: the inbox is additive. Reverting means restoring the thirteen `PostNotice` calls and
dropping the three save columns; a v8 file read by a v7 build loses the inbox and nothing else, and
the pre-v8 snapshot is on disk either way.

## Validation criteria

- [x] Exactly one `_attention` assignment across every `Game1*.cs`, at the pinned place in the frame.
- [x] Every foreground gate reads the owner and hand-lists none of `_showSettings`, `_showHelp`,
      `_showTitle`, `_tourActive`, `WelcomeUp`, `_showTypeSpec`, `LogOpen`, `RevealActive`,
      `DeathTransitionUp`, `_opening.Running`, `SpecialisationOpen`.
- [x] No notice toast, and no surfaced dispatch, over the authored opening — asserted by
      `check_opening_trace.py`, with non-vacuity checks that the recorders are still wired.
- [x] Every rail tile's chains come off exactly once, and never before the last authored card is
      answered.
- [x] `check_draw_purity.py`: zero findings, zero excused.
- [x] A pre-v8 save opens with an empty inbox and no unread mark; a v8 save is believed as written.
- [x] 75 captures across the owner states at 100 / 125 / 150 %, plus Reduced Motion, inspected
      (`production/qa/evidence/attention/README.md`).

## ADR dependencies

| Field | Value |
|---|---|
| **Depends on** | ADR-006 (Accepted) — every control this decision adds resolves its edge in `Update` and shares one geometry with its paint. ADR-005 (Accepted) — the transition's durations are existing `UiMotion` bands and the chrome is laid out at the density profile. ADR-001 (Accepted) — the inbox, its keys, its copy and its save DTO are pure Core. |
| **Enables** | Any future background-news producer: it posts a typed row under a semantic key and is done. |
| **Blocks** | None. |
| **Ordering note** | The owner must exist before a surface can be gated by it; the inbox model and its save version must exist before any producer migrates to it. Both held in the implementation order. |

## Engine compatibility

| Field | Value |
|---|---|
| **Engine** | MonoGame 3.8.4.1 (`docs/engine-reference/monogame/VERSION.md`) |
| **Domain** | UI |
| **Knowledge risk** | LOW — nothing here touches an engine API beyond `SpriteBatch` draws and `Keys`/`MouseState` reads that ADR-005 and ADR-006 already cover |
| **References consulted** | `docs/architecture/ADR-005`, `ADR-006` |
| **Post-cutoff APIs used** | None |
| **Verification required** | None engine-specific. The catch-up-tick behaviour this design inherits from ADR-006 is the only framework contract it leans on, and it is unchanged. |

MonoGame ships no UI framework, no modal stack and no notification system, which is why this decision
exists at all — but nothing in it is version-specific. It would hold on any MonoGame that keeps the
XNA game-loop contract.

## GDD requirements addressed

| GDD document | System | Requirement | How this ADR satisfies it |
|---|---|---|---|
| `design/gdd/dispatches.md` | Dispatches | "Background news is a letter that waits in an inbox until it is read; nothing about somewhere else crosses the screen the player is on" | The thirteen background producers post typed rows under semantic keys; the toast keeps direct feedback only |
| `design/gdd/dispatches.md` | Dispatches | "A letter arrives once per event, survives a reload, and is never re-told" | Semantic keys are the dedupe; `Known` is persisted and never pruned |
| `design/gdd/dispatches.md` | Dispatches | "A veteran's first boot after the inbox ships shows no wall of unread history" | `SeedKnown` marks a pre-v8 file's history known with no rows |
| `design/gdd/game-flow.md` | Game flow | "The first gem drop writes a DISPATCH, a NEW mark on FORGE and a two-card tour" | `gem.first`, re-derived from `GemsHeld() > 0` |
| `design/gdd/onboarding-tutorial-system.md` | Onboarding | The failure loop teaches READ THE LOG and MAKE ONE CHANGE; the retry is recorded, not asked for | FirstRetry deleted; `RetriedAfterChange` kept as silent telemetry |
| `design/gdd/accessibility-settings-system.md` | Accessibility | Reduced Motion suppresses menu/UI transitions | The death transition cuts rather than fades; end state identical |

## Related

- `docs/architecture/ADR-006-draw-must-not-consume-input.md` — the input invariant carried here unchanged.
- `docs/architecture/ADR-005-ui-density-profile-and-one-cursor.md` — the motion vocabulary and the density profile.
- `design/gdd/dispatches.md` — the quick-design spec for the inbox.
- `src/IdleXIdle.Game/AttentionOwner.cs`, `Game1.cs` (the owner site and every gate), `Game1.Dispatches.cs`, `PresentationBeats.cs` (`DeathTransition`), `HuntScreen.cs`.
- `src/IdleXIdle.Core/Progression/Dispatches.cs`, `DispatchCopy.cs`, `Persistence/SaveGame.cs`.
- `production/qa/evidence/attention/README.md` and `production/qa/evidence/dispatches/` — the 75 inspected captures.
