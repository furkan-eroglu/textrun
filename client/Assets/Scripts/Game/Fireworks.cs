using System;
using TextRun.Audio;
using UnityEngine;

namespace TextRun.Game
{
    // Yarışı bitirince pistin üstünde patlayan havai fişekler. Roketler alttan yükselir, tepede renkli kıvılcımlara dağılır.
    // Konum zamandan hesaplanır (parçacık listesi tutulmaz); ilk üçe girenler daha çok ve daha büyük patlama görür.
    public class Fireworks
    {
        const float RiseSeconds = 0.55f;
        const float SparkSeconds = 1.5f;
        const float Gravity = 70f;

        static readonly Color[] Colors =
        {
            new Color(1.00f, 0.82f, 0.30f), new Color(1.00f, 0.35f, 0.38f), new Color(0.40f, 0.80f, 1.00f),
            new Color(0.55f, 1.00f, 0.60f), new Color(0.85f, 0.55f, 1.00f), new Color(1.00f, 0.62f, 0.30f)
        };

        class Burst
        {
            public float delay, x, apex;
            public Color color;
            public float[] angle, speed;
        }

        Burst[] bursts = new Burst[0];
        float startedAt = -999f;
        float totalSeconds;

        public float TotalSeconds => totalSeconds;
        public bool Active => Time.realtimeSinceStartup - startedAt < totalSeconds;

        public void Clear()
        {
            startedAt = -999f;
            totalSeconds = 0f;
        }

        // big: ilk üçe girenler için çok sayıda büyük patlama
        public void Launch(bool big)
        {
            var random = new System.Random(Environment.TickCount);
            int count = big ? 7 : 3;
            int sparks = big ? 44 : 26;
            float spread = big ? 3.4f : 1.6f;
            float power = big ? 1f : 0.7f;

            bursts = new Burst[count];
            for (int i = 0; i < count; i++)
            {
                var b = new Burst
                {
                    delay = spread * i / (count - 1) + (float)random.NextDouble() * 0.2f,
                    x = 0.15f + (float)random.NextDouble() * 0.7f,
                    apex = 0.18f + (float)random.NextDouble() * 0.40f,
                    color = Colors[random.Next(Colors.Length)],
                    angle = new float[sparks],
                    speed = new float[sparks]
                };
                for (int k = 0; k < sparks; k++)
                {
                    b.angle[k] = (k + (float)random.NextDouble() * 0.6f) / sparks * Mathf.PI * 2f;
                    b.speed[k] = (90f + (float)random.NextDouble() * 90f) * power;
                }
                bursts[i] = b;
            }

            totalSeconds = spread + 0.2f + RiseSeconds + SparkSeconds;
            startedAt = Time.realtimeSinceStartup;
        }

        // Verilen alanın (pist üstü) içinde çizer
        public void Draw(Rect area)
        {
            if (!Active) return;

            float now = Time.realtimeSinceStartup - startedAt;
            var disc = SoundIcon.Disc();
            var old = GUI.color;

            foreach (var b in bursts)
            {
                float t = now - b.delay;
                if (t < 0f) continue;

                float x = area.x + b.x * area.width;
                float apexY = area.y + b.apex * area.height;

                if (t < RiseSeconds)
                {
                    // Roket: alttan tepe noktaya yükselir, arkasında kısa iz bırakır
                    float u = t / RiseSeconds;
                    float y = Mathf.Lerp(area.yMax, apexY, 1f - (1f - u) * (1f - u));
                    for (int i = 0; i < 4; i++)
                    {
                        GUI.color = new Color(1f, 0.92f, 0.7f, 0.85f - i * 0.2f);
                        GUI.DrawTexture(new Rect(x - 2.5f + i * 0.4f, y + i * 7f - 2.5f, 5f - i, 5f - i), disc);
                    }
                    continue;
                }

                float s = t - RiseSeconds;
                if (s > SparkSeconds) continue;

                float life = s / SparkSeconds;
                float alpha = Mathf.Clamp01(2.2f - life * 2.0f);

                // Patlama anında kısa bir parlama
                if (s < 0.12f)
                {
                    GUI.color = new Color(1f, 1f, 0.9f, 0.9f * (1f - s / 0.12f));
                    GUI.DrawTexture(new Rect(x - 30f, apexY - 30f, 60f, 60f), disc);
                }

                float size = Mathf.Lerp(14f, 5f, life);
                for (int k = 0; k < b.angle.Length; k++)
                {
                    float px = x + Mathf.Cos(b.angle[k]) * b.speed[k] * s;
                    float py = apexY + Mathf.Sin(b.angle[k]) * b.speed[k] * s + 0.5f * Gravity * s * s;
                    var c = Color.Lerp(b.color, Color.white, 0.35f * (1f - life));
                    c.a = alpha;
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(px - size / 2f, py - size / 2f, size, size), disc);
                }
            }

            GUI.color = old;
        }
    }
}
