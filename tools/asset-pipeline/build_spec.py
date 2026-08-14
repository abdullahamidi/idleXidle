#!/usr/bin/env python3
"""Emit the complete asset spec from the game's runtime contract.

This does NOT mirror the legacy library. It enumerates every key the code can
actually ask for — including the ones built by string interpolation — and
designs one asset per key. Anything the legacy tree held that nothing asks for
(loose animation frames, `_1024` duplicates, mask/medallion triplicates,
placement guides) is simply not in the output.

Sources of truth, all verified against the code:
  Source     6  Creature.cs        Body Mind Nature Machine Shadow Spirit
  Role       5  Creature.cs        Attacker Defender Support Crafter Producer
  GearSlot   8  Gear.cs            Weapon Charm Focus Helm Chest Gloves Boots Ring
  GearTrait 10  GearTraits.cs      Keen Heavy Swift Savage Warding Vital Greedy Attuned Focused Wild
  enemies    6  SoloExpeditionScreen.EnemyForSource
  bosses     6  SoloExpeditionScreen.BossForRegion
  bar types  5  UiKit.BarArt        health mana progress boss xp

Run:  python3 tools/asset-pipeline/build_spec.py
Writes manifest.json (stills) and animations.json (strips).
"""

from __future__ import annotations

import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))

SOURCES = ["body", "mind", "nature", "machine", "shadow", "spirit"]
ROLES = ["attacker", "defender", "support", "crafter", "producer"]
SLOTS = ["weapon", "charm", "focus", "helm", "chest", "gloves", "boots", "ring"]
TRAITS = ["keen", "heavy", "swift", "savage", "warding", "vital", "greedy", "attuned", "focused", "wild"]
BAR_TYPES = ["health", "mana", "progress", "boss", "xp"]

# Source identity: colour + the edge quality the art bible mandates (§4.2).
SOURCE_ART = {
    "body":    ("blood crimson red", "convex smooth symmetric arcs, bulging musculature"),
    "mind":    ("crystal cyan",      "hard faceted symmetric cuts, mirrored halves"),
    "nature":  ("moss green",        "branching asymmetric outgrowths, thorns and forks"),
    "machine": ("rust iron orange",  "orthogonal rigid bolted plates, modular segments"),
    "shadow":  ("deep indigo violet", "torn discontinuous silhouette, jagged bite-notches"),
    "spirit":  ("pale lavender white", "soft dissolving contours tapering into wisps"),
}

# One enemy per Source (SoloExpeditionScreen.EnemyForSource). The attack clip is
# "slam" for stone_sentinel and "attack" for everyone else.
ENEMIES = {
    "bonecrawler":    ("body",    "a skittering crimson bone crawler with many clawed legs", "attack"),
    "soul_leech":     ("mind",    "a hovering cyan robed leech-wraith with a hollow hood",    "attack"),
    "wisp":           ("nature",  "a floating green spirit wisp with trailing tendrils",      "attack"),
    "stone_sentinel": ("machine", "a hulking rust-iron stone sentinel golem",                 "slam"),
    "shadeling":      ("shadow",  "a lean indigo shadow imp with tattered edges",             "attack"),
    "rift_guardian":  ("spirit",  "a tall pale armoured rift guardian wreathed in wisps",     "attack"),
}

# One boss per region (SoloExpeditionScreen.BossForRegion).
BOSSES = {
    "thorn_regent":   ("nature",  "a towering antlered thorn regent crowned in brambles"),
    "forge_colossus": ("machine", "a massive rust-iron forge colossus with molten seams"),
    "void_reaper":    ("shadow",  "a shrouded void reaper with a great curved scythe"),
    "crystal_lich":   ("mind",    "a floating crystal lich wreathed in faceted cyan shards"),
    "lumen_angel":    ("spirit",  "a radiant many-winged lumen angel of pale light"),
    "spirit_matron":  ("body",    "a gaunt crimson spirit matron trailing bone and sinew"),
}

TRAIT_LOOK = {
    # MATERIAL, not effect. These double as the character overlay, where a piece is
    # only ~50 px tall — anything described as glowing, floating or surrounding the
    # object becomes pure haze at that size (an "attuned" boot read as a purple
    # blob). Describing the metal instead survives the downscale and still tells
    # the traits apart at a glance.
    "keen":    "polished razor-edged bright steel",
    "heavy":   "thick blunt blackened iron",
    "swift":   "slender lightweight pale steel",
    "savage":  "jagged barbed battle-scarred iron",
    "warding": "rune-etched blue-steel",
    "vital":   "warm red-bronze with crimson inlay",
    "greedy":  "gilded gold with fine ornament",
    "attuned": "deep violet arcane metal",
    "focused": "dark steel with a single bright inset gem",
    "wild":    "mossy bronze with green vine engraving",
}

SLOT_OBJECT = {
    "weapon": "a sword blade", "charm": "a hanging amulet", "focus": "a round floating crystal orb",
    "helm": "a knight helm", "chest": "a breastplate", "gloves": "a gauntlet",
    "boots": "an armoured boot", "ring": "a jewelled ring",
}

RARITY_RING = {
    "common": "a plain dark border, no ring",
    "uncommon": "a single thin gold ring",
    "rare": "a double concentric gold ring",
    "epic": "a double gold ring with four corner flourishes",
    "legendary": "a full ward-seal of concentric rings with radiating flourishes",
}

