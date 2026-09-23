using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace GraveOfTruth
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public partial class GraveOfTruthPlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.graveoftruth";
        public const string NAME = "GraveOfTruth";
        public const string VERSION = "0.1.2";

        /// <summary>Broadcast to every modded client: a grave to put the show on.</summary>
        private const string WailRpc = "GraveOfTruth_Wail";

        // Audio tuning.
        private const float MinDistance = 6f;
        private const float MaxDistance = 96f;
        // Several deaths at once, or a chatty client, still get one show every couple of seconds.
        private const float WailCooldown = 2f;
        // The bolt and its clap go first - started underneath them, the jingle is just noise.
        private const float StrikeLeadIn = 1.7f;
        private const float EchoDelay = 0.9f;
        private const float EchoVolume = 0.35f;
        private const float TailDelay = 2f;
        private const float TailVolume = 0.14f;
        // Out past StormRadius the jingle follows the thunder in, from this far off towards the
        // grave and this much quieter - a real grave would be out of earshot.
        private const float DistantOffset = 40f;
        private const float DistantVolume = 0.5f;

        // Weather tuning.
        private const string StormEnv = "ThunderStorm";
        private const float StormDuration = 9f;
        private const float StormFade = 2f;
        // Closer than this to the grave you get the bolt and the storm, further out distant thunder.
        private const float StormRadius = 150f;
        private const float StrikeAltitude = 12f;

        private static GraveOfTruthPlugin instance;
        private static ManualLogSource log;
        private static AudioClip audioClip;
        private static AudioMixerGroup mixer;
        private static GameObject boltPrefab;
        private static Thunder thunder;
        private static ZRoutedRpc registeredOn;
        private static float lastWail = -100f;
        private static float stormStart = -100f;

        void Awake()
        {
            instance = this;
            log = Logger;
            StartCoroutine(LoadClip());
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }

        /// <summary>ZNet builds a fresh ZRoutedRpc per session, so re-register once per game.</summary>
        [HarmonyPatch(typeof(Game), "Start")]
        public static class RegisterRpc
        {
            private static void Postfix()
            {
                ZRoutedRpc rpc = ZRoutedRpc.instance;
                if (rpc == null || rpc == registeredOn)
                {
                    return;
                }
                registeredOn = rpc;
                rpc.Register<Vector3, bool>(WailRpc, RPC_Wail);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
        public static class WailOnDeath
        {
            private static void Postfix(Player __instance)
            {
                // Never let this throw: OnDeath sits in the middle of the respawn machinery.
                try
                {
                    if (__instance != Player.m_localPlayer)
                    {
                        return;
                    }
                    // CreateTombStone instantiates the grave at exactly this point, so it is
                    // where the grave is even when an empty inventory means there isn't one.
                    Wail(__instance.GetCenterPoint(), strike: true);
                }
                catch (Exception e)
                {
                    log.LogWarning("death effects failed: " + e);
                }
            }
        }

        /// <summary>Opening your own grave to loot it wails once more.</summary>
        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Interact))]
        public static class WailOnLoot
        {
            private static void Postfix(TombStone __instance, Humanoid character, bool hold, bool __result)
            {
                try
                {
                    if (hold || !__result || character != Player.m_localPlayer || !__instance.IsOwner())
                    {
                        return;
                    }
                    Wail(__instance.transform.position, strike: false);
                }
                catch (Exception e)
                {
                    log.LogWarning("grave effects failed: " + e);
                }
            }
        }

        /// <summary>
        /// Draws the storm over whatever the weather really is. SetEnv only renders an environment
        /// - light, fog, clouds, rain, the ambient loop - while wet, cold, wind and spawns all
        /// read GetCurrentEnvironment(), which this never touches. So the storm is looks only, and it
        /// is dry: dark clouds and thunder, no rain of its own.
        /// </summary>
        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
        public static class StormSky
        {
            private static void Prefix(EnvMan __instance, ref EnvSetup env)
            {
                float t = Time.time - stormStart;
                if (t >= StormDuration || env == null)
                {
                    return;
                }
                // No sky in a crypt.
                Player player = Player.m_localPlayer;
                if (player != null && player.InInterior())
                {
                    return;
                }
                EnvSetup storm = __instance.GetEnv(StormEnv);
                if (storm == null)
                {
                    return;
                }
                float fade = Mathf.Clamp01(Mathf.Min(t, StormDuration - t) / StormFade);
                EnvSetup mix = __instance.InterpolateEnvironment(env, storm, fade);
                // A dry storm: the interpolation already keeps the real weather's rain and wet
                // shader, and the storm's ambient loop is its rain, so keep the real one too. Only
                // the env object is swapped over halfway - it carries the storm's own Thunder,
                // which flashes on the horizon.
                mix.m_ambientLoop = env.m_ambientLoop;
                mix.m_ambientVol = env.m_ambientVol;
                if (fade >= 0.5f)
                {
                    mix.m_envObject = storm.m_envObject;
                }
                env = mix;
            }
        }

        /// <summary>Tells everyone on the server to put on the show at the grave.</summary>
        private static void Wail(Vector3 pos, bool strike)
        {
            if (ZRoutedRpc.instance != null)
            {
                // Everybody includes us, so this also plays locally.
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, WailRpc, pos, strike);
            }
            else
            {
                RPC_Wail(0L, pos, strike);
            }
        }

        /// <summary>
        /// The show, run by every modded client for itself: up close the bolt, the storm and the
        /// jingle, further out a roll of thunder and a faint jingle from the grave's direction.
        /// Everything it spawns is local to this client and cosmetic, so nobody sees a thing twice
        /// or feels it at all.
        /// </summary>
        private static void RPC_Wail(long sender, Vector3 pos, bool strike)
        {
            // A dedicated server has nobody watching.
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                return;
            }
            if (Time.time - lastWail < WailCooldown)
            {
                return;
            }
            lastWail = Time.time;

            try
            {
                if (!strike)
                {
                    instance.StartCoroutine(WailRoutine(pos, 0f, 1f));
                    return;
                }
                // Decided once: whoever died is at the grave now, and keeps their storm after
                // respawning at home.
                Vector3 origin;
                if (TryGetOrigin(out origin) && Vector3.Distance(origin, pos) > StormRadius)
                {
                    instance.StartCoroutine(DistantThunder(origin, pos));
                    return;
                }
                if (!StrikeGrave(pos))
                {
                    SkyFlash(pos);
                }
                stormStart = Time.time;
                instance.StartCoroutine(WailRoutine(pos, StrikeLeadIn, 1f));
            }
            catch (Exception e)
            {
                log.LogWarning("wail failed: " + e);
            }
        }

        /// <summary>
        /// Waits out the thunder, then rings the jingle over the grave and lets two fading
        /// repeats chase it - a hillside echo, faked with nothing but more audio sources.
        /// </summary>
        private static IEnumerator WailRoutine(Vector3 pos, float leadIn, float volume)
        {
            if (leadIn > 0f)
            {
                yield return new WaitForSeconds(leadIn);
            }
            // The repeats come back a shade flatter, the way one would off a hillside.
            PlayAt(pos, volume, 1f);
            yield return new WaitForSeconds(EchoDelay);
            PlayAt(pos, volume * EchoVolume, 0.97f);
            yield return new WaitForSeconds(TailDelay - EchoDelay);
            PlayAt(pos, volume * TailVolume, 0.97f);
        }

        /// <summary>A throwaway 3D source at the grave, so the jingle carries across the world.</summary>
        private static void PlayAt(Vector3 pos, float volume, float pitch)
        {
            if (audioClip == null)
            {
                return;
            }
            GameObject go = new GameObject("GraveOfTruthWail");
            go.transform.position = pos + Vector3.up;

            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = audioClip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.dopplerLevel = 0f;
            source.volume = volume;
            source.pitch = pitch;
            source.outputAudioMixerGroup = SfxMixer();
            source.Play();

            Destroy(go, audioClip.length / source.pitch + 0.5f);
        }

        /// <summary>Route through the game's SFX bus so the volume sliders apply.</summary>
        private static AudioMixerGroup SfxMixer()
        {
            if (mixer != null)
            {
                return mixer;
            }
            AudioMan man = AudioMan.instance;
            if (man == null || man.m_masterMixer == null)
            {
                return null;
            }
            foreach (string name in new[] { "SFX", "Sfx", "sfx" })
            {
                AudioMixerGroup[] groups = man.m_masterMixer.FindMatchingGroups(name);
                if (groups != null && groups.Length > 0)
                {
                    mixer = groups[0];
                    return mixer;
                }
            }
            mixer = man.m_ambientMixer;
            return mixer;
        }

        /// <summary>
        /// Drops the obliterator's lightning straight onto the gravestone - one copy per client,
        /// belonging to nobody but that client. Every Aoe on it is defanged before it wakes up,
        /// so the bolt is pure show and hurts nobody.
        /// </summary>
        private static bool StrikeGrave(Vector3 pos)
        {
            GameObject prefab = FindBolt();
            if (prefab == null)
            {
                return false;
            }

            // Spawn it asleep: an Aoe with m_hitOnEnable would otherwise land its damage in
            // Instantiate, before we ever get to touch it. m_forceDisableInit is the game's own
            // way of saying "no ZDO for this one": the ZNetView destroys itself in Awake, so the
            // bolt never leaves this client - every client spawns its own off the same RPC
            // instead of also being shown someone else's.
            bool wasActive = prefab.activeSelf;
            bool wasDisabled = ZNetView.m_forceDisableInit;
            prefab.SetActive(false);
            ZNetView.m_forceDisableInit = true;
            try
            {
                GameObject bolt = Instantiate(prefab, pos, Quaternion.identity);

                foreach (Aoe aoe in bolt.GetComponentsInChildren<Aoe>(true))
                {
                    aoe.m_damage = new HitData.DamageTypes();
                    aoe.m_damagePerLevel = new HitData.DamageTypes();
                    aoe.m_damageSelf = 0f;
                    aoe.m_attackForce = 0f;
                    aoe.m_hitCharacters = false;
                    aoe.m_hitProps = false;
                    aoe.m_hitTerrain = false;
                    aoe.m_hitOwner = false;
                    aoe.m_hitParent = false;
                    aoe.m_launchCharacters = false;
                }

                // Awake runs here, so the flag has to still be set.
                bolt.SetActive(true);
            }
            finally
            {
                ZNetView.m_forceDisableInit = wasDisabled;
                prefab.SetActive(wasActive);
            }
            return true;
        }

        /// <summary>The obliterator's bolt, borrowed off the prefab rather than shipped.</summary>
        private static GameObject FindBolt()
        {
            if (boltPrefab != null)
            {
                return boltPrefab;
            }
            foreach (Incinerator inc in Resources.FindObjectsOfTypeAll<Incinerator>())
            {
                if (inc.m_lightingAOEs != null)
                {
                    boltPrefab = inc.m_lightingAOEs;
                    return boltPrefab;
                }
            }
            log.LogWarning("no obliterator lightning found, falling back to a sky flash");
            return null;
        }

        /// <summary>Fallback when the obliterator is nowhere to be found: flash low overhead, instant clap.</summary>
        private static void SkyFlash(Vector3 pos)
        {
            Thunder t = FindThunder();
            if (t == null)
            {
                return;
            }
            Flash(t, pos + Vector3.up * StrikeAltitude, Quaternion.LookRotation(Vector3.down));
            t.m_thunderEffect.Create(pos, Quaternion.identity);
        }

        /// <summary>
        /// Too far off for the storm: the bolt on the grave, a flash on the horizon towards it and
        /// the clap a few seconds later, the way vanilla Thunder does it - and the jingle, faintly, behind it.
        /// </summary>
        private static IEnumerator DistantThunder(Vector3 origin, Vector3 grave)
        {
            Thunder t = FindThunder();
            if (t == null)
            {
                yield break;
            }
            Vector3 toGrave = grave - origin;
            toGrave.y = 0f;
            toGrave.Normalize();
            Vector3 flashPos = origin + toGrave * t.m_flashDistanceMax + Vector3.up * t.m_flashAltitude;
            Flash(t, flashPos, Quaternion.LookRotation((origin - flashPos).normalized));
            // The bolt still comes down on the grave itself, for whoever can see that far.
            StrikeGrave(grave);

            yield return new WaitForSeconds(UnityEngine.Random.Range(t.m_thunderDelayMin, t.m_thunderDelayMax));
            t.m_thunderEffect.Create(flashPos, Quaternion.identity);
            yield return WailRoutine(origin + toGrave * DistantOffset, StrikeLeadIn, DistantVolume);
        }

        private static void Flash(Thunder t, Vector3 pos, Quaternion rotation)
        {
            foreach (GameObject flash in t.m_flashEffect.Create(pos, Quaternion.identity))
            {
                foreach (Light light in flash.GetComponentsInChildren<Light>())
                {
                    light.transform.rotation = rotation;
                }
            }
        }

        /// <summary>Thunder lives on the ThunderStorm's env object, inactive in fair weather.</summary>
        private static Thunder FindThunder()
        {
            if (thunder != null)
            {
                return thunder;
            }
            Thunder[] all = Resources.FindObjectsOfTypeAll<Thunder>();
            if (all.Length > 0)
            {
                thunder = all[0];
            }
            else
            {
                log.LogWarning("no Thunder component found, skipping lightning");
            }
            return thunder;
        }

        private static bool TryGetOrigin(out Vector3 pos)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                pos = player.transform.position;
                return true;
            }
            // Right after dying the player is gone but the death camera is still around.
            Camera camera = Utils.GetMainCamera();
            if (camera != null)
            {
                pos = camera.transform.position;
                return true;
            }
            pos = Vector3.zero;
            return false;
        }

        private static IEnumerator LoadClip()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sound.ogg");
            if (!File.Exists(path))
            {
                log.LogWarning(path + " does not exist");
                yield break;
            }
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    log.LogWarning("could not load " + path + ": " + www.error);
                    yield break;
                }
                audioClip = DownloadHandlerAudioClip.GetContent(www);
            }
        }
    }
}
