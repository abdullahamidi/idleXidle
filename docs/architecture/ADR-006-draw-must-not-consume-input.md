# ADR-006 — Draw must not consume input: Update owns every edge, and both halves read one geometry

| Field | Value |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-12 |
| **Deciders** | technical-director + ui-programmer (autonomous session, per standing user instruction) |
| **Related** | ADR-001 (Core owns the arithmetic; the Game assembly owns MonoGame types), ADR-005 (UI SCALE is a density profile and the cursor is mapped once) |
| **Enforced by** | `tools/check_draw_purity.py` (in `tools/check_all.sh`), `tests/unit/IdleXIdle.Game.Tests/catch_up_tick_test.cs` |
| **Scope** | the Game assembly's `Draw*` methods. Core has no input and is unaffected (ADR-001). |

## Context

MonoGame's `Game.Tick` makes **at least one** call to `Update` and **exactly one** call to `Draw`. Only
`IsFixedTimeStep = false` guarantees one Update per Draw, and this project never sets it — the constructor
(`Game1.cs`) touches only the `GraphicsDeviceManager`, the content root, `IsMouseVisible` and the window
title, so MonoGame's default fixed 60 Hz timestep is in force. On a frame that runs over budget the loop
therefore catches up: `Update`, `Update`, `Draw`.

The host latches the left-click edge once per Update, at the top:

```csharp
_clicked = _mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
```

and overwrites `_prevMouse` at the end of every Update, in `Latch()`. So on a catch-up tick the first
Update latches the edge, `Latch()` makes `_prevMouse` pressed, the second Update recomputes the edge as
**false**, and the single `Draw` that follows hit-tests an edge that no longer exists. A human holds the
button for three to six frames, so the edge never re-arms: the press is silently dropped.

The codebase had adopted the opposite convention deliberately, and wrote it down. `DrawSettings` carried
the reasoning in its own doc comment — *"Drawn (not updated) because `UiKit.Button` hit-tests as it
renders — the click edge is latched in Update precisely so Draw can read it"* — and `Game1.Update`'s
`if (_showWarren)` block said *"the dashboard handles its clicks in Draw (like Forge/Stats), so it needs
no Update here."* That reasoning is sound for a tick that updates once and wrong for one that does not.

**It stopped being theoretical on 2026-09-12.** The title screen shipped with exactly this defect: a
fresh-save validation pass at 150 % — the profile whose glyph atlas is re-keyed mid-Draw, so the most
likely to run over budget — lost a title-menu click. Worse, the harness had the bug written into it:
`tools/check_opening_trace.py` exempted title clicks from its "every clicked beat advanced on its first
click" check, with the mechanism described in the exemption's own comment. A gate that documents a defect
cannot fail on it.

Three screens had already migrated, for their own reasons, and proved the shape works: `MasteryScreen`,
`GearScreen` and `RosterScreen` resolve clicks in `Update`, take no edge in `Draw`, and paint their
buttons with `clicked: false`.

## Decision

### 1. The invariant

> **Update owns input and state. Draw paints.**

`Draw` **may** compute presentation geometry, render, read current state, read the cursor **position**,
read a **held** button (`UiKit.MouseHeld`) and derive hover. `Draw` **must not** consume a click,
right-click or wheel edge, read a key edge, or mutate semantic state — no navigation, no purchase, no
equip, no selection change, no tutorial acknowledgement, no preference change, no panel open or close, no
currency spent, no save written. Calling `Draw` repeatedly with no `Update` between must change nothing
semantic.

Hover is exempt because it is presentation and causes no action. The rule is about **action input**, not
about the word "mouse".

### 2. One geometry, read twice

> **What is drawn is what is hit-tested.**

A migrated control's rectangle is never written twice. Each is a named member or helper that both halves
call — the shape `MasteryScreen` already used (`TakeBtn`, `InspectorPanel`, `SpecSealBtn`). Where a rect
was solved inside a paint loop it is hoisted into an indexed or list-returning helper
(`TraitCardRects(count)`, `CardVisible(index)`, `SettingsToggleButton(x, y, width)`), and the loop then
calls the same helper. Copying a literal or an expression into `Update` is forbidden: coordinate drift is
a worse defect than the one being fixed, and it fails silently.

Two narrow exceptions are allowed, and only these two:

* **A layout that must measure text mid-paint.** The HELP panel wraps its columns against its own width
  and the WELCOME panel's height is its tiles plus its camp lines plus its resume line, so a pure layout
  function for either would have to re-run the measurement — a second geometry system, which is the one
  thing this decision forbids. `Draw` stores the rectangle it actually used (`_helpClose`, `_helpView`,
  `_welcomeContinue`) and `Update` hit-tests that stored rectangle on the next frame. This is the idiom
  the authored opening already used for the same reason (`Game1.Opening.cs`: `_openingButton`,
  `_prologueNext`, `_prologueSkip`, *"hit-tested next frame"*). It is safe because the rectangle cannot
  move without the content moving, and because both controls have a keyboard twin resolved in `Update`
  that never depended on `Draw`.
