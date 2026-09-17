"""Splice the 2026-09-16 region x archetype enemy matrix into spec.json, byte-preserving the rest.

    python tools/asset-pipeline/v2/add_enemy_matrix.py

The matrix is PRESENTATION: gameplay Source stays whatever Core says. One entry per
(region, archetype) cell; the art key is `<region slug>_<archetype>` and the clips are
idle / attack / death on the south-west rotation (enemies face LEFT), per
design/art/arena-art-contract.md. Routes: "character" = create_character v3 side 128 +
animate_character; "image" = create_image_pixen still + animate_image (non-humanoids).
"""
from __future__ import annotations

import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC = os.path.join(HERE, "spec.json")

DEATH_STAND = ("dying: staggers, drops to the knees, then collapses forward onto the ground and lies "
               "still, fully fallen by the last frame; a clear visible body remains in every frame "
               "including the last one, never empty")
DEATH_HOVER = ("dying: the glow gutters, it sinks to the ground and crumples in a heap, fully fallen "
               "by the last frame; a clear visible remnant remains in every frame including the last "
               "one, never empty")
DEATH_CRAWL = ("dying: the legs buckle, the body tips over onto its side and lies still, fully "
               "fallen by the last frame, facing left the whole time; a clear visible body remains "
               "in every frame including the last one, never empty")
IDLE_CRAWL = ("idle breathing in place: the body rising and sinking slightly, legs shifting weight, "
              "staying on the same spot, facing left the whole time; returns to the start by the "
              "last frame so it loops")
IDLE_HOVER_SMALL = ("idle: hovering in place, bobbing up and down gently, the glow pulsing, staying on "
                    "the same spot, facing left the whole time; returns to the start by the last "
                    "frame so it loops")
STAND_IDLE = "idle"
HOVER_IDLE = "hover_idle"

R = {
    "verdant": ("verdant_hollow", "Nature"),
    "cinder": ("cinderworks", "Machine"),
    "umbral": ("umbral_reach", "Shadow"),
    "marrow": ("marrow_wastes", "Body"),
    "archive": ("still_archive", "Mind"),
    "choir": ("pale_choir", "Spirit"),
}

