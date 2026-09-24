# OdinsTree

Makes a tree safe to build a tree house on. See the root `CLAUDE.md` for the shared build, the
scripts and the environment.

**State:** all four features built in their simplest form (one file, no assets) and played
once: blessing, hints and cycling work. The server side (the terrain guard for players without
the mod) is built but not yet run on a server. The 0.1.0 package is built (`dist/OdinsTree-0.1.0.zip`,
changelog section `## 0.1.0`) but not yet uploaded; the next change opens a new `## Unreleased`.
`package/icon.png` is a generated placeholder.

| Path | What |
| --- | --- |
| `src/OdinsTree.cs` | The whole mod: entry point, settings, helpers, and the patches for the four features in order. |
| `ROADMAP.md` | What comes next, and the known rough edges. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## The rules that always apply

- **Vanilla compatible first.** Whatever the mod does to a tree must survive the mod's removal:
  the blessing is the tree's own health on its ZDO, growth is the sapling's own `plantTime`, a
  new tree kind is a vanilla prefab. Our own ZDO keys only for things that are allowed to vanish
  with the mod. The terrain guard is the one feature that cannot be vanilla: a client without
  the mod is only held to it when the server has the mod, and the mod page says so.
- Never take a gesture away from building: a left click with a piece selected always places the
  piece. The mod only answers the hammer's repair tool on a tree, and the remove click on a tree.
- A blessed tree cannot be cycled to another kind — that would pull the trunk out from under a
  tree house.
- Every write to a tree or sapling claims the object's `ZNetView` first and checks the ward
  (`PrivateArea.CheckAccess`), like building does.
- Null-guard everything: `Player.m_localPlayer` is frequently null.

## How each feature works (verified against `decompiled/`)

- **Blessing** = `ZDOVars.s_health` on the tree's ZDO set to `1e12`; blessed is `>= 1e8`. Lifting
  writes the prefab's `m_health`. `TreeBase.RPC_Damage` runs on the owner, subtracts from that
  value and destroys at 0; at `1e12` a float cannot even hold the loss of a normal hit, so a
  client without the mod chops forever. With the mod a blessed tree is unhittable like the
  ancient root (`ResourceRoot`, which has no `IDestructible` at all): prefixes on
  `TreeBase.Damage` (hitter side: no RPC, no damage text, no shake, no chips, no noise) and on
  `RPC_Damage` (owner side, for hitters without the mod) return false. The weapon's own hit
  effect still plays, as on the root. The click is a prefix on
  `Player.Repair(toolItem, repairPiece)`, which vanilla only enters with the repair tool selected
  and which does nothing unless a `Piece` is hovered — a tree is never one, so skipping it loses
  nothing. `TreeBase.m_nview` is private (publicized); `ClaimOwnership` just moves the ZDO owner.
- **The look** (`BlessedLook`, added to a tree when it is blessed) is borrowed from the
  game, nothing is shipped in `assets/`. Effect prefabs (`fx_*`, `vfx_*`) have no `ZNetView`
  and are not in `ZNetScene`, so each is reached through something that references it:
  the strike is `ZNetScene.GetPrefab("lightningAOE")` (the incinerator's lightning: a
  networked `Aoe` with 400 lightning damage, so only its visual children - bolt, glow, sparks,
  shockwave, two lights, `sfx_shockwave` - are cloned under a holder destroyed after 10 s,
  the `Aoe` children `AOE_ROD`/`AOE_AREA` skipped); the red burst is
  `ObjectDB.GetStatusEffect("GP_Eikthyr").m_startEffects` (`fx_GP_Activation`, self
  destructs after 5 s, has a `CamShake`); the lasting glow is
  `BossStone_Eikthyr`'s `BossStone.m_activeEffect` (`active_effects`: a flickering
  orange-red `LightFlicker` light, two looping particle systems, and a `GuidePoint` that
  would register a raven text - dropped before its `Start`). All three were read out of
  bundle `c4210710` with UnityPy (see the root `CLAUDE.md`). Strike, burst and glow are one
  show for the blesser (the glow is destroyed 6 s after the burst); afterwards the tree looks
  like any other, on purpose. A lasting tint was tried (`MaterialMan.SetValue(tree,
  ShaderProps._Color / _EmissionColor)`, the way `Piece` marks an invalid piece - every tree
  shader has both) and dropped: a blessed tree should not look odd.
