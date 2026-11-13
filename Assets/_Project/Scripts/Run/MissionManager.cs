using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Combat;
using Roguelike.Enemies;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// Runs one mission from start to finish: destroy every totem (each offers a buff), which wakes the boss;
    /// kill the boss to complete the level; leave through the portal to keep what you gathered.
    /// Dying at any point loses everything carried.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [SerializeField] Totem[] totems;
        [SerializeField] ExtractionZone extraction;

        [Header("Buffs")]
        [Tooltip("Every buff a totem can offer. Each totem offers a few of these at random.")]
        [SerializeField] BuffDefinition[] buffPool;
        [SerializeField] int choicesPerTotem = 3;

        [Header("Boss")]
        [SerializeField] GameObject bossPrefab;
        [SerializeField] Transform bossSpawnPoint;
        [Tooltip("Dropped where the boss dies. The player has to carry it out to keep it.")]
        [SerializeField] GameObject[] bossRewards;
        [SerializeField] int bossEssence = 150;
        [SerializeField] int bossCoins = 80;

        [Header("Ending")]
        [Tooltip("Seconds after dying or extracting before the mission restarts. Later this returns to the hub.")]
        [SerializeField] float endDelay = 5f;

        readonly List<BuffDefinition> chosenBuffs = new List<BuffDefinition>();
        PlayerHealth player;
        EnemyHealth boss;
        int totemsDestroyed;
        bool bossDefeated;
        bool ended;
        string objective = "";

        void OnEnable()
        {
            EnemyHealth.AnyDied += OnEnemyDied;
        }

        void OnDisable()
        {
            EnemyHealth.AnyDied -= OnEnemyDied;
            // Buffs last for one run only.
            PlayerStats.RemoveAll(this);
        }

        void Start()
        {
            RunState.Begin();

            player = FindObjectOfType<PlayerHealth>();
            if (player != null)
            {
                // In a mission, death is final.
                player.ReviveDelay = 0f;
                player.Died += OnPlayerDied;
            }

            foreach (var totem in totems)
                if (totem != null) totem.Destroyed += OnTotemDestroyed;
            if (extraction != null) extraction.Extracted += OnExtracted;

            UpdateObjective();
            Announcer.Show("Destroy the totems", new Color(0.8f, 0.6f, 1f));
        }

        void OnTotemDestroyed(Totem totem)
        {
            totemsDestroyed++;
            Announcer.Show("Totem destroyed  " + totemsDestroyed + " / " + totems.Length + "\nChoose a blessing", new Color(0.8f, 0.6f, 1f));
            BuffChoice.Offer(totem.transform.position, PickBuffs(), OnBuffChosen);
            UpdateObjective();

            if (totemsDestroyed >= totems.Length) StartCoroutine(WakeBoss());
        }

        List<BuffDefinition> PickBuffs()
        {
            var pool = new List<BuffDefinition>();
            if (buffPool != null)
                foreach (var buff in buffPool)
                    if (buff != null) pool.Add(buff);

            var picks = new List<BuffDefinition>();
            while (picks.Count < choicesPerTotem && pool.Count > 0)
            {
                int index = Random.Range(0, pool.Count);
                picks.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picks;
        }

        void OnBuffChosen(BuffDefinition buff)
        {
            buff.Apply(this);
            chosenBuffs.Add(buff);
            Announcer.Show(buff.displayName + "\n" + buff.description, buff.color);
        }

        IEnumerator WakeBoss()
        {
            // Give the player a moment to pick the last buff.
            yield return new WaitForSeconds(4f);
            if (ended || bossPrefab == null) yield break;

            Transform point = bossSpawnPoint != null ? bossSpawnPoint : transform;
            GameObject spawned = Instantiate(bossPrefab, point.position, point.rotation);
            boss = spawned.GetComponent<EnemyHealth>();
            Announcer.Show("The guardian awakens", new Color(1f, 0.3f, 0.2f), 4f);
            UpdateObjective();
        }

        void OnEnemyDied(EnemyHealth enemy, DamageInfo info)
        {
            if (ended) return;

            RunState.CountKill();
            if (enemy == boss && boss != null)
            {
                bossDefeated = true;
                RunState.Add(bossEssence, bossCoins);
                DropRewards(enemy.transform.position);
                if (extraction != null) extraction.Fast = true;
                Announcer.Show("LEVEL COMPLETE\nTake your reward to the portal", new Color(1f, 0.85f, 0.3f), 5f);
            }
            else
            {
                RunState.Add(enemy.EssenceReward, enemy.CoinReward);
            }
            UpdateObjective();
        }

        void DropRewards(Vector3 position)
        {
            if (bossRewards == null || bossRewards.Length == 0) return;
            GameObject reward = bossRewards[Random.Range(0, bossRewards.Length)];
            if (reward != null) Instantiate(reward, position + Vector3.up * 1.2f, Quaternion.identity);
        }

        void OnPlayerDied()
        {
            if (ended) return;
            ended = true;

            int essence = RunState.Essence, coins = RunState.Coins;
            RunState.Lose();
            objective = "";
            Announcer.Show("YOU DIED\nLost: " + essence + " essence, " + coins + " coins, everything carried", new Color(1f, 0.2f, 0.15f), endDelay);
            StartCoroutine(Restart());
        }

        void OnExtracted()
        {
            if (ended) return;
            ended = true;

            int essence = RunState.Essence, coins = RunState.Coins;
            RunState.Bank();
            objective = "";
            Announcer.Show((bossDefeated ? "VICTORY" : "EXTRACTED") + "\nKept: " + essence + " essence, " + coins + " coins",
                new Color(0.4f, 1f, 0.8f), endDelay);
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return new WaitForSeconds(endDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void UpdateObjective()
        {
            if (ended) return;
            if (bossDefeated) objective = "Reach the portal to leave with your loot";
            else if (boss != null) objective = "Defeat the guardian";
            else if (totemsDestroyed >= totems.Length) objective = "Something stirs...";
            else objective = "Destroy the totems  " + totemsDestroyed + " / " + totems.Length + "   (or leave early through the portal)";
        }

        /// <summary>Objective and run loot. Drawn on the monitor; the headset gets the floating announcements.</summary>
        void OnGUI()
        {
            float x = Screen.width - 370f;
            GUI.Box(new Rect(x, 10f, 360f, 74f + chosenBuffs.Count * 18f), "Mission");
            GUI.Label(new Rect(x + 10f, 32f, 340f, 22f), objective);
            GUI.Label(new Rect(x + 10f, 52f, 340f, 22f),
                "Essence: " + RunState.Essence + "    Coins: " + RunState.Coins + "    Kills: " + RunState.Kills);
            for (int i = 0; i < chosenBuffs.Count; i++)
                GUI.Label(new Rect(x + 10f, 72f + i * 18f, 340f, 22f), "+ " + chosenBuffs[i].displayName);
        }
    }
}
