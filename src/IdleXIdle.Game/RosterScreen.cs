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
    private const int Top = 120;            // the hint slot owns canvas y 86..134
    private const int PageGutter = 24;
    private const int ColumnGap = 16;
    private const int InspectorWidth = 496; // the house inspector width (D3)
    private const int GridInset = 24, CardGap = 16, RowGap = 20, Columns = 5;

    /// <summary>The reading column. Its aspect picks the VERTICAL frame, whose inset is 28 — the old
    /// near-square panel wore the SQUARE frame and paid 68 px of margin plus a 28 px title drop.</summary>
    private static Rectangle Inspector =>
        new(UiKit.PageRight(PageGutter + InspectorWidth), Top, InspectorWidth, UiKit.PageBottom(40) - Top);

    /// <summary>Everyone, as a grid. A list surface is QUIET — it wore the ornate frame while the
    /// inspector beside it wore the quiet one, so the eye landed on the filigree.</summary>
    private static Rectangle GridPlate =>
        new(PageGutter, Top, Inspector.X - ColumnGap - PageGutter, UiKit.PageBottom(40) - Top);

    private static Rectangle ActionRect =>
        new(UiKit.ContentLeft(Inspector), Inspector.Bottom - 92,
            UiKit.ContentRight(Inspector) - UiKit.ContentLeft(Inspector), 68);

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
    private const int HeaderY = 40;
    private static int RuleY => HeaderY + UiTypography.Pitch(UiTypography.Body);
    private static int CardsY => RuleY + 12;

    // Every card metric is DERIVED from the plate, so the grid grows with the page instead of
    // overhanging it: at 1920 a card is 249x398, at 1536 it is 172x290.
    private static int CardWidth => (GridPlate.Width - GridInset * 2 - CardGap * (Columns - 1)) / Columns;
    private static int ColumnPitch => CardWidth + CardGap;
    private static int CardHeight => (GridPlate.Height - CardsY - GridInset - RowGap) / 2;
    private static int RowPitch => CardHeight + RowGap;
    private static int GridLeft => (GridPlate.Width - (ColumnPitch * (Columns - 1) + CardWidth)) / 2;

    private static Rectangle Card(RosterCell cell) => Card(cell.Column, cell.Row);

    private static Rectangle Card(int column, int row) =>
        new(GridPlate.X + GridLeft + column * ColumnPitch, GridPlate.Y + CardsY + row * RowPitch,
            CardWidth, CardHeight);

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
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var cls = CharacterRoster.ClassColumns[column];
            var top = Card(column, 0);
            var color = UiKit.ClassColor(cls);
            _ui.ClassIcon(b, cls, new Rectangle(top.Center.X - 66, GridPlate.Y + HeaderY - 2, 24, 24));
            _ui.TextCenterBig(b, $"THE {ItemClasses.PluralOf(cls)}", top.Center.X + 16, GridPlate.Y + HeaderY,
                              color, UiTypography.Body);
            _ui.Fill(b, new Rectangle(top.X, GridPlate.Y + RuleY, top.Width, 2), color * 0.6f);
        }

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

            const int BadgeH = 36, Gap = 8, StatusH = 84, PortraitInset = 20;
            var t = active || sel ? 4 : 3;
            _ui.Fill(b, card, active ? CardActive : !unlocked ? CardLocked : hot ? CardHot : CardBg);

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
                _ui.Fill(b, new Rectangle(s.X, s.Y, s.Width, 2), Bone);
                _ui.Fill(b, new Rectangle(s.X, s.Bottom - 2, s.Width, 2), Bone);
                _ui.Fill(b, new Rectangle(s.X, s.Y, 2, s.Height), Bone);
                _ui.Fill(b, new Rectangle(s.Right - 2, s.Y, 2, s.Height), Bone);
            }

            // THE CLASS BADGE, at FULL strength whether locked or not. It was drawn at 0.45 alpha on a
            // locked card, which halved the one element that reads at any size — and the padlock says
            // "locked" better than a dimmed colour does.
            var classColor = UiKit.ClassColor(c.Class);
            var badge = new Rectangle(card.X + t, card.Y + t, card.Width - 2 * t, BadgeH);
            _ui.Fill(b, badge, classColor);
            var ink = new Color(0x14, 0x10, 0x1A);
            _ui.ClassIcon(b, c.Class, new Rectangle(badge.X + 8, badge.Y + 6, 24, 24), fallback: ink);
            _ui.TextCenterBig(b, ItemClasses.NameOf(c.Class), badge.Center.X, badge.Y + 7, ink, UiTypography.Body);
            if (!unlocked)
                _ui.Icon(b, "ui_slot_locked", new Rectangle(badge.Right - 32, badge.Y + 6, 24, 24), ink);

            // The hunter themselves, taking whatever the fixed stack leaves. Each breathes on its own
            // phase, so ten cards do not pulse in unison.
            var statusTop = card.Bottom - t - StatusH;
            var py = badge.Bottom + Gap;
            var ph = statusTop - UiTypography.Pitch(UiTypography.Body) - UiTypography.Pitch(UiTypography.Headline) - Gap - py;
            var portrait = new Rectangle(card.X + PortraitInset, py, card.Width - PortraitInset * 2, ph);
            // A READABLE grey, not the old near-black 0x2A2830, which turned a locked hunter into a
            // silhouette you could not tell from any other locked hunter.
            var tint = unlocked ? Color.White : new Color(0x6E, 0x6A, 0x78);
            if (!_ui.AnimSprite(b, c.StripKey("idle"), portrait, _anim + i * 0.37f, 10f, loop: true, tint, -1f))
                _ui.SpriteGrounded(b, c.SpriteKey, portrait, tint, 0.02f);

            // SHORT NAME at Headline: the full name is what the inspector is for, and THE FALLING TOWER
            // at this rung would ellipsise on a 249 px card.
            var ny = portrait.Bottom + Gap;
            _ui.TextCenterBig(b, _ui.ShortenBig(c.ShortName, card.Width - 16, UiTypography.Headline),
                              card.Center.X, ny, unlocked ? Bone : Slate, UiTypography.Headline);
            // The identity label: the hunter's own innate, which is unique to them — unlike the road or
            // the tier line, which the card used to spend two rows on.
            _ui.TextCenterBig(b, _ui.ShortenBig(c.PassiveName, card.Width - 16, UiTypography.Body),
                              card.Center.X, ny + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Body);

            DrawStatusBand(b, c, new Rectangle(card.X, statusTop, card.Width, StatusH), unlocked, active);
        }
    }

    // ── THE STATUS BAND: what this card is, or how close it is. ──────────────────────────────────
    //
    // A conquest-gated card used to say the single word LOCKED, although the region depth it is
    // waiting on was already inside the QuestProgress the screen is handed every frame. Every gate
    // counts now.
    private void DrawStatusBand(SpriteBatch b, Character c, Rectangle band, bool unlocked, bool active)
    {
        if (unlocked)
        {
            var word = active ? "PLAYING" : "READY";
            var col = active ? Gold : Met;
            var w = _ui.MeasureBig(word, UiTypography.Body) + 44;
            var pill = new Rectangle(band.Center.X - w / 2, band.Y + 26, w, UiTypography.Body + 12);
            _ui.Fill(b, pill, col * 0.14f);
            _ui.Fill(b, new Rectangle(pill.X, pill.Y, pill.Width, 1), col);
            _ui.Fill(b, new Rectangle(pill.X, pill.Bottom - 1, pill.Width, 1), col);
            _ui.Fill(b, new Rectangle(pill.X, pill.Y, 1, pill.Height), col);
            _ui.Fill(b, new Rectangle(pill.Right - 1, pill.Y, 1, pill.Height), col);
            _ui.TextCenterBig(b, word, pill.Center.X, pill.Y + 6, col, UiTypography.Body);
            return;
        }

        if (QuestCatalogue.Find(c.Unlock.QuestId) is { } q)
        {
            var pct = q.Threshold <= 0 ? 0f : Math.Clamp(q.Current(Progress) / (float)q.Threshold, 0f, 1f);
            _ui.Bar(b, band.X + 24, band.Y + 22, band.Width - 48, 12, pct, Met);
            _ui.TextCenterBig(b, _ui.ShortenBig(q.ProgressLine(Progress), band.Width - 16, UiTypography.Body),
                              band.Center.X, band.Y + 42, Bone, UiTypography.Body);
            return;
        }

        if (c.Unlock.Kind == UnlockKind.Conquest && c.Unlock.RegionId is { } region)
        {
            var (now, need) = ConquestProgress(region);
            if (Regions.Find(region) is { } def)
                _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, band.Width - 16, UiTypography.Secondary),
                                  band.Center.X, band.Y + 2, Slate, UiTypography.Secondary);
            _ui.Bar(b, band.X + 24, band.Y + 26, band.Width - 48, 12, need <= 0 ? 0f : now / (float)need, Met);
            _ui.TextCenterBig(b, $"{now} / {need} WAVES", band.Center.X, band.Y + 46, Bone, UiTypography.Body);
            return;
        }

        _ui.TextCenterBig(b, "LOCKED", band.Center.X, band.Y + 30, Slate, UiTypography.Body);
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

        // The state block FOLLOWS the content, but never lower than the band that clears the button.
        // Anchored outright it left a two-hundred-pixel hole under a short hunter — a gap that reads
        // as something having failed to draw; flowed outright it would collide with the button.
        var showButton = unlocked && !active;
        const int StateBand = 232;
        var stateLimit = (showButton ? ActionRect.Y : UiKit.ContentBottom(Inspector)) - StateBand;
        var floor = stateLimit - 12;

        var y = Inspector.Y + UiTypography.PanelTitleTop;

        void Rule()
        {
            if (y + 12 > floor) return;
            _ui.Fill(b, new Rectangle(left, y, width, 1), Dim);
            y += 10;
        }

        // (No WHO THEY ARE panel title: a generic gold headline that outranked the hunter's own name
        //  thirty pixels beneath it. The name IS the title.)

        // 1 CATEGORY
        _ui.ClassIcon(b, c.Class, new Rectangle(left, y, 26, 26));
        _ui.TextBig(b, $"HUNTER · {ItemClasses.NameOf(c.Class)}", left + 36, y + 4,
                    UiKit.ClassColor(c.Class), UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary) + 6;

        // 2 NAME
        _ui.TextBig(b, _ui.ShortenBig(c.Name, width, UiTypography.Headline), left, y,
                    active ? Gold : unlocked ? Bone : Slate, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + 2;

        // The two blocks that carry the decision are reserved BEFORE the flavour is drawn, so a long
        // blurb can never be the reason the starting skill is missing.
        var skill = c.StartingSkillId is { } sid ? SkillCatalogue.Find(sid) : null;
        var skillNeed = 10 + UiTypography.Pitch(UiTypography.Secondary) + 36 + lineH * 2;
        var innateNeed = 10 + UiTypography.Pitch(UiTypography.Secondary) + lineH * 4;

        // 3 IDENTITY
        var blurbLines = floor - (skillNeed + innateNeed) - y >= lineH * 2 ? 2 : 1;
        y = DrawWrapped(b, c.Blurb, left, y, width, Slate, UiTypography.Body, blurbLines) + 10;

        // 4 STARTING SKILL — the fact that most separates two hunters, and the screen never showed it.
        //   It is latched permanently into your skill set the moment you play them.
        Rule();
        _ui.TextBig(b, "STARTING SKILL", left, y, Slate, UiTypography.Secondary);
        if (skill is not null && Mastery?.LearnedSkills().Contains(skill.Id) == true)
            _ui.TextRightBig(b, "ALREADY KNOWN", right, y, Met, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        if (skill is null)
        {
            _ui.TextBig(b, "NONE.", left, y, Slate, UiTypography.Body);
            y += lineH;
        }
        else
        {
            _ui.Icon(b, $"icon_skill_{skill.Id}", new Rectangle(left, y, 32, 32), unlocked ? Gold : Slate);
            _ui.TextBig(b, _ui.ShortenBig(
                            $"{skill.Name} · {skill.Style.ToString().ToUpperInvariant()} · {(skill.TakesABeat ? "ACTIVE" : "PASSIVE")}",
                            width - 42, UiTypography.Body),
                        left + 42, y + 4, Bone, UiTypography.Body);
            y += 36;
            y = DrawWrapped(b, skill.Line, left, y, width, Bone, UiTypography.Body, 2);
        }

        // 5 INNATE — always on. The word is glossed in place rather than assumed.
        Rule();
        _ui.TextBig(b, "INNATE — ALWAYS ON", left, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        _ui.TextBig(b, c.PassiveName, left, y, Bone, UiTypography.Body);
        y += lineH;
        y = DrawWrapped(b, c.PassiveText, left, y, width, Bone, UiTypography.Body, 3);

        // 6 ROAD AND GEAR — dropped first when the page is short.
        if (floor - y >= 10 + UiTypography.Pitch(UiTypography.Secondary) + lineH * 3)
        {
            Rule();
            _ui.TextBig(b, "ROAD AND GEAR", left, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
            // Colour AND word: the road never has to be read out of a hue alone.
            _ui.TextBig(b, c.Lean is { } br ? $"{BranchName(br)} ROAD" : "NO ROAD — EVERY ROAD FITS",
                        left, y, lean, UiTypography.Body);
            _ui.TextRightBig(b, c.TierLine, right, y + 2, Slate, UiTypography.Secondary);
            y += lineH;
            // The CLASS's own line, not its description: for THE OATHBOUND the description printed
            // "BUILT FOR THE LOOT ROAD" one row under "NO ROAD — EVERY ROAD FITS", which is the class
            // and the hunter contradicting each other on screen.
            DrawWrapped(b, ItemClasses.WearsLine(c.Class), left, y, width, Bone, UiTypography.Body, 2);
        }

        DrawStateBlock(b, c, Math.Min(y + 24, stateLimit), left, right, width, unlocked, active);

        // 8 ONE BUTTON, and only when it can be pressed. A disabled PLAYING / LOCKED button is a
        // control that invites a click with no answer; the state block above already said both.
        if (showButton && _ui.Button(b, ActionRect, "SET ACTIVE", hit, clicked, true, ButtonStyle.Primary))
            Confirm(state);
    }

    /// <summary>CURRENT STATE for a hunter you can play, YOU NEED FIRST for one you cannot.</summary>
    private void DrawStateBlock(SpriteBatch b, Character c, int y, int left, int right, int width,
                                bool unlocked, bool active)
    {
        var lineH = UiTypography.Pitch(UiTypography.Body);
        _ui.Fill(b, new Rectangle(left, y, width, 1), Dim);
        y += 12;

        if (unlocked)
        {
            _ui.TextBig(b, "CURRENT STATE", left, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
            _ui.TextBig(b, active ? "YOU ARE PLAYING THIS HUNTER" : "READY TO PLAY", left, y,
                        active ? Gold : Met, UiTypography.Body);
            y += lineH;
            y = DrawWrapped(b, "SWITCHING IS FREE. YOUR SKILLS, TRAITS, GEAR AND THE WARREN STAY.",
                            left, y, width, Slate, UiTypography.Body, 2);
            if (active)
            {
                DrawWrapped(b, "PICK ANOTHER CARD TO READ A DIFFERENT HUNTER.", left, y, width, Slate,
                            UiTypography.Body, 2);
                return;
            }

            // WHAT DOES COME OFF. The same test the host runs after the switch — so the one thing the
            // old banner got wrong is stated before the press, not discovered after it.
            var shed = 0;
            foreach (var slot in Enum.GetValues<GearSlot>())
                if (Hunter?.Worn(slot) is { } w && !Gear.CanWear(c, w)) shed++;
            DrawWrapped(b, shed == 0 ? "NOTHING COMES OFF. THIS HUNTER CAN WEAR EVERYTHING YOU HAVE ON."
                            : shed == 1 ? "1 WORN PIECE GOES BACK TO YOUR BAG. THIS HUNTER CANNOT WEAR IT."
                            : $"{shed} WORN PIECES GO BACK TO YOUR BAG. THIS HUNTER CANNOT WEAR THEM.",
                        left, y, width, Slate, UiTypography.Body, 2);
            return;
        }

        // LOCKED — the requirement, then how close, then the promise. Nothing here is gold: a gate is
        // not a reward.
        _ui.TextBig(b, "YOU NEED FIRST", left, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        _ui.Icon(b, "ui_slot_locked", new Rectangle(left, y + 2, 22, 22), Bone);
        y = DrawWrapped(b, UnlockText(c), left + 32, y, width - 32, Bone, UiTypography.Body, 2) + 6;

        var q = QuestCatalogue.Find(c.Unlock.QuestId);
        string count, unit;
        float pct;
        if (q is not null)
        {
            pct = q.Threshold <= 0 ? 0f : Math.Clamp(q.Current(Progress) / (float)q.Threshold, 0f, 1f);
            count = q.ProgressText(Progress);
            unit = q.Unit.ToUpperInvariant();
        }
        else if (c.Unlock.Kind == UnlockKind.Conquest && c.Unlock.RegionId is { } region)
        {
            var (now, need) = ConquestProgress(region);
            pct = need <= 0 ? 0f : now / (float)need;
            count = $"{now} / {need}";
            unit = Regions.Find(region) is { } def ? $"WAVES IN {def.Name}" : "WAVES";
        }
        else { pct = 0f; count = ""; unit = ""; }

        if (count.Length > 0)
        {
            _ui.Bar(b, left, y, width, 16, pct, Met);
            y += 26;
            _ui.TextBig(b, count, left, y, Bone, UiTypography.PrimaryValue);
            _ui.TextRightBig(b, _ui.ShortenBig(unit, width - 140, UiTypography.Body), right,
                             y + (UiTypography.PrimaryValue - UiTypography.Body) / 2, Slate, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.PrimaryValue);
        }
        DrawWrapped(b, "THIS HUNTER JOINS YOU THE MOMENT IT IS DONE.", left, y, width, Slate,
                    UiTypography.Body, 2);
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
