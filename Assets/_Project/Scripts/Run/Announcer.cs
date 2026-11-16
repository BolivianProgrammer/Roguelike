using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.UI;

namespace Roguelike.Run
{
    /// <summary>
    /// Big messages for the player ("Totem destroyed", "You died"): a banner floating a couple of meters ahead in
    /// the world, which works in the headset and on a monitor alike. The first line of a message is the title (spaced
    /// small capitals with a dark halo); any further lines are a smaller subtitle. Behind them a dark band fades out
    /// at its ends, between two hairline rules in the message's colour that sweep open as it appears.
    ///
    /// One banner is built per scene and reused; a new message replaces the one showing.
    /// </summary>
    public class Announcer : MonoBehaviour
    {
        const float Width = 1900f;     // canvas pixels (1 px = 1 mm in the world)
        const float RuleWidth = 1300f;
        const float FadeIn = 0.25f;
        const float FadeOut = 0.6f;
        const float Distance = 2.2f;

        static Announcer current;

        CanvasGroup group;
        RectTransform band, ruleTop, ruleBottom;
        Image ruleTopImage, ruleBottomImage;
        TextMeshProUGUI title, subtitle;
        float shownAt, endTime;
        bool snapNext;

        /// <summary>Shows a message in front of the player for a few seconds, replacing any message still showing.</summary>
        public static void Show(string message, Color color, float seconds = 3.5f)
        {
            if (Camera.main == null || string.IsNullOrEmpty(message)) return;
            if (current == null) current = Build();
            current.Present(message, color, seconds);
        }

        public static void Show(string message) => Show(message, Color.white);

        void Present(string message, Color color, float seconds)
        {
            bool wasShowing = gameObject.activeSelf && Time.unscaledTime < endTime;
            gameObject.SetActive(true);

            int split = message.IndexOf('\n');
            string head = split < 0 ? message : message.Substring(0, split);
            string rest = split < 0 ? "" : message.Substring(split + 1);

            // The title takes the message colour, lifted a little so dark reds and violets still read at night.
            title.color = Color.Lerp(color, Color.white, 0.2f);
            title.text = head;
            subtitle.text = rest;
            subtitle.gameObject.SetActive(rest.Length > 0);

            Color rule = color;
            rule.a = 0.9f;
            ruleTopImage.color = rule;
            ruleBottomImage.color = rule;

            // Lay out: title, then subtitle, the band and the rules around them.
            float titleHeight = title.GetPreferredValues(head, Width - 160f, 0f).y;
            float subtitleHeight = rest.Length > 0 ? subtitle.GetPreferredValues(rest, Width - 220f, 0f).y + 14f : 0f;
            float content = titleHeight + subtitleHeight;
            float top = content * 0.5f;
            title.rectTransform.anchoredPosition = new Vector2(0f, top - titleHeight * 0.5f);
            title.rectTransform.sizeDelta = new Vector2(Width - 160f, titleHeight);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, top - titleHeight - subtitleHeight * 0.5f);
            subtitle.rectTransform.sizeDelta = new Vector2(Width - 220f, subtitleHeight);
            band.sizeDelta = new Vector2(Width, content + 90f);
            ruleTop.anchoredPosition = new Vector2(0f, top + 30f);
            ruleBottom.anchoredPosition = new Vector2(0f, -top - 30f);

            // A message replacing one still on screen keeps its place instead of fading in again from nothing.
            shownAt = wasShowing ? Time.unscaledTime - FadeIn : Time.unscaledTime;
            endTime = Time.unscaledTime + seconds;
            snapNext = !wasShowing;
            LateUpdate();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Drift after the player's view instead of being glued to it, which is uncomfortable in a headset.
            Follow(cam, snapNext ? 1f : 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            snapNext = false;

            float age = Time.unscaledTime - shownAt;
            float remaining = endTime - Time.unscaledTime;
            float appear = Mathf.Clamp01(age / FadeIn);
            float eased = 1f - (1f - appear) * (1f - appear);
            group.alpha = Mathf.Min(eased, Mathf.Clamp01(remaining / FadeOut));
            transform.localScale = Vector3.one * (UiTheme.Instance.pixelSize * Mathf.Lerp(0.94f, 1f, eased));

            // The rules sweep open from the middle.
            float sweep = Mathf.Clamp01(age / (FadeIn * 1.6f));
            float ruleWidth = RuleWidth * (1f - (1f - sweep) * (1f - sweep));
            ruleTop.sizeDelta = new Vector2(ruleWidth, 3f);
            ruleBottom.sizeDelta = new Vector2(ruleWidth, 3f);

            if (remaining <= 0f) gameObject.SetActive(false);
        }

        void Follow(Camera cam, float blend)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = cam.transform.up;
            forward.Normalize();

            Vector3 goal = cam.transform.position + forward * Distance + Vector3.up * 0.25f;
            transform.position = Vector3.Lerp(transform.position, goal, blend);
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        // ---------------------------------------------------------------- Construction

        static Announcer Build()
        {
            UiTheme theme = UiTheme.Instance;
            var go = new GameObject("Announcement", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 90;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(Width, 400f);
            rect.localScale = Vector3.one * theme.pixelSize;

            var announcer = go.AddComponent<Announcer>();
            announcer.group = go.GetComponent<CanvasGroup>();
            announcer.group.interactable = false;
            announcer.group.blocksRaycasts = false;

            Image bandImage = Graphic<Image>(rect, "Band");
            bandImage.sprite = theme.fadeBand;
            bandImage.color = theme.band;
            announcer.band = bandImage.rectTransform;

            announcer.ruleTopImage = Graphic<Image>(rect, "Rule Top");
            announcer.ruleTopImage.sprite = theme.fadeLine;
            announcer.ruleTop = announcer.ruleTopImage.rectTransform;
            announcer.ruleBottomImage = Graphic<Image>(rect, "Rule Bottom");
            announcer.ruleBottomImage.sprite = theme.fadeLine;
            announcer.ruleBottom = announcer.ruleBottomImage.rectTransform;

            announcer.title = GameText.Apply(Graphic<TextMeshProUGUI>(rect, "Title"), TextStyle.Banner);
            announcer.title.fontSize = 124f;
            announcer.title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            announcer.title.characterSpacing = 6f;
            announcer.title.alignment = TextAlignmentOptions.Center;
            announcer.title.enableWordWrapping = true;

            announcer.subtitle = GameText.Apply(Graphic<TextMeshProUGUI>(rect, "Subtitle"), TextStyle.Soft);
            announcer.subtitle.fontSize = 58f;
            announcer.subtitle.fontStyle = FontStyles.Italic;
            announcer.subtitle.color = theme.header;
            announcer.subtitle.alignment = TextAlignmentOptions.Center;
            announcer.subtitle.enableWordWrapping = true;

            go.SetActive(false);
            return announcer;
        }

        static T Graphic<T>(RectTransform parent, string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = Vector2.zero;
            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }
    }
}
