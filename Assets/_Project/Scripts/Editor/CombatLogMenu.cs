using UnityEditor;
using Roguelike.Combat;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Roguelike > Combat Log turns the combat measurement log (<see cref="CombatLog"/>) on or off while playing in
    /// the editor: every attack window, hit (and the reason a contact did not count as a hit), block, parry, cut and
    /// mission event is printed to the Console with its frame and time. Used to tune weapon and enemy numbers.
    /// Off by default; a checkmark shows its state.
    /// </summary>
    public static class CombatLogMenu
    {
        const string MenuPath = "Roguelike/Combat Log";

        [MenuItem(MenuPath)]
        static void Toggle() => CombatLog.Enabled = !CombatLog.Enabled;

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(MenuPath, CombatLog.Enabled);
            return true;
        }
    }
}
