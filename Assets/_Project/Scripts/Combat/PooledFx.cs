using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// On an effect instance from <see cref="FxLibrary"/>: hands it back to its pool when its particles finish
    /// (the root particle system's Stop Action is set to Callback), or after a fixed time for effects without
    /// particles.
    /// </summary>
    public class PooledFx : MonoBehaviour
    {
        [HideInInspector] public GameObject Prefab;
        [Tooltip("For effects without a particle system: seconds before it goes back to the pool. 0 = never by itself.")]
        public float lifetime;

        float releaseAt;

        void OnEnable() => releaseAt = lifetime > 0f ? Time.time + lifetime : float.MaxValue;

        void Update()
        {
            if (Time.time >= releaseAt) FxLibrary.Release(gameObject, Prefab);
        }

        /// <summary>Called by Unity when the root particle system stops (Stop Action = Callback).</summary>
        void OnParticleSystemStopped() => FxLibrary.Release(gameObject, Prefab);
    }
}
