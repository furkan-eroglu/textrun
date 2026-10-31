using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Game
{
    // Oyun tarzı düğmeler ve paneller (geçici OnGUI arayüzü): yuvarlak köşe, dikey degrade, kalın koyu alt kenar,
    // üstte parlak yansıma, basınca aşağı çökme. Yazılar gölgeli. Tüm yazı durumları (normal/hover/aktif) aynı renkte
    // olduğu için fare üstüne gelince yazı rengi değişmez.
    public static class UiKit
    {
        // Her başarılı tıklamada çağrılır (tıklama sesi için)
        public static Action Clicked;

        static readonly Color PrimaryTop = new Color(0.27f, 0.64f, 1.00f);
        static readonly Color PrimaryBottom = new Color(0.12f, 0.42f, 0.93f);
        static readonly Color PrimaryLip = new Color(0.06f, 0.24f, 0.62f);

        static readonly Color GlassBody = new Color(0.17f, 0.20f, 0.37f, 0.92f);
        static readonly Color GlassLip = new Color(0.08f, 0.10f, 0.22f, 0.95f);

        static readonly Dictionary<string, Texture2D> gradients = new Dictionary<string, Texture2D>();
        static Texture2D playIcon;
        static GUIStyle textStyle;

        // ---------- Altyapı ----------

        static void EnsureResources()
        {
            if (textStyle != null) return;

            textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            SetAllTextColors(textStyle, Color.white);

            // Sağa bakan oynat üçgeni
            const int size = 64;
            playIcon = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // üçgen köşeleri: (14,6) (14,58) (56,32)
                    float t = (x - 14f) / 42f;
                    float halfHeight = 26f * (1f - t);
                    float inside = x >= 14f && x <= 56f ? Mathf.Clamp01(halfHeight - Mathf.Abs(y - 32f) + 0.5f) : 0f;
                    playIcon.SetPixel(x, y, new Color(1f, 1f, 1f, inside));
                }
            }
            playIcon.Apply();
        }

        public static void SetAllTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        static Texture2D Gradient(Color top, Color bottom)
        {
            string key = ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom);
            if (gradients.TryGetValue(key, out var tex) && tex != null) return tex;

            const int h = 64;
            tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < h; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(h - 1))); // y=0 alt satır
            tex.Apply();
            gradients[key] = tex;
            return tex;
        }

        static void Fill(Rect r, Texture tex, Color color, float radius)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) / 2f);
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true, 0f, color, Vector4.zero, new Vector4(rad, rad, rad, rad));
        }

        static void Frame(Rect r, Color color, float radius)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) / 2f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, new Vector4(1.5f, 1.5f, 1.5f, 1.5f), new Vector4(rad, rad, rad, rad));
        }

        static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);
        static bool Pressed(Rect r) => Hover(r) && Input.GetMouseButton(0);

        static void Text(Rect r, string text, int fontSize, Color color)
        {
            textStyle.fontSize = fontSize;
            var old = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.40f * color.a); // yazı gölgesi
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, r.height), text, textStyle);

            GUI.color = color;
            GUI.Label(r, text, textStyle);
            GUI.color = old;
        }

        static bool Click(Rect r)
        {
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) Clicked?.Invoke();
            return clicked;
        }

        // Kalın alt kenarlı "oyun" düğmesi gövdesi. Dönen: yazının ortalanacağı gövde dikdörtgeni.
        static Rect Chunky(Rect r, Color top, Color bottom, Color lip, Color? glassBody, bool pressed, bool hover, float radius, float lipHeight)
        {
            float press = pressed ? lipHeight * 0.7f : 0f;
            var lipRect = new Rect(r.x, r.y + lipHeight - press, r.width, r.height - lipHeight);
            var body = new Rect(r.x, r.y + press, r.width, r.height - lipHeight);

            // gölge, alt kenar, gövde
            Fill(new Rect(lipRect.x, lipRect.y + 5f, lipRect.width, lipRect.height), Texture2D.whiteTexture, new Color(0f, 0f, 0f, 0.30f), radius);
            Fill(lipRect, Texture2D.whiteTexture, lip, radius);

            if (glassBody.HasValue) Fill(body, Texture2D.whiteTexture, glassBody.Value, radius);
            else Fill(body, Gradient(top, bottom), Color.white, radius);

            // üstte parlak yansıma
            Fill(new Rect(body.x + 4f, body.y + 3f, body.width - 8f, body.height * 0.42f), Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.15f), radius * 0.75f);

            if (hover && !pressed) Fill(body, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.09f), radius);
            return body;
        }

        // ---------- Bileşenler ----------

        // Yarı saydam koyu yuvarlak panel
        public static void Panel(Rect r, float alpha, float radius = 18f)
        {
            EnsureResources();
            Fill(r, Texture2D.whiteTexture, new Color(0.05f, 0.06f, 0.14f, alpha), radius);
        }

        // Renkli yuvarlak kart (liste satırı zemini gibi)
        public static void Card(Rect r, Color color, float radius = 12f)
        {
            EnsureResources();
            Fill(r, Texture2D.whiteTexture, color, radius);
        }

        // Ana düğme: parlak mavi, istenirse sol tarafta oynat simgesi
        public static bool PrimaryButton(Rect r, string text, int fontSize = 24, bool playArrow = false)
        {
            EnsureResources();
            bool pressed = Pressed(r);
            var body = Chunky(r, PrimaryTop, PrimaryBottom, PrimaryLip, null, pressed, Hover(r), 20f, 8f);

            if (playArrow)
            {
                float icon = fontSize * 0.95f;
                textStyle.fontSize = fontSize;
                float textWidth = textStyle.CalcSize(new GUIContent(text)).x;
                float total = icon + 12f + textWidth;
                float startX = body.center.x - total / 2f;

                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.35f);
                GUI.DrawTexture(new Rect(startX, body.center.y - icon / 2f + 2f, icon, icon), playIcon);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(startX, body.center.y - icon / 2f, icon, icon), playIcon);
                GUI.color = old;

                Text(new Rect(startX + icon + 12f, body.y, textWidth, body.height), text, fontSize, Color.white);
            }
            else
            {
                Text(body, text, fontSize, Color.white);
            }
            return Click(r);
        }

        // İkincil düğme: koyu cam görünümlü
        public static bool SecondaryButton(Rect r, string text, int fontSize = 20)
        {
            EnsureResources();
            var body = Chunky(r, Color.clear, Color.clear, GlassLip, GlassBody, Pressed(r), Hover(r), 16f, 6f);
            Frame(body, new Color(1f, 1f, 1f, 0.16f), 16f);
            Text(body, text, fontSize, Color.white);
            return Click(r);
        }

        // Kullanılamayan düğme: silik, tıklanmaz
        public static void DisabledButton(Rect r, string text, int fontSize = 20)
        {
            EnsureResources();
            var body = Chunky(r, Color.clear, Color.clear, new Color(0.08f, 0.09f, 0.18f, 0.6f), new Color(0.14f, 0.16f, 0.28f, 0.55f), false, false, 16f, 5f);
            Text(body, text, fontSize, new Color(1f, 1f, 1f, 0.40f));
        }

        // Yan yana iki/çok seçenek (ör. Türkçe | English). Tıklanan indeksi döndürür, tıklanmadıysa -1.
        public static int Segmented(Rect r, string[] options, int selected, int fontSize = 20)
        {
            EnsureResources();
            const float gap = 10f;
            float cellWidth = (r.width - gap * (options.Length - 1)) / options.Length;
            int clicked = -1;

            for (int i = 0; i < options.Length; i++)
            {
                var cell = new Rect(r.x + (cellWidth + gap) * i, r.y, cellWidth, r.height);
                bool isSelected = i == selected;
                bool pressed = Pressed(cell);

                Rect body = isSelected
                    ? Chunky(cell, PrimaryTop, PrimaryBottom, PrimaryLip, null, pressed, Hover(cell), 16f, 6f)
                    : Chunky(cell, Color.clear, Color.clear, GlassLip, GlassBody, pressed, Hover(cell), 16f, 6f);

                Text(body, options[i], fontSize, isSelected ? Color.white : new Color(1f, 1f, 1f, 0.80f));
                if (Click(cell)) clicked = i;
            }
            return clicked;
        }
    }
}
