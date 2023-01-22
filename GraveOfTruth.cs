using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace GraveOfTruth
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class GraveOfTruthAudio : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.graveoftruth";
        public const string NAME = "GraveOfTruth";
        public const string VERSION = "0.0.1";

        private static AudioClip audioClip;
        private static float lastPlay;

        void Awake()
        {
            StartCoroutine(PreloadClipsCoroutine());
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }


        [HarmonyPatch(typeof(TombStone), "GetHoverText")]
        public static class PlaySoundOnGrave
        {
            private static void Postfix(ref TombStone __instance)
            {
                if (__instance.IsOwner())
                {
                    playAudio();
                }

            }
        }

        public static void playAudio()
        {
            if (Time.realtimeSinceStartup - lastPlay > audioClip.length * 3)
            {
                lastPlay = Time.realtimeSinceStartup;
                Destroy(Player.m_localPlayer.gameObject.GetComponent<AudioSource>());
                AudioSource playerAudio = Player.m_localPlayer.gameObject.AddComponent<AudioSource>();
                playerAudio.volume = 1f;
                playerAudio.clip = audioClip;
                playerAudio.loop = false;
                playerAudio.spatialBlend = 0.5f;
                playerAudio.Play();
            }
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
            //Debug.LogWarning($"getting audio clip from filename: {filename}");

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
