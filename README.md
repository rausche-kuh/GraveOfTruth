# Valheim mods

| Mod                                     | What it does                                                                                                                                 |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| [GraveOfTruth](GraveOfTruth/)           | Dying calls down the obliterator's lightning on your grave and plays a loser jingle out of it, echo, thunderstorm and all.                   |
| [OdinsMissingPatch](OdinsMissingPatch/) | Small quality of life changes, each one configurable: wider crafting station, comfort and mist clearing ranges, fires that never go out, stamina that only drains in combat. |

## Quick start

```bash
./scripts/setup.sh    # find Valheim + BepInEx, stage reference assemblies into lib/
./scripts/deploy.sh   # build every mod and install it into your profile
```

`scripts\setup.ps1` and `scripts\deploy.ps1` are the same thing on Windows.
[scripts/README.md](scripts/README.md) documents all five scripts, their flags, how to add a new
mod, and what to do when something goes wrong.

## Layout

```
GraveOfTruth/          one mod: <Name>/<Name>.csproj, src/, assets/, package/, README.md
  package/             manifest.json, icon.png, the Thunderstore page README.md and CHANGELOG.md
OdinsMissingPatch/     the same shape, minus assets/
scripts/               setup, deploy, package, decompile, clean (.sh and .ps1)
Directory.Build.props  the build every mod shares
lib/                   game + BepInEx reference assemblies   (generated, gitignored)
decompiled/            the game's own C#, for API lookup      (generated, gitignored)
dist/                  Thunderstore zips                      (generated, gitignored)
```
