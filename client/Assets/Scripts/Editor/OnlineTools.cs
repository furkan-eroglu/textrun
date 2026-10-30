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

        [MenuItem("TextRun/Create Online Game Scene")]
        public static void CreateScene()
        {
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

            // --- Test 1: tek insan + botlar, tam yarış ---
            var c = new OnlineRaceClient("unity-test-" + run + "-a", false);
            SnapshotDto countdown = null;
            GoDto go = null;
            var acks = new List<AckDto>();
            ResultsDto results = null;
            c.Countdown += s => countdown = s;
            c.Go += g => go = g;
            c.Ack += a => acks.Add(a);
            c.Results += r => results = r;

            await c.ConnectAsync();
            Check(!string.IsNullOrEmpty(c.UserId), "giris yapildi, kullanici kimligi var");

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
            Check(results.results.Length == 5, "5 kisilik sonuc tablosu");
            ResultDto mine = null;
            foreach (var r in results.results) if (r.userId == c.UserId) mine = r;
            Check(mine != null && mine.status == "finished" && mine.rank == 1, "insan oyuncu bitirdi ve birinci oldu");
            Check(mine != null && mine.errors == 1, "tek yanlis kelime sayildi");
            Check(results.results[0].userId == c.UserId, "sonuc listesi siralamali");
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
            Debug.Log("Goruntu kaydedildi.");
        }

        static void Render(Camera cam, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;

            // Görüş açısı çizim boyutunun oranına göre hesaplanır; boyut belli olduktan sonra kamerayı yeniden konumlandır.
            var raceCamera = cam.GetComponent<RaceCamera>();
            raceCamera.Frame(raceCamera.transform.position.z - raceCamera.offset.z, raceCamera.transform.position.x - raceCamera.offset.x);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
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
            // Geliştirme sunucusu http/ws kullanır; canlı sürümde https/wss olacak ve bu ayar kapatılacak.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            var summary = report.summary;
            Debug.Log("Derleme sonucu: " + summary.result + ", sure: " + summary.totalTime);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
