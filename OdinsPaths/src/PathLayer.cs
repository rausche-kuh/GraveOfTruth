using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// One path, start to finish: search from the nearest of the starts to the goal around the
    /// locations and buildings in the way, shape the route into a trail, write it into the
    /// terrain, clear the trees and rocks off it, and set a barrel at each sea landing. A
    /// coroutine, run on the plugin. Server only.
    /// Today the dev command "paths lay" calls it; the sleep trigger will (ROADMAP.md section 3).
    /// </summary>
    internal static class PathLayer
    {
        /// <summary>Locations smaller than this still keep the path at this distance.</summary>
        private const float MinLocationRadius = 8f;

        internal sealed class Outcome
        {
            public string Failure;
            public PathSearch Search;
            public Trail Trail;
            public TerrainWriter.Result Written = new TerrainWriter.Result();
            public Clearing.Result Cleared = new Clearing.Result();
            public List<ZDOID> Landings = new List<ZDOID>();
            public int Structures;
            public double WriteMilliseconds;
        }

        public static IEnumerator Lay(List<Vector2> starts, Vector2 goal, Action<string> report, Action<Outcome> done)
        {
            Outcome outcome = new Outcome();
            List<Circle> locations = LocationsAround(starts, goal);
            Structures structures = Structures.Around(starts, goal);
            outcome.Structures = structures.Count;
            outcome.Search = new PathSearch(starts, goal, locations, structures);
            report("Searching (" + locations.Count + " locations and " + structures.Count + " built pieces to avoid)...");
            yield return outcome.Search.Run(OdinsPathsPlugin.SearchBudgetMs.Value);
            if (outcome.Search.Result == null)
            {
                outcome.Failure = outcome.Search.Failure;
                done(outcome);
                yield break;
            }
            outcome.Trail = new Trail(outcome.Search.Result);
            report("Found " + outcome.Trail.Length.ToString("F0") + " m in " + outcome.Search.Milliseconds.ToString("F0")
                + " ms; writing the terrain...");
            float started = Time.realtimeSinceStartup;
            yield return TerrainWriter.Write(outcome.Trail, locations, structures, outcome.Written);
            yield return Clearing.Clear(outcome.Trail, outcome.Cleared);
            outcome.Landings = OdinsPaths.Landings.Place(outcome.Trail, structures);
            outcome.WriteMilliseconds = (Time.realtimeSinceStartup - started) * 1000.0;
            done(outcome);
        }

        /// <summary>
        /// Every location near the search area, as a circle to keep out of - except the ones a
        /// start or the goal lies in, which the path has to leave or reach. Server only: clients
        /// hold no location instances.
        /// </summary>
        private static List<Circle> LocationsAround(List<Vector2> starts, Vector2 goal)
        {
            List<Circle> result = new List<Circle>();
            float reach = 0f;
            foreach (Vector2 start in starts)
            {
                reach = Mathf.Max(reach, PathSearch.EllipseLimit(start, goal));
            }
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
            {
                Vector2 center = new Vector2(instance.m_position.x, instance.m_position.z);
                Circle circle = new Circle
                {
                    Center = center,
                    Radius = Mathf.Max(instance.m_location.m_exteriorRadius, MinLocationRadius),
                };
                if (circle.Contains(goal) || Vector2.Distance(center, goal) > reach)
                {
                    continue;
                }
                bool holdsStart = false;
                foreach (Vector2 start in starts)
                {
                    holdsStart |= circle.Contains(start);
                }
                if (!holdsStart)
                {
                    result.Add(circle);
                }
            }
            return result;
        }
    }
}
