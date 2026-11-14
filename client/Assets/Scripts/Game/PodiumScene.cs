using System.Collections.Generic;
using TextRun.Characters;
using TextRun.Common;
using TextRun.Race;
using UnityEngine;

namespace TextRun.Game
{
    // Sonuç ekranındaki kürsü: ilk üç oyuncunun karakterleri (giydikleriyle) altın, gümüş ve bronz basamakların üstünde sevinir.
    // Pistten uzakta kurulur ve kendi kamerasıyla bir dokuya çizilir; arayüz bu dokuyu gösterir ve isimleri üstüne yazar.
    public class PodiumScene
    {
        static readonly Vector3 Origin = new Vector3(9000f, 0f, 9000f);

        // Sıra: 1., 2., 3. Kamera karakterlerin yüzüne baktığı için dünyadaki +X ekranda solda kalır: 2. solda, 3. sağda durur.
        static readonly float[] StepX = { 0f, 1.3f, -1.3f };
        static readonly float[] StepHeight = { 0.95f, 0.62f, 0.4f };
        static readonly Color[] StepColor = { new Color(1f, 0.80f, 0.24f), new Color(0.78f, 0.82f, 0.88f), new Color(0.82f, 0.54f, 0.30f) };

        const float ViewCenterY = 1.88f;
        const float ViewHalfHeight = 1.92f;
        public const float Aspect = 2f;

        GameObject root;
        Camera cam;
        RenderTexture target;
        readonly CharacterAnimator[] characters = new CharacterAnimator[3];
        string key;

        public Texture Texture => target;

        // looks[i]: (i+1). oyuncunun görünümü; null ise o basamak boş kalır
        public void Show(IList<string>[] looks)
        {
            if (root == null) Create();
            root.SetActive(true);
            cam.enabled = true;

            var parts = new string[3];
            for (int i = 0; i < 3; i++) parts[i] = i < looks.Length && looks[i] != null ? string.Join(",", looks[i]) : "-";
            string next = string.Join("|", parts);
            if (next == key) return;
            key = next;

            for (int i = 0; i < 3; i++)
            {
                bool present = i < looks.Length && looks[i] != null;
                characters[i].gameObject.SetActive(present);
                if (present) Outfits.Apply(characters[i], looks[i]);
            }
        }

        public void Hide()
        {
            if (root != null && root.activeSelf)
            {
                cam.enabled = false;
                root.SetActive(false);
            }
        }

        // (place+1). oyuncunun adının yazılacağı nokta: doku üstünde 0..1 (x soldan, y yukarıdan)
        public Vector2 NamePoint(int place)
        {
            float halfWidth = ViewHalfHeight * Aspect;
            float x = 0.5f - StepX[place] / (2f * halfWidth); // +X ekranda solda
            float y = 0.5f - (StepHeight[place] + 2.95f - ViewCenterY) / (2f * ViewHalfHeight);
            return new Vector2(x, y);
        }

        void Create()
        {
            root = new GameObject("PodiumScene");
            root.transform.position = Origin;

            var batch = new MeshBatch();
            for (int i = 0; i < 3; i++)
            {
                float height = StepHeight[i];
                batch.AddBox(StepColor[i], new Vector3(StepX[i], height / 2f, 0f), new Vector3(1.28f, height, 1.1f));
                batch.AddBox(Color.Lerp(StepColor[i], Color.white, 0.45f), new Vector3(StepX[i], height + 0.02f, 0f), new Vector3(1.34f, 0.05f, 1.16f));
                PixelFont.Add(batch, (i + 1).ToString(), new Color(0.16f, 0.12f, 0.05f), new Vector3(StepX[i], height * 0.5f, 0.56f), Vector3.left, Vector3.up, Mathf.Min(0.075f, height * 0.11f));

                characters[i] = CharacterBuilder.Build("Podium" + (i + 1), Color.clear);
                characters[i].transform.SetParent(root.transform, false);
                characters[i].transform.localPosition = new Vector3(StepX[i], height + 0.045f, 0f);
                var marker = characters[i].transform.Find("LaneMarker");
                if (marker != null) Object.Destroy(marker.gameObject);
                characters[i].state = CharacterState.Cheer;
                characters[i].runSpeed = 1f - i * 0.12f; // birinci en coşkulu
                characters[i].Pose(i * 1.7f);
            }
            batch.Build(root.transform, "PodiumPart");

            target = new RenderTexture(768, 384, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

            var camObject = new GameObject("PodiumCamera");
            camObject.transform.SetParent(root.transform, false);
            camObject.transform.localPosition = new Vector3(0f, ViewCenterY, 9f);
            camObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ViewHalfHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 30f;
            cam.targetTexture = target;
            cam.depth = -21f;
        }
    }
}
