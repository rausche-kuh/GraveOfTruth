# ImmersiveEntrance

The black surface in a dungeon entrance becomes a window into the dungeon. See the root
`CLAUDE.md` for the shared build, the scripts and the environment.

**State:** prototype, reworked on 2026-09-23 and untested in game since (see "Untested since the
rework" below). Nothing is released; `VERSION` stays 0.1.0 until the first Thunderstore upload.
`package/icon.png` is a generated placeholder.

| Path | What |
| --- | --- |
| `src/ImmersiveEntrance.cs` | Entry point, config, the teleport registry (`Location.Awake` / `OnDestroy` patches), the `DungeonGenerator.Spawn` patch, the once-a-second pick of entrances near the camera, the `Camera.onPreCull` hook. |
| `src/Portal.cs` | One doorway: portal camera, off-axis frustum, render texture, the quad, the outdoor fog layer, the reflection cubemap, the dust, the AO copy. All calibration fields. |
| `src/DoorSurfaces.cs` | The trigger frames the view is mapped between; the game's black and white boxes; boxes measured in a frame. |
| `src/InteriorLook.cs` | The interior environment blended for the time of day as `EnvMan.SetEnv` does, swapped in for the portal render; the torches near the exit. |
| `src/ExitGlow.cs` | The exit's white box and the lights and particles in it, hidden for the portal render only. |
| `src/InteriorGroup.cs` | Renderers the location marks interior-only, switched on for the portal render only. |
| `src/Dev/EntranceCommands.cs` | `ientrance dump / shaders / status / light / part / floor / refl / toggle / flip / offset / clip / size / at / auto` (Debug only). |

## How it works

- The portal camera stands in the exit frame where the main camera stands in the entrance frame,
  turned 180 degrees about Y, and looks straight along the exit door's normal. Its projection is
  an off-axis frustum (`Matrix4x4.Frustum`) through the doorway rectangle. The floor shift and
  the tuning offsets translate the whole mapping - the camera moves and the rectangle moves with
  it, so the frustum keeps the unshifted eye's shape. Shifting the eye against a fixed rectangle
  tilts every ray instead and a raised eye lifts the dungeon in the door (that was the
  2026-09-23 "shift reappeared" bug). The texture is exactly the picture that belongs on the door: the doorway is a
  plain quad with unit UVs (a 4 x 4 grid, only so the outdoor fog can vary per vertex), every
  texel is spent on the door (`TextureSize` is the longer side), and no oblique projection is
  needed. The near plane is the door plane or `ClipOffset` past the exit trigger, whichever is
  further, so the void and the white door are never drawn.
- The doorway rectangle is the front face of the game's black box (`FindBlack`; `Gateway/Cube`,
  material `tointerior_portal` at a burial chamber) measured in the entrance frame, 1 cm in front
  of it and drawn in a render queue after it, so the black stays underneath. Without a box a
  `ManualSize` rectangle on the entrance frame is used. The box's other faces sit inside the
  walls and are left black.
- No shader ships with the mod; the doorway uses `Sprites/Default` (`Unlit/Texture` is not in
  the build), which multiplies colour by alpha, so the view texture is `RGB111110Float` - no
  alpha channel - where supported.
- The doorway is transparent and the deferred fog pass runs before the transparent queue, so
  the outdoor fog is a second submesh over the view in `RenderSettings.fogColor` with alpha
  1 - Unity's fog factor at each vertex's view depth.
- Rooms arrive after the location (`DungeonGenerator.LoadRoomPrefabsAsync` then `Spawn`), so the
  doorway is not drawn while `m_loadedRooms` still holds entries, and the `Spawn` postfix
  re-gathers torches and interior-only renderers and retakes the reflection.

## Lighting: what the render swaps in

`EnvMan.SetEnv` writes the environment into global state for where the player is, blended by
four time-of-day weights it computes from `m_smoothDayFraction`; `m_alwaysDark` only answers the
daylight query. `InteriorLook.Compute` repeats the weights and the blend for the interior
environment (`Location.m_interiorEnvironment`), and `Apply` puts in place, for the portal render only:

- `RenderSettings` fog colour and density, flat ambient and the ambient probe set outright.
- The sun's colour, intensity and rotation (the interior's `m_sunAngle`), `ForceVertex` and no
  shadows, as `EnvMan` does whenever the player is in an interior. Left per-pixel, the outdoor sun
  lit the whole dungeon evenly.
