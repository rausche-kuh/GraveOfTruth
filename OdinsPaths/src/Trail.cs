using System.Collections.Generic;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// A found route turned into something to lay: the grid's corners rounded off (Chaikin, two
    /// passes), a point every 2 m, each knowing its generated ground height, whether it is under
    /// water, and the height the path should run at there - the ground smoothed over about 10 m
    /// either way, within the stretch of dry land it belongs to.
    /// </summary>
    internal sealed class Trail
    {
        public const float Spacing = 2f;
        /// <summary>Ground this close above the water level is shore, left unpainted.</summary>
        public const float ShoreMargin = 0.3f;
        private const int ProfileReach = 5;

        public readonly List<Vector2> Points = new List<Vector2>();
        public readonly List<float> Ground = new List<float>();
        public readonly List<bool> Water = new List<bool>();
        public readonly List<float> Profile = new List<float>();

        public Trail(List<Vector2> route)
        {
            List<Vector2> smooth = Chaikin(Chaikin(route));
            Resample(smooth);
            float waterLevel = ZoneSystem.instance.m_waterLevel;
            foreach (Vector2 p in Points)
            {
                float h = WorldGenerator.instance.GetHeight(p.x, p.y);
                Ground.Add(h);
                Water.Add(h < waterLevel + ShoreMargin);
            }
            for (int i = 0; i < Points.Count; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = i; k >= 0 && k >= i - ProfileReach && !Water[k]; k--)
                {
                    sum += Ground[k];
                    count++;
                }
                for (int k = i + 1; k < Points.Count && k <= i + ProfileReach && !Water[k]; k++)
                {
                    sum += Ground[k];
                    count++;
                }
                Profile.Add(count > 0 ? sum / count : Ground[i]);
            }
        }

        public float Length => Points.Count > 1 ? (Points.Count - 1) * Spacing : 0f;

        /// <summary>Metres of the trail under water, and the steepest grade on dry land.</summary>
        public void Stats(out float waterMetres, out float steepestGrade)
        {
            waterMetres = 0f;
            steepestGrade = 0f;
            for (int i = 1; i < Points.Count; i++)
            {
                if (Water[i])
                {
                    waterMetres += Spacing;
                }
                else if (!Water[i - 1])
                {
                    steepestGrade = Mathf.Max(steepestGrade, Mathf.Abs(Profile[i] - Profile[i - 1]) / Spacing);
                }
            }
        }

        /// <summary>
        /// The point of segment i..i+1 nearest to p: its distance, and the path height there (the
        /// dry end's profile if one end is under water).
        /// </summary>
        public float Nearest(Vector2 p, int segment, out float profile)
        {
            Vector2 a = Points[segment];
            Vector2 b = Points[segment + 1];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float pa = Profile[segment];
            float pb = Profile[segment + 1];
            if (Water[segment])
            {
                pa = pb;
            }
            else if (Water[segment + 1])
            {
                pb = pa;
            }
            profile = Mathf.Lerp(pa, pb, t);
            return Vector2.Distance(p, a + ab * t);
        }

        private static List<Vector2> Chaikin(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return new List<Vector2>(points);
            }
            List<Vector2> result = new List<Vector2> { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                result.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
                result.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        private void Resample(List<Vector2> line)
        {
            Points.Add(line[0]);
            float carried = 0f;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i];
                float length = Vector2.Distance(a, line[i + 1]);
                float at = Spacing - carried;
                while (at <= length)
                {
                    Points.Add(Vector2.Lerp(a, line[i + 1], at / length));
                    at += Spacing;
                }
                carried = length - (at - Spacing);
            }
            if (Points[Points.Count - 1] != line[line.Count - 1])
            {
                Points.Add(line[line.Count - 1]);
            }
        }
    }
}
