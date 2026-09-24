# Roadmap

Odin's Compass is a craftable compass, worn like the wishbone, that points the way to the nearest
boss - and, upgraded through the biomes, to the dungeons and ore of each one. Sections 1 to 5
are built and make up 0.1.0 (packaged 2026-09-24, not yet uploaded); section 6 is what is still
to check in play, section 7 what comes next, section 8 what may come later.

Game facts marked **verify** were written from memory of the game or from the asset manifest,
which lists items and effects but not the location prefabs. Check each one before writing the
code that rests on it; the `compass` dev command (`src/Dev/CompassCommands.cs`, Debug builds
only, `deploy -c Debug`) exists for exactly that. What the check teaches goes into `CLAUDE.md`,
and a name that turns out wrong goes into the config default in `src/OdinsCompass.cs`. The
location dump of 2026-09-24 (a local world, 232 locations) confirmed every location name below;
what is still marked is what that dump could not show.

## 0. The two facts the design rests on

**Finding is the game's own, and it works on any server.** A vegvisir stone calls
`Game.DiscoverClosestLocation(name, point, pinName, pinType, showMap, discoverAll)`, which sends
the routed RPC `RPC_DiscoverClosestLocation` to the server. The server (every server, modded or
not - the handler is vanilla `Game` code, registered when `ZNet.IsServer()`) runs
`ZoneSystem.FindClosestLocation` over `m_locationInstances` and answers the asking peer with
`RPC_DiscoverLocationResponse(pinName, pinType, pos, showMap)`, whose client side handler pins
the map and turns the player toward it. Only the server has `m_locationInstances`; a client holds
the location *definitions* (`ZoneSystem.m_locations`, so it can validate names) and the map
icons the server sent (`m_locationIcons`: boss stones and the trader only, not enough on its own).
Location instances are generated when the world is created, `m_placed` or not, so an unvisited
boss altar is found as readily as a visited one. **The mod is therefore client side:** it asks
with a `pinName` of its own (`OdinsCompass:<request id>`), and a prefix on
`Game.RPC_DiscoverLocationResponse` catches any answer whose pin name starts with that, stores
the position and returns false - no pin, no forced look. Every other answer (a real vegvisir)
passes through untouched. The `find` console command does not help here: it walks the location
list and every loaded `GameObject` locally, which is why it is cheat only and server only.

**Ore veins are not locations.** Copper, tin, silver and the rest are vegetation, spawned per zone
by `ZoneSystem` when the zone is first generated, so they exist only as ZDOs of generated zones
and nobody can ask the server for "the nearest copper" - vanilla has no such RPC. A client holds
the ZDOs of the zones around it, so `ZDOMan.instance.GetAllZDOsWithPrefabIterative(name, list,
ref index)` on the client finds veins within the loaded area, a couple of hundred metres. That
is what the wishbone does for silver (`Beacon`s on loaded objects, `m_range` 20-50 m), only
farther, so ore is the compass's short range find and the mod page says so. A world wide vein
search needs the mod on the server (section 7).

## 1. The compass: eight items, not one item with eight qualities - built

**The player:** a *Compass* is crafted at a workbench from 1 deer trophy and 10 stone. It goes in
the utility slot - the wishbone's, the wisplight's, the belt's - so it competes with those, which
is part of the point: you wear the compass *instead* of the wishbone. Each upgrade is a new
recipe that consumes the compass below it plus the tier's materials, so the crafting tab shows
*Compass (Black Forest)* needing *Compass (Meadows)* + 2 bronze + 2 surtling cores. The tiers,
one per biome:

| Tier | Biome | Default recipe (on top of the tier below) | Why these |
| --- | --- | --- | --- |
| 1 | Meadows | TrophyDeer 1, Stone 10 | the first thing a new player can make |
| 2 | Black Forest | Bronze 2, SurtlingCore 2 | the needle is bronze; cores light it |
| 3 | Swamp | Iron 2, WitheredBone 2 | |
| 4 | Mountain | Silver 2, Crystal 1 | a lens from a stone golem |
| 5 | Plains | BlackMetal 2, Needle 2 | a deathsquito's needle for a needle |
| 6 | Mistlands | Eitr 2, Wisp 1 | the wisplight's own material |
| 7 | Ashlands | FlametalNew 2, CharredCogwheel 1 | a cogwheel exists as an item (`CharredCogwheel.prefab` in the manifest) |
| 8 | Deep North | FrostCore 1 | a placeholder: the item exists (`FrostCore.prefab`), the tier's real material is unknown - **verify** |

