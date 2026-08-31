# Settings Menu UI: IDLExIDLE

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | ux-designer |
| **Status** | Complete — authored autonomously, no user available this session (per this session's stated constraint). Ambiguities resolved using `design/gdd/accessibility-settings-system.md` (the spine of this document — every setting name, type, range, and default is taken from it verbatim), `design/art/art-bible.md` (§3.4, §4.5, §4.6, §7.1–§7.7), `design/gdd/input-targeting-system.md` (§3.4, §3.9), and `design/gdd/save-load-persistence.md` as authority; every resolution is flagged inline as an assumption below. |
| **Priority / Tier** | MVP — UI layer (`design/gdd/systems-index.md` #25) |
| **Depends On** | `accessibility-settings-system` (every setting, its data model, defaults, ranges, persistence contract), `input-targeting-system` (rebindable action IDs, cycle-and-confirm mechanics, device coexistence model) |
| **Depended On By** | None — leaf node. This is a screen, not a service; nothing in the currently locked design consumes anything this document defines. |

---

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| B1 | `keybind_map` is interpreted as `dict<action_id, dict<device_class, binding>>`, where `device_class ∈ {keyboard_mouse, gamepad}` — i.e. every action carries **two independent, simultaneously-live bindings**, one per device class, not one binding total. | `accessibility-settings-system`'s example JSON is illustrative and abbreviated (`"..." : "..."`) and only shows a gamepad-flavored value per action; it does not explicitly state the per-device shape. `input-targeting-system` §3.9 requires that mouse and gamepad/keyboard input coexist simultaneously with no "current device" mode — that is only representable if each action has an independent binding slot per device class. This document adopts the interpretation required to make the spine doc's own coexistence model renderable as a rebinding UI, and flags it for confirmation in a future centralized pass. |
| B2 | A new field, `first_run_prompt_shown` (bool, default `false`), is proposed as an **additive** addition to `accessibility_settings.json`'s `settings` object. | The first-run accessibility prompt (§3.3) needs a persisted flag to guarantee it is shown exactly once. Per this session's file discipline, `accessibility-settings-system.md` is not edited to add this field directly — it is specified here, flagged as a gap for that document's next revision, exactly as that document itself flagged unresolved gaps in `save-load-persistence.md` and `systems-index.md` without self-editing them. Additive-only, so it does not require a `settings_schema_version` bump under that document's own versioning policy (§3.1 of that document). |
| B3 | Keyboard defaults are proposed for the three MVP actions: `cycle_next` = `E`, `cycle_prev` = `Q`, `confirm` = `Space`. Gamepad defaults reuse `accessibility-settings-system`'s own stated example (`GamepadRightBumper`/`GamepadLeftBumper`/`GamepadA`). | The spine doc states "engine defaults" without enumerating keyboard values explicitly. This screen needs a concrete default to render on first launch and to test rebinding/conflict-detection against. Proposed here as this document's own default, not a spine-doc-locked value — flagged for confirmation once `ui-programmer` implements input capture. |
| B4 | The menu-open/pause input (`Escape` on keyboard, `Start`/`Menu` on gamepad) and the rebind-capture-cancel input (`Escape` on keyboard, `B`/East face button on gamepad) are treated as **fixed, non-remappable system-level bindings**, deliberately excluded from `keybind_map` and from this screen's rebinding surface entirely. No GDD currently defines a formal `menu_open` or `pause` action ID; this document reserves the concept structurally rather than inventing new `keybind_map` schema surface for it. | This is the direct, explicit resolution to the "rebind creates an unreachable state" problem this task calls out by name (unbinding the menu-open key). Making the menu-open and cancel inputs categorically un-offerable in the rebinding UI is a stronger guarantee than validating against it at bind-time — it makes the failure mode structurally impossible rather than merely caught. This mirrors near-universal genre convention (Escape/Start are essentially never rebindable in shipped games) rather than inventing a novel pattern. |
| B5 | The mouse's primary (left) button is reserved and cannot be reassigned to a different action within this screen's rebinding surface. | `input-targeting-system` §3.3 defines the mouse path as atomic — a left-click both selects and confirms a target in one motion, with no separate "confirm" step. Allowing that button to be reassigned to something else would silently break the game's primary input path for the majority of players. This document adds the reservation explicitly since the spine doc does not state it. |
| B6 | None of the three MVP actions (`cycle_next`, `cycle_prev`, `confirm`) may be set to "Unbound" on a device class that currently has a binding — every action always carries exactly one binding per device class it supports; rebinding replaces a binding, it never clears one to nothing. | Mouse's atomic click path does not depend on `cycle_next`/`cycle_prev`/`confirm` at all (§3.3 of `input-targeting-system`), so a keyboard-only or gamepad-only player is the population genuinely at risk: if any of these three actions could be cleared to "none" on their only connected device's binding, that player would lose the sole path to combat targeting entirely — a core-loop-breaking lockout, not a minor inconvenience. Removing the ability to fully unbind these three specific actions is judged the correct, narrow trade-off; it does not restrict *reassignment* (any valid key/button remains fully available), only *deletion without replacement*. |

---

## 1. Overview

`settings-menu-ui` is the UI-tier screen (`systems-index.md` #25) that renders and edits every
value `accessibility-settings-system` defines. It owns the screen's layout, settings taxonomy,
navigation model, first-run surfacing, live-preview/reversion behavior, and input-rebinding
interface — it does **not** own the data model, value ranges, defaults, or persistence mechanism
for a single one of the settings it displays; those belong entirely to `accessibility-settings-system`
and are used here by exact field name, never redefined. This document exists to close a specific,
named gap: `accessibility-settings-system` authored a complete settings surface with no screen to
host it, and this is that screen. Its central and most interesting design problem is that the
screen a player uses to make IDLExIDLE accessible to themselves must already be accessible,
on its own, before a single one of its own settings has been touched — solved explicitly in §3.1,
not glossed over.

---

## 2. Player Fantasy

This screen's job is to disappear. Its success is measured in a very different unit than every
other document in this project: not "does the player feel something," but **does the player find
what they need in under 30 seconds and never think about this screen again.** A player who came
here to turn the music down should be back in the game in three clicks, having formed no opinion
about this screen at all — that indifference is the win condition.

But that framing is only half true, and stating only half of it would be dishonest. For the subset
of players `accessibility-settings-system`'s Player Fantasy (§2 of that document) describes — a
player with tremor, a colorblind player, a player who needs 200% text to read anything, a player
who is Deaf and relies entirely on a visual telegraph — this is not a screen they visit and forget.
It is the screen that decides, before a single fight has happened, whether the rest of the game's
promise ("success is a reading skill, never a reflex test") is available to them at all. If this
screen is itself hard to read, hard to navigate, or hides the one setting a given player needs three
menus deep, the failure is not cosmetic — it is the exact failure `accessibility-settings-system`
exists to prevent, relocated one layer up, into the screen meant to prevent it.

Those two framings are not in tension; they are sequential. For most players, most of the time, this
screen is furniture — quick, forgettable, correctly organized so the one setting they want is where
they'd guess it is. For the player who needs it most, on the one visit that matters, it must never
once ask them to already have the thing it's about to give them. Every decision in §3 below is made
in service of both framings holding at once, for the same screen, without a "basic mode" and an
"accessible mode" fork — there is only ever one settings menu, and it has to work as both.

---

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document defines the **screen**: its structure, navigation, and behavior. It is explicitly
silent on:

- The value, type, range, default, or persistence mechanism of any individual setting — all of
  that is `accessibility-settings-system`'s exclusive authority, referenced here by exact field
  name only.
- The visual chrome asset design (exact icon art, exact panel corner radius, exact border pixel
  weight) — that is `art-director`/`ui-programmer` territory once this document's structural and
  behavioral requirements are locked. This document specifies *what* must be visually distinguishable
  and *how it behaves*, not the literal pixels.
- Any setting or mechanic not already defined by `accessibility-settings-system` or
  `input-targeting-system`. This document does not invent new player-facing settings (e.g., no
  display-resolution or fullscreen/windowed toggle is specified here, because no GDD currently
  defines one — see §5, Edge Case #10, and §6 for the resulting flagged gap).

### 3.1 The Bootstrap Problem — Resolution

**The problem, stated precisely**: the settings menu is the screen a player uses to make the game
accessible to them. But the settings menu itself must already be accessible before any of those
settings are applied. A player who needs 200% UI scale cannot read a settings menu rendered at
100% scale in order to find the UI Scale setting. If this document simply said "the settings menu
follows the same accessibility rules as everything else," it would have begged the question rather
than answered it — those rules are exactly what's in question at the moment a player who needs them
opens this screen for the first time, at its default, unconfigured state.

**Resolution — four independent mechanisms, not one, because no single one is sufficient alone:**

1. **A guaranteed-legible default baseline, independent of any setting's current value.**
   `accessibility-settings-system` §3.8 already binds this exact screen to a text-legibility floor:
   Body/Data workhorse-tier text renders at or above **18px logical size at 1080p/100% scale** —
   its default, unconfigured state — and chrome icons/glyph-grammar containers never render below
   `HUD_GLYPH_MIN_PX_AT_1080P` (16px) at any resolution (Formula 1's clamp, referenced in §4 below).
   Critically: this is a claim about the screen's **default** state, before the player has touched
   a single control. A player has never opened a genuinely illegible settings menu, even at
   `ui_scale_percent = 100`, its lowest available value. This is a "guaranteed functional" claim,
   not a "guaranteed ideal" claim — 18px at 100% scale is not what a player who ultimately wants
   200% would call comfortable, but it is legible enough, for the overwhelming majority of players
   in that position, to locate and operate the UI Scale control that gets them the rest of the way.
2. **The first-run prompt (§3.3) puts the highest-impact setting in front of every player before
   the "illegible main menu" scenario can occur at all.** This is the strongest lever, because it
   removes the burden of finding the settings menu, learning its tab structure, and locating the
   right control — for the one setting (`ui_scale_percent`) where that burden matters most. A
   player who needs a larger UI is offered the control to get it before they have had to read
   anything harder than this prompt's own guaranteed-legible baseline.
3. **Live preview (§3.4) turns the UI Scale control into something operable by feel, not just by
   sight.** The control communicates its own effect through direct, immediate visual feedback: every
   press of "increase" grows every piece of text and every icon on the settings screen itself in the
   same frame, and the control's on-screen position never changes between presses (fixed spatial
   layout — see §3.2). A player does not need to already be able to read a percentage value to
   operate this control correctly; they can press "increase" repeatedly, watching everything grow,
   and stop when comfortable — the same way a person adjusting a physical volume knob by ear doesn't
   need to see the number printed on it.
4. **`ui_scale_percent` is clamped to `[100, 200]` on every load** (`accessibility-settings-system`
   §5, Edge Case #7). There is no sub-100%, "trapped small" state this control can ever be found in
   — the floor of its range is always the guaranteed-legible baseline from mechanism 1, never lower.

**The honestly-acknowledged limit**: these four mechanisms together guarantee this screen is
*reachable and operable* for every player up to and including the maximum scale this game's own
architecture offers. They do not guarantee every player's vision needs are met by that maximum —
a player who needs more magnification than 200% of an 18px baseline provides is outside what this
system can serve on its own, and the correct fallback for that player is OS-level accessibility
tooling (Windows Magnifier, high-DPI display scaling) operating on the whole application window from
outside this game's systems entirely. Building a rescue mechanism for that case is genuinely out of
this document's authority and this project's current scope; stating the limit plainly here is more
useful than silently assuming it away.

This resolution has an "evil twin" — a player who scales *up* far enough to lose the ability to
reach the control that would undo it. That failure mode is a distinct problem with its own
dedicated resolution, covered fully in §5, Edge Case #1 (not restated here to avoid duplicating the
same content under two headings) — the short version is that §3.2's persistent, non-scaling header
strip is the mechanism that makes it structurally impossible, reinforced by §3.4's revert-on-timeout
safety net as an independent second layer.

### 3.2 Screen Structure and Settings Taxonomy

**The call, made explicitly**: `accessibility-settings-system` defines a large settings surface —
visual, motor, motion, auditory, and cognitive groups (§3.2–§3.6 of that document). Two organizing
strategies were weighed:

- **Fully distributed into contextual categories** (Display / Audio / Input) — a player looking for
  volume finds it under Audio, exactly where every other game trains them to look; no screen is
  disproportionately large or small.
- **A single dedicated "Accessibility" tab holding everything** — maximally discoverable and
  signals commitment, but this specific game's settings surface is *unusually* accessibility-heavy
  (nearly every non-volume setting `accessibility-settings-system` defines is, by that document's
  own framing, an accessibility setting). A single tab holding almost the entire surface while
  Display/Audio/Input sit nearly empty would be a lopsided, confusing taxonomy, not a clean one.

**Resolution: a hybrid — contextual placement is the source of truth, with a curated, prominent
Accessibility tab as an additional, non-fragmenting entry point.** Four top-level tabs, in this
fixed order:

| Order | Tab | Rationale for position |
|---|---|---|
| 1 | **Accessibility** | Placed first, deliberately — the strongest available discoverability signal, at zero cost to a player who doesn't need it (skipping past the first tab to whichever tab they want costs exactly one input, the same as any other position would). This directly reflects §2's stated commitment: this is not an incidental screen. |
| 2 | **Display** | Conventional genre position; also the *native* home for the visual settings duplicated into the Accessibility tab (below). |
| 3 | **Audio** | Conventional genre position. |
| 4 | **Input** | Holds the full rebinding sub-screen (§3.5), which is large enough to deserve its own tab rather than being squeezed into Accessibility. |

**The Accessibility tab is a curated hub, not a container of unique controls it alone owns.** Every
control inside it is the *same live-bound widget* as its native-home counterpart elsewhere — two
widgets reading and writing one underlying field in the runtime settings object, not two independent
copies of state. This avoids the fragmentation/duplication-bug risk a naive "copy the control" design
would carry, while still making the highest-impact settings reachable in exactly one click from the
first tab a player sees. Grouped by need, mirroring `accessibility-settings-system`'s own §3.2–§3.6
taxonomy for internal consistency:

| Group | Controls shown (native home in parentheses) |
|---|---|
| **Vision** | `ui_scale_percent` (Display), `high_contrast_mode` (Display), `colorblind_preview_mode` (Display) |
| **Motor** | `magnetism_strength` (Input), `cycle_initial_delay_ms` / `cycle_repeat_interval_ms` (Input), a jump-button labeled "Remap Controls…" that navigates directly into the Input tab's rebinding sub-screen (§3.5) — not duplicated inline, because a rebinding table is a whole interactive interface, not a compact control |
| **Motion** | `reduced_motion_enabled` (Display), `telegraph_audio_precue_enabled` (Audio) |
| **Hearing** | `mono_audio_enabled` (Audio), a jump-button labeled "Volume Mixer…" that navigates directly into the Audio tab (not duplicated — five sliders is too much to responsibly inline in a curated hub) |
| **Cognitive** | `simplified_ui_density` — **not duplicated anywhere else**; this is its only home, since no other tab's contextual grouping fits it |
| — | A "Reset All Accessibility Settings to Default" button, scoped to every control listed above (a subset of the full "Reset All" in the persistent header — see §3.6) |

**Display, Audio, and Input tabs** hold every remaining setting in their natural contextual home,
including the ones also duplicated into Accessibility above:

- **Display**: `ui_scale_percent`, `high_contrast_mode`, `colorblind_preview_mode`,
  `reduced_motion_enabled`.
- **Audio**: `volume_master`, `volume_music`, `volume_sfx`, `volume_dialogue`, `volume_ui`,
  `audio_loudness_normalization_enabled`, `mono_audio_enabled`, `telegraph_audio_precue_enabled`.
- **Input**: the full `keybind_map` rebinding table (§3.5), `magnetism_strength`,
  `cycle_initial_delay_ms`, `cycle_repeat_interval_ms`, `controller_vibration_enabled` (grouped
  here, matching `accessibility-settings-system`'s own placement of it as "the input-device-adjacent
  toggle," §3.3 of that document — not duplicated into Accessibility, since it is a comfort setting
  rather than a functional-access one).

**Persistent, non-scaling header/footer strip** — present on every tab, and the mechanism that
resolves §3.1's "evil twin": a fixed-size chrome band, top and bottom of the screen, that is the
**one deliberate, documented exception to Formula 1's scaling** (§4) — it never grows or shrinks
with `ui_scale_percent`, because its entire purpose is to remain the one place on this screen that
is *always* exactly where the player last saw it, at a size already inside the guaranteed-legible
floor from §3.1. It is not exempt from `high_contrast_mode` or `colorblind_preview_mode`, which
apply to the whole composited frame regardless (`accessibility-settings-system` §5, Edge Case #10)
— only from the *size* multiplier, because size is the one axis that can make it unreachable, and
color/contrast axes cannot. It always contains: the current tab name, a "Reset This Tab" button, a
global "Reset All Settings" button, and — only while a `ui_scale_percent` change is pending
confirmation — the live-preview confirmation prompt (§3.4). All four are reachable via a single,
fixed, always-available keyboard/gamepad shortcut regardless of scroll position or focus depth
elsewhere on the screen (§3.7).

**No settings not defined by `accessibility-settings-system` are added.** Display-resolution,
fullscreen/windowed mode, and any other conventional "graphics options" a player might expect are
explicitly out of this document's scope — no GDD currently defines them (§5, Edge Case #10 covers
the resulting user-facing gap honestly rather than inventing values this document has no authority
to set).

### 3.3 First-Run Accessibility Prompt

**Recommendation: yes, surface key accessibility options before the player is dropped into
gameplay.** Reasoning: most players never open a settings menu voluntarily during their first
session — by the time a player who needs a given accessibility setting *would* think to look for it,
they may have already bounced off an unreadable HUD or an un-completable input sequence and quit.
Surfacing the highest-impact controls once, up front, converts "a menu most players never visit"
into "a menu most players are guaranteed to at least see once" — directly serving §2's stated tension
that this screen decides, for some players, whether the rest of the game is available to them at all.

**Flow**:

1. **Trigger**: on the first application launch where `accessibility_settings.json` is absent, or
   present with `first_run_prompt_shown = false` (Assumption B2) — checked before the main menu
   becomes interactive.
2. **Content**: a single, non-scrolling screen (itself subject to the same guaranteed-legible
   baseline as §3.1 — this is not a special "unlocked" screen exempt from the rules the rest of the
   menu follows) presenting exactly five items, in this order:
   - `ui_scale_percent` (stepped slider — the highest-impact single control, per §3.1).
   - `colorblind_preview_mode` (segmented cycle control — see §3.2's Vision group and §3.8's
     labeling requirement).
   - `reduced_motion_enabled` (toggle).
   - `telegraph_audio_precue_enabled` (toggle — shown even though it defaults **on**, so a player
     who wants to turn it off knows it exists and where).
   - A single button: **"Full Accessibility Settings…"**, which jumps directly into the
     Accessibility tab (§3.2) if the player wants more before continuing, rather than making them
     hunt for it later.
3. **Live preview applies identically to this screen** (§3.4) — a change to `ui_scale_percent` here
   grows this screen's own text in the same frame, for the same reason it does everywhere else.
4. **Two exits, both terminal for this prompt**: **"Continue"** (commits whatever was chosen) and a
   corner **"Skip"** (commits the current defaults unchanged). Both write `first_run_prompt_shown =
   true` immediately, using `accessibility-settings-system`'s own unconditional-write-on-close
   trigger (§3.1 of that document) — this prompt is itself a settings-adjacent surface for the
   purposes of that trigger. **Skip is not "ask me later"** — dismissing without changes means
   "I choose the defaults," and every one of these five controls remains fully reachable from the
   Accessibility tab at any later point. The prompt is never re-forced, and never blocks progress
   with a multi-step wizard — a single skippable screen only.
5. **Voluntary replay**: a "Replay Welcome Screen" entry lives inside the Accessibility tab (§3.2),
   for a player who wants to revisit this exact curated prompt later — this is the same
   tutorial-replay convention `accessibility-settings-system` §3.6 flags as a coordination point for
   `onboarding-tutorial-system`, applied here to this screen specifically since that system does not
   yet exist to own it.
6. **Fully keyboard/gamepad-operable**, identically to the rest of the menu (§3.7) — a screen whose
   entire purpose is accessibility failing its own keyboard/gamepad gate would be a direct
   contradiction this document will not allow, even for a single one-time screen.

### 3.4 Live Preview and Reversion

**All settings with a directly observable effect apply live, the instant they change — no separate
"Apply" button, no navigate-away-then-discover round trip.** This directly serves §2's "never think
about it again" framing: a commit-then-check-then-adjust-again loop is exactly the kind of friction
this screen exists to remove.

| Setting(s) | Live-preview behavior |
|---|---|
| `ui_scale_percent`, `high_contrast_mode`, `colorblind_preview_mode` | Applied globally, including to this screen's own rendering, within the same frame the control changes. No separate preview panel is needed — the settings screen is its own live-preview canvas. |
| `reduced_motion_enabled` | Applied globally in real time; its visible effect within the menu itself is on tab/panel transition motion (§3.8), which immediately switches to instant cuts when enabled. |
| `volume_master`/`music`/`sfx`/`dialogue`/`ui`, `mono_audio_enabled`, `audio_loudness_normalization_enabled` | A short live sample plays on adjustment (standard "hear it while you drag it" convention), and the change applies to the actual game mix immediately, whether or not gameplay is currently audible behind the menu. |
| `controller_vibration_enabled` | A single test-rumble pulse fires immediately on toggle, so a gamepad player feels the change without needing to enter combat to verify it. |
| `magnetism_strength`, `cycle_initial_delay_ms`, `cycle_repeat_interval_ms` | Applied immediately to the live input system the moment they change; not meaningfully previewable *visually* on this screen (there is no creature to target here), so no preview widget is invented for them — see §5 for the explicit statement that this is a deliberate omission, not an oversight. |
| `simplified_ui_density` | Applied immediately in the underlying model; its visible effect is only observable the next time the player opens one of the three screens it governs (Loot Filters, Automation Config, Creature Roster), none of which are visible from this screen — stated as an accepted, unavoidable limit, not a bug (§5). |

**Reversion is scoped asymmetrically, and that asymmetry is deliberate, not an oversight:**

- **`ui_scale_percent` alone gets a revert-on-timeout confirmation.** It is the one setting in this
  entire surface capable of making *other controls unreachable* (the "evil twin" of §3.1) — no other
  setting carries that specific risk category. On every change, the persistent header/footer strip
  (§3.2) shows: *"Keep this UI Scale? [Keep]  [Revert]  — reverting automatically in `Ns`"* (default
  15s; range and rationale in §7). This banner lives in the header/footer's fixed, non-scaling
  region specifically so the confirmation prompt itself can never become part of the problem it
  exists to guard against. Every additional adjustment to `ui_scale_percent` while the banner is
  showing **extends** the timeout from the moment of that latest change, rather than the timer
  continuing to count down from the first change — so a player actively exploring the slider is
  never cut off mid-adjustment. "Revert" restores the **last-persisted** value (which may itself
  already differ from the factory default, if the player had previously customized and confirmed a
  different scale) — not a hard reset to 100%; that distinction belongs to the separate, explicit
  Reset control (§3.6).
- **No other setting gets a timeout.** `high_contrast_mode` and `colorblind_preview_mode` change
  color/contrast, never layout or reachability — a player can always still see and operate a Reset
  button even under an unwanted contrast or CVD-simulation state, because this screen's controls are
  never color-only-dependent (art-bible's Master Rule, enforced here by §3.8). Applying a forced
  confirm-or-revert cycle to `colorblind_preview_mode` specifically would actively work against its
  stated purpose as a quick self-verification tool a player is meant to toggle freely for comparison
  (§3.8, Assumption A3 of the spine doc) — friction there is a cost with no matching safety benefit,
  so it is deliberately not added.

### 3.5 Input Rebinding Interface

Lives entirely inside the Input tab, as its own sub-screen (too large to inline per §3.2).

**Layout**: a three-column list — Action Name (Display-face label, e.g. "Cycle Next Target") |
Current Binding, shown as two adjacent chips, one per device class (Keyboard/Mouse and Gamepad,
per Assumption B1) | a "Rebind" button per chip. Gamepad chips render the actual pre-learned button
glyph (a bumper icon, a face-button icon) using flat, chrome-style iconography per art-bible §7.2's
"chrome iconography" rule — these are generic, pre-learned-by-every-player icons, not glyph-grammar
content, so they never borrow stained-glass rendering. The row list is **data-driven off
`keybind_map`'s own keys** — when `combat-encounter-system` eventually registers its forward-reserved
actions (`ability_1`, `ability_2`, `ability_3`, `ultimate`, `dodge_block`, per the spine doc's §3.3),
new rows appear automatically; this screen requires no redesign to accommodate them.

**Capture flow**, per action + device-class chip:

1. Player activates a "Rebind" button (click, or Enter/A while focused). The chip enters a
   **listening state**: label changes to "Press a key…" (or "Press a button…"). Per art-bible §7.3's
   utility-screen motion discipline ("instant or near-instant state changes… no lingering
   transitions, no flourish"), this is a **static, clearly-labeled state change, never a looping
   pulse or animation** — a continuous animated "waiting" indicator would violate the same motion
   budget the rest of this chrome-only screen is held to (§3.8).
2. The very next valid input event on that device class is captured, **except** the reserved
   cancel/menu-open inputs (Assumption B4) — `Escape` on keyboard, `B`/East face button or
   `Start`/Menu on gamepad — which always cancel the capture instead of being bindable to anything,
   even if the player's intent was to bind that exact input. This is stated explicitly because it is
   the one input in this entire interface that behaves differently from what it looks like it should
   do, and "no hand-waving" requires that be spelled out rather than assumed obvious.
3. **Conflict check**, before commit: the newly captured input is checked against every other
   binding already registered for that same device class (never cross-device — a keyboard key and a
   gamepad button occupying "the same" logical slot on two different actions is not a conflict, since
   both can be legitimately bound to the same action simultaneously, Assumption B1).
   - **No conflict**: commits immediately. The debounced write to `accessibility_settings.json`
     (§3.1 of the spine doc, ~500ms) begins on this change like any other setting.
   - **Conflict found**: does **not** commit. An inline warning names the conflicting action exactly
     (e.g., *"This key is already bound to Cycle Next Target"*) and offers exactly two choices,
     matching `accessibility-settings-system` §5 Edge Case #5's own language precisely rather than
     inventing a third "auto-swap" behavior that document does not license: **"Choose a Different
     Key"** (discards this capture, re-enters listening state) or **"Go Reassign Cycle Next
     Target"** (discards this capture, and moves keyboard/gamepad focus directly to that other
     action's row so the player can free up the key manually, in a second explicit capture). Never
     a silent overwrite, never an automatic swap.
4. **Unbinding is not offered as an option** for `cycle_next`, `cycle_prev`, or `confirm`
   (Assumption B6) — the Rebind button always *replaces* a binding, and there is no separate "Clear"
   control for these three actions. This is the second half of this document's structural resolution
   to the "unreachable input state" requirement (the first half, §3.5 step 2 above, covers the
   menu-open key specifically; this covers the combat-targeting actions specifically).
5. **Device disconnect during capture**: if the device physically disconnects while a chip is in the
   listening state, the capture auto-cancels after a short timeout (§7) with a visible message, and
   the chip reverts to showing its previous, still-valid binding — there is no way to receive further
   input from a device that is no longer connected, so waiting indefinitely is not an option.
6. **Rows remain visible and editable regardless of current device connection state** — a player may
   configure a gamepad binding with no gamepad currently connected (e.g., setting up ahead of time).
   Only the live "test" affordances that require the physical device (e.g., a vibration test pulse)
   are unavailable until it is connected; this is non-blocking and does not affect the binding itself.

### 3.6 Reset-to-Default

Three granularities, all present, because a self-rescue affordance is essential and no single scope
is correct for every situation:

1. **Per-control reset** — a small icon button beside every individual setting, restoring that one
   field to its `accessibility-settings-system` §7-documented default. Lowest-friction, most
   surgical.
2. **Per-tab reset** ("Reset This Tab") — lives in the persistent header/footer strip (§3.2),
   restores every setting native to the currently open tab. Includes the Accessibility tab's own
   scoped "Reset All Accessibility Settings" variant (§3.2), which resets every setting in its
   curated list regardless of which tab natively owns it.
3. **Global reset** ("Reset All Settings") — also in the persistent header/footer strip, restores
   every setting this document renders to its documented default in one action. This is the
   emergency affordance §3.1 and §5's "evil twin" scenario depend on being reachable at all times,
   which is precisely why it lives in the one region of the screen exempt from `ui_scale_percent`'s
   scaling (§3.2).

All three are undoable-in-spirit but not literally reversible with an "undo" stack — resetting is
itself just setting values to known-good defaults, which the player can then re-customize freely;
no confirmation dialog gates the per-control or per-tab reset (low stakes, easily re-done), but the
global reset shows a single lightweight confirmation ("Reset all settings to default?") since it is
the one action capable of discarding a fully customized rebinding table in one press.

### 3.7 Navigation Model

**Keyboard**: `Tab`/`Shift+Tab` or `Up`/`Down` arrows move focus between rows in the current tab's
content pane. `Left`/`Right` arrows adjust the focused control's value directly (stepped slider,
segmented cycle control) or, on a Rebind button/toggle, `Enter` activates it. A dedicated pair of
keys (`Q` / `E`, matching the proposed `cycle_prev`/`cycle_next` defaults for mnemonic consistency,
Assumption B3) switch between the four top-level tabs **from anywhere in the content pane**, without
first needing to navigate focus up to a tab rail — tab-switching is never overloaded onto the same
`Left`/`Right` inputs used for adjusting a focused control's value, removing any ambiguity about
which behavior a given press triggers. `Escape` closes the settings screen, **except** while a
rebind capture is listening (§3.5), where it cancels only the capture — closing the whole screen
from that state requires a second, separate `Escape` press once the capture itself has been
resolved or cancelled.

**Gamepad**: D-pad or left-stick `Up`/`Down` moves focus between rows; D-pad or left-stick
`Left`/`Right` adjusts the focused control. `LB`/`RB` (bumpers) switch tabs, mirroring the keyboard's
`Q`/`E` exactly (same input class as combat's own `cycle_prev`/`cycle_next` for the same
mnemonic-consistency reason). `A` activates/confirms. `B` closes the screen, or cancels an in-progress
rebind capture only (same nested-escape rule as keyboard's `Escape`).

**Mouse**: click a tab in the left-hand rail to switch tabs; click-and-drag or click a stepper
arrow to adjust a slider; click a toggle to flip it; click a segmented cycle control's arrows or
click-and-cycle through its states; click "Rebind" to enter capture.

**Every interactive element carries a visible keyboard-focus indicator at all times** (art-bible
§7.7, restated as binding here per §3.8) — this is never optional, never toggled off, and never
achieved through a color change alone.

### 3.8 Art-Bible Compliance (binding, not advisory)

- **Ceremony tier: None (chrome)**, matching Combat HUD, Roster, Automation Config, and Loot
  Filters (art-bible §7.6). Justification, made explicitly rather than by table lookup alone: this
  screen carries **zero glyph-grammar content at all** — even the gamepad rebinding icons are
  chrome iconography (art-bible §7.2's "generic, pre-learned" test), not Source medallions or role
  badges — making it, if anything, a *more* pure chrome screen than the Roster (which at least
  displays glyph-grammar content per row). Per §3.4 of the art bible, "ornamentation is allowed to
  flex with ceremony, never with density" — this is a high-density utility screen the player may
  revisit often, and its importance is carried entirely by correct information architecture, never
  by frame decoration. **The first-run prompt (§3.3) is explicitly not a ceremony-tier moment
  either** — it is informational, not a celebratory payoff beat, and applying ceremony-tier motion
  or ornament to it (art-bible §7.6's reserved "High" tier language) would actively work against its
  job of being instantly, calmly legible on a player's very first look at the game.
- **Palette: the restricted three-color chrome subset only** (art-bible §4.5) — Void Ink
  (borders/backgrounds), Bone Parchment (fills/text), Cold Slate (disabled/inactive states). No
  reserved content color (Hearth Gold, Ember Threat, Corruption Bloom, any Source color) appears
  anywhere on this screen as decorative fill. The one art-bible-sanctioned exception (a chrome
  element briefly borrowing a reserved color when it is itself the trigger for that exact meaning)
  does not apply anywhere in this document — nothing on this screen creates a mastery-tier
  commitment the way the Forge's Vow-binding confirmation does.
- **Chrome interaction-state matrix**: every control on this screen must be distinguishable across
  the five states art-bible §7.7 names — hover, keyboard-focus, selected, disabled, drag-preview
  (not applicable here — no drag-to-reorder pattern exists in this screen's design; see §5 for the
  explicit statement that this is intentional) — built from value-steps and border weight within
  the three-color budget, never a fourth hue. Keyboard-focus specifically must be visually distinct
  from mouse-hover (two different, real states, both real-world common on this screen given mixed
  input use is expected, §3.9 of `input-targeting-system`).
- **Typography — the 7.1 hybrid, applied**: the screen title and the four tab names use the pixel
  **Display face** (integer-snapped per Formula 2, §4) — they qualify as "screen titles" under
  art-bible §7.1's own enumerated Display-face use cases. Every setting's row label, description
  text, and current value uses the scalable **Data face** (continuous per Formula 1, §4) with
  **tabular (fixed-width) numerals** wherever a numeric value appears (every ms/%/px value on this
  screen) — this is exactly art-bible §7.1's design test ("if a number will be compared against
  other numbers in a list, it renders in the UI/Data face with tabular figures") applied directly;
  every value on this screen is inherently list-context. No italics anywhere (art-bible §7.1).
- **Motion**: governed entirely by art-bible §7.3's utility-screen rule — instant or near-instant
  state changes only, fast linear or slight ease-out, no bounce, no lingering transitions. The
  rebind-capture "listening" state (§3.5) is a static label change, never a loop, for the same
  reason. Tab switches use a fast slide/fade per §7.3's "standard menu transitions" language, cut to
  an instant swap entirely when `reduced_motion_enabled` is on.

---

## 4. Formulas

This document's own mathematical contribution is intentionally minimal, and that is stated
explicitly rather than padded to look more substantial than it is — per §3.0, this document does
not re-derive math it does not own. Every setting's value math (UI scale application, display-face
integer snapping, aim-assist display mapping, WCAG contrast) belongs entirely to
`accessibility-settings-system` and is referenced here by exact formula name, not redefined:

- **Formula 1 (that document) — UI Scale, Continuous** governs every piece of this screen's own
  Data-face text and chrome-icon rendering directly; this screen is itself one of the "every UI-space
  screen" consumers that formula's `FinalRenderedSize_px` clamp exists to protect (§3.1 above).
- **Formula 2 (that document) — UI Scale, Integer-Snapped** governs this screen's title and tab
  names (§3.8).
- **Formula 3 (that document) — Aim-Assist Display Mapping** governs exactly how the
  `magnetism_strength` slider's 0–100% display value is computed from the persisted `[0.0, 1.0]`
  float this screen writes.
- **Formula 4 (that document) — WCAG Contrast Ratio** governs the exact hover/keyboard-focus/
  selected/disabled value-step and border-weight pairings this screen's chrome interaction-state
  matrix (§3.8) must independently clear (3:1 minimum for active, non-exempt states, per that
  document's stated genuine gap — this screen inherits that open item rather than resolving it,
  since the concrete hex/weight values remain `ui-programmer`'s implementation task).

### Formula 1 (this document) — UI Scale Revert-Timeout Expiry

```
revert_deadline_s = last_change_timestamp_s + UI_SCALE_REVERT_TIMEOUT_S
is_expired         = current_time_s ≥ revert_deadline_s
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `last_change_timestamp_s` | float | ≥ 0 | Wall-clock time of the most recent `ui_scale_percent` adjustment while the confirmation banner (§3.4) is showing. Re-set on every additional adjustment, not fixed at the first one — this is what makes an actively-exploring player never get cut off mid-adjustment. |
| `UI_SCALE_REVERT_TIMEOUT_S` | float (constant) | 5–30, default 15 (§7) | Tuning knob. |
| `current_time_s` | float | ≥ 0 | Wall-clock time, sampled each frame the banner is visible. |
| `is_expired` | bool | — | When `true`, the pending `ui_scale_percent` change is discarded and the last-persisted value is restored (§3.4) — equivalent to the player pressing "Revert" themselves, minus the input. |

**Worked example**: `UI_SCALE_REVERT_TIMEOUT_S = 15`. Player sets `ui_scale_percent` from 100 to
150 at `t = 0s` (`last_change_timestamp_s = 0`). At `t = 8s` they nudge it again to 160
(`last_change_timestamp_s` updates to `8`). With no further input, `revert_deadline_s = 8 + 15 =
23`. At `t = 23s`, `is_expired` becomes `true` and the value reverts to whatever was last persisted
before this session of edits began (100, in this example, assuming no prior confirmed change) — not
to 150, the intermediate unconfirmed value the player briefly held before nudging further.

No other original formula exists in this document; every remaining setting listed in §3.2 is a
direct boolean/enum/linear pass-through with no derived math of its own, exactly mirroring the
spine doc's own "Non-Formulaic Settings" statement (§4 of that document) — this screen simply reads
and writes those values, it does not transform them further.

---

## 5. Edge Cases

1. **`ui_scale_percent` is set high enough that this screen's own controls would overflow and
   become unreachable — the bootstrap problem's evil twin.** Resolved by three independent,
   non-cooperating mechanisms, stated together as defense-in-depth so a single implementation
   failure in any one does not strand a player: **(a)** the persistent, non-scaling header/footer
   strip (§3.2) always renders its Reset controls at a fixed size and fixed screen position,
   regardless of `ui_scale_percent`'s current value — it is structurally exempt from the formula
   that could ever push it off-legibility. **(b)** Per `accessibility-settings-system` §3.7 item 5
   and §5 Edge Case #1, content must reflow (become scrollable) rather than overlap or clip at any
   scale up to 200% — this screen's dense settings lists follow that binding requirement exactly.
   **(c)** The revert-on-timeout mechanism (§3.4, Formula 1 above) auto-reverts an unconfirmed
   `ui_scale_percent` change within `UI_SCALE_REVERT_TIMEOUT_S` regardless of whether the player
   successfully interacts with anything at all — it does not depend on the player being able to
   *reach* a button, only on time passing.
2. **A rebind attempt targets the menu-open or capture-cancel input** (`Escape`, gamepad `Start`,
   or gamepad `B`). Per Assumption B4, these inputs are never offered as capturable values in the
   first place — pressing one while a chip is listening always cancels the capture instead of
   binding to it, with no error state, no crash, and no way to reach a configuration where the
   menu-open input has been reassigned away from opening the menu.
3. **A rebind attempt would leave `cycle_next`, `cycle_prev`, or `confirm` with zero bindings on a
   device class.** Per Assumption B6, this is not offered as an option at all — the Rebind control
   always replaces an existing binding with a newly captured one; there is no "Clear" affordance for
   these three actions specifically. A keyboard-only or gamepad-only player can never talk themselves
   into a state with no way to target or confirm an attack.
4. **A rebind attempt targets the mouse's primary (left) button.** Rejected per Assumption B5 — the
   left button is reserved for `input-targeting-system`'s atomic direct-click path (§3.3 of that
   document) and is never offered as a capturable value for any other action, in either device-class
   column.
5. **Two of `cycle_next`/`cycle_prev`/`confirm` are captured with the identical key/button within the
   same device class.** Resolved per §3.5's conflict-check step, matching
   `accessibility-settings-system` §5 Edge Case #5 exactly: the new binding does not commit; the
   player is offered "Choose a Different Key" or "Go Reassign [conflicting action]" — never a silent
   overwrite.
6. **The persisted `accessibility_settings.json` is corrupted or missing on launch.** Per
   `accessibility-settings-system` §3.1/§5, the runtime state loads the full default set with no
   crash. This screen's specific responsibility on top of that: every tab renders its full default
   values immediately with no blocking "error" dialog and no visibly broken/partial state — the
   player can begin editing settings right away, and the very first change produces a fresh, valid
   file via the same debounced write mechanism as any ordinary edit (§3.1 of the spine doc).
7. **A setting's persisted value falls outside its current valid range** (e.g., a future patch
   narrows `cycle_repeat_interval_ms`'s minimum). Per `accessibility-settings-system` §5 Edge Case
   #7, the out-of-range value is silently clamped to the nearest bound on load — this screen always
   displays the **clamped** value, never the stale out-of-range one, and never shows a warning
   banner about it; the spine doc's own philosophy treats this as ordinary, graceful drift, not an
   event worth interrupting the player over.
8. **`colorblind_preview_mode` and `high_contrast_mode` are both active simultaneously while the
   player adjusts `ui_scale_percent`.** Per `accessibility-settings-system` §5 Edge Case #10, all
   three are independent and orthogonal, applied in the same composited-frame pass with no ordering
   conflict — this screen's own chrome is part of that same composited frame, so it inherits the
   same guarantee automatically; no special-case handling is needed on this document's side.
9. **A player expects `colorblind_preview_mode` to be a corrective filter, not a simulation.** This
   is a real, foreseeable confusion this document resolves through labeling, not through a runtime
   check: the control's on-screen label reads **"Colorblind Preview (Simulation)"**, never
   "Colorblind Mode" alone, with an inline description stating its actual purpose plainly — "Shows
   how the screen looks under different types of color vision, so you can confirm every important
   signal is still readable. Nothing in this game depends on color alone, so there's nothing here to
   correct." A player with genuine CVD is not underserved by this labeling choice — per
   `accessibility-settings-system` Assumption A3, that population is already fully served by the
   underlying colorblind-safe-by-construction art (art-bible §4.6) with no setting required at all;
   this control exists for verification and curiosity, not as anyone's primary accessibility lever,
   and mislabeling it as corrective would create false expectations this screen has no way to meet.
10. **A player looks for display-resolution, fullscreen/windowed, or VSync options and does not find
    them.** Not an oversight this document is silently hiding — no GDD currently defines those
    settings' values, ranges, or defaults, and per §3.0 this document does not invent settings it has
    no authority to specify. Flagged here plainly, and again in §6, as a real gap for a future
    `display-settings` scope (owned by whichever system eventually defines it) to fill; this screen's
    tab structure (§3.2) has room reserved in the Display tab for it without requiring a redesign.
11. **`volume_dialogue` is adjusted and produces no audible change.** Expected, not broken — per
    `accessibility-settings-system` §3.5, this slider is a reserved no-op fader with no dialogue
    content to affect yet (`game-concept.md`'s narrative scope is explicitly N/A for MVP). This
    screen labels the control with the same "(no dialogue content yet)" caption the spine doc's own
    reasoning implies, specifically so a player who moves the slider and hears nothing does not
    conclude the control is malfunctioning.
12. **`simplified_ui_density` is toggled while none of the three screens it affects (Loot Filters,
    Automation Config, Creature Roster) are open.** The underlying value changes immediately and
    persists normally; its effect is simply not observable from this screen, since none of those
    three screens render here. Stated as an accepted limit (§3.4), not a defect — not every setting
    on this surface can be meaningfully live-previewed on the settings screen itself.
13. **A gamepad disconnects while a rebind chip is in the listening state** (§3.5, step 5). The
    capture auto-cancels after the device-disconnect timeout (§7) with a visible message; the chip
    reverts to its last valid binding. No partial or corrupted binding state is ever written.
14. **A drag-to-reorder pattern is expected on this screen and does not exist.** None of this
    screen's settings involve reordering (unlike Loot Filters or Creature Roster, which
    `accessibility-settings-system` §3.7 item 2 specifically binds to a non-drag keyboard equivalent
    requirement) — this is stated explicitly so the absence of a drag-preview interaction state in
    §3.8's chrome matrix reads as a deliberate scope observation, not an unaddressed requirement.

---

## 6. Dependencies

### Depends On

- **`accessibility-settings-system`** (`design/gdd/accessibility-settings-system.md`) — the spine of
  this document. Every setting rendered here is used by exact field name, type, range, and default
  from that document's §3.2–§3.6 and §7: `ui_scale_percent`, `high_contrast_mode`,
  `colorblind_preview_mode`, `keybind_map`, `magnetism_strength`, `cycle_initial_delay_ms`,
  `cycle_repeat_interval_ms`, `controller_vibration_enabled`, `reduced_motion_enabled`,
  `telegraph_audio_precue_enabled`, `volume_master`/`music`/`sfx`/`dialogue`/`ui`,
  `audio_loudness_normalization_enabled`, `mono_audio_enabled`, `simplified_ui_density`. Also reuses
  that document's persistence contract (§3.1 of that document — write triggers, debounce timing,
  load/clamp behavior on out-of-range values) without redefining any of it. **Flagged gap, not
  self-edited**: this document proposes one additive field, `first_run_prompt_shown` (Assumption
  B2), that `accessibility-settings-system.md`'s own schema example does not currently list. Per this
  session's file discipline, that document is not edited here; the addition and its justification
  are recorded fully in this document for a future centralized reconciliation pass, following the
  exact precedent that document itself set when flagging its own gaps in `save-load-persistence.md`
  and `systems-index.md` without self-editing them.
- **`input-targeting-system`** (`design/gdd/input-targeting-system.md`) — reads, by exact name, the
  `cycle_next` / `cycle_prev` / `confirm` action identifiers (§3.4 of that document) as the rows this
  screen's rebinding table renders, and depends on §3.9's device-coexistence model (mouse and
  gamepad/keyboard both always live, no "current device" mode) as the reason `keybind_map` must carry
  independent per-device-class bindings (Assumption B1). Also depends on §3.3's statement that the
  mouse path is atomic (click = select + confirm) as the justification for reserving the mouse's left
  button from reassignment (Assumption B5).
- **`save-load-persistence`** (`design/gdd/save-load-persistence.md`) — indirectly, via
  `accessibility-settings-system`'s reuse of that document's atomic temp-file-then-replace write
  pattern (§3.1 of the spine doc). This screen never touches `save.json` or that system's mechanisms
  directly; it is a downstream beneficiary of a pattern `accessibility-settings-system` already
  adopted, not a direct consumer of `save-load-persistence` itself.

### Depended On By

None. This is a leaf node in the dependency graph — a screen, not a service. Nothing in the
currently locked design reads data from, subscribes to, or requires this document's own definitions
to function; a future `onboarding-tutorial-system` may add a coordination note pointing *at* this
document's Accessibility tab (§3.3's "Replay Welcome Screen" entry) once it exists, exactly mirroring
the forward-looking coordination note `accessibility-settings-system` §3.6 already left for that same
future system — but that would be a note added to `onboarding-tutorial-system`'s own future GDD, not
a dependency edge pointing back into this one.

### `systems-index.md` note (flagged, not self-edited)

Per this session's file discipline, `design/gdd/systems-index.md` is not edited by this document.
That file's own footnote (added when `settings-menu-ui` was enumerated as system #25) already
correctly lists this document as depending on `accessibility-settings-system` — this document
additionally surfaces a dependency on `input-targeting-system` (for the rebindable action set and
device-coexistence model) that is not yet reflected in that file's Dependency Map, flagged here for
a future centralized update rather than edited directly.

---

## 7. Tuning Knobs

| Knob | Field | Type | Safe Range | Default | Notes |
|---|---|---|---|---|---|
| UI Scale revert-timeout | `UI_SCALE_REVERT_TIMEOUT_S` | float, seconds | 5–30 | 15 | §3.4, §4 Formula 1. Below ~5s risks reverting before a player has even read the confirmation prompt; above ~30s leaves an unconfirmed, potentially unreadable state active uncomfortably long if the player has stepped away. |
| UI Scale keyboard/gamepad step increment | (implementation constant, this document's own) | int, % | 5–25 | 10 | §3.1 mechanism 3 — the fixed step size a single "increase"/"decrease" press moves `ui_scale_percent` by, within its `accessibility-settings-system`-owned `[100, 200]` range. Too small makes reaching a comfortable value feel tedious via keyboard/gamepad; too large overshoots past comfortable values too easily. Mouse-drag remains continuous within the same range regardless of this step value. |
| Aim-assist / cycle-timing display step increments | (implementation constant, this document's own) | — | designer's choice, matching the underlying setting's own granularity | `magnetism_strength`: 5% steps; `cycle_initial_delay_ms`/`cycle_repeat_interval_ms`: 25ms steps | Presentation-only; the underlying persisted value is unaffected by display step size (mirrors Formula 3 of the spine doc's own presentation-vs-persisted-value distinction). |
| Rebind-capture device-disconnect timeout | (implementation constant, this document's own) | float, seconds | 3–10 | 5 | §3.5 step 5, §5 Edge Case #13 — how long a rebind chip waits in the listening state after its target device physically disconnects before auto-cancelling the capture. |
| First-run prompt trigger | `first_run_prompt_shown` | bool | `{false, true}` | `false` | §3.3, Assumption B2. Not itself player-tunable — set to `true` automatically on the first prompt dismissal (Continue or Skip) and never reset by normal play; a player could only see it again via the deliberate "Replay Welcome Screen" entry (§3.3), which does not re-flip this flag to `false`. |

Every value in `accessibility-settings-system` §7's own Tuning Knobs table (UI scale range, aim-assist
range, cycle-timing ranges, volume ranges, etc.) governs this screen's controls directly and is not
re-listed or re-tuned here — this table contains only the tuning knobs this document itself
introduces.

---

## 8. Acceptance Criteria

1. **No self-lockout at any offered UI scale value.** At `ui_scale_percent = 100`, `150`, and `200`,
   every control across all four tabs remains reachable via keyboard-only and gamepad-only input,
   with no overlapping or silently-truncated text or controls (reflow/scroll only, per
   `accessibility-settings-system` §3.7 item 5) — verified by a scripted or manual walkthrough at
   each scale value, on every tab, including the Accessibility hub's duplicated widgets.
2. **The persistent header/footer strip never scales with `ui_scale_percent`.** Its rendered size at
   `ui_scale_percent = 200` is pixel-identical to its rendered size at `ui_scale_percent = 100` (same
   resolution) — verified by a direct rendered-size comparison at both values.
3. **Every setting is reachable and operable keyboard-only**, with no functionality gated behind
   mouse-hover-only interaction anywhere on this screen, including rebind capture and the segmented
   cycle controls — verified by a full keyboard-only walkthrough touching every control in every tab.
4. **Every setting is reachable and operable gamepad-only**, substituting a single connected gamepad
   with no keyboard or mouse connected — identical verification method to Criterion 3.
5. **A rebind cannot produce an unrecoverable input state**, verified by four specific negative
   tests, each expected to fail/reject cleanly with no crash: (a) attempting to bind any action to
   `Escape`/gamepad `Start`/gamepad `B` is never offered as a capturable option; (b) attempting to
   leave `cycle_next`, `cycle_prev`, or `confirm` with zero bindings on any device class is never
   offered; (c) attempting to bind any action to the mouse's left button is rejected; (d) attempting
   to bind two of `cycle_next`/`cycle_prev`/`confirm` to the identical key within one device class is
   rejected with a visible conflict warning naming the conflicting action, with no state change
   persisted until resolved.
6. **A corrupted or missing `accessibility_settings.json` on launch results in every tab rendering
   the full default value set** from `accessibility-settings-system` §7, with no crash, no blocking
   error dialog, and the screen immediately editable — the very next settings change produces a
   valid, repaired file.
7. **An out-of-range persisted value displays as its clamped value**, never the stale out-of-range
   value, on any control whose underlying setting's valid range has been narrowed since the value was
   saved — verified with a fixture file containing a deliberately out-of-range value.
8. **The first-run prompt appears exactly once** under a fresh (`accessibility_settings.json`-absent)
   launch, and never again automatically after either "Continue" or "Skip" is pressed — verified by
   simulating both dismissal paths and confirming `first_run_prompt_shown = true` is written
   immediately in both cases, with a second simulated launch not re-triggering the prompt.
9. **The first-run prompt is reachable again voluntarily** via the "Replay Welcome Screen" entry in
   the Accessibility tab, without that action re-flipping `first_run_prompt_shown` back to `false`.
10. **Live preview is observable within the same frame** for `ui_scale_percent`, `high_contrast_mode`,
    and `colorblind_preview_mode` — changing any of the three visibly alters this screen's own
    rendering immediately, with no navigation away and back required to see the effect.
11. **An unconfirmed `ui_scale_percent` change reverts automatically** to the last-persisted value
    (not necessarily the factory default) within `UI_SCALE_REVERT_TIMEOUT_S ± 1s` of the last
    adjustment, verified with a simulated clock; a further adjustment made before expiry correctly
    extends the deadline rather than the original timer continuing to run.
12. **No other setting exhibits the revert-on-timeout behavior** — changing `high_contrast_mode`,
    `colorblind_preview_mode`, any volume slider, or any toggle persists immediately with no pending
    "unconfirmed" state and no countdown banner.
13. **Duplicated widgets stay in sync with their native-home counterpart with no manual refresh.**
    Changing `ui_scale_percent` from the Accessibility tab's Vision group is immediately reflected in
    the Display tab's copy of the same control (and vice versa) without switching tabs to force a
    reload — verified by changing the value on one tab and reading it back on the other in the same
    session, confirming a single underlying value is bound to both widgets rather than two
    independent copies.
14. **`colorblind_preview_mode`'s label and description never use "correct" or "corrective"
    language**, and explicitly state its purpose as a simulation/self-verification tool — verified by
    a direct read-through of the shipped copy text (a QA content check, not a runtime assertion).
15. **This screen's ceremony tier is None/chrome**: no frame ornament beyond the three-color chrome
    budget (Void Ink / Bone Parchment / Cold Slate) appears anywhere on any tab, including the
    first-run prompt — verified by visual/asset review against art-bible §4.5 and §7.6.
16. **A visible keyboard-focus indicator is present on 100% of interactive elements** across every
    tab, distinct in value-step/border-weight from the hover, selected, and disabled states, and
    never differentiated by color alone — verified by a full keyboard-navigation sweep with focus
    state logged at each stop.
17. **Tabular (fixed-width) numerals render for every numeric value on this screen** (all ms/%/px
    values), and no Display-face (pixel bitmap) font is used for any value inside a settings row —
    only for the screen title and the four tab names — verified by a typography/asset audit against
    art-bible §7.1.
18. **A device disconnect during rebind capture auto-cancels within the configured timeout** and
    restores the chip's last valid binding, with no partial or corrupted binding persisted — verified
    by simulating a mid-capture disconnect event.