STILL_STYLES = {
    "medallion": "Ornate gold heraldic medallion badge icon, dark near-black center, {subject}, "
                 "baroque filigree rim, warm gold #F0A830 metal on void black #1B1620, "
                 "dark fantasy RPG inventory icon, flat bold shapes, centered, symmetrical, no text",
    "creature":  "A single small fantasy creature, front facing, full body, {subject}. "
                 "Dark fantasy RPG creature icon, bold readable silhouette, flat bold shapes, "
                 "centered, plain background, no text, no border, no frame",
    "item":      "{subject}. Dark fantasy RPG item icon, single object, three-quarter view, "
                 "bold readable silhouette, flat bold shapes, centered, plain background, "
                 "no text, no border, no frame",
    "chrome":    "{subject}. Dark fantasy RPG user-interface element, ornate gold trim on "
                 "near-black #1B1620, flat, clean edges, plain background, no text",
    # A bar FILL is not chrome. The chrome style literally asks for "ornate gold trim", so every
    # ui_bar_*_fill came back as another empty ornate frame — which is why the boss bar rendered with
    # no visible fill at all, and why the xp bar had filigree floating in the middle of its liquid.
    # A fill is clipped horizontally and drawn inside the frame's window, so it must be full-bleed
    # colour with no edge features of any kind.
    "barfill":   "A plain solid slab of {subject} filling the ENTIRE image edge to edge, a flat "
                 "colour field with a subtle vertical gradient and faint grain. NO frame, NO border, "
                 "NO trim, NO gold, NO ornament, NO filigree, NO corners, NO panel, NO window, "
                 "NO text, NO icons, NO objects — nothing but the colour itself, full bleed",
    "background": "{subject}. Dark fantasy pixel-art game background, VERY DARK and desaturated, "
                  "low contrast, dim ambient atmosphere only, deep near-black, muted colours, "
                  "wide establishing shot, no characters, no text, no UI elements, no borders",
    # Arenas are a STAGE, not a vista. The first pass generated free-form
    # establishing shots whose horizon landed anywhere from 63% to 97% of the
    # height, so no single GroundY could plant characters on the floor and they
    # read as fighting in mid-air. These prompts pin the ground plane instead.
    "arena": "{subject}. Side-on 2D fighting-stage background. COMPOSITION IS CRITICAL: "
             "a wide FLAT EMPTY GROUND PLANE fills the entire bottom third of the image, "
             "unobstructed and clear from left edge to right edge, with a clean horizon line "
             "about two thirds of the way down. All scenery, walls and canopy stay in the upper "
             "two thirds. The lower third is bare walkable floor with NOTHING standing on it. "
             "Dark fantasy pixel art, VERY DARK and desaturated, low contrast, dim ambient only, "
             "deep near-black, muted, no characters, no creatures, no text, no UI, no borders",
    # Purpose-drawn RIG PARTS. Cutting a joint cover out of the body sprite gives you
    # pixels lit for the resting pose, so the moment the limb rotates the cover reads
    # as a patch. A drawn piece is lit for itself and reads as armour at any angle —
    # generate the right asset instead of masking the wrong one.
    "rigpart": "ONLY {subject}, and nothing else. A single isolated piece, front view, "
               "cut out on an empty background. NO character, NO body, NO head, NO arms, "
               "NO background, NO frame, no text, NO glow, NO aura — a solid opaque object. "
               "Dark fantasy RPG pixel art, bone-parchment cloth and cold-slate leather, "
               "bold readable silhouette, flat bold shapes, centered",

    # Worn rig gear. Fundamentally different from an item ICON: an icon is drawn on
    # the diagonal to fill a square inventory cell, which reads as a sword worn like
    # a sash once it is pinned to a hand bone. Rig gear must be FRONT-ON in its worn
    # orientation, isolated, and shaped to the body part it covers.
    "geararmor": "ONLY {subject}, and nothing else. Front view, straight on, exactly as it is worn. "
                 "A single isolated piece of equipment lying flat, cut out on an empty background. "
                 "NO character, NO body, NO mannequin, NO hands, NO head, NO other equipment, "
                 "NO background, NO frame, NO border, no text. NO glow, NO aura, NO particles, "
                 "NO floating effects — a solid opaque object. Dark fantasy RPG pixel art, "
                 "bold readable silhouette, flat bold shapes, centered",
    # A weapon is authored VERTICAL, hilt at the bottom, blade pointing straight up.
    # Then the hand bone's own angle is the only rotation needed.
    "gearweapon": "ONLY {subject}, and nothing else. Held UPRIGHT and VERTICAL, hilt at the bottom, "
                  "blade pointing straight up, perfectly vertical. A single isolated weapon cut out "
                  "on an empty background. NO character, NO hands, NO background, NO frame, no text. "
                  "NO glow, NO aura, NO particles — a solid opaque weapon. Dark fantasy RPG pixel art, "
                  "bold readable silhouette, flat bold shapes, centered",

    # A playable character is not a "creature". Routed through the creature style the roster came back
    # as a bestiary: the starter arrived as a goblin, two characters brought a painted backdrop with
    # them, and one returned as TWO figures standing side by side. The creature style says "a single
    # small fantasy creature", and every one of those is that phrase being answered honestly.
    #
    # What a hero sprite needs instead: one person, standing straight and facing front so the strip
    # animator has a clean neutral frame to work from, and nothing whatsoever behind them — no ground,
    # no shadow, no vignette, because those survive knockout and then ride along under the figure on
    # the battle stage.
    # "full body" is not enough on its own — the starter came back cropped at the belt, which the arena
    # then drew as a torso standing on the floor. Naming the FEET is what gets the legs: a model that
    # has been told to include both boots cannot frame the shot above them.
    "hero":      "{subject}. ONE single character alone, front facing, standing upright and still, "
                 "facing the viewer. The ENTIRE body is visible from the top of the head all the way "
                 "down to both feet, with both legs and both boots fully in frame and a little empty "
                 "space below them. Dark fantasy RPG hero sprite, bold readable silhouette, "
                 "flat bold shapes, strong rim light, centered on a COMPLETELY EMPTY background with "
                 "nothing behind the figure at all: no scene, no wall, no ground, no floor, no cast "
                 "shadow, no circle, no vignette. One figure only, no companion, no second character, "
                 "no text, no border, no frame",

    # A GLYPH is drawn to be TINTED. The screens that use these pick the colour at draw time — a
    # skill-tree node is coloured by its branch, a trait row by its road — so the art must arrive as
    # one flat light value and nothing else. The "item" and "medallion" styles both bake colour in,
    # and multiplying a tint through an already-coloured object gives mud; the medallion style also
    # spends the canvas on a gold RIM, which is the one thing a glyph must not have, because the
    # frame around it is a separate asset that has to fit.
    "glyph":     "{subject}, drawn as ONE flat heraldic emblem in solid pale bone-white, a single "
                 "uniform light tone with no colour in it, filling the whole frame edge to edge, "
                 "bold thick chunky silhouette, strongly symmetrical, centered on an empty "
                 "background. It is a stencil, not a picture: NO ring, NO circle, NO rim, NO frame, "
                 "NO border, NO badge, NO medallion, NO plaque, NO coin, NO background, NO scene, "
                 "NO text, NO gold trim, NO filigree",

    # The rig style must say "ONLY" and name the exclusions explicitly. Without
    # that the model drew a whole hooded figure for every part, which is useless
    # for a cutout rig that composes parts itself.
    "rig":       "ONLY {subject} and nothing else. A single detached body part lying alone, "
                 "cut out for a 2D puppet rig. NO full character, NO whole body, NO head unless "
                 "asked, NO other limbs. Dark fantasy hunter garb in bone-parchment cloth and "
                 "cold-slate leather. Flat bold shapes, one isolated object centered on a plain "
                 "empty background, no text, no border, no frame",
}

ANIM_STYLE_VFX = ("{subject}, pixel-art VFX sprite, bright saturated energy, bold readable shape, "
                  "centered, plain background, no text, no character, no border")


# The roster's ids, at module scope: stills() designs them and animations() clips them, and a list
# that drifted between the two would quietly animate nine characters and leave the tenth a statue.
ROSTER_IDS = ["seeker", "anvil", "chorus", "metronome", "unbroken",
              "tower", "quiver", "thornwall", "oathbound", "magpie"]


