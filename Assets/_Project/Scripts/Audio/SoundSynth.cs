using UnityEngine;

namespace Roguelike.Audio
{
    /// <summary>
    /// Makes simple placeholder sounds out of maths (noise, tones and filters), so the game has audio before real
    /// recordings exist. Each method returns a short AudioClip. Replace them with real clips through a
    /// <see cref="SoundLibrary"/> asset when they are made.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 22050;

        /// <summary>A rush of air: band-passed noise whose pitch rises and falls. Higher <paramref name="pitch"/> = lighter weapon.</summary>
        public static AudioClip Whoosh(string name, float seconds, float pitch, int seed)
        {
            var random = new System.Random(seed);
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var filter = new BandPass();
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
                float centre = Mathf.Lerp(300f, 1400f, Mathf.Sin(Mathf.PI * t)) * pitch;
                filter.Set(centre, 1.2f);
                data[i] = filter.Process(Noise(random)) * envelope * 1.6f;
            }
            return Make(name, data);
        }

        /// <summary>A body blow: a dropping low thump with a smack of noise on top.</summary>
        public static AudioClip Thump(string name, float seconds, float startHz, float endHz, float noise, int seed)
        {
            var random = new System.Random(seed);
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var low = new LowPass();
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float u = (float)i / count;
                float hz = Mathf.Lerp(startHz, endHz, Mathf.Sqrt(u));
                phase += hz / Rate * Mathf.PI * 2f;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 18f);
                low.Set(Mathf.Lerp(3500f, 600f, u));
                float smack = low.Process(Noise(random)) * Mathf.Exp(-t * 40f) * noise;
                data[i] = (body + smack) * 0.9f;
            }
            return Make(name, data);
        }

        /// <summary>Metal on metal: inharmonic ringing partials. Brighter and longer for a parry.</summary>
        public static AudioClip Clang(string name, float seconds, float baseHz, float brightness, int seed)
        {
            var random = new System.Random(seed);
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f, 13.3f };
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float sum = 0f;
                for (int p = 0; p < ratios.Length; p++)
                {
                    float decay = Mathf.Exp(-t * (4f + p * 3f) / brightness);
                    sum += Mathf.Sin(2f * Mathf.PI * baseHz * ratios[p] * t) * decay / (1f + p * 0.6f);
                }
                float attack = Noise(random) * Mathf.Exp(-t * 120f) * 0.6f;
                data[i] = (sum * 0.45f + attack);
            }
            return Make(name, data);
        }

        /// <summary>Something wet: a burst of low-passed noise with a gurgle.</summary>
        public static AudioClip Splat(string name, float seconds, int seed)
        {
            var random = new System.Random(seed);
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var low = new LowPass();
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float gurgle = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t);
                low.Set(Mathf.Lerp(2200f, 400f, (float)i / count));
                data[i] = low.Process(Noise(random)) * Mathf.Exp(-t * 7f) * gurgle * 1.8f;
            }
            return Make(name, data);
        }

        /// <summary>A rough voice: a buzzing tone through two vowel-like resonances. Low pitch = big creature.</summary>
        public static AudioClip Growl(string name, float seconds, float pitchHz, float vowel, int seed)
        {
            var random = new System.Random(seed);
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var f1 = new BandPass();
            var f2 = new BandPass();
            f1.Set(Mathf.Lerp(500f, 750f, vowel), 4f);
            f2.Set(Mathf.Lerp(900f, 1200f, vowel), 5f);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float hz = pitchHz * (1f + 0.15f * Mathf.Sin(t * Mathf.PI) - 0.2f * t) * (1f + 0.03f * Noise(random));
                phase += hz / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float rough = saw + Noise(random) * 0.35f;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.3f)) * (0.8f + 0.2f * Mathf.Sin(t * 70f));
                data[i] = (f1.Process(rough) + f2.Process(rough) * 0.6f) * envelope * 1.4f;
            }
            return Make(name, data);
        }

        /// <summary>A short tone that slides between two pitches: menu clicks, pickups, chimes.</summary>
        public static AudioClip Blip(string name, float seconds, float fromHz, float toHz, float harmonics)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float u = (float)i / count;
                float t = (float)i / Rate;
                phase += Mathf.Lerp(fromHz, toHz, u) / Rate;
                float wave = Mathf.Sin(2f * Mathf.PI * phase) + harmonics * Mathf.Sin(4f * Mathf.PI * phase) * 0.5f;
                data[i] = wave * Mathf.Exp(-t * 6f / seconds) * Mathf.Min(1f, t * 400f) * 0.5f;
            }
            return Make(name, data);
        }

        /// <summary>A slow, looping bed of sound for a scene: a few detuned notes of a chord under soft wind.</summary>
        public static AudioClip Drone(string name, float seconds, float[] notesHz, float wind, float pulseHz, int seed)
        {
            var random = new System.Random(seed);
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var low = new LowPass();
            low.Set(500f);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float sum = 0f;
                for (int n = 0; n < notesHz.Length; n++)
                {
                    // Whole cycles over the loop, so it repeats without a click.
                    float hz = Mathf.Round(notesHz[n] * seconds) / seconds;
                    float detune = Mathf.Round((notesHz[n] * 1.004f) * seconds) / seconds;
                    float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t * (n + 1) / seconds);
                    sum += (Mathf.Sin(2f * Mathf.PI * hz * t) + Mathf.Sin(2f * Mathf.PI * detune * t)) * swell;
                }
                sum /= Mathf.Max(1, notesHz.Length) * 2f;
                float beat = pulseHz > 0f ? Mathf.Pow(0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * (Mathf.Round(pulseHz * seconds) / seconds) * t), 6f) : 0f;
                float air = low.Process(Noise(random)) * wind;
                data[i] = (sum * 0.5f + air + beat * 0.25f * Mathf.Sin(2f * Mathf.PI * 55f * t)) * 0.6f;
            }
            // Fade the very ends together so the loop point is seamless.
            int fade = Rate / 10;
            for (int i = 0; i < fade && i < count; i++)
            {
                float k = (float)i / fade;
                data[i] *= k;
                data[count - 1 - i] *= k;
            }
            return Make(name, data);
        }

        static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float[] data)
        {
            float peak = 0.0001f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = Mathf.Min(1f, 0.9f / peak);
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A two-pole resonant band-pass filter.</summary>
        class BandPass
        {
            float a0, a1, a2, b1, b2, x1, x2, y1, y2;

            public void Set(float hz, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float norm = 1f + alpha;
                a0 = alpha / norm;
                a1 = 0f;
                a2 = -alpha / norm;
                b1 = -2f * Mathf.Cos(w) / norm;
                b2 = (1f - alpha) / norm;
            }

            public float Process(float x)
            {
                float y = a0 * x + a1 * x1 + a2 * x2 - b1 * y1 - b2 * y2;
                x2 = x1; x1 = x;
                y2 = y1; y1 = y;
                return y;
            }
        }

        /// <summary>A one-pole low-pass filter.</summary>
        class LowPass
        {
            float k, y;

            public void Set(float hz) => k = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate);

            public float Process(float x)
            {
                y += k * (x - y);
                return y;
            }
        }
    }
}
