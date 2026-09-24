using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// Where a trail leaves the land for the open sea, and where it comes ashore again, a wooden
    /// barrel stands on its last dry point - a stand-in for the dock to come. Fords get none: only
    /// a stretch of water with a point deeper than a ford is a crossing. A landing that already
    /// has something built on it (an earlier path's barrel, a player's dock) gets no second one.
    /// The barrels are structures like any other, so later paths walk around them. Server only.
    /// </summary>
    internal static class Landings
    {
        private const string BarrelPrefab = "piece_chest_barrel";
        /// <summary>A structure this close to a landing already marks it.</summary>
        private const float Taken = 4f;

        /// <summary>The barrels placed, as their ZDOs.</summary>
        public static List<ZDOID> Place(Trail trail, Structures structures)
        {
            List<ZDOID> placed = new List<ZDOID>();
            GameObject prefab = ZNetScene.instance.GetPrefab(BarrelPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[OdinsPaths] No " + BarrelPrefab + " prefab - landings left unmarked.");
                return placed;
            }
            float waterLevel = ZoneSystem.instance.m_waterLevel;
            int i = 0;
            while (i < trail.Points.Count)
            {
                if (!trail.Water[i])
                {
                    i++;
                    continue;
                }
                int first = i;
                bool deep = false;
                while (i < trail.Points.Count && trail.Water[i])
                {
                    deep |= trail.Ground[i] < waterLevel - PathSearch.FordDepth;
                    i++;
                }
                if (!deep)
                {
                    continue;
                }
                // The shore before the crossing looks out over it, the one after looks back.
                if (first > 0)
                {
                    Mark(trail, first - 1, first, prefab, structures, placed);
                }
                if (i < trail.Points.Count)
                {
                    Mark(trail, i, i - 1, prefab, structures, placed);
                }
            }
            return placed;
        }

        private static void Mark(Trail trail, int shore, int toward, GameObject prefab, Structures structures, List<ZDOID> placed)
        {
            Vector2 at = trail.Points[shore];
            if (structures.Distance(at, Taken) < Taken)
            {
                return;
            }
            Vector2 look = trail.Points[toward] - at;
            Quaternion rotation = look.sqrMagnitude > 0f
                ? Quaternion.LookRotation(new Vector3(look.x, 0f, look.y)) : Quaternion.identity;
            Vector3 position = new Vector3(at.x, Height(trail, shore), at.y);

            // As ZNetView.Awake would make it for the prefab; no creator, so nobody's work.
            int hash = prefab.name.GetStableHashCode();
            ZNetView view = prefab.GetComponent<ZNetView>();
            ZDO zdo = ZDOMan.instance.CreateNewZDO(position, hash);
            zdo.Persistent = view.m_persistent;
            zdo.Type = view.m_type;
            zdo.Distant = view.m_distant;
            zdo.SetPrefab(hash);
            zdo.SetRotation(rotation);
            structures.Add(at);
            placed.Add(zdo.m_uid);
        }

        /// <summary>The generated ground, moved as the terrain writer's levelling moves it.</summary>
        private static float Height(Trail trail, int index)
        {
            float ground = trail.Ground[index];
            if (OdinsPathsPlugin.Levelling.Value && ground >= ZoneSystem.instance.m_waterLevel + Trail.ShoreMargin + 0.2f)
            {
                float maxCut = OdinsPathsPlugin.MaxCut.Value;
                ground += Mathf.Clamp(trail.Profile[index] - ground, -maxCut, maxCut);
            }
            return ground;
        }
    }
}
