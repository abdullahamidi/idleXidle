using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Quests;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// The ROSTER screen: who you can be, and what changes when you are them.
/// </summary>
/// <remarks>
/// <para>
/// Two questions, in this order. <i>Who should I play?</i> is answered by the two facts that actually
/// separate two hunters — the SIGNATURE SKILL that belongs to them and to nobody else, and the INNATE
/// power that is always on — neither of which this screen showed at all before UX V2 P2.2.
/// </para>
/// <para>
/// <i>What do I lose by switching?</i> is answered BESIDE THE BUTTON rather than in a banner over the
/// screen. Switching is free — <c>CharacterState.Select</c> changes one id; both trees, gear, Gleam and
/// the Warren are account-level — but it is not quite nothing: gear the new hunter cannot wear goes
/// back to your bag (<c>Game1.ShedUnwearable</c>), so the state block counts those pieces before you
/// press. The old header line promised "YOU KEEP … GEAR" on every visit, in the least readable type on
/// the screen, and it over-promised.
/// </para>
/// <para>
/// A locked hunter shows everything about itself except the ability to be picked — and now also HOW
/// CLOSE it is: every gate has a bar and a live count, conquest gates included, which used to say the
/// single word LOCKED.
/// </para>
/// <para>
/// <b>THE DENSITY PROFILE (UI polish §8–§9).</b> Every size that is not the frame's own art comes from
/// <see cref="UiMetrics"/>: the badge, the status band, the icons and the button at the control rate;
/// the insets, gaps and the grid's margins at the spacing rate. Nothing is anchored to 1080 px of
/// height — the card stack takes what the page leaves, and the inspector MEASURES its blocks before it
/// draws them and drops from the least important end (ROAD AND GEAR, then the blurb, then the innate's
/// third line) until the state block and the button fit. At 150 % the card's innate label goes when
/// the portrait would otherwise fall under a third of the card: the art is the card's job, and the
/// inspector says the innate in full. The glyphs beside the column headers and inside the badges go
/// the same way, row-wide, the moment the words would run into them — a word is never overprinted
/// and never shortened to keep a glyph the colour already carries.
/// </para>
/// </remarks>
public sealed class RosterScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Met = UiInk.Good;

    // The card's own surfaces — the GearScreen cell precedent. A card is a QUIET plate with a state.
    private static readonly Color CardLocked = new(0x14, 0x10, 0x10, 0xF0);

    // ── THE CARD'S STATES (UI polish §25–§29), each a number the draw reads, none a second surface.
    //
    // HOVER is the same lift a UiKit.Button gets — a thin white luminance on the face and the edge
    // nudged a quarter of the way to white — eased in over UiMotion.Fast, so a card is noticed without
    // jumping. PRESSED is the mouse held over the card: the whole face drops PressDrop px and darkens
    // for exactly as long as the button is down, and the glow goes with it (§27); the click still
    // lands on release, on the card's AUTHORITATIVE rect, which never moves (§15). SELECTED is the
    // bone rule, ACTIVE the gold edge — both persistent structure, neither a luminance (§28). A
    // LOCKED card keeps its own dark surface and says why in its band; it still lifts on hover,
    // because it can still be picked to read.
    private const float HoverLift = 0.07f;
    private const float EdgeLift = 0.25f;
    private const float PressShade = 0.18f;
    private const int PressDrop = 2;

    // THE SWITCH HIGHLIGHT (§31 transition, §73–§82 "card / inspector update + short highlight"): a
    // gold wash over the card you just became and behind the inspector's header, UiMotion.Transition
    // long, 1 → 0. Armed ONCE, in Confirm, the moment the switch lands — never from Draw.
    private const float FlashWash = 0.26f;
    private const float HeaderWash = 0.22f;

    private readonly UiKit _ui;
    private string _selectedId = CharacterRoster.StarterId;
    private float _anim;
    private string? _notice;
    private string? _cue;                   // sound cue waiting for the host to play — the host owns audio

    /// <summary>
    /// DEV (capture rig only): pose a transient no frame-60 shutter can catch. Read from
    /// <c>RH_SHOT_ROSTER</c>, and only under <c>RH_SHOT</c>: <c>flash</c> holds the switch highlight
    /// at its peak on the active card and the inspector header; <c>held</c> draws the card under the
    /// posed cursor (<c>RH_SHOT_PAGE_MOUSE</c>) as PRESSED.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rig shoots frame 60 and a Transition is eleven frames, so a real switch is long gone by the
    /// shutter; and the rig never holds the mouse button, so a pressed card cannot be posed at all. A
    /// state no capture can pose has never been looked at (project rule). Both dials change nothing
    /// outside the rig: <see cref="DevPose"/> is null unless RH_SHOT is set.
    /// </para>
    /// <para>
    /// The dial is read here rather than added to <c>capture.sh</c>'s forwarding list because the
    /// rig's <c>dn</c> helper runs dotnet through <c>env</c>, so an exported variable reaches the game
    /// as it stands. The four poses, as run:
    /// </para>
    /// <code>
    /// RH_SHOT_UISCALE=100 RH_SHOT_PAGE_MOUSE=173,399 \
    ///     bash tools/asset-pipeline/capture.sh roster build/shots/p2_roster_hover_100.png
    /// RH_SHOT_UISCALE=100 RH_SHOT_PAGE_MOUSE=173,399 RH_SHOT_ROSTER=held \
    ///     bash tools/asset-pipeline/capture.sh roster build/shots/p2_roster_pressed_100.png
    /// RH_SHOT_UISCALE=100 RH_SHOT_ROSTER=flash \
    ///     bash tools/asset-pipeline/capture.sh rosterswitch build/shots/p2_roster_switchflash_100.png
    /// RH_SHOT_UISCALE=100 \
    ///     bash tools/asset-pipeline/capture.sh rosterswitch build/shots/p2_roster_switchsettled_100.png
    /// </code>
    /// <para>
    /// 173,399 is page space, and it lands inside the top-left card at 100 % AND at 150 % — the grid
    /// shrinks toward its own top-left as the profile grows, so one posed cursor serves both. The
    /// <c>rosterswitch</c> fixture is the one that poses the switch's OWN subject: it makes the
    /// selected hunter the active one, which is the only arrangement in which the card that lights and
    /// the header that lights describe the same hunter.
    /// </para>
    /// </remarks>
    private static readonly string? DevPose =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
            ? Environment.GetEnvironmentVariable("RH_SHOT_ROSTER")?.Trim().ToLowerInvariant()
            : null;

    private static bool DevFlashFrozen => DevPose == "flash";
    private static bool DevHeld => DevPose == "held";

    public RosterScreen(UiKit ui) => _ui = ui;

    /// <summary>DEV: pose the detail panel on a given character for the capture fixture.</summary>
    public void DevSelect(string id) => _selectedId = id;

    public bool DevRosterDebug { get; set; }

    /// <summary>
    /// The sound cue for a switch just made, cleared by reading — the host owns audio, this screen
    /// does not. <c>sfx_nav</c>: becoming another hunter is a navigation, not a purchase.
    /// </summary>
    /// <remarks>Same shape as TraitsScreen.ConsumeCue, so the host's Update reads one way everywhere.</remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    // The highlight's keys are the EVENT's, not a rectangle's: a card's rect changes with the profile,
    // and the inspector has one header whichever hunter it shows.
    private static int CardFlashKey(string characterId) => HashCode.Combine("roster.switch.card", characterId);
    private static readonly int InspectorFlashKey = HashCode.Combine("roster.switch.inspector");

    /// <summary>
    /// How strong the "you just became this hunter" highlight is on a card right now: 1 the frame the
    /// switch lands, 0 once it has settled, one <see cref="UiMotion.Transition"/> later. Exposed so a
    /// test can drive the switch and read the displayed value at t = 0, mid and end.
    /// </summary>
    /// <remarks>
    /// Under Reduced Motion this stays a short fade and settles at the same 0: it is a highlight
    /// marking "this just changed", which brief §32 keeps (it drops movement, scale and idle motion,
    /// not simple fades), and <see cref="UiMotion"/> runs pulses under Reduced for that reason. What
    /// Reduced does collapse on this screen is the card's hover, which is a
    /// <see cref="UiMotion.Ease"/> and therefore its target on the first ask.
    /// </remarks>
    public float SwitchHighlight(string characterId) => UiMotion.Pulse(CardFlashKey(characterId));

    /// <summary>The same highlight behind the inspector's header — it lights with the card it describes.</summary>
    public float InspectorHighlight => UiMotion.Pulse(InspectorFlashKey);

    /// <summary>The permanent skill set, so the inspector can say a starting skill is already known.</summary>
    public MasteryTree? Mastery { get; set; }

    /// <summary>What is worn, so the inspector can say what would come off. Null hides that line.</summary>
    public Hunter? Hunter { get; set; }

    /// <summary>The hunter just switched to, taken once. The host posts it as a toast.</summary>
    public string? TakeNotice() { var n = _notice; _notice = null; return n; }

    // ── Layout. Page-anchored: this screen used to subtract from 1920/1080 throughout. ───────────
    //
    // The three page anchors below do NOT follow the profile: they say where the two columns sit
    // against the page's edges, and a margin that grew with the type would spend the page's room on
    // air. Everything inside the columns does follow it, through UiMetrics.
    private static int Top => UiKit.PageTop;   // the first row under the chrome band, at this profile (was 120)
    private const int PageGutter = 24;      // the columns' distance from the page's left and right edges
    private const int PageFoot = 40;        // the columns' distance from the page's bottom edge
    private const int Columns = 5;

    private static int ColumnGap => UiMetrics.Space(16);
    private static int GridInset => UiMetrics.Space(24);
    private static int CardGap => UiMetrics.Space(16);
    private static int RowGap => UiMetrics.Space(20);

    /// <summary>The house inspector width (D3) — wider at the larger profiles so the bigger type keeps its line length.</summary>
    private static int InspectorWidth => UiMetrics.InspectorWidth(UiKit.Page.Width);

    /// <summary>The reading column. Its aspect picks the VERTICAL frame, whose inset is 28 — the old
    /// near-square panel wore the SQUARE frame and paid 68 px of margin plus a 28 px title drop.</summary>
    private static Rectangle Inspector =>
        new(UiKit.PageRight(PageGutter + InspectorWidth), Top, InspectorWidth, UiKit.PageBottom(PageFoot) - Top);

    /// <summary>Everyone, as a grid. A list surface is QUIET — it wore the ornate frame while the
    /// inspector beside it wore the quiet one, so the eye landed on the filigree.</summary>
    private static Rectangle GridPlate =>
        new(PageGutter, Top, Inspector.X - ColumnGap - PageGutter, UiKit.PageBottom(PageFoot) - Top);

    /// <summary>The one lit button, anchored to the inspector's foot at the primary height — never under anything.</summary>
    private static Rectangle ActionRect =>
        new(UiKit.ContentLeft(Inspector), Inspector.Bottom - UiMetrics.Space(24) - UiMetrics.ButtonHeightPrimary,
            UiKit.ContentRight(Inspector) - UiKit.ContentLeft(Inspector), UiMetrics.ButtonHeightPrimary);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.ChampionCards => new[] { GridPlate },
        TourTarget.ChampionDetail => new[] { Inspector },
        TourTarget.BecomeThem => new[] { ActionRect },
        _ => Array.Empty<Rectangle>(),
    };

    // BY CLASS COLUMN, not catalogue order. Playtest (2026-08-26): "characters of the same class
    // should sit one under the other." The arrangement is CharacterRoster.Grid — one column per class
    // in ClassColumns order, the FIRST of the class on the top row, the SECOND directly beneath.
    private static int HeaderY => UiMetrics.Space(40);
    private static int RuleY => HeaderY + UiTypography.Pitch(UiTypography.Body);
    private static int CardsY => RuleY + UiMetrics.Space(12);

    // Every card metric is DERIVED from the plate, so the grid grows with the page instead of
    // overhanging it: at 1920 a card is 249x398, at 1536 it is 172x290 — and at 150 % it is 193x379,
    // because the inspector beside it took the room the bigger type needs.
    private static int CardWidth => (GridPlate.Width - GridInset * 2 - CardGap * (Columns - 1)) / Columns;
    private static int ColumnPitch => CardWidth + CardGap;
    private static int CardHeight => (GridPlate.Height - CardsY - GridInset - RowGap) / 2;
    private static int RowPitch => CardHeight + RowGap;
    private static int GridLeft => (GridPlate.Width - (ColumnPitch * (Columns - 1) + CardWidth)) / 2;

    private static Rectangle Card(RosterCell cell) => Card(cell.Column, cell.Row);

    private static Rectangle Card(int column, int row) =>
        new(GridPlate.X + GridLeft + column * ColumnPitch, GridPlate.Y + CardsY + row * RowPitch,
            CardWidth, CardHeight);

    // ── The card's own stack: badge · portrait · name (· innate) · status band ─────────────────
    //
    // The band and the badge are CONTROL-sized (they hold a line of type each); the breaths between
    // them are SPACING. The card's border is a hairline, not a pad: 3 px at rest, 4 when it is the
    // active or the selected card, and it does not grow with the profile.
    private const int Edge = 3, EdgeLit = 4, SelectRule = 2;
    private static int BadgeH => UiMetrics.Control(36);
    private static int CardBreath => UiMetrics.Space(8);
    private static int PortraitInset => UiMetrics.Space(12);   // the art keeps its width as the controls grow (roster-05)
    private static int NameInset => UiMetrics.Space(8);

    /// <summary>
    /// The status band's height: a breath, the bar, a breath, one Body line, and the foot — 84 at
    /// 100 %. Derived rather than written, so the bar and the line it captions both keep their room
    /// when the profile grows one faster than the other.
    /// </summary>
    private static int StatusH =>
        UiMetrics.Space(22) + UiMetrics.Control(12) + UiMetrics.Space(8)
        + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(14);

    /// <summary>An unlocked card's band: one Secondary word with its breaths — the portrait keeps the rest (roster-05).</summary>
    private static int ChipBandH => UiMetrics.Space(10) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(12);

    /// <summary>The least a portrait may keep of its card before the innate label under the name gives way.</summary>
    private static int MinPortrait(Rectangle card) => card.Height * 3 / 10;

    private static string BranchName(Branch b) => b switch
    {
        Branch.Resonance => "RESONANCE", Branch.Loot => "LOOT", Branch.Tempo => "TEMPO", _ => "ENDURE",
    };

    // The same four colours the mastery tree uses. A character's lean has to read as the SAME road the
    // player walks on the tree, or the mapping is a coincidence rather than a design.
    private static Color BranchColor(Branch b) => b switch
    {
        Branch.Resonance => new Color(0xD6, 0x48, 0x5C),
        Branch.Loot => new Color(0x48, 0xB8, 0x88),
        Branch.Tempo => new Color(0x74, 0xC6, 0xE8),
        _ => new Color(0xC0, 0x6E, 0xE0),
    };

    private static Color LeanColor(Character c) => c.Lean is { } b ? BranchColor(b) : Slate;

    /// <summary>Live quest progress, set by the host each frame. Empty until quests are wired.</summary>
    public QuestProgress Progress { get; set; } = QuestProgress.Empty;

    /// <summary>What a locked character is waiting for, in the player's terms.</summary>
    /// <remarks>
    /// A quest gate reads its demand from the QUEST, not from the character's own copy of the words.
    /// Two strings saying the same thing is two strings that can disagree, and the one the player would
    /// have believed is whichever screen they happened to be on.
    /// </remarks>
    private static string UnlockText(Character c) => c.Unlock.Kind switch
    {
        UnlockKind.Start => "YOURS FROM THE START",
        UnlockKind.Conquest => c.Unlock.RegionId is { } r && Regions.Find(r) is { } def
            ? $"CONQUER {def.Name}"
            : "CONQUER A REGION",
        _ => (QuestCatalogue.Find(c.Unlock.QuestId)?.Demand ?? c.Unlock.QuestText ?? "FINISH A QUEST")
             .ToUpperInvariant(),
    };

    // The state block's fixed lines, named once so the block is MEASURED with the words it draws.
    //
    // THE PROMISE HAD TO NARROW. It said "YOUR SKILLS ... STAY", which was true while every champion's
    // own skill was banked account-wide the moment you played them. A signature belongs to one
    // champion now (BRIEF sec.9), so switching genuinely changes which techniques you can weave -
    // that is the point of the system - and the line has to say so rather than promise otherwise.
    // What DOES stay is every skill's experience: access is temporary, experience is permanent.
    // Sentence case, in the Primary ink, and shorter: four capitalised sentences in the label ink under
    // the hunter's mixed-case prose were two voices in one column, and at 150 % their four lines were
    // what pushed ROAD AND GEAR off the page (release polish 2026-09-05, roster-09, roster-15).
    private const string FreeLine = "Switching is free — gear, traits and the Warren stay, and no skill loses its levels.";
    private const string PickAnotherLine = "Pick another card to read a different hunter.";
    private const string JoinsLine = "Joins you the moment it is done.";

    public void Update(Point mouse, bool clicked, CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var hit = mouse;

        foreach (var cell in CharacterRoster.Grid)
            if (UiKit.ClickedIn(Card(cell), hit, clicked))
                _selectedId = cell.Character.Id;

        // THE SWITCH IS TAKEN HERE, in Update, on the button's authoritative rect — not in Draw off
        // the button's return value, which is where it used to land. The switch arms a highlight and
        // a sound cue, and neither may start from a draw pass; the button in Draw only shows itself.
        if (CanSetActive(state) && UiKit.ClickedIn(ActionRect, hit, clicked)) Confirm(state);
    }

    /// <summary>The one condition under which SET ACTIVE exists: the selected hunter is yours and not already you.</summary>
    private bool CanSetActive(CharacterState state)
    {
        var c = CharacterRoster.Get(_selectedId);
        return state.IsUnlocked(c.Id) && state.ActiveId != c.Id;
    }

    /// <summary>Try to become the selected hunter. Returns true when the active hunter changed.</summary>
    /// <remarks>
    /// Stays defensive about both refusals but no longer writes player text for them: the button is
    /// not drawn at all when the hunter is already active or still locked, so a refusal message here
    /// would be for a press that cannot happen. A refusal arms nothing — no cue, no highlight.
    /// </remarks>
    public bool Confirm(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var c = CharacterRoster.Get(_selectedId);
        if (state.ActiveId == c.Id || !state.IsUnlocked(c.Id)) return false;
        state.Select(c.Id);
        _notice = c.Name;
        // The card you became and the header that now names you light together, once, for one
        // Transition — the "at once, with a short highlight" the brief asks for, and no dialog.
        UiMotion.Flash(CardFlashKey(c.Id), UiMotion.Transition);
        UiMotion.Flash(InspectorFlashKey, UiMotion.Transition);
        _cue = "sfx_nav";
        return true;
    }

    public void Draw(SpriteBatch b, CharacterState state, Point mouse)
    {
        ArgumentNullException.ThrowIfNull(state);
        _anim += 1f / 60f;
        var hit = mouse;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0B, 0x09, 0x08, 0xC0));
        _ui.TextCenterBig(b, "ROSTER", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);
        // (The SWITCH FREELY banner is gone. It was the least readable line on the screen, it was drawn
        //  on every visit, and it over-promised — gear the new hunter cannot wear does come off. The
        //  honest version of it now sits in the state block, beside the button it de-risks.)

        DrawGrid(b, state, hit);
        DrawDetail(b, state, hit);
        if (DevRosterDebug)
            foreach (var r in new[] { GridPlate, Inspector })
            {
                _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
                _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            }
    }

    private void DrawGrid(SpriteBatch b, CharacterState state, Point hit)
    {
        // QUIET, not ornate: this is a list of things to pick between, and it was outranking the
        // column that does the explaining. The panel title THE ROSTER went with the frame — it said
        // again, 60 px lower, what the screen title says — and so did the `2 / 10` counter, which had
        // no legal home (the hint slot owns the band above, the currency pills own the right).
        _ui.Plate(b, GridPlate);

        // THE COLUMN HEADERS: the class, plural, in the class's colour with its glyph, over a rule the
        // width of the column — so the two cards beneath read as one class's pair before either is read.
        // The glyph and the words are ONE GROUP centred on the column — they used to sit at two fixed
        // offsets from its centre, which the bigger type walked straight across. If the widest group on
        // the row would not clear its column, the whole row drops its glyphs together: the badge on the
        // card below carries the same one, and a row with three glyphs and two gaps reads as a fault.
        // Heads at Secondary — they are heads, not prose — so the class glyph stays beside the word at
        // 150 % (roster-10), and in the Secondary ink: the class colour lives in the rule under the head,
        // the badge and the card's edge, and a red head read as an error line (roster-12).
        var headIcon = UiMetrics.IconSmall;
        var headGap = UiMetrics.Space(6);
        var withIcon = true;
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var w = headIcon + headGap + _ui.MeasureBig($"THE {ItemClasses.PluralOf(CharacterRoster.ClassColumns[column])}", UiTypography.Secondary);
            if (w > ColumnPitch - UiMetrics.Space(4)) withIcon = false;
        }
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var cls = CharacterRoster.ClassColumns[column];
            var top = Card(column, 0);
            var color = UiKit.ClassColor(cls);
            var label = $"THE {ItemClasses.PluralOf(cls)}";
            var textW = _ui.MeasureBig(label, UiTypography.Secondary);
            var groupW = (withIcon ? headIcon + headGap : 0) + textW;
            var x = top.Center.X - groupW / 2;
            var y = GridPlate.Y + HeaderY;
            if (withIcon)
            {
                _ui.ClassIcon(b, cls, new Rectangle(x, y + (UiTypography.Secondary - headIcon) / 2, headIcon, headIcon));
                x += headIcon + headGap;
            }
            _ui.TextBig(b, label, x, y, Slate, UiTypography.Secondary);
            _ui.Fill(b, new Rectangle(top.X, GridPlate.Y + RuleY, top.Width, 2), color * 0.6f);
        }

        // THE BADGE'S GLYPHS follow the headers' rule. The class glyph sits in the left slot and the
        // lock in the right only while the widest class name, centred, clears both slots — true at
        // 100 and 125 %. At 150 % the name is wider than the room between the slots (WANDERER is 117
        // of a 187 px badge), so the whole grid drops the class glyph — the badge's colour and word
        // already say the class — and a locked card's lock joins its label as ONE CENTRED GROUP, the
        // way the header's glyph joins its word. The label is never shortened while it has a slot's
        // worth of room, and never overprinted.
        var glyph = UiMetrics.IconSmall;
        var slot = CardBreath + glyph + CardBreath;
        var badgeGlyphs = true;
        foreach (var cls in CharacterRoster.ClassColumns)
            if (_ui.MeasureBig(ItemClasses.NameOf(cls), UiTypography.Secondary) > CardWidth - 2 * EdgeLit - 2 * slot)
                badgeGlyphs = false;

        var i = -1;
        foreach (var cell in CharacterRoster.Grid)
        {
            i++;
            var c = cell.Character;
            var card = Card(cell);
            var unlocked = state.IsUnlocked(c.Id);
            var active = state.ActiveId == c.Id;
            var sel = _selectedId == c.Id;
            var hot = card.Contains(hit);
            // No card hover tip: it crossed into the inspector and covered SET ACTIVE at 125/150 %, and
            // the inspector is the card's explanation (roster-04). Click selects; the column explains.

            var t = active || sel ? EdgeLit : Edge;
            // HOVER eases in (UiMotion.Fast; at once under Reduced Motion) — the same lift a button
            // gets, on every card, because every card can be picked to read: hover is a luminance, not
            // a claim, and the three surfaces (quiet, locked, active) stay what they are under it.
            // PRESSED reads the held button straight: the face drops and darkens, the glow goes. The
            // AUTHORITATIVE rect — the hit, the tip's anchor, the click Update takes — is Card(cell);
            // only the DRAWN face moves, the way UiKit.Button's does.
            var held = hot && (UiKit.MouseHeld || DevHeld);
            var lift = UiMotion.Ease(UiMotion.KeyOf(card), hot ? 1f : 0f);
            var glow = held ? 0f : lift;
            var flash = Math.Max(SwitchHighlight(c.Id), DevFlashFrozen && active ? 1f : 0f);
            if (held) card = new Rectangle(card.X, card.Y + PressDrop, card.Width, card.Height);
            var inner = new Rectangle(card.X + t, card.Y + t, card.Width - 2 * t, card.Height - 2 * t);
            // THE HOUSE PLATE under every card (roster-07): ten hand-filled rectangles with saturated
            // 3 px strokes and full-chroma bands outranked the hunter art and read as a colour legend.
            // Locked keeps its darker tint; ACTIVE is the plate with a warm gold wash and its gold edge,
            // not a brown face beside nine navy ones (roster-14).
            _ui.Plate(b, card);
            if (!unlocked) _ui.Fill(b, card, CardLocked);
            else if (active) _ui.Fill(b, inner, Gold * 0.08f);
            if (glow > 0f) _ui.Fill(b, inner, Color.White * (HoverLift * glow));
            if (held) _ui.Fill(b, inner, Color.Black * PressShade);

            // ACTIVE WINS. The edge used to read `sel ? Bone : active ? Gold : …`, so clicking the
            // hunter you are playing took its gold away — the one law the colour carries, lost on the
            // one card it matters most on. Selection adds a second cue instead of replacing the first.
            // Only a STATE strokes the plate (the plate has its own rule): gold for the hunter you
            // are playing, the class colour at a quarter for one you may play, nothing for a locked one.
            if (active || unlocked)
            {
                var edge = active ? Gold : UiKit.ClassColor(c.Class) * 0.45f;
                if (glow > 0f) edge = Color.Lerp(edge, Color.White, EdgeLift * glow);
                var et = active ? t : 1;
                _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, et), edge);
                _ui.Fill(b, new Rectangle(card.X, card.Bottom - et, card.Width, et), edge);
                _ui.Fill(b, new Rectangle(card.X, card.Y, et, card.Height), edge);
                _ui.Fill(b, new Rectangle(card.Right - et, card.Y, et, card.Height), edge);
            }
            if (sel)
            {
                _ui.Fill(b, new Rectangle(inner.X, inner.Y, inner.Width, SelectRule), Bone);
                _ui.Fill(b, new Rectangle(inner.X, inner.Bottom - SelectRule, inner.Width, SelectRule), Bone);
                _ui.Fill(b, new Rectangle(inner.X, inner.Y, SelectRule, inner.Height), Bone);
                _ui.Fill(b, new Rectangle(inner.Right - SelectRule, inner.Y, SelectRule, inner.Height), Bone);
            }

            // THE CLASS BADGE, at FULL strength whether locked or not. It was drawn at 0.45 alpha on a
            // locked card, which halved the one element that reads at any size — and the padlock says
            // "locked" better than a dimmed colour does.
            // THE CLASS BADGE: the class colour as an ACCENT — a quarter-strength wash under a full-
            // colour top rule, the word in Bone — not a full-chroma band (roster-07). The lock is the
            // house padlock, cut from the slot tile, in Bone: tinted with a dark ink over a coloured band
            // it came out as a solid black square (roster-01).
            var classColor = UiKit.ClassColor(c.Class);
            var badge = new Rectangle(card.X + t, card.Y + t, card.Width - 2 * t, BadgeH);
            _ui.Fill(b, badge, classColor * 0.28f);
            _ui.Fill(b, new Rectangle(badge.X, badge.Y, badge.Width, UiMetrics.Control(3)), classColor);
            var ink = Bone;
            var glyphY = badge.Y + (BadgeH - glyph) / 2;
            var labelY = badge.Y + (BadgeH - UiTypography.Secondary) / 2;
            var label = ItemClasses.NameOf(c.Class);
            if (badgeGlyphs)
            {
                _ui.ClassIcon(b, c.Class, new Rectangle(badge.X + CardBreath, glyphY, glyph, glyph), fallback: classColor);
                _ui.TextCenterBig(b, label, badge.Center.X, labelY, ink, UiTypography.Secondary);
                if (!unlocked)
                    _ui.LockGlyph(b, new Rectangle(badge.Right - CardBreath - glyph, glyphY, glyph, glyph), ink);
            }
            else
            {
                // The group breathes at the tight rate: the badge is already too narrow for a slot, and
                // the lock's box carries its own clear margin around the ink. WANDERER beside a lock
                // needs 163 of the badge's 187 px at 150 %; a full breath at each end would have cost
                // it its last letters, and a shortened word is the one thing this branch exists to avoid.
                var tight = UiMetrics.Space(4);
                var lockW = unlocked ? 0 : tight + glyph;
                label = _ui.ShortenBig(label, badge.Width - 2 * tight - lockW, UiTypography.Secondary);
                var labelW = _ui.MeasureBig(label, UiTypography.Secondary);
                var x = badge.Center.X - (labelW + lockW) / 2;
                _ui.TextBig(b, label, x, labelY, ink, UiTypography.Secondary);
                if (!unlocked)
                    _ui.LockGlyph(b, new Rectangle(x + labelW + tight, glyphY, glyph, glyph), ink);
            }

            // THE TEXT BLOCK under the portrait: the short name at Headline — on two lines when the card
            // is too narrow for it on one, which only happens at 150 % — and the innate label, which
            // gives way when it would leave the portrait under a third of the card.
            // THE NAME ON ONE ROW, a rung down when it would wrap: FALLING TOWER on two Headline rows
            // left its portrait half its neighbours' at 150 % (roster-02) — the same fit-or-step policy
            // ShortenBig embodies, applied to the rung instead of the letters.
            var bodyH = UiTypography.Pitch(UiTypography.Body);
            var nameW = card.Width - NameInset * 2;
            // MEASURED, NOT WRAPPED. The old test asked WrapBig whether the name took two lines — and
            // WrapBig cannot break a word that has no spaces, so METRONOME, OATHBOUND and THORNWALL all
            // reported "one line" and were ellipsised at Headline instead of stepping down. Only the
            // two-word names were ever caught. FitRung asks the question the test meant to ask: what is
            // the largest rung the whole name fits at?
            var nameRung = _ui.FitRung(c.ShortName, nameW, UiTypography.Headline);
            var headlineH = UiTypography.Pitch(nameRung);
            const int nameRows = 1;

            // The hunter themselves, taking whatever the stack leaves. Each breathes on its own phase,
            // so ten cards do not pulse in unison. An unlocked card's band is a WORD, so it reserves a
            // word's height, not the locked band's bar-and-caption stack (roster-05).
            var bandH = unlocked ? ChipBandH : StatusH;
            var statusTop = card.Bottom - t - bandH;
            var py = badge.Bottom + CardBreath;
            var room = statusTop - CardBreath - py;
            var showLabel = room - (headlineH * nameRows + bodyH) >= MinPortrait(card);
            var ph = room - headlineH * nameRows - (showLabel ? bodyH : 0);
            var portrait = new Rectangle(card.X + PortraitInset, py, card.Width - PortraitInset * 2, ph);
            // A READABLE grey, not the old near-black 0x2A2830, which turned a locked hunter into a
            // silhouette you could not tell from any other locked hunter.
            var tint = unlocked ? Color.White : new Color(0x6E, 0x6A, 0x78);
            if (!_ui.AnimSprite(b, c.StripKey("idle"), portrait, _anim + i * 0.37f, 10f, loop: true, tint, -1f))
                _ui.SpriteGrounded(b, c.SpriteKey, portrait, tint, 0.02f);

            // SHORT NAME at Headline: the full name is what the inspector is for, and THE FALLING TOWER
            // at this rung would ellipsise on a 249 px card.
            var ny = portrait.Bottom + CardBreath;
            var nameInk = unlocked ? Bone : Slate;
            _ui.TextCenterBig(b, _ui.ShortenBig(c.ShortName, nameW, nameRung), card.Center.X, ny, nameInk, nameRung);
            // The identity label: the hunter's own innate, which is unique to them — unlike the road or
            // the tier line, which the card used to spend two rows on.
            if (showLabel)
            {
                // The innate's name is the card's second identifier — MANY MOUTHS read "MANY MOUT…" at
                // 150 %. Same rule as the name above it: shrink, do not cut.
                var innateRung = _ui.FitRung(c.PassiveName, nameW, UiTypography.Body);
                _ui.TextCenterBig(b, _ui.ShortenBig(c.PassiveName, nameW, innateRung),
                                  card.Center.X, ny + headlineH * nameRows, Slate, innateRung);
            }

            DrawStatusBand(b, c, new Rectangle(card.X, statusTop, card.Width, bandH), unlocked, active);

            // THE SWITCH HIGHLIGHT: one gold wash over the whole card — art, name, chip — for one
            // Transition after you became this hunter. OVER the content, so it reads as a flash on
            // the card rather than a fourth surface under it. The PLAYING chip lights with the rest
            // of the card and has no state of its own: it is a label, not a button, and it never
            // lifts, drops or darkens on its own account.
            if (flash > 0f) _ui.Fill(b, card, Gold * (FlashWash * flash));
        }
    }

    // ── THE STATUS BAND: what this card is, or how close it is. ──────────────────────────────────
    //
    // A conquest-gated card used to say the single word LOCKED, although the region depth it is
    // waiting on was already inside the QuestProgress the screen is handed every frame. Every gate
    // counts now. Every offset in here is the same part of StatusH it was at 100 %.
    private void DrawStatusBand(SpriteBatch b, Character c, Rectangle band, bool unlocked, bool active)
    {
        var barH = UiMetrics.Control(12);
        var barInset = UiMetrics.Space(24);
        var textW = band.Width - NameInset * 2;
        if (unlocked)
        {
            // THE STATE AS A WORD with a small diamond before it — not a bordered box, which sat where
            // a button sits and made the grid read as ten buttons beside the one real one (roster-06).
            var word = active ? "PLAYING" : "READY";
            var col = active ? Gold : Met;
            var pip = UiMetrics.Control(8);
            var wordW = _ui.MeasureBig(word, UiTypography.Secondary);
            var x = band.Center.X - (pip + UiMetrics.Space(8) + wordW) / 2;
            var y = band.Y + UiMetrics.Space(10);
            _ui.Diamond(b, new Rectangle(x, y + (UiTypography.Secondary - pip) / 2, pip, pip), col);
            _ui.TextBig(b, word, x + pip + UiMetrics.Space(8), y, col, UiTypography.Secondary);
            return;
        }

        if (QuestCatalogue.Find(c.Unlock.QuestId) is { } q)
        {
            var pct = q.Threshold <= 0 ? 0f : Math.Clamp(q.Current(Progress) / (float)q.Threshold, 0f, 1f);
            var barY = band.Y + UiMetrics.Space(22);
            _ui.Bar(b, band.X + barInset, barY, band.Width - barInset * 2, barH, pct, Met);
            _ui.TextCenterBig(b, _ui.ShortenBig(q.ProgressLine(Progress), textW, UiTypography.Body),
                              band.Center.X, barY + barH + UiMetrics.Space(8), Bone, UiTypography.Body);
            return;
        }

        if (c.Unlock.Kind == UnlockKind.Conquest && c.Unlock.RegionId is { } region)
        {
            var (now, need) = ConquestProgress(region);
            var nameY = band.Y + UiMetrics.Space(2);
            // The region's name takes the band's full width (it has no glyph slots), so THE STILL
            // ARCHIVE is not cut on a 172 px card at 150 % (roster-11).
            if (Regions.Find(region) is { } def)
                _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, band.Width - UiMetrics.Space(8), UiTypography.Secondary),
                                  band.Center.X, nameY, Slate, UiTypography.Secondary);
            var barY = nameY + UiTypography.Pitch(UiTypography.Secondary);
            _ui.Bar(b, band.X + barInset, barY, band.Width - barInset * 2, barH, need <= 0 ? 0f : now / (float)need, Met);
            _ui.TextCenterBig(b, $"{now} / {need} WAVES", band.Center.X, barY + barH + UiMetrics.Space(8), Bone, UiTypography.Body);
            return;
        }

        _ui.TextCenterBig(b, "LOCKED", band.Center.X, band.Y + UiMetrics.Space(30), Slate, UiTypography.Body);
    }

    /// <summary>Waves held toward conquering a region, out of <see cref="Checkpoints.ConquestWave"/>.</summary>
    /// <remarks>
    /// The same number the host compares against when it decides a region is conquered — it comes from
    /// the QuestProgress snapshot, which is built from each region's best depth.
    /// </remarks>
    private (int Now, int Need) ConquestProgress(string regionId) =>
        (Math.Min(Progress.DepthByRegion.TryGetValue(regionId, out var d) ? d : 0, Checkpoints.ConquestWave),
         Checkpoints.ConquestWave);

    // ── THE INSPECTOR — the house grammar (§6): CATEGORY · NAME · IDENTITY · what it gives you ·
    //    what it always does · what it wears · CURRENT STATE or YOU NEED FIRST · one button. ──────
    //
    // MEASURED BEFORE IT IS DRAWN. The column has a fixed height and the type has three sizes, so the
    // blocks are costed against the room above the state block (which is itself costed against the
    // button) and dropped from the least important end: ROAD AND GEAR first, then the blurb line by
    // line, then the innate's third and second lines. What carries the decision — the starting skill,
    // the innate's name, the state — is never the thing that goes.

    /// <summary>The skill row: its icon's box plus a breath — 36 at 100 %.</summary>
    private static int SkillIconRow => UiMetrics.Control(32) + UiMetrics.Space(4);

    /// <summary>The lock glyph's column in YOU NEED FIRST: the glyph plus a breath — 32 at 100 %.</summary>
    private static int LockColumn => UiMetrics.Control(22) + UiMetrics.Space(10);

    private int Lines(string text, int width, int cap, int rung = 0)
        => Math.Clamp(_ui.WrapBig(text, width, rung == 0 ? UiTypography.Body : rung).Count, 1, cap);

    /// <summary>The road and what the class wears, in one sentence — colour AND word, never the hue alone.</summary>
    private static string RoadLine(Character c)
        => (c.Lean is { } br ? $"{BranchName(br)} ROAD" : "NO ROAD — EVERY ROAD FITS") + " · " + ItemClasses.WearsLine(c.Class);

    private void DrawDetail(SpriteBatch b, CharacterState state, Point hit)
    {
        _ui.PanelQuiet(b, Inspector);
        var c = CharacterRoster.Get(_selectedId);
        var unlocked = state.IsUnlocked(c.Id);
        var active = state.ActiveId == c.Id;
        var lean = LeanColor(c);

        var left = UiKit.ContentLeft(Inspector);
        var right = UiKit.ContentRight(Inspector);
        var width = right - left;
        var lineH = UiTypography.Pitch(UiTypography.Body);
        var headH = UiTypography.Pitch(UiTypography.Secondary);
        var ruleH = UiMetrics.Space(10);
        var stateGap = UiMetrics.Space(24);
        var skill = c.SignatureSkillId is { } sid ? SkillCatalogue.Find(sid) : null;

        // The state block FOLLOWS the content, and the content is cut so that it always can: the block
        // used to be reserved as a flat 232 px, which was one hunter's worth at one profile.
        var showButton = unlocked && !active;
        // The column's foot is the button when there is one, else the content edge itself — the extra
        // breath that was reserved for a button not drawn cost a locked hunter its innate at 150 % (roster-03).
        var limit = showButton ? ActionRect.Y - UiMetrics.Space(12) : UiKit.ContentBottom(Inspector);
        var y = Inspector.Y + UiTypography.PanelTitleTop;

        var blurbAll = Lines(c.Blurb, width, 2);
        var skillLines = skill is null ? 1 : Lines(skill.Line, width, 2);
        var passiveAll = Lines(c.PassiveText, width, 3);

        var fixedBase = headH + UiMetrics.Space(6)                                               // 1 CATEGORY
                   + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(2)             // 2 NAME
                   + ruleH + headH + (skill is null ? lineH : SkillIconRow + skillLines * lineH) // 4 STARTING SKILL
                   + ruleH + headH + lineH                                                      // 5 INNATE, its name
                   + stateGap;
        // THE STATE BLOCK YIELDS ITS BOILERPLATE before the innate loses its one line: at 150 % the
        // column cannot hold the header, the skill, the innate AND four lines of state, and a block that
        // did not yield drew its last line under SET ACTIVE. The shed line — the one sentence that
        // de-risks the press — never yields.
        var stateH = StateBlockHeight(c, width, unlocked, active, brief: false);
        var stateBrief = fixedBase + stateH + lineH > limit - y;
        if (stateBrief) stateH = StateBlockHeight(c, width, unlocked, active, brief: true);
        var fixedH = fixedBase + stateH;                                                          // 7 the state
        // ROAD AND GEAR is one sentence at Secondary now — road · what it wears — wrapped to at most two
        // lines, so it fits the 150 % column instead of being the first thing dropped (roster-09).
        var roadText = RoadLine(c);
        var roadLines = Lines(roadText, width - UiMetrics.Control(8) - UiMetrics.Space(8), 2, UiTypography.Secondary);
        var roadH = ruleH + headH + roadLines * headH;
        var room = limit - y - fixedH;
        int Need(int blurb, int passive, bool withRoad)
            => (blurb > 0 ? blurb * lineH + UiMetrics.Space(10) : 0) + passive * lineH + (withRoad ? roadH : 0);

        var road = Need(blurbAll, passiveAll, true) <= room;
        var blurbLines = blurbAll;
        while (!road && blurbLines > 0 && Need(blurbLines, passiveAll, false) > room) blurbLines--;
        var passiveLines = passiveAll;
        while (!road && blurbLines == 0 && passiveLines > 1 && Need(0, passiveLines, false) > room) passiveLines--;

        void Rule()
        {
            _ui.Fill(b, new Rectangle(left, y, width, 1), Dim);
            y += ruleH;
        }

        // (No WHO THEY ARE panel title: a generic gold headline that outranked the hunter's own name
        //  thirty pixels beneath it. The name IS the title.)

        // THE HEADER LIGHTS WITH THE CARD: the same gold wash, behind CATEGORY and NAME, for the same
        // Transition — so the inspector is SEEN to update, not merely found updated (§35). The frame
        // stays; only the header's ground flashes.
        var flash = Math.Max(InspectorHighlight, DevFlashFrozen && active ? 1f : 0f);
        if (flash > 0f)
        {
            var headerH = headH + UiMetrics.Space(6) + UiTypography.Pitch(UiTypography.Headline);
            var pad = UiMetrics.Space(8);
            // CLIPPED TO THE PANEL'S INSIDE. The wash breathes a pad past the content column so it reads
            // as a ground BEHIND the two header lines rather than a stripe under them — and that pad is
            // then cut back to UiKit.PanelInner, because the frame's ornament is the panel's own art and
            // a wash lying over it would read as a fault in the frame rather than as feedback.
            var wash = new Rectangle(left - pad, y - pad, width + pad * 2, headerH + pad * 2);
            _ui.Fill(b, Rectangle.Intersect(wash, UiKit.PanelInner(Inspector)), Gold * (HeaderWash * flash));
        }

        // 1 CATEGORY
        var catIcon = UiMetrics.Control(26);
        _ui.ClassIcon(b, c.Class, new Rectangle(left, y, catIcon, catIcon));
        _ui.TextBig(b, $"HUNTER · {ItemClasses.NameOf(c.Class)}", left + catIcon + UiMetrics.Space(10),
                    y + (catIcon - UiTypography.Secondary) / 2, Slate, UiTypography.Secondary);   // the class icon carries the colour (roster-12)
        y += headH + UiMetrics.Space(6);

        // 2 NAME
        // A HUNTER'S NAME, on the screen whose whole job is telling them apart: METRONOME read
        // "METRONO…" and OATHBOUND "OATHBOU…" at 150 %. Names shrink here, they do not lose letters.
        var nameRung = _ui.FitRung(c.Name, width, UiTypography.Headline);
        _ui.TextBig(b, _ui.ShortenBig(c.Name, width, nameRung), left, y,
                    active ? Gold : unlocked ? Bone : Slate, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(2);

        // 3 IDENTITY — the flavour, and the first thing to go after ROAD AND GEAR: the card's hover
        //   tip says it too.
        if (blurbLines > 0)
            y = DrawWrapped(b, c.Blurb, left, y, width, Slate, UiTypography.Body, blurbLines) + UiMetrics.Space(10);

        // 4 SIGNATURE SKILL — the fact that most separates two hunters (BRIEF sec.11).
        //
        //   It said STARTING SKILL and, on the right, ALREADY KNOWN — because the skill was one of the
        //   twelve shared ones and playing this champion banked it account-wide for ever. Both halves
        //   were true and both are now wrong: the skill is this champion's alone, no other champion
        //   can ever weave it, and unlocking them does not add it to anybody's library.
        Rule();
        _ui.TextBig(b, "SIGNATURE SKILL", left, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"ONLY {c.Name}", right, y, Slate, UiTypography.Secondary);   // a descriptor, not an earned state (roster-13)
        y += headH;
        if (skill is null)
        {
            _ui.TextBig(b, "NONE.", left, y, Slate, UiTypography.Body);
            y += lineH;
        }
        else
        {
            var skillIcon = UiMetrics.Control(32);
            var skillText = left + skillIcon + UiMetrics.Space(10);
            _ui.Icon(b, $"icon_skill_{skill.Id}", new Rectangle(left, y, skillIcon, skillIcon), unlocked ? Gold : Slate);
            _ui.TextBig(b, _ui.ShortenBig(
                            $"{skill.Name} · {skill.Style.ToString().ToUpperInvariant()} · {(skill.TakesABeat ? "ACTIVE" : "PASSIVE")}",
                            right - skillText, UiTypography.Body),
                        skillText, y + (skillIcon - UiTypography.Body) / 2, Bone, UiTypography.Body);
            y += SkillIconRow;
            y = DrawWrapped(b, skill.Line, left, y, width, Bone, UiTypography.Body, skillLines);
        }

        // 5 INNATE — always on. The word is glossed in place rather than assumed.
        Rule();
        _ui.TextBig(b, "INNATE — ALWAYS ON", left, y, Slate, UiTypography.Secondary);
        y += headH;
        _ui.TextBig(b, c.PassiveName, left, y, Bone, UiTypography.Body);
        y += lineH;
        y = DrawWrapped(b, c.PassiveText, left, y, width, Bone, UiTypography.Body, passiveLines);

        // 6 ROAD AND GEAR — dropped first when the page is short.
        if (road)
        {
            Rule();
            _ui.TextBig(b, "ROAD AND GEAR", left, y, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, c.TierLine, right, y, Slate, UiTypography.Secondary);
            y += headH;
            // ONE line: a colour square for the road, then the road and what the class wears in words —
            // colour AND word, and never the branch hue as type (roster-09, roster-12).
            var pip = UiMetrics.Control(8);
            _ui.Diamond(b, new Rectangle(left, y + (UiTypography.Secondary - pip) / 2, pip, pip), lean);
            y = DrawWrapped(b, roadText, left + pip + UiMetrics.Space(8), y, width - pip - UiMetrics.Space(8),
                            Bone, UiTypography.Secondary, roadLines);
        }

        // 7 THE STATE — pinned to the column's foot, so CURRENT STATE / YOU NEED FIRST sits directly
        //   above the button (or the foot) and the free room lands under the descriptive blocks
        //   instead of between the sentence that de-risks the press and the press itself (roster-08).
        DrawStateBlock(b, c, Math.Max(y + stateGap, limit - stateH), left, right, width, unlocked, active, stateBrief);

        // 8 ONE BUTTON, and only when it can be pressed. A disabled PLAYING / LOCKED button is a
        // control that invites a click with no answer; the state block above already said both.
        // The press is TAKEN IN UPDATE on this same rect (see Update); here the button only shows
        // its hover and its press, so nothing — no switch, no cue, no highlight — starts from Draw.
        if (showButton) _ui.Button(b, ActionRect, "SET ACTIVE", hit, clicked: false, true, ButtonStyle.Primary);
    }

    /// <summary>How many worn pieces this hunter could not wear — what a switch to them sheds.</summary>
    private int ShedCount(Character c)
    {
        var shed = 0;
        foreach (var slot in Enum.GetValues<GearSlot>())
            if (Hunter?.Worn(slot) is { } w && !Gear.CanWear(c, w)) shed++;
        return shed;
    }

    private static string ShedLine(int shed) =>
        shed == 0 ? "Nothing comes off — this hunter can wear everything you have on."
        : shed == 1 ? "1 worn piece goes back to your bag — this hunter cannot wear it."
        : $"{shed} worn pieces go back to your bag — this hunter cannot wear them.";

    /// <summary>The gate a locked hunter is behind: how far along, the count, and what is counted.</summary>
    private (float Pct, string Count, string Unit) Gate(Character c)
    {
        if (QuestCatalogue.Find(c.Unlock.QuestId) is { } q)
            return (q.Threshold <= 0 ? 0f : Math.Clamp(q.Current(Progress) / (float)q.Threshold, 0f, 1f),
                    q.ProgressText(Progress), q.Unit.ToUpperInvariant());
        if (c.Unlock.Kind == UnlockKind.Conquest && c.Unlock.RegionId is { } region)
        {
            var (now, need) = ConquestProgress(region);
            return (need <= 0 ? 0f : now / (float)need, $"{now} / {need}",
                    Regions.Find(region) is { } def ? $"WAVES IN {def.Name}" : "WAVES");
        }
        return (0f, "", "");
    }

    /// <summary>The state block's height, measured with the words <see cref="DrawStateBlock"/> draws.</summary>
    private int StateBlockHeight(Character c, int width, bool unlocked, bool active, bool brief)
    {
        var lineH = UiTypography.Pitch(UiTypography.Body);
        var h = UiMetrics.Space(12) + UiTypography.Pitch(UiTypography.Secondary);
        if (unlocked)
        {
            // Three lines each: at 150 % the shed sentence needs three, and a block that reserved two
            // drew its third line under SET ACTIVE.
            h += lineH + (brief ? 0 : Lines(FreeLine, width, 3) * lineH);   // brief: the boilerplate yields, the shed line never does
            h += Lines(active ? PickAnotherLine : ShedLine(ShedCount(c)), width, 3) * lineH;
            return h;
        }
        h += Lines(UnlockText(c), width - LockColumn, 2) * lineH + UiMetrics.Space(6);
        if (Gate(c).Count.Length > 0)
            h += UiMetrics.Control(16) + UiMetrics.Space(10) + UiTypography.Pitch(UiTypography.PrimaryValue);
        return h + (brief ? 0 : Lines(JoinsLine, width, 2) * lineH);
    }

    /// <summary>CURRENT STATE for a hunter you can play, YOU NEED FIRST for one you cannot.</summary>
    private void DrawStateBlock(SpriteBatch b, Character c, int y, int left, int right, int width,
                                bool unlocked, bool active, bool brief)
    {
        var lineH = UiTypography.Pitch(UiTypography.Body);
        _ui.Fill(b, new Rectangle(left, y, width, 1), Dim);
        y += UiMetrics.Space(12);

        if (unlocked)
        {
            _ui.TextBig(b, "CURRENT STATE", left, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
            _ui.TextBig(b, active ? "YOU ARE PLAYING THIS HUNTER" : "READY TO PLAY", left, y,
                        active ? Gold : Met, UiTypography.Body);
            y += lineH;
            if (!brief) y = DrawWrapped(b, FreeLine, left, y, width, Bone, UiTypography.Body, 3);
            if (active)
            {
                DrawWrapped(b, PickAnotherLine, left, y, width, Bone, UiTypography.Body, 3);
                return;
            }

            // WHAT DOES COME OFF. The same test the host runs after the switch — so the one thing the
            // old banner got wrong is stated before the press, not discovered after it.
            DrawWrapped(b, ShedLine(ShedCount(c)), left, y, width, Bone, UiTypography.Body, 3);
            return;
        }

        // LOCKED — the requirement, then how close, then the promise. Nothing here is gold: a gate is
        // not a reward.
        _ui.TextBig(b, "YOU NEED FIRST", left, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        var lockGlyph = UiMetrics.Control(22);
        _ui.LockGlyph(b, new Rectangle(left, y + (lineH - lockGlyph) / 2, lockGlyph, lockGlyph), Bone);
        y = DrawWrapped(b, UnlockText(c), left + LockColumn, y, width - LockColumn, Bone, UiTypography.Body, 2) + UiMetrics.Space(6);

        var (pct, count, unit) = Gate(c);
        if (count.Length > 0)
        {
            var barH = UiMetrics.Control(16);
            _ui.Bar(b, left, y, width, barH, pct, Met);
            y += barH + UiMetrics.Space(10);
            _ui.TextBig(b, count, left, y, Bone, UiTypography.PrimaryValue);
            var countW = _ui.MeasureBig(count, UiTypography.PrimaryValue) + UiMetrics.Space(16);
            _ui.TextRightBig(b, _ui.ShortenBig(unit, width - countW, UiTypography.Body), right,
                             y + (UiTypography.PrimaryValue - UiTypography.Body) / 2, Slate, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.PrimaryValue);
        }
        if (!brief) DrawWrapped(b, JoinsLine, left, y, width, Bone, UiTypography.Body, 2);
    }

    /// <summary>Word-wrap into a width at a rung, bounded. Returns the y AFTER the last line drawn.</summary>
    /// <remarks>
    /// The ToUpperInvariant is gone: labels, heads and requirement lines are written in capitals at the
    /// call site, and Core's prose — a blurb, an innate's text, a skill's line — is drawn as authored,
    /// which is how the other passed screens draw it and how it survives 720p.
    /// </remarks>
    private int DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c, int px, int maxLines)
    {
        var lines = _ui.WrapBig(text, width, px);
        var pitch = UiTypography.Pitch(px);
        for (var i = 0; i < Math.Min(lines.Count, maxLines); i++)
        {
            var last = i == maxLines - 1 && lines.Count > maxLines;
            _ui.TextBig(b, last ? _ui.ShortenBig(lines[i] + " …", width, px) : lines[i], x, y, c, px);
            y += pitch;
        }
        return y;
    }
}
