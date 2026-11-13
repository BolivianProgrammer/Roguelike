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
        static Material sharedMaterial;

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

        MeshRenderer orb;
        LineRenderer innerRing;
        LineRenderer outerRing;

        /// <summary>
        /// Creates a bolt charging in an enemy's hand. It follows the hand and shows the player where it will
        /// be aimed until <see cref="Launch"/> is called.
        /// </summary>
        public static Projectile Spawn(Transform hand, Transform owner, float speed, float damage)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = hand.position;
            go.transform.localScale = Vector3.one * (Radius * 2f);

            var projectile = go.AddComponent<Projectile>();
            projectile.holder = hand;
            projectile.owner = owner;
            projectile.speed = speed;
            projectile.damage = damage;
            projectile.orb = go.GetComponent<MeshRenderer>();
            projectile.orb.sharedMaterial = SharedMaterial();
            projectile.orb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            projectile.player = FindObjectOfType<PlayerHealth>();
            projectile.innerRing = CreateRing("Bolt Marker Inner", 0.012f);
            projectile.outerRing = CreateRing("Bolt Marker Outer", 0.008f);
            projectile.SetColor(projectile.aimColor);
            return projectile;
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
        }

        /// <summary>The thrower was interrupted before releasing: the bolt simply goes out.</summary>
        public void Fizzle()
        {
            if (state == State.Held) Destroy(gameObject);
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
                    Destroy(gameObject);
                    return;
                }
                transform.position = holder.position;
                // Pulse while charging so the throw is easy to see coming.
                transform.localScale = Vector3.one * (Radius * 2f * (0.7f + 0.3f * Mathf.Sin(Time.time * 18f)));
                Vector3 toChest = Chest() - transform.position;
                direction = toChest.sqrMagnitude > 0.001f ? toChest.normalized : transform.forward;
            }

            UpdateMarker();
        }

        void FixedUpdate()
        {
            if (state == State.Held) return;

            age += Time.fixedDeltaTime;
            if (age > lifetime)
            {
                Destroy(gameObject);
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
                    Destroy(gameObject);
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
                Destroy(gameObject);
                return true;
            }

            if (other.GetComponentInParent<Grabbable>() != null && state == State.Deflected) return false;

            // Walls, floor, pillars.
            Destroy(gameObject);
            return true;
        }

        /// <summary>A weapon in the player's hand stops the bolt; a weapon swung into it sends it back.</summary>
        bool TryMeetWeapon(Collider other, Vector3 point)
        {
            var item = other.GetComponentInParent<Grabbable>();
            if (item == null || !item.IsHeld) return false;

            float swing = (item.VelocityBeforeImpact(point) - PlayerBody.RigVelocity).magnitude;
            bool deflected = swing >= deflectSpeed;

            foreach (var hand in item.Holders)
                hand.Hand.Haptic(deflected ? 1f : 0.6f, deflected ? 0.15f : 0.08f);
            DamagePopup.Spawn(point, deflected ? "DEFLECT" : "BLOCK", deflected ? deflectedColor : Color.white);

            if (!deflected)
            {
                Destroy(gameObject);
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

        /// <summary>Rings live on their own objects so the bolt's size does not squash their line width.</summary>
        static LineRenderer CreateRing(string name, float width)
        {
            var go = new GameObject(name);
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.sharedMaterial = SharedMaterial();
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.enabled = false;
            return ring;
        }

        void SetColor(Color color)
        {
            // The orb needs its own material instance to be tinted; the rings take their colour directly.
            orb.material.color = color;
            innerRing.startColor = innerRing.endColor = color;
            outerRing.startColor = outerRing.endColor = color;
        }

        static Material SharedMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) sharedMaterial = new Material(shader);
            return sharedMaterial;
        }

        void OnDestroy()
        {
            if (orb != null && orb.sharedMaterial != sharedMaterial) Destroy(orb.sharedMaterial);
            if (innerRing != null) Destroy(innerRing.gameObject);
            if (outerRing != null) Destroy(outerRing.gameObject);
        }

        sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
