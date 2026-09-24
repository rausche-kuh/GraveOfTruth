using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// The guidance effect: a few wavy lines of bright blue light leaving the player toward the
    /// target. No environment has a wind streak system to borrow (the compass psystems dump of
    /// 2026-09-24), so it is built here: two ParticleSystems, one of long wavy lines - trails
    /// drawn behind slow particles that noise sways as they go - and one of a few small motes
    /// riding along. Both start at the player and fly toward the target in world space, so on a
    /// wide screen they sit in the middle of the view and not at its edges. They keep low over
    /// the ground, lifted and let down with the terrain under them every tick, so a hill in the
    /// way carries them up instead of swallowing them. Three or four lines
    /// in the air at a time, a couple more when close, none once arrived (SE_Compass hides it
    /// within ArrivalDistance): a hint of a wind, not a storm. The material
    /// is the wishbone ping's, so no asset ships; a plain particle shader is the fallback.
    /// </summary>
    internal sealed class Streaks
    {
        /// <summary>How a layer looks: what it emits, how big, how long, how many.</summary>
        private struct Layer
        {
            public string Name;
            public float Width;              // metres: the trail's width, or a mote's size
            public float Trail;              // the trail's length as a share of the particle's life; 0 for no trail
            public float Stretch;            // a trail-less particle's length as a multiple of its width
            public float LifeMin, LifeMax;   // seconds in the air
            public float RateFar, RateNear;  // particles per second, far from / at the target
            public float Height;             // where they start: above the player's feet ...
            public float Ahead;              // ... and at least this far toward the target ...
            public float Spread;             // ... or up to this much further, anywhere in between
            public float Radius;             // ... give or take this much to the sides
            public float Wave;               // how far the noise sways them sideways, metres per second
        }

        /// <summary>The lines: long, wavy, one every second or two - two or three in the air at once.</summary>
        private static readonly Layer Lines = new Layer
        {
            Name = "Lines", Width = 0.08f, Trail = 0.3f, Stretch = 1f,
            LifeMin = 2f, LifeMax = 2.8f, RateFar = 0.5f, RateNear = 1f,
            Height = 1.3f, Ahead = 0.5f, Spread = 5f, Radius = 0.5f, Wave = 1.2f,
        };

        /// <summary>The motes riding along: small, rare, a little wider spread.</summary>
        private static readonly Layer Motes = new Layer
        {
            Name = "Motes", Width = 0.06f, Trail = 0f, Stretch = 4f,
            LifeMin = 2f, LifeMax = 3f, RateFar = 0.3f, RateNear = 0.8f,
            Height = 1.2f, Ahead = 0f, Spread = 5f, Radius = 1.5f, Wave = 0.8f,
        };

        /// <summary>The wind's pace, metres per second, and how much of it a single particle may vary.</summary>
        private const float Speed = 8f;
        private const float SpeedMin = 0.75f;
        private const float SpeedMax = 1.3f;
        /// <summary>How long a wave is, roughly: the noise frequency is its inverse.</summary>
        private const float WaveLength = 3f;
        /// <summary>
        /// How high over the ground the lines fly. Every tick each particle is given the
        /// vertical speed that carries it toward this height above the terrain under it,
        /// Settle times the gap per second (a spring: ten per second lags less than a metre
        /// on a slope as steep as the wind is fast), and one below HoverMin climbs at the
        /// wind's full pace. Only the ground is sampled, not the rocks or trees on it, so the
        /// lines still pass through a boulder but never into a hill. Steering the speed and
        /// not the position keeps the path smooth: a position set fifty times a second, with
        /// the noise pushing back between the sets, drew the lines as a zigzag (2026-09-24).
        /// </summary>
        private const float Hover = 1.2f;
        private const float HoverMin = 0.5f;
        private const float Settle = 10f;

        /// <summary>The colour, half transparent: a wind is seen through, not looked at.</summary>
        private static readonly Color Blue = new Color(0.4f, 0.8f, 1f, 0.45f);
        /// <summary>
        /// How wide a line is along its length: a point at both ends and full width in the
        /// middle, so a line has a tip and a tail instead of two blunt ends.
        /// </summary>
        private static readonly AnimationCurve Taper = new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f), new Keyframe(0.5f, 1f, 0f, 0f), new Keyframe(1f, 0f, -3f, -3f));

        private GameObject go;
        private readonly Part[] parts = new Part[2];

        private sealed class Part
        {
            public Layer Layer;
            public GameObject Go;
            public ParticleSystem System;
            public ParticleSystem.Particle[] Buffer;
            public ParticleSystem.MainModule Main;
            public ParticleSystem.ShapeModule Shape;
            public ParticleSystem.EmissionModule Emission;
            public ParticleSystem.VelocityOverLifetimeModule Velocity;
            /// <summary>Whether the lines are cut to end at the target (see Point).</summary>
            public bool Near;
        }

        internal static Streaks Create()
        {
            if (!OdinsCompassPlugin.StreakEffect.Value)
            {
                return null;
            }
            Streaks streaks = new Streaks();
            try
            {
                streaks.Build();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OdinsCompass] could not build the streak effect: " + e.Message);
                streaks.Destroy();
                return null;
            }
            return streaks;
        }

        private void Build()
        {
            go = new GameObject("OdinsCompassStreaks");
            Material material = MakeMaterial();
            parts[0] = Make(Lines, material);
            parts[1] = Make(Motes, material);
        }

        /// <summary>
        /// One layer: a thin box from the player a few metres toward the target emits, every
        /// particle is pushed toward the target at its own pace, swayed by noise, and fades in
        /// and out. A layer with a trail
        /// draws only the trail - the wavy line the particle leaves - and hides the particle
        /// itself; one without is drawn stretched along its travel.
        /// </summary>
        private Part Make(Layer layer, Material material)
        {
            Part part = new Part { Layer = layer };
            part.Go = new GameObject(layer.Name);
            part.Go.transform.SetParent(go.transform, false);
            part.Go.transform.localPosition = new Vector3(0f, layer.Height, layer.Ahead);
            ParticleSystem system = part.Go.AddComponent<ParticleSystem>();
            part.System = system;
            system.Stop();

            ParticleSystem.MainModule main = system.main;
            part.Main = main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(layer.LifeMin, layer.LifeMax);
            main.startSpeed = 0f;
            main.startSize = layer.Width;
            main.startColor = Blue;
            main.maxParticles = Mathf.CeilToInt(layer.RateNear * layer.LifeMax * 2f) + 4;
            part.Buffer = new ParticleSystem.Particle[main.maxParticles];
            main.gravityModifier = 0f;

            // The box's near face sits Ahead of the player; Point sets its length (Spread,
            // less close to the target) and keeps its centre half that further on.
            part.Shape = system.shape;
            part.Shape.enabled = true;
            part.Shape.shapeType = ParticleSystemShapeType.Box;
            part.Shape.scale = new Vector3(layer.Radius * 2f, layer.Radius * 2f, layer.Spread);
            part.Shape.position = new Vector3(0f, 0f, layer.Spread * 0.5f);

            part.Emission = system.emission;
            part.Emission.enabled = true;
            part.Emission.rateOverTime = 0f;

            part.Velocity = system.velocityOverLifetime;
            part.Velocity.enabled = true;
            part.Velocity.space = ParticleSystemSimulationSpace.World;
            part.Velocity.speedModifier = new ParticleSystem.MinMaxCurve(SpeedMin, SpeedMax);

            // The sway: smooth noise moving each particle sideways as it flies, so the path - and
            // the trail drawn along it - waves instead of running straight. Sideways only: the
            // height is Follow's, and noise fighting it made the lines jagged.
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = layer.Wave > 0f;
            noise.separateAxes = true;
            noise.strengthX = layer.Wave;
            noise.strengthY = 0f;
            noise.strengthZ = layer.Wave;
            noise.frequency = 1f / WaveLength;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // A mote fades in and out. A line does not: it appears as its tip draws it out and
            // disappears as its tail catches up (the trail outlives the particle, below), so it
            // only fades in for the first moment and is then full until it is gone.
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                layer.Trail > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 1f) }
                    : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystemRenderer renderer = part.Go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (layer.Trail > 0f)
            {
                ParticleSystem.TrailModule trails = system.trails;
                trails.enabled = true;
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.ratio = 1f;
                trails.lifetime = layer.Trail;
                trails.minVertexDistance = 0.15f;
                trails.worldSpace = true;
                // The trail outlives the particle: when the particle dies the head stops and the
                // tail runs up to it over the trail's lifetime - the line retracts into nothing
                // instead of vanishing in one frame.
                trails.dieWithParticles = false;
                trails.sizeAffectsWidth = false;
                trails.inheritParticleColor = true;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(layer.Width, Taper);
                trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
                Gradient tail = new Gradient();
                tail.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                trails.colorOverTrail = tail;
                renderer.renderMode = ParticleSystemRenderMode.None;
                renderer.trailMaterial = material;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0f;
                renderer.lengthScale = layer.Stretch;
                renderer.material = material;
            }

            system.Play();
            return part;
        }

        /// <summary>The wishbone ping's particle material, copied; else the first particle shader found.</summary>
        private static Material MakeMaterial()
        {
            GameObject ping = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("vfx_WishbonePing") : null;
            ParticleSystemRenderer source = ping != null ? ping.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            if (source != null && source.sharedMaterial != null)
            {
                return new Material(source.sharedMaterial);
            }
            foreach (string name in new[] { "Particles/Standard Unlit", "Legacy Shaders/Particles/Additive", "Sprites/Default" })
            {
                Shader shader = Shader.Find(name);
                if (shader != null)
                {
                    Debug.Log("[OdinsCompass] no vfx_WishbonePing material; streaks use " + name);
                    return new Material(shader);
                }
            }
            Debug.LogWarning("[OdinsCompass] no particle shader found; the streaks may draw pink");
            return null;
        }

        /// <summary>
        /// Leaves the player toward the target. Closeness 0 is "far" (a few lines), 1 is at the
        /// target (a few more). A line starts anywhere in the first few metres of the way; it
        /// flies on the flat toward the target, and the ground under it sets its height (see
        /// Follow), so it climbs and drops with the land and ends just above the target's
        /// ground. Once the target is nearer than a line would fly in its life, the start band
        /// shrinks and every line is given exactly the life the way takes from the band's
        /// middle at the wind's exact pace, so it ends in the target instead of sailing over it.
        /// </summary>
        internal void Point(Vector3 player, Vector3 target, float closeness)
        {
            if (go == null)
            {
                return;
            }
            Vector3 flat = target - player;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1f)
            {
                Hide();
                return;
            }
            go.transform.position = player;
            go.transform.rotation = Quaternion.LookRotation(flat.normalized);
            foreach (Part part in parts)
            {
                if (part == null)
                {
                    continue;
                }
                Vector3 aim = target - part.Go.transform.position;
                aim.y = 0f;
                float distance = aim.magnitude;
                if (distance < 0.5f)
                {
                    part.Emission.rateOverTime = 0f;
                    continue;
                }
                Vector3 v = aim * (Speed / distance);
                part.Velocity.x = new ParticleSystem.MinMaxCurve(v.x);
                part.Velocity.y = new ParticleSystem.MinMaxCurve(0f);
                part.Velocity.z = new ParticleSystem.MinMaxCurve(v.z);
                float spread = Mathf.Min(part.Layer.Spread, distance * 0.3f);
                part.Shape.scale = new Vector3(part.Layer.Radius * 2f, part.Layer.Radius * 2f, spread);
                part.Shape.position = new Vector3(0f, 0f, spread * 0.5f);
                float life = (distance - spread * 0.5f) / Speed;
                if (life < part.Layer.LifeMax)
                {
                    part.Main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(life, 0.3f));
                    part.Velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f);
                    part.Near = true;
                }
                else if (part.Near)
                {
                    part.Main.startLifetime = new ParticleSystem.MinMaxCurve(part.Layer.LifeMin, part.Layer.LifeMax);
                    part.Velocity.speedModifier = new ParticleSystem.MinMaxCurve(SpeedMin, SpeedMax);
                    part.Near = false;
                }
                part.Emission.rateOverTime = Mathf.Lerp(part.Layer.RateFar, part.Layer.RateNear, Mathf.Clamp01(closeness));
            }
        }

        /// <summary>
        /// Keeps every particle in the air low over the ground: each gets the vertical speed
        /// that carries it toward Hover above the terrain under it, and one below HoverMin
        /// climbs at the wind's full pace. Runs every tick, pointed or hidden, so lines still
        /// flying after the wind stopped follow the land too. A dozen particles at most, so a
        /// terrain raycast each is nothing.
        /// </summary>
        internal void Follow(float dt)
        {
            if (go == null || ZoneSystem.instance == null)
            {
                return;
            }
            foreach (Part part in parts)
            {
                if (part == null || part.System == null)
                {
                    continue;
                }
                int count = part.System.GetParticles(part.Buffer);
                if (count == 0)
                {
                    continue;
                }
                for (int i = 0; i < count; i++)
                {
                    Vector3 position = part.Buffer[i].position;
                    Vector3 velocity = part.Buffer[i].velocity;
                    if (!ZoneSystem.instance.GetGroundHeight(position, out float ground))
                    {
                        velocity.y = 0f;
                    }
                    else
                    {
                        velocity.y = Mathf.Clamp((ground + Hover - position.y) * Settle, -Speed, Speed);
                        if (position.y < ground + HoverMin)
                        {
                            velocity.y = Speed;
                        }
                    }
                    part.Buffer[i].velocity = velocity;
                }
                part.System.SetParticles(part.Buffer, count);
            }
        }

        internal void Hide()
        {
            if (go == null)
            {
                return;
            }
            foreach (Part part in parts)
            {
                if (part != null)
                {
                    part.Emission.rateOverTime = 0f;
                }
            }
        }

        internal void Destroy()
        {
            if (go != null)
            {
                Object.Destroy(go);
                go = null;
            }
        }
    }
}
