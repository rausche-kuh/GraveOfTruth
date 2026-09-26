using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// Along a road through the Mistlands - a dirt track there (<see cref="Trail.Mistlands"/>) -
    /// a Dvergr road post every so often, as the Mistlands' own stand (<c>Mistlands_RoadPost1</c>:
    /// a black marble post with the lamp on top, 3.5 m up): it lights the way and clears the mist
    /// around it. They stand just past the road's edge, on alternate sides. Structures like the
    /// landing posts, so later roads walk around them, and a player may break them as any other. Server only.
    /// </summary>
    internal static class Lamps
    {
        internal const string LampPrefab = "dverger_demister";
        internal const string PostPrefab = "blackmarble_post01";
        /// <summary>How high on its post the lamp sits, as in <c>Mistlands_RoadPost1</c>.</summary>
        private const float LampHeight = 3.5f;
        /// <summary>How far round its foot the post looks for the lowest ground, so it never stands on air.</summary>
        private const float Foot = 0.75f;
        /// <summary>Metres between two posts along the Mistlands: every 24 m was too many (seen in game 2026-09-25).</summary>
        private const float Every = 48f;
        /// <summary>How far past the paint's widest a post stands.</summary>
        private const float Clearance = 1f;
        /// <summary>A structure this close to a spot already stands there: another lamp, a player's piece.</summary>
        private const float Taken = 6f;
        /// <summary>Ground steeper than this (rise over run) under a spot tries the other side of the road.</summary>
        private const float MaxSlope = 0.8f;

        /// <summary>The posts and lamps placed, as their ZDOs.</summary>
        public static List<ZDOID> Place(Trail trail, Structures structures, List<Circle> locations)
        {
            List<ZDOID> placed = new List<ZDOID>();
            if (!trail.Mistlands.Contains(true))
            {
                return placed;
            }
            GameObject prefab = ZNetScene.instance.GetPrefab(LampPrefab);
            GameObject post = ZNetScene.instance.GetPrefab(PostPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[OdinsPaths] No " + LampPrefab + " prefab - the roads through the Mistlands are left unlit.");
                return placed;
            }
            int step = Mathf.Max(1, Mathf.RoundToInt(Every / Trail.Spacing));
            int side = 1;
            int i = 0;
            while (i < trail.Points.Count)
            {
                if (!trail.Mistlands[i] || trail.Water[i])
                {
                    i++;
                    continue;
                }
                int start = i;
                while (i < trail.Points.Count && trail.Mistlands[i] && !trail.Water[i])
                {
                    i++;
                }
                // The first a few metres into the stretch, where the stone gives out; a short one gets one in its middle.
                int length = i - start;
                for (int k = start + Mathf.Min(2, length / 2); k < i; k += step)
                {
                    if (Mark(trail, k, side, prefab, post, structures, locations, placed) || Mark(trail, k, -side, prefab, post, structures, locations, placed))
                    {
                        side = -side;
                    }
                }
            }
            return placed;
        }

        private static bool Mark(Trail trail, int index, int side, GameObject prefab, GameObject post, Structures structures, List<Circle> locations, List<ZDOID> placed)
        {
            int a = Mathf.Max(0, index - 1);
            int b = Mathf.Min(trail.Points.Count - 1, index + 1);
            Vector2 dir = (trail.Points[b] - trail.Points[a]).normalized;
            if (dir == Vector2.zero)
            {
                return false;
            }
            Vector2 across = new Vector2(-dir.y, dir.x) * side;
            Vector2 at = trail.Points[index] + across * (trail.Kind.MaxHalfWidth * (1f + TerrainWriter.EdgeWobble) + Clearance);
            float ground = Ground.Height(at.x, at.y);
            if (ground < ZoneSystem.instance.m_waterLevel + Trail.ShoreMargin || structures.Distance(at, Taken) < Taken
                || Slope(at) > MaxSlope || InAny(locations, at))
            {
                return false;
            }
            // Facing the road, as the road posts face theirs; the post's foot on the lowest ground under it.
            Quaternion rotation = Quaternion.LookRotation(new Vector3(-across.x, 0f, -across.y));
            float foot = ground;
            for (int corner = 0; corner < 4; corner++)
            {
                foot = Mathf.Min(foot, Ground.Height(at.x + ((corner & 1) == 0 ? -Foot : Foot), at.y + ((corner & 2) == 0 ? -Foot : Foot)));
            }
            if (post != null)
            {
                placed.Add(Landings.Spawn(post, new Vector3(at.x, foot, at.y), rotation).m_uid);
            }
            placed.Add(Landings.Spawn(prefab, new Vector3(at.x, (post != null ? foot + LampHeight : ground + 0.3f), at.y), rotation).m_uid);
            structures.Add(at);
            return true;
        }

        /// <summary>The ground's slope at p, rise over run, over a metre either way.</summary>
        private static float Slope(Vector2 p)
        {
            float dx = Ground.Height(p.x + 1f, p.y) - Ground.Height(p.x - 1f, p.y);
            float dz = Ground.Height(p.x, p.y + 1f) - Ground.Height(p.x, p.y - 1f);
            return Mathf.Sqrt(dx * dx + dz * dz) / 2f;
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
    }
}
