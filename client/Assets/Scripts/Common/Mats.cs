using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Common
{
    // Prototip için basit renkli malzeme önbelleği. Gerçek modellerle birlikte kaldırılacak.
    public static class Mats
    {
        // Shader.Find("Standard") derlenmiş uygulamada (telefon/PC) boş dönebilir, çünkü shader derlemeye dahil edilmez.
        // Bunun yerine Resources içindeki bir malzeme dosyası kullanılır: Unity bu dosyanın shader'ını derlemeye dahil eder.
        const string BaseMaterialPath = "Materials/Base";

        // CreatePrimitive her şekil için bir collider ekler. Android'de kullanılmayan fizik sınıfları derlemeden ayıklandığı için
        // "Can't add component ... doesn't exist" hataları yazılıyordu. Türlere burada başvurmak ayıklanmalarını önler.
        static readonly System.Type[] KeepPhysicsClasses = { typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider), typeof(MeshCollider) };

        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Material baseMaterial;

        public static Material Get(Color color, float smoothness = 0.1f)
        {
            if (cache.TryGetValue(color, out var existing) && existing != null)
                return existing;

            if (baseMaterial == null) baseMaterial = Resources.Load<Material>(BaseMaterialPath);
            if (baseMaterial == null)
                throw new System.InvalidOperationException("Assets/Resources/Materials/Base.mat bulunamadi (menu: TextRun > Create Online Game Scene bunu olusturur).");

            var mat = new Material(baseMaterial) { color = color };
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
