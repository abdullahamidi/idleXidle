# Capture views: the presentation-QA switches

The capture rig (`tools/capture_seq.sh`, `RH_SHOT=...`) films the game as it is. These switches let a film show ONE
layer of the fight's language at a time, so a review can ask "does the body read alone?", "does the force path read
alone?", "is the flash hiding the hit?". They are read once from the environment in
`src/IdleXIdle.Game/Presentation/CaptureViews.cs` (and `AssetLibrary.LoadFixtureOverrides`), the game never sets
them, and every one is inert without its variable. They change no fight, no Core event and no timing.

| Variable | Effect |
|---|---|
| `RH_SHOT_NOVFX=1` | no effects, no performed material or light, no reaction layer: does the animation read alone? |
| `RH_SHOT_NOCHAMP=1` | the champion is not drawn: does the force path read alone? |
| `RH_SHOT_SOCKETS=1` | the hand sockets drawn as crosshairs |
| `RH_SHOT_NOTINT=1` | no ember wind-up tint on the creatures |
| `RH_SHOT_SIL=1` | every creature drawn as a flat grey silhouette: does the SHAPE perform the action? |
| `RH_SHOT_NOROOT=1` | the pack's presentation lunge off (the creatures stay in their row; the strip still plays) |
| `RH_SHOT_NORECOIL=1` | the champion's hit recoil off (the bite motif at his edge is not the recoil's and stays; `RH_SHOT_NOVFX` hides it) |
| `RH_SHOT_FLASH=off` | the hit flash off for every hit |
| `RH_SHOT_FLASH=peak,rise,ms` | the hit flash overridden for every hit, a recipe's included, e.g. `1,0.2,200` is the old full-white flash and `0.45,0.15,80` the usual one |
| `RH_SHOT_STRIP_FILES=key=path;...` | textures loaded from outside the asset tree under a key: a stand-in body (film a light creature in a dark region's fight), a prototype strip |

They compose: `RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1` is the enemy-attack acceptance view (the attack must read with
everything but the body off); `RH_SHOT_FLASH=1,0.2,200` beside the default is the flash comparison.

The rule for adding one: it must be GENERIC (a layer on or off, a dial overridden), never "show study variant X".
A one-off study knob belongs to the prototype that needs it and is removed when the study is filed.

The fight's other rig variables (`RH_SHOT_SEED`, `RH_SHOT_T`, `RH_SHOT_SEQ`, `RH_SHOT_TAKE`, `RH_SHOT_SWAP`,
`RH_SHOT_BITE=1` + `RH_SHOT_LEAD` for a posed seek before a bite, `RH_REACTION_RECIPES`, `RH_PRESENT_TRACE`) are the
host's and are documented at their call sites in `Game1.cs`.
