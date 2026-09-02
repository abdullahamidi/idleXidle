using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>
/// BUILD: what your hunter carries into the fight — the skills in their slots, each skill's own variation
/// and reinforcements, the keystones, and the Vow bound to a slot — edited as master-detail.
/// </summary>
/// <remarks>
/// <para>
/// UX V2 P1.4 (brief §66–§71, D3, D5, D9). Three columns. YOUR LOADOUT on the left is the one ornate surface:
/// the slots grouped ACTIVE / PASSIVE, each row saying skill · style · level · variation · Source · vow. The
/// SKILLS column is a quiet plate: the twelve skills grouped by style, and under them the selected skill's own
/// tree drawn as the fork it is — a variation OWNS the skill's Source, so the player never wonders whether a
/// Source is equipped separately (§70). The INSPECTOR on the right speaks the shared grammar — CATEGORY ·
/// NAME · IDENTITY · WHAT IT DOES · YOU NEED FIRST · WHAT IT COSTS / CURRENT STATE · a refusal line · ONE
/// primary action — and carries the Vow as a VALIDATOR: the demand, what your build actually is, and whether
/// the vow holds (§71). Hover highlights and tips; click selects; the primary button commits (D5).
/// </para>
/// <para>
/// Everything shown is computed by Core today: <see cref="Vows.IsActive"/> against
/// <see cref="SoloBattle.DescribeBuild"/>, <see cref="DamageBench"/> for the bench figure, <see cref="SkillProgress"/>
/// for levels, <see cref="StyleAffinity"/> for the style factor, <see cref="SourceMatchup"/> for the region line.
/// Nothing here is a guess.
/// </para>
/// </remarks>
public sealed class LoadoutScreen
{
    /// <summary>The build's STYLE specialisation, taken on the mastery tree. Host-fed. Null until chosen.</summary>
    public Style? ChosenStyle { get; set; }

