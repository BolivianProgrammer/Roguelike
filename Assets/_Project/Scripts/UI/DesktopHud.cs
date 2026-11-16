using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.Player;

namespace Roguelike.UI
{
    /// <summary>
    /// The on-screen HUD for mouse-and-keyboard play (in the headset the same information is on the wrist and in the
    /// world): vitals bottom left, objective panel top right, combat line bottom centre, crosshair, and the controls
    /// card top left. Dark panels with a gold hairline frame, in the UI theme's colours and type.
    ///
    /// One screen-space canvas, built on first use. Game code pushes values in through the Set methods, which do
    /// nothing when the value has not changed, so text is only laid out again when it really changes (unlike OnGUI,
    /// which rebuilt and allocated everything several times a frame). The bars sit on their own nested canvas so
    /// their per-frame changes do not rebatch the rest.
    /// </summary>
    public class DesktopHud : MonoBehaviour
    {
        static DesktopHud instance;

        Canvas canvas;
        RectTransform healthFill, staminaFill;
        TextMeshProUGUI healthText, combatText, weaponText;
        GameObject objectivePanel, helpPanel, helpHint, crosshair;
        TextMeshProUGUI objectiveTitle, objectiveBody, helpBody;

        int shownHealth = -1, shownMax = -1;
        float shownHealthFill = -1f, shownStaminaFill = -1f;
        string shownCombat, shownWeapon, shownTitle, shownBody, shownHelp;
        bool helpExpanded = true;

        /// <summary>The HUD, built on first use; null when not playing on a monitor.</summary>
        public static DesktopHud Get()
        {
            if (!DesktopRig.IsDesktop) return null;
            if (instance == null) instance = Build();
            return instance;
        }

        // ---------------------------------------------------------------- Values

        public void SetVitals(float health01, float stamina01, float health, float maxHealth)
        {
            SetFill(healthFill, health01, ref shownHealthFill);
            SetFill(staminaFill, stamina01, ref shownStaminaFill);
            int current = Mathf.CeilToInt(health), max = Mathf.CeilToInt(maxHealth);
            if (current == shownHealth && max == shownMax) return;
            shownHealth = current;
            shownMax = max;
            healthText.SetText("{0}<size=70%><color=#9C958A> / {1}</color></size>", current, max);
        }

        /// <summary>The aim / heavy / last-hit line above the weapon. Null or empty hides it.</summary>
        public void SetCombat(string text) => SetLine(combatText, text, ref shownCombat);

        /// <summary>The held weapon and its special. Null or empty hides it.</summary>
        public void SetWeapon(string text) => SetLine(weaponText, text, ref shownWeapon);

        /// <summary>The panel at the top right (mission objective and loot, or the hub summary). Null title hides it.</summary>
        public void SetObjective(string title, string body)
        {
            bool show = !string.IsNullOrEmpty(title);
            if (objectivePanel.activeSelf != show) objectivePanel.SetActive(show);
            if (!show) return;
            if (title != shownTitle) objectiveTitle.text = shownTitle = title;
            if (body != shownBody) objectiveBody.text = shownBody = body;
        }

        /// <summary>The controls card: expanded shows the full list, otherwise a small "F1 Controls" hint.</summary>
        public void SetHelp(bool expanded, string body)
        {
            if (expanded != helpExpanded)
            {
                helpExpanded = expanded;
                helpPanel.SetActive(expanded);
                helpHint.SetActive(!expanded);
            }
            if (expanded && body != shownHelp) helpBody.text = shownHelp = body;
        }

        public void SetCrosshair(bool visible)
        {
            if (crosshair.activeSelf != visible) crosshair.SetActive(visible);
        }

