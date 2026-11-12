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

        EnemyHealth health;
        EnemyBrain brain;
        bool bleeding;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            brain = GetComponent<EnemyBrain>();
        }

        void OnEnable() => health.Struck += OnStruck;
        void OnDisable() => health.Struck -= OnStruck;

        void LateUpdate()
        {
            // The animation must not grow a severed limb back.
            for (int i = 0; i < severedBones.Count; i++)
                if (severedBones[i] != null) severedBones[i].localScale = Vector3.one * Collapsed;

            if (bleeding) health.Bleed(bleedPerSecond * Time.deltaTime);
        }

        void OnStruck(DamageInfo info)
        {
            if (info.HitCollider == null) return;
            var zone = info.HitCollider.GetComponent<HitZone>();
            if (zone == null || zone.part == BodyPart.Torso) return;

            // A finisher to the head always takes it off.
            if (info.Execution && zone.part == BodyPart.Head)
            {
                Sever(zone, info);
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

            if (wound >= limit) Sever(zone, info);
        }

        void Sever(HitZone zone, DamageInfo info)
        {
            Transform bone = zone.transform;
            if (severedBones.Contains(bone)) return;
            EnemyHealth.LastHit += "  ->  SEVERED";

            // Losing the weapon arm means losing the weapon.
            if (brain != null && brain.HoldsWeaponUnder(bone)) brain.Disarm();

            // A crushed head just bursts; anything else leaves a piece behind.
            bool crushed = zone.part == BodyPart.Head && info.Type == DamageType.Blunt;
            if (!crushed) SpawnPiece(bone, info);

            Vector3 stump = bone.position;
            Vector3 outward = bone.parent != null ? (bone.position - bone.parent.position).normalized : Vector3.up;

            RemovePhysics(bone);
            severedBones.Add(bone);
            bone.localScale = Vector3.one * Collapsed;

            BloodFx.Burst(stump, (outward + info.Direction * 0.5f).normalized, crushed ? 90 : 45);
            if (bone.parent != null) BloodFx.Stream(bone.parent, stump, outward);

            if (health.IsDead) return;

            // Nobody fights on without a head or a leg. Without an arm they do, but they bleed out.
            if (zone.part == BodyPart.LeftArm || zone.part == BodyPart.RightArm) bleeding = true;
            else health.Kill(info);
        }

        /// <summary>Strips the colliders, joints and rigidbodies from a bone and everything below it.</summary>
        static void RemovePhysics(Transform bone)
        {
            // Joints first: a rigidbody cannot be removed while a joint still depends on it.
            foreach (var joint in bone.GetComponentsInChildren<Joint>()) DestroyImmediate(joint);
            foreach (var zone in bone.GetComponentsInChildren<HitZone>()) Destroy(zone);
            foreach (var collider in bone.GetComponentsInChildren<Collider>()) Destroy(collider);
            foreach (var body in bone.GetComponentsInChildren<Rigidbody>()) Destroy(body);
        }

        void SpawnPiece(Transform bone, DamageInfo info)
        {
            if (limbModel == null || skeletonRoot == null) return;

            GameObject piece = Instantiate(limbModel, transform.position, transform.rotation);
            piece.name = bone.name + " (severed)";
            var animator = piece.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            // Put the copy in exactly the pose the enemy is in right now.
            var copies = new Dictionary<string, Transform>();
            foreach (var t in piece.GetComponentsInChildren<Transform>()) copies[t.name] = t;
            foreach (var source in skeletonRoot.GetComponentsInChildren<Transform>())
            {
                if (!copies.TryGetValue(source.name, out Transform copy)) continue;
                copy.localPosition = source.localPosition;
                copy.localRotation = source.localRotation;
                copy.localScale = source.localScale;
            }

            if (!copies.TryGetValue(bone.name, out Transform pieceBone) || !copies.TryGetValue(skeletonRoot.name, out Transform pieceHips))
            {
                Destroy(piece);
                return;
            }

            // Keep the limb, and shrink the rest of the body into the cut so it also seals the open end.
            pieceBone.SetParent(piece.transform, true);
            pieceHips.position = pieceBone.position;
            pieceHips.localScale = Vector3.one * Collapsed;

            foreach (var skin in piece.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.updateWhenOffscreen = true;

            // Same collision shapes as the living limb had.
            foreach (var source in bone.GetComponentsInChildren<Collider>())
            {
                if (!copies.TryGetValue(source.name, out Transform target) || !target.IsChildOf(pieceBone)) continue;
                var capsule = source as CapsuleCollider;
                var sphere = source as SphereCollider;
                if (capsule != null)
                {
                    var c = target.gameObject.AddComponent<CapsuleCollider>();
                    c.center = capsule.center;
                    c.radius = capsule.radius;
                    c.height = capsule.height;
                    c.direction = capsule.direction;
                }
                else if (sphere != null)
                {
                    var s = target.gameObject.AddComponent<SphereCollider>();
                    s.center = sphere.center;
                    s.radius = sphere.radius;
                }
            }

            var body = piece.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.velocity = info.Direction * Mathf.Clamp(info.ImpactSpeed, 1f, 6f) * 0.5f + Vector3.up * 1.5f;
            body.angularVelocity = Random.insideUnitSphere * 6f;

            BloodFx.Stream(piece.transform, pieceBone.position, -info.Direction, 1.5f);

            // Keep the floor from filling up with body parts.
            loosePieces.Enqueue(piece);
            while (loosePieces.Count > MaxLoosePieces)
            {
                GameObject oldest = loosePieces.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
            Destroy(piece, pieceLifetime);
        }
    }
}
