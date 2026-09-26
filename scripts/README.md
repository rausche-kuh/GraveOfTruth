# The scripts

Every script comes twice, `*.ps1` for Windows PowerShell and `*.sh` for bash, except `publish` and
`icon`, which are Linux only. Both sets share `lib.ps1` / `lib.sh` and the gitignored state they
produce (`lib/`, `decompiled/`, `Valheim.props`, `dist/`), so they are interchangeable on the same
checkout.

|             |                                                                                            |
| ----------- | ------------------------------------------------------------------------------------------ |
| `setup`     | Find Valheim + BepInEx, stage the reference assemblies into `lib/`, write `Valheim.props`. |
| `deploy`    | Build and install into your mod manager profile.                                           |
| `bump`      | Raise a mod's version for a release and close off its changelog.                           |
| `package`   | Build the Thunderstore zips in `dist/`.                                                    |
| `publish`   | Upload every version that is not on Thunderstore and Hexium yet (Linux only).              |
| `decompile` | Dump the game's own C# into `decompiled/` for API lookup.                                  |
| `clean`     | Delete what the others produced.                                                           |
| `icon`      | Trim and square a map icon to 64x64 (Linux only, ImageMagick).                             |

```powershell
.\scripts\setup.ps1      # once, and after every Valheim update
.\scripts\deploy.ps1     # build + install every mod
.\scripts\bump.ps1       # raise a version, rename ## Unreleased
.\scripts\package.ps1    # dist\<Mod>-<version>.zip
.\scripts\decompile.ps1  # game source into decompiled\
.\scripts\clean.ps1      # bin\, obj\, dist\
```

```bash
./scripts/setup.sh
./scripts/deploy.sh      # -c Debug also compiles each mod's src/Dev/ helpers
./scripts/bump.sh
./scripts/package.sh
./scripts/publish.sh     # -n for a dry run
./scripts/decompile.sh
./scripts/clean.sh
./scripts/icon.sh <image>
```

`deploy`, `package` and `clean` take mod names and act on every mod in the repo when given none:
`./scripts/deploy.sh GraveOfTruth` builds just that one. `dotnet build GraveOfTruth/GraveOfTruth.csproj`
also works once `setup` has run.

**More:** [docs/scripts.md](../docs/scripts.md) has every flag of `setup`, `deploy`, `decompile`,
`clean` and `icon`, the paths `setup` searches, where things end up, and what to do when something
goes wrong. [docs/releasing.md](../docs/releasing.md) covers `bump`, `package`, preview images on the
mod page and `publish` (categories, tokens).

## What you need

|                      |                                                                                                                                            |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| .NET SDK 8           | `winget install Microsoft.DotNet.SDK.8`, `sudo pacman -S dotnet-sdk`, `sudo apt install dotnet-sdk-8.0`, `sudo dnf install dotnet-sdk-8.0` |
| Valheim              | Installed through Steam. On Linux, the native build.                                                                                       |
| BepInEx              | Installed into a profile by a mod manager — [Gale](https://github.com/Kesomannen/gale) or r2modman.                                        |
| binutils             | Linux only, for `strings`; used just to read the Unity and BepInEx versions. Optional.                                                     |
| `python3` or `zip`   | Linux only, for `package.sh`.                                                                                                              |
| `python3` and `curl` | Linux only, for `publish.sh`.                                                                                                              |
| ImageMagick 7        | Linux only, for `icon.sh`.                                                                                                                 |

No Mono, no Wine on Linux: the game assemblies are `net472` references and the NuGet package
`Microsoft.NETFramework.ReferenceAssemblies` supplies the framework, so the .NET 8 SDK builds the
plugins on its own.

## Adding a mod

A mod is any top level directory holding `<Name>/<Name>.csproj`; the scripts discover them that way,
so nothing needs registering. Create:

```
<Name>/
  <Name>.csproj      AssemblyName + RootNamespace only - the build lives in Directory.Build.props
  src/<Name>.cs      the BaseUnityPlugin, with the GUID/NAME/VERSION consts
  src/Dev/           optional, test helpers compiled into Debug builds only
  assets/            optional, shipped next to the DLL
  package/           what Thunderstore gets: manifest.json, icon.png, README.md, CHANGELOG.md,
                     plus publish.json (the categories per site, not shipped)
  README.md          dev facing, not shipped - what the mod is, how to build it
  CLAUDE.md          optional, the notes an agent needs; keep it short, detail goes in docs/
```

`package/manifest.json` needs `name`, `version_number` (whatever — `package` overwrites it from
`VERSION`), `website_url`, `description` and the BepInEx pack in `dependencies`.
`package/README.md` is the Thunderstore page and `package/CHANGELOG.md` its changelog (see
[docs/releasing.md](../docs/releasing.md)); `package` refuses to build a zip without either. Preview
images go in the top level `images/`, not in the mod. Then `./scripts/deploy.sh <Name>`.
