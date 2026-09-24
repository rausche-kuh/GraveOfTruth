using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// The PNGs shipped beside the DLL as sprites, loaded once each. Null when a file is missing
    /// or unreadable, which leaves an item with the game's blank icon but working. The zip carries
    /// them in icons/, but a mod manager may flatten that folder into the plugin directory on
    /// install (Gale does), so both places are tried.
    /// </summary>
    internal static class Icons
    {
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        internal static Sprite Get(string name)
        {
            if (icons.TryGetValue(name, out Sprite sprite))
            {
                return sprite;
            }
            sprite = null;
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string path = IconPath(dir, name);
                if (path != null)
                {
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (LoadPng(texture, File.ReadAllBytes(path)))
                    {
                        texture.filterMode = FilterMode.Bilinear;
                        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f);
                        sprite.name = name;
                    }
                }
                else
                {
                    Debug.LogWarning("[OdinsCompass] icon missing: " + name + ".png, looked in "
                        + Path.Combine(dir, "icons") + " and " + dir);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OdinsCompass] could not load icon " + name + ": " + e.Message);
            }
            icons[name] = sprite;
            return sprite;
        }

        private static string IconPath(string dir, string name)
        {
            string file = name + ".png";
            string[] candidates = { Path.Combine(Path.Combine(dir, "icons"), file), Path.Combine(dir, file) };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static MethodInfo loadImage;

        /// <summary>
        /// The game's PNG decoder, ImageConversion.LoadImage(Texture2D, byte[]). Its module is
        /// built against netstandard 2.1, which a net472 build cannot reference, so it is found at
        /// runtime instead of at compile time.
        /// </summary>
        private static bool LoadPng(Texture2D texture, byte[] png)
        {
            if (loadImage == null)
            {
                Type conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                loadImage = conversion != null
                    ? conversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) })
                    : null;
                if (loadImage == null)
                {
                    Debug.LogWarning("[OdinsCompass] ImageConversion.LoadImage not found; icons stay blank");
                    return false;
                }
            }
            return (bool)loadImage.Invoke(null, new object[] { texture, png });
        }
    }
}
