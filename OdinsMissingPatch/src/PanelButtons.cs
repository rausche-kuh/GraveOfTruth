using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OdinsMissingPatch
{
    /// <summary>
    /// Square icon buttons for the inventory screen, shared by ChestButtons and InventoryButtons.
    /// Each is a copy of the chest panel's own Take all button, so it keeps the game's button
    /// skin, hover tint and click sound, with the label blanked and an icon from assets/icons/
    /// drawn in its place in the colour the label had. A tooltip names the button, since the
    /// icon is all that shows. The buttons stand beside the panels, clear of their background,
    /// in a column each at the same distance from the panel: beside the inventory between the
    /// armour and weight boxes, beside the chest from its top edge down.
    /// </summary>
    internal static class PanelButtons
    {
        /// <summary>The space between two buttons, in UI pixels.</summary>
        internal const float Gap = 6f;

        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        /// <summary>The side of a button: the Take all button's height, so it matches the panel.</summary>
        internal static float Size(InventoryGui gui)
        {
            Button template = gui != null ? gui.m_takeAllButton : null;
            return template != null ? ((RectTransform)template.transform).rect.height : 32f;
        }

        /// <summary>
        /// A new button under <paramref name="parent"/>, inactive until its owner places it.
        /// Null when the panel has no Take all button to copy, in which case there is nothing to
        /// build a column from and the owner leaves the panel as it is.
        /// </summary>
        internal static Button Create(InventoryGui gui, Transform parent, string name, string icon,
            string title, string tooltip, UnityAction onClick)
        {
            Button template = gui != null ? gui.m_takeAllButton : null;
            if (template == null)
            {
                return null;
            }
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = "OMP_" + name;
            Button button = go.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            button.interactable = true;

            float size = Size(gui);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);

            AddIcon(go, icon, BlankLabel(go), size);

            UITooltip tip = go.GetComponent<UITooltip>();
            if (tip == null)
            {
                GameObject prefab = TooltipPrefab(gui);
                if (prefab != null)
                {
                    tip = go.AddComponent<UITooltip>();
                    tip.m_tooltipPrefab = prefab;
                }
            }
            if (tip != null)
            {
                tip.m_topic = title;
                tip.m_text = tooltip;
            }
            go.SetActive(false);
            return button;
        }

        // ---- Where things are ------------------------------------------------------------------

        /// <summary>
        /// Puts a rect's centre at <paramref name="center"/>, measured from its parent's bottom
        /// left corner, whatever it was anchored to before; the size is kept.
        /// </summary>
        internal static void Pin(RectTransform rect, Vector2 center)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
        }

        /// <summary>
        /// How far a panel's visible background reaches past its rect, to the right (x) and
        /// above (y): the background is a stretched child (`Bkg`) with a size delta, half of
        /// which hangs out on each side. 10px either way when the child is not as expected.
        /// </summary>
        private static Vector2 Overhang(RectTransform panel)
        {
            RectTransform bkg = panel.Find("Bkg") as RectTransform;
            if (bkg != null && bkg.anchorMin == Vector2.zero && bkg.anchorMax == Vector2.one)
            {
                return Vector2.Max(Vector2.zero, bkg.sizeDelta * 0.5f);
            }
            return new Vector2(10f, 10f);
        }

        /// <summary>
        /// Where a button's left edge goes beside a panel, measured from the panel's left: past
        /// the panel's background plus a gap. The readout boxes' own rects overlap that border,
        /// so lining up with them would put a button on the panel's edge.
        /// </summary>
        internal static float ColumnLeft(RectTransform panel)
        {
            return panel.rect.width + Overhang(panel).x + Gap;
        }

        /// <summary>
        /// Where the first button's top edge goes beside a panel, measured from the panel's
        /// bottom: level with the top of the panel's background, so the buttons read as part
        /// of the panel they belong to.
        /// </summary>
        internal static float ColumnTop(RectTransform panel)
        {
            return panel.rect.height + Overhang(panel).y;
        }

        /// <summary>
        /// A readout box beside a panel (the armour or weight box): the parent of the
        /// InventoryGui text that shows the number, found by field name since the text is a
        /// TextMeshPro type the build cannot reference, else the panel's child of that name.
        /// Null when neither is there.
        /// </summary>
        private static RectTransform Box(InventoryGui gui, string field, RectTransform panel, string name)
        {
            FieldInfo info = typeof(InventoryGui).GetField(field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Component text = info != null ? info.GetValue(gui) as Component : null;
            RectTransform box = text != null ? text.transform.parent as RectTransform : null;
            if (box == null && panel != null)
            {
                box = panel.Find(name) as RectTransform;
            }
            return box;
        }

        /// <summary>
        /// A rect's edges in a panel's space, measured from the panel's bottom left corner like
        /// everything Pin places, whatever the rect is anchored to and wherever it sits in the
        /// hierarchy.
        /// </summary>
        private static Rect InPanel(RectTransform panel, RectTransform rect)
        {
            Vector3 min = panel.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
            Vector3 max = panel.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
            Vector2 origin = Vector2.Scale(panel.pivot, panel.rect.size);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x) + origin.x, Mathf.Min(min.y, max.y) + origin.y,
                Mathf.Max(min.x, max.x) + origin.x, Mathf.Max(min.y, max.y) + origin.y);
        }

        // ---- The column beside the inventory ---------------------------------------------------

        private static readonly List<KeyValuePair<int, Button>> column = new List<KeyValuePair<int, Button>>();

        /// <summary>
        /// Enters a button into the column beside the inventory panel; <paramref name="order"/>
        /// is its place from the top. Only active buttons take a place, so the column closes up
        /// around whatever is hidden.
        /// </summary>
        internal static void Enlist(Button button, int order)
        {
            column.Add(new KeyValuePair<int, Button>(order, button));
            column.Sort((a, b) => a.Key.CompareTo(b.Key));
        }

        /// <summary>
        /// Stacks the column's active buttons top to bottom from the column's left edge, centred
        /// in the space between the armour box and the weight box (the whole side of the panel
        /// when a box is missing). Every owner calls this from its per-frame postfix after
        /// showing or hiding its buttons, since the column is shared and the last call of a
        /// frame settles it.
        /// </summary>
        internal static void LayoutInventoryColumn(InventoryGui gui)
        {
            RectTransform panel = gui != null ? gui.m_player : null;
            if (panel == null)
            {
                return;
            }
            float size = Size(gui);
            int count = 0;
            foreach (KeyValuePair<int, Button> entry in column)
            {
                if (entry.Value != null && entry.Value.gameObject.activeSelf)
                {
                    count++;
                }
            }
            if (count == 0)
            {
                return;
            }
            RectTransform armor = Box(gui, "m_armor", panel, "Armor");
            RectTransform weight = Box(gui, "m_weight", panel, "Weight");
            float top = armor != null ? InPanel(panel, armor).yMin : panel.rect.height;
            float bottom = weight != null ? InPanel(panel, weight).yMax : 0f;
            float stack = count * size + (count - 1) * Gap;
            float x = ColumnLeft(panel) + size * 0.5f;
            float y = (top + bottom) * 0.5f + stack * 0.5f - size * 0.5f;
            foreach (KeyValuePair<int, Button> entry in column)
            {
                if (entry.Value == null || !entry.Value.gameObject.activeSelf)
                {
                    continue;
                }
                RectTransform rect = (RectTransform)entry.Value.transform;
                rect.sizeDelta = new Vector2(size, size);
                Pin(rect, new Vector2(x, y));
                y -= size + Gap;
            }
        }

        // ---- The column beside the chest -------------------------------------------------------

        /// <summary>
        /// Stacks <paramref name="buttons"/> in one column beside the chest panel, in the order
        /// given from the top down, the first one level with the top of the panel. Null entries
        /// are skipped.
        /// </summary>
        internal static void LayoutChestColumn(InventoryGui gui, IList<Button> buttons)
        {
            RectTransform panel = gui != null ? gui.m_container : null;
            if (panel == null)
            {
                return;
            }
            float size = Size(gui);
            float x = ColumnLeft(panel) + size * 0.5f;
            float y = ColumnTop(panel) - size * 0.5f;
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }
                RectTransform rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(size, size);
                Pin(rect, new Vector2(x, y));
                y -= size + Gap;
            }
        }

        /// <summary>
        /// The icon named by a PNG in assets/icons/ beside the DLL, loaded once. Null when the file
        /// is missing or unreadable, which leaves the button blank but working.
        /// </summary>
        internal static Sprite Icon(string name)
        {
            if (icons.TryGetValue(name, out Sprite sprite))
            {
                return sprite;
            }
            sprite = null;
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(Path.Combine(dir, "icons"), name + ".png");
                if (File.Exists(path))
                {
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (LoadPng(texture, File.ReadAllBytes(path)))
                    {
                        texture.filterMode = FilterMode.Bilinear;
                        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                else
                {
                    Debug.LogWarning("[OdinsMissingPatch] icon missing: " + path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OdinsMissingPatch] could not load icon " + name + ": " + e.Message);
            }
            icons[name] = sprite;
            return sprite;
        }

        private static MethodInfo loadImage;

        /// <summary>
        /// The game's PNG decoder, ImageConversion.LoadImage(Texture2D, byte[]). Its module is
        /// built against netstandard 2.1, which a net472 build cannot reference, so it is
        /// found at runtime instead of at compile time.
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
                    Debug.LogWarning("[OdinsMissingPatch] ImageConversion.LoadImage not found; icons stay blank");
                    return false;
                }
            }
            return (bool)loadImage.Invoke(null, new object[] { texture, png });
        }

        /// <summary>
        /// Empties the copied label and returns its colour. The label is a TextMeshPro text,
        /// which is not among the staged reference assemblies, so it is found by its text
        /// property; its colour is the Graphic colour every UI text has.
        /// </summary>
        private static Color BlankLabel(GameObject go)
        {
            Color tint = Color.white;
            foreach (Component component in go.GetComponentsInChildren<Component>(true))
            {
                PropertyInfo text = component.GetType().GetProperty("text", typeof(string));
                if (text == null || !text.CanWrite)
                {
                    continue;
                }
                if (component is Graphic graphic)
                {
                    tint = graphic.color;
                }
                text.SetValue(component, "", null);
            }
            return tint;
        }

        private static void AddIcon(GameObject go, string icon, Color tint, float size)
        {
            GameObject child = new GameObject("icon", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(go.transform, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            float inset = size * 0.2f;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            Image image = child.GetComponent<Image>();
            image.sprite = Icon(icon);
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = image.sprite != null;
        }

        /// <summary>
        /// The tooltip prefab the inventory slots use, for a button that was copied without one.
        /// </summary>
        private static GameObject TooltipPrefab(InventoryGui gui)
        {
            InventoryGrid grid = gui.m_playerGrid;
            GameObject prefab = grid != null ? grid.m_elementPrefab : null;
            InventoryElement element = prefab != null ? prefab.GetComponent<InventoryElement>() : null;
            if (element != null && element.m_tooltip != null && element.m_tooltip.m_tooltipPrefab != null)
            {
                return element.m_tooltip.m_tooltipPrefab;
            }
            foreach (UITooltip tip in gui.GetComponentsInChildren<UITooltip>(true))
            {
                if (tip.m_tooltipPrefab != null)
                {
                    return tip.m_tooltipPrefab;
                }
            }
            return null;
        }
    }
}
