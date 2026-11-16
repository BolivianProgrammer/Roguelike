using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Run
{
    /// <summary>
    /// Every blessing a totem can offer, in any mission. Lives at Resources/BuffCatalog so new blessings reach
    /// every mission without editing each scene. A mission's own Buff Pool, if filled in, is used instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Buff Catalog", fileName = "BuffCatalog")]
    public class BuffCatalog : ScriptableObject
    {
        public BuffDefinition[] buffs;

        static BuffCatalog instance;

        public static BuffCatalog Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<BuffCatalog>("BuffCatalog");
                return instance;
            }
        }

        /// <summary>
        /// Picks a number of different blessings at random, rarer ones less often. At higher mission tiers, rare and
        /// legendary blessings come up more.
        /// </summary>
        public static List<BuffDefinition> Pick(IList<BuffDefinition> pool, int count, int tier)
        {
            var remaining = new List<BuffDefinition>();
            if (pool != null)
                foreach (var buff in pool)
                    if (buff != null) remaining.Add(buff);

            var picks = new List<BuffDefinition>();
            float tierBoost = 1f + tier * 0.5f;
            while (picks.Count < count && remaining.Count > 0)
            {
                float total = 0f;
                foreach (var buff in remaining) total += WeightOf(buff, tierBoost);
                float roll = Random.value * total;
                int chosen = remaining.Count - 1;
                for (int i = 0; i < remaining.Count; i++)
                {
                    roll -= WeightOf(remaining[i], tierBoost);
                    if (roll <= 0f) { chosen = i; break; }
                }
                picks.Add(remaining[chosen]);
                remaining.RemoveAt(chosen);
            }
            return picks;
        }

        static float WeightOf(BuffDefinition buff, float tierBoost) =>
            buff.rarity == Meta.Rarity.Common ? buff.Weight : buff.Weight * tierBoost;
    }
}
