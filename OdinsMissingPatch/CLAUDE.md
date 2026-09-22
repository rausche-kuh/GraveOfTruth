# OdinsMissingPatch

A collection of quality of life changes, each one its own configurable tweak. See the root
`CLAUDE.md` for the shared build, the scripts and the environment.

**Shipped (0.1.0):** station range (a crafting station's build/craft/repair radius and how far its
extensions may stand from it) and comfort range (the radius Rested counts furniture in), both
doubled by default and both configurable. Both are client side.

**Unreleased:** endless fuel — every `Fireplace` (campfires, hearths, torches, braziers, the hot
tub) is kept topped up, so nothing that burns fuel for light ever goes out. Unlike the other two it
writes world state (the fuel on the fire's ZDO), see below. Mist clear range — every `Demister`
(the wisplight ball, wisp torches, whatever else clears the Mistlands mist) clears a wider circle,
doubled by default. Client side. Combat stamina — sprinting, jumping, swimming, sneaking, building,
chopping, mining and weapon swings cost nothing while nothing hostile is within 25m and nothing
that has noticed the player is coming for them; the bar also refills while swimming and mid
swing. Client side, one switch per cost. Instant comfort — sitting down by a fire grants Rested at
once, for the comfort of the spot, instead of after the ten seconds of Resting. Client side. Fast
portals — a portal trip ends as soon as the screen is black and the other side is loaded, not
after the fixed eight seconds; the fade is shorter too. Client side. Keep gear on death — items
of a configurable list of types (weapons, armour, ammo, tools, utility, trinkets, consumables by
default) stay in the inventory and stay equipped when the player dies, whatever the world's death
penalty; only the rest goes to the grave. Client side, owner-only code path.

| Path | What |
| --- | --- |
| `src/OdinsMissingPatch.cs` | BepInEx entry point: binds every tweak's config, then patches all. |
| `src/Tweak.cs` | The base class: the section, the `Enabled` switch, `BindMultiplier`, `OnSettingChanged`. |
| `src/Tweaks/<Name>.cs` | One quality of life change, with its `[HarmonyPatch]` classes nested inside it. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## Conventions

- A tweak is an `internal sealed class : Tweak` with a private constructor and a
  `static readonly Instance`, listed in `Tweaks` in `src/OdinsMissingPatch.cs`. That list is the
  only registration; nothing scans the assembly for tweaks.
- Patches are nested in their tweak, not in the plugin class — the root convention, one level down,
  so a tweak is one file holding both its settings and the code they drive.
- **Every patch is applied at startup regardless of the config**, and asks `Instance.On` (and reads
  its multipliers) each time it runs. That is what makes the switches work without a restart; never
  make patching itself conditional.
- A multiplier is `1` when its tweak is off, so callers multiply either way instead of branching.
  `BindMultiplier` clamps to 0.1–20: the value goes straight into a game field.
- A tweak that only reads its setting where it is used (`ComfortRange`) needs nothing else. One
  that *writes* game state on load (`StationRange` scales `m_rangeBuild` in `CraftingStation.Start`)
  must also rescale what is already loaded from `OnSettingChanged`, by the ratio of the new scale to
  the last applied one — the vanilla values are gone once they have been multiplied.
- The ratio only works when a rescale reaches everything the load patch touched. Where the game's
  registry is `OnEnable`/`OnDisable` based (`Demister`), something switched off during the change
  would come back at a stale scale and drift on the next ratio; `MistClearRange` instead keeps each
  instance's vanilla value in a `ConditionalWeakTable` and sets `vanilla * scale`, which is
  idempotent, so it can be applied on every `OnEnable` and every rescale without bookkeeping.
- `CraftingStation.m_allStations` / `StationExtension.m_allExtensions` (private statics, reachable
  thanks to the publicizer) are the registries of what is actually in the world; use them for a
  rescale rather than `Resources.FindObjectsOfTypeAll`, which also returns the prefabs — scaling a
  prefab would compound with the `Start` patch on every station spawned afterwards.
- Scale exactly what a rescale can reach again. A patch that writes to an object the game never
  registered (a placement ghost) leaves it stranded at whatever the multiplier was when it spawned,
  so `StationExtension`'s postfix repeats the game's own `GetZDO() != null` condition and skips it.
- A cost that is computed and spent inside one method (`CombatStamina`'s sprint, jump, swim and
  sneak) is waived by letting the method run untouched and dropping the spend at
  `Player.UseStamina`, under a static flag the method's prefix sets and its postfix clears. That
  keeps the skill XP, the empty-bar flash and the drown timer as vanilla, does not care what the
  drain formula is, and needs no game field to be zeroed and restored. It only works because each
  of those methods makes exactly one `UseStamina` call and none nest; check that before adding a
  fifth. A cost that is fetched from a getter and spent elsewhere (`GetBuildStamina`,
  `Attack.GetAttackStamina`) is zeroed at the getter instead, which also clears the
  `HaveStamina` gate that reads the same value.
- Anything decided per character (`Attack` runs for every character in the world) is waived only
  for `Player.m_localPlayer`; a check that is only ever clear when nothing hostile is near is
  exactly what a wandering boar would otherwise collect on.
- A list setting is one comma separated `ConfigEntry<string>` (`KeepGearOnDeath.KeepTypes`),
  parsed into a set once at bind and again from the entry's own `SettingChanged`, never per use.
  Unknown names are logged and skipped rather than failing the whole list; the description lists
  every valid name so a player never has to look them up.
- A game method that is a filter over a private list (`Inventory.MoveInventoryToGrave`,
  `RemoveUnequipped`) is replaced by a prefix that returns false and runs the same loop with the
  tweak's predicate folded in, rather than pulling items out of the list around the original: a
  throw inside the original would leave the pulled items nowhere, and this is the death path.

## Game facts worth keeping

- `CraftingStation.m_rangeBuild` is an instance field copied off the prefab, and the area marker
  circle plus the station's `m_effectAreaCollider` are both recomputed from it inside the private
  `GetExtensions()`, on a 2s timer. Scale the field and the check, the circle and the collider all
  follow — patching `GetStationBuildRange()` instead would let you build outside the circle drawn.
- What that recompute does *not* touch is `CircleProjector.m_nrOfSegments`, the number of markers
  the ring is laid out with. It is a fixed count from the prefab, so a wider circle is drawn by the
  same markers spread thinner until the ring reads as a dotted line. Scale it with the radius.
- The comfort radius is a literal `10f` in `SE_Rested.GetNearbyComfortPieces`, but it reaches the
  world through `Piece.GetAllComfortPiecesInRadius(p, radius, pieces)`, whose only caller that is.
  A `ref float radius` prefix there is the whole change, no transpiler needed.
- Build range and comfort are both decided client side (`Player.PlacePiece`, `Hud`, `Player`'s
  comfort level), so these tweaks need no server install and change nothing in the world.
- A fire's fuel is a float on its ZDO (`ZDOVars.s_fuel`), burnt down by whoever **owns** the ZDO
  in `Fireplace.UpdateFireplace`, a 2s `InvokeRepeating` tick that then calls the private
  `UpdateState()` to switch the flame and the full/half/empty models. Only the owner's writes are
  synced, so `EndlessFuel` checks `m_nview.IsOwner()` before it tops up — and the top-up sits in a
  postfix, after the burn, so a fire loaded after hours away does not dip through empty for one tick.
  Because it is world state, it needs the mod on whichever client owns the fire: a player without
  it standing closest lets the fire burn normally.
- A demister's radius is its `ParticleSystemForceField.endRange` (`Demister.m_forceField`, found in
  `Awake`): the force field pushes the mist particles out to it, and `ParticleMist` reads the same
  value to decide where mist is emitted around a demister and whether a point is inside one. One
  instance field, so scaling it is the whole tweak. `Demister.GetDemisters()` is the list of the
  enabled ones (`m_instances`, kept by `OnEnable`/`OnDisable`). Placement ghosts have their
  `Demister` destroyed by `Player.SetupPlacementGhost`, so they never register. The mist is rendered
  per client, so the wider circle is only seen by clients with the mod — the wisplight ball is a
  networked prefab, but its `Demister` runs its own `Awake` on every client. `ParticleMist` emits
  ~4πr² particles around each demister's edge, so the particle count grows with the square of the
  multiplier.
