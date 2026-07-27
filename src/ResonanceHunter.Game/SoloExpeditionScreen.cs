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
    private static readonly Color OnScene = UiKit.Vellum;

    private const int GroundY = 178;
    // A wave is resolved instantly then REPLAYED at this speed. It was 3.5x with no gap between waves, so
    // the whole run blurred past — playtest: "waves flow too fast". Slowed to a watchable pace, and a short
    // BREATH now sits between waves so each clear reads as its own beat. The champion's own skill rate
    // (TEMPO, the Focus slot) still speeds combat back up on top of this, so the upgrade is now visible.
    private const float PlaybackSpeed = 2.0f;
    private const float WaveBreakSeconds = 0.8f;   // the pause after a clear, before the next enemy is fought
    private const float DownedSeconds = 1.6f;   // the recovery beat before the champion tries again

    // package_10 arena layout (region D): the hunter fights on the left, enemies to the right, both grounded
    // on the same floor line — LARGE, so the hero dominates the arena the way the reference does.
    private static readonly Rectangle ChampBox = new(54, GroundY - 86, 76, 86);
    private static readonly Rectangle EnemyBox = new(208, GroundY - 84, 100, 84);

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

    private void Say(string text, Color color, int yOffset = -10)
    {
        var stack = _callouts.Count(c => c.Life > 0.6f);
        _callouts.Add(new Callout { Text = text, Color = color, X = ChampBox.Center.X, Y = ChampBox.Y + yOffset - stack * 9, Life = 1f, Px = 9 });
    }

    /// <summary>A floating combat number over the enemy — the fight's "action" read (package_10). Scaled off
    /// the hunter's real PowerRating, jittered so numbers don't stack; crits are gold and linger.</summary>
    private void SpawnDamage(int amount, bool crit)
    {
        _callouts.Add(new Callout
        {
            Text = crit ? $"-{amount:N0} CRIT" : $"-{amount:N0}",
            Color = crit ? Gold : Bone,
            X = EnemyBox.Center.X + Jitter((int)_playheadMs, 26),
            Y = EnemyBox.Y + 10 - Jitter((int)_playheadMs + 11, 8),
            Life = crit ? 1.3f : 1f,
            Px = crit ? 18 : 13,
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

        _playheadMs += dt * 1000f * PlaybackSpeed;

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
                    Say($"+{e.Amount}", Verdant, -2);
                    _vfx.Play("vfx_levelup", ChampBox.Center.X, ChampBox.Y + 8, tint: Verdant);
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
                _vfx.Play("vfx_levelup", ChampBox.Center.X, ChampBox.Y + 6, fps: 18f, tint: Verdant);
                break;
            default:
                _vfx.Play("vfx_weakhit", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 18f, tint: Steel);
                break;
        }
    }

    private static int Jitter(int seed, int spread) => (int)(seed * 2654435761L % (spread * 2 + 1)) - spread;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, Point mouse, bool clicked, string regionName, string enemyArt = "")
    {
        _enemyArt = enemyArt;
        if (_run is null || _replay is null || _champ is null) return;

        // The top HUD, stage header, resource bar and hunt-log panel are drawn as overlays AFTER the arena.

        // ── The enemy, with an anticipation ring before it swings, sliding in on a new wave. ─────────
        var elunge = (int)(_enemyLunge * -10f);
        var enter = (int)(_enemyEnter * 140f);   // starts 140px right of home and slides to it
        var ebox = new Rectangle(EnemyBox.X + elunge + enter, EnemyBox.Y, EnemyBox.Width, EnemyBox.Height);
        _ui.Fill(b, new Rectangle(ebox.X + 8, ebox.Bottom - 6, ebox.Width - 16, 4), Shadow);
        // A boss wave shows the region's boss creature; every other wave shows the element's creature (art
        // pack v1 is one creature per SOURCE — see design/art/asset-integration-spec.md §4). Nothing is
        // substituted: an unmatched key falls to the flat marker below.
        var isBossWave = WaveScaling.IsBossWave(_run.Wave + 1, ExpeditionTuning.Default);
        // package_02A/03A/04A flipbook strips — idle loops; the attack/slam plays during the wind-up
        // telegraph. Falls back to the package_03/04 static key poses for enemies/bosses without a strip.
        var bob = (int)(MathF.Sin(_anim * 2f) * 2f);
        var attacking = _enemyWindup > 0f;
        var boxW = isBossWave ? (int)(ebox.Width * 1.15f) : ebox.Width;
        var boxH = isBossWave ? (int)(ebox.Height * 1.25f) : ebox.Height - 6;
        var figTop = ebox.Bottom - 4 - boxH;   // stable top of the drawn figure (no bob) — anchors the bar/label
        var ab = new Rectangle(ebox.Center.X - boxW / 2, figTop + bob, boxW, boxH);
        var crop = isBossWave ? 0.22f : 0.08f;   // bosses have big top padding + streak artifacts to trim

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
            else _ui.Fill(b, new Rectangle(ebox.X + 10, ebox.Y + 10, ebox.Width - 20, ebox.Height - 20), Ember);
        }

        if (_enemyWindup > 0f)
        {
            var r = (int)(4 + _enemyWindup * 8);
            Outline(b, new Rectangle(ab.X - r, figTop - r, ab.Width + r * 2, boxH + r * 2), Ember, 1);
        }
        // HP bar + BOSS label ride just above the ACTUAL figure top, so they scale with the boss.
        var barW = isBossWave ? boxW - 8 : ebox.Width - 12;
        _ui.BarArt(b, new Rectangle(ebox.Center.X - barW / 2, figTop - 9, barW, 6), _replay.EnemyHealthFraction, Ember);
        if (_run.LastWaveWasBoss || isBossWave)
            _ui.TextCenterBig(b, "BOSS", ebox.Center.X, figTop - 21, Gold, 10);

        // ── The champion (arena left). Name/HP now live in the top-left HUD. ──
        var push = (int)(_champLunge * 10f);
        var cbox = new Rectangle(ChampBox.X + push, ChampBox.Y, ChampBox.Width, ChampBox.Height);
        if (_replay.IsShielded(0)) Outline(b, new Rectangle(cbox.X - 1, cbox.Y - 1, cbox.Width + 2, cbox.Height + 2), Steel, 1);
        DrawChampion(b, cbox, dead: _mode == Mode.Downed);

        _vfx.Draw(b);
        DrawCallouts(b);

        // A red wash over the whole frame the instant the champion falls — you can't miss the death.
        if (_deathFlash > 0f) _ui.Fill(b, new Rectangle(0, 0, 480, 270), Ember * (_deathFlash * 0.35f));

        // The wave-cleared banner: a gold flash above the fight that fades as the next wave opens.
        if (_bannerTimer > 0f)
        {
            var fade = Math.Clamp(_bannerTimer * 1.4f, 0f, 1f);
            _ui.TextCenterBig(b, _bannerText, 196, 78, Gold * fade, 11);   // floats above the fight, clear of the header/HUD
        }

        // ── package_10 HUD + panels, drawn over the arena. ──
        DrawHunterHud(b);
        DrawStageHeader(b, regionName, isBossWave);
        DrawHuntLog(b);
        DrawSkillDock(b);
        DrawSpeedDial(b);

        if (_mode == Mode.Downed)
        {
            _ui.Fill(b, new Rectangle(60, 116, 270, 30), PanelBg);
            _ui.TextCenter(b, "CHAMPION DOWN — REGROUPING", 195, 122, Ember);
            _ui.TextCenter(b, $"REACHED WAVE {_run.Wave + 1}. STRENGTHEN THE BUILD (B).", 195, 134, Slate);
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
        var panel = new Rectangle(5, 4, 134, 52);
        _ui.Panel(b, panel);
        // Medallion disc first (it's opaque), then the portrait INSET on top so the ornate ring frames it.
        var med = new Rectangle(panel.X + 4, panel.Y + 3, 46, 46);
        if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, med, Color.White);
        if (_ui.Assets.Get("hunter_portrait") is { } por)
            b.Draw(por, new Rectangle(med.X + 8, med.Y + 8, med.Width - 16, med.Height - 16), Color.White);

        var tx = med.Right + 5;
        var adept = Mastery.MasteryForm() is { } mf ? $"{FormShort(mf)} ADEPT" : "SEEKER";
        _ui.Text(b, adept, tx, panel.Y + 5, Bone);
        _ui.TextRight(b, $"LV {_hunter?.HunterLevel ?? 1}", panel.Right - 7, panel.Y + 5, Gold);
        var hp = Math.Max(0, _replay?.HealthOf(0) ?? 0);
        _ui.BarArt(b, new Rectangle(tx, panel.Y + 21, panel.Right - tx - 7, 7), _replay?.HealthFractionOf(0) ?? 1f, Bloom);
        _ui.Text(b, $"{hp}/{_champ?.MaxHealth ?? 0}", tx, panel.Y + 31, Slate);
        _ui.TextRight(b, $"PWR {_hunter?.PowerRating ?? 0}", panel.Right - 7, panel.Y + 31, Ember);

        var sx = panel.X + 3;
        foreach (var s in Loadout.Skills)
        {
            var box = new Rectangle(sx, panel.Bottom + 3, 14, 14);
            if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g) b.Draw(g, box, Color.White);
            else _ui.Diamond(b, box, SourceColor.GetValueOrDefault(s.Source, Slate));
            sx += 17;
        }
    }

    /// <summary>Top-center stage header (region B): region name, current wave, and the conquest progress bar.</summary>
    private void DrawStageHeader(SpriteBatch b, string regionName, bool isBossWave)
    {
        const int cx = 196;   // centered between the HUD (ends ~140) and the now-compact resource bar
        _ui.TextCenterBig(b, regionName.ToUpperInvariant(), cx, 3, Gold, 12);   // prominent region title
        _ui.TextCenter(b, $"WAVE {_run!.Wave + 1}", cx, 20, Bone);
        var frac = ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f;
        _ui.BarArt(b, new Rectangle(cx - 55, 31, 110, 6), frac, isBossWave ? Gold : Verdant);
        _ui.TextCenter(b, Deepest >= ConquerAt ? "CONQUERED — PUSH FOR LOOT" : $"DEEPEST {Deepest} / {ConquerAt}",
            cx, 40, Deepest >= ConquerAt ? Gold : Slate);
    }

    /// <summary>Right context column (region E): objective + progress, the real to-do cues, and the
    /// current expedition state — all from live run data.</summary>
    private void DrawHuntLog(SpriteBatch b)
    {
        var panel = new Rectangle(352, 24, 122, 152);
        _ui.Panel(b, panel);
        var x = panel.X + 11;
        _ui.TextCenter(b, "HUNT LOG", panel.Center.X, panel.Y + 9, Gold);

        var y = panel.Y + 26;
        _ui.Text(b, "OBJECTIVE", x, y, Slate); y += 11;
        _ui.Text(b, Deepest >= ConquerAt ? "Region conquered" : $"Reach depth {ConquerAt}", x, y, Bone); y += 10;
        _ui.BarArt(b, new Rectangle(x, y, panel.Width - 22, 6), ConquerAt > 0 ? Deepest / (float)ConquerAt : 0f, Gold); y += 15;

        if (ChestCount > 0)
        {
            _ui.Diamond(b, new Rectangle(x, y + 1, 7, 7), Gold);
            _ui.Text(b, $"{ChestCount} CHEST{(ChestCount == 1 ? "" : "S")} · F", x + 11, y, Bone); y += 12;
        }
        if (Mastery.Available > 0)
        {
            _ui.Diamond(b, new Rectangle(x, y + 1, 7, 7), Bloom);
            _ui.Text(b, $"{Mastery.Available} MASTERY · B", x + 11, y, Bone); y += 12;
        }

        y = panel.Bottom - 40;
        _ui.Text(b, "EXPEDITION", x, y, Slate); y += 11;
        _ui.Text(b, $"Wave {_run!.Wave + 1}", x, y, Bone);
        _ui.TextRight(b, _mode == Mode.Downed ? "DOWN" : "IN PROGRESS", panel.Right - 11, y, _mode == Mode.Downed ? Ember : Verdant);
    }

    /// <summary>Auto-skill dock (region F): the build as circular auto-cast medallions, centered under the
    /// arena. Each shows its Source glyph, its Form, and the AUTO state the reference calls for.</summary>
    private void DrawSkillDock(SpriteBatch b)
    {
        var skills = Loadout.Skills;
        var n = PlayerLoadout.MaxSkills;
        const int d = 34, gap = 8;
        var x0 = 200 - (n * d + (n - 1) * gap) / 2;
        const int y = 183;
        for (var i = 0; i < n; i++)
        {
            var box = new Rectangle(x0 + i * (d + gap), y, d, d);
            if (i < skills.Count)
            {
                var s = skills[i];
                // Ornate round medallion (its own centre), then the source glyph on top — NO square fill
                // behind it (that showed dark corners around the round frame).
                if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, box, Color.White);
                if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g)
                    b.Draw(g, new Rectangle(box.X + 8, box.Y + 7, box.Width - 16, box.Height - 14), Color.White);
                else _ui.Diamond(b, new Rectangle(box.Center.X - 7, box.Center.Y - 7, 14, 14), SourceColor.GetValueOrDefault(s.Source, Bone));
                _ui.TextCenter(b, FormShort(s.Form), box.Center.X, box.Bottom + 1, Bone);
                _ui.TextCenter(b, "AUTO", box.Center.X, box.Bottom + 10, Gold);
            }
            else
            {
                if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, box, Color.White * 0.5f);
                _ui.TextCenter(b, i == skills.Count ? "+B" : "—", box.Center.X, box.Center.Y - 3, Dim);
            }
        }
    }

    /// <summary>Bottom-left speed/mode controls (region G): an always-on AUTO dial + the playback speed.</summary>
    private void DrawSpeedDial(SpriteBatch b)
    {
        var dial = new Rectangle(10, 190, 30, 30);
        if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, dial, Color.White);   // frame first
        _ui.TextCenter(b, "AUTO", dial.Center.X, dial.Center.Y - 3, Gold);
        _ui.Text(b, $"x{PlaybackSpeed:0.0}", dial.Right + 6, dial.Center.Y - 6, Bone);
        _ui.Text(b, "SPEED", dial.Right + 6, dial.Center.Y + 3, Slate);
    }

    // The gear overlays register to the champion base's canvas; this is their back-to-front occlusion order.
    private static readonly GearSlot[] OverlayOrder =
    {
        GearSlot.Chest, GearSlot.Boots, GearSlot.Gloves, GearSlot.Helm,
        GearSlot.Weapon, GearSlot.Ring, GearSlot.Charm, GearSlot.Focus,
    };

    private void DrawChampion(SpriteBatch b, Rectangle box, bool dead)
    {
        _ui.Fill(b, new Rectangle(box.X + 10, box.Bottom - 5, box.Width - 20, 3), Shadow);
        var attacking = _champLunge > 0.3f;
        var tint = dead ? new Color(0x3A, 0x3A, 0x44) : Color.White;

        // FULL-BODY static poses (package_02). The package_02A "animation" strips are head-and-torso busts,
        // so they're wrong for the arena figure — only a full body reads here. Pose swaps on lunge/death.
        var key = dead ? "hunter_defeated" : attacking ? "hunter_attack_01" : "hunter_idle";
        if (!_ui.Sprite(b, key, box, tint, 0.03f) && !_ui.Sprite(b, "hunter_idle", box, tint, 0.03f))
            _ui.Fill(b, new Rectangle(box.Center.X - 8, box.Bottom - 20, 16, 18), dead ? Dim : Gold);
    }

    /// <summary>
    /// Draw the champion as a base body plus one overlay per EQUIPPED slot — so the player's actual gear
    /// shows on the champion. Every layer shares the base's 800x1040 canvas, so drawing each into the same
    /// bottom-anchored rect registers it automatically; an empty slot draws nothing, and a slot whose
    /// overlay for this pose isn't authored yet simply isn't drawn (never a borrowed asset).
    /// </summary>
    private void DrawLayeredChampion(SpriteBatch b, Rectangle box, Texture2D baseTex, string pose, Color tint, Hunter? hunter)
    {
        var draw = new Rectangle(box.X + 2, box.Y + 2, box.Width - 4, box.Height - 10);
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
            var rise = (int)((1f - c.Life) * 10f);
            var fade = Math.Clamp(c.Life * 1.8f, 0f, 1f);
            _ui.TextCenterBig(b, c.Text, c.X, c.Y - rise, c.Color * fade, c.Px <= 0 ? 9 : c.Px);
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
