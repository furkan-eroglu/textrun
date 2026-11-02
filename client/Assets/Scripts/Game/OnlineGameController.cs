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
        enum Page { Connecting, Welcome, Login, NameEntry, Settings, Menu, PrivateMenu, JoinCode, RoomLobby, Searching, MatchFound, Countdown, Racing, Results, Error }

        const string LanguageKey = "textrun.language";

        readonly KeyboardInput keyboard = new KeyboardInput();
        readonly WordLinesView wordView = new WordLinesView();
        readonly RaceProgressStrip progressStrip = new RaceProgressStrip();
        readonly MenuBackdrop backdrop = new MenuBackdrop();
        readonly ResultsBackdrop resultsBackdrop = new ResultsBackdrop();
        readonly Fireworks fireworks = new Fireworks();
        const bool MenuMusicEnabled = false; // menü müziği şimdilik kapalı (kod hazır; true yapınca çalar)
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
        GameLanguage raceLanguage;                 // içinde bulunulan yarışın dili (özel odada odanın dili)
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
        bool isPrivateRoom;
        string roomCode = "";
        string roomHost = "";
        string roomLang = "tr";
        readonly KeyboardTracker tracker = new KeyboardTracker();
        int trackerGeneration = -1;
        bool keyboardRequested;        // yarış ekranında klavye istendi mi (ışıklar başlayınca açılır, yarış bitince kapanır)
        bool roomBots;                 // kurucu: boş yerleri botla doldur
        string joinCodeInput = "";
        ProfileDto profile = new ProfileDto();   // sunucudaki oyuncu profili (ad, XP, seviye); misafirde guest=true
        enum Entry { Guest, Account, Saved }     // sunucuya giriş türü: misafir, kullanıcı adı + şifre, telefonda kayıtlı hesap
        Entry lastEntry = Entry.Guest;
        string loginInput = "";
        string passInput = "";
        string passAgainInput = "";
        bool showPassword;

        // Giriş/kayıt formundaki her şeyi siler (çıkışta, sekme değişince ve ekrandan ayrılınca eski bilgi kalmasın)
        void ClearLoginForm()
        {
            loginInput = ""; passInput = ""; passAgainInput = ""; showPassword = false; loginNotice = "";
        }
        GUIStyle passwordToggleStyle;
        GUIStyle PasswordToggleStyle()
        {
            if (passwordToggleStyle != null) return passwordToggleStyle;
            passwordToggleStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            UiKit.SetAllTextColors(passwordToggleStyle, new Color(0.45f, 0.75f, 1f));
            return passwordToggleStyle;
        }
        string loginNotice = "";
        bool registerMode;
        string nameInput = "";
        string nameNotice = "";
        bool nameBusy;
        RewardDto myReward;                      // son yarıştan kazanılan XP (sonuç ekranında gösterilir)
        string codeNotice = "";
        float copiedUntil;
        float matchFoundSeconds;

        GUIStyle nameFieldStyle, rowNameStyle, rowTagStyle, codeStyle, codeFieldStyle, titleStyle, bigButton, labelStyle, centerStyle, smallStyle, smallCenterStyle, stopwatchStyle, headingStyle, rankStyle, searchTitleStyle, positionStyle;

        void Start()
        {
            Application.targetFrameRate = 60;
            Debug.developerConsoleVisible = false; // geliştirme sürümünde ekrana basılan hata yazıları gizli (günlüğe yazılmaya devam eder)
            language = (GameLanguage)PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Turkish);
            raceLanguage = language;
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

            // Bu telefonda kayıtlı hesap varsa doğrudan girilir; yoksa giriş seçimi gelir (misafir olarak girilince bağlanılır)
            if (OnlineRaceClient.HasSavedAccount) Connect(Entry.Saved);
            else screen = Page.Welcome;
        }

        string T(string key) => Loc.T(key, language);

        // --- Geliştirme araçları: yalnızca DevHarness (-devharness komut satırı seçeneği) kullanır ---
        public string DevPageName => screen.ToString();

        public int DevPlayerCount => players.Length;
        public void DevCreateRoom() { screen = Page.PrivateMenu; CreateRoom(); }
        public void DevLeaveRoom() { LeaveRoom(); }
        public void DevBackToMenu() { isPrivateRoom = false; screen = Page.Menu; }

        public void DevStartSearch()
        {
            if (screen == Page.Menu) StartSearch();
        }

        // ---------- Bağlantı ----------

        void Connect() { Connect(lastEntry); }

        async void Connect(Entry entry)
        {
            lastEntry = entry;
            screen = Page.Connecting;
            if (net != null) await net.CloseAsync();

            // Misafir geçicidir: her bağlanışta yeni kimlik, cihaza kaydedilmez. Hesap girişinde oturum kaydedilir.
            net = new OnlineRaceClient(Guid.NewGuid().ToString(), false);
            if (entry == Entry.Guest) net.GuestName = "Misafir" + UnityEngine.Random.Range(1000, 10000);
            else if (entry == Entry.Saved) net.UseSavedAccount = true;
            else
            {
                net.LoginName = loginInput;
                net.Password = passInput;
                net.Register = registerMode;
            }
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
                profile = await net.GetProfileAsync();
                if (entry != Entry.Guest) lastEntry = Entry.Saved; // yeniden denemede kayıtlı oturum kullanılır (şifre bellekte tutulmaz)
                ClearLoginForm(); // şifre bellekte kalmaz, kullanıcı adı da bir sonraki girişe taşınmaz
                if (entry == Entry.Guest || !string.IsNullOrEmpty(profile.name)) screen = Page.Menu;
                else { nameInput = ""; nameNotice = ""; screen = Page.NameEntry; } // yeni hesap: önce oyun içi ad
            }
            catch (Exception e)
            {
                int status = OnlineRaceClient.StatusOf(e);
                if (entry == Entry.Account && status != 0)
                {
                    lastEntry = Entry.Guest;
                    // Kayıtta var olan bir adla farklı şifre denenirse sunucu 401 döner: kullanıcıya "ad alınmış" denir
                    loginNotice = status == 409 || (registerMode && status == 401) ? T("loginTaken") : status == 400 ? T("passwordShort") : T("loginWrong");
                    screen = Page.Login;
                }
                else if (entry == Entry.Saved && status != 0)
                {
                    // Kayıtlı oturum artık geçerli değil (ör. şifre/hesap silindi): unutulur, giriş seçimine dönülür
                    OnlineRaceClient.ForgetAccount();
                    lastEntry = Entry.Guest;
                    screen = Page.Welcome;
                }
                else Fail(e);
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
            if (screen == Page.Menu || screen == Page.PrivateMenu || screen == Page.JoinCode || screen == Page.Connecting || screen == Page.Error
                || screen == Page.Welcome || screen == Page.Login || screen == Page.NameEntry || screen == Page.Settings) return;
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
            raceLanguage = language;
            isPrivateRoom = false;
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

        // ---------- Özel oda ----------

        async void CreateRoom()
        {
            if (searchBusy) return;
            searchBusy = true;
            ResetForRoom();
            raceLanguage = language;
            roomCode = "";
            roomHost = "";
            isPrivateRoom = true;
            screen = Page.RoomLobby; // sunucudan gelen oyuncu listesi bu ekranda karşılanır (kurucu da listede görünür)

            try
            {
                var room = await net.CreateRoomAsync(language);
                roomCode = room.code;
            }
            catch (Exception e)
            {
                Fail(e);
            }
            finally
            {
                searchBusy = false;
            }
        }

        async void JoinRoom()
        {
            if (searchBusy || joinCodeInput.Length != 5) return;
            searchBusy = true;
            codeNotice = "";
            ResetForRoom();
            roomCode = joinCodeInput;
            roomHost = "";
            isPrivateRoom = true;
            screen = Page.RoomLobby;

            try
            {
                var room = await net.JoinRoomAsync(joinCodeInput);
                raceLanguage = room.lang == "en" ? GameLanguage.English : GameLanguage.Turkish; // yarış odanın dilindedir
                roomCode = room.code;
            }
            catch (Exception e)
            {
                isPrivateRoom = false;
                screen = Page.JoinCode;
                if (OnlineRaceClient.IsError(e, "room_closed")) codeNotice = T("roomStarted");
                else if (OnlineRaceClient.IsError(e, "room_full")) codeNotice = T("roomIsFull");
                else if (OnlineRaceClient.IsError(e, "room_not_found")) codeNotice = T("roomNotFound");
                else Fail(e);
            }
            finally
            {
                searchBusy = false;
            }
        }

        void ResetForRoom()
        {
            noticeText = "";
            players = new PlayerDto[0];
            lobbyEndsLocal = 0f;
            endingEndsLocal = 0f;
            finishOrder.Clear();
            view.ResetToStart();
            view.SetRacing(false);
            wordView.Reset();
            keyboard.Text = "";
        }

        async void LeaveRoom()
        {
            await net.LeaveAsync();
            isPrivateRoom = false;
            screen = Page.PrivateMenu;
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
            if (screen == Page.Menu || screen == Page.PrivateMenu || screen == Page.JoinCode || screen == Page.Error) return;

            if (s.kind == "private") { isPrivateRoom = true; roomCode = s.code ?? ""; roomHost = s.host ?? ""; roomLang = s.lang ?? "tr"; }

            players = s.players ?? new PlayerDto[0];
            view.SetRoster(players, net.UserId);
            noticeText = "";

            float now = Time.realtimeSinceStartup;
            if (s.lobbyEndsAt > 0) lobbyEndsLocal = now + (s.lobbyEndsAt - s.serverNow) / 1000f;
            if (s.endsAt > 0) endingEndsLocal = now + (s.endsAt - s.serverNow) / 1000f; // yeniden bağlanana da geri sayım gösterilir

            switch (s.phase)
            {
                case Phase.Lobby:
                    screen = s.kind == "private" ? Page.RoomLobby : Page.Searching;
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
                    break; // klavye Update içinde açılır

                case Phase.Finished:
                    results = s.results ?? new ResultDto[0];
                    resultsBackdrop.Restart();
                    screen = Page.Results;
                    break;
            }
        }

        void PrepareTyping(SnapshotDto s)
        {
            if (typing != null && typing.Count == s.words.Length && wordCount == s.words.Length && typing.WordAt(0) == TextNormalizer.ToLower(s.words[0], raceLanguage)) return;
            typing = new TypingSession(s.words, raceLanguage);
            wordCount = s.words.Length;
            wordView.Reset();
            input = new WordInput(raceLanguage);
            BeginInput();
        }

        // Giriş süzgecini şu anki aktif kelimeye ayarlar ve klavye metnini temizler.
        void BeginInput()
        {
            if (input == null || typing == null) return;
            input.Begin(typing.Finished ? "" : typing.WordAt(typing.CurrentIndex));
            tracker.Sync(keyboard.Text); // klavye metni silinmez; o ana kadar yazılanlar (ör. ışıklar yanarken) yeni sayılmaz
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
            pendingAcks = 0;
            typing.MarkStarted(Time.realtimeSinceStartup);
            view.SetRacing(true);
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

        string lastAckInfo = "";
        int pendingAcks;               // gönderilmiş ama sunucu cevabı gelmemiş kelime sayısı

        void OnAck(AckDto ack)
        {
            if (typing == null) return;
            lastAckInfo = ack.kind + " srv=" + ack.index + " yerel=" + typing.CurrentIndex;
            if (ack.kind == AckKind.Disqualified) noticeText = T("disqualifiedNotice");
            if (ack.kind == AckKind.Finished)
            {
                // Bitirdin: klavye kapanır, "Bitirdin!" paneli ve canlı sıra gösterilir
                if (!finishOrder.Contains(net.UserId)) finishOrder.Add(net.UserId);
                keyboard.Close();
                sound.Applause(); // bitirdiğin hemen anlaşılsın
                fireworks.Launch(finishOrder.IndexOf(net.UserId) < 3); // ilk üç: büyük gösteri, diğerleri: küçük
            }
            if (pendingAcks > 0) pendingAcks--;
            if (TypingSession.ShouldResync(ack.index, typing.CurrentIndex, pendingAcks, ack.kind == AckKind.Wrong))
            {
                typing.SyncTo(ack.index);
                pendingAcks = 0;
                BeginInput();
            }
        }

        void OnResults(ResultsDto r)
        {
            results = r.results ?? new ResultDto[0];
            myReward = null;
            if (r.rewards != null)
                foreach (var reward in r.rewards)
                    if (reward.userId == net.UserId) myReward = reward;
            RefreshProfile();
            view.SetRacing(false);
            fireworks.Clear();
            keyboard.Close();
            keyboardRequested = false;
            endingEndsLocal = 0f;
            resultsBackdrop.Restart(); // ekran açılınca konfeti yukarıdan süzülmeye başlar
            screen = Page.Results;

            sound.Finish(PodiumRank() <= 3);
        }

        // Yarış sonrası menüdeki seviye çubuğu güncel olsun diye profil sessizce yeniden okunur
        async void RefreshProfile()
        {
            try
            {
                var fresh = await net.GetProfileAsync();
                profile = fresh;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Profil yenilenemedi: " + e.Message);
            }
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

        Page lastScreen = Page.Connecting;

        void Update()
        {
            // Ekran değişince yazı kutusunun odağı bırakılır, telefon klavyesi kapanır (yarışın kendi klavyesi hariç)
            if (screen != lastScreen)
            {
                if (lastScreen == Page.Login && screen != Page.Connecting) ClearLoginForm(); // Geri'ye basıldı: form boşalır (bağlanırken kalır, hata olursa düzeltilebilsin)
                lastScreen = screen;
                GUIUtility.keyboardControl = 0;
                focusField = null;
                if (screen != Page.Countdown && screen != Page.Racing) keyboard.Close();
            }
            ReleaseFieldKeyboardIfUnfocused();

            net?.Pump();
            if (screen == Page.MatchFound && Time.realtimeSinceStartup >= countdownEndsLocal - countdownTotal) screen = Page.Countdown;
            TickCountdownSound();
            TickEndingSound();
            sound.SetMenuMusic(MenuMusicEnabled && (screen == Page.Menu || screen == Page.Searching || screen == Page.MatchFound));

            // Pist ve karakterler yalnızca geri sayım ve yarışta görünür
            bool wantTrack = screen == Page.Countdown || screen == Page.Racing;

            // Tüm ekranlarda 60 FPS (animasyonlar akıcı olsun)
            const int targetFps = 60;
            if (Application.targetFrameRate != targetFps) Application.targetFrameRate = targetFps;
            if (view.IsVisible != wantTrack) view.SetVisible(wantTrack);

            // Klavye pist ekranı gelir gelmez (ışıklar yanarken) açılır; oyuncu bitirince ya da yarış sona erince kapanır
            bool wantKeyboard = screen == Page.Countdown || (screen == Page.Racing && typing != null && !typing.Finished);
            if (wantKeyboard && !keyboardRequested)
            {
                keyboard.Open(KeyboardKind.Default);
                keyboardRequested = true;
            }
            else if (!wantKeyboard && keyboardRequested)
            {
                keyboard.Close();
                keyboardRequested = false;
            }

            if (screen != Page.Racing || typing == null || input == null) return;
            PollTyping();
        }

        // Klavyeden gelen yeni harfleri sırayla süzgeçten geçirir. Klavye metni her zaman "yalnızca doğru harfler" haline geri alınır,
        // böylece yanlış harfler, otomatik düzeltmeler ya da klavyenin kendi eklemeleri girişi bozamaz.
        void PollTyping()
        {
            // Klavye yeniden açıldıysa (ör. kelime kutusuna dokunuldu) klavye metni boştur; ilerleme korunur
            if (trackerGeneration != keyboard.Generation)
            {
                tracker.Sync(keyboard.Text);
                trackerGeneration = keyboard.Generation;
            }

            // Klavye metnine dokunulmaz; yalnızca değişiklik izlenir (bkz. KeyboardTracker). Yanlış harfler klavyede kalır ama giriş onları yok sayar.
            tracker.Update(keyboard.Text, raceLanguage, c => !typing.Finished && HandleKey(c), () => input.TrimTo(input.Entered.Length - 1));
        }

        GUIStyle debugStyle;

        // Geliştirme sürümü (telefon): klavye ve giriş durumunu okunur küçük bir kutuda gösterir; takılma olursa ekran görüntüsünden teşhis için
        void DrawKeyboardDebug(float width, float top)
        {
            if (debugStyle == null) debugStyle = new GUIStyle(smallStyle) { fontSize = 12, wordWrap = false, clipping = TextClipping.Clip };
            UiKit.SetAllTextColors(debugStyle, Color.white);

            string kb = keyboard.Text ?? "";
            if (kb.Length > 28) kb = "..." + kb.Substring(kb.Length - 28);
            string expected = typing.Finished ? "-" : typing.WordAt(typing.CurrentIndex);

            var rect = new Rect(8f, top + 38f, width - 16f, 50f);
            UiKit.Panel(rect, 0.8f, 8f);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), "kb[" + (keyboard.Text ?? "").Length + "] '" + kb + "'", debugStyle);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 17f, rect.width - 12f, 16f), "giris '" + input.Entered + "'  beklenen '" + expected + "'  idx " + typing.CurrentIndex, debugStyle);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 32f, rect.width - 12f, 16f), "ack: " + lastAckInfo, debugStyle);
        }

        // Harfi girişe verir; giriş tarafından kabul edildiyse (doğru harf ya da kelime gönderimi) true döner.
        bool HandleKey(char c)
        {
            switch (input.Press(c))
            {
                case KeyResult.Rejected:
                    wordView.FlashMistake();
                    return false;

                case KeyResult.Submit:
                    SubmitCurrentWord();
                    return false; // boşluk girişte bir harf değildir; silinirse geri alınacak bir şey yok

                case KeyResult.Accepted:
                    return true;

                default:
                    return false;
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
            pendingAcks++;

            input.Begin(typing.Finished ? "" : typing.WordAt(typing.CurrentIndex));
            tracker.Boundary(); // gönderilen kelimenin harfleri yeni kelimeyi etkilemez
            keyboard.Text = ""; // klavye metni kısa kalsın: uzayan metinde telefon klavyesi takılıyordu. Yalnızca kelime sınırında silinir, yanlış harfte asla.
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
            codeStyle = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            UiKit.SetAllTextColors(codeStyle, new Color(1f, 0.84f, 0.32f));
            codeFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            UiKit.SetAllTextColors(codeFieldStyle, Color.white);
            nameFieldStyle = new GUIStyle(codeFieldStyle) { fontSize = 28 };
            rowNameStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft };
            rowTagStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight };
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
            GUI.skin.settings.cursorColor = Color.clear;

            float scale = Mathf.Max(1.2f, ScreenDpi() / 160f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = UnityEngine.Screen.width / scale;
            float height = UnityEngine.Screen.height / scale;
            float top = (UnityEngine.Screen.height - UnityEngine.Screen.safeArea.yMax) / scale + 6f;

            if (screen == Page.Connecting || screen == Page.Menu || screen == Page.PrivateMenu || screen == Page.JoinCode || screen == Page.RoomLobby || screen == Page.Searching || screen == Page.MatchFound || screen == Page.Error)
                backdrop.Draw(width, height);

            switch (screen)
            {
                case Page.Connecting: DrawCenterMessage(width, height, T("connecting")); break;
                case Page.Error: DrawError(width, height); break;
                case Page.Welcome: DrawWelcome(width, height, top); break;
                case Page.Login: DrawLogin(width, height, top); break;
                case Page.NameEntry: DrawNameEntry(width, height, top); break;
                case Page.Settings: DrawSettings(width, height, top); break;
                case Page.Menu: DrawMenu(width, height, top); break;
                case Page.PrivateMenu: DrawPrivateMenu(width, height, top); break;
                case Page.JoinCode: DrawJoinCode(width, height, top); break;
                case Page.RoomLobby: DrawRoomLobby(width, height, top); break;
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

            bool dev = Application.isEditor || Debug.isDebugBuild;
            if (UiKit.SecondaryButton(new Rect(30f, height / 2f + 30f, width - 60f, 56f), T("retry"), 24))
            {
                if (dev) ServerSettings.Host = hostInput; // yazılan adres de denenir
                Connect();
            }

            // Geliştirme: telefonda sunucu adresi yanlışsa buradan düzeltilir (menüye ulaşılamadığı için burada da bulunur)
            if (dev)
            {
                float hy = height / 2f + 130f;
                GUI.Label(new Rect(30f, hy, width - 60f, 22f), T("server"), smallStyle);
                hostInput = GUI.TextField(new Rect(30f, hy + 26f, width - 60f, 44f), hostInput, labelStyle);
            }
        }

        // Geliştirme: hata ekranını gösterir (yalnızca DevHarness kullanır)
        public void DevShowError()
        {
            errorText = "An error occurred while sending the request";
            screen = Page.Error;
        }

        void DrawMenu(float width, float height, float top)
        {
            FullViewport();

            logo.Draw(width, top + 130f, LogoElapsed());

            DrawSoundButton(new Rect(width - 54f, top + 8f, 42f, 42f));

            // Oyuncu kartı (ad, seviye, XP çubuğu): dokununca ayarlar açılır
            var card = new Rect(12f, top + 8f, width - 76f, 58f);
            DrawProfileCard(card);
            if (GUI.Button(card, GUIContent.none, GUIStyle.none)) OpenSettings();

            // Dil seçimi: başlık yok, yalnızca iki seçenek
            float y = top + 220f;
            int clickedLanguage = UiKit.Segmented(new Rect(24f, y, width - 48f, 52f), new[] { "Türkçe", "English" }, language == GameLanguage.Turkish ? 0 : 1, 21);
            if (clickedLanguage >= 0) SetLanguage((GameLanguage)clickedLanguage);
            y += 86f;

            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 76f), T("normalGame"), 26)) StartSearch();
            y += 96f;

            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 56f), T("privateGame"), 20))
            {
                joinCodeInput = "";
                codeNotice = "";
                screen = Page.PrivateMenu;
            }

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

        // Logo animasyon saati: menü/karşılama her açıldığında harfler yeniden kayarak gelir.
        // Saat kare kare ilerler ve tek karede en fazla 1/30 sn atlar: takılan bir kare animasyonu "zıplatmaz".
        float LogoElapsed()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastMenuDrawAt > 0.5f) menuShownAt = 0f;
            if (Event.current.type == EventType.Repaint)
            {
                menuShownAt += Mathf.Min(now - lastMenuDrawAt, 1f / 30f);
                lastMenuDrawAt = now;
            }
            return menuShownAt;
        }

        // ---------- Hesap: karşılama, ad, ayarlar, seviye ----------

        // Ad, seviye ve XP çubuğu olan kart. Misafirde seviye/XP yoktur: yalnızca ad ve "Misafir" etiketi.
        void DrawProfileCard(Rect r)
        {
            UiKit.Card(r, new Color(1f, 1f, 1f, 0.08f), 14f);

            string name = profile.guest ? (net?.Username ?? "-") : string.IsNullOrEmpty(profile.name) ? "-" : profile.name;
            GUI.Label(new Rect(r.x + 12f, r.y + 3f, r.width - 100f, 26f), name, rowNameStyle);
            if (profile.guest)
            {
                GUI.Label(new Rect(r.xMax - 118f, r.y + 3f, 106f, 26f), T("guestTag"), rowTagStyle);
                return;
            }
            GUI.Label(new Rect(r.xMax - 118f, r.y + 3f, 106f, 26f), T("level") + " " + profile.level, rowTagStyle);

            var bar = new Rect(r.x + 12f, r.yMax - 20f, r.width - 24f, 10f);
            DrawXpBar(bar, profile.xpIntoLevel, profile.xpForNext);
        }

        static void DrawXpBar(Rect bar, int into, int needed)
        {
            UiKit.Card(bar, new Color(0f, 0f, 0f, 0.35f), 5f);
            float t = needed > 0 ? Mathf.Clamp01(into / (float)needed) : 0f;
            if (t > 0f) UiKit.Card(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * t), bar.height), new Color(0.27f, 0.64f, 1f), 5f);
        }

        // Karşılama: kullanıcı adı + şifre ile giriş/kayıt, misafir, (yakında) Google ve Apple
        void DrawWelcome(float width, float height, float top)
        {
            FullViewport();
            logo.Draw(width, top + 110f, LogoElapsed());

            float y = top + 210f;
            GUI.Label(new Rect(0f, y, width, 44f), T("welcomeTitle"), headingStyle);
            y += 64f;

            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 64f), T("accountLogin"), 22))
            {
                loginNotice = "";
                screen = Page.Login;
            }
            y += 78f;
            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 56f), T("guest"), 20)) ContinueAsGuest();
            y += 72f;
            // Google ve Apple girişi, mağaza hesapları bağlanınca açılacak
            UiKit.DisabledButton(new Rect(24f, y, width - 48f, 50f), T("signGoogle") + " (" + T("soon") + ")", 18);
            y += 62f;
            UiKit.DisabledButton(new Rect(24f, y, width - 48f, 50f), T("signApple") + " (" + T("soon") + ")", 18);
            y += 66f;
            GUI.Label(new Rect(30f, y, width - 60f, 60f), T("welcomeNote"), smallCenterWrapStyle());
        }

        GUIStyle smallCenterWrap;
        GUIStyle smallCenterWrapStyle()
        {
            if (smallCenterWrap == null) smallCenterWrap = new GUIStyle(smallCenterStyle) { wordWrap = true, fontSize = 14 };
            return smallCenterWrap;
        }

        // Giriş adı: yalnızca küçük harf, rakam, alt çizgi
        static string CleanLogin(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (raw ?? "").ToLowerInvariant())
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') sb.Append(c);
            return sb.ToString();
        }

        static bool LoginIsValid(string login, string password) => login.Length >= 3 && login.Length <= 16 && password.Length >= 8;

        // Oyun içi ad: sunucudaki kuralın aynısı (son karar sunucunundur)
        static string CleanName(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw ?? "")
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || " çğıöşüÇĞİÖŞÜ_.-".IndexOf(c) >= 0) sb.Append(c);
            return sb.ToString();
        }

        static bool NameIsValid(string raw)
        {
            string name = System.Text.RegularExpressions.Regex.Replace((raw ?? "").Trim(), @"\s+", " ");
            if (name.Length < 3 || name.Length > 12) return false;
            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) return true;
            return false;
        }

        // Kullanıcı adı + şifre ile giriş ya da kayıt: üstte iki sekme, giriş yapma varsayılan
        void DrawLogin(float width, float height, float top)
        {
            FullViewport();
            GUI.Label(new Rect(0f, top + 30f, width, 44f), T(registerMode ? "registerTitle" : "loginTitle"), headingStyle);

            float y = top + 96f;
            int tab = UiKit.Segmented(new Rect(24f, y, width - 48f, 50f), new[] { T("login"), T("register") }, registerMode ? 1 : 0, 20);
            if (tab >= 0 && (tab == 1) != registerMode)
            {
                registerMode = tab == 1;
                ClearLoginForm();
                focusField = null;
            }
            y += 70f;

            GUI.Label(new Rect(24f, y, width - 48f, 22f), T("username"), smallStyle);
            y += 24f;
            loginInput = CleanLogin(DrawField(new Rect(24f, y, width - 48f, 58f), loginInput ?? "", 16, "loginField", nameFieldStyle));
            y += 68f;
            GUI.Label(new Rect(24f, y, width - 48f, 22f), T("password"), smallStyle);
            if (GUI.Button(new Rect(width - 24f - 120f, y - 4f, 120f, 30f), T(showPassword ? "hidePassword" : "showPassword"), PasswordToggleStyle())) showPassword = !showPassword;
            y += 24f;
            passInput = DrawField(new Rect(24f, y, width - 48f, 58f), passInput ?? "", 32, "passField", nameFieldStyle, true);
            y += 66f;

            // Şifre tekrarı yalnızca kayıtta istenir
            bool mismatch = registerMode && passAgainInput.Length > 0 && passAgainInput != passInput;
            if (registerMode)
            {
                GUI.Label(new Rect(24f, y, width - 48f, 22f), T("passwordAgain"), smallStyle);
                y += 24f;
                passAgainInput = DrawField(new Rect(24f, y, width - 48f, 58f), passAgainInput ?? "", 32, "passAgainField", nameFieldStyle, true);
                y += 66f;
            }

            // Kurallar yalnızca kayıtta yazar; girişte yalnızca hata mesajı çıkar
            string hint = loginNotice.Length > 0 ? loginNotice : mismatch ? T("passwordMismatch") : registerMode ? T("loginRule") : "";
            GUI.Label(new Rect(24f, y, width - 48f, 44f), hint, smallCenterWrapStyle());
            y += 52f;

            string action = T(registerMode ? "register" : "login");
            bool canSubmit = registerMode ? LoginIsValid(loginInput, passInput) && passAgainInput == passInput : loginInput.Length > 0 && passInput.Length > 0;
            if (canSubmit) { if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 62f), action, 24)) SubmitLogin(registerMode); }
            else UiKit.DisabledButton(new Rect(24f, y, width - 48f, 62f), action, 24);
            y += 80f;

            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 50f), T("back"), 20)) screen = Page.Welcome;
        }

        // Yazı kutusu. Metni, imleci ve odağı kendimiz yönetiriz; telefonda yalnızca sistemin yerel klavyesini kullanır.
        //  - İmleç her zaman yazının sonundadır.
        //  - Kutuya tekrar dokunmak metni silmez (klavye zaten açıksa dokunuş yok sayılır, kapalıysa mevcut metinle yeniden açılır).
        string pendingFocus;
        string focusField;          // odaktaki kutunun adı (yoksa null)
        string fieldLast = "";      // odaktaki kutunun son döndürülen metni (çağıranın metni değiştirip değiştirmediğini anlamak için)
        TouchScreenKeyboard fieldKb;
        bool fieldKbUsed;           // bu kutular klavye açtı: odak bırakılınca sistemden gizlenir
        readonly Dictionary<GUIStyle, GUIStyle> rightAlignedStyles = new Dictionary<GUIStyle, GUIStyle>();

        string DrawField(Rect r, string text, int maxLength, string controlName, GUIStyle style, bool password = false)
        {
            var e = Event.current;
            text = text ?? "";

            if (pendingFocus == controlName)
            {
                FocusField(controlName, text);
                pendingFocus = null;
            }

            if (e.type == EventType.MouseDown)
            {
                if (r.Contains(e.mousePosition))
                {
                    if (focusField != controlName) FocusField(controlName, text);
                    else if (Application.isMobilePlatform && fieldKb == null) FocusField(controlName, text); // klavye kapanmış: aynı metinle yeniden aç
                    e.Use();
                }
                else if (focusField == controlName) focusField = null; // başka yere dokunuldu (başka kutuysa o odağı alır)
            }

            bool focused = focusField == controlName;
            if (focused)
            {
                if (Application.isMobilePlatform)
                {
                    if (fieldKb != null)
                    {
                        if (text != fieldLast) fieldKb.text = text; // çağıran metni düzeltti (ör. izin verilmeyen harf atıldı)
                        else text = fieldKb.text ?? "";
                        if (text.Length > maxLength) { text = text.Substring(0, maxLength); fieldKb.text = text; }
                        var status = fieldKb.status;
                        if (status == TouchScreenKeyboard.Status.Done || status == TouchScreenKeyboard.Status.Canceled || status == TouchScreenKeyboard.Status.LostFocus)
                            focusField = null; // klavye kapatıldı
                    }
                }
                else if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Backspace) { if (text.Length > 0) text = text.Substring(0, text.Length - 1); e.Use(); }
                    else if (e.character != '\0' && !char.IsControl(e.character)) { if (text.Length < maxLength) text += e.character; e.Use(); }
                }
                fieldLast = text;
            }

            if (e.type == EventType.Repaint)
            {
                string shown = password && !showPassword ? new string('*', text.Length) : text;
                var content = new GUIContent(shown);
                var drawStyle = style;
                if (shown.Length > 0 && style.CalcSize(content).x > r.width - 28f)
                {
                    // Uzun metin: sonu görünsün diye sağa yaslanır
                    if (!rightAlignedStyles.TryGetValue(style, out drawStyle))
                    {
                        drawStyle = new GUIStyle(style) { alignment = TextAnchor.MiddleRight, padding = new RectOffset(style.padding.left, style.padding.right + 8, style.padding.top, style.padding.bottom) };
                        rightAlignedStyles[style] = drawStyle;
                    }
                }
                drawStyle.Draw(r, content, false, false, false, focused);

                if (focused && ((int)(Time.realtimeSinceStartup * 2f) & 1) == 0)
                {
                    // İmleç yazının hemen sonunda (yazı boşsa kutunun ortasında)
                    Vector2 end = shown.Length == 0 ? new Vector2(r.center.x, 0f) : drawStyle.GetCursorPixelPosition(r, content, shown.Length);
                    float caretHeight = style.fontSize * 1.25f;
                    var old = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(end.x + 1f, r.center.y - caretHeight / 2f, 2f, caretHeight), Texture2D.whiteTexture);
                    GUI.color = old;
                }
            }
            return text;
        }

        void FocusField(string controlName, string text)
        {
            focusField = controlName;
            fieldLast = text;
            if (!Application.isMobilePlatform) return;
            if (fieldKb != null) fieldKb.active = false;
            TouchScreenKeyboard.hideInput = true;
            // Yarıştaki klavyeyle aynı ayar: otomatik düzeltme kapalı, tek satır, "şifre tipi" (telefon klavyesi metni kendi kafasına göre değiştirmesin)
            fieldKb = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default, false, false, true, false, "", 0);
            fieldKbUsed = true;
        }

        // Odak bırakıldıysa klavyeyi sistemden gizler (başka kutuya geçilmişse odak null değildir, klavye açık kalır)
        void ReleaseFieldKeyboardIfUnfocused()
        {
            if (focusField != null || !fieldKbUsed) return;
            fieldKbUsed = false;
            if (fieldKb != null) { fieldKb.active = false; fieldKb = null; }
            keyboard.Close();
        }

        public void DevFocusField(string controlName) { pendingFocus = controlName; }
        public bool DevCaretOn => ((int)(Time.realtimeSinceStartup * 2f) & 1) == 0;
        public void DevFillLogin(string user, string pass, string again, bool reveal)
        {
            loginInput = user; passInput = pass; passAgainInput = again; showPassword = reveal;
        }

        void SubmitLogin(bool register)
        {
            if (screen == Page.Connecting) return;
            if (Debug.isDebugBuild || Application.isEditor) ServerSettings.Host = hostInput;
            registerMode = register;
            loginNotice = "";
            Connect(Entry.Account);
        }

        // Yeni hesap: oyun içi ad (giriş adından ayrıdır)
        void DrawNameEntry(float width, float height, float top)
        {
            FullViewport();
            GUI.Label(new Rect(0f, top + 40f, width, 44f), T("chooseName"), headingStyle);

            float y = top + 130f;
            nameInput = CleanName(DrawField(new Rect(24f, y, width - 48f, 70f), nameInput ?? "", 12, "nameField", nameFieldStyle));
            y += 82f;
            GUI.Label(new Rect(24f, y, width - 48f, 44f), nameNotice.Length > 0 ? nameNotice : T("nameRule"), smallCenterWrapStyle());
            y += 60f;

            if (NameIsValid(nameInput) && !nameBusy) { if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 64f), T("continue"), 24)) SubmitName(true); }
            else UiKit.DisabledButton(new Rect(24f, y, width - 48f, 64f), T("continue"), 24);
        }

        async void SubmitName(bool first)
        {
            if (nameBusy) return;
            nameBusy = true;
            nameNotice = "";
            try
            {
                profile = await net.SetNameAsync(nameInput);
                if (first) screen = Page.Menu;
                else { nameInput = profile.name; nameNotice = T("saved"); }
            }
            catch (Exception e)
            {
                if (OnlineRaceClient.IsError(e, "name_invalid")) nameNotice = T("nameInvalid");
                else Fail(e);
            }
            finally
            {
                nameBusy = false;
            }
        }

        // Ayarlar / hesap: misafirde bilgi ve giriş düğmesi; hesapta oyun içi ad, istatistikler ve çıkış
        void DrawSettings(float width, float height, float top)
        {
            FullViewport();
            GUI.Label(new Rect(0f, top + 30f, width, 44f), T("settings"), headingStyle);

            float y = top + 100f;
            DrawProfileCard(new Rect(16f, y, width - 32f, 58f));
            y += 64f;

            if (profile.guest)
            {
                y += 14f;
                GUI.Label(new Rect(30f, y, width - 60f, 80f), T("guestNote"), smallCenterWrapStyle());
                y += 100f;
                if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 58f), T("accountLogin"), 20)) Logout();
                y += 72f;
            }
            else
            {
                GUI.Label(new Rect(16f, y, width - 32f, 22f), profile.xpIntoLevel + " / " + profile.xpForNext + " XP     " + T("raceCount") + " " + profile.races + "     " + T("winCount") + " " + profile.wins, smallCenterStyle);
                y += 44f;

                GUI.Label(new Rect(24f, y, width - 48f, 22f), T("playerName"), smallStyle);
                y += 24f;
                nameInput = CleanName(DrawField(new Rect(24f, y, width - 48f, 58f), nameInput ?? "", 12, "nameField", nameFieldStyle));
                y += 64f;
                GUI.Label(new Rect(24f, y, width - 48f, 44f), nameNotice.Length > 0 ? nameNotice : T("nameRule"), smallCenterWrapStyle());
                y += 50f;

                bool changed = NameIsValid(nameInput) && nameInput.Trim() != profile.name;
                if (changed && !nameBusy) { if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 54f), T("save"), 20)) SubmitName(false); }
                else UiKit.DisabledButton(new Rect(24f, y, width - 48f, 54f), T("save"), 20);
                y += 66f;

                if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 54f), T("logout"), 20)) Logout();
                y += 66f;
            }

            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 50f), T("back"), 20)) screen = Page.Menu;
        }

        void OpenSettings()
        {
            nameInput = profile.name ?? "";
            nameNotice = "";
            screen = Page.Settings;
        }

        // Misafir olarak girer: her seferinde yeni, geçici bir hesap (cihaza kaydedilmez, oyun kapanınca kaybolur) ve otomatik ad
        void ContinueAsGuest()
        {
            if (screen == Page.Connecting) return;
            if (Debug.isDebugBuild || Application.isEditor) ServerSettings.Host = hostInput; // yazılan sunucu adresi de denenir
            Connect(Entry.Guest);
        }

        // Çıkış (ya da misafirken hesap açmak için): bağlantı kapanır, kayıtlı oturum unutulur, karşılamaya dönülür
        async void Logout()
        {
            OnlineRaceClient.ForgetAccount();
            lastEntry = Entry.Guest;
            profile = new ProfileDto();
            ClearLoginForm();
            screen = Page.Welcome;
            if (net != null) await net.CloseAsync();
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

        // Özel oyun menüsü: oda kur / kodla katıl
        void DrawPrivateMenu(float width, float height, float top)
        {
            FullViewport();

            GUI.Label(new Rect(0f, top + 30f, width, 44f), T("privateGame"), headingStyle);

            float y = top + 120f;
            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 76f), T("createRoom"), 26)) CreateRoom();
            y += 96f;
            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 64f), T("joinWithCode"), 22)) { codeNotice = ""; screen = Page.JoinCode; }
            y += 100f;
            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 50f), T("back"), 20)) screen = Page.Menu;
        }

        // Oda kodu girişi: 5 karakter, büyük harf
        void DrawJoinCode(float width, float height, float top)
        {
            FullViewport();

            GUI.Label(new Rect(0f, top + 30f, width, 44f), T("enterCode"), headingStyle);

            float y = top + 110f;
            string typed = DrawField(new Rect(width / 2f - 130f, y, 260f, 70f), joinCodeInput, 5, "codeField", codeFieldStyle);
            joinCodeInput = FilterCode(typed);
            y += 86f;

            if (codeNotice.Length > 0) GUI.Label(new Rect(0f, y, width, 26f), codeNotice, smallCenterStyle);
            y += 40f;

            if (joinCodeInput.Length == 5) { if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 64f), T("join"), 24)) JoinRoom(); }
            else UiKit.DisabledButton(new Rect(24f, y, width - 48f, 64f), T("join"), 24);
            y += 84f;

            if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 50f), T("back"), 20)) screen = Page.PrivateMenu;
        }

        static string FilterCode(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw.ToUpperInvariant())
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.ToString();
        }

        // Oda lobisi: büyük oda kodu, oyuncu listesi; kurucu için başlat düğmesi ve botlu/botsuz seçimi
        void DrawRoomLobby(float width, float height, float top)
        {
            FullViewport();

            GUI.Label(new Rect(0f, top + 16f, width, 24f), T("roomCode"), smallCenterStyle);

            var oldColor = GUI.color;
            GUI.Label(new Rect(0f, top + 38f, width, 64f), roomCode, codeStyle);

            bool copied = Time.realtimeSinceStartup < copiedUntil;
            if (UiKit.SecondaryButton(new Rect(width / 2f - 90f, top + 108f, 180f, 40f), copied ? T("copied") : T("copyCode"), 17))
            {
                GUIUtility.systemCopyBuffer = roomCode;
                copiedUntil = Time.realtimeSinceStartup + 1.5f;
            }

            // Oyuncu listesi
            float y = top + 168f;
            GUI.Label(new Rect(24f, y, width - 48f, 24f), T("players") + "  " + players.Length + " / 5", smallStyle);
            GUI.Label(new Rect(24f, y, width - 48f, 24f), T("roomLang") + ": " + (roomLang == "en" ? "English" : "Türkçe"), rowTagStyle);
            y += 30f;

            bool iAmHost = net != null && roomHost == net.UserId;
            foreach (var p in players)
            {
                var row = new Rect(20f, y, width - 40f, 44f);
                UiKit.Card(row, new Color(1f, 1f, 1f, 0.07f), 12f);

                GUI.color = OnlineRaceView.LaneColors[p.slot % OnlineRaceView.LaneColors.Length];
                GUI.DrawTexture(new Rect(row.x + 14f, row.y + 14f, 16f, 16f), SoundIcon.Disc());
                GUI.color = oldColor;

                bool me = net != null && p.id == net.UserId;
                GUI.color = me ? new Color(1f, 0.92f, 0.45f) : Color.white; // kendi adım sarı
                GUI.Label(new Rect(row.x + 42f, row.y, row.width - 120f, 44f), p.name, rowNameStyle);
                GUI.color = oldColor;
                if (p.id == roomHost) GUI.Label(new Rect(row.xMax - 100f, row.y, 90f, 44f), T("hostTag"), rowTagStyle);
                y += 52f;
            }

            // Altta: kurucu başlatır, diğerleri bekler
            float bottom = height - 28f;
            if (UiKit.SecondaryButton(new Rect(24f, bottom - 50f, width - 48f, 50f), T("leaveRoom"), 19)) LeaveRoom();
            bottom -= 62f;

            if (iAmHost)
            {
                bool canStart = players.Length >= 2 || roomBots;
                if (canStart) { if (UiKit.SecondaryButton(new Rect(24f, bottom - 66f, width - 48f, 66f), T("startRace"), 24)) net.StartRoom(roomBots); }
                else UiKit.DisabledButton(new Rect(24f, bottom - 66f, width - 48f, 66f), T("needTwo"), 20);
                bottom -= 80f;

                int choice = UiKit.Segmented(new Rect(24f, bottom - 46f, width - 48f, 46f), new[] { T("noBots"), T("withBots") }, roomBots ? 1 : 0, 18);
                if (choice >= 0) roomBots = choice == 1;
            }
            else
            {
                GUI.Label(new Rect(0f, bottom - 40f, width, 30f), T("waitHost"), smallCenterStyle);
            }
        }

        // Geliştirme: özel oda ekranlarını sahte verilerle gösterir (yalnızca DevHarness kullanır)
        public void DevShowPrivateScreen(string which)
        {
            switch (which)
            {
                case "menu": screen = Page.PrivateMenu; break;
                case "join": joinCodeInput = "K7M2P"; codeNotice = T("roomNotFound"); screen = Page.JoinCode; break;
                case "lobby":
                    roomCode = "K7M2P";
                    roomHost = net.UserId;
                    roomBots = false;
                    players = new[]
                    {
                        new PlayerDto { id = net.UserId, name = "Oyuncu4821", slot = 0, connected = true },
                        new PlayerDto { id = "u2", name = "Mehmet", slot = 1, connected = true },
                        new PlayerDto { id = "u3", name = "Ayse", slot = 2, connected = true }
                    };
                    screen = Page.RoomLobby;
                    break;
            }
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
            view.DrawLabels(scale, null, T("bot"));
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
            view.DrawLabels(scale, null, T("bot")); // yarışta kendi oyun içi adım (sarı) yazar
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
            // Geliştirme sürümü (telefon): klavye metni ve giriş küçük gri yazıyla gösterilir; takılma olursa ekran görüntüsünden teşhis için
            if (Debug.isDebugBuild && !Application.isEditor && input != null) DrawKeyboardDebug(width, top);
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
                string name = r.isBot ? T("bot") + " (" + r.name + ")" : r.name; // kendi adım sarı renkle ayrılır
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

            // Misafir: XP ve ödül yok, hesap açmaya davet
            if (myReward == null && profile.guest)
            {
                var note = new Rect(16f, y + 4f, width - 32f, 56f);
                UiKit.Card(note, new Color(1f, 1f, 1f, 0.07f), 14f);
                GUI.Label(new Rect(note.x + 12f, note.y + 4f, note.width - 24f, note.height - 8f), T("guestResultNote"), smallCenterWrapStyle());
                y = note.yMax;
            }

            // Kazanılan XP ve seviye
            if (myReward != null)
            {
                var panel = new Rect(16f, y + 4f, width - 32f, myReward.leveledUp ? 98f : 78f);
                UiKit.Card(panel, new Color(0.27f, 0.64f, 1f, 0.16f), 14f);

                var gold = new Color(1f, 0.84f, 0.32f);
                var oldXp = GUI.color;
                GUI.color = gold;
                GUI.Label(new Rect(panel.x + 12f, panel.y, 140f, panel.height), "+" + myReward.gain + " XP", rankStyle);

                // Sağ taraf: seviye (atlandıysa altın renkli duyuru), çubuğun üstünde "110 / 220 XP", çubuk
                float rx = panel.x + 150f;
                float rw = panel.width - 162f;
                float line = panel.y + 4f;
                if (myReward.leveledUp)
                {
                    GUI.color = gold;
                    GUI.Label(new Rect(rx - 40f, line, rw + 40f, 22f), T("levelUp"), rowTagStyle);
                    line += 22f;
                }
                GUI.color = myReward.leveledUp ? gold : Color.white;
                GUI.Label(new Rect(rx, line, rw, 22f), T("level") + " " + myReward.level, rowTagStyle);
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.Label(new Rect(rx, line + 22f, rw, 20f), myReward.xpIntoLevel + " / " + myReward.xpForNext + " XP", rowTagStyle);
                GUI.color = oldXp;
                DrawXpBar(new Rect(rx, panel.yMax - 22f, rw, 10f), myReward.xpIntoLevel, myReward.xpForNext);
                y = panel.yMax;
            }

            y = Mathf.Max(y + 10f, height - 148f);
            if (isPrivateRoom)
            {
                if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 62f), T("menu"), 22)) BackToMenu();
            }
            else
            {
                if (UiKit.SecondaryButton(new Rect(24f, y, width - 48f, 62f), T("playAgain"), 22)) AgainAfterResults();
                if (UiKit.SecondaryButton(new Rect(24f, y + 76f, width - 48f, 50f), T("menu"), 20)) BackToMenu();
            }
        }

        // Geliştirme: sahte sonuç göstererek ekranı doğrular (yalnızca DevHarness kullanır)
        public void DevContinueAsGuest() { ContinueAsGuest(); }
        public void DevMakeGuestProfile() { profile = new ProfileDto { guest = true }; }

        public void DevShowAccountScreen(string which)
        {
            profile = new ProfileDto { name = "Furkan", xp = 410, level = 4, xpIntoLevel = 110, xpForNext = 220, races = 9, wins = 3 };
            switch (which)
            {
                case "welcome": screen = Page.Welcome; break;
                case "login": registerMode = false; loginNotice = ""; screen = Page.Login; break;
                case "register": registerMode = true; loginNotice = ""; screen = Page.Login; break;
                case "name": nameInput = "Fur"; nameNotice = ""; screen = Page.NameEntry; break;
                case "guestsettings": profile = new ProfileDto { guest = true }; screen = Page.Settings; break;
                default: screen = Page.Settings; break;
            }
        }

        public void DevShowFakeResults(int myRank, string myStatus = "finished", bool levelUp = false, bool guestView = false)
        {
            myReward = new RewardDto { userId = net.UserId, gain = 20 + new[] { 50, 35, 25, 15, 10 }[Mathf.Clamp(myRank - 1, 0, 4)], xp = 410, level = levelUp ? 5 : 4, xpIntoLevel = levelUp ? 25 : 110, xpForNext = levelUp ? 220 : 220, leveledUp = levelUp };
            if (guestView)
            {
                myReward = null; // misafir: XP kutusu yok, hesap açma notu var
                profile = new ProfileDto { guest = true };
            }
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
