using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// BRAND's field tick reports the mark IN FORCE (ADR-011, the MARK reference, 2026-09-29): the presentation-only hook
/// that lets the screen show who is marked and how deep.
/// </summary>
/// <remarks>
/// <para>
/// The field path emitted <see cref="BattleEventKind.Marked"/> BEFORE it updated the depth, so every wave's first tick
/// said 0 % and ETCH was always one step late (the cast path and OATHMARK already reported after the update). The emit
/// moved below the update: an information-only change.
/// </para>
/// <para>
/// Two promises. It moves NO gameplay number: a fingerprint of every other event, every Marked's time and window, every
/// wave's outcome and metrics and the champion's end state, taken on the code BEFORE the move, is byte-identical after
/// it (the events list is write-only inside the fight). And it is TRUE: the reported depth is the one the next hits pay.
/// </para>
/// </remarks>
public class brand_marked_report_test
{
    private static Champion Fresh(int pool = 400_000) => new() { MaxHealth = pool, Health = pool };

    private static Build Equip(params EquippedSkill[] skills)
    {
        var b = new Build();
        foreach (var s in skills) b.Equip(s);
        return b;
    }

    private static List<WaveCreature> Pack(int count, float health, float damage)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature { MaxHealth = health, Health = health, Damage = damage }).ToList();

    public static IEnumerable<object[]> Battery()
    {
        yield return new object[] { "brand" };
        yield return new object[] { "brand_etch" };
        yield return new object[] { "brand_etch_sink_graven" };
        yield return new object[] { "brand_etch_pace" };
        yield return new object[] { "brand_sprawl_anchor_winnow" };
        yield return new object[] { "brand_sprawl_even" };
        yield return new object[] { "brand_call" };
        yield return new object[] { "brand_spend" };
        yield return new object[] { "brand_press_seeker" };
        yield return new object[] { "brand_first_slot" };
    }

    private static (Build Build, Func<List<WaveCreature>> Pack) Case(string name) => name switch
    {
        "brand" => (TestBuilds.Of("volley_spray", "sign_brand"), () => Pack(3, 2_400f, 40f)),
        "brand_etch" => (Equip(TestBuilds.Skill("volley_spray"), TestBuilds.Chosen("sign_brand", "ETCH")), () => Pack(3, 4_000f, 40f)),
        "brand_etch_sink_graven" => (Equip(TestBuilds.Skill("volley_spray"), TestBuilds.Chosen("sign_brand", "ETCH", null, "SINK", "GRAVEN")), () => Pack(4, 6_000f, 40f)),
        "brand_etch_pace" => (Equip(TestBuilds.Skill("volley_spray"), TestBuilds.Chosen("sign_brand", "ETCH", null, "PACE")), () => Pack(3, 4_000f, 40f)),
        "brand_sprawl_anchor_winnow" => (Equip(TestBuilds.Skill("volley_spray"), TestBuilds.Chosen("sign_brand", "SPRAWL", null, "ANCHOR", "WINNOW")), () => Pack(4, 5_000f, 40f)),
        "brand_sprawl_even" => (Equip(TestBuilds.Skill("volley_spray"), TestBuilds.Chosen("sign_brand", "SPRAWL", null, "EVEN")), () => Pack(4, 3_000f, 40f)),
        "brand_call" => (TestBuilds.Of("sign_call", "volley_spray", "sign_brand"), () => Pack(3, 4_000f, 40f)),
        "brand_spend" => (Equip(TestBuilds.Chosen("sign_call", "SPEND"), TestBuilds.Skill("volley_spray"), TestBuilds.Skill("sign_brand")), () => Pack(3, 5_000f, 40f)),
        "brand_press_seeker" => (TestBuilds.Of("sig_seeker_hard_hands", "volley_spray", "hammer_press", "sign_brand"), () => Pack(4, 4_000f, 50f)),
        "brand_first_slot" => (TestBuilds.Of("sign_brand", "volley_spray"), () => Pack(3, 3_000f, 40f)),
        _ => throw new ArgumentException(name),
    };

    /// <summary>Three consecutive waves on one champion, seeded.</summary>
    private static (List<(WaveOutcome Outcome, List<BattleEvent> Events, string Metrics)> Waves, Champion Champ) Run(string name)
    {
        var (build, pack) = Case(name);
        var champ = Fresh();
        var rng = new Random(11);
        var tuning = ExpeditionTuning.Default with { TickCeilingMs = 20_000 };
        var waves = new List<(WaveOutcome, List<BattleEvent>, string)>();
        for (var w = 0; w < 3 && champ.Alive; w++)
        {
            var metrics = new WaveMetrics();
            var (outcome, events) = SoloBattle.ResolveWave(champ, build, new Hunter(), pack(), 1000, tuning, rng, metrics: metrics);
            waves.Add((outcome, events, Snapshot(metrics)));
        }
        return (waves, champ);
    }

    private static string Snapshot(object o)
    {
        var sb = new StringBuilder();
        foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var v = p.GetValue(o);
            sb.Append(p.Name).Append('=');
            if (v is IDictionary d)
                foreach (var k in d.Keys.Cast<object>().OrderBy(k => k.ToString())) sb.Append(k).Append(':').Append(d[k]).Append(',');
            else sb.Append(v);
            sb.Append(';');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Everything the fight decided, with a Marked's REPORTED DEPTH (its Slot) blanked: the one number the move changes.
    /// </summary>
    private static string Fingerprint(string name)
    {
        var (waves, champ) = Run(name);
        var sb = new StringBuilder();
        foreach (var (outcome, events, metrics) in waves)
        {
            sb.Append("wave ").Append(outcome).Append('|').Append(metrics).Append('|');
            foreach (var e in events)
                sb.Append(e.Kind == BattleEventKind.Marked ? e with { Slot = -999 } : e).Append(';');
        }
        sb.Append("end ").Append(champ.Health).Append(',').Append(champ.ElapsedMs).Append(',').Append(champ.BeatCount)
          .Append(',').Append(champ.CurrentShield);
        foreach (var kv in champ.ReadyAt.OrderBy(kv => kv.Key)) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
        foreach (var kv in champ.ReadyAtBeat.OrderBy(kv => kv.Key)) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    // ── 1. IT MOVES NO GAMEPLAY NUMBER ──────────────────────────────────────────────────────────

    /// <summary>Fingerprints taken on the code BEFORE the emit moved (2026-09-29, HEAD 0060f5e9).</summary>
    private static readonly Dictionary<string, string> Before = new()
    {
        ["brand"] = "f3d1a1ca16038c0523b1295c566cfe5e6c711559739801a01a5b1c9068011c9d",
        ["brand_etch"] = "c3d79b886e6fae53501002b773a73e8d8bdcefd52ee1648dbd396e1338fd7fa2",
        ["brand_etch_sink_graven"] = "5db59bd0ce8a1739b38aff97fc8b760b24109f9deb550a632a35000a5c74c99e",
        ["brand_etch_pace"] = "6973452f11d8784ed0e736902fb43944c7849909034f2021be7ec400127ab171",
        ["brand_sprawl_anchor_winnow"] = "3ac10cccd7a486d62618c0534772875f3648b23604955040d2bcdd150a6b74f5",
        ["brand_sprawl_even"] = "f731fdf18953245d2c3d763520b8bd598170bcef83a07f093b889cfc8e6ac141",
        ["brand_call"] = "a97b9320fcc78272401fe2b2b23707f44f2286a49f7fd7584ff6157735797a5c",
        ["brand_spend"] = "ea79305441d071be9bf3807c0693a325df8cef96a37b121c1d88f20bb0e1cabc",
        ["brand_press_seeker"] = "7bc3299538e9b30cb5561979f8ab02a28bac846122c814e26044428d87e14938",
        ["brand_first_slot"] = "e736fc86eeda5c85efc5ebd21e6ae8a40006affd1f6055cef414134c8a57cedb",
    };

    [Theory]
    [MemberData(nameof(Battery))]
    public void test_the_marked_report_moves_no_gameplay_number(string name)
    {
        var now = Fingerprint(name);
        Assert.True(Before[name] == now, $"{name}: the fight moved: {now}");
    }

    // ── 2. IT IS TRUE ───────────────────────────────────────────────────────────────────────────

    private static List<(int At, int Percent, int Window)> Marks(string name, int wave = 0)
        => Run(name).Waves[wave].Events.Where(e => e.Kind == BattleEventKind.Marked).Select(e => (e.AtMs, e.Slot, e.Amount)).ToList();

    [Fact]
    public void test_the_first_tick_reports_the_mark_it_opens()
    {
        // base BRAND: +70 % from the wave's first tick at 2000 ms (it said 0 % for two seconds of every wave)
        var brand = Marks("brand");
        Assert.Equal((2000, 70, 2000), brand[0]);
        Assert.All(brand, m => Assert.Equal(70, m.Percent));
        // SPRAWL at half strength across the wave; EVEN at three-quarters (0.525f x 100 is 52.4999..., reported 52)
        Assert.Equal(45, Marks("brand_sprawl_anchor_winnow")[0].Percent);   // 35 + WINNOW's first +10
        Assert.Equal(52, Marks("brand_sprawl_even")[0].Percent);
    }

    [Fact]
    public void test_etch_reports_each_depth_on_the_tick_that_cuts_it()
    {
        // ETCH deepens on every tick, the first included, to its cap of +170 over the base (the card's "+170%" is the
        // deepening; the total in force is 70 + 170 = 240, reported as such)
        Assert.Equal(new[] { 120, 170, 220, 240 }, Marks("brand_etch").Select(m => m.Percent).Take(4).ToArray());
        Assert.Equal(new[] { 150, 230, 310, 320 }, Marks("brand_etch_sink_graven").Select(m => m.Percent).Take(4).ToArray());
        var pace = Marks("brand_etch_pace");
        Assert.Equal((1000, 120, 1000), pace[0]);
        Assert.Equal((2000, 170, 1000), pace[1]);
    }

    [Fact]
    public void test_the_reported_depth_is_what_the_front_enemy_pays()
    {
        // the same seeded wave with and without BRAND: after each tick, the first SPRAY arrow on the front enemy pays
        // exactly 1 + the reported depth (integer rounding aside)
        var tuning = ExpeditionTuning.Default with { TickCeilingMs = 12_000 };
        List<BattleEvent> Wave(Build b)
        {
            var (_, events) = SoloBattle.ResolveWave(Fresh(), b, new Hunter(), Pack(3, 60_000f, 1f), 1000, tuning, new Random(5));
            return events;
        }
        var marked = Wave(Equip(TestBuilds.Skill("volley_spray"), TestBuilds.Chosen("sign_brand", "ETCH")));
        var plain = Wave(TestBuilds.Of("volley_spray"));
        var checkedTicks = 0;
        foreach (var m in marked.Where(e => e.Kind == BattleEventKind.Marked))
        {
            var found = marked.Cast<BattleEvent?>().FirstOrDefault(e => e!.Value.Kind == BattleEventKind.Strike && e.Value.Slot == 0
                                                                        && e.Value.AtMs > m.AtMs && e.Value.AtMs < m.AtMs + m.Amount);
            if (found is not { } hit) continue;
            var bare = plain.First(e => e.Kind == BattleEventKind.Strike && e.Slot == 0 && e.AtMs == hit.AtMs
                                        && e.Hit == hit.Hit && e.Crit == hit.Crit);
            Assert.InRange(hit.Amount / (float)bare.Amount, 1f + m.Slot / 100f - 0.02f, 1f + m.Slot / 100f + 0.02f);
            checkedTicks++;
        }
        Assert.True(checkedTicks >= 3, $"only {checkedTicks} ticks had a front hit inside their window");
    }
}
