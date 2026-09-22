# GraveOfTruth

Makes dying embarrassing. See the root `CLAUDE.md` for the shared build, the scripts and the
environment.

**Shipped (0.1.0):** on death the loser jingle is broadcast to every modded client, plays as a 3D
sound from the grave a beat after the bolt strikes it, with two fading echoes behind it; opening
your own grave to loot it wails once more. The dying/looting player additionally gets local weather — a ~18s `ThunderStorm`
on death, a wind gust while looting.

| Path | What |
| --- | --- |
| `src/GraveOfTruth.cs` | The whole plugin: BepInEx entry point + Harmony patches. |
| `assets/sound.ogg` | Loser jingle, loaded at runtime from next to the DLL. |
| `package/` | Thunderstore `manifest.json` + `icon.png`. |

## Conventions

- The jingle plays from a throwaway 3D `AudioSource` spawned at the grave, routed through the
  master mixer's SFX group so the volume sliders apply. Never attach it to the player — that
  object is destroyed on respawn and cuts the death sound off.
- The jingle waits `StrikeLeadIn` (~1.7s) before it starts on a death, or the bolt's clap buries
  it, and is followed by two quieter, slightly detuned repeats (`EchoDelay` / `TailDelay`) that
  fake an echo. Each repeat is its own throwaway source, so they overlap the way a real one does.
- The jingle is networked with a routed RPC (`GraveOfTruth_Wail`, a `Vector3` and a `bool` for
  whether a bolt comes with it) sent to
  `ZRoutedRpc.Everybody`, which includes the sender. `ZNet` builds a fresh `ZRoutedRpc` per
  session, so registration hangs off a `Game.Start` postfix and is guarded against re-registering
  on the same instance (`m_functions.Add` throws on a duplicate). Only modded clients hear it.
- Weather stays local to whoever died or looted — it is not part of the RPC.
- The bolt on the grave is the obliterator's own lightning, `Incinerator.m_lightingAOEs`, found via
  `Resources.FindObjectsOfTypeAll<Incinerator>()`. It is a networked prefab, so only the client the
  death happened to spawns it (`sender == ZDOMan.GetSessionID()` in the RPC handler); other clients
  get a `Thunder.m_flashEffect` + `m_thunderEffect` over the grave instead. It carries real `Aoe`
  damage, so spawn it **inactive**, zero every `Aoe` on it, then `SetActive(true)` — an `Aoe` with
  `m_hitOnEnable` lands its hit inside `Instantiate` otherwise. `Aoe` cleans itself up via `m_ttl`,
  so keep the component and defang it rather than destroying it.
- Never hook anything that runs *inside* `Player.OnDeath` before `Game.RequestRespawn` (e.g.
  `TombStone.Setup`, which `CreateTombStone` calls): an exception there skips the respawn request
  and the corpse stays standing forever. Patch `OnDeath` itself and wrap the body in try/catch.
- Weather is faked through `EnvMan` (`m_debugEnv` for the environment, `SetDebugWind` /
  `ResetDebugWind` for the gust, which also drives `AudioMan`'s wind loop) and lightning reuses the
  vanilla `Thunder` component's `m_flashEffect` / `m_thunderEffect`. All of it is local-client only,
  so restore `m_debugEnv` to whatever it was rather than assuming `""`.
