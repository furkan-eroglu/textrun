using System;
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
        enum Page { Connecting, Menu, Searching, Countdown, Racing, Results, Error }

        const string LanguageKey = "textrun.language";

        readonly KeyboardInput keyboard = new KeyboardInput();
        readonly WordLinesView wordView = new WordLinesView();
        readonly RaceProgressStrip progressStrip = new RaceProgressStrip();

        OnlineRaceClient net;
        OnlineRaceView view;
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
        float countdownEndsLocal;

        GUIStyle titleStyle, bigButton, labelStyle, centerStyle, smallStyle, statsStyle;

        void Start()
        {
            Application.targetFrameRate = 60;
            language = (GameLanguage)PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Turkish);
            hostInput = ServerSettings.Host;

            gameObject.AddComponent<DevHarness>(); // komut satırında -devharness yoksa kendini kaldırır
            view = gameObject.AddComponent<OnlineRaceView>();
            view.Build();
            view.SetAllActive(new[] { "", "", "", "", "" }, 2);
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
            errorText = T("connectionLost");
            screen = Page.Error;
        }

        // ---------- Oyun akışı ----------

        async void StartSearch()
        {
            if (searchBusy) return;
            searchBusy = true;
            noticeText = "";
            screen = Page.Searching;
            players = new PlayerDto[0];
            lobbyEndsLocal = 0f;
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

            switch (s.phase)
            {
                case Phase.Lobby:
                    screen = Page.Searching;
                    break;

                case Phase.Countdown:
                    PrepareTyping(s);
                    countdownEndsLocal = now + (s.startAt - s.serverNow) / 1000f;
                    screen = Page.Countdown;
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
            typing.MarkStarted(Time.realtimeSinceStartup);
            view.SetRacing(true);
            keyboard.Open(KeyboardKind.Default);
            BeginInput();
        }

        void OnProgress(ProgressDto progress)
        {
            players = progress.players ?? players;
            view.SetProgress(players, wordCount);
        }

        void OnAck(AckDto ack)
        {
            if (typing == null) return;
            if (ack.kind == AckKind.Disqualified) noticeText = T("disqualifiedNotice");
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
            screen = Page.Results;
        }

        // ---------- Yazma girişi ----------

        void Update()
        {
            net?.Pump();

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
            statsStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = false, fontStyle = FontStyle.Bold };
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

            switch (screen)
            {
                case Page.Connecting: DrawCenterMessage(width, height, T("connecting")); break;
                case Page.Error: DrawError(width, height); break;
                case Page.Menu: DrawMenu(width, height, top); break;
                case Page.Searching: DrawSearching(width, height, top); break;
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

        void DrawPanel(Rect r, float alpha = 0.75f)
        {
            GUI.color = new Color(0.06f, 0.08f, 0.13f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
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

            if (GUI.Button(new Rect(30f, height / 2f + 35f, width - 60f, 50f), T("retry"), bigButton)) Connect();
        }

        void DrawMenu(float width, float height, float top)
        {
            FullViewport();
            DrawPanel(new Rect(0f, 0f, width, height), 0.55f);

            GUI.Label(new Rect(0f, top + 20f, width, 60f), "TextRun", titleStyle);

            float y = top + 100f;
            GUI.Label(new Rect(20f, y, width - 40f, 26f), T("language"), labelStyle);
            y += 30f;
            float half = (width - 50f) / 2f;
            if (DrawChoice(new Rect(20f, y, half, 44f), "Türkçe", language == GameLanguage.Turkish)) SetLanguage(GameLanguage.Turkish);
            if (DrawChoice(new Rect(30f + half, y, half, 44f), "English", language == GameLanguage.English)) SetLanguage(GameLanguage.English);
            y += 70f;

            if (GUI.Button(new Rect(20f, y, width - 40f, 64f), T("normalGame"), bigButton)) StartSearch();
            y += 76f;

            GUI.enabled = false;
            GUI.Button(new Rect(20f, y, width - 40f, 54f), T("privateGame"), bigButton);
            GUI.enabled = true;

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

        bool DrawChoice(Rect r, string text, bool selected)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = selected ? new Color(0.3f, 0.6f, 1f) : Color.gray;
            bool clicked = GUI.Button(r, text, bigButton);
            GUI.backgroundColor = old;
            return clicked;
        }

        void SetLanguage(GameLanguage value)
        {
            language = value;
            PlayerPrefs.SetInt(LanguageKey, (int)value);
            PlayerPrefs.Save();
        }

        void DrawSearching(float width, float height, float top)
        {
            FullViewport();
            DrawPanel(new Rect(10f, top + 10f, width - 20f, 230f));

            GUI.Label(new Rect(10f, top + 20f, width - 20f, 36f), T("searching"), centerStyle);
            GUI.Label(new Rect(10f, top + 60f, width - 20f, 30f), T("playersFound") + ": " + HumanCount() + " / 5", centerStyle);

            if (lobbyEndsLocal > 0f)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(lobbyEndsLocal - Time.realtimeSinceStartup));
                GUI.Label(new Rect(10f, top + 95f, width - 20f, 30f), T("botsJoinIn") + " " + seconds, centerStyle);
            }
            if (noticeText.Length > 0) GUI.Label(new Rect(10f, top + 130f, width - 20f, 30f), noticeText, centerStyle);

            if (GUI.Button(new Rect(40f, top + 175f, width - 80f, 46f), T("cancel"), bigButton)) CancelSearch();
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
            LayoutRaceArea(width, height, scale, out var box, out float statsY);
            view.DrawLabels(scale, T("you"), T("bot"));
            progressStrip.Draw(new Rect(8f, top + 8f, width - 16f, 26f), players, wordCount, net.UserId, 0f);

            int seconds = Mathf.Max(0, Mathf.CeilToInt(countdownEndsLocal - Time.realtimeSinceStartup));
            DrawPanel(new Rect(width / 2f - 90f, top + 46f, 180f, 90f), 0.6f);
            GUI.Label(new Rect(width / 2f - 90f, top + 46f, 180f, 90f), seconds > 0 ? seconds.ToString() : T("getReady"), titleStyle);

            if (typing != null) wordView.Draw(box, typing, "");
            DrawSimulatedKeyboard(width, height, scale, statsY);
        }

        // Yalnızca editörde: telefon klavyesinin kaplayacağı alanı gri bir blokla gösterir, düzeni PC'de telefondaki gibi görmek için.
        void DrawSimulatedKeyboard(float width, float height, float scale, float statsY)
        {
            if (!Application.isEditor) return;

            // İstatistik satırının sağında (ilerleme şeridiyle çakışmasın)
            if (GUI.Button(new Rect(width - 118f, statsY - 2f, 112f, 24f), KeyboardInput.SimulateInEditor ? "Klavye: AÇIK" : "Klavye: KAPALI"))
                KeyboardInput.SimulateInEditor = !KeyboardInput.SimulateInEditor;

            float keyboardH = keyboard.AreaHeight / scale;
            if (keyboardH <= 0f) return;
            DrawPanel(new Rect(0f, height - keyboardH, width, keyboardH), 1f);
            GUI.Label(new Rect(0f, height - keyboardH, width, keyboardH), "(klavye alanı - simülasyon)", centerStyle);
        }

        void DrawRacing(float width, float height, float top, float scale)
        {
            LayoutRaceArea(width, height, scale, out var box, out float statsY);
            view.DrawLabels(scale, T("you"), T("bot"));
            // Kendi ilerlemen sunucu cevabını beklemeden yerel durumdan gösterilir
            progressStrip.Draw(new Rect(8f, top + 8f, width - 16f, 26f), players, wordCount, net.UserId, (float)typing.CurrentIndex / Mathf.Max(1, wordCount));

            // Tek satır: ikinci satıra kayarsa kelime kutusunun arkasında kalır
            string stats = T("wpm") + " " + typing.Wpm(Time.realtimeSinceStartup).ToString("0")
                + "  |  %" + (typing.Accuracy * 100f).ToString("0")
                + "  |  " + typing.CurrentIndex + "/" + typing.Count;
            GUI.Label(new Rect(8f, statsY, width - 16f, 24f), stats, statsStyle);

            // Kelime kutusunda yalnızca doğru harfler (yeşil) görünür. Klavye kapanırsa kutuya dokunmak yeniden açar.
            if (wordView.Draw(box, typing, input.Entered)) keyboard.Open(KeyboardKind.Default);
            DrawSimulatedKeyboard(width, height, scale, statsY);

            if (noticeText.Length > 0)
            {
                DrawPanel(new Rect(10f, top + 42f, width - 20f, 56f), 0.85f);
                GUI.Label(new Rect(10f, top + 42f, width - 20f, 56f), noticeText, centerStyle);
            }
        }

        // Alttan yukarı: klavye, kelime kutusu, istatistik satırı; kalan üst bölge 3D pist.
        void LayoutRaceArea(float width, float height, float scale, out Rect box, out float statsY)
        {
            float keyboardH = keyboard.AreaHeight / scale;
            float bottomEdge = height - keyboardH - 8f;

            box = new Rect(8f, bottomEdge - WordLinesView.BoxHeight, width - 16f, WordLinesView.BoxHeight);
            statsY = box.y - 26f;

            float trackBottomPx = (height - statsY + 4f) * scale; // ekranın altından piksel olarak
            view.SetViewport(new Rect(0f, trackBottomPx, UnityEngine.Screen.width, UnityEngine.Screen.height - trackBottomPx));
        }

        void DrawResults(float width, float height, float top)
        {
            FullViewport();
            DrawPanel(new Rect(0f, 0f, width, height), 0.96f);
            GUI.Label(new Rect(0f, top + 10f, width, 50f), T("results"), titleStyle);

            float y = top + 70f;
            foreach (var r in results)
            {
                bool mine = r.userId == net.UserId;
                string name = mine ? T("you") : r.isBot ? T("bot") + " (" + r.name + ")" : r.name;
                string status = r.status == "finished" ? "" : r.status == "disqualified" ? T("disqualified") : T("dnf");

                GUI.color = mine ? new Color(1f, 0.92f, 0.4f) : Color.white;
                GUI.Label(new Rect(14f, y, width - 28f, 28f), r.rank + ".  " + name, labelStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(14f, y + 24f, width - 28f, 24f),
                    r.wpm.ToString("0") + " " + T("wpm") + "   %" + (r.accuracy * 100f).ToString("0") + "   " + (r.finishMs > 0 ? (r.finishMs / 1000f).ToString("0.0") + " s" : "") + "   " + status, smallStyle);
                y += 58f;
            }

            y = Mathf.Max(y + 10f, height - 150f);
            if (GUI.Button(new Rect(20f, y, width - 40f, 54f), T("playAgain"), bigButton)) AgainAfterResults();
            if (GUI.Button(new Rect(20f, y + 62f, width - 40f, 46f), T("menu"), bigButton)) BackToMenu();
        }

        async void AgainAfterResults()
        {
            await net.LeaveAsync();
            StartSearch();
        }

        async void BackToMenu()
        {
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
