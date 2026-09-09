using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Game;

/// <summary>The four verbs the game has for an item. Raised here; the host routes them to the FORGE.</summary>
public enum ItemAction { Equip, Upgrade, Reforge, Salvage }

/// <summary>
/// GEAR: what the hunter wears, what is in the bag, and whether a piece is worth putting on.
/// </summary>
/// <remarks>
/// <para>
/// UX V2 P1.7 (brief §60–§65, D3, D5, D9). Three panels. EQUIPPED is the one ornate surface: the hunter's
/// header (portrait · name · class · GEAR POWER · innate), the paper doll with its eight slots, a footer that
/// says how much is worn and which set rungs are on, and the two utility actions. INVENTORY is a quiet plate:
/// a filter as a pill row, a four-column grid (three at 150 %) whose empty cells are flat so the items emerge
/// (§63), a footer that counts. ITEM DETAIL is the inspector in the shared grammar — CATEGORY · NAME · the
/// DECISION first (the verdict, what it replaces) · WHAT IT DOES · the SET as a ladder with reached and
/// unreached rungs in three encodings (§64) · one primary EQUIP. The LOADOUT column that spent a sixth of the
/// width on duplicates is gone (§61). EQUIP BEST is EQUIP HIGHEST POWER, because that is what it compares (§65).
/// </para>
/// <para>
/// Every value is the model's: <see cref="Hunter.PowerContribution"/>, <see cref="DamageBench"/> for the
/// weapon's verdict, <see cref="ElementSets"/> for the ladder, <see cref="EnchantNeed"/> for whether an
/// enchant works with the build, <see cref="ItemClasses.WhyNot"/> for who can wear it. There is no lock
/// system, so there is no LOCK button.
/// </para>
/// <para>
/// THE DENSITY PROFILE (UI polish §8–§9, LAW 7). The page is 1920×1080 at every UI SCALE; what grows is the
/// type, the rows, the boxes and the pads, all read from <see cref="UiMetrics"/> and <see cref="UiTypography"/>.
/// Every vertical rhythm here is derived from the room that is actually there: the header stack from the
/// rungs it stacks, the slot pitch from the span between header and footer, the inventory's rows from the
/// span between its tabs and its footer, the inspector's floor from the verbs and the button under it. Where
/// the content no longer fits at 150 % it reflows — slot labels step beside their boxes, the grid drops to
/// three columns and scrolls, the tab row stacks, the inspector scrolls by whole items — and never by
/// shrinking a font (§17).
/// </para>
/// <para>
/// FEEDBACK, NOT INFORMATION (UI polish §22–§38, §48–§52). Nothing on this screen was added to say a new
/// thing; what was added says that something CHANGED. An equip pulses the slot it landed in once, ticks
/// GEAR POWER from the old number to the new over one Transition and lets the number flash, and reveals a
/// set rung the moment it is reached with one Source-coloured pulse. The first time a set reaches five
/// pieces the screen records it and hands the host one notice to toast. Every one of those is a
/// <see cref="GearFeedback"/> event fired from <see cref="Update"/> by DIFFING the hunter — never by
/// Draw, and never by the click that caused it, so the menu route the host equips through and the
/// EQUIP HIGHEST POWER sweep pulse exactly like the EQUIP button does. Every pulse is a
/// <see cref="UiMotion"/> one-shot, so Reduced Motion collapses it to the same end state.
/// </para>
/// </remarks>
public sealed class GearScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color CellBg = new(0x15, 0x10, 0x0F);
    private static readonly Color CellHot = new(0x2E, 0x24, 0x20);
    private static readonly Color Green = UiInk.Good;
    private static readonly Color Shadow = new(0x08, 0x07, 0x0B);

    /// <summary>
    /// The Source accent (§45–§46): the set ladder's capstone emblem and its reveal wear it. The same six
    /// values VaultScreen and HuntScreen hold; a shared token belongs in UiKit, which this pass may not edit.
    /// </summary>
    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x9A, 0x7A, 0xD8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };

    /// <summary>The capstone emblem of each set — icon_set_momentum … icon_set_harmony, keyed once, not per frame.</summary>
    private static readonly Dictionary<Source, string> CapstoneIconKey =
        Enum.GetValues<Source>().ToDictionary(s => s, s => "icon_set_" + ElementSets.CapstoneName(s).ToLowerInvariant());

    private readonly UiKit _ui;
    private readonly ForgeScreen _forge;   // owns the item bag + the shared item-icon renderer

    // ── THE FEEDBACK STATE — see GearFeedback at the foot of this file. ──
    private readonly GearFeedback _feedback = new();
    /// <summary>
    /// When this screen last ran an Update, so a re-entry after time away re-primes the diff instead of
    /// pulsing every slot the roster switch or the Forge changed while nobody was looking (§30: animate
    /// CHANGE — a change the player did not make on this screen is not one to celebrate here).
    /// </summary>
    private long _lastUpdateMs = -1;
    private string? _observedCharacterId;
    private const int StaleMs = 700;

    /// <summary>The sound cue for the last thing the player did here, cleared by reading — the host owns audio.</summary>
    /// <remarks>Same shape as TraitsScreen.ConsumeCue. Cues raised: sfx_click (a tab or an item picked), sfx_equip (a piece put on or taken off by this screen's own controls), sfx_error (a refusal: CANNOT WEAR, or a button that is off).</remarks>
    public string? ConsumeCue() => _feedback.ConsumeCue();

    /// <summary>A set's first five-piece completion, as two lines for a toast ("NATURE SET COMPLETE" / "OVERGROWTH ACTIVE"), cleared by reading. The host toasts it and plays sfx_levelup.</summary>
    public string? ConsumeNotice() => _feedback.ConsumeNotice();

    /// <summary>The set that just completed for the first time (a <see cref="Source"/> name, the form SaveGame.CompletedSets holds), cleared by reading.</summary>
    public string? ConsumeCompletedSet() => _feedback.ConsumeCompletedSet();

    /// <summary>Every set completed so far — what the host writes to SaveGame.CompletedSets.</summary>
    public IReadOnlyCollection<string> CompletedSets => _feedback.CompletedSets;

    /// <summary>Seed the completed sets from a save, so a set completed in an earlier session never announces itself again.</summary>
    public void RestoreCompletedSets(IEnumerable<string> sets) => _feedback.RestoreCompletedSets(sets);

    // ── DEV: the capture pose. The rig shoots one frame, and a pulse is over in a fifth of a second, so
    // the state "just equipped" can only be photographed by FIRING THE EQUIP A KNOWN NUMBER OF FRAMES
    // BEFORE THE SHUTTER — the same reasoning as HuntScreen.DevSeekBefore. Read by this screen alone:
    //
    //   RH_SHOT_GEAR_POSE=equip[@N]   the character fixture takes four NATURE pieces off silently on its
    //                                 first frame, then puts the chest back on N frames (5 when unsaid)
    //                                 before the shot: one slot pulses, GEAR POWER is mid-tick, the
    //                                 ladder's fifth rung reveals, and the set completes for the first
    //                                 time (the notice is the host's to toast). @0 is the pulse's peak.
    //   RH_SHOT_GEAR_POSE=best        runs EQUIP HIGHEST POWER on the first frame, so nothing in the bag
    //                                 beats what is worn and the button draws OFF and says why.
    //   RH_SHOT_GEAR_POSE=bare        takes everything off, so UNEQUIP ALL draws OFF and the doll is eight
    //                                 quiet EMPTY slots.
    //   RH_SHOT_GEAR_POSE=press       holds the mouse button down for the whole run, so a PRESSED cell,
    //                                 tab, slot, menu row or verb can be photographed under RH_SHOT_PAGE_MOUSE.
    private static readonly string? DevPoseSpec = Environment.GetEnvironmentVariable("RH_SHOT_GEAR_POSE");

    /// <summary>
    /// DEV: RH_SHOT_GEAR_DETAIL=&lt;rows&gt; parks the item detail column that many rows down.
    /// </summary>
    /// <remarks>
    /// The column scrolls, and at UI SCALE 150 % the rows a modifier row can collide on — the built-in
    /// with its caption, both sides of a prefix's trade — start below the fold. A state no capture mode
    /// can pose has never been looked at, which is exactly how the caption came to draw through its own
    /// figure there; this dial is the fixture that makes the fixed row photographable. Rig-only: unset,
    /// the column opens at the top as it always has.
    /// </remarks>
    private static readonly int DevDetailScroll =
        int.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_GEAR_DETAIL"), out var rows) ? Math.Max(0, rows) : 0;

    /// <summary>
    /// DEV: RH_SHOT_GEAR_REDUCED=1 forces Reduced Motion for a capture. The accessibility setting lives in
    /// the host's SETTINGS and a rig run never reads a save, so the ONE law this pass must be judged on —
    /// "the same end state, instantly" — had no way to be photographed at all. Applied from Update, before
    /// this frame draws; inert without the variable, and rig-only.
    /// </summary>
    private static readonly bool DevReduced = Environment.GetEnvironmentVariable("RH_SHOT_GEAR_REDUCED") is { Length: > 0 };
    private bool _devPosePrepared, _devPoseFired;
    private ItemInstance? _devPoseItem;

    public PlayerLoadout Loadout { get; set; } = null!;
    public MasteryTree Mastery { get; set; } = null!;

    /// <summary>The keystones the world has taught this account - set by the host.</summary>
    /// <remarks>
    /// The weapon bench COMPOSES the build it measures, so it needs the same two catalogues the fight
    /// does. Keystone and Vow knowledge left the trait tree for the world, and without them every
    /// comparison number on this screen belongs to a different hunter: a player who conquered for
    /// GLASS CANNON would be choosing between weapons on a build wearing no keystone at all.
    /// </remarks>
    public IReadOnlyList<Keystone> DiscoveredKeystones { get; set; } = Array.Empty<Keystone>();

    /// <summary>The Vows the account has found - set by the host.</summary>
    public IReadOnlyList<Vow> KnownVows { get; set; } = Array.Empty<Vow>();

    /// <summary>What the equipped skills have earned, so the weapon bench measures the build the fight runs.</summary>
    public SkillProgress? SkillLevels { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    /// <summary>F7 layout-debug: panel rects, cells, selection.</summary>
    public bool DevGearDebug { get; set; }

    public GearScreen(UiKit ui, ForgeScreen forge) { _ui = ui; _forge = forge; }

    // ── LAYOUT: three columns to the page — EQUIPPED · INVENTORY · DETAIL in the brief's 42 / 27 / 31 shares (§61). ──
    // THE PAGE ANCHORS. Top, Margin and BottomInset are where the columns sit on the 1920×1080 page and do not
    // follow the profile (§8, §11: the top-level layout keeps fitting; what grows is inside it). Everything
    // that is a row, a button, an icon, a pad or a gap below is read from UiMetrics — never a per-screen factor.
    private static int Top => UiKit.PageTop;   // the first row under the chrome band, at this profile (was 120)
    private const int Margin = 24, BottomInset = 40;
    private static int Gutter => UiMetrics.Space(16);
    // THE EQUIPPED COLUMN IS 42 %, NOT 776 PX, AND THE REASON IS THE FRAME ART: UiKit.Panel picks its texture
    // by aspect, and at 776/920 = 0.84 this panel wore the SQUARE frame, whose side ornament reaches 57 px in —
    // past the 40 px content margin, so the GEAR POWER label was drawn under it. At 0.80 and under it wears
    // the vertical frame, whose side rail is 24.
    //
    // BOTH EDGES OF EVERY COLUMN FOLLOW THE PAGE. A fixed left edge against a page-relative right one
    // collapsed the inspector to two hundred pixels at UI SCALE 125% — the columns are shares of the
    // width that is actually there.
    private static int ColumnsWidth => UiKit.PageRight(Margin) - Margin - Gutter * 2;
    // THE INSPECTOR TAKES THE LARGER of its 31 % share and the house InspectorWidth — §9: "wider inspectors" at
    // the accessibility profiles, so 33 px prose keeps a readable line. At 100 % the share wins (570) and
    // nothing moves; at 150 % it is 744, and the doll and the bag split what is left in the same 42 : 27.
    private static int DetailWidth => Math.Max(ColumnsWidth * 31 / 100, UiMetrics.InspectorWidth(UiKit.Page.Width));
    private static int EquippedWidth => (ColumnsWidth - DetailWidth) * 42 / 69;
    private static int PanelHeight => UiKit.PageBottom(BottomInset) - Top;
    private static Rectangle EquippedPanel => new(Margin, Top, EquippedWidth, PanelHeight);
    private static Rectangle InventoryPanel => new(EquippedPanel.Right + Gutter, Top, ColumnsWidth - DetailWidth - EquippedWidth, PanelHeight);
    private static Rectangle DetailPanel => new(InventoryPanel.Right + Gutter, Top, UiKit.PageRight(Margin) - (InventoryPanel.Right + Gutter), PanelHeight);

    /// <summary>The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates.</summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.PaperDoll => new[] { EquippedPanel },
        TourTarget.Inventory => new[] { InventoryPanel },
        TourTarget.ItemDetail => new[] { DetailPanel },
        _ => Array.Empty<Rectangle>(),
    };

    // ── THE HEADER: portrait · name · class · GEAR POWER, then two full-width lines. Every y is derived from
    // the rung above it, so at 150 % the stack grows instead of overprinting (the 2026-09-01 capture had
    // WANDERER printed through WEARS… and the innate through the first slot ring). ──
    /// <summary>
    /// Where the header row starts: one title line and a breath under the panel's top edge — 216 at 100 %.
    /// Measured from the EDGE, not from <see cref="UiKit.BodyTopBare"/>: this panel wears the square frame,
    /// whose crest drop the title takes but the header never did, and the doll needs the 28 px more than
    /// the header needs the air.
    /// </summary>
    private static int HeaderTop => EquippedPanel.Y + UiTypography.PanelBodyTopBare + UiMetrics.Space(34);
    private static int PortraitSize => UiMetrics.Control(72);
    /// <summary>The two full-width lines (what this hunter may wear, the innate) start under the portrait.</summary>
    private static int WearsTop => HeaderTop + PortraitSize + UiMetrics.Space(10);
    // The doll and its eight slots: armour down the left, weapon and jewellery down the right.
    // THE COLUMN FITS THE PANEL IT IS IN. Written as a fixed 102-box on a 130 pitch from y 356, the four rows
    // needed 508 px and asked for 520 — which the 1080 page has and a 125 % page did not. Every figure below
    // is derived from the room between the header and the footer.
    // The two header sentences WRAP rather than being cut (release polish 2026-09-05, gear-01): at 150 %
    // "WEARS WANDERER GEAR AND ANY CHARM, RING O…" was cut mid-word to keep the slot column fixed. The
    // column moves down by the lines the sentences actually took, mirrored here from the last draw.
    private static int s_wearsLines = 1, s_innateLines = 1;
    private static int SlotsTop => WearsTop + s_wearsLines * UiTypography.Pitch(UiTypography.Secondary) + s_innateLines * UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(6);

    private static int SlotInset => UiMetrics.Space(6);
    private static int SlotSpan => FooterStrip.Y - UiMetrics.Space(16) - SlotsTop;
    private static int SlotPitch => Math.Max(1, SlotSpan / 4);
    /// <summary>What a label UNDER its box costs the row: one Secondary line and a breath.</summary>
    private static int SlotLabelRoom => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    /// <summary>The lane a label BESIDE its box takes — the widest label (WEAPON, GLOVES) at Secondary.</summary>
    private static int SlotLabelLane => UiMetrics.Control(64);
    /// <summary>A box that still holds an icon and its ring comfortably.</summary>
    private static int SlotBoxComfortable => UiMetrics.IconSize + UiMetrics.Space(24);
    /// <summary>
    /// LABELS UNDER THEIR BOXES while four rows of box-and-label stack in the column with a comfortable box
    /// (100 %); BESIDE them — toward the doll — once they would not (125 % and 150 %). The box keeps its size
    /// and the label keeps its rung: the profile exists to make text bigger, never smaller (§17).
    /// </summary>
    private static bool SlotLabelsUnder => SlotPitch - SlotLabelRoom >= SlotBoxComfortable;
    private static int SlotBox => Math.Clamp(SlotPitch - (SlotLabelsUnder ? SlotLabelRoom : UiMetrics.Space(8)),
                                             UiMetrics.IconSmall, UiMetrics.Control(102));
    private static readonly (GearSlot Slot, string Label, int Column, int Row)[] SlotLayout =
    {
        (GearSlot.Helm, "HELM", 0, 0), (GearSlot.Chest, "CHEST", 0, 1), (GearSlot.Gloves, "GLOVES", 0, 2), (GearSlot.Boots, "BOOTS", 0, 3),
        (GearSlot.Weapon, "WEAPON", 1, 0), (GearSlot.Focus, "FOCUS", 1, 1), (GearSlot.Charm, "CHARM", 1, 2), (GearSlot.Ring, "RING", 1, 3),
    };
    /// <summary>The slot columns' inset from the panel's EDGE — the house margin and a breath (46 at 100 %), the frame's crest drop not counted, as the doll has always been placed.</summary>
    private static int SlotColumnInset => UiTypography.PanelPadX + SlotInset;
    private static Rectangle SlotRect(int column, int row)
        => new(column == 0 ? EquippedPanel.X + SlotColumnInset : EquippedPanel.Right - SlotColumnInset - SlotBox,
               SlotsTop + row * SlotPitch, SlotBox, SlotBox);
    /// <summary>The doll's frame at 100 % — sprite geometry (the idle strip is fitted by height), not a control.</summary>
    private const int DollWidth = 360;
    private static int DollTop => SlotsTop - UiMetrics.Space(10);
    /// <summary>The width between the two slot columns (and their labels, when those sit beside the boxes).</summary>
    private static int DollRoom => EquippedPanel.Width - 2 * (SlotColumnInset + SlotBox
                                                              + (SlotLabelsUnder ? 0 : UiMetrics.Space(8) + SlotLabelLane));
    private static Rectangle HunterBox
    {
        get
        {
            var w = Math.Clamp(DollRoom, UiMetrics.Control(120), DollWidth);
            return new(EquippedPanel.Center.X - w / 2, DollTop, w, Math.Max(UiMetrics.Control(120), FooterStrip.Y - UiMetrics.Space(16) - DollTop));
        }
    }
    private static readonly GearSlot[] AllSlots =
    {
        GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus, GearSlot.Helm,
        GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring,
    };

    // ── THE FOOTER: the count strip, then the two utility buttons, stacked up from the panel's content bottom. ──
    /// <summary>Where the two buttons end: the house bottom pad and a breath above the panel's EDGE (1000 at 100 %) — the crest drop, again, not counted.</summary>
    private static int FooterButtonsBottom => EquippedPanel.Bottom - UiTypography.PanelPadBottom - UiMetrics.Space(8);
    /// <summary>One Body line and its breath: 48 at 100 %.</summary>
    private static int FooterStripH => UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(20);
    /// <summary>A set chip's height: one Secondary line in a bordered plate. 30 at 100 %.</summary>
    private static int SetChipH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    private static Rectangle FooterStrip
        => new(UiKit.ContentLeft(EquippedPanel), FooterButtonsBottom - UiMetrics.ButtonHeight - UiMetrics.Space(10) - FooterStripH,
               UiKit.ContentRight(EquippedPanel) - UiKit.ContentLeft(EquippedPanel), FooterStripH);
    private static Rectangle EquipBestBtn
        => new(FooterStrip.X, FooterButtonsBottom - UiMetrics.ButtonHeight, (FooterStrip.Width - Gutter) / 2, UiMetrics.ButtonHeight);
    private static Rectangle UnequipAllBtn
        => new(EquipBestBtn.Right + Gutter, EquipBestBtn.Y, FooterStrip.Width - EquipBestBtn.Width - Gutter, UiMetrics.ButtonHeight);

    // ── THE INVENTORY: a pill row, then a grid filling the content width; flat empty cells (§63). ──
    private static int InvGap => UiMetrics.Space(8);
    private static int InvX => UiKit.ContentLeft(InventoryPanel);
    /// <summary>The lane the grid yields to its scrollbar while a row waits below — 0 when every row fits (gear-20).</summary>
    private static int s_invLane;
    private static int InvW => UiKit.ContentRight(InventoryPanel) - InvX - s_invLane;
    private static readonly string[] Tabs = { "ALL", "WEAPONS", "ARMOR", "ACCESSORY" };
    /// <summary>A tab's height: a Secondary line with its pad, never under the hit-target minimum (§107).</summary>
    private static int TabH => Math.Max(UiMetrics.HitTargetMinimum, UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(14));
    private static int TabsTop => UiKit.CaptionTop(InventoryPanel) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(12);
    /// <summary>
    /// ONE ROW OF FOUR while the widest label fits a quarter of the width; TWO ROWS OF TWO once it does not
    /// (125 % and up — "ACCESSORY" at 24 px is wider than a 98 px tab). A stacked control (§9), not a shorter
    /// word and not a smaller one.
    /// </summary>
    private int TabRows
    {
        get
        {
            var one = (InvW - InvGap * (Tabs.Length - 1)) / Tabs.Length;
            var widest = 0;
            foreach (var t in Tabs) widest = Math.Max(widest, _ui.MeasureBig(t, UiTypography.Secondary));
            return widest + UiTypography.ChipPadX * 2 <= one ? 1 : 2;
        }
    }
    private Rectangle TabRect(int i)
    {
        var per = Tabs.Length / TabRows;
        var w = (InvW - InvGap * (per - 1)) / per;
        return new(InvX + i % per * (w + InvGap), TabsTop + i / per * (TabH + InvGap), w, TabH);
    }
    private int InvTop => TabsTop + TabRows * (TabH + InvGap) - InvGap + UiMetrics.Space(12);
    /// <summary>FOUR COLUMNS while a cell keeps a comfortable icon; THREE once it would not (150 %, §9).</summary>
    private static int InvCols => (InvW - InvGap * 3) / 4 >= UiMetrics.Control(64) ? 4 : 3;
    private static int InvCell => (InvW - InvGap * (InvCols - 1)) / InvCols;
    private const string SortedLabel = "SORTED BY RARITY", MoreLabel = "RIGHT-CLICK FOR MORE";
    /// <summary>The two footer lines share a row while both fit it; they stack once they do not (150 %).</summary>
    private bool InvFooterStacked
        => _ui.MeasureBig(SortedLabel, UiTypography.Secondary) + _ui.MeasureBig(MoreLabel, UiTypography.Secondary) + UiMetrics.Space(24) > InvW;
    // ANCHORED TO THE FRAME, NOT TO THE RECTANGLE. The panel wears the house quiet frame now, so its
    // last usable row is UiKit.ContentBottom, not `Bottom - 16`: measured from the raw edge the stacked
    // footer at UI SCALE 150 printed RIGHT-CLICK FOR MORE across the frame's own bottom rail.
    private int InvFooterTop
        => UiKit.ContentBottom(InventoryPanel) - UiTypography.Pitch(UiTypography.Secondary) * (InvFooterStacked ? 2 : 1);
    /// <summary>ROWS FROM THE ROOM: as many as fit between the tabs and the footer, never printing over either.</summary>
    private int InvRows => Math.Max(1, (InvFooterTop - UiMetrics.Space(12) - InvTop + InvGap) / (InvCell + InvGap));
    private Rectangle InvCellRect(int i)
        => new(InvX + i % InvCols * (InvCell + InvGap), InvTop + i / InvCols * (InvCell + InvGap), InvCell, InvCell);

    // ── THE INSPECTOR'S ACTIONS: the verb row and the one primary button, anchored to the panel's bottom. ──
    private static int DetX => UiKit.ContentLeft(DetailPanel);
    private static int DetW => UiKit.ContentRight(DetailPanel) - DetX;
    /// <summary>The verb row's height: a Secondary line, never under the hit-target minimum (§107).</summary>
    private static int VerbRowH => UiMetrics.ButtonHeightSmall;
    /// <summary>The breath between the three verb buttons.</summary>
    private static int VerbGap => UiMetrics.Space(8);
    private static Rectangle EquipBtn
        => new(DetX, UiKit.ContentBottom(DetailPanel) - UiMetrics.Space(4) - UiMetrics.ButtonHeightPrimary, DetW, UiMetrics.ButtonHeightPrimary);
    private static Rectangle VerbText(int i)
        => new(DetX + i * ((DetW - 2 * VerbGap) / 3 + VerbGap), EquipBtn.Y - UiMetrics.Space(8) - VerbRowH, (DetW - 2 * VerbGap) / 3, VerbRowH);
    private static readonly (ItemAction Action, string Label)[] Verbs =
    {
        (ItemAction.Upgrade, "UPGRADE"), (ItemAction.Reforge, "REFORGE"), (ItemAction.Salvage, "SALVAGE"),
    };

    private int _tab;
    private int _invScroll;
    private string? _selectedId;
    private ItemInstance? _hovered;
    private string? _tip;
    private Rectangle _tipAt;

    /// <summary>The CELL the hover card explains — the card hangs off it, so it does not chase the pointer.</summary>
    private Rectangle _hoveredAt;

    // THE INSPECTOR'S SCROLL, in whole items from the top (§18: long inspectors scroll; the verbs and EQUIP
    // never do). What the last Draw measured tells the next Update whether there is anything below to reach.
    private int _detScroll;
    private string? _detScrollFor;
    private bool _detOverflow;
    private const int DetailScrollStep = 2;   // items per wheel notch

    /// <summary>Everything in the bag that fits a slot — whether or not THIS hunter may wear it.</summary>
    private List<ItemInstance> Wearable()
        => _forge.Inventory.Where(i => Gear.SlotFor(i.BaseType) is not null).ToList();

    private bool CanWearNow(ItemInstance item) => Gear.CanWear(Character, item);

    /// <summary>
    /// Put the carried piece where it was dropped: on its own slot, or back in the bag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It goes through <c>hunter.Equip</c> / <c>hunter.Unequip</c> — the same two calls the inspector's
    /// button makes — so the drag cannot equip something the button would refuse, and a piece cannot
    /// end up somewhere the model does not agree it is.
    /// </para>
    /// <para>
    /// <b>A DROP ON THE WRONG SLOT IS ANSWERED, NOT IGNORED.</b> Boots dropped on the helm slot play the
    /// refusal cue, because silence there reads as a broken drag rather than as a rule. A drop on
    /// nothing at all is a cancel and says nothing, which is what letting go over empty space means.
    /// </para>
    /// </remarks>
    private void DropGear(Hunter hunter, string itemId, Point at)
    {
        var item = Wearable().FirstOrDefault(i => i.InstanceId == itemId)
                   ?? AllSlots.Select(hunter.Worn).FirstOrDefault(w => w?.InstanceId == itemId);
        if (item is null) return;
        _selectedId = itemId;

        foreach (var (slot, _, col, row) in SlotLayout)
        {
            if (!SlotRect(col, row).Contains(at)) continue;
            if (Gear.SlotFor(item.BaseType) != slot || !CanWearNow(item))
            {
                _feedback.Cue("sfx_error");
                return;
            }
            if (_carryFromSlot == slot) return;   // dropped back where it came from: nothing happened
            hunter.Equip(item);
            ArmFlight(item, _carryFromRect, slot);
            Dirty = true;
            _feedback.Cue("sfx_equip");
            return;
        }

        // OFF THE DOLL AND INTO THE BAG. Only from a worn slot, and only onto the bag itself — dropping
        // a piece on the doll's portrait or on the page's margin is a change of mind.
        if (_carryFromSlot is { } fromSlot && InventoryPanel.Contains(at))
        {
            hunter.Unequip(fromSlot);
            Dirty = true;
            _feedback.Cue("sfx_equip");
        }
    }

    private static bool InTab(ItemBaseType t, int tab) => tab switch
    {
        1 => t == ItemBaseType.Weapon,
        2 => t is ItemBaseType.Helm or ItemBaseType.Chest or ItemBaseType.Gloves or ItemBaseType.Boots,
        3 => t is ItemBaseType.Charm or ItemBaseType.AbilityFocus or ItemBaseType.Ring,
        _ => true,
    };

    /// <summary>The grid's list: bag wearables on the active tab, worn pieces excluded (they live on the doll).</summary>
    private List<ItemInstance> Filtered(Hunter hunter)
        => Wearable().Where(i => InTab(i.BaseType, _tab) && !IsWorn(hunter, i))
            .OrderByDescending(i => (int)i.Rarity).ThenByDescending(i => i.ItemLevel).ToList();

    // ── ITEM VERBS: request/consume — this screen names the verb and the item; Game1 routes it. ─────────
    public (string InstanceId, ItemAction Action)? ConsumeItemAction()
    {
        var r = _itemAction;
        _itemAction = null;
        return r;
    }
    private (string InstanceId, ItemAction Action)? _itemAction;

    private string? _menuItemId;
    private Point _menuAt;
    private static readonly (ItemAction Action, string Label)[] MenuEntries =
    {
        (ItemAction.Equip, "EQUIP"), (ItemAction.Upgrade, "UPGRADE"), (ItemAction.Reforge, "REFORGE"), (ItemAction.Salvage, "SALVAGE"),
    };
    // The right-click menu: a header naming the item, then one row per verb. The rows follow the Body rung
    // they hold (44 at 100 %), so they are not tight at 150 % and each is a hit target of its own height.
    private static int MenuW => UiMetrics.Control(250);
    private static int MenuRowH => UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(16);
    private static int MenuHeaderH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(10);
    private static int MenuPad => UiMetrics.Space(6);
    private static int MenuH => MenuEntries.Length * MenuRowH + MenuHeaderH + MenuPad * 2;
    private Rectangle MenuRect => new(_menuAt.X, _menuAt.Y, MenuW, MenuH);
    /// <summary>One menu row — drawn and hit-tested from the same rectangle (LAW 5).</summary>
    private Rectangle MenuRow(int i)
        => new(MenuRect.X + MenuPad, MenuRect.Y + MenuHeaderH + MenuPad + i * MenuRowH, MenuW - MenuPad * 2, MenuRowH - UiMetrics.Space(4));

    /// <summary>The character being played — the doll shows them.</summary>
    public Character Character { get; set; } = CharacterRoster.Get(CharacterRoster.StarterId);
    private float _anim;

    // ── DRAG AND DROP, AND THE FLIGHT THAT FOLLOWS IT ────────────────────────────────────────────
    //
    // Playtest 2026-09-09: "items in the inventory should be draggable, drag-and-drop should work.
    // There should also be an equipped effect and animation. The player should feel good."
    //
    // Both halves are one idea. Equipping was a click on a button in a third column, and the piece
    // simply appeared on the doll — the object never moved, so nothing on screen connected the thing
    // picked up with the place it went. Dragging makes the player perform the move, and the FLIGHT
    // makes the game perform it back: on any equip, from any path, the piece travels from where it was
    // to the slot it lands in and the slot flares as it arrives.
    //
    // The click path, the right-click menu and EQUIP HIGHEST POWER all still work and all arm the same
    // flight, so the reward is a property of equipping rather than of one gesture.

    /// <summary>How far the pointer must travel before a press becomes a drag rather than a click.</summary>
    private const int DragSlop = 4;

    private string? _carryId;
    private Rectangle _carryFromRect;
    private GearSlot? _carryFromSlot;
    private Point _carryFrom;
    private Point _carryAt;
    private bool _carryMoved;
    private bool _wasHeld;

    /// <summary>Is a piece actually in flight under the hand? A press that never moved is still a click.</summary>
    private bool Dragging => _carryId is not null && _carryMoved;

    /// <summary>Where a POSED drag holds its hand — the capture rig's only way to aim one. Null in play.</summary>
    private Point? _devDragAt;

    /// <summary>The piece travelling to the slot it was just put in, and the two rectangles it crosses.</summary>
    private (ItemInstance Item, Rectangle From, Rectangle To)? _flight;

    /// <summary>The flight's one-shot. One at a time: a second equip replaces the first, as the eye would.</summary>
    private static readonly int FlightKey = HashCode.Combine("gear.flight");

    /// <summary>
    /// Send a piece from where it sat to the slot it now occupies, and flare the slot when it lands.
    /// </summary>
    /// <remarks>
    /// Armed from every equip path — the inspector's button, the drop, the menu, EQUIP HIGHEST POWER —
    /// so "I equipped something" always looks the same. A missing source rectangle (an equip with no
    /// visible origin, such as the bulk button's later pieces) simply flies from the bag's centre,
    /// which is where those pieces actually were.
    /// </remarks>
    private void ArmFlight(ItemInstance? item, Rectangle from, GearSlot slot)
    {
        if (item is null || UiMotion.Reduced) return;
        var to = SlotRect(SlotLayout.First(l => l.Slot == slot).Column, SlotLayout.First(l => l.Slot == slot).Row);
        if (from.Width <= 0) from = new Rectangle(InventoryPanel.Center.X - to.Width / 2, InventoryPanel.Center.Y - to.Height / 2, to.Width, to.Height);
        _flight = (item, from, to);
        UiMotion.Flash(FlightKey, UiMotion.Reward);
    }

    // ── UPDATE ──────────────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, bool rightClicked, int wheel, Hunter hunter)
    {
        var hit = mouse;

        if (DevReduced) UiMotion.Reduced = true;   // dev capture dial, see DevReduced

        // THE DOLL'S IDLE, advanced here rather than in Draw, and not at all under Reduced Motion (§32:
        // drop idle motion). Fixed-step: one Update is one sixtieth.
        if (!UiMotion.Reduced) _anim += 1f / 60f;

        // THE FEEDBACK OBSERVES THE HUNTER FIRST, every frame, and fires on what changed since the last
        // look. Silent on the first look, on a change of character, and after time away (see _lastUpdateMs).
        var now = Environment.TickCount64;
        var stale = _lastUpdateMs < 0 || now - _lastUpdateMs > StaleMs || _observedCharacterId != Character.Id;
        _lastUpdateMs = now;
        _observedCharacterId = Character.Id;
        DevPose(hunter);
        var moved = _feedback.Observe(hunter, stale);
        // EQUIP HIGHEST POWER's reason to be off costs a bench run per bag weapon (DamageBench), so it is
        // taken when the gear actually moved — an equip, a re-entry — and never on a frame where nothing did.
        if (moved || stale) _anyHigherPower = AnyHigherPower(hunter);

        var list = Filtered(hunter);

        // The selection is adopted only when the item is GONE from the bag, not merely off-tab.
        if (_selectedId is null || (Wearable().All(i => i.InstanceId != _selectedId) && AllSlots.Select(hunter.Worn).All(w => w?.InstanceId != _selectedId)))
            _selectedId = list.FirstOrDefault()?.InstanceId;

        var rows = (list.Count + InvCols - 1) / InvCols;
        _invScroll = UiKit.Scrolled(_invScroll, wheel != 0 && InventoryPanel.Contains(hit) ? Math.Sign(wheel) : 0, InvRows, rows);
        if (wheel != 0 && DetailPanel.Contains(hit))
        {
            // Down only while the last Draw left something under the floor; up to the top.
            if (wheel < 0 && _detOverflow) _detScroll += DetailScrollStep;
            else if (wheel > 0) _detScroll = Math.Max(0, _detScroll - DetailScrollStep);
        }

        // ── THE CARRY, RESOLVED BEFORE ANY EARLY RETURN. ────────────────────────────────────────
        //
        // This method returns from a dozen places once it has spent a click, so the drag has to be
        // handled above all of them or a release would be eaten by whichever branch happened to match.
        var held = UiKit.MouseHeld;
        var pressed = held && !_wasHeld;
        var releasedDrag = !held && _wasHeld;
        _wasHeld = held;
        _carryAt = _devDragAt ?? hit;
        if (_carryId is not null && held
            && (Math.Abs(hit.X - _carryFrom.X) > DragSlop || Math.Abs(hit.Y - _carryFrom.Y) > DragSlop))
            _carryMoved = true;
        if (!held)
        {
            if (releasedDrag && _carryId is { } dropped && _carryMoved) DropGear(hunter, dropped, hit);
            _carryId = null;
            _carryFromSlot = null;
            _carryMoved = false;
        }
        else if (pressed && !rightClicked)
        {
            // FROM THE BAG, or off the doll. Picking a worn piece up and dropping it on the bag is the
            // drag path's TAKE OFF, and picking one out of the bag and dropping it on its slot is the
            // equip — the two gestures the third column's buttons were the only way to reach.
            for (var vis = 0; vis < InvCols * InvRows && _carryId is null; vis++)
            {
                var idx = _invScroll * InvCols + vis;
                if (idx >= list.Count) break;
                if (!InvCellRect(vis).Contains(hit)) continue;
                _carryId = list[idx].InstanceId;
                _carryFromRect = InvCellRect(vis);
                _carryFromSlot = null;
                _carryFrom = hit;
                _carryMoved = false;
            }
            foreach (var (slot, _, col, row) in SlotLayout)
            {
                if (_carryId is not null || !SlotRect(col, row).Contains(hit)) continue;
                if (hunter.Worn(slot) is not { } wornPick) continue;
                _carryId = wornPick.InstanceId;
                _carryFromRect = SlotRect(col, row);
                _carryFromSlot = slot;
                _carryFrom = hit;
                _carryMoved = false;
            }
        }

        if (rightClicked)
        {
            for (var vis = 0; vis < InvCols * InvRows; vis++)
            {
                var idx = _invScroll * InvCols + vis;
                if (idx >= list.Count) break;
                if (!InvCellRect(vis).Contains(hit)) continue;
                _selectedId = list[idx].InstanceId;
                OpenMenu(list[idx].InstanceId, InvCellRect(vis));
                return;
            }
            foreach (var (slot, _, col, row) in SlotLayout)
                if (SlotRect(col, row).Contains(hit) && hunter.Worn(slot) is { } wornItem)
                {
                    _selectedId = wornItem.InstanceId;
                    OpenMenu(wornItem.InstanceId, SlotRect(col, row));
                    return;
                }
            _menuItemId = null;
            return;
        }

        if (!clicked) return;

        if (_menuItemId is not null)
        {
            MenuHit(hit);
            _menuItemId = null;
            return;
        }

        for (var i = 0; i < Tabs.Length; i++)
            if (TabRect(i).Contains(hit))
            {
                _tab = i; _invScroll = 0; _selectedId = Filtered(hunter).FirstOrDefault()?.InstanceId;
                _feedback.Cue("sfx_click");
                return;
            }

        // A worn slot: select it. (Taking off is the inspector's TAKE OFF, or the menu — one gesture, said.)
        foreach (var (slot, _, col, row) in SlotLayout)
            if (SlotRect(col, row).Contains(hit) && hunter.Worn(slot) is { } worn) { _selectedId = worn.InstanceId; _feedback.Cue("sfx_click"); return; }

        for (var vis = 0; vis < InvCols * InvRows; vis++)
        {
            var idx = _invScroll * InvCols + vis;
            if (idx >= list.Count) break;
            if (InvCellRect(vis).Contains(hit)) { _selectedId = list[idx].InstanceId; _feedback.Cue("sfx_click"); return; }
        }

        if (Selected(hunter) is { } sel)
        {
            if (EquipBtn.Contains(hit))
            {
                // The equip itself is the cue's moment; the slot pulse and the power tick follow from the
                // diff, not from here. A press on CANNOT WEAR is a refusal, and a refusal is heard (§86).
                if (IsWorn(hunter, sel) && Gear.SlotFor(sel.BaseType) is { } ws) { hunter.Unequip(ws); Dirty = true; _feedback.Cue("sfx_equip"); }
                else if (!IsWorn(hunter, sel) && CanWearNow(sel))
                {
                    // FROM THE CELL IT IS SITTING IN, so the button's equip animates like the drag's.
                    var cellFrom = Rectangle.Empty;
                    for (var vis = 0; vis < InvCols * InvRows; vis++)
                    {
                        var idx = _invScroll * InvCols + vis;
                        if (idx < list.Count && list[idx].InstanceId == sel.InstanceId) { cellFrom = InvCellRect(vis); break; }
                    }
                    hunter.Equip(sel);
                    if (Gear.SlotFor(sel.BaseType) is { } into) ArmFlight(sel, cellFrom, into);
                    Dirty = true;
                    _feedback.Cue("sfx_equip");
                }
                else _feedback.Cue("sfx_error");
                return;
            }
            for (var v = 0; v < Verbs.Length; v++)
                if (VerbText(v).Contains(hit)) { _itemAction = (sel.InstanceId, Verbs[v].Action); return; }
        }

        // A BUTTON DRAWN OFF DOES NOT ACT — it answers. Both utilities are disabled when they would
        // change nothing (§29), and a press on one is a refusal, heard rather than swallowed (§86).
        if (EquipBestBtn.Contains(hit))
        {
            _feedback.Cue(_anyHigherPower && EquipHighestPower(hunter) ? "sfx_equip" : "sfx_error");
            return;
        }
        if (UnequipAllBtn.Contains(hit))
        {
            if (AllSlots.Any(s => hunter.Worn(s) is not null))
            {
                foreach (var s in AllSlots) hunter.Unequip(s);
                Dirty = true;
                _feedback.Cue("sfx_equip");
            }
            else _feedback.Cue("sfx_error");
            return;
        }
    }

    /// <summary>DEV: the capture pose — see <see cref="DevPoseSpec"/>. Inert without the variable.</summary>
    private void DevPose(Hunter hunter)
    {
        if (DevPoseSpec is null) return;
        var kind = DevPoseSpec;
        var lead = 5;
        var at = kind.IndexOf('@');
        if (at > 0 && int.TryParse(kind[(at + 1)..], out var n)) { lead = Math.Max(0, n); kind = kind[..at]; }
        // PRESSED is not an event but a HELD button, and the rig has no hands: this holds it down for the
        // whole run, and RH_SHOT_PAGE_MOUSE says what it is held over.
        if (kind == "press") { UiKit.MouseHeld = true; return; }
        // A DRAG IS A STATE NO CAPTURE COULD POSE, and a state no capture can pose has never been looked
        // at — which is how every wrong UI state in this project has been found. This holds the first
        // wearable bag piece in the hand, already past the slop, so the shutter sees the ghost, the lit
        // target slot and the dimmed cell it came out of. RH_SHOT_PAGE_MOUSE says where the hand is.
        if (kind == "drag")
        {
            var first = Wearable().FirstOrDefault(i => Gear.SlotFor(i.BaseType) is not null && CanWearNow(i));
            if (first is null || Gear.SlotFor(first.BaseType) is not { } goesTo) return;
            var list = Filtered(hunter);
            // HELD, or the lifecycle clears the hand on its own next line: the rig has no buttons, and a
            // carry with the mouse up is a carry that has just been dropped.
            UiKit.MouseHeld = true;
            _carryId = first.InstanceId;
            _selectedId = first.InstanceId;
            _carryFromSlot = null;
            _carryMoved = true;
            for (var vis = 0; vis < InvCols * InvRows; vis++)
            {
                var idx = _invScroll * InvCols + vis;
                if (idx < list.Count && list[idx].InstanceId == first.InstanceId) { _carryFromRect = InvCellRect(vis); break; }
            }
            // ...and the hand is put over the slot this piece actually belongs in, so the pose shows the
            // lit target rather than needing the caller to compute a rectangle the screen owns.
            // ...and unless the caller has aimed the pointer itself (RH_SHOT_PAGE_MOUSE), the hand is put
            // over the slot this piece belongs in, so the pose shows the lit target without the caller
            // having to compute a rectangle the screen owns. With a page mouse given, the rig wins: that
            // is how a MID-FLIGHT frame — ghost between the bag and the doll — is photographed at all.
            if (Environment.GetEnvironmentVariable("RH_SHOT_PAGE_MOUSE") is not { Length: > 0 })
            {
                var seat = SlotLayout.First(l => l.Slot == goesTo);
                _devDragAt = SlotRect(seat.Column, seat.Row).Center;
            }
            return;
        }
        if (!_devPosePrepared)
        {
            _devPosePrepared = true;
            switch (kind)
            {
                // Four NATURE pieces off on the first frame — the frame the feedback primes on, so nothing
                // fires — leaves the set at four of five: the chest going back on is the 4 → 5 step.
                case "equip":
                    _devPoseItem = hunter.Unequip(GearSlot.Chest);
                    hunter.Unequip(GearSlot.Helm);
                    hunter.Unequip(GearSlot.Gloves);
                    hunter.Unequip(GearSlot.Boots);
                    break;
                case "best": EquipHighestPower(hunter); break;
                case "bare": foreach (var s in AllSlots) hunter.Unequip(s); break;
            }
        }
        if (kind != "equip") return;
        // ShotFrameNow is the last DRAWN frame; this Update precedes the next one. Firing when the drawn
        // count is ShotAtFrame − 1 − lead puts the equip in the Update before Draw (ShotAtFrame − lead), so
        // the shutter frame has seen exactly `lead` ticks of the pulse.
        if (!_devPoseFired && _devPoseItem is { } it && Game1.ShotFrameNow >= Game1.ShotAtFrame - 1 - lead)
        {
            _devPoseFired = true;
            hunter.Equip(it);
            _selectedId = it.InstanceId;
        }
    }

    // ── EQUIP HIGHEST POWER's reason to be off: nothing in the bag beats what is worn (§29). Once per CHANGE. ──
    private bool _anyHigherPower;
    private bool AnyHigherPower(Hunter hunter)
    {
        foreach (var item in _forge.Inventory)
        {
            if (Gear.SlotFor(item.BaseType) is not { } slot || !CanWearNow(item)) continue;
            var worn = hunter.Worn(slot);
            if (worn?.InstanceId == item.InstanceId) continue;
            if (slot == GearSlot.Weapon ? WeaponDps(hunter, item) > WeaponDps(hunter, worn) * 1.001f
                                        : hunter.PowerContribution(item) > hunter.PowerContribution(worn))
                return true;
        }
        return false;
    }

    private void OpenMenu(string instanceId, Rectangle anchor)
    {
        _menuItemId = instanceId;
        var x = Math.Min(anchor.Right + UiMetrics.Space(8), UiKit.PageRight(UiMetrics.Space(16)) - MenuW);
        var y = Math.Min(anchor.Y, UiKit.PageBottom(UiMetrics.Space(16)) - MenuH);
        _menuAt = new Point(x, y);
    }

    private void MenuHit(Point hit)
    {
        if (_menuItemId is null || !MenuRect.Contains(hit)) return;
        for (var i = 0; i < MenuEntries.Length; i++)
        {
            if (!MenuRow(i).Contains(hit)) continue;
            if (MenuEntries[i].Action == ItemAction.Equip
                && Wearable().FirstOrDefault(it => it.InstanceId == _menuItemId) is { } bagItem && !CanWearNow(bagItem))
            {
                _feedback.Cue("sfx_error");   // CANNOT WEAR is not a verb — but a press on it is a refusal, and heard
                return;
            }
            _itemAction = (_menuItemId, MenuEntries[i].Action);
            return;
        }
    }

    private ItemInstance? Selected(Hunter hunter)
    {
        if (_selectedId is null) return null;
        return Wearable().FirstOrDefault(i => i.InstanceId == _selectedId)
            ?? AllSlots.Select(hunter.Worn).OfType<ItemInstance>().FirstOrDefault(i => i.InstanceId == _selectedId);
    }

    private static bool IsWorn(Hunter hunter, ItemInstance item)
        => Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId;

    /// <summary>
    /// EQUIP HIGHEST POWER: per slot, the wearable bag item with the highest ITEM POWER if it beats what is worn;
    /// the weapon by bench damage with THIS build. Named for what it compares (§65) — it does not see set bonuses.
    /// Returns whether anything went on.
    /// </summary>
    private bool EquipHighestPower(Hunter hunter)
    {
        var changed = false;
        foreach (var slot in AllSlots)
        {
            if (slot == GearSlot.Weapon)
            {
                var bestWeapon = Wearable().Where(i => Gear.SlotFor(i.BaseType) == GearSlot.Weapon && CanWearNow(i))
                    .OrderByDescending(i => WeaponDps(hunter, i)).FirstOrDefault();
                if (bestWeapon is not null && WeaponDps(hunter, bestWeapon) > WeaponDps(hunter, hunter.Worn(GearSlot.Weapon)) * 1.001f)
                { hunter.Equip(bestWeapon); changed = true; }
                continue;
            }
            var best = Wearable().Where(i => Gear.SlotFor(i.BaseType) == slot && CanWearNow(i))
                .OrderByDescending(hunter.PowerContribution).FirstOrDefault();
            if (best is not null && hunter.PowerContribution(best) > hunter.PowerContribution(hunter.Worn(slot)))
            { hunter.Equip(best); changed = true; }
        }
        if (changed) Dirty = true;
        return changed;
    }

    // ── The weapon's real number: bench DPS with it worn, swap-and-restore, cached by everything the bench reads. ──
    private readonly Dictionary<string, float> _dpsCache = new();
    private float WeaponDps(Hunter hunter, ItemInstance? weapon)
    {
        if (Loadout is null || Mastery is null)
            return weapon is null ? 0f : hunter.PowerContribution(weapon);
        var others = string.Join(",", AllSlots.Where(sl => sl != GearSlot.Weapon).Select(sl => hunter.Worn(sl)?.InstanceId ?? "-"));
        var skills = string.Join(",", Loadout.Skills.Select(sk => $"{sk.Source}:{sk.SkillId}:{sk.VowId}"));
        var keystones = string.Join(",", Loadout.KeystoneIds);
        // THE KEY MUST MOVE WHEN THE BUILD DOES. Keystone and Vow knowledge come from the world now,
        // so the tree's owned ids no longer change when the player earns one - a key built from them
        // alone would serve a stale damage number for the rest of the session.
        var trees = $"{Mastery.Taken.Count}:{string.Join(",", Mastery.Taken)}"
                  + $"|{DiscoveredKeystones.Count}|{KnownVows.Count}";
        var key = $"{weapon?.InstanceId ?? "-"}|{weapon?.ItemLevel}|{others}|{skills}|{keystones}|{trees}|{hunter.PowerRating}|{Character.Id}";
        if (_dpsCache.TryGetValue(key, out var cached)) return cached;

        var was = hunter.Worn(GearSlot.Weapon);
        if (weapon is null) hunter.Unequip(GearSlot.Weapon); else hunter.Equip(weapon);
        float dps;
        try { dps = DamageBench.Measure(Loadout.ToBuild(Mastery, Character, SkillLevels, DiscoveredKeystones, KnownVows), hunter).Dps; }
        finally { if (was is null) hunter.Unequip(GearSlot.Weapon); else hunter.Equip(was); }
        if (_dpsCache.Count > 512) _dpsCache.Clear();
        _dpsCache[key] = dps;
        return dps;
    }

    // ── DRAW ────────────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        var hit = mouse;
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0B, 0x09, 0x08, 0xD8));

        _ui.TextCenterBig(b, "GEAR", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        _hovered = null;
        _hoveredAt = Rectangle.Empty;
        _tip = null;
        DrawEquipped(b, hit, hunter);
        DrawInventory(b, hit, hunter);
        DrawDetail(b, hit, hunter);
        DrawItemMenu(b, hit, hunter);

        // The hover card answers "what is this" without a click — never to the right over the inspector.
        if (_hovered is { } hov && _menuItemId is null)
            // The card's canvas stops above the doll's footer strip, so a hovered bag cell never blanks the
            // EQUIPPED buttons or the strip beside the doll (gear-11).
            // THE WHOLE PAGE'S HEIGHT (2026-09-07): the card is clamped into this canvas, and a canvas
            // that stopped at the footer strip was shorter than the card at 150 % — the plate was cut
            // to the canvas while the walk drew every line, so the last three hung off the plate over
            // the doll's footer. The strip is inside the EQUIPPED panel the card already floats over.
            ItemTooltip.Draw(_ui, b, hov, hunter, _hoveredAt, new Rectangle(0, 0, DetailPanel.X - UiMetrics.Space(8), UiKit.Page.Height), Character);
        else if (_tip is { } tip && _menuItemId is null) _ui.HoverTip(b, tip, _tipAt);
        // LAST, over every panel: the piece in the hand must never slide behind the column it is being
        // carried between, and the flight crosses two panels by definition.
        DrawCarriedGear(b, hunter);
        if (DevGearDebug) DrawDebug(b, hunter);
    }

    /// <summary>
    /// Open the item menu on a cell — used by the headless capture to pose it. RH_SHOT_GEAR_MENU=&lt;instanceId&gt;
    /// picks WHICH item, so the menu's CANNOT WEAR row (a class-locked piece) is photographable too; without
    /// it the menu opens on the first item in the bag, as it always has.
    /// </summary>
    public void DevOpenItemMenu()
    {
        var bag = Wearable();
        var want = Environment.GetEnvironmentVariable("RH_SHOT_GEAR_MENU");
        var pick = (want is { Length: > 0 } ? bag.FirstOrDefault(i => i.InstanceId == want) : null) ?? bag.FirstOrDefault();
        if (pick is null) return;
        _selectedId = pick.InstanceId;
        OpenMenu(pick.InstanceId, InvCellRect(0));
    }

    private void DrawItemMenu(SpriteBatch b, Point hit, Hunter hunter)
    {
        if (_menuItemId is null) return;
        var item = Wearable().FirstOrDefault(i => i.InstanceId == _menuItemId)
                   ?? AllSlots.Select(hunter.Worn).OfType<ItemInstance>().FirstOrDefault(i => i.InstanceId == _menuItemId);
        if (item is null) { _menuItemId = null; return; }

        var worn = IsWorn(hunter, item);
        var rc = RarityColor(item.Rarity);
        var box = MenuRect;
        _ui.Fill(b, new Rectangle(box.X + 4, box.Y + 4, box.Width, box.Height), new Color(0, 0, 0, 0xA0));
        _ui.Fill(b, box, new Color(0x15, 0x10, 0x0F, 0xF6));
        Ring(b, box, rc, 3);
        var hx = box.X + UiMetrics.Space(14);
        _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), box.Width - UiMetrics.Space(14) * 2, UiTypography.Secondary),
                    hx, box.Y + (MenuHeaderH - UiTypography.Secondary) / 2, rc, UiTypography.Secondary);
        _ui.Fill(b, new Rectangle(box.X + UiMetrics.Space(8), box.Y + MenuHeaderH - UiMetrics.Space(4), box.Width - UiMetrics.Space(8) * 2, 1), Dim);
        string? lockedWhy = null;
        var lockedRow = Rectangle.Empty;
        for (var i = 0; i < MenuEntries.Length; i++)
        {
            var (action, label) = MenuEntries[i];
            var row = MenuRow(i);
            var locked = action == ItemAction.Equip && !worn && !CanWearNow(item);
            var shown = action == ItemAction.Equip && worn ? "TAKE OFF" : locked ? "CANNOT WEAR" : label;
            var hover = row.Contains(hit) && !locked;
            // HOVER eases in (§26); PRESSED is the held button — the row's text drops a pixel (§27); a
            // locked row keeps its label readable and says why under the pointer (§29).
            var lift = UiMotion.Ease(UiMotion.KeyOf(row), hover ? 1f : 0f);
            var pressed = hover && UiKit.MouseHeld;
            if (lift > 0f) _ui.Fill(b, row, new Color(0x36, 0x2A, 0x4E) * lift);
            if (pressed) _ui.Fill(b, row, Color.Black * 0.18f);
            _ui.TextBig(b, shown, row.X + UiMetrics.Space(16), row.Y + (row.Height - UiTypography.Body) / 2 + (pressed ? 1 : 0),
                        locked ? UiInk.Disabled : hover ? Bone : Slate, UiTypography.Body);
            if (locked && row.Contains(hit)) { lockedWhy = ItemClasses.WhyNot(Character, item); lockedRow = row; }
        }
        // THE LOCKED ROW SAYS WHY, HERE. Not through Tip(): the deferred tip is suppressed while this menu
        // is open (a card over a menu), so a Tip() on this row would have been a reason nobody ever saw —
        // and this menu is the last thing drawn, so the card belongs on top of it.
        if (lockedWhy is { } lw) _ui.HoverTip(b, lw, lockedRow);
    }

    /// <summary>The item currently in hand, resolved live off the bag or the doll.</summary>
    /// <remarks>
    /// By ID rather than by reference: a merge, a salvage or a champion switch can retire the object
    /// mid-drag, and a stale reference would draw a piece that no longer exists and then equip it.
    /// </remarks>
    private ItemInstance? CarriedItem(Hunter hunter)
        => _carryId is null ? null
           : Wearable().FirstOrDefault(i => i.InstanceId == _carryId)
             ?? AllSlots.Select(hunter.Worn).FirstOrDefault(w => w?.InstanceId == _carryId);

    /// <summary>
    /// The piece under the hand, and the piece flying to the slot it was just put in.
    /// </summary>
    /// <remarks>
    /// Drawn after every panel so neither is ever behind one. The flight is the answer to "there should
    /// be an equipped effect and animation": the object LEAVES the bag and ARRIVES on the doll, on an
    /// arc, shrinking into the slot, and the slot flares as it lands. Without it the piece teleported
    /// and the only feedback was a number changing in another column.
    /// </remarks>
    private void DrawCarriedGear(SpriteBatch b, Hunter hunter)
    {
        if (Dragging && CarriedItem(hunter) is { } held)
        {
            var side = SlotBox * 4 / 5;
            var g = new Rectangle(_carryAt.X - side / 2, _carryAt.Y - side / 2, side, side);
            _ui.Fill(b, new Rectangle(g.X + 5, g.Y + 6, g.Width, g.Height), new Color(0, 0, 0) * 0.45f);
            _ui.Plate(b, g);
            _ui.Fill(b, new Rectangle(g.X, g.Y, 5, g.Height), RarityColor(held.Rarity));
            _forge.DrawItemIcon(b, held, Shrink(g, UiMetrics.Space(6)));
            Ring(b, g, RarityColor(held.Rarity), 2);
        }

        if (_flight is not { } f || UiMotion.Reduced) return;
        var p = UiMotion.Pulse(FlightKey);
        if (p <= 0f) { _flight = null; return; }

        // 0 at the launch, 1 on arrival. Eased so it leaves fast and settles, which is what a thing
        // being PUT somewhere looks like, and lifted through an arc so it travels rather than slides.
        var t = UiMotion.Smooth(1f - p);
        var arc = MathF.Sin(t * MathF.PI) * f.From.Height * 0.55f;
        var w = (int)MathHelper.Lerp(f.From.Width, f.To.Width, t);
        var cx = MathHelper.Lerp(f.From.Center.X, f.To.Center.X, t);
        var cy = MathHelper.Lerp(f.From.Center.Y, f.To.Center.Y, t) - arc;
        var box = new Rectangle((int)cx - w / 2, (int)cy - w / 2, w, w);
        var tint = RarityColor(f.Item.Rarity);
        // A TRAIL: three ghosts along the path behind it, fading. Cheap, and it turns a moving square
        // into something with speed.
        for (var k = 1; k <= 3; k++)
        {
            var tk = Math.Max(0f, t - k * 0.07f);
            var ak = MathF.Sin(tk * MathF.PI) * f.From.Height * 0.55f;
            var wk = (int)MathHelper.Lerp(f.From.Width, f.To.Width, tk);
            var bx = (int)MathHelper.Lerp(f.From.Center.X, f.To.Center.X, tk) - wk / 2;
            var by = (int)(MathHelper.Lerp(f.From.Center.Y, f.To.Center.Y, tk) - ak) - wk / 2;
            _ui.Fill(b, new Rectangle(bx, by, wk, wk), tint * (0.16f * (1f - k / 4f)));
        }
        _forge.DrawItemIcon(b, f.Item, box);
        Ring(b, box, tint, 2);
    }

    // ── EQUIPPED: the one ornate surface — who, what they wear, what it adds up to, two actions. ──────────
    private void DrawEquipped(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = EquippedPanel;
        _ui.Panel(b, panel);
        _ui.TextCenterBig(b, "EQUIPPED", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);

        // THE HEADER ROW (moved from the LOADOUT column, §61): portrait · name · class line · GEAR POWER · innate.
        var x0 = UiKit.ContentLeft(panel);
        var hy = HeaderTop;
        // The header's right column is inside the frame's CORNER band, not beside its side rail, so it
        // asks for the corner-aware edge. Before this GEAR POWER was drawn under the ornament's curl.
        var x1 = UiKit.ContentRightAt(panel, hy, PortraitSize);
        var por = new Rectangle(x0, hy, PortraitSize, PortraitSize);
        if (_ui.Assets.GetFirst(Character.PortraitKey, "hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        // GEAR POWER TICKS (§36): the number the feedback shows runs from the old value to the new over one
        // Transition, and the digits flash gold and settle back — the true value at once under Reduced Motion.
        var powerVal = $"{_feedback.DisplayedPower:N0}";
        var powerW = Math.Max(_ui.MeasureBig("GEAR POWER", UiTypography.Secondary), _ui.MeasureBig(powerVal, UiTypography.PrimaryValue));
        // The emphasis is the DIGITS' OWN COLOUR going gold and settling back — not a plate behind them.
        // A lit rectangle here would be a panel the polish is not allowed to add (§22), and its right edge
        // sat under the frame's corner ornament.
        var emphasis = UiMotion.Smooth(_feedback.PowerEmphasis);
        _ui.TextRightBig(b, "GEAR POWER", x1, hy + UiMetrics.Space(4), Slate, UiTypography.Secondary);
        var powerY = hy + UiTypography.Pitch(UiTypography.Secondary);
        _ui.TextRightBig(b, powerVal, x1, powerY, Color.Lerp(Bone, Gold, emphasis), UiTypography.PrimaryValue);
        var tx = por.Right + UiMetrics.Space(16);
        var nameW = x1 - powerW - UiMetrics.Space(24) - tx;
        var ny = hy + UiMetrics.Space(6);
        _ui.TextBig(b, _ui.ShortenBig(Character.Name.ToUpperInvariant(), nameW, UiTypography.Headline), tx, ny, Bone, UiTypography.Headline);
        _ui.TextBig(b, _ui.ShortenBig(ItemClasses.NameOf(Character.Class), nameW, UiTypography.Body),
                    tx, ny + UiTypography.Pitch(UiTypography.Headline), UiKit.ClassColor(Character.Class), UiTypography.Body);
        // WHAT THIS HUNTER MAY WEAR — the sentence that explains every locked cell in the grid — and the
        // INNATE, the thing that most changes how a build performs, beside the gear it modifies. Each gets
        // the full content width; at the header's right column they were cut mid-clause.
        var wy = WearsTop;
        // Measured at THEIR OWN y (the side rail, not the header's corner band) and WRAPPED to two lines
        // rather than shortened — the slot column below follows (SlotsTop, gear-01).
        var wearsRight = UiKit.ContentRightAt(panel, wy, UiTypography.Pitch(UiTypography.Secondary));
        var wearsLines = _ui.WrapBig(ItemClasses.WearsLine(Character.Class).ToUpperInvariant(), wearsRight - x0, UiTypography.Secondary).Take(2).ToList();
        foreach (var l in wearsLines) { _ui.TextBig(b, l, x0, wy, Slate, UiTypography.Secondary); wy += UiTypography.Pitch(UiTypography.Secondary); }
        s_wearsLines = Math.Max(1, wearsLines.Count);
        // THE INNATE: the earned NAME in gold, the sentence in Bone — a whole gold sentence was the loudest
        // line in the header, louder than the hunter's own name (gear-17).
        var passiveName = Character.PassiveName.ToUpperInvariant();
        var innate = $"{passiveName} — {Character.PassiveText}";
        var innateRight = UiKit.ContentRightAt(panel, wy, UiTypography.Pitch(UiTypography.Body));
        var innateLines = _ui.WrapBig(innate, innateRight - x0, UiTypography.Body).Take(2).ToList();
        var innateTop = wy;
        for (var li = 0; li < innateLines.Count; li++)
        {
            var l = innateLines[li];
            if (li == 0 && l.StartsWith(passiveName, StringComparison.Ordinal))
            {
                _ui.TextBig(b, passiveName, x0, wy, Gold, UiTypography.Body);
                _ui.TextBig(b, l[passiveName.Length..], x0 + _ui.MeasureBig(passiveName, UiTypography.Body), wy, Bone, UiTypography.Body);
            }
            else _ui.TextBig(b, l, x0, wy, Bone, UiTypography.Body);
            wy += UiTypography.Pitch(UiTypography.Body);
        }
        s_innateLines = Math.Max(1, innateLines.Count);
        Tip(new Rectangle(x0, innateTop, innateRight - x0, wy - innateTop), hit, $"INNATE — {Character.PassiveName}: {Character.PassiveText}");

        // THE DOLL, dressed: the same idle strip the arena draws.
        var doll = HunterBox;
        _ui.GroundShadow(b, doll.Center.X, doll.Bottom - 8, (int)(doll.Width * 0.7f), 40, 0.55f);
        if (!_ui.AnimSprite(b, Character.StripKey("idle"), doll, _anim, 10f, loop: true, Color.White, -1f))
            _ui.SpriteGrounded(b, Character.SpriteKey, doll, Color.White, 0.02f);

        var labelsUnder = SlotLabelsUnder;
        foreach (var (slot, label, col, row) in SlotLayout)
        {
            var box = SlotRect(col, row);
            var worn = hunter.Worn(slot);
            // A worn slot is a control (it selects); an empty one is not, and stays quiet under the pointer (§50).
            var hot = worn is not null && box.Contains(hit);
            // ...but while something is IN HAND every slot is a target, empty or not, because the whole
            // question the drag answers is "where does this go".
            var carried = Dragging ? CarriedItem(hunter) : null;
            var fits = carried is not null && Gear.SlotFor(carried.BaseType) == slot && CanWearNow(carried);
            var overSlot = Dragging && box.Contains(_carryAt);
            var liftedOut = Dragging && _carryFromSlot == slot;
            var selected = worn is not null && worn.InstanceId == _selectedId;
            var lift = UiMotion.Ease(UiMotion.KeyOf(box), hot ? 1f : 0f);
            var pressed = hot && UiKit.MouseHeld;
            var pulse = _feedback.SlotPulse(slot);
            var round = slot is GearSlot.Charm or GearSlot.Focus or GearSlot.Ring;
            var slotArt = round ? "ui_slot_trinket_round" : "ui_slot_empty";
            if (_ui.Assets.Get(slotArt) is { } sa) b.Draw(sa, box, worn is not null ? Color.White : Color.White * 0.7f);
            else _ui.Fill(b, box, CellBg);
            // The ring art's well and its rim, in the art's own proportion (12 and 14 of its 102 source px).
            var well = box.Width * 12 / 102;
            var rim = box.Width * 14 / 102;
            var emptyInRing = false;
            if (worn is { } w2)
            {
                if (hot) { _hovered = w2; _hoveredAt = box; }
                // PRESSED: the piece sits two pixels lower and darker for as long as the button is held (§27).
                var drop = pressed ? 2 : 0;
                // RARITY ON THE LEFT EDGE, the grid's own grammar — a bar floating above the ring read
                // as a loose line rather than part of the slot (release polish 2026-09-05, gear-14).
                _ui.Fill(b, new Rectangle(box.X, box.Y, 5, box.Height), RarityColor(w2.Rarity));
                _forge.DrawItemIcon(b, w2, new Rectangle(box.X + well, box.Y + well + drop, box.Width - well * 2, box.Height - well * 2));
                if (lift > 0f) _ui.Fill(b, Shrink(box, rim), Color.White * (0.08f * lift));   // hover: a thin luminance lift (§26)
                if (pressed) _ui.Fill(b, Shrink(box, rim), Color.Black * 0.18f);
            }
            else
            {
                // An empty slot says so in shape and in a word — not a dash. The word sits in the ring while it
                // fits there; beside the box otherwise (a 150 % ring is narrower than EMPTY at its rung).
                Ring(b, Shrink(box, rim), UiInk.Empty, 1);
                emptyInRing = _ui.MeasureBig("EMPTY", UiTypography.Caption) <= box.Width - rim * 2;
                if (emptyInRing) _ui.TextCenterBig(b, "EMPTY", box.Center.X, box.Center.Y - UiTypography.Caption / 2, UiInk.Empty, UiTypography.Caption);
            }
            // THE SLOT THIS PIECE BELONGS IN LIGHTS THE MOMENT IT LEAVES THE BAG, before the pointer
            // ever reaches it — so the drag says where to go rather than waiting to be guessed at. The
            // one under the pointer lights harder; every other slot goes quiet so the answer is single.
            if (Dragging && carried is not null)
            {
                if (fits) _ui.Fill(b, box, Gold * (overSlot ? 0.24f : 0.10f));
                if (fits && overSlot) Ring(b, box, Gold, 3);
                else if (overSlot) Ring(b, box, UiInk.Danger, 3);   // dropped here it would be refused, and says so
            }
            if (liftedOut) _ui.Fill(b, box, new Color(0x0B, 0x09, 0x08) * 0.55f);

            if (selected) Ring(b, box, Gold, 3);   // gold = selected
            else if (lift > 0f && !Dragging) Ring(b, box, Slate * lift, 2);
            // JUST EQUIPPED (§49): one gold pulse on the slot the piece landed in — a fading wash inside the
            // ring and a halo outside it, which steps out as it fades (a fade alone under Reduced Motion).
            if (pulse > 0f)
            {
                var glow = UiMotion.Smooth(pulse);
                var grow = UiMotion.Reduced ? 2 : 2 + (int)MathF.Round(UiMetrics.Space(4) * (1f - glow));
                _ui.Fill(b, Shrink(box, rim), Gold * (0.28f * glow));
                Ring(b, Shrink(box, -grow), Gold * glow, 3);
            }

            var labelInk = worn is not null ? Bone : Slate;
            if (labelsUnder)
                _ui.TextCenterBig(b, label, box.Center.X, box.Bottom + UiMetrics.Space(6), labelInk, UiTypography.Secondary);
            else
            {
                // BESIDE THE BOX, toward the doll: the label at its own rung, and EMPTY under it when the ring
                // could not hold the word.
                var emptyBeside = worn is null && !emptyInRing;
                var lh = UiTypography.Pitch(UiTypography.Secondary) + (emptyBeside ? UiTypography.Pitch(UiTypography.Caption) : 0);
                var ly = box.Center.Y - lh / 2;
                var text = _ui.ShortenBig(label, SlotLabelLane, UiTypography.Secondary);
                if (col == 0)
                {
                    var lx = box.Right + UiMetrics.Space(8);
                    _ui.TextBig(b, text, lx, ly, labelInk, UiTypography.Secondary);
                    if (emptyBeside) _ui.TextBig(b, "EMPTY", lx, ly + UiTypography.Pitch(UiTypography.Secondary), UiInk.Empty, UiTypography.Caption);
                }
                else
                {
                    var rx = box.X - UiMetrics.Space(8);
                    _ui.TextRightBig(b, text, rx, ly, labelInk, UiTypography.Secondary);
                    if (emptyBeside) _ui.TextRightBig(b, "EMPTY", rx, ly + UiTypography.Pitch(UiTypography.Secondary), UiInk.Empty, UiTypography.Caption);
                }
            }
        }

        // THE FOOTER STRIP: how much is worn, the average item level, and the set rungs that are ON as chips.
        var wornAll = AllSlots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        var strip = FooterStrip;
        _ui.Plate(b, strip);
        var chipX = strip.Right - UiMetrics.Space(12);
        var gem = SetChipH - UiMetrics.Space(5) * 2;
        foreach (var a in ElementSets.Active(hunter).GroupBy(a => a.Element).Select(g => g.OrderByDescending(a => a.Tier.Pieces).First()))
        {
            var text = $"{a.Element.ToString().ToUpperInvariant()} {a.Tier.Pieces}";
            var textX = UiMetrics.Space(6) + gem + UiMetrics.Space(4);
            var cw = textX + _ui.MeasureBig(text, UiTypography.Secondary) + UiMetrics.Space(14);
            chipX -= cw;
            var chip = new Rectangle(chipX, strip.Y + (strip.Height - SetChipH) / 2, cw, SetChipH);
            _ui.Fill(b, chip, Gold * 0.14f);
            Ring(b, chip, Gold, 1);
            if (_ui.Assets.Get($"source_{a.Element.ToString().ToLowerInvariant()}") is { } g)
                b.Draw(g, new Rectangle(chip.X + UiMetrics.Space(6), chip.Y + (SetChipH - gem) / 2, gem, gem), Color.White);
            _ui.TextBig(b, text, chip.X + textX, chip.Y + (SetChipH - UiTypography.Secondary) / 2, Gold, UiTypography.Secondary);
            Tip(chip, hit, a.Tier.Pieces == ElementSets.Rungs[^1]
                ? $"{ElementSets.Name(a.Element)} · {ElementSets.CapstoneName(a.Element)} — {a.Tier.Line}"
                : $"{ElementSets.Name(a.Element)} — {a.Tier.Line}");
            chipX -= UiMetrics.Space(8);
        }
        var il = wornAll.Count > 0 ? (int)Math.Round(wornAll.Average(i => i.ItemLevel)) : 0;
        var countX = strip.X + UiMetrics.Space(16);
        // The set chips take the right of this strip, so what is left SHRINKS rather than dropping the
        // AVERAGE ITEM LEVEL clause — the half of the line a player cannot read anywhere else.
        var footLine = $"{wornAll.Count} / 8 EQUIPPED  ·  AVERAGE ITEM LEVEL {il}";
        var footRoom = chipX - UiMetrics.Space(12) - countX;
        var footRung = _ui.FitRung(footLine, footRoom, UiTypography.Body);
        _ui.TextBig(b, _ui.ShortenBig(footLine, footRoom, footRung),
                    countX, strip.Y + (strip.Height - footRung) / 2, Bone, footRung);

        // TWO UTILITY ACTIONS, secondary: named for what they do (§65). Each is off when it would do
        // nothing, and says why under the pointer (§29) — a button that clicks and changes nothing is the
        // silent failure the brief forbids.
        _ui.Button(b, EquipBestBtn, "EQUIP HIGHEST POWER", hit, false, _anyHigherPower);
        Tip(EquipBestBtn, hit, _anyHigherPower
            ? "Puts on the highest ITEM POWER in each slot, and the weapon that benches the most damage with your build. It does not count set bonuses."
            : "Nothing in the bag beats what you are wearing.");
        _ui.Button(b, UnequipAllBtn, "UNEQUIP ALL", hit, false, wornAll.Count > 0);
        if (wornAll.Count == 0) Tip(UnequipAllBtn, hit, "You are not wearing anything to take off.");
    }

    // ── INVENTORY: a quiet plate; the items emerge, the empties do not (§63). ─────────────────────────────
    private void DrawInventory(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = InventoryPanel;
        // THE THREE COLUMNS ARE ONE SURFACE (BRIEF's Gear Inventory: "coherent secondary panel", "no
        // prototype visual mismatch"). EQUIPPED is deliberately the one ORNATE panel and DETAIL wears
        // the house quiet frame; INVENTORY wore a bare Plate, so the middle of three columns was the
        // only one with no frame at all — a flat rectangle between two framed neighbours, which reads
        // as a panel that was never finished rather than as a deliberate quiet one.
        _ui.PanelQuiet(b, panel);
        var headY = UiKit.CaptionTop(panel);
        _ui.TextBig(b, "INVENTORY", InvX, headY, Slate, UiTypography.Secondary);
        var total = Wearable().Count;
        var list = Filtered(hunter);
        // THE COUNT IS WHAT THE GRID SHOWS — "24 ITEMS" over sixteen icons made the panel look wrong (the
        // eight worn pieces are counted on the doll's own strip) (release polish 2026-09-05, gear-06).
        _ui.TextRightBig(b, $"{list.Count} {(_tab == 0 ? (list.Count == 1 ? "ITEM" : "ITEMS") : Tabs[_tab])}", InvX + InvW, headY, Slate, UiTypography.Secondary);

        for (var i = 0; i < Tabs.Length; i++)
        {
            var r = TabRect(i);
            var on = i == _tab;
            var hot = r.Contains(hit);
            // The four states, each its own thing (§25–§28): SELECTED is the house lit chip (a gold wash
            // under a gold ring, the set chip's own look) and stays so under the pointer; HOVER eases a
            // lift in over the plate; PRESSED drops the word a pixel and dims for as long as the button is
            // held. House plates rather than private fills (release polish 2026-09-05, gear-13).
            var lift = UiMotion.Ease(UiMotion.KeyOf(r), hot ? 1f : 0f);
            var pressed = hot && UiKit.MouseHeld;
            _ui.Plate(b, r);
            if (on) _ui.Fill(b, r, Gold * 0.14f);
            else if (lift > 0f) _ui.Fill(b, r, Color.White * (0.06f * lift));
            if (pressed) _ui.Fill(b, r, Color.Black * 0.18f);
            if (on) Ring(b, r, Gold, 2);
            _ui.TextCenterBig(b, Tabs[i], r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2 + (pressed ? 1 : 0),
                              on ? Gold : Color.Lerp(Slate, Bone, lift), UiTypography.Secondary);
        }

        var cols = InvCols;
        var visibleRows = InvRows;
        var iconInset = UiMetrics.Space(6);
        var lockEdge = UiMetrics.Control(24);
        // THE SCROLL LANE: when a row waits beyond the last visible one, the grid gives the bar its lane
        // inside the content edge rather than the bar riding the frame's rail (gear-20). Mirrored for
        // the next frame's cell arithmetic.
        var rowsAll = (list.Count + cols - 1) / cols;
        s_invLane = rowsAll > visibleRows ? UiMetrics.ScrollbarWidth + UiMetrics.Space(8) : 0;
        for (var vis = 0; vis < cols * visibleRows; vis++)
        {
            var idx = _invScroll * cols + vis;
            var cell = InvCellRect(vis);
            // The house plate for every cell — empties at half strength, so a part-filled grid reads as a
            // grid with room rather than as unfinished squares (gear-15).
            _ui.Plate(b, cell, alpha: idx >= list.Count ? 0.5f : 1f);
            if (idx >= list.Count) continue;
            var item = list[idx];
            var hot = cell.Contains(hit);
            var sel = item.InstanceId == _selectedId;
            if (hot) { _hovered = item; _hoveredAt = cell; }
            // HOVER eases in (§26) and is never the selected look (§28); PRESSED sits the icon two pixels
            // lower and darker while the button is held (§27); a LOCKED cell keeps its dimmed veil and lock
            // and says who can wear it on the hover card (§29).
            var lift = UiMotion.Ease(UiMotion.KeyOf(cell), hot ? 1f : 0f);
            var pressed = hot && UiKit.MouseHeld;
            if (lift > 0f && !sel && !Dragging) _ui.Fill(b, cell, CellHot * lift);
            var drop = pressed ? 2 : 0;
            _forge.DrawItemIcon(b, item, new Rectangle(cell.X + iconInset, cell.Y + iconInset + drop, cell.Width - iconInset * 2, cell.Height - iconInset * 2));
            if (pressed) _ui.Fill(b, Shrink(cell, 5), Color.Black * 0.18f);
            _ui.Fill(b, new Rectangle(cell.X, cell.Y, 5, cell.Height), RarityColor(item.Rarity));   // rarity owns the left edge
            var wearable = CanWearNow(item);
            if (!wearable)
            {
                // The house lock glyph in Primary over the dark veil — not a hand-built padlock in the OTHER
                // class's colour, which scattered five untaught hues across the grid (gear-07). Who can
                // wear it stays on the hover card and in the inspector's CANNOT WEAR line.
                _ui.Fill(b, Shrink(cell, 5), new Color(0x0B, 0x09, 0x08, 0xB4));
                _ui.Icon(b, "ui_slot_locked", new Rectangle(cell.Right - UiMetrics.Space(8) - lockEdge, cell.Bottom - UiMetrics.Space(8) - lockEdge, lockEdge, lockEdge), Bone);
            }
            // BETTER: one green hairline inside the frame — a hint; the inspector makes the case. Judged by the
            // same ranking the inspector's verdict uses: bench damage for a weapon, ITEM POWER for the rest.
            var better = wearable && Gear.SlotFor(item.BaseType) is { } bs && hunter.Worn(bs) is { } wornPiece
                         && (bs == GearSlot.Weapon ? WeaponDps(hunter, item) > WeaponDps(hunter, wornPiece) * 1.005f
                                                    : hunter.PowerContribution(item) > hunter.PowerContribution(wornPiece));
            // THE CELL THE PIECE WAS LIFTED OUT OF GOES DARK — over the icon, not under it, or the veil
            // is a shade behind a picture that is still at full brightness and the item reads as being
            // in two places at once, which is what makes a drag look like a copy.
            if (Dragging && item.InstanceId == _carryId) _ui.Fill(b, Shrink(cell, 2), new Color(0x0B, 0x09, 0x08) * 0.62f);
            if (better) Ring(b, Shrink(cell, 3), new Color(0x6E, 0xC8, 0x7A, 0x9E), 1);
            if (sel) { Ring(b, cell, Gold, 3); Ring(b, Shrink(cell, 3), new Color(0x16, 0x11, 0x10, 0x88), 1); }   // gold = selected
            else if (lift > 0f) Ring(b, cell, Slate * lift, 2);
        }

        // THE SCROLLBAR, in its own lane inside the content edge — drawn only when there is a row beyond the last visible one.
        var rows = rowsAll;
        _ui.ScrollBar(b, new Rectangle(InvX + InvW + UiMetrics.Space(8), InvTop, UiMetrics.ScrollbarWidth, visibleRows * (InvCell + InvGap) - InvGap),
                      _invScroll, visibleRows, rows);

        if (list.Count == 0)
        {
            // ── AN EMPTY GRID'S MESSAGE BELONGS IN THE MIDDLE OF THE EMPTY GRID. ──────────────────
            //
            // It was pinned to the column's left edge, forty pixels under the tabs — the position a
            // FIRST ROW would occupy. So the one moment the panel has no rows, it drew a heading where
            // row one goes and left the other nine tenths of the column blank beneath it, which reads
            // as a loading state rather than as an answer (playtest 2026-09-09: "move NOTHING IN THIS
            // FILTER to the exact centre").
            //
            // Centred on the GRID, not on the panel: the grid is what is empty, and it is the region
            // the tabs above and the footer below already bracket. Measured from the same three rungs
            // the block is drawn in, so it stays centred at every density profile.
            var head = total == 0 ? "NOTHING IN THE BAG" : "NOTHING IN THIS FILTER";
            var why = total == 0 ? "CHESTS DROP GEAR — BOSSES DROP CHESTS, AND THE VAULT OPENS THEM." : "TRY ANOTHER TAB, OR THE ALL TAB.";
            var lines = _ui.WrapBig(why, InvW, UiTypography.Body).ToList();
            var blockH = UiTypography.Pitch(UiTypography.Headline) + lines.Count * UiTypography.Pitch(UiTypography.Body);
            var gridTop = InvTop;
            var gridBottom = InvFooterTop - UiMetrics.Space(12);
            var ey = gridTop + Math.Max(0, (gridBottom - gridTop - blockH) / 2);
            var cx = InvX + InvW / 2;
            _ui.TextCenterBig(b, head, cx, ey, UiInk.Empty, UiTypography.Headline);
            ey += UiTypography.Pitch(UiTypography.Headline);
            foreach (var l in lines) { _ui.TextCenterBig(b, l, cx, ey, Slate, UiTypography.Body); ey += UiTypography.Pitch(UiTypography.Body); }
        }

        // The footer. The four verbs are named in the inspector now, so this says only what the order is.
        var fy = InvFooterTop;
        _ui.TextBig(b, SortedLabel, InvX, fy, Slate, UiTypography.Secondary);
        if (InvFooterStacked) fy += UiTypography.Pitch(UiTypography.Secondary);
        _ui.TextRightBig(b, MoreLabel, InvX + InvW, fy, Slate, UiTypography.Secondary);
    }

    // ── ITEM DETAIL: the inspector (D3). Decision first. ────────────────────────────────────────────────
    private void DrawDetail(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = DetailPanel;
        _ui.PanelQuiet(b, panel);
        var x = DetX; var w = DetW;
        var top = UiKit.TitleTop(panel);
        var y = top;
        var floor = VerbText(0).Y - UiMetrics.Space(12);
        // When the sheet overflowed last frame, its last row is the MORE BELOW line: the floor is raised
        // by that line so the announcement never sits on a row (gear-10). Stable after one frame.
        if (_detOverflow) floor -= UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);

        // THE SHEET SCROLLS when it is taller than the room (§18: long inspectors) — by whole items from the
        // top, so the first visible line is always a complete one, and the verbs and EQUIP stay anchored under
        // the floor (never below a scroll region). A new selection starts at the top.
        if (_detScrollFor != _selectedId) { _detScroll = DevDetailScroll; _detScrollFor = _selectedId; }
        var scrollable = _detScroll > 0 || _detOverflow;
        if (scrollable) w -= UiMetrics.ScrollbarWidth + UiMetrics.Space(8);
        int index = 0, shown = 0;
        var overflow = false;
        // Reserve h px for the next item: false when it is scrolled off the top or does not fit above the floor
        // (and once one does not fit, nothing after it is drawn either, so the sheet keeps its order).
        bool Take(int h)
        {
            var i = index++;
            if (i < _detScroll) return false;
            if (overflow || y + h > floor) { overflow = true; return false; }
            shown++;
            return true;
        }

        void Head(string s)
        {
            var h = UiTypography.Pitch(UiTypography.Secondary);
            if (!Take(h)) return;
            _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary); y += h;
        }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            var h = UiTypography.Pitch(px);
            var lines = _ui.WrapBig(s, w, px).Take(maxLines).ToList();
            // ONE CLAIM FOR THE WHOLE SENTENCE (2026-09-07). Claimed a line at a time, a wrapped row
            // could straddle the fold — "ON WAVE CLEAR: 38% CHANCE" on the last visible line and the
            // rest a wheel away — which reads as a sentence cut, not as a panel that scrolls. A row
            // that does not fit whole goes below the fold whole, where the scroll finds it.
            if (lines.Count == 0 || !Take(h * lines.Count)) return;
            foreach (var l in lines)
            {
                _ui.TextBig(b, l, x, y, c, px); y += h;
            }
        }
        void Rule()
        {
            var h = UiMetrics.Space(16);
            if (!Take(h)) return;
            _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(6), w, 1), Dim); y += h;
        }
        void Pair(string k, string v, Color vc)
        {
            var h = UiTypography.Pitch(UiTypography.Body);
            // THE VALUE IS RESERVED FIRST (2026-09-08). The label was drawn at the margin and the figure
            // right-aligned at the column's edge with nothing but luck between them: at 150 % a built-in
            // row's "CRITICAL CHANCE  ·  BUILT IN" ran straight through its own "+6.0%". The room is what
            // the figure leaves, and the label is reduced into it by the shared ladder — the caption goes
            // before any part of the stat word does.
            var room = w - _ui.MeasureBig(v, UiTypography.Body) - UiMetrics.Space(16);
            if (_ui.TryFitLabel(k, room, UiTypography.Body, out var fitted))
            {
                if (!Take(h)) return;
                _ui.TextBig(b, fitted, x, y, Slate, UiTypography.Body);
                _ui.TextRightBig(b, v, x + w, y, vc, UiTypography.Body);
                y += h;
                return;
            }
            // NOTHING LEFT TO GIVE UP: the stat word itself does not fit beside the figure, so the row
            // takes a second line rather than cutting either half. "SKILL RATE" reduced to "SKILL…"
            // beside a number is a modifier a player cannot read; this column scrolls, so a row that is
            // two rows tall costs a wheel notch and nothing else.
            if (!Take(h * 2)) return;
            _ui.TextBig(b, _ui.ShortenBig(k, w, UiTypography.Body), x, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, v, x + w, y + h, vc, UiTypography.Body);
            y += h * 2;
        }

        var item = Selected(hunter);
        if (item is null)
        {
            _detOverflow = false;
            Head("ITEM DETAIL");
            _ui.TextBig(b, "NOTHING SELECTED", x, y, UiInk.Empty, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
            Line("Click an item in the bag or on the doll to read it here. EQUIP puts it on; TAKE OFF puts it back.", Slate);
            return;
        }

        var rc = RarityColor(item.Rarity);
        var worn = IsWorn(hunter, item);
        var slot = Gear.SlotFor(item.BaseType);
        var wornPiece = slot is { } s0 ? hunter.Worn(s0) : null;
        var whyNot = ItemClasses.WhyNot(Character, item);

        // CATEGORY, with the class line beside it while both fit the row — under it once they do not (150 %).
        var cat = $"{RarityShort(item.Rarity)} {ItemNaming.SlotWord(item.BaseType)}  ·  {item.Element?.ToString().ToUpperInvariant() ?? "PLAIN"}  ·  LEVEL {item.ItemLevel}";
        var cls = ItemClasses.ClassLine(item);
        var clsInk = item.Class is { } dc && ItemClasses.IsClassLocked(item.BaseType) ? UiKit.ClassColor(dc) : Slate;
        var catH = UiTypography.Pitch(UiTypography.Secondary);
        if (_ui.MeasureBig(cat, UiTypography.Secondary) + UiMetrics.Space(16) + _ui.MeasureBig(cls, UiTypography.Secondary) <= w)
        {
            // The CATEGORY in Secondary, as the inspector grammar says — rarity belongs to the NAME below,
            // and two gold lines in a row made the head as loud as its subject (gear-05).
            if (Take(catH + UiMetrics.Space(2)))
            {
                _ui.TextBig(b, cat, x, y, Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, cls, x + w, y, clsInk, UiTypography.Secondary);
                y += catH + UiMetrics.Space(2);
            }
        }
        else
        {
            // When the two do not share a row, the class line keeps the LEFT under the category, so the
            // head stays one block rather than a right-aligned orphan (gear-21, seen only at 125 %).
            if (Take(catH)) { _ui.TextBig(b, _ui.ShortenBig(cat, w, UiTypography.Secondary), x, y, Slate, UiTypography.Secondary); y += catH; }
            if (Take(catH + UiMetrics.Space(2))) { _ui.TextBig(b, cls, x, y, clsInk, UiTypography.Secondary); y += catH + UiMetrics.Space(2); }
        }

        // NAME with the icon, then THE DECISION FIRST: the verdict, then what it replaces.
        string verdict; Color vcol;
        if (worn) { verdict = "EQUIPPED"; vcol = Gold; }
        else if (whyNot is not null) { verdict = $"{Character.Name.ToUpperInvariant()} CANNOT WEAR THIS"; vcol = Ember; }
        else if (item.BaseType == ItemBaseType.Weapon)
        {
            var now = WeaponDps(hunter, wornPiece);
            var with = WeaponDps(hunter, item);
            var pct = now > 0f ? with / now - 1f : 0f;
            verdict = wornPiece is null ? "SLOT EMPTY — WEARING IT ADDS ITS DAMAGE" : $"{pct:+0%;-0%;0%} DAMAGE PER SECOND";
            vcol = wornPiece is null ? Green : pct > 0.005f ? Green : pct < -0.005f ? Ember : Slate;
        }
        else
        {
            var delta = hunter.PowerContribution(item) - hunter.PowerContribution(wornPiece);
            verdict = wornPiece is null ? $"SLOT EMPTY — +{hunter.PowerContribution(item):N0} POWER" : $"{(delta >= 0 ? "+" : "")}{delta:N0} POWER";
            vcol = wornPiece is null || delta > 0 ? Green : delta < 0 ? Ember : Slate;
        }
        var icoSize = UiMetrics.Control(64);
        var nameTop = UiMetrics.Space(4);
        var blockH = Math.Max(icoSize, nameTop + UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Body)) + UiMetrics.Space(4);
        if (Take(blockH))
        {
            var ico = new Rectangle(x, y, icoSize, icoSize);
            _forge.DrawItemIcon(b, item, ico);
            var nx = ico.Right + UiMetrics.Space(12);
            _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), x + w - nx, UiTypography.Headline), nx, y + nameTop, rc, UiTypography.Headline);
            _ui.TextBig(b, _ui.ShortenBig(verdict, x + w - nx, UiTypography.Body), nx, y + nameTop + UiTypography.Pitch(UiTypography.Headline), vcol, UiTypography.Body);
            y += blockH;
        }
        if (!worn && wornPiece is not null && whyNot is null)
            Line($"REPLACES {ItemNaming.FullName(wornPiece).ToUpperInvariant()}", Slate, UiTypography.Secondary, 1);
        if (whyNot is not null) Line(whyNot, Ember, UiTypography.Secondary, 2);
        Rule();

        // WHAT IT DOES: power, affixes, the prefix's numbers, the enchant and whether it works with the build.
        Head("WHAT IT DOES");
        // THE ROWS ARE CORE'S (ItemPresentation, 2026-09-07): every fact about the piece — its power,
        // its built-in, its rolls, its gems, both sides of its prefix's trade, its enchant's sentence
        // and need — arrives with its words decided, and this panel only lays them out. It used to
        // compose the words itself (and once printed "GLOVES  +2%" for a built-in, and "NO STATS" over a
        // helm carrying +2 DEFENCE); a screen that composes nothing cannot compose that again.
        // The verdict and the set rows are drawn elsewhere on this panel (the head, the ladder).
        // THE BUILD'S FACTS for the enchant's verdict — the weave, the sockets and the sworn vows — from
        // the same Build the DPS bench composes. Passing the weave alone let a keystone combo read
        // WORKS WITH YOUR BUILD on a build with no such keystone (2026-09-07); a wrongly gilded item is
        // a player who equips it and wonders why nothing changed. Composed only when the item has a
        // need to judge.
        var judged = Enchantments.Of(item)?.Needs is not null && Loadout is not null && Mastery is not null;
        var triggers = judged ? Loadout!.ToBuild(Mastery!, Character, SkillLevels, DiscoveredKeystones, KnownVows).Triggers(hunter).ToList() : null;
        var rows = ItemPresentation.Rows(item, hunter, Character, Loadout?.EquippedDefs(), triggers, judged ? Loadout!.SwornVows.Count : null);
        var prefixSides = rows.Count(r => r.Kind == ItemRowKind.PrefixSide);
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case ItemRowKind.Power: Pair(row.Label, row.Value, Bone); break;
                case ItemRowKind.BuiltIn: Pair($"{row.Label}  ·  {ItemPresentation.BuiltInCaption}", row.Value, Gold); break;
                case ItemRowKind.Affix: Pair(row.Label, row.Value, Green); break;
                case ItemRowKind.GemCount: Pair(row.Label, row.Value, Slate); break;
                case ItemRowKind.Gem: Pair(row.Label, row.Line, Green); break;
                case ItemRowKind.Family: Line(row.Label, Slate, UiTypography.Secondary, 2); break;
                case ItemRowKind.Prefix:
                    // THE TRADE, BOTH SIDES, one stat to a row — never the whole trade right-aligned into a
                    // value column it could not fit at 150 %. The cost rows wear the cost colour.
                    Line($"PREFIX · {row.Label}", Gold, UiTypography.Body, 1);
                    if (prefixSides == 0) Line(row.Note, Bone, UiTypography.Body, 2);
                    break;
                case ItemRowKind.PrefixSide: Pair(row.Label, row.Value, row.IsDrawback ? Ember : Gold); break;
                case ItemRowKind.Enchant:
                    // One row — the name and its sentence, wrapped to up to four lines — rather than a head
                    // row and a body row, so the set ladder under it keeps a rung at 150 % (gear-10). Four
                    // lines' room, because the sentence says what its number is OF and a cut sentence would
                    // put the number back beside nothing; the column scrolls, so a rung the sentence pushes
                    // down is a wheel away, not gone.
                    Line($"ENCHANT · {row.Label} — {row.Note}", Bone, UiTypography.Body, 4);
                    break;
                case ItemRowKind.EnchantNeed: Line(row.Label, row.Tone == ItemRowTone.Bonus ? Green : Ember, UiTypography.Secondary, 2); break;
            }
        }

        // THE SET, as a ladder: every rung, reached or not, in three encodings — a mark, a colour, a word (§64).
        if (item.Element is { } el)
        {
            Rule();
            var tiers = ElementSets.TiersOf(el);
            var wornOf = ElementSets.WornCount(hunter, el);
            // What wearing THIS would make the count — the same element already in the slot changes nothing.
            var would = worn ? wornOf : wornOf + (wornPiece?.Element == el ? 0 : 1);
            Head($"{ElementSets.Name(el).ToUpperInvariant()}  ·  {ElementSets.Progress(wornOf).ToUpperInvariant()}");
            var rowH = UiTypography.Pitch(UiTypography.Body);
            var mark = UiMetrics.Control(12);
            // The capstone's emblem is a little larger than a rung's disc, and still clears the number column.
            var emblem = mark + UiMetrics.Space(6);
            var sc = SourceColor.GetValueOrDefault(el, Slate);
            // The number column and the sentence column hang from the Body rung: one and two rungs in.
            var numX = x + UiTypography.Body;
            // Where the mark column has to stop — the reveal's halo is clamped to it (see Halo).
            var markRight = numX - UiMetrics.Space(3);
            var textX = x + UiTypography.Body * 2;
            foreach (var t in tiers)
            {
                var reached = wornOf >= t.Pieces;
                var reaches = !reached && !worn && whyNot is null && would >= t.Pieces;
                // JUST REACHED (§51): the rung's one-time Source-accent reveal — the mark and its words
                // borrow the Source colour and a halo steps out from the mark as the pulse fades.
                var reveal = reached ? UiMotion.Smooth(_feedback.RungPulse(el, t.Pieces)) : 0f;
                var ink = reached ? Bone : reaches ? Gold : Slate;
                if (reveal > 0f) ink = Color.Lerp(ink, sc, reveal);
                // THE CAPSTONE IS NAMED, and the name is what the rung is remembered by. A player talks
                // about running MOMENTUM, not about their fifth Body piece — so the word is on the row,
                // ahead of the sentence, and it keeps its emphasis whether or not the rung is reached:
                // an unreached capstone is the thing the ladder is FOR.
                var cap = t.Pieces == ElementSets.Rungs[^1] ? ElementSets.CapstoneName(el) : null;
                var capW = cap is null ? 0 : _ui.MeasureBig(cap + "  ", UiTypography.Body);
                // THE RIGHT LABEL'S ROOM IS MEASURED, not guessed at 64px. A fixed reserve was wrong in
                // both directions: it truncated a rung's sentence when there was no label to make room
                // for, and it was far too small for "WEARING THIS REACHES IT" when there was.
                var right = reached ? "ON" : reaches ? "WEARING THIS REACHES IT" : "";
                var rightW = right.Length == 0 ? 0 : _ui.MeasureBig(right, UiTypography.Secondary) + UiMetrics.Space(12);

                // THE RULE IS WRAPPED, NOT CUT. This column is 320 px wide and MACHINE's third rung is
                // sixty-three characters, so a single line ended "…worth 12% of your maximum h…" — a
                // sentence the player cannot finish is a rung they cannot evaluate, which is the whole
                // job of this list. Continuation lines hang under the sentence, past the number and the
                // capstone name, so the column still reads as one row per rung.
                // ONE CLAIM FOR THE WHOLE RUNG (2026-09-07), as Line makes for a sentence: claimed a line
                // at a time, a wrapped rung could leave its first line above the fold and its rule below it.
                var first = true;
                var rungLines = _ui.WrapBig(t.Line, x + w - rightW - textX - capW, UiTypography.Body);
                if (!Take(rowH * rungLines.Count)) continue;
                foreach (var wrapped in rungLines)
                {
                    var lx = first ? textX + capW : textX;
                    {
                        if (first)
                        {
                            // THE RUNG'S MARK IS A SHAPE — a filled disc reached, a hollow one not (§51's
                            // ● / ○, drawn, because the font has neither glyph) — and the CAPSTONE'S is
                            // its emblem in the set's Source colour, full when reached, dimmed until then
                            // (§23: the capstone is the important rung; §24: Source colour is identity).
                            var m = new Rectangle(x + 2, y + (UiTypography.Body - mark) / 2 + 1, mark, mark);
                            if (cap is not null && _ui.Assets.Get(CapstoneIconKey[el]) is { } emblemArt)
                            {
                                var e = new Rectangle(Math.Max(x, m.Center.X - emblem / 2), m.Center.Y - emblem / 2, emblem, emblem);
                                if (reveal > 0f) DiscRing(b, Shrink(e, -Halo(reveal, markRight - e.Right)), sc * reveal, 2f);
                                // An unreached capstone is the thing the ladder is FOR, so it is dim, not absent.
                                b.Draw(emblemArt, e, reached ? Color.Lerp(sc, Color.White, reveal * 0.5f) : sc * 0.55f);
                            }
                            else
                            {
                                if (reveal > 0f) DiscRing(b, Shrink(m, -Halo(reveal, markRight - m.Right)), sc * reveal, 2f);
                                if (reached) Disc(b, m, Color.Lerp(Bone, sc, reveal)); else DiscRing(b, m, reaches ? Gold : Slate, 1.5f);
                            }
                            _ui.TextBig(b, $"{t.Pieces}", numX, y, ink, UiTypography.Body);
                            if (cap is not null) _ui.TextBig(b, cap, textX, y, reached ? Color.Lerp(Gold, sc, reveal) : ink, UiTypography.Body);
                            if (right.Length > 0) _ui.TextRightBig(b, right, x + w, y, reached ? Green : Gold, UiTypography.Secondary);
                        }
                        _ui.TextBig(b, wrapped, lx, y, ink, UiTypography.Body);
                        y += rowH;
                    }
                    first = false;
                }
            }
        }

        _detOverflow = overflow;
        // A cut sheet SAYS it is cut: the one cue was a 10 px bar (gear-10).
        if (overflow)
            _ui.TextBig(b, "MORE BELOW — THE MOUSE WHEEL SCROLLS", x, floor + UiMetrics.Space(4), Slate, UiTypography.Secondary);
        if (scrollable || overflow)
        {
            // The track starts under the frame's corner band, not in it (gear-09).
            var trackTop = UiKit.PanelInner(panel).Y + UiMetrics.Space(8);
            _ui.ScrollBar(b, new Rectangle(x + w + UiMetrics.Space(8), trackTop, UiMetrics.ScrollbarWidth, Math.Max(1, floor - trackTop)), _detScroll, shown, index);
        }

        // THE VERBS as three Secondary buttons — bare grey words above the primary read as column heads,
        // not as the doors to the FORGE they are (gear-08) — then the ONE primary action.
        for (var v = 0; v < Verbs.Length; v++)
        {
            var r = VerbText(v);
            _ui.Button(b, r, Verbs[v].Label, hit, false, true, ButtonStyle.Secondary);
            Tip(r, hit, Verbs[v].Action switch
            {
                ItemAction.Upgrade => "Take it to the FORGE to raise its level.",
                ItemAction.Reforge => "Take it to the FORGE to re-roll its enchant.",
                _ => "Take it to the FORGE to break it into materials.",
            });
        }
        var label = worn ? "TAKE OFF" : whyNot is null ? "EQUIP" : "CANNOT WEAR";
        _ui.Button(b, EquipBtn, label, hit, false, worn || whyNot is null, !worn && whyNot is null ? ButtonStyle.Primary : ButtonStyle.Secondary);
        if (!worn && whyNot is not null) Tip(EquipBtn, hit, whyNot);   // off, and says why (§29)
    }

    /// <summary>
    /// How far a reveal's halo has stepped out from its mark: tight at the peak, a breath by the end, and
    /// never past <paramref name="room"/> — the mark column ends where the number column begins, and a halo
    /// that crossed it would print on the rung's own number. No movement at all under Reduced Motion.
    /// </summary>
    private static int Halo(float reveal, int room)
        => Math.Clamp(UiMotion.Reduced ? 2 : 2 + (int)MathF.Round(UiMetrics.Space(4) * (1f - reveal)), 1, Math.Max(1, room));

    /// <summary>Remember a row's explanation, and THE ROW — the tip hangs off it, never off the cursor.</summary>
    private void Tip(Rectangle r, Point hit, string text) { if (r.Contains(hit)) { _tip = text; _tipAt = r; } }

    private void DrawDebug(SpriteBatch b, Hunter hunter)
    {
        foreach (var r in new[] { EquippedPanel, InventoryPanel, DetailPanel }) Ring(b, r, Ember, 2);
        foreach (var (_, _, col, row) in SlotLayout) Ring(b, SlotRect(col, row), Green, 2);
        for (var v = 0; v < InvCols * InvRows; v++) Ring(b, InvCellRect(v), new Color(0x40, 0xE0, 0xE0), 2);
        _ui.TextBig(b, $"tab {Tabs[_tab]}  sel {_selectedId ?? "none"}  nav GEAR  scroll {_invScroll}", 40, 108, Gold, UiTypography.Secondary);
    }

    private static Rectangle Shrink(Rectangle r, int by)
        => new(r.X + by, r.Y + by, Math.Max(1, r.Width - by * 2), Math.Max(1, r.Height - by * 2));

    private void Ring(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    // ── DISCS from horizontal spans of the one pixel texture: no circle art, no glyph the font lacks, and
    // they batch with everything else. A dozen spans for a 12 px mark. Shared shapes belong in UiKit,
    // which this pass may not edit — the host pass lifts these. ──

    /// <summary>A filled disc inscribed in <paramref name="box"/>.</summary>
    private void Disc(SpriteBatch b, Rectangle box, Color c)
    {
        var r = box.Width / 2f;
        var cx = box.X + r;
        for (var row = 0; row < box.Height; row++)
        {
            var dy = row + 0.5f - r;
            var half = MathF.Sqrt(MathF.Max(0f, r * r - dy * dy));
            var x0 = (int)MathF.Round(cx - half);
            var x1 = (int)MathF.Round(cx + half);
            if (x1 > x0) _ui.Fill(b, new Rectangle(x0, box.Y + row, x1 - x0, 1), c);
        }
    }

    /// <summary>A hollow disc — a circular ring of <paramref name="thickness"/> px inscribed in <paramref name="box"/>.</summary>
    private void DiscRing(SpriteBatch b, Rectangle box, Color c, float thickness)
    {
        var r = box.Width / 2f;
        var ri = MathF.Max(0f, r - thickness);
        var cx = box.X + r;
        for (var row = 0; row < box.Height; row++)
        {
            var dy = row + 0.5f - r;
            var outer = MathF.Sqrt(MathF.Max(0f, r * r - dy * dy));
            var inner = dy * dy < ri * ri ? MathF.Sqrt(ri * ri - dy * dy) : 0f;
            var x0 = (int)MathF.Round(cx - outer);
            var x1 = (int)MathF.Round(cx + outer);
            if (x1 <= x0) continue;
            if (inner <= 0f) { _ui.Fill(b, new Rectangle(x0, box.Y + row, x1 - x0, 1), c); continue; }
            var i0 = (int)MathF.Round(cx - inner);
            var i1 = (int)MathF.Round(cx + inner);
            if (i0 > x0) _ui.Fill(b, new Rectangle(x0, box.Y + row, i0 - x0, 1), c);
            if (x1 > i1) _ui.Fill(b, new Rectangle(i1, box.Y + row, x1 - i1, 1), c);
        }
    }

    private static string RarityShort(Rarity r) => r switch
    {
        Rarity.Common => "COMMON", Rarity.Uncommon => "UNCOMMON", Rarity.Rare => "RARE",
        Rarity.Epic => "EPIC", _ => "LEGENDARY",
    };
    private static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Common => Bone, Rarity.Uncommon => UiInk.Good, Rarity.Rare => new Color(0x4A, 0x90, 0xD9),
        Rarity.Epic => new Color(0x8B, 0x3F, 0x82), _ => Gold,
    };
}

