# Immersive Entrance

Dungeon entrances are no longer a black wall.

Walk up to a burial chamber, a troll cave or any other dungeon, and the doorway shows what is
behind it: the first room, lit by its own torches and in the dungeon's own gloom, moving with
you as you move. Step through and you arrive where you were looking. The white door on the way
out is left as it is.

## Settings

- `MaxDistance`: how close you have to be before a doorway opens up.
- `MaxPortals`: how many doorways are drawn at once. Each one draws the dungeon behind it every
  frame, so keep it low on a slow machine.
- `TextureSize`: the sharpness of the view through the door.
- `InteriorLights`: whether the torches and braziers inside are lit in the view.

## Multiplayer

Purely client side. Install it on whichever clients want it; nothing goes on the server, and
nobody else is affected.
