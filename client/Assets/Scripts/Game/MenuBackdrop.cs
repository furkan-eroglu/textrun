using UnityEngine;

namespace TextRun.Game
{
    // Menü ve eşleşme ekranlarının sade arka planı: koyu lacivert degrade ve üstte tek bir yumuşak ışık.
    // Animasyon yoktur.
    public class MenuBackdrop
    {
        Texture2D gradient;
        Texture2D glow;

        public void Draw(float width, float height)
        {
            if (gradient == null)
            {
                gradient = MakeGradient();
                glow = MakeGlow();
            }

            GUI.DrawTexture(new Rect(0f, 0f, width, height), gradient, ScaleMode.StretchToFill);

            float r = width * 1.05f;
            var old = GUI.color;
            GUI.color = new Color(0.25f, 0.55f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(width * 0.5f - r, height * 0.16f - r, r * 2f, r * 2f), glow);
            GUI.color = old;
        }

        static Texture2D MakeGradient()
        {
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var top = new Color(0.04f, 0.06f, 0.14f);
            var bottom = new Color(0.08f, 0.10f, 0.24f);
            for (int y = 0; y < h; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(h - 1))); // y=0 alt satır
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
