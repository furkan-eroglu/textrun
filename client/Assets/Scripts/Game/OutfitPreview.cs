using System.Collections.Generic;
using TextRun.Characters;
using UnityEngine;

namespace TextRun.Game
{
    // Envanter ekranÄ±ndaki karakter Ã¶nizlemesi: pistten uzakta duran bir karakter, kendi kamerasÄ±yla bir dokuya Ã§izilir.
    // ArayÃ¼z bu dokuyu bir resim gibi gÃ¶sterir. Ekran aÃ§Ä±k deÄŸilken gizli tutulur (hiÃ§ iÅŸlemci harcamaz).
    public class OutfitPreview
    {
        static readonly Vector3 Origin = new Vector3(3000f, 0f, 3000f);

        GameObject root;
        CharacterAnimator character;
        Camera cam;
        RenderTexture target;
        string key;

        public Texture Texture => target;

        public void Show(IList<string> look)
        {
            if (root == null) Create();
            root.SetActive(true);
            cam.enabled = true;

            string next = look == null ? "" : string.Join(",", look);
            if (next != key)
            {
                key = next;
                Outfits.Apply(character, look);
            }

            // Karakter yavaÅŸÃ§a saÄŸa sola dÃ¶ner; giysilerin yan gÃ¶rÃ¼nÃ¼mÃ¼ de anlaÅŸÄ±lÄ±r
            character.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.realtimeSinceStartup * 0.9f) * 32f, 0f);
        }

        public void Hide()
        {
            if (root != null && root.activeSelf)
            {
                cam.enabled = false;
                root.SetActive(false);
            }
        }

        void Create()
        {
            root = new GameObject("OutfitPreview");
            root.transform.position = Origin;

            character = CharacterBuilder.Build("PreviewCharacter", Color.clear);
            character.transform.SetParent(root.transform, false);
            var marker = character.transform.Find("LaneMarker");
            if (marker != null) Object.Destroy(marker.gameObject);

            target = new RenderTexture(512, 640, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

            var camObject = new GameObject("PreviewCamera");
            camObject.transform.SetParent(root.transform, false);
            camObject.transform.localPosition = new Vector3(0f, 1.3f, 5.2f);
            camObject.transform.localRotation = Quaternion.Euler(3f, 180f, 0f);
            cam = camObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 30f;
            cam.targetTexture = target;
            cam.depth = -20f;
        }
    }
}

