using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextRun.Race
{
    // Çok sayıda küpü renge göre gruplayıp renk başına tek mesh'e birleştirir. Statik dekor için az çizim çağrısı demektir.
    public class MeshBatch
    {
        readonly Dictionary<Color, List<CombineInstance>> items = new Dictionary<Color, List<CombineInstance>>();
        readonly Mesh box = MeshFactory.Box();

        public void AddBox(Color color, Vector3 center, Vector3 size)
        {
            if (!items.TryGetValue(color, out var list))
            {
                list = new List<CombineInstance>();
                items[color] = list;
            }
            list.Add(new CombineInstance { mesh = box, transform = Matrix4x4.TRS(center, Quaternion.identity, size) });
        }

        public void Build(Transform parent, string namePrefix)
        {
            int index = 0;
            foreach (var pair in items)
            {
                var mesh = new Mesh { name = namePrefix + index };
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);

                var go = new GameObject(namePrefix + index);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Mats.Get(pair.Key);
                renderer.shadowCastingMode = ShadowCastingMode.Off; // arena parçaları pistin üstüne gölge düşürmesin
                index++;
            }
        }
    }
}
