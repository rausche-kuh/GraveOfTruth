using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// The road network as laid (docs/network.md): every road with its kind and, at each of its
    /// points, what walking there from the hub costs; the instance pinned for each main location
    /// group; the points of interest that have a spur; the bases that have a road. World data,
    /// kept on one data ZDO of the mod's own far outside the world, which no client is ever sent
    /// and which saves with the world. Server only.
    ///
    /// The network is where a new road sets out from (<see cref="Starts"/>): every hub at cost 0,
    /// and a point every 20 m of the main roads at alpha x its cost from the hub - so a new road
    /// forks off an old one where that is cheaper than a road of its own, and alpha is how much a
    /// long walk from the hub counts against the fork.
    /// </summary>
    internal sealed class Network
    {
        /// <summary>The data ZDO's prefab: no such prefab exists, and none needs to - see <see cref="DataPosition"/>.</summary>
        private const string DataPrefab = "OdinsPaths_Network";
        private static readonly int DataKey = "OdinsPaths_NetworkData".GetStableHashCode();
        /// <summary>
        /// Inside the ZDO sector grid (±16 km) and 11 km past the world's edge: no player comes near,
        /// so no client is sent it and <c>ZNetScene</c> never tries to build (and then destroy) the
        /// unknown prefab. Loading logs one "ZDOs with unknown prefabs" warning and keeps it.
        /// </summary>
        private static readonly Vector3 DataPosition = new Vector3(-15000f, 0f, -15000f);
        private const int FormatVersion = 1;
        /// <summary>A road keeps a point every this many metres of its trail.</summary>
        public const float PointSpacing = 8f;
        /// <summary>A new main road may set out from a point every this many metres of the old ones.</summary>
        public const float StartSpacing = 20f;

        internal sealed class Road
        {
            public RoadKind Kind;
            /// <summary>What it leads to: a location prefab name, "base", or a dev lay's target.</summary>
            public string Target;
            public Vector2 Goal;
            public readonly List<Vector2> Points = new List<Vector2>();
            /// <summary>Per point, the cost of walking there from the hub, in metres of easy walking.</summary>
            public readonly List<float> Costs = new List<float>();

            /// <summary>
            /// For a road searched toward its hub (a base's, from the network): each point's cost
            /// becomes what walking to it from the far end costs, the end itself 0.
            /// </summary>
            public void MeasureFromEnd()
            {
                float end = Costs[Costs.Count - 1];
                for (int i = 0; i < Costs.Count; i++)
                {
                    Costs[i] = end - Costs[i];
                }
            }

            public float Length
            {
                get
                {
                    float length = 0f;
                    for (int i = 1; i < Points.Count; i++)
                    {
                        length += Vector2.Distance(Points[i - 1], Points[i]);
                    }
                    return length;
                }
            }
        }

        public readonly List<Road> Roads = new List<Road>();
        /// <summary>Main location group (a location prefab name) -> the instance its road leads to.</summary>
        public readonly Dictionary<string, Vector2> Pinned = new Dictionary<string, Vector2>();
        /// <summary>The points of interest (location instance positions) that have a spur.</summary>
        public readonly List<Vector2> Connected = new List<Vector2>();
        /// <summary>The bases (their ward's position) that have a road.</summary>
        public readonly List<Vector2> Bases = new List<Vector2>();
        /// <summary>Groups and bases no road could reach; the planner skips them.</summary>
        public readonly HashSet<string> Unreachable = new HashSet<string>();

        private static Network current;
        private static ZDOMan currentFor;

        /// <summary>The world's network, read from its data ZDO the first time; null off the server.</summary>
        public static Network Current
        {
            get
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
                {
                    return null;
                }
                if (current == null || currentFor != ZDOMan.instance)
                {
                    currentFor = ZDOMan.instance;
                    current = new Network();
                    ZDO zdo = FindData();
                    byte[] data = zdo != null ? zdo.GetByteArray(DataKey) : null;
                    if (data != null && !current.Decode(data))
                    {
                        Debug.LogWarning("[OdinsPaths] The network data is in an unknown shape; starting a new network.");
                        current = new Network();
                    }
                }
                return current;
            }
        }

        /// <summary>Writes the network onto its data ZDO, making the ZDO the first time.</summary>
        public void Save()
        {
            ZDO zdo = FindData();
            if (zdo == null)
            {
                int hash = DataPrefab.GetStableHashCode();
                zdo = ZDOMan.instance.CreateNewZDO(DataPosition, hash);
                zdo.Persistent = true;
                zdo.SetPrefab(hash);
            }
            zdo.Set(DataKey, Encode());
        }

        /// <summary>
        /// Where a new road of this kind may set out from, besides its own hubs: for a main road,
        /// a point every <see cref="StartSpacing"/> of every main road at alpha x its cost from
        /// the hub; for a spur, the same points at no cost - a side path starts wherever is nearest.
        /// </summary>
        public List<Start> Starts(RoadKind kind, float alpha)
        {
            List<Start> starts = new List<Start>();
            foreach (Road road in Roads)
            {
                if (road.Kind != RoadKind.Main)
                {
                    continue;
                }
                float since = StartSpacing;
                for (int i = 0; i < road.Points.Count; i++)
                {
                    if (i > 0)
                    {
                        since += Vector2.Distance(road.Points[i - 1], road.Points[i]);
                    }
                    if (since < StartSpacing && i < road.Points.Count - 1)
                    {
                        continue;
                    }
                    since = 0f;
                    float hubCost = road.Costs[i];
                    starts.Add(new Start(road.Points[i], kind == RoadKind.Main ? alpha * hubCost : 0f, hubCost));
                }
            }
            return starts;
        }

        /// <summary>
        /// A laid trail as a road of the network: a point every <see cref="PointSpacing"/>, each
        /// with its cost from the hub - the cost its start carried from the hub, plus the search's
        /// cost along the route to the matching place, by the share of the length walked.
        /// </summary>
        public static Road FromLay(Trail trail, List<Vector2> route, List<float> costs, Start origin, Vector2 goal, string target)
        {
            Road road = new Road { Kind = trail.Kind, Target = target, Goal = goal };
            float[] along = new float[route.Count];
            for (int i = 1; i < route.Count; i++)
            {
                along[i] = along[i - 1] + Vector2.Distance(route[i - 1], route[i]);
            }
            float routeLength = along[route.Count - 1];
            int every = Mathf.Max(1, Mathf.RoundToInt(PointSpacing / Trail.Spacing));
            int last = trail.Points.Count - 1;
            int segment = 0;
            for (int i = 0; i <= last; i = i < last && i + every > last ? last : i + every)
            {
                float at = last > 0 ? routeLength * i / last : 0f;
                while (segment < route.Count - 2 && along[segment + 1] < at)
                {
                    segment++;
                }
                float cost = costs[0];
                if (route.Count > 1)
                {
                    float span = along[segment + 1] - along[segment];
                    float t = span > 0f ? Mathf.Clamp01((at - along[segment]) / span) : 0f;
                    cost = Mathf.Lerp(costs[segment], costs[segment + 1], t);
                }
                road.Points.Add(trail.Points[i]);
                road.Costs.Add(origin.HubCost + cost);
                if (i == last)
                {
                    break;
                }
            }
            return road;
        }

        /// <summary>A separate network with the same contents, to plan on without changing this one.</summary>
        public Network Copy()
        {
            Network copy = new Network();
            copy.Decode(Encode());
            return copy;
        }

        public void Clear()
        {
            Roads.Clear();
            Pinned.Clear();
            Connected.Clear();
            Bases.Clear();
            Unreachable.Clear();
        }

        /// <summary>Takes a road out again, with the pin or the base it made; the terrain keeps it.</summary>
        public bool Remove(Road road)
        {
            if (!Roads.Remove(road))
            {
                return false;
            }
            if (road.Target != null && Pinned.TryGetValue(road.Target, out Vector2 pinned) && pinned == road.Goal)
            {
                Pinned.Remove(road.Target);
            }
            Bases.Remove(road.Goal);
            return true;
        }

        /// <summary>Whether a base within this radius of the ward already has a road.</summary>
        public bool HasBase(Vector2 ward, float radius)
        {
            return Bases.Exists(b => (b - ward).sqrMagnitude < radius * radius);
        }

        private static ZDO FindData()
        {
            int hash = DataPrefab.GetStableHashCode();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                if (zdo.GetPrefab() == hash)
                {
                    return zdo;
                }
            }
            return null;
        }

        private byte[] Encode()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(FormatVersion);
            pkg.Write(Roads.Count);
            foreach (Road road in Roads)
            {
                pkg.Write(road.Kind.Id);
                pkg.Write(road.Target ?? "");
                Write(pkg, road.Goal);
                pkg.Write(road.Points.Count);
                for (int i = 0; i < road.Points.Count; i++)
                {
                    Write(pkg, road.Points[i]);
                    pkg.Write(road.Costs[i]);
                }
            }
            pkg.Write(Pinned.Count);
            foreach (KeyValuePair<string, Vector2> pin in Pinned)
            {
                pkg.Write(pin.Key);
                Write(pkg, pin.Value);
            }
            WriteList(pkg, Connected);
            WriteList(pkg, Bases);
            pkg.Write(Unreachable.Count);
            foreach (string name in Unreachable)
            {
                pkg.Write(name);
            }
            return Utils.Compress(pkg.GetArray());
        }

        private bool Decode(byte[] bytes)
        {
            ZPackage pkg = new ZPackage(Utils.Decompress(bytes));
            if (pkg.ReadInt() != FormatVersion)
            {
                return false;
            }
            int roads = pkg.ReadInt();
            for (int r = 0; r < roads; r++)
            {
                Road road = new Road
                {
                    Kind = RoadKind.ById(pkg.ReadByte()),
                    Target = pkg.ReadString(),
                    Goal = ReadVector2(pkg),
                };
                int points = pkg.ReadInt();
                for (int i = 0; i < points; i++)
                {
                    road.Points.Add(ReadVector2(pkg));
                    road.Costs.Add(pkg.ReadSingle());
                }
                Roads.Add(road);
            }
            int pins = pkg.ReadInt();
            for (int p = 0; p < pins; p++)
            {
                string group = pkg.ReadString();
                Pinned[group] = ReadVector2(pkg);
            }
            ReadList(pkg, Connected);
            ReadList(pkg, Bases);
            int unreachable = pkg.ReadInt();
            for (int u = 0; u < unreachable; u++)
            {
                Unreachable.Add(pkg.ReadString());
            }
            return true;
        }

        private static void Write(ZPackage pkg, Vector2 v)
        {
            pkg.Write(v.x);
            pkg.Write(v.y);
        }

        private static Vector2 ReadVector2(ZPackage pkg) => new Vector2(pkg.ReadSingle(), pkg.ReadSingle());

        private static void WriteList(ZPackage pkg, List<Vector2> list)
        {
            pkg.Write(list.Count);
            foreach (Vector2 v in list)
            {
                Write(pkg, v);
            }
        }

        private static void ReadList(ZPackage pkg, List<Vector2> list)
        {
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadVector2(pkg));
            }
        }
    }
}
