using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Meta
{
    /// <summary>Every mission in the game, in the order the mission board lists them. Lives at Resources/MissionCatalog.</summary>
    [CreateAssetMenu(menuName = "Roguelike/Mission Catalog", fileName = "MissionCatalog")]
    public class MissionCatalog : ScriptableObject
    {
        public MissionDefinition[] missions;

        static MissionCatalog instance;

        public static MissionCatalog Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<MissionCatalog>("MissionCatalog");
                return instance;
            }
        }

        public static MissionDefinition Find(string id)
        {
            var catalog = Instance;
            if (catalog == null || catalog.missions == null) return null;
            foreach (var mission in catalog.missions)
                if (mission != null && mission.name == id) return mission;
            return null;
        }

        /// <summary>The missions the player can pick right now.</summary>
        public static List<MissionDefinition> Unlocked(SaveData data)
        {
            var list = new List<MissionDefinition>();
            var catalog = Instance;
            if (catalog == null || catalog.missions == null) return list;
            foreach (var mission in catalog.missions)
                if (mission != null && mission.IsUnlocked(data)) list.Add(mission);
            return list;
        }

        /// <summary>The mission the gate leads to: the player's pick, or the first one open.</summary>
        public static MissionDefinition Selected(SaveData data)
        {
            var picked = Find(data.selectedMission);
            if (picked != null && picked.IsUnlocked(data)) return picked;
            var open = Unlocked(data);
            return open.Count > 0 ? open[0] : null;
        }

        /// <summary>The mission being played right now, set when the gate is entered.</summary>
        public static MissionDefinition Current { get; set; }
    }
}
