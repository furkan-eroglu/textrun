using UnityEngine;

namespace TextRun.Race
{
    // Çok sayıda seyirciyi tek mesh'e birleştirirken kullanılan, az köşeli basit şekiller.
    // Unity'nin hazır kapsül/küre mesh'leri yüzlerce köşe içerir; kalabalıkta bu telefonu gereksiz yorar.
    public static class MeshFactory
    {
        // 1x1x1 küp (24 köşe)
        public static Mesh Box()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var triangles = new int[36];

            Vector3[] faceNormals = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faceNormals[f];
                Vector3 u = f < 2 ? Vector3.right : (f < 4 ? Vector3.right : Vector3.forward);
                Vector3 v = Vector3.Cross(n, u);
                int baseIndex = f * 4;

                vertices[baseIndex + 0] = (n - u - v) * 0.5f;
                vertices[baseIndex + 1] = (n + u - v) * 0.5f;
                vertices[baseIndex + 2] = (n + u + v) * 0.5f;
                vertices[baseIndex + 3] = (n - u + v) * 0.5f;
                for (int i = 0; i < 4; i++) normals[baseIndex + i] = n;

                int t = f * 6;
                triangles[t + 0] = baseIndex; triangles[t + 1] = baseIndex + 2; triangles[t + 2] = baseIndex + 1;
                triangles[t + 3] = baseIndex; triangles[t + 4] = baseIndex + 3; triangles[t + 5] = baseIndex + 2;
            }

            var mesh = new Mesh { name = "Box" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            FixWinding(mesh);
            return mesh;
        }

        // Çapı 1 olan, az dilimli küre (varsayılan 8x6 = 63 köşe)
        public static Mesh Ball(int longitudes = 8, int latitudes = 6)
        {
            int columns = longitudes + 1;
            var vertices = new Vector3[columns * (latitudes + 1)];
            var normals = new Vector3[vertices.Length];

            for (int lat = 0; lat <= latitudes; lat++)
            {
                float theta = Mathf.PI * lat / latitudes;
                for (int lon = 0; lon <= longitudes; lon++)
                {
                    float phi = 2f * Mathf.PI * lon / longitudes;
                    var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    int i = lat * columns + lon;
                    vertices[i] = dir * 0.5f;
                    normals[i] = dir;
                }
            }

            var triangles = new int[longitudes * latitudes * 6];
            int t = 0;
            for (int lat = 0; lat < latitudes; lat++)
            {
                for (int lon = 0; lon < longitudes; lon++)
                {
                    int a = lat * columns + lon;
                    int b = a + columns;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                    triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = "Ball" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            FixWinding(mesh);
            return mesh;
        }

        // Üçgenlerin dışa bakmasını sağlar: normali köşelerin ortalama normaline ters olan üçgenin sırasını çevirir.
        static void FixWinding(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                Vector3 avg = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                if (Vector3.Dot(faceNormal, avg) < 0f)
                {
                    int tmp = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = tmp;
                }
            }
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }
    }
}
