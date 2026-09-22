# OdinsMissingPatch

A collection of quality of life changes, each one its own configurable tweak. See the root
`CLAUDE.md` for the shared build, the scripts and the environment.

Twenty tweaks are in the source; `docs/tweaks.md` is the index of what each one does, its scope
(client side, world state, character) and which deep note covers it. Most are client side: the
chest tweaks and `EndlessFuel` write world state, `PowerPicker` writes the character.

| Path | What |
| --- | --- |
| `src/OdinsMissingPatch.cs` | BepInEx entry point: binds every tweak's config, then patches all. |
| `src/Tweak.cs` | The base class: the section, the `Enabled` switch, `BindMultiplier`, `OnSettingChanged`. |
| `src/Tweaks/<Name>.cs` | One quality of life change, with its `[HarmonyPatch]` classes nested inside it. |
| `src/NearbyChests.cs` | Shared by the chest tweaks: the registry of loaded containers, the in-reach rule, `Claim`, the "reach" that widens the backpack, the per-chest opt-out flag. |
| `src/ChestGlow.cs` | The golden pulse plus floating text on a chest (`ChestGlow.Flash`). |
| `src/Hotkeys.cs` | `Pressed` / `Held` for a `KeyboardShortcut`, read through `ZInput`. |
| `src/PanelButtons.cs` | Icon buttons for the inventory screen, cut from the chest panel's Take all button. Used by ChestButtons, InventoryButtons and the Nearby use button. |
| `src/InventorySorter.cs` | Merge-and-sort of an `Inventory` in place, from a given row down, around items a caller keeps. |
| `src/MaterialOrder.cs` | The crafting tree derived from `ObjectDB`: which family a material belongs to and how deep it lies. |
| `assets/icons/` | The button icons, 64px white-on-transparent PNGs, shipped beside the DLL. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

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
- Read the doc for the area before changing it — each one holds both the conventions that area
  follows and the game facts they were derived from, so it is where a change is checked and where
  what a change taught goes back.

## Where the details live

| Read before | Doc |
| --- | --- |
| adding or changing any tweak | [`docs/conventions.md`](docs/conventions.md) — tweak shape, config, multipliers and rescaling, the patch shapes |
| looking up what a tweak does | [`docs/tweaks.md`](docs/tweaks.md) — one entry per tweak, defaults and scope |
| anything reaching into chests | [`docs/chests.md`](docs/chests.md) — the reach, `Claim`, container ZDOs, requirement checks, refuelling, the favourite flag |
| anything drawn in the inventory screen | [`docs/inventory-ui.md`](docs/inventory-ui.md) — panel buttons, panel geometry, `InventoryGui`, the sorter |
| sorting or classifying items | [`docs/item-order.md`](docs/item-order.md) — `MaterialOrder`, the `ObjectDB` crafting tree |
| stations, fires, demisters, repairing | [`docs/building-and-world.md`](docs/building-and-world.md) |
| stamina costs and what counts as hostile | [`docs/stamina.md`](docs/stamina.md) |
| Rested, comfort and healing | [`docs/comfort-and-healing.md`](docs/comfort-and-healing.md) |
| portals and the death path | [`docs/death-and-portals.md`](docs/death-and-portals.md) |
| the radial menu and guardian powers | [`docs/radial-menu.md`](docs/radial-menu.md) |
| equipping, the hotbar, the equip queue | [`docs/equipping.md`](docs/equipping.md) |
| touching ground another mod already covers | [`docs/references.md`](docs/references.md) — the reference checkouts, what each solves and how this mod differs |
