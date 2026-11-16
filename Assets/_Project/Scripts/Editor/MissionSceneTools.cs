using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Roguelike.Enemies;
using Roguelike.Player;
using Roguelike.Run;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Helpers for turning a level you built yourself into a playable mission (see MISSIONS.md).
    /// Roguelike > Mission Scene > Check Open Scene lists what is missing; Add Missing Pieces adds only what is
    /// missing and never removes or moves anything; Add Chest / Add Challenge Shrine place one where the Scene
    /// view is looking.
    /// </summary>
    public static class MissionSceneTools
    {
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string BossPrefabPath = "Assets/_Project/Prefabs/Enemies/Brute.prefab";

        [MenuItem("Roguelike/Mission Scene/Check Open Scene")]
        public static void Check()
        {
            var report = new StringBuilder("[Mission check] " + EditorSceneManager.GetActiveScene().name + "\n");
            int problems = 0;

            void Need(bool ok, string what)
            {
                report.Append(ok ? "  OK   " : "  MISSING  ").Append(what).Append('\n');
                if (!ok) problems++;
            }

            var mission = Object.FindObjectOfType<MissionManager>();
            Need(Object.FindObjectOfType<PlayerHealth>() != null, "Player (Assets/_Project/Prefabs/Player.prefab), standing where the run starts");
            Need(Object.FindObjectOfType<LevelNavMesh>() != null, "Level NavMesh (an empty object with the LevelNavMesh component), so enemies can find paths");
            Need(mission != null, "Mission (an object with MissionManager)");
            var totems = Object.FindObjectsOfType<Totem>();
            Need(totems.Length > 0, "at least one Totem (any object with the Totem component and a collider)");
            var zones = Object.FindObjectsOfType<ExtractionZone>();
            Need(zones.Length > 0, "an Extraction Zone (the portal out)");
            if (mission != null)
            {
                var data = new SerializedObject(mission);
                Need(data.FindProperty("totems").arraySize == totems.Length, "the Mission lists every Totem in the scene (" + data.FindProperty("totems").arraySize + " of " + totems.Length + ")");
                Need(data.FindProperty("extraction").objectReferenceValue != null, "the Mission's Extraction is set");
                Need(data.FindProperty("bossPrefab").objectReferenceValue != null, "the Mission's Boss Prefab is set");
                Need(data.FindProperty("bossSpawnPoint").objectReferenceValue != null, "the Mission's Boss Spawn Point is set");
            }
            Need(Object.FindObjectsOfType<EnemySpawner>().Length > 0, "enemy spawners (EnemySpawner) guarding the totems");
            report.Append("  (optional) treasure chests: " + Object.FindObjectsOfType<TreasureChest>().Length + ", challenge shrines: " + Object.FindObjectsOfType<KillChallenge>().Length + "\n");

            bool inBuild = false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.path == EditorSceneManager.GetActiveScene().path && scene.enabled) inBuild = true;
            Need(inBuild, "the scene is in File > Build Settings (and a Mission asset in Resources/MissionCatalog names it)");

            report.Append(problems == 0 ? "Ready to play." : problems + " thing(s) to fix. Roguelike > Mission Scene > Add Missing Pieces can add most of them.");
            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        [MenuItem("Roguelike/Mission Scene/Add Missing Pieces")]
        public static void AddMissing()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var player = Object.FindObjectOfType<PlayerHealth>();
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = PivotOnGround();
                    player = instance.GetComponentInChildren<PlayerHealth>();
                    Undo.RegisterCreatedObjectUndo(instance, "Add Player");
                }
            }

            if (Object.FindObjectOfType<LevelNavMesh>() == null)
                Undo.RegisterCreatedObjectUndo(new GameObject("Level NavMesh", typeof(LevelNavMesh)), "Add Level NavMesh");

            var zone = Object.FindObjectOfType<ExtractionZone>();
            if (zone == null)
            {
                Vector3 at = player != null ? player.transform.position + Vector3.back * 5f : PivotOnGround();
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Extraction Zone";
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.position = at + Vector3.up * 0.02f;
                ring.transform.localScale = new Vector3(3f, 0.02f, 3f);
                zone = ring.AddComponent<ExtractionZone>();
                var zoneData = new SerializedObject(zone);
                zoneData.FindProperty("ring").objectReferenceValue = ring.GetComponent<MeshRenderer>();
                zoneData.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(ring, "Add Extraction Zone");
            }

            var mission = Object.FindObjectOfType<MissionManager>();
            if (mission == null)
            {
                var go = new GameObject("Mission");
                mission = go.AddComponent<MissionManager>();
                Undo.RegisterCreatedObjectUndo(go, "Add Mission");
            }

            var data = new SerializedObject(mission);
            var totems = new List<Totem>(Object.FindObjectsOfType<Totem>());
            var totemList = data.FindProperty("totems");
            totemList.arraySize = totems.Count;
            for (int i = 0; i < totems.Count; i++) totemList.GetArrayElementAtIndex(i).objectReferenceValue = totems[i];
            if (data.FindProperty("extraction").objectReferenceValue == null) data.FindProperty("extraction").objectReferenceValue = zone;
            if (data.FindProperty("bossPrefab").objectReferenceValue == null)
                data.FindProperty("bossPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (data.FindProperty("bossSpawnPoint").objectReferenceValue == null)
            {
                var point = new GameObject("Boss Spawn Point").transform;
                point.position = PivotOnGround();
                Undo.RegisterCreatedObjectUndo(point.gameObject, "Add Boss Spawn Point");
                data.FindProperty("bossSpawnPoint").objectReferenceValue = point;
            }
            data.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Mission tools] Added what was missing to " + scene.name + ". Save the scene, then run Check Open Scene.");
            Check();
        }

        [MenuItem("Roguelike/Mission Scene/Add Chest Here")]
        public static void AddChest()
        {
            var go = new GameObject("Treasure Chest", typeof(TreasureChest));
            go.transform.position = PivotOnGround();
            Undo.RegisterCreatedObjectUndo(go, "Add Treasure Chest");
            Selection.activeGameObject = go;
        }

        [MenuItem("Roguelike/Mission Scene/Add Challenge Shrine Here")]
        public static void AddChallenge()
        {
            var go = new GameObject("Challenge Shrine", typeof(KillChallenge));
            go.transform.position = PivotOnGround();
            Undo.RegisterCreatedObjectUndo(go, "Add Challenge Shrine");
            Selection.activeGameObject = go;
        }

        /// <summary>The point on the ground the Scene view is centred on.</summary>
        static Vector3 PivotOnGround()
        {
            var view = SceneView.lastActiveSceneView;
            Vector3 pivot = view != null ? view.pivot : Vector3.zero;
            if (Physics.Raycast(pivot + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f)) return hit.point;
            pivot.y = 0f;
            return pivot;
        }
    }
}