- `Fireplace.m_infiniteFuel` is the game's own eternal flame flag, but it also blanks the hover
  text, refuses fuel and lets the fire burn at 0 fuel showing its empty model — which is why the
  tweak tops the fuel up instead of setting the flag. `m_secPerFuel <= 0` fires do not burn at all.
- The player's stamina spends, and where each is decided: sprint in `Player.CheckRun` off
  `m_runStaminaDrain`, jump in `Player.OnJump` off `m_jumpStaminaUsage`, swim in
  `Player.OnSwimming` as a lerp between `m_swimStaminaDrainMinSkill` and `...MaxSkill`, sneak in
  `Player.OnSneaking` off `m_sneakStaminaDrain`, each ending in one `UseStamina` call. Place,
  repair and remove piece all fetch `Player.GetBuildStamina()`; every swing (tools included) fetches
  `Attack.GetAttackStamina()`, which can be negative (`m_staminaReturnPerMissingHP` is a refund).
  The bow's `m_drawStaminaDrain` has no reader in `assembly_valheim`. `Player.UseStamina` returns
  early on `0`, otherwise it is what restarts the regen delay (`m_staminaRegenTimer =
  m_staminaRegenDelay` in `RPC_UseStamina`).
- `Player.UpdateStats(float)` (the overload matters, there is a parameterless one) sets the regen
  rate to a local `0f` while `(IsSwimming() && !IsOnGround()) || InAttack() || InDodge() ||
  m_wallRunning || IsEncumbered()`, and `SEMan.ModifyStaminaRegen` after it only multiplies. The
  ZDO stamina write is inside the method, so a postfix that changes `m_stamina` is a frame late
  for other clients. Drowning (`OnSwimming`) starts only when `!HaveStamina()`, i.e. at 0.
