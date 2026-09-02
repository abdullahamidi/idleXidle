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
/// separate two hunters — the STARTING SKILL they hand you, latched permanently into your skill set
/// the moment you play them, and the INNATE power that is always on — neither of which this screen
/// showed at all before UX V2 P2.2.
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
    private static readonly Color CardBg = new(0x1C, 0x18, 0x28, 0xF0);
    private static readonly Color CardHot = new(0x2C, 0x25, 0x44, 0xF0);
    private static readonly Color CardLocked = new(0x12, 0x0F, 0x1A, 0xF0);
    private static readonly Color CardActive = new(0x2A, 0x22, 0x10, 0xF0);

    private readonly UiKit _ui;
    private string _selectedId = CharacterRoster.StarterId;
    private float _anim;
    private string? _notice;
    private string? _tip;
    private Point _tipAt;

    public RosterScreen(UiKit ui) => _ui = ui;

    /// <summary>DEV: pose the detail panel on a given character for the capture fixture.</summary>
    public void DevSelect(string id) => _selectedId = id;

    public bool DevRosterDebug { get; set; }

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
    private const int Top = 120;            // the hint slot owns canvas y 86..134
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
    private static int PortraitInset => UiMetrics.Space(20);
    private static int NameInset => UiMetrics.Space(8);

    /// <summary>
    /// The status band's height: a breath, the bar, a breath, one Body line, and the foot — 84 at
    /// 100 %. Derived rather than written, so the bar and the line it captions both keep their room
    /// when the profile grows one faster than the other.
    /// </summary>
    private static int StatusH =>
        UiMetrics.Space(22) + UiMetrics.Control(12) + UiMetrics.Space(8)
        + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(14);

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
    private const string FreeLine = "SWITCHING IS FREE. YOUR SKILLS, TRAITS, GEAR AND THE WARREN STAY.";
    private const string PickAnotherLine = "PICK ANOTHER CARD TO READ A DIFFERENT HUNTER.";
    private const string JoinsLine = "THIS HUNTER JOINS YOU THE MOMENT IT IS DONE.";

    public void Update(Point mouse, bool clicked, CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var hit = mouse;

        foreach (var cell in CharacterRoster.Grid)
            if (UiKit.ClickedIn(Card(cell), hit, clicked))
                _selectedId = cell.Character.Id;
    }

    /// <summary>Try to become the selected hunter. Returns true when the active hunter changed.</summary>
    /// <remarks>
    /// Stays defensive about both refusals but no longer writes player text for them: the button is
    /// not drawn at all when the hunter is already active or still locked, so a refusal message here
    /// would be for a press that cannot happen.
    /// </remarks>
    public bool Confirm(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var c = CharacterRoster.Get(_selectedId);
        if (state.ActiveId == c.Id || !state.IsUnlocked(c.Id)) return false;
        state.Select(c.Id);
        _notice = c.Name;
        return true;
    }

    public void Draw(SpriteBatch b, CharacterState state, Point mouse, bool clicked)
    {
        ArgumentNullException.ThrowIfNull(state);
        _anim += 1f / 60f;
        _tip = null;
        var hit = mouse;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));
        _ui.TextCenterBig(b, "ROSTER", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);
        // (The SWITCH FREELY banner is gone. It was the least readable line on the screen, it was drawn
        //  on every visit, and it over-promised — gear the new hunter cannot wear does come off. The
        //  honest version of it now sits in the state block, beside the button it de-risks.)

        DrawGrid(b, state, hit);
        DrawDetail(b, state, hit, clicked);
        if (_tip is { } tip) _ui.HoverTip(b, tip, _tipAt);
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
        var headIcon = UiMetrics.IconSmall;
        var headGap = UiMetrics.Space(6);
        var withIcon = true;
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var w = headIcon + headGap + _ui.MeasureBig($"THE {ItemClasses.PluralOf(CharacterRoster.ClassColumns[column])}", UiTypography.Body);
            if (w > ColumnPitch - UiMetrics.Space(4)) withIcon = false;
        }
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var cls = CharacterRoster.ClassColumns[column];
            var top = Card(column, 0);
            var color = UiKit.ClassColor(cls);
            var label = $"THE {ItemClasses.PluralOf(cls)}";
            var textW = _ui.MeasureBig(label, UiTypography.Body);
            var groupW = (withIcon ? headIcon + headGap : 0) + textW;
            var x = top.Center.X - groupW / 2;
            var y = GridPlate.Y + HeaderY;
            if (withIcon)
            {
                _ui.ClassIcon(b, cls, new Rectangle(x, y + (UiTypography.Body - headIcon) / 2, headIcon, headIcon));
                x += headIcon + headGap;
            }
            _ui.TextBig(b, label, x, y, color, UiTypography.Body);
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
            if (_ui.MeasureBig(ItemClasses.NameOf(cls), UiTypography.Body) > CardWidth - 2 * EdgeLit - 2 * slot)
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
            if (hot)
            {
                _tip = unlocked ? $"{c.Name} — {c.Blurb}" : $"{c.Name} — {UnlockText(c)}";
                _tipAt = hit;
            }

            var t = active || sel ? EdgeLit : Edge;
            // HOVER eases in (UiMotion.Fast) rather than snapping — the same lift a button gets. A locked
            // card and the active one keep their own surfaces: hover is a luminance, not a claim.
            var lift = UiMotion.Ease(UiMotion.KeyOf(card), hot ? 1f : 0f);
            _ui.Fill(b, card, active ? CardActive : !unlocked ? CardLocked : Color.Lerp(CardBg, CardHot, lift));

            // ACTIVE WINS. The edge used to read `sel ? Bone : active ? Gold : …`, so clicking the
            // hunter you are playing took its gold away — the one law the colour carries, lost on the
            // one card it matters most on. Selection adds a second cue instead of replacing the first.
            var edge = active ? Gold : unlocked ? UiKit.ClassColor(c.Class) : Dim;
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, t), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - t, card.Width, t), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Y, t, card.Height), edge);
            _ui.Fill(b, new Rectangle(card.Right - t, card.Y, t, card.Height), edge);
            if (sel)
            {
                var s = new Rectangle(card.X + t, card.Y + t, card.Width - 2 * t, card.Height - 2 * t);
                _ui.Fill(b, new Rectangle(s.X, s.Y, s.Width, SelectRule), Bone);
                _ui.Fill(b, new Rectangle(s.X, s.Bottom - SelectRule, s.Width, SelectRule), Bone);
                _ui.Fill(b, new Rectangle(s.X, s.Y, SelectRule, s.Height), Bone);
                _ui.Fill(b, new Rectangle(s.Right - SelectRule, s.Y, SelectRule, s.Height), Bone);
            }

            // THE CLASS BADGE, at FULL strength whether locked or not. It was drawn at 0.45 alpha on a
            // locked card, which halved the one element that reads at any size — and the padlock says
            // "locked" better than a dimmed colour does.
            var classColor = UiKit.ClassColor(c.Class);
            var badge = new Rectangle(card.X + t, card.Y + t, card.Width - 2 * t, BadgeH);
            _ui.Fill(b, badge, classColor);
            var ink = new Color(0x14, 0x10, 0x1A);
            var glyphY = badge.Y + (BadgeH - glyph) / 2;
            var labelY = badge.Y + (BadgeH - UiTypography.Body) / 2;
            var label = ItemClasses.NameOf(c.Class);
            if (badgeGlyphs)
            {
                _ui.ClassIcon(b, c.Class, new Rectangle(badge.X + CardBreath, glyphY, glyph, glyph), fallback: ink);
                _ui.TextCenterBig(b, label, badge.Center.X, labelY, ink, UiTypography.Body);
                if (!unlocked)
                    _ui.Icon(b, "ui_slot_locked", new Rectangle(badge.Right - CardBreath - glyph, glyphY, glyph, glyph), ink);
            }
            else
            {
                // The group breathes at the tight rate: the badge is already too narrow for a slot, and
                // the lock's box carries its own clear margin around the ink. WANDERER beside a lock
                // needs 163 of the badge's 187 px at 150 %; a full breath at each end would have cost
                // it its last letters, and a shortened word is the one thing this branch exists to avoid.
                var tight = UiMetrics.Space(4);
                var lockW = unlocked ? 0 : tight + glyph;
                label = _ui.ShortenBig(label, badge.Width - 2 * tight - lockW, UiTypography.Body);
                var labelW = _ui.MeasureBig(label, UiTypography.Body);
                var x = badge.Center.X - (labelW + lockW) / 2;
                _ui.TextBig(b, label, x, labelY, ink, UiTypography.Body);
                if (!unlocked)
                    _ui.Icon(b, "ui_slot_locked", new Rectangle(x + labelW + tight, glyphY, glyph, glyph), ink);
            }

            // THE TEXT BLOCK under the portrait: the short name at Headline — on two lines when the card
            // is too narrow for it on one, which only happens at 150 % — and the innate label, which
            // gives way when it would leave the portrait under a third of the card.
            var headlineH = UiTypography.Pitch(UiTypography.Headline);
            var bodyH = UiTypography.Pitch(UiTypography.Body);
            var nameW = card.Width - NameInset * 2;
            var nameLines = _ui.WrapBig(c.ShortName, nameW, UiTypography.Headline);
            var nameRows = Math.Clamp(nameLines.Count, 1, 2);

            // The hunter themselves, taking whatever the stack leaves. Each breathes on its own phase,
            // so ten cards do not pulse in unison.
            var statusTop = card.Bottom - t - StatusH;
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
            if (nameRows == 1)
                _ui.TextCenterBig(b, _ui.ShortenBig(c.ShortName, nameW, UiTypography.Headline),
                                  card.Center.X, ny, nameInk, UiTypography.Headline);
            else
                for (var row = 0; row < nameRows; row++)
                    _ui.TextCenterBig(b, _ui.ShortenBig(nameLines[row], nameW, UiTypography.Headline),
                                      card.Center.X, ny + row * headlineH, nameInk, UiTypography.Headline);
            // The identity label: the hunter's own innate, which is unique to them — unlike the road or
            // the tier line, which the card used to spend two rows on.
            if (showLabel)
                _ui.TextCenterBig(b, _ui.ShortenBig(c.PassiveName, nameW, UiTypography.Body),
                                  card.Center.X, ny + headlineH * nameRows, Slate, UiTypography.Body);

            DrawStatusBand(b, c, new Rectangle(card.X, statusTop, card.Width, StatusH), unlocked, active);
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
            var word = active ? "PLAYING" : "READY";
            var col = active ? Gold : Met;
            var w = _ui.MeasureBig(word, UiTypography.Body) + UiMetrics.Space(44);
            var pill = new Rectangle(band.Center.X - w / 2, band.Y + UiMetrics.Space(26), w,
                                     UiTypography.Body + UiMetrics.Space(12));
            _ui.Fill(b, pill, col * 0.14f);
            _ui.Fill(b, new Rectangle(pill.X, pill.Y, pill.Width, 1), col);
            _ui.Fill(b, new Rectangle(pill.X, pill.Bottom - 1, pill.Width, 1), col);
            _ui.Fill(b, new Rectangle(pill.X, pill.Y, 1, pill.Height), col);
            _ui.Fill(b, new Rectangle(pill.Right - 1, pill.Y, 1, pill.Height), col);
            _ui.TextCenterBig(b, word, pill.Center.X, pill.Y + UiMetrics.Space(6), col, UiTypography.Body);
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
            if (Regions.Find(region) is { } def)
                _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, textW, UiTypography.Secondary),
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

    private int Lines(string text, int width, int cap) => Math.Clamp(_ui.WrapBig(text, width, UiTypography.Body).Count, 1, cap);

    private void DrawDetail(SpriteBatch b, CharacterState state, Point hit, bool clicked)
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
        var skill = c.StartingSkillId is { } sid ? SkillCatalogue.Find(sid) : null;

        // The state block FOLLOWS the content, and the content is cut so that it always can: the block
        // used to be reserved as a flat 232 px, which was one hunter's worth at one profile.
        var showButton = unlocked && !active;
        var limit = (showButton ? ActionRect.Y : UiKit.ContentBottom(Inspector)) - UiMetrics.Space(12);
        var y = Inspector.Y + UiTypography.PanelTitleTop;

        var blurbAll = Lines(c.Blurb, width, 2);
        var skillLines = skill is null ? 1 : Lines(skill.Line, width, 2);
        var passiveAll = Lines(c.PassiveText, width, 3);
        var wearsLines = Lines(ItemClasses.WearsLine(c.Class), width, 2);
        var stateH = StateBlockHeight(c, width, unlocked, active);

        var fixedH = headH + UiMetrics.Space(6)                                                  // 1 CATEGORY
                   + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(2)             // 2 NAME
                   + ruleH + headH + (skill is null ? lineH : SkillIconRow + skillLines * lineH) // 4 STARTING SKILL
                   + ruleH + headH + lineH                                                      // 5 INNATE, its name
                   + stateGap + stateH;                                                         // 7 the state
        var roadH = ruleH + headH + lineH + wearsLines * lineH;
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

        // 1 CATEGORY
        var catIcon = UiMetrics.Control(26);
        _ui.ClassIcon(b, c.Class, new Rectangle(left, y, catIcon, catIcon));
        _ui.TextBig(b, $"HUNTER · {ItemClasses.NameOf(c.Class)}", left + catIcon + UiMetrics.Space(10),
                    y + (catIcon - UiTypography.Secondary) / 2, UiKit.ClassColor(c.Class), UiTypography.Secondary);
        y += headH + UiMetrics.Space(6);

        // 2 NAME
        _ui.TextBig(b, _ui.ShortenBig(c.Name, width, UiTypography.Headline), left, y,
                    active ? Gold : unlocked ? Bone : Slate, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(2);

        // 3 IDENTITY — the flavour, and the first thing to go after ROAD AND GEAR: the card's hover
        //   tip says it too.
        if (blurbLines > 0)
            y = DrawWrapped(b, c.Blurb, left, y, width, Slate, UiTypography.Body, blurbLines) + UiMetrics.Space(10);

        // 4 STARTING SKILL — the fact that most separates two hunters, and the screen never showed it.
        //   It is latched permanently into your skill set the moment you play them.
        Rule();
        _ui.TextBig(b, "STARTING SKILL", left, y, Slate, UiTypography.Secondary);
        if (skill is not null && Mastery?.LearnedSkills().Contains(skill.Id) == true)
            _ui.TextRightBig(b, "ALREADY KNOWN", right, y, Met, UiTypography.Secondary);
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
            y += headH;
            // Colour AND word: the road never has to be read out of a hue alone.
            _ui.TextBig(b, c.Lean is { } br ? $"{BranchName(br)} ROAD" : "NO ROAD — EVERY ROAD FITS",
                        left, y, lean, UiTypography.Body);
            _ui.TextRightBig(b, c.TierLine, right, y + (UiTypography.Body - UiTypography.Secondary) / 2, Slate, UiTypography.Secondary);
            y += lineH;
            // The CLASS's own line, not its description: for THE OATHBOUND the description printed
            // "BUILT FOR THE LOOT ROAD" one row under "NO ROAD — EVERY ROAD FITS", which is the class
            // and the hunter contradicting each other on screen.
            y = DrawWrapped(b, ItemClasses.WearsLine(c.Class), left, y, width, Bone, UiTypography.Body, wearsLines);
        }

        // 7 THE STATE — right after the content, and never so low that it runs under the button: the
        //   cuts above guarantee the second, the clamp is for a hunter whose words outgrow the column.
        DrawStateBlock(b, c, Math.Min(y + stateGap, limit - stateH), left, right, width, unlocked, active);

        // 8 ONE BUTTON, and only when it can be pressed. A disabled PLAYING / LOCKED button is a
        // control that invites a click with no answer; the state block above already said both.
        if (showButton && _ui.Button(b, ActionRect, "SET ACTIVE", hit, clicked, true, ButtonStyle.Primary))
            Confirm(state);
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
        shed == 0 ? "NOTHING COMES OFF. THIS HUNTER CAN WEAR EVERYTHING YOU HAVE ON."
        : shed == 1 ? "1 WORN PIECE GOES BACK TO YOUR BAG. THIS HUNTER CANNOT WEAR IT."
        : $"{shed} WORN PIECES GO BACK TO YOUR BAG. THIS HUNTER CANNOT WEAR THEM.";

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
    private int StateBlockHeight(Character c, int width, bool unlocked, bool active)
    {
        var lineH = UiTypography.Pitch(UiTypography.Body);
        var h = UiMetrics.Space(12) + UiTypography.Pitch(UiTypography.Secondary);
        if (unlocked)
        {
            h += lineH + Lines(FreeLine, width, 2) * lineH;
            h += Lines(active ? PickAnotherLine : ShedLine(ShedCount(c)), width, 2) * lineH;
            return h;
        }
        h += Lines(UnlockText(c), width - LockColumn, 2) * lineH + UiMetrics.Space(6);
        if (Gate(c).Count.Length > 0)
            h += UiMetrics.Control(16) + UiMetrics.Space(10) + UiTypography.Pitch(UiTypography.PrimaryValue);
        return h + Lines(JoinsLine, width, 2) * lineH;
    }

    /// <summary>CURRENT STATE for a hunter you can play, YOU NEED FIRST for one you cannot.</summary>
    private void DrawStateBlock(SpriteBatch b, Character c, int y, int left, int right, int width,
                                bool unlocked, bool active)
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
            y = DrawWrapped(b, FreeLine, left, y, width, Slate, UiTypography.Body, 2);
            if (active)
            {
                DrawWrapped(b, PickAnotherLine, left, y, width, Slate, UiTypography.Body, 2);
                return;
            }

            // WHAT DOES COME OFF. The same test the host runs after the switch — so the one thing the
            // old banner got wrong is stated before the press, not discovered after it.
            DrawWrapped(b, ShedLine(ShedCount(c)), left, y, width, Slate, UiTypography.Body, 2);
            return;
        }

        // LOCKED — the requirement, then how close, then the promise. Nothing here is gold: a gate is
        // not a reward.
        _ui.TextBig(b, "YOU NEED FIRST", left, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        var lockGlyph = UiMetrics.Control(22);
        _ui.Icon(b, "ui_slot_locked", new Rectangle(left, y + (lineH - lockGlyph) / 2, lockGlyph, lockGlyph), Bone);
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
        DrawWrapped(b, JoinsLine, left, y, width, Slate, UiTypography.Body, 2);
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
