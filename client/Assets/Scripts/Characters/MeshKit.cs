using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TextRun.Characters
{
    // Karakter ve eşya modelleri için ağ (mesh) üreticileri. Hazır küp/küre yerine gerçek biçimler kurar:
    // dönel yüzey (şapka, gövde, kol, bacak), yuvarlak köşeli kutu, kalınlık verilmiş kesit (vizör, yıldız, kanat tüyü)
    // ve kıvrımlı kumaş (pelerin). Aynı ölçülerle istenen ağ bir kez üretilir ve saklanır.
    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        static string Key(string kind, float[] values, params float[] extra)
        {
            var sb = new StringBuilder(kind);
            foreach (float v in values) sb.Append('|').Append(v.ToString("0.###"));
            foreach (float v in extra) sb.Append('#').Append(v.ToString("0.###"));
            return sb.ToString();
        }

        static Mesh Finish(string key, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = key.Length > 40 ? key.Substring(0, 40) : key, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        // ---------- Dönel yüzey ----------

        // Kesit çizgisini (yarıçap, yükseklik çiftleri) dikey eksen etrafında döndürür. Kesit kapalı bir halka da olabilir
        // (ör. şapka kenarı: alttan dışa, üstten içe). Yüzeyin dışa bakması için yön kendiliğinden düzeltilir.
        // fromDegrees/sweepDegrees: tam tur yerine yalnızca bir yay döndürülür (0 derece +X, 90 derece +Z yani yüz tarafı).
        public static Mesh Lathe(float[] profile, int segments = 24, float fromDegrees = 0f, float sweepDegrees = 360f)
        {
            string key = Key("lathe", profile, segments, fromDegrees, sweepDegrees);
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int points = profile.Length / 2;
            // İşaretli alan: saat yönünün tersine çizilmiş kesit dışa bakar; değilse üçgenler ters çevrilir
            // (Açık kesitler eksene kapatılarak ölçülür: aşağıdan yukarı çizilen dış yüzey her zaman dışa bakar.)
            var loop = new List<Vector2>();
            for (int i = 0; i < points; i++) loop.Add(new Vector2(profile[i * 2], profile[i * 2 + 1]));
            loop.Add(new Vector2(0f, profile[points * 2 - 1]));
            loop.Add(new Vector2(0f, profile[1]));
            float area = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                var p = loop[i]; var q = loop[(i + 1) % loop.Count];
                area += p.x * q.y - q.x * p.y;
            }
            bool flip = area < 0f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int ring = segments + 1;
            for (int i = 0; i < points; i++)
            {
                float radius = profile[i * 2], y = profile[i * 2 + 1];
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (fromDegrees + s * sweepDegrees / segments) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
                }
            }
            for (int i = 0; i < points - 1; i++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = i * ring + s, b = (i + 1) * ring + s, c = i * ring + s + 1, d = (i + 1) * ring + s + 1;
                    if (flip) { triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(c); triangles.Add(d); triangles.Add(b); }
                    else { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(c); triangles.Add(b); triangles.Add(d); }
                }
            }

            var mesh = Finish(key, vertices, triangles);
            // Dikiş: halkanın ilk ve son köşesi aynı yerdedir; normalleri eşitlenir ki çizgi görünmesin
            if (sweepDegrees < 359.9f) return mesh; // yay: uçlar birleşmez
            var normals = mesh.normals;
            for (int i = 0; i < points; i++)
            {
                var joined = (normals[i * ring] + normals[i * ring + segments]).normalized;
                normals[i * ring] = joined;
                normals[i * ring + segments] = joined;
            }
            mesh.normals = normals;
            return mesh;
        }

        // ---------- Yuvarlak köşeli kutu ----------

        public static Mesh RoundedBox(float width, float height, float depth, float radius, int steps = 6)
        {
            string key = Key("box", new[] { width, height, depth, radius }, steps);
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var half = new Vector3(width, height, depth) / 2f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            var inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Altı yüz: her yüz ızgara halinde kurulur, köşeler iç kutudan "radius" kadar dışarı itilerek yuvarlanır
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int start = vertices.Count;
                    for (int i = 0; i <= steps; i++)
                    {
                        for (int j = 0; j <= steps; j++)
                        {
                            float u = EdgeBiased(i / (float)steps), v = EdgeBiased(j / (float)steps);
                            var p = Vector3.zero;
                            p[axis] = sign * half[axis];
                            p[(axis + 1) % 3] = Mathf.Lerp(-half[(axis + 1) % 3], half[(axis + 1) % 3], u);
                            p[(axis + 2) % 3] = Mathf.Lerp(-half[(axis + 2) % 3], half[(axis + 2) % 3], v);
                            var core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var push = p - core;
                            vertices.Add(push.sqrMagnitude > 1e-10f ? core + push.normalized * radius : p);
                        }
                    }
                    for (int i = 0; i < steps; i++)
                    {
                        for (int j = 0; j < steps; j++)
                        {
                            int a = start + i * (steps + 1) + j, b = a + steps + 1, c = a + 1, d = b + 1;
                            AddOutward(vertices, triangles, a, b, c);
                            AddOutward(vertices, triangles, c, b, d);
                        }
                    }
                }
            }
            return Finish(key, vertices, triangles);
        }

        // Izgara çizgilerini kenarlara yaklaştırır: yuvarlak köşelere daha çok üçgen düşer
        static float EdgeBiased(float t) => 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);

        // Üçgeni, yüzü merkezden dışarı bakacak yönde ekler (dışbükey biçimler için)
        static void AddOutward(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude < 1e-14f) return;
            bool outward = Vector3.Dot(normal, vertices[a] + vertices[b] + vertices[c]) >= 0f;
            triangles.Add(a);
            triangles.Add(outward ? b : c);
            triangles.Add(outward ? c : b);
        }

        // ---------- Kalınlık verilmiş kesit ----------

        // XY düzlemindeki kapalı çizgiye (x, y çiftleri) Z yönünde kalınlık verir. Çizgi, ağırlık merkezinden bakınca
        // her köşesi görünen bir biçim olmalıdır (yıldız, yaprak, yarım daire, üçgen...). Kenarlar keskin kalır.
        public static Mesh Prism(float[] outline, float depth)
        {
            string key = Key("prism", outline, depth);
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int n = outline.Length / 2;
            var points = new Vector2[n];
            var center = Vector2.zero;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                points[i] = new Vector2(outline[i * 2], outline[i * 2 + 1]);
                center += points[i] / n;
            }
            for (int i = 0; i < n; i++)
            {
                var p = points[i]; var q = points[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }
            if (area < 0f) Array.Reverse(points);

            float z = depth / 2f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector2 p = points[i], q = points[(i + 1) % n];
                // Ön yüz (+Z), arka yüz (-Z)
                int f = vertices.Count;
                vertices.Add(new Vector3(center.x, center.y, z)); vertices.Add(new Vector3(p.x, p.y, z)); vertices.Add(new Vector3(q.x, q.y, z));
                triangles.Add(f); triangles.Add(f + 1); triangles.Add(f + 2);
                int k = vertices.Count;
                vertices.Add(new Vector3(center.x, center.y, -z)); vertices.Add(new Vector3(q.x, q.y, -z)); vertices.Add(new Vector3(p.x, p.y, -z));
                triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 2);
                // Yan yüz
                int s = vertices.Count;
                vertices.Add(new Vector3(p.x, p.y, z)); vertices.Add(new Vector3(p.x, p.y, -z)); vertices.Add(new Vector3(q.x, q.y, -z)); vertices.Add(new Vector3(q.x, q.y, z));
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }
            return Finish(key, vertices, triangles);
        }

        // ---------- Kumaş ----------

        // İki yüzü de görünen ince yüzey. surface(u, v): u -1..1 (soldan sağa), v v0..v1 (yukarıdan aşağı) için konumu verir.
        public static Mesh Sheet(string name, float v0, float v1, int columns, int rows, Func<float, float, Vector3> surface)
        {
            string key = Key("sheet" + name, new[] { v0, v1 }, columns, rows);
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = vertices.Count;
                for (int r = 0; r <= rows; r++)
                    for (int c = 0; c <= columns; c++)
                        vertices.Add(surface(Mathf.Lerp(-1f, 1f, c / (float)columns), Mathf.Lerp(v0, v1, r / (float)rows)));
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        int a = start + r * (columns + 1) + c, b = a + columns + 1, d = b + 1, e = a + 1;
                        if (side == 0) { triangles.Add(a); triangles.Add(b); triangles.Add(e); triangles.Add(e); triangles.Add(b); triangles.Add(d); }
                        else { triangles.Add(a); triangles.Add(e); triangles.Add(b); triangles.Add(e); triangles.Add(d); triangles.Add(b); }
                    }
                }
            }
            return Finish(key, vertices, triangles);
        }

        // ---------- Hazır kesitler ----------

        // Çok köşeli yıldız (points uçlu); inner = iç köşelerin dış yarıçapa oranı
        public static float[] Star(int points, float radius, float inner = 0.45f)
        {
            var outline = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? radius : radius * inner;
                outline[i * 2] = Mathf.Cos(angle) * r;
                outline[i * 2 + 1] = Mathf.Sin(angle) * r;
            }
            return outline;
        }

        // Elips dilimi: fromDegrees..toDegrees arası yay (180 derecelik yay yarım daire olur)
        public static float[] Arc(float radiusX, float radiusY, float fromDegrees, float toDegrees, int steps = 12)
        {
            var outline = new float[(steps + 1) * 2];
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps) * Mathf.Deg2Rad;
                outline[i * 2] = Mathf.Cos(angle) * radiusX;
                outline[i * 2 + 1] = Mathf.Sin(angle) * radiusY;
            }
            return outline;
        }

        // Kesit çizgisinde (yarıçap, yükseklik çiftleri) verilen yükseklikteki yarıçap
        public static float RadiusAt(float[] profile, float y)
        {
            int points = profile.Length / 2;
            for (int i = 0; i < points - 1; i++)
            {
                float y0 = profile[i * 2 + 1], y1 = profile[i * 2 + 3];
                if ((y >= y0 && y <= y1) || (y <= y0 && y >= y1))
                {
                    float t = Mathf.Abs(y1 - y0) < 1e-6f ? 0f : (y - y0) / (y1 - y0);
                    return Mathf.Lerp(profile[i * 2], profile[i * 2 + 2], t);
                }
            }
            return y < Mathf.Min(profile[1], profile[points * 2 - 1]) ? profile[0] : profile[points * 2 - 2];
        }
    }
}
