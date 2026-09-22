using System;
using System.Collections.Generic;
using System.Text;

namespace OdinsMissingPatch
{
    /// <summary>
    /// The kinds of item a chest is marked to take, whether or not it holds any right now. An
    /// Alt-click on an item in the open chest's grid marks its kind — the same modifier that
    /// marks a stack in the backpack, one gesture for both favourites — and quick stacking and
    /// Fill the chest then treat a marked chest as though it already held that item, so a chest
    /// emptied of its wood still draws wood back instead of losing the habit.
    ///
    /// Nothing is drawn on a slot for them - a marked kind the chest holds none of has no slot,
    /// so the list is shown whole, in the chest panel's Clear favourites button and its tooltip.
    ///
    /// The list is a string on the chest's ZDO, so it persists, survives the chest being emptied
    /// and is the same for every client. Item names are the shared name (a localization token,
    /// "$item_wood"), joined by newlines with one at each end, so a name is matched between two
    /// separators and no name can be a prefix of another.
    /// </summary>
    internal static class ChestFavorites
    {
        private static readonly int FavoritesHash = "OMP_ChestFavorites".GetStableHashCode();

        private const char Separator = '\n';

        /// <summary>Whether a chest's favourites mean anything right now: the two tweaks that read them.</summary>
        internal static bool Used => QuickStack.Instance.On || ChestButtons.Instance.On;

        /// <summary>
        /// The chest's marks as one string, for a caller that asks about many items at once (a
        /// grid refresh, a stack away); pass it to <see cref="Marked"/>. Empty when the chest is
        /// gone, unloaded or unmarked.
        /// </summary>
        internal static string Marks(Container chest)
        {
            ZNetView nview = chest != null ? chest.m_nview : null;
            return nview != null && nview.IsValid() ? nview.GetZDO().GetString(FavoritesHash, "") : "";
        }

        /// <summary>
        /// Whether <paramref name="marks"/> holds <paramref name="itemName"/>. A hit has to have
        /// a separator on either side, which every name in the string does, so Wood never matches
        /// inside WoodArrow.
        /// </summary>
        internal static bool Marked(string marks, string itemName)
        {
            if (string.IsNullOrEmpty(marks) || string.IsNullOrEmpty(itemName))
            {
                return false;
            }
            for (int at = marks.IndexOf(itemName, StringComparison.Ordinal); at >= 0;
                at = marks.IndexOf(itemName, at + 1, StringComparison.Ordinal))
            {
                // Every string this writes has a separator at each end, so a name always has
                // both neighbours; the bounds are asked anyway rather than trusting the ZDO.
                int after = at + itemName.Length;
                if (at > 0 && after < marks.Length
                    && marks[at - 1] == Separator && marks[after] == Separator)
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool Accepts(Container chest, string itemName)
        {
            return Marked(Marks(chest), itemName);
        }

        internal static List<string> Names(Container chest)
        {
            List<string> names = new List<string>();
            foreach (string name in Marks(chest).Split(Separator))
            {
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
            return names;
        }

        /// <summary>
        /// Marks or unmarks a kind of item, and says which it did. Only for the chest open in the
        /// panel, which is the one the local client owns.
        /// </summary>
        internal static bool Toggle(Container chest, string itemName)
        {
            List<string> names = Names(chest);
            bool marked = !names.Remove(itemName);
            if (marked)
            {
                names.Add(itemName);
            }
            Write(chest, names);
            return marked;
        }

        internal static void Clear(Container chest)
        {
            Write(chest, new List<string>());
        }

        /// <summary>
        /// The marked kinds as a readable list, one per line, for the button's tooltip - which is
        /// the only place they are shown, since a kind the chest holds none of has no slot.
        /// </summary>
        internal static string Describe(Container chest)
        {
            StringBuilder text = new StringBuilder();
            foreach (string name in Names(chest))
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append("- ").Append(Localize(name));
            }
            return text.ToString();
        }

        internal static string Localize(string itemName)
        {
            return Localization.instance != null ? Localization.instance.Localize(itemName) : itemName;
        }

        /// <summary>
        /// The chest whose inventory this is, when it is the one open in the panel; null for the
        /// backpack and for any other inventory.
        /// </summary>
        internal static Container OpenChest(Inventory inventory)
        {
            InventoryGui gui = InventoryGui.instance;
            Container chest = gui != null ? gui.m_currentContainer : null;
            return chest != null && inventory != null && chest.m_inventory == inventory ? chest : null;
        }

        private static void Write(Container chest, List<string> names)
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
            if (!nview.IsOwner())
            {
                return;
            }
            StringBuilder marks = new StringBuilder();
            foreach (string name in names)
            {
                marks.Append(Separator).Append(name);
            }
            if (marks.Length > 0)
            {
                marks.Append(Separator);
            }
            nview.GetZDO().Set(FavoritesHash, marks.ToString());
        }
    }
}
