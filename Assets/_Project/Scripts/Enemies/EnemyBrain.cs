using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Enemies
{
    /// <summary>One attack an enemy can perform: which animation, when it can hurt, and how much.</summary>
    [Serializable]
    public class EnemyAttack
    {
        [Tooltip("Name of the Animator state to play.")]
        public string stateName = "SlashA";
        [Tooltip("Total length of the attack in seconds.")]
        public float duration = 1.5f;
        [Tooltip("Seconds into the attack when the weapon starts being dangerous. Everything before is the wind-up.")]
        public float hitStart = 0.45f;
        [Tooltip("Seconds into the attack when the weapon stops being dangerous.")]
        public float hitEnd = 0.8f;
        public float damage = 15f;
        public DamageType damageType = DamageType.Slash;
    }

    /// <summary>
    /// A melee enemy: notices the player, closes in, and attacks with telegraphed swings.
    /// Blocks knock its weapon back, parries stagger it, and solid hits make it flinch.
    /// Movement is a straight walk across flat ground for now; proper pathfinding comes with real levels.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyBrain : MonoBehaviour
    {
        enum State { Idle, Chase, Attack, Flinch, Stagger, Dead }

        static readonly List<EnemyBrain> all = new List<EnemyBrain>();
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [SerializeField] Animator animator;
        [SerializeField] EnemyRagdoll ragdoll;
        [SerializeField] MeleeAttackHitbox weaponHitbox;
        [Tooltip("The visible weapon, parented to the hand. It is let go when the enemy dies.")]
        [SerializeField] Transform weaponVisual;

        [Header("Senses")]
        [Tooltip("The enemy starts chasing when the player comes within this distance (meters).")]
        [SerializeField] float aggroRange = 7f;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 1.4f;
        [SerializeField] float runSpeed = 3.5f;
        [Tooltip("Runs when further than this from the player, walks when closer.")]
        [SerializeField] float runDistance = 4f;
        [Tooltip("Stops advancing at this distance from the player.")]
        [SerializeField] float stopDistance = 1.3f;
        [SerializeField] float turnSpeed = 360f;
        [Tooltip("Keeps this much room (meters) from other enemies.")]
        [SerializeField] float personalSpace = 0.9f;

        [Header("Attacks")]
        [SerializeField] EnemyAttack[] attacks;
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] Vector2 timeBetweenAttacks = new Vector2(1f, 2f);

        [Header("Reactions")]
        [Tooltip("Hits with less stagger than this do not interrupt the enemy.")]
        [SerializeField] float flinchThreshold = 10f;
        [Tooltip("Hits with at least this much stagger stun the enemy outright, like a parry does.")]
        [SerializeField] float heavyStaggerThreshold = 60f;
        [SerializeField] float heavyStun = 1.8f;
        [SerializeField] float flinchDuration = 0.7f;
        [Tooltip("After flinching, the enemy cannot be interrupted again for this long, so it cannot be stun-locked.")]
        [SerializeField] float poiseTime = 1.5f;
        [SerializeField] float blockedRecoil = 0.7f;
        [SerializeField] float parriedStun = 2.5f;
        [Tooltip("While stunned, hits of at least this much damage play a hit reaction.")]
        [SerializeField] float staggeredFlinchThreshold = 3f;
        [Tooltip("How long a hit reaction plays on a stunned enemy before it sags back into the stun.")]
        [SerializeField] float staggeredReactionTime = 0.5f;

        [Header("Death")]
        [Tooltip("How hard the killing blow throws the body, per m/s of swing speed.")]
        [SerializeField] float deathImpulsePerSpeed = 12f;
        [SerializeField] float corpseLifetime = 10f;

        EnemyHealth health;
        PlayerHealth target;
        State state;
        float stateTime;
        float stateDuration;
        float nextAttackTime;
        float flinchImmuneUntil;
        float resumeStaggerAt;
        float speed;
        int attackIndex;
        EnemyAttack attack;
        bool hitOpened;
        bool hitClosed;

        /// <summary>True while the enemy is reeling from a parry. Later this is the opening for finishers.</summary>
        public bool IsStaggered => state == State.Stagger;
        public bool IsDead => state == State.Dead;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        void OnEnable()
        {
            all.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (weaponHitbox != null) weaponHitbox.Blocked += OnBlocked;
        }

        void OnDisable()
        {
            all.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (weaponHitbox != null) weaponHitbox.Blocked -= OnBlocked;
        }

        void Start()
        {
            target = FindObjectOfType<PlayerHealth>();
        }

        void Update()
        {
            if (state == State.Dead) return;

            stateTime += Time.deltaTime;
            float desiredSpeed = 0f;

            bool hasTarget = target != null && !target.IsDead;
            Vector3 toTarget = hasTarget ? target.transform.position - transform.position : Vector3.zero;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            switch (state)
            {
                case State.Idle:
                    if (hasTarget && distance <= aggroRange) Enter(State.Chase);
                    break;

                case State.Chase:
                    if (!hasTarget)
                    {
                        Enter(State.Idle);
                        break;
                    }
                    Face(toTarget, turnSpeed);
                    if (distance > stopDistance)
                        desiredSpeed = distance > runDistance ? runSpeed : walkSpeed;
                    if (distance <= attackRange && Time.time >= nextAttackTime && Vector3.Angle(transform.forward, toTarget) < 25f)
                        StartAttack();
                    break;

                case State.Attack:
                    // Keeps tracking the player during the wind-up, then commits to the swing.
                    if (stateTime < attack.hitStart && hasTarget) Face(toTarget, turnSpeed * 0.4f);

                    if (!hitOpened && stateTime >= attack.hitStart)
                    {
                        hitOpened = true;
                        weaponHitbox.Begin(attack.damage, attack.damageType);
                    }
                    if (hitOpened && !hitClosed && stateTime >= attack.hitEnd)
                    {
                        hitClosed = true;
                        weaponHitbox.End();
                    }
                    if (stateTime >= attack.duration) FinishAttack();
                    break;

                case State.Flinch:
                case State.Stagger:
                    if (state == State.Stagger && resumeStaggerAt > 0f && Time.time >= resumeStaggerAt)
                    {
                        resumeStaggerAt = 0f;
                        if (stateTime < stateDuration - 0.2f) animator.CrossFadeInFixedTime("Stagger", 0.2f);
                    }
                    if (stateTime >= stateDuration)
                    {
                        animator.CrossFadeInFixedTime("Locomotion", 0.25f);
                        Enter(State.Chase);
                    }
                    break;
            }

            Move(desiredSpeed);
        }

        void Move(float desiredSpeed)
        {
            speed = Mathf.MoveTowards(speed, desiredSpeed, 8f * Time.deltaTime);
            Vector3 step = transform.forward * (speed * Time.deltaTime);

            // Do not stand inside other enemies.
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == this || other.state == State.Dead) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float gap = away.magnitude;
                if (gap < personalSpace && gap > 0.001f)
                    step += away / gap * ((personalSpace - gap) * 2f * Time.deltaTime);
            }

            transform.position += step;
            animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
        }

        void Face(Vector3 direction, float degreesPerSecond)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, degreesPerSecond * Time.deltaTime);
        }

        void StartAttack()
        {
            if (attacks == null || attacks.Length == 0 || weaponHitbox == null) return;

            attack = attacks[attackIndex++ % attacks.Length];
            hitOpened = false;
            hitClosed = false;
            animator.CrossFadeInFixedTime(attack.stateName, 0.1f);
            Enter(State.Attack);
        }

        void FinishAttack()
        {
            weaponHitbox.End();
            nextAttackTime = Time.time + UnityEngine.Random.Range(timeBetweenAttacks.x, timeBetweenAttacks.y);
            animator.CrossFadeInFixedTime("Locomotion", 0.2f);
            Enter(State.Chase);
        }

        void OnBlocked(Vector3 point, bool parried)
        {
            if (state == State.Dead) return;

            weaponHitbox.End();
            nextAttackTime = Time.time + timeBetweenAttacks.x;
            DamagePopup.Spawn(point, parried ? "PARRY" : "BLOCK", parried ? new Color(1f, 0.85f, 0.2f) : Color.white);

            if (parried)
            {
                animator.CrossFadeInFixedTime("Stagger", 0.15f);
                Enter(State.Stagger, parriedStun);
            }
            else
            {
                animator.CrossFadeInFixedTime("HitFront", 0.1f);
                Enter(State.Flinch, blockedRecoil);
            }
        }

        void OnDamaged(DamageInfo info, HitZoneType zone)
        {
            if (state == State.Dead) return;

            // React away from the side the blow came from.
            float side = transform.InverseTransformDirection(info.Direction).x;
            string reaction = side > 0.4f ? "HitSideA" : side < -0.4f ? "HitSideB" : "HitFront";

            if (state == State.Stagger)
            {
                // A stunned enemy has no poise: every real hit shows, then it sags back into the stun.
                if (info.Amount < staggeredFlinchThreshold) return;
                animator.CrossFadeInFixedTime(reaction, 0.08f, 0, 0f);
                resumeStaggerAt = Time.time + staggeredReactionTime;
                return;
            }

            // Stagger is how hard the hit knocks the enemy off balance; plain damage if the source did not say.
            float force = info.Stagger > 0f ? info.Stagger : info.Amount;

            // A heavy enough blow stuns outright, even through the flinch protection.
            if (force >= heavyStaggerThreshold)
            {
                if (weaponHitbox != null) weaponHitbox.End();
                nextAttackTime = Time.time + heavyStun;
                animator.CrossFadeInFixedTime("Stagger", 0.15f);
                Enter(State.Stagger, heavyStun);
                return;
            }

            if (state == State.Idle) Enter(State.Chase);
            if (force < flinchThreshold || Time.time < flinchImmuneUntil) return;

            if (weaponHitbox != null) weaponHitbox.End();
            flinchImmuneUntil = Time.time + flinchDuration + poiseTime;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + flinchDuration);
            animator.CrossFadeInFixedTime(reaction, 0.08f, 0, 0f);
            Enter(State.Flinch, flinchDuration);
        }

        void OnDied(DamageInfo info)
        {
            Enter(State.Dead);
            if (weaponHitbox != null)
            {
                weaponHitbox.End();
                Destroy(weaponHitbox.gameObject);
            }

            // The weapon falls out of its hand as a plain physics object.
            if (weaponVisual != null)
            {
                weaponVisual.SetParent(transform, true);
                var blade = weaponVisual.gameObject.AddComponent<BoxCollider>();
                blade.center = new Vector3(0f, 0f, 0.4f);
                blade.size = new Vector3(0.05f, 0.02f, 0.95f);
                weaponVisual.gameObject.AddComponent<Rigidbody>().mass = 1.5f;
            }

            if (ragdoll != null)
            {
                float push = Mathf.Clamp(info.ImpactSpeed, 2f, 10f) * deathImpulsePerSpeed;
                ragdoll.Activate(info.Direction * push, info.Point);
            }

            Destroy(gameObject, corpseLifetime);
        }

        void Enter(State next, float duration = 0f)
        {
            state = next;
            resumeStaggerAt = 0f;
            stateTime = 0f;
            stateDuration = duration;
        }
    }
}
