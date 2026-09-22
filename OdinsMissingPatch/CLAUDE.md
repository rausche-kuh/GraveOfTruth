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
penalty; only the rest goes to the grave. Client side, owner-only code path. Area repair — one
`Player.Repair` swing carries on to every damaged `Piece` within a configurable radius (10m by
default) of the one the player aims at, closest first, at the game's own cost per piece. It writes
world state through the game's own `WearNTear.Repair` RPC, so it needs no server install.
Nearby crafting — the game's requirement checks and spends see the player-placed chests within a
configurable range (20m) as part of the backpack, backpack paying first; an ingredient amount
the chests have to pay for shows yellow, and its tooltip lists carried vs. in chests. Quick stack — a hotkey
(. by default; G is bound by the game) moves every carried stack into the nearest chest in range that already holds that item, with a
glow and a floating count per chest; an Alt-click marks a stack as a favourite (golden frame),
which quick stacking skips, as it does equipped items and the hotbar. Nearby fuel — the four
manual add-fuel interactions (fire, smelter, oven, shield generator) see the chests the same way,
so a unit comes out of a chest when the backpack has none; nothing refuels itself. All three
write chest inventories, which is world state, see below; a "Nearby use" button in the chest
panel keeps a chest out of all three. Auto repair — pressing Use on a crafting station
repairs every worn item in the inventory that station could repair, asking the crafting
panel's own `CanRepair` per item, instead of one item per click of the repair button.
Client side, and repairing is free in vanilla, so there is nothing to pay.

| Path | What |
| --- | --- |
| `src/OdinsMissingPatch.cs` | BepInEx entry point: binds every tweak's config, then patches all. |
| `src/Tweak.cs` | The base class: the section, the `Enabled` switch, `BindMultiplier`, `OnSettingChanged`. |
| `src/Tweaks/<Name>.cs` | One quality of life change, with its `[HarmonyPatch]` classes nested inside it. |
| `src/NearbyChests.cs` | Shared by the three chest tweaks: the registry of loaded containers, the in-reach rule, `Claim`, the "reach" that widens the backpack, the per-chest opt-out flag with its panel button and hover line. |
| `src/ChestGlow.cs` | The golden pulse plus floating text on a chest (`ChestGlow.Flash`), for quick stack and anything that later needs to point at a chest. |
| `src/Hotkeys.cs` | `Pressed` / `Held` for a `KeyboardShortcut`, read through `ZInput`. |
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
- A tweak that repeats a game action on many objects (`AreaRepair`) pays the game's own price for
  each one and asks the game's own questions about each one, rather than making the first action
  cheaper or wider. The piece the game already handled is simply asked again and refuses by itself,
  which keeps the patch a postfix with no special case for it and no way to pay twice.
- A game method that is a filter over a private list (`Inventory.MoveInventoryToGrave`,
  `RemoveUnequipped`) is replaced by a prefix that returns false and runs the same loop with the
  tweak's predicate folded in, rather than pulling items out of the list around the original: a
  throw inside the original would leave the pulled items nowhere, and this is the death path.
- A tweak that lets a game action reach into chests (`NearbyCrafting`, `NearbyFuel`) does not
  patch the action: it opens `NearbyChests.EnterReach(range)` in a prefix on the action and closes
  it in a **finalizer** (`__state` says whether it opened), and the three `Inventory` methods the
  action goes through (`CountItems`, `HaveItem(string)`, `RemoveItem(string, ...)`) are widened
  in one place while a reach is open, for the local player's backpack only. The backpack pays
  first: the remove prefix lowers `amount` to what is carried and takes the rest from chests. A
  finalizer rather than a postfix because Harmony skips postfixes when the original throws, and
  a reach left open would make every later backpack read see the chests. Nothing outside an
  opened reach touches a chest, so "which actions" is the whole design decision of such a tweak,
  and each is named in the tweak. Nested reaches keep the innermost range.
- Shared code that is not a tweak (`NearbyChests`) may hold patches of its own when the thing
  they serve belongs to no single tweak (the registry, the opt-out button); they gate on
  `NearbyChests.AnyTweakOn` rather than on one tweak.
