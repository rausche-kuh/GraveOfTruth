# Roadmap

Odin's Paths lays trails across the world the way real footpaths form. When a vegvisir reveals a
boss altar, the next time the world sleeps a path is laid to it from the nearest point of the
network: the sacrificial stones, a marked base, or a path already laid. The path follows the
easiest ground, winds around hills, ends at a shore and picks up again on the far one. Nothing is
built yet. This file says what is possible, what it costs and in which order to build it.

Game facts without a mark were read in `decompiled/assembly_valheim/` on 2026-09-24. Facts marked
**verify** come from memory of the game or from reasoning, and still need checking in game before
code rests on them. The `paths facts` and `paths bench` dev commands (`src/Dev/PathCommands.cs`,
`deploy -c Debug`) exist for the first of those checks. What a check shows goes into `CLAUDE.md`.

## 0. What the design rests on

**The server does everything, in one pass, without loading a single zone.** A zone's terrain
edits (everything the hoe, the pickaxe and the cultivator ever did there) live in one blob on one
ZDO: the zone's *terrain compiler*, a `TerrainComp` prefab placed at the zone centre
(`Heightmap.GetAndCreateTerrainCompiler`, `ZoneSystem.GetZonePos` = `id * 64`, y 0).
`TerrainComp.Save` writes the blob to `ZDOVars.s_TCData`, and the format is simple enough to
write ourselves: version `1`, operation count, last point and radius, then per vertex
`bool modifiedHeight [+ float levelDelta, float smoothDelta]`, then per vertex
`bool modifiedPaint [+ r g b a]`, all run through `Utils.Compress`. So the mod can write a whole
path as data:

- a zone that has a compiler: decompress the blob, merge the path in, write it back;
- a zone without one: `ZDOMan.CreateNewZDO(zoneCenter, compilerPrefabHash)` with the same
  `Persistent` / `Type` / `SetPrefab` / `SetRotation` that `ZNetView.Awake` sets, plus our blob.

Only the server may create compilers this way. It holds every ZDO, so it can see whether a zone
already has one. A client sees only the ZDOs near it and could make a second compiler, and
`TerrainComp.Awake` destroys a duplicate. That would wipe a player's digging. A client that has
the zone loaded picks up the change by itself: `TerrainComp.Update` → `CheckLoad` reloads whenever
the ZDO's `DataRevision` moves, then pokes the heightmap and resets the grass. The precedent is
`ValheimServersideQoL` (in `~/Documents/Code/test/othervalheimmods/`, no license, so read only),
whose admin options rewrite `s_TCData` on the server.

The exact base heights and base mask of any zone, loaded or not, come from
`HeightmapBuilder.instance.RequestTerrainSync(zonePos, width, scale, false, WorldGenerator.instance)`.
That is what the game builds heightmaps from, biome blending included. Deltas computed against
it are the deltas the game will add.

**Because it is ordinary terrain data, only the server needs the mod.** Clients without it see
the paths, and the paths stay when the mod is removed. In a local game the host is the server.

**What it costs.** The blob has a fixed shape per zone whatever is in it: `(width+1)²` vertices,
`65² = 4225`, since the zone heightmap is 64 × 1 m (`paths facts`, 2026-09-24; the compiler
prefab is `_TerrainCompiler`). A path 3 m wide crosses a zone on about 200-300 vertices. At
1 + 8 + 16 bytes each, plus a bool for every other vertex, that compresses to a few KB. A 3 km
route touches about 50-80 zones: a few hundred KB of world save, sent to a client only when it
loads those zones. Loading such a zone costs what loading any hoed zone costs: one heightmap and
collider rebuild. **Levelling costs the same as painting.** Level and smooth deltas sit in the
same blob, and a changed height rebuilds the same mesh as a changed colour. So ground levelling
is no performance question. What limits it is how it looks, and the clamp in
`ApplyToHeightmap`: final height is kept within **±8 m of the base height**.

