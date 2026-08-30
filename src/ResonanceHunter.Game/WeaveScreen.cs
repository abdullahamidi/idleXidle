using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Prestige;

using ResonanceHunter.Core.Progression;
namespace ResonanceHunter.Client;

/// <summary>
/// THE WEAVE: pick each skill's Source and Form, and swear its Vow.
/// </summary>
/// <remarks>
/// <para>
/// This replaces a column of <c>&lt; VALUE &gt;</c> cycling cells wedged beside the mastery tree. Cycling
/// is the wrong verb for a list of six — picking SPIRIT from BODY was five clicks and five reads, and
/// nothing on screen ever said what the other five options were. Everything choosable is now visible
/// and one click away.
/// </para>
/// <para>
/// THE POINT OF THE SCREEN IS THE VOW COLUMN. A Vow pays a large multiplier only while the BUILD meets
/// its demand — one Form, no crit investment, a bare gear slot — and it pays nothing at all when the
/// demand is unmet. That check already existed and was already correct (<c>Weaving.IsActive</c>), but it
/// ran inside the simulation, which is to say: after the player had descended, where they could not see
/// it. A Vow whose condition you cannot check before you leave is a coin flip wearing a decision's
/// clothes. <c>SoloBattle.DescribeBuild</c> is pure and public, so this screen asks the same question
/// against the live loadout and shows the answer next to every Vow, updating as you edit.
/// </para>
/// </remarks>
public sealed class WeaveScreen
{
    /// <summary>
    /// The build's DISCIPLINE — the Form specialisation taken on the mastery tree. Host-fed each
    /// frame. Null until a Specialisation node is bought, and the screen says where to get one.
    /// </summary>
    public Form? Discipline { get; set; }

    /// <summary>Host-fed mastery walk, for the build share code. The code is identity, not power.</summary>
    public System.Collections.Generic.IReadOnlyCollection<string> MasteryTaken { get; set; }
        = System.Array.Empty<string>();

