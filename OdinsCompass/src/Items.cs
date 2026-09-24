using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// The eight compasses, one item per tier, cloned from the game's Wishbone: a utility item
    /// with the wishbone's model and its own name, icon, description, recipe and status effect.
    /// Separate items rather than one item with qualities, because the vanilla upgrade maths
    /// scales a single resource list and needs a workbench of level N for quality N (ROADMAP.md,
    /// section 1). Tier N's recipe consumes tier N-1's compass plus the tier's materials.
    /// <para>
    /// Registration is repeated for every item database the game builds: the main menu's
    /// (ObjectDB.CopyOtherDB from FejdStartup) and the game scene's (ObjectDB.Awake), plus the
    /// network scene's prefab list (ZNetScene.Awake), so a dropped compass can be spawned by
    /// its prefab hash. The clones themselves are made once and kept.
    /// </para>
    /// </summary>
    internal static class Items
    {
        internal const string PrefabPrefix = "OdinsCompass";
        private const string SourceItem = "Wishbone";
        private const string WorkbenchName = "$piece_workbench";
        private const string IconName = "compass";

        private static GameObject root;
        private static readonly GameObject[] prefabs = new GameObject[OdinsCompassPlugin.TierNames.Length];
        private static readonly Recipe[] recipes = new Recipe[OdinsCompassPlugin.TierNames.Length];
        private static SE_Compass effect;

        /// <summary>The tier of an item, 1..8, or 0 when it is not a compass.</summary>
        internal static int TierOf(ItemDrop.ItemData item)
        {
            GameObject prefab = item?.m_dropPrefab;
            return prefab != null ? TierOf(prefab.name) : 0;
        }

        internal static int TierOf(string prefabName)
        {
            if (prefabName == null || !prefabName.StartsWith(PrefabPrefix) || prefabName.Length != PrefabPrefix.Length + 1)
            {
                return 0;
            }
            int tier = prefabName[PrefabPrefix.Length] - '0';
            return tier >= 1 && tier <= prefabs.Length ? tier : 0;
        }

        internal static string PrefabName(int tier)
        {
            return PrefabPrefix + tier;
        }

        /// <summary>
        /// Makes the clones if they do not exist yet. The source is whichever holder of the
        /// wishbone prefab the caller has: the item database or the network scene. A missing
        /// wishbone is no news here - the main menu's databases wake empty and are copies
        /// without it - so this stays quiet; the network scene's registration, the last one
        /// before play, is the one that complains.
        /// </summary>
        private static bool Ensure(GameObject wishbone)
        {
            if (prefabs[0] != null)
            {
                return true;
            }
            if (wishbone == null)
            {
                return false;
            }
            ItemDrop source = wishbone.GetComponent<ItemDrop>();
            if (source == null)
            {
                Debug.LogWarning("[OdinsCompass] " + SourceItem + " has no ItemDrop; the compass does not exist in this game");
                return false;
            }

            // An inactive parent keeps Awake from running on the clones, which would register
            // them as dropped items in the world. DontDestroyOnLoad keeps them across the
            // menu / game scene switches.
            root = new GameObject(PrefabPrefix + "Prefabs");
            root.SetActive(false);
            Object.DontDestroyOnLoad(root);

            effect = ScriptableObject.CreateInstance<SE_Compass>();
            effect.name = SE_Compass.EffectName;
            effect.m_name = "$oc_compass_effect";
            effect.m_tooltip = "$oc_compass_effect_tooltip";
            effect.m_icon = Icons.Get(IconName);

            Sprite icon = Icons.Get(IconName);
            for (int i = 0; i < prefabs.Length; i++)
            {
                int tier = i + 1;
                GameObject clone = Object.Instantiate(wishbone, root.transform);
                clone.name = PrefabName(tier);
                ItemDrop drop = clone.GetComponent<ItemDrop>();
                if (ReferenceEquals(drop.m_itemData.m_shared, source.m_itemData.m_shared))
                {
                    // Instantiate copies serialized plain classes by value, so this is not
                    // expected; if it ever happens, changing the clone would rename the wishbone.
                    drop.m_itemData.m_shared = (ItemDrop.ItemData.SharedData)AccessTools
                        .Method(typeof(object), "MemberwiseClone").Invoke(source.m_itemData.m_shared, null);
                    Debug.LogWarning("[OdinsCompass] the clone shared the wishbone's item data; copied it by hand");
                }
                ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
                shared.m_name = "$oc_compass" + tier;
                shared.m_description = Description(tier);
                shared.m_maxQuality = 1;
                shared.m_maxStackSize = 1;
                shared.m_equipStatusEffect = effect;
                if (icon != null)
                {
                    shared.m_icons = new[] { icon };
                }
                prefabs[i] = clone;
            }
            return true;
        }

        /// <summary>
        /// The item's tooltip text: what a compass is, then what this tier's one knows, as the
        /// group names' tokens - the tooltip localizes them when it shows.
        /// </summary>
        private static string Description(int tier)
        {
            List<string> names = new List<string>();
            foreach (TargetGroup group in TargetGroup.ForTier(tier))
            {
                names.Add(group.Name);
            }
            string knows = names.Count > 0 ? string.Join(", ", names) : "$oc_compass_knows_nothing";
            return "$oc_compass_desc\n\n$oc_compass_knows: " + knows;
        }

        /// <summary>
        /// Puts the items, their recipes and the status effect into an item database that does
        /// not have them yet. The menu's database shares its lists with the prefab it copied, so
        /// a later database may or may not already hold them: every list is checked.
        /// </summary>
        private static void Register(ObjectDB db)
        {
            if (db == null || !Ensure(db.GetItemPrefab(SourceItem)))
            {
                return;
            }
            bool changed = false;
            foreach (GameObject prefab in prefabs)
            {
                if (!db.m_items.Contains(prefab))
                {
                    db.m_items.Add(prefab);
                    changed = true;
                }
            }
            if (!db.m_StatusEffects.Contains(effect))
            {
                db.m_StatusEffects.Add(effect);
            }
            if (changed)
            {
                db.UpdateRegisters();
            }
            RegisterRecipes(db);
        }

        private static void RegisterRecipes(ObjectDB db)
        {
            CraftingStation workbench = Workbench(db);
            if (workbench == null)
            {
                Debug.LogWarning("[OdinsCompass] no recipe uses " + WorkbenchName + "; the compass cannot be crafted");
                return;
            }
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (recipes[i] == null)
                {
                    recipes[i] = MakeRecipe(db, i + 1, workbench);
                }
                if (recipes[i] != null && !db.m_recipes.Contains(recipes[i]))
                {
                    db.m_recipes.Add(recipes[i]);
                }
            }
        }

        /// <summary>
        /// The tier's recipe from its config string, plus the compass of the tier below. An item
        /// name that resolves to nothing is logged and left out; a tier 1 recipe left with no
        /// resources at all is not added, since a free compass is worse than none.
        /// </summary>
        private static Recipe MakeRecipe(ObjectDB db, int tier, CraftingStation station)
        {
            List<Piece.Requirement> resources = new List<Piece.Requirement>();
            if (tier > 1)
            {
                resources.Add(Requirement(prefabs[tier - 2].GetComponent<ItemDrop>(), 1));
            }
            string text = OdinsCompassPlugin.TierRecipes[tier - 1].Value ?? "";
            foreach (string entry in text.Split(','))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }
                int colon = trimmed.IndexOf(':');
                string name = colon > 0 ? trimmed.Substring(0, colon).Trim() : trimmed;
                int amount = 1;
                if (colon > 0 && !int.TryParse(trimmed.Substring(colon + 1).Trim(), out amount))
                {
                    amount = 1;
                }
                GameObject prefab = db.GetItemPrefab(name);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    Debug.LogWarning("[OdinsCompass] tier " + tier + " recipe: no item named '" + name + "', left out");
                    continue;
                }
                resources.Add(Requirement(drop, Mathf.Max(1, amount)));
            }
            if (resources.Count == 0)
            {
                Debug.LogWarning("[OdinsCompass] tier " + tier + " recipe has no usable item at all; not added");
                return null;
            }
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_" + PrefabName(tier);
            recipe.m_item = prefabs[tier - 1].GetComponent<ItemDrop>();
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = station;
            recipe.m_minStationLevel = 1;
            recipe.m_resources = resources.ToArray();
            return recipe;
        }

        private static Piece.Requirement Requirement(ItemDrop item, int amount)
        {
            return new Piece.Requirement { m_resItem = item, m_amount = amount, m_amountPerLevel = 0, m_recover = false };
        }

        /// <summary>
        /// The workbench, borrowed from the first vanilla recipe crafted at one - the database
        /// exists in the main menu too, where there is no network scene to look the prefab up in.
        /// </summary>
        private static CraftingStation Workbench(ObjectDB db)
        {
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe != null && recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == WorkbenchName)
                {
                    return recipe.m_craftingStation;
                }
            }
            return null;
        }

        /// <summary>The main menu's database, copied from the prefab when the menu starts.</summary>
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class OnCopy
        {
            private static void Postfix(ObjectDB __instance)
            {
                Register(__instance);
            }
        }

        /// <summary>The game scene's database.</summary>
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static class OnAwake
        {
            private static void Postfix(ObjectDB __instance)
            {
                Register(__instance);
            }
        }

        /// <summary>
        /// The network scene's prefab list, so a compass lying in the world (a ZDO with the
        /// clone's prefab hash) can be spawned. Awake filled m_namedPrefabs from m_prefabs
        /// before this runs, so both get the clones. The wishbone comes from the scene's own list
        /// in case the item database has not woken yet; a world whose scene has no wishbone at
        /// all is the one case worth a warning.
        /// </summary>
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class OnSceneAwake
        {
            private static void Postfix(ZNetScene __instance)
            {
                if (__instance == null)
                {
                    return;
                }
                GameObject fromDb = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(SourceItem) : null;
                if (!Ensure(__instance.GetPrefab(SourceItem)) && !Ensure(fromDb))
                {
                    Debug.LogWarning("[OdinsCompass] no " + SourceItem + " prefab to clone; the compass does not exist in this world");
                    return;
                }
                foreach (GameObject prefab in prefabs)
                {
                    int hash = prefab.name.GetStableHashCode();
                    if (!__instance.m_namedPrefabs.ContainsKey(hash))
                    {
                        __instance.m_prefabs.Add(prefab);
                        __instance.m_namedPrefabs.Add(hash, prefab);
                    }
                }
            }
        }
    }
}