- Hostility is `BaseAI.IsEnemy(a, b)` (static): factions, tame status, groups and aggravation
  (an aggravated dvergr is an enemy of players). `Character.GetAllCharacters()` is every loaded
  character, `BaseAI.BaseAIInstances` every loaded AI.
- Alert state (`BaseAI.IsAlerted()`, the animator's `alert`, the "!" effect) is on the ZDO as
  `s_alert` and a non-owner re-reads it every `UpdateAI`, so it is right on every client. A
  monster's target (`MonsterAI.m_targetCreature`) is **not** on the ZDO - only a "has a target"
  bool is - so on a client that does not own the monster `GetTargetCreature()` is null. What does
  cross the network is `MonsterAI.UpdateTarget` calling `Player.OnTargeted(sensed, alerted)` on its
  target when that is a player, which throttles to one `OnTargeted` RPC per ~0.5s per player (the
  timers advance on remote copies too) and lands in `Player.RPC_OnTargeted` on the player's own
  client. That is what `IsTargeted()` / `IsSensed()` (the stealth HUD eye) and the combat music
  timer are fed from, and what `CombatStamina` reads its enraged signal from: alerted, and coming
  for you, whoever owns the monster. `HuntPlayer()` monsters (bosses, event creatures) are alerted
  permanently and target the closest player within 200m, so they report too.
- Rested is handed out by Resting, not by the player: `Player.UpdateEnvStatusEffects` adds
  `Resting` (`SE_Cozy`, `resetTime: false`) every frame the conditions hold - a fire within the
  last 0.25s (`m_nearFireTimer`), and sitting or in shelter, and not wet, cold, freezing, burning
  or sensed - and removes it the frame they stop, so each rest is a fresh clone with `m_time` at
  0. `SE_Cozy.UpdateStatusEffect` adds its `m_statusEffect` (`Rested`, `resetTime: true`) on
  every tick past `m_delay` (10s). `SE_Rested.ResetTime` → `UpdateTTL` sets the duration to
  `m_baseTTL + (comfort - 1) * m_TTLPerComfortLevel` only if that is longer than what is left,
  so refreshing it every frame never shortens it. The comfort it reads is
  `Player.m_comfortLevel`, re-measured by `Player.UpdateBaseValue` on a 2s timer - stale for up to
  2s after walking in, which is why `InstantComfort` measures it again before the first grant.
  `IsSitting()` is the animator tag, so a chair, a bench and the sit emote all count.
- A portal trip is `Player.UpdateTeleport(dt)` (owner only, from `FixedUpdate`) counting
  `m_teleportTimer` against three literals: past `2f` the player is moved to the target, past
  `8f` (distant teleports only) *and* once `ZNetScene.IsAreaReady` the floor is looked for and
  the trip ends, past `15f` with no floor found the player is dropped at `GetSolidHeight`. The
  portal trigger itself has no delay. `FastPortals` does not touch the literals: once the loading
  screen is fully black it sets the timer to `8f`, so both gates are passed in one frame and the
  fallback keeps its seven seconds. The black screen is `Hud.m_loadingScreen` (a `CanvasGroup`),
  moved at `dt / Hud.GetFadeDuration(player)`, a literal `1f` unless dead or sleeping - and the
  fade out on arrival calls it with the teleport already over, so nothing on the player says why
  the screen is up; the tweak remembers that itself. `Player.TeleportTo` refuses a new trip while
  `m_teleportCooldown < 2f`, counted from the end of the last one - left alone, it is what keeps
  you from bouncing straight back through the portal you arrived at.
- Death and the inventory is `Player.CreateTombStone()`, called from `Player.OnDeath` (owner
  only) and skipped entirely when the inventory is empty or the world has
  `GlobalKeys.DeathKeepInventory`. It reads three more world modifier keys: unless
  `DeathKeepEquip` (or `DeathDeleteUnequipped`) it calls `Humanoid.UnequipAllItems()`, which
  walks the nine equipment slot fields through `UnequipItem(item, false)`; under
  `DeathDeleteItems` / `DeathDeleteUnequipped` it calls `Inventory.RemoveUnequipped()`; then it
  spawns `m_tombstone` and calls `Inventory.MoveInventoryToGrave(original)` on its container. Both
  `Inventory` methods filter on `!m_questItem && !m_equipped` and have no other caller, so
  "equipped" is the game's whole notion of "stays with you", which is why `KeepGearOnDeath` keeps
  its items equipped (skipping their `UnequipItem` while the local player's `CreateTombStone`
  runs) and folds its type list into both filters. Respawn goes through `Player.Load`, which
  unequips everything and then `EquipInventoryItems()` re-equips whatever has `m_equipped` set -
  so gear that goes through death equipped comes back worn. An empty tombstone destroys itself
  (`TombStone.UpdateDespawn`, not in use and zero items). `ItemType` is
  `ItemDrop.ItemData.ItemType`; `Ammo` is arrows and bolts, `AmmoNonEquipable` the rest,
  `Consumable` covers food and meads alike, `Misc` is coins and the like, `Tool` the hammer, hoe
  and cultivator.

## References

`~/Documents/Code/test/othervalheimmods/ValheimMods` is Crystal Ferrai's mod collection (Apache-2.0, published on
Thunderstore as `Crystal/*` and kept as a reference here — a checkout, not a dependency, and none
of its code is copied in). Five of its mods touch the same ground as the tweaks here and have been
shipped and played for far longer, so they are the thing to check before changing any of them:

