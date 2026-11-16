using UnityEditor;
using UnityEngine;
using Roguelike.Weapons;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the player's weapon prefabs (Sword, Hammer, Dagger) from the weapon models and their stat assets.
    /// Run from the menu (Roguelike > Build Weapon Prefabs). Convention for every weapon: the grip is at the
    /// prefab's origin, the weapon extends along forward (blue), and a blade's edge faces right (red).
    /// The prefabs keep their file names, so scenes, the item database and saves keep working.
    /// </summary>
    public static class WeaponBuilder
    {
        const string DataFolder = "Assets/_Project/Data/Weapons/";
        const string PrefabFolder = "Assets/_Project/Prefabs/Weapons";

        /// <summary>A box-shaped piece of a weapon's collision: where it is and how big.</summary>
        struct Part
        {
            public string name;
            public Vector3 centre, size;
            public Part(string name, Vector3 centre, Vector3 size) { this.name = name; this.centre = centre; this.size = size; }
        }

        [MenuItem("Roguelike/Build Weapon Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Weapons");

            BuildSword();
            BuildHammer();
            BuildDagger();
            BuildFineSword();
            BuildFineDagger();
            BuildCleaver();
            BuildGuardianAxe();
            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponBuilder] Built the weapon prefabs in " + PrefabFolder);
        }

        static void BuildFineSword()
        {
            // A better-made sword: the same balance, more damage, a faster special.
            var definition = LoadOrCreateDefinition("Sword_Fine");
            definition.displayName = "Fine Sword";
            definition.mass = 1.4f;
            definition.baseDamage = 32f;
            definition.minHitSpeed = 1.5f;
            definition.fullDamageSpeed = 6f;
            definition.minSwingDistance = 0.3f;
            definition.hitCooldown = 0.32f;
            definition.staggerMultiplier = 1.1f;
            definition.blockStability = 0.78f;
            EditorUtility.SetDirty(definition);

            Finish<EmpoweredStrikeSpecial>("Sword_Fine", definition, twoHanded: true,
                handle: new[]
                {
                    new Part("Handle", new Vector3(0f, 0f, -0.02f), new Vector3(0.05f, 0.05f, 0.24f)),
                    new Part("Guard", new Vector3(0f, 0f, 0.125f), new Vector3(0.22f, 0.04f, 0.05f)),
                },
                striker: new Part("Blade", new Vector3(0f, 0f, 0.545f), new Vector3(0.06f, 0.014f, 0.79f)),
                specialName: "Power Strike", activeTime: 3f, cooldown: 6f, glow: new Color(0.6f, 0.85f, 1f), modelName: "Sword");
        }

        static void BuildFineDagger()
        {
            var definition = LoadOrCreateDefinition("Dagger_Fine");
            definition.displayName = "Fine Dagger";
            definition.mass = 0.4f;
            definition.baseDamage = 19f;
            definition.minHitSpeed = 1f;
            definition.fullDamageSpeed = 4f;
            definition.minSwingDistance = 0.12f;
            definition.minDamageFraction = 0.3f;
            definition.hitCooldown = 0.18f;
            definition.staggerMultiplier = 0.5f;
            definition.blockStability = 0.62f;
            definition.stabMultiplier = 2f;
            definition.bluntMultiplier = 0.3f;
            EditorUtility.SetDirty(definition);

            Finish<FlurrySpecial>("Dagger_Fine", definition, twoHanded: false,
                handle: new[]
                {
                    new Part("Handle", new Vector3(0f, 0f, -0.03f), new Vector3(0.04f, 0.04f, 0.12f)),
                    new Part("Guard", new Vector3(0f, 0f, 0.07f), new Vector3(0.12f, 0.035f, 0.07f)),
                },
                striker: new Part("Blade", new Vector3(0f, 0f, 0.215f), new Vector3(0.045f, 0.025f, 0.21f)),
                specialName: "Flurry", activeTime: 5f, cooldown: 8f, glow: new Color(0.5f, 1f, 0.8f), modelName: "Dagger");
        }

        static void BuildCleaver()
        {
            // A guardian's spoil: a heavy single-edged chopper whose special opens bleeding wounds.
            var definition = LoadOrCreateDefinition("Cleaver_Revenant");
            definition.displayName = "Revenant Cleaver";
            definition.mass = 2.2f;
            definition.baseDamage = 38f;
            definition.minHitSpeed = 1.7f;
            definition.fullDamageSpeed = 5.8f;
            definition.minSwingDistance = 0.38f;
            definition.hitCooldown = 0.4f;
            definition.staggerMultiplier = 1.3f;
            definition.blockStability = 0.72f;
            definition.hasEdge = true;
            definition.hasPoint = false;
            definition.bluntMultiplier = 0.5f;
            EditorUtility.SetDirty(definition);

            FinishFromModel<BleedSpecial>("Cleaver", "Enemy_Cleaver", definition, twoHanded: true, handleShare: 0.3f,
                specialName: "Bloodletting", activeTime: 6f, cooldown: 12f, glow: new Color(0.8f, 0.05f, 0.05f));
        }

        static void BuildGuardianAxe()
        {
            // The rarest spoil: the guardian's own axe. Slow, brutal, and its shockwave is wider.
            var definition = LoadOrCreateDefinition("Axe_Guardian");
            definition.displayName = "Guardian Axe";
            definition.mass = 3.4f;
            definition.baseDamage = 55f;
            definition.minHitSpeed = 2f;
            definition.fullDamageSpeed = 5.5f;
            definition.minSwingDistance = 0.45f;
            definition.minDamageFraction = 0.25f;
            definition.hitCooldown = 0.5f;
            definition.staggerMultiplier = 1.7f;
            definition.blockStability = 0.82f;
            definition.hasEdge = true;
            definition.hasPoint = false;
            definition.bluntMultiplier = 0.7f;
            EditorUtility.SetDirty(definition);

            FinishFromModel<ShockwaveSpecial>("GuardianAxe", "Enemy_Axe", definition, twoHanded: true, handleShare: 0.62f,
                specialName: "Guardian's Wrath", activeTime: 4f, cooldown: 10f, glow: new Color(1f, 0.3f, 0.1f));
        }

        /// <summary>
        /// Like <see cref="Finish{T}"/>, for weapons whose collision shapes are measured from the model: the first
        /// part along its length is the handle, the rest is the striking part.
        /// </summary>
        static void FinishFromModel<T>(string name, string modelName, WeaponDefinition definition, bool twoHanded, float handleShare,
            string specialName, float activeTime, float cooldown, Color glow) where T : WeaponSpecial
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtImporter.WeaponFolder + "/" + modelName + ".fbx");
            Bounds bounds = new Bounds(new Vector3(0f, 0f, 0.4f), new Vector3(0.1f, 0.03f, 0.8f));
            if (model != null)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                bool first = true;
                foreach (var r in probe.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                Object.DestroyImmediate(probe);
            }

            float start = bounds.min.z, length = bounds.size.z;
            float split = start + length * handleShare;
            var handle = new Part("Handle", new Vector3(0f, 0f, (start + split) * 0.5f), new Vector3(0.045f, 0.045f, split - start));
            var striker = new Part("Head", new Vector3(bounds.center.x, bounds.center.y, (split + bounds.max.z) * 0.5f),
                new Vector3(Mathf.Max(0.04f, bounds.size.x), Mathf.Max(0.02f, bounds.size.y), bounds.max.z - split));
            Finish<T>(name, definition, twoHanded, new[] { handle }, striker, specialName, activeTime, cooldown, glow, modelName);
        }

        static void BuildSword()
        {
            // Balanced: the stats asset is left as it is, since it has been tuned by hand.
            var definition = LoadOrCreateDefinition("Sword_Basic");
            if (string.IsNullOrEmpty(definition.displayName) || definition.displayName == "Weapon") definition.displayName = "Sword";
            EditorUtility.SetDirty(definition);

            Finish<EmpoweredStrikeSpecial>("Sword", definition, twoHanded: true,
                handle: new[]
                {
                    new Part("Handle", new Vector3(0f, 0f, -0.02f), new Vector3(0.05f, 0.05f, 0.24f)),
                    new Part("Guard", new Vector3(0f, 0f, 0.125f), new Vector3(0.22f, 0.04f, 0.05f)),
                },
                striker: new Part("Blade", new Vector3(0f, 0f, 0.545f), new Vector3(0.06f, 0.014f, 0.79f)),
                specialName: "Power Strike", activeTime: 3f, cooldown: 8f, glow: new Color(1f, 0.85f, 0.2f));
        }

        static void BuildHammer()
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
            definition.blockStability = 0.82f;
            definition.hasEdge = false;
            definition.hasPoint = false;
            definition.bluntMultiplier = 1f;
            EditorUtility.SetDirty(definition);

            Finish<ShockwaveSpecial>("Hammer", definition, twoHanded: true,
                handle: new[] { new Part("Shaft", new Vector3(0f, 0f, 0.13f), new Vector3(0.045f, 0.045f, 0.74f)) },
                striker: new Part("Head", new Vector3(-0.005f, 0f, 0.595f), new Vector3(0.25f, 0.105f, 0.185f)),
                specialName: "Shockwave", activeTime: 4f, cooldown: 12f, glow: new Color(1f, 0.5f, 0.1f));
        }

        static void BuildDagger()
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
            definition.blockStability = 0.6f;
            definition.hasEdge = true;
            definition.hasPoint = true;
            definition.slashMultiplier = 1f;
            definition.stabMultiplier = 1.8f;
            definition.bluntMultiplier = 0.3f;
            definition.stabSpeedFactor = 0.5f;
            EditorUtility.SetDirty(definition);

            Finish<FlurrySpecial>("Dagger", definition, twoHanded: false,
                handle: new[]
                {
                    new Part("Handle", new Vector3(0f, 0f, -0.03f), new Vector3(0.04f, 0.04f, 0.12f)),
                    new Part("Guard", new Vector3(0f, 0f, 0.07f), new Vector3(0.12f, 0.035f, 0.07f)),
                },
                striker: new Part("Blade", new Vector3(0f, 0f, 0.215f), new Vector3(0.045f, 0.025f, 0.21f)),
                specialName: "Flurry", activeTime: 4f, cooldown: 10f, glow: new Color(0.4f, 1f, 0.6f));
        }

        /// <summary>
        /// Assembles one weapon: the visible model, invisible box colliders shaped to it (the striker is the
        /// part that deals damage), and the grab, damage and special-attack components.
        /// </summary>
        static void Finish<T>(string name, WeaponDefinition definition, bool twoHanded, Part[] handle, Part striker,
            string specialName, float activeTime, float cooldown, Color glow, string modelName = null) where T : WeaponSpecial
        {
            var root = new GameObject(name);

            // The model keeps its own built-in rotation, which is what points it along forward.
            Renderer modelRenderer = null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtImporter.WeaponFolder + "/" + (modelName ?? name) + ".fbx");
            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                modelRenderer = visual.GetComponentInChildren<Renderer>();
            }
            else Debug.LogWarning("[WeaponBuilder] Missing model for " + name);

            foreach (Part part in handle) AddBox(root.transform, part);
            Collider strikerCollider = AddBox(root.transform, striker);

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
            damageColliders.GetArrayElementAtIndex(0).objectReferenceValue = strikerCollider;
            weaponData.ApplyModifiedPropertiesWithoutUndo();

            var special = root.AddComponent<T>();
            ConfigureSpecial(special, specialName, activeTime, cooldown, glow, modelRenderer);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static Collider AddBox(Transform parent, Part part)
        {
            var go = new GameObject(part.name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = part.centre;
            box.size = part.size;
            return box;
        }

        /// <summary>Fills in the shared settings of a special attack.</summary>
        public static void ConfigureSpecial(WeaponSpecial special, string name, float activeTime, float cooldown, Color glow, Renderer glowRenderer)
        {
            var data = new SerializedObject(special);
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("activeTime").floatValue = activeTime;
            data.FindProperty("cooldown").floatValue = cooldown;
            data.FindProperty("glowColor").colorValue = glow;
            var renderers = data.FindProperty("glowRenderers");
            renderers.arraySize = glowRenderer != null ? 1 : 0;
            if (glowRenderer != null) renderers.GetArrayElementAtIndex(0).objectReferenceValue = glowRenderer;
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
    }
}
