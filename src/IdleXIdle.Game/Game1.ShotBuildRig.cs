using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Game.Rig;

namespace IdleXIdle.Game;

/// <summary>
/// THE SWEEP'S FIGHT DIALS (design.md section 9): <c>RH_SHOT_BUILD</c> poses a take's whole build, <c>RH_SHOT_KEYSTONES</c>
/// sockets keystones into it, <c>RH_SHOT_SEEK</c> aims the shutter at an event. Dev only, inert without the variables, and
/// LOUD: anything they cannot pose throws, and the process exits non-zero (Program.cs) instead of filming a plausible
/// picture of something else.
/// </summary>
public partial class Game1
{
    /// <summary>The fixtures that wear the full loadout (the roads taken, four slots): the only ones a build is posed on.</summary>
    private static bool ShotBuildMode(string sm)
        => sm is "fight" or "fightswing" or "fightflash" or "fightshield" or "fightstatus" or "fightfive"
            or "fightshieldbroken" or "fightmulti" or "fightinspect" or "vfxdebug";

    /// <summary>The fixtures that read a seek into the wave (RH_SHOT_T's list): the only ones a RH_SHOT_SEEK is armed on.</summary>
    private static bool ShotSeekMode(string sm)
        => sm is "fight" or "fightshield" or "fightshieldbroken" or "fightmulti" or "fightinspect" or "fightstatus"
            or "fightfive" or "vfxdebug";

