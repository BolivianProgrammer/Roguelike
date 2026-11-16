using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Blood effects built in code: a burst when a limb comes off, and a stream from the stump afterwards.
    /// Placeholder look; swap for authored effects once the game has an art style.
    /// </summary>
    public static class BloodFx
    {
        static readonly Color BloodColor = new Color(0.45f, 0.02f, 0.02f);
        static Material material;

        /// <summary>A one-off spray of blood from a point, thrown in a direction.</summary>
        public static void Burst(Vector3 position, Vector3 direction, int count = 45)
        {
            Quaternion facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            GameObject pooled = FxLibrary.Spawn(FxKind.BloodBurst, position, facing);
            if (pooled != null)
            {
                // The prefab's burst size scaled to this blow.
                var pooledSystem = pooled.GetComponent<ParticleSystem>();
                var pooledEmission = pooledSystem.emission;
                pooledEmission.SetBurst(0, new ParticleSystem.Burst(0f, (short)count));
            }
            else
            {
                // Fallback if the effect library has not been built (Roguelike > Build Effect Prefabs).
                ParticleSystem system = Create("Blood Burst", position, direction, 0.6f);
                var emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
                var shape = system.shape;
                shape.angle = 35f;
                system.Play();
            }

            // The spray lands: a few splashes on the floor around and ahead of it.
            int splashes = Mathf.Clamp(count / 25, 1, 5);
            for (int i = 0; i < splashes; i++)
            {
                Vector3 spread = direction * Random.Range(0.2f, 0.9f) + Random.insideUnitSphere * 0.35f;
                BloodDecal.Splat(position + spread, Mathf.Lerp(0.25f, 0.6f, count / 120f), direction);
            }
            // The blow already made its own hit sound; only a gush big enough to be a severing adds one.
            if (count >= 80) Audio.Sfx.Play(Audio.SfxKind.Sever, position, Mathf.Clamp01(count / 80f));
        }

        /// <summary>Blood pumping from a stump for a few seconds. Follows the body part it is attached to.</summary>
        public static void Stream(Transform parent, Vector3 position, Vector3 direction, float duration = 3f)
        {
            Quaternion facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            if (FxLibrary.Spawn(FxKind.BloodStream, position, facing, parent) != null) return;

            ParticleSystem system = Create("Blood Stream", position, direction, duration);
            system.transform.SetParent(parent, true);

            var main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);

            var emission = system.emission;
            emission.rateOverTime = 35f;

            var shape = system.shape;
            shape.angle = 12f;

            system.Play();
        }

        static ParticleSystem Create(string name, Vector3 position, Vector3 direction, float duration)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (direction.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(direction);

            var system = go.AddComponent<ParticleSystem>();
            // A new system starts playing with default settings; stop it before configuring.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.startColor = BloodColor;
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.02f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        static Material Material()
        {
            if (material != null) return material;
            material = new Material(FlatMaterials.Shared);
            return material;
        }
    }
}
