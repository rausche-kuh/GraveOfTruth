# OdinsTree

Bless a tree with the hammer and it never falls, so a tree house built on it is safe — and stays
safe without the mod. The ground under it is guarded, the hammer cycles a tree through the kinds
of its family, and dodging back and forth beside a sapling grows it in seconds.
[`package/README.md`](package/README.md) is the Thunderstore page; [`ROADMAP.md`](ROADMAP.md) is
what comes next and the known bugs.

| Path | What |
| --- | --- |
| `src/OdinsTree.cs` | The whole mod: entry point, settings and the four features' patches. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`. |

## Build

```bash
./scripts/setup.sh                # once, and after every Valheim update
./scripts/deploy.sh OdinsTree     # build + install into your profile
./scripts/package.sh OdinsTree    # dist/OdinsTree-<version>.zip
```

`VERSION` in `src/OdinsTree.cs` is the version — `package` stamps it into
`package/manifest.json`. See [scripts/README.md](../scripts/README.md) for the full workflow on
Windows and Linux.
