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
using ResonanceHunter.Core.Prestige;

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

    public WeaveScreen(UiKit ui) => _ui = ui;

    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MasteryTree Mastery { get; set; } = new();
    public MemoryDustTree Tree { get; set; } = new();
    public Hunter? Hunter { get; set; }
    public Character? Character { get; set; }

    /// <summary>Set when the loadout changed, so the host can save. Same shape as the other editors.</summary>
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    /// <summary>DEV: pose a slot and a vow for the capture fixture.</summary>
    public void DevPose(int slot, string? vowId) { _slot = slot; _readingVowId = vowId; }

    // ── Layout ────────────────────────────────────────────────────────────────────────────────────
    // 800, not 718. The left column's height is DATA — one row per skill slot, then the add button,
    // then the keystone sockets — and the fifth weave pushed the third socket 68px through the frame.
    // (At four slots it already cleared the interior by 12; the fifth only made it obvious.) All three
    // grow together so the row of panels still reads as a row.
    private static readonly Rectangle SlotsPanel = new(38, 144, 520, 800);
    private static readonly Rectangle PickPanel = new(578, 144, 640, 800);
    private static readonly Rectangle VowPanel = new(1238, 144, 642, 800);

    private static Rectangle SlotRow(int i) => new(SlotsPanel.X + 74, SlotsPanel.Y + 96 + i * 86, 372, 76);
    private static Rectangle DropX(int i) { var r = SlotRow(i); return new(r.Right - 34, r.Y + 4, 30, 30); }

    /// <summary>Everything below the slot list hangs off the CAPACITY, not off the type's floor.</summary>
    private int SlotsEnd => SlotsPanel.Y + 96 + Loadout.SkillCapacity * 86;

    private Rectangle AddBtn => new(SlotsPanel.X + 74, SlotsEnd, 372, 48);

    // KEYSTONE SOCKETS live here too. They were chips at the bottom of the deleted sidebar, and a
    // keystone is a loadout decision exactly like a Vow is — the trait tree TEACHES them, this screen
    // is where you decide which of them you are carrying. Losing the only UI for them along with the
    // sidebar would have been a silent regression: the tree would keep selling a payoff with nowhere
    // left to equip it.
    /// <summary>How many keystone chips are on screen at once. A WINDOW, not the socket count.</summary>
    private const int KeystoneRows = 3;

    // 94/46/42, measured against the WORST case rather than the current one: at five slots SlotsEnd is
    // 670, so a 102/48/44 row ends at 912 against a panel interior that closes at 904. Eight pixels, and
    // the third chip is drawn on the frame.
    private Rectangle KeystoneChip(int i) => new(SlotsPanel.X + 74, SlotsEnd + 94 + i * 46, 372, 42);

    private static readonly Source[] Sources = Enum.GetValues<Source>();
    private static readonly Form[] Forms = Enum.GetValues<Form>();

    // Every vertical offset here was measured off a capture, not guessed. The first pass put the FORM
    // heading at +356 while the source grid's second row ran to +364, so the heading printed straight
    // through the bottom of the gems.
    private const int PickPad = 74;                       // the panel art's side ornaments eat ~70px
    private static int PickInner => PickPanel.Width - PickPad * 2;
    private const int CellPitch = 164, CellW = 148, CellH = 104;

    private static Rectangle SourceCell(int i) =>
        new(PickPanel.X + PickPad + i % 3 * CellPitch, PickPanel.Y + 130 + i / 3 * 116, CellW, CellH);

    private static Rectangle FormCell(int i) =>
        new(PickPanel.X + PickPad + i % 3 * CellPitch, PickPanel.Y + 446 + i / 3 * 116, CellW, CellH);

    // Five rows, not six. Six fitted only by squeezing each to 54px, where a Vow's name and its
    // verdict printed over one another.
    // Five again. This was cut to four when the panel was 718 tall and a three-line description ran
    // out through the bottom ornament; at 800 both fit, and the reading block is still bounded so the
    // next long Vow ellipsises instead of escaping.
    private const int VowRows = 5;
    private const int VowRowH = 70;
    private static Rectangle VowRow(int i) => new(VowPanel.X + 74, VowPanel.Y + 136 + i * (VowRowH + 8), 494, VowRowH);
    private static Rectangle VowClear => new(VowPanel.X + 74, VowPanel.Y + 136 + VowRows * (VowRowH + 8) + 4, 494, 44);

    private static string FormName(Form f) => f.ToString().ToUpperInvariant();
    private static string SourceName(Source s) => s.ToString().ToUpperInvariant();

    /// <summary>What a Vow demands, in one line the player can check against their own build.</summary>
    private static string DemandText(Vow v) => v.Demand switch
    {
        VowDemand.SingleForm => "EVERY SKILL THE SAME FORM",
        VowDemand.SingleSource => "EVERY SKILL THE SAME SOURCE",
        VowDemand.EveryWeaveFilled => "NO EMPTY SKILL SLOT",
        VowDemand.NoCritInvestment => "NO CRIT INVESTED",
        VowDemand.CadenceAtOrBelow => $"SKILL RATE AT OR BELOW {v.Threshold:0.##}x",
        VowDemand.CadenceAtOrAbove => $"SKILL RATE AT OR ABOVE {v.Threshold:0.##}x",
        VowDemand.NoDefence => "NO DEFENCE AT ALL",
        VowDemand.NoKeystone => "NO KEYSTONE SOCKETED",
        VowDemand.SlotLeftBare => $"{v.Bare.ToString().ToUpperInvariant()} SLOT LEFT EMPTY",
        _ => "NOTHING — IT SIMPLY COSTS",
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

    public void Update(Point mouse, bool clicked, int wheel)
    {
        var hit = Game1.ToOverlay(mouse);
        var skills = Loadout.Skills;
        var known = Known;

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
                Dirty = true;
                _msg = "SLOT UNWOVEN.";
                return;
            }
            if (SlotRow(i).Contains(hit)) { _slot = i; _msg = ""; return; }
        }

        if (skills.Count < Loadout.SkillCapacity && AddBtn.Contains(hit))
        {
            var added = Loadout.AddSkill();
            if (added >= 0) { _slot = added; Dirty = true; _msg = "SLOT WOVEN."; }
            else _msg = "NO MORE SLOTS — THE SPINE SELLS THEM IN TRAITS (P).";
            return;
        }

        var learned = DustEffects.LearnedKeystones(Tree);
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned.Count - KeystoneRows));
        for (var i = 0; i < KeystoneRows && _keystoneScroll + i < learned.Count; i++)
        {
            if (!KeystoneChip(i).Contains(hit)) continue;
            if (Loadout.ToggleKeystone(learned[_keystoneScroll + i].Id, learned)) { Dirty = true; _msg = ""; }
            else _msg = $"ONLY {Loadout.KeystoneCapacity} SOCKET(S) — THE SPINE SELLS MORE.";
            return;
        }

        if (_slot >= skills.Count) return;

        for (var i = 0; i < Sources.Length; i++)
            if (SourceCell(i).Contains(hit)) { Loadout.SetSource(_slot, Sources[i]); Dirty = true; return; }

        for (var i = 0; i < Forms.Length; i++)
            if (FormCell(i).Contains(hit)) { Loadout.SetForm(_slot, Forms[i]); Dirty = true; return; }

        for (var r = 0; r < VowRows; r++)
        {
            var idx = _vowScroll + r;
            if (idx >= known.Count || !VowRow(r).Contains(hit)) continue;
            var v = known[idx];
            _readingVowId = v.Id;
            // Clicking the sworn Vow again breaks it. A Vow is a restriction, and the way out of a
            // restriction should be the same control that put you in it.
            var already = skills[_slot].VowId == v.Id;
            if (Loadout.SetVow(_slot, already ? null : v.Id, known))
            {
                Dirty = true;
                _msg = already ? $"{v.Name.ToUpperInvariant()} BROKEN." : $"{v.Name.ToUpperInvariant()} SWORN.";
            }
            return;
        }

        if (VowClear.Contains(hit) && Loadout.SetVow(_slot, null, known))
        {
            Dirty = true;
            _msg = "NO VOW ON THIS SLOT.";
        }
    }

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        var hit = Game1.ToOverlay(mouse);
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xC8));
        _ui.TextCenterBig(b, "THE WEAVE", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "A SKILL IS A SOURCE, A FORM, AND WHAT YOU SWORE FOR IT",
                          960, 80, Slate, UiTypography.Secondary);

        DrawSlots(b, hit);
        DrawPicker(b, hit);
        DrawVows(b, hit);

        if (_msg.Length > 0) _ui.TextCenter(b, _msg, 960, 1016, Gold);
    }

    private void DrawSlots(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, SlotsPanel);
        _ui.TextCenterBig(b, "YOUR SKILLS", SlotsPanel.Center.X, SlotsPanel.Y + 44, Gold, UiTypography.SectionTitle);

        var skills = Loadout.Skills;
        var ctx = Context;
        for (var i = 0; i < Loadout.SkillCapacity; i++)
        {
            if (i >= skills.Count) break;
            var s = skills[i];
            var row = SlotRow(i);
            var on = i == _slot;
            var col = SourceColor.GetValueOrDefault(s.Source, Bone);

            _ui.Fill(b, row, on ? new Color(0x2C, 0x25, 0x44) : Quiet);
            _ui.Fill(b, new Rectangle(row.X, row.Y, 5, row.Height), col);
            if (on) Outline(b, row, Bone, 2);

            if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } gem)
                b.Draw(gem, new Rectangle(row.X + 16, row.Y + 10, 56, 56), Color.White);

            _ui.TextBig(b, Fit($"{SourceName(s.Source)} {FormName(s.Form)}", 236), row.X + 88, row.Y + 8, Bone, UiTypography.Body);

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

            var vow = Weaving.ById(s.VowId);
            if (vow is null)
                _ui.Text(b, "NO VOW", row.X + 88, row.Y + 42, Slate);
            else
            {
                var live = Weaving.IsActive(vow, ctx);
                _ui.Text(b, Fit(vow.Short.ToUpperInvariant(), 190), row.X + 88, row.Y + 42, live ? Gold : Slate);
                _ui.TextRight(b, live ? "MET" : "UNMET", row.Right - 14, row.Y + 42, live ? Met : Ember);
            }
        }

        if (skills.Count < Loadout.SkillCapacity)
        {
            var r = AddBtn;
            _ui.Fill(b, r, r.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
            _ui.TextCenter(b, "+ WEAVE ANOTHER", r.Center.X, r.Y + 14, r.Contains(hit) ? Gold : Slate);
        }

        // ── KEYSTONE SOCKETS ──
        var learned = DustEffects.LearnedKeystones(Tree);
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned.Count - KeystoneRows));
        var head = new Rectangle(SlotsPanel.X + 74, KeystoneChip(0).Y - 44, 372, 30);
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
            _ui.TextRight(b, worn ? "SOCKETED" : "", chip.Right - 14, chip.Y + 12, Met);
        }
    }

    private void DrawPicker(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, PickPanel);
        var skills = Loadout.Skills;
        if (_slot >= skills.Count)
        {
            _ui.TextCenter(b, "PICK A SLOT ON THE LEFT.", PickPanel.Center.X, PickPanel.Center.Y, Slate);
            return;
        }
        var cur = skills[_slot];

        _ui.TextCenterBig(b, "SOURCE", PickPanel.Center.X, PickPanel.Y + 66, Gold, UiTypography.SectionTitle);
        _ui.TextCenter(b, "WHAT IT IS MADE OF", PickPanel.Center.X, PickPanel.Y + 102, Slate);

        // What the player is ASKING about — whatever the cursor is over, falling back to what they
        // already picked. Hovering is the question "what is this?", and until now the screen had no
        // answer to it at all: six Source icons and six Form icons, named and otherwise silent.
        Source? hoverSource = null;
        Form? hoverForm = null;

        for (var i = 0; i < Sources.Length; i++)
        {
            var cell = SourceCell(i);
            var s = Sources[i];
            if (cell.Contains(hit)) hoverSource = s;
            var on = cur.Source == s;
            var col = SourceColor.GetValueOrDefault(s, Bone);
            _ui.Fill(b, cell, on ? new Color(0x2C, 0x25, 0x44) : Quiet);
            Outline(b, cell, on ? Bone : cell.Contains(hit) ? col : Dim, on ? 3 : 2);
            if (_ui.Assets.Get($"source_{s.ToString().ToLowerInvariant()}") is { } gem)
                b.Draw(gem, new Rectangle(cell.Center.X - 28, cell.Y + 8, 56, 56), Color.White);
            else _ui.Diamond(b, new Rectangle(cell.Center.X - 24, cell.Y + 16, 48, 48), col);
            _ui.TextCenterBig(b, SourceName(s), cell.Center.X, cell.Bottom - 24, on ? Bone : col, UiTypography.Secondary);
        }

        _ui.TextCenterBig(b, "FORM", PickPanel.Center.X, PickPanel.Y + 384, Gold, UiTypography.SectionTitle);
        _ui.TextCenter(b, "HOW IT REACHES", PickPanel.Center.X, PickPanel.Y + 420, Slate);

        for (var i = 0; i < Forms.Length; i++)
        {
            var cell = FormCell(i);
            var f = Forms[i];
            if (cell.Contains(hit)) hoverForm = f;
            var on = cur.Form == f;
            var tint = on ? Bone : Slate;
            _ui.Fill(b, cell, on ? new Color(0x2C, 0x25, 0x44) : Quiet);
            Outline(b, cell, on ? Bone : cell.Contains(hit) ? Gold : Dim, on ? 3 : 2);
            // Tinted by the slot's SOURCE, so the picker previews the pairing rather than showing six
            // grey shapes — the skill you are building is a Source AND a Form, never either alone.
            var glyphTint = on ? SourceColor.GetValueOrDefault(cur.Source, Bone) : Slate;
            if (!_ui.Icon(b, $"icon_form_{f.ToString().ToLowerInvariant()}",
                          new Rectangle(cell.Center.X - 26, cell.Y + 8, 52, 52), glyphTint))
                _ui.Diamond(b, new Rectangle(cell.Center.X - 20, cell.Y + 18, 40, 40), glyphTint);
            _ui.TextCenterBig(b, FormName(f), cell.Center.X, cell.Bottom - 24, tint, UiTypography.Secondary);
        }

        DrawExplainer(b, hoverForm ?? cur.Form, hoverSource ?? cur.Source, hoverForm is null && hoverSource is null);
    }

    /// <summary>
    /// What the Form and Source under the cursor actually DO.
    /// </summary>
    /// <remarks>
    /// The playtest's flattest sentence was "I do not know what the skills do, or what difference the
    /// ones I picked make", and it was a fair description of this screen: it asked for four picks out
    /// of thirty-six combinations and printed only their names. The text is <see cref="BuildGlossary"/>,
    /// in Core, derived from the same constants the fight reads — so a retuned cooldown cannot leave a
    /// lie behind on this panel.
    /// </remarks>
    private void DrawExplainer(SpriteBatch b, Form form, Source source, bool showingSelection)
    {
        var x = PickPanel.X + 34;
        var width = PickPanel.Width - 68;

        // ANCHORED TO THE LAST CELL, not to a measured-once offset. The first version used
        // PickPanel.Y + 626 and drew straight over the second row of Form cells — the panel's contents
        // move when a row is added or a cell is resized, and a magic number does not move with them.
        var top = FormCell(Forms.Length - 1).Bottom + 16;
        var y = top;

        _ui.Fill(b, new Rectangle(x - 10, top - 10, width + 20, PickPanel.Bottom - top - 16), Quiet);

        _ui.TextBig(b, $"{FormName(form)} — {BuildGlossary.FormHeadline(form)}", x, y,
                    showingSelection ? Gold : Bone, UiTypography.Body);
        y += 26;

        foreach (var line in _ui.WrapBig(BuildGlossary.FormRule(form), width, UiTypography.Secondary))
        {
            _ui.TextBig(b, line, x, y, Bone, UiTypography.Secondary);
            y += 20;
        }

        y += 8;
        var col = SourceColor.GetValueOrDefault(source, Bone);
        _ui.TextBig(b, SourceName(source), x, y, col, UiTypography.Secondary);
        _ui.TextBig(b, BuildGlossary.SourceLine(source), x + 92, y, Slate, UiTypography.Secondary);
    }

    private void DrawVows(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, VowPanel);
        _ui.TextCenterBig(b, "VOWS", VowPanel.Center.X, VowPanel.Y + 66, Gold, UiTypography.SectionTitle);

        var known = Known;
        var skills = Loadout.Skills;
        var sworn = _slot < skills.Count ? skills[_slot].VowId : null;
        var ctx = Context;

        if (known.Count == 0)
        {
            _ui.TextCenter(b, "YOU HAVE STUDIED NO VOWS.", VowPanel.Center.X, VowPanel.Y + 140, Slate);
            _ui.TextCenter(b, "THE SPINE TEACHES THEM — TRAITS (P).", VowPanel.Center.X, VowPanel.Y + 176, Dim);
            return;
        }

        _ui.TextCenter(b, "PAID ONLY WHILE YOUR BUILD MEETS IT", VowPanel.Center.X, VowPanel.Y + 104, Slate);

        _vowScroll = Math.Clamp(_vowScroll, 0, Math.Max(0, known.Count - VowRows));
        for (var r = 0; r < VowRows; r++)
        {
            var idx = _vowScroll + r;
            if (idx >= known.Count) break;
            var v = known[idx];
            var row = VowRow(r);
            var on = v.Id == sworn;
            var live = Weaving.IsActive(v, ctx);
            var hover = row.Contains(hit);

            _ui.Fill(b, row, on ? new Color(0x2C, 0x25, 0x44) : hover ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            _ui.Fill(b, new Rectangle(row.X, row.Y, 5, row.Height), live ? Met : Ember);
            if (on) Outline(b, row, Gold, 2);

            // The right-hand 120px belongs to the verdict and the multiplier. The demand is truncated
            // to what is left rather than allowed to run under them — a demand line that reads
            // "EVERY SKILL THE SAME SOURCUNMET" is worse than one that is short.
            const int verdict = 124;
            _ui.TextBig(b, v.Name.ToUpperInvariant(), row.X + 18, row.Y + 8, on ? Gold : Bone, UiTypography.Body);
            _ui.Text(b, Fit(DemandText(v), row.Width - 32 - verdict), row.X + 18, row.Y + 40, live ? Met : Slate);
            _ui.TextRight(b, $"x{Weaving.VowMultiplier(v, WeavingTuning.Default):0.00}",
                          row.Right - 16, row.Y + 8, live ? Gold : Dim);
            _ui.TextRight(b, live ? "MET" : "UNMET", row.Right - 16, row.Y + 40, live ? Met : Ember);
        }

        if (known.Count > VowRows)
            _ui.TextRight(b, $"{_vowScroll + 1}-{Math.Min(known.Count, _vowScroll + VowRows)} / {known.Count}",
                          VowPanel.Right - 74, VowPanel.Y + 104, Slate);

        var clear = VowClear;
        _ui.Fill(b, clear, clear.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
        _ui.TextCenter(b, sworn is null ? "NO VOW SWORN" : "BREAK THE VOW",
                       clear.Center.X, clear.Y + 14, sworn is null ? Dim : Ember);

        // The reading panel: the full text of whichever Vow was last touched, because the row can only
        // carry its demand and a Vow's cost is the half that decides whether to take it.
        var reading = Weaving.ById(_readingVowId) ?? Weaving.ById(sworn) ?? known[_vowScroll];
        var y = VowClear.Bottom + 18;
        _ui.Fill(b, new Rectangle(VowPanel.X + 74, y, 494, 2), Dim);
        y += 16;
        _ui.TextBig(b, reading.Name.ToUpperInvariant(), VowPanel.X + 74, y, Gold, UiTypography.Body);
        y += 32;
        // Bounded, so a longer Vow than any in the catalogue today cannot reintroduce the overflow: the
        // panel's frame art reaches 40px in, and text drawn past that is text on the ornament.
        DrawWrapped(b, reading.Description, VowPanel.X + 74, y, 494, Bone, VowPanel.Bottom - 40);
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
