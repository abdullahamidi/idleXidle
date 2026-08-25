using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Quests;

using ResonanceHunter.Core.Progression;
namespace ResonanceHunter.Client;

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

    private const int Cols = 5;

    private static Rectangle Card(int i) =>
        new(GridPanel.X + 52 + i % Cols * 218, GridPanel.Y + 96 + i / Cols * 300, 200, 280);

    private static string BranchName(Branch b) => b switch
    {
        Branch.Weight => "WEIGHT", Branch.Spread => "SPREAD", Branch.Tempo => "TEMPO", _ => "ENDURE",
    };

    // The same four colours the mastery tree uses. A character's lean has to read as the SAME road the
    // player walks on the tree, or the mapping is a coincidence rather than a design.
    private static Color BranchColor(Branch b) => b switch
    {
        Branch.Weight => new Color(0xD6, 0x48, 0x5C),
        Branch.Spread => new Color(0x48, 0xB8, 0x88),
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

        for (var i = 0; i < CharacterRoster.All.Count; i++)
            if (UiKit.ClickedIn(Card(i), hit, clicked))
            {
                _selectedId = CharacterRoster.All[i].Id;
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
        _ui.TextCenterBig(b, "THE ROSTER", GridPanel.Center.X, GridPanel.Y + 44, Gold, UiTypography.SectionTitle);
        _ui.TextRightBig(b, $"{have} / {CharacterRoster.All.Count}", GridPanel.Right - 56, GridPanel.Y + 30,
                         Slate, UiTypography.Secondary);

        for (var i = 0; i < CharacterRoster.All.Count; i++)
        {
            var c = CharacterRoster.All[i];
            var card = Card(i);
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
                              unlocked ? classColor : classColor * 0.6f, 14);
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
        // +74, not +46. The recorded layout fact for this panel art: the SIDE ornaments eat about
        // seventy pixels, so content at +40 runs underneath them — which is exactly what the passive
        // text did, starting on top of the left flourish.
        var left = DetailPanel.X + 74;
        var width = DetailPanel.Width - 148;

        // DERIVED FROM THE PANEL'S INTERIOR, not a hand-picked +44. UiKit.Panel picks its plate art by
        // ASPECT: the wide grid panel gets a slim top flourish, this near-square one gets a tall hanging
        // finial. The same +44 that clears the first buries this title in the second — "WHO THEY ARE"
        // was drawn in gold ON gold filigree, the least readable text on the screen, while the identical
        // offset on the panel beside it looked perfect. Two panels on one screen disagreeing is the tell.
        _ui.TextCenterBig(b, "WHO THEY ARE", DetailPanel.Center.X, UiKit.PanelInner(DetailPanel).Y + 34,
                          Gold, UiTypography.SectionTitle);

        // A cursor, not twelve literal offsets — the same lesson the trait panel learned when one extra
        // row silently pushed its prerequisite list through the divider below it.
        var y = DetailPanel.Y + 126;

        _ui.TextCenterBig(b, c.Name, DetailPanel.Center.X, y, unlocked ? Bone : Slate, UiTypography.PanelTitle);
        y += 34;
        _ui.TextCenterBig(b, c.Blurb, DetailPanel.Center.X, y, Slate, UiTypography.Secondary);
        y += 34;

        // Lean and aptitude, side by side: the two facts that decide whether this character suits the
        // build the player has already spent thirty points on.
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim);
        y += 14;
        _ui.TextBig(b, "ROAD", left, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, c.Lean is { } br ? BranchName(br) : "NONE — ANY WORKS",
                         DetailPanel.Right - 74, y - 2, lean, UiTypography.Body);
        y += 32;
        _ui.TextBig(b, "BEST AT", left, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, c.Aptitude is { } f
                            ? $"{f.ToString().ToUpperInvariant()}  +{(int)Math.Round((c.AptitudePower - 1f) * 100)}%"
                            : "NONE",
                         DetailPanel.Right - 74, y - 2, c.Aptitude is null ? Dim : Bone, UiTypography.Body);
        y += 32;
        // THE CLASS, on its own line in its own colour with its icon, then its one sentence. Two
        // champions share each class, so the sentence names the road the class was built for rather
        // than the champion — that is the roster's whole answer to "which of these two should I be" —
        // and the tier on the right says which of the two this one is.
        var cls = ItemClasses.Get(c.Class);
        var classColor = UiKit.ClassColor(c.Class);
        _ui.ClassIcon(b, c.Class, new Rectangle(left, y - 4, 30, 30));
        _ui.TextBig(b, cls.Name, left + 40, y - 4, classColor, UiTypography.PanelTitle);
        _ui.TextRightBig(b, c.TierLine, DetailPanel.Right - 74, y + 2, Slate, UiTypography.Secondary);
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
            _ui.TextRightBig(b, q.ProgressLine(Progress), DetailPanel.Right - 74, y,
                             q.IsDone(Progress) ? Met : Gold, UiTypography.Body);
        y += 30;
        DrawWrapped(b, UnlockText(c), left, y, width, unlocked ? Slate : Bone);

        var btn = new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 96, DetailPanel.Width - 80, 68);
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
