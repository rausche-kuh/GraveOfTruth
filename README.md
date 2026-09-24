# Valheim mods

| Mod                                     | What it does                                                                                                                                 |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| [GraveOfTruth](GraveOfTruth/)           | Dying calls down the obliterator's lightning on your grave and plays a loser jingle out of it, echo, thunderstorm and all.                   |
| [OdinsMissingPatch](OdinsMissingPatch/) | Small quality of life changes, each one configurable: wider crafting station, comfort and mist clearing ranges, fires that never go out, stamina that only drains in combat. |
| [ThisIsValheim](ThisIsValheim/)         | Doors are kicked open, not opened — the game's own bare handed kick bursts a door open four times faster than it should, with a battering ram's impact in its face. |
| [ImmersiveEntrance](ImmersiveEntrance/) | Dungeon entrances are no longer a black wall: the doorway shows the dungeon behind it, torches lit. Proof of concept. |
| [OdinsTree](OdinsTree/)                 | Bless a tree with the hammer and it never falls, so the tree house on it is safe — even without the mod. |
| [OdinsCompass](OdinsCompass/)           | A compass worn like the wishbone: wavy lines of blue light blow toward the nearest boss, and each upgrade teaches it a biome's dungeons and ore. Works on any server. What comes next: its [roadmap](OdinsCompass/ROADMAP.md). |
| [OdinsPaths](OdinsPaths/)               | Read a vegvisir, sleep, and a path winds from home to the boss altar along the easiest ground, shore to shore. Server side. Planned: see its [roadmap](OdinsPaths/ROADMAP.md). |

## Quick start

```bash
./scripts/setup.sh    # find Valheim + BepInEx, stage reference assemblies into lib/
./scripts/deploy.sh   # build every mod and install it into your profile
```

`scripts\setup.ps1` and `scripts\deploy.ps1` are the same thing on Windows.
[scripts/README.md](scripts/README.md) documents all six scripts, their flags, how to add a new
mod, and what to do when something goes wrong.

## Layout

```
GraveOfTruth/          one mod: <Name>/<Name>.csproj, src/, assets/, package/, README.md
  package/             manifest.json, icon.png, the Thunderstore page README.md and CHANGELOG.md
OdinsMissingPatch/     the same shape, minus assets/
ThisIsValheim/         the same shape, minus assets/
ImmersiveEntrance/     the same shape, minus assets/
OdinsTree/             the same shape, minus assets/
OdinsCompass/          the same shape
OdinsPaths/            the same shape, minus assets/ (a scaffold so far)
scripts/               setup, deploy, bump, package, decompile, clean (.sh and .ps1)
Directory.Build.props  the build every mod shares
lib/                   game + BepInEx reference assemblies   (generated, gitignored)
decompiled/            the game's own C#, for API lookup      (generated, gitignored)
dist/                  Thunderstore zips                      (generated, gitignored)
```
