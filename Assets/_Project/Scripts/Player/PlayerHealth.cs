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
        [SerializeField] Transform head;
        [SerializeField] PhysicsHand[] hands;
        [Tooltip("Optional: a health number is shown at this transform, e.g. on the back of the left hand.")]
        [SerializeField] Transform healthLabelAnchor;

        float invulnerableUntil;
        float lastMax;
        float flashAlpha;
        Image flash;
        TMPro.TextMeshPro label;
        int shownHealth = -1;

        /// <summary>Maximum health including bonuses from buffs and gear.</summary>
        public float Max => maxHealth * PlayerStats.Get(StatType.MaxHealth);
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        void Awake()
        {
            Instance = this;
            Current = maxHealth;
            lastMax = maxHealth;
            if (GetComponent<PlayerStamina>() == null) gameObject.AddComponent<PlayerStamina>();
        }

        void OnEnable() => PlayerStats.Changed += OnStatsChanged;
        void OnDisable() => PlayerStats.Changed -= OnStatsChanged;

        /// <summary>A max-health bonus also heals by the amount gained; a penalty trims health to the new cap.</summary>
        void OnStatsChanged()
        {
            float newMax = Max;
            if (newMax > lastMax && !IsDead) Current += newMax - lastMax;
            Current = Mathf.Min(Current, newMax);
            lastMax = newMax;
            RefreshLabel();
        }

        void Start()
        {
            lastMax = Max;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head != null) CreateFlash();
            if (healthLabelAnchor != null) CreateLabel();
            RefreshLabel();
        }

        /// <summary>The player in the current scene.</summary>
        public static PlayerHealth Instance { get; private set; }

        /// <summary>Damage taken this run, for the end-of-run summary.</summary>
        public float DamageTakenTotal { get; private set; }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || Time.time < invulnerableUntil || info.Amount <= 0f) return;

            // Armor and trinkets make the player tougher (or frailer).
            info.Amount *= PlayerStats.Get(StatType.DamageTaken);
            Current = Mathf.Max(0f, Current - info.Amount);
            DamageTakenTotal += info.Amount;
            if (info.Blocked)
            {
                // Chip damage through a block: a sting, not a wound. No hurt sound (the block already made one) and
                // no immunity window, so a follow-up blow that gets past the guard still lands.
                flashAlpha = Mathf.Max(flashAlpha, 0.2f);
                Haptic(0.4f, 0.08f);
            }
            else
            {
                invulnerableUntil = Time.time + invulnerabilityTime;
                flashAlpha = 0.55f;
                Haptic(1f, 0.2f);
                Audio.Sfx.Play(Audio.SfxKind.PlayerHurt, head != null ? head.position : transform.position + Vector3.up * 1.5f);
            }
            RefreshLabel();
            Damaged?.Invoke(info);

            if (Current <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(Max, Current + amount);
            RefreshLabel();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Die()
        {
            IsDead = true;
            Audio.Sfx.Play(Audio.SfxKind.PlayerDeath, head != null ? head.position : transform.position);
            Died?.Invoke();
            Debug.Log("[Player] Died");
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
            label = UI.GameText.World(healthLabelAnchor, "Health Label", UI.TextStyle.Soft, 0.28f);
            label.fontStyle = TMPro.FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(0.3f, 0.08f);
        }

        void RefreshLabel()
        {
            if (label == null) return;
            // Rebuild the text only when the shown number changes (no string or mesh churn every hit tick).
            int value = Mathf.CeilToInt(Current);
            if (value == shownHealth) return;
            shownHealth = value;
            label.SetText("{0}", value);
            label.color = Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.4f, 1f, 0.5f), Current / Mathf.Max(1f, Max));
        }
    }
}
