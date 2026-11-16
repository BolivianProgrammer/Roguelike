using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Roguelike.Audio;
using Roguelike.Combat;
using Roguelike.Meta;

namespace Roguelike.UI
{
    /// <summary>
    /// The death or victory screen: a panel floating in front of the player with what the last run brought home
    /// (or cost). It drifts after the player's view, closes on any button press, and fades away on its own.
    /// </summary>
    public class RunSummaryPanel : MonoBehaviour
    {
        const float Width = 1.1f;
        const float Height = 0.9f;
        const float Lifetime = 20f;

        static RunSummaryPanel current;

        float openedAt;
        InputAction anyButton;

        public static void Show(RunSummary summary)
        {
            if (summary == null || string.IsNullOrEmpty(summary.outcome)) return;
            if (current != null) Destroy(current.gameObject);

            var go = new GameObject("Run Summary");
            current = go.AddComponent<RunSummaryPanel>();
            current.Build(summary);
        }

        void Awake()
        {
            openedAt = Time.unscaledTime;
            anyButton = new InputAction("Close Summary", InputActionType.Button);
            anyButton.AddBinding("<Keyboard>/anyKey");
            anyButton.AddBinding("<Mouse>/leftButton");
            anyButton.AddBinding("<XRController>/{TriggerButton}");
            anyButton.AddBinding("<XRController>/{PrimaryButton}");
            anyButton.Enable();
        }

        void OnDestroy()
        {
            anyButton?.Disable();
            if (current == this) current = null;
        }

        void Build(RunSummary summary)
        {
            Color accent;
            string title;
            switch (summary.outcome)
            {
                case "victory": title = "VICTORY"; accent = new Color(1f, 0.85f, 0.3f); break;
                case "extracted": title = "EXTRACTED"; accent = new Color(0.4f, 1f, 0.8f); break;
                case "abandoned": title = "RUN ABANDONED"; accent = new Color(1f, 0.45f, 0.2f); break;
                default: title = "YOU DIED"; accent = new Color(1f, 0.2f, 0.15f); break;
            }
            bool lost = summary.outcome == "died" || summary.outcome == "abandoned";

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(summary.mission)) lines.Add(summary.mission);
            if (summary.outcome != "abandoned")
            {
                lines.Add("Time " + summary.seconds / 60 + ":" + (summary.seconds % 60).ToString("00") + "    Kills " + summary.kills + "    Totems " + summary.totems
                    + (summary.bossKilled ? "    Guardian slain" : ""));
                lines.Add((lost ? "Lost: " : "Banked: ") + summary.essence + " essence, " + summary.coins + " coins");
            }
            if (summary.items != null && summary.items.Count > 0)
                lines.Add((lost ? "Lost: " : "Brought home: ") + string.Join(", ", summary.items.ConvertAll(ItemDatabase.NameOf)));
            if (summary.blessings != null && summary.blessings.Count > 0)
                lines.Add("Blessings: " + string.Join(", ", summary.blessings));
            if (summary.outcome == "abandoned") lines.Add("The game was closed during a mission. That counts as dying there.");
            lines.Add("<size=70%><color=#999>press any button</color></size>");

            Quad("Panel", Vector3.forward * 0.004f, new Vector2(Width, Height), new Color(0.04f, 0.04f, 0.05f, 0.92f));
            Quad("Band", new Vector3(0f, Height * 0.5f - 0.09f, 0.002f), new Vector2(Width, 0.18f), accent * 0.5f);
            Text("Title", title, new Vector3(0f, Height * 0.5f - 0.09f, 0f), new Vector2(Width - 0.06f, 0.14f), Color.white, 0.9f, TextAlignmentOptions.Center);
            Text("Body", string.Join("\n", lines), new Vector3(0f, -0.08f, 0f), new Vector2(Width - 0.1f, Height - 0.3f), new Color(0.92f, 0.88f, 0.8f), 0.42f, TextAlignmentOptions.Top);

            Sfx.PlayUi(lost ? SfxKind.UiBack : SfxKind.ChallengeEnd);
            Follow(1f);
        }

        void Quad(string name, Vector3 localPosition, Vector2 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var quadRenderer = go.GetComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = FlatMaterials.Shared;
            var block = new MaterialPropertyBlock();
            color.a = Mathf.Max(color.a, 0.9f);
            block.SetColor("_Color", color);
            quadRenderer.SetPropertyBlock(block);
        }

        void Text(string name, string content, Vector3 localPosition, Vector2 box, Color color, float maxSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition + Vector3.back * 0.002f;
            var text = GameText.Apply(go.AddComponent<TextMeshPro>(), TextStyle.Plain);
            text.rectTransform.sizeDelta = box;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = maxSize;
        }

        void LateUpdate()
        {
            Follow(1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            float age = Time.unscaledTime - openedAt;
            if (age > Lifetime || (age > 1f && anyButton.WasPressedThisFrame())) Destroy(gameObject);
        }

        /// <summary>Drifts after the player's view instead of being glued to it, which is uncomfortable in a headset.</summary>
        void Follow(float blend)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 goal = cam.transform.position + forward * 1.6f;
            transform.position = Vector3.Lerp(transform.position, goal, blend);
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
