# GraveOfTruth

Dying calls down the obliterator's lightning on your grave and plays a loser jingle out of it, echo,
thunderstorm and all. [`package/README.md`](package/README.md) is the Thunderstore page and
describes what the mod actually does.

| Path | What |
| --- | --- |
| `src/GraveOfTruth.cs` | The whole plugin: BepInEx entry point + Harmony patches. |
| `assets/sound.ogg` | The jingle, loaded at runtime from next to the DLL. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md`. |

## Build

```bash
./scripts/setup.sh                  # once, and after every Valheim update
./scripts/deploy.sh GraveOfTruth    # build + install into your profile
./scripts/package.sh GraveOfTruth   # dist/GraveOfTruth-<version>.zip
```

`VERSION` in `src/GraveOfTruth.cs` is the version — `package` stamps it into `package/manifest.json`.
See [scripts/README.md](../scripts/README.md) for the full workflow on Windows and Linux.
