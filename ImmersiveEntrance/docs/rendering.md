# Rendering the doorway

How the portal view is made and what the game state is swapped for during its render. Read before touching `Portal.cs`, `InteriorLook.cs`, `ExitGlow.cs` or `InteriorGroup.cs`.

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
