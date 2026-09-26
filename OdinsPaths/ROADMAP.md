# Roadmap

Odin's Paths grows a road network across the world. Paved main roads run from the sacrificial
stones and the players' bases to the boss altars in progression order and to the traders, each
from wherever the network already goes, so the roads share trunks and fork. Dirt spurs branch
off them to the villages, crypts and camps close by. The roads follow the easiest ground, wind
around hills, end at a shore and pick up again on the far one, and they are the game's own
terrain data - server side, seen by every player. Nothing is released yet. This file says in
which order to build it and what is still open; the design itself, with every game fact it rests
on, lives in `docs/`.

Game facts without a mark were read in `decompiled/assembly_valheim/` on 2026-09-24. Facts marked
**verify**, here and in `docs/`, come from memory of the game or from reasoning, and still need
checking in game before code rests on them. The `paths facts` and `paths bench` dev commands
(`src/Dev/PathCommands.cs`, `deploy -c Debug`) exist for the first of those checks. What a check
shows goes into `CLAUDE.md`.

## The docs

| Doc                   | What it holds                                                                                                                                                                                                                                                                                                                                                        |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `docs/foundations.md` | Why the server can write a path as terrain data without loading a zone: the `TerrainComp` blob and its format, what a path costs in save size and zone loads, why heights come from `WorldGenerator` on the main thread, the two vanilla triggers (vegvisir, sleep), the start temple, clearing vegetation at zone generation, and snow.                             |
| `docs/search.md`      | The A\* search: the step cost table (slope, water and sea with the landing cost, biome, locations, wander), the one search from every network point to every instance of a location with the alpha knob for reuse, the spur search, the coarse → middle → fine passes with the times measured on 2026-09-24, the building avoidance, and what the search cannot see. |
| `docs/laying.md`      | Writing a trail into the zones: stone for main roads and dirt for spurs, the soft edge, the width drift, never over the player's work, no paint in water, zone border seams, which vegetation is cleared, the harbour stones, the levelling profile and its 1 m clamp.                                                                                              |
| `docs/network.md`     | The two tiers: main roads (hubs, the progression list, traders (to camps not generated yet), custom locations; reuse by alpha) and spurs (points of interest under a prize), when the network grows, bases marked by a ward, and the data ZDO.                                                                                                                       |
| `docs/biomes.md`      | What to expect from each biome, and what still needs testing: the Mountain climb, the Mistlands' rock formations, Ashlands off, snow in the Deep North, sea crossings.                                                                                                                                                                                               |
| `docs/prior-art.md`   | Procedural Roads and `PLAN.md` weighed against this mod (2026-09-25): why Jötunn is not needed, the built-ground height and the many-goal search taken from them, what was left and why, and what was decided from it.                                                                                                                                               |

## The design in short (2026-09-24, two tiers 2026-09-25)

From one path per vegvisir to a **network** (`docs/network.md`):

- **Main roads, stone.** Hubs: the sacrificial stones and every base (a cluster of wards). Main
  locations, one road per group, in a **progression list** (config; default Eikthyr to Yagluth,
  the Queen and Fader opt in), plus the traders (since 2026-09-25 to a camp not generated yet, `docs/network.md`), plus a config list
  of custom main locations. Each group's instance is the one cheapest to reach from the network
  as it stands (the many-goal search, built), and it is pinned; vegvisirs are overridden to
  reveal it. A road may start from any point of an earlier one at `alpha ×` that point's cost to
  its hub: roads share trunks and fork, and alpha 1 gives every location a road of its own.
- **Spurs, dirt.** After each main road, every point of interest in a config list (draugr
  villages, crypts, burial chambers, troll caves, fuling villages, towers...) whose walk from the
  road costs less than its prize gets a narrow dirt spur to its edge. One search per main road
  finds them all.
- **When:** at server start up

## Build order

1. **Search and lay, one path** - done: `paths search` / `paths lay` with passes, costs tested
   2026-09-24 (sea 2, 1400 m per crossing, swamp 3, `128 32:512 8:128`); built-ground heights
   and the many-goal search 2026-09-25. Still to look at in game: `paths bench` near a biome
   border (Ground against the built zone), `paths search GDKing` choosing among altars, Moder and
   the hitch on a steep mountain stretch, the snow channel in the Deep North and on the Mountain.
   Steps 2 to 6 were built on 2026-09-25 and compile, but none of them has run in game yet. What
   each one still has to show is listed with it.

