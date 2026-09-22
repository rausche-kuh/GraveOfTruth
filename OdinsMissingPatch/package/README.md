# Odin's Missing Patch

The patch Odin forgot: a collection of small quality of life changes for Valheim. Every tweak has
its own section in the config file and can be turned off on its own. Nothing needs a server install.

## What it does

- **Station range** — a crafting station covers twice the area, so building, crafting and
  repairing work across the whole workshop instead of an arm's length from the bench. The circle
  the station draws grows with it, so you can still see exactly how far it reaches. Station
  extensions (the forge cooler, the tanning rack, ...) may stand twice as far from their station
  and still raise its level.
- **Comfort range** — the furniture that counts towards Rested is picked up from twice as far, so
  a hall can be furnished like a hall rather than crowded around one chair.
- **Endless fuel** — campfires, hearths, torches, braziers and the hot tub never run out of fuel.
  Whatever you light stays lit, and one that had already burnt out lights itself again. Switch it
  off and everything simply burns down from full again.
- **Mist clear range** — a wisplight, a wisp torch and everything else that clears the Mistlands
  mist pushes it back twice as far, so you can see the cliff edge before you walk off it.
- **Combat stamina** — stamina only drains in combat. Sprinting, jumping, swimming, sneaking,
  building, chopping, mining and weapon swings cost nothing while nothing hostile is within 25m of
  you and nothing that has noticed you is coming for you, and the bar keeps refilling while you
  swim or swing. Something hostile walks into range, or the troll you woke gives chase from 40m
  out, and every cost is back at full price, mid swing if need be. Hostility is the game's own
  verdict, so tamed animals do not count, a boar that has not noticed you does, and so does an
  enraged enemy however far away it is. Each cost has its own switch, and the radius is yours to
  set. Free swimming also means you cannot drown out of combat, since drowning only starts once
  stamina is empty - switch `FreeSwim` off if the water should stay dangerous. Skills level as
  before.
- **Instant comfort** — sit down by a fire and you are Rested at once, for the comfort of the
  spot you sit in, instead of after ten seconds of Resting. Everything else about resting is the
  game's own: you still need the fire, the wet and the cold still get in the way, and standing by
  the fire still takes the usual wait. A chair, a bench or the sit emote all count.
- **Fast portals** — a portal takes as long as it needs and not a second more. The game waits a
  flat eight seconds on every trip; with this you are through as soon as the screen is black and
  the other side has loaded, which on a decent machine is about a second. The fade to black is
  half a second instead of one, and configurable.
- **Keep gear on death** — dying keeps your gear. Weapons, armour, ammunition, tools, the belt and
  the food you carry stay in your inventory and stay equipped, so you respawn ready to fight your
  way back to the grave; only the run's loot goes in it — materials, trophies, fish, coins. The
  game's own "keep equipment" death penalty only keeps what you had on, which leaves the spare
  arrows, the food and the backup weapon in the grave; this decides by item type instead, from a
  list you can edit, and does so whatever the world's death penalty is set to. On a world that
  deletes the grave's contents the kept items survive that too; a world that keeps everything is
  left alone. Skill loss is untouched.
- **Area repair** — one swing of the hammer mends the whole neighbourhood: every damaged piece
  within 10m of the one you aim at, closest first, instead of one piece per click along every wall
  a raid went through. Each piece costs what it always did — the same stamina, the same hammer
  durability, the same crafting station in range — so a swing stops when the stamina runs out or
  the hammer is one repair from breaking, and picks up where it left off on the next one. Wards are
  respected, so a neighbour's house is not on your hammer. Hold `Left Alt` while you swing for the
  single piece you aim at, the way the game does it. The radius and that key are both yours to set.
- **Auto repair** — walk up to a workbench, press Use, and everything you carry that the
  bench can repair is repaired: the forge mends what belongs to the forge, the workbench what
  belongs to the workbench, instead of one item per click of the repair button. Which items a
  station takes is the game's own answer, the one the repair button uses, so nothing is
  repaired that you could not have repaired there yourself — and repairing is free in
  Valheim, so it costs you nothing but the walk. Crafting skill still goes up for the wear
  you mended.
- **Nearby crafting** — crafting, upgrading and building take their materials from the chests
  around you, without a chest being opened. What you carry is spent first, and only what is
  missing comes out of the chests, nearest first. The crafting panel and the build menu count
  the chests too, so a recipe reads as craftable exactly when it is: an amount your backpack
  covers stays white, one the chests have to pay for turns yellow, and hovering an ingredient
  tells you what you carry and what the chests hold. Only chests placed by a
  player count, never a dungeon's or a ruin's, and every chest gets a **Nearby use** button in
  its panel that keeps it out of all of this — for the chest you keep your emergency wood in.
  The range is yours to set, 20m by default.
