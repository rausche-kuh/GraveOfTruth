# The two facts the design rests on

Why the compass is client side and why ore is short range. The plan is [`../ROADMAP.md`](../ROADMAP.md).

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
search needs the mod on the server ("World wide ore" under "Later" in the roadmap).
