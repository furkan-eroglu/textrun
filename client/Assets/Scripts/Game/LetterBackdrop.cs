using TextRun.Typing;
using UnityEngine;

namespace TextRun.Game
{
    // Menü ve eşleşme ekranlarının arka planı: koyu degrade üzerinde yavaşça süzülen, eğri büğrü duran renkli harfler
    // ve arkada yumuşak ışık lekeleri. Harfler seçili dilin alfabesinden gelir (Türkçede ç, ğ, ı, ö, ş, ü dahil).
    // Büyük harfler daha silik ve yavaştır (derinlik hissi). Geçici OnGUI çizimi.
    public class LetterBackdrop
    {
        const int LetterCount = 46;
        static readonly int[] Sizes = { 26, 40, 58, 84, 118, 160 };

        static readonly string TurkishAlphabet = "ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ";
        static readonly string EnglishAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        static readonly Color[] Palette =
        {
            new Color(0.35f, 0.85f, 1.00f), new Color(1.00f, 0.42f, 0.76f), new Color(1.00f, 0.80f, 0.36f),
            new Color(0.58f, 1.00f, 0.64f), new Color(0.72f, 0.56f, 1.00f)
        };

        struct Letter
        {
            public string text;
            public float x, y;          // ekran oranı (0-1)
            public int sizeBucket;
            public float angle, spin, spinSpeed, phase, swaySpeed, drift;
            public int color;
            public float alphaFactor;
        }

        readonly System.Random rng = new System.Random(5);
        Letter[] letters;
        GameLanguage builtFor;
        bool built;

        Texture2D gradient;
        Texture2D glow;
        GUIStyle[] styles;

        public void Draw(float width, float height, GameLanguage language)
        {
            Prepare(language);
            float time = Time.realtimeSinceStartup;
            float dt = Event.current.type == EventType.Repaint ? Time.unscaledDeltaTime : 0f;

            GUI.DrawTexture(new Rect(0f, 0f, width, height), gradient, ScaleMode.StretchToFill);
            DrawGlow(width, height, time);

            var oldMatrix = GUI.matrix;
            for (int i = 0; i < letters.Length; i++)
            {
                ref Letter l = ref letters[i];

                if (dt > 0f)
                {
                    l.y -= l.drift * dt;
                    if (l.y < -0.25f) Respawn(ref l, 1.15f);
                }

                float depth = l.sizeBucket / (float)(Sizes.Length - 1);
                float alpha = Mathf.Lerp(0.55f, 0.16f, depth) * l.alphaFactor;
                float sway = Mathf.Sin(time * l.swaySpeed + l.phase) * 0.025f;
                float angle = l.angle + Mathf.Sin(time * l.spinSpeed + l.phase) * l.spin;

                float px = (l.x + sway) * width;
                float py = l.y * height;
                float s = Sizes[l.sizeBucket];

                var style = styles[l.sizeBucket];
                Color c = Palette[l.color];
                style.normal.textColor = new Color(c.r, c.g, c.b, alpha);

                GUIUtility.RotateAroundPivot(angle, new Vector2(px, py));
                GUI.Label(new Rect(px - s, py - s, s * 2f, s * 2f), l.text, style);
                GUI.matrix = oldMatrix;
            }
        }

        void Prepare(GameLanguage language)
        {
            if (gradient == null) gradient = MakeGradient();
            if (glow == null) glow = MakeGlow();

            if (styles == null)
            {
                styles = new GUIStyle[Sizes.Length];
                for (int i = 0; i < styles.Length; i++)
                    styles[i] = new GUIStyle(GUI.skin.label) { fontSize = Sizes[i], fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }

            if (built && builtFor == language) return;

            string alphabet = language == GameLanguage.Turkish ? TurkishAlphabet : EnglishAlphabet;
            letters = new Letter[LetterCount];
            for (int i = 0; i < letters.Length; i++)
            {
                letters[i] = new Letter();
                Respawn(ref letters[i], (float)rng.NextDouble() * 1.3f - 0.1f);

                // Bazı harfler küçük harf: çeşitlilik
                char ch = alphabet[rng.Next(alphabet.Length)];
                if (rng.NextDouble() < 0.3) ch = TextNormalizer.ToLower(ch, language);
                letters[i].text = ch.ToString();
            }
            builtFor = language;
            built = true;
        }

        void Respawn(ref Letter l, float y)
        {
            l.x = (float)rng.NextDouble();
            l.y = y;
            l.sizeBucket = rng.Next(Sizes.Length);
            float depth = l.sizeBucket / (float)(Sizes.Length - 1);
            l.angle = (float)(rng.NextDouble() * 80.0 - 40.0);
            l.spin = 6f + (float)rng.NextDouble() * 14f;
            l.spinSpeed = 0.2f + (float)rng.NextDouble() * 0.5f;
            l.phase = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            l.swaySpeed = 0.15f + (float)rng.NextDouble() * 0.35f;
            l.drift = Mathf.Lerp(0.06f, 0.015f, depth) * (0.7f + (float)rng.NextDouble() * 0.6f); // büyük harfler daha yavaş
            l.color = rng.Next(Palette.Length);
            l.alphaFactor = 0.7f + (float)rng.NextDouble() * 0.5f;
        }

        // Arkada yavaşça kayan üç yumuşak ışık lekesi
        void DrawGlow(float width, float height, float time)
        {
            DrawBlob(width, height, 0.25f + 0.12f * Mathf.Sin(time * 0.11f), 0.22f + 0.08f * Mathf.Cos(time * 0.09f), 0.95f, new Color(0.30f, 0.80f, 1.00f, 0.22f));
            DrawBlob(width, height, 0.78f + 0.10f * Mathf.Cos(time * 0.08f), 0.55f + 0.10f * Mathf.Sin(time * 0.12f), 1.10f, new Color(1.00f, 0.35f, 0.75f, 0.20f));
            DrawBlob(width, height, 0.35f + 0.10f * Mathf.Sin(time * 0.07f + 1f), 0.92f + 0.05f * Mathf.Cos(time * 0.1f), 0.90f, new Color(1.00f, 0.75f, 0.30f, 0.15f));
        }

        void DrawBlob(float width, float height, float cx, float cy, float radiusFactor, Color color)
        {
            float r = width * radiusFactor;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx * width - r, cy * height - r, r * 2f, r * 2f), glow);
            GUI.color = Color.white;
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
