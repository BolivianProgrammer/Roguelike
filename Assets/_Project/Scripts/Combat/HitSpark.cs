using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Small bursts of light where blows land: sparks for metal on metal, a spray for flesh, flashes for effects.
    /// One shared particle system emits every burst, which keeps it cheap on the Quest.
    /// </summary>
    public static class HitSpark
    {
        static ParticleSystem system;

        /// <summary>A burst at a point, flying out along a direction.</summary>
        public static void Spawn(Vector3 point, Vector3 direction, Color color, int count = 14, float speed = 3f, float size = 0.025f)
        {
            ParticleSystem particles = System();
            if (particles == null) return;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up;
            direction.Normalize();

            var emit = new ParticleSystem.EmitParams { startColor = color, startSize = size, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 spread = (direction + Random.insideUnitSphere * 0.8f).normalized;
                emit.position = point;
                emit.velocity = spread * speed * Random.Range(0.4f, 1.2f);
                emit.startLifetime = Random.Range(0.15f, 0.35f);
                particles.Emit(emit, 1);
            }
        }

        /// <summary>A ring of particles spreading over the ground, e.g. when a totem falls or a portal opens.</summary>
        public static void Ring(Vector3 centre, Color color, float radius = 1f, int count = 40)
        {
            ParticleSystem particles = System();
            if (particles == null) return;
            var emit = new ParticleSystem.EmitParams { startColor = color, startSize = 0.06f };
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0.15f, Mathf.Sin(angle));
                emit.position = centre + outward * 0.2f;
                emit.velocity = outward * radius * 3f + Vector3.up * Random.Range(0.5f, 2f);
                emit.startLifetime = Random.Range(0.5f, 0.9f);
                particles.Emit(emit, 1);
            }
        }

        static ParticleSystem System()
        {
            if (system != null) return system;

            // The authored prefab, if the effect library has been built.
            var library = FxLibrary.Instance;
            if (library != null && library.hitSparks != null)
            {
                var instance = Object.Instantiate(library.hitSparks);
                Object.DontDestroyOnLoad(instance);
                system = instance.GetComponent<ParticleSystem>();
                system.Play();
                return system;
            }

            var go = new GameObject("Hit Sparks");
            Object.DontDestroyOnLoad(go);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.6f;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = FlatMaterials.Shared;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            system.Play();
            return system;
        }
    }
}
