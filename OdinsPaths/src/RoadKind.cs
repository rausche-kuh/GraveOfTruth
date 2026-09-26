using BepInEx.Configuration;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>
    /// How a road is laid: which paint, how wide, how hard its edge, and whether and how far the
    /// ground under it is levelled. Two kinds (docs/network.md): a main road is paved stone, wide,
    /// with a kerb rather than a worn fringe, and levelled; a spur is a narrow dirt track that
    /// follows the ground more closely. Width, its drift and the levelling are settings, one
    /// section per kind; the paint and the edge are the kind's own.
    /// </summary>
    internal sealed class RoadKind
    {
        /// <summary>Paved stone, the texture a player's paving gives; it clears the Deep North's snow too.</summary>
        public static RoadKind Main;
        /// <summary>Dirt, as a hoe's or a path's.</summary>
        public static RoadKind Spur;

        /// <summary>What the network stores for a road of this kind.</summary>
        public readonly byte Id;
        public readonly string Name;
        /// <summary>The paint mask colour r, g and b lerp toward (<c>Heightmap.m_paintMask*</c>); alpha is kept.</summary>
        public readonly Color Paint;
        /// <summary>Over how many metres inside the edge the paint fades in.</summary>
        public readonly float EdgeSoftness;
        /// <summary>
        /// Where the ground is steep, how far the levelling may cut or fill anyway (never less
        /// than MaxCut): enough to hold the road level across a slope and its grade down to
        /// <see cref="MaxGrade"/>. MaxCut alone left steep stretches tilted and lumpy (seen in
        /// game 2026-09-25). A spur keeps to its MaxCut.
        /// </summary>
        public readonly float SteepCut;
        /// <summary>The steepest grade (rise over run) the levelling cuts a road down to where it can; 0: none.</summary>
        public readonly float MaxGrade;
        /// <summary>
        /// The ground's slope (rise over run) at which the paint starts to fade, and where it is
        /// gone: stone on a slope looked drawn on, so a main road paints its flat bench only.
        /// </summary>
        public readonly float PaintSlopeFade;
        public readonly float MaxPaintSlope;
        /// <summary>
        /// Levelled flat out to the widest the paint reaches, and dropping off sharply past it:
        /// a bench with an edge, not a rounded hump the paint runs down.
        /// </summary>
        public readonly bool SharpEdge;

        private readonly ConfigEntry<float> width;
        private readonly ConfigEntry<float> variation;
        private readonly ConfigEntry<bool> levelling;
        private readonly ConfigEntry<float> maxCut;

        /// <summary>Over about this many metres the width drifts from narrow to wide and back.</summary>
        private const float WidthDrift = 30f;

        private RoadKind(byte id, string name, Color paint, float edgeSoftness, float steepCut, float maxGrade,
            float paintSlopeFade, float maxPaintSlope, bool sharpEdge, ConfigEntry<float> width,
            ConfigEntry<float> variation, ConfigEntry<bool> levelling, ConfigEntry<float> maxCut)
        {
            Id = id;
            Name = name;
            Paint = paint;
            EdgeSoftness = edgeSoftness;
            SteepCut = steepCut;
            MaxGrade = maxGrade;
            PaintSlopeFade = paintSlopeFade;
            MaxPaintSlope = maxPaintSlope;
            SharpEdge = sharpEdge;
            this.width = width;
            this.variation = variation;
            this.levelling = levelling;
            this.maxCut = maxCut;
        }

        public float Width => width.Value;
        public bool Levelling => levelling.Value;
        public float MaxCut => maxCut.Value;
        /// <summary>Whether its paint is stone - the writer treats stone already there as a road's or a player's.</summary>
        public bool Paved => Paint.b > 0.5f;

        /// <summary>Half the widest this kind gets.</summary>
        public float MaxHalfWidth => (width.Value + variation.Value) * 0.5f;

        /// <summary>How far past half its width a road is levelled flat: to the paint's widest wobble with a sharp edge.</summary>
        public float FlatFactor => SharpEdge ? 1f + TerrainWriter.EdgeWobble : 1f;

        /// <summary>How deep the levelling may go where the ground asks for it.</summary>
        public float DeepestCut => Mathf.Max(MaxCut, SteepCut);

        /// <summary>How far from the trail's line the writer may change anything: edge and shoulder.</summary>
        public float Reach => MaxHalfWidth * (1f + TerrainWriter.EdgeWobble) + TerrainWriter.Shoulder;

        /// <summary>The widest reach of any kind, for what has to keep clear of every road.</summary>
        public static float MaxReach => Mathf.Max(Main.Reach, Spur.Reach);

        /// <summary>
        /// Half the road's width at a point: the configured width, drifting by up to the variation
        /// either way. Slow noise over the world, offset by its seed - a trail's two edges get the
        /// same width, and where two roads of a kind meet they agree on it.
        /// </summary>
        public float HalfWidthAt(Vector2 at)
        {
            int seed = WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 0;
            float offset = (seed & 0x3ff) * 1.37f;
            // Perlin noise rarely strays far from 0.5; stretched, the road reaches both extremes.
            float noise = Mathf.Clamp((Mathf.PerlinNoise(at.x / WidthDrift + offset, at.y / WidthDrift - offset) - 0.5f) * 2.5f, -1f, 1f);
            return Mathf.Max(0.5f, width.Value + variation.Value * noise) * 0.5f;
        }

        /// <summary>
        /// Settings still at the defaults before 2026-09-25 take the new ones: main roads 4 m and
        /// 0.3 became 5 m and 1 (4 to 6 m, open stretches and narrows, asked for after the first
        /// lay); spurs 2.5 m and 0.5 became 3.5 m and 0.75 (barely visible).
        /// </summary>
        internal static void UpgradeDefaults()
        {
            Upgrade(Main.width, 4f, 5f);
            Upgrade(Main.variation, 0.3f, 1f);
            Upgrade(Spur.width, 2.5f, 3.5f);
            Upgrade(Spur.variation, 0.5f, 0.75f);
        }

        private static void Upgrade(ConfigEntry<float> entry, float old, float now)
        {
            if (Mathf.Approximately(entry.Value, old))
            {
                entry.Value = now;
            }
        }

        public static RoadKind ById(byte id) => id == Spur.Id ? Spur : Main;

        /// <summary>"main"/"stone" or "spur"/"dirt"; null for anything else.</summary>
        public static RoadKind Parse(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "main": case "stone": case "paved": return Main;
                case "spur": case "dirt": return Spur;
                default: return null;
            }
        }

        public override string ToString() => Name;

        /// <summary>Both kinds' settings, one section each.</summary>
        internal static void Bind(ConfigFile config)
        {
            Main = new RoadKind(0, "main road", Heightmap.m_paintMaskPaved, 0.3f, 4f, 0.22f, 0.4f, 0.55f, true,
                config.Bind("MainRoads", "Width", 5f, new ConfigDescription(
                    "How wide a main road - the paved roads to the boss altars and the traders - is, in metres.",
                    new AcceptableValueRange<float>(1f, 8f))),
                config.Bind("MainRoads", "WidthVariation", 1f, new ConfigDescription(
                    "How far a main road's width drifts from Width along its length, in metres either way. 0 keeps it even.",
                    new AcceptableValueRange<float>(0f, 2f))),
                config.Bind("MainRoads", "Levelling", true,
                    "Smooth the ground along a main road so it rises and falls gently instead of following " +
                    "every bump. Ground a player has already dug or raised is never touched."),
                config.Bind("MainRoads", "MaxCut", 1f, new ConfigDescription(
                    "How far levelling may cut into or raise the ground under a main road, in metres.",
                    new AcceptableValueRange<float>(0f, 4f))));
            Spur = new RoadKind(1, "spur", Heightmap.m_paintMaskDirt, 0.5f, 0f, 0f, 0.7f, 0.9f, false,
                config.Bind("Spurs", "Width", 3.5f, new ConfigDescription(
                    "How wide a spur - the dirt tracks from a main road to the villages, crypts and caves near it - is, in metres.",
                    new AcceptableValueRange<float>(1f, 8f))),
                config.Bind("Spurs", "WidthVariation", 0.75f, new ConfigDescription(
                    "How far a spur's width drifts from Width along its length, in metres either way, " +
                    "so it narrows and widens like a trodden path. 0 keeps it even.",
                    new AcceptableValueRange<float>(0f, 2f))),
                config.Bind("Spurs", "Levelling", true,
                    "Smooth the ground along a spur a little. Ground a player has already dug or raised is never touched."),
                config.Bind("Spurs", "MaxCut", 0.4f, new ConfigDescription(
                    "How far levelling may cut into or raise the ground under a spur, in metres. Less than a " +
                    "main road's, so a spur keeps more of the ground's bumps.",
                    new AcceptableValueRange<float>(0f, 4f))));
        }
    }
}
