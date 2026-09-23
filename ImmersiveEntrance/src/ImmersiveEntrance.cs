using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ImmersiveEntrance
{
    /// <summary>
    /// The black surface in a dungeon entrance becomes a window into the dungeon. Every interior
    /// already exists while you stand at its door - the game spawns it 5000 units straight up the
    /// moment the location loads - so all this does is point a second camera at the interior door,
    /// posed the way the player's camera stands to the entrance, and paint what it sees onto the
    /// doorway.
    ///
    /// Nothing is networked and nothing is written: the portal is purely what this client draws.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public partial class ImmersiveEntrancePlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.immersiveentrance";
        public const string NAME = "Immersive Entrance";
        public const string VERSION = "0.1.0";

        /// <summary>Anything above this is inside an interior - the game's own <c>Character.InInterior</c> line.</summary>
        internal const float InteriorHeight = 3000f;

        /// <summary>How often the entrances around the camera are looked for again.</summary>
        private const float ScanInterval = 1f;

        internal static ImmersiveEntrancePlugin Instance;
        internal static ManualLogSource Log;

        // ---- Config --------------------------------------------------------------------------

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> MaxDistance;
        internal static ConfigEntry<int> MaxPortals;
        internal static ConfigEntry<int> TextureSize;
        internal static ConfigEntry<bool> InteriorLights;

        /// <summary>The live portals, one per entrance close enough to the camera.</summary>
        private static readonly Dictionary<Teleport, Portal> portals = new Dictionary<Teleport, Portal>();

        /// <summary>
        /// Every teleport of every loaded location, kept by the <see cref="Location"/> patches
        /// below so the scan never has to search the scene. Both doors of an interior are
        /// children of the location by the end of its <c>Awake</c>.
        /// </summary>
        private static readonly List<Teleport> teleports = new List<Teleport>();

        private static readonly Plane[] frustum = new Plane[6];
        private static float nextScan;

        void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Whether dungeon entrances show the dungeon behind them.");
            MaxDistance = Config.Bind("General", "MaxDistance", 30f, new ConfigDescription(
                "How close, in metres, the camera has to be to an entrance before it opens up.",
                new AcceptableValueRange<float>(5f, 100f)));
            MaxPortals = Config.Bind("General", "MaxPortals", 1, new ConfigDescription(
                "How many entrances are drawn at once. Each one renders the dungeon behind it every frame.",
                new AcceptableValueRange<int>(1, 4)));
            TextureSize = Config.Bind("General", "TextureSize", 1024, new ConfigDescription(
                "The sharpness of the view through the door: the longer side of its texture, in pixels.",
                new AcceptableValueRange<int>(256, 2048)));
            InteriorLights = Config.Bind("General", "InteriorLights", true,
                "Whether torches and braziers inside are lit in the view through the door. The " +
                "game switches off every light far from the player, and the dungeon is far.");

            Config.SettingChanged += (sender, args) =>
            {
                if (args.ChangedSetting == Enabled)
                {
                    ClearPortals();
                }
            };

            Camera.onPreCull += OnPreCull;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }

        void OnDestroy()
        {
            Camera.onPreCull -= OnPreCull;
            ClearPortals();
        }

        void Update()
        {
            Camera main = Utils.GetMainCamera();
            if (!Enabled.Value || main == null || Player.m_localPlayer == null ||
                main.transform.position.y > InteriorHeight)
            {
                ClearPortals();
                return;
            }
            if (Time.time < nextScan)
            {
                return;
            }
            nextScan = Time.time + ScanInterval;
            Scan(main.transform.position);
        }

        /// <summary>
        /// Keeps a portal on the nearest few exterior entrances within reach and drops the rest.
        /// An exterior entrance is a <see cref="Teleport"/> below the interior line whose target
        /// is above it.
        /// </summary>
        private static void Scan(Vector3 from)
        {
            float reach = MaxDistance.Value;
            teleports.RemoveAll(t => t == null);
            var near = new List<KeyValuePair<float, Teleport>>();
            foreach (Teleport teleport in teleports)
            {
                if (!IsEntrance(teleport))
                {
                    continue;
                }
                float distance = Vector3.Distance(from, teleport.transform.position);
                if (distance <= reach)
                {
                    near.Add(new KeyValuePair<float, Teleport>(distance, teleport));
                }
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));

            var keep = new HashSet<Teleport>();
            for (int i = 0; i < near.Count && i < MaxPortals.Value; i++)
            {
                keep.Add(near[i].Value);
            }

            var drop = new List<Teleport>();
            foreach (KeyValuePair<Teleport, Portal> pair in portals)
            {
                if (!keep.Contains(pair.Key) || !pair.Value.Valid)
                {
                    drop.Add(pair.Key);
                }
            }
            foreach (Teleport teleport in drop)
            {
                portals[teleport].Dispose();
                portals.Remove(teleport);
            }

            foreach (Teleport teleport in keep)
            {
                if (teleport != null && !portals.ContainsKey(teleport))
                {
                    Portal portal = Portal.Create(teleport);
                    if (portal != null)
                    {
                        portals.Add(teleport, portal);
                    }
                }
            }
        }

        internal static bool IsEntrance(Teleport teleport)
        {
            return teleport != null && teleport.isActiveAndEnabled && teleport.m_targetPoint != null &&
                   teleport.transform.position.y < InteriorHeight &&
                   teleport.m_targetPoint.transform.position.y > InteriorHeight;
        }

        internal static void ClearPortals()
        {
            foreach (Portal portal in portals.Values)
            {
                portal.Dispose();
            }
            portals.Clear();
        }

        /// <summary>
        /// Portals are drawn just before the main camera culls: every LateUpdate has run by then,
        /// so the camera stands where it will render from, and the doorway quad can still be
        /// switched on or off for this very frame.
        /// </summary>
        private static void OnPreCull(Camera camera)
        {
            if (portals.Count == 0 || camera == null || camera != Utils.GetMainCamera())
            {
                return;
            }
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            foreach (Portal portal in portals.Values)
            {
                portal.Render(camera, frustum);
            }
        }

        /// <summary>Everything the dev commands need to see, without exposing the dictionary.</summary>
        internal static IEnumerable<Portal> Portals => portals.Values;

        /// <summary>The known teleports, for the dev commands.</summary>
        internal static IEnumerable<Teleport> Teleports => teleports;

        /// <summary>
        /// A location's doors, registered as it wakes. The interior - and with it the exit door -
        /// is instantiated inside <c>Location.Awake</c> as a child of the location, so a postfix
        /// sees both doors of a dungeon.
        /// </summary>
        [HarmonyPatch(typeof(Location), "Awake")]
        private static class LocationAwake
        {
            private static void Postfix(Location __instance)
            {
                foreach (Teleport teleport in __instance.GetComponentsInChildren<Teleport>(true))
                {
                    if (!teleports.Contains(teleport))
                    {
                        teleports.Add(teleport);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Location), "OnDestroy")]
        private static class LocationDestroyed
        {
            private static void Postfix(Location __instance)
            {
                foreach (Teleport teleport in __instance.GetComponentsInChildren<Teleport>(true))
                {
                    teleports.Remove(teleport);
                }
                teleports.RemoveAll(t => t == null);
            }
        }

        /// <summary>
        /// A dungeon's rooms arrive after its location: the generator loads their prefabs
        /// asynchronously and places them in <c>Spawn</c>. A portal made in between has nothing
        /// to show and no torches to find, so it is told the moment they exist.
        /// </summary>
        [HarmonyPatch(typeof(DungeonGenerator), "Spawn")]
        private static class RoomsSpawned
        {
            private static void Postfix(DungeonGenerator __instance)
            {
                foreach (Portal portal in portals.Values)
                {
                    portal.OnRoomsSpawned(__instance);
                }
            }
        }
    }
}
