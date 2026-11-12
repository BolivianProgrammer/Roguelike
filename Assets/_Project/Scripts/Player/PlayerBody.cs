using UnityEngine;

namespace Roguelike.Player
{
    /// <summary>
    /// The player's torso: a capsule that stays under the headset and is what enemy attacks hit.
    /// It turns to follow the head only after the head has turned well away, so holsters on the hips
    /// stay put while the player just looks around.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        [SerializeField] Transform head;
        [Tooltip("The XR Origin; its height is treated as the floor.")]
        [SerializeField] Transform origin;
        [SerializeField] CapsuleCollider capsule;
        [Tooltip("How far (degrees) the head can turn before the body starts turning with it.")]
        [SerializeField] float turnThreshold = 50f;
        [SerializeField] float turnSpeed = 300f;

        float yaw;
        bool turning;

        /// <summary>Current height of the head above the floor, in meters.</summary>
        public float Height { get; private set; } = 1.7f;
        public Transform Head => head;

        /// <summary>
        /// How fast the whole player rig is moving through the world (walking, not hand or head movement).
        /// Weapons subtract this so that walking into an enemy does not count as a swing.
        /// </summary>
        public static Vector3 RigVelocity { get; private set; }

        Vector3 lastOriginPosition;

        void Start()
        {
            if (head != null) yaw = head.eulerAngles.y;
            if (origin != null) lastOriginPosition = origin.position;
        }

        void FixedUpdate()
        {
            if (origin == null) return;
            Vector3 velocity = (origin.position - lastOriginPosition) / Time.fixedDeltaTime;
            lastOriginPosition = origin.position;
            // Smoothed, because the rig moves once per rendered frame rather than once per physics step.
            RigVelocity = Vector3.Lerp(RigVelocity, velocity, 0.3f);
        }

        void OnDestroy() => RigVelocity = Vector3.zero;

        void LateUpdate()
        {
            if (head == null) return;

            float floorY = origin != null ? origin.position.y : 0f;
            Vector3 headPosition = head.position;
            Height = Mathf.Clamp(headPosition.y - floorY, 0.5f, 2.3f);

            float headYaw = head.eulerAngles.y;
            float delta = Mathf.Abs(Mathf.DeltaAngle(yaw, headYaw));
            if (delta > turnThreshold) turning = true;
            else if (delta < 5f) turning = false;
            if (turning) yaw = Mathf.MoveTowardsAngle(yaw, headYaw, turnSpeed * Time.deltaTime);

            transform.SetPositionAndRotation(new Vector3(headPosition.x, floorY, headPosition.z), Quaternion.Euler(0f, yaw, 0f));

            if (capsule != null)
            {
                capsule.height = Height;
                capsule.center = new Vector3(0f, Height * 0.5f, 0f);
            }
        }
    }
}
