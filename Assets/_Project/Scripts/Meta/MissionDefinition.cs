using UnityEngine;

namespace Roguelike.Meta
{
    /// <summary>
    /// One mission the player can pick at the mission board: which scene it plays in and how hard it is.
    /// Several missions can share a scene at different tiers. Create with Create > Roguelike > Mission and add it
    /// to the Mission Catalog in Resources. See MISSIONS.md for what a mission scene needs.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Mission", fileName = "NewMission")]
    public class MissionDefinition : ScriptableObject
    {
        public string displayName = "Mission";
        [TextArea] public string description = "";
        [Tooltip("The scene to load. It must be in File > Build Settings.")]
        public string sceneName = "Mission_Forest";
        [Tooltip("0 is the easiest. Each tier makes enemies tougher and loot richer.")]
        [Min(0)] public int tier;
        [Tooltip("This mission only appears once that one has been completed (its guardian killed). Empty = always open.")]
        public MissionDefinition unlockedBy;
        [Tooltip("Seconds for the timed kill challenges in this mission.")]
        public float challengeTime = 45f;

        public bool IsUnlocked(SaveData data) => unlockedBy == null || data.completedMissions.Contains(unlockedBy.name);
    }
}
