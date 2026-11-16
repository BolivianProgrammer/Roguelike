using UnityEngine;
using Roguelike.Gear;
using Roguelike.Meta;

namespace Roguelike.Run
{
    /// <summary>
    /// One blessing a destroyed totem can offer for the rest of the run. Create more with
    /// Create > Roguelike > Buff Definition; a blessing is a name, a description and a list of stat changes,
    /// which can include lifesteal, thorns, more loot and the like. Rarer blessings are offered less often
    /// and are stronger.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Buff Definition", fileName = "NewBuff")]
    public class BuffDefinition : ScriptableObject
    {
        public string displayName = "Buff";
        [TextArea] public string description = "";
        public Color color = Color.white;
        public Rarity rarity = Rarity.Common;
        public StatEffect[] effects;

        /// <summary>How likely this blessing is to be offered, relative to a common one.</summary>
        public float Weight => rarity == Rarity.Legendary ? 0.15f : rarity == Rarity.Rare ? 0.45f : 1f;

        /// <summary>Grants this buff to the player until the given source's bonuses are removed.</summary>
        public void Apply(object source) => StatEffect.ApplyAll(effects, source);
    }
}
