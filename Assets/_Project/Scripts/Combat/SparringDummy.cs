using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// A practice opponent for blocking and parrying: a post with one arm that winds up (the club turns red),
    /// then swings at the player from the right, the left or overhead. Blocking knocks the arm back;
    /// parrying leaves it stunned for longer.
    /// </summary>
    public class SparringDummy : MonoBehaviour
    {
        enum State { Idle, Windup, Swing, Stunned, Recover }

        [Tooltip("Kinematic rigidbody at the shoulder. The arm extends along its forward (blue) axis.")]
        [SerializeField] Rigidbody arm;
        [SerializeField] MeleeAttackHitbox hitbox;
        [SerializeField] Renderer clubRenderer;
        [Tooltip("Defaults to the main camera.")]
        [SerializeField] Transform player;

        [Header("Attack")]
        [SerializeField] float damage = 15f;
        [Tooltip("The dummy only attacks while the player is within this distance (meters).")]
        [SerializeField] float attackRange = 2.2f;
        [SerializeField] float timeBetweenAttacks = 2f;
        [Tooltip("How long the arm stays raised before swinging. This is the player's warning.")]
        [SerializeField] float windupTime = 0.8f;
        [SerializeField] float swingTime = 0.3f;
        [SerializeField] float recoverTime = 0.6f;
        [SerializeField] float windupAngle = 110f;
        [SerializeField] float followThroughAngle = 60f;

        [Header("When blocked")]
        [SerializeField] float blockedStun = 0.8f;
        [SerializeField] float parriedStun = 2.5f;

        [Header("Colors")]
        [SerializeField] Color idleColor = new Color(0.45f, 0.45f, 0.45f);
        [SerializeField] Color windupColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] Color stunnedColor = new Color(0.3f, 0.6f, 1f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        State state;
        float timer;
        float stunDuration;
        float facingYaw;
        float swingRoll;
        int attackIndex;
        Quaternion from;
        Quaternion targetRotation;
        Vector3 shoulderOffset;
        MaterialPropertyBlock propertyBlock;

        /// <summary>True while the dummy is reeling from a block or parry. Later used to open finishers.</summary>
        public bool IsStunned => state == State.Stunned;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            facingYaw = transform.eulerAngles.y;
            targetRotation = RestPose();
            if (arm != null)
            {
                shoulderOffset = transform.InverseTransformPoint(arm.position);
                arm.transform.rotation = targetRotation;
            }
            SetColor(idleColor);
        }

        void OnEnable()
        {
            if (hitbox != null) hitbox.Blocked += OnBlocked;
        }

        void OnDisable()
        {
            if (hitbox != null) hitbox.Blocked -= OnBlocked;
        }

        void Start()
        {
            if (player == null && Camera.main != null) player = Camera.main.transform;
        }

        void FixedUpdate()
        {
            if (arm == null || hitbox == null) return;

            timer += Time.fixedDeltaTime;
            switch (state)
            {
                case State.Idle:
                    targetRotation = RestPose();
                    if (timer >= timeBetweenAttacks && PlayerInRange()) StartWindup();
                    break;

                case State.Windup:
                {
                    float t = Mathf.Clamp01(timer / windupTime);
                    // Reach the raised pose in the first 60% of the windup, then hold it as a clear warning.
                    targetRotation = Quaternion.Slerp(from, Pose(swingRoll, windupAngle), Mathf.SmoothStep(0f, 1f, t / 0.6f));
                    SetColor(Color.Lerp(idleColor, windupColor, t));
                    if (t >= 1f)
                    {
                        hitbox.Begin(damage);
                        Enter(State.Swing);
                    }
                    break;
                }

                case State.Swing:
                {
                    float t = Mathf.Clamp01(timer / swingTime);
                    targetRotation = Pose(swingRoll, Mathf.Lerp(windupAngle, -followThroughAngle, t * t));
                    if (t >= 1f || !hitbox.Active && timer > swingTime * 0.5f)
                    {
                        hitbox.End();
                        SetColor(idleColor);
                        Enter(State.Recover);
                    }
                    break;
                }

                case State.Stunned:
                {
                    // Knocked back towards where the swing came from.
                    float t = Mathf.Clamp01(timer / 0.15f);
                    targetRotation = Quaternion.Slerp(from, Pose(swingRoll, windupAngle * 0.7f), t);
                    if (timer >= stunDuration)
                    {
                        SetColor(idleColor);
                        Enter(State.Recover);
                    }
                    break;
                }

                case State.Recover:
                {
                    float t = Mathf.Clamp01(timer / recoverTime);
                    targetRotation = Quaternion.Slerp(from, RestPose(), Mathf.SmoothStep(0f, 1f, t));
                    if (t >= 1f) Enter(State.Idle);
                    break;
                }
            }

            // A kinematic body does not follow its parent on its own, so keep the shoulder on the dummy.
            arm.MovePosition(transform.TransformPoint(shoulderOffset));
            arm.MoveRotation(targetRotation);
        }

        void StartWindup()
        {
            Vector3 toPlayer = player.position - arm.position;
            facingYaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;

            // Cycle: from the right, from the left, overhead.
            switch (attackIndex++ % 3)
            {
                case 0: swingRoll = 0f; break;
                case 1: swingRoll = 180f; break;
                default: swingRoll = 90f; break;
            }
            Enter(State.Windup);
        }

        void OnBlocked(Vector3 point, bool parried)
        {
            hitbox.End();
            stunDuration = parried ? parriedStun : blockedStun;
            SetColor(stunnedColor);
            DamagePopup.Spawn(point, parried ? "PARRY" : "BLOCK", parried ? new Color(1f, 0.85f, 0.2f) : Color.white);
            Enter(State.Stunned);
        }

        void Enter(State next)
        {
            state = next;
            timer = 0f;
            from = arm.rotation;
        }

        bool PlayerInRange()
        {
            if (player == null) return false;
            Vector3 offset = player.position - arm.position;
            offset.y = 0f;
            return offset.magnitude <= attackRange;
        }

        Quaternion RestPose() => Pose(90f, 90f);

        /// <summary>
        /// Arm rotation for a swing plane (roll: 0 = from the right, 180 = from the left, 90 = overhead)
        /// and an angle within that plane (0 = pointing straight at the player).
        /// </summary>
        Quaternion Pose(float roll, float angle)
        {
            return Quaternion.AngleAxis(facingYaw, Vector3.up)
                 * Quaternion.AngleAxis(roll, Vector3.forward)
                 * Quaternion.AngleAxis(angle, Vector3.up);
        }

        void SetColor(Color color)
        {
            if (clubRenderer == null) return;
            clubRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            clubRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
