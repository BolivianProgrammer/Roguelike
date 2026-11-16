using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Makes a fire's light breathe: intensity (and a little of its range) wanders with smooth noise.
    /// Put it on the Light of a fire effect prefab; it allocates nothing and only touches its own light.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Tooltip("How far intensity swings either way, as a share of the starting intensity.")]
        [SerializeField, Range(0f, 1f)] float amount = 0.25f;
        [Tooltip("Flicker speed. Fire is about 6-10.")]
        [SerializeField] float speed = 8f;

        Light lamp;
        float baseIntensity;
        float baseRange;
        float seed;

        void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            baseRange = lamp.range;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * speed;
            // Two octaves: a slow sway plus a fast crackle.
            float noise = Mathf.PerlinNoise(seed, t * 0.35f) * 0.7f + Mathf.PerlinNoise(seed + 17f, t) * 0.3f;
            float wave = (noise - 0.5f) * 2f;
            lamp.intensity = baseIntensity * (1f + wave * amount);
            lamp.range = baseRange * (1f + wave * amount * 0.25f);
        }
    }
}
