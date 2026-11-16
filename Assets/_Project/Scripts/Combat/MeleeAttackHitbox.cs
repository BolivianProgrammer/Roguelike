using System;
using UnityEngine;
using Roguelike.Player;
using Roguelike.Weapons;

namespace Roguelike.Combat
{
    /// <summary>
    /// An enemy's striking surface (a club, a blade, a fist). While an attack is active it damages the player
    /// on touch, unless it meets something the player is holding first: that is a block, settled by
    /// <see cref="PlayerGuard"/> (stamina cost, chip damage, guard breaks). Meeting the attack with a fast swing of
    /// your own is a parry, which stops it completely. While the guard is broken, held items do not stop it.
    /// Put this on a kinematic rigidbody whose colliders are the striking surface.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MeleeAttackHitbox : MonoBehaviour
    {
        [Tooltip("Speed (m/s) the player's weapon must be moving at to turn a block into a parry.")]
        [SerializeField] float parrySpeed = 2.5f;

        float damage;
        DamageType type;
        bool guardBreaker;

        /// <summary>True while the current attack can still hurt the player.</summary>
        public bool Active { get; private set; }

        /// <summary>Raised when the attack meets a held item: parried, blocked, or blocked so hard the guard broke.</summary>
        public event Action<Vector3, PlayerGuard.Outcome> Blocked;
        public event Action<PlayerHealth> HitPlayer;

        /// <summary>Makes the surface dangerous. A guard breaker breaks any block it meets (only a parry stops it).</summary>
        public void Begin(float attackDamage, DamageType attackType = DamageType.Blunt, bool breaksGuard = false)
        {
            damage = attackDamage;
            type = attackType;
            guardBreaker = breaksGuard;
            Active = true;
        }

        public void End() => Active = false;

        void OnCollisionEnter(Collision collision) => CheckBlock(collision);
        void OnCollisionStay(Collision collision) => CheckBlock(collision);

        void CheckBlock(Collision collision)
        {
            if (!Active || WorldFreeze.Active) return;

            var item = collision.collider.GetComponentInParent<Grabbable>();
            // A broken guard stops nothing: the blow carries on into the player.
            if (!PlayerGuard.CanBlockWith(item)) return;

            Active = false;
            Vector3 point = collision.GetContact(0).point;
            float itemSpeed = item.VelocityBeforeImpact(point).magnitude;
            bool parried = itemSpeed >= parrySpeed;
            CombatLog.Log("Enemy weapon met held " + item.name + " at " + itemSpeed.ToString("F2") + " m/s (parry >= " + parrySpeed + ") -> " + (parried ? "PARRY" : "BLOCK"));

            Vector3 direction = item.transform.position - transform.position;
            PlayerGuard.Outcome outcome = PlayerGuard.Resolve(item, damage, type, parried, guardBreaker, point, direction, gameObject);
            if (outcome != PlayerGuard.Outcome.GuardBroken)
            {
                foreach (var holder in item.Holders)
                    holder.Hand.Haptic(parried ? 1f : 0.7f, parried ? 0.15f : 0.1f);
                HitSpark.Spawn(point, Vector3.up, parried ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.95f, 0.8f), parried ? 26 : 12, parried ? 4f : 2.5f);
            }

            Blocked?.Invoke(point, outcome);
        }

        void OnTriggerEnter(Collider other) => CheckPlayer(other);
        void OnTriggerStay(Collider other) => CheckPlayer(other);

        void CheckPlayer(Collider other)
        {
            if (!Active || WorldFreeze.Active) return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null) return;

            Active = false;
            Vector3 point = other.ClosestPoint(transform.position);
            CombatLog.Log("Enemy weapon HIT PLAYER (" + other.name + ") for " + damage + " " + type);
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
