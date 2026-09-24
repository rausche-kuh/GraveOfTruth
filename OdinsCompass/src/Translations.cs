using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// The mod's own words, in translations.csv beside the DLL: one row per $oc_ token, one column
    /// per language, English first. The game's own CSV reader loads it, so an empty cell falls
    /// back to English by itself and a new language is a new column and nothing else. Item names,
    /// descriptions, status effect text and the group names all pass through the game's
    /// localization when shown, so call sites hold tokens only.
    /// </summary>
    internal static class Translations
    {
        private const string FileName = "translations.csv";
        private const string Fallback = "English";

        private static string csv;
        private static string[] languages;

        /// <summary>
        /// Feeds the file to the game's loader every time a language is set up - on startup and
        /// again on every language change, which wipes every translation first. A missing file
        /// only leaves the mod's tokens showing as [oc_...].
        /// </summary>
        [HarmonyPatch(typeof(Localization), "SetupLanguage")]
        private static class Load
        {
            private static void Postfix(Localization __instance, string language)
            {
                if (__instance == null || !Read() || !Reader())
                {
                    return;
                }
                object asset = null;
                try
                {
                    asset = textAsset.Invoke(new object[] { csv });
                    loadCsv.Invoke(__instance, new[] { asset, Has(language) ? language : Fallback });
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OdinsCompass] could not load " + FileName + ": " + e.Message);
                }
                finally
                {
                    UnityEngine.Object leftover = asset as UnityEngine.Object;
                    if (leftover != null)
                    {
                        UnityEngine.Object.Destroy(leftover);
                    }
                }
            }
        }

        private static bool Read()
        {
            if (languages != null)
            {
                return csv != null;
            }
            languages = new string[0];
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(dir, FileName);
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[OdinsCompass] " + FileName + " missing: " + path);
                    return false;
                }
                csv = File.ReadAllText(path);
                using (StringReader reader = new StringReader(csv))
                {
                    string header = reader.ReadLine();
                    languages = header != null ? header.Split(',') : new string[0];
                }
                for (int i = 0; i < languages.Length; i++)
                {
                    languages[i] = languages[i].Trim().Trim('"');
                }
            }
            catch (Exception e)
            {
                csv = null;
                Debug.LogWarning("[OdinsCompass] could not read " + FileName + ": " + e.Message);
            }
            return csv != null;
        }

        private static bool Has(string language)
        {
            foreach (string known in languages)
            {
                if (known == language)
                {
                    return true;
                }
            }
            return false;
        }

        private static ConstructorInfo textAsset;
        private static MethodInfo loadCsv;
        private static bool looked;

        /// <summary>
        /// The game's CSV reader, Localization.LoadCSV(TextAsset, string), and the TextAsset to
        /// hand it, both found at runtime: TextAsset lives in a module built against
        /// netstandard 2.1, which a net472 build cannot name.
        /// </summary>
        private static bool Reader()
        {
            if (looked)
            {
                return loadCsv != null && textAsset != null;
            }
            looked = true;
            Type asset = Type.GetType("UnityEngine.TextAsset, UnityEngine.CoreModule");
            if (asset != null)
            {
                textAsset = asset.GetConstructor(new[] { typeof(string) });
                loadCsv = typeof(Localization).GetMethod("LoadCSV", new[] { asset, typeof(string) });
            }
            if (loadCsv == null || textAsset == null)
            {
                Debug.LogWarning("[OdinsCompass] Localization.LoadCSV not found; the mod's words stay untranslated");
                return false;
            }
            return true;
        }
    }
}
