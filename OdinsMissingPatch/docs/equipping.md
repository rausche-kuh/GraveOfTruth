# Equipping

EquipWhileRunning.

- A hotbar press is `Player.UseHotbarItem` → `Humanoid.UseItem` → `Player.ToggleEquipped`. For
  an item whose `m_shared.m_equipDuration` is `0` (a torch, most tools) that equips on the spot
  through `EquipItem`; for everything with a duration (weapons, shields, armour - the prefab
  default is `1f`) it appends a `MinorActionData` (`Equip` or `Unequip`) to the private
  `Player.m_actionQueue`, and `UpdateActionQueue` (owner only, from `FixedUpdate`) plays the
  `equipping` animation on the upper body layer, counts `m_time` up by `dt` and calls
  `EquipItem` / `UnequipItem` when it passes the duration. A second press on a queued item
  removes it from the queue again. The same queue carries a crossbow's reload (`Reload`), which
  `UpdateWeaponLoading` re-queues on every frame the weapon is unloaded and no reload is queued.
- Nothing in that path asks whether the player is running. The block is `Player.CheckRun`
  (`Character.UpdateWalking` calls it every tick, owner only): after `Character.CheckRun` has
  accepted the run - run held, moving, not crouching, encumbered or dodging - it pays the sprint
  stamina and, when there is still stamina left, calls `ClearActionQueue()` and returns `true`.
  So a sprint wipes the queue on every tick it is actually running; a queued equip never
  accumulates time, and the press only lands once the sprint stops. Out of stamina, the player
  stops sprinting and the queue survives.
- `ClearActionQueue` is `Player`'s override of a protected virtual on `Humanoid`, and its other
  callers are an attack that just started (`Humanoid.StartAttack`), a jump (`Player.OnJump`) and
  a dodge that just fired (`Player.UpdateDodge`). `EquipWhileRunning` therefore does not patch
  the call away but flags the frames a `CheckRun` is on the stack (its prefix and postfix, the
  same shape as `CombatStamina`'s waiver) and has a `ClearActionQueue` prefix drop the wipe under
  the flag, so an attack, jump or dodge still interrupts an equip as in vanilla. `CheckRun` makes
  exactly that one call; check that still holds after a game update.
- The prefix does not skip the wipe outright: it removes the `Reload` entries and keeps the rest.
  Keeping the reload would let a crossbow load while sprinting, since the reload is re-queued
  every frame it is missing - a change to combat rather than to convenience, so the bolt still
  waits for the player to slow down.
- `EquipItem` itself refuses during an attack or a dodge, while swimming off the ground and for a
  broken item, so the equip landing mid sprint asks the same questions as one landing on foot.
  `InMinorAction()` is true while the equipping animation plays, which for the duration blocks
  a swing, a block, a bow draw and the guardian power (`Humanoid.StartAttack`,
  `Humanoid.IsBlocking`, `Player.UpdateAttackBowDraw`, `Player.StartGuardianPower`) but not
  the run itself: `Character.CheckRun` never asks for it.
