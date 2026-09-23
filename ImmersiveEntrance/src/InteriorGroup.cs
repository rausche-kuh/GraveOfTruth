using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveEntrance
{
    /// <summary>
    /// Renderers a location keeps for the inside only. <c>RenderGroupSystem</c> switches every
    /// <c>RenderGroupSubscriber</c> in the interior group off while the player is not in an
    /// interior, and the player at the door is not - so whatever a dungeon marks that way is
    /// switched on for the portal render alone.
    /// </summary>
    internal sealed class InteriorGroup
    {
        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        private readonly List<bool> saved = new List<bool>();

        internal int Count => renderers.Count;

        /// <summary>Every interior-group renderer under the location, by path, for the dump.</summary>
        internal readonly List<string> Names = new List<string>();

        internal void Gather(Location location)
        {
            renderers.Clear();
            Names.Clear();
            if (location == null)
            {
                return;
            }
            foreach (RenderGroupSubscriber subscriber in location.GetComponentsInChildren<RenderGroupSubscriber>(true))
            {
                if (subscriber.Group != RenderGroup.Interior)
                {
                    continue;
                }
                MeshRenderer renderer = subscriber.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderers.Add(renderer);
                    Names.Add(DoorSurfaces.PathOf(renderer.transform));
                }
            }
        }

        internal void On()
        {
            saved.Clear();
            foreach (MeshRenderer renderer in renderers)
            {
                saved.Add(renderer != null && renderer.enabled);
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        internal void Restore()
        {
            for (int i = 0; i < renderers.Count && i < saved.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = saved[i];
                }
            }
        }
    }
}
