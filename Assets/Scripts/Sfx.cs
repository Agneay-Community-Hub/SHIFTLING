using System.Collections.Generic;
using UnityEngine;

namespace Shiftling
{
    /// <summary>All sound effects and the music loop are synthesised at startup - no audio assets.</summary>
    public static class Sfx
    {
        const int SR = 22050;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource[] pool;
        static int next;
        static AudioSource music;
        static System.Random rng = new System.Random(1234);
        public static bool Muted;

        public static void Init(GameObject host)
        {
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = host.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            music = host.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0.32f;

            Add("jump", Sweep(0.13f, 280, 620, 1, 0.22f));
            Add("djump", Sweep(0.15f, 520, 980, 3, 0.3f));
            Add("frogjump", Sweep(0.22f, 160, 520, 3, 0.35f));
            Add("flap", Noise(0.06f, 0.18f, 0.6f));
            Add("land", Noise(0.07f, 0.25f, 0.9f));
            Add("thud", Mix(Sweep(0.25f, 120, 40, 0, 0.6f), Noise(0.2f, 0.25f, 0.95f)));
            Add("shift", Mix(Sweep(0.32f, 220, 1400, 0, 0.3f, true), Noise(0.3f, 0.08f, 0.2f)));
            Add("nope", Notes(new[] { 52, 47 }, 0.09f, 1, 0.2f));
            Add("die", Mix(Sweep(0.55f, 700, 60, 2, 0.3f), Noise(0.4f, 0.12f, 0.6f)));
            Add("pickup", Notes(new[] { 72, 76, 79, 84 }, 0.08f, 3, 0.35f));
            Add("unlock", Notes(new[] { 64, 67, 71, 76, 79, 83, 88 }, 0.07f, 3, 0.35f));
            Add("checkpoint", Notes(new[] { 76, 83 }, 0.1f, 0, 0.35f));
            Add("splash", Noise(0.35f, 0.3f, 0.75f));
            Add("crumble", Mix(Noise(0.5f, 0.4f, 0.97f), Sweep(0.4f, 90, 40, 2, 0.25f)));
            Add("burn", Noise(0.7f, 0.25f, 0.5f, true));
            Add("door", Sweep(0.35f, 140, 330, 1, 0.18f));
            Add("win", Notes(new[] { 60, 64, 67, 72, 67, 72, 76, 79 }, 0.11f, 3, 0.35f));
            Add("select", Sweep(0.05f, 900, 1100, 3, 0.15f));

            music.clip = Music();
            music.Play();
        }

        static void Add(string n, float[] data)
        {
            var c = AudioClip.Create(n, data.Length, 1, SR, false);
            c.SetData(data, 0);
            clips[n] = c;
        }

        public static void Play(string name, float vol = 1f, float pitchVar = 0.06f)
        {
            if (Muted || pool == null || !clips.TryGetValue(name, out var c)) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = 1f + ((float)rng.NextDouble() * 2 - 1) * pitchVar;
            s.PlayOneShot(c, vol);
            OnPlay?.Invoke(name, vol, s.pitch);
        }

        /// <summary>Raised for every sound played (used by the trailer recorder to rebuild the soundtrack).</summary>
        public static System.Action<string, float, float> OnPlay;
        public static AudioClip Clip(string name) => clips.TryGetValue(name, out var c) ? c : null;
        public static AudioClip MusicClip => music != null ? music.clip : null;
        public const float MusicVolume = 0.32f;
        public const int SampleRate = SR;

        public static void ToggleMute()
        {
            Muted = !Muted;
            if (music != null) music.mute = Muted;
        }

        // --------------------------------------------------------------- synth helpers
        static float Wave(int w, double ph)
        {
            double f = ph - System.Math.Floor(ph);
            switch (w)
            {
                case 0: return Mathf.Sin((float)(f * 2 * System.Math.PI));
                case 1: return f < 0.5 ? 0.8f : -0.8f;
                case 2: return (float)(f * 2 - 1);
                default: return (float)(f < 0.5 ? f * 4 - 1 : 3 - f * 4);
            }
        }

        static float Rand() => (float)(rng.NextDouble() * 2 - 1);

        static float[] Sweep(float dur, float f0, float f1, int wave, float vol, bool wobble = false)
        {
            int len = (int)(SR * dur);
            var d = new float[len];
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float p = i / (float)len;
                float f = Mathf.Lerp(f0, f1, p * p * (wobble ? 1 : 0.5f) + p * (wobble ? 0 : 0.5f));
                if (wobble) f *= 1 + Mathf.Sin(p * 60) * 0.08f;
                ph += f / SR;
                float env = Mathf.Min(1, i / (SR * 0.004f)) * (1 - p) * (1 - p);
                d[i] = Wave(wave, ph) * env * vol;
            }
            return d;
        }