- `BuildSpace/BuildSpacePlugin.cs` — the station build radius. Same shape as `StationRange`:
  scale `m_rangeBuild` in a `CraftingStation.Start` postfix, and walk `m_allStations` by the ratio
  of old to new multiplier when the setting changes. **The area marker's segment count is a fix taken from it.** It
  differs in setting the projector's radius and count itself (`radius * 4`) rather than leaving the
  radius to the game's own recompute, in leaving `m_extraRangePerLevel` and `StationExtension`
  alone, and in clamping the value in `SettingChanged` rather than declaring an
  `AcceptableValueRange`.
- `Comfortable/ComfortablePlugin.cs` — the comfort radius, plus the `EffectArea` heat radius and
  `SE_Rested`'s `m_baseTTL` / `m_TTLPerComfortLevel` (both worth having as tweaks of their own).
  It reaches the radius with a transpiler over `SE_Rested.GetNearbyComfortPieces`, swapping the
  inlined `10f` for the configured value and re-patching whenever the setting changes;
  `ComfortRange` instead widens the argument at the one call that literal is spent on, which needs
  no re-patching and does not care what the constant is. Their `Player.Awake`/`OnDestroy` roster
  and `ObjectDB` hook are the way to reach a live `SE_Rested`, if a rested-time tweak ever needs it.
- `ClearTheAir/ClearTheAirPlugin.cs` — the mist clear radius, and the model for `MistClearRange`.
  It scales `m_forceField.endRange` in a `Demister.Awake` postfix and walks `m_instances` by the
  ratio of old to new multiplier on a setting change. `MistClearRange` patches `OnEnable` instead
  (it runs after `Awake`, and again after a re-enable) and sets the range from a remembered
  vanilla value, see the conventions above; otherwise the same shape.

