# Roadmap

Odin's Tree makes a tree safe to build a tree house on. Nothing is implemented yet beyond the
entry point and its settings; this is the plan, in the order to build it.

Game facts marked **verify** were written from memory of the game code, without `decompiled/` at
hand. Check each one against `decompiled/assembly_valheim/` before writing the code that rests on
it; what the check teaches goes into `CLAUDE.md`.

## 1. The blessing: an indestructible tree

**The player:** with the hammer's **repair tool** selected, left click a standing tree. It shines
briefly and says `This tree is blessed by Odin`. It can no longer be felled — not by an axe, a
troll, a falling log or fire. Left click it again to lift the blessing.

**Vanilla compatible:** a standing tree is a `TreeBase`, and its health lives on its ZDO
(`ZDOVars.s_health`, falling back to the prefab's `m_health` when unset). `TreeBase.RPC_Damage`
subtracts the hit from that value and fells the tree at 0 (verify). The blessing sets it to a
value no damage reaches (`1e9f`, not infinity — keep NaN out of other mods' maths). That is
vanilla data read by vanilla code, so:

- a blessed tree stays blessed when the mod is removed, on every client, modded or not;
- lifting the blessing writes the prefab's `m_health` back (the damage already taken is lost,
  which is harmless for a tree);
- "is this tree blessed" is `health >= 1e8f`, nothing else to store.

**Write access:** `TreeBase` has no RPC to set its health, so the blesser claims ownership of the
tree's `ZNetView` and writes the ZDO (verify that `ClaimOwnership` on a tree has no side effect).
Respect wards: `PrivateArea.CheckAccess(position)` must pass, as it does for building.

**Why the repair tool:** a left click with a building piece selected places that piece, and
placing pieces against the trunk is the whole point of a tree house. The repair tool does nothing
to a tree in vanilla, so nothing is taken away. Where the click is caught: the repair path in
`Player.UpdatePlacement` → `Player.Repair(toolItem, repairPiece)` (private, see
`OdinsMissingPatch/docs/building-and-world.md`), prefixed to handle a hovered `TreeBase` and skip
the vanilla repair. Hover detection: `Player.GetHoverObject()` → `GetComponentInParent<TreeBase>()`.

Also: a hover line on a tree while the repair tool is out (`[LMB] Bless` / `[LMB] Lift blessing`,
`[RMB] Next kind`). Trees have no `Hoverable` of their own (verify) — the line goes through the
HUD's hover text instead, like ThisIsValheim's kick hint does for doors.

## 2. The ground under a blessed tree

**The player:** the ground within `TerrainGuardRadius` (3m) of a blessed trunk cannot be dug,
raised, levelled or tilled. The pickaxe bounces off the ground, the hoe's ghost turns red.

**Only with the mod:** vanilla has no such rule, so this part is the mod's alone — a player
without it can still dig there. Say so on the mod page.

Where terrain changes (verify each):

- The hoe and the cultivator place a `TerrainOp` piece: add a placement status in
  `Player.UpdatePlacementGhost` (the way the game marks a ward it has no access to) so the ghost
  turns red and the placement is refused with a message.
- The pickaxe digs with `Attack.SpawnOnHitTerrain` (the attack's `m_spawnOnHitTerrain`, a
  `TerrainOp` prefab): skip the spawn near a blessed tree.
- Anything else ends in `TerrainComp.DoOperation` / `ApplyOperation` on the terrain's owner: a
  last line of defence there, since that runs on the owner and not the digger.

Finding blessed trees fast: keep a registry of loaded `TreeBase`s with blessed health (filled in
`TreeBase.Awake`, trimmed on destroy, updated on bless/lift and when the ZDO's health changes
under us), rather than a physics query per dig.

## 3. Tree kinds: right click cycles

**The player:** with the hammer out, right click an **unblessed** tree to turn it into the next
kind of its family — Birch1 → Birch2, the three Yggdrasil shoots, a young beech into a grown one.
A blessed tree does not cycle, so a misplaced right click never swaps the tree a house stands on.

How: the family is every prefab one `Plant` can grow into (`Plant.m_grownPrefabs`, read from every
sapling in `ZNetScene` at start) plus the naming siblings the world generator places
(`Birch1`/`Birch2`/`Birch1_aut`/`Birch2_aut`, `Oak1`, `Pinetree_01`, `FirTree`, `SwampTree1`,
`YggaShoot1..3`, ... — list them from `ZNetScene.m_prefabs` by `TreeBase`, verify the names).
Cycling destroys the tree (`ZNetScene.instance.Destroy`, owner only) and spawns the next kind at
the same position, rotation and scale (`ZNetView.SetLocalScale` stores it on the ZDO). Vanilla
prefabs only, so nothing is left behind if the mod is removed.

Right click with the hammer is "remove piece", which does nothing on a tree in vanilla (verify),
so the gesture is free. Open: right click in any hammer mode, or only with the repair tool like
the blessing? Starting with any mode.

## 4. Twerking grows saplings

**The player:** dodge back and forth beside a sapling. Every dodge pushes it closer to grown,
with a puff of leaves; about 15 seconds of solid twerking (`TwerkGrowSeconds`, 10–20s is the
intended range) and it is a tree. Stamina is the price — every dodge costs its usual stamina.

How:

- A sapling is a `Plant` whose grown prefabs are trees. Its age is the ZDO's `plantTime`
  (`ZDOVars.s_plantTime`, ticks) against `Plant.GetGrowTime()` (seeded random between
  `m_growTime` and `m_growTimeMax`, verify). Each dodge within 3m moves `plantTime` back by
  `growTime × dodgeInterval / TwerkGrowSeconds` — measure the dodge interval, don't assume it.
- A dodge is caught where it actually starts (`Player.UpdateDodge`, after the stamina check,
  verify) — not on the key press, so a dodge refused for lack of stamina does not count.
- Writing `plantTime` needs ownership: claim the sapling's `ZNetView` first. The game's own
  `Plant` update (every few seconds, owner only, verify the interval) then grows it — vanilla
  code, vanilla data.
- A sapling that cannot grow (wrong biome, no room, too cold, verify `Plant.m_status`) does not
  grow faster: say why, the way the cultivator's hover text does.
- Growing a tree the tree house needs is a first step; blessing it is still a separate click.

## 5. Later

- A visible mark on a blessed tree (a faint rune at the foot of the trunk, or leaves that shimmer
  on hover) so builders can tell which trees are safe — client side, looks only.
- A setting for who may bless: anyone, or only players with access to the ward over the tree.
- Translations with a `translations.csv` like OdinsMissingPatch's.

# Bugs

None known — nothing is built yet.
