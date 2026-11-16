using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Roguelike.Combat;
using Roguelike.Run;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the effect prefabs (blood burst and stream, hit sparks, blood decal, loot shard) with their particle
    /// settings and material assets, and the effect library that lists them. Roguelike > Build Effect Prefabs.
    /// After building, tweak the prefabs in the Inspector like any other asset; code only spawns them.
    /// </summary>
    public static class FxBuilder
    {
        const string PrefabFolder = "Assets/_Project/Prefabs/Effects";
        const string MaterialFolder = "Assets/_Project/Materials/Effects";

        [MenuItem("Roguelike/Build Effect Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Effects");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Effects");

            Material alpha = ParticleMaterial("Fx_Particle_Alpha", false);
            Material additive = ParticleMaterial("Fx_Particle_Additive", true);
            Material blood = UnlitMaterial("Fx_Blood", new Color(0.3f, 0.02f, 0.02f, 0.92f), true);
            Material shard = UnlitMaterial("Fx_Shard", Color.white, false);

            var library = AssetDatabase.LoadAssetAtPath<FxLibrary>("Assets/_Project/Resources/FxLibrary.asset");
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<FxLibrary>();
                AssetDatabase.CreateAsset(library, "Assets/_Project/Resources/FxLibrary.asset");
            }
            library.bloodBurst = BloodBurst(alpha);
            library.bloodStream = BloodStream(alpha);
            library.hitSparks = HitSparks(additive);
            library.bloodDecal = Decal(blood);
            library.lootShard = Shard(shard);
            Material glow = UnlitMaterial("Fx_Glow", Color.white, true);
            Material marker = UnlitMaterial("Fx_ExecuteMarker", new Color(1f, 0.8f, 0.1f), false);
            library.shockwave = Shockwave(glow);
            library.executeMarker = ExecuteMarker(marker);
            library.bolt = Bolt(UnlitMaterial("Fx_Bolt", Color.white, false), alpha);

            // Ambient scene effects (placed by the scene builders, not pooled): soft round particles.
            Texture2D soft = SoftDot();
            Material fire = ParticleMaterial("Fx_Fire_Additive", true, soft);
            Material smoke = ParticleMaterial("Fx_Smoke_Alpha", false, soft);
            BrazierFire(fire, smoke);
            AmbientMotes(fire);
            GroundMist(smoke);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[FxBuilder] Built the effect prefabs in " + PrefabFolder + " and Resources/FxLibrary.");
        }

        // ---------------------------------------------------------------- Prefabs

        static GameObject BloodBurst(Material material)
        {
            var go = new GameObject("BloodBurst");
            ParticleSystem system = Particles(go, material, new Color(0.45f, 0.02f, 0.02f), 0.6f, 200);
            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            var shape = system.shape;
            shape.angle = 35f;
            go.AddComponent<PooledFx>();
            return Save(go, "BloodBurst");
        }

        static GameObject BloodStream(Material material)
        {
            var go = new GameObject("BloodStream");
            ParticleSystem system = Particles(go, material, new Color(0.45f, 0.02f, 0.02f), 3f, 200);
            var main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            var emission = system.emission;
            emission.rateOverTime = 35f;
            var shape = system.shape;
            shape.angle = 12f;
            go.AddComponent<PooledFx>();
            return Save(go, "BloodStream");
        }

        /// <summary>One long-lived system that code emits bursts from (cheaper than one system per hit).</summary>
        static GameObject HitSparks(Material material)
        {
            var go = new GameObject("HitSparks");
            ParticleSystem system = Particles(go, material, Color.white, 1f, 600);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.gravityModifier = 0.6f;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            return Save(go, "HitSparks");
        }

        static GameObject Decal(Material material)
        {
            var go = new GameObject("BloodDecal");
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return Save(go, "BloodDecal");
        }

        static GameObject Shard(Material material)
        {
            var go = new GameObject("LootShard");
            // The crystal from the forest set; a small cube if it has not been imported.
            Mesh crystal = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ArtImporter.ForestArtFolder + "/Models/Loot_Shard.fbx"))
                if (asset is Mesh mesh) crystal = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = crystal != null ? crystal : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * (crystal != null ? 0.45f : 0.06f);
            go.AddComponent<LootShard>();
            return Save(go, "LootShard");
        }

        /// <summary>A see-through sphere that swells and fades (ShockwaveEffect drives it and returns it to the pool).</summary>
        static GameObject Shockwave(Material material)
        {
            var go = new GameObject("Shockwave");
            MeshRenderer sphere = MeshObject(go, "Sphere.fbx", material);
            sphere.receiveShadows = false;
            var effect = go.AddComponent<Roguelike.Weapons.ShockwaveEffect>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("sphere").objectReferenceValue = sphere;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<PooledFx>();
            return Save(go, "Shockwave");
        }

        /// <summary>The gold ball over a stunned enemy's head.</summary>
        static GameObject ExecuteMarker(Material material)
        {
            var go = new GameObject("ExecuteMarker");
            MeshObject(go, "Sphere.fbx", material);
            go.transform.localScale = Vector3.one * 0.09f;
            go.AddComponent<PooledFx>();
            return Save(go, "ExecuteMarker");
        }

        /// <summary>
        /// A caster's bolt: the Projectile on the root (scale 1), a pulsing orb child, a trail, and two aim rings
        /// drawn in world space.
        /// </summary>
        static GameObject Bolt(Material orbMaterial, Material lineMaterial)
        {
            var go = new GameObject("Bolt");
            var orbObject = new GameObject("Orb");
            orbObject.transform.SetParent(go.transform, false);
            orbObject.transform.localScale = Vector3.one * 0.18f;
            MeshRenderer orb = MeshObject(orbObject, "Sphere.fbx", orbMaterial);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthMultiplier = 0.09f * 1.6f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = lineMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            LineRenderer inner = Ring(go, "Marker Inner", 0.012f, lineMaterial);
            LineRenderer outer = Ring(go, "Marker Outer", 0.008f, lineMaterial);

            var projectile = go.AddComponent<Projectile>();
            var serialized = new SerializedObject(projectile);
            serialized.FindProperty("orb").objectReferenceValue = orb;
            serialized.FindProperty("trail").objectReferenceValue = trail;
            serialized.FindProperty("innerRing").objectReferenceValue = inner;
            serialized.FindProperty("outerRing").objectReferenceValue = outer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<PooledFx>();
            return Save(go, "Bolt");
        }

        // ---------------------------------------------------------------- Ambient

        /// <summary>
        /// Fire in a brazier bowl: flames, rising embers, a thin smoke column and a flickering warm light.
        /// The prefab's origin is the fire centre (Brazier.fbx: 1.03 m above its base).
        /// </summary>
        static GameObject BrazierFire(Material fire, Material smoke)
        {
            var go = new GameObject("Ambient_Brazier");

            ParticleSystem flames = Looping(go, "Flames", fire, 60);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.3f, 0.05f, 0.9f));
            var emission = flames.emission;
            emission.rateOverTime = 34f;
            var shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeAndShrink(flames, new Color(1f, 0.75f, 0.3f), new Color(0.8f, 0.1f, 0.02f));

            ParticleSystem embers = Looping(go, "Embers", fire, 40);
            main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = new Color(1f, 0.6f, 0.2f);
            emission = embers.emission;
            emission.rateOverTime = 9f;
            shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.8f;
            FadeAndShrink(embers, new Color(1f, 0.7f, 0.3f), new Color(1f, 0.2f, 0.05f));

            ParticleSystem column = Looping(go, "Smoke", smoke, 30);
            column.transform.localPosition = Vector3.up * 0.35f;
            main = column.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startColor = new Color(0.12f, 0.11f, 0.1f, 0.35f);
            emission = column.emission;
            emission.rateOverTime = 5f;
            shape = column.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var grow = column.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var fade = column.colorOverLifetime;
            fade.enabled = true;
            fade.color = Gradient(new Color(0.15f, 0.13f, 0.12f), 0f, 0.35f, 0f);

            var lightObject = new GameObject("Fire Light");
            lightObject.transform.SetParent(go.transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.3f;
            var fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.22f);
            fireLight.intensity = 2.6f;
            fireLight.range = 7.5f;
            fireLight.shadows = LightShadows.None;
            lightObject.AddComponent<LightFlicker>();

            return Save(go, "Ambient_Brazier");
        }

        /// <summary>
        /// Slow drifting sparks and dust in the air over a whole area (box 30 x 6 x 30 m). Cheap: about 60 particles.
        /// </summary>
        static GameObject AmbientMotes(Material material)
        {
            var go = new GameObject("Ambient_Motes");
            ParticleSystem motes = Looping(go, "Motes", material, 80);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.65f, 0.3f, 0.8f), new Color(0.75f, 0.6f, 1f, 0.6f));
            main.prewarm = true;
            var emission = motes.emission;
            emission.rateOverTime = 8f;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 6f, 30f);
            shape.position = new Vector3(0f, 3f, 0f);
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.25f;
            var fade = motes.colorOverLifetime;
            fade.enabled = true;
            fade.color = Gradient(Color.white, 0f, 1f, 0f);
            return Save(go, "Ambient_Motes");
        }

        /// <summary>Low, slow mist puffs creeping over the ground in an area (box 30 x 0.5 x 30 m).</summary>
        static GameObject GroundMist(Material material)
        {
            var go = new GameObject("Ambient_Mist");
            ParticleSystem mist = Looping(go, "Mist", material, 40);
            var main = mist.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startColor = new Color(0.55f, 0.6f, 0.75f, 0.12f);
            main.prewarm = true;
            var emission = mist.emission;
            emission.rateOverTime = 3f;
            var shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.5f, 30f);
            shape.position = new Vector3(0f, 0.4f, 0f);
            var fade = mist.colorOverLifetime;
            fade.enabled = true;
            fade.color = Gradient(Color.white, 0f, 1f, 0f);
            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortingFudge = 10f;
            return Save(go, "Ambient_Mist");
        }

        /// <summary>A looping, always-on particle system on a child object.</summary>
        static ParticleSystem Looping(GameObject root, string name, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            return system;
        }

        static void FadeAndShrink(ParticleSystem system, Color start, Color end)
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        /// <summary>A colour that fades in, holds, and fades out over a particle's life.</summary>
        static Gradient Gradient(Color color, float startAlpha, float peakAlpha, float endAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(peakAlpha, 0.3f), new GradientAlphaKey(peakAlpha, 0.7f), new GradientAlphaKey(endAlpha, 1f) });
            return gradient;
        }

        /// <summary>A 64 px soft round dot (white, alpha falling off to the edge), saved as an asset.</summary>
        static Texture2D SoftDot()
        {
            string path = MaterialFolder + "/Fx_SoftDot.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static LineRenderer Ring(GameObject parent, string name, float width, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 28;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.sharedMaterial = material;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
            return ring;
        }

        static MeshRenderer MeshObject(GameObject go, string builtinMesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(builtinMesh);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return meshRenderer;
        }

        /// <summary>A one-shot particle system with the shared blood/spark settings; it returns itself to the pool when done.</summary>
        static ParticleSystem Particles(GameObject go, Material material, Color color, float duration, int maxParticles)
        {
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.startColor = color;
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.Callback;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.02f;
            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            return system;
        }

        static GameObject Save(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- Materials

        /// <summary>URP's particle shader, alpha blended or additive (for glowing sparks).</summary>
        static Material ParticleMaterial(string name, bool additive, Texture2D texture = null)
        {
            Material material = LoadOrCreate(name, "Universal Render Pipeline/Particles/Unlit");
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP Unlit: a flat colour (tinted per object with property blocks), optionally see-through.</summary>
        static Material UnlitMaterial(string name, Color color, bool transparent)
        {
            Material material = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material LoadOrCreate(string name, string shaderName)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
