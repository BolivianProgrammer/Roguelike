using System;
using UnityEngine;
using Roguelike.Combat;

namespace Roguelike.Enemies
{
    /// <summary>
    /// An enemy's health. Sits on the enemy's root; hits on any body collider below it end up here,
    /// scaled by the <see cref="HitZone"/> that was struck. Also decides when a hit is a finisher.
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

        /// <summary>
        /// Asked on every hit: is the enemy currently open to a finisher (stunned)?
        /// If so, a blow to the head or a stab to the body kills outright.
        /// </summary>
        public Func<bool> CanBeExecuted;

        /// <summary>Raised for every hit that did damage to a living enemy, with the damage already scaled by zone.</summary>
        public event Action<DamageInfo, HitZoneType> Damaged;
        /// <summary>Raised once, with the killing blow.</summary>
        public event Action<DamageInfo> Died;
        /// <summary>Raised for every hit that connects, including hits on a corpse. Dismemberment listens to this.</summary>
        public event Action<DamageInfo> Struck;

        /// <summary>
        /// A short description of the most recent hit on any enemy: body part, bone, hit type and damage.
        /// Shown on screen in desktop play to check that hits land where they were aimed.
        /// </summary>
        public static string LastHit { get; set; }

        static string Describe(DamageInfo info)
        {
            string where = "Body";
            if (info.HitCollider != null)
            {
                var hitZone = info.HitCollider.GetComponent<HitZone>();
                string bone = info.HitCollider.name.Replace("mixamorig:", "");
                where = hitZone != null ? hitZone.part + " (" + bone + ")" : bone;
            }
            return where + ", " + info.Type + ", " + Mathf.RoundToInt(info.Amount) + " damage";
        }

        void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (info.Amount <= 0f) return;

            LastHit = Describe(info) + (IsDead ? " (corpse)" : "");

            if (IsDead)
            {
                // The body can still be cut apart.
                Struck?.Invoke(info);
                return;
            }

            HitZoneType zone = HitZoneType.Torso;
            if (info.HitCollider != null)
            {
                var hitZone = info.HitCollider.GetComponent<HitZone>();
                if (hitZone != null) zone = hitZone.zone;
            }

            if (zone == HitZoneType.Head) info.Amount *= headMultiplier;
            else if (zone == HitZoneType.Limb) info.Amount *= limbMultiplier;

            bool finisherBlow = zone == HitZoneType.Head || zone == HitZoneType.Torso && info.Type == DamageType.Stab;
            if (finisherBlow && CanBeExecuted != null && CanBeExecuted())
            {
                info.Execution = true;
                info.Amount = Mathf.Max(info.Amount, Current);
            }

            Current = Mathf.Max(0f, Current - info.Amount);
            if (showDamageNumbers && !info.Execution) DamagePopup.Spawn(info.Point, info.Amount, info.Type);
            Damaged?.Invoke(info, zone);

            if (Current <= 0f && !IsDead) Die(info);
            Struck?.Invoke(info);
        }

        /// <summary>Loses health without a hit, such as bleeding from a severed limb. No hit reaction.</summary>
        public void Bleed(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            if (Current <= 0f)
                Die(new DamageInfo { Point = transform.position + Vector3.up, Direction = -transform.forward, ImpactSpeed = 0f });
        }

        /// <summary>Kills the enemy outright with the given blow, e.g. when it loses its head.</summary>
        public void Kill(DamageInfo info)
        {
            if (IsDead) return;
            Current = 0f;
            Die(info);
        }

        void Die(DamageInfo info)
        {
            IsDead = true;
            Died?.Invoke(info);
        }
    }
}
