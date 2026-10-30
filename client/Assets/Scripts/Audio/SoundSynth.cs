using UnityEngine;

namespace TextRun.Audio
{
    // Oyun seslerini kodla üretir (geçici, telifsiz). Hazır kayıtlarla değiştirilebilir.
    // Tüm sesler tek kanallı (mono), 22,05 kHz'dir.
    public static class SoundSynth
    {
        public const int SampleRate = 22050;
        const float TwoPi = Mathf.PI * 2f;

        // Geri sayım tiki: kısa, temiz bir bip
        public static float[] TickSamples(float frequency = 660f)
        {
            int n = (int)(0.22f * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 16f);
                data[i] = (Mathf.Sin(TwoPi * frequency * t) * 0.65f + Mathf.Sin(TwoPi * frequency * 2f * t) * 0.2f) * env;
            }
            return data;
        }

        // Hakem düdüğü: ~2,9 kHz taşıyıcı, 30 Hz titreşim ("bilye" sesi) ve hafif hava
        public static float[] WhistleSamples()
        {
            float duration = 0.9f;
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(7);
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float trill = Mathf.Sin(TwoPi * 30f * t);
                float frequency = 2900f + 160f * trill;
                phase += TwoPi * frequency / SampleRate;

                float attack = Mathf.Min(1f, t / 0.03f);
                float release = Mathf.Min(1f, (duration - t) / 0.15f);
                float breath = ((float)rng.NextDouble() - 0.5f) * 0.25f;
                data[i] = (Mathf.Sin(phase) * (0.72f + 0.28f * trill) + breath) * attack * release * 0.7f;
            }
            return data;
        }

        // Yarış müziği: 128 BPM, 16 vuruşluk (yaklaşık 7,5 sn) döngü. Kick + hi-hat + bas + arpej. La minör.
        public static float[] RaceLoopSamples()
        {
            const float bpm = 128f;
            const int beats = 16;
            float beatSeconds = 60f / bpm;
            int total = (int)(beatSeconds * beats * SampleRate);
            var data = new float[total];
            var rng = new System.Random(11);

            float[] bass = { 55f, 55f, 65.4f, 82.4f, 43.65f, 43.65f, 49f, 49f, 55f, 55f, 65.4f, 82.4f, 36.7f, 49f, 55f, 73.4f };
            float[] arp = { 220f, 261.6f, 329.6f, 392f, 440f, 392f, 329.6f, 261.6f };
            int[] arpPattern = { 0, 2, 4, 2, 1, 3, 5, 3, 0, 2, 4, 6, 5, 3, 1, 2 };

            for (int beat = 0; beat < beats; beat++)
            {
                int beatStart = (int)(beat * beatSeconds * SampleRate);
                int beatLength = (int)(beatSeconds * SampleRate);

                // Kick: her vuruşta, düşen frekanslı sinüs
                AddNote(data, beatStart, (int)(0.28f * SampleRate), (t) =>
                {
                    float f = 50f + 110f * Mathf.Exp(-t * 38f);
                    return Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t * 9f) * 0.9f;
                });

                // Hi-hat: vuruş arası (ve iki yarım nota)
                int hat = beatStart + beatLength / 2;
                AddNote(data, hat, (int)(0.05f * SampleRate), (t) => ((float)rng.NextDouble() - 0.5f) * Mathf.Exp(-t * 70f) * 0.5f);

                // Bas: her vuruşta bir nota, yumuşatılmış kare dalga
                float bassFreq = bass[beat % bass.Length];
                AddNote(data, beatStart, (int)(beatSeconds * 0.95f * SampleRate), (t) =>
                {
                    float wave = (float)System.Math.Tanh(Mathf.Sin(TwoPi * bassFreq * t) * 2.5f);
                    return wave * Mathf.Exp(-t * 2.2f) * 0.32f;
                });

                // Arpej: 16'lık notalar (vuruş başına 4)
                for (int sub = 0; sub < 4; sub++)
                {
                    float arpFreq = arp[arpPattern[(beat * 4 + sub) % arpPattern.Length] % arp.Length];
                    int start = beatStart + sub * beatLength / 4;
                    AddNote(data, start, (int)(beatSeconds / 4f * 0.9f * SampleRate), (t) =>
                    {
                        float tri = 2f * Mathf.Abs(2f * (t * arpFreq - Mathf.Floor(t * arpFreq + 0.5f))) - 1f;
                        return tri * Mathf.Exp(-t * 14f) * 0.16f;
                    });
                }
            }

            Normalize(data, 0.8f);
            FadeEdges(data, 0.004f); // döngü başı/sonunda tıklama olmasın
            return data;
        }

        // Bitiş jingle'ı. Podyum (ilk 3): C majör arpej + parlak final akoru. Diğerleri: iki notalık yumuşak "bitti" sesi.
        // Gerçek alkış kaydı kodla inandırıcı üretilemediği için müzikal bir ses seçildi.
        public static float[] FanfareSamples(bool podium)
        {
            float duration = podium ? 2.1f : 1.2f;
            int n = (int)(duration * SampleRate);
            var data = new float[n];

            // Notalar: başlangıç (sn), frekans, süre (sn), ses
            float[][] notes = podium
                ? new[]
                {
                    new[] { 0.00f, 523.25f, 0.30f, 0.8f },  // C5
                    new[] { 0.14f, 659.25f, 0.30f, 0.8f },  // E5
                    new[] { 0.28f, 783.99f, 0.30f, 0.8f },  // G5
                    new[] { 0.42f, 1046.5f, 1.55f, 0.9f },  // C6 (uzun)
                    new[] { 0.42f, 659.25f, 1.55f, 0.45f }, // akor: E5
                    new[] { 0.42f, 783.99f, 1.55f, 0.45f }, // akor: G5
                    new[] { 0.42f, 130.81f, 1.55f, 0.5f },  // bas: C3
                }
                : new[]
                {
                    new[] { 0.00f, 392.00f, 0.35f, 0.7f },  // G4
                    new[] { 0.28f, 523.25f, 0.90f, 0.8f },  // C5
                };

            foreach (var note in notes)
            {
                float start = note[0], frequency = note[1], length = note[2], volume = note[3];
                AddNote(data, (int)(start * SampleRate), (int)(length * SampleRate), (t) =>
                {
                    float attack = Mathf.Min(1f, t / 0.008f);
                    float release = Mathf.Clamp01((length - t) / 0.28f);
                    float decay = 0.62f + 0.38f * Mathf.Exp(-t * 5f);
                    float vibrato = 1f + 0.004f * Mathf.Sin(TwoPi * 5.5f * t);
                    float p = TwoPi * frequency * vibrato * t;
                    float tone = Mathf.Sin(p) + 0.5f * Mathf.Sin(2f * p) + 0.22f * Mathf.Sin(3f * p);
                    return tone * attack * release * decay * volume * 0.35f;
                });
            }

            Normalize(data, 0.75f);
            FadeEdges(data, 0.004f);
            return data;
        }

        // ---- yardımcılar ----

        static void AddNote(float[] data, int start, int length, System.Func<float, float> generate)
        {
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= data.Length) break;

                float t = i / (float)SampleRate;
                float release = Mathf.Min(1f, (length - i) / (0.008f * SampleRate)); // nota sonunda ani kesilme olmasın
                data[index] += generate(t) * release;
            }
        }

        public static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-6f) return;

            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        static void FadeEdges(float[] data, float seconds)
        {
            int fade = Mathf.Min((int)(seconds * SampleRate), data.Length / 2);
            for (int i = 0; i < fade; i++)
            {
                float g = i / (float)fade;
                data[i] *= g;
                data[data.Length - 1 - i] *= g;
            }
        }

        public static AudioClip ToClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
