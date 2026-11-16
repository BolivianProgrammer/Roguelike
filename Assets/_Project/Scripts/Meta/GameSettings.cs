using System;
using System.IO;
using UnityEngine;

namespace Roguelike.Meta
{
    /// <summary>The player's options. Kept apart from the progress save, so a new game keeps them.</summary>
    [Serializable]
    public class SettingsData
    {
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.6f;

        /// <summary>Darkens the edges of the view while moving with the stick, which eases motion sickness.</summary>
        public bool comfortVignette = true;
        /// <summary>True for left-handed players: the wrist display goes on the right arm.</summary>
        public bool leftHanded;
        /// <summary>Raises the view for players sitting down, in meters.</summary>
        public float seatedHeightOffset;

        /// <summary>Desktop only: degrees of turn per pixel of mouse movement.</summary>
        public float mouseSensitivity = 0.12f;
        /// <summary>Desktop only: shows the list of keys in the corner.</summary>
        public bool showKeyHints = true;
    }

    /// <summary>Loads and saves <see cref="SettingsData"/> as settings.json next to the save file.</summary>
    public static class GameSettings
    {
        const string FileName = "settings.json";
        static SettingsData data;

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Raised after any setting is saved, so the things that use them can update at once.</summary>
        public static event Action Changed;

        public static SettingsData Current
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
                if (File.Exists(FilePath)) data = JsonUtility.FromJson<SettingsData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] Could not read the settings, using defaults. " + e.Message);
            }
            if (data == null) data = new SettingsData();
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] Could not write the settings. " + e.Message);
            }
            Changed?.Invoke();
        }
    }
}
