using System;
using System.IO;
using TextRun.Audio;
using TextRun.KeyboardTest;
using TextRun.Typing;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextRun.EditorTools
{
    public static class KeyboardTestTools
    {
        const string ScenePath = "Assets/Scenes/KeyboardTest.unity";
        const string ApkPath = "Builds/Android/TextRunKeyboardTest.apk";

        [MenuItem("TextRun/Create Keyboard Test Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.07f, 0.08f, 0.12f);

            new GameObject("KeyboardTest").AddComponent<KeyboardTestScreen>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Komut satırı: -executeMethod TextRun.EditorTools.KeyboardTestTools.RunSelfTest
        public static void RunSelfTest()
        {
            int failures = 0;
            void Check(bool condition, string name)
            {
                if (condition) { Debug.Log("[TEST OK] " + name); return; }
                Debug.LogError("[TEST FAIL] " + name);
                failures++;
            }

            // Türkçe I/İ kuralları
            Check(TextNormalizer.ToLower("Iİ", GameLanguage.Turkish) == "ıi", "TR: I->ı, İ->i");
            Check(TextNormalizer.ToLower("Iİ", GameLanguage.English) != "ıi", "EN: I Turkce kuralina girmez");
            Check(TextNormalizer.ToLower("ÇĞÖŞÜ", GameLanguage.Turkish) == "çğöşü", "TR: ÇĞÖŞÜ kucuk harfe doner");

            var words = new[] { "ısı", "ilaç", "şeker" };
            var s = new TypingSession(words, GameLanguage.Turkish);

            Check(s.IsPrefixOfCurrent("ı"), "onek: ı ile baslayan kelime");
            Check(!s.IsPrefixOfCurrent("i"), "onek: i ile ı karismaz");
            Check(s.Submit("ısı", 0f), "dogru kelime kabul");
            Check(s.Submit("İLAÇ", 1f), "buyuk harfle yazilan dogru kelime kabul (otomatik buyuk harf)");
            Check(!s.Submit("seker", 2f), "ş yerine s yazmak hata");
            Check(s.CurrentIndex == 2 && !s.Finished, "yanlis kelime ilerletmez");
            Check(s.Submit("şeker", 2f), "dogru yazinca ilerler");
            Check(s.Finished, "liste bitince Finished");
            Check(s.CorrectWords == 3 && s.WrongWords == 1, "sayaclar 3 dogru 1 yanlis");

            // Doğru kelimeler: (3+1) + (4+1) + (5+1) = 15 karakter, 2 saniyede -> 15/5 kelime / (2/60 dk) = 90 WPM
            Check(Mathf.Abs(s.Wpm(2f) - 90f) < 0.01f, "WPM formulu");
            Check(Mathf.Abs(s.Accuracy - 15f / 21f) < 0.01f, "dogruluk 15 / (15 + 6)");

            s.SyncTo(1);
            Check(s.CurrentIndex == 1 && s.StateAt(0) == WordState.Correct && s.StateAt(1) == WordState.Pending, "SyncTo sunucu sirasina esitler");

            TestWordInput(Check);
            TestAudio(Check);
            TestWordDatabase(Check);

            Debug.Log(failures == 0 ? "TUM TESTLER GECTI" : failures + " TEST BASARISIZ");
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // Sentezlenen seslerin teknik doğruluğu: bozuk/sessiz mi, tavan değeri aşılıyor mu, frekans doğru mu, döngü pürüzsüz mü.
        // (Sesin kulağa nasıl geldiği ölçülemez; bunu oyuncu/geliştirici dinleyerek karar verir.)
        static void TestAudio(Action<bool, string> check)
        {
            void Common(string name, float[] data, float expectedSeconds)
            {
                float peak = 0f, sumSquares = 0f;
                bool finite = true;
                foreach (var v in data)
                {
                    if (float.IsNaN(v) || float.IsInfinity(v)) finite = false;
                    peak = Mathf.Max(peak, Mathf.Abs(v));
                    sumSquares += v * v;
                }
                float rms = Mathf.Sqrt(sumSquares / data.Length);
                float seconds = data.Length / (float)SoundSynth.SampleRate;

                check(finite, "ses " + name + ": gecerli sayilar (NaN yok)");
                check(peak <= 1.0f && peak > 0.3f, "ses " + name + ": tepe seviye 0.3-1.0 arasi (" + peak.ToString("0.00") + ")");
                check(rms > 0.02f, "ses " + name + ": sessiz degil (rms " + rms.ToString("0.000") + ")");
                check(Mathf.Abs(seconds - expectedSeconds) < 0.05f, "ses " + name + ": sure ~" + expectedSeconds + " sn (" + seconds.ToString("0.00") + ")");
            }

            // Sıfır geçiş sayısından kabaca baskın frekans
            float Frequency(float[] data)
            {
                int crossings = 0;
                for (int i = 1; i < data.Length; i++)
                    if ((data[i - 1] < 0f) != (data[i] < 0f)) crossings++;
                return crossings / 2f / (data.Length / (float)SoundSynth.SampleRate);
            }

            var tick = SoundSynth.TickSamples(660f);
            Common("tik", tick, 0.22f);
            float tickHz = Frequency(tick);
            check(tickHz > 560f && tickHz < 1500f, "ses tik: temel frekans yuksek bip araliginda (" + tickHz.ToString("0") + " Hz)");

            var click = SoundSynth.ClickSamples();
            Common("tiklama", click, 0.09f);
            check(Mathf.Abs(click[0]) < 0.05f && Mathf.Abs(click[click.Length - 1]) < 0.02f, "ses tiklama: basi/sonu sessiz (tiklama patlamasi yok)");

            var alert = SoundSynth.AlertTickSamples(false);
            Common("uyari tik", alert, 0.14f);
            var alertFinal = SoundSynth.AlertTickSamples(true);
            Common("uyari tik (son 3 sn)", alertFinal, 0.18f);
            float alertHz = Frequency(alert), finalHz = Frequency(alertFinal);
            check(finalHz > alertHz * 1.5f, "ses uyari: son 3 sn tiki daha tiz (" + alertHz.ToString("0") + " -> " + finalHz.ToString("0") + " Hz)");

            var whistle = SoundSynth.WhistleSamples();
            Common("duduk", whistle, 0.9f);
            float whistleHz = Frequency(whistle);
            check(whistleHz > 2400f && whistleHz < 3800f, "ses duduk: ~2.9 kHz civarinda (" + whistleHz.ToString("0") + " Hz)");

            var loop = SoundSynth.RaceLoopSamples();
            Common("yaris muzigi", loop, 16f * 60f / 128f);
            check(Mathf.Abs(loop[0]) < 0.02f && Mathf.Abs(loop[loop.Length - 1]) < 0.02f, "ses yaris muzigi: dongu basi/sonu sessiz (tiklama yok)");
            check(Mathf.Abs(loop[0] - loop[loop.Length - 1]) < 0.02f, "ses yaris muzigi: dongu sicramasi yok");

            var podium = SoundSynth.FanfareSamples(true);
            Common("fanfar (podyum)", podium, 2.1f);
            check(Mathf.Abs(podium[0]) < 0.02f && Mathf.Abs(podium[podium.Length - 1]) < 0.02f, "ses fanfar (podyum): basi/sonu sessiz");

            var done = SoundSynth.FanfareSamples(false);
            Common("bitti sesi", done, 1.2f);
            check(Mathf.Abs(done[0]) < 0.02f && Mathf.Abs(done[done.Length - 1]) < 0.02f, "ses bitti sesi: basi/sonu sessiz");
        }

        // Harf süzgeci: yanlış tuşlar yok sayılır, kelime doğru harfi bekler
        static void TestWordInput(Action<bool, string> check)
        {
            var input = new WordInput(GameLanguage.Turkish);
            input.Begin("kalem");

            foreach (char c in "kal") input.Press(c);
            check(input.Entered == "kal" && input.MistakesThisWord == 0, "giris: kal dogru kabul");

            foreach (char c in "asdf") check(input.Press(c) == KeyResult.Rejected, "giris: '" + c + "' yok sayildi");
            check(input.Entered == "kal", "giris: yanlis harflerden sonra kal ayni kalir (silmeye gerek yok)");
            check(input.MistakesThisWord == 4, "giris: 4 hata sayildi");

            check(input.Press(' ') == KeyResult.Ignored, "giris: kelime bitmeden bosluk yok sayilir");
            check(input.Press('E') == KeyResult.Accepted, "giris: buyuk harf de kabul");
            check(input.Press('m') == KeyResult.Accepted && input.Complete, "giris: kelime tamamlandi");
            check(input.Press('x') == KeyResult.Rejected, "giris: kelime bittikten sonra fazla harf yok sayilir");
            check(input.Press(' ') == KeyResult.Submit, "giris: tamam kelime + bosluk = gonder");

            input.Begin("ısı"); // ısı
            check(input.Press('i') == KeyResult.Rejected, "giris: Turkce i, ı yerine gecmez");
            check(input.Press('I') == KeyResult.Accepted, "giris: buyuk I, Turkcede ı olur");
            check(input.Press('S') == KeyResult.Accepted, "giris: S -> s");
            check(input.Press('I') == KeyResult.Accepted && input.Entered == "ısı", "giris: Turkce kelime tamam");

            input.Begin("ilac");
            check(input.Press('İ') == KeyResult.Accepted, "giris: İ (noktali buyuk) -> i");

            input.Begin("kalem");
            foreach (char c in "kale") input.Press(c);
            input.TrimTo(2);
            check(input.Entered == "ka", "giris: silme tusu girisi kisaltir");
            input.TrimTo(-5);
            check(input.Entered == "", "giris: silme sinirda kalir");

            var english = new WordInput(GameLanguage.English);
            english.Begin("window");
            check(english.Press('W') == KeyResult.Accepted, "giris (EN): buyuk harf kabul");
        }

        static void TestWordDatabase(Action<bool, string> check)
        {
            var config = JsonUtility.FromJson<WordConfig>(Resources.Load<TextAsset>("Words/config").text);
            const string turkishLetters = "çğıöşü";

            foreach (var language in new[] { GameLanguage.Turkish, GameLanguage.English })
            {
                string code = language.Code();
                var db = WordDatabase.Load(language);
                string allowed = null;
                foreach (var entry in config.languages) if (entry.code == code) allowed = entry.allowedChars;

                check(db.Count > 700, code + ": havuzda 700'den fazla kelime (" + db.Count + ")");

                var list = db.Generate(2000, 11);
                check(list.Count == 2000, code + ": istenen sayida kelime uretildi");
                check(string.Join(",", db.Generate(100, 7)) == string.Join(",", db.Generate(100, 7)), code + ": ayni tohum ayni dizi");
                check(string.Join(",", db.Generate(100, 7)) != string.Join(",", db.Generate(100, 8)), code + ": farkli tohum farkli dizi");

                bool badChar = false, badLength = false;
                int shortCount = 0;
                foreach (var w in list)
                {
                    foreach (char ch in w) if (allowed.IndexOf(ch) < 0) badChar = true;
                    if (w.Length < config.minLength || w.Length > config.maxLength) badLength = true;
                    if (w.Length <= 4) shortCount++;
                }
                check(!badChar, code + ": tum kelimeler izinli harflerden olusuyor");
                check(!badLength, code + ": tum kelimeler uzunluk sinirlarinda");
                check(shortCount > 400 && shortCount < 800, code + ": kisa kelime orani %20-40 arasi (" + shortCount / 20 + "%)");

                bool nearRepeat = false;
                for (int i = 0; i < list.Count && !nearRepeat; i++)
                    for (int j = Math.Max(0, i - config.recentWindow); j < i; j++)
                        if (list[i] == list[j]) { nearRepeat = true; break; }
                check(!nearRepeat, code + ": son " + config.recentWindow + " kelime icinde tekrar yok");

                bool hasTurkishLetter = false;
                foreach (var w in list) if (w.IndexOfAny(turkishLetters.ToCharArray()) >= 0) hasTurkishLetter = true;
                check(hasTurkishLetter == (language == GameLanguage.Turkish), code + ": Turkce harf sadece Turkce listede");
            }

            // Türkçe listede altı özel harfin hepsi bulunmalı
            var trList = WordDatabase.Load(GameLanguage.Turkish).Generate(3000, 3);
            foreach (char letter in turkishLetters)
            {
                bool found = false;
                foreach (var w in trList) if (w.IndexOf(letter) >= 0) { found = true; break; }
                check(found, "tr: '" + letter + "' iceren kelime var");
            }
        }

        // Komut satırı: -buildTarget Android -executeMethod TextRun.EditorTools.KeyboardTestTools.BuildAndroid
        public static void BuildAndroid()
        {
            CreateScene();

            PlayerSettings.companyName = "girincicode";
            PlayerSettings.productName = "TextRun Klavye Testi";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.girincicode.textrun");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("Derleme sonucu: " + summary.result + ", boyut: " + summary.totalSize / (1024 * 1024) + " MB, sure: " + summary.totalTime);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
