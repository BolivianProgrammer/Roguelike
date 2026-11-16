using System.Collections.Generic;
using UnityEngine;
using Roguelike.Audio;
using Roguelike.Combat;
using Roguelike.Enemies;
using Roguelike.Meta;
using Roguelike.UI;

namespace Roguelike.Run
{
    /// <summary>
    /// A challenge shrine. Use it to start a timed fight: enemies pour in around the shrine, and killing the
    /// required number before time runs out pays out a big reward (and, often, an item). Failing costs nothing
    /// but the time, though the enemies that came stay. One challenge per shrine per run.
    /// Place it in a mission scene, in an open area enemies can reach.
    /// </summary>
    public class KillChallenge : MonoBehaviour
    {
        [Tooltip("How many of the shrine's enemies must die.")]
        [SerializeField] int killsNeeded = 6;
        [Tooltip("Seconds to do it in. 0 = use the mission's challenge time.")]
        [SerializeField] float timeLimit = 0f;
        [Tooltip("How many enemies are on the field at once.")]
        [SerializeField] int maxAtOnce = 3;
        [Tooltip("How far from the shrine enemies appear.")]
        [SerializeField] float spawnRadius = 9f;
        [SerializeField] int rewardEssence = 120;
        [SerializeField] int rewardCoins = 90;
        [SerializeField, Range(0f, 1f)] float rewardItemChance = 0.6f;

        enum Stage { Waiting, Running, Won, Lost }

        /// <summary>The countdown of the challenge running now, for the wrist display. Empty when none is running.</summary>
        public static string ActiveText { get; private set; } = "";

        Stage stage;
        float endTime;
        int kills;
        int spawnedTotal;
        readonly List<EnemyHealth> mine = new List<EnemyHealth>();
        InteractPrompt prompt;
        float nextSpawn;
        float nextAnnounce;

        void Start()
        {
            if (GetComponentInChildren<Renderer>() == null) BuildGraybox();
            prompt = InteractPrompt.Add(gameObject, "Challenge: kill " + killsNeeded + " in " + Mathf.RoundToInt(TimeLimit) + "s", 2.4f, 2.2f);
            prompt.Used += Begin;
        }

        void OnEnable() => EnemyHealth.AnyDied += OnEnemyDied;

        void OnDisable()
        {
            EnemyHealth.AnyDied -= OnEnemyDied;
            if (stage == Stage.Running) ActiveText = "";
        }

        float TimeLimit
        {
            get
            {
                if (timeLimit > 0f) return timeLimit;
                var mission = MissionManager.Current;
                return mission != null && mission.Definition != null ? mission.Definition.challengeTime : 45f;
            }
        }

        void BuildGraybox()
        {
            var stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stone.name = "Shrine Stone";
            stone.transform.SetParent(transform, false);
            stone.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            stone.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            stone.GetComponent<MeshRenderer>().sharedMaterial = FlatMaterials.Create(new Color(0.25f, 0.22f, 0.28f));
            var glyph = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glyph.name = "Shrine Glyph";
            Destroy(glyph.GetComponent<Collider>());
            glyph.transform.SetParent(transform, false);
            glyph.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            glyph.transform.localScale = Vector3.one * 0.25f;
            glyph.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            glyph.GetComponent<MeshRenderer>().sharedMaterial = FlatMaterials.Create(new Color(1f, 0.35f, 0.25f));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.25f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.7f, 1.8f, 0.7f));
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }

        void Begin()
        {
            if (stage != Stage.Waiting) return;
            stage = Stage.Running;
            endTime = Time.time + TimeLimit;
            prompt.Set("", false);
            Sfx.Play(SfxKind.ChallengeStart, transform.position + Vector3.up);
            HitSpark.Ring(transform.position, new Color(1f, 0.35f, 0.25f), 3f, 70);
            Announcer.Show("CHALLENGE\nKill " + killsNeeded + " in " + Mathf.RoundToInt(TimeLimit) + " seconds", new Color(1f, 0.45f, 0.3f), 2.5f);
        }

        void OnEnemyDied(EnemyHealth enemy, DamageInfo info)
        {
            if (stage != Stage.Running || !mine.Remove(enemy)) return;
            kills++;
            if (kills >= killsNeeded) Win();
        }

        void Update()
        {
            if (stage != Stage.Running) return;
            if (WorldFreeze.Active) { endTime += Time.deltaTime; return; }

            float left = endTime - Time.time;
            ActiveText = "Challenge: " + kills + " / " + killsNeeded + "   " + Mathf.CeilToInt(Mathf.Max(0f, left)) + "s";
            prompt.Set(ActiveText, false);
            if (left <= 10f && Time.time >= nextAnnounce)
            {
                nextAnnounce = Time.time + 5f;
                Announcer.Show(Mathf.CeilToInt(left) + " seconds", new Color(1f, 0.6f, 0.3f), 1.2f);
            }
            if (left <= 0f)
            {
                Lose();
                return;
            }

            // Keep the field busy, but never more than needed to finish.
            mine.RemoveAll(e => e == null || e.IsDead);
            if (Time.time >= nextSpawn && mine.Count < maxAtOnce && spawnedTotal < killsNeeded + maxAtOnce)
            {
                nextSpawn = Time.time + 1.2f;
                SpawnOne();
            }
        }

        void SpawnOne()
        {
            var assets = GameAssets.Instance;
            if (assets == null) return;
            var mission = MissionManager.Current;
            int tier = mission != null && mission.Definition != null ? mission.Definition.tier : 0;
            GameObject prefab = assets.RandomMelee(tier);
            if (prefab == null) return;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            GameObject enemy = Instantiate(prefab, at, Quaternion.LookRotation(transform.position - at));
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null) mine.Add(health);
            spawnedTotal++;
        }

        void Win()
        {
            stage = Stage.Won;
            ActiveText = "";
            prompt.Set("Challenge complete", false);
            Sfx.Play(SfxKind.ChallengeEnd, transform.position + Vector3.up);
            Announcer.Show("CHALLENGE COMPLETE", new Color(1f, 0.85f, 0.3f), 3f);
            LootShard.Burst(transform.position + Vector3.up * 2f, rewardEssence, rewardCoins);

            if (Random.value > rewardItemChance) return;
            var loot = ItemDatabase.All(item => item.chestLoot);
            if (loot.Count == 0) return;
            var found = loot[Random.Range(0, loot.Count)];
            if (found.kind == ItemKind.Weapon && found.prefab != null)
                Instantiate(found.prefab, transform.position + Vector3.up * 2.3f + transform.forward * 0.6f, Quaternion.identity);
            else MissionManager.FoundGear.Add(found.id);
            Announcer.Show("Reward: " + found.displayName, new Color(1f, 0.85f, 0.3f), 3f);
        }

        void Lose()
        {
            stage = Stage.Lost;
            ActiveText = "";
            prompt.Set("Challenge failed", false);
            Sfx.Play(SfxKind.UiBack, transform.position + Vector3.up);
            Announcer.Show("Challenge failed", new Color(1f, 0.4f, 0.3f), 2.5f);
        }
    }
}