/// <summary>
/// THE GEAR SCREEN'S FEEDBACK as a state machine with no drawing in it: what changed on the hunter since the
/// last look, which pulses that starts, what the GEAR POWER number should read right now, and which set has
/// just completed for the first time.
/// </summary>
/// <remarks>
/// <para>
/// It DIFFS rather than listens. Gear goes on by four routes — the EQUIP button, EQUIP HIGHEST POWER, the
/// item menu (which the host equips through, a frame later), and TAKE OFF — and a pulse wired to one of
/// them is a pulse the other three forget. <see cref="Observe"/> compares the hunter's worn pieces, power
/// and per-Source counts with the previous frame's and fires once per change, whoever made it. It is called
/// from the screen's Update, never from Draw, and a pulse is keyed to its event (the slot, the number, the
/// rung), so a second Observe with nothing changed re-arms nothing.
/// </para>
/// <para>
/// Every duration is <see cref="UiMotion"/>'s and every value is read back through it, so Reduced Motion
/// collapses the number's run to the true value and leaves the one-shot fades — the brief keeps simple
/// fades — with the same end state. It holds no UiKit, so the Game tests can drive it: an equip, then
/// <see cref="UiMotion.Tick"/> at t = 0, mid and end, and the displayed number at each.
/// </para>
/// <para>
/// The number's run is a <see cref="UiMotion.Flash"/> read back through <see cref="UiMotion.Pulse"/>, not
/// <see cref="UiMotion.Ease"/>. Ease is shaped for a 0 → 1 target: it seeds an unseen key at
/// <c>1 − target</c> and steps by <c>dt / seconds</c>, so handed a target of 397 it would start at −396 and
/// take a minute to arrive. A one-shot with the old value interpolated against it is the same curve, the
/// same duration, and honest about being one event.
/// </para>
/// <para>
/// The completed-set record lives here because the screen cannot reach the save: the host copies
/// <see cref="CompletedSets"/> into <c>SaveGame.CompletedSets</c> and seeds it back through
/// <see cref="RestoreCompletedSets"/> on load. Names are <see cref="Source"/> names ("Machine"), the form
/// the save's own test writes.
/// </para>
/// </remarks>
public sealed class GearFeedback
{
    private static readonly GearSlot[] Slots = Enum.GetValues<GearSlot>();
    private static readonly Source[] Sources = Enum.GetValues<Source>();

