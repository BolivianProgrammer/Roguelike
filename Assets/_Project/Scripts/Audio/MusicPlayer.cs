using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Meta;

namespace Roguelike.Audio
{
    /// <summary>
    /// Background music and ambience. Picks a mood from the scene (menu, hub, mission) and cross-fades when it
    /// changes; the mission switches to the boss mood when the guardian wakes. Uses clips from the
    /// <see cref="SoundLibrary"/> if any, and slow made-up drones otherwise. Created automatically.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float FadeTime = 2f;

        static MusicPlayer instance;

        readonly Dictionary<MusicMood, AudioClip> placeholders = new Dictionary<MusicMood, AudioClip>();
        AudioSource[] sources;
        int active;
        MusicMood mood = MusicMood.None;
        float moodVolume = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
            instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        /// <summary>Changes the music, e.g. to the boss theme.</summary>
        public static void SetMood(MusicMood next)
        {
            if (instance != null) instance.Switch(next);
        }

        void Awake()
        {
            sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].loop = true;
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].volume = 0f;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (scene.name == SceneFlow.MenuScene) Switch(MusicMood.Menu);
            else if (scene.name == SceneFlow.HubScene) Switch(MusicMood.Hub);
            else if (FindObjectOfType<Run.MissionManager>() != null) Switch(MusicMood.Mission);
            else Switch(MusicMood.None);
        }

        void Switch(MusicMood next)
        {
            if (next == mood) return;
            mood = next;
            if (next == MusicMood.None)
            {
                active = 1 - active;
                sources[active].Stop();
                return;
            }

            var library = SoundLibrary.Instance;
            var entry = library != null ? library.Find(next) : null;
            AudioClip clip = entry != null ? entry.clip : Placeholder(next);
            moodVolume = entry != null ? entry.volume : 0.35f;

            active = 1 - active;
            sources[active].clip = clip;
            sources[active].volume = 0f;
            levels[active] = 0f;
            sources[active].Play();
        }

        /// <summary>Each source's cross-faded level, before the duck is applied on top.</summary>
        readonly float[] levels = new float[2];

        void Update()
        {
            SettingsData settings = GameSettings.Current;
            float goal = moodVolume * settings.musicVolume * settings.masterVolume;
            float step = Time.unscaledDeltaTime / FadeTime;
            // The music steps back while a big moment (totem, roar, death) plays. Applied on top of the slow
            // cross-fade so the dip is as quick as the sound that causes it.
            float duck = Sfx.Duck;
            for (int i = 0; i < sources.Length; i++)
            {
                float target = i == active && mood != MusicMood.None ? goal : 0f;
                levels[i] = Mathf.MoveTowards(levels[i], target, step * Mathf.Max(goal, 0.05f));
                sources[i].volume = levels[i] * duck;
                if (i != active && sources[i].isPlaying && levels[i] <= 0.001f) sources[i].Stop();
            }
        }

        AudioClip Placeholder(MusicMood which)
        {
            if (placeholders.TryGetValue(which, out AudioClip clip)) return clip;
            switch (which)
            {
                // Low, hollow and slow for the title; warmer for the plaza; tense with a pulse for missions; a
                // faster, darker pulse for the boss.
                case MusicMood.Menu: clip = SoundSynth.Drone("Music Menu", 16f, new[] { 55f, 82.4f, 130.8f }, 0.35f, 0f, 1); break;
                case MusicMood.Hub: clip = SoundSynth.Drone("Music Hub", 16f, new[] { 110f, 138.6f, 164.8f, 220f }, 0.15f, 0f, 2); break;
                case MusicMood.Mission: clip = SoundSynth.Drone("Music Mission", 12f, new[] { 73.4f, 87.3f, 110f }, 0.3f, 1f, 3); break;
                default: clip = SoundSynth.Drone("Music Boss", 12f, new[] { 61.7f, 65.4f, 92.5f }, 0.3f, 2f, 4); break;
            }
            placeholders[which] = clip;
            return clip;
        }
    }
}
