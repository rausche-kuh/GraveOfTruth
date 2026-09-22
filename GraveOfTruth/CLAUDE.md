# GraveOfTruth

Makes dying embarrassing. See the root `CLAUDE.md` for the shared build, the scripts and the
environment.

**Shipped (0.1.0):** on death the whole show is broadcast to every modded client — the bolt on the
grave, a ~18s `ThunderStorm`, and the loser jingle as a 3D sound from the grave a beat after the
bolt strikes, with two fading echoes behind it. Opening your own grave to loot it wails once more,
with a wind gust. Every client runs its own copy of the effects off the one RPC, so nothing is
spawned or played twice.

| Path | What |
| --- | --- |
| `src/GraveOfTruth.cs` | The whole plugin: BepInEx entry point + Harmony patches. |
| `assets/sound.ogg` | Loser jingle, loaded at runtime from next to the DLL. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page). |

## Conventions

- The jingle plays from a throwaway 3D `AudioSource` spawned at the grave, routed through the
  master mixer's SFX group so the volume sliders apply. Never attach it to the player — that
  object is destroyed on respawn and cuts the death sound off.
- The jingle waits `StrikeLeadIn` (~1.7s) before it starts on a death, or the bolt's clap buries
  it, and is followed by two quieter, slightly detuned repeats (`EchoDelay` / `TailDelay`) that
  fake an echo. Each repeat is its own throwaway source, so they overlap the way a real one does.
- Everything is networked with one routed RPC (`GraveOfTruth_Wail`, a `Vector3` and a `bool` for
  whether it was a death or a looting) sent to `ZRoutedRpc.Everybody`, which includes the sender —
  `InvokeRoutedRPC` handles it locally and the server does not echo it back, so every client runs
  the handler exactly once. `ZNet` builds a fresh `ZRoutedRpc` per
  session, so registration hangs off a `Game.Start` postfix and is guarded against re-registering
  on the same instance (`m_functions.Add` throws on a duplicate). Only modded clients hear it.
- The patches on `Player.OnDeath` / `TombStone.Interact` do nothing but send the RPC; every effect
  lives in the handler, so there is one description of the show and every client runs the same one.
  The handler bails out on a dedicated server (`ZNet.IsDedicated()`) — nobody is watching there.
- The bolt on the grave is the obliterator's own lightning, `Incinerator.m_lightingAOEs`, found via
  `Resources.FindObjectsOfTypeAll<Incinerator>()`. It is a networked prefab, so every client spawns
  its own copy under `ZNetView.m_forceDisableInit` (the game's own idiom: the `ZNetView` destroys
  itself in `Awake`, no ZDO, nothing replicated) — instantiating it normally would show the other
  clients a second bolt on top of the one they spawned. `Thunder.m_flashEffect` +
  `m_thunderEffect` over the grave is the fallback when no obliterator can be found. It carries
  real `Aoe` damage, so spawn it **inactive**, zero every `Aoe` on it, then `SetActive(true)` — an
  `Aoe` with `m_hitOnEnable` lands its hit inside `Instantiate` otherwise, and `Awake` (where the
  `ZNetView` reads the flag) runs on that `SetActive`. `Aoe` cleans itself up via `m_ttl`, so keep
  the component and defang it rather than destroying it.
- Never hook anything that runs *inside* `Player.OnDeath` before `Game.RequestRespawn` (e.g.
  `TombStone.Setup`, which `CreateTombStone` calls): an exception there skips the respawn request
  and the corpse stays standing forever. Patch `OnDeath` itself and wrap the body in try/catch.
- Weather is faked through `EnvMan` (`m_debugEnv` for the environment, `SetDebugWind` /
  `ResetDebugWind` for the gust, which also drives `AudioMan`'s wind loop) and lightning reuses the
  vanilla `Thunder` component's `m_flashEffect` / `m_thunderEffect`. None of it is networked by the
  game — it only reaches the other players because each of them runs the RPC — so restore
  `m_debugEnv` to whatever it was rather than assuming `""`, and keep `Blow`'s single coroutine so
  a second wail never stacks a second storm.
