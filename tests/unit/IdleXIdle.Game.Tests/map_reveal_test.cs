using IdleXIdle.Core.Encounters;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The MAP's reveal pulse (UI polish brief §73, §30–§32): a region that has JUST become available pulses
/// ONCE, keyed to the region, at the Reward length — and never again this session. The pulse is armed from
/// <see cref="MapScreen.Update"/>, so the state machine is driven here with no GraphicsDevice: the screen's
/// constructor only stores its kit, and Update never draws.
/// </summary>
/// <remarks>
/// The capture rig can only photograph the frozen peak (RH_SHOT_REVEAL); the fade between the peak and rest is
/// what these tests read, at t = 0, mid and end, in both motion settings.
/// </remarks>
public class MapRevealTests
{
    private const string Home = VerdantHollow.RegionId;
    private const string Next = "cinderworks";

    private static MapScreen Fresh(World world)
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        UiMotion.Tick(1f / 60f);
        var screen = new MapScreen(null!) { World = world, ActiveRegion = Home };
        return screen;
    }

    private static void Frame(MapScreen screen) => screen.Update(default(KeyboardState), default(KeyboardState), new Point(-1, -1), false);

    /// <summary>Let the clock run, at 60 Hz — <see cref="UiMotion.Tick"/> clamps one step to 100 ms, as the host's frame does.</summary>
    private static void Advance(float seconds)
    {
        const float step = 1f / 60f;
        for (var t = 0f; t < seconds - 1e-4f; t += step) UiMotion.Tick(step);
    }

    [Fact]
    public void test_first_sight_of_the_world_pulses_nothing()
    {
        var world = new World();
        var screen = Fresh(world);

        Frame(screen);

        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Home)));
        Assert.False(UiMotion.Pulsing(MapScreen.RevealKey(Home)));
        Assert.Null(screen.ConsumeCue());
    }

    [Fact]
    public void test_a_region_conquered_open_since_the_last_look_pulses_once_at_reward_length()
    {
        var world = new World();
        var screen = Fresh(world);
        Frame(screen);
        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Next)));

        // The hunt conquers the home region; the next one is unlocked by the time the map is looked at again.
        world.Conquer(Home);
        Frame(screen);

        var key = MapScreen.RevealKey(Next);
        Assert.Equal(1f, UiMotion.Pulse(key));                       // t = 0: the peak
        Assert.Equal("sfx_reveal_tick", screen.ConsumeCue());        // the cue names the moment, once
        Assert.Null(screen.ConsumeCue());
        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Home)));  // the conquered region is not "newly available"

        Advance(UiMotion.Reward / 2f);
        Assert.InRange(UiMotion.Pulse(key), 0.45f, 0.55f);           // mid: half of it left

        Advance(UiMotion.Reward);
        Assert.Equal(0f, UiMotion.Pulse(key));                       // end: gone
        Assert.False(UiMotion.Pulsing(key));
    }

    [Fact]
    public void test_a_later_look_at_the_same_world_does_not_re_arm_the_pulse()
    {
        var world = new World();
        var screen = Fresh(world);
        Frame(screen);
        world.Conquer(Home);
        Frame(screen);
        Advance(UiMotion.Reward * 2f);
        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Next)));
        screen.ConsumeCue();

        // Sixty more frames on the same world: nothing new has happened, so nothing pulses and nothing sounds.
        for (var i = 0; i < 60; i++) { Frame(screen); UiMotion.Tick(1f / 60f); }

        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Next)));
        Assert.False(UiMotion.Pulsing(MapScreen.RevealKey(Next)));
        Assert.Null(screen.ConsumeCue());
    }

    [Fact]
    public void test_reduced_motion_keeps_the_same_short_fade_and_the_same_end_state()
    {
        var world = new World();
        var screen = Fresh(world);
        UiMotion.Reduced = true;
        Frame(screen);
        world.Conquer(Home);
        Frame(screen);

        var key = MapScreen.RevealKey(Next);
        Assert.Equal(1f, UiMotion.Pulse(key));
        Advance(UiMotion.Reward / 2f);
        Assert.InRange(UiMotion.Pulse(key), 0.45f, 0.55f);
        Advance(UiMotion.Reward);
        Assert.Equal(0f, UiMotion.Pulse(key));
        UiMotion.Reduced = false;
    }

    [Fact]
    public void test_each_region_that_opens_pulses_on_its_own_key_and_only_when_it_opens()
    {
        var world = new World();
        var screen = Fresh(world);
        Frame(screen);

        world.Conquer(Home);
        Frame(screen);
        Assert.Equal(1f, UiMotion.Pulse(MapScreen.RevealKey(Next)));
        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey("umbral_reach")));
        Advance(UiMotion.Reward * 2f);

        world.Conquer(Next);
        Frame(screen);
        Assert.Equal(1f, UiMotion.Pulse(MapScreen.RevealKey("umbral_reach")));
        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Next)));
        Assert.Equal("sfx_reveal_tick", screen.ConsumeCue());
    }

    /// <summary>
    /// START A NEW GAME builds a new <see cref="World"/> and keeps this screen alive, so the memo of
    /// "already shown available" has to belong to the world it was gathered from. Without that, the first
    /// region a new career opens — the one reveal a new player would ever see — is remembered from the old
    /// world and pulses nothing.
    /// </summary>
    [Fact]
    public void test_a_new_world_forgets_what_the_old_one_had_already_shown()
    {
        var old = new World();
        var screen = Fresh(old);
        Frame(screen);
        old.Conquer(Home);
        Frame(screen);                                   // cinderworks reveals in the OLD world
        Advance(UiMotion.Reward * 2f);
        screen.ConsumeCue();

        // A new game: same ids, a different World.
        var fresh = new World();
        screen.World = fresh;
        Frame(screen);                                   // the new world's first look only re-seeds
        Assert.Equal(0f, UiMotion.Pulse(MapScreen.RevealKey(Next)));
        Assert.Null(screen.ConsumeCue());

        fresh.Conquer(Home);
        Frame(screen);
        Assert.Equal(1f, UiMotion.Pulse(MapScreen.RevealKey(Next)));
        Assert.Equal("sfx_reveal_tick", screen.ConsumeCue());
    }

    [Fact]
    public void test_arrow_keys_pick_a_region_with_a_click_and_enter_on_a_locked_one_is_an_error()
    {
        var world = new World();
        var screen = Fresh(world);
        Frame(screen);

        var right = new KeyboardState(Keys.Right);
        screen.Update(right, default(KeyboardState), new Point(-1, -1), false);   // home → cinderworks, which is locked
        Assert.Equal("sfx_click", screen.ConsumeCue());

        var enter = new KeyboardState(Keys.Enter);
        screen.Update(enter, default(KeyboardState), new Point(-1, -1), false);
        Assert.Equal("sfx_error", screen.ConsumeCue());
        Assert.Null(screen.ConsumeEnter());

        var left = new KeyboardState(Keys.Left);
        screen.Update(left, default(KeyboardState), new Point(-1, -1), false);    // back to home, which is open
        screen.ConsumeCue();
        screen.Update(enter, default(KeyboardState), new Point(-1, -1), false);
        Assert.Equal("sfx_nav", screen.ConsumeCue());
        Assert.Equal(Home, screen.ConsumeEnter());
    }
}
