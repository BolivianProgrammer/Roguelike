using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Enemies;
using BodyPart = Roguelike.Combat.BodyPart;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Assembles the enemy prefabs from the Revenant and Boss art: Animator controller, body colliders with
    /// hit zones, ragdoll joints, a weapon in the right hand, dismemberment and the AI.
    /// Run from the menu (Roguelike > Build Enemy Prefabs) after changing models, clips or the numbers below.
    /// The prefabs keep their old file names (Grunt, Brute) so every scene that uses them keeps working.
    /// </summary>
    public static class EnemyBuilder
    {
        const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";

        class Attack
        {
            public string clip;
            public float duration, start, end, damage;
            public DamageType type = DamageType.Slash;
            public Vector2[] extraHits;
            /// <summary>Starts this attack from up to this far (0 = the enemy's attack range).</summary>
            public float range;
            /// <summary>Meters it moves forward during the attack, between the two lunge times.</summary>
            public float lunge, lungeStart, lungeEnd;
            /// <summary>The lunge stops with the player this far away: where the weapon strikes (0 = 0.8 m).</summary>
            public float lungeStop;
            /// <summary>Both hands on the weapon (the off hand is put on the grip by IK in game).</summary>
            public bool twoHanded;
            /// <summary>Breaks any block (only a parry stops it); the weapon glows during the wind-up.</summary>
            public bool guardBreak;
        }

        class Config
        {
            public string name, modelPath, controllerPath, weaponModelPath;
            /// <summary>How much bigger than the Revenant this body is. Scales collision shapes.</summary>
            public float size = 1f;
            public float weaponScale = 1f;
            public Vector3 hitboxCentre, hitboxSize;
            public string idleClip = "Idle", walkClip = "Walk", runClip = "Run";
            /// <summary>Ground speed (m/s) each clip was animated for on this body, to avoid sliding feet.</summary>
            public float walkClipSpeed = 1f, runClipSpeed = 2.5f;
            public float walkSpeed = 1.4f, runSpeed = 3.2f;
            public string stunnedClip = "Stunned", introClip = "Spawn", deathClip = "";
            public Attack[] attacks;
            public System.Action<SerializedObject, SerializedObject, SerializedObject> tune;
            /// <summary>Tints the character's material, so a variant can be told apart. Alpha 0 = keep the original.</summary>
            public Color tint = new Color(0f, 0f, 0f, 0f);
            /// <summary>Overall size of the whole enemy, its weapon and collision included.</summary>
            public float scale = 1f;
            /// <summary>Carries a shield on the left forearm.</summary>
            public bool shield;
        }

        // The Grunt's five attacks; variants pick from these.
        static Attack RightSlash => new Attack { clip = "Attack_RightSlash", duration = 1.47f, start = 0.60f, end = 0.80f, damage = 15f };
        static Attack LeftSlash => new Attack { clip = "Attack_LeftSlash", duration = 1.47f, start = 0.60f, end = 0.80f, damage = 15f };
        static Attack Overhead => new Attack { clip = "Attack_Overhead", duration = 1.60f, start = 0.70f, end = 0.88f, damage = 20f };
        static Attack Thrust => new Attack { clip = "Attack_Thrust", duration = 1.47f, start = 0.65f, end = 0.78f, damage = 16f, type = DamageType.Stab,
            range = 1.45f, lunge = 0.35f, lungeStart = 0.5f, lungeEnd = 0.7f };
        static Attack Heavy => new Attack { clip = "Attack_Heavy", duration = 1.80f, start = 0.95f, end = 1.20f, damage = 28f, twoHanded = true, guardBreak = true };

        [MenuItem("Roguelike/Build Enemy Prefabs")]
        public static void BuildAll()
        {
            BuildGrunt();
            BuildBoss();
            CasterBuilder.Build();
            BuildRunner();
            BuildShieldbearer();
            BuildElite();
            GameDataBuilder.BuildGameAssets();
        }

        /// <summary>Fast and frail: quick slashes and lunges, flinches at every hit.</summary>
        public static GameObject BuildRunner()
        {
            return Build(GruntBody("Runner", new Color(0.78f, 0.82f, 0.7f), 0.92f, new[] { RightSlash, LeftSlash, Thrust }, (health, brain, dismemberment) =>
            {
                health.FindProperty("maxHealth").floatValue = 55f;
                health.FindProperty("essenceReward").intValue = 8;
                health.FindProperty("coinReward").intValue = 4;
                brain.FindProperty("damageMultiplier").floatValue = 0.7f;
                brain.FindProperty("timeBetweenAttacks").vector2Value = new Vector2(0.5f, 1.0f);
                brain.FindProperty("flinchThreshold").floatValue = 6f;
                brain.FindProperty("aggroRange").floatValue = 10f;
                brain.FindProperty("stopDistance").floatValue = 1.0f;
                brain.FindProperty("attackRange").floatValue = 1.15f;
            }, walk: 1.8f, run: 4.6f));
        }

        /// <summary>Slow, with a shield that stops blows from the front: get around it, break it, or cut off the arm.</summary>
        public static GameObject BuildShieldbearer()
        {
            var config = GruntBody("Shieldbearer", new Color(0.55f, 0.6f, 0.72f), 1.02f, new[] { Overhead, Thrust, Heavy }, (health, brain, dismemberment) =>
            {
                health.FindProperty("maxHealth").floatValue = 120f;
                health.FindProperty("essenceReward").intValue = 18;
                health.FindProperty("coinReward").intValue = 10;
                brain.FindProperty("flinchThreshold").floatValue = 18f;
                brain.FindProperty("stopDistance").floatValue = 1.05f;
                brain.FindProperty("attackRange").floatValue = 1.2f;
            }, walk: 1.2f, run: 2.6f);
            config.shield = true;
            return Build(config);
        }

        /// <summary>A hardened veteran: bigger, tougher, hits harder and more often, and drops three times the loot.</summary>
        public static GameObject BuildElite()
        {
            var config = GruntBody("Elite", new Color(0.62f, 0.14f, 0.1f), 1.1f, new[] { RightSlash, LeftSlash, Overhead, Thrust, Heavy }, (health, brain, dismemberment) =>
            {
                health.FindProperty("maxHealth").floatValue = 230f;
                health.FindProperty("essenceReward").intValue = 35;
                health.FindProperty("coinReward").intValue = 20;
                brain.FindProperty("damageMultiplier").floatValue = 1.35f;
                brain.FindProperty("timeBetweenAttacks").vector2Value = new Vector2(0.7f, 1.4f);
                // Half its attacks chain straight into a second: blocking one is not the end of the exchange.
                brain.FindProperty("followUpChance").floatValue = 0.5f;
                brain.FindProperty("flinchThreshold").floatValue = 18f;
                brain.FindProperty("heavyStaggerThreshold").floatValue = 85f;
                brain.FindProperty("aggroRange").floatValue = 10f;
                brain.FindProperty("stopDistance").floatValue = 1.1f;
                brain.FindProperty("attackRange").floatValue = 1.3f;
                dismemberment.FindProperty("headHealth").floatValue = 70f;
                dismemberment.FindProperty("armHealth").floatValue = 55f;
                dismemberment.FindProperty("legHealth").floatValue = 80f;
                dismemberment.FindProperty("torsoHealth").floatValue = 130f;
            }, walk: 1.4f, run: 3.4f);
            // Elites carry the rusty bearded axe instead of the cleaver, so they read differently at a glance.
            config.weaponModelPath = ArtImporter.WeaponFolder + "/Enemy_Axe.fbx";
            config.hitboxCentre = new Vector3(0.05f, 0f, 0.3f);
            config.hitboxSize = new Vector3(0.24f, 0.06f, 0.5f);
            return Build(config);
        }

        /// <summary>A Grunt-shaped enemy with its own look, size, attacks and Animator (so its walk matches its speed).</summary>
        static Config GruntBody(string name, Color tint, float scale, Attack[] attacks,
            System.Action<SerializedObject, SerializedObject, SerializedObject> tune, float walk, float run)
        {
            return new Config
            {
                name = name,
                modelPath = ArtImporter.RevenantFolder + "/Revenant.fbx",
                controllerPath = ArtImporter.RevenantFolder + "/" + name + ".controller",
                weaponModelPath = ArtImporter.WeaponFolder + "/Enemy_Cleaver.fbx",
                hitboxCentre = new Vector3(0.03f, 0f, 0.39f),
                hitboxSize = new Vector3(0.15f, 0.06f, 0.6f),
                walkSpeed = walk,
                runSpeed = run,
                tint = tint,
                scale = scale,
                attacks = attacks,
                tune = tune,
            };
        }

        public static GameObject BuildGrunt()
        {
            return Build(new Config
            {
                name = "Grunt",
                modelPath = ArtImporter.RevenantFolder + "/Revenant.fbx",
                controllerPath = ArtImporter.RevenantFolder + "/Revenant.controller",
                weaponModelPath = ArtImporter.WeaponFolder + "/Enemy_Cleaver.fbx",
                hitboxCentre = new Vector3(0.03f, 0f, 0.39f),
                hitboxSize = new Vector3(0.15f, 0.06f, 0.6f),
                // Strike times come from the animation notes: the window in which the weapon can hurt.
                attacks = new[] { RightSlash, LeftSlash, Overhead, Thrust, Heavy },
                // Measured in play: the cleaver's striking surface reaches 1.04 m (overhead) to 1.24 m (thrust) in
                // front of the Grunt, and the player's body is 0.2 m in radius. Stopping at 1.05 m and only starting
                // a swing within 1.2 m means every attack can connect with a player who stands still.
                tune = (health, brain, dismemberment) =>
                {
                    brain.FindProperty("stopDistance").floatValue = 1.05f;
                    brain.FindProperty("attackRange").floatValue = 1.2f;
                },
            });
        }

        public static GameObject BuildBoss()
        {
            return Build(new Config
            {
                name = "Brute",
                modelPath = ArtImporter.BossFolder + "/Boss.fbx",
                controllerPath = ArtImporter.BossFolder + "/Boss.controller",
                weaponModelPath = ArtImporter.WeaponFolder + "/Enemy_Axe.fbx",
                size = 1.3f,
                weaponScale = 1.7f,
                hitboxCentre = new Vector3(0.09f, 0f, 0.5f),
                hitboxSize = new Vector3(0.4f, 0.1f, 0.85f),
                idleClip = "Boss_Idle",
                walkClip = "Boss_Walk",
                walkClipSpeed = 0.92f,
                runClipSpeed = 3.25f,
                walkSpeed = 1.5f,
                runSpeed = 3f,
                stunnedClip = "Boss_Stunned",
                introClip = "Boss_Roar",
                deathClip = "Boss_Death",
                attacks = new[]
                {
                    new Attack { clip = "Boss_Attack_Sweep", duration = 1.90f, start = 0.80f, end = 1.06f, damage = 30f },
                    new Attack { clip = "Boss_Attack_Combo", duration = 2.40f, start = 0.60f, end = 0.80f, damage = 22f,
                        extraHits = new[] { new Vector2(1.00f, 1.20f), new Vector2(1.48f, 1.68f) } },
                    new Attack { clip = "Boss_Attack_Slam", duration = 2.10f, start = 1.05f, end = 1.27f, damage = 45f, type = DamageType.Blunt, twoHanded = true, guardBreak = true },
                    // The charge covers ground: it starts from further away and lunges forward into the strike.
                    // Measured on the clip: the axe drives forward from 0.83 s and stays out at 1.3-2.2 m in front,
                    // chest high, until about 1.25 s. The lunge stops with the player in the middle of that reach.
                    new Attack { clip = "Boss_Attack_Charge", duration = 1.80f, start = 0.84f, end = 1.10f, damage = 32f, type = DamageType.Stab,
                        range = 3.4f, lunge = 2.2f, lungeStart = 0.45f, lungeEnd = 0.85f, lungeStop = 1.65f },
                },
                tune = (health, brain, dismemberment) =>
                {
                    health.FindProperty("maxHealth").floatValue = 450f;
                    health.FindProperty("canBeFinished").boolValue = false;

                    brain.FindProperty("aggroRange").floatValue = 40f;
                    // Measured reach of the axe: 1.24 m (slam) to 1.95 m (charge); see the Grunt's note.
                    brain.FindProperty("stopDistance").floatValue = 1.3f;
                    brain.FindProperty("attackRange").floatValue = 1.5f;
                    brain.FindProperty("personalSpace").floatValue = 1.4f;
                    brain.FindProperty("flinchThreshold").floatValue = 30f;
                    brain.FindProperty("heavyStaggerThreshold").floatValue = 150f;
                    brain.FindProperty("parriedStun").floatValue = 2f;
                    brain.FindProperty("introDuration").floatValue = 2.7f;
                    // Below half health it roars and fights harder: faster, more often, harder hits.
                    brain.FindProperty("enrageBelow").floatValue = 0.5f;
                    brain.FindProperty("enrageState").stringValue = "Boss_Roar";
                    brain.FindProperty("enrageDuration").floatValue = 2.7f;
                    brain.FindProperty("strafeSpeed").floatValue = 0.78f;
                    brain.FindProperty("backSpeed").floatValue = 0.99f;
                    brain.FindProperty("turnInPlaceSpeed").floatValue = 80f;
                    brain.FindProperty("deathAnimationChance").floatValue = 0f;
                    brain.FindProperty("deathDuration").floatValue = 3f;

                    // Too tough to take apart while it is alive; its corpse still comes apart.
                    dismemberment.FindProperty("headHealth").floatValue = 600f;
                    dismemberment.FindProperty("armHealth").floatValue = 260f;
                    dismemberment.FindProperty("legHealth").floatValue = 320f;
                    dismemberment.FindProperty("torsoHealth").floatValue = 700f;
                    dismemberment.FindProperty("corpseFactor").floatValue = 0.15f;
                },
            });
        }

        static GameObject Build(Config config)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(config.modelPath);
            if (model == null)
            {
                Debug.LogError("[EnemyBuilder] Missing model at " + config.modelPath);
                return null;
            }

            Dictionary<string, AnimationClip> clips = LoadClips();
            AnimatorController controller = BuildController(config, clips);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = config.name;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (config.tint.a > 0f) Tint(root, config.name, config.tint);

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // Bones carry the colliders the player hits, so they must keep animating off-screen.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Rigidbody[] bodies = BuildBody(animator, config.size);
            Transform weapon = BuildWeapon(animator, config);
            GameObject weaponHitbox = BuildWeaponHitbox(root, weapon, config);
            Transform shield = config.shield ? BuildShield(animator) : null;

            var health = root.AddComponent<EnemyHealth>();
            var ragdoll = root.AddComponent<EnemyRagdoll>();
            var dismemberment = root.AddComponent<EnemyDismemberment>();
            var brain = root.AddComponent<EnemyBrain>();

            var ragdollData = new SerializedObject(ragdoll);
            ragdollData.FindProperty("animator").objectReferenceValue = animator;
            var bodyList = ragdollData.FindProperty("bodies");
            bodyList.arraySize = bodies.Length;
            for (int i = 0; i < bodies.Length; i++)
                bodyList.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            ragdollData.ApplyModifiedPropertiesWithoutUndo();

            var dismembermentData = new SerializedObject(dismemberment);
            dismembermentData.FindProperty("limbModel").objectReferenceValue = model;
            dismembermentData.FindProperty("skeletonRoot").objectReferenceValue = animator.GetBoneTransform(HumanBodyBones.Hips);

            var healthData = new SerializedObject(health);

            var brainData = new SerializedObject(brain);
            brainData.FindProperty("animator").objectReferenceValue = animator;
            brainData.FindProperty("ragdoll").objectReferenceValue = ragdoll;
            brainData.FindProperty("weaponHitbox").objectReferenceValue = weaponHitbox.GetComponent<MeleeAttackHitbox>();
            brainData.FindProperty("weaponVisual").objectReferenceValue = weapon;
            brainData.FindProperty("shield").objectReferenceValue = shield;
            brainData.FindProperty("walkSpeed").floatValue = config.walkSpeed;
            brainData.FindProperty("runSpeed").floatValue = config.runSpeed;
            // Long enough for the hit reaction clips (0.85 s) to read before the enemy acts again.
            brainData.FindProperty("flinchDuration").floatValue = 0.8f;
            brainData.FindProperty("stunnedState").stringValue = config.stunnedClip;
            brainData.FindProperty("introState").stringValue = clips.ContainsKey(config.introClip) ? config.introClip : "";
            brainData.FindProperty("introDuration").floatValue = clips.ContainsKey(config.introClip) ? clips[config.introClip].length : 0f;
            brainData.FindProperty("deathState").stringValue = config.deathClip;

            var attacks = brainData.FindProperty("attacks");
            attacks.arraySize = config.attacks.Length;
            for (int i = 0; i < config.attacks.Length; i++)
            {
                Attack attack = config.attacks[i];
                SerializedProperty element = attacks.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("stateName").stringValue = attack.clip;
                element.FindPropertyRelative("duration").floatValue = attack.duration;
                element.FindPropertyRelative("hitStart").floatValue = attack.start;
                element.FindPropertyRelative("hitEnd").floatValue = attack.end;
                element.FindPropertyRelative("damage").floatValue = attack.damage;
                element.FindPropertyRelative("damageType").enumValueIndex = (int)attack.type;
                element.FindPropertyRelative("ranged").boolValue = false;
                element.FindPropertyRelative("limb").enumValueIndex = 0;
                element.FindPropertyRelative("range").floatValue = attack.range;
                element.FindPropertyRelative("lungeDistance").floatValue = attack.lunge;
                element.FindPropertyRelative("lungeTime").vector2Value = new Vector2(attack.lungeStart, attack.lungeEnd);
                element.FindPropertyRelative("lungeStopAt").floatValue = attack.lungeStop > 0f ? attack.lungeStop : 0.8f;
                element.FindPropertyRelative("twoHanded").boolValue = attack.twoHanded;
                element.FindPropertyRelative("guardBreak").boolValue = attack.guardBreak;
                SerializedProperty extra = element.FindPropertyRelative("extraHits");
                extra.arraySize = attack.extraHits != null ? attack.extraHits.Length : 0;
                for (int h = 0; h < extra.arraySize; h++)
                    extra.GetArrayElementAtIndex(h).vector2Value = attack.extraHits[h];
            }

            if (config.tune != null) config.tune(healthData, brainData, dismembermentData);
            healthData.ApplyModifiedPropertiesWithoutUndo();
            brainData.ApplyModifiedPropertiesWithoutUndo();
            dismembermentData.ApplyModifiedPropertiesWithoutUndo();

            // Size last, so the whole enemy (collision, weapon and hitbox included) grows or shrinks together.
            if (!Mathf.Approximately(config.scale, 1f)) root.transform.localScale = Vector3.one * config.scale;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Enemies");
            string prefabPath = PrefabFolder + "/" + config.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            // Reload it from disk, so anything built from this prefab next (the Caster) sees the finished version.
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab.GetComponent<Animator>().runtimeAnimatorController == null)
                Debug.LogError("[EnemyBuilder] " + config.name + " lost its Animator controller while saving.");

            Debug.Log("[EnemyBuilder] Built " + config.name + " with " + bodies.Length + " body parts and " + config.attacks.Length + " attacks.");
            return prefab;
        }

        /// <summary>The character's own material, tinted, saved as Graybox_[name] so it can be tweaked by hand.</summary>
        static void Tint(GameObject root, string name, Color tint)
        {
            string path = "Assets/_Project/Materials/Graybox_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(ArtImporter.RevenantFolder + "/Revenant.mat");
                material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(material);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                skin.sharedMaterials = materials;
            }
        }

        /// <summary>A plain board shield strapped to the outside of the left forearm (graybox until a model exists).</summary>
        static Transform BuildShield(Animator animator)
        {
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shield.name = "Shield";
            // In T-pose the forearm runs along -X; the board faces forward (+Z), centred on the forearm.
            Vector3 centre = (forearm.position + hand.position) * 0.5f + new Vector3(0f, 0f, 0.08f);
            shield.transform.SetPositionAndRotation(centre, Quaternion.identity);
            shield.transform.localScale = new Vector3(0.42f, 0.62f, 0.04f);
            shield.transform.SetParent(forearm, true);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Graybox_Wall.mat");
            if (material != null) shield.GetComponent<MeshRenderer>().sharedMaterial = material;
            return shield.transform;
        }

        // ---------------------------------------------------------------- Animator

        /// <summary>Every clip from the Revenant and Boss animation folders, by name.</summary>
        static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string folder in new[] { ArtImporter.RevenantFolder + "/Animations", ArtImporter.BossFolder + "/Animations" })
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        var clip = asset as AnimationClip;
                        if (clip != null && !clip.name.StartsWith("__preview")) clips[clip.name] = clip;
                    }
            return clips;
        }

        static AnimatorController BuildController(Config config, Dictionary<string, AnimationClip> clips)
        {
            // Reuse the existing controller and empty it, instead of deleting and recreating it: a recreated
            // asset gets a new identity, which leaves prefabs built earlier pointing at nothing.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(config.controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(config.controllerPath);
            }
            else
            {
                AnimatorStateMachine old = controller.layers[0].stateMachine;
                foreach (ChildAnimatorState child in old.states) old.RemoveState(child.state);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(config.controllerPath))
                    if (asset is BlendTree) Object.DestroyImmediate(asset, true);
                while (controller.parameters.Length > 0) controller.RemoveParameter(0);
            }
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            // IK pass on, for the off hand on two-handed attacks (EnemyBrain.OnAnimatorIK; idle cost is negligible).
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Idle, walk and run blend by how fast the enemy is moving. Each clip is sped up or slowed down so
            // its footsteps match the speed the enemy really moves at.
            BlendTree tree;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip(clips, config.idleClip), 0f);
            tree.AddChild(Clip(clips, config.walkClip), config.walkSpeed);
            tree.AddChild(Clip(clips, config.runClip), config.runSpeed);
            ChildMotion[] children = tree.children;
            children[1].timeScale = config.walkSpeed / config.walkClipSpeed;
            children[2].timeScale = config.runSpeed / config.runClipSpeed;
            tree.children = children;
            machine.defaultState = locomotion;

            // Every other clip becomes a state of the same name, started from code (EnemyBrain).
            foreach (var pair in clips)
            {
                if (pair.Key == config.idleClip || pair.Key == config.walkClip || pair.Key == config.runClip) continue;
                AnimatorState state = machine.AddState(pair.Key);
                state.motion = pair.Value;
            }

            // Write the controller out before any prefab is saved against it.
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(config.controllerPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(config.controllerPath);
        }

        static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (clips.TryGetValue(name, out AnimationClip clip)) return clip;
            Debug.LogWarning("[EnemyBuilder] Missing clip " + name);
            return null;
        }

        // ---------------------------------------------------------------- Body colliders and ragdoll

        static Rigidbody[] BuildBody(Animator animator, float size)
        {
            var bodies = new List<Rigidbody>();

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (neck == null) neck = head;

            Rigidbody hipsBody = AddBody(hips, 12f, HitZoneType.Torso, BodyPart.Torso, bodies);
            AddSphere(hips, hips.position, 0.16f * size);

            // The torso ends where the neck starts, and the head's shape reaches down over the neck. A blow to
            // the neck therefore counts as a head hit and takes the head off at the neck cut, instead of adding to
            // the torso wound and splitting the body under the ribs.
            Rigidbody chestBody = AddBody(spine, 15f, HitZoneType.Torso, BodyPart.Torso, bodies);
            AddCapsuleBetween(spine, spine.position, neck.position, 0.16f * size);
            AddJoint(chestBody, hipsBody, 25f, 20f);

            Rigidbody headBody = AddBody(head, 5f, HitZoneType.Head, BodyPart.Head, bodies);
            AddCapsuleBetween(head, neck.position, head.position + Vector3.up * 0.22f * size, 0.11f * size);
            AddJoint(headBody, chestBody, 35f, 25f);

            AddLimb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.055f * size, 0.05f * size, 2.5f, 2f, chestBody, BodyPart.LeftArm, bodies);
            AddLimb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.055f * size, 0.05f * size, 2.5f, 2f, chestBody, BodyPart.RightArm, bodies);
            AddLimb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.085f * size, 0.065f * size, 7f, 4f, hipsBody, BodyPart.LeftLeg, bodies);
            AddLimb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.085f * size, 0.065f * size, 7f, 4f, hipsBody, BodyPart.RightLeg, bodies);

            return bodies.ToArray();
        }

        static void AddLimb(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end,
            float upperRadius, float lowerRadius, float upperMass, float lowerMass, Rigidbody parent, BodyPart part, List<Rigidbody> bodies)
        {
            Transform upperBone = animator.GetBoneTransform(upper);
            Transform lowerBone = animator.GetBoneTransform(lower);
            Transform endBone = animator.GetBoneTransform(end);

            Rigidbody upperBody = AddBody(upperBone, upperMass, HitZoneType.Limb, part, bodies);
            AddCapsule(upperBone, lowerBone, upperRadius);
            AddJoint(upperBody, parent, 60f, 30f);

            Rigidbody lowerBody = AddBody(lowerBone, lowerMass, HitZoneType.Limb, part, bodies);
            AddCapsule(lowerBone, endBone, lowerRadius);
            AddJoint(lowerBody, upperBody, 50f, 10f);
        }

        static Rigidbody AddBody(Transform bone, float mass, HitZoneType zone, BodyPart part, List<Rigidbody> bodies)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = false;
            var hitZone = bone.gameObject.AddComponent<HitZone>();
            hitZone.zone = zone;
            hitZone.part = part;
            bodies.Add(body);
            return body;
        }

        static void AddSphere(Transform bone, Vector3 worldCenter, float radius)
        {
            var sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.center = bone.InverseTransformPoint(worldCenter);
            sphere.radius = radius / bone.lossyScale.x;
        }

        /// <summary>A capsule running from this bone to the next one, whichever way the bone's axes happen to point.</summary>
        static void AddCapsule(Transform bone, Transform next, float radius)
        {
            Vector3 local = bone.InverseTransformPoint(next.position);
            int axis = 0;
            if (Mathf.Abs(local.y) > Mathf.Abs(local[axis])) axis = 1;
            if (Mathf.Abs(local.z) > Mathf.Abs(local[axis])) axis = 2;

            float scale = bone.lossyScale.x;
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = local * 0.5f;
            capsule.radius = radius / scale;
            capsule.height = local.magnitude + capsule.radius;
        }

        /// <summary>A capsule whose rounded ends sit exactly on two points (it does not stick out past them).</summary>
        static void AddCapsuleBetween(Transform bone, Vector3 worldFrom, Vector3 worldTo, float radius)
        {
            Vector3 from = bone.InverseTransformPoint(worldFrom);
            Vector3 to = bone.InverseTransformPoint(worldTo);
            Vector3 along = to - from;
            int axis = 0;
            if (Mathf.Abs(along.y) > Mathf.Abs(along[axis])) axis = 1;
            if (Mathf.Abs(along.z) > Mathf.Abs(along[axis])) axis = 2;

            float scale = bone.lossyScale.x;
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = (from + to) * 0.5f;
            capsule.radius = radius / scale;
            capsule.height = Mathf.Max(along.magnitude, capsule.radius * 2f);
        }

        static void AddJoint(Rigidbody body, Rigidbody parent, float swing, float twist)
        {
            var joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = true;
            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
        }

        // ---------------------------------------------------------------- Weapon

        /// <summary>
        /// The visible weapon, parented to the right hand so it can never drift out of the grip.
        /// Per the model's notes: with the body in T-pose the grip sits at the centre of the right fist with no
        /// rotation, which leaves the blade pointing forward and its edge facing outward.
        /// </summary>
        static Transform BuildWeapon(Animator animator, Config config)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            var socket = new GameObject("Weapon Socket").transform;
            socket.SetPositionAndRotation(hand.position + new Vector3(0.055f, -0.007f, 0.002f) * config.size, Quaternion.identity);
            socket.SetParent(hand, true);

            var weapon = new GameObject("Enemy Weapon").transform;
            weapon.SetParent(socket, false);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(config.weaponModelPath);
            if (model != null)
            {
                // The model keeps its own built-in rotation; only its size is changed.
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, weapon);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale *= config.weaponScale;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            }
            return weapon;
        }

        /// <summary>
        /// The invisible striking surface that follows the weapon through the physics engine.
        /// It is thicker than the visible blade so blocks register reliably.
        /// </summary>
        static GameObject BuildWeaponHitbox(GameObject root, Transform weapon, Config config)
        {
            var hitbox = new GameObject("Enemy Weapon Hitbox");
            hitbox.transform.SetParent(root.transform, false);
            hitbox.transform.SetPositionAndRotation(weapon.position, weapon.rotation);

            var blade = hitbox.AddComponent<BoxCollider>();
            blade.center = config.hitboxCentre;
            blade.size = config.hitboxSize;

            var body = hitbox.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            hitbox.AddComponent<MeleeAttackHitbox>();
            hitbox.AddComponent<KinematicFollower>().target = weapon;
            return hitbox;
        }
    }
}
