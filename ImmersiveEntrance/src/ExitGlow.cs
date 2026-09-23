using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveEntrance
{
    /// <summary>
    /// The white glow of an exit door - the box the game draws in it (<c>ExteriorGateway/Cube</c>,
    /// material <c>exirt_portal</c> at a burial chamber) and any light or particles inside that box -
    /// switched off for the portal render only. The near plane cuts the box where it crosses the
    /// door, but the rest of it, and whatever light it holds, washed the view looking in with a
    /// glow that belongs to looking out.
    /// </summary>
    internal sealed class ExitGlow
    {
        /// <summary>How far past the white box a light or particle system still counts as its glow.</summary>
        private const float Margin = 1f;
        /// <summary>
        /// How far from the exit trigger a light still counts as the glow, box or no box. A
        /// cave's exit light need not hang in its box; an ice cave's looked lit from the door.
        /// </summary>
        private const float LightRadius = 2.5f;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Light> lights = new List<Light>();
        private readonly List<bool> savedRenderers = new List<bool>();
        private readonly List<bool> savedLights = new List<bool>();

        /// <summary>Switched with <c>ientrance part exit</c>.</summary>
        internal static bool Hide = true;

        /// <summary>The white box itself; null when none was found.</summary>
        internal MeshRenderer White { get; }

        internal string Found { get; private set; } = "nothing";

        internal ExitGlow(Teleport exit)
        {
            White = DoorSurfaces.FindWhite(exit);
            Bounds box = new Bounds(exit.transform.position, Vector3.zero);
            if (White != null)
            {
                renderers.Add(White);
                box = White.bounds;
                box.Expand(Margin * 2f);
            }
            // The glow's light need not hang under the box, so the whole location is looked
            // through; torches are left alone - fire is not what spills out of the door.
            Location location = exit.GetComponentInParent<Location>();
            Transform root = location != null ? location.transform : exit.transform.root;
            Vector3 door = exit.transform.position;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer && (box.Contains(renderer.transform.position) || Vector3.Distance(renderer.transform.position, door) <= LightRadius))
                {
                    renderers.Add(renderer);
                }
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if ((box.Contains(light.transform.position) || Vector3.Distance(light.transform.position, door) <= LightRadius) &&
                    light.GetComponentInParent<Fireplace>() == null)
                {
                    lights.Add(light);
                }
            }
            if (renderers.Count == 0 && lights.Count == 0)
            {
                return;
            }
            var names = new List<string>();
            foreach (Renderer renderer in renderers)
            {
                names.Add(DoorSurfaces.PathOf(renderer.transform));
            }
            foreach (Light light in lights)
            {
                names.Add($"light {DoorSurfaces.PathOf(light.transform)} ({light.type}, {light.intensity:0.00})");
            }
            Found = string.Join(", ", names);
        }

        internal void Off()
        {
            savedRenderers.Clear();
            savedLights.Clear();
            if (!Hide)
            {
                return;
            }
            foreach (Renderer renderer in renderers)
            {
                savedRenderers.Add(renderer != null && renderer.forceRenderingOff);
                if (renderer != null)
                {
                    renderer.forceRenderingOff = true;
                }
            }
            foreach (Light light in lights)
            {
                savedLights.Add(light != null && light.enabled);
                if (light != null)
                {
                    light.enabled = false;
                }
            }
        }

        internal void Restore()
        {
            for (int i = 0; i < renderers.Count && i < savedRenderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].forceRenderingOff = savedRenderers[i];
                }
            }
            for (int i = 0; i < lights.Count && i < savedLights.Count; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].enabled = savedLights[i];
                }
            }
        }
    }
}
