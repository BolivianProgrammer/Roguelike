using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roguelike.Player;
using Roguelike.Run;

namespace Roguelike.UI
{
    /// <summary>
    /// The choice of blessings after a totem falls, laid out like the boon choice in Hades: a header, then the
    /// offers as wide rows stacked top to bottom. Each row has an icon framed in its rarity colour (Common pale,
    /// Rare blue, Legendary orange), the name in the display typeface, a rarity tag, and the description in large
    /// plain text with its numbers picked out in gold. Rows slide in one after another; the one pointed at lights up
    /// and nudges aside.
    ///
    /// On a monitor it is drawn on the screen over a dimmed game, picked with the mouse or the 1-3 keys. In the
    /// headset it floats in front of the player; a beam comes from each hand and the trigger picks. Enemies freeze
    /// while it is up (see <see cref="Combat.WorldFreeze"/>), so the choice can be made in peace mid-fight.
    /// Built once per offer from the UI theme and destroyed after the pick.
    /// </summary>
    public class BuffCards : MonoBehaviour
    {
        // Canvas pixels. On screen they follow a 1920x1080 reference; in the world 1 px = the theme's pixel size (1 mm).
        const float Width = 1120f;
        const float RowHeight = 176f;
        const float RowGap = 18f;
        const float Icon = 132f;
        const float WorldDistance = 1.35f;
        const float PickDelay = 0.6f;
        const float SlideIn = 0.28f;
        const float Stagger = 0.08f;

        static BuffCards current;
        static readonly Regex Numbers = new Regex(@"[+\-]?\d+(?:[.,]\d+)?%?", RegexOptions.Compiled);

        class Row
        {
            public BuffDefinition Buff;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Back, Frame, Accent;
            public Color RarityColor;
            public float Hover;
        }

        readonly List<Row> rows = new List<Row>();
        Action<BuffDefinition> onChosen;
        Canvas canvas;
        RectTransform rowsRoot;
        CanvasGroup whole;
        PhysicsHand[] hands;
        LineRenderer[] beams;
        InputAction leftSelect, rightSelect;
        Row hovered;
        float openedAt;
        bool onScreen;

        /// <summary>True while a choice of blessings is up.</summary>
        public static bool IsOpen => current != null;

