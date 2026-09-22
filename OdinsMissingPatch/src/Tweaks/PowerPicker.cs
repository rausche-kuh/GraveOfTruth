using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Valheim.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// A Forsaken powers category in the radial menu: opening it shows one element per boss power
    /// this character has unlocked, each with the power's own icon, and picking one makes it the
    /// power the power key casts - without the walk back to the sacrificial stones.
    ///
    /// Only the choice is moved. The power is still cast by the game's own key, the cooldown is
    /// still the one you were already on (switching does not reset it, exactly as switching at the
    /// stones does not), and the category is not there at all until a power has been taken at the
    /// stones once - the game writes that into the character on the way, which is what this reads.
    /// </summary>
    internal sealed class PowerPicker : Tweak
    {
        internal static readonly PowerPicker Instance = new PowerPicker();

        private PowerPicker() { }

        /// <summary>The tint of the icon of the power you are already carrying.</summary>
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f, 1f);

        internal override string Section => "Power Picker";

        protected override string Summary =>
            "A Forsaken powers category in the radial menu, holding every boss power you have " +
            "unlocked; picking one makes it yours, without the walk back to the stones.";

        protected override void Bind(ConfigFile config)
        {
            // Nothing beyond the Enabled switch: the radial is built afresh every time it opens
            // and asks the tweak then, so switching it off simply drops the category again.
        }

        // ---- What is unlocked ------------------------------------------------------------------

        /// <summary>
        /// The boss powers this character may choose from, in the game's own order. Taking a
        /// power at a sacrificial stone goes through <c>Player.SetGuardianPower</c>, which writes
        /// the power's name into the character's unique keys, so those keys are the record of
        /// what has ever been unlocked; a key naming a status effect with a cooldown - the one
        /// field only a guardian power fills in - is one of them.
        ///
        /// The power the character is carrying is counted whether or not it left a key behind, so
        /// that a character who took one before the game started writing those keys, or through
        /// another mod, never finds their own power missing from the list.
        /// </summary>
        private static List<StatusEffect> Unlocked()
        {
            List<StatusEffect> powers = new List<StatusEffect>();
            Player player = Player.m_localPlayer;
            ObjectDB db = ObjectDB.instance;
            if (player == null || db == null)
            {
                return powers;
            }
            string current = player.GetGuardianPowerName();
            foreach (StatusEffect effect in db.m_StatusEffects)
            {
                if (effect != null && effect.m_cooldown > 0f
                    && (player.HaveUniqueKey(effect.name) || effect.name == current))
                {
                    powers.Add(effect);
                }
            }
            return powers;
        }

        /// <summary>The power the character is carrying, or "" - the game's own answer.</summary>
        private static string Current()
        {
            Player player = Player.m_localPlayer;
            return player != null ? player.GetGuardianPowerName() : "";
        }

        /// <summary>
        /// Hands the power over, the same call the stone's own delayed activation makes. The
        /// cooldown is a timer on the player rather than on the power, so it is left alone and a
        /// switch mid cooldown buys nothing.
        /// </summary>
        private static void Pick(StatusEffect power)
        {
            Player player = Player.m_localPlayer;
            if (player == null || power == null || player.GetGuardianPowerName() == power.name)
            {
                return;
            }
            player.SetGuardianPower(power.name);
            player.Message(MessageHud.MessageType.Center, Localization.instance.Localize(power.m_name));
        }

        // ---- The category ----------------------------------------------------------------------

        /// <summary>
        /// Adds the category to the top level of the radial menu. Every config builds its own
        /// list of elements and hands it to <c>ConstructRadial</c>, so a prefix there is where a
        /// list can still be added to; the main menu is told apart by the config the radial is
        /// currently opening. The list is built afresh on every open, so nothing is cached and a
        /// power unlocked since is simply there the next time.
        /// </summary>
        [HarmonyPatch(typeof(RadialBase), "ConstructRadial")]
        private static class TopCategory
        {
            private static void Prefix(RadialBase __instance, List<RadialMenuElement> elements)
            {
                if (!Instance.On || elements == null || !(__instance.CurrentConfig is ValheimRadialConfig))
                {
                    return;
                }
                // A character that has never taken a power at a stone has nothing to choose
                // between, and an empty category would only be a dead end.
                if (RadialData.SO == null || RadialData.SO.GroupElement == null || Unlocked().Count == 0)
                {
                    return;
                }
                GroupElement group = Object.Instantiate(RadialData.SO.GroupElement);
                // The main config is its own back config: picking Back from the powers returns
                // to the ring the category was opened from, as every other group does.
                group.Init(new PowerConfig(), __instance.CurrentConfig, __instance);
                elements.Add(group);
            }
        }

        /// <summary>
        /// The category itself and the sub menu behind it. A radial config is a plain
        /// <see cref="IRadialConfig"/> - the game's own are ScriptableObjects only because they
        /// are authored in the editor - and its <c>InitRadialConfig</c> is called by the radial
        /// each time the sub menu opens.
        /// </summary>
        private sealed class PowerConfig : IRadialConfig
        {
            public string LocalizedName => "Forsaken Powers";

            /// <summary>
            /// The icon of the power you are carrying, so the top level ring already says which
            /// one that is; the first unlocked power's icon while none is set.
            /// </summary>
            public Sprite Sprite
            {
                get
                {
                    List<StatusEffect> powers = Unlocked();
                    string current = Current();
                    foreach (StatusEffect power in powers)
                    {
                        if (power.name == current)
                        {
                            return power.m_icon;
                        }
                    }
                    return powers.Count > 0 ? powers[0].m_icon : null;
                }
            }

            public void InitRadialConfig(RadialBase radial)
            {
                List<RadialMenuElement> elements = new List<RadialMenuElement>();
                string current = Current();
                foreach (StatusEffect power in Unlocked())
                {
                    RadialMenuElement element = Element(power, power.name == current);
                    if (element != null)
                    {
                        elements.Add(element);
                    }
                }
                radial.ConstructRadial(elements);
            }

            /// <summary>
            /// One power, built from the emote element - the game's own icon-and-name element,
            /// which is what an emote in the emote sub menu is. Its Init is for an emote, so the
            /// three things an element is - what it is called, what it shows and what it does -
            /// are set here instead.
            ///
            /// The radial keeps whatever was last used as an element of its own and puts it back
            /// in the top level ring, so the element outlives the menu it was built for; picking
            /// marks it as the active one there and then, rather than leaving the ring showing a
            /// power that has since become yours as though it had not.
            /// </summary>
            private static RadialMenuElement Element(StatusEffect power, bool active)
            {
                EmoteElement element = RadialData.SO.EmoteElement != null
                    ? Object.Instantiate(RadialData.SO.EmoteElement) : null;
                if (element == null)
                {
                    return null;
                }
                element.SubTitle = Localization.instance.Localize(power.GetTooltipString());
                element.CloseOnInteract = () => true;
                element.Interact = delegate
                {
                    Pick(power);
                    Mark(element, power, true);
                    return true;
                };
                Mark(element, power, active);
                return element;
            }

            /// <summary>Names an element and tints its icon, gold while it is the power you carry.</summary>
            private static void Mark(RadialMenuElement element, StatusEffect power, bool active)
            {
                string name = Localization.instance.Localize(power.m_name);
                element.Name = active ? name + " (active)" : name;
                Image icon = element.Icon;
                if (icon != null)
                {
                    icon.gameObject.SetActive(power.m_icon != null);
                    icon.sprite = power.m_icon;
                    icon.color = active ? Gold : Color.white;
                }
            }
        }
    }
}
