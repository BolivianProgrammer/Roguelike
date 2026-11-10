using System;
using UnityEngine;
using Roguelike.Player;
using Roguelike.Weapons;

namespace Roguelike.Combat
{
    /// <summary>
    /// An enemy's striking surface (a club, a blade, a fist). While an attack is active it damages the player
    /// on touch, unless it meets something the player is holding first: that is a block.
    /// Meeting the attack with a fast swing of your own is a parry.
    /// Put this on a kinematic rigidbody whose colliders are the striking surface.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MeleeAttackHitbox : MonoBehaviour
    {
        [Tooltip("Speed (m/s) the player's weapon must be moving at to turn a block into a parry.")]
        [SerializeField] float parrySpeed = 2.5f;

        float damage;
        DamageType type;

        /// <summary>True while the current attack can still hurt the player.</summary>
        public bool Active { get; private set; }

        /// <summary>Raised when the player stops the attack with a held item. The flag is true for a parry.</summary>
        public event Action<Vector3, bool> Blocked;
        public event Action<PlayerHealth> HitPlayer;

        public void Begin(float attackDamage, DamageType attackType = DamageType.Blunt)
        {
            damage = attackDamage;
            type = attackType;
            Active = true;
        }

        public void End() => Active = false;

        void OnCollisionEnter(Collision collision) => CheckBlock(collision);
        void OnCollisionStay(Collision collision) => CheckBlock(collision);

        void CheckBlock(Collision collision)
        {
            if (!Active) return;

            var item = collision.collider.GetComponentInParent<Grabbable>();
            if (item == null || !item.IsHeld) return;

            Active = false;
            Vector3 point = collision.GetContact(0).point;
            bool parried = item.VelocityBeforeImpact(point).magnitude >= parrySpeed;

            foreach (var holder in item.Holders)
                holder.Hand.Haptic(parried ? 1f : 0.7f, parried ? 0.15f : 0.1f);

            Blocked?.Invoke(point, parried);
        }

        void OnTriggerEnter(Collider other) => CheckPlayer(other);
        void OnTriggerStay(Collider other) => CheckPlayer(other);

        void CheckPlayer(Collider other)
        {
            if (!Active) return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null) return;

            Active = false;
            Vector3 point = other.ClosestPoint(transform.position);
            player.TakeDamage(new DamageInfo
            {
                Amount = damage,
                Type = type,
                Point = point,
                Direction = (other.bounds.center - transform.position).normalized,
                HitCollider = other,
                Source = gameObject
            });
            HitPlayer?.Invoke(player);
        }
    }
}
