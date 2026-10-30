using System;

namespace TextRun.Typing
{
    public enum KeyResult
    {
        Accepted,   // doğru harf, kelime ilerledi
        Rejected,   // yanlış harf, yok sayıldı
        Submit,     // kelime tamam ve boşluğa basıldı: kelime gönderilmeli
        Ignored     // etkisiz tuş (kelime bitmeden boşluk gibi)
    }

    // Harf harf giriş kuralı: yalnızca sıradaki DOĞRU harf kabul edilir, yanlış tuşlar yok sayılır.
    // Böylece oyuncu hatalı harfleri silmek zorunda kalmaz; kelime doğru harfi bekler.
    // Büyük/küçük harf farkı yoktur (otomatik büyük harf yapan klavyeler için). Kelime bitince boşluk ile gönderilir.
    public class WordInput
    {
        readonly GameLanguage language;
        string expected = "";

        public string Entered { get; private set; } = "";
        public int MistakesThisWord { get; private set; }
        public bool Complete => expected.Length > 0 && Entered.Length == expected.Length;

        public WordInput(GameLanguage language)
        {
            this.language = language;
        }

        public void Begin(string word)
        {
            expected = word ?? "";
            Entered = "";
            MistakesThisWord = 0;
        }

        public KeyResult Press(char c)
        {
            if (expected.Length == 0) return KeyResult.Ignored;

            if (c == ' ') return Complete ? KeyResult.Submit : KeyResult.Ignored;
            if (char.IsControl(c)) return KeyResult.Ignored;

            if (Entered.Length < expected.Length && TextNormalizer.ToLower(c, language) == expected[Entered.Length])
            {
                Entered += expected[Entered.Length];
                return KeyResult.Accepted;
            }

            MistakesThisWord++;
            return KeyResult.Rejected;
        }

        // Silme tuşu: yalnızca doğru harflerden oluşan girişin sonundan harf siler.
        public void TrimTo(int length)
        {
            length = Math.Max(0, Math.Min(length, Entered.Length));
            Entered = Entered.Substring(0, length);
        }
    }
}
