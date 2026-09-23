# Odin's Missing Patch

> **Warning:** this mod is developed heavily with the use of AI, and many of its ideas and even source
> code is copied from other mods.

The patch Odin forgot: my personal take on the quality of life changes Valheim should have shipped
years ago, in one mod. Nothing needs a server install.

## Changes beyond QoL

Some tweaks go beyond QoL and change how the game plays, and I ship them on by default
because I want them, not because I can argue they are neutral:

- **Combat stamina** is the big one. Eliminating stamina usage outside of combat removes a huge
  part of Valheim's exploration. This drastically simplifies climbing mountains and allows for
  swimming in the ocean.
- **Keep gear on death** removes the stakes of dying on a world already set to the Casual death
  penalty. The corpse run is a real punishment, but not one I have the time for.
- **Endless fuel** deletes the coal-and-wood upkeep of a lit base. Small, but it is an economy.
- **Pocket upgrades** hands you Haldor's first extra inventory row two bosses early. It is a
  progression change, not a QoL one: the cramped backpack of the Swamp and the Mountains is meant
  to be part of the game, and I would rather spend those hours on the game's other ideas.

## What it does

- **Station range** — scales a crafting station's build/craft/repair radius, and how far an
  extension may stand from it, by a multiplier (2 by default).
- **Comfort range** — widens the radius `Rested` counts furniture in, by the same kind of
  multiplier.
- **Endless fuel** — tops the fuel back up on every campfire, hearth, torch, brazier and hot tub,
  so nothing that burns for light goes out.
- **Mist clear range** — scales a demister's push radius (wisplight, wisp torch, anything else),
  doubled by default.
- **Combat stamina** — drops the stamina cost of sprinting, jumping, swimming, sneaking, building,
  chopping, mining and swinging while nothing hostile is within 25m and nothing that has noticed
  you is coming for you. Free swimming means no drowning unless attacked, since drowning starts
  at empty stamina.
- **Instant comfort** — sitting down grants `Rested` immediately, instead of after the game's ten
  seconds of Resting.
- **Fireside healing** — folds `comfort level × 2` health into the game's own ten-second food
  regen tick while you are Resting.
- **Fast portals** — ends the trip as soon as the screen is fully black and the far side has
  loaded, instead of the game's flat eight seconds. Dungeon and cave entrances are instant, with
  no black screen at all.
- **Keep gear on death** — makes casual death penalty even weaker: an item-type list
  (weapons, armour, ammo, tools, utility, trinkets, consumables by default) defines which stay
  in your inventory and stay equipped.
- **Area repair** — after a hammer swing lands, repeats the game's own repair on every damaged
  piece within 10m, closest first. Hold `Left Alt` for the single piece.
- **Nearby crafting** — while a craft, upgrade or build is being checked or paid for, the game
  also checks player-placed chests within 20m. Your backpack pays first, the nearest chest pays
  the rest.
- **Quick stack** — one key (`.`) pushes every carried stack into the nearest chest in range that
  already holds that item, merging into its stacks before taking a slot. Each chest that took
  something glows with a count. Equipped items, the hotbar (a switch) and favourites stay.
  `Alt`-click an item to make it a favourite.
- **Chest favourites** — `Alt`-click an item inside an open chest to mark the chest for that kind
  of item. Quick stack and Fill the chest's stacks put it there even when the chest holds none.
- **Nearby fuel** — lets the four manual add-fuel interactions (fire, smelter, oven, shield
  generator) draw their one unit from a chest when your backpack has none.
- **Add all** — `Shift` + Use on a fire, smelter, kiln, oven, cooking station, shield generator or
  ballista to fill up fuel, ore, food and bolts. Uses backpack first and the chests after.
- **Auto repair** — on Use of a crafting station repairs all repairable items.
- **Chest buttons** — replaces Take all and Stack all with five icon buttons placed beside the
  panels rather than on them: **fill your stacks** from the chest, and, down the chest's side,
  **take all**, **place all**, **fill the chest's stacks** and **sort the chest**.