Every item in the table exists as a prefab in the manifest (checked 2026-09-24).

Every recipe is a config string (`Tier N <Biome>` / `Recipe`, `Item:amount,...`), so a wrong
guess is fixed in the config, not the code. Boss trophies stay out of the recipes: one drops per
kill and it belongs on the sacrificial stones.

**Why separate items:** a vanilla quality upgrade cannot do this. `Piece.Requirement.GetAmount`
scales one resource list by quality (×1, ×2, ×3, then ×4, ×4.5, ...), so tier materials cannot
differ per level, and `Recipe.GetRequiredStationLevel` is `m_minStationLevel + quality - 1`, so a
quality 8 compass would need a workbench of level 8. Separate items keep every recipe a plain
`Recipe` and the crafting UI untouched; the price is eight prefabs instead of one.

**Making the items without an asset bundle and without Jotunn** (the reference mods that add
items use Jotunn's `ItemManager`; this workspace has no such dependency, so the game's own
`Wishbone` is cloned instead). This is `src/Items.cs`, and it follows the list below with two
adjustments: the clones sit under an inactive `DontDestroyOnLoad` root so their `Awake` (which
would register them as items lying in the world) never runs, and the workbench is taken from the
first vanilla recipe whose station is named `$piece_workbench`, because the main menu's database
has no `ZNetScene` to look the prefab up in. Whether `Instantiate` copies `m_shared` is checked
at runtime and a shared instance is copied by hand, with a warning:

- Postfix `ObjectDB.CopyOtherDB` (the main menu's database, which `FejdStartup` copies from the
  prefab) and `ObjectDB.Awake` (the game scene's), and run once per `ObjectDB` instance: fetch
  `ObjectDB.GetItemPrefab("Wishbone")`, `Object.Instantiate` it under an inactive, hidden
  `DontDestroyOnLoad` root, name the clone `OdinsCompass1`..`OdinsCompass8`. `Instantiate` copies
  the serialized `ItemDrop.m_itemData` by value, so the clone's `m_shared` is its own and the
  wishbone is not touched (**verify** the shared data is not the same instance).
  Seen 2026-09-24: the main menu's database answered `GetItemPrefab("Wishbone")` with null, the
  world's did not (no warning after the world loaded), so the menu copy is treated as optional
  and only noted in the log. **verify** with `compass items wish` from the menu console what the
  menu database holds - if it lacks every wearable, the compass simply cannot exist before a
  world loads, which nothing in the menu needs.
- Set on the clone's `m_shared`: `m_name` (`$oc_compass1`..), `m_description`, `m_icons[0]` (a
  sprite from `assets/icons/`, loaded like OdinsMissingPatch's `PanelButtons.Icon` through
  `ImageConversion.LoadImage` found by reflection), `m_maxQuality = 1`, `m_equipStatusEffect` =
  the compass status effect (section 4), `m_itemType` stays `Utility`.
- Add the clone to `ObjectDB.m_items` and call the private `UpdateRegisters()` (publicized), and
  add it to `ZNetScene.m_prefabs` + `m_namedPrefabs` in a postfix on `ZNetScene.Awake` (the
  `Awake` fills `m_namedPrefabs` from `m_prefabs`, so a prefab added before `Awake` runs is enough
  there, added after needs both). A dropped compass is a ZDO with that prefab hash; a player
  without the mod who sees one dropped gets an unknown prefab and the game skips it, no crash
  (**verify** with a second client).
- A `Recipe` is `ScriptableObject.CreateInstance<Recipe>()`: `m_item` = the clone's `ItemDrop`,
  `m_amount = 1`, `m_craftingStation` = `piece_workbench` (found among `ZNetScene` prefabs by
  name, **verify** the name), `m_minStationLevel = 1`, `m_resources` = the config list resolved
  through `ObjectDB.GetItemPrefab` (a name that resolves to nothing is logged and skipped) plus
  the tier below's compass with `m_amount = 1`, `m_recover = false`. Added to
  `ObjectDB.m_recipes`. `m_enabled = true`.
