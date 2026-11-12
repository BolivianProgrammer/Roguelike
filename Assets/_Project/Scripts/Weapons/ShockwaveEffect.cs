using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Weapons
{
    /// <summary>A sphere that swells to the shockwave's radius and fades out.</summary>
    public class ShockwaveEffect : MonoBehaviour
    {
        const float Lifetime = 0.35f;

        float radius;
        float age;
        Color color;
        Material material;

        public static void Spawn(Vector3 position, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shockwave";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.zero;

            var effect = go.AddComponent<ShockwaveEffect>();
            effect.radius = radius;
            effect.color = color;

            var shader = Shader.Find("Sprites/Default");
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (shader != null) meshRenderer.material = new Material(shader);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effect.material = meshRenderer.material;
            effect.material.color = color;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            transform.localScale = Vector3.one * (radius * 2f * Mathf.Sqrt(t));

            var faded = color;
            faded.a = color.a * (1f - t);
            material.color = faded;

            if (age >= Lifetime) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
