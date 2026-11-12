using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Weapons
{
    /// <summary>Sword special: the next hit while armed lands much harder and staggers.</summary>
    public class EmpoweredStrikeSpecial : WeaponSpecial
    {
        [SerializeField] float damageMultiplier = 2.5f;
        [SerializeField] float staggerMultiplier = 3f;

        public override void ModifyHit(ref float damage, ref float stagger)
        {
            if (!IsActive) return;
            damage *= damageMultiplier;
            stagger *= staggerMultiplier;
        }

        public override void AfterHit(DamageInfo info, IDamageable target)
        {
            if (IsActive) Finish();
        }
    }
}
