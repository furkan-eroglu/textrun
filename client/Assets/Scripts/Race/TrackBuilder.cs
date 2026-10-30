using TextRun.Common;
using UnityEngine;

namespace TextRun.Race
{
    // Beş kulvarlı düz pisti kurar. Başlangıç z=0, bitiş z=trackLength.
    // Pist kapalı bir arenanın içindedir (bkz. StandsBuilder); bitiş kapısı FinishGateBuilder'dadır.
    public static class TrackBuilder
    {
        // Pist koyu mavi: karakterin ten rengi mavinin karşısında belirgin durur. Zemin pistten ve karakterden ayrışan koyu kömür-lacivert.
        static readonly Color ArenaFloor = new Color(0.13f, 0.14f, 0.19f);
        static readonly Color LaneA = new Color(0.14f, 0.38f, 0.80f);
        static readonly Color LaneB = new Color(0.11f, 0.32f, 0.70f);
        static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color Black = new Color(0.12f, 0.12f, 0.14f);

        public static float LaneX(int lane, int laneCount, float laneWidth)
        {
            return (lane - (laneCount - 1) / 2f) * laneWidth;
        }

        public static Transform Build(int laneCount, float laneWidth, float trackLength)
        {
            var root = new GameObject("Track").transform;
            float width = laneCount * laneWidth;
            float centerZ = trackLength / 2f;

            // Arena zemini (tribünlerin altına kadar uzanır; duvarların dışı zaten görünmez)
            Mats.Primitive("Floor", PrimitiveType.Cube, root, new Vector3(0f, -0.06f, centerZ), new Vector3(60f, 0.1f, trackLength + 60f), ArenaFloor);

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
            BuildFinishStripe(root, width, trackLength);
            FinishGateBuilder.Build(root, width, trackLength);
            StandsBuilder.Build(root, laneCount, laneWidth, trackLength);
            return root;
        }

        // Yerdeki kareli bitiş çizgisi
        static void BuildFinishStripe(Transform root, float width, float z)
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
        }
    }
}
