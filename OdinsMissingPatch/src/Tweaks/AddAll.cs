using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Shift + Use on anything that takes items up to a cap puts in everything that fits, in one
    /// go: the fuel of a fire, a smelter, an oven or a shield generator, the ore of a smelter,
    /// the food of a cooking station, the bolts of a ballista. Use alone still adds one, as ever,
    /// and so does Shift + Use when the single unit that fits is in the backpack anyway, so the
    /// game's own messages explain a full station or nothing to put in. The hover text names what
    /// a Shift + Use would add.
    ///
    /// Everything it adds - fuel, ore, food and bolts alike - comes out of the backpack first and
    /// then out of the chests around the player: the shared reach (see NearbyChests) is open, at
    /// this tweak's own range, while a plan is drawn up and while its items are paid for, so the
    /// counts and the spends see the chests the way nearby crafting and nearby fuel do. Every
    /// unit goes in through the station's own RPC, one call per unit, so the station's owner applies each
    /// exactly as it applies a single Use; a fire has an amount RPC and gets one call. The count
    /// is bounded here, before the RPCs go out, because a client that does not own the station
    /// would not see its cap move until the owner had answered.
    /// </summary>
    internal sealed class AddAll : Tweak
    {
        internal static readonly AddAll Instance = new AddAll();

        private AddAll() { }

        private ConfigEntry<float> range;

        internal override string Section => "Add All";

        protected override string Summary =>
            "Shift + Use on a fire, smelter, oven, cooking station, shield generator or ballista " +
            "adds everything that fits, instead of one item: out of your backpack first and then " +
            "out of the chests around you.";

        protected override void Bind(ConfigFile config)
        {
            range = config.Bind(Section, "Range", 20f, new ConfigDescription(
                "How far from you a chest may stand, in metres, for a Shift + Use to reach what it holds.",
                new AcceptableValueRange<float>(1f, 100f)));
        }

        /// <summary>One kind of item and how many of it a Shift + Use would put in.</summary>
        private struct Batch
        {
            public string Name;
            public string Prefab;
            public bool Cheated;
            public int Count;
        }

        private static readonly List<Batch> Plan = new List<Batch>();

        private static readonly StringBuilder Text = new StringBuilder();

        /// <summary>The Shift + Use of the local player, on the frame the key goes down.</summary>
        private static bool Wants(Humanoid user, bool hold, bool alt)
        {
            return Instance.On && alt && !hold && user != null && user == Player.m_localPlayer;
        }

        private static bool Hovering => Instance.On && Player.m_localPlayer != null;

        // ---- The reach: counting and taking through the chests around the player --------------

        /// <summary>
        /// The chests around the local player, in reach for as long as this lives. Opened around
        /// a plan and around the spend that follows it, so both see the same items; it nests
        /// inside the scope NearbyFuel opens on the very same Use without either disturbing the
        /// other.
        /// </summary>
        private readonly struct Reach : IDisposable
        {
            private readonly bool entered;

            internal Reach(Humanoid user)
            {
                entered = Instance.On && user != null && user == Player.m_localPlayer;
                if (entered)
                {
                    NearbyChests.EnterReach(Instance.range.Value);
                }
            }

            public void Dispose()
            {
                if (entered)
                {
                    NearbyChests.LeaveReach();
                }
            }
        }

        private static int Count(Humanoid user, string name)
        {
            using (new Reach(user))
            {
                return user.GetInventory().CountItems(name);
            }
        }

        private static void Take(Humanoid user, string name, int amount)
        {
            using (new Reach(user))
            {
                user.GetInventory().RemoveItem(name, amount);
            }
        }

        /// <summary>
        /// A unit of the item to copy the cheat flag and the prefab off: the backpack's first,
        /// else the first a chest in reach holds. Only ever asked about an item a count has
        /// already found, so the chest walk is never paid for something nobody has.
        /// </summary>
        private static ItemDrop.ItemData Sample(Humanoid user, string name)
        {
            ItemDrop.ItemData item = user.GetInventory().GetItem(name);
            if (item != null || !Instance.On || Player.m_localPlayer == null)
            {
                return item;
            }
            foreach (Container chest in NearbyChests.Find(Player.m_localPlayer.transform.position, Instance.range.Value))
            {
                item = chest.GetInventory().GetItem(name);
                if (item != null)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// The ammunition the ballista would load: the game's own lookup on the backpack, and
        /// failing that the same lookup on each chest in reach, nearest first. It picks the type,
        /// so an empty ballista loads whatever the nearest chest has and a loaded one only ever
        /// gets more of what is already in it.
        /// </summary>
        private static ItemDrop.ItemData Ammo(Turret turret, Humanoid user)
        {
            ItemDrop.ItemData item = turret.FindAmmoItem(user.GetInventory(), onlyCurrentlyLoadableType: true);
            if (item != null || !Instance.On || Player.m_localPlayer == null)
            {
                return item;
            }
            foreach (Container chest in NearbyChests.Find(Player.m_localPlayer.transform.position, Instance.range.Value))
            {
                item = turret.FindAmmoItem(chest.GetInventory(), onlyCurrentlyLoadableType: true);
                if (item != null)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// True when the game's own Use would do exactly what this plan says: nothing at all, or
        /// the single unit the backpack already pays for. The Use is handed back then, so the
        /// vanilla message, effect and skill explain a full station or an empty backpack.
        /// </summary>
        private static bool LeaveToGame(Humanoid user, List<Batch> plan)
        {
            int total = Total(plan);
            return total == 0
                || (total == 1 && NearbyChests.Carried(user.GetInventory(), plan[0].Name, -1, true) > 0);
        }

        /// <summary>
        /// How many units a smelter, an oven or a shield generator still takes: the game adds
        /// while the fuel is at most one below the cap, so a fraction burnt off makes room for
        /// one more.
        /// </summary>
        private static int FuelRoom(float fuel, int maxFuel)
        {
            if (fuel > maxFuel - 1)
            {
                return 0;
            }
            return Mathf.FloorToInt(maxFuel - 1 - fuel) + 1;
        }

        private static int Total(List<Batch> plan)
        {
            int total = 0;
            foreach (Batch batch in plan)
            {
                total += batch.Count;
            }
            return total;
        }

        /// <summary>"6 $item_wood, 2 $item_coal", for the message and the hover line.</summary>
        private static string Describe(List<Batch> plan)
        {
            Text.Length = 0;
            foreach (Batch batch in plan)
            {
                if (Text.Length > 0)
                {
                    Text.Append(", ");
                }
                Text.Append(batch.Count).Append(' ').Append(batch.Name);
            }
            return Text.ToString();
        }

        private static string HoverLine(List<Batch> plan)
        {
            if (Total(plan) <= 0)
            {
                return "";
            }
            string alt = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive() ? "$KEY_AltKeys" : "$KEY_AltPlace";
            return Localization.instance.Localize("\n[<color=yellow><b>" + alt + " + $KEY_Use</b></color>] Add all (" + Describe(plan) + ")");
        }

        private static void Report(Humanoid user, List<Batch> plan)
        {
            user.Message(MessageHud.MessageType.Center, "$msg_added " + Describe(plan));
        }

        // ---- The plans: what one Shift + Use would put in --------------------------------------

        private static void PlanFire(Fireplace fire, Humanoid user)
        {
            Plan.Clear();
            ZNetView nview = fire.m_nview;
            if (nview == null || !nview.IsValid() || !fire.m_canRefill || fire.m_infiniteFuel || fire.m_fuelItem == null)
            {
                return;
            }
            int room = (int)fire.m_maxFuel - Mathf.CeilToInt(nview.GetZDO().GetFloat(ZDOVars.s_fuel));
            string name = fire.m_fuelItem.m_itemData.m_shared.m_name;
            int count = Mathf.Min(room, Count(user, name));
            if (count > 0)
            {
                Plan.Add(new Batch { Name = name, Count = count });
            }
        }

        private static void PlanSmelterFuel(Smelter smelter, Humanoid user)
        {
            Plan.Clear();
            if (smelter.m_fuelItem == null)
            {
                return;
            }
            int room = FuelRoom(smelter.GetFuel(), smelter.m_maxFuel);
            string name = smelter.m_fuelItem.m_itemData.m_shared.m_name;
            int count = Mathf.Min(room, Count(user, name));
            if (count > 0)
            {
                Plan.Add(new Batch { Name = name, Count = count });
            }
        }

        /// <summary>Every ore the smelter processes, in the order of its conversion list, as the game's own lookup goes.</summary>
        private static void PlanSmelterOre(Smelter smelter, Humanoid user)
        {
            Plan.Clear();
            int room = smelter.m_maxOre - smelter.GetQueueSize();
            using (new Reach(user))
            {
                Inventory inventory = user.GetInventory();
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                {
                    if (room <= 0)
                    {
                        break;
                    }
                    if (conversion.m_from == null)
                    {
                        continue;
                    }
                    string name = conversion.m_from.m_itemData.m_shared.m_name;
                    int count = Mathf.Min(room, inventory.CountItems(name));
                    if (count <= 0)
                    {
                        continue;
                    }
                    ItemDrop.ItemData item = Sample(user, name);
                    Plan.Add(new Batch { Name = name, Prefab = conversion.m_from.gameObject.name, Cheated = item != null && item.m_cheated, Count = count });
                    room -= count;
                }
            }
        }

        private static void PlanOvenFuel(CookingStation station, Humanoid user)
        {
            Plan.Clear();
            if (!station.m_useFuel || station.m_fuelItem == null)
            {
                return;
            }
            int room = FuelRoom(station.GetFuel(), station.m_maxFuel);
            string name = station.m_fuelItem.m_itemData.m_shared.m_name;
            int count = Mathf.Min(room, Count(user, name));
            if (count > 0)
            {
                Plan.Add(new Batch { Name = name, Count = count });
            }
        }

        /// <summary>
        /// The free slots, filled in conversion order. Nothing while something is done, since
        /// Use then takes that off first, and nothing while the fire below is out.
        /// </summary>
        private static void PlanFood(CookingStation station, Humanoid user)
        {
            Plan.Clear();
            ZNetView nview = station.m_nview;
            if (nview == null || !nview.IsValid() || station.HaveDoneItem() || (station.m_requireFire && !station.IsFireLit()))
            {
                return;
            }
            int room = 0;
            for (int i = 0; i < station.m_slots.Length; i++)
            {
                if (nview.GetZDO().GetString("slot" + i) == "")
                {
                    room++;
                }
            }
            using (new Reach(user))
            {
                Inventory inventory = user.GetInventory();
                foreach (CookingStation.ItemConversion conversion in station.m_conversion)
                {
                    if (room <= 0)
                    {
                        break;
                    }
                    if (conversion.m_from == null)
                    {
                        continue;
                    }
                    string name = conversion.m_from.m_itemData.m_shared.m_name;
                    int count = Mathf.Min(room, inventory.CountItems(name));
                    if (count <= 0)
                    {
                        continue;
                    }
                    ItemDrop.ItemData item = Sample(user, name);
                    Plan.Add(new Batch { Name = name, Prefab = conversion.m_from.gameObject.name, Cheated = item != null && item.m_cheated, Count = count });
                    room -= count;
                }
            }
        }

        /// <summary>Every fuel the generator burns, in the order of its list, as the game's own lookup goes.</summary>
        private static void PlanShieldFuel(ShieldGenerator generator, Humanoid user)
        {
            Plan.Clear();
            int room = FuelRoom(generator.GetFuel(), generator.m_maxFuel);
            foreach (ItemDrop fuel in generator.m_fuelItems)
            {
                if (room <= 0)
                {
                    break;
                }
                if (fuel == null)
                {
                    continue;
                }
                string name = fuel.m_itemData.m_shared.m_name;
                int count = Mathf.Min(room, Count(user, name));
                if (count <= 0)
                {
                    continue;
                }
                Plan.Add(new Batch { Name = name, Count = count });
                room -= count;
            }
        }

        /// <summary>The ammunition the ballista would load on a Use: what is already loaded, or any it takes when empty.</summary>
        private static void PlanAmmo(Turret turret, Humanoid user)
        {
            Plan.Clear();
            ZNetView nview = turret.m_nview;
            if (nview == null || !nview.IsValid() || turret.m_maxAmmo <= 0)
            {
                return;
            }
            int room = turret.m_maxAmmo - turret.GetAmmo();
            ItemDrop.ItemData item = Ammo(turret, user);
            if (room <= 0 || item == null || item.m_dropPrefab == null)
            {
                return;
            }
            string name = item.m_shared.m_name;
            int count = Mathf.Min(room, Count(user, name));
            if (count > 0)
            {
                Plan.Add(new Batch { Name = name, Prefab = item.m_dropPrefab.name, Count = count });
            }
        }

        // ---- The interactions ----------------------------------------------------------------

        /// <summary>Use on a campfire, hearth, torch or brazier. One amount RPC, clamped by the owner.</summary>
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
        private static class FireUse
        {
            private static bool Prefix(Fireplace __instance, Humanoid user, bool hold, bool alt)
            {
                if (!Wants(user, hold, alt))
                {
                    return true;
                }
                PlanFire(__instance, user);
                if (LeaveToGame(user, Plan))
                {
                    return true;
                }
                int count = Total(Plan);
                ZNetView nview = __instance.m_nview;
                if (!nview.HasOwner())
                {
                    nview.ClaimOwnership();
                }
                Take(user, Plan[0].Name, count);
                nview.InvokeRPC("RPC_AddFuelAmount", (float)count);
                Report(user, Plan);
                return false;
            }
        }

        /// <summary>The switches: a smelter's ore and fuel, an oven's fuel and food, a shield generator's fuel.</summary>
        [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
        private static class SwitchUse
        {
            private static bool Prefix(Switch __instance, Humanoid character, bool hold, bool alt)
            {
                if (!Wants(character, hold, alt))
                {
                    return true;
                }
                Smelter smelter = __instance.GetComponentInParent<Smelter>();
                if (smelter != null && smelter.m_nview != null && smelter.m_nview.IsValid())
                {
                    if (__instance == smelter.m_addWoodSwitch)
                    {
                        return !AddSmelterFuel(smelter, character);
                    }
                    if (__instance == smelter.m_addOreSwitch)
                    {
                        return !AddSmelterOre(smelter, character);
                    }
                    return true;
                }
                CookingStation station = __instance.GetComponentInParent<CookingStation>();
                if (station != null && station.m_nview != null && station.m_nview.IsValid())
                {
                    if (__instance == station.m_addFuelSwitch)
                    {
                        return !AddOvenFuel(station, character);
                    }
                    if (__instance == station.m_addFoodSwitch)
                    {
                        return !AddFood(station, character);
                    }
                    return true;
                }
                ShieldGenerator generator = __instance.GetComponentInParent<ShieldGenerator>();
                if (generator != null && generator.m_nview != null && generator.m_nview.IsValid() && __instance == generator.m_addFuelSwitch)
                {
                    return !AddShieldFuel(generator, character);
                }
                return true;
            }
        }

        /// <summary>Use on a cooking station without a food switch, i.e. the spit over a fire.</summary>
        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.Interact))]
        private static class StationUse
        {
            private static bool Prefix(CookingStation __instance, Humanoid user, bool hold, bool alt)
            {
                if (!Wants(user, hold, alt) || __instance.m_addFoodSwitch != null || __instance.m_nview == null || !__instance.m_nview.IsValid())
                {
                    return true;
                }
                return !AddFood(__instance, user);
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.Interact))]
        private static class BallistaUse
        {
            private static bool Prefix(Turret __instance, Humanoid character, bool hold, bool alt)
            {
                if (!Wants(character, hold, alt))
                {
                    return true;
                }
                PlanAmmo(__instance, character);
                if (LeaveToGame(character, Plan))
                {
                    return true;
                }
                int count = Total(Plan);
                Batch batch = Plan[0];
                Take(character, batch.Name, count);
                for (int i = 0; i < count; i++)
                {
                    Game.instance.IncrementPlayerStat(PlayerStatType.TurretAmmoAdded);
                    __instance.m_nview.InvokeRPC("RPC_AddAmmo", batch.Prefab);
                }
                Report(character, Plan);
                return false;
            }
        }

        /// <summary>True when this took over; false hands the Use to the game, which adds one or says why not.</summary>
        private static bool AddSmelterFuel(Smelter smelter, Humanoid user)
        {
            PlanSmelterFuel(smelter, user);
            if (LeaveToGame(user, Plan))
            {
                return false;
            }
            int count = Total(Plan);
            Take(user, Plan[0].Name, count);
            for (int i = 0; i < count; i++)
            {
                smelter.m_nview.InvokeRPC("RPC_AddFuel");
            }
            Report(user, Plan);
            return true;
        }

        private static bool AddSmelterOre(Smelter smelter, Humanoid user)
        {
            PlanSmelterOre(smelter, user);
            if (LeaveToGame(user, Plan))
            {
                return false;
            }
            foreach (Batch batch in Plan)
            {
                Take(user, batch.Name, batch.Count);
                for (int i = 0; i < batch.Count; i++)
                {
                    smelter.m_nview.InvokeRPC("RPC_AddOre", batch.Prefab, batch.Cheated);
                }
            }
            smelter.m_addedOreTime = Time.time;
            if (smelter.m_addOreAnimationDuration > 0f)
            {
                smelter.SetAnimation(active: true);
            }
            Report(user, Plan);
            return true;
        }

        private static bool AddOvenFuel(CookingStation station, Humanoid user)
        {
            PlanOvenFuel(station, user);
            if (LeaveToGame(user, Plan))
            {
                return false;
            }
            int count = Total(Plan);
            Take(user, Plan[0].Name, count);
            for (int i = 0; i < count; i++)
            {
                station.m_nview.InvokeRPC("RPC_AddFuel");
            }
            Report(user, Plan);
            return true;
        }

        private static bool AddFood(CookingStation station, Humanoid user)
        {
            PlanFood(station, user);
            if (LeaveToGame(user, Plan))
            {
                return false;
            }
            ZNetView nview = station.m_nview;
            if (!nview.HasOwner())
            {
                nview.ClaimOwnership();
            }
            foreach (Batch batch in Plan)
            {
                Take(user, batch.Name, batch.Count);
                for (int i = 0; i < batch.Count; i++)
                {
                    nview.InvokeRPC("RPC_AddItem", batch.Prefab, batch.Cheated);
                    if (station.m_skill != Skills.SkillType.None)
                    {
                        Player.m_localPlayer.RaiseSkill(station.m_skill, 0.4f);
                    }
                }
            }
            Report(user, Plan);
            return true;
        }

        private static bool AddShieldFuel(ShieldGenerator generator, Humanoid user)
        {
            PlanShieldFuel(generator, user);
            if (LeaveToGame(user, Plan))
            {
                return false;
            }
            foreach (Batch batch in Plan)
            {
                Take(user, batch.Name, batch.Count);
                for (int i = 0; i < batch.Count; i++)
                {
                    generator.m_nview.InvokeRPC("RPC_AddFuel");
                }
            }
            Report(user, Plan);
            return true;
        }

        // ---- The hover lines -----------------------------------------------------------------

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
        private static class FireHover
        {
            private static void Postfix(Fireplace __instance, ref string __result)
            {
                if (!Hovering || string.IsNullOrEmpty(__result))
                {
                    return;
                }
                PlanFire(__instance, Player.m_localPlayer);
                __result += HoverLine(Plan);
            }
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
        private static class SwitchHover
        {
            private static void Postfix(Switch __instance, ref string __result)
            {
                if (!Hovering || string.IsNullOrEmpty(__result))
                {
                    return;
                }
                Player player = Player.m_localPlayer;
                Smelter smelter = __instance.GetComponentInParent<Smelter>();
                if (smelter != null)
                {
                    if (smelter.m_nview == null || !smelter.m_nview.IsValid())
                    {
                        return;
                    }
                    if (__instance == smelter.m_addWoodSwitch)
                    {
                        PlanSmelterFuel(smelter, player);
                    }
                    else if (__instance == smelter.m_addOreSwitch)
                    {
                        PlanSmelterOre(smelter, player);
                    }
                    else
                    {
                        return;
                    }
                    __result += HoverLine(Plan);
                    return;
                }
                CookingStation station = __instance.GetComponentInParent<CookingStation>();
                if (station != null)
                {
                    if (station.m_nview == null || !station.m_nview.IsValid())
                    {
                        return;
                    }
                    if (__instance == station.m_addFuelSwitch)
                    {
                        PlanOvenFuel(station, player);
                    }
                    else if (__instance == station.m_addFoodSwitch)
                    {
                        PlanFood(station, player);
                    }
                    else
                    {
                        return;
                    }
                    __result += HoverLine(Plan);
                    return;
                }
                ShieldGenerator generator = __instance.GetComponentInParent<ShieldGenerator>();
                if (generator != null && generator.m_nview != null && generator.m_nview.IsValid() && __instance == generator.m_addFuelSwitch)
                {
                    PlanShieldFuel(generator, player);
                    __result += HoverLine(Plan);
                }
            }
        }

        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetHoverText))]
        private static class StationHover
        {
            private static void Postfix(CookingStation __instance, ref string __result)
            {
                if (!Hovering || string.IsNullOrEmpty(__result) || __instance.m_addFoodSwitch != null || __instance.m_nview == null || !__instance.m_nview.IsValid())
                {
                    return;
                }
                PlanFood(__instance, Player.m_localPlayer);
                __result += HoverLine(Plan);
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
        private static class BallistaHover
        {
            private static void Postfix(Turret __instance, ref string __result)
            {
                if (!Hovering || string.IsNullOrEmpty(__result) || !__instance.m_targetEnemies
                    || !PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false))
                {
                    return;
                }
                PlanAmmo(__instance, Player.m_localPlayer);
                __result += HoverLine(Plan);
            }
        }
    }
}
