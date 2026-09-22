using BepInEx;
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
    public class GraveOfTruthAudio : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.graveoftruth";
        public const string NAME = "GraveOfTruth";
        public const string VERSION = "0.1.1";

        /// <summary>Broadcast to every modded client: the whole show here, bolt and storm included.</summary>
        private const string WailRpc = "GraveOfTruth_Wail";

        // Audio tuning.
        private const float MinDistance = 6f;
        private const float MaxDistance = 96f;
        private const float SendCooldown = 2f;
        // The bolt and its clap go first - started underneath them, the jingle is just noise.
        private const float StrikeLeadIn = 1.7f;
        private const float EchoDelay = 0.9f;
        private const float EchoVolume = 0.35f;
        private const float TailDelay = 2f;
        private const float TailVolume = 0.14f;

        // Weather tuning.
        private const string StormEnv = "ThunderStorm";
        private const float DeathStormDuration = 18f;
        private const float GraveWindDuration = 6f;
        private const float WindIntensity = 1f;
        private const float StrikeAltitude = 12f;

        private static GraveOfTruthAudio instance;
        private static AudioClip audioClip;
        private static AudioMixerGroup mixer;
        private static GameObject boltPrefab;
        private static float lastSend = -100f;
        private static ZRoutedRpc registeredOn;
        private static Thunder thunder;
        private static Coroutine weather;
        private static string savedEnv;

        void Awake()
        {
            instance = this;
            StartCoroutine(PreloadClipsCoroutine());
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
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
                    Debug.LogWarning("[GraveOfTruth] death effects failed: " + e);
                }
            }
        }

        /// <summary>Opening your own grave to loot it wails once more, gust and all.</summary>
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
                    Debug.LogWarning("[GraveOfTruth] grave effects failed: " + e);
                }
            }
        }

        /// <summary>Tells everyone on the server to put on the show at the grave.</summary>
        private static void Wail(Vector3 pos, bool strike)
        {
            if (Time.realtimeSinceStartup - lastSend <= SendCooldown)
            {
                return;
            }
            lastSend = Time.realtimeSinceStartup;

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
        /// The whole show, run the same way on every modded client: the bolt, the weather and the
        /// jingle. Everything it spawns is local to this client, so nobody sees a thing twice.
        /// </summary>
        private static void RPC_Wail(long sender, Vector3 pos, bool strike)
        {
            // A dedicated server has nobody watching, and its EnvMan is not the players'.
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                return;
            }
            try
            {
                if (strike && !StrikeGrave(pos))
                {
                    SkyFlash(pos);
                }
                Blow(strike ? DeathStormDuration : GraveWindDuration, strike);

                if (instance == null)
                {
                    PlayAt(pos, 1f);
                    return;
                }
                instance.StartCoroutine(WailRoutine(pos, strike ? StrikeLeadIn : 0f));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GraveOfTruth] wail failed: " + e);
            }
        }

        /// <summary>
        /// Waits out the thunder, then rings the jingle over the grave and lets two fading
        /// repeats chase it - a hillside echo, faked with nothing but more audio sources.
        /// </summary>
        private static IEnumerator WailRoutine(Vector3 pos, float leadIn)
        {
            if (leadIn > 0f)
            {
                yield return new WaitForSeconds(leadIn);
            }
            PlayAt(pos, 1f);
            yield return new WaitForSeconds(EchoDelay);
            PlayAt(pos, EchoVolume);
            yield return new WaitForSeconds(TailDelay - EchoDelay);
            PlayAt(pos, TailVolume);
        }

        /// <summary>A throwaway 3D source at the grave, so the jingle carries across the world.</summary>
        private static void PlayAt(Vector3 pos, float volume)
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
            // The repeats come back a shade flatter, the way one would off a hillside.
            source.pitch = volume < 1f ? 0.97f : 1f;
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
            Debug.LogWarning("[GraveOfTruth] no obliterator lightning found, falling back to a sky flash");
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
            Vector3 flashPos = pos + Vector3.up * StrikeAltitude;
            Quaternion rotation = Quaternion.LookRotation(Vector3.down);
            foreach (GameObject flash in t.m_flashEffect.Create(flashPos, Quaternion.identity))
            {
                foreach (Light light in flash.GetComponentsInChildren<Light>())
                {
                    light.transform.rotation = rotation;
                }
            }
            t.m_thunderEffect.Create(pos, Quaternion.identity);
        }

        /// <summary>
        /// Kicks up wind, and for a death the whole thunderstorm. Faked through EnvMan, so every
        /// client runs its own - and a second wail never stacks a second storm on top.
        /// </summary>
        private static void Blow(float duration, bool storm)
        {
            if (instance == null || EnvMan.instance == null)
            {
                return;
            }
            if (weather != null)
            {
                if (!storm)
                {
                    return; // a gust never cuts a running storm short
                }
                instance.StopCoroutine(weather);
            }
            else
            {
                savedEnv = EnvMan.instance.m_debugEnv;
            }
            weather = instance.StartCoroutine(instance.WeatherRoutine(duration, storm));
        }

        private IEnumerator WeatherRoutine(float duration, bool storm)
        {
            EnvMan env = EnvMan.instance;
            env.SetDebugWind(UnityEngine.Random.Range(0f, 360f), WindIntensity);

            if (storm)
            {
                env.m_debugEnv = StormEnv;
                // The bolt on the grave already went off; these are the rumbles after it.
                float remaining = duration;
                while (remaining > 0f)
                {
                    float wait = Mathf.Min(UnityEngine.Random.Range(3f, 6f), remaining);
                    yield return new WaitForSeconds(wait);
                    remaining -= wait;
                    if (remaining > 0f)
                    {
                        yield return DistantStrike();
                        remaining -= 4f;
                    }
                }
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }

            if (EnvMan.instance != null)
            {
                EnvMan.instance.m_debugEnv = savedEnv;
                EnvMan.instance.ResetDebugWind();
            }
            weather = null;
        }

        /// <summary>Flash on the horizon, clap a few seconds later, the way vanilla Thunder does it.</summary>
        private IEnumerator DistantStrike()
        {
            Thunder t = FindThunder();
            Vector3 origin;
            if (t == null || !TryGetOrigin(out origin))
            {
                yield break;
            }

            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float distance = UnityEngine.Random.Range(t.m_flashDistanceMin, t.m_flashDistanceMax);
            Vector3 flashPos = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
            flashPos.y += t.m_flashAltitude;

            Quaternion rotation = Quaternion.LookRotation((origin - flashPos).normalized);
            foreach (GameObject flash in t.m_flashEffect.Create(flashPos, Quaternion.identity))
            {
                foreach (Light light in flash.GetComponentsInChildren<Light>())
                {
                    light.transform.rotation = rotation;
                }
            }

            yield return new WaitForSeconds(UnityEngine.Random.Range(t.m_thunderDelayMin, t.m_thunderDelayMax));
            t.m_thunderEffect.Create(flashPos, Quaternion.identity);
        }

        /// <summary>Thunder lives on the ThunderStorm particle systems, inactive in fair weather.</summary>
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
                Debug.LogWarning("[GraveOfTruth] no Thunder component found, skipping lightning");
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

        public static IEnumerator PreloadClipsCoroutine()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sound.ogg");

            if (!File.Exists(path))
            {
                Debug.LogWarning($"file {path} does not exist!");
                yield break;
            }
            string filename = "file:///" + path.Replace("\\", "/");

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filename, AudioType.OGGVORBIS))
            {
                www.SendWebRequest();
                yield return null;

                if (www != null)
                {
                    DownloadHandlerAudioClip dac = ((DownloadHandlerAudioClip)www.downloadHandler);
                    if (dac != null)
                    {
                        AudioClip ac = dac.audioClip;
                        if (ac != null)
                        {
                            audioClip = ac;
                        }
                        else
                        {
                            Debug.LogWarning("audio clip is null. data: " + dac.text);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("DownloadHandler is null. bytes downloaded: " + www.downloadedBytes);
                    }
                }
                else
                {
                    Debug.LogWarning("www is null " + www.url);
                }
            }
        }
    }
}
