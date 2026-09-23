using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// The chests around a point, as the nearby-chest tweaks see them. Every Container that wakes
    /// up loaded is put on a list, and a query walks that list instead of the physics world. A
    /// chest is in reach when a player placed it, nobody has it open, the local player may open
    /// it (privacy setting, ward) and it has not been switched off with the button in its panel.
    /// Shared by NearbyCrafting, QuickStack, NearbyFuel and AddAll; owns the switch-off button
    /// and the hover text line that goes with it, since the flag serves all four, and beside it
    /// the button that clears a chest's favourites (<see cref="ChestFavorites"/>), which is the
    /// other thing the chest panel says about a chest rather than about what is in it.
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
            NearbyCrafting.Instance.On || QuickStack.Instance.On || NearbyFuel.Instance.On
            || AddAll.Instance.On;

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
            if (!Eligible(entry) || chest.m_inventory == null)
            {
                return false;
            }
            ZNetView nview = chest.m_nview;
            if (nview == null || !nview.IsValid())
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

        /// <summary>
        /// A chest the nearby tweaks could ever use: one a player placed, not a grave. Found
        /// chests (a dungeon's, a village's) and tombstones never are, so the switch stays off
        /// their panel.
        /// </summary>
        private static bool Eligible(Container chest)
        {
            return Eligible(Registry.Find(e => e.Container == chest));
        }

        private static bool Eligible(Entry entry)
        {
            return entry != null && !entry.Tombstone
                && entry.Piece != null && entry.Piece.IsPlacedByPlayer();
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

        /// <summary>One entry per open reach, its range; the innermost, i.e. the last, is in force.</summary>
        private static readonly List<float> Reaches = new List<float>();

        /// <summary>
        /// Opens the reach for the game action about to run, at <paramref name="range"/> metres
        /// around the player. Pair with <see cref="LeaveReach"/> in a finalizer, so it closes
        /// whether or not the action throws. Reaches do nest - Add all draws up its plan inside
        /// the scope NearbyFuel opened around the same Use - so the innermost range wins while it
        /// is open and the one around it is back in force once it closes.
        /// </summary>
        internal static void EnterReach(float range)
        {
            Reaches.Add(range);
        }

        internal static void LeaveReach()
        {
            if (Reaches.Count > 0)
            {
                Reaches.RemoveAt(Reaches.Count - 1);
            }
        }

        private static bool InReach => Reaches.Count > 0;

        private static float ReachRange => Reaches[Reaches.Count - 1];

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
                List<Container> chests = new List<Container>(Find(player.transform.position, ReachRange));
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
        }

        /// <summary>
        /// What an inventory itself holds of an item, without going through the widened
        /// CountItems - the backpack's own share of a reach.
        /// </summary>
        internal static int Carried(Inventory inventory, string name, int quality, bool matchWorldLevel)
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
            foreach (Container chest in Find(player.transform.position, ReachRange))
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
        /// The two text buttons in the chest panel: the switch that takes a chest out of (and
        /// back into) nearby use, and, while the chest has any, the one that clears the
        /// favourites it was marked with. Both are copies of the panel's own Take all button, so
        /// they inherit the look, and both are widened to their label plus a margin either side,
        /// since the labels outgrow Take all's.
        ///
        /// While ChestButtons has the game's own two hidden they stand in their spots, the top
        /// left and the top right of the panel - the only free width that band has, the chest's
        /// name being centred between them. Otherwise the band is full (Take all, the name,
        /// Stack all) and they go beside the panel from its top edge down, where ChestButtons'
        /// column would start. The switch shows on a chest a player placed while any of the four
        /// nearby tweaks is on, the
        /// clear button while the chest's favourites mean anything and the chest carries some.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
        private static class PanelButton
        {
            private static readonly TextButton NearbyUse = new TextButton("OMP_NearbyUse", Toggle);

            private static readonly TextButton ClearFavorites =
                new TextButton("OMP_ClearFavorites", ClearFavorited);

            /// <summary>The marks, and the language, the count and the tooltip below were written for.</summary>
            private static string favorites = "";
            private static int revision = -1;
            private static int favoritesCount;
            private static string favoritesTooltip = "";

            private static void Postfix(InventoryGui __instance)
            {
                // Leave both buttons where they are while the screen fades out, as ChestButtons
                // does - see PanelButtons.Closing.
                if (PanelButtons.Closing(__instance))
                {
                    return;
                }
                Container chest = __instance.m_currentContainer;
                bool panel = chest != null && __instance.m_container.gameObject.activeSelf;
                int slot = 0;
                if (panel && AnyTweakOn && Eligible(chest) && NearbyUse.Show(__instance))
                {
                    NearbyUse.SetLabel(IsExcluded(chest) ? "$omp_nearby_off" : "$omp_nearby_on");
                    NearbyUse.SetTooltip("$omp_nearby_topic", "$omp_nearby_tip");
                    // While the game's Take all is hidden its spot is free, and that is where the
                    // switch goes; the column slot is then still free for the other button.
                    Button spot = ChestButtons.HidesVanilla ? __instance.m_takeAllButton : null;
                    NearbyUse.Place(__instance, spot, keepLeft: true, slot: slot);
                    if (spot == null)
                    {
                        slot++;
                    }
                }
                else
                {
                    NearbyUse.Hide();
                }
                string marks = panel ? ChestFavorites.Marks(chest) : "";
                if (ChestFavorites.Used && marks.Length > 0 && ClearFavorites.Show(__instance))
                {
                    Describe(chest, marks);
                    ClearFavorites.SetLabel(
                        favoritesCount == 1 ? "$omp_clear_favourite" : "$omp_clear_favourites",
                        favoritesCount.ToString());
                    ClearFavorites.SetTooltip("$omp_favourites_topic", favoritesTooltip);
                    Button spot = ChestButtons.HidesVanilla ? __instance.m_stackAllButton : null;
                    ClearFavorites.Place(__instance, spot, keepLeft: false, slot: slot);
                }
                else
                {
                    ClearFavorites.Hide();
                }
            }

            /// <summary>
            /// Counts a chest's marks and writes the tooltip for them, and only when they have
            /// changed: this runs on every frame the panel is up, and naming the items means
            /// splitting the list and localizing each one. A language change counts as a change,
            /// since the names in the tooltip were translated when it was written.
            /// </summary>
            private static void Describe(Container chest, string marks)
            {
                if (marks == favorites && revision == Translations.Revision)
                {
                    return;
                }
                favorites = marks;
                revision = Translations.Revision;
                favoritesCount = ChestFavorites.Names(chest).Count;
                favoritesTooltip = "$omp_favourites_tip_head\n"
                    + ChestFavorites.Describe(chest)
                    + "\n\n$omp_favourites_tip_foot";
            }

            private static Container Open()
            {
                InventoryGui gui = InventoryGui.instance;
                return gui != null ? gui.m_currentContainer : null;
            }

            private static void Toggle()
            {
                Container chest = Open();
                if (chest != null)
                {
                    SetExcluded(chest, !IsExcluded(chest));
                }
            }

            private static void ClearFavorited()
            {
                Container chest = Open();
                if (chest == null)
                {
                    return;
                }
                ChestFavorites.Clear(chest);
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    player.Message(MessageHud.MessageType.Center, "$omp_favourites_cleared");
                }
            }
        }

        /// <summary>
        /// A copy of the chest panel's Take all button that keeps its label instead of trading it
        /// for an icon the way <see cref="PanelButtons"/> does: the game's skin, hover tint and
        /// click sound, with words of our own and a width cut to fit them. It is built the first
        /// time it is shown and kept from then on, and placed on every frame the panel refreshes,
        /// since where it belongs follows ChestButtons' switch and what it says follows the chest.
        /// </summary>
        private sealed class TextButton
        {
            /// <summary>The room between the label and each end of the button, in UI pixels.</summary>
            private const float Margin = 16f;

            private readonly string name;
            private readonly UnityAction onClick;

            private Button button;
            private UITooltip tip;
            private string label;
            private string tooltip;
            private Component labelText;
            private PropertyInfo labelWidth;

            internal TextButton(string name, UnityAction onClick)
            {
                this.name = name;
                this.onClick = onClick;
            }

            /// <summary>
            /// Shows the button, building it the first time. False when the panel has no Take all
            /// button to copy, in which case the caller leaves the panel alone.
            /// </summary>
            internal bool Show(InventoryGui gui)
            {
                if (button == null && !Create(gui))
                {
                    return false;
                }
                button.gameObject.SetActive(true);
                return true;
            }

            internal void Hide()
            {
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }

            /// <summary>
            /// Either in the spot of the vanilla button <paramref name="at"/>, keeping the edge
            /// that button is lined up on - its left for Take all, its right for Stack all -
            /// while the extra width grows the other way, or, without one, in the column beside
            /// the panel, <paramref name="slot"/> places down from its top edge.
            /// </summary>
            internal void Place(InventoryGui gui, Button at, bool keepLeft, int slot)
            {
                RectTransform takeAll = gui.m_takeAllButton != null
                    ? (RectTransform)gui.m_takeAllButton.transform : null;
                if (takeAll == null)
                {
                    return;
                }
                float height = takeAll.rect.height;
                float width = Mathf.Max(takeAll.rect.width, LabelWidth() + 2f * Margin);
                RectTransform rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(width, height);
                RectTransform spot = at != null ? (RectTransform)at.transform : null;
                if (spot == null)
                {
                    RectTransform panel = gui.m_container;
                    PanelButtons.Pin(rect, new Vector2(
                        PanelButtons.ColumnLeft(panel) + width * 0.5f,
                        PanelButtons.ColumnTop(panel) - height * 0.5f - slot * (height + PanelButtons.Gap)));
                    return;
                }
                rect.anchorMin = spot.anchorMin;
                rect.anchorMax = spot.anchorMax;
                rect.pivot = spot.pivot;
                float grown = width - spot.rect.width;
                rect.anchoredPosition = spot.anchoredPosition
                    + new Vector2(keepLeft ? grown * (1f - spot.pivot.x) : -grown * spot.pivot.x, 0f);
            }

            /// <summary>
            /// How wide the label wants to be for its current text, from the text's own
            /// preferred width (a TextMeshPro property, read by reflection since that assembly
            /// is not referenced). Zero when there is no label to ask.
            /// </summary>
            private float LabelWidth()
            {
                if (labelText == null)
                {
                    Transform text = button.transform.Find("Text");
                    if (text == null)
                    {
                        return 0f;
                    }
                    foreach (Component component in text.GetComponents<Component>())
                    {
                        PropertyInfo property = component.GetType().GetProperty("preferredWidth", typeof(float));
                        if (property != null && property.CanRead)
                        {
                            labelText = component;
                            labelWidth = property;
                            break;
                        }
                    }
                }
                return labelText != null ? (float)labelWidth.GetValue(labelText, null) : 0f;
            }

            private bool Create(InventoryGui gui)
            {
                Button takeAll = gui.m_takeAllButton;
                if (takeAll == null)
                {
                    return false;
                }
                GameObject go = UnityEngine.Object.Instantiate(takeAll.gameObject, takeAll.transform.parent);
                go.name = name;
                button = go.GetComponent<Button>();
                if (button == null)
                {
                    UnityEngine.Object.Destroy(go);
                    return false;
                }
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(onClick);
                go.transform.SetSiblingIndex(takeAll.transform.GetSiblingIndex());
                tip = PanelButtons.Tooltip(gui, go);
                label = null;
                tooltip = null;
                labelText = null;
                labelWidth = null;
                return true;
            }

            /// <summary>
            /// The label is a TextMeshPro text, which is not among the staged reference
            /// assemblies, so it is set through the one property both it and a legacy Text have.
            /// <paramref name="text"/> is a $omp_ token, which nothing else would translate -
            /// unlike a tooltip, a label on a component is shown exactly as it is written. It is
            /// translated on the way in and compared translated, so the caller may hand the same
            /// token over every frame and the label still follows a language change.
            /// </summary>
            internal void SetLabel(string text, params string[] words)
            {
                if (Localization.instance != null)
                {
                    text = Localization.instance.Localize(text, words);
                }
                if (text == label)
                {
                    return;
                }
                label = text;
                foreach (Component component in button.GetComponentsInChildren<Component>(true))
                {
                    PropertyInfo property = component.GetType().GetProperty("text", typeof(string));
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(component, text, null);
                    }
                }
            }

            /// <summary>
            /// The hover text. The copy comes without a `UITooltip`, so `PanelButtons.Tooltip`
            /// made one when the button was built; without it there is nothing to write to.
            /// </summary>
            internal void SetTooltip(string topic, string text)
            {
                if (text == tooltip || tip == null)
                {
                    return;
                }
                tooltip = text;
                tip.m_topic = topic;
                tip.m_text = text;
            }
        }
    }
}
