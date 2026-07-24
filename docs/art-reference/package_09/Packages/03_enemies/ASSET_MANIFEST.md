# ASSET_MANIFEST

Total runtime assets: **245**

| Enemy | Tier | Suggested Source | Suggested Role | Actions | Normalized Canvas |
|---|---|---|---|---|---|
| `wisp` | `common_1` | `spirit` | `support` | idle × 2, move × 2, attack × 2, die × 2 | `512x512` |
| `shadeling` | `common_1` | `shadow` | `attacker` | idle × 2, move × 2, attack × 2, die × 2 | `512x512` |
| `bonecrawler` | `common_1` | `body` | `attacker` | idle × 2, move × 2, attack × 2, die × 2 | `512x512` |
| `void_spitter` | `common_1` | `shadow` | `producer` | idle × 2, move × 2, attack × 2, projectile × 2 | `512x512` |
| `gloombat` | `common_1` | `shadow` | `support` | idle × 2, move × 2, attack × 2, die × 2 | `512x512` |
| `nightstalker` | `common_2` | `shadow` | `attacker` | idle × 2, move × 2, attack × 2, die × 2 | `512x512` |
| `rift_hound` | `common_2` | `body` | `attacker` | idle × 2, move × 2, attack × 2, die × 2 | `512x512` |
| `stone_sentinel` | `common_2` | `machine` | `defender` | idle × 2, attack × 2, slam × 2, die × 2 | `512x512` |
| `soul_leech` | `common_2` | `mind` | `support` | idle × 2, attack × 2, drain × 2, die × 2 | `512x512` |
| `void_mage` | `common_2` | `mind` | `crafter` | idle × 2, attack × 2, cast × 2, die × 2 | `512x512` |
| `rift_guardian` | `elite` | `spirit` | `defender` | idle × 1, attack_1 × 1, attack_2 × 1, die × 1 | `512x512` |
| `corrupted_brute` | `elite` | `body` | `defender` | idle × 1, attack_1 × 1, attack_2 × 1, die × 1 | `512x512` |
| `baneling_wraith` | `elite` | `spirit` | `attacker` | idle × 1, attack_1 × 1, attack_2 × 1, die × 1 | `512x512` |
| `oblivion_stalker` | `elite` | `shadow` | `attacker` | idle × 1, attack × 1, dash × 1, die × 1 | `512x512` |
| `rift_harvester` | `elite` | `mind` | `producer` | idle × 1, attack × 1, channel × 1, die × 1 | `512x512` |

## Folder pattern

```text
assets/enemies/<enemy>/native/<enemy>_<action>_<frame>.png
assets/enemies/<enemy>/normalized/<enemy>_<action>_<frame>.png
assets/portraits/<enemy>_portrait.png
assets/silhouettes/<enemy>_silhouette.png
assets/shadows/<enemy>_ground_shadow.png
```