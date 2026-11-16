using UnityEngine;

namespace Roguelike.Meta
{
    /// <summary>
    /// Prefabs that code needs to create at runtime (mixed enemy spawns, a challenge shrine's waves).
    /// Lives at Resources/GameAssets so it is in every build. Filled in by Roguelike > Build Game Data.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Game Assets", fileName = "GameAssets")]
    public class GameAssets : ScriptableObject
    {
        public GameObject grunt;
        public GameObject caster;
        public GameObject runner;
        public GameObject shieldbearer;
        public GameObject elite;

        static GameAssets instance;

        public static GameAssets Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<GameAssets>("GameAssets");
                return instance;
            }
        }

        /// <summary>A random melee enemy for waves, tougher kinds more likely on harder missions.</summary>
        public GameObject RandomMelee(int tier)
        {
            float roll = Random.value;
            if (elite != null && roll < 0.05f + tier * 0.08f) return elite;
            if (shieldbearer != null && roll < 0.2f + tier * 0.08f) return shieldbearer;
            if (runner != null && roll < 0.45f) return runner;
            return grunt;
        }
    }
}
