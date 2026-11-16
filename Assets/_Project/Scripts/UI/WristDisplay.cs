using TMPro;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Gear;
using Roguelike.Meta;
using Roguelike.Player;
using Roguelike.Run;
using Roguelike.Weapons;

namespace Roguelike.UI
{
    /// <summary>
    /// The player's status, readable in the headset: turn the back of the off-hand wrist towards your face and a
    /// small panel fades in with health, stamina, the held weapon's special, the objective and the run's loot
    /// (in the hub: level, currencies and worn gear). Health and stamina also glow faintly as two thin bars at
    /// all times, so a glance is enough mid-fight. On a monitor the same information is drawn on screen.
    /// </summary>
    public class WristDisplay : MonoBehaviour
    {
        const float Width = 0.16f;
        const float Height = 0.12f;

        PhysicsHand hand;
        PhysicsHand weaponHand;
        Transform panel;
        TextMeshPro text;
        Transform healthBar, staminaBar;
        MeshRenderer[] faded;
        float visibility;
        float nextText;

        /// <summary>Puts the display on the off hand (left, or right for left-handed players).</summary>
        public static WristDisplay Attach()
        {
            PhysicsHand left = null, right = null;
            foreach (var h in FindObjectsOfType<PhysicsHand>())
            {
                if (h.HandNode == UnityEngine.XR.XRNode.LeftHand) left = h;
                else right = h;
            }
            bool leftHanded = GameSettings.Current.leftHanded;
            PhysicsHand off = leftHanded ? right : left;
            if (off == null) return null;

            var display = off.gameObject.AddComponent<WristDisplay>();
            display.hand = off;
            display.weaponHand = leftHanded ? left : right;
            display.Build();
            return display;
        }

