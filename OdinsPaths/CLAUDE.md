# OdinsPaths

Paths that grow across the world like real footpaths: a vegvisir reveals a boss altar, the next
sleep lays a trail to it from the nearest point of the network. See the root `CLAUDE.md` for the
shared build, the scripts and the environment.

**State:** the search (around buildings), the shaping, the terrain writing, the vegetation clearing and the landing barrels are built and reached only through
the dev command `paths lay` (not yet played). No triggers, no network, nothing a player without
devcommands can see.
`ROADMAP.md` is the plan and the feasibility study, with every game fact it rests on and which of
them still need checking in game. Nothing is released — `VERSION` stays 0.1.0 until the first
Thunderstore upload. `package/icon.png` is a generated placeholder.

| Path | What |
| --- | --- |
| `src/OdinsPaths.cs` | BepInEx entry point and settings: path width (3.5 m, drifting ±0.5 m along the path), levelling and its max cut, the search's per-frame budget. |
| `src/PathSearch.cs` | A* over 4 m cells from world generator heights, 16 neighbours; the step costs (slope, fords, sea, biomes, locations, wander) are constants at the top. A coroutine. |
| `src/Trail.cs` | A found route shaped for laying: Chaikin smoothed, a point every 2 m, water flags, the levelling profile. |
| `src/TerrainWriter.cs` | Writes a trail into each zone's `TerrainComp` blob (merged, or on a new compiler ZDO): dirt paint and levelling. `TerrainData` is `TerrainComp`'s save format. |
| `src/Structures.cs` | Every built piece in the search area (players', ruins', landing barrels), bucketed: the search goes around them, the writer leaves the ground beside them alone. |
| `src/Landings.cs` | A barrel on the last dry point before a sea crossing and the first one after it - a dock later. |
| `src/Clearing.cs` | Removes generated trees and rocks from a path, rocks measured from their meshes: after a lay in existing zones, and in a zone the server generates later (read back from the dirt in its terrain data). The `SpawnZone` patch. |
| `src/PathLayer.cs` | Search → trail → write → clear → landings for one path, with the locations to avoid. What the sleep trigger will call. |
| `src/Dev/PathCommands.cs` | `paths facts`, `paths bench [cells]`, `paths lay <x> <z>` or `paths lay <location>`, `paths undo`, `paths clearpins`. Debug builds only. |
| `ROADMAP.md` | The plan, section by section, with the build order in section 5. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## The rules that always apply

- **Server side only.** The search, the queue and every terrain write run where
  `ZNet.instance.IsServer()` — the dedicated server, or the host of a local game. Clients need
  nothing and must not write terrain for the mod: only the server sees every ZDO, so only it
  can tell whether a zone already has a terrain compiler (a second one gets destroyed, and a
  player's digging with it).
- **A path is vanilla terrain data** (`ZDOVars.s_TCData` on the zone's `TerrainComp` ZDO), so it
  outlives the mod and every client sees it. The mod's own data (queue, laid polylines) may
  vanish with the mod.
- **Never over the player's work:** a vertex the player raised, lowered, paved or cultivated is
  left alone.
- **Heights for the search come from `WorldGenerator`, on the main thread only** — its river cache
  is unlocked and shared with the `HeightmapBuilder` thread. Budget the work per frame.
- Null-guard everything: `Player.m_localPlayer` is null on a dedicated server, always.

## Verified in game (2026-09-24, `paths facts` / `paths bench`, local game)

- The zone heightmap is **64 wide at scale 1**: `TerrainComp` arrays are 65 × 65, vertex `x` sits
  at `zoneCenter.x - 32 + x`. The compiler prefab is **`_TerrainCompiler`**, placed at the zone
  centre with y 0. Water level 30.
- `WorldGenerator.GetHeight` costs **1.5 µs** per sample. `RequestTerrainSync` for one zone takes
  ~20 ms, mostly waiting on the builder thread - use `RequestTerrain` and poll instead.
