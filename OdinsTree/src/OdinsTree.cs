using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;

namespace OdinsTree
{
    /// <summary>
    /// A tree the hammer has blessed never falls, so a tree house built on it is safe. The blessing
    /// is the tree's own health on its ZDO, raised past anything that can hit it - vanilla data the
    /// vanilla damage code reads, which is why the tree stays standing when the mod is removed.
    ///
    /// Only the entry point and the settings exist so far; ROADMAP.md is the plan for the rest.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class OdinsTreePlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.odinstree";
        public const string NAME = "Odin's Tree";
        public const string VERSION = "0.1.0";

        internal static ConfigEntry<float> TerrainGuardRadius;
        internal static ConfigEntry<float> TwerkGrowSeconds;

        void Awake()
        {
            TerrainGuardRadius = Config.Bind("Blessing", "TerrainGuardRadius", 3f, new ConfigDescription(
                "How far from a blessed tree's trunk the ground cannot be dug, raised or levelled, " +
                "in metres. 0 lets the ground be changed right up to the trunk.",
                new AcceptableValueRange<float>(0f, 10f)));
            TwerkGrowSeconds = Config.Bind("Growth", "TwerkGrowSeconds", 15f, new ConfigDescription(
                "How many seconds of dodging back and forth beside a sapling grow it into a tree. " +
                "0 switches twerking off.",
                new AcceptableValueRange<float>(0f, 60f)));

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }
    }
}
