using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Roguelike.Player
{
    public enum StatType
    {
        /// <summary>Multiplies all weapon damage the player deals.</summary>
        Damage,
        /// <summary>Multiplies the player's maximum health.</summary>
        MaxHealth,
        /// <summary>Multiplies how fast the player moves.</summary>
        MoveSpeed,
        /// <summary>Multiplies the player's maximum stamina.</summary>
        MaxStamina,
        /// <summary>Multiplies how fast stamina refills.</summary>
        StaminaRegen
    }

    /// <summary>
    /// The one place every bonus goes through. Armor, trinkets, totem buffs and character levels all add
    /// modifiers here, and weapons, health and movement read the result, so new gear never needs new code.
    /// Each stat is a multiplier: 1 with no modifiers, 1.25 with a single +25% modifier, and so on.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        struct Modifier
        {
            public object Source;
            public StatType Stat;
            public float Percent;
        }

        static PlayerStats instance;

        readonly List<Modifier> modifiers = new List<Modifier>();

        /// <summary>Raised whenever a modifier is added or removed.</summary>
        public static event Action Changed;

        void Awake() => instance = this;

        ContinuousMoveProvider moveProvider;
        float baseMoveSpeed;

        void Start()
        {
            // Stick movement in the headset reads its speed from here. (Desktop movement asks for the stat directly.)
            moveProvider = FindObjectOfType<ContinuousMoveProvider>();
            if (moveProvider != null) baseMoveSpeed = moveProvider.moveSpeed;
            Changed += ApplyMoveSpeed;
        }

        void ApplyMoveSpeed()
        {
            if (moveProvider != null) moveProvider.moveSpeed = baseMoveSpeed * Get(StatType.MoveSpeed);
        }


        void OnDestroy()
        {
            Changed -= ApplyMoveSpeed;
            if (instance == this) instance = null;
        }

        /// <summary>The current multiplier for a stat. 1 when nothing modifies it.</summary>
        public static float Get(StatType stat)
        {
            if (instance == null) return 1f;
            float percent = 0f;
            foreach (var modifier in instance.modifiers)
                if (modifier.Stat == stat) percent += modifier.Percent;
            return Mathf.Max(0f, 1f + percent);
        }

        /// <summary>
        /// Adds a bonus (or penalty, if negative). Percent is a fraction: 0.25 means +25%.
        /// The source is whatever grants it (an armor piece, a buff) so it can be taken away again.
        /// </summary>
        public static void Add(object source, StatType stat, float percent)
        {
            if (instance == null) return;
            instance.modifiers.Add(new Modifier { Source = source, Stat = stat, Percent = percent });
            Changed?.Invoke();
        }

        /// <summary>Removes every bonus a source granted.</summary>
        public static void RemoveAll(object source)
        {
            if (instance == null) return;
            if (instance.modifiers.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0)
                Changed?.Invoke();
        }
    }
}
