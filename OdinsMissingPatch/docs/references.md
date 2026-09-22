# Reference checkouts

Other people's mods that solve the same ground, kept in `~/Documents/Code/test/othervalheimmods/`.
Read them before changing a tweak they cover; copy nothing out of them (see the root `CLAUDE.md`
for the licences).

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
