using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE THROTTLE TABLE IS GENERATED, NOT MIRRORED (the remaining-skill sweep's design.md section 7). <c>film_audio.py</c>
/// renders a traced film's sound with the bank's repeat throttle; it used to carry a hand copy of <see cref="SoundBank"/>'s
/// gaps and unthrottled cues, which drifts the first time a cue is added on one side. This test WRITES
/// <c>tools/asset-pipeline/sound_throttle.json</c> from the bank itself (sorted keys, LF) and FAILS when the committed file
/// differed, so a stale table is a red test, and the next run (with the file regenerated) is green. It also pins every
/// <c>throttle: false</c> call site to <see cref="SoundBank.Unthrottled"/>, the one declared list.
/// </summary>
public class sound_throttle_table_test
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return dir!;
    }

    private static string TablePath() => Path.Combine(RepoRoot(), "tools", "asset-pipeline", "sound_throttle.json");

    /// <summary>The table's text, exactly as committed: deterministic (ordinal-sorted keys), LF, one trailing newline.</summary>
    internal static string TableText()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"_generated_by\": \"tests/unit/IdleXIdle.Game.Tests/sound_throttle_table_test.cs from SoundBank - do not edit\",\n");
        sb.Append($"  \"default_min_gap_ms\": {SoundBank.DefaultMinGapMs},\n");
        sb.Append($"  \"repeat_half_life_ms\": {SoundBank.RepeatHalfLifeMs.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n");
        sb.Append("  \"min_gap_ms\": {\n");
        var gaps = SoundBank.ThrottleTable.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        for (var i = 0; i < gaps.Count; i++)
            sb.Append($"    \"{gaps[i].Key}\": {gaps[i].Value}{(i < gaps.Count - 1 ? "," : "")}\n");
        sb.Append("  },\n");
        sb.Append("  \"unthrottled\": [\n");
        var free = SoundBank.Unthrottled.OrderBy(k => k, StringComparer.Ordinal).ToList();
        for (var i = 0; i < free.Count; i++)
            sb.Append($"    \"{free[i]}\"{(i < free.Count - 1 ? "," : "")}\n");
        sb.Append("  ]\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    [Fact]
    public void test_the_throttle_table_file_matches_the_sound_bank_exactly()
    {
        var path = TablePath();
        var want = TableText();
        // a checkout under core.autocrlf=true writes CRLF: the table is the same table, so line endings are not a difference
        var had = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
        if (had != want) File.WriteAllText(path, want, new UTF8Encoding(false));
        Assert.True(had == want,
            had is null
                ? "tools/asset-pipeline/sound_throttle.json was missing: it is written now; commit it."
                : "tools/asset-pipeline/sound_throttle.json was stale against SoundBank: it is regenerated now; commit it.");
    }

    [Fact]
    public void test_the_table_holds_the_bank_gaps_and_its_default()
    {
        Assert.Equal(90, SoundBank.DefaultMinGapMs);
        Assert.Equal(250f, SoundBank.RepeatHalfLifeMs);
        Assert.Equal(60, SoundBank.ThrottleTable["sfx_hit"]);
        Assert.Equal(900, SoundBank.ThrottleTable["sfx_dispatch"]);
        Assert.Contains("\"sfx_enemy_down\": 140", TableText());
        Assert.DoesNotContain("\r", TableText());
    }

    [Fact]
    public void test_the_unthrottled_list_is_the_reveal_tick_the_contact_ticks_and_the_brand_infects()
    {
        Assert.Equal(SoundBank.Unthrottled.OrderBy(k => k, StringComparer.Ordinal), SoundBank.Unthrottled);
        Assert.Equal(SoundBank.Unthrottled.Distinct().Count(), SoundBank.Unthrottled.Count);
        foreach (var key in new[] { "sfx_reveal_tick", "sfx_seeker_spray_tick", "sfx_seeker_brand_infect", "sfx_seeker_brand_ash" })
            Assert.Contains(key, SoundBank.Unthrottled);
        // a throttled cue never sneaks in: PRESS's tick and JAWS' bite go through the gate
        Assert.DoesNotContain("sfx_seeker_press_tick", SoundBank.Unthrottled);
        Assert.DoesNotContain("sfx_seeker_jaws_bite", SoundBank.Unthrottled);
    }

    [Fact]
    public void test_every_recipe_cue_played_past_the_gate_is_declared_unthrottled()
    {
        // every action recipe's contact ticks (played with throttle: false by HuntScreen), by reflection, so a new recipe's
        // tick cannot be left out of the table
        foreach (var f in typeof(ActionRecipes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is { } recipe && recipe.GetType().GetProperty("ContactTickCues")?.GetValue(recipe) is IEnumerable<string> ticks)
                foreach (var k in ticks)
                    Assert.True(SoundBank.Unthrottled.Contains(k), $"{f.Name}'s contact tick '{k}' plays unthrottled but is not declared.");
        // every mark recipe's unthrottled kinds (throttle: !MarkRecipe.Unthrottled(kind))
        foreach (var f in typeof(MarkRecipes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is MarkRecipe mark)
                foreach (var kind in Enum.GetValues<MarkCueKind>().Where(MarkRecipe.Unthrottled))
                    foreach (var k in mark.CuesOf(kind))
                        Assert.True(SoundBank.Unthrottled.Contains(k), $"{f.Name}'s {kind} cue '{k}' plays unthrottled but is not declared.");
    }

    [Fact]
    public void test_every_throttle_false_call_site_is_pinned()
    {
        // The three sites that pass the gate, by what they play. A fourth is a new unthrottled cue: declare it in
        // SoundBank.Unthrottled (and so in sound_throttle.json) and pin it here.
        var src = Path.Combine(RepoRoot(), "src");
        var sites = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}build{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select(l => (File: Path.GetFileName(f), Line: l.Trim())))
            .Where(x => !x.Line.StartsWith("//") && (x.Line.Contains("throttle: false") || x.Line.Contains("throttle: !")))
            .ToList();
        Assert.Equal(3, sites.Count);
        Assert.Contains(sites, x => x.File == "ForgeScreen.cs" && x.Line.Contains("Sound?.Play(\"sfx_reveal_tick\""));
        Assert.Contains(sites, x => x.File == "HuntScreen.cs" && x.Line.StartsWith("Sound.Play(tickKey,"));
        Assert.Contains(sites, x => x.File == "HuntScreen.cs" && x.Line.Contains("throttle: !MarkRecipe.Unthrottled(c.Kind)"));
        // ...and the tick key there is resolved from the recipe's ContactTickCues, which the test above declares
        var hunt = File.ReadAllText(Path.Combine(src, "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("Sound?.Resolve(p.Recipe.ContactTickCues) is { } tickKey", hunt);
    }

    [Fact]
    public void test_film_audio_reads_the_table_and_keeps_no_hand_mirror()
    {
        var py = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "asset-pipeline", "film_audio.py"));
        Assert.Contains("sound_throttle.json", py);
        Assert.DoesNotContain("MIN_GAP_MS = {", py);
        Assert.DoesNotContain("UNTHROTTLED = {", py);
        Assert.DoesNotContain("MIN_REPEAT_MS = 90", py);
    }
}