**Heights for the search come from the world generator, not from loaded terrain.**
`WorldGenerator.GetHeight(x, z)` is the biome lookup plus that biome's noise. It works anywhere,
on any server, loaded or not. It is not safe off the main thread next to the game's own
`HeightmapBuilder` thread: `m_cachedRiverGrid` / `m_cachedRiverPoints` are two unlocked fields,
and the biome caches are static. So the search runs as a coroutine on the main thread with a
per-frame budget. `paths bench` measured **1.5 µs per sample** (40 000 in 61 ms, 2026-09-24).
The exact zone build takes ~20 ms through `RequestTerrainSync`, but that is mostly waiting on the
builder thread: its non-blocking twin `RequestTerrain` queues a zone and returns null until it is
ready, so `TerrainWriter` asks for every zone at once and costs the main thread nothing for it. Heights the generator does not know about are player digging, the flattening that
locations do with `TerrainModifier`, and vegetation: trees, rocks, the Mistlands' giant rock
formations. Section 1 deals with each.

**The triggers are two vanilla calls on the server.**

- *Revealed altar:* `Vegvisir.Interact` → `Game.DiscoverClosestLocation` → the routed
  `RPC_DiscoverClosestLocation(sender, name, point, pinName, pinType, showMap, discoverAll)`,
  which the server handles with `ZoneSystem.FindClosestLocation`. A postfix there (server only)
  repeats the lookup and queues a path when `name` is a boss location. Names confirmed by
  Odin's Compass's location dump: `Eikthyrnir`, `GDKing`, `Bonemass`, `Dragonqueen`,
  `GoblinKing`, `Mistlands_DvergrBossEntrance1`, `FaderLocation`. The queue is keyed by location
  position, so reading the same stone twice queues nothing new.
- *Sleep:* `Game.UpdateSleeping` (server, every 2 s) calls `EnvMan.SkipToMorning` once everyone
  is in bed. The skip runs for **12 seconds** (`m_timeSkipSpeed = remaining / 12`), then
  `SleepStop` goes out. A postfix on `SkipToMorning` starts the queued searches. Writing waits
  for a finished search, so the path is ready at wake up when the search fits in the skip, and
  appears a little later when it does not. Nobody is out walking at night anyway.

**Starting points.** The sacrificial stones are the `StartTemple` location
(`Game.m_StartLocation`). The server has its instance in `ZoneSystem.m_locationInstances`, and
`ZoneSystem.GetLocationIcon("StartTemple", out pos)` works too. Bases and existing paths:
section 3.

**Vegetation can be kept off the path before it exists.** `ZoneSystem.SpawnZone` generates a
zone once, on the server (`SpawnMode.Full`, or `Ghost` for zones nobody stands in). It calls
`PlaceLocations` and then `PlaceVegetation(..., clearAreas, ...)`, and every vegetation spawn is
skipped `InsideClearArea(clearAreas, p)`. Built instead (`Clearing.cs`): a postfix on
`SpawnZone` that, for a zone generated just now, removes the clearable vegetation ZDOs near the
dirt in the zone's terrain data - no trail storage needed, and it sees exactly which vegetation it
removes, which `InsideClearArea` does not. In zones already generated, the lay itself removes the
vegetation ZDOs near the trail (`ZDOMan.FindObjects`, `SetOwner` to the server, `DestroyZDO`).
Vegetation without a `ZNetView` is placed again by every client on every load and cannot be
cleared by the server. Grass is clutter, not vegetation: `ClutterSystem` reads the paint mask,
so painted ground is bare.

**Snow.** In the Deep North the paint mask's green channel, which is cultivation everywhere else,
is **snow depth**. `Heightmap.GetHeightOffset` sinks the feet by it, and `GetGroundMaterial`
turns it into `SnowDeep` / `SnowVeryDeep`. `PaintCleared` treats `Cultivate` in the Deep North as
*adding* snow. Every other paint type lerps the channel toward its own green, which is 0 for
dirt and for paving, so **a dirt path digs itself out of the snow** at no extra cost.
`TerrainComp.SetSnowMask` shows the base depth comes from the Deep North's `GetBiomeHeight`
mask. The Mountain's snow is the biome's texture, not a channel. Whether dirt or paving paint
shows through it the way a hoed path does is a **verify** (expected: yes).

