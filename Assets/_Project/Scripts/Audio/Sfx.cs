using System.Collections.Generic;
using UnityEngine;
using Roguelike.Meta;

namespace Roguelike.Audio
{
    /// <summary>
    /// Plays sound effects: <c>Sfx.Play(SfxKind.HitSlash, point)</c>. Sounds in the world come from where they
    /// happen (3D, so you hear which side a blow came from in the headset); menu sounds play flat.
    /// Uses recordings from the <see cref="SoundLibrary"/> when there are some, and made-up placeholders otherwise.
    ///
    /// Everything goes through one mix (<see cref="SfxMix"/>) so a fight stays readable instead of saturating:
    /// - each kind of sound has its own level, a cap on how many play at once, a minimum gap between repeats and a
    ///   hearing range (an enemy's footsteps across the clearing are simply not played);
    /// - the whole game has a voice budget: when it is full, an important sound (your own hurt, a parry, a block)
    ///   takes the place of the least important one playing (a footstep, a whoosh), and an unimportant new sound is
    ///   just dropped;
    /// - big moments (a totem breaking, the guardian's roar, an execution, your death) duck the music and the
    ///   small sounds for a moment so they cut through.
    /// A fixed pool of audio sources is reused, so playing sounds allocates nothing.
    /// </summary>
    public static class Sfx
    {
        const int PoolSize = 20;
        /// <summary>World sounds allowed at once across the game (menu sounds are not counted).</summary>
        const int VoiceBudget = 12;
        const int Variants = 3;
        const float DuckAttack = 0.08f, DuckRelease = 0.6f;

        static readonly Dictionary<SfxKind, AudioClip[]> placeholders = new Dictionary<SfxKind, AudioClip[]>();
        static AudioSource[] pool;
        static SfxKind[] playingKind;
        static int[] playingPriority;
        static float[] startedAt;
        static int next;
        static readonly float[] lastPlayed = new float[64];
        static readonly int[] lastClip = new int[64];
        static Transform listener;
        static float duckDepth, duckStart, duckEnd = -10f;