2. **Road kinds in the writer** - built (`src/RoadKind.cs`): a `RoadKind` per trail with its
   paint (main roads paved `(0, 0, 1)`, spurs dirt), width and drift, edge softness (0.3 m
   against 0.8 m) and levelling with its max cut, settings in `[MainRoads]` and `[Spurs]`
   (4 m / ±0.3 m / 1 m cut, and 2.5 m / ±0.5 m / 0.4 m cut). New zones count paved texels as road
   when they clear. `paths lay <target> spur` lays dirt, and `main` (the default) lays stone. **Look at:**
   stone in each biome (the Plains' grass and the Mountain's snow first), the kerb at 0.3 m, and
   a spur's dirt stopping at the stone.
3. **The network model and its storage** - built (`src/Network.cs`): roads with their kind,
   target and a point every 8 m carrying its cost from the hub, pinned instances, connected
   points of interest, bases, and groups found unreachable, compressed on one data ZDO at
   (-15000, -15000). That position is inside the ±16 km sector grid, which a position 50 km out
   is not: such a ZDO would land in sector 0. `paths lay` starts from the player at 0 and from
   every 20 m of the main roads at `Directness` (alpha, 0.4) × their hub cost, adds the road to
   the network, and `paths undo` takes it out again; `paths network` lists it, and `paths forget`
   empties it. The search draws its ellipses, and gathers locations and buildings, around a
   thinned set of starts (`PathSearch.Anchors`: the cheapest start per 100 m square, and of those
   only the ones that could still win). **Verify:** the data ZDO survives a save and a reload (and the
   log shows one "unknown prefabs" warning, not a removal), and a second lay forks off the first.
4. **The planner** - built (`src/Planner.cs`): `Progression` (location:key pairs, Eikthyr to
   Yagluth, up to the second undefeated boss), `Traders` once their instance is placed,
   `CustomLocations`, then every base (wards within `BaseRadius` 150 m of each other, at least
   `BaseMinPieces` 20 built pieces within 30 m) that has no road yet. A base's road is searched
   from the stones and the main roads at cost 0 toward the base, and its costs are then measured
   from the base, which becomes a hub. `paths plan` lists what is due and searches the first
   without writing; `paths grow [n]` lays the next n. A group with no way to it is stored as
   unreachable and skipped. Not done: the shared coarse terrain cache (`docs/prior-art.md`),
   which should wait until the times of a whole start-up growth are known.
5. **Spurs** - built (`PathLayer.LaySpurs`, `PathSearch.Collect`): one plain Dijkstra from
   every point of the new main road at cost 0, inside a corridor `Prize / 0.7` wide either
   side, until the open cost passes `Prize` (400); every point of interest settled by then gets a
   dirt spur to a point 1 m outside its exterior radius on the road's side, and sea cells are
   impassable for it. `PointsOfInterest` is the list. The planner lays a road's spurs before
   the next road; `paths spurs` lays those of the newest main road. **Verify:** the default
   point-of-interest names (a location dump), how many spurs a road gets at 400, and the search
   time along a long road.
