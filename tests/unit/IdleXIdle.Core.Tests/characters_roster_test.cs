using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests;

/// <summary>
/// The characters GDD's acceptance criteria, as tests.
/// </summary>
/// <remarks>
/// The last one — "every passive is read by the sim" — is the one that matters most and the one a
/// human review keeps missing. This project's single most expensive bug was a build value resolved,
/// carried and summed by correct code at every step and read by nothing at all for the whole of
/// development, and ten characters is ten fresh chances to do it again.
/// </remarks>
public class CharactersRosterTest
{
    [Fact]
    public void test_roster_is_ten_characters_with_unique_ids()
    {
        Assert.Equal(10, CharacterRoster.All.Count);
        Assert.Equal(10, CharacterRoster.All.Select(c => c.Id).Distinct().Count());
        Assert.Equal(10, CharacterRoster.All.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void test_fresh_state_unlocks_only_the_starter()
    {
        var state = new CharacterState();
        Assert.Equal(CharacterRoster.StarterId, state.ActiveId);
        Assert.Single(state.Unlocked);
        Assert.True(state.IsUnlocked(CharacterRoster.StarterId));
    }

    [Fact]
    public void test_conquest_unlocks_its_character_and_reports_it_once()
    {
        var state = new CharacterState();
        var first = state.Refresh(new[] { "cinderworks" });

        // Cinderworks gates exactly ONE character now — THE MAGPIE used to arrive on the same frame,
        // which is the "everything unlocks at once" the tiered roster exists to end.
        Assert.Single(first);
        Assert.Equal("anvil", first[0].Id);
        Assert.False(state.IsUnlocked("magpie"));

        // Idempotent: the same conquest a second time is not a second unlock.
        Assert.Empty(state.Refresh(new[] { "cinderworks" }));
    }

    [Fact]
    public void test_quest_gated_characters_stay_locked_until_the_quest_is_done()
    {
        var state = new CharacterState();
        var everyRegion = CharacterRoster.All
            .Where(c => c.Unlock.Kind == UnlockKind.Conquest)
            .Select(c => c.Unlock.RegionId!)
            .ToList();
        state.Refresh(everyRegion);

        Assert.False(state.IsUnlocked("quiver"));
        Assert.False(state.IsUnlocked("oathbound"));

        state.CompleteQuest("q_three_vows");
        var fresh = state.Refresh(everyRegion);
        Assert.Single(fresh);
        Assert.Equal("oathbound", fresh[0].Id);
        Assert.False(state.IsUnlocked("quiver"));
    }

    [Fact]
    public void test_a_locked_character_cannot_be_selected()
    {
        var state = new CharacterState();
        Assert.False(state.Select("anvil"));
        Assert.Equal(CharacterRoster.StarterId, state.ActiveId);

        state.Refresh(new[] { "cinderworks" });
        Assert.True(state.Select("anvil"));
        Assert.Equal("anvil", state.ActiveId);
    }

    [Fact]
    public void test_restore_falls_back_to_the_starter_on_an_unknown_id()
    {
        var state = new CharacterState();
        state.Restore("a_character_that_was_deleted", null);
        Assert.Equal(CharacterRoster.StarterId, state.ActiveId);
    }

    [Fact]
    public void test_you_never_lose_the_character_you_were_playing()
    {
        // Restored mid-career as ANVIL, but with no conquests yet recorded — the rules that granted
        // them are gone. Refusing to load them would strand the player as somebody else.
        var state = new CharacterState();
        state.Restore("anvil", null);
        Assert.Equal("anvil", state.ActiveId);
        Assert.True(state.IsUnlocked("anvil"));
    }

    [Fact]
    public void test_quests_done_survive_a_save_round_trip()
    {
        var state = new CharacterState();
        state.CompleteQuest("q_three_vows");
        var saved = state.SaveQuests();

        var loaded = new CharacterState();
        loaded.Restore("seeker", saved);
        Assert.True(loaded.QuestDone("q_three_vows"));
        Assert.False(loaded.QuestDone("q_magpie_chests"));
    }

    [Fact]
    public void test_a_character_style_power_lifts_only_its_own_style()
    {
        // Aptitude is gone (P3c): a character's whole contribution is Shape, Mods and Grants. The one
        // style bonus left in the roster is THE THORNWALL's — its card's second sentence, "Every trap
        // you set does more", carried in its Shape where the sim actually reads it.
        var thornwall = CharacterRoster.Get("thornwall");

        var shape = thornwall.Shape;

        Assert.Equal(1.35f, shape.StylePowerFor(Style.Snare), 3);
        Assert.Equal(1f, shape.StylePowerFor(Style.Hammer));
        Assert.Equal(1f, shape.StylePowerFor(Style.Volley));
    }

    [Fact]
    public void test_quiver_passive_is_carried_by_grants_and_skill_rate()
    {
        // This test's slot used to pin THE QUIVER's Projectile aptitude. Aptitudes were unadvertised
        // bonuses and are deleted; what the character really contributes now is the trigger its card
        // names (LOOSE AGAIN) and a skill-rate lift — the fields the sim reads.
        var quiver = CharacterRoster.Get("quiver");

        Assert.Contains(BuildTrigger.LooseAgain, quiver.Grants);
        Assert.Equal(1.10f, quiver.Shape.SkillRate, 3);
    }

    [Fact]
    public void test_style_powers_compound_with_a_tree_node_on_the_same_style()
    {
        // Multiplicative, not "the larger wins". A character shaped for Snares and a Style
        // specialisation node on Snare should both matter.
        var a = new SkillShape { StylePower = new Dictionary<Style, float> { [Style.Snare] = 1.35f } };
        var b = new SkillShape { StylePower = new Dictionary<Style, float> { [Style.Snare] = 1.20f } };
        var combined = SkillShape.Combine(a, b);
        Assert.Equal(1.62f, combined.StylePowerFor(Style.Snare), 3);
    }

    [Fact]
    public void test_every_character_changes_something_the_sim_reads()
    {
        // The acceptance criterion that exists because of BuildMods.Rarity: a passive that is only
        // screen text is a passive that does nothing, and it looks exactly like one that works.
        foreach (var c in CharacterRoster.All)
        {
            var touchesMods = c.Mods != BuildMods.None;
            var touchesShape = c.Shape != SkillShape.None;
            var grants = c.Grants.Count > 0;
            Assert.True(touchesMods || touchesShape || grants,
                        $"{c.Name} contributes nothing the simulation reads.");
        }
    }

    [Fact]
    public void test_twice_sworn_actually_makes_a_vow_pay_more()
    {
        // THE TEST THE ONE ABOVE COULD NOT BE. `test_every_character_changes_something_the_sim_reads`
        // passes if ANY of the three channels is non-default, and THE OATHBOUND's Shape carried its
        // Mark half — so the character sailed through it while the clause it is NAMED for, "Vows pay
        // far more", had no field in SkillShape to write to at all. A passive with two promises needs
        // a test per promise.
        var oathbound = CharacterRoster.All.Single(c => c.Id == "oathbound");
        Assert.True(oathbound.Shape.VowPowerMultiplier > 1f,
                    "TWICE SWORN says Vows pay far more; nothing in its Shape says so");
        Assert.True(oathbound.Shape.MarkWindowMultiplier > 1f,
                    "…and the other half of the same sentence must still hold");
    }

    [Fact]
    public void test_a_vow_power_multiplier_scales_the_bonus_and_not_the_baseline()
    {
        // The formulation matters more than the number. A Vow worth x1.90 pays +0.90, and TWICE SWORN
        // scales THAT. Scaling the whole factor would pay out on a build with no Vow sworn — a flat
        // damage bonus wearing a Vow's name, worth most to the player ignoring the system it is about.
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");
        var plain = DamageWith(SkillShape.None, vow);
        var sworn = DamageWith(SkillShape.None with { VowPowerMultiplier = 1.5f }, vow);
        var noVowPlain = DamageWith(SkillShape.None, null);
        var noVowTwice = DamageWith(SkillShape.None with { VowPowerMultiplier = 1.5f }, null);

        Assert.True(sworn > plain, $"a sworn Vow must pay more under TWICE SWORN — {sworn:N0} vs {plain:N0}");
        Assert.Equal(noVowPlain, noVowTwice);
    }

    /// <summary>Damage a one-skill build lands over a fixed window, with the Vow sworn on that skill.</summary>
    private static float DamageWith(SkillShape shape, Vow? vow)
    {
        var build = new Build { PassiveMods = BuildMods.None, Shape = shape };
        // The SkillId is the identity the sim reads now (P3c); the Form is the legacy fixture bridge,
        // and the cooldown argument is inert — cadence comes from the catalogue def's Beats.
        build.Weave(new EquippedSkill(
            new WovenAbility { Name = "S", Source = Source.Nature, Form = Form.Strike, Vow = vow, SkillId = "hammer_blow" },
            FormBehaviour.BaseCooldownMs(Form.Strike)));

        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var target = new WaveCreature { MaxHealth = 1e9f, Health = 1e9f, Damage = 0f };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), new[] { target },
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(11));
        return events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
    }

