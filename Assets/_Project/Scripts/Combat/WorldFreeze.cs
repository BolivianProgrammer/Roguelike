using System.Collections.Generic;

namespace Roguelike.Combat
{
    /// <summary>
    /// Stops the dangerous parts of the world (enemies, their weapons, bolts, bleeding) while the player makes a
    /// choice, such as picking a blessing card. Unlike pausing the game (Time.timeScale = 0) the player's own
    /// physics hands keep working, which matters in the headset where you point with them.
    /// Anything can hold the freeze; the world moves again once every holder has let go.
    /// </summary>
    public static class WorldFreeze
    {
        static readonly HashSet<object> holders = new HashSet<object>();

        /// <summary>True while anything is holding the world still.</summary>
        public static bool Active => holders.Count > 0;

        public static void Hold(object holder)
        {
            if (holder != null) holders.Add(holder);
        }

        public static void Release(object holder)
        {
            if (holder != null) holders.Remove(holder);
        }

        /// <summary>Lets go of everything, e.g. when a scene changes.</summary>
        public static void Clear() => holders.Clear();
    }
}