- `ProperPortals/ProperPortalsPlugin.cs` — the portal wait, and what `FastPortals` was checked
  against. It replaces the `2f` / `8f` / `15f` literals through a transpiler with
  `FadeTime` / `FadeTime + MinPortalTime` / `+ 0.5f`, which bakes the config into the IL, so it
  unpatches and re-patches on every setting change; it also prefixes `Hud.GetFadeDuration` for
  the fade in only. `FastPortals` jumps the timer from a prefix once the screen reads as black,
  needs no re-patching and covers the fade out too. Its `Inventory.IsTeleportable` override and
  the `m_activationRange` / `m_proximityRoot` tweak are the place to start if either becomes a
  tweak.

- `DeathPenalty/DeathPenaltyPlugin.cs` — the *other* half of the death penalty: skill loss
  percent, level progress reset, the no-skill-loss and corpse-run effect durations. It never
  touches the inventory, so `KeepGearOnDeath` was built from the game code alone; it is the place
  to start if skill loss ever becomes a tweak (`Skills.m_DeathLowerFactor`,
  `Player.m_hardDeathCooldown`, `TombStone.m_lootStatusEffect.m_ttl`).

All of them take a hard dependency on shudnal's `ConditionalConfigSync` so a server can enforce the values
on every client, and all default to vanilla and do nothing until configured. None of that applies here
— this mod is meant to be dropped in and to change something — but server enforcement is the answer
if a tweak ever stops being purely local.

`~/Documents/Code/test/othervalheimmods/Digitalroot.Valheim.EternalFire` is Digitalroot's Eternal Fire (AGPL-3.0,
[GitHub](https://github.com/Digitalroot-Valheim/Digitalroot.Valheim.EternalFire), Thunderstore
`Digitalroot/Eternal_Fire`), the same kind of reference checkout and the model for `EndlessFuel`;
none of its code is copied in — the AGPL would bind the whole mod.

- `src/Digitalroot.Valheim.EternalFire/Patch.cs` + `Main.cs` — a `Fireplace.UpdateFireplace`
  **prefix** that sets the ZDO's `fuel` to `m_maxFuel`, plus `CookingStation.SetFuel` /
  `Smelter.SetFuel` prefixes (and an `AddFuel` RPC from their `Awake`) so the oven, smelter, blast
  furnace and eitr refinery can be made eternal too. One `bool` per vanilla prefab name, matched
  on `name` with `(Clone)` stripped, plus a comma separated custom list, all synced from the server
  through Jötunn. `EndlessFuel` keeps the ZDO top-up but moves it to a postfix (no one-tick dip
  after a long absence), only writes as the owner (a non-owner's write is never synced) and skips
  `m_infiniteFuel` / `m_secPerFuel <= 0` fires. It covers every `Fireplace` with one switch instead
  of a list, and leaves the ovens and smelters alone: those are not light sources, and turning them
  eternal is an economy change rather than a convenience. Their `SetFuel` prefix is the place to
  start if that ever becomes a tweak.

`~/Documents/Code/test/othervalheimmods/cartur-safe-stamina` is Cartur's Safe Stamina (Thunderstore
`Cartur/Carturs_Safe_Stamina`, [GitHub](https://github.com/jekkle/cartur-safe-stamina)), the model
for `CombatStamina` and the mod it replaces in the profile. The checkout has **no license file**,
so it is all rights reserved: read it, copy nothing.

- `src/Plugin.cs` — the same seven patch points, arrived at independently by reading the game:
  `Attack.GetAttackStamina` and `Player.GetBuildStamina` postfixed to 0, `CheckRun` / `OnJump` /
  `OnSwimming` / `OnSneaking` with the drain **field** zeroed in a prefix and restored in a
  postfix, and an `UpdateStats(float)` postfix that re-runs the regen line on the frames vanilla
  zeroes it. Its safe check is one thing: no `BaseAI.IsEnemy` character within `SafeRadius`,
  cached for 0.25s. `CombatStamina` keeps that radius and adds the enraged signal from
  `RPC_OnTargeted`, and waives the four movement costs at `UseStamina` under a scope flag instead
  of zeroing the fields (see the conventions). Its README's "worth knowing" list - free swimming
  means no drowning, skills still level, other characters pay - all holds here too.
