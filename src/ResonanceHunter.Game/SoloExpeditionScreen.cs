using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The idle heart of the game: your champion clears the region ON ITS OWN, forever, and you watch.
/// </summary>
/// <remarks>
/// <para>
/// There is no bank-or-push and no muster gate any more. The champion fights wave after wave
/// automatically; every wave it clears pays out on the spot; when it finally falls it picks itself up
/// and starts the region again. The player never clicks a fight. All the decisions live elsewhere — the
/// BUILD (press B), the Forge, the tree — and this screen is where you see whether they were good ones.
/// </para>
/// <para>
/// It drives <see cref="SoloExpedition"/> on a timer and hands each cleared wave's haul to the host via a
/// small reward queue, so the host credits gleam, cores and loot as they are earned rather than at a
/// run's end that no longer exists.
/// </para>
/// </remarks>
public sealed class SoloExpeditionScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x2C, 0x2C, 0x36);
    private static readonly Color Bloom = new(0x8A, 0x6A, 0xB0);
    private static readonly Color Steel = new(0x7A, 0x9A, 0xC0);
    private static readonly Color Verdant = new(0x5A, 0x9A, 0x4A);
    private static readonly Color Shadow = new(0x10, 0x0E, 0x14);
    private static readonly Color PanelBg = new(0x14, 0x11, 0x1A, 0xC8);
    private static readonly Color GroundShade = new(0x00, 0x00, 0x00, 0x64);   // soft translucent contact shadow
    private static readonly Color OnScene = UiKit.Vellum;

    private const int GroundY = 735;   // spec §11.4 — the ground line every actor stands on
    // A wave is resolved instantly then REPLAYED at this speed. It was 3.5x with no gap between waves, so
    // the whole run blurred past — playtest: "waves flow too fast". Slowed to a watchable pace, and a short
    // BREATH now sits between waves so each clear reads as its own beat. The champion's own skill rate
    // (TEMPO, the Focus slot) still speeds combat back up on top of this, so the upgrade is now visible.
    private const float PlaybackSpeed = 2.0f;   // default battle speed
    private float _speedMul = PlaybackSpeed;     // player-adjustable via the HUD's BATTLE SPEED buttons (x1/x2/x4/x8)
    private static readonly float[] SpeedSteps = { 1f, 2f, 4f, 8f };
    private const float WaveBreakSeconds = 0.8f;   // the pause after a clear, before the next enemy is fought
    private const float DownedSeconds = 1.6f;   // the recovery beat before the champion tries again

    // Spec §12: the hunter is the DOMINANT figure, bottom-centred at (560,735), ~390px tall; the enemy grounds
    // at the front-melee anchor (1110,750), smaller (~250px) so the hunter reads as the focal point. The old
    // layout over-sized the enemy/boss ("boss too big, masked in a box") — the spec's ranges fix that.
    private static readonly Rectangle ChampBox = new(560 - 180, 735 - 390, 360, 390);
    // Rev 3 §16.1: one normal enemy bottom-centred at (1160,735), visible ~320px (range 280–360). A boss is
    // drawn far larger from its own anchor (see the draw), so this box is the NORMAL-enemy size only.
    private static readonly Rectangle EnemyBox = new(1160 - 175, 735 - 340, 350, 340);
    private static readonly Rectangle BossBox = new(1210 - 220, 750 - 540, 440, 540);   // §16.5: (1210,750), ~510px tall

    // package_03: one representative common enemy per Source (no Nature enemy shipped — a wisp stands in).
    private static readonly Dictionary<Source, string> EnemyForSource = new()
    {
        [Source.Body] = "bonecrawler", [Source.Mind] = "soul_leech", [Source.Nature] = "wisp",
        [Source.Machine] = "stone_sentinel", [Source.Shadow] = "shadeling", [Source.Spirit] = "rift_guardian",
    };
    // package_04: the six region bosses, matched to region theme.
    private static readonly Dictionary<string, string> BossForRegion = new()
    {
        ["verdant_hollow"] = "thorn_regent", ["cinderworks"] = "forge_colossus", ["umbral_reach"] = "void_reaper",
        ["still_archive"] = "crystal_lich", ["pale_choir"] = "lumen_angel", ["marrow_wastes"] = "spirit_matron",
    };

    private readonly UiKit _ui;
    private readonly VfxPlayer _vfx;
    private readonly Random _rng = new();

    private enum Mode { Fighting, Downed }
    private Mode _mode = Mode.Fighting;

    private SoloExpedition? _run;
    private Champion? _champ;
    private float _anim;
    private float _downedTimer;
    private float _breakTimer;   // the between-wave breath; while >0 the cleared frame holds, then the next wave begins

    // Replay state.
    private float _playheadMs;
    private WaveReplay? _replay;
    private float _champLunge, _enemyLunge, _enemyWindup;
    private int _nextEnemyStrikeMs;
    private float _enemyBaseHealth = 120f, _enemyBaseDamage = 9f;
    private WaveOutcome _outcome = WaveOutcome.Cleared;

    // Feedback so a WATCHED fight actually reads: a wave-cleared banner, the next enemy sliding IN from
    // the right, and a red flash when the champion falls. Without these, waves passed silently and the
    // playtest note was exactly that — "I can't tell what's happening".
    private float _enemyEnter;    // 1 → 0: how far off-screen-right the new enemy still is
    private float _bannerTimer;   // 1.2 → 0: the "WAVE N CLEARED" flash
    private string _bannerText = "";
    private float _deathFlash;    // 1 → 0: red vignette on a fall
    private string _enemyArt = "";

    /// <summary>Deepest wave reached in this region, across restarts — what conquest is measured against.</summary>
    public int Deepest { get; private set; }

    private readonly List<Callout> _callouts = new();
    private struct Callout { public string Text; public Color Color; public int X, Y; public float Life; public int Px; }
    private int _strikeCount;   // throttles per-strike damage numbers so they don't flood

    /// <summary>The player's build choices and the tree that powers them. Set by the host each frame.</summary>
    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MemoryDustTree Tree { get; set; } = new();
    public MasteryTree Mastery { get; set; } = new();
    public Source? EnemySource { get; set; }
    /// <summary>The active region id — picks the boss creature (boss_&lt;region&gt;) on boss waves. Set by the host.</summary>
    public string RegionId { get; set; } = "";
    /// <summary>The active region's combat character — handed to the run so the enemy's bite tempo matches it.</summary>
    public AttackBias EnemyBias { get; set; } = AttackBias.Balanced;
    /// <summary>Unopened chests waiting in the Forge — drives the "you have things to do" nudge. Set by the host.</summary>
    public int ChestCount { get; set; }
    /// <summary>Gleam per second the idle champion earns — shown in the HUNT idle-rewards panel. Set by the host.</summary>
    public float IdleGleamRate { get; set; }
    private Source? _lastSource;
    private Hunter? _hunter;

    public SoloExpeditionScreen(UiKit ui)
    {
        _ui = ui;
        _vfx = new VfxPlayer(ui.Assets);
    }

    // ── Reward channel: one entry per cleared wave, drained by the host ─────────────────────────────
    public readonly record struct WaveReward(Haul Haul, int Wave, bool IsBoss);
    private readonly Queue<WaveReward> _rewards = new();
    public bool HasReward => _rewards.Count > 0;
    public WaveReward TakeReward() => _rewards.Dequeue();

    /// <summary>What each Form looks like when it fires — the fight is watched, so the effect is the read.</summary>
    private static (string Text, Color Color) CalloutFor(Form form) => form switch
    {
        Form.Strike => ("STRIKE", Ember),
        Form.Projectile => ("VOLLEY", Steel),
        Form.Aura => ("AURA", Bloom),
        Form.Trap => ("TRAP", Gold),
        Form.Mark => ("MARK", Bone),
        _ => ("MORPH", Verdant),
    };

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Update(GameTime time, KeyboardState keys, Point mouse, bool clicked, int wheel,
        Hunter hunter, float enemyBaseHealth, float enemyBaseDamage)
    {
        _hunter = hunter;
        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        _anim += dt;
        _champLunge = Math.Max(0f, _champLunge - dt * 5f);
        _enemyLunge = Math.Max(0f, _enemyLunge - dt * 5f);
        _enemyEnter = Math.Max(0f, _enemyEnter - dt * 2.5f);   // the new enemy slides in over ~0.4s
        _bannerTimer = Math.Max(0f, _bannerTimer - dt);
        _deathFlash = Math.Max(0f, _deathFlash - dt * 1.5f);
        _vfx.Update(dt);
        for (var i = 0; i < _callouts.Count; i++) { var c = _callouts[i]; c.Life -= dt * 1.6f; _callouts[i] = c; }
        _callouts.RemoveAll(c => c.Life <= 0f);

        // Travelling to a new region (its element changes) restarts the champion there, fresh.
        if (EnemySource != _lastSource)
        {
            _lastSource = EnemySource;
            _run = null;
            Deepest = 0;
        }

        // Keep the region's difficulty current; it is read the next time a run (re)starts.
        _enemyBaseHealth = enemyBaseHealth;
        _enemyBaseDamage = enemyBaseDamage;

        if (_run is null) StartRun(hunter);

        // Stat training moved to the CHARACTER screen (press C); this is a pure idle-watch view now.

        switch (_mode)
        {
            case Mode.Fighting: UpdateFight(dt); break;
            case Mode.Downed:
                _downedTimer -= dt;
                if (_downedTimer <= 0f) StartRun(hunter);
                break;
        }
    }

    private void StartRun(Hunter hunter)
    {
        var build = Loadout.ToBuild(Tree, Mastery);
        // RECKLESS OFFERING's price: a smaller pool for the whole run, charged once here at mint.
        var hp = Math.Max(1, (int)MathF.Round(Math.Max(60, hunter.MaxHealth) * SoloBattle.VowHealthMultiplier(build)));
        _champ = new Champion { MaxHealth = hp, Health = hp };
        _run = new SoloExpedition(build, _champ, hunter, _enemyBaseHealth, _enemyBaseDamage,
            ExpeditionTuning.Default, EnemySource, _rng) { EnemyBias = EnemyBias };
        _mode = Mode.Fighting;
        BeginWave();
    }

    private void BeginWave()
    {
        if (_run is null || _champ is null) return;

        var startHealth = new Dictionary<int, int> { [0] = _champ.Health };
        var maxHealth = new Dictionary<int, int> { [0] = _champ.MaxHealth };
        var enemyHp = _enemyBaseHealth * WaveScaling.EnemyScale(_run.Wave + 1, ExpeditionTuning.Default);

        _outcome = _run.PushWave();

        _replay = new WaveReplay(_run.LastWaveEvents, startHealth, maxHealth, enemyHp);
        _playheadMs = 0f;
        _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(0f);
        _callouts.Clear();
    }

    /// <summary>The host rolled a chest for the boss just felled — upgrade the banner to the reward beat.</summary>
    /// <remarks>Called same-frame as the boss-down banner, so "CHEST DROPPED!" replaces "BOSS DOWN!" cleanly.</remarks>
    public void FlashChest()
    {
        _bannerText = "BOSS DOWN — CHEST DROPPED!  (F — FORGE)";
        _bannerTimer = 2.4f;
    }

    private void Say(string text, Color color, int yOffset = -40)
    {
        var stack = _callouts.Count(c => c.Life > 0.6f);
        _callouts.Add(new Callout { Text = text, Color = color, X = ChampBox.Center.X, Y = ChampBox.Y + yOffset - stack * 36, Life = 1f, Px = 36 });
    }

    /// <summary>A floating combat number over the enemy — the fight's "action" read (package_10). Scaled off
    /// the hunter's real PowerRating, jittered so numbers don't stack; crits are gold and linger.</summary>
    private void SpawnDamage(int amount, bool crit)
    {
        _callouts.Add(new Callout
        {
            Text = crit ? $"-{amount:N0} CRIT" : $"-{amount:N0}",
            Color = crit ? Gold : Bone,
            X = EnemyBox.Center.X + Jitter((int)_playheadMs, 104),
            Y = EnemyBox.Y + 40 - Jitter((int)_playheadMs + 11, 32),
            Life = crit ? 1.3f : 1f,
            Px = crit ? 72 : 52,
        });
    }

    private int HitDamage(float mult)
    {
        var pwr = Math.Max(1, _hunter?.PowerRating ?? 100);
        var j = 0.85f + (Jitter((int)_playheadMs, 30) + 30) / 200f;   // ~0.85..1.15 spread
        return Math.Max(1, (int)(pwr * mult * j));
    }

    private void UpdateFight(float dt)
    {
        if (_run is null || _replay is null) return;

        // The between-wave breath: hold on the cleared frame for a beat, THEN walk into the next wave. This
        // also gates re-entry — the just-finished replay stays "finished", so the clear payout below fires
        // exactly once.
        if (_breakTimer > 0f)
        {
            _breakTimer -= dt;
            if (_breakTimer <= 0f) { BeginWave(); _enemyEnter = 1f; }
            return;
        }

        // Hold the fight until the new enemy has finished sliding in — otherwise the champion swings at empty
        // air while the enemy is still off to the right ("hunter hits before the enemy arrives").
        if (_enemyEnter > 0f) return;

        _playheadMs += dt * 1000f * _speedMul;

        var lead = _nextEnemyStrikeMs - _playheadMs;
        _enemyWindup = lead is > 0 and < 600 ? 1f - lead / 600f : 0f;

        foreach (var e in _replay.Advance(_playheadMs))
        {
            switch (e.Kind)
            {
                case BattleEventKind.Strike:
                    // Just the lunge. A hit-spark on EVERY strike (skills AND auto-attacks fire these
                    // constantly) was the "too many red slashes" the playtest flagged — loudness has to be
                    // budgeted against importance, so the loud VFX are reserved for the SKILL casts below.
                    _champLunge = 1f;
                    if ((_strikeCount++ & 1) == 0) SpawnDamage(HitDamage(1f), false);   // every other auto-hit
                    break;
                case BattleEventKind.EnemyStrike:
                    _enemyLunge = 1f;
                    _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(e.AtMs);
                    _vfx.Play("vfx_hit", ChampBox.Center.X, ChampBox.Center.Y, tint: Ember);
                    break;
                case BattleEventKind.Skill:
                    var form = (Form)e.Amount;
                    var (text, colour) = CalloutFor(form);
                    Say(text, colour);
                    PlayFormVfx(form);
                    SpawnDamage(HitDamage(form == Form.Trap ? 3f : 2f), form == Form.Trap);   // skills hit big
                    break;
                case BattleEventKind.Heal:
                    Say($"+{e.Amount}", Verdant, -8);
                    _vfx.Play("vfx_levelup", ChampBox.Center.X, ChampBox.Y + 32, tint: Verdant);
                    break;
                case BattleEventKind.Shield:
                    Say("UNDYING", Gold);
                    _vfx.Play("vfx_interrupt", ChampBox.Center.X, ChampBox.Center.Y, tint: Gold);
                    break;
                case BattleEventKind.Down:
                    _vfx.Play("vfx_death", ChampBox.Center.X, ChampBox.Center.Y, fps: 14f);
                    break;
                case BattleEventKind.EnemyDown:
                    _vfx.Play("vfx_death", EnemyBox.Center.X, EnemyBox.Center.Y, scale: 2, fps: 14f);
                    break;
            }
        }

        if (!_replay.Finished) return;

        if (_outcome == WaveOutcome.Cleared)
        {
            // Pay this wave out NOW, then walk straight into the next one — no boundary, no button.
            _rewards.Enqueue(new WaveReward(_run.LastWaveHaul, _run.Wave, _run.LastWaveWasBoss));
            if (_run.Wave > Deepest) Deepest = _run.Wave;

            // Announce the clear and slide the NEXT enemy in, so the wave boundary is something you SEE.
            // A boss falling is the reward beat — it dropped a chest — so it gets its own louder banner
            // that lingers a touch longer. You FEEL the earn at the kill, then go crack it in the Forge.
            // A chest is no longer a given, so the boss banner no longer promises one — the host calls
            // FlashChest() and upgrades this banner only when a chest actually drops.
            _bannerText = _run.LastWaveWasBoss ? "BOSS DOWN!" : $"WAVE {_run.Wave} CLEARED";
            _bannerTimer = _run.LastWaveWasBoss ? 1.6f : 1.2f;
            // Take a breath on the clear, THEN begin the next wave (see the break gate atop UpdateFight). A
            // boss's fall lingers a touch longer — it dropped a chest, and that beat should land.
            _breakTimer = _run.LastWaveWasBoss ? WaveBreakSeconds + 0.5f : WaveBreakSeconds;
        }
        else
        {
            // Fell (or stalled). A red flash, a short breath, then the champion regroups.
            _deathFlash = 1f;
            _mode = Mode.Downed;
            _downedTimer = DownedSeconds;
        }
    }

    private void PlayFormVfx(Form form)
    {
        switch (form)
        {
            case Form.Strike:
                _vfx.Play("vfx_ability_ruinstrike", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 22f, tint: Ember);
                break;
            case Form.Trap:
                _vfx.Play("vfx_crit", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 20f, tint: Gold);
                break;
            case Form.Mark:
                _vfx.Play("vfx_interrupt", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 20f, tint: Bone);
                break;
            case Form.Transformation:
                _vfx.Play("vfx_levelup", ChampBox.Center.X, ChampBox.Y + 24, fps: 18f, tint: Verdant);
                break;
            default:
                _vfx.Play("vfx_weakhit", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 18f, tint: Steel);
                break;
        }
    }

    private static int Jitter(int seed, int spread) => (int)(seed * 2654435761L % (spread * 2 + 1)) - spread;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, Point mouse, bool clicked, string regionName, string enemyArt = "", bool suppressBanner = false)
    {
        _enemyArt = enemyArt;
        // This screen authors in true 1920 coords at canvas scale 1, so the VFX overlay draws at scale 1 too.
        _vfx.Scale = 1;
        // The host hands us the mouse in 480-logical space; lift it into this screen's 1920 space so every
        // hit-test below (the only clickables are the BATTLE SPEED buttons) lands on the drawn rects.
        var hit = new Point(mouse.X * 4, mouse.Y * 4);
        if (_run is null || _replay is null || _champ is null) return;

        // The top HUD, stage header, resource bar and hunt-log panel are drawn as overlays AFTER the arena.

        // ── The enemy (Rev 3 §16-17): a normal enemy grounds at (1160,735) ~320px; a boss is far larger from
        // its own anchor (1210,750) and gets a dedicated TOP-OF-ARENA bar, never an overhead one. ─────────
        var isBossWave = WaveScaling.IsBossWave(_run.Wave + 1, ExpeditionTuning.Default);
        var elunge = (int)(_enemyLunge * -40f);
        var enter = (int)(_enemyEnter * 280f);   // starts right of home and slides in
        var baseBox = isBossWave ? BossBox : EnemyBox;
        var ebox = new Rectangle(baseBox.X + elunge + enter, baseBox.Y, baseBox.Width, baseBox.Height);
        _ui.Fill(b, new Rectangle(ebox.X + 60, ebox.Bottom - 14, ebox.Width - 120, 14), GroundShade);

        // package_02A/03A/04A flipbook strips — idle loops; the attack/slam plays during the wind-up
        // telegraph. Falls back to the package_03/04 static key poses for enemies/bosses without a strip.
        var bob = (int)(MathF.Sin(_anim * 2f) * 8f);
        var attacking = _enemyWindup > 0f;
        var crop = isBossWave ? 0.30f : 0.08f;   // boss frames carry a smoke STREAK across the top — trim it
        var figTop = ebox.Bottom - ebox.Height;   // stable figure top (no bob) — anchors the wind-up ring
        var ab = new Rectangle(ebox.X, figTop + bob, ebox.Width, ebox.Height);

        string? stripKey = null, staticKey = null;
        var fps = attacking ? 16f : isBossWave ? 10f : 12f;
        var loop = !attacking;
        if (isBossWave && BossForRegion.TryGetValue(RegionId, out var boss))
        {
            var act = attacking ? "attack" : "idle";
            stripKey = $"{boss}_{act}_strip8_1024";
            staticKey = $"{boss}_{act}_1024";
        }
        else if (EnemySource is { } es && EnemyForSource.TryGetValue(es, out var en))
        {
            var act = attacking ? en == "stone_sentinel" ? "slam" : "attack" : "idle";
            stripKey = $"{en}_{act}_strip8_512";
            staticKey = attacking ? $"{en}_attack_01" : $"{en}_idle_01";
        }

        if (stripKey is null || !_ui.AnimSprite(b, stripKey, ab, _anim, fps, loop, Color.White, crop))
        {
            var etorso = staticKey is not null ? _ui.Assets.Get(staticKey) : null;
            if (etorso is not null)
            {
                var cropY = (int)(etorso.Height * crop);
                var srcH = etorso.Height - cropY;
                var sc = ab.Height / (float)srcH;
                var w = Math.Max(1, (int)(etorso.Width * sc));
                b.Draw(etorso, new Rectangle(ab.Center.X - w / 2, ab.Bottom - ab.Height, w, ab.Height),
                    new Rectangle(0, cropY, etorso.Width, srcH), Color.White);
            }
            else _ui.Fill(b, new Rectangle(ebox.X + 40, ebox.Y + 40, ebox.Width - 80, ebox.Height - 80), Ember);
        }

        if (_enemyWindup > 0f)
        {
            var r = (int)(16 + _enemyWindup * 32);
            Outline(b, new Rectangle(ab.X - r, figTop - r, ab.Width + r * 2, ebox.Height + r * 2), Ember, 4);
        }

        if (isBossWave)
        {
            // Dedicated top-of-arena boss bar (§17.3) — the boss gets NO overhead bar.
            _ui.TextCenterBig(b, "BOSS", 960, 158, Gold, 24);
            _ui.BarArt(b, new Rectangle(510, 184, 900, 30), _replay.EnemyHealthFraction, "boss");
        }
        else
        {
            // Normal enemy: a QUIET minimal bar (§17.1), 130×12, ~18px above the figure — no ornate frame.
            var ebar = new Rectangle(ebox.Center.X - 65, figTop - 24, 130, 12);
            _ui.Fill(b, ebar, new Color(0x0D, 0x0B, 0x14, 0xDC));
            var fw = (int)(ebar.Width * Math.Clamp(_replay.EnemyHealthFraction, 0f, 1f));
            if (fw > 0) _ui.Fill(b, new Rectangle(ebar.X, ebar.Y, fw, ebar.Height), Ember);
            _ui.Fill(b, new Rectangle(ebar.X, ebar.Y, ebar.Width, 2), new Color(0, 0, 0, 0x50));
        }

        // ── The champion (arena left). Name/HP now live in the top-left HUD. ──
        var push = (int)(_champLunge * 40f);
        var cbox = new Rectangle(ChampBox.X + push, ChampBox.Y, ChampBox.Width, ChampBox.Height);
        if (_replay.IsShielded(0)) Outline(b, new Rectangle(cbox.X - 4, cbox.Y - 4, cbox.Width + 8, cbox.Height + 8), Steel, 4);
        DrawChampion(b, cbox, dead: _mode == Mode.Downed);

        _vfx.Draw(b);
        DrawCallouts(b);

        // A red wash over the whole frame the instant the champion falls — you can't miss the death.
        if (_deathFlash > 0f) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Ember * (_deathFlash * 0.35f));

        // The wave-cleared banner: a gold flash above the fight that fades as the next wave opens. Rev 3 §23:
        // only one major overlay at a time — the welcome-back toast (higher priority) suppresses this.
        if (!suppressBanner && _bannerTimer > 0f)
        {
            var fade = Math.Clamp(_bannerTimer * 1.4f, 0f, 1f);
            _ui.TextCenterBig(b, _bannerText, 960, 268, Gold * fade, 40);   // spec §18.2 — centred in the arena, clear of the header
        }

        // ── package_10 HUD + panels, drawn over the arena. ──
        DrawHunterHud(b);
        DrawStageHeader(b, regionName, isBossWave);
        DrawRightColumn(b);
        DrawSkillDock(b);
        DrawBattleControls(b, hit, clicked);

        if (_mode == Mode.Downed)
        {
            _ui.Fill(b, new Rectangle(430, 470, 800, 130), PanelBg);
            _ui.TextCenterBig(b, "CHAMPION DOWN — REGROUPING", 830, 496, Ember, 26);
            _ui.TextCenterBig(b, $"REACHED WAVE {_run.Wave + 1}. STRENGTHEN THE BUILD (B).", 830, 544, Slate, 18);
        }

        // (The navigation bar is the shared hex nav the host draws over every screen.)
    }

    private const int ConquerAt = 7;   // mirrors Game1.ConquerWaveDepth — shown so the goal is visible

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x9A, 0x7A, 0xD8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };
    private static string FormShort(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };

    /// <summary>Top-left hunter HUD (package_10 region A): portrait medallion, name/level, HP, power, and a
    /// row of Source icons for the build's elements.</summary>
    private void DrawHunterHud(SpriteBatch b)
    {
        // Spec §8: player summary, exact rectangles. Ornate primary frame; portrait AspectFit (it already
        // carries its own frame — no second medallion); name/level/power/HP/tempo/source-icons per §8.3.
        var panel = new Rectangle(24, 20, 420, 205);
        _ui.Panel(b, panel);

        var por = new Rectangle(42, 39, 104, 104);
        if (_ui.Assets.Get("hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        else if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, por, Color.White);

        var adept = Mastery.MasteryForm() is { } mf ? $"{FormShort(mf)} ADEPT" : "SEEKER";
        _ui.TextBig(b, adept, 162, 43, Bone, 26);                                   // name
        _ui.TextBig(b, $"LV {_hunter?.HunterLevel ?? 1}", 162, 80, Gold, 20);        // level
        // Combat power — an icon + value (spec: an icon, not a "PWR" label).
        if (_ui.Assets.Get("state_resonance_128") is { } pi) b.Draw(pi, new Rectangle(268, 74, 30, 30), Ember);
        _ui.TextBig(b, Game1.Abbrev(_hunter?.PowerRating ?? 0), 304, 76, Ember, 24);
        // HP bar (162,112,220,24), value centred on it.
        var hp = Math.Max(0, _replay?.HealthOf(0) ?? 0);
        var hpBar = new Rectangle(162, 112, 220, 24);
        _ui.BarArt(b, hpBar, _replay?.HealthFractionOf(0) ?? 1f, "health");
        _ui.TextCenterBig(b, $"{hp}/{_champ?.MaxHealth ?? 0}", hpBar.Center.X, hpBar.Y + 3, Bone, 16);
        // Secondary stat line — TEMPO × skill rate (spec §8.6: no fake mana bar; real SquadSkillRate).
        _ui.TextBig(b, $"TEMPO {(_hunter?.SquadSkillRate ?? 1f):0.00}x SKILL RATE", 162, 145, Slate, 15);

        // Source icons (42,160,330,32) — the build's real elements.
        var sx = 42;
        foreach (var s in Loadout.Skills)
        {
            var box = new Rectangle(sx, 162, 32, 32);
            if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g) b.Draw(g, box, Color.White);
            else _ui.Diamond(b, box, SourceColor.GetValueOrDefault(s.Source, Slate));
            sx += 40;
        }
    }

    /// <summary>Top-center stage header (region B): region name, current wave, and the conquest progress bar.</summary>
    private void DrawStageHeader(SpriteBatch b, string regionName, bool isBossWave)
    {
        // Rev 3 §12: stage header (630,18,560,135) — narrower, so it clears the currency bar (≥20px gap). One
        // clean hierarchy region → depth → progress → wave, all centred at x=910 (the banner centre).
        var bar = new Rectangle(630, 18, 560, 135);
        if (_ui.Assets.Get("ui_panel_modal_wide") is { } bg) b.Draw(bg, bar, Color.White);
        else _ui.Panel(b, bar);
        const int cx = 910;
        _ui.TextCenterBig(b, Ellipsize(regionName.ToUpperInvariant(), 490, 36), cx, 32, Gold, 36);
        _ui.TextCenterBig(b, Deepest >= ConquerAt ? "CONQUERED" : $"DEPTH {Deepest} / {ConquerAt}",
            cx, 73, Deepest >= ConquerAt ? Gold : Bone, 24);
        _ui.BarArt(b, new Rectangle(710, 102, 400, 18),
            ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f, "progress");
        _ui.TextCenterBig(b, $"WAVE {_run!.Wave + 1}", cx, 124, isBossWave ? Gold : Bone, 22);
    }

    /// <summary>Clamp text to a pixel width at the given size, adding an ellipsis (spec §12.4 / §25.1).</summary>
    private string Ellipsize(string s, int maxPx, int px)
    {
        if (_ui.MeasureBig(s, px) <= maxPx) return s;
        while (s.Length > 1 && _ui.MeasureBig(s + "…", px) > maxPx) s = s[..^1];
        return s + "…";
    }

    private static readonly Color PlateEdge = new(0x74, 0x62, 0x3E);

    /// <summary>A titled panel using the ORNATE ui_panel_* frame art (package_01) — the reference's look.
    /// The title is left-aligned just inside the top edge so it clears the frame's top-centre gem. Returns
    /// the inner content rect (inset past the ornate corners).</summary>
    private Rectangle CleanPanel(SpriteBatch b, Rectangle r, string title)
    {
        _ui.Panel(b, r);
        _ui.TextBig(b, title, r.X + 44, r.Y + 26, Gold, 20);
        return new Rectangle(r.X + 44, r.Y + 70, r.Width - 88, r.Height - 92);
    }

    /// <summary>Right context column (reference regions): a stack of four plates — Idle Rewards, Objective,
    /// Loot, Expedition — all fed from live run data.</summary>
    private void DrawRightColumn(SpriteBatch b)
    {
        // Rev 3 §20: right context rail (1570,110,326,690) — secondary panels, all live data, NO keyboard
        // hints (§21). Idle-rate → objective → reward activity → expedition.
        const int px = 1570, pw = 326;

        // Idle rate (1570,110,326,130). Auto-credited, so no Claim button (§20.2).
        var inner = CleanPanel(b, new Rectangle(px, 110, pw, 130), "IDLE RATE");
        if (_ui.Assets.Get("currency_gleam") is { } gi) b.Draw(gi, new Rectangle(inner.X, inner.Y + 2, 38, 38), Color.White);
        _ui.TextBig(b, $"+{Game1.Abbrev((long)(IdleGleamRate * 60f))}/min", inner.X + 48, inner.Y + 8, Gold, 24);

        // Objective (1570,254,326,150).
        inner = CleanPanel(b, new Rectangle(px, 254, pw, 150), "OBJECTIVE");
        _ui.TextBig(b, Deepest >= ConquerAt ? "Conquered" : $"Reach depth {ConquerAt}", inner.X, inner.Y, Bone, 19);
        _ui.TextRightBig(b, $"{Math.Min(Deepest, ConquerAt)} / {ConquerAt}", inner.Right, inner.Y, Gold, 18);
        _ui.BarArt(b, new Rectangle(inner.X, inner.Y + 40, inner.Width, 26), ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f, "progress");

        // Reward activity (1570,418,326,210) — real summary, no fake loot grid, no keyboard hints (§20.4/§21).
        inner = CleanPanel(b, new Rectangle(px, 418, pw, 210), "REWARD ACTIVITY");
        var ry = inner.Y;
        var anyReward = false;
        if (ChestCount > 0)
        {
            _ui.TextBig(b, $"{ChestCount} chest{(ChestCount == 1 ? "" : "s")} available", inner.X, ry, Bone, 18); ry += 40; anyReward = true;
        }
        if (Mastery.Available > 0)
        {
            _ui.TextBig(b, $"{Mastery.Available} Mastery Point{(Mastery.Available == 1 ? "" : "s")}", inner.X, ry, Bone, 18); ry += 40; anyReward = true;
        }
        if (Deepest > 0) _ui.TextBig(b, $"Deepest wave reached: {Deepest}", inner.X, ry, Slate, 16);
        else if (!anyReward) _ui.TextBig(b, "No rewards pending", inner.X, inner.Y, Slate, 18);

        // Expedition (1570,642,326,145) — region wave + live status.
        inner = CleanPanel(b, new Rectangle(px, 642, pw, 145), "EXPEDITION");
        _ui.TextBig(b, $"Wave {_run!.Wave + 1}", inner.X, inner.Y, Bone, 19);
        var state = _mode == Mode.Downed ? "RECOVERING"
            : WaveScaling.IsBossWave(_run.Wave + 1, ExpeditionTuning.Default) ? "BOSS WAVE" : "ACTIVE";
        _ui.TextRightBig(b, state, inner.Right, inner.Y + 2, _mode == Mode.Downed ? Ember : state == "BOSS WAVE" ? Gold : Verdant, 16);
    }

    /// <summary>Auto-skill dock (region F): the build as circular auto-cast medallions, centered under the
    /// arena. Each shows its Source glyph, its Form, and the AUTO state the reference calls for.</summary>
    private void DrawSkillDock(SpriteBatch b)
    {
        // Spec §14: auto-skill dock centred under the arena (500,770,920,145). Hex slots (ui_slot_skill_hex),
        // Source glyph inside, Form + AUTO beneath. Presentation Model C (§14.4): AUTO/READY, no fake cooldowns.
        var skills = Loadout.Skills;
        var n = PlayerLoadout.MaxSkills;
        const int slot = 104, gap = 18, y = 792, dockCx = 960;
        var total = n * slot + (n - 1) * gap;
        var x0 = dockCx - total / 2;
        for (var i = 0; i < n; i++)
        {
            var box = new Rectangle(x0 + i * (slot + gap), y, slot, slot);
            if (i < skills.Count)
            {
                var s = skills[i];
                var sc = SourceColor.GetValueOrDefault(s.Source, Bone);
                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl) b.Draw(sl, box, Color.White);
                // §18.3 Layer 1: source-coloured inner glow (no Form-glyph asset ships, so the Source glyph is
                // the central identity and the Form name labels it — a quieter composition per §36).
                _ui.Diamond(b, new Rectangle(box.Center.X - 33, box.Center.Y - 33, 66, 66), sc * 0.28f);
                if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g)
                    b.Draw(g, new Rectangle(box.X + 28, box.Y + 24, box.Width - 56, box.Height - 56), Color.White);
                else _ui.Diamond(b, new Rectangle(box.Center.X - 24, box.Center.Y - 24, 48, 48), sc);
                // (§18.3 Layer 4 Vow glyph omitted: the loadout SkillChoice doesn't carry the Vow — it lives on
                // the built ability. Wiring the built skills through would add it; deferred as optional.)
                _ui.TextCenterBig(b, FormShort(s.Form), box.Center.X, box.Bottom + 2, Bone, 16);
                _ui.TextCenterBig(b, "AUTO", box.Center.X, box.Bottom + 26, Gold, 14);
            }
            else
            {
                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl) b.Draw(sl, box, Color.White * 0.5f);
                _ui.TextCenterBig(b, i == skills.Count ? "+B" : "-", box.Center.X, box.Center.Y - 10, Dim, 16);
            }
        }
    }

    /// <summary>Bottom-left battle controls (reference region G): adjustable BATTLE SPEED (x1/x2/x4/x8) driving
    /// the real replay multiplier, plus the AUTO HUNT state.</summary>
    private void DrawBattleControls(SpriteBatch b, Point mouse, bool clicked)
    {
        // Spec §15 + §4.4: speed / auto-hunt controls, bottom-left (24,785,250,140). A QUIET surface (not a
        // heavy ornate frame) — these are minor controls; the arena stays dominant. SPEED drives the real
        // replay multiplier; AUTO HUNT is always ON (this is a pure idle-watch screen).
        var panel = new Rectangle(24, 785, 252, 140);
        _ui.Fill(b, panel, new Color(0x0D, 0x0B, 0x14, 0xD2));
        _ui.Fill(b, new Rectangle(panel.X, panel.Y, panel.Width, 3), Bloom * 0.5f);

        _ui.TextBig(b, "SPEED", 42, 796, Slate, 15);
        for (var i = 0; i < SpeedSteps.Length; i++)
        {
            var r = new Rectangle(42 + i * 50, 824, 44, 34);
            var on = Math.Abs(_speedMul - SpeedSteps[i]) < 0.01f;
            var hover = r.Contains(mouse);
            _ui.Fill(b, r, on ? Gold : hover ? new Color(0x2C, 0x25, 0x44) : new Color(0x1A, 0x14, 0x28));
            _ui.TextCenterBig(b, $"x{(int)SpeedSteps[i]}", r.Center.X, r.Y + 8, on ? Shadow : Bone, 16);
            if (clicked && hover) _speedMul = SpeedSteps[i];
        }

        _ui.TextBig(b, "AUTO HUNT", 42, 872, Slate, 15);
        _ui.Fill(b, new Rectangle(150, 868, 106, 30), new Color(0x1A, 0x30, 0x22));
        _ui.Fill(b, new Rectangle(150, 868, 106, 3), Verdant * 0.7f);
        _ui.TextCenterBig(b, "ON", 203, 873, Verdant, 16);
    }

    // The gear overlays register to the champion base's canvas; this is their back-to-front occlusion order.
    private static readonly GearSlot[] OverlayOrder =
    {
        GearSlot.Chest, GearSlot.Boots, GearSlot.Gloves, GearSlot.Helm,
        GearSlot.Weapon, GearSlot.Ring, GearSlot.Charm, GearSlot.Focus,
    };

    private void DrawChampion(SpriteBatch b, Rectangle box, bool dead)
    {
        _ui.Fill(b, new Rectangle(box.X + 60, box.Bottom - 14, box.Width - 120, 14), GroundShade);
        var attacking = _champLunge > 0.3f;
        var tint = dead ? new Color(0x3A, 0x3A, 0x44) : Color.White;

        // FULL-BODY static poses (package_02). The package_02A "animation" strips are head-and-torso busts,
        // so they're wrong for the arena figure — only a full body reads here. Pose swaps on lunge/death.
        var key = dead ? "hunter_defeated" : attacking ? "hunter_attack_01" : "hunter_idle";
        if (!_ui.Sprite(b, key, box, tint, 0.03f) && !_ui.Sprite(b, "hunter_idle", box, tint, 0.03f))
            _ui.Fill(b, new Rectangle(box.Center.X - 32, box.Bottom - 80, 64, 72), dead ? Dim : Gold);
    }

    /// <summary>
    /// Draw the champion as a base body plus one overlay per EQUIPPED slot — so the player's actual gear
    /// shows on the champion. Every layer shares the base's 800x1040 canvas, so drawing each into the same
    /// bottom-anchored rect registers it automatically; an empty slot draws nothing, and a slot whose
    /// overlay for this pose isn't authored yet simply isn't drawn (never a borrowed asset).
    /// </summary>
    private void DrawLayeredChampion(SpriteBatch b, Rectangle box, Texture2D baseTex, string pose, Color tint, Hunter? hunter)
    {
        var draw = new Rectangle(box.X + 8, box.Y + 8, box.Width - 16, box.Height - 40);
        var sc = MathF.Min(draw.Width / (float)baseTex.Width, draw.Height / (float)baseTex.Height);
        var w = Math.Max(1, (int)(baseTex.Width * sc));
        var h = Math.Max(1, (int)(baseTex.Height * sc));
        var rect = new Rectangle(draw.Center.X - w / 2, draw.Bottom - h, w, h);

        b.Draw(baseTex, rect, tint);
        if (hunter is null) return;
        foreach (var slot in OverlayOrder)
            if (hunter.Worn(slot) is not null
                && _ui.Assets.Get($"overlay_{slot.ToString().ToLowerInvariant()}_{pose}") is { } ov)
                b.Draw(ov, rect, tint);
    }

    private void DrawCallouts(SpriteBatch b)
    {
        foreach (var c in _callouts)
        {
            var rise = (int)((1f - c.Life) * 40f);
            var fade = Math.Clamp(c.Life * 1.8f, 0f, 1f);
            _ui.TextCenterBig(b, c.Text, c.X, c.Y - rise, c.Color * fade, c.Px <= 0 ? 36 : c.Px);
        }
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    /// <summary>DEV: start a run and play partway into a wave, for screenshots.</summary>
    public void DevStart(Hunter hunter, float ehp, float edmg)
    {
        _hunter = hunter;
        _enemyBaseHealth = ehp;
        _enemyBaseDamage = edmg;
        _lastSource = EnemySource;
        StartRun(hunter);
        _playheadMs = 900f;
    }
}
