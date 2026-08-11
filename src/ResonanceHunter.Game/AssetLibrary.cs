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

    /// <summary>Winning source path per key, so collision tie-breaks stay deterministic.</summary>
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);

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

            // Rev 4 §5: after the folder skips, everything remaining is a runtime candidate. Assert it is
            // NOT a preview / source-reference / concept / contact-sheet asset before we ever open it — a
            // forbidden asset must never reach the screen, so a match is a hard error, not a silent skip.
            ValidateRuntimeAssetPath(path);

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
        ["item_glyph_core"] = "core_hatch",
        // Rarity frames ship as ui_frame_rarity_<tier> (assets/art/UI/slots + ItemsLoot/frames). The
        // earlier frame_<tier> targets never existed on disk, so every one of these keys resolved to
        // null and no item ever drew a rarity frame.
        ["item_frame_common"] = "ui_frame_rarity_common", ["item_frame_uncommon"] = "ui_frame_rarity_uncommon",
        ["item_frame_rare"] = "ui_frame_rarity_rare", ["item_frame_epic"] = "ui_frame_rarity_epic",
        ["item_frame_legendary"] = "ui_frame_rarity_legendary",
        // UiKit.KeyCap asks for ui_keycap; the pack ships the blank square variant under a longer name.
        ["ui_keycap"] = "ui_keycap_square_blank",
        // Per-source creatures → package_03 enemy idle poses. One representative enemy per element (the
        // Warren's per-role keys fall back here; no Nature enemy shipped, so a wisp stands in).
        ["crea_body"] = "bonecrawler_idle_01", ["crea_machine"] = "stone_sentinel_idle_01",
        ["crea_mind"] = "soul_leech_idle_01", ["crea_nature"] = "wisp_idle_01",
        ["crea_shadow"] = "shadeling_idle_01", ["crea_spirit"] = "rift_guardian_idle_01",
        // Region bosses → package_04 boss idle poses (normalized 1024 canvases), matched to region theme.
        ["boss_verdant_hollow"] = "thorn_regent_idle_1024", ["boss_cinderworks"] = "forge_colossus_idle_1024",
        ["boss_umbral_reach"] = "void_reaper_idle_1024", ["boss_still_archive"] = "crystal_lich_idle_1024",
        ["boss_pale_choir"] = "lumen_angel_idle_1024", ["boss_marrow_wastes"] = "spirit_matron_idle_1024",
        // Arena backgrounds now ship under their own bg_arena_<Source> keys, so the old
        // <element>_<region>_clean indirection is gone. Only the legacy fallback key still
        // needs a bridge.
        ["bg_arena_verdant"] = "bg_arena_nature",   // the DrawSceneBackground fallback key
        // Combat FX (8 square 512 frames each). VfxPlayer resolves these keys through Get().
        // crit / death / interrupt now have DEDICATED strips. Previously crit shared slash_void with
        // ability_ruinstrike, death shared smoke_puff with weakhit, and interrupt drew a holy HEAL burst —
        // so a interrupted cast and a heal looked identical. Each semantic event now reads as itself.
        ["vfx_hit"] = "impact_gold_strip8_512", ["vfx_weakhit"] = "smoke_puff_strip8_512",
        ["vfx_crit"] = "impact_crit_strip8_512", ["vfx_ability_ruinstrike"] = "slash_shadow_strip8_512",
        ["vfx_death"] = "death_dissolve_strip8_512", ["vfx_interrupt"] = "interrupt_break_strip8_512",
        ["vfx_levelup"] = "levelup_gold_purple_strip8_512",
        // Per-Source slash strips, for damage-type-aware hit FX.
        ["vfx_slash_body"] = "slash_body_strip8_512", ["vfx_slash_mind"] = "slash_mind_strip8_512",
        ["vfx_slash_machine"] = "slash_machine_strip8_512", ["vfx_slash_nature"] = "slash_nature_strip8_512",
        ["vfx_slash_shadow"] = "slash_shadow_strip8_512", ["vfx_slash_spirit"] = "slash_spirit_strip8_512",
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