    /// <summary>Host-fed mastery walk, for the build share code.</summary>
    public IReadOnlyCollection<string> MasteryTaken { get; set; } = Array.Empty<string>();

    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Met = UiInk.Good;
    private static readonly Color Quiet = UiInk.Plate;

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Body] = new Color(0xD6, 0x48, 0x5C), [Source.Mind] = new Color(0x74, 0xC6, 0xE8),
        [Source.Nature] = new Color(0x48, 0xB8, 0x88), [Source.Machine] = new Color(0xE0, 0x8A, 0x3A),
        [Source.Shadow] = new Color(0x8A, 0x6E, 0xE0), [Source.Spirit] = new Color(0xC8, 0xC0, 0xE8),
    };

    private readonly UiKit _ui;
    private int _slot;
    private string _msg = "";
    private int _copyToastFrames;
    private string _copyToast = "";
    private int _keystoneScroll;
    private bool _vowListOpen;
    /// <summary>
    /// Counts down from 2 when the vow list opens; on the draw it reaches 0 the inspector's body scrolls just enough
    /// to show the whole list. Two, not one: the first draw after opening is the one that discovers the body now
    /// overflows and reserves the scrollbar's lane, and the list only settles once that narrower column has wrapped.
    /// </summary>
    private int _vowListReveal;

    // ── WHAT IS SELECTED: the thing the inspector is about and the primary button acts on. ──────────────
    private enum Pick { Slot, Library, Variation, Reinforcement, Keystone }
    private Pick _pick = Pick.Slot;
    private string _pickSkillId = "";      // Library
    private int _pickIndex;                // Variation / Reinforcement
    private string _pickKeystoneId = "";   // Keystone

    // ── THE BENCH. DamageBench measures a build against a reference dummy; cached by what it measured. ──
    private (string Id, int Slot, int Rev)? _previewKey;
    private float _previewDps;
    private float _currentDps;
    private int _currentRev = -1;
    private int _buildRev;

    // ── DRAGGING a slot row reorders — the order IS cast priority. ─────────────────────────────────────
    private enum Carry { None, Slot }
    private Carry _carrying;
    private int _carrySlot = -1;
    private Point _carryFrom;
    private Point _carryAt;
    private bool _carryMoved;
    private bool _wasHeld;
    private const int DragSlop = 7;

    private const float SetFlashSeconds = 0.34f;
    private const float BindFlashSeconds = 0.85f;
    private const float ChainClipSeconds = 0.55f;
    private readonly Dictionary<int, float> _slotFlash = new();
    private readonly Dictionary<int, float> _bindFlash = new();
    private bool _devHoldChain;
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _lastTick;

    /// <summary>Cues, host-fed like every other screen's.</summary>
    public SoundBank? Sound { get; set; }

    public LoadoutScreen(UiKit ui) => _ui = ui;

    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MasteryTree Mastery { get; set; } = new();
    public MemoryDustTree Tree { get; set; } = new();
    /// <summary>What each skill has earned by being used, and where the player spends it.</summary>
    public SkillProgress SkillLevels { get; set; } = new();
    public Hunter? Hunter { get; set; }
    public Character? Character { get; set; }
    /// <summary>Where the hunter is hunting, so a Source can be judged against real creatures.</summary>
    public string RegionId { get; set; } = "";
    public string RegionName { get; set; } = "";

    /// <summary>Set when the loadout changed, so the host can save.</summary>
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    /// <summary>DEV: pose a slot and, with a vow id, the vow list open, for the capture fixture.</summary>
    public void DevPose(int slot, string? vowId, float chainAt = 0f)
    {
        _slot = slot;
        _pick = Pick.Slot;
        _vowListOpen = vowId is not null;
        _vowListReveal = _vowListOpen ? 2 : 0;
        if (chainAt > 0f)
        {
            _bindFlash[slot] = BindFlashSeconds * Math.Clamp(chainAt, 0f, 1f);
            _devHoldChain = true;
        }
    }

    /// <summary>DEV: kept for the fixtures that call it — the skill tree is always on screen now.</summary>
    public void DevOpenSkillTree() { }

    // ── LAYOUT. Three columns to the page. The columns' WIDTHS are page shares and do not move with the
    // profile; everything inside a column is a density size read from UiMetrics (brief §7–§11) and laid out
    // from the height that is there — a column that no longer fits scrolls (§9, §18), it never overprints. ──
    private const int Top = 150;          // under the hint slot's band (y 86–134 stays free of controls)
    private const int BottomMargin = 60;
    private const int LoadoutPanelW = 520, InspectorPanelW = 496, ColumnGap = 20;
    private static Rectangle LoadoutPanel => new(38, Top, LoadoutPanelW, UiKit.PageBottom(BottomMargin) - Top);
    private static Rectangle InspectorPanel => new(UiKit.PageRight(40) - InspectorPanelW, Top, InspectorPanelW, UiKit.PageBottom(BottomMargin) - Top);
    private static Rectangle SkillsPanel => new(LoadoutPanel.Right + ColumnGap, Top, InspectorPanel.X - ColumnGap - (LoadoutPanel.Right + ColumnGap), UiKit.PageBottom(BottomMargin) - Top);

    /// <summary>The lane a scrollbar takes at the right of a column's content, when the column scrolls.</summary>
    private static int ScrollLane => UiMetrics.ScrollbarWidth + UiMetrics.Gap;
    /// <summary>What one wheel notch moves a scrolling column by.</summary>
    private static int ScrollStep => UiMetrics.RowHeight;
    /// <summary>The part of <paramref name="r"/> that is actually on screen inside a clipped region — the rectangle it is hit-tested by (LAW 5).</summary>
    private static Rectangle In(Rectangle r, Rectangle region) => Rectangle.Intersect(r, region);

    // ── YOUR LOADOUT: the slot rows and the keystone chips are one list above the bench. ──────────────
    private static int LoadX => UiKit.ContentLeft(LoadoutPanel);
    private static int LoadW => UiKit.ContentRight(LoadoutPanel) - LoadX;
    /// <summary>A slot row: three lines of type beside a glyph box — a control, so it grows at the full rate.</summary>
    private static int SlotH => UiMetrics.Control(90);
    private static int SlotGap => UiMetrics.Space(8);
    private static int SpineW => UiMetrics.Control(22);
    private static int GlyphBox => UiMetrics.Control(56);
    private static int ChipH => UiMetrics.Control(44);
    private static int ChipGap => UiMetrics.Space(4);
    private static int GroupCaptionH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int GroupGap => UiMetrics.Space(10);
    private static int KeystoneHeadH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    private int KeystoneRows => Loadout.SkillCapacity >= 5 ? 2 : 3;

    /// <summary>The bench's block: a caption line, the headline figure, and its air.</summary>
    private static int BenchH => UiTypography.Pitch(UiTypography.Secondary) + UiTypography.PrimaryValue + UiMetrics.Space(30);
    private static Rectangle BenchBlock => new(LoadX, LoadoutPanel.Bottom - UiKit.PanelCorner - UiMetrics.Space(10) - BenchH, LoadW, BenchH);
    /// <summary>Where the list lives: from the panel's first body row down to the bench. Rows are clipped to it when it scrolls.</summary>
    private static Rectangle ListRegion
    {
        get
        {
            var top = LoadoutPanel.Y + UiTypography.PanelBodyTopBare;
            return new(LoadoutPanel.X + UiKit.PanelCorner / 2, top, LoadoutPanel.Width - UiKit.PanelCorner, BenchBlock.Y - UiMetrics.Space(12) - top);
        }
    }

    /// <summary>The slot's glyph box, beside the numbered spine and centred in its row.</summary>
    private static Rectangle GlyphRect(Rectangle row) => new(row.X + SpineW + UiMetrics.Space(10), row.Y + (row.Height - GlyphBox) / 2, GlyphBox, GlyphBox);
    private static int RowTextX(Rectangle row) => GlyphRect(row).Right + UiMetrics.Space(12);

    /// <summary>The rows this frame: slot index (or -1 for an empty), where it is drawn, and whether it heads its group (and carries the caption).</summary>
    private readonly List<(int Slot, Rectangle Rect, bool Passive, bool First)> _rows = new();
    /// <summary>The keystone chips this frame: an index into the learned list (past its end: an empty socket), and where it is drawn.</summary>
    private readonly List<(int Index, Rectangle Rect)> _chips = new();
    private Rectangle _keystoneHead;
    private int _rowsEnd;
    private int _loadScroll;
    private int _loadOverflow;
    /// <summary>FIT mode: the keystones page <see cref="KeystoneRows"/> at a time under the wheel, as they always did. When the column scrolls instead, the pager is retired and every learned keystone is a chip — a wheel that moved two different lists depending on where the cursor sat would be a trap.</summary>
    private bool _keystonePaged = true;

    /// <summary>
    /// Lay the list out: actives first, then passives, each group to its capacity, then the keystones — and if
    /// that is taller than the room above the bench, scroll it rather than let a chip print over the bench.
    /// </summary>
    private void LayoutRows()
    {
        var skills = Loadout.Skills;
        var cap = Math.Max(1, Loadout.SkillCapacity);
        var kinds = BuildComposer.SlotKinds(
            skills.Select(k => new BuildComposer.SkillPick(k.Source, k.VowId, k.Passive, k.SkillId)).ToList(), cap);
        var actives = new List<int>();
        var passives = new List<int>();
        for (var i = 0; i < skills.Count; i++) (i < kinds.Count && kinds[i] ? passives : actives).Add(i);
        var activeCap = Math.Max(Build.ActiveSlotsFor(cap), actives.Count);
        var passiveCap = Math.Max(Build.PassiveSlotsFor(cap), passives.Count);
        var learned = DustEffects.LearnedKeystones(Tree).Count;
        var region = ListRegion;

        // Measure at rest with the keystones paged; if that does not fit, the column scrolls and lists them all.
        _keystonePaged = true;
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned - KeystoneRows));
        var height = Place(0, KeystoneRows, LoadW);
        _keystonePaged = height <= region.Height;
        if (!_keystonePaged) { _keystoneScroll = 0; height = Place(0, Math.Max(KeystoneRows, learned), LoadW); }
        _loadOverflow = Math.Max(0, height - region.Height);
        _loadScroll = Math.Clamp(_loadScroll, 0, _loadOverflow);
        if (_loadOverflow > 0) Place(_loadScroll, Math.Max(KeystoneRows, learned), LoadW - ScrollLane);

        int Place(int scroll, int chipCount, int width)
        {
            _rows.Clear();
            _chips.Clear();
            // Empties beyond the equipped count are drawn in the group that still has room.
            var empties = Math.Max(0, cap - skills.Count);
            var y = region.Y - scroll;
            void Group(List<int> members, int groupCap, bool passive)
            {
                y += GroupCaptionH;   // the caption's line
                var first = true;
                for (var k = 0; k < groupCap; k++)
                {
                    var slot = k < members.Count ? members[k] : -1;
                    if (slot < 0) { if (empties <= 0) continue; empties--; }
                    _rows.Add((slot, new Rectangle(LoadX, y, width, SlotH), passive, first));
                    first = false;
                    y += SlotH + SlotGap;
                }
                y += GroupGap;
            }
            Group(actives, activeCap, false);
            if (passiveCap > 0) Group(passives, passiveCap, true);
            _rowsEnd = y;
            _keystoneHead = new Rectangle(LoadX, y + UiMetrics.Space(6), width, KeystoneHeadH);
            y = _keystoneHead.Bottom;
            for (var i = 0; i < chipCount; i++)
            {
                _chips.Add((_keystonePaged ? _keystoneScroll + i : i, new Rectangle(LoadX, y, width, ChipH)));
                y += ChipH + ChipGap;
            }
            return y - ChipGap - (region.Y - scroll);
        }
    }

    // ── SKILLS: the library and the selected skill's tree, one scrolling column under the SKILLS head. ──
    private const int StyleCount = 6;
    private static int SkillsX => SkillsPanel.X + UiMetrics.PanelPadding;
    private static int SkillsW => SkillsPanel.Width - UiMetrics.PanelPadding * 2;
    private static int SkillsHeadY => SkillsPanel.Y + UiMetrics.Space(16);
    /// <summary>Everything under the SKILLS head, down to the plate's bottom padding. Clipped to when it scrolls.</summary>
    private static Rectangle SkillsRegion
    {
        get
        {
            var top = SkillsHeadY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8);
            return new(SkillsPanel.X + 2, top, SkillsPanel.Width - 4, SkillsPanel.Bottom - UiMetrics.PanelPadding - top);
        }
    }
    private int _skillsScroll;
    private int _skillsOverflow;
    private int LibW => SkillsW - (_skillsOverflow > 0 ? ScrollLane : 0);
    private int LibTop => SkillsRegion.Y + UiMetrics.Space(6) - _skillsScroll;
    private static int LibTileH => UiMetrics.Control(52);
    private static int LibRowPitch => LibTileH + UiMetrics.Space(10);
    /// <summary>The style label's column: ~80 px of ink at 100 % and the rest is the gap to the tiles, so it grows at the spacing rate and the tiles keep their width.</summary>
    private static int LibStyleW => UiMetrics.Space(118);
    private static int LibTileGap => UiMetrics.Space(12);
    private Rectangle LibTile(int style, int which)
    {
        var w = (LibW - LibStyleW - LibTileGap) / 2;
        return new(SkillsX + LibStyleW + which * (w + LibTileGap), LibTop + style * LibRowPitch, w, LibTileH);
    }
    private int TreeTop => LibTop + StyleCount * LibRowPitch + UiMetrics.Space(22);
    private static int TreeIcon => UiMetrics.Control(48);
    private int RailY => TreeTop + TreeIcon + UiMetrics.Space(10);
    private static int VarCardH => UiMetrics.Control(96);
    private static int VarCardGap => UiMetrics.Space(16);
    private Rectangle VarCard(int which)
    {
        var w = (LibW - VarCardGap) / 2;
        return new(SkillsX + which * (w + VarCardGap), RailY + UiMetrics.Space(16), w, VarCardH);
    }
    private static int ReinfH => UiMetrics.Control(34);
    private static int ReinfPitch => ReinfH + UiMetrics.Space(6);
    private Rectangle ReinfChip(int which, int ri)
    {
        var card = VarCard(which);
        return new(card.X, card.Bottom + UiMetrics.Space(10) + ri * ReinfPitch, card.Width, ReinfH);
    }

    /// <summary>The tree drawn for the selected slot's skill, if it has one the hunter knows.</summary>
    private SkillDef? TreeDefIn(IReadOnlySet<string> known) => SlotDef(_slot) is { } d && known.Contains(d.Id) ? d : null;
    private SkillDef? TreeDef => TreeDefIn(KnownSkills());

    /// <summary>How tall the skills column's content is at this profile, and so whether — and how far — it scrolls.</summary>
    private void LayoutSkills()
    {
        var height = UiMetrics.Space(6) + StyleCount * LibRowPitch + UiMetrics.Space(22);
        if (TreeDef is { } def)
        {
            var reinforcements = 0;
            foreach (var v in def.Variations.Take(2)) reinforcements = Math.Max(reinforcements, Math.Min(3, v.Reinforcements.Count));
            height += TreeIcon + UiMetrics.Space(10) + UiMetrics.Space(16) + VarCardH + UiMetrics.Space(10) + reinforcements * ReinfPitch - UiMetrics.Space(6);
        }
        else height += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6) + UiTypography.Pitch(UiTypography.Body);
        _skillsOverflow = Math.Max(0, height + UiMetrics.Space(12) - SkillsRegion.Height);
        _skillsScroll = Math.Clamp(_skillsScroll, 0, _skillsOverflow);
    }

    // ── THE INSPECTOR: a scrolling body over an anchored refusal line, two text actions and the one button. ──
    private static int InsX => UiKit.ContentLeft(InspectorPanel);
    private static int InsW => UiKit.ContentRight(InspectorPanel) - InsX;
    private static Rectangle PrimaryBtn => new(InsX, InspectorPanel.Bottom - UiMetrics.Space(36) - UiMetrics.ButtonHeightPrimary, InsW, UiMetrics.ButtonHeightPrimary);
    /// <summary>The text actions' row: a hit target at least the house minimum tall (brief §107).</summary>
    private static int ActionRowH => Math.Max(UiMetrics.HitTargetMinimum, UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8));
    private static Rectangle RespecText => new(InsX, PrimaryBtn.Y - UiMetrics.Space(8) - ActionRowH, InsW / 2 - UiMetrics.Space(8), ActionRowH);
    private static Rectangle CopyText => new(InsX + InsW / 2 + UiMetrics.Space(8), RespecText.Y, InsW / 2 - UiMetrics.Space(8), ActionRowH);
    private static int RefusalY => RespecText.Y - UiMetrics.Space(4) - UiTypography.Pitch(UiTypography.Secondary);
    private static int InsBodyTop => InspectorPanel.Y + UiTypography.PanelTitleTop;
    /// <summary>The body's room: from the title row down to the refusal line. The body is clipped to it when it scrolls.</summary>
    private static Rectangle InsRegion => new(InspectorPanel.X + UiKit.PanelCorner / 2, InsBodyTop, InspectorPanel.Width - UiKit.PanelCorner, RefusalY - UiMetrics.Space(4) - InsBodyTop);
    private int _insScroll;
    private int _insOverflow;
    /// <summary>The body's width — the content column less the scrollbar's lane while it scrolls. Wrapping follows, and the two states cannot flip-flop: text in the narrower column is never shorter.</summary>
    private int InsBodyW => InsW - (_insOverflow > 0 ? ScrollLane : 0);
    private Rectangle ChangeVowBtn(int y) => new(InsX, y, InsBodyW, UiMetrics.ButtonHeightSmall);
    private static int VowRowH => UiMetrics.Control(46);
    private static int VowRowPitch => VowRowH + UiMetrics.Space(4);
    private Rectangle VowListRow(int i, int top) => new(InsX, top + i * VowRowPitch, InsBodyW, VowRowH);

    /// <summary>The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates.</summary>
    internal Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.SkillSlots => new[] { new Rectangle(LoadoutPanel.X, LoadoutPanel.Y, LoadoutPanel.Width, Math.Max(UiMetrics.Control(200), _rowsEnd - LoadoutPanel.Y)) },
        TourTarget.SkillPicker => new[] { SkillsPanel },
        TourTarget.Vows => new[]
        {
            InspectorPanel,
            new Rectangle(LoadoutPanel.X, _keystoneHead.Y - UiMetrics.Space(10), LoadoutPanel.Width,
                          (_chips.Count > 0 ? _chips[^1].Rect.Bottom : _keystoneHead.Bottom) + UiMetrics.Space(16) - _keystoneHead.Y + UiMetrics.Space(10)),
        },
        _ => Array.Empty<Rectangle>(),
    };

    // ── CLIPPING. A column that scrolls is drawn under a scissor on its region — the house pattern (MASTERY,
    // TRAITS): close the host's batch, reopen it clipped in the SAME transform, and reopen it unclipped after.
    // A column that fits is drawn straight into the host's batch, so 100 % costs nothing it did not before. ──
    private RasterizerState? _clip;
    private RasterizerState Clip => _clip ??= new RasterizerState { ScissorTestEnable = true };

    private void BeginClip(SpriteBatch b, Rectangle region)
    {
        b.End();
        _ui.Device.ScissorRectangle = Rectangle.Intersect(Game1.OverlayToCanvas(region, Vector2.Zero), _ui.Device.Viewport.Bounds);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Clip, null, Game1.OverlayTransform(Vector2.Zero));
    }

    private static void EndClip(SpriteBatch b)
    {
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Game1.OverlayTransform(Vector2.Zero));
    }

    /// <summary>The scrollbar's track at the right of a column's content, down its region.</summary>
    private static Rectangle ScrollTrack(int contentRight, Rectangle region) => new(contentRight - UiMetrics.ScrollbarWidth, region.Y, UiMetrics.ScrollbarWidth, region.Height);

    // ── MODEL READS ─────────────────────────────────────────────────────────────────────────────────────
    private static string SourceName(Source s) => s.ToString().ToUpperInvariant();
    private static string StyleName(Style s) => s.ToString().ToUpperInvariant();
    private IReadOnlyList<Vow> Known => DustEffects.KnownVows(Tree);

    /// <summary>The live build, described to the Vow layer — the same struct the simulation judges against.</summary>
    private BuildContext Context =>
        Hunter is { } h ? SoloBattle.DescribeBuild(Loadout.ToBuild(Tree, Mastery, Character, SkillLevels), h) : BuildContext.Empty;

    /// <summary>Every skill this hunter can equip: the roads walked, plus what it was born with.</summary>
    private IReadOnlySet<string> KnownSkills()
    {
        var set = Mastery.LearnedSkills().ToHashSet(StringComparer.Ordinal);
        if (Character?.StartingSkillId is { } born) set.Add(born);
        return set;
    }

    private SkillDef? SlotDef(int slot) =>
        slot >= 0 && slot < Loadout.Skills.Count ? SkillCatalogue.Find(Loadout.Skills[slot].SkillId) : null;

    /// <summary>A slot that holds a skill the hunter knows. Anything else reads as empty — the composer refuses it.</summary>
    private bool SlotFilled(int slot) => SlotDef(slot) is { } d && KnownSkills().Contains(d.Id);

    private float Dps(PlayerLoadout loadout, Hunter hunter)
        => DamageBench.Measure(loadout.ToBuild(Tree, Mastery, Character, SkillLevels), hunter).Dps;

    /// <summary>What a Vow demands, in one line the player can check against their own build.</summary>
    private static string DemandText(Vow v) => v.Demand switch
    {
        VowDemand.SingleStyle => "EVERY SKILL THE SAME STYLE",
        VowDemand.SingleSource => "EVERY SKILL THE SAME SOURCE",
        VowDemand.EverySlotFilled => "NO EMPTY SKILL SLOT",
        VowDemand.NoCritInvestment => "NO CRITICAL BONUS",
        VowDemand.CadenceAtOrBelow => $"SKILL RATE MAX {v.Threshold:0.##}x",
        VowDemand.CadenceAtOrAbove => $"SKILL RATE MIN {v.Threshold:0.##}x",
        VowDemand.NoDefence => "NO DEFENCE AT ALL",
        VowDemand.NoKeystone => "NO KEYSTONE IN USE",
        VowDemand.SlotLeftBare => $"{v.Bare.ToString().ToUpperInvariant()} SLOT LEFT EMPTY",
        _ => "NO DEMAND — ALWAYS ON",
    };

    /// <summary>What the build actually IS, against the same demand — the validator's second line.</summary>
    private string YourBuildText(Vow v, BuildContext ctx)
    {
        switch (v.Demand)
        {
            case VowDemand.SingleStyle:
                var styles = Loadout.EquippedDefs().Select(d => StyleName(d.Style)).Distinct().ToList();
                return styles.Count == 0 ? "NO SKILLS" : $"{styles.Count} STYLE{(styles.Count == 1 ? "" : "S")}: {string.Join(", ", styles)}";
            case VowDemand.SingleSource:
                var sources = Loadout.ToBuild(Tree, Mastery, Character, SkillLevels).Skills.Select(s => SourceName(s.Source)).Distinct().ToList();
                return sources.Count == 0 ? "NO SKILLS" : $"{sources.Count} SOURCE{(sources.Count == 1 ? "" : "S")}: {string.Join(", ", sources)}";
            case VowDemand.EverySlotFilled: return $"{ctx.SkillsWoven} OF {ctx.SkillSlots} SLOTS FILLED";
            case VowDemand.NoCritInvestment: return $"CRITICAL {ctx.CritPercent:0.#}% (BASE {ctx.BaseCritPercent:0.#}%)";
            case VowDemand.CadenceAtOrBelow:
            case VowDemand.CadenceAtOrAbove: return $"SKILL RATE {ctx.SkillRate:0.00}x";
            case VowDemand.NoDefence: return $"DEFENCE {ctx.Defence}";
            case VowDemand.NoKeystone: return $"{ctx.KeystonesWorn} KEYSTONE{(ctx.KeystonesWorn == 1 ? "" : "S")} IN USE";
            case VowDemand.SlotLeftBare: return $"{v.Bare.ToString().ToUpperInvariant()} SLOT {(ctx.WornSlots.Contains(v.Bare) ? "WORN" : "EMPTY")}";
            default: return "ALWAYS ON";
        }
    }

    // ── UPDATE ──────────────────────────────────────────────────────────────────────────────────────────
    public void Update(Point mouse, bool clicked, bool held, int wheel)
    {
        var hit = mouse;
        var skills = Loadout.Skills;
        var known = Known;
        // The wheel first, against last frame's overflow, so this frame's layout already sits where it scrolled to.
        if (wheel != 0)
        {
            if (LoadoutPanel.Contains(hit))
            {
                if (_keystonePaged) _keystoneScroll = UiKit.Scrolled(_keystoneScroll, wheel, KeystoneRows, DustEffects.LearnedKeystones(Tree).Count);
                else _loadScroll = Math.Clamp(_loadScroll - wheel * ScrollStep, 0, _loadOverflow);
            }
            else if (SkillsPanel.Contains(hit)) _skillsScroll = Math.Clamp(_skillsScroll - wheel * ScrollStep, 0, _skillsOverflow);
            else if (InspectorPanel.Contains(hit)) _insScroll = Math.Clamp(_insScroll - wheel * ScrollStep, 0, _insOverflow);
        }
        LayoutRows();
        LayoutSkills();

        _carryAt = hit;
        var released = _wasHeld && !held;
        _wasHeld = held;
        if (!held && !released && _carrying != Carry.None) { _carrying = Carry.None; _carrySlot = -1; _carryMoved = false; }
        if (_carrying != Carry.None && held
            && (Math.Abs(hit.X - _carryFrom.X) > DragSlop || Math.Abs(hit.Y - _carryFrom.Y) > DragSlop))
            _carryMoved = true;

        if (released && _carrying != Carry.None)
        {
            var from = _carrySlot;
            var moved = _carryMoved;
            _carrying = Carry.None; _carrySlot = -1; _carryMoved = false;
            if (moved)
            {
                var onto = SlotUnder(hit);
                if (from >= 0 && onto >= 0 && Loadout.MoveSkill(from, onto))
                {
                    _slot = onto; _pick = Pick.Slot;
                    Dirty = true; _buildRev++;
                    _slotFlash[onto] = SetFlashSeconds;
                    Sound?.Play("sfx_weave", 0.5f);
                    _msg = onto == 0 ? "FIRST IN LINE — IT WINS EVERY TIED BEAT." : $"NOW SLOT {onto + 1}.";
                }
                return;
            }
        }

        if (!clicked) return;

        // ── THE LOADOUT COLUMN: rows select (and arm a reorder); an empty row adds a slot. A row is hit
        // by the part of it that is on screen — what a scrolled list shows is what it takes (LAW 5). ──
        var list = ListRegion;
        foreach (var (slot, rect, _, _) in _rows)
        {
            if (!In(rect, list).Contains(hit)) continue;
            if (slot >= 0)
            {
                _slot = slot; _pick = Pick.Slot; _vowListOpen = false; _msg = "";
                _carrying = Carry.Slot; _carrySlot = slot; _carryFrom = hit; _carryMoved = false;
            }
            else if (skills.Count < Loadout.SkillCapacity)
            {
                _slot = Loadout.AddSkill(); _pick = Pick.Slot; _vowListOpen = false;
                Dirty = true; _buildRev++;
                _msg = "PICK A SKILL FROM THE LIBRARY FOR THIS SLOT.";
            }
            return;
        }
        var learned = DustEffects.LearnedKeystones(Tree);
        foreach (var (index, chip) in _chips)
        {
            if (index >= learned.Count || !In(chip, list).Contains(hit)) continue;
            _pick = Pick.Keystone; _pickKeystoneId = learned[index].Id; _vowListOpen = false; _msg = "";
            return;
        }

        // ── THE SKILLS COLUMN: tiles, the fork, the chips — all select. ─────────────────────────────
        var skillsRegion = SkillsRegion;
        for (var st = 0; st < StyleCount; st++)
            for (var w = 0; w < 2; w++)
            {
                if (!In(LibTile(st, w), skillsRegion).Contains(hit)) continue;
                var def = w == 0 ? SkillCatalogue.ActiveOf((Style)st) : SkillCatalogue.PassiveOf((Style)st);
                _pick = Pick.Library; _pickSkillId = def.Id; _vowListOpen = false; _msg = "";
                return;
            }
        if (SlotDef(_slot) is { } cur && KnownSkills().Contains(cur.Id))
        {
            var chosen = SkillLevels.VariationOf(cur);
            for (var vi = 0; vi < cur.Variations.Count && vi < 2; vi++)
            {
                if (In(VarCard(vi), skillsRegion).Contains(hit)) { _pick = Pick.Variation; _pickIndex = vi; _vowListOpen = false; _msg = ""; return; }
                var v = cur.Variations[vi];
                for (var ri = 0; ri < v.Reinforcements.Count && ri < 3; ri++)
                    if (In(ReinfChip(vi, ri), skillsRegion).Contains(hit))
                    {
                        if (chosen?.Name != v.Name) { _pick = Pick.Variation; _pickIndex = vi; _msg = $"CHOOSE {v.Name} FIRST — ITS REINFORCEMENTS COME AFTER."; }
                        else { _pick = Pick.Reinforcement; _pickIndex = ri; _msg = ""; }
                        _vowListOpen = false;
                        return;
                    }
            }
        }

        // ── THE INSPECTOR: the primary button, the text actions, the vow list. ──────────────────────
        var insRegion = InsRegion;
        if (_vowListOpen)
        {
            var top = _vowListTop;
            for (var r = 0; r <= known.Count; r++)
            {
                var idx = r - 1;   // row 0 is NO VOW
                if (!In(VowListRow(r, top), insRegion).Contains(hit)) continue;
                if (_slot >= skills.Count) { _msg = "PICK A SLOT FIRST."; return; }
                if (idx < 0)
                {
                    if (Loadout.SetVow(_slot, null, known)) { Dirty = true; _buildRev++; _msg = "NO VOW ON THIS SLOT."; }
                }
                else
                {
                    var v = known[idx];
                    var already = skills[_slot].VowId == v.Id;
                    if (Loadout.SetVow(_slot, already ? null : v.Id, known))
                    {
                        Dirty = true; _buildRev++;
                        if (already) _msg = $"{v.Name.ToUpperInvariant()} BROKEN.";
                        else { _msg = $"{v.Name.ToUpperInvariant()} BOUND TO SLOT {_slot + 1}."; _bindFlash[_slot] = BindFlashSeconds; Sound?.Play("sfx_bind", 0.55f); }
                    }
                    else _msg = "THIS SLOT CANNOT TAKE THAT VOW.";
                }
                _vowListOpen = false;
                return;
            }
            if (_changeVowShown && In(ChangeVowBtn(_changeVowY), insRegion).Contains(hit)) { _vowListOpen = false; return; }
            if (InspectorPanel.Contains(hit)) return;
        }
        else if (_changeVowShown && In(ChangeVowBtn(_changeVowY), insRegion).Contains(hit) && _pick == Pick.Slot && SlotFilled(_slot))
        {
            _vowListOpen = true;
            _vowListReveal = 2;
            return;
        }
        if (RespecText.Contains(hit) && _respecShown && SlotDef(_slot) is { } rd)
        {
            SkillLevels.Respec(rd.Id);
            Dirty = true; _buildRev++;
            if (_pick == Pick.Reinforcement) _pick = Pick.Slot;
            _msg = $"{rd.Name} IS UNSPENT AGAIN. EVERY LEVEL IT EARNED IS STILL THERE.";
            return;
        }
        if (CopyText.Contains(hit))
        {
            var code = IdleXIdle.Core.Persistence.ShareCodes.EncodeBuild(
                new IdleXIdle.Core.Persistence.ShareCodes.SharedBuild
                {
                    Skills = Loadout.Skills.Select(s => new IdleXIdle.Core.Persistence.SavedSkill
                    {
                        SkillId = s.SkillId, Source = s.Source.ToString(), VowId = s.VowId, Passive = s.Passive,
                    }).ToList(),
                    Keystones = Loadout.KeystoneIds.ToList(),
                    Mastery = MasteryTaken.ToList(),
                });
            _copyToast = ClipboardInterop.TrySet(code) ? "COPIED — A FRIEND PASTES IT IN THE VAULT" : "COPY FAILED — TRY AGAIN";
            _copyToastFrames = 240;
            return;
        }
        if (PrimaryBtn.Contains(hit)) Commit();
    }

    /// <summary>The primary button's verb for the current selection, and whether it may be pressed.</summary>
    private (string Label, bool Enabled, string Refusal) Primary()
    {
        var skills = Loadout.Skills;
        var known = KnownSkills();
        switch (_pick)
        {
            case Pick.Library:
            {
                if (SkillCatalogue.Find(_pickSkillId) is not { } def) return ("", false, "");
                if (!known.Contains(def.Id)) return ($"LEARN ON {StyleName(def.Style)}'S ROAD", false, $"LEARNED ON {StyleName(def.Style)}'S ROAD, ON THE MASTERY TREE.");
                if (_slot >= skills.Count) return ("EQUIP", false, "PICK A SLOT ON THE LEFT FIRST.");
                if (skills[_slot].SkillId == def.Id) return ($"EQUIPPED IN SLOT {_slot + 1}", false, "");
                // LAW 13: one slot per skill. The button says WHERE it already is, in words, rather than
                // going grey without a reason — the loadout itself refuses the write regardless.
                if (Loadout.IndexOfSkill(def.Id) is var other && other >= 0)
                    return ($"ALREADY EQUIPPED IN SLOT {other + 1}", false, $"A SKILL GOES IN ONE SLOT. IT IS IN SLOT {other + 1} — PICK THAT SLOT TO CHANGE IT.");
                return ($"EQUIP TO SLOT {_slot + 1}", true, "");
            }
            case Pick.Variation:
            {
                if (SlotDef(_slot) is not { } def || _pickIndex >= def.Variations.Count) return ("", false, "");
                var v = def.Variations[_pickIndex];
                var chosen = SkillLevels.VariationOf(def);
                if (chosen?.Name == v.Name) return ("CHOSEN", false, "");
                if (chosen is not null) return ($"CHOOSE {v.Name}", false, $"{def.Name} IS {chosen.Name}. RESPEC TO CHANGE — IT IS FREE.");
                if (SkillLevels.FreeOn(def.Id) < 1)
                    return ($"CHOOSE {v.Name}", false, $"NO LEVEL TO SPEND — {WavesToNext(def)} MORE WAVES WITH {def.Name} EQUIPPED.");
                return ($"CHOOSE {v.Name}", true, "");
            }
            case Pick.Reinforcement:
            {
                if (SlotDef(_slot) is not { } def || SkillLevels.VariationOf(def) is not { } v || _pickIndex >= v.Reinforcements.Count) return ("", false, "");
                var r = v.Reinforcements[_pickIndex];
                if (SkillLevels.HasReinforcement(def.Id, r.Name)) return ("OWNED", false, "");
                if (SkillLevels.FreeOn(def.Id) < 1)
                    return ($"TAKE {r.Name}", false, $"NO LEVEL TO SPEND — {WavesToNext(def)} MORE WAVES WITH {def.Name} EQUIPPED.");
                return ($"TAKE {r.Name}", true, "");
            }
            case Pick.Keystone:
            {
                if (Loadout.HasKeystone(_pickKeystoneId)) return ("UNSOCKET", true, "");
                if (Loadout.KeystoneIds.Count >= Loadout.KeystoneCapacity)
                    return ("SOCKET", false, $"ONLY {Loadout.KeystoneCapacity} SOCKET{(Loadout.KeystoneCapacity == 1 ? "" : "S")} — MORE ON THE TRAITS SCREEN.");
                return ("SOCKET", true, "");
            }
            default:
            {
                if (!SlotFilled(_slot)) return ("REMOVE FROM SLOT", false, "");
                if (skills.Count <= 1) return ("REMOVE FROM SLOT", false, "THE LAST SKILL STAYS — A HUNTER NEEDS ONE.");
                return ("REMOVE FROM SLOT", true, "");
            }
        }
    }

    private string WavesToNext(SkillDef def)
    {
        var level = SkillLevels.LevelOf(def.Id);
        if (level >= SkillProgress.MaxLevel) return "0";
        return Math.Max(0, SkillProgress.UsesForLevel(level + 1) - SkillLevels.UsesOf(def.Id)).ToString();
    }

    /// <summary>Press the primary button: the one commit per selection.</summary>
    private void Commit()
    {
        var (_, enabled, _) = Primary();
        if (!enabled) return;
        switch (_pick)
        {
            case Pick.Library:
                // The loadout has the last word (LAW 13): a refusal here means Primary() and the model
                // disagree, and the message says so instead of pretending the equip happened.
                if (!Loadout.SetSkill(_slot, _pickSkillId))
                {
                    var where = Loadout.IndexOfSkill(_pickSkillId);
                    _msg = where >= 0 ? $"ALREADY EQUIPPED IN SLOT {where + 1}." : "THAT SKILL CANNOT GO THERE.";
                    break;
                }
                Dirty = true; _buildRev++;
                _slotFlash[_slot] = SetFlashSeconds; Sound?.Play("sfx_weave", 0.45f);
                _msg = $"{SkillCatalogue.Find(_pickSkillId)?.Name} EQUIPPED IN SLOT {_slot + 1}.";
                _pick = Pick.Slot;
                break;
            case Pick.Variation:
            {
                var def = SlotDef(_slot)!;
                var v = def.Variations[_pickIndex];
                SkillLevels.ChooseVariation(def, v.Name); Dirty = true; _buildRev++;
                _msg = $"{def.Name} IS NOW {v.Name}, AND IT IS {SourceName(v.Source)}.";
                break;
            }
            case Pick.Reinforcement:
            {
                var def = SlotDef(_slot)!;
                var r = SkillLevels.VariationOf(def)!.Reinforcements[_pickIndex];
                SkillLevels.TakeReinforcement(def, r.Name); Dirty = true; _buildRev++;
                _msg = $"{r.Name}: {r.Line.ToUpperInvariant()}";
                break;
            }
            case Pick.Keystone:
                if (Loadout.ToggleKeystone(_pickKeystoneId, DustEffects.LearnedKeystones(Tree))) { Dirty = true; _buildRev++; _msg = ""; }
                break;
            default:
                Loadout.RemoveSkill(_slot);
                _slot = Math.Max(0, Math.Min(_slot, Loadout.Skills.Count - 1));
                Dirty = true; _buildRev++; _msg = "SLOT CLEARED.";
                break;
        }
    }

    private int SlotUnder(Point p)
    {
        var list = ListRegion;
        var slopX = UiMetrics.Space(8);
        var slopY = UiMetrics.Space(4);
        foreach (var (slot, r, _, _) in _rows)
            if (slot >= 0 && In(new Rectangle(r.X - slopX, r.Y - slopY, r.Width + slopX * 2, r.Height + slopY * 2), list).Contains(p)) return slot;
        return -1;
    }

    // ── DRAW ────────────────────────────────────────────────────────────────────────────────────────────
    private string? _tip;
    private Point _tipAt;
    private int _changeVowY;
    private bool _changeVowShown;
    private int _vowListTop;
    private bool _respecShown;

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        var hit = mouse;
        _tip = null;
        LayoutRows();
        LayoutSkills();
        TickEffects();

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC8));
        _ui.TextCenterBig(b, "BUILD", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        DrawLoadout(b, hit);
        DrawSkills(b, hit);
        DrawInspector(b, hit);

        if (_msg.Length > 0) _ui.TextCenterBig(b, _msg, UiKit.PageCenterX, UiKit.PageBottom(BottomMargin) + UiMetrics.Space(16), Gold, UiTypography.Body);
        if (_copyToastFrames > 0)
        {
            _copyToastFrames--;
            _ui.TextCenterBig(b, _copyToast, InspectorPanel.Center.X, InspectorPanel.Bottom + UiMetrics.Space(14),
                              _copyToast.StartsWith("COPIED", StringComparison.Ordinal) ? Gold : Ember, UiTypography.Secondary);
        }
        DrawCarried(b);
        if (_tip is { } tip) _ui.HoverTip(b, tip, _tipAt);
    }

    private void Tip(Rectangle r, Point hit, string text) { if (r.Contains(hit) && _carrying == Carry.None) { _tip = text; _tipAt = hit; } }

    // ── YOUR LOADOUT ────────────────────────────────────────────────────────────────────────────────────
    private void DrawLoadout(SpriteBatch b, Point hit)
    {
        var panel = LoadoutPanel;
        _ui.Panel(b, panel);   // the one ornate surface: the build IS the subject of this screen
        _ui.TextCenterBig(b, "YOUR LOADOUT", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);

        var skills = Loadout.Skills;
        var ctx = Context;
        var known = KnownSkills();
        var region = ListRegion;
        var scrolling = _loadOverflow > 0;
        if (scrolling) BeginClip(b, region);
        foreach (var (slot, row, passive, first) in _rows)
        {
            if (first)
                _ui.TextBig(b, passive ? "PASSIVE — ALWAYS ON" : "ACTIVE — TAKES A TURN", row.X, row.Y - UiTypography.Pitch(UiTypography.Secondary) - 2, Slate, UiTypography.Secondary);
            if (row.Bottom <= region.Y || row.Y >= region.Bottom) continue;   // scrolled clear of the region
            var shown = In(row, region);
            if (slot < 0) { DrawEmptyRow(b, row, shown, hit); continue; }

            var s = skills[slot];
            var def = SkillCatalogue.Find(s.SkillId);
            var filled = def is not null && known.Contains(def.Id);
            var on = slot == _slot && _pick != Pick.Keystone;
            var over = shown.Contains(hit) && _carrying == Carry.None;
            var dropping = _carrying == Carry.Slot && _carryMoved && SlotUnder(_carryAt) == slot && _carrySlot != slot;
            var inHand = _carrying == Carry.Slot && _carryMoved && _carrySlot == slot;
            var col = SourceColor.GetValueOrDefault(s.Source, Bone);

            _ui.Fill(b, row, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            if (dropping) { _ui.Fill(b, row, col * 0.16f); Outline(b, row, Bone, 3); }
            if (inHand) _ui.Fill(b, row, new Color(0x0C, 0x09, 0x14) * 0.6f);
            if (_slotFlash.TryGetValue(slot, out var sf)) { var t = Math.Clamp(sf / SetFlashSeconds, 0f, 1f); _ui.Fill(b, row, col * (0.30f * t)); }
            if (_bindFlash.TryGetValue(slot, out var bf)) DrawBindChain(b, row, bf / BindFlashSeconds);
            if (on && !dropping) Outline(b, row, Gold, 2);   // gold = selected

            // The numbered spine: this order IS cast priority.
            var spine = new Rectangle(row.X, row.Y, SpineW, row.Height);
            _ui.Fill(b, spine, col * (on ? 0.55f : 0.34f));
            _ui.TextCenterBig(b, $"{slot + 1}", spine.Center.X, row.Y + (row.Height - UiTypography.Body) / 2, on ? Bone : Bone * 0.75f, UiTypography.Body);

            var gbox = GlyphRect(row);
            _ui.Fill(b, gbox, new Color(0x0C, 0x09, 0x14) * 0.55f);
            // The row's three lines: the name, the style line one headline pitch under it, and the variation line
            // sitting on the row's bottom pad — so a taller row (a bigger profile) opens air between them rather
            // than printing line 2 over line 3.
            var tx = RowTextX(row);
            var pad = UiMetrics.Space(12);
            var right = row.Right - pad;
            var line1 = row.Y + UiMetrics.Space(10);
            var line2 = line1 + UiTypography.Pitch(UiTypography.Headline) - UiMetrics.Space(4);
            var line3 = row.Bottom - UiMetrics.Space(8) - UiTypography.Body;
            if (!filled || def is null)
            {
                _ui.Icon(b, "ui_slot_locked", gbox, Slate);
                _ui.TextBig(b, "EMPTY SLOT", tx, row.Y + pad, UiInk.Empty, UiTypography.Headline);
                _ui.TextBig(b, _ui.ShortenBig("PICK A SKILL FROM THE LIBRARY", right - tx, UiTypography.Secondary), tx, row.Y + pad + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Secondary);
                continue;
            }
            _ui.Icon(b, $"icon_skill_{def.Id}", gbox, col);

            // Line 1: NAME, and the vow pill at the right.
            var vow = Vows.ById(s.VowId);
            var pillW = 0;
            if (vow is not null)
            {
                var live = Vows.IsActive(vow, ctx);
                var pill = $"{vow.Short.ToUpperInvariant()} {(live ? "OK" : "BROKEN")}";
                pillW = _ui.MeasureBig(pill, UiTypography.Caption) + UiTypography.ChipPadX * 2;
                var pr = new Rectangle(right - pillW, row.Y + pad, pillW, UiTypography.Caption + UiTypography.ChipPadY * 2);
                _ui.Fill(b, pr, (live ? Gold : Ember) * 0.16f);
                Outline(b, pr, live ? Gold : Ember, 1);
                _ui.TextBig(b, pill, pr.X + UiTypography.ChipPadX, pr.Y + UiTypography.ChipPadY, live ? Gold : Ember, UiTypography.Caption);
                Tip(In(pr, region), hit, live ? $"{vow.Name.ToUpperInvariant()} holds — x{Vows.Multiplier(vow):0.00}." : $"{vow.Name.ToUpperInvariant()} is broken: {DemandText(vow)} — it pays nothing until it holds.");
            }
            _ui.TextBig(b, _ui.ShortenBig(def.Name, right - pillW - pad - tx, UiTypography.Headline), tx, line1, on ? Gold : Bone, UiTypography.Headline);
            // Line 2: STYLE · LEVEL · the style factor.
            var level = SkillLevels.LevelOf(def.Id);
            var line2Text = $"{StyleName(def.Style)} · LV {level}";
            if (ChosenStyle is { } dd)
            {
                var f = StyleAffinity.Factor(dd, def.Style, vowSworn: s.VowId is not null);
                line2Text += f >= 1.99f ? " · x2.0 YOURS" : $" · x{f:0.0#}";
            }
            _ui.TextBig(b, line2Text, tx, line2, Slate, UiTypography.Secondary);
            // Line 3: the variation, its Source (gem + word), and how much of it is bought — or the level to spend.
            var chosen = SkillLevels.VariationOf(def);
            var free = SkillLevels.FreeOn(def.Id);
            var y3 = line3;
            if (chosen is null)
            {
                // The call to action keeps its first clause whole when the row is too narrow for both — the
                // variation cards under the tree say the rest — rather than ending on a cut word.
                var call = free > 0 ? $"+{free} LEVEL TO SPEND — CHOOSE A VARIATION" : "NO VARIATION YET";
                if (free > 0 && _ui.MeasureBig(call, UiTypography.Body) > right - tx) call = $"+{free} LEVEL TO SPEND";
                _ui.TextBig(b, _ui.ShortenBig(call, right - tx, UiTypography.Body), tx, y3, free > 0 ? Gold : Slate, UiTypography.Body);
            }
            else
            {
                var vc = SourceColor.GetValueOrDefault(chosen.Source, Bone);
                var gemEdge = UiTypography.Body;   // the Source gem sits on the line, as tall as its type
                if (_ui.Assets.Get($"source_{chosen.Source.ToString().ToLowerInvariant()}") is { } gem)
                    b.Draw(gem, new Rectangle(tx, y3 + 1, gemEdge, gemEdge), Color.White);
                var bought = chosen.Reinforcements.Count(r => SkillLevels.HasReinforcement(def.Id, r.Name));
                var x3 = tx + gemEdge + UiMetrics.Space(6);
                var room = right - x3;
                var vname = chosen.Name.ToUpperInvariant();
                const string dot = " · ";
                var source = SourceName(chosen.Source);
                var spare = free > 0 ? $"  +{free}" : "";
                var tail = $"{dot}{bought}/{chosen.Reinforcements.Count}{spare}";
                var fixedW = _ui.MeasureBig(dot, UiTypography.Body) + _ui.MeasureBig(source, UiTypography.Body);
                // Fit, in order: the whole line; else without the bought count (the tree's chips carry it) but
                // never without a level to spend; else the name gives way. The Source word always stays — it is
                // the skill's identity, and a colour alone must never carry it.
                if (_ui.MeasureBig(vname, UiTypography.Body) + fixedW + _ui.MeasureBig(tail, UiTypography.Body) > room) tail = spare;
                vname = _ui.ShortenBig(vname, room - fixedW - _ui.MeasureBig(tail, UiTypography.Body), UiTypography.Body);
                _ui.TextBig(b, vname, x3, y3, Bone, UiTypography.Body); x3 += _ui.MeasureBig(vname, UiTypography.Body);
                _ui.TextBig(b, dot, x3, y3, Slate, UiTypography.Body); x3 += _ui.MeasureBig(dot, UiTypography.Body);
                _ui.TextBig(b, source, x3, y3, vc, UiTypography.Body); x3 += _ui.MeasureBig(source, UiTypography.Body);
                if (tail.Length > 0) _ui.TextBig(b, tail, x3, y3, free > 0 ? Gold : Slate, UiTypography.Body);
            }
            Tip(shown, hit, $"{def.Name} — {def.Line}");
        }

        // ── KEYSTONES: chips; click selects, the inspector sockets. Part of the same list, so at a profile
        // where the rows alone fill the column they scroll into view instead of being dropped. ────────
        var learned = DustEffects.LearnedKeystones(Tree);
        var head = _keystoneHead;
        _ui.TextBig(b, "KEYSTONES", head.X, head.Y, Slate, UiTypography.Secondary);
        var paged = _keystonePaged && learned.Count > KeystoneRows
            ? $"  ·  {_keystoneScroll + 1}-{Math.Min(learned.Count, _keystoneScroll + KeystoneRows)} OF {learned.Count}" : "";
        _ui.TextRightBig(b, learned.Count == 0 ? "LEARN THEM ON THE TRAITS SCREEN" : $"{Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity} SOCKETS" + paged,
                         head.Right, head.Y, Slate, UiTypography.Secondary);
        foreach (var (idx, chip) in _chips)
        {
            if (chip.Bottom <= region.Y || chip.Y >= region.Bottom) continue;
            var shown = In(chip, region);
            var textY = chip.Y + (chip.Height - UiTypography.Body) / 2;
            if (idx >= learned.Count)
            {
                _ui.Plate(b, chip);
                _ui.TextBig(b, learned.Count == 0 && idx == 0 ? "NO KEYSTONES LEARNED YET" : "EMPTY SOCKET", chip.X + UiMetrics.Space(16), textY, UiInk.Empty, UiTypography.Body);
                continue;
            }
            var k = learned[idx];
            var worn = Loadout.HasKeystone(k.Id);
            var on = _pick == Pick.Keystone && _pickKeystoneId == k.Id;
            var over = shown.Contains(hit);
            _ui.Fill(b, chip, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            if (worn) _ui.Fill(b, new Rectangle(chip.X, chip.Y, 5, chip.Height), Gold);
            if (on) Outline(b, chip, Gold, 2);
            _ui.TextBig(b, k.Name.ToUpperInvariant(), chip.X + UiMetrics.Space(16), textY, worn ? Gold : Bone, UiTypography.Body);
            if (worn) _ui.TextRightBig(b, "IN USE", chip.Right - UiMetrics.Space(14), chip.Y + (chip.Height - UiTypography.Secondary) / 2, Met, UiTypography.Secondary);
            Tip(shown, hit, k.Blurb);
        }
        if (scrolling)
        {
            EndClip(b);
            _ui.ScrollBar(b, ScrollTrack(LoadX + LoadW, region), _loadScroll, region.Height, region.Height + _loadOverflow);
        }

        // ── THE BENCH: what the build does against the reference dummy, and what the pick would do. ──
        if (Hunter is { } hunter)
        {
            var bench = BenchBlock;
            _ui.Fill(b, new Rectangle(bench.X, bench.Y - UiMetrics.Space(10), bench.Width, 1), Dim);
            _ui.TextBig(b, "BUILD DAMAGE (BENCH)", bench.X, bench.Y, Slate, UiTypography.Secondary);
            if (_currentRev != _buildRev) { _currentDps = Dps(Loadout, hunter); _currentRev = _buildRev; }
            var vy = bench.Y + UiTypography.Pitch(UiTypography.Secondary);
            _ui.TextBig(b, $"{_currentDps:N0} / s", bench.X, vy, Bone, UiTypography.PrimaryValue);
            // The library pick under inspection, previewed in the selected slot: measured on the real loadout, then put back.
            // Not previewed when the pick is already worn elsewhere: SetSkill would refuse it (LAW 13) and
            // the bench would measure the unchanged build and print NO CHANGE for a real difference.
            if (_pick == Pick.Library && SkillCatalogue.Find(_pickSkillId) is { } pd && known.Contains(pd.Id)
                && _slot < skills.Count && skills[_slot].SkillId is { } keepId && keepId != pd.Id
                && !Loadout.HasSkill(pd.Id))
            {
                var key = (pd.Id, _slot, _buildRev);
                if (_previewKey != key)
                {
                    Loadout.SetSkill(_slot, pd.Id);
                    _previewDps = Dps(Loadout, hunter);
                    Loadout.SetSkill(_slot, keepId);
                    _previewKey = key;
                }
                var pct = _currentDps > 0.01f ? (_previewDps - _currentDps) / _currentDps * 100f : 0f;
                var tint = MathF.Abs(pct) < 0.5f ? Slate : pct > 0f ? Met : Ember;
                _ui.TextRightBig(b, MathF.Abs(pct) < 0.5f ? "NO CHANGE" : $"{(pct > 0 ? "+" : "")}{pct:0}% WITH {pd.Name}", bench.Right, vy + UiMetrics.Space(8), tint, UiTypography.Body);
            }
            Tip(bench, hit, "Damage per second against a reference dummy, from the same bench the balance tests use. The fight varies; this compares builds.");
        }
    }

    private void DrawEmptyRow(SpriteBatch b, Rectangle row, Rectangle shown, Point hit)
    {
        var over = shown.Contains(hit);
        _ui.Plate(b, row);
        if (over) Outline(b, row, Slate, 1);
        var gbox = GlyphRect(row);
        Outline(b, gbox, UiInk.Empty, 1);
        var tx = RowTextX(row);
        var top = row.Y + UiMetrics.Space(14);
        var room = row.Right - UiMetrics.Space(12) - tx;
        _ui.TextBig(b, "EMPTY SLOT", tx, top, UiInk.Empty, UiTypography.Headline);
        _ui.TextBig(b, _ui.ShortenBig("CLICK, THEN PICK A SKILL FROM THE LIBRARY", room, UiTypography.Secondary), tx, top + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Secondary);
    }

    // ── SKILLS: the library by style, and the selected skill's tree ────────────────────────────────────
    private void DrawSkills(SpriteBatch b, Point hit)
    {
        var panel = SkillsPanel;
        _ui.Plate(b, panel);
        var known = KnownSkills();
        var skills = Loadout.Skills;
        _ui.TextBig(b, "SKILLS", SkillsX, SkillsHeadY, Slate, UiTypography.Secondary);
        var learnedHead = $"LEARNED {known.Count(id => SkillCatalogue.Find(id) is not null)} / 12  ·  LEARN MORE ON THE MASTERY TREE";
        // Shortened against the room LEFT of it, so a bigger profile trims the sentence instead of printing it
        // through the word SKILLS.
        var headRoom = SkillsW - _ui.MeasureBig("SKILLS", UiTypography.Secondary) - UiMetrics.Space(24);
        _ui.TextRightBig(b, _ui.ShortenBig(learnedHead, headRoom, UiTypography.Secondary), SkillsX + SkillsW, SkillsHeadY, Slate, UiTypography.Secondary);

        // Everything under the head is one column that scrolls when the library and the tree together are
        // taller than the plate (150 %, and 125 % with a tree open) — nothing is shrunk to fit (brief §17).
        var region = SkillsRegion;
        var scrolling = _skillsOverflow > 0;
        if (scrolling) BeginClip(b, region);
        DrawLibrary(b, hit, region, known, skills.Select(s => s.SkillId).OfType<string>().ToHashSet(StringComparer.Ordinal));
        DrawTree(b, hit, region, known);
        if (scrolling)
        {
            EndClip(b);
            _ui.ScrollBar(b, ScrollTrack(SkillsX + SkillsW, region), _skillsScroll, region.Height, region.Height + _skillsOverflow);
        }
    }

    private void DrawLibrary(SpriteBatch b, Point hit, Rectangle region, IReadOnlySet<string> known, IReadOnlySet<string> equipped)
    {
        var icoEdge = UiMetrics.Control(36);
        var lockEdge = UiMetrics.Control(20);
        var nameLeft = UiMetrics.Control(52);
        var edgePad = UiMetrics.Space(8);
        // The EQUIPPED · SLOT N badge shares the name's line. Where a tile at this profile cannot hold the
        // widest badge AND the library's widest name beside it, the badge says only SLOT N — the gold outline
        // already says equipped (brief §9: 150 % may carry less secondary text; never a smaller font). Decided
        // once per frame from measured widths, so every tile agrees.
        var widestName = 0;
        for (var st = 0; st < StyleCount; st++)
            widestName = Math.Max(widestName, Math.Max(_ui.MeasureBig(SkillCatalogue.ActiveOf((Style)st).Name, UiTypography.Body),
                                                       _ui.MeasureBig(SkillCatalogue.PassiveOf((Style)st).Name, UiTypography.Body)));
        var longBadge = LibTile(0, 0).Width - nameLeft - _ui.MeasureBig("EQUIPPED · SLOT 5", UiTypography.Caption) - UiMetrics.Space(16) >= widestName;
        for (var st = 0; st < StyleCount; st++)
        {
            var style = (Style)st;
            var rowY = LibTop + st * LibRowPitch;
            var yours = ChosenStyle == style;
            var labelY = rowY + UiMetrics.Space(14);
            _ui.TextBig(b, StyleName(style), SkillsX, labelY, yours ? Gold : Slate, UiTypography.Body);
            if (yours) _ui.TextBig(b, "YOURS", SkillsX, labelY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(6), Gold, UiTypography.Caption);
            for (var w = 0; w < 2; w++)
            {
                var def = w == 0 ? SkillCatalogue.ActiveOf(style) : SkillCatalogue.PassiveOf(style);
                var tile = LibTile(st, w);
                var shown = In(tile, region);
                var have = known.Contains(def.Id);
                var isEquipped = equipped.Contains(def.Id);
                var on = _pick == Pick.Library && _pickSkillId == def.Id;
                var over = shown.Contains(hit);
                _ui.Fill(b, tile, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.8f);
                Outline(b, tile, on ? Bone : isEquipped ? Gold : over ? Slate : Dim, on || isEquipped ? 2 : 1);
                var ico = new Rectangle(tile.X + edgePad, tile.Y + edgePad, icoEdge, icoEdge);
                if (!_ui.Icon(b, $"icon_skill_{def.Id}", ico, have ? (isEquipped ? Gold : Bone) : Slate)) _ui.Diamond(b, ico, Slate);
                var badge = isEquipped ? (longBadge ? $"EQUIPPED · SLOT {Loadout.IndexOfSkill(def.Id) + 1}" : $"SLOT {Loadout.IndexOfSkill(def.Id) + 1}") : "";
                var tail = isEquipped ? _ui.MeasureBig(badge, UiTypography.Caption) + edgePad : !have ? lockEdge + edgePad : 0;
                var nameX = tile.X + nameLeft;
                var nameY = tile.Y + UiMetrics.Space(6);
                _ui.TextBig(b, _ui.ShortenBig(def.Name, tile.Right - edgePad - tail - nameX, UiTypography.Body), nameX, nameY, have ? (isEquipped ? Gold : Bone) : Slate, UiTypography.Body);
                _ui.TextBig(b, def.TakesABeat ? "ACTIVE" : "PASSIVE", nameX, nameY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(6), Slate, UiTypography.Caption);
                if (!have) _ui.Icon(b, "ui_slot_locked", new Rectangle(tile.Right - edgePad - lockEdge, tile.Y + (tile.Height - lockEdge) / 2, lockEdge, lockEdge), Slate);
                else if (isEquipped) _ui.TextRightBig(b, badge, tile.Right - edgePad, tile.Y + (tile.Height - UiTypography.Caption) / 2, Gold, UiTypography.Caption);
                Tip(shown, hit, have ? $"{def.Name} — {def.Line}" : $"{def.Name} — learned on {StyleName(style)}'s road, on the MASTERY tree.");
            }
        }
    }

    /// <summary>THE SKILL TREE of the selected slot's skill — drawn as the fork it is.</summary>
    private void DrawTree(SpriteBatch b, Point hit, Rectangle region, IReadOnlySet<string> known)
    {
        var treeTop = TreeTop;
        _ui.Fill(b, new Rectangle(SkillsX, treeTop - UiMetrics.Space(14), LibW, 1), Dim);
        if (TreeDefIn(known) is not { } treeDef)
        {
            _ui.TextBig(b, "SKILL TREE", SkillsX, treeTop, Slate, UiTypography.Secondary);
            var ty = treeTop + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
            foreach (var l in _ui.WrapBig("PICK A FILLED SLOT — ITS SKILL'S OWN TREE OPENS HERE", LibW, UiTypography.Body).Take(2))
            {
                _ui.TextBig(b, l, SkillsX, ty, UiInk.Empty, UiTypography.Body);
                ty += UiTypography.Pitch(UiTypography.Body);
            }
            return;
        }
        var chosen = SkillLevels.VariationOf(treeDef);
        var free = SkillLevels.FreeOn(treeDef.Id);
        var level = SkillLevels.LevelOf(treeDef.Id);
        var uses = SkillLevels.UsesOf(treeDef.Id);
        var headIco = new Rectangle(SkillsX, treeTop, TreeIcon, TreeIcon);
        _ui.Icon(b, $"icon_skill_{treeDef.Id}", headIco, Gold);
        var headX = headIco.Right + UiMetrics.Space(14);
        var headRoom = SkillsX + LibW - headX;
        _ui.TextBig(b, _ui.ShortenBig($"{treeDef.Name} — SKILL TREE", headRoom, UiTypography.Headline), headX, treeTop, Bone, UiTypography.Headline);
        var lvl = level >= SkillProgress.MaxLevel ? $"LEVEL {level} · MAX" : $"LEVEL {level} · {uses}/{SkillProgress.UsesForLevel(level + 1)} WAVES TO THE NEXT";
        if (free > 0) lvl += $"  ·  +{free} TO SPEND";
        _ui.TextBig(b, _ui.ShortenBig(lvl, headRoom, UiTypography.Secondary), headX, treeTop + UiTypography.Pitch(UiTypography.Headline) - UiMetrics.Space(4), free > 0 ? Gold : Slate, UiTypography.Secondary);
        Tip(In(new Rectangle(SkillsX, treeTop, LibW, TreeIcon + UiMetrics.Space(12)), region), hit, "A skill levels by being used: clear waves with it equipped. The first level chooses a variation — and the variation is what gives the skill its Source. The next levels buy that variation's reinforcements. Respec is free.");

        // The rails from the skill down to the two variations.
        var railY = RailY;
        _ui.Fill(b, new Rectangle(headIco.Center.X - 1, headIco.Bottom, 2, railY - headIco.Bottom), Dim);
        _ui.Fill(b, new Rectangle(VarCard(0).Center.X, railY, VarCard(1).Center.X - VarCard(0).Center.X, 2), Dim);
        var cardPad = UiMetrics.Space(12);
        for (var vi = 0; vi < treeDef.Variations.Count && vi < 2; vi++)
        {
            var v = treeDef.Variations[vi];
            var card = VarCard(vi);
            var shown = In(card, region);
            _ui.Fill(b, new Rectangle(card.Center.X - 1, railY, 2, card.Y - railY), Dim);
            var taken = chosen?.Name == v.Name;
            var other = chosen is not null && !taken;
            var on = _pick == Pick.Variation && _pickIndex == vi;
            var over = shown.Contains(hit);
            var col = SourceColor.GetValueOrDefault(v.Source, Bone);
            _ui.Fill(b, card, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.85f);
            Outline(b, card, on ? Bone : taken ? Gold : Dim, on || taken ? 2 : 1);
            if (taken) _ui.Fill(b, new Rectangle(card.X, card.Y, 5, card.Height), Gold);
            var gem = new Rectangle(card.X + UiMetrics.Space(16), card.Y + cardPad, UiMetrics.IconSize, UiMetrics.IconSize);
            _ui.Diamond(b, new Rectangle(gem.X - 3, gem.Y - 3, gem.Width + 6, gem.Height + 6), col * (other ? 0.12f : 0.30f));
            if (_ui.Assets.Get($"source_{v.Source.ToString().ToLowerInvariant()}") is { } gg) b.Draw(gg, gem, other ? Color.White * 0.55f : Color.White);
            var textX = gem.Right + cardPad;
            var nameY = card.Y + UiMetrics.Space(10);
            _ui.TextBig(b, v.Name.ToUpperInvariant(), textX, nameY, taken ? Gold : other ? Slate : Bone, UiTypography.Body);
            _ui.TextBig(b, $"{SourceName(v.Source)}{(taken ? " · CHOSEN" : "")}", textX, nameY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(4), other ? Slate : col, UiTypography.Secondary);
            _ui.TextBig(b, _ui.ShortenBig(v.Line.ToUpperInvariant(), card.Width - cardPad * 2, UiTypography.Caption), card.X + cardPad, card.Bottom - UiMetrics.Space(8) - UiTypography.Caption - 2, Slate, UiTypography.Caption);
            Tip(shown, hit, $"{v.Name} · {SourceName(v.Source)} — {v.Line} {BuildGlossary.SourceLine(v.Source)}.");

            // Its three reinforcements: under a taken fork they are buyable; under the other, a road not taken (readable, not dim).
            for (var ri = 0; ri < v.Reinforcements.Count && ri < 3; ri++)
            {
                var r = v.Reinforcements[ri];
                var chip = ReinfChip(vi, ri);
                var shownChip = In(chip, region);
                var owned = taken && SkillLevels.HasReinforcement(treeDef.Id, r.Name);
                var can = taken && !owned && free > 0;
                var onR = _pick == Pick.Reinforcement && taken && _pickIndex == ri;
                var overR = shownChip.Contains(hit);
                _ui.Fill(b, chip, onR ? new Color(0x2C, 0x25, 0x44) : overR ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.7f);
                Outline(b, chip, onR ? Bone : owned ? Met : Dim, onR || owned ? 2 : 1);
                var chipPad = UiMetrics.Space(10);
                _ui.TextBig(b, r.Name.ToUpperInvariant(), chip.X + chipPad, chip.Y + (chip.Height - UiTypography.Secondary) / 2, owned ? Met : can ? Gold : taken ? Bone : Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, owned ? "OWNED" : can ? "READY" : taken ? $"LEVEL {level + 1}" : "", chip.Right - chipPad, chip.Y + (chip.Height - UiTypography.Caption) / 2, owned ? Met : can ? Gold : Slate, UiTypography.Caption);
                Tip(shownChip, hit, $"{r.Name} — {r.Line}");
            }
        }
    }

    // ── THE INSPECTOR (D3) ──────────────────────────────────────────────────────────────────────────────
    private void DrawInspector(SpriteBatch b, Point hit)
    {
        var panel = InspectorPanel;
        _ui.PanelQuiet(b, panel);
        // The body is a scrolling region (brief §18: long inspectors) between the title row and the refusal
        // line, clipped so a long selection — a slot with a vow, its region matchup, its waiting gear — scrolls
        // into reach instead of being cut, and CHANGE VOW is never a button the profile hid. The refusal, the two
        // text actions and the one primary button stay anchored under it.
        var region = InsRegion;
        var x = InsX; var w = InsBodyW;
        var scroll = _insScroll;   // the scroll this frame is drawn at — a reveal may move _insScroll for the next
        var y = InsBodyTop - scroll;
        var known = KnownSkills();
        var skills = Loadout.Skills;
        var ctx = Context;
        _changeVowY = 0;
        _changeVowShown = false;
        _respecShown = false;

        void Head(string s) { _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary); y += UiTypography.Pitch(UiTypography.Secondary); }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            // The line cap is written for the 100 % column. Bigger type in the same column (less the scrollbar's
            // lane) needs more lines to say the same thing, so the cap grows with the type: the text wraps
            // further instead of ending mid-sentence (brief §9), and at 100 % the cap is exactly what it was.
            var cap = (int)MathF.Ceiling(maxLines * UiMetrics.TextScale * InsW / (float)w);
            foreach (var l in _ui.WrapBig(s, w, px).Take(cap))
            {
                _ui.TextBig(b, l, x, y, c, px); y += UiTypography.Pitch(px);
            }
        }
        void Gap() { y += UiMetrics.Space(10); }
        void Rule() { _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(4), w, 1), Dim); y += UiTypography.HairlineGap; }
        void Pair(string k, string v)
        {
            _ui.TextBig(b, k, x, y, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, _ui.ShortenBig(v, w - UiMetrics.Text(120), UiTypography.Body), x + w, y - 2, Bone, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        BeginClip(b, region);
        Body();
        var contentH = y + scroll - InsBodyTop + UiMetrics.Space(8);
        _insOverflow = Math.Max(0, contentH - region.Height);
        _insScroll = Math.Clamp(_insScroll, 0, _insOverflow);
        EndClip(b);
        _ui.ScrollBar(b, ScrollTrack(InsX + InsW, region), _insScroll, region.Height, region.Height + _insOverflow);

        // ── The actions: a refusal line, one primary button, two text actions. ───────────────────────
        var (label, enabled, refusal) = Primary();
        if (refusal.Length > 0)
            _ui.TextBig(b, _ui.ShortenBig(refusal, InsW, UiTypography.Secondary), InsX, RefusalY, Ember, UiTypography.Secondary);
        var actionTextY = (ActionRowH - UiTypography.Secondary) / 2;
        if (_respecShown)
        {
            var over = RespecText.Contains(hit);
            _ui.TextBig(b, "RESPEC — FREE", RespecText.X, RespecText.Y + actionTextY, over ? Bone : Slate, UiTypography.Secondary);
            Tip(RespecText, hit, "Give this skill's levels back. Free, and it keeps every level it has earned.");
        }
        {
            var over = CopyText.Contains(hit);
            _ui.TextRightBig(b, "COPY BUILD CODE", CopyText.Right, CopyText.Y + actionTextY, over ? Bone : Slate, UiTypography.Secondary);
            Tip(CopyText, hit, "Copies this build as a code. A friend pastes it in the VAULT.");
        }
        if (label.Length > 0)
            _ui.Button(b, PrimaryBtn, label, hit, false, enabled, enabled ? ButtonStyle.Primary : ButtonStyle.Secondary);

        void Body()
        {
            SkillDef? def = null;
            switch (_pick)
            {
                case Pick.Library: def = SkillCatalogue.Find(_pickSkillId); break;
                case Pick.Keystone: break;
                default: def = SlotDef(_slot); break;
            }

            if (_pick == Pick.Keystone)
            {
                var k = DustEffects.LearnedKeystones(Tree).FirstOrDefault(ks => ks.Id == _pickKeystoneId);
                if (k is null) { _pick = Pick.Slot; return; }
                Head("KEYSTONE");
                _ui.TextBig(b, k.Name.ToUpperInvariant(), x, y, Loadout.HasKeystone(k.Id) ? Gold : Bone, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Head("WHAT IT DOES"); Line(k.Blurb, Bone, UiTypography.Body, 6); Gap();
                Head("CURRENT STATE"); Line(Loadout.HasKeystone(k.Id) ? "IN USE" : "NOT IN USE", Bone);
                Line($"SOCKETS {Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity} — MORE ON THE TRAITS SCREEN", Slate, UiTypography.Secondary);
                return;
            }
            if (def is null || (_pick == Pick.Slot && !known.Contains(def.Id)))
            {
                Head(_slot < skills.Count ? $"SLOT {_slot + 1}" : "NOTHING SELECTED");
                _ui.TextBig(b, "EMPTY", x, y, UiInk.Empty, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Line("Pick a skill from the library and press EQUIP. A skill is learned on the MASTERY tree; once learned it is yours for good.", Slate);
                return;
            }

            var have = known.Contains(def.Id);
            var chosen = SkillLevels.VariationOf(def);
            var free = SkillLevels.FreeOn(def.Id);
            var level = SkillLevels.LevelOf(def.Id);
            var slotOf = skills.ToList().FindIndex(s => s.SkillId == def.Id);

            if (_pick == Pick.Variation && _pickIndex < def.Variations.Count)
            {
                var v = def.Variations[_pickIndex];
                var col = SourceColor.GetValueOrDefault(v.Source, Bone);
                Head($"VARIATION OF {def.Name.ToUpperInvariant()}");
                _ui.TextBig(b, v.Name.ToUpperInvariant(), x, y, chosen?.Name == v.Name ? Gold : Bone, UiTypography.Headline);
                _ui.TextRightBig(b, SourceName(v.Source), x + w, y + UiMetrics.Space(6), col, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Line($"{v.Line} {BuildGlossary.SourceLine(v.Source)}.", Bone); Gap(); Rule();
                Head("WHAT IT BUYS NEXT");
                foreach (var r in v.Reinforcements.Take(3)) Line($"{r.Name.ToUpperInvariant()} — {r.Line}", Bone, UiTypography.Body, 2);
                Gap(); Rule();
                Head("YOU NEED FIRST");
                Line(chosen is null
                        ? (free > 0 ? $"ONE FREE LEVEL — YOU HAVE {free}" : $"LEVEL 1 — {WavesToNext(def)} MORE WAVES WITH {def.Name.ToUpperInvariant()} EQUIPPED")
                        : chosen.Name == v.Name ? "CHOSEN — ITS REINFORCEMENTS ARE OPEN" : $"{def.Name.ToUpperInvariant()} IS {chosen.Name.ToUpperInvariant()}. RESPEC TO CHANGE — FREE.",
                     free > 0 && chosen is null ? Gold : Bone);
                _respecShown = chosen is not null || SkillLevels.SpentOn(def.Id) > 0;
                return;
            }
            if (_pick == Pick.Reinforcement && chosen is { } cv && _pickIndex < cv.Reinforcements.Count)
            {
                var r = cv.Reinforcements[_pickIndex];
                var owned = SkillLevels.HasReinforcement(def.Id, r.Name);
                Head($"REINFORCEMENT OF {def.Name.ToUpperInvariant()} · {cv.Name.ToUpperInvariant()}");
                _ui.TextBig(b, r.Name.ToUpperInvariant(), x, y, owned ? Met : Bone, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Head("WHAT IT DOES"); Line(r.Line, Bone); Gap(); Rule();
                Head("YOU NEED FIRST");
                Line(owned ? "OWNED" : free > 0 ? $"ONE FREE LEVEL — YOU HAVE {free}" : $"LEVEL {level + 1} — {WavesToNext(def)} MORE WAVES WITH {def.Name.ToUpperInvariant()} EQUIPPED", owned ? Met : free > 0 ? Gold : Bone);
                _respecShown = true;
                return;
            }

            // A skill: from a slot, or from the library.
            Head($"SKILL · {StyleName(def.Style)} · {(def.TakesABeat ? "ACTIVE" : "PASSIVE")}{(slotOf >= 0 ? $" · SLOT {slotOf + 1}" : "")}");
            var ico = new Rectangle(x, y, TreeIcon, TreeIcon);
            _ui.Icon(b, $"icon_skill_{def.Id}", ico, have ? Gold : Slate);
            _ui.TextBig(b, _ui.ShortenBig(def.Name.ToUpperInvariant(), x + w - ico.Right - UiMetrics.Space(12), UiTypography.Headline), ico.Right + UiMetrics.Space(12), y + UiMetrics.Space(8), Bone, UiTypography.Headline);
            y += TreeIcon + UiMetrics.Space(8);
            Line(def.Line, Bone); Gap(); Rule();
            Head("WHAT IT DOES");
            if (chosen is null)
            {
                foreach (var v in def.Variations.Take(2)) Line($"{v.Name.ToUpperInvariant()} · {SourceName(v.Source)} — {v.Line}", Bone, UiTypography.Body, 2);
                if (have) Line("A VARIATION IS CHOSEN IN THE SKILL TREE — IT GIVES THE SKILL ITS SOURCE.", Slate, UiTypography.Secondary, 2);
            }
            else
            {
                Line($"{chosen.Name.ToUpperInvariant()} · {SourceName(chosen.Source)} — {chosen.Line}", Bone, UiTypography.Body, 2);
                foreach (var r in chosen.Reinforcements.Where(r => SkillLevels.HasReinforcement(def.Id, r.Name))) Line($"{r.Name.ToUpperInvariant()} — {r.Line}", Met, UiTypography.Body, 2);
            }
            Gap(); Rule();
            if (!have)
            {
                Head("YOU NEED FIRST");
                var lockEdge = UiMetrics.Control(20);
                _ui.Icon(b, "ui_slot_locked", new Rectangle(x, y + 2, lockEdge, lockEdge), Bone);
                _ui.TextBig(b, _ui.ShortenBig($"LEARNED ON {StyleName(def.Style)}'S ROAD, ON THE MASTERY TREE", w - lockEdge - UiMetrics.Space(8), UiTypography.Body), x + lockEdge + UiMetrics.Space(8), y, Bone, UiTypography.Body); y += UiTypography.Pitch(UiTypography.Body);
            }
            else
            {
                Head("CURRENT STATE");
                Line(level >= SkillProgress.MaxLevel ? $"LEVEL {level} · MAX" : $"LEVEL {level} · {SkillLevels.UsesOf(def.Id)}/{SkillProgress.UsesForLevel(level + 1)} WAVES TO THE NEXT{(free > 0 ? $" · +{free} TO SPEND" : "")}", free > 0 ? Gold : Bone);
                if (ChosenStyle is { } dd)
                {
                    var f = StyleAffinity.Factor(dd, def.Style, vowSworn: slotOf >= 0 && skills[slotOf].VowId is not null);
                    Line(f >= 1.99f ? $"YOUR STYLE IS {StyleName(dd)} — THIS SKILL HITS x2.0" : $"YOUR STYLE IS {StyleName(dd)} — THIS {StyleName(def.Style)} SKILL HITS x{f:0.0#}", f >= 1.99f ? Gold : Slate, UiTypography.Secondary, 2);
                }
                else Line("NO STYLE CHOSEN YET — A SPECIALISATION NODE ON THE MASTERY TREE CHOOSES ONE.", Slate, UiTypography.Secondary, 2);
                if (_pick == Pick.Library && slotOf >= 0 && slotOf != _slot)
                    Line($"EQUIPPED · SLOT {slotOf + 1} — A SKILL GOES IN ONE SLOT", Gold, UiTypography.Secondary);
                else if (_pick == Pick.Library && _slot < skills.Count && skills[_slot].SkillId != def.Id && SkillCatalogue.Find(skills[_slot].SkillId) is { } replacing)
                    Line($"EQUIP REPLACES {replacing.Name.ToUpperInvariant()} IN SLOT {_slot + 1}", Slate, UiTypography.Secondary);
            }
            _respecShown = slotOf >= 0 && have && SkillLevels.SpentOn(def.Id) > 0;

            // THE VOW — the validator block, on the slot's own skill.
            if (_pick == Pick.Slot && slotOf >= 0 && have)
            {
                Gap(); Rule();
                var vow = Vows.ById(skills[slotOf].VowId);
                if (_vowListOpen) { DrawVowList(b, hit, ref y, region); }
                else
                {
                    Head("VOW");
                    if (vow is null)
                    {
                        Line(Known.Count == 0 ? "NO VOW ON THIS SLOT — VOWS ARE LEARNED ON THE TRAITS SCREEN" : "NO VOW ON THIS SLOT", Slate);
                    }
                    else
                    {
                        var live = Vows.IsActive(vow, ctx);
                        _ui.TextBig(b, vow.Name.ToUpperInvariant(), x, y, live ? Gold : Bone, UiTypography.Body);
                        _ui.TextRightBig(b, $"x{Vows.Multiplier(vow):0.00}", x + w, y, live ? Gold : Slate, UiTypography.Body);
                        y += UiTypography.Pitch(UiTypography.Body);
                        Pair("DEMAND", DemandText(vow));
                        Pair("YOUR BUILD", YourBuildText(vow, ctx));
                        Line(live ? "HOLDS — THE VOW PAYS" : $"BROKEN — IT PAYS NOTHING UNTIL {DemandText(vow)}", live ? Met : Ember, UiTypography.Body, 2);
                    }
                    if (Known.Count > 0)
                    {
                        _changeVowY = y + UiMetrics.Space(4);
                        _changeVowShown = true;
                        var btn = ChangeVowBtn(_changeVowY);
                        var overBtn = In(btn, region).Contains(hit);
                        _ui.Fill(b, btn, overBtn ? new Color(0x2C, 0x25, 0x44) : Quiet);
                        Outline(b, btn, overBtn ? Bone : Dim, 1);
                        _ui.TextCenterBig(b, vow is null ? "BIND A VOW" : "CHANGE VOW", btn.Center.X, btn.Y + (btn.Height - UiTypography.Body) / 2, overBtn ? Bone : Slate, UiTypography.Body);
                        y = btn.Bottom + UiMetrics.Space(8);
                    }
                }
            }

            // WHERE YOU ARE HUNTING — the skill's Source against the creatures that live there.
            if (!_vowListOpen && RegionId.Length > 0 && chosen is not null && BandCycles.RosterFor(RegionId) is { Count: > 0 } roster)
            {
                Gap(); Rule();
                Head($"{def.Name.ToUpperInvariant()}'S {SourceName(chosen.Source)} IN {(RegionName.Length > 0 ? RegionName : RegionId).ToUpperInvariant()}");
                foreach (var enemy in roster.Distinct().Take(3))
                {
                    var mult = SourceMatchup.Effectiveness(chosen.Source, enemy);
                    var verdict = mult > 1.01f ? "STRONG" : mult < 0.99f ? "WEAK" : "EVEN";
                    _ui.TextBig(b, SourceName(enemy), x, y, SourceColor.GetValueOrDefault(enemy, Bone), UiTypography.Body);
                    _ui.TextRightBig(b, $"{verdict}  x{mult:0.00}", x + w, y, mult > 1.01f ? Met : mult < 0.99f ? Ember : Slate, UiTypography.Body);
                    y += UiTypography.Pitch(UiTypography.Body);
                }
            }
            // WHAT YOUR GEAR IS WAITING FOR.
            if (!_vowListOpen && Hunter is { } h)
            {
                var wants = h.WornEnchantments
                    .Where(e => e.Needs is { Keystone: null, AnyVow: false } n && !n.MetBySkills(Loadout.EquippedDefs()))
                    .Select(e => (e.Needs!.Label, e.Name)).DistinctBy(t => t.Item1).Take(2).ToList();
                if (wants.Count > 0) { Gap(); Rule(); Head("YOUR GEAR IS WAITING FOR"); foreach (var (want, ench) in wants) Line($"{want} — {ench.ToUpperInvariant()}", Met, UiTypography.Body, 1); }
            }
        }
    }

    /// <summary>The known vows, in place of the sections below the validator: click one to bind it to the selected slot. Every known vow is a row — the body scrolls, so the list no longer pages.</summary>
    private void DrawVowList(SpriteBatch b, Point hit, ref int y, Rectangle region)
    {
        var known = Known;
        var ctx = Context;
        var skills = Loadout.Skills;
        var sworn = _slot < skills.Count ? skills[_slot].VowId : null;
        var w = InsBodyW;
        _ui.TextBig(b, $"BIND TO SLOT {_slot + 1}", InsX, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, "CLICK ONE", InsX + w, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
        _vowListTop = y;
        var pad = UiMetrics.Space(12);
        // The right column — the multiplier over HOLDS / BROKEN — is measured, so the vow's name gets every
        // pixel the row can give it before it is shortened.
        var tail = Math.Max(_ui.MeasureBig("x0.00", UiTypography.Body), _ui.MeasureBig("BROKEN", UiTypography.Caption)) + pad * 2 + UiMetrics.Space(8);
        for (var r = 0; r <= known.Count; r++)
        {
            var idx = r - 1;   // row 0 is NO VOW
            var row = VowListRow(r, _vowListTop);
            var shown = In(row, region);
            var over = shown.Contains(hit);
            var nameY = row.Y + UiMetrics.Space(4);
            var subY = nameY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(6);
            if (idx < 0)
            {
                _ui.Fill(b, row, over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
                Outline(b, row, sworn is null ? Gold : over ? Slate : Dim, sworn is null ? 2 : 1);
                _ui.TextBig(b, "NO VOW", row.X + pad, row.Y + (row.Height - UiTypography.Body) / 2, sworn is null ? Gold : Bone, UiTypography.Body);
                continue;
            }
            var v = known[idx];
            var live = Vows.IsActive(v, ctx);
            var on = v.Id == sworn;
            _ui.Fill(b, row, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            Outline(b, row, on ? Gold : over ? Slate : Dim, on ? 2 : 1);
            _ui.TextBig(b, _ui.ShortenBig(v.Name.ToUpperInvariant(), row.Width - tail, UiTypography.Body), row.X + pad, nameY, on ? Gold : Bone, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig(DemandText(v), row.Width - tail, UiTypography.Caption), row.X + pad, subY, Slate, UiTypography.Caption);
            _ui.TextRightBig(b, $"x{Vows.Multiplier(v):0.00}", row.Right - pad, nameY, live ? Gold : Slate, UiTypography.Body);
            _ui.TextRightBig(b, live ? "HOLDS" : "BROKEN", row.Right - pad, subY, live ? Met : Ember, UiTypography.Caption);
            Tip(shown, hit, v.Description);
        }
        y = _vowListTop + (known.Count + 1) * VowRowPitch - UiMetrics.Space(4);
        _changeVowY = y + UiMetrics.Space(4);
        _changeVowShown = true;
        var btn = ChangeVowBtn(_changeVowY);
        var overBtn = In(btn, region).Contains(hit);
        _ui.Fill(b, btn, overBtn ? new Color(0x2C, 0x25, 0x44) : Quiet);
        Outline(b, btn, Dim, 1);
        _ui.TextCenterBig(b, "CLOSE", btn.Center.X, btn.Y + (btn.Height - UiTypography.Body) / 2, overBtn ? Bone : Slate, UiTypography.Body);
        y = btn.Bottom + UiMetrics.Space(8);
        if (_vowListReveal > 0 && --_vowListReveal == 0)
        {
            // The list just opened where the VOW block was: scroll the body just enough that CLOSE is on
            // screen, but never so far that the list's own head leaves the top. Takes effect next frame.
            var headY = _vowListTop - UiTypography.Pitch(UiTypography.Secondary) - UiMetrics.Space(4);
            var need = Math.Clamp(y - region.Bottom, 0, Math.Max(0, headY - region.Y));
            _insScroll += need;
        }
    }

    // ── EFFECTS ─────────────────────────────────────────────────────────────────────────────────────────
    private void DrawCarried(SpriteBatch b)
    {
        if (_carrying == Carry.None || !_carryMoved) return;
        var p = _carryAt;
        if (_carrying == Carry.Slot && _carrySlot >= 0 && _carrySlot < Loadout.Skills.Count)
        {
            var s = Loadout.Skills[_carrySlot];
            var col = SourceColor.GetValueOrDefault(s.Source, Bone);
            var ghostW = UiMetrics.Text(180);   // a name-sized ghost, one chip tall
            var r = new Rectangle(p.X - ghostW / 2, p.Y - ChipH / 2, ghostW, ChipH);
            _ui.Fill(b, new Rectangle(r.X + 4, r.Y + 5, r.Width, r.Height), new Color(0, 0, 0) * 0.45f);
            _ui.Fill(b, r, new Color(0x2C, 0x25, 0x44));
            _ui.Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), col);
            Outline(b, r, Bone, 2);
            var pad = UiMetrics.Space(16);
            _ui.TextBig(b, _ui.ShortenBig(SkillCatalogue.Find(s.SkillId)?.Name ?? "EMPTY", r.Width - pad * 2, UiTypography.Body), r.X + pad, r.Y + (r.Height - UiTypography.Body) / 2, Bone, UiTypography.Body);
        }
    }

    /// <summary>A chain closing on the slot's skill glyph — the flourish a bound Vow plays over its row.</summary>
    private void DrawBindChain(SpriteBatch b, Rectangle row, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var played = 1f - t;
        _ui.Fill(b, row, Gold * (0.22f * t * t));
        var gem = GlyphRect(row);
        var wide = 46f * t * t;
        var box = new Rectangle((int)(gem.X - wide), (int)(gem.Y - wide), (int)(gem.Width + wide * 2), (int)(gem.Height + wide * 2));
        var alpha = Math.Clamp(t * 1.9f, 0f, 1f);
        if (!_ui.AnimSprite(b, "fx_bind_chain_strip8_512", box, played * ChainClipSeconds, 8f, loop: false, Color.White * alpha))
            Outline(b, row, Gold * alpha, 3);
    }

    private void TickEffects()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = (float)Math.Clamp(now - _lastTick, 0.0, 0.10);
        _lastTick = now;
        Decay(_slotFlash, dt);
        if (!_devHoldChain) Decay(_bindFlash, dt);
        static void Decay(Dictionary<int, float> d, float dt)
        {
            if (d.Count == 0) return;
            foreach (var k in d.Keys.ToList()) { var left = d[k] - dt; if (left <= 0f) d.Remove(k); else d[k] = left; }
        }
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
