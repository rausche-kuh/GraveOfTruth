using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveEntrance
{
    /// <summary>
    /// A door's frame: the teleport trigger's position and rotation, raised by the manual offset.
    /// +z is the trigger's forward, which points out of the dungeon at the entrance and into it at
    /// the exit. Entrance and exit frames correspond, so the view is mapped between them.
    /// </summary>
    internal struct Surface
    {
        public Vector3 Center;
        public Quaternion Rotation;
        /// <summary>The doorway's size when there is no black box to lay the view over.</summary>
        public Vector2 Size;

        public Vector3 Forward => Rotation * Vector3.forward;

        /// <summary>A world point in this frame.</summary>
        public Vector3 ToLocal(Vector3 world)
        {
            return Quaternion.Inverse(Rotation) * (world - Center);
        }

        public Vector3 ToWorld(Vector3 local)
        {
            return Center + Rotation * local;
        }

        public override string ToString()
        {
            return $"{Size.x:0.00}x{Size.y:0.00} at {Center}";
        }
    }

    /// <summary>
    /// Finds the black surface at an entrance and the white one at an exit. The prefabs cannot be
    /// read offline, so this is a heuristic: a mesh near the trigger whose material is dark
    /// (entrance) or bright (exit), or whose name says so. The dev command <c>ientrance dump</c>
    /// prints every candidate it looked at.
    /// </summary>
    internal static class DoorSurfaces
    {
        private const float SearchRadius = 5f;
        private const float MaxThickness = 0.3f;
        private const float MinSide = 0.5f;
        private const float MinArea = 1f;
        private const float MaxArea = 40f;
        private const float DarkBelow = 0.15f;
        private const float BrightAbove = 0.6f;

        private static readonly string[] DarkNames = { "black", "dark", "void", "portal", "fade" };
        private static readonly string[] BrightNames = { "white", "light", "bright", "portal", "fade" };
        private static readonly string[] PortalNames = { "portal" };

        internal struct Candidate
        {
            public MeshRenderer Renderer;
            /// <summary>Centre and extent of the mesh's box in the trigger's frame; Center.z is the front face.</summary>
            public Vector3 Center;
            public Vector3 Size;
            /// <summary>Luminance of the material's colour, or -1 when it has none.</summary>
            public float Luminance;
            public bool Flat;
            public bool Dark;
            public bool Bright;
        }

        /// <summary>
        /// The door's frame. Measured surfaces made poor frames: at a burial chamber the black and
        /// the white "surfaces" are 1 m deep fade boxes of different sizes, so the frame is the
        /// trigger's, which lines up.
        /// </summary>
        internal static Surface Find(Teleport door)
        {
            return new Surface
            {
                Center = door.transform.position + door.transform.rotation * Portal.ManualOffset,
                Rotation = door.transform.rotation,
                Size = Portal.ManualSize,
            };
        }

        /// <summary>
        /// The black box the game draws in an entrance (<c>Gateway/Cube</c>, material
        /// <c>tointerior_portal</c> at a burial chamber): the nearest dark mesh near the trigger,
        /// preferring one whose name says portal. The doorway view is laid over its front face, so
        /// it fills whatever shape and size of opening the location has. Null when there is none.
        /// </summary>
        internal static MeshRenderer FindBlack(Teleport door)
        {
            Candidate? best = null;
            foreach (Candidate candidate in Candidates(door))
            {
                if (!candidate.Dark)
                {
                    continue;
                }
                if (best == null || Better(candidate, best.Value))
                {
                    best = candidate;
                }
            }
            return best?.Renderer;
        }

        /// <summary>
        /// The exit's white box: the nearest mesh near the trigger whose name or material says
        /// portal, or failing that says white, light, bright or fade (a bright material alone is
        /// not enough - an ice wall is bright).
        /// </summary>
        internal static MeshRenderer FindWhite(Teleport exit)
        {
            List<Candidate> candidates = Candidates(exit);
            return Nearest(candidates, PortalNames) ?? Nearest(candidates, BrightNames);
        }

        private static MeshRenderer Nearest(List<Candidate> candidates, string[] words)
        {
            MeshRenderer best = null;
            float bestDistance = float.MaxValue;
            foreach (Candidate candidate in candidates)
            {
                if (!Mentions(NameOf(candidate.Renderer), words))
                {
                    continue;
                }
                float distance = candidate.Center.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate.Renderer;
                }
            }
            return best;
        }

        private static bool Better(Candidate a, Candidate b)
        {
            bool aPortal = Mentions(NameOf(a.Renderer), PortalNames);
            bool bPortal = Mentions(NameOf(b.Renderer), PortalNames);
            if (aPortal != bPortal)
            {
                return aPortal;
            }
            return a.Center.sqrMagnitude < b.Center.sqrMagnitude;
        }

        private static string NameOf(Renderer renderer)
        {
            return (renderer.name + " " + (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "")).ToLowerInvariant();
        }

        /// <summary>
        /// The box of a mesh in a frame: the mesh's own bounds, every corner taken to the frame,
        /// and the box around those. A disabled renderer's bounds are not reliable, which is why
        /// the mesh's are used.
        /// </summary>
        internal static bool Box(MeshRenderer renderer, Vector3 origin, Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            min = Vector3.one * float.MaxValue;
            max = Vector3.one * float.MinValue;
            MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                return false;
            }
            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
            Quaternion inverse = Quaternion.Inverse(rotation);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = inverse * (toWorld.MultiplyPoint3x4(corner) - origin);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
            return true;
        }

        /// <summary>The world height of a mesh's lowest corner, or null without a mesh.</summary>
        internal static float? Bottom(MeshRenderer renderer)
        {
            return Box(renderer, Vector3.zero, Quaternion.identity, out Vector3 min, out _) ? min.y : (float?)null;
        }

        /// <summary>Every mesh near the trigger, measured in the trigger's frame.</summary>
        internal static List<Candidate> Candidates(Teleport door)
        {
            var result = new List<Candidate>();
            Transform root = SearchRoot(door);
            Vector3 origin = door.transform.position;

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (renderer.name.StartsWith("ImmersiveEntrance") || (renderer.bounds.center - origin).sqrMagnitude > SearchRadius * SearchRadius * 4f)
                {
                    continue;
                }
                if (!Box(renderer, origin, door.transform.rotation, out Vector3 min, out Vector3 max))
                {
                    continue;
                }
                Vector3 size = max - min;
                Vector3 center = (min + max) * 0.5f;
                center.z = max.z;
                if (center.sqrMagnitude > SearchRadius * SearchRadius)
                {
                    continue;
                }

                float luminance = Luminance(renderer.sharedMaterial);
                string name = NameOf(renderer);
                result.Add(new Candidate
                {
                    Renderer = renderer,
                    Center = center,
                    Size = size,
                    Luminance = luminance,
                    Flat = size.z <= MaxThickness && size.x >= MinSide && size.y >= MinSide &&
                           size.x * size.y >= MinArea && size.x * size.y <= MaxArea,
                    Dark = (luminance >= 0f && luminance <= DarkBelow) || Mentions(name, DarkNames),
                    Bright = luminance >= BrightAbove || Mentions(name, BrightNames),
                });
            }
            return result;
        }

        /// <summary>
        /// The location the door belongs to - the dungeon's rooms are children of its generator,
        /// which is part of the location - or the door's own root when it has none.
        /// </summary>
        private static Transform SearchRoot(Teleport door)
        {
            Location location = door.GetComponentInParent<Location>();
            return location != null ? location.transform : door.transform.root;
        }

        private static float Luminance(Material material)
        {
            if (material == null)
            {
                return -1f;
            }
            foreach (string property in new[] { "_Color", "_TintColor", "_BaseColor" })
            {
                if (material.HasProperty(property))
                {
                    Color c = material.GetColor(property);
                    return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                }
            }
            return -1f;
        }

        private static bool Mentions(string name, string[] words)
        {
            foreach (string word in words)
            {
                if (name.Contains(word))
                {
                    return true;
                }
            }
            return false;
        }

        internal static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (Transform t = transform.parent; t != null; t = t.parent)
            {
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
