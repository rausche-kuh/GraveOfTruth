using BepInEx.Configuration;
using HarmonyLib;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Sitting down by the fire grants Rested at once, for the comfort of the spot you sat down
    /// in, instead of after the ten seconds of Resting the game makes you wait through first.
    ///
    /// The game's own chain is kept: Resting is still what decides whether you may rest at all
    /// (a fire, shelter or a seat, not wet, cold, burning or spotted), and Rested is still the
    /// effect it hands out. The only change is that the delay before the hand-over is skipped
    /// while you sit; standing by the fire waits as it always has.
    /// </summary>
    internal sealed class InstantComfort : Tweak
    {
        internal static readonly InstantComfort Instance = new InstantComfort();

        private InstantComfort() { }

        internal override string Section => "Instant Comfort";

        protected override string Summary =>
            "Sitting down by a fire grants Rested at once, at the comfort of where you sit, " +
            "instead of after ten seconds of Resting.";

        protected override void Bind(ConfigFile config)
        {
            // Nothing beyond the Enabled switch: the patch asks it on every Resting tick, so
            // switching the tweak off brings the ten second wait straight back.
        }

        /// <summary>
        /// Resting (SE_Cozy) is added by the player's environment update on every frame the
        /// resting conditions hold and removed the moment they stop, so it is a fresh instance
        /// each time you sit down, and its own tick hands out Rested once its age passes
        /// m_delay. This postfix hands it out from the first tick instead, while the player is
        /// sitting, and steps aside once the vanilla tick has taken over.
        ///
        /// The comfort level Rested's duration is computed from is re-measured by the player on
        /// a two second timer, which the ten second wait used to hide: sit down straight after
        /// walking in and the level may still be the one from outside. So it is measured again
        /// before the first grant. Later ticks only refresh the duration, which the game never
        /// shortens, so they can use the timer's value.
        /// </summary>
        [HarmonyPatch(typeof(SE_Cozy), nameof(SE_Cozy.UpdateStatusEffect))]
        private static class GrantWhileSitting
        {
            private static void Postfix(SE_Cozy __instance)
            {
                if (!Instance.On || __instance.m_time > __instance.m_delay)
                {
                    return;
                }
                Player player = __instance.m_character as Player;
                if (player == null || player != Player.m_localPlayer || !player.IsSitting())
                {
                    return;
                }
                int rested = __instance.m_statusEffectHash;
                if (rested == 0)
                {
                    return;
                }
                SEMan seman = player.GetSEMan();
                if (seman == null)
                {
                    return;
                }
                if (!seman.HaveStatusEffect(rested))
                {
                    player.m_comfortLevel = SE_Rested.CalculateComfortLevel(player);
                }
                seman.AddStatusEffect(rested, resetTime: true);
            }
        }
    }
}
