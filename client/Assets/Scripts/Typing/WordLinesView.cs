using UnityEngine;

namespace TextRun.Typing
{
    // İki satırlık kelime ekranı (geçici OnGUI çizimi). İlk satır bitince ikinci satır yukarı kayar, altına yenisi gelir.
    // Aktif kelime vurgulanır: yazılan kısım doğru gidiyorsa mavi, hata varsa kırmızı.
    public class WordLinesView
    {
        public const float LineHeight = 40f;
        public const float BoxHeight = LineHeight * 2f + 20f;
        const float WordGap = 14f;

        GUIStyle style;
        int lineStart;

        public void Reset()
        {
            lineStart = 0;
        }

        // Kutuya dokunulduysa true döner (klavyeyi açmak için).
        public bool Draw(Rect box, TypingSession session, string liveText)
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = false };

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool tapped = Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition);

            float maxWidth = box.width - 16f;
            int firstLine = FitCount(session, lineStart, maxWidth);
            if (firstLine > 0 && session.CurrentIndex >= lineStart + firstLine)
            {
                lineStart += firstLine;
                firstLine = FitCount(session, lineStart, maxWidth);
            }
            int secondLine = FitCount(session, lineStart + firstLine, maxWidth);

            DrawLine(session, liveText, lineStart, firstLine, box.x + 8f, box.y + 10f);
            DrawLine(session, liveText, lineStart + firstLine, secondLine, box.x + 8f, box.y + 10f + LineHeight);
            return tapped;
        }

        int FitCount(TypingSession session, int start, float maxWidth)
        {
            float x = 0f;
            int count = 0;
            for (int i = start; i < session.Count; i++)
            {
                float w = style.CalcSize(new GUIContent(session.WordAt(i))).x;
                if (x + w > maxWidth && count > 0) break;
                x += w + WordGap;
                count++;
            }
            return count;
        }

        void DrawLine(TypingSession session, string liveText, int start, int count, float x, float y)
        {
            for (int i = start; i < start + count && i < session.Count; i++)
            {
                string word = session.WordAt(i);
                var size = style.CalcSize(new GUIContent(word));
                var rect = new Rect(x, y, size.x, LineHeight - 4f);

                Color color;
                if (i == session.CurrentIndex)
                {
                    bool good = session.IsPrefixOfCurrent(liveText ?? "");
                    GUI.color = good ? new Color(0.25f, 0.4f, 0.7f, 1f) : new Color(0.75f, 0.25f, 0.25f, 1f);
                    GUI.DrawTexture(new Rect(rect.x - 3f, rect.y, rect.width + 6f, rect.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    color = Color.white;
                }
                else
                {
                    color = session.StateAt(i) == WordState.Correct ? new Color(0.4f, 0.85f, 0.5f) : new Color(0.72f, 0.74f, 0.8f);
                }

                style.normal.textColor = color;
                GUI.Label(rect, word, style);
                x += size.x + WordGap;
            }
        }
    }
}
