using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
/// </remarks>
public sealed class AssetLibrary
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly GraphicsDevice _device;

    /// <summary>Winning source path per key, so collision tie-breaks stay deterministic.</summary>
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);

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
        // Per-source creatures → package_03 enemy idle poses. One representative enemy per element (the
        // Warren's per-role keys fall back here; no Nature enemy shipped, so a wisp stands in).
        ["crea_body"] = "bonecrawler_idle_01", ["crea_machine"] = "stone_sentinel_idle_01",
        ["crea_mind"] = "soul_leech_idle_01", ["crea_nature"] = "wisp_idle_01",
        ["crea_shadow"] = "shadeling_idle_01", ["crea_spirit"] = "rift_guardian_idle_01",
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
        // `fx_levelup` was aliased here, the file is on disk, and NOTHING has ever played it — §112's
        // "every committed generated asset needs a live consumer". The alias is gone; the file is
        // listed in tools/asset_orphans_baseline.txt, which is where a decision about deleting art
        // belongs (the desk, not a gate).
        // SHIELD BROKEN (UI polish §70, §95): generated through PixelLab (a cracked dome bursting into
        // shards) because fx_shield is a dome that flashes in and settles — a gain, not a break.
        ["fx_shield_break"] = "fx_shield_break_strip8_512",
        // THE SHIELD BAR (UI polish §66, §95). Reused, not generated: ui_bar_mana_* shipped with the bar
        // family (the same ornate gold frame as the health bar, a cold blue fill) and no screen ever
        // drew it — the game has no mana. A bar beside HEALTH that is the same frame in a cold colour
        // is exactly what a SHIELD bar should be, so BarArt(…, "shield") resolves to it.
        ["ui_bar_shield_frame"] = "ui_bar_mana_frame", ["ui_bar_shield_fill"] = "ui_bar_mana_fill",
    };

    private Texture2D? Resolve(string key)
        => _textures.GetValueOrDefault(key)
           ?? (Aliases.TryGetValue(key, out var aliased) ? _textures.GetValueOrDefault(aliased) : null);

    /// <summary>The texture for a key (or its migration alias), or null if absent (caller falls back to shapes).</summary>
    public Texture2D? Get(string key) => Resolve(key);

    public bool Has(string key) => Resolve(key) is not null;

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