- **Aiming:** `Player.UpdateHover` sets `m_hovering` to null whenever `InPlaceMode()`, so
  `GetHoverObject()` is always null with the hammer out and a `Hoverable` on the tree is never
  read. Everything the mod does with the hammer therefore uses its own raycast, copied from
  `UpdateWearNTearHover`: camera position and forward, 50 m, `m_removeRayMask`, hit within
  `m_maxPlaceDistance` (5 m) of `m_eye`. Out of build mode `GetHoverObject()` is used as is.
  `AimedObject` in the source.
- **Hover text** is written into `Hud.m_hoverName` by a postfix on `Hud.UpdateCrosshair`,
  after the game has set it (to "" in build mode, and to the tree's name out of it: the
  tree prefabs carry a `HoverText` with `$prop_beech` and the like, a prefab field that no
  ZDO changes). "Blessed by Odin" replaces that whenever a blessed tree is looked at,
  like the root's status; the click hints only with the hammer out. The field is a
  `TextMeshProUGUI` and `lib/` stages no TextMeshPro assembly, so the text property is set
  through Harmony's `Traverse`.
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
- **Cycling** is a prefix on `Player.RemovePiece`, which `UpdatePlacement` calls for the remove
  button (hammer only: `m_canRemovePieces`) and for a left click with the remove tool. Vanilla
  raycasts for a `Piece` and would find none on a tree. Families: every `Plant.m_grownPrefabs`
  with more than one entry (read from `ZNetScene.m_prefabs` once) plus the `Families` config,
  merged when they share a name. Real prefab names checked in `SoftRef/manifest_extended`:
  `Birch1/2(_aut)`, `Beech1`, `Beech_small1/2`, `FirTree`, `FirTree_small(_dead)`,
  `FirTree_big`, `Pinetree_01`, `Pinetree_Snow`, `SwampTree1/2`, `YggaShoot1..3`, `Oak1`.
  The swap is `Instantiate(next, pos, rot)` + `ZNetView.SetLocalScale` (needs owner, which a
  fresh object is) + `nview.Destroy()` on the old one after `ClaimOwnership`.
- **Twerking:** `PlayerController` passes `crouch` to `Player.SetControls` as the button's
  rising edge (`flag8 && !m_lastCrouch`), one frame per press; a prefix counts presses that
  come within 1.5 s of the previous one. Three in a row start the twerk, which lasts until
  1.5 s after the last press. The plugin's `Update` then, every frame: saplings come from
  `SlowUpdate.GetAllInstaces()` (`Plant` is a `SlowUpdate`), within 3 m XZ. `Plant.SUpdate`
  runs every 10 s on the owner and calls `Grow()` once `TimeSincePlanted() > GetGrowTime()`
  (seeded lerp of `m_growTime..m_growTimeMax`). We shift `ZDOVars.s_plantTime` back by
  `growTime × dt / TwerkGrowSeconds` (so TwerkGrowSeconds of twerking grows any sapling), zero
  `m_updateTime` so the half-grown model swaps in on the next slow tick, and call `Grow()`
  ourselves when it is due — only after `UpdateHealth` says `Healthy`, because `Grow()` on an
  unhealthy plant with `m_destroyIfCantGrow` deletes it. Any `Plant` with grown prefabs counts,
  crops included. The green glow is `MaterialMan.instance.SetValue(go, ShaderProps._Color /
  _EmissionColor)` - the same property block the piece highlight uses (`WearNTear.Highlight`),
  registered with `updateRenderers: true` so the inactive grown model is tinted too - and
  `ResetValue` when the plant leaves the radius, the twerk stops or it grows.
