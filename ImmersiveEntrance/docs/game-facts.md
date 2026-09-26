# Game facts

What the mod relies on in the game, measured from dumps and the decompiled source. Re-check after a Valheim update.

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
