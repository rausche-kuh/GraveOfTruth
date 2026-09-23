using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveEntrance
{
    /// <summary>
    /// Makes the view through the door look like the dungeon rather than the weather outside. The
    /// game's <c>EnvMan.SetEnv</c> writes the environment into global state - <c>RenderSettings</c>,
    /// the sun, shader globals - for where the player is, and only there. This computes what it
    /// would write for the interior environment at this time of day, the same blend of night, day,
    /// morning and evening values, puts it in place just for the portal camera's render and
    /// restores everything straight after.
    /// </summary>
    internal static class InteriorLook
    {
        private static readonly int AmbientColor = Shader.PropertyToID("_AmbientColor");
        private static readonly int SunColor = Shader.PropertyToID("_SunColor");
        private static readonly int SunFogColor = Shader.PropertyToID("_SunFogColor");
        private static readonly int SunDir = Shader.PropertyToID("_SunDir");
        private static readonly int SkyboxSunDir = Shader.PropertyToID("_SkyboxSunDir");
        private static readonly int Wet = Shader.PropertyToID("_Wet");

        /// <summary>What <c>SetEnv</c> would put in place for an environment right now.</summary>
        internal struct Blend
        {
            /// <summary>False when the environment was not found and the outdoor values are used as they are.</summary>
            public bool Found;
            public float Night, Day, Morning, Evening;
            public Color FogColor;
            public float FogDensity;
            public Color SunFogColor;
            public Color Ambient;
            public Color SunColor;
            public float SunIntensity;
            public Quaternion SunRotation;
            public Color AoColor;
            public float AoIntensity;

            public override string ToString()
            {
                return $"weights night {Night:0.00} day {Day:0.00} morning {Morning:0.00} evening {Evening:0.00}; " +
                       $"ambient {Ambient}, fog {FogColor} density {FogDensity:0.000}, sun fog {SunFogColor}, " +
                       $"sun {SunColor} x {SunIntensity:0.00}, ao {AoColor} x {AoIntensity:0.00}";
            }
        }

        internal struct Saved
        {
            public bool Valid;
            public bool Fog;
            public Color FogColor;
            public float FogDensity;
            public AmbientMode AmbientMode;
            public Color AmbientLight;
            public SphericalHarmonicsL2 AmbientProbe;
            public Color AmbientGlobal;
            public Color SunGlobal;
            public Color SunFogGlobal;
            public Vector4 SunDirGlobal;
            public Vector4 SkyboxSunDirGlobal;
            public float WetGlobal;
            public Color LightColor;
            public float LightIntensity;
            public Quaternion LightRotation;
            public LightRenderMode LightMode;
            public LightShadows LightShadows;
            public DefaultReflectionMode ReflectionMode;
            public Texture CustomReflection;
            public float ReflectionIntensity;
        }

        // The parts of the look, each switchable with "ientrance part <name>" to find which one
        // is off in game. Plain fields, not config: they are for finding out, not for players.

        /// <summary>Whether the dungeon's own fog is used.</summary>
        internal static bool DungeonFog = true;

        /// <summary>Whether the dungeon's ambient light is used; off, the ambient is black.</summary>
        internal static bool Ambient = true;

        /// <summary>Whether the dungeon's sun is used; off, its intensity is zero.</summary>
        internal static bool Sun = true;

        /// <summary>Whether the dungeon is dry whatever the weather outside (the <c>_Wet</c> shader global).</summary>
        internal static bool Dry = true;

        private static Cubemap black;

        /// <summary>What <see cref="Apply"/> last put in place, for <c>ientrance light</c>.</summary>
        internal static string Last = "nothing rendered yet";

        /// <summary>
        /// The four time-of-day weights exactly as <c>EnvMan.FixedUpdate</c> computes them. They
        /// are what the game blends every environment with, interiors included:
        /// <c>m_alwaysDark</c> only answers the daylight query, it does not darken the render.
        /// </summary>
        private static void Weights(EnvMan env, out float night, out float day, out float morning, out float evening)
        {
            float t = env.GetDayFraction();
            night = Mathf.Pow(Mathf.Max(1f - Mathf.Clamp01(t / 0.25f), Mathf.Clamp01((t - 0.75f) / 0.25f)), 0.5f);
            day = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(t - 0.5f) / 0.25f), 0.5f);
            morning = Mathf.Min(Mathf.Clamp01(1f - (t - 0.26f) / (0f - env.m_sunHorizonTransitionL)),
                                Mathf.Clamp01(1f - (t - 0.26f) / env.m_sunHorizonTransitionH));
            evening = Mathf.Min(Mathf.Clamp01(1f - (t - 0.74f) / (0f - env.m_sunHorizonTransitionH)),
                                Mathf.Clamp01(1f - (t - 0.74f) / env.m_sunHorizonTransitionL));
            float sum = 1f / (night + day + morning + evening);
            night *= sum;
            day *= sum;
            morning *= sum;
            evening *= sum;
        }

        /// <summary>
        /// The interior environment blended for right now, the way <c>SetEnv</c> does it. With no
        /// environment the outdoor values as they stand are returned, so the render still gets the
        /// vertex sun and the reflection swap.
        /// </summary>
        internal static Blend Compute(EnvSetup setup)
        {
            EnvMan env = EnvMan.instance;
            Light sun = env != null ? env.m_dirLight : null;
            var blend = new Blend
            {
                Found = false,
                FogColor = RenderSettings.fogColor,
                FogDensity = RenderSettings.fogDensity,
                SunFogColor = Shader.GetGlobalColor(SunFogColor),
                Ambient = RenderSettings.ambientLight,
                SunColor = sun != null ? sun.color : Color.black,
                SunIntensity = sun != null ? sun.intensity : 0f,
                SunRotation = sun != null ? sun.transform.rotation : Quaternion.identity,
                AoColor = Color.black,
                AoIntensity = 0f,
            };
            if (env == null || setup == null)
            {
                return blend;
            }

            Weights(env, out float night, out float day, out float morning, out float evening);
            blend.Found = true;
            blend.Night = night;
            blend.Day = day;
            blend.Morning = morning;
            blend.Evening = evening;

            blend.SunRotation = Quaternion.Euler(-90f + setup.m_sunAngle, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f) *
                                Quaternion.Euler(-90f + 360f * env.GetDayFraction(), 0f, 0f);
            if (night > 0f)
            {
                blend.SunRotation *= Quaternion.Euler(180f, 0f, 0f);
            }
            blend.SunIntensity = setup.m_lightIntensityDay * day + setup.m_lightIntensityNight * night;
            blend.SunColor = setup.m_sunColorNight * night;
            if (day > 0f)
            {
                blend.SunColor += setup.m_sunColorDay * day + setup.m_sunColorMorning * morning + setup.m_sunColorEvening * evening;
            }
            blend.FogColor = setup.m_fogColorNight * night + setup.m_fogColorDay * day +
                             setup.m_fogColorMorning * morning + setup.m_fogColorEvening * evening;
            Color sunFog = setup.m_fogColorSunNight * night;
            if (day > 0f)
            {
                sunFog += setup.m_fogColorSunDay * day + setup.m_fogColorSunMorning * morning + setup.m_fogColorSunEvening * evening;
            }
            blend.SunFogColor = Color.Lerp(blend.FogColor, sunFog, Mathf.Clamp01(Mathf.Max(night, day) * 3f));
            blend.FogDensity = setup.m_fogDensityNight * night + setup.m_fogDensityDay * day +
                               setup.m_fogDensityMorning * morning + setup.m_fogDensityEvening * evening;
            blend.Ambient = Color.Lerp(setup.m_ambColorNight, setup.m_ambColorDay, day);
            blend.AoColor = setup.m_ambientOcclusionColor;
            blend.AoIntensity = setup.m_aoIntensityDay * day + setup.m_aoIntensityEvening * evening +
                                setup.m_aoIntensityNight * night + setup.m_aoIntensityMorning * morning;
            return blend;
        }

        /// <summary>
        /// Puts the blend in place: fog, ambient, sun and the shader globals, the sun switched to
        /// per-vertex without shadows as <c>EnvMan</c> does whenever the player is in an interior
        /// (left per-pixel, the outdoor sun floods the whole dungeon), the dungeon dry, and the
        /// sky reflection replaced by <paramref name="reflection"/> - the dungeon's own cubemap,
        /// or black when there is none. <c>ReflectionUpdate</c> keeps the game's two probes on the
        /// player, 1000 units tall, so the dungeon 5000 up falls outside both and would reflect
        /// the default sky: daylight at any hour.
        /// </summary>
        internal static void Apply(Blend blend, Texture reflection, out Saved saved)
        {
            saved = default;
            EnvMan env = EnvMan.instance;
            Light sun = env != null ? env.m_dirLight : null;
            if (sun == null)
            {
                Last = "no EnvMan or sun";
                return;
            }

            saved = new Saved
            {
                Valid = true,
                Fog = RenderSettings.fog,
                FogColor = RenderSettings.fogColor,
                FogDensity = RenderSettings.fogDensity,
                AmbientMode = RenderSettings.ambientMode,
                AmbientLight = RenderSettings.ambientLight,
                AmbientProbe = RenderSettings.ambientProbe,
                AmbientGlobal = Shader.GetGlobalColor(AmbientColor),
                SunGlobal = Shader.GetGlobalColor(SunColor),
                SunFogGlobal = Shader.GetGlobalColor(SunFogColor),
                SunDirGlobal = Shader.GetGlobalVector(SunDir),
                SkyboxSunDirGlobal = Shader.GetGlobalVector(SkyboxSunDir),
                WetGlobal = Shader.GetGlobalFloat(Wet),
                LightColor = sun.color,
                LightIntensity = sun.intensity,
                LightRotation = sun.transform.rotation,
                LightMode = sun.renderMode,
                LightShadows = sun.shadows,
                ReflectionMode = RenderSettings.defaultReflectionMode,
                CustomReflection = RenderSettings.customReflectionTexture,
                ReflectionIntensity = RenderSettings.reflectionIntensity,
            };

            Color ambient = Ambient ? blend.Ambient : Color.black;
            float intensity = Sun ? blend.SunIntensity : 0f;
            float density = DungeonFog ? blend.FogDensity : 0f;

            RenderSettings.fog = density > 0f;
            RenderSettings.fogColor = blend.FogColor;
            RenderSettings.fogDensity = density;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            // Set outright: Unity may only rebuild the probe from ambientLight later in the frame.
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe;
            sun.transform.rotation = blend.SunRotation;
            sun.color = blend.SunColor;
            sun.intensity = intensity;
            sun.renderMode = LightRenderMode.ForceVertex;
            sun.shadows = LightShadows.None;
            Vector3 sunDir = -sun.transform.forward;
            Shader.SetGlobalColor(AmbientColor, ambient);
            Shader.SetGlobalColor(SunColor, blend.SunColor * intensity);
            Shader.SetGlobalColor(SunFogColor, blend.SunFogColor);
            Shader.SetGlobalVector(SunDir, sunDir);
            Shader.SetGlobalVector(SkyboxSunDir, sunDir);
            if (Dry)
            {
                Shader.SetGlobalFloat(Wet, 0f);
            }
            UseReflection(reflection, saved.ReflectionIntensity);

            Last = $"{(blend.Found ? "interior" : "NOT found, outdoor values as they are")}: {blend}; " +
                   $"reflection {(reflection != null ? reflection.name : "black")}, wet {(Dry ? "0" : saved.WetGlobal.ToString("0.00"))}";
        }

        /// <summary>Swaps the reflection everything outside the game's probes gets; null for black.</summary>
        internal static void UseReflection(Texture reflection, float intensity)
        {
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection != null ? reflection : Black();
            RenderSettings.reflectionIntensity = reflection != null ? intensity : 0f;
        }

        internal static void Restore(Saved saved)
        {
            if (!saved.Valid)
            {
                return;
            }
            RenderSettings.fog = saved.Fog;
            RenderSettings.fogColor = saved.FogColor;
            RenderSettings.fogDensity = saved.FogDensity;
            RenderSettings.ambientMode = saved.AmbientMode;
            RenderSettings.ambientLight = saved.AmbientLight;
            RenderSettings.ambientProbe = saved.AmbientProbe;
            Shader.SetGlobalColor(AmbientColor, saved.AmbientGlobal);
            Shader.SetGlobalColor(SunColor, saved.SunGlobal);
            Shader.SetGlobalColor(SunFogColor, saved.SunFogGlobal);
            Shader.SetGlobalVector(SunDir, saved.SunDirGlobal);
            Shader.SetGlobalVector(SkyboxSunDir, saved.SkyboxSunDirGlobal);
            Shader.SetGlobalFloat(Wet, saved.WetGlobal);
            RenderSettings.defaultReflectionMode = saved.ReflectionMode;
            RenderSettings.customReflectionTexture = saved.CustomReflection;
            RenderSettings.reflectionIntensity = saved.ReflectionIntensity;
            Light sun = EnvMan.instance != null ? EnvMan.instance.m_dirLight : null;
            if (sun != null)
            {
                sun.transform.rotation = saved.LightRotation;
                sun.color = saved.LightColor;
                sun.intensity = saved.LightIntensity;
                sun.renderMode = saved.LightMode;
                sun.shadows = saved.LightShadows;
            }
        }

        private static Cubemap Black()
        {
            if (black == null)
            {
                black = new Cubemap(4, TextureFormat.RGBA32, false) { name = "ImmersiveEntrance Black" };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.black;
                }
                foreach (CubemapFace face in new[]
                         {
                             CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                             CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ,
                         })
                {
                    black.SetPixels(pixels, face);
                }
                black.Apply();
            }
            return black;
        }

        /// <summary>
        /// The torches near an exit door. <c>LightLod</c> switches every light off, and its shadow
        /// off, that is far from the player, and the dungeon is 5000 units up, so these are lit -
        /// with shadows for those close to the door - only for the portal render, and left exactly
        /// as <c>LightLod</c> had them afterwards; its fade coroutine never sees the difference.
        /// The player's point light and shadow limits are honoured, nearest to the door first,
        /// as <c>LightLod</c> honours them nearest to the player first.
        /// </summary>
        internal sealed class Lights
        {
            private const float Range = 40f;

            private struct State
            {
                public bool Enabled;
                public float Range;
                public LightShadows Shadows;
                public float ShadowStrength;
            }

            private struct Near
            {
                public LightLod Lod;
                public float Distance;
            }

            private readonly List<Near> near = new List<Near>();
            private readonly List<State> saved = new List<State>();

            internal int Count => near.Count;
            internal int Lit { get; private set; }
            internal int Shadowed { get; private set; }

            internal void Gather(Vector3 around)
            {
                near.Clear();
                foreach (LightLod lod in LightLod.m_lights)
                {
                    if (lod == null || lod.m_light == null || (!lod.m_lightLod && !lod.m_shadowLod))
                    {
                        continue;
                    }
                    float distance = Vector3.Distance(lod.transform.position, around);
                    if (distance < Range)
                    {
                        near.Add(new Near { Lod = lod, Distance = distance });
                    }
                }
                near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            internal void On()
            {
                saved.Clear();
                Lit = 0;
                Shadowed = 0;
                for (int i = 0; i < near.Count; i++)
                {
                    LightLod lod = near[i].Lod;
                    Light light = lod != null ? lod.m_light : null;
                    if (light == null)
                    {
                        saved.Add(default);
                        continue;
                    }
                    saved.Add(new State
                    {
                        Enabled = light.enabled,
                        Range = light.range,
                        Shadows = light.shadows,
                        ShadowStrength = light.shadowStrength,
                    });
                    if (lod.m_lightLod && (LightLod.m_lightLimit < 0 || Lit < LightLod.m_lightLimit))
                    {
                        light.enabled = true;
                        light.range = lod.m_baseRange;
                        Lit++;
                    }
                    if (lod.m_shadowLod && near[i].Distance < lod.m_shadowDistance &&
                        (LightLod.m_shadowLimit < 0 || Shadowed < LightLod.m_shadowLimit))
                    {
                        light.shadows = LightShadows.Soft;
                        light.shadowStrength = lod.m_baseShadowStrength;
                        Shadowed++;
                    }
                }
            }

            internal void Restore()
            {
                for (int i = 0; i < near.Count && i < saved.Count; i++)
                {
                    Light light = near[i].Lod != null ? near[i].Lod.m_light : null;
                    if (light != null)
                    {
                        light.enabled = saved[i].Enabled;
                        light.range = saved[i].Range;
                        light.shadows = saved[i].Shadows;
                        light.shadowStrength = saved[i].ShadowStrength;
                    }
                }
            }
        }
    }
}
