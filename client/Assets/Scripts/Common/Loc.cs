using System.Collections.Generic;
using TextRun.Typing;

namespace TextRun.Common
{
    // Merkezi çeviri tablosu: { anahtar -> [Türkçe, İngilizce] }. Yeni dil eklemek için her satıra bir sütun eklenir.
    public static class Loc
    {
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            { "connecting", new[] { "Sunucuya bağlanılıyor...", "Connecting to server..." } },
            { "connectionError", new[] { "Bağlantı kurulamadı", "Could not connect" } },
            { "retry", new[] { "Tekrar dene", "Retry" } },
            { "normalGame", new[] { "Normal Oyun", "Normal Game" } },
            { "privateGame", new[] { "Özel Oyun (yakında)", "Private Game (soon)" } },
            { "searching", new[] { "Karşılaşma aranıyor", "Searching for a match" } },
            { "playersFound", new[] { "Bulunan oyuncu", "Players found" } },
            { "cancel", new[] { "İptal", "Cancel" } },
            { "getReady", new[] { "Hazır ol!", "Get ready!" } },
            { "go", new[] { "BAŞLA!", "GO!" } },
            { "typed", new[] { "Yazılan", "Typed" } },
            { "wpm", new[] { "WPM", "WPM" } },
            { "accuracy", new[] { "Doğruluk", "Accuracy" } },
            { "results", new[] { "Sonuçlar", "Results" } },
            { "playAgain", new[] { "Tekrar oyna", "Play again" } },
            { "menu", new[] { "Menü", "Menu" } },
            { "bot", new[] { "Bot", "Bot" } },
            { "you", new[] { "Sen", "You" } },
            { "raceEnding", new[] { "Yarış bitiyor", "Race ending" } },
            { "youFinished", new[] { "Bitirdin!", "You finished!" } },
            { "positionNow", new[] { "{0}. sıradasın", "Position: {0}" } },
            { "finished", new[] { "Bitirdi", "Finished" } },
            { "dnf", new[] { "Bitiremedi", "Did not finish" } },
            { "disqualified", new[] { "Diskalifiye", "Disqualified" } },
            { "disqualifiedNotice", new[] { "Hız sınırı aşıldı, yarıştan çıkarıldın", "Speed limit exceeded, you were removed from the race" } },
            { "roomFull", new[] { "Oda dolu, tekrar aranıyor...", "Room is full, searching again..." } },
            { "languageMismatch", new[] { "Bu oda başka bir dilde", "This room uses a different language" } },
            { "connectionLost", new[] { "Bağlantı koptu", "Connection lost" } },
            { "server", new[] { "Sunucu adresi", "Server address" } },
            { "connect", new[] { "Bağlan", "Connect" } },
        };

        public static string T(string key, GameLanguage language)
        {
            if (!Table.TryGetValue(key, out var values)) return key;
            return values[language == GameLanguage.Turkish ? 0 : 1];
        }
    }
}