- Every chest write goes through `NearbyChests.Claim`: re-checks the in-reach rule (someone may
  have opened the chest since it was found), reloads the inventory from the ZDO, then claims
  ownership. `Find` hands out one reused list, so copy it before a loop that writes.

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
- Repairing is `Player.Repair(toolItem, repairPiece)` (private, called from `Player.Update` when
  the selected build piece is `m_repairPiece` and attack is pressed): it repairs
  `GetHoveringPiece()` alone, after `CheckCanRemovePiece` (the piece's `m_craftingStation` within
  `CraftingStation.HaveBuildStationInRange` of the *player*, waived by `m_noPlacementCost` /
  `GlobalKeys.NoWorkbench`) and `PrivateArea.CheckAccess`, then spends `GetBuildStamina()`,
  `m_attack.m_attackEitr` and `m_useDurabilityDrain * Game.m_durabilityRate`. `WearNTear.Repair()`
  is the whole write and is idempotent on its own: it returns false at full health and false again
  within `1f` of the last repair (`m_lastRepair`), otherwise it sends `RPC_Repair` — so the health
  is set by whoever owns the piece and the mod is never needed on the other side.
- Repairing an item is the crafting panel's repair button: `InventoryGui.OnRepairPressed` →
  `RepairOneItem()`, which walks `Inventory.GetWornItems` (every item with `m_useDurability`
  below its max) and repairs the **first** one the private `CanRepair(item)` accepts, then
  returns. `CanRepair` is the whole question: the item may be repaired at all
  (`m_canBeReparied`), the player's current station is named by the item's recipe as its
  `m_craftingStation` or `m_repairStation` (or `item.m_worldLevel < Game.m_worldLevel`), and
  the station's level is at least the recipe's `m_minStationLevel`. A repair costs nothing:
  it raises Crafting by the wear it mended, sets `m_durability` to `GetMaxDurability()` and
  plays `CraftingStation.m_repairItemDoneEffects`. `m_canRepair` is not asked there - it is
  what hides the button in `UpdateRepair` - so a tweak that repairs by itself has to ask it.
- `CraftingStation.Interact` is the whole of opening a station: for the local player, after
  `InUseDistance` and `CheckUsable`, it calls `Player.SetCraftingStation(this)` and shows the
  crafting panel. That is the only caller that sets a station, and `Player.UpdateStations`
  clears it again the frame the panel closes or the player walks out of range, so "the local
  player now holds this station" is exactly one station opening.
- `Piece.s_allPieces` is the registry of every loaded piece (private static, publicized), and
  `Piece.s_ghostLayer` is the layer the placement ghost sits on; the game's own radius searches
  (`GetAllPiecesInRadius`, `GetAllComfortPiecesInRadius`) walk the one and skip the other, which is
  what `AreaRepair` repeats. `Player.PlacementCostDisabled` is the public read of
  `m_noPlacementCost`.
- `ZInput.GetKey(KeyCode, logWarning)` is the game's own keyboard read and is null-safe before
  `ZInput` exists; the legacy `UnityEngine.Input` is not what Valheim reads. `JoyAltKeys` (the
  copy-piece modifier at the repair call site) is bound to a gamepad trigger only, so `LeftAlt`
  reaches `Repair` untouched and is free for a modifier of our own.
- A `Container`'s inventory is a `ZDO` byte blob (`s_items`), written by `Container.Save` only
  when the local client **owns** the ZDO, and read back by `Load` in a 1s `CheckForChanges` tick
  whenever the data revision moved and the chest is not in use. Opening a chest is an
  `RPC_RequestOpen` to the owner, who refuses if it is in use, else hands the ZDO over
  (`SetOwner`) and answers `RPC_OpenResponse`; "in use" is `m_inUse` on the owner and `s_inUse`
  on the ZDO for everyone else, and a cart's hold also asks `Vagon.InUse()`. So a write from afar
  is: reload, `ClaimOwnership`, edit the `Inventory` (its `m_onChanged` calls `Save`). There is no
  lock; the in-use check is the only courtesy, which is why it is re-asked right before a write.
  `CheckAccess(playerId)` (private) is the privacy setting and dereferences `m_piece`, null on a
  container whose `Piece` sits on a parent (ship hold); the ward check is
  `m_checkGuardStone && !PrivateArea.CheckAccess(pos, 0, flash: false)`, as in `Interact`.
