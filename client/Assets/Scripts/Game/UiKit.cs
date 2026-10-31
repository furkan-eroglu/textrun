using UnityEngine;

namespace TextRun.Game
{
    // Modern düğme ve panel çizimleri (geçici OnGUI arayüzü için): yuvarlak köşeler, degrade, hafif gölge,
    // üstüne gelince/basınca tepki. Metinlerin tüm durumları (normal/hover/aktif) aynı renkte olduğu için
    // fare üstüne gelince yazı rengi değişmez.
    public static class UiKit
    {
        static readonly Color GradientLeft = new Color(0.16f, 0.72f, 1.00f);
        static readonly Color GradientRight = new Color(0.58f, 0.34f, 1.00f);

        static Texture2D primaryGradient;
        static GUIStyle textStyle;

        // ---------- Temel çizimler ----------

        static void EnsureResources()
        {
            if (primaryGradient == null)
            {
                const int width = 128;
                primaryGradient = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                for (int x = 0; x < width; x++) primaryGradient.SetPixel(x, 0, Color.Lerp(GradientLeft, GradientRight, x / (float)(width - 1)));
                primaryGradient.Apply();
            }

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
                // Hover/aktif durumlarında da aynı renk: yazı üstüne gelince renk değiştirmesin
                SetAllTextColors(textStyle, Color.white);
            }
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

        static void Fill(Rect r, Texture tex, Color color, float radius)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) / 2f);
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true, 0f, color, Vector4.zero, new Vector4(rad, rad, rad, rad));
        }

        static void Label(Rect r, string text, int fontSize, Color color)
        {
            EnsureResources();
            textStyle.fontSize = fontSize;
            var old = GUI.color;
            GUI.color = color; // yazı rengi beyaz kalır, renk ve saydamlık buradan uygulanır
            GUI.Label(r, text, textStyle);
            GUI.color = old;
        }

        static bool Hover(Rect r)
        {
            return r.Contains(Event.current.mousePosition);
        }

        static bool Pressed(Rect r)
        {
            return Hover(r) && Input.GetMouseButton(0);
        }

        // ---------- Bileşenler ----------

        // Yarı saydam koyu yuvarlak panel
        public static void Panel(Rect r, float alpha, float radius = 18f)
        {
            EnsureResources();
            Fill(r, Texture2D.whiteTexture, new Color(0.05f, 0.06f, 0.14f, alpha), radius);
        }

        // Ana düğme: yatay degrade, gölge, üstüne gelince hafif parlar, basılınca kararır
        public static bool PrimaryButton(Rect r, string text, int fontSize = 24)
        {
            EnsureResources();
            float radius = r.height / 2f;

            Fill(new Rect(r.x, r.y + 4f, r.width, r.height), Texture2D.whiteTexture, new Color(0f, 0f, 0f, 0.35f), radius);
            Fill(r, primaryGradient, Color.white, radius);

            if (Pressed(r)) Fill(r, Texture2D.whiteTexture, new Color(0f, 0f, 0f, 0.18f), radius);
            else if (Hover(r)) Fill(r, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.14f), radius);

            Label(r, text, fontSize, Color.white);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // İkincil düğme: yarı saydam beyaz zemin, ince çerçeve
        public static bool SecondaryButton(Rect r, string text, int fontSize = 20)
        {
            EnsureResources();
            float radius = r.height / 2f;

            float alpha = Pressed(r) ? 0.26f : Hover(r) ? 0.20f : 0.12f;
            Fill(r, Texture2D.whiteTexture, new Color(1f, 1f, 1f, alpha), radius);
            Frame(r, new Color(1f, 1f, 1f, 0.35f), radius);

            Label(r, text, fontSize, Color.white);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // Kullanılamayan düğme: silik, tıklanmaz
        public static void DisabledButton(Rect r, string text, int fontSize = 20)
        {
            EnsureResources();
            float radius = r.height / 2f;

            Fill(r, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.06f), radius);
            Frame(r, new Color(1f, 1f, 1f, 0.14f), radius);
            Label(r, text, fontSize, new Color(1f, 1f, 1f, 0.45f));
        }

        // Yan yana seçenekler (ör. Türkçe | English). Seçilene tıklanan indeksi döndürür, tıklanmadıysa -1.
        public static int Segmented(Rect r, string[] options, int selected, int fontSize = 20)
        {
            EnsureResources();
            float radius = r.height / 2f;
            const float inset = 4f;

            Fill(r, Texture2D.whiteTexture, new Color(0.04f, 0.05f, 0.13f, 0.60f), radius);
            Frame(r, new Color(1f, 1f, 1f, 0.18f), radius);

            float cellWidth = r.width / options.Length;
            int clicked = -1;

            for (int i = 0; i < options.Length; i++)
            {
                var cell = new Rect(r.x + cellWidth * i, r.y, cellWidth, r.height);
                var pill = new Rect(cell.x + inset, cell.y + inset, cell.width - inset * 2f, cell.height - inset * 2f);

                if (i == selected)
                {
                    Fill(pill, primaryGradient, Color.white, pill.height / 2f);
                }
                else if (Hover(cell))
                {
                    Fill(pill, Texture2D.whiteTexture, new Color(1f, 1f, 1f, 0.10f), pill.height / 2f);
                }

                Label(cell, options[i], fontSize, i == selected ? Color.white : new Color(1f, 1f, 1f, 0.75f));
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) clicked = i;
            }
            return clicked;
        }

        // İnce yuvarlak çerçeve (içi boş)
        static void Frame(Rect r, Color color, float radius)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) / 2f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, new Vector4(1.5f, 1.5f, 1.5f, 1.5f), new Vector4(rad, rad, rad, rad));
        }
    }
}
