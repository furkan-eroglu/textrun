using System.Collections.Generic;
using TextRun.Characters;
using UnityEngine;

namespace TextRun.Game
{
    // Geliştirme aracı: birkaç eşyayı ayrı ayrı karakterlere giydirip ızgara halinde gösterir (DevHarness ekran görüntüsü için).
    public class OutfitGallery
    {
        GameObject root;

        public void Show(IList<string> ids)
        {
            Clear();
            root = new GameObject("OutfitGallery");
            root.transform.position = new Vector3(5000f, 0f, 5000f);

            for (int i = 0; i < ids.Count; i++)
            {
                var character = CharacterBuilder.Build("G" + i, Color.clear);
                character.transform.SetParent(root.transform, false);
                var marker = character.transform.Find("LaneMarker");
                if (marker != null) Object.Destroy(marker.gameObject);
                character.transform.localPosition = new Vector3((i % 3 - 1) * 1.35f, -(i / 3) * 2.8f, 0f);
                character.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                Outfits.Apply(character, new[] { ids[i] });
            }

            var camObject = new GameObject("GalleryCamera");
            camObject.transform.SetParent(root.transform, false);
            camObject.transform.localPosition = new Vector3(0f, -2.4f, 10f);
            camObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.24f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 30f;
            cam.depth = 50f;
        }

        public void Clear()
        {
            if (root != null) Object.Destroy(root);
            root = null;
        }
    }
}
