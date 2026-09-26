# ImmersiveEntrance roadmap

## To check in game

The 2026-09-23 rework replaced the screen-space grid and oblique projection with the off-axis
frustum and added the AO copy, the reflection cubemap, the day/night blend, the wet and sun
direction globals, the interior render group, the light limits, the dust prewarm, the teleport
registry and the room-ready gate. First in-game run: the lighting was judged good and the view
lines up sideways; with no floor shift the chamber sat about half a metre too high, and every
shift made it worse because the rectangle did not move with the eye (fixed since). Still to check:

- Second run, with the rectangle moving: a burial chamber lined up with `Boxes` but for 10 cm of
  depth, a troll cave only with `Probes` and 60 cm of depth, an ice cave lined up but was far too
  bright, as if lit from its white door. Now to check: whether `BoxDepth` reads about those two
  depths and `Auto` picks `Boxes` at the chamber and `Probes` at the cave (`status`), and
  whether the ice cave's brightness is the exit glow (`ientrance part exit` to compare; `status`
  lists what was hidden) or the `Caves` environment's own daytime ambient (`ientrance light`).
- Whether the AO copy renders without errors on the portal camera (BepInEx log) and the reflection
  cubemap shows the dungeon (`ientrance part refl` to compare with black).
- Whether the crypt's day values differ from its night values (`ientrance light` prints both).
- Whether any dungeon renderer is interior-only (`ientrance dump` lists them).

## Bugs

- Invested mines: camera renders infront of fog
- Winding Tunnels: placement is completly off, as the entrance is flat on the flow
- lighting quirk with the ice dungeon - exit glow hidden is correct for the other, seems to do nothing for the ice cave
- floor mode probe works best, with the ice cave still needing 0 0 0.5 offset.
