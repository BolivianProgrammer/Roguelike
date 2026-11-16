using UnityEngine;
using Roguelike.Combat;
using Roguelike.Enemies;

namespace Roguelike.Weapons
{
    /// <summary>
    /// Bloodletting: while armed, every hit opens a wound that keeps hurting the enemy for a few seconds.
    /// Several wounds stack, so a flurry of cuts bleeds an enemy out.
    /// </summary>
    public class BleedSpecial : WeaponSpecial
    {
        [Tooltip("Damage per second each wound does.")]
        [SerializeField] float bleedPerSecond = 6f;
        [Tooltip("How long each wound bleeds, in seconds.")]
        [SerializeField] float bleedTime = 5f;

        public override void AfterHit(DamageInfo info, IDamageable target)
        {
            if (!IsActive) return;
            var enemy = target as EnemyHealth;
            if (enemy == null || enemy.IsDead) return;
            enemy.AddWound(bleedPerSecond, bleedTime);
            BloodFx.Burst(info.Point, -info.Direction, 25);
            Haptic(0.4f, 0.05f);
        }
    }
}