        void Build()
        {
            panel = new GameObject("Wrist Display").transform;
            panel.SetParent(hand.Target != null ? hand.Target : hand.transform, false);
            // On the back of the wrist, a little towards the elbow, facing out of the back of the hand.
            panel.localPosition = new Vector3(0f, 0.045f, -0.09f);
            panel.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var back = Quad("Back", Vector3.forward * 0.002f, new Vector2(Width, Height), new Color(0.03f, 0.03f, 0.04f, 0.9f));
            healthBar = Quad("Health", new Vector3(0f, Height * 0.5f - 0.012f, 0f), new Vector2(Width - 0.01f, 0.008f), new Color(0.85f, 0.15f, 0.12f)).transform;
            staminaBar = Quad("Stamina", new Vector3(0f, Height * 0.5f - 0.024f, 0f), new Vector2(Width - 0.01f, 0.006f), new Color(0.95f, 0.8f, 0.25f)).transform;

            var go = new GameObject("Text");
            go.transform.SetParent(panel, false);
            go.transform.localPosition = new Vector3(0f, -0.012f, -0.001f);
            text = GameText.Apply(go.AddComponent<TextMeshPro>(), TextStyle.Plain);
            text.rectTransform.sizeDelta = new Vector2(Width - 0.012f, Height - 0.03f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 0.12f;
            text.color = new Color(0.92f, 0.9f, 0.84f);

            faded = new[] { back };
        }

        MeshRenderer Quad(string name, Vector3 localPosition, Vector2 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(panel, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var quadRenderer = go.GetComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = FlatMaterials.Shared;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            quadRenderer.SetPropertyBlock(block);
            return quadRenderer;
        }

        void LateUpdate()
        {
            if (panel == null) return;
            // On a monitor the status is on the screen HUD instead.
            if (panel.gameObject.activeSelf == DesktopRig.IsDesktop) panel.gameObject.SetActive(!DesktopRig.IsDesktop);
            if (DesktopRig.IsDesktop)
            {
                UpdateScreenVitals();
                return;
            }
            var cam = Camera.main;
            if (cam == null) return;

            // Fully shown when the panel faces the eyes; otherwise only the two thin bars stay faintly visible.
            Vector3 toEyes = (cam.transform.position - panel.position).normalized;
            float facing = Vector3.Dot(-panel.forward, toEyes);
            float goal = DesktopRig.IsDesktop ? 0f : Mathf.InverseLerp(0.55f, 0.8f, facing);
            visibility = Mathf.MoveTowards(visibility, goal, Time.unscaledDeltaTime * 5f);

            var player = PlayerHealth.Instance;
            float health = player != null ? Mathf.Clamp01(player.Current / Mathf.Max(1f, player.Max)) : 1f;
            float stamina = PlayerStamina.Exists ? Mathf.Clamp01(PlayerStamina.Current / Mathf.Max(1f, PlayerStamina.Max)) : 1f;
            SetBar(healthBar, health);
            SetBar(staminaBar, stamina);

            bool show = visibility > 0.02f;
            text.gameObject.SetActive(show);
            foreach (var r in faded) r.enabled = show;
            if (show && Time.unscaledTime >= nextText)
            {
                nextText = Time.unscaledTime + 0.2f;
                text.text = Status(player);
                text.alpha = visibility;
            }
            else if (show) text.alpha = visibility;
        }

        static void SetBar(Transform bar, float fill)
        {
            Vector3 scale = bar.localScale;
            float full = Width - 0.01f;
            scale.x = Mathf.Max(0.0001f, full * fill);
            bar.localScale = scale;
            Vector3 position = bar.localPosition;
            position.x = -full * 0.5f + scale.x * 0.5f;
            bar.localPosition = position;
        }

        /// <summary>The lines of the display: the same text on the wrist and on a monitor.</summary>
        string Status(PlayerHealth player)
        {
            var lines = new System.Text.StringBuilder();
            if (player != null) lines.Append("<b>" + Mathf.CeilToInt(player.Current) + "</b> / " + Mathf.CeilToInt(player.Max) + " health   ");
            if (PlayerStamina.Exists) lines.Append(Mathf.CeilToInt(PlayerStamina.Current) + " stamina");
            lines.Append('\n');

            string special = SpecialStatus(weaponHand);
            if (special != null) lines.Append(special).Append('\n');

            if (MissionManager.Current != null)
            {
                lines.Append("<color=#C9B6FF>" + MissionManager.Objective + "</color>\n");
                if (!string.IsNullOrEmpty(KillChallenge.ActiveText)) lines.Append("<color=#FF9A7A>" + KillChallenge.ActiveText + "</color>\n");
                lines.Append("Essence " + RunState.Essence + "   Coins " + RunState.Coins + "   Kills " + RunState.Kills);
                if (MissionManager.FoundGear.Count > 0) lines.Append("\nFound: " + string.Join(", ", MissionManager.FoundGear.ConvertAll(ItemDatabase.NameOf)));
            }
            else
            {
                SaveData data = SaveSystem.Data;
                lines.Append("Level " + data.level + "   Essence " + data.essence + "   Coins " + data.coins + "\n");
                ArmorDefinition armor = Equipment.Armor;
                lines.Append(armor != null ? armor.displayName : "No armor");
                if (data.trinkets.Count > 0) lines.Append(", " + string.Join(", ", data.trinkets.ConvertAll(ItemDatabase.NameOf)));
            }
            return lines.ToString();
        }

        static string SpecialStatus(PhysicsHand weaponHand)
        {
            if (weaponHand == null) return null;
            var grabber = weaponHand.GetComponent<PhysicsHandGrabber>();
            var weapon = grabber != null && grabber.Held != null ? grabber.Held.GetComponent<Weapon>() : null;
            if (weapon == null || weapon.Special == null) return null;
            var special = weapon.Special;
            string state = special.IsActive ? "<color=#FFD24D>ACTIVE</color>" : special.IsReady ? "ready" : Mathf.CeilToInt(special.CooldownRemaining) + "s";
            return weapon.Definition.displayName + " - " + special.DisplayName + ": " + state;
        }

        /// <summary>On a monitor: health and stamina on the screen HUD.</summary>
        static void UpdateScreenVitals()
        {
            DesktopHud hud = DesktopHud.Get();
            if (hud == null) return;
            var player = PlayerHealth.Instance;
            float health = player != null ? player.Current / Mathf.Max(1f, player.Max) : 1f;
            float stamina = PlayerStamina.Exists ? PlayerStamina.Current / Mathf.Max(1f, PlayerStamina.Max) : 1f;
            hud.SetVitals(health, stamina, player != null ? player.Current : 0f, player != null ? player.Max : 0f);
        }
    }
}
