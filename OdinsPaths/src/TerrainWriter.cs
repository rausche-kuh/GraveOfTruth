using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// Lays a trail into the world as the game's own terrain data: per zone, the blob a
    /// <c>TerrainComp</c> saves in <c>ZDOVars.s_TCData</c>, merged with what is there or written
    /// onto a new terrain compiler ZDO. No zone needs to be loaded; one that is picks the change
    /// up by itself (<c>TerrainComp.CheckLoad</c> watches the ZDO's data revision). Server only:
    /// only the server holds every ZDO, so only it can be sure a zone has no compiler yet.
    ///
    /// The base heights and base paint of a zone come from the game's heightmap builder thread,
    /// asked without blocking (<c>RequestTerrain</c>), so the main thread only merges and packs.
    /// </summary>
    internal static class TerrainWriter
    {
        private const string CompilerPrefab = "_TerrainCompiler";
        /// <summary>The blob format <c>TerrainComp.Save</c> writes.</summary>
        private const int FormatVersion = 1;
        /// <summary>Where the dirt fades out at the path's edge, in metres.</summary>
        private const float EdgeSoftness = 0.8f;
        /// <summary>How much the painted width wobbles, as a share of it.</summary>
        private const float EdgeWobble = 0.15f;
        /// <summary>Beyond the path's edge, the levelling blends back into the ground over this.</summary>
        private const float Shoulder = 1.5f;
        private const int ZonesPerFrame = 2;

        /// <summary>Over about this many metres the width drifts from narrow to wide and back.</summary>
        private const float WidthDrift = 40f;

        /// <summary>How far from the trail's line the writer may change anything: edge and shoulder.</summary>
        internal static float Reach => MaxHalfWidth * (1f + EdgeWobble) + Shoulder;

        /// <summary>Half the widest a path gets.</summary>
        internal static float MaxHalfWidth => (OdinsPathsPlugin.PathWidth.Value + OdinsPathsPlugin.WidthVariation.Value) * 0.5f;

        /// <summary>
        /// Half the path's width at a point: the configured width, drifting by up to the variation
        /// either way. Slow noise over the world, offset by its seed - a trail's two edges get the
        /// same width, and where two paths meet they agree on it.
        /// </summary>
        internal static float HalfWidthAt(Vector2 at)
        {
            int seed = WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 0;
            float offset = (seed & 0x3ff) * 1.37f;
            // Perlin noise rarely strays far from 0.5; stretched, the path reaches both extremes.
            float noise = Mathf.Clamp((Mathf.PerlinNoise(at.x / WidthDrift + offset, at.y / WidthDrift - offset) - 0.5f) * 2.5f, -1f, 1f);
            float width = OdinsPathsPlugin.PathWidth.Value + OdinsPathsPlugin.WidthVariation.Value * noise;
            return Mathf.Max(0.5f, width) * 0.5f;
        }

        /// <summary>What was in a zone before the trail, so a dev "undo" can put it back.</summary>
        internal sealed class ZoneBackup
        {
            public ZDOID Compiler;
            public byte[] Data;
        }

        internal sealed class Result
        {
            public int Zones;
            public int Created;
            public int Painted;
            public int Levelled;
            public int Skipped;
            public readonly Dictionary<Vector2s, ZoneBackup> Backups = new Dictionary<Vector2s, ZoneBackup>();
        }

        public static IEnumerator Write(Trail trail, List<Circle> locations, Structures structures, Result result)
        {
            Heightmap prefabMap = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
            int width = prefabMap.m_width;
            float scale = prefabMap.m_scale;
            float reach = Reach + scale;

            Dictionary<Vector2s, List<int>> zones = ZonesNear(trail, reach);

            // Ask the builder thread for all of them at once, take each as it is ready, and
            // merge a couple per frame - one zone is a few ms of distance checks.
            HashSet<Vector2s> pending = new HashSet<Vector2s>(zones.Keys);
            Dictionary<Vector2s, HeightmapBuilder.HMBuildData> built = new Dictionary<Vector2s, HeightmapBuilder.HMBuildData>();
            List<Vector2s> done = new List<Vector2s>();
            while (pending.Count > 0 || built.Count > 0)
            {
                foreach (Vector2s zone in pending)
                {
                    HeightmapBuilder.HMBuildData data = HeightmapBuilder.instance.RequestTerrain(
                        ZoneSystem.GetZonePos(zone), width, scale, false, WorldGenerator.instance);
                    if (data != null)
                    {
                        built[zone] = data;
                    }
                }
                pending.ExceptWith(built.Keys);
                done.Clear();
                foreach (KeyValuePair<Vector2s, HeightmapBuilder.HMBuildData> entry in built)
                {
                    if (done.Count == ZonesPerFrame)
                    {
                        break;
                    }
                    WriteZone(entry.Key, entry.Value, trail, zones[entry.Key], locations, structures, result);
                    done.Add(entry.Key);
                }
                foreach (Vector2s zone in done)
                {
                    built.Remove(zone);
                }
                yield return null;
            }
        }

        /// <summary>
        /// Every segment with dry land at one end, filed under each zone that has a point within
        /// reach of it.
        /// </summary>
        internal static Dictionary<Vector2s, List<int>> ZonesNear(Trail trail, float reach)
        {
            Dictionary<Vector2s, List<int>> zones = new Dictionary<Vector2s, List<int>>();
            for (int i = 0; i < trail.Points.Count - 1; i++)
            {
                if (trail.Water[i] && trail.Water[i + 1])
                {
                    continue;
                }
                Vector2 a = trail.Points[i];
                Vector2 b = trail.Points[i + 1];
                Vector2s min = ZoneSystem.GetZone(new Vector3(Mathf.Min(a.x, b.x) - reach, 0f, Mathf.Min(a.y, b.y) - reach));
                Vector2s max = ZoneSystem.GetZone(new Vector3(Mathf.Max(a.x, b.x) + reach, 0f, Mathf.Max(a.y, b.y) + reach));
                for (int x = min.x; x <= max.x; x++)
                {
                    for (int y = min.y; y <= max.y; y++)
                    {
                        Vector2s zone = new Vector2s(x, y);
                        if (!zones.TryGetValue(zone, out List<int> segments))
                        {
                            zones[zone] = segments = new List<int>();
                        }
                        segments.Add(i);
                    }
                }
            }
            return zones;
        }

        private static void WriteZone(Vector2s zone, HeightmapBuilder.HMBuildData data, Trail trail,
            List<int> segments, List<Circle> locations, Structures structures, Result result)
        {
            int width = data.m_width;
            int pitch = width + 1;
            float scale = data.m_scale;
            Vector3 center = ZoneSystem.GetZonePos(zone);
            ZDO compiler = FindCompiler(zone);
            byte[] old = compiler != null ? compiler.GetByteArray(ZDOVars.s_TCData) : null;
            TerrainData terrain = old != null ? TerrainData.Decode(old, pitch) : new TerrainData(pitch);
            if (terrain == null)
            {
                Debug.LogWarning("[OdinsPaths] Zone " + zone + ": terrain data in an unknown shape, left alone.");
                result.Skipped++;
                return;
            }

            float waterLevel = ZoneSystem.instance.m_waterLevel;
            float maxHalfWidth = MaxHalfWidth;
            bool level = OdinsPathsPlugin.Levelling.Value;
            float maxCut = OdinsPathsPlugin.MaxCut.Value;
            float origin = -width * scale * 0.5f;
            // Heights sit on the vertices. The paint mask's texels are half a metre off them, as
            // TerrainComp.PaintCleared's half offset has it.
            Vector2 firstVertex = new Vector2(center.x + origin, center.z + origin);
            Vector2 firstTexel = firstVertex + new Vector2(0.5f, 0.5f) * scale;
            float[] texelDistance = Nearest(trail, segments, firstTexel, pitch, scale, maxHalfWidth * (1f + EdgeWobble), out float[] _);
            float[] vertexDistance = Nearest(trail, segments, firstVertex, pitch, scale, maxHalfWidth + Shoulder, out float[] profile);

            int painted = 0;
            int levelled = 0;
            for (int y = 0; y < pitch; y++)
            {
                for (int x = 0; x < pitch; x++)
                {
                    int index = y * pitch + x;
                    float baseHeight = data.m_baseHeights[index];
                    Vector2 texel = firstTexel + new Vector2(x, y) * scale;
                    float wobble = 1f + EdgeWobble * (Mathf.PerlinNoise(texel.x / 5f, texel.y / 5f) * 2f - 1f);
                    float paintDistance = texelDistance[index];
                    float edge = paintDistance < float.MaxValue ? HalfWidthAt(texel) * wobble : 0f;
                    // No dirt under a building, and levelling fades out over a shoulder beside one.
                    if (paintDistance < edge && baseHeight >= waterLevel + Trail.ShoreMargin
                        && structures.Distance(texel, Structures.PieceReach) >= Structures.PieceReach
                        && Paint(terrain, data.m_baseMask[index], index, texel,
                            Mathf.Clamp01((edge - paintDistance) / EdgeSoftness)))
                    {
                        painted++;
                    }

                    float distance = vertexDistance[index];
                    Vector2 vertex = firstVertex + new Vector2(x, y) * scale;
                    if (!level || distance >= maxHalfWidth + Shoulder)
                    {
                        continue;
                    }
                    float halfWidth = HalfWidthAt(vertex);
                    if (distance >= halfWidth + Shoulder || terrain.ModifiedHeight[index]
                        || baseHeight < waterLevel + Trail.ShoreMargin + 0.2f || InAny(locations, vertex))
                    {
                        continue;
                    }
                    float building = structures.Distance(vertex, Structures.PieceReach + Shoulder);
                    if (building < Structures.PieceReach)
                    {
                        continue;
                    }
                    float blend = distance <= halfWidth ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (distance - halfWidth) / Shoulder);
                    blend *= Mathf.SmoothStep(0f, 1f, (building - Structures.PieceReach) / Shoulder);
                    float delta = Mathf.Clamp(profile[index] - baseHeight, -maxCut, maxCut) * blend;
                    if (Mathf.Abs(delta) < 0.02f)
                    {
                        continue;
                    }
                    terrain.ModifiedHeight[index] = true;
                    terrain.LevelDelta[index] = delta;
                    terrain.SmoothDelta[index] = 0f;
                    levelled++;
                }
            }
            if (painted == 0 && levelled == 0)
            {
                return;
            }

            ZoneBackup backup = new ZoneBackup { Data = old };
            if (compiler == null)
            {
                compiler = CreateCompiler(center);
                result.Created++;
            }
            backup.Compiler = compiler.m_uid;
            if (!result.Backups.ContainsKey(zone))
            {
                result.Backups[zone] = backup;
            }
            // Zone centre and a radius over its corners: a loaded client resets the grass of
            // the whole zone when it reloads (TerrainComp.CheckLoad, one operation more).
            compiler.Set(ZDOVars.s_TCData, terrain.Encode(center, width * scale * 0.72f));
            result.Zones++;
            result.Painted += painted;
            result.Levelled += levelled;
        }

        /// <summary>
        /// Dirt over the ground, fading at the edge; alpha (the vegetation mask) is kept. A texel
        /// the player cultivated or paved is left alone - except that in the Deep North the green
        /// channel is snow depth, which dirt clears.
        /// </summary>
        private static bool Paint(TerrainData terrain, Color baseMask, int index, Vector2 at, float weight)
        {
            Color current = terrain.ModifiedPaint[index] ? terrain.Paint[index] : baseMask;
            bool deepNorth = WorldGenerator.IsDeepnorth(at.x, at.y);
            if (current.b > 0.5f || (!deepNorth && current.g > 0.5f))
            {
                return false;
            }
            Color dirt = current;
            dirt.r = Mathf.Lerp(current.r, 1f, weight);
            dirt.g = Mathf.Lerp(current.g, 0f, weight);
            dirt.b = Mathf.Lerp(current.b, 0f, weight);
            if (terrain.ModifiedPaint[index] && dirt == current)
            {
                return false;
            }
            terrain.ModifiedPaint[index] = true;
            terrain.Paint[index] = dirt;
            return true;
        }

        /// <summary>
        /// For a pitch x pitch grid starting at first: each point's distance to the nearest
        /// segment, and the path height there. Each segment only visits the points within reach
        /// of it; the rest stay at float.MaxValue.
        /// </summary>
        private static float[] Nearest(Trail trail, List<int> segments, Vector2 first, int pitch, float scale,
            float reach, out float[] profile)
        {
            float[] distance = new float[pitch * pitch];
            profile = new float[pitch * pitch];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = float.MaxValue;
            }
            foreach (int segment in segments)
            {
                Vector2 a = trail.Points[segment];
                Vector2 b = trail.Points[segment + 1];
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - first.x) / scale));
                int x1 = Mathf.Min(pitch - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - first.x) / scale));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach - first.y) / scale));
                int y1 = Mathf.Min(pitch - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach - first.y) / scale));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int index = y * pitch + x;
                        float d = trail.Nearest(first + new Vector2(x, y) * scale, segment, out float height);
                        if (d < distance[index])
                        {
                            distance[index] = d;
                            profile[index] = height;
                        }
                    }
                }
            }
            return distance;
        }

        private static bool InAny(List<Circle> circles, Vector2 p)
        {
            foreach (Circle circle in circles)
            {
                if (circle.Contains(p))
                {
                    return true;
                }
            }
            return false;
        }

        internal static ZDO FindCompiler(Vector2s zone)
        {
            int hash = CompilerPrefab.GetStableHashCode();
            List<ZDO> objects = new List<ZDO>();
            ZDOMan.instance.FindObjects(zone, objects, new HashSet<ZoneSystem.SectorIndex>());
            foreach (ZDO zdo in objects)
            {
                if (zdo.GetPrefab() == hash)
                {
                    return zdo;
                }
            }
            return null;
        }

        /// <summary>A compiler ZDO as <c>ZNetView.Awake</c> would make it for the prefab.</summary>
        private static ZDO CreateCompiler(Vector3 center)
        {
            int hash = CompilerPrefab.GetStableHashCode();
            ZNetView view = ZNetScene.instance.GetPrefab(hash).GetComponent<ZNetView>();
            ZDO zdo = ZDOMan.instance.CreateNewZDO(center, hash);
            zdo.Persistent = view.m_persistent;
            zdo.Type = view.m_type;
            zdo.Distant = view.m_distant;
            zdo.SetPrefab(hash);
            zdo.SetRotation(Quaternion.identity);
            return zdo;
        }

        /// <summary>A zone's terrain edits, in <c>TerrainComp</c>'s layout: (width+1)² per array.</summary>
        internal sealed class TerrainData
        {
            public int Operations;
            public readonly bool[] ModifiedHeight;
            public readonly float[] LevelDelta;
            public readonly float[] SmoothDelta;
            public readonly bool[] ModifiedPaint;
            public readonly Color[] Paint;

            public TerrainData(int pitch)
            {
                int n = pitch * pitch;
                ModifiedHeight = new bool[n];
                LevelDelta = new float[n];
                SmoothDelta = new float[n];
                ModifiedPaint = new bool[n];
                Paint = new Color[n];
            }

            /// <summary>
            /// <c>TerrainComp.Load</c>, including its conversion of old saves whose paint arrays
            /// were width² instead of (width+1)². Null if the heights do not fit this pitch.
            /// </summary>
            public static TerrainData Decode(byte[] bytes, int pitch)
            {
                ZPackage pkg = new ZPackage(Utils.Decompress(bytes));
                TerrainData data = new TerrainData(pitch);
                pkg.ReadInt();
                data.Operations = pkg.ReadInt();
                pkg.ReadVector3();
                pkg.ReadSingle();
                int heights = pkg.ReadInt();
                if (heights != data.ModifiedHeight.Length)
                {
                    return null;
                }
                for (int i = 0; i < heights; i++)
                {
                    data.ModifiedHeight[i] = pkg.ReadBool();
                    if (data.ModifiedHeight[i])
                    {
                        data.LevelDelta[i] = pkg.ReadSingle();
                        data.SmoothDelta[i] = pkg.ReadSingle();
                    }
                }
                int paints = pkg.ReadInt();
                int width = pitch - 1;
                if (paints != data.ModifiedPaint.Length && paints != width * width)
                {
                    return null;
                }
                for (int j = 0; j < paints; j++)
                {
                    data.ModifiedPaint[j] = pkg.ReadBool();
                    if (data.ModifiedPaint[j])
                    {
                        data.Paint[j] = new Color(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
                    }
                }
                if (paints == width * width)
                {
                    Color[] paint = (Color[])data.Paint.Clone();
                    bool[] modified = (bool[])data.ModifiedPaint.Clone();
                    for (int k = 0; k < data.Paint.Length; k++)
                    {
                        int row = k / pitch;
                        int nextRow = (k + 1) / pitch;
                        int source = k - row;
                        if (row == width)
                        {
                            source -= width;
                        }
                        if (k > 0 && (k - row) % width == 0 && (k + 1 - nextRow) % width == 0)
                        {
                            source--;
                        }
                        data.Paint[k] = paint[source];
                        data.ModifiedPaint[k] = modified[source];
                    }
                }
                return data;
            }

            /// <summary><c>TerrainComp.Save</c>, one operation more than before.</summary>
            public byte[] Encode(Vector3 opPoint, float opRadius)
            {
                ZPackage pkg = new ZPackage();
                pkg.Write(FormatVersion);
                pkg.Write(Operations + 1);
                pkg.Write(opPoint);
                pkg.Write(opRadius);
                pkg.Write(ModifiedHeight.Length);
                for (int i = 0; i < ModifiedHeight.Length; i++)
                {
                    pkg.Write(ModifiedHeight[i]);
                    if (ModifiedHeight[i])
                    {
                        pkg.Write(LevelDelta[i]);
                        pkg.Write(SmoothDelta[i]);
                    }
                }
                pkg.Write(ModifiedPaint.Length);
                for (int j = 0; j < ModifiedPaint.Length; j++)
                {
                    pkg.Write(ModifiedPaint[j]);
                    if (ModifiedPaint[j])
                    {
                        pkg.Write(Paint[j].r);
                        pkg.Write(Paint[j].g);
                        pkg.Write(Paint[j].b);
                        pkg.Write(Paint[j].a);
                    }
                }
                return Utils.Compress(pkg.GetArray());
            }
        }
    }
}
