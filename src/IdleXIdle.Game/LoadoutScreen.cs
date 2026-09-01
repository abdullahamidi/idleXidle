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
    private int _vowScroll;
    private bool _vowListOpen;

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
        if (chainAt > 0f)
        {
            _bindFlash[slot] = BindFlashSeconds * Math.Clamp(chainAt, 0f, 1f);
            _devHoldChain = true;
        }
    }

    /// <summary>DEV: kept for the fixtures that call it — the skill tree is always on screen now.</summary>
    public void DevOpenSkillTree() { }

    // ── LAYOUT. Three columns to the page, so UI SCALE can shrink the page under them. ─────────────────
    private const int Top = 150;          // under the hint slot's band (y 86–134 stays free of controls)
    private const int BottomMargin = 60;
    private static Rectangle LoadoutPanel => new(38, Top, 520, UiKit.PageBottom(BottomMargin) - Top);
    private static Rectangle InspectorPanel => new(UiKit.PageRight(40) - 496, Top, 496, UiKit.PageBottom(BottomMargin) - Top);
    private static Rectangle SkillsPanel => new(LoadoutPanel.Right + 20, Top, InspectorPanel.X - 20 - (LoadoutPanel.Right + 20), UiKit.PageBottom(BottomMargin) - Top);

    private static int LoadX => UiKit.ContentLeft(LoadoutPanel);
    private static int LoadW => UiKit.ContentRight(LoadoutPanel) - LoadX;
    private const int SlotH = 90, SlotPitch = 98;
    private const int ChipH = 44, ChipPitch = 48;
    private int KeystoneRows => Loadout.SkillCapacity >= 5 ? 2 : 3;

    /// <summary>The rows this frame: slot index (or -1 for an empty), and where it is drawn.</summary>
    private readonly List<(int Slot, Rectangle Rect, bool Passive)> _rows = new();
    private int _rowsEnd;

    /// <summary>Lay the slot rows out: actives first, then passives, each group to its capacity.</summary>
    private void LayoutRows()
    {
        _rows.Clear();
        var skills = Loadout.Skills;
        var cap = Math.Max(1, Loadout.SkillCapacity);
        var kinds = BuildComposer.SlotKinds(
            skills.Select(k => new BuildComposer.SkillPick(k.Source, k.VowId, k.Passive, k.SkillId)).ToList(), cap);
        var actives = new List<int>();
        var passives = new List<int>();
        for (var i = 0; i < skills.Count; i++) (i < kinds.Count && kinds[i] ? passives : actives).Add(i);
        var activeCap = Math.Max(Build.ActiveSlotsFor(cap), actives.Count);
        var passiveCap = Math.Max(Build.PassiveSlotsFor(cap), passives.Count);
        // Empties beyond the equipped count are drawn in the group that still has room.
        var empties = Math.Max(0, cap - skills.Count);
        var y = LoadoutPanel.Y + UiTypography.PanelBodyTopBare;
        void Group(List<int> members, int groupCap, bool passive)
        {
            y += UiTypography.Pitch(UiTypography.Secondary) + 4;   // the caption's line
            for (var k = 0; k < groupCap; k++)
            {
                var slot = k < members.Count ? members[k] : -1;
                if (slot < 0) { if (empties <= 0) continue; empties--; }
                _rows.Add((slot, new Rectangle(LoadX, y, LoadW, SlotH), passive));
                y += SlotPitch;
            }
            y += 10;
        }
        Group(actives, activeCap, false);
        if (passiveCap > 0) Group(passives, passiveCap, true);
        _rowsEnd = y;
    }

    private Rectangle KeystoneHead => new(LoadX, _rowsEnd + 6, LoadW, 30);
    private Rectangle KeystoneChip(int i) => new(LoadX, _rowsEnd + 6 + UiTypography.Pitch(UiTypography.Secondary) + 6 + i * ChipPitch, LoadW, ChipH);
    private Rectangle BenchBlock => new(LoadX, LoadoutPanel.Bottom - UiKit.PanelCorner - 96, LoadW, 86);

    private static int SkillsX => SkillsPanel.X + 24;
    private static int SkillsW => SkillsPanel.Width - 48;
    private const int LibRowPitch = 62, LibTileH = 52, LibStyleW = 118;
    private static int LibTop => SkillsPanel.Y + 54;
    private static Rectangle LibTile(int style, int which)
    {
        var w = (SkillsW - LibStyleW - 12) / 2;
        return new(SkillsX + LibStyleW + which * (w + 12), LibTop + style * LibRowPitch, w, LibTileH);
    }
    private static int TreeTop => LibTop + 6 * LibRowPitch + 22;
    private static Rectangle VarCard(int which)
    {
        var w = (SkillsW - 16) / 2;
        return new(SkillsX + which * (w + 16), TreeTop + 74, w, 96);
    }
    private static Rectangle ReinfChip(int which, int ri)
    {
        var card = VarCard(which);
        return new(card.X, card.Bottom + 10 + ri * 40, card.Width, 34);
    }

    private static int InsX => UiKit.ContentLeft(InspectorPanel);
    private static int InsW => UiKit.ContentRight(InspectorPanel) - InsX;
    private static Rectangle PrimaryBtn => new(InsX, InspectorPanel.Bottom - 92, InsW, 56);
    private static Rectangle RespecText => new(InsX, PrimaryBtn.Y - 36, InsW / 2 - 8, 28);
    private static Rectangle CopyText => new(InsX + InsW / 2 + 8, PrimaryBtn.Y - 36, InsW / 2 - 8, 28);
    private static Rectangle ChangeVowBtn(int y) => new(InsX, y, InsW, 40);
    private static Rectangle VowListRow(int i, int top) => new(InsX, top + i * 50, InsW, 46);
    private const int VowListRows = 7;

    /// <summary>The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates.</summary>
    internal Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.SkillSlots => new[] { new Rectangle(LoadoutPanel.X, LoadoutPanel.Y, LoadoutPanel.Width, Math.Max(200, _rowsEnd - LoadoutPanel.Y)) },
        TourTarget.SkillPicker => new[] { SkillsPanel },
        TourTarget.Vows => new[] { InspectorPanel, new Rectangle(LoadoutPanel.X, KeystoneHead.Y - 10, LoadoutPanel.Width, KeystoneChip(KeystoneRows - 1).Bottom + 16 - KeystoneHead.Y + 10) },
        _ => Array.Empty<Rectangle>(),
    };

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
        LayoutRows();

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

        if (wheel != 0 && LoadoutPanel.Contains(hit))
            _keystoneScroll = Math.Clamp(_keystoneScroll - wheel, 0, Math.Max(0, DustEffects.LearnedKeystones(Tree).Count - KeystoneRows));
        if (wheel != 0 && _vowListOpen && InspectorPanel.Contains(hit))
            _vowScroll = Math.Clamp(_vowScroll - wheel, 0, Math.Max(0, known.Count + 1 - VowListRows));

        if (!clicked) return;

        // ── THE LOADOUT COLUMN: rows select (and arm a reorder); an empty row adds a slot. ──────────
        foreach (var (slot, rect, _) in _rows)
        {
            if (!rect.Contains(hit)) continue;
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
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned.Count - KeystoneRows));
        for (var i = 0; i < KeystoneRows && _keystoneScroll + i < learned.Count; i++)
        {
            if (!KeystoneChip(i).Contains(hit)) continue;
            _pick = Pick.Keystone; _pickKeystoneId = learned[_keystoneScroll + i].Id; _vowListOpen = false; _msg = "";
            return;
        }

        // ── THE SKILLS COLUMN: tiles, the fork, the chips — all select. ─────────────────────────────
        for (var st = 0; st < 6; st++)
            for (var w = 0; w < 2; w++)
            {
                if (!LibTile(st, w).Contains(hit)) continue;
                var def = w == 0 ? SkillCatalogue.ActiveOf((Style)st) : SkillCatalogue.PassiveOf((Style)st);
                _pick = Pick.Library; _pickSkillId = def.Id; _vowListOpen = false; _msg = "";
                return;
            }
        if (SlotDef(_slot) is { } cur && KnownSkills().Contains(cur.Id))
        {
            var chosen = SkillLevels.VariationOf(cur);
            for (var vi = 0; vi < cur.Variations.Count && vi < 2; vi++)
            {
                if (VarCard(vi).Contains(hit)) { _pick = Pick.Variation; _pickIndex = vi; _vowListOpen = false; _msg = ""; return; }
                var v = cur.Variations[vi];
                for (var ri = 0; ri < v.Reinforcements.Count && ri < 3; ri++)
                    if (ReinfChip(vi, ri).Contains(hit))
                    {
                        if (chosen?.Name != v.Name) { _pick = Pick.Variation; _pickIndex = vi; _msg = $"CHOOSE {v.Name} FIRST — ITS REINFORCEMENTS COME AFTER."; }
                        else { _pick = Pick.Reinforcement; _pickIndex = ri; _msg = ""; }
                        _vowListOpen = false;
                        return;
                    }
            }
        }

        // ── THE INSPECTOR: the primary button, the text actions, the vow list. ──────────────────────
        if (_vowListOpen)
        {
            var top = _vowListTop;
            for (var r = 0; r < VowListRows; r++)
            {
                var idx = _vowScroll + r - 1;   // row 0 is NO VOW
                if (idx >= known.Count) break;
                if (!VowListRow(r, top).Contains(hit)) continue;
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
            if (ChangeVowBtn(_changeVowY).Contains(hit)) { _vowListOpen = false; return; }
            if (InspectorPanel.Contains(hit)) return;
        }
        else if (_changeVowY > 0 && ChangeVowBtn(_changeVowY).Contains(hit) && _pick == Pick.Slot && SlotFilled(_slot))
        {
            _vowListOpen = true; _vowScroll = 0;
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
        foreach (var (slot, r, _) in _rows)
            if (slot >= 0 && new Rectangle(r.X - 8, r.Y - 4, r.Width + 16, r.Height + 8).Contains(p)) return slot;
        return -1;
    }

    // ── DRAW ────────────────────────────────────────────────────────────────────────────────────────────
    private string? _tip;
    private Point _tipAt;
    private int _changeVowY;
    private int _vowListTop;
    private bool _respecShown;

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        var hit = mouse;
        _tip = null;
        LayoutRows();
        TickEffects();

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC8));
        _ui.TextCenterBig(b, "BUILD", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        DrawLoadout(b, hit);
        DrawSkills(b, hit);
        DrawInspector(b, hit);

        if (_msg.Length > 0) _ui.TextCenterBig(b, _msg, UiKit.PageCenterX, UiKit.PageBottom(BottomMargin) + 16, Gold, UiTypography.Body);
        if (_copyToastFrames > 0)
        {
            _copyToastFrames--;
            _ui.TextCenterBig(b, _copyToast, InspectorPanel.Center.X, InspectorPanel.Bottom + 14,
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
        var captionDrawn = new bool[2];
        foreach (var (slot, row, passive) in _rows)
        {
            var g = passive ? 1 : 0;
            if (!captionDrawn[g])
            {
                _ui.TextBig(b, passive ? "PASSIVE — ALWAYS ON" : "ACTIVE — TAKES A TURN", row.X, row.Y - UiTypography.Pitch(UiTypography.Secondary) - 2, Slate, UiTypography.Secondary);
                captionDrawn[g] = true;
            }
            if (slot < 0) { DrawEmptyRow(b, row, hit); continue; }

            var s = skills[slot];
            var def = SkillCatalogue.Find(s.SkillId);
            var filled = def is not null && known.Contains(def.Id);
            var on = slot == _slot && _pick != Pick.Keystone;
            var over = row.Contains(hit) && _carrying == Carry.None;
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
            var spine = new Rectangle(row.X, row.Y, 22, row.Height);
            _ui.Fill(b, spine, col * (on ? 0.55f : 0.34f));
            _ui.TextCenterBig(b, $"{slot + 1}", spine.Center.X, row.Y + row.Height / 2 - 12, on ? Bone : Bone * 0.75f, UiTypography.Body);

            var gbox = new Rectangle(row.X + 32, row.Y + 17, 56, 56);
            _ui.Fill(b, gbox, new Color(0x0C, 0x09, 0x14) * 0.55f);
            if (!filled || def is null)
            {
                _ui.Icon(b, "ui_slot_locked", gbox, Slate);
                _ui.TextBig(b, "EMPTY SLOT", row.X + 100, row.Y + 12, UiInk.Empty, UiTypography.Headline);
                _ui.TextBig(b, "PICK A SKILL FROM THE LIBRARY", row.X + 100, row.Y + 12 + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Secondary);
                continue;
            }
            _ui.Icon(b, $"icon_skill_{def.Id}", gbox, col);

            var tx = row.X + 100;
            var right = row.Right - 12;
            // Line 1: NAME, and the vow pill at the right.
            var vow = Vows.ById(s.VowId);
            var pillW = 0;
            if (vow is not null)
            {
                var live = Vows.IsActive(vow, ctx);
                var pill = $"{vow.Short.ToUpperInvariant()} {(live ? "OK" : "BROKEN")}";
                pillW = _ui.MeasureBig(pill, UiTypography.Caption) + UiTypography.ChipPadX * 2;
                var pr = new Rectangle(right - pillW, row.Y + 12, pillW, UiTypography.Caption + UiTypography.ChipPadY * 2);
                _ui.Fill(b, pr, (live ? Gold : Ember) * 0.16f);
                Outline(b, pr, live ? Gold : Ember, 1);
                _ui.TextBig(b, pill, pr.X + UiTypography.ChipPadX, pr.Y + UiTypography.ChipPadY, live ? Gold : Ember, UiTypography.Caption);
                Tip(pr, hit, live ? $"{vow.Name.ToUpperInvariant()} holds — x{Vows.Multiplier(vow):0.00}." : $"{vow.Name.ToUpperInvariant()} is broken: {DemandText(vow)} — it pays nothing until it holds.");
            }
            _ui.TextBig(b, _ui.ShortenBig(def.Name, right - pillW - 12 - tx, UiTypography.Headline), tx, row.Y + 10, on ? Gold : Bone, UiTypography.Headline);
            // Line 2: STYLE · LEVEL · the style factor.
            var level = SkillLevels.LevelOf(def.Id);
            var line2 = $"{StyleName(def.Style)} · LV {level}";
            if (ChosenStyle is { } dd)
            {
                var f = StyleAffinity.Factor(dd, def.Style, vowSworn: s.VowId is not null);
                line2 += f >= 1.99f ? " · x2.0 YOURS" : $" · x{f:0.0#}";
            }
            _ui.TextBig(b, line2, tx, row.Y + 10 + UiTypography.Pitch(UiTypography.Headline) - 4, Slate, UiTypography.Secondary);
            // Line 3: the variation, its Source (gem + word), and how much of it is bought — or the level to spend.
            var chosen = SkillLevels.VariationOf(def);
            var free = SkillLevels.FreeOn(def.Id);
            var y3 = row.Y + row.Height - 8 - UiTypography.Body;
            if (chosen is null)
            {
                _ui.TextBig(b, free > 0 ? $"+{free} LEVEL TO SPEND — CHOOSE A VARIATION" : "NO VARIATION YET", tx, y3, free > 0 ? Gold : Slate, UiTypography.Body);
            }
            else
            {
                var vc = SourceColor.GetValueOrDefault(chosen.Source, Bone);
                if (_ui.Assets.Get($"source_{chosen.Source.ToString().ToLowerInvariant()}") is { } gem)
                    b.Draw(gem, new Rectangle(tx, y3 + 1, 22, 22), Color.White);
                var bought = chosen.Reinforcements.Count(r => SkillLevels.HasReinforcement(def.Id, r.Name));
                var x3 = tx + 28;
                _ui.TextBig(b, chosen.Name.ToUpperInvariant(), x3, y3, Bone, UiTypography.Body); x3 += _ui.MeasureBig(chosen.Name.ToUpperInvariant(), UiTypography.Body);
                _ui.TextBig(b, " · ", x3, y3, Slate, UiTypography.Body); x3 += _ui.MeasureBig(" · ", UiTypography.Body);
                _ui.TextBig(b, SourceName(chosen.Source), x3, y3, vc, UiTypography.Body); x3 += _ui.MeasureBig(SourceName(chosen.Source), UiTypography.Body);
                _ui.TextBig(b, $" · {bought}/{chosen.Reinforcements.Count}{(free > 0 ? $"  +{free}" : "")}", x3, y3, free > 0 ? Gold : Slate, UiTypography.Body);
            }
            Tip(row, hit, $"{def.Name} — {def.Line}");
        }

        // ── KEYSTONES: chips; click selects, the inspector sockets. ──────────────────────────────────
        var learned = DustEffects.LearnedKeystones(Tree);
        _keystoneScroll = Math.Clamp(_keystoneScroll, 0, Math.Max(0, learned.Count - KeystoneRows));
        var head = KeystoneHead;
        if (head.Bottom < BenchBlock.Y - 30)
        {
            _ui.TextBig(b, "KEYSTONES", head.X, head.Y, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, learned.Count == 0 ? "LEARN THEM ON THE TRAITS SCREEN"
                                : $"{Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity} SOCKETS" + (learned.Count > KeystoneRows ? $"  ·  {_keystoneScroll + 1}-{Math.Min(learned.Count, _keystoneScroll + KeystoneRows)} OF {learned.Count}" : ""),
                             head.Right, head.Y, Slate, UiTypography.Secondary);
            for (var i = 0; i < KeystoneRows; i++)
            {
                var chip = KeystoneChip(i);
                if (chip.Bottom > BenchBlock.Y - 8) break;
                var idx = _keystoneScroll + i;
                if (idx >= learned.Count)
                {
                    _ui.Plate(b, chip);
                    _ui.TextBig(b, learned.Count == 0 && i == 0 ? "NO KEYSTONES LEARNED YET" : "EMPTY SOCKET", chip.X + 16, chip.Y + 10, UiInk.Empty, UiTypography.Body);
                    continue;
                }
                var k = learned[idx];
                var worn = Loadout.HasKeystone(k.Id);
                var on = _pick == Pick.Keystone && _pickKeystoneId == k.Id;
                var over = chip.Contains(hit);
                _ui.Fill(b, chip, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
                if (worn) _ui.Fill(b, new Rectangle(chip.X, chip.Y, 5, chip.Height), Gold);
                if (on) Outline(b, chip, Gold, 2);
                _ui.TextBig(b, k.Name.ToUpperInvariant(), chip.X + 16, chip.Y + 10, worn ? Gold : Bone, UiTypography.Body);
                if (worn) _ui.TextRightBig(b, "IN USE", chip.Right - 14, chip.Y + 12, Met, UiTypography.Secondary);
                Tip(chip, hit, k.Blurb);
            }
        }

        // ── THE BENCH: what the build does against the reference dummy, and what the pick would do. ──
        if (Hunter is { } hunter)
        {
            var bench = BenchBlock;
            _ui.Fill(b, new Rectangle(bench.X, bench.Y - 10, bench.Width, 1), Dim);
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
                _ui.TextRightBig(b, MathF.Abs(pct) < 0.5f ? "NO CHANGE" : $"{(pct > 0 ? "+" : "")}{pct:0}% WITH {pd.Name}", bench.Right, vy + 8, tint, UiTypography.Body);
            }
            Tip(bench, hit, "Damage per second against a reference dummy, from the same bench the balance tests use. The fight varies; this compares builds.");
        }
    }

    private void DrawEmptyRow(SpriteBatch b, Rectangle row, Point hit)
    {
        var over = row.Contains(hit);
        _ui.Plate(b, row);
        if (over) Outline(b, row, Slate, 1);
        var gbox = new Rectangle(row.X + 32, row.Y + 17, 56, 56);
        Outline(b, gbox, UiInk.Empty, 1);
        _ui.TextBig(b, "EMPTY SLOT", row.X + 100, row.Y + 14, UiInk.Empty, UiTypography.Headline);
        _ui.TextBig(b, "CLICK, THEN PICK A SKILL FROM THE LIBRARY", row.X + 100, row.Y + 14 + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Secondary);
    }

    // ── SKILLS: the library by style, and the selected skill's tree ────────────────────────────────────
    private void DrawSkills(SpriteBatch b, Point hit)
    {
        var panel = SkillsPanel;
        _ui.Plate(b, panel);
        var known = KnownSkills();
        var skills = Loadout.Skills;
        _ui.TextBig(b, "SKILLS", SkillsX, panel.Y + 16, Slate, UiTypography.Secondary);
        var learnedHead = $"LEARNED {known.Count(id => SkillCatalogue.Find(id) is not null)} / 12  ·  LEARN MORE ON THE MASTERY TREE";
        // Shortened against the room LEFT of it, so a narrower page (UI SCALE) trims the sentence instead of
        // printing it through the word SKILLS.
        var headRoom = SkillsW - _ui.MeasureBig("SKILLS", UiTypography.Secondary) - 24;
        _ui.TextRightBig(b, _ui.ShortenBig(learnedHead, headRoom, UiTypography.Secondary), SkillsX + SkillsW, panel.Y + 16, Slate, UiTypography.Secondary);

        var equipped = skills.Select(s => s.SkillId).ToHashSet(StringComparer.Ordinal);
        for (var st = 0; st < 6; st++)
        {
            var style = (Style)st;
            var rowY = LibTop + st * LibRowPitch;
            var yours = ChosenStyle == style;
            _ui.TextBig(b, StyleName(style), SkillsX, rowY + 14, yours ? Gold : Slate, UiTypography.Body);
            if (yours) _ui.TextBig(b, "YOURS", SkillsX, rowY + 14 + UiTypography.Pitch(UiTypography.Body) - 6, Gold, UiTypography.Caption);
            for (var w = 0; w < 2; w++)
            {
                var def = w == 0 ? SkillCatalogue.ActiveOf(style) : SkillCatalogue.PassiveOf(style);
                var tile = LibTile(st, w);
                var have = known.Contains(def.Id);
                var isEquipped = equipped.Contains(def.Id);
                var on = _pick == Pick.Library && _pickSkillId == def.Id;
                var over = tile.Contains(hit);
                _ui.Fill(b, tile, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.8f);
                Outline(b, tile, on ? Bone : isEquipped ? Gold : over ? Slate : Dim, on || isEquipped ? 2 : 1);
                var ico = new Rectangle(tile.X + 8, tile.Y + 8, 36, 36);
                if (!_ui.Icon(b, $"icon_skill_{def.Id}", ico, have ? (isEquipped ? Gold : Bone) : Slate)) _ui.Diamond(b, ico, Slate);
                _ui.TextBig(b, _ui.ShortenBig(def.Name, tile.Width - 56 - 70, UiTypography.Body), tile.X + 52, tile.Y + 6, have ? (isEquipped ? Gold : Bone) : Slate, UiTypography.Body);
                _ui.TextBig(b, def.TakesABeat ? "ACTIVE" : "PASSIVE", tile.X + 52, tile.Y + 6 + UiTypography.Pitch(UiTypography.Body) - 6, Slate, UiTypography.Caption);
                if (!have) _ui.Icon(b, "ui_slot_locked", new Rectangle(tile.Right - 28, tile.Y + 14, 20, 20), Slate);
                else if (isEquipped) _ui.TextRightBig(b, $"EQUIPPED · SLOT {Loadout.IndexOfSkill(def.Id) + 1}", tile.Right - 8, tile.Y + 16, Gold, UiTypography.Caption);
                Tip(tile, hit, have ? $"{def.Name} — {def.Line}" : $"{def.Name} — learned on {StyleName(style)}'s road, on the MASTERY tree.");
            }
        }

        // ── THE SKILL TREE of the selected slot's skill — drawn as the fork it is. ───────────────────
        _ui.Fill(b, new Rectangle(SkillsX, TreeTop - 14, SkillsW, 1), Dim);
        if (SlotDef(_slot) is not { } treeDef || !known.Contains(treeDef.Id))
        {
            _ui.TextBig(b, "SKILL TREE", SkillsX, TreeTop, Slate, UiTypography.Secondary);
            _ui.TextBig(b, "PICK A FILLED SLOT — ITS SKILL'S OWN TREE OPENS HERE", SkillsX, TreeTop + UiTypography.Pitch(UiTypography.Secondary) + 6, UiInk.Empty, UiTypography.Body);
            return;
        }
        var chosen = SkillLevels.VariationOf(treeDef);
        var free = SkillLevels.FreeOn(treeDef.Id);
        var level = SkillLevels.LevelOf(treeDef.Id);
        var uses = SkillLevels.UsesOf(treeDef.Id);
        var headIco = new Rectangle(SkillsX, TreeTop, 48, 48);
        _ui.Icon(b, $"icon_skill_{treeDef.Id}", headIco, Gold);
        _ui.TextBig(b, $"{treeDef.Name} — SKILL TREE", headIco.Right + 14, TreeTop, Bone, UiTypography.Headline);
        var lvl = level >= SkillProgress.MaxLevel ? $"LEVEL {level} · MAX" : $"LEVEL {level} · {uses}/{SkillProgress.UsesForLevel(level + 1)} WAVES TO THE NEXT";
        if (free > 0) lvl += $"  ·  +{free} TO SPEND";
        _ui.TextBig(b, lvl, headIco.Right + 14, TreeTop + UiTypography.Pitch(UiTypography.Headline) - 4, free > 0 ? Gold : Slate, UiTypography.Secondary);
        Tip(new Rectangle(SkillsX, TreeTop, SkillsW, 60), hit, "A skill levels by being used: clear waves with it equipped. The first level chooses a variation — and the variation is what gives the skill its Source. The next levels buy that variation's reinforcements. Respec is free.");

        // The rails from the skill down to the two variations.
        var railY = TreeTop + 58;
        _ui.Fill(b, new Rectangle(headIco.Center.X - 1, headIco.Bottom, 2, railY - headIco.Bottom), Dim);
        _ui.Fill(b, new Rectangle(VarCard(0).Center.X, railY, VarCard(1).Center.X - VarCard(0).Center.X, 2), Dim);
        for (var vi = 0; vi < treeDef.Variations.Count && vi < 2; vi++)
        {
            var v = treeDef.Variations[vi];
            var card = VarCard(vi);
            _ui.Fill(b, new Rectangle(card.Center.X - 1, railY, 2, card.Y - railY), Dim);
            var taken = chosen?.Name == v.Name;
            var other = chosen is not null && !taken;
            var on = _pick == Pick.Variation && _pickIndex == vi;
            var over = card.Contains(hit);
            var col = SourceColor.GetValueOrDefault(v.Source, Bone);
            _ui.Fill(b, card, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.85f);
            Outline(b, card, on ? Bone : taken ? Gold : Dim, on || taken ? 2 : 1);
            if (taken) _ui.Fill(b, new Rectangle(card.X, card.Y, 5, card.Height), Gold);
            var gem = new Rectangle(card.X + 16, card.Y + 12, 40, 40);
            _ui.Diamond(b, new Rectangle(gem.X - 3, gem.Y - 3, gem.Width + 6, gem.Height + 6), col * (other ? 0.12f : 0.30f));
            if (_ui.Assets.Get($"source_{v.Source.ToString().ToLowerInvariant()}") is { } gg) b.Draw(gg, gem, other ? Color.White * 0.55f : Color.White);
            _ui.TextBig(b, v.Name.ToUpperInvariant(), gem.Right + 12, card.Y + 10, taken ? Gold : other ? Slate : Bone, UiTypography.Body);
            _ui.TextBig(b, $"{SourceName(v.Source)}{(taken ? " · CHOSEN" : "")}", gem.Right + 12, card.Y + 10 + UiTypography.Pitch(UiTypography.Body) - 4, other ? Slate : col, UiTypography.Secondary);
            _ui.TextBig(b, _ui.ShortenBig(v.Line.ToUpperInvariant(), card.Width - 24, UiTypography.Caption), card.X + 12, card.Bottom - 8 - UiTypography.Caption - 2, Slate, UiTypography.Caption);
            Tip(card, hit, $"{v.Name} · {SourceName(v.Source)} — {v.Line} {BuildGlossary.SourceLine(v.Source)}.");

            // Its three reinforcements: under a taken fork they are buyable; under the other, a road not taken (readable, not dim).
            for (var ri = 0; ri < v.Reinforcements.Count && ri < 3; ri++)
            {
                var r = v.Reinforcements[ri];
                var chip = ReinfChip(vi, ri);
                var owned = taken && SkillLevels.HasReinforcement(treeDef.Id, r.Name);
                var can = taken && !owned && free > 0;
                var onR = _pick == Pick.Reinforcement && taken && _pickIndex == ri;
                var overR = chip.Contains(hit);
                _ui.Fill(b, chip, onR ? new Color(0x2C, 0x25, 0x44) : overR ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.7f);
                Outline(b, chip, onR ? Bone : owned ? Met : Dim, onR || owned ? 2 : 1);
                _ui.TextBig(b, r.Name.ToUpperInvariant(), chip.X + 10, chip.Y + 6, owned ? Met : can ? Gold : taken ? Bone : Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, owned ? "OWNED" : can ? "READY" : taken ? $"LEVEL {level + 1}" : "", chip.Right - 10, chip.Y + 6, owned ? Met : can ? Gold : Slate, UiTypography.Caption);
                Tip(chip, hit, $"{r.Name} — {r.Line}");
            }
        }
    }

    // ── THE INSPECTOR (D3) ──────────────────────────────────────────────────────────────────────────────
    private void DrawInspector(SpriteBatch b, Point hit)
    {
        var panel = InspectorPanel;
        _ui.PanelQuiet(b, panel);
        var x = InsX; var w = InsW;
        var y = panel.Y + UiTypography.PanelTitleTop;
        var floor = RespecText.Y - 16;
        var known = KnownSkills();
        var skills = Loadout.Skills;
        var ctx = Context;
        _changeVowY = 0;
        _respecShown = false;

        void Head(string s) { if (y + UiTypography.Pitch(UiTypography.Secondary) > floor) return; _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary); y += UiTypography.Pitch(UiTypography.Secondary); }
        void Line(string s, Color c, int px = UiTypography.Body, int maxLines = 3)
        {
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
            {
                if (y + UiTypography.Pitch(px) > floor) return;
                _ui.TextBig(b, l, x, y, c, px); y += UiTypography.Pitch(px);
            }
        }
        void Gap(int px = 10) { y += px; }
        void Rule() { if (y + 12 < floor) { _ui.Fill(b, new Rectangle(x, y + 4, w, 1), Dim); y += 12; } }

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
            _ui.TextBig(b, k.Name.ToUpperInvariant(), x, y, Loadout.HasKeystone(k.Id) ? Gold : Bone, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + 4;
            Head("WHAT IT DOES"); Line(k.Blurb, Bone, UiTypography.Body, 6); Gap();
            Head("CURRENT STATE"); Line(Loadout.HasKeystone(k.Id) ? "IN USE" : "NOT IN USE", Bone);
            Line($"SOCKETS {Loadout.KeystoneIds.Count} / {Loadout.KeystoneCapacity} — MORE ON THE TRAITS SCREEN", Slate, UiTypography.Secondary);
        }
        else if (def is null || (_pick == Pick.Slot && !known.Contains(def.Id)))
        {
            Head(_slot < skills.Count ? $"SLOT {_slot + 1}" : "NOTHING SELECTED");
            _ui.TextBig(b, "EMPTY", x, y, UiInk.Empty, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + 4;
            Line("Pick a skill from the library and press EQUIP. A skill is learned on the MASTERY tree; once learned it is yours for good.", Slate);
        }
        else
        {
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
                _ui.TextRightBig(b, SourceName(v.Source), x + w, y + 6, col, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Headline) + 4;
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
            }
            else if (_pick == Pick.Reinforcement && chosen is { } cv && _pickIndex < cv.Reinforcements.Count)
            {
                var r = cv.Reinforcements[_pickIndex];
                var owned = SkillLevels.HasReinforcement(def.Id, r.Name);
                Head($"REINFORCEMENT OF {def.Name.ToUpperInvariant()} · {cv.Name.ToUpperInvariant()}");
                _ui.TextBig(b, r.Name.ToUpperInvariant(), x, y, owned ? Met : Bone, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + 4;
                Head("WHAT IT DOES"); Line(r.Line, Bone); Gap(); Rule();
                Head("YOU NEED FIRST");
                Line(owned ? "OWNED" : free > 0 ? $"ONE FREE LEVEL — YOU HAVE {free}" : $"LEVEL {level + 1} — {WavesToNext(def)} MORE WAVES WITH {def.Name.ToUpperInvariant()} EQUIPPED", owned ? Met : free > 0 ? Gold : Bone);
                _respecShown = true;
            }
            else
            {
                // A skill: from a slot, or from the library.
                Head($"SKILL · {StyleName(def.Style)} · {(def.TakesABeat ? "ACTIVE" : "PASSIVE")}{(slotOf >= 0 ? $" · SLOT {slotOf + 1}" : "")}");
                var ico = new Rectangle(x, y, 48, 48);
                _ui.Icon(b, $"icon_skill_{def.Id}", ico, have ? Gold : Slate);
                _ui.TextBig(b, def.Name.ToUpperInvariant(), ico.Right + 12, y + 8, Bone, UiTypography.Headline);
                y += 56;
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
                    _ui.Icon(b, "ui_slot_locked", new Rectangle(x, y + 2, 20, 20), Bone);
                    _ui.TextBig(b, $"LEARNED ON {StyleName(def.Style)}'S ROAD, ON THE MASTERY TREE", x + 28, y, Bone, UiTypography.Body); y += UiTypography.Pitch(UiTypography.Body);
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
                    if (_vowListOpen) { DrawVowList(b, hit, ref y, floor); }
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
                        if (Known.Count > 0 && y + 48 < floor)
                        {
                            _changeVowY = y + 4;
                            var btn = ChangeVowBtn(_changeVowY);
                            _ui.Fill(b, btn, btn.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
                            Outline(b, btn, btn.Contains(hit) ? Bone : Dim, 1);
                            _ui.TextCenterBig(b, vow is null ? "BIND A VOW" : "CHANGE VOW", btn.Center.X, btn.Y + 9, btn.Contains(hit) ? Bone : Slate, UiTypography.Body);
                            y = btn.Bottom + 8;
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
                        if (y + UiTypography.Pitch(UiTypography.Body) > floor) break;
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

        // ── The actions: a refusal line, one primary button, two text actions. ───────────────────────
        var (label, enabled, refusal) = Primary();
        if (refusal.Length > 0)
            _ui.TextBig(b, _ui.ShortenBig(refusal, w, UiTypography.Secondary), x, RespecText.Y - 30, Ember, UiTypography.Secondary);
        if (_respecShown)
        {
            var over = RespecText.Contains(hit);
            _ui.TextBig(b, "RESPEC — FREE", RespecText.X, RespecText.Y + 4, over ? Bone : Slate, UiTypography.Secondary);
            Tip(RespecText, hit, "Give this skill's levels back. Free, and it keeps every level it has earned.");
        }
        {
            var over = CopyText.Contains(hit);
            _ui.TextRightBig(b, "COPY BUILD CODE", CopyText.Right, CopyText.Y + 4, over ? Bone : Slate, UiTypography.Secondary);
            Tip(CopyText, hit, "Copies this build as a code. A friend pastes it in the VAULT.");
        }
        if (label.Length > 0)
            _ui.Button(b, PrimaryBtn, label, hit, false, enabled, enabled ? ButtonStyle.Primary : ButtonStyle.Secondary);

        void Pair(string k, string v)
        {
            if (y + UiTypography.Pitch(UiTypography.Body) > floor) return;
            _ui.TextBig(b, k, x, y, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, _ui.ShortenBig(v, w - 120, UiTypography.Body), x + w, y - 2, Bone, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }
    }

    /// <summary>The known vows, in place of the sections below the validator: click one to bind it to the selected slot.</summary>
    private void DrawVowList(SpriteBatch b, Point hit, ref int y, int floor)
    {
        var known = Known;
        var ctx = Context;
        var skills = Loadout.Skills;
        var sworn = _slot < skills.Count ? skills[_slot].VowId : null;
        _ui.TextBig(b, $"BIND TO SLOT {_slot + 1}", InsX, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, "CLICK ONE", InsX + InsW, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary) + 4;
        _vowListTop = y;
        _vowScroll = Math.Clamp(_vowScroll, 0, Math.Max(0, known.Count + 1 - VowListRows));
        for (var r = 0; r < VowListRows; r++)
        {
            var idx = _vowScroll + r - 1;
            if (idx >= known.Count) break;
            var row = VowListRow(r, _vowListTop);
            if (row.Bottom > floor - 48) break;
            var over = row.Contains(hit);
            if (idx < 0)
            {
                _ui.Fill(b, row, over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
                Outline(b, row, sworn is null ? Gold : over ? Slate : Dim, sworn is null ? 2 : 1);
                _ui.TextBig(b, "NO VOW", row.X + 12, row.Y + 10, sworn is null ? Gold : Bone, UiTypography.Body);
                continue;
            }
            var v = known[idx];
            var live = Vows.IsActive(v, ctx);
            var on = v.Id == sworn;
            _ui.Fill(b, row, on ? new Color(0x2C, 0x25, 0x44) : over ? new Color(0x1E, 0x18, 0x2C) : Quiet);
            Outline(b, row, on ? Gold : over ? Slate : Dim, on ? 2 : 1);
            _ui.TextBig(b, _ui.ShortenBig(v.Name.ToUpperInvariant(), row.Width - 170, UiTypography.Body), row.X + 12, row.Y + 4, on ? Gold : Bone, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig(DemandText(v), row.Width - 170, UiTypography.Caption), row.X + 12, row.Y + 4 + UiTypography.Pitch(UiTypography.Body) - 6, Slate, UiTypography.Caption);
            _ui.TextRightBig(b, $"x{Vows.Multiplier(v):0.00}", row.Right - 12, row.Y + 4, live ? Gold : Slate, UiTypography.Body);
            _ui.TextRightBig(b, live ? "HOLDS" : "BROKEN", row.Right - 12, row.Y + 4 + UiTypography.Pitch(UiTypography.Body) - 6, live ? Met : Ember, UiTypography.Caption);
            Tip(row, hit, v.Description);
        }
        y = Math.Min(floor - 48, _vowListTop + Math.Min(VowListRows, known.Count + 1) * 50);
        _changeVowY = y + 4;
        var btn = ChangeVowBtn(_changeVowY);
        _ui.Fill(b, btn, btn.Contains(hit) ? new Color(0x2C, 0x25, 0x44) : Quiet);
        Outline(b, btn, Dim, 1);
        _ui.TextCenterBig(b, "CLOSE", btn.Center.X, btn.Y + 9, btn.Contains(hit) ? Bone : Slate, UiTypography.Body);
        y = btn.Bottom + 8;
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
            var r = new Rectangle(p.X - 90, p.Y - 22, 180, 44);
            _ui.Fill(b, new Rectangle(r.X + 4, r.Y + 5, r.Width, r.Height), new Color(0, 0, 0) * 0.45f);
            _ui.Fill(b, r, new Color(0x2C, 0x25, 0x44));
            _ui.Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), col);
            Outline(b, r, Bone, 2);
            _ui.TextBig(b, _ui.ShortenBig(SkillCatalogue.Find(s.SkillId)?.Name ?? "EMPTY", 150, UiTypography.Body), r.X + 16, r.Y + 10, Bone, UiTypography.Body);
        }
    }

    /// <summary>A chain closing on the slot's skill glyph — the flourish a bound Vow plays over its row.</summary>
    private void DrawBindChain(SpriteBatch b, Rectangle row, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var played = 1f - t;
        _ui.Fill(b, row, Gold * (0.22f * t * t));
        var gem = new Rectangle(row.X + 32, row.Y + 17, 56, 56);
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
