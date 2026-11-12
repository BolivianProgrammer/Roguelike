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
            ParticleSystem system = Create("Blood Burst", position, direction, 0.6f);

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = system.shape;
            shape.angle = 35f;

            system.Play();
        }

        /// <summary>Blood pumping from a stump for a few seconds. Follows the body part it is attached to.</summary>
        public static void Stream(Transform parent, Vector3 position, Vector3 direction, float duration = 3f)
        {
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
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader);
            return material;
        }
    }
}
