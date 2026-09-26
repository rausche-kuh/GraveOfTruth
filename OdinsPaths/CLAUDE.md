# OdinsPaths

A road network that grows across the world: paved main roads from the sacrificial stones and
the bases to the boss altars (in progression order) and the traders, forking off each other, and
dirt spurs from them to the villages, crypts and camps nearby (`docs/network.md`). See the root
`CLAUDE.md` for the shared build, the scripts and the environment.

**State:** the search (around buildings), the shaping, the terrain writing, the vegetation
clearing and the landing marks are built and have been tried in game through `paths lay`. The
road kinds, the network and its storage, the planner, the spurs and the triggers were built on
2026-09-25; the first start-up growth ran in game the same day (it worked, and stuttered). The
harbour stones that replaced the landing barrels (2026-09-26) have not run in game yet.
A Release build grows roads by itself on the server 10 s after loading and after every sleep,
unless `[Network] Grow` is off; a **Debug build holds that** until `paths auto on`
(`Grower.Held`), so a test world changes only through the dev commands.
`ROADMAP.md` is the plan (build order, the rework, what comes later, bugs); `docs/` is the design
and the feasibility study, with every game fact it rests on and which of them still need checking
in game. Nothing is released — `VERSION` stays 0.1.0 until the first
Thunderstore upload. `package/icon.png` is a generated placeholder.

