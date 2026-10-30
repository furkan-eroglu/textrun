using System;
using System.IO;
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
            Check(s.Finished, "liste bitince Finished");
            Check(s.CorrectWords == 2 && s.WrongWords == 1, "sayaclar 2 dogru 1 yanlis");

            // Doğru kelimeler: (3+1) + (4+1) = 9 karakter, 2 saniyede -> 9/5 kelime / (2/60 dk) = 54 WPM
            Check(Mathf.Abs(s.Wpm(2f) - 54f) < 0.01f, "WPM formulu");
            Check(Mathf.Abs(s.Accuracy - 0.6f) < 0.01f, "dogruluk 9 / (9 + 6)");

            // Aynı tohum aynı liste
            var a = SampleWords.Generate(GameLanguage.Turkish, 50, 7);
            var b = SampleWords.Generate(GameLanguage.Turkish, 50, 7);
            Check(string.Join(",", a) == string.Join(",", b), "ayni tohum ayni kelime dizisi");
            var en = SampleWords.Generate(GameLanguage.English, 50, 7);
            bool anyTurkishChar = false;
            foreach (var w in en)
                if (w.IndexOfAny("çğıöşü".ToCharArray()) >= 0) anyTurkishChar = true;
            Check(!anyTurkishChar, "Ingilizce listede Turkce harf yok");

            Debug.Log(failures == 0 ? "TUM TESTLER GECTI" : failures + " TEST BASARISIZ");
            EditorApplication.Exit(failures == 0 ? 0 : 1);
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
