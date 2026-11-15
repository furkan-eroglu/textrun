using System.Collections.Generic;
using TextRun.Characters;
using UnityEngine;

namespace TextRun.Game
{
    // Eşya resimleri: her eşya çıplak bir mankene giydirilip ilgili bölgeye (kafa, gövde, bacak, ayak, sırt) yakından bakan
    // bir kamerayla küçük bir dokuya bir kez çizilir ve saklanır. Mağaza ve envanter bu resimleri gösterir.
    public static class ItemIcons
    {
        const int Size = 192;
        const int PerFrame = 3; // bir karede en fazla bu kadar resim üretilir (takılma olmasın)
        static readonly Vector3 Origin = new Vector3(7000f, 0f, 7000f);

        static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();
        static readonly Queue<string> pending = new Queue<string>();
        static readonly HashSet<string> queued = new HashSet<string>();

        static GameObject root;
        static CharacterAnimator mannequin;
        static Camera cam;

        // Resim hazırsa döner; değilse sıraya alır ve null döner (birkaç kare sonra hazır olur)
        public static Texture Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var texture) && texture != null && texture.IsCreated()) return texture;
            if (queued.Add(id)) pending.Enqueue(id);
            return null;
        }

        // Her kare çağrılır (OnGUI dışında): sıradaki resimleri üretir
        public static void Tick()
        {
            if (pending.Count == 0)
            {
                if (root != null && root.activeSelf) root.SetActive(false);
                return;
            }

            if (root == null) Create();
            root.SetActive(true);
            for (int i = 0; i < PerFrame && pending.Count > 0; i++)
            {
                string id = pending.Dequeue();
                queued.Remove(id);
                cache[id] = Render(id);
            }
        }

        static void Create()
        {
            root = new GameObject("ItemIconStudio");
            root.transform.position = Origin;

            mannequin = CharacterBuilder.Build("IconMannequin", Color.clear);
            mannequin.transform.SetParent(root.transform, false);
            var marker = mannequin.transform.Find("LaneMarker");
            if (marker != null) Object.Destroy(marker.gameObject);
            mannequin.enabled = false; // nefes alma hareketi resimleri oynatmasın
            mannequin.Pose(0f);

            var camObject = new GameObject("IconCamera");
            camObject.transform.SetParent(root.transform, false);
            cam = camObject.AddComponent<Camera>();
            cam.enabled = false; // yalnızca elle çizilir
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 20f;
        }

        static RenderTexture Render(string id)
        {
            // Kategoriye göre bakış: merkez yüksekliği, görüş yarı yüksekliği, yandan açı, yukarıdan açı
            float centerY, halfSize, yaw, pitch = 6f;
            switch (Outfits.CategoryOf(id))
            {
                case "head": centerY = 2.0f; halfSize = 0.74f; yaw = 24f; break;
                case "top": centerY = 1.12f; halfSize = 0.6f; yaw = 24f; break;
                case "bottom": centerY = 0.44f; halfSize = 0.58f; yaw = 24f; break;
                case "shoes": centerY = 0.12f; halfSize = 0.36f; yaw = 42f; pitch = 16f; break;
                case "cape": centerY = 1.02f; halfSize = 0.66f; yaw = 156f; break;
                default: centerY = 1.1f; halfSize = 1.3f; yaw = 24f; break;
            }

            Outfits.Apply(mannequin, new[] { id });

            var rotation = Quaternion.Euler(pitch, yaw + 180f, 0f);
            var center = new Vector3(0f, centerY, 0f);
            cam.transform.localRotation = rotation;
            cam.transform.localPosition = center - rotation * Vector3.forward * 8f;
            cam.orthographicSize = halfSize;

            var target = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            cam.targetTexture = target;
            cam.Render();
            cam.targetTexture = null;
            return target;
        }
    }
}
