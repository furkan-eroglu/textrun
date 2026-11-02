using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Nakama;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.Online
{
    // Başlamış (geri sayım ya da yarış aşamasındaki) yarışın bilgisi telefona yazılır. Oyun kapanır, bağlantı kopar ya da
    // telefon arka plana atılırsa oyuncu yarış sürüyorsa aynı yarışa geri döner; yarış bittiyse ana menüye düşer.
    // Misafirin gizli kimliği de yalnızca bu süre boyunca saklanır (yarış bitince silinir).
    public static class ResumeStore
    {
        const string Key = "textrun.resume";
        const long MaxAgeSeconds = 3600; // bir yarış bundan uzun sürmez; eski kayıt yok sayılır

        [Serializable]
        public class Info
        {
            public string matchId;
            public string lang;
            public string code;     // özel oda kodu (herkese açık yarışta boş)
            public string userId;
            public string guestId;  // yalnızca misafirde: aynı geçici hesaba dönebilmek için cihaz kimliği
            public long savedAt;    // unix saniye
        }

        public static Info Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (string.IsNullOrEmpty(json)) return null;
                var info = JsonUtility.FromJson<Info>(json);
                if (info == null || string.IsNullOrEmpty(info.matchId)) return null;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - info.savedAt > MaxAgeSeconds) { Clear(); return null; }
                return info;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(Info info)
        {
            info.savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(info));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    // Nakama sunucusuyla konuşur: giriş, yarış bulma, odaya katılma, kelime gönderme, gelen mesajları işleme.
    // Gelen olaylar bir kuyruğa konur ve Pump() çağrılınca ana iş parçacığında dağıtılır (Unity nesnelerine güvenle dokunulabilsin diye).
    public class OnlineRaceClient
    {
        const string SessionKey = "textrun.session";
        const string RefreshKey = "textrun.refresh";
        const string DeviceKey = "textrun.deviceId";
        const string AccountKey = "textrun.account"; // 1 = bu telefonda kalıcı hesap oturumu kayıtlı

        readonly IClient client;
        readonly string deviceId;
        readonly bool persistSession;
        readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        ISession session;
        ISocket socket;
        string matchId;
        Dictionary<string, string> matchMeta; // odaya girerken gönderilen bilgi (dil, özel oda kodu): yeniden girişte aynısı gerekir

        public event Action<SnapshotDto> Snapshot;
        public event Action<SnapshotDto> Roster;
        public event Action<SnapshotDto> Countdown;
        public event Action<GoDto> Go;
        public event Action<ProgressDto> Progress;
        public event Action<AckDto> Ack;
        public event Action<ResultsDto> Results;
        public event Action<EndingDto> Ending;
        public event Action<string> Closed;

        public string UserId => session?.UserId;
        public string Username => session?.Username;
        public string MatchId => matchId;
        public string DeviceId => deviceId;
        public bool IsConnected => socket != null && socket.IsConnected;

        // Giriş türü (ConnectAsync'ten önce ayarlanır):
        //   hiçbiri            -> misafir (cihaz kimliği; GuestName ile ad)
        //   LoginName+Password -> kullanıcı adı + şifre (Register: yeni hesap aç)
        //   UseSavedAccount    -> bu telefonda kayıtlı hesap oturumu
        public string GuestName;
        public string LoginName;
        public string Password;
        public bool Register;
        public bool UseSavedAccount;
        public bool IsAccount { get; private set; }

        public static bool HasSavedAccount => PlayerPrefs.GetInt(AccountKey, 0) == 1 && !string.IsNullOrEmpty(PlayerPrefs.GetString(RefreshKey, ""));

        // Çıkış: kayıtlı hesap oturumu unutulur
        public static void ForgetAccount()
        {
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.SetInt(AccountKey, 0);
            PlayerPrefs.Save();
        }

        // Sunucuda giriş adı e-posta biçimine çevrilir (gerçek e-posta istenmez)
        static string EmailOf(string loginName) => loginName.Trim().ToLowerInvariant() + "@textrun.local";

        // Sunucunun HTTP durumu (ör. 401 yanlış şifre, 404 hesap yok, 409 ad alınmış); sunucu cevabı değilse 0
        public static int StatusOf(Exception e)
        {
            for (var x = e; x != null; x = x.InnerException)
                if (x is ApiResponseException api) return (int)api.StatusCode;
            return 0;
        }

        // deviceId verilmezse bu cihaza ait kalıcı bir kimlik kullanılır. Testlerde her istemciye ayrı kimlik verilir.
        public OnlineRaceClient(string deviceId = null, bool persistSession = true)
        {
            this.persistSession = persistSession;
            this.deviceId = deviceId ?? LoadOrCreateDeviceId();
            client = new Client(ServerSettings.Scheme, ServerSettings.Host, ServerSettings.Port, ServerSettings.ServerKey);
        }

        static string LoadOrCreateDeviceId()
        {
            var id = PlayerPrefs.GetString(DeviceKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(DeviceKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        public async Task ConnectAsync()
        {
            if (LoginName != null)
            {
                session = await client.AuthenticateEmailAsync(EmailOf(LoginName), Password, LoginName.Trim().ToLowerInvariant(), Register);
                IsAccount = true;
                SaveAccountSession();
                await OpenSocketAsync();
                return;
            }
            if (UseSavedAccount)
            {
                session = await RestoreAccountAsync();
                IsAccount = true;
                await OpenSocketAsync();
                return;
            }

            session = persistSession ? RestoreSession() : null;

            if (session != null)
            {
                try
                {
                    await OpenSocketAsync();
                    return;
                }
                catch (Exception)
                {
                    // Kayıtlı oturum artık geçerli değil (ör. sunucu sıfırlandı). Yeniden giriş yapılır.
                    session = null;
                }
            }

            session = await AuthenticateAsync();
            await OpenSocketAsync();
        }

        void SaveAccountSession()
        {
            PlayerPrefs.SetString(SessionKey, session.AuthToken);
            PlayerPrefs.SetString(RefreshKey, session.RefreshToken ?? "");
            PlayerPrefs.SetInt(AccountKey, 1);
            PlayerPrefs.Save();
        }

        // Kayıtlı oturum süresi dolmak üzereyse yenileme anahtarıyla uzatılır (geçersizse sunucu hata döner)
        async Task<ISession> RestoreAccountAsync()
        {
            var restored = Session.Restore(PlayerPrefs.GetString(SessionKey, ""), PlayerPrefs.GetString(RefreshKey, ""));
            if (restored == null) throw new Exception("kayitli oturum yok");
            if (restored.HasExpired(DateTime.UtcNow.AddHours(1)))
            {
                restored = await client.SessionRefreshAsync(restored);
                session = restored;
                SaveAccountSession();
            }
            return restored;
        }

        ISession RestoreSession()
        {
            var token = PlayerPrefs.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(token)) return null;

            var restored = Session.Restore(token, PlayerPrefs.GetString(RefreshKey, ""));
            return restored == null || restored.HasExpired(DateTime.UtcNow.AddHours(1)) ? null : restored;
        }

        async Task<ISession> AuthenticateAsync()
        {
            ISession created;
            try
            {
                created = await client.AuthenticateDeviceAsync(deviceId, GuestName ?? "Oyuncu" + UnityEngine.Random.Range(1000, 10000), true);
            }
            catch (Exception)
            {
                // Rastgele kullanıcı adı başkasında olabilir; adsız dene, sunucu bir ad üretir.
                created = await client.AuthenticateDeviceAsync(deviceId, null, true);
            }

            if (persistSession)
            {
                PlayerPrefs.SetString(SessionKey, created.AuthToken);
                PlayerPrefs.SetString(RefreshKey, created.RefreshToken ?? "");
                PlayerPrefs.Save();
            }
            return created;
        }

        async Task OpenSocketAsync()
        {
            var created = client.NewSocket();
            // Yalnızca şu anki soketin olayları dikkate alınır (yeniden bağlanınca eski soketin geç gelen olayları karışmasın)
            created.ReceivedMatchState += state => { if (created == socket) OnMatchState(state); };
            created.Closed += reason => queue.Enqueue(() => { if (created == socket) Closed?.Invoke(reason); });
            socket = created;
            await created.ConnectAsync(session, true);
        }

        // Bağlantı koptuktan sonra aynı hesapla yeni soket açar (süresi dolan oturum yenilenir). Odaya girmez: ResumeMatchAsync çağrılır.
        public async Task ReconnectAsync()
        {
            var old = socket;
            if (session.HasExpired(DateTime.UtcNow.AddMinutes(1)))
            {
                if (IsAccount)
                {
                    session = await client.SessionRefreshAsync(session);
                    SaveAccountSession();
                }
                else session = await AuthenticateAsync(); // misafir: aynı cihaz kimliği aynı geçici hesabı verir
            }
            await OpenSocketAsync();
            if (old != null) { try { await old.CloseAsync(); } catch (Exception) { } }
        }

        // Daha önce girilmiş yarışa yeniden girer (sunucu oyuncuyu kaldığı yerden devam ettirir). Yarış yoksa/kapalıysa istisna fırlatır.
        public async Task ResumeMatchAsync(string id, Dictionary<string, string> meta)
        {
            var match = await socket.JoinMatchAsync(id, meta);
            matchId = match.Id;
            matchMeta = meta;
        }

        public Dictionary<string, string> MatchMeta => matchMeta;

        // Yarış kaydı unutulur (sunucuya "ayrıldım" denmez: bağlantı zaten yoktur)
        public void ForgetMatch() { matchId = null; matchMeta = null; }

        // Seçilen dilde açık bir yarış bulur ve odaya katılır. Oda dolu/kapalıysa birkaç kez yeniden arar.
        public async Task FindAndJoinAsync(GameLanguage language, Action<string> onRetry = null)
        {
            string code = language.Code();
            Exception last = null;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                var rpc = await client.RpcAsync(session, "find_race", JsonUtility.ToJson(new FindRaceRequest { lang = code }));
                var found = JsonUtility.FromJson<FindRaceResponse>(rpc.Payload);

                try
                {
                    var meta = new Dictionary<string, string> { { "lang", code } };
                    var match = await socket.JoinMatchAsync(found.matchId, meta);
                    matchId = match.Id;
                    matchMeta = meta;
                    return;
                }
                catch (Exception e)
                {
                    last = e;
                    if (e.Message.Contains("language_mismatch")) throw;
                    onRetry?.Invoke(e.Message);
                    await Task.Delay(200);
                }
            }
            throw last ?? new Exception("yarış bulunamadı");
        }

        // Özel oda kurar ve içine girer. Dönen kodu arkadaşlara söyleyip kodla katılmaları sağlanır.
        public async Task<RoomResponse> CreateRoomAsync(GameLanguage language)
        {
            var rpc = await client.RpcAsync(session, "create_room", JsonUtility.ToJson(new RoomRequest { lang = language.Code() }));
            var room = JsonUtility.FromJson<RoomResponse>(rpc.Payload);
            await JoinPrivateAsync(room);
            return room;
        }

        // Kodla özel odaya katılır. Hata iletileri: room_not_found, room_closed, room_full, language_mismatch
        // (oyuncunun oyun dili oda kurucusununkinden farklıysa).
        public async Task<RoomResponse> JoinRoomAsync(string code, GameLanguage language)
        {
            var rpc = await client.RpcAsync(session, "join_room", JsonUtility.ToJson(new RoomRequest { code = code, lang = language.Code() }));
            var room = JsonUtility.FromJson<RoomResponse>(rpc.Payload);
            await JoinPrivateAsync(room);
            return room;
        }

        async Task JoinPrivateAsync(RoomResponse room)
        {
            var meta = new Dictionary<string, string> { { "lang", room.lang }, { "code", room.code } };
            var match = await socket.JoinMatchAsync(room.matchId, meta);
            matchId = match.Id;
            matchMeta = meta;
        }

        // Oyuncu profili (ad, XP, seviye) sunucudan okunur
        public async Task<ProfileDto> GetProfileAsync()
        {
            var rpc = await client.RpcAsync(session, "get_profile", "{}");
            return JsonUtility.FromJson<ProfileDto>(rpc.Payload);
        }

        // Sahip olunan eşyalar
        public async Task<InventoryDto> GetInventoryAsync()
        {
            var rpc = await client.RpcAsync(session, "get_inventory", "{}");
            return JsonUtility.FromJson<InventoryDto>(rpc.Payload);
        }

        // Bir eşyayı takar (itemId) ya da bir kategoriyi boşaltır (category); güncel envanteri döner.
        public async Task<InventoryDto> EquipAsync(string itemId, string category)
        {
            var request = string.IsNullOrEmpty(itemId) ? "{\"category\":\"" + category + "\"}" : "{\"itemId\":\"" + itemId + "\"}";
            var rpc = await client.RpcAsync(session, "equip_item", request);
            return JsonUtility.FromJson<InventoryDto>(rpc.Payload);
        }

        // Bir sandık açar (tier: bronze | silver | gold). Sandık yoksa istisna: IsError(e, "no_chest")
        public async Task<ChestResultDto> OpenChestAsync(string tier)
        {
            var rpc = await client.RpcAsync(session, "open_chest", JsonUtility.ToJson(new ChestRequest { tier = tier }));
            return JsonUtility.FromJson<ChestResultDto>(rpc.Payload);
        }

        // Oyuncu adını değiştirir. Geçersizse istisna fırlatır: IsError(e, "name_invalid")
        public async Task<ProfileDto> SetNameAsync(string name)
        {
            var rpc = await client.RpcAsync(session, "set_name", JsonUtility.ToJson(new NameRequest { name = name }));
            return JsonUtility.FromJson<ProfileDto>(rpc.Payload);
        }

        // Sunucunun fırlattığı hata metni (ör. room_not_found) istisnanın ya da iç istisnalarının metninde aranır.
        public static bool IsError(Exception e, string code)
        {
            for (var x = e; x != null; x = x.InnerException)
                if ((x.Message ?? "").Contains(code) || x.ToString().Contains(code)) return true;
            return false;
        }

        // Yalnızca oda kurucusu: yarışı başlatır (bots: boş yerleri botla doldur)
        public void StartRoom(bool bots)
        {
            if (socket == null || matchId == null) return;
            _ = SendAsync(Op.Start, JsonUtility.ToJson(new StartDto { bots = bots }));
        }

        public void SubmitWord(string word, int mistakes = 0)
        {
            if (socket == null || matchId == null || string.IsNullOrEmpty(word)) return;
            _ = SendAsync(Op.Submit, JsonUtility.ToJson(new SubmitDto { w = word, e = mistakes }));
        }

        async Task SendAsync(long opCode, string json)
        {
            try
            {
                await socket.SendMatchStateAsync(matchId, opCode, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Mesaj gonderilemedi: " + e.Message);
            }
        }

        public async Task LeaveAsync()
        {
            if (socket == null || matchId == null) return;
            var id = matchId;
            matchId = null;
            matchMeta = null;
            try { await socket.LeaveMatchAsync(id); } catch (Exception) { }
        }

        public async Task CloseAsync()
        {
            matchId = null;
            if (socket == null) return;
            try { await socket.CloseAsync(); } catch (Exception) { }
        }

        void OnMatchState(IMatchState state)
        {
            string json = Encoding.UTF8.GetString(state.State ?? new byte[0]);
            long op = state.OpCode;

            queue.Enqueue(() =>
            {
                switch (op)
                {
                    case Op.Snapshot: Snapshot?.Invoke(JsonUtility.FromJson<SnapshotDto>(json)); break;
                    case Op.Roster: Roster?.Invoke(JsonUtility.FromJson<SnapshotDto>(json)); break;
                    case Op.Countdown: Countdown?.Invoke(JsonUtility.FromJson<SnapshotDto>(json)); break;
                    case Op.Go: Go?.Invoke(JsonUtility.FromJson<GoDto>(json)); break;
                    case Op.Progress: Progress?.Invoke(JsonUtility.FromJson<ProgressDto>(json)); break;
                    case Op.Ack: Ack?.Invoke(JsonUtility.FromJson<AckDto>(json)); break;
                    case Op.Results: Results?.Invoke(JsonUtility.FromJson<ResultsDto>(json)); break;
                    case Op.Ending: Ending?.Invoke(JsonUtility.FromJson<EndingDto>(json)); break;
                }
            });
        }

        // Her karede çağrılır: gelen mesajları ana iş parçacığında olaylara çevirir.
        public void Pump()
        {
            while (queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
