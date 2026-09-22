using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Dying keeps your gear. Weapons, armour, ammunition, tools, the belt and the food you carry
    /// stay in your inventory, and stay equipped, so you respawn ready to fight your way back;
    /// only the loot of the run - materials, trophies, fish, coins - goes to the grave.
    ///
    /// The game's own lowest death penalty keeps the items you happen to have equipped, which
    /// leaves the spare arrows, the food and the backup weapon lying in the grave. This instead
    /// decides by item type, from a configurable list, whatever the world's death penalty says:
    /// on a world that deletes the grave's contents, the kept types survive that too.
    /// </summary>
    internal sealed class KeepGearOnDeath : Tweak
    {
        internal static readonly KeepGearOnDeath Instance = new KeepGearOnDeath();

        private KeepGearOnDeath() { }

        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource(OdinsMissingPatchPlugin.NAME);

        private const string DefaultKeepTypes =
            "OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Bow, Shield, Torch, Tool, " +
            "Helmet, Chest, Legs, Shoulder, Hands, Utility, Trinket, Ammo, AmmoNonEquipable, " +
            "Consumable";

        private ConfigEntry<string> keepTypes;

        // Parsed from keepTypes; re-read whenever the setting changes, since parsing a comma
        // separated list on every item of every death would be silly.
        private HashSet<ItemDrop.ItemData.ItemType> kept = new HashSet<ItemDrop.ItemData.ItemType>();

        // The player whose tombstone is being filled right now, so the unequip patch knows that
        // the unequip it is about to see is the one that happens on death and not, say, a swap.
        private static Player dying;

        internal override string Section => "Keep Gear On Death";

        protected override string Summary =>
            "Dying keeps the item types listed in KeepTypes - weapons, armour, ammunition, tools " +
            "and food by default - in your inventory and equipped. Only the rest goes to the grave.";

        protected override void Bind(ConfigFile config)
        {
            string types = string.Join(", ", Enum.GetNames(typeof(ItemDrop.ItemData.ItemType))
                .Where(name => name != nameof(ItemDrop.ItemData.ItemType.None)).ToArray());
            keepTypes = config.Bind(Section, "KeepTypes", DefaultKeepTypes,
                "Comma separated item types that stay with you when you die. Everything else " +
                "goes to the grave. The types the game knows: " + types + ".");
            keepTypes.SettingChanged += (sender, args) => Parse();
            Parse();
        }

        private void Parse()
        {
            var parsed = new HashSet<ItemDrop.ItemData.ItemType>();
            foreach (string raw in keepTypes.Value.Split(','))
            {
                string name = raw.Trim();
                if (name.Length == 0)
                {
                    continue;
                }
                try
                {
                    parsed.Add((ItemDrop.ItemData.ItemType)Enum.Parse(typeof(ItemDrop.ItemData.ItemType), name, ignoreCase: true));
                }
                catch (ArgumentException)
                {
                    Log.LogWarning(Section + ": '" + name + "' is not an item type and is ignored");
                }
            }
            kept = parsed;
        }

        /// <summary>Whether this item stays with the player, by type. Quest items always do.</summary>
        private bool Keeps(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null &&
                (item.m_shared.m_questItem || kept.Contains(item.m_shared.m_itemType));
        }

        /// <summary>
        /// The game's own grave filter - not a quest item, not equipped - with the kept types
        /// taken out. Items that were equipped when the player died are only still equipped
        /// because the world keeps equipment or because the unequip patch below kept them so.
        /// </summary>
        private bool Drops(ItemDrop.ItemData item)
        {
            return item != null && !item.m_equipped && !Keeps(item);
        }

        /// <summary>
        /// The death path unequips everything before it fills the grave, so anything that stays
        /// in the inventory comes back unequipped on respawn - unless it is still equipped, which
        /// is how the game's own keep-equipment penalty sends you back into the fight dressed.
        /// While the local player's tombstone is being made, unequipping a kept item is skipped,
        /// so it goes through death equipped and is put back on by the respawn's load.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        private static class DeathScope
        {
            private static void Prefix(Player __instance)
            {
                dying = Instance.On ? __instance : null;
            }

            private static void Postfix()
            {
                dying = null;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem), typeof(ItemDrop.ItemData), typeof(bool))]
        private static class KeepEquipped
        {
            private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                if (dying == null || __instance != dying || !Instance.On)
                {
                    return true;
                }
                return !Instance.Keeps(item);
            }
        }

        /// <summary>
        /// The move into the grave: the game's loop, with the kept types left where they are.
        /// Its only caller is the death path.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveInventoryToGrave))]
        private static class FillGrave
        {
            private static bool Prefix(Inventory __instance, Inventory original)
            {
                if (!Instance.On || original == null)
                {
                    return true;
                }
                __instance.m_inventory.Clear();
                __instance.m_width = original.m_width;
                __instance.m_height = original.m_height;
                foreach (ItemDrop.ItemData item in original.m_inventory)
                {
                    if (Instance.Drops(item))
                    {
                        __instance.m_inventory.Add(item);
                    }
                }
                original.m_inventory.RemoveAll(Instance.Drops);
                original.Changed();
                __instance.Changed();
                return false;
            }
        }

        /// <summary>
        /// The death penalties that delete instead of dropping call this before the grave is
        /// filled, with the same filter. The kept types survive that too, so the tweak means the
        /// same thing on every world; its only caller is the death path.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveUnequipped))]
        private static class DeleteUnkept
        {
            private static bool Prefix(Inventory __instance)
            {
                if (!Instance.On)
                {
                    return true;
                }
                __instance.m_inventory.RemoveAll(Instance.Drops);
                __instance.Changed();
                return false;
            }
        }
    }
}