    [Fact]
    public void test_every_character_is_described_to_the_player()
    {
        foreach (var c in CharacterRoster.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name), $"{c.Id} has no name");
            Assert.False(string.IsNullOrWhiteSpace(c.Blurb), $"{c.Id} has no blurb");
            Assert.False(string.IsNullOrWhiteSpace(c.PassiveName), $"{c.Id} has no passive name");
            Assert.False(string.IsNullOrWhiteSpace(c.PassiveText), $"{c.Id} has no passive text");
        }
    }

    [Fact]
    public void test_every_unlock_names_a_real_gate()
    {
        foreach (var c in CharacterRoster.All)
            switch (c.Unlock.Kind)
            {
                case UnlockKind.Conquest:
                    Assert.NotNull(c.Unlock.RegionId);
                    // A region id that does not exist is an unlock that can never fire, and nothing
                    // else in the game would ever say so.
                    Assert.True(IdleXIdle.Core.Encounters.Regions.Find(c.Unlock.RegionId!) is not null,
                                $"{c.Name} waits on region '{c.Unlock.RegionId}', which does not exist.");
                    break;
                case UnlockKind.Quest:
                    Assert.False(string.IsNullOrWhiteSpace(c.Unlock.QuestId));
                    Assert.False(string.IsNullOrWhiteSpace(c.Unlock.QuestText));
                    break;
            }
    }

    [Fact]
    public void test_sprite_and_strip_keys_follow_the_asset_naming()
    {
        var c = CharacterRoster.Get("magpie");
        Assert.Equal("char_magpie_base", c.SpriteKey);
        Assert.Equal("char_magpie_idle_strip8_512", c.StripKey("idle"));
        Assert.Equal("char_magpie_attack_strip8_512", c.StripKey("attack"));
        // The 2026-08-22 art pass added cast and death clips and a per-character HUD portrait; the
        // screens ask for these by name, so the names are part of the contract.
        Assert.Equal("char_magpie_cast_strip8_512", c.StripKey("cast"));
        Assert.Equal("char_magpie_death_strip8_512", c.StripKey("death"));
        Assert.Equal("char_magpie_portrait", c.PortraitKey);
    }
}
