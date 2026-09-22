# Conventions

How a tweak is written, beyond the four rules in the mod's `CLAUDE.md` that always apply. The
subsystem docs add their own rules on top: see `chests.md`, `inventory-ui.md`, `item-order.md`.

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