    private static string? ShotDial(string name) => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } v ? v : null;

    /// <summary>
    /// Refuse a sweep dial set on a fixture that never reads it: the take would film the fixture's own build or instant
    /// and look exactly like the one asked for.
    /// </summary>
    private static void RefuseUnreadShotDials(string sm)
    {
        if (ShotDial(ShotBuild.Variable) is not null && !ShotBuildMode(sm))
            throw new InvalidOperationException($"{ShotBuild.Variable} is set, but the '{sm}' fixture poses no build.");
        if (ShotDial("RH_SHOT_KEYSTONES") is not null && !ShotBuildMode(sm))
            throw new InvalidOperationException($"RH_SHOT_KEYSTONES is set, but the '{sm}' fixture poses no build.");
        if (ShotDial(ShotSeek.Variable) is not null && !ShotSeekMode(sm))
            throw new InvalidOperationException($"{ShotSeek.Variable} is set, but the '{sm}' fixture reads no seek.");
    }

    /// <summary>
    /// RH_SHOT_BUILD: the posed champion's loadout becomes exactly the asked build, slot by slot, through the loadout's own
    /// verbs (ClearSkill / AddSkill / SetSkill / SetSource). Refused loudly beside RH_SHOT_SWAP (two dials writing one
    /// loadout), on any refusal of <see cref="ShotBuild.Refusal"/>, and on any write the loadout declines. Returns the asked
    /// entries for <see cref="VerifyShotBuild"/>, or null when the dial is not set.
    /// </summary>
    private System.Collections.Generic.IReadOnlyList<(string SkillId, IdleXIdle.Core.Sources.Source? Source)>? ApplyShotBuild()
    {
        if (ShotDial(ShotBuild.Variable) is not { } spec) return null;
        if (ShotDial("RH_SHOT_SWAP") is not null)
            throw new InvalidOperationException(
                $"{ShotBuild.Variable} and RH_SHOT_SWAP are both set: the build names every slot, so a swap has nothing to swap. Use one.");
        var entries = ShotBuild.Parse(spec);
        var defs = ShotBuild.Resolve(entries);
        if (ShotBuild.Refusal(defs, _loadout.SignatureSkillId, PlayerLoadout.MaxSkills) is { } refusal)
            throw new InvalidOperationException(refusal);

        _loadout.SkillCapacity = Math.Max(_loadout.SkillCapacity, PlayerLoadout.MaxSkills);
        for (var i = 0; i < _loadout.Skills.Count; i++)
            if (!_loadout.IsSignatureSlot(i)) _loadout.ClearSkill(i);
        while (_loadout.Skills.Count < entries.Count)
            if (_loadout.AddSkill() < 0)
                throw new InvalidOperationException($"{ShotBuild.Variable}: the loadout would not open slot {_loadout.Skills.Count + 1}.");
        // THE SIGNATURE HOLDS SLOT ONE (Refusal asked for it there). Woven already, it is lifted; missing (a repair that
        // returned -1 for want of room), it is seated in slot one, which the clearing above has just emptied.
        if (!_loadout.HasSkill(defs[0].Id) && !_loadout.SetSkill(0, defs[0].Id))
            throw new InvalidOperationException($"{ShotBuild.Variable}: the loadout refused the signature '{defs[0].Id}' in slot 1.");
        _loadout.PinSignature();
        for (var i = 0; i < entries.Count; i++)
        {
            if (!_loadout.SetSkill(i, defs[i].Id))
                throw new InvalidOperationException(
                    $"{ShotBuild.Variable}: the loadout refused '{defs[i].Id}' in slot {i + 1} "
                    + $"({_loadout.RefusalFor(i, defs[i])}); nothing is filmed.");
            _loadout.SetSource(i, entries[i].Source ?? ShotBuild.DefaultSource);
        }
        // a slot past the build is taken away, never left empty: the take wears what it asked for and nothing else
        for (var i = _loadout.Skills.Count - 1; i >= entries.Count; i--) _loadout.RemoveSkill(i);
        if (_loadout.Skills.Count != entries.Count)
            throw new InvalidOperationException($"{ShotBuild.Variable}: the loadout kept {_loadout.Skills.Count} slots, not {entries.Count}.");
        return entries;
    }

    /// <summary>
    /// RH_SHOT_KEYSTONES=&lt;id&gt;[,&lt;id&gt;...]: the account KNOWS each keystone (as `fightstatus` seeds it) and the loadout wears it,
    /// the sockets raised to the count. Refused loudly on an unknown id or a socket the loadout declines (more than
    /// <see cref="PlayerLoadout.MaxKeystones"/>). Returns the ids for <see cref="VerifyShotBuild"/>, or null when unset.
    /// </summary>
    private string[]? ApplyShotKeystones()
    {
        if (ShotDial("RH_SHOT_KEYSTONES") is not { } spec) return null;
        var ids = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0 || ids.Length > PlayerLoadout.MaxKeystones || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidOperationException(
                $"RH_SHOT_KEYSTONES='{spec}' needs 1-{PlayerLoadout.MaxKeystones} distinct keystone ids.");
        foreach (var id in ids)
            if (Keystones.Catalog.All(k => k.Id != id))
                throw new InvalidOperationException(
                    $"RH_SHOT_KEYSTONES: '{id}' is not a keystone id. Known: {string.Join(", ", Keystones.Catalog.Select(k => k.Id))}.");
        foreach (var id in ids) _discoveredKeystones.Add(id);
        RebuildBuildMenus();
        _loadout.KeystoneCapacity = Math.Max(_loadout.KeystoneCapacity, ids.Length);
        foreach (var id in ids)
            if (!_loadout.HasKeystone(id) && !_loadout.ToggleKeystone(id, _keystoneMenu))
                throw new InvalidOperationException(
                    $"RH_SHOT_KEYSTONES: '{id}' cannot be socketed (sockets {_loadout.KeystoneCapacity}, worn: "
                    + $"{string.Join(", ", _loadout.KeystoneIds)}).");
        return ids;
    }

    /// <summary>
    /// Once the fixture has started its run: the LIVE RUN must carry exactly the asked build (ids and Sources, in order) and
    /// the asked keystones, or the take throws. This is the check that catches a silent repair (EnsureSignature's -1, a road
    /// or an owner the composer refuses) after every loadout write was accepted. It runs AT THE SHUTTER
    /// (<see cref="HuntScreen.DevCheckRunAtShutter"/>), not here: the run a fixture starts is composed before the frame
    /// pushes the posed champion to the hunt screen, and the host restarts it on its first live frame. Measured: checked at
    /// DevStart, THE QUIVER's BACKDRAW was missing from a run whose restarted twin, the one filmed, carried it.
    /// </summary>
    private void VerifyShotBuild(System.Collections.Generic.IReadOnlyList<(string SkillId, IdleXIdle.Core.Sources.Source? Source)>? build,
                                 string[]? keystones)
    {
        if (build is null && keystones is null) return;
        // WARMED HERE, on the fixture's frame, and the answer thrown away (this run is not the one filmed): the first call
        // JITs the check and its line, so the shutter's frame does only the comparison. Cheap insurance against a stall on
        // the photographed frame; it was NOT the cause of ref_fast's 3 px body difference (that is the take's cold start,
        // see action_regression.py's HOST_X).
        _ = ShotRunMismatch(build, keystones, print: false);
        _ = VerifiedLine(_expedition.DevRunSkills(), keystones);
        _expedition.DevCheckRunAtShutter(() => ShotRunMismatch(build, keystones, print: true));
    }

    /// <summary>Why the live run is not the asked build, or null (then the verified build is printed to the take's log).</summary>
    private string? ShotRunMismatch(System.Collections.Generic.IReadOnlyList<(string SkillId, IdleXIdle.Core.Sources.Source? Source)>? build,
                                    string[]? keystones, bool print)
    {
        var live = _expedition.DevRunSkills();
        if (live.Count == 0) return $"{ShotBuild.Variable} / RH_SHOT_KEYSTONES: the fixture started no run to verify.";
        if (build is not null && ShotBuild.Mismatch(build, live) is { } mismatch) return mismatch;
        if (keystones is not null)
        {
            var worn = _expedition.DevComposedKeystoneIds();
            if (keystones.FirstOrDefault(k => !worn.Contains(k)) is { } missing)
                return $"RH_SHOT_KEYSTONES: the fight composes without '{missing}' (composed: {string.Join(", ", worn)}).";
        }
        if (print) Console.WriteLine(VerifiedLine(live, keystones));
        return null;
    }

    private static string VerifiedLine(System.Collections.Generic.IReadOnlyList<EquippedSkill> live, string[]? keystones)
        => "RH_SHOT_BUILD verified at the shutter: "
           + string.Join(", ", live.Select((s, i) => $"slot {i} {s.Def.Id}@{s.Source}"))
           + (keystones is null ? "" : "; keystones " + string.Join(", ", keystones));

    /// <summary>
    /// RH_SHOT_SEEK: the shutter lands RH_SHOT_LEAD before the event the predicate finds in the wave (resolved on the frame
    /// the seek applies, against the live run's slots), and a wave without one THROWS. Refused beside RH_SHOT_T and
    /// RH_SHOT_BITE (three dials aiming one shutter).
    /// </summary>
    private void ArmShotSeek()
    {
        if (ShotDial(ShotSeek.Variable) is not { } spec) return;
        if (ShotDial("RH_SHOT_T") is not null || Environment.GetEnvironmentVariable("RH_SHOT_BITE") == "1")
            throw new InvalidOperationException($"{ShotSeek.Variable} is set beside RH_SHOT_T or RH_SHOT_BITE: one dial aims the shutter.");
        var aim = ShotSeek.Parse(spec);
        _expedition.DevSeekBefore(events => aim.Find(events, _expedition.DevSlotOf), ShotLead(), $"{ShotSeek.Variable}='{aim}'");
    }
}
