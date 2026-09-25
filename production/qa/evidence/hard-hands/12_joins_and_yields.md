# Joins and yields (tools/asset-pipeline/action_joins.py; handoff/yield lines of RH_PRESENT_TRACE)

A JOIN is where one clip hands the figure to the next. The numbers are how far the drawn silhouette's feet line, head and centre move between the last frame before the join and the first after it, through the renderer's own placement arithmetic. The feet should not move; the centre moves as much as the two poses differ.

## NEW normal

```
worst join per pair (feet, head, centre x px):
  attack->idle             feet  +6.7  head   -0.0  centre  -15.2   at 7300
  idle->attack             feet -13.2  head   +3.3  centre  +30.1   at 3583
  idle->projectile         feet  +0.0  head   -9.7  centre   +5.9   at 4617
  idle->strike             feet  -0.2  head   +3.0  centre  +10.9   at 7833
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 5533
  strike->idle             feet  +0.2  head   +0.2  centre   +0.4   at 8600
```

## NEW tempo_first

```
worst join per pair (feet, head, centre x px):
  attack->projectile       feet  +6.7  head   -6.5  centre  -12.3   at 5250
  attack->strike           feet  +6.4  head   -0.3  centre   -6.1   at 3933
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 4833
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 6133
  strike->idle             feet  +0.2  head   +0.2  centre   +0.4   at 4700
```

## NEW tempo_hh

```
worst join per pair (feet, head, centre x px):
  attack->projectile       feet  +6.7  head   -6.5  centre  -12.3   at 5250
  attack->strike           feet  +6.4  head   -0.3  centre  -25.1   at 8333
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 4833
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 6133
  strike->idle             feet  +0.2  head   +0.2  centre   +0.4   at 4700
  strike->projectile       feet  +5.2  head   -1.2  centre -105.7   at 8950
```

## NEW tempo_long

```
worst join per pair (feet, head, centre x px):
  attack->idle             feet +13.2  head  -52.3  centre  +90.8   at 12300
  attack->projectile       feet  +6.7  head   -6.5  centre  -13.3   at 11850
  attack->strike           feet  +6.4  head   -0.3  centre  -25.1   at 8333
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 4833
  idle->strike             feet  -0.2  head   -3.5  centre   +9.3   at 333
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 6133
  strike->idle             feet  +0.2  head   +0.2  centre   +0.4   at 4700
  strike->projectile       feet  +5.2  head   -1.2  centre -122.7   at 950
```

## NEW fastest_long

```
worst join per pair (feet, head, centre x px):
  attack->idle             feet +13.2  head  -52.3  centre  +60.6   at 7517
  attack->projectile       feet  +6.7  head   -6.5  centre  -32.3   at 6817
  attack->strike           feet  +6.4  head   -0.3  centre  -13.1   at 4917
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 3850
  idle->projectile         feet  +0.0  head   +3.2  centre  -13.6   at 3483
  idle->strike             feet  -0.2  head   -3.5  centre   +6.1   at 133
  projectile->attack       feet  -0.1  head   -6.5  centre  -31.3   at 1250
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 3833
  projectile->strike       feet  -0.2  head   -0.2  centre   +6.7   at 7600
  strike->attack           feet  +5.1  head   -1.2  centre -160.5   at 13950
  strike->idle             feet  +0.2  head   +0.2  centre   +0.4   at 8300
  strike->projectile       feet  +2.5  head  -55.9  centre   +5.3   at 533
```

## NEW fastest_hh

```
worst join per pair (feet, head, centre x px):
  attack->strike           feet  +6.4  head   -0.3  centre  -13.1   at 4917
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 3850
  idle->projectile         feet  +0.0  head   +3.2  centre  -13.6   at 3483
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 3833
  strike->projectile       feet  +2.5  head  -55.9  centre   -4.7   at 5233
```

## OLD normal

```
worst join per pair (feet, head, centre x px):
  attack->idle             feet  +6.7  head   -0.0  centre  -15.2   at 7300
  idle->attack             feet  -0.1  head   -9.7  centre  -28.7   at 3450
  idle->projectile         feet  +0.0  head   -9.7  centre   +5.9   at 4617
  idle->strike             feet  -0.9  head   -4.3  centre   +6.6   at 7417
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 5533
  strike->idle             feet  +0.9  head  -31.1  centre  +11.1   at 8833
```

