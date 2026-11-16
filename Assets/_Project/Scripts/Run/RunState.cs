using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// What the player has gathered during a mission. Dying wipes it; only extracting or finishing the level
    /// banks it. (Weapons brought in and worn gear are not part of this and are never lost.)
    /// </summary>
    public static class RunState
    {
        /// <summary>Currency for levelling up the character, gathered this run.</summary>
        public static int Essence { get; private set; }
        /// <summary>Currency for vendors, gathered this run.</summary>
        public static int Coins { get; private set; }
        public static int Kills { get; private set; }
        public static int Totems { get; private set; }
        public static bool BossKilled { get; set; }
        public static float StartTime { get; private set; }
        /// <summary>Names of the blessings taken this run.</summary>
        public static readonly List<string> Blessings = new List<string>();

        public static event Action Changed;

        /// <summary>
        /// Adds loot found on the run. Trinkets and armor that boost essence or coins, and the mission's
        /// difficulty, scale it up.
        /// </summary>
        public static void Add(int essence, int coins)
        {
            Essence += Mathf.RoundToInt(essence * PlayerStats.Get(StatType.EssenceGain) * Enemies.Difficulty.Loot);
            Coins += Mathf.RoundToInt(coins * PlayerStats.Get(StatType.CoinGain) * Enemies.Difficulty.Loot);
            Changed?.Invoke();
        }

        public static void CountKill()
        {
            Kills++;
            Changed?.Invoke();
        }

        public static void CountTotem()
        {
            Totems++;
            Changed?.Invoke();
        }

        /// <summary>Starts a fresh run with empty pockets.</summary>
        public static void Begin()
        {
            Essence = 0;
            Coins = 0;
            Kills = 0;
            Totems = 0;
            BossKilled = false;
            Blessings.Clear();
            StartTime = Time.time;
            Changed?.Invoke();
        }

        /// <summary>The player died: everything carried is gone.</summary>
        public static void Lose()
        {
            Essence = 0;
            Coins = 0;
            Changed?.Invoke();
        }

        /// <summary>The player made it out: what they gathered is added to their saved totals.</summary>
        public static void Bank()
        {
            var data = Meta.SaveSystem.Data;
            data.essence += Essence;
            data.coins += Coins;
            Meta.SaveSystem.Save();
        }
    }
}