    /// <summary>The pulse key of a slot that just received a piece.</summary>
    public static int SlotKey(GearSlot slot) => HashCode.Combine("gear.slot", (int)slot);

    /// <summary>The pulse key of the GEAR POWER number.</summary>
    public static readonly int PowerKey = HashCode.Combine("gear.power", 0);

    /// <summary>The pulse key of one set rung — the reveal it plays the moment it is reached.</summary>
    public static int RungKey(Source element, int pieces) => HashCode.Combine("gear.rung", (int)element, pieces);

    private readonly string?[] _wornIds = new string?[Slots.Length];
    private readonly int[] _counts = new int[Sources.Length];
    private readonly int[] _nextCounts = new int[Sources.Length];
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private bool _primed;
    private int _power, _powerFrom;
    private string? _completedSet, _notice, _cue;

    /// <summary>
    /// Look at the hunter and fire on what changed since the last look. <paramref name="quiet"/> takes the
    /// snapshot without firing — the first look, a change of character, a return after time away. Returns
    /// whether anything moved at all, so a caller can hang expensive work off a change rather than off a frame.
    /// </summary>
    public bool Observe(Hunter hunter, bool quiet = false)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        var silent = quiet || !_primed;
        var moved = false;
        Array.Clear(_nextCounts);
        for (var i = 0; i < Slots.Length; i++)
        {
            var worn = hunter.Worn(Slots[i]);
            var id = worn?.InstanceId;
            if (worn?.Element is { } el) _nextCounts[(int)el]++;
            if (!string.Equals(id, _wornIds[i], StringComparison.Ordinal)) moved = true;
            // A piece ARRIVING pulses; a slot emptying does not — taking off is not a reward (§30).
            if (!silent && id is not null && !string.Equals(id, _wornIds[i], StringComparison.Ordinal))
                UiMotion.Flash(SlotKey(Slots[i]), UiMotion.Transition);
            _wornIds[i] = id;
        }

