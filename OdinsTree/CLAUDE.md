# OdinsTree

Makes a tree safe to build a tree house on. See the root `CLAUDE.md` for the shared build, the
scripts and the environment.

**State:** scaffold. The entry point and its settings exist; none of the features do yet, and
nothing is released — `VERSION` stays 0.1.0 until the first Thunderstore upload.
`package/icon.png` is a generated placeholder.

| Path | What |
| --- | --- |
| `src/OdinsTree.cs` | BepInEx entry point and settings. |
| `ROADMAP.md` | The plan, feature by feature, with the game facts each one rests on and which of them still need checking. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## The rules that always apply

- **Vanilla compatible first.** Whatever the mod does to a tree must survive the mod's removal:
  the blessing is the tree's own health on its ZDO, growth is the sapling's own `plantTime`, a
  new tree kind is a vanilla prefab. Our own ZDO keys only for things that are allowed to vanish
  with the mod. The terrain guard is the one feature that cannot be vanilla, and the mod page says
  so.
- Never take a gesture away from building: a left click with a piece selected always places the
  piece. The mod only answers the hammer's repair tool on a tree, and the remove click on a tree.
- A blessed tree cannot be cycled to another kind — that would pull the trunk out from under a
  tree house.
- Every write to a tree or sapling claims the object's `ZNetView` first and checks the ward
  (`PrivateArea.CheckAccess`), like building does.
- Null-guard everything: `Player.m_localPlayer` is frequently null.
