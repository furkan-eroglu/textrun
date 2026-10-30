using System;
using System.Collections.Generic;

namespace TextRun.Typing
{
    public enum WordState { Pending, Correct, Wrong }

    // Bir oyuncunun kelime listesi üzerindeki ilerlemesi. Yalnızca mantık içerir, ekran bilgisi yoktur.
    // Büyük/küçük harf farkı hata sayılmaz (otomatik büyük harf yapan klavyeler için).
    public class TypingSession
    {
        readonly List<string> words;
        readonly WordState[] states;
        int correctChars;
        int wrongChars;
        float startTime = -1f;

        public GameLanguage Language { get; }
        public int CurrentIndex { get; private set; }
        public int Count => words.Count;
        public bool Finished => CurrentIndex >= words.Count;
        public int CorrectWords { get; private set; }
        public int WrongWords { get; private set; }

        public TypingSession(IEnumerable<string> source, GameLanguage language)
        {
            Language = language;
            words = new List<string>();
            foreach (var w in source) words.Add(TextNormalizer.ToLower(w, language));
            states = new WordState[words.Count];
        }

        public string WordAt(int index) => words[index];
        public WordState StateAt(int index) => states[index];

        public void MarkStarted(float now)
        {
            if (startTime < 0f) startTime = now;
        }

        // Yazılan kısmın aktif kelimenin başıyla uyuşup uyuşmadığı (anlık geri bildirim için).
        public bool IsPrefixOfCurrent(string typed)
        {
            if (Finished) return false;
            var normalized = TextNormalizer.ToLower(typed, Language);
            return words[CurrentIndex].StartsWith(normalized, StringComparison.Ordinal);
        }

        // Boşluğa basıldığında çağrılır. Kelime doğruysa ilerler ve true döner.
        // Yanlış kelime ilerletmez: oyuncu aynı kelimeyi doğru yazana kadar bekler (sunucudaki kuralla aynı).
        public bool Submit(string typed, float now)
        {
            if (Finished) return false;
            MarkStarted(now);

            var target = words[CurrentIndex];
            var normalized = TextNormalizer.ToLower(typed, Language);

            if (!string.Equals(target, normalized, StringComparison.Ordinal))
            {
                wrongChars += Math.Max(target.Length, normalized.Length) + 1;
                WrongWords++;
                return false;
            }

            correctChars += target.Length + 1; // +1: boşluk
            CorrectWords++;
            states[CurrentIndex] = WordState.Correct;
            CurrentIndex++;
            return true;
        }

        // Sunucu farklı bir sıra bildirirse (ör. yeniden bağlanma) yerel durumu ona eşitler.
        public void SyncTo(int serverIndex)
        {
            serverIndex = Math.Max(0, Math.Min(serverIndex, words.Count));
            for (int i = 0; i < words.Count; i++)
                states[i] = i < serverIndex ? WordState.Correct : WordState.Pending;
            CurrentIndex = serverIndex;
        }

        // Standart WPM: 5 doğru karakter = 1 kelime.
        public float Wpm(float now)
        {
            if (startTime < 0f) return 0f;
            float minutes = (now - startTime) / 60f;
            return minutes < 0.001f ? 0f : correctChars / 5f / minutes;
        }

        public float Accuracy
        {
            get
            {
                int total = correctChars + wrongChars;
                return total == 0 ? 1f : (float)correctChars / total;
            }
        }
    }
}
