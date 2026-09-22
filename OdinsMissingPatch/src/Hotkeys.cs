using BepInEx.Configuration;
using UnityEngine;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Reads a configured KeyboardShortcut through ZInput, the game's own input layer.
    /// KeyboardShortcut.IsDown itself refuses to fire while any key outside the combination is
    /// held, which in this game means "not while walking"; these only ask for the keys named.
    /// </summary>
    internal static class Hotkeys
    {
        /// <summary>True on the frame the main key goes down with every modifier held.</summary>
        internal static bool Pressed(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, logWarning: false))
            {
                return false;
            }
            return ModifiersHeld(shortcut);
        }

        /// <summary>True while the main key and every modifier are held.</summary>
        internal static bool Held(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKey(shortcut.MainKey, logWarning: false))
            {
                return false;
            }
            return ModifiersHeld(shortcut);
        }

        private static bool ModifiersHeld(KeyboardShortcut shortcut)
        {
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!ZInput.GetKey(modifier, logWarning: false))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
