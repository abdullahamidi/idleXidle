using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Characters;

namespace IdleXIdle.Game;

/// <summary>
/// Loads the raw PNG assets from disk at startup, keyed by filename (no extension, no folder).
/// </summary>
/// <remarks>
/// Deliberately bypasses the MGCB content pipeline. The art is small, flat, point-sampled pixel art —
/// there is nothing an .xnb build would add — and loading via <see cref="Texture2D.FromStream"/> keeps
/// adding a new sprite a zero-config drop-in. The .csproj copies <c>assets/art/**</c> to the output
/// directory; this scans that copy recursively, so the on-disk folder grouping (creatures/items/ui) is
/// cosmetic and does not affect keys.
///
/// The game runs fine with NO assets present — every draw site falls back to the flat-shape rendering
/// that predates the art. That keeps the greybox playable and means a missing or malformed PNG degrades
/// gracefully instead of crashing.
///
/// <para>
/// THE HEAVY FAMILIES LOAD ON FIRST USE (2026-09-16). Every animation strip is 8 x 512 frames, 8 MB
/// resident, and the game ships ten champions' clips and effects, six bosses' and twenty-four creatures',
/// of which one champion, one boss and one region's four creatures are ever on screen together. Loading them
/// all at boot held about 1.8 GB against the 512 MB working ceiling
/// (<c>.claude/docs/technical-preferences.md</c>). So <see cref="IsDeferred"/> paths are only INDEXED at
/// boot; a key loads the first time something asks for its texture, and the screens that know what
/// they are about to draw ask <see cref="Warm"/> from Update, so the decode never lands in a Draw.
/// <see cref="Has"/> answers from the index and never loads. Nothing is evicted: a texture, once
/// loaded, stays for the session (caches elsewhere hold texture references, and a disposed texture
/// drawn later would throw).
/// </para>
/// </remarks>
public sealed class AssetLibrary
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly GraphicsDevice _device;

    /// <summary>Winning source path per key, so collision tie-breaks stay deterministic.</summary>
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Indexed but not yet loaded: key to the PNG it will load from (see <see cref="IsDeferred"/>).</summary>
    private readonly Dictionary<string, string> _deferred = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The deferred index's pixel area per key, for the same larger-wins collision rule the eager load keeps.</summary>
    private readonly Dictionary<string, long> _deferredArea = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Does this runtime path wait for first use rather than load at boot? The animation strips (every
    /// champion's, boss's and creature's clips), the creatures' stills, and each champion's own effect
    /// strips (<c>VFX/&lt;champion id&gt;_&lt;form&gt;</c>): the families only one of which is on screen at a time.
    /// </summary>
    public static bool IsDeferred(string assetPath)
    {
        var norm = assetPath.Replace('\\', '/');
        if (norm.Contains("/Animations/", StringComparison.OrdinalIgnoreCase)
            || norm.Contains("/Enemies/enemies/", StringComparison.OrdinalIgnoreCase))
            return true;
        var vfx = norm.IndexOf("/VFX/", StringComparison.OrdinalIgnoreCase);
        if (vfx < 0) return false;
        var folder = norm.AsSpan(vfx + 5);
        foreach (var id in ChampionIds)
            if (folder.Length > id.Length && folder.StartsWith(id, StringComparison.OrdinalIgnoreCase) && folder[id.Length] == '_')
                return true;
        return false;
    }

    /// <summary>Every roster champion's id — the heads of the per-champion effect folders.</summary>
    private static readonly string[] ChampionIds = CharacterRoster.All.Select(c => c.Id).ToArray();

    /// <summary>How many textures are loaded right now (masks included), for the boot report and the tests.</summary>
    public int LoadedCount => _textures.Count;

    /// <summary>How many indexed textures have not been asked for yet.</summary>
    public int DeferredCount => _deferred.Count;

    /// <summary>Resident texture memory right now, in bytes (RGBA8: width x height x 4 per texture).</summary>
    public long ResidentBytes
    {
        get
        {
            long total = 0;
            foreach (var tex in _textures.Values) total += (long)tex.Width * tex.Height * 4;
            return total;
        }
    }

    public AssetLibrary(GraphicsDevice device)
    {
        _device = device;
        var root = Path.Combine(AppContext.BaseDirectory, "assets", "art");
        if (!Directory.Exists(root)) return;

        foreach (var path in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
        {
            // Gameplay uses the NORMALIZED canvases. Skip the tightly-cropped `native/` variants (they share
            // basenames with normalized and would collide in this flat filename→texture map) and the
            // `preview/` source sheets (not runtime art).
            var norm = path.Replace('\\', '/');
            if (norm.Contains("/native/") || norm.Contains("/preview/")) continue;
            // package_08 ships every glyph as color/mask/medallion under the SAME basename; keep the
            // full-colour variant and skip the other two so they don't collide in this flat filename map.
            if (norm.Contains("/mask/") || norm.Contains("/medallion/")) continue;

            // Rev 4 §5: after the folder skips, everything remaining is a runtime candidate. Assert it is
            // NOT a preview / source-reference / concept / contact-sheet asset before we ever open it — a
            // forbidden asset must never reach the screen, so a match is a hard error, not a silent skip.
            ValidateRuntimeAssetPath(path);

            if (IsDeferred(norm))
            {
                Index(path, norm);
                continue;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var texture = Texture2D.FromStream(device, stream);
                Premultiply(texture);

                // 98 basenames exist at two or three runtime paths with DIFFERENT content —
                // typically a full-size sprite plus a smaller thumbnails/ copy. Keys are flat
                // basenames, so "last writer wins" made the winner depend on directory
                // enumeration order: the same item could render full-res or thumbnail-res
                // between runs. Resolve deterministically and in favour of quality by keeping
                // the larger image; equal areas tie-break on path so the choice is stable.
                var key = Path.GetFileNameWithoutExtension(path);
                if (_textures.TryGetValue(key, out var existing))
                {
                    var incomingArea = texture.Width * texture.Height;
                    var existingArea = existing.Width * existing.Height;
                    var keepIncoming = incomingArea > existingArea
                        || (incomingArea == existingArea
                            && string.CompareOrdinal(norm, _sources[key]) < 0);
                    if (!keepIncoming)
                    {
                        texture.Dispose();
                        continue;
                    }
                    existing.Dispose();
                }

                _textures[key] = texture;
                _sources[key] = norm;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // A single corrupt PNG must not take the whole game down — it just falls back to shapes.
            }
        }
        LoadFixtureOverrides();
    }

    /// <summary>
    /// CAPTURE FIXTURE ONLY (<c>RH_SHOT_STRIP_FILES=key=path;key=path</c>): replace loaded textures with files from
    /// OUTSIDE the asset tree, so a prototype strip (an enemy-attack key-pose study, a stand-in body) can be filmed
    /// through the real draw without touching a repo asset. The game never sets it; inert without the variable.
    /// </summary>
    private void LoadFixtureOverrides()
    {
        if (Environment.GetEnvironmentVariable("RH_SHOT_STRIP_FILES") is not { Length: > 0 } spec) return;
        foreach (var pair in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) throw new InvalidOperationException($"RH_SHOT_STRIP_FILES entry '{pair}' is not key=path.");
            var key = pair[..eq].Trim();
            var path = pair[(eq + 1)..].Trim();
            if (!File.Exists(path)) throw new InvalidOperationException($"RH_SHOT_STRIP_FILES: '{path}' does not exist.");
            using var stream = File.OpenRead(path);
            var texture = Texture2D.FromStream(_device, stream);
            Premultiply(texture);
            if (_textures.TryGetValue(key, out var existing)) existing.Dispose();
            _textures[key] = texture;
            _sources[key] = path.Replace('\\', '/');
            _deferred.Remove(key);
        }
    }

    /// <summary>
    /// Rev 4 §5: reject preview / source-reference / concept / contact-sheet assets at load time so a
    /// non-runtime texture can never reach the screen. A forbidden load is a hard error, not a warning.
    /// </summary>
    public static void ValidateRuntimeAssetPath(string assetPath)
    {
        var normalized = assetPath.Replace('\\', '/').ToLowerInvariant();
        string[] forbidden =
        {
            "/preview/", "source_reference", "source_sheet", "concept",
            "contact_sheet", "runtime_assets_preview", "poster", "showcase",
            "mood_board", "pitchboard"
        };
        foreach (var token in forbidden)
            if (normalized.Contains(token))
                throw new InvalidOperationException($"Forbidden runtime asset: {assetPath}");
    }

    public int Count => _textures.Count;

    /// <summary>
    /// Legacy code-key → vector-pack filename. The screens ask for the names the greybox used; the art pack
    /// ships its own names. This one table migrates them in a single reviewable place instead of editing a
    /// dozen call sites — and it is NOT substitution: each alias points a key at the ONE asset that depicts
    /// exactly that thing (see design/art/asset-integration-spec.md §1/§5). A key with no alias and no file
    /// stays null, so its draw site keeps its old flat shape rather than borrowing someone else's art.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ui_gleam_coin"] = "currency_gleam",       // package_05 currencies
        ["ui_memory_dust"] = "currency_memory_dust",
        // Generic per-slot glyphs. The previous item_<slot> targets never existed on disk, so every
        // equipment slot glyph resolved to null; these point at the shipped item_slot_<slot> art.
        ["item_glyph_weapon"] = "item_slot_weapon", ["item_glyph_charm"] = "item_slot_charm",
        ["item_glyph_focus"] = "item_slot_focus", ["item_glyph_helm"] = "item_slot_helm",
        ["item_glyph_chest"] = "item_slot_chest", ["item_glyph_gloves"] = "item_slot_gloves",
        ["item_glyph_boots"] = "item_slot_boots", ["item_glyph_ring"] = "item_slot_ring",
        // (item_glyph_core -> core_hatch went on 2026-09-17: no item is a core, and nothing asked.)
        // Rarity frames ship as ui_frame_rarity_<tier> (assets/art/UI/slots + ItemsLoot/frames). The
        // earlier frame_<tier> targets never existed on disk, so every one of these keys resolved to
        // null and no item ever drew a rarity frame.
        ["item_frame_common"] = "ui_frame_rarity_common", ["item_frame_uncommon"] = "ui_frame_rarity_uncommon",
        ["item_frame_rare"] = "ui_frame_rarity_rare", ["item_frame_epic"] = "ui_frame_rarity_epic",
        ["item_frame_legendary"] = "ui_frame_rarity_legendary",
        // STAT GEMS get a FACE. Six medallions (assets/art/ItemsLoot/glyphs/affix) shipped with the
        // item pack and were referenced by nothing in src/ — dormant art for a feature that was drawing
        // a flat coloured diamond instead, so every gem on every screen looked like every other gem.
        // Keyed by the gem's AffixStat, which is what a gem IS; the medallion depicts that stat.
        ["gem_damage"] = "affix_power", ["gem_health"] = "affix_health",
        ["gem_skillrate"] = "affix_timer", ["gem_haul"] = "affix_resonance",
        ["gem_crit"] = "affix_critical", ["gem_defense"] = "affix_defense",
        // TRAINING ROWS get a FACE (playtest: "MAIN TRAINING has dummy icons, COMBAT TRAINING has no
        // icons"). Keyed by the stat the row trains; each points at the ONE shipped icon that depicts
        // that stat's effect — the icon_status_* set covers seven, and the two without a status glyph
        // borrow the tree art that means the same thing: WEIGHT (heavier hits) for the crit-damage
        // stat, AVARICE (the loot road) for the loot stat. No new art.
        ["stat_might"] = "icon_status_power", ["stat_resonance"] = "icon_status_resonance",
        ["stat_tempo"] = "icon_status_timer", ["stat_vitality"] = "icon_status_healing",
        ["stat_health"] = "icon_status_health", ["stat_defense"] = "icon_status_defense",
        ["stat_critical"] = "icon_status_critical", // FOCUS and GUILE pointed at glyph art (a white hammer, a cream purse) beside seven painted
        // medallions and read as unfinished (playtest 2026-08-25). Painted stand-ins from the same
        // package: crossed blades for the critical's bite, a chest of coin for the loot.
        ["stat_focus"] = "icon_role_attacker",
        ["stat_guile"] = "icon_facility_hoardvaults",
        // GEAR CLASS ICONS are NOT aliased. ItemClasses.IconKey builds icon_class_<warden|ranger|
        // mystic|bulwark|wanderer>, and the loader keys every PNG by its basename, so the files the
        // art pass drops under assets/art/Characters/Hunter/icons/class/ resolve the moment they land.
        // An alias from a key to a file of the same name would be a no-op that the asset gate would
        // flag as a dead target until the art exists. Until it does, UiKit.ClassIcon draws a diamond
        // in the class colour, and tools/check_asset_keys.py reports the family by name.
        // (The crea_<source> aliases to the six Source-era creature stills went with those bodies on
        // 2026-09-16: the arena's cast is EnemyPresentation's, and nothing asked for a crea_ key.)
        // Arena backgrounds now ship under their own bg_arena_<Source> keys, so the old
        // <element>_<region>_clean indirection is gone. Only the legacy fallback key still
        // needs a bridge.
        ["bg_arena_verdant"] = "bg_arena_nature",   // the DrawSceneBackground fallback key
        // Combat effects (2026-08-22 art contract, design/art/arena-art-contract.md §5): one white strip per
        // FORM (tinted by the casting skill's Source at play time) plus the combat beats. VfxPlayer resolves
        // these short keys through Get(); every strip is 8 square 512 frames, drawn additively.
        ["fx_strike"] = "fx_strike_strip8_512", ["fx_projectile"] = "fx_projectile_strip8_512",
        ["fx_aura"] = "fx_aura_strip8_512", ["fx_trap"] = "fx_trap_strip8_512",
        ["fx_mark"] = "fx_mark_strip8_512", ["fx_transformation"] = "fx_transformation_strip8_512",
        ["fx_hit"] = "fx_hit_strip8_512", ["fx_weakhit"] = "fx_weakhit_strip8_512",
        ["fx_crit"] = "fx_crit_strip8_512", ["fx_death"] = "fx_death_strip8_512",
        ["fx_heal"] = "fx_heal_strip8_512", ["fx_shield"] = "fx_shield_strip8_512",
        // THREE SKILLS NAMED ART NOTHING COULD FIND. PRESS, WEEP and WILT carry the FxKeys `press`,
        // `weep` and `wilt` (SkillCatalogue), the strips have been on disk and loaded the whole time,
        // and this table had no entry for them — so the resolver returned null and VfxPlayer returned
        // silently. PRESS and WILT are two of the four Field skills, and a Field's art is what drives
        // the champion's held field, so a build running either of them had NO field effect at all.
        // The gate could not see it: tools/check_asset_keys.py reads string LITERALS at call sites and
        // these keys were computed from the skill. The contract keeps every effect key in one table
        // (VfxProfiles) so the gate has something to read, and vfx_asset_test walks the rest.
        ["fx_press"] = "fx_press_strip8_512", ["fx_weep"] = "fx_weep_strip8_512",
        ["fx_wilt"] = "fx_wilt_strip8_512",
        // `fx_levelup` was aliased here and NOTHING ever played it — §112's "every committed generated
        // asset needs a live consumer". The alias went first; the strip itself (8.4 MB of texture memory
        // at every boot) was deleted on 2026-09-17, when RH_ASSET_TRACE (tools/asset-pipeline/
        // asset_usage.sh) showed what check_asset_consumers.py cannot: `fx_` heads an interpolated key
        // family, so that gate counts every fx_* file as reached.
        // SHIELD BROKEN (UI polish §70, §95): generated through PixelLab (a cracked shell bursting into
        // shards) because fx_shield is the barrier STANDING — a held loop, not a break.
        ["fx_shield_break"] = "fx_shield_break_strip8_512",
        // THE SHIELD BAR (UI polish §66, §95). Reused, not generated: ui_bar_mana_* shipped with the bar
        // family (the same ornate gold frame as the health bar, a cold blue fill) and no screen ever
        // drew it — the game has no mana. A bar beside HEALTH that is the same frame in a cold colour
        // is exactly what a SHIELD bar should be, so BarArt(…, "shield") resolves to it.
        ["ui_bar_shield_frame"] = "ui_bar_mana_frame", ["ui_bar_shield_fill"] = "ui_bar_mana_fill",
    };

    private Texture2D? Resolve(string key)
    {
        Trace(key);
        return Loaded(key)
               ?? (Aliases.TryGetValue(key, out var aliased) ? Loaded(Trace(aliased)) : null);
    }

    // ── RH_ASSET_TRACE=<file> (dev only): every key the game ASKS for, appended once per process. ──
    // "Is this texture used?" has no static answer here — keys are built from catalogues, regions,
    // champions and clip names at runtime, and the asset gate counts a whole interpolated family as
    // reached. So the trace records the question itself: Get, Has, GetFirst, WhiteMask, an alias's
    // target, and every key a Warm prefix matched. Run the capture battery with it on and the union is
    // the set of textures some screen really asked for (tools/asset-pipeline/asset_usage.sh). Inert
    // without the variable: one static bool read per request.
    private static readonly string? TracePath = Environment.GetEnvironmentVariable("RH_ASSET_TRACE") is { Length: > 0 } t ? t : null;
    private static readonly HashSet<string> Traced = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="key">The key asked for.</param>
    /// <param name="mark">How it was asked: empty for a request that returns the texture (Get, GetFirst,
    /// WhiteMask, an alias target), <c>?</c> for an existence check (<see cref="Has"/>), <c>~</c> for a key
    /// a <see cref="Warm"/> prefix loaded — so the report can tell a texture something DREW from one that
    /// was only asked about, or only warmed as part of a family.</param>
    private static string Trace(string key, string mark = "")
    {
        if (TracePath is null || string.IsNullOrEmpty(key)) return key;
        var line = mark + key;
        lock (Traced)
        {
            if (!Traced.Add(line)) return key;
            try { File.AppendAllText(TracePath, line + "\n"); }
            catch (IOException)
            {
                // A trace is best-effort; the game never fails for it. (A line comment on purpose: the
                // asset gates strip block comments, and one here paired with the `**` in the class notes.)
            }
        }
        return key;
    }

    /// <summary>A loaded texture, loading a deferred one now if this is its first ask.</summary>
    private Texture2D? Loaded(string key)
        => _textures.TryGetValue(key, out var tex) ? tex
           : _deferred.ContainsKey(key) ? LoadDeferred(key)
           : null;

    /// <summary>The texture for a key (or its migration alias), or null if absent (caller falls back to shapes).</summary>
    public Texture2D? Get(string key) => Resolve(key);

    /// <summary>
    /// Load now every deferred texture whose key starts with <paramref name="prefix"/>. A screen that knows
    /// which family it is about to draw calls this from Update, so the decode is not paid inside a Draw.
    /// Returns how many loaded; keys already loaded cost nothing.
    /// </summary>
    public int Warm(string prefix)
    {
        if (TracePath is not null && !string.IsNullOrEmpty(prefix))
            foreach (var key in _deferred.Keys.Concat(_textures.Keys))
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) Trace(key, "~");
        if (_deferred.Count == 0 || string.IsNullOrEmpty(prefix)) return 0;
        _warmBuffer.Clear();
        foreach (var key in _deferred.Keys)
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) _warmBuffer.Add(key);
        var loaded = 0;
        foreach (var key in _warmBuffer)
            if (LoadDeferred(key) is not null) loaded++;
        return loaded;
    }

    private readonly List<string> _warmBuffer = new();

    /// <summary>Index one deferred PNG by its header, keeping the larger of two files under one key.</summary>
    private void Index(string path, string norm)
    {
        var key = Path.GetFileNameWithoutExtension(path);
        long area;
        try { area = HeaderArea(path); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return; }
        if (_deferredArea.TryGetValue(key, out var existing)
            && !(area > existing || (area == existing && string.CompareOrdinal(norm, _sources.GetValueOrDefault(key, "")) < 0)))
            return;
        _deferred[key] = path;
        _deferredArea[key] = area;
        _sources[key] = norm;
    }

    /// <summary>A PNG's width x height from its IHDR chunk, without decoding it.</summary>
    private static long HeaderArea(string path)
    {
        Span<byte> head = stackalloc byte[24];
        using var stream = File.OpenRead(path);
        if (stream.Read(head) != 24 || head[12] != (byte)'I' || head[13] != (byte)'H')
            throw new InvalidDataException($"{path} is not a PNG");
        var w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        var h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        return (long)w * h;
    }

    /// <summary>Decode a deferred texture into the table. A corrupt file leaves the index, and its draws fall back to shapes.</summary>
    private Texture2D? LoadDeferred(string key)
    {
        if (!_deferred.Remove(key, out var path)) return _textures.GetValueOrDefault(key);
        _deferredArea.Remove(key);
        try
        {
            using var stream = File.OpenRead(path);
            var texture = Texture2D.FromStream(_device, stream);
            Premultiply(texture);
            _textures[key] = texture;
            _byTexture?.TryAdd(texture, key);
            return texture;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _sources.Remove(key);
            return null;
        }
    }

    /// <summary>
    /// The key a texture was loaded under, or null if it was not loaded from this library.
    /// </summary>
    /// <remarks>
    /// For the raster ledger (<see cref="UiRasterLedger"/>) only: several UiKit primitives are handed a
    /// <see cref="Texture2D"/> rather than a key, and a ledger row that cannot name its asset cannot be
    /// acted on. The reverse lookup is built once, lazily, and only when the dial is on — it is a linear
    /// walk of the table, which is fine once and would not be fine per draw.
    /// </remarks>
    public string? KeyOf(Texture2D? tex)
    {
        if (tex is null) return null;
        _byTexture ??= _textures.GroupBy(kv => kv.Value)
                                .ToDictionary(g => g.Key, g => g.First().Key);
        return _byTexture.TryGetValue(tex, out var k) ? k : null;
    }

    private Dictionary<Texture2D, string>? _byTexture;

    /// <summary>
    /// Is there art under this key (or its alias)? Answered from the table and the deferred index, so asking
    /// never loads anything.
    /// </summary>
    public bool Has(string key)
        => Present(Trace(key, "?")) || (Aliases.TryGetValue(key, out var aliased) && Present(Trace(aliased, "?")));

    private bool Present(string key) => _textures.ContainsKey(key) || _deferred.ContainsKey(key);

    /// <summary>The key a texture's white silhouette is registered under (see <see cref="WhiteMask"/>).</summary>
    public static string MaskKey(string key) => key + "|mask";

    /// <summary>
    /// A WHITE SILHOUETTE of a texture — every pixel's colour replaced by its alpha (premultiplied, so
    /// white at the alpha) — built once and registered under <see cref="MaskKey"/>, so it can be drawn
    /// through the same strip/sprite helpers as the original. The hit flash draws a creature's mask over
    /// the creature: a tint can only darken a sprite and an additive pass only adds the sprite's own
    /// dark colours, so "flash white" needs a white shape (playtest 2026-08-28: "the white flash is
    /// definitely not showing").
    /// </summary>
    public Texture2D? WhiteMask(string key)
    {
        var maskKey = MaskKey(key);
        if (_textures.TryGetValue(maskKey, out var have)) return have;
        if (Resolve(key) is not { } src) return null;
        var data = new Color[src.Width * src.Height];
        src.GetData(data);
        for (var i = 0; i < data.Length; i++)
        {
            var a = data[i].A;
            data[i] = new Color(a, a, a, a);
        }
        var mask = new Texture2D(_device, src.Width, src.Height);
        mask.SetData(data);
        _textures[maskKey] = mask;
        return mask;
    }

    /// <summary>First present texture among candidates, or null. Lets callers try a specific-then-generic key.</summary>
    public Texture2D? GetFirst(params string[] keys) => keys.Select(Get).FirstOrDefault(t => t is not null);

    /// <summary>
    /// Premultiply a texture's colour by its alpha in place.
    /// </summary>
    /// <remarks>
    /// <see cref="Texture2D.FromStream"/> returns straight (non-premultiplied) alpha, but the scene is
    /// drawn with <see cref="BlendState.AlphaBlend"/>, which expects premultiplied. Skipping this leaves
    /// a dark halo around every transparent edge — the classic MonoGame PNG artifact. Fully-opaque and
    /// fully-transparent pixels are unaffected, so it is a no-op for hard-edged pixel art and a fix for
    /// anything with soft edges.
    /// </remarks>
    private static void Premultiply(Texture2D texture)
    {
        var data = new Color[texture.Width * texture.Height];
        texture.GetData(data);

        for (var i = 0; i < data.Length; i++)
        {
            var c = data[i];
            data[i] = Color.FromNonPremultiplied(c.R, c.G, c.B, c.A);
        }

        texture.SetData(data);
    }
}
