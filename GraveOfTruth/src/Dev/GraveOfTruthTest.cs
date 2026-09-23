using HarmonyLib;
using UnityEngine;

namespace GraveOfTruth
{
    // Dev only: src/Dev/ is compiled into Debug builds alone, so this never ships. Nothing else
    // refers to it - delete the file to drop the test command.
    public partial class GraveOfTruthPlugin
    {
        // How far ahead "gravetest" drops its grave by default.
        private const float TestDistance = 30f;

        /// <summary>
        /// Dev command for testing without dying: "gravetest [distance]" drops a real grave of
        /// yours, holding one stone so it can be looted, where you are looking and puts the death
        /// show on over it for everyone. Needs devcommands.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        public static class TestCommand
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("gravetest",
                    "[distance=30] spawns a grave where you are looking and plays the death effects on it",
                    args => SpawnTestGrave(args.TryParameterFloat(1, TestDistance)), isCheat: true);
            }
        }

        private static void SpawnTestGrave(float distance)
        {
            Player player = Player.m_localPlayer;
            Camera camera = Utils.GetMainCamera();
            if (player == null || camera == null)
            {
                return;
            }
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = player.transform.forward;
            }
            forward.Normalize();
            Vector3 pos = player.transform.position + forward * distance;
            if (ZoneSystem.instance != null)
            {
                pos.y = ZoneSystem.instance.GetSolidHeight(pos);
            }

            if (player.m_tombstone != null)
            {
                GameObject grave = Instantiate(player.m_tombstone, pos + Vector3.up, Quaternion.LookRotation(-forward));
                // An empty grave cannot be opened, so give it something to loot.
                GameObject stone = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Stone") : null;
                if (stone != null)
                {
                    grave.GetComponent<Container>().GetInventory().AddItem(stone, 1);
                }
                PlayerProfile profile = Game.instance.GetPlayerProfile();
                grave.GetComponent<TombStone>().Setup(profile.GetName(), profile.GetPlayerID());
            }
            Wail(pos + Vector3.up, strike: true);
        }
    }
}
