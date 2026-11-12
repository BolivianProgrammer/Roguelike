using UnityEngine;
using UnityEngine.InputSystem;
using Roguelike.Weapons;

namespace Roguelike.Player
{
    /// <summary>
    /// Grabs <see cref="Grabbable"/> items while the grip button is held.
    /// Near items are taken directly; an item the hand points at from further away is pulled into the hand.
    /// A second hand can join on an already held item for a two-handed grip.
    /// </summary>
    [RequireComponent(typeof(PhysicsHand))]
    public class PhysicsHandGrabber : MonoBehaviour
    {
        [SerializeField] InputActionProperty gripAction;
        [Tooltip("Where a held item's grip point is placed. Rotate it to change how weapons sit in the hand.")]
        [SerializeField] Transform attachPoint;
        [SerializeField] float grabRadius = 0.12f;

        [Header("Distance grab")]
        [Tooltip("How far away (meters) an item can be and still be pulled into the hand.")]
        [SerializeField] float distanceGrabRange = 4f;
        [Tooltip("How far off the pointing direction (degrees) an item can be and still be targeted.")]
        [SerializeField] float distanceGrabAngle = 25f;
        [SerializeField] float pullSpeed = 9f;
        [Tooltip("Direction the hand points for distance grabs, in the attach point's local space.")]
        [SerializeField] Vector3 pointDirection = Vector3.forward;
        [SerializeField] Color pointerColor = new Color(0.4f, 0.9f, 1f, 0.8f);

        [Header("Two-handed grip")]
        [Tooltip("How firmly the main hand keeps twisting the weapon while a second hand steers it.")]
        [SerializeField] float twoHandedTwistSpring = 60f;
        [SerializeField] float twoHandedTwistDamper = 4f;

        PhysicsHand hand;
        Grabbable held;
        ConfigurableJoint joint;
        bool isPrimary;
        bool heldUsedGravity;
        bool wasPressed;

        InputAction specialAction;
        Grabbable pulling;
        bool pullingUsedGravity;
        Grabbable pointed;
        LineRenderer pointer;
        readonly Collider[] overlapResults = new Collider[32];

        public PhysicsHand Hand => hand;
        public Grabbable Held => held;
        /// <summary>
        /// When set, distance grabs aim along this transform's forward instead of the hand.
        /// Desktop play sets it to the camera so you grab what you look at.
        /// </summary>
        public Transform AimOverride { get; set; }

        void Awake()
        {
            hand = GetComponent<PhysicsHand>();
            if (attachPoint == null) attachPoint = transform;
            CreatePointer();
        }

        /// <summary>Arms the special attack of the weapon in this hand, if it has one.</summary>
        public bool ActivateHeldSpecial()
        {
            if (held == null) return false;
            var weapon = held.GetComponent<Weapon>();
            return weapon != null && weapon.ActivateSpecial();
        }

        void OnEnable()
        {
            // The trigger of this hand's controller arms the held weapon's special attack.
            if (specialAction == null)
            {
                string side = hand.HandNode == UnityEngine.XR.XRNode.LeftHand ? "LeftHand" : "RightHand";
                specialAction = new InputAction("Special", InputActionType.Button, "<XRController>{" + side + "}/{TriggerButton}");
            }
            specialAction.Enable();
            gripAction.action?.Enable();
            hand.Teleported += OnHandTeleported;
        }

        void OnDisable()
        {
            hand.Teleported -= OnHandTeleported;
            specialAction?.Disable();
            StopPulling();
            Release();
            if (pointer) pointer.enabled = false;
        }

        void Update()
        {
            bool pressed = gripAction.action != null && gripAction.action.IsPressed();
            if (pressed && !wasPressed) TryGrab();
            else if (!pressed && wasPressed)
            {
                StopPulling();
                Release();
            }
            wasPressed = pressed;

            if (specialAction != null && specialAction.WasPressedThisFrame()) ActivateHeldSpecial();

            UpdatePointer(pressed);
        }

