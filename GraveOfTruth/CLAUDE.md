# GraveOfTruth

Makes dying embarrassing. See the root `CLAUDE.md` for the shared build, the scripts and the
environment.

**State (0.1.2, shipped):** a death is broadcast to every modded client. Those within
`StormRadius` (150 m) of the grave, the dead player included, get the obliterator's bolt on the
grave, a ~9 s dry `ThunderStorm` (looks only) and the loser jingle from the grave a beat after the
strike, with two fading echoes. The rest get the bolt, one distant flash and clap, and the jingle
at `DistantVolume` from `DistantOffset` towards the grave (the grave itself is past the jingle's
96 m rolloff). Looting your own grave wails once more. The tuning is `const`s at the top of
`src/GraveOfTruth.cs`, not config.

| File | Read it when |
| --- | --- |
| `ROADMAP.md` | Picking the next feature (death stats, the obituary, the talking grave) and the facts each still needs checked. |
| `package/CHANGELOG.md` | Adding a player-visible change (see the root `CLAUDE.md`). |

## Source

| Path | What |
| --- | --- |
| `src/GraveOfTruth.cs` | The whole plugin: `RegisterRpc`, `WailOnDeath`, `WailOnLoot`, `StormSky`, the `RPC_Wail` handler and its effects. |
| `src/Dev/GraveOfTruthTest.cs` | `gravetest [distance]`, a `partial` of the plugin class. Debug builds only. |
| `assets/sound.ogg` | The jingle, loaded at runtime from next to the DLL. |
| `package/` | `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md`. |

## Rules that always apply

- **One routed RPC** (`GraveOfTruth_Wail`: a `Vector3`, and a `bool` for death vs. looting) sent
  to `ZRoutedRpc.Everybody`, sender included, so every client runs the handler exactly once.
  `ZNet` builds a fresh `ZRoutedRpc` per session: register in a `Game.Start` postfix, guarded
  against re-registering (`m_functions.Add` throws). The handler bails out on a dedicated server.
- **The patches only send the RPC**; every effect lives in the handler, so there is one
  description of the show. Nearness is decided once, when the RPC arrives, so the dead player
  keeps their storm after respawning. `WailCooldown` guards against several deaths at once and
  clients sending the RPC at will.
- **Never hook anything inside `Player.OnDeath` before `Game.RequestRespawn`** (e.g.
  `TombStone.Setup`): an exception there skips the respawn and the corpse stands forever. Patch
  `OnDeath` itself and wrap the body in try/catch.
- **The jingle** plays from a throwaway 3D `AudioSource` at the grave, routed through the master
  mixer's SFX group so the sliders apply — never on the player, who is destroyed on respawn. It
  waits `StrikeLeadIn` (~1.7 s) so the clap does not bury it; the echoes (`EchoDelay`,
  `TailDelay`) are quieter, detuned repeats, each its own source so they overlap.
- **The bolt** is `Incinerator.m_lightingAOEs` (found via `FindObjectsOfTypeAll<Incinerator>()`;
  `Thunder.m_flashEffect` + `m_thunderEffect` is the fallback). It is networked and carries real
  `Aoe` damage: spawn it **inactive** under `ZNetView.m_forceDisableInit` (no ZDO, or other clients
  see two bolts), zero every `Aoe` (keep the component — `m_ttl` cleans it up), then
  `SetActive(true)`; an `Aoe` with `m_hitOnEnable` would hit inside `Instantiate` otherwise.
- **The storm stays cosmetic.** Never set `EnvMan.m_debugEnv` or `SetDebugWind` — they change
  the environment every system reads (Wet, Freezing, dungeon `EnvZone`, sailing wind). `StormSky`
  prefixes the private `EnvMan.SetEnv`, which only renders, with
  `InterpolateEnvironment(real, ThunderStorm, fade)`; `IsWet` / `IsCold` / wind read
  `GetCurrentEnvironment()` and never see it. The real env's `m_psystems`, `m_isWet` and
  `m_ambientLoop` are kept, so it never rains; only `m_envObject` swaps to the storm's past the
  halfway mark, which brings its horizon flashes. Skipped while `InInterior()`.
- **Testing without dying:** `scripts/deploy.sh -c Debug GraveOfTruth`, then `devcommands` and
  `gravetest [distance]` in the F5 console drops a real grave of yours (one stone inside, so
  looting it wails too) 30 m ahead and sends the same RPC a death would. Mind `WailCooldown`.
