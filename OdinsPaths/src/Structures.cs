using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// Everything built that stands in a search area: every piece a player placed, the pieces of
    /// generated ruins and villages, and the barrels earlier paths left at their landings - any
    /// ZDO whose prefab is a <c>Piece</c> or wears (<c>WearNTear</c>). The search goes around
    /// them, and the terrain writer leaves the ground under and beside them alone. Server only:
    /// only the server holds every ZDO, loaded or not.
    /// </summary>
    internal sealed class Structures
    {
        /// <summary>How far a piece reaches from its centre - a wall is 4 m long.</summary>
        public const float PieceReach = 2f;
        private const float BucketSize = 8f;
        /// <summary>Dungeon interiors sit at y 5000 above their entrances; they are not in the way.</summary>
        private const float InteriorHeight = 1000f;

        /// <summary>Prefab hash -> whether it is a structure; built once per scene.</summary>
        private static Dictionary<int, bool> kinds;
        private static ZNetScene kindsFor;

        private readonly Dictionary<long, List<Vector2>> buckets = new Dictionary<long, List<Vector2>>();

        public int Count { get; private set; }

        /// <summary>The structures inside the ellipse of each start with the goal, as the search has it.</summary>
        public static Structures Around(List<Vector2> starts, Vector2 goal)
        {
            Structures result = new Structures();
            float[] limit = new float[starts.Count];
            for (int s = 0; s < starts.Count; s++)
            {
                limit[s] = PathSearch.EllipseLimit(starts[s], goal);
            }
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                Vector3 p = zdo.GetPosition();
                if (p.y > InteriorHeight || !IsStructure(zdo.GetPrefab()))
                {
                    continue;
                }
                Vector2 at = new Vector2(p.x, p.z);
                float toGoal = Vector2.Distance(at, goal);
                for (int s = 0; s < starts.Count; s++)
                {
                    if (Vector2.Distance(at, starts[s]) + toGoal <= limit[s])
                    {
                        result.Add(at);
                        break;
                    }
                }
            }
            return result;
        }

        public void Add(Vector2 at)
        {
            long key = Key(Mathf.FloorToInt(at.x / BucketSize), Mathf.FloorToInt(at.y / BucketSize));
            if (!buckets.TryGetValue(key, out List<Vector2> bucket))
            {
                buckets[key] = bucket = new List<Vector2>();
            }
            bucket.Add(at);
            Count++;
        }

        /// <summary>The distance to the nearest structure, or float.MaxValue if none is within reach.</summary>
        public float Distance(Vector2 at, float reach)
        {
            float best = float.MaxValue;
            if (Count == 0)
            {
                return best;
            }
            int x0 = Mathf.FloorToInt((at.x - reach) / BucketSize);
            int x1 = Mathf.FloorToInt((at.x + reach) / BucketSize);
            int y0 = Mathf.FloorToInt((at.y - reach) / BucketSize);
            int y1 = Mathf.FloorToInt((at.y + reach) / BucketSize);
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (!buckets.TryGetValue(Key(x, y), out List<Vector2> bucket))
                    {
                        continue;
                    }
                    foreach (Vector2 p in bucket)
                    {
                        best = Mathf.Min(best, (p - at).sqrMagnitude);
                    }
                }
            }
            return best == float.MaxValue ? best : Mathf.Sqrt(best);
        }

        private static bool IsStructure(int prefab)
        {
            if (kinds == null || kindsFor != ZNetScene.instance)
            {
                kinds = new Dictionary<int, bool>();
                kindsFor = ZNetScene.instance;
            }
            if (!kinds.TryGetValue(prefab, out bool structure))
            {
                GameObject go = ZNetScene.instance.GetPrefab(prefab);
                structure = go != null && (go.GetComponent<Piece>() != null || go.GetComponent<WearNTear>() != null);
                kinds[prefab] = structure;
            }
            return structure;
        }

        private static long Key(int i, int j) => ((long)i << 32) | (uint)j;
    }
}
