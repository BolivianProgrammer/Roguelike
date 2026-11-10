using UnityEngine;

namespace Roguelike.Enemies
{
    /// <summary>
    /// An invisible kinematic rigidbody that follows a transform through the physics engine, used as the
    /// striking surface of an enemy's weapon. The visible weapon is parented to the hand so it always looks
    /// right; this body is swept from pose to pose, so a fast swing cannot skip through the player's blade.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicFollower : MonoBehaviour
    {
        public Transform target;

        Rigidbody body;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void Start()
        {
            // Stand alone in the scene: as a child, the owner's own movement would fight the physics moves.
            transform.SetParent(null, true);
            if (target == null) return;
            transform.SetPositionAndRotation(target.position, target.rotation);
            body.position = target.position;
            body.rotation = target.rotation;
        }

        void FixedUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }
            body.MovePosition(target.position);
            body.MoveRotation(target.rotation);
        }
    }
}
