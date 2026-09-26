using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// The height the game builds the ground at, anywhere, without building a zone: what
    /// <c>HeightmapBuilder.Build</c> does per vertex. <c>WorldGenerator.GetHeight</c> takes the
    /// biome at the point itself; the builder takes the biomes at the four corners of the zone's
    /// heightmap and, where they differ, blends their heights across the whole zone with a
    /// smoothstep. Near a biome border the two can be metres apart, and a small patch of another
    /// biome inside one zone is never built at all. A path levelled against GetHeight gets a step
    /// there, and a search sees a cliff that is not in the game.
    ///
    /// Callable from the search's thread once <see cref="Prepare"/> has run on the main thread (it
    /// reads the zone prefab); the corner biomes are kept per zone, under a lock.
    /// </summary>
    internal static class Ground
    {
        private struct Corners
        {
            public Heightmap.Biome SW, SE, NW, NE;
            public bool Same;
        }

        private static readonly Dictionary<long, Corners> corners = new Dictionary<long, Corners>();
        private static WorldGenerator cachedFor;
        /// <summary>The zone heightmap's side, width x scale: 64 m.</summary>
        private static float size;

        /// <summary>Main thread: for this world, before a thread asks for heights.</summary>
        public static void Prepare()
        {
            WorldGenerator gen = WorldGenerator.instance;
            if (gen == cachedFor)
            {
                return;
            }
            Heightmap map = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
            lock (corners)
            {
                corners.Clear();
                size = map.m_width * map.m_scale;
                cachedFor = gen;
            }
        }

        public static float Height(float x, float z)
        {
            WorldGenerator gen = WorldGenerator.instance;
            if (gen != cachedFor)
            {
                Prepare();
            }
            Vector3 center = ZoneSystem.GetZonePos(ZoneSystem.GetZone(new Vector3(x, 0f, z)));
            float cornerX = center.x - size * 0.5f;
            float cornerZ = center.z - size * 0.5f;
            long key = ((long)Mathf.RoundToInt(cornerX) << 32) | (uint)Mathf.RoundToInt(cornerZ);
            Corners c;
            bool known;
            lock (corners)
            {
                known = corners.TryGetValue(key, out c);
            }
            if (!known)
            {
                c.SW = gen.GetBiome(cornerX, cornerZ);
                c.SE = gen.GetBiome(cornerX + size, cornerZ);
                c.NW = gen.GetBiome(cornerX, cornerZ + size);
                c.NE = gen.GetBiome(cornerX + size, cornerZ + size);
                c.Same = c.SE == c.SW && c.NW == c.SW && c.NE == c.SW;
                lock (corners)
                {
                    corners[key] = c;
                }
            }
            if (c.Same)
            {
                return gen.GetBiomeHeight(c.SW, x, z, out Color _);
            }
            float tx = DUtils.SmoothStep(0f, 1f, (x - cornerX) / size);
            float tz = DUtils.SmoothStep(0f, 1f, (z - cornerZ) / size);
            float south = DUtils.Lerp(gen.GetBiomeHeight(c.SW, x, z, out Color _), gen.GetBiomeHeight(c.SE, x, z, out Color _), tx);
            float north = DUtils.Lerp(gen.GetBiomeHeight(c.NW, x, z, out Color _), gen.GetBiomeHeight(c.NE, x, z, out Color _), tx);
            return DUtils.Lerp(south, north, tz);
        }
    }
}
