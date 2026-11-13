using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Enemies;
using Roguelike.Player;
using Roguelike.Run;
using Roguelike.Weapons;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds everything the first mission needs: a reusable Player prefab (taken from the test scene),
    /// the buff assets, the boss, and a graybox mission scene wired up and ready to play.
    /// Run from the menu: Roguelike > Build Mission Scene. The level layout is placeholder; replace it freely,
    /// keeping the Mission object and its references.
    /// </summary>
    public static class MissionBuilder
    {
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string SwordPrefabPath = "Assets/_Project/Prefabs/Weapons/Sword.prefab";
        const string BrutePrefabPath = "Assets/_Project/Prefabs/Enemies/Brute.prefab";
        const string GruntPrefabPath = "Assets/_Project/Prefabs/Enemies/Grunt.prefab";
        const string BuffFolder = "Assets/_Project/Data/Buffs";
        const string MaterialFolder = "Assets/_Project/Materials/";
        const string TestScenePath = "Assets/Scenes/VRTestScene.unity";
        const string MissionScenePath = "Assets/Scenes/Mission01.unity";

        [MenuItem("Roguelike/Build Mission Scene")]
        public static void Build()
        {
            if (!EnsurePlayerPrefab()) return;
            BuffDefinition[] buffs = BuildBuffs();
            GameObject brute = BuildBrute();
            BuildScene(buffs, brute);
            Debug.Log("[MissionBuilder] Built " + MissionScenePath);
        }

        // ---------------------------------------------------------------- Player prefab

        /// <summary>Groups the player rig in the test scene under one object and saves it as a prefab, once.</summary>
        static bool EnsurePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath) != null) return true;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TestScenePath)
            {
                EditorSceneManager.SaveOpenScenes();
                scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            GameObject sword = GameObject.Find("Sword");
            if (sword != null && AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath) == null)
                PrefabUtility.SaveAsPrefabAssetAndConnect(sword, SwordPrefabPath, InteractionMode.AutomatedAction);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                var player = new GameObject("Player");
                foreach (var name in new[] { "XR Interaction Manager", "XR Origin (XR Rig)", "Left Physics Hand", "Right Physics Hand", "Player Body", "Systems" })
                {
                    GameObject part = GameObject.Find(name);
                    if (part == null)
                    {
                        Debug.LogError("[MissionBuilder] Could not find '" + name + "' in the test scene.");
                        return false;
                    }
                    part.transform.SetParent(player.transform, true);
                }
                PrefabUtility.SaveAsPrefabAssetAndConnect(player, PlayerPrefabPath, InteractionMode.AutomatedAction);

                // The prefab cannot keep a reference to the scene's sword, so restore it on this instance.
                SetStartingItem(player, sword);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        static void SetStartingItem(GameObject player, GameObject item)
        {
            if (item == null) return;
            foreach (var holster in player.GetComponentsInChildren<Holster>())
            {
                if (holster.name != "Holster Left Hip") continue;
                var data = new SerializedObject(holster);
                data.FindProperty("startingItem").objectReferenceValue = item.GetComponent<Grabbable>();
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------------------------------------------------------------- Buffs

        static BuffDefinition[] BuildBuffs()
        {
            if (!AssetDatabase.IsValidFolder(BuffFolder)) AssetDatabase.CreateFolder("Assets/_Project/Data", "Buffs");

            return new[]
            {
                Buff("Might", "+25% damage", new Color(1f, 0.35f, 0.25f), (StatType.Damage, 0.25f)),
                Buff("Vigor", "+30% max health", new Color(0.3f, 1f, 0.4f), (StatType.MaxHealth, 0.3f)),
                Buff("Swiftness", "+20% move speed", new Color(0.3f, 0.8f, 1f), (StatType.MoveSpeed, 0.2f)),
                Buff("Endurance", "+40% max stamina", new Color(1f, 0.9f, 0.3f), (StatType.MaxStamina, 0.4f)),
                Buff("Second Wind", "+50% stamina regen", new Color(0.7f, 1f, 0.9f), (StatType.StaminaRegen, 0.5f)),
                Buff("Glass Cannon", "+50% damage, -20% max health", new Color(1f, 0.4f, 0.9f), (StatType.Damage, 0.5f), (StatType.MaxHealth, -0.2f)),
            };
        }

        static BuffDefinition Buff(string name, string description, Color color, params (StatType stat, float percent)[] effects)
        {
            string path = BuffFolder + "/" + name.Replace(" ", "") + ".asset";
            var buff = AssetDatabase.LoadAssetAtPath<BuffDefinition>(path);
            if (buff == null)
            {
                buff = ScriptableObject.CreateInstance<BuffDefinition>();
                AssetDatabase.CreateAsset(buff, path);
            }

            buff.displayName = name;
            buff.description = description;
            buff.color = color;
            buff.effects = new BuffDefinition.Effect[effects.Length];
            for (int i = 0; i < effects.Length; i++)
                buff.effects[i] = new BuffDefinition.Effect { stat = effects[i].stat, percent = effects[i].percent };
            EditorUtility.SetDirty(buff);
            return buff;
        }

        // ---------------------------------------------------------------- Boss

        /// <summary>The boss is a bigger, tougher Grunt for now: a prefab variant, so Grunt changes carry over.</summary>
        static GameObject BuildBrute()
        {
            var grunt = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(grunt);
            instance.name = "Brute";
            instance.transform.localScale = Vector3.one * 1.4f;

            var health = new SerializedObject(instance.GetComponent<EnemyHealth>());
            health.FindProperty("maxHealth").floatValue = 450f;
            health.FindProperty("canBeFinished").boolValue = false;
            health.ApplyModifiedPropertiesWithoutUndo();

            var brain = new SerializedObject(instance.GetComponent<EnemyBrain>());
            brain.FindProperty("aggroRange").floatValue = 40f;
            brain.FindProperty("walkSpeed").floatValue = 1.6f;
            brain.FindProperty("runSpeed").floatValue = 3.2f;
            brain.FindProperty("stopDistance").floatValue = 1.8f;
            brain.FindProperty("attackRange").floatValue = 2.2f;
            brain.FindProperty("personalSpace").floatValue = 1.4f;
            brain.FindProperty("flinchThreshold").floatValue = 30f;
            brain.FindProperty("heavyStaggerThreshold").floatValue = 150f;
            brain.FindProperty("parriedStun").floatValue = 2f;
            var attacks = brain.FindProperty("attacks");
            for (int i = 0; i < attacks.arraySize; i++)
            {
                var damage = attacks.GetArrayElementAtIndex(i).FindPropertyRelative("damage");
                damage.floatValue = i == 0 ? 28f : 45f;
            }
            brain.ApplyModifiedPropertiesWithoutUndo();

            // Too tough to take apart while it is alive; its corpse still comes apart.
            var dismemberment = new SerializedObject(instance.GetComponent<EnemyDismemberment>());
            dismemberment.FindProperty("headHealth").floatValue = 600f;
            dismemberment.FindProperty("armHealth").floatValue = 260f;
            dismemberment.FindProperty("legHealth").floatValue = 320f;
            dismemberment.FindProperty("torsoHealth").floatValue = 700f;
            dismemberment.FindProperty("corpseFactor").floatValue = 0.15f;
            dismemberment.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, BrutePrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        // ---------------------------------------------------------------- Scene

        static void BuildScene(BuffDefinition[] buffs, GameObject brute)
        {
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material ground = GrayboxMaterial("Graybox_Ground", new Color(0.36f, 0.38f, 0.4f));
            Material wall = GrayboxMaterial("Graybox_Wall", new Color(0.22f, 0.23f, 0.26f));
            Material totemMaterial = GrayboxMaterial("Graybox_Totem", new Color(0.5f, 0.2f, 0.8f));
            Material portalMaterial = GrayboxMaterial("Graybox_Portal", new Color(0.1f, 0.5f, 0.6f));

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var level = new GameObject("Level").transform;

            // Ground and boundary. The play area is 80 x 80 meters.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(level);
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = ground;

            Block(level, "Wall North", new Vector3(0f, 2f, 40f), new Vector3(80f, 4f, 1f), wall);
            Block(level, "Wall South", new Vector3(0f, 2f, -40f), new Vector3(80f, 4f, 1f), wall);
            Block(level, "Wall East", new Vector3(40f, 2f, 0f), new Vector3(1f, 4f, 80f), wall);
            Block(level, "Wall West", new Vector3(-40f, 2f, 0f), new Vector3(1f, 4f, 80f), wall);

            // Cover and landmarks, kept low and sparse: enemies walk in straight lines for now.
            Vector3[] pillars =
            {
                new Vector3(-8f, 0f, -18f), new Vector3(9f, 0f, -16f), new Vector3(-14f, 0f, 6f), new Vector3(13f, 0f, 10f),
                new Vector3(-6f, 0f, 16f), new Vector3(7f, 0f, 18f), new Vector3(-28f, 0f, 14f), new Vector3(28f, 0f, -18f),
            };
            for (int i = 0; i < pillars.Length; i++)
                Block(level, "Pillar " + (i + 1), pillars[i] + Vector3.up * 1.5f, new Vector3(1.2f, 3f, 1.2f), wall);

            // Player start, with spare weapons on a table.
            Vector3 start = new Vector3(0f, 0f, -30f);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            player.transform.position = start;

            var sword = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath));
            sword.transform.position = start + new Vector3(-0.4f, 1f, 0.6f);
            SetStartingItem(player, sword);

            Block(level, "Weapon Table", start + new Vector3(2.2f, 0.4f, 1.5f), new Vector3(1.6f, 0.8f, 0.7f), wall);
            PlaceWeapon("Assets/_Project/Prefabs/Weapons/Hammer.prefab", start + new Vector3(1.9f, 0.9f, 1.5f));
            PlaceWeapon("Assets/_Project/Prefabs/Weapons/Dagger.prefab", start + new Vector3(2.7f, 0.9f, 1.5f));

            // The way out, just behind the start.
            var portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portal.name = "Extraction Zone";
            Object.DestroyImmediate(portal.GetComponent<Collider>());
            portal.transform.position = start + new Vector3(0f, 0.02f, -5f);
            portal.transform.localScale = new Vector3(3f, 0.02f, 3f);
            portal.GetComponent<MeshRenderer>().sharedMaterial = portalMaterial;
            var extraction = portal.AddComponent<ExtractionZone>();
            var extractionData = new SerializedObject(extraction);
            extractionData.FindProperty("ring").objectReferenceValue = portal.GetComponent<MeshRenderer>();
            extractionData.ApplyModifiedPropertiesWithoutUndo();

            // Three totems, each guarded.
            var grunt = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
            Vector3[] totemSpots = { new Vector3(-22f, 0f, -8f), new Vector3(22f, 0f, -2f), new Vector3(0f, 0f, 28f) };
            var totems = new List<Totem>();
            for (int i = 0; i < totemSpots.Length; i++)
            {
                totems.Add(BuildTotem(level, "Totem " + (i + 1), totemSpots[i], totemMaterial, wall));
                BuildGuards(level, "Guards " + (i + 1), totemSpots[i], grunt);
            }

            // One ranged enemy near each totem, if the Caster prefab has been built.
            var caster = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Caster.prefab");
            Vector3[] casterSpots = { new Vector3(-22f, 0f, -1f), new Vector3(22f, 0f, 5f), new Vector3(0f, 0f, 35f) };
            for (int i = 0; caster != null && i < casterSpots.Length; i++)
            {
                var spawner = new GameObject("Caster " + (i + 1)).AddComponent<EnemySpawner>();
                spawner.transform.position = casterSpots[i];
                var spawnerData = new SerializedObject(spawner);
                spawnerData.FindProperty("enemyPrefab").objectReferenceValue = caster;
                spawnerData.FindProperty("maxAlive").intValue = 1;
                spawnerData.FindProperty("totalSpawns").intValue = 1;
                spawnerData.FindProperty("activationRange").floatValue = 22f;
                spawnerData.ApplyModifiedPropertiesWithoutUndo();
            }

            // Works out where enemies can walk each time the scene loads.
            new GameObject("Level NavMesh").AddComponent<LevelNavMesh>();

            // The boss appears in the middle once the totems are gone.
            var bossPoint = new GameObject("Boss Spawn Point").transform;
            bossPoint.SetParent(level);
            bossPoint.SetPositionAndRotation(new Vector3(0f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));

            var mission = new GameObject("Mission").AddComponent<MissionManager>();
            var missionData = new SerializedObject(mission);
            SetArray(missionData.FindProperty("totems"), totems);
            missionData.FindProperty("extraction").objectReferenceValue = extraction;
            SetArray(missionData.FindProperty("buffPool"), buffs);
            missionData.FindProperty("bossPrefab").objectReferenceValue = brute;
            missionData.FindProperty("bossSpawnPoint").objectReferenceValue = bossPoint;
            SetArray(missionData.FindProperty("bossRewards"), new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/Hammer.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/Dagger.prefab"),
            });
            missionData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MissionScenePath);
            AddToBuildSettings(MissionScenePath);
            AddToBuildSettings(TestScenePath);
        }

        static Totem BuildTotem(Transform level, string name, Vector3 position, Material totemMaterial, Material baseMaterial)
        {
            Block(level, name + " Base", position + Vector3.up * 0.15f, new Vector3(1.6f, 0.3f, 1.6f), baseMaterial);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = name;
            pillar.transform.SetParent(level);
            pillar.transform.position = position + Vector3.up * 1.4f;
            pillar.transform.localScale = new Vector3(0.6f, 1.1f, 0.6f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = totemMaterial;

            var totem = pillar.AddComponent<Totem>();
            var data = new SerializedObject(totem);
            data.FindProperty("body").objectReferenceValue = pillar.GetComponent<MeshRenderer>();
            data.ApplyModifiedPropertiesWithoutUndo();
            return totem;
        }

        /// <summary>Three Grunts per totem, two at a time, that wake when the player gets close.</summary>
        static void BuildGuards(Transform level, string name, Vector3 totemPosition, GameObject grunt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(level);
            go.transform.position = totemPosition;

            var points = new List<Transform>();
            Vector3[] offsets = { new Vector3(-3f, 0f, 2f), new Vector3(3f, 0f, 2f), new Vector3(0f, 0f, 4f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject("Spawn Point " + (i + 1)).transform;
                point.SetParent(go.transform);
                point.position = totemPosition + offsets[i];
                points.Add(point);
            }

            var spawner = go.AddComponent<EnemySpawner>();
            var data = new SerializedObject(spawner);
            data.FindProperty("enemyPrefab").objectReferenceValue = grunt;
            SetArray(data.FindProperty("spawnPoints"), points);
            data.FindProperty("maxAlive").intValue = 2;
            data.FindProperty("totalSpawns").intValue = 3;
            data.FindProperty("respawnDelay").floatValue = 2f;
            data.FindProperty("activationRange").floatValue = 16f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Helpers

        static void PlaceWeapon(string prefabPath, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            weapon.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 90f, 0f));
        }

        static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        static Material GrayboxMaterial(string name, Color color)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetArray<T>(SerializedProperty property, IList<T> items) where T : Object
        {
            property.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var existing in scenes)
                if (existing.path == scenePath) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