- "Placed by a player" is `Piece.IsPlacedByPlayer()` = `s_creator != 0`. A ruin's chest is the
  same prefab with creator 0; loot chests have no `Piece`. Tombstones are a `Container` with a
  `TombStone` beside it. No static list of containers exists; `Container.Awake` (only when the
  object has a ZDO, so never a prefab or a placement ghost) is the place to register one.
- `Inventory.AddItem(ItemData)` (what `StackAll` and `MoveItemToThis` use) is a merge-then-place:
  it bumps the chest's own stacks one unit at a time and, for the remainder, moves the very
  `ItemData` object into a free slot. **True** means everything went and the caller must remove
  the object from the source (the unit count on it was never decremented when it all merged);
  **false** means the merged part was subtracted from `item.m_stack`, the object is still the
  source's, and an error line was logged — hence `HaveEmptySlot() || FindFreeStackSpace() > 0`
  first. `FindFreeStackItem` matches name, quality, world level and cheated flag, not
  `m_customData`, so a flagged stack absorbs unflagged units and keeps its flag.
- `ItemData.m_customData` is a string dictionary saved with the item (inventory blob, character
  file, dropped item) and copied by `Clone()`, so it is the place for a per-stack flag
  (`QuickStack`'s favourite); splitting a stack copies the flag.
- The requirement checks, and where each spends: `Player.HaveRequirementItems` (private; counts
  per quality level 1..max and takes the best) behind `HaveRequirements(Recipe, ...)`, which with
  `discover: true` reads `m_knownMaterial` and no inventory; `HaveRequirements(Piece, mode)` for
  the build menu, the ghost and `Hud`; `ConsumeResources` for a craft, an upgrade and
  `PlacePiece`; `InventoryGui.SetupRequirement` (static, takes the `Player`) for every ingredient
  row of the crafting panel and the piece info, per frame; it shows only the needed amount
  (`res_amount`, a `TMP_Text`, reachable as `Graphic` for its colour) in white, or blinking red
  when the count falls short, and sets the row's `UITooltip.m_text` to the item name (the HUD
  rows have one too, but no cursor to hover them). Harmony's `__state` may be a struct, which
  is how `NearbyCrafting.RowScope` carries the carried count taken before the reach opens into
  its postfix. A `m_requireOnlyOneIngredient` recipe
  additionally looks the ingredient up in the backpack (`GetFirstRequiredItem`), so a widened
  count there promises a craft the lookup then fails; `NearbyCrafting` leaves those recipes alone.
  `InventoryGui.UpdateRecipeList` counts every ingredient of every recipe per quality level in one
  frame, hence the per-frame chest count cache.
- Manual refuelling is one unit per Use: `Fireplace.Interact`, `Smelter.OnAddFuel`,
  `CookingStation.OnAddFuelSwitch`, `ShieldGenerator.OnAddFuel` (a list of fuels), each a
  `HaveItem(name)` then `RemoveItem(name, 1)` on the user's inventory and an `RPC_AddFuel`; the
  game refuses once `fuel > maxFuel - 1`. `Fireplace.UseItem` is the hotbar drop and takes the
  item it is given. A kiln is a `Smelter` with no `m_fuelItem` (its wood is the ore).
- `MaterialMan.instance.SetValue(go, ShaderProps._EmissionColor / _Color, color)` and
  `ResetValue` tint every renderer under `go` through a property block, which is how
  `WearNTear.Highlight` flashes a piece; `ShaderProps` is in `assembly_utils`. A ship's hold has
  `m_rootObjectOverride`, so the ship is what glows.
- `DamageText.AddInworldText(type, pos, distance, text, mySelf)` (private) is the floating combat
  text without the RPC that `ShowText` sends to everyone; `TextType.Bonus` is the large orange one
  that lingers 3s.
- `InventoryGrid.OnLeftDown` is where a click on a slot is turned into the select callback that
  picks the item up, so a prefix returning false is a clean veto; Shift and Ctrl are the game's
  split and move modifiers there, Alt is free. `InventoryElement.m_equiped` is the frame `Image`
  drawn over an equipped item, toggled by `enabled`, and a tinted copy of it is a frame of our own.
  `UpdateGui` rebuilds every element when the inventory changes size.
- `Player.TakeInput()` is false while any GUI is open, the inventory included; a hotkey that should
  work with the inventory open has to re-ask the chat, console, text input and menu itself.
  `KeyboardShortcut.IsDown` (BepInEx) refuses while any key outside the combination is held, i.e.
  while walking, and reads legacy input; read the shortcut's keys through `ZInput` instead.
