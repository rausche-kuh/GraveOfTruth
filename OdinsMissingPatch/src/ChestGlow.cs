using UnityEngine;

namespace OdinsMissingPatch
{
    /// <summary>
    /// A golden pulse over a chest, three seconds long, with a floating text above it: what quick stacking
    /// shows on every chest it put something in. The pulse goes through MaterialMan, the same
    /// route the hammer's repair flash takes, so it needs no material of its own and resets to
    /// whatever the chest looked like before. One component per chest, restarted when hit again.
    /// </summary>
    internal sealed class ChestGlow : MonoBehaviour
    {
        private const float Duration = 3f;
        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f);

        private float time;

        /// <summary>Pulses the chest and floats <paramref name="text"/> above it (null for no text).</summary>
        internal static void Flash(Container chest, string text)
        {
            if (chest == null)
            {
                return;
            }
            GameObject root = chest.m_rootObjectOverride != null ? chest.m_rootObjectOverride.gameObject : chest.gameObject;
            ChestGlow glow = root.GetComponent<ChestGlow>();
            if (glow == null)
            {
                glow = root.AddComponent<ChestGlow>();
            }
            glow.time = 0f;
            if (text != null)
            {
                ShowText(chest.transform.position + Vector3.up, text);
            }
        }

        /// <summary>
        /// The game's own floating combat text, added locally rather than through its RPC so only
        /// this client sees it. The Bonus style is the large orange one that lingers for 3s.
        /// </summary>
        private static void ShowText(Vector3 position, string text)
        {
            DamageText damageText = DamageText.instance;
            if (damageText == null)
            {
                return;
            }
            Camera camera = Utils.GetMainCamera();
            float distance = camera != null ? Vector3.Distance(camera.transform.position, position) : 0f;
            damageText.AddInworldText(DamageText.TextType.Bonus, position, distance, text, mySelf: false);
        }

        private void Update()
        {
            time += Time.deltaTime;
            MaterialMan materials = MaterialMan.instance;
            if (materials == null || time >= Duration)
            {
                Destroy(this);
                return;
            }
            // Three pulses over the duration, as long as the floating text stays.
            float pulse = Mathf.Abs(Mathf.Sin(time / Duration * Mathf.PI * 3f));
            materials.SetValue(gameObject, ShaderProps._EmissionColor, Gold * (0.6f * pulse));
            materials.SetValue(gameObject, ShaderProps._Color, Color.Lerp(Color.white, Gold, 0.6f * pulse));
        }

        private void OnDestroy()
        {
            MaterialMan materials = MaterialMan.instance;
            if (materials != null)
            {
                materials.ResetValue(gameObject, ShaderProps._EmissionColor);
                materials.ResetValue(gameObject, ShaderProps._Color);
            }
        }
    }
}
