using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Roguelike.Player;
using Roguelike.UI;

namespace Roguelike.Hub
{
    /// <summary>
    /// A character standing in the hub that the player can talk to. Walk up and look at them, and a prompt appears
    /// over their head; press the talk button (E on keyboard, A or X on the controllers) and they open their menu.
    /// What the menu contains is up to whoever listens to <see cref="Interacted"/>.
    /// </summary>
    public class HubNpc : MonoBehaviour
    {
        /// <summary>How directly the player must look at a character to talk: cos 30 degrees off the centre of view.</summary>
        const float LookThreshold = 0.866f;

        [SerializeField] string displayName = "Stranger";
        [Tooltip("How close (meters) the player must be to talk.")]
        [SerializeField] float talkRange = 2.8f;
        [Tooltip("How fast the NPC turns to face a nearby player, in degrees per second.")]
        [SerializeField] float turnSpeed = 120f;
        [SerializeField] Animator animator;
        [Tooltip("Animator state played when the player starts talking. Empty = none.")]
        [SerializeField] string greetState = "Npc_Greet";
        [Tooltip("Animator state looped while this character's menu is open. Empty = none.")]
        [SerializeField] string talkState = "Npc_Talk";
        [SerializeField] string idleState = "Npc_Idle";
        [SerializeField] float promptHeight = 2.15f;

        TextMeshPro prompt;
        InputAction talk;
        Quaternion restRotation;
        bool talking;
        float busyUntil;
        float lookScore = -1f;

        /// <summary>The NPC the player is close to and looking at right now, if any.</summary>
        public static HubNpc Focused { get; private set; }

        public string DisplayName => displayName;

        /// <summary>Raised when the player presses the talk button next to this NPC.</summary>
        public event Action<HubNpc> Interacted;

        void Awake()
        {
            restRotation = transform.rotation;

            talk = new InputAction("Talk", InputActionType.Button);
            talk.AddBinding("<Keyboard>/e");
            talk.AddBinding("<XRController>/{PrimaryButton}");

            // The model's wound caps are for dismemberment; a friendly NPC never shows them.
            foreach (var capRenderer in GetComponentsInChildren<Renderer>(true))
                if (capRenderer.name.StartsWith("Cap_") || capRenderer.name.Contains("Clip_Anchor")) capRenderer.enabled = false;

            CreatePrompt();
        }

        void OnEnable() => talk.Enable();

        void OnDisable()
        {
            talk.Disable();
            if (Focused == this) Focused = null;
        }

        /// <summary>Plays a one-off gesture (trade, bless), which returns to idle by itself.</summary>
        public void Play(string state, float seconds = 2f)
        {
            if (animator == null || !HasState(state)) return;
            animator.CrossFadeInFixedTime(state, 0.15f);
            busyUntil = Time.time + seconds;
            talking = false;
        }

        bool HasState(string state) =>
            !string.IsNullOrEmpty(state) && animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash(state));

        void CreatePrompt()
        {
            var go = new GameObject("Talk Prompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * promptHeight;

            prompt = UI.GameText.Apply(go.AddComponent<TextMeshPro>(), UI.TextStyle.Soft);
            prompt.rectTransform.sizeDelta = new Vector2(1.6f, 0.3f);
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.enableWordWrapping = false;
            prompt.enableAutoSizing = true;
            prompt.fontSizeMin = 0.1f;
            prompt.fontSizeMax = 1f;
            prompt.color = new Color(0.95f, 0.9f, 0.8f);
            go.SetActive(false);
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 toPlayer = cam.transform.position - transform.position;
            toPlayer.y = 0f;
            bool near = toPlayer.magnitude <= talkRange;

            // Turn towards a nearby player, and back to where they were facing once the player leaves.
            Quaternion goal = near && toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer, Vector3.up) : restRotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * Time.deltaTime);

            // Talking needs the player to be close AND looking at this character; of several in view, the one
            // nearest the middle of the view wins.
            Vector3 toChest = transform.position + Vector3.up * 1.3f - cam.transform.position;
            lookScore = near ? Vector3.Dot(cam.transform.forward, toChest.normalized) : -1f;
            bool looked = lookScore >= LookThreshold;
            // While their menu is open the player looks at the menu, not the character: keep the conversation.
            bool keep = near && WorldMenu.IsOpen;
            if (looked && (Focused == null || Focused == this || Focused.lookScore < LookThreshold || lookScore > Focused.lookScore))
                Focused = this;
            else if (Focused == this && !looked && !keep)
                Focused = null;
            bool focused = Focused == this;

            bool showPrompt = focused && !WorldMenu.IsOpen;
            if (prompt.gameObject.activeSelf != showPrompt) prompt.gameObject.SetActive(showPrompt);
            if (showPrompt)
            {
                prompt.text = InteractPrompt.Format(displayName, DesktopRig.IsDesktop ? "E" : "A / X", "Talk");
                prompt.transform.rotation = Quaternion.LookRotation(prompt.transform.position - cam.transform.position, Vector3.up);
            }

            // Gesture while their menu is open; back to idle when it closes.
            bool shouldTalk = focused && WorldMenu.IsOpen;
            if (animator != null && shouldTalk != talking && Time.time >= busyUntil && HasState(shouldTalk ? talkState : idleState))
            {
                talking = shouldTalk;
                animator.CrossFadeInFixedTime(shouldTalk ? talkState : idleState, 0.3f);
            }

            if (!focused || !talk.WasPressedThisFrame()) return;

            // The same button closes the menu again.
            if (WorldMenu.IsOpen)
            {
                WorldMenu.Close();
                return;
            }

            Play(greetState, 1.2f);
            Interacted?.Invoke(this);
        }
    }
}
