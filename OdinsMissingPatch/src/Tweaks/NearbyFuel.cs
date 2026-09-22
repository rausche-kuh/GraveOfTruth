using BepInEx.Configuration;
using HarmonyLib;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Adding fuel by hand reaches into the chests around you. Press Use on a fire, a smelter,
    /// an oven or a shield generator as ever: one unit goes in, out of your backpack if you carry
    /// the fuel and out of the nearest chest that holds it if you do not. Nothing refuels itself;
    /// the station only ever takes fuel when you give it some.
    ///
    /// The four add-fuel interactions are the whole change: the shared reach (see NearbyChests)
    /// is open while one of them runs for the local player, so the game's own "have any?" and
    /// "take one" see the chests, backpack first.
    /// </summary>
    internal sealed class NearbyFuel : Tweak
    {
        internal static readonly NearbyFuel Instance = new NearbyFuel();

        private NearbyFuel() { }

        private ConfigEntry<float> range;

        internal override string Section => "Nearby Fuel";

        protected override string Summary =>
            "Adding fuel to a fire, smelter, oven or shield generator by hand takes it from chests " +
            "around you when your backpack has none. Only chests placed by a player, and not chests " +
            "switched off with the Nearby use button in their panel.";

        protected override void Bind(ConfigFile config)
        {
            range = config.Bind(Section, "Range", 20f, new ConfigDescription(
                "How far from you a chest may stand, in metres, for its fuel to be within reach.",
                new AcceptableValueRange<float>(1f, 100f)));
        }

        private static bool Enter(Humanoid user)
        {
            if (!Instance.On || user == null || user != Player.m_localPlayer)
            {
                return false;
            }
            NearbyChests.EnterReach(Instance.range.Value);
            return true;
        }

        private static void Leave(bool entered)
        {
            if (entered)
            {
                NearbyChests.LeaveReach();
            }
        }

        /// <summary>Use on a campfire, hearth, torch, brazier or hot tub.</summary>
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
        private static class FireScope
        {
            private static void Prefix(Humanoid user, out bool __state) => __state = Enter(user);

            private static void Finalizer(bool __state) => Leave(__state);
        }

        /// <summary>The fuel switch of a smelter or blast furnace; a kiln has none.</summary>
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        private static class SmelterScope
        {
            private static void Prefix(Humanoid user, out bool __state) => __state = Enter(user);

            private static void Finalizer(bool __state) => Leave(__state);
        }

        /// <summary>The fuel switch of a cooking station that burns fuel, i.e. an oven.</summary>
        [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
        private static class OvenScope
        {
            private static void Prefix(Humanoid user, out bool __state) => __state = Enter(user);

            private static void Finalizer(bool __state) => Leave(__state);
        }

        /// <summary>The fuel switch of a shield generator, which takes any of several fuels.</summary>
        [HarmonyPatch(typeof(ShieldGenerator), "OnAddFuel")]
        private static class ShieldGeneratorScope
        {
            private static void Prefix(Humanoid user, out bool __state) => __state = Enter(user);

            private static void Finalizer(bool __state) => Leave(__state);
        }
    }
}
