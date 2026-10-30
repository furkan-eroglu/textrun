using System.Collections.Generic;
using System.IO;
using System.Text;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.KeyboardTest
{
    // Klavye prototipi: iki satır kelime, anlık yazma geri bildirimi ve klavye davranış sayaçları.
    // Arayüz geçici olarak OnGUI ile çizilir; asıl oyun arayüzü Faz 4'te yapılacak.
    public class KeyboardTestScreen : MonoBehaviour
    {
        const float LineHeight = 40f;
        const float WordGap = 14f;
        const int WordCount = 150;

        readonly KeyboardInput keyboard = new KeyboardInput();
        readonly List<string> log = new List<string>();

        GameLanguage language = GameLanguage.Turkish;
        KeyboardKind kind = KeyboardKind.Default;
        TypingSession session;
        string prevText = "";
        int lineStart;
        bool keyboardWasOpen;

        int jumps, capsEvents, batches, maxBatch, backspaces;

        GUIStyle wordStyle, smallStyle, buttonStyle;

        void Start()
        {
            Application.targetFrameRate = 60;
            ResetSession();
        }

        void ResetSession()
        {
            session = new TypingSession(SampleWords.Generate(language, WordCount, 7), language);
            lineStart = 0;
            jumps = capsEvents = batches = maxBatch = backspaces = 0;
            prevText = "";
            keyboard.Text = "";
            log.Clear();
            AddLog("Sifirlandi: " + language + ", klavye tipi " + kind);
        }

        void AddLog(string message)
        {
            log.Add(message);
            if (log.Count > 200) log.RemoveAt(0);
        }

        void Update()
        {
            bool open = keyboard.IsOpen;
            if (keyboardWasOpen && !open) AddLog("Klavye kapandi (durum: " + keyboard.StatusText + ")");
            keyboardWasOpen = open;

            string text = keyboard.Text ?? "";
            if (text != prevText) Analyze(prevText, text);

            int space = text.IndexOf(' ');
            if (space >= 0)
            {
                string typed = text.Substring(0, space);
                string rest = text.Substring(space + 1).TrimStart();
                if (typed.Length > 0 && !session.Finished)
                {
                    string expected = session.WordAt(session.CurrentIndex);
                    bool ok = session.Submit(typed, Time.realtimeSinceStartup);
                    if (!ok) AddLog("Yanlis: beklenen '" + expected + "', yazilan '" + typed + "'");
                }
                keyboard.Text = rest;
                text = rest;
            }

            prevText = text;
        }

        // Klavyenin metni her karede nasıl değiştirdiğine bakar; beklenmedik değişiklikler otomatik düzeltme belirtisidir.
        void Analyze(string prev, string cur)
        {
            if (prev.Length == 0 && cur.Length > 0) session.MarkStarted(Time.realtimeSinceStartup);

            if (cur.Length == prev.Length + 1 && cur.StartsWith(prev, System.StringComparison.Ordinal))
            {
                char c = cur[cur.Length - 1];
                if (char.IsUpper(c))
                {
                    capsEvents++;
                    AddLog("Buyuk harf geldi: '" + c + "' (otomatik buyuk harf olabilir)");
                }
            }
            else if (cur.Length == prev.Length - 1 && prev.StartsWith(cur, System.StringComparison.Ordinal))
            {
                backspaces++;
            }
            else if (cur.Length > prev.Length && cur.StartsWith(prev, System.StringComparison.Ordinal))
            {
                int added = cur.Length - prev.Length;
                batches++;
                maxBatch = Mathf.Max(maxBatch, added);
                AddLog("Tek karede " + added + " harf geldi: '" + cur.Substring(prev.Length) + "'");
            }
            else
            {
                jumps++;
                AddLog("DEGISIM: '" + prev + "' -> '" + cur + "'");
            }
        }

        void EnsureStyles()
        {
            if (wordStyle != null) return;
            wordStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = false };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }

        void OnGUI()
        {
            keyboard.HandleGuiEvent(Event.current);
            EnsureStyles();

            float scale = Mathf.Max(1.2f, Screen.dpi > 0f ? Screen.dpi / 160f : 1.2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Screen.width / scale;
            float top = (Screen.height - Screen.safeArea.yMax) / scale + 6f;
            float bottom = (Screen.height - keyboard.AreaHeight) / scale - 6f;
            float y = top;

            DrawButtons(width, ref y);
            DrawStats(width, ref y);

            var box = new Rect(8f, y, width - 16f, LineHeight * 2f + 20f);
            DrawWordBox(box);
            y = box.yMax + 6f;

            string live = keyboard.Text ?? "";
            GUI.Label(new Rect(8f, y, width - 16f, 30f), live.Length == 0 ? "Yazmak icin kutuya dokun" : "Yazilan: " + live, wordStyle);
            y += 38f;

            DrawLog(new Rect(8f, y, width - 16f, Mathf.Max(0f, bottom - y)));
        }

        void DrawButtons(float width, ref float y)
        {
            float w = (width - 16f - 18f) / 4f;
            float x = 8f;

            if (GUI.Button(new Rect(x, y, w, 38f), language == GameLanguage.Turkish ? "Dil: TR" : "Dil: EN", buttonStyle))
            {
                language = language == GameLanguage.Turkish ? GameLanguage.English : GameLanguage.Turkish;
                ResetSession();
            }
            x += w + 6f;

            if (GUI.Button(new Rect(x, y, w, 38f), kind == KeyboardKind.Default ? "Tip: Normal" : "Tip: ASCII", buttonStyle))
            {
                kind = kind == KeyboardKind.Default ? KeyboardKind.AsciiCapable : KeyboardKind.Default;
                keyboard.Open(kind);
                ResetSession();
            }
            x += w + 6f;

            if (GUI.Button(new Rect(x, y, w, 38f), "Sifirla", buttonStyle)) ResetSession();
            x += w + 6f;

            if (GUI.Button(new Rect(x, y, w, 38f), "Rapor", buttonStyle)) SaveReport();

            y += 44f;
        }

        void DrawStats(float width, ref float y)
        {
            string stats = "WPM " + session.Wpm(Time.realtimeSinceStartup).ToString("0")
                + "  Dogruluk %" + (session.Accuracy * 100f).ToString("0")
                + "  Kelime " + session.CurrentIndex + "/" + session.Count;
            string kb = "Klavye " + (keyboard.IsNative ? "yerel" : "masaustu")
                + " yuk " + Mathf.RoundToInt(keyboard.AreaHeight) + "px"
                + " | degisim " + jumps + " | buyuk harf " + capsEvents
                + " | toplu " + batches + " (en cok " + maxBatch + ")";

            GUI.Label(new Rect(8f, y, width - 16f, 24f), stats, smallStyle);
            GUI.Label(new Rect(8f, y + 20f, width - 16f, 40f), kb, smallStyle);
            y += 54f;
        }

        void DrawWordBox(Rect box)
        {
            GUI.color = new Color(0.1f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition))
                keyboard.Open(kind);

            float maxWidth = box.width - 16f;
            int firstLine = FitCount(lineStart, maxWidth);
            if (firstLine > 0 && session.CurrentIndex >= lineStart + firstLine)
            {
                lineStart += firstLine;
                firstLine = FitCount(lineStart, maxWidth);
            }
            int secondLine = FitCount(lineStart + firstLine, maxWidth);

            DrawLine(lineStart, firstLine, box.x + 8f, box.y + 10f);
            DrawLine(lineStart + firstLine, secondLine, box.x + 8f, box.y + 10f + LineHeight);
        }

        int FitCount(int start, float maxWidth)
        {
            float x = 0f;
            int count = 0;
            for (int i = start; i < session.Count; i++)
            {
                float w = wordStyle.CalcSize(new GUIContent(session.WordAt(i))).x;
                if (x + w > maxWidth && count > 0) break;
                x += w + WordGap;
                count++;
            }
            return count;
        }

        void DrawLine(int start, int count, float x, float y)
        {
            for (int i = start; i < start + count && i < session.Count; i++)
            {
                string word = session.WordAt(i);
                var size = wordStyle.CalcSize(new GUIContent(word));
                var rect = new Rect(x, y, size.x, LineHeight - 4f);

                Color color;
                if (i == session.CurrentIndex)
                {
                    bool good = session.IsPrefixOfCurrent(keyboard.Text ?? "");
                    GUI.color = good ? new Color(0.25f, 0.4f, 0.7f, 1f) : new Color(0.75f, 0.25f, 0.25f, 1f);
                    GUI.DrawTexture(new Rect(rect.x - 3f, rect.y, rect.width + 6f, rect.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    color = Color.white;
                }
                else
                {
                    switch (session.StateAt(i))
                    {
                        case WordState.Correct: color = new Color(0.4f, 0.85f, 0.5f); break;
                        case WordState.Wrong: color = new Color(0.95f, 0.4f, 0.4f); break;
                        default: color = new Color(0.72f, 0.74f, 0.8f); break;
                    }
                }

                wordStyle.normal.textColor = color;
                GUI.Label(rect, word, wordStyle);
                x += size.x + WordGap;
            }
        }

        void DrawLog(Rect area)
        {
            if (area.height < 20f) return;

            smallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
            int lines = Mathf.Max(1, Mathf.FloorToInt(area.height / 18f));
            int from = Mathf.Max(0, log.Count - lines);

            var sb = new StringBuilder();
            for (int i = from; i < log.Count; i++) sb.AppendLine(log[i]);
            GUI.Label(area, sb.ToString(), smallStyle);
        }

        void SaveReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Cihaz: " + SystemInfo.deviceModel + " / " + SystemInfo.operatingSystem);
            sb.AppendLine("Dil: " + language + ", klavye tipi: " + kind + ", yerel: " + keyboard.IsNative);
            sb.AppendLine("Kelime: dogru " + session.CorrectWords + ", yanlis " + session.WrongWords
                + ", WPM " + session.Wpm(Time.realtimeSinceStartup).ToString("0")
                + ", dogruluk %" + (session.Accuracy * 100f).ToString("0"));
            sb.AppendLine("Degisim (otomatik duzeltme belirtisi): " + jumps);
            sb.AppendLine("Buyuk harf: " + capsEvents + ", toplu giris: " + batches + " (en cok " + maxBatch + " harf), silme: " + backspaces);
            sb.AppendLine("--- olaylar ---");
            foreach (var line in log) sb.AppendLine(line);

            string path = Path.Combine(Application.persistentDataPath, "keyboard-report.txt");
            File.WriteAllText(path, sb.ToString());
            AddLog("Rapor kaydedildi: " + path);
        }
    }
}
