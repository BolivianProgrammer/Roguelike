using TMPro;
using UnityEngine;

namespace Roguelike.UI
{
    /// <summary>How a piece of text should look. Each maps to a typeface and a material preset on the <see cref="UiTheme"/>.</summary>
    public enum TextStyle
    {
        /// <summary>Body text on a panel (menus, card descriptions): no effects needed.</summary>
        Plain,
        /// <summary>Body text floating in the world or over the game: thin outline and a small shadow.</summary>
        Soft,
        /// <summary>Titles on panels and cards: display typeface, thin outline, small shadow.</summary>
        Title,
        /// <summary>Hit markers and damage numbers: display typeface, heavy outline and a drop shadow.</summary>
        Bold,
        /// <summary>Announcement titles: display typeface, outline and a soft dark halo.</summary>
        Banner,
    }

    /// <summary>
    /// The one place that decides how text looks: typeface and effect material come from the UI theme, so every
    /// label, prompt, hit marker and banner in the game shares one style. Two typefaces: a display face (Cinzel) for
    /// titles and anything short and loud, and a plain body face for anything meant to be read at length.
    /// Materials are assets (made by Roguelike > Build UI Prefabs), never created at runtime: the Quest build only
    /// keeps the shader variants (outline, underlay) that some material asset uses.
    /// </summary>
    public static class GameText
    {
        /// <summary>The body typeface.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                var theme = UiTheme.Instance;
                return theme.font != null ? theme.font : TMP_Settings.defaultFontAsset;
            }
        }

        /// <summary>The display typeface (falls back to the body one).</summary>
        public static TMP_FontAsset DisplayFont
        {
            get
            {
                var theme = UiTheme.Instance;
                return theme.displayFont != null ? theme.displayFont : Font;
            }
        }

        public static bool UsesDisplayFont(TextStyle style) => style == TextStyle.Title || style == TextStyle.Bold || style == TextStyle.Banner;

        /// <summary>Gives a text the typeface and effect material for its style.</summary>
        public static T Apply<T>(T text, TextStyle style) where T : TMP_Text
        {
            if (text == null) return null;
            TMP_FontAsset font = UsesDisplayFont(style) ? DisplayFont : Font;
            if (font != null && text.font != font) text.font = font;

            Material preset = Preset(style);
            // A preset made for another atlas would draw garbage; fall back to the font's own material then.
            if (preset != null && font != null && preset.mainTexture == font.material.mainTexture) text.fontSharedMaterial = preset;
            else if (font != null) text.fontSharedMaterial = font.material;
            return text;
        }

        /// <summary>A new world-space text (TextMeshPro) under a parent, styled and centred.</summary>
        public static TextMeshPro World(Transform parent, string name, TextStyle style, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            Apply(text, style);
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            // World text never needs shadows cast on it or by it.
            var meshRenderer = text.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return text;
        }

        static Material Preset(TextStyle style)
        {
            var theme = UiTheme.Instance;
            switch (style)
            {
                case TextStyle.Soft: return theme.textSoft;
                case TextStyle.Title: return theme.textTitle;
                case TextStyle.Bold: return theme.textBold;
                case TextStyle.Banner: return theme.textBanner;
                default: return null;
            }
        }
    }
}
