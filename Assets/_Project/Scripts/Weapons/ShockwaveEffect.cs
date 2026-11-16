using UnityEngine;
using Roguelike.Combat;

namespace Roguelike.Weapons
{
    /// <summary>
    /// A sphere that swells to the shockwave's radius and fades out. The look lives in the Shockwave prefab
    /// (Prefabs/Effects, made by Roguelike > Build Effect Prefabs); instances come from the effect pool and are
    /// tinted with a property block, so no material is created at runtime.
    /// </summary>
    public class ShockwaveEffect : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] float lifetime = 0.35f;
        [SerializeField] Renderer sphere;

        MaterialPropertyBlock block;
        float radius;
        float age;
        Color color;

        public static void Spawn(Vector3 position, float radius, Color color)
        {
            GameObject go = FxLibrary.Spawn(FxKind.Shockwave, position, Quaternion.identity);
            if (go == null) return;
            var effect = go.GetComponent<ShockwaveEffect>();
            effect.radius = radius;
            effect.color = color;
            effect.age = 0f;
            go.transform.localScale = Vector3.zero;
            effect.Tint(color);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.localScale = Vector3.one * (radius * 2f * Mathf.Sqrt(t));

            var faded = color;
            faded.a = color.a * (1f - t);
            Tint(faded);

            if (age >= lifetime) FxLibrary.Release(gameObject);
        }

        void Tint(Color tint)
        {
            if (sphere == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, tint);
            sphere.SetPropertyBlock(block);
        }
    }
}