        void FixedUpdate()
        {
            if (pulling == null) return;

            if (pulling.IsHeld || !pulling.isActiveAndEnabled)
            {
                StopPulling();
                return;
            }

            Vector3 toHand = attachPoint.position - pulling.GripPoint.position;
            float distance = toHand.magnitude;
            if (distance <= grabRadius)
            {
                var target = pulling;
                StopPulling();
                Grab(target);
                return;
            }

            // Never overshoot the hand in a single step.
            float speed = Mathf.Min(pullSpeed, distance / Time.fixedDeltaTime);
            pulling.Body.velocity = toHand / distance * speed;
            pulling.Body.angularVelocity *= 0.9f;
        }

        /// <summary>
        /// Grabs the nearest item, or starts pulling the one being pointed at. Returns false if there was neither.
        /// Called by the grip button in VR and directly by the desktop controls.
        /// </summary>
        public bool TryGrab()
        {
            if (held != null || pulling != null) return false;

            Grabbable near = FindNear();
            if (near != null)
            {
                Grab(near);
                return true;
            }

            Grabbable far = FindPointed();
            if (far != null) StartPulling(far);
            return far != null;
        }

        /// <summary>Grabs a specific item regardless of distance, e.g. drawing from a holster on desktop.</summary>
        public bool GrabItem(Grabbable target)
        {
            if (held != null || target == null || !CanGrab(target)) return false;
            StopPulling();
            Grab(target);
            return true;
        }

        Grabbable FindNear()
        {
            int count = Physics.OverlapSphereNonAlloc(attachPoint.position, grabRadius, overlapResults, ~0, QueryTriggerInteraction.Collide);
            Grabbable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = overlapResults[i].GetComponentInParent<Grabbable>();
                if (candidate == null || !CanGrab(candidate)) continue;
                float distance = Vector3.Distance(attachPoint.position, overlapResults[i].ClosestPoint(attachPoint.position));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        bool CanGrab(Grabbable candidate)
        {
            if (!candidate.IsHeld) return true;
            return candidate.AllowTwoHanded && candidate.Holders.Count < 2 && candidate.Holder != this;
        }

        /// <summary>The loose item the hand is pointing at, if any.</summary>
        Grabbable FindPointed()
        {
            Vector3 origin = AimOverride ? AimOverride.position : attachPoint.position;
            Vector3 direction = AimOverride ? AimOverride.forward : attachPoint.TransformDirection(pointDirection).normalized;
            Grabbable best = null;
            float bestAngle = distanceGrabAngle;

            var all = Grabbable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var candidate = all[i];
                // Items on the player's own body are drawn by hand; items on a weapon rack can be pulled.
                if (!candidate.AllowDistanceGrab || candidate.IsHeld) continue;
                if (candidate.Holster != null && candidate.Holster.OnBody) continue;
                if (candidate.PulledBy != null && candidate.PulledBy != this) continue;

                Vector3 toItem = candidate.Body.worldCenterOfMass - origin;
                float distance = toItem.magnitude;
                if (distance > distanceGrabRange || distance < grabRadius) continue;

                float angle = Vector3.Angle(direction, toItem);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }
            return best;
        }

        void StartPulling(Grabbable target)
        {
            target.PrepareForGrab();
            pulling = target;
            pulling.PulledBy = this;
            pullingUsedGravity = pulling.Body.useGravity;
            pulling.Body.useGravity = false;
            hand.Haptic(0.2f, 0.04f);
        }

        void StopPulling()
        {
            if (pulling == null) return;
            pulling.Body.useGravity = pullingUsedGravity;
            pulling.PulledBy = null;
            pulling = null;
        }

        void Grab(Grabbable target)
        {
            target.PrepareForGrab();

            held = target;
            isPrimary = !held.IsHeld;

            if (isPrimary)
            {
                held.AlignTo(attachPoint);
                heldUsedGravity = held.Body.useGravity;
                // Weight comes from the hand following more slowly; gravity on top only makes the weapon droop.
                held.Body.useGravity = false;
                CreateJoint(held.transform.InverseTransformPoint(attachPoint.position));
            }
            else
            {
                // Second hand: hold the item where it was touched and let both hands steer it.
                CreateJoint(held.transform.InverseTransformPoint(held.ClosestPoint(attachPoint.position)));
            }

            SetIgnoredWithPlayer(held, true);
            held.NotifyGrabbed(this);
            hand.Haptic(0.3f, 0.05f);
        }

