# How the features work

Blessing, its look, aiming with the hammer, the hover text, cycling and twerking, each as built in
`src/OdinsTree.cs` and verified against `decompiled/`. The terrain guard has its own file,
[terrain-guard.md](terrain-guard.md).

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