6. **Triggers** - built (`src/Grower.cs`): 10 s after the world is up on the server,
   everything due; on `EnvMan.SkipToMorning` (everyone in bed), whatever became due since.
   Boss kills, a placed trader and a new ward need no hooks of their own, because the planner reads
   them at every sleep. The vegvisir override is a prefix on `Game.RPC_DiscoverClosestLocation`: for a
   pinned group it answers with the pinned instance. `[Network] Grow` turns it off; a test
   world grows on its own now unless it is off. **Verify:** a whole start-up growth on a
   fresh world (time, hitches: `Planner.Due` walks every ZDO twice per job for the wards),
   the growth after a sleep, and a vegvisir pinning the pinned altar.
   The first start-up growth in game (2026-09-25, local game) laid six roads and 33 spurs in
   about two minutes, and the game **stuttered badly** throughout. Since then (`src/Progress.cs`):
   the terrain writing and the clearing keep to `SearchBudgetMs` per frame as the search does (a
   second zone per frame only while it lasts), the search looks at the clock every 32 cells
   instead of every 256 and while seeding its starts, the host sees a progress bar at the top of
   the screen (road n of m, the stage, its percentage), and every player gets message-log lines
   at the start, per road and at the end (vanilla `ShowMessage`, `[Network] Announce` /
   `ProgressBar`). Each laid road's log line now ends with its slowest frame and the stage it fell
   in, and how many frames were over 50 ms. **Verify:** that the stutter is gone or at least
   smaller, and **read those log lines** - they say which stage to work on next (the writing,
   which makes a loaded zone rebuild its mesh and collider, is the likeliest; the whole-ZDO
   walks in `Structures.Around`, `Planner.Bases` and `Network.FindData` are single frames); the
   bar's look and position (IMGUI, 440 px at 1080p, 64 px from the top); the translated names
   in the messages (the `$enemy_*` names are the bosses' own, read from the game; `$npc_haldor` / `hildir` / `bogwitch` are guesses - the prefab name
   shows when one is wrong).
   To check the planning without the stutter, a Debug build now holds the automatic growth
   (`paths auto on` lets it go) and `paths preview` plans every due road and its spurs coarsely
   on a copy of the network and pins targets, points of interest, the lines, water and landings
   (`src/Dev/PathPreview.cs`); `paths show` pins what is laid. **Verify** that the preview's
   choices match what a full growth then lays (the coarse line can pick another instance or fork).
   `paths preview all` plans every boss and trader as if all were due.
   **Late biomes** (2026-09-25): the default progression goes on to the Queen, Fader and the Frozen
   King (`DN_Bossroom`), names and keys read out of the game's bundles (the vegvisirs' location
   names, the bosses' `m_defeatSetGlobalKey`); a config still holding the first default is
   upgraded on load. The Ashlands are passable except for lava (`docs/biomes.md`).
   **Spurs that missed** a point of interest the road passed (seen in preview and a growth): a
   village's or crypt's own pieces stand up to its edge and made every cell of the approach cost
   ×40, and at 16 m the goal cell's centre could fall inside the location's ×10 circle. A spur's
   search now drops the structures within 12 m past its points of interest (the writer keeps
   them) and shrinks their circles by three quarters of a cell; the preview names each point of
   interest it passed with its cost, or its distance from the road if the search never got there.
   **Verify** with `paths preview` that the near ones connect now, and what the rest cost.
   **Slow at 90%** (the Queen never finished in a coarse preview; Moder and Yagluth 21 s each): the
   distance heuristic does not see the sea's lump sums or the Mistlands' ×4, so the search flooded
   its ellipse. Each lay now runs a 128 m survey backwards from the goals first and every pass is
   guided by it (`docs/search.md`). **Verify** the times the preview prints.
   The preview pins as it goes now: a job's instances when it starts (grey skulls), the road the
   moment it is found, its spurs when they are, and a magenta pin on the running search's front;
   the pins' names are tooltips on the large map (the targets and hubs keep theirs).
   **Swamp, Mistlands, passes by distance** (2026-09-25, after a preview): the road to Bonemass was
   marked water end to end, so swamp water down to 1.5 m is now a causeway, built up to 0.4 m
   above the water and counted as ground by the search; the Mistlands went from ×4 to ×8 (their
   height profile is too broken for a road they do not need); and the first pass is chosen by the
   distance to the goal (`AdaptivePasses`: 4 m alone up to 600 m, 16 m up to 1.5 km, 32 m up to
   4 km, 64 m + 16 m beyond). **Verify** a causeway in game (`docs/biomes.md`) and the times.
   A second road forking 50 m out of the spawn instead of leaving it on its own is `Directness`
   at work (0.4: a fork near a hub costs almost nothing), not a bug; seen and kept for now.
   **The first full preview** (`paths preview all`, 2026-09-25): 11 roads and 112 spurs in 166 s,
   the Queen's 32 m pass alone 61 s (72 000 cells), Moder and Yagluth 18 s each. Those were
   wall times at 6 ms of work per frame, so most of it was frames in between; a wide cell
   (five samples) came to roughly 0.2-0.3 ms of work. So the search now runs **on a thread of its
   own** (`SearchThread`, on; the world generator is safe to read there, `docs/foundations.md`)
   and the locations are bucketed instead of checked one by one per cell. The preview prints
   cells sampled and, when spread over frames, the work time. **Verify** the times, that nothing
   stutters while a search runs, and that leaving the world mid-search is clean (the thread stops
   at its next 32 cells).
   Same preview: both late roads took the altar nearest a coast, mostly by boat, and left a third
   to half of the disc without a road into the Ashlands and the Deep North; `[Network] Central`
   now sends them to the medoid of their altars, the rest grey in the preview. The Queen's road
   ended as near her entrance as a boat gets, which stays; three infected mines are now roads
   before her (`docs/network.md`). **Verify** both in a preview.
   **Second full preview** (with the thread and the mines): every road but three under 3 s, but
   520 s in all - the third mine alone 460 s (the fine pass alone, 2.1 million cells, a goal
   400 m from the network across a strait and in the Mistlands), the second mine 11 s, the Bog
   Witch 15 s the same way. Now: goals cut to the likely 12, passes chosen by the survey's cost
   estimate, a flooding fine pass restarts at 16 m, Mistlands goals reached through entries at
   their edge (`docs/biomes.md`), one table for the search's cells (`docs/search.md`), fewer
   preview pins (only the candidates), `[Network] Style` for which instance a road takes. The
   preview's lines end with the slowest frame now - **read them** to tell the pins' lag from
   the garbage collector's.
   **Third full preview**: 14 roads and 119 spurs in 24 s, every search under 3 s; the lag was
   two frames of 1 to 2.4 s a road, finding the buildings on the main thread - now on the worker
   (`docs/search.md`). Traders' roads now go to camps not generated yet (`docs/network.md`).
   **Next:** `paths grow all` on a copy of the world, for the writing's and clearing's share.
   **First full lay** (2026-09-25), what was seen and done: grow's pins were white (now the
   preview's, and far fewer: an orb every 300 m, one at each spur's fork, portals at landings,
   none over water); the Elder's road ran into his altar and a mine's to the door at the bottom
   of its stairs (roads now end at the location's radius, a slope-turned entrance on its downhill
   side, `docs/network.md`); a road ended at a rock (standing rocks, ore and roots are obstacles
   where their zone exists, `docs/search.md`); stone on cliffs (no paint over 42°); spurs barely
   visible (3.5 m, harder edge) and main roads too even (4 to 6 m); messy hairpins (the legs'
   heights blended, `docs/laying.md`). Then, as the user decided: serpentines only need a flat
   turn - a landing at every sharp bend on steep ground (`Trail.FlattenBends`); the Mistlands
   paved only on gentle ground, dirt and a deeper cut on the rough stretches, and a
   `dverger_demister` (the lamp the Mistlands' road posts carry, it clears the mist) every
   24 m beside them (`Lamps`); big rocks nobody can break (cliffs, the Mistlands' rocks, giant
   roots) cleared off the road, also when their zone generates later (`docs/laying.md`).
   **Second full lay** (2026-09-25), what was seen and done: steep stretches still steep, and
   stone on the slopes of a half-levelled road looked awful - main roads now cut and fill up to
   4 m (`RoadKind.SteepCut`) to lie level across a slope and toward a 22% grade
   (`Trail.LimitGrade`, `FindCuts`), levelled flat out to the paint's widest and dropping off
   sharply past it, with paint only below 40-55% (`docs/laying.md`); dirt roads looked great and
   are unchanged. A hairpin's legs dug into each other: where two legs come closer than both
   need, each narrows to half the room (`Trail.LegRoom`), and their heights blend only outside
   their flat parts. Roads waded across fjords to islands with no landing: the Ocean biome is
   only the open sea, so water deeper than any river (6.5 m) is sea in every biome, and water
   longer than 100 m a crossing however shallow (`docs/search.md`). The lamps stood in the
   ground: they now sit 3.5 m up on the road post's `blackmarble_post01`, as in
   `Mistlands_RoadPost1`. A road ran through a Mistlands structure: the viaducts' and giant
   skeletons' pieces reach far past their exterior radius (8-11 m), so the search and the
   clearing now keep to each location's measured footprint (`Footprints`). The Mistlands, as the
   user suggested, get no levelling and no stone any more: a dirt track, a road post every 24 m
   (48 m since the third lay) along all of it. The progression gained two `CharredFortress` before Fader, and `NorthVillage`
   and `MorkBorg` (the winding tunnels) before the Frozen King (prefab names from the bundles'
   `Location` components).
   **To check in game:** the cut banks' look (4 m at a 1.5 m shoulder is steep) and whether
   22% is gentle enough; that paint gaps on stretches the cut could not tame are acceptable;
   hairpins; a fjord crossing and a lake; the posts (upright, lamp on top, not floating); the
   log's "reaches ... m" lines from `Footprints` (sizes, and how long measuring took); and
   whether `NorthVillage` and `MorkBorg` are placed in a world at all (they are not in the
   location lists the earlier dump read - `paths where NorthVillage`).
   **Third full lay** (2026-09-25), what was seen and done: `NorthVillage` and `MorkBorg` are
   placed, but `MorkBorg` is Mörkhalla and the Winding Tunnels are `TheHole01`, and a village is a
   generated camp that may or may not have the tunnels beside it. The Deep North now gets side
   roads after the Frozen King's altar - dirt, from the network at cost 0, but due and pinned
   (`~` in the progression) - to the tunnels, two villages beside them where there are any
   (`@TheHole01`) and Mörkhalla, and the Deep North's huts, frozen ships, memorial and hot
   springs joined the points of interest (`docs/network.md`). The Forge of Potential
   (`AncientUpgradeStation`, Mountains) is a main road due with Moder. The road levelled the
   start temple's ground bumpily: no levelling within 10 m of its centre. Serpentines still had
   steep edges on the steepest slopes: the fine search now pays for turning, squared, so it takes
   fewer, wider hairpins farther apart (`docs/search.md`). Mountain stretches "flattened, but on
   a 45° angle without road" were the stone's slope limit: too steep for stone is now dirt, up to
   53° (`docs/laying.md`). Half as many Mistlands posts (every 48 m). Yagluth's road ended in a
   pillar of his arena: roads now end at the measured footprint (`docs/network.md`). Spurs may
   cross water at 15% of the lump sums, also from a road's own sea crossing, and get a minor
   harbour (a `wood_pole_log_4`), apart from the main roads' barrels (harbour stones since
   2026-09-26, 9 below). `paths reset confirm` (Debug) empties every zone's terrain data, removes
   everything the mod placed and forgets the network - `paths undo` only ever took back the last
   job.
   **To check in game:** switchbacks on the steepest slopes and whether flat roads now bend too
   little or too much (`TurnWeight`); the dirt on steep stretches; the temple; Yagluth's road
   end (and the log's "reaches" line for `GoblinKing`); whether villages stand beside the
   tunnels (`paths where TheHole01`, `paths where NorthVillage`); a spur's harbour post (does the
   pole stand, is it seen from the water, how many spurs now cross water); `paths reset` on a copy.
