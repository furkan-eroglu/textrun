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

        // Geliştirme bilgisayarının yerel ağ adresi: telefona kurulan geliştirme sürümü varsayılan olarak buna bağlanır.
        // Başka bir ağa geçilirse oyun içindeki "Sunucu adresi" kutusundan değiştirilir.
        const string DevLanHost = "192.168.1.114";

        // Geliştirme araçları: menüdeki "Sunucu adresi" kutusu. Canlı sunucuya geçilince false yapılacak.
        public const bool DevTools = true;

        static string DefaultHost => Application.isMobilePlatform && DevTools ? DevLanHost : "127.0.0.1";

        public static string Host
        {
            get => PlayerPrefs.GetString(HostKey, DefaultHost);
            set
            {
                PlayerPrefs.SetString(HostKey, string.IsNullOrWhiteSpace(value) ? DefaultHost : value.Trim());
                PlayerPrefs.Save();
            }
        }
    }
}
