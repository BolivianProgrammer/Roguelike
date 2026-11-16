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
        [Tooltip("Bosses turn this off: a finisher blow then only does its normal damage.")]
        [SerializeField] bool canBeFinished = true;

        [Header("Dropped on death")]
        [SerializeField] int essenceReward = 10;
        [SerializeField] int coinReward = 5;

        public int EssenceReward => essenceReward;
        public int CoinReward => coinReward;

        /// <summary>Raised whenever any enemy dies, for kill counts, loot and mission progress.</summary>
        public static event Action<EnemyHealth, DamageInfo> AnyDied;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>
        /// Asked on every hit: is the enemy currently open to a finisher (stunned)?
        /// If so, a blow to the head or a stab to the body kills outright.
        /// </summary>
        public Func<bool> CanBeExecuted;

        /// <summary>Asked on every hit: does something (a shield) stop this blow completely?</summary>
        public Func<DamageInfo, bool> Guards;

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

        readonly System.Collections.Generic.List<Vector2> wounds = new System.Collections.Generic.List<Vector2>();

        void Awake()
        {
            Current = maxHealth;
        }

        void Start()
        {
            // Tougher enemies on harder missions; elites are set up tougher still in their prefab.
            maxHealth *= Difficulty.EnemyHealth;
            Current = maxHealth;
        }

        /// <summary>An open wound that does damage over time: X = damage per second, Y = seconds left.</summary>
        public void AddWound(float damagePerSecond, float seconds)
        {
            if (!IsDead) wounds.Add(new Vector2(damagePerSecond, seconds));
        }

        void Update()
        {
            if (wounds.Count == 0 || IsDead || WorldFreeze.Active) return;
            float total = 0f;
            for (int i = wounds.Count - 1; i >= 0; i--)
            {
                Vector2 wound = wounds[i];
                total += wound.x * Time.deltaTime;
                wound.y -= Time.deltaTime;
                if (wound.y <= 0f) wounds.RemoveAt(i);
                else wounds[i] = wound;
            }
            Bleed(total);
        }

        public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;

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

            // A raised shield takes the blow instead.
            if (Guards != null && Guards(info))
            {
                CombatLog.Log(name + " shield stopped a blow");
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
            if (canBeFinished && finisherBlow && CanBeExecuted != null && CanBeExecuted())
            {
                info.Execution = true;
                info.Amount = Mathf.Max(info.Amount, Current);
            }

            Current = Mathf.Max(0f, Current - info.Amount);
            CombatLog.Log(name + " took " + info.Amount.ToString("F1") + " (" + zone + " zone" + (info.Execution ? ", EXECUTION" : "") + ") health "
                + Current.ToString("F1") + " / " + maxHealth);
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
            AnyDied?.Invoke(this, info);
        }
    }
}
