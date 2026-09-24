# The scripts

Six scripts, twice: `*.ps1` for Windows PowerShell, `*.sh` for bash. They share `lib.ps1` /
`lib.sh` and the gitignored state they produce — `lib/`, `decompiled/`, `Valheim.props`, `dist/` —
so the two sets are interchangeable on the same checkout.

| | |
| --- | --- |
| `setup` | Find Valheim + BepInEx, stage the reference assemblies into `lib/`, write `Valheim.props`. |
| `deploy` | Build and install into your mod manager profile. |
| `bump` | Raise a mod's version for a release and close off its changelog. |
| `package` | Build the Thunderstore zips in `dist/`. |
| `decompile` | Dump the game's own C# into `decompiled/` for API lookup. |
| `clean` | Delete what the other five produced. |

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
./scripts/deploy.sh
./scripts/bump.sh
./scripts/package.sh
./scripts/decompile.sh
./scripts/clean.sh
```

`deploy`, `package` and `clean` take mod names and act on every mod in the repo when given none:
`./scripts/deploy.sh GraveOfTruth` builds just that one. `dotnet build GraveOfTruth/GraveOfTruth.csproj`
also works once `setup` has run.

## What you need

| | |
| --- | --- |
| .NET SDK 8 | `winget install Microsoft.DotNet.SDK.8`, `sudo pacman -S dotnet-sdk`, `sudo apt install dotnet-sdk-8.0`, `sudo dnf install dotnet-sdk-8.0` |
| Valheim | Installed through Steam. On Linux, the native build. |
| BepInEx | Installed into a profile by a mod manager — [Gale](https://github.com/Kesomannen/gale) or r2modman. |
| binutils | Linux only, for `strings`; used just to read the Unity and BepInEx versions. Optional. |
| `python3` or `zip` | Linux only, for `package.sh`. |

No Mono, no Wine on Linux: the game assemblies are `net472` references and the NuGet package
`Microsoft.NETFramework.ReferenceAssemblies` supplies the framework, so the .NET 8 SDK builds the
plugins on its own.

## `setup` — find the game, stage the references

```powershell
.\scripts\setup.ps1 [-ValheimDir DIR] [-GaleProfile NAME]
```
```bash
./scripts/setup.sh [--valheim-dir DIR] [--profile NAME]
```

Locates the game and BepInEx, copies the reference assemblies into `lib/`, writes `Valheim.props`,
and restores every mod. Re-run it after a Valheim update — `lib/` is wiped and re-staged.

It searches, in order:

- **Valheim** — the Steam install from the registry on Windows, and on Linux every Steam library it
  can find: `~/.steam/steam`, `~/.steam/root`, `~/.local/share/Steam`, the Flatpak Steam under
  `~/.var/app/com.valvesoftware.Steam/`. Both platforms also read the extra libraries listed as
  `"path"` entries in `steamapps/libraryfolders.vdf`. A directory is the game if it has
  `valheim_Data/Managed/assembly_valheim.dll`.
- **BepInEx** — the game directory first, then every mod manager profile: Gale
  (`%APPDATA%\com.kesomannen.gale\valheim\profiles\`,
  `~/.local/share/com.kesomannen.gale/valheim/profiles/`) and r2modman
  (`%APPDATA%\r2modmanPlus-local\Valheim\profiles\`,
  `~/.config/r2modmanPlus-local/Valheim/profiles/`), Flatpak variants included. The newest
  `BepInEx.dll` across all of them wins.

`Valheim.props` gets a `<ValheimDir>` and a `<ProfileDir>`; `deploy` installs each mod into
`<ProfileDir>/BepInEx/plugins/rauschekuh-<mod>/`. On Windows the profile defaults to Gale's
`Default` and `-GaleProfile` picks another; on Linux it defaults to whichever profile BepInEx was
found in — usually the one you actually play — and `--profile <name>` picks another.

## `deploy` — build and install

```powershell
.\scripts\deploy.ps1 [mod ...] [-Configuration Debug|Release] [-ProfileDir DIR]
```
```bash
./scripts/deploy.sh [-c Debug|Release] [--profile-dir DIR] [mod ...]
```

Builds (Release by default; `Debug` also compiles each mod's `src/Dev/` test helpers) and copies each mod's DLL, everything in its `assets/`, its
`manifest.json` and its `icon.png` into the profile. The profile override is for a one-off install
against a second profile without re-running `setup`.

## `bump` — raise a version for a release

```powershell
.\scripts\bump.ps1 [mod] [major|minor|patch] [-Yes]
```
```bash
./scripts/bump.sh [mod] [major|minor|patch] [-y]
```

Asks which mod and which part of `major.minor.patch` to raise — both menus are skipped when given
as arguments — then shows the new version, what the release would ship and waits for a `y`. On
confirmation it writes all three places a version lives:

- the `VERSION` const in the mod's plugin source, which is the source of truth,
- `version_number` in its `package/manifest.json`,
- the `## Unreleased` heading in its `package/CHANGELOG.md`, which becomes `## <version>`.

A minor bump zeroes the patch, a major one zeroes both. If there is nothing under `## Unreleased`
it says so before asking, since that release would show up on Thunderstore with no notes. Nothing
is built, committed or published — run `package` afterwards and upload the zip.

## `package` — Thunderstore zips

```powershell
.\scripts\package.ps1 [mod ...]
```
```bash
./scripts/package.sh [mod ...]
```

