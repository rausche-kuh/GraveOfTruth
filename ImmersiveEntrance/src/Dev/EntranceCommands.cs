using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ImmersiveEntrance
{
    // Dev only: src/Dev/ is compiled into Debug builds alone, so this never ships. Nothing else
    // refers to it - delete the file to drop the commands.
    public partial class ImmersiveEntrancePlugin
    {
        private const string Usage =
            "ientrance dump | shaders | status | light | part fog|outfog|exit|ambient|sun|refl|ao|wet|torches | floor | tol <metres> | refl | toggle | flip | offset <x> <y> <z> | clip <metres> | size <w> <h> | at <x> <y> <z> | auto";

        /// <summary>
        /// "ientrance ..." - calibration and inspection for the portal. Everything it prints also
        /// goes to the BepInEx log, which is where the dump is meant to be read. Needs devcommands.
        /// </summary>
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        public static class Commands
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("ientrance", Usage, args =>
                {
                    string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    switch (sub)
                    {
                        case "dump": Say(args.Context, Dump()); break;
                        case "shaders": Say(args.Context, Shaders()); break;
                        case "status": Say(args.Context, Status()); break;
                        case "light": Say(args.Context, Lighting()); break;
                        case "floor":
                            Portal.Floor = (Portal.FloorMode)(((int)Portal.Floor + 1) % 4);
                            Say(args.Context, "Floor shift from: " + Portal.Floor + " (the portals are rebuilt)");
                            ClearPortals();
                            break;
                        case "tol":
                            Portal.BoxTolerance = args.TryParameterFloat(2, 0.25f);
                            Say(args.Context, "BoxTolerance: " + Portal.BoxTolerance + " (the portals are rebuilt)");
                            ClearPortals();
                            break;
                        case "refl":
                            foreach (Portal portal in Portals)
                            {
                                portal.RefreshReflection();
                            }
                            Say(args.Context, "Reflections taken again");
                            break;
                        case "part":
                            Say(args.Context, TogglePart(args.Length > 2 ? args[2].ToLowerInvariant() : ""));
                            break;
                        case "toggle":
                            Enabled.Value = !Enabled.Value;
                            Say(args.Context, "Enabled: " + Enabled.Value);
                            break;
                        case "flip":
                            Portal.FlipV = !Portal.FlipV;
                            Say(args.Context, "Flipped: " + Portal.FlipV);
                            break;
                        case "offset":
                            Portal.CameraOffset = new Vector3(args.TryParameterFloat(2, 0f), args.TryParameterFloat(3, 0f), args.TryParameterFloat(4, 0f));
                            Say(args.Context, "CameraOffset: " + Portal.CameraOffset);
                            break;
                        case "clip":
                            Portal.ClipOffset = args.TryParameterFloat(2, 0.05f);
                            Say(args.Context, "ClipOffset: " + Portal.ClipOffset);
                            break;
                        case "size":
                            Portal.AutoDetect = false;
                            Portal.ManualSize = new Vector2(args.TryParameterFloat(2, 2.5f), args.TryParameterFloat(3, 3f));
                            Say(args.Context, "AutoDetect off, ManualSize: " + Portal.ManualSize + " (the portals are rebuilt)");
                            ClearPortals();
                            break;
                        case "at":
                            Portal.AutoDetect = false;
                            Portal.ManualOffset = new Vector3(args.TryParameterFloat(2, 0f), args.TryParameterFloat(3, 0.5f), args.TryParameterFloat(4, 0f));
                            Say(args.Context, "AutoDetect off, ManualOffset: " + Portal.ManualOffset + " (the portals are rebuilt)");
                            ClearPortals();
                            break;
                        case "auto":
                            Portal.AutoDetect = true;
                            Say(args.Context, "AutoDetect on (the portals are rebuilt)");
                            ClearPortals();
                            break;
                        default: Say(args.Context, Usage); break;
                    }
                }, isCheat: true);
            }
        }

        private static void Say(Terminal terminal, string text)
        {
            Log.LogInfo(text);
            if (terminal != null)
            {
                foreach (string line in text.Split('\n').Take(12))
                {
                    terminal.AddString(line);
                }
                if (text.Count(c => c == '\n') >= 12)
                {
                    terminal.AddString("... the rest is in BepInEx/LogOutput.log");
                }
            }
        }

        /// <summary>
        /// The nearest entrance and its exit: where each trigger is, every mesh around each
        /// trigger measured in its frame with its material and shader, which one the detection
        /// picked, the bottom of each box, the floor in front of each door, and every renderer
        /// the location keeps for the inside only. This is what the calibration gets corrected from.
        /// </summary>
        private static string Dump()
        {
            Camera main = Utils.GetMainCamera();
            if (main == null)
            {
                return "No camera.";
            }
            Teleport nearest = Teleports
                .Where(IsEntrance)
                .OrderBy(t => Vector3.Distance(t.transform.position, main.transform.position))
                .FirstOrDefault();
            if (nearest == null)
            {
                return "No dungeon entrance loaded.";
            }

            var text = new StringBuilder();
            Location location = nearest.GetComponentInParent<Location>();
            text.AppendLine($"== Entrance of {(location != null ? location.name : "?")}, environment '{(location != null ? location.m_interiorEnvironment : "")}'");
            text.AppendLine($"render pipeline: {(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "built-in")}, " +
                            $"main camera: path {main.actualRenderingPath}, hdr {main.allowHDR}, mask {main.cullingMask}, near {main.nearClipPlane}, far {main.farClipPlane}, " +
                            $"ao {(main.GetComponent<AmplifyOcclusionEffect>() != null ? (main.GetComponent<AmplifyOcclusionEffect>().enabled ? "on" : "off") : "none")}");
            DumpDoor(text, "entrance", nearest, dark: true);
            DumpDoor(text, "exit", nearest.m_targetPoint, dark: false);
            if (location != null)
            {
                var group = new InteriorGroup();
                group.Gather(location);
                text.AppendLine($"-- interior-only renderers (RenderGroup.Interior): {group.Count}" +
                                (group.Count > 0 ? "\n   " + string.Join("\n   ", group.Names) : ""));
                DungeonGenerator generator = location.GetComponentInChildren<DungeonGenerator>(true);
                text.AppendLine($"-- generator {(generator != null ? DoorSurfaces.PathOf(generator.transform) : "none")}, " +
                                $"rooms {location.GetComponentsInChildren<Room>(true).Length}, " +
                                $"still loading {(generator != null && generator.m_loadedRooms != null && generator.m_loadedRooms.Length > 0 ? generator.m_loadedRooms.Length.ToString() : "0")}");
            }
            return text.ToString();
        }

        private static void DumpDoor(StringBuilder text, string label, Teleport door, bool dark)
        {
            Transform t = door.transform;
            text.AppendLine($"-- {label}: {DoorSurfaces.PathOf(t)} pos {t.position} euler {t.eulerAngles} scale {t.lossyScale} " +
                            $"forward {t.forward} hover '{door.m_hoverText}'");
            foreach (Collider collider in door.GetComponents<Collider>())
            {
                text.AppendLine($"   collider {collider.GetType().Name} trigger {collider.isTrigger} bounds {collider.bounds}");
            }
            List<DoorSurfaces.Candidate> candidates = DoorSurfaces.Candidates(door);
            foreach (DoorSurfaces.Candidate c in candidates.OrderBy(c => c.Center.sqrMagnitude))
            {
                Material m = c.Renderer.sharedMaterial;
                text.AppendLine($"   {(c.Flat ? "FLAT" : "    ")} {(c.Dark ? "dark" : "    ")} {(c.Bright ? "bright" : "      ")} " +
                                $"centre {c.Center} size {c.Size} bottom {c.Center.y - 0.5f * c.Size.y:0.00} lum {c.Luminance:0.00} layer {LayerMask.LayerToName(c.Renderer.gameObject.layer)} " +
                                $"{DoorSurfaces.PathOf(c.Renderer.transform)} mat '{(m != null ? m.name : "-")}' shader '{(m != null && m.shader != null ? m.shader.name : "-")}' " +
                                $"tex '{(m != null && m.mainTexture != null ? m.mainTexture.name : "-")}'");
            }
            Surface picked = DoorSurfaces.Find(door);
            MeshRenderer box = dark ? DoorSurfaces.FindBlack(door) : DoorSurfaces.FindWhite(door);
            text.AppendLine($"   frame: {picked}, {(dark ? "black" : "white")} box: " +
                            $"{(box != null ? $"{DoorSurfaces.PathOf(box.transform)} bottom {Portal.Fmt(DoorSurfaces.Bottom(box))} (world), {Portal.Fmt(DoorSurfaces.Bottom(box) - picked.Center.y)} below the frame" : "none")}");
            text.AppendLine($"   floor below the doorway centre, by distance in front: {Portal.FloorProfile(picked)}");
        }

        private static string Shaders()
        {
            var names = Resources.FindObjectsOfTypeAll<Shader>()
                .Select(s => s.name)
                .Where(n => n.StartsWith("Unlit") || n.StartsWith("Sprites") || n.StartsWith("UI") ||
                            n.StartsWith("Legacy") || n.StartsWith("Particles") || n.StartsWith("Custom"))
                .Distinct()
                .OrderBy(n => n);
            Shader chosen = Portal.FindShader();
            return $"doorway shader: {(chosen != null ? chosen.name : "none")}\n" + string.Join("\n", names);
        }

        /// <summary>Switches one part of the interior look, to find which one looks wrong.</summary>
        private static string TogglePart(string part)
        {
            switch (part)
            {
                case "fog": InteriorLook.DungeonFog = !InteriorLook.DungeonFog; break;
                case "exit": ExitGlow.Hide = !ExitGlow.Hide; break;
                case "outfog": Portal.OutdoorFog = !Portal.OutdoorFog; break;
                case "ambient": InteriorLook.Ambient = !InteriorLook.Ambient; break;
                case "sun": InteriorLook.Sun = !InteriorLook.Sun; break;
                case "refl": Portal.Reflections = !Portal.Reflections; break;
                case "ao": Portal.AmbientOcclusion = !Portal.AmbientOcclusion; break;
                case "wet": InteriorLook.Dry = !InteriorLook.Dry; break;
                case "torches": InteriorLights.Value = !InteriorLights.Value; break;
                case "": break;
                default: return "ientrance part fog|outfog|exit|ambient|sun|refl|ao|wet|torches";
            }
            return $"dungeon fog {InteriorLook.DungeonFog}, outdoor fog over the doorway {Portal.OutdoorFog}, exit glow hidden {ExitGlow.Hide}, " +
                   $"ambient {InteriorLook.Ambient}, sun {InteriorLook.Sun}, dungeon reflection {Portal.Reflections}, ao {Portal.AmbientOcclusion}, " +
                   $"dry {InteriorLook.Dry}, torches {InteriorLights.Value}";
        }

        /// <summary>What the last portal render lit the dungeon with, and what the environment holds.</summary>
        private static string Lighting()
        {
            var text = new StringBuilder();
            text.AppendLine($"InteriorLights {InteriorLights.Value}, TextureSize {TextureSize.Value}");
            text.AppendLine("last look: " + InteriorLook.Last);
            text.AppendLine($"outdoor now: fog {(RenderSettings.fog ? $"{RenderSettings.fogColor} density {RenderSettings.fogDensity:0.000} ({RenderSettings.fogMode})" : "off")}, " +
                            $"ambient {RenderSettings.ambientMode} {RenderSettings.ambientLight}, laid over the doorway {Portal.OutdoorFog}");
            text.AppendLine($"doorway shader {(Portal.FindShader() != null ? Portal.FindShader().name : "none")}, " +
                            $"view format {(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? "RGB111110Float (no alpha)" : "with alpha")}");
            Light sun = EnvMan.instance != null ? EnvMan.instance.m_dirLight : null;
            if (sun != null)
            {
                text.AppendLine($"sun now: {sun.color} x {sun.intensity:0.00}, {sun.renderMode}, shadows {sun.shadows}, day fraction {EnvMan.instance.GetDayFraction():0.000}");
            }
            text.AppendLine($"point light limit {LightLod.m_lightLimit}, shadow limit {LightLod.m_shadowLimit}");
            foreach (Portal portal in Portals)
            {
                EnvSetup setup = portal.Setup;
                if (setup != null)
                {
                    text.AppendLine($"env '{setup.m_name}' night: ambient {setup.m_ambColorNight}, fog {setup.m_fogColorNight} density {setup.m_fogDensityNight:0.000}, " +
                                    $"sun {setup.m_sunColorNight} x {setup.m_lightIntensityNight:0.00}, ao x {setup.m_aoIntensityNight:0.00}, always dark {setup.m_alwaysDark}");
                    text.AppendLine($"env '{setup.m_name}' day: ambient {setup.m_ambColorDay}, fog {setup.m_fogColorDay} density {setup.m_fogDensityDay:0.000}, " +
                                    $"sun {setup.m_sunColorDay} x {setup.m_lightIntensityDay:0.00}, ao x {setup.m_aoIntensityDay:0.00}, ao colour {setup.m_ambientOcclusionColor}, sun angle {setup.m_sunAngle}");
                    text.AppendLine($"env object '{(setup.m_envObject != null ? DoorSurfaces.PathOf(setup.m_envObject.transform) : "-")}', " +
                                    $"particles [{(setup.m_psystems != null ? string.Join(", ", setup.m_psystems.Where(p => p != null).Select(p => DoorSurfaces.PathOf(p.transform))) : "")}], outside only {setup.m_psystemsOutsideOnly}");
                }
                text.AppendLine($"portal '{portal.Environment}': {portal.Lights.Count} torches near the exit, {portal.Lights.Lit} lit, {portal.Lights.Shadowed} with shadows; " +
                                $"{portal.Group.Count} interior-only renderers");
            }
            return text.ToString().TrimEnd();
        }

        private static string Status()
        {
            var text = new StringBuilder();
            text.AppendLine($"Enabled {Enabled.Value}, AutoDetect {Portal.AutoDetect}, ClipOffset {Portal.ClipOffset}, CameraOffset {Portal.CameraOffset}, floor from {Portal.Floor}, " +
                            $"BoxTolerance {Portal.BoxTolerance}, {Teleports.Count(IsEntrance)} entrances known");
            foreach (Portal portal in Portals)
            {
                text.AppendLine($"portal at {(portal.Entrance != null ? portal.Entrance.transform.position.ToString() : "gone")}, " +
                                $"location {portal.LocationName}, doorway {portal.RectSize.x:0.00}x{portal.RectSize.y:0.00} " +
                                $"{(portal.Black != null ? "on " + DoorSurfaces.PathOf(portal.Black.transform) : "manual")}, " +
                                $"exit glow {portal.Glow.Found}, rooms {(portal.Ready ? "ready" : "loading")}, drawn {portal.Drawn}, eye depth {portal.Depth:0.00}, " +
                                $"floor shift {portal.Measured}, baked {portal.BakedOffset}, " +
                                $"outside {portal.Outside}, inside {portal.Inside}");
            }
            return text.ToString().TrimEnd();
        }
    }
}
