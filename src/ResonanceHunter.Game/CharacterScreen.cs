using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The CHARACTER sheet: where you dress the champion and train it — kept apart from the fight it feeds.
/// </summary>
/// <remarks>
/// <para>
/// Stat training used to be four labels squeezed onto the combat screen's "COMMAND" strip (four of the
/// nine stats, mid-fight). The player asked for it to live somewhere of its own, next to the gear, with
/// the champion actually shown. So this is a real character sheet: a paper-doll with the three worn
/// slots, every trainable stat, the bag to equip from, and — the point of the whole screen — a live
/// DAMAGE / SEC gauge (<see cref="DamageBench"/>) so a change to gear, a stat, or the tree can be judged
/// by whether the number moves.
/// </para>
/// <para>
/// That gauge is also an honesty check. It first revealed three DEAD stats — FOCUS, DEFENSE and CRIT
/// reached no formula in the solo game, so training them moved nothing. They are wired now (CRIT is crit
/// chance, FOCUS crit damage, DEFENSE incoming-damage mitigation — see <see cref="SoloBattle"/>), so all
/// nine stats do something, and the bench shows the offensive ones move the number.
/// </para>
/// </remarks>
public sealed class CharacterScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Bg = new(0x0E, 0x0C, 0x12);
    private static readonly Color RowBg = new(0x1C, 0x18, 0x24);
    private static readonly Color RowHot = new(0x2A, 0x24, 0x14);
    private static readonly Color Shadow = new(0x08, 0x07, 0x0B);

    private readonly UiKit _ui;
    private readonly ForgeScreen _forge;   // owns the item bag + the shared item-icon renderer

    /// <summary>The live build, for the DPS gauge. Set by the host each frame, like BuildScreen.</summary>
    public PlayerLoadout Loadout { get; set; } = null!;
    public MasteryTree Mastery { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;

    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    private DamageReadout _readout;
    private string _msg = "";
    private Color _msgColor = Bone;

    public CharacterScreen(UiKit ui, ForgeScreen forge) { _ui = ui; _forge = forge; }

    // ── Layout: three columns so nothing is cramped; all end by y=232 to clear the shared nav bar. ────
    private static readonly Rectangle DollPanel = new(24, 104, 584, 632);
    private static readonly Rectangle DpsPanel = new(24, 752, 584, 176);
    private static readonly Rectangle StatPanel = new(632, 104, 704, 824);
    private static readonly Rectangle BagPanel = new(1360, 104, 536, 824);

    // Eight slots: armour down the left, weapon + jewellery down the right, the champion between them.
    private static readonly GearSlot[] LeftSlots = { GearSlot.Helm, GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots };
    private static readonly GearSlot[] RightSlots = { GearSlot.Weapon, GearSlot.Focus, GearSlot.Charm, GearSlot.Ring };

    // Back-to-front occlusion order for the dressed-champion overlays (matches SoloExpeditionScreen).
    private static readonly GearSlot[] OverlayOrder =
    {
        GearSlot.Chest, GearSlot.Boots, GearSlot.Gloves, GearSlot.Helm,
        GearSlot.Weapon, GearSlot.Ring, GearSlot.Charm, GearSlot.Focus,
    };
    private static readonly GearSlot[] Slots =
    {
        GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus, GearSlot.Helm,
        GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring,
    };

    private static readonly Rectangle SpriteBox = new(DollPanel.X + 176, DollPanel.Y + 72, 232, DollPanel.Height - 104);
    private static Rectangle LeftSlotBox(int i) => new(DollPanel.X + 24, DollPanel.Y + 64 + i * 136, 112, 112);
    private static Rectangle RightSlotBox(int i) => new(DollPanel.Right - 136, DollPanel.Y + 64 + i * 136, 112, 112);

    private static IEnumerable<(GearSlot Slot, Rectangle Box)> AllSlots()
    {
        for (var i = 0; i < LeftSlots.Length; i++) yield return (LeftSlots[i], LeftSlotBox(i));
        for (var i = 0; i < RightSlots.Length; i++) yield return (RightSlots[i], RightSlotBox(i));
    }

    private static string SlotAbbrev(GearSlot s) => s switch
    {
        GearSlot.Weapon => "WPN", GearSlot.Charm => "CHM", GearSlot.Focus => "FOC", GearSlot.Helm => "HELM",
        GearSlot.Chest => "CHST", GearSlot.Gloves => "GLOV", GearSlot.Boots => "BOOT", _ => "RING",
    };

    private static readonly (HunterStat Stat, string Label, string Effect)[] Stats =
    {
        (HunterStat.AttackPower,       "MIGHT",     "DAMAGE"),
        (HunterStat.ResonanceAffinity, "RESONANCE", "ABILITY"),
        (HunterStat.Engineering,       "TEMPO",     "SKILL SPD"),
        (HunterStat.CriticalChance,    "CRIT",      "CRIT %"),
        (HunterStat.Focus,             "FOCUS",     "CRIT DMG"),
        (HunterStat.Vitality,          "VITALITY",  "MAX HP %"),
        (HunterStat.MaxHealth,         "HEALTH",    "MAX HP"),
        (HunterStat.Defense,           "DEFENSE",   "MITIGATE"),
        (HunterStat.Guile,             "GUILE",     "HAUL"),
    };
    private static Rectangle StatRow(int i) => new(StatPanel.X + 24, StatPanel.Y + 96 + i * 80, StatPanel.Width - 48, 72);
    private static Rectangle TrainBtn(int i) { var r = StatRow(i); return new(r.Right - 184, r.Y + 12, 176, 60); }

    private const int BagVisible = 7;   // a single readable column — full type + rarity words fit
    private int _bagScroll;
    private static Rectangle BagCard(int vis) => new(BagPanel.X + 24, BagPanel.Y + 80 + vis * 96, BagPanel.Width - 48, 88);

    private List<ItemInstance> Wearable()
        => _forge.Inventory.Where(i => Gear.SlotFor(i.BaseType) is not null).ToList();

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter)
    {
        var hit = new Point(mouse.X * 4, mouse.Y * 4);
        var bag = Wearable();

        // Recompute the bench every frame — it is one deterministic wave sim, cheap for a menu, and
        // always current no matter what the tree/gear/stats just did.
        if (Loadout is not null && Tree is not null && Mastery is not null)
            _readout = DamageBench.Measure(Loadout.ToBuild(Tree, Mastery), hunter);

        if (wheel != 0 && BagPanel.Contains(hit))
        {
            var maxScroll = Math.Max(0, bag.Count - BagVisible);
            _bagScroll = Math.Clamp(_bagScroll - Math.Sign(wheel), 0, maxScroll);
        }

        if (!clicked) return;

        // Take off a worn item by clicking its doll slot.
        foreach (var (slot, box) in AllSlots())
            if (box.Contains(hit) && hunter.Worn(slot) is { } worn)
            {
                ToggleEquip(hunter, worn);
                return;
            }

        // Training moved to its own STATS page (press V) — this sheet is gear + bag now, and the stat
        // column here is a read-only summary. Clicking it jumps nowhere; the values are informational.

        // Equip / unequip a bag item.
        for (var vis = 0; vis < BagVisible; vis++)
        {
            var idx = _bagScroll + vis;
            if (idx >= bag.Count) break;
            if (BagCard(vis).Contains(hit)) { ToggleEquip(hunter, bag[idx]); return; }
        }
    }

    private void ToggleEquip(Hunter hunter, ItemInstance item)
    {
        if (Gear.SlotFor(item.BaseType) is not { } slot) return;
        if (hunter.Worn(slot)?.InstanceId == item.InstanceId) { hunter.Unequip(slot); Say("TOOK IT OFF.", Slate); }
        else { hunter.Equip(item); Say($"EQUIPPED. POWER {hunter.PowerRating}.", Gold); }
        Dirty = true;
    }

    private void Say(string msg, Color color) { _msg = msg; _msgColor = color; }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        var hit = new Point(mouse.X * 4, mouse.Y * 4);
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Bg);

        // Header band — CHARACTER, the gear score (average worn item level), then LV/POWER and Gleam.
        var wornItems = Slots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        var gearIl = wornItems.Count > 0 ? (int)Math.Round(wornItems.Average(i => i.ItemLevel)) : 0;
        _ui.Diamond(b, new Rectangle(32, 20, 52, 52), new Color(0x5F, 0xE0, 0xC8));   // title gem, like the reference
        _ui.Text(b, "CHAMPION GEAR", 104, 16, Gold);
        _ui.Text(b, $"LV {hunter.HunterLevel} · POWER {hunter.PowerRating} · GEAR iL{gearIl}", 104, 56, Slate);
        // Gleam/Dust/Materials are drawn as the shared currency pills (Game1.DrawCurrencyPills).

        DrawPaperDoll(b, hit, hunter);
        DrawDps(b);
        DrawStats(b, hit, hunter);
        DrawBag(b, hit, hunter);

        if (_hoverItem is { } hov) DrawItemTooltip(b, hov, hunter, hit);

        // No equip toast: the top-right is the shared currency row now, and the swap already shows itself —
        // the WORN badge lights, the doll re-dresses, POWER and DAMAGE/SEC update live.
        _ = _msg;
    }

    private void DrawPaperDoll(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, DollPanel);
        _ui.Text(b, "GEAR", DollPanel.X + 24, DollPanel.Y + 24, Slate);

        // The champion in the centre, DRESSED in whatever is equipped: a base body plus one overlay per worn
        // slot, every layer sharing the base's 800x1040 canvas so it registers when drawn into the same rect.
        // This is the whole point of the paper-doll — the gear you put on shows on the body, not just in a box.
        _ui.Fill(b, new Rectangle(SpriteBox.X + 16, SpriteBox.Bottom - 12, SpriteBox.Width - 32, 12), Shadow);
        var baseTex = _ui.Assets.Get("hunter_idle");   // package_02 full-body pose (equipment overlays need per-pose tuning — deferred)
        if (baseTex is not null)
        {
            var sc = MathF.Min(SpriteBox.Width / (float)baseTex.Width, SpriteBox.Height / (float)baseTex.Height);
            var w = Math.Max(1, (int)(baseTex.Width * sc));
            var h = Math.Max(1, (int)(baseTex.Height * sc));
            var rect = new Rectangle(SpriteBox.Center.X - w / 2, SpriteBox.Bottom - h, w, h);
            b.Draw(baseTex, rect, Color.White);
            foreach (var slot in OverlayOrder)
                if (hunter.Worn(slot) is not null
                    && _ui.Assets.Get($"overlay_{slot.ToString().ToLowerInvariant()}_idle") is { } ov)
                    b.Draw(ov, rect, Color.White);
        }
        else _ui.Fill(b, new Rectangle(SpriteBox.Center.X - 32, SpriteBox.Bottom - 136, 64, 136), Gold);

        // The eight worn slots, four each side — hexagons like the reference doll: a rarity rim, the item as
        // an element-tinted gem, the slot name beneath.
        foreach (var (slot, box) in AllSlots())
        {
            var hot = box.Contains(hit);
            var worn = hunter.Worn(slot);
            var rim = hot ? Gold : worn is { } wr ? RarityRim[(int)wr.Rarity] : new Color(0x39, 0x31, 0x52);
            _ui.Hex(b, box, rim);
            _ui.Hex(b, new Rectangle(box.X + 8, box.Y + 8, box.Width - 16, box.Height - 16), new Color(0x16, 0x11, 0x28));
            // Show the ACTUAL equipped piece (its own icon), not a generic gem — this is the reference doll.
            if (worn is { } w2)
                _forge.DrawItemIcon(b, w2, new Rectangle(box.Center.X - 36, box.Center.Y - 40, 72, 72));
            else _ui.TextCenter(b, "—", box.Center.X, box.Center.Y - 16, Dim);
            // Label BELOW the hex, bone-bright when filled — it used to sit inside the hex's bottom point
            // in dim Slate, where it clipped against the outline and could not be read.
            _ui.TextCenter(b, SlotAbbrev(slot), box.Center.X, box.Bottom, hot ? Gold : worn is not null ? Bone : Slate);
        }
    }

    private static readonly Color[] RarityRim =
    {
        new(0x8A, 0x86, 0x96), new(0x6E, 0xC8, 0x7A), new(0x5A, 0xA6, 0xE6),
        new(0xC0, 0x6E, 0xE0), new(0xF0, 0xB2, 0x4A),
    };

    private static Color SlotGem(ItemInstance i) => i.Element switch
    {
        Source.Body => new Color(0xD6, 0x48, 0x5C), Source.Mind => new Color(0x74, 0xC6, 0xE8),
        Source.Nature => new Color(0x48, 0xB8, 0x88), Source.Machine => new Color(0xBC, 0x78, 0x40),
        Source.Shadow => new Color(0x9A, 0x7A, 0xD8), Source.Spirit => new Color(0xDC, 0xD4, 0xEC),
        _ => new Color(0x5F, 0xE0, 0xC8),
    };

    private void DrawDps(SpriteBatch b)
    {
        _ui.Panel(b, DpsPanel, gold: true);
        _ui.Text(b, "DAMAGE / SEC", DpsPanel.X + 32, DpsPanel.Y + 28, Slate);
        _ui.Text(b, Short((long)MathF.Round(_readout.Dps)), DpsPanel.X + 32, DpsPanel.Y + 80, Ember);
        _ui.TextRight(b, "VS DUMMY", DpsPanel.Right - 32, DpsPanel.Y + 88, Slate);
        _ui.Text(b, $"OVER {_readout.Seconds:0}S AT FULL HP", DpsPanel.X + 32, DpsPanel.Bottom - 48, Slate);
    }

    private void DrawStats(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, StatPanel);
        _ui.Text(b, "STATS", StatPanel.X + 24, StatPanel.Y + 32, Slate);
        _ui.TextRight(b, "TRAIN ON V", StatPanel.Right - 24, StatPanel.Y + 32, Gold);

        // A READ-ONLY summary — the values only. Training lives on the STATS page (V) with full descriptions.
        for (var i = 0; i < Stats.Length; i++)
        {
            var (stat, label, effect) = Stats[i];
            var row = StatRow(i);

            _ui.Fill(b, row, RowBg);
            _ui.Text(b, label, row.X + 16, row.Y + 8, Bone);
            _ui.Text(b, effect, row.X + 16, row.Y + 44, Slate);
            _ui.TextRight(b, $"{(int)hunter.ValueOf(stat)}", row.Right - 24, row.Y + 24, Gold);
            if (hunter.RankOf(stat) >= 60) _ui.TextRight(b, "MAX", row.Right - 136, row.Y + 24, Slate);
        }
    }

    private ItemInstance? _hoverItem;   // the bag item under the cursor this frame — its detail tooltip

    private void DrawBag(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, BagPanel);
        _hoverItem = null;
        var bag = Wearable();
        _ui.Text(b, "BAG", BagPanel.X + 24, BagPanel.Y + 32, Slate);
        _ui.TextRight(b, $"{bag.Count}", BagPanel.Right - 24, BagPanel.Y + 32, Dim);

        if (bag.Count == 0)
        {
            _ui.TextCenter(b, "NO GEAR YET", BagPanel.Center.X, BagPanel.Y + 440, Dim);
            _ui.TextCenter(b, "BOSSES DROP CHESTS", BagPanel.Center.X, BagPanel.Y + 488, Dim);
            return;
        }

        for (var vis = 0; vis < BagVisible; vis++)
        {
            var idx = _bagScroll + vis;
            if (idx >= bag.Count) break;
            var item = bag[idx];
            var card = BagCard(vis);
            var worn = Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId;
            var hot = card.Contains(hit);
            if (hot) _hoverItem = item;

            _ui.Fill(b, card, hot ? RowHot : RowBg);
            _forge.DrawItemIcon(b, item, new Rectangle(card.X + 12, card.Y + 12, 80, 80));
            _ui.Text(b, SlotShort(item.BaseType), card.X + 112, card.Y + 16, worn ? Gold : Bone);
            _ui.Text(b, RarityShort(item.Rarity), card.X + 112, card.Y + 56, RarityColor(item.Rarity));
            _ui.TextRight(b, $"iL{item.ItemLevel}", card.Right - 16, card.Y + 16, Slate);
            // The idle affordance: a green UP when this beats what you have in its slot, answered before
            // the player even reads the stats.
            if (worn) _ui.TextRight(b, "WORN", card.Right - 16, card.Y + 56, Gold);
            else if (Gear.SlotFor(item.BaseType) is { } sl && Gear.ItemScore(item) > Gear.ItemScore(hunter.Worn(sl)))
                _ui.TextRight(b, "UP", card.Right - 16, card.Y + 56, new Color(0x6E, 0xC8, 0x7A));
        }

        var below = bag.Count - (_bagScroll + BagVisible);
        if (below > 0) _ui.TextRight(b, $"+{below} MORE - SCROLL", BagPanel.Right - 24, BagPanel.Bottom - 40, Dim);
        else if (_bagScroll > 0) _ui.TextRight(b, "SCROLL UP", BagPanel.Right - 24, BagPanel.Bottom - 40, Dim);
    }

    /// <summary>
    /// The floating comparison card: hover a bag item and see it laid AGAINST the piece you wear in that
    /// slot. Every rolled attribute (affix) lines up new-vs-worn and is coloured for the verdict; both
    /// passives (the trait trade and the enchant trigger) are shown for each; and it closes with the net
    /// power move. Answers "is this better, and in what way?" fully — not just on damage.
    /// </summary>
    private void DrawItemTooltip(SpriteBatch b, ItemInstance item, Hunter hunter, Point hit)
    {
        var slot = Gear.SlotFor(item.BaseType);
        var equipped = slot is { } s ? hunter.Worn(s) : null;

        // Attributes as stat -> magnitude maps, so a stat present on EITHER item shares one aligned row.
        var newAff = ItemAffixes.Of(item).ToDictionary(a => a.Stat, a => a.Magnitude);
        var eqAff = equipped is null
            ? new Dictionary<AffixStat, float>()
            : ItemAffixes.Of(equipped).ToDictionary(a => a.Stat, a => a.Magnitude);
        var stats = newAff.Keys.Union(eqAff.Keys).OrderBy(st => (int)st).ToList();

        var newTrait = GearTraits.TraitOf(item);
        var eqTrait = equipped is null ? null : GearTraits.TraitOf(equipped);
        var newEnch = Enchantments.Of(item);
        var eqEnch = equipped is null ? null : Enchantments.Of(equipped);

        var green = new Color(0x6E, 0xC8, 0x7A);
        var violet = new Color(0xC8, 0x8A, 0xE0);

        const int w = 736;
        var contentRows = 1 /*NEW/WORN header*/ + Math.Max(1, stats.Count) + 2 /*two passive lines*/;
        var h = 112 + contentRows * 40 + 56;
        // Grow LEFTWARD from the bag; clamp vertically so a tall card never runs off the canvas.
        var x = BagPanel.X - w - 12;
        var y = Math.Clamp(hit.Y - 24, 104, 1048 - h);
        var box = new Rectangle(x, y, w, h);

        _ui.Fill(b, new Rectangle(box.X + 8, box.Y + 8, box.Width, box.Height), Shadow);
        _ui.Fill(b, box, new Color(0x14, 0x11, 0x1A));
        _ui.Fill(b, new Rectangle(box.X, box.Y, box.Width, 4), RarityColor(item.Rarity));

        // Header — the NEW item's identity.
        _ui.Text(b, $"{SlotShort(item.BaseType)}  iL{item.ItemLevel}", box.X + 24, box.Y + 24, Bone);
        _ui.Text(b, RarityShort(item.Rarity), box.X + 24, box.Y + 64, RarityColor(item.Rarity));

        var colNew = box.Right - 208;   // right edge of the NEW value column
        var colNow = box.Right - 24;    // right edge of the WORN value column
        var ly = box.Y + 112;
        _ui.TextRight(b, "NEW", colNew, ly, Slate);
        _ui.TextRight(b, equipped is null ? "EMPTY" : "WORN", colNow, ly, Slate);
        ly += 40;

        // Attributes — one aligned row per stat; the NEW value is green if it beats WORN, ember if worse.
        if (stats.Count == 0) { _ui.Text(b, "NO ATTRIBUTES", box.X + 24, ly, Slate); ly += 40; }
        foreach (var st in stats)
        {
            var nv = newAff.GetValueOrDefault(st);
            var ev = eqAff.GetValueOrDefault(st);
            var col = nv > ev + 0.0001f ? green : nv < ev - 0.0001f ? Ember : Bone;
            _ui.Text(b, StatShort(st), box.X + 24, ly, Slate);
            _ui.TextRight(b, nv != 0 ? AffixVal(st, nv) : "—", colNew, ly, col);
            _ui.TextRight(b, ev != 0 ? AffixVal(st, ev) : "—", colNow, ly, Slate);
            ly += 40;
        }

        // Passives — trait trade + enchant. Names are too wide to column, so NEW on one row (trait gold,
        // enchant violet), the WORN piece's on the next in dim, to read the swap at a glance.
        var ntName = newTrait is { } nt ? GearTraits.NameOf(nt) : "—";
        _ui.Text(b, ntName, box.X + 24, ly, newTrait is null ? Slate : Gold);
        _ui.Text(b, "· " + (newEnch is { } ne ? ne.Name : "—"), box.X + 40 + _ui.Measure(ntName), ly,
            newEnch is null ? Slate : violet);
        ly += 40;
        var etName = eqTrait is { } et ? GearTraits.NameOf(et) : "—";
        _ui.Text(b, $"WAS  {etName} · {(eqEnch is { } ee ? ee.Name : "—")}", box.X + 24, ly, Slate);

        // Verdict — the net power move, coloured for the call.
        var delta = Gear.ItemScore(item) - Gear.ItemScore(equipped);
        var (verdict, vColor) = equipped is null
            ? ("EQUIPS TO EMPTY", green)
            : delta > 0 ? ($"UPGRADE  +{delta} PWR", green)
            : delta < 0 ? ($"DOWNGRADE  {delta} PWR", Ember)
            : ("SIDEGRADE  =", Slate);
        _ui.Fill(b, new Rectangle(box.X + 16, box.Bottom - 48, box.Width - 32, 4), Dim);
        _ui.Text(b, verdict, box.X + 24, box.Bottom - 40, vColor);
    }

    private static string StatShort(AffixStat s) => s switch
    {
        AffixStat.Damage => "DMG", AffixStat.Health => "HP", AffixStat.SkillRate => "SKILL",
        AffixStat.Haul => "HAUL", AffixStat.Crit => "CRIT", _ => "DEF",
    };

    /// <summary>Just the value (sign + unit) of an affix — the label lives in its own column.</summary>
    private static string AffixVal(AffixStat s, float m) => s switch
    {
        AffixStat.Crit => $"+{m:0.0}%",
        AffixStat.Defense => $"+{m:0}",
        _ => $"+{m * 100f:0}%",
    };

    private static string SlotShort(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "WEAPON", ItemBaseType.Charm => "CHARM", ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Helm => "HELM", ItemBaseType.Chest => "CHEST", ItemBaseType.Gloves => "GLOVES",
        ItemBaseType.Boots => "BOOTS", ItemBaseType.Ring => "RING", _ => "-",
    };
    private static string RarityShort(Rarity r) => r switch
    {
        Rarity.Common => "COMMON", Rarity.Uncommon => "UNCOMMON", Rarity.Rare => "RARE",
        Rarity.Epic => "EPIC", _ => "LEGENDARY",
    };
    private static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Common => Bone, Rarity.Uncommon => new Color(0x6E, 0xC8, 0x7A), Rarity.Rare => new Color(0x4A, 0x90, 0xD9),
        Rarity.Epic => new Color(0x8B, 0x3F, 0x82), _ => Gold,
    };

    private static string Short(long n)
        => n >= 1_000_000 ? $"{n / 1_000_000f:0.0}M" : n >= 1_000 ? $"{n / 1_000f:0.0}K" : n.ToString();

    private void Reticle(SpriteBatch b, Rectangle r, Color c)
    {
        const int len = 20, t = 4;
        _ui.Fill(b, new Rectangle(r.X, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Bottom - len, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
    }
}
