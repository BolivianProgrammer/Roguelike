using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Roguelike.Audio;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Use Sounds From Folders: the recordings in Audio/Real/[SfxKind]/ (and Audio/Real/Music/[Mood]/) become that
    /// sound's clips in Resources/SoundLibrary. A sound with no clips falls back to the one Sfx makes in code.
    /// </summary>
    public static class SoundBaker
    {
        const string RealFolder = "Assets/_Project/Audio/Real";
        const string LibraryPath = "Assets/_Project/Resources/SoundLibrary.asset";

        [MenuItem("Roguelike/Audio/Use Sounds From Folders")]
        public static void UseRealSoundsMenu() => UseRealSounds(true);

        /// <summary>Any clips in Audio/Real/[SfxKind]/ replace that sound's placeholders; Audio/Real/Music/[Mood]/ sets the music.</summary>
        static void UseRealSounds(bool log)
        {
            foreach (SfxKind kind in Enum.GetValues(typeof(SfxKind)))
                Directory.CreateDirectory(RealFolder + "/" + kind);
            foreach (MusicMood mood in Enum.GetValues(typeof(MusicMood)))
                if (mood != MusicMood.None) Directory.CreateDirectory(RealFolder + "/Music/" + mood);
            AssetDatabase.Refresh();

            var library = Library();
            var effects = new List<SoundLibrary.Entry>(library.effects ?? new SoundLibrary.Entry[0]);
            int replaced = 0;
            foreach (SfxKind kind in Enum.GetValues(typeof(SfxKind)))
            {
                var clips = ClipsIn(RealFolder + "/" + kind, false);
                if (clips.Count == 0) continue;
                // Keep a volume already tuned in the Inspector; otherwise start from the default for this kind.
                SoundLibrary.Entry old = effects.Find(e => e.kind == kind);
                float volume = old != null && old.clips != null && old.clips.Length > 0 && IsReal(old.clips[0]) ? old.volume : DefaultVolume(kind);
                effects.RemoveAll(e => e.kind == kind);
                effects.Add(new SoundLibrary.Entry { kind = kind, clips = clips.ToArray(), volume = volume });
                replaced++;
            }
            library.effects = effects.ToArray();

            var music = new List<SoundLibrary.MusicEntry>();
            foreach (MusicMood mood in Enum.GetValues(typeof(MusicMood)))
            {
                if (mood == MusicMood.None) continue;
                var clips = ClipsIn(RealFolder + "/Music/" + mood, true);
                if (clips.Count > 0) music.Add(new SoundLibrary.MusicEntry { mood = mood, clip = clips[0], volume = 0.5f });
            }
            library.music = music.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log("[SoundBaker] Real recordings now used for " + replaced + " sound(s) and " + music.Count + " music mood(s). Folders: " + RealFolder);
        }

        static bool IsReal(AudioClip clip) => clip != null && AssetDatabase.GetAssetPath(clip).StartsWith(RealFolder);

        /// <summary>Starting volumes: sounds that repeat a lot in a fight sit lower so they never drown the hits.</summary>
        static float DefaultVolume(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.BoltCharge: return 0.55f;
                case SfxKind.BoltRelease: return 0.7f;
                case SfxKind.Footstep: return 0.6f;
                default: return 1f;
            }
        }

        static List<AudioClip> ClipsIn(string folder, bool music)
        {
            var clips = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureImport(path, music);
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            return clips;
        }

        /// <summary>Quest-friendly import: short effects decompressed in memory, music streamed from disk.</summary>
        static void ConfigureImport(string path, bool music)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            importer.forceToMono = !music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.6f : 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        static SoundLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library != null) return library;
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            return library;
        }
    }
}
