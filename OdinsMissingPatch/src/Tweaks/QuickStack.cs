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
    /// anything marked as a favourite: a modifier-click on an item in the inventory marks it with
    /// a golden frame, and quick stacking leaves it alone.
    ///
    /// A favourite is a flag on the stack itself, so it follows the stack into a chest and back
    /// and can be cleared there; splitting a favourite makes two.
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

        private void Stack(Player player)
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
                if (!Instance.On || !Hotkeys.Held(Instance.favoriteModifier.Value) || __instance.m_inventory == null)
                {
                    return true;
                }
                Vector2i pos = __instance.GetButtonPos(clickHandler.gameObject);
                ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(pos.x, pos.y);
                if (item == null)
                {
                    return true;
                }
                SetFavorite(item, !IsFavorite(item));
                // Saves the flag with the chest, or re-weighs the backpack; both harmless.
                __instance.m_inventory.Changed();
                return false;
            }
        }

        /// <summary>
        /// The golden frame: a copy of the slot's own "equipped" frame, tinted, drawn right
        /// above it and switched on for favourites after every grid refresh. Slots are rebuilt
        /// when an inventory changes size, so a frame whose slot is gone is simply made again.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        private static class ShowFavorites
        {
            private static readonly Dictionary<InventoryElement, Image> frames = new Dictionary<InventoryElement, Image>();

            private static void Postfix(InventoryGrid __instance)
            {
                Inventory inventory = __instance.m_inventory;
                if (inventory == null)
                {
                    return;
                }
                foreach (InventoryElement element in __instance.m_elements)
                {
                    if (frames.TryGetValue(element, out Image frame) && frame != null)
                    {
                        frame.enabled = false;
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
                    Image frame = element != null ? FrameFor(element) : null;
                    if (frame != null)
                    {
                        frame.enabled = true;
                    }
                }
            }

            private static Image FrameFor(InventoryElement element)
            {
                if (frames.TryGetValue(element, out Image frame) && frame != null)
                {
                    return frame;
                }
                Image template = element.m_equiped;
                if (template == null)
                {
                    return null;
                }
                GameObject go = Object.Instantiate(template.gameObject, template.transform.parent);
                go.name = "OMP_Favorite";
                go.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
                go.SetActive(true);
                frame = go.GetComponent<Image>();
                if (frame == null)
                {
                    Object.Destroy(go);
                    return null;
                }
                frame.color = Gold;
                frames[element] = frame;
                return frame;
            }
        }
    }
}
