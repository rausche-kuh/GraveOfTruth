using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Two icon buttons in a column beside the inventory panel, between the armour box and
    /// the weight box: stack nearby, which is quick stacking by click (shown while Quick Stack is on, since
    /// it is that tweak's range and rules, and only while no chest is open, when ChestButtons'
    /// fill your stacks takes its place), and sort, which merges and sorts the backpack below
    /// the hotbar. Equipped items and favourites keep their slot; everything else flows around them.
    /// </summary>
    internal sealed class InventoryButtons : Tweak
    {
        internal static readonly InventoryButtons Instance = new InventoryButtons();

        private InventoryButtons() { }

        /// <summary>The inventory screen was destroyed; its buttons went with it.</summary>
        internal static void Forget() => Panel.Forget();

        private ConfigEntry<bool> sortHotbar;

        internal override string Section => "Inventory Buttons";

        protected override string Summary =>
            "Two icon buttons beside the inventory panel: stack nearby (quick stacking by click, " +
            "while Quick Stack is on and no chest is open) and sort.";

        protected override void Bind(ConfigFile config)
        {
            sortHotbar = config.Bind(Section, "SortHotbar", false,
                "Whether Sort also sorts the hotbar row. Off leaves your tools, food and arrows " +
                "where you put them.");
        }

        // ---- The actions ---------------------------------------------------------------------

        private static void StackNearby()
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (gui == null || player == null || player.IsTeleporting() || !QuickStack.Instance.On)
            {
                return;
            }
            gui.SetupDragItem(null, null, 1);
            QuickStack.Instance.Stack(player);
        }

        private static void SortInventory()
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (gui == null || player == null || player.IsTeleporting())
            {
                return;
            }
            gui.SetupDragItem(null, null, 1);
            InventorySorter.Sort(player.GetInventory(), Instance.sortHotbar.Value ? 0 : 1,
                item => item.m_equipped || player.IsItemEquiped(item) || QuickStack.IsFavorite(item));
        }

        // ---- The buttons -----------------------------------------------------------------------

        /// <summary>
        /// Runs every frame the inventory screen refreshes the backpack grid: builds the two
        /// buttons once, shows what applies and has the shared column beside the panel laid
        /// out (PanelButtons.LayoutInventoryColumn, where ChestButtons' fill your stacks comes
        /// first).
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "UpdateInventory", new[] { typeof(Player) })]
        private static class Panel
        {
            private static Button stackNearby;
            private static Button sort;

            /// <summary>
            /// Lets go of the buttons once the game has destroyed the screen they were on (see
            /// PanelButtons.ScreenDestroyed), so the next frame makes them afresh on the new one.
            /// </summary>
            internal static void Forget()
            {
                stackNearby = null;
                sort = null;
            }

            private static void Postfix(InventoryGui __instance)
            {
                // The chest is already gone on the frame the screen starts fading, so stack
                // nearby would come back into a column still standing beside an open chest
                // panel - see PanelButtons.Closing.
                if (PanelButtons.Closing(__instance))
                {
                    return;
                }
                if (!Instance.On)
                {
                    Show(__instance, false);
                }
                else if (sort != null || Create(__instance))
                {
                    Show(__instance, true);
                }
                PanelButtons.LayoutInventoryColumn(__instance);
            }

            private static void Show(InventoryGui gui, bool on)
            {
                if (stackNearby != null)
                {
                    stackNearby.gameObject.SetActive(on && QuickStack.Instance.On && gui.m_currentContainer == null);
                }
                if (sort != null)
                {
                    sort.gameObject.SetActive(on);
                }
            }

            private static bool Create(InventoryGui gui)
            {
                RectTransform panel = gui.m_player;
                if (panel == null)
                {
                    return false;
                }
                stackNearby = PanelButtons.Create(gui, panel, "StackNearby", "stack_nearby",
                    "$omp_stack_nearby", "$omp_stack_nearby_tip", StackNearby);
                sort = PanelButtons.Create(gui, panel, "SortInventory", "sort",
                    "$omp_sort", "$omp_sort_tip", SortInventory);
                if (stackNearby == null || sort == null)
                {
                    if (stackNearby != null)
                    {
                        Object.Destroy(stackNearby.gameObject);
                    }
                    if (sort != null)
                    {
                        Object.Destroy(sort.gameObject);
                    }
                    stackNearby = null;
                    sort = null;
                    return false;
                }
                PanelButtons.Enlist(stackNearby, 10);
                PanelButtons.Enlist(sort, 11);
                return true;
            }
        }
    }
}