        /// <summary>Shows one row per buff and calls back with the one the player picks.</summary>
        public static void Offer(IList<BuffDefinition> buffs, Action<BuffDefinition> onChosen)
        {
            if (current != null) Destroy(current.gameObject);
            if (buffs == null || buffs.Count == 0) return;

            var names = new List<string>();
            foreach (var buff in buffs) names.Add(buff.name);
            Combat.CombatLog.Log("CARDS offered [" + string.Join(", ", names) + "]");

            var go = new GameObject("Blessing Choice", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            current = go.AddComponent<BuffCards>();
            current.onChosen = onChosen;
            current.Build(buffs);
        }

        void Awake()
        {
            openedAt = Time.unscaledTime;
            onScreen = DesktopRig.IsDesktop;
            hands = FindObjectsOfType<PhysicsHand>();

            leftSelect = new InputAction("Card Select Left", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
            rightSelect = new InputAction("Card Select Right", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            leftSelect.Enable();
            rightSelect.Enable();

            // The enemies wait while the player chooses.
            Combat.WorldFreeze.Hold(this);

            if (onScreen) DesktopRig.InputBlocked = true;
            else CreateBeams();
        }

        void OnDestroy()
        {
            leftSelect?.Disable();
            rightSelect?.Disable();
            if (beams != null)
                foreach (var beam in beams)
                    if (beam != null) Destroy(beam.gameObject);

            Combat.WorldFreeze.Release(this);
            if (current != this) return;
            current = null;
            DesktopRig.InputBlocked = false;
        }

        // ---------------------------------------------------------------- Building

        void Build(IList<BuffDefinition> buffs)
        {
            UiTheme theme = UiTheme.Instance;
            canvas = GetComponent<Canvas>();
            whole = GetComponent<CanvasGroup>();
            whole.interactable = false;
            whole.blocksRaycasts = false;
            var root = (RectTransform)transform;

            float contentHeight = 150f + buffs.Count * (RowHeight + RowGap) + 70f;
            if (onScreen)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 80;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                // The game dims behind the choice, as in Hades.
                Image dim = Graphic<Image>(root, "Dim");
                Stretch(dim.rectTransform);
                dim.color = new Color(0f, 0f, 0f, 0.62f);
            }
            else
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 80;
                root.sizeDelta = new Vector2(Width + 80f, contentHeight + 40f);
                root.localScale = Vector3.one * theme.pixelSize;
                PlaceInWorld();

                // A dark backing so the rows read against bright fire or sky.
                Image backing = Graphic<Image>(root, "Backing");
                Stretch(backing.rectTransform);
                backing.sprite = theme.panelFill;
                backing.type = Image.Type.Sliced;
                backing.color = new Color(0.03f, 0.025f, 0.02f, 0.82f);
            }

            // Everything sits in one centred column.
            var column = new GameObject("Column", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(root, false);
            column.sizeDelta = new Vector2(Width, contentHeight);

            TextMeshProUGUI header = Label(column, "Header", "Choose a Blessing", TextStyle.Title, 64f, theme.title, TextAlignmentOptions.Center);
            header.characterSpacing = 6f;
            Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(Width, 80f));
            TextMeshProUGUI sub = Label(column, "Subheader", "The fallen totem's power answers you. Take one.", TextStyle.Soft, 28f, theme.header, TextAlignmentOptions.Center);
            sub.fontStyle = FontStyles.Italic;
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(Width, 40f));
            Image rule = Graphic<Image>(column, "Rule");
            rule.sprite = theme.fadeLine;
            rule.color = theme.border;
            Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(Width * 0.8f, 2f));

            rowsRoot = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            rowsRoot.SetParent(column, false);
            Place(rowsRoot, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(Width, buffs.Count * (RowHeight + RowGap)));
            for (int i = 0; i < buffs.Count; i++) rows.Add(BuildRow(buffs[i], i, theme));

            string how = onScreen ? "Click a blessing, or press <color=#FFD25A>1</color> - <color=#FFD25A>" + buffs.Count + "</color>"
                                  : "Point at a blessing and pull the trigger";
            TextMeshProUGUI prompt = Label(column, "Prompt", how, TextStyle.Soft, 26f, theme.disabledText, TextAlignmentOptions.Center);
            Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(Width, 40f));

