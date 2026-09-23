using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OdinsMissingPatch
{
    /// <summary>
    /// A cartography table shares by itself: walk up to one and your map goes onto it and its
    /// map onto yours, without touching it. That is the game's own write - read the table, merge,
    /// send - done silently: no message, no write effect, and never on a table behind a ward you
    /// may not use (that one is only read).
    /// <para>
    /// A table is synced when you arrive within SyncRange of it, again when a pin of yours
    /// changes while you are there, and again when someone else has written to it, but never
    /// more often than MinInterval: every write costs the same hitch as a manual one, since the
    /// game packs its whole exploration bitmap each time. Leaving the range forgets the table,
    /// so the next arrival syncs again.
    /// </para>
    /// </summary>
    internal sealed class SharedMapTable : Tweak
    {
        internal static readonly SharedMapTable Instance = new SharedMapTable();

        private SharedMapTable() { }

        /// <summary>How often the tables around the player are looked at.</summary>
        private const float CheckInterval = 1f;

        /// <summary>A table counts as left this far beyond SyncRange, so its edge does not flicker.</summary>
        private const float LeaveMargin = 8f;

        private ConfigEntry<float> syncRange;
        private ConfigEntry<float> minInterval;

        internal override string Section => "Shared Map Table";

        protected override string Summary =>
            "A cartography table shares by itself: come near one and your map is written onto it " +
            "and its map onto yours, without a click. Tables behind a ward you have no access to " +
            "are only read.";

        protected override void Bind(ConfigFile config)
        {
            syncRange = config.Bind(Section, "SyncRange", 64f, new ConfigDescription(
                "Metres from a table within which it syncs. 64 is one zone.",
                new AcceptableValueRange<float>(5f, 256f)));
            minInterval = config.Bind(Section, "MinInterval", 10f, new ConfigDescription(
                "Seconds between two syncs of the same table. Each sync is as heavy as writing " +
                "the table by hand, so a very low value can make the game stutter near a table.",
                new AcceptableValueRange<float>(1f, 600f)));
        }

        private sealed class TableState
        {
            /// <summary>Synced since the player last came into range.</summary>
            public bool UpToDate;
            public uint Revision;
            public float LastSync = float.NegativeInfinity;
            /// <summary>The pin generation the last sync wrote.</summary>
            public int Generation;
            /// <summary>What the last sync sent, to tell our own write from someone else's.</summary>
            public byte[] Sent;
        }

        private static readonly List<MapTable> Tables = new List<MapTable>();
        private static readonly ConditionalWeakTable<MapTable, TableState> States =
            new ConditionalWeakTable<MapTable, TableState>();

        /// <summary>Counts the saved pins added or removed, so a table knows it is behind.</summary>
        private static int generation;
        private static bool syncing;
        private static float nextCheck;

        private void Check(Player player)
        {
            Vector3 origin = player.transform.position;
            float range = syncRange.Value;
            for (int i = Tables.Count - 1; i >= 0; i--)
            {
                MapTable table = Tables[i];
                if (table == null)
                {
                    Tables.RemoveAt(i);
                    continue;
                }
                if (table.m_nview == null || !table.m_nview.IsValid())
                {
                    continue;
                }
                TableState state = States.GetOrCreateValue(table);
                float distance = Vector3.Distance(origin, table.transform.position);
                if (distance > range + LeaveMargin)
                {
                    state.UpToDate = false;
                    continue;
                }
                if (distance > range || Time.time - state.LastSync < minInterval.Value)
                {
                    continue;
                }
                bool canWrite = PrivateArea.CheckAccess(table.transform.position, 0f, flash: false);
                bool behind = canWrite && state.Generation != generation;
                if (state.UpToDate && !behind && !WrittenByOthers(table, state))
                {
                    continue;
                }
                Sync(table, player, state, canWrite);
                // One table per check: each sync is a hitch of its own.
                return;
            }
        }

        /// <summary>
        /// Whether the table changed since this player last synced it. Our own write changes the
        /// revision too, once it reaches the table's owner, so a revision change whose data is
        /// exactly what we sent is taken as the new baseline instead.
        /// </summary>
        private static bool WrittenByOthers(MapTable table, TableState state)
        {
            ZDO zdo = table.m_nview.GetZDO();
            if (zdo.DataRevision == state.Revision)
            {
                return false;
            }
            byte[] data = zdo.GetByteArray(ZDOVars.s_data);
            if (state.Sent != null && data != null && SameBytes(data, state.Sent))
            {
                state.Revision = zdo.DataRevision;
                return false;
            }
            return true;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// MapTable.OnWrite without the parts a player would notice: the read runs without its
        /// message, the ward is asked without flashing, and nothing is printed or played.
        /// </summary>
        private static void Sync(MapTable table, Player player, TableState state, bool canWrite)
        {
            ZDO zdo = table.m_nview.GetZDO();
            syncing = true;
            try
            {
                table.OnRead(null, player, null, showMessage: false);
                if (canWrite)
                {
                    byte[] current = zdo.GetByteArray(ZDOVars.s_data);
                    if (current != null)
                    {
                        current = Utils.Decompress(current);
                    }
                    ZPackage data = table.GetMapData(current);
                    state.Sent = data.GetArray();
                    table.m_nview.InvokeRPC("MapData", data);
                }
            }
            finally
            {
                syncing = false;
            }
            state.UpToDate = true;
            state.Revision = zdo.DataRevision;
            state.LastSync = Time.time;
            state.Generation = generation;
        }

        /// <summary>MapTable keeps no list of itself, so every one that starts is put on ours.</summary>
        [HarmonyPatch(typeof(MapTable), "Start")]
        private static class Register
        {
            private static void Postfix(MapTable __instance)
            {
                if (__instance.m_nview != null && __instance.m_nview.GetZDO() != null)
                {
                    Tables.Add(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        private static class Tick
        {
            private static void Postfix(Player __instance)
            {
                if (!Instance.On || __instance != Player.m_localPlayer || Minimap.instance == null
                    || Time.time < nextCheck)
                {
                    return;
                }
                nextCheck = Time.time + CheckInterval;
                Instance.Check(__instance);
            }
        }

        /// <summary>A saved pin was added: the tables in range are behind. Not for the pins a sync itself reads in.</summary>
        [HarmonyPatch(typeof(Minimap), nameof(Minimap.AddPin))]
        private static class PinAdded
        {
            private static void Postfix(bool save)
            {
                if (save && !syncing)
                {
                    generation++;
                }
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.RemovePin), typeof(Minimap.PinData))]
        private static class PinRemoved
        {
            private static void Postfix(Minimap.PinData pin)
            {
                if (pin != null && pin.m_save && !syncing)
                {
                    generation++;
                }
            }
        }
    }
}
