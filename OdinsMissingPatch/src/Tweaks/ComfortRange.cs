using BepInEx.Configuration;
using HarmonyLib;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Lets the comfort of a room reach across the room: the furniture that counts towards Rested
    /// no longer has to be crowded around the spot you stand on.
    /// </summary>
    internal sealed class ComfortRange : Tweak
    {
        internal static readonly ComfortRange Instance = new ComfortRange();

        private ComfortRange() { }

        private ConfigEntry<float> radiusMultiplier;

        internal override string Section => "Comfort Range";

        protected override string Summary =>
            "Widen the radius around you in which comfort furniture counts towards Rested.";

        protected override void Bind(ConfigFile config)
        {
            radiusMultiplier = BindMultiplier(config, "RadiusMultiplier", 2f,
                "Multiplier on the radius your comfort pieces are counted in. 1 is vanilla (10m).");
        }

        /// <summary>
        /// The radius is a literal inside SE_Rested, but it reaches the pieces through this one
        /// call, so widening the argument is the whole tweak - nothing is written to game state,
        /// so there is nothing to restore and nothing to rescale when the setting changes.
        /// </summary>
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetAllComfortPiecesInRadius))]
        private static class WidenComfortRadius
        {
            private static void Prefix(ref float radius)
            {
                if (Instance.On)
                {
                    radius *= Instance.radiusMultiplier.Value;
                }
            }
        }
    }
}
