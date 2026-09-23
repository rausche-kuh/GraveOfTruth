# Map pins and map tables

The area doc for `SharedMapTable`, `AutoPins`, `PinLooks` and the helper they share,
`src/UniversalPins.cs`, plus `DeathPins`, which only tidies the game's own death pin. Implemented 2026-09-23 from the handover plan this file used to be; it
**builds but has not been run in game yet** — see "Not yet verified in game" at the end before
trusting any of it. Every fact in "How the game does it" was read out of
`decompiled/assembly_valheim/` for the current build, with line numbers.

## What it is for

The cartography table exists and nobody uses it, because sharing is a chore: walk to the table,
write, and everyone else has to walk there and read. And pins are a chore: placing one means a
click, a name, another click, so nobody pins the crypt they just cleared. The result is a map that
only the one player who explored an area can navigate.

Three things, all meant to feel like the game already did them:

1. **A table shares by itself** (`SharedMapTable`). Stand near a map table and your map is on it
   and its map is on yours. Nothing to click.
2. **The places worth a pin get one** (`AutoPins`). Dungeon entrances, ore deposits and places
   without an interior (fuling villages, tar pits, dragon eggs, Dvergr outposts, ...) — never
   a vegvisir ruin, since reading the stone pins the boss already. The pin is an ordinary map pin with an ordinary icon and the game's own name for the
   place. What is worth a pin is read off the game's data - an interior, a generator, tar, an item
   that cannot be teleported, a rock a smelter takes the drop of - not a list of names, so a new
   biome's places come in without a change.
3. **Those pins are universal** (`UniversalPins`, `PinLooks`). They belong to nobody, never
   collide with a pin a player placed by hand, never duplicate however many players and tables
   they pass through, and can be hidden as a group or by category (toggles on the large map),
   coloured by biome so they read at a glance.

Deliberately not done: new pin types with own icons and cloned legend buttons (BetterMap, see
`references.md`; it degrades on uninstall and is a lot of UI surgery), a server-side component,
or replacing the table's protocol (BetterCartographyTable). Everything is client side and speaks
the game's own table format, so a player without the mod sees the auto pins as ordinary shared
pins.

The Forge of Potential is location `AncientUpgradeStation`, flagged icon-placed and unique in the
game data: the game itself draws it on every map once its zone has been generated, like Haldor and
Hildir. Every icon-flagged location is skipped (below), so it gets no pin from us.

## How the game does it

All in `decompiled/assembly_valheim/Minimap.cs` unless said otherwise. The publicizer makes every
private member below reachable.

- **A pin** is `Minimap.PinData` (line 47): `m_name`, `m_type` (`PinType`, line 25: `Icon0`
  fire, `Icon1` house, `Icon2` hammer, `Icon3` orb, `Icon4` portal, then `Death`, `Bed`, `Shout`,
  `None`, `Boss`, `Player`, ..., `Memorial`), `m_pos`, `m_save`, `m_ownerID`, `m_author`
  (`PlatformUserID`), `m_checked`, plus UI state (`m_uiElement`, `m_iconElement`,
  `m_NamePinData`). All pins, of every kind, live in the one list `m_pins` (line 316).
- **Pin names are localized when drawn** (`PinNameData.SetTextAndGameObject`, line 97), so a
  `$token` name shows in each reader's own language. A pin with a valid author that is not the
  local user goes through `CensorShittyWords.FilterUGC`, which returns the text unchanged unless
  the platform requires text filtering (consoles).
- **`PinNameData.PinNameText` is a `TMP_Text`**, and TextMeshPro is not among the staged
  references: naming the property is CS0012. `PinLooks.NameText` finds the same child as the
  `Graphic` whose type name starts with `TextMeshPro`.
- **Profile save** (`GetMapData` line 2150 / `SetMapData` 2185) keeps every `m_save` pin with
  name, pos, type, checked, owner and author. Owner and author round-trip; nothing else about a pin
  survives a reload, so the icon is re-derived from the type on load. Filter state
  (`m_visibleIconTypes`) is not saved.
- **Adding** is `AddPin(pos, type, name, save, isChecked, ownerID, author)` (line 2352). A type
  past `m_visibleIconTypes.Length` is coerced to `Icon3` with a warning, which is why custom
  types are fragile. Adding a pin of a hidden type flips that type visible again
  (`ToggleIconFilter`, 2658, which also rumbles a gamepad); `UniversalPins.Add` puts the filter
  and the legend's grey back itself. `HaveSimilarPin` (2340) is name+type+save within 1m;
  `HavePinInRange` (2256) is any saved pin within a radius. `DiscoverLocation` (2309) is the
  game's own "auto pin" helper, and its message token **`$msg_pin_added`** ("Map location added")
  is what AutoPins shows too — there is no `$omp_` row for it.
