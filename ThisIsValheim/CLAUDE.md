# ThisIsValheim

Doors are kicked open, not opened. See the root `CLAUDE.md` for the shared build, the scripts and
the environment.

**Shipped (0.1.0):** the bare handed secondary attack — the game's own kick, `unarmed_kick` — landing
on a closed door bursts it open at `SwingSpeed`× the normal animation speed while a battering ram,
splintering wood and a puff of sawdust go off at the door. The kick is broadcast to every modded
client; the door itself is opened through the game's own `UseDoor` RPC, so the server and unmodded
clients see an ordinary door swing.

**Shipped (0.1.1):** a warded or key-locked door that refuses the kick plays its own `m_lockedEffects`,
shows the game's `$msg_door_needkey` / `$piece_noaccess` and staggers the kicker; a bare handed
player looking at a door the kick would open gets a `$KEY_SecondaryAttack` "Kick" line in the
hover text (`ShowHint`).

**Shipped (0.1.2):** the secondary attack while hovering a shut door is the kick whatever is held
(`KickAtDoor`), and the hint shows regardless of the weapon; a kick that opens a door also kicks
any other door with a collider within `SeamReach` (0.6m) of the hit point (`KickNeighbours`), so
double doors no longer need the seam hit exactly. Only after the first door opened, so a locked
pair rebuffs once; each half gets its own bang. The fast swing now ends when the door has
opened or is shut again, so closing it right after the kick is at normal speed.

| Path | What |
| --- | --- |
| `src/ThisIsValheim.cs` | The whole plugin: BepInEx entry point + Harmony patches. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## How it works

- **The kick is the game's, not the mod's.** `PlayerUnarmed`'s `m_secondaryAttack` is
  `unarmed_kick` (range 1.6, force ×3, stagger ×6) — there is no extra key and no new input. The
  hook is a postfix on the private `Attack.AddHitPoint`, which every melee sweep calls for every
  object it touches *before* the game asks whether that object can be damaged. So the reach, the
  aim and the angle are the game's own, and doors that have no `WearNTear` at all (the dungeon and
  Mistlands gates) still register. The attack is identified by `m_attackAnimation` containing
  "kick", so a punch or a sword never counts and another mod's kick would.
- **`Door.Open` asks nothing.** `RPC_UseDoor` flips the state for whoever sends it — no key check,
  no ward check, no hover range — so every one of those is `Check`'s job. `Door.CanInteract()`
  is the game's own "is it standing still and openable", and `ZDOVars.s_state != 0` is "already
  open"; both are needed. `IncrementPlayerStat(PlayerStatType.DoorsOpened)` is here too, because
  `Door.Interact` — the only thing that normally counts a door — never runs.
- `Check` returns a `Refusal`: `Busy` (open, swinging, not loaded) is silent; `Ward` and `Key`
  go to `Rebuff`, which plays the door's `m_lockedEffects`, puts the game's own reason on screen
  and calls `Player.Stagger(away)`. `RPC_Stagger` turns the character to face `-forceDirection`,
  so passing the door→player direction keeps the kicker facing the door as they reel back. The
  cooldown is set before the rebuff too, so several rays of one kick stagger once.
- A key door only refuses a kicker without the key (`Door.HaveKey`, world level matched like
  vanilla). With the key, `TryKick` does what `Door.Interact` does: `$msg_door_usingkey`, and the
  key removed when `m_consumeKey`. With `LockedDoors` on, the lock is ignored and no key is spent.
- **Any weapon kicks a door.** `KickAtDoor` is a `Humanoid.StartAttack` prefix: for the local
  player's secondary attack with a door under `GetHoverObject()` and `Check` not `Busy`, it sets
  `forceUnarmed`, and a `GetCurrentWeapon` postfix answers with `m_unarmedWeapon` while that is
  set; a finalizer clears it. `Attack` keeps its own `m_weapon` from then on, so only that one
  call is fooled and the rest is the ordinary unarmed kick (range, stamina, `NoticeKick`). Bows
  never get here — their input goes through `UpdateAttackBowDraw`, not `StartAttack(secondary)` —
  and hovering reaches further than the kick (1.6), so from across the room it is a whiffed kick.
