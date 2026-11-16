using UnityEngine;

namespace Roguelike.Gear
{
    /// <summary>
    /// A small worn charm with a passive effect. Effects go through the shared stat system, including the ones
    /// that are more than plain numbers: lifesteal, thorns, extra blessing choices, more loot, faster specials.
    /// Create with Create > Roguelike > Trinket, then add it to the Item Database.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Trinket", fileName = "NewTrinket")]
    public class TrinketDefinition : ScriptableObject
    {
        public string displayName = "Trinket";
        [TextArea] public string description = "";
        public Color color = new Color(0.9f, 0.8f, 0.4f);
        public StatEffect[] effects;

        public string Summary => StatEffect.Describe(effects);
    }
}
