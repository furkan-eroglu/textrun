using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextRun.Race
{
    // Pistin etrafına kapalı bir arena kurar: iki yanda ve bitişin arkasında basamaklı tribünler, yüksek duvarlar,
    // reklam panoları, tavan ve ışık şeritleri. Dışarıdaki çimen/gökyüzü hiçbir açıdan görünmez.
    // Statik parçalar renge göre birleştirilir (MeshBatch); seyirciler 8 grup halinde hafifçe zıplar.
    public static class StandsBuilder
    {
        const int Tiers = 5;
        const float TierDepth = 1.7f;
        const float TierStep = 0.8f;
        const float WallHeight = 14f;
        const float Spacing = 0.95f;

        static readonly Color TierA = new Color(0.27f, 0.29f, 0.37f);
        static readonly Color TierB = new Color(0.22f, 0.24f, 0.32f);
        static readonly Color WallColor = new Color(0.15f, 0.17f, 0.26f);
        static readonly Color CeilingColor = new Color(0.10f, 0.11f, 0.18f);
        static readonly Color LightStrip = new Color(1f, 0.96f, 0.82f);
        static readonly Color BoardBlue = new Color(0.20f, 0.42f, 0.88f);
        static readonly Color BoardWhite = new Color(0.96f, 0.96f, 0.96f);

        static readonly Color[] PanelColors =
        {
            new Color(0.85f, 0.25f, 0.28f), new Color(0.95f, 0.72f, 0.18f), new Color(0.20f, 0.60f, 0.85f), new Color(0.30f, 0.72f, 0.45f)
        };

        public static void Build(Transform parent, int laneCount, float laneWidth, float trackLength)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            float halfTrack = laneCount * laneWidth / 2f;
            float sideInner = halfTrack + 1.6f;
            float outerSide = sideInner + Tiers * TierDepth;
            float zStart = -17f;
            float endInner = trackLength + 5f;
            float endOuter = endInner + Tiers * TierDepth;

            float zMin = zStart - 0.3f;
            float zMax = endOuter + 0.3f;
            float zLength = zMax - zMin;
            float zCenter = (zMin + zMax) / 2f;
            float width = 2f * outerSide + 1.2f;

            var batch = new MeshBatch();

            // Pist kenarındaki reklam panoları: mavi/beyaz dönüşümlü
            int segment = 0;
            for (float z = zMin; z < zMax; z += 4f, segment++)
            {
                float len = Mathf.Min(4f, zMax - z);
                var color = segment % 2 == 0 ? BoardBlue : BoardWhite;
                for (int side = -1; side <= 1; side += 2)
                    batch.AddBox(color, new Vector3(side * (halfTrack + 0.9f), 0.45f, z + len / 2f), new Vector3(0.2f, 0.9f, len));
            }

            // Basamaklar: yanlarda ve bitişin arkasında
            for (int tier = 0; tier < Tiers; tier++)
            {
                float top = TierStep * (tier + 1);
                var color = tier % 2 == 0 ? TierA : TierB;

                for (int side = -1; side <= 1; side += 2)
                    batch.AddBox(color, new Vector3(side * (sideInner + TierDepth * (tier + 0.5f)), top / 2f, zCenter), new Vector3(TierDepth, top, zLength));

                batch.AddBox(color, new Vector3(0f, top / 2f, endInner + TierDepth * (tier + 0.5f)), new Vector3(2f * outerSide, top, TierDepth));
            }

            // Duvarlar (yanlar, bitiş ve başlangıç) ve tavan
            for (int side = -1; side <= 1; side += 2)
                batch.AddBox(WallColor, new Vector3(side * (outerSide + 0.3f), WallHeight / 2f, zCenter), new Vector3(0.6f, WallHeight, zLength));
            batch.AddBox(WallColor, new Vector3(0f, WallHeight / 2f, zMax - 0.3f), new Vector3(width, WallHeight, 0.6f));
            batch.AddBox(WallColor, new Vector3(0f, WallHeight / 2f, zMin + 0.3f), new Vector3(width, WallHeight, 0.6f));
            batch.AddBox(CeilingColor, new Vector3(0f, WallHeight + 0.3f, zCenter), new Vector3(width, 0.6f, zLength));

            // Tavan ışık şeritleri
            for (float z = zMin + 6f; z < zMax - 3f; z += 9f)
                batch.AddBox(LightStrip, new Vector3(0f, WallHeight - 0.05f, z), new Vector3(2f * outerSide * 0.85f, 0.1f, 1.1f));

            // Duvarlarda renkli sponsor panoları
            int panel = 0;
            for (float z = zMin + 3f; z < zMax - 5f; z += 7f, panel++)
                for (int side = -1; side <= 1; side += 2)
                    batch.AddBox(PanelColors[panel % PanelColors.Length], new Vector3(side * (outerSide - 0.02f), 8.2f, z), new Vector3(0.12f, 3.2f, 5.5f));
            for (float x = -outerSide + 3f; x < outerSide - 2f; x += 7f, panel++)
                batch.AddBox(PanelColors[panel % PanelColors.Length], new Vector3(x, 8.2f, zMax - 0.32f), new Vector3(5.5f, 3.2f, 0.12f));

            batch.Build(root, "ArenaPart");
            BuildCrowd(root, outerSide, sideInner, zStart, endInner, endOuter);
        }

        // Seyirci yerlerini hesaplar: yan tribünlerde ve bitiş arkasında her basamakta bir sıra
        static void BuildCrowd(Transform root, float outerSide, float sideInner, float zStart, float endInner, float endOuter)
        {
            var slots = new List<CrowdSlot>();

            for (int tier = 0; tier < Tiers; tier++)
            {
                float top = TierStep * (tier + 1);

                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (sideInner + TierDepth * (tier + 0.5f));
                    for (float z = zStart + 0.5f; z < endOuter - 0.5f; z += Spacing)
                        slots.Add(new CrowdSlot { position = new Vector3(x, top, z), facing = new Vector3(-side, 0f, 0f) });
                }

                // Bitiş arkasındaki tribün (yan tribünlerin arasında kalan kısım), pistin başına bakar
                float ez = endInner + TierDepth * (tier + 0.5f);
                for (float x = -sideInner + 0.5f; x < sideInner - 0.4f; x += Spacing)
                    slots.Add(new CrowdSlot { position = new Vector3(x, top, ez), facing = new Vector3(0f, 0f, -1f) });
            }

            CrowdBuilder.Build(root, slots, 42);
        }
    }
}
