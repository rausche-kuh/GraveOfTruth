# ImmersiveEntrance

The black surface in a dungeon entrance shows the live interior behind it, as a render-texture
portal. [`package/README.md`](package/README.md) is the Thunderstore page.

| Path | What |
| --- | --- |
| `src/ImmersiveEntrance.cs` | BepInEx entry point, config, the registry of loaded entrances, the room-spawn hook. |
| `src/Portal.cs` | One doorway: the portal camera and its off-axis frustum, the texture, the quad over the black surface, reflection, dust, AO. |
| `src/DoorSurfaces.cs` | The door frames the view is mapped between, and the game's black and white boxes. |
| `src/InteriorLook.cs` | The dungeon's fog, ambient, sun, globals and torches, blended for the time of day and swapped in around the portal render. |
| `src/ExitGlow.cs` | The exit's white glow, hidden around the portal render. |
| `src/InteriorGroup.cs` | Interior-only renderers, shown around the portal render. |
| `src/Dev/EntranceCommands.cs` | `ientrance` console command for inspecting and calibrating. Debug builds only. |
| `package/` | What Thunderstore gets. `icon.png` is a placeholder. |

## Build

```bash
./scripts/setup.sh                                # once, and after every Valheim update
./scripts/deploy.sh -c Debug ImmersiveEntrance    # with the ientrance command
./scripts/deploy.sh ImmersiveEntrance             # without
```

See [scripts/README.md](../scripts/README.md) for the full workflow on Windows and Linux.
