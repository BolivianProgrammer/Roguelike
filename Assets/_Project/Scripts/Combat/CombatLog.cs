using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Measurement log for playtesting: attack windows, hits, blocks, cuts, mission and save events.
    /// Off by default, so it costs nothing in normal play. Turn it on from the editor menu Roguelike > Combat Log
    /// and every line appears in the Console starting with "[TEST]", stamped with the frame number and game time
    /// so timings can be compared.
    /// </summary>
    public static class CombatLog
    {
        public const string Prefix = "[TEST]";

        /// <summary>Master switch, toggled from Roguelike > Combat Log in the editor. False in builds.</summary>
        public static bool Enabled;

        public static void Log(string message)
        {
            if (!Enabled) return;
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0} f{1} t={2:F3} {3}",
                Prefix, Time.frameCount, Time.time, message);
        }

        /// <summary>A short, readable name for a collider: "Head (Head)", "LeftArm (LeftLowerArm)".</summary>
        public static string Where(Collider collider)
        {
            if (collider == null) return "nothing";
            var zone = collider.GetComponent<HitZone>();
            string bone = collider.name.Replace("mixamorig:", "");
            return zone != null ? zone.part + " (" + bone + ")" : bone;
        }
    }
}
