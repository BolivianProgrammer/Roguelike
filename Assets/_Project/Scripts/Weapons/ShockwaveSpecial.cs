using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Weapons
{
    /// <summary>Hammer special: the next hit while armed sends out a shockwave that hurts and staggers everything nearby.</summary>
    public class ShockwaveSpecial : WeaponSpecial
    {
        [SerializeField] float radius = 3f;
        [SerializeField] float shockwaveDamage = 30f;
        [Tooltip("Stagger dealt to everything caught in the shockwave. High enough to stun.")]
        [SerializeField] float shockwaveStagger = 100f;
        [SerializeField] Color shockwaveColor = new Color(1f, 0.7f, 0.2f, 0.5f);

        readonly Collider[] results = new Collider[64];

        public override void AfterHit(DamageInfo info, IDamageable target)
        {
            if (!IsActive) return;

            var struck = new HashSet<IDamageable> { target };
            int count = Physics.OverlapSphereNonAlloc(info.Point, radius, results, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = results[i].GetComponentInParent<IDamageable>();
                if (other == null || other is PlayerHealth || !struck.Add(other)) continue;

                Vector3 point = results[i].ClosestPoint(info.Point);
                Vector3 away = results[i].bounds.center - info.Point;
                other.TakeDamage(new DamageInfo
                {
                    Amount = shockwaveDamage,
                    Stagger = shockwaveStagger,
                    Type = DamageType.Blunt,
                    Point = point,
                    Direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up,
                    ImpactSpeed = 6f,
                    HitCollider = results[i],
                    Source = gameObject
                });
            }

            ShockwaveEffect.Spawn(info.Point, radius, shockwaveColor);
            Haptic(1f, 0.25f);
            Finish();
        }
    }
}
