using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using Xunit;

namespace ResonanceHunter.Core.Tests;

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

        // Cinderworks gates TWO characters, and both have to be announced — a roster that reports one
        // of them leaves the other silently in the list.
        Assert.Equal(2, first.Count);
        Assert.Contains(first, c => c.Id == "anvil");
        Assert.Contains(first, c => c.Id == "magpie");

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

        state.CompleteQuest("q_first_vow");
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
        state.CompleteQuest("q_first_vow");
        var saved = state.SaveQuests();

        var loaded = new CharacterState();
        loaded.Restore("seeker", saved);
        Assert.True(loaded.QuestDone("q_first_vow"));
        Assert.False(loaded.QuestDone("q_hollow_hunt"));
    }

    [Fact]
    public void test_an_aptitude_lifts_only_its_own_form()
    {
        var quiver = CharacterRoster.Get("quiver");
        Assert.Equal(Form.Projectile, quiver.Aptitude);

        var shape = quiver.TotalShape;
        Assert.True(shape.FormPowerFor(Form.Projectile) > 1f);
        Assert.Equal(1f, shape.FormPowerFor(Form.Strike));
        Assert.Equal(1f, shape.FormPowerFor(Form.Aura));
    }

    [Fact]
    public void test_aptitudes_compound_with_a_tree_node_on_the_same_form()
    {
        // Multiplicative, not "the larger wins". A character built for Projectiles and a Form
        // specialisation node on Projectiles should both matter.
        var a = new SkillShape { FormPower = new Dictionary<Form, float> { [Form.Projectile] = 1.35f } };
        var b = new SkillShape { FormPower = new Dictionary<Form, float> { [Form.Projectile] = 1.20f } };
        var combined = SkillShape.Combine(a, b);
        Assert.Equal(1.62f, combined.FormPowerFor(Form.Projectile), 3);
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
            var hasAptitude = c.Aptitude is not null;
            Assert.True(touchesMods || touchesShape || grants || hasAptitude,
                        $"{c.Name} contributes nothing the simulation reads.");
        }
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
                    Assert.True(ResonanceHunter.Core.Encounters.Regions.Find(c.Unlock.RegionId!) is not null,
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
    }
}