## 1. The search: what makes it look like a path

A least-cost search (A*) over a grid of 4 m cells, with **16 neighbours** (the 8 plus the
knight's moves) so that straight stretches are not stuck at 45° steps. The cost of a step is its
length times:

| Factor | Rule | Why |
| --- | --- | --- |
| slope | `1 + k · (grade / g0)²`, with grade = height difference / step length; above a max grade (about 0.6, 31°) ×20 | Climbing is expensive and gets worse fast. Mountains end up in switchbacks without any code for them. |
| water | height below water level (`ZoneSystem.m_waterLevel`, 30): shallow (up to 1.5 m deep) ×3, deep ×2 per metre plus a one-off **landfall cost** of about 150 m for every land→water step | Rivers get forded at their narrowest point. The sea is crossed only when walking around costs more, and then the path ends at the shore and resumes on the far one. |
| biome | Swamp ×1.5, Mistlands ×4, Ashlands and lava forbidden (config) | Real paths skirt bogs. See section 4. |
| locations | inside any location's `m_exteriorRadius`, except the target: ×10 | Keeps the path out of villages and crypt yards, and off terrain their `TerrainModifier` has flattened. |
| existing path | ×0.3 | Paths merge and share their trunk instead of running side by side. |
| wander | × (1 + 0.3 · Perlin noise at 150 m), seeded by the world seed | On flat ground every line costs the same. The noise gives them long, lazy bends, the same for everyone on the world. |

**The network is free.** The search starts from *every* network point at cost 0: the start
temple, each base, and a point every 20 m along each path already laid. It stops at the altar,
and the cheapest connection falls out on its own, whether that is the base, the stones, or a
branch off the path to the last boss. Then the grid path is smoothed (Chaikin, two passes) into a
polyline with a point every 2 m.

**Search size.** A route of 3 km, searched inside an ellipse around the straight line, visits on
the order of 100 000-300 000 cells, each one height sample (cached, a neighbour's sample is
reused). At a few µs per sample that is under a second of work, spread over frames with a budget
of 5 ms (10 ms on a dedicated server, which renders nothing). If `paths bench` says otherwise:
search at 16 m first, then again at 4 m inside a 64 m corridor around the coarse result.

**Buildings (built):** before the search, every ZDO in its ellipse whose prefab has a `Piece` or
`WearNTear` - players' pieces, ruins, village walls, earlier landing barrels; not dungeon
interiors (y above 1000) - goes into a bucketed point set (`Structures`). A cell within
sqrt(k² + (knight step / 2)²) of one costs 40 times more, k being the writer's reach (half width
with wobble, plus the 1.5 m shoulder) plus 2 m for the piece itself: the smoothed trail runs along
the steps between cell centres, so that radius keeps the whole painted and levelled strip off
the piece. High, not impassable - a start inside a base has to get out - and where the path is
forced through anyway, the writer paints nothing within 2 m of a piece and fades the levelling
out over the 1.5 m beyond that.

**The search cannot see** player digging, trees and rocks in generated zones (they are cleared
instead), and the Mistlands' rock formations (section 4).

## 2. Laying it

Per zone the path crosses, once, in one batch: take the path points within reach, and for every
vertex within the half width (1.75 m, config; the width drifts ±0.5 m over some 40 m of trail,
slow world noise offset by the seed, config `WidthVariation`) paint **dirt** with a soft edge. The weight is
`1 - distance/halfWidth` raised to a small power, lerping r, g, b toward `(1, 0, 0)` exactly as
`PaintCleared` does, and alpha stays the base mask's. Then write the blob. Rules:

- **Never over the player's work.** Leave vertices whose paint is already paved or cultivated
  (outside the Deep North), and vertices whose height the player changed
  (`modifiedHeight` already set).
- **Not in water:** no paint below the water level plus 0.3 m. The path visibly ends at the
  shore.
- **Zone borders:** an edge vertex belongs to two zones' blobs. `PaintCleared` copies edge values
  to the neighbour (`spread`), so write both, or there is a seam.
- **Vegetation (built):** trees and logs, and rocks and bushes whose drops are only wood, stone,
  flint or resin, from `ZoneSystem.m_vegetation`. A tree goes if its trunk is within 1 m of the
  path's edge; a rock or bush if it reaches onto the path at all - its reach measured once per
  prefab from its meshes' bounds (the longer of x and z, in the prefab's unscaled space) times
  the scale its ZDO was placed at, so a boulder the path runs through goes however big it is.
  Ore, nests, spawners and pickables stay, and so does anything in a location or within 10 m of a
  player-built piece (a grown sapling is the same prefab as a wild tree). No setting yet.
- **Landings (built):** where the trail enters a stretch of water with a point deeper than a
  ford (1.5 m), a `piece_chest_barrel` stands on the last dry point, and another on the first dry
  point across, facing the water - unless a structure already stands within 4 m. No creator. It
  stands in for a dock; being a piece, later paths walk around it. Its height is the generated
  ground plus the levelling delta, which may be a few cm off the built heightmap.
- Throttle the writes: a few zones per frame. Each is a decompress-merge-compress of at most a
  few KB.

**Levelling** (config, default on and gentle):

- Take the height profile along the path from the base heights, smoothed over about 10 m.
- Every vertex within the half width gets `levelDelta = profile - base`, blended into 0 over a
  shoulder of 1.5 m.
- Clamp the cut or fill to 1 m (config). That stays far inside the game's ±8 m.

The result is a path you can walk without hopping, with a slight bank where it cuts across a
slope. The cost is the same blob as the paint. The risk is looks: on steep ground a 1 m cut makes
a small wall on the uphill side. That is the in-game test.

**Later, paving:** stretches used often (count players on path zones, as AntTrails does) turn
to `Paved`.

## 3. The network and the base marker

A path starts from the network: the start temple, each **base**, and every path already laid. The
player decides where a base is by placing something. Options:

| Marker | For | Against |
| --- | --- | --- |
| **The ward** (`guard_stone`, recommended to start) | Vanilla, placed exactly where people live. The server sees every ward ZDO. Clients need no mod. | People also ward outposts and portal huts. Wards within 150 m count as one base, and a config setting can require a minimum of pieces nearby. |
| A named sign (`sign`, text starting with a keyword) | Vanilla, deliberate, and names the base for later signposts | Needs text typing. Reads as a hack. |
| A new piece, the *Waystone* (cloned from a vanilla prefab, like Odin's Compass's items) | Clearest to players, can carry its own look and hover text | Every client needs the mod, or the piece is an unknown prefab to them. That undoes "server side only". |

**Open decision, recommended:** start with the ward, and keep `BaseMarker` (a prefab name) in the
config so a server can switch to another vanilla piece. The Waystone comes later, if the mod ever
wants a client side anyway.

A new base joins the network on the next sleep: a path from it to the nearest network point,
with the same search.

**Storage.** The queue of revealed altars and the polylines of laid paths (for "existing path"
costs, merging and network points) are world data. They go on one data ZDO of the mod's own,
placed out of any player's reach (**verify** that the server keeps and saves a ZDO whose prefab
no client knows, as `ValheimServersideQoL` relies on), or else in a file next to the world save.
A 3 km path at a point every 8 m is about 3 KB.

## 4. Biomes: what to expect and test