def stills() -> list[dict]:
    a: list[dict] = []

    def add(key, dest, style, subject, **kw):
        a.append({"key": key, "dest": dest, "style": style, "subject": subject, **kw})

    # --- Source medallions: source_<element> (MapScreen/CharacterScreen/BuildScreen/ForgeScreen)
    for s in SOURCES:
        col, edge = SOURCE_ART[s]
        add(f"source_{s}", "assets/art/UI/icons/sources", "medallion",
            f"a {col} elemental gem with {edge}")

    # --- Role badges: icon_role_<role> (AutomationScreen)
    role_motif = {"attacker": "two large crossed golden swords filling the whole center", "defender": "a stout golden tower shield",
                  "support": "a golden chalice radiating light", "crafter": "a golden hammer resting on an anvil",
                  "producer": "a tall stack of golden coins"}
    for r in ROLES:
        add(f"icon_role_{r}", "assets/art/UI/icons/roles", "medallion", role_motif[r])

    # --- Region crests: icon_region_<id> (MapScreen)
    region_motif = {
        "verdant": ("nature", "a thorned branching antler-leaf"),
        "cinderworks": ("machine", "one large rust-orange cogwheel gear"),
        "umbral": ("shadow", "one jagged torn crescent moon"),
        "marrow_wastes": ("body", "one pale bone skull"),
        "still_archive": ("mind", "one large faceted cyan crystal shard"),
        "pale_choir": ("spirit", "one glowing pale lavender halo ring"),
    }
    for rid, (src, motif) in region_motif.items():
        col, _ = SOURCE_ART[src]
        add(f"icon_region_{rid}", "assets/art/UI/icons/regions", "medallion",
            f"{motif} in {col} filling the center")

    # --- Warren roster: crea_<source>_<role>_<name> (AutomationScreen)
    warren = {
        "nature":  [("atk","whelp"),("sup","mossling"),("def","bramble"),("prd","sporeling"),("crf","sapwright")],
        "machine": [("atk","warden"),("sup","drone"),("def","bulwark"),("crf","cogwright"),("prd","boiler")],
        "shadow":  [("atk","stalker"),("sup","wisp"),("def","bulwark"),("crf","weaver"),("prd","spore")],
        "body":    [("atk","sinew"),("def","bonewall"),("sup","pulsekin"),("crf","marrow"),("prd","brood")],
        "mind":    [("atk","lance"),("def","aegis"),("sup","chorus"),("crf","schema"),("prd","bloom")],
        "spirit":  [("atk","echofang"),("def","vigil"),("sup","solace"),("crf","rite"),("prd","emberfont")],
    }
    mass = {"atk": "lean forward-leaning predatory body, bladed claws, top-heavy mass",
            "def": "broad squat armoured body, heavy wide base, shielded carapace",
            "sup": "slender upright body, glowing emissive core, raised thin limbs",
            "crf": "hunched compact body with many fine working limbs and tool-claws",
            "prd": "rounded bulbous nurturing body, swollen abdomen, short legs"}
    for s, entries in warren.items():
        col, edge = SOURCE_ART[s]
        for rk, name in entries:
            add(f"crea_{s}_{rk}_{name}", "assets/art/UI/icons/creatures", "creature",
                f"a creature called the {name}, {col} colouring, {edge}, {mass[rk]}")

    # --- Trait item icons: item_<slot>_<trait> (ForgeScreen.TraitGlyph) — 8 x 10
    for slot in SLOTS:
        for tr in TRAITS:
            add(f"item_{slot}_{tr}", f"assets/art/ItemsLoot/traits/{slot}", "item",
                f"{SLOT_OBJECT[slot]} with {TRAIT_LOOK[tr]}")

    # --- Generic slot glyphs (alias targets for item_glyph_<slot>)
    for slot in SLOTS:
        add(f"item_slot_{slot}", "assets/art/UI/icons/slots", "medallion",
            f"{SLOT_OBJECT[slot]} in plain silver-grey metal, neutral mid-value for engine tinting")

    # --- Rarity frames: ui_frame_rarity_<tier> (alias target for item_frame_<tier>)
    for tier, ring in RARITY_RING.items():
        add(f"ui_frame_rarity_{tier}", "assets/art/UI/slots", "chrome",
            f"an empty square item slot frame with {ring}, hollow transparent center")

    # --- Bars: ui_bar_<type>_frame / _fill (UiKit.BarArt)
    bar_col = {"health": "glowing crimson red", "mana": "glowing deep blue", "progress": "glowing warm gold",
               "boss": "glowing molten orange-red", "xp": "glowing violet"}
    NEG_FILL = ("frame, border, trim, ornament, filigree, gold leaf, corners, panel, window, "
                "bar frame, text, icon, object, character")
    for t in BAR_TYPES:
        add(f"ui_bar_{t}_frame", "assets/art/UI/bars", "chrome",
            "an ornate horizontal bar frame with a hollow dark window and gold trim",
            width=256, height=64)
        add(f"ui_bar_{t}_fill", "assets/art/UI/bars", "barfill",
            bar_col[t], width=256, height=64, knockout=False, no_background=False,
            negative_description=NEG_FILL)
    # Legacy generic pair still referenced by UiKit.Bar
    add("ui_bar_frame", "assets/art/UI/bars", "chrome",
        "an ornate horizontal bar frame with a hollow dark window and gold trim", width=256, height=64)
    add("ui_bar_fill", "assets/art/UI/bars", "barfill",
        "glowing pale bone white", width=256, height=64, knockout=False, no_background=False,
        negative_description=NEG_FILL)

    # --- Currencies / materials / affixes
    for key, subj in [
        ("currency_gleam", "a glowing gold coin marked with a rune"),
        ("currency_memory_dust", "a pinch of luminous violet dust motes"),
        ("currency_resonance_shard", "a humming pale blue crystal shard"),
        ("mat_scrap", "a bundle of rusted scrap metal offcuts"),
        ("mat_essence", "a small vial of glowing green essence"),
        ("mat_core", "a dense glowing machine core"),
        ("mat_crystal", "a cluster of clear faceted crystals"),
    ]:
        add(key, "assets/art/ItemsLoot/materials", "item", subj)
    for key, subj in [
        ("affix_critical", "a red starburst crit sigil"), ("affix_defense", "a blue shield sigil"),
        ("affix_healing", "a green cross-leaf sigil"), ("affix_health", "a crimson heart sigil"),
        ("affix_power", "a gold fist sigil"), ("affix_resonance", "a violet tuning-fork sigil"),
        ("affix_timer", "a pale hourglass sigil"),
    ]:
        add(key, "assets/art/ItemsLoot/affixes", "medallion", subj)

    # --- Status / equipment / class icons
    for key, subj in [
        ("icon_status_health", "a crimson heart"), ("icon_status_power", "a gold clenched fist"),
        ("icon_status_defense", "a steel shield"), ("icon_status_critical", "a red starburst"),
        ("icon_status_healing", "a green leaf cross"), ("icon_status_resonance", "a violet tuning fork"),
        ("icon_status_timer", "a pale hourglass"), ("icon_class_hunter", "a hooded hunter head in profile"),
    ]:
        add(key, "assets/art/UI/icons/status", "medallion", subj)
    for key, subj in [
        ("icon_equipment_blade", "a sword blade"), ("icon_equipment_bow", "a longbow"),
        ("icon_equipment_spear", "a spear"), ("icon_equipment_scythe", "a scythe"),
        ("icon_equipment_helmet", "a knight helm"), ("icon_equipment_chest", "a breastplate"),
        ("icon_equipment_gloves", "a gauntlet"), ("icon_equipment_boots", "an armoured boot"),
        ("icon_equipment_accessory", "a jewelled amulet"),
    ]:
        add(key, "assets/art/UI/icons/equipment", "medallion", subj)

    # --- Navigation glyphs
    for key, subj in [
        ("nav_hunt", "crossed swords"), ("nav_forge", "a blacksmith hammer resting on an anvil, no cross"),
        ("nav_map", "a folded map"), ("nav_warren", "a burrow arch"),
        ("nav_build", "a branching skill tree"), ("nav_gear", "a breastplate"),
        ("nav_codex", "an open book"), ("nav_evolve", "a spiral of ascending motes"),
        ("nav_shop", "a coin pouch"), ("nav_mail", "a sealed envelope"),
        ("nav_stats", "a bar chart"), ("nav_prestige", "a many-pointed star"),
    ]:
        add(key, "assets/art/UI/icons/nav", "medallion", subj)

    # --- UI chrome
    for key, subj, w, h in [
        ("ui_panel_small", "an ornate rectangular panel with gold trim and a dark inset field", 256, 256),
        ("ui_panel_large", "a large ornate rectangular panel with gold trim and a dark inset field", 384, 256),
        ("ui_button_primary", "a raised rectangular button with gold trim", 256, 96),
        ("ui_button_secondary", "a flat rectangular button with thin gold trim", 256, 96),
        ("ui_button_confirm", "a raised green-tinted confirm button with gold trim", 256, 96),
        ("ui_button_danger", "a raised red-tinted danger button with gold trim", 256, 96),
        ("ui_button_disabled", "a flat grey inactive button", 256, 96),
        ("ui_tab_active", "a solid filled raised tab plate with a bright gold underline bar", 256, 96),
        ("ui_tab_inactive", "a dim recessed inactive tab", 256, 96),
        ("ui_slot_empty", "an empty square inventory slot with a dark recessed field", 128, 128),
        ("ui_slot_locked", "a square inventory slot with a padlock over a dark field", 128, 128),
        ("ui_medallion_round", "a plain round gold medallion frame with a hollow center", 128, 128),
        ("ui_medallion_hex", "a plain hexagonal gold medallion frame with a hollow center", 128, 128),
        ("ui_divider_long", "a long thin ornate horizontal divider rule", 256, 32),
        ("ui_divider_short", "a short ornate horizontal divider rule", 128, 32),
        ("ui_scrollbar_track", "a narrow vertical scrollbar track groove", 32, 256),
        ("ui_scrollbar_handle", "a narrow vertical scrollbar handle with gold trim", 32, 128),
        ("ui_keycap_square_blank", "a blank square keyboard keycap", 64, 64),
        ("ui_keycap_space_blank", "a blank wide spacebar keycap", 192, 64),
        ("ui_corner_top_left", "an ornate gold corner bracket, top-left", 64, 64),
        ("ui_corner_top_right", "an ornate gold corner bracket, top-right", 64, 64),
        ("ui_corner_bottom_left", "an ornate gold corner bracket, bottom-left", 64, 64),
        ("ui_corner_bottom_right", "an ornate gold corner bracket, bottom-right", 64, 64),
        ("ui_button_icon_close", "a small square button with an X glyph", 64, 64),
        ("ui_button_icon_back", "a small square button with a left arrow glyph", 64, 64),
        ("ui_button_icon_square", "a small blank square icon button", 64, 64),
    ]:
        add(key, "assets/art/UI/chrome", "chrome", subj, width=w, height=h)

    # --- The cutout rig's SOURCE FIGURE.
    #
    # The rig used to be cut out of `hunter_idle`, which is frame 0 of an animation strip — a pose
    # authored to look good in motion, not to be sliced. Everything that fought the cutter came from
    # that: a sword baked into the sprite crossing the leg, forearm and hand (4,000+ px of erasing); a
    # cloak wrapping the arms so no rectangle could separate sleeve from cape; and limbs pressed
    # against the torso, leaving no measurable boundary to cut on.
    #
    # This asset is authored FOR the cutter. The prompt asks for the three properties a rectangle
    # decomposition actually needs — a gap of background between each arm and the body, a gap between
    # the legs, and nothing draped across a joint — plus empty hands, because worn equipment is drawn
    # at the hand bone and the figure must not bring its own.
    a.append({
        "key": "hunter_rig_base", "dest": "assets/art/Characters/Hunter", "model": "pixen",
        "width": 512, "height": 512, "knockout": True, "single_subject": True,
        # Written entirely in the POSITIVE. A first attempt listed "NO cloak, NO cape, NO weapon,
        # NO straps across the chest" and got back a caped figure with a baldric — the same failure
        # as the pauldron prompts, where naming a thing summons it. What works is describing the
        # silhouette you want and then saying what the clothing IS, tightly.
        "prompt": "A fantasy hunter standing in a wide A-pose, front view, full body from hood to "
                  "boots, both arms angled outward and away from the sides so there is a large "
                  "triangle of empty background under each armpit, legs straight and set apart with "
                  "empty background between them, hands open and empty. "
                  "The outfit is CLOSE-FITTING ONLY: a short hood, a fitted leather jerkin ending at "
                  "the hip, bracers, trousers and boots. The empty background is visible everywhere "
                  "around the figure — behind the shoulders, behind the arms, behind the legs — "
                  "because there is nothing behind him. "
                  "Dark fantasy RPG pixel art, flat bold shapes, symmetrical, one figure centered on "
                  "a completely empty background, no text, no border, no frame",
    })

    # --- THE ROSTER: ten characters, as FLAT SPRITES. Ids in ROSTER_IDS at module scope, because
    # animations() needs the same list and a roster that drifted between the two would silently
    # animate nine characters and leave the tenth as a still.
    #
    # The cutout rig is retired. It bought articulated limbs and charged for them in constraints that
    # reached all the way back into the art: every source body had to stand in a wide A-pose with a
    # measurable gap under each armpit and between the legs, hold nothing, and wear nothing that hung —
    # because a rectangle cannot separate an arm from a cloak behind it. Four of those clauses are
    # design decisions the rig was making on the artist's behalf, and the roster is where that bill came
    # due: no weapons, no capes, no asymmetry, and a hit rate that needed an automated gate and repeated
    # re-rolls to reach.
    #
    # A sprite strip has none of that. It is also FEWER assets, not more — one base sprite plus two
    # eight-frame clips per character, against seventeen cut parts plus a drawn pauldron plus a pose
    # table — and the animation is authored rather than assembled from rotating rectangles, which is a
    # ceiling the rig could never get past (see HunterRigRenderer.DeathPose, capped at 0.4 rad because
    # flat cutouts visibly separate past it).
    #
    # The structure mirrors the MASTERY TREE, so the roster is legible rather than arbitrary: four
    # characters sit on the four opposed roads, four sit on the bridges between adjacent roads exactly
    # where the tree puts its own bridges, and two stand off the tree entirely.
    ROSTER = [
        # ── The four roads ────────────────────────────────────────────────────────────────────────
        ("seeker",
         "a lean HUMAN hooded ranger, an ordinary adult person with a plain human face, in a fitted "
         "leather jerkin and bracers, a short knife at the belt, hood up, watchful"),
        ("anvil",
         "a huge broad-shouldered warrior in a riveted dark steel cuirass and banded skirt, bare "
         "heavy forearms, a thick braided beard, standing with feet planted wide and fists closed"),
        ("chorus",
         "a bald ascetic in a plain quilted tunic, arms and throat wrapped in cord, a heavy belt "
         "strung with dozens of small bone charms that hang down the thigh"),
        ("metronome",
         "a wiry runner in a light fitted gambeson with tightly wrapped forearms and tall soft "
         "boots, hair tied back, poised mid-step on the balls of the feet"),
        ("unbroken",
         "a short immensely broad figure in a heavy overlapping scale coat, a plain featureless iron "
         "mask covering the whole face, thick banded greaves, arms folded"),
        # ── The four bridges, in the tree's own pairs ─────────────────────────────────────────────
        # "holding an enormous maul upright beside them" drew the maul BESIDE them — as a second
        # object standing on its own, twice, with the knight off to one side of it. A weapon has to be
        # described as connected to the body that carries it: both hands on the haft, the head on the
        # ground between the feet.
        ("tower",
         "a very tall top-heavy knight in dark plate, visor down, gripping the haft of an enormous "
         "two-handed maul in both gauntlets with the head of the maul resting on the ground between "
         "their own feet, leaning their weight onto it"),
        ("quiver",
         "a lean archer holding a tall unstrung longbow like a staff, a full back quiver of "
         "arrows over one shoulder, a short half-cape, hood down, sharp attentive face"),
        ("thornwall",
         "a heavy-set warden behind a battered tower shield planted on the ground, coils of black "
         "bramble and barbed wire wound around the shield and both forearms"),
        # The Endure+Weight bridge has no character. Ten was the brief and the structure wanted
        # eleven, and this is the corner ANVIL and UNBROKEN already stand on either side of — a
        # stone-skinned giant between them read as a boss rather than as somebody you play.
        # ── Off the tree ─────────────────────────────────────────────────────────────────────────
        ("oathbound",
         "a tall gaunt penitent in dark robes bound with many wound chains across the chest and "
         "arms, a strip of cloth sealed over the eyes, hands clasped and bound together"),
        ("magpie",
         "a grinning scavenger in cheerfully mismatched scavenged armour, an enormous overstuffed "
         "pack of loot on the back with pans and trinkets hanging off it, pouches everywhere"),
    ]
    for cid, look in ROSTER:
        add(f"char_{cid}_base", "assets/art/Characters/Roster", "hero", look,
            model="pixen", width=512, height=512, single_subject=True)

    # --- Hunter cutout rig parts: NOT GENERATED.
    #
    # hunter_part_* is produced by tools/asset-pipeline/cut_rig_parts.py, which slices them out of the
    # single hunter_idle sprite. They must RECONNECT, and independently generated limbs never do — the
    # first assembled figure came out as parts strung down the screen because each was invented with its
    # own proportions, lighting and joint width.
    #
    # Leaving them in this manifest was actively dangerous: the filenames match, so any --force run would
    # have quietly overwritten the cut parts with generated ones and broken the rig. (One did slip
    # through: hunter_part_cloak, for a bone that no longer exists.) The drawn pauldron is the exception
    # and is declared on its own, above.

    # --- Arena backgrounds: bg_arena_<theme> (Game1). 640x360 x3 = 1920x1080.
    arena = {
        "nature": "a mossy earth clearing floor, with vast twisted green trees and hanging moss behind it",
        "machine": "a flat riveted iron foundry floor, with cold furnaces and broken gantries behind it",
        "shadow": "a flat cracked black stone floor, with a torn void and dead sky behind it",
        "body": "a flat red bone-strewn moor floor, with a low blood moon and marsh behind it",
        "mind": "a flat polished crystal cavern floor, with vast faceted cyan formations behind it",
        "spirit": "a flat pale marble sanctum floor, with broken columns and drifting light behind it",
    }
    for s, subj in arena.items():
        add(f"bg_arena_{s}", "assets/art/Environments/arenas", "arena", subj,
            model="pixen", width=640, height=360, upscale=3, knockout=False, detail="highly detailed")

    # --- Screen backgrounds
    for key, subj in [
        ("bg_title", "a vast ruined cathedral of pale bone and tarnished gold, arches receding into blackness"),
        ("bg_forge", "a dark underground smithy, banked forge coals glowing dim orange, heavy anvils in shadow"),
        ("bg_warren", "a dark underground burrow complex of low nesting chambers lit by dim lanterns"),
        ("bg_regionmap", "a dark cartographer's chamber, weathered charts and brass instruments, dim candlelight"),
        ("bg_constellation", "a deep night sky of dim violet stars with faint constellation lines"),
    ]:
        add(key, "assets/art/Environments/screens", "background", subj,
            model="pixen", width=640, height=360, upscale=3, knockout=False, detail="highly detailed")

    # --- THE RIG'S ART IS NOT GENERATED ANY MORE, because the rig is gone.
    #
    # Fifty gear_<slot>_<trait> pieces, the drawn pauldron and the worn_<tier>_<bone> armour sets were
    # all authored for HunterRigRenderer: gear bound to bones, armour as drop-in replacement textures
    # for the bones a slot covers. That renderer is deleted, the champion is a flat sprite, and a
    # character's appearance is fixed.
    #
    # Removed from the SPEC, not merely deleted from disk, because a manifest entry is a standing
    # order: a plain `generate.py` run regenerates whatever is missing, and one did — it resurrected
    # the whole megabyte the same afternoon the rig was deleted, straight back into AssetLibrary's
    # startup scan. Dead art with a live manifest entry does not stay dead.
    #
    # Recoverable from git if the wardrobe is ever attempted again.

    # --- The two nav emblems that do not exist in any shipped set.
    #
    # Measured, not guessed: scoring every nav_*/state_* asset by how much BRIGHT pixel there is in
    # its centre disc shows HUNT/GEAR/STATS/BUILD/MAP/DUST all carry a symbol (0.26-0.69) while
    # nav_forge, nav_forge_128, nav_warren and nav_warren_128 all score 0.00 — they are ornate rings
    # around an empty dark centre. Every scene audit independently reported the rail as "some tabs
    # have a symbol, some are blank", and this is why.
    # Style "item", not "medallion", for the same reason the facility icons needed it: the medallion
    # prompt asks for a "dark near-black center", so the symbol comes out dark on dark and vanishes at
    # the 48px a nav tile gives it. Measured 0.00 bright-centre both times before the switch.
    for key, motif in {
        "icon_nav_forge": "a bright polished gold blacksmith hammer crossed over a gold anvil",
        "icon_nav_warren": "three bright gold arched burrow entrances in a mound",
    }.items():
        add(key, "assets/art/UI/icons/nav", "item",
            f"{motif}, glowing warm gold, filling the frame",
            negative_description="frame, border, ring, circle, medallion, rim, coin, badge, "
                                 "dark center, black center, text")

    # --- Warren facility icons: icon_facility_<kind> (WarrenScreen grid)
    #
    # These cards drew a flat coloured HEXAGON where the facility's picture belongs — eight identical
    # shapes distinguished only by tint, so the grid read as a colour swatch chart. A facility is a
    # place; give each one an image of itself.
    facility_motif = {
        "nursery": "a woven straw nest cradling three pale speckled eggs",
        "tunnels": "a dark round burrow mouth cut into earth, timber-braced",
        "foragingpits": "a shallow dug pit heaped with roots and mushrooms",
        "scavengerruns": "a bundle of scavenged scrap bones and rags tied with cord",
        "breedingchamber": "a warm bedded alcove lined with fur",
        "ritualnest": "a ring of standing candles around a rune-marked stone",
        "hoardvaults": "a heavy iron-banded strongbox spilling coins",
        "sentryburrows": "a raised watch-mound with a wooden lookout stake",
    }
    # Style "item", not "medallion". The medallion style spends the canvas on a baroque gold RIM around a
    # "dark near-black center" — at the 88px a card gives it, seven of the eight came back as an ornate
    # ring with an empty black hole in the middle. The card already supplies the frame (a tinted hex
    # plate), so what is needed here is the object filling the frame, nothing else.
    for kind, motif in facility_motif.items():
        add(f"icon_facility_{kind}", "assets/art/UI/icons/facilities", "item",
            f"{motif}, filling the frame",
            negative_description="frame, border, ring, circle, medallion, rim, filigree, coin, "
                                 "badge, gold trim, text")

    # --- The MAP screen's chart field.
    #
    # It had been reusing bg_regionmap — the cartographer's-chamber art already covering the whole screen
    # behind the panels — under an 0x88 scrim, so the map panel read as a black void with six cards
    # floating in it. The nodes want to sit ON a map; this is that map.
    a.append({
        "key": "bg_mapfield", "dest": "assets/art/Environments/screens", "model": "pixen",
        # pixen caps the canvas at a 512x512 AREA (262144 px), so 768x576 is refused; 576x428 keeps the
        # map panel's 1.35 aspect inside that budget and doubles cleanly to cover it.
        "width": 576, "height": 428, "knockout": False, "upscale": 2,
        "prompt": "An aged parchment campaign map, blank hand-inked chart with faint coastlines, "
                  "contour hatching, compass rose in one corner and a worn creased border. "
                  "Dark fantasy pixel art, muted browns and dim ochre, low contrast, evenly lit, "
                  "NO characters, NO icons, NO markers, NO pins, NO text, NO labels, NO UI, NO frame",
    })

    # --- Memory-Dust blessing category icons: icon_blessing_<category> (PrestigeScreen)
    #
    # Every one of the 44 blessing cards drew the SAME ui_memory_dust blob, recoloured by state — so the
    # grid gave the player no way to tell an Amplifier from an Expansion without reading the label. Three
    # icons, one per category, is the smallest set that makes the grid scannable.
    for cat, motif in {
        "amplifier": "a rising three-step chevron arrow pointing up",
        "expansion": "an open archway gate with a keystone",
        "convenience": "a slim hourglass with running sand",
    }.items():
        add(f"icon_blessing_{cat}", "assets/art/UI/icons/blessings", "medallion", motif)

    # --- MASTERY TREE node art: icon_branch_<branch> + ui_node_<kind> (BuildScreen.DrawNode)
    #
    # A node was a branch-coloured square with, on the bigger kinds, a smaller square in the middle of
    # it. That carries two facts — which branch, roughly how expensive — and it carries them in the two
    # channels a player reads LAST (hue and area). Everything else about a node lived in text.
    #
    # The split is deliberate: the FRAME says what KIND of commitment this is, the GLYPH inside says
    # which BRANCH it belongs to. One is the socket, the other is what is set in it, so four glyphs and
    # seven frames cover all thirty-odd nodes instead of thirty-odd bespoke pictures — and a new node
    # added to the catalogue arrives already drawn.
    branch_glyph = {
        # WEIGHT is fewer, heavier hits. A maul head is the blunt-mass silhouette, and it is the one
        # weapon shape that cannot be confused with the Spread arrows or a Tempo bolt at 40px.
        "weight": "a massive blunt double-headed iron maul head, squat and heavy, seen straight on",
        # SPREAD is generated OUT OF BAND — see the literal prompt below the loop. Left here so the
        # four branches read as one set in source; the loop skips it.
        "spread": None,
        # TEMPO is rate. A forked bolt reads as speed at any size and needs no legend.
        "tempo": "one jagged forked lightning bolt striking downward",
        # ENDURE is staying alive. A tower shield, not a round one: round reads as a boss or a coin.
        "endure": "one tall rectangular tower shield with a raised centre spine and a studded rim",
    }
    for br, motif in branch_glyph.items():
        if motif is None:
            continue
        add(f"icon_branch_{br}", "assets/art/UI/icons/branches", "glyph", motif,
            width=192, height=192, shading="flat shading", detail="low detail", single_subject=True)

    # SPREAD is NOT generated, and is deliberately absent from this manifest — see
    # draw_branch_spread.py, which authors it. Six attempts returned a feathered trident, a single
    # arrow, a spread-winged phoenix, a solid crescent moon, a trident embossed on a SHIELD (Endure's
    # glyph, the worst collision available) and a bare corner bracket. "One line that becomes three"
    # has no name of its own in the model's vocabulary, so every phrasing landed on the nearest shape
    # that does — and the last two attempts dropped the "heraldic" style prefix and still failed.
    #
    # It is listed here only so a reader of this file finds out where it went. Adding a key back into
    # the manifest would let a `--force` run overwrite the authored art with a seventh guess, which is
    # exactly the trap the cut rig parts are kept out of the manifest to avoid.

    # The FRAMES. Each is a socket with a hollow middle, because the branch glyph and the node's state
    # colour are both drawn through it. Ring count and spikes climb with price so the shape of a
    # commitment reads before its label does — a Mastery must not be merely a bigger Minor.
    for key, w, h, subj in [
        # The hub the whole tree grows from, and the only node that is not a purchase. A heavy
        # eight-pointed rose, because the first pass returned a hairline ring that read as the least
        # important thing on a page it is the centre of.
        ("ui_node_start", 192, 192,
         "a heavy eight-pointed compass rose star with a thick round rim band around it and a hollow "
         "centre"),
        # The CHEAPEST node must look the cheapest. The first pass came back studded and gold-flecked —
        # more ornate than the Notable that costs three times as much — which inverted the whole
        # price ladder the shapes exist to state. Undecorated, and say so several ways.
        # ...and "ring" on its own is a piece of JEWELLERY, so the second pass returned a bangle drawn
        # in three-quarter perspective. Every other frame in this set is flat and face-on; a tilted one
        # would read as an item that had fallen into the tree.
        ("ui_node_minor", 192, 192,
         "one plain smooth undecorated circular socket frame drawn perfectly flat and face on, a "
         "single unbroken narrow band of dull metal, no ornament of any kind anywhere on it, "
         "hollow centre"),
        ("ui_node_notable", 192, 192,
         "a hexagonal socket frame with a beaded rim and a small stud at each of the six corners, "
         "hollow centre"),
        ("ui_node_greater", 192, 192,
         "a heavy round socket frame ringed by twelve short radiating spikes, hollow centre"),
        ("ui_node_bridge", 192, 192,
         "a square socket frame clasped by one heavy chain link across its left side and another "
         "across its right side, hollow centre"),
        ("ui_node_spec", 192, 192,
         "a diamond socket frame standing on one point with a small flourish at each of its four "
         "corners, hollow centre"),
        # The one wide asset: a Mastery is drawn as a plaque, not a stud, because it is the branch's
        # whole identity and its name is printed inside it.
        ("ui_node_mastery", 320, 160,
         "a wide ornate banner plaque with scrolled ends, a heavy trimmed rim and a hollow centre "
         "field"),
    ]:
        # The Minor carries an extra refusal list of its own: the shared "chrome" style asks for ornate
        # gold trim in its very first clause, and on the plainest frame in the set that clause is the
        # thing fighting the subject.
        extra = (", spikes, studs, rivets, gems, jewels, flourishes, filigree, ornament, engraving, "
                 "scrollwork, points, teeth, perspective, tilted, angled, three-quarter view, "
                 "isometric, ring, bracelet, bangle, jewellery, wedding band"
                 if key == "ui_node_minor" else "")
        add(key, "assets/art/UI/nodes", "chrome", subj + ", transparent hollow middle, empty centre",
            width=w, height=h,
            negative_description="text, letters, numbers, icon, symbol, glyph, emblem, face, "
                                 "creature, filled centre, solid centre, plate, disc" + extra)

    # --- FORM glyphs: icon_form_<form> (WeaveScreen)
    #
    # The six Forms are the whole of what a skill IS, and until now they were six words in a cycling
    # cell. A Form is picked far more often than a road is, so it earns a picture more than a road does.
    # Tinted at the draw site by the skill's Source, so one glyph serves every colour.
    form_glyph = {
        "strike": "one heavy straight sword blade pointing straight down, plain crossguard",
        "projectile": "one arrow pointing straight up with a broad head and flights at the bottom",
        "aura": "three plain concentric rings, one inside the next, with clear gaps between them",
        "trap": "one open toothed jaw trap seen from the side, both jaws sprung wide apart",
        "mark": "one crosshair: a plain circle with four short straight lines crossing it, and one solid "
                "dot at the exact centre",
        # A thin spiral knocked out to 100% transparency — there was not enough of it left to be an
        # image. A transform symbol needs MASS: one thick band bent nearly into a circle reads the same
        # and survives the background removal.
        "transformation": "one very thick curved arrow bent around into a nearly complete circle, its "
                          "broad arrowhead almost touching its own tail, wide heavy band",
    }
    for fm, motif in form_glyph.items():
        add(f"icon_form_{fm}", "assets/art/UI/icons/forms", "glyph", motif,
            width=192, height=192, shading="flat shading", detail="low detail", single_subject=True)

    # --- TRAIT ROAD glyphs: icon_road_<road> (PrestigeScreen)
    #
    # The four roads plus the spine are the entire decision this screen exists to present, and they were
    # five words in five colours. Tinted at the draw site, so they arrive as flat stencils.
    road_glyph = {
        "spine": "an upright segmented backbone column of five stacked vertebrae",
        "ruin": "one cracked skull split down the middle by a jagged fissure",
        "aegis": "one battlemented fortress wall with a crenellated top and a barred gate",
        "avarice": "one heaped mound of coins overflowing from a torn purse",
        "artifice": "two interlocking toothed gears with a single thread woven between them",
    }
    for rd, motif in road_glyph.items():
        add(f"icon_road_{rd}", "assets/art/UI/icons/roads", "glyph", motif,
            width=192, height=192, shading="flat shading", detail="low detail", single_subject=True)

    # --- TERMINAL emblems: art_terminal_<keystone> (PrestigeScreen detail panel)
    #
    # The four 12-point terminals are the most expensive things in the game and two of them are
    # unreachable in a career. Every one of them drew the same generic category blob. These are the only
    # trait art that is NOT tinted — a terminal should look like the thing you spent thirty points on.
    for key, subj in [
        ("art_terminal_reaper", "a long curved reaping scythe crossed over a full blood-red harvest "
                                "moon, ears of black wheat below"),
        ("art_terminal_titan", "a colossal horned iron helm set into a mountain crag, unbroken and "
                               "immovable"),
        ("art_terminal_hoarder", "a great vault door standing open with a wave of gold coins pouring "
                                 "out of it"),
        ("art_terminal_weaver", "a spindle with two glowing threads twisting together into one "
                                "braided cord"),
    ]:
        add(key, "assets/art/UI/terminals", "medallion", subj, width=256, height=256)

    # --- The unlock flourish: vfx_trait_burst / vfx_trait_ring (PrestigeScreen celebration)
    #
    # Both carry a literal prompt. Every shared still style asks for an OBJECT — an icon, an item, a
    # framed badge — and what is wanted here is light with no subject in it at all.
    a.append({
        "key": "vfx_trait_burst", "dest": "assets/art/VFX/traits",
        "width": 256, "height": 256, "knockout": True,
        "prompt": "A radial star flare: many long thin straight rays of light shooting outward in "
                  "every direction from one small brilliant point at the exact centre, perfectly "
                  "symmetrical, pale white and warm gold, fading out toward the edges, on an empty "
                  "background. Pixel art. NO object, NO character, NO creature, NO frame, NO border, "
                  "NO ring, NO circle outline, NO text",
        "outline": "lineless", "shading": "flat shading",
    })
    # There is NO generated shockwave ring, on purpose. Two attempts both returned a SUN — a filled
    # disc with a corona — because that is what a bright circle is in almost all training data, and the
    # second attempt refused "disc, filled circle, ball, sphere, orb, sun, moon, planet, coin" by name
    # and still filled the middle in. A hole is not a subject the model can be argued into drawing.
    #
    # It is also the one effect that does not want a texture. A ring expands, and a scaled-up 256px
    # sprite goes soft exactly when it is biggest; PrestigeScreen plots the circumference directly
    # (PrestigeScreen.Ring), which stays one crisp pixel band at any radius and costs no asset at all.
    # Generate the right asset — or, when the right asset is a formula, do not generate one.

    # --- Loot chest
    add("chest_loot", "assets/art/ItemsLoot/chests", "item", "a closed ornate treasure chest with gold bands")

    # --- Keys referenced by the code that predate this spec. Kept so nothing is
    #     left rendering old painted art next to the new set.
    for key, subj, w, h in [
        ("ui_panel_medium", "an ornate rectangular panel with gold trim and a dark inset field", 256, 192),
        ("ui_panel_square", "an ornate square panel with gold trim and a dark inset field", 256, 256),
        ("ui_panel_vertical", "a tall ornate panel with gold trim and a dark inset field", 192, 256),
        ("ui_panel_modal_wide", "a wide ornate modal panel with gold trim and a dark inset field", 384, 224),
        ("ui_slot_skill_hex", "an empty hexagonal skill slot with a dark recessed field and gold rim", 128, 128),
        ("ui_slot_trinket_round", "an empty round trinket slot with a dark recessed field and gold rim", 128, 128),
    ]:
        add(key, "assets/art/UI/chrome", "chrome", subj, width=w, height=h)

    for key, subj in [
        ("nav_hunt_128", "crossed swords"), ("nav_forge_128", "a blacksmith hammer on an anvil"),
        ("nav_warren_128", "a burrow arch"), ("nav_inventory_128", "an open satchel"),
        ("nav_relics_128", "an ancient rune stone"),
        ("state_mastery_128", "a laurel wreath around a star"),
        ("state_resonance_128", "a vibrating tuning fork"),
    ]:
        add(key, "assets/art/UI/icons/nav", "medallion", subj, width=128, height=128)

    add("core_hatch", "assets/art/ItemsLoot/materials", "item",
        "a cracked glowing egg-like machine core hatching open")
    # Matches hunter_rig_base: a stern man's face under a dark green hood, not the faceless void the
    # previous figure had. The HUD portrait sits beside the character it is supposed to BE.
    add("hunter_portrait", "assets/art/Characters/Hunter/portraits", "medallion",
        "the face of a stern bearded man looking forward from under a dark green hood, "
        "brown leather collar")
    add("logo_horizontal_full", "assets/art/BrandingSymbols/branding", "chrome",
        "an ornate gold heraldic crest emblem with radiating filigree wings", width=384, height=192)
    return a


