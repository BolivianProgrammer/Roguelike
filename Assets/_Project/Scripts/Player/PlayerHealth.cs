using System;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.Combat;

namespace Roguelike.Player
{
    /// <summary>
    /// The player's health. Getting hit shakes both controllers and flashes the view red.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Seconds after a hit during which further hits are ignored.")]
        [SerializeField] float invulnerabilityTime = 0.4f;
        [Tooltip("Test-scene behaviour: come back at full health this many seconds after dying. 0 = stay dead.")]
        [SerializeField] float reviveDelay = 2f;
        [SerializeField] Transform head;
        [SerializeField] PhysicsHand[] hands;
        [Tooltip("Optional: a health number is shown at this transform, e.g. on the back of the left hand.")]
        [SerializeField] Transform healthLabelAnchor;

        float invulnerableUntil;
        float flashAlpha;
        Image flash;
        TextMesh label;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Revived;

        void Awake()
        {
            Current = maxHealth;
        }

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head != null) CreateFlash();
            if (healthLabelAnchor != null) CreateLabel();
            RefreshLabel();
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || Time.time < invulnerableUntil || info.Amount <= 0f) return;

            invulnerableUntil = Time.time + invulnerabilityTime;
            Current = Mathf.Max(0f, Current - info.Amount);
            flashAlpha = 0.55f;
            Haptic(1f, 0.2f);
            RefreshLabel();
            Damaged?.Invoke(info);

            if (Current <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            RefreshLabel();
        }

        void Die()
        {
            IsDead = true;
            Died?.Invoke();
            Debug.Log("[Player] Died");
            if (reviveDelay > 0f) Invoke(nameof(Revive), reviveDelay);
        }

        public void Revive()
        {
            IsDead = false;
            Current = maxHealth;
            RefreshLabel();
            Revived?.Invoke();
        }

        void Update()
        {
            if (flash == null) return;
            float target = IsDead ? 0.8f : 0f;
            flashAlpha = Mathf.MoveTowards(flashAlpha, target, Time.deltaTime * 1.5f);
            var color = flash.color;
            color.a = flashAlpha;
            flash.color = color;
            flash.enabled = flashAlpha > 0.001f;
        }

        void Haptic(float amplitude, float duration)
        {
            if (hands == null) return;
            foreach (var hand in hands)
                if (hand != null) hand.Haptic(amplitude, duration);
        }

        void CreateFlash()
        {
            var go = new GameObject("Hit Flash", typeof(Canvas));
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.SetParent(head, false);
            rect.sizeDelta = new Vector2(4000f, 4000f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0f, 0f, 0.4f);
            rect.localRotation = Quaternion.identity;

            var imageGo = new GameObject("Flash", typeof(Image));
            var imageRect = (RectTransform)imageGo.transform;
            imageRect.SetParent(rect, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            flash = imageGo.GetComponent<Image>();
            flash.raycastTarget = false;
            flash.color = new Color(0.7f, 0f, 0f, 0f);
            flash.enabled = false;
        }

        void CreateLabel()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Health Label");
            go.transform.SetParent(healthLabelAnchor, false);

            label = go.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 64;
            label.characterSize = 0.004f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        void RefreshLabel()
        {
            if (label == null) return;
            label.text = Mathf.CeilToInt(Current).ToString();
            label.color = Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.4f, 1f, 0.5f), Current / maxHealth);
        }
    }
}