Reads each mod's `VERSION` const from its `src/`, stamps it into its `package/manifest.json`,
builds Release, and writes a flat `dist/<Mod>-<version>.zip` containing the DLL, the assets and the
mod's `package/` folder — `manifest.json`, `icon.png`, `README.md` and `CHANGELOG.md`. On Linux it
uses `zip` if present, otherwise `python3`.

The shipped README is `<Mod>/package/README.md`, and it is the Thunderstore page: what the mod does,
how to install it, multiplayer and compatibility notes. Nothing about building from source and no
links relative to the repo — Thunderstore renders it standalone, so a `../` link is a dead link.
`<Mod>/package/CHANGELOG.md` sits next to it and becomes the Changelog tab on the mod page: a
`## <version>` section per release, newest first, with `## Unreleased` on top for what has not
shipped yet. `<Mod>/README.md` is the dev facing one and is not shipped.

Only bump `VERSION` for an actual Thunderstore release, and use `bump` above to do it.
Check `dependencies` in that mod's `package/manifest.json` against the current BepInEx pack first.

## `decompile` — read the game's API

```powershell
.\scripts\decompile.ps1 [-Force] [assembly ...]
```
```bash
./scripts/decompile.sh [--force] [assembly ...]
```

Dumps the game's own C# into `decompiled/<assembly>/` so you can grep for game types. Defaults to
`assembly_valheim` and `assembly_utils`; skips anything already decompiled unless forced. Shared by
every mod.

Installs `ilspycmd` 9.1.0.7988 as a global tool on first run (pinned — 10.x and newer are net10.0
tools and refuse to install on the .NET 8 SDK). It is invoked by its full path, so `~/.dotnet/tools`
does not need to be on your `PATH`; on Linux set `DOTNET_TOOLS_DIR` if yours lives elsewhere.
Remove it again with `dotnet tool uninstall -g ilspycmd`.

Note that `Assembly-CSharp.dll` is a ~23 KB stub — the game's code is in `assembly_valheim`.

## `clean` — undo the rest

```powershell
.\scripts\clean.ps1 [mod ...] [-Deployed] [-All]
```
```bash
./scripts/clean.sh [--deployed] [--all] [mod ...]
```

Removes each mod's `bin/` and `obj/` plus `dist/`. `-Deployed` / `--deployed` also removes the mods
from the profile they were installed into, and `-All` / `--all` also removes the shared `lib/`,
`decompiled/` and `Valheim.props` — after that, `setup` has to run again before anything builds.

## Adding a mod

A mod is any top level directory holding `<Name>/<Name>.csproj`; the scripts discover them that way,
so nothing needs registering. Create:

```
<Name>/
  <Name>.csproj      AssemblyName + RootNamespace only - the build lives in Directory.Build.props
  src/<Name>.cs      the BaseUnityPlugin, with the GUID/NAME/VERSION consts
  assets/            optional, shipped next to the DLL
  package/           what Thunderstore gets: manifest.json, icon.png, README.md, CHANGELOG.md
  README.md          dev facing, not shipped - what the mod is, how to build it
```

`package/manifest.json` needs `name`, `version_number` (whatever — `package` overwrites it from
`VERSION`), `website_url`, `description` and the BepInEx pack in `dependencies`.
`package/README.md` is the Thunderstore page and `package/CHANGELOG.md` its changelog (see `package`
above); `package` refuses to build a zip without either. Then `./scripts/deploy.sh <Name>`.

## Where things end up

| | |
| --- | --- |
| Game | `C:\Program Files (x86)\Steam\steamapps\common\Valheim`, `~/.local/share/Steam/steamapps/common/Valheim`, or another library |
| Game assemblies | `<game>/valheim_Data/Managed/` |
| Gale profiles | `%APPDATA%\com.kesomannen.gale\valheim\profiles\<profile>\`, `~/.local/share/com.kesomannen.gale/valheim/profiles/<profile>/` |
| r2modman profiles | `%APPDATA%\r2modmanPlus-local\Valheim\profiles\<profile>\`, `~/.config/r2modmanPlus-local/Valheim/profiles/<profile>/` |
| Installed plugin | `<profile>/BepInEx/plugins/rauschekuh-<mod>/` |
| BepInEx log | `<profile>/BepInEx/LogOutput.log` |

`lib/`, `decompiled/`, `dist/`, every `<mod>/bin/`, `<mod>/obj/` and `Valheim.props` are generated
and gitignored. Game assemblies must never be committed.

## When it goes wrong

**`Valheim not found`** — the game is on a drive whose Steam library isn't registered, or on Linux
you are on the Proton/Windows build. Point at it directly:

```bash
./scripts/setup.sh --valheim-dir ~/Games/SteamLibrary/steamapps/common/Valheim
```

**`BepInEx not found`** — install the BepInEx pack for Valheim in your mod manager and launch the
game once, so the profile is populated. A freshly created profile is an empty directory until then.

**`No .NET SDK found`** — install the SDK, not just the runtime; `dotnet --list-sdks` has to print a
version.

**`unknown mod '...'`** — the directory name and the `.csproj` name have to match exactly.

**Build errors about missing game types** — `lib/` is stale after a Valheim update. Re-run `setup`.

**The mod doesn't load in game** — check that it landed in the profile you actually launch (`setup`
prints `Profile : …`), and read `<profile>/BepInEx/LogOutput.log`.

**`BadImageFormatException: Method has zero rva`**, with garbled method names in the stack trace,
after a deploy — the game was running when the DLL was copied over. Mono reads method bodies out of
the file lazily, so the running game read the new file at the old offsets. Nothing is wrong with the
build: restart the game. `deploy` warns when it sees Valheim running.