* **A rect cached for the frame by a pure measurement pass**, where the cache is never read as input
  state.

### 3. One edge, at most one action

A click edge may cause at most one semantic action. Where an action changes the screen, changes the
density profile (which relays out every rectangle), or opens a modal over a control under the same
cursor, the edge must be spent. The existing mechanisms are kept and reused rather than replaced:
`_swallowInput`, each screen's own suppress flags (`_modalOpenedNow` in `VaultScreen`, `ConfirmOpen` in
`ForgeScreen`), and — new for the host modals — `Game1._modalOpenedNow`, set by the click that opens
Settings so the panel's own input pass cannot also act on it. `UpdateSettings` returns immediately after
cycling UI SCALE for the same reason. No frame delays, no sleeps.

`_swallowInput` cannot do `_modalOpenedNow`'s job: it is also true while the authored opening owns the
frame, and SETTINGS is the opening's one way out.

### 4. The tutorial's input authority is untouched

The authored opening's authority stays exactly where it was. `Game1.MouseClicked` remains the single
choke point (`_clicked && !_showSettings && !_showHelp && !WelcomeUp && !_swallowInput`, where
`_swallowInput = _tourActive || _opening.OwnsInput`), and the screens that receive
`MouseClicked || ForcedScreenClick()` — the forced-target click a `ForceAction` beat lets through to the
control the light is cut from — still receive exactly that. Migration moves **where** an edge is read,
never **which** edge. No gate was added, removed, widened or narrowed.

This matters because `_swallowInput` and every modal flag are final before the screen `Update`s run
(`Game1.Update` settles them at the gear/learn/hint block, well above the `if (_showX)` dispatch), and
unchanged until `Draw`. So a gate evaluated in `Update` has the same value it had in `Draw`.

### 5. Requests stay requests

Domain ownership does not move. A screen still raises a request and the host still consumes it
(`ConsumeUpgrade`, `ConsumeOpen`, `ConsumeTraderBuy`, `ConsumeCue`, `Dirty`, `WantsLog`); `Update` simply
calls the same production methods the `Draw` path called. Where a screen reset a per-frame latch in
`Draw` (`TraitCollectionScreen`'s `Dirty = false`), the reset moves to the top of its `Update` — and the
host's consume then **must** move to sit immediately after that `Update`, the pattern MASTERY and GEAR
already use. Reading such a flag from the `Draw` site after the reset moved would lose it on a catch-up
tick and re-fire it on a tick where the screen's Update did not run.

## Consequences

* A dropped click of this class cannot survive a gate run: `tools/check_draw_purity.py` fails the build-free
  gate set on any edge-consuming mention inside a `Draw*` method in the Game assembly. It judges
  hit-testing widgets by their **edge argument** — `clicked: false` is the drawn-only form and passes —
  so it does not punish the correct pattern, and it says nothing about cursor-position or held reads.
* A site that must stay carries `// draw-input-ok: <reason>` (or `// draw-input-ok-method:` above the
  signature) and is printed by `--audit`, so the exception list is visible rather than invisible. The
  desired state is zero, and the gate's summary line reports how many are excused.
* **The gate is necessary and not sufficient, and its blind spots are known.** It reads one line at a
  time inside `Draw*` methods, so it cannot see:
  * **a state machine driven by a HELD button.** `UiKit.MouseHeld` is deliberately an allowed read, and
    the trait screen's drag-and-drop was built on it: `_wasHeld && !held` computed a release edge from
    two held reads, and the release called `DropTrait` — which equipped or unequipped a trait, wrote
    `Dirty` and played a cue — from the paint pass. Three `Draw`s with no `Update` between equipped a
    trait on the first and not the other two, and the gate reported the file clean.
  * **a mutation one call deep.** `Settle()` (Warren), `CloseRevealIfSettled()` → `AdvanceReveal()`
    (Forge) and `DropTrait()` (Traits) all mutate, and all were invoked from a `Draw` by a name the
    MUTATORS list does not contain. Naming every such helper would be an endless list; naming none
    would make the check useless. The list covers the verbs that spend, persist or advance.
  * **a latch reset.** `Dirty = false` (Traits) and `var click = _revealClick; _revealClick = false;`
    (Forge) are plain field writes, and a `Draw` writes fields legitimately all the time (hover keys,
    measured rows, cached layout), so flagging assignments would be pure noise.

  Every one of those was found by reading the screen, not by the gate. So the standing rule is: the
  gate stops a REGRESSION of the known shape; a screen that is newly touched still needs a human (or
  reviewing-agent) pass asking "what does this paint change?".
* `tools/check_opening_trace.py` no longer exempts the title from "every clicked beat advanced on its
  first click".
* Controls now resolve one frame earlier than they painted, so a click's visible result appears on the
  same frame rather than the next. Strictly more responsive; nothing semantic differs.
* The cost is one extra layout evaluation per frame for a migrated screen (`Update` derives the same
  rectangles `Draw` will). These are integer-arithmetic helpers over `UiMetrics`; the profile's glyph
  atlas, which is the expensive part, is untouched.

## Alternatives considered

**Make the edge survive the catch-up tick** (latch a pending click and clear it after `Draw`). One line,
and wrong: every consumer that reads the edge in `Update` would then see it true on both Updates of the
tick and fire twice. Fixing that needs per-consumer consumption discipline, which is a larger change than
moving the hit tests — and double-firing a purchase is worse than dropping a click.

**Set `IsFixedTimeStep = false`.** It removes the catch-up tick and therefore this exact symptom, while
leaving every `Draw`-side mutation in place and making the whole simulation's step variable. The defect is
input ownership, not the timestep.

**A two-pass IMGUI** — run each screen's existing combined paint-and-decide method twice, once with input
enabled and painting suppressed. It guarantees zero geometry drift by construction, because it is
literally the same code, but it needs a suppression flag threaded through every `_ui.*` call and it
doubles the paint work. Named geometry helpers get the same guarantee for the controls that matter.

**A retained UI framework, MVVM, or an event bus.** Far beyond the defect, and it would replace an
architecture that is otherwise working.

## Engine compatibility

MonoGame 3.8.4.1 (the pinned version, `docs/engine-reference/monogame/VERSION.md`). The behaviour this
decision is built on is documented in the shipped framework XML for `Game.Tick`: it *"Makes at least one
call to Update … and exactly one call to Draw … When IsFixedTimeStep is set to false this will make
exactly one call to Update"*, and `Game.IsRunningSlowly` is described as set *"when IsFixedTimeStep is set
to true and a tick of the game loop takes longer than TargetElapsedTime for a few frames in a row"*.
Neither `IsFixedTimeStep` nor `TargetElapsedTime` is assigned anywhere in this repository, so the default
fixed 60 Hz timestep — and its catch-up tick — is what ships. Nothing here depends on a 3.8.4.1-specific
API; the decision would hold on any MonoGame that keeps the XNA game-loop contract.

## Migration and what remains

The pass of 2026-09-12 migrated the host's own modals (`DrawSettings`, `DrawHelp`, `DrawWelcomePanel`),
the host's own `Draw()` screen dispatch (which drained five screens' request queues and, for TRAINING,
called `_hunter.Train(stat)` and `Save()` from the paint pass), and every screen that still decided in
`Draw`: Map, Warren, Hunt, Traits, Training, Vault and Forge. `MasteryScreen`, `GearScreen` and
`RosterScreen` needed no work beyond dropping an edge parameter that was already unused, and
`LoadoutScreen`'s `Draw` took a `clicked` it never read.

