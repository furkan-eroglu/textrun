using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;

namespace TextRun.Race
{
    // Klasik bitiş kapısı: iki direk, üstte alt ve üstü kareli bayrak desenli bant ve ortasında "FINISH" yazısı.
    // Yazı küçük küplerden (piksel yazı tipi) kurulur: her çözünürlükte keskin görünür, dil/yazı tipi dosyası gerektirmez.
    public static class FinishGateBuilder
    {
        const float PixelSize = 0.17f;
        const float CheckerCell = 0.3f;
        const float BannerHeight = 2.5f;
        const float BannerCenterY = 4.3f;

        static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color Black = new Color(0.07f, 0.07f, 0.09f);

        // 5x7 piksel harfler (1 = dolu), yukarıdan aşağıya
        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
            { 'I', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" } },
            { 'N', new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" } },
            { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
            { 'H', new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" } },
        };

        public static void Build(Transform root, float trackWidth, float z)
        {
            var gate = new GameObject("FinishGate").transform;
            gate.SetParent(root, false);

            float bannerWidth = trackWidth + 0.9f;

            // Direkler
            for (int side = -1; side <= 1; side += 2)
                NoShadow(Mats.Primitive("Post", PrimitiveType.Cube, gate, new Vector3(side * (trackWidth / 2f + 0.3f), 2.6f, z), new Vector3(0.4f, 5.2f, 0.4f), White));

            // Beyaz bant gövdesi
            NoShadow(Mats.Primitive("Banner", PrimitiveType.Cube, gate, new Vector3(0f, BannerCenterY, z), new Vector3(bannerWidth, BannerHeight, 0.2f), White));

            // Siyah ayrıntılar (kareli bayrak desenleri ve harfler) tek mesh olarak birleştirilir
            var boxes = new List<CombineInstance>();
            Mesh box = MeshFactory.Box();
            float faceZ = z - 0.13f; // kameraya (başlangıca) bakan yüz

            // Alt ve üst kareli bantlar: 2 satır
            int columns = Mathf.FloorToInt(bannerWidth / CheckerCell);
            float startX = -columns * CheckerCell / 2f + CheckerCell / 2f;
            for (int band = 0; band < 2; band++)
            {
                for (int row = 0; row < 2; row++)
                {
                    // band 0: bandın üst kenarından aşağı, band 1: alt kenarından yukarı
                    float y = band == 0
                        ? BannerCenterY + BannerHeight / 2f - CheckerCell * (row + 0.5f)
                        : BannerCenterY - BannerHeight / 2f + CheckerCell * (row + 0.5f);

                    for (int col = 0; col < columns; col++)
                    {
                        if ((col + row) % 2 != 0) continue;
                        boxes.Add(Cell(box, new Vector3(startX + col * CheckerCell, y, faceZ), CheckerCell, CheckerCell));
                    }
                }
            }

            // "FINISH" yazısı: ortada, harfler arası 1 piksel boşluk
            const string text = "FINISH";
            int totalPixels = text.Length * 5 + (text.Length - 1);
            float textLeft = -totalPixels * PixelSize / 2f + PixelSize / 2f;
            float textTop = BannerCenterY + 7 * PixelSize / 2f - PixelSize / 2f;

            int pixelCursor = 0;
            foreach (char c in text)
            {
                string[] glyph = Font[c];
                for (int row = 0; row < 7; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        if (glyph[row][col] != '1') continue;
                        float x = textLeft + (pixelCursor + col) * PixelSize;
                        float y = textTop - row * PixelSize;
                        boxes.Add(Cell(box, new Vector3(x, y, faceZ), PixelSize, PixelSize));
                    }
                }
                pixelCursor += 6;
            }

            var mesh = new Mesh { name = "FinishMarks" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(boxes.ToArray(), true, true);

            var marks = new GameObject("FinishMarks");
            marks.transform.SetParent(gate, false);
            marks.AddComponent<MeshFilter>().sharedMesh = mesh;
            var marksRenderer = marks.AddComponent<MeshRenderer>();
            marksRenderer.sharedMaterial = Mats.Get(Black);
            marksRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Kapı gölge düşürmez: bitişten geçen karakter panonun gölgesinde koyulaşmasın
        static void NoShadow(GameObject go)
        {
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static CombineInstance Cell(Mesh box, Vector3 center, float width, float height)
        {
            return new CombineInstance
            {
                mesh = box,
                transform = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(width, height, 0.06f))
            };
        }
    }
}
