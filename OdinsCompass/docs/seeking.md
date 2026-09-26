# Finding and choosing the target - built

The target groups per tier, which lookup each takes, how the server is asked, and the cycle key.
The plan is [`../ROADMAP.md`](../ROADMAP.md).

## Finding the target

Built as planned in `src/Seeker.cs`, with these adjustments: a prefab is a location when
`ZoneSystem.m_locationsByHash` has its hash, a world object when `ZNetScene` has the prefab,
and anything else is warned about once. Silence is three rounds (`SilentRounds`), and a vein
group that finds nothing says "none nearby" instead. The object search walks everything the
client has received this session, so a vein passed an hour ago is still found - the compass
remembers what the player has seen, which is short range in a looser sense than planned.

**What a target is:** a *group* - a name plus one or more prefab names - from the tier's
`Targets` config (`name=prefab,prefab|name=...`), parsed once by `TargetGroup` in
`src/Targets.cs`. A compass of tier N offers the groups of tiers 1..N. The defaults, with what
the dump confirmed and what is still open (`compass locations` lists every location name of the
running build with its biome, `compass prefabs copper` the world object prefabs):

| Tier | Group | Prefabs | Status |
| --- | --- | --- | --- |
| 1 | Eikthyr | `Eikthyrnir` | confirmed |
| 1 | Haldor | `Vendor_BlackForest` | confirmed, unique |
| 1 | Hildir | `Hildir_camp` | confirmed, unique |
| 2 | The Elder | `GDKing` | confirmed |
| 2 | Burial chamber | `Crypt2`, `Crypt3`, `Crypt4` | confirmed (`Hildir_crypt` is Hildir's, `HalfBurried_ForestCrypt` disabled) |
| 2 | Troll cave | `TrollCave02` | confirmed (`TrollCave` disabled) |
| 2 | Copper | `rock4_copper` | confirmed as a prefab (`MineRock_Copper` also exists - which one the world places, **verify** by walking up to a vein; the config can list both) - vein, short range |
| 2 | Tin | `MineRock_Tin` | confirmed 2026-09-24 (`Pickable_Tin` is the pickable) - vein, short range |
| 3 | Bonemass | `Bonemass` | confirmed |
| 3 | Sunken crypt | `SunkenCrypt4` | confirmed (1-3 disabled) |
| 3 | Bog witch | `BogWitch_Camp` | confirmed, unique |
| 4 | Moder | `Dragonqueen` | confirmed |
| 4 | Drake nest | `DrakeNest01` | confirmed |
| 4 | Frost cave | `MountainCave02` | confirmed (`MountainCave01` disabled) |
| 4 | Silver | `silvervein` | confirmed 2026-09-24 (`rock3_silver` also exists - **verify** which one the mountains place; the config can list both) - vein, short range |
| 5 | Yagluth | `GoblinKing` | confirmed |
| 5 | Fuling village | `GoblinCamp2`, `GoblinCamp2_1` | confirmed (`GoblinCamp1` disabled; the `GoblinHut` ones are single huts) |
| 5 | Fuling totem | `GoblinTotem` | confirmed 2026-09-24 (`goblin_totempole` is the village pole) - a world object inside villages, short range |
| 5 | Tar pit | `TarPit1`, `TarPit2`, `TarPit3` and their `_1` variants | confirmed |
| 6 | The Queen | `Mistlands_DvergrBossEntrance1` | confirmed |
| 6 | Infested mine | `Mistlands_DvergrTownEntrance1`, `..2` | confirmed |
| 6 | Guard tower | `Mistlands_GuardTower1_new`, `2_new`, `3_new` | confirmed; the `_ruined` variants are left out on purpose |
| 6 | Extractor | `Mistlands_Excavation1`, `2`, `3` | confirmed |
| 7 | Fader | `FaderLocation` | confirmed |
| 7 | Charred fortress | `CharredFortress` | confirmed |
| 7 | Putrid hole | `MorgenHole1`, `2`, `3` | confirmed |
| 8 | Boss of the Deep North | `DN_Bossroom` | confirmed as a name, x3 like every boss; what it is in play is unknown here |
| 8 | The hole | `TheHole01` | a Deep North location x40 - probably the winding tunnels' entrance (`TheDarkestHole` is a disabled unique), **verify** in play |
| 8 | Mork Borg | `MorkBorg` | x40; the `Morkhalla` environment (interior dust) suggests the dungeon behind it, **verify** |
| 8 | Village | `NorthVillage` | x135 |
| 8 | Memorial | `NorthMemorialPlace` | x15 |
| 8 | Lumber camp | `LumberCamp` | x50 |

The Deep North also has `FrozenShip01..03_DN`, `IcePond1`, `DN_hut01`, `ShipSetting02/03`,
`ShipWreck01/02_DN`, `DN_gammeltrollFrac01/02` and disabled `HotSpring1..3` - not offered, since
nobody here has played the biome yet and a wreck is not a destination.

**Which lookup:** a prefab whose name is in `ZoneSystem.instance.m_locations` is a location and
goes to the server; anything else is searched in the client's ZDOs. Decided once per group when
the config is parsed, after `ZoneSystem.Start` has run `SetupLocations` (which it does on clients
too - **verify**). A name that is neither is logged once and ignored.

**The ask:** while a compass is worn, the status effect sends one `RPC_DiscoverClosestLocation`
per location prefab in the chosen group (a burial chamber is three asks) straight over
`ZRoutedRpc` - `Game.DiscoverClosestLocation` is the same call behind a "DiscoverClosestLocation"
log line per ask, which filled the log at the first cadence (2026-09-24) - with
`pinName = "OdinsCompass:" + requestId`, `showMap = false`, `discoverAll = false`. The
prefix on `RPC_DiscoverLocationResponse` matches the prefix, records `pos` for the request, and
the nearest answer wins once all are in or the next ask starts. Asks are rare: a found target is
kept until the player has walked a quarter of the way toward it (20 to 200 m), since locations do
not move and the nearest one only changes as the player does; an unanswered ask repeats every
`SeekInterval` (3 s) until silence has counted, then every 30 s. The veins in the group are
scanned locally on the same rule, plus a rescan after 20 m of walking while none is found, since
walking loads new ground. The server's cost is a linear scan of a few thousand
`LocationInstance`s per ask - nothing. A compass on a server without the mod is exactly as good
as one on a server with it; nothing here needs the server.

**Silence is an answer:** no response within two intervals means the target does not exist in
this world (a vanilla server logs "Failed to find location" and sends nothing). The compass goes
quiet and the status icon says so.

## Choosing what to seek

Built as planned in `SE_Compass`. `C` turned out to toggle walking, so the key is `N`
(`ZInput`'s defaults bind A C D E F G M Q R S T V W X and no other letter), read through
`ZInput.GetKeyDown` because the game runs on the new input system, and only while
`Player.TakeInput` says the player is not in a menu. Swapping one compass for another keeps the
effect, so the choice is checked against the worn tier every tick.

**The player:** press `CycleTargetKey` (`N`; planned as C, which turned out
to toggle walking) with a compass worn and the compass moves to the next group its tier offers, announced
top left: *Compass: Burial chamber*. The choice is kept per character in
`Player.m_customData["OdinsCompass.Target"]` (a vanilla string dictionary saved with the
character), so it survives a relog and a worn compass starts on what it last sought. A tier that
no longer offers the saved group (a downgrade: dropping a tier 4 compass for a tier 2) falls back
to the tier's first group.

Also: the status effect's icon text (`GetIconText`) shows the group's name, and the distance if
`ShowDistance` is on - off by default, a direction only is the wishbone's feel.
