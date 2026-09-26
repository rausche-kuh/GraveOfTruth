# OdinsTree

Makes a tree safe to build a tree house on: bless it with the hammer, guard the ground under it,
cycle it through the kinds of its family, twerk a sapling grown. See the root `CLAUDE.md` for the
shared build, the scripts and the environment.

**State:** all four features built in their simplest form (one file, no assets) and played
once: blessing, hints and cycling work. The server side (the terrain guard for players without
the mod) is built but not yet run on a server. The 0.1.0 package is built (`dist/OdinsTree-0.1.0.zip`,
changelog section `## 0.1.0`) but not yet uploaded; the next change opens a new `## Unreleased`.
`package/icon.png` is a generated placeholder.

## Docs

| File | Read it when |
| --- | --- |
| [`docs/mechanics.md`](docs/mechanics.md) | Touching blessing, its look (the borrowed strike, burst and glow), hammer aiming, the hover text, cycling or twerking: which game members each one rests on. |
| [`docs/terrain-guard.md`](docs/terrain-guard.md) | Touching the terrain guard: what a vanilla client can be made to obey, the server side warden (compiler ownership), the client side guard. |
| [`ROADMAP.md`](ROADMAP.md) | Planning the next change: polish per feature, what comes later, known bugs. |

## Source map

Everything is in `src/OdinsTree.cs`, in this order:

| Part | What |
| --- | --- |
| `OdinsTreePlugin` top | `VERSION`, constants (`BlessedHealth` 1e12, `BlessedThreshold` 1e8, `TwerkRadius` 3 m, `MaxCrouchInterval` 1.5 s), settings (`TerrainGuardRadius`, `TwerkGrowSeconds`, `Families`). |
| Helpers | `IsBlessed`, `AimedObject` / `AimedTree` (own raycast in build mode), `HoverText`, `NearBlessedTree` (client list) / `NearBlessedTreeZDO` (world data), `GetFamilies` / `NextKind`, `SwingHammer`. |
| `RememberTree`, `BlessedLook`, `BlessOnRepair`, `UnhittableWhenBlessed`, `TreeHoverText` | Blessing: the tree list, the one-off show, the repair-tool click, no damage, "Blessed by Odin". |
| `GuardGhost`, `GuardTerrainOp`, `GuardTerrainOwner`, `Warden` | Terrain guard: red ghost, dropped ops, the owner's refusal, the server holding the compilers. |
| `CycleOnRemove` | Cycling on the hammer's remove click. |
| `CountCrouches` + `Update` | Twerking: crouch presses counted, saplings in reach grown and tinted. |

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
- With the hammer out `GetHoverObject()` is always null (`Player.UpdateHover` clears it in place
  mode): use `AimedObject`, never a `Hoverable` on the tree.
- `Grow()` on an unhealthy plant with `m_destroyIfCantGrow` deletes it: only call it after
  `UpdateHealth` says `Healthy`.
- Null-guard everything: `Player.m_localPlayer` is frequently null.
