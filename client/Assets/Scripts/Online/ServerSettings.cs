using UnityEngine;

namespace TextRun.Online
{
    // Sunucu adresi. Geliştirme sırasında telefonda test için adres oyun içinden değiştirilebilir.
    public static class ServerSettings
    {
        const string HostKey = "textrun.serverHost";

        public const string Scheme = "http";
        public const int Port = 7350;
        // Yerel geliştirme anahtarı. Canlı sunucuda farklı bir değer kullanılacak.
        public const string ServerKey = "defaultkey";

        public static string Host
        {
            get => PlayerPrefs.GetString(HostKey, "127.0.0.1");
            set
            {
                PlayerPrefs.SetString(HostKey, string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim());
                PlayerPrefs.Save();
            }
        }
    }
}
