using UnityEngine;

namespace Roguelike.Gear
{
    public enum ArmorWeight { Light, Medium, Heavy }

    /// <summary>
    /// A set of armor. It changes how the player fights through stat changes only: heavy sets add health and
    /// toughness but slow you down and make swings cost more; light sets do the opposite.
    /// Create with Create > Roguelike > Armor, then add it to the Item Database.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Armor", fileName = "NewArmor")]
    public class ArmorDefinition : ScriptableObject
    {
        public string displayName = "Armor";
        public ArmorWeight weight = ArmorWeight.Medium;
        [TextArea] public string description = "";
        [Tooltip("Tint for the gauntlets and menus.")]
        public Color color = Color.gray;
        public StatEffect[] effects;

        public string Summary => weight + " armor: " + StatEffect.Describe(effects);
    }
}
