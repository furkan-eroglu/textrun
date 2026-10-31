using System;
using System.Collections.Generic;
using TextRun.Audio;
using TextRun.Common;
using TextRun.Online;
using TextRun.Race;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.Game
{
    // Oynanabilir ana akış: bağlanma -> menü -> rakip arama -> geri sayım -> yarış -> sonuç.
    // Arayüz geçici olarak OnGUI ile çizilir (şık arayüz sonraki aşamada). Yarış mantığı sunucudadır;
    // burada yalnızca yazma girişi alınır, sunucudan gelenler gösterilir.
    public class OnlineGameController : MonoBehaviour
    {
        enum Page { Connecting, Menu, Searching, MatchFound, Countdown, Racing, Results, Error }

        const string LanguageKey = "textrun.language";

        readonly KeyboardInput keyboard = new KeyboardInput();
        readonly WordLinesView wordView = new WordLinesView();
        readonly RaceProgressStrip progressStrip = new RaceProgressStrip();
        readonly MenuBackdrop backdrop = new MenuBackdrop();
        readonly ResultsBackdrop resultsBackdrop = new ResultsBackdrop();
        readonly Fireworks fireworks = new Fireworks();
        readonly GameLogo logo = new GameLogo();
        float menuShownAt;
        float lastMenuDrawAt = -999f;

        OnlineRaceClient net;
        OnlineRaceView view;
        RaceAudio sound;
        int lastTickSecond = int.MinValue;
        float endingEndsLocal;                     // yarışın biteceği an (yerel saat); 0 = bitiş geri sayımı yok
        int lastAlertSecond = int.MinValue;
        readonly List<string> finishOrder = new List<string>(); // bitirenlerin sırası (canlı sıra göstermek için)
        GameLanguage language;
        Page screen = Page.Connecting;
        string errorText = "";
        string noticeText = "";

        TypingSession typing;
        WordInput input;
        PlayerDto[] players = new PlayerDto[0];
        ResultDto[] results = new ResultDto[0];
        int wordCount;
        string hostInput;
        bool searchBusy;

        // Yerel saat tabanlı geri sayımlar (sunucu saatinden fark alınarak hesaplanır)
        float lobbyEndsLocal;
        float searchStartedAt;
        float countdownEndsLocal;
        float countdownTotal = 3f;     // F1 ışıklarının dağılacağı süre (sunucu geri sayımı)
        float goFlashUntil;            // GO anında ışıkların yeşil yanıp sönme bitişi
        int litLights;                 // geri sayımda şu ana kadar yanan ışık sayısı
        const int LightCount = 5;
        const float LightsSeconds = 6f;   // ışıkların yanacağı süre; sunucu geri sayımının kalanı "karşılaşma bulundu" ekranına ayrılır
        float matchFoundStartedAt;
        float matchFoundSeconds;

        GUIStyle titleStyle, bigButton, labelStyle, centerStyle, smallStyle, smallCenterStyle, stopwatchStyle, headingStyle, rankStyle, searchTitleStyle, positionStyle;

        void Start()
        {
            Application.targetFrameRate = 60;
            language = (GameLanguage)PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Turkish);
            hostInput = ServerSettings.Host;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            sound = gameObject.AddComponent<RaceAudio>();
            UiKit.Clicked = () => sound.Click(); // her düğme tıklamasında (ses açıkken) tıklama sesi
            Debug.Log("[basla] sesler hazirlandi: " + timer.ElapsedMilliseconds + " ms");

            gameObject.AddComponent<DevHarness>(); // komut satırında -devharness yoksa kendini kaldırır
            timer.Restart();
            view = gameObject.AddComponent<OnlineRaceView>();
            view.Build();
            Debug.Log("[basla] pist ve arena kuruldu: " + timer.ElapsedMilliseconds + " ms");
            view.SetAllActive(new[] { "", "", "", "", "" }, 2);
            view.SetVisible(false); // menü ve eşleşme ekranlarında pist görünmez; yalnızca arka plan ve arayüz vardır
            view.ResetToStart();

            Connect();
        }

        string T(string key) => Loc.T(key, language);

        // --- Geliştirme araçları: yalnızca DevHarness (-devharness komut satırı seçeneği) kullanır ---
        public string DevPageName => screen.ToString();

        public void DevStartSearch()
        {
            if (screen == Page.Menu) StartSearch();
        }

        // ---------- Bağlantı ----------

        async void Connect()
        {
            screen = Page.Connecting;
            if (net != null) await net.CloseAsync();

            net = new OnlineRaceClient();
            net.Snapshot += OnSnapshot;
            net.Roster += OnSnapshot;
            net.Countdown += OnSnapshot;
            net.Go += OnGo;
            net.Progress += OnProgress;
            net.Ack += OnAck;
            net.Results += OnResults;
            net.Ending += OnEnding;
            net.Closed += OnClosed;

            try
            {
                await net.ConnectAsync();
                screen = Page.Menu;
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void Fail(Exception e)
        {
            Debug.LogWarning("Hata: " + e);
            errorText = e.Message;
            screen = Page.Error;
        }

        void OnClosed(string reason)
        {
            if (screen == Page.Menu || screen == Page.Connecting || screen == Page.Error) return;
            sound.StopMusic();
            errorText = T("connectionLost");
            screen = Page.Error;
        }

        // ---------- Oyun akışı ----------

        async void StartSearch()
        {
            if (searchBusy) return;
            searchBusy = true;
            searchStartedAt = Time.realtimeSinceStartup;
            noticeText = "";
            screen = Page.Searching;
            players = new PlayerDto[0];
            lobbyEndsLocal = 0f;
            endingEndsLocal = 0f;
            finishOrder.Clear();
            view.ResetToStart();
            view.SetRacing(false);
            wordView.Reset();
            keyboard.Text = "";

            try
            {
                await net.FindAndJoinAsync(language, message =>
                {
                    if (message.Contains("room_full") || message.Contains("room_closed")) noticeText = T("roomFull");
                });
            }
            catch (Exception e)
            {
                if (e.Message.Contains("language_mismatch")) errorText = T("languageMismatch");
                else errorText = e.Message;
                screen = Page.Error;
            }
            finally
            {
                searchBusy = false;
            }
        }

        async void CancelSearch()
        {
            sound.StopMusic();
            await net.LeaveAsync();
            screen = Page.Menu;
        }

        // SNAPSHOT, ROSTER ve COUNTDOWN aynı gövdeyi taşır; hepsi buradan durumu günceller.
        void OnSnapshot(SnapshotDto s)
        {
            if (screen == Page.Menu || screen == Page.Error) return;

            players = s.players ?? new PlayerDto[0];
            view.SetRoster(players, net.UserId);
            noticeText = "";

            float now = Time.realtimeSinceStartup;
            if (s.lobbyEndsAt > 0) lobbyEndsLocal = now + (s.lobbyEndsAt - s.serverNow) / 1000f;
            if (s.endsAt > 0) endingEndsLocal = now + (s.endsAt - s.serverNow) / 1000f; // yeniden bağlanana da geri sayım gösterilir

            switch (s.phase)
            {
                case Phase.Lobby:
                    screen = Page.Searching;
                    break;

                case Phase.Countdown:
                    PrepareTyping(s);
                    countdownEndsLocal = now + (s.startAt - s.serverNow) / 1000f;
                    float remaining = (s.startAt - s.serverNow) / 1000f;
                    countdownTotal = Mathf.Min(LightsSeconds, Mathf.Max(0.5f, remaining));
                    litLights = 0;
                    goFlashUntil = 0f;
                    lastTickSecond = int.MinValue;
                    endingEndsLocal = 0f;
                    finishOrder.Clear();
                    fireworks.Clear();

                    // Önce "karşılaşma bulundu" uyarısı (ses ile), sonra ışıklar. Geç bağlananlar (yeniden bağlanma) doğrudan ışıklara geçer.
                    if (screen != Page.MatchFound && screen != Page.Countdown && remaining > LightsSeconds + 0.6f)
                    {
                        matchFoundStartedAt = now;
                        matchFoundSeconds = remaining - countdownTotal;
                        screen = Page.MatchFound;
                        sound.MatchFound();
                    }
                    else if (screen != Page.MatchFound) screen = Page.Countdown;
                    break;

                case Phase.Racing:
                    // Yarış sürerken (yeniden bağlanma) kaldığımız yerden devam ederiz
                    PrepareTyping(s);
                    SyncLocalIndex();
                    view.SetProgress(players, wordCount);
                    view.SetRacing(true);
                    screen = Page.Racing;
                    keyboard.Open(KeyboardKind.Default);
                    break;

                case Phase.Finished:
                    results = s.results ?? new ResultDto[0];
                    resultsBackdrop.Restart();
                    screen = Page.Results;
                    break;
            }
        }

        void PrepareTyping(SnapshotDto s)
        {
            if (typing != null && typing.Count == s.words.Length && wordCount == s.words.Length && typing.WordAt(0) == TextNormalizer.ToLower(s.words[0], language)) return;
            typing = new TypingSession(s.words, language);
            wordCount = s.words.Length;
            wordView.Reset();
            input = new WordInput(language);
            BeginInput();
        }

        // Giriş süzgecini şu anki aktif kelimeye ayarlar ve klavye metnini temizler.
        void BeginInput()
        {
            if (input == null || typing == null) return;
            input.Begin(typing.Finished ? "" : typing.WordAt(typing.CurrentIndex));
            keyboard.Text = "";
        }

        void SyncLocalIndex()
        {
            foreach (var p in players)
                if (p.id == net.UserId) typing.SyncTo(p.index);
            BeginInput();
        }

        void OnGo(GoDto go)
        {
            if (typing == null) return;
            screen = Page.Racing;
            goFlashUntil = Time.realtimeSinceStartup + 0.8f;
            typing.MarkStarted(Time.realtimeSinceStartup);
            view.SetRacing(true);
            keyboard.Open(KeyboardKind.Default);
            BeginInput();
            sound.Go();
        }

        void OnProgress(ProgressDto progress)
        {
            players = progress.players ?? players;
            view.SetProgress(players, wordCount);

            // Bitirenlerin sırasını kaydet (canlı "kaçıncı sıradasın" bilgisi için)
            foreach (var p in players)
                if (p.finished && !p.disqualified && !finishOrder.Contains(p.id)) finishOrder.Add(p.id);
        }

        // İlk oyuncu bitirdi: yarış, sunucunun bildirdiği anda sona erecek (kırmızı geri sayım)
        void OnEnding(EndingDto e)
        {
            endingEndsLocal = Time.realtimeSinceStartup + (e.endsAt - e.serverNow) / 1000f;
            lastAlertSecond = int.MinValue;
        }

        void OnAck(AckDto ack)
        {
            if (typing == null) return;
            if (ack.kind == AckKind.Disqualified) noticeText = T("disqualifiedNotice");
            if (ack.kind == AckKind.Finished)
            {
                // Bitirdin: klavye kapanır, "Bitirdin!" paneli ve canlı sıra gösterilir
                if (!finishOrder.Contains(net.UserId)) finishOrder.Add(net.UserId);
                keyboard.Close();
                sound.Applause(); // bitirdiğin hemen anlaşılsın
                fireworks.Launch(finishOrder.IndexOf(net.UserId) < 3); // ilk üç: büyük gösteri, diğerleri: küçük
            }
            if (ack.index != typing.CurrentIndex)
            {
                typing.SyncTo(ack.index);
                BeginInput();
            }
        }

        void OnResults(ResultsDto r)
        {
            results = r.results ?? new ResultDto[0];
            view.SetRacing(false);
            fireworks.Clear();
            endingEndsLocal = 0f;
            resultsBackdrop.Restart(); // ekran açılınca konfeti yukarıdan süzülmeye başlar
            screen = Page.Results;

            sound.Finish(PodiumRank() <= 3);
        }

        // Yarış bitiş geri sayımında saniye değiştikçe gerilim tiki; son 3 saniyede daha tiz
        void TickEndingSound()
        {
            if (screen != Page.Racing || endingEndsLocal <= 0f) return;

            int seconds = Mathf.CeilToInt(endingEndsLocal - Time.realtimeSinceStartup);
            if (seconds == lastAlertSecond || seconds < 1) return;

            lastAlertSecond = seconds;
            sound.AlertTick(seconds <= 3);
        }

        // F1 ışıkları: geri sayım süresine eşit aralıklarla sırayla yanar, her ışıkta bir tik sesi
        int LightsLit()
        {
            float elapsed = countdownTotal - (countdownEndsLocal - Time.realtimeSinceStartup);
            return Mathf.Clamp(Mathf.FloorToInt(elapsed / (countdownTotal / (LightCount + 1)) + 0.0001f), 0, LightCount);
        }

        void TickCountdownSound()
        {
            if (screen != Page.Countdown) return;

            int lit = LightsLit();
            if (lit > litLights) sound.Tick();
            litLights = lit;
        }

        // ---------- Yazma girişi ----------

        void Update()
        {
            net?.Pump();
            if (screen == Page.MatchFound && Time.realtimeSinceStartup >= countdownEndsLocal - countdownTotal) screen = Page.Countdown;
            TickCountdownSound();
            TickEndingSound();
            sound.SetMenuMusic(screen == Page.Menu || screen == Page.Searching || screen == Page.MatchFound);

            // Pist ve karakterler yalnızca geri sayım ve yarışta görünür
            bool wantTrack = screen == Page.Countdown || screen == Page.Racing;

            // Menü ve eşleşme ekranlarında 30 FPS yeter (pil ve ısınma); yarışta 60
            int targetFps = wantTrack ? 60 : 30;
            if (Application.targetFrameRate != targetFps) Application.targetFrameRate = targetFps;
            if (view.IsVisible != wantTrack) view.SetVisible(wantTrack);

            if (screen != Page.Racing || typing == null || input == null) return;
            PollTyping();
        }

        // Klavyeden gelen yeni harfleri sırayla süzgeçten geçirir. Klavye metni her zaman "yalnızca doğru harfler" haline geri alınır,
        // böylece yanlış harfler, otomatik düzeltmeler ya da klavyenin kendi eklemeleri girişi bozamaz.
        void PollTyping()
        {
            string raw = keyboard.Text ?? "";
            if (raw == input.Entered) return;

            if (raw.Length > input.Entered.Length && StartsWithIgnoreCase(raw, input.Entered))
            {
                for (int i = input.Entered.Length; i < raw.Length && !typing.Finished; i++) HandleKey(raw[i]);
            }
            else if (raw.Length < input.Entered.Length && StartsWithIgnoreCase(input.Entered, raw))
            {
                input.TrimTo(raw.Length); // silme tuşu
            }

            if (keyboard.Text != input.Entered) keyboard.Text = input.Entered;
        }

        bool StartsWithIgnoreCase(string text, string prefix)
        {
            if (text.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (TextNormalizer.ToLower(text[i], language) != prefix[i]) return false;
            return true;
        }

        void HandleKey(char c)
        {
            switch (input.Press(c))
            {
                case KeyResult.Rejected:
                    wordView.FlashMistake();
                    break;

                case KeyResult.Submit:
                    SubmitCurrentWord();
                    break;
            }
        }

        void SubmitCurrentWord()
        {
            string word = input.Entered;
            int mistakes = input.MistakesThisWord;

            // Yerelde hemen ilerletiriz (gecikme hissedilmesin); sunucu cevabı (ACK) gerekirse düzeltir.
            typing.Submit(word, Time.realtimeSinceStartup);
            typing.RegisterMistakes(mistakes);
            net.SubmitWord(word, mistakes);

            input.Begin(typing.Finished ? "" : typing.WordAt(typing.CurrentIndex));
        }

        // ---------- Arayüz ----------

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };

            headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            rankStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            searchTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            positionStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = true, clipping = TextClipping.Overflow };
            smallCenterStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            stopwatchStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };

            // Fare üstüne gelince yazı rengi değişmesin: tüm durumlar aynı renk
            var text = new Color(0.92f, 0.93f, 0.96f);
            UiKit.SetAllTextColors(titleStyle, Color.white);
            UiKit.SetAllTextColors(labelStyle, text);
            UiKit.SetAllTextColors(centerStyle, text);
            UiKit.SetAllTextColors(smallStyle, text);
            UiKit.SetAllTextColors(smallCenterStyle, text);
            UiKit.SetAllTextColors(stopwatchStyle, Color.white);
            UiKit.SetAllTextColors(searchTitleStyle, Color.white);
            UiKit.SetAllTextColors(positionStyle, Color.white);
            UiKit.SetAllTextColors(headingStyle, Color.white);
            UiKit.SetAllTextColors(rankStyle, new Color(1f, 0.88f, 0.45f));
        }

        void OnGUI()
        {
            keyboard.HandleGuiEvent(Event.current);
            EnsureStyles();

            float scale = Mathf.Max(1.2f, ScreenDpi() / 160f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = UnityEngine.Screen.width / scale;
            float height = UnityEngine.Screen.height / scale;
            float top = (UnityEngine.Screen.height - UnityEngine.Screen.safeArea.yMax) / scale + 6f;

            if (screen == Page.Connecting || screen == Page.Menu || screen == Page.Searching || screen == Page.MatchFound || screen == Page.Error)
                backdrop.Draw(width, height);

            switch (screen)
            {
                case Page.Connecting: DrawCenterMessage(width, height, T("connecting")); break;
                case Page.Error: DrawError(width, height); break;
                case Page.Menu: DrawMenu(width, height, top); break;
                case Page.Searching: DrawSearching(width, height, top); break;
                case Page.MatchFound: DrawMatchFound(width, height); break;
                case Page.Countdown: DrawCountdown(width, height, top, scale); break;
                case Page.Racing: DrawRacing(width, height, top, scale); break;
                case Page.Results: DrawResults(width, height, top); break;
            }
        }

        static float ScreenDpi() => UnityEngine.Screen.dpi > 0f ? UnityEngine.Screen.dpi : 160f;

        void FullViewport()
        {
            view.SetViewport(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height));
        }

        void DrawPanel(Rect r, float alpha = 0.75f, float radius = 18f)
        {
            UiKit.Panel(r, alpha, radius);
        }

        void DrawCenterMessage(float width, float height, string message)
        {
            FullViewport();
            DrawPanel(new Rect(20f, height / 2f - 40f, width - 40f, 80f));
            GUI.Label(new Rect(20f, height / 2f - 40f, width - 40f, 80f), message, centerStyle);
        }

        void DrawError(float width, float height)
        {
            FullViewport();
            DrawPanel(new Rect(20f, height / 2f - 110f, width - 40f, 220f));
            GUI.Label(new Rect(30f, height / 2f - 100f, width - 60f, 30f), T("connectionError"), centerStyle);
            GUI.Label(new Rect(30f, height / 2f - 65f, width - 60f, 90f), errorText, smallStyle);

            if (UiKit.PrimaryButton(new Rect(30f, height / 2f + 30f, width - 60f, 56f), T("retry"))) Connect();
        }

        void DrawMenu(float width, float height, float top)
        {
            FullViewport();

            // Logo: menü her açıldığında harfler yeniden kayarak gelir
            float now = Time.realtimeSinceStartup;
            if (now - lastMenuDrawAt > 0.5f) menuShownAt = now;
            lastMenuDrawAt = now;
            logo.Draw(width, top + 80f, now - menuShownAt);

            DrawSoundButton(new Rect(width - 54f, top + 8f, 42f, 42f));

            // Dil seçimi: başlık yok, yalnızca iki seçenek
            float y = top + 160f;
            int clickedLanguage = UiKit.Segmented(new Rect(24f, y, width - 48f, 52f), new[] { "Türkçe", "English" }, language == GameLanguage.Turkish ? 0 : 1, 21);
            if (clickedLanguage >= 0) SetLanguage((GameLanguage)clickedLanguage);
            y += 86f;

            if (UiKit.PrimaryButton(new Rect(24f, y, width - 48f, 76f), T("normalGame"), 26, true)) StartSearch();
            y += 96f;

            UiKit.DisabledButton(new Rect(24f, y, width - 48f, 56f), T("privateGame"), 20);

            // Geliştirme: telefonda sunucu adresini (bilgisayarın yerel IP'si) girmek için
            if (Application.isEditor || Debug.isDebugBuild)
            {
                float by = height - 70f;
                GUI.Label(new Rect(20f, by - 24f, width - 40f, 22f), T("server"), smallStyle);
                hostInput = GUI.TextField(new Rect(20f, by, width - 150f, 40f), hostInput, labelStyle);
                if (GUI.Button(new Rect(width - 120f, by, 100f, 40f), T("connect")))
                {
                    ServerSettings.Host = hostInput;
                    Connect();
                }
            }
        }

        // Hoparlör simgesi: dokununca ses açılır/kapanır; kapalıyken simgenin üstünde kırmızı çarpı görünür.
        void DrawSoundButton(Rect rect)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, SoundIcon.Disc());
            GUI.color = Color.white;

            var icon = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, rect.height - 14f);
            GUI.DrawTexture(icon, SoundIcon.Get(sound.Enabled));

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                sound.Enabled = !sound.Enabled;
                if (sound.Enabled) sound.Click(); // açılınca tıklama sesi duyulur; kapanırken ses çıkmaz
            }
        }

        void SetLanguage(GameLanguage value)
        {
            language = value;
            PlayerPrefs.SetInt(LanguageKey, (int)value);
            PlayerPrefs.Save();
        }

        // Eşleşme ekranı: yalnızca sade arka plan; ortada kronometre, bulunan oyuncu noktaları ve iptal düğmesi
        void DrawSearching(float width, float height, float top)
        {
            FullViewport();

            float centerX = width / 2f;
            float centerY = height / 2f - 30f;

            // Başlık: tam ortada; harfleri soldan sağa doğru sırayla büyüyüp küçülür
            DrawWaveTitle(new Vector2(centerX, centerY - 150f), T("searching"));

            DrawStopwatch(new Vector2(centerX, centerY), 74f, Mathf.Max(0, (int)(Time.realtimeSinceStartup - searchStartedAt)));

            // Bulunan oyuncular: 5 nokta, bulunanlar parlak
            int found = HumanCount();
            const float dotSize = 16f;
            const float dotGap = 12f;
            float rowWidth = 5 * dotSize + 4 * dotGap;
            var oldColor = GUI.color;
            for (int i = 0; i < 5; i++)
            {
                GUI.color = i < found ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(1f, 1f, 1f, 0.16f);
                GUI.DrawTexture(new Rect(centerX - rowWidth / 2f + i * (dotSize + dotGap), centerY + 110f, dotSize, dotSize), SoundIcon.Disc());
            }
            GUI.color = oldColor;
            GUI.Label(new Rect(0f, centerY + 132f, width, 26f), found + " / 5", smallCenterStyle);

            if (noticeText.Length > 0) GUI.Label(new Rect(0f, centerY + 160f, width, 26f), noticeText, smallCenterStyle);

            if (UiKit.SecondaryButton(new Rect(width / 2f - 110f, centerY + 190f, 220f, 54f), T("cancel"), 20)) CancelSearch();
        }

        // "Karşılaşma bulundu!": başlık büyüyerek gelir, 5 oyuncu noktası yanar, altta yarışın başlamasına kalan süre akar
        void DrawMatchFound(float width, float height)
        {
            FullViewport();

            float elapsed = Time.realtimeSinceStartup - matchFoundStartedAt;
            float centerX = width / 2f;
            float centerY = height / 2f - 30f;

            // Başlık yaylanarak büyür (kısa bir taşmayla oturur)
            float u = Mathf.Clamp01(elapsed / 0.4f);
            float c1 = 1.70158f, c3 = c1 + 1f;
            float pop = 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);

            var oldMatrix = GUI.matrix;
            var pivot = oldMatrix.MultiplyPoint3x4(new Vector3(centerX, centerY - 40f, 0f)); // pivot ölçeklenmiş koordinatta
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), pivot);
            GUI.Label(new Rect(0f, centerY - 80f, width, 80f), T("matchFound"), searchTitleStyle);
            GUI.matrix = oldMatrix;

            // 5 oyuncu da hazır
            const float dotSize = 16f;
            const float dotGap = 12f;
            float rowWidth = 5 * dotSize + 4 * dotGap;
            var oldColor = GUI.color;
            for (int i = 0; i < 5; i++)
            {
                float appear = Mathf.Clamp01((elapsed - 0.15f - i * 0.07f) / 0.2f);
                GUI.color = new Color(0.35f, 0.85f, 1f, appear);
                GUI.DrawTexture(new Rect(centerX - rowWidth / 2f + i * (dotSize + dotGap), centerY + 10f, dotSize, dotSize), SoundIcon.Disc());
            }
            GUI.color = oldColor;

            GUI.Label(new Rect(0f, centerY + 44f, width, 26f), T("matchFoundSub"), smallCenterStyle);

            // Süre çubuğu: yarışın (ışıkların) başlamasına kalan süre
            float remaining01 = Mathf.Clamp01(1f - elapsed / Mathf.Max(0.1f, matchFoundSeconds));
            const float barWidth = 200f;
            UiKit.Card(new Rect(centerX - barWidth / 2f, centerY + 84f, barWidth, 6f), new Color(1f, 1f, 1f, 0.14f), 3f);
            UiKit.Card(new Rect(centerX - barWidth / 2f, centerY + 84f, barWidth * remaining01, 6f), new Color(0.35f, 0.85f, 1f, 1f), 3f);
        }

        // Başlık harfleri soldan sağa doğru sırayla büyüyüp küçülür (dalga); büyürken hafif maviye parlar.
        // Dalga başlığın dışından başlayıp dışında biter, böylece iki dalga arasında kısa bir sakin an olur.
        void DrawWaveTitle(Vector2 center, string text)
        {
            var widths = new float[text.Length];
            float total = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                widths[i] = text[i] == ' ' ? searchTitleStyle.fontSize * 0.35f : searchTitleStyle.CalcSize(new GUIContent(text[i].ToString())).x;
                total += widths[i];
            }

            const float cycleSeconds = 2.4f;
            float wavePosition = Mathf.Repeat(Time.realtimeSinceStartup / cycleSeconds, 1f) * (text.Length + 8f) - 4f;

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float x = center.x - total / 2f;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ')
                {
                    float distance = i - wavePosition;
                    float bump = Mathf.Exp(-(distance * distance) / (2f * 1.8f * 1.8f)); // yumuşak çan eğrisi: 0 -> 1 -> 0
                    float scale = 1f + 0.5f * bump;

                    var rect = new Rect(x, center.y - 24f, widths[i], 48f);
                    var pivot = rect.center;
                    GUI.matrix = oldMatrix * Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(scale, scale, 1f)) * Matrix4x4.Translate(-pivot);
                    GUI.color = Color.Lerp(Color.white, new Color(0.45f, 0.85f, 1f), bump);
                    GUI.Label(rect, text[i].ToString(), searchTitleStyle);
                }
                x += widths[i];
            }

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        // Kronometre: yuvarlak zemin, çevresinde 60 saniye çizgisi (geçen saniyeler yanar) ve ortada dakika:saniye
        void DrawStopwatch(Vector2 center, float radius, int seconds)
        {
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;

            GUI.color = new Color(1f, 1f, 1f, 0.07f);
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), SoundIcon.Disc());

            int lit = seconds % 60;
            for (int i = 0; i < 60; i++)
            {
                bool major = i % 5 == 0;
                float length = major ? 12f : 7f;
                GUI.color = i <= lit ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(1f, 1f, 1f, major ? 0.30f : 0.16f);

                // RotateAroundPivot merkezi, arayüz ölçeği uygulanmış (ekran) koordinatında bekler
                GUIUtility.RotateAroundPivot(i * 6f, oldMatrix.MultiplyPoint3x4(center));
                GUI.DrawTexture(new Rect(center.x - 1.5f, center.y - radius + 4f, 3f, length), Texture2D.whiteTexture);
                GUI.matrix = oldMatrix;
            }
            GUI.color = oldColor;

            string time = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            GUI.Label(new Rect(center.x - radius, center.y - 26f, radius * 2f, 52f), time, stopwatchStyle);
        }

        int HumanCount()
        {
            int n = 0;
            foreach (var p in players) if (!p.isBot) n++;
            return n;
        }

        void DrawCountdown(float width, float height, float top, float scale)
        {
            // Üstte 3D pist, altta kelimeler (yarıştaki düzenin aynısı; yazmak için henüz erken)
            LayoutRaceArea(width, height, scale, out var box, out float devButtonY);
            view.DrawLabels(scale, T("you"), T("bot"));
            progressStrip.Draw(new Rect(8f, top + 8f, width - 16f, 26f), players, wordCount, net.UserId, 0f);

            DrawStartLights(width, top, LightsLit(), false);

            if (typing != null) wordView.Draw(box, typing, "");
            DrawSimulatedKeyboard(width, height, scale, devButtonY);
        }

        // F1 start ışıkları: koyu bir panelde 5 kırmızı ışık sırayla yanar; GO'da hepsi kısa süre yeşil yanıp söner
        void DrawStartLights(float width, float top, int lit, bool green)
        {
            const float size = 38f, gap = 10f;
            float panelW = LightCount * size + (LightCount + 1) * gap;
            var panel = new Rect(width / 2f - panelW / 2f, top + 44f, panelW, size + gap * 2f + 22f);
            UiKit.Panel(panel, 0.85f, 14f);

            var old = GUI.color;
            for (int i = 0; i < LightCount; i++)
            {
                var r = new Rect(panel.x + gap + i * (size + gap), panel.y + gap, size, size);
                bool on = green || i < lit;
                GUI.color = new Color(0.10f, 0.10f, 0.12f, 1f); // lamba yuvası
                GUI.DrawTexture(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), SoundIcon.Disc());
                if (on)
                {
                    GUI.color = green ? new Color(0.20f, 0.95f, 0.40f, 0.30f) : new Color(1f, 0.15f, 0.15f, 0.30f); // parlama
                    GUI.DrawTexture(new Rect(r.x - 7f, r.y - 7f, r.width + 14f, r.height + 14f), SoundIcon.Disc());
                    GUI.color = green ? new Color(0.25f, 0.95f, 0.45f, 1f) : new Color(0.95f, 0.12f, 0.14f, 1f);
                }
                else GUI.color = new Color(0.25f, 0.10f, 0.11f, 1f); // sönük
                GUI.DrawTexture(r, SoundIcon.Disc());
            }
            GUI.color = old;

            GUI.Label(new Rect(panel.x, panel.yMax - 26f, panel.width, 22f), green ? T("go") : T("getReady"), smallCenterStyle);
        }

        // Yalnızca editörde: telefon klavyesinin kaplayacağı alanı gri bir blokla gösterir, düzeni PC'de telefondaki gibi görmek için.
        void DrawSimulatedKeyboard(float width, float height, float scale, float devButtonY)
        {
            if (!Application.isEditor) return;

            // İstatistik satırının sağında (ilerleme şeridiyle çakışmasın)
            if (GUI.Button(new Rect(width - 118f, devButtonY, 112f, 24f), KeyboardInput.SimulateInEditor ? "Klavye: AÇIK" : "Klavye: KAPALI"))
                KeyboardInput.SimulateInEditor = !KeyboardInput.SimulateInEditor;

            float keyboardH = keyboard.AreaHeight / scale;
            if (keyboardH <= 0f) return;
            DrawPanel(new Rect(0f, height - keyboardH, width, keyboardH), 1f);
            GUI.Label(new Rect(0f, height - keyboardH, width, keyboardH), "(klavye alanı - simülasyon)", centerStyle);
        }

        void DrawRacing(float width, float height, float top, float scale)
        {
            LayoutRaceArea(width, height, scale, out var box, out float devButtonY);
            view.DrawLabels(scale, T("you"), T("bot"));
            if (Time.realtimeSinceStartup < goFlashUntil) DrawStartLights(width, top, LightCount, true);
            // Kendi ilerlemen sunucu cevabını beklemeden yerel durumdan gösterilir
            progressStrip.Draw(new Rect(8f, top + 8f, width - 16f, 26f), players, wordCount, net.UserId, (float)typing.CurrentIndex / Mathf.Max(1, wordCount));
            fireworks.Draw(new Rect(0f, top + 40f, width, height * 0.40f));

            // Yarış sırasında istatistik (WPM, doğruluk, kelime sayısı) gösterilmez; oyuncu yalnızca birinci olmaya odaklanır.
            // İstatistikler yarış sonunda sonuç tablosunda görünür.

            if (typing.Finished)
            {
                // Bitirdin: kelime kutusu yerine "Bitirdin!" ve canlı sıra; yarış (ve kırmızı geri sayım) sürer
                DrawFinishedPanel(box);
            }
            else
            {
                // Kelime kutusunda yalnızca doğru harfler (yeşil) görünür. Klavye kapanırsa kutuya dokunmak yeniden açar.
                if (wordView.Draw(box, typing, input.Entered)) keyboard.Open(KeyboardKind.Default);
            }
            DrawSimulatedKeyboard(width, height, scale, devButtonY);
            DrawEndingBadge(width, top);

            if (noticeText.Length > 0)
            {
                float noticeY = endingEndsLocal > 0f ? top + 134f : top + 42f;
                DrawPanel(new Rect(10f, noticeY, width - 20f, 56f), 0.85f);
                GUI.Label(new Rect(10f, noticeY, width - 20f, 56f), noticeText, centerStyle);
            }
        }

        // Bitirdin paneli: sıraya göre renklenen kupa, "Bitirdin!" ve büyük, renkli sıra numarası
        void DrawFinishedPanel(Rect box)
        {
            int index = finishOrder.IndexOf(net.UserId);
            int place = index >= 0 ? index + 1 : 0;

            var tint = place == 1 ? Pictograms.TrophyTint.Gold : place == 2 ? Pictograms.TrophyTint.Silver : place == 3 ? Pictograms.TrophyTint.Bronze : Pictograms.TrophyTint.Steel;
            Color accent = place == 1 ? new Color(1.00f, 0.83f, 0.30f) : place == 2 ? new Color(0.82f, 0.89f, 1.00f) : place == 3 ? new Color(1.00f, 0.62f, 0.34f) : new Color(0.55f, 0.66f, 0.96f);

            UiKit.Panel(box, 0.88f, 18f);
            UiKit.Card(new Rect(box.x + 22f, box.y + 4f, box.width - 44f, 3f), accent, 2f); // üstte sıra renginde ince şerit

            float icon = box.height - 12f;
            GUI.DrawTexture(new Rect(box.x + 14f, box.y + 6f, icon, icon), Pictograms.Trophy(tint));

            float textX = box.x + icon + 22f;
            float textWidth = box.width - (icon + 34f);

            var old = GUI.color;
            GUI.color = accent;
            GUI.Label(new Rect(textX, box.y + 8f, textWidth, 34f), T("youFinished"), headingStyle);
            GUI.color = old;

            if (place > 0)
            {
                // Sıra numarası daha büyük ve sıra renginde (zengin metin)
                string number = "<size=34><b><color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">" + place + "</color></b></size>";
                GUI.Label(new Rect(textX, box.y + 40f, textWidth, 36f), string.Format(T("positionNow"), number), positionStyle);
            }
        }

        // İlk oyuncu bitirince çıkan kırmızı geri sayım rozeti: büyük kırmızı daire, içinde kalan saniye.
        // Son 3 saniyede daire hafifçe nabız gibi büyüyüp küçülür.
        void DrawEndingBadge(float width, float top)
        {
            if (endingEndsLocal <= 0f) return;

            int seconds = Mathf.Max(0, Mathf.CeilToInt(endingEndsLocal - Time.realtimeSinceStartup));
            float pulse = seconds <= 3 ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 6f)) : 1f;
            float size = 70f * pulse;
            var center = new Vector2(width / 2f, top + 82f);

            var old = GUI.color;
            GUI.color = new Color(0.95f, 0.15f, 0.18f, 0.30f); // dış hale
            GUI.DrawTexture(new Rect(center.x - size * 0.68f, center.y - size * 0.68f, size * 1.36f, size * 1.36f), SoundIcon.Disc());
            GUI.color = new Color(0.90f, 0.14f, 0.17f, 1f);    // kırmızı daire
            GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), SoundIcon.Disc());
            GUI.color = old;

            GUI.Label(new Rect(center.x - size / 2f, center.y - size / 2f - 1f, size, size), seconds.ToString(), headingStyle);

            // Altındaki yazı, pistin üstünde okunsun diye koyu bir zeminde
            string label = T("raceEnding");
            float labelWidth = smallCenterStyle.CalcSize(new GUIContent(label)).x + 28f;
            var labelRect = new Rect(center.x - labelWidth / 2f, center.y + size / 2f + 8f, labelWidth, 24f);
            UiKit.Panel(labelRect, 0.78f, 12f);
            GUI.color = new Color(1f, 0.55f, 0.55f);
            GUI.Label(labelRect, label, smallCenterStyle);
            GUI.color = old;
        }

        // Geliştirme: "bitirdin + kırmızı geri sayım" görünümünü doğrular (yalnızca DevHarness kullanır)
        public void DevShowFinishedEnding()
        {
            if (typing == null) return;
            typing.SyncTo(typing.Count);
            finishOrder.Clear();
            finishOrder.Add(net.UserId);
            fireworks.Launch(true);
            endingEndsLocal = Time.realtimeSinceStartup + 7.4f;
            screen = Page.Racing;
        }

        // Alttan yukarı: klavye, kelime kutusu; kalan üst bölge 3D pist.
        // devButtonY: editördeki klavye simülasyonu düğmesinin yeri (pistin alt kenarı, kelime kutusunun hemen üstü).
        void LayoutRaceArea(float width, float height, float scale, out Rect box, out float devButtonY)
        {
            float keyboardH = keyboard.AreaHeight / scale;
            float bottomEdge = height - keyboardH - 8f;

            box = new Rect(8f, bottomEdge - WordLinesView.BoxHeight, width - 16f, WordLinesView.BoxHeight);
            devButtonY = box.y - 30f;

            float trackBottomPx = (height - box.y + 6f) * scale; // ekranın altından piksel olarak
            view.SetViewport(new Rect(0f, trackBottomPx, UnityEngine.Screen.width, UnityEngine.Screen.height - trackBottomPx));
        }

        // Ödül sırası: kupa rengi, konfeti ve zafer müziği için TEK kural, yalnızca SIRAYA bağlı.
        // İlk üçte olan (yarışı bitirsin ya da süre dolduğu için bitirmesin) altın/gümüş/bronz kupayı, konfetiyi ve zafer müziğini alır.
        // Sıralamayı sunucu zaten adil kurar: diskalifiyeler en sona atılır, bu yüzden ilk üçe giremezler.
        int PodiumRank()
        {
            foreach (var r in results)
                if (r.userId == net.UserId) return r.rank;
            return 4;
        }

        // Sonuç ekranı: sıraya göre renklenen büyük bir kupa, altında sıralama kartları ve düğmeler
        void DrawResults(float width, float height, float top)
        {
            FullViewport();

            int podiumRank = PodiumRank();
            const float trophySize = 128f;
            var trophyCenter = new Vector2(width / 2f, top + 8f + trophySize / 2f);
            resultsBackdrop.Draw(width, height, trophyCenter, trophySize, podiumRank);

            float y = top + trophySize + 10f;
            GUI.Label(new Rect(0f, y, width, 36f), T("results"), headingStyle);
            y += 44f;

            foreach (var r in results)
            {
                bool mine = r.userId == net.UserId;
                string name = mine ? T("you") : r.isBot ? T("bot") + " (" + r.name + ")" : r.name;
                string status = r.status == "finished" ? "" : r.status == "disqualified" ? T("disqualified") : T("dnf");

                var card = new Rect(16f, y, width - 32f, 50f);
                UiKit.Card(card, mine ? new Color(1f, 0.82f, 0.30f, 0.24f) : new Color(1f, 1f, 1f, 0.07f), 14f);

                GUI.Label(new Rect(card.x + 10f, card.y, 36f, card.height), r.rank.ToString(), rankStyle);

                var oldColor = GUI.color;
                GUI.color = mine ? new Color(1f, 0.92f, 0.45f) : Color.white;
                GUI.Label(new Rect(card.x + 52f, card.y + 4f, card.width - 60f, 24f), name, labelStyle);
                GUI.color = oldColor;

                GUI.Label(new Rect(card.x + 52f, card.y + 26f, card.width - 60f, 20f),
                    r.wpm.ToString("0") + " " + T("wpm") + "   %" + (r.accuracy * 100f).ToString("0") + "   " + (r.finishMs > 0 ? (r.finishMs / 1000f).ToString("0.0") + " s" : "") + "   " + status, smallStyle);
                y += 56f;
            }

            y = Mathf.Max(y + 10f, height - 148f);
            if (UiKit.PrimaryButton(new Rect(24f, y, width - 48f, 62f), T("playAgain"), 22, true)) AgainAfterResults();
            if (UiKit.SecondaryButton(new Rect(24f, y + 76f, width - 48f, 50f), T("menu"), 20)) BackToMenu();
        }

        // Geliştirme: sahte sonuç göstererek ekranı doğrular (yalnızca DevHarness kullanır)
        public void DevShowFakeResults(int myRank, string myStatus = "finished")
        {
            var list = new ResultDto[5];
            for (int i = 0; i < 5; i++)
            {
                bool me = i + 1 == myRank;
                list[i] = new ResultDto
                {
                    userId = me ? net.UserId : "bot-" + (i + 1),
                    name = "Bot " + (i + 1),
                    isBot = !me,
                    rank = i + 1,
                    status = me ? myStatus : "finished",
                    finishMs = 31000 + i * 3000,
                    wpm = 78f - i * 9f,
                    accuracy = 0.97f - i * 0.01f,
                    correctWords = 40,
                    errors = i
                };
            }
            results = list;
            resultsBackdrop.Restart();
            screen = Page.Results;
        }

        async void AgainAfterResults()
        {
            await net.LeaveAsync();
            StartSearch();
        }

        async void BackToMenu()
        {
            sound.StopMusic();
            await net.LeaveAsync();
            view.ResetToStart();
            screen = Page.Menu;
        }

        void OnDestroy()
        {
            _ = net?.CloseAsync();
        }
    }
}
