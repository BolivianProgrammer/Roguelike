using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;

namespace Roguelike.Enemies
{
    /// <summary>
    /// Lets an enemy be cut apart. Every body part soaks up damage where it is hit; once a part has taken
    /// enough, it comes off at the bone that was struck: the limb vanishes from the body, a loose copy of it
    /// flies off as a physics object, and the stump bleeds.
    ///
    /// How the cut works: shrinking a bone to nothing collapses that part of the mesh into the joint, which also
    /// closes the wound. The loose piece is a second copy of the model with everything except that limb shrunk away.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyDismemberment : MonoBehaviour
    {
        const float Collapsed = 0.0001f;
        const int MaxLoosePieces = 8;

        static readonly Queue<GameObject> loosePieces = new Queue<GameObject>();

        [Tooltip("The plain character model, used to make the loose limb pieces.")]
        [SerializeField] GameObject limbModel;
        [Tooltip("The root bone of the skeleton (hips).")]
        [SerializeField] Transform skeletonRoot;

        [Header("Damage a part takes before it comes off")]
        [SerializeField] float headHealth = 40f;
        [SerializeField] float armHealth = 30f;
        [SerializeField] float legHealth = 45f;
        [Tooltip("Slash damage the torso takes before the body is cut in two. Only slashes count: hammers and stabs cannot do it.")]
        [SerializeField] float torsoHealth = 75f;
        [Tooltip("Parts of a dead body come off this much more easily.")]
        [SerializeField, Range(0.1f, 1f)] float corpseFactor = 0.5f;
        [Tooltip("Hits weaker than this do not count towards cutting a part off.")]
        [SerializeField] float minimumHit = 8f;

        [Header("How well each hit type cuts")]
        [SerializeField] float slashFactor = 1f;
        [SerializeField] float bluntFactor = 0.6f;
        [SerializeField] float stabFactor = 0.2f;

        [Header("Aftermath")]
        [Tooltip("Health lost per second after losing an arm.")]
        [SerializeField] float bleedPerSecond = 12f;
        [SerializeField] float pieceLifetime = 10f;

        readonly Dictionary<HitZone, float> wounds = new Dictionary<HitZone, float>();
        readonly List<Transform> severedBones = new List<Transform>();

        /// <summary>
        /// Wound caps: flesh-and-bone lids the model supplies for every cut, named Cap_*. For each bone that
        /// can come off there is one for the stump left on the body and one for the end of the removed part.
        /// </summary>
        static readonly (HumanBodyBones bone, string stump, string removed)[] CapTable =
        {
            (HumanBodyBones.Head, "Cap_Neck_Body", "Cap_Neck_Head"),
            (HumanBodyBones.Spine, "Cap_Waist_Lower", "Cap_Waist_Upper"),
            (HumanBodyBones.Chest, "Cap_Chest_Lower", "Cap_Chest_Upper"),
            (HumanBodyBones.LeftUpperArm, "Cap_Shoulder_L_Body", "Cap_Shoulder_L_Arm"),
            (HumanBodyBones.RightUpperArm, "Cap_Shoulder_R_Body", "Cap_Shoulder_R_Arm"),
            (HumanBodyBones.LeftLowerArm, "Cap_Elbow_L_UpperArm", "Cap_Elbow_L_Forearm"),
            (HumanBodyBones.RightLowerArm, "Cap_Elbow_R_UpperArm", "Cap_Elbow_R_Forearm"),
            (HumanBodyBones.LeftUpperLeg, "Cap_Hip_L_Body", "Cap_Hip_L_Leg"),
            (HumanBodyBones.RightUpperLeg, "Cap_Hip_R_Body", "Cap_Hip_R_Leg"),
            (HumanBodyBones.LeftLowerLeg, "Cap_Knee_L_Thigh", "Cap_Knee_L_Shin"),
            (HumanBodyBones.RightLowerLeg, "Cap_Knee_R_Thigh", "Cap_Knee_R_Shin"),
        };

        readonly Dictionary<string, Renderer> caps = new Dictionary<string, Renderer>();
        readonly Dictionary<Transform, (string stump, string removed)> capsByBone = new Dictionary<Transform, (string, string)>();

        EnemyHealth health;
        EnemyBrain brain;
        bool bleeding;

        /// <summary>True once any part of this body has been cut off.</summary>
        public bool HasSevered => severedBones.Count > 0;
        float torsoWound;
        Transform waistBone;
        Transform chestBone;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            brain = GetComponent<EnemyBrain>();

            // The torso can be cut at two heights: the waist, and under the ribs.
            var animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                waistBone = animator.GetBoneTransform(HumanBodyBones.Spine);
                chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chestBone == null) chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);

                foreach (var entry in CapTable)
                {
                    Transform bone = animator.GetBoneTransform(entry.bone);
                    if (bone != null) capsByBone[bone] = (entry.stump, entry.removed);
                }
            }

            // Wound caps stay hidden until their cut happens.
            foreach (var capRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!capRenderer.name.StartsWith("Cap_")) continue;
                caps[capRenderer.name] = capRenderer;
                capRenderer.enabled = false;
            }
        }

        void OnEnable() => health.Struck += OnStruck;
        void OnDisable() => health.Struck -= OnStruck;

        void LateUpdate()
        {
            // The animation must not grow a severed limb back.
            for (int i = 0; i < severedBones.Count; i++)
                if (severedBones[i] != null) severedBones[i].localScale = Vector3.one * Collapsed;
            foreach (var pair in pinnedScales)
                if (pair.Key != null) pair.Key.localScale = pair.Value;

            if (bleeding && !WorldFreeze.Active) health.Bleed(bleedPerSecond * Time.deltaTime);
        }

        void OnStruck(DamageInfo info)
        {
            if (info.HitCollider == null) return;
            var zone = info.HitCollider.GetComponent<HitZone>();
            if (zone == null) return;

            if (zone.part == BodyPart.Torso)
            {
                StrikeTorso(info);
                return;
            }

            // A finisher to the head always takes it off.
            if (info.Execution && zone.part == BodyPart.Head)
            {
                Sever(zone.transform, zone.part, info);
                return;
            }

            if (info.Amount < minimumHit) return;

            float factor = info.Type == DamageType.Slash ? slashFactor : info.Type == DamageType.Blunt ? bluntFactor : stabFactor;
            wounds.TryGetValue(zone, out float wound);
            wound += info.Amount * factor;
            wounds[zone] = wound;

            float limit = zone.part == BodyPart.Head ? headHealth
                : zone.part == BodyPart.LeftArm || zone.part == BodyPart.RightArm ? armHealth
                : legHealth;
            if (health.IsDead) limit *= corpseFactor;

            CombatLog.Log(name + " wound " + CombatLog.Where(info.HitCollider) + " +" + (info.Amount * factor).ToString("F1") + " (" + info.Type + ") = "
                + wound.ToString("F1") + " / " + limit.ToString("F1") + (health.IsDead ? " (corpse)" : ""));
            if (wound >= limit) Sever(zone.transform, zone.part, info);
        }

        /// <summary>
        /// Slashes across the body add up until it is cut in two, at the waist or under the ribs depending on
        /// how high the deciding blow landed.
        /// </summary>
        void StrikeTorso(DamageInfo info)
        {
            if (waistBone == null || info.Type != DamageType.Slash || info.Amount < minimumHit) return;
            if (severedBones.Contains(waistBone) || (chestBone != null && severedBones.Contains(chestBone))) return;

            torsoWound += info.Amount;
            float limit = health.IsDead ? torsoHealth * corpseFactor : torsoHealth;
            CombatLog.Log(name + " torso wound +" + info.Amount.ToString("F1") + " = " + torsoWound.ToString("F1") + " / " + limit.ToString("F1")
                + " at height " + info.Point.y.ToString("F2") + " (chest bone " + (chestBone != null ? chestBone.position.y.ToString("F2") : "-") + ")");
            if (torsoWound < limit) return;

            bool high = chestBone != null && chestBone != waistBone && info.Point.y >= chestBone.position.y;
            Sever(high ? chestBone : waistBone, BodyPart.Torso, info);
        }

        void Sever(Transform bone, BodyPart part, DamageInfo info)
        {
            if (severedBones.Contains(bone)) return;
            bool torso = part == BodyPart.Torso;
            EnemyHealth.LastHit += torso ? "  ->  CUT IN HALF" : "  ->  SEVERED";

            // Cut in half while alive: the upper body keeps going, and the legs are what falls away.
            if (torso && !health.IsDead && brain != null && !brain.IsOnTheGround)
            {
                BisectAlive(bone, info);
                return;
            }

            // Losing the weapon arm means losing the weapon.
            if (brain != null && brain.HoldsWeaponUnder(bone)) brain.Disarm();

            // A crushed head just bursts; anything else leaves a piece behind.
            bool crushed = part == BodyPart.Head && info.Type == DamageType.Blunt;
            if (!crushed) SpawnPiece(bone, info);

            Vector3 stump = bone.position;
            Vector3 outward = bone.parent != null ? (bone.position - bone.parent.position).normalized : Vector3.up;

            // Close the wound on the body with its cap.
            string capShown = "none";
            if (capsByBone.TryGetValue(bone, out var capNames) && caps.TryGetValue(capNames.stump, out Renderer stumpCap))
            {
                stumpCap.enabled = true;
                capShown = stumpCap.name;
            }
            CombatLog.Log(name + " SEVERED " + part + " at bone " + bone.name + " (struck " + CombatLog.Where(info.HitCollider) + ", " + info.Type
                + ") stump cap: " + capShown + (crushed ? ", head crushed" : ""));

            RemovePhysics(bone);
            if (torso) TrimTorsoCollider(bone);
            severedBones.Add(bone);
            bone.localScale = Vector3.one * Collapsed;

            BloodFx.Burst(stump, (outward + info.Direction * 0.5f).normalized, crushed || torso ? 110 : 45);
            if (bone.parent != null) BloodFx.Stream(bone.parent, stump, outward);

            if (health.IsDead) return;

            // Nobody fights on without a head. Without an arm they fight on and bleed out; without a leg they drop
            // and crawl, bleeding.
            if (part == BodyPart.LeftArm || part == BodyPart.RightArm) bleeding = true;
            else if ((part == BodyPart.LeftLeg || part == BodyPart.RightLeg) && brain != null)
            {
                bleeding = true;
                brain.StartCrawling();
            }
            else health.Kill(info);
        }

        /// <summary>True if this bone, or one it hangs from, has been cut off.</summary>
        public bool IsSevered(Transform bone)
        {
            for (Transform t = bone; t != null && t != transform; t = t.parent)
                if (severedBones.Contains(t)) return true;
            return false;
        }

        // ---------------------------------------------------------------- Cut in half, still alive

        const float Shrunk = 0.01f;
        readonly Dictionary<Transform, Vector3> pinnedScales = new Dictionary<Transform, Vector3>();

        /// <summary>
        /// The enemy is cut at the waist or under the ribs and lives on as an upper body.
        /// On the body: the legs collapse into the hips, the hips (and the belly, for a cut under the ribs) shrink to
        /// almost nothing, and the cut bone is scaled back up so the upper body keeps its size. Its open end gets the
        /// "upper" wound cap. The lower half, with the legs, falls away as a loose ragdoll.
        /// </summary>
        void BisectAlive(Transform cutBone, DamageInfo info)
        {
            EnemyHealth.LastHit += "  ->  CUT IN HALF (upper body lives)";
            Vector3 stump = cutBone.position;

            SpawnLowerHalf(cutBone, info);

            var animator = GetComponent<Animator>();
            foreach (var legBone in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg })
            {
                Transform leg = animator.GetBoneTransform(legBone);
                if (leg == null || severedBones.Contains(leg)) continue;
                RemovePhysics(leg);
                severedBones.Add(leg);
                leg.localScale = Vector3.one * Collapsed;
            }

            // Shrink everything between the hips and the cut, then undo that shrink at the cut bone.
            float scale = 1f;
            for (Transform t = cutBone.parent; t != null && t != transform; t = t.parent)
            {
                if (t == skeletonRoot)
                {
                    pinnedScales[t] = Vector3.one * Shrunk;
                    scale *= Shrunk;
                    RemoveOwnPhysics(t);
                    break;
                }
                RemoveOwnPhysics(t);
            }
            pinnedScales[cutBone] = Vector3.one / scale;
            foreach (var pair in pinnedScales) pair.Key.localScale = pair.Value;

            // The upper body needs its own torso shape: after a cut under the ribs nothing covered the chest.
            if (cutBone.GetComponent<Collider>() == null)
            {
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                var capsule = cutBone.gameObject.AddComponent<CapsuleCollider>();
                Vector3 top = neck != null ? cutBone.InverseTransformPoint(neck.position) : Vector3.up * 0.3f;
                int axis = Mathf.Abs(top.x) > Mathf.Abs(top.y) ? (Mathf.Abs(top.x) > Mathf.Abs(top.z) ? 0 : 2) : (Mathf.Abs(top.y) > Mathf.Abs(top.z) ? 1 : 2);
                capsule.direction = axis;
                capsule.center = top * 0.5f;
                capsule.radius = 0.15f / Mathf.Max(0.0001f, cutBone.lossyScale.x);
                capsule.height = Mathf.Max(top.magnitude, capsule.radius * 2f);
                var zone = cutBone.gameObject.AddComponent<HitZone>();
                zone.zone = HitZoneType.Torso;
                zone.part = BodyPart.Torso;
            }

            // The open end of the upper body gets its cap.
            if (capsByBone.TryGetValue(cutBone, out var capNames) && caps.TryGetValue(capNames.removed, out Renderer upperCap))
                upperCap.enabled = true;

            BloodFx.Burst(stump, (Vector3.down + info.Direction * 0.5f).normalized, 120);
            BloodFx.Stream(cutBone, stump, Vector3.down);
            CombatLog.Log(name + " CUT IN HALF at " + cutBone.name + " and lives on as an upper body (cap " + capNames.removed + ")");

            // It will not last long like this.
            bleeding = true;
            bleedPerSecond *= 1.5f;
            brain.StartBisected();
        }

        /// <summary>Strips the collider, joint, hit zone and rigidbody of one bone only, not the bones below it.</summary>
        static void RemoveOwnPhysics(Transform bone)
        {
            foreach (var joint in bone.GetComponents<Joint>()) Destroy(joint);
            foreach (var zone in bone.GetComponents<HitZone>()) Destroy(zone);
            foreach (var collider in bone.GetComponents<Collider>()) Destroy(collider);
        }

        /// <summary>The legs and hips of a bisected enemy: a loose ragdoll that falls where it stood.</summary>
        void SpawnLowerHalf(Transform cutBone, DamageInfo info)
        {
            if (limbModel == null || skeletonRoot == null) return;
            GameObject piece = CopyPose("Lower body (severed)", out Dictionary<string, Transform> copies);
            if (!copies.TryGetValue(cutBone.name, out Transform pieceCut) || !copies.TryGetValue(skeletonRoot.name, out Transform pieceHips))
            {
                Destroy(piece);
                return;
            }

            // Keep everything below the cut; the upper body collapses into the cut, which seals it.
            pieceCut.localScale = Vector3.one * Collapsed;
            capsByBone.TryGetValue(cutBone, out var pieceCaps);
            DressPiece(piece, pieceCaps.stump, pieceCaps.removed);

            var animator = GetComponent<Animator>();
            Rigidbody hipsBody = AddPieceBody(pieceHips, skeletonRoot, 12f, null);
            if (cutBone != waistBone && waistBone != null && copies.TryGetValue(waistBone.name, out Transform pieceWaist))
            {
                // Cut under the ribs: the belly stays with the legs.
                var belly = pieceWaist.gameObject.AddComponent<SphereCollider>();
                belly.radius = 0.15f / Mathf.Max(0.0001f, pieceWaist.lossyScale.x);
                belly.center = pieceWaist.InverseTransformVector(Vector3.up * 0.08f);
            }
            foreach (var chain in new[] { new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg }, new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg } })
            {
                Rigidbody parent = hipsBody;
                foreach (var boneId in chain)
                {
                    Transform original = animator.GetBoneTransform(boneId);
                    if (original == null || severedBones.Contains(original) || !copies.TryGetValue(original.name, out Transform copy)) break;
                    parent = AddPieceBody(copy, original, boneId == chain[0] ? 7f : 4f, parent);
                }
            }
            hipsBody.velocity = info.Direction * Mathf.Clamp(info.ImpactSpeed, 1f, 5f) * 0.3f;

            TrackPiece(piece);
        }

        /// <summary>Gives a bone of a severed piece the same collision shape as the original, a body, and a joint to its parent.</summary>
        static Rigidbody AddPieceBody(Transform copy, Transform original, float mass, Rigidbody parent)
        {
            foreach (var source in original.GetComponents<Collider>()) CopyCollider(source, copy.gameObject);
            var body = copy.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            if (parent != null)
            {
                var joint = copy.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.enableProjection = true;
                joint.swing1Limit = new SoftJointLimit { limit = 50f };
                joint.swing2Limit = new SoftJointLimit { limit = 50f };
            }
            return body;
        }

        static void CopyCollider(Collider source, GameObject target)
        {
            var capsule = source as CapsuleCollider;
            var sphere = source as SphereCollider;
            if (capsule != null)
            {
                var c = target.AddComponent<CapsuleCollider>();
                c.center = capsule.center;
                c.radius = capsule.radius;
                c.height = capsule.height;
                c.direction = capsule.direction;
            }
            else if (sphere != null)
            {
                var s = target.AddComponent<SphereCollider>();
                s.center = sphere.center;
                s.radius = sphere.radius;
            }
        }

        /// <summary>A second copy of the model, posed exactly like this body is right now.</summary>
        GameObject CopyPose(string pieceName, out Dictionary<string, Transform> copies)
        {
            GameObject piece = Instantiate(limbModel, transform.position, transform.rotation);
            piece.name = pieceName;
            piece.transform.localScale = transform.localScale;
            var animator = piece.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            copies = new Dictionary<string, Transform>();
            foreach (var t in piece.GetComponentsInChildren<Transform>()) copies[t.name] = t;
            foreach (var source in skeletonRoot.GetComponentsInChildren<Transform>())
            {
                if (!copies.TryGetValue(source.name, out Transform copy)) continue;
                copy.localPosition = source.localPosition;
                copy.localRotation = source.localRotation;
                copy.localScale = source.localScale;
            }
            return piece;
        }

        /// <summary>
        /// The piece looks like the body it came from and shows the wounds that body already had, plus the cap that
        /// closes its own cut end (and never the cap that closes the body's side of that cut).
        /// </summary>
        void DressPiece(GameObject piece, string ownCap, string otherCap)
        {
            foreach (var pieceRenderer in piece.GetComponentsInChildren<Renderer>(true))
            {
                Transform original = FindByName(transform, pieceRenderer.name);
                var originalRenderer = original != null ? original.GetComponent<Renderer>() : null;
                if (originalRenderer != null) pieceRenderer.sharedMaterials = originalRenderer.sharedMaterials;

                if (pieceRenderer.name.StartsWith("Cap_"))
                    pieceRenderer.enabled = pieceRenderer.name == ownCap
                        || (originalRenderer != null && originalRenderer.enabled && pieceRenderer.name != otherCap);

                var skin = pieceRenderer as SkinnedMeshRenderer;
                if (skin != null) skin.updateWhenOffscreen = true;
            }
        }

        /// <summary>Keeps the floor from filling up with body parts.</summary>
        void TrackPiece(GameObject piece)
        {
            loosePieces.Enqueue(piece);
            while (loosePieces.Count > MaxLoosePieces)
            {
                GameObject oldest = loosePieces.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
            Destroy(piece, pieceLifetime);
        }

        /// <summary>
        /// After a cut under the ribs, the belly keeps its collision shape, which used to reach up to the neck.
        /// Shorten it so nothing invisible is left standing above the cut.
        /// </summary>
        void TrimTorsoCollider(Transform cutBone)
        {
            if (waistBone == null || cutBone == waistBone) return;
            var capsule = waistBone.GetComponent<CapsuleCollider>();
            if (capsule == null) return;

            Vector3 top = waistBone.InverseTransformPoint(cutBone.position);
            capsule.center = top * 0.5f;
            capsule.height = Mathf.Max(top.magnitude, capsule.radius * 2f);
        }

        static Transform FindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// Takes a bone and everything below it out of the physics. Cuts happen inside collision callbacks, where
        /// components may only be destroyed at the end of the frame (Destroy, never DestroyImmediate). A rigidbody
        /// cannot be destroyed while a joint still points at it, so the bodies are switched off and dropped from the
        /// ragdoll instead of destroyed: they cost nothing and the enemy is destroyed later anyway.
        /// </summary>
        void RemovePhysics(Transform bone)
        {
            var ragdoll = GetComponent<EnemyRagdoll>();
            foreach (var joint in bone.GetComponentsInChildren<Joint>()) Destroy(joint);
            foreach (var zone in bone.GetComponentsInChildren<HitZone>()) Destroy(zone);
            foreach (var collider in bone.GetComponentsInChildren<Collider>()) Destroy(collider);
            foreach (var body in bone.GetComponentsInChildren<Rigidbody>())
            {
                body.isKinematic = true;
                body.detectCollisions = false;
                if (ragdoll != null) ragdoll.Forget(body);
            }
        }

        void SpawnPiece(Transform bone, DamageInfo info)
        {
            if (limbModel == null || skeletonRoot == null) return;

            GameObject piece = CopyPose(bone.name + " (severed)", out Dictionary<string, Transform> copies);
            if (!copies.TryGetValue(bone.name, out Transform pieceBone) || !copies.TryGetValue(skeletonRoot.name, out Transform pieceHips))
            {
                Destroy(piece);
                return;
            }

            // Keep the limb, and shrink the rest of the body into the cut so it also seals the open end.
            pieceBone.SetParent(piece.transform, true);
            pieceHips.position = pieceBone.position;
            pieceHips.localScale = Vector3.one * Collapsed;

            capsByBone.TryGetValue(bone, out var pieceCaps);
            DressPiece(piece, pieceCaps.removed, pieceCaps.stump);

            // Same collision shapes as the living limb had.
            foreach (var source in bone.GetComponentsInChildren<Collider>())
            {
                if (!copies.TryGetValue(source.name, out Transform target) || !target.IsChildOf(pieceBone)) continue;
                CopyCollider(source, target.gameObject);
            }

            // A piece cut off above the waist has no torso shape of its own to copy, so give it one.
            if (bone.GetComponent<Collider>() == null && bone.GetComponentInParent<HitZone>() != null && bone != waistBone && bone == chestBone)
            {
                var chest = pieceBone.gameObject.AddComponent<SphereCollider>();
                chest.radius = 0.17f / Mathf.Max(0.0001f, pieceBone.lossyScale.x);
                chest.center = pieceBone.InverseTransformVector(Vector3.up * 0.1f);
            }

            var body = piece.AddComponent<Rigidbody>();
            body.mass = bone == waistBone || bone == chestBone ? 25f : 3f;
            body.velocity = info.Direction * Mathf.Clamp(info.ImpactSpeed, 1f, 6f) * 0.5f + Vector3.up * 1.5f;
            body.angularVelocity = Random.insideUnitSphere * 6f;

            BloodFx.Stream(piece.transform, pieceBone.position, -info.Direction, 1.5f);
            TrackPiece(piece);
        }
    }
}
