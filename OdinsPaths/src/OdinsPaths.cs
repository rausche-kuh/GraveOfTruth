using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;

namespace OdinsPaths
{
    /// <summary>
    /// Paths grow across the world the way real ones do. When a vegvisir reveals a boss altar,
    /// the next night's sleep lays a trail to it from the nearest point of the network - the
    /// sacrificial stones, a marked base, or a path already there - following the easiest ground:
    /// around hills rather than over them, along valleys, to the shore and on from the next one.
    ///
    /// The mod is meant to run on the server (the host in a local game): the server knows every
    /// boss altar, every zone's terrain data and when the world sleeps, and a path written as the
    /// game's own terrain data is seen by every player, modded or not.
    ///
    /// Built so far: the search (PathSearch), the shaping (Trail) and the terrain writing
    /// (TerrainWriter), run together by PathLayer - reached only through the dev command
    /// "paths lay" in src/Dev/. The triggers and the network are next; ROADMAP.md is the plan.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public partial class OdinsPathsPlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.odinspaths";
        public const string NAME = "Odin's Paths";
        public const string VERSION = "0.1.0";

        /// <summary>The running plugin, for coroutines.</summary>
        internal static OdinsPathsPlugin Instance;

        internal static ConfigEntry<float> PathWidth;
        internal static ConfigEntry<float> WidthVariation;
        internal static ConfigEntry<bool> Levelling;
        internal static ConfigEntry<float> MaxCut;
        internal static ConfigEntry<float> SearchBudgetMs;

        void Awake()
        {
            Instance = this;
            PathWidth = Config.Bind("Path", "Width", 3.5f, new ConfigDescription(
                "How wide a path is painted, in metres. The edge fades and wobbles a little.",
                new AcceptableValueRange<float>(1f, 8f)));
            WidthVariation = Config.Bind("Path", "WidthVariation", 0.5f, new ConfigDescription(
                "How far a path's width drifts from Width along its length, in metres either way, " +
                "so it narrows and widens like a trodden path. 0 keeps it even.",
                new AcceptableValueRange<float>(0f, 2f)));
            Levelling = Config.Bind("Path", "Levelling", true,
                "Smooth the ground along a path so it rises and falls gently instead of following " +
                "every bump. Ground a player has already dug or raised is never touched.");
            MaxCut = Config.Bind("Path", "MaxCut", 1f, new ConfigDescription(
                "How far levelling may cut into or raise the ground, in metres.",
                new AcceptableValueRange<float>(0f, 4f)));
            SearchBudgetMs = Config.Bind("Performance", "SearchBudgetMs", 6f, new ConfigDescription(
                "How many milliseconds per frame the path search may take. More finds a path sooner " +
                "and costs frame rate while it runs.",
                new AcceptableValueRange<float>(1f, 50f)));
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }
    }
}
