# The inventory screen

`src/PanelButtons.cs`, `src/InventorySorter.cs` and the tweaks that draw into the screen
(ChestButtons, InventoryButtons, the Nearby use button).

## Conventions

- A button in the inventory screen comes from `PanelButtons.Create`, a copy of the chest
  panel's Take all button with the label blanked and an icon in its place, so it keeps the
  game's skin and sounds without a prefab of our own. Buttons go *beside* the panels, never on
  them - the panels are grid to the edge, and anything on them covers a slot. They hang off the
  panel's top right corner, outside its border: `PanelButtons.Pin` works in the parent's
  bottom-left space so anchors do not matter, `ColumnLeft` is the x every button's left edge
  starts at (past the panel's stretched `Bkg` border plus a gap - not lined up with the armour
  and weight boxes, whose rects overlap that border) and `ColumnTop` the y the first button's
  top edge sits at (level with the border's top). Buttons beside the inventory panel `Enlist`
  in one column that `LayoutInventoryColumn` centres in the gap between the armour box and
  the weight box (found through the `m_armor` / `m_weight` texts' parents by reflection, or
  the panel's `Armor` / `Weight` children, their edges taken in panel space so anchoring does
  not matter), active buttons only, so it closes up around whatever a tweak hides - at most
  two show at once, since fill your stacks (chest open) and stack nearby (no chest) never
  meet, and two is all the gap holds; the chest's four stand in one column from the top down
  (`LayoutChestColumn`). Each owner places from its
  per-frame postfix (`UpdateContainer`, `UpdateInventory`) rather than once at creation, since
  buttons come and go with their tweaks and the panel resizes with the inventory. The Nearby
  use button takes the game's Take all spot (top left of the chest panel) while
  `ChestButtons.HidesVanilla`, else the top of that column; it is widened to its label's TMP
  `preferredWidth` (reflection) plus a margin each side, since the label outgrows Take all.
  Cancel any drag first (`SetupDragItem(null, null, 1)`), as the game's buttons do, so a held
  item is not moved under the cursor.
- A sort never adds or drops a unit: `InventorySorter` merges by the game's own stack rule plus
  variant and custom data (so a tagged stack never swallows a plain one), refuses without
  touching anything when the free slots would not hold the items, and only ever writes
  `m_gridPos` and `m_stack` before one `Changed()`. Items above the chosen row and items the
  caller keeps hold their slot; the rest flows around them. The backpack's Sort keeps what the
  player has equipped (`m_equipped` or `Player.IsItemEquiped`, as Quick Stack reads it) and every
  favourite, so a kept stack is never moved and never merged into either. A chest's Sort keeps
  nothing: neither flag means anything outside the backpack.

## Game facts

- `InventoryGrid.OnLeftDown` is where a click on a slot is turned into the select callback that
  picks the item up, so a prefix returning false is a clean veto; Shift and Ctrl are the game's
  split and move modifiers there, Alt is free. `InventoryElement.m_equiped` is the frame `Image`
  drawn over an equipped item, toggled by `enabled`; its `RectTransform` is what a mark of our own
  borrows to sit exactly on the slot. A mark laid over that frame has to be a border rather than a
  fill, or an equipped item and a favourite look the same — four stretched `Image`s with no sprite,
  anchored to one side each and pivoted onto it, need no asset.
  `UpdateGui` rebuilds every element when the inventory changes size.
- `Player.TakeInput()` is false while any GUI is open, the inventory included; a hotkey that should
  work with the inventory open has to re-ask the chat, console, text input and menu itself.
  `KeyboardShortcut.IsDown` (BepInEx) refuses while any key outside the combination is held, i.e.
  while walking, and reads legacy input; read the shortcut's keys through `ZInput` instead.
- `InventoryGui.m_takeAllButton` / `m_stackAllButton` are the chest panel's buttons; the panel is
  shown from `UpdateContainer` only while `m_currentContainer.IsOwner()`, so a flag set on the
  open chest's ZDO from there always sticks. TextMeshPro is not among the staged reference
  assemblies (`lib/`), so a copied button's label is set through its `text` property by
  reflection rather than through `TMP_Text`; its colour is the `Graphic.color` every UI text
  has, which is what tints a button's icon to match. `OnTakeAll` / `OnStackAll` are the two
  buttons' handlers (private, publicized): `Inventory.MoveAll(from)` is take all, and
  `Inventory.StackAll(from)` moves what `this` already holds by name, skipping only what the
  local player has equipped - so the game's Stack all empties the hotbar too, and, since it adds
  through `AddItem`, it spills whatever does not fit the existing stacks into free slots. Fill
  your stacks does not use it: `ChestButtons.TopUp` merges unit for unit into the stacks the
  target already has and stops at their caps, so the button never opens a stack that was not
  there - take all is the button for that. Both cancel a
  drag first with `SetupDragItem(null, null, 1)`. `InventoryGui.UpdateInventory(Player)`
  refreshes the backpack grid every frame the screen is up; `m_player` is the inventory panel's
  `RectTransform`, `m_container` the chest panel's. An item's slot is nothing but
  `m_gridPos`; the grid redraws from it on its next `UpdateGui`, so a sort is setting positions
  and one `Changed()`.
- The inventory screen's geometry (from the `_GameMain` prefab, see the workspace CLAUDE.md for
  how to read it): the `Player` panel is 570x287 (taller with more rows, `SetInventorySize`),
  its grid fills it to the edges, and the `Container` panel is a child of it, 570x340, hung
  30px below. Each panel's visible background is a stretched `Bkg` child with a 20px size delta,
  so it reaches 10px past the rect on every side. The armour box (80x64) and weight box (80x64)
  hang centred 32px right of the inventory panel's rect, anchored at its right edge at half
  height (armour) and at the bottom (weight), with a 91px gap between them - so their rects
  start 8px inside the panel and overlap its border, which is why buttons are placed from the
  border rather than from the boxes. The 91px between the armour box's bottom and the weight
  box's top is just room for a column of two 40px buttons with a 6px gap, and there is no room
  above the armour box (40px to the rect top) for a third; the chest's weight box (80x60)
  sits at the bottom of the same column beside the chest panel, well clear of a column of
  four from the top. On the chest panel's top 46px band: Take all
  (133x40) at the left, the name centred, Stack all at the right - there is no free width on
  it. All of this is anchored to a point, so a rect's centre from its parent's bottom left is
  `anchor * parentSize + anchoredPosition + (0.5 - pivot) * size`.
- `UITooltip` (assembly_guiutils) shows nothing without an `m_tooltipPrefab`; the slot prefab
  (`InventoryGrid.m_elementPrefab`, `InventoryElement.m_tooltip`) has one to borrow for a
  button copied without a tooltip. `m_topic` is the header line, `m_text` the body.
- `UnityEngine.ImageConversionModule`, where `Texture2D.LoadImage` lives, is built against
  netstandard 2.1 and cannot be referenced from a net472 build (CS1705); `PanelButtons.LoadPng`
  reaches `ImageConversion.LoadImage` by reflection instead. Everything else in `lib/` binds.
