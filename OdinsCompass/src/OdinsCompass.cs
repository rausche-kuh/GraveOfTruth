using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// A craftable compass, worn like the wishbone, that points the way to the nearest boss - and,
    /// as it is upgraded through the biomes, to the dungeons and ore of each one. The pointing is
    /// a wind of bright blue lines blowing past the player toward the target, a little denser
    /// the closer it gets and gone once the player has arrived; it makes no sound.
    ///
    /// The finding is the game's own: the vegvisir stones ask the server for the nearest location
    /// of a name over a routed RPC that every server answers, modded or not, so the mod is client
    /// side. Ore veins are not locations and are searched among the objects the client has loaded,
    /// which makes them a short range find like the wishbone's silver.
    ///
    /// Items.cs makes the items and recipes, SE_Compass.cs runs the worn compass, Seeker.cs finds
    /// the target and Streaks.cs draws the way; ROADMAP.md is the plan and what is still to check.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public partial class OdinsCompassPlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.odinscompass";
        public const string NAME = "Odin's Compass";
        public const string VERSION = "0.1.0";

        /// <summary>One tier per biome, in progression order; the tier is 1 + the array index.</summary>
        internal static readonly string[] TierNames =
        {
            "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "Ashlands", "DeepNorth",
        };

        internal static ConfigEntry<float> SeekInterval;
        internal static ConfigEntry<KeyCode> CycleTargetKey;
        internal static ConfigEntry<bool> ShowDistance;
        internal static ConfigEntry<bool> StreakEffect;
        internal static ConfigEntry<float> ArrivalDistance;

        /// <summary>
        /// Per tier, what the compass can be set to seek: <c>name=prefab,prefab|name=prefab</c>.
        /// A name is a $oc_ token from translations.csv (or plain text), each prefab either a
        /// location (found world wide through the server) or a world object such as an ore vein
        /// (found among what the client has loaded). A tier offers its own groups plus every lower
        /// tier's. Every name was confirmed to exist with "compass locations" and "compass prefabs";
        /// whether the world places rock4_copper or MineRock_Copper, silvervein or rock3_silver, is
        /// the one open question - see ROADMAP.md.
        /// </summary>
        internal static ConfigEntry<string>[] TierTargets;

        /// <summary>
        /// Per tier, what crafting it costs: <c>Item:amount,Item:amount</c>. Tier 1 is crafted at a
        /// workbench; every higher tier also consumes the compass of the tier below.
        /// </summary>
        internal static ConfigEntry<string>[] TierRecipes;

        private static readonly string[] DefaultTargets =
        {
            "$oc_eikthyr=Eikthyrnir|$oc_haldor=Vendor_BlackForest|$oc_hildir=Hildir_camp",
            "$oc_elder=GDKing|$oc_burialchamber=Crypt2,Crypt3,Crypt4|$oc_trollcave=TrollCave02|$oc_copper=rock4_copper|$oc_tin=MineRock_Tin",
            "$oc_bonemass=Bonemass|$oc_sunkencrypt=SunkenCrypt4|$oc_bogwitch=BogWitch_Camp",
            "$oc_moder=Dragonqueen|$oc_drakenest=DrakeNest01|$oc_frostcave=MountainCave02|$oc_silver=silvervein",
            "$oc_yagluth=GoblinKing|$oc_fulingvillage=GoblinCamp2,GoblinCamp2_1|$oc_fulingtotem=GoblinTotem|$oc_tarpit=TarPit1,TarPit2,TarPit3,TarPit1_1,TarPit2_1,TarPit3_1",
            "$oc_queen=Mistlands_DvergrBossEntrance1|$oc_infestedmine=Mistlands_DvergrTownEntrance1,Mistlands_DvergrTownEntrance2|$oc_guardtower=Mistlands_GuardTower1_new,Mistlands_GuardTower2_new,Mistlands_GuardTower3_new|$oc_extractor=Mistlands_Excavation1,Mistlands_Excavation2,Mistlands_Excavation3",
            "$oc_fader=FaderLocation|$oc_charredfortress=CharredFortress|$oc_putridhole=MorgenHole1,MorgenHole2,MorgenHole3",
            "$oc_dnboss=DN_Bossroom|$oc_thehole=TheHole01|$oc_morkborg=MorkBorg|$oc_northvillage=NorthVillage|$oc_memorial=NorthMemorialPlace|$oc_lumbercamp=LumberCamp",
        };

        private static readonly string[] DefaultRecipes =
        {
            "TrophyDeer:1,Stone:10",
            "Bronze:2,SurtlingCore:2",
            "Iron:2,WitheredBone:2",
            "Silver:2,Crystal:1",
            "BlackMetal:2,Needle:2",
            "Eitr:2,Wisp:1",
            "FlametalNew:2,CharredCogwheel:1",
            "FrostCore:1",
        };

        void Awake()
        {
            SeekInterval = Config.Bind("Compass", "SeekInterval", 3f, new ConfigDescription(
                "The least time between two asks for the target, in seconds. A found target is kept " +
                "until you have walked a quarter of the way toward it, so asks are rare; while nothing " +
                "is found yet the compass asks on this interval. Each ask is one small message to the " +
                "server per location name in the chosen target.",
                new AcceptableValueRange<float>(1f, 30f)));
            CycleTargetKey = Config.Bind("Compass", "CycleTargetKey", KeyCode.N,
                "Switches the worn compass to its next target. Only the targets its tier has unlocked " +
                "are offered; the choice is saved with the character. N is unbound in the game's " +
                "defaults (C toggles walking, V auto pickup).");
            ShowDistance = Config.Bind("Compass", "ShowDistance", false,
                "Show the distance to the target on the compass's status icon. Off keeps the compass " +
                "a direction only, the way the wishbone is.");
            StreakEffect = Config.Bind("Compass", "StreakEffect", true,
                "The wind of blue lines blowing toward the target while a compass is worn. Off leaves " +
                "only the status icon.");
            ArrivalDistance = Config.Bind("Compass", "ArrivalDistance", 10f, new ConfigDescription(
                "How close to the target the wind stops, in metres: you have arrived, the compass " +
                "goes quiet. The status icon keeps naming the target.",
                new AcceptableValueRange<float>(0f, 100f)));

            TierTargets = new ConfigEntry<string>[TierNames.Length];
            TierRecipes = new ConfigEntry<string>[TierNames.Length];
            for (int i = 0; i < TierNames.Length; i++)
            {
                string section = "Tier " + (i + 1) + " " + TierNames[i];
                TierTargets[i] = Config.Bind(section, "Targets", DefaultTargets[i],
                    "What a compass of this tier (and every higher one) can seek: name=prefab,prefab|name=prefab. " +
                    "A name is a $oc_ token from translations.csv or plain text; a prefab is a location name " +
                    "(found anywhere in the world) or a world object such as an ore vein (found nearby only). " +
                    "Empty offers nothing new at this tier.");
                TierRecipes[i] = Config.Bind(section, "Recipe", DefaultRecipes[i],
                    "What crafting this tier costs, Item:amount,Item:amount, on top of the compass of the " +
                    "tier below (tier 1 needs no compass). Changes apply on the next game start.");
            }

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }
    }
}
