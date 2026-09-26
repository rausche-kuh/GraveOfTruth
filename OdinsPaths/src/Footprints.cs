using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// How far a location's buildings really reach. Its exterior radius is what the game clears
    /// and levels for it, and some reach far past that: the Mistlands' viaducts (8 m) and giant
    /// skeletons (10 m) stand 20 m and more out, and a road went through one (seen in game
    /// 2026-09-25), its clearing taking pieces of it as scenery. Measured once per location from
    /// its prefab's meshes as seen from above; never less than the exterior radius and never more
    /// than <see cref="Largest"/>. Main thread only.
    ///
    /// Loading a prefab took 20-640 ms (CharredFortress 516, TheHole01 635; 2026-09-26), and a
    /// road's first look at 57 of them stalled one frame for 5.9 s. So once the world is up
    /// <see cref="Warm"/> loads every location the world has instances of asynchronously, a few at
    /// a time, and a lay waits for it (<see cref="Wait"/>); the reaches are kept on disk per game
    /// version (<see cref="CacheFile"/>), so it happens once. A location it has not measured yet
    /// is still loaded on the spot.
    /// </summary>
    internal static class Footprints
    {
        /// <summary>No footprint is taken as wider than this: a stray water plane or backdrop mesh would close off a valley.</summary>
        private const float Largest = 48f;
        /// <summary>Meshes further above or below the location's origin than this are its interior (dungeons sit thousands of metres up), not its footprint.</summary>
        private const float Above = 60f;
        /// <summary>Prefabs loading at once in the warm-up.</summary>
        private const int InFlight = 4;
        /// <summary>Seconds a load may take before the warm-up gives up on it (and takes the exterior radius).</summary>
        private const float GiveUp = 20f;
        /// <summary>Raised whenever the measuring changes, so the reaches on disk are taken again.</summary>
        private const int Format = 1;

        /// <summary>The reach of each location prefab by name, unclamped.</summary>
        private static readonly Dictionary<string, float> measured = new Dictionary<string, float>();
        private static bool cacheRead;
        private static bool warming;
        private static ZoneSystem warmedFor;

        private static string CacheFile => Path.Combine(BepInEx.Paths.CachePath, "OdinsPaths.footprints.txt");

        /// <summary>The radius a road keeps out of around an instance of this location.</summary>
        public static float Radius(ZoneSystem.ZoneLocation location)
        {
            if (location == null)
            {
                return 0f;
            }
            ReadCache();
            if (!measured.TryGetValue(location.m_prefabName, out float reach))
            {
                reach = Measure(location);
                measured[location.m_prefabName] = reach;
            }
            return Mathf.Max(location.m_exteriorRadius, Mathf.Min(reach, Largest));
        }

        /// <summary>
        /// From the plugin's Update on the server: starts the warm-up once per world, as soon as
        /// its locations are known.
        /// </summary>
        public static void Tick()
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null || warmedFor == zones || !zones.LocationsGenerated)
            {
                return;
            }
            Begin();
        }

        /// <summary>Until every location of the world is measured: a lay yields to this before it looks at any.</summary>
        public static IEnumerator Wait()
        {
            if (warmedFor != ZoneSystem.instance)
            {
                Begin();
            }
            while (warming)
            {
                yield return null;
            }
        }

        private static void Begin()
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (warming || zones == null || OdinsPathsPlugin.Instance == null)
            {
                return;
            }
            warmedFor = zones;
            ReadCache();
            List<ZoneSystem.ZoneLocation> due = new List<ZoneSystem.ZoneLocation>();
            HashSet<string> seen = new HashSet<string>();
            foreach (ZoneSystem.LocationInstance instance in zones.m_locationInstances.Values)
            {
                ZoneSystem.ZoneLocation location = instance.m_location;
                if (location != null && !measured.ContainsKey(location.m_prefabName) && seen.Add(location.m_prefabName))
                {
                    due.Add(location);
                }
            }
            if (due.Count == 0)
            {
                return;
            }
            warming = true;
            OdinsPathsPlugin.Instance.StartCoroutine(Warm(due));
        }

        /// <summary>
        /// Loads the prefabs asynchronously, a few at a time, and measures each once it is in,
        /// within the frame budget. Started only by <see cref="Begin"/>, as a coroutine of its own
        /// that nothing stops, so <see cref="warming"/> always comes down.
        /// </summary>
        private static IEnumerator Warm(List<ZoneSystem.ZoneLocation> due)
        {
            System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            System.Diagnostics.Stopwatch frame = new System.Diagnostics.Stopwatch();
            List<Loading> loading = new List<Loading>();
            int next = 0;
            while (next < due.Count || loading.Count > 0)
            {
                while (loading.Count < InFlight && next < due.Count)
                {
                    ZoneSystem.ZoneLocation location = due[next++];
                    Loading started = Loading.Begin(location);
                    if (started != null)
                    {
                        loading.Add(started);
                    }
                    else
                    {
                        measured[location.m_prefabName] = 0f;
                    }
                }
                yield return null;
                frame.Restart();
                for (int i = loading.Count - 1; i >= 0 && frame.Elapsed.TotalMilliseconds < OdinsPathsPlugin.SearchBudgetMs.Value; i--)
                {
                    if (loading[i].Done)
                    {
                        measured[loading[i].Location.m_prefabName] = loading[i].Finish();
                        loading.RemoveAt(i);
                    }
                }
            }
            WriteCache();
            Debug.Log("[OdinsPaths] Measured " + due.Count + " locations' footprints in " + total.ElapsedMilliseconds + " ms, loaded in the background.");
            warming = false;
        }

        /// <summary>
        /// <c>ZoneLocation.m_prefab</c> is a <c>SoftReference</c> from an assembly the build does
        /// not reference, so it is reached by reflection: a boxed copy of it loads and releases the
        /// same asset, which is all it holds the id of. <c>Load</c> and <c>LoadAsync</c> both hold
        /// a reference that <c>Release</c> gives back.
        /// </summary>
        private class Loading
        {
            public ZoneSystem.ZoneLocation Location;
            private object reference;
            private PropertyInfo loaded;
            private PropertyInfo busy;
            private PropertyInfo asset;
            private MethodInfo release;
            private float since;

            public static Loading Begin(ZoneSystem.ZoneLocation location, bool wait = false)
            {
                object reference = typeof(ZoneSystem.ZoneLocation).GetField("m_prefab")?.GetValue(location);
                System.Type type = reference?.GetType();
                PropertyInfo valid = type?.GetProperty("IsValid");
                MethodInfo load = type?.GetMethod(wait ? "Load" : "LoadAsync", System.Type.EmptyTypes);
                Loading loading = new Loading
                {
                    Location = location,
                    reference = reference,
                    loaded = type?.GetProperty("IsLoaded"),
                    busy = type?.GetProperty("IsLoading"),
                    asset = type?.GetProperty("Asset"),
                    release = type?.GetMethod("Release", System.Type.EmptyTypes),
                    since = Time.realtimeSinceStartup,
                };
                if (valid == null || load == null || loading.loaded == null || loading.busy == null || loading.asset == null
                    || loading.release == null || !(bool)valid.GetValue(reference))
                {
                    return null;
                }
                load.Invoke(reference, null);
                return loading;
            }

            /// <summary>Loaded, or given up on.</summary>
            public bool Done => (bool)loaded.GetValue(reference) || !(bool)busy.GetValue(reference)
                || Time.realtimeSinceStartup - since > GiveUp;

            /// <summary>The reach, and the asset released.</summary>
            public float Finish()
            {
                try
                {
                    GameObject prefab = (bool)loaded.GetValue(reference) ? asset.GetValue(reference) as GameObject : null;
                    return prefab != null ? Reach(prefab, Location) : 0f;
                }
                catch (System.Exception error)
                {
                    Debug.LogWarning("[OdinsPaths] Could not measure " + Location.m_prefabName + ": " + error.Message);
                    return 0f;
                }
                finally
                {
                    release.Invoke(reference, null);
                }
            }
        }

        /// <summary>A location the warm-up has not measured, loaded on the spot.</summary>
        private static float Measure(ZoneSystem.ZoneLocation location)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            Loading loading = Loading.Begin(location, true);
            float reach = loading != null ? loading.Finish() : 0f;
            if (watch.ElapsedMilliseconds > 20)
            {
                Debug.Log("[OdinsPaths] Measuring " + location.m_prefabName + " on the spot took " + watch.ElapsedMilliseconds + " ms.");
            }
            return reach;
        }

        private static float Reach(GameObject prefab, ZoneSystem.ZoneLocation location)
        {
            float reach = 0f;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds bounds = filter.sharedMesh.bounds;
                Matrix4x4 toPrefab = toRoot * filter.transform.localToWorldMatrix;
                if (Mathf.Abs(toPrefab.MultiplyPoint3x4(bounds.center).y) > Above)
                {
                    continue;
                }
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 p = toPrefab.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, offset));
                    reach = Mathf.Max(reach, new Vector2(p.x, p.z).magnitude);
                }
            }
            if (reach > location.m_exteriorRadius + 1f)
            {
                Debug.Log("[OdinsPaths] " + location.m_prefabName + " reaches " + reach.ToString("0") + " m, past its exterior radius of "
                    + location.m_exteriorRadius.ToString("0") + " m" + (reach > Largest ? " - kept to " + Largest.ToString("0") + " m." : "."));
            }
            return reach;
        }

        /// <summary>The first line names the format and the game version; a mismatch reads nothing.</summary>
        private static string Header => "# OdinsPaths footprints " + Format + " " + Version.GetVersionString();

        private static void ReadCache()
        {
            if (cacheRead)
            {
                return;
            }
            cacheRead = true;
            try
            {
                if (!File.Exists(CacheFile))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(CacheFile);
                if (lines.Length == 0 || lines[0] != Header)
                {
                    return;
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split('\t');
                    if (parts.Length == 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float reach))
                    {
                        measured[parts[0]] = reach;
                    }
                }
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("[OdinsPaths] Could not read " + CacheFile + ": " + error.Message);
            }
        }

        private static void WriteCache()
        {
            try
            {
                List<string> lines = new List<string> { Header };
                foreach (KeyValuePair<string, float> entry in measured)
                {
                    lines.Add(entry.Key + "\t" + entry.Value.ToString("R", CultureInfo.InvariantCulture));
                }
                Directory.CreateDirectory(BepInEx.Paths.CachePath);
                File.WriteAllLines(CacheFile, lines);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("[OdinsPaths] Could not write " + CacheFile + ": " + error.Message);
            }
        }
    }
}
