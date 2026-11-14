using System;
using TextRun.Online;
using UnityEngine;

namespace TextRun.Game
{
    // Liderlik tablosu: seviyesi en yüksek ilk 10 oyuncu ve oyuncunun kendi sırası. Sıralama sunucuda tutulur.
    public partial class OnlineGameController
    {
        LeaderboardDto leaderboard;   // null = henüz okunmadı
        bool leaderboardFailed;

        static readonly Color[] MedalColors = { new Color(1f, 0.82f, 0.22f), new Color(0.80f, 0.84f, 0.90f), new Color(0.84f, 0.56f, 0.30f) };

        void OpenLeaderboard()
        {
            leaderboard = null;
            leaderboardFailed = false;
            screen = Page.Leaderboard;
            LoadLeaderboard();
        }

        async void LoadLeaderboard()
        {
            if (net == null) return;
            try
            {
                var fresh = await net.GetLeaderboardAsync();
                if (fresh.top == null) fresh.top = new LeaderRowDto[0];
                leaderboard = fresh;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Liderlik tablosu okunamadi: " + e.Message);
                leaderboardFailed = true;
            }
        }

        void DrawLeaderRow(Rect row, LeaderRowDto entry)
        {
            bool medal = entry.rank >= 1 && entry.rank <= 3;
            Color accent = medal ? MedalColors[entry.rank - 1] : new Color(1f, 1f, 1f, 0.5f);
            UiKit.Card(row, entry.me ? new Color(0.20f, 0.52f, 1f, 0.42f) : new Color(1f, 1f, 1f, 0.08f), 12f);
            if (medal) UiKit.Card(new Rect(row.x, row.y + 6f, 5f, row.height - 12f), accent, 2.5f);

            var badge = new Rect(row.x + 12f, row.center.y - 15f, 30f, 30f);
            UiKit.Card(badge, medal ? accent : new Color(1f, 1f, 1f, 0.12f), 15f);
            var old = GUI.color;
            if (medal) GUI.color = new Color(0.10f, 0.09f, 0.04f);
            GUI.Label(badge, entry.rank.ToString(), tabStyle);
            GUI.color = old;

            GUI.Label(new Rect(row.x + 52f, row.y, row.width - 160f, row.height), entry.name, rowNameStyle);
            GUI.Label(new Rect(row.xMax - 118f, row.y, 106f, row.height), T("level") + " " + entry.level, rowTagStyle);
        }

        void DrawLeaderboard(float width, float height, float top)
        {
            FullViewport();
            EnsureInventoryStyles();

            float y = top + 8f;
            GUI.Label(new Rect(0f, y, width, 40f), T("leaderboard"), headingStyle);
            GUI.Label(new Rect(0f, y + 38f, width, 22f), T("leaderboardSub"), smallCenterStyle);
            y += 68f;

            if (leaderboard == null)
                GUI.Label(new Rect(20f, y + 40f, width - 40f, 30f), leaderboardFailed ? T("connectionError") : "...", smallCenterStyle);
            else if (leaderboard.top.Length == 0)
                GUI.Label(new Rect(28f, y + 30f, width - 56f, 50f), T("leaderboardEmpty"), smallCenterWrapStyle());
            else
            {
                // Satır yüksekliği ekrana göre: 10 satır + kendi sıran her telefonda sığar
                float rowHeight = Mathf.Clamp((height - 64f - y - 78f) / 10f - 5f, 34f, 46f);
                bool inTop = false;
                foreach (var entry in leaderboard.top)
                {
                    DrawLeaderRow(new Rect(16f, y, width - 32f, rowHeight), entry);
                    if (entry.me) inTop = true;
                    y += rowHeight + 5f;
                }

                // Kendi sıran (ilk 10'da değilsen ayrıca gösterilir)
                if (!profile.guest && !inTop)
                {
                    y += 6f;
                    if (leaderboard.me != null && leaderboard.me.rank > 0)
                    {
                        GUI.Label(new Rect(20f, y, width - 40f, 20f), T("yourRank"), smallStyle);
                        DrawLeaderRow(new Rect(16f, y + 22f, width - 32f, rowHeight), leaderboard.me);
                    }
                    else GUI.Label(new Rect(20f, y + 6f, width - 40f, 24f), T("notRanked"), smallCenterStyle);
                }
            }

            if (UiKit.SecondaryButton(new Rect(24f, height - 64f, width - 48f, 50f), T("back"), 20)) screen = Page.Menu;
        }

        // Geliştirme: liderlik tablosunu örnek veriyle gösterir (DevHarness)
        public void DevShowLeaderboard()
        {
            profile = new ProfileDto { name = "Furkan", level = 4, gold = 1250, diamonds = 320 };
            string[] names = { "HızlıParmak", "Zeynep", "klavyeci_34", "Mert", "Elif", "Kaan07", "Deniz", "Ayşe", "TypeMaster", "Bora" };
            int[] levels = { 31, 27, 24, 19, 17, 14, 12, 9, 8, 6 };
            var rows = new LeaderRowDto[10];
            for (int i = 0; i < 10; i++) rows[i] = new LeaderRowDto { rank = i + 1, name = names[i], level = levels[i], xp = levels[i] * 500 };
            leaderboard = new LeaderboardDto { top = rows, me = new LeaderRowDto { rank = 42, name = "Furkan", level = 4, xp = 410, me = true } };
            leaderboardFailed = false;
            screen = Page.Leaderboard;
        }
    }
}
