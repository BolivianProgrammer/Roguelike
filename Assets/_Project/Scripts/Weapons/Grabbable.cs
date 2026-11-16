using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Player;

namespace Roguelike.Weapons
{
    /// <summary>
    /// Anything a <see cref="PhysicsHandGrabber"/> can pick up. The grip point is where the first hand holds it;
    /// a second hand can grab anywhere else along it when two-handed use is allowed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        static readonly List<Grabbable> all = new List<Grabbable>();
        /// <summary>Every enabled grabbable in the scene, for distance-grab targeting.</summary>
        public static IReadOnlyList<Grabbable> All => all;
        /// <summary>Raised when an item is let go by its last hand.</summary>
        public static event Action<Grabbable, PhysicsHandGrabber> Dropped;

        [Tooltip("Where the hand holds this item. Its forward (blue) axis runs along the fist. Defaults to this transform.")]
        [SerializeField] Transform gripPoint;
        [Tooltip("Lets a second hand grab the item anywhere along it for more control and less weight per hand.")]
        [SerializeField] bool allowTwoHanded = true;
        [Tooltip("Lets the player pull this item into the hand from a distance by pointing at it.")]
        [SerializeField] bool allowDistanceGrab = true;

        readonly List<PhysicsHandGrabber> holders = new List<PhysicsHandGrabber>(2);

        public Rigidbody Body { get; private set; }
        public Collider[] Colliders { get; private set; }
        public Transform GripPoint => gripPoint ? gripPoint : transform;
        public bool AllowTwoHanded => allowTwoHanded;
        public bool AllowDistanceGrab => allowDistanceGrab;

        /// <summary>The first hand that grabbed the item (the one on the grip point), or null.</summary>
        public PhysicsHandGrabber Holder => holders.Count > 0 ? holders[0] : null;
        public IReadOnlyList<PhysicsHandGrabber> Holders => holders;
        public bool IsHeld => holders.Count > 0;
        /// <summary>The hand currently pulling this item through the air, if any.</summary>
        public PhysicsHandGrabber PulledBy { get; set; }
        /// <summary>The holster this item is stored in, if any.</summary>
        public Holster Holster { get; set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.solverIterations = 20;
            Body.solverVelocityIterations = 10;
            // Unity caps spin at 7 rad/s by default; a wrist flick with a dagger goes far beyond that, and a capped
            // item lags and stutters behind the hand (which allows 50). Match the hand.
            Body.maxAngularVelocity = 50f;
            Colliders = GetComponentsInChildren<Collider>();
        }

        Vector3 preStepVelocity;
        Vector3 preStepAngularVelocity;
        Vector3 preStepCenterOfMass;

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            preStepVelocity = Body.velocity;
            preStepAngularVelocity = Body.angularVelocity;
            preStepCenterOfMass = Body.worldCenterOfMass;
            RotationBeforeImpact = Body.rotation;
        }

        /// <summary>Which way this item was facing just before the current physics step knocked it about.</summary>
        public Quaternion RotationBeforeImpact { get; private set; } = Quaternion.identity;

        /// <summary>
        /// How fast a point on this item was moving just before the current physics step.
        /// Inside collision callbacks the solver has already slowed the item down, so use this for swing speed.
        /// </summary>
        public Vector3 VelocityBeforeImpact(Vector3 point)
        {
            return preStepVelocity + Vector3.Cross(preStepAngularVelocity, point - preStepCenterOfMass);
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// <summary>Moves this item so that its grip point lines up with the given anchor.</summary>
        public void AlignTo(Transform anchor)
        {
            Transform grip = GripPoint;
            Quaternion gripLocalRotation = Quaternion.Inverse(transform.rotation) * grip.rotation;
            Vector3 gripLocalOffset = Quaternion.Inverse(transform.rotation) * (grip.position - transform.position);

            Quaternion rotation = anchor.rotation * Quaternion.Inverse(gripLocalRotation);
            Vector3 position = anchor.position - rotation * gripLocalOffset;

            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            if (!Body.isKinematic)
            {
                Body.velocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>The point on this item's surface closest to the given world position.</summary>
        public Vector3 ClosestPoint(Vector3 position)
        {
            Vector3 best = transform.position;
            float bestDistance = float.MaxValue;
            foreach (var c in Colliders)
            {
                if (c == null || !c.enabled) continue;
                Vector3 point = c.ClosestPoint(position);
                float distance = (point - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }
            return best;
        }

        /// <summary>Takes the item out of its holster so a hand can hold it.</summary>
        internal void PrepareForGrab()
        {
            if (Holster != null) Holster.Eject();
        }

        internal void NotifyGrabbed(PhysicsHandGrabber grabber)
        {
            holders.Add(grabber);
            RefreshHolders();
        }

        internal void NotifyReleased(PhysicsHandGrabber grabber)
        {
            holders.Remove(grabber);
            RefreshHolders();
            if (holders.Count == 0) Dropped?.Invoke(this, grabber);
        }

        void RefreshHolders()
        {
            for (int i = 0; i < holders.Count; i++)
                holders[i].OnHoldersChanged();
        }
    }
}