- The shader globals `_AmbientColor`, `_SunColor`, `_SunFogColor`, `_SunDir`, `_SkyboxSunDir`
  (the game's fog post shader tints toward the sun), and `_Wet` = 0 so rain outside does not
  wet the dungeon.
- The default reflection: `ReflectionUpdate` keeps the game's two probes on the player, 2000 x
  1000 x 2000, so the dungeon 5000 up gets the default sky reflection, bright at any hour. The
  portal renders its own cubemap of the dungeon once (a second plain camera, `RenderToCubemap`,
  3 m in from the exit, under the interior look with a black sky) and uses it as the custom
  reflection; `ientrance part refl` falls back to black.
- Ambient occlusion: the game's AO is `AmplifyOcclusionEffect` on the main camera, tinted and
  scaled per environment through `CameraEffects.SetEnvironmentAOParams`. The portal camera gets
  a copy of the component (public fields copied by reflection) with the interior's AO colour and
  intensity per render.
- Torches: `LightLod` switches off every light farther than 40 m from the player and its shadow
  beyond 20 m. The lights within 40 m of the exit are switched on for the render, nearest first
  within the player's point light and shadow limits, and restored straight after; the fade
  coroutine never notices.
- The exit's white box (a mesh named or textured "portal", else "white", "light", "bright" or
  "fade") and any non-fireplace light or particle system within 1 m of it or 2.5 m of the exit
  trigger are hidden, and interior-only renderers (`RenderGroupSubscriber` in
  `RenderGroup.Interior`, which `RenderGroupSystem` disables while the player is outside) are
  enabled. The first ice cave looked lit from its door, hence the radius; `status` prints what
  the glow search found and `ientrance part exit` shows it again.
- Fog is drawn by the post-processing stack v1 (`FogComponent`, `Hidden/Post FX/Fog`, a command
  buffer after opaque, reading `RenderSettings` and depth); the portal camera carries a
  `PostProcessingBehaviour` with a profile that has only fog on. It requires deferred rendering
  and `RenderSettings.fog` true.
- The environment's particle systems (`EnvSetup.m_psystems`; the Crypt's is
  `_GameMain/_Environment/FollowPlayer/InteriorDust`) follow the player, so each portal places a
  copy 4 m in from the exit door, simulated 5 s ahead so it never fades in.

## Floor shift

The two teleport triggers are not at the same height above their floors (about 30 cm apart in a
burial chamber), nor the same distance from the visible door faces. The shift is a translation
of the whole mapping in the exit frame (`Portal.Shift`, a vector), and `ientrance status` prints
every measurement behind it. `Portal.Floor` picks how the vertical part is measured;
`ientrance floor` cycles it:

- `Auto` (default): `Boxes` when both box bottoms sit within `BoxTolerance` (0.25 m,
  `ientrance tol`) of their probed floors, else `Probes`. A burial chamber's boxes stand on the
  floor and lined up nearly perfectly; a troll cave's did not and its probes did.
- `Boxes`: the bottom edge of the black box against the bottom edge of the white box.
- `Probes`: the highest floor hit at 0.1 - 0.6 m in front of each door, plus the per-location
  `Offsets` table measured by hand (Crypt2 - 4: y 0.16; the names are a guess to confirm with
  `status`). Probes alone gave 0.21 at a crypt where 0.36 looked right, hence the table.
- `Off`.

The depth part (`BoxDepth`) is the white box's face towards the dungeon against where the black
box's front face lands when turned about, in every mode but `Off`: the visible faces are the
ones the designers placed exactly, whether or not the bottoms are sunk. Before it existed the
crypt wanted `offset 0 0 0.1` and the troll cave `0 0 0.6` by hand; whether the boxes give those
numbers is unconfirmed.

## Game facts it rests on

- `Location.Awake` instantiates the interior prefab at the zone centre, entrance height + 5000,
  as a child of the location, so the interior is loaded whenever the entrance is, and a postfix
  on it sees both teleports of the dungeon. Rooms are children of the location's
  `DungeonGenerator`; generated doors are root objects.
- An entrance and its exit are a `Teleport` pair through `m_targetPoint`. Arrival is
  `target.position + forward - up`, facing `target.forward`, so the entrance's forward points
  out of the dungeon and the exit's forward points into it. Anything above y 3000 is an interior.
- Crypt2 dump: the black and white "surfaces" are 1 m thick `Custom/LitParticles` fade boxes
  (`Gateway/Cube`, mat `tointerior_portal`, 3.12 x 2.67; `ExteriorGateway/Cube`, mat
  `exirt_portal`, 4 x 5). The main camera renders `DeferredShading`, HDR. Real Crypt night values:
  ambient 0.31 grey, fog (0.18, 0.18, 0.22) density 0.1 Exponential, sun intensity 0, `m_alwaysDark`.
- Terrain (`Heightmap`) is in `RenderGroup.Overworld`, switched off while the player is in an
  interior. The game has no texture mipmap streaming, so a manually rendered camera sees full textures.
- The game uses the built-in render pipeline (no SRP assemblies in `Managed/`), so
  `Camera.onPreCull` and a manual `Camera.Render()` work.
- Installing a Release build (`deploy` without `-c Debug`) removes the `ientrance` command; check
  the DLL size when the commands go missing. Another session's `deploy.sh` with no mod names
  redeploys every mod as Release.

## Untested since the rework

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
