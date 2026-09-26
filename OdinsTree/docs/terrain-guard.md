# The terrain guard

Keeps the ground within `TerrainGuardRadius` of a blessed trunk from being dug, raised, levelled
or tilled: what a vanilla client can be made to obey, the server side warden that reaches
players without the mod, and the client side guard. Verified against `decompiled/`; the warden
has not run on a dedicated server yet (see [../ROADMAP.md](../ROADMAP.md)).

- **What a vanilla client can be made to see or refuse:** terrain edits are refused by
  vanilla only inside a ward without access (`PrivateArea.CheckAccess` in
  `UpdatePlacementGhost` for hoe and cultivator pieces and in `Attack.SpawnOnHitTerrain` for
  the pickaxe; the ward's 32 m radius and its building block are on the prefab, nothing on
  the ZDO changes them) or inside a no-build location (`Location.IsInsideNoBuildLocation`, a
  static list filled by `Location.Awake` of the client-local location prefabs; it blocks
  pieces too, placing and removing, so a tree house inside one is impossible). A
  `LocationProxy` ZDO does make a vanilla client spawn a vanilla location anywhere
  (`SpawnProxyLocation`, `SpawnMode.Client`: the prefab's static scenery, its `ZNetView`
  children left out), but the ZDO carries only the location hash and a seed - the radius,
  `m_noBuild` and the scenery are the prefab's. No piece blocks a terrain op: `TerrainOp.Awake`
  checks only `m_forceDisableTerrainOps`, hoe and pickaxe work straight through a floor, and
  the start temple's stones are just meshes without `Destructible` inside a no-build
  location. A custom prefab is invisible to a vanilla client (`ZNetScene.CreateObject`
  skips unknown prefab hashes). The only vanilla hover text a ZDO can carry is a `Sign`'s
  `s_text` (and a ward's `s_creatorName`); `TreeBase` has no name to set. The game's own
  "custom data" (`Player.m_customData`, `ItemData.m_customData`, and any ZDO key the game
  does not know) is stored and synced but never read by vanilla code, so it can inform a
  modded client but cannot change what a vanilla one does. What moves a tree when the ground
  changes is `StaticPhysics` on the tree prefab (`m_fall` and `m_pushUp`, first check 20 s
  after `Awake`, then on the slow update; every client moves its own instance, the owner
  writes the ZDO position); it reads nothing from the ZDO, so no world data pins a tree.
  What a vanilla client does obey is the owner of the terrain compiler - hence the warden
  below.
- **The warden** (server side, `Warden` in the source) is how the terrain guard reaches
  players without the mod. Every terrain change is `TerrainComp.ApplyOperation` →
  `RPC_ApplyOperation` on the owner of the zone's `_TerrainCompiler` ZDO; the sender applies
  nothing itself, it waits for the owner's `s_TCData`. The server hands ownership out in
  `ZDOMan.ReleaseNearbyZDOS` (every 2 s, once for itself and once per peer, unowned or
  out-of-area ZDOs near the peer go to the peer). A postfix on it, on the server, finds the
  blessed trees in that call's `m_tempNearObjects` (tree prefab hash from `ZNetScene.
  m_namedPrefabs` + `s_health >= 1e8`), takes the compilers of every zone within 16 m of one
  (`SetOwner(GetSessionID())`), keeps their zones spawned with `ZoneSystem.PokeLocalZone` (the
  server keeps the zones around its reference position, `Vector3.zero` on a dedicated server,
  the same way; `UpdateTTL` drops a zone 4 s after its last poke unless an instance is in the
  sector) and creates a compiler through `Heightmap.GetAndCreateTerrainCompiler` for a zone
  that has none yet - otherwise the first digger would create it as owner. A prefix on
  `ZNetScene.CreateObjects` adds the held compilers to the near list, so `CreateObjectsSorted`
  instantiates them and `RemoveObjects` (same list) keeps them; `TerrainComp.Awake` needs its
  `Heightmap`, so only compilers of loaded zones go in. Held compilers are forgotten 10 s
  after a peer was last near, and their instances and zones go the vanilla way. A postfix on
  `ZDOMan.IsInPeerActiveArea` counts a held compiler as inside the server's area, or the
  peer's hand-out would take it every 2 s and each owner change resends the compiler's whole
  `s_TCData` (about 100 KB). The refusal itself is the prefix on `RPC_ApplyOperation`
  (`GuardTerrainOwner`): it peeks position and `TerrainOp.Settings` out of the package,
  rewinds it, checks the world data (`NearBlessedTreeZDO`: `FindSectorObjects` over the 3×3
  zones) and answers the sender with the routed `ShowMessage` RPC that `MessageHud`
  registers. The vanilla digger still pays the stamina and the stone and sees a green hoe
  ghost; that cannot be helped without the mod on their side.
- **Terrain guard** (client side): every terrain change on a client is an instantiated `TerrainOp` prefab —
  hoe and cultivator pieces through `TryPlacePiece`, the pickaxe through
  `Attack.SpawnOnHitTerrain` — whose `Awake` applies itself to the heightmaps and self-destructs.
  A prefix on `TerrainOp.Awake` drops it near a blessed tree (`TerrainOp.m_forceDisableTerrainOps`
  is set while the placement ghost is built; leave those alone). The ghost turns red through a
  postfix on `Player.UpdatePlacementGhost` that sets `m_placementStatus = Invalid` when the ghost
  carries a `TerrainOp`, which also makes `TryPlacePiece` refuse with `$msg_invalidplacement`
  before any stone is spent. Blessed trees are found from a static list filled in
  `TreeBase.Awake`; destroyed ones are Unity-null and pruned on scan. The owner-side check is
  the warden's `GuardTerrainOwner` above.
