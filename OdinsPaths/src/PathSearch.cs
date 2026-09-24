using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>A location's footprint the search keeps out of, and levelling leaves alone.</summary>
    internal struct Circle
    {
        public Vector2 Center;
        public float Radius;

        public bool Contains(Vector2 p) => (p - Center).sqrMagnitude < Radius * Radius;
    }

    /// <summary>
    /// The least-cost route from the nearest of several starts to a goal, over a grid of 4 m cells
    /// with heights from the world generator - so it works on zones nobody has loaded. The cost of
    /// a step is its length times how hard it is: climbing gets expensive fast, fords cost a
    /// little, the open sea costs a lump sum to set out on, bogs and the Mistlands are avoided,
    /// locations and buildings are walked around, and a slow noise makes flat ground wander instead of running
    /// straight. The search is A* with 16 neighbours (the 8 plus the knight's moves), so a straight
    /// stretch can run at any of 16 angles instead of zig-zagging at 45°.
    ///
    /// <see cref="Run"/> is a coroutine: the world generator is not thread safe (its river cache
    /// is shared with the game's heightmap thread), so the work is spread over frames instead.
    /// </summary>
    internal sealed class PathSearch
    {
        public const float CellSize = 4f;

        /// <summary>A grade (rise / run) that costs double. The cost grows with its square.</summary>
        private const float ComfortGrade = 0.15f;
        /// <summary>Above this grade (about 31°) a step costs twenty times more again.</summary>
        private const float MaxGrade = 0.6f;
        /// <summary>Water up to this deep is a ford.</summary>
        internal const float FordDepth = 1.5f;
        private const float FordFactor = 3f;
        /// <summary>Crossing deep water, per metre.</summary>
        private const float SeaFactor = 2f;
        /// <summary>What setting out on deep water costs, in metres of flat walking.</summary>
        private const float LandfallCost = 150f;
        private const float SwampFactor = 1.5f;
        private const float MistlandsFactor = 4f;
        private const float DeepNorthFactor = 1.2f;
        private const float LocationFactor = 10f;
        /// <summary>
        /// A cell near a building. High rather than impassable: a start inside a base has to get
        /// out, and the terrain writer leaves the ground beside the pieces alone anyway.
        /// </summary>
        private const float StructureFactor = 40f;
        /// <summary>How much the slow noise bends flat ground: 0.3 = each cell costs 0.7 to 1.3.</summary>
        private const float WanderStrength = 0.3f;
        private const float WanderScale = 150f;
        /// <summary>The search stays inside an ellipse around each start and the goal.</summary>
        private const float EllipseStretch = 1.4f;
        private const float EllipseMargin = 300f;
        private const int MaxExpanded = 3000000;

        private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1, 1, 1, -1, -1, 2, 2, -2, -2 };
        private static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1, 2, -2, 2, -2, 1, -1, 1, -1 };

        private struct Cell
        {
            /// <summary>Generated ground height; under water it is the sea floor.</summary>
            public float Height;
            /// <summary>Biome, wander and location multiplier; 0 = impassable.</summary>
            public float Factor;
        }

        private readonly List<Vector2> starts;
        private readonly Vector2 goal;
        private readonly List<Circle> locations;
        private readonly Structures structures;
        /// <summary>A cell centre this close to a structure is near it; see the constructor.</summary>
        private readonly float structureRadius;
        private readonly float waterLevel;
        private readonly Vector2 wanderOffset;
        private readonly float[] ellipseLimit;

        private readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
        private readonly Dictionary<long, float> cost = new Dictionary<long, float>();
        private readonly Dictionary<long, long> parent = new Dictionary<long, long>();
        private readonly HashSet<long> closed = new HashSet<long>();
        private readonly MinHeap open = new MinHeap();

        /// <summary>The route, start to goal, as cell centres; null until found or on failure.</summary>
        public List<Vector2> Result { get; private set; }
        public string Failure { get; private set; }
        public int Expanded { get; private set; }
        public int Sampled => cells.Count;
        public double Milliseconds { get; private set; }

        public PathSearch(List<Vector2> starts, Vector2 goal, List<Circle> locations, Structures structures)
        {
            this.starts = starts;
            this.goal = goal;
            this.locations = locations;
            this.structures = structures;
            // The trail runs along the steps between cell centres, and the longest step (a
            // knight's move) passes half its length closer to a point than its ends do.
            float keep = TerrainWriter.Reach + Structures.PieceReach;
            float halfStep = CellSize * Mathf.Sqrt(5f) * 0.5f;
            structureRadius = Mathf.Sqrt(keep * keep + halfStep * halfStep);
            waterLevel = ZoneSystem.instance.m_waterLevel;
            System.Random random = new System.Random(WorldGenerator.instance.GetSeed());
            wanderOffset = new Vector2(random.Next(1000, 9000), random.Next(1000, 9000));
            ellipseLimit = new float[starts.Count];
            for (int i = 0; i < starts.Count; i++)
            {
                ellipseLimit[i] = EllipseLimit(starts[i], goal);
            }
        }

        /// <summary>A cell is searched if its distances to this start and the goal add up to no more.</summary>
        public static float EllipseLimit(Vector2 start, Vector2 goal)
        {
            return Vector2.Distance(start, goal) * EllipseStretch + EllipseMargin;
        }

        public IEnumerator Run(float budgetMs)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch frame = Stopwatch.StartNew();
            long goalKey = Key(Mathf.RoundToInt(goal.x / CellSize), Mathf.RoundToInt(goal.y / CellSize));
            foreach (Vector2 start in starts)
            {
                int i = Mathf.RoundToInt(start.x / CellSize);
                int j = Mathf.RoundToInt(start.y / CellSize);
                long key = Key(i, j);
                if (GetCell(i, j).Factor > 0f && !cost.ContainsKey(key))
                {
                    cost[key] = 0f;
                    open.Push(Heuristic(i, j), key);
                }
            }

            while (open.Count > 0)
            {
                if ((Expanded & 255) == 0 && frame.Elapsed.TotalMilliseconds > budgetMs)
                {
                    yield return null;
                    frame.Restart();
                }
                long current = open.Pop();
                if (!closed.Add(current))
                {
                    continue;
                }
                if (current == goalKey)
                {
                    Result = Rebuild(goalKey);
                    Milliseconds = total.Elapsed.TotalMilliseconds;
                    yield break;
                }
                if (++Expanded > MaxExpanded)
                {
                    break;
                }
                int ci = (int)(current >> 32);
                int cj = (int)(uint)current;
                Cell from = cells[current];
                float fromCost = cost[current];
                for (int d = 0; d < StepX.Length; d++)
                {
                    int ni = ci + StepX[d];
                    int nj = cj + StepY[d];
                    long next = Key(ni, nj);
                    if (closed.Contains(next) || !InBounds(ni, nj))
                    {
                        continue;
                    }
                    Cell to = GetCell(ni, nj);
                    if (to.Factor <= 0f && next != goalKey)
                    {
                        continue;
                    }
                    float length = CellSize * Mathf.Sqrt(StepX[d] * StepX[d] + StepY[d] * StepY[d]);
                    float reached = fromCost + StepCost(from, to, length);
                    if (cost.TryGetValue(next, out float known) && known <= reached)
                    {
                        continue;
                    }
                    cost[next] = reached;
                    parent[next] = current;
                    open.Push(reached + Heuristic(ni, nj), next);
                }
            }
            Milliseconds = total.Elapsed.TotalMilliseconds;
            Failure = Expanded > MaxExpanded
                ? "gave up after " + MaxExpanded + " cells"
                : "no way through - water, lava or the world's edge is in the way";
        }

        private float StepCost(Cell from, Cell to, float length)
        {
            // The water surface is flat: stepping into or out of the sea is not a climb.
            float fromTop = Mathf.Max(from.Height, waterLevel);
            float toTop = Mathf.Max(to.Height, waterLevel);
            float depth = waterLevel - to.Height;
            float factor;
            float extra = 0f;
            if (depth > FordDepth)
            {
                factor = SeaFactor;
                if (waterLevel - from.Height <= FordDepth)
                {
                    extra = LandfallCost;
                }
            }
            else if (depth > 0f)
            {
                factor = FordFactor;
            }
            else
            {
                float grade = Mathf.Abs(toTop - fromTop) / length;
                float ratio = grade / ComfortGrade;
                factor = 1f + ratio * ratio;
                if (grade > MaxGrade)
                {
                    factor *= 20f;
                }
            }
            return length * factor * (to.Factor > 0f ? to.Factor : 1f) + extra;
        }

        private float Heuristic(int i, int j)
        {
            return Vector2.Distance(new Vector2(i * CellSize, j * CellSize), goal);
        }

        private bool InBounds(int i, int j)
        {
            Vector2 p = new Vector2(i * CellSize, j * CellSize);
            float toGoal = Vector2.Distance(p, goal);
            for (int s = 0; s < starts.Count; s++)
            {
                if (Vector2.Distance(p, starts[s]) + toGoal <= ellipseLimit[s])
                {
                    return true;
                }
            }
            return false;
        }

        private Cell GetCell(int i, int j)
        {
            long key = Key(i, j);
            if (cells.TryGetValue(key, out Cell cell))
            {
                return cell;
            }
            float x = i * CellSize;
            float z = j * CellSize;
            WorldGenerator gen = WorldGenerator.instance;
            Heightmap.Biome biome = gen.GetBiome(x, z);
            cell.Height = gen.GetBiomeHeight(biome, x, z, out Color _);
            cell.Factor = BiomeFactor(biome);
            if (x * x + z * z > 9900f * 9900f)
            {
                cell.Factor = 0f;
            }
            if (cell.Factor > 0f)
            {
                float noise = Mathf.PerlinNoise(x / WanderScale + wanderOffset.x, z / WanderScale + wanderOffset.y);
                cell.Factor *= 1f + WanderStrength * (noise * 2f - 1f);
                Vector2 p = new Vector2(x, z);
                foreach (Circle location in locations)
                {
                    if (location.Contains(p))
                    {
                        cell.Factor *= LocationFactor;
                        break;
                    }
                }
                if (structures.Distance(p, structureRadius) < structureRadius)
                {
                    cell.Factor *= StructureFactor;
                }
            }
            cells[key] = cell;
            return cell;
        }

        private static float BiomeFactor(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.AshLands: return 0f;
                case Heightmap.Biome.Swamp: return SwampFactor;
                case Heightmap.Biome.Mistlands: return MistlandsFactor;
                case Heightmap.Biome.DeepNorth: return DeepNorthFactor;
                default: return 1f;
            }
        }

        private List<Vector2> Rebuild(long goalKey)
        {
            List<Vector2> route = new List<Vector2>();
            long key = goalKey;
            while (true)
            {
                route.Add(new Vector2((int)(key >> 32) * CellSize, (int)(uint)key * CellSize));
                if (!parent.TryGetValue(key, out long previous))
                {
                    break;
                }
                key = previous;
            }
            route.Reverse();
            return route;
        }

        private static long Key(int i, int j) => ((long)i << 32) | (uint)j;

        /// <summary>A plain binary min-heap; stale entries are skipped by the closed set.</summary>
        private sealed class MinHeap
        {
            private float[] keys = new float[1024];
            private long[] values = new long[1024];
            public int Count { get; private set; }

            public void Push(float key, long value)
            {
                if (Count == keys.Length)
                {
                    System.Array.Resize(ref keys, Count * 2);
                    System.Array.Resize(ref values, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int up = (i - 1) >> 1;
                    if (keys[up] <= key)
                    {
                        break;
                    }
                    keys[i] = keys[up];
                    values[i] = values[up];
                    i = up;
                }
                keys[i] = key;
                values[i] = value;
            }

            public long Pop()
            {
                long top = values[0];
                if (--Count > 0)
                {
                    float key = keys[Count];
                    long value = values[Count];
                    int i = 0;
                    while (true)
                    {
                        int child = 2 * i + 1;
                        if (child >= Count)
                        {
                            break;
                        }
                        if (child + 1 < Count && keys[child + 1] < keys[child])
                        {
                            child++;
                        }
                        if (keys[child] >= key)
                        {
                            break;
                        }
                        keys[i] = keys[child];
                        values[i] = values[child];
                        i = child;
                    }
                    keys[i] = key;
                    values[i] = value;
                }
                return top;
            }
        }
    }
}
