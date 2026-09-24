using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OdinsPaths
{
    // Dev only: src/Dev/ is compiled into Debug builds alone, so this never ships. Nothing else
    // refers to it - delete the file to drop the commands.
    public partial class OdinsPathsPlugin
    {
        private const string Usage = "paths facts | bench [cells] | lay <x> <z> | lay <location> | undo | clearpins";

        /// <summary>What the last "paths lay" replaced, zone by zone, for "paths undo".</summary>
        private static Dictionary<Vector2s, TerrainWriter.ZoneBackup> lastBackups;
        /// <summary>The landing barrels the last "paths lay" set, for "paths undo".</summary>
        private static List<ZDOID> lastLandings;

        /// <summary>The unsaved map pins the last lays dropped along their trails.</summary>
        private static readonly List<Minimap.PinData> trailPins = new List<Minimap.PinData>();

        private static bool laying;

        /// <summary>
        /// "paths ..." - the checks ROADMAP.md section 0 asks for (facts, bench), and "lay": a
        /// path from the player to a point or to the nearest location of a name, searched, shaped
        /// and written into the terrain exactly as the sleep trigger will do it, with unsaved map
        /// pins along it. "undo" puts back the terrain data the last lay replaced; "clearpins"
        /// removes the pins. Server only for lay and undo - the host of a local game is one.
        /// Everything it prints also goes to the BepInEx log. Needs devcommands.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        public static class Commands
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("paths", Usage, args =>
                {
                    string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    switch (sub)
                    {
                        case "facts": Say(args.Context, Facts()); break;
                        case "bench":
                            int cells = args.Length > 2 && int.TryParse(args[2], out int n) ? Mathf.Clamp(n, 10, 1000) : 200;
                            Say(args.Context, Bench(cells));
                            break;
                        case "lay": Lay(args); break;
                        case "undo": Say(args.Context, Undo()); break;
                        case "clearpins": Say(args.Context, ClearPins()); break;
                        default: Say(args.Context, Usage); break;
                    }
                }, isCheat: true);
            }
        }

        /// <summary>
        /// The zone heightmap's width and scale (the TerrainComp arrays are (width+1)^2), the
        /// terrain compiler's prefab name, the water level, and under the player: biome, generated
        /// versus actual height, the paint mask (r dirt, g cultivated - snow depth in the Deep
        /// North -, b paved, a vegetation) and the size of the zone's saved terrain data.
        /// </summary>
        private static string Facts()
        {
            if (ZoneSystem.instance == null || WorldGenerator.instance == null)
            {
                return "Not in a world.";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("server: ").Append(ZNet.instance != null && ZNet.instance.IsServer())
                .Append("  dedicated: ").Append(ZNet.instance != null && ZNet.instance.IsDedicated()).Append('\n');
            Heightmap prefabMap = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
            sb.Append("zone heightmap: width ").Append(prefabMap.m_width).Append(" scale ").Append(prefabMap.m_scale)
                .Append("  terrain compiler prefab: ")
                .Append(prefabMap.m_terrainCompilerPrefab != null ? prefabMap.m_terrainCompilerPrefab.name : "none").Append('\n');
            sb.Append("water level: ").Append(ZoneSystem.instance.m_waterLevel).Append('\n');

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return sb.Append("No local player - the rest needs one.").ToString();
            }
            Vector3 pos = player.transform.position;
            sb.Append("at ").Append(pos.ToString("F1")).Append("  zone ").Append(ZoneSystem.GetZone(pos))
                .Append("  biome ").Append(WorldGenerator.instance.GetBiome(pos))
                .Append("  deep north: ").Append(WorldGenerator.IsDeepnorth(pos.x, pos.z)).Append('\n');
            float generated = WorldGenerator.instance.GetHeight(pos.x, pos.z, out Color genMask);
            sb.Append("generated height ").Append(generated.ToString("F2"))
                .Append("  generated mask ").Append(genMask.ToString("F2")).Append('\n');
            Heightmap hmap = Heightmap.FindHeightmap(pos);
            if (hmap != null)
            {
                hmap.WorldToVertex(pos, out int x, out int y);
                sb.Append("heightmap vertex ").Append(x).Append(',').Append(y)
                    .Append("  height ").Append(hmap.GetHeight(x, y).ToString("F2"))
                    .Append("  paint ").Append(hmap.GetPaintMask(x, y).ToString("F2")).Append('\n');
            }
            TerrainComp comp = TerrainComp.FindTerrainCompiler(pos);
            if (comp == null)
            {
                sb.Append("no terrain compiler in this zone (never modified)");
            }
            else
            {
                byte[] data = comp.m_nview != null && comp.m_nview.IsValid()
                    ? comp.m_nview.GetZDO().GetByteArray(ZDOVars.s_TCData) : null;
                sb.Append("terrain compiler at ").Append(comp.transform.position.ToString("F1"))
                    .Append("  saved data ").Append(data != null ? data.Length + " bytes compressed" : "none")
                    .Append("  operations ").Append(comp.m_operations);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Times what a path search would do per cell: one generated height (biome lookup plus
        /// that biome's noise), on a cells x cells grid 4 m apart around the player - and, for
        /// comparison, the game's own exact heightmap build of one zone.
        /// </summary>
        private static string Bench(int cells)
        {
            if (WorldGenerator.instance == null || Player.m_localPlayer == null)
            {
                return "Needs a world and a local player.";
            }
            Vector3 center = Player.m_localPlayer.transform.position;
            WorldGenerator gen = WorldGenerator.instance;
            float half = cells * 2f;
            float sum = 0f;
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < cells; i++)
            {
                for (int j = 0; j < cells; j++)
                {
                    sum += gen.GetHeight(center.x - half + i * 4f, center.z - half + j * 4f);
                }
            }
            watch.Stop();
            int samples = cells * cells;
            double perSample = watch.Elapsed.TotalMilliseconds * 1000.0 / samples;

            Heightmap prefabMap = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
            // A zone far from the player, so HeightmapBuilder has nothing cached for it.
            Vector3 far = ZoneSystem.GetZonePos(ZoneSystem.GetZone(center + new Vector3(640f, 0f, 640f)));
            Stopwatch zoneWatch = Stopwatch.StartNew();
            HeightmapBuilder.instance.RequestTerrainSync(far, prefabMap.m_width, prefabMap.m_scale, false, gen);
            zoneWatch.Stop();

            return samples + " generated heights in " + watch.Elapsed.TotalMilliseconds.ToString("F0") + " ms = "
                + perSample.ToString("F2") + " us each (checksum " + sum.ToString("F0") + ")\n"
                + "one zone's exact heightmap build: " + zoneWatch.Elapsed.TotalMilliseconds.ToString("F1") + " ms";
        }

        private static void Lay(Terminal.ConsoleEventArgs args)
        {
            Terminal terminal = args.Context;
            Player player = Player.m_localPlayer;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || player == null)
            {
                Say(terminal, "paths lay runs on the server with a local player - a local game, or its host.");
                return;
            }
            if (laying)
            {
                Say(terminal, "Still laying the last one.");
                return;
            }
            Vector3 from = player.transform.position;
            Vector2 goal;
            if (args.Length > 3 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                goal = new Vector2(x, z);
            }
            else if (args.Length > 2 && ZoneSystem.instance.FindClosestLocation(args[2], from, out ZoneSystem.LocationInstance location))
            {
                goal = new Vector2(location.m_position.x, location.m_position.z);
            }
            else
            {
                Say(terminal, args.Length > 2 ? "No location called " + args[2] + " - boss altars are Eikthyrnir, GDKing, Bonemass, Dragonqueen, GoblinKing." : Usage);
                return;
            }

            laying = true;
            List<Vector2> starts = new List<Vector2> { new Vector2(from.x, from.z) };
            Say(terminal, "Laying a path from " + starts[0].ToString("F0") + " to " + goal.ToString("F0")
                + ", " + Vector2.Distance(starts[0], goal).ToString("F0") + " m as the raven flies.");
            Instance.StartCoroutine(PathLayer.Lay(starts, goal, text => Say(terminal, text), outcome =>
            {
                laying = false;
                if (outcome.Failure != null)
                {
                    Say(terminal, "No path: " + outcome.Failure + " (" + outcome.Search.Expanded + " cells searched in "
                        + outcome.Search.Milliseconds.ToString("F0") + " ms).");
                    return;
                }
                lastBackups = outcome.Written.Backups;
                lastLandings = outcome.Landings;
                DropPins(outcome.Trail);
                outcome.Trail.Stats(out float water, out float steepest);
                TerrainWriter.Result written = outcome.Written;
                Say(terminal, "Laid " + outcome.Trail.Length.ToString("F0") + " m, " + water.ToString("F0") + " m of it over water, "
                    + "steepest grade " + (steepest * 100f).ToString("F0") + "%.\n"
                    + "Search: " + outcome.Search.Expanded + " cells expanded, " + outcome.Search.Sampled + " heights sampled, "
                    + outcome.Search.Milliseconds.ToString("F0") + " ms spread over frames, around "
                    + outcome.Structures + " built pieces.\n"
                    + "Terrain: " + written.Zones + " zones written (" + written.Created + " new compilers), "
                    + written.Painted + " texels painted, " + written.Levelled + " vertices levelled, "
                    + written.Skipped + " zones skipped; " + outcome.Cleared.Cleared + " trees and rocks cleared, "
                    + outcome.Cleared.Kept + " kept near buildings; " + outcome.Landings.Count + " landing barrels; "
                    + outcome.WriteMilliseconds.ToString("F0") + " ms. "
                    + "'paths undo' takes the terrain and the barrels back, not the trees.");
            }));
        }

        /// <summary>An unsaved pin every 50 m of the trail, so its shape shows on the map.</summary>
        private static void DropPins(Trail trail)
        {
            if (Minimap.instance == null)
            {
                return;
            }
            int every = Mathf.Max(1, Mathf.RoundToInt(50f / Trail.Spacing));
            for (int i = 0; i < trail.Points.Count; i += every)
            {
                Vector2 p = trail.Points[i];
                trailPins.Add(Minimap.instance.AddPin(new Vector3(p.x, trail.Ground[i], p.y),
                    trail.Water[i] ? Minimap.PinType.Icon2 : Minimap.PinType.Icon3, "", save: false, isChecked: false));
            }
        }

        private static string ClearPins()
        {
            if (Minimap.instance != null)
            {
                foreach (Minimap.PinData pin in trailPins)
                {
                    Minimap.instance.RemovePin(pin);
                }
            }
            int count = trailPins.Count;
            trailPins.Clear();
            return "Removed " + count + " pins.";
        }

        /// <summary>
        /// Writes back what each zone held before the last lay and takes its barrels away. A zone that had no terrain data
        /// gets an empty blob rather than losing its compiler, which is what vanilla leaves
        /// behind too, and needs no ownership.
        /// </summary>
        private static string Undo()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return "paths undo runs on the server.";
            }
            if (lastBackups == null)
            {
                return "Nothing to undo.";
            }
            Heightmap prefabMap = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
            int restored = 0;
            foreach (KeyValuePair<Vector2s, TerrainWriter.ZoneBackup> entry in lastBackups)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(entry.Value.Compiler);
                if (zdo == null)
                {
                    continue;
                }
                byte[] data = entry.Value.Data ?? new TerrainWriter.TerrainData(prefabMap.m_width + 1)
                    .Encode(ZoneSystem.GetZonePos(entry.Key), prefabMap.m_width * prefabMap.m_scale * 0.72f);
                zdo.Set(ZDOVars.s_TCData, data);
                restored++;
            }
            lastBackups = null;
            int removed = 0;
            foreach (ZDOID id in lastLandings ?? new List<ZDOID>())
            {
                ZDO barrel = ZDOMan.instance.GetZDO(id);
                if (barrel != null)
                {
                    barrel.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(barrel);
                    removed++;
                }
            }
            lastLandings = null;
            return "Restored " + restored + " zones, removed " + removed + " barrels.";
        }

        private static void Say(Terminal terminal, string text)
        {
            UnityEngine.Debug.Log("[OdinsPaths] " + text);
            foreach (string line in text.Split('\n'))
            {
                terminal.AddString(line);
            }
        }
    }
}
