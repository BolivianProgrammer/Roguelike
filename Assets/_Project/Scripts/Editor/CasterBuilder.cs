using UnityEditor;
using UnityEngine;
using Roguelike.Enemies;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the Caster: a ranged enemy made from the Grunt as a prefab variant, so changes to the Grunt carry over.
    /// It has no sword, keeps its distance and throws bolts the player can block or bat back.
    /// Run from the menu: Roguelike > Build Caster Prefab (after the Grunt prefab exists).
    /// </summary>
    public static class CasterBuilder
    {
        const string GruntPrefabPath = "Assets/_Project/Prefabs/Enemies/Grunt.prefab";
        const string CasterPrefabPath = "Assets/_Project/Prefabs/Enemies/Caster.prefab";
        const string MaterialPath = "Assets/_Project/Materials/Graybox_Caster.mat";

        [MenuItem("Roguelike/Build Caster Prefab")]
        public static GameObject Build()
        {
            var grunt = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
            if (grunt == null)
            {
                Debug.LogError("[CasterBuilder] Build the Grunt prefab first.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(grunt);
            instance.name = "Caster";

            // No sword: hide it and its hitbox. (A prefab variant cannot delete parts of its parent, only switch them off.)
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                if (t.name == "Grunt Sword" || t.name == "Grunt Sword Hitbox") t.gameObject.SetActive(false);

            // A different colour so it can be told apart from a Grunt at a distance.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.25f, 0.5f, 0.9f));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                skin.sharedMaterials = materials;
            }

            var animator = instance.GetComponent<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            var health = new SerializedObject(instance.GetComponent<EnemyHealth>());
            health.FindProperty("maxHealth").floatValue = 60f;
            health.FindProperty("essenceReward").intValue = 15;
            health.FindProperty("coinReward").intValue = 8;
            health.ApplyModifiedPropertiesWithoutUndo();

            var brain = new SerializedObject(instance.GetComponent<EnemyBrain>());
            brain.FindProperty("weaponHitbox").objectReferenceValue = null;
            brain.FindProperty("weaponVisual").objectReferenceValue = null;
            brain.FindProperty("isRanged").boolValue = true;
            brain.FindProperty("muzzle").objectReferenceValue = hand;
            brain.FindProperty("aggroRange").floatValue = 18f;
            brain.FindProperty("timeBetweenAttacks").vector2Value = new Vector2(1.8f, 3f);

            // One attack: the slash animation doubles as a throw, releasing the bolt where the swing is fastest.
            var attacks = brain.FindProperty("attacks");
            attacks.arraySize = 1;
            var attack = attacks.GetArrayElementAtIndex(0);
            attack.FindPropertyRelative("stateName").stringValue = "SlashA";
            attack.FindPropertyRelative("duration").floatValue = 1.5f;
            attack.FindPropertyRelative("hitStart").floatValue = 0.6f;
            attack.FindPropertyRelative("hitEnd").floatValue = 0.7f;
            attack.FindPropertyRelative("damage").floatValue = 12f;
            attack.FindPropertyRelative("ranged").boolValue = true;
            brain.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, CasterPrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log("[CasterBuilder] Built " + CasterPrefabPath);
            return prefab;
        }
    }
}
