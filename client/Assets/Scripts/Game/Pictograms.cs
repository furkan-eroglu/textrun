using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Game
{
    // Kodla çizilen küçük resimler (dosya gerektirmez): kupa ve ışın demeti.
    // Şekiller basit geometri formülleriyle tanımlanır; kenarlar 3x3 örnekle yumuşatılır. Her doku bir kez üretilir.
    public static class Pictograms
    {
        public enum TrophyTint { Gold, Silver, Bronze, Steel }

        static readonly Dictionary<TrophyTint, Texture2D> trophies = new Dictionary<TrophyTint, Texture2D>();
        static Texture2D rays;

        // ---------- Kupa ----------

        public static Texture2D Trophy(TrophyTint tint)
        {
            if (trophies.TryGetValue(tint, out var cached) && cached != null) return cached;

            Color top, bottom, highlight, shade;
            switch (tint)
            {
                case TrophyTint.Silver:
                    top = new Color(0.96f, 0.98f, 1.00f); bottom = new Color(0.60f, 0.66f, 0.76f); highlight = Color.white; shade = new Color(0.42f, 0.48f, 0.60f); break;
                case TrophyTint.Bronze:
                    top = new Color(0.96f, 0.72f, 0.46f); bottom = new Color(0.68f, 0.38f, 0.17f); highlight = new Color(1f, 0.86f, 0.66f); shade = new Color(0.44f, 0.23f, 0.09f); break;
                case TrophyTint.Steel:
                    top = new Color(0.64f, 0.72f, 0.88f); bottom = new Color(0.36f, 0.43f, 0.60f); highlight = new Color(0.86f, 0.90f, 1f); shade = new Color(0.24f, 0.30f, 0.45f); break;
                default:
                    top = new Color(1.00f, 0.91f, 0.42f); bottom = new Color(0.90f, 0.58f, 0.10f); highlight = new Color(1f, 0.98f, 0.76f); shade = new Color(0.68f, 0.42f, 0.06f); break;
            }

            var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    int covered = 0;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            if (TrophyColor(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f, top, bottom, highlight, shade, out var c))
                            {
                                r += c.r; g += c.g; b += c.b;
                                covered++;
                            }
                        }
                    }
                    tex.SetPixel(x, y, covered == 0 ? Color.clear : new Color(r / covered, g / covered, b / covered, covered / 9f));
                }
            }
            tex.Apply();
            trophies[tint] = tex;
            return tex;
        }

        static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        static bool InRect(float x, float y, float x0, float x1, float y0, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        // Kupa şekli (256x256, y yukarı): kase, kenar, iki kulp, gövde, düğüm, iki kat taban ve ortada madalyon
        static bool TrophyColor(float x, float y, Color top, Color bottom, Color highlight, Color shade, out Color color)
        {
            color = default;

            bool bowl = y <= 212f && InEllipse(x, y, 128f, 212f, 56f, 86f);
            bool rim = InRect(x, y, 64f, 192f, 207f, 220f);
            float dl = Mathf.Sqrt((x - 68f) * (x - 68f) + (y - 178f) * (y - 178f));
            float dr = Mathf.Sqrt((x - 188f) * (x - 188f) + (y - 178f) * (y - 178f));
            bool handle = (dl >= 18f && dl <= 29f && x <= 90f) || (dr >= 18f && dr <= 29f && x >= 166f);
            bool stem = InRect(x, y, 119f, 137f, 92f, 136f);
            bool knob = InEllipse(x, y, 128f, 98f, 23f, 9f);
            bool plateUpper = InRect(x, y, 92f, 164f, 64f, 90f);
            bool plateLower = InRect(x, y, 74f, 182f, 44f, 66f);

            if (!(bowl || rim || handle || stem || knob || plateUpper || plateLower)) return false;

            // Madalyon: kasenin ortasında halka ve parlak iç
            if (bowl)
            {
                float de = Mathf.Sqrt((x - 128f) * (x - 128f) + (y - 168f) * (y - 168f));
                if (de <= 12f) { color = Color.Lerp(highlight, top, 0.25f); return true; }
                if (de <= 19f) { color = shade; return true; }
            }

            float t = Mathf.InverseLerp(40f, 222f, y);
            Color metal = Color.Lerp(bottom, top, t);

            if (bowl || rim)
            {
                // Sol yansıma şeridi ve sağ gölge
                if (x >= 84f && x <= 96f) metal = Color.Lerp(metal, highlight, 0.75f);
                else if (x > 96f && x <= 106f) metal = Color.Lerp(metal, highlight, 0.30f);
                else if (x > 152f) metal = Color.Lerp(metal, shade, Mathf.Clamp01((x - 152f) / 46f) * 0.6f);
            }
            else if (handle)
            {
                metal = Color.Lerp(metal, shade, 0.25f);
            }
            else if (stem || knob || plateUpper || plateLower)
            {
                if (x < 128f) metal = Color.Lerp(metal, highlight, 0.18f);
                else metal = Color.Lerp(metal, shade, 0.22f);
            }

            color = metal;
            return true;
        }

        // ---------- Işın demeti ----------

        // Ortadan açılan, dışa doğru silikleşen açısal şeritler (beyaz; renk çizimde verilir)
        public static Texture2D Rays()
        {
            if (rays != null) return rays;

            const int size = 512;
            const int stripes = 14;
            rays = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    if (radius >= 1f)
                    {
                        rays.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float phase = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * stripes, 1f);
                    float stripe = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.5f - Mathf.Abs(phase - 0.5f)) / 0.12f)); // yumuşak kenar
                    float alpha = stripe * Mathf.Pow(1f - radius, 1.4f);
                    rays.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            rays.Apply();
            return rays;
        }
    }
}
