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
        StaminaRegen,
        /// <summary>Multiplies the stamina every hit costs. Below 1 is cheaper.</summary>
        StaminaCost,
        /// <summary>Multiplies the damage the player takes. Below 1 is tougher.</summary>
        DamageTaken,
        /// <summary>Bonus: share of the damage dealt that heals the player (0.1 = 10%).</summary>
        Lifesteal,
        /// <summary>Bonus: share of a melee blow taken that hurts the attacker back.</summary>
        Thorns,
        /// <summary>Bonus: extra blessing cards offered by each totem (1 = one more card).</summary>
        BuffChoices,
        /// <summary>Multiplies essence picked up during a run.</summary>
        EssenceGain,
        /// <summary>Multiplies coins picked up during a run.</summary>
        CoinGain,
        /// <summary>Multiplies how hard the player's hits knock enemies off balance.</summary>
        StaggerPower,
        /// <summary>Multiplies weapon special cooldowns. Below 1 is faster.</summary>
        SpecialCooldown
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
        public static float Get(StatType stat) => Mathf.Max(0f, 1f + Bonus(stat));

        /// <summary>
        /// The sum of every modifier on a stat, 0 when nothing modifies it. For stats that are an amount rather than
        /// a multiplier (lifesteal, thorns, extra cards) this is the value to use.
        /// </summary>
        public static float Bonus(StatType stat)
        {
            if (instance == null) return 0f;
            float percent = 0f;
            foreach (var modifier in instance.modifiers)
                if (modifier.Stat == stat) percent += modifier.Percent;
            return percent;
        }

        /// <summary>A readable line for a stat change, such as "+25% damage" or "+1 blessing choice".</summary>
        public static string Describe(StatType stat, float percent)
        {
            string sign = percent >= 0f ? "+" : "-";
            float size = Mathf.Abs(percent);
            switch (stat)
            {
                case StatType.BuffChoices: return sign + Mathf.RoundToInt(size) + " blessing choice" + (Mathf.RoundToInt(size) == 1 ? "" : "s");
                case StatType.Lifesteal: return sign + Mathf.RoundToInt(size * 100f) + "% of damage dealt heals you";
                case StatType.Thorns: return sign + Mathf.RoundToInt(size * 100f) + "% of melee damage taken hurts the attacker";
            }
            string what;
            switch (stat)
            {
                case StatType.Damage: what = "damage"; break;
                case StatType.MaxHealth: what = "max health"; break;
                case StatType.MoveSpeed: what = "move speed"; break;
                case StatType.MaxStamina: what = "max stamina"; break;
                case StatType.StaminaRegen: what = "stamina regen"; break;
                case StatType.StaminaCost: what = "stamina cost"; break;
                case StatType.DamageTaken: what = "damage taken"; break;
                case StatType.EssenceGain: what = "essence found"; break;
                case StatType.CoinGain: what = "coins found"; break;
                case StatType.StaggerPower: what = "stagger"; break;
                case StatType.SpecialCooldown: what = "special cooldown"; break;
                default: what = stat.ToString(); break;
            }
            return sign + Mathf.RoundToInt(size * 100f) + "% " + what;
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