            Animate(0f);
        }

        Row BuildRow(BuffDefinition buff, int index, UiTheme theme)
        {
            Color rarity = theme.RarityColor(buff.rarity);
            var rect = new GameObject("Blessing " + (index + 1) + " " + buff.displayName, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            rect.SetParent(rowsRoot, false);
            Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -index * (RowHeight + RowGap)), new Vector2(Width, RowHeight));
            var row = new Row { Buff = buff, Rect = rect, Group = rect.GetComponent<CanvasGroup>(), RarityColor = rarity };

            row.Back = Sliced(rect, "Back", theme.panelFill, new Color(0.07f, 0.06f, 0.05f, 0.94f));
            Stretch(row.Back.rectTransform);
            row.Frame = Sliced(rect, "Frame", theme.panelFrame, Faded(rarity, 0.55f));
            Stretch(row.Frame.rectTransform);
            // A bar down the left edge in the rarity colour; it glows when the row is pointed at.
            row.Accent = Sliced(rect, "Accent", theme.panelFill, rarity);
            Place(row.Accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, RowHeight - 28f));

            // The icon: a framed square in the blessing's own colour with its initial, Hades' god emblem stand-in.
            var icon = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
            icon.SetParent(rect, false);
            Place(icon, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(Icon, Icon));
            Image iconFill = Sliced(icon, "Fill", theme.panelFill, Deep(buff.color));
            Stretch(iconFill.rectTransform);
            Image iconFrame = Sliced(icon, "Frame", theme.panelFrame, rarity);
            Stretch(iconFrame.rectTransform);
            string initial = string.IsNullOrEmpty(buff.displayName) ? "?" : buff.displayName.Substring(0, 1).ToUpperInvariant();
            TextMeshProUGUI glyph = Label(icon, "Initial", initial, TextStyle.Title, 78f, Bright(buff.color), TextAlignmentOptions.Center);
            Stretch(glyph.rectTransform);

            float textLeft = 28f + Icon + 28f;
            float textWidth = Width - textLeft - 30f;
            TextMeshProUGUI title = Label(rect, "Name", buff.displayName, TextStyle.Title, 44f, theme.title, TextAlignmentOptions.Left);
            title.characterSpacing = 2f;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -18f), new Vector2(textWidth - 200f, 54f));
            TextMeshProUGUI tag = Label(rect, "Rarity", buff.rarity.ToString(), TextStyle.Title, 24f, rarity, TextAlignmentOptions.Right);
            tag.characterSpacing = 6f;
            Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -26f), new Vector2(220f, 34f));
            if (onScreen)
            {
                // The number key that picks this row.
                TextMeshProUGUI key = Label(rect, "Key", (index + 1).ToString(), TextStyle.Title, 22f, Faded(theme.header, 0.5f), TextAlignmentOptions.Right);
                Place(key.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 14f), new Vector2(60f, 30f));
            }

            TextMeshProUGUI description = Label(rect, "Description", Highlight(buff.description, theme.highlight), TextStyle.Plain, 32f, theme.header, TextAlignmentOptions.TopLeft);
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Ellipsis;
            Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -76f), new Vector2(textWidth, RowHeight - 90f));
            return row;
        }

        /// <summary>Numbers and percentages in gold and bold, the way Hades picks out a boon's values.</summary>
        static string Highlight(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return Numbers.Replace(text, m => "<color=#" + hex + "><b>" + m.Value + "</b></color>");
        }

        void PlaceInWorld()
        {
            var cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.up * 1.6f;
            transform.position = eye + forward * WorldDistance + Vector3.down * 0.12f;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // ---------------------------------------------------------------- Choosing

        void Update()
        {
            var cam = Camera.main;
            Row pointed = null;
            bool select = false;

            if (onScreen)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 point = mouse.position.ReadValue();
                    foreach (Row row in rows)
                        if (RectTransformUtility.RectangleContainsScreenPoint(row.Rect, point, null)) pointed = row;
                    select = mouse.leftButton.wasPressedThisFrame;
                }
                // Number keys pick directly.
                var keyboard = Keyboard.current;
                if (keyboard != null)
                    for (int i = 0; i < rows.Count && i < 9; i++)
                        if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame || keyboard[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame)
                        {
                            pointed = rows[i];
                            select = true;
                        }
            }
            else if (cam != null)
            {
                for (int i = 0; i < hands.Length; i++)
                {
                    PhysicsHand hand = hands[i];
                    if (hand == null) continue;
                    // The world is frozen, so the physics hand may lag: point with the real controller instead.
                    Transform pointer = hand.Target != null ? hand.Target : hand.transform;
                    var ray = new Ray(pointer.position, pointer.forward);
                    Row row = RowUnder(ray, out float distance);
                    DrawBeam(i, ray, row != null ? distance : 1.5f, row != null);
                    if (row == null) continue;

                    pointed = row;
                    InputAction trigger = hand.HandNode == UnityEngine.XR.XRNode.LeftHand ? leftSelect : rightSelect;
                    if (trigger.WasPressedThisFrame())
                    {
                        select = true;
                        hand.Haptic(0.6f, 0.08f);
                        break;
                    }
                }
            }

            if (pointed != hovered && pointed != null) Audio.Sfx.PlayUi(Audio.SfxKind.UiHover, 0.4f);
            hovered = pointed;
            Animate(Time.unscaledTime - openedAt);

            // A short pause after appearing, so the swing or press that broke the totem cannot pick a blessing.
            if (!select || hovered == null || Time.unscaledTime - openedAt < PickDelay) return;

            BuffDefinition chosen = hovered.Buff;
            Action<BuffDefinition> callback = onChosen;
            Destroy(gameObject);
            // Let the world move again right away so anything the callback starts runs normally.
            Combat.WorldFreeze.Release(this);
            current = null;
            DesktopRig.InputBlocked = false;
            callback?.Invoke(chosen);
        }

        /// <summary>Rows slide in from the left one after another; the pointed-at row brightens and nudges right.</summary>
        void Animate(float age)
        {
            whole.alpha = Mathf.Clamp01(age / 0.2f);
            float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                float t = Mathf.Clamp01((age - i * Stagger) / SlideIn);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                row.Hover = Mathf.Lerp(row.Hover, row == hovered ? 1f : 0f, age <= 0f ? 1f : blend);

                row.Group.alpha = eased;
                row.Rect.anchoredPosition = new Vector2(-60f * (1f - eased) + 14f * row.Hover, -i * (RowHeight + RowGap));
                row.Back.color = Color.Lerp(new Color(0.07f, 0.06f, 0.05f, 0.94f), new Color(0.16f, 0.13f, 0.09f, 0.97f), row.Hover);
                row.Frame.color = Faded(row.RarityColor, Mathf.Lerp(0.55f, 1f, row.Hover));
                row.Accent.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(8f, 12f, row.Hover), RowHeight - 28f);
            }
        }

        Row RowUnder(Ray ray, out float distance)
        {
            distance = 0f;
            var plane = new Plane(-transform.forward, transform.position);
            if (!plane.Raycast(ray, out float enter) || enter > 8f) return null;
            Vector3 hit = ray.GetPoint(enter);
            foreach (Row row in rows)
            {
                Vector2 local = row.Rect.InverseTransformPoint(hit);
                if (!row.Rect.rect.Contains(local)) continue;
                distance = enter;
                return row;
            }
            return null;
        }

        void CreateBeams()
        {
            beams = new LineRenderer[hands.Length];
            for (int i = 0; i < hands.Length; i++)
            {
                var go = new GameObject("Blessing Pointer Beam");
                var beam = go.AddComponent<LineRenderer>();
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = 0.006f;
                beam.endWidth = 0.002f;
                beam.sharedMaterial = Combat.FlatMaterials.Shared;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beams[i] = beam;
            }
        }

        void DrawBeam(int index, Ray ray, float length, bool onRow)
        {
            if (beams == null || index >= beams.Length || beams[index] == null) return;
            LineRenderer beam = beams[index];
            Color color = onRow ? new Color(1f, 0.85f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.25f);
            beam.startColor = color;
            beam.endColor = color;
            beam.SetPosition(0, ray.origin);
            beam.SetPosition(1, ray.origin + ray.direction * length);
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>The blessing's colour, darkened for an icon background that its bright initial reads on.</summary>
        static Color Deep(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            Color deep = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.9f), Mathf.Min(v, 0.32f));
            deep.a = 1f;
            return deep;
        }

        /// <summary>The blessing's colour, lifted so it reads on the dark icon (deep reds included).</summary>
        static Color Bright(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            Color bright = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.7f), Mathf.Max(v, 0.95f));
            bright.a = 1f;
            return bright;
        }

        static Color Faded(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static T Graphic<T>(Transform parent, string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        static Image Sliced(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = Graphic<Image>(parent, name);
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, TextStyle style, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = GameText.Apply(Graphic<TextMeshProUGUI>(parent, name), style);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            return label;
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
