using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Enemies
{
    /// <summary>
    /// Keeps a set number of enemies alive, spawning replacements at its spawn points.
    /// Raise Max Alive to practise fighting several at once.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [Tooltip("Where enemies appear, used in turn. Defaults to this object's position.")]
        [SerializeField] Transform[] spawnPoints;
        [SerializeField, Min(0)] int maxAlive = 1;
        [Tooltip("Seconds between an enemy dying and its replacement appearing.")]
        [SerializeField] float respawnDelay = 4f;
        [Tooltip("How many enemies this spawner produces in total. 0 = keep replacing them forever.")]
        [SerializeField, Min(0)] int totalSpawns = 0;
        [Tooltip("The spawner stays asleep until the player comes within this distance (meters). 0 = active from the start.")]
        [SerializeField] float activationRange = 0f;
        [Tooltip("Spawned enemies start unaware (weapon lowered) and notice the player when they come close.")]
        [SerializeField] bool spawnUnaware = true;
        [Tooltip("A spawner of Grunts sometimes sends a Runner, Shieldbearer or Elite instead (more often on harder missions).")]
        [SerializeField] bool mixVariants = true;

        readonly List<EnemyHealth> alive = new List<EnemyHealth>();
        float nextSpawnTime;
        int nextPoint;
        int spawned;
        bool awake;

        void Update()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null && !alive[i].IsDead) continue;
                alive.RemoveAt(i);
                nextSpawnTime = Mathf.Max(nextSpawnTime, Time.time + respawnDelay);
            }

            if (!awake)
            {
                var cam = Camera.main;
                if (activationRange > 0f && (cam == null || Vector3.Distance(cam.transform.position, transform.position) > activationRange)) return;
                awake = true;
            }

            if (totalSpawns > 0 && spawned >= totalSpawns) return;
            if (enemyPrefab == null || alive.Count >= maxAlive || Time.time < nextSpawnTime) return;

            Transform point = spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[nextPoint++ % spawnPoints.Length] : transform;
            GameObject prefab = enemyPrefab;
            var assets = Meta.GameAssets.Instance;
            if (mixVariants && assets != null && prefab == assets.grunt)
            {
                var mission = Run.MissionManager.Current;
                int tier = mission != null && mission.Definition != null ? mission.Definition.tier : 0;
                GameObject variant = assets.RandomMelee(tier);
                if (variant != null) prefab = variant;
            }
            GameObject enemy = Instantiate(prefab, point.position, point.rotation);
            var brain = enemy.GetComponent<EnemyBrain>();
            // The first enemies at a post have not seen the player yet; replacements arrive alert.
            if (brain != null && spawnUnaware && spawned < maxAlive) brain.SetUnaware(true);
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null) alive.Add(health);
            spawned++;
            nextSpawnTime = Time.time + 0.5f;
        }
    }
}
