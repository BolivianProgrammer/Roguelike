using System;
using UnityEngine;
using Roguelike.Player;
using Roguelike.Weapons;

namespace Roguelike.Combat
{
    /// <summary>
    /// A slow, visible bolt thrown by a ranged enemy. It flies in a straight line at where the player's chest was
    /// when it left the enemy's hand, so it can be sidestepped, blocked or batted back.
    ///
    /// A marker shows where to put your weapon: a ring in front of your chest on the bolt's path, with an outer
    /// ring that closes in as the bolt arrives. Holding a weapon there blocks it. Swinging the weapon into the
    /// bolt deflects it back at whoever threw it.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        enum State { Held, Flying, Deflected }

        const float Radius = 0.09f;
        const int RingSegments = 28;

        static readonly RaycastHit[] hits = new RaycastHit[24];
        static readonly Collider[] overlaps = new Collider[16];
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("How fast the player's weapon must be moving (m/s) when it meets the bolt to send it back.")]
        public float deflectSpeed = 2.5f;
        [Tooltip("Speed multiplier of a deflected bolt.")]
        public float deflectedSpeedScale = 1.8f;
        [Tooltip("Damage a deflected bolt does to an enemy.")]
        public float deflectedDamage = 40f;
        [Tooltip("Stagger a deflected bolt deals. High enough to stun, which opens the enemy to a finisher.")]
        public float deflectedStagger = 70f;
        [Tooltip("How far in front of the player's chest (meters) the marker sits: where to hold the weapon.")]
        public float guardDistance = 0.45f;
        public float lifetime = 8f;

        public Color aimColor = new Color(1f, 0.85f, 0.2f);
        public Color flyingColor = new Color(1f, 0.25f, 0.15f);
        public Color deflectedColor = new Color(0.3f, 0.8f, 1f);

        State state = State.Held;
        Transform holder;
        Transform owner;
        PlayerHealth player;
        Vector3 direction;
        float speed;
        float damage;
        float age;

        [Header("Parts (set in the Bolt prefab)")]
        [SerializeField] MeshRenderer orb;
        [SerializeField] TrailRenderer trail;
        [Tooltip("Rings in world space, so the orb's pulsing size does not squash their line width.")]
        [SerializeField] LineRenderer innerRing;
        [SerializeField] LineRenderer outerRing;
        MaterialPropertyBlock block;

        /// <summary>
        /// Takes a bolt from the effect pool (Prefabs/Effects/Bolt, made by Roguelike > Build Effect Prefabs),
        /// charging in an enemy's hand. It follows the hand and shows the player where it will be aimed until
        /// <see cref="Launch"/> is called. Returns null if the effect library has no bolt.
        /// </summary>
        public static Projectile Spawn(Transform hand, Transform owner, float speed, float damage)
        {
            GameObject go = FxLibrary.Spawn(FxKind.Bolt, hand.position, Quaternion.identity);
            if (go == null)
            {
                Debug.LogWarning("[Projectile] No Bolt prefab in Resources/FxLibrary. Run Roguelike > Build Effect Prefabs.");
                return null;
            }

            var projectile = go.GetComponent<Projectile>();
            projectile.holder = hand;
            projectile.owner = owner;
            projectile.speed = speed;
            projectile.damage = damage;
            projectile.state = State.Held;
            projectile.age = 0f;
            projectile.direction = hand.forward;
            if (projectile.player == null) projectile.player = FindObjectOfType<PlayerHealth>();
            projectile.trail.Clear();
            projectile.trail.emitting = false;
            projectile.innerRing.enabled = false;
            projectile.outerRing.enabled = false;
            projectile.SetColor(projectile.aimColor);
            return projectile;
        }

        /// <summary>Done: back to the pool.</summary>
        void Despawn()
        {
            if (!gameObject.activeSelf) return;
            trail.emitting = false;
            innerRing.enabled = false;
            outerRing.enabled = false;
            holder = null;
            FxLibrary.Release(gameObject);
        }

        /// <summary>Releases the bolt at where the player's chest is right now.</summary>
        public void Launch()
        {
            if (state != State.Held) return;

            Vector3 toTarget = Chest() - transform.position;
            direction = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
            holder = null;
            state = State.Flying;
            age = 0f;
            SetColor(flyingColor);
            if (trail != null) trail.emitting = true;
            Audio.Sfx.Play(Audio.SfxKind.BoltRelease, transform.position);
            CombatLog.Log("Bolt RELEASED by " + (owner != null ? owner.name : "?") + ", " + toTarget.magnitude.ToString("F2") + " m to chest at " + speed + " m/s (ETA " + (toTarget.magnitude / speed).ToString("F2") + "s)");
        }

        /// <summary>The thrower was interrupted before releasing: the bolt simply goes out.</summary>
        public void Fizzle()
        {
            if (state == State.Held) Despawn();
        }

        Vector3 Chest()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform.position + Vector3.down * 0.35f;
            return player != null ? player.transform.position + Vector3.up * 1.2f : transform.position + transform.forward;
        }

        void Update()
        {
            if (state == State.Held)
            {
                if (holder == null)
                {
                    Despawn();
                    return;
                }
                transform.position = holder.position;
                // Pulse while charging so the throw is easy to see coming.
                orb.transform.localScale = Vector3.one * (Radius * 2f * (0.7f + 0.3f * Mathf.Sin(Time.time * 18f)));
                Vector3 toChest = Chest() - transform.position;
                direction = toChest.sqrMagnitude > 0.001f ? toChest.normalized : transform.forward;
            }

            UpdateMarker();
        }

        void FixedUpdate()
        {
            if (state == State.Held || WorldFreeze.Active) return;

            age += Time.fixedDeltaTime;
            if (age > lifetime)
            {
                Despawn();
                return;
            }

            // A deflected bolt curves back to whoever threw it.
            if (state == State.Deflected && owner != null)
            {
                Vector3 toOwner = (owner.position + Vector3.up * 1.2f * owner.lossyScale.y - transform.position).normalized;
                direction = Vector3.RotateTowards(direction, toOwner, 4f * Time.fixedDeltaTime, 0f);
            }

            float step = speed * Time.fixedDeltaTime;

            // A weapon swung across the path can be between two steps, so also check right around the bolt.
            if (state == State.Flying)
            {
                int near = Physics.OverlapSphereNonAlloc(transform.position, Radius * 1.8f, overlaps, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < near; i++)
                    if (TryMeetWeapon(overlaps[i], transform.position)) return;
            }

            int count = Physics.SphereCastNonAlloc(transform.position, Radius, direction, hits, step, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, 0, count, HitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = hits[i].distance > 0f ? hits[i].point : transform.position;
                if (Touch(hits[i].collider, point)) return;
            }

            transform.position += direction * step;
        }

        /// <summary>Handles the bolt touching something. Returns true if the bolt is finished or was turned around.</summary>
        bool Touch(Collider other, Vector3 point)
        {
            if (state == State.Flying)
            {
                if (TryMeetWeapon(other, point)) return true;

                // The player's body.
                var hitPlayer = other.GetComponentInParent<PlayerHealth>();
                if (hitPlayer == null && other is CharacterController) hitPlayer = player;
                if (hitPlayer != null)
                {
                    CombatLog.Log("Bolt HIT PLAYER after " + age.ToString("F2") + "s for " + damage);
                    hitPlayer.TakeDamage(new DamageInfo
                    {
                        Amount = damage,
                        Type = DamageType.Blunt,
                        Point = point,
                        Direction = direction,
                        ImpactSpeed = speed,
                        HitCollider = other,
                        Source = gameObject
                    });
                    Despawn();
                    return true;
                }
            }

            if (other.isTrigger) return false;
            // Hands do not stop bolts; neither do the thrower's allies or their weapons.
            if (other.GetComponentInParent<PhysicsHand>() != null) return false;
            if (other.GetComponentInParent<MeleeAttackHitbox>() != null) return false;

            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && !(target is PlayerHealth))
            {
                if (state != State.Deflected) return false;

                CombatLog.Log("Deflected bolt HIT " + CombatLog.Where(other) + " after " + age.ToString("F2") + "s");
                target.TakeDamage(new DamageInfo
                {
                    Amount = deflectedDamage,
                    Stagger = deflectedStagger,
                    Type = DamageType.Blunt,
                    Point = point,
                    Direction = direction,
                    ImpactSpeed = speed,
                    HitCollider = other,
                    Source = gameObject
                });
                Despawn();
                return true;
            }

            if (other.GetComponentInParent<Grabbable>() != null && state == State.Deflected) return false;

            // Walls, floor, pillars.
            CombatLog.Log("Bolt (" + state + ") stopped by " + other.name + " after " + age.ToString("F2") + "s");
            Despawn();
            return true;
        }

        /// <summary>A weapon in the player's hand stops the bolt; a weapon swung into it sends it back.</summary>
        bool TryMeetWeapon(Collider other, Vector3 point)
        {
            var item = other.GetComponentInParent<Grabbable>();
            // A broken guard stops nothing: the bolt flies on into the player.
            if (!PlayerGuard.CanBlockWith(item)) return false;

            float swing = (item.VelocityBeforeImpact(point) - PlayerBody.RigVelocity).magnitude;
            bool deflected = swing >= deflectSpeed;
            CombatLog.Log("Bolt met " + item.name + " after " + age.ToString("F2") + "s at weapon speed " + swing.ToString("F2") + " m/s (deflect >= " + deflectSpeed + ") -> " + (deflected ? "DEFLECT" : "BLOCK"));

            // A deflection is the bolt's parry; a plain block costs stamina and lets some through, like any blow.
            PlayerGuard.Outcome outcome = PlayerGuard.Resolve(item, damage, DamageType.Blunt, deflected, false, point, direction, gameObject);
            foreach (var hand in item.Holders)
                hand.Hand.Haptic(deflected ? 1f : 0.6f, deflected ? 0.15f : 0.08f);
            if (outcome != PlayerGuard.Outcome.GuardBroken)
                DamagePopup.Spawn(point, deflected ? "DEFLECT" : "BLOCK", deflected ? deflectedColor : Color.white);

            Audio.Sfx.Play(deflected ? Audio.SfxKind.Deflect : Audio.SfxKind.Block, point);
            HitSpark.Spawn(point, -direction, deflected ? deflectedColor : Color.white, deflected ? 22 : 10);
            if (!deflected)
            {
                Despawn();
                return true;
            }

            state = State.Deflected;
            age = 0f;
            speed *= deflectedSpeedScale;
            direction = owner != null
                ? (owner.position + Vector3.up * 1.2f * owner.lossyScale.y - transform.position).normalized
                : -direction;
            // Clear of the weapon so it is not caught again on the way out.
            transform.position = point + direction * (Radius * 2.5f);
            SetColor(deflectedColor);
            return true;
        }

        // ---------------------------------------------------------------- Marker

        void UpdateMarker()
        {
            bool show = state != State.Deflected;
            Vector3 chest = Chest();
            Vector3 toChest = chest - transform.position;
            float along = Vector3.Dot(toChest, direction);
            Vector3 nearest = transform.position + direction * along;

            // No marker once the bolt has passed, or if it is going to miss anyway.
            if (along < 0.1f || Vector3.Distance(nearest, chest) > 0.6f) show = false;

            innerRing.enabled = show;
            outerRing.enabled = show;
            if (!show) return;

            Vector3 center = nearest - direction * Mathf.Min(guardDistance, along * 0.9f);
            float timeLeft = state == State.Held ? 1f : Mathf.Clamp01((along - guardDistance) / speed / 1.2f);

            DrawRing(innerRing, center, 0.09f);
            DrawRing(outerRing, center, 0.09f + 0.3f * timeLeft);
        }

        void DrawRing(LineRenderer ring, Vector3 center, float radius)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(direction, right);

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
            }
        }

        void SetColor(Color color)
        {
            // Tinted with a property block: every bolt shares the prefab's material.
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            orb.SetPropertyBlock(block);
            trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            innerRing.startColor = innerRing.endColor = color;
            outerRing.startColor = outerRing.endColor = color;
        }
        sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
