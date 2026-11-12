using UnityEngine;

namespace Roguelike.Player
{
    /// <summary>
    /// The player's stamina. Every landed hit spends some, heavier weapons and heavy attacks spend more,
    /// and it refills after a short rest. Running dry does not stop you swinging, but hits land at half strength,
    /// so flailing is punished and pacing your swings is rewarded.
    /// Added to the player automatically by <see cref="PlayerHealth"/>.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        static PlayerStamina instance;

        [SerializeField] float max = 100f;
        [Tooltip("Stamina regained per second once regeneration has started.")]
        [SerializeField] float regenPerSecond = 30f;
        [Tooltip("Seconds without spending stamina before it starts to refill.")]
        [SerializeField] float regenDelay = 0.8f;

        float current;
        float lastUseTime = -10f;

        public static float Current => instance != null ? instance.current : 0f;
        public static float Max => instance != null ? instance.max : 0f;
        /// <summary>True when a player with stamina exists. Without one, nothing costs stamina.</summary>
        public static bool Exists => instance != null;

        void Awake()
        {
            instance = this;
            current = max;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            if (Time.time - lastUseTime >= regenDelay)
                current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
        }

        /// <summary>True if at least this much stamina is available, without spending it.</summary>
        public static bool Has(float amount) => instance == null || instance.current >= amount;

        /// <summary>
        /// Spends stamina. Returns false if there was not enough; what is left is spent anyway,
        /// so swinging while exhausted keeps you exhausted.
        /// </summary>
        public static bool TryUse(float amount)
        {
            if (instance == null || amount <= 0f) return true;

            instance.lastUseTime = Time.time;
            if (instance.current >= amount)
            {
                instance.current -= amount;
                return true;
            }
            instance.current = 0f;
            return false;
        }
    }
}
