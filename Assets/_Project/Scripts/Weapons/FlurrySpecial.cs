using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Weapons
{
    /// <summary>Dagger special: for a few seconds hits land far more often and harder, rewarding rapid stabs.</summary>
    public class FlurrySpecial : WeaponSpecial
    {
        [SerializeField] float damageMultiplier = 1.5f;
        [Tooltip("Share of the normal delay between hits on the same target while the flurry lasts.")]
        [SerializeField, Range(0.05f, 1f)] float hitCooldownScale = 0.25f;

        public override float HitCooldownScale => IsActive ? hitCooldownScale : 1f;

        public override void ModifyHit(ref float damage, ref float stagger)
        {
            if (IsActive) damage *= damageMultiplier;
        }
    }
}