- **Inventory buttons** — stack nearby and **sort**, beside your inventory. The sort merges stacks
  and orders by kind, then name, then quality, below the hotbar. What you have equipped and your
  favourites stay in the slot you put them in, and everything else is laid out around them.
- **Power picker** — adds a Forsaken powers ring to the radial menu, so you can pick a power
  without running to the sacrifice stone.
- **Equip while running** — a hotbar press while sprinting still equips the weapon, shield or armour.
- **Auto shield** — drawing a one handed weapon raises a shield with it. It picks the shield
  you marked as a favourite first.
- **Pocket upgrades** — Haldor sells the two extra inventory rows, **Wider Pockets** and **Deeper
  Pockets**, once the boss you choose has fallen in that world. Wider Pockets waits for the Elder
  instead of Moder by default; Deeper Pockets keeps the Queen.
- **Shared map table** — come within 64m of a cartography table and your map is written onto it
  and its map onto yours, silently, with no click. Again when a pin of yours changes nearby or
  someone else has written to it, never more than every ten seconds.
- **Auto pins** — dungeon and cave entrances, ore deposits you strike (not tin, which is
  everywhere; soft tissue from the Mistlands giants counts) and places (fuling villages and other ruined settlements, tar pits, dragon eggs,
  Dvergr excavations and watchtowers, ...) get an ordinary map pin when you come within
  40m. It works out what counts from the game's own data, so a new biome needs no update. The pins belong to nobody: a map table shares each
  one exactly once, the large map's shared-map button hides them all, a first click ticks one off,
  and a right click removes it for good. A mined-out deposit's pin is crossed off when you pass it
  (or removed, if you prefer). Nothing is pinned within 10m of a pin you already have, so maps
  from another pin mod are not doubled up.
- **Pin looks** — those pins are coloured by their biome, and ore and place pins hide when the
  large map is zoomed far out. Toggles at the bottom right of the large map, beside "Visible to
  other players", show or hide dungeon, ore and place pins.
- **Death pins** — a death pin goes away by itself once your grave is gone, whoever emptied it,
  and a death that leaves no grave leaves no pin.

## Configuration

`BepInEx/config/rauschekuh.odinsmissingpatch.cfg`, written on first run. Every tweak has an
`Enabled` switch; the ranges have a multiplier (`1` is vanilla), and the rest have the settings
named above. Changes apply while the game runs, including from an in-game config manager.

## Translations (AI generated)

The mod's buttons, hover text and messages follow the language Valheim is set to. English, German,
Russian, Chinese, Spanish, French, Brazilian Portuguese, Polish, Italian, Japanese and Ukrainian
ship with it, each built on Valheim's own wording. Any other language is a column in
`translations.csv`, which sits next to the DLL in `BepInEx/plugins/rauschekuh-OdinsMissingPatch/`.
Open it in a spreadsheet or a text editor, add a column headed with the language exactly as Valheim
names it (`Dutch`, `Czech`, `Portuguese_European`, ...) and fill in the rows — anything left empty
stays English, and `$1` and `$2` are the numbers and item names the game fills in, which may stand
anywhere in the sentence. Send one over and it ships with the next version.

## Multiplayer

Client side; no server install, and nothing required of anyone else.

Most of it never leaves your machine. Four things touch the world, and each does it the way you
would by hand: **area repair** sends the game's own repair, one piece at a time. The **chest
tweaks** take a chest over for the write exactly as opening it does, skip a chest someone else has
open or that you could not open yourself, and respect the per-chest **Nearby use** switch stored
with it. **Endless fuel** writes the fuel on fires your machine owns — Valheim gives a fire to
whoever is nearest, so a fire only a modless player stands by burns down as usual. The **shared
map table** writes a table exactly as its Write button does, and never one behind a ward you
cannot use.

Auto pins travel on map tables in the game's own format, so a player without the mod reads them
from a table as ordinary shared pins. Removing one is yours alone: other players with the mod keep
theirs and bring it back to the table.

