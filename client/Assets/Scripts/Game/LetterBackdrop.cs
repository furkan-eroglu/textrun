using UnityEngine;

namespace TextRun.Game
{
    // Menü ve eşleşme ekranlarının arka planı: koyu degrade üzerinde dağınık, eğri duran, renkli harfler ve arkada
    // yumuşak ışık lekeleri. Hepsi SABİTTİR (animasyon yok): oyunu yormaz, pencereye dönüşte fırlama/sıçrama olmaz.
    // Harfler her zaman A-Z'dir (Türkçe karakter yok), dil değişince değişmez.
    public class LetterBackdrop
    {
        const int LetterCount = 46;
        static readonly int[] Sizes = { 26, 40, 58, 84, 118, 160 };
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        static readonly Color[] Palette =
        {
            new Color(0.35f, 0.85f, 1.00f), new Color(1.00f, 0.42f, 0.76f), new Color(1.00f, 0.80f, 0.36f),
            new Color(0.58f, 1.00f, 0.64f), new Color(0.72f, 0.56f, 1.00f)
        };

        struct Letter
        {
            public string text;
            public float x, y;   // ekran oranı (0-1), sabit
            public int sizeBucket;
            public float angle;
            public int color;
            public float alphaFactor;
        }

        Letter[] letters;
        Texture2D gradient;
        Texture2D glow;
        GUIStyle[] styles;

        public void Draw(float width, float height)
        {
            Prepare();

            GUI.DrawTexture(new Rect(0f, 0f, width, height), gradient, ScaleMode.StretchToFill);

            // Sabit ışık lekeleri
            DrawBlob(width, height, 0.25f, 0.22f, 0.95f, new Color(0.30f, 0.80f, 1.00f, 0.22f));
            DrawBlob(width, height, 0.80f, 0.58f, 1.10f, new Color(1.00f, 0.35f, 0.75f, 0.20f));
            DrawBlob(width, height, 0.35f, 0.95f, 0.90f, new Color(1.00f, 0.75f, 0.30f, 0.15f));

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            foreach (var l in letters)
            {
                float depth = l.sizeBucket / (float)(Sizes.Length - 1);
                float alpha = Mathf.Lerp(0.55f, 0.16f, depth) * l.alphaFactor;
                float px = l.x * width;
                float py = l.y * height;
                float s = Sizes[l.sizeBucket];

                Color c = Palette[l.color];
                GUI.color = new Color(c.r, c.g, c.b, alpha); // yazı beyaz, renk buradan: hover/aktif durumlarında renk değişmez

                GUIUtility.RotateAroundPivot(l.angle, new Vector2(px, py));
                GUI.Label(new Rect(px - s, py - s, s * 2f, s * 2f), l.text, styles[l.sizeBucket]);
                GUI.matrix = oldMatrix;
            }
            GUI.color = oldColor;
        }

        void Prepare()
        {
            if (letters != null) return;

            gradient = MakeGradient();
            glow = MakeGlow();

            styles = new GUIStyle[Sizes.Length];
            for (int i = 0; i < styles.Length; i++)
            {
                styles[i] = new GUIStyle(GUI.skin.label) { fontSize = Sizes[i], fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                UiKit.SetAllTextColors(styles[i], Color.white);
            }

            // Sabit tohum: harfler her açılışta aynı yerlerde durur
            var rng = new System.Random(5);
            letters = new Letter[LetterCount];
            for (int i = 0; i < letters.Length; i++)
            {
                char ch = Alphabet[rng.Next(Alphabet.Length)];
                if (rng.NextDouble() < 0.3) ch = char.ToLowerInvariant(ch);

                letters[i] = new Letter
                {
                    text = ch.ToString(),
                    x = (float)rng.NextDouble(),
                    y = (float)rng.NextDouble(),
                    sizeBucket = rng.Next(Sizes.Length),
                    angle = (float)(rng.NextDouble() * 80.0 - 40.0),
                    color = rng.Next(Palette.Length),
                    alphaFactor = 0.7f + (float)rng.NextDouble() * 0.5f
                };
            }
        }

        void DrawBlob(float width, float height, float cx, float cy, float radiusFactor, Color color)
        {
            float r = width * radiusFactor;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx * width - r, cy * height - r, r * 2f, r * 2f), glow);
            GUI.color = old;
        }

        static Texture2D MakeGradient()
        {
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var top = new Color(0.08f, 0.06f, 0.24f);
            var mid = new Color(0.20f, 0.09f, 0.40f);
            var bottom = new Color(0.04f, 0.19f, 0.38f);

            for (int y = 0; y < h; y++)
            {
                float t = 1f - y / (float)(h - 1); // y=0 alt satır; t=0 üst
                tex.SetPixel(0, y, t < 0.5f ? Color.Lerp(top, mid, t / 0.5f) : Color.Lerp(mid, bottom, (t - 0.5f) / 0.5f));
            }
            tex.Apply();
            return tex;
        }

        // Ortası parlak, kenarı şeffaf yumuşak daire
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