        public void Release()
        {
            if (held == null) return;

            if (joint != null) Destroy(joint);
            joint = null;

            var released = held;
            held = null;
            hand.HeldMass = 0f;

            bool stillHeld = released.Holders.Count > 1;
            if (isPrimary && stillHeld)
                released.Holders[1].TakeOver(heldUsedGravity);
            if (!stillHeld)
            {
                released.Body.useGravity = heldUsedGravity;
                SetIgnoredWithPlayer(released, false);
            }

            released.NotifyReleased(this);
        }

        /// <summary>Called on the second hand when the main hand lets go: it now carries the item alone.</summary>
        void TakeOver(bool itemUsedGravity)
        {
            isPrimary = true;
            heldUsedGravity = itemUsedGravity;
            // Rebuild the joint so the item stays as it currently sits in this hand instead of twisting back.
            if (joint != null) Destroy(joint);
            CreateJoint(held.transform.InverseTransformPoint(attachPoint.position));
        }

        /// <summary>Called by the held item whenever a hand joins or leaves it.</summary>
        internal void OnHoldersChanged()
        {
            if (held == null || joint == null) return;

            int count = held.Holders.Count;
            hand.HeldMass = held.Body.mass / Mathf.Max(1, count);

            bool rigid = count <= 1;
            var motion = rigid ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
            joint.angularXMotion = motion;
            joint.angularYMotion = motion;
            joint.angularZMotion = motion;

            // With two hands the line between them aims the weapon; the main hand only twists it softly.
            bool twists = !rigid && held.Holder == this;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = twists ? twoHandedTwistSpring : 0f,
                positionDamper = twists ? twoHandedTwistDamper : 0f,
                maximumForce = float.MaxValue
            };
        }

        void CreateJoint(Vector3 anchorOnItem)
        {
            joint = held.gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = hand.Body;
            joint.anchor = anchorOnItem;
            joint.connectedAnchor = hand.transform.InverseTransformPoint(attachPoint.position);
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.enableCollision = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.02f;
            joint.projectionAngle = 5f;
        }

        void SetIgnoredWithPlayer(Grabbable item, bool ignore)
        {
            if (hand.IgnoredColliders == null) return;
            foreach (var own in item.Colliders)
                foreach (var other in hand.IgnoredColliders)
                    if (own != null && other != null)
                        Physics.IgnoreCollision(own, other, ignore);
        }

        void OnHandTeleported()
        {
            if (held != null && isPrimary && held.Holders.Count == 1) held.AlignTo(attachPoint);
        }

        void CreatePointer()
        {
            var go = new GameObject("Distance Grab Pointer");
            go.transform.SetParent(transform, false);
            pointer = go.AddComponent<LineRenderer>();
            pointer.useWorldSpace = true;
            pointer.positionCount = 2;
            pointer.startWidth = 0.006f;
            pointer.endWidth = 0.002f;
            pointer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pointer.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) pointer.material = new Material(shader);
            pointer.startColor = pointerColor;
            pointer.endColor = pointerColor;
            pointer.enabled = false;
        }

        void UpdatePointer(bool pressed)
        {
            Grabbable target = pulling;
            if (target == null && held == null && !pressed && FindNear() == null)
                target = FindPointed();

            if (target != pointed)
            {
                pointed = target;
                if (pointed != null && pulling == null) hand.Haptic(0.1f, 0.02f);
            }

            pointer.enabled = target != null;
            if (target == null) return;
            pointer.SetPosition(0, attachPoint.position);
            pointer.SetPosition(1, target.Body.worldCenterOfMass);
        }

        void OnDrawGizmosSelected()
        {
            Transform origin = attachPoint ? attachPoint : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin.position, grabRadius);
            Gizmos.DrawRay(origin.position, origin.TransformDirection(pointDirection).normalized * 0.5f);
        }
    }
}