| Path | What |
| --- | --- |
| `src/OdinsPaths.cs` | BepInEx entry point and settings: the network (`Grow`, `Announce`, `ProgressBar`, `Directness` = alpha 0.4, `Progression` - with `|` groups, `*n` counts, `~` side roads and `@` near-preferences: the Forge of Potential with Moder, three infected mines before the Queen, two charred fortresses before Fader, and after the Frozen King's altar side roads to the Winding Tunnels (`TheHole01`), two villages beside them and Mörkhalla (`MorkBorg`) -, `Style` (Explore / Fastest / Custom) with the `Central` (medoid instance) and `Remote` (farthest from the roads, planned last) lists, `Traders`, `CustomLocations`, the base marker, radius and minimum pieces), the spurs' `Prize` and `PointsOfInterest`, the route costs (sea 2 per metre, boarding 1000 m, landing 400 m, swamp 3 per metre), `SearchThread` (on), the per-frame budget of the writing, the clearing and an unthreaded search, `AdaptivePasses` (on: the first pass by distance - 4 m alone up to 600 m, 16 m up to 1.5 km, 32 m up to 4 km, 64 m + 16 m beyond), else the coarse cell (32 m, up to 128), the optional middle pass (off; its cell and 256 m corridor) and the fine corridor width (96 m). |
| `src/PathSearch.cs` | A* over square cells from built-ground heights (`Ground`), 16 neighbours, on a thread of its own (`SearchThread`; the locations bucketed in a `LocationGrid`, every cell's ground and search state in one `CellMap`), from many starts to the cheapest of many goals; the step costs (slope, fords, rivers swum, biomes, locations, wander, and at the fine cell a squared turn cost against the last 8 m, `Heading`) are constants at the top; the sea's cost per metre, the boarding and landing lump sums (the search runs the way the player walks, so each falls on its shore; a spur pays 15% of both) and the swamp's factor are settings, read at each search (deep water in the Ocean biome is sea, and deeper than any river - 6.5 m - anywhere, `IsSea`; the rest a river). Cells of 16 m and wider are sampled at five points and water counts by its share. Guided by a 128 m `Survey` run backwards from the goals (the cost to the goal, sea and biomes included, as the heuristic). The cell size and a bounds rule are the caller's; each `Start` carries a starting cost (a network point's share of its route to the hub). A coroutine that waits on its thread, or steps within the frame budget with the thread off; reports wall and work time. |
| `src/Entries.cs` | Ways into the Mistlands: per goal in them, the nearest ground outside them (or a beach) in eight directions; the road is searched to one and goes on in a small search of its own. `Worker`: a thread waited for frame by frame, for the search and the entries. |
| `src/Corridor.cs` | The strip around a coarse route the fine pass may search: bucketed route samples, one distance query per cell. |
| `src/Ground.cs` | The ground height as `HeightmapBuilder` builds it (the zone's corner biomes blended), not `GetHeight`'s; the search, the trail and the landings use it. |
| `src/RoadKind.cs` | The two kinds of road, `Main` (paved, 5 m ±1, hard edge, 1 m cut, up to 4 m on steep ground toward a 22% grade, flat to the paint's widest with a sharp edge, paint below 40-55% slope) and `Spur` (dirt, 3.5 m ±0.75, soft edge, 0.4 m cut, paint below 70-90%; old defaults upgraded): paint, width and its drift, edge, levelling. Their settings are the `[MainRoads]` and `[Spurs]` sections; the steep-ground values are the kind's own. |
| `src/Trail.cs` | A found route shaped for laying: Chaikin smoothed, a point every 2 m, water flags, causeway flags (swamp down to 1.5 m under the water, raised to 0.4 m above it), the levelling profile (a main road's cut and filled toward its grade, `LimitGrade`) with a flat landing at each hairpin on steep ground (`Bends`), the cut allowed per point (`FindCuts`; none in the Mistlands, `Built`), the half width two close legs leave each other (`LegRoom`, `HalfWidthAt`), dirt in the Mistlands (`PaintAt`), and its `RoadKind`. |
| `src/TerrainWriter.cs` | Writes a trail into each zone's `TerrainComp` blob (merged, or on a new compiler ZDO): the kind's levelling (a main road flat to the paint's widest with a sharp edge; a hairpin's legs blended outside their flats), then its paint (none on ground steeper than the kind allows; a main road too steep for stone gets dirt up to 53°); no levelling within 10 m of the sacrificial stones; never over paint that is already stone or cultivated, so a spur stops at its main road. `TerrainData` is `TerrainComp`'s save format. |
| `src/Network.cs` | The laid network: roads (kind, target, a point every 8 m with its cost from the hub), pinned instances, connected points of interest, bases, and groups found unreachable, stored on a data ZDO at (-15000, -15000). `Starts` gives the points a new road may set out from (alpha × hub cost). |
| `src/Planner.cs` | What is due, in order (progression up to the second undefeated boss - entries sharing a key count once -, several roads for a `*n` entry, side roads (dirt, from the network at cost 0) for a `~` one, the instances near its `@` locations where there are any, the medoid alone for a `Central` group, the traders - to camps not generated yet, re-planned if the game drops the pinned one -, custom locations, bases without a road), each job's starts, and `Run`: a job laid, recorded, and followed by its spurs. |
| `src/Progress.cs` | What a growth is doing: the host's progress bar (IMGUI, road n of m, stage, %), the message-log lines to every player (vanilla `ShowMessage`), the game's names for the main locations, and the frame watch (each road's slowest frame and its stage, for the log). |
| `src/Grower.cs` | The triggers: the growth once the world is up, the `EnvMan.SkipToMorning` postfix, the vegvisir answering with the pinned instance (and a harbour stone with the harbours across), and the `Busy` flag shared with the dev commands. |
| `src/Structures.cs` | Every built piece in the search area (players', ruins', harbour stones and posts) and every rock, ore and root standing in the way (`Clearing.Obstacles`: ore and the like, a disc of points each), gathered on the worker thread from a copy of the ZDO list, bucketed: the search goes around them, the writer leaves the ground beside them alone. |
| `src/Lamps.cs` | A road post every 48 m along a road's Mistlands stretches, as `Mistlands_RoadPost1` has it: a `blackmarble_post01` with a `dverger_demister` (clears the mist) 3.5 m up on it. |
| `src/Footprints.cs` | How far a location's buildings really reach (its prefab's meshes, measured once, by reflection past the unreferenced `SoftReference`), at least its exterior radius and at most 48 m: the search's circles and the clearing keep to it. Every location the world has instances of is loaded asynchronously once the world is up (`Tick`, four at a time; a synchronous load stalled one frame 5.9 s on 2026-09-26), a lay waits for it (`Wait`), and the reaches are kept per game version in `BepInEx/cache/OdinsPaths.footprints.txt`. |
| `src/Landings.cs` | The landings: the last dry point before a sea crossing (sea - the Ocean biome, or deeper than any river, `PathSearch.IsSea` - over 40 m or more, or any water over 100 m) and the first one after it, each with its crossing; a main road's get harbour stones (`Harbours`), a spur's a minor harbour, a `wood_pole_log_4`. Rivers and shorter water are swum and get none. Everything the mod places is marked `OdinsPaths_Placed` (`Spawn`). |
| `src/Harbours.cs` | The harbour stones: the start temple's vegvisir copied as a network prefab of the mod's own (`OdinsPaths_Harbour`, a `ZNetView`, blue runes and light), registered after `ZoneSystem.Start` on the server and every client with the mod; one per landing of a main road, shared by landings within 25 m, each ZDO holding the positions of the harbours across (both ways); the server's answer when one is used (the stone itself, silently if already pinned, then every linked stone still standing, as `Harbour` pins). |
| `src/Clearing.cs` | Removes generated trees, plain rocks and unbreakable scenery (cliffs, the Mistlands' rocks, giant roots) from a path, rocks measured as their meshes' boxes, turned and scaled as placed: after a lay in existing zones, and in a zone the server generates later (read back from the dirt and stone in its terrain data). The `SpawnZone` patch. |
| `src/PathLayer.cs` | Search passes (each in a corridor around the line before it) → trail → write → clear → landings for one road, with the locations to avoid, and the road as a `Network.Road`; `Options` lists the passes (`Pass` = cell and corridor; the settings give coarse, optional middle, fine), the kind and whether to write at all. `LaySpurs`: the one collecting search along a main road and a spur to every point of interest under the prize. A road to a location ends at its measured footprint (`Footprints`, never under the exterior radius), a slope-turned dungeon entrance's at its exterior radius on its downhill side (`EndAtEdge`, `Approach`); spurs get landings too. |
| `src/Dev/PathPreview.cs` | `paths preview [all] [full] [spacing]` (every road due - `all`: every boss of the progression and every trader, the network as it ends up -, planned one after another on a copy of the network with its spurs, coarse by default - the coarse pass alone, spurs at 16 m -, nothing written, pinned as it goes: instances at a job's start, the road when found, spurs, and a pin following the search front), `paths show [spacing]` (the saved network as pins), `paths auto [on\|off]`; the pins, shared with `paths grow`/`lay`/`search` (red target, orange point of interest with a spur, grey looked at and passed, white hub, a pale orb every 300 m of a main road's land, an orange orb where a spur forks off, blue portals for landings, nothing over water), re-tinted in a `Minimap.UpdatePins` postfix; what each pin is as an IMGUI tooltip on the large map (`PinTips`). Debug builds only. |
| `src/Dev/PathCommands.cs` | `paths facts`, `paths bench [cells]`, `paths where <location>` (its instances by distance), `paths costs [<name> <value> ...]` (show or set the route costs: sea, boarding, landing, swamp), `paths search <target> [pass ...] [main\|spur] [solo]` (search and pin, write nothing), `paths lay <target> [pass ...] [main\|spur] [solo]` (from the player and the network, unless `solo`; added to the network), `paths spurs` (those of the newest main road), `paths plan` (what is due, the first searched), `paths grow [n\|all]` (lay the next n due; `all`: the whole network, every boss, as `preview all` shows it, with the total time), `paths network`, `paths forget` (empty the network, keep the terrain), `paths undo` (the last lay or grow job with its spurs: terrain, harbour stones, posts, lamps, network), `paths reset [confirm]` (every zone's terrain data emptied - players' digging too -, every harbour stone, post, lamp and old barrel of the mod removed, the network forgotten; the cleared trees stay gone), `paths clearpins`; a target is `<x> <z>` or a location name (every instance a goal, the cheapest taken), a pass `cell` or `cell:corridor` (one alone is the coarse cell before the settings' fine pass, 0 = none; `64 16:256 4` is three passes). Debug builds only. |
| `ROADMAP.md` | The plan: a table of the docs, the rework, the build order, what comes later, bugs. |
| `docs/` | The design, one file each: `foundations.md` (terrain data, triggers, heights, snow), `search.md` (costs, passes, buildings), `laying.md` (paint, levelling, clearing, landings), `network.md` (the two tiers, traders, bases, storage), `biomes.md`, `prior-art.md` (Procedural Roads and `PLAN.md` weighed, why no Jötunn). |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## The rules that always apply

- **Server side only.** The search, the queue and every terrain write run where
  `ZNet.instance.IsServer()` — the dedicated server, or the host of a local game. Clients must
  not write terrain for the mod: only the server sees every ZDO, so only it can tell whether a
  zone already has a terrain compiler (a second one gets destroyed, and a player's digging with
  it). The one thing a client needs the mod for is **seeing the harbour stones**
  (`Harbours`, a prefab of the mod's own); everything else works on a vanilla client.
- **A path is vanilla terrain data** (`ZDOVars.s_TCData` on the zone's `TerrainComp` ZDO), so it
  outlives the mod and every client sees it. The mod's own data (queue, laid polylines) may
  vanish with the mod.
- **Never over the player's work:** a vertex the player raised, lowered, paved or cultivated is
  left alone.
- **Heights for the search come from `WorldGenerator`**, which the search reads on a thread of its
  own, as the game's `HeightmapBuilder` does (its river cache is locked). Everything else the search
  touches is built on the main thread before it starts and only read after; Unity objects stay
  on the main thread (`Ground.Prepare`). The writing and the clearing are main thread work,
  budgeted per frame.
- Null-guard everything: `Player.m_localPlayer` is null on a dedicated server, always.

## Verified in game (2026-09-24, `paths facts` / `paths bench`, local game)

- The zone heightmap is **64 wide at scale 1**: `TerrainComp` arrays are 65 × 65, vertex `x` sits
  at `zoneCenter.x - 32 + x`. The compiler prefab is **`_TerrainCompiler`**, placed at the zone
  centre with y 0. Water level 30.
- `WorldGenerator.GetHeight` costs **1.5 µs** per sample. `RequestTerrainSync` for one zone takes
  ~20 ms, mostly waiting on the builder thread - use `RequestTerrain` and poll instead.
- Search wall times at the 6 ms budget, one sample per cell (2026-09-24): to Yagluth 1 s at 64 m,
  4 s at 32 m, 16 s at 16 m, plus 6 s for the 4 m pass in its corridor; 32 m is good, 64 m alone
  hops into water. With five-point sampling `128 32:512 8:128` gave a good line to Yagluth 4.5 km
  out, and sea 2 with 700 m each way felt right. The numbers behind the passes in `docs/search.md`.