        void LateUpdate()
        {
            // The pause menu has the screen to itself; the HUD also hides if the player switches to the headset.
            bool show = DesktopRig.IsDesktop && !PauseMenu.IsPaused;
            if (canvas.enabled != show) canvas.enabled = show;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        static void SetFill(RectTransform fill, float value, ref float shown)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - shown) < 0.001f) return;
            shown = value;
            fill.anchorMax = new Vector2(value, 1f);
        }

        static void SetLine(TextMeshProUGUI text, string value, ref string shown)
        {
            if (value == shown) return;
            shown = value;
            bool show = !string.IsNullOrEmpty(value);
            if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
            if (show) text.text = value;
        }

        // ---------------------------------------------------------------- Construction

        static DesktopHud Build()
        {
            UiTheme theme = UiTheme.Instance;
            var go = new GameObject("Desktop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var hud = go.AddComponent<DesktopHud>();
            hud.canvas = go.GetComponent<Canvas>();
            hud.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            // Vitals, bottom left, on a nested canvas because the bars change every frame.
            RectTransform vitals = Panel(root, "Vitals", new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(440f, 92f), theme);
            vitals.gameObject.AddComponent<Canvas>();
            hud.healthFill = Bar(vitals, "Health", new Vector2(20f, -22f), new Vector2(300f, 18f), theme.health, theme);
            hud.staminaFill = Bar(vitals, "Stamina", new Vector2(20f, -54f), new Vector2(300f, 10f), theme.stamina, theme);
            hud.healthText = Label(vitals, "Health Value", 30f, TextStyle.Soft, theme.title, TextAlignmentOptions.MidlineRight);
            Place(hud.healthText.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -12f), new Vector2(100f, 40f));
            hud.healthText.fontStyle = FontStyles.Bold;

            // Combat line and weapon, bottom centre.
            hud.weaponText = Label(root, "Weapon", 28f, TextStyle.Soft, theme.header, TextAlignmentOptions.Bottom);
            Place(hud.weaponText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1100f, 40f));
            hud.combatText = Label(root, "Combat", 24f, TextStyle.Soft, theme.disabledText, TextAlignmentOptions.Bottom);
            Place(hud.combatText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(1100f, 34f));

            // Objective panel, top right: grows with its text.
            RectTransform objective = Panel(root, "Objective", new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(460f, 0f), theme);
            Stack(objective, new RectOffset(26, 26, 18, 22), 8f);
            hud.objectiveTitle = Label(objective, "Title", 30f, TextStyle.Title, theme.title, TextAlignmentOptions.Left);
            hud.objectiveTitle.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            hud.objectiveTitle.characterSpacing = 4f;
            Rule(objective, theme);
            hud.objectiveBody = Label(objective, "Body", 23f, TextStyle.Plain, theme.header, TextAlignmentOptions.TopLeft);
            hud.objectiveBody.enableWordWrapping = true;
            hud.objectiveBody.lineSpacing = 8f;
            hud.objectivePanel = objective.gameObject;
            hud.objectivePanel.SetActive(false);

            // Controls card, top left, and the hint shown when it is folded away.
            RectTransform help = Panel(root, "Controls", new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(500f, 0f), theme);
            Stack(help, new RectOffset(26, 26, 18, 22), 8f);
            TextMeshProUGUI helpTitle = Label(help, "Title", 28f, TextStyle.Title, theme.title, TextAlignmentOptions.Left);
            helpTitle.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            helpTitle.characterSpacing = 4f;
            helpTitle.text = "Controls <size=70%><color=#9C958A>  F1 to hide</color></size>";
            Rule(help, theme);
            hud.helpBody = Label(help, "Body", 21f, TextStyle.Plain, theme.header, TextAlignmentOptions.TopLeft);
            hud.helpBody.enableWordWrapping = true;
            hud.helpBody.lineSpacing = 4f;
            hud.helpPanel = help.gameObject;

            TextMeshProUGUI hint = Label(root, "Controls Hint", 22f, TextStyle.Soft, theme.header, TextAlignmentOptions.TopLeft);
            Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(300f, 34f));
            hint.text = "<color=#FFD25A>F1</color>  Controls";
            hud.helpHint = hint.gameObject;
            hud.helpHint.SetActive(false);

            // Crosshair: a light dot in a dark ring, readable on any background.
            var cross = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
            cross.SetParent(root, false);
            Place(cross, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
            Image ring = Fill(cross, "Ring", new Color(0f, 0f, 0f, 0.55f), theme.panelFill);
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
            Image dot = Fill(cross, "Dot", new Color(1f, 0.95f, 0.85f, 0.9f), theme.panelFill);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            hud.crosshair = cross.gameObject;

            return hud;
        }

        /// <summary>A dark rounded panel with a gold hairline frame, anchored at a screen corner.</summary>
        static RectTransform Panel(RectTransform parent, string name, Vector2 corner, Vector2 offset, Vector2 size, UiTheme theme)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            Image fill = Fill(rect, "Fill", theme.hudPanel, theme.panelFill);
            Stretch(fill.rectTransform);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Color frameColor = theme.border;
            frameColor.a = 0.55f;
            Image frame = Fill(rect, "Frame", frameColor, theme.panelFrame);
            Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return rect;
        }

        /// <summary>Makes a panel stack its children top to bottom and grow to fit them.</summary>
        static void Stack(RectTransform panel, RectOffset padding, float spacing)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static void Rule(RectTransform parent, UiTheme theme)
        {
            Color color = theme.border;
            color.a = 0.8f;
            Image rule = Fill(parent, "Rule", color, theme.fadeLine);
            rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 2f;
        }

        /// <summary>A bar: dark track and a coloured fill whose right edge follows the value (anchorMax.x).</summary>
        static RectTransform Bar(RectTransform parent, string name, Vector2 topLeft, Vector2 size, Color color, UiTheme theme)
        {
            Image track = Fill(parent, name, theme.barBack, theme.panelFill);
            RectTransform rect = track.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;

            Image fill = Fill(rect, "Fill", color, theme.panelFill);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            return fillRect;
        }

        static Image Fill(Transform parent, string name, Color color, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Label(Transform parent, string name, float size, TextStyle style, Color color, TextAlignmentOptions alignment)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            GameText.Apply(text, style);
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
