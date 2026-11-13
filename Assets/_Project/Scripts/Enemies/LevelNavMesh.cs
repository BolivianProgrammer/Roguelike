using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike.Enemies
{
    /// <summary>
    /// Works out where enemies can walk, automatically, when the level loads. Put one of these in a level
    /// and enemies will path around its walls and obstacles. Nothing needs to be baked by hand: move or add
    /// level geometry and it is picked up the next time the scene starts.
    ///
    /// Everything with a collider inside the volume counts as level geometry, except moving things:
    /// anything with a rigidbody (weapons, enemies, physics props), triggers and the player.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class LevelNavMesh : MonoBehaviour
    {
        [Tooltip("Size of the box (meters), centered on this object, that is searched for level geometry.")]
        [SerializeField] Vector3 size = new Vector3(120f, 30f, 120f);
        [Tooltip("How close enemies may walk to walls. Roughly the enemy's shoulder width.")]
        [SerializeField] float agentRadius = 0.4f;
        [SerializeField] float agentHeight = 1.9f;
        [Tooltip("Tallest ledge (meters) an enemy can step up.")]
        [SerializeField] float stepHeight = 0.35f;
        [Tooltip("Steepest slope (degrees) an enemy can walk on.")]
        [SerializeField] float maxSlope = 40f;

        NavMeshDataInstance instance;

        /// <summary>True once a level has built its walkable area. Enemies then use pathfinding.</summary>
        public static bool Ready { get; private set; }

        void Awake() => Rebuild();

        /// <summary>Works out the walkable area again, e.g. after the level changes while playing.</summary>
        public void Rebuild()
        {
            instance.Remove();

            var bounds = new Bounds(transform.position, size);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);

            // Keep only things that stay put.
            sources.RemoveAll(source =>
            {
                var collider = source.component as Collider;
                if (collider == null) return false;
                return collider.isTrigger || collider.attachedRigidbody != null || collider is CharacterController;
            });

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentClimb = stepHeight;
            settings.agentSlope = maxSlope;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogWarning("[LevelNavMesh] Could not build a walkable area for this level.", this);
                return;
            }

            instance = NavMesh.AddNavMeshData(data);
            Ready = true;
        }

        void OnDestroy()
        {
            instance.Remove();
            Ready = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
