namespace TextRun.Typing
{
    public enum GameLanguage { Turkish, English }

    // Türkçede I/ı ve İ/i çiftleri standart küçük harf dönüşümüyle karışır, bu yüzden elle yapılır.
    public static class TextNormalizer
    {
        public static string ToLower(string text, GameLanguage language)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                chars[i] = ToLower(chars[i], language);
            return new string(chars);
        }

        public static char ToLower(char c, GameLanguage language)
        {
            if (language == GameLanguage.Turkish)
            {
                if (c == 'I') return 'ı'; // ı
                if (c == 'İ') return 'i'; // İ
            }
            return char.ToLowerInvariant(c);
        }
    }
}
