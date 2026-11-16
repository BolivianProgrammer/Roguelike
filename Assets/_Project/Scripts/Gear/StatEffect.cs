using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Player;

namespace Roguelike.Gear
{
    /// <summary>One change to one player stat, used by armor, trinkets and blessings alike.</summary>
    [Serializable]
    public struct StatEffect
    {
        public StatType stat;
        [Tooltip("Fraction added to the stat: 0.25 is +25%, -0.2 is -20%. For extra blessing choices, 1 is one more card.")]
        public float percent;

        public StatEffect(StatType stat, float percent)
        {
            this.stat = stat;
            this.percent = percent;
        }

        /// <summary>Grants every effect in the list to the player until the source's bonuses are removed.</summary>
        public static void ApplyAll(IEnumerable<StatEffect> effects, object source)
        {
            if (effects == null) return;
            foreach (var effect in effects) PlayerStats.Add(source, effect.stat, effect.percent);
        }

        /// <summary>The effects as one readable line: "+30% max health, -15% move speed".</summary>
        public static string Describe(IEnumerable<StatEffect> effects)
        {
            var parts = new List<string>();
            if (effects != null)
                foreach (var effect in effects) parts.Add(PlayerStats.Describe(effect.stat, effect.percent));
            return string.Join(", ", parts);
        }
    }
}
