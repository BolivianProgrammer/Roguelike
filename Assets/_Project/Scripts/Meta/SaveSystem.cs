using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Roguelike.Meta
{
    /// <summary>Everything that survives between sessions.</summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Currency for levelling up, safely banked.</summary>
        public int essence;
        /// <summary>Currency for vendors, safely banked.</summary>
        public int coins;
        /// <summary>Character level. Each level adds a little damage and health.</summary>
        public int level;
        /// <summary>Weapons kept safe in the hub, by item id.</summary>
        public List<string> stash = new List<string>();
        /// <summary>Weapons taken into the current mission. Lost if the player dies there.</summary>
        public List<string> loadout = new List<string>();

        /// <summary>Armor and trinkets owned but not worn: safe in the hub.</summary>
        public List<string> gear = new List<string>();
        /// <summary>The armor worn, by item id, or empty. Worn gear goes on missions and always comes back.</summary>
        public string armor = "";
        /// <summary>The trinkets worn, by item id.</summary>
        public List<string> trinkets = new List<string>();

        /// <summary>
        /// True from the moment the player steps through the gate until the mission ends in death or extraction.
        /// If the game is closed in between, the next visit to the hub treats the run as a death.
        /// </summary>
        public bool inMission;
        /// <summary>The mission the gate leads to, by id.</summary>
        public string selectedMission = "";
        /// <summary>Missions finished by defeating their guardian, by id. They unlock harder ones.</summary>
        public List<string> completedMissions = new List<string>();

        /// <summary>Changes every time the player returns to the hub, so the vendor's stock rotates.</summary>
        public int vendorRotation;

        /// <summary>The last run's outcome, shown when the player is back in the hub. Empty once shown.</summary>
        public RunSummary lastRun = new RunSummary();

        // Lifetime numbers, for the status display.
        public int runs, extractions, deaths, bossesKilled;
    }

    /// <summary>What happened on one run, for the death and victory screens.</summary>
    [Serializable]
    public class RunSummary
    {
        /// <summary>"extracted", "victory", "died", "abandoned", or empty for no summary to show.</summary>
        public string outcome = "";
        public string mission = "";
        public int essence, coins, kills, totems, seconds;
        public bool bossKilled;
        /// <summary>Items brought home, or lost.</summary>
        public List<string> items = new List<string>();
        public List<string> blessings = new List<string>();
    }

    /// <summary>
    /// Loads and saves the player's progress as a small JSON file in the game's own data folder.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";

        static SaveData data;

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>True if a saved game exists on disk.</summary>
        public static bool Exists => File.Exists(FilePath);

        /// <summary>The current progress. Loaded from disk the first time it is asked for.</summary>
        public static SaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static void Load()
        {
            data = null;
            try
            {
                if (File.Exists(FilePath))
                    data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not read the save file, starting fresh. " + e.Message);
            }

            if (data == null) data = new SaveData();
            // Saves from older versions of the game lack the newer lists.
            if (data.stash == null) data.stash = new List<string>();
            if (data.loadout == null) data.loadout = new List<string>();
            if (data.gear == null) data.gear = new List<string>();
            if (data.trinkets == null) data.trinkets = new List<string>();
            if (data.completedMissions == null) data.completedMissions = new List<string>();
            if (data.armor == null) data.armor = "";
            if (data.selectedMission == null) data.selectedMission = "";
            if (data.lastRun == null) data.lastRun = new RunSummary();
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
                Combat.CombatLog.Log("SAVE written: essence " + data.essence + ", coins " + data.coins + ", level " + data.level
                    + ", stash [" + string.Join(", ", data.stash) + "], loadout [" + string.Join(", ", data.loadout) + "], armor " + data.armor
                    + ", trinkets [" + string.Join(", ", data.trinkets) + "], gear [" + string.Join(", ", data.gear) + "], in mission " + data.inMission);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not write the save file. " + e.Message);
            }
        }

        /// <summary>Wipes all progress and starts over.</summary>
        public static void NewGame()
        {
            data = new SaveData();
            Save();
        }
    }
}
