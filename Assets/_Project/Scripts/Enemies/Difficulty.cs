using UnityEngine;

namespace Roguelike.Enemies
{
    /// <summary>
    /// How hard the current mission is. Set by the mission when it starts, from the mission's own difficulty and
    /// the character's level, and read by every enemy as it spawns: more health, harder hits, better loot.
    /// 1 everywhere means the numbers in the enemy prefabs are used as they are.
    /// </summary>
    public static class Difficulty
    {
        public static float EnemyHealth { get; private set; } = 1f;
        public static float EnemyDamage { get; private set; } = 1f;
        public static float Loot { get; private set; } = 1f;

        /// <summary>
        /// Sets the difficulty for a mission. Each tier adds 25% health and 15% damage; each character level adds
        /// a further 4% and 3%, so enemies keep pace with a stronger player.
        /// </summary>
        public static void Set(int missionTier, int characterLevel)
        {
            int tier = Mathf.Max(0, missionTier);
            int level = Mathf.Max(0, characterLevel);
            EnemyHealth = 1f + tier * 0.25f + level * 0.04f;
            EnemyDamage = 1f + tier * 0.15f + level * 0.03f;
            Loot = 1f + tier * 0.3f;
        }

        /// <summary>Back to the prefab numbers, e.g. in the hub or a test scene.</summary>
        public static void Reset()
        {
            EnemyHealth = 1f;
            EnemyDamage = 1f;
            Loot = 1f;
        }
    }
}
