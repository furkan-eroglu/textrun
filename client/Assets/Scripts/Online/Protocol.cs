using System;

namespace TextRun.Online
{
    // Sunucuyla konuşma kuralları. Ayrıntı: docs/PROTOCOL.md. Alan adları sunucunun JSON'uyla birebir aynı olmalıdır.
    public static class Op
    {
        public const long Submit = 1;
        public const long Start = 2; // özel oda: kurucu yarışı başlatır
        public const long Snapshot = 100;
        public const long Roster = 101;
        public const long Countdown = 102;
        public const long Go = 103;
        public const long Progress = 104;
        public const long Ack = 105;
        public const long Results = 106;
        public const long Ending = 107; // ilk oyuncu bitirdi: yarışın biteceği an (geri sayım)
    }

    public static class Phase
    {
        public const string Lobby = "lobby";
        public const string Countdown = "countdown";
        public const string Racing = "racing";
        public const string Finished = "finished";
    }

    public static class AckKind
    {
        public const string Correct = "correct";
        public const string Wrong = "wrong";
        public const string Finished = "finished";
        public const string Disqualified = "disqualified";
    }

    [Serializable]
    public class PlayerDto
    {
        public string id;
        public string name;
        public bool isBot;
        public int slot;
        public int index;
        public bool connected;
        public bool finished;
        public bool disqualified;
    }

    [Serializable]
    public class ResultDto
    {
        public string userId;
        public string name;
        public bool isBot;
        public int rank;
        public string status; // finished | dnf | disqualified
        public int finishMs;
        public float wpm;
        public float accuracy;
        public int correctWords;
        public int errors;
    }

    // SNAPSHOT, ROSTER ve COUNTDOWN mesajlarının ortak gövdesi
    [Serializable]
    public class SnapshotDto
    {
        public string phase;
        public string lang;
        public string kind;  // public | private
        public string code;  // özel oda kodu
        public string host;  // özel oda kurucusunun kimliği
        public long serverNow;
        public long lobbyEndsAt;
        public long startAt;
        public long endsAt; // 0 = bitiş geri sayımı başlamadı
        public string[] words;
        public PlayerDto[] players;
        public ResultDto[] results;
    }

    [Serializable]
    public class GoDto
    {
        public long serverNow;
        public long startAt;
    }

    [Serializable]
    public class EndingDto
    {
        public long serverNow;
        public long endsAt;
    }

    [Serializable]
    public class ProgressDto
    {
        public long t;
        public PlayerDto[] players;
    }

    [Serializable]
    public class AckDto
    {
        public bool ok;
        public string kind;
        public int index;
    }

    [Serializable]
    public class ResultsDto
    {
        public long serverNow;
        public ResultDto[] results;
        public RewardDto[] rewards; // gerçek oyunculara verilen XP (özel odada tek başına oynanırsa boş)
    }

    [Serializable]
    public class RewardDto
    {
        public string userId;
        public int gain;
        public int xp;
        public int level;
        public int xpIntoLevel; // yeni seviyede biriktirilen XP
        public int xpForNext;   // yeni seviyeyi geçmek için gereken XP
        public bool leveledUp;
    }

    // Oyuncu profili (sunucuda tutulur). name boşsa oyuncu henüz ad seçmemiştir.
    [Serializable]
    public class ProfileDto
    {
        public bool guest;   // misafir: XP, seviye, ödül yok
        public string name;
        public int xp;
        public int level = 1;
        public int xpIntoLevel;
        public int xpForNext = 100;
        public int races;
        public int wins;
    }

    [Serializable]
    public class NameRequest
    {
        public string name;
    }

    [Serializable]
    public class SubmitDto
    {
        public string w;
        public int e; // bu kelimede basılan yanlış tuş sayısı (yalnızca doğruluk istatistiği için)
    }

    [Serializable]
    public class FindRaceRequest
    {
        public string lang;
    }

    [Serializable]
    public class RoomRequest
    {
        public string lang;
        public string code;
    }

    [Serializable]
    public class RoomResponse
    {
        public string matchId;
        public string code;
        public string lang;
    }

    [Serializable]
    public class StartDto
    {
        public bool bots;
    }

    [Serializable]
    public class FindRaceResponse
    {
        public string matchId;
        public string lang;
    }
}
