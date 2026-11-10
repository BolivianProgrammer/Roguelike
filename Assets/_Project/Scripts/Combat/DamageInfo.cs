using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>How a hit landed. Enemies and armor can react differently to each.</summary>
    public enum DamageType
    {
        /// <summary>The edge of a blade, swung edge-first.</summary>
        Slash,
        /// <summary>The point of a weapon, driven in along its length.</summary>
        Stab,
        /// <summary>A hammer head, a pommel, or the flat of a blade.</summary>
        Blunt
    }

    /// <summary>Everything a target needs to know about one hit.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public Vector3 Point;
        /// <summary>Direction the weapon was moving at impact, for knockback and hit reactions.</summary>
        public Vector3 Direction;
        public float ImpactSpeed;
        /// <summary>The collider that was struck, so targets can tell head from limb.</summary>
        public Collider HitCollider;
        public GameObject Source;
    }

    /// <summary>Anything that can take a hit: enemies, totems, the boss, the player.</summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }
}
