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
            { "privateGame", new[] { "Özel Oyun", "Private Game" } },
            { "createRoom", new[] { "Oda Kur", "Create Room" } },
            { "joinWithCode", new[] { "Kodla Katıl", "Join with Code" } },
            { "back", new[] { "Geri", "Back" } },
            { "roomCode", new[] { "Oda kodu", "Room code" } },
            { "copyCode", new[] { "Kodu Kopyala", "Copy Code" } },
            { "copied", new[] { "Kopyalandı!", "Copied!" } },
            { "startRace", new[] { "Yarışı Başlat", "Start Race" } },
            { "needTwo", new[] { "En az 2 oyuncu gerekli", "At least 2 players needed" } },
            { "waitHost", new[] { "Kurucunun başlatması bekleniyor...", "Waiting for the host to start..." } },
            { "noBots", new[] { "Botsuz", "No bots" } },
            { "withBots", new[] { "Botlu", "With bots" } },
            { "leaveRoom", new[] { "Odadan Ayrıl", "Leave Room" } },
            { "hostTag", new[] { "Kurucu", "Host" } },
            { "enterCode", new[] { "Oda kodunu gir", "Enter room code" } },
            { "join", new[] { "Katıl", "Join" } },
            { "roomNotFound", new[] { "Oda bulunamadı", "Room not found" } },
            { "roomStarted", new[] { "Bu odada yarış başlamış", "The race in this room has started" } },
            { "roomIsFull", new[] { "Oda dolu", "Room is full" } },
            { "roomLang", new[] { "Oda dili", "Room language" } },
            { "players", new[] { "Oyuncular", "Players" } },
            { "searching", new[] { "Karşılaşma aranıyor", "Searching for a match" } },
            { "playersFound", new[] { "Bulunan oyuncu", "Players found" } },
            { "cancel", new[] { "İptal", "Cancel" } },
            { "matchFound", new[] { "Karşılaşma bulundu!", "Match found!" } },
            { "matchFoundSub", new[] { "Yarış birazdan başlıyor", "The race starts soon" } },
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
            { "welcomeTitle", new[] { "Hoş geldin!", "Welcome!" } },
            { "guest", new[] { "Misafir olarak devam et", "Continue as guest" } },
            { "signGoogle", new[] { "Google ile giriş", "Sign in with Google" } },
            { "signApple", new[] { "Apple ile giriş", "Sign in with Apple" } },
            { "soon", new[] { "yakında", "soon" } },
            { "welcomeNote", new[] { "Şimdilik ilerlemen bu telefonda saklanır. Hesap bağlama yakında eklenecek.", "For now your progress is kept on this phone. Account linking is coming soon." } },
            { "chooseName", new[] { "Oyuncu adını seç", "Choose your player name" } },
            { "nameRule", new[] { "3-12 karakter: harf, rakam, boşluk", "3-12 characters: letters, digits, space" } },
            { "nameInvalid", new[] { "Bu ad olmaz. 3-12 karakter, harf ve rakam kullan.", "Not allowed. Use 3-12 letters or digits." } },
            { "continue", new[] { "Devam", "Continue" } },
            { "save", new[] { "Kaydet", "Save" } },
            { "saved", new[] { "Kaydedildi", "Saved" } },
            { "settings", new[] { "Ayarlar", "Settings" } },
            { "playerName", new[] { "Oyuncu adı", "Player name" } },
            { "level", new[] { "Seviye", "Level" } },
            { "levelUp", new[] { "SEVİYE ATLADIN!", "LEVEL UP!" } },
            { "raceCount", new[] { "Yarış", "Races" } },
            { "winCount", new[] { "Zafer", "Wins" } },
        };

        public static string T(string key, GameLanguage language)
        {
            if (!Table.TryGetValue(key, out var values)) return key;
            return values[language == GameLanguage.Turkish ? 0 : 1];
        }
    }
}