- `InventoryGui.m_takeAllButton` / `m_stackAllButton` are the chest panel's buttons; the panel is
  shown from `UpdateContainer` only while `m_currentContainer.IsOwner()`, so a flag set on the
  open chest's ZDO from there always sticks. TextMeshPro is not among the staged reference
  assemblies (`lib/`), so a copied button's label is set through its `text` property by
  reflection rather than through `TMP_Text`.

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

`~/Documents/Code/test/othervalheimmods/VentureValheim` is OrianaVenture's mod collection (MIT,
[GitHub](https://github.com/OrianaVenture/VentureValheim)), another reference checkout; its
`AreaRepair` is the model for `AreaRepair`, published as
`VentureValheim/Venture_Area_Repair`. None of its code is copied in — MIT would need the notice.

- `AreaRepair/src/AreaRepair.cs` — the same `Player.Repair` postfix over a distance sorted
  `Piece.s_allPieces`, with the same per piece cost and station cache. It hard-codes 20m and has no
  config at all, and reads its single-repair modifier through a `Player.Update` transpiler that
  stores `ZInput.GetKey(KeyCode.LeftAlt)` in a static each frame; `AreaRepair` makes the radius and
  the key settings and reads the key in the postfix itself, where the swing has just happened, so
  no transpiler is needed. It also gates on `HaveStamina` only, where this one also stops before
  the hammer breaks, and skips the eitr check its own TODO asks for.

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

`~/Documents/Code/test/othervalheimmods/SmartCraft-Storage` is Zellds' SmartCraft-Storage
(Thunderstore `Zellds/SmartCraftStorage`, [GitHub](https://github.com/Zellds/SmartCraft-Storage)),
the model for `NearbyCrafting` and `QuickStack` and the mod they replace in the profile. The
checkout has **no license file**, so it is all rights reserved: read it, copy nothing. It needs
Jötunn and syncs its gameplay settings from the server; this mod does neither.

- `Shared/NearbyContainers.cs` — its chest search: a masked `Physics.OverlapSphere` cached for
  0.25s per origin, `GetComponentInParent<Container>` (plus `Vagon.m_container` for carts), and
  the same in-use / privacy / ward rules, re-checked in `TryClaimWriteAccess` before every write
  (its comments spell out why: `ClaimOwnership` is no lock). `NearbyChests` keeps the rules and
  the re-check but walks a registry filled from `Container.Awake` instead of the physics world,
  and adds "placed by a player" and the per-chest opt-out, which it does not have (it locks
  individual stacks instead).
- `CraftingChestAccess/InventoryChestPatches.cs` — the same three `Inventory` patches, gated on
  "the crafting station is set or the player is in place mode"; `NearbyChests` gates on a reach
  the tweak opens around the named game actions instead, so hand crafting works and nothing
  else in those modes sees the chests. Its `ChestCountCache` is the same per-frame idea.
  `RequirementAmountPatch.cs` prints the available amount in brackets after the requirement,
  which is worth having.
- `QuickStack/QuickStackService.cs` — the same "only into chests that already hold it" rule,
  moved stack by stack with `MoveItemToThis` into matching stacks then empty slots; `QuickStack`
  uses `AddItem`'s own merge-then-place. `ItemMarking/` is its lock (a `m_customData` flag, an
  overlay `Image` per slot from `InventoryGrid.UpdateGui`, toggled from an `OnLeftDown` prefix)
  — the same shape as the favourite, arrived at from the game code. `Hotkeys/HotkeyPatch.cs`
  documents the `KeyboardShortcut.IsDown` problem.
- `Repair/RepairAllPatch.cs` — the same repair loop, arrived at from the same game code: a
  `RepairOneItem` prefix that walks the worn items and repairs every one `CanRepair` accepts.
  It hangs off the repair button, where `AutoRepair` hangs off opening the station, so no
  click is needed at all; it also plays the station effect once per item and does not ask
  `m_canRepair`.
- `Stations/*` — its stations pull ore, food and fuel by themselves on their update ticks and
  store their output; the roadmap wanted none of that, so `NearbyFuel` widens the manual
  add-fuel interactions and nothing else.
