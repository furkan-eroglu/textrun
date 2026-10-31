using UnityEngine;

namespace TextRun.Game
{
    // Sonuç ekranının arka planı: koyu degrade, kupanın arkasında açılan ışın demeti ve ışık, ortada büyük bir kupa.
    // Sıraya göre kupa altın (1.), gümüş (2.), bronz (3.) ya da çelik (diğerleri) olur.
    // İlk üçe girenlerde ekran açılınca konfetiler yukarıdan aşağı süzülür (bir kez); hepsi ekrandan çıkınca animasyon biter.
    public class ResultsBackdrop
    {
        struct Confetti
        {
            public float x;            // yatay başlangıç (ekran oranı)
            public float w, h;         // boyut
            public float delay;        // düşmeye başlama gecikmesi (sn)
            public float speed;        // düşme hızı (arayüz birimi/sn)
            public float swayAmount;   // yana salınım genliği
            public float swayFrequency;
            public float phase;
            public float angle;        // başlangıç açısı
            public float spin;         // dönüş hızı (derece/sn)
            public Color color;
        }

        static readonly Color[] ConfettiColors =
        {
            new Color(1.00f, 0.80f, 0.30f), new Color(0.35f, 0.85f, 1.00f), new Color(1.00f, 0.42f, 0.76f),
            new Color(0.58f, 1.00f, 0.64f), new Color(0.72f, 0.56f, 1.00f)
        };

        Texture2D gradient;
        Texture2D glow;
        Confetti[] confetti;
        float startedAt = -999f;

        // Sonuç ekranı her açıldığında çağrılır: konfeti yeniden yağmaya başlar
        public void Restart()
        {
            startedAt = Time.realtimeSinceStartup;
        }

        // trophyCenter: kupanın ekran konumu (arayüz birimi), trophySize: kenar uzunluğu
        public void Draw(float width, float height, Vector2 trophyCenter, float trophySize, int rank)
        {
            Prepare();

            var tint = rank == 1 ? Pictograms.TrophyTint.Gold : rank == 2 ? Pictograms.TrophyTint.Silver : rank == 3 ? Pictograms.TrophyTint.Bronze : Pictograms.TrophyTint.Steel;
            Color accent = rank == 1 ? new Color(1.00f, 0.82f, 0.30f) : rank == 2 ? new Color(0.78f, 0.86f, 1.00f) : rank == 3 ? new Color(1.00f, 0.62f, 0.34f) : new Color(0.45f, 0.58f, 0.95f);

            GUI.DrawTexture(new Rect(0f, 0f, width, height), gradient, ScaleMode.StretchToFill);

            var old = GUI.color;

            // Işın demeti ve kupanın arkasındaki ışık
            float rayRadius = width * 1.3f;
            GUI.color = new Color(accent.r, accent.g, accent.b, rank <= 3 ? 0.20f : 0.10f);
            GUI.DrawTexture(new Rect(trophyCenter.x - rayRadius, trophyCenter.y - rayRadius, rayRadius * 2f, rayRadius * 2f), Pictograms.Rays());

            float glowRadius = width * 0.75f;
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.30f);
            GUI.DrawTexture(new Rect(trophyCenter.x - glowRadius, trophyCenter.y - glowRadius, glowRadius * 2f, glowRadius * 2f), glow);

            // Kupanın yer gölgesi ve kupa
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(trophyCenter.x - trophySize * 0.38f, trophyCenter.y + trophySize * 0.42f, trophySize * 0.76f, trophySize * 0.12f), glow);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(trophyCenter.x - trophySize / 2f, trophyCenter.y - trophySize / 2f, trophySize, trophySize), Pictograms.Trophy(tint));
            GUI.color = old;

            // Süzülen konfetiler (ilk üçte), kupanın ve listenin önünde
            if (rank <= 3) DrawConfetti(width, height);
        }

        void DrawConfetti(float width, float height)
        {
            float elapsed = Time.realtimeSinceStartup - startedAt;
            var matrix = GUI.matrix;
            var old = GUI.color;

            foreach (var c in confetti)
            {
                float t = elapsed - c.delay;
                if (t < 0f) continue;

                float y = -24f + c.speed * t;
                if (y > height + 24f) continue; // ekrandan çıktı

                float x = c.x * width + Mathf.Sin(t * c.swayFrequency + c.phase) * c.swayAmount;
                float fadeIn = Mathf.Clamp01(t / 0.25f);
                var rect = new Rect(x, y, c.w, c.h);

                GUI.color = new Color(c.color.r, c.color.g, c.color.b, 0.9f * fadeIn);
                GUIUtility.RotateAroundPivot(c.angle + c.spin * t, matrix.MultiplyPoint3x4(rect.center)); // pivot, ölçeklenmiş koordinatta verilir
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = old;
        }

        void Prepare()
        {
            if (gradient != null) return;

            gradient = MakeGradient();
            glow = MakeGlow();

            // Sabit tohum: konfetilerin düşüş düzeni her seferinde aynı
            var rng = new System.Random(11);
            confetti = new Confetti[56];
            for (int i = 0; i < confetti.Length; i++)
            {
                confetti[i] = new Confetti
                {
                    x = (float)rng.NextDouble(),
                    w = 6f + (float)rng.NextDouble() * 8f,
                    h = 3f + (float)rng.NextDouble() * 4f,
                    delay = (float)rng.NextDouble() * 1.6f,
                    speed = 110f + (float)rng.NextDouble() * 110f,
                    swayAmount = 10f + (float)rng.NextDouble() * 26f,
                    swayFrequency = 1.5f + (float)rng.NextDouble() * 2.5f,
                    phase = (float)(rng.NextDouble() * Mathf.PI * 2.0),
                    angle = (float)(rng.NextDouble() * 180.0),
                    spin = (float)(rng.NextDouble() * 360.0 - 180.0),
                    color = ConfettiColors[rng.Next(ConfettiColors.Length)]
                };
            }
        }

        static Texture2D MakeGradient()
        {
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var top = new Color(0.07f, 0.06f, 0.19f);
            var bottom = new Color(0.04f, 0.07f, 0.16f);
            for (int y = 0; y < h; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(h - 1)));
            tex.Apply();
            return tex;
        }

        static Texture2D MakeGlow()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
