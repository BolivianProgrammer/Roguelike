using System;
using UnityEngine;

namespace Roguelike.Audio
{
    /// <summary>Every kind of sound effect the game plays.</summary>
    public enum SfxKind
    {
        Whoosh, WhooshHeavy, HitSlash, HitStab, HitBlunt, Block, Parry, Deflect, Sever, Execution,
        EnemyAttack, EnemyHurt, EnemyDeath, EnemyRoar, Footstep, PlayerHurt, PlayerDeath,
        UiHover, UiClick, UiBack, TotemBreak, Blessing, PortalOpen, Extract, Pickup, Coins, ChestOpen,
        BoltCharge, BoltRelease, Special, LevelUp, ChallengeStart, ChallengeEnd
    }

    /// <summary>Which background music plays.</summary>
    public enum MusicMood { None, Menu, Hub, Mission, Boss }

    /// <summary>
    /// Real recordings to use instead of the made-up placeholder sounds. Create one at
    /// Resources/SoundLibrary (Create > Roguelike > Sound Library) and fill in only the sounds you have;
    /// anything left empty keeps its placeholder.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxKind kind;
            [Tooltip("One is picked at random each time, so repeats do not sound identical.")]
            public AudioClip[] clips;
            [Range(0f, 2f)] public float volume = 1f;
        }

        [Serializable]
        public class MusicEntry
        {
            public MusicMood mood;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.5f;
        }

        public Entry[] effects;
        public MusicEntry[] music;

        static SoundLibrary instance;
        static bool looked;

        /// <summary>The library in Resources, or null if there is none.</summary>
        public static SoundLibrary Instance
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    instance = Resources.Load<SoundLibrary>("SoundLibrary");
                }
                return instance;
            }
        }

        public Entry Find(SfxKind kind)
        {
            if (effects == null) return null;
            foreach (var entry in effects)
                if (entry != null && entry.kind == kind && entry.clips != null && entry.clips.Length > 0) return entry;
            return null;
        }

        public MusicEntry Find(MusicMood mood)
        {
            if (music == null) return null;
            foreach (var entry in music)
                if (entry != null && entry.mood == mood && entry.clip != null) return entry;
            return null;
        }
    }
}
