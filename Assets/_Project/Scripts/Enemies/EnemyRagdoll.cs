using UnityEngine;

namespace Roguelike.Enemies
{
    /// <summary>
    /// The rigidbodies on an enemy's bones. While alive they are kinematic and simply follow the animation,
    /// which is what the player's weapons hit. On death the animation stops and they go limp.
    /// </summary>
    public class EnemyRagdoll : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Rigidbody[] bodies;

        public bool IsLimp { get; private set; }

        void Awake()
        {
            SetKinematic(true);
        }

        /// <summary>Goes limp and shoves the body part nearest to the hit.</summary>
        public void Activate(Vector3 impulse, Vector3 point)
        {
            if (IsLimp) return;
            IsLimp = true;

            if (animator != null) animator.enabled = false;
            SetKinematic(false);

            Rigidbody nearest = null;
            float best = float.MaxValue;
            foreach (var body in bodies)
            {
                if (body == null) continue;
                float distance = (body.worldCenterOfMass - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = body;
                }
            }
            if (nearest != null) nearest.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

        void SetKinematic(bool kinematic)
        {
            if (bodies == null) return;
            foreach (var body in bodies)
            {
                if (body == null) continue;
                body.isKinematic = kinematic;
                body.useGravity = !kinematic;
            }
        }
    }
}
