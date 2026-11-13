using System;
using UnityEngine;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// One buff a destroyed totem can offer. Create more with Create > Roguelike > Buff Definition;
    /// a buff is just a name, a description and a list of stat changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Buff Definition", fileName = "NewBuff")]
    public class BuffDefinition : ScriptableObject
    {
        [Serializable]
        public struct Effect
        {
            public StatType stat;
            [Tooltip("Fraction added to the stat: 0.25 is +25%, -0.2 is -20%.")]
            public float percent;
        }

        public string displayName = "Buff";
        [TextArea] public string description = "";
        public Color color = Color.white;
        public Effect[] effects;

        /// <summary>Grants this buff to the player until the given source's bonuses are removed.</summary>
        public void Apply(object source)
        {
            if (effects == null) return;
            foreach (var effect in effects)
                PlayerStats.Add(source, effect.stat, effect.percent);
        }
    }
}