- Looks: the clone wears the wishbone's model on the belt and drops the wishbone's mesh. That is
  acceptable for the first release; a compass of its own is section 7. If `Wishbone` turns out to
  have an attach visual that reads wrong, `BeltStrength` or `Demister` are the other utility
  items to clone (`Assets/GameElements/Items/utility/`: `BeltStrength`, `Demister`, `IceShoes`,
  `IceSkates`, `Wishbone`).

## 2. Finding the target - built

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

## 3. Choosing what to seek - built

Built as planned in `SE_Compass`. `C` turned out to toggle walking, so the key is `N`
(`ZInput`'s defaults bind A C D E F G M Q R S T V W X and no other letter), read through
`ZInput.GetKeyDown` because the game runs on the new input system, and only while
`Player.TakeInput` says the player is not in a menu. Swapping one compass for another keeps the
effect, so the choice is checked against the worn tier every tick.

**The player:** press `CycleTargetKey` (C, **verify** that C is free in `ZInput`'s default
bindings) with a compass worn and the compass moves to the next group its tier offers, announced
top left: *Compass: Burial chamber*. The choice is kept per character in
`Player.m_customData["OdinsCompass.Target"]` (a vanilla string dictionary saved with the
character), so it survives a relog and a worn compass starts on what it last sought. A tier that
no longer offers the saved group (a downgrade: dropping a tier 4 compass for a tier 2) falls back
to the tier's first group.

Also: the status effect's icon text (`GetIconText`) shows the group's name, and the distance if
`ShowDistance` is on - off by default, a direction only is the wishbone's feel.

## 4. The guidance effect - built

Built the second way in `src/Streaks.cs`, then reworked twice after looking (2026-09-24). The
first version - a box emitter three metres behind the player, forty-five short streaks a
second when close, plus the wishbone's pings - was too much, too close and too loud. The
second - a ring ten metres around the player - was stiff and, on an ultrawide screen, all at
the edges of the view. Now it is two `ParticleSystem`s made in code that start *at* the player
and fly toward the target, world space, at a wind's pace with a little variation per particle,
swayed sideways by the noise module: the lines are trails drawn behind hidden particles, so
each line bends along its wavy path, one every second or two - two or three in the air at
once, each starting anywhere in the first five metres of the way - and a few small motes
ride along. The wishbone ping's material is copied. The pings are gone for
good; the only sound is none. The lines fly on the flat and keep low over the ground: every
tick each particle is given the vertical speed that carries it toward a metre or so above
the terrain under it (a terrain raycast per particle, a dozen at most; the noise sways only
sideways), so a hill carries them up and over instead of swallowing them (seen vanishing
into a slope, 2026-09-24; setting the position instead of the speed drew zigzags, same day);
rocks and trees are not sampled, so a boulder is still flown through. Once the target is nearer than a line flies in its life
each line gets exactly the life the way takes, so close by they run into the altar instead of
over it (seen sailing over it from fifteen metres, 2026-09-24). Within `ArrivalDistance`
(ten metres by default) `SE_Compass`
stops the wind - the player has arrived, and lines still in the air fly out and fade.
The lines are half transparent and taper to a point at both ends (`Blue` and `Taper` at the
top of the file, 2026-09-24); a line appears as its tip draws it out and disappears as its
tail runs up to the tip, because the trail outlives its particle instead of dying with it. `StreakEffect` is the config switch, the counts, sizes, sway and
start point are the `Layer` constants at the top of the file.

**The player:** three or four wavy lines of bright blue light leave the player toward the
target, a couple more when close, never a storm, and none once there. They sit in the middle
of the view. No sound. Nothing on the map.

**How:** `SE_Compass : StatusEffect`, created with `ScriptableObject.CreateInstance`, `name =
"OdinsCompass"` (the status effect is found by `name.GetStableHashCode()`, so the name is the
identity), added to `ObjectDB.m_StatusEffects` next to the items, and set as every tier's
`m_equipStatusEffect` - `Humanoid.UpdateEquipmentStatusEffects` adds it while a utility item
with it is worn and removes it when not, nothing to patch. Which tier is worn:
`Player.m_utilityItem.m_dropPrefab.name`.

The streaks - the `compass psystems` dump of 2026-09-24 settled it: no environment has a wind
streak system (the systems are mist, fog, rain, snow, ash, `WhirlWind` for Moder's fight and
interior dust), so the first way is out and the effect is built in code:

- **Reuse the game's wind.** The environments' particle systems are `EnvSetup.m_psystems` on
  `EnvMan.instance.m_environments` (`compass psystems` lists them by environment). If one of them
  is a wind streak system (the rain and snow ones are; a clear weather "wind" is the thing to
  look for - **verify** it exists), clone it, tint its start colour bright blue, parent it to the
  player, and turn its emitter so the streaks travel from behind the player toward the target
  (velocity over lifetime along the target direction, in world space).
- **Build one in code.** A `ParticleSystem` on a new `GameObject` under the player: a ring
  emitter around the player, world space simulation, velocity toward the target, long stretched
  billboards (stretched render mode) in bright blue, additive material taken from
  `vfx_WishbonePing`'s renderer so no material asset is needed. Rate scales with closeness.

Either way the direction is `target - player` flattened to XZ, and the effect stops when there
is no target. Sound: `SE_Finder`'s `m_pingEffectNear/Med/Far` on the wishbone's own status
effect were played with `SE_Finder`'s cadence and dropped after the first look - a compass
that pings every second is a nuisance. If a sound ever comes back it is a soft one on a
change (a new target found, the target switched), never a cadence.
`vfx_WishbonePing` is the fallback visual if the streaks disappoint.

## 5. Words

`assets/translations.csv` like OdinsMissingPatch's (`key,English,German,...`, fed to
`Localization.SetupLanguage` by a postfix), holding the item names and descriptions
(`$oc_compass1`..`8`, `$oc_compass_desc`), the group names the config defaults use (`$oc_eikthyr`,
`$oc_burialchamber`, ...), the status effect's name and the *Compass: ...* message. A group name
in the config that is not a `$` token is shown as typed, so a player adding a group needs no
translation file.