- The hover hint is a `Door.GetHoverText` postfix that appends
  `[$KEY_SecondaryAttack] Kick` only when `Check` is `None`, whatever is held. `Localization.Translate` turns `KEY_<name>` into the bound key, and into the
  `Joy<name>` binding when a gamepad is active. "Kick" is literal English, as the game's own
  "Change pose" on the armor stand is.
- The ward is checked but deliberately **not** flashed (`PrivateArea.CheckAccess(..., flash: false)`).
  The kick that got us here is a hit like any other, and `WearNTear.RPC_Damage` already calls
  `PrivateArea.OnObjectDamaged` for it; flashing again would flash twice for one kick.
- One swing sweeps several rays and can land on the same door more than once, and the door's state
  takes a round trip to its owner before it changes, so a second hit would read the door as still
  shut and slam it closed again. `KickCooldown` (0.6s, per door in `kicked`) is what stops that;
  the dictionary is cleared once it grows past eight doors, since by then all but the newest are
  long out of cooldown.
- The bang is a list of the game's own effect prefabs, named in the config and resolved in a
  `ZNetScene.Awake` postfix (behind the loading screen) and again on `SettingChanged`:
  `sfx_battering_ram_impact` (the Ashlands siege ram's piston landing on a gate),
  `sfx_wood_break`, `vfx_SawDust` and `fx_hit_camshake`. `fx_GP_Activation`, the Forsaken power
  activation, is still a valid name for anyone who wants it — its audio is
  `DarkMagic_HauntedMask2` and `DarkMagic_DeathWhisper`, which is why it sounds like a séance
  rather than a boot. Resolution tries `ZNetScene.GetPrefab` first and falls back to a single
  `Resources.FindObjectsOfTypeAll<GameObject>()` sweep for *all* remaining names at once, because
  only effects carrying a `ZNetView` are in `ZNetScene` — `sfx_battering_ram_impact` and
  `fx_hit_camshake` are not. The sweep prefers root objects, because a child inside some
  other prefab can share an effect's name, and only falls back to a child (of a prefab asset,
  `!scene.IsValid()`, never of a world instance) when no root exists. That fallback is what finds
  `sfx_battering_ram_impact`: its standalone prefab sits in bundle `c4210710` but nothing
  hard-references it, so it is never loaded; the ram's `m_punchEffect` is `fx_batteringram_fire`
  (smoke, spikes, shockwave, flame spikes) with the sound as a child, and instantiating that child
  gives the sound alone. `RPC_Kick` starts the swing before the bang, so an
  effect that throws cannot leave the door at normal speed.
- Effects without a `TimedDestruction` get `Destroy(go, EffectLifetime)` put on them. Most of the
  game's effects clean themselves up; a bare `ZSFX` that normally lives as a child of some machine
  does not, and spawned loose it would sit at the door forever.
- Everything is networked with one routed RPC (`ThisIsValheim_Kick`, the door's `ZDOID`) sent to
  `ZRoutedRpc.Everybody`, which includes the sender — `InvokeRoutedRPC` handles it locally and the
  server does not echo it back, so every client runs the handler exactly once and the bang lands
  on the kicker's own screen in the same frame. `ZNet` builds a fresh `ZRoutedRpc` per session, so
  registration hangs off a `Game.Start` postfix and is guarded against re-registering on the same
  instance (`m_functions.Add` throws on a duplicate). The handler bails out on a dedicated server.
  A receiver whose `ZNetScene.FindInstance` comes up empty simply has the door out of its loaded
  zones and does nothing.
- The fast swing is `Door.m_animator.speed`, wound up and put back. It cannot be tied to the
  animation's length: the state change arrives through `UseDoor` whenever the door's owner gets
  to it, so the animator is sped up *before* the animation starts and held through the round
  trip. `SwingRoutine` polls the animator's `state` parameter each frame and restores the speed
  once it has gone non-zero and the `open`-tagged state has finished (`normalizedTime >= 1`, not
  in transition), or as soon as it drops back to 0 — so a door shut right after the kick closes
  at normal speed. `SwingWindow` (1.5s) is only the cap. One coroutine per door in `swinging`, so a second kick on the same door
  does not restore the speed out from under the first.
- Nothing is shipped in `assets/` and nothing is spawned with a ZDO: the effects go up under
  `ZNetView.m_forceDisableInit` (the game's own idiom — the `ZNetView`, if there is one, destroys
  itself in `Awake`), because every client spawns its own copy off the one RPC.