- **Quick stack** — one key (`.` by default) stacks your inventory away into the chests around
  you: every stack you carry goes to the nearest chest that already holds that item, topping up
  its stacks before taking a free slot. Each chest that took something glows and shows how many
  it took, and a message sums it up. Equipped items stay, the hotbar stays (a switch), and so
  does anything you mark as a favourite: hold `Alt` and click an item in the inventory to give
  it a golden frame, and quick stacking leaves it alone. A favourite lives in your inventory
  and nowhere else: the mark can only be set there, and a stack that leaves you — into a chest,
  into your grave, onto the ground — comes out of it plain. The key, the modifier and the range
  are configurable.
- **Nearby fuel** — adding fuel by hand reaches into the chests around you. Press Use on a fire,
  a smelter, an oven or a shield generator as you always have: one unit goes in, out of your
  backpack if you carry the fuel and out of the nearest chest that holds it if you do not.
  Nothing refuels itself; a station only ever takes fuel when you give it some. Same chests as
  nearby crafting, same button to keep one out of it, its own range.
- **Add all** — hold `Shift` and press Use on a fire, a smelter, a blast furnace, an oven, a
  cooking station, a shield generator or a ballista, and everything that fits goes in at once:
  wood up to the fire's cap, ore up to the smelter's queue, food into every free slot, bolts up
  to the magazine. Use alone still adds one, as ever. The hover text tells you what a
  `Shift` + Use would put in, count and all, so you know before you press. Fuel, ore, food and
  bolts all come out of your backpack first and then out of the chests around you, so a smelter
  fills from the chest you keep the ore in without your opening it. Same chests as nearby
  crafting, same button to keep one out of it, its own range.
- **Chest buttons** — the chest panel's Take all and Stack all become five icon buttons beside
  the panels, each with a tooltip. Beside your inventory, between armour and weight: **Fill your
  stacks**, which brings you what the chest holds of the items you carry. In a column down the
  side of the chest, from its top: **Take all**; **Place all**, which puts everything you carry
  into the chest; **Fill the chest's stacks**, which puts in what you carry of the items the
  chest holds; and **Sort the chest**, which merges its stacks and orders them by kind and
  name. The two that put things in leave worn gear, favourites and the hotbar alone (the hotbar
  is a switch). Switch the tweak off and the game's two buttons are back.
- **Inventory buttons** — two icon buttons in a column beside your inventory, between the
  armour and the weight: **Stack nearby**, which is quick stack by click and is there while quick stack is on
  and no chest is open (with a chest open, Fill your stacks stands in its place), and **Sort**,
  which merges your stacks and orders the rows below the hotbar by kind (weapons, shields,
  tools, armour, ammunition, food, materials, trophies, the rest), then name, then quality.
  Favourites keep their slot and so does the hotbar (a switch).
- **Power picker** — a **Forsaken powers** category in the radial menu, so the boss power you
  carry is a choice you make where you stand instead of a walk back to the sacrificial stones.
  Opening it shows one ring element per power you have unlocked, each with the boss's own icon
  and its description; pick one and it is what your power key casts from then on. The one you
  are already carrying is gold and says so, and it is the icon the category itself wears, so the
  ring tells you which power you are on before you open it. Nothing else about the powers
  changes: the cooldown is a timer on you rather than on the power, so it carries straight over
  and switching mid cooldown buys you nothing, and a power you have never taken at its stone is
  not in the list. A character who has never taken one has no category at all.

## Configuration

`BepInEx/config/rauschekuh.odinsmissingpatch.cfg`, written the first time the game runs with the
mod. Every tweak has an `Enabled` switch and its own settings — for the range tweaks a multiplier,
`2` by default and `1` for vanilla; for combat stamina the threat radius in metres, an
`EnragedEnemies` switch and one switch per cost; for keep gear on death the comma separated list
of item types that stay with you, with every type the game knows listed in the setting's
description; for area repair the radius in metres and the key that holds it back to one piece; for
the nearby chest tweaks the range in metres, and for quick stack the hotkey, the favourite modifier
and whether the hotbar is stacked away too; for chest buttons whether the hotbar goes into the
chest too, and for inventory buttons whether Sort touches the hotbar. Instant comfort, auto
repair and the power picker have nothing but their switch. Changes are picked up while the game
is running, including from an in-game config manager: stations already standing around you are
re-measured on the spot.

