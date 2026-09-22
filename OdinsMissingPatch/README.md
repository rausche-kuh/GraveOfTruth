# OdinsMissingPatch

A collection of small quality of life changes, one config section each.
[`package/README.md`](package/README.md) is the Thunderstore page and describes what the mod
actually does.

| Path | What |
| --- | --- |
| `src/OdinsMissingPatch.cs` | The BepInEx entry point: binds the config, applies the patches. |
| `src/Tweak.cs` | What a tweak is — its config section, its Enabled switch, its multipliers. |
| `src/Tweaks/` | One file per quality of life change, patches included. |
| `docs/` | The notes behind the code: `tweaks.md` (what each tweak does), `conventions.md`, one file per subsystem, `references.md`. `CLAUDE.md` indexes them. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`. |

## Adding a tweak

Drop a `src/Tweaks/<Name>.cs` holding an `internal sealed class <Name> : Tweak` with a private
constructor and a `static readonly <Name> Instance`, give it a `Section`, a `Summary`, a `Bind`
that binds its settings, and nest its `[HarmonyPatch]` classes inside it. Then list
`<Name>.Instance` in `Tweaks` in `src/OdinsMissingPatch.cs` — that is the whole registration.

## Build

```bash
./scripts/setup.sh                        # once, and after every Valheim update
./scripts/deploy.sh OdinsMissingPatch     # build + install into your profile
./scripts/package.sh OdinsMissingPatch    # dist/OdinsMissingPatch-<version>.zip
```

`VERSION` in `src/OdinsMissingPatch.cs` is the version — `package` stamps it into
`package/manifest.json`. See [scripts/README.md](../scripts/README.md) for the full workflow on
Windows and Linux.
