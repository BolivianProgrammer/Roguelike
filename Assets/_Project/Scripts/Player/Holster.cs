using UnityEngine;
using Roguelike.Weapons;

namespace Roguelike.Player
{
    /// <summary>
    /// A slot on the player's body that stores one item. Let go of an item close to the holster to store it;
    /// grab it there to draw it. The holster's rotation sets how the stored item hangs.
    /// </summary>
    public class Holster : MonoBehaviour
    {
        [SerializeField] PlayerBody body;
        [Tooltip("Height of the holster as a share of the player's head height (0.55 is about hip level).")]
        [SerializeField, Range(0.2f, 1f)] float heightFraction = 0.55f;
        [Tooltip("Offset from the body's center: X to the right, Y forward.")]
        [SerializeField] Vector2 offset = new Vector2(0.22f, 0f);
        [Tooltip("An item let go within this distance (meters) is stored.")]
        [SerializeField] float radius = 0.2f;
        [Tooltip("Item stored here when the scene starts.")]
        [SerializeField] Grabbable startingItem;
        [Tooltip("Shown while the holster is empty.")]
        [SerializeField] Renderer emptyIndicator;

        Grabbable item;
        bool[] colliderWasTrigger;
        bool itemWasKinematic;
        RigidbodyInterpolation itemInterpolation;

        public Grabbable Item => item;
        /// <summary>True for holsters worn on the player; false for fixed ones such as a weapon rack.</summary>
        public bool OnBody => body != null;

        void OnEnable() => Grabbable.Dropped += OnDropped;
        void OnDisable() => Grabbable.Dropped -= OnDropped;

        void Start()
        {
            UpdatePosition();
            if (startingItem != null) Store(startingItem);
            RefreshIndicator();
        }

        void LateUpdate() => UpdatePosition();

        void UpdatePosition()
        {
            if (body == null) return;
            transform.localPosition = new Vector3(offset.x, body.Height * heightFraction, offset.y);
        }

        void OnDropped(Grabbable dropped, PhysicsHandGrabber hand)
        {
            if (item != null || dropped.Holster != null) return;
            if (Vector3.Distance(dropped.GripPoint.position, transform.position) > radius) return;

            Store(dropped);
            if (hand != null) hand.Hand.Haptic(0.4f, 0.06f);
        }

        public void Store(Grabbable target)
        {
            item = target;
            item.Holster = this;

            var itemBody = item.Body;
            itemWasKinematic = itemBody.isKinematic;
            itemInterpolation = itemBody.interpolation;
            if (!itemBody.isKinematic)
            {
                itemBody.velocity = Vector3.zero;
                itemBody.angularVelocity = Vector3.zero;
            }
            itemBody.isKinematic = true;
            // A parented body must not interpolate, or it lags behind the player.
            itemBody.interpolation = RigidbodyInterpolation.None;

            // Stored items must not shove the hands or the world around, but still need to be found by a grab.
            var colliders = item.Colliders;
            colliderWasTrigger = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                colliderWasTrigger[i] = colliders[i].isTrigger;
                colliders[i].isTrigger = true;
            }

            item.transform.SetParent(transform, true);
            item.AlignTo(transform);
            RefreshIndicator();
        }

        /// <summary>Hands the stored item back to the physics world so it can be grabbed.</summary>
        public void Eject()
        {
            if (item == null) return;

            var released = item;
            item = null;
            released.Holster = null;
            released.transform.SetParent(null, true);

            var colliders = released.Colliders;
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].isTrigger = colliderWasTrigger[i];

            released.Body.isKinematic = itemWasKinematic;
            released.Body.interpolation = itemInterpolation;
            RefreshIndicator();
        }

        void RefreshIndicator()
        {
            if (emptyIndicator != null) emptyIndicator.enabled = item == null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawRay(transform.position, transform.forward * 0.3f);
        }
    }
}
