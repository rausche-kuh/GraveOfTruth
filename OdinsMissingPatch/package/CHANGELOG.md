# Changelog

## Unreleased

- A chest can now be made the home of a kind of item: Alt-click an item inside an open chest, the
  same click that makes a favourite in your own inventory, and the chest is marked for that kind.
  Quick stack and Fill the chest's stacks then put it there even when the chest holds none of it,
  and quick stacking fills the chests marked for an item before the ones that only happen to hold
  one — so a chest emptied of its wood still draws the next load back.
- Closing the inventory with a chest open no longer flashes the game's old Take all and Stack all
  buttons over the chest panel, nor slides Stack nearby into the buttons beside your inventory,
  while the screen fades out; every button now stays put until the panel is gone.
- While a chest has any marks, a Clear favourites button appears in its panel next to the Nearby
  use switch: it counts them, lists them in its tooltip and clears them all in one click.
- Every word the mod shows now follows the language Valheim is set to, and can be translated by
  adding a column to `translations.csv` beside the DLL. English is what ships today.

## 0.2.2

- Auto shield: drawing a one handed weapon now raises a shield with it, if your off hand is empty
  and you carry one. A shield or torch already in your hand stays, and the shield is chosen from
  your favourites first, then your hotbar, then the rest of your backpack.
- Fill your stacks now only tops the stacks you already carry up to their caps; what the chest
  holds beyond that stays in the chest instead of landing in your free slots as a new stack.
- Sorting your inventory now leaves what you have equipped where it is, the way it already left
  your favourites, and lays everything else out around both.
- A favourite item is now marked with a yellow border around its slot instead of a filled
  background, so you can still tell at a glance whether a favourite is equipped.

## 0.2.1

- Equip while running: pressing a hotbar key while sprinting now equips or unequips the weapon,
  shield or armour without you having to slow down. It still takes the usual moment, an
  attack, jump or dodge still interrupts it, and a crossbow still only reloads once you stop.

## 0.2.0

- Endless fuel: campfires, hearths, torches, braziers and the hot tub never go out.
- Mist clear range: wisplights, wisp torches and everything else that clears the mist reach twice
  as far.
- Combat stamina: sprinting, jumping, swimming, sneaking, building, chopping, mining and weapon
  swings cost nothing while nothing hostile is near you or hunting you, and the bar refills while
  you swim or swing. One switch per cost, the radius configurable.
- Instant comfort: sitting down by a fire grants Rested at once, for the comfort of the spot you
  sit in, instead of after ten seconds of Resting.
- Fireside healing: resting by a fire heals you for the comfort of the spot, every ten seconds,
  on top of what your food heals — two health per comfort level by default, so a campfire out in
  the open is a slow mend and a furnished hall patches you up in a minute. The rate is
  configurable, and a switch decides whether you have to be sitting.
- Fast portals: a portal sends you through as soon as the screen is black and the other side has
  loaded, instead of after a fixed eight seconds, and the fade to black is twice as quick.
- Keep gear on death: weapons, armour, ammunition, tools, the belt and your food stay with you
  when you die, and stay equipped, so you respawn ready to fight your way back. Only materials,
  trophies and the rest of the run's loot go to the grave. Which item types stay is configurable.
  Only applies on a world whose death penalty is set to Casual, the lowest setting — on a world
  set any harsher the grave takes everything the game says it should.
- Nearby crafting: crafting, upgrading and building take their materials from chests around you
  without opening them, your backpack first. The counts in the crafting panel and the build menu
  include those chests: an amount your backpack covers stays white, one that needs the chests
  turns yellow, and hovering an ingredient shows what you carry and what the chests hold. Only
  chests placed by a player count; every chest gets a "Nearby use" button in its panel to keep it
  out of this. The range is configurable.
- Quick stack: a hotkey (. by default) stacks your inventory into the chests around you that
  already hold each item. Every chest that took something glows and shows how many it took.
  Alt-click an item in your inventory to mark it as a favourite, which keeps it out of quick
  stacking; equipped items and the hotbar stay too. A favourite only holds while the stack is
  yours — put it in a chest, leave it in your grave or drop it and the mark is gone. Hotkey,
  modifier, range and the hotbar are configurable.
- Nearby fuel: adding fuel to a fire, smelter, oven or shield generator by hand takes it from a
  chest around you when your backpack has none. Nothing refuels itself.
- Add all: Shift + Use on a fire, smelter, oven, cooking station, shield generator or ballista
  puts in everything that fits, instead of one. Fuel, ore, food and bolts all come out of your
  backpack first and then out of the chests around you, without your opening them. The hover
  text says what would go in, and the range is configurable.
- Auto repair: opening a crafting station repairs everything you carry that it can repair, so
  the forge mends what belongs to the forge the moment you walk up to it.
- Area repair: one swing of the hammer repairs every damaged piece around the one you aim at,
  closest first, for the usual cost per piece. The radius is configurable, and holding Left Alt
  repairs the single piece you aim at as before.
- Chest buttons: the chest panel's Take all and Stack all become five icon buttons beside the
  panels: fill your stacks from the chest beside your inventory, between armour and weight;
  take all, place all, fill the chest's stacks from your backpack and sort the chest in a
  column down the side of the chest. Putting things in leaves worn gear, favourites and the
  hotbar alone.
- Inventory buttons: a stack nearby button (quick stack by click, while no chest is open) and a
  sort button in a column beside your inventory, between the armour and the weight. Sort merges
  your stacks and orders them by kind and name, leaving favourites and the hotbar where they
  are. Materials are not just alphabetical: an ore sits with the bar it smelts into and with
  what that bar makes, every log sits with the coal it burns down to, families come in the order
  you meet them, and everything a portal refuses to carry ends up in one block at the end.
- Power picker: a Forsaken powers category in the radial menu, holding the power of every boss you
  have beaten with its own icon. Pick one and it is the power your power key casts, without the
  trip back to the sacrificial stones. The category shows the power you are carrying, and the
  cooldown you are on carries over, so switching mid cooldown buys you nothing.

## 0.1.0

- First release: station range and comfort range, both configurable.