The current exception list is whatever `py tools/check_draw_purity.py --audit` prints as `OK` — that
command, not this document, is the authority, so the list cannot go stale. **At the time of writing it is
empty: zero findings, zero excused.**

**A migration miss, found 2026-09-15.** The title screen's branch of `Game1.Update` returns through its
own `Latch(); return;` before the modal block that calls `UpdateSettings()`, so a panel opened from the
title painted and hovered but took no click at all -- the exact symptom this decision exists to remove,
on the one path the pass did not walk. The title block now calls `UpdateSettings()` itself, and
`tests/unit/IdleXIdle.Game.Tests/title_settings_input_test.cs` pins it. The rule for the next migration: a
handler moved out of `Draw` must be reached from **every** `Update` branch that can paint the control,
and the branches with an early return are the ones to check first.

Screens whose press cannot be unit-tested, and what covers them instead: `WarrenScreen` and
`ForgeScreen` measure text inside `Update`, and `TraitCollectionScreen`'s constructor null-checks its
`UiKit`, so none can be driven by `new …(null!)` the way Mastery, Roster, Map, Training and Vault are in
`catch_up_tick_test.cs`. A null-`UiKit` escape added for the test's benefit would have the fixture posing
a control measured from nothing — a test of a path the game never takes. Those three are covered by the
gate, by the per-screen migration review, and by the live fresh-save opening run at 100 / 125 / 150,
which drives the Vault's forced chest open and the Gear equip through the real input path.

A known **pre-existing** layout defect was photographed during the pass and deliberately not fixed here,
because this decision is about input and not layout: at 150 % the GUIDANCE switch's row and the
"GUIDANCE OFF STOPS EVERY PROMPT" caption are both laid out at y 709 (`f.ToggleY + 5 × f.TogglePitch`
against `f.AccessCaptionY`), so the label prints through the caption. It is present identically before and
after the migration — the settings panel is pixel-identical at 150 % across the change — and belongs to
whoever next opens `SettingsFrameNow`.
