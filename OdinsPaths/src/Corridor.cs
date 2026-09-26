using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// The strip the fine search may use: every point within half a width of a coarse route. The
    /// route is sampled every quarter width and the samples bucketed by half a width, so a query
    /// looks at nine buckets.
    /// </summary>
    internal sealed class Corridor
    {
        private readonly float radius;
        private readonly Dictionary<long, List<Vector2>> buckets = new Dictionary<long, List<Vector2>>();

        public Corridor(List<Vector2> route, float width)
        {
            radius = Mathf.Max(width * 0.5f, 1f);
            float step = radius * 0.5f;
            for (int i = 0; i < route.Count; i++)
            {
                Add(route[i]);
                if (i + 1 < route.Count)
                {
                    float length = Vector2.Distance(route[i], route[i + 1]);
                    for (float at = step; at < length; at += step)
                    {
                        Add(Vector2.Lerp(route[i], route[i + 1], at / length));
                    }
                }
            }
        }

        public bool Contains(Vector2 p)
        {
            int bx = Mathf.FloorToInt(p.x / radius);
            int by = Mathf.FloorToInt(p.y / radius);
            float radiusSq = radius * radius;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (buckets.TryGetValue(Key(bx + dx, by + dy), out List<Vector2> points))
                    {
                        foreach (Vector2 q in points)
                        {
                            if ((q - p).sqrMagnitude <= radiusSq)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private void Add(Vector2 p)
        {
            long key = Key(Mathf.FloorToInt(p.x / radius), Mathf.FloorToInt(p.y / radius));
            if (!buckets.TryGetValue(key, out List<Vector2> points))
            {
                buckets[key] = points = new List<Vector2>();
            }
            points.Add(p);
        }

        private static long Key(int i, int j) => ((long)i << 32) | (uint)j;
    }
}
