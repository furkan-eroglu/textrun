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
    // Nakama sunucusuyla konuşur: giriş, yarış bulma, odaya katılma, kelime gönderme, gelen mesajları işleme.
    // Gelen olaylar bir kuyruğa konur ve Pump() çağrılınca ana iş parçacığında dağıtılır (Unity nesnelerine güvenle dokunulabilsin diye).
    public class OnlineRaceClient
    {
        const string SessionKey = "textrun.session";
        const string RefreshKey = "textrun.refresh";
        const string DeviceKey = "textrun.deviceId";

        readonly IClient client;
        readonly string deviceId;
        readonly bool persistSession;
        readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        ISession session;
        ISocket socket;
        string matchId;

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
                created = await client.AuthenticateDeviceAsync(deviceId, "Oyuncu" + UnityEngine.Random.Range(1000, 10000), true);
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
            socket = client.NewSocket();
            socket.ReceivedMatchState += OnMatchState;
            socket.Closed += reason => queue.Enqueue(() => Closed?.Invoke(reason));
            await socket.ConnectAsync(session, true);
        }

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
                    var match = await socket.JoinMatchAsync(found.matchId, new Dictionary<string, string> { { "lang", code } });
                    matchId = match.Id;
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
