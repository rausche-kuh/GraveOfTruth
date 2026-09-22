using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// The chests around a point, as the nearby-chest tweaks see them. Every Container that wakes
    /// up loaded is put on a list, and a query walks that list instead of the physics world. A
    /// chest is in reach when a player placed it, nobody has it open, the local player may open
    /// it (privacy setting, ward) and it has not been switched off with the button in its panel.
    /// Shared by NearbyCrafting, QuickStack and NearbyFuel; owns the switch-off button and the
    /// hover text line that goes with it, since the flag serves all three.
    ///
    /// It also owns the reach: while a tweak has opened it, the three inventory methods the
    /// game's own actions go through (count, have, remove by name) treat the chests around the
    /// player as part of the backpack, the backpack paying first. Everything outside an opened
    /// reach is vanilla, so an action a tweak has not named never touches a chest.
    /// </summary>
    internal static class NearbyChests
    {
        /// <summary>The per-chest opt-out, on the chest's ZDO so it persists and every client sees it.</summary>
        private static readonly int ExcludedHash = "OMP_NoNearbyUse".GetStableHashCode();

        private static readonly List<Entry> Registry = new List<Entry>();

        /// <summary>The list every Find returns, cleared and refilled each call.</summary>
        private static readonly List<Container> Found = new List<Container>();

        private sealed class Entry
        {
            public Container Container;
            public Piece Piece;
            public bool Tombstone;
        }

        internal static bool AnyTweakOn =>
            NearbyCrafting.Instance.On || QuickStack.Instance.On || NearbyFuel.Instance.On;

        /// <summary>
        /// Every chest in reach within <paramref name="radius"/> of <paramref name="origin"/>,
        /// nearest first. The list is reused by the next call, so copy it before doing anything
        /// that could query again. Empty without a local player, i.e. on a dedicated server.
        /// </summary>
        internal static List<Container> Find(Vector3 origin, float radius)
        {
            Found.Clear();
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return Found;
            }
            long playerId = player.GetPlayerID();
            float radiusSqr = radius * radius;
            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                Entry entry = Registry[i];
                if (entry.Container == null)
                {
                    Registry.RemoveAt(i);
                    continue;
                }
                if ((entry.Container.transform.position - origin).sqrMagnitude > radiusSqr)
                {
                    continue;
                }
                if (IsInReach(entry, playerId))
                {
                    Found.Add(entry.Container);
                }
            }
            Found.Sort((a, b) => (a.transform.position - origin).sqrMagnitude
                .CompareTo((b.transform.position - origin).sqrMagnitude));
            return Found;
        }

        /// <summary>
        /// The rule for "this chest may be used from afar". Checked on every query and again
        /// before every write, through <see cref="Claim"/>, so nothing is ever taken from a chest
        /// another player has opened since it was found.
        /// </summary>
        private static bool IsInReach(Entry entry, long playerId)
        {
            Container chest = entry.Container;
            if (entry.Tombstone || chest.m_inventory == null)
            {
                return false;
            }
            ZNetView nview = chest.m_nview;
            if (nview == null || !nview.IsValid())
            {
                return false;
            }
            if (entry.Piece == null || !entry.Piece.IsPlacedByPlayer())
            {
                return false;
            }
            if (nview.GetZDO().GetBool(ExcludedHash))
            {
                return false;
            }
            // The one chest the player has open is theirs to use: the panel edits and the
            // tweaks' writes go through the same Inventory object.
            InventoryGui gui = InventoryGui.instance;
            bool openByMe = gui != null && gui.m_currentContainer == chest;
            if (!openByMe)
            {
                if (chest.IsInUse() || nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1)
                {
                    return false;
                }
                if (chest.m_wagon != null && chest.m_wagon.InUse())
                {
                    return false;
                }
            }
            if (chest.m_privacy != Container.PrivacySetting.Public && chest.m_piece == null)
            {
                return false;
            }
            if (!chest.CheckAccess(playerId))
            {
                return false;
            }
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, flash: false))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Makes the local client the chest's owner so that a write to its inventory is saved and
        /// synced, after pulling the latest contents off the ZDO: a client that does not own a
        /// chest only refreshes its copy once a second, and saving a stale copy over someone
        /// else's change would undo it. False if the chest is no longer in reach.
        /// </summary>
        internal static bool Claim(Container chest)
        {
            if (chest == null)
            {
                return false;
            }
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            Entry entry = Registry.Find(e => e.Container == chest);
            if (entry == null || !IsInReach(entry, player.GetPlayerID()))
            {
                return false;
            }
            ZNetView nview = chest.m_nview;
            chest.Load();
            if (!nview.IsOwner())
            {
                nview.ClaimOwnership();
            }
            return nview.IsOwner();
        }

        internal static bool IsExcluded(Container chest)
        {
            ZNetView nview = chest != null ? chest.m_nview : null;
            return nview != null && nview.IsValid() && nview.GetZDO().GetBool(ExcludedHash);
        }

        /// <summary>Only while the chest is open in the panel, which is when the local client owns it.</summary>
        private static void SetExcluded(Container chest, bool excluded)
        {
            ZNetView nview = chest != null ? chest.m_nview : null;
            if (nview == null || !nview.IsValid())
            {
                return;
            }
            if (!nview.IsOwner())
            {
                nview.ClaimOwnership();
            }
            if (nview.IsOwner())
            {
                nview.GetZDO().Set(ExcludedHash, excluded);
            }
        }

        // ---- The reach: the backpack widened to the chests around the player ---------------

        private static int reachDepth;
        private static float reachRange;

        /// <summary>
        /// Opens the reach for the game action about to run, at <paramref name="range"/> metres
        /// around the player. Pair with <see cref="LeaveReach"/> in a finalizer, so it closes
        /// whether or not the action throws. Actions do not nest across tweaks, so the innermost
        /// range simply wins while one is open.
        /// </summary>
        internal static void EnterReach(float range)
        {
            reachDepth++;
            reachRange = range;
        }

        internal static void LeaveReach()
        {
            if (reachDepth > 0)
            {
                reachDepth--;
            }
        }

        private static bool InReach => reachDepth > 0;

        private static bool IsBackpack(Inventory inventory)
        {
            Player player = Player.m_localPlayer;
            return player != null && inventory == player.GetInventory();
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
        private static class CountChests
        {
            private static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
            {
                if (!InReach || name == null || !IsBackpack(__instance))
                {
                    return;
                }
                __result += InChests(name, quality, matchWorldLevel);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), new[] { typeof(string), typeof(bool) })]
        private static class HaveInChests
        {
            private static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
            {
                if (__result || !InReach || name == null || !IsBackpack(__instance))
                {
                    return;
                }
                __result = InChests(name, -1, matchWorldLevel) > 0;
            }
        }

        /// <summary>
        /// The backpack pays what it can and the original removes exactly that; the rest is taken
        /// from the chests here, nearest first. The chest inventories' own RemoveItem calls come
        /// back through this prefix and pass straight through, not being the backpack.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
        private static class TakeFromChests
        {
            private static void Prefix(Inventory __instance, string name, ref int amount, int itemQuality, bool worldLevelBased)
            {
                if (!InReach || name == null || !IsBackpack(__instance))
                {
                    return;
                }
                int carried = Carried(__instance, name, itemQuality, worldLevelBased);
                if (amount <= carried)
                {
                    return;
                }
                int missing = amount - carried;
                amount = carried;
                Player player = Player.m_localPlayer;
                List<Container> chests = new List<Container>(Find(player.transform.position, reachRange));
                foreach (Container chest in chests)
                {
                    if (missing <= 0)
                    {
                        break;
                    }
                    Inventory inventory = chest.GetInventory();
                    if (inventory.CountItems(name, itemQuality, worldLevelBased) <= 0 || !Claim(chest))
                    {
                        continue;
                    }
                    int take = Mathf.Min(missing, inventory.CountItems(name, itemQuality, worldLevelBased));
                    if (take <= 0)
                    {
                        continue;
                    }
                    inventory.RemoveItem(name, take, itemQuality, worldLevelBased);
                    missing -= take;
                }
                counts.Clear();
            }

            /// <summary>The backpack's own count, without going through the widened CountItems.</summary>
            private static int Carried(Inventory inventory, string name, int quality, bool matchWorldLevel)
            {
                int total = 0;
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (item.m_shared.m_name == name
                        && (quality < 0 || item.m_quality == quality)
                        && (!matchWorldLevel || item.m_worldLevel >= Game.m_worldLevel))
                    {
                        total += item.m_stack;
                    }
                }
                return total;
            }
        }

        /// <summary>
        /// The crafting panel counts every ingredient of every recipe, per quality level, each
        /// time it refreshes, and the same material many times over. Chest totals are kept for
        /// the rest of the frame and dropped after any spend.
        /// </summary>
        private static readonly Dictionary<CountKey, int> counts = new Dictionary<CountKey, int>();
        private static int countsFrame = -1;

        private static int InChests(string name, int quality, bool matchWorldLevel)
        {
            if (Time.frameCount != countsFrame)
            {
                counts.Clear();
                countsFrame = Time.frameCount;
            }
            CountKey key = new CountKey(name, quality, matchWorldLevel);
            if (counts.TryGetValue(key, out int total))
            {
                return total;
            }
            Player player = Player.m_localPlayer;
            foreach (Container chest in Find(player.transform.position, reachRange))
            {
                total += chest.GetInventory().CountItems(name, quality, matchWorldLevel);
            }
            counts[key] = total;
            return total;
        }

        private readonly struct CountKey : IEquatable<CountKey>
        {
            private readonly string name;
            private readonly int quality;
            private readonly bool matchWorldLevel;

            public CountKey(string name, int quality, bool matchWorldLevel)
            {
                this.name = name;
                this.quality = quality;
                this.matchWorldLevel = matchWorldLevel;
            }

            public bool Equals(CountKey other)
            {
                return quality == other.quality && matchWorldLevel == other.matchWorldLevel
                    && string.Equals(name, other.name, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is CountKey other && Equals(other);

            public override int GetHashCode()
            {
                int hash = name.GetHashCode();
                hash = hash * 397 ^ quality;
                return hash * 397 ^ (matchWorldLevel ? 1 : 0);
            }
        }

        // ---- The registry and the panel button ---------------------------------------------

        /// <summary>
        /// Container.Awake builds the inventory and registers the RPCs only when the object has a
        /// ZDO, so a prefab or a placement ghost never gets an inventory and is skipped here too.
        /// The Piece may sit on a parent (a ship's hold, a cart), so it is looked up once here
        /// rather than on every query.
        /// </summary>
        [HarmonyPatch(typeof(Container), "Awake")]
        private static class Register
        {
            private static void Postfix(Container __instance)
            {
                if (__instance.m_inventory == null)
                {
                    return;
                }
                Registry.Add(new Entry
                {
                    Container = __instance,
                    Piece = __instance.GetComponentInParent<Piece>(),
                    Tombstone = __instance.GetComponent<TombStone>() != null,
                });
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        private static class HoverText
        {
            private static void Postfix(Container __instance, ref string __result)
            {
                if (AnyTweakOn && IsExcluded(__instance))
                {
                    __result += "\n<color=#a0a0a0>Nearby use: off</color>";
                }
            }
        }

        /// <summary>
        /// The button in the chest panel that switches a chest out of (and back into) nearby
        /// use. It is a copy of the panel's own Take all button, placed to the left of the
        /// leftmost of Take all and Stack all so it inherits their look and layout, and it is
        /// only shown while at least one of the three tweaks is on.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
        private static class PanelButton
        {
            private static Button button;
            private static string label;

            private static void Postfix(InventoryGui __instance)
            {
                Container chest = __instance.m_currentContainer;
                bool show = AnyTweakOn && chest != null && __instance.m_container.gameObject.activeSelf;
                if (!show)
                {
                    if (button != null)
                    {
                        button.gameObject.SetActive(false);
                    }
                    return;
                }
                if (button == null && !Create(__instance))
                {
                    return;
                }
                button.gameObject.SetActive(true);
                SetLabel(IsExcluded(chest) ? "Nearby use: off" : "Nearby use: on");
            }

            private static bool Create(InventoryGui gui)
            {
                Button takeAll = gui.m_takeAllButton;
                if (takeAll == null)
                {
                    return false;
                }
                GameObject go = UnityEngine.Object.Instantiate(takeAll.gameObject, takeAll.transform.parent);
                go.name = "OMP_NearbyUse";
                button = go.GetComponent<Button>();
                if (button == null)
                {
                    UnityEngine.Object.Destroy(go);
                    return false;
                }
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(Toggle);

                RectTransform anchor = (RectTransform)takeAll.transform;
                if (gui.m_stackAllButton != null)
                {
                    RectTransform stackAll = (RectTransform)gui.m_stackAllButton.transform;
                    if (stackAll.anchoredPosition.x < anchor.anchoredPosition.x)
                    {
                        anchor = stackAll;
                    }
                }
                RectTransform rect = (RectTransform)go.transform;
                rect.anchoredPosition = anchor.anchoredPosition - new Vector2(anchor.rect.width + 8f, 0f);
                go.transform.SetSiblingIndex(anchor.GetSiblingIndex());

                UITooltip tooltip = go.GetComponent<UITooltip>();
                if (tooltip != null)
                {
                    tooltip.m_text = "Whether crafting, quick stacking and refuelling may reach into this chest";
                }
                label = null;
                return true;
            }

            /// <summary>
            /// The label is a TextMeshPro text, which is not among the staged reference
            /// assemblies, so it is set through the one property both it and a legacy Text have.
            /// </summary>
            private static void SetLabel(string text)
            {
                if (text == label)
                {
                    return;
                }
                label = text;
                foreach (Component component in button.GetComponentsInChildren<Component>(true))
                {
                    var property = component.GetType().GetProperty("text", typeof(string));
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(component, text, null);
                    }
                }
            }

            private static void Toggle()
            {
                InventoryGui gui = InventoryGui.instance;
                Container chest = gui != null ? gui.m_currentContainer : null;
                if (chest != null)
                {
                    SetExcluded(chest, !IsExcluded(chest));
                }
            }
        }
    }
}
