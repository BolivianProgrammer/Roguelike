using UnityEditor;
using UnityEngine;
using Roguelike.Weapons;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the graybox Hammer and Dagger prefabs and their stat assets from primitives.
    /// Run from the menu (Roguelike > Build Weapon Prefabs). Convention for every weapon: the grip is at the
    /// prefab's origin, the weapon extends along forward (blue), and a blade's edge faces right (red).
    /// </summary>
    public static class WeaponBuilder
    {
        const string DataFolder = "Assets/_Project/Data/Weapons/";
        const string PrefabFolder = "Assets/_Project/Prefabs/Weapons";

        [MenuItem("Roguelike/Build Weapon Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Weapons");

            var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Steel.mat");
            var grip = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/SwordGrip.mat");

            BuildHammer(steel, grip);
            BuildDagger(steel, grip);
            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponBuilder] Built Hammer and Dagger prefabs in " + PrefabFolder);
        }

        static void BuildHammer(Material steel, Material grip)
        {
            // Heavy and slow: every hit is blunt, hits hard and knocks enemies off balance.
            var definition = LoadOrCreateDefinition("Hammer_Basic");
            definition.displayName = "Hammer";
            definition.mass = 4f;
            definition.baseDamage = 45f;
            definition.minHitSpeed = 2f;
            definition.fullDamageSpeed = 5.5f;
            definition.minSwingDistance = 0.5f;
            definition.minDamageFraction = 0.25f;
            definition.hitCooldown = 0.5f;
            definition.staggerMultiplier = 1.6f;
            definition.hasEdge = false;
            definition.hasPoint = false;
            definition.bluntMultiplier = 1f;
            EditorUtility.SetDirty(definition);

            var root = new GameObject("Hammer");
            AddPart(root.transform, "Shaft", new Vector3(0f, 0f, 0.35f), new Vector3(0.035f, 0.035f, 1f), grip);
            GameObject head = AddPart(root.transform, "Head", new Vector3(0f, 0f, 0.86f), new Vector3(0.26f, 0.12f, 0.12f), steel);

            Finish<ShockwaveSpecial>(root, definition, head, twoHanded: true, specialName: "Shockwave",
                activeTime: 4f, cooldown: 12f, glow: new Color(1f, 0.5f, 0.1f));
        }

        static void BuildDagger(Material steel, Material grip)
        {
            // Light and fast: weak slashes, strong stabs, hits again quickly, barely staggers.
            var definition = LoadOrCreateDefinition("Dagger_Basic");
            definition.displayName = "Dagger";
            definition.mass = 0.4f;
            definition.baseDamage = 14f;
            definition.minHitSpeed = 1f;
            definition.fullDamageSpeed = 4f;
            definition.minSwingDistance = 0.12f;
            definition.minDamageFraction = 0.3f;
            definition.hitCooldown = 0.2f;
            definition.staggerMultiplier = 0.5f;
            definition.hasEdge = true;
            definition.hasPoint = true;
            definition.slashMultiplier = 1f;
            definition.stabMultiplier = 1.8f;
            definition.bluntMultiplier = 0.3f;
            definition.stabSpeedFactor = 0.5f;
            EditorUtility.SetDirty(definition);

            var root = new GameObject("Dagger");
            AddPart(root.transform, "Grip", new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.03f, 0.11f), grip);
            AddPart(root.transform, "Guard", new Vector3(0f, 0f, 0.065f), new Vector3(0.08f, 0.02f, 0.02f), steel);
            GameObject blade = AddPart(root.transform, "Blade", new Vector3(0f, 0f, 0.2f), new Vector3(0.035f, 0.008f, 0.25f), steel);

            Finish<FlurrySpecial>(root, definition, blade, twoHanded: false, specialName: "Flurry",
                activeTime: 4f, cooldown: 10f, glow: new Color(0.4f, 1f, 0.6f));
        }

        static void Finish<T>(GameObject root, WeaponDefinition definition, GameObject striker, bool twoHanded,
            string specialName, float activeTime, float cooldown, Color glow) where T : WeaponSpecial
        {
            var body = root.AddComponent<Rigidbody>();
            body.mass = definition.mass;

            var grabbable = root.AddComponent<Grabbable>();
            var grabbableData = new SerializedObject(grabbable);
            grabbableData.FindProperty("allowTwoHanded").boolValue = twoHanded;
            grabbableData.ApplyModifiedPropertiesWithoutUndo();

            var weapon = root.AddComponent<Weapon>();
            var weaponData = new SerializedObject(weapon);
            weaponData.FindProperty("definition").objectReferenceValue = definition;
            var damageColliders = weaponData.FindProperty("damageColliders");
            damageColliders.arraySize = 1;
            damageColliders.GetArrayElementAtIndex(0).objectReferenceValue = striker.GetComponent<Collider>();
            weaponData.ApplyModifiedPropertiesWithoutUndo();

            var special = root.AddComponent<T>();
            ConfigureSpecial(special, specialName, activeTime, cooldown, glow, striker.GetComponent<Renderer>());

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + root.name + ".prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>Fills in the shared settings of a special attack. Also used for the sword in the scene.</summary>
        public static void ConfigureSpecial(WeaponSpecial special, string name, float activeTime, float cooldown, Color glow, Renderer glowRenderer)
        {
            var data = new SerializedObject(special);
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("activeTime").floatValue = activeTime;
            data.FindProperty("cooldown").floatValue = cooldown;
            data.FindProperty("glowColor").colorValue = glow;
            var renderers = data.FindProperty("glowRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = glowRenderer;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static WeaponDefinition LoadOrCreateDefinition(string name)
        {
            string path = DataFolder + name + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition != null) return definition;

            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        static GameObject AddPart(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }
    }
}
