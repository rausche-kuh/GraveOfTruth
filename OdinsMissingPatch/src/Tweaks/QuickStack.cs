using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// One key stacks your inventory away into the chests around you: every stack you carry goes
    /// to the nearest chest that already holds that item, topping up its stacks before taking a
    /// free slot. Each chest that took something pulses and shows how many it took; a message
    /// sums it up. Equipped items and the hotbar stay (the hotbar is a switch), and so does
    /// anything marked as a favourite: a modifier-click on an item in the inventory draws a golden
    /// border around it, and quick stacking leaves it alone.
    ///
    /// A favourite is a flag on the stack itself and only means anything in your own backpack:
    /// it can only be set there, and a stack that leaves it — into a chest, into the grave your
    /// death fills, onto the ground — loses the mark. Splitting a favourite makes two.
    /// </summary>
    internal sealed class QuickStack : Tweak
    {
        internal static readonly QuickStack Instance = new QuickStack();

        private QuickStack() { }

        private const string FavoriteKey = "OMP_Favorite";
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f, 1f);

        private ConfigEntry<KeyboardShortcut> hotkey;
        private ConfigEntry<KeyboardShortcut> favoriteModifier;
        private ConfigEntry<float> range;
        private ConfigEntry<bool> includeHotbar;

        internal override string Section => "Quick Stack";

        protected override string Summary =>
            "A hotkey stacks your inventory away into the chests around you that already hold " +
            "each item. Equipped items, favourites and (unless switched on below) the hotbar stay.";

        protected override void Bind(ConfigFile config)
        {
            hotkey = config.Bind(Section, "Hotkey", new KeyboardShortcut(KeyCode.Period),
                "The key that stacks your inventory into the chests around you. Works in the " +
                "world and with the inventory open.");
            favoriteModifier = config.Bind(Section, "FavoriteModifier", new KeyboardShortcut(KeyCode.LeftAlt),
                "Held while clicking an item in the inventory to mark it as a favourite, or to " +
                "clear the mark. Favourites are never quick stacked.");
            range = config.Bind(Section, "Range", 20f, new ConfigDescription(
                "How far from you a chest may stand, in metres, to be stacked into.",
                new AcceptableValueRange<float>(1f, 100f)));
            includeHotbar = config.Bind(Section, "IncludeHotbar", false,
                "Whether the hotbar row is stacked away too. Off keeps your tools, food and arrows.");
        }

        // ---- The hotkey ----------------------------------------------------------------------

        [HarmonyPatch(typeof(Player), "Update")]
        private static class Hotkey
        {
            private static void Postfix(Player __instance)
            {
                if (!Instance.On || __instance != Player.m_localPlayer || !CanTakeHotkey(__instance))
                {
                    return;
                }
                if (Hotkeys.Pressed(Instance.hotkey.Value))
                {
                    Instance.Stack(__instance);
                }
            }

            /// <summary>
            /// The player's own input gate, plus the one screen it refuses that the key should
            /// still work through: the inventory. Anything that takes typed text still blocks.
            /// </summary>
            private static bool CanTakeHotkey(Player player)
            {
                if (player.TakeInput())
                {
                    return true;
                }
                return InventoryGui.IsVisible()
                    && !(Chat.instance != null && Chat.instance.HasFocus())
                    && !Console.IsVisible() && !TextInput.IsVisible() && !Menu.IsVisible()
                    && !UnifiedPopup.IsVisible()
                    && !player.IsDead() && !player.IsTeleporting();
            }
        }

        /// <summary>The whole action, for the hotkey and for the inventory panel's Stack nearby button.</summary>
        internal void Stack(Player player)
        {
            Inventory backpack = player.GetInventory();
            List<Container> chests = new List<Container>(NearbyChests.Find(player.transform.position, range.Value));
            if (chests.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, "No chest in reach");
                return;
            }
            Dictionary<Container, int> stashed = new Dictionary<Container, int>();
            int moved = 0;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(backpack.GetAllItems()))
            {
                if (item.m_equipped || player.IsItemEquiped(item) || IsFavorite(item))
                {
                    continue;
                }
                if (!includeHotbar.Value && item.m_gridPos.y == 0)
                {
                    continue;
                }
                foreach (Container chest in chests)
                {
                    if (item.m_stack <= 0)
                    {
                        break;
                    }
                    Inventory inventory = chest.GetInventory();
                    string name = item.m_shared.m_name;
                    if (!inventory.ContainsItemByName(name) || !NearbyChests.Claim(chest) || !inventory.ContainsItemByName(name))
                    {
                        continue;
                    }
                    // AddItem logs an error, rather than declining, when nothing fits.
                    if (!inventory.HaveEmptySlot() && inventory.FindFreeStackSpace(name, item.m_worldLevel) <= 0)
                    {
                        continue;
                    }
                    int before = item.m_stack;
                    if (inventory.AddItem(item))
                    {
                        // Everything went: merged into the chest's stacks, or the stack itself
                        // now sits in a free slot there. Either way it leaves the backpack.
                        backpack.RemoveItem(item);
                        moved += before;
                        Add(stashed, chest, before);
                        break;
                    }
                    // Part went into the chest's stacks and the item is still ours, smaller.
                    int part = before - item.m_stack;
                    if (part > 0)
                    {
                        backpack.Changed();
                        moved += part;
                        Add(stashed, chest, part);
                    }
                }
            }
            foreach (KeyValuePair<Container, int> pair in stashed)
            {
                ChestGlow.Flash(pair.Key, "+" + pair.Value);
                InventoryGui gui = InventoryGui.instance;
                if (gui != null)
                {
                    gui.m_moveItemEffects.Create(pair.Key.transform.position, Quaternion.identity);
                }
            }
            player.Message(MessageHud.MessageType.Center, moved > 0
                ? "Stacked " + moved + " into " + stashed.Count + (stashed.Count == 1 ? " chest" : " chests")
                : "Nothing to stack away");
        }

        private static void Add(Dictionary<Container, int> stashed, Container chest, int count)
        {
            stashed.TryGetValue(chest, out int total);
            stashed[chest] = total + count;
        }

        // ---- Favourites ----------------------------------------------------------------------

        internal static bool IsFavorite(ItemDrop.ItemData item)
        {
            return item.m_customData.TryGetValue(FavoriteKey, out string value) && value == "1";
        }

        private static void SetFavorite(ItemDrop.ItemData item, bool favorite)
        {
            if (favorite)
            {
                item.m_customData[FavoriteKey] = "1";
            }
            else
            {
                item.m_customData.Remove(FavoriteKey);
            }
        }

        /// <summary>
        /// A click with the modifier held toggles the favourite instead of picking the item up.
        /// The grid raises its select callback from here, so returning false is the whole veto.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), "OnLeftDown")]
        private static class ToggleFavorite
        {
            private static bool Prefix(InventoryGrid __instance, UIInputHandler clickHandler)
            {
                Player player = Player.m_localPlayer;
                if (!Instance.On || player == null || __instance.m_inventory == null
                    || !Hotkeys.Held(Instance.favoriteModifier.Value))
                {
                    return true;
                }
                Vector2i pos = __instance.GetButtonPos(clickHandler.gameObject);
                ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(pos.x, pos.y);
                if (item == null)
                {
                    return true;
                }
                if (__instance.m_inventory != player.GetInventory())
                {
                    // A mark set anywhere else is stripped again the moment that inventory
                    // changes, so refuse the click and say why instead of doing nothing.
                    player.Message(MessageHud.MessageType.Center, "Favourites only in your inventory");
                    return false;
                }
                SetFavorite(item, !IsFavorite(item));
                // Re-weighs the backpack; harmless.
                __instance.m_inventory.Changed();
                return false;
            }
        }

        /// <summary>
        /// Every way out of the backpack but one ends in another inventory, and every inventory
        /// raises Changed once it has taken an item — a drag, Place all, a quick stack, the grave
        /// a death fills all pass through here, before a container saves itself — so that is the
        /// one place the mark has to come off. While there is no local player their own inventory
        /// is the one being loaded, so nothing is touched then.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), "Changed")]
        private static class ClearOutsideBackpack
        {
            private static void Prefix(Inventory __instance)
            {
                Player player = Player.m_localPlayer;
                if (!Instance.On || player == null || __instance == player.GetInventory())
                {
                    return;
                }
                foreach (ItemDrop.ItemData item in __instance.GetAllItems())
                {
                    if (IsFavorite(item))
                    {
                        SetFavorite(item, false);
                    }
                }
            }
        }

        /// <summary>
        /// The one way out no inventory sees: a dropped stack is a clone of its own, living in
        /// its own ZDO. The drop has already saved it by the time this runs, so clearing the
        /// mark needs a second save.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), "DropItem")]
        private static class ClearOnDrop
        {
            private static void Postfix(ItemDrop __result)
            {
                if (!Instance.On || __result == null || __result.m_itemData == null
                    || !IsFavorite(__result.m_itemData))
                {
                    return;
                }
                SetFavorite(__result.m_itemData, false);
                __result.Save();
            }
        }

        /// <summary>
        /// The golden border: four thin gold bars along the edges of the slot's own "equipped"
        /// frame, switched on for favourites after every grid refresh. A border rather than a
        /// filled frame leaves the game's own equipped highlight visible underneath, so an
        /// equipped favourite still reads as equipped. Slots are rebuilt when an inventory
        /// changes size, so a border whose slot is gone is simply made again.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        private static class ShowFavorites
        {
            /// <summary>How thick each bar is, in the canvas' units — a slot is about seventy.</summary>
            private const float Thickness = 3f;

            private static readonly Dictionary<InventoryElement, GameObject> borders =
                new Dictionary<InventoryElement, GameObject>();

            private static void Postfix(InventoryGrid __instance)
            {
                Inventory inventory = __instance.m_inventory;
                if (inventory == null)
                {
                    return;
                }
                foreach (InventoryElement element in __instance.m_elements)
                {
                    if (borders.TryGetValue(element, out GameObject border) && border != null)
                    {
                        border.SetActive(false);
                    }
                }
                if (!Instance.On)
                {
                    return;
                }
                int width = inventory.GetWidth();
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (!IsFavorite(item))
                    {
                        continue;
                    }
                    InventoryElement element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, width);
                    GameObject border = element != null ? BorderFor(element) : null;
                    if (border != null)
                    {
                        border.SetActive(true);
                    }
                }
            }

            private static GameObject BorderFor(InventoryElement element)
            {
                if (borders.TryGetValue(element, out GameObject border) && border != null)
                {
                    return border;
                }
                Image template = element.m_equiped;
                if (template == null)
                {
                    return null;
                }
                RectTransform source = template.rectTransform;
                border = new GameObject("OMP_Favorite", typeof(RectTransform));
                border.layer = source.gameObject.layer;
                RectTransform rect = (RectTransform)border.transform;
                rect.SetParent(source.parent, false);
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.anchoredPosition = source.anchoredPosition;
                rect.sizeDelta = source.sizeDelta;
                rect.localScale = source.localScale;
                rect.localRotation = source.localRotation;
                rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
                Bar(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, Thickness));
                Bar(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, Thickness));
                Bar(rect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(Thickness, 0f));
                Bar(rect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(Thickness, 0f));
                borders[element] = border;
                return border;
            }

            /// <summary>
            /// One edge: an image stretched along the side its two anchors share and sized across
            /// it, pivoted onto that side so it sits inside the frame.
            /// </summary>
            private static void Bar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
            {
                GameObject bar = new GameObject("Bar", typeof(RectTransform));
                bar.layer = parent.gameObject.layer;
                RectTransform rect = (RectTransform)bar.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = (anchorMin + anchorMax) * 0.5f;
                rect.sizeDelta = size;
                rect.anchoredPosition = Vector2.zero;
                Image image = bar.AddComponent<Image>();
                image.color = Gold;
                image.raycastTarget = false;
            }
        }
    }
}
