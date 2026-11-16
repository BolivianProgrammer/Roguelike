using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Roguelike.Meta;
using Roguelike.Player;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the title screen scene: a dark void, a title that surfaces out of black, "press any button",
    /// then Continue / New Game / Quit. Run from the menu: Roguelike > Build Main Menu Scene.
    /// Change the title text on the Title object in the scene afterwards; rebuilding overwrites the scene.
    /// </summary>
    public static class MenuBuilder
    {
        const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string EmberMaterialPath = "Assets/_Project/Materials/Menu_Ember.mat";

        [MenuItem("Roguelike/Build Main Menu Scene")]
        public static void Build()
        {
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Nothing but darkness around the menu.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            // Something to stand on, never seen.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor (invisible)";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
            floor.GetComponent<MeshRenderer>().enabled = false;

            // The same player rig as everywhere else, so the headset and hands just work. Desktop movement is
            // switched off here: on a monitor the menu is driven with the keyboard and mouse.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            player.transform.position = Vector3.zero;
            var desktop = player.GetComponentInChildren<DesktopRig>(true);
            if (desktop != null)
            {
                var desktopData = new SerializedObject(desktop);
                desktopData.FindProperty("mode").enumValueIndex = (int)DesktopRig.Mode.Disabled;
                desktopData.ApplyModifiedPropertiesWithoutUndo();
            }
            var camera = player.GetComponentInChildren<Camera>(true);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.position = Vector3.zero;

            // The menu hangs in the dark a few meters ahead, at eye level.
            var canvasGo = new GameObject("Menu Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(1600f, 900f);
            canvasRect.localScale = Vector3.one * 0.0026f;
            canvasRect.position = new Vector3(0f, 1.45f, 3.4f);

            Color warm = new Color(0.86f, 0.82f, 0.74f);
            TMP_Text title = Label(canvasRect, "Title", "ROGUELIKE", 132f, new Vector2(0f, 190f), new Vector2(1500f, 180f), warm, 26f);
            TMP_Text subtitle = Label(canvasRect, "Subtitle", "WORKING TITLE", 26f, new Vector2(0f, 96f), new Vector2(1200f, 40f), new Color(0.55f, 0.52f, 0.47f), 40f);
            Line(canvasRect, "Title Rule", new Vector2(0f, 62f), new Vector2(520f, 2f), new Color(0.5f, 0.46f, 0.4f, 0.6f));
            TMP_Text prompt = Label(canvasRect, "Prompt", "PRESS ANY BUTTON", 34f, new Vector2(0f, -150f), new Vector2(1000f, 50f), warm, 18f);
            TMP_Text footer = Label(canvasRect, "Footer", "PROTOTYPE BUILD", 18f, new Vector2(0f, -410f), new Vector2(1000f, 30f), new Color(0.4f, 0.4f, 0.4f), 20f);

            string[] names = { "CONTINUE", "NEW GAME", "QUIT GAME" };
            var items = new List<MainMenuItem>();
            for (int i = 0; i < names.Length; i++)
            {
                TMP_Text label = Label(canvasRect, "Item " + names[i], names[i], 40f, new Vector2(0f, -90f - i * 78f), new Vector2(640f, 64f), warm, 14f);
                label.raycastTarget = true;
                var item = label.gameObject.AddComponent<MainMenuItem>();
                var itemData = new SerializedObject(item);
                itemData.FindProperty("label").objectReferenceValue = label;
                itemData.ApplyModifiedPropertiesWithoutUndo();
                items.Add(item);
            }

            // Two short rules that slide to whichever entry is selected.
            var marker = new GameObject("Selection Marker", typeof(RectTransform)).GetComponent<RectTransform>();
            marker.SetParent(canvasRect, false);
            marker.sizeDelta = new Vector2(640f, 64f);
            marker.anchoredPosition = new Vector2(0f, -90f);
            Line(marker, "Left Rule", new Vector2(-300f, 0f), new Vector2(90f, 2f), warm);
            Line(marker, "Right Rule", new Vector2(300f, 0f), new Vector2(90f, 2f), warm);

            var menu = canvasGo.AddComponent<MainMenu>();
            var menuData = new SerializedObject(menu);
            menuData.FindProperty("title").objectReferenceValue = title;
            menuData.FindProperty("subtitle").objectReferenceValue = subtitle;
            menuData.FindProperty("prompt").objectReferenceValue = prompt;
            menuData.FindProperty("footer").objectReferenceValue = footer;
            menuData.FindProperty("selectionMarker").objectReferenceValue = marker;
            var itemsProperty = menuData.FindProperty("items");
            itemsProperty.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            menuData.ApplyModifiedPropertiesWithoutUndo();

            BuildEmbers();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildOrder();
            Debug.Log("[MenuBuilder] Built " + ScenePath);
        }

        static TMP_Text Label(RectTransform parent, string name, string text, float size, Vector2 position, Vector2 box, Color color, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = box;
            rect.anchoredPosition = position;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.characterSpacing = spacing;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        static void Line(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>A few slow embers drifting up through the dark, for a little life behind the text.</summary>
        static void BuildEmbers()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, EmberMaterialPath);
            }

            var go = new GameObject("Embers");
            go.transform.position = new Vector3(0f, 0f, 4.5f);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f, 0.9f), new Color(1f, 0.75f, 0.3f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;

            var emission = system.emission;
            emission.rateOverTime = 10f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 3f, 0.2f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.3f;

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        /// <summary>The game starts on the menu, so it goes first in the build order.</summary>
        public static void SetBuildOrder()
        {
            string[] order =
            {
                ScenePath, "Assets/Scenes/Hub.unity", "Assets/Scenes/Mission_Forest.unity",
            };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in order)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