- **Removing** is `RemovePin(PinData)` (2292). The player's removals — right click
  (`RemovePinUnderPointer`, 2528) and the gamepad's `JoyTabRight` (1009) — both go through
  **`RemovePin(Vector3, float)`** (2245), and nothing else does: the table read, the death pin and
  the event pins remove by `PinData`. So a prefix on the `Vector3` overload is exactly "the player
  took this off the map", which is where the removed-pin record is written.
- **Left click** (`OnMapLeftClick` 2476, and the gamepad's `JoyTabLeft` at 1015 inside
  `UpdateMap`): a pin with `m_ownerID != 0` is *claimed* (owner set to 0) instead of checked.
  Second click checks. The gamepad branch does **not** set `m_pinUpdateRequired`, so a fix in the
  `UpdatePins` postfix would lag behind it; `UniversalPins.UndoClaims` is a `Minimap.Update`
  postfix instead, while the large map is open.
- **Drawing** (`UpdatePins` 1627) runs only when `m_pinUpdateRequired` (Update, line 761), so on
  map movement, zoom, or a pin change, not every frame. For each pin: skipped when off screen,
  when its type is filtered, or when it has an owner and `m_sharedMapDataFade` is 0. A pin with an
  owner is drawn in `(0.7, 0.7, 0.7, 0.8 * fade)` and switches on `m_sharedMapHint`; others are
  white. Names show only in the large map below `m_showNamesZoom` (0.5; the large map zooms from
  `m_minZoom` 0.01 to `m_maxZoom` 1). `m_iconElement.color` and the name's colour are written on
  every run, so a tint has to be reapplied after it. **The game never activates a marker it
  finds inactive** — it only creates, moves and destroys them — so a patch that culls by
  `SetActive(false)` has to turn it back on itself.
- **Clicking needs a visible marker**: `GetClosestPin` (2273) skips a pin whose `m_uiElement` is
  not `activeInHierarchy`, so a culled pin cannot be ticked or removed by accident.
- **The shared-map toggle** is vanilla: `OnToggleSharedMapData` (2642) flips
  `m_showSharedMapData`, Update (738-757) fades `m_sharedMapDataFade` and the map shader's
  `_SharedFade`. It hides every pin with an owner and the "explored by others" fog. It is a button
  on the large map that appears once any owned pin exists (`m_sharedMapHint`).
- **The legend filter** is vanilla too: double tap an icon (`IconPressed` 2622,
  `ToggleIconFilter` 2658) hides that whole type; `JoyDPadRight` on gamepad.
- **The table blob.** `MapTable.cs`: the ZDO holds `ZDOVars.s_data`, a compressed
  `ZPackage`: version int (`Version.SharedMap.PinsAuthor` = 3), the explored bitmap as one bool
  per cell (`m_textureSize²`, the prefab sets 2048 so ~4M bools), pin count, then per pin owner
  long, name, pos, type int, checked, author string. `GetSharedMapData(old)` (2707) merges the
  old bitmap with `m_explored | m_exploredOthers` and writes every `m_save` pin **of the local
  map** except deaths — the old blob's pins are not carried over, which is why every write reads
  first — owner = `m_ownerID` or, when 0, the local player id; author = `m_author` when valid.
  `AddSharedMapData(data)` (2781) explores the others-bitmap, then **marks every pin with a
  foreign owner for deletion**, walks the blob, keeps a marked pin when any saved pin lies within
  1m of the blob pin (`HavePinInRange`), adds a blob pin whose owner is not the local player when
  nothing is within 1m, and finally removes what is still marked. So: foreign pins are replaced
  by what the table holds, own pins are never touched, and duplicates are impossible as long as
  the position is the same. A blob pin never carries owner 0 (the writer substitutes its id).
- **Read and write** (`MapTable.OnRead` line 53, `OnWrite` 84): read decompresses `s_data` and
  calls `AddSharedMapData`; write calls read first (no message), checks
  `PrivateArea.CheckAccess(pos)` (the flashing variant), builds `GetMapData(current)` and sends it
  with `m_nview.InvokeRPC("MapData", pkg)`, which the owner answers by setting `s_data`
  (`RPC_MapData` 111) to `pkg.GetArray()` — byte for byte what the writer sent. Any client may
  write; the hover texts use `PrivateArea.CheckAccess(pos, 0f, flash: false)` for the
  non-flashing check. `MapTable` has no instance registry and no `OnDestroy`.
