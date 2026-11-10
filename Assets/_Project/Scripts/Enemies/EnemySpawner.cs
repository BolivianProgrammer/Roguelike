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

        readonly List<EnemyHealth> alive = new List<EnemyHealth>();
        float nextSpawnTime;
        int nextPoint;

        void Update()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null && !alive[i].IsDead) continue;
                alive.RemoveAt(i);
                nextSpawnTime = Mathf.Max(nextSpawnTime, Time.time + respawnDelay);
            }

            if (enemyPrefab == null || alive.Count >= maxAlive || Time.time < nextSpawnTime) return;

            Transform point = spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[nextPoint++ % spawnPoints.Length] : transform;
            GameObject enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null) alive.Add(health);
            nextSpawnTime = Time.time + 0.5f;
        }
    }
}