## OLD tempo_first

```
worst join per pair (feet, head, centre x px):
  attack->projectile       feet  +6.7  head   -6.5  centre  -12.3   at 5250
  attack->strike           feet  +5.8  head   -4.3  centre   -5.6   at 3867
  idle->attack             feet  -0.1  head   -9.7  centre  -31.9   at 4833
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 6133
  strike->idle             feet  +0.9  head  -31.1  centre  +11.1   at 4667
```

## OLD tempo_hh

```
worst join per pair (feet, head, centre x px):
  attack->projectile       feet  +6.7  head   -6.5  centre  -12.3   at 5250
  attack->strike           feet  +5.8  head   -4.3  centre  -24.6   at 8267
  idle->attack             feet -13.2  head   +3.3  centre  +30.1   at 3533
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 6133
  strike->idle             feet  +0.9  head  -31.1  centre  +11.1   at 4667
  strike->projectile       feet  +0.9  head  -37.6  centre  +17.0   at 8917
```

## OLD tempo_long

```
worst join per pair (feet, head, centre x px):
  attack->idle             feet +13.2  head  -52.3  centre  +89.2   at 14000
  attack->projectile       feet  +6.7  head   -6.5  centre  -13.3   at 11850
  attack->strike           feet  +5.8  head   -4.3  centre  -24.6   at 8267
  idle->attack             feet  -0.1  head   -9.7  centre  -31.9   at 4833
  idle->projectile         feet  +0.0  head   -9.7  centre   +9.1   at 100
  idle->strike             feet  -0.9  head   -4.3  centre   +6.6   at 12767
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 6133
  strike->idle             feet  +0.9  head  -31.1  centre  +11.1   at 4667
  strike->projectile       feet  +0.9  head  -37.6  centre  +17.0   at 8917
```

## OLD fastest_long

```
worst join per pair (feet, head, centre x px):
  attack->idle             feet +13.2  head  -52.3  centre  +59.0   at 8417
  attack->projectile       feet  +6.7  head   -6.5  centre  -32.3   at 5117
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 3850
  idle->projectile         feet  +0.0  head   +3.2  centre  -13.6   at 3450
  projectile->attack       feet  -0.1  head   -6.5  centre  -31.3   at 2950
  projectile->idle         feet  +0.0  head   -9.7  centre  +12.0   at 7817
  projectile->strike       feet  -0.9  head   -4.3  centre   +7.2   at 7650
  strike->idle             feet  +0.9  head  -31.1  centre  +11.1   at 8133
```

## OLD fastest_hh

```
worst join per pair (feet, head, centre x px):
  attack->projectile       feet  +6.7  head   -6.5  centre  -32.3   at 5117
  idle->attack             feet  -0.1  head   -6.5  centre  -31.9   at 3850
  idle->projectile         feet  +0.0  head   +3.2  centre  -13.6   at 3467
  projectile->idle         feet  +0.0  head   +0.0  centre   +0.6   at 3833
```

# The fastest build's handoffs (TEMPO trained to 60, 33 s)

## NEW

clips started: {'projectile': 18, 'attack': 30, 'strike': 11}

| outgoing | fit | next | count |
|---|---|---|---:|
| attack | Natural | attack | 13 |
| attack | Natural | projectile | 11 |
| attack | Natural | strike | 5 |
| projectile | Compressed | attack | 2 |
| projectile | Floor | strike | 5 |
| strike | Compressed | attack | 3 |
| strike | Squeezed | attack | 1 |
| strike | Yielded | projectile | 6 |

| not animated (yield) | for | count |
|---|---|---:|
| attack | projectile | 11 |

## OLD

clips started: {'projectile': 18, 'attack': 30, 'strike': 5}

| outgoing | fit | next | count |
|---|---|---|---:|
| attack | Natural | attack | 13 |
| attack | Natural | projectile | 16 |
| projectile | Compressed | attack | 3 |
| projectile | Compressed | strike | 6 |

| not animated (yield) | for | count |
|---|---|---:|
| attack | projectile | 11 |
| strike | projectile | 6 |
