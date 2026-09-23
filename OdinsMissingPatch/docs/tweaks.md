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
| AutoShield | client | [equipping](equipping.md) |
| PocketUpgrades | client | [trader](trader.md) |
| SharedMapTable | world state (map tables, the game's own write) | [map-pins](map-pins.md) |
| AutoPins | character (the removed-pin record); a routed RPC to the other clients | [map-pins](map-pins.md) |
| PinLooks | client | [map-pins](map-pins.md) |
| DeathPins | client | [map-pins](map-pins.md) |

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
  loaded, not after the fixed eight seconds; the fade is shorter too. Dungeon doors
  (`InstantDungeonDoors`, on by default) skip the black screen entirely when the inside is loaded.
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
  in that inventory and is stripped from every stack that leaves it. The same Alt-click in an open
  chest's grid marks the *chest* for that kind of item (`ChestFavorites`, on the chest's ZDO): a
  marked chest counts as holding it, and is filled before the chests that do. The marks get no
  border of their own; the chest panel's Clear favourites button lists them in its tooltip. Read by
  Chest buttons' Fill the chest's stacks as well, so either tweak alone is enough for the marks to
  mean something.
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
  shared with Inventory buttons), which tops the backpack's stacks up to their caps and opens no
  new one; take all, place all, fill the chest's stacks from the backpack and
  sort the chest, in a column beside the chest panel. The two that put things in skip worn gear,
  favourites and (a switch) the hotbar; Fill the chest's stacks also takes the kinds the chest is
  marked for, whether or not it holds any.
- **Inventory buttons** — stack nearby (quick stacking by click, shown while that tweak is on and no
  chest is open) and sort, in the same column. The sort merges stacks and lays out by kind, name and
  quality, leaving equipped items, favourites and, by default, the hotbar in place; materials come
  first by whether a portal carries them, then by family and depth, then by name.
- **Power picker** — a ninth element in the radial menu's top level, a Forsaken powers group whose
  sub menu holds one element per power whose boss has fallen, with the power's own `StatusEffect`
  icon; picking one calls `Player.SetGuardianPower`, the same call the sacrificial stone makes.

## Unreleased

- **Equip while running** — the equip queue survives a sprint. `Player.CheckRun` wipes it on
  every sprinting tick in vanilla, so a hotbar press for anything with an equip duration only
  lands once the player slows down; the wipe is dropped for equips and unequips under a flag
  set while `CheckRun` runs, and still drops a queued crossbow reload. Attack, jump and dodge
  clear the queue as before.
- **Auto shield** — equipping a one handed weapon raises a shield with it, when the off hand comes
  out of the equip empty. A `Player.ToggleEquipped` prefix records the weapon a press is for, and a
  `Humanoid.EquipItem` postfix acts on that item alone, so restoring gear at login, taking hands
  back out and a drag in the inventory never arm anything; a shield or a torch already in the hand
  survives the weapon and is left alone. The shield is picked favourites first, then the hotbar row,
  then the rest of the backpack, first slot within each, and is equipped through `ToggleEquipped`
  so it takes its own duration and can be interrupted like any equip.
- **Pocket upgrades** — which boss each of Haldor's two extra inventory rows waits for is a
  setting: Wider Pockets, `defeated_dragon` (Moder) in vanilla, is offered once the Elder has
  fallen by default, and Deeper Pockets keeps the Queen. Only the gate moves — the upgrades are
  still bought from Haldor at their own price, once per character, for one row each. The item's own
  `m_requiredGlobalKey` is rewritten from the vanilla one kept aside per item, in a prefix on
  `Trader.GetAvailableItems`, so the game's own filter still asks the question.
- **Shared map table** — a cartography table syncs by itself, silently, within `SyncRange` (64m):
  it is read whenever it holds data this client has not read (arrival, someone else's write), and
  written on arrival and when a saved pin changes while in range — only if the table actually
  lacks something of this map, and never in answer to someone else's write, since that looped.
  At most every `MinInterval` (30s) per table. A table behind a ward the player has no access to
  is only read.
- **Auto pins** — dungeon entrances, struck ore deposits, and places found by rule
  (generated camps and villages, tar pits, dragon eggs; never a vegvisir ruin) or listed in `PlaceList`
  get an ordinary map pin with a vanilla icon, owned by nobody (`UniversalPins`), so a table
  hands each one to everyone exactly once. Locations within `DiscoverRange` (40m) every three
  seconds; ore from the strike itself (`ExtraOre` adds soft tissue, `SkipOre` drops tin). No pin
  within `PinSpacing` (10m) of a saved non-universal, non-death pin. Portals are not pinned — a
  separate feature to come. A mined-out deposit's pin is ticked when you
  come by (`MinedOut`: `Tick`, `Remove` or `Keep`). A right click removes one for good (recorded
  on the character, per world). With `Share` (on) every pin made here goes to every player
  online the moment it is made, as a routed RPC (`PinBroadcast`), and a player who joins asks
  everyone for theirs once; the receiver applies its own switches, icons, spacing and removals.
  Players without the mod still get the pins from a table.
- **Pin looks** — those pins are tinted by the biome they stand in instead of the shared-pin
  grey, and ore and place pins hide beyond zoom 0.5 on the large map. `MapToggles` adds a toggle
  per category above the large map's "visible to other players" one; each sets `ShowDungeons`,
  `ShowOre` or `ShowPlaces`, which hide that category on both maps.
- **Death pins** — a death pin within 32m whose grave (a `TombStone` the local player owns) is not
  within 8m of it on two sweeps in a row is removed (`RemoveWithGrave`); a death that set up no
  grave has its pin removed right after `Player.OnDeath` (`OnlyWithGrave`).

Each chest tweak is kept out of a chest by that chest's "Nearby use" button in the chest panel,
which only a chest a player placed has: found chests and graves are never used from afar.
