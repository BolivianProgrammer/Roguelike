using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;

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

        Grabbable grabbable;
        readonly Dictionary<IDamageable, float> lastHitTimes = new Dictionary<IDamageable, float>();

        public WeaponDefinition Definition => definition;
        public Grabbable Grabbable => grabbable;

        void Awake()
        {
            grabbable = GetComponent<Grabbable>();
            if (definition != null)
                GetComponent<Rigidbody>().mass = definition.mass;
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

            // The solver has already slowed the weapon down by now, so use the motion from just before the impact.
            Vector3 velocity = grabbable.VelocityBeforeImpact(contact.point);
            DamageType type = Classify(velocity);

            float speed = collision.relativeVelocity.magnitude;
            float damage = definition.CalculateDamage(speed, type);
            if (damage <= 0f) return;

            if (lastHitTimes.TryGetValue(target, out float lastHit) && Time.time - lastHit < definition.hitCooldown) return;
            lastHitTimes[target] = Time.time;

            target.TakeDamage(new DamageInfo
            {
                Amount = damage,
                Type = type,
                Point = contact.point,
                Direction = velocity.normalized,
                ImpactSpeed = speed,
                HitCollider = collision.collider,
                Source = gameObject
            });

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
