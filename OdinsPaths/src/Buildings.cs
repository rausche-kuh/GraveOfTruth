using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// The old buildings beside a new harbour's road - huts, sheds, storehouses: building
    /// blueprints of the land's biome (<see cref="Blueprints"/>), a different one each while there
    /// are, raised and weathered by <see cref="Builder"/>. Each stands just off the road's levelled
    /// edge, its front to the road, somewhere along the first 40 m inland of the dock, on either
    /// side: where its ground is dry, no steeper than <see cref="MaxRise"/> across, clear of
    /// structures, locations and the road itself. Its floor is at the highest ground under it, and
    /// its piles reach down to the rest. Trees and rocks on its footprint are cleared, now and when
    /// its zone is generated later (<see cref="Clearing"/>). Server only.
    /// </summary>
    internal static class Buildings
    {
        /// <summary>Along the road, from the dock's land end: where the first building may stand, the last, and the step between tries.</summary>
        private const float From = 4f;
        private const float To = 40f;
        private const float Step = 3f;
        /// <summary>The most the ground may rise across a building's footprint: its piles make up the rest.</summary>
        private const float MaxRise = 2.5f;
        /// <summary>How far above the water its ground has to be everywhere.</summary>
        private const float Dry = 1f;
        /// <summary>A structure this close to its footprint keeps it away.</summary>
        private const float Taken = 1.5f;
        /// <summary>Room between the road's levelled edge and the building's front.</summary>
        private const float Verge = 0.5f;
        /// <summary>How far round a building's pieces its trees are cleared.</summary>
        private const float ClearRadius = 1.5f;

        /// <summary>The buildings of a new harbour, as their objects.</summary>
        public static List<ZDOID> ForHarbour(Trail trail, Landings.Landing landing, float landEnd, Structures structures, int seed)
        {
            List<ZDOID> placed = new List<ZDOID>();
            int wanted = Docks.BuildingCount.Value;
            if (!Docks.Enabled.Value || wanted <= 0)
            {
                return placed;
            }
            System.Random rng = new System.Random(seed);
            Docks.Along(trail, landing, landEnd, out float _, out Vector2 seaward);
            Heightmap.Biome biome = Builder.LandBiome(trail.Points[landing.Shore], seaward);
            List<Blueprint> order = Blueprints.Shuffled(dock: false, biome, rng);
            if (order.Count == 0)
            {
                return placed;
            }
            int built = 0;
            List<string> names = new List<string>();
            for (float d = landEnd + From; d <= landEnd + To && built < wanted; d += Step)
            {
                int first = rng.Next(2) == 0 ? -1 : 1;
                for (int k = 0; k < 2 && built < wanted; k++)
                {
                    Blueprint blueprint = order[built % order.Count];
                    Builder.Result result = TryAt(trail, landing, d, k == 0 ? first : -first, blueprint, biome, structures, rng, new Builder.Options());
                    if (result != null)
                    {
                        placed.AddRange(result.Placed);
                        names.Add(result.ToString());
                        built++;
                    }
                }
            }
            Debug.Log("[OdinsPaths] Harbour at " + trail.Points[landing.Shore].ToString("F0") + ": " + built + " buildings"
                + (names.Count > 0 ? " - " + string.Join("; ", names) : ""));
            return placed;
        }

        /// <summary>A building d metres inland along the road, on one side (1 right looking out to sea, -1 left), if it fits there.</summary>
        private static Builder.Result TryAt(Trail trail, Landings.Landing landing, float d, int side, Blueprint blueprint,
            Heightmap.Biome biome, Structures structures, System.Random rng, Builder.Options options)
        {
            Vector2 road = Docks.Along(trail, landing, d, out float _, out Vector2 seaward);
            Vector2 right = new Vector2(seaward.y, -seaward.x);
            Vector2 inward = right * side;
            Vector2 origin = road + inward * (trail.Kind.Reach + Verge);
            List<Builder.Part> parts = Builder.Resolve(blueprint, false);
            Builder.Frame frame = Builder.Frame.Make(origin, inward, 0f);
            if (!Ground(trail, parts, frame, structures, out float floor))
            {
                return null;
            }
            frame.Floor = floor;
            options.Biome = biome;
            options.Building = true;
            options.ChestChance = Docks.ChestChance.Value;
            options.EnemyChance = Docks.EnemyChance.Value;
            Builder.Result result = new Builder.Result();
            Builder.Raise(blueprint, parts, frame, options, rng, result);
            foreach (Vector2 point in result.Footprint)
            {
                structures?.Add(point);
            }
            Clearing.ClearAround(result.Footprint, ClearRadius);
            return result;
        }

        /// <summary>
        /// Whether the ground under a building's footprint (every metre of the box around its
        /// floors, walls and piles) will do, and the height its floor goes at: the highest of it.
        /// </summary>
        private static bool Ground(Trail trail, List<Builder.Part> parts, Builder.Frame frame, Structures structures, out float floor)
        {
            floor = 0f;
            Vector2 min = Vector2.one * float.MaxValue;
            Vector2 max = Vector2.one * float.MinValue;
            foreach (Builder.Part part in parts)
            {
                if (part.Stands || part.Role == Role.Wall || part.Role == Role.Pile)
                {
                    min = Vector2.Min(min, new Vector2(part.Min.x, part.Min.z));
                    max = Vector2.Max(max, new Vector2(part.Max.x, part.Max.z));
                }
            }
            if (min.x > max.x)
            {
                return false;
            }
            float water = ZoneSystem.instance.m_waterLevel;
            float low = float.MaxValue;
            float high = float.MinValue;
            int near = NearestPoint(trail, frame.Origin);
            for (float x = min.x; x <= max.x + 0.01f; x += Mathf.Min(1f, max.x - min.x + 0.01f))
            {
                for (float z = min.y; z <= max.y + 0.01f; z += Mathf.Min(1f, max.y - min.y + 0.01f))
                {
                    Vector2 at = frame.Flat(x, z);
                    float ground = OdinsPaths.Ground.Height(at.x, at.y);
                    low = Mathf.Min(low, ground);
                    high = Mathf.Max(high, ground);
                    if (ground < water + Dry || high - low > MaxRise
                        || (structures != null && structures.Distance(at, Taken) < Taken)
                        || OnRoad(trail, near, at) || Clearing.InLocation(at))
                    {
                        return false;
                    }
                }
            }
            floor = high + 0.05f;
            return true;
        }

        private static int NearestPoint(Trail trail, Vector2 at)
        {
            int best = 0;
            float nearest = float.MaxValue;
            for (int i = 0; i < trail.Points.Count; i++)
            {
                float d = (trail.Points[i] - at).sqrMagnitude;
                if (d < nearest)
                {
                    nearest = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Whether a point is on the road or its levelled edge, near the trail point given (within 60 m of it along the trail).</summary>
        private static bool OnRoad(Trail trail, int near, Vector2 at)
        {
            for (int s = Mathf.Max(0, near - 30); s < Mathf.Min(trail.Points.Count - 1, near + 30); s++)
            {
                if (trail.Nearest(at, s, out float _) < trail.Kind.Reach)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>For the dev command: a building exactly here, its front at the origin, facing inward, on the floor height given.</summary>
        public static Builder.Result Here(Blueprint blueprint, Vector2 origin, Vector2 inward, float floor, Builder.Options options, int seed)
        {
            System.Random rng = new System.Random(seed);
            Builder.Frame frame = Builder.Frame.Make(origin, inward, floor);
            options.Biome = Builder.LandBiome(origin, -inward);
            options.Building = true;
            Builder.Result result = new Builder.Result();
            Builder.Raise(blueprint, Builder.Resolve(blueprint, options.Edit), frame, options, rng, result);
            return result;
        }
    }
}
