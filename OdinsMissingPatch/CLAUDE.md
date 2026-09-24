# OdinsMissingPatch

A collection of quality of life changes, each one its own configurable tweak. See the root
`CLAUDE.md` for the shared build, the scripts and the environment.

Twenty-five tweaks are in the source; `docs/tweaks.md` is the index of what each one does, its scope
(client side, world state, character) and which deep note covers it. Most are client side: the
chest tweaks, `EndlessFuel` and `SharedMapTable` write world state, `PowerPicker` and `AutoPins`
write the character.

| Path                       | What                                                                                                                                                                                                 |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/OdinsMissingPatch.cs` | BepInEx entry point: binds every tweak's config, then patches all.                                                                                                                                   |
| `src/Tweak.cs`             | The base class: the section, the `Enabled` switch, `BindMultiplier`, `OnSettingChanged`.                                                                                                             |
| `src/Tweaks/<Name>.cs`     | One quality of life change, with its `[HarmonyPatch]` classes nested inside it.                                                                                                                      |
| `src/NearbyChests.cs`      | Shared by the chest tweaks: the registry of loaded containers, the in-reach rule, `Claim`, the "reach" that widens the backpack, the per-chest opt-out flag, and the chest panel's two text buttons. |
| `src/ChestFavorites.cs`    | The kinds of item a chest is marked to take, on its ZDO: read by QuickStack and ChestButtons, set by an Alt-click in the chest's grid, listed in the Clear favourites button's tooltip.              |
| `src/ChestGlow.cs`         | The golden pulse plus floating text on a chest (`ChestGlow.Flash`).                                                                                                                                  |
| `src/Hotkeys.cs`           | `Pressed` / `Held` for a `KeyboardShortcut`, read through `ZInput`.                                                                                                                                  |
| `src/PanelButtons.cs`      | Icon buttons for the inventory screen, cut from the chest panel's Take all button, and where a column beside a panel is. Used by ChestButtons, InventoryButtons and the chest panel's text buttons.  |
| `src/InventorySorter.cs`   | Merge-and-sort of an `Inventory` in place, from a given row down, around items a caller keeps.                                                                                                       |
| `src/MaterialOrder.cs`     | The crafting tree derived from `ObjectDB`: which family a material belongs to and how deep it lies.                                                                                                  |
| `src/UniversalPins.cs`     | Map pins that belong to nobody (a fixed owner, an `OdinsMissingPatch_<category>` author): the identity, adding, the removed-pin record, and the patches that keep them through a table read and turn a claim into a tick. Used by AutoPins, SharedMapTable and PinLooks. |
| `src/PinBroadcast.cs`      | The routed RPCs that hand an auto pin to every player online the moment it is made, and that give a joining player everyone's pins once. Sends for AutoPins, receives into it.                   |
| `src/Translations.cs`      | Hands `assets/translations.csv` to the game's localization on every language setup.                                                                                                                  |
| `assets/icons/`            | The button icons, 64px white-on-transparent PNGs, shipped beside the DLL. Gale flattens the folder on install, so `PanelButtons.Icon` looks in `icons/` and then beside the DLL.                     |
| `assets/translations.csv`  | Every word the mod shows, one row per `$omp_` token, one column per language                                                                                                                         |
| `ROADMAP.md`               | What comes next, with the research each entry needs first, and the known bugs.                                                                                                                       |
| `package/`                 | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`).                                                           |

## The rules that always apply

- A tweak is an `internal sealed class : Tweak` with a private constructor and a
  `static readonly Instance`, listed in `Tweaks` in `src/OdinsMissingPatch.cs`. That list is the
  only registration; nothing scans the assembly for tweaks.
- Patches are nested in their tweak, not in the plugin class — the root convention, one level down,
  so a tweak is one file holding both its settings and the code they drive.
- **Every patch is applied at startup regardless of the config**, and asks `Instance.On` (and reads
  its multipliers) each time it runs. That is what makes the switches work without a restart; never
  make patching itself conditional.
- Null-guard everything: `Player.m_localPlayer` is frequently null.
- Nothing the player reads is a literal in the code: a call site passes a `$omp_` token and
  `assets/translations.csv` holds the words. Config descriptions are the exception and stay
  English — see [`docs/translations.md`](docs/translations.md).
- Read the doc for the area before changing it — each one holds both the conventions that area
  follows and the game facts they were derived from, so it is where a change is checked and where
  what a change taught goes back.

## Where the details live

| Read before                                | Doc                                                                                                                         |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| adding or changing any tweak               | [`docs/conventions.md`](docs/conventions.md) — tweak shape, config, multipliers and rescaling, the patch shapes             |
| looking up what a tweak does               | [`docs/tweaks.md`](docs/tweaks.md) — one entry per tweak, defaults and scope                                                |
| anything reaching into chests              | [`docs/chests.md`](docs/chests.md) — the reach, `Claim`, container ZDOs, requirement checks, refuelling, the two favourites |
| anything drawn in the inventory screen     | [`docs/inventory-ui.md`](docs/inventory-ui.md) — panel buttons, panel geometry, `InventoryGui`, the sorter                  |
| any word a player reads                    | [`docs/translations.md`](docs/translations.md) — the tokens, the CSV, what translates itself and what does not              |
| sorting or classifying items               | [`docs/item-order.md`](docs/item-order.md) — `MaterialOrder`, the `ObjectDB` crafting tree                                  |
| stations, fires, demisters, repairing      | [`docs/building-and-world.md`](docs/building-and-world.md)                                                                  |
| stamina costs and what counts as hostile   | [`docs/stamina.md`](docs/stamina.md)                                                                                        |
| Rested, comfort and healing                | [`docs/comfort-and-healing.md`](docs/comfort-and-healing.md)                                                                |
| portals and the death path                 | [`docs/death-and-portals.md`](docs/death-and-portals.md)                                                                    |
| the radial menu and guardian powers        | [`docs/radial-menu.md`](docs/radial-menu.md)                                                                                |
| equipping, the hotbar, the equip queue     | [`docs/equipping.md`](docs/equipping.md)                                                                                    |
| the trader's shelf and inventory rows      | [`docs/trader.md`](docs/trader.md) — the `Trader` shelf, how a gate is moved, the two pocket upgrades, `invrows` |
| the map, its pins, or map tables           | [`docs/map-pins.md`](docs/map-pins.md) — shared tables, auto pins, universal pins and pin looks, plus the map facts they rest on |
| touching ground another mod already covers | [`docs/references.md`](docs/references.md) — the reference checkouts, what each solves and how this mod differs             |
