using UnityEngine;

namespace TextRun.Game
{
    // Ana menü logosu: eğik, kalın "TEXT" (beyaz) + "RUN" (turuncu), koyu çerçeve ve gölge, alt kısmı daha koyu iki tonlu yüzey.
    // Menü açılınca harfler soldan hız çizgileriyle kayarak gelir (bir kez); altta damalı bayrak şeridi açılır.
    public class GameLogo
    {
        static readonly Color Outline = new Color(0.03f, 0.07f, 0.20f);
        static readonly Color TextTop = new Color(1f, 1f, 1f);
        static readonly Color TextBottom = new Color(0.62f, 0.78f, 1f);
        static readonly Color RunTop = new Color(1f, 0.84f, 0.32f);
        static readonly Color RunBottom = new Color(1f, 0.45f, 0.12f);

        GUIStyle style;

        // Logonun kapladığı yükseklik (altındaki öğeleri yerleştirmek için)
        public const float Height = 120f;

        // centerY: logo yazısının dikey ortası. elapsed: menü açıldığından beri geçen süre (animasyon).
        public void Draw(float width, float centerY, float elapsed)
        {
            float size = Mathf.Min(62f, width * 0.15f);
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            style.fontSize = Mathf.RoundToInt(size);

            const string text = "TEXTRUN";
            const int split = 4; // "TEXT" | "RUN"
            var widths = new float[text.Length];
            float total = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                widths[i] = style.CalcSize(new GUIContent(text[i].ToString())).x - 2f; // harfler biraz sıkı dursun
                total += widths[i];
            }

            float x = width / 2f - total / 2f;
            float height = size * 1.3f;
            float top = centerY - height / 2f;
            var old = GUI.color;

            // Hız çizgileri: harflerin geldiği yönde (solda), harfler yerine oturunca kaybolur
            float lineFade = Mathf.Clamp01(1f - (elapsed - 0.55f) / 0.5f);
            if (lineFade > 0f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float len = (60f - i * 16f) * (0.5f + 0.5f * Mathf.Clamp01(elapsed / 0.3f));
                    GUI.color = new Color(1f, 1f, 1f, 0.55f * lineFade);
                    GUI.DrawTexture(new Rect(x - 12f - len, centerY - 16f + i * 15f, len, 4f), Texture2D.whiteTexture);
                }
            }

            GUI.color = old;
            float cursor = x;
            for (int i = 0; i < text.Length; i++)
            {
                float u = Mathf.Clamp01((elapsed - i * 0.07f) / 0.4f);
                float pop = 1f + 2.70158f * Mathf.Pow(u - 1f, 3f) + 1.70158f * Mathf.Pow(u - 1f, 2f); // yaylanarak oturur
                float slide = (1f - pop) * 70f;                                                       // soldan gelir
                var rect = new Rect(cursor + slide, top, widths[i], height);

                bool run = i >= split;
                Draw(rect, text[i].ToString(), run ? RunTop : TextTop, run ? RunBottom : TextBottom, u);
                cursor += widths[i];
            }

            // Altta damalı bayrak şeridi: ortadan iki yana açılır
            float stripT = Mathf.Clamp01((elapsed - 0.5f) / 0.4f);
            float stripWidth = total * stripT;
            const float cell = 7f;
            float stripY = top + height * 0.9f;
            int columns = Mathf.Max(0, Mathf.FloorToInt(stripWidth / cell));
            float stripX = width / 2f - columns * cell / 2f;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    bool dark = (row + col) % 2 == 0;
                    GUI.color = dark ? new Color(0.03f, 0.07f, 0.20f, 0.95f) : new Color(1f, 1f, 1f, 0.95f);
                    GUI.DrawTexture(new Rect(stripX + col * cell, stripY + row * cell, cell, cell), Texture2D.whiteTexture);
                }
            }

            GUI.color = old;
        }

        // Tek harf: gölge, koyu çerçeve (8 yönde), üst yarı açık ton, alt yarı koyu ton
        void Draw(Rect rect, string letter, Color top, Color bottom, float alpha)
        {
            Colorize(new Color(0f, 0f, 0f, 0.38f * alpha));
            GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, rect.height), letter, style);

            Colorize(new Color(Outline.r, Outline.g, Outline.b, alpha));
            for (int k = 0; k < 8; k++)
            {
                float angle = k * Mathf.PI / 4f;
                GUI.Label(new Rect(rect.x + Mathf.Cos(angle) * 4.5f, rect.y + Mathf.Sin(angle) * 4.5f, rect.width, rect.height), letter, style);
            }

            Colorize(new Color(top.r, top.g, top.b, alpha));
            GUI.Label(rect, letter, style);

            // Alt yarı: aynı harf daha koyu tonda, yalnızca alt kısmı görünür
            float cut = rect.height * 0.54f;
            GUI.BeginGroup(new Rect(rect.x - 6f, rect.y + cut, rect.width + 12f, rect.height - cut));
            Colorize(new Color(bottom.r, bottom.g, bottom.b, alpha));
            GUI.Label(new Rect(6f, -cut, rect.width, rect.height), letter, style);
            GUI.EndGroup();
        }

        // Tüm durumlara aynı renk (üstüne gelince renk değişmesin)
        void Colorize(Color c)
        {
            style.normal.textColor = c;
            style.hover.textColor = c;
            style.active.textColor = c;
            style.focused.textColor = c;
        }
    }
}
