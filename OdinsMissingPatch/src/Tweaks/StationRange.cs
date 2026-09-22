using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Widens the circle a crafting station covers, and how far its extensions may sit from it -
    /// so a workshop can be a room rather than a huddle around the workbench.
    /// </summary>
    internal sealed class StationRange : Tweak
    {
        internal static readonly StationRange Instance = new StationRange();

        private StationRange() { }

        // Below this the ring the area marker draws stops being a ring.
        private const int MinSegments = 3;

        private ConfigEntry<float> buildRange;
        private ConfigEntry<float> extensionRange;

        // What the stations standing in the world were last scaled by. Keeping it means a setting
        // changed mid game can rescale them without knowing their vanilla values.
        private float appliedBuild = 1f;
        private float appliedExtension = 1f;

        internal override string Section => "Station Range";

        protected override string Summary =>
            "Widen the area a crafting station covers, and how far its extensions may stand from it.";

        protected override void Bind(ConfigFile config)
        {
            buildRange = BindMultiplier(config, "BuildRangeMultiplier", 2f,
                "Multiplier on the radius in which a station lets you build, craft and repair. " +
                "1 is vanilla - 10m for the workbench, 20m for the forge.");
            extensionRange = BindMultiplier(config, "ExtensionRangeMultiplier", 2f,
                "Multiplier on how far an extension (the forge cooler, the tanning rack, ...) may " +
                "stand from its station and still raise its level. 1 is vanilla, usually 5m.");

            OnSettingChanged(config, Rescale);
            // Nothing is loaded yet, so this only records what the stations to come are scaled by.
            Rescale();
        }

        /// <summary>1 while the tweak is off, so callers can multiply either way.</summary>
        private float BuildScale => On ? buildRange.Value : 1f;

        private float ExtensionScale => On ? extensionRange.Value : 1f;

        /// <summary>
        /// m_rangeBuild is an instance field copied off the prefab, so scaling it here is per
        /// station and never touches the prefab. Everything the game derives from it follows: the
        /// build check, the area marker circle and the station's effect area collider.
        /// </summary>
        [HarmonyPatch(typeof(CraftingStation), "Start")]
        private static class ScaleStation
        {
            private static void Postfix(CraftingStation __instance)
            {
                Scale(__instance, Instance.BuildScale);
            }
        }

        /// <summary>
        /// The same condition the game's own Awake registers an extension under. Anything failing
        /// it - a placement ghost - is not in m_allExtensions, so a rescale could never reach it
        /// again; leaving it vanilla keeps "scaled" and "rescalable" the same set. It is also not
        /// in the list the game searches, so its range is never read anyway.
        /// </summary>
        [HarmonyPatch(typeof(StationExtension), "Awake")]
        private static class ScaleExtension
        {
            private static void Postfix(StationExtension __instance)
            {
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO() != null)
                {
                    __instance.m_maxStationDistance *= Instance.ExtensionScale;
                }
            }
        }

        /// <summary>
        /// Stations are scaled as they load, so a setting changed mid game has to be carried to
        /// the ones already standing. The game's own registries hold exactly those - anything not
        /// in them is not in the world.
        /// </summary>
        private void Rescale()
        {
            // The step from what they are scaled by now to what they should be scaled by.
            float buildStep = BuildScale / appliedBuild;
            float extensionStep = ExtensionScale / appliedExtension;

            foreach (CraftingStation station in CraftingStation.m_allStations)
            {
                Scale(station, buildStep);
            }
            foreach (StationExtension ext in StationExtension.m_allExtensions)
            {
                ext.m_maxStationDistance *= extensionStep;
            }

            appliedBuild = BuildScale;
            appliedExtension = ExtensionScale;
        }

        /// <summary>
        /// The station's build range, and the ring the area marker draws it with.
        ///
        /// The radius of that ring the game recomputes from m_rangeBuild itself, on its own two
        /// second timer, but the number of markers it lays around it is whatever the prefab set
        /// and is never touched - so a widened circle would be drawn by the same handful of
        /// markers, stretched into a dotted line. Scaling the count alongside the radius keeps
        /// the ring as dense as it looks in vanilla, whatever density the prefab chose.
        /// </summary>
        private static void Scale(CraftingStation station, float step)
        {
            station.m_rangeBuild *= step;
            station.m_extraRangePerLevel *= step;

            CircleProjector circle = station.m_areaMarkerCircle;
            if (circle != null)
            {
                circle.m_nrOfSegments = Mathf.Max(MinSegments, Mathf.RoundToInt(circle.m_nrOfSegments * step));
            }
        }
    }
}
