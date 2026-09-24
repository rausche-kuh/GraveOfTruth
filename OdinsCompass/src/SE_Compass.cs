using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// The status effect every compass tier applies while it is worn - the game adds it from the
    /// utility item's m_equipStatusEffect and removes it when the compass comes off, nothing to
    /// patch. One effect for all eight tiers: which tier is worn is read off the worn item. On the
    /// local player it runs the whole compass: the Seeker finds the target, the Streaks show the
    /// way until the player is within ArrivalDistance of it, the cycle key moves to the next
    /// target and the choice is saved with the character.
    /// It makes no sound - the wishbone's ping was tried and was too much. On anyone else it does
    /// nothing.
    /// </summary>
    public class SE_Compass : StatusEffect
    {
        /// <summary>The identity: ObjectDB finds a status effect by the stable hash of its name.</summary>
        internal const string EffectName = "OdinsCompass";
        /// <summary>Where the chosen target lives in Player.m_customData, saved with the character.</summary>
        internal const string CustomDataKey = "OdinsCompass.Target";

        /// <summary>The local player's running instance, for the cycle key.</summary>
        private static SE_Compass local;

        private Seeker seeker;
        private Streaks streaks;

        /// <summary>The worn compass's tier, 1..8, or 0 when the wearer holds none.</summary>
        internal int Tier
        {
            get
            {
                Humanoid wearer = m_character as Humanoid;
                ItemDrop.ItemData item = wearer != null ? wearer.m_utilityItem : null;
                return item != null ? Items.TierOf(item) : 0;
            }
        }

        private bool IsLocal => m_character != null && m_character == Player.m_localPlayer;

        public override void Setup(Character character)
        {
            base.Setup(character);
            if (!IsLocal)
            {
                return;
            }
            local = this;
            seeker = new Seeker();
            seeker.Choose(Saved(TargetGroup.ForTier(Tier)));
            streaks = Streaks.Create();
        }

        public override void Stop()
        {
            base.Stop();
            Teardown();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Teardown();
        }

        private void Teardown()
        {
            if (local == this)
            {
                local = null;
            }
            if (seeker != null)
            {
                seeker.Release();
                seeker = null;
            }
            if (streaks != null)
            {
                streaks.Destroy();
                streaks = null;
            }
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (seeker == null || m_character == null)
            {
                return;
            }
            // Swapping one compass for another keeps the effect (same effect, no re-add), so the
            // choice is checked against the worn tier every tick.
            List<TargetGroup> offered = TargetGroup.ForTier(Tier);
            if (seeker.Group == null || !offered.Contains(seeker.Group))
            {
                seeker.Choose(offered.Count > 0 ? offered[0] : null);
            }
            Vector3 from = m_character.transform.position;
            seeker.Update(dt, from);
            streaks?.Follow(dt);
            Vector3? target = seeker.Position(from);
            if (!target.HasValue)
            {
                streaks?.Hide();
                return;
            }
            float distance = Utils.DistanceXZ(from, target.Value);
            if (distance <= OdinsCompassPlugin.ArrivalDistance.Value)
            {
                // Arrived: the wind stops and the lines already in the air fly out and fade.
                streaks?.Hide();
                return;
            }
            float far = Mathf.Clamp01(distance / seeker.Range);
            streaks?.Point(from, target.Value, 1f - far);
        }

        // ---- The choice --------------------------------------------------------------------

        /// <summary>The saved choice if the tier still offers it, else the tier's first group.</summary>
        private static TargetGroup Saved(List<TargetGroup> offered)
        {
            string name;
            Player player = Player.m_localPlayer;
            if (player != null && player.m_customData.TryGetValue(CustomDataKey, out name))
            {
                foreach (TargetGroup group in offered)
                {
                    if (group.Name == name)
                    {
                        return group;
                    }
                }
            }
            return offered.Count > 0 ? offered[0] : null;
        }

        /// <summary>The next group the worn tier offers, announced and saved.</summary>
        private void Cycle()
        {
            List<TargetGroup> offered = TargetGroup.ForTier(Tier);
            if (seeker == null || offered.Count == 0)
            {
                return;
            }
            int index = seeker.Group != null ? offered.IndexOf(seeker.Group) : -1;
            TargetGroup next = offered[(index + 1) % offered.Count];
            seeker.Choose(next);
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.m_customData[CustomDataKey] = next.Name;
                player.Message(MessageHud.MessageType.TopLeft, "$oc_compass_effect: " + next.Name);
            }
        }

        /// <summary>
        /// The cycle key, read where the game reads its own keys and only when it would: not in
        /// menus, chat, the console or the map.
        /// </summary>
        [HarmonyPatch(typeof(Player), "Update")]
        private static class CycleKey
        {
            private static void Postfix(Player __instance)
            {
                if (local == null || __instance != Player.m_localPlayer || !__instance.TakeInput())
                {
                    return;
                }
                KeyCode key = OdinsCompassPlugin.CycleTargetKey.Value;
                if (key != KeyCode.None && ZInput.GetKeyDown(key, logWarning: false))
                {
                    local.Cycle();
                }
            }
        }

        // ---- The words on the HUD ----------------------------------------------------------

        public override string GetIconText()
        {
            if (seeker == null || seeker.Group == null)
            {
                return "";
            }
            Vector3 from = m_character.transform.position;
            switch (seeker.Current(from))
            {
                case Seeker.State.Lost: return Localization.instance.Localize("$oc_compass_lost");
                case Seeker.State.NoneNear: return Localization.instance.Localize("$oc_compass_none_near");
                case Seeker.State.Found:
                    string name = Localization.instance.Localize(seeker.Group.Name);
                    if (!OdinsCompassPlugin.ShowDistance.Value)
                    {
                        return name;
                    }
                    return name + " " + DistanceText(Utils.DistanceXZ(from, seeker.Position(from).Value));
                default: return Localization.instance.Localize(seeker.Group.Name);
            }
        }

        public override string GetTooltipString()
        {
            string text = Localization.instance.Localize(m_tooltip);
            if (seeker != null && seeker.Group != null)
            {
                text += "\n" + Localization.instance.Localize("$oc_compass_effect: " + seeker.Group.Name + " (" + GetIconText() + ")");
            }
            return text;
        }

        private static string DistanceText(float metres)
        {
            return metres < 1000f ? Mathf.RoundToInt(metres) + "m" : (metres / 1000f).ToString("0.0") + "km";
        }
    }
}