    // COPY BUILD CODE — moved HERE from the build overview after the adversarial review found the
    // overview had been retired a commit earlier: the button was drawn on a page no player can
    // reach. This screen is where builds are woven, which is where sharing them belongs.
    private static readonly Rectangle CopyCodeBtn = new(1389, 918, 340, 48);
    private int _copyToastFrames;
    private string _copyToast = "";

    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Quiet = new(0x16, 0x12, 0x20, 0xE0);

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Body] = new Color(0xD6, 0x48, 0x5C), [Source.Mind] = new Color(0x74, 0xC6, 0xE8),
        [Source.Nature] = new Color(0x48, 0xB8, 0x88), [Source.Machine] = new Color(0xE0, 0x8A, 0x3A),
        [Source.Shadow] = new Color(0x8A, 0x6E, 0xE0), [Source.Spirit] = new Color(0xC8, 0xC0, 0xE8),
    };

    private readonly UiKit _ui;
    private int _slot;
    private string? _readingVowId;
    private string _msg = "";
    private int _vowScroll;

    /// <summary>First VISIBLE keystone. Three fit; the tree teaches far more than three.</summary>
    /// <remarks>
    /// The list used to be drawn `for (i = 0; i < 3; i++)` straight off the learned collection, and 3 is
    /// the SOCKET count, not a list length — a player who learned a fourth keystone could never see it,
    /// let alone choose it over the three the catalogue happened to order first. Sockets stay scarce;
    /// the CHOICE of what goes in them is the decision the trait tree's roads are selling.
    /// </remarks>
    private int _keystoneScroll;

    // ── The build readout ───────────────────────────────────────────────────────────────────────
    // DamageBench measures a build against a reference dummy and has lived in Core, fully tested, with
    // ZERO callers in the game — the exact "built and never reaches the player" shape this codebase
    // keeps producing. It is what this screen was missing: the player picks from thirty-six
    // combinations and had no way to see what any of them DID to their damage.
    //
    // Cached by the thing being previewed, because a reading costs ~0.5ms and only changes when the
    // hover or the build does. Measured, not assumed — a per-frame bench would have been fine too, but
    // the cache makes that a fact rather than a hope.
    private (Source S, Form F, int Slot, int Rev)? _previewKey;
    private float _previewDps;
    private float _currentDps;
    private int _currentRev = -1;
    private int _buildRev;

    // ── DRAGGING ────────────────────────────────────────────────────────────────────────────────
    //
    // Playtest 2026-08-28: <i>"skill seçimlerinin tıkladım oldu gibi basit bir aksiyondan ziyade,
    // sürükleme bırakma ... ile pekiştirilmesi"</i> and, for the Vow column, <i>"skille sürükleme
    // bırakma ve 'bind' hissiyatı için efekt ve seslerle desteklenmesi"</i>.
    //
    // A Vow BINDS BY BUTTON, not by carrying it (designer's call, 2026-08-28): clicking a seal opens
    // a BIND row directly beneath it, and that row is what commits. Dragging a seal was tried first
    // and dropped — the gesture was fine and the reachability was not, since drag needs a pointer and
    // this project targets gamepad through cycle-and-confirm (technical-preferences.md). A button is
    // one target, states its consequence, and every input can press it.
    //
    // The SLOT carry stays: reordering is the one decision here with no other control, and the order
    // is cast priority.
    private enum Carry { None, Slot }

    private Carry _carrying;
    private int _carrySlot = -1;      // Carry.Slot — which woven skill is being reordered
    private Point _carryFrom;         // where the press began, to tell a drag from a click
    private Point _carryAt;           // the cursor now, for the ghost
    private bool _carryMoved;         // past the slop radius: this is a drag, not a click
    private bool _wasHeld;

    /// <summary>How far the cursor must travel before a press becomes a drag.</summary>
    /// <remarks>
    /// Generous on purpose. A player aiming at a 70px row with a mouse moves a pixel or two while
    /// clicking, and a drag that arms at 2px turns every click into a cancelled drag.
    /// </remarks>
    private const int DragSlop = 7;

    /// <summary>How long a pick's flourish and a bind's seal last, in seconds.</summary>
    /// <remarks>
    /// The bind runs longer than the pick because it is the heavier act and because sfx_bind's own
    /// ring hangs for about that long — a flourish that ends before its sound does reads as two
    /// unrelated events.
    /// </remarks>
    private const float SetFlashSeconds = 0.34f;
    private const float BindFlashSeconds = 0.85f;

    /// <summary>
    /// A CHAIN CLOSING ON A SKILL — the flourish a bound Vow plays over its row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Designer's brief (2026-08-28): <i>"bind edilince de skill üzerinde bir zincirleme efekt ve
    /// animasyonu (genişleyip sıkışan zincir animasyonu olabilir) oynasın"</i>.
    /// </para>
    /// <para>
    /// A REAL ASSET, not a ring of drawn diamonds. The first pass composed the chain out of UiKit
    /// primitives and the designer's note on it was the whole point of this round: <i>"kendin bir
    /// kutucuk veya buton oluşturup görsel olarak onu kullanıyorsun"</i>. <c>fx_bind_chain</c> is an
    /// eight-frame strip generated through the same route as every arena effect (arena-art-contract.md
    /// §5-6) — heavy interlocking gold links with inward spikes, drawn wide and closing.
    /// </para>
    /// <para>
    /// It plays over the SOURCE MEDALLION rather than the row's outline, which is what the asset's own
    /// shape asks for: a ring wraps a disc, not a 440x76 rectangle. It also puts the flourish on the
    /// half of the row that identifies the skill — the thing the Vow is being bound to.
    /// </para>
    /// <param name="t">1 at the instant of binding, falling to 0 as the flourish ends.</param>
    /// </remarks>
    private void DrawBindChain(SpriteBatch b, Rectangle row, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var played = 1f - t;                   // 0 at the bind, 1 when the flourish ends

        // The plate lights under the chain and fades faster, so the chain is what is left to look at.
        _ui.Fill(b, row, Gold * (0.22f * t * t));

        // Centred on the gem, and it OVERSHOOTS it: the strip's own art contracts across its frames,
        // so the box only has to start clear of the medallion and end tight on it for the two motions
        // to read as one. Alpha holds through the close and lets go at the end.
        var gem = new Rectangle(row.X + 28, row.Y + 12, 52, 52);
        var wide = 46f * t * t;
        var box = new Rectangle((int)(gem.X - wide), (int)(gem.Y - wide),
                                (int)(gem.Width + wide * 2), (int)(gem.Height + wide * 2));
        var alpha = Math.Clamp(t * 1.9f, 0f, 1f);

        // ChainClipSeconds, not the flourish's own length: the strip is authored to close over its
        // eight frames and is played once, held on the last. Driving it off `played` means the art's
        // contraction and the box's contraction finish together.
        if (!_ui.AnimSprite(b, "fx_bind_chain_strip8_512", box, played * ChainClipSeconds, 8f,
                            loop: false, Color.White * alpha))
        {
            // The asset is missing: say so with the row's own gold rather than drawing nothing, so a
            // stripped build still shows that something was bound.
            Outline(b, row, Gold * alpha, 3);
        }
    }

    /// <summary>How long the chain strip takes to play its eight frames, in seconds.</summary>
    /// <remarks>Shorter than the flourish it rides, so the links are CLOSED for the last of it.</remarks>
    private const float ChainClipSeconds = 0.55f;

    /// <summary>Which woven slot the cursor is over, or -1. The drop target for both carries.</summary>
    /// <remarks>
    /// PADDED by half a row gap, per technical-preferences.md's Fitts's-Law note: a drop that must
    /// land inside 76 exact pixels is a drop that misses. The pad closes the dead gap BETWEEN rows
    /// rather than growing the list, so every point inside the slot column belongs to some row.
    /// </remarks>
    private int SlotUnder(Point p)
    {
        for (var i = 0; i < Loadout.Skills.Count; i++)
        {
            var r = SlotRow(i);
            if (new Rectangle(r.X - 8, r.Y - 5, r.Width + 16, r.Height + 10).Contains(p)) return i;
        }
        return -1;
    }

    /// <summary>Age the flourishes. Called once per frame from Draw, off this screen's own clock.</summary>
    private void TickEffects()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = (float)Math.Clamp(now - _lastTick, 0.0, 0.10);   // clamped: a load hitch must not skip a flash
        _lastTick = now;
        Decay(_slotFlash, dt);
        if (!_devHoldChain) Decay(_bindFlash, dt);

        static void Decay(Dictionary<int, float> d, float dt)
        {
            if (d.Count == 0) return;
            foreach (var k in d.Keys.ToList())
            {
                var left = d[k] - dt;
                if (left <= 0f) d.Remove(k); else d[k] = left;
            }
        }
    }

    // ── EFFECTS ─────────────────────────────────────────────────────────────────────────────────
    // Seconds remaining on each flourish. Decayed in Draw off a stopwatch, because this screen's
    // Update takes no GameTime and threading one through for two timers is a worse trade than the
    // four lines below.
    private readonly Dictionary<int, float> _slotFlash = new();   // slot -> a Source/Form was set
    private readonly Dictionary<int, float> _bindFlash = new();   // slot -> a Vow was bound

    /// <summary>DEV: freeze the bind chain where <see cref="DevPose"/> put it, for a capture.</summary>
    private bool _devHoldChain;
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _lastTick;

    /// <summary>Cues, host-fed like every other screen's.</summary>
    public SoundBank? Sound { get; set; }

    public WeaveScreen(UiKit ui) => _ui = ui;

    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MasteryTree Mastery { get; set; } = new();
    public MemoryDustTree Tree { get; set; } = new();

    /// <summary>
    /// What each skill has earned by being used, and where the player spends it.
    /// </summary>
    /// <remarks>
    /// Without this the variation system is unreachable: the skills level, the fight reads whatever
    /// was chosen, and there is nowhere to choose. A progression the player cannot spend is the same
    /// dead weight as a field nothing reads.
    /// </remarks>
    public SkillProgress SkillLevels { get; set; } = new();
    public Hunter? Hunter { get; set; }
    public Character? Character { get; set; }

    /// <summary>Where the champion is hunting, so a Source pick can be judged against real creatures.</summary>
    /// <remarks>
    /// The screen told the player "BODY is strong against MIND and NATURE" and stopped there — a rule
    /// with no board to play it on. The region's roster is what turns that into a decision, and it was
    /// available in Core the whole time (<c>BandCycles.RosterFor</c>) with nothing on this screen asking.
    /// </remarks>
    public string RegionId { get; set; } = "";
    public string RegionName { get; set; } = "";

    /// <summary>Set when the loadout changed, so the host can save. Same shape as the other editors.</summary>
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    /// <summary>DEV: pose a slot and a vow for the capture fixture.</summary>
    /// <param name="chainAt">
    /// DEV: hold the bind chain part-played over this slot, so the flourish is photographable. It runs
    /// for under a second in play, which is exactly long enough to be impossible to capture by hand.
    /// </param>
    public void DevPose(int slot, string? vowId, float chainAt = 0f)
    {
        _slot = slot;
        _readingVowId = vowId;
        if (chainAt > 0f)
        {
            // HELD, not merely started. The flourish is over inside a second, and a capture run draws
            // several frames before it writes the file — so a posed chain that decays like a live one
            // is a chain nobody can photograph. Same idea as the hunt screen's DevShowFall.
            _bindFlash[slot] = BindFlashSeconds * Math.Clamp(chainAt, 0f, 1f);
            _devHoldChain = true;
        }
    }

    // ── Layout ────────────────────────────────────────────────────────────────────────────────────
    // 800, not 718. The left column's height is DATA — one row per skill slot, then the add button,
    // then the keystone sockets — and the fifth weave pushed the third socket 68px through the frame.
    // (At four slots it already cleared the interior by 12; the fifth only made it obvious.) All three
    // grow together so the row of panels still reads as a row.
    /// <summary>
    /// The way out. Set when the player asked to go back; the host clears it and opens BUILD.
    /// </summary>
    /// <remarks>
    /// THIS SCREEN HAD NO EXIT. It is opened FROM the Build overview, and the only way to leave was T —
    /// which toggles, so it landed on the HUNT rather than back where you came from. A sub-screen you
    /// can enter and not return from teaches the player to avoid entering it.
    /// </remarks>
    public bool WantsBack { get; set; }

    /// <summary>Top-left, in the band the centred title leaves empty on both sides.</summary>
    private static readonly Rectangle BackBtn = new(38, 26, 240, 46);

    // 850, not 800 — bottom 994, which is canvas 890 and still 168 canvas px clear of the picture's
    // floor. The three columns keep a shared baseline. Aspects 0.612 / 0.753 / 0.755 stay in
    // ui_panel_vertical's bucket (< 0.82), so no frame art changes.
    private static readonly Rectangle SlotsPanel = new(38, 144, 520, 850);
    private static readonly Rectangle PickPanel = new(578, 144, 640, 850);
    private static readonly Rectangle VowPanel = new(1238, 144, 642, 850);

    private static int SlotColX => UiKit.ContentLeft(SlotsPanel);
    private static int SlotColW => SlotsPanel.Width - UiKit.PadX(SlotsPanel) * 2;
    private static Rectangle SlotRow(int i) => new(SlotColX, UiKit.BodyTopBare(SlotsPanel) + i * 86, SlotColW, 76);
    private static Rectangle DropX(int i) { var r = SlotRow(i); return new(r.Right - 34, r.Y + 4, 30, 30); }

    /// <summary>
    /// The ACTIVE / PASSIVE switch on a woven row — the only door to two of the twelve skills.
    /// </summary>
    /// <remarks>
    /// A style has two skills and the SLOT decides which one this is. AURA and TRAP resolve to their
    /// styles' passives from either side, so FIELD's PULSE and SNARE's REPAY cannot be reached at all
    /// without this. Padded generously beyond its text, per the input rules — a 20px word is not a
    /// click target.
    /// </remarks>
    /// <summary>One of a skill's two variation buttons, drawn under its name.</summary>
    private static Rectangle VariationBtn(int i, int which)
    {
        var r = SlotRow(i);
        var w = (r.Right - 140 - (r.X + 134)) / 2 - 3;
        return new(r.X + 134 + which * (w + 6), r.Y + 26, w, 20);
    }

    private static Rectangle KindToggle(int i)
    {
        var r = SlotRow(i);
        return new(r.Right - 132, r.Y + 4, 92, 28);
    }

    /// <summary>Everything below the slot list hangs off the CAPACITY, not off the type's floor.</summary>
    private int SlotsEnd => UiKit.BodyTopBare(SlotsPanel) + Loadout.SkillCapacity * 86;

    private Rectangle AddBtn => new(SlotColX, SlotsEnd, SlotColW, 48);

    // KEYSTONE SOCKETS live here too. They were chips at the bottom of the deleted sidebar, and a
    // keystone is a loadout decision exactly like a Vow is — the trait tree TEACHES them, this screen
    // is where you decide which of them you are carrying. Losing the only UI for them along with the
    // sidebar would have been a silent regression: the tree would keep selling a payoff with nowhere
    // left to equip it.
    /// <summary>
    /// How many keystone chips are on screen at once. A WINDOW, not the socket count.
    /// </summary>
    /// <remarks>
    /// TWO AT FIVE SLOTS, because everything below the slot list hangs off SkillCapacity and the fifth
    /// slot pushes the chips 86px down. The list already scrolls, so a shorter window costs a scroll;
    /// a third chip at five slots costs the WHAT THIS BUILD DOES readout entirely — see DrawReadout.
    /// </remarks>
    private int KeystoneRows => Loadout.SkillCapacity >= 5 ? 2 : 3;

    // 94/46/42, measured against the WORST case rather than the current one: at five slots SlotsEnd is
    // 670, so a 102/48/44 row ends at 912 against a panel interior that closes at 904. Eight pixels, and
    // the third chip is drawn on the frame.
    private Rectangle KeystoneChip(int i) => new(SlotColX, SlotsEnd + 94 + i * 46, SlotColW, 42);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>
    /// An instance method, unlike the other screens': the slot rows and the keystone sockets under them
    /// sit where the loadout's capacity puts them, so the light has to be measured against the live one.
    /// </remarks>
    internal Rectangle[] Spotlights(TourTarget target)
    {
        switch (target)
        {
            case TourTarget.SkillSlots:
                return new[] { new Rectangle(SlotsPanel.X, SlotsPanel.Y, SlotsPanel.Width, SlotsEnd + 60 - SlotsPanel.Y) };
            case TourTarget.SkillPicker:
                return new[] { PickPanel };
            case TourTarget.Vows:
                var top = KeystoneChip(0).Y - 54;
                var sockets = new Rectangle(SlotsPanel.X, top, SlotsPanel.Width, KeystoneChip(KeystoneRows - 1).Bottom + 16 - top);
                return new[] { VowPanel, sockets };
            default:
                return Array.Empty<Rectangle>();
        }
    }

    private static readonly Source[] Sources = Enum.GetValues<Source>();
    private static readonly Form[] Forms = Enum.GetValues<Form>();

    // Every vertical offset here was measured off a capture, not guessed. The first pass put the FORM
    // heading at +356 while the source grid's second row ran to +364, so the heading printed straight
    // through the bottom of the gems.
    // THE GRID SPANS THE PANEL'S OWN MARGIN. It used to inset 74 a side with a comment claiming the
    // frame ate 70 — this panel wears the VERTICAL frame, whose side rail is 24, so 34 px of each
    // margin was nothing but a narrower column. Three cells and their two gaps now fill the content
    // width exactly, which is also what makes the right margin equal the left.
    private static int PickPad => UiKit.PadX(PickPanel);
    private static int PickInner => PickPanel.Width - PickPad * 2;
    // 122, from 104. The strip of description that used to close this panel is gone — it is a hover
    // card beside the cursor now (playtest 2026-08-28: "açıklamaların altta bir bölümde değil de
    // seçeneğe hover yapılınca hover paneliyle çıkması") — and most of the height it was eating goes
    // to the twelve cells that are the actual content of the panel.
    //
    // NOT ALL OF IT, and the arithmetic is written down because a first pass took 140 and silently
    // lost the footer. The panel gives 728px between its body line (236) and its floor (964); the two
    // grids cost 4*CellH plus two 16px gaps plus the 104px FORM heading block, and DrawSlotBanner
    // needs ~80 under them. 4*122 + 154 + 80 = 722, which fits with six to spare. At 140 the grids
    // alone reached 950 and the banner's own guard dropped it — dead space where the footer should be.
    private const int CellGap = 16, CellH = 122, CellRow = CellH + CellGap;
    private static int CellW => (PickInner - CellGap * 2) / 3;
    private static int CellPitch => CellW + CellGap;

    /// <summary>The SOURCE grid's first row — the panel's standard body line.</summary>
    private static int SourceTop => UiKit.BodyTop(PickPanel);

    /// <summary>FORM's heading, a clear gap under the last SOURCE cell.</summary>
    private static int FormTitleTop => SourceTop + CellRow + CellH + 34;

    /// <summary>FORM's first cell row — its heading and caption, on the same rhythm the panel's own use.</summary>
    private static int FormTop => FormTitleTop + (UiTypography.PanelBodyTop - UiTypography.PanelTitleTop);

    private static Rectangle SourceCell(int i) =>
        new(PickPanel.X + PickPad + i % 3 * CellPitch, SourceTop + i / 3 * CellRow, CellW, CellH);

    private static Rectangle FormCell(int i) =>
        new(PickPanel.X + PickPad + i % 3 * CellPitch, FormTop + i / 3 * CellRow, CellW, CellH);

    // Five rows, not six. Six fitted only by squeezing each to 54px, where a Vow's name and its
    // verdict printed over one another.
    // Five again. This was cut to four when the panel was 718 tall and a three-line description ran
    // out through the bottom ornament; at 800 both fit, and the reading block is still bounded so the
    // next long Vow ellipsises instead of escaping.
    private const int VowRows = 5;
    private const int VowRowH = 70;
    private static int VowColX => UiKit.ContentLeft(VowPanel);
    private static int VowColW => VowPanel.Width - UiKit.PadX(VowPanel) * 2;

    /// <summary>The BIND row's height, plus the gap that separates it from the seal above it.</summary>
    private const int VowBindH = 42, VowBindGap = 6;

    /// <summary>
    /// The visible row whose BIND control is open — the seal the player last clicked, or -1.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="_readingVowId"/> every time it is asked rather than stored as an index:
    /// the list scrolls, and an index into a scrolling window is a stale number waiting to happen.
    /// </remarks>
    private int OpenBindRow(IReadOnlyList<Vow> known)
    {
        if (_readingVowId is null) return -1;
        for (var r = 0; r < VowRows; r++)
        {
            var idx = _vowScroll + r;
            if (idx < known.Count && known[idx].Id == _readingVowId) return r;
        }
        return -1;
    }

    /// <summary>How far rows below the open seal are pushed down.</summary>
    private int VowShift(int row, int openRow) => openRow >= 0 && row > openRow ? VowBindH + VowBindGap : 0;

    private Rectangle VowRow(int i, int openRow)
        => new(VowColX, UiKit.BodyTop(VowPanel) + i * (VowRowH + 8) + VowShift(i, openRow), VowColW, VowRowH);

    /// <summary>The BIND control, directly under the seal it belongs to.</summary>
    private Rectangle VowBindBtn(int openRow)
    {
        var above = VowRow(openRow, openRow);
        return new Rectangle(VowColX + 16, above.Bottom + VowBindGap, VowColW - 32, VowBindH);
    }

    private Rectangle VowClearAt(int openRow)
        => new(VowColX, UiKit.BodyTop(VowPanel) + VowRows * (VowRowH + 8) + 4
                        + (openRow >= 0 ? VowBindH + VowBindGap : 0), VowColW, 44);

    private static string FormName(Form f) => f.ToString().ToUpperInvariant();
    private static string SourceName(Source s) => s.ToString().ToUpperInvariant();

    /// <summary>What a Vow demands, in one line the player can check against their own build.</summary>
    private static string DemandText(Vow v) => v.Demand switch
    {
        VowDemand.SingleForm => "EVERY SKILL THE SAME FORM",
        VowDemand.SingleSource => "EVERY SKILL THE SAME SOURCE",
        VowDemand.EveryWeaveFilled => "NO EMPTY SKILL SLOT",
        VowDemand.NoCritInvestment => "NO CRITICAL BONUS",
        // SHORTENED SO THE NUMBER SURVIVES. "SKILL RATE AT OR BELOW 1.20x" is wider than the 338px the
        // row leaves, and Fit() cut it at "SKILL RATE AT OR BE..." — deleting the threshold, which is
        // the entire content of the demand.
        //
        // MAX/MIN rather than the obvious mathematical symbols: check_font_coverage.py refused U+2264
        // and U+2265, because the shipping font's proven set is ASCII plus nine marks and an unproven
        // glyph draws as NOTHING. The gate caught it on the frame after I wrote it — which is the whole
        // reason it exists, and worth recording as a case where it earned its keep.
        VowDemand.CadenceAtOrBelow => $"SKILL RATE MAX {v.Threshold:0.##}x",
        VowDemand.CadenceAtOrAbove => $"SKILL RATE MIN {v.Threshold:0.##}x",
        VowDemand.NoDefence => "NO DEFENCE AT ALL",
        VowDemand.NoKeystone => "NO KEYSTONE IN USE",
        VowDemand.SlotLeftBare => $"{v.Bare.ToString().ToUpperInvariant()} SLOT LEFT EMPTY",
        _ => "NO DEMAND — ALWAYS ON",
    };

    private IReadOnlyList<Vow> Known => DustEffects.KnownVows(Tree);

    /// <summary>
    /// The live build, described to the Vow layer — the same struct the simulation judges against.
    /// </summary>
    /// <remarks>
    /// Rebuilt every frame rather than cached. It has to be: the whole value of this screen is that
    /// changing a Form flips a Vow from UNMET to MET while you watch, and a cache is how that stops
    /// being true one edit later.
    /// </remarks>
    private WeaveContext Context =>
        Hunter is { } h ? SoloBattle.DescribeBuild(Loadout.ToBuild(Tree, Mastery, Character), h) : WeaveContext.Empty;

    public void Update(Point mouse, bool clicked, bool held, int wheel)
    {
        var hit = Game1.ToOverlay(mouse);
        var skills = Loadout.Skills;
        var known = Known;

        _carryAt = hit;
        var released = _wasHeld && !held;
        _wasHeld = held;

        // A CARRY CANNOT OUTLIVE THE PRESS THAT STARTED IT. Update stops being called when the player
        // navigates away, so a button released on another screen never reaches the release path below —
        // and the carry would still be armed on the way back, resolving against wherever the cursor
        // happened to land. Any frame with the button up and no release to handle clears it.
        if (!held && !released && _carrying != Carry.None)
        {
            _carrying = Carry.None; _carrySlot = -1; _carryMoved = false;
        }
        if (_carrying != Carry.None && held
            && (Math.Abs(hit.X - _carryFrom.X) > DragSlop || Math.Abs(hit.Y - _carryFrom.Y) > DragSlop))
            _carryMoved = true;

        // ── A DRAG ENDS ─────────────────────────────────────────────────────────────────────────
        //
        // On RELEASE, and only a drag that actually moved is treated as one. A press that never left
        // its slop radius already did its work through the click path below, so resolving it again
        // here would bind twice — and, on the slot carry, would reorder on every ordinary click.
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
                    _slot = onto;
                    Dirty = true; _buildRev++;
                    _slotFlash[onto] = SetFlashSeconds;
                    Sound?.Play("sfx_weave", 0.5f);
                    // SLOT ORDER IS CAST PRIORITY, so the message names the consequence rather than
                    // the gesture — "moved" would describe the mouse, not the build.
                    _msg = onto == 0 ? "FIRST IN LINE — IT WINS EVERY TIED BEAT." : $"NOW SLOT {onto + 1}.";
                }
                return;
            }
        }

        if (wheel != 0 && VowPanel.Contains(hit))
            _vowScroll = Math.Clamp(_vowScroll - wheel, 0, Math.Max(0, known.Count - VowRows));
        if (wheel != 0 && SlotsPanel.Contains(hit))
            _keystoneScroll = Math.Clamp(_keystoneScroll - wheel, 0,
                                         Math.Max(0, DustEffects.LearnedKeystones(Tree).Count - KeystoneRows));

        if (!clicked) return;

        for (var i = 0; i < skills.Count; i++)
        {
            // The X first: it sits inside the row, so testing the row first would swallow it.
            if (skills.Count > 1 && DropX(i).Contains(hit))
            {
                Loadout.RemoveSkill(i);
                _slot = Math.Max(0, Math.Min(_slot, Loadout.Skills.Count - 1));
                Dirty = true; _buildRev++;
                _msg = "SLOT UNWOVEN.";
                return;
            }
            {
                // TAKING A VARIATION. Only offered while a level is free and none is taken, which is
                // the same condition the row draws them under — the button and the rule are one test.
                var kindsNow = BuildComposer.SlotKinds(
                    skills.Select(k => new BuildComposer.SkillPick(k.Source, k.Form, k.VowId, "", k.Passive)).ToList(),
                    Loadout.SkillCapacity);
                var rd = SkillCatalogue.Resolve(skills[i].Form, i < kindsNow.Count && kindsNow[i]);
                var takenVar = SkillLevels.VariationOf(rd);
                if (takenVar is null && SkillLevels.FreeOn(rd.Id) > 0)
                    for (var vi = 0; vi < rd.Variations.Count && vi < 2; vi++)
                        if (VariationBtn(i, vi).Contains(hit))
                        {
                            SkillLevels.ChooseVariation(rd, rd.Variations[vi].Name);
                            _slot = i; Dirty = true; _buildRev++;
                            _msg = $"{rd.Name} IS NOW {rd.Variations[vi].Name}.";
                            return;
                        }
                // BUYING A REINFORCEMENT. Same strip, same rule as the draw below: a variation is
                // taken and a level is spare. Only the UNBOUGHT ones are offered, so the list shortens
                // as it is spent and the last purchase leaves the plain name line behind it.
                else if (takenVar is not null && SkillLevels.FreeOn(rd.Id) > 0)
                {
                    var open = takenVar.Reinforcements
                        .Where(r => !SkillLevels.HasReinforcement(rd.Id, r.Name)).ToList();
                    for (var ri = 0; ri < open.Count && ri < 2; ri++)
                        if (VariationBtn(i, ri).Contains(hit))
                        {
                            SkillLevels.TakeReinforcement(rd, open[ri].Name);
                            _slot = i; Dirty = true; _buildRev++;
                            _msg = $"{open[ri].Name}: {open[ri].Line.ToUpperInvariant()}";
                            return;
                        }
                }
            }

            if (KindToggle(i).Contains(hit))
            {
                // FLIPPING THE SLOT CHANGES WHICH SKILL IT IS, not merely when it acts — a style's
                // two skills are different abilities. Say which one it became, because the row's
                // name line changes underneath the click and an unexplained change reads as a bug.
                // What it is RIGHT NOW, resolved the way the fight resolves it — an unset choice is
                // not the same as ACTIVE, so the flip reads the effective kind rather than the field.
                var effective = BuildComposer.SlotKinds(
                    skills.Select(k => new BuildComposer.SkillPick(k.Source, k.Form, k.VowId, "", k.Passive)).ToList(),
                    Loadout.SkillCapacity);
                var nowPassive = !(i < effective.Count && effective[i]);
                if (Loadout.SetPassive(i, nowPassive))
                {
                    _slot = i;
                    Dirty = true; _buildRev++;
                    var became = SkillCatalogue.Resolve(Loadout.Skills[i].Form, nowPassive);
                    _msg = $"NOW {became.Name} — {(nowPassive ? "COSTS NO ACTION" : "TAKES AN ACTION")}.";
                }
                return;
            }
            if (SlotRow(i).Contains(hit))
            {
                _slot = i; _msg = "";
                // ARMED, not acted on: if the cursor leaves the row while held this becomes a reorder,
                // and if it does not, selecting the slot (already done) was the whole click.
                _carrying = Carry.Slot; _carrySlot = i; _carryFrom = hit; _carryMoved = false;
                return;
            }
        }

        if (skills.Count < Loadout.SkillCapacity && AddBtn.Contains(hit))
        {
            // The guard above is the negation of the only way AddSkill refuses, and the button is only
            // drawn under the same guard — the old else-branch ("NO MORE SLOTS…") had never rendered.
            _slot = Loadout.AddSkill();
            Dirty = true; _buildRev++; _msg = "SLOT WOVEN.";
            return;
        }

        var learned = DustEffects.LearnedKeystones(Tree);
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned.Count - KeystoneRows));
        for (var i = 0; i < KeystoneRows && _keystoneScroll + i < learned.Count; i++)
        {
            if (!KeystoneChip(i).Contains(hit)) continue;
            if (Loadout.ToggleKeystone(learned[_keystoneScroll + i].Id, learned)) { Dirty = true; _buildRev++; _msg = ""; }
            else _msg = $"ONLY {Loadout.KeystoneCapacity} SOCKET(S) — MORE IN TRAITS (P).";
            return;
        }

        if (CopyCodeBtn.Contains(hit))
        {
            var code = ResonanceHunter.Core.Persistence.ShareCodes.EncodeBuild(
                new ResonanceHunter.Core.Persistence.ShareCodes.SharedBuild
                {
                    Skills = Loadout.Skills.Select(s => new ResonanceHunter.Core.Persistence.SavedSkill
                    {
                        Source = s.Source.ToString(), Form = s.Form.ToString(), VowId = s.VowId,
                    }).ToList(),
                    Keystones = Loadout.KeystoneIds.ToList(),
                    Mastery = MasteryTaken.ToList(),
                });
            // The failure is NOT silent (the review's other note): no clipboard, no lie.
            _copyToast = ClipboardInterop.TrySet(code)
                ? "COPIED — A FRIEND PASTES IT IN THE VAULT"
                : "COPY FAILED — TRY AGAIN";
            _copyToastFrames = 240;
            return;
        }

        if (_slot >= skills.Count) return;

        for (var i = 0; i < Sources.Length; i++)
            if (SourceCell(i).Contains(hit))
            {
                var changed = skills[_slot].Source != Sources[i];
                Loadout.SetSource(_slot, Sources[i]); Dirty = true; _buildRev++;
                // ONLY WHEN IT CHANGED. Re-picking what is already picked is a no-op, and a flourish
                // on a no-op teaches the player that the flourish means nothing.
                if (changed) { _slotFlash[_slot] = SetFlashSeconds; Sound?.Play("sfx_weave", 0.45f); }
                return;
            }

        for (var i = 0; i < Forms.Length; i++)
            if (FormCell(i).Contains(hit))
            {
                var changed = skills[_slot].Form != Forms[i];
                Loadout.SetForm(_slot, Forms[i]); Dirty = true; _buildRev++;
                if (changed) { _slotFlash[_slot] = SetFlashSeconds; Sound?.Play("sfx_weave", 0.45f); }
                return;
            }

        var openRow = OpenBindRow(known);

        // THE BIND CONTROL, tested BEFORE the seals: it sits between two of them, and whichever is
        // checked first owns the overlap.
        if (openRow >= 0 && VowBindBtn(openRow).Contains(hit) && _slot < skills.Count)
        {
            var v = known[_vowScroll + openRow];
            var already = skills[_slot].VowId == v.Id;
            if (Loadout.SetVow(_slot, already ? null : v.Id, known))
            {
                Dirty = true; _buildRev++;
                if (already) _msg = $"{v.Name.ToUpperInvariant()} BROKEN.";
                else
                {
                    _msg = $"{v.Name.ToUpperInvariant()} BOUND TO SLOT {_slot + 1}.";
                    _bindFlash[_slot] = BindFlashSeconds;
                    Sound?.Play("sfx_bind", 0.55f);
                }
            }
            else _msg = "THIS SLOT CANNOT TAKE THAT VOW.";
            return;
        }

        for (var r = 0; r < VowRows; r++)
        {
            var idx = _vowScroll + r;
            if (idx >= known.Count || !VowRow(r, openRow).Contains(hit)) continue;
            var v = known[idx];
            // CLICKING AN OPEN SEAL CLOSES IT. Otherwise the only way to put the BIND row away is to
            // open a different one, and a control that cannot be dismissed reads as modal.
            _readingVowId = _readingVowId == v.Id ? null : v.Id;
            return;
        }

        if (VowClearAt(openRow).Contains(hit) && Loadout.SetVow(_slot, null, known))
        {
            Dirty = true; _buildRev++;
            _msg = "NO VOW ON THIS SLOT.";
        }
    }

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        var hit = Game1.ToOverlay(mouse);
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC8));
        // THIS IS THE BUILD SCREEN NOW. Playtest: "'Choose your skills' butonu ile açılan sayfayı bu
        // sayfaya entegre edelim. Ana mantığı o aslında bu sayfanın." Right — the overview it used to
        // hang off showed the same skills without letting you change any of them, so BUILD was a page
        // you looked at and this was the page you used. The rail's BUILD tile opens this directly.
        _ui.TextCenterBig(b, "BUILD", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "A SKILL IS A SOURCE, A FORM, AND THE VOW YOU PUT ON IT",
                          960, 80, Slate, UiTypography.Secondary);

        // THE DISCIPLINE LINE — the Nen frame this game was born from, finally said out loud: your
        // skills are your specialisation's craft, and a sworn Vow buys back what it does not give you.
        if (Discipline is { } disc)
            _ui.TextCenterBig(b, $"DISCIPLINE: {FormName(disc)} — ITS SKILLS HIT TWICE AS HARD  ·  A VOW ON A FAR-FORM SKILL PULLS IT ONE RING CLOSER",
                              960, 108, Gold, UiTypography.Secondary);
        else
            _ui.TextCenterBig(b, "NO DISCIPLINE YET — A SPECIALISATION NODE ON THE MASTERY TREE (E) GIVES YOU ONE",
                              960, 108, Slate, UiTypography.Secondary);

        TickEffects();
        _card = null;

        DrawSlots(b, hit);
        DrawPicker(b, hit);
        DrawVows(b, hit);

        // The build as one line on the clipboard — show, don't trade. Same flat-cell idiom as
        // VowClear — this screen has no ornate-button helper of its own.
        _ui.Fill(b, CopyCodeBtn, CopyCodeBtn.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
        _ui.TextCenter(b, "COPY BUILD CODE", CopyCodeBtn.Center.X, CopyCodeBtn.Y + 16,
                       CopyCodeBtn.Contains(hit) ? Bone : Slate);
        if (_copyToastFrames > 0)
        {
            _copyToastFrames--;
            _ui.TextCenter(b, _copyToast, CopyCodeBtn.Center.X, CopyCodeBtn.Y - 26,
                           _copyToast.StartsWith("COPIED", StringComparison.Ordinal) ? Gold : Ember);
        }

        if (_msg.Length > 0) _ui.TextCenter(b, _msg, 960, 1016, Gold);

        // LAST, AND IN THIS ORDER. The card explains a cell in the middle panel and must sit over the
        // column beside it; the carried seal must sit over everything including the card, because it
        // is attached to the cursor and anything drawn on top of it would look like the drop failed.
        if (_carrying == Carry.None && _card is { } card) DrawCard(b, card.What, card.Cell);
        DrawCarried(b);
    }

    /// <summary>What is in hand, under the cursor, while a drag is live.</summary>
    /// <remarks>
    /// Drawn only once the press has passed the slop radius: a ghost that appears on every click makes
    /// the screen feel twitchy and tells the player they started something they did not.
    /// </remarks>
    private void DrawCarried(SpriteBatch b)
    {
        if (_carrying == Carry.None || !_carryMoved) return;
        var p = _carryAt;

        if (_carrying == Carry.Slot && _carrySlot >= 0 && _carrySlot < Loadout.Skills.Count)
        {
            var s = Loadout.Skills[_carrySlot];
            var col = SourceColor.GetValueOrDefault(s.Source, Bone);
            var r = new Rectangle(p.X - 90, p.Y - 22, 180, 44);
            _ui.Fill(b, new Rectangle(r.X + 4, r.Y + 5, r.Width, r.Height), new Color(0, 0, 0) * 0.45f);
            _ui.Fill(b, r, new Color(0x2C, 0x25, 0x44));
            _ui.Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), col);
            Outline(b, r, Bone, 2);
            if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } gem)
                b.Draw(gem, new Rectangle(r.X + 10, r.Y + 6, 32, 32), Color.White);
            _ui.Text(b, Fit($"{SourceName(s.Source)} {FormName(s.Form)}", 118), r.X + 50, r.Y + 14, Bone);
        }
    }

    private void DrawSlots(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, SlotsPanel);
        _ui.TextCenterBig(b, "YOUR SKILLS", SlotsPanel.Center.X, UiKit.TitleTop(SlotsPanel), Gold, UiTypography.PanelTitle);

        var skills = Loadout.Skills;
        var ctx = Context;

        // WHICH SLOTS COST AN ACTION. Read from BuildComposer rather than worked out here, so the
        // screen and the fight cannot drift: the composer's walk IS the rule, and a second copy of it
        // would agree only until one of them was edited. The player needs this on the screen because
        // the two kinds are no longer interchangeable — an active takes the champion's turn and a
        // passive never can, which is why four skills used to leave the plain attack almost no beats.
        var passiveSlot = BuildComposer.SlotKinds(
            skills.Select(k => new BuildComposer.SkillPick(k.Source, k.Form, k.VowId, "", k.Passive)).ToList(),
            Loadout.SkillCapacity);

        for (var i = 0; i < Loadout.SkillCapacity; i++)
        {
            if (i >= skills.Count) break;
            var s = skills[i];
            var row = SlotRow(i);
            var on = i == _slot;
            var col = SourceColor.GetValueOrDefault(s.Source, Bone);

            // ── THE ROW'S STATE, in the order it is painted: ground, drop light, edge. ──────────
            var dropping = _carrying == Carry.Slot && _carryMoved && SlotUnder(_carryAt) == i
                           && _carrySlot != i;
            var hovering = row.Contains(hit) && _carrying == Carry.None;
            var inHand = _carrying == Carry.Slot && _carryMoved && _carrySlot == i;

            _ui.Fill(b, row, on ? new Color(0x2C, 0x25, 0x44) : hovering ? new Color(0x1E, 0x18, 0x2C) : Quiet);

            // A ROW THE SEAL IS OVER LIGHTS UP. Without it a drag is a ghost floating over an inert
            // list and the player has to guess where it would land — which is the whole reason a
            // list-and-click was not worth replacing with a drag in the first place.
            if (dropping)
            {
                _ui.Fill(b, row, col * 0.16f);
                Outline(b, row, Bone, 3);
            }
            // The row being carried stays in place but goes hollow, so the list still shows its length
            // and the gap says where the thing came from.
            if (inHand) _ui.Fill(b, row, new Color(0x0C, 0x09, 0x14) * 0.6f);

            // THE NUMBERED SPINE. The order of this list IS the cast priority — SoloBattle takes the
            // first READY skill in slot order, so slot 1 wins every tied beat — and the screen never
            // said so. A 5px colour bar became a 22px spine carrying the number, which is also what
            // makes dragging a row read as changing something rather than tidying a list.
            var spine = new Rectangle(row.X, row.Y, 22, row.Height);
            _ui.Fill(b, spine, col * (on ? 0.55f : 0.34f));
            _ui.TextCenter(b, $"{i + 1}", spine.Center.X, row.Y + 26, on ? Bone : Bone * 0.75f);
            if (on && !dropping) Outline(b, row, Bone, 2);

            // ── THE FLOURISHES. A pick pulses the row's own colour; a bind presses a gold seal. ──
            if (_slotFlash.TryGetValue(i, out var sf))
            {
                var t = Math.Clamp(sf / SetFlashSeconds, 0f, 1f);
                _ui.Fill(b, row, col * (0.30f * t));
                _ui.Fill(b, new Rectangle(row.X, row.Y, row.Width, 3), col * t);
            }
            if (_bindFlash.TryGetValue(i, out var bf)) DrawBindChain(b, row, bf / BindFlashSeconds);

            // THE PAIR, AS TWO GLYPHS. The Form used to live only in the name, so a list of four skills
            // was four gems and a wall of words — and the Form is the half that says what the skill DOES.
            if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } gem)
                b.Draw(gem, new Rectangle(row.X + 28, row.Y + 12, 52, 52), Color.White);
            // BESIDE THE GEM, NOT ON IT. A 26px badge tucked into the gem's corner drew the Strike
            // sword as a two-pixel sliver against the gem's own ornament — unreadable, and worse than
            // nothing because it looked like an artefact. It gets its own square and its own ground.
            var fbox = new Rectangle(row.X + 88, row.Y + 20, 38, 38);
            _ui.Fill(b, fbox, new Color(0x0C, 0x09, 0x14) * 0.55f);
            _ui.Icon(b, $"icon_form_{s.Form.ToString().ToLowerInvariant()}", fbox, col);

            _ui.TextBig(b, Fit($"{SourceName(s.Source)} {FormName(s.Form)}", 158), row.X + 134, row.Y + 8, Bone, UiTypography.Body);

            // THE SLOT'S KIND, as a SWITCH rather than a label. ACTIVE and PASSIVE are the genre's
            // own terms and the ones the designer asked for, and the line under the name says which
            // of the style's two skills the choice actually produces — BLOW or PRESS, not "the
            // passive one", because they are different abilities and the screen should say so.
            var isPassive = i < passiveSlot.Count && passiveSlot[i];
            var kindBox = KindToggle(i);
            var overKind = kindBox.Contains(hit);
            _ui.Fill(b, kindBox, (isPassive ? Met : Gold) * (overKind ? 0.30f : 0.16f));
            Outline(b, kindBox, isPassive ? Met : Gold, overKind ? 2 : 1);
            _ui.TextCenter(b, isPassive ? "PASSIVE" : "ACTIVE", kindBox.Center.X, kindBox.Y + 5,
                           isPassive ? Met : Gold);

            // The skill this slot actually resolves to, under its Source and Form.
            var resolved = SkillCatalogue.Resolve(s.Form, isPassive);
            var chosen = SkillLevels.VariationOf(resolved);
            var free = SkillLevels.FreeOn(resolved.Id);

            if (chosen is null && free > 0)
            {
                // A LEVEL IS WAITING. The two variations are offered where the skill's name would be,
                // because deciding what this skill IS is the more urgent sentence — and the name is
                // already on the row above, in the Source-and-Form line.
                for (var vi = 0; vi < resolved.Variations.Count && vi < 2; vi++)
                {
                    var vb = VariationBtn(i, vi);
                    var overV = vb.Contains(hit);
                    _ui.Fill(b, vb, Gold * (overV ? 0.32f : 0.14f));
                    Outline(b, vb, Gold, overV ? 2 : 1);
                    _ui.TextCenter(b, Fit(resolved.Variations[vi].Name, vb.Width - 8), vb.Center.X, vb.Y + 2, Gold);
                }
            }
            else if (chosen is not null && free > 0
                     && chosen.Reinforcements.Where(r => !SkillLevels.HasReinforcement(resolved.Id, r.Name))
                               .ToList() is { Count: > 0 } open)
            {
                // A LEVEL IS WAITING AND THE SKILL ALREADY KNOWS WHAT IT IS. Now the level buys one of
                // the variation's reinforcements, in the same two-across strip the variations used —
                // the question has not changed, only the answers have.
                //
                // TWO AT A TIME, not all three. Three buttons across this strip leave about sixty
                // pixels each, and the first capture of it read "DEEP… SEDI… SILT" — a purchase whose
                // name the player cannot finish reading is not a choice they can make. The third
                // appears once one of these is bought, and all three are bought by level 4 anyway, so
                // what is deferred is the ORDER rather than the content.
                for (var ri = 0; ri < open.Count && ri < 2; ri++)
                {
                    var rb = VariationBtn(i, ri);
                    var overR = rb.Contains(hit);
                    _ui.Fill(b, rb, Gold * (overR ? 0.32f : 0.14f));
                    Outline(b, rb, Gold, overR ? 2 : 1);
                    _ui.TextCenter(b, Fit(open[ri].Name, rb.Width - 8), rb.Center.X, rb.Y + 2, Gold);
                }
            }
            else
            {
                // The skill, what it was taken as, and how much of that variation is bought. The count
                // is spelled out rather than shown as pips: a player who cannot see how much is left
                // has no reason to come back to a skill they already levelled.
                var line = chosen is null ? resolved.Name : $"{resolved.Name} · {chosen.Name}";
                if (chosen is not null)
                {
                    var bought = chosen.Reinforcements.Count(r => SkillLevels.HasReinforcement(resolved.Id, r.Name));
                    line += $"  {bought}/{chosen.Reinforcements.Count}";
                }
                if (free > 0) line += $"   +{free}";
                _ui.Text(b, Fit(line, row.Right - 140 - (row.X + 134)), row.X + 134, row.Y + 30,
                         free > 0 ? Gold : isPassive ? Met : Slate);
            }

            // The hexagon's verdict on this woven skill — and the Vow buy-back drawn as the LIFT it
            // is ("x0.45→x0.75"), so swearing a Vow on an off-discipline skill visibly pays.
            if (Discipline is { } dd)
            {
                var baseF = FormBehaviour.AffinityFactor(dd, s.Form);
                var vowF = FormBehaviour.AffinityFactor(dd, s.Form, vowSworn: s.VowId is not null);
                var lifted = vowF > baseF + 0.001f;
                var boughtUp = vowF > baseF + 0.001f;
                var tag = boughtUp ? $"x{baseF:0.0#}→x{vowF:0.0#}" : $"x{vowF:0.0#}";
                var tc = vowF >= 1.99f ? Gold : boughtUp ? Met : vowF >= 1.14f ? Met : vowF >= 0.74f ? Slate : Ember;
                _ui.TextRight(b, tag, row.Right - 50, row.Y + 32, tc);
            }

            // The Vow line carries its VERDICT, not just its name. "SWORN" on a Vow that pays nothing
            // is the most misleading thing this screen could say.
            if (skills.Count > 1)
            {
                var x = DropX(i);
                // U+00D7, not U+2715. The heavier multiplication X drew as NOTHING on the system-font
                // path, so this button \u2014 the only way to remove a skill \u2014 was an invisible 30px square
                // that worked perfectly when clicked and advertised itself not at all. It hid behind a
                // `"\u2715"` escape, which the font gate could not see until it learned to decode them.
                _ui.TextCenter(b, "\u00d7", x.Center.X, x.Y + 6, x.Contains(hit) ? Ember : Slate);
            }

            // THE VOW LINE IS A SOCKET NOW. Empty, it says what to do with it rather than merely
            // reporting an absence — "NO VOW" named the state and left the player to discover that a
            // seal from the right-hand column can be dropped here.
            var vow = Weaving.ById(s.VowId);
            var seal = new Rectangle(row.X + 134, row.Y + 44, row.Width - 134 - 14, 24);
            if (vow is null)
            {
                _ui.Fill(b, seal, new Color(0x0E, 0x0B, 0x16) * 0.7f);
                Outline(b, seal, Dim, 1);
                _ui.Text(b, "EMPTY VOW SOCKET", seal.X + 8, seal.Y + 4, Dim);
            }
            else
            {
                var live = Weaving.IsActive(vow, ctx);
                _ui.Fill(b, seal, (live ? Gold : Slate) * 0.13f);
                _ui.Fill(b, new Rectangle(seal.X, seal.Y, 3, seal.Height), live ? Gold : Ember);
                _ui.Text(b, Fit(vow.Short.ToUpperInvariant(), seal.Width - 66), seal.X + 10, seal.Y + 4,
                         live ? Gold : Slate);
                _ui.TextRight(b, live ? "MET" : "UNMET", seal.Right - 6, seal.Y + 4, live ? Met : Ember);
            }
        }

        if (skills.Count < Loadout.SkillCapacity)
        {
            var r = AddBtn;
            _ui.Fill(b, r, r.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
            _ui.TextCenter(b, "+ ADD A SKILL", r.Center.X, r.Y + 14, r.Contains(hit) ? Gold : Slate);
        }

        // ── KEYSTONE SOCKETS ──
        var learned = DustEffects.LearnedKeystones(Tree);
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned.Count - KeystoneRows));
        var head = new Rectangle(SlotColX, KeystoneChip(0).Y - 44, SlotColW, 30);
        _ui.TextBig(b, "KEYSTONES", head.X, head.Y, Gold, UiTypography.Body);
        // Sockets used, then the window into the list — a player with six learned needs to know both
        // that they may wear two and that there are three more below the fold.
        _ui.TextRight(b, learned.Count == 0
                          ? "LEARN IN TRAITS (P)"
                          : learned.Count > KeystoneRows
                              ? $"{Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity}   "
                                + $"[{_keystoneScroll + 1}-{Math.Min(learned.Count, _keystoneScroll + KeystoneRows)} of {learned.Count}]"
                              : $"{Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity}",
                      head.Right, head.Y + 6, Slate);

        for (var i = 0; i < KeystoneRows; i++)
        {
            var chip = KeystoneChip(i);
            var idx = _keystoneScroll + i;
            if (idx >= learned.Count)
            {
                _ui.Fill(b, chip, new Color(0x11, 0x0E, 0x18, 0xC0));
                _ui.Text(b, "—", chip.X + 18, chip.Y + 12, Dim);
                continue;
            }
            var k = learned[idx];
            var worn = Loadout.HasKeystone(k.Id);
            var hover = chip.Contains(hit);
            _ui.Fill(b, chip, worn ? new Color(0x2A, 0x24, 0x14) : hover ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            if (worn) _ui.Fill(b, new Rectangle(chip.X, chip.Y, 5, chip.Height), Gold);
            _ui.Text(b, k.Name.ToUpperInvariant(), chip.X + 18, chip.Y + 12, worn ? Gold : hover ? Bone : Slate);
            _ui.TextRight(b, worn ? "IN USE" : "", chip.Right - 14, chip.Y + 12, Met);
        }
    }

    /// <summary>
    /// What this build DOES, and what the pick under the cursor would do to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest: <i>"Skiller hala yavan, skill oluşturma kısmı oyunun en unique kısımlarından biri.
    /// Buranın çok özenli olması lazım ve mantığını arayüzünden anlatabilmesi lazım."</i>
    /// </para>
    /// <para>
    /// The screen already explained each Form and Source in isolation. What it could not do was answer
    /// the only question a player actually has at the moment of choosing: <b>is this better than what I
    /// have?</b> Thirty-six combinations, four slots, and no way to compare any two of them except by
    /// committing and going to watch. That is what "yavan" describes — not missing text, missing
    /// consequence.
    /// </para>
    /// <para>
    /// Three things, in the order they matter: what the build does now, what it would do with the pick
    /// under the cursor, and whether that Source is any good WHERE YOU ARE HUNTING. The last one turns
    /// the matchup from a rule into a decision — "BODY beats MIND and NATURE" means nothing until you
    /// know the place you are going fields them.
    /// </para>
    /// </remarks>
    private void DrawReadout(SpriteBatch b, Source? hoverSource, Form? hoverForm)
    {
        if (Hunter is not { } hunter) return;

        var top = KeystoneChip(KeystoneRows - 1).Bottom + 26;
        var x = SlotColX;
        var width = SlotColW;

        // A FLOOR, NOT A BAIL — this readout was DEAD for most of the game.
        //
        // Everything in this column hangs off SkillCapacity: top = 494 + 86*capacity. The old guard was
        // `if (top > SlotsPanel.Bottom - 120) return;` = 824, so at FOUR slots (top 838) and five (924)
        // the whole block vanished — and four is where a normal build lives. The one readout that
        // answers "is this pick better than what I have" was invisible for the entire mid and late game,
        // silently, on the screen it exists for.
        //
        // It now draws what fits and stops, which is what the guard's own comment said it was for.
        var floor = SlotsPanel.Bottom - UiKit.PanelCorner - 8;
        if (top + 64 > floor) return;   // not even a heading and one row; never draw through the frame

        _ui.Fill(b, new Rectangle(x - 12, top - 12, width + 24, floor - top + 4), Quiet);
        _ui.Text(b, "WHAT THIS BUILD DOES", x, top, Slate);

        var y = top + 34;

        // CURRENT. Re-measured only when the build actually changes.
        if (_currentRev != _buildRev)
        {
            _currentDps = Dps(Loadout, hunter);
            _currentRev = _buildRev;
        }

        _ui.TextBig(b, "NOW", x, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"{_currentDps:N0} damage each second", x + width, y, Bone, UiTypography.Body);
        y += 30;
        if (y + 30 > floor) return;

        // THE PICK UNDER THE CURSOR, measured on a COPY so hovering never mutates the real loadout.
        if ((hoverSource is not null || hoverForm is not null) && _slot >= 0 && _slot < Loadout.Skills.Count)
        {
            var cur = Loadout.Skills[_slot];
            var s2 = hoverSource ?? cur.Source;
            var f2 = hoverForm ?? cur.Form;
            var key = (s2, f2, _slot, _buildRev);

            if (_previewKey != key)
            {
                // MEASURED ON THE REAL LOADOUT, then put back — the same trick
                // Hunter.PowerContribution uses to price an item, and for the same reason: a second
                // "copy of the build" type would be a second place for the build's rules to live, and
                // the copy is the one that goes stale. The game is single-threaded and this runs inside
                // Draw, so nothing observes the intermediate state.
                var keepSource = cur.Source;
                var keepForm = cur.Form;
                Loadout.SetSource(_slot, s2);
                Loadout.SetForm(_slot, f2);
                _previewDps = Dps(Loadout, hunter);
                Loadout.SetSource(_slot, keepSource);
                Loadout.SetForm(_slot, keepForm);
                _previewKey = key;
            }

            var delta = _previewDps - _currentDps;
            var pct = _currentDps > 0.01f ? delta / _currentDps * 100f : 0f;
            var tint = MathF.Abs(pct) < 0.5f ? Slate : pct > 0f ? Met : Ember;

            _ui.TextBig(b, "WITH THIS", x, y, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{_previewDps:N0} damage each second", x + width, y, tint, UiTypography.Body);
            y += 24;
            _ui.TextRightBig(b, MathF.Abs(pct) < 0.5f ? "no change" : $"{(pct > 0 ? "+" : "")}{pct:0}%",
                             x + width, y, tint, UiTypography.Secondary);
            y += 30;
        }
        else
        {
            // SIZED AND INKED FOR THE COLUMN. At the default 32px raster this measured ~540px against a
            // 440px column, so it ran across the panel's ornate frame and into the gutter — and in Dim
            // it was barely visible while doing it.
            _ui.TextBig(b, "hover a SOURCE or FORM to compare", x, y, Slate, UiTypography.Secondary);
            y += 54;
        }

        // WHAT YOUR GEAR IS WAITING FOR. The badge on the Form cell draws the eye; this says which item
        // and which enchantment, because a marker with no sentence behind it is a riddle.
        var wants = GearWants();
        if (wants.Count > 0)
        {
            _ui.Fill(b, new Rectangle(x, y, width, 2), Dim);
            y += 14;
            foreach (var (form, enchant) in wants.Take(2))
            {
                _ui.Text(b, $"{enchant.ToUpperInvariant()} WANTS", x + 10, y, Slate);
                _ui.TextRight(b, FormName(form), x + width, y, Met);
                y += 24;
            }
            y += 6;
        }

        // WHERE YOU ARE HUNTING. The matchup, against the creatures that actually live there.
        if (RegionId.Length == 0) return;

        var roster = BandCycles.RosterFor(RegionId);
        if (roster.Count == 0) return;

        _ui.Fill(b, new Rectangle(x, y, width, 2), Dim);
        y += 14;
        _ui.Text(b, $"IN {(RegionName.Length > 0 ? RegionName : RegionId).ToUpperInvariant()}", x, y, Slate);
        y += 30;

        var judged = hoverSource ?? (_slot >= 0 && _slot < Loadout.Skills.Count
            ? Loadout.Skills[_slot].Source
            : Source.Body);

        foreach (var enemy in roster.Distinct().Take(4))
        {
            var mult = Weaving.SourceEffectiveness(judged, enemy, new WeavingTuning());
            var verdict = mult > 1.01f ? "STRONG" : mult < 0.99f ? "WEAK" : "even";
            var tint = mult > 1.01f ? Met : mult < 0.99f ? Ember : Slate;

            _ui.Text(b, SourceName(enemy), x + 10, y, SourceColor.GetValueOrDefault(enemy, Bone));
            _ui.TextRight(b, $"{verdict}  x{mult:0.00}", x + width, y, tint);
            y += 24;
        }
    }

    /// <summary>
    /// Forms your WORN GEAR is waiting for, that your build does not yet fire.
    /// </summary>
    /// <remarks>
    /// Six enchantments are Form combos — Overdraw wants a PROJECTILE, Execute wants a STRIKE — and
    /// without that Form they are dead weight on the item. The dependency ran ONE WAY: the Forge greys
    /// out a combo the build cannot meet, but the screen where Forms are actually CHOSEN never mentioned
    /// that a piece of your gear was waiting on one. Core has carried the requirement, machine-readable,
    /// the whole time (<c>Enchantment.NeedsForm</c>); nothing on this screen asked it.
    ///
    /// This is the cheapest possible way to make the Weave feel connected to the rest of the game, and
    /// it turns a shrug into a reason: not "pick a Form" but "your focus is waiting for a VOLLEY".
    /// </remarks>
    private IReadOnlyList<(Form Form, string Enchant)> GearWants()
    {
        if (Hunter is not { } h) return Array.Empty<(Form, string)>();

        var have = Loadout.Skills.Select(sk => sk.Form).ToHashSet();
        return h.WornEnchantments
                .Where(e => e.NeedsForm is { } f && !have.Contains(f))
                .Select(e => (e.NeedsForm!.Value, e.Name))
                .DistinctBy(t => t.Item1)
                .ToList();
    }

    /// <summary>One damage reading for a loadout, through the same bench the balance tests use.</summary>
    private float Dps(PlayerLoadout loadout, Hunter hunter)
        => DamageBench.Measure(loadout.ToBuild(Tree, Mastery, Character), hunter).Dps;

    private void DrawPicker(SpriteBatch b, Point hit)
    {
        _ui.PanelQuiet(b, PickPanel);
        var skills = Loadout.Skills;
        if (_slot >= skills.Count)
        {
            _ui.TextCenter(b, "PICK A SLOT ON THE LEFT.", PickPanel.Center.X, PickPanel.Center.Y, Slate);
            return;
        }
        var cur = skills[_slot];

        _ui.TextCenterBig(b, "SOURCE", PickPanel.Center.X, UiKit.TitleTop(PickPanel), Gold, UiTypography.PanelTitle);
        _ui.TextCenter(b, "WHAT IT IS MADE OF", PickPanel.Center.X, UiKit.CaptionTop(PickPanel), Slate);

        // What the player is ASKING about — whatever the cursor is over, falling back to what they
        // already picked. Hovering is the question "what is this?", and until now the screen had no
        // answer to it at all: six Source icons and six Form icons, named and otherwise silent.
        Source? hoverSource = null;
        Form? hoverForm = null;

        for (var i = 0; i < Sources.Length; i++)
        {
            var cell = SourceCell(i);
            var s = Sources[i];
            var over = cell.Contains(hit);
            if (over) hoverSource = s;
            var on = cur.Source == s;
            var col = SourceColor.GetValueOrDefault(s, Bone);
            PickCell(b, cell, on, over, col);
            // THE GEM SITS IN ITS OWN LIGHT. A source-tinted halo behind the art gives the six cells
            // their identity at a glance — the icons are close in silhouette and were reading as one
            // grey grid until the colour was doing something other than labelling the caption.
            var art = new Rectangle(cell.Center.X - 36, cell.Y + 16, 72, 72);
            _ui.Diamond(b, new Rectangle(art.X - 6, art.Y - 6, art.Width + 12, art.Height + 12),
                        col * (on ? 0.34f : over ? 0.22f : 0.10f));
            if (_ui.Assets.Get($"source_{s.ToString().ToLowerInvariant()}") is { } gem)
                b.Draw(gem, art, on || over ? Color.White : Color.White * 0.72f);
            else _ui.Diamond(b, art, col);
            _ui.TextCenterBig(b, SourceName(s), cell.Center.X, cell.Bottom - 34,
                              on ? Bone : over ? col : col * 0.85f, UiTypography.Body);
        }

        _ui.TextCenterBig(b, "FORM", PickPanel.Center.X, FormTitleTop, Gold, UiTypography.PanelTitle);
        _ui.TextCenter(b, "WHAT IT DOES", PickPanel.Center.X, FormTitleTop + (UiTypography.PanelCaptionTop - UiTypography.PanelTitleTop), Slate);

        var wanted = GearWants();

        var slotCol = SourceColor.GetValueOrDefault(cur.Source, Bone);

        for (var i = 0; i < Forms.Length; i++)
        {
            var cell = FormCell(i);
            var f = Forms[i];
            var over = cell.Contains(hit);
            if (over) hoverForm = f;
            var on = cur.Form == f;
            PickCell(b, cell, on, over, slotCol);

            // Tinted by the slot's SOURCE, so the picker previews the pairing rather than showing six
            // grey shapes — the skill you are building is a Source AND a Form, never either alone.
            var glyphTint = on ? slotCol : over ? Bone : Slate;
            var art = new Rectangle(cell.Center.X - 34, cell.Y + 18, 68, 68);
            if (on) _ui.Diamond(b, new Rectangle(art.X - 6, art.Y - 6, art.Width + 12, art.Height + 12), slotCol * 0.28f);
            if (!_ui.Icon(b, $"icon_form_{f.ToString().ToLowerInvariant()}", art, glyphTint))
                _ui.Diamond(b, new Rectangle(cell.Center.X - 24, cell.Y + 26, 48, 48), glyphTint);
            _ui.TextCenterBig(b, FormName(f), cell.Center.X, cell.Bottom - 34,
                              on ? Bone : over ? Bone : Slate, UiTypography.Body);

            // THE HEXAGON'S VERDICT on this Form for YOUR discipline, on the cell itself — the picker
            // teaches the class while you choose. A CHIP now rather than loose text: at 140px tall the
            // cell has room for the number to be readable instead of tucked into a corner.
            if (Discipline is { } dpick)
            {
                var factor = FormBehaviour.AffinityFactor(dpick, f);
                var (aTag, aCol) = factor >= 1.99f ? ("x2.0", Gold)
                    : factor >= 1.14f ? ("x1.15", Met)
                    : factor >= 0.74f ? ("x0.75", Slate)
                    : ("x0.45", Ember);
                var chip = new Rectangle(cell.X + 8, cell.Y + 8, 54, 22);
                _ui.Fill(b, chip, new Color(0x0C, 0x09, 0x14) * 0.85f);
                _ui.Fill(b, new Rectangle(chip.X, chip.Bottom - 2, chip.Width, 2), aCol * 0.9f);
                _ui.TextCenter(b, aTag, chip.Center.X, chip.Y + 4, aCol);
                if (factor >= 1.99f && !on) Outline(b, cell, Gold, 2);
            }

            // YOUR GEAR IS WAITING FOR THIS ONE. A small mark rather than a line of text: the point is
            // to draw the eye, not to explain here — the hover card spells out which item wants it.
            if (wanted.Any(w => w.Form == f))
                _ui.Fill(b, new Rectangle(cell.Right - 24, cell.Y + 12, 12, 12), Met);
        }

        DrawSlotBanner(b, cur);
        DrawReadout(b, hoverSource, hoverForm);

        // THE CARD IS NOT DRAWN HERE. It has to sit over the panels beside it, and this method runs
        // before them — Draw renders it last. Recorded, not rendered.
        if (hoverSource is not null || hoverForm is not null)
        {
            // ANCHORED TO THE CELL, not to the cursor. A card at cursor+28 lands ON the option it is
            // explaining — the capture showed it covering PROJECTILE's own label while describing it.
            var cell = hoverForm is { } hf
                ? FormCell(Array.IndexOf(Forms, hf))
                : SourceCell(Array.IndexOf(Sources, hoverSource!.Value));
            _card = (ReadingFor(hit, cur, hoverSource, hoverForm), cell);
        }
    }

    /// <summary>
    /// One pick cell's plate: its ground, its edge, and the lift a hovered cell gets.
    /// </summary>
    /// <remarks>
    /// Shared by both grids so SOURCE and FORM cannot drift apart, which they had: the Source grid
    /// outlined its hover in the source's own colour and the Form grid in gold, so the same gesture
    /// looked like two different affordances on one panel.
    /// </remarks>
    private void PickCell(SpriteBatch b, Rectangle cell, bool on, bool over, Color accent)
    {
        _ui.Fill(b, cell, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
        // A SELECTED CELL IS LIT FROM ITS TOP EDGE — the same language the slot rows and the vow seals
        // use for "this is the one", so the three panels read as one screen.
        if (on) _ui.Fill(b, new Rectangle(cell.X, cell.Y, cell.Width, 3), accent);
        Outline(b, cell, on ? Bone : over ? accent : Dim, on ? 3 : 2);
    }

    /// <summary>
    /// What you are editing, across the foot of the picker: the pair as one thing.
    /// </summary>
    /// <remarks>
    /// The description strip that used to close this panel became a hover card, and leaving the space
    /// empty would have been the wrong trade — a panel of twelve options needs to say which slot the
    /// next click lands on. This says it in the game's own terms (the Source gem, the Form glyph, the
    /// composed name), so the answer is a picture rather than a sentence.
    /// </remarks>
    private void DrawSlotBanner(SpriteBatch b, PlayerLoadout.SkillChoice cur)
    {
        var x = UiKit.ContentLeft(PickPanel);
        var w = PickPanel.Width - UiKit.PadX(PickPanel) * 2;
        var top = FormCell(Forms.Length - 1).Bottom + 18;
        var r = new Rectangle(x, top, w, Math.Max(0, PickPanel.Bottom - 30 - top));
        if (r.Height < 40) return;

        var col = SourceColor.GetValueOrDefault(cur.Source, Bone);
        _ui.Fill(b, r, new Color(0x11, 0x0D, 0x1A, 0xE0));
        _ui.Fill(b, new Rectangle(r.X, r.Y, 4, r.Height), col);

        var mid = r.Y + r.Height / 2;
        if (_ui.Assets.Get($"source_{cur.Source.ToString().ToLowerInvariant()}") is { } gem)
            b.Draw(gem, new Rectangle(r.X + 16, mid - 24, 48, 48), Color.White);
        _ui.Icon(b, $"icon_form_{cur.Form.ToString().ToLowerInvariant()}",
                 new Rectangle(r.X + 72, mid - 22, 44, 44), col);

        _ui.TextBig(b, $"SLOT {_slot + 1}", r.X + 130, mid - 26, Slate, UiTypography.Caption);
        _ui.TextBig(b, $"{SourceName(cur.Source)} {FormName(cur.Form)}", r.X + 130, mid - 6, Bone, UiTypography.Body);
        _ui.TextRight(b, cur.VowId is null ? "NO VOW" : (Weaving.ById(cur.VowId)?.Name ?? "").ToUpperInvariant(),
                      r.Right - 16, mid - 6, cur.VowId is null ? Dim : Gold);
    }

    /// <summary>One title line and one paragraph — everything the strip under the picker ever says.</summary>
    private readonly record struct Explanation(string Title, string Body, bool IsSelection);

    /// <summary>
    /// WHAT THE STRIP IS TALKING ABOUT: the one thing under the cursor, or — when the cursor rests on
    /// nothing this screen explains — the skill in the chosen slot, its Source and its Form together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest, 2026-08-28: <i>"Aura is selected but I am hovering over a source — the Aura
    /// description takes more room. It is not readable."</i> Exactly right, and the strip was doing it
    /// on purpose: it printed the SELECTED Form's rule and the HOVERED Source's lines at once, so the
    /// answer to "what is this gem?" arrived underneath three lines about a Form the player had not
    /// asked about. Two answers in a space sized for one is the same as no answer.
    /// </para>
    /// <para>
    /// The rule is now ONE SUBJECT AT A TIME, and the cursor picks it. Every hoverable thing the
    /// composer offers answers here — a Source gem, a Form glyph, a Vow row, a keystone chip — which
    /// makes the strip one predictable place to look rather than a panel with moods of its own. The
    /// last two live in the neighbouring columns; their rows already light up under the cursor, and
    /// this is where the sentence behind the light goes.
    /// </para>
    /// <para>
    /// There is no keyboard focus to follow: this composer is pointer-driven (<see cref="Update"/>
    /// takes a mouse position and a wheel and no keys at all), so hover IS the focus. If a focused
    /// cell is ever added, it belongs in this one method and nowhere else.
    /// </para>
    /// </remarks>
    private Explanation ReadingFor(Point hit, PlayerLoadout.SkillChoice cur, Source? hoverSource, Form? hoverForm)
    {
        if (hoverSource is { } hs)
            return new Explanation($"SOURCE: {SourceName(hs)}",
                                   $"{BuildGlossary.SourceLine(hs)}. {BuildGlossary.MatchupLine(hs)}.", false);

        if (hoverForm is { } hf)
            return new Explanation($"FORM: {FormName(hf)} — {BuildGlossary.FormHeadline(hf)}",
                                   BuildGlossary.FormRule(hf), false);

        var known = Known;
        for (var r = 0; r < VowRows; r++)
        {
            var idx = _vowScroll + r;
            // _vowScroll is clamped in DrawVows, which runs AFTER this — so the bound is checked here
            // rather than assumed. Same for the keystone window below.
            if (idx < known.Count && VowRow(r, OpenBindRow(known)).Contains(hit))
                return new Explanation(Titled("VOW", known[idx].Name), known[idx].Description, false);
        }

        var learned = DustEffects.LearnedKeystones(Tree);
        for (var i = 0; i < KeystoneRows; i++)
        {
            var idx = _keystoneScroll + i;
            if (idx < learned.Count && KeystoneChip(i).Contains(hit))
                return new Explanation(Titled("KEYSTONE", learned[idx].Name), learned[idx].Blurb, false);
        }

        // NOTHING UNDER THE CURSOR — the skill being edited. Both halves, because a skill IS both, and
        // this is the only reading where the player has not pointed at one of them.
        return new Explanation(
            $"YOUR SKILL: {SourceName(cur.Source)} {FormName(cur.Form)} — {BuildGlossary.FormHeadline(cur.Form)}",
            $"{BuildGlossary.FormRule(cur.Form)} {BuildGlossary.SourceLine(cur.Source)}.", true);
    }

    /// <summary>
    /// The description strip: a title line, a paragraph, and never a second of either.
    /// </summary>
    /// <remarks>
    /// The playtest's flattest sentence was "I do not know what the skills do, or what difference the
    /// ones I picked make", and it was a fair description of this screen: it asked for four picks out
    /// of thirty-six combinations and printed only their names. The text is <see cref="BuildGlossary"/>,
    /// in Core, derived from the same constants the fight reads — so a retuned cooldown cannot leave a
    /// lie behind on this panel. WHICH of those strings the strip shows is decided in one place, and
    /// this is not it: see <see cref="ReadingFor"/>.
    /// </remarks>
    /// <summary>The explanation card and where the cursor was, recorded by the picker for Draw.</summary>
    private (Explanation What, Rectangle Cell)? _card;

    /// <summary>
    /// The hovered option's explanation, on a card beside the cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces a strip across the foot of the picker (playtest 2026-08-28: "açıklamaların altta
    /// bir bölümde değil de seçeneğe hover yapılınca hover paneliyle çıkması"). The strip had the
    /// reader looking 400px away from the thing they were asking about, and it cost the panel the
    /// height that now belongs to the cells.
    /// </para>
    /// <para>
    /// Its own drawing rather than <see cref="UiKit.HoverTip"/>: this card carries a TITLE as well as a
    /// paragraph, and the title is the half that says which of twelve options is being explained.
    /// Flipped away from the screen edges the same way, so a card on the last column is never clipped.
    /// </para>
    /// <para>
    /// NOTHING IS HOVER-ONLY HERE, which the project forbids. Every cell still carries its name, its
    /// affinity chip and its gear mark, the foot of the panel names the slot being edited, and the
    /// left column's readout answers "is this better than what I have" without a cursor. The card is
    /// the long form, not the only form.
    /// </para>
    /// </remarks>
    private void DrawCard(SpriteBatch b, Explanation what, Rectangle cell)
    {
        const int width = 460, pad = 16;
        var lines = _ui.WrapBig(what.Body, width - pad * 2, UiTypography.Secondary);
        var h = pad * 2 + 30 + lines.Count * 21;

        // BESIDE THE PANEL, level with the row being asked about. Anchoring to the cell's own right
        // edge would still overlap the two cells beside it, and the grid is what the player is
        // reading — so the card clears the whole picker and lines up with the cell instead.
        var x = PickPanel.Right + 12;
        if (x + width > 1908) x = PickPanel.X - width - 12;
        var y = Math.Clamp(cell.Center.Y - h / 2, 12, 1068 - h);

        _ui.Fill(b, new Rectangle(x + 5, y + 6, width, h), new Color(0, 0, 0) * 0.5f);
        _ui.Fill(b, new Rectangle(x, y, width, h), new Color(0x15, 0x10, 0x22));
        Outline(b, new Rectangle(x, y, width, h), new Color(0x3A, 0x30, 0x50), 2);
        // The accent bar carries the same colour language as the cell it came from: gold for the thing
        // you have chosen, bone for one you are only asking about.
        _ui.Fill(b, new Rectangle(x, y, width, 3), what.IsSelection ? Gold : Bone * 0.7f);

        _ui.TextBig(b, FitBig(what.Title, width - pad * 2, UiTypography.Body), x + pad, y + pad,
                    what.IsSelection ? Gold : Bone, UiTypography.Body);
        var ty = y + pad + 30;
        foreach (var line in lines) { _ui.TextBig(b, line, x + pad, ty, Bone, UiTypography.Secondary); ty += 21; }
    }

    /// <summary>
    /// A title line: what kind of thing it is, then its name — unless the name already says the kind.
    /// </summary>
    /// <remarks>
    /// Twelve of the thirteen Vows are called "VOW OF something", so a flat prefix reads
    /// "VOW: VOW OF THE DELIBERATE". The thirteenth (RECKLESS OFFERING) needs the word, which is why
    /// the prefix is conditional rather than simply dropped.
    /// </remarks>
    private static string Titled(string kind, string name)
    {
        var n = name.ToUpperInvariant();
        return n.StartsWith(kind, StringComparison.Ordinal) ? n : $"{kind}: {n}";
    }

    /// <summary>Truncate to a pixel width at a given size — <see cref="Fit"/> measures at the default.</summary>
    private string FitBig(string text, int width, int px)
    {
        if (_ui.MeasureBig(text, px) <= width) return text;
        var s = text;
        while (s.Length > 1 && _ui.MeasureBig(s + "…", px) > width) s = s[..^1];
        return s.TrimEnd() + "…";
    }

    private void DrawVows(SpriteBatch b, Point hit)
    {
        _ui.PanelQuiet(b, VowPanel);
        _ui.TextCenterBig(b, "VOWS", VowPanel.Center.X, UiKit.TitleTop(VowPanel), Gold, UiTypography.PanelTitle);

        var known = Known;
        var skills = Loadout.Skills;
        var sworn = _slot < skills.Count ? skills[_slot].VowId : null;
        var ctx = Context;

        if (known.Count == 0)
        {
            _ui.TextCenter(b, "YOU KNOW NO VOWS YET.", VowPanel.Center.X, VowPanel.Y + 140, Slate);
            _ui.TextCenter(b, "LEARN THEM IN TRAITS (P).", VowPanel.Center.X, VowPanel.Y + 176, Dim);
            return;
        }

        _ui.TextCenter(b, "WORKS ONLY IF YOU MEET ITS DEMAND", VowPanel.Center.X, UiKit.CaptionTop(VowPanel), Slate);

        _vowScroll = Math.Clamp(_vowScroll, 0, Math.Max(0, known.Count - VowRows));
        var openRow = OpenBindRow(known);
        for (var r = 0; r < VowRows; r++)
        {
            var idx = _vowScroll + r;
            if (idx >= known.Count) break;
            var v = known[idx];
            var row = VowRow(r, openRow);
            var on = v.Id == sworn;
            var picked = v.Id == _readingVowId;
            var live = Weaving.IsActive(v, ctx);
            var hover = row.Contains(hit);

            // ── A SEAL, NOT A LIST ROW ──────────────────────────────────────────────────────────
            //
            // Playtest 2026-08-28: the column read as "düz sıralı, tıkladım eklendi" — a settings list.
            // A Vow is bound TO A SKILL (SkillChoice carries the id), so it is drawn as the thing that
            // gets pressed into one: a wax medallion carrying the price, the name beside it, and the
            // demand underneath as the condition written on the seal. Clicking it opens the BIND row.
            _ui.Fill(b, row, on || picked ? new Color(0x2C, 0x25, 0x44) : hover ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            if (on) Outline(b, row, Gold, 2);
            else if (picked) Outline(b, row, Bone, 2);
            else if (hover) Outline(b, row, Bone * 0.55f, 1);

            // THE MEDALLION carries the multiplier, because the price is what a player is shopping for
            // and it was the smallest thing on the row. Its ring is the verdict: gold while the demand
            // holds, ember while it does not.
            // A RING OF WAX, not a boxed icon. A rectangle outline around a diamond reads as a frame
            // holding a shape — two silhouettes arguing. Two diamonds, the outer one the verdict's
            // colour and the inner one the panel's own dark, give the medallion a single edge.
            var med = new Rectangle(row.X + 10, row.Y + 11, 48, 48);
            _ui.Diamond(b, med, (live ? Gold : Ember) * (on ? 0.85f : 0.55f));
            _ui.Diamond(b, new Rectangle(med.X + 5, med.Y + 5, med.Width - 10, med.Height - 10),
                        new Color(0x16, 0x11, 0x22));
            _ui.TextCenter(b, $"x{Weaving.VowMultiplier(v, WeavingTuning.Default):0.00}",
                           med.Center.X, med.Y + 16, live ? Gold : Slate);

            const int verdict = 74;
            var tx = row.X + 70;
            _ui.TextBig(b, Fit(v.Name.ToUpperInvariant(), row.Width - 70 - verdict - 18), tx, row.Y + 10,
                        on ? Gold : Bone, UiTypography.Body);
            // SLATE, NOT DIM, WHEN UNMET. Dim on this row plate measures ~1.6:1, so the demand — the
            // thing the player is deciding whether to chase — was hardest to read exactly when it
            // mattered most. Gold-when-live against slate-when-not is still an unmistakable two-state.
            _ui.Text(b, Fit(DemandText(v), row.Width - 70 - verdict - 18), tx, row.Y + 42, live ? Met : Slate);
            _ui.TextRight(b, live ? "MET" : "UNMET", row.Right - 14, row.Y + 42, live ? Met : Ember);

            // WHAT TO DO WITH IT, on the row the cursor is on. The gesture is new, so it is taught
            // where it is used rather than in a legend nobody reads.
            if (on) _ui.TextRight(b, "SWORN", row.Right - 14, row.Y + 12, Gold);
        }

        // ── THE BIND CONTROL, under the seal that opened it ─────────────────────────────────────
        //
        // Designer's call (2026-08-28), replacing a drag: a seal is CHOSEN by clicking it and BOUND by
        // pressing this. One target, one consequence, and reachable by any input — a drag needs a
        // pointer, and combat targeting here is cycle-and-confirm for exactly that reason.
        if (openRow >= 0 && _vowScroll + openRow < known.Count)
        {
            var v = known[_vowScroll + openRow];
            var btn = VowBindBtn(openRow);
            var canBind = _slot < skills.Count;
            var already = canBind && skills[_slot].VowId == v.Id;
            var over = btn.Contains(hit);

            _ui.Fill(b, btn, !canBind ? Quiet
                             : already ? new Color(0x30, 0x18, 0x18)
                             : over ? new Color(0x3A, 0x2E, 0x18) : new Color(0x24, 0x1D, 0x2E));
            Outline(b, btn, !canBind ? Dim : already ? Ember : over ? Gold : Gold * 0.55f, 2);

            // NAMES THE SLOT, not "the current skill": the player is looking at four of them on the
            // left and the button has to say which one it means.
            var label = !canBind ? "PICK A SLOT ON THE LEFT"
                : already ? $"BREAK THIS VOW ON SLOT {_slot + 1}"
                : $"BIND TO SLOT {_slot + 1}  ·  {SourceName(skills[_slot].Source)} {FormName(skills[_slot].Form)}";
            _ui.TextCenter(b, Fit(label, btn.Width - 20), btn.Center.X, btn.Y + 13,
                           !canBind ? Dim : already ? Ember : over ? Gold : Bone);
        }

        if (known.Count > VowRows)
            _ui.TextRight(b, $"{_vowScroll + 1}-{Math.Min(known.Count, _vowScroll + VowRows)} / {known.Count}",
                          UiKit.ContentRight(VowPanel), UiKit.CaptionTop(VowPanel), Slate);

        var clear = VowClearAt(openRow);
        _ui.Fill(b, clear, clear.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
        _ui.TextCenter(b, sworn is null ? "NO VOW SWORN" : "BREAK THE VOW",
                       clear.Center.X, clear.Y + 14, sworn is null ? Dim : Ember);

        // The reading panel: the full text of whichever Vow was last touched, because the row can only
        // carry its demand and a Vow's cost is the half that decides whether to take it.
        var reading = Weaving.ById(_readingVowId) ?? Weaving.ById(sworn) ?? known[_vowScroll];
        var y = clear.Bottom + 18;
        _ui.Fill(b, new Rectangle(VowColX, y, VowColW, 2), Dim);
        y += 16;
        _ui.TextBig(b, reading.Name.ToUpperInvariant(), VowColX, y, Gold, UiTypography.Body);
        y += 32;
        // Bounded, so a longer Vow than any in the catalogue today cannot reintroduce the overflow: the
        // panel's frame art reaches 40px in, and text drawn past that is text on the ornament.
        DrawWrapped(b, reading.Description, VowColX, y, VowColW, Bone, VowPanel.Bottom - 40);
    }

    /// <summary>Truncate to a pixel width, with an ellipsis, so a long line cannot invade its neighbour.</summary>
    private string Fit(string text, int width)
    {
        if (_ui.Measure(text) <= width) return text;
        var s = text;
        while (s.Length > 1 && _ui.Measure(s + "\u2026") > width) s = s[..^1];
        return s.TrimEnd() + "\u2026";
    }

    /// <summary>Wrap to <paramref name="width"/>, and stop at <paramref name="maxY"/> rather than run past it.</summary>
    private void DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c, int maxY = int.MaxValue)
    {
        const int lineH = 28;
        var line = "";
        foreach (var w in text.ToUpperInvariant().Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0)
            {
                if (y + lineH > maxY) { _ui.Text(b, line + "\u2026", x, y, c); return; }
                _ui.Text(b, line, x, y, c);
                y += lineH;
                line = w;
            }
            else line = probe;
        }
        if (line.Length > 0 && y + lineH <= maxY) _ui.Text(b, line, x, y, c);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
