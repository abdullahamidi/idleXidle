using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Abilities;
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
/// Built around the one question a roster has to answer before any other: <i>what do I lose by
/// switching?</i> The answer here is nothing — both trees, gear, Gleam and the Warren are shared and
/// none of them resets — and the screen says so in a line at the top rather than leaving the player to
/// discover it by risking a build.
/// </para>
/// <para>
/// A locked character shows everything about itself except the ability to be picked. Hiding a locked
/// character's passive would make the roster a list of question marks, and the roster's job is to be a
/// reason to go conquer something.
/// </para>
/// </remarks>
public sealed class RosterScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);

    private readonly UiKit _ui;
    private string _selectedId = CharacterRoster.StarterId;
    private float _anim;
    private string _msg = "";

    public RosterScreen(UiKit ui) => _ui = ui;

    /// <summary>DEV: pose the detail panel on a given character for the capture fixture.</summary>
    public void DevSelect(string id) => _selectedId = id;

    public bool DevRosterDebug { get; set; }

    // Two panels: the grid of everyone, and the one you are reading.
    private static readonly Rectangle GridPanel = new(38, 144, 1180, 718);
    private static readonly Rectangle DetailPanel = new(1250, 144, 630, 718);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.ChampionCards => new[] { GridPanel },
        TourTarget.ChampionDetail => new[] { DetailPanel },
        TourTarget.BecomeThem => new[] { new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 96, DetailPanel.Width - 80, 68) },
        _ => Array.Empty<Rectangle>(),
    };

    // BY CLASS COLUMN, not catalogue order. Playtest (2026-08-26): "characters of the same class
    // should sit one under the other." The arrangement is CharacterRoster.Grid — one column per class
    // in ClassColumns order, the FIRST of the class on the top row, the SECOND directly beneath — and
    // this screen only walks the cells. A column header names the class above each pair.
    private const int ColumnPitch = 218, RowPitch = 300, CardWidth = 200, CardHeight = 280;
    // The class headers sit on the panel's own caption line, the rule under them, the cards under
    // that — one rhythm rather than three offsets measured off a title that was 22 px too low.
    private const int HeaderY = UiTypography.PanelCaptionTop, RuleY = HeaderY + 22, CardsY = RuleY + 8;

    private static Rectangle Card(RosterCell cell) => Card(cell.Column, cell.Row);

    /// <summary>The card grid is CENTRED in its panel — five fixed-width cards, the slack split evenly.</summary>
    /// <remarks>
    /// A panel whose whole content is one grid centres the grid; the house margin governs its TEXT, and
    /// this panel has none beside the title. Written as +52 it was symmetric only by luck (52 left, 56
    /// right) and would have stopped being so the first time a column was added.
    /// </remarks>
    private static int GridLeft => (GridPanel.Width - (ColumnPitch * 4 + CardWidth)) / 2;

    private static Rectangle Card(int column, int row) =>
        new(GridPanel.X + GridLeft + column * ColumnPitch, GridPanel.Y + CardsY + row * RowPitch, CardWidth, CardHeight);

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
        var hit = Game1.ToOverlay(mouse);

        foreach (var cell in CharacterRoster.Grid)
            if (UiKit.ClickedIn(Card(cell), hit, clicked))
            {
                _selectedId = cell.Character.Id;
                _msg = "";
            }
    }

    /// <summary>Try to become the selected character. Returns true when the active character changed.</summary>
    public bool Confirm(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var c = CharacterRoster.Get(_selectedId);
        if (state.ActiveId == c.Id) { _msg = $"YOU ARE ALREADY {c.Name}."; return false; }
        if (!state.IsUnlocked(c.Id)) { _msg = $"LOCKED — {UnlockText(c)}."; return false; }
        state.Select(c.Id);
        _msg = $"YOU ARE {c.Name}.";
        return true;
    }

    public void Draw(SpriteBatch b, CharacterState state, Point mouse, bool clicked)
    {
        ArgumentNullException.ThrowIfNull(state);
        _anim += 1f / 60f;
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));
        _ui.TextCenterBig(b, "ROSTER", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        // The line that makes the screen safe to use. Everything else on it is a comparison a player
        // will not make until they believe switching cannot cost them anything.
        _ui.TextCenterBig(b, "SWITCH FREELY — YOU KEEP SKILLS, TRAITS, GEAR AND THE WARREN",
                          960, 80, Slate, UiTypography.Secondary);

        DrawGrid(b, state, hit);
        DrawDetail(b, state, hit, clicked);
        if (DevRosterDebug)
            foreach (var r in new[] { GridPanel, DetailPanel })
            {
                _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
                _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            }
    }

    private void DrawGrid(SpriteBatch b, CharacterState state, Point hit)
    {
        _ui.Panel(b, GridPanel);
        var have = CharacterRoster.All.Count(c => state.IsUnlocked(c.Id));
        _ui.TextCenterBig(b, "THE ROSTER", GridPanel.Center.X, UiKit.TitleTop(GridPanel), Gold, UiTypography.PanelTitle);
        // Right-aligned on the title's own row: the title's top plus half the difference in their heights,
        // so the two sit on one optical line rather than on two guessed ones.
        _ui.TextRightBig(b, $"{have} / {CharacterRoster.All.Count}", UiKit.ContentRight(GridPanel),
                         UiKit.TitleTop(GridPanel) + (UiTypography.PanelTitle - UiTypography.Secondary) / 2,
                         Slate, UiTypography.Secondary);

        // THE COLUMN HEADERS: the class, plural, in the class's colour, over a rule the width of the
        // column — so the two cards beneath read as one class's pair before either card is read.
        for (var column = 0; column < CharacterRoster.ClassColumns.Count; column++)
        {
            var cls = CharacterRoster.ClassColumns[column];
            var top = Card(column, 0);
            var color = UiKit.ClassColor(cls);
            _ui.TextCenterBig(b, $"THE {ItemClasses.PluralOf(cls)}", top.Center.X, GridPanel.Y + HeaderY,
                              color, UiTypography.Secondary);
            _ui.Fill(b, new Rectangle(top.X, GridPanel.Y + RuleY, top.Width, 2), color * 0.6f);
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
            var lean = LeanColor(c);

            _ui.Fill(b, card, active ? new Color(0x2A, 0x22, 0x10, 0xF0) : new Color(0x16, 0x12, 0x20, 0xE0));
            var edge = sel ? Bone : active ? Gold : unlocked ? lean : Dim;
            var t = sel || active ? 4 : 3;
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, t), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - t, card.Width, t), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Y, t, card.Height), edge);
            _ui.Fill(b, new Rectangle(card.Right - t, card.Y, t, card.Height), edge);

            // THE CLASS BADGE. Playtest (2026-08-26): "I cannot see the characters' classes." The class
            // was one line of small text among four; now it is a strip across the top of the card in
            // the class's own colour, with the class icon and the class word in capitals — the first
            // thing on the card, and the same colour the bag paints that class's gear.
            var classColor = UiKit.ClassColor(c.Class);
            var badge = new Rectangle(card.X + t, card.Y + t, card.Width - 2 * t, 28);
            _ui.Fill(b, badge, unlocked ? classColor : classColor * 0.45f);
            var ink = new Color(0x14, 0x10, 0x1A);
            var badgeIcon = new Rectangle(badge.X + 8, badge.Y + 3, 22, 22);
            _ui.ClassIcon(b, c.Class, badgeIcon, fallback: ink);
            _ui.TextCenterBig(b, ItemClasses.NameOf(c.Class), badge.Center.X + 10, badge.Y + 5,
                              unlocked ? ink : Bone * 0.8f, UiTypography.Secondary);

            // The character themselves. A roster of names is a menu; a roster of people is a roster.
            // Each breathes on its own phase, so ten cards do not pulse in unison — and the sprite is
            // the approved full-body design rather than a generated clip, for the reason set out in
            // SoloExpeditionScreen.DrawChampion.
            // 118 tall: the card took on a fourth line of text (the tier) and a badge strip, and the
            // portrait is the only thing on it with height to spare.
            var portrait = new Rectangle(card.X + 16, badge.Bottom + 8, card.Width - 32, 118);
            var tint = unlocked ? Color.White : new Color(0x2A, 0x28, 0x30);
            // Each on its own phase, so ten cards do not breathe in unison.
            if (!_ui.AnimSprite(b, c.StripKey("idle"), portrait, _anim + i * 0.37f, 10f, loop: true, tint, -1f))
                _ui.SpriteGrounded(b, c.SpriteKey, portrait, tint, 0.02f);

            _ui.TextCenterBig(b, c.Name, card.Center.X, card.Bottom - 116,
                              unlocked ? Bone : Slate, UiTypography.Secondary);
            // FIRST OF THE WARDENS / SECOND OF THE WARDENS — which of the class's two this is, and so
            // whether it is the one a conquest hands over or the one a quest makes you earn.
            _ui.TextCenterBig(b, c.TierLine, card.Center.X, card.Bottom - 90,
                              unlocked ? classColor : classColor * 0.6f, UiTypography.Caption);
            if (c.Lean is { } br)
                _ui.TextCenter(b, BranchName(br), card.Center.X, card.Bottom - 62, unlocked ? lean : Dim);
            else
                // SLATE, NOT DIM: on the card plate this measured ~1.65:1, so the one word explaining
                // why a champion has no lean was effectively invisible on exactly the cards that need
                // explaining.
                _ui.TextCenter(b, "NO ROAD", card.Center.X, card.Bottom - 62, Slate);

            // `state` is the CharacterState parameter; this is the word on the card. A quest-gated
            // card counts instead — "12 / 30 CHESTS" — so the player can see how far off it is.
            var stateText = active ? "PLAYING" : unlocked ? "READY" : "LOCKED";
            if (!unlocked && QuestCatalogue.Find(c.Unlock.QuestId) is { } cq)
                stateText = cq.ProgressLine(Progress);
            _ui.TextCenter(b, stateText, card.Center.X, card.Bottom - 30,
                           active ? Gold : unlocked ? Met : Slate);
        }
    }

    private void DrawDetail(SpriteBatch b, CharacterState state, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, DetailPanel);
        var c = CharacterRoster.Get(_selectedId);
        var unlocked = state.IsUnlocked(c.Id);
        var active = state.ActiveId == c.Id;
        var lean = LeanColor(c);
        // The panel's own margin. This column wears the SQUARE frame, whose side diamonds reach 64 px
        // in — the fact that used to live here as a hand-measured +74 now lives in UiKit.FrameDrop, so
        // every square panel in the game gets it rather than only this one.
        var left = UiKit.ContentLeft(DetailPanel);
        var width = DetailPanel.Width - UiKit.PadX(DetailPanel) * 2;

        // FRAME-AWARE, not hand-picked. UiKit.Panel picks its plate art by ASPECT: the wide grid panel
        // gets a slim top flourish, this near-square one gets a tall hanging finial. The same offset that
        // clears the first buries the title in the second — "WHO THEY ARE" was once drawn in gold ON
        // gold filigree, the least readable text on the screen, while the identical offset on the panel
        // beside it looked perfect. Two panels on one screen disagreeing is the tell.
        _ui.TextCenterBig(b, "WHO THEY ARE", DetailPanel.Center.X, UiKit.TitleTop(DetailPanel),
                          Gold, UiTypography.PanelTitle);

        // A cursor, not twelve literal offsets — the same lesson the trait panel learned when one extra
        // row silently pushed its prerequisite list through the divider below it.
        var y = UiKit.BodyTop(DetailPanel);

        _ui.TextCenterBig(b, c.Name, DetailPanel.Center.X, y, unlocked ? Bone : Slate, UiTypography.Headline);
        y += 34;
        _ui.TextCenterBig(b, c.Blurb, DetailPanel.Center.X, y, Slate, UiTypography.Secondary);
        y += 34;

        // Lean and aptitude, side by side: the two facts that decide whether this character suits the
        // build the player has already spent thirty points on.
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim);
        y += 14;
        _ui.TextBig(b, "ROAD", left, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, c.Lean is { } br ? BranchName(br) : "NONE — ANY WORKS",
                         UiKit.ContentRight(DetailPanel), y - 2, lean, UiTypography.Body);
        y += 32;
        // (The BEST AT row died with the aptitude, 2026-08-31: a generic per-style damage tax no
        // card advertised — and for THE OATHBOUND, a lie: its MARK +30% multiplied nothing at all.)
        // THE CLASS, on its own line in its own colour with its icon, then its one sentence. Two
        // champions share each class, so the sentence names the road the class was built for rather
        // than the champion — that is the roster's whole answer to "which of these two should I be" —
        // and the tier on the right says which of the two this one is.
        var cls = ItemClasses.Get(c.Class);
        var classColor = UiKit.ClassColor(c.Class);
        _ui.ClassIcon(b, c.Class, new Rectangle(left, y - 4, 30, 30));
        _ui.TextBig(b, cls.Name, left + 40, y - 4, classColor, UiTypography.Headline);
        _ui.TextRightBig(b, c.TierLine, UiKit.ContentRight(DetailPanel), y + 2, Slate, UiTypography.Secondary);
        y += 34;
        y = DrawWrapped(b, $"{cls.Description} Wears {cls.Name} gear.", left, y, width, Slate) + 34;

        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim);
        y += 14;
        _ui.TextBig(b, "ALWAYS ON", left, y, Slate, UiTypography.Secondary);
        y += 30;
        _ui.TextBig(b, c.PassiveName, left, y, Gold, UiTypography.Body);
        y += 32;
        y = DrawWrapped(b, c.PassiveText, left, y, width, Bone) + 40;

        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim);
        y += 14;
        _ui.TextBig(b, unlocked ? "EARNED" : "LOCKED", left, y, unlocked ? Met : Ember, UiTypography.Secondary);
        // HOW CLOSE, not just what. A gate that names a demand and nothing else is a gate a player
        // cannot tell they are one descent away from.
        if (!unlocked && QuestCatalogue.Find(c.Unlock.QuestId) is { } q)
            _ui.TextRightBig(b, q.ProgressLine(Progress), UiKit.ContentRight(DetailPanel), y,
                             q.IsDone(Progress) ? Met : Gold, UiTypography.Body);
        y += 30;
        DrawWrapped(b, UnlockText(c), left, y, width, unlocked ? Slate : Bone);

        var btn = new Rectangle(left, DetailPanel.Bottom - 96, width, 68);
        var label = active ? "PLAYING" : unlocked ? "BECOME THEM" : "LOCKED";
        if (_ui.Button(b, btn, label, hit, clicked, enabled: unlocked && !active))
            Confirm(state);

        if (_msg.Length > 0)
            _ui.TextCenter(b, _msg, DetailPanel.Center.X, DetailPanel.Bottom - 128, Gold);
    }

    /// <summary>Word-wrap into a width. Returns the y of the last line so a cursor can carry on.</summary>
    private int DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        var line = "";
        foreach (var w in text.ToUpperInvariant().Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0) { _ui.Text(b, line, x, y, c); y += 28; line = w; }
            else line = probe;
        }
        if (line.Length > 0) _ui.Text(b, line, x, y, c);
        return y;
    }
}
