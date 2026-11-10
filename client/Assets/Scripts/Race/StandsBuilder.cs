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

        static readonly Color BandColor = new Color(0.06f, 0.07f, 0.13f);
        static readonly Color NeonCyan = new Color(0.25f, 0.90f, 1f);
        static readonly Color NeonPink = new Color(1f, 0.35f, 0.75f);
        static readonly Color CheckerDark = new Color(0.08f, 0.08f, 0.10f);

        // Işık çubuğu rengi: alçak çubuk mavi, yüksek çubuk pembe (birkaç sabit ton: parçalar renge göre birleştirilir)
        static Color BarColor(float wave)
        {
            float step = Mathf.Round(wave * 5f) / 5f;
            return Color.Lerp(new Color(0.20f, 0.55f, 1f), new Color(1f, 0.35f, 0.80f), step);
        }

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

            // Duvarlarda ışık duvarı: koyu bir bant üstünde yükseklikleri dalgalanan renkli ışık çubukları (ses dalgası gibi),
            // bandın altında ve üstünde ince neon çizgiler. Bitiş duvarında ortada damalı bitiş panosu.
            const float bandY = 8.3f, bandHeight = 3.4f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (outerSide - 0.02f);
                batch.AddBox(BandColor, new Vector3(x, bandY, zCenter), new Vector3(0.12f, bandHeight, zLength - 1f));
                batch.AddBox(NeonCyan, new Vector3(x - side * 0.06f, bandY - bandHeight / 2f, zCenter), new Vector3(0.08f, 0.12f, zLength - 1f));
                batch.AddBox(NeonPink, new Vector3(x - side * 0.06f, bandY + bandHeight / 2f, zCenter), new Vector3(0.08f, 0.12f, zLength - 1f));

                int bar = 0;
                for (float z = zMin + 1.2f; z < zMax - 1.2f; z += 0.95f, bar++)
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin(bar * 0.55f) * Mathf.Cos(bar * 0.17f + side);
                    float barHeight = Mathf.Lerp(0.5f, bandHeight - 0.7f, wave);
                    batch.AddBox(BarColor(wave), new Vector3(x - side * 0.08f, bandY - bandHeight / 2f + 0.3f + barHeight / 2f, z), new Vector3(0.08f, barHeight, 0.5f));
                }
            }

            float endZ = zMax - 0.32f;
            batch.AddBox(BandColor, new Vector3(0f, bandY, endZ), new Vector3(2f * outerSide - 1f, bandHeight, 0.12f));
            batch.AddBox(NeonCyan, new Vector3(0f, bandY - bandHeight / 2f, endZ - 0.06f), new Vector3(2f * outerSide - 1f, 0.12f, 0.08f));
            batch.AddBox(NeonPink, new Vector3(0f, bandY + bandHeight / 2f, endZ - 0.06f), new Vector3(2f * outerSide - 1f, 0.12f, 0.08f));
            const int checkerColumns = 12, checkerRows = 4;
            const float cell = 0.62f;
            for (int row = 0; row < checkerRows; row++)
                for (int column = 0; column < checkerColumns; column++)
                    batch.AddBox((row + column) % 2 == 0 ? BoardWhite : CheckerDark,
                        new Vector3((column - (checkerColumns - 1) / 2f) * cell, bandY + (row - (checkerRows - 1) / 2f) * cell, endZ - 0.08f), new Vector3(cell, cell, 0.08f));
            int endBar = 0;
            for (float x = -outerSide + 1.5f; x < outerSide - 1.2f; x += 0.95f, endBar++)
            {
                if (Mathf.Abs(x) < checkerColumns * cell / 2f + 0.8f) continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(endBar * 0.6f);
                float barHeight = Mathf.Lerp(0.5f, bandHeight - 0.7f, wave);
                batch.AddBox(BarColor(wave), new Vector3(x, bandY - bandHeight / 2f + 0.3f + barHeight / 2f, endZ - 0.08f), new Vector3(0.5f, barHeight, 0.08f));
            }

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
