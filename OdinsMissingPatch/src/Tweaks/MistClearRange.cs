using BepInEx.Configuration;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Pushes the Mistlands mist further back: a wisplight, a wisp torch and anything else that
    /// keeps the mist off clears a wider circle, so you can see the cliff before you walk off it.
    /// </summary>
    internal sealed class MistClearRange : Tweak
    {
        internal static readonly MistClearRange Instance = new MistClearRange();

        private MistClearRange() { }

        private ConfigEntry<float> radiusMultiplier;

        // The vanilla radius of every demister that has been scaled, so a rescale can start from
        // it instead of from whatever the field holds now. Weak keys: an entry dies with its
        // demister, and nothing here has to know when that happens.
        private readonly ConditionalWeakTable<Demister, StrongBox<float>> vanilla =
            new ConditionalWeakTable<Demister, StrongBox<float>>();

        internal override string Section => "Mist Clear Range";

        protected override string Summary =>
            "Widen the circle a wisplight, a wisp torch and everything else that clears the " +
            "Mistlands mist keeps clear.";

        protected override void Bind(ConfigFile config)
        {
            radiusMultiplier = BindMultiplier(config, "RadiusMultiplier", 2f,
                "Multiplier on the radius everything that clears mist keeps clear. 1 is vanilla.");

            OnSettingChanged(config, Rescale);
        }

        /// <summary>1 while the tweak is off, so callers can multiply either way.</summary>
        private float Scale => On ? radiusMultiplier.Value : 1f;

        /// <summary>
        /// The radius is the end range of the demister's particle force field: the field itself
        /// pushes the mist particles out to it, and ParticleMist reads the same value to decide
        /// where mist may be emitted and whether a point counts as inside a demister. Setting it
        /// from the remembered vanilla value means applying it twice is harmless, so a demister
        /// can be brought up to date whenever it is seen without tracking what it was last
        /// scaled by.
        /// </summary>
        private void Apply(Demister demister)
        {
            if (demister == null || demister.m_forceField == null)
            {
                return;
            }
            // The first time a demister is seen its range is still the prefab's.
            StrongBox<float> range = vanilla.GetValue(demister,
                d => new StrongBox<float>(d.m_forceField.endRange));
            demister.m_forceField.endRange = range.Value * Scale;
        }

        /// <summary>
        /// Demisters are scaled as they come on, so a setting changed mid game has to be carried
        /// to the ones already on. The game's own list holds exactly those; one that is off at
        /// the time is caught up by the OnEnable patch when it comes back.
        /// </summary>
        private void Rescale()
        {
            foreach (Demister demister in Demister.GetDemisters())
            {
                Apply(demister);
            }
        }

        /// <summary>
        /// OnEnable is where the game registers a demister, and it runs after Awake has found
        /// the force field. Patching it rather than Awake also covers a demister the game
        /// switches off and on again - the wisp fountain does that with its nearby-wisps object -
        /// which would otherwise come back at whatever scale it was switched off with.
        /// </summary>
        [HarmonyPatch(typeof(Demister), "OnEnable")]
        private static class ScaleDemister
        {
            private static void Postfix(Demister __instance)
            {
                Instance.Apply(__instance);
            }
        }
    }
}