Two caveats. Client-side range tweaks reach only you: a player without the mod has vanilla range
at the same bench and sees mist close in at the vanilla distance. And **keep gear on death** goes
further than the Casual death penalty a host chose, for you — on a world set any harsher it
switches itself off, so it can never undo the penalty a host asked for.

## Install

A mod manager (Gale or r2modman), or drop the zip's contents into
`BepInEx/plugins/rauschekuh-OdinsMissingPatch/`. Requires the BepInEx pack for Valheim.

## Credits

Several tweaks cover ground others got to first, and are worth a look if you want that one thing
on its own or want it server-enforced. **Do not run both of a pair.**

- Endless fuel follows Digitalroot's
  [Eternal Fire](https://thunderstore.io/c/valheim/p/Digitalroot/Eternal_Fire/), which also covers
  ovens and smelters and is configurable per fire type.
- Mist clear range and fast portals do what Crystal Ferrai's
  [Clear The Air](https://thunderstore.io/c/valheim/p/Crystal/ClearTheAir/) and
  [Proper Portals](https://thunderstore.io/c/valheim/p/Crystal/ProperPortals/) do; both default to
  vanilla and can be enforced by a server.
- Combat stamina is modelled on Cartur's
  [Safe Stamina](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Safe_Stamina/); this one also
  counts an enemy that has noticed you, wherever it is.
- Area repair does what
  [Venture Area Repair](https://thunderstore.io/c/valheim/p/VentureValheim/Venture_Area_Repair/)
  does at a fixed 20m; here the radius and the single-repair key are settings. Azumatt's
  [AzuAreaRepair](https://valheim.hexium.gg/mods/Azumatt/AzuAreaRepair) covers the same ground.
- The chest tweaks and auto repair cover part of Zellds'
  [SmartCraft-Storage](https://thunderstore.io/c/valheim/p/Zellds/SmartCraftStorage/), which goes
  much further — stations that feed themselves, restocking, animal feeding. This one keeps every
  move yours.
- Equip while running is what blacks7ar's
  [EquipGearWhileRunning](https://thunderstore.io/c/valheim/p/blacks7ar/EquipGearWhileRunning/) got
  to first, and that one is a drop-in with nothing to configure; here it is a switch beside the
  rest.
- Auto shield follows Vapok's
  [ShieldMeBruh](https://thunderstore.io/c/valheim/p/Vapok/ShieldMeBruh/).
- Pocket upgrades follows chooweey's
  [EarlyHaldorPockets](https://valheim.hexium.gg/mods/chooweey/EarlyHaldorPockets).

- Auto pins do what Searica's
  [Discovery Pins](https://thunderstore.io/c/valheim/p/Searica/DiscoveryPins/) does, which also
  clears the death pin when you pick your grave back up and mass-pins on a key; here the pins are
  shared through map tables instead.
- Shared map tables go where nbusseneau's
  [Better Cartography Table](https://thunderstore.io/c/valheim/p/nbusseneau/BetterCartographyTable/)
  goes by another road: that one adds public and private pins and syncs on use; this one keeps the
  game's own table and syncs by walking up to it.

## Recommendations

Mods I run beside this one. None of them overlap it — they fill the gaps this one leaves.

- [Unshamed](https://valheim.hexium.gg/mods/Azumatt/Unshamed) — gives you back the achievements
  Valheim switches off the moment it sees a mod. It unlocks nothing you have not earned, and it
  can backfill the progress your modded hours already made.
- [HUD Compass](https://thunderstore.io/c/valheim/p/Neobotics/HUDCompass/) — a compass across the
  top of the screen, carrying live markers for your ships, carts and portals so you can find where
  you left them.
- [Target Portal](https://valheim.hexium.gg/mods/Smoothbrain/TargetPortal) — pick the portal you
  are travelling to off a map instead of juggling tag pairs, with per-portal access rules and
  favourites. This one wants to be on the server too.
- [Plant Everything](https://thunderstore.io/c/valheim/p/Advize/PlantEverything/) — berry bushes,
  mushrooms, flowers and every kind of tree on the cultivator, with growth timers on the plants and
  a setting for each one.