CELLS = {
    # ── VERDANT HOLLOW — thorn, moss, root, rot, predatory forest life
    "verdant_swarm": dict(name="BRIAR MITES", route="image", size=192,
        look="a small round thorny beetle creature of dark moss and bramble, six spiny legs, two glowing green eyes, tiny thorn mandibles, low to the ground, three-quarter side view facing left",
        idle=IDLE_CRAWL,
        attack="attack: rears up briefly then lunges forward to the LEFT snapping its thorn mandibles, then scuttles back to the starting spot, facing left the whole time",
        death=DEATH_CRAWL),
    "verdant_caster": dict(name="BOG WEAVER", route="character", size=128,
        look="a tall thin hooded figure grown of moss and hanging roots, a hollow cowl of bark with two glowing green spore-lights for eyes, long ragged root-fibre sleeves, holding a crooked root staff tipped with a glowing seed pod, floating just above the ground",
        idle=HOVER_IDLE,
        attack="the same figure throughout, never changing outfit or shape: raises the root staff, the seed pod flares green, then thrusts the staff forward releasing a burst of spores toward the front, the release landing at the fifth frame, then settles back to the hovering start by the last frame, the staff still in hand",
        death=DEATH_HOVER),
    "verdant_armoured": dict(name="BARK WARDEN", route="character", size=128,
        look="a squat broad figure entirely plated in thick overlapping bark and shell, a closed helm of knotted wood with a narrow glowing green slit, a huge round shield of tree-ring wood held in front covering most of the body, thick stumpy legs, solid planted stance",
        idle="standing idle, feet planted and never moving: the shield lowering and lifting slightly with slow breathing, moss swaying, the slit glow pulsing; returns to the start by the last frame so it loops seamlessly",
        attack="the same figure throughout, never changing outfit or shape: rams the whole tree-ring shield forward, the bark face leading, the impact landing at the fifth frame, then settles back to the starting stance by the last frame, the shield still held in front",
        death=DEATH_STAND),
    "verdant_bruiser": dict(name="THORN OGRE", route="character", size=128,
        look="a huge broad-shouldered ogre of bramble and rotten wood, enormous arms, a small head sunk between vast thorny shoulders, hanging vines, one arm ending in a massive club of twisted root, glowing green eyes, feet planted wide",
        idle=STAND_IDLE,
        attack="the same figure throughout, never changing outfit or shape: heaves the root club up over the head and slams it down into the ground in front, the impact landing at the fifth frame, then straightens back to the starting pose by the last frame, the club still in hand",
        death=DEATH_STAND),
    # ── CINDERWORKS — forge, slag, furnace, steam, industrial constructs
    "cinder_swarm": dict(name="CINDER GNATS", route="image", size=192,
        look="a small brass clockwork insect with a glowing orange ember for an abdomen, four thin jointed metal legs, two stubby smokestack wings puffing sparks, low to the ground, three-quarter side view facing left",
        idle="idle in place: the ember pulsing, the legs shifting weight, tiny sparks puffing from the stacks, staying on the same spot, facing left the whole time; returns to the start by the last frame so it loops",
        attack="attack: rears up then darts forward to the LEFT with a snap of hot sparks, then skitters back to the starting spot, facing left the whole time",
        death=DEATH_CRAWL),
    "cinder_caster": dict(name="FURNACE PRIEST", route="character", size=128,
        look="a tall gaunt figure in soot-black robes wearing a tall iron chimney mitre, the face hidden behind a glowing orange furnace grate, holding a long bellows staff leaking steam, floating slightly on a cushion of steam",
        idle=HOVER_IDLE,
        attack="the same figure throughout, never changing outfit or shape: raises the bellows staff, the grate flares orange, then thrusts the staff forward blasting a gout of steam and sparks toward the front, the blast landing at the fifth frame, then settles back to the hovering start by the last frame, the staff still in hand",
        death=DEATH_HOVER),
    "cinder_armoured": dict(name="BOILER KNIGHT", route="character", size=128,
        look="a squat wide iron construct of riveted boiler plates, a round furnace door glowing orange in the chest, a domed rivet helm with a narrow slit, thick piston legs, huge iron gauntlets clenched, solid planted stance",
        idle="standing idle, feet planted and never moving: the furnace door glow pulsing, steam venting from the joints, the heavy shoulders rising and falling slowly; returns to the start by the last frame so it loops seamlessly",
        attack="the same figure throughout, never changing outfit or shape: rears back and drives both iron gauntlets forward in a crushing double punch, the impact landing at the fifth frame, then straightens back to the starting stance by the last frame",
        death=DEATH_STAND),
    "cinder_bruiser": dict(name="SLAG HULK", route="character", size=128,
        look="a massive broad brute of cooling black slag and iron, glowing orange cracks across the body, enormous fists like sledgehammer heads, a tiny head with one glowing ember eye, vast shoulders, feet planted wide",
        idle=STAND_IDLE,
        attack="the same figure throughout, never changing outfit or shape: heaves one enormous hammer fist up and slams it down into the ground in front, sparks flying, the impact landing at the fifth frame, then hauls it back up to the starting stance by the last frame",
        death=DEATH_STAND),
    # ── UMBRAL REACH — shadow, void, torn silhouettes, impossible dark predators
    "umbral_swarm": dict(name="GLOOM WHELPS", route="image", size=192,
        look="a small hunched shadow creature with torn ragged edges, four thin legs, a long tail of drifting darkness, two pale glowing eyes, no visible mouth, low to the ground, three-quarter side view facing left",
        idle="idle in place: the ragged edges flickering and drifting, the body breathing, the eyes blinking, staying on the same spot, facing left the whole time; returns to the start by the last frame so it loops",
        attack="attack: coils back then pounces forward to the LEFT with a slash of shadow, then springs back to the starting spot, facing left the whole time",
        death=DEATH_CRAWL),
    "umbral_caster": dict(name="HOLLOW SEER", route="character", size=128,
        look="a tall thin hooded figure of torn indigo shadow with nothing inside the cowl but a single pale glowing eye, long torn sleeves trailing into darkness, no visible hands, floating just above the ground",
        idle=HOVER_IDLE,
        attack="the same figure throughout, never changing outfit or shape: the cowl tilts forward, the single eye flares pale white, and a lash of shadow whips forward from the sleeves toward the front, landing at the fifth frame, then it settles back to the hovering start by the last frame",
        death=DEATH_HOVER),
    "umbral_armoured": dict(name="NIGHT CARAPACE", route="character", size=128,
        look="a squat figure completely enclosed in overlapping plates of black chitin, a closed beetle-like helm with two pale glowing slits, plated arms folded like a shell across the front, short thick plated legs, solid planted stance",
        idle="standing idle, feet planted and never moving: the chitin plates shifting slightly with slow breathing, the pale slits pulsing; returns to the start by the last frame so it loops seamlessly",
        attack="the same figure throughout, never changing outfit or shape: the folded arm plates snap open and ram forward like a battering shell, the impact landing at the fifth frame, then fold shut again back to the starting stance by the last frame",
        death=DEATH_STAND),
    "umbral_bruiser": dict(name="DUSK BRUTE", route="character", size=128,
        look="a huge broad ape-like brute of solid shadow with torn ragged edges, enormous long arms with hooked claws, a small head with pale glowing eyes, vast shoulders, knuckles resting on the ground, feet planted wide",
        idle=STAND_IDLE,
        attack="the same figure throughout, never changing outfit or shape: rears up and slams both clawed fists down into the ground in front, shadow bursting up, the impact landing at the fifth frame, then drops back onto its knuckles in the starting pose by the last frame",
        death=DEATH_STAND),
    # ── MARROW WASTES — bone, sinew, carrion, skeletal / visceral scavengers
    "marrow_swarm": dict(name="MARROW TICKS", route="image", size=192,
        look="a small tick-like creature of pale bone with a bloated dark red sinew body, six spindly bone legs, a hooked bone beak, low to the ground, three-quarter side view facing left",
        idle=IDLE_CRAWL,
        attack="attack: rears up then lunges forward to the LEFT stabbing with its hooked beak, then scuttles back to the starting spot, facing left the whole time",
        death=DEATH_CRAWL),
    "marrow_caster": dict(name="CARRION SHAMAN", route="character", size=128,
        look="a tall gaunt skeletal figure draped in strips of dried sinew and tattered hide, a skull face with glowing red eye sockets, holding a long staff made of a spine topped with a rattling ribcage, floating slightly above the ground",
        idle=HOVER_IDLE,
        attack="the same figure throughout, never changing outfit or shape: raises the spine staff, the ribcage rattles and glows red, then thrusts the staff forward releasing a burst of bone shards toward the front, the release landing at the fifth frame, then settles back to the hovering start by the last frame, the staff still in hand",
        death=DEATH_HOVER),
    "marrow_armoured": dict(name="SHELL GHOUL", route="character", size=128,
        look="a hunched squat ghoul encased in a heavy carapace of fused bone plates and skulls, a closed bone helm with a narrow red slit, thick bone-plated arms held across the front, short thick legs, solid planted stance",
        idle="standing idle, feet planted and never moving: the bone plates shifting with slow ragged breathing, the red slit pulsing; returns to the start by the last frame so it loops seamlessly",
        attack="the same figure throughout, never changing outfit or shape: rams the whole bone-plated bulk forward with a shoulder charge, the impact landing at the fifth frame, then settles back to the hunched starting stance by the last frame",
        death=DEATH_STAND),
    "marrow_bruiser": dict(name="FLAYED BRUTE", route="character", size=128,
        look="a massive broad brute of exposed dark red muscle and sinew with bone spurs breaking through the shoulders, enormous arms, a small skull head, a huge club of bone in one hand, feet planted wide",
        idle=STAND_IDLE,
        attack="the same figure throughout, never changing outfit or shape: heaves the bone club up over the head and slams it down into the ground in front, the impact landing at the fifth frame, then straightens back to the starting pose by the last frame, the club still in hand",
        death=DEATH_STAND),
    # ── STILL ARCHIVE — crystal, runes, preserved thought, geometric / arcane constructs
    "archive_swarm": dict(name="GLYPH MOTES", route="image", size=192,
        look="a small cluster of floating cyan crystal shards arranged around a glowing rune, geometric and sharp, no legs, hovering, three-quarter side view facing left",
        idle=IDLE_HOVER_SMALL,
        attack="attack: the shards draw in tight then dart forward to the LEFT as a spinning volley, then drift back to the starting spot, facing left the whole time",
        death=DEATH_HOVER),
    "archive_caster": dict(name="LENS ARCHIVIST", route="character", size=128,
        look="a tall thin robed construct of pale stone and cyan crystal, a smooth featureless head ringed by three floating crystal lenses, long geometric sleeves, glowing cyan runes down the robe, floating just above the ground",
        idle=HOVER_IDLE,
        attack="the same figure throughout, never changing outfit or shape: the three lenses swing forward and align, gathering cyan light, then fire a focused beam toward the front, the beam landing at the fifth frame, then the lenses drift back to the hovering start by the last frame",
        death=DEATH_HOVER),
    "archive_armoured": dict(name="RUNE SENTRY", route="character", size=128,
        look="a squat wide construct of interlocking geometric crystal plates, a blocky angular helm with a glowing cyan rune for a face, thick faceted crystal arms held across the front like a shield, short heavy legs, solid planted stance",
        idle="standing idle, feet planted and never moving: the rune face pulsing, the crystal plates shifting slightly with a slow hum; returns to the start by the last frame so it loops seamlessly",
        attack="the same figure throughout, never changing outfit or shape: the faceted arms swing apart and slam together forward in a crushing crystal clap, the impact landing at the fifth frame, then return to the crossed starting stance by the last frame",
        death=DEATH_STAND),
    "archive_bruiser": dict(name="TABLET GOLEM", route="character", size=128,
        look="a huge broad golem built of stacked stone tablets and slabs carved with glowing cyan runes, enormous slab fists, a small block head with a single rune eye, vast square shoulders, feet planted wide",
        idle=STAND_IDLE,
        attack="the same figure throughout, never changing outfit or shape: heaves one enormous slab fist up and slams it down into the ground in front, the impact landing at the fifth frame, then hauls it back up to the starting stance by the last frame",
        death=DEATH_STAND),
    # ── PALE CHOIR — pale spirit, light, resonance, choral / spectral beings
    "choir_swarm": dict(name="CHOIR WISPS", route="image", size=192,
        look="a small pale spirit of soft white light with a tiny hooded shape and a trailing tail of glow, faint feathered edges, no legs, hovering, three-quarter side view facing left",
        idle=IDLE_HOVER_SMALL,
        attack="attack: swells bright then surges forward to the LEFT in a ripple of light, then drifts back to the starting spot, facing left the whole time",
        death=DEATH_HOVER),
    "choir_caster": dict(name="PALE CANTOR", route="character", size=128,
        look="a tall slender spectral figure in flowing pale robes of light, a smooth glowing faceless head with a thin halo ring, long open sleeves, both hands raised as if singing, floating just above the ground",
        idle=HOVER_IDLE,
        attack="the same figure throughout, never changing outfit or shape: the raised hands sweep forward together and the halo flares, releasing a ring of pale light toward the front, the release landing at the fifth frame, then the hands lift back to the singing start by the last frame",
        death=DEATH_HOVER),
    "choir_armoured": dict(name="HYMN WARDEN", route="character", size=128,
        look="a squat broad figure in closed pale spectral plate armour with feathered edges of light, a smooth closed helm with a thin glowing seam, a tall pale tower shield of light held in front, solid planted stance",
        idle="standing idle, feet planted and never moving: the feathered edges drifting slowly, the shield glow pulsing, the shoulders rising and falling; returns to the start by the last frame so it loops seamlessly",
        attack="the same figure throughout, never changing outfit or shape: rams the tower shield of light forward, the pale face leading, the impact landing at the fifth frame, then settles back to the starting stance by the last frame, the shield still held in front",
        death=DEATH_STAND),
    "choir_bruiser": dict(name="BELL GIANT", route="character", size=128,
        look="a huge broad pale giant of spectral light with a great bronze bell for a head, enormous arms ending in heavy bell-clapper fists, wide shoulders draped in pale feathered cloth, feet planted wide",
        idle=STAND_IDLE,
        attack="the same figure throughout, never changing outfit or shape: swings one heavy clapper fist back and hammers it forward, the bell head ringing with a burst of light, the impact landing at the fifth frame, then returns to the starting stance by the last frame",
        death=DEATH_STAND),
}


