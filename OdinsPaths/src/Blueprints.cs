using BepInEx;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OdinsPaths
{
    /// <summary>What a piece of a blueprint is, for how likely the weather takes it (<see cref="Builder"/>).</summary>
    internal enum Role
    {
        /// <summary>A plank or floor to walk on; goes at times, never near the land end or under something that stands on it.</summary>
        Deck,
        /// <summary>A floor that stays: a hut's, what the walls and furniture stand on.</summary>
        Floor,
        /// <summary>Below the deck or floor: always stays, and the lowest of each column is carried on down to the ground.</summary>
        Pile,
        Wall,
        /// <summary>Goes at times, and always when no wall is left standing within the blueprint's roofReach.</summary>
        Roof,
        /// <summary>A mooring post, a railing post: goes at times, with the lamp on it.</summary>
        Post,
        /// <summary>A lamp on a post: goes with its post.</summary>
        Lamp,
        /// <summary>Furniture: a relic copy that drops nothing (<see cref="Relics"/>).</summary>
        Deco,
        /// <summary>Loose things lying about: never weathered away, they are the weathering.</summary>
        Clutter,
        /// <summary>Anything else: stays as it is.</summary>
        Keep,
    }

    /// <summary>Which point of a piece's measured box goes to its <c>pos</c>.</summary>
    internal enum Anchor
    {
        /// <summary>The prefab's own pivot, as the game placed it: what <c>docks capture</c> writes.</summary>
        Pivot,
        /// <summary>The top face's centre: decks, whose top is the walking surface.</summary>
        Top,
        /// <summary>The bottom face's centre: walls, furniture, standing on the deck.</summary>
        Bottom,
    }

    /// <summary>
    /// A dock or a building, as a JSON file in <c>harbours/</c> beside the DLL (shipped from the
    /// mod's <c>assets/harbours/</c>) or in <c>BepInEx/config/OdinsPaths/harbours/</c> (the
    /// player's own, and what <c>docks capture</c> writes; one of the same name replaces the
    /// shipped one). Plain JSON, read by <see cref="Json"/> (Unity's <c>JsonUtility</c> left the
    /// piece lists empty in game); a field left out is 0, false or empty. The format is in <c>docs/docks.md</c>.
    /// </summary>
    [Serializable]
    public sealed class Blueprint
    {
        public string name;
        /// <summary>"dock" or "building".</summary>
        public string kind;
        /// <summary><c>Heightmap.Biome</c> names; none: any biome.</summary>
        public string[] biomes;
        /// <summary>How often it is picked among those that fit, against the others' (0 counts as 1).</summary>
        public float weight;
        /// <summary>Starts snowed over (the game's <c>preSnow</c>), as the Deep North's own buildings do.</summary>
        public bool preSnow;
        /// <summary>How far, in metres, a roof piece may be from a standing wall and still hold (0: 3 m).</summary>
        public float roofReach;
        /// <summary>The furniture a "deco" spot picks from.</summary>
        public string[] deco;
        /// <summary>Loose pieces some deck pieces get, clutterChance each.</summary>
        public string[] clutter;
        public float clutterChance;
        public List<BlueprintPiece> pieces = new List<BlueprintPiece>();
        public List<BlueprintSpot> spots = new List<BlueprintSpot>();

        [NonSerialized] internal string File;
        [NonSerialized] internal bool IsDock;
        [NonSerialized] internal Heightmap.Biome Biomes;

        internal float RoofReach => roofReach > 0f ? roofReach : 3f;
        internal float Weight => weight > 0f ? weight : 1f;
        internal bool Fits(Heightmap.Biome biome) => Biomes == Heightmap.Biome.None || (Biomes & biome) != 0;
    }

    /// <summary>
    /// One piece, in the blueprint's frame: a dock's origin is the middle of its land end, where
    /// the road runs onto it, z out to sea, x to the right looking out, y up from the deck's top
    /// there (the road's height); a building's is the middle of its front, the side facing the
    /// road, z into the building, x to the right looking in, y up from its floor's top.
    /// </summary>
    [Serializable]
    public sealed class BlueprintPiece
    {
        public string prefab;
        /// <summary>[x, y, z] in metres.</summary>
        public float[] pos;
        /// <summary>[yaw], or [x, y, z] Euler angles in degrees, or [x, y, z, w] a quaternion; none: unturned.</summary>
        public float[] rot;
        /// <summary>A <see cref="OdinsPaths.Role"/> by name; none: keep.</summary>
        public string role;
        /// <summary>"pivot" (none), "top" or "bottom": which point of its measured box goes to pos.</summary>
        public string anchor;

        [NonSerialized] internal Vector3 Position;
        [NonSerialized] internal Quaternion Rotation;
        [NonSerialized] internal Role Role;
        [NonSerialized] internal Anchor Anchor;
    }

    /// <summary>A place something may go: "chest", "enemy", "deco" (furniture from the deco list), or "stone" (a dock's harbour stone).</summary>
    [Serializable]
    public sealed class BlueprintSpot
    {
        public string kind;
        public float[] pos;
        public float yaw;

        [NonSerialized] internal Vector3 Position;
    }

    internal static class Blueprints
    {
        public const string Folder = "harbours";

        private static List<Blueprint> all;

        /// <summary>Every blueprint, read the first time it is asked for.</summary>
        public static List<Blueprint> All
        {
            get
            {
                if (all == null)
                {
                    Load();
                }
                return all;
            }
        }

        /// <summary>The player's own folder, where captures go: <c>BepInEx/config/OdinsPaths/harbours</c>.</summary>
        public static string UserFolder => Path.Combine(Path.Combine(Paths.ConfigPath, "OdinsPaths"), Folder);

        private static string ShippedFolder => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", Folder);

        /// <summary>Reads both folders again; a file in the player's folder replaces a shipped one of the same name.</summary>
        public static void Load()
        {
            Dictionary<string, Blueprint> byName = new Dictionary<string, Blueprint>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in new[] { ShippedFolder, UserFolder })
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                string[] files = Directory.GetFiles(folder, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    Blueprint blueprint = Read(file);
                    if (blueprint != null)
                    {
                        byName[blueprint.name] = blueprint;
                    }
                }
            }
            all = new List<Blueprint>(byName.Values);
            all.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            Debug.Log("[OdinsPaths] " + all.Count + " harbour blueprints (" + ShippedFolder + ", " + UserFolder + ").");
        }

        /// <summary>A blueprint from its file, checked and made ready; null (with a warning) if it cannot be used.</summary>
        public static Blueprint Read(string file)
        {
            Blueprint blueprint;
            try
            {
                blueprint = FromJson(Json.Parse(File.ReadAllText(file)) as Dictionary<string, object>);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OdinsPaths] Harbour blueprint " + file + " is no valid JSON - left out: " + e.Message);
                return null;
            }
            if (blueprint == null)
            {
                return null;
            }
            blueprint.File = file;
            if (string.IsNullOrEmpty(blueprint.name))
            {
                blueprint.name = Path.GetFileNameWithoutExtension(file);
            }
            string kind = (blueprint.kind ?? "").Trim().ToLowerInvariant();
            if (kind != "dock" && kind != "building")
            {
                Debug.LogWarning("[OdinsPaths] Harbour blueprint " + file + ": kind must be \"dock\" or \"building\" - left out.");
                return null;
            }
            blueprint.IsDock = kind == "dock";
            blueprint.Biomes = Heightmap.Biome.None;
            foreach (string name in blueprint.biomes ?? new string[0])
            {
                try
                {
                    blueprint.Biomes |= (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), name.Trim(), true);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("[OdinsPaths] Harbour blueprint " + blueprint.name + ": no biome " + name + ".");
                }
            }
            List<BlueprintPiece> pieces = new List<BlueprintPiece>();
            foreach (BlueprintPiece piece in blueprint.pieces ?? new List<BlueprintPiece>())
            {
                if (piece == null || string.IsNullOrEmpty(piece.prefab) || piece.pos == null || piece.pos.Length != 3)
                {
                    Debug.LogWarning("[OdinsPaths] Harbour blueprint " + blueprint.name + ": a piece without prefab or [x, y, z] pos - left out.");
                    continue;
                }
                piece.Position = new Vector3(piece.pos[0], piece.pos[1], piece.pos[2]);
                piece.Rotation = Rotation(piece.rot);
                piece.Role = Parse(piece.role, Role.Keep, blueprint.name);
                piece.Anchor = Parse(piece.anchor, Anchor.Pivot, blueprint.name);
                pieces.Add(piece);
            }
            blueprint.pieces = pieces;
            List<BlueprintSpot> spots = new List<BlueprintSpot>();
            foreach (BlueprintSpot spot in blueprint.spots ?? new List<BlueprintSpot>())
            {
                if (spot == null || spot.pos == null || spot.pos.Length != 3)
                {
                    continue;
                }
                spot.kind = (spot.kind ?? "").Trim().ToLowerInvariant();
                spot.Position = new Vector3(spot.pos[0], spot.pos[1], spot.pos[2]);
                spots.Add(spot);
            }
            blueprint.spots = spots;
            if (pieces.Count == 0)
            {
                Debug.LogWarning("[OdinsPaths] Harbour blueprint " + blueprint.name + " has no pieces - left out.");
                return null;
            }
            return blueprint;
        }

        /// <summary>A blueprint from a parsed JSON object; null if the text was no object.</summary>
        private static Blueprint FromJson(Dictionary<string, object> o)
        {
            if (o == null)
            {
                return null;
            }
            Blueprint blueprint = new Blueprint
            {
                name = Json.String(o, "name"),
                kind = Json.String(o, "kind"),
                biomes = Json.Strings(o, "biomes"),
                weight = Json.Float(o, "weight"),
                preSnow = Json.Bool(o, "preSnow"),
                roofReach = Json.Float(o, "roofReach"),
                deco = Json.Strings(o, "deco"),
                clutter = Json.Strings(o, "clutter"),
                clutterChance = Json.Float(o, "clutterChance"),
            };
            foreach (Dictionary<string, object> p in Json.Objects(o, "pieces"))
            {
                blueprint.pieces.Add(new BlueprintPiece
                {
                    prefab = Json.String(p, "prefab"),
                    pos = Json.Floats(p, "pos"),
                    rot = Json.Floats(p, "rot"),
                    role = Json.String(p, "role"),
                    anchor = Json.String(p, "anchor"),
                });
            }
            foreach (Dictionary<string, object> p in Json.Objects(o, "spots"))
            {
                blueprint.spots.Add(new BlueprintSpot
                {
                    kind = Json.String(p, "kind"),
                    pos = Json.Floats(p, "pos"),
                    yaw = Json.Float(p, "yaw"),
                });
            }
            return blueprint;
        }

        private static T Parse<T>(string text, T otherwise, string blueprint) where T : struct
        {
            if (string.IsNullOrEmpty(text))
            {
                return otherwise;
            }
            try
            {
                return (T)Enum.Parse(typeof(T), text.Trim(), true);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("[OdinsPaths] Harbour blueprint " + blueprint + ": no " + typeof(T).Name.ToLowerInvariant() + " " + text + ".");
                return otherwise;
            }
        }

        private static Quaternion Rotation(float[] rot)
        {
            if (rot == null || rot.Length == 0)
            {
                return Quaternion.identity;
            }
            if (rot.Length == 1)
            {
                return Quaternion.Euler(0f, rot[0], 0f);
            }
            if (rot.Length == 4)
            {
                return new Quaternion(rot[0], rot[1], rot[2], rot[3]).normalized;
            }
            return Quaternion.Euler(rot[0], rot[1], rot.Length > 2 ? rot[2] : 0f);
        }

        public static Blueprint Named(string name)
        {
            return All.Find(b => string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The blueprints of a kind for a biome, in a random order weighted by each one's weight.</summary>
        public static List<Blueprint> Shuffled(bool dock, Heightmap.Biome biome, System.Random rng)
        {
            List<Blueprint> pool = All.FindAll(b => b.IsDock == dock && b.Fits(biome));
            if (pool.Count == 0)
            {
                // A biome nothing is drawn for yet builds as the Meadows do.
                pool = All.FindAll(b => b.IsDock == dock && b.Fits(Heightmap.Biome.Meadows));
            }
            List<Blueprint> order = new List<Blueprint>();
            while (pool.Count > 0)
            {
                float total = 0f;
                foreach (Blueprint b in pool)
                {
                    total += b.Weight;
                }
                double pick = rng.NextDouble() * total;
                int at = 0;
                for (; at < pool.Count - 1; at++)
                {
                    pick -= pool[at].Weight;
                    if (pick < 0)
                    {
                        break;
                    }
                }
                order.Add(pool[at]);
                pool.RemoveAt(at);
            }
            return order;
        }

        /// <summary>Writes a blueprint as JSON, one piece a line, and returns the path.</summary>
        public static string Save(Blueprint blueprint)
        {
            Directory.CreateDirectory(UserFolder);
            string path = Path.Combine(UserFolder, blueprint.name + ".json");
            StringBuilder s = new StringBuilder();
            s.Append("{\n");
            s.Append("  \"name\": ").Append(Quote(blueprint.name)).Append(",\n");
            s.Append("  \"kind\": ").Append(Quote(blueprint.IsDock ? "dock" : "building")).Append(",\n");
            s.Append("  \"biomes\": ").Append(Strings(blueprint.biomes)).Append(",\n");
            s.Append("  \"weight\": ").Append(F(blueprint.Weight)).Append(",\n");
            s.Append("  \"preSnow\": ").Append(blueprint.preSnow ? "true" : "false").Append(",\n");
            s.Append("  \"roofReach\": ").Append(F(blueprint.RoofReach)).Append(",\n");
            s.Append("  \"deco\": ").Append(Strings(blueprint.deco)).Append(",\n");
            s.Append("  \"clutter\": ").Append(Strings(blueprint.clutter)).Append(",\n");
            s.Append("  \"clutterChance\": ").Append(F(blueprint.clutterChance)).Append(",\n");
            s.Append("  \"pieces\": [\n");
            for (int i = 0; i < blueprint.pieces.Count; i++)
            {
                BlueprintPiece p = blueprint.pieces[i];
                s.Append("    {\"prefab\": ").Append(Quote(p.prefab)).Append(", \"pos\": ").Append(Floats(p.pos));
                if (p.rot != null && p.rot.Length > 0)
                {
                    s.Append(", \"rot\": ").Append(Floats(p.rot));
                }
                s.Append(", \"role\": ").Append(Quote(p.role ?? "keep"));
                if (!string.IsNullOrEmpty(p.anchor) && !p.anchor.Equals("pivot", StringComparison.OrdinalIgnoreCase))
                {
                    s.Append(", \"anchor\": ").Append(Quote(p.anchor));
                }
                s.Append(i + 1 < blueprint.pieces.Count ? "},\n" : "}\n");
            }
            s.Append("  ],\n");
            s.Append("  \"spots\": [\n");
            for (int i = 0; i < blueprint.spots.Count; i++)
            {
                BlueprintSpot spot = blueprint.spots[i];
                s.Append("    {\"kind\": ").Append(Quote(spot.kind)).Append(", \"pos\": ").Append(Floats(spot.pos))
                    .Append(", \"yaw\": ").Append(F(spot.yaw)).Append(i + 1 < blueprint.spots.Count ? "},\n" : "}\n");
            }
            s.Append("  ]\n}\n");
            File.WriteAllText(path, s.ToString());
            return path;
        }

        private static string Quote(string text) => "\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string Strings(string[] items)
        {
            if (items == null || items.Length == 0)
            {
                return "[]";
            }
            StringBuilder s = new StringBuilder("[");
            for (int i = 0; i < items.Length; i++)
            {
                s.Append(i > 0 ? ", " : "").Append(Quote(items[i]));
            }
            return s.Append(']').ToString();
        }

        private static string Floats(float[] values)
        {
            StringBuilder s = new StringBuilder("[");
            for (int i = 0; i < values.Length; i++)
            {
                s.Append(i > 0 ? ", " : "").Append(F(values[i]));
            }
            return s.Append(']').ToString();
        }

        internal static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
