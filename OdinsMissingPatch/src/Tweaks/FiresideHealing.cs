using BepInEx.Configuration;
using HarmonyLib;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Resting by the fire mends you, and the better the room the faster: on the same ten second
    /// beat the game heals you for the food you have eaten, you also heal for the comfort of the
    /// spot you are resting in. A campfire out in the open is worth little; a furnished hall
    /// with a fire, a bed, a banner and a chair is worth ten times as much.
    ///
    /// Whether you are resting at all is the game's own verdict (the Resting effect): a fire
    /// within reach, and sitting or under your own roof, and not wet, cold, burning or spotted.
    /// So the fire is always required, the wet and the cold still get in the way, and a monster
    /// finding you cuts the healing off with the rest of it.
    /// </summary>
    internal sealed class FiresideHealing : Tweak
    {
        internal static readonly FiresideHealing Instance = new FiresideHealing();

        private FiresideHealing() { }

        // The game's own regen period: Player.UpdateFood heals once m_foodRegenTimer passes it.
        private const float RegenInterval = 10f;

        private ConfigEntry<float> healthPerComfortLevel;
        private ConfigEntry<bool> requireSitting;

        // What this regen tick owes for comfort, waiting to be folded into the game's own heal.
        // Non-zero only while Player.UpdateFood is running.
        private static float pending;

        internal override string Section => "Fireside Healing";

        protected override string Summary =>
            "Resting by a fire heals you for the comfort of the spot, on top of what your food " +
            "heals, on the same ten second beat.";

        protected override void Bind(ConfigFile config)
        {
            healthPerComfortLevel = config.Bind(Section, "HealthPerComfortLevel", 2f, new ConfigDescription(
                "Health healed per comfort level every ten seconds while you rest by a fire. A " +
                "lone campfire is comfort 1, a furnished hall reaches ten and more, so 2 is " +
                "about 12 health a minute at a campfire and 120 in a good hall. 0 is vanilla.",
                new AcceptableValueRange<float>(0f, 100f)));
            requireSitting = config.Bind(Section, "RequireSitting", true,
                "Only heal while you are actually sitting - a chair, a bench or the sit emote. " +
                "Off heals whenever you are Resting, which includes standing by the fire under " +
                "your own roof.");
        }

        /// <summary>
        /// What the spot the player is in is worth this tick, or 0 if it owes nothing. The
        /// comfort level is the one the player measured on their own two second timer, which is
        /// as fresh as anything on a ten second beat needs.
        /// </summary>
        private float ComfortHealth(Player player)
        {
            if (!On || player == null || player != Player.m_localPlayer)
            {
                return 0f;
            }
            if (requireSitting.Value && !player.IsSitting())
            {
                return 0f;
            }
            SEMan seman = player.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(SEMan.s_statusEffectResting))
            {
                return 0f;
            }
            int comfort = player.GetComfortLevel();
            return comfort > 0 ? comfort * healthPerComfortLevel.Value : 0f;
        }

        /// <summary>
        /// The regen tick lives inside <c>Player.UpdateFood</c>: past ten seconds it sums the
        /// food's regen, lets the status effects multiply it and heals that much - and when no
        /// food is eaten it heals nothing at all. So the tick is worked out in the prefix (the
        /// timer plus this frame's dt, exactly what the method is about to test) and put aside
        /// for the <see cref="FoldIntoFoodHeal"/> patch to add to the game's own heal, which
        /// keeps it one number on screen rather than two floating over each other.
        ///
        /// The finalizer heals what is left over, which is the tick where the player has eaten
        /// nothing and the game never healed at all. It heals only once the timer confirms the
        /// tick really did fire, so a mispredicted frame pays nothing, and it clears what was
        /// put aside whether the method returned or threw - a stale amount would otherwise be
        /// picked up by the next heal the player gets from anywhere.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
        private static class HealForComfort
        {
            private static void Prefix(Player __instance, float dt, bool forceUpdate, out float __state)
            {
                __state = __instance.m_foodRegenTimer;
                pending = forceUpdate || __instance.m_foodRegenTimer + dt < RegenInterval
                    ? 0f
                    : Instance.ComfortHealth(__instance);
            }

            private static void Finalizer(Player __instance, float __state)
            {
                float owed = pending;
                pending = 0f;
                if (owed > 0f && __instance.m_foodRegenTimer < __state)
                {
                    __instance.Heal(owed);
                }
            }
        }

        /// <summary>
        /// The one heal the regen tick does, made bigger. Nothing else can be caught by this:
        /// the amount is only ever put aside for the length of <c>Player.UpdateFood</c>, and the
        /// one heal in there is the tick's own.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.Heal))]
        private static class FoldIntoFoodHeal
        {
            private static void Prefix(Character __instance, ref float hp)
            {
                if (pending > 0f && __instance == Player.m_localPlayer)
                {
                    hp += pending;
                    pending = 0f;
                }
            }
        }
    }
}
