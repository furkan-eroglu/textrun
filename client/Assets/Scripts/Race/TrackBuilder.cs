using TextRun.Common;
using UnityEngine;

namespace TextRun.Race
{
    // Beş kulvarlı düz pisti kurar. Başlangıç z=0, bitiş z=trackLength.
    public static class TrackBuilder
    {
        static readonly Color Grass = new Color(0.42f, 0.74f, 0.38f);
        static readonly Color LaneA = new Color(0.86f, 0.58f, 0.46f);
        static readonly Color LaneB = new Color(0.80f, 0.52f, 0.41f);
        static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color Black = new Color(0.12f, 0.12f, 0.14f);

        // "BİTİŞ" panosunun merkezi (dünya konumu); yazı arayüzde bu noktaya hizalanarak çizilir.
        public static Vector3 FinishLabelPosition(float trackLength)
        {
            return new Vector3(0f, 3.6f, trackLength);
        }

        public static float LaneX(int lane, int laneCount, float laneWidth)
        {
            return (lane - (laneCount - 1) / 2f) * laneWidth;
        }

        public static Transform Build(int laneCount, float laneWidth, float trackLength)
        {
            var root = new GameObject("Track").transform;
            float width = laneCount * laneWidth;
            float centerZ = trackLength / 2f;

            Mats.Primitive("Grass", PrimitiveType.Cube, root, new Vector3(0f, -0.06f, centerZ), new Vector3(120f, 0.1f, trackLength + 120f), Grass);

            for (int i = 0; i < laneCount; i++)
            {
                Mats.Primitive("Lane" + i, PrimitiveType.Cube, root,
                    new Vector3(LaneX(i, laneCount, laneWidth), 0f, centerZ),
                    new Vector3(laneWidth, 0.02f, trackLength + 6f),
                    i % 2 == 0 ? LaneA : LaneB);
            }

            for (int i = 0; i <= laneCount; i++)
            {
                float x = (i - laneCount / 2f) * laneWidth;
                Mats.Primitive("LaneLine" + i, PrimitiveType.Cube, root, new Vector3(x, 0.012f, centerZ), new Vector3(0.06f, 0.01f, trackLength + 6f), White);
            }

            Mats.Primitive("StartLine", PrimitiveType.Cube, root, new Vector3(0f, 0.014f, 0f), new Vector3(width, 0.01f, 0.25f), White);
            BuildFinishLine(root, width, trackLength);
            StandsBuilder.Build(root, laneCount, laneWidth, trackLength);
            return root;
        }

        static void BuildFinishLine(Transform root, float width, float z)
        {
            const float cell = 0.35f;
            int columns = Mathf.CeilToInt(width / cell);

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    float x = -width / 2f + cell * (col + 0.5f);
                    Mats.Primitive("Finish", PrimitiveType.Cube, root,
                        new Vector3(x, 0.016f, z + (row - 0.5f) * cell),
                        new Vector3(cell, 0.01f, cell),
                        (row + col) % 2 == 0 ? White : Black);
                }
            }

            // Bitiş kapısı: iki direk ve üstte "BİTİŞ" yazısının (ekranda çizilir) duracağı koyu pano
            Mats.Primitive("GatePostL", PrimitiveType.Cube, root, new Vector3(-width / 2f - 0.2f, 2f, z), new Vector3(0.25f, 4f, 0.25f), White);
            Mats.Primitive("GatePostR", PrimitiveType.Cube, root, new Vector3(width / 2f + 0.2f, 2f, z), new Vector3(0.25f, 4f, 0.25f), White);
            Mats.Primitive("GateBanner", PrimitiveType.Cube, root, FinishLabelPosition(z), new Vector3(width + 0.65f, 1.1f, 0.3f), new Color(0.1f, 0.12f, 0.2f));
        }
    }
}
