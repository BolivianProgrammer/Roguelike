using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The night look shared by the hub and the missions: a cold moon, a dark sky, blue-violet fog, and the authored
    /// fire / embers / mist effect prefabs (Prefabs/Effects, made by Roguelike > Build Effect Prefabs).
    /// Scene builders call this; nothing here runs in the game.
    /// </summary>
    public static class SceneAtmosphere
    {
        const string EffectFolder = "Assets/_Project/Prefabs/Effects/";
        const string SkyPath = "Assets/_Project/Materials/Hub_NightSky.mat";
        public const string BrazierModel = ArtImporter.HubArtFolder + "/Models/Brazier.fbx";

        /// <summary>Moon, ambient light, fog and sky. Brightness scales the moon and ambient (1 = the hub).</summary>
        public static void Night(float fogStart, float fogEnd, float brightness = 1f)
        {
            GameObject moonObject = GameObject.Find("Directional Light");
            if (moonObject == null) moonObject = new GameObject("Directional Light", typeof(Light));
            var moon = moonObject.GetComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.9f);
            moon.intensity = 0.32f * brightness;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.7f;
            moonObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.sun = moon;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.14f, 0.24f) * brightness;
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.08f, 0.12f) * brightness;
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.035f, 0.045f) * brightness;

            // Fog swallows everything far away, so no edge of the world ever shows.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.07f, 0.07f, 0.12f);
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            sky.SetFloat("_SunDisk", 0f);
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetColor("_SkyTint", new Color(0.22f, 0.2f, 0.35f));
            sky.SetColor("_GroundColor", new Color(0.05f, 0.05f, 0.07f));
            sky.SetFloat("_Exposure", 0.22f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        /// <summary>An authored effect prefab placed at a point (fire, embers, mist). Null if it has not been built.</summary>
        public static GameObject Effect(string name, Transform parent, Vector3 position, float scale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectFolder + name + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[SceneAtmosphere] Missing effect " + name + ": run Roguelike > Build Effect Prefabs.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>A point light with no shadows (cheap on the headset).</summary>
        public static Light Glow(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
