using System;
using UnityEngine;
using Roguelike.Combat;

namespace Roguelike.Enemies
{
    /// <summary>
    /// An enemy's health. Sits on the enemy's root; hits on any body collider below it end up here,
    /// scaled by the <see cref="HitZone"/> that was struck.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float headMultiplier = 2f;
        [SerializeField] float limbMultiplier = 0.7f;
        [SerializeField] bool showDamageNumbers = true;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Raised for every hit that did damage, with the damage already scaled by zone.</summary>
        public event Action<DamageInfo, HitZoneType> Damaged;
        /// <summary>Raised once, with the killing blow.</summary>
        public event Action<DamageInfo> Died;

        void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return;

            HitZoneType zone = HitZoneType.Torso;
            if (info.HitCollider != null)
            {
                var hitZone = info.HitCollider.GetComponent<HitZone>();
                if (hitZone != null) zone = hitZone.zone;
            }

            if (zone == HitZoneType.Head) info.Amount *= headMultiplier;
            else if (zone == HitZoneType.Limb) info.Amount *= limbMultiplier;

            Current = Mathf.Max(0f, Current - info.Amount);
            if (showDamageNumbers) DamagePopup.Spawn(info.Point, info.Amount, info.Type);
            Damaged?.Invoke(info, zone);

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(info);
            }
        }
    }
}