## 6. Verify list, in the order it is needed

1. ~~`compass locations`~~ done 2026-09-24: section 2's table is confirmed, the Deep North filled.
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

- **Choosing the target in a menu.** `N` cycles blind through a tier's groups - a tier 5
  compass has a dozen - and the only feedback is the status icon's text. Wanted: the key opens
  a menu that shows every group the worn tier offers with its icon and name, the current one
  marked, one click to pick. The game's radial menu (`Hud.instance.m_radialMenu`, a
  `Valheim.UI.RadialBase`) is the natural fit: it is opened with an `IRadialConfig`
  (`LocalizedName`, `Sprite`, `InitRadialConfig(radial)`), whose `InitRadialConfig` instantiates
  elements and hands them to `radial.ConstructRadial(list)`; `EmoteGroupConfig` is the
  simplest example (25 `EmoteElement`s from `RadialData.SO.EmoteElement`, one `Init` each).
  A `RadialMenuElement` carries `Name`, `SubTitle`, `Description`, an `Icon` image and an
  `Interact` func, so a mod's element is a `RadialMenuElement` subclass or a reused
  `EmoteElement`/`ItemElement` prefab with the mod's sprite and text - **verify** which
  element prefab in `RadialData.SO` can be instantiated with a custom sprite and name without
  an item or emote behind it, and that `RadialBase.Open(config)` from a `Player.Update` postfix
  on the cycle key opens it (`OpenRadialConfig` shows the game's own open path). `RadialBase`
  and the elements are public, so no publicizer is needed. The `N` cycling stays as the
  fallback (a gamepad has no spare key; the config can bind the menu to the radial's own
  button with a modifier). The gamepad and the `Hud.InRadial()` input block come for free with
  the game's radial. A plain panel of buttons (`Hud` prefab clones, the way the inventory
  screen's tabs are made) is the fallback if the radial cannot be reused.
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
