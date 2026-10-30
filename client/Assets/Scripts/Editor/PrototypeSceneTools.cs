using System.IO;
using TextRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextRun.EditorTools
{
    public static class PrototypeSceneTools
    {
        const string ScenePath = "Assets/Scenes/RacePrototype.unity";

        [MenuItem("TextRun/Create Race Prototype Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("RacePrototype");
            go.AddComponent<RacePrototypeController>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Sahne olusturuldu: " + ScenePath);
        }

        // Komut satırı: -executeMethod TextRun.EditorTools.PrototypeSceneTools.CreateAndCapture -out dosya.png
        public static void CreateAndCapture()
        {
            CreateScene();

            var controller = Object.FindFirstObjectByType<RacePrototypeController>();
            controller.Build();
            controller.PreviewPose(20f);

            var cam = Camera.main;
            var target = new RenderTexture(900, 700, 24);
            cam.targetTexture = target;
            cam.Render();

            RenderTexture.active = target;
            var tex = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(GetArg("-out") ?? "race.png", tex.EncodeToPNG());
            Debug.Log("Goruntu kaydedildi.");
        }

        static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
