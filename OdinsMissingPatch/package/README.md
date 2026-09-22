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

## Configuration

`BepInEx/config/rauschekuh.odinsmissingpatch.cfg`, written the first time the game runs with the
mod. Every tweak has an `Enabled` switch and its own multipliers — `2` is the default, `1` is
vanilla. Changes are picked up while the game is running, including from an in-game config manager:
stations already standing around you are re-measured on the spot.

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

## Install

Install through a mod manager (Gale or r2modman), or drop the contents of the zip into
`BepInEx/plugins/rauschekuh-OdinsMissingPatch/`.

Requires the BepInEx pack for Valheim.

## Credits

Endless fuel follows the approach of Digitalroot's
[Eternal Fire](https://thunderstore.io/c/valheim/p/Digitalroot/Eternal_Fire/), which also covers
ovens and smelters and can be configured per fire type if you want finer control. Mist clear range
does what Crystal Ferrai's [Clear The Air](https://thunderstore.io/c/valheim/p/Crystal/ClearTheAir/)
does, which defaults to vanilla and can be enforced by a server.
