using System;
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

        // Düğme tıklaması: kısa, yumuşak "pop" (hızla düşen frekans) ve çok kısa bir tık
        public static float[] ClickSamples()
        {
            int n = (int)(0.09f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(3);
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float frequency = Mathf.Max(480f, 1250f - 9000f * t);
                phase += TwoPi * frequency / SampleRate;

                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 48f);
                float tick = t < 0.003f ? ((float)rng.NextDouble() - 0.5f) * 0.5f : 0f;
                data[i] = Mathf.Sin(phase) * env * 0.85f + tick;
            }

            Normalize(data, 0.7f);
            FadeEdges(data, 0.003f);
            return data;
        }

        // Yarış bitiş geri sayımı: her saniyede bir gerilim tiki. Normal: alçak, kuru "tok"; son 3 saniye (urgent): tiz ve vurgulu
        public static float[] AlertTickSamples(bool urgent)
        {
            float duration = urgent ? 0.18f : 0.14f;
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            float frequency = urgent ? 1180f : 520f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * (urgent ? 14f : 24f));
                float tone = Mathf.Sin(TwoPi * frequency * t) + 0.45f * Mathf.Sin(TwoPi * frequency * 1.5f * t);
                float thump = urgent ? 0f : Mathf.Sin(TwoPi * 95f * t) * Mathf.Exp(-t * 40f) * 0.7f;
                data[i] = (tone * 0.6f + thump) * env;
            }

            Normalize(data, urgent ? 0.85f : 0.65f);
            FadeEdges(data, 0.003f);
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

        // Ana menü müziği: 8 ölçülük (19,2 sn) kesintisiz döngü. Am - F - C - G akoru; bas, pad, arpej, hafif vuruş ve hi-hat.
        // Notaların kuyruğu döngü başına sarılır, böylece döngü noktasında tık sesi olmaz.
        public static float[] MenuMusicSamples()
        {
            const float bpm = 100f;
            float beat = 60f / bpm;
            float bar = beat * 4f;
            int bars = 8;
            int n = (int)(bar * bars * SampleRate);
            var data = new float[n];
            var random = new System.Random(11);

            float[] roots = { 110f, 87.31f, 130.81f, 98f };
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f },
                new[] { 261.63f, 329.63f, 392f }, new[] { 196f, 246.94f, 293.66f }
            };

            for (int b = 0; b < bars; b++)
            {
                int chordIndex = b / 2;
                float barStart = b * bar;
                var chord = chords[chordIndex];

                // Pad: yumuşak girişli, yavaş çıkışlı akor
                foreach (float f in chord)
                    AddWrapped(data, barStart, bar + 0.4f, t => (Mathf.Sin(TwoPi * f * t) + 0.3f * Mathf.Sin(TwoPi * f * 2f * t)) * 0.07f * Mathf.Min(1f, t / 0.5f) * Mathf.Clamp01((bar + 0.4f - t) / 0.6f));

                // Bas: 1. vuruşta ve 2. vuruşun yarısında
                float root = roots[chordIndex];
                foreach (float at in new[] { 0f, beat * 1.5f, beat * 2f })
                    AddWrapped(data, barStart + at, 0.6f, t => Mathf.Sin(TwoPi * root * t) * 0.32f * Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t * 4.5f));

                // Arpej: sekizlik notalar, akor sesleri bir oktav yukarıda
                int[] pattern = { 0, 1, 2, 1, 2, 1, 0, 1 };
                for (int e = 0; e < 8; e++)
                {
                    float f = chord[pattern[e]] * 2f;
                    AddWrapped(data, barStart + e * beat / 2f, 0.5f, t => (Mathf.Sin(TwoPi * f * t) + 0.3f * Mathf.Sin(TwoPi * f * 2f * t)) * 0.12f * Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 9f));
                }

                // Vuruş: 1. ve 3. vuruşta yumuşak davul; sekizlik arası hi-hat
                foreach (float at in new[] { 0f, beat * 2f })
                    AddWrapped(data, barStart + at, 0.25f, t => Mathf.Sin(TwoPi * (45f + 90f * Mathf.Exp(-t * 28f)) * t) * 0.5f * Mathf.Exp(-t * 14f));

                for (int e = 0; e < 4; e++)
                {
                    float hatNoise = (float)random.NextDouble();
                    AddWrapped(data, barStart + (e + 0.5f) * beat, 0.05f, t => (Mathf.Sin(hatNoise * 100f + t * 90000f) * 0.5f + Mathf.Sin(t * 130000f)) * 0.04f * Mathf.Exp(-t * 90f));
                }
            }

            Normalize(data, 0.8f);
            return data;
        }

        static void AddWrapped(float[] data, float startSeconds, float lengthSeconds, Func<float, float> generate)
        {
            int start = (int)(startSeconds * SampleRate);
            int length = (int)(lengthSeconds * SampleRate);
            for (int i = 0; i < length; i++)
            {
                int index = (start + i) % data.Length;
                data[index] += generate(i / (float)SampleRate);
            }
        }

        // "Karşılaşma bulundu": iki yükselen çan sesi (kısa ve neşeli)
        public static float[] MatchFoundSamples()
        {
            const float seconds = 0.9f;
            int n = (int)(seconds * SampleRate);
            var data = new float[n];

            AddBell(data, 0.00f, 659.25f, 0.8f); // Mi
            AddBell(data, 0.16f, 987.77f, 1.0f); // Si (daha yüksek ve parlak)

            FadeEdges(data, 0.01f);
            Normalize(data, 0.85f);
            return data;
        }

        static void AddBell(float[] data, float startSeconds, float frequency, float gain)
        {
            int start = (int)(startSeconds * SampleRate);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 5.5f);
                float tone = Mathf.Sin(TwoPi * frequency * t) + 0.35f * Mathf.Sin(TwoPi * frequency * 2f * t) + 0.12f * Mathf.Sin(TwoPi * frequency * 3f * t);
                data[i] += tone * env * gain * 0.5f;
            }
        }

        // Alkış: kalabalık el çırpma. Rastgele kısa "çıt"lar (sabit tohumla, hep aynı ses) yoğunlaşıp seyrelir, altında hafif gürültü zemini.
        public static float[] ApplauseSamples()
        {
            const float seconds = 2.6f;
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            var random = new System.Random(7);

            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float swell = Mathf.Min(1f, t / 0.25f) * Mathf.Exp(-Mathf.Max(0f, t - 0.9f) * 1.3f); // hızla yükselir, sonra söner

                // Zemin: yumuşatılmış gürültü (uzaktaki kalabalık uğultusu)
                low += ((float)random.NextDouble() * 2f - 1f - low) * 0.35f;
                data[i] += low * 0.35f * swell;
            }

            // Tek tek el çırpmalar: kısa, hızla sönen gürültü patlamaları
            int claps = 260;
            for (int c = 0; c < claps; c++)
            {
                float at = (float)random.NextDouble() * (seconds - 0.1f);
                float density = Mathf.Min(1f, at / 0.25f) * Mathf.Exp(-Mathf.Max(0f, at - 0.9f) * 1.3f);
                if (random.NextDouble() > density) continue;

                int start = (int)(at * SampleRate);
                float amp = 0.4f + (float)random.NextDouble() * 0.6f;
                int length = (int)(0.012f * SampleRate);
                for (int k = 0; k < length && start + k < n; k++)
                {
                    float env = Mathf.Exp(-k / (0.003f * SampleRate));
                    data[start + k] += ((float)random.NextDouble() * 2f - 1f) * amp * env;
                }
            }

            FadeEdges(data, 0.03f);
            Normalize(data, 0.85f);
            return data;
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
