using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveEntrance
{
    /// <summary>
    /// One entrance drawn as a window. A camera of our own stands behind the exit door, posed
    /// relative to the exit frame the way the main camera stands relative to the entrance frame,
    /// turned 180 degrees so walking in looks the same way the teleport faces you.
    ///
    /// It looks straight along the door's normal through an off-axis frustum whose near plane is
    /// the door itself: the four corners of the doorway rectangle, seen from the eye, bound the
    /// view. What it renders is therefore exactly the picture that belongs on the door, so the
    /// doorway is a plain textured quad with unit UVs, every texel is spent on the door, and the
    /// depth buffer is regular - the fog pass measures true distance beyond the door, which is
    /// the amount of dungeon fog a point behind the door should get.
    /// </summary>
    internal sealed class Portal
    {
        /// <summary>Cells per side of the doorway quad; the outdoor fog is per vertex, so a few help.</summary>
        private const int Grid = 4;

        /// <summary>How far the quad floats in front of the black surface, against z-fighting.</summary>
        private const float Lift = 0.01f;

        /// <summary>Closer than this to the door plane the frustum is too narrow to be stable.</summary>
        private const float MinDepth = 0.05f;

        /// <summary>How often the lights near the exit are gathered again.</summary>
        private const float LightScanInterval = 1f;

        private static readonly Quaternion Turn = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>How far in from the exit door the dungeon's dust is set up.</summary>
        private const float DustDepth = 4f;

        /// <summary>How many seconds of dust are simulated before the first frame, so it never fades in.</summary>
        private const float DustPrewarm = 5f;

        /// <summary>Where the dungeon's reflection is taken from: this far in from the exit door, at the frame's height.</summary>
        private const float ReflectionDepth = 3f;
        private const int ReflectionSize = 64;

        /// <summary>
        /// Where in front of each door the floor is measured - the highest hit wins, which is the
        /// threshold: 0.6 m into a burial chamber is already the first step down the stairs, and
        /// measuring there left the view 15 cm too low - and how far down it is looked for.
        /// </summary>
        internal static readonly float[] FloorProbes = { 0.1f, 0.25f, 0.4f, 0.6f };
        private const float FloorProbeDepth = 6f;

        private static readonly int FloorMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        private static Shader shader;

        // ---- Calibration -----------------------------------------------------------------------
        // Plain fields, set by the dev commands: they are for lining the view up, not for players.

        /// <summary>
        /// How the floor inside is lined up with the ground outside. The two teleport triggers do
        /// not sit at the same height above their floors - in a burial chamber about 30 cm apart -
        /// so matching the triggers alone let the player look through the dungeon floor.
        /// <c>Boxes</c> takes the bottom edges of the game's black and white boxes, which the
        /// designers placed to fill each opening; <c>Probes</c> raycasts the floor in front of each
        /// door and adds the per-location corrections in <see cref="Offsets"/>. <c>Auto</c> takes
        /// the boxes where both bottoms sit on their floors within <see cref="BoxTolerance"/> and
        /// the probes otherwise - a cave mouth's boxes are sunk into the ground. The depth is
        /// measured from the boxes' visible faces in every mode but <c>Off</c>.
        /// </summary>
        internal enum FloorMode { Auto, Boxes, Probes, Off }
        internal static FloorMode Floor = FloorMode.Auto;
        /// <summary>How far a box bottom may be off its probed floor before <c>Auto</c> distrusts the boxes.</summary>
        internal static float BoxTolerance = 0.25f;

        /// <summary>Whether the view is laid over the game's black box; off, or without one, a <see cref="ManualSize"/> rectangle is used.</summary>
        internal static bool AutoDetect = true;
        internal static Vector2 ManualSize = new Vector2(2.5f, 3f);
        /// <summary>Where the doorway's centre is relative to the teleport trigger: right, up, towards the player.</summary>
        internal static Vector3 ManualOffset = new Vector3(0f, 0.5f, 0f);
        /// <summary>How far into the dungeon, past the exit trigger, the near plane is at least.</summary>
        internal static float ClipOffset = 0.05f;
        /// <summary>Shifts the eye behind the door, in the exit frame: right, up, into the dungeon.</summary>
        internal static Vector3 CameraOffset = Vector3.zero;

        /// <summary>Samples the texture upside down, the escape hatch should a graphics API flip it (<c>ientrance flip</c>).</summary>
        internal static bool FlipV = false;

        /// <summary>
        /// Whether the fog outside is laid over the doorway (<c>ientrance part outfog</c>). The
        /// doorway is transparent, and in deferred rendering the fog is a pass drawn before the
        /// transparent queue, so without this the view stayed crisp and dark in a fog that washes
        /// out everything else at the same distance - a mountain's blue haze most of all.
        /// </summary>
        internal static bool OutdoorFog = true;

        /// <summary>Whether the portal render gets the game's ambient occlusion (<c>ientrance part ao</c>).</summary>
        internal static bool AmbientOcclusion = true;

        /// <summary>Whether the dungeon reflects itself rather than black (<c>ientrance part refl</c>).</summary>
        internal static bool Reflections = true;

        /// <summary>
        /// Corrections measured by hand in game with <see cref="FloorMode.Probes"/>, per location,
        /// in the exit door's frame (x right, y up, z into the dungeon). The burial chambers
        /// share one entrance. Only applied with the probes. (The 0.1 the crypts once carried in z
        /// is now measured from the boxes, see <see cref="BoxDepth"/>.)
        /// </summary>
        private static readonly Dictionary<string, Vector3> Offsets = new Dictionary<string, Vector3>
        {
            { "Crypt2", new Vector3(0f, 0.16f, 0f) },
            { "Crypt3", new Vector3(0f, 0.16f, 0f) },
            { "Crypt4", new Vector3(0f, 0.16f, 0f) },
        };

        // ---- One portal ------------------------------------------------------------------------

        internal readonly Teleport Entrance;
        internal readonly Teleport Exit;
        internal readonly Surface Outside;
        internal readonly Surface Inside;
        internal readonly string Environment;
        /// <summary>The interior environment; null when the game has none by that name.</summary>
        internal readonly EnvSetup Setup;
        internal readonly string LocationName;
        internal readonly Vector3 BakedOffset;

        /// <summary>The game's black box in the entrance, whose front face the doorway covers; null when not found.</summary>
        internal readonly MeshRenderer Black;

        /// <summary>The floor shift each method measured; null where it could not.</summary>
        internal readonly float? BoxShift;
        internal readonly float? ProbeShift;
        /// <summary>
        /// The depth shift: how much further into the dungeon, past where the entrance frame
        /// lands, the white box's visible face is than the black box's. The visible faces are what
        /// the designers placed exactly, so this holds where the bottoms do not.
        /// </summary>
        internal readonly float? BoxDepth;
        /// <summary>Each box's bottom above its probed floor, for <see cref="FloorMode.Auto"/> and the status.</summary>
        internal readonly float? BlackAboveFloor;
        internal readonly float? WhiteAboveFloor;

        /// <summary>The doorway rectangle in the entrance frame: x right, y up, at <see cref="rectDepth"/> towards the player.</summary>
        private readonly Vector2 rectMin;
        private readonly Vector2 rectMax;
        private readonly float rectDepth;

        private readonly Location location;
        private readonly DungeonGenerator generator;
        private readonly List<GameObject> dust = new List<GameObject>();

        private readonly GameObject quadObject;
        private readonly MeshRenderer quad;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Material fogMaterial;
        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly Color[] colors;
        private bool flipped;
        /// <summary>The doorway in world space, measured once - a disabled renderer's bounds are not reliable.</summary>
        private readonly Bounds bounds;

        private readonly Camera camera;
        private readonly Camera reflectionCamera;
        private UnityEngine.PostProcessing.PostProcessingProfile fogProfile;
        private AmplifyOcclusionEffect occlusion;
        private AmplifyOcclusionEffect mainOcclusion;
        private RenderTexture texture;
        private RenderTexture reflection;
        private bool reflectionDirty = true;

        internal readonly InteriorLook.Lights Lights = new InteriorLook.Lights();
        internal readonly InteriorGroup Group = new InteriorGroup();
        internal readonly ExitGlow Glow;
        private float nextLightScan;

        /// <summary>Whether the last frame actually drew through this door.</summary>
        internal bool Drawn { get; private set; }

        /// <summary>The last frame's eye distance in front of the door plane, for the status.</summary>
        internal float Depth { get; private set; }

        internal bool Valid => Entrance != null && Exit != null && quadObject != null && camera != null;

        /// <summary>The doorway's width and height.</summary>
        internal Vector2 RectSize => rectMax - rectMin;

        /// <summary>
        /// Whether the dungeon's rooms exist yet. The generator loads room prefabs asynchronously
        /// and clears its list once they are placed; until then the view would be a void.
        /// </summary>
        internal bool Ready => generator == null || generator.m_loadedRooms == null || generator.m_loadedRooms.Length == 0;

        /// <summary>Whether both box bottoms sit on their floors closely enough for <see cref="FloorMode.Auto"/>.</summary>
        internal bool BoxesTrusted =>
            BoxShift.HasValue && BlackAboveFloor.HasValue && WhiteAboveFloor.HasValue &&
            Mathf.Abs(BlackAboveFloor.Value) <= BoxTolerance && Mathf.Abs(WhiteAboveFloor.Value) <= BoxTolerance;

        /// <summary>The floor mode actually applied, with <see cref="FloorMode.Auto"/> resolved.</summary>
        internal FloorMode Mode
        {
            get
            {
                if (Floor != FloorMode.Auto)
                {
                    return Floor;
                }
                return BoxesTrusted || !ProbeShift.HasValue ? FloorMode.Boxes : FloorMode.Probes;
            }
        }

        /// <summary>The shift in use, in the exit frame: right, up, into the dungeon.</summary>
        internal Vector3 Shift
        {
            get
            {
                switch (Mode)
                {
                    case FloorMode.Boxes: return new Vector3(0f, BoxShift ?? ProbeShift ?? 0f, BoxDepth ?? 0f);
                    case FloorMode.Probes: return new Vector3(0f, ProbeShift ?? 0f, BoxDepth ?? 0f) + BakedOffset;
                    default: return Vector3.zero;
                }
            }
        }

        internal static Portal Create(Teleport entrance)
        {
            if (!ImmersiveEntrancePlugin.IsEntrance(entrance) || FindShader() == null)
            {
                return null;
            }
            return new Portal(entrance);
        }

        private Portal(Teleport entrance)
        {
            Entrance = entrance;
            Exit = entrance.m_targetPoint;
            Outside = DoorSurfaces.Find(Entrance);
            Inside = DoorSurfaces.Find(Exit);
            Black = AutoDetect ? DoorSurfaces.FindBlack(Entrance) : null;
            location = entrance.GetComponentInParent<Location>();
            generator = location != null ? location.GetComponentInChildren<DungeonGenerator>(true) : null;
            Environment = location != null ? location.m_interiorEnvironment : "";
            Setup = EnvMan.instance != null && !string.IsNullOrEmpty(Environment) ? EnvMan.instance.GetEnv(Environment) : null;
            LocationName = location != null ? Utils.GetPrefabName(location.gameObject) : "none";
            Offsets.TryGetValue(LocationName, out BakedOffset);
            if (Setup == null)
            {
                ImmersiveEntrancePlugin.Log.LogWarning(
                    $"Portal at {Entrance.transform.position}: interior environment '{Environment}' not found; the view keeps the weather outside.");
            }

            // The doorway: the front face of the game's black box, a hair in front of it, or
            // failing that a ManualSize rectangle on the entrance frame. The box's own shape fills
            // a cave mouth as well as a crypt door; its other faces sit inside the walls.
            if (Black != null && DoorSurfaces.Box(Black, Outside.Center, Outside.Rotation, out Vector3 min, out Vector3 max))
            {
                rectMin = new Vector2(min.x, min.y);
                rectMax = new Vector2(max.x, max.y);
                rectDepth = max.z + Lift;
            }
            else
            {
                Black = null;
                rectMin = -0.5f * ManualSize;
                rectMax = 0.5f * ManualSize;
                rectDepth = Lift;
            }

            Glow = new ExitGlow(Exit);
            Group.Gather(location);

            // The floor shift, both ways, so the dev commands can compare them, and the depth.
            float? blackBottom = DoorSurfaces.Bottom(Black);
            float? whiteBottom = DoorSurfaces.Bottom(Glow.White);
            bool outsideKnown = Threshold(Outside, out float outsideFloor);
            bool insideKnown = Threshold(Inside, out float insideFloor);
            if (blackBottom.HasValue && whiteBottom.HasValue)
            {
                BoxShift = (whiteBottom.Value - Inside.Center.y) - (blackBottom.Value - Outside.Center.y);
            }
            if (outsideKnown && insideKnown)
            {
                // Inside, the camera should stand as high above the floor as it does outside.
                ProbeShift = (insideFloor - Inside.Center.y) - (outsideFloor - Outside.Center.y);
            }
            if (blackBottom.HasValue && outsideKnown)
            {
                BlackAboveFloor = blackBottom.Value - outsideFloor;
            }
            if (whiteBottom.HasValue && insideKnown)
            {
                WhiteAboveFloor = whiteBottom.Value - insideFloor;
            }
            // The rectangle lands at exit-frame z = -(rectDepth - Lift) when turned about; the
            // white box's face towards the dungeon (its far face, the exit's +z points inward) is
            // where the dungeon actually starts.
            if (DoorSurfaces.Box(Glow.White, Inside.Center, Inside.Rotation, out _, out Vector3 whiteMax))
            {
                BoxDepth = whiteMax.z + (rectDepth - Lift);
            }

            // The quad: a small grid over the rectangle. u runs the way the player sees the door,
            // whose left edge is the frame's +x (the player faces -z), so u = 0 is at rectMax.x.
            var points = new List<Vector3>();
            var triangles = new List<int>();
            Vector2 size = RectSize;
            for (int y = 0; y <= Grid; y++)
            {
                for (int x = 0; x <= Grid; x++)
                {
                    points.Add(new Vector3(rectMax.x - size.x * x / Grid, rectMin.y + size.y * y / Grid, rectDepth));
                }
            }
            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    int i00 = y * (Grid + 1) + x, i10 = i00 + 1, i01 = i00 + Grid + 1, i11 = i01 + 1;
                    triangles.Add(i00); triangles.Add(i10); triangles.Add(i01);
                    triangles.Add(i10); triangles.Add(i11); triangles.Add(i01);
                }
            }
            vertices = points.ToArray();
            int count = vertices.Length;
            // Submesh 0 shows the view, submesh 1 is the same quad again, drawn over it in the
            // outdoor fog's colour with each vertex's fog as its alpha.
            var doubled = new Vector3[count * 2];
            vertices.CopyTo(doubled, 0);
            vertices.CopyTo(doubled, count);
            var fogTriangles = new int[triangles.Count];
            for (int i = 0; i < fogTriangles.Length; i++)
            {
                fogTriangles[i] = triangles[i] + count;
            }
            uvs = new Vector2[count * 2];
            colors = new Color[count * 2];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = i < count ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
            mesh = new Mesh { name = "ImmersiveEntrance Doorway" };
            mesh.MarkDynamic();
            mesh.vertices = doubled;
            mesh.colors = colors;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(triangles, 0);
            mesh.SetTriangles(fogTriangles, 1);
            WriteUvs();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            material = new Material(shader) { name = "ImmersiveEntrance Doorway" };
            // Drawn after the black box, which stays in place underneath.
            if (Black != null && Black.sharedMaterial != null)
            {
                material.renderQueue = Mathf.Max(material.renderQueue, Black.sharedMaterial.renderQueue + 1);
            }
            // No texture: the shader's default white, tinted by _Color. After the view, always.
            fogMaterial = new Material(shader) { name = "ImmersiveEntrance Doorway Fog", renderQueue = material.renderQueue + 1 };

            quadObject = new GameObject("ImmersiveEntrance Doorway");
            quadObject.layer = Black != null ? Black.gameObject.layer : Entrance.gameObject.layer;
            quadObject.transform.SetPositionAndRotation(Outside.Center, Outside.Rotation);
            if (location != null)
            {
                // Dies with the location, so an unloaded zone never leaves a doorway behind.
                quadObject.transform.SetParent(location.transform, true);
            }
            quadObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            quad = quadObject.AddComponent<MeshRenderer>();
            quad.sharedMaterials = new[] { material, fogMaterial };
            quad.shadowCastingMode = ShadowCastingMode.Off;
            quad.receiveShadows = false;
            quad.enabled = false;
            bounds = new Bounds(Outside.ToWorld(vertices[0]), Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                bounds.Encapsulate(Outside.ToWorld(vertex));
            }

            Camera main = Utils.GetMainCamera();
            camera = MakeCamera("ImmersiveEntrance Camera", main);
            AddFog(camera.gameObject, main);
            AddOcclusion(camera.gameObject, main);
            reflectionCamera = MakeCamera("ImmersiveEntrance Reflection", main);
            reflection = new RenderTexture(ReflectionSize, ReflectionSize, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "ImmersiveEntrance Reflection",
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = true,
            };
            reflection.Create();
            AddDust();

            ImmersiveEntrancePlugin.Log.LogInfo(
                $"Portal at {Entrance.transform.position}: doorway {(Black != null ? DoorSurfaces.PathOf(Black.transform) : "manual")} {size.x:0.00}x{size.y:0.00}, " +
                $"exit glow {Glow.Found}, interior-only renderers {Group.Count}, location {LocationName}, environment '{Environment}', " +
                $"floor shift {Measured}, ao {(occlusion != null ? "yes" : "no")}, dust {dust.Count}, rooms {(Ready ? "ready" : "loading")}");
        }

        /// <summary>Every measurement behind the shift and what was chosen, for the log and the status.</summary>
        internal string Measured =>
            $"boxes {Fmt(BoxShift)} (black bottom {Fmt(BlackAboveFloor)}, white bottom {Fmt(WhiteAboveFloor)} above the floor) " +
            $"probes {Fmt(ProbeShift)} depth {Fmt(BoxDepth)}, {Floor} -> {Mode}, applied {Shift}";

        internal static string Fmt(float? value)
        {
            return value.HasValue ? value.Value.ToString("0.00") : "-";
        }

        /// <summary>A disabled camera set up like the main one, rendered by hand.</summary>
        private static Camera MakeCamera(string name, Camera main)
        {
            var cameraObject = new GameObject(name);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            if (main != null)
            {
                camera.CopyFrom(main);
            }
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.targetTexture = null;
            return camera;
        }

        /// <summary>
        /// The main camera renders deferred, where Unity's fog is not drawn by the materials but by
        /// a pass of the post-processing stack (v1) after the opaque geometry - without one of its
        /// own the portal camera drew no fog whatever <c>RenderSettings</c> said. So it gets a
        /// <c>PostProcessingBehaviour</c> with everything but the fog switched on. The fog shader
        /// rebuilds distance from the depth buffer; with the door as a regular near plane that
        /// distance starts at the door, which is where the dungeon's fog should start.
        /// </summary>
        private void AddFog(GameObject cameraObject, Camera main)
        {
            var mainStack = main != null ? main.GetComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>() : null;
            fogProfile = ScriptableObject.CreateInstance<UnityEngine.PostProcessing.PostProcessingProfile>();
            fogProfile.name = "ImmersiveEntrance Fog";
            fogProfile.fog.enabled = true;
            if (mainStack != null && mainStack.profile != null)
            {
                fogProfile.fog.settings = mainStack.profile.fog.settings;
            }
            var stack = cameraObject.AddComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>();
            stack.profile = fogProfile;
        }

        /// <summary>
        /// The game's ambient occlusion is the Amplify effect on the main camera, and every
        /// environment tints and scales it - crypts dark. A copy with the main camera's settings
        /// goes on the portal camera; the interior's tint and intensity are set per render.
        /// </summary>
        private void AddOcclusion(GameObject cameraObject, Camera main)
        {
            mainOcclusion = main != null ? main.GetComponent<AmplifyOcclusionEffect>() : null;
            if (mainOcclusion == null)
            {
                return;
            }
            occlusion = cameraObject.AddComponent<AmplifyOcclusionEffect>();
            foreach (FieldInfo field in typeof(AmplifyOcclusionEffect).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                field.SetValue(occlusion, field.GetValue(mainOcclusion));
            }
            occlusion.enabled = mainOcclusion.enabled;
        }

        /// <summary>
        /// Inside, the game switches on the environment's particle systems - dust for a crypt - on
        /// an object that follows the player, so the dungeon itself has none where the portal
        /// camera looks. A copy of each stands a few metres in from the exit door, emitting all
        /// the time, for as long as the portal lives, and starts a few seconds into its life so
        /// it is never seen filling up.
        /// </summary>
        private void AddDust()
        {
            if (Setup == null || Setup.m_psystems == null)
            {
                return;
            }
            Vector3 at = Inside.ToWorld(new Vector3(0f, 0f, DustDepth));
            foreach (GameObject source in Setup.m_psystems)
            {
                if (source == null)
                {
                    continue;
                }
                GameObject copy = Object.Instantiate(source, at, source.transform.rotation);
                copy.name = "ImmersiveEntrance Dust";
                foreach (FollowPlayer follow in copy.GetComponentsInChildren<FollowPlayer>(true))
                {
                    Object.Destroy(follow);
                }
                copy.SetActive(true);
                foreach (ParticleSystem system in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.EmissionModule emission = system.emission;
                    emission.enabled = true;
                    system.Simulate(DustPrewarm, false, true);
                    system.Play(false);
                }
                dust.Add(copy);
            }
        }

        /// <summary>The highest floor just in front of a door, on the side its forward points to.</summary>
        private static bool Threshold(Surface surface, out float height)
        {
            height = float.MinValue;
            foreach (float distance in FloorProbes)
            {
                if (FloorBelow(surface.Center + surface.Forward * distance, out float floor))
                {
                    height = Mathf.Max(height, floor);
                }
            }
            return height > float.MinValue;
        }

        /// <summary>The floor at each probe, relative to the doorway's centre, for the dump.</summary>
        internal static string FloorProfile(Surface surface)
        {
            var parts = new List<string>();
            foreach (float distance in FloorProbes)
            {
                parts.Add(FloorBelow(surface.Center + surface.Forward * distance, out float floor)
                    ? $"{distance:0.00}m: {floor - surface.Center.y:0.00}"
                    : $"{distance:0.00}m: -");
            }
            return string.Join(", ", parts);
        }

        /// <summary>The height of the first solid surface below a point, ignoring triggers.</summary>
        internal static bool FloorBelow(Vector3 from, out float height)
        {
            height = 0f;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, FloorProbeDepth, FloorMask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            return false;
        }

        /// <summary>The rooms of this dungeon were just placed: torches and interior-only pieces are new, and so is the reflection.</summary>
        internal void OnRoomsSpawned(DungeonGenerator spawned)
        {
            if (spawned != generator)
            {
                return;
            }
            nextLightScan = 0f;
            reflectionDirty = true;
            Group.Gather(location);
        }

        /// <summary>Takes the dungeon's reflection again on the next render (<c>ientrance refl</c>).</summary>
        internal void RefreshReflection()
        {
            reflectionDirty = true;
        }

        /// <summary>Draws the interior into the texture from where the player's eye would be behind the door.</summary>
        internal void Render(Camera main, Plane[] frustum)
        {
            Drawn = false;
            if (!Valid)
            {
                return;
            }
            quad.enabled = false;
            if (!Ready)
            {
                return;
            }

            // The eye in the entrance frame; in front of the door plane there is a view.
            Vector3 local = Outside.ToLocal(main.transform.position);
            Depth = local.z - rectDepth;
            if (Depth < MinDepth || !GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                return;
            }
            UpdateFogLayer(main);
            EnsureTexture();

            // The eye in the exit frame: turned about, then shifted so the floors line up, plus
            // the tuning offsets. The shift is a translation of the whole mapping, so the doorway
            // rectangle moves with the eye: the frustum keeps the shape it has from the unshifted
            // eye and only the camera moves. (Moving the eye alone against a fixed rectangle tilts
            // every ray instead, and a raised eye then lifts the dungeon in the door.)
            Vector3 eye = Turn * local;
            Vector3 shifted = eye + Shift + CameraOffset;
            camera.transform.SetPositionAndRotation(Inside.ToWorld(shifted), Inside.Rotation);
            camera.cullingMask = main.cullingMask;
            camera.renderingPath = main.renderingPath;
            camera.allowHDR = main.allowHDR;

            // The frustum through the rectangle. The camera looks along the exit frame's +z, so a
            // point at exit-frame p is at p - eye in camera space; the rectangle's x is mirrored
            // by the turn. Its plane is Depth away, the near plane at least ClipOffset past the
            // exit trigger (measured from where the camera actually stands), and the rectangle
            // scales to whichever is used.
            float near = Mathf.Max(Depth, ClipOffset - shifted.z);
            float far = main.farClipPlane;
            float k = near / Depth;
            float left = (-rectMax.x - eye.x) * k;
            float right = (-rectMin.x - eye.x) * k;
            float bottom = (rectMin.y - eye.y) * k;
            float top = (rectMax.y - eye.y) * k;
            camera.nearClipPlane = near;
            camera.farClipPlane = far;
            camera.projectionMatrix = Matrix4x4.Frustum(left, right, bottom, top, near, far);

            if (Time.time >= nextLightScan)
            {
                nextLightScan = Time.time + LightScanInterval;
                Lights.Gather(Inside.Center);
            }

            InteriorLook.Blend blend = InteriorLook.Compute(Setup);
            if (occlusion != null)
            {
                occlusion.enabled = AmbientOcclusion && mainOcclusion != null && mainOcclusion.enabled;
                occlusion.Tint = blend.AoColor;
                occlusion.FadeToTint = blend.AoColor;
                occlusion.Intensity = blend.AoIntensity;
            }
            InteriorLook.Apply(blend, null, out InteriorLook.Saved look);
            if (!look.Valid)
            {
                return;
            }
            bool lit = ImmersiveEntrancePlugin.InteriorLights.Value;
            if (lit)
            {
                Lights.On();
            }
            Glow.Off();
            Group.On();
            try
            {
                if (Reflections && reflection != null && reflectionCamera != null)
                {
                    if (reflectionDirty)
                    {
                        // Taken under the interior look, with the sky still black, so it holds
                        // the dungeon alone.
                        reflectionDirty = false;
                        reflectionCamera.transform.position = Inside.ToWorld(new Vector3(0f, 0f, ReflectionDepth));
                        reflectionCamera.cullingMask = main.cullingMask;
                        reflectionCamera.RenderToCubemap(reflection);
                    }
                    InteriorLook.UseReflection(reflection, look.ReflectionIntensity);
                }
                camera.Render();
            }
            finally
            {
                Group.Restore();
                Glow.Restore();
                if (lit)
                {
                    Lights.Restore();
                }
                InteriorLook.Restore(look);
            }

            quad.enabled = true;
            Drawn = true;
        }

        /// <summary>Unit UVs, the way the player sees the door; the fog copy shares them.</summary>
        private void WriteUvs()
        {
            int count = vertices.Length;
            for (int y = 0; y <= Grid; y++)
            {
                for (int x = 0; x <= Grid; x++)
                {
                    int i = y * (Grid + 1) + x;
                    float v = (float)y / Grid;
                    uvs[i] = uvs[i + count] = new Vector2((float)x / Grid, FlipV ? 1f - v : v);
                }
            }
            mesh.uv = uvs;
            flipped = FlipV;
        }

        /// <summary>
        /// The outdoor fog over the doorway: the fog colour with, at each vertex, the alpha Unity's
        /// own formula gives for that vertex's depth under the main camera - the depth the fog
        /// pass measures.
        /// </summary>
        private void UpdateFogLayer(Camera main)
        {
            if (flipped != FlipV)
            {
                WriteUvs();
            }
            int count = vertices.Length;
            bool fog = OutdoorFog && RenderSettings.fog;
            for (int i = 0; i < count; i++)
            {
                Vector3 viewport = main.WorldToViewportPoint(Outside.ToWorld(vertices[i]));
                colors[i + count].a = fog ? 1f - Clear(viewport.z - main.nearClipPlane) : 0f;
            }
            mesh.colors = colors;
            Color tint = RenderSettings.fogColor;
            tint.a = 1f;
            fogMaterial.color = tint;
        }

        /// <summary>
        /// How much of a point <paramref name="depth"/> away shows through the current fog - Unity's
        /// own fog formulas, which the fog pass uses with <c>RenderSettings</c> as they stand.
        /// </summary>
        private static float Clear(float depth)
        {
            depth = Mathf.Max(0f, depth);
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear:
                    float span = RenderSettings.fogEndDistance - RenderSettings.fogStartDistance;
                    return span > 0f ? Mathf.Clamp01((RenderSettings.fogEndDistance - depth) / span) : 1f;
                case FogMode.Exponential:
                    return Mathf.Exp(-RenderSettings.fogDensity * depth);
                default:
                    float d = RenderSettings.fogDensity * depth;
                    return Mathf.Exp(-d * d);
            }
        }

        /// <summary>The view texture, shaped like the door, its longer side <c>TextureSize</c>.</summary>
        private void EnsureTexture()
        {
            int size = ImmersiveEntrancePlugin.TextureSize.Value;
            Vector2 rect = RectSize;
            int width = size, height = size;
            if (rect.x > rect.y && rect.x > 0f)
            {
                height = Mathf.Max(16, Mathf.RoundToInt(size * rect.y / rect.x));
            }
            else if (rect.y > 0f)
            {
                width = Mathf.Max(16, Mathf.RoundToInt(size * rect.x / rect.y));
            }
            if (texture != null && texture.width == width && texture.height == height)
            {
                return;
            }
            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, Format())
            {
                name = "ImmersiveEntrance View",
            };
            texture.Create();
            camera.targetTexture = texture;
            material.mainTexture = texture;
        }

        /// <summary>
        /// A format without alpha where the GPU has one. The fallback doorway shaders multiply the
        /// colour by alpha, and Valheim's shaders leave their own values in the alpha channel -
        /// different per material - so with an alpha channel the stones came out darkened and
        /// off-colour. Without one, alpha always reads 1.
        /// </summary>
        private static RenderTextureFormat Format()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float))
            {
                return RenderTextureFormat.RGB111110Float;
            }
            return RenderTextureFormat.DefaultHDR;
        }

        /// <summary>
        /// An unlit shader that shows a texture as it is. None of these is guaranteed to be in the
        /// build, so they are tried in turn; the dev command <c>ientrance shaders</c> lists what is.
        /// </summary>
        internal static Shader FindShader()
        {
            if (shader != null)
            {
                return shader;
            }
            string[] names = { "Unlit/Texture", "Sprites/Default", "UI/Default" };
            foreach (string name in names)
            {
                shader = Shader.Find(name);
                if (shader != null)
                {
                    break;
                }
            }
            if (shader == null)
            {
                foreach (Shader loaded in Resources.FindObjectsOfTypeAll<Shader>())
                {
                    if (System.Array.IndexOf(names, loaded.name) >= 0)
                    {
                        shader = loaded;
                        break;
                    }
                }
            }
            if (shader == null)
            {
                ImmersiveEntrancePlugin.Log.LogWarning("No unlit texture shader found; entrances stay black.");
            }
            else
            {
                ImmersiveEntrancePlugin.Log.LogInfo($"Doorway shader: {shader.name}");
            }
            return shader;
        }

        private void ReleaseTexture()
        {
            if (texture == null)
            {
                return;
            }
            if (camera != null)
            {
                camera.targetTexture = null;
            }
            texture.Release();
            Object.Destroy(texture);
            texture = null;
        }

        internal void Dispose()
        {
            ReleaseTexture();
            if (reflection != null)
            {
                reflection.Release();
                Object.Destroy(reflection);
                reflection = null;
            }
            foreach (GameObject copy in dust)
            {
                if (copy != null)
                {
                    Object.Destroy(copy);
                }
            }
            if (camera != null)
            {
                Object.Destroy(camera.gameObject);
            }
            if (reflectionCamera != null)
            {
                Object.Destroy(reflectionCamera.gameObject);
            }
            if (quadObject != null)
            {
                Object.Destroy(quadObject);
            }
            Object.Destroy(mesh);
            Object.Destroy(material);
            Object.Destroy(fogMaterial);
            Object.Destroy(fogProfile);
        }
    }
}
