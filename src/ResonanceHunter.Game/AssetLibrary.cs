using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ResonanceHunter.Client;

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
/// </remarks>
public sealed class AssetLibrary
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);

    public AssetLibrary(GraphicsDevice device)
    {
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

            try
            {
                using var stream = File.OpenRead(path);
                var texture = Texture2D.FromStream(device, stream);
                Premultiply(texture);
                _textures[Path.GetFileNameWithoutExtension(path)] = texture;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // A single corrupt PNG must not take the whole game down — it just falls back to shapes.
            }
        }
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
        ["item_glyph_weapon"] = "item_weapon", ["item_glyph_charm"] = "item_charm",
        ["item_glyph_focus"] = "item_focus", ["item_glyph_helm"] = "item_helm",
        ["item_glyph_chest"] = "item_chest", ["item_glyph_gloves"] = "item_gloves",
        ["item_glyph_boots"] = "item_boots", ["item_glyph_ring"] = "item_ring",
        ["item_glyph_core"] = "core_hatch",
        ["item_frame_common"] = "frame_common", ["item_frame_uncommon"] = "frame_uncommon",
        ["item_frame_rare"] = "frame_rare", ["item_frame_epic"] = "frame_epic",
        ["item_frame_legendary"] = "frame_legendary",
        // Per-source creatures → package_03 enemy idle poses. One representative enemy per element (the
        // Warren's per-role keys fall back here; no Nature enemy shipped, so a wisp stands in).
        ["crea_body"] = "bonecrawler_idle_01", ["crea_machine"] = "stone_sentinel_idle_01",
        ["crea_mind"] = "soul_leech_idle_01", ["crea_nature"] = "wisp_idle_01",
        ["crea_shadow"] = "shadeling_idle_01", ["crea_spirit"] = "rift_guardian_idle_01",
        // Region bosses → package_04 boss idle poses (normalized 1024 canvases), matched to region theme.
        ["boss_verdant_hollow"] = "thorn_regent_idle_1024", ["boss_cinderworks"] = "forge_colossus_idle_1024",
        ["boss_umbral_reach"] = "void_reaper_idle_1024", ["boss_still_archive"] = "crystal_lich_idle_1024",
        ["boss_pale_choir"] = "lumen_angel_idle_1024", ["boss_marrow_wastes"] = "spirit_matron_idle_1024",
        // package_07 region arenas — the fight draws bg_arena_<Source theme>; resolve to the element's
        // full-screen background (the 'unconquered' variant is the in-combat look).
        // Use the CLEAN variant — the 'unconquered' variant bakes purple corruption-marker circles into the
        // art (they read as stray targeting reticles over the arena).
        ["bg_arena_body"] = "body_blood_moors_clean", ["bg_arena_mind"] = "mind_crystal_caverns_clean",
        ["bg_arena_nature"] = "nature_verdant_hollow_clean", ["bg_arena_machine"] = "machine_forge_wastes_clean",
        ["bg_arena_shadow"] = "shadow_void_wastes_clean", ["bg_arena_spirit"] = "spirit_twilight_sanctum_clean",
        ["bg_arena_verdant"] = "nature_verdant_hollow_clean",   // the DrawSceneBackground fallback key
        // Combat FX: the sim fires semantic events; the pack ships GENERIC effect strips meant to serve them
        // (a crit reads as a slash, an interrupt as a shield). This is the strips' intended use, not a stand-in
        // for a missing bespoke effect. fx_bolt / fx_aura / fx_heal are authored but not yet triggered anywhere.
        // package_06 VFX strips (8 square 512 frames each). VfxPlayer resolves these keys through Get().
        ["vfx_hit"] = "impact_gold_strip8_512", ["vfx_weakhit"] = "smoke_puff_strip8_512",
        ["vfx_crit"] = "slash_void_strip8_512", ["vfx_ability_ruinstrike"] = "slash_void_strip8_512",
        ["vfx_death"] = "smoke_puff_strip8_512", ["vfx_interrupt"] = "heal_holy_burst_strip8_512",
        ["vfx_levelup"] = "levelup_gold_purple_strip8_512",
    };

    private Texture2D? Resolve(string key)
        => _textures.GetValueOrDefault(key)
           ?? (Aliases.TryGetValue(key, out var aliased) ? _textures.GetValueOrDefault(aliased) : null);

    /// <summary>The texture for a key (or its migration alias), or null if absent (caller falls back to shapes).</summary>
    public Texture2D? Get(string key) => Resolve(key);

    public bool Has(string key) => Resolve(key) is not null;

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
