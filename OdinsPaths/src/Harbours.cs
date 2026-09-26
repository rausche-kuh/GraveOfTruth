using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// The main network's harbours: where a main road leaves the land for the sea and where it
    /// comes ashore again stands a vegvisir with blue runes, and using it pins every harbour
    /// across the water from it - the one on the far shore of its crossing, and more where
    /// several roads set out from the same shore (a harbour within <see cref="Merge"/> of a new
    /// landing is that landing's harbour, and gains a link) - and the stone's own harbour, if the
    /// player has no pin there yet.
    ///
    /// Vanilla vegvisirs are no network objects, only children of the locations every client
    /// builds itself, so the stone is a prefab of the mod's own (<see cref="PrefabName"/>): the
    /// start temple's vegvisir, copied with a <c>ZNetView</c> and blue runes, registered on the
    /// server and on every client that has the mod. A client without the mod sees no stone (and
    /// logs "Missing prefab hash" near one); only the server removes a ZDO of an unknown prefab.
    ///
    /// Using the stone runs the vanilla path: the client asks the server for the closest
    /// <see cref="LocationName"/>, which no location is called, and the server's
    /// <c>RPC_DiscoverClosestLocation</c> prefix (<see cref="OdinsPathsPlugin.RevealPinned"/>)
    /// answers with the harbours linked to the stone at that point (<see cref="Answer"/>). The
    /// links are kept on the stone's own ZDO, as the positions of the stones across.
    /// </summary>
    internal static class Harbours
    {
        public const string PrefabName = "OdinsPaths_Harbour";
        public static readonly int Hash = PrefabName.GetStableHashCode();
        /// <summary>What the stone asks the server for; no location has this name.</summary>
        public const string LocationName = "OdinsPaths_Harbour";
        /// <summary>The location whose vegvisir the stone is copied from: unique, always there, and near every spawn.</summary>
        private const string SourceLocation = "StartTemple";
        private static readonly int LinksKey = "OdinsPaths_HarbourLinks".GetStableHashCode();
        /// <summary>A landing this close to a harbour is that harbour: roads leaving one shore for different islands share their stone.</summary>
        private const float Merge = 25f;
        /// <summary>A structure this close to a landing's shore point stands on it; the stone goes a little inland instead.</summary>
        private const float Taken = 4f;
        /// <summary>How many trail points (2 m each) inland a stone may move off a built-on shore point.</summary>
        private const int Inland = 5;
        /// <summary>A stone this close to the point a client asked from is the one it used.</summary>
        private const float Asked = 3f;
        /// <summary>
        /// The runes' emission, blue where the vanilla vegvisir's is (1, 0, 0). Its shader
        /// (Custom/StaticRock) reads <c>_EmissionColor</c>; the material also keeps a stale
        /// <c>_EmissiveColor</c> that the shader has no property for.
        /// </summary>
        private static readonly Color Runes = new Color(0.1f, 0.45f, 1f);
        /// <summary>The point light's colour, blue where the vanilla one's is (1, 0.37, 0.37).</summary>
        private static readonly Color Glow = new Color(0.37f, 0.6f, 1f);
        private const string PinName = "Harbour";
        private const Minimap.PinType PinType = Minimap.PinType.Icon4;

        private static GameObject prefab;
        private static bool failed;

        /// <summary>
        /// Adds the stone to this scene's <c>ZNetScene</c>, building it the first time. Runs
        /// after <c>ZoneSystem.Start</c> has set the locations up, before any object is created.
        /// </summary>
        public static void Register()
        {
            if (ZNetScene.instance == null || ZoneSystem.instance == null)
            {
                return;
            }
            if (prefab == null && !failed)
            {
                prefab = Build();
                failed = prefab == null;
            }
            if (prefab != null)
            {
                ZNetScene.instance.m_namedPrefabs[Hash] = prefab;
            }
        }

        /// <summary>
        /// The start temple's vegvisir, copied under an inactive holder that outlives the scene
        /// (so a copy's <c>Awake</c> runs only once the game instantiates it), with a
        /// <c>ZNetView</c>, the question for <see cref="LocationName"/> and blue runes. The temple
        /// is loaded through its <c>SoftReference</c> (by reflection, as in <see cref="Footprints"/>)
        /// and never released: the copy shares its meshes and textures.
        /// </summary>
        private static GameObject Build()
        {
            ZoneSystem.ZoneLocation source = ZoneSystem.instance.m_locations.Find(l => l.m_prefabName == SourceLocation);
            object reference = source != null ? typeof(ZoneSystem.ZoneLocation).GetField("m_prefab")?.GetValue(source) : null;
            System.Type type = reference?.GetType();
            System.Reflection.PropertyInfo valid = type?.GetProperty("IsValid");
            System.Reflection.PropertyInfo asset = type?.GetProperty("Asset");
            System.Reflection.MethodInfo load = type?.GetMethod("Load", System.Type.EmptyTypes);
            if (valid == null || asset == null || load == null || !(bool)valid.GetValue(reference))
            {
                Debug.LogWarning("[OdinsPaths] No " + SourceLocation + " location to copy a vegvisir from - harbours get no stone.");
                return null;
            }
            load.Invoke(reference, null);
            Vegvisir original = (asset.GetValue(reference) as GameObject)?.GetComponentInChildren<Vegvisir>(true);
            if (original == null)
            {
                Debug.LogWarning("[OdinsPaths] " + SourceLocation + " holds no vegvisir - harbours get no stone.");
                return null;
            }
            GameObject holder = new GameObject("OdinsPaths_Prefabs");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            GameObject stone = Object.Instantiate(original.gameObject, holder.transform);
            stone.name = PrefabName;
            stone.transform.localPosition = Vector3.zero;
            stone.transform.localRotation = Quaternion.identity;
            stone.SetActive(true);

            ZNetView view = stone.AddComponent<ZNetView>();
            view.m_persistent = true;
            view.m_type = ZDO.ObjectType.Default;

            Vegvisir vegvisir = stone.GetComponent<Vegvisir>();
            vegvisir.m_hoverName = PinName;
            vegvisir.m_setsGlobalKey = "";
            vegvisir.m_setsPlayerKey = "";
            vegvisir.m_locations = new List<Vegvisir.VegvisrLocation>
            {
                new Vegvisir.VegvisrLocation { m_locationName = LocationName, m_pinName = PinName, m_pinType = PinType, m_showMap = true },
            };

            int tinted = Tint(stone);
            if (tinted == 0)
            {
                Debug.LogWarning("[OdinsPaths] The vegvisir's material has no _EmissionColor - the harbour stones' runes stay red.");
            }
            return stone;
        }

        /// <summary>The runes' emission and the light turned blue, on copies of the materials; how many materials were.</summary>
        private static int Tint(GameObject stone)
        {
            Dictionary<Material, Material> blue = new Dictionary<Material, Material>();
            foreach (Renderer renderer in stone.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || !material.HasProperty("_EmissionColor"))
                    {
                        continue;
                    }
                    if (!blue.TryGetValue(material, out Material copy))
                    {
                        copy = new Material(material) { name = material.name + " (harbour)" };
                        copy.SetColor("_EmissionColor", Runes);
                        blue[material] = copy;
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
            foreach (Light light in stone.GetComponentsInChildren<Light>(true))
            {
                light.color = Glow;
            }
            return blue.Count;
        }

        /// <summary>
        /// A stone at each landing of a main road's trail, the two shores of each crossing linked
        /// both ways; a landing near a harbour already there joins it. The stones placed, as ZDOs.
        /// Server only.
        /// </summary>
        public static List<ZDOID> Place(Trail trail, List<Landings.Landing> landings, Structures structures)
        {
            List<ZDOID> placed = new List<ZDOID>();
            if (landings.Count == 0)
            {
                return placed;
            }
            GameObject stone = ZNetScene.instance.GetPrefab(Hash);
            if (stone == null)
            {
                Debug.LogWarning("[OdinsPaths] No harbour stone prefab - landings left unmarked.");
                return placed;
            }
            List<ZDO> stones = Stones();
            for (int i = 0; i < landings.Count; i++)
            {
                ZDO here = Harbour(trail, landings[i], stone, stones, structures, placed);
                // The shore across is the next landing of the same crossing; a trail that ends
                // or starts in the water has a crossing with one shore.
                if (i + 1 < landings.Count && landings[i + 1].Crossing == landings[i].Crossing)
                {
                    ZDO there = Harbour(trail, landings[i + 1], stone, stones, structures, placed);
                    Link(here, there);
                    Link(there, here);
                    i++;
                }
            }
            return placed;
        }

        /// <summary>The harbour a landing belongs to: the stone within <see cref="Merge"/>, or a new one on its shore.</summary>
        private static ZDO Harbour(Trail trail, Landings.Landing landing, GameObject stone, List<ZDO> stones, Structures structures, List<ZDOID> placed)
        {
            Vector2 shore = trail.Points[landing.Shore];
            ZDO nearest = null;
            float best = Merge;
            foreach (ZDO zdo in stones)
            {
                float distance = Vector2.Distance(Flat(zdo.GetPosition()), shore);
                if (distance < best)
                {
                    best = distance;
                    nearest = zdo;
                }
            }
            if (nearest != null)
            {
                return nearest;
            }
            // Off a built-on shore point (a player's dock), a few metres up the trail, away from the water.
            int step = landing.Shore < landing.Toward ? -1 : 1;
            int at = landing.Shore;
            for (int i = 1; i <= Inland && structures.Distance(trail.Points[at], Taken) < Taken; i++)
            {
                int next = landing.Shore + i * step;
                if (next < 0 || next >= trail.Points.Count || trail.Water[next])
                {
                    break;
                }
                at = next;
            }
            Vector2 point = trail.Points[at];
            // Its face to the road, its back to the water: read on the way to the boat, and seen
            // from the boat coming in. Verify in game which face carries the runes.
            Vector2 inland = point - trail.Points[landing.Toward];
            Quaternion rotation = inland.sqrMagnitude > 0f
                ? Quaternion.LookRotation(new Vector3(inland.x, 0f, inland.y)) : Quaternion.identity;
            ZDO spawned = Landings.Spawn(stone, new Vector3(point.x, Landings.Height(trail, at), point.y), rotation);
            stones.Add(spawned);
            structures.Add(point);
            placed.Add(spawned.m_uid);
            return spawned;
        }

        /// <summary>Adds the harbour at to's position to from's links, once.</summary>
        private static void Link(ZDO from, ZDO to)
        {
            if (from == to)
            {
                return;
            }
            List<Vector2> links = Links(from);
            Vector2 there = Flat(to.GetPosition());
            if (links.Exists(l => (l - there).sqrMagnitude < 1f))
            {
                return;
            }
            links.Add(there);
            ZPackage pkg = new ZPackage();
            pkg.Write(links.Count);
            foreach (Vector2 link in links)
            {
                pkg.Write(link.x);
                pkg.Write(link.y);
            }
            // A client near the stone may own it; the server takes it back to write.
            from.SetOwner(ZDOMan.GetSessionID());
            from.Set(LinksKey, pkg.GetArray());
        }

        private static List<Vector2> Links(ZDO zdo)
        {
            List<Vector2> links = new List<Vector2>();
            byte[] data = zdo.GetByteArray(LinksKey);
            if (data == null)
            {
                return links;
            }
            ZPackage pkg = new ZPackage(data);
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                links.Add(new Vector2(pkg.ReadSingle(), pkg.ReadSingle()));
            }
            return links;
        }

        /// <summary>
        /// The server's answer to a stone: a pin at the stone itself, then one at every harbour
        /// linked to the stone nearest the point asked from, whose stone still stands (a dev undo
        /// takes stones away, not the links to them). Whether the question was a harbour's at all.
        /// The stone's own pin goes without showMap, so the client adds it silently only when it
        /// has none there yet (<c>Minimap.DiscoverLocation</c>), and first, so the player ends up
        /// looking across the water, not at the stone.
        /// </summary>
        public static bool Answer(long sender, string name, Vector3 point, bool showMap)
        {
            if (name != LocationName)
            {
                return false;
            }
            List<ZDO> stones = Stones();
            ZDO asked = null;
            float best = Asked;
            foreach (ZDO zdo in stones)
            {
                float distance = Vector2.Distance(Flat(zdo.GetPosition()), Flat(point));
                if (distance < best)
                {
                    best = distance;
                    asked = zdo;
                }
            }
            if (asked == null)
            {
                Debug.LogWarning("[OdinsPaths] A harbour stone at " + Flat(point) + " asked, but none stands there.");
                return true;
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RPC_DiscoverLocationResponse", PinName, (int)PinType, asked.GetPosition(), false);
            int answered = 0;
            foreach (Vector2 link in Links(asked))
            {
                ZDO across = stones.Find(s => (Flat(s.GetPosition()) - link).sqrMagnitude < 1f);
                if (across == null)
                {
                    continue;
                }
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RPC_DiscoverLocationResponse", PinName, (int)PinType, across.GetPosition(), showMap);
                answered++;
            }
            if (answered == 0)
            {
                Debug.LogWarning("[OdinsPaths] The harbour stone at " + Flat(point) + " has no harbour across standing.");
            }
            return true;
        }

        /// <summary>Every harbour stone in the world. Rare - a lay's landings, a stone used -, so a walk over every ZDO.</summary>
        private static List<ZDO> Stones()
        {
            List<ZDO> stones = new List<ZDO>();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                if (zdo.GetPrefab() == Hash)
                {
                    stones.Add(zdo);
                }
            }
            return stones;
        }

        private static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
    }

    public partial class OdinsPathsPlugin
    {
        /// <summary>The harbour stone joins the scene's prefabs, on the server and on every client with the mod.</summary>
        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        public static class RegisterHarbourStone
        {
            private static void Postfix()
            {
                Harbours.Register();
            }
        }
    }
}
