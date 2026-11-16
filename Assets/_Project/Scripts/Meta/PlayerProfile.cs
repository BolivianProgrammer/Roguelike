using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Player;

namespace Roguelike.Meta
{
    /// <summary>
    /// The character's permanent progression. Levels are bought with essence in the hub and make the player
    /// a little stronger everywhere. Applied automatically every time a scene loads.
    /// </summary>
    public static class PlayerProfile
    {
        /// <summary>Damage gained per level, as a fraction (0.05 = +5%).</summary>
        public const float DamagePerLevel = 0.05f;
        /// <summary>Max health gained per level, as a fraction.</summary>
        public const float HealthPerLevel = 0.05f;

        static readonly object Source = new object();

        public static int Level => SaveSystem.Data.level;

        /// <summary>Essence needed to reach the next level.</summary>
        public static int NextLevelCost => 100 * (Level + 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        /// <summary>Gives the player the bonuses for their current level.</summary>
        public static void Apply()
        {
            PlayerStats.RemoveAll(Source);
            int level = Level;
            if (level <= 0) return;
            PlayerStats.Add(Source, StatType.Damage, DamagePerLevel * level);
            PlayerStats.Add(Source, StatType.MaxHealth, HealthPerLevel * level);
        }

        /// <summary>Spends essence on the next level if the player can afford it.</summary>
        public static bool TryLevelUp()
        {
            var data = SaveSystem.Data;
            int cost = NextLevelCost;
            if (data.essence < cost) return false;

            data.essence -= cost;
            data.level++;
            SaveSystem.Save();
            Apply();
            return true;
        }
    }
}
