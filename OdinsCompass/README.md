# OdinsCompass

Craft a compass, wear it like the wishbone, and it points you to the nearest boss: a few wavy
lines of bright blue light blow toward it, low over the ground, a few more the closer you get.
Upgrade it biome by biome and it learns each biome's dungeons and ore. The finding is the game's
own vegvisir request, so it works on any server, modded or not.
[`package/README.md`](package/README.md) is the Thunderstore page; [`ROADMAP.md`](ROADMAP.md) is
what is still to check and what comes next; [`CLAUDE.md`](CLAUDE.md) is how it is built.

| Path | What |
| --- | --- |
| `src/OdinsCompass.cs` | BepInEx entry point and settings: the seek interval, the cycle key, the arrival distance, and per tier the targets and the recipe. |
| `src/Items.cs` | The eight compass items, cloned from the wishbone, and their recipes. |
| `src/SE_Compass.cs` | The status effect that runs the compass while it is worn. |
| `src/Seeker.cs` | Finds the chosen target: vegvisir asks to the server, a local scan for ore. |
| `src/Streaks.cs` | The wind: two particle systems built in code. |
| `src/Targets.cs`, `src/Translations.cs`, `src/Icons.cs` | The target groups from the config, the words, the icons. |
| `src/Dev/CompassCommands.cs` | The `compass` console command for checking location and prefab names in game. Debug builds only. |
| `assets/` | `translations.csv` and the item icons, shipped beside the DLL. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`. |

## Build

```bash
./scripts/setup.sh                          # once, and after every Valheim update
./scripts/deploy.sh OdinsCompass            # build + install into your profile
./scripts/deploy.sh OdinsCompass -c Debug   # the same, with the compass dev command
./scripts/package.sh OdinsCompass           # dist/OdinsCompass-<version>.zip
```

`VERSION` in `src/OdinsCompass.cs` is the version — `package` stamps it into
`package/manifest.json`. See [scripts/README.md](../scripts/README.md) for the full workflow on
Windows and Linux.