| Biome | Expectation |
| --- | --- |
| Meadows, Black Forest | The easy case. Forests are dense: vegetation clearing matters here most. |
| Swamp | Low and wet. Much of it is at or just under water level, so the path fords and breaks. Bonemass sits in the swamp's heart. Expect gaps. |
| Mountain | **Needs testing.** Moder's altar is on a peak. The slope cost should make switchbacks. The questions are whether the max grade finds any way up at all (fall back to a steeper grade on the last 200 m), whether levelling cuts look bad on steep flanks, and whether dirt shows through the snow texture. |
| Plains | Easy, flat, open. Fuling villages are locations, so the path steers clear. |
| Mistlands | **Likely not in the first version.** The base heights are terraced (`GetMistlandsHeight` quantises to 1/400 of the height multiplier of 200, that is 0.5 m steps). The slope cost can handle that. The real walls are the giant rock formations, which are vegetation: invisible to the search in generated zones unless it reads their ZDOs as obstacles, and kept off the path by the clear areas in ungenerated ones. That second part might just work. Default: high cost, the path to the Queen's entrance is config opt in, and the mod page says it is experimental. |
| Ashlands | Off. Reached across the sea, lava in the vegetation mask (paint keeps alpha, so a path cannot cover lava). Fader's path would stop at the shore facing it, which is fine. |
| Deep North | No boss altar in this build (**verify**). Paths from bases there dig out the snow, see section 0. |
| Ocean | The landfall cost decides. Test a world where Eikthyr's altar is on another island. |

## 5. Build order

1. **Facts in game** - done for the Meadows (see section 0). Still open: the snow channel in the
   Deep North and on the Mountain (`paths facts` standing in each).
2. **`paths lay <x> <z>` / `paths lay <location>`** - built, not yet played. Searches from the
   player, writes the result straight into the zones' terrain data with levelling on, drops
   unsaved map pins every 50 m, and `paths undo` restores what the last lay replaced. What to
   look at: how the path looks and feels to walk, the numbers it prints (search time, zones),
   seams at zone borders, a mountain climb, a sea crossing. The costs in section 1 are
   constants in `PathSearch.cs`, to tune from what the test shows.
3. **Triggers and queue:** the vegvisir postfix, the sleep postfix, the data ZDO.
4. **Network:** start temple, wards, existing paths as start points and discounts.
5. **Levelling**, **vegetation**, **buildings** and **landings** - built with the lay; still to
   do: a setting for the clearing, and checking it in game (Black Forest density, a boulder on
   the path, a lay out of a base, the barrels' height at the shore).
6. Mod page, translations (the mod speaks only in the log so far), release.

## 6. Later

- A dock in place of each landing barrel.
- Smaller side paths from a path to the entrances of places it passes - sunken crypts, stone
  towers, villages close by (a narrower width, the location's position as the goal and the
  nearest trail point as the start).
- Signposts where paths meet (a vanilla sign piece, text = the base or boss it leads to).
- Paths to other things: the trader, found dungeons, portals.
- Stone paving from use, as AntTrails does (its Thunderstore page describes the mechanics).
- A path to the next boss as soon as the last one falls (`defeated_*` global keys), for worlds
  where nobody reads the stones.

# Bugs

None, nothing is built. Risks to watch:

- A compiler created by the server in a zone a client is *generating* at the same moment
  (`SpawnZone` is server side, so it should be serialised on the main thread). **Verify** that no
  vanilla path creates a compiler while generating.
- In a zone generated *after* its path was written, vegetation is placed on the unlevelled
  ground: what stands on the path is cleared, but one on the levelling shoulder (up to 1.5 m past
  the edge) can float or sink a little.
- `paths undo` restores the terrain and removes the barrels, not the cleared trees and rocks.
- A landing barrel is placed at the height the path should have there, computed from the world
  generator; if the built ground differs, it floats or sinks a little.
- A rock without meshes on its prefab counts as 2.5 m wide; a MineRock5 whose fragments sit
  far from its pivot is measured by its bounds, so it may go a little early.
- A player digging in a zone while the server rewrites it: last writer wins, and one of the two
  edits is lost. Rare, since it only happens during sleep.
