using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace ResonanceHunter.Client;

/// <summary>
/// Loads raw WAV audio from disk at startup, keyed by filename, and plays named cues.
/// </summary>
/// <remarks>
/// The audio twin of <see cref="AssetLibrary"/>, and deliberately the same shape: it bypasses the MGCB
/// content pipeline, loads via <see cref="SoundEffect.FromStream"/>, and — most importantly — the game
/// runs perfectly with NO audio present. Every <see cref="Play"/> / <see cref="PlayMusic"/> is a no-op
/// when the cue is missing, so sound drops in file-by-file exactly like the art did.
///
/// It is also defensive about the audio DEVICE: a machine with no sound hardware (or a headless
/// screenshot/CI run) must not crash. Construction and every playback call are guarded, and any audio
/// failure disables the bank rather than propagating.
/// </remarks>
public sealed class SoundBank
{
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _enabled;

    private SoundEffectInstance? _music;
    private string _musicKey = "";

    private float _masterSfx = 0.8f;
    private float _masterMusic = 0.5f;
    private float _musicBase = 1f;   // the track's own volume, so the master can be re-applied live

    /// <summary>Effects master volume, 0..1. Applied to every subsequent one-shot.</summary>
    public float SfxVolume
    {
        get => _masterSfx;
        set => _masterSfx = Clamp01(value);
    }

    /// <summary>Music master volume, 0..1. Applied to the CURRENTLY PLAYING bed immediately.</summary>
    public float MusicVolume
    {
        get => _masterMusic;
        set
        {
            _masterMusic = Clamp01(value);
            try { if (_music is not null) _music.Volume = Clamp01(_musicBase * _masterMusic); }
            catch (Exception) { /* a disposed voice must never break the settings screen */ }
        }
    }

    /// <param name="disable">
    /// Force-off, used for headless screenshot/CI runs where no audio device exists. When true, nothing
    /// is loaded and every call is inert.
    /// </param>
    public SoundBank(bool disable = false)
    {
        if (disable) { _enabled = false; return; }

        var root = Path.Combine(AppContext.BaseDirectory, "assets", "audio");
        if (!Directory.Exists(root)) { _enabled = false; return; }

        var loadedAny = false;
        foreach (var path in Directory.EnumerateFiles(root, "*.wav", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(path);
                _sounds[Path.GetFileNameWithoutExtension(path)] = SoundEffect.FromStream(stream);
                loadedAny = true;
            }
            catch (Exception)
            {
                // A single bad/unsupported WAV must not take audio down — it just stays silent.
            }
        }

        // Only "enable" if at least one sound loaded AND the audio device is actually usable. Touching
        // MasterVolume forces OpenAL init; on hardware-less machines that throws, and we stay silent.
        try
        {
            if (loadedAny) { _ = SoundEffect.MasterVolume; _enabled = true; }
        }
        catch (Exception)
        {
            _enabled = false;
        }
    }

    public int Count => _sounds.Count;
    public bool Enabled => _enabled;
    public bool Has(string key) => _sounds.ContainsKey(key);

    // ── The combat throttle. The fight fires the same cue in bursts — a swarm wave lands five hits
    // inside a frame, and BATTLE SPEED multiplies the playback clock — and five simultaneous copies
    // of one sample are one sample five times as loud. Two rules, applied to EVERY one-shot:
    //   1. the same cue never STARTS twice inside ~90 ms;
    //   2. a cue re-fired while its recent copies still ring plays QUIETER (divided by the square
    //      root of a decaying repeat count), so a swarm reads as a swarm, not as a wall.
    // Centralised here rather than at each call site so no caller can forget it.
    private const long MinRepeatMs = 90;
    private readonly Dictionary<string, (long LastMs, float Recent)> _recent = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Play a one-shot cue. No-op if audio is off or the cue is missing.</summary>
    /// <remarks>
    /// Rate-limited per cue: a repeat inside ~90 ms is dropped, and rapid repeats play progressively
    /// quieter (see the throttle note above). Volume always rides <see cref="SfxVolume"/>, so the
    /// settings slider governs every effect in the game.
    /// </remarks>
    public void Play(string key, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (!_enabled || !_sounds.TryGetValue(key, out var fx)) return;

        var now = Environment.TickCount64;
        if (_recent.TryGetValue(key, out var t))
        {
            var since = now - t.LastMs;
            if (since < MinRepeatMs) return;
            // Half-life 250 ms: a cue that last fired long ago is back to full volume.
            var recent = t.Recent * MathF.Pow(0.5f, since / 250f) + 1f;
            _recent[key] = (now, recent);
            volume /= MathF.Sqrt(recent);
        }
        else _recent[key] = (now, 1f);

        try { fx.Play(Clamp01(volume * _masterSfx), Clamp(pitch, -1f, 1f), Clamp(pan, -1f, 1f)); }
        catch (Exception) { /* an exhausted voice pool must never break a frame */ }
    }

    /// <summary>Play the first present cue among candidates — lets callers try specific-then-generic.</summary>
    public void PlayFirst(float volume, params string[] keys)
    {
        foreach (var k in keys)
            if (_sounds.ContainsKey(k)) { Play(k, volume); return; }
    }

    /// <summary>
    /// Swap the looping music bed. No-op if audio is off, the track is missing, or it is already playing.
    /// </summary>
    public void PlayMusic(string key, float volume = 1f)
    {
        if (!_enabled || key == _musicKey) return;
        if (!_sounds.TryGetValue(key, out var fx)) { StopMusic(); _musicKey = key; return; }

        try
        {
            _music?.Stop();
            _music?.Dispose();
            _music = fx.CreateInstance();
            _music.IsLooped = true;
            _musicBase = Clamp01(volume);
            _music.Volume = Clamp01(volume * _masterMusic);
            _music.Play();
            _musicKey = key;
        }
        catch (Exception) { _music = null; }
    }

    public void StopMusic()
    {
        try { _music?.Stop(); } catch (Exception) { /* ignore */ }
        _music = null;
        _musicKey = "";
    }

    private static float Clamp01(float v) => Clamp(v, 0f, 1f);
    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
