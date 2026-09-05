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
/// and reinforcements, the keystones, and the Vows the build has sworn — edited as master-detail.
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
    // THE STANDARD STATES of a custom-drawn cell (brief §23, §25–§28). SELECTED is a persistent fill under
    // the gold outline. HOVER is a lavender lift EASED on top of whatever fill the cell already has — never a
    // fill swap, so a hover reads on a selected cell too and the two are never confused. PRESSED darkens the
    // whole cell for exactly as long as the mouse is down, the acknowledgement UiKit.Button already gives its
    // own face. The tile / card / chip surfaces are the quiet plates they always were, named instead of
    // repeated at six call sites.
    private static readonly Color Selected = new(0x2C, 0x25, 0x44);
    private static readonly Color HoverWash = new Color(0x8A, 0x7A, 0xC0) * 0.22f;
    private static readonly Color PressWash = Color.Black * 0.30f;
    private static readonly Color PressLip = Color.Black * 0.40f;
    private static readonly Color TileBg = new Color(0x0E, 0x0B, 0x16) * 0.8f;
    private static readonly Color CardBg = new Color(0x0E, 0x0B, 0x16) * 0.85f;
    private static readonly Color ChipBg = new Color(0x0E, 0x0B, 0x16) * 0.7f;

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

    // ── FEEDBACK (UI polish P3–P6: brief §22–§38, §39–§44). Every change the player makes is answered AT
    // THE THING THAT CHANGED: an equip pulses the slot it landed in; a chosen variation brightens its branch
    // of the tree; a reinforcement taken pulses its chip; the inspector's content fades in when the selection
    // changes; a bound Vow plays its chain. Every one-shot is armed ONCE, from Update, at the moment the model
    // changed — Draw only reads a phase, so drawing can never re-arm anything and nothing here loops.
    //
    // RESPEC STAYS CALM (§44). It is free and reversible and it says what it did in words; a flash would be
    // claiming an event the player did not have.
    //
    // KEYS ARE SEMANTIC — the slot, the skill's branch, the chip — not rectangles. A list that scrolls while a
    // pulse is running moves the rectangle out from under it, and the pulse would be orphaned mid-play. Hover,
    // which lives and dies inside one frame's geometry, is keyed by rect like every other screen's.
    //
    // REDUCED MOTION is UiMotion's contract, not a second implementation here: eases collapse to their target,
    // and one-shots stay, because a pulse is the short fade §32 keeps. Either way the END STATE is the same.
    private static int SlotKey(int slot) => HashCode.Combine("build.slot", slot);
    private static int BindKey(int slot) => HashCode.Combine("build.bind", slot);
    private static int BranchKey(string skillId, int vi) => HashCode.Combine("build.branch", skillId, vi);
    private static int ReinfKey(string skillId, int vi, int ri) => HashCode.Combine("build.reinf", skillId, vi, ri);
    /// <summary>The inspector's content fade. One key, because one inspector is on screen (§35).</summary>
    private static readonly int InsFadeKey = HashCode.Combine("build.inspector");
    /// <summary>How much of the chain strip the flourish's phase is mapped over — see <see cref="DrawBindChain"/>.</summary>
    private const float ChainClipSeconds = 0.55f;
    /// <summary>What the inspector is about, folded to one value; when it changes, the content fades in (§35).</summary>
    private int _insSignature;
    /// <summary>The inspector body's ink alpha this frame — read once per Draw, applied by <see cref="Ins"/>.</summary>
    private float _insAlpha = 1f;
    /// <summary>The sound cue waiting for the host — set at the semantic moment, cleared by <see cref="ConsumeCue"/>.</summary>
    private string? _cue;
    /// <summary>DEV: the bind chain held at this phase (1 = just bound, 0 = done) for the `vow` capture; null when it plays.</summary>
    private float? _devChainAt;

    /// <summary>
    /// DEV: <c>RH_SHOT_BUILD_FX=&lt;t&gt;</c> holds the change feedback at phase <c>t</c> (1 = the instant it fired,
    /// 0 = settled), so a still frame can prove it draws: the selected slot's equip pulse, its chosen variation's
    /// branch brightening, that branch's first reinforcement chip, and the inspector part-faded — all at once,
    /// each on the rectangle it would really play on. Only the CLOCK is the dial's; the drawing is the shipped
    /// path. Without it the rig shoots frame 60, a full second after a 180 ms pulse has ended.
    ///
    ///   RH_SHOT_BUILD_FX=1 bash tools/asset-pipeline/capture.sh weave build/shots/p2_loadout_fx_100.png
    /// </summary>
    private static readonly float? DevFxPose = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_BUILD_FX"),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fx) ? Math.Clamp(fx, 0f, 1f) : null;

    /// <summary>
    /// DEV: <c>RH_SHOT_BUILD_HOLD=1</c> counts the mouse as held, so whatever <c>RH_SHOT_PAGE_MOUSE</c> is over
    /// is photographed in its PRESSED state. The rig has no way to hold a mouse button down.
    /// </summary>
    private static readonly bool DevHold = Environment.GetEnvironmentVariable("RH_SHOT_BUILD_HOLD") == "1";

    /// <summary>
    /// DEV: <c>RH_SHOT_BUILD_PICK=&lt;skillId&gt;</c> selects that library skill exactly as a click on its tile
    /// would. Point it at a skill the fixture already wears in another slot and the screen poses the refusal
    /// — ALREADY EQUIPPED IN SLOT N with its reason line (brief §4, LAW 13); add RH_SHOT_BUILD_HOLD and a
    /// cursor on the button and the refused click's PRESSED state is photographable too.
    /// </summary>
    private static readonly string? DevPick = Environment.GetEnvironmentVariable("RH_SHOT_BUILD_PICK") is { Length: > 0 } dp ? dp : null;

    /// <summary>The mouse is down — the PRESSED state's condition, posed by <see cref="DevHold"/> for a capture.</summary>
    private static bool MouseDown => UiKit.MouseHeld || DevHold;

    /// <summary>Is the cell the cursor is over being pressed right now?</summary>
    private static bool Down(bool over) => over && MouseDown;

    /// <summary>
    /// A custom cell's eased HOVER lift, 0 → 1 over <see cref="UiMotion.Fast"/> — the same call
    /// <see cref="UiKit.Button"/>, MapScreen and RosterScreen make, keyed by the cell's own rectangle.
    /// </summary>
    private static float Lift(Rectangle r, bool over) => UiMotion.Ease(UiMotion.KeyOf(r), over ? 1f : 0f);

    /// <summary>The phase of a one-shot — or the phase the capture dial holds it at, for a posed target.</summary>
    private static float Phase(int key, bool posed) => DevFxPose is { } t && posed ? t : UiMotion.Pulse(key);

    /// <summary>
    /// THE THREE STATES every custom-drawn row, tile, card and chip on this screen wears, in one method so
    /// they cannot drift apart: the surface it rests on, SELECTED as a persistent fill, HOVER eased on top of
    /// that (so a hover reads on a selected cell and is never mistaken for one), PRESSED as a darkening for
    /// as long as the mouse is down. DISABLED is not a fill — it is the cell's own words, and every disabled
    /// cell here says why in one plain line (§29).
    /// </summary>
    private void Cell(SpriteBatch b, Rectangle r, Color surface, bool selected, bool over)
    {
        // THE HOUSE PLATE under every cell (release polish 2026-09-05, build-06): rows, tiles, cards and
        // chips were four hand-mixed fills, some hair-lined and some not — one tier, four looks. The
        // tint argument is history; the Selected wash, the hover lift and the press lip go on top.
        _ui.Plate(b, r);
        if (selected) _ui.Fill(b, r, Selected);
        var down = Down(over);
        // §27: PRESSED is a depression with REDUCED glow — so the hover's lift comes OFF while the mouse is
        // down and the cell darkens under a lip at its top edge. Without dropping the lift, pressed was a
        // 20 % shade of hover: a difference a meter can find and an eye cannot.
        var lift = Lift(r, over);
        if (lift > 0f && !down) _ui.Fill(b, r, HoverWash * lift);
        if (down)
        {
            _ui.Fill(b, r, PressWash);
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), PressLip);
        }
    }

    /// <summary>An inspector-body ink at this frame's content-fade alpha (§35: the content fades, the frame stays).</summary>
    private Color Ins(Color c) => c * _insAlpha;

    /// <summary>
    /// The inspector body's alpha. The content fades in over <see cref="UiMotion.Fast"/> whenever the
    /// selection changes — from a floor rather than from nothing, so the panel never blinks empty.
    /// </summary>
    private static float InspectorAlpha => 0.18f + 0.82f * UiMotion.Smooth(1f - (DevFxPose ?? UiMotion.Pulse(InsFadeKey)));

    // ── ARMING. One call per event, from Update, on the key that event belongs to. ───────────────
    private static void PulseSlot(int slot) => UiMotion.Flash(SlotKey(slot), UiMotion.Transition);
    private void BrightenBranch(int vi) { if (SlotDef(_slot) is { } d) UiMotion.Flash(BranchKey(d.Id, vi), UiMotion.Transition); }
    private void PulseReinforcement(int vi, int ri) { if (SlotDef(_slot) is { } d) UiMotion.Flash(ReinfKey(d.Id, vi, ri), UiMotion.Transition); }

    /// <summary>Which fork of the skill this variation is — the branch a reinforcement hangs from.</summary>
    private static int BranchOf(SkillDef def, SkillVariation v)
    {
        for (var i = 0; i < def.Variations.Count; i++)
            if (def.Variations[i].Name == v.Name) return i;
        return 0;
    }

    /// <summary>Cues, host-fed like every other screen's.</summary>
    public SoundBank? Sound { get; set; }

    /// <summary>
    /// The sound cue for the last refused click, cleared by reading — the host owns audio, this screen does not.
    /// </summary>
    /// <remarks>
    /// Same shape as <c>TraitsScreen.ConsumeCue</c>, so the host's Update reads one way everywhere. The only
    /// cue that comes this way is <c>sfx_error</c>, for a refused equip. The two cues this screen already
    /// owned (<c>sfx_weave</c> on an equip or a reorder, <c>sfx_bind</c> on a Vow bound) still go straight to
    /// the host-fed <see cref="Sound"/>, so nothing that works today waits on new wiring.
    /// </remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    public LoadoutScreen(UiKit ui) => _ui = ui;

    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MasteryTree Mastery { get; set; } = new();

    /// <summary>
    /// The keystones the WORLD has taught this account — set by the host every frame.
    /// </summary>
    /// <remarks>
    /// The screen used to read the trait tree directly. Keystones come from conquest, region mastery
    /// and the corruption now, so the menu is a fact about the world and the screen is handed it.
    /// </remarks>
    public IReadOnlyList<Keystone> DiscoveredKeystones { get; set; } = Array.Empty<Keystone>();

    /// <summary>
    /// Every shared skill the account has EVER reached — the host's discovery latch. A shared skill outside
    /// it is drawn as <c>???</c>: no name, no glyph, no line (release polish 2026-09-05, "gizem ve keşif").
    /// </summary>
    public IReadOnlySet<string> DiscoveredSkills { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// May this skill be NAMED on this screen? Its own signature always; a shared skill once it has been
    /// reached on the tree, or once the hunter has spent a wave on it (a used skill is never a stranger).
    /// </summary>
    private bool Seen(SkillDef def)
        => def.OwnerCharacterId is not null || KnownSkills().Contains(def.Id)
           || DiscoveredSkills.Contains(def.Id) || SkillLevels.UsesOf(def.Id) > 0;

    /// <summary>The ink of a thing not yet discovered — the TRAITS screen's own unknown ink, so the two screens agree.</summary>
    private static readonly Color Unknown = new(0x6A, 0x64, 0x80);

    /// <summary>The Vows the account has found, by keeping a rule once without them.</summary>
    public IReadOnlyList<Vow> KnownVows { get; set; } = Array.Empty<Vow>();

    /// <summary>What the world says about the next keystone socket, or "" when all three are open.</summary>
    public string NextSocketNote { get; set; } = "";

    /// <summary>What the world says about the next Vow, or "" at the last one.</summary>
    public string NextVowNote { get; set; } = "";
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
        if (chainAt > 0f) _devChainAt = Math.Clamp(chainAt, 0f, 1f);
    }

    /// <summary>DEV: kept for the fixtures that call it — the skill tree is always on screen now.</summary>
    public void DevOpenSkillTree() { }

    /// <summary>DEV: pick a skill in the LIBRARY, so the inspector reads that skill for a capture.</summary>
    /// <remarks>
    /// The reason this exists is the LOCKED reading (BRIEF sec.20). A locked skill's inspector and its
    /// primary button are reachable only by clicking a library tile, and a capture never clicks — so
    /// without this dial the one state the section is about could not be photographed at any profile.
    /// </remarks>
    public void DevPickLibrary(string skillId)
    {
        if (SkillCatalogue.Find(skillId) is not { } def) return;
        _pick = Pick.Library;
        _pickSkillId = def.Id;
        _vowListOpen = false;
    }

    // ── LAYOUT. Three columns to the page. The columns' WIDTHS are page shares and do not move with the
    // profile; everything inside a column is a density size read from UiMetrics (brief §7–§11) and laid out
    // from the height that is there — a column that no longer fits scrolls (§9, §18), it never overprints. ──
    private static int Top => UiKit.PageTop;   // the first row under the chrome band, at this profile
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
    // Tight to the frame's bottom rail: the bench used to keep 40 px of air under its figure while the
    // list above it had to scroll and clip a row at 125/150 % (release polish 2026-09-05, build-08).
    private static int BenchH => UiTypography.Pitch(UiTypography.Secondary) + UiTypography.PrimaryValue + UiMetrics.Space(8);
    private static Rectangle BenchBlock => new(LoadX, LoadoutPanel.Bottom - UiKit.PanelCorner - BenchH, LoadW, BenchH);
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
        var learned = DiscoveredKeystones.Count;
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
    // The column wears the quiet FRAME now (build-07), so its content grid is the frame's: the head at the
    // panel's title row, the library and tree inside the corner scrollwork.
    private static int SkillsX => UiKit.ContentLeft(SkillsPanel);
    private static int SkillsW => UiKit.ContentRight(SkillsPanel) - SkillsX;
    private static int SkillsHeadY => UiKit.TitleTop(SkillsPanel);
    /// <summary>Everything under the SKILLS head, down to the frame's bottom rail. Clipped to when it scrolls.</summary>
    private static Rectangle SkillsRegion
    {
        get
        {
            var top = SkillsHeadY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8);
            return new(SkillsPanel.X + UiKit.PanelCorner / 2, top, SkillsPanel.Width - UiKit.PanelCorner, SkillsPanel.Bottom - UiMetrics.Space(24) - top);
        }
    }
    private int _skillsScroll;
    private int _skillsOverflow;
    private int LibW => SkillsW - (_skillsOverflow > 0 ? ScrollLane : 0);
    private int LibTop => SkillsRegion.Y + UiMetrics.Space(6) - _skillsScroll;
    // A Body name over a Secondary state line with their pads fits 52 (6 + 28 + 19 = 53 with the
    // descender room the pitch already holds) — and the tree's reinforcement chips must stay reachable
    // without a scroll at 100 % (build-04; loadout_feedback_test pins it).
    private static int LibTileH => UiMetrics.Control(52);
    private static int LibRowPitch => LibTileH + UiMetrics.Space(8);
    /// <summary>The style label's column: ~80 px of ink at 100 % and the rest is the gap to the tiles, so it grows at the spacing rate and the tiles keep their width.</summary>
    private static int LibStyleW => UiMetrics.Space(118);
    private static int LibTileGap => UiMetrics.Space(12);

    /// <summary>
    /// This champion's own skill — the one no other champion may ever weave. Null when no champion is
    /// set (the tests and the first frames of a load), and then the library is the twelve shared alone.
    /// </summary>
    private SkillDef? Signature => Character?.SignatureSkillId is { } id ? SkillCatalogue.Find(id) : null;

    /// <summary>
    /// The room the SIGNATURE block takes above the six style rows: its own row, plus the rule that
    /// separates it from them. Zero when there is no signature to pin, so the twelve sit where they
    /// always did.
    /// </summary>
    /// <remarks>
    /// One more <see cref="LibRowPitch"/> and one spacing token — the same arithmetic the six rows are
    /// built from, so the block reflows with them at 125 % and 150 % instead of needing its own numbers.
    /// </remarks>
    private int SigBlockH => Signature is null ? 0 : LibRowPitch + UiMetrics.Space(12);

    /// <summary>
    /// The signature's tile: one row at the top of the library, across BOTH tile columns.
    /// </summary>
    /// <remarks>
    /// Full width on purpose (BRIEF sec.12: "do not hide it among twelve shared Skills without
    /// distinction"). A single tile in the left column would read as half of a style's pair, which is
    /// the one thing this row must not read as — it belongs to nobody's style and to one champion.
    /// </remarks>
    private Rectangle SigTile => new(SkillsX + LibStyleW, LibTop, LibW - LibStyleW, LibTileH);

    private Rectangle LibTile(int style, int which)
    {
        var w = (LibW - LibStyleW - LibTileGap) / 2;
        return new(SkillsX + LibStyleW + which * (w + LibTileGap), LibTop + SigBlockH + style * LibRowPitch, w, LibTileH);
    }
    private int TreeTop => LibTop + SigBlockH + StyleCount * LibRowPitch + UiMetrics.Space(14);
    private static int TreeIcon => UiMetrics.Control(48);
    private int RailY => TreeTop + TreeIcon + UiMetrics.Space(10);
    private static int VarCardH => UiMetrics.Control(112);   // room for the variation's sentence on two Secondary lines (build-03)
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
        var height = UiMetrics.Space(6) + SigBlockH + StyleCount * LibRowPitch + UiMetrics.Space(22);
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
    // Under the frame's corner scrollwork, not in it: at PanelTitleTop the CATEGORY line's first letters
    // sat on the top-left curl and the scroll track ran over the top rail (release polish 2026-09-05,
    // build-01). The body scrolls, so the rows are not lost.
    private static int InsBodyTop => UiKit.PanelInner(InspectorPanel).Y + UiMetrics.Space(6);
    /// <summary>
    /// The second line the refusal needs, when one line of this column cannot hold it. A DISABLED CONTROL
    /// SAYS WHY (§29) — and a reason that ends "… PICK THAT SLOT TO CH…" has not said it. The refusal wraps
    /// upward from its anchor and the BODY gives up the room, because the body scrolls and the reason does
    /// not. It cannot oscillate: the wrap depends only on the refusal's own words and the column's width,
    /// never on how tall the body came out.
    /// </summary>
    private int _refusalRise;
    /// <summary>The body's room: from the title row down to the refusal. The body is clipped to it when it scrolls.</summary>
    /// <summary>Whether the selection has a refusal to show — only then is the refusal's line reserved (build-09).</summary>
    private bool _refusalShown;
    // The refusal's room is reserved only when there IS a refusal: an always-reserved line was an 88 px
    // dead band above RESPEC / COPY at 150 % while the vow card sat out of view (release polish
    // 2026-09-05, build-09). The refusal depends only on the selection, so this cannot oscillate.
    private Rectangle InsRegion => new(InspectorPanel.X + UiKit.PanelCorner / 2, InsBodyTop, InspectorPanel.Width - UiKit.PanelCorner,
                                       (_refusalShown ? RefusalY - _refusalRise - UiMetrics.Space(4) : RespecText.Y - UiMetrics.Space(8)) - InsBodyTop);
    private int _insScroll;
    private int _insOverflow;
    /// <summary>The body's width — the content column less the scrollbar's lane while it scrolls. Wrapping follows, and the two states cannot flip-flop: text in the narrower column is never shorter.</summary>
    private int InsBodyW => InsW - (_insOverflow > 0 ? ScrollLane : 0);
    private Rectangle ChangeVowBtn(int y) => new(InsX, y, InsBodyW, UiMetrics.ButtonHeightSmall);
    /// <summary>A vow row's height: room for its seal beside two lines. 64 at 100 % (46 before the seals).</summary>
    private static int VowRowH => UiMetrics.Control(64);
    private static int VowRowPitch => VowRowH + UiMetrics.Space(4);
    private Rectangle VowListRow(int i, int top) => new(InsX, top + i * VowRowPitch, InsBodyW, VowRowH);

    // ── VOW SEALS (release polish 2026-09-05). A vow used to be a line of text in a list of lines of text;
    //    it is a SEAL now — the sigil the world stamped on the promise — with its state drawn on it. ────

    /// <summary>The asset key of a vow's seal: <c>icon_vow_singular</c> for <c>vow_singular</c>. Ten medallions were generated for the pass; a vow without one draws a diamond.</summary>
    private static string SealKey(Vow v) => "icon_" + v.Id;

    /// <summary>
    /// A vow's seal in a box: the gold medallion when the vow HOLDS, dimmed and CRACKED when it is broken —
    /// two ember strokes across the face — so the state is a picture before it is a word.
    /// </summary>
    private void VowSeal(SpriteBatch b, Vow v, Rectangle box, bool live, float alpha = 1f)
    {
        var tint = (live ? Color.White : new Color(0x8A, 0x84, 0x96)) * alpha;
        if (!_ui.Icon(b, SealKey(v), box, tint))
            _ui.Diamond(b, new Rectangle(box.X + box.Width / 6, box.Y + box.Height / 6, box.Width * 2 / 3, box.Height * 2 / 3), (live ? Gold : Slate) * alpha);
        if (live) return;
        // THE CRACK: a broken seal is a seal with a crack through it — one long stroke, one short.
        var c = box.Center.ToVector2();
        var r = box.Width * 0.36f;
        var thick = Math.Max(2f, box.Width / 32f);
        _ui.LineSeg(b, c + new Vector2(-r * 0.55f, -r), c + new Vector2(r * 0.15f, r * 0.05f), thick, Ember * alpha);
        _ui.LineSeg(b, c + new Vector2(r * 0.15f, r * 0.05f), c + new Vector2(-r * 0.2f, r), thick, Ember * alpha);
    }

    /// <summary>The seal of NO VOW: a dark empty medallion — the unknown seal, faint — where a promise could go.</summary>
    private void EmptySeal(SpriteBatch b, Rectangle box, float alpha = 1f)
    {
        if (!_ui.Icon(b, "icon_unknown_seal", box, Color.White * (0.55f * alpha)))
            _ui.Diamond(b, new Rectangle(box.X + box.Width / 6, box.Y + box.Height / 6, box.Width * 2 / 3, box.Height * 2 / 3), Dim * alpha);
    }

    /// <summary>
    /// A vow's RISK as four pips from its own <see cref="Vow.Severity"/> — the Core's measure of how much
    /// the demand costs a build — so a heavier promise reads heavier before its multiplier is read.
    /// </summary>
    private void RiskPips(SpriteBatch b, Vow v, int x, int y, int pip, Color on, Color off)
    {
        var lit = Math.Clamp((int)MathF.Ceiling(v.Severity * 4f - 0.01f), 1, 4);
        for (var i = 0; i < 4; i++)
            _ui.Diamond(b, new Rectangle(x + i * (pip + UiMetrics.Space(3)), y, pip, pip), i < lit ? on : off);
    }

    /// <summary>The width of four risk pips at this pip size, for laying a label beside them.</summary>
    private static int RiskPipsWidth(int pip) => pip * 4 + UiMetrics.Space(3) * 3;

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
    private IReadOnlyList<Vow> Known => KnownVows;

    /// <summary>
    /// The live build and the context the Vow layer judges it in, composed once, together.
    /// </summary>
    /// <remarks>
    /// The BUILD is wanted as well as the context because a Vow is a promise about the whole build:
    /// what it pays, and whether the affinity buy-back applies at all, are questions about the build's
    /// sworn promises taken together — never about one row. Composed the same way the fight composes
    /// it, so what this screen prints is what the descent will do.
    /// </remarks>
    private (Build Build, BuildContext Ctx) Live()
    {
        var build = Loadout.ToBuild(Mastery, Character, SkillLevels, DiscoveredKeystones, KnownVows);
        return (build, Hunter is { } h ? SoloBattle.DescribeBuild(build, h) : BuildContext.Empty);
    }

    /// <summary>The live build, described to the Vow layer — the same struct the simulation judges against.</summary>
    private BuildContext Context => Live().Ctx;

    /// <summary>Every skill this hunter can equip: the roads walked, plus what it was born with.</summary>
    private IReadOnlySet<string> KnownSkills()
    {
        var set = Mastery.AvailableSkills().ToHashSet(StringComparer.Ordinal);
        if (Character?.SignatureSkillId is { } born) set.Add(born);
        return set;
    }

    private SkillDef? SlotDef(int slot) =>
        slot >= 0 && slot < Loadout.Skills.Count ? SkillCatalogue.Find(Loadout.Skills[slot].SkillId) : null;

    /// <summary>A slot that holds a skill the hunter knows. Anything else reads as empty — the composer refuses it.</summary>
    private bool SlotFilled(int slot) => SlotDef(slot) is { } d && KnownSkills().Contains(d.Id);

    private float Dps(PlayerLoadout loadout, Hunter hunter)
        => DamageBench.Measure(loadout.ToBuild(Mastery, Character, SkillLevels, DiscoveredKeystones, KnownVows), hunter).Dps;

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
                var sources = Loadout.ToBuild(Mastery, Character, SkillLevels, DiscoveredKeystones, KnownVows).Skills.Select(s => SourceName(s.Source)).Distinct().ToList();
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
        if (_copyToastFrames > 0) _copyToastFrames--;
        UpdateInput(mouse, clicked, held, wheel);
        // After the input, whatever path it took — UpdateInput returns early from a dozen places, and the
        // fade must not depend on which one. RH_SHOT_BUILD_PICK poses a selection the way a click would;
        // then the inspector learns whether what it is about has changed. This is the ONLY place any of
        // this screen's one-shots is armed by something other than the player's own edit.
        if (DevPick is { } posed && SkillCatalogue.Find(posed) is not null)
        {
            _pick = Pick.Library; _pickSkillId = posed; _vowListOpen = false;
        }
        TrackSelection();
    }

    /// <summary>
    /// The inspector is ABOUT something; when that something changes, its content fades in (§35). The
    /// signature is every field the body reads to decide WHAT it is showing — not the values it shows, so a
    /// bench figure ticking over or a Vow going live does not restart the fade.
    /// </summary>
    private void TrackSelection()
    {
        var sig = HashCode.Combine((int)_pick, _slot, _pickSkillId, _pickIndex, _pickKeystoneId, _vowListOpen);
        if (sig == _insSignature) return;
        _insSignature = sig;
        UiMotion.Flash(InsFadeKey, UiMotion.Fast);
    }

    private void UpdateInput(Point mouse, bool clicked, bool held, int wheel)
    {
        var hit = mouse;
        var skills = Loadout.Skills;
        var known = Known;
        // The wheel first, against last frame's overflow, so this frame's layout already sits where it scrolled to.
        if (wheel != 0)
        {
            if (LoadoutPanel.Contains(hit))
            {
                if (_keystonePaged) _keystoneScroll = UiKit.Scrolled(_keystoneScroll, wheel, KeystoneRows, DiscoveredKeystones.Count);
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
                    PulseSlot(onto);
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
        var learned = DiscoveredKeystones;
        foreach (var (index, chip) in _chips)
        {
            if (index >= learned.Count || !In(chip, list).Contains(hit)) continue;
            _pick = Pick.Keystone; _pickKeystoneId = learned[index].Id; _vowListOpen = false; _msg = "";
            return;
        }

        // ── THE SKILLS COLUMN: tiles, the fork, the chips — all select. ─────────────────────────────
        var skillsRegion = SkillsRegion;
        // THE SIGNATURE'S OWN TILE, first, because it sits above the twelve. Only ever THIS champion's:
        // Signature reads the active character, so another champion's signature has no cell to click.
        if (Signature is { } sigPick && In(SigTile, skillsRegion).Contains(hit))
        {
            _pick = Pick.Library; _pickSkillId = sigPick.Id; _vowListOpen = false; _msg = "";
            return;
        }
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
                    if (Loadout.SetVow(_slot, null, known)) { Dirty = true; _buildRev++; _msg = "NO VOW SWORN."; }
                }
                else
                {
                    var v = known[idx];
                    var already = skills[_slot].VowId == v.Id;
                    if (Loadout.SetVow(_slot, already ? null : v.Id, known))
                    {
                        Dirty = true; _buildRev++;
                        if (already) _msg = $"{v.Name.ToUpperInvariant()} — NO LONGER SWORN.";
                        else { _msg = $"{v.Name.ToUpperInvariant()} SWORN."; UiMotion.Flash(BindKey(_slot), UiMotion.Reward); Sound?.Play("sfx_bind", 0.55f); }
                    }
                    else _msg = Loadout.VowFitsCapacity(_slot, v.Id)
                        ? "THAT VOW CANNOT BE SWORN RIGHT NOW."
                        : $"YOU MAY HOLD {Loadout.VowCapacity} VOW{(Loadout.VowCapacity == 1 ? "" : "S")} AT ONCE. "
                          + (NextVowNote.Length > 0 ? NextVowNote + "." : "");
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
        if (PrimaryBtn.Contains(hit))
        {
            var (_, enabled, refusal) = Primary();
            if (enabled) Commit();
            // A refused click is ANSWERED, not swallowed (brief §29, LAW 13): the button gives the pressed
            // state while the mouse is down on it (drawn in DrawInspector), and the host plays the dull
            // refusal cue. A disabled button with nothing to refuse — CHOSEN, OWNED, EQUIPPED IN THIS
            // SLOT — only presses; there is nothing to say no to.
            else if (refusal.Length > 0) _cue = "sfx_error";
        }
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
                // LOCKED, NOT UNLEARNED (BRIEF sec.20, LAW 4). The button says the state and the level the
                // player keeps; the refusal beside it names the one thing that would open it again. Neither
                // may say a word that implies the waves spent on this skill have gone anywhere.
                if (!known.Contains(def.Id) && !Seen(def))
                    return ("UNDISCOVERED", false, $"TAKE {StyleName(def.Style)}'S ROAD ON THE MASTERY TREE TO REVEAL IT.");
                if (!known.Contains(def.Id))
                {
                    var kept = SkillLevels.LevelOf(def.Id);
                    return (kept > 0 ? $"LOCKED  ·  LEVEL {kept} KEPT" : "LOCKED", false,
                            kept > 0
                                ? $"TAKE {StyleName(def.Style)}'S ROAD ON THE MASTERY TREE TO UNLOCK IT. YOUR LEVEL {kept} IS WAITING."
                                : $"TAKE {StyleName(def.Style)}'S ROAD ON THE MASTERY TREE TO UNLOCK IT.");
                }
                if (_slot >= skills.Count) return ("EQUIP", false, "PICK A SLOT ON THE LEFT FIRST.");
                if (skills[_slot].SkillId == def.Id) return ($"EQUIPPED IN SLOT {_slot + 1}", false, "");
                // LAW 13: one slot per skill. The button says WHERE it already is, in words, rather than
                // going grey without a reason — the loadout itself refuses the write regardless.
                if (Loadout.IndexOfSkill(def.Id) is var other && other >= 0)
                    return ($"ALREADY EQUIPPED IN SLOT {other + 1}", false, $"IT IS IN SLOT {other + 1} — PICK THAT SLOT TO CHANGE IT.");
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
                    return ("SOCKET", false, $"ONLY {Loadout.KeystoneCapacity} SOCKET{(Loadout.KeystoneCapacity == 1 ? "" : "S")}. {NextSocketNote}.");
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
                    _cue = "sfx_error";
                    break;
                }
                Dirty = true; _buildRev++;
                PulseSlot(_slot); Sound?.Play("sfx_weave", 0.45f);
                _msg = $"{SkillCatalogue.Find(_pickSkillId)?.Name} EQUIPPED IN SLOT {_slot + 1}.";
                _pick = Pick.Slot;
                break;
            case Pick.Variation:
            {
                var def = SlotDef(_slot)!;
                var v = def.Variations[_pickIndex];
                SkillLevels.ChooseVariation(def, v.Name); Dirty = true; _buildRev++;
                _msg = $"{def.Name} IS NOW {v.Name}, AND IT IS {SourceName(v.Source)}.";
                BrightenBranch(_pickIndex);   // the fork taken lights once (§43)
                break;
            }
            case Pick.Reinforcement:
            {
                var def = SlotDef(_slot)!;
                var chosen = SkillLevels.VariationOf(def)!;
                var r = chosen.Reinforcements[_pickIndex];
                SkillLevels.TakeReinforcement(def, r.Name); Dirty = true; _buildRev++;
                _msg = $"{r.Name}: {r.Line.ToUpperInvariant()}";
                PulseReinforcement(BranchOf(def, chosen), _pickIndex);   // the chip just taken pulses once (§43)
                break;
            }
            case Pick.Keystone:
                if (Loadout.ToggleKeystone(_pickKeystoneId, DiscoveredKeystones)) { Dirty = true; _buildRev++; _msg = ""; }
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

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC8));
        _ui.TextCenterBig(b, "BUILD", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        DrawLoadout(b, hit);
        DrawSkills(b, hit);
        DrawInspector(b, hit);

        if (_msg.Length > 0) _ui.TextCenterBig(b, _msg, UiKit.PageCenterX, UiKit.PageBottom(BottomMargin) + UiMetrics.Space(16), Gold, UiTypography.Body);
        if (_copyToastFrames > 0)
        {
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
        var (model, ctx) = Live();
        // THE BUY-BACK IS A FACT ABOUT THE BUILD. The fight pulls every off-discipline skill one ring
        // closer while the build is keeping at least one promise (SoloBattle, Vows.AnyKept), so the
        // rows have to ask the same question — asking each row about its own vow printed a number the
        // descent would not use.
        var vowKept = Vows.AnyKept(model.Vows, ctx);
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
            // The row you are HOLDING keeps its hover and takes the pressed state; a row you are dragging
            // OVER gets the drop mark instead, which is a different answer to a different question.
            var over = shown.Contains(hit) && (_carrying == Carry.None || _carrySlot == slot);
            var dropping = _carrying == Carry.Slot && _carryMoved && SlotUnder(_carryAt) == slot && _carrySlot != slot;
            var inHand = _carrying == Carry.Slot && _carryMoved && _carrySlot == slot;
            var col = SourceColor.GetValueOrDefault(s.Source, Bone);

            Cell(b, row, Quiet, on, over);
            if (dropping) { _ui.Fill(b, row, col * 0.16f); Outline(b, row, Bone, 3); }
            if (inHand) _ui.Fill(b, row, new Color(0x0C, 0x09, 0x14) * 0.6f);
            // THE EQUIP PULSE (§40): the slot that just took a skill answers once, in that skill's own Source
            // colour, over Transition. Armed at the equip in Update; this only reads how far along it is.
            var pulse = Phase(SlotKey(slot), slot == _slot);
            if (pulse > 0f) _ui.Fill(b, row, col * (0.34f * pulse));
            var chain = slot == _slot && _devChainAt is { } dc ? dc : UiMotion.Pulse(BindKey(slot));
            if (chain > 0f) DrawBindChain(b, row, chain);
            if (on && !dropping) Outline(b, row, Gold, 2);   // gold = selected

            // The numbered spine: this order IS cast priority.
            var spine = new Rectangle(row.X, row.Y, SpineW, row.Height);
            _ui.Fill(b, spine, col * (on ? 0.55f : 0.34f));
            _ui.TextCenterBig(b, $"{slot + 1}", spine.Center.X, row.Y + (row.Height - UiTypography.Body) / 2, on ? Bone : Bone * 0.75f, UiTypography.Body);

            var gbox = GlyphRect(row);
            // (No darker well behind the glyph: it read as an empty image frame the icon was pasted into — build-19.)
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

            // Line 1: NAME, and the vow's SEAL at the right — the medallion, then a chip with the state.
            // The seal carries the identity (its name is the hover), the chip carries the verdict.
            var vow = Vows.ById(s.VowId);
            var pillW = 0;
            if (vow is not null)
            {
                var live = Vows.IsActive(vow, ctx);
                var sealEdge = UiTypography.Caption + UiTypography.ChipPadY * 2 + UiMetrics.Space(6);
                // THE NAME KEEPS ITS ROOM. The chip says the most the row can afford: HOLDS x1.30, then
                // HOLDS alone, then nothing — the seal's tint and crack already say the state, and at
                // 150 % a full chip shortened HARD HANDS to "H…" (fix3_weave_vowcomplete_150.png).
                var nameNeed = _ui.MeasureBig(def.Name, UiTypography.Headline) + UiMetrics.Space(12);
                var roomForChip = right - tx - nameNeed - sealEdge - UiMetrics.Space(6);
                var pill = live ? $"HOLDS  x{Vows.Multiplier(vow):0.00}" : "BROKEN";
                var chipW = _ui.MeasureBig(pill, UiTypography.Caption) + UiTypography.ChipPadX * 2;
                if (chipW > roomForChip && live) { pill = "HOLDS"; chipW = _ui.MeasureBig(pill, UiTypography.Caption) + UiTypography.ChipPadX * 2; }
                if (chipW > roomForChip) { pill = ""; chipW = 0; }
                pillW = sealEdge + (chipW > 0 ? UiMetrics.Space(6) + chipW : 0);
                var sealBox = new Rectangle(right - pillW, row.Y + pad - UiMetrics.Space(3), sealEdge, sealEdge);
                VowSeal(b, vow, sealBox, live);
                var hot = sealBox;
                if (chipW > 0)
                {
                    var pr = new Rectangle(sealBox.Right + UiMetrics.Space(6), row.Y + pad, chipW, UiTypography.Caption + UiTypography.ChipPadY * 2);
                    _ui.Fill(b, pr, (live ? Gold : Ember) * 0.16f);
                    Outline(b, pr, live ? Gold : Ember, 1);
                    _ui.TextBig(b, pill, pr.X + UiTypography.ChipPadX, pr.Y + UiTypography.ChipPadY, live ? Gold : Ember, UiTypography.Caption);
                    hot = Rectangle.Union(sealBox, pr);
                }
                Tip(In(hot, region), hit, live ? $"{vow.Name.ToUpperInvariant()} holds — {DemandText(vow)} — every skill x{Vows.Multiplier(vow):0.00}." : $"{vow.Name.ToUpperInvariant()} is broken: {DemandText(vow)} — it pays nothing until it holds.");
            }
            _ui.TextBig(b, _ui.ShortenBig(def.Name, right - pillW - pad - tx, UiTypography.Headline), tx, line1, on ? Gold : Bone, UiTypography.Headline);
            // Line 2: STYLE · LEVEL · the style factor.
            var level = SkillLevels.LevelOf(def.Id);
            var line2Text = $"{StyleName(def.Style)} · LEVEL {level}";   // the word, as the inspector and the tree say it (build-11)
            if (ChosenStyle is { } dd)
            {
                var f = StyleAffinity.Factor(dd, def.Style, vowKept);
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
                // "+1 TO SPEND" says what the number is; a bare "+1" after "0/3" was two numbers with no noun
                // (build-11). The short form is the last rung of the fit ladder below.
                var spare = free > 0 ? $"  +{free} TO SPEND" : "";
                var spareShort = free > 0 ? $"  +{free}" : "";
                var tail = $"{dot}{bought}/{chosen.Reinforcements.Count}{spare}";
                var fixedW = _ui.MeasureBig(dot, UiTypography.Body) + _ui.MeasureBig(source, UiTypography.Body);
                // Fit, in order: the whole line; else without the bought count (the tree's chips carry it) but
                // never without a level to spend; then the short spend; else the name gives way. The Source word
                // always stays — it is the skill's identity, and a colour alone must never carry it.
                var nameW = _ui.MeasureBig(vname, UiTypography.Body);
                if (nameW + fixedW + _ui.MeasureBig(tail, UiTypography.Body) > room) tail = spare;
                if (nameW + fixedW + _ui.MeasureBig(tail, UiTypography.Body) > room) tail = spareShort;
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
        var learned = DiscoveredKeystones;
        var head = _keystoneHead;
        _ui.TextBig(b, "KEYSTONES", head.X, head.Y, Slate, UiTypography.Secondary);
        var paged = _keystonePaged && learned.Count > KeystoneRows
            ? $"  ·  {_keystoneScroll + 1}-{Math.Min(learned.Count, _keystoneScroll + KeystoneRows)} OF {learned.Count}" : "";
        // MEASURED AGAINST WHAT THE LABEL LEAVES. Right-aligned text with a left-aligned label beside
        // it is only safe while the two together fit, and at UI SCALE 150 this sentence ran back
        // underneath the word KEYSTONES — two strings in the same pixels, which no reader can unpick.
        var headRoom = head.Width - _ui.MeasureBig("KEYSTONES", UiTypography.Secondary) - UiMetrics.Space(16);
        var headNote = learned.Count == 0
            ? "CONQUER A REGION"
            : $"{Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity} SOCKETS" + paged;
        _ui.TextRightBig(b, _ui.ShortenBig(headNote, headRoom, UiTypography.Secondary),
                         head.Right, head.Y, Slate, UiTypography.Secondary);
        foreach (var (idx, chip) in _chips)
        {
            if (chip.Bottom <= region.Y || chip.Y >= region.Bottom) continue;
            var shown = In(chip, region);
            var textY = chip.Y + (chip.Height - UiTypography.Body) / 2;
            if (idx >= learned.Count)
            {
                _ui.Plate(b, chip);
                var emptyText = learned.Count == 0 && idx == 0
                    ? "NO KEYSTONES YET"
                    : "EMPTY SOCKET";
                _ui.TextBig(b, _ui.ShortenBig(emptyText, chip.Width - UiMetrics.Space(32), UiTypography.Body),
                            chip.X + UiMetrics.Space(16), textY, UiInk.Empty, UiTypography.Body);
                continue;
            }
            var k = learned[idx];
            var worn = Loadout.HasKeystone(k.Id);
            var on = _pick == Pick.Keystone && _pickKeystoneId == k.Id;
            var over = shown.Contains(hit);
            Cell(b, chip, Quiet, on, over);
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
            // "BENCH" was the developer's word for the test bench the figure is measured on (build-18).
            _ui.TextBig(b, _ui.ShortenBig("DAMAGE PER SECOND · PRACTICE DUMMY", bench.Width, UiTypography.Secondary), bench.X, bench.Y, Slate, UiTypography.Secondary);
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
        var over = shown.Contains(hit) && _carrying == Carry.None;
        _ui.Plate(b, row);
        var lift = Lift(row, over);
        if (lift > 0f) _ui.Fill(b, row, HoverWash * lift);
        if (Down(over)) _ui.Fill(b, row, PressWash);
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
        // The quiet FRAME, like its two neighbours: the largest surface on the screen was a flat plate
        // between the gold loadout frame and the bronze inspector, and read as the least finished thing
        // on it (release polish 2026-09-05, build-07).
        _ui.PanelQuiet(b, panel);
        var known = KnownSkills();
        var skills = Loadout.Skills;
        _ui.TextBig(b, "SKILLS", SkillsX, SkillsHeadY, Slate, UiTypography.Secondary);
        // THE TWELVE, AND ONLY THE TWELVE. The signature is not one of them — counting it printed 13 / 12 —
        // and the word is UNLOCKED rather than LEARNED, because access follows the current mastery
        // allocation now and a skill can go back to LOCKED (BRIEF sec.16, sec.20, LAW 3).
        var openShared = SkillCatalogue.Shared.Count(d => known.Contains(d.Id));
        var learnedHead = $"{openShared} / 12 SHARED SKILLS UNLOCKED  ·  MORE ON THE MASTERY TREE";
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
        var lockEdge = UiMetrics.Control(24);   // the lock badge on a locked tile's icon (build-05)
        var nameLeft = UiMetrics.Control(52);
        var edgePad = UiMetrics.Space(8);
        static int nameY0(Rectangle tile) => tile.Y + UiMetrics.Space(6);
        // The EQUIPPED · SLOT N badge shares the name's line. Where a tile at this profile cannot hold the
        // widest badge AND the library's widest name beside it, the badge says only SLOT N — the gold outline
        // already says equipped (brief §9: 150 % may carry less secondary text; never a smaller font). Decided
        // once per frame from measured widths, so every tile agrees.
        var widestName = 0;
        for (var st = 0; st < StyleCount; st++)
            widestName = Math.Max(widestName, Math.Max(_ui.MeasureBig(SkillCatalogue.ActiveOf((Style)st).Name, UiTypography.Body),
                                                       _ui.MeasureBig(SkillCatalogue.PassiveOf((Style)st).Name, UiTypography.Body)));
        var longBadge = LibTile(0, 0).Width - nameLeft - _ui.MeasureBig("EQUIPPED · SLOT 5", UiTypography.Caption) - UiMetrics.Space(16) >= widestName;
        // THE SECOND LINE OF A LOCKED TILE, decided the same way and for the same reason. A skill the
        // hunter cannot weave right now says LOCKED and — where waves have been spent on it — the LEVEL it
        // keeps, which is the whole of BRIEF sec.20. Where the narrow tile cannot hold ACTIVE and both, the
        // kind is what goes: the inspector still says it, and no word may imply the levels are gone (LAW 4).
        var subRoom = LibTile(0, 0).Width - nameLeft - edgePad * 2;
        var widestLock = 0;
        for (var st = 0; st < StyleCount; st++)
            foreach (var d in new[] { SkillCatalogue.ActiveOf((Style)st), SkillCatalogue.PassiveOf((Style)st) })
                if (!known.Contains(d.Id)) widestLock = Math.Max(widestLock, _ui.MeasureBig(LockLine(d, full: true), UiTypography.Secondary));
        var longLock = widestLock <= subRoom;

        // One tile, wherever it sits: the signature's own row and the twelve are the same object drawn twice,
        // so a change to the tile cannot land on one and miss the other.
        void Tile(Rectangle tile, SkillDef def)
        {
            var shown = In(tile, region);
            var have = known.Contains(def.Id);
            var isEquipped = equipped.Contains(def.Id);
            var on = _pick == Pick.Library && _pickSkillId == def.Id;
            var over = shown.Contains(hit);
            Cell(b, tile, TileBg, on, over);
            // Only a STATE draws an outline over the plate's own rule (build-06).
            if (on || isEquipped || over) Outline(b, tile, on ? Bone : isEquipped ? Gold : Slate, on || isEquipped ? 2 : 1);
            var ico = new Rectangle(tile.X + edgePad, tile.Y + edgePad, icoEdge, icoEdge);
            var subY = nameY0(tile) + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(6);
            if (!have && !Seen(def))
            {
                // AN UNDISCOVERED SKILL IS A SEAL AND A QUESTION, and nothing else — no name, no glyph, no
                // kind, no level, no lock (a lock says "you know what this is"). The same reading the
                // TRAITS screen gives an unknown characteristic, so the two libraries agree on what a
                // secret looks like. The tile stays clickable: the inspector says how a skill is revealed.
                if (!_ui.Icon(b, "icon_unknown_seal", ico, Color.White * (over ? 1f : 0.85f))) _ui.Diamond(b, ico, Unknown * 0.6f);
                _ui.TextBig(b, "???", tile.X + nameLeft, nameY0(tile), Unknown, UiTypography.Body);
                _ui.TextBig(b, "UNDISCOVERED", tile.X + nameLeft, subY, Unknown * 0.8f, UiTypography.Secondary);
                Tip(shown, hit, "An undiscovered skill. A road on the MASTERY tree reveals it.");
                return;
            }
            if (!_ui.Icon(b, $"icon_skill_{def.Id}", ico, have ? (isEquipped ? Gold : Bone) : Slate)) _ui.Diamond(b, ico, Slate);
            if (!have)
            {
                // THE LOCK AS A BADGE ON THE ICON, on its own dark plate — the 20 px copy at the tile's far
                // edge was a 128 px medallion downscaled to a grey smudge (build-05).
                var badge = new Rectangle(ico.Right - lockEdge * 2 / 3, ico.Bottom - lockEdge * 2 / 3, lockEdge, lockEdge);
                _ui.Fill(b, badge, UiInk.Plate);
                _ui.Icon(b, "ui_slot_locked", badge, Bone);
            }
            var badgeText = isEquipped ? (longBadge ? $"EQUIPPED · SLOT {Loadout.IndexOfSkill(def.Id) + 1}" : $"SLOT {Loadout.IndexOfSkill(def.Id) + 1}") : "";
            var tail = isEquipped ? _ui.MeasureBig(badgeText, UiTypography.Caption) + edgePad : 0;
            var nameX = tile.X + nameLeft;
            var nameY = nameY0(tile);
            _ui.TextBig(b, _ui.ShortenBig(def.Name, tile.Right - edgePad - tail - nameX, UiTypography.Body), nameX, nameY, have ? (isEquipped ? Gold : Bone) : Slate, UiTypography.Body);
            var sub = have ? (def.TakesABeat ? "ACTIVE" : "PASSIVE") : LockLine(def, longLock);
            // The state line at Secondary, never fine print, and never the refusal red: a kept level is
            // good news (build-04). The lock badge and the Slate name already say LOCKED.
            var subInk = have ? Slate : Bone;
            _ui.TextBig(b, _ui.ShortenBig(sub, tile.Right - edgePad - tail - nameX, UiTypography.Secondary), nameX, subY, subInk, UiTypography.Secondary);
            if (isEquipped) _ui.TextRightBig(b, badgeText, tile.Right - edgePad, tile.Y + (tile.Height - UiTypography.Caption) / 2, Gold, UiTypography.Caption);
            Tip(shown, hit, have ? $"{def.Name} — {def.Line}" : LockTip(def));
        }

        // ── THE SIGNATURE, PINNED ABOVE THE TWELVE (BRIEF sec.12). It is this champion's alone, it needs
        //    no mastery node, and no other champion's signature has a cell here to be picked from. ──
        if (Signature is { } sig)
        {
            var sigLabelY = LibTop + UiMetrics.Space(14);
            // The row label in Primary and the owner in Secondary — gold is for the equipped tile beside
            // them, and a label wearing it was a third gold claim in one row (build-16).
            _ui.TextBig(b, "SIGNATURE", SkillsX, sigLabelY, Bone, UiTypography.Body);
            if (Character is { } who)
                _ui.TextBig(b, _ui.ShortenBig(who.Name.ToUpperInvariant(), LibStyleW - UiMetrics.Space(10), UiTypography.Secondary),
                            SkillsX, sigLabelY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(6), Slate, UiTypography.Secondary);
            Tile(SigTile, sig);
            // The rule that says the twelve below are a different kind of thing: shared, and behind a road.
            _ui.Fill(b, new Rectangle(SkillsX, LibTop + LibRowPitch + UiMetrics.Space(4), LibW, 1), Dim);
        }

        for (var st = 0; st < StyleCount; st++)
        {
            var style = (Style)st;
            var rowY = LibTop + SigBlockH + st * LibRowPitch;
            var yours = ChosenStyle == style;
            var labelY = rowY + UiMetrics.Space(14);
            _ui.TextBig(b, StyleName(style), SkillsX, labelY, yours ? Gold : Slate, UiTypography.Body);
            if (yours) _ui.TextBig(b, "YOURS", SkillsX, labelY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(6), Gold, UiTypography.Caption);
            for (var w = 0; w < 2; w++)
                Tile(LibTile(st, w), w == 0 ? SkillCatalogue.ActiveOf(style) : SkillCatalogue.PassiveOf(style));
        }
    }

    /// <summary>
    /// A locked tile's second line: LOCKED, and the level the player keeps while it is locked.
    /// </summary>
    /// <remarks>
    /// BRIEF sec.20 and LAW 4. Access follows the current mastery allocation and comes and goes with it;
    /// the waves spent on a skill never do. So a skill with experience and no node must never read as
    /// unlearned — it reads LOCKED, and it says the level that is waiting for it.
    /// The <paramref name="full"/> form carries the ACTIVE / PASSIVE kind as well; the narrow one drops it.
    /// </remarks>
    private string LockLine(SkillDef def, bool full)
    {
        var kind = def.TakesABeat ? "ACTIVE" : "PASSIVE";
        var level = SkillLevels.LevelOf(def.Id);
        if (level <= 0) return full ? $"{kind} · LOCKED" : "LOCKED";
        return full ? $"{kind} · LOCKED · LEVEL {level}" : $"LOCKED · LEVEL {level}";
    }

    /// <summary>The hover line for a locked tile — what it is, what is kept, and what would open it.</summary>
    private string LockTip(SkillDef def)
    {
        var road = $"Take {StyleName(def.Style)}'s road on the MASTERY tree to unlock it.";
        var level = SkillLevels.LevelOf(def.Id);
        return level > 0
            ? $"{def.Name} — LOCKED. Your level {level} is kept while it is locked. {road}"
            : $"{def.Name} — LOCKED. {road}";
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
        // One clause: the four-line paragraph here was the only telling of the levelling rule, and it sat
        // over the inspector's action band (build-14). The inspector's CURRENT STATE says the rest.
        Tip(In(new Rectangle(SkillsX, treeTop, LibW, TreeIcon + UiMetrics.Space(12)), region), hit, "Levels come from clearing waves with it equipped. Respec is free.");

        // The rails from the skill down to the two variations — the crossbar STARTS at the trunk, so the
        // fork connects: a stub under the icon and a floating bracket over the cards read as two stray
        // fragments (build-02).
        var railY = RailY;
        var trunkX = headIco.Center.X;
        var barX0 = Math.Min(trunkX, VarCard(0).Center.X);
        _ui.Fill(b, new Rectangle(trunkX - 1, headIco.Bottom, 2, railY - headIco.Bottom), Dim);
        _ui.Fill(b, new Rectangle(barX0 - 1, railY, VarCard(1).Center.X - barX0 + 2, 2), Dim);
        var cardPad = UiMetrics.Space(12);
        for (var vi = 0; vi < treeDef.Variations.Count && vi < 2; vi++)
        {
            var v = treeDef.Variations[vi];
            var card = VarCard(vi);
            var shown = In(card, region);
            var taken = chosen?.Name == v.Name;
            var other = chosen is not null && !taken;
            var on = _pick == Pick.Variation && _pickIndex == vi;
            var over = shown.Contains(hit);
            var col = SourceColor.GetValueOrDefault(v.Source, Bone);
            // THE FORK TAKEN LIGHTS ONCE (§43): the rail down to the chosen card, the card itself and its
            // chips brighten in the variation's own Source colour for one Transition. What is left afterwards
            // is the gold rule that says CHOSEN — the motion explained the change, it did not become the state.
            var branch = Phase(BranchKey(treeDef.Id, vi), taken);
            var rail = new Rectangle(card.Center.X - 1, railY, 2, card.Y - railY);
            _ui.Fill(b, rail, Dim);
            if (branch > 0f) _ui.Fill(b, rail, col * branch);
            Cell(b, card, CardBg, on, over);
            if (branch > 0f) { _ui.Fill(b, card, col * (0.30f * branch)); Outline(b, card, Gold * branch, 2); }
            if (on || taken) Outline(b, card, on ? Bone : Gold, 2);   // only a state outlines the plate (build-06)
            if (taken) _ui.Fill(b, new Rectangle(card.X, card.Y, 5, card.Height), Gold);
            var gem = new Rectangle(card.X + UiMetrics.Space(16), card.Y + cardPad, UiMetrics.IconSize, UiMetrics.IconSize);
            _ui.Diamond(b, new Rectangle(gem.X - 3, gem.Y - 3, gem.Width + 6, gem.Height + 6), col * (other ? 0.12f : 0.30f));
            if (_ui.Assets.Get($"source_{v.Source.ToString().ToLowerInvariant()}") is { } gg) b.Draw(gg, gem, other ? Color.White * 0.55f : Color.White);
            var textX = gem.Right + cardPad;
            var nameY = card.Y + UiMetrics.Space(10);
            _ui.TextBig(b, v.Name.ToUpperInvariant(), textX, nameY, taken ? Gold : other ? Slate : Bone, UiTypography.Body);
            _ui.TextBig(b, $"{SourceName(v.Source)}{(taken ? " · CHOSEN" : "")}", textX, nameY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(4), other ? Slate : col, UiTypography.Secondary);
            // The sentence WRAPS at Secondary — it was cut mid-word at Caption on both profiles where it
            // showed, and it is the one line that says what the variation does (build-03).
            var ly = nameY + UiTypography.Pitch(UiTypography.Body) + UiTypography.Pitch(UiTypography.Secondary) - UiMetrics.Space(2);
            foreach (var l in _ui.WrapBig(v.Line.ToUpperInvariant(), card.Width - cardPad * 2, UiTypography.Secondary).Take(2))
            {
                _ui.TextBig(b, l, card.X + cardPad, ly, Slate, UiTypography.Secondary);
                ly += UiTypography.Pitch(UiTypography.Secondary);
            }
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
                // A REINFORCEMENT TAKEN PULSES ONCE (§43), and every chip on the chosen fork rides that fork's
                // brightening, so the branch reads as one thing lighting rather than four separate flickers.
                var chipPulse = Phase(ReinfKey(treeDef.Id, vi, ri), taken && ri == 0);
                Cell(b, chip, ChipBg, onR, overR);
                if (branch > 0f) _ui.Fill(b, chip, col * (0.18f * branch));
                if (chipPulse > 0f) { _ui.Fill(b, chip, Met * (0.34f * chipPulse)); Outline(b, chip, Met * chipPulse, 2); }
                Outline(b, chip, onR ? Bone : owned ? Met : Dim, onR || owned ? 2 : 1);
                var chipPad = UiMetrics.Space(10);
                // DISABLED SAYS WHY (§29). A chip under the fork you did NOT take cannot be bought, and it used
                // to say nothing at all — the reason only appeared as a message after you had clicked it. The
                // name gives way to the state, so the two never print through each other at a bigger profile.
                var chipState = owned ? "OWNED" : can ? "READY" : taken ? $"LEVEL {level + 1}" : "CHOOSE FIRST";
                var chipRoom = chip.Width - chipPad * 2 - _ui.MeasureBig(chipState, UiTypography.Caption) - UiMetrics.Space(10);
                _ui.TextBig(b, _ui.ShortenBig(r.Name.ToUpperInvariant(), chipRoom, UiTypography.Secondary), chip.X + chipPad, chip.Y + (chip.Height - UiTypography.Secondary) / 2, owned ? Met : can ? Gold : taken ? Bone : Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, chipState, chip.Right - chipPad, chip.Y + (chip.Height - UiTypography.Caption) / 2, owned ? Met : can ? Gold : Slate, UiTypography.Caption);
                Tip(shownChip, hit, $"{r.Name} — {r.Line}");
            }
        }
    }

    // ── THE INSPECTOR (D3) ──────────────────────────────────────────────────────────────────────────────
    private void DrawInspector(SpriteBatch b, Point hit)
    {
        // THE CONTENT FADE (§35), read once for the whole frame. The BODY fades in when the selection
        // changes; the frame, the refusal line, the two text actions and the primary button do not — they
        // are the panel's structure, and structure that blinked on every click is the opposite of calm.
        _insAlpha = InspectorAlpha;
        // THE REFUSAL IS MEASURED BEFORE THE BODY IS CLIPPED, so a two-line reason takes its room from the
        // body rather than printing through it. Primary() is a pure read of the selection, so asking it
        // here costs nothing and cannot disagree with the button drawn from the same answer below.
        var (label, enabled, refusal) = Primary();
        var refusalWrap = refusal.Length > 0 ? _ui.WrapBig(refusal, InsW, UiTypography.Secondary) : System.Array.Empty<string>();
        var refusalRows = Math.Min(3, refusalWrap.Count);   // two is the longest any refusal here needs; three is the net
        _refusalRise = Math.Max(0, refusalRows - 1) * UiTypography.Pitch(UiTypography.Secondary);
        _refusalShown = refusalRows > 0;
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
        var (model, ctx) = Live();
        var vowKept = Vows.AnyKept(model.Vows, ctx);   // see DrawLoadout: the buy-back is build-wide
        _changeVowY = 0;
        _changeVowShown = false;
        _respecShown = false;

        void Head(string s, Color? c = null) { _ui.TextBig(b, s, x, y, Ins(c ?? Slate), UiTypography.Secondary); y += UiTypography.Pitch(UiTypography.Secondary); }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            // The line cap is written for the 100 % column. Bigger type in the same column (less the scrollbar's
            // lane) needs more lines to say the same thing, so the cap grows with the type: the text wraps
            // further instead of ending mid-sentence (brief §9), and at 100 % the cap is exactly what it was.
            var cap = (int)MathF.Ceiling(maxLines * UiMetrics.TextScale * InsW / (float)w);
            foreach (var l in _ui.WrapBig(s, w, px).Take(cap))
            {
                _ui.TextBig(b, l, x, y, Ins(c), px); y += UiTypography.Pitch(px);
            }
        }
        void Gap() { y += UiMetrics.Space(10); }
        void Rule() { _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(4), w, 1), Ins(Dim)); y += UiTypography.HairlineGap; }
        void Pair(string k, string v)
        {
            _ui.TextBig(b, k, x, y, Ins(Slate), UiTypography.Secondary);
            _ui.TextRightBig(b, _ui.ShortenBig(v, w - UiMetrics.Text(120), UiTypography.Body), x + w, y - 2, Ins(Bone), UiTypography.Body);
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
        var refusalY = RefusalY - _refusalRise;
        for (var i = 0; i < refusalRows; i++)
        {
            _ui.TextBig(b, refusalWrap[i], InsX, refusalY, Ember, UiTypography.Secondary);
            refusalY += UiTypography.Pitch(UiTypography.Secondary);
        }
        var actionTextY = (ActionRowH - UiTypography.Secondary) / 2;
        // The two text actions get the same three states a button does: the ink LIFTS toward bone over
        // Fast rather than snapping, and while the mouse is down the line settles a pixel and dims — the
        // same depression, at the weight a text action deserves. RESPEC gets no flourish beyond that: it
        // is free and reversible, and it stays calm (§44).
        if (_respecShown)
        {
            var over = RespecText.Contains(hit);
            var press = Down(over);
            _ui.TextBig(b, "RESPEC — FREE", RespecText.X, RespecText.Y + actionTextY + (press ? 1 : 0),
                        Color.Lerp(Slate, Bone, Lift(RespecText, over)) * (press ? 0.75f : 1f), UiTypography.Secondary);
            Tip(RespecText, hit, "Give this skill's levels back. Free, and it keeps every level it has earned.");
        }
        {
            var over = CopyText.Contains(hit);
            var press = Down(over);
            _ui.TextRightBig(b, "COPY BUILD CODE", CopyText.Right, CopyText.Y + actionTextY + (press ? 1 : 0),
                             Color.Lerp(Slate, Bone, Lift(CopyText, over)) * (press ? 0.75f : 1f), UiTypography.Secondary);
            Tip(CopyText, hit, "Copies this build as a code. A friend pastes it in the VAULT.");
        }
        if (label.Length > 0)
        {
            // A REFUSED CLICK IS ANSWERED, NOT SWALLOWED (§29, LAW 13). UiKit.Button gives a disabled face no
            // pressed state at all, so pressing ALREADY EQUIPPED IN SLOT 4 moved nothing on screen and read as
            // a dead control rather than as a NO. The depression is drawn here for the one case that has
            // something to refuse: a button that is off AND has a reason printed above it. A button that is off
            // with nothing to say no to — CHOSEN, OWNED, EQUIPPED IN THIS SLOT — is simply already true.
            var refused = !enabled && refusal.Length > 0 && Down(PrimaryBtn.Contains(hit));
            var face = refused ? new Rectangle(PrimaryBtn.X, PrimaryBtn.Y + 2, PrimaryBtn.Width, PrimaryBtn.Height) : PrimaryBtn;
            _ui.Button(b, face, label, hit, false, enabled, enabled ? ButtonStyle.Primary : ButtonStyle.Secondary);
            if (refused) _ui.Fill(b, UiKit.PanelInner(face), PressWash);
        }

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
                var k = DiscoveredKeystones.FirstOrDefault(ks => ks.Id == _pickKeystoneId);
                if (k is null) { _pick = Pick.Slot; return; }
                Head("KEYSTONE");
                _ui.TextBig(b, k.Name.ToUpperInvariant(), x, y, Ins(Loadout.HasKeystone(k.Id) ? Gold : Bone), UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Head("WHAT IT DOES"); Line(k.Blurb, Bone, UiTypography.Body, 6); Gap();
                Head("CURRENT STATE"); Line(Loadout.HasKeystone(k.Id) ? "IN USE" : "NOT IN USE", Bone);
                Line($"SOCKETS {Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity}"
                     + (NextSocketNote.Length > 0 ? $" — {NextSocketNote}" : ""), Slate, UiTypography.Secondary);
                // HOW MANY ARE STILL OUT THERE. A total, never a checklist — it says that more exist
                // and nothing about which, where, or how close, which is what keeps finding one a
                // discovery instead of the next tick of a list.
                var unfound = Keystones.Catalog.Count - DiscoveredKeystones.Count;
                if (unfound > 0)
                    Line(unfound == 1
                            ? "ONE MORE KEYSTONE IS STILL WAITING IN THE WORLD."
                            : $"{unfound} MORE KEYSTONES ARE STILL WAITING IN THE WORLD.",
                         Slate, UiTypography.Secondary);
                return;
            }
            if (def is null || (_pick == Pick.Slot && !known.Contains(def.Id)))
            {
                Head(_slot < skills.Count ? $"SLOT {_slot + 1}" : "NOTHING SELECTED");
                _ui.TextBig(b, "EMPTY", x, y, Ins(UiInk.Empty), UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Line("Pick a skill from the library and press EQUIP. A shared skill is unlocked by its road on the MASTERY tree, and stays unlocked while that road is taken. Your own SIGNATURE skill needs no road — it is always yours.", Slate);
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
                _ui.TextBig(b, v.Name.ToUpperInvariant(), x, y, Ins(chosen?.Name == v.Name ? Gold : Bone), UiTypography.Headline);
                _ui.TextRightBig(b, SourceName(v.Source), x + w, y + UiMetrics.Space(6), Ins(col), UiTypography.Body);
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
                _ui.TextBig(b, r.Name.ToUpperInvariant(), x, y, Ins(owned ? Met : Bone), UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
                Head("WHAT IT DOES"); Line(r.Line, Bone); Gap(); Rule();
                Head("YOU NEED FIRST");
                Line(owned ? "OWNED" : free > 0 ? $"ONE FREE LEVEL — YOU HAVE {free}" : $"LEVEL {level + 1} — {WavesToNext(def)} MORE WAVES WITH {def.Name.ToUpperInvariant()} EQUIPPED", owned ? Met : free > 0 ? Gold : Bone);
                _respecShown = true;
                return;
            }

            // A skill: from a slot, or from the library.
            var ownSignature = def.OwnerCharacterId is not null;
            if (!have && !Seen(def))
            {
                // THE UNKNOWN READING — a position in the library, not a skill. Nothing here reads the
                // definition beyond its Style, which the row it sits in already says.
                Head($"SKILL · {StyleName(def.Style)}");
                var seal = new Rectangle(x, y, TreeIcon, TreeIcon);
                if (!_ui.Icon(b, "icon_unknown_seal", seal, Ins(Color.White))) _ui.Diamond(b, seal, Ins(Unknown * 0.6f));
                _ui.TextBig(b, "???", seal.Right + UiMetrics.Space(12), y + UiMetrics.Space(8), Ins(Unknown), UiTypography.Headline);
                y += TreeIcon + UiMetrics.Space(8);
                Line("SOMETHING REMAINS UNDISCOVERED.", Slate);
                Gap(); Rule();
                Head("HOW IT IS REVEALED");
                Line($"TAKE {StyleName(def.Style)}'S ROAD ON THE MASTERY TREE. THE SKILL SHOWS ITS NAME THE MOMENT THE ROAD IS YOURS, AND KEEPS IT AFTER.", Bone, UiTypography.Body, 3);
                return;
            }
            Head($"{(ownSignature ? "SIGNATURE" : "SKILL")} · {StyleName(def.Style)} · {(def.TakesABeat ? "ACTIVE" : "PASSIVE")}{(slotOf >= 0 ? $" · SLOT {slotOf + 1}" : "")}",
                 ownSignature ? Gold : null);
            var ico = new Rectangle(x, y, TreeIcon, TreeIcon);
            _ui.Icon(b, $"icon_skill_{def.Id}", ico, Ins(have ? Gold : Slate));
            _ui.TextBig(b, _ui.ShortenBig(def.Name.ToUpperInvariant(), x + w - ico.Right - UiMetrics.Space(12), UiTypography.Headline), ico.Right + UiMetrics.Space(12), y + UiMetrics.Space(8), Ins(Bone), UiTypography.Headline);
            y += TreeIcon + UiMetrics.Space(8);
            Line(def.Line.ToUpperInvariant(), Bone);   // one case for the column (build-15): the tree cards uppercase the same sentence
            // EXCLUSIVE, AND SAID SO (BRIEF sec.11-12, LAW 1). A signature is the one skill on this screen
            // that no road opens and no other champion may ever hold.
            if (ownSignature && Character is { } owner)
                Line($"ONLY {owner.Name.ToUpperInvariant()} CAN USE THIS SKILL — IT NEEDS NO MASTERY ROAD", Gold, UiTypography.Secondary, 2);
            Gap(); Rule();
            Head("WHAT IT DOES");
            if (chosen is null)
            {
                foreach (var v in def.Variations.Take(2)) Line($"{v.Name.ToUpperInvariant()} · {SourceName(v.Source)} — {v.Line.ToUpperInvariant()}", Bone, UiTypography.Body, 2);
                if (have) Line("A VARIATION IS CHOSEN IN THE SKILL TREE — IT GIVES THE SKILL ITS SOURCE.", Slate, UiTypography.Secondary, 2);
            }
            else
            {
                Line($"{chosen.Name.ToUpperInvariant()} · {SourceName(chosen.Source)} — {chosen.Line.ToUpperInvariant()}", Bone, UiTypography.Body, 2);
                foreach (var r in chosen.Reinforcements.Where(r => SkillLevels.HasReinforcement(def.Id, r.Name))) Line($"{r.Name.ToUpperInvariant()} — {r.Line.ToUpperInvariant()}", Met, UiTypography.Body, 2);
            }
            Gap(); Rule();
            if (!have)
            {
                // LOCKED, WITH THE LEVEL IT KEEPS (BRIEF sec.20). The state comes first because it is the
                // thing that changed — a skill the player has spent waves on has not become a stranger.
                // ONE SHORT LINE. The refusal over the button says what would unlock it and that the level
                // is waiting, and the button says it again; a third, longer telling of the same fact only
                // pushed YOU NEED FIRST off the bottom of this column at 150 %.
                Head("CURRENT STATE");
                Line(level > 0 ? $"LOCKED · LEVEL {level} IS KEPT" : "LOCKED · NO LEVELS YET",
                     level > 0 ? Gold : Slate);
                Gap();
                Head("YOU NEED FIRST");
                var lockEdge = UiMetrics.Control(20);
                _ui.Icon(b, "ui_slot_locked", new Rectangle(x, y + 2, lockEdge, lockEdge), Ins(Bone));
                // WRAPPED, NOT TRIMMED. At 125 % this column cuts the line at "…ON THE MASTERY TR…", and a
                // reason that stops mid-word has not said why (brief §29). It is the only sentence here.
                var needX = x + lockEdge + UiMetrics.Space(8);
                foreach (var l in _ui.WrapBig($"TAKE {StyleName(def.Style)}'S ROAD ON THE MASTERY TREE", w - lockEdge - UiMetrics.Space(8), UiTypography.Body).Take(2))
                {
                    _ui.TextBig(b, l, needX, y, Ins(Bone), UiTypography.Body);
                    y += UiTypography.Pitch(UiTypography.Body);
                }
            }
            else
            {
                Head("CURRENT STATE");
                Line(level >= SkillProgress.MaxLevel ? $"LEVEL {level} · MAX" : $"LEVEL {level} · {SkillLevels.UsesOf(def.Id)}/{SkillProgress.UsesForLevel(level + 1)} WAVES TO THE NEXT{(free > 0 ? $" · +{free} TO SPEND" : "")}", free > 0 ? Gold : Bone);
                if (ChosenStyle is { } dd)
                {
                    var f = StyleAffinity.Factor(dd, def.Style, vowKept);
                    Line(f >= 1.99f ? $"YOUR STYLE IS {StyleName(dd)} — THIS SKILL HITS x2.0" : $"YOUR STYLE IS {StyleName(dd)} — THIS {StyleName(def.Style)} SKILL HITS x{f:0.0#}", f >= 1.99f ? Gold : Slate, UiTypography.Secondary, 2);
                }
                else Line("NO STYLE CHOSEN YET — A SPECIALISATION NODE ON THE MASTERY TREE CHOOSES ONE.", Slate, UiTypography.Secondary, 2);
                if (_pick == Pick.Library && slotOf >= 0 && slotOf != _slot)
                    Line($"EQUIPPED · SLOT {slotOf + 1} — A SKILL GOES IN ONE SLOT", Gold, UiTypography.Secondary);
                else if (_pick == Pick.Library && _slot < skills.Count && skills[_slot].SkillId != def.Id && SkillCatalogue.Find(skills[_slot].SkillId) is { } replacing)
                    Line($"EQUIP REPLACES {replacing.Name.ToUpperInvariant()} IN SLOT {_slot + 1}", Slate, UiTypography.Secondary);
            }
            _respecShown = slotOf >= 0 && have && SkillLevels.SpentOn(def.Id) > 0;

            // THE VOWS — the validator block, and it asks the BUILD, not this row.
            //
            // It used to read `skills[slotOf].VowId` and judge that one vow, which was honest while a
            // vow hung on a skill slot. A vow is a promise about the WHOLE BUILD now: it is validated
            // once against the build, it pays once however many rows carry it, and how many you may
            // hold is a number the world grants rather than a count of your skill slots. A slot is
            // still where a vow is STORED — that is the save shape, and it is where a player swears
            // one — but the block below reports the build's promises, all of them, judged together.
            if (_pick == Pick.Slot && slotOf >= 0 && have)
            {
                Gap(); Rule();
                // The COMPOSED build's own list, not the loadout's ids: it is deduplicated exactly as the
                // simulation deduplicates it, and it includes a vow the build did not swear but is paid
                // for anyway — THE KEPT WORD's loan. Reading the raw ids would under-report that loan,
                // and the screen would disagree with the fight about what is holding this hunter.
                var live = Live();
                var sworn = live.Build.Vows;
                var lentId = live.Build.BorrowedVow?.Id;
                if (_vowListOpen) { DrawVowList(b, hit, ref y, region); }
                else
                {
                    Head(sworn.Count > 1 ? "YOUR VOWS" : "YOUR VOW");
                    if (sworn.Count == 0)
                    {
                        // THE EMPTY SEAL: a dark medallion where a promise could go, and the one line that
                        // says how a promise is found. A card, like a sworn one, so the block keeps its
                        // shape whether or not a vow is in it.
                        var card = new Rectangle(x, y, w, UiMetrics.Control(64) + UiMetrics.Space(20));
                        _ui.Plate(b, card, null, _insAlpha);   // no accent: a Rule-grey bar read as a stray line (build-12)
                        var sealBox = new Rectangle(card.X + UiMetrics.Space(14), card.Y + UiMetrics.Space(10), UiMetrics.Control(64), UiMetrics.Control(64));
                        EmptySeal(b, sealBox, _insAlpha);
                        var tx0 = sealBox.Right + UiMetrics.Space(14);
                        _ui.TextBig(b, "NO VOW SWORN", tx0, card.Y + UiMetrics.Space(14), Ins(UiInk.Empty), UiTypography.Headline);
                        var hint = Known.Count == 0 ? "A VOW REVEALS ITSELF WHEN YOU KEEP ITS RULE WITHOUT IT" : "A PROMISE KEPT IS PAID ON EVERY SKILL";
                        // At Secondary, the floor for a sentence (build-12); the card has the room for one line.
                        foreach (var l in _ui.WrapBig(hint, card.Right - UiMetrics.Space(12) - tx0, UiTypography.Secondary).Take(1))
                            _ui.TextBig(b, l, tx0, card.Y + UiMetrics.Space(14) + UiTypography.Pitch(UiTypography.Headline), Ins(Slate), UiTypography.Secondary);
                        y = card.Bottom + UiMetrics.Space(8);
                    }
                    else
                    {
                        foreach (var v in sworn)
                        {
                            var holds = Vows.IsActive(v, ctx);
                            var lent = v.Id == lentId;
                            var accent = holds ? Met : Ember;
                            // THE SEAL CARD. Left: the medallion, cracked when broken. Right: the name, the
                            // reward, the risk, then ASKS / YOUR BUILD as two labelled lines, and the verdict
                            // as a ribbon along the card's foot. Everything on it is a Core fact.
                            var sealEdge = UiMetrics.Control(64);
                            var pad = UiMetrics.Space(12);
                            var tx0 = x + pad + sealEdge + UiMetrics.Space(14);
                            var tw = x + w - pad - tx0;
                            var verdict = holds ? "HOLDS — IT PAYS" : $"BROKEN — PAYS NOTHING UNTIL {DemandText(v)}";
                            var verdictLines = _ui.WrapBig(verdict, w - pad * 2, UiTypography.Secondary).Take(2).ToList();
                            var lentLines = lent ? _ui.WrapBig("YOU DID NOT SWEAR THIS — A TRAIT HOLDS YOU TO IT", w - pad * 2, UiTypography.Caption).Take(2).ToList() : new List<string>();
                            var bodyH = UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Secondary) * 2 + UiMetrics.Space(6);
                            var ribbonH = UiMetrics.Space(8) + verdictLines.Count * UiTypography.Pitch(UiTypography.Secondary) + lentLines.Count * UiTypography.Pitch(UiTypography.Caption) + UiMetrics.Space(6);
                            var card = new Rectangle(x, y, w, UiMetrics.Space(10) + Math.Max(sealEdge, bodyH) + UiMetrics.Space(10) + ribbonH);
                            _ui.Plate(b, card, Ins(accent), _insAlpha);
                            Outline(b, card, Ins(accent * 0.55f), 1);
                            var sealBox = new Rectangle(x + pad, card.Y + UiMetrics.Space(10), sealEdge, sealEdge);
                            VowSeal(b, v, sealBox, holds, _insAlpha);

                            var ly = card.Y + UiMetrics.Space(10);
                            var mult = $"x{Vows.Multiplier(v):0.00}";
                            var multW = _ui.MeasureBig(mult, UiTypography.Headline);
                            _ui.TextBig(b, _ui.ShortenBig(v.Name.ToUpperInvariant(), tw - multW - UiMetrics.Space(10), UiTypography.Headline), tx0, ly, Ins(holds ? Gold : Bone), UiTypography.Headline);
                            _ui.TextRightBig(b, mult, x + w - pad, ly, Ins(holds ? Gold : Slate), UiTypography.Headline);
                            ly += UiTypography.Pitch(UiTypography.Headline);
                            // ASKS · YOUR BUILD — the two lines a player checks a promise against.
                            var labelW = Math.Max(_ui.MeasureBig("ASKS", UiTypography.Caption), _ui.MeasureBig("YOUR BUILD", UiTypography.Caption)) + UiMetrics.Space(10);
                            _ui.TextBig(b, "ASKS", tx0, ly + 2, Ins(Slate), UiTypography.Caption);
                            _ui.TextBig(b, _ui.ShortenBig(DemandText(v), tw - labelW, UiTypography.Secondary), tx0 + labelW, ly, Ins(Bone), UiTypography.Secondary);
                            ly += UiTypography.Pitch(UiTypography.Secondary);
                            _ui.TextBig(b, "YOUR BUILD", tx0, ly + 2, Ins(Slate), UiTypography.Caption);
                            _ui.TextBig(b, _ui.ShortenBig(YourBuildText(v, ctx), tw - labelW, UiTypography.Secondary), tx0 + labelW, ly, Ins(holds ? Met : Ember), UiTypography.Secondary);
                            ly += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
                            // RISK pips under the seal's column, right-aligned beside the reward they buy.
                            var pip = UiMetrics.Control(8);
                            _ui.TextRightBig(b, "RISK", x + w - pad - RiskPipsWidth(pip) - UiMetrics.Space(6), ly - 1, Ins(Slate), UiTypography.Caption);
                            RiskPips(b, v, x + w - pad - RiskPipsWidth(pip), ly + 1, pip, Ins(holds ? Gold : Ember), Ins(Dim));

                            // THE RIBBON: the verdict along the foot, on the accent.
                            var ribbon = new Rectangle(card.X + 5, card.Bottom - ribbonH, card.Width - 6, ribbonH);
                            _ui.Fill(b, ribbon, Ins(accent) * 0.12f);
                            _ui.Fill(b, new Rectangle(ribbon.X, ribbon.Y, ribbon.Width, 1), Ins(accent) * 0.5f);
                            var ry = ribbon.Y + UiMetrics.Space(6);
                            foreach (var l in verdictLines) { _ui.TextBig(b, l, x + pad, ry, Ins(accent), UiTypography.Secondary); ry += UiTypography.Pitch(UiTypography.Secondary); }
                            // A vow the hunter never swore is paid for anyway, and a player who cannot see
                            // WHY a promise they did not make is holding them has been handed a mystery.
                            foreach (var l in lentLines) { _ui.TextBig(b, l, x + pad, ry, Ins(Slate), UiTypography.Caption); ry += UiTypography.Pitch(UiTypography.Caption); }
                            Tip(In(card, region), hit, v.Description);
                            y = card.Bottom + UiMetrics.Space(8);
                        }
                        // WHAT THEY PAY TOGETHER — one number, because that is how the fight bills them:
                        // every kept vow's bonus summed under one ceiling, and a broken one adds nothing.
                        if (sworn.Count > 1)
                            Pair("TOGETHER", $"x{Vows.CombinedFactor(sworn, ctx):0.00} ON EVERY SKILL");
                    }
                    if (Known.Count > 0)
                    {
                        _changeVowY = y + UiMetrics.Space(4);
                        _changeVowShown = true;
                        var btn = ChangeVowBtn(_changeVowY);
                        // A real Secondary button: a hairlined box with Slate text read as a disabled
                        // placeholder above the gold primary (build-10). The click stays in UpdateInput.
                        _ui.Button(b, btn, sworn.Count == 0 ? "SWEAR A VOW" : "CHANGE VOW", hit, false, true, ButtonStyle.Secondary);
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
                    _ui.TextBig(b, SourceName(enemy), x, y, Ins(SourceColor.GetValueOrDefault(enemy, Bone)), UiTypography.Body);
                    _ui.TextRightBig(b, $"{verdict}  x{mult:0.00}", x + w, y, Ins(mult > 1.01f ? Met : mult < 0.99f ? Ember : Slate), UiTypography.Body);
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

    /// <summary>The known vows, in place of the sections below the validator: click one to swear it. Every known vow is a row — the body scrolls, so the list no longer pages.</summary>
    private void DrawVowList(SpriteBatch b, Point hit, ref int y, Rectangle region)
    {
        var known = Known;
        var ctx = Context;
        var skills = Loadout.Skills;
        var sworn = _slot < skills.Count ? skills[_slot].VowId : null;
        var w = InsBodyW;
        _ui.TextBig(b, "SWEAR A VOW", InsX, y, Ins(Slate), UiTypography.Secondary);
        // HOW MANY DIFFERENT PROMISES THIS HUNTER MAY HOLD. A Vow is a promise about the build, so the
        // number shown counts DIFFERENT vows the build has sworn, however many rows happen to record
        // them — and the world grants the capacity. A capacity the player is refused by and never
        // shown is a rule they cannot learn.
        _ui.TextRightBig(b, $"{Loadout.SwornVows.Count} / {Loadout.VowCapacity} VOWS SWORN",
                         InsX + w, y, Ins(Slate), UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        // HOW MANY VOWS EXIST, AND HOW MANY ARE FOUND — a total, not a checklist, and never a
        // condition: every other Vow is found by keeping its rule once without it, and saying which
        // rule would be the one thing that turns a discovery back into a shopping list. Folded into
        // the capacity note's own line so the header costs no extra height at any UI SCALE.
        var note = $"{known.Count} OF {Vows.Catalog.Count} VOWS FOUND."
                 + (NextVowNote.Length > 0 ? $"  {NextVowNote}." : "");
        _ui.TextBig(b, _ui.ShortenBig(note, w, UiTypography.Caption), InsX, y,
                    Ins(Slate), UiTypography.Caption);
        y += UiTypography.Pitch(UiTypography.Caption);
        y += UiMetrics.Space(4);
        _vowListTop = y;
        var pad = UiMetrics.Space(12);
        // EVERY ROW IS A SEAL (release polish 2026-09-05): the medallion at the left, cracked where the
        // build would break it today; the name and what it asks beside it; the reward and the verdict at
        // the right. The right column is measured, so the vow's name gets every pixel the row can give it.
        var sealEdge = VowRowH - UiMetrics.Space(12);
        var textX = pad + sealEdge + UiMetrics.Space(10);
        var tail = Math.Max(_ui.MeasureBig("x0.00", UiTypography.Body), _ui.MeasureBig("BROKEN", UiTypography.Caption)) + pad + UiMetrics.Space(8);
        for (var r = 0; r <= known.Count; r++)
        {
            var idx = r - 1;   // row 0 is NO VOW
            var row = VowListRow(r, _vowListTop);
            var shown = In(row, region);
            var over = shown.Contains(hit);
            var nameY = row.Y + UiMetrics.Space(8);
            var subY = nameY + UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(4);
            var sealBox = new Rectangle(row.X + pad, row.Y + (row.Height - sealEdge) / 2, sealEdge, sealEdge);
            if (idx < 0)
            {
                Cell(b, row, Quiet, sworn is null, over);
                Outline(b, row, Ins(sworn is null ? Gold : over ? Slate : Dim), sworn is null ? 2 : 1);
                EmptySeal(b, sealBox, _insAlpha);
                _ui.TextBig(b, "NO VOW", row.X + textX, nameY, Ins(sworn is null ? Gold : Bone), UiTypography.Body);
                _ui.TextBig(b, _ui.ShortenBig("NO PROMISE, NO REWARD", row.Width - textX - pad, UiTypography.Caption), row.X + textX, subY, Ins(Slate), UiTypography.Caption);
                continue;
            }
            var v = known[idx];
            var live = Vows.IsActive(v, ctx);
            var on = v.Id == sworn;
            // A VOW CAPACITY WILL REFUSE IS DRAWN AS REFUSED, rather than as an option that does
            // nothing when clicked. Its own row still reads normally, because unswearing is allowed.
            var barred = !on && !Loadout.VowFitsCapacity(_slot, v.Id);
            Cell(b, row, Quiet, on, over);
            Outline(b, row, Ins(on ? Gold : over ? Slate : Dim), on ? 2 : 1);
            VowSeal(b, v, sealBox, live, barred ? 0.45f * _insAlpha : _insAlpha);
            var nameRoom = row.Width - textX - tail;
            _ui.TextBig(b, _ui.ShortenBig(v.Name.ToUpperInvariant(), nameRoom, UiTypography.Body), row.X + textX, nameY, Ins(on ? Gold : barred ? Dim : Bone), UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig("ASKS  " + DemandText(v), nameRoom, UiTypography.Caption), row.X + textX, subY, Ins(barred ? Dim : Slate), UiTypography.Caption);
            _ui.TextRightBig(b, $"x{Vows.Multiplier(v):0.00}", row.Right - pad, nameY, Ins(live ? Gold : Slate), UiTypography.Body);
            _ui.TextRightBig(b, live ? "HOLDS" : "BROKEN", row.Right - pad, subY, Ins(live ? Met : Ember), UiTypography.Caption);
            Tip(shown, hit, barred ? $"{v.Description} YOU MAY HOLD {Loadout.VowCapacity} VOW{(Loadout.VowCapacity == 1 ? "" : "S")} AT ONCE." : v.Description);
        }
        y = _vowListTop + (known.Count + 1) * VowRowPitch - UiMetrics.Space(4);
        _changeVowY = y + UiMetrics.Space(4);
        _changeVowShown = true;
        var btn = ChangeVowBtn(_changeVowY);
        _ui.Button(b, btn, "CLOSE", hit, false, true, ButtonStyle.Secondary);   // a real button (build-10); the click stays in UpdateInput
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
            _ui.Fill(b, r, Selected);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), col);
            Outline(b, r, Bone, 2);
            var pad = UiMetrics.Space(16);
            _ui.TextBig(b, _ui.ShortenBig(SkillCatalogue.Find(s.SkillId)?.Name ?? "EMPTY", r.Width - pad * 2, UiTypography.Body), r.X + pad, r.Y + (r.Height - UiTypography.Body) / 2, Bone, UiTypography.Body);
        }
    }

    /// <summary>A chain closing on a skill glyph — the flourish a newly sworn Vow plays over the row it was sworn at.</summary>
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

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
