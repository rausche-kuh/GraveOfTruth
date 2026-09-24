using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OdinsTree
{
    /// <summary>
    /// A tree the hammer has blessed never falls, so a tree house built on it is safe. The blessing
    /// is the tree's own health on its ZDO, raised past anything that can hit it - vanilla data the
    /// vanilla damage code reads, which is why the tree stays standing when the mod is removed.
    ///
    /// Around it: the ground under a blessed tree cannot be dug (this client only), the hammer's
    /// remove click swaps an unblessed tree for the next kind of its family, and dodging beside a
    /// sapling grows it.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class OdinsTreePlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.odinstree";
        public const string NAME = "Odin's Tree";
        public const string VERSION = "0.1.0";

        /// <summary>
        /// The health a blessed tree gets. Finite, so other mods' maths stay sane, but so large
        /// that a float cannot even represent the loss of an ordinary hit: a client without the
        /// mod chops at it forever.
        /// </summary>
        private const float BlessedHealth = 1e12f;

        /// <summary>Anything at or above this is blessed - no vanilla tree comes near it.</summary>
        private const float BlessedThreshold = 1e8f;

        /// <summary>How far from the player a sapling still feels the twerk, in metres.</summary>
        private const float TwerkRadius = 3f;

        /// <summary>A pause longer than this between two crouch presses ends the twerk.</summary>
        private const float MaxCrouchInterval = 1.5f;

        /// <summary>Crouch presses in a row before the saplings start to grow.</summary>
        private const int TwerkCrouches = 3;

        /// <summary>The tint of a growing sapling; the emission is a dimmer copy of it.</summary>
        private static readonly Color TwerkGreen = new Color(0.4f, 1f, 0.3f);

        internal static ConfigEntry<float> TerrainGuardRadius;
        internal static ConfigEntry<float> TwerkGrowSeconds;
        internal static ConfigEntry<string> Families;

        /// <summary>Every loaded tree; destroyed ones turn null and are dropped on the next scan.</summary>
        private static readonly List<TreeBase> trees = new List<TreeBase>();

        /// <summary>The tree families, built once the prefabs are known - see <see cref="GetFamilies"/>.</summary>
        private static List<List<string>> families;

        /// <summary>Crouch presses in the current run, and when the last one came.</summary>
        private static int crouches;
        private static float lastCrouch = -999f;
        private static float lastWhyNotGrowing = -999f;

        /// <summary>The saplings currently tinted green, to untint when the twerk stops.</summary>
        private static readonly List<Plant> glowing = new List<Plant>();

        void Awake()
        {
            TerrainGuardRadius = Config.Bind("Blessing", "TerrainGuardRadius", 3f, new ConfigDescription(
                "How far from a blessed tree's trunk the ground cannot be dug, raised or levelled, " +
                "in metres. 0 lets the ground be changed right up to the trunk. On a server this " +
                "is what players without the mod are held to.",
                new AcceptableValueRange<float>(0f, 10f)));
            TwerkGrowSeconds = Config.Bind("Growth", "TwerkGrowSeconds", 15f, new ConfigDescription(
                "How many seconds of dodging back and forth beside a sapling grow it into a tree. " +
                "0 switches twerking off.",
                new AcceptableValueRange<float>(0f, 60f)));
            Families = Config.Bind("Kinds", "Families",
                "Birch1,Birch2,Birch1_aut,Birch2_aut;" +
                "Beech_small1,Beech_small2,Beech1;" +
                "FirTree_small,FirTree,FirTree_big;" +
                "Pinetree_01,Pinetree_Snow;" +
                "YggaShoot1,YggaShoot2,YggaShoot3;" +
                "SwampTree1,SwampTree2",
                "Which trees the hammer's remove click cycles through, as prefab names: commas " +
                "between the kinds of one family, semicolons between families. Whatever a sapling " +
                "can grow into is a family already; unknown names are ignored.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static bool IsBlessed(TreeBase tree)
        {
            ZNetView nview = tree != null ? tree.m_nview : null;
            return nview != null && nview.IsValid()
                && nview.GetZDO().GetFloat(ZDOVars.s_health, tree.m_health) >= BlessedThreshold;
        }

        /// <summary>
        /// What the player is pointed at. Out of build mode that is the game's own hover object.
        /// Build mode clears it every frame (UpdateHover), so there this is the same raycast the
        /// game uses to find the piece to repair or remove: from the camera, on the remove mask,
        /// within placing distance of the eyes.
        /// </summary>
        private static GameObject AimedObject(Player player)
        {
            if (player == null)
            {
                return null;
            }
            if (!player.InPlaceMode())
            {
                return player.GetHoverObject();
            }
            if (GameCamera.instance == null || player.m_eye == null)
            {
                return null;
            }
            Transform camera = GameCamera.instance.transform;
            if (!Physics.Raycast(camera.position, camera.forward, out RaycastHit hit, 50f, player.m_removeRayMask)
                || Vector3.Distance(player.m_eye.position, hit.point) >= player.m_maxPlaceDistance)
            {
                return null;
            }
            return hit.collider.gameObject;
        }

        private static TreeBase AimedTree(Player player)
        {
            GameObject aimed = AimedObject(player);
            return aimed != null ? aimed.GetComponentInParent<TreeBase>() : null;
        }

        /// <summary>
        /// The tree's status, like the ancient root's, plus the hammer's hints for what it is
        /// pointed at; or an empty string.
        /// </summary>
        private static string HoverText(Player player, GameObject aimed)
        {
            if (aimed == null)
            {
                return "";
            }
            TreeBase tree = aimed.GetComponentInParent<TreeBase>();
            ZNetView nview = aimed.GetComponentInParent<ZNetView>();
            bool blessed = IsBlessed(tree);
            string text = blessed ? "Blessed by Odin" : "";
            if (!player.InPlaceMode())
            {
                return text;
            }
            if (tree != null && player.GetSelectedPiece()?.m_repairPiece == true)
            {
                text += blessed ? "\n[LMB] Lift blessing" : "\n[LMB] Bless";
            }
            if (!blessed && nview != null && NextKind(nview.gameObject) != null)
            {
                text += "\n[RMB] Next kind";
            }
            return text.TrimStart('\n');
        }

        private static bool NearBlessedTree(Vector3 point, float radius)
        {
            float guard = TerrainGuardRadius.Value;
            if (guard <= 0f)
            {
                return false;
            }
            for (int i = trees.Count - 1; i >= 0; i--)
            {
                TreeBase tree = trees[i];
                if (tree == null)
                {
                    trees.RemoveAt(i);
                }
                else if (IsBlessed(tree) && Utils.DistanceXZ(tree.transform.position, point) < guard + radius)
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly List<ZDO> nearZDOs = new List<ZDO>();

        /// <summary>
        /// <see cref="NearBlessedTree"/> from the world data instead of the loaded trees, so it
        /// also works where the trees are not instantiated: on a server. Looks through the zone
        /// of the point and its neighbours.
        /// </summary>
        private static bool NearBlessedTreeZDO(Vector3 point, float radius)
        {
            float guard = TerrainGuardRadius.Value;
            if (guard <= 0f || ZDOMan.instance == null)
            {
                return false;
            }
            nearZDOs.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(point), new SimulationDistance(1, 0, true), nearZDOs);
            foreach (ZDO zdo in nearZDOs)
            {
                if (IsBlessedTree(zdo) && Utils.DistanceXZ(zdo.GetPosition(), point) < guard + radius)
                {
                    return true;
                }
            }
            return false;
        }

        private static HashSet<int> treePrefabs;

        /// <summary>A blessed tree by its ZDO alone: a TreeBase prefab with the blessed health.</summary>
        private static bool IsBlessedTree(ZDO zdo)
        {
            if (treePrefabs == null)
            {
                if (ZNetScene.instance == null)
                {
                    return false;
                }
                treePrefabs = new HashSet<int>();
                foreach (KeyValuePair<int, GameObject> pair in ZNetScene.instance.m_namedPrefabs)
                {
                    if (pair.Value != null && pair.Value.GetComponent<TreeBase>() != null)
                    {
                        treePrefabs.Add(pair.Key);
                    }
                }
            }
            return treePrefabs.Contains(zdo.GetPrefab()) && zdo.GetFloat(ZDOVars.s_health) >= BlessedThreshold;
        }

        /// <summary>
        /// The kinds a tree cycles through: every set of prefabs one sapling can grow into, plus
        /// the families from the config, merged wherever they share a name.
        /// </summary>
        private static List<List<string>> GetFamilies()
        {
            if (families != null || ZNetScene.instance == null)
            {
                return families;
            }
            families = new List<List<string>>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Plant plant = prefab != null ? prefab.GetComponent<Plant>() : null;
                if (plant != null && plant.m_grownPrefabs != null && plant.m_grownPrefabs.Length > 1)
                {
                    AddFamily(plant.m_grownPrefabs.Where(p => p != null).Select(p => p.name));
                }
            }
            foreach (string family in (Families.Value ?? "").Split(';'))
            {
                AddFamily(family.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
            }
            return families;
        }

        private static void AddFamily(IEnumerable<string> names)
        {
            List<string> kinds = names.Distinct().ToList();
            if (kinds.Count < 2)
            {
                return;
            }
            List<string> existing = families.FirstOrDefault(f => f.Intersect(kinds).Any());
            if (existing == null)
            {
                families.Add(kinds);
                return;
            }
            existing.AddRange(kinds.Where(k => !existing.Contains(k)));
        }

        /// <summary>The prefab this tree turns into next, or null when it belongs to no family.</summary>
        private static GameObject NextKind(GameObject tree)
        {
            List<List<string>> all = GetFamilies();
            if (tree == null || all == null)
            {
                return null;
            }
            string name = Utils.GetPrefabName(tree);
            List<string> family = all.FirstOrDefault(f => f.Contains(name));
            if (family == null)
            {
                return null;
            }
            int index = family.IndexOf(name);
            for (int step = 1; step < family.Count; step++)
            {
                GameObject next = ZNetScene.instance.GetPrefab(family[(index + step) % family.Count]);
                if (next != null)
                {
                    return next;
                }
            }
            return null;
        }

        /// <summary>The swing, the stamina and the tool delay the hammer charges for any click.</summary>
        private static void SwingHammer(Player player, ItemDrop.ItemData tool)
        {
            player.FaceLookDirection();
            if (tool != null)
            {
                player.m_zanim.SetTrigger(tool.m_shared.m_attack.m_attackAnimation);
            }
            player.UseStamina(player.GetBuildStamina());
            player.m_lastToolUseTime = Time.time;
        }

        // ---- 1. The blessing ----------------------------------------------------------------

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Awake))]
        private static class RememberTree
        {
            private static void Postfix(TreeBase __instance)
            {
                trees.Add(__instance);
            }
        }

        /// <summary>
        /// The look of a blessing, all borrowed from the game: the incinerator's lightning strikes
        /// the trunk, the forsaken power's red burst follows, and the boss stone's flickering glow
        /// lingers for a few seconds. Added to a tree when it is blessed and plays only for
        /// whoever blessed; afterwards the tree looks like any other.
        /// </summary>
        private class BlessedLook : MonoBehaviour
        {
            /// <summary>How long the glow stays after the burst.</summary>
            private const float GlowSeconds = 6f;

            private GameObject glow;

            /// <summary>Plays the blessing: strike, burst, glow.</summary>
            public void Bless()
            {
                Lift();
                Strike(transform.position);
                Invoke(nameof(Burst), 0.7f);
            }

            /// <summary>Cuts a show that is still playing.</summary>
            public void Lift()
            {
                CancelInvoke(nameof(Burst));
                if (glow != null)
                {
                    Destroy(glow);
                }
            }

            /// <summary>
            /// The incinerator's lightning without its damage: lightningAOE is a networked Aoe, so
            /// its visual children are cloned one by one under a holder that goes away after the
            /// longest of them has played.
            /// </summary>
            private static void Strike(Vector3 pos)
            {
                GameObject lightning = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("lightningAOE") : null;
                if (lightning == null)
                {
                    return;
                }
                GameObject holder = new GameObject("OdinsTree_lightning");
                holder.transform.position = pos;
                foreach (Transform child in lightning.transform)
                {
                    if (child.GetComponent<Aoe>() == null)
                    {
                        Instantiate(child.gameObject, holder.transform, false);
                    }
                }
                Destroy(holder, 10f);
            }

            /// <summary>
            /// The red burst of activating a forsaken power (fx_GP_Activation), and the glow that
            /// lingers after it.
            /// </summary>
            private void Burst()
            {
                StatusEffect power = ObjectDB.instance != null
                    ? ObjectDB.instance.GetStatusEffect("GP_Eikthyr".GetStableHashCode()) : null;
                if (power != null)
                {
                    power.m_startEffects.Create(transform.position, Quaternion.identity);
                }
                glow = Glow(transform);
                if (glow != null)
                {
                    Destroy(glow, GlowSeconds);
                }
            }

            /// <summary>
            /// The boss stone's active glow: a flickering red light and two looping particle
            /// systems. Its raven guide point is dropped before it can register, and the tree's
            /// random scale is cancelled so the glow is the same size on every tree.
            /// </summary>
            private static GameObject Glow(Transform tree)
            {
                GameObject stone = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("BossStone_Eikthyr") : null;
                BossStone boss = stone != null ? stone.GetComponent<BossStone>() : null;
                if (boss == null || boss.m_activeEffect == null)
                {
                    return null;
                }
                GameObject glow = Instantiate(boss.m_activeEffect, tree, false);
                foreach (GuidePoint guide in glow.GetComponentsInChildren<GuidePoint>(true))
                {
                    guide.enabled = false;
                    Destroy(guide.gameObject);
                }
                Vector3 scale = tree.lossyScale;
                glow.transform.localPosition = Vector3.zero;
                glow.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                glow.SetActive(true);
                return glow;
            }
        }

        /// <summary>
        /// The repair tool on a tree blesses it, or lifts the blessing. Vanilla's Repair only acts
        /// on a hovered piece, so on a tree nothing is taken away by skipping it.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
        private static class BlessOnRepair
        {
            private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem, Piece repairPiece)
            {
                TreeBase tree = AimedTree(__instance);
                if (tree == null)
                {
                    return true;
                }
                ZNetView nview = tree.m_nview;
                if (nview == null || !nview.IsValid() || !PrivateArea.CheckAccess(tree.transform.position))
                {
                    return false;
                }
                bool lift = IsBlessed(tree);
                nview.ClaimOwnership();
                nview.GetZDO().Set(ZDOVars.s_health, lift ? tree.m_health : BlessedHealth);
                BlessedLook look = tree.GetComponent<BlessedLook>();
                if (lift)
                {
                    if (look != null)
                    {
                        look.Lift();
                    }
                    if (repairPiece != null)
                    {
                        repairPiece.m_placeEffect.Create(tree.transform.position, tree.transform.rotation, null, 1f, -1, __instance.GetZDOID());
                    }
                }
                else
                {
                    if (look == null)
                    {
                        look = tree.gameObject.AddComponent<BlessedLook>();
                    }
                    look.Bless();
                }
                __instance.Message(MessageHud.MessageType.TopLeft,
                    lift ? "The blessing is lifted" : "This tree is blessed by Odin");
                SwingHammer(__instance, toolItem);
                return false;
            }
        }

        /// <summary>
        /// A blessed tree does not take hits, like the ancient root: no damage text, no shake, no
        /// chips, no noise. Damage is what the hitter's client calls; RPC_Damage is what the
        /// tree's owner runs, and catches hits from clients without the mod.
        /// </summary>
        [HarmonyPatch(typeof(TreeBase))]
        private static class UnhittableWhenBlessed
        {
            [HarmonyPrefix, HarmonyPatch(nameof(TreeBase.Damage))]
            private static bool Damage(TreeBase __instance) => !IsBlessed(__instance);

            [HarmonyPrefix, HarmonyPatch(nameof(TreeBase.RPC_Damage))]
            private static bool RPC_Damage(TreeBase __instance) => !IsBlessed(__instance);
        }

        /// <summary>
        /// The hints go straight into the HUD's crosshair label, after the game has written its
        /// own (which in build mode is always empty, since the hover object is cleared). The label
        /// is a TextMeshProUGUI and lib/ has no TextMeshPro to reference, so it is set by name.
        /// </summary>
        [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
        private static class TreeHoverText
        {
            private static void Postfix(Hud __instance, Player player)
            {
                string text = HoverText(player, AimedObject(player));
                if (text.Length == 0)
                {
                    return;
                }
                Traverse.Create(__instance).Field("m_hoverName").Property("text").SetValue(text);
                if (__instance.m_crosshair != null)
                {
                    __instance.m_crosshair.color = Color.yellow;
                }
            }
        }

        // ---- 2. The ground under a blessed tree ---------------------------------------------

        /// <summary>The hoe's and the cultivator's ghost turns red near a blessed tree.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        private static class GuardGhost
        {
            private static void Postfix(Player __instance)
            {
                GameObject ghost = __instance.m_placementGhost;
                if (ghost == null || !ghost.activeSelf || __instance.m_placementStatus != Player.PlacementStatus.Valid)
                {
                    return;
                }
                TerrainOp op = ghost.GetComponentInChildren<TerrainOp>();
                if (op == null || !NearBlessedTree(ghost.transform.position, op.GetRadius()))
                {
                    return;
                }
                __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                __instance.SetPlacementGhostValid(false);
            }
        }

        /// <summary>
        /// Every terrain change on this client - hoe, cultivator, pickaxe - passes through a
        /// TerrainOp's Awake. Near a blessed tree it is dropped before it reaches the terrain.
        /// </summary>
        [HarmonyPatch(typeof(TerrainOp), nameof(TerrainOp.Awake))]
        private static class GuardTerrainOp
        {
            private static bool Prefix(TerrainOp __instance)
            {
                if (TerrainOp.m_forceDisableTerrainOps || !NearBlessedTree(__instance.transform.position, __instance.GetRadius()))
                {
                    return true;
                }
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Odin's tree guards this ground");
                UnityEngine.Object.Destroy(__instance.gameObject);
                return false;
            }
        }

        /// <summary>
        /// The terrain's owner refuses operations near its blessed trees and tells whoever sent
        /// them. This is the check that reaches players without the mod, as long as the owner
        /// has it - which <see cref="Warden"/> sees to on a server.
        /// </summary>
        [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.RPC_ApplyOperation))]
        private static class GuardTerrainOwner
        {
            private static bool Prefix(TerrainComp __instance, long sender, ZPackage pkg)
            {
                if (TerrainGuardRadius.Value <= 0f || __instance.m_nview == null || !__instance.m_nview.IsOwner())
                {
                    return true;
                }
                int start = pkg.GetPos();
                Vector3 pos = pkg.ReadVector3();
                if (pkg.ReadBool())
                {
                    pkg.ReadVector3();
                }
                TerrainOp.Settings settings = TerrainOp.Settings.Deserialize(pkg);
                pkg.SetPos(start);
                if (settings == null || !NearBlessedTreeZDO(pos, settings.GetRadius()))
                {
                    return true;
                }
                if (ZRoutedRpc.instance != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(sender, "ShowMessage", (int)MessageHud.MessageType.Center, "Odin's tree guards this ground");
                }
                return false;
            }
        }

        /// <summary>
        /// Makes <see cref="GuardTerrainOwner"/> reach players without the mod. Every terrain
        /// change is sent to the owner of the zone's terrain compiler, and the server hands that
        /// ownership out every two seconds, once per peer (ZDOMan.ReleaseNearbyZDOS). So on the
        /// server, after each hand-out, the compilers of the zones around a blessed tree near
        /// that peer are taken back for the server itself, their zones are kept loaded (the way
        /// the server keeps the zones around spawn) and the compilers are slipped into the list
        /// of objects ZNetScene instantiates. The change then arrives at a real TerrainComp on
        /// the server, where the guard refuses it. A zone with a blessed tree but no compiler
        /// yet gets one from the server, before a player's first dig could create it as owner.
        /// </summary>
        private static class Warden
        {
            /// <summary>Guard radius plus the widest terrain operation, with room to spare.</summary>
            private const float Reach = 16f;

            /// <summary>How long a compiler stays instantiated after a peer was last near it.</summary>
            private const float Forget = 10f;

            private static readonly int CompilerPrefab = "_TerrainCompiler".GetStableHashCode();

            /// <summary>The compilers held, each with the time a peer was last seen near it.</summary>
            private static readonly Dictionary<ZDOID, float> held = new Dictionary<ZDOID, float>();
            private static readonly HashSet<Vector2s> zones = new HashSet<Vector2s>();
            private static readonly List<ZDO> sector = new List<ZDO>();
            private static readonly List<ZDOID> stale = new List<ZDOID>();

            [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ReleaseNearbyZDOS))]
            private static class TakeCompilers
            {
                private static void Postfix(ZDOMan __instance)
                {
                    if (TerrainGuardRadius.Value <= 0f || ZoneSystem.instance == null)
                    {
                        return;
                    }
                    zones.Clear();
                    foreach (ZDO zdo in __instance.m_tempNearObjects)
                    {
                        if (IsBlessedTree(zdo))
                        {
                            AddZonesInReach(zdo.GetPosition());
                        }
                    }
                    foreach (Vector2s zone in zones)
                    {
                        bool fresh = ZoneSystem.instance.PokeLocalZone(zone);
                        sector.Clear();
                        ZDOMan.instance.FindSectorObjects(zone, new SimulationDistance(0, 0, true), sector);
                        ZDO compiler = sector.Find(z => z.GetPrefab() == CompilerPrefab);
                        if (compiler == null && !fresh && ZoneSystem.instance.IsZoneLoaded(zone))
                        {
                            Heightmap hmap = Heightmap.FindHeightmap(ZoneSystem.GetZonePos(zone));
                            TerrainComp comp = hmap != null ? hmap.GetAndCreateTerrainCompiler() : null;
                            compiler = comp != null && comp.m_nview != null ? comp.m_nview.GetZDO() : null;
                        }
                        if (compiler != null)
                        {
                            Hold(compiler);
                        }
                    }
                }
            }

            /// <summary>The zones whose ground lies within reach of a blessed tree at pos.</summary>
            private static void AddZonesInReach(Vector3 pos)
            {
                Vector2s home = ZoneSystem.GetZone(pos);
                float half = ZoneSystem.instance.m_zoneSize * 0.5f;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        Vector2s zone = new Vector2s(home.x + dx, home.y + dz);
                        Vector3 centre = ZoneSystem.GetZonePos(zone);
                        float x = Mathf.Max(Mathf.Abs(pos.x - centre.x) - half, 0f);
                        float z = Mathf.Max(Mathf.Abs(pos.z - centre.z) - half, 0f);
                        if (x * x + z * z < Reach * Reach)
                        {
                            zones.Add(zone);
                        }
                    }
                }
            }

            private static void Hold(ZDO compiler)
            {
                long me = ZDOMan.GetSessionID();
                if (compiler.GetOwner() != me)
                {
                    compiler.SetOwner(me);
                }
                held[compiler.m_uid] = Time.time;
            }

            /// <summary>
            /// The hand-out gives a peer whatever the server owns outside the server's own area,
            /// and every owner change sends the compiler's whole terrain data again. So a held
            /// compiler counts as inside the server's area.
            /// </summary>
            [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.IsInPeerActiveArea))]
            private static class KeepOwnership
            {
                private static void Postfix(Vector3 point, long uid, ref bool __result)
                {
                    if (__result || held.Count == 0 || uid != ZDOMan.GetSessionID())
                    {
                        return;
                    }
                    foreach (ZDOID id in held.Keys)
                    {
                        ZDO zdo = ZDOMan.instance.GetZDO(id);
                        if (zdo != null && zdo.GetPosition() == point)
                        {
                            __result = true;
                            return;
                        }
                    }
                }
            }

            /// <summary>The held compilers count as near, so ZNetScene creates and keeps them.</summary>
            [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateObjects))]
            private static class KeepCompilers
            {
                private static void Prefix(List<ZDO> currentNearObjects)
                {
                    if (held.Count == 0)
                    {
                        return;
                    }
                    stale.Clear();
                    foreach (KeyValuePair<ZDOID, float> pair in held)
                    {
                        ZDO zdo = ZDOMan.instance.GetZDO(pair.Key);
                        if (zdo == null || Time.time - pair.Value > Forget)
                        {
                            stale.Add(pair.Key);
                        }
                        else if (ZoneSystem.instance.IsZoneLoaded(zdo.GetSector()) && !currentNearObjects.Contains(zdo))
                        {
                            currentNearObjects.Add(zdo);
                        }
                    }
                    foreach (ZDOID id in stale)
                    {
                        held.Remove(id);
                    }
                }
            }
        }

        // ---- 3. Tree kinds ------------------------------------------------------------------

        /// <summary>
        /// The hammer's remove click on an unblessed tree of a known family swaps it for the next
        /// kind, at the same spot, rotation and size. Vanilla's RemovePiece needs a piece, so on a
        /// tree it would do nothing anyway.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.RemovePiece))]
        private static class CycleOnRemove
        {
            private static bool Prefix(Player __instance, ref bool __result)
            {
                GameObject aimed = AimedObject(__instance);
                ZNetView nview = aimed != null ? aimed.GetComponentInParent<ZNetView>() : null;
                GameObject next = nview != null ? NextKind(nview.gameObject) : null;
                if (next == null)
                {
                    return true;
                }
                __result = false;
                if (IsBlessed(nview.GetComponent<TreeBase>()))
                {
                    __instance.Message(MessageHud.MessageType.Center, "Blessed by Odin - lift the blessing first");
                    return false;
                }
                if (!nview.IsValid() || !PrivateArea.CheckAccess(nview.transform.position))
                {
                    return false;
                }
                Transform old = nview.transform;
                nview.ClaimOwnership();
                GameObject spawned = UnityEngine.Object.Instantiate(next, old.position, old.rotation);
                spawned.GetComponent<ZNetView>()?.SetLocalScale(old.localScale);
                nview.Destroy();
                SwingHammer(__instance, __instance.GetRightItem());
                return false;
            }
        }

        // ---- 4. Twerking grows saplings -----------------------------------------------------

        /// <summary>
        /// Counts crouch presses. <c>crouch</c> is true for one frame per press (the controller
        /// sends the button's rising edge), so three of them within reach of each other start
        /// the twerk, and it lasts until the presses stop.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static class CountCrouches
        {
            private static void Prefix(Player __instance, bool crouch)
            {
                if (!crouch || __instance != Player.m_localPlayer)
                {
                    return;
                }
                float now = Time.time;
                crouches = now - lastCrouch <= MaxCrouchInterval ? crouches + 1 : 1;
                lastCrouch = now;
            }
        }

        private static bool Twerking =>
            crouches >= TwerkCrouches && Time.time - lastCrouch <= MaxCrouchInterval;

        /// <summary>
        /// While the twerk lasts, every healthy sapling within reach glows green and its planting
        /// time slides back by growTime × dt / TwerkGrowSeconds, so after TwerkGrowSeconds of
        /// twerking it is grown whatever its age, and <see cref="Plant.Grow"/> is called.
        /// </summary>
        void Update()
        {
            Player player = Player.m_localPlayer;
            float seconds = TwerkGrowSeconds.Value;
            if (player == null || ZNet.instance == null || seconds <= 0f || !Twerking)
            {
                if (glowing.Count > 0)
                {
                    StopGlowing();
                }
                return;
            }
            float now = Time.time;
            List<Plant> near = new List<Plant>();
            foreach (SlowUpdate slow in SlowUpdate.GetAllInstaces())
            {
                Plant plant = slow as Plant;
                ZNetView nview = plant != null ? plant.m_nview : null;
                if (nview == null || !nview.IsValid() || plant.m_grownPrefabs == null || plant.m_grownPrefabs.Length == 0
                    || Utils.DistanceXZ(plant.transform.position, player.transform.position) > TwerkRadius)
                {
                    continue;
                }
                plant.UpdateHealth(plant.TimeSincePlanted());
                if (plant.GetStatus() != Plant.Status.Healthy)
                {
                    if (now - lastWhyNotGrowing > 2f)
                    {
                        lastWhyNotGrowing = now;
                        player.Message(MessageHud.MessageType.TopLeft, plant.GetHoverText());
                    }
                    continue;
                }
                near.Add(plant);
            }
            foreach (Plant plant in glowing.ToList())
            {
                if (plant == null || !near.Contains(plant))
                {
                    Unglow(plant);
                }
            }
            foreach (Plant plant in near)
            {
                Glow(plant);
                plant.m_nview.ClaimOwnership();
                ZDO zdo = plant.m_nview.GetZDO();
                float growTime = plant.GetGrowTime();
                long planted = zdo.GetLong(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks);
                long shift = (long)(growTime * Time.deltaTime / seconds * TimeSpan.TicksPerSecond);
                zdo.Set(ZDOVars.s_plantTime, planted - shift);
                // Lets the next slow update swap in the half-grown model without the 10 s wait.
                plant.m_updateTime = 0f;
                if (plant.TimeSincePlanted() > growTime)
                {
                    Unglow(plant);
                    plant.Grow();
                }
            }
        }

        /// <summary>Tints a sapling green the way the game highlights a piece.</summary>
        private static void Glow(Plant plant)
        {
            if (glowing.Contains(plant) || MaterialMan.instance == null)
            {
                return;
            }
            MaterialMan.instance.SetValue(plant.gameObject, ShaderProps._Color, TwerkGreen, true);
            MaterialMan.instance.SetValue(plant.gameObject, ShaderProps._EmissionColor, TwerkGreen * 0.4f);
            glowing.Add(plant);
        }

        private static void Unglow(Plant plant)
        {
            glowing.Remove(plant);
            if (plant != null && MaterialMan.instance != null)
            {
                MaterialMan.instance.ResetValue(plant.gameObject, ShaderProps._Color);
                MaterialMan.instance.ResetValue(plant.gameObject, ShaderProps._EmissionColor);
            }
        }

        private static void StopGlowing()
        {
            foreach (Plant plant in glowing.ToList())
            {
                Unglow(plant);
            }
            glowing.Clear();
        }
    }
}
