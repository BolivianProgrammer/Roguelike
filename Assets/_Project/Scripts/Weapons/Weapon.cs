using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Weapons
{
    /// <summary>
    /// Turns physical impacts of a held weapon into damage, scaled by how fast it was swung
    /// and by how it landed: edge-first (slash), point-first (stab) or flat (blunt).
    /// Convention: the weapon's forward (blue) axis runs along the blade, its right (red) axis points at the edge.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class Weapon : MonoBehaviour
    {
        [SerializeField] WeaponDefinition definition;
        [Tooltip("Colliders that deal damage, such as the blade. Leave empty to let every collider deal damage.")]
        [SerializeField] Collider[] damageColliders;

        [Header("Hit type detection")]
        [Tooltip("A hit counts as a stab when the weapon moves within this angle (degrees) of its own length.")]
        [SerializeField, Range(5f, 60f)] float stabAngle = 35f;
        [Tooltip("A hit counts as a slash when the swing is within this angle (degrees) of the edge direction.")]
        [SerializeField, Range(10f, 80f)] float edgeAngle = 50f;

        /// <summary>Share of damage a hit keeps when the player has no stamina left for it.</summary>
        const float TiredDamageScale = 0.5f;
        /// <summary>How long (seconds) the weapon may slow down mid-swing before the swing counts as over.</summary>
        const float SwingResetTime = 0.1f;

        /// <summary>
        /// When true, weapons only deal damage during an attack window. Mouse-and-keyboard play turns this on,
        /// because there the weapon also moves whenever the camera turns. In VR it stays off.
        /// </summary>
        public static bool RequireAttackWindow;

        public static float AttackWindowUntil;
        public static float AttackWindowDamageScale = 1f;
        public static float AttackWindowStaggerScale = 1f;
        public static float AttackWindowStaminaScale = 1f;

        /// <summary>Opens a window in which held weapons can deal damage, e.g. for one desktop swing.</summary>
        public static void OpenAttackWindow(float duration, float damageScale = 1f, float staggerScale = 1f, float staminaScale = 1f)
        {
            AttackWindowUntil = Time.time + duration;
            AttackWindowDamageScale = damageScale;
            AttackWindowStaggerScale = staggerScale;
            AttackWindowStaminaScale = staminaScale;
        }

        /// <summary>
        /// The player's own arm must be swinging at least this share of the weapon's minimum hit speed.
        /// The physics weapon can be flung by collisions (walking into an enemy, an enemy running into the blade);
        /// the controller in the player's hand cannot, so it decides whether a swing is real.
        /// </summary>
        const float ArmSpeedShare = 0.6f;
        /// <summary>Faster than any arm can move: a tracking jump, not a swing.</summary>
        const float ImpossibleArmSpeed = 30f;
        /// <summary>How long a burst of arm speed still counts for a hit, in seconds.</summary>
        const float ArmSpeedMemory = 0.12f;

        Grabbable grabbable;
        WeaponSpecial special;
        float swingDistance;
        float slowTime;
        Vector3 lastArmPoint;
        bool hasArmPoint;
        float armSpeed;
        float armSpeedTime;
        readonly Dictionary<IDamageable, float> lastHitTimes = new Dictionary<IDamageable, float>();

        public WeaponDefinition Definition => definition;
        public Grabbable Grabbable => grabbable;
        /// <summary>This weapon's special attack, or null if it has none.</summary>
        public WeaponSpecial Special => special;

        /// <summary>Arms the weapon's special attack if it has one and it is off cooldown.</summary>
        public bool ActivateSpecial() => special != null && special.TryActivate();

        void Awake()
        {
            grabbable = GetComponent<Grabbable>();
            special = GetComponent<WeaponSpecial>();
            if (definition != null)
                GetComponent<Rigidbody>().mass = definition.mass;
        }

        void FixedUpdate()
        {
            if (definition == null) return;

            // Track how far the striking part has travelled at swing speed since it last slowed down.
            Vector3 point = StrikePoint();
            float speed = (grabbable.Body.GetPointVelocity(point) - PlayerBody.RigVelocity).magnitude;
            if (grabbable.IsHeld && speed >= definition.minHitSpeed * 0.75f)
            {
                swingDistance += speed * Time.fixedDeltaTime;
                slowTime = 0f;
            }
            else
            {
                slowTime += Time.fixedDeltaTime;
                if (slowTime > SwingResetTime) swingDistance = 0f;
            }
        }

        /// <summary>
        /// Where the striking part would be if the weapon followed the player's real hand (the controller) exactly,
        /// measured every rendered frame after the controller has moved.
        /// </summary>
        void LateUpdate()
        {
            PhysicsHandGrabber holder = grabbable.Holder;
            Transform controller = holder != null && holder.Hand != null ? holder.Hand.Target : null;
            if (controller == null || Time.deltaTime <= 0f)
            {
                hasArmPoint = false;
                return;
            }

            Vector3 strikeInHand = holder.Hand.transform.InverseTransformPoint(StrikePoint());
            // Measured in the tracking space (the controller's parent, which carries walking, sprinting and turning),
            // so only the player's real arm movement counts: running with a sword in hand is not a swing.
            Transform space = controller.parent;
            Vector3 armPoint = controller.TransformPoint(strikeInHand);
            if (space != null) armPoint = space.InverseTransformPoint(armPoint);
            if (hasArmPoint)
            {
                Vector3 velocity = (armPoint - lastArmPoint) / Time.deltaTime;
                if (space == null) velocity -= PlayerBody.RigVelocity;
                float speed = velocity.magnitude;
                if (speed < ImpossibleArmSpeed && (speed >= armSpeed || Time.time - armSpeedTime > ArmSpeedMemory))
                {
                    armSpeed = speed;
                    armSpeedTime = Time.time;
                }
            }
            lastArmPoint = armPoint;
            hasArmPoint = true;

            // A real swing whooshes (louder the faster it is) and leaves a faint trail.
            float swingSpeed = RecentArmSpeed;
            bool swinging = swingSpeed >= definition.fullDamageSpeed * 0.55f;
            if (swinging && Time.time >= nextWhooshTime)
            {
                nextWhooshTime = Time.time + 0.35f;
                float loudness = Mathf.InverseLerp(definition.fullDamageSpeed * 0.55f, definition.fullDamageSpeed * 1.3f, swingSpeed);
                Audio.Sfx.Play(definition.mass >= 2.5f ? Audio.SfxKind.WhooshHeavy : Audio.SfxKind.Whoosh, StrikePoint(), Mathf.Lerp(0.35f, 1f, loudness));
            }
            if (trail == null) CreateTrail();
            if (trail != null) trail.emitting = swingSpeed >= definition.minHitSpeed * 1.5f;
        }

        float nextWhooshTime;
        TrailRenderer trail;

        /// <summary>A thin streak behind the striking part, shown only while it is really being swung.</summary>
        void CreateTrail()
        {
            Collider striker = damageColliders != null && damageColliders.Length > 0 ? damageColliders[0] : null;
            var go = new GameObject("Swing Trail");
            go.transform.SetParent(striker != null ? striker.transform : transform, false);
            var box = striker as BoxCollider;
            go.transform.localPosition = box != null ? box.center + Vector3.forward * box.size.z * 0.4f : Vector3.forward * 0.6f;
            trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.minVertexDistance = 0.02f;
            trail.widthMultiplier = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = FlatMaterials.Shared;
            trail.startColor = new Color(1f, 1f, 1f, 0.35f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
        }

        /// <summary>The fastest the player's own arm moved the striking part in the last moment.</summary>
        float RecentArmSpeed => Time.time - armSpeedTime <= ArmSpeedMemory ? armSpeed : 0f;

        Vector3 StrikePoint()
        {
            if (damageColliders != null && damageColliders.Length > 0 && damageColliders[0] != null)
                return damageColliders[0].bounds.center;
            return transform.position + transform.forward * 0.4f;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (definition == null || !grabbable.IsHeld) return;

            ContactPoint contact = collision.GetContact(0);
            if (!IsDamageCollider(contact.thisCollider)) return;

            // Clashing with an enemy's weapon is not a hit on the enemy.
            if (collision.collider.GetComponentInParent<MeleeAttackHitbox>() != null) return;

            var target = collision.collider.GetComponentInParent<IDamageable>();
            if (target == null) return;

            // With mouse and keyboard, only a deliberate attack can hurt; turning the camera must not.
            bool inWindow = Time.time <= AttackWindowUntil;
            string where = CombatLog.Enabled ? definition.name + " -> " + CombatLog.Where(collision.collider) : null;
            if (RequireAttackWindow && !inWindow)
            {
                CombatLog.Log("NO HIT " + where + ": outside attack window");
                return;
            }

            // The solver has already slowed the weapon down by now, so use the motion from just before the impact.
            // Only the weapon's own swing counts: not the player walking, and not the enemy walking into it.
            Vector3 velocity = grabbable.VelocityBeforeImpact(contact.point) - PlayerBody.RigVelocity;
            DamageType type = Classify(velocity);
            float speed = velocity.magnitude;

            // The weapon has to be travelling into the target, not being dragged across or away from it.
            if (Vector3.Dot(velocity, collision.collider.bounds.center - contact.point) <= 0f)
            {
                CombatLog.Log("NO HIT " + where + ": moving away from target (" + speed.ToString("F2") + " m/s)");
                return;
            }

            // The player's own arm has to be swinging: a blade knocked about by a collision does not count.
            float arm = RecentArmSpeed;
            if (arm < definition.minHitSpeed * ArmSpeedShare)
            {
                CombatLog.Log("NO HIT " + where + ": arm only moving " + arm.ToString("F2") + " m/s (weapon " + speed.ToString("F2") + " m/s)");
                return;
            }

            // A real swing covers some distance first. This is what stops a resting or nudged weapon from hurting.
            float needed = definition.minSwingDistance * (type == DamageType.Stab ? 0.5f : 1f);
            if (swingDistance < needed)
            {
                CombatLog.Log("NO HIT " + where + ": swing " + swingDistance.ToString("F2") + " m < " + needed.ToString("F2") + " m (" + speed.ToString("F2") + " m/s)");
                return;
            }

            float damage = definition.CalculateDamage(speed, type);
            if (damage <= 0f)
            {
                CombatLog.Log("NO HIT " + where + ": too slow, " + speed.ToString("F2") + " m/s " + type);
                return;
            }

            float hitCooldown = definition.hitCooldown * (special != null ? special.HitCooldownScale : 1f);
            if (lastHitTimes.TryGetValue(target, out float lastHit) && Time.time - lastHit < hitCooldown)
            {
                CombatLog.Log("NO HIT " + where + ": hit cooldown");
                return;
            }
            lastHitTimes[target] = Time.time;

            damage *= PlayerStats.Get(StatType.Damage);
            float stagger = damage * definition.staggerMultiplier * PlayerStats.Get(StatType.StaggerPower);
            float staminaCost = definition.StaminaCost * PlayerStats.Get(StatType.StaminaCost);
            if (inWindow)
            {
                damage *= AttackWindowDamageScale;
                stagger *= AttackWindowStaggerScale;
                staminaCost *= AttackWindowStaminaScale;
            }

            // Swinging while out of breath still connects, but weakly.
            bool tired = !PlayerStamina.TryUse(staminaCost);
            if (tired)
            {
                damage *= TiredDamageScale;
                stagger *= TiredDamageScale;
            }
            if (special != null) special.ModifyHit(ref damage, ref stagger);

            CombatLog.Log("HIT " + where + ": " + type + ", " + speed.ToString("F2") + " m/s, swing " + swingDistance.ToString("F2") + " m, "
                + damage.ToString("F1") + " dmg (before zone), stagger " + stagger.ToString("F1") + ", stamina " + staminaCost.ToString("F1")
                + (tired ? " TIRED" : "") + (inWindow && AttackWindowDamageScale > 1f ? " HEAVY" : ""));

            var info = new DamageInfo
            {
                Amount = damage,
                Stagger = stagger,
                Type = type,
                Point = contact.point,
                Direction = velocity.normalized,
                ImpactSpeed = speed,
                HitCollider = collision.collider,
                Source = gameObject
            };
            target.TakeDamage(info);
            if (special != null) special.AfterHit(info, target);

            // Lifesteal: part of every blow on an enemy heals the player.
            float lifesteal = PlayerStats.Bonus(StatType.Lifesteal);
            if (lifesteal > 0f && target is Enemies.EnemyHealth && PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(damage * lifesteal);

            Audio.Sfx.Play(type == DamageType.Slash ? Audio.SfxKind.HitSlash : type == DamageType.Stab ? Audio.SfxKind.HitStab : Audio.SfxKind.HitBlunt,
                contact.point, Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(definition.minHitSpeed, definition.fullDamageSpeed, speed)));
            HitSpark.Spawn(contact.point, -velocity.normalized, type == DamageType.Blunt ? new Color(1f, 0.9f, 0.7f) : new Color(0.7f, 0.05f, 0.03f));

            float strength = Mathf.InverseLerp(definition.minHitSpeed, definition.fullDamageSpeed, speed);
            foreach (var holder in grabbable.Holders)
                holder.Hand.Haptic(Mathf.Lerp(0.3f, 1f, strength), 0.08f);
        }

        DamageType Classify(Vector3 velocity)
        {
            if (velocity.sqrMagnitude < 0.0001f) return DamageType.Blunt;
            Vector3 direction = velocity.normalized;
            // The impact has already twisted the weapon, so judge by how it was facing just before.
            Vector3 forward = grabbable.RotationBeforeImpact * Vector3.forward;
            Vector3 edge = grabbable.RotationBeforeImpact * Vector3.right;

            if (definition.hasPoint && Vector3.Angle(direction, forward) < stabAngle)
                return DamageType.Stab;

            if (definition.hasEdge)
            {
                // Angle between the swing and the edge axis, ignoring which of the two edges leads.
                float angle = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(direction, edge)))) * Mathf.Rad2Deg;
                if (angle < edgeAngle) return DamageType.Slash;
            }

            return DamageType.Blunt;
        }

        bool IsDamageCollider(Collider collider)
        {
            if (damageColliders == null || damageColliders.Length == 0) return true;
            foreach (var c in damageColliders)
                if (c == collider) return true;
            return false;
        }
    }
}