        static float[] Noise(float dur, float vol, float lowpass, bool crackle = false)
        {
            int len = (int)(SR * dur);
            var d = new float[len];
            float y = 0;
            for (int i = 0; i < len; i++)
            {
                float p = i / (float)len;
                float x = Rand();
                if (crackle && rng.NextDouble() > 0.04) x *= 0.15f;
                y = Mathf.Lerp(x, y, lowpass);
                float env = Mathf.Min(1, i / (SR * 0.003f)) * Mathf.Pow(1 - p, 2.2f);
                d[i] = y * env * vol * (1 + lowpass * 2);
            }
            return d;
        }

        static float Midi(int n) => 440f * Mathf.Pow(2, (n - 69) / 12f);

        static float[] Notes(int[] notes, float step, int wave, float vol)
        {
            int noteLen = (int)(SR * step);
            int len = noteLen * notes.Length + (int)(SR * 0.25f);
            var d = new float[len];
            for (int n = 0; n < notes.Length; n++)
            {
                float f = Midi(notes[n]);
                int start = n * noteLen;
                int dur = (int)(SR * 0.3f);
                for (int i = 0; i < dur && start + i < len; i++)
                {
                    float t = i / (float)SR;
                    d[start + i] += Wave(wave, f * t) * Mathf.Exp(-t * 9) * Mathf.Min(1, i / 40f) * vol;
                }
            }
            return d;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var d = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < d.Length; i++) d[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
            return d;
        }

        static void AddTone(float[] buf, float start, float dur, float freq, int wave, float vol, float decay, float attack = 0.005f)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(1, t / attack) * Mathf.Exp(-t * decay) * Mathf.Min(1, (n - i) / 200f);
                buf[s0 + i] += Wave(wave, freq * t) * env * vol;
            }
        }

        /// <summary>A gentle, looping minor-key tune: bass, pad, arpeggio and light percussion.</summary>
        static AudioClip Music()
        {
            float bpm = 100, beat = 60f / bpm;
            int bars = 8;
            float total = bars * 4 * beat;
            var buf = new float[(int)(total * SR)];
            int[] roots = { 45, 41, 48, 43, 45, 41, 43, 40 };      // Am F C G Am F G E
            bool[] minor = { true, false, false, false, true, false, false, false };
            var mel = new System.Random(7);
            for (int bar = 0; bar < bars; bar++)
            {
                int r = roots[bar];
                int third = minor[bar] ? 3 : 4;
                int[] chord = { r, r + third, r + 7 };
                float t0 = bar * 4 * beat;
                // pad
                foreach (var n in chord)
                    AddTone(buf, t0, 4 * beat, Midi(n + 12), 0, 0.045f, 0.35f, 0.4f);
                // bass on eighths
                int[] bassPat = { 0, 0, 12, 0, 7, 0, 12, 7 };
                for (int e = 0; e < 8; e++)
                    AddTone(buf, t0 + e * beat / 2, beat / 2, Midi(r - 12 + bassPat[e]), 3, 0.16f, 7f);
                // arpeggio on sixteenths, gated pseudo-randomly for a melodic feel
                int[] arp = { 0, 1, 2, 1 };
                for (int s = 0; s < 16; s++)
                {
                    if (bar < 2 && s % 4 != 0) continue;
                    if (mel.NextDouble() < 0.25) continue;
                    int note = chord[arp[s % 4]] + 24 + (s >= 8 && bar % 2 == 1 ? 12 : 0);
                    AddTone(buf, t0 + s * beat / 4, beat / 2, Midi(note), 1, 0.035f, 14f);
                }
                // percussion
                for (int b = 0; b < 4; b++)
                {
                    float tb = t0 + b * beat;
                    if (b % 2 == 0)
                    {
                        int s0 = (int)(tb * SR);
                        for (int i = 0; i < SR * 0.2f && s0 + i < buf.Length; i++)
                        {
                            float t = i / (float)SR;
                            buf[s0 + i] += Mathf.Sin(2 * Mathf.PI * (50 * t + 60 * (1 - Mathf.Exp(-t * 30)) / 30f)) * Mathf.Exp(-t * 14) * 0.3f;
                        }
                    }
                    int h0 = (int)((tb + beat / 2) * SR);
                    for (int i = 0; i < SR * 0.05f && h0 + i < buf.Length; i++)
                    {
                        float t = i / (float)SR;
                        buf[h0 + i] += Rand() * Mathf.Exp(-t * 90) * 0.05f;
                    }
                }
            }
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)System.Math.Tanh(buf[i] * 1.4f) * 0.8f;
            var clip = AudioClip.Create("music", buf.Length, 1, SR, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
