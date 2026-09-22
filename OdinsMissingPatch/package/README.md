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

## Configuration

`BepInEx/config/rauschekuh.odinsmissingpatch.cfg`, written the first time the game runs with the
mod. Every tweak has an `Enabled` switch and its own settings — for the range tweaks a multiplier,
`2` by default and `1` for vanilla; for combat stamina the threat radius in metres, an
`EnragedEnemies` switch and one switch per cost. Changes are picked up while the game is running,
including from an in-game config manager: stations already standing around you are re-measured on
the spot.

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
you as its target.

## Install

Install through a mod manager (Gale or r2modman), or drop the contents of the zip into
`BepInEx/plugins/rauschekuh-OdinsMissingPatch/`.

Requires the BepInEx pack for Valheim.

## Credits

Endless fuel follows the approach of Digitalroot's
[Eternal Fire](https://thunderstore.io/c/valheim/p/Digitalroot/Eternal_Fire/), which also covers
ovens and smelters and can be configured per fire type if you want finer control. Mist clear range
does what Crystal Ferrai's [Clear The Air](https://thunderstore.io/c/valheim/p/Crystal/ClearTheAir/)
does, which defaults to vanilla and can be enforced by a server. Combat stamina is modelled on
Cartur's [Safe Stamina](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Safe_Stamina/), which
waives the same costs inside a radius; this one also counts an enemy that has noticed you and is
coming for you, wherever it is. Do not run both.
