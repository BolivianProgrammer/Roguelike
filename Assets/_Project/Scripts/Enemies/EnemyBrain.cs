using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Enemies
{
    /// <summary>Which part of the enemy strikes during an attack.</summary>
    public enum AttackLimb { Weapon, LeftHand, RightHand }

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
        [Tooltip("Further strikes in the same animation, for combos: X = start, Y = end, in seconds.")]
        public Vector2[] extraHits;
        [Tooltip("A thrown bolt instead of a weapon swing. It leaves the hand at Hit Start; Damage is what the bolt does.")]
        public bool ranged;
        [Tooltip("What strikes: the held weapon, or a bare hand.")]
        public AttackLimb limb = AttackLimb.Weapon;
        [Tooltip("The enemy starts this attack from up to this far away (meters). 0 = the enemy's normal attack range.")]
        public float range;
        [Tooltip("How far (meters) the enemy moves forward during the attack, e.g. a charging lunge. 0 = attacks on the spot.")]
        public float lungeDistance;
        [Tooltip("When the lunge starts and ends, in seconds into the attack.")]
        public Vector2 lungeTime = new Vector2(0.4f, 0.8f);
        [Tooltip("The lunge stops when the player is this close (meters): where the weapon reaches during the strike, so it lands on the player instead of past them.")]
        public float lungeStopAt = 0.8f;
        [Tooltip("Both hands on the weapon: the off hand is held on the grip by IK (the clips only animate the weapon hand properly).")]
        public bool twoHanded;
        [Tooltip("Breaks any block it meets (only a parry stops it). The weapon glows red during the wind-up as a warning.")]
        public bool guardBreak;

        public EnemyAttack() { }

        public EnemyAttack(string state, float duration, float start, float end, float damage, DamageType type, AttackLimb limb, float range)
        {
            stateName = state;
            this.duration = duration;
            hitStart = start;
            hitEnd = end;
            this.damage = damage;
            damageType = type;
            this.limb = limb;
            this.range = range;
        }
    }

    /// <summary>
    /// A melee (or ranged) enemy: notices the player, closes in, and attacks with telegraphed swings.
    /// Blocks knock its weapon back, parries stagger it, and solid hits make it flinch.
    /// It fights on as long as it can: without its weapon it punches, without legs it crawls, and cut in half
    /// its upper body drags itself along. Paths around obstacles on the level's walkable area.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyBrain : MonoBehaviour
    {
        enum State { Intro, Unaware, Notice, Taunt, Idle, Chase, Attack, Flinch, Stagger, Fall, Dead }
        enum Body { Standing, Crawling, Bisected }

        static readonly List<EnemyBrain> all = new List<EnemyBrain>();
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [SerializeField] Animator animator;
        [SerializeField] EnemyRagdoll ragdoll;
        [SerializeField] MeleeAttackHitbox weaponHitbox;
        [Tooltip("The visible weapon, parented to the hand. It is let go when the enemy dies.")]
        [SerializeField] Transform weaponVisual;

        [Header("Animation states (names in the Animator)")]
        [SerializeField] string hitFrontState = "Hit_Front";
        [Tooltip("Played for a blow on the enemy's left side.")]
        [SerializeField] string hitLeftState = "Hit_Left";
        [SerializeField] string hitRightState = "Hit_Right";
        [SerializeField] string hitBackState = "Hit_Back";
        [SerializeField] string stunnedState = "Stunned";
        [SerializeField] string blockedState = "Blocked_Recoil";
        [Tooltip("Played once when the enemy appears, before it does anything else. Empty = none.")]
        [SerializeField] string introState = "";
        [SerializeField] float introDuration = 2f;
        [Tooltip("Played on death before the body goes limp. Empty = go limp at once (or play a fall, see below).")]
        [SerializeField] string deathState = "";
        [SerializeField] float deathDuration = 3f;
        [Tooltip("Chance (0-1) that an enemy without a death state plays a fall (Death_Front / Death_Back) instead of going limp.")]
        [SerializeField, Range(0f, 1f)] float deathAnimationChance = 0.4f;

        [Header("Senses")]
        [Tooltip("The enemy starts chasing when the player comes within this distance (meters).")]
        [SerializeField] float aggroRange = 7f;
        [Tooltip("Starts off unaware (weapon lowered) instead of playing its intro. It notices the player when they come close or hit it.")]
        [SerializeField] bool startUnaware;
        [Tooltip("Chance (0-1) of a taunt after noticing the player.")]
        [SerializeField, Range(0f, 1f)] float tauntChance = 0.35f;

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
        [Tooltip("Sideways speed while circling the player between attacks (the strafe clips are made for 0.6 m/s).")]
        [SerializeField] float strafeSpeed = 0.6f;
        [Tooltip("Backing-away speed (the Walk_Back clip is made for 0.76 m/s).")]
        [SerializeField] float backSpeed = 0.76f;
        [Tooltip("Turning on the spot, in degrees per second (the turn clips are made for about 100).")]
        [SerializeField] float turnInPlaceSpeed = 100f;
        [Tooltip("Speed when dragging itself along the ground without legs, or as an upper body.")]
        [SerializeField] float crawlSpeed = 0.45f;

        [Header("Attacks")]
        [SerializeField] EnemyAttack[] attacks;
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] Vector2 timeBetweenAttacks = new Vector2(1f, 2f);
        [Tooltip("Multiplies the damage of every attack: difficulty, elites and enrage raise it.")]
        [SerializeField] float damageMultiplier = 1f;
        [Tooltip("Attacks after losing the weapon.")]
        [SerializeField] EnemyAttack[] unarmedAttacks =
        {
            new EnemyAttack("Unarmed_Attack", 1.4f, 0.6f, 0.75f, 8f, DamageType.Blunt, AttackLimb.LeftHand, 1.15f),
        };
        [Tooltip("Attacks while crawling without legs.")]
        [SerializeField] EnemyAttack[] crawlAttacks =
        {
            new EnemyAttack("Crawl_Attack", 1.4f, 0.6f, 0.78f, 7f, DamageType.Slash, AttackLimb.RightHand, 1.0f),
        };
        [Tooltip("Attacks of an upper body cut off at the waist or chest.")]
        [SerializeField] EnemyAttack[] bisectedAttacks =
        {
            new EnemyAttack("Bisected_Attack", 1.4f, 0.6f, 0.78f, 6f, DamageType.Slash, AttackLimb.RightHand, 0.95f),
        };

        [Header("Ranged")]
        [Tooltip("A ranged enemy keeps its distance and throws bolts instead of closing in.")]
        [SerializeField] bool isRanged;
        [Tooltip("Where bolts appear: normally the throwing hand.")]
        [SerializeField] Transform muzzle;
        [Tooltip("A ranged enemy stops advancing at this distance from the player.")]
        [SerializeField] float rangedStopDistance = 9f;
        [Tooltip("A ranged enemy attacks from up to this far away.")]
        [SerializeField] float rangedAttackRange = 13f;
        [Tooltip("Meters per second. Slow enough to see, block and bat back.")]
        [SerializeField] float boltSpeed = 9f;
        [Tooltip("A ranged enemy backs away when the player gets closer than this (meters). 0 = never.")]
        [SerializeField] float retreatDistance = 4f;

        [Header("Reactions")]
        [Tooltip("Hits with less stagger than this do not interrupt the enemy.")]
        [SerializeField] float flinchThreshold = 10f;
        [Tooltip("Hits with at least this much stagger stun the enemy outright, like a parry does.")]
        [SerializeField] float heavyStaggerThreshold = 60f;
        [SerializeField] float heavyStun = 1.8f;
        [SerializeField] float flinchDuration = 0.7f;
        [Tooltip("After flinching, the enemy cannot be interrupted again for this long, so it cannot be stun-locked.")]
        [SerializeField] float poiseTime = 1.5f;
        [SerializeField] float parriedStun = 2.5f;
        [Tooltip("While stunned, hits of at least this much damage play a hit reaction.")]
        [SerializeField] float staggeredFlinchThreshold = 3f;
        [Tooltip("How long a hit reaction plays on a stunned enemy before it sags back into the stun.")]
        [SerializeField] float staggeredReactionTime = 0.5f;

        [Tooltip("Chance (0-1) that an attack is followed straight away by another, with no pause: a two-hit combo. " +
                 "Elites use it. The follow-up never chains into a third.")]
        [SerializeField, Range(0f, 1f)] float followUpChance = 0f;
        [Tooltip("Pause before attacking again after breaking the player's guard: it presses the opening.")]
        [SerializeField] float pressOpeningPause = 0.15f;

        [Tooltip("Two-handed attacks: how much the off hand's turn follows the weapon hand's (0 = keep the clip's own wrist).")]
        [SerializeField, Range(0f, 1f)] float offHandTurn = 0f;

        [Header("Shield")]
        [Tooltip("A shield on the left forearm that stops blows from the front while the enemy is ready. Empty = no shield.")]
        [SerializeField] Transform shield;
        [Tooltip("A blow with at least this much stagger breaks through the shield (and stuns).")]
        [SerializeField] float shieldBreak = 55f;

        [Header("Enrage (boss second phase)")]
        [Tooltip("Below this share of its health (0-1) the enemy roars and fights harder. 0 = never.")]
        [SerializeField, Range(0f, 1f)] float enrageBelow;
        [SerializeField] string enrageState = "Boss_Roar";
        [SerializeField] float enrageDuration = 2.7f;
        [Tooltip("Once enraged: movement speed and damage multiply by this, and the pause between attacks divides by it.")]
        [SerializeField] float enrageStrength = 1.3f;

        [Header("Finishers")]
        [Tooltip("Health the player gets back for finishing a stunned enemy.")]
        [SerializeField] float executionHeal = 15f;
        [Tooltip("Stamina the player gets back for finishing a stunned enemy.")]
        [SerializeField] float executionStamina = 50f;

        [Header("Death")]
        [Tooltip("How hard the killing blow throws the body, per m/s of swing speed.")]
        [SerializeField] float deathImpulsePerSpeed = 12f;
        [SerializeField] float corpseLifetime = 10f;

        EnemyHealth health;
        EnemyDismemberment dismemberment;
        PlayerHealth target;
        State state;
        Body body = Body.Standing;
        float stateTime;
        float stateDuration;
        float nextAttackTime;
        float flinchImmuneUntil;
        bool disarmed;
        bool enraged;
        bool frozen;
        NavMeshAgent agent;
        float nextPathTime;
        readonly RaycastHit[] sightHits = new RaycastHit[16];
        Projectile heldBolt;
        Transform headBone;
        GameObject executeMarker;
        float resumeStaggerAt;
        float speed;
        int attackIndex;
        EnemyAttack attack;
        MeleeAttackHitbox strikingHitbox;
        readonly Dictionary<AttackLimb, MeleeAttackHitbox> handHitboxes = new Dictionary<AttackLimb, MeleeAttackHitbox>();
        bool hitOpened;
        int hitIndex;
        float lungeDone;
        string moveClip = "Locomotion";
        float strafeSign = 1f;
        float strafeUntil;
        bool retreating;
        float retreatUntil;
        float stride;
        float nextHurtSound;
        bool shieldRecoiling;

        float VoicePitch => 1f / Mathf.Max(0.5f, transform.lossyScale.x);
        Vector3 Mouth => headBone != null ? headBone.position : transform.position + Vector3.up * 1.6f;

        public bool IsDead => state == State.Dead;
        /// <summary>True once the enemy has lost its legs or the lower half of its body.</summary>
        public bool IsOnTheGround => body != Body.Standing;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            dismemberment = GetComponent<EnemyDismemberment>();
            // A stunned enemy is open to a finisher.
            health.CanBeExecuted = () => state == State.Stagger;
            health.Guards = ShieldStops;
            if (animator != null && animator.isHuman) headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        void OnEnable()
        {
            all.Add(this);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (weaponHitbox != null)
            {
                weaponHitbox.Blocked += OnBlocked;
                weaponHitbox.HitPlayer += OnHitPlayer;
            }
        }

        void OnDisable()
        {
            all.Remove(this);
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (weaponHitbox != null)
            {
                weaponHitbox.Blocked -= OnBlocked;
                weaponHitbox.HitPlayer -= OnHitPlayer;
            }
        }

        void OnDestroy()
        {
            foreach (var hitbox in handHitboxes.Values)
                if (hitbox != null) Destroy(hitbox.gameObject);
        }

        void Start()
        {
            target = FindObjectOfType<PlayerHealth>();
            damageMultiplier *= Difficulty.EnemyDamage;
            SetUpPathfinding();

            if (startUnaware)
            {
                animator.Play("Idle_Unaware", 0, UnityEngine.Random.value);
                moveClip = "Idle_Unaware";
                Enter(State.Unaware);
            }
            else if (!string.IsNullOrEmpty(introState))
            {
                animator.Play(introState, 0, 0f);
                if (introState.Contains("Roar")) Audio.Sfx.Play(Audio.SfxKind.EnemyRoar, Mouth, 1f, VoicePitch);
                Enter(State.Intro, introDuration);
            }
            else Enter(State.Idle);
        }

        /// <summary>Sets whether the enemy starts unaware. Spawners call this right after creating it.</summary>
        public void SetUnaware(bool unaware) => startUnaware = unaware;

        /// <summary>
        /// If the level has worked out its walkable area, walk on it: around walls and other enemies.
        /// Without one (a bare test scene) the enemy falls back to walking straight at the player.
        /// </summary>
        void SetUpPathfinding()
        {
            if (!LevelNavMesh.Ready) return;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return;

            transform.position = hit.position;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.acceleration = 14f;
            agent.angularSpeed = 0f;
            agent.updateRotation = false;
            agent.stoppingDistance = 0f;
            agent.autoBraking = false;
            agent.avoidancePriority = UnityEngine.Random.Range(30, 70);
        }

        bool Pathfinding => agent != null && agent.enabled && agent.isOnNavMesh;

        /// <summary>True if nothing solid stands between this enemy's head and the player's chest.</summary>
        bool HasLineOfSight()
        {
            if (target == null) return false;
            Vector3 from = headBone != null ? headBone.position : transform.position + Vector3.up * 1.5f;
            Vector3 to = target.transform.position + Vector3.up * 1.2f;
            Vector3 offset = to - from;

            int count = Physics.RaycastNonAlloc(from, offset.normalized, sightHits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = sightHits[i].collider;
                // Only the level itself blocks sight: not bodies, weapons or the player.
                if (c.attachedRigidbody == null && !(c is CharacterController)) return false;
            }
            return true;
        }

        float SpeedScale => enraged ? enrageStrength : 1f;

        void Update()
        {
            if (state == State.Dead) return;

            // A choice is being made (blessing cards): hold perfectly still.
            if (WorldFreeze.Active)
            {
                if (!frozen)
                {
                    frozen = true;
                    animator.speed = 0f;
                    if (Pathfinding) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                }
                return;
            }
            if (frozen)
            {
                frozen = false;
                animator.speed = 1f;
            }

            stateTime += Time.deltaTime;
            UpdateTelegraph();
            float desiredSpeed = 0f;
            Vector3 sideStep = Vector3.zero;

            bool hasTarget = target != null && !target.IsDead;
            Vector3 toTarget = hasTarget ? target.transform.position - transform.position : Vector3.zero;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            switch (state)
            {
                case State.Intro:
                case State.Notice:
                case State.Taunt:
                case State.Fall:
                    if (state != State.Intro && state != State.Fall && hasTarget) Face(toTarget, turnSpeed * 0.5f);
                    if (stateTime >= stateDuration)
                    {
                        if (state == State.Notice && UnityEngine.Random.value < tauntChance && body == Body.Standing)
                        {
                            animator.CrossFadeInFixedTime("Taunt", 0.15f);
                            Enter(State.Taunt, 2f);
                            break;
                        }
                        SetMoveClip(BaseMoveClip, true);
                        Enter(state == State.Intro ? State.Idle : State.Chase);
                    }
                    break;

                case State.Unaware:
                    if (hasTarget && distance <= aggroRange && HasLineOfSight()) Notice();
                    break;

                case State.Idle:
                    if (hasTarget && distance <= aggroRange) Enter(State.Chase);
                    break;

                case State.Chase:
                    if (!hasTarget)
                    {
                        Enter(State.Idle);
                        break;
                    }
                    UpdateChase(toTarget, distance, ref desiredSpeed, ref sideStep);
                    break;

                case State.Attack:
                    UpdateAttack(toTarget, hasTarget);
                    break;

                case State.Flinch:
                case State.Stagger:
                    if (state == State.Stagger && resumeStaggerAt > 0f && Time.time >= resumeStaggerAt)
                    {
                        resumeStaggerAt = 0f;
                        if (stateTime < stateDuration - 0.2f) animator.CrossFadeInFixedTime(stunnedState, 0.2f);
                    }
                    if (stateTime >= stateDuration)
                    {
                        SetMoveClip(BaseMoveClip, true);
                        Enter(State.Chase);
                    }
                    break;
            }

            Move(desiredSpeed, sideStep, hasTarget ? target.transform.position : transform.position);
            PlaceExecuteMarker();
        }

        // ---------------------------------------------------------------- Chasing

        void UpdateChase(Vector3 toTarget, float distance, ref float desiredSpeed, ref Vector3 sideStep)
        {
            // A ranged enemy with a wall in the way keeps coming until it can see the player.
            bool throwing = isRanged && !disarmed && body == Body.Standing;
            bool canSee = !throwing || HasLineOfSight();
            float stop = body != Body.Standing ? 0.7f : !throwing ? stopDistance : canSee ? rangedStopDistance : 1.5f;

            // A thrower that is rushed backs away to keep its distance.
            if (isRanged && !disarmed && body == Body.Standing && retreatDistance > 0f)
            {
                if (!retreating && distance < retreatDistance && Time.time >= retreatUntil && RoomBehind(1.5f))
                {
                    retreating = true;
                    retreatUntil = Time.time + 3f;
                }
                if (retreating && (distance > retreatDistance + 2f || Time.time >= retreatUntil || !RoomBehind(0.6f)))
                {
                    retreating = false;
                    retreatUntil = Time.time + 2f;
                }
            }
            else retreating = false;

            if (retreating)
            {
                Face(toTarget, turnSpeed);
                sideStep = -toTarget.normalized * backSpeed * SpeedScale;
                SetMoveClip("Walk_Back");
                return;
            }

            if (distance > stop)
            {
                desiredSpeed = body != Body.Standing ? crawlSpeed : (distance > runDistance ? runSpeed : walkSpeed) * SpeedScale;
                strafeUntil = 0f;
            }

            // Look where it is walking while it paths around things; look at the player once it stops.
            Vector3 heading = Pathfinding && desiredSpeed > 0f ? agent.desiredVelocity : Vector3.zero;
            heading.y = 0f;
            bool turningInPlace = false;
            if (heading.sqrMagnitude > 0.05f) Face(heading, turnSpeed);
            else if (body == Body.Standing && desiredSpeed <= 0f && Vector3.Angle(transform.forward, toTarget) > 45f)
            {
                // Standing still and the player is well off to the side: step around on the spot.
                turningInPlace = true;
                float signed = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
                SetMoveClip(signed < 0f ? "Turn_Left" : "Turn_Right");
                Face(toTarget, turnInPlaceSpeed);
            }
            else Face(toTarget, turnSpeed);

            EnemyAttack next = NextAttack(distance, canSee);
            bool facing = Vector3.Angle(transform.forward, toTarget) < 25f;
            if (next != null && Time.time >= nextAttackTime && facing)
            {
                StartAttack(next);
                return;
            }

            // Waiting for the next attack within reach: circle the player instead of standing still.
            if (body == Body.Standing && !throwing && desiredSpeed <= 0f && !turningInPlace && distance < stop + 0.3f)
            {
                if (strafeUntil <= 0f || Time.time > strafeUntil)
                {
                    strafeSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                    strafeUntil = Time.time + UnityEngine.Random.Range(1.2f, 2.4f);
                }
                Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized) * strafeSign;
                if (!RoomAt(side, 0.8f)) strafeSign = -strafeSign;
                sideStep = Vector3.Cross(Vector3.up, toTarget.normalized) * strafeSign * strafeSpeed * SpeedScale;
                SetMoveClip(strafeSign > 0f ? "Strafe_Left" : "Strafe_Right");
                return;
            }

            if (!turningInPlace) SetMoveClip(desiredSpeed > 0f ? BaseMoveClip : IdleClip);
        }

        /// <summary>The attack to use next from here, or null if none can reach.</summary>
        EnemyAttack NextAttack(float distance, bool canSee)
        {
            EnemyAttack[] set = CurrentAttacks;
            if (set == null || set.Length == 0 || !canSee) return null;

            for (int i = 0; i < set.Length; i++)
            {
                EnemyAttack candidate = set[(attackIndex + i) % set.Length];
                float reach = candidate.ranged ? rangedAttackRange : candidate.range > 0f ? candidate.range : attackRange;
                if (distance > reach) continue;
                if (candidate.ranged ? muzzle == null : HitboxFor(candidate) == null) continue;
                attackIndex += i;
                return candidate;
            }
            return null;
        }

        EnemyAttack[] CurrentAttacks
        {
            get
            {
                if (body == Body.Crawling) return crawlAttacks;
                if (body == Body.Bisected) return bisectedAttacks;
                return disarmed ? unarmedAttacks : attacks;
            }
        }

        string BaseMoveClip => body == Body.Crawling ? "Crawl" : body == Body.Bisected ? "Bisected_Crawl" : "Locomotion";
        string IdleClip => body == Body.Standing && disarmed ? "Unarmed_Idle" : BaseMoveClip;

        /// <summary>Cross-fades to a movement clip unless it is already playing.</summary>
        void SetMoveClip(string clip, bool force = false)
        {
            if (!force && clip == moveClip) return;
            moveClip = clip;
            animator.CrossFadeInFixedTime(clip, 0.25f);
        }

        /// <summary>True if the enemy can walk this far straight back.</summary>
        bool RoomBehind(float meters) => RoomAt(-transform.forward, meters);

        bool RoomAt(Vector3 direction, float meters)
        {
            if (!Pathfinding) return true;
            Vector3 to = transform.position + direction.normalized * meters;
            return !NavMesh.Raycast(transform.position, to, out _, NavMesh.AllAreas);
        }

        void Move(float desiredSpeed, Vector3 sideStep, Vector3 destination)
        {
            if (Pathfinding)
            {
                if (desiredSpeed > 0.01f)
                {
                    agent.isStopped = false;
                    agent.speed = desiredSpeed;
                    // Paths are refreshed a few times a second, not every frame.
                    if (Time.time >= nextPathTime)
                    {
                        nextPathTime = Time.time + 0.2f;
                        agent.SetDestination(destination);
                    }
                }
                else
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.MoveTowards(agent.velocity, Vector3.zero, 20f * Time.deltaTime);
                    // Strafing, backing away and lunges move directly, still kept on the walkable area.
                    if (sideStep.sqrMagnitude > 0f) agent.Move(sideStep * Time.deltaTime);
                }

                speed = agent.velocity.magnitude;
                animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
                Footsteps(speed + sideStep.magnitude);
                return;
            }

            speed = Mathf.MoveTowards(speed, desiredSpeed, 8f * Time.deltaTime);
            Vector3 step = transform.forward * (speed * Time.deltaTime) + sideStep * Time.deltaTime;

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
            Footsteps(speed + sideStep.magnitude);
        }

        /// <summary>A footfall every stride while walking; dragging along the ground makes a softer scrape.</summary>
        void Footsteps(float groundSpeed)
        {
            if (groundSpeed < 0.2f) return;
            stride += groundSpeed * Time.deltaTime;
            float length = (body == Body.Standing ? 0.8f : 0.5f) * transform.lossyScale.x;
            if (stride < length) return;
            stride = 0f;
            Audio.Sfx.Play(Audio.SfxKind.Footstep, transform.position, body == Body.Standing ? Mathf.Clamp01(groundSpeed / 3f) * 0.7f + 0.2f : 0.25f, VoicePitch);
        }

        void Face(Vector3 direction, float degreesPerSecond)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, degreesPerSecond * Time.deltaTime);
        }

        void Notice()
        {
            animator.CrossFadeInFixedTime("Notice", 0.2f);
            Enter(State.Notice, 1f);
        }

        // ---------------------------------------------------------------- Attacking

        void StartAttack(EnemyAttack next)
        {
            attack = next;
            attackIndex++;
            strikingHitbox = attack.ranged ? null : HitboxFor(attack);

            // A bolt gathers in the hand during the wind-up, which is the player's warning.
            if (attack.ranged) heldBolt = Projectile.Spawn(muzzle, transform, boltSpeed, attack.damage * damageMultiplier);
            hitOpened = false;
            hitIndex = 0;
            lungeDone = 0f;
            animator.CrossFadeInFixedTime(attack.stateName, 0.1f);
            moveClip = attack.stateName;
            Enter(State.Attack);
            Audio.Sfx.Play(attack.ranged ? Audio.SfxKind.BoltCharge : Audio.SfxKind.EnemyAttack, attack.ranged && muzzle != null ? muzzle.position : Mouth, 0.8f, VoicePitch);
            CombatLog.Log(name + " ATTACK " + attack.stateName + " (" + attack.duration.ToString("F2") + "s, window " + attack.hitStart.ToString("F2") + "-" + attack.hitEnd.ToString("F2") + ", " + (attack.damage * damageMultiplier).ToString("F0") + " dmg)");
        }

        void UpdateAttack(Vector3 toTarget, bool hasTarget)
        {
            // Keeps tracking the player during the wind-up, then commits to the swing.
            if (stateTime < attack.hitStart && hasTarget) Face(toTarget, turnSpeed * 0.4f);

            // A lunge carries the enemy forward until the player is where the weapon will strike.
            if (attack.lungeDistance > 0f && stateTime >= attack.lungeTime.x && lungeDone < attack.lungeDistance)
            {
                float span = Mathf.Max(0.01f, attack.lungeTime.y - attack.lungeTime.x);
                float step = Mathf.Min(attack.lungeDistance / span * Time.deltaTime, attack.lungeDistance - lungeDone);
                bool roomLeft = !hasTarget || toTarget.magnitude > Mathf.Max(0.8f, attack.lungeStopAt * transform.lossyScale.x);
                if (roomLeft && stateTime <= attack.lungeTime.y + 0.05f)
                {
                    Vector3 delta = transform.forward * step;
                    if (Pathfinding) agent.Move(delta);
                    else transform.position += delta;
                }
                lungeDone += step;
            }

            // An attack has one strike, or several in a row for a combo.
            int strikes = 1 + (attack.extraHits != null ? attack.extraHits.Length : 0);
            if (hitIndex < strikes)
            {
                float start = hitIndex == 0 ? attack.hitStart : attack.extraHits[hitIndex - 1].x;
                float end = hitIndex == 0 ? attack.hitEnd : attack.extraHits[hitIndex - 1].y;

                if (!hitOpened && stateTime >= start)
                {
                    hitOpened = true;
                    CombatLog.Log(name + " " + attack.stateName + " strike " + (hitIndex + 1) + "/" + strikes + " OPEN at " + stateTime.ToString("F3") + "s (planned " + start.ToString("F2") + ")");
                    if (attack.ranged)
                    {
                        // The bolt leaves the hand.
                        if (heldBolt != null) heldBolt.Launch();
                        heldBolt = null;
                    }
                    else if (strikingHitbox != null) strikingHitbox.Begin(attack.damage * damageMultiplier, attack.damageType, attack.guardBreak);
                }
                if (hitOpened && stateTime >= end)
                {
                    hitOpened = false;
                    CombatLog.Log(name + " " + attack.stateName + " strike " + (hitIndex + 1) + "/" + strikes + " CLOSE at " + stateTime.ToString("F3") + "s (planned " + end.ToString("F2") + ")");
                    hitIndex++;
                    EndStrike();
                }
            }
            if (stateTime >= attack.duration && !TryFollowUp(toTarget, hasTarget)) FinishAttack();
        }

        bool followingUp, pressOpening;

        /// <summary>
        /// Elites: sometimes chains straight into a second attack instead of pausing, so a block is not the end of
        /// the exchange. Never more than two in a row.
        /// </summary>
        bool TryFollowUp(Vector3 toTarget, bool hasTarget)
        {
            if (followingUp)
            {
                followingUp = false;
                return false;
            }
            if (followUpChance <= 0f || !hasTarget || body != Body.Standing || disarmed || attack.ranged) return false;
            if (UnityEngine.Random.value >= followUpChance) return false;

            EnemyAttack previous = attack;
            EnemyAttack next = NextAttack(toTarget.magnitude, HasLineOfSight());
            if (next == previous) next = NextAttack(toTarget.magnitude, HasLineOfSight());
            if (next == null || next.ranged) return false;

            EndStrike();
            StartAttack(next);
            followingUp = true;
            return true;
        }

        void EndStrike()
        {
            if (strikingHitbox != null) strikingHitbox.End();
        }

        void FinishAttack()
        {
            EndStrike();
            followingUp = false;
            // After breaking the guard it comes again almost at once, while the player is still open.
            float pause = pressOpening ? pressOpeningPause : UnityEngine.Random.Range(timeBetweenAttacks.x, timeBetweenAttacks.y) / SpeedScale;
            pressOpening = false;
            nextAttackTime = Time.time + pause;
            SetMoveClip(BaseMoveClip, true);
            Enter(State.Chase);
        }

        /// <summary>What strikes for this attack: the weapon, or a bare hand that is still attached.</summary>
        MeleeAttackHitbox HitboxFor(EnemyAttack forAttack)
        {
            if (forAttack.limb == AttackLimb.Weapon) return disarmed ? null : weaponHitbox;

            AttackLimb limb = forAttack.limb;
            if (!HandAttached(limb)) limb = limb == AttackLimb.LeftHand ? AttackLimb.RightHand : AttackLimb.LeftHand;
            if (!HandAttached(limb)) return null;

            if (handHitboxes.TryGetValue(limb, out MeleeAttackHitbox existing) && existing != null) return existing;

            // A fist-sized striking surface that follows the hand through the physics engine.
            Transform hand = animator.GetBoneTransform(limb == AttackLimb.LeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var go = new GameObject(name + " " + limb + " Hitbox");
            go.transform.SetPositionAndRotation(hand.position, hand.rotation);
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.16f * transform.lossyScale.x;
            var hitboxBody = go.AddComponent<Rigidbody>();
            hitboxBody.isKinematic = true;
            hitboxBody.useGravity = false;
            var hitbox = go.AddComponent<MeleeAttackHitbox>();
            go.AddComponent<KinematicFollower>().target = hand;
            hitbox.Blocked += OnBlocked;
            hitbox.HitPlayer += OnHitPlayer;
            handHitboxes[limb] = hitbox;
            return hitbox;
        }

        bool HandAttached(AttackLimb limb)
        {
            if (animator == null || !animator.isHuman) return false;
            Transform hand = animator.GetBoneTransform(limb == AttackLimb.LeftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            return hand != null && (dismemberment == null || !dismemberment.IsSevered(hand));
        }

        void OnHitPlayer(PlayerHealth player)
        {
            // Thorns: part of the blow comes back at the attacker.
            float thorns = PlayerStats.Bonus(StatType.Thorns);
            if (thorns <= 0f || attack == null || state == State.Dead) return;
            health.TakeDamage(new DamageInfo
            {
                Amount = attack.damage * damageMultiplier * thorns,
                Type = DamageType.Stab,
                Point = transform.position + Vector3.up * 1.2f,
                Direction = -transform.forward,
                Source = player.gameObject
            });
        }

        /// <summary>
        /// The attack met something the player held. Only a parry knocks this enemy off balance; a plain block just
        /// ends this strike and the attack plays on (a combo's next strike can still land); a broken guard is an
        /// opening it presses.
        /// </summary>
        void OnBlocked(Vector3 point, PlayerGuard.Outcome outcome)
        {
            if (state == State.Dead) return;
            bool parried = outcome == PlayerGuard.Outcome.Parried;

            CombatLog.Log(name + " " + outcome + " during " + (attack != null && state == State.Attack ? attack.stateName + " at " + stateTime.ToString("F3") + "s" : state.ToString()));
            EndStrike();
            if (outcome != PlayerGuard.Outcome.GuardBroken)
            {
                DamagePopup.Spawn(point, parried ? "PARRY" : "BLOCK", parried ? new Color(1f, 0.85f, 0.2f) : Color.white);
                Audio.Sfx.Play(parried ? Audio.SfxKind.Parry : Audio.SfxKind.Block, point);
            }

            if (!parried)
            {
                if (outcome == PlayerGuard.Outcome.GuardBroken) pressOpening = true;
                // On the ground there is nothing to follow through with: the attack simply stops.
                if (body != Body.Standing) FinishAttack();
                return;
            }

            nextAttackTime = Time.time + timeBetweenAttacks.x;
            followingUp = false;
            if (body != Body.Standing)
            {
                FinishAttack();
                return;
            }
            animator.CrossFadeInFixedTime(stunnedState, 0.15f);
            Enter(State.Stagger, parriedStun);
        }

        // ---------------------------------------------------------------- Getting hurt

        void OnDamaged(DamageInfo info, HitZoneType zone)
        {
            if (state == State.Dead) return;

            CombatLog.Log(name + " reacts: state " + state + ", stagger " + (info.Stagger > 0f ? info.Stagger : info.Amount).ToString("F1")
                + " (flinch>=" + flinchThreshold + ", stun>=" + heavyStaggerThreshold + (Time.time < flinchImmuneUntil ? ", poise up" : "") + ")");

            if (Time.time >= nextHurtSound)
            {
                nextHurtSound = Time.time + 0.6f;
                Audio.Sfx.Play(Audio.SfxKind.EnemyHurt, Mouth, 0.8f, VoicePitch);
            }

            // Taken by surprise.
            if (state == State.Unaware)
            {
                Notice();
                return;
            }

            // Below half health, a boss roars and fights harder.
            if (enrageBelow > 0f && !enraged && health.Current > 0f && health.Current <= health.Max * enrageBelow && body == Body.Standing)
            {
                enraged = true;
                damageMultiplier *= enrageStrength;
                EndStrike();
                if (heldBolt != null) { heldBolt.Fizzle(); heldBolt = null; }
                animator.CrossFadeInFixedTime(enrageState, 0.15f);
                Enter(State.Taunt, enrageDuration);
                Audio.Sfx.Play(Audio.SfxKind.EnemyRoar, Mouth, 1f, VoicePitch);
                Run.Announcer.Show("The guardian is enraged!", new Color(1f, 0.25f, 0.15f), 2.5f);
                return;
            }

            // On the ground there are no hit reactions; blows just hurt.
            if (body != Body.Standing || state == State.Fall || state == State.Taunt) return;

            // React away from the side the blow came from.
            // The weapon's travel tells which side it came from: moving to the enemy's right means it struck the left side.
            Vector3 travel = transform.InverseTransformDirection(info.Direction);
            string reaction = travel.z > 0.6f ? hitBackState
                : travel.x > 0.4f ? hitLeftState
                : travel.x < -0.4f ? hitRightState
                : hitFrontState;

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
                EndStrike();
                nextAttackTime = Time.time + heavyStun;
                animator.CrossFadeInFixedTime(stunnedState, 0.15f);
                Enter(State.Stagger, heavyStun);
                return;
            }

            if (state == State.Idle) Enter(State.Chase);
            if (force < flinchThreshold || Time.time < flinchImmuneUntil) return;

            EndStrike();
            flinchImmuneUntil = Time.time + flinchDuration + poiseTime;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + flinchDuration);
            animator.CrossFadeInFixedTime(reaction, 0.08f, 0, 0f);
            Enter(State.Flinch, flinchDuration);
        }

        /// <summary>
        /// True if the shield takes this blow: it comes from the front, the enemy is ready (not mid-swing, reeling
        /// or on the ground) and still has its shield arm, and the blow is not heavy enough to smash through.
        /// </summary>
        bool ShieldStops(DamageInfo info)
        {
            if (shield == null || body != Body.Standing) return false;
            if (state == State.Attack || state == State.Stagger || state == State.Dead || state == State.Unaware) return false;
            // Reeling from a real blow leaves it open; reeling from a blow the shield took does not.
            if (state == State.Flinch && !shieldRecoiling) return false;
            if (dismemberment != null && dismemberment.IsSevered(shield)) return false;
            bool onShield = info.HitCollider != null && info.HitCollider.transform.IsChildOf(shield);
            if (!onShield && Vector3.Dot(info.Direction, transform.forward) > -0.25f) return false;

            float force = info.Stagger > 0f ? info.Stagger : info.Amount;
            if (force >= shieldBreak)
            {
                // Smashed through: the shield is knocked aside and the enemy reels.
                DamagePopup.Spawn(info.Point, "GUARD BROKEN", new Color(1f, 0.6f, 0.2f));
                return false;
            }

            DamagePopup.Spawn(info.Point, "SHIELD", new Color(0.8f, 0.85f, 0.9f));
            Audio.Sfx.Play(Audio.SfxKind.Block, info.Point);
            HitSpark.Spawn(info.Point, -info.Direction, new Color(1f, 0.95f, 0.8f), 10);
            animator.CrossFadeInFixedTime(blockedState, 0.08f, 0, 0f);
            Enter(State.Flinch, 0.45f);
            shieldRecoiling = true;
            return true;
        }

        /// <summary>Lost both legs' worth of standing: drops to the ground and crawls on with its arms.</summary>
        public void StartCrawling()
        {
            if (state == State.Dead || body != Body.Standing) return;
            body = Body.Crawling;
            DropWeapon();
            EndStrike();
            animator.CrossFadeInFixedTime("Fall_ToCrawl", 0.1f);
            moveClip = "Fall_ToCrawl";
            Enter(State.Fall, 1.2f);
            CombatLog.Log(name + " falls and CRAWLS on");
        }

        /// <summary>Cut in half while still alive: the upper body drags itself on.</summary>
        public void StartBisected()
        {
            if (state == State.Dead || body == Body.Bisected) return;
            body = Body.Bisected;
            DropWeapon();
            EndStrike();
            if (agent != null) agent.height = 0.8f;
            animator.CrossFadeInFixedTime("Bisected_Fall", 0.05f);
            moveClip = "Bisected_Fall";
            Enter(State.Fall, 1f);
            CombatLog.Log(name + " cut in half and the upper body CRAWLS on");
        }

        void OnDied(DamageInfo info)
        {
            CombatLog.Log(name + " DIED" + (info.Execution ? " (EXECUTION)" : "") + " by " + info.Type + " to " + CombatLog.Where(info.HitCollider));
            Enter(State.Dead);
            animator.speed = 1f;
            Audio.Sfx.Play(info.Execution ? Audio.SfxKind.Execution : Audio.SfxKind.EnemyDeath, Mouth, 1f, VoicePitch);
            if (agent != null) agent.enabled = false;
            SetExecuteMarker(false);
            Disarm();
            foreach (var hitbox in handHitboxes.Values)
                if (hitbox != null) hitbox.End();

            if (info.Execution)
            {
                DamagePopup.Spawn(info.Point + Vector3.up * 0.2f, "EXECUTION", new Color(1f, 0.3f, 0.1f));
                if (target != null) target.Heal(executionHeal);
                PlayerStamina.Restore(executionStamina);
            }

            // An upper body crawling on its own slumps where it lies.
            if (body == Body.Bisected)
            {
                animator.CrossFadeInFixedTime("Bisected_Death", 0.15f);
                Destroy(gameObject, corpseLifetime);
                return;
            }

            // Bosses die on their feet first: the death animation plays, then the body goes limp.
            bool intact = dismemberment == null || !dismemberment.HasSevered;
            if (!string.IsNullOrEmpty(deathState) && intact && !info.Execution)
            {
                animator.CrossFadeInFixedTime(deathState, 0.15f);
                StartCoroutine(GoLimpAfter(deathDuration));
                Destroy(gameObject, corpseLifetime + deathDuration);
                return;
            }

            // Some deaths are a fall rather than a ragdoll: backwards if struck from the front, forwards from behind.
            if (intact && !info.Execution && body == Body.Standing && UnityEngine.Random.value < deathAnimationChance)
            {
                bool fromFront = Vector3.Dot(info.Direction, transform.forward) < 0f;
                animator.CrossFadeInFixedTime(fromFront ? "Death_Front" : "Death_Back", 0.1f);
                Destroy(gameObject, corpseLifetime);
                return;
            }

            if (ragdoll != null)
            {
                float push = Mathf.Clamp(info.ImpactSpeed, 2f, 10f) * deathImpulsePerSpeed;
                ragdoll.Activate(info.Direction * push, info.Point);
            }

            Destroy(gameObject, corpseLifetime);
        }

        System.Collections.IEnumerator GoLimpAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (ragdoll != null) ragdoll.Activate(Vector3.zero, transform.position);
        }

        // ---------------------------------------------------------------- Guard-break warning

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly Color WarningColor = new Color(1f, 0.25f, 0.08f);
        Light warningLight;
        Renderer[] weaponRenderers;
        Color[] weaponBaseColors;
        MaterialPropertyBlock warningBlock;
        float warning;

        /// <summary>
        /// A guard-breaking attack is announced: the weapon glows ember-red through the wind-up, brighter and
        /// pulsing faster as the strike nears, and goes dark once the strike is over. Parry it or get out of reach.
        /// Costs nothing between such attacks (one small light and a property block, made the first time).
        /// </summary>
        void UpdateTelegraph()
        {
            bool show = state == State.Attack && attack != null && attack.guardBreak && !disarmed && weaponVisual != null
                        && stateTime < attack.hitEnd;
            float target = 0f;
            if (show)
            {
                float nearing = Mathf.Clamp01(stateTime / Mathf.Max(0.1f, attack.hitStart));
                target = Mathf.Lerp(0.35f, 1f, nearing) * (0.8f + 0.2f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 22f, nearing)));
            }
            if (warning <= 0f && target <= 0f) return;
            warning = Mathf.MoveTowards(warning, target, Time.deltaTime * 6f);

            if (warningLight == null)
            {
                if (weaponVisual == null) return;
                weaponRenderers = weaponVisual.GetComponentsInChildren<Renderer>();
                weaponBaseColors = new Color[weaponRenderers.Length];
                Bounds bounds = new Bounds(weaponVisual.position, Vector3.zero);
                for (int i = 0; i < weaponRenderers.Length; i++)
                {
                    Material material = weaponRenderers[i].sharedMaterial;
                    weaponBaseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                    bounds.Encapsulate(weaponRenderers[i].bounds);
                }
                warningLight = new GameObject("Guard Break Warning").AddComponent<Light>();
                warningLight.transform.SetParent(weaponVisual, false);
                warningLight.transform.position = bounds.center;
                warningLight.type = LightType.Point;
                warningLight.color = WarningColor;
                warningLight.range = 2.2f;
                warningLight.shadows = LightShadows.None;
                warningBlock = new MaterialPropertyBlock();
            }

            warningLight.enabled = warning > 0.01f;
            warningLight.intensity = warning * 3.5f;
            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                Renderer weaponRenderer = weaponRenderers[i];
                if (weaponRenderer == null) continue;
                if (warning <= 0.01f)
                {
                    // Back to the shared material, so the weapon batches normally again.
                    weaponRenderer.SetPropertyBlock(null);
                    continue;
                }
                warningBlock.SetColor(BaseColorId, Color.Lerp(weaponBaseColors[i], WarningColor, warning * 0.6f));
                warningBlock.SetColor(EmissionColorId, WarningColor * (warning * 2f));
                weaponRenderer.SetPropertyBlock(warningBlock);
            }
        }

        // ---------------------------------------------------------------- Two-handed grip

        /// <summary>How far down the handle (meters, from the weapon hand) the off hand closes.</summary>
        const float OffHandGap = 0.11f;
        float offHandWeight;
        // During OnAnimatorIK the bones still hold last frame's pose, but the IK goals hold this frame's. The weapon
        // hand's goal rotation differs from its bone rotation by a fixed avatar offset, measured once in LateUpdate.
        Quaternion weaponHandGoal;
        Quaternion goalToBone = Quaternion.identity;
        bool haveGoalToBone, sawGoal;

        void LateUpdate()
        {
            // Bones now hold this frame's pose; the weapon hand had no IK, so its bone matches the goal read this frame.
            if (haveGoalToBone || !sawGoal || offHandWeight > 0f || animator == null || !animator.isHuman) return;
            Transform weaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (weaponHand == null) return;
            goalToBone = Quaternion.Inverse(weaponHandGoal) * weaponHand.rotation;
            haveGoalToBone = true;
        }

        /// <summary>
        /// Two-handed attacks: the clips only really animate the weapon hand, and the off hand wanders (during a
        /// heavy wind-up it ended up behind the head). Humanoid IK puts it on the handle just below the weapon hand,
        /// with the elbow kept low and out to the side, blending in at the start of the swing and out at the end.
        /// Runs only while such an attack plays (the weight is zero otherwise), so it costs nothing in between.
        /// Needs "IK Pass" on the controller's base layer (EnemyBuilder sets it).
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman) return;
            weaponHandGoal = animator.GetIKRotation(AvatarIKGoal.RightHand);
            sawGoal = true;
            bool wanted = state == State.Attack && attack != null && attack.twoHanded && attack.limb == AttackLimb.Weapon
                          && !disarmed && body == Body.Standing && shield == null && weaponVisual != null && HandAttached(AttackLimb.LeftHand);
            // Let go a little before the clip ends, so the hands part naturally on the recovery.
            if (wanted && stateTime > attack.duration - 0.25f) wanted = false;
            offHandWeight = Mathf.MoveTowards(offHandWeight, wanted ? 1f : 0f, Time.deltaTime / 0.15f);

            if (offHandWeight <= 0f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                return;
            }

            Transform weaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (weaponHand == null) return;

            // The grip point, fixed relative to the weapon hand (the weapon is rigidly held)...
            Vector3 gripWorld = weaponVisual.position - weaponVisual.forward * (OffHandGap * weaponVisual.lossyScale.z);
            Vector3 gripInHand = Quaternion.Inverse(weaponHand.rotation) * (gripWorld - weaponHand.position);
            // ...placed on this frame's weapon hand (from its IK goal), not last frame's, so it does not trail the swing.
            Vector3 grip = gripWorld;
            if (haveGoalToBone)
                grip = animator.GetIKPosition(AvatarIKGoal.RightHand) + weaponHandGoal * goalToBone * gripInHand;

            float eased = offHandWeight * offHandWeight * (3f - 2f * offHandWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, eased);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, grip);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, eased * offHandTurn);
            if (offHandTurn > 0f) animator.SetIKRotation(AvatarIKGoal.LeftHand, weaponHandGoal);

            if (shoulder != null)
            {
                // Elbow halfway between shoulder and grip, pushed down and out to the left: it can never fold back.
                Vector3 elbow = Vector3.Lerp(shoulder.position, grip, 0.5f) - transform.right * 0.25f + Vector3.down * 0.25f;
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, eased * 0.7f);
                animator.SetIKHintPosition(AvatarIKHint.LeftElbow, elbow);
            }
        }

        /// <summary>True if the enemy's weapon is held by the given bone or anything below it (e.g. its sword arm).</summary>
        public bool HoldsWeaponUnder(Transform bone)
        {
            if (disarmed) return false;
            if (isRanged) return muzzle != null && muzzle.IsChildOf(bone);
            return weaponVisual != null && weaponVisual.IsChildOf(bone);
        }

        /// <summary>The weapon falls out of the enemy's hand as a plain physics object. It cannot attack with it afterwards.</summary>
        public void Disarm()
        {
            if (disarmed) return;
            DropWeapon();

            // Still standing: it fights on with its fists.
            if (state == State.Attack && attack != null && attack.limb == AttackLimb.Weapon)
            {
                SetMoveClip(BaseMoveClip, true);
                Enter(State.Chase);
            }
        }

        void DropWeapon()
        {
            if (disarmed) return;
            disarmed = true;

            // The dropped weapon gets the same collision shape its striking surface had.
            Vector3 shapeCentre = new Vector3(0f, 0f, 0.4f), shapeSize = new Vector3(0.05f, 0.02f, 0.95f);
            if (weaponHitbox != null)
            {
                var shape = weaponHitbox.GetComponent<BoxCollider>();
                if (shape != null)
                {
                    shapeCentre = shape.center;
                    shapeSize = shape.size;
                }
                weaponHitbox.End();
                Destroy(weaponHitbox.gameObject);
                weaponHitbox = null;
            }

            if (weaponVisual != null && weaponVisual.GetComponent<Rigidbody>() == null)
            {
                weaponVisual.SetParent(null, true);
                var blade = weaponVisual.gameObject.AddComponent<BoxCollider>();
                blade.center = shapeCentre;
                blade.size = shapeSize;
                weaponVisual.gameObject.AddComponent<Rigidbody>().mass = 1.5f;
                Destroy(weaponVisual.gameObject, corpseLifetime + 5f);
            }
        }

        /// <summary>A marker over the head of a stunned enemy: hit the head (or stab the body) now to finish it.</summary>
        void SetExecuteMarker(bool show)
        {
            if (!show)
            {
                if (executeMarker != null) FxLibrary.Release(executeMarker);
                executeMarker = null;
                return;
            }
            if (executeMarker != null) return;

            // Prefabs/Effects/ExecuteMarker (Roguelike > Build Effect Prefabs), from the effect pool.
            executeMarker = FxLibrary.Spawn(FxKind.ExecuteMarker, transform.position, Quaternion.identity, transform);
            PlaceExecuteMarker();
        }

        void PlaceExecuteMarker()
        {
            if (executeMarker == null) return;
            Vector3 top = headBone != null ? headBone.position : transform.position + Vector3.up * 1.6f;
            executeMarker.transform.position = top + Vector3.up * (0.32f + Mathf.Sin(Time.time * 6f) * 0.03f);
        }

        void Enter(State next, float duration = 0f)
        {
            SetExecuteMarker(next == State.Stagger);
            // Interrupted mid wind-up: the bolt never leaves the hand.
            if (next != State.Attack && heldBolt != null)
            {
                heldBolt.Fizzle();
                heldBolt = null;
            }
            if (next != State.Chase) retreating = false;
            shieldRecoiling = false;
            state = next;
            resumeStaggerAt = 0f;
            stateTime = 0f;
            stateDuration = duration;
        }
    }
}
