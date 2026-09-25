using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// The chest panel's Take all and Stack all give way to five icon buttons, each hovering to
    /// a name and a line on what it does. Beside the inventory panel, at the top of the
    /// InventoryButtons column between the armour and weight boxes: fill the stacks you carry
    /// from the chest, up to their caps and no further - it never opens a stack you did not
    /// already have. Beside the chest panel, in a column from its top: take all, place all (everything you carry that is not equipped,
    /// a favourite or in the hotbar), fill the chest's stacks from your backpack, and sort the
    /// chest.
    /// Switching the tweak off puts the game's own two buttons back.
    /// </summary>
    internal sealed class ChestButtons : Tweak
    {
        internal static readonly ChestButtons Instance = new ChestButtons();

        private ChestButtons() { }

        private ConfigEntry<bool> includeHotbar;

        internal override string Section => "Chest Buttons";

        protected override string Summary =>
            "Replaces the chest panel's Take all and Stack all with icon buttons beside the " +
            "panels: fill your stacks from the chest in the column beside the inventory; take " +
            "all, place all, fill the chest's stacks from your backpack and sort the chest in a " +
            "column down the side of the chest.";

        protected override void Bind(ConfigFile config)
        {
            includeHotbar = config.Bind(Section, "IncludeHotbar", false,
                "Whether Fill chest and Place all take from the hotbar row too. Off keeps your " +
                "tools, food and arrows.");
        }

        /// <summary>Whether the game's Take all and Stack all are hidden behind the buttons right now.</summary>
        internal static bool HidesVanilla => Panel.VanillaHidden;

        /// <summary>The inventory screen was destroyed; its buttons went with it.</summary>
        internal static void Forget() => Panel.Forget();

        // ---- The actions ---------------------------------------------------------------------

        /// <summary>The panel, player and chest a click may act on, or null: same gate as the game's own buttons.</summary>
        private static InventoryGui Ready(out Player player, out Container chest)
        {
            InventoryGui gui = InventoryGui.instance;
            player = Player.m_localPlayer;
            chest = gui != null ? gui.m_currentContainer : null;
            if (gui == null || player == null || player.IsTeleporting() || chest == null || !chest.IsOwner())
            {
                return null;
            }
            return gui;
        }

        private static void TakeAll()
        {
            InventoryGui gui = Ready(out _, out _);
            if (gui != null)
            {
                gui.OnTakeAll();
            }
        }

        /// <summary>
        /// Tops the stacks you carry up out of the chest, and no further: the game's own Stack all
        /// would spill the rest into your free slots, which turns a top-up into a second stack you
        /// never asked for. Take all is the button for that.
        /// </summary>
        private static void FillInventory()
        {
            InventoryGui gui = Ready(out Player player, out Container chest);
            if (gui == null)
            {
                return;
            }
            gui.SetupDragItem(null, null, 1);
            int moved = TopUp(player.GetInventory(), chest.GetInventory());
            Report(gui, player, moved, "$omp_took", "$omp_took_none");
        }

        /// <summary>
        /// Moves what fits into the stacks <paramref name="target"/> already holds and nothing
        /// more: no free slot is taken, so nothing the target does not already carry appears in
        /// it and no stack grows past its cap. What may merge is the game's own rule, from
        /// Inventory.FindFreeStackItem - same name, quality, world level and cheat flag, and room
        /// left under the cap. Returns how many units went.
        /// </summary>
        private static int TopUp(Inventory target, Inventory source)
        {
            int moved = 0;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(source.GetAllItems()))
            {
                if (item.m_shared.m_maxStackSize <= 1)
                {
                    continue;
                }
                int before = item.m_stack;
                foreach (ItemDrop.ItemData stack in target.GetAllItems())
                {
                    if (item.m_stack <= 0)
                    {
                        break;
                    }
                    if (!Merges(stack, item))
                    {
                        continue;
                    }
                    int fits = Mathf.Min(stack.m_shared.m_maxStackSize - stack.m_stack, item.m_stack);
                    stack.m_stack += fits;
                    item.m_stack -= fits;
                }
                if (item.m_stack == before)
                {
                    continue;
                }
                moved += before - item.m_stack;
                if (item.m_stack <= 0)
                {
                    source.RemoveItem(item);
                }
            }
            if (moved > 0)
            {
                target.Changed();
                source.Changed();
            }
            return moved;
        }

        /// <summary>Whether a unit of <paramref name="item"/> may join the stack <paramref name="stack"/>.</summary>
        private static bool Merges(ItemDrop.ItemData stack, ItemDrop.ItemData item)
        {
            return stack.m_stack < stack.m_shared.m_maxStackSize
                && stack.m_shared.m_name == item.m_shared.m_name
                && stack.m_quality == item.m_quality
                && stack.m_worldLevel == item.m_worldLevel
                && stack.m_cheated == item.m_cheated;
        }

        private static void FillChest()
        {
            InventoryGui gui = Ready(out Player player, out Container chest);
            if (gui == null)
            {
                return;
            }
            gui.SetupDragItem(null, null, 1);
            int moved = Instance.MoveToChest(player, chest, onlyExisting: true);
            Report(gui, player, moved, "$omp_stacked_chest", "$omp_stacked_chest_none");
        }

        private static void PlaceAll()
        {
            InventoryGui gui = Ready(out Player player, out Container chest);
            if (gui == null)
            {
                return;
            }
            gui.SetupDragItem(null, null, 1);
            int moved = Instance.MoveToChest(player, chest, onlyExisting: false);
            Report(gui, player, moved, "$omp_placed", "$omp_placed_none");
        }

        private static void SortChest()
        {
            InventoryGui gui = Ready(out _, out Container chest);
            if (gui == null)
            {
                return;
            }
            gui.SetupDragItem(null, null, 1);
            InventorySorter.Sort(chest.GetInventory(), 0, null);
        }

        /// <summary>
        /// The count goes into <paramref name="done"/> here, since the message hud localizes a
        /// token but cannot fill in a $1; <paramref name="nothing"/> it translates by itself.
        /// </summary>
        private static void Report(InventoryGui gui, Player player, int moved, string done, string nothing)
        {
            if (moved > 0)
            {
                gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
            }
            player.Message(MessageHud.MessageType.Center, moved > 0
                ? Localization.instance.Localize(done, moved.ToString())
                : nothing);
        }

        /// <summary>
        /// Moves what the backpack may part with into the chest, stacks first and free slots
        /// after, and returns how many units went. With <paramref name="onlyExisting"/> only
        /// items the chest already holds or has been marked for go, the game's own Stack all
        /// rule plus the chest's favourites. Same add-and-remove dance as quick stacking: the
        /// game's add either takes the whole stack, or merges what fits and leaves the smaller
        /// stack ours.
        /// </summary>
        private int MoveToChest(Player player, Container container, bool onlyExisting)
        {
            Inventory backpack = player.GetInventory();
            Inventory chest = container.GetInventory();
            string marks = ChestFavorites.Marks(container);
            int moved = 0;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(backpack.GetAllItems()))
            {
                if (!Stashable(player, item))
                {
                    continue;
                }
                string name = item.m_shared.m_name;
                if (onlyExisting && !chest.ContainsItemByName(name) && !ChestFavorites.Marked(marks, name))
                {
                    continue;
                }
                // AddItem logs an error, rather than declining, when nothing fits.
                if (!chest.HaveEmptySlot() && chest.FindFreeStackSpace(name, item.m_worldLevel) <= 0)
                {
                    continue;
                }
                int before = item.m_stack;
                if (chest.AddItem(item))
                {
                    backpack.RemoveItem(item);
                    moved += before;
                    continue;
                }
                int part = before - item.m_stack;
                if (part > 0)
                {
                    backpack.Changed();
                    moved += part;
                }
            }
            return moved;
        }

        /// <summary>What may leave the backpack: not worn, not a favourite, and not on the hotbar unless allowed.</summary>
        private bool Stashable(Player player, ItemDrop.ItemData item)
        {
            if (item.m_equipped || player.IsItemEquiped(item) || QuickStack.IsFavorite(item))
            {
                return false;
            }
            return includeHotbar.Value || item.m_gridPos.y != 0;
        }

        // ---- The buttons -----------------------------------------------------------------------

        /// <summary>
        /// Builds the buttons the first time the chest panel shows with the tweak on, then keeps
        /// them placed every frame: fill your stacks in the column beside the inventory panel
        /// (PanelButtons.LayoutInventoryColumn, shared with InventoryButtons), the other four in
        /// the column beside the chest panel, from its top down. The game's two buttons are hidden while
        /// ours are up and shown again when they are not.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
        private static class Panel
        {
            private static readonly List<Button> all = new List<Button>();
            private static readonly List<Button> chestColumn = new List<Button>();

            internal static bool VanillaHidden { get; private set; }

            private static void Postfix(InventoryGui __instance)
            {
                // The chest panel is still up while the screen fades out, so nothing moves until
                // it is gone - see PanelButtons.Closing.
                if (PanelButtons.Closing(__instance))
                {
                    return;
                }
                RectTransform panel = __instance.m_container;
                Container chest = __instance.m_currentContainer;
                bool show = Instance.On && chest != null && panel != null && panel.gameObject.activeSelf;
                if (!show)
                {
                    Hide(__instance);
                    PanelButtons.LayoutInventoryColumn(__instance);
                    return;
                }
                if (all.Count == 0 && !Create(__instance))
                {
                    return;
                }
                SetVanilla(__instance, false);
                foreach (Button button in all)
                {
                    button.gameObject.SetActive(true);
                }
                PanelButtons.LayoutChestColumn(__instance, chestColumn);
                PanelButtons.LayoutInventoryColumn(__instance);
            }

            /// <summary>
            /// Lets go of the buttons once the game has destroyed the screen they were on (see
            /// PanelButtons.ScreenDestroyed). The list is emptied so the next frame with a chest
            /// open makes them afresh on the new screen, whose vanilla buttons were never hidden.
            /// </summary>
            internal static void Forget()
            {
                all.Clear();
                chestColumn.Clear();
                VanillaHidden = false;
            }

            private static void Hide(InventoryGui gui)
            {
                foreach (Button button in all)
                {
                    if (button != null)
                    {
                        button.gameObject.SetActive(false);
                    }
                }
                SetVanilla(gui, true);
            }

            /// <summary>Only ever restores what it hid, so a button another mod hid stays hidden.</summary>
            private static void SetVanilla(InventoryGui gui, bool active)
            {
                if (active == !VanillaHidden)
                {
                    return;
                }
                VanillaHidden = !active;
                if (gui.m_takeAllButton != null)
                {
                    gui.m_takeAllButton.gameObject.SetActive(active);
                }
                if (gui.m_stackAllButton != null)
                {
                    gui.m_stackAllButton.gameObject.SetActive(active);
                }
            }

            private static bool Create(InventoryGui gui)
            {
                RectTransform inventory = gui.m_player;
                RectTransform chest = gui.m_container;
                if (inventory == null || chest == null)
                {
                    return false;
                }
                Button fillInventory = PanelButtons.Create(gui, inventory, "FillInventory", "fill_inventory",
                    "$omp_fill_inventory", "$omp_fill_inventory_tip", FillInventory);
                Button takeAll = PanelButtons.Create(gui, chest, "TakeAll", "take_all",
                    "$omp_take_all", "$omp_take_all_tip", TakeAll);
                Button placeAll = PanelButtons.Create(gui, chest, "PlaceAll", "place_all",
                    "$omp_place_all", "$omp_place_all_tip", PlaceAll);
                Button fillChest = PanelButtons.Create(gui, chest, "FillChest", "fill_chest",
                    "$omp_fill_chest", "$omp_fill_chest_tip", FillChest);
                Button sortChest = PanelButtons.Create(gui, chest, "Sort", "sort",
                    "$omp_sort_chest", "$omp_sort_chest_tip", SortChest);
                Button[] made = { fillInventory, takeAll, placeAll, fillChest, sortChest };
                foreach (Button button in made)
                {
                    if (button == null)
                    {
                        foreach (Button other in made)
                        {
                            if (other != null)
                            {
                                Object.Destroy(other.gameObject);
                            }
                        }
                        return false;
                    }
                }
                all.AddRange(made);
                // Ahead of InventoryButtons' two in the column: it stands where Stack nearby
                // does with no chest open, the one the chest replaces.
                PanelButtons.Enlist(fillInventory, 0);
                // Top to bottom beside the chest: the whole-chest moves, then the stack move, then sort.
                chestColumn.Add(takeAll);
                chestColumn.Add(placeAll);
                chestColumn.Add(fillChest);
                chestColumn.Add(sortChest);
                return true;
            }
        }
    }
}
