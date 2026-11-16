using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Roguelike.Player;

namespace Roguelike.UI
{
    /// <summary>
    /// "Walk up and press a button" for things in the world (chests, challenge shrines): when the player is close,
    /// a label floats over the object; pressing E (desktop) or A / X (controllers) uses it. The same rule as
    /// talking to people in the hub: nothing has to be touched or hit.
    /// </summary>
    public class InteractPrompt : MonoBehaviour
    {
        TextMeshPro label;
        InputAction use;
        float range = 2.2f;
        float height = 1.4f;
        string text = "Use";
        bool available = true;

        /// <summary>Raised when the player uses the object.</summary>
        public event System.Action Used;

        /// <summary>The object closest to the player that can be used right now, so one press uses only one thing.</summary>
        public static InteractPrompt Focused { get; private set; }

        public static InteractPrompt Add(GameObject target, string labelText, float useRange = 2.2f, float labelHeight = 1.4f)
        {
            var prompt = target.AddComponent<InteractPrompt>();
            prompt.text = labelText;
            prompt.range = useRange;
            prompt.height = labelHeight;
            return prompt;
        }

        /// <summary>Changes the label, e.g. to show a challenge's countdown. Unavailable hides the prompt.</summary>
        public void Set(string labelText, bool canUse)
        {
            text = labelText;
            available = canUse;
        }

        void Awake()
        {
            use = new InputAction("Use", InputActionType.Button);
            use.AddBinding("<Keyboard>/e");
            use.AddBinding("<XRController>/{PrimaryButton}");
            use.Enable();

            var go = new GameObject("Prompt");
            go.transform.SetParent(transform, false);
            label = GameText.Apply(go.AddComponent<TextMeshPro>(), TextStyle.Soft);
            label.rectTransform.sizeDelta = new Vector2(1.6f, 0.3f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.1f;
            label.fontSizeMax = 0.9f;
            label.color = new Color(0.95f, 0.9f, 0.8f);
            go.SetActive(false);
        }

        void OnDestroy()
        {
            use?.Disable();
            if (Focused == this) Focused = null;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 offset = cam.transform.position - transform.position;
            offset.y = 0f;
            bool near = offset.magnitude <= range;

            if (near && available && (Focused == null || Focused == this || DistanceOf(Focused, cam) > offset.magnitude)) Focused = this;
            else if (Focused == this && (!near || !available)) Focused = null;

            bool show = near && (Focused == this || !available) && !WorldMenu.IsOpen;
            if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            if (show)
            {
                label.text = available ? Format(text, DesktopRig.IsDesktop ? "E" : "A / X") : Format(text, null);
                label.transform.position = transform.position + Vector3.up * height;
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, Vector3.up);
            }

            if (Focused == this && available && use.WasPressedThisFrame() && !WorldMenu.IsOpen && !PauseMenu.IsPaused) Used?.Invoke();
        }

        static float DistanceOf(InteractPrompt other, Camera cam)
        {
            Vector3 offset = cam.transform.position - other.transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>
        /// The look of every "walk up and press" label: the name in bold over a dark strip, the button in gold below.
        /// Shared with the hub characters so all prompts match.
        /// </summary>
        public static string Format(string title, string button, string action = null)
        {
            string line = "<mark=#0E0B08D9><b> " + title + " </b></mark>";
            if (string.IsNullOrEmpty(button)) return line;
            string hint = "[" + button + "]" + (string.IsNullOrEmpty(action) ? "" : "  " + action);
            return line + "\n<size=65%><mark=#0E0B08D9><color=#FFD25A> " + hint + " </color></mark></size>";
        }
    }
}
