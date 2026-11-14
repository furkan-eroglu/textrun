using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TextRun.Game;
using TextRun.Online;
using TextRun.Race;
using TextRun.Typing;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextRun.EditorTools
{
    public static class OnlineTools
    {
        const string ScenePath = "Assets/Scenes/OnlineGame.unity";
        const string ApkPath = "Builds/Android/TextRun.apk";

        const string BaseMaterialPath = "Assets/Resources/Materials/Base.mat";

        // Oyun, renkli nesneleri bu malzemeden kopyalayarak üretir. Dosya Resources içinde olduğu için shader'ı derlemeye dahil edilir.
        static void EnsureBaseMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath) != null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(BaseMaterialPath));
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), BaseMaterialPath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TextRun/Create Online Game Scene")]
        public static void CreateScene()
        {
            EnsureBaseMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("OnlineGame").AddComponent<OnlineGameController>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ---------- Gerçek sunucuya karşı otomatik test ----------
        // Komut satırı: -executeMethod TextRun.EditorTools.OnlineTools.RunOnlineTest
        // Önkoşul: sunucu test ayarlarıyla çalışıyor (docs/PROTOCOL.md "Test"): 8 kelime, 3 sn lobi, yüksek WPM sınırı.
        public static void RunOnlineTest()
        {
            Task<int> task = RunOnlineTestAsync();
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!task.IsCompleted) return;
                EditorApplication.update -= poll;
                if (task.IsFaulted) Debug.LogError("[ONLINE TEST] beklenmeyen hata: " + task.Exception);
                EditorApplication.Exit(task.IsFaulted ? 1 : task.Result);
            };
            EditorApplication.update += poll;
        }

        static async Task<int> RunOnlineTestAsync()
        {
            int failures = 0;
            void Check(bool condition, string name)
            {
                if (condition) { Debug.Log("[ONLINE OK] " + name); return; }
                Debug.LogError("[ONLINE FAIL] " + name);
                failures++;
            }

            // Pump() ana iş parçacığında çalışmalı: bu yüzden bekleme döngüsü editör güncellemesine bırakılır.
            async Task WaitUntil(OnlineRaceClient client, Func<bool> condition, int timeoutMs, string what)
            {
                var started = DateTime.UtcNow;
                while (!condition())
                {
                    client.Pump();
                    if ((DateTime.UtcNow - started).TotalMilliseconds > timeoutMs) throw new TimeoutException(what + " zaman asimi");
                    await Task.Delay(20);
                }
            }

            string run = Guid.NewGuid().ToString("N").Substring(0, 8);

            // --- Test 0: kullanıcı adı + şifre hesabı: kayıt, profil, oyun içi ad, kayıtlı oturumla geri giriş, hatalı giriş ---
            string login = "u" + run;
            var acc = new OnlineRaceClient("unity-test-" + run + "-acc", false) { LoginName = login, Password = "sifre12345", Register = true };
            await acc.ConnectAsync();
            Check(acc.IsAccount && !string.IsNullOrEmpty(acc.UserId), "hesap kaydi ve girisi (kullanici adi + sifre)");
            var accProfile = await acc.GetProfileAsync();
            Check(!accProfile.guest && string.IsNullOrEmpty(accProfile.name) && accProfile.level == 1, "yeni hesap: misafir degil, oyun ici ad henuz yok");
            accProfile = await acc.SetNameAsync("Furkan Çş");
            Check(accProfile.name == "Furkan Çş", "oyun ici ad kaydedildi (Turkce harfli)");
            try { await acc.SetNameAsync("<x>"); Check(false, "gecersiz ad reddedilmeli"); }
            catch (Exception e) { Check(OnlineRaceClient.IsError(e, "name_invalid"), "gecersiz oyun ici ad reddedildi"); }
            await acc.CloseAsync();

            var saved = new OnlineRaceClient("unity-test-" + run + "-saved", false) { UseSavedAccount = true };
            await saved.ConnectAsync();
            Check(saved.IsAccount && saved.UserId == acc.UserId, "kayitli oturumla ayni hesaba geri girildi");
            Check((await saved.GetProfileAsync()).name == "Furkan Çş", "kayitli oturumda profil geldi");
            await saved.CloseAsync();
            OnlineRaceClient.ForgetAccount();
            Check(!OnlineRaceClient.HasSavedAccount, "cikista kayitli oturum unutuldu");

            try { await new OnlineRaceClient("x", false) { LoginName = login, Password = "yanlis12345" }.ConnectAsync(); Check(false, "yanlis sifre reddedilmeli"); }
            catch (Exception e) { Check(OnlineRaceClient.StatusOf(e) == 401 || OnlineRaceClient.StatusOf(e) == 404, "yanlis sifre reddedildi (durum " + OnlineRaceClient.StatusOf(e) + ")"); }
            try { await new OnlineRaceClient("x", false) { LoginName = login, Password = "baska12345", Register = true }.ConnectAsync(); Check(false, "ayni ad ikinci kez alinamaz"); }
            catch (Exception e) { Check(OnlineRaceClient.StatusOf(e) == 409 || OnlineRaceClient.StatusOf(e) == 401, "ayni kullanici adiyla ikinci kayit reddedildi (durum " + OnlineRaceClient.StatusOf(e) + ")"); }
            OnlineRaceClient.ForgetAccount();

            // --- Test 1: tek insan + botlar, tam yarış ---
            var c = new OnlineRaceClient("unity-test-" + run + "-a", false);
            SnapshotDto countdown = null;
            GoDto go = null;
            var acks = new List<AckDto>();
            ResultsDto results = null;
            EndingDto ending = null;
            long resultsAt = 0, endingAt = 0;
            c.Ending += e => { ending = e; endingAt = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond; };
            c.Countdown += s => countdown = s;
            c.Go += g => go = g;
            c.Ack += a => acks.Add(a);
            c.Results += r => { results = r; resultsAt = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond; };

            await c.ConnectAsync();
            Check(!string.IsNullOrEmpty(c.UserId), "giris yapildi, kullanici kimligi var");
            Check((await c.GetProfileAsync()).guest, "cihaz kimligiyle giren misafirdir (XP/seviye yok)");

            await c.FindAndJoinAsync(GameLanguage.Turkish);
            Check(!string.IsNullOrEmpty(c.MatchId), "Turkce odaya katilindi");

            await WaitUntil(c, () => countdown != null, 12000, "geri sayim");
            Check(countdown.lang == "tr", "oda dili Turkce");
            Check(countdown.players.Length == 5, "5 oyuncu (1 insan + 4 bot)");
            int bots = 0;
            foreach (var p in countdown.players) if (p.isBot) bots++;
            Check(bots == 4, "4 bot etiketli");
            Check(countdown.words != null && countdown.words.Length > 0, "kelime listesi geldi (" + countdown.words?.Length + ")");

            var typing = new TypingSession(countdown.words, GameLanguage.Turkish);
            await WaitUntil(c, () => go != null, 8000, "GO");
            Check(go != null, "yaris basladi (GO)");

            c.SubmitWord("kesinlikleyanlis");
            await WaitUntil(c, () => acks.Count >= 1, 5000, "yanlis kelime ACK");
            Check(!acks[0].ok && acks[0].index == 0, "yanlis kelime ilerletmedi");

            while (!typing.Finished)
            {
                int before = acks.Count;
                string word = typing.WordAt(typing.CurrentIndex);
                c.SubmitWord(word);
                typing.Submit(word, 0f);
                await WaitUntil(c, () => acks.Count > before, 5000, "ACK");
                await Task.Delay(15);
            }

            await WaitUntil(c, () => results != null, 12000, "sonuclar");
            Check(ending != null && ending.endsAt > ending.serverNow, "ilk bitirenden sonra bitis geri sayimi (ENDING) geldi");
            Check(ending != null && resultsAt - endingAt >= 2000, "sonuclar bitirir bitirmez degil, geri sayim dolunca geldi (" + (resultsAt - endingAt) + " ms)");
            Check(results.results.Length == 5, "5 kisilik sonuc tablosu");
            ResultDto mine = null;
            foreach (var r in results.results) if (r.userId == c.UserId) mine = r;
            Check(mine != null && mine.status == "finished" && mine.rank == 1, "insan oyuncu bitirdi ve birinci oldu");
            Check(mine != null && mine.errors == 1, "tek yanlis kelime sayildi");
            Check(results.results[0].userId == c.UserId, "sonuc listesi siralamali");
            Check(results.rewards == null || results.rewards.Length == 0, "misafire XP odulu verilmedi");
            await c.CloseAsync();

            // --- Test 2: dil ayrimi: Ingilizce oyuncu ayri odaya duser ---
            var tr = new OnlineRaceClient("unity-test-" + run + "-tr", false);
            var en = new OnlineRaceClient("unity-test-" + run + "-en", false);
            await tr.ConnectAsync();
            await en.ConnectAsync();
            await tr.FindAndJoinAsync(GameLanguage.Turkish);
            await en.FindAndJoinAsync(GameLanguage.English);
            Check(tr.MatchId != en.MatchId, "Turkce ve Ingilizce oyuncu farkli odada");

            SnapshotDto enSnapshot = null;
            en.Snapshot += s => enSnapshot = s;
            await WaitUntil(en, () => enSnapshot != null, 5000, "snapshot");
            Check(enSnapshot.lang == "en", "Ingilizce odanin dili en");
            await tr.CloseAsync();
            await en.CloseAsync();

            // --- Test 3: ozel oda: kur, kodla katil, yanlis kod reddi, kurucu botsuz baslatir ---
            var host = new OnlineRaceClient("unity-test-" + run + "-host", false);
            var friend = new OnlineRaceClient("unity-test-" + run + "-friend", false);
            await host.ConnectAsync();
            await friend.ConnectAsync();

            SnapshotDto hostSnapshot = null, hostCountdown = null, friendCountdown = null;
            host.Snapshot += s => hostSnapshot = s;
            host.Countdown += s => hostCountdown = s;
            friend.Countdown += s => friendCountdown = s;

            var room = await host.CreateRoomAsync(GameLanguage.Turkish);
            Check(room.code != null && room.code.Length == 5, "ozel oda kodu 5 karakter (" + room.code + ")");
            await WaitUntil(host, () => hostSnapshot != null, 5000, "kurucu snapshot");
            Check(hostSnapshot.kind == "private" && hostSnapshot.code == room.code && hostSnapshot.host == host.UserId, "oda ozel, kod dogru, kurucu kendisi");

            bool wrongCodeRejected = false;
            string wrongCodeMessage = "";
            try { await friend.JoinRoomAsync("ZZZZZ", GameLanguage.Turkish); }
            catch (Exception e) { wrongCodeMessage = e.GetType().Name + ": " + e.Message; wrongCodeRejected = OnlineRaceClient.IsError(e, "room_not_found"); }
            Check(wrongCodeRejected, "yanlis kod reddedildi (" + wrongCodeMessage + ")");

            bool wrongLanguageRejected = false;
            try { await friend.JoinRoomAsync(room.code, GameLanguage.English); }
            catch (Exception e) { wrongLanguageRejected = OnlineRaceClient.IsError(e, "language_mismatch"); }
            Check(wrongLanguageRejected, "farkli oyun dilindeki oyuncu ozel odaya giremedi");

            var joined = await friend.JoinRoomAsync(room.code.ToLowerInvariant(), GameLanguage.Turkish);
            Check(joined.matchId == room.matchId && joined.lang == "tr", "kodla (kucuk harfle de) ayni odaya girildi");

            friend.StartRoom(false); // kurucu degil: yok sayilmali
            await Task.Delay(1200);
            host.Pump();
            Check(hostCountdown == null, "kurucu olmayanin baslat istegi yok sayildi");

            host.StartRoom(false);
            await WaitUntil(host, () => hostCountdown != null, 8000, "ozel oda geri sayimi");
            await WaitUntil(friend, () => friendCountdown != null, 8000, "arkadas geri sayimi");
            Check(hostCountdown.players.Length == 2 && friendCountdown.players.Length == 2, "botsuz: yalnizca 2 insan yarisiyor");
            await host.CloseAsync();
            await friend.CloseAsync();

            // --- Test 3: kopan oyuncu sürmekte olan yarışa geri döner (yeni soket, aynı hesap) ---
            var rc = new OnlineRaceClient("unity-test-" + run + "-resume", false);
            SnapshotDto rcCountdown = null, rcBack = null;
            GoDto rcGo = null;
            rc.Countdown += s => rcCountdown = s;
            rc.Go += g => rcGo = g;
            await rc.ConnectAsync();
            await rc.FindAndJoinAsync(GameLanguage.Turkish);
            await WaitUntil(rc, () => rcGo != null, 14000, "yaris basladi (yeniden giris testi)");
            string matchBefore = rc.MatchId;
            var metaBefore = new Dictionary<string, string>(rc.MatchMeta);
            rc.Snapshot += s => rcBack = s;
            await rc.ReconnectAsync();
            await rc.ResumeMatchAsync(matchBefore, metaBefore);
            await WaitUntil(rc, () => rcBack != null, 5000, "yeniden giriste durum");
            Check(rc.MatchId == matchBefore, "ayni yarisa geri girildi");
            Check(rcBack.phase == "racing" && rcBack.words != null && rcBack.words.Length == rcCountdown.words.Length, "yaris surerken geri giren oyuncu yarisin ortasina alindi");
            bool seesSelf = false;
            foreach (var p in rcBack.players) if (p.id == rc.UserId) seesSelf = true;
            Check(seesSelf, "geri giren oyuncu yarisin oyuncu listesinde");
            await rc.CloseAsync();

            Debug.Log(failures == 0 ? "[ONLINE] TUM TESTLER GECTI" : "[ONLINE] " + failures + " TEST BASARISIZ");
            return failures == 0 ? 0 : 1;
        }

        // ---------- 3D görünüm için örnek görüntü ----------
        // Komut satırı: -executeMethod TextRun.EditorTools.OnlineTools.CapturePreview -out dosya.png
        public static void CapturePreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var view = new GameObject("View").AddComponent<OnlineRaceView>();
            view.Build();
            // Oyuncu en sol kulvarda ve önde: kamera bu durumda da oyuncuyu net göstermeli
            view.SetAllActive(new[] { "Sen", "Bot 1", "Bot 2", "Bot 3", "Bot 4" }, 0);
            view.SnapPositions(new[] { 30f, 27f, 29f, 24f, 31f }, true);

            string path = "online-preview.png";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-out") path = args[i + 1];

            // İki oran: yarış sırasındaki üst bölge (neredeyse kare) ve menü/arama/sonuç ekranındaki tam dikey ekran
            Render(view.Cam, 540, 520, path);
            Render(view.Cam, 540, 960, Path.ChangeExtension(path, null) + "-portrait.png");
            // Klavye açıkken küçük telefonda (ör. 375x667 pt) 3D alana kalan yer: yaklaşık 375x217 pt
            Render(view.Cam, 540, 312, Path.ChangeExtension(path, null) + "-small.png");

            // Bitişe yaklaşırken: kapı, FINISH yazısı ve arka tribün yakından
            view.SnapPositions(new[] { 53f, 50f, 52f, 49f, 54f }, true);
            Render(view.Cam, 540, 520, Path.ChangeExtension(path, null) + "-finish.png");

            // Bitirdikten sonra: karakter çizginin hemen ilerisinde durmalı ve kapının arkasında gizlenmemeli
            view.SnapPositions(new[] { 61.6f, 55f, 57f, 53f, 58f }, false);
            Render(view.Cam, 540, 520, Path.ChangeExtension(path, null) + "-overshoot.png");

            // Tribünlerde yakın plan: seyirci tiplerini (kilolu/zayıf/çocuk/kadın/erkek, telefon, flaş) değerlendirmek için
            var camT = view.Cam.transform;
            camT.position = new Vector3(0.5f, 2.4f, 17f);
            camT.LookAt(new Vector3(-9.5f, 3.4f, 23f));
            view.Cam.fieldOfView = 48f;
            Render(view.Cam, 540, 520, Path.ChangeExtension(path, null) + "-crowd1.png", false);

            camT.position = new Vector3(-3f, 3.2f, 52f);
            camT.LookAt(new Vector3(8f, 4.4f, 60f));
            view.Cam.fieldOfView = 55f;
            Render(view.Cam, 540, 520, Path.ChangeExtension(path, null) + "-crowd2.png", false);
            Debug.Log("Goruntu kaydedildi.");
        }

        static void Render(Camera cam, int width, int height, string path, bool frameRace = true)
        {
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;

            // Görüş açısı çizim boyutunun oranına göre hesaplanır; boyut belli olduktan sonra kamerayı yeniden konumlandır.
            var raceCamera = cam.GetComponent<RaceCamera>();
            if (frameRace) raceCamera.Frame(raceCamera.transform.position.z - raceCamera.offset.z, raceCamera.transform.position.x - raceCamera.offset.x);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        // ---------- Windows derlemesi (geliştirme: arayüzü gerçek bir derlemede doğrulamak için) ----------
        // Komut satırı: -buildTarget Win64 -executeMethod TextRun.EditorTools.OnlineTools.BuildWindows
        public static void BuildWindows()
        {
            CreateScene();
            PlayerSettings.productName = "TextRun";
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            Directory.CreateDirectory("Builds/Win");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Win/TextRun.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log("Derleme sonucu: " + summary.result + ", sure: " + summary.totalTime);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // ---------- Android derlemesi ----------
        // Komut satırı: -buildTarget Android -executeMethod TextRun.EditorTools.OnlineTools.BuildAndroid
        public static void BuildAndroid()
        {
            CreateScene();

            PlayerSettings.companyName = "girincicode";
            PlayerSettings.productName = "TextRun";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.girincicode.textrun");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Uygulama simgesi (Assets/Icon/app_icon.png, tools ile üretilir)
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning("Uygulama simgesi bulunamadi: Assets/Icon/app_icon.png");

            // Geliştirme sunucusu http/ws kullanır; canlı sürümde https/wss olacak ve bu ayar kapatılacak.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None // geliştirme derlemesi değil: dosya küçülür, oyun hızlanır
            });
            var summary = report.summary;
            Debug.Log("Derleme sonucu: " + summary.result + ", sure: " + summary.totalTime);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
