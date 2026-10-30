using UnityEngine;

namespace TextRun.Typing
{
    // İki satırlık kelime ekranı (geçici OnGUI çizimi). İlk satır bitince ikinci satır yukarı kayar, altına yenisi gelir.
    // Aktif kelime vurgulanır: yazılan kısım doğru gidiyorsa mavi, hata varsa kırmızı.
    public class WordLinesView
    {
        public const float LineHeight = 32f;
        public const float BoxHeight = LineHeight * 2f + 16f;
        const float WordGap = 11f;

        static readonly Color CorrectColor = new Color(0.4f, 0.85f, 0.5f);   // doğru yazılan: yeşil
        static readonly Color WrongColor = new Color(0.95f, 0.4f, 0.4f);     // yanlış: kırmızı
        static readonly Color RemainingColor = new Color(0.78f, 0.8f, 0.85f); // aktif kelimede sırada bekleyen harfler: açık gri
        static readonly Color UpcomingColor = new Color(0.55f, 0.57f, 0.65f); // sıradaki kelimeler: gri

        const float MistakeFlashSeconds = 0.15f;

        GUIStyle style;
        int lineStart;
        float flashUntil;

        public void Reset()
        {
            lineStart = 0;
            flashUntil = 0f;
        }

        // Yanlış tuşa basıldığında aktif kelimenin zemini kısa süre kırmızıya döner (harfler değişmez).
        public void FlashMistake()
        {
            flashUntil = Time.realtimeSinceStartup + MistakeFlashSeconds;
        }

        // Kutuya dokunulduysa true döner (klavyeyi açmak için).
        public bool Draw(Rect box, TypingSession session, string liveText)
        {
            // Boşluk (padding) sıfır: bir kelime birkaç renkli parça olarak çizildiğinde parçalar arasında aralık oluşmasın.
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };

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

            DrawLine(session, liveText, lineStart, firstLine, box.x + 8f, box.y + 8f);
            DrawLine(session, liveText, lineStart + firstLine, secondLine, box.x + 8f, box.y + 8f + LineHeight);
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

                if (i == session.CurrentIndex)
                {
                    // Aktif kelime: arka plan vurgusu + harf harf renk. Hata varsa arka plan hafif kırmızıya döner.
                    string typed = liveText ?? "";
                    CountMatch(session, word, typed, out int match, out int wrong);

                    bool flashing = Time.realtimeSinceStartup < flashUntil;
                    GUI.color = wrong > 0 || flashing ? new Color(0.38f, 0.18f, 0.2f, 1f) : new Color(0.2f, 0.26f, 0.42f, 1f);
                    GUI.DrawTexture(new Rect(rect.x - 4f, rect.y, rect.width + 8f, rect.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    DrawSegments(rect, word, match, wrong);
                }
                else
                {
                    style.normal.textColor = session.StateAt(i) == WordState.Correct ? CorrectColor : UpcomingColor;
                    GUI.Label(rect, word, style);
                }

                x += size.x + WordGap;
            }
        }

        // Yazılanın baştan kaç harfi doğru (match), ilk hatadan sonra kaç harfi yanlış (wrong) olduğunu sayar.
        static void CountMatch(TypingSession session, string word, string typed, out int match, out int wrong)
        {
            match = 0;
            while (match < typed.Length && match < word.Length && TextNormalizer.ToLower(typed[match], session.Language) == word[match]) match++;
            wrong = Mathf.Min(typed.Length, word.Length) - match;
        }

        // Kelimeyi üç renkli parça olarak çizer: doğru yazılan (yeşil), yanlış (kırmızı), henüz yazılmayan (gri).
        void DrawSegments(Rect rect, string word, int match, int wrong)
        {
            float x = rect.x;
            x = DrawSegment(x, rect, word.Substring(0, match), CorrectColor);
            x = DrawSegment(x, rect, word.Substring(match, wrong), WrongColor);
            DrawSegment(x, rect, word.Substring(match + wrong), RemainingColor);
        }

        float DrawSegment(float x, Rect rect, string text, Color color)
        {
            if (text.Length == 0) return x;
            var size = style.CalcSize(new GUIContent(text));
            style.normal.textColor = color;
            GUI.Label(new Rect(x, rect.y, size.x + 2f, rect.height), text, style);
            return x + size.x;
        }
    }
}