        var power = hunter.PowerRating;
        if (power != _power) moved = true;
        if (!silent && power != _power)
        {
            // From wherever the number IS — a second change mid-run continues from the shown value
            // rather than jumping back to the old one.
            _powerFrom = DisplayedPower;
            _power = power;
            UiMotion.Flash(PowerKey, UiMotion.Transition);
        }
        else _power = power;

        var last = ElementSets.Rungs[^1];
        for (var s = 0; s < Sources.Length; s++)
        {
            var before = _counts[s];
            var after = _nextCounts[s];
            if (!silent && after > before)
            {
                foreach (var rung in ElementSets.Rungs)
                    if (before < rung && after >= rung)
                        UiMotion.Flash(RungKey(Sources[s], rung), UiMotion.Reward);
                // THE FIRST FIVE-PIECE COMPLETION, once per set, ever (§52): recorded, and one notice for the host.
                if (before < last && after >= last && _completed.Add(Sources[s].ToString()))
                {
                    _completedSet = Sources[s].ToString();
                    _notice = $"{ElementSets.Name(Sources[s])} COMPLETE\n{ElementSets.CapstoneName(Sources[s])} ACTIVE";
                }
            }
            _counts[s] = after;
        }
        _primed = true;
        return moved;
    }

    /// <summary>
    /// What the GEAR POWER readout shows this frame: the true value, or — for one Transition after it
    /// changed — a number on its way there from the old one. The true value at once under Reduced Motion.
    /// </summary>
    public int DisplayedPower
    {
        get
        {
            if (UiMotion.Reduced) return _power;
            var p = UiMotion.Pulse(PowerKey);
            if (p <= 0f) return _power;
            return (int)MathF.Round(_power + (_powerFrom - _power) * UiMotion.Smooth(p));
        }
    }

    /// <summary>How much of the number's flash is left, 1 → 0 — 0 at rest.</summary>
    public float PowerEmphasis => UiMotion.Pulse(PowerKey);

    /// <summary>How much of a slot's just-equipped pulse is left, 1 → 0 — 0 at rest.</summary>
    public float SlotPulse(GearSlot slot) => UiMotion.Pulse(SlotKey(slot));

    /// <summary>How much of a rung's reveal is left, 1 → 0 — 0 at rest.</summary>
    public float RungPulse(Source element, int pieces) => UiMotion.Pulse(RungKey(element, pieces));

    /// <summary>Every set completed so far, by <see cref="Source"/> name.</summary>
    public IReadOnlyCollection<string> CompletedSets => _completed;

    /// <summary>Seed the record from a save.</summary>
    public void RestoreCompletedSets(IEnumerable<string> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        foreach (var s in sets) if (!string.IsNullOrEmpty(s)) _completed.Add(s);
    }

    /// <summary>The set that just completed for the first time, cleared by reading.</summary>
    public string? ConsumeCompletedSet() { var c = _completedSet; _completedSet = null; return c; }

    /// <summary>The completion notice, two lines, cleared by reading.</summary>
    public string? ConsumeNotice() { var n = _notice; _notice = null; return n; }

    /// <summary>Raise a sound cue at its semantic moment; the host reads it once through <see cref="ConsumeCue"/>.</summary>
    public void Cue(string cue) => _cue = cue;

    /// <summary>The pending cue, cleared by reading.</summary>
    public string? ConsumeCue() { var c = _cue; _cue = null; return c; }
}
