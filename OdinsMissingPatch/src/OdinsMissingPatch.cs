using BepInEx;
using HarmonyLib;
using System.Linq;
using System.Reflection;

namespace OdinsMissingPatch
{
    /// <summary>
    /// A collection of small quality of life changes. The plugin itself does nothing but bind the
    /// config file and apply the patches - every change lives in its own <see cref="Tweak"/>.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class OdinsMissingPatchPlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.odinsmissingpatch";
        public const string NAME = "Odin's Missing Patch";
        public const string VERSION = "0.2.2";

        /// <summary>Every tweak the mod ships. Listing one here is all it takes to enable it.</summary>
        private static readonly Tweak[] Tweaks =
        {
            StationRange.Instance,
            ComfortRange.Instance,
            EndlessFuel.Instance,
            MistClearRange.Instance,
            CombatStamina.Instance,
            InstantComfort.Instance,
            FiresideHealing.Instance,
            FastPortals.Instance,
            KeepGearOnDeath.Instance,
            AreaRepair.Instance,
            NearbyCrafting.Instance,
            QuickStack.Instance,
            NearbyFuel.Instance,
            AddAll.Instance,
            AutoRepair.Instance,
            ChestButtons.Instance,
            InventoryButtons.Instance,
            PowerPicker.Instance,
            EquipWhileRunning.Instance,
            AutoShield.Instance,
        };

        void Awake()
        {
            foreach (Tweak tweak in Tweaks)
            {
                tweak.Setup(Config);
            }

            // Every patch is applied once, whatever the config says, and asks its own tweak
            // whether it is on before it does anything - so the toggles work while the game runs.
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);

            string on = string.Join(", ", Tweaks.Where(t => t.On).Select(t => t.Section).ToArray());
            Logger.LogInfo(on.Length > 0 ? "tweaks on: " + on : "every tweak is switched off");
        }
    }
}
