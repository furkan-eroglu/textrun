using UnityEngine;

namespace TextRun.Audio
{
    // Hoparlör simgesi, kodla çizilir (dosya gerektirmez): ses açıkken ses dalgalı, kapalıyken kırmızı çarpılı.
    public static class SoundIcon
    {
        const int Size = 64;

        static Texture2D on;
        static Texture2D off;
        static Texture2D disc;

        public static Texture2D Get(bool soundOn)
        {
            if (soundOn) return on != null ? on : (on = Make(true));
            return off != null ? off : (off = Make(false));
        }

        // Düğmenin yuvarlak arka planı
        public static Texture2D Disc()
        {
            if (disc != null) return disc;

            disc = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float r = Size / 2f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)))));
            disc.Apply();
            return disc;
        }

        static Texture2D Make(bool soundOn)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var red = new Color(0.95f, 0.30f, 0.30f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Kenarları yumuşatmak için her pikselde 3x3 örnek alınır
                    int white = 0, redCount = 0;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float px = x + (sx + 0.5f) / 3f;
                            float py = y + (sy + 0.5f) / 3f;
                            if (!soundOn && InCross(px, py)) redCount++;
                            else if (InSpeaker(px, py) || (soundOn && InWaves(px, py))) white++;
                        }
                    }

                    int total = white + redCount;
                    tex.SetPixel(x, y, total == 0 ? Color.clear : new Color(
                        (Color.white.r * white + red.r * redCount) / total,
                        (Color.white.g * white + red.g * redCount) / total,
                        (Color.white.b * white + red.b * redCount) / total,
                        total / 9f));
                }
            }
            tex.Apply();
            return tex;
        }

        // Çizimin yatay sınırları iki durumda da aynıdır (8..57): doku ortası 32'ye göre simetrik, düğmenin yuvarlağında tam ortalanır.
        // Dikey: koni 10..54 arasındadır, ortası 32.

        // Hoparlör gövdesi (dikdörtgen) ve koni (genişleyen yamuk)
        static bool InSpeaker(float x, float y)
        {
            if (x >= 8f && x <= 18f && Mathf.Abs(y - 32f) <= 8f) return true;
            if (x > 18f && x <= 32f) return Mathf.Abs(y - 32f) <= 8f + (x - 18f) * (14f / 14f);
            return false;
        }

        // Ses dalgaları: hoparlörün sağında iki yay (en dış nokta x=57)
        static bool InWaves(float x, float y)
        {
            float dx = x - 32f, dy = y - 32f;
            if (dx <= 0f) return false;

            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Abs(Mathf.Atan2(dy, dx));
            if (angle > 0.85f) return false;
            return (distance >= 12f && distance <= 16f) || (distance >= 21f && distance <= 25f);
        }

        // Hoparlörün yanındaki çarpı: dalgaların kapladığı alanın aynısını kaplar (x 38..57)
        static bool InCross(float x, float y)
        {
            if (x < 38f || x > 57f || y < 22f || y > 42f) return false;

            float a = (x - 38f) / 19f * 20f; // çarpıyı kutuya yay
            float b = y - 22f;
            return Mathf.Abs(a - b) <= 2.6f || Mathf.Abs(a + b - 20f) <= 2.6f;
        }
    }
}
