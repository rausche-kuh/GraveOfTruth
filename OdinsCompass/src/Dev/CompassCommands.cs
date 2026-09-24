using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OdinsCompass
{
    // Dev only: src/Dev/ is compiled into Debug builds alone, so this never ships. Nothing else
    // refers to it - delete the file to drop the commands.
    public partial class OdinsCompassPlugin
    {
        private const string Usage =
            "compass locations [filter] | prefabs <filter> | items [filter] | psystems | find <location>";

        /// <summary>
        /// "compass ..." - the checks ROADMAP.md asks for before the features are written: which
        /// location names exist in this build, which world object prefabs, which items the item
        /// database holds (in the main menu too), which environment particle systems, and whether
        /// the server answers a vegvisir style find. Everything it
        /// prints also goes to the BepInEx log, which is where a long dump is meant to be read.
        /// Needs devcommands. On a dedicated server, "locations" is run in the server's console,
        /// because only the server holds the location list.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        public static class Commands
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("compass", Usage, args =>
                {
                    string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    string filter = args.Length > 2 ? args[2].ToLowerInvariant() : "";
                    switch (sub)
                    {
                        case "locations": Say(args.Context, Locations(filter)); break;
                        case "prefabs": Say(args.Context, Prefabs(filter)); break;
                        case "items": Say(args.Context, Items(filter)); break;
                        case "psystems": Say(args.Context, ParticleSystems()); break;
                        case "find":
                            if (filter.Length == 0 || Game.instance == null || Player.m_localPlayer == null)
                            {
                                Say(args.Context, Usage);
                                break;
                            }
                            // The vanilla vegvisir request: the server answers with a map pin named
                            // "compass" and turns the player toward it, so the answer is visible
                            // even before the mod intercepts it.
                            Game.instance.DiscoverClosestLocation(args[2], Player.m_localPlayer.transform.position,
                                "compass", (int)Minimap.PinType.Icon3, showMap: false);
                            Say(args.Context, "Asked the server for the nearest " + args[2] + " - watch the map for a 'compass' pin.");
                            break;
                        default: Say(args.Context, Usage); break;
                    }
                }, isCheat: true);
            }
        }

        /// <summary>Every location the zone system knows, with its biome, name filtered if given.</summary>
        private static string Locations(string filter)
        {
            if (ZoneSystem.instance == null)
            {
                return "No zone system - not in a world.";
            }
            StringBuilder sb = new StringBuilder();
            int count = 0;
            foreach (ZoneSystem.ZoneLocation location in ZoneSystem.instance.m_locations)
            {
                // SetupLocations copies the soft reference's name here, and reading the reference
                // itself would need SoftReferenceableAssets.dll, which lib/ does not stage.
                string name = location.m_prefabName;
                if (string.IsNullOrEmpty(name) || (filter.Length > 0 && !name.ToLowerInvariant().Contains(filter)))
                {
                    continue;
                }
                count++;
                sb.Append(name).Append("  [").Append(location.m_biome).Append("]")
                    .Append(location.m_enable ? "" : " (disabled)")
                    .Append(location.m_unique ? " unique" : "")
                    .Append(" x").Append(location.m_quantity).Append('\n');
            }
            sb.Append(count).Append(" locations");
            if (!ZNet.instance.IsServer())
            {
                sb.Append(" (definitions only - instances live on the server)");
            }
            return sb.ToString();
        }

        /// <summary>The networked prefabs whose name contains the filter - ore veins, pickables, totems.</summary>
        private static string Prefabs(string filter)
        {
            if (ZNetScene.instance == null || filter.Length == 0)
            {
                return "compass prefabs <filter> - needs a world and a filter.";
            }
            StringBuilder sb = new StringBuilder();
            int count = 0;
            foreach (string name in ZNetScene.instance.GetPrefabNames())
            {
                if (name.ToLowerInvariant().Contains(filter))
                {
                    count++;
                    sb.Append(name).Append('\n');
                }
            }
            sb.Append(count).Append(" prefabs containing '").Append(filter).Append("'");
            return sb.ToString();
        }

        /// <summary>
        /// The item database's prefabs whose name contains the filter, and how many it holds in
        /// all. Works in the main menu, whose database is a copy of the prefab's and may not hold
        /// every item - the compass clones the wishbone, so it needs to be there.
        /// </summary>
        private static string Items(string filter)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                return "No item database yet.";
            }
            StringBuilder sb = new StringBuilder();
            int count = 0;
            foreach (GameObject item in db.m_items)
            {
                if (item != null && (filter.Length == 0 || item.name.ToLowerInvariant().Contains(filter)))
                {
                    count++;
                    sb.Append(item.name).Append('\n');
                }
            }
            sb.Append(count).Append(" of ").Append(db.m_items.Count).Append(" items")
                .Append(filter.Length > 0 ? " contain '" + filter + "'" : "")
                .Append(ZNetScene.instance == null ? " (main menu database)" : " (world database)")
                .Append("; Wishbone ").Append(db.GetItemPrefab("Wishbone") != null ? "found" : "missing");
            return sb.ToString();
        }

        /// <summary>
        /// Every environment's particle systems, by name: where the wind streaks live, if the game
        /// has any - the candidates for the compass's guidance effect.
        /// </summary>
        private static string ParticleSystems()
        {
            if (EnvMan.instance == null)
            {
                return "No environment manager - not in a world.";
            }
            StringBuilder sb = new StringBuilder();
            foreach (EnvSetup env in EnvMan.instance.m_environments)
            {
                if (env.m_psystems == null || env.m_psystems.Length == 0)
                {
                    continue;
                }
                sb.Append(env.m_name).Append(": ");
                List<string> names = new List<string>();
                foreach (GameObject system in env.m_psystems)
                {
                    if (system != null)
                    {
                        names.Add(system.name);
                    }
                }
                sb.Append(string.Join(", ", names)).Append('\n');
            }
            return sb.ToString();
        }

        private static void Say(Terminal terminal, string text)
        {
            Debug.Log("[OdinsCompass] " + text);
            foreach (string line in text.Split('\n'))
            {
                terminal.AddString(line);
            }
        }
    }
}