def animations() -> list[dict]:
    a: list[dict] = []

    # Enemy idle + attack strips. Statics (<en>_idle_01 / <en>_attack_01) are
    # sliced out of these afterwards, so they cost nothing extra.
    for en, (src, desc, act) in ENEMIES.items():
        col, edge = SOURCE_ART[src]
        base = f"{desc}, {col} colouring, {edge}"
        a.append({"key": f"{en}_idle_strip8_512", "dest": f"assets/art/Animations/Enemies/{en}_idle",
                  "prompt": f"{base}. Dark fantasy RPG enemy sprite, front facing, full body, "
                            f"bold readable silhouette, flat bold shapes, centered, plain background, no text",
                  "action": "breathing and swaying gently in place", "frame_count": 8})
        a.append({"key": f"{en}_{act}_strip8_512", "dest": f"assets/art/Animations/Enemies/{en}_{act}",
                  "prompt": f"{base}. Dark fantasy RPG enemy sprite, front facing, full body, attacking pose, "
                            f"bold readable silhouette, flat bold shapes, centered, plain background, no text",
                  "action": "lunging forward to strike then recoiling back", "frame_count": 8,
                  "drift_threshold": 0.35})

    # Boss idle + attack strips at 1024 frames (Game draws body ~540px tall).
    for boss, (src, desc) in BOSSES.items():
        col, edge = SOURCE_ART[src]
        base = f"{desc}, {col} colouring, {edge}"
        for clip, action in [("idle", "breathing slowly and swaying in place"),
                             ("attack", "raising up and striking forward then recovering")]:
            a.append({"key": f"{boss}_{clip}_strip8_1024",
                      "dest": f"assets/art/Animations/Bosses/{boss}_{clip}",
                      "prompt": f"{base}. Imposing dark fantasy RPG boss sprite, front facing, full body, "
                                f"bold readable silhouette, flat bold shapes, centered, plain background, no text",
                      "action": action, "frame_count": 8, "frame_size": 1024,
                      "drift_threshold": 0.35})

    # Hunter clips. hunter_idle / hunter_attack_01 / hunter_defeated statics are
    # sliced from these.
    # EMPTY-HANDED on purpose. Equipped gear is drawn over this base at sockets
    # (SoloExpeditionScreen.Sockets), so a weapon baked into the base gives the
    # champion two swords the moment the player equips one.
    hunter = ("a lone hooded hunter in bone-parchment cloth and cold-slate leather, "
              "EMPTY HANDS, no weapon, no sword, no shield, unarmed")
    for clip, action in [("idle", "standing and breathing in place"),
                         ("attack", "swinging a blade forward then recovering"),
                         ("cast", "raising a hand and channelling energy"),
                         ("hurt_recover", "flinching back from a hit then straightening"),
                         ("death", "staggering and collapsing to the ground")]:
        a.append({"key": f"hunter_{clip}_strip8_512", "dest": f"assets/art/Animations/Hunter/hunter_{clip}",
                  "prompt": f"{hunter}. Dark fantasy RPG player sprite, front facing, full body, "
                            f"bold readable silhouette, flat bold shapes, centered, plain background, no text",
                  "action": action, "frame_count": 8, "drift_threshold": 0.35})

    # THE ROSTER'S CLIPS. Animated FROM the approved base sprite (`source`), never from a fresh prompt.
    #
    # That is the whole reason the roster can be flat sprites at all. A prompt-driven strip re-invents
    # the character on every clip — the idle and the attack come back as two different people, which is
    # exactly what the cutout rig existed to prevent and what it charged four art constraints for.
    # Animating from the sprite keeps the design fixed and lets the motion be authored, so a strip is
    # both cheaper (8 generations, not 9) and more on-model than the rig ever was.
    #
    # Two clips each, and only two: idle is what the champion does for almost the whole expedition, and
    # attack is the one beat the combat loop drives. Death and cast can follow once these are proven —
    # every clip is eight more generations per character, and ten characters make that a real number.
    # AN ACTION A POSE CAN ACTUALLY PERFORM.
    #
    # One attack line for ten characters worked for seven of them and could not work for three, and the
    # gate said so in numbers before I read the prompts: TOWER's clip drifted 21-48% in silhouette height
    # against a 12% cap, over six rolls, while every passing attack sat between 1.3% and 8.6%. The cause
    # is in the BASE pose, not the animator. TOWER stands with the head of its maul resting on the ground
    # between its feet, leaning its weight onto it — "swinging forward to strike" orders the model to
    # hoist a weapon off the floor, and the silhouette grows by half doing it. QUIVER holds an unstrung
    # longbow LIKE A STAFF; you cannot swing that. OATHBOUND's hands are clasped AND BOUND TOGETHER, and
    # its eyes are sealed. The seven that passed all have free hands and a neutral stance.
    #
    # So the three get an action their own body can do without re-inventing itself. Same principle the
    # padding uses: author the input so the tool succeeds, rather than arguing with the tool.
    ATTACK_ACTION = {
        # The maul never leaves the floor — it is driven INTO it. Also what MOMENTUM should look like.
        "tower": "leaning hard onto the maul and driving its head down into the ground, then straightening",
        # A drawn bow is the same height as a held one; a swung one is not.
        "quiver": "raising the longbow and drawing the string back to the cheek, then loosing",
        # Bound hands can still be thrust forward, and the chains pull taut when they are.
        "oathbound": "bowing the head and thrusting both bound hands forward, the chains pulling taut",
    }
    DEFAULT_ATTACK = "swinging forward to strike and then recovering to a stand"

    for cid in ROSTER_IDS:
        for clip, action in [("idle", "standing and breathing in place, shifting weight very slightly"),
                             ("attack", ATTACK_ACTION.get(cid, DEFAULT_ATTACK))]:
            a.append({
                "key": f"char_{cid}_{clip}_strip8_512",
                "dest": f"assets/art/Animations/Roster/{cid}_{clip}",
                "source": f"assets/art/Characters/Roster/char_{cid}_base.png",
                "action": action, "frame_count": 8,
                # HOW MUCH ROOM TO LEAVE THE ANIMATOR, and it is not the same for both clips.
                #
                # The animator crops what it is given, so the source is letterboxed to leave the crop
                # somewhere to land that is not the character. 0.62 holds for an IDLE. An ATTACK does
                # not: measured over four rolls, every seeker attack came back a waist-up bust at 0.62
                # while its idle passed at the same value on the second. A swing is a more cinematic
                # instruction than standing still, and the model answers it with a closer shot — so an
                # attack gets half the canvas rather than five eighths.
                "pad_fraction": 0.50 if clip == "attack" else 0.62,
                # Per-frame stray removal. The animator hallucinates spare parts exactly as the still
                # generators do — measured across the first twenty clips: a black spike floating above
                # a head, and one attack clip carrying seventeen loose blobs. They are always
                # DISCONNECTED, so keeping the largest component deletes them and touches nothing else.
                "single_subject": True,
                # The attack clip moves the figure a long way from frame 0, so the drift guard has to be
                # loose or every attack strip is rejected — same value the hunter and enemy attacks use.
                **({"drift_threshold": 0.35} if clip == "attack" else {}),
            })

    # VFX. No sprite to animate from, so frame 1 is generated then animated.
    for key, dest, subj, action in [
        ("impact_gold_strip8_512", "assets/art/VFX/impact/gold",
         "a bright gold impact starburst", "the burst expanding outward and fading"),
        ("impact_crit_strip8_512", "assets/art/VFX/impact/crit",
         "a fierce gold and crimson crit burst of shards", "the burst expanding outward and fading"),
        ("smoke_puff_strip8_512", "assets/art/VFX/smoke/smoke_puff",
         "a grey smoke puff", "the smoke billowing out and dissipating"),
        ("death_dissolve_strip8_512", "assets/art/VFX/death/dissolve",
         "a swirl of dark violet motes and ash", "the motes rising and scattering apart"),
        ("interrupt_break_strip8_512", "assets/art/VFX/interrupt/break",
         "a cracked white shield sigil", "the sigil cracking then shattering apart"),
        ("heal_holy_burst_strip8_512", "assets/art/VFX/heal/holy_burst",
         "a warm green and gold healing bloom", "the bloom opening upward and fading"),
        ("levelup_gold_purple_strip8_512", "assets/art/VFX/levelup/gold_purple",
         "a column of gold and violet ascending light", "the column rising and flaring out"),
        ("loot_pop_strip8_512", "assets/art/VFX/loot/loot_pop",
         "a small gold sparkle pop", "the sparkle bursting outward and fading"),
        ("projectile_arcane_strip8_512", "assets/art/VFX/projectile/arcane",
         "a violet arcane energy bolt", "the bolt streaking forward with a trailing tail"),
        ("aura_arcane_ring_strip8_512", "assets/art/VFX/aura/arcane_ring",
         "a flat violet arcane ground ring", "the ring pulsing and rotating slowly"),
        ("binding_void_bind_strip8_512", "assets/art/VFX/binding/void_bind",
         "dark violet binding chains in a circle", "the chains tightening inward"),
    ] + [
        (f"slash_{s}_strip8_512", f"assets/art/VFX/slash/{s}",
         f"a curved {SOURCE_ART[s][0]} slash arc with {SOURCE_ART[s][1]}",
         "the slash sweeping across and fading")
        for s in SOURCES
    ]:
        a.append({"key": key, "dest": dest, "prompt": ANIM_STYLE_VFX.format(subject=subj),
                  "action": action, "frame_count": 8, "outline": "selective outline"})
    return a


