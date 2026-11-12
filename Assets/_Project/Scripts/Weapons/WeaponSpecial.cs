using UnityEngine;
using Roguelike.Combat;

namespace Roguelike.Weapons
{
    /// <summary>
    /// A weapon's special attack. Pressing the trigger (R on desktop) while holding the weapon arms it:
    /// the weapon glows for a short time, during which the special does its thing, then it goes on cooldown.
    /// Each weapon gets one subclass that decides what "its thing" is.
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public abstract class WeaponSpecial : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] string displayName = "Special";
        [Tooltip("Seconds the special stays armed after activation.")]
        [SerializeField] float activeTime = 3f;
        [Tooltip("Seconds after the special ends before it can be used again.")]
        [SerializeField] float cooldown = 8f;
        [SerializeField] Color glowColor = new Color(1f, 0.6f, 0.1f);
        [Tooltip("Parts of the weapon that change colour while the special is armed.")]
        [SerializeField] Renderer[] glowRenderers;

        Grabbable grabbable;
        MaterialPropertyBlock propertyBlock;
        float activeUntil;
        float readyAt;
        bool wasActive;
        bool wasReady = true;

        public string DisplayName => displayName;
        public bool IsActive => Time.time < activeUntil;
        public bool IsReady => !IsActive && Time.time >= readyAt;
        public float CooldownRemaining => IsActive ? 0f : Mathf.Max(0f, readyAt - Time.time);

        /// <summary>Scales the weapon's delay between hits on the same target. Below 1 means faster hits.</summary>
        public virtual float HitCooldownScale => 1f;

        protected virtual void Awake()
        {
            grabbable = GetComponent<Grabbable>();
            propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>Arms the special if it is off cooldown. Returns whether it activated.</summary>
        public bool TryActivate()
        {
            if (!IsReady) return false;

            activeUntil = Time.time + activeTime;
            wasActive = true;
            SetGlow(true);
            Haptic(0.8f, 0.15f);
            OnActivated();
            return true;
        }

        /// <summary>Ends the special early (for example once its empowered hit has landed) and starts the cooldown.</summary>
        protected void Finish()
        {
            activeUntil = 0f;
            wasActive = false;
            wasReady = false;
            readyAt = Time.time + cooldown;
            SetGlow(false);
        }

        protected virtual void Update()
        {
            if (wasActive && !IsActive) Finish();

            // A short buzz tells the player the special is ready again.
            if (!wasReady && IsReady)
            {
                wasReady = true;
                Haptic(0.3f, 0.05f);
            }
        }

        protected virtual void OnActivated() { }

        /// <summary>Lets the special change a hit's damage and stagger before it lands.</summary>
        public virtual void ModifyHit(ref float damage, ref float stagger) { }

        /// <summary>Called after a hit has landed on a target.</summary>
        public virtual void AfterHit(DamageInfo info, IDamageable target) { }

        protected void Haptic(float amplitude, float duration)
        {
            foreach (var holder in grabbable.Holders)
                holder.Hand.Haptic(amplitude, duration);
        }

        void SetGlow(bool on)
        {
            if (glowRenderers == null) return;
            foreach (var glowRenderer in glowRenderers)
            {
                if (glowRenderer == null) continue;
                if (on)
                {
                    glowRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(BaseColorId, glowColor);
                    glowRenderer.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    glowRenderer.SetPropertyBlock(null);
                }
            }
        }
    }
}
