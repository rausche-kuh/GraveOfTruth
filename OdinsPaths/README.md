# OdinsPaths

A road network that grows with the world's progress: stone roads to the boss altars and the
traders, forking off each other, dirt spurs to the villages and crypts beside them, all
following the easiest ground. Server side; the paths are the game's own terrain data. [`package/README.md`](package/README.md) is the
Thunderstore page; [`ROADMAP.md`](ROADMAP.md) is the plan, and [`docs/`](docs/) the design behind it: what is
possible, what it costs and what still has to be checked in game.

| Path | What |
| --- | --- |
| `src/OdinsPaths.cs` | Entry point and settings. |
| `src/PathSearch.cs`, `src/Trail.cs`, `src/TerrainWriter.cs`, `src/PathLayer.cs` | Find a route, shape it, write it into the terrain. |
| `src/RoadKind.cs`, `src/Network.cs`, `src/Planner.cs`, `src/Grower.cs` | Stone and dirt, the network and its storage, what comes next, and when it grows. |
| `src/Dev/PathCommands.cs` | `paths lay`, `paths plan`, `paths grow`, `paths network`, `paths undo` and more (Debug builds only; the list is in `CLAUDE.md`). |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`. |

## Build

```bash
./scripts/setup.sh                        # once, and after every Valheim update
./scripts/deploy.sh -c Debug OdinsPaths   # build + install, with the dev commands
./scripts/package.sh OdinsPaths           # dist/OdinsPaths-<version>.zip
```

`VERSION` in `src/OdinsPaths.cs` is the version — `package` stamps it into
`package/manifest.json`. See [scripts/README.md](../scripts/README.md) for the full workflow on
Windows and Linux.
