using BepInEx.Configuration;
using System;

namespace OdinsMissingPatch
{
    /// <summary>
    /// One quality of life change: the config section it owns, plus the Harmony patches that carry
    /// it out, nested inside it. The patches are always applied and ask <see cref="On"/> before
    /// they do anything, so a tweak can be switched on and off while the game is running.
    /// </summary>
    internal abstract class Tweak
    {
        // A multiplier is applied straight to a game value, so keep a typo from breaking the game.
        private const float MinMultiplier = 0.1f;
        private const float MaxMultiplier = 20f;

        private ConfigEntry<bool> enabled;

        /// <summary>The config section this tweak owns, e.g. "Station Range".</summary>
        internal abstract string Section { get; }

        /// <summary>One line on what switching it on does - the description of Enabled.</summary>
        protected abstract string Summary { get; }

        /// <summary>Binds the tweak's own settings. Enabled is already bound when this runs.</summary>
        protected abstract void Bind(ConfigFile config);

        /// <summary>False until <see cref="Setup"/> has run, so an early patch is simply inert.</summary>
        internal bool On => enabled != null && enabled.Value;

        internal void Setup(ConfigFile config)
        {
            enabled = config.Bind(Section, "Enabled", true, Summary);
            Bind(config);
        }

        /// <summary>A multiplier on a vanilla value, clamped to something the game survives.</summary>
        protected ConfigEntry<float> BindMultiplier(ConfigFile config, string key, float value, string description)
        {
            return config.Bind(Section, key, value, new ConfigDescription(
                description, new AcceptableValueRange<float>(MinMultiplier, MaxMultiplier)));
        }

        /// <summary>
        /// Runs the handler whenever an entry in this tweak's section changes - Enabled included.
        /// Only tweaks that write game state on load need it; one that reads its setting where it
        /// is used follows the config file by itself.
        /// </summary>
        protected void OnSettingChanged(ConfigFile config, Action handler)
        {
            config.SettingChanged += (sender, args) =>
            {
                if (args.ChangedSetting.Definition.Section == Section)
                {
                    handler();
                }
            };
        }
    }
}
