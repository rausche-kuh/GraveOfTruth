using System.Collections.Generic;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// One thing a compass can be set to seek: a name and the prefabs that count as it, from the
    /// tier's Targets config. Whether a prefab is a location (asked of the server) or a world
    /// object (searched among what the client has loaded) is decided when it is sought, not here.
    /// </summary>
    internal sealed class TargetGroup
    {
        /// <summary>A $oc_ token or plain text, shown through Localize either way.</summary>
        internal readonly string Name;
        internal readonly string[] Prefabs;
        /// <summary>The tier that unlocks it, 1..8.</summary>
        internal readonly int Tier;

        private TargetGroup(string name, string[] prefabs, int tier)
        {
            Name = name;
            Prefabs = prefabs;
            Tier = tier;
        }

        private static List<TargetGroup> all;

        /// <summary>Every group of every tier, in tier order then config order. Parsed once.</summary>
        internal static List<TargetGroup> All()
        {
            if (all != null)
            {
                return all;
            }
            all = new List<TargetGroup>();
            for (int i = 0; i < OdinsCompassPlugin.TierTargets.Length; i++)
            {
                string text = OdinsCompassPlugin.TierTargets[i].Value;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }
                foreach (string entry in text.Split('|'))
                {
                    int eq = entry.IndexOf('=');
                    if (eq <= 0)
                    {
                        Debug.LogWarning("[OdinsCompass] tier " + (i + 1) + " target without a name=prefab shape, skipped: '" + entry + "'");
                        continue;
                    }
                    string name = entry.Substring(0, eq).Trim();
                    List<string> prefabs = new List<string>();
                    foreach (string prefab in entry.Substring(eq + 1).Split(','))
                    {
                        string trimmed = prefab.Trim();
                        if (trimmed.Length > 0)
                        {
                            prefabs.Add(trimmed);
                        }
                    }
                    if (name.Length == 0 || prefabs.Count == 0)
                    {
                        Debug.LogWarning("[OdinsCompass] tier " + (i + 1) + " target without a name or a prefab, skipped: '" + entry + "'");
                        continue;
                    }
                    all.Add(new TargetGroup(name, prefabs.ToArray(), i + 1));
                }
            }
            return all;
        }

        /// <summary>The groups a compass of this tier offers: its own and every lower tier's.</summary>
        internal static List<TargetGroup> ForTier(int tier)
        {
            List<TargetGroup> result = new List<TargetGroup>();
            foreach (TargetGroup group in All())
            {
                if (group.Tier <= tier)
                {
                    result.Add(group);
                }
            }
            return result;
        }
    }
}
