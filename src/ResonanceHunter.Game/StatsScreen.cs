using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The STATS screen (nav: STATS): the hunter summary card, ONE merged training panel — every trainable
/// stat as icon + name + rank + the REAL number the fight uses + a priced TRAIN button — a PROGRESS
/// panel, and RESET ALL TRAINING (the respec). Hovering a training row opens a plain-words card that
/// states the stat's exact rule with the player's own numbers filled in.
/// </summary>
/// <remarks>
/// <para>
/// Rebuilt to playtest item 3: <i>"I train my stats, fine, but I cannot SEE what my attack, health,
/// attack speed etc. actually ARE... MAIN TRAINING has dummy icons, COMBAT TRAINING has no icons —
/// two unrelated-looking panels. Merge them properly, icons for everything, explanations on hover."</i>
/// The two panels were the same purchase drawn in two dialects; they are now nine identical rows in one
/// panel, and every row's headline is the number the simulation actually consumes — read through
/// <see cref="Build.Resolve"/> and <see cref="SoloBattle"/>'s own public formulas, never recomputed
/// here, because a screen that recomputes a formula is a second implementation of it and the second one
/// is the one that drifts.
/// </para>
/// <para>
/// Playtest item 4 adds the respec: RESET ALL TRAINING returns 100% of the Gleam the ranks cost
/// (computed by <see cref="Hunter.TrainingRefund"/> from the real cost curve) and costs one Crystal.
/// Two clicks on purpose, exactly like the settings panel's START A NEW GAME: the first arms a red
/// are-you-sure state, the second confirms, any other click disarms.
/// </para>
/// </remarks>
public sealed class StatsScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Sky = new(0x7A, 0x9A, 0xC0);
    private static readonly Color RowBg = new(0x1A, 0x16, 0x24, 0xC0);
    private static readonly Color ArmedRed = new(0x8C, 0x1E, 0x1E);   // the settings panel's are-you-sure red

    private readonly UiKit _ui;

    // Set by the host each frame (like the other screens). Mastery gives the adept title; the career values
    // come from the game loop (they live in Game1, not on the Hunter).
    public MasteryTree Mastery { get; set; } = null!;
    public PlayerLoadout Loadout { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;

    /// <summary>
    /// The active character, because this page states the player's numbers and a character is one of
    /// the three things that changes them.
    /// </summary>
    /// <remarks>
    /// Passing this to <c>ToBuild</c> is not optional bookkeeping. The two-argument overload silently
    /// substitutes <c>null</c>, and a shape built without the character is a shape the sim never uses.
    /// The character layer arrived later and re-opened that exact bug once already.
    /// </remarks>
    public Character? Character { get; set; }
    public int HighestWave { get; set; }
    public int ChestsOpened { get; set; }
    public int MasteryPoints { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    public bool DevStatsDebug { get; set; }

    public StatsScreen(UiKit ui) => _ui = ui;

    /// <summary>The house abbreviation, so the subtitle agrees with the currency pill above it.</summary>
    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    // ── Layout: hunter card + progress on the left, ONE training panel in the middle, and the reset
    //    bar under it. Panels sized to what they draw — a column that ends where its content does. ──
    private static readonly Rectangle HunterCard = new(56, 150, 372, 480);
    private static readonly Rectangle ProgressPanel = new(56, 670, 372, 290);
    private static readonly Rectangle TrainPanel = new(472, 150, 1318, 700);
    private static readonly Rectangle ResetBar = new(472, 874, 1318, 106);

    // The screen only draws and hit-tests inside Draw; nothing to advance per-frame.
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter) { }

    // ── Request/consume, like WarrenScreen: the screen never mutates the Hunter, so the host stays the
    //    only place a currency is spent and the screen stays drawable in a capture with no game state. ──

    /// <summary>The stat a TRAIN button was pressed for this frame, taken once.</summary>
    private HunterStat? _trainRequest;

    public HunterStat? ConsumeTrain()
    {
        var r = _trainRequest;
        _trainRequest = null;
        return r;
    }

    /// <summary>True once, after the armed RESET ALL TRAINING button's confirming second click.</summary>
    /// <remarks>
    /// The host answers it with <c>Hunter.ResetTraining()</c> — which validates again (Core is the
    /// gate), spends the Crystal, refunds the Gleam and zeroes the ranks — then saves.
    /// </remarks>
    public bool ConsumeReset()
    {
        var r = _resetRequest;
        _resetRequest = false;
        return r;
    }

    private bool _resetRequest;
    private bool _resetArmed;   // first click landed; the next click confirms or disarms
    private long _resetArmedAtMs;   // when it landed — the arm expires after ArmSeconds like settings' reset

    /// <summary>How long the armed red state waits for the confirming click before standing down.</summary>
    private const float ArmSeconds = 4f;

    /// <summary>Withdraw the armed reset — navigating away must never leave a live one-click wipe.</summary>
    public void CancelConfirm() => _resetArmed = false;

    /// <summary>
    /// DEV ONLY: <c>RH_SHOT_ARM=reset</c> poses the armed red are-you-sure state for a capture.
    /// </summary>
    /// <remarks>
    /// Read once, and only while <c>RH_SHOT</c> itself is set, so a normal run never looks at it —
    /// the same pattern as ForgeScreen's <c>RH_SHOT_HOVER</c>. It sets draw-state only; no Hunter is
    /// touched, and no confirming click can come from a headless capture.
    /// </remarks>
    private bool _devArmPending =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_ARM") == "reset";

    // The hover card raised by whichever row the pointer rests on this frame. Stored during the row
    // draws and drawn LAST, over every panel, so no later panel can sit on top of the explanation.
    private string? _hoverTitle;
    private string[]? _hoverBody;
    private Point _hoverAt;

    public void Draw(SpriteBatch b, Point mouse, Hunter hunter, bool clicked = false)
    {
        // INVERT THE OVERLAY INSET BEFORE ANY HIT-TEST. Every rect on this screen is authored in
        // 1920x1080 and drawn through Game1.BeginOverlayCanvas; the cursor arrives in 480x270 canvas
        // space. Without this conversion the two spaces never meet — the exact bug that once made
        // every TRAIN button unclickable. check_mouse_space.py enforces it now.
        var hit = Game1.ToOverlay(mouse);
        _hoverTitle = null;
        _hoverBody = null;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));   // scrim over the shared backdrop

        _ui.TextCenterBig(b, "STATS", 960, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, $"SPEND GLEAM TO GET STRONGER — YOU HAVE {Ab(hunter.Gleam)}", 960, 80, Slate, UiTypography.Secondary);

        // ONE build, resolved ONCE, and every row reads from it — the same composition the fight uses
        // (Build.Resolve folds keystones, worn gear, trained stats and the passive tree together).
        var build = Loadout.ToBuild(Tree, Mastery, Character);
        var mods = build.Resolve(hunter);
        var shape = build.Shape;

        DrawHunterCard(b, hunter, hit);
        DrawTraining(b, hunter, build, mods, shape, hit, clicked);
        DrawReset(b, hunter, hit, clicked);
        DrawProgression(b, hunter, hit);
        if (DevStatsDebug) DrawDebug(b);

        DrawHoverCard(b);
    }

    private void DrawHunterCard(SpriteBatch b, Hunter hunter, Point hit)
    {
        _ui.PanelQuiet(b, HunterCard);
        _ui.TextCenterBig(b, "HUNTER", HunterCard.Center.X, HunterCard.Y + 22, Gold, UiTypography.SectionTitle);

        var por = new Rectangle(HunterCard.X + 40, HunterCard.Y + 66, 118, 118);
        if (_ui.Assets.GetFirst(Character?.PortraitKey ?? "hunter_portrait", "hunter_portrait") is { } p)
            b.Draw(p, por, Color.White);

        var tx = por.Right + 20;
        var adept = Mastery?.Affinity() is { } mf ? $"{FormShort(mf)} ADEPT" : "SEEKER";
        _ui.TextBig(b, adept, tx, HunterCard.Y + 74, Bone, UiTypography.PanelTitle);
        _ui.TextBig(b, $"LEVEL {hunter.HunterLevel}", tx, HunterCard.Y + 106, Gold, UiTypography.Body);
        // The card's LEVEL line answers "what does level mean" on hover — same card as PROGRESS's row.
        if (new Rectangle(tx, HunterCard.Y + 102, 180, 30).Contains(hit))
            SetHover(hit, "LEVEL", LevelCard(hunter));
        var hpBar = new Rectangle(tx, HunterCard.Y + 138, HunterCard.Right - tx - 24, 26);
        _ui.BarArt(b, hpBar, 1f, "health");
        _ui.TextCenterBig(b, $"{hunter.MaxHealth:N0} / {hunter.MaxHealth:N0}", hpBar.Center.X, hpBar.Y + 4, Bone, UiTypography.Secondary);
        _ui.TextBig(b, $"TEMPO {hunter.SquadSkillRate:0.00}x SKILL RATE", tx, HunterCard.Y + 176, Slate, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(HunterCard.X + 24, HunterCard.Y + 224, HunterCard.Width - 48, 2), Dim);

        if (_ui.Assets.Get("state_resonance_128") is { } gi) b.Draw(gi, new Rectangle(HunterCard.X + 32, HunterCard.Y + 258, 44, 44), Ember);
        _ui.TextBig(b, "GEAR POWER", HunterCard.X + 88, HunterCard.Y + 256, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{hunter.PowerRating:N0}", HunterCard.X + 88, HunterCard.Y + 284, Bone, UiTypography.PrimaryValue);

        if (_ui.Assets.Get("state_mastery_128") is { } mi) b.Draw(mi, new Rectangle(HunterCard.X + 32, HunterCard.Y + 350, 44, 44), Gold);
        _ui.TextBig(b, "MASTERY POINTS", HunterCard.X + 88, HunterCard.Y + 348, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{MasteryPoints:N0}", HunterCard.X + 88, HunterCard.Y + 376, Bone, UiTypography.PrimaryValue);
    }

    /// <summary>Every trainable stat, one identical row each: icon, name, rank, live effect, TRAIN.</summary>
    private void DrawTraining(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape,
                              Point hit, bool clicked)
    {
        _ui.Panel(b, TrainPanel);
        _ui.TextCenterBig(b, "TRAINING", TrainPanel.Center.X, TrainPanel.Y + 22, Gold, UiTypography.SectionTitle);
        _ui.TextCenterBig(b, "EVERY ROW SHOWS THE REAL NUMBER THE FIGHT USES · REST THE POINTER ON A ROW TO SEE ITS RULE",
                          TrainPanel.Center.X, TrainPanel.Y + 56, Slate, UiTypography.Secondary);

        // Icon keys are AssetLibrary aliases (stat_*) onto shipped art; the colour is the flat-diamond
        // fallback if a file ever goes missing (the house rule: fail soft, never blank).
        var rows = new (string Label, HunterStat Stat, string IconKey, Color Gem)[]
        {
            ("MIGHT", HunterStat.AttackPower, "stat_might", new(0xD6, 0x48, 0x5C)),
            ("RESONANCE", HunterStat.ResonanceAffinity, "stat_resonance", new(0x74, 0xC6, 0xE8)),
            ("TEMPO", HunterStat.Engineering, "stat_tempo", new(0x48, 0xB8, 0x88)),
            ("VITALITY", HunterStat.Vitality, "stat_vitality", new(0xC0, 0x6E, 0xE0)),
            ("HEALTH", HunterStat.MaxHealth, "stat_health", new(0x6E, 0xC8, 0x7A)),
            ("DEFENSE", HunterStat.Defense, "stat_defense", new(0x8A, 0x96, 0xA8)),
            ("CRITICAL", HunterStat.CriticalChance, "stat_critical", new(0xF0, 0xA8, 0x30)),
            ("FOCUS", HunterStat.Focus, "stat_focus", new(0xE8, 0xC8, 0x7A)),
            ("GUILE", HunterStat.Guile, "stat_guile", new(0xF0, 0xB2, 0x4A)),
        };

        var y = TrainPanel.Y + 92;
        foreach (var (label, stat, iconKey, gem) in rows)
        {
            var row = new Rectangle(TrainPanel.X + 40, y - 8, TrainPanel.Width - 80, 54);
            var hovered = row.Contains(hit);
            _ui.Fill(b, row, hovered ? new Color(0x2A, 0x24, 0x38, 0xD0) : RowBg);

            var iconBox = new Rectangle(TrainPanel.X + 52, y - 1, 40, 40);
            if (!_ui.Icon(b, iconKey, iconBox, Color.White))
                _ui.Diamond(b, new Rectangle(iconBox.X + 7, iconBox.Y + 7, 26, 26), gem);

            _ui.TextBig(b, label, TrainPanel.X + 108, y + 8, Bone, UiTypography.Body);
            _ui.TextBig(b, $"RANK {hunter.RankOf(stat)} OF {hunter.StatRankCap}", TrainPanel.X + 300, y + 10, Slate, UiTypography.Secondary);

            var (effect, card) = Describe(stat, hunter, build, mods, shape);
            _ui.TextBig(b, effect, TrainPanel.X + 470, y + 10, Sky, UiTypography.Secondary);

            DrawTrain(b, hunter, stat, TrainPanel.Right - 216, y - 3, hit, clicked);

            if (hovered) SetHover(hit, label, card);
            y += 62;
        }
    }

    /// <summary>
    /// One stat's live headline and its plain-words hover card, with the player's real numbers filled in.
    /// </summary>
    /// <remarks>
    /// Every number is read from the same code path the fight executes — the code-path comments beside
    /// each case name it. Nothing here re-derives a formula; the worst bug this screen can have is
    /// stating a number the simulation does not use.
    /// </remarks>
    private (string Effect, string[] Card) Describe(HunterStat stat, Hunter hunter, Build build,
                                                    BuildMods mods, SkillShape shape)
    {
        switch (stat)
        {
            case HunterStat.AttackPower:
            {
                // Hunter.SquadDamageMultiplier = gear × (1 + 0.010 × MIGHT) × worn mods; the fight
                // multiplies every hit by mods.Damage (Build.Resolve → SoloBattle.Amp).
                var v = hunter.ValueOf(stat);
                return ($"YOUR HITS DEAL {mods.Damage:0.00}× DAMAGE", new[]
                {
                    "MIGHT is your attack. Every point of MIGHT adds 1% to the damage of every hit.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} MIGHT. You have {v:0} MIGHT, so your MIGHT alone gives +{v:0}% damage.",
                    $"With your weapon, keystones and tree counted in, your hits now deal {mods.Damage:0.00}× damage. This is the number the fight uses.",
                });
            }
            case HunterStat.ResonanceAffinity:
            {
                // ResonanceWeaving.BasePower: a skill's base power = FormBaseValue × (1 + 0.008 × RESONANCE),
                // read by SoloBattle through FormBehaviour.BaseDamage for every skill hit.
                var v = hunter.ValueOf(stat);
                var per = 100f * WeavingTuning.Default.SourceScalingCoefficient;
                return ($"SKILLS START +{per * v:0}% STRONGER", new[]
                {
                    $"RESONANCE makes every skill start from a bigger base. Every point adds {per:0.0}% to a skill's base power, before anything else multiplies it.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} RESONANCE. You have {v:0} RESONANCE.",
                    $"Your skills now start +{per * v:0}% above their base power.",
                });
            }
            case HunterStat.Engineering:
            {
                // Hunter.SquadSkillRate = worn focus × worn mods × (1 + 0.006 × TEMPO); the fight divides
                // every skill's waiting time by mods.SkillRate × shape.SkillRate (SoloBattle cooldowns).
                var rate = mods.SkillRate * shape.SkillRate;
                var v = hunter.ValueOf(stat);
                return ($"SKILLS COME BACK {rate:0.00}× AS FAST", new[]
                {
                    "TEMPO makes your skills come back sooner after they fire. Every point makes the wait 0.6% shorter.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} TEMPO. You have {v:0} TEMPO.",
                    $"With your gear and your build counted in, skills now come back {rate:0.00}× as fast. This is the number the fight uses.",
                });
            }
            case HunterStat.Vitality:
            {
                // SoloBattle: damage taken = bite ÷ mods.Health (then DEFENSE cuts it further).
                // Hunter.SquadHealthMultiplier = (1 + 0.012 × VITALITY) × worn charm × worn mods.
                var v = hunter.ValueOf(stat);
                // Toughness under 1 is a designed state (GLASS CANNON and friends): the double
                // negative "-78.6% less damage" read as a bug, so the words flip with the sign.
                var tough = MathF.Max(0.01f, mods.Health);
                var less = 100f * (1f - 1f / tough);
                var headline = tough >= 1f ? $"YOU TAKE {less:0.0}% LESS DAMAGE"
                                           : $"YOU TAKE {-less:0.0}% MORE DAMAGE";
                var tail = tough >= 1f
                    ? $"With gear and keystones counted in, your toughness is {tough:0.00}× — so you take {less:0.0}% less damage."
                    : $"With gear and keystones counted in, your toughness is {tough:0.00}× — so you take {-less:0.0}% MORE damage. A keystone or vow is trading your skin for power.";
                return (headline, new[]
                {
                    "VITALITY is toughness. Every bite an enemy lands is divided by your toughness number before it touches your life.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} VITALITY. You have {v:0} VITALITY, and every point adds 1.2% toughness.",
                    tail,
                });
            }
            case HunterStat.MaxHealth:
            {
                // SoloBattle.ChampionHealth: the pool a fight starts with = max(60, Hunter.MaxHealth)
                // × the build's health multiplier (vows and passives).
                var pool = SoloBattle.ChampionHealth(build, hunter);
                return ($"{pool:N0} LIFE IN A FIGHT", new[]
                {
                    $"HEALTH is the life your champion enters every fight with. One rank of training adds {hunter.GainPerRank(stat):0} health.",
                    $"With your charm counted in, your health is {hunter.MaxHealth:N0}. After your build's promises and passives, your champion starts a fight with {pool:N0} life.",
                    "When it reaches zero, the descent ends.",
                });
            }
            case HunterStat.Defense:
            {
                // SoloBattle: taken × 100 ÷ (100 + DEFENSE) — a curve that never reaches 100%.
                var k = SoloBattle.DefenseMitigationConstant;
                var less = 100f - 100f * (k / (k + hunter.Defense));
                return ($"{less:0.0}% LESS DAMAGE TAKEN", new[]
                {
                    "DEFENSE shrinks every hit you take. The rule: damage is multiplied by 100, then divided by 100 plus your DEFENSE.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} DEFENSE. With your gear you have {hunter.Defense} DEFENSE, so you take {less:0.0}% less damage.",
                    "Each new point helps a little less than the last, and it can never reach 100%.",
                });
            }
            case HunterStat.CriticalChance:
            {
                // SoloBattle.CritChance: trained + gear affixes + tree nodes, capped at 75%.
                var p = SoloBattle.CritChance(hunter, shape);
                return ($"{100f * p:0.0}% OF HITS ARE CRITICAL", new[]
                {
                    $"CRITICAL is your chance that a hit becomes a critical hit. One rank of training adds {hunter.GainPerRank(stat):0.00}% chance.",
                    $"With your gear and tree counted in, {100f * p:0.0}% of your hits are critical right now.",
                    $"The most it can ever be is {100f * SoloBattle.MaxCritChance:0}%. FOCUS decides how hard a critical hit lands.",
                });
            }
            case HunterStat.Focus:
            {
                // SoloBattle.CritMultiplier: 1.5 + 0.01 × FOCUS — FOCUS is the crit-DAMAGE stat.
                var m = SoloBattle.CritMultiplier(hunter);
                var v = hunter.ValueOf(stat);
                return ($"CRITICAL HITS DEAL {100f * m:0}% DAMAGE", new[]
                {
                    $"FOCUS makes your critical hits land harder. A critical hit deals {100f * SoloBattle.CritBaseMultiplier:0}% damage, plus 1% for every point of FOCUS.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} FOCUS. You have {v:0} FOCUS.",
                    $"Your critical hits now deal {100f * m:0}% damage. CRITICAL decides how often they happen.",
                });
            }
            default:   // HunterStat.Guile
            {
                // Hunter.HaulMultiplier = (1 + 0.010 × GUILE) × worn mods; SoloExpedition multiplies
                // every wave's Gleam payout by mods.Haul.
                var v = hunter.ValueOf(HunterStat.Guile);
                var pct = 100f * (mods.Haul - 1f);
                var sign = pct >= 0f ? "+" : "";
                return ($"{sign}{pct:0}% LOOT FROM EVERY WAVE", new[]
                {
                    "GUILE raises what a fight pays. Every point adds 1% to the Gleam every cleared wave hands over.",
                    $"One rank of training adds {hunter.GainPerRank(HunterStat.Guile):0} GUILE. You have {v:0} GUILE.",
                    $"With gear and keystones counted in, your waves now pay {sign}{pct:0}% loot.",
                });
            }
        }
    }

    /// <summary>
    /// One rank of one stat, priced. The cost is on the button because it changes every purchase.
    /// </summary>
    /// <remarks>
    /// Geometric growth means the tenth rank of a stat costs many times the first, so a player choosing
    /// where gleam goes is making a real decision — and a button that only said "TRAIN" would hide the
    /// entire decision behind a click.
    /// </remarks>
    private void DrawTrain(SpriteBatch b, Hunter hunter, HunterStat stat, int x, int y, Point mouse, bool clicked)
    {
        var maxed = hunter.RankOf(stat) >= hunter.StatRankCap;
        var cost = hunter.NextRankCost(stat);
        var rect = new Rectangle(x, y, 176, 44);
        var afford = hunter.CanTrain(stat);

        var label = maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} GLEAM" : $"NEED  {cost:N0} GLEAM";

        if (_ui.Button(b, rect, label, mouse, clicked, enabled: afford))
            _trainRequest = stat;
    }

    /// <summary>
    /// RESET ALL TRAINING — the respec bar. Refunds 100% of the Gleam the ranks cost; costs 1 Crystal.
    /// </summary>
    /// <remarks>
    /// The refund figure is on the button because it IS the decision, and it comes from
    /// <see cref="Hunter.TrainingRefund"/> — the sum of the real geometric rank costs — never from a
    /// tracked total. Two clicks like START A NEW GAME: first arms red, second confirms, any other
    /// click disarms. When the Crystal is missing, or nothing is trained, the button says so plainly
    /// and refuses.
    /// </remarks>
    private void DrawReset(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        if (_devArmPending) { _resetArmed = true; _resetArmedAtMs = Environment.TickCount64; _devArmPending = false; }

        _ui.Fill(b, ResetBar, RowBg);
        _ui.Fill(b, new Rectangle(ResetBar.X, ResetBar.Y, ResetBar.Width, 2), Dim);

        var refund = hunter.TrainingRefund();
        var crystals = hunter.MaterialOf(Material.Crystal);
        var price = hunter.TrainingResetCrystalCost;

        _ui.TextBig(b, "RESET", ResetBar.X + 40, ResetBar.Y + 24, Bone, UiTypography.Body);
        _ui.TextBig(b, $"YOU HAVE {crystals:N0} CRYSTAL", ResetBar.X + 40, ResetBar.Y + 54, Slate, UiTypography.Secondary);

        var button = new Rectangle(ResetBar.Right - 40 - 960, ResetBar.Y + 25, 960, 56);

        if (_resetArmed && Environment.TickCount64 - _resetArmedAtMs > (long)(ArmSeconds * 1000))
            _resetArmed = false;   // the settings pattern this mirrors auto-disarms; so does this now
        if (_resetArmed)
        {
            // The settings panel's armed pattern, exactly: red ground, warning stripe, white text.
            _ui.Fill(b, button, ArmedRed);
            _ui.Fill(b, new Rectangle(button.X, button.Y, button.Width, 3), Ember);
            _ui.TextCenterBig(b, $"SURE? ALL RANKS GO BACK TO ZERO AND {refund:N0} GLEAM COMES BACK — CLICK AGAIN",
                              button.Center.X, button.Center.Y - 10, Color.White, UiTypography.Body, TextFace.Strong);
            if (UiKit.ClickedIn(button, hit, clicked))
            {
                _resetArmed = false;
                _resetRequest = true;
            }
            else if (clicked)
            {
                _resetArmed = false;   // any click that is not the confirmation disarms
            }
            return;
        }

        // The three honest states: nothing to reset, missing the Crystal, or ready.
        if (refund <= 0)
        {
            _ui.Button(b, button, "NOTHING TO RESET — NO TRAINING BOUGHT YET", hit, clicked, enabled: false);
        }
        else if (crystals < price)
        {
            _ui.Button(b, button, $"RESET NEEDS {price:N0} CRYSTAL — YOU HAVE {crystals:N0}", hit, clicked, enabled: false);
        }
        else if (_ui.Button(b, button,
                            $"RESET ALL TRAINING — RETURNS {refund:N0} GLEAM · COSTS {price:N0} CRYSTAL",
                            hit, clicked))
        {
            _resetArmed = true;
            _resetArmedAtMs = Environment.TickCount64;
        }
    }

    private void DrawProgression(SpriteBatch b, Hunter hunter, Point hit)
    {
        _ui.PanelQuiet(b, ProgressPanel);
        _ui.TextCenterBig(b, "PROGRESS", ProgressPanel.Center.X, ProgressPanel.Y + 22, Gold, UiTypography.SectionTitle);
        // Only the counters the game actually tracks — monsters/bosses/play-time/deaths aren't recorded.
        var rows = new (string, string)[]
        {
            ("HUNTER LEVEL", $"{hunter.HunterLevel}"),
            ("HIGHEST WAVE", $"{HighestWave}"),
            ("CHESTS OPENED", $"{ChestsOpened:N0}"),
            ("MASTERY POINTS", $"{MasteryPoints:N0}"),
        };
        var y = ProgressPanel.Y + 74;
        foreach (var (label, value) in rows)
        {
            var row = new Rectangle(ProgressPanel.X + 40, y - 6, ProgressPanel.Width - 80, 40);
            if (label == "HUNTER LEVEL" && row.Contains(hit))
                SetHover(hit, "LEVEL", LevelCard(hunter));
            _ui.TextBig(b, label, ProgressPanel.X + 40, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, value, ProgressPanel.Right - 40, y, Bone, UiTypography.Body);
            _ui.Fill(b, new Rectangle(ProgressPanel.X + 40, y + 36, ProgressPanel.Width - 80, 2), Dim * 0.5f);
            y += 48;
        }
    }

    /// <summary>
    /// The honest answer to "what does the character level mean?" (playtest item 8).
    /// </summary>
    /// <remarks>
    /// Investigated before writing: <c>Hunter.HunterLevel</c> is <c>1 + total ranks ÷ 5</c> and its own
    /// doc comment says "Cosmetic only — never a gate". Every consumer is a display (this screen, the
    /// character screen, the fight HUD, the build summary, the boot log). It feeds NO formula, so this
    /// card says exactly that rather than inventing a mechanic the game does not have.
    /// </remarks>
    private static string[] LevelCard(Hunter hunter) => new[]
    {
        "LEVEL shows how far your training has come. Every five ranks of training make one level.",
        $"You have level {hunter.HunterLevel}. It does not change the fight — it is a medal, not a stat.",
        "If you reset your training, your level goes back with it.",
    };

    private void SetHover(Point at, string title, string[] body)
    {
        _hoverTitle = title;
        _hoverBody = body;
        _hoverAt = at;
    }

    /// <summary>
    /// The plain-words card for the hovered row: what the stat does, the exact rule, the live numbers.
    /// </summary>
    /// <remarks>
    /// Drawn like <see cref="ItemTooltip"/>: its own dark ground rather than a panel frame, flipped to
    /// the pointer's other side near the right edge, and clamped to the canvas so it can never run off.
    /// </remarks>
    private void DrawHoverCard(SpriteBatch b)
    {
        if (_hoverTitle is null || _hoverBody is null) return;

        const int w = 640;
        const int lineH = 26;
        var wrapped = new System.Collections.Generic.List<string>();
        var breaks = new System.Collections.Generic.List<int>();   // index of each paragraph's last line
        foreach (var para in _hoverBody)
        {
            foreach (var line in _ui.WrapBig(para, w - 48, UiTypography.Secondary)) wrapped.Add(line);
            breaks.Add(wrapped.Count - 1);
        }
        var h = 62 + wrapped.Count * lineH + (breaks.Count - 1) * 10 + 20;

        // FLIP, don't clamp: a card pinned to the edge sits on the row it is explaining.
        var x = _hoverAt.X + 28 + w <= 1900 ? _hoverAt.X + 28 : _hoverAt.X - w - 28;
        var y = Math.Clamp(_hoverAt.Y - 24, 150, Math.Max(150, 1060 - h));
        var card = new Rectangle(x, y, w, h);

        _ui.Fill(b, new Rectangle(card.X + 4, card.Y + 4, card.Width, card.Height), new Color(0, 0, 0, 140));
        _ui.Fill(b, card, new Color(0x0E, 0x0C, 0x14, 0xF2));
        _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 3), Gold);
        _ui.Fill(b, new Rectangle(card.X, card.Bottom - 3, card.Width, 3), Gold * 0.4f);
        _ui.Fill(b, new Rectangle(card.X, card.Y, 3, card.Height), Gold * 0.4f);
        _ui.Fill(b, new Rectangle(card.Right - 3, card.Y, 3, card.Height), Gold * 0.4f);

        _ui.TextBig(b, _hoverTitle, card.X + 24, card.Y + 18, Gold, UiTypography.PanelTitle);

        var ty = card.Y + 56;
        for (var i = 0; i < wrapped.Count; i++)
        {
            _ui.TextBig(b, wrapped[i], card.X + 24, ty, Bone, UiTypography.Secondary);
            ty += lineH;
            if (breaks.Contains(i)) ty += 10;
        }
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { HunterCard, TrainPanel, ResetBar, ProgressPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, "nav STATS  merged training + respec", 60, 112, Gold, UiTypography.Secondary);
    }

    private static string FormShort(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };
}
