using UnityEngine;
using Roguelike.Combat;

namespace Roguelike.Weapons
{
    /// <summary>
    /// The tunable stats of one weapon type. Create one asset per weapon (Create > Roguelike > Weapon Definition).
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Weapon Definition", fileName = "NewWeapon")]
    public class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Weapon";

        [Tooltip("Kilograms. Heavier weapons follow the hand more slowly.")]
        [Min(0.1f)] public float mass = 1.5f;

        [Tooltip("Damage of a full-speed hit.")]
        [Min(0f)] public float baseDamage = 25f;

        [Tooltip("Swing speed (m/s) below which a hit does no damage.")]
        [Min(0f)] public float minHitSpeed = 1.5f;

        [Tooltip("Swing speed (m/s) at which a hit deals full damage.")]
        [Min(0.1f)] public float fullDamageSpeed = 6f;

        [Tooltip("Share of full damage dealt by the weakest hit that still counts.")]
        [Range(0f, 1f)] public float minDamageFraction = 0.2f;

        [Tooltip("Seconds before the same target can be hit again by this weapon.")]
        [Min(0f)] public float hitCooldown = 0.35f;

        [Tooltip("Meters the striking part must travel at swing speed before a hit counts. Stops a resting or nudged weapon " +
                 "from doing damage. Bigger for heavy weapons that need a full swing; stabs need half of it.")]
        [Min(0f)] public float minSwingDistance = 0.3f;

        [Tooltip("Stamina each landed hit costs. 0 = work it out from the weapon's mass.")]
        [Min(0f)] public float staminaCost = 0f;

        /// <summary>Stamina a landed hit costs: the set value, or a default that grows with weight.</summary>
        public float StaminaCost => staminaCost > 0f ? staminaCost : 4f + mass * 3.5f;

        [Tooltip("How much a hit knocks enemies off balance, as a multiple of its damage. Above 1 for heavy weapons, below 1 for light ones.")]
        [Min(0f)] public float staggerMultiplier = 1f;

        [Tooltip("Share of an enemy blow this weapon stops when blocking (the rest gets through). A dagger is poor, a " +
                 "hammer or axe solid. Both hands on the weapon add 0.1. A timed parry stops everything regardless.")]
        [Range(0f, 1f)] public float blockStability = 0.75f;

        [Header("Hit types")]
        [Tooltip("The weapon has a cutting edge. Edge-first swings count as slashes; hits with the flat count as blunt.")]
        public bool hasEdge = true;
        [Tooltip("The weapon has a point. Thrusts along its length count as stabs.")]
        public bool hasPoint = true;
        [Tooltip("Damage multiplier for edge-first swings.")]
        [Min(0f)] public float slashMultiplier = 1f;
        [Tooltip("Damage multiplier for thrusts with the point.")]
        [Min(0f)] public float stabMultiplier = 1.4f;
        [Tooltip("Damage multiplier for blunt hits. Keep low on blades so hitting with the flat is punished; 1 for hammers.")]
        [Min(0f)] public float bluntMultiplier = 0.4f;
        [Tooltip("Thrusts are slower than swings, so stabs reach full damage at this share of Full Damage Speed.")]
        [Range(0.2f, 1f)] public float stabSpeedFactor = 0.5f;

        public float CalculateDamage(float impactSpeed, DamageType type = DamageType.Slash)
        {
            // Every hit needs the same minimum speed, so slowly pushing the point into someone is not a stab.
            // Stabs then reach full damage sooner, because a thrust is slower than a swing.
            float min = minHitSpeed;
            if (impactSpeed < min) return 0f;
            float full = type == DamageType.Stab ? Mathf.Max(min + 0.1f, fullDamageSpeed * stabSpeedFactor) : fullDamageSpeed;
            float t = Mathf.InverseLerp(min, full, impactSpeed);
            return baseDamage * Mathf.Lerp(minDamageFraction, 1f, t) * Multiplier(type);
        }

        public float Multiplier(DamageType type)
        {
            switch (type)
            {
                case DamageType.Stab: return stabMultiplier;
                case DamageType.Blunt: return bluntMultiplier;
                default: return slashMultiplier;
            }
        }
    }
}
