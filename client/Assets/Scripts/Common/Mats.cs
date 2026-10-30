using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Common
{
    // Prototip için basit renkli malzeme önbelleği. Gerçek modellerle birlikte kaldırılacak.
    public static class Mats
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        public static Material Get(Color color, float smoothness = 0.1f)
        {
            if (cache.TryGetValue(color, out var existing) && existing != null)
                return existing;

            var mat = new Material(Shader.Find("Standard")) { color = color };
            mat.SetFloat("_Glossiness", smoothness);
            cache[color] = mat;
            return mat;
        }

        public static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Get(color);
            return go;
        }
    }
}