def build() -> dict:
    items = {}
    for key, cell in CELLS.items():
        slug, arch = key.split("_", 1)
        region_id, source = R[slug]
        items[key] = {
            "region": region_id, "archetype": arch.capitalize(), "source": source,
            "name": cell["name"], "route": cell["route"], "size": cell["size"],
            "look": cell["look"], "idle": cell["idle"], "attack": cell["attack"], "death": cell["death"],
        }
    return {
        "$comment": [
            "The 2026-09-16 region x archetype matrix: 6 regions x 4 archetypes = 24 normal-enemy identities.",
            "PRESENTATION ONLY — gameplay Source, HP, damage and archetype rolls stay in Core. The key is",
            "<region slug>_<archetype>; clips idle/attack/death; south-west rotation (faces LEFT); 8 x 512 frames.",
            "Archetype reads from the SILHOUETTE: swarm small/busy/low, caster tall/thin/hovering, armoured",
            "closed/plated/heavy centre, bruiser broad/dominant. Scale (ArchetypeScale) is supporting language only.",
            "`done` records the PixelLab ids per key: character route {character_id, idle, attack, death},",
            "image route {image_job, idle_job, attack_job, death_job}. The six 2026-08-22 Source bodies stay in",
            "`enemies` above as history; the matrix supersedes them for the arena.",
        ],
        "dest": "enemies", "key": "{id}_{clip}_strip8_512", "clips": ["idle", "attack", "death"], "rotation": "south-west",
        "regions": {slug: {"id": rid, "source": src} for slug, (rid, src) in R.items()},
        "done": {},
        "items": items,
    }


def main() -> int:
    text = open(SPEC, encoding="utf-8").read()
    if '"enemy_matrix"' in text:
        print("spec.json already carries enemy_matrix")
        return 0
    body = json.dumps({"enemy_matrix": build()}, indent=2, ensure_ascii=False)
    # drop the outer braces and re-indent by two so it sits beside the other sections
    inner = "\n".join(line for line in body.split("\n")[1:-1])
    end = text.rstrip().rfind("}")
    new = text[:end].rstrip() + ",\n" + inner + "\n}\n"
    json.loads(new)   # must still parse
    open(SPEC, "w", encoding="utf-8", newline="\n").write(new)
    print(f"spliced enemy_matrix with {len(CELLS)} cells into {os.path.relpath(SPEC)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
