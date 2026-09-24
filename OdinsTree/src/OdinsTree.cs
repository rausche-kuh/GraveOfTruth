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

        /// <summary>The health a blessed tree gets. Finite, so other mods' maths stay sane.</summary>
        private const float BlessedHealth = 1e9f;

        /// <summary>Anything at or above this is blessed - no vanilla tree comes near it.</summary>
        private const float BlessedThreshold = 1e8f;

        /// <summary>How far from the player a sapling still feels the dodge, in metres.</summary>
        private const float TwerkRadius = 3f;

        /// <summary>A pause longer than this between two dodges does not count as twerking.</summary>
        private const float MaxDodgeInterval = 1.5f;

        internal static ConfigEntry<float> TerrainGuardRadius;
        internal static ConfigEntry<float> TwerkGrowSeconds;
        internal static ConfigEntry<string> Families;

        /// <summary>Every loaded tree; destroyed ones turn null and are dropped on the next scan.</summary>
        private static readonly List<TreeBase> trees = new List<TreeBase>();

        /// <summary>The tree families, built once the prefabs are known - see <see cref="GetFamilies"/>.</summary>
        private static List<List<string>> families;

        private static float lastDodge = -999f;
        private static float lastWhyNotGrowing = -999f;

        void Awake()
        {
            TerrainGuardRadius = Config.Bind("Blessing", "TerrainGuardRadius", 3f, new ConfigDescription(
                "How far from a blessed tree's trunk the ground cannot be dug, raised or levelled, " +
                "in metres. 0 lets the ground be changed right up to the trunk.",
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

        private static TreeBase HoveredTree(Player player)
        {
            GameObject hover = player != null ? player.GetHoverObject() : null;
            return hover != null ? hover.GetComponentInParent<TreeBase>() : null;
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
                if (__instance.GetComponent<Hoverable>() == null)
                {
                    __instance.gameObject.AddComponent<TreeHint>();
                }
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
                TreeBase tree = HoveredTree(__instance);
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
                if (repairPiece != null)
                {
                    repairPiece.m_placeEffect.Create(tree.transform.position, tree.transform.rotation, null, 1f, -1, __instance.GetZDOID());
                }
                __instance.Message(MessageHud.MessageType.TopLeft,
                    lift ? "The blessing is lifted" : "This tree is blessed by Odin");
                SwingHammer(__instance, toolItem);
                return false;
            }
        }

        /// <summary>
        /// Trees are not Hoverable, so every tree gets this one: the hammer's hints while in
        /// build mode, nothing otherwise (an empty text keeps the crosshair plain).
        /// </summary>
        private class TreeHint : MonoBehaviour, Hoverable
        {
            public string GetHoverText()
            {
                Player player = Player.m_localPlayer;
                TreeBase tree = GetComponent<TreeBase>();
                if (player == null || tree == null || !player.InPlaceMode())
                {
                    return "";
                }
                bool blessed = IsBlessed(tree);
                string text = blessed ? "Blessed by Odin" : "";
                if (player.GetSelectedPiece()?.m_repairPiece == true)
                {
                    text += blessed ? "\n[LMB] Lift blessing" : "\n[LMB] Bless";
                }
                if (!blessed && NextKind(gameObject) != null)
                {
                    text += "\n[RMB] Next kind";
                }
                return text.TrimStart('\n');
            }

            public string GetHoverName()
            {
                return "";
            }

            public float GetHoverOffset()
            {
                return 0f;
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

        /// <summary>The terrain's owner refuses other players' operations near its blessed trees.</summary>
        [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.DoOperation))]
        private static class GuardTerrainOwner
        {
            private static bool Prefix(Vector3 pos, TerrainOp.Settings modifier)
            {
                return modifier == null || !NearBlessedTree(pos, modifier.GetRadius());
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
                GameObject hover = __instance.GetHoverObject();
                ZNetView nview = hover != null ? hover.GetComponentInParent<ZNetView>() : null;
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
        /// A dodge that actually starts (stamina paid, timer consumed) moves every sapling within
        /// reach closer to grown, by the time since the last dodge.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateDodge))]
        private static class TwerkGrows
        {
            private static void Prefix(Player __instance, out bool __state)
            {
                __state = __instance.m_queuedDodgeTimer > 0f;
            }

            private static void Postfix(Player __instance, bool __state)
            {
                float seconds = TwerkGrowSeconds.Value;
                if (!__state || __instance.m_queuedDodgeTimer != 0f || __instance != Player.m_localPlayer
                    || seconds <= 0f || ZNet.instance == null)
                {
                    return;
                }
                float now = Time.time;
                float interval = Mathf.Min(now - lastDodge, MaxDodgeInterval);
                lastDodge = now;
                foreach (SlowUpdate slow in SlowUpdate.GetAllInstaces().ToList())
                {
                    Plant plant = slow as Plant;
                    ZNetView nview = plant != null ? plant.m_nview : null;
                    if (nview == null || !nview.IsValid() || plant.m_grownPrefabs == null || plant.m_grownPrefabs.Length == 0
                        || Utils.DistanceXZ(plant.transform.position, __instance.transform.position) > TwerkRadius)
                    {
                        continue;
                    }
                    plant.UpdateHealth(plant.TimeSincePlanted());
                    if (plant.GetStatus() != Plant.Status.Healthy)
                    {
                        if (now - lastWhyNotGrowing > 2f)
                        {
                            lastWhyNotGrowing = now;
                            __instance.Message(MessageHud.MessageType.TopLeft, plant.GetHoverText());
                        }
                        continue;
                    }
                    nview.ClaimOwnership();
                    ZDO zdo = nview.GetZDO();
                    float growTime = plant.GetGrowTime();
                    long planted = zdo.GetLong(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks);
                    long shift = (long)(growTime * interval / seconds * TimeSpan.TicksPerSecond);
                    zdo.Set(ZDOVars.s_plantTime, planted - shift);
                    if (plant.TimeSincePlanted() > growTime)
                    {
                        plant.Grow();
                    }
                }
            }
        }
    }
}
