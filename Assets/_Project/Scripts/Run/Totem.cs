using System;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// A mission objective. Destroy it either by hitting it with a weapon, or by holding a hand on it
    /// for a couple of seconds. When it goes, it offers a choice of buffs.
    /// </summary>
    public class Totem : MonoBehaviour, IDamageable
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float health = 60f;
        [Tooltip("Seconds a hand must stay on the totem to destroy it without a weapon.")]
        [SerializeField] float channelTime = 2.5f;
        [Tooltip("How close (meters) a hand must be to the totem's center line to count as touching it.")]
        [SerializeField] float touchRadius = 0.6f;
        [SerializeField] Renderer body;
        [SerializeField] Color idleColor = new Color(0.5f, 0.2f, 0.8f);
        [SerializeField] Color channelColor = new Color(1f, 0.9f, 0.4f);

        MaterialPropertyBlock propertyBlock;
        PhysicsHand[] hands;
        float channel;
        float collapse = -1f;
        Vector3 startScale;

        public bool IsDestroyed { get; private set; }

        /// <summary>Raised once when the totem is destroyed, before it finishes collapsing.</summary>
        public event Action<Totem> Destroyed;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            startScale = transform.localScale;
            SetColor(idleColor);
        }

        void Start()
        {
            hands = FindObjectsOfType<PhysicsHand>();
        }

        void Update()
        {
            if (IsDestroyed)
            {
                Collapse();
                return;
            }

            // Interacting: a hand resting on the totem slowly unmakes it.
            bool touching = false;
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                Vector3 offset = hand.transform.position - transform.position;
                float height = offset.y;
                offset.y = 0f;
                if (offset.magnitude <= touchRadius && height > -0.2f && height < 2.4f)
                {
                    touching = true;
                    hand.Haptic(0.15f + 0.5f * channel / channelTime, 0.05f);
                }
            }

            channel = touching ? channel + Time.deltaTime : Mathf.Max(0f, channel - Time.deltaTime * 2f);
            float progress = Mathf.Clamp01(channel / channelTime);
            SetColor(Color.Lerp(idleColor, channelColor, progress));
            // The "little animation": it trembles and spins faster as it is unmade.
            transform.Rotate(0f, (20f + 500f * progress) * Time.deltaTime, 0f);

            if (channel >= channelTime) Break(transform.position + Vector3.up);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDestroyed || info.Amount <= 0f) return;

            health -= info.Amount;
            DamagePopup.Spawn(info.Point, info.Amount, info.Type);
            if (health <= 0f) Break(info.Point);
        }

        void Break(Vector3 point)
        {
            IsDestroyed = true;
            collapse = 0f;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            SetColor(channelColor);
            Destroyed?.Invoke(this);
        }

        void Collapse()
        {
            if (collapse < 0f) return;
            collapse += Time.deltaTime;
            float t = Mathf.Clamp01(collapse / 0.8f);
            transform.Rotate(0f, 900f * Time.deltaTime, 0f);
            transform.localScale = startScale * (1f - t * t);
            if (t >= 1f)
            {
                collapse = -1f;
                gameObject.SetActive(false);
            }
        }

        void SetColor(Color color)
        {
            if (body == null) return;
            body.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            body.SetPropertyBlock(propertyBlock);
        }
    }
}
