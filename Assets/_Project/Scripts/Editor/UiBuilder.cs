using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.UI;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the in-world menu prefabs (panel and row) with Unity's UI system, and the UI theme that holds their
    /// colours. Run from Roguelike > Build UI Prefabs. Edit the prefabs or the theme afterwards as you like;
    /// rerunning rebuilds the prefabs but keeps the theme's colours.
    /// </summary>
    public static class UiBuilder
    {
        const string Folder = "Assets/_Project/Prefabs/UI";
        const string ThemePath = "Assets/_Project/Resources/UiTheme.asset";

        [MenuItem("Roguelike/Build UI Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            else
            {
                // Start from the palette in UiTheme.cs, so a rebuild always picks up colour changes made there.
                // The typeface is a choice made in the Inspector, so it survives the reset.
                TMP_FontAsset chosenFont = theme.font, chosenDisplay = theme.displayFont;
                var defaults = ScriptableObject.CreateInstance<UiTheme>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), theme);
                Object.DestroyImmediate(defaults);
                theme.font = chosenFont;
                theme.displayFont = chosenDisplay;
            }

            BuildTextStyles(theme);
            theme.menuRow = BuildRow(theme);
            theme.menuPanel = BuildPanel(theme);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiBuilder] Built the menu prefabs in " + Folder + " and the theme at " + ThemePath);
        }

        // ---------------------------------------------------------------- Text styles and sprites

        const string StyleFolder = "Assets/_Project/Materials/UI";
        const string SpriteFolder = "Assets/_Project/Art/UI";

        /// <summary>
        /// The text effect materials (outline, shadow, halo) for the theme's typeface, and the white UI sprites that
        /// panels, bands and rules are tinted from. Materials use TextMesh Pro's mobile SDF shader, the cheap one on
        /// the Quest; being assets, their outline/underlay shader variants are kept in the build.
        /// </summary>
        static void BuildTextStyles(UiTheme theme)
        {
            EnsureFolder(StyleFolder);
            EnsureFolder(SpriteFolder);
            if (theme.font == null) theme.font = TMP_Settings.defaultFontAsset;
            if (theme.displayFont == null) theme.displayFont = DisplayFont(theme.font);
            TMP_FontAsset body = theme.font;
            TMP_FontAsset display = theme.displayFont != null ? theme.displayFont : body;

            var dark = new Color(0.05f, 0.035f, 0.02f, 1f);
            // Labels and HUD: thin outline, small soft shadow down-right.
            theme.textSoft = TextMaterial(body, "Soft", 0.04f, dark, 0.14f, new Color(0f, 0f, 0f, 0.6f), new Vector2(0.7f, -0.7f), 0.15f, 0.35f);
            // Panel and card titles: the same treatment on the display face.
            theme.textTitle = TextMaterial(display, "Title", 0.02f, dark, 0.12f, new Color(0f, 0f, 0f, 0.6f), new Vector2(0.7f, -0.7f), 0.15f, 0.35f);
            // Hit markers: fat outline, a heavier drop shadow, so they pop over blood, fire and foliage.
            theme.textBold = TextMaterial(display, "Bold", 0.08f, new Color(0.09f, 0.03f, 0.02f, 1f), 0.26f, new Color(0f, 0f, 0f, 0.75f), new Vector2(1f, -1f), 0.35f, 0.25f);
            // Banner titles: outline plus a wide, soft dark halo instead of a hard shadow.
            theme.textBanner = TextMaterial(display, "Banner", 0.05f, new Color(0.1f, 0.07f, 0.03f, 1f), 0.16f, new Color(0f, 0f, 0f, 0.85f), Vector2.zero, 0.75f, 0.9f);

            theme.fadeBand = SpriteAsset("UI_FadeBand", 512, 128, Vector4.zero, (x, y) =>
                Smooth(Mathf.Min(x, 1f - x) / 0.32f) * Smooth(Mathf.Min(y, 1f - y) / 0.22f));
            theme.fadeLine = SpriteAsset("UI_FadeLine", 256, 4, Vector4.zero, (x, y) => Smooth(Mathf.Min(x, 1f - x) / 0.4f));
            const int corner = 14;
            theme.panelFill = SpriteAsset("UI_PanelFill", 64, 64, Vector4.one * (corner + 2), (x, y) => RoundedRect(x * 64f, y * 64f, 64f, corner, 0f));
            theme.panelFrame = SpriteAsset("UI_PanelFrame", 64, 64, Vector4.one * (corner + 2), (x, y) =>
                RoundedRect(x * 64f, y * 64f, 64f, corner, 0f) * (1f - RoundedRect(x * 64f, y * 64f, 64f, corner, 2f)));
        }

        const string DisplayFontSource = "Assets/_Project/Art/Fonts/Cinzel/Cinzel-Bold.ttf";
        const string DisplayFontAsset = "Assets/_Project/Art/Fonts/Cinzel/Cinzel-Bold SDF.asset";

        /// <summary>
        /// The display typeface: Cinzel Bold (SIL Open Font License, see Cinzel_OFL.txt next to it) as a TextMesh Pro
        /// SDF font with a pre-baked, static atlas: Basic Latin, Latin-1 (Spanish accents, ñ, ¿ ¡) and typographic
        /// punctuation. Static means no glyphs are rasterised at runtime on the headset; anything missing falls back
        /// to the body font. Made once; delete the asset to rebuild it.
        /// </summary>
        static TMP_FontAsset DisplayFont(TMP_FontAsset fallback)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontAsset);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(DisplayFontSource);
            if (source == null)
            {
                Debug.LogWarning("[UiBuilder] " + DisplayFontSource + " is missing; titles use the body font.");
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 72, 7, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, false);
            var characters = new System.Text.StringBuilder();
            for (int c = 32; c < 127; c++) characters.Append((char)c);
            for (int c = 160; c < 256; c++) characters.Append((char)c);
            characters.Append("–—‘’“”•…×"); // dashes, quotes, bullet, ellipsis, ×
            font.TryAddCharacters(characters.ToString(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log("[UiBuilder] Cinzel has no glyph for: " + missing + " (the body font covers them).");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.name = "Cinzel-Bold SDF";
            if (fallback != null) font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };

            AssetDatabase.CreateAsset(font, DisplayFontAsset);
            font.atlasTexture.name = "Cinzel-Bold Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.material.name = "Cinzel-Bold Material";
            font.material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontAsset);
        }

        static Material TextMaterial(TMP_FontAsset font, string style, float dilate, Color outline, float outlineWidth,
            Color underlay, Vector2 underlayOffset, float underlayDilate, float underlaySoftness)
        {
            string path = StyleFolder + "/Text_" + style + ".mat";
            // Start from the font's own material so the atlas and its SDF settings match.
            var material = new Material(font.material) { name = "Text_" + style };
            material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            material.SetFloat("_FaceDilate", dilate);
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor("_OutlineColor", outline);
            material.SetFloat("_OutlineWidth", outlineWidth);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", underlay);
            material.SetFloat("_UnderlayOffsetX", underlayOffset.x);
            material.SetFloat("_UnderlayOffsetY", underlayOffset.y);
            material.SetFloat("_UnderlayDilate", underlayDilate);
            material.SetFloat("_UnderlaySoftness", underlaySoftness);
            ShaderUtilities.GetShaderPropertyIDs();
            ShaderUtilities.UpdateShaderRatios(material);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            existing.name = material.name;
            Object.DestroyImmediate(material);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Writes a white sprite whose alpha is shape(u, v) (0..1 across the texture) and imports it.</summary>
        static Sprite SpriteAsset(string name, int width, int height, Vector4 border, System.Func<float, float, float> shape)
        {
            string path = SpriteFolder + "/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float a = Mathf.Clamp01(shape((x + 0.5f) / width, (y + 0.5f) / height));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Coverage (anti-aliased) of a rounded square of the given size and corner radius, shrunk by inset.</summary>
        static float RoundedRect(float px, float py, float size, float radius, float inset)
        {
            float half = size * 0.5f - inset;
            float r = Mathf.Max(0f, radius - inset);
            float dx = Mathf.Abs(px - size * 0.5f) - (half - r);
            float dy = Mathf.Abs(py - size * 0.5f) - (half - r);
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
            return Mathf.Clamp01(0.5f - outside);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        // ---------------------------------------------------------------- Menu prefabs

        static GameObject BuildPanel(UiTheme theme)
        {
            // A world-space canvas: 900 px wide; its height follows the rows.
            var root = new GameObject("Menu Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(900f, 600f);
            rect.localScale = Vector3.one * theme.pixelSize;

            // A gold frame behind a slightly smaller dark panel.
            Image border = Child<Image>(rect, "Border");
            Stretch(border.rectTransform, 0f);
            border.color = theme.border;

            Image background = Child<Image>(rect, "Background");
            Stretch(background.rectTransform, 4f);
            background.color = theme.panel;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(rect, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text title = GameText.Apply(Text(content, "Title", 52f, FontStyles.Normal, theme.title, TextAlignmentOptions.Center, 0f), TextStyle.Title);
            title.enableWordWrapping = true;
            // No fixed height: a long header wraps onto a second line and pushes the rows down instead of overlapping them.
            TMP_Text header = Text(content, "Header", 30f, FontStyles.Normal, theme.header, TextAlignmentOptions.Center, 0f);
            header.enableWordWrapping = true;

            // A thin gold rule between the header and the rows.
            var rule = Child<Image>(content, "Rule");
            rule.color = theme.border;
            rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 3f;

            var rows = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            rows.SetParent(content, false);
            var rowLayout = rows.GetComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            TMP_Text footer = Text(content, "Footer", 26f, FontStyles.Italic, theme.header, TextAlignmentOptions.Center, 0f);
            footer.enableWordWrapping = true;

            var view = root.AddComponent<MenuPanelView>();
            view.root = rect;
            view.background = background;
            view.border = border;
            view.title = title;
            view.header = header;
            view.rows = rows;
            view.footer = footer;

            return Save(root, Folder + "/MenuPanel.prefab");
        }

        static GameObject BuildRow(UiTheme theme)
        {
            var root = new GameObject("Menu Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(828f, 72f);
            var background = root.GetComponent<Image>();
            background.color = theme.row;
            root.GetComponent<LayoutElement>().minHeight = 72f;
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 8, 8);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // The label takes the room left over; the detail keeps a fixed column on the right, so they never overlap.
            TMP_Text label = Text(rect, "Label", 34f, FontStyles.Normal, theme.rowText, TextAlignmentOptions.MidlineLeft, 0f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text detail = Text(rect, "Detail", 30f, FontStyles.Normal, theme.rowDetail, TextAlignmentOptions.MidlineRight, 0f);
            detail.overflowMode = TextOverflowModes.Ellipsis;
            var detailLayout = detail.GetComponent<LayoutElement>();
            detailLayout.preferredWidth = 260f;
            detailLayout.flexibleWidth = 0f;

            var view = root.AddComponent<MenuRowView>();
            view.rect = rect;
            view.background = background;
            view.label = label;
            view.detail = detail;
            return Save(root, Folder + "/MenuRow.prefab");
        }

        static T Child<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>A text line with a fixed size (no auto-sizing, so every menu reads the same).</summary>
        static TMP_Text Text(Transform parent, string name, float size, FontStyles style, Color color, TextAlignmentOptions alignment, float height)
        {
            var text = GameText.Apply(Child<TextMeshProUGUI>(parent, name), TextStyle.Plain);
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            var element = text.gameObject.AddComponent<LayoutElement>();
            if (height > 0f) element.preferredHeight = height;
            return text;
        }

        static GameObject Save(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
