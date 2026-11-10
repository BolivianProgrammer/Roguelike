using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Enemies;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Assembles the Grunt enemy prefab from the imported Mixamo character: Animator controller, body colliders
    /// with hit zones, ragdoll joints, a sword in the right hand and the AI components.
    /// Run it again from the menu (Roguelike > Build Grunt Prefab) after swapping the model or clips.
    /// Because the character is a Humanoid, any other humanoid model can be dropped in the same way.
    /// </summary>
    public static class GruntBuilder
    {
        const string ArtFolder = "Assets/_Project/Art/Enemies/Grunt/";
        const string ModelPath = ArtFolder + "Grunt.fbx";
        const string ControllerPath = ArtFolder + "Grunt.controller";
        const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";
        const string PrefabPath = PrefabFolder + "/Grunt.prefab";

        [MenuItem("Roguelike/Build Grunt Prefab")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[GruntBuilder] Missing model at " + ModelPath);
                return;
            }

            AnimatorController controller = BuildController();

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "Grunt";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // Bones carry the colliders the player hits, so they must keep animating off-screen.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Rigidbody[] bodies = BuildBody(animator);
            GameObject weapon = BuildWeapon(root, animator);
            GameObject weaponHitbox = BuildWeaponHitbox(root, weapon.transform);

            var health = root.AddComponent<EnemyHealth>();
            var ragdoll = root.AddComponent<EnemyRagdoll>();
            var brain = root.AddComponent<EnemyBrain>();

            var ragdollData = new SerializedObject(ragdoll);
            ragdollData.FindProperty("animator").objectReferenceValue = animator;
            var bodyList = ragdollData.FindProperty("bodies");
            bodyList.arraySize = bodies.Length;
            for (int i = 0; i < bodies.Length; i++)
                bodyList.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            ragdollData.ApplyModifiedPropertiesWithoutUndo();

            var brainData = new SerializedObject(brain);
            brainData.FindProperty("animator").objectReferenceValue = animator;
            brainData.FindProperty("ragdoll").objectReferenceValue = ragdoll;
            brainData.FindProperty("weaponHitbox").objectReferenceValue = weaponHitbox.GetComponent<MeleeAttackHitbox>();
            brainData.FindProperty("weaponVisual").objectReferenceValue = weapon.transform;
            var attacks = brainData.FindProperty("attacks");
            attacks.arraySize = 2;
            // Timings measured from the clips: the weapon hand is fastest at 0.60 s and 0.54 s.
            SetAttack(attacks.GetArrayElementAtIndex(0), "SlashA", 1.5f, 0.48f, 0.82f, 15f);
            SetAttack(attacks.GetArrayElementAtIndex(1), "GreatSlash", 1.27f, 0.42f, 0.82f, 25f);
            brainData.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Enemies");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log("[GruntBuilder] Built " + PrefabPath + " with " + bodies.Length + " body parts.");
        }

        static void SetAttack(SerializedProperty attack, string state, float duration, float hitStart, float hitEnd, float damage)
        {
            attack.FindPropertyRelative("stateName").stringValue = state;
            attack.FindPropertyRelative("duration").floatValue = duration;
            attack.FindPropertyRelative("hitStart").floatValue = hitStart;
            attack.FindPropertyRelative("hitEnd").floatValue = hitEnd;
            attack.FindPropertyRelative("damage").floatValue = damage;
            attack.FindPropertyRelative("damageType").enumValueIndex = (int)DamageType.Slash;
        }

        // ---------------------------------------------------------------- Animator

        static AnimationClip LoadClip(string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ArtFolder + "Grunt@" + name + ".fbx"))
            {
                var clip = asset as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview")) return clip;
            }
            Debug.LogWarning("[GruntBuilder] Missing clip " + name);
            return null;
        }

        static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;

            // Idle, walk and run blend by how fast the enemy is moving (m/s).
            BlendTree tree;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(LoadClip("Idle"), 0f);
            tree.AddChild(LoadClip("Walk"), 1.4f);
            tree.AddChild(LoadClip("Run"), 3.5f);
            machine.defaultState = locomotion;

            // Everything else is started from code (EnemyBrain), so no transitions are needed.
            foreach (var name in new[] { "SlashA", "GreatSlash", "HitSideA", "HitSideB", "HitFront", "Stagger" })
            {
                AnimatorState state = machine.AddState(name);
                state.motion = LoadClip(name);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ---------------------------------------------------------------- Body colliders and ragdoll

        static Rigidbody[] BuildBody(Animator animator)
        {
            var bodies = new List<Rigidbody>();

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (neck == null) neck = head;

            Rigidbody hipsBody = AddBody(hips, 12f, HitZoneType.Torso, bodies);
            AddSphere(hips, hips.position, 0.16f);

            Rigidbody chestBody = AddBody(spine, 15f, HitZoneType.Torso, bodies);
            AddCapsule(spine, neck, 0.16f);
            AddJoint(chestBody, hipsBody, 25f, 20f);

            Rigidbody headBody = AddBody(head, 5f, HitZoneType.Head, bodies);
            AddSphere(head, head.position + Vector3.up * 0.09f, 0.11f);
            AddJoint(headBody, chestBody, 35f, 25f);

            AddLimb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.055f, 0.045f, 2.5f, 2f, chestBody, bodies);
            AddLimb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.055f, 0.045f, 2.5f, 2f, chestBody, bodies);
            AddLimb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.085f, 0.06f, 7f, 4f, hipsBody, bodies);
            AddLimb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.085f, 0.06f, 7f, 4f, hipsBody, bodies);

            return bodies.ToArray();
        }

        static void AddLimb(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end,
            float upperRadius, float lowerRadius, float upperMass, float lowerMass, Rigidbody parent, List<Rigidbody> bodies)
        {
            Transform upperBone = animator.GetBoneTransform(upper);
            Transform lowerBone = animator.GetBoneTransform(lower);
            Transform endBone = animator.GetBoneTransform(end);

            Rigidbody upperBody = AddBody(upperBone, upperMass, HitZoneType.Limb, bodies);
            AddCapsule(upperBone, lowerBone, upperRadius);
            AddJoint(upperBody, parent, 60f, 30f);

            Rigidbody lowerBody = AddBody(lowerBone, lowerMass, HitZoneType.Limb, bodies);
            AddCapsule(lowerBone, endBone, lowerRadius);
            AddJoint(lowerBody, upperBody, 50f, 10f);
        }

        static Rigidbody AddBody(Transform bone, float mass, HitZoneType zone, List<Rigidbody> bodies)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = false;
            bone.gameObject.AddComponent<HitZone>().zone = zone;
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

        static GameObject BuildWeapon(GameObject root, Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform knuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);

            // The model is in T-pose here: arms out to the sides, palms down. A sword gripped in that fist points forward.
            var socket = new GameObject("Weapon Socket").transform;
            Vector3 palm = knuckle != null ? Vector3.Lerp(hand.position, knuckle.position, 0.7f) : hand.position;
            socket.SetPositionAndRotation(palm + Vector3.down * 0.03f, Quaternion.LookRotation(root.transform.forward, Vector3.up));
            socket.SetParent(hand, true);

            // The visible sword is parented to the hand so it can never drift out of the grip.
            var weapon = new GameObject("Grunt Sword");
            weapon.transform.SetParent(socket, false);

            var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Steel.mat");
            var grip = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/SwordGrip.mat");
            AddPart(weapon.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.035f, 0.2f), grip, false);
            AddPart(weapon.transform, "Guard", PrimitiveType.Cube, new Vector3(0f, 0f, 0.11f), new Vector3(0.16f, 0.03f, 0.03f), steel, false);
            AddPart(weapon.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, 0f, 0.5f), new Vector3(0.05f, 0.012f, 0.75f), steel, false);
            return weapon;
        }

        /// <summary>
        /// The invisible striking surface that follows the sword through the physics engine.
        /// It is thicker than the visible blade so blocks register reliably.
        /// </summary>
        static GameObject BuildWeaponHitbox(GameObject root, Transform weapon)
        {
            var hitbox = new GameObject("Grunt Sword Hitbox");
            hitbox.transform.SetParent(root.transform, false);
            hitbox.transform.SetPositionAndRotation(weapon.position, weapon.rotation);

            var blade = hitbox.AddComponent<BoxCollider>();
            blade.center = new Vector3(0f, 0f, 0.5f);
            blade.size = new Vector3(0.07f, 0.05f, 0.8f);

            var body = hitbox.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            hitbox.AddComponent<MeleeAttackHitbox>();
            hitbox.AddComponent<KinematicFollower>().target = weapon;
            return hitbox;
        }

        static void AddPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
