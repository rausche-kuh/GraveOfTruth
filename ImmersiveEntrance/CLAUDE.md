# ImmersiveEntrance

The black surface in a dungeon entrance becomes a window into the dungeon: a portal camera
renders the interior into a texture drawn over the black box. Purely client side. See the root
`CLAUDE.md` for the shared build, the scripts and the environment.

**State:** prototype, reworked on 2026-09-23 and only partly tried in game since
(`ROADMAP.md`). Nothing is released; `VERSION` stays 0.1.0 until the first Thunderstore upload.
`package/icon.png` is a generated placeholder.

## Docs

| File | Read it when |
| --- | --- |
| `ROADMAP.md` | Picking up work: what still has to be checked in game, and the known bugs. |
| `docs/rendering.md` | Touching the portal camera, the doorway quad, fog, or anything swapped in for the portal render (fog, ambient, sun, reflection, AO, torches, exit glow, dust). |
| `docs/floor-shift.md` | Lining a dungeon up: the `Portal.Floor` modes, `BoxDepth`, the per-location `Offsets`. |
| `docs/game-facts.md` | Relying on how locations, teleports, the black/white boxes or render groups work. |

## Rules that always apply

- Everything the portal render changes (render settings, shader globals, lights, the exit glow,
  interior-only renderers) is swapped in for that render only and restored straight after.
- The portal camera maps the exit frame to the entrance frame as a whole: a shift moves the
  camera **and** the doorway rectangle, never the eye alone (that tilts every ray).
- No shader ships with the mod; the doorway uses `Sprites/Default` (`Unlit/Texture` is not in
  the build).
- The doorway is not drawn while the dungeon's rooms are still loading
  (`DungeonGenerator.m_loadedRooms`); the `Spawn` postfix re-gathers what depends on them.
- The `ientrance` command is Debug only: a Release `deploy` (another session's `deploy.sh` with
  no mod names does this) removes it. Check the installed DLL's size when it goes missing.

## Source

| Path | What |
| --- | --- |
| `src/ImmersiveEntrance.cs` | Entry point, config, the teleport registry (`Location.Awake` / `OnDestroy`), the `DungeonGenerator.Spawn` patch, the once-a-second pick of entrances near the camera, the `Camera.onPreCull` hook. |
| `src/Portal.cs` | One doorway: portal camera, off-axis frustum, render texture, quad, outdoor fog layer, reflection cubemap, dust, AO copy, floor shift. All calibration fields. |
| `src/DoorSurfaces.cs` | The trigger frames the view is mapped between; the game's black and white boxes, measured in a frame. |
| `src/InteriorLook.cs` | The interior environment blended for the time of day as `EnvMan.SetEnv` does; the torches near the exit. |
| `src/ExitGlow.cs` | The exit's white box and the lights and particles in it, hidden for the portal render. |
| `src/InteriorGroup.cs` | Interior-only renderers, switched on for the portal render. |
| `src/Dev/EntranceCommands.cs` | `ientrance dump / shaders / status / light / floor / tol / refl / part / toggle / flip / offset / clip / size / at / auto` (Debug only). |