- **Cost of a write:** `ReadExploredArray` builds a `List<bool>` of 4M entries from the old blob,
  `GetSharedMapData` writes 4M bools, then `Utils.Compress`. Main thread, roughly a tenth of a
  second; it is the hitch the game already has on a manual write.
- **Locations on a client.** `ZoneSystem.m_locations` (ZoneSystem.cs 511) is the list of
  `ZoneLocation` (148): `m_prefabName`, `m_biome`, `m_group`, `m_unique`, `m_iconAlways`,
  `m_iconPlaced`, and `m_prefab`, a `SoftReference<GameObject>` from `SoftReferenceableAssets.dll`
  — which `setup` does not stage, so the dev dump reaches `Load` / `Asset` / `Release` by
  reflection. `ZoneSystem.GetLocation(int hash)` finds one by prefab name hash. The list is
  present on every client. `m_locationInstances` (where each one is in this world) is **server
  only**; clients only get the icon-flagged ones through the `LocationIcons` RPC, which
  `UpdateLocationPins` (Minimap 1454) draws as unsaved `PinType.None` pins with a location
  sprite. A placed location in a loaded zone is a `LocationProxy` (LocationProxy.cs) whose ZDO
  holds `s_location` (the prefab name hash) and which parents the spawned prefab under itself
  (`m_instance`, private); `GetComponentInParent<LocationProxy>()` from the `Location` leads back
  to the name. The spawned prefab carries a `Location` component (Location.cs): `m_hasInterior`,
  `m_exteriorRadius`, `m_interiorTransform`, `m_discoverLabel` (the name `Player` records as a
  known location, line 2037; a fallback pin name), `m_biome`, and the static registry
  `s_allLocations` (line 50), filled in `Awake`, emptied in `OnDestroy`. A dungeon entrance is a
  `Teleport` child of that prefab (Teleport.cs): `m_enterText` is the place name the game shows
  on entry (`$location_forestcrypt` "Burial Chambers", `$location_mountaincave` "Frost Caves",
  `$location_morkhalla` "Mörkhalla", ... — the game's own tokens), `Interact` moves the player.
  The interior sits ~5000m above the entrance, so `Character.InInterior(pos)` (Character.cs 4371,
  `y > 3000`) tells the two apart.
- **The location table can be read without running the game.** `SoftRef/manifest` (not
  `_extended`) lists every location prefab as `Assets/world/Locations/<Biome>/<Name>.prefab` with
  its asset ID and bundle. The `ZoneLocation` lists are MonoBehaviours in `_GameMain`'s bundle:
  `_ZoneSystem` (130 entries; a second, stale `_ZoneSystem` holds 124) plus `_LocationList_Mistlands`,
  `_Ashlands`, `_DeepNorth`, `_MountainCaves`, `_Hildir` and `_cp1` (the tar pits, easy to miss:
  under 1 KB). **The serialized `m_prefabName` is stale** (the game sets it from `m_prefab.Name`
  at load, ZoneSystem.cs 935): match `m_prefab.m_assetID` instead, whose `v3 v2 v1 v0` as 8-digit
  hex each is the manifest's asset ID. As of 2026-09-23: 170 enabled locations. `omp_locations`
  is still the in-game check.
- **A location's networked pieces are spawned apart from it, but its copy keeps them.**
  `ZoneSystem.SpawnLocation` (2426) in `Full`/`Ghost` mode instantiates every enabled `ZNetView`
  child as an object of its own (pickables, the tar, a `DungeonGenerator`, which then generates),
  then makes the `LocationProxy`; the proxy spawns the prefab in `Client` mode, which deactivates
  the `ZNetView` children, instantiates, and reactivates them on the prefab only. So the `Location`
  under the proxy has them as **inactive children** on every machine, and
  `GetComponentsInChildren<T>(true)` sees what the location is made of. `Location.m_generator`
  points at the copy's generator; `DungeonGenerator.m_themes` is a `Room.Theme` flag set
  (`GoblinCamp` 0x10, `MeadowsVillage` 0x20, `MeadowsFarm` 0x40, `AshlandRuins` 0x1000,
  `FortressRuins` 0x2000, `NorthVillage` 0x10000, ...). `LiquidVolume.m_liquidType` is
  `LiquidType.Tar` in a tar pit. `Vegvisir.m_name` is `$piece_vegvisir`.
- **Items that cannot go through a portal** (`m_shared.m_teleportable == false`): the ores and
  metals, `DvergrNeedle`, `MechanicalSpring`, `CharredCogwheel`, Hildir's three chests and the
  `DragonEgg` (type `Misc`) — and of those, only the dragon egg is a `Pickable` in any location.
- **Ore.** The game has three kinds of rock, and every deposit is one of them:
  - `MineRock5` (MineRock5.cs): `m_name`, `m_dropItems` (a `DropTable`, `m_drops` of
    `DropData.m_item`). The copper deposit, silver vein, muddy scrap pile, the Mistlands giant
    armour (`giant_helmet*`, `giant_sword*`: iron and copper scrap) and the Deep North's
    petrified gammeltroll (`GoldOre`) break apart as one — the `_frac` prefab.
  - `MineRock` (MineRock.cs): the same `m_dropItems`. The Ashlands' lava leviathan
    (`LeviathanLava`, flametal) and the ocean's `Leviathan` (chitin) are this.
  - `Destructible` (Destructible.cs) with a `DropOnDestroyed` (`m_dropWhenDestroyed`): tin and
    obsidian. The copper deposit and silver vein are a `Destructible` too, whose
    `m_spawnWhenDestroyed` is the `_frac` `MineRock5`; the first hit swaps it.
  All three have `Damage(HitData)`, the entry every hit goes through on the client that swung,
  before the RPC to the owner; `HitData.GetAttacker()` names who. A deposit `Destructible` is
  **immune to everything but the pickaxe** (`m_damages`: `m_blunt`/`m_chop`/... `Immune`,
  `m_pickaxe` `Normal`); a Dvergr barrel or crate that drops copper scrap or flametal takes blunt
  normally. Which drop is ore is not a name list: it is whatever some `Smelter.m_conversion` on
  `ZNetScene.m_prefabs` accepts as `m_from` (the derivation ServersideQoL uses) — **restricted
  to smelters with an `m_fuelItem`**, since the kiln, the windmill and the spinning wheel are
  `Smelter`s too and would make wood, barley and flax "ore". `m_to` is what it becomes. Soft
  tissue (`Softtissue`, the Mistlands giant remains) is the eitr refinery's *fuel*, not its
  input, so the rule misses it; `ExtraOre` adds it back. Tin (`TinOre`) passes the rule but
  lines every Black Forest shore; `SkipOre` drops it.
- **Death pin.** `Player.OnDeath` (Player.cs 3317, owner only) sets the profile's death point
  (read by nothing: `Minimap.UpdateProfilePins` calls `HaveDeathPoint()` and drops the result),
  calls `CreateTombStone` (3283), then adds a saved `PinType.Death` pin `$hud_mapday N` at the
  player's feet (3448). The game never removes it. `CreateTombStone` makes a grave only when the
  inventory is not empty and `DeathKeepInventory` is off, and then calls
  `TombStone.Setup(name, playerID)` on it — the one point that says a grave was made (the
  inventory count does not: `KeepGearOnDeath` and the world's death keys decide too). An empty
  grave destroys itself in `UpdateDespawn` on whichever machine owns its ZDO (`GiveBoost` runs
  there, so a patch on it misses a grave a friend emptied); `PositionCheck` puts a grave that
  drifted more than 4m (XZ) back to its spawn point. `TombStone.GetOwner()` is the dead player's
  id. No registry of graves exists.
- **Portals.** `TeleportWorld` (TeleportWorld.cs): the tag is ZDO `s_tag`, `GetText()` returns
  it (UGC-filtered), `SetText` sends `RPC_SetTag`, which the owner applies (187-212) — so a
  `SetText` postfix would see the old tag on every client but the owner. A placement ghost has no
  ZDO and disables itself in `Awake`. No instance registry.
- **Biome at a point**: `Heightmap.FindBiome(Vector3)` (Heightmap.cs 1147) needs a **loaded**
  heightmap and returns `None` otherwise, which a pin far across the map never has.
  `WorldGenerator.instance.GetBiome(Vector3)` (WorldGenerator.cs 746) is the noise itself,
  microseconds, anywhere. `Heightmap.GetBiomeColor(Biome)` is the terrain splat mask
  (`(255,0,0,0)` for Swamp), not a colour anyone should look at.
- **Per character storage** is `Player.m_customData` (Player.cs 581), a string dictionary saved
  and loaded with the character. BetterMap keeps its pin record there, BetterCartographyTable its
  shared pins.
- **`PlatformUserID`** is not in `assembly_valheim` or `assembly_utils` but in
  `valheim_Data/Managed/Splatform.dll` (struct `Splatform.PlatformUserID`), which `setup.sh` /
  `setup.ps1` now stage into `lib/` (`Splatform*.dll`). `new PlatformUserID(platform, userID)`
  builds one with `m_platform` a `Platform` (`Equals(string)` compares the name) and `m_userID`
  the rest; `TryParse` maps only `S`/`X`/`N`/`A`/`V` prefixes to real platforms and accepts any
  other `prefix_value`, `IsValid` is true for it, and `ToString()` gives `prefix_value` back. So
  `OdinsMissingPatch_dungeon` is a legal author that survives the profile and the table
  unchanged and carries the category. Nothing in `Minimap` displays the author.

## How it works

### Identity: a universal pin is a shared pin nobody owns

An auto pin is an ordinary saved pin (`m_save = true`, vanilla type) with

- `m_ownerID = UniversalPins.Owner`, a fixed non-zero constant
  (`"OdinsMissingPatch".GetStableHashCode()` shifted left with the low bit set); and
- `m_author = new PlatformUserID("OdinsMissingPatch", "<category>")`, category one of `dungeon`,
  `ore`, `place`, `portal`.

That single choice gets the whole "universal" behaviour from vanilla: the table carries it to
everyone, the position dedupes it, the shared-map toggle hides all of them at once, and a player
without the mod sees a normal shared pin. **The author is the identity**; the owner is restored
from it (`Normalize`) whenever it has been lost — to a claim (owner 0) or to a player without the
mod writing it to a table under their own id.

The helper's patches run while either `AutoPins` or `SharedMapTable` is on:

1. **Table read keeps our pins** (`KeepOnTableRead`). `AddSharedMapData` deletes foreign pins
   the table does not hold, which would eat a fresh discovery at the first table that has not seen
   it. The prefix zeroes the owner of every universal pin for the length of the read — the game
   then treats them as the player's own, never marks them, and still position-dedupes the blob's
   pins against them — and the finalizer puts the owner back. Swapping the owner keeps each pin's
   `PinData`, its ticked state and its marker; re-adding the missing ones afterwards would not.
   The finalizer then normalizes what the blob brought, removes what the player has dismissed, and
   drops a universal pin the read added within the category's merge radius of one the player
   already had (the table's own check is 1m; two strikes on one ore deposit are metres apart).
2. **A claim becomes a tick** (`UndoClaims`, `Minimap.Update` postfix with the large map open):
   a universal pin with owner 0 gets its owner back and `m_checked` flipped, so the vanilla first
   click ticks it. Owner 0 can only come from a claim, since a blob never carries it.
3. **Removal is remembered** (`RecordRemoval`, prefix on `RemovePin(Vector3, float)`): the
   position and category go into `m_customData["omp.pins.dismissed"]` as
   `world;category;x;z|...` (world UID, one decimal). Discovery and the table read both skip a
   dismissed position within the category's merge radius. `omp_pins_forget` (dev build) clears
   the current world's.

A removal is local to the character. Our next write leaves the pin off the table (a write carries
only the writer's own map), so a player without the mod loses it at their next read, while a
player with the mod keeps theirs through the read and puts it back on the next write.

### Discovery: the client, from registries, not sweeps

Everything is decided on the client that is there; there is no scan of `ZNetScene.m_instances`.

| Source | Hook | Position | Name |
| --- | --- | --- | --- |
| Dungeon | `Location.s_allLocations` with `m_hasInterior`, every 3s | its outside `Teleport` (fall back to the location) | `Teleport.m_enterText`, else `m_discoverLabel`, else `$omp_dungeon` |
| Place | `Location.s_allLocations` without an interior, a rule or `PlaceList`, every 3s | the location | the list's token (`Name=$token`, or the default's), else `m_discoverLabel`, else the rule's name, else the prefab name |
| Ore | `MineRock5`/`MineRock`/`Destructible.Damage` postfixes, attacker is the local player | the rock | the `m_name` of what the smelter makes of the drop (`$item_copper`, `$item_iron` for scrap, ...), or of the item itself for an `ExtraOre` one (`$item_softtissue`) |

A place without an interior gets a pin when it is in `PlaceList` or one of these holds, first
match wins (`Classify`, once per `Location`):

1. **Treasure**: a `Pickable` of an item that cannot be teleported — named by the item (the
   dragon egg in `DrakeNest01`).
2. **Generated**: it has a `DungeonGenerator` — named by `Room.Theme`: fuling village, village
   (Meadows and Deep North), farm, fortress ruins, and "Ruins" for the Ashlands ruins and any
   theme a later update adds.
3. **Tar**: a `LiquidVolume` of tar — "Tar Pit".

Before any rule, a location holding a `Vegvisir` is ruled out: the stone pins its boss when read,
which is all the ruin is for, so a pin of its own is clutter. `PlaceList` still wins, so a vegvisir
ruin named there is pinned.

What is left over holds nothing that names it: houses, huts, shipwrecks, runestones, the Dvergr
sites. The Dvergr sites are worth a pin and are the default `PlaceList`. Against the game's table
as of 2026-09-23 the rules pin all 12 interiors, 7 generated places, 3 tar pits and the drake nest
and rule out 16 vegvisir ruins; the list adds the 10 Dvergr sites. (The two outdoor places of
mystery and the Deep North memorial were in the list until vegvisirs were dropped; they are
vegvisir ruins too.)

Portals are not pinned: that is to become a feature of its own. `Category.Portal` (author
`OdinsMissingPatch_portal`) stays in `UniversalPins` for it, and `PinLooks` has no zoom setting
for it (never hidden).

Each location is described once (`ConditionalWeakTable<Location, Site>`): prefab name from the
proxy's `s_location` hash via `ZoneSystem.GetLocation`, whether the game draws its own icon
(`m_iconAlways || m_iconPlaced` — boss altars, traders, Hildir, the Forge: skipped in both
categories), and the entrance (looked for again until found, in case the door spawns late).
Locations count only within `DiscoverRange` (40m, 3D, so the interior 5000m up is
never "near"; 0 pins whatever is loaded), and the sweep skips while the player is in an interior.
A muddy scrap pile inside a crypt is excluded by `InInterior`.

**Mined out** (`SweepMinedOut`, the same 3s sweep): an ore pin within 32m of the player, in an
area `ZNetScene.IsAreaReady` calls loaded, with no rock that drops ore within 12m of it
(`Physics.OverlapSphereNonAlloc`, then `GetComponentInParent` for the three rock kinds and the
same `OreLabel` test) on two sweeps in a row, has been mined out — by anyone, since a rock is
destroyed on its owner's machine and just vanishes on the others. `MinedOut` decides: `Tick`
(default) sets `m_checked`, the game's cross, once per pin and session so a player who unticks
it is not overruled; `Remove` takes it off through `UniversalPins.Discard`, which records it as
dismissed like a right click; `Keep` does nothing. 12m because a pin sits where the deposit was
first struck and a copper deposit's pieces spread from there; a neighbouring deposit that close
keeps the pin, which errs the right way.

Before adding: a universal pin of the same category within `MergeRadius` (1m for locations and
portals, whose positions every client derives from the same ZDO; 8m for ore) means it is already
there, whoever put it there; a dismissed position means no; and so does any other saved pin that
is not a death pin within `PinSpacing` (10m, XZ; 0 off) — the player's own, a friend's from a
table, or another mod's. That last one is what keeps a map made with a discovery pin mod
(ordinary pins, the dungeon pin on the entrance and the ore pin on the rock, as ours) from
getting a second pin beside each of its own. It is checked only before adding; a universal pin a
table brings next to such a pin is kept. The pin goes in through
`UniversalPins.Add`, and `$msg_pin_added: <name>` goes to the top left with the pin's icon, as
`DiscoverLocation` does.

### Sharing: the write is the sync

`SharedMapTable.Sync` is `OnWrite` minus what a player would notice: `OnRead(..., showMessage:
false)`, the non-flashing access check, `GetMapData`, `InvokeRPC("MapData")`, no message, no
effect. Tables register in a `MapTable.Start` postfix; a dead entry is pruned on the next check.

A 1s check (Player.Update postfix, local player) syncs at most **one** table per check, since each
sync is a hitch. A table within `SyncRange` (64m) syncs when:

1. **Arrival**: it is not up to date for this visit. Beyond `SyncRange + 8m` it is forgotten, so
   the next arrival syncs again; the margin keeps the edge from flickering.
2. **A pin changed**: `AddPin`/`RemovePin(PinData)` postfixes on saved pins bump a generation
   counter (not while a sync itself runs); a writable table behind the counter syncs.
3. **Someone else wrote**: the ZDO's `DataRevision` moved and the data is not byte-for-byte what
   this client last sent (the owner stores exactly the sent bytes, so our own write is recognised
   and becomes the new baseline instead of triggering another write).

No table syncs more often than `MinInterval` (10s). A table behind a ward the player cannot use is
read on arrival and on a foreign write, never written. Two players writing one table in the same
second is last-writer-wins on the ZDO; since each write merges first, the loser's pins return on
their next sync. A "pins only, keep my exploration private" switch is deliberately absent: the
game's writer always merges the bitmap, and faking it needs BetterCartographyTable's transpiler.

### Looks: colour by biome, hide by zoom or by toggle

`PinLooks.Tint` is an `UpdatePins` postfix. For each universal pin with a marker: in the large map,
zoomed out past the category's zoom setting (`OreZoom` and `PlaceZoom` 0.5, `DungeonZoom` 1 =
never; a portal pin has no setting and is never hidden), the marker and the name are deactivated; otherwise the marker is active
again (also after the tweak is switched off) and, while on, icon and name take the biome's colour
with alpha = `m_sharedMapDataFade`, so the vanilla toggle still fades them. The biome is asked of
`WorldGenerator` once per `PinData`. Colours are one `ConfigEntry<Color>` per biome, hand-picked
light tints (Meadows green, Black Forest darker green, Swamp brown, Mountain ice blue, Plains
yellow, Mistlands lilac grey, Ashlands red, Deep North pale blue, Ocean blue).

**Toggles** (`MapToggles`): at `Minimap.Start` the panel of `m_publicPosition` (`PublicPanel`,
bottom right, 250x42 at y 20, pivot bottom) is copied once per category with a show setting and
stacked above it at 51 px a row — the step from it to `SharedPanel` (y 92, pivot middle, which is
`m_sharedMapHint` and only shown once there is shared data) — so the copies sit at 122, 173, 224,
clear of the legend (`IconPanel`, x -85..-9). Each copy's `onValueChanged` is replaced, since it
still carried the prefab's call to `OnTogglePublicPosition`, and its `UIGamePad` and hint are
destroyed, since the copy would answer the same gamepad key and flip with the original (so the
toggles are mouse only). The label is set by reflection on the TextMeshPro `text`. A toggle writes
`ShowDungeons` / `ShowOre` / `ShowPlaces`; `Tint` then culls that category on **both** maps the
same way as the zoom does, so a hidden pin cannot be clicked either. Every change in the section
(`Tweak.OnSettingChanged`) re-syncs the toggles and sets `m_pinUpdateRequired`; the tweak off
hides the toggles and shows every pin.

Icons are per category and vanilla: dungeons `Icon1` (house), ore `Icon3` (the orb), places
`Icon0` (fire), `Icon4` (portal) kept for the portal feature; a value outside `Icon0..Icon4` falls back to the default (BepInEx
cannot restrict an enum entry to a list — `AcceptableValueList<T>` needs `IEquatable<T>`). Because
each category has its own icon, the legend's double tap hides a category. `Icon2`, the hammer, is
left free for the player's own pins.

### Death pins: gone with the grave

`DeathPins` leaves the game's death pin alone except for two things:

1. **No grave, no pin** (`OnlyWithGrave`): a `Player.OnDeath` prefix clears a flag, a
   `TombStone.Setup` postfix sets it when the owner id is the local player's, and the `OnDeath`
   postfix removes the death pin within 1m of the player if the flag is still clear.
2. **The pin goes with the grave** (`RemoveWithGrave`): graves register in a `TombStone.Awake`
   postfix. Every 2s, a death pin within 32m (3D, so a death in a dungeon is checked only from
   inside it, where its grave is too) in an area `ZNetScene.IsAreaReady` calls loaded, with no
   grave of the local player within 8m (XZ) on two sweeps in a row, is removed. It asks the
   world, like `MinedOut`, so it does not matter who emptied the grave or on which machine it
   was destroyed; an old death pin whose grave is long gone is cleared on the next visit.

## The pieces

| File | Holds |
| --- | --- |
| `src/UniversalPins.cs` | owner, author, `IsUniversal`, `TryGetCategory`, `Find`, `Add`, `Normalize`, the dismissed record, `UndoClaims`, `KeepOnTableRead`, `RecordRemoval` |
| `src/Tweaks/SharedMapTable.cs` | section "Shared Map Table": `SyncRange` (64), `MinInterval` (10s); the registry, the check, the silent write |
| `src/Tweaks/AutoPins.cs` | section "Auto Pins": `Dungeons`/`Ore`/`Places`, `DiscoverRange` (40), `PinSpacing` (10), the three icons, `PlaceList`, `ExtraOre` (`Softtissue`), `SkipOre` (`TinOre`), `MinedOut`, `ShowMessage`; the sweep, the place rules, the ore strike, the mined-out check |
| `src/Tweaks/PinLooks.cs` | section "Pin Looks": a colour per biome, a zoom for dungeons, ore and places, `MapToggles` and `ShowDungeons`/`ShowOre`/`ShowPlaces`; the tint, the large map's toggles |
| `src/Tweaks/DeathPins.cs` | section "Death Pins": `RemoveWithGrave`, `OnlyWithGrave`; the grave registry, the sweep, the no-grave check |
| `src/Dev/MapCommands.cs` | Debug only: `omp_locations [filter]` (every `ZoneLocation`: name, biome, interior, game icon, discover label, entrance text, the place rule it falls under — also to the BepInEx log), `omp_pins`, `omp_pins_forget`, `omp_pins_clear` |

Default `PlaceList`, from the game's location table (BetterMap's `Mistlands_GuardTower1-3` no
longer exist; the towers are `_new` and `_ruined_new` now): `Mistlands_Excavation1-3`,
`Mistlands_GuardTower1-3_new`, `Mistlands_GuardTower1_ruined_new`, `_ruined_new2`,
`Mistlands_GuardTower3_ruined_new` and `Mistlands_Lighthouse1_new`, each with its
`$omp_place_*` token in `AutoPins.DefaultPlaces`. Translations: `$omp_dungeon`, nine
`$omp_place_*` (the rules' and the list's names) and the three toggle labels `$omp_map_*`. Dungeons, ore and treasure are named by the
game's own tokens (`$location_*`, `$item_*`) and need no row.

## Not yet verified in game

Built on Linux in Debug and Release; nothing below has been seen running. In order:

1. **`omp_locations`**: the `rule` column should match the offline read above (read from the
   prefab asset, where every child is active); the `PlaceList` names exist; Morkhalla (`MorkBorg`)
   comes through the dungeon rule.
2. **Location on the host.** The zone's generator spawns locations in `SpawnMode.Full`; check that
   a `Location` under a `LocationProxy` exists there too, so the host pins the same way a client
   does (the prefab-name fallback `Utils.GetPrefabName` covers a missing proxy).
3. **Two clients on a local host**: A places a table, B walks up, both maps merge without a click;
   a table under B's ward is read but not written by A; the arrival hitch is no worse than a
   manual write; standing next to a table does not write every `MinInterval` (the own-write
   check).
4. **Pins**: a burial chamber is pinned once with the game's name, entering does not pin again,
   B gets it through a table at the same position; striking copper pins "Copper" once, tin
   "Tin", a lava leviathan "Flametal", a Mistlands giant helmet "Iron"; silver is not pinned
   until struck; a Dvergr barrel and a crypt mudpile are not pinned; a tar pit, a fuling village
   and a drake nest are pinned on walking up (the rules see the inactive children); a copper
   deposit mined to the last piece is ticked a few seconds later, and one mined by B while A was
   away is ticked when A comes by; a deposit still half there keeps its pin; with `Remove` the pin
   goes and no table brings it back; a removed pin stays removed across a table
   round trip; a foreign pin the table lacks survives a read; a first left click (mouse and
   gamepad) ticks a universal pin and the shared-map button still hides it.
5. **Looks**: Swamp and Mountain tints read against the map; the fade follows the toggle; culled
   pins come back on zooming in and after switching the tweak off; the name text is found (the
   `TextMeshPro` type-name lookup).

6. **Changes of 2026-09-23 (from the DiscoveryPins comparison)**: a hand-placed pin at a crypt
   entrance keeps the auto pin away, a death pin nearby does not; the Mistlands giant remains
   are pinned as soft tissue (is the drop in their `DropTable` / `DropOnDestroyed`, and does a
   `Destructible` one pass `OnlyPickaxe`?); tin is not pinned; a death with an empty inventory
   leaves no pin; a grave looted by you, and one looted by another player while you were away,
   takes its pin with it a few seconds after you are near; a grave that slid down a slope keeps
   its pin; dying inside a dungeon, the pin stays until the grave there is emptied.
7. **Toggles and vegvisirs**: the three toggles sit above "visible to other players" without
   overlapping the shared-map panel or the legend, at 1080p and at a wide aspect; each hides its
   category on the large map and the minimap and survives a restart; the original toggle still
   sets the public position and a gamepad press flips only the original; walking up to a
   vegvisir ruin adds no pin (Charred Fortress included).

Known limits: if the arrival hitch is noticed, the
blob can be built off the main thread (copy `m_explored`/`m_exploredOthers`, pack and compress on
a `Task`, send on the next Update). `AddSharedMapData` is version-tolerant and the write goes
through the game's own serializer, so nothing needs doing unless `Version.SharedMap` grows.

## References

The three checkouts that cover this ground, with what each one settled, are in
[`references.md`](references.md) under "Map tables and auto pins". Copy nothing (MIT needs the
notice, the other two carry no license at all).
