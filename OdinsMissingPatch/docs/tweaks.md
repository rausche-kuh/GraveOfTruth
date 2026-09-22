# The tweaks

What each tweak changes, in registration order (`Tweaks` in `src/OdinsMissingPatch.cs`), with its
defaults and whether it touches anything but the local client. `package/README.md` says the same
for players; this is the developer's index — the deep notes live in the docs linked per entry.

**Scope** is one of: *client* (nothing leaves the machine, no server install), *world state*
(writes a ZDO — needs the mod on whichever client owns the object), *character* (writes the
player's own save).

| Tweak | Scope | Deep notes |
| --- | --- | --- |
| StationRange | client | [building-and-world](building-and-world.md) |
| ComfortRange | client | [comfort-and-healing](comfort-and-healing.md) |
| EndlessFuel | world state | [building-and-world](building-and-world.md) |
| MistClearRange | client | [building-and-world](building-and-world.md) |
| CombatStamina | client | [stamina](stamina.md) |
| InstantComfort | client | [comfort-and-healing](comfort-and-healing.md) |
| FiresideHealing | client | [comfort-and-healing](comfort-and-healing.md) |
| FastPortals | client | [death-and-portals](death-and-portals.md) |
| KeepGearOnDeath | client | [death-and-portals](death-and-portals.md) |
| AreaRepair | world state (game's own RPC) | [building-and-world](building-and-world.md) |
| NearbyCrafting | world state (chests) | [chests](chests.md) |
| QuickStack | world state (chests) | [chests](chests.md) |
| NearbyFuel | world state (chests) | [chests](chests.md) |
| AddAll | world state (chests, station RPCs) | [chests](chests.md) |
| AutoRepair | client | [building-and-world](building-and-world.md) |
| ChestButtons | world state (the open chest) | [inventory-ui](inventory-ui.md) |
| InventoryButtons | client (chest writes go to the open chest) | [inventory-ui](inventory-ui.md), [item-order](item-order.md) |
| PowerPicker | character | [radial-menu](radial-menu.md) |
| EquipWhileRunning | client | [equipping](equipping.md) |

## Shipped (0.1.0)

- **Station range** — a crafting station's build/craft/repair radius, and how far its extensions
  may stand from it. Doubled by default, configurable. The area marker circle grows with it.
- **Comfort range** — the radius Rested counts furniture in. Doubled by default.

## Shipped (0.2.0)

- **Endless fuel** — every `Fireplace` (campfires, hearths, torches, braziers, the hot tub) is kept
  topped up, so nothing that burns fuel for light goes out. The only tweak of the first three that
  writes world state (the fuel on the fire's ZDO), owner only.
- **Mist clear range** — every `Demister` (wisplight ball, wisp torches, anything else that clears
  the Mistlands mist) clears a wider circle, doubled by default.
- **Combat stamina** — sprinting, jumping, swimming, sneaking, building, chopping, mining and
  weapon swings cost nothing while nothing hostile is within 25m and nothing that has noticed the
  player is coming for them; the bar also refills while swimming and mid swing. One switch per cost.
- **Instant comfort** — sitting down by a fire grants Rested at once, for the comfort of the spot,
  instead of after the ten seconds of Resting.
- **Fireside healing** — the game's ten second food regen tick also heals `comfort level ×
  HealthPerComfortLevel` (2 by default) while the player is Resting, sitting by default. The amount
  is folded into the tick's own `Heal` call so it shows as one number, and is healed on its own when
  no food is eaten and the game heals nothing.
- **Fast portals** — a portal trip ends as soon as the screen is black and the other side is
  loaded, not after the fixed eight seconds; the fade is shorter too.
- **Keep gear on death** — items of a configurable list of types (weapons, armour, ammo, tools,
  utility, trinkets, consumables by default) stay in the inventory and stay equipped when the player
  dies; only the rest goes to the grave. Only on a world whose death penalty is Casual, the lowest
  step of the slider (`GlobalKeys.DeathKeepEquip`); inert on any harsher one. Owner-only code path.
- **Area repair** — one `Player.Repair` swing carries on to every damaged `Piece` within a
  configurable radius (10m) of the one the player aims at, closest first, at the game's own cost per
  piece. It writes through the game's own `WearNTear.Repair` RPC, so it needs no server install.
- **Nearby crafting** — the game's requirement checks and spends see the player-placed chests
  within a configurable range (20m) as part of the backpack, backpack paying first. An ingredient
  amount the chests have to pay for shows yellow, and its tooltip lists carried vs. in chests.
- **Quick stack** — a hotkey (`.` by default; G is bound by the game) moves every carried stack into
  the nearest chest in range that already holds that item, with a three-second glow and a floating
  count per chest. An Alt-click in the player's own inventory marks a stack as a favourite (golden
  frame), which quick stacking skips, as it does equipped items and the hotbar; the mark exists only
  in that inventory and is stripped from every stack that leaves it.
- **Nearby fuel** — the four manual add-fuel interactions (fire, smelter, oven, shield generator)
  see the chests the same way, so a unit comes out of a chest when the backpack has none; nothing
  refuels itself.
- **Add all** — Shift + Use on a `Fireplace`, a `Smelter` switch (ore or fuel), a `CookingStation`
  (fuel switch, food switch or the spit itself), a `ShieldGenerator` switch or a `Turret` puts in
  min(room under the cap, carried) units through the station's own add RPC, one call per unit. Fuel,
  ore, food and bolts alike are counted and paid through a reach of its own, so the backpack pays
  first and the chests around the player pay the rest. It hands the Use back to the game whenever
  the game would do exactly the same, so the vanilla messages explain a full station or an empty
  backpack.
- **Auto repair** — pressing Use on a crafting station repairs every worn item in the inventory that
  station could repair, asking the crafting panel's own `CanRepair` per item, instead of one item
  per click of the repair button. Repairing is free in vanilla, so there is nothing to pay.
- **Chest buttons** — the chest panel's Take all and Stack all give way to five icon buttons placed
  beside the panels: fill your stacks from the chest (in the column beside the inventory panel,
  shared with Inventory buttons); take all, place all, fill the chest's stacks from the backpack and
  sort the chest, in a column beside the chest panel. The two that put things in skip worn gear,
  favourites and (a switch) the hotbar.
- **Inventory buttons** — stack nearby (quick stacking by click, shown while that tweak is on and no
  chest is open) and sort, in the same column. The sort merges stacks and lays out by kind, name and
  quality, leaving favourites and, by default, the hotbar in place; materials come first by whether a
  portal carries them, then by family and depth, then by name.
- **Power picker** — a ninth element in the radial menu's top level, a Forsaken powers group whose
  sub menu holds one element per power whose boss has fallen, with the power's own `StatusEffect`
  icon; picking one calls `Player.SetGuardianPower`, the same call the sacrificial stone makes.

## Unreleased

- **Equip while running** — the equip queue survives a sprint. `Player.CheckRun` wipes it on
  every sprinting tick in vanilla, so a hotbar press for anything with an equip duration only
  lands once the player slows down; the wipe is dropped for equips and unequips under a flag
  set while `CheckRun` runs, and still drops a queued crossbow reload. Attack, jump and dodge
  clear the queue as before.

Each chest tweak is kept out of a chest by that chest's "Nearby use" button in the chest panel.
