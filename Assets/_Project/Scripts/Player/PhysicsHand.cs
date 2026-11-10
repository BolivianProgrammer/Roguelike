using System;
using UnityEngine;
using UnityEngine.XR;

namespace Roguelike.Player
{
    /// <summary>
    /// A rigidbody hand that chases a tracked controller pose by setting its velocity every physics step.
    /// Because it is a real physics body, walls, shields and enemies can stop it instead of letting it pass through.
    /// Holding something heavy makes it follow more sluggishly, which is what gives weapons their weight.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsHand : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] XRNode handNode = XRNode.LeftHand;

        [Header("Follow")]
        [Tooltip("Share (0-1) of the remaining gap to the controller closed each physics step, with nothing held.")]
        [SerializeField, Range(0.05f, 1f)] float followStrength = 0.9f;
        [SerializeField] float maxLinearSpeed = 20f;
        [SerializeField] float maxAngularSpeed = 50f;
        [Tooltip("How much each kilogram held reduces follow strength. Higher = heavy weapons lag more.")]
        [SerializeField] float weightPenaltyPerKg = 0.15f;
        [Tooltip("Resistance to being twisted by impacts (like wrist strength). Too low and blades slide around targets " +
                 "instead of stopping on them. Does not affect how closely the hand follows the controller.")]
        [SerializeField] float rotationalInertia = 1f;

        [Header("Snap back")]
        [Tooltip("If the hand stays this far (meters) from the controller for Snap Delay seconds, it snaps back.")]
        [SerializeField] float snapDistance = 0.6f;
        [SerializeField] float snapDelay = 0.4f;
        [Tooltip("Beyond this distance the hand snaps back immediately, e.g. after teleporting.")]
        [SerializeField] float instantSnapDistance = 1.5f;

        [Header("Collision")]
        [Tooltip("Colliders the hand should never hit, such as the player's own body.")]
        [SerializeField] Collider[] ignoredColliders;

        Rigidbody body;
        Collider[] handColliders;
        float heldMass;
        float separatedTime;

        /// <summary>Raised after the hand snapped back to the controller, so held items can follow.</summary>
        public event Action Teleported;

        public Rigidbody Body => body;
        public Transform Target => target;
        public XRNode HandNode => handNode;
        public Collider[] IgnoredColliders => ignoredColliders;
        public float HeldMass { get => heldMass; set => heldMass = Mathf.Max(0f, value); }
        /// <summary>Distance between the physics hand and the real controller.</summary>
        public float Separation => target ? Vector3.Distance(body.position, target.position) : 0f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = maxAngularSpeed;
            body.solverIterations = 20;
            body.solverVelocityIterations = 10;
            body.inertiaTensor = Vector3.one * rotationalInertia;
            body.inertiaTensorRotation = Quaternion.identity;
            handColliders = GetComponentsInChildren<Collider>();
        }

        void Start()
        {
            SetIgnored(ignoredColliders, true);
            if (target) TeleportToTarget();
        }

        void FixedUpdate()
        {
            if (target == null) return;

            float dt = Time.fixedDeltaTime;
            float separation = Separation;
            separatedTime = separation > snapDistance ? separatedTime + dt : 0f;
            if (separation > instantSnapDistance || separatedTime > snapDelay)
            {
                TeleportToTarget();
                return;
            }

            float strength = followStrength / (1f + heldMass * weightPenaltyPerKg);

            Vector3 desiredVelocity = (target.position - body.position) / dt;
            body.velocity = Vector3.ClampMagnitude(Vector3.Lerp(body.velocity, desiredVelocity, strength), maxLinearSpeed);

            Quaternion delta = target.rotation * Quaternion.Inverse(body.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 desiredAngular = Vector3.zero;
            if (Mathf.Abs(angle) > 0.01f && !float.IsInfinity(axis.x))
                desiredAngular = axis * (angle * Mathf.Deg2Rad / dt);
            body.angularVelocity = Vector3.ClampMagnitude(Vector3.Lerp(body.angularVelocity, desiredAngular, strength), maxAngularSpeed);
        }

        public void TeleportToTarget()
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
            body.position = target.position;
            body.rotation = target.rotation;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            separatedTime = 0f;
            Teleported?.Invoke();
        }

        /// <summary>Turns collisions between this hand and the given colliders off (or back on).</summary>
        public void SetIgnored(Collider[] others, bool ignore)
        {
            if (others == null) return;
            foreach (var other in others)
            {
                if (other == null) continue;
                foreach (var own in handColliders)
                    Physics.IgnoreCollision(own, other, ignore);
            }
        }

        public void Haptic(float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(handNode);
            if (device.isValid)
                device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }
    }
}
