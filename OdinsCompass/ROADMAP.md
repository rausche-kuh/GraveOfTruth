# Roadmap

Odin's Compass is a craftable compass, worn like the wishbone, that points the way to the nearest
boss - and, upgraded through the biomes, to the dungeons and ore of each one. Sections 1 to 5
are built and make up 0.1.0 (packaged 2026-09-24, not yet uploaded); section 6 is what is still
to check in play, section 7 what comes next, section 8 what may come later. The design behind
each section, with the game facts it rests on, is in `docs/`.

Game facts marked **verify** were written from memory of the game or from the asset manifest,
which lists items and effects but not the location prefabs. Check each one before writing the
code that rests on it; the `compass` dev command (`src/Dev/CompassCommands.cs`, Debug builds
only, `deploy -c Debug`) exists for exactly that. What the check teaches goes into the doc it
concerns, and a name that turns out wrong goes into the config default in `src/OdinsCompass.cs`.
The location dump of 2026-09-24 (a local world, 232 locations) confirmed every location name;
what is still marked is what that dump could not show.

| Section | Doc |
| --- | --- |
| 0. The two facts the design rests on | [`docs/foundations.md`](docs/foundations.md) |
| 1. The eight compass items, 5. Words | [`docs/items.md`](docs/items.md) |
| 2. Finding the target, 3. Choosing it | [`docs/seeking.md`](docs/seeking.md) |
| 4. The guidance effect | [`docs/guidance.md`](docs/guidance.md) |
| 7. The target menu | [`docs/radial-menu.md`](docs/radial-menu.md) |
| Every source file, every rule with its reasons | [`docs/architecture.md`](docs/architecture.md) |

## 0. The two facts the design rests on

Finding is the game's own vegvisir request (`RPC_DiscoverClosestLocation`), which every server
answers, so the mod is client side and catches its own answers by pin name. Ore veins are
vegetation ZDOs, not locations, so a client finds only veins in what it has loaded.
[`docs/foundations.md`](docs/foundations.md)

## 1. The compass: eight items - built

Eight prefabs `OdinsCompass1`..`8` cloned from `Wishbone`, one per biome, each with a plain
`Recipe` from the config that consumes the tier below; a quality upgrade cannot vary materials
per level. [`docs/items.md`](docs/items.md)

## 2. Finding the target - built

A tier offers the target groups of its tier and below; a location is asked of the server, rarely
(a found one is kept for a quarter of the way), a world object is scanned locally; three silent
rounds mean "not in this world". The table of every group and its status is in
[`docs/seeking.md`](docs/seeking.md).

## 3. Choosing what to seek - built

`N` cycles the groups, the choice is saved in `Player.m_customData`. [`docs/seeking.md`](docs/seeking.md)

## 4. The guidance effect - built

Two particle systems built in code: a few wavy blue lines flying low from the player to the
target, stopping within `ArrivalDistance`, no sound. [`docs/guidance.md`](docs/guidance.md)

## 5. Words - built

`assets/translations.csv`, `$oc_` tokens. [`docs/items.md`](docs/items.md#words---built)

## 6. Verify list, in the order it is needed

1. ~~`compass locations`~~ done 2026-09-24: the table in `docs/seeking.md` is confirmed, the Deep North filled.
2. ~~`compass prefabs tin|silver|totem`~~ done 2026-09-24: `MineRock_Tin`, `silvervein`,
   `GoblinTotem` all exist. Still open: whether the world places `rock4_copper` or
   `MineRock_Copper`, and `silvervein` or `rock3_silver` - walk up to a vein with the compass set
   to it; if it stays "none nearby", the other name is the one, and both can go in the config.
3. ~~`compass find Eikthyrnir` on a local world~~ done: the pin appeared. Still open: the same on
   a dedicated server *without* the mod installed there.
4. ~~`compass psystems`~~ done: no wind streak system exists, the effect is built in code.
5. Cloning `Wishbone`: ~~no "copied it by hand" warning, the compass crafts, wearing it shows
   the status icon with the first target~~ done 2026-09-24. Still open: a second client without
   the mod does not crash on a dropped compass. The main menu's database had no wishbone; the
   world's has it (1530 items), and the console does not offer `compass` in the menu, so the
   menu copy is left as a log note and not chased further.
6. ~~`C` is unbound~~ it toggles walking; the key is `N` now.
7. The seeking in play: ~~the wind lines point the right way and turn as the player moves,
   they are two or three at a time, start near the player and wave (the first two versions
   were too much, too close, then too far out and stiff; then they zigzagged, then they were
   too many), they climb a hill in the way instead of going into it, they run into the
   target from fifteen metres out and stop within ten, the log is not filled with asks~~
   seen 2026-09-24. Still open: the wind comes back on walking away from the target, the
   icon text says the target and the distance with `ShowDistance` on, `N` cycles and the
   choice survives a relog, "not in this world" appears for a target the world lacks (set a
   Deep North target on an old world, or a location whose name is misspelt in the config), a
   vein group finds a vein passed earlier.
8. The same on a dedicated server without the mod installed there.

## 7. Next

- **Choosing the target in a menu.** `N` cycles blind through a tier's groups; the key should
  open the game's radial menu with every group the worn tier offers. The design and what to
  verify: [`docs/radial-menu.md`](docs/radial-menu.md).
- **Icons.** `assets/icons/compass.png` and `package/icon.png` are generated placeholders, and
  every tier shows the same one. Wanted: one icon per tier that reads at inventory size (64
  px) and says which biome the tier is - the same compass with the biome's colour in the
  face, or its boss trophy's silhouette on the dial - and a matching 256 px `package/icon.png`.
  The group icons the menu above needs can come from the game (the boss trophies, the
  dungeons' entrance sprites where there are any, the ores) - `ObjectDB.GetItemPrefab
  (...).m_itemData.m_shared.m_icons[0]` for trophies and ores, **verify** what a location
  can offer (probably nothing; a drawn set of eight or so small glyphs is the alternative).

## 8. Later

- **World wide ore.** A routed RPC of the mod's own (`OdinsCompass_FindPrefab`), answered only
  by a server that has the mod, scanning its `ZDOMan` for the nearest ZDO of a prefab - every
  generated zone, so every vein anyone has walked near. Ungenerated zones have no ZDOs, so even
  this is not the whole world; predicting vegetation placement from the seed is out of scope.
  With the mod absent on the server the compass falls back to the local scan, so it stays
  optional there.
- **A compass of its own:** a mesh and an icon instead of the wishbone's, which means an asset
  bundle and a Unity project.
- **Gate the upgrades on the world** as an option: tier N craftable only once
  `defeated_<previous boss>` is a global key (`defeated_eikthyr`, `defeated_gdking`,
  `defeated_bonemass`, `defeated_dragon`, `defeated_goblinking` are in the `GlobalKeys` enum;
  the Queen's, Fader's and the Deep North boss's are string keys set by the boss prefab -
  **verify** their spelling with `ZoneSystem.GetGlobalKeys` after a kill). Recipes have no key
  requirement in vanilla, so this is a prefix on `Player.HaveRequirements` for the mod's recipes.
- **Ping the map** as an option: a compass that, on a key, drops a pin where the target is - the
  vegvisir's behaviour, for players who want the map after all.
- **Tier 8** once the Deep North's names and materials are known.

# Bugs

None known. Played on a local world only; the dedicated server test (section 6, item 8) is
open.