def main() -> None:
    still = stills()
    anim = animations()

    manifest = {
        "$comment": ["Generated by build_spec.py from the game's runtime contract.",
                     "Do not hand-edit — edit build_spec.py and re-run."],
        "defaults": {"width": 256, "height": 256, "no_background": True,
                     "outline": "single color black outline", "shading": "medium shading",
                     "detail": "medium detail", "text_guidance_scale": 9, "knockout": True,
                     "style_suffix": ""},
        "styles": STILL_STYLES,
        "assets": still,
    }
    with open(os.path.join(HERE, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)
    with open(os.path.join(HERE, "animations.json"), "w", encoding="utf-8") as fh:
        json.dump({"$comment": ["Generated by build_spec.py."], "animations": anim}, fh, indent=2)

    keys = [x["key"] for x in still]
    assert len(keys) == len(set(keys)), "duplicate still key"
    akeys = [x["key"] for x in anim]
    assert len(akeys) == len(set(akeys)), "duplicate animation key"

    print(f"stills     : {len(still):4d}  ({len(still)} generations)")
    print(f"animations : {len(anim):4d}  ({sum(8 + (0 if x.get('source') else 1) for x in anim)} generations)")
    print(f"TOTAL COST : {len(still) + sum(8 + (0 if x.get('source') else 1) for x in anim)} generations")


if __name__ == "__main__":
    main()
