# ThisIsValheim

Kicking a door open — with the game's own bare handed kick — sets a battering ram off in its
face and makes it swing four times faster.
[`package/README.md`](package/README.md) is the Thunderstore page and describes what the mod
actually does.

| Path | What |
| --- | --- |
| `src/ThisIsValheim.cs` | The whole plugin: BepInEx entry point + Harmony patches. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`. |

No `assets/`: the bang is a list of the game's own effect prefabs, named in the config and
looked up at runtime.

## Build

```bash
./scripts/setup.sh                   # once, and after every Valheim update
./scripts/deploy.sh ThisIsValheim    # build + install into your profile
./scripts/package.sh ThisIsValheim   # dist/ThisIsValheim-<version>.zip
```

`VERSION` in `src/ThisIsValheim.cs` is the version — `package` stamps it into
`package/manifest.json`. See [scripts/README.md](../scripts/README.md) for the full workflow on
Windows and Linux.
