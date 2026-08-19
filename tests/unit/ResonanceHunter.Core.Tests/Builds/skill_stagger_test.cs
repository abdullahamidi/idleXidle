using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// Skills cast one at a time, in slot order — never four flashes on the same tick.
/// </summary>
/// <remarks>
/// Playtest: "Skiller sırasıyla atılmalı, hepsini bir anda atıyor." Every ready skill used to fire on
/// the same 100ms tick. The fix is a global <see cref="SoloBattle.CastGapMs"/> lock: a deferred skill
/// stays READY (its cooldown is not consumed), so the stagger spreads casts without eating them. This
/// test asserts the END of that chain, on the same sim the game runs.
/// </remarks>
public class SkillStaggerTest
{
    private static EquippedSkill Sk(Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = Source.Nature, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    [Fact]
    public void test_no_two_casts_share_an_instant_and_every_gap_honours_the_lock()
    {
        // Four CASTING forms (Aura is passive and emits no Skill event; Trap fires on being bitten).
        // No triggers/keystones, so every Skill event is its own activation — Echo and Weaver
        // deliberately fire twice within ONE activation and are out of scope here.
        var build = new Build();
        foreach (var f in new[] { Form.Strike, Form.Projectile, Form.Transformation, Form.Mark })
            build.Weave(Sk(f));

        var champ = new Champion { MaxHealth = 200_000, Health = 200_000 };
        // One enormous creature, so the wave runs long enough for many casts of every skill.
        var creatures = new List<WaveCreature>
        {
            new() { MaxHealth = 50_000_000, Health = 50_000_000, Damage = 1f },
        };

        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), creatures, enemyIntervalMs: 900,
            ExpeditionTuning.Default, new Random(11), metrics: new WaveMetrics());

        var casts = events.Where(e => e.Kind == BattleEventKind.Skill)
                          .Select(e => e.AtMs).OrderBy(t => t).ToList();

        Assert.True(casts.Count >= 8, $"expected a long fight with many casts, saw {casts.Count}");
        for (var i = 1; i < casts.Count; i++)
            Assert.True(casts[i] - casts[i - 1] >= SoloBattle.CastGapMs,
                $"casts at {casts[i - 1]}ms and {casts[i]}ms are only {casts[i] - casts[i - 1]}ms apart "
                + $"— the {SoloBattle.CastGapMs}ms one-at-a-time lock is not being honoured.");
    }
}
