using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Audio;
using Roguelike.Combat;
using Roguelike.Enemies;
using Roguelike.Gear;
using Roguelike.Meta;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// Runs one mission from start to finish: destroy every totem (each offers a blessing), which wakes the boss;
    /// kill the boss for its spoils; leave through the portal to keep what you gathered.
    /// Dying at any point, or quitting, loses only what this run gave (its essence, coins and finds); the weapons
    /// brought in and the gear worn go back home.
    /// See MISSIONS.md for how to set up a mission scene of your own.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [SerializeField] Totem[] totems;
        [SerializeField] ExtractionZone extraction;

        [Header("Buffs")]
        [Tooltip("The blessings totems offer here. Empty = every blessing in Resources/BuffCatalog.")]
        [SerializeField] BuffDefinition[] buffPool;
        [SerializeField] int choicesPerTotem = 3;

        [Header("Boss")]
        [SerializeField] GameObject bossPrefab;
        [SerializeField] Transform bossSpawnPoint;
        [Tooltip("Only used if the item database has no guardian loot: dropped where the boss dies.")]
        [SerializeField] GameObject[] bossRewards;
        [SerializeField] int bossEssence = 150;
        [SerializeField] int bossCoins = 80;

        [Header("Ending")]
        [Tooltip("Seconds after dying or extracting before returning to the hub.")]
        [SerializeField] float endDelay = 5f;

        /// <summary>The mission running right now, or null outside a mission.</summary>
        public static MissionManager Current { get; private set; }

        /// <summary>What the player should do next, for the wrist display and the desktop overlay.</summary>
        public static string Objective { get; private set; } = "";

        /// <summary>Armor and trinkets found on this run. They are only the player's once carried out.</summary>
        public static readonly List<string> FoundGear = new List<string>();

        readonly List<BuffDefinition> chosenBuffs = new List<BuffDefinition>();
        MissionDefinition definition;
        PlayerHealth player;
        EnemyHealth boss;
        int totemsDestroyed;
        bool bossDefeated;
        bool ended;

        public MissionDefinition Definition => definition;

        void OnEnable()
        {
            Current = this;
            EnemyHealth.AnyDied += OnEnemyDied;
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            EnemyHealth.AnyDied -= OnEnemyDied;
            // Buffs last for one run only.
            PlayerStats.RemoveAll(this);
            Objective = "";
            Difficulty.Reset();
        }

        void Start()
        {
            RunState.Begin();
            FoundGear.Clear();

            SaveData data = SaveSystem.Data;
            definition = MissionCatalog.Current != null ? MissionCatalog.Current : MissionCatalog.Selected(data);
            Difficulty.Set(definition != null ? definition.tier : 0, data.level);

            // The player arrives with what they carried through the hub's gate. Started directly in the editor
            // with nothing chosen, they get the starter weapon so the scene is still playable.
            var loadout = new List<string>(data.loadout);
            if (!Loadout.EnteredFromHub && loadout.Count == 0 && Loadout.CarriedItems().Count == 0 && ItemDatabase.Instance != null)
                loadout.Add(ItemDatabase.Instance.starterItemId);
            Loadout.Equip(loadout);

            player = FindObjectOfType<PlayerHealth>();
            if (player != null)
            {
                player.Died += OnPlayerDied;
            }

            foreach (var totem in totems)
                if (totem != null) totem.Destroyed += OnTotemDestroyed;
            if (extraction != null) extraction.Extracted += OnExtracted;

            UpdateObjective();
            string title = definition != null ? definition.displayName + "\n" : "";
            Announcer.Show(title + "Destroy the totems", new Color(0.8f, 0.6f, 1f));
            CombatLog.Log("MISSION start " + (definition != null ? definition.name + " tier " + definition.tier : "(no definition)") + ": loadout [" + string.Join(", ", loadout)
                + "], fromHub " + Loadout.EnteredFromHub + ", enemy health x" + Difficulty.EnemyHealth.ToString("F2") + ", damage x" + Difficulty.EnemyDamage.ToString("F2"));
        }

        // ---------------------------------------------------------------- Totems and blessings

        void OnTotemDestroyed(Totem totem)
        {
            totemsDestroyed++;
            RunState.CountTotem();
            CombatLog.Log("MISSION totem destroyed " + totem.name + " (" + totemsDestroyed + "/" + totems.Length + ")");
            Sfx.Play(SfxKind.TotemBreak, totem.transform.position);
            HitSpark.Ring(totem.transform.position, new Color(0.8f, 0.5f, 1f), 1.5f, 60);
            Announcer.Show("Totem destroyed  " + totemsDestroyed + " / " + totems.Length + "\nChoose a blessing", new Color(0.8f, 0.6f, 1f));

            int choices = choicesPerTotem + Mathf.RoundToInt(PlayerStats.Bonus(StatType.BuffChoices));
            var pool = buffPool != null && buffPool.Length > 0 ? (IList<BuffDefinition>)buffPool
                : BuffCatalog.Instance != null ? BuffCatalog.Instance.buffs : null;
            // Two totems broken close together: the second offer waits for the first choice.
            StartCoroutine(OfferWhenFree(BuffCatalog.Pick(pool, choices, definition != null ? definition.tier : 0)));
            UpdateObjective();

            if (totemsDestroyed >= totems.Length) StartCoroutine(WakeBoss());
        }

        IEnumerator OfferWhenFree(List<BuffDefinition> offer)
        {
            while (UI.BuffCards.IsOpen) yield return null;
            if (!ended) UI.BuffCards.Offer(offer, OnBuffChosen);
        }

        void OnBuffChosen(BuffDefinition buff)
        {
            CombatLog.Log("MISSION buff chosen " + buff.name);
            buff.Apply(this);
            chosenBuffs.Add(buff);
            RunState.Blessings.Add(buff.displayName);
            Sfx.PlayUi(SfxKind.Blessing);
            Announcer.Show(buff.displayName + "\n" + buff.description, buff.color);
        }

        // ---------------------------------------------------------------- Boss

        IEnumerator WakeBoss()
        {
            // Give the player a moment to pick the last buff.
            yield return new WaitForSeconds(4f);
            while (UI.BuffCards.IsOpen) yield return null;
            if (ended || bossPrefab == null) yield break;

            Transform point = bossSpawnPoint != null ? bossSpawnPoint : transform;
            HitSpark.Ring(point.position, new Color(1f, 0.25f, 0.1f), 3f, 120);
            Sfx.Play(SfxKind.EnemyRoar, point.position + Vector3.up * 2f, 1f, 0.8f);
            GameObject spawned = Instantiate(bossPrefab, point.position, point.rotation);
            boss = spawned.GetComponent<EnemyHealth>();
            MusicPlayer.SetMood(MusicMood.Boss);
            CombatLog.Log("MISSION boss spawned at " + point.position);
            Announcer.Show("The guardian awakens", new Color(1f, 0.3f, 0.2f), 4f);
            UpdateObjective();
        }

        void OnEnemyDied(EnemyHealth enemy, DamageInfo info)
        {
            if (ended) return;

            RunState.CountKill();
            Vector3 at = enemy.transform.position + Vector3.up;
            if (enemy == boss && boss != null)
            {
                bossDefeated = true;
                RunState.BossKilled = true;
                CombatLog.Log("MISSION boss defeated");
                LootShard.Burst(at, bossEssence, bossCoins);
                DropRewards(enemy.transform.position);
                if (extraction != null) extraction.Fast = true;
                MusicPlayer.SetMood(MusicMood.Mission);
                Announcer.Show("LEVEL COMPLETE\nTake your reward to the portal", new Color(1f, 0.85f, 0.3f), 5f);
            }
            else
            {
                // Essence and coins spill out as shards that fly to the player.
                LootShard.Burst(at, enemy.EssenceReward, enemy.CoinReward);
            }
            UpdateObjective();
        }

        /// <summary>The guardian's spoils: one guardian-only item, rarer ones more likely on harder missions.</summary>
        void DropRewards(Vector3 position)
        {
            int tier = definition != null ? definition.tier : 0;
            var loot = ItemDatabase.All(item => item.bossLoot);
            if (loot.Count == 0)
            {
                if (bossRewards == null || bossRewards.Length == 0) return;
                GameObject reward = bossRewards[Random.Range(0, bossRewards.Length)];
                if (reward != null) Instantiate(reward, position + Vector3.up * 1.2f, Quaternion.identity);
                return;
            }

            float total = 0f;
            foreach (var item in loot) total += Weight(item, tier);
            float roll = Random.value * total;
            ItemDatabase.Item chosen = loot[loot.Count - 1];
            foreach (var item in loot)
            {
                roll -= Weight(item, tier);
                if (roll <= 0f) { chosen = item; break; }
            }

            if (chosen.kind == ItemKind.Weapon && chosen.prefab != null)
            {
                Instantiate(chosen.prefab, position + Vector3.up * 1.2f, Quaternion.identity);
                Announcer.Show("The guardian drops its " + chosen.displayName + "\nCarry it out to keep it", new Color(1f, 0.85f, 0.3f), 5f);
            }
            else
            {
                FoundGear.Add(chosen.id);
                Announcer.Show("You claim the " + chosen.displayName + "\nReach the portal to keep it", new Color(1f, 0.85f, 0.3f), 5f);
            }
            Sfx.Play(SfxKind.ChestOpen, position);
        }

        static float Weight(ItemDatabase.Item item, int tier) =>
            item.rarity == Rarity.Legendary ? 0.35f + tier * 0.4f : item.rarity == Rarity.Rare ? 1f + tier * 0.3f : 1.5f;

        // ---------------------------------------------------------------- Endings

        void OnPlayerDied() => EndInDeath("died");

        /// <summary>The player gave up from the pause menu: it counts as dying here.</summary>
        public void Abandon()
        {
            if (player != null && !player.IsDead) EndInDeath("abandoned");
        }

        void EndInDeath(string outcome)
        {
            if (ended) return;
            ended = true;

            SaveData data = SaveSystem.Data;
            int essence = RunState.Essence, coins = RunState.Coins;
            // Only what this run gave is lost: its essence and coins, gear found here, and weapons picked up here.
            // The weapons brought in and the armor and trinkets worn go back home.
            var lost = new List<string>(FoundGear);
            lost.AddRange(PickedUpHere(data.loadout));
            CombatLog.Log("MISSION " + outcome.ToUpper() + ": lost " + essence + " essence, " + coins + " coins, items [" + string.Join(", ", lost)
                + "]; kept [" + string.Join(", ", data.loadout) + "] and worn gear");

            data.lastRun = Summary(outcome, lost);
            RunState.Lose();
            FoundGear.Clear();
            data.stash.AddRange(data.loadout);
            data.loadout.Clear();
            data.inMission = false;
            data.deaths++;
            SaveSystem.Save();

            Objective = "";
            Announcer.Show((outcome == "died" ? "YOU DIED" : "RUN ABANDONED") + "\nLost this run's " + essence + " essence, " + coins + " coins and finds",
                new Color(1f, 0.2f, 0.15f), endDelay);
            StartCoroutine(ReturnToHub());
        }

        /// <summary>Weapons the player is carrying now that they did not bring into the mission.</summary>
        static List<string> PickedUpHere(List<string> brought)
        {
            var remaining = new List<string>(brought);
            var found = new List<string>();
            foreach (string id in Loadout.CarriedIds())
                if (!remaining.Remove(id)) found.Add(id);
            return found;
        }

        void OnExtracted()
        {
            if (ended) return;
            ended = true;

            SaveData data = SaveSystem.Data;
            int essence = RunState.Essence, coins = RunState.Coins;
            // Whatever is in the player's hands and holsters right now comes home, including anything found here.
            List<string> carried = Loadout.CarriedIds();
            CombatLog.Log("MISSION EXTRACTED: banking " + essence + " essence, " + coins + " coins, carried [" + string.Join(", ", carried) + "], gear [" + string.Join(", ", FoundGear) + "], boss " + bossDefeated);

            var kept = new List<string>(carried);
            kept.AddRange(FoundGear);
            data.loadout.Clear();
            data.stash.AddRange(carried);
            data.gear.AddRange(FoundGear);
            FoundGear.Clear();
            if (bossDefeated)
            {
                data.bossesKilled++;
                if (definition != null && !data.completedMissions.Contains(definition.name)) data.completedMissions.Add(definition.name);
            }
            data.inMission = false;
            data.extractions++;
            data.lastRun = Summary(bossDefeated ? "victory" : "extracted", kept);
            RunState.Bank();

            Objective = "";
            Sfx.PlayUi(SfxKind.Extract);
            Announcer.Show((bossDefeated ? "VICTORY" : "EXTRACTED") + "\nKept: " + essence + " essence, " + coins + " coins, " + kept.Count + " items",
                new Color(0.4f, 1f, 0.8f), endDelay);
            StartCoroutine(ReturnToHub());
        }

        RunSummary Summary(string outcome, List<string> items)
        {
            return new RunSummary
            {
                outcome = outcome,
                mission = definition != null ? definition.displayName : "",
                essence = RunState.Essence,
                coins = RunState.Coins,
                kills = RunState.Kills,
                totems = totemsDestroyed,
                seconds = Mathf.RoundToInt(Time.time - RunState.StartTime),
                bossKilled = bossDefeated,
                items = items,
                blessings = new List<string>(RunState.Blessings),
            };
        }

        IEnumerator ReturnToHub()
        {
            yield return new WaitForSeconds(endDelay);
            SceneFlow.Load(SceneFlow.HubScene);
        }

        void UpdateObjective()
        {
            if (ended) return;
            if (bossDefeated) Objective = "Reach the portal to leave with your loot";
            else if (boss != null) Objective = "Defeat the guardian";
            else if (totemsDestroyed >= totems.Length) Objective = "Something stirs...";
            else Objective = "Destroy the totems  " + totemsDestroyed + " / " + totems.Length + "   (or leave early through the portal)";
        }

        float nextHud;
        int hudSignature;

        /// <summary>Objective and run loot on the monitor's HUD. The headset shows the same on the wrist display.</summary>
        void LateUpdate()
        {
            UI.DesktopHud hud = UI.DesktopHud.Get();
            if (hud == null || Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + 0.1f;

            // Rebuild the panel text only when something on it changed.
            string objective = string.IsNullOrEmpty(KillChallenge.ActiveText) ? Objective : KillChallenge.ActiveText;
            int signature = (objective != null ? objective.GetHashCode() : 0) ^ (RunState.Essence * 73856093) ^ (RunState.Coins * 19349663)
                            ^ (RunState.Kills * 83492791) ^ (chosenBuffs.Count << 24);
            if (signature == hudSignature) return;
            hudSignature = signature;

            var body = new System.Text.StringBuilder(160);
            body.Append(objective).Append('\n');
            body.Append("<color=#9C958A>Essence</color> <color=#FFD25A>").Append(RunState.Essence)
                .Append("</color>    <color=#9C958A>Coins</color> <color=#FFD25A>").Append(RunState.Coins)
                .Append("</color>    <color=#9C958A>Kills</color> <color=#FFE9A8>").Append(RunState.Kills).Append("</color>");
            for (int i = 0; i < chosenBuffs.Count; i++)
                body.Append("\n<color=#D4A82A>+</color> ").Append(chosenBuffs[i].displayName);
            hud.SetObjective(definition != null ? definition.displayName : "Mission", body.ToString());
        }
    }
}
