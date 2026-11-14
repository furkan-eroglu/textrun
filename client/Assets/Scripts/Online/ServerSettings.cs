using UnityEngine;

namespace TextRun.Online
{
    // Sunucu adresi. Telefon ve tabletteki oyun canlı sunucuya (şifreli bağlantı) bağlanır.
    // Bilgisayardaki sürüm (Unity editörü, otomatik denemeler) bu bilgisayarda çalışan yerel geliştirme sunucusuna bağlanır.
    public static class ServerSettings
    {
        const string HostKey = "textrun.serverHost";

        // Canlı sunucu (Hetzner). Alan adı alınınca LiveHost değişir. Sunucu anahtarı gizli değildir (uygulamanın içinde gider),
        // yalnızca varsayılan anahtar olmamalıdır; gizli anahtarlar sunucudaki .env dosyasındadır (bkz. server/deploy).
        const string LiveHost = "2-31-32-138.sslip.io";
        const string LiveServerKey = "895bc52e679f7d72cf38b28b6c89ac52";

        // Yerel geliştirme sunucusu
        const string LocalHost = "127.0.0.1";
        const string LocalServerKey = "defaultkey";

        public static bool Live => Application.isMobilePlatform;

        public static string Scheme => Live ? "https" : "http";
        public static int Port => Live ? 443 : 7350;
        public static string ServerKey => Live ? LiveServerKey : LocalServerKey;

        // Geliştirme araçları: menüdeki "Sunucu adresi" kutusu (telefonu yerel ağdaki bilgisayara bağlamak içindi).
        // Canlı sunucuya geçildiği için kapalı.
        public const bool DevTools = false;

        public static string Host
        {
            get => Live ? LiveHost : PlayerPrefs.GetString(HostKey, LocalHost);
            set
            {
                PlayerPrefs.SetString(HostKey, string.IsNullOrWhiteSpace(value) ? LocalHost : value.Trim());
                PlayerPrefs.Save();
            }
        }
    }
}