        /// <summary>
        /// 1 normally; lower for a moment after a big sound, so it stands out. The music player follows it too.
        /// </summary>
        public static float Duck
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= duckEnd + DuckRelease) return 1f;
                float amount = now < duckStart + DuckAttack ? (now - duckStart) / DuckAttack
                             : now <= duckEnd ? 1f
                             : 1f - (now - duckEnd) / DuckRelease;
                return 1f - duckDepth * Mathf.Clamp01(amount);
            }
        }

        /// <summary>Plays a sound at a point in the world. Volume 1 is normal; pitch is varied slightly each time.</summary>
        public static void Play(SfxKind kind, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayInternal(kind, position, true, volume, pitch);
        }

        /// <summary>Plays a sound with no position, e.g. for menus.</summary>
        public static void PlayUi(SfxKind kind, float volume = 1f)
        {
            PlayInternal(kind, Vector3.zero, false, volume, 1f);
        }

        /// <summary>Starts (or deepens) a duck of the music and the minor sounds.</summary>
        public static void DuckFor(float depth, float seconds)
        {
            float now = Time.unscaledTime;
            bool active = now < duckEnd + DuckRelease;
            duckDepth = active ? Mathf.Max(duckDepth, depth) : depth;
            if (!active) duckStart = now;
            duckEnd = Mathf.Max(duckEnd, now + Mathf.Clamp(seconds, 0.3f, 2.5f));
        }

        static void PlayInternal(SfxKind kind, Vector3 position, bool spatial, float volume, float pitch)
        {
            SfxMix.Rule rule = SfxMix.For(kind);
            float now = Time.unscaledTime;

            // Too soon after the same sound: a flurry of identical sounds is just louder noise.
            int slot = (int)kind;
            if (slot < lastPlayed.Length && now - lastPlayed[slot] < rule.minGap) return;

            // Out of earshot: not worth a voice. (Menu kinds have no range; placed in the world they carry 25 m.)
            float range = rule.range > 0f ? rule.range : 25f;
            if (spatial)
            {
                if (listener == null && Camera.main != null) listener = Camera.main.transform;
                if (listener != null && (listener.position - position).sqrMagnitude > range * range) return;
            }

            EnsurePool();
            int index = spatial ? FindVoice(kind, rule) : FreeOrOldest();
            if (index < 0) return;
            if (slot < lastPlayed.Length) lastPlayed[slot] = now;

            AudioClip clip;
            float entryVolume = 1f;
            var library = SoundLibrary.Instance;
            var entry = library != null ? library.Find(kind) : null;
            if (entry != null)
            {
                int pick = Random.Range(0, entry.clips.Length);
                // Never the same recording twice in a row.
                if (entry.clips.Length > 1 && slot < lastClip.Length && pick == lastClip[slot]) pick = (pick + 1) % entry.clips.Length;
                if (slot < lastClip.Length) lastClip[slot] = pick;
                clip = entry.clips[pick];
                entryVolume = entry.volume;
            }
            else
            {
                var options = Placeholder(kind);
                clip = options[Random.Range(0, options.Length)];
            }
            if (clip == null) return;

            // A big moment ducks the others; it, and anything as important, plays at full level.
            if (rule.duck > 0f) DuckFor(rule.duck, clip.length * 0.6f);
            float duck = rule.priority >= SfxMix.DuckImmune ? 1f : Duck;

            AudioSource source = pool[index];
            source.Stop();
            source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f;
            source.maxDistance = Mathf.Max(range, 2f);
            // Menu sounds still play while the game is paused (the pause silences everything else).
            source.ignoreListenerPause = !spatial;
            source.clip = clip;
            float spread = PitchSpread(kind);
            source.pitch = pitch * Random.Range(1f - spread, 1f + spread);
            SettingsData settings = GameSettings.Current;
            source.volume = Mathf.Clamp01(volume * rule.volume * entryVolume * duck * settings.sfxVolume * settings.masterVolume);
            source.Play();
            playingKind[index] = kind;
            playingPriority[index] = spatial ? rule.priority : int.MaxValue;
            startedAt[index] = now;
        }

        /// <summary>
        /// A source for a world sound, or -1 to skip it. Respects the kind's own cap (the oldest of that kind gives
        /// way to the newest, or the new one is dropped) and the game-wide budget (the least important sound
        /// playing gives way to a more important one; an unimportant sound arriving at a full mix is dropped).
        /// </summary>
        static int FindVoice(SfxKind kind, SfxMix.Rule rule)
        {
            int sameKind = 0, oldestSame = -1, playing = 0, weakest = -1, free = -1;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!pool[i].isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (playingPriority[i] == int.MaxValue) continue; // menu sounds sit outside the budget
                playing++;
                if (playingKind[i] == kind)
                {
                    sameKind++;
                    if (oldestSame < 0 || startedAt[i] < startedAt[oldestSame]) oldestSame = i;
                }
                if (weakest < 0 || playingPriority[i] < playingPriority[weakest]
                    || (playingPriority[i] == playingPriority[weakest] && startedAt[i] < startedAt[weakest])) weakest = i;
            }

            if (sameKind >= rule.maxVoices) return rule.steal ? oldestSame : -1;
            if (playing < VoiceBudget && free >= 0) return free;
            return weakest >= 0 && playingPriority[weakest] < rule.priority ? weakest : -1;
        }

        static int FreeOrOldest()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                int index = (next + i) % PoolSize;
                if (pool[index].isPlaying) continue;
                next = (index + 1) % PoolSize;
                return index;
            }
            int oldest = 0;
            for (int i = 1; i < PoolSize; i++)
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            return oldest;
        }

        static void EnsurePool()
        {
            if (pool != null && pool[0] != null) return;
            var root = new GameObject("Sound Effects");
            Object.DontDestroyOnLoad(root);
            pool = new AudioSource[PoolSize];
            playingKind = new SfxKind[PoolSize];
            playingPriority = new int[PoolSize];
            startedAt = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Sfx " + i);
                go.transform.SetParent(root.transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                // Natural fall-off: full level within 1.5 m, then fading with distance out to the kind's range.
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1.5f;
                source.maxDistance = 25f;
                source.dopplerLevel = 0f;
                pool[i] = source;
            }
        }

        /// <summary>
        /// How much each play's pitch may wander. Sounds heard dozens of times a fight (swings, hits, steps) wander
        /// the most so no two in a row match; menu sounds stay steady.
        /// </summary>
        static float PitchSpread(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.Whoosh:
                case SfxKind.WhooshHeavy:
                case SfxKind.Footstep:
                    return 0.15f;
                case SfxKind.HitSlash:
                case SfxKind.HitStab:
                case SfxKind.HitBlunt:
                case SfxKind.Block:
                case SfxKind.Parry:
                case SfxKind.Deflect:
                case SfxKind.Sever:
                case SfxKind.EnemyHurt:
                case SfxKind.EnemyAttack:
                    return 0.1f;
                case SfxKind.UiHover:
                case SfxKind.UiClick:
                case SfxKind.UiBack:
                    return 0.03f;
                default:
                    return 0.07f;
            }
        }

        /// <summary>The made-up sounds, created the first time each kind is needed.</summary>
        static AudioClip[] Placeholder(SfxKind kind)
        {
            if (placeholders.TryGetValue(kind, out AudioClip[] clips)) return clips;
            clips = new AudioClip[Variants];
            for (int v = 0; v < Variants; v++)
            {
                int seed = (int)kind * 97 + v * 13;
                string name = kind + " " + v;
                float jitter = 1f + (v - 1) * 0.06f;
                switch (kind)
                {
                    case SfxKind.Whoosh: clips[v] = SoundSynth.Whoosh(name, 0.28f, 1.3f * jitter, seed); break;
                    case SfxKind.WhooshHeavy: clips[v] = SoundSynth.Whoosh(name, 0.45f, 0.7f * jitter, seed); break;
                    case SfxKind.HitSlash: clips[v] = SoundSynth.Splat(name, 0.22f, seed); break;
                    case SfxKind.HitStab: clips[v] = SoundSynth.Thump(name, 0.2f, 160f * jitter, 90f, 1.2f, seed); break;
                    case SfxKind.HitBlunt: clips[v] = SoundSynth.Thump(name, 0.3f, 110f * jitter, 45f, 0.6f, seed); break;
                    case SfxKind.Block: clips[v] = SoundSynth.Clang(name, 0.45f, 310f * jitter, 0.7f, seed); break;
                    case SfxKind.Parry: clips[v] = SoundSynth.Clang(name, 0.9f, 520f * jitter, 1.6f, seed); break;
                    case SfxKind.Deflect: clips[v] = SoundSynth.Clang(name, 0.6f, 760f * jitter, 1.2f, seed); break;
                    case SfxKind.Sever: clips[v] = SoundSynth.Splat(name, 0.5f, seed + 5); break;
                    case SfxKind.Execution: clips[v] = SoundSynth.Thump(name, 0.6f, 90f, 35f, 1.5f, seed); break;
                    case SfxKind.EnemyAttack: clips[v] = SoundSynth.Growl(name, 0.45f, 105f * jitter, 0.2f, seed); break;
                    case SfxKind.EnemyHurt: clips[v] = SoundSynth.Growl(name, 0.3f, 140f * jitter, 0.8f, seed); break;
                    case SfxKind.EnemyDeath: clips[v] = SoundSynth.Growl(name, 0.9f, 95f * jitter, 0.5f, seed); break;
                    case SfxKind.EnemyRoar: clips[v] = SoundSynth.Growl(name, 2.2f, 70f * jitter, 0.1f, seed); break;
                    case SfxKind.Footstep: clips[v] = SoundSynth.Thump(name, 0.12f, 140f * jitter, 70f, 0.8f, seed); break;
                    case SfxKind.PlayerHurt: clips[v] = SoundSynth.Thump(name, 0.35f, 80f * jitter, 40f, 0.9f, seed); break;
                    case SfxKind.PlayerDeath: clips[v] = SoundSynth.Drone(name, 2.5f, new[] { 55f, 58.3f, 82.4f }, 0.6f, 0f, seed); break;
                    case SfxKind.UiHover: clips[v] = SoundSynth.Blip(name, 0.05f, 1400f, 1500f, 0f); break;
                    case SfxKind.UiClick: clips[v] = SoundSynth.Blip(name, 0.09f, 900f, 1300f, 0.4f); break;
                    case SfxKind.UiBack: clips[v] = SoundSynth.Blip(name, 0.1f, 900f, 600f, 0.4f); break;
                    case SfxKind.TotemBreak: clips[v] = SoundSynth.Clang(name, 1.6f, 140f * jitter, 2f, seed); break;
                    case SfxKind.Blessing: clips[v] = SoundSynth.Blip(name, 0.8f, 523f, 1046f, 0.6f); break;
                    case SfxKind.PortalOpen: clips[v] = SoundSynth.Whoosh(name, 1.4f, 0.5f * jitter, seed); break;
                    case SfxKind.Extract: clips[v] = SoundSynth.Blip(name, 1.2f, 392f, 784f, 0.8f); break;
                    case SfxKind.Pickup: clips[v] = SoundSynth.Blip(name, 0.15f, 700f * jitter, 1100f * jitter, 0.3f); break;
                    case SfxKind.Coins: clips[v] = SoundSynth.Clang(name, 0.3f, 1800f * jitter, 0.4f, seed); break;
                    case SfxKind.ChestOpen: clips[v] = SoundSynth.Thump(name, 0.5f, 70f * jitter, 50f, 1.4f, seed); break;
                    case SfxKind.BoltCharge: clips[v] = SoundSynth.Whoosh(name, 0.6f, 2.2f * jitter, seed); break;
                    case SfxKind.BoltRelease: clips[v] = SoundSynth.Whoosh(name, 0.35f, 2.8f * jitter, seed); break;
                    case SfxKind.Special: clips[v] = SoundSynth.Blip(name, 0.5f, 220f, 880f, 0.7f); break;
                    case SfxKind.LevelUp: clips[v] = SoundSynth.Blip(name, 1.0f, 330f, 990f, 0.8f); break;
                    case SfxKind.ChallengeStart: clips[v] = SoundSynth.Blip(name, 0.4f, 600f, 300f, 0.5f); break;
                    case SfxKind.ChallengeEnd: clips[v] = SoundSynth.Blip(name, 0.6f, 440f, 880f, 0.5f); break;
                    default: clips[v] = SoundSynth.Blip(name, 0.1f, 800f, 800f, 0f); break;
                }
            }
            placeholders[kind] = clips;
            return clips;
        }
    }
}
