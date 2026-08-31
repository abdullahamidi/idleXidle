---
name: resonance-hunter-sell-salvage-confirm-research
description: 2026-08-18 research on sell/salvage confirmation dialogs (Forge screen) surfaced a real behavior pivot needed vs current code and vs forge-ui.md's own spec — flag before implementing
metadata:
  type: project
---

Researched (2026-08-18, no file writes, research-only task) shipped-game destructive-action
confirmation patterns for the FORGE screen's SELL/SALVAGE actions, per the user's ask for: a
warning on every sell/salvage, a "don't show this again" checkbox, and an always-asked extra
question when the item is currently equipped.

**Two reconciliation gaps found while grounding the research, not yet resolved anywhere**:

1. **Equipped-item handling is currently a hard block, not a confirm-and-allow.**
   `src/ResonanceHunter.Game/ForgeScreen.cs` `Sell()` returns early with "TAKE IT OFF BEFORE
   SELLING IT." for a worn item, and the mouse SELL button renders `enabled: !worn`. The user's
   requested "always-asked question when equipped" implies *allowing* the sale/salvage of worn
   gear behind a mandatory confirm — a real behavior change from today's code, not just new dialog
   copy on an existing allowed path.
2. **No confirmation dialog exists in code at all yet** for single-item Sell/Salvage (both fire
   immediately). `design/gdd/forge-ui.md` §3.8 already speccs a confirmation-gate pattern and its
   Assumption A7 only escalates extra ceremony for Legendary-rarity disposals — it does not warn on
   *every* sell/salvage, and it has no "don't show again" suppression mechanism at all. Implementing
   the user's ask means either revising `forge-ui.md` or layering new behavior beside it; either way
   the two documents will disagree until reconciled.

Also relevant: a separate bulk hotkey `SalvageJunk` (key J) already ships as a **deliberately
zero-confirmation** bulk clear of Common/Uncommon junk (excludes worn items, doc comment: "the
idle twin of a single DISMANTLE... busywork one at a time... clears the two junk tiers in one
press"). Worth checking whether "warn on every sell/salvage" is meant to reach that bulk path too
— its own doc comment states the opposite design intent, and re-adding friction there would
contradict a design decision already shipped.

**How to apply**: before writing or editing `design/gdd/forge-ui.md`, `ForgeScreen.cs`, or any new
confirmation-dialog code for Sell/Salvage, confirm with the user whether (a) worn-item sell/salvage
should move from hard-block to allow-with-confirm, and (b) whether the new "every sell/salvage"
warning is scoped to the single-item FORGE actions only or also reaches `SalvageJunk`'s bulk path.
Don't assume either silently — both are genuine scope decisions, not implementation details.
