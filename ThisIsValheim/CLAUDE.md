# ThisIsValheim

Doors are kicked open, not opened. See the root `CLAUDE.md` for the shared build, the scripts and
the environment.

**State (0.1.2, shipped):** the game's own secondary-attack kick — with any weapon held, while a
shut door is hovered — bursts the door open at `SwingSpeed`× speed with a battering ram, splinters
and sawdust going off at it; a double door's other half goes with it; a warded or key-locked door
rattles, says why and staggers the kicker; the hover text shows a "Kick" hint. The door itself is
opened through the game's `UseDoor` RPC, so the server and unmodded clients see an ordinary door.

## Docs

| File | Read it when |
| --- | --- |
| `docs/kick.md` | touching how a kick is noticed (`AddHitPoint`, any-weapon kick), `Check`/refusals, keys, wards, the cooldown, double doors or the hover hint; also what each release added. |
| `docs/effects-and-network.md` | touching the effect list and its resolution, the routed RPC, or the fast swing's timing. |
| `package/CHANGELOG.md` | adding a player-visible change (see the root `CLAUDE.md`). |

## Rules that always apply

- **The kick is the game's `unarmed_kick`** — no new input, no new attack. Reach, aim and
  stamina stay vanilla; the mod only reacts to hits (`Attack.AddHitPoint` postfix) and fakes the
  unarmed weapon for one `StartAttack` call.
- **`Door.Open`/`RPC_UseDoor` check nothing**, so every ward, key and range check is `Check`'s
  job — never open a door without going through it.
- **The ward is checked, not flashed** (`flash: false`); the hit already flashes it.
- **One routed RPC to `Everybody`** runs the handler once on every client, sender included;
  register it per session in `Game.Start`, guarded against duplicates, and bail out on a
  dedicated server.
- **Effects are spawned locally, never with a ZDO** (`ZNetView.m_forceDisableInit`), and anything
  without a `TimedDestruction` gets destroyed after `EffectLifetime`.
- **No `assets/`:** the bang is a config list of the game's own prefabs; some (e.g.
  `sfx_battering_ram_impact`) are only found by the `FindObjectsOfTypeAll` fallback.
- The fast swing is `Door.m_animator.speed`, restored by polling the animator, not by timing;
  `SwingWindow` is only the cap.

## Source

| Path | What |
| --- | --- |
| `src/ThisIsValheim.cs` | The whole plugin: config (`[Kick] SwingSpeed`, `LockedDoors`, `ShowHint`; `[Effects] Prefabs`), the patches (`NoticeKick`, `KickAtDoor`, `KickWeapon`, `ShowKickHint`, `ResolveOnLoad`, `RegisterRpc`), `Check`/`TryKick`/`Rebuff`, `KickNeighbours`, `RPC_Kick`, `Bang`, `ResolveEffects`, `Swing`. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md`. |