## Multiplayer

Client side, and no server-side install. Build range and comfort are both decided on your own
machine, so those two change what you can do and nothing about the world or anyone else's game.
That also means they reach no further than your own machine: a player without the mod still has
vanilla range at the same workbench, and sees the smaller circle. The mist is drawn on your own
machine too, so the wider clearing is yours alone: a player without the mod standing next to you
sees the mist close in at the vanilla distance.

Endless fuel does change the world — the fuel in the fire — and Valheim leaves a fire to whoever
is closest to it. Fires near you stay lit for everyone; a fire that only a player without the mod
is standing next to burns down as usual. Put the mod on every client if every fire should stay lit.

Combat stamina is decided on your own machine and only waives your own stamina: nothing is waived
for anyone else, player or monster. It works the same whether the monster hunting you is run by
your machine or by another player's, because the game already tells your client when a monster has
you as its target. Instant comfort only touches your own Rested, and status effects are your own
machine's business, so it needs nothing from anyone else. Fast portals only shortens your own
trip; the portal itself, and everyone else's trips, are untouched. Auto repair only touches
the durability of the items in your own inventory, which is your own machine's business too. Keep gear on death is decided
by the dying player's own machine, so it applies to you and to nobody else: a player without the
mod dies by the world's rules. On someone else's server it overrides the death penalty the host
chose, for you - ask before you bring it.

Area repair does change the world - a repaired wall is repaired for everyone - but only through the
game's own repair, one piece at a time, so it needs nothing from the server and nothing from anyone
else. It pays the full cost for every piece and asks the same station and ward questions the game
asks, so all it saves you is the clicking.

Nearby crafting, quick stack, nearby fuel and add all write to chests, which is world state, and
they do it the way you would by hand: the chest is taken over by your machine for the write, exactly
as when you open it. A chest someone else has open is left alone, and so is one you could not
open yourself — a private chest of theirs, or one inside a ward you have no access to. The
"Nearby use" switch is stored with the chest, so it holds for everyone with the mod. None of it
needs anyone else to have the mod, and none of it runs on a dedicated server. The chest buttons
write to the chest you have open, which is yours while it is open, exactly as dragging items by
hand is; the inventory buttons only touch your own backpack, apart from stack nearby, which is
quick stack. Add all sends the station the same request the game sends for a single Use, once
per item, so whoever runs the station applies it exactly as if you had pressed Use that many
times; what it takes from chests it takes the way nearby crafting does.

The power picker only writes to your own character. Which power you carry is saved with the
character file and nothing else reads it, exactly as when you take one at the stones, so it needs
nothing from the server and nothing from anyone else — and a player without the mod still walks.

## Install

Install through a mod manager (Gale or r2modman), or drop the contents of the zip into
`BepInEx/plugins/rauschekuh-OdinsMissingPatch/`.

Requires the BepInEx pack for Valheim.

## Credits

Endless fuel follows the approach of Digitalroot's
[Eternal Fire](https://thunderstore.io/c/valheim/p/Digitalroot/Eternal_Fire/), which also covers
ovens and smelters and can be configured per fire type if you want finer control. Mist clear range
does what Crystal Ferrai's [Clear The Air](https://thunderstore.io/c/valheim/p/Crystal/ClearTheAir/)
does, which defaults to vanilla and can be enforced by a server. Fast portals does the timer half of Crystal Ferrai's
[Proper Portals](https://thunderstore.io/c/valheim/p/Crystal/ProperPortals/), which also lets
metals through and can be enforced by a server. Combat stamina is modelled on
Cartur's [Safe Stamina](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Safe_Stamina/), which
waives the same costs inside a radius; this one also counts an enemy that has noticed you and is
coming for you, wherever it is. Do not run both. Area repair does what
[Venture Area Repair](https://thunderstore.io/c/valheim/p/VentureValheim/Venture_Area_Repair/)
does, which reaches a fixed 20m; this one lets you set the radius and the single repair key. Do not
run both. Nearby crafting, quick stack and auto repair cover the same ground as Zellds'
[SmartCraft-Storage](https://thunderstore.io/c/valheim/p/Zellds/SmartCraftStorage/), which goes
much further — stations that feed and empty themselves, restocking, animal feeding — if you want a
base that runs itself; this one keeps every move yours. Do not run both.