7. **Levelling**, **vegetation**, **buildings** and **landings** - built with the lay; still to
   do: a setting for the clearing, and checking it in game (Black Forest density, a boulder on
   the path, a lay out of a base, a junction's heights).
8. Mod page, translations (the mod speaks only in the log so far), release.
9. **Harbour stones** - built 2026-09-26 (`src/Harbours.cs`, `docs/laying.md`), in place of the
   main roads' landing barrels: a vegvisir with blue runes on each shore of a crossing that pins
   the harbours across (and itself, if not pinned yet), several where roads leave one shore
   (25 m) for different islands. A
   vegvisir is no network object (it has no `ZNetView` and lives only inside the locations each
   client builds), so the stone is a prefab of the mod's own - the start temple's vegvisir
   copied with a `ZNetView`, registered after `ZoneSystem.Start` - and **clients need the mod to
   see it**; a vanilla client only logs "Missing prefab hash" near one (only the server deletes
   unknown prefabs, and it knows this one). **Verify:** the stone appears on a client with the
   mod and on a dedicated server's clients, the runes and the light are blue, which face the
   runes are on (it faces the road now), its height at the shore, using it pins the far shore
   (and every shore of a shared harbour), and whether the log spam on a vanilla client matters.

## Later

- A dock beside each harbour stone.
- Signposts where paths meet (a vanilla sign piece, text = the base or boss it leads to; a
  `Sign` reads its text from its ZDO, so a vanilla client shows it).
- Paths to portals and to dungeons a player has entered, as spurs.
- Wear: spurs used often widen or turn to stone, as AntTrails does (its Thunderstore page describes the mechanics).

# Bugs

None, nothing is released. Risks to watch:

- An exception inside a growth (or a dev lay) ends its coroutine and leaves `Grower.Busy` set
  until the world is left: no more growth that session. The log shows the exception.
- `paths undo` after `paths grow 3` takes back only the last job, with its spurs; `paths reset
confirm` takes back everything, a player's own digging and paving included.
- A base is stored by its first ward's position. If that ward is removed, the base's other wards
  still count as based within 150 m of it; a base that moves more than that gets a second road.

- A compiler created by the server in a zone a client is _generating_ at the same moment
  (`SpawnZone` is server side, so it should be serialised on the main thread). **Verify** that no
  vanilla path creates a compiler while generating.
- In a zone generated _after_ its path was written, vegetation is placed on the unlevelled
  ground: what stands on the path is cleared, but one on the levelling shoulder (up to 1.5 m past
  the edge) can float or sink a little.
- `paths undo` restores the terrain and removes the stones and posts, not the cleared trees and
  rocks, nor a link it added to a harbour that was there before (the answer skips links to a
  stone that no longer stands).
- A harbour stone is placed at the height the path should have there. Since 2026-09-25 that
  comes from the ground as the game builds it (`Ground.Height`), not `GetHeight`, so the metres
  of error at biome borders are gone; **verify** at a shore.
- A rock without meshes on its prefab counts as 2.5 m wide; a MineRock5 whose fragments sit
  far from its pivot is measured by its bounds, so it may go a little early.
- A player digging in a zone while the server rewrites it: last writer wins, and one of the two
  edits is lost. Rare, since it only happens during sleep.
