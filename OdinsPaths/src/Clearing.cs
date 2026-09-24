using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// Takes the trees and rocks the world generated off a path, as their ZDOs: gone for every
    /// player, no drops, no stumps. Server only - the server owns every ZDO's fate.
    ///
    /// Two moments: right after a path is laid, for the zones that already exist (measured
    /// against the trail itself, and never near something a player built - a grown sapling is
    /// the same prefab as a wild tree); and whenever the server generates a zone later, for the
    /// stretch of path already written into it (measured against the dirt in the zone's terrain
    /// data - a zone generated just now holds no player's work, so all its dirt is a path's).
    ///
    /// Only what <c>ZoneSystem.m_vegetation</c> places is touched, and of that only trees, logs,
    /// and rocks and bushes that give nothing but wood, stone, flint or resin: ore, nests and
    /// pickables stay. Nothing inside a location is touched. A tree goes if its trunk stands on
    /// the path; a rock or bush if any of it reaches onto the path, measured from its meshes and
    /// the scale it was placed at - a boulder the path runs through goes, however big.
    /// </summary>
    internal static class Clearing
    {
        /// <summary>A trunk this far past the path's edge still goes.</summary>
        private const float Trunk = 1f;
        /// <summary>A rock or bush without meshes to measure counts as this wide from its centre.</summary>
        private const float Unmeasured = 2.5f;
        /// <summary>Vegetation this close to a player-built piece stays: it may have been planted.</summary>
        private const float WorkRadius = 10f;
        /// <summary>A texel with this much dirt counts as path.</summary>
        private const float Dirt = 0.3f;
        private const float MinLocationRadius = 8f;
        private const int ZonesPerFrame = 4;

        private static readonly HashSet<string> PlainDrops = new HashSet<string>
        {
            "Wood", "FineWood", "RoundLog", "Stone", "Flint", "Resin",
        };

        private struct Kind
        {
            /// <summary>How far past its centre it reaches, at scale 1.</summary>
            public float Reach;
            /// <summary>Whether its reach grows with the scale it was placed at (not a trunk's).</summary>
            public bool Scales;
            /// <summary>The prefab's own scale, for a ZDO that stores none.</summary>
            public float PrefabScale;
        }

        /// <summary>Clearable vegetation prefab hash -> its kind; built once per world.</summary>
        private static Dictionary<int, Kind> clearable;
        /// <summary>The farthest any clearable vegetation reaches, at its largest scale.</summary>
        private static float maxReach;
        private static ZoneSystem clearableFor;

        internal sealed class Result
        {
            public int Cleared;
            /// <summary>On the path, but left for being near a player's building.</summary>
            public int Kept;
        }

        /// <summary>The zones that already exist along a freshly laid trail.</summary>
        public static IEnumerator Clear(Trail trail, Result result)
        {
            Clearable();
            Dictionary<Vector2s, List<int>> zones = TerrainWriter.ZonesNear(trail, TerrainWriter.MaxHalfWidth + maxReach);
            int count = 0;
            foreach (KeyValuePair<Vector2s, List<int>> entry in zones)
            {
                List<int> segments = entry.Value;
                ClearZone(entry.Key, (at, reach) => TrailDistance(trail, segments, at) < TerrainWriter.HalfWidthAt(at) + reach, true, result);
                if (++count % ZonesPerFrame == 0)
                {
                    yield return null;
                }
            }
        }

        /// <summary>A zone the server has just generated, if a path runs through it.</summary>
        public static void ClearNewZone(Vector2s zone)
        {
            ZDO compiler = TerrainWriter.FindCompiler(zone);
            byte[] bytes = compiler != null ? compiler.GetByteArray(ZDOVars.s_TCData) : null;
            if (bytes == null)
            {
                return;
            }
            Heightmap prefabMap = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
            int pitch = prefabMap.m_width + 1;
            float scale = prefabMap.m_scale;
            TerrainWriter.TerrainData terrain = TerrainWriter.TerrainData.Decode(bytes, pitch);
            if (terrain == null)
            {
                return;
            }
            Vector3 center = ZoneSystem.GetZonePos(zone);
            float origin = -prefabMap.m_width * scale * 0.5f;
            // Texel x covers the metre from vertex x to vertex x + 1 (see TerrainWriter).
            Vector2 firstTexel = new Vector2(center.x + origin, center.z + origin) + new Vector2(0.5f, 0.5f) * scale;
            Result result = new Result();
            ClearZone(zone, (at, reach) => NearDirt(terrain, pitch, scale, firstTexel, at, reach), false, result);
            if (result.Cleared > 0)
            {
                Debug.Log("[OdinsPaths] Zone " + zone + " generated: cleared " + result.Cleared + " trees and rocks off the path.");
            }
        }

        private static void ClearZone(Vector2s zone, Func<Vector2, float, bool> onPath, bool spareWork, Result result)
        {
            Dictionary<int, Kind> kinds = Clearable();
            List<ZDO> doomed = new List<ZDO>();
            List<Vector2> work = null;
            foreach (ZDO zdo in Objects(zone))
            {
                if (!kinds.TryGetValue(zdo.GetPrefab(), out Kind kind))
                {
                    continue;
                }
                Vector3 p = zdo.GetPosition();
                Vector2 at = new Vector2(p.x, p.z);
                float reach = kind.Scales ? kind.Reach * ScaleOf(zdo, kind.PrefabScale) : kind.Reach;
                if (!onPath(at, reach) || InLocation(at))
                {
                    continue;
                }
                if (spareWork)
                {
                    if (work == null)
                    {
                        work = PlayerWork(zone);
                    }
                    if (Near(work, at, WorkRadius))
                    {
                        result.Kept++;
                        continue;
                    }
                }
                doomed.Add(zdo);
            }
            // The server may take any ZDO over; DestroyZDO only sends what its owner destroys.
            foreach (ZDO zdo in doomed)
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(zdo);
                result.Cleared++;
            }
        }

        /// <summary>The scale it was placed at, as <c>ZNetView.Awake</c> reads it back.</summary>
        private static float ScaleOf(ZDO zdo, float prefabScale)
        {
            Vector3 scale = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero);
            if (scale != Vector3.zero)
            {
                return Mathf.Max(scale.x, scale.z);
            }
            return zdo.GetFloat(ZDOVars.s_scaleScalarHash, prefabScale);
        }

        private static float TrailDistance(Trail trail, List<int> segments, Vector2 at)
        {
            float best = float.MaxValue;
            foreach (int segment in segments)
            {
                best = Mathf.Min(best, trail.Nearest(at, segment, out float _));
            }
            return best;
        }

        private static bool NearDirt(TerrainWriter.TerrainData terrain, int pitch, float scale, Vector2 firstTexel,
            Vector2 at, float radius)
        {
            float fx = (at.x - firstTexel.x) / scale;
            float fy = (at.y - firstTexel.y) / scale;
            float r = radius / scale;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(fx - r));
            int x1 = Mathf.Min(pitch - 1, Mathf.CeilToInt(fx + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(fy - r));
            int y1 = Mathf.Min(pitch - 1, Mathf.CeilToInt(fy + r));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int index = y * pitch + x;
                    if (terrain.ModifiedPaint[index] && terrain.Paint[index].r > Dirt
                        && (x - fx) * (x - fx) + (y - fy) * (y - fy) <= r * r)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static List<ZDO> Objects(Vector2s zone)
        {
            List<ZDO> objects = new List<ZDO>();
            ZDOMan.instance.FindObjects(zone, objects, new HashSet<ZoneSystem.SectorIndex>());
            return objects;
        }

        /// <summary>Where the pieces players built stand, in the zone and around it.</summary>
        private static List<Vector2> PlayerWork(Vector2s zone)
        {
            List<Vector2> work = new List<Vector2>();
            for (int x = zone.x - 1; x <= zone.x + 1; x++)
            {
                for (int y = zone.y - 1; y <= zone.y + 1; y++)
                {
                    foreach (ZDO zdo in Objects(new Vector2s(x, y)))
                    {
                        if (zdo.GetLong(ZDOVars.s_creator, 0L) != 0L)
                        {
                            Vector3 p = zdo.GetPosition();
                            work.Add(new Vector2(p.x, p.z));
                        }
                    }
                }
            }
            return work;
        }

        private static bool Near(List<Vector2> points, Vector2 at, float radius)
        {
            foreach (Vector2 p in points)
            {
                if ((p - at).sqrMagnitude < radius * radius)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Inside any location, the altar a path leads to included. Server only.</summary>
        private static bool InLocation(Vector2 at)
        {
            Vector2s zone = ZoneSystem.GetZone(new Vector3(at.x, 0f, at.y));
            for (int x = zone.x - 1; x <= zone.x + 1; x++)
            {
                for (int y = zone.y - 1; y <= zone.y + 1; y++)
                {
                    if (ZoneSystem.instance.m_locationInstances.TryGetValue(new Vector2s(x, y), out ZoneSystem.LocationInstance instance))
                    {
                        float radius = Mathf.Max(instance.m_location.m_exteriorRadius, MinLocationRadius);
                        if ((new Vector2(instance.m_position.x, instance.m_position.z) - at).sqrMagnitude < radius * radius)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static Dictionary<int, Kind> Clearable()
        {
            if (clearable != null && clearableFor == ZoneSystem.instance)
            {
                return clearable;
            }
            clearable = new Dictionary<int, Kind>();
            clearableFor = ZoneSystem.instance;
            maxReach = Trunk;
            foreach (ZoneSystem.ZoneVegetation veg in ZoneSystem.instance.m_vegetation)
            {
                GameObject prefab = veg.m_prefab;
                if (prefab == null || prefab.GetComponent<ZNetView>() == null || !KindOf(prefab, out Kind kind))
                {
                    continue;
                }
                clearable[prefab.name.GetStableHashCode()] = kind;
                if (kind.Scales)
                {
                    maxReach = Mathf.Max(maxReach, kind.Reach * Mathf.Max(kind.PrefabScale, veg.m_scaleMax));
                }
            }
            return clearable;
        }

        /// <summary>Whether this vegetation may go, and how far it reaches.</summary>
        private static bool KindOf(GameObject prefab, out Kind kind)
        {
            kind = new Kind { Reach = Trunk, PrefabScale = prefab.transform.localScale.x };
            if (prefab.GetComponent<SpawnArea>() != null || prefab.GetComponent<CreatureSpawner>() != null
                || prefab.GetComponent<Pickable>() != null || prefab.GetComponent<Container>() != null)
            {
                return false;
            }
            if (prefab.GetComponent<TreeBase>() != null || prefab.GetComponent<TreeLog>() != null)
            {
                return true;
            }
            MineRock rock = prefab.GetComponent<MineRock>();
            MineRock5 boulder = prefab.GetComponent<MineRock5>();
            if (rock != null && !Plain(rock.m_dropItems) || boulder != null && !Plain(boulder.m_dropItems))
            {
                return false;
            }
            if (rock == null && boulder == null)
            {
                if (prefab.GetComponent<Destructible>() == null)
                {
                    return false;
                }
                DropOnDestroyed drops = prefab.GetComponent<DropOnDestroyed>();
                if (drops != null && !Plain(drops.m_dropWhenDestroyed))
                {
                    return false;
                }
            }
            float measured = Radius(prefab);
            kind.Reach = measured > 0f ? measured : Unmeasured;
            kind.Scales = measured > 0f;
            return true;
        }

        /// <summary>
        /// How far its meshes reach from its centre along x or z, in its own unscaled space - the
        /// longer of the two, since vegetation is placed at any rotation. 0 without meshes.
        /// </summary>
        private static float Radius(GameObject prefab)
        {
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            float reach = 0f;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds bounds = filter.sharedMesh.bounds;
                Matrix4x4 toPrefab = toRoot * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 p = toPrefab.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, offset));
                    reach = Mathf.Max(reach, Mathf.Abs(p.x), Mathf.Abs(p.z));
                }
            }
            return reach;
        }

        private static bool Plain(DropTable table)
        {
            if (table == null)
            {
                return true;
            }
            foreach (DropTable.DropData drop in table.m_drops)
            {
                if (drop.m_item != null && !PlainDrops.Contains(drop.m_item.name))
                {
                    return false;
                }
            }
            return true;
        }
    }

    public partial class OdinsPathsPlugin
    {
        /// <summary>
        /// A zone the server generates for the first time - fully, or as a ghost beyond the
        /// players - gets the trees and rocks on a path already written into it taken away
        /// again, right after they were placed.
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SpawnZone))]
        public static class ClearNewZones
        {
            private static void Prefix(ZoneSystem __instance, Vector2s zoneID, ZoneSystem.SpawnMode mode, out bool __state)
            {
                __state = (mode == ZoneSystem.SpawnMode.Full || mode == ZoneSystem.SpawnMode.Ghost)
                    && ZNet.instance != null && ZNet.instance.IsServer() && !__instance.IsZoneGenerated(zoneID);
            }

            private static void Postfix(Vector2s zoneID, bool __result, bool __state)
            {
                if (__state && __result)
                {
                    Clearing.ClearNewZone(zoneID);
                }
            }
        }
    }
}
