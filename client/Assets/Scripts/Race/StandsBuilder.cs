using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Race
{
    // Pistin etrafına kapalı bir spor salonu kurar. Dışarıdaki çimen/gökyüzü hiçbir açıdan görünmez.
    // Pist kenarı: sponsor panoları, koşu dışı şerit, hakemler, kulvar numaraları ve çıkış takozları.
    // Tribünler: beton basamaklar, renkli koltuk sıraları, merdivenli koridorlar, tünel girişleri, ön korkuluk.
    // Üst kat: camlı localar, ışıklı yazı şeridi, flamalar. Çatı: çelik kirişler ve projektörler. Bitiş duvarı: dev ekran.
    // Statik parçalar renge göre birleştirilir (MeshBatch); seyirciler gruplar halinde hafifçe zıplar.
    public static class StandsBuilder
    {
        const int Tiers = 5;
        const float TierDepth = 1.7f;
        const float TierStep = 0.8f;
        const float WallHeight = 14f;
        const float Spacing = 0.95f;

        // Koridorlar (merdivenler) bu aralıkla tekrarlanır; her iki koridorda bir tünel girişi bulunur
        const float AislePeriod = 13f;
        const float AisleWidth = 1.5f;
        const float FirstAisle = -10.25f;

        static readonly Color ConcreteA = new Color(0.44f, 0.46f, 0.51f);
        static readonly Color ConcreteB = new Color(0.37f, 0.39f, 0.44f);
        static readonly Color ConcreteLight = new Color(0.60f, 0.62f, 0.66f);
        static readonly Color WallColor = new Color(0.15f, 0.17f, 0.26f);
        static readonly Color Pilaster = new Color(0.21f, 0.24f, 0.34f);
        static readonly Color CeilingColor = new Color(0.10f, 0.11f, 0.18f);
        static readonly Color Steel = new Color(0.30f, 0.33f, 0.40f);
        static readonly Color DarkMetal = new Color(0.10f, 0.11f, 0.14f);
        static readonly Color LightStrip = new Color(1f, 0.96f, 0.82f);
        static readonly Color White = new Color(0.96f, 0.96f, 0.96f);
        static readonly Color Black = new Color(0.05f, 0.05f, 0.07f);
        static readonly Color Yellow = new Color(0.98f, 0.80f, 0.18f);
        static readonly Color Rail = new Color(0.78f, 0.80f, 0.85f);
        static readonly Color Apron = new Color(0.09f, 0.20f, 0.40f);
        static readonly Color Walkway = new Color(0.33f, 0.34f, 0.38f);
        static readonly Color GlassCool = new Color(0.42f, 0.60f, 0.80f);
        static readonly Color GlassWarm = new Color(1f, 0.84f, 0.52f);
        static readonly Color ScreenBlue = new Color(0.05f, 0.13f, 0.36f);
        static readonly Color ExitGreen = new Color(0.16f, 0.66f, 0.36f);

        static readonly Color[] SeatColors = { new Color(0.15f, 0.36f, 0.78f), new Color(0.80f, 0.22f, 0.24f) };

        // Uydurma sponsor adları ve pano renkleri
        static readonly string[] Brands = { "TEXTRUN", "VOLTA", "SPRINT", "NOVA", "TURBO", "PIXEL", "ZIP", "KLAV" };
        static readonly Color[] BrandColors =
        {
            new Color(0.12f, 0.36f, 0.86f), new Color(0.90f, 0.62f, 0.10f), new Color(0.80f, 0.20f, 0.24f), new Color(0.12f, 0.58f, 0.46f),
            new Color(0.50f, 0.28f, 0.80f), new Color(0.10f, 0.12f, 0.16f), new Color(0.86f, 0.36f, 0.12f), new Color(0.14f, 0.50f, 0.74f)
        };
        static readonly Color[] RibbonColors = { new Color(0.30f, 0.90f, 1f), new Color(1f, 0.85f, 0.25f), new Color(1f, 0.45f, 0.75f), new Color(0.55f, 1f, 0.55f) };
        static readonly Color[] BannerColors =
        {
            new Color(0.78f, 0.18f, 0.22f), new Color(0.14f, 0.36f, 0.80f), new Color(0.92f, 0.70f, 0.14f), new Color(0.14f, 0.56f, 0.40f),
            new Color(0.52f, 0.26f, 0.78f), new Color(0.88f, 0.42f, 0.12f), new Color(0.12f, 0.52f, 0.72f)
        };
        const string BannerLetters = "TEXTRUN";

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

            BuildTrackside(batch, laneCount, laneWidth, trackLength, halfTrack, sideInner, zMin, zMax);
            BuildTiers(batch, sideInner, outerSide, endInner, zMin, zMax);
            BuildUpperLevel(batch, outerSide, zMin, zMax);
            BuildRoof(batch, outerSide, zMin, zMax);
            BuildEndWall(batch, outerSide, zMax);

            // Duvarlar (yanlar, bitiş ve başlangıç) ve tavan
            for (int side = -1; side <= 1; side += 2)
                batch.AddBox(WallColor, new Vector3(side * (outerSide + 0.3f), WallHeight / 2f, zCenter), new Vector3(0.6f, WallHeight, zLength));
            batch.AddBox(WallColor, new Vector3(0f, WallHeight / 2f, zMax - 0.3f), new Vector3(width, WallHeight, 0.6f));
            batch.AddBox(WallColor, new Vector3(0f, WallHeight / 2f, zMin + 0.3f), new Vector3(width, WallHeight, 0.6f));
            batch.AddBox(CeilingColor, new Vector3(0f, WallHeight + 0.3f, zCenter), new Vector3(width, 0.6f, zLength));

            batch.Build(root, "ArenaPart");
            BuildCrowd(root, sideInner, zStart, endInner, endOuter);
            BuildOfficials(root, halfTrack, trackLength);
        }

        // z, bir koridorun (merdivenin) içinde mi?
        static bool InAisle(float z) => Mathf.Repeat(z - FirstAisle + AisleWidth / 2f, AislePeriod) < AisleWidth;

        // z, bir tünel girişinin önünde mi? (iki koridorun tam ortasında, her iki bölümde bir)
        static bool InTunnel(float z, float halfWidth) => Mathf.Abs(Mathf.Repeat(z - FirstAisle - AislePeriod / 2f + AislePeriod, AislePeriod * 2f) - AislePeriod) < halfWidth;

        // ---------- Pist kenarı ----------

        static void BuildTrackside(MeshBatch batch, int laneCount, float laneWidth, float trackLength, float halfTrack, float sideInner, float zMin, float zMax)
        {
            float zLength = zMax - zMin, zCenter = (zMin + zMax) / 2f;

            for (int side = -1; side <= 1; side += 2)
            {
                // Koşu dışı şerit ve dış beyaz çizgi, panoların arkasında beton yürüyüş yolu
                batch.AddBox(Apron, new Vector3(side * (halfTrack + 0.47f), 0.002f, zCenter), new Vector3(0.94f, 0.02f, zLength));
                batch.AddBox(White, new Vector3(side * (halfTrack + 0.8f), 0.006f, zCenter), new Vector3(0.05f, 0.02f, zLength));
                batch.AddBox(Walkway, new Vector3(side * (halfTrack + 1.3f), 0.004f, zCenter), new Vector3(0.62f, 0.02f, zLength));

                // Sponsor panoları: koyu gövde, üstte ince açık şerit, renkli bölmeler ve küp yazılar
                float boardX = side * (halfTrack + 0.98f);
                batch.AddBox(DarkMetal, new Vector3(boardX, 0.5f, zCenter), new Vector3(0.16f, 1f, zLength));
                batch.AddBox(Rail, new Vector3(boardX, 1.02f, zCenter), new Vector3(0.2f, 0.05f, zLength));
                int panel = side < 0 ? 0 : 3;
                for (float z = zMin + 0.4f; z < zMax - 6f; z += 6f, panel++)
                {
                    var brand = Brands[panel % Brands.Length];
                    var color = BrandColors[panel % BrandColors.Length];
                    batch.AddBox(color, new Vector3(boardX - side * 0.085f, 0.5f, z + 2.9f), new Vector3(0.02f, 0.88f, 5.7f));
                    PixelFont.Add(batch, brand, White, new Vector3(boardX - side * 0.1f, 0.5f, z + 2.9f), new Vector3(0f, 0f, -side), Vector3.up, 0.085f);
                }
            }

            // Her 10 metrede pist boyunca ince işaret çizgisi ve sol şeritte mesafe yazısı
            for (int meters = 10; meters < trackLength - 1f; meters += 10)
            {
                batch.AddBox(new Color(0.55f, 0.72f, 1f), new Vector3(0f, 0.012f, meters), new Vector3(halfTrack * 2f, 0.01f, 0.05f));
                PixelFont.Add(batch, meters.ToString(), White, new Vector3(-(halfTrack + 0.42f), 0.014f, meters + 0.6f), Vector3.right, Vector3.forward, 0.055f, 0.01f);
            }

            // Başlangıcın gerisinde, zeminde oyunun adı
            PixelFont.Add(batch, "TEXTRUN", new Color(0.62f, 0.78f, 1f), new Vector3(0f, 0.014f, -4.6f), Vector3.right, Vector3.forward, 0.085f, 0.01f);

            for (int lane = 0; lane < laneCount; lane++)
            {
                float x = TrackBuilder.LaneX(lane, laneCount, laneWidth);
                string number = (lane + 1).ToString();

                // Zeminde kulvar numarası (başlangıcın hemen ilerisinde ve bitişten önce)
                PixelFont.Add(batch, number, White, new Vector3(x, 0.014f, 2.4f), Vector3.right, Vector3.forward, 0.11f, 0.01f);
                PixelFont.Add(batch, number, White, new Vector3(x, 0.014f, trackLength - 3f), Vector3.right, Vector3.forward, 0.11f, 0.01f);

                // Başlangıcın gerisinde kulvar numarası kutusu
                batch.AddBox(White, new Vector3(x, 0.26f, -1.9f), new Vector3(0.42f, 0.52f, 0.3f));
                batch.AddBox(Yellow, new Vector3(x, 0.54f, -1.9f), new Vector3(0.44f, 0.05f, 0.32f));
                PixelFont.Add(batch, number, Black, new Vector3(x, 0.27f, -2.06f), Vector3.right, Vector3.up, 0.05f);

                // Çıkış takozu: ray ve iki ayak yastığı
                batch.AddBox(Steel, new Vector3(x, 0.03f, -0.55f), new Vector3(0.1f, 0.05f, 0.7f));
                batch.AddBox(DarkMetal, new Vector3(x - 0.16f, 0.09f, -0.4f), new Vector3(0.16f, 0.16f, 0.2f));
                batch.AddBox(DarkMetal, new Vector3(x + 0.16f, 0.09f, -0.7f), new Vector3(0.16f, 0.16f, 0.2f));
                batch.AddBox(new Color(0.85f, 0.2f, 0.2f), new Vector3(x - 0.16f, 0.18f, -0.4f), new Vector3(0.16f, 0.03f, 0.2f));
                batch.AddBox(new Color(0.85f, 0.2f, 0.2f), new Vector3(x + 0.16f, 0.18f, -0.7f), new Vector3(0.16f, 0.03f, 0.2f));
            }

            // Bitişte foto-finiş kamerası (üç ayaklı) ve süre tabelası
            float cameraX = halfTrack + 0.45f;
            batch.AddBox(DarkMetal, new Vector3(cameraX, 0.6f, trackLength), new Vector3(0.06f, 1.2f, 0.06f));
            batch.AddBox(DarkMetal, new Vector3(cameraX, 0.06f, trackLength), new Vector3(0.5f, 0.05f, 0.5f));
            batch.AddBox(Black, new Vector3(cameraX, 1.3f, trackLength), new Vector3(0.34f, 0.24f, 0.22f));
            batch.AddBox(GlassCool, new Vector3(cameraX - 0.2f, 1.3f, trackLength), new Vector3(0.08f, 0.14f, 0.14f));

            float clockX = -(halfTrack + 0.45f);
            batch.AddBox(DarkMetal, new Vector3(clockX, 0.45f, trackLength + 1.2f), new Vector3(0.1f, 0.9f, 0.1f));
            batch.AddBox(Black, new Vector3(clockX, 1.15f, trackLength + 1.2f), new Vector3(0.9f, 0.5f, 0.16f));
            PixelFont.Add(batch, "FINAL", Yellow, new Vector3(clockX, 1.15f, trackLength + 1.11f), Vector3.right, Vector3.up, 0.028f, 0.02f);
        }

        // ---------- Tribünler ----------

        static void BuildTiers(MeshBatch batch, float sideInner, float outerSide, float endInner, float zMin, float zMax)
        {
            float zLength = zMax - zMin, zCenter = (zMin + zMax) / 2f;

            for (int tier = 0; tier < Tiers; tier++)
            {
                float top = TierStep * (tier + 1);
                var concrete = tier % 2 == 0 ? ConcreteA : ConcreteB;

                for (int side = -1; side <= 1; side += 2)
                    batch.AddBox(concrete, new Vector3(side * (sideInner + TierDepth * (tier + 0.5f)), top / 2f, zCenter), new Vector3(TierDepth, top, zLength));
                batch.AddBox(concrete, new Vector3(0f, top / 2f, endInner + TierDepth * (tier + 0.5f)), new Vector3(2f * outerSide, top, TierDepth));

                // Koltuk sıraları: basamağın arka yarısında, bölümden bölüme renk değiştirir
                for (int side = -1; side <= 1; side += 2)
                {
                    float seatX = side * (sideInner + TierDepth * (tier + 0.78f));
                    int section = 0;
                    for (float from = FirstAisle - AislePeriod + AisleWidth / 2f; from < zMax; from += AislePeriod, section++)
                    {
                        float a = Mathf.Max(from, zMin + 0.3f), b = Mathf.Min(from + AislePeriod - AisleWidth, zMax - 0.3f);
                        if (b - a < 0.5f) continue;
                        var seat = SeatColors[(section + (side < 0 ? 0 : 1)) % SeatColors.Length];
                        batch.AddBox(seat, new Vector3(seatX, top + 0.24f, (a + b) / 2f), new Vector3(0.5f, 0.48f, b - a));
                    }

                    // Koridor merdiveni: her basamağın arka yarısında yarım basamak ve sarı kenar şeridi
                    for (float aisle = FirstAisle; aisle < zMax - 1f; aisle += AislePeriod)
                    {
                        if (aisle < zMin + 1f) continue;
                        float frontX = side * (sideInner + TierDepth * (tier + 0.25f));
                        float backX = side * (sideInner + TierDepth * (tier + 0.75f));
                        batch.AddBox(ConcreteLight, new Vector3(frontX, top + 0.005f, aisle), new Vector3(TierDepth / 2f, 0.02f, AisleWidth));
                        batch.AddBox(ConcreteLight, new Vector3(backX, top + TierStep / 4f, aisle), new Vector3(TierDepth / 2f, TierStep / 2f, AisleWidth));
                        batch.AddBox(Yellow, new Vector3(frontX - side * (TierDepth / 4f - 0.04f), top + 0.02f, aisle), new Vector3(0.07f, 0.02f, AisleWidth));
                        batch.AddBox(Yellow, new Vector3(backX - side * (TierDepth / 4f - 0.04f), top + TierStep / 2f + 0.01f, aisle), new Vector3(0.07f, 0.02f, AisleWidth));
                    }
                }

                // Bitiş arkasındaki tribünün koltukları
                batch.AddBox(SeatColors[tier % 2], new Vector3(0f, top + 0.24f, endInner + TierDepth * (tier + 0.78f)), new Vector3(2f * sideInner - 1f, 0.48f, 0.5f));
            }

            for (int side = -1; side <= 1; side += 2)
            {
                // Ön korkuluk: alçak beton duvar ve üstünde metal tırabzan
                float parapetX = side * (sideInner - 0.07f);
                batch.AddBox(ConcreteLight, new Vector3(parapetX, 0.6f, zCenter), new Vector3(0.14f, 1.2f, zLength));
                batch.AddBox(Rail, new Vector3(parapetX, 1.32f, zCenter), new Vector3(0.06f, 0.05f, zLength));
                for (float z = zMin + 1f; z < zMax; z += 2.2f)
                    batch.AddBox(Rail, new Vector3(parapetX, 1.26f, z), new Vector3(0.05f, 0.14f, 0.05f));

                // Tünel girişleri: 3. ve 4. basamaklar arasında beton çıkıntı, karanlık ağız, üstünde yeşil çıkış tabelası
                float blockX = side * (sideInner + TierDepth * 3.3f);
                float mouthX = side * (sideInner + TierDepth * 2.5f);
                float floor = TierStep * 3f;
                for (float z = FirstAisle + AislePeriod / 2f; z < zMax - 3f; z += AislePeriod * 2f)
                {
                    if (z < zMin + 3f) continue;
                    batch.AddBox(ConcreteLight, new Vector3(blockX, floor + 1.05f, z), new Vector3(TierDepth * 1.6f, 2.1f, 3.4f));
                    batch.AddBox(Black, new Vector3(mouthX - side * 0.02f, floor + 0.85f, z), new Vector3(0.06f, 1.7f, 2.3f));
                    batch.AddBox(ExitGreen, new Vector3(mouthX - side * 0.04f, floor + 1.88f, z), new Vector3(0.06f, 0.22f, 0.9f));
                    batch.AddBox(Rail, new Vector3(blockX, floor + 2.14f, z), new Vector3(TierDepth * 1.6f, 0.05f, 3.4f));
                }
            }

            // Bitiş tribününün ön korkuluğu
            batch.AddBox(ConcreteLight, new Vector3(0f, 0.6f, endInner - 0.07f), new Vector3(2f * sideInner, 1.2f, 0.14f));
            batch.AddBox(Rail, new Vector3(0f, 1.32f, endInner - 0.07f), new Vector3(2f * sideInner, 0.05f, 0.06f));
        }

        // ---------- Üst kat: localar, ışıklı yazı şeridi, flamalar ----------

        static void BuildUpperLevel(MeshBatch batch, float outerSide, float zMin, float zMax)
        {
            float zLength = zMax - zMin - 1f, zCenter = (zMin + zMax) / 2f;

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (outerSide - 0.02f);
                var toTrack = new Vector3(-side, 0f, 0f);

                // Localar: koyu çerçeve içinde camlar (bir kısmı içeriden aydınlatılmış), altta balkon çıkıntısı
                batch.AddBox(DarkMetal, new Vector3(x, 6.2f, zCenter), new Vector3(0.3f, 2.4f, zLength));
                batch.AddBox(ConcreteLight, new Vector3(x - side * 0.3f, 4.95f, zCenter), new Vector3(0.9f, 0.16f, zLength));
                batch.AddBox(Rail, new Vector3(x - side * 0.72f, 5.3f, zCenter), new Vector3(0.05f, 0.05f, zLength));
                int box = 0;
                for (float z = zMin + 1.2f; z < zMax - 4.4f; z += 4.5f, box++)
                {
                    bool lit = (box * 7 + (side < 0 ? 0 : 3)) % 5 < 2;
                    batch.AddBox(lit ? GlassWarm : GlassCool, new Vector3(x - side * 0.16f, 6.2f, z + 2f), new Vector3(0.04f, 1.7f, 3.9f));
                    batch.AddBox(DarkMetal, new Vector3(x - side * 0.18f, 6.2f, z + 2f), new Vector3(0.05f, 1.7f, 0.07f));
                    batch.AddBox(Rail, new Vector3(x - side * 0.72f, 5.14f, z), new Vector3(0.05f, 0.34f, 0.05f));
                    // Locadaki izleyicilerin silüetleri
                    if (lit)
                        for (int person = 0; person < 3; person++)
                            batch.AddBox(new Color(0.16f, 0.14f, 0.16f), new Vector3(x - side * 0.13f, 5.75f + (person % 2) * 0.08f, z + 0.9f + person * 1.1f), new Vector3(0.04f, 0.75f, 0.42f));
                }

                // Işıklı yazı şeridi: siyah bant üstünde parlak renkli sponsor yazıları
                batch.AddBox(Black, new Vector3(x, 8.05f, zCenter), new Vector3(0.34f, 0.86f, zLength));
                batch.AddBox(Steel, new Vector3(x, 8.52f, zCenter), new Vector3(0.38f, 0.07f, zLength));
                batch.AddBox(Steel, new Vector3(x, 7.58f, zCenter), new Vector3(0.38f, 0.07f, zLength));
                int word = side < 0 ? 0 : 2;
                for (float z = zMin + 4f; z < zMax - 4f; z += 6.5f, word++)
                {
                    PixelFont.Add(batch, Brands[word % Brands.Length], RibbonColors[word % RibbonColors.Length], new Vector3(x - side * 0.18f, 8.05f, z), new Vector3(0f, 0f, -side), Vector3.up, 0.08f);
                    batch.AddBox(RibbonColors[(word + 1) % RibbonColors.Length], new Vector3(x - side * 0.18f, 8.05f, z + 3.25f), new Vector3(0.03f, 0.18f, 0.18f));
                }

                // Duvar sütunları ve aralarında harfli flamalar
                int banner = side < 0 ? 0 : 3;
                for (float z = zMin + 3.5f; z < zMax - 3f; z += 6.5f, banner++)
                {
                    batch.AddBox(Pilaster, new Vector3(x - side * 0.05f, 11f, z - 3.25f), new Vector3(0.3f, 4.8f, 0.5f));
                    var color = BannerColors[banner % BannerColors.Length];
                    batch.AddBox(Steel, new Vector3(x - side * 0.2f, 12.75f, z), new Vector3(0.08f, 0.08f, 2.7f));
                    batch.AddBox(color, new Vector3(x - side * 0.2f, 10.95f, z), new Vector3(0.05f, 3.5f, 2.3f));
                    batch.AddBox(White, new Vector3(x - side * 0.23f, 9.5f, z), new Vector3(0.03f, 0.2f, 2.3f));
                    PixelFont.Add(batch, BannerLetters[banner % BannerLetters.Length].ToString(), White, new Vector3(x - side * 0.24f, 11.2f, z), new Vector3(0f, 0f, -side), Vector3.up, 0.24f);
                }
            }
        }

        // ---------- Çatı: çelik kirişler, projektörler, tavan ışıkları ----------

        static void BuildRoof(MeshBatch batch, float outerSide, float zMin, float zMax)
        {
            for (float z = zMin + 6f; z < zMax - 3f; z += 9f)
            {
                // Tavan ışık paneli
                batch.AddBox(LightStrip, new Vector3(0f, WallHeight - 0.05f, z + 4.5f), new Vector3(2f * outerSide * 0.7f, 0.1f, 0.9f));

                // Kafes kiriş: alt ve üst başlık, aralarında dikmeler
                batch.AddBox(Steel, new Vector3(0f, 12.5f, z), new Vector3(2f * outerSide, 0.2f, 0.2f));
                batch.AddBox(Steel, new Vector3(0f, 13.7f, z), new Vector3(2f * outerSide, 0.2f, 0.2f));
                for (float x = -outerSide + 1f; x < outerSide; x += 2.3f)
                    batch.AddBox(Steel, new Vector3(x, 13.1f, z), new Vector3(0.12f, 1.1f, 0.12f));

                // Projektör grupları: kirişin altında, piste bakan lamba dizileri
                foreach (float x in new[] { -outerSide * 0.55f, outerSide * 0.55f })
                {
                    batch.AddBox(DarkMetal, new Vector3(x, 12.2f, z), new Vector3(2.2f, 0.4f, 0.6f));
                    for (int lamp = 0; lamp < 4; lamp++)
                        batch.AddBox(LightStrip, new Vector3(x - 0.78f + lamp * 0.52f, 11.98f, z), new Vector3(0.36f, 0.08f, 0.4f));
                }
            }

            // Boyuna kirişler
            foreach (float x in new[] { -outerSide * 0.55f, 0f, outerSide * 0.55f })
                batch.AddBox(Steel, new Vector3(x, 13.75f, (zMin + zMax) / 2f), new Vector3(0.16f, 0.16f, zMax - zMin));
        }

        // ---------- Bitiş duvarı: dev ekran ----------

        static void BuildEndWall(MeshBatch batch, float outerSide, float zMax)
        {
            float z = zMax - 0.75f;
            batch.AddBox(DarkMetal, new Vector3(0f, 9.3f, z), new Vector3(12f, 5.6f, 0.5f));
            batch.AddBox(ScreenBlue, new Vector3(0f, 9.3f, z - 0.26f), new Vector3(11.2f, 4.8f, 0.04f));
            PixelFont.Add(batch, "TEXTRUN", White, new Vector3(0f, 9.9f, z - 0.3f), Vector3.right, Vector3.up, 0.22f, 0.04f);
            PixelFont.Add(batch, "FINAL", Yellow, new Vector3(0f, 8.05f, z - 0.3f), Vector3.right, Vector3.up, 0.12f, 0.04f);

            // Ekranın altında ve üstünde damalı şerit
            const float cell = 0.4f;
            for (int col = 0; col < 28; col++)
            {
                float x = (col - 13.5f) * cell;
                if (col % 2 == 0)
                {
                    batch.AddBox(White, new Vector3(x, 11.45f, z - 0.3f), new Vector3(cell, cell * 0.6f, 0.04f));
                    batch.AddBox(White, new Vector3(x, 7.15f, z - 0.3f), new Vector3(cell, cell * 0.6f, 0.04f));
                }
            }

            // Askı halatları ve iki yanda ışıklı yazı şeridi
            foreach (float x in new[] { -4.5f, 4.5f })
                batch.AddBox(Steel, new Vector3(x, 12.9f, z), new Vector3(0.08f, 1.8f, 0.08f));
            batch.AddBox(Black, new Vector3(0f, 5.6f, zMax - 0.5f), new Vector3(2f * outerSide - 1f, 0.86f, 0.2f));
            int word = 1;
            for (float x = -outerSide + 5f; x < outerSide - 4f; x += 6.5f, word++)
                PixelFont.Add(batch, Brands[word % Brands.Length], RibbonColors[word % RibbonColors.Length], new Vector3(x, 5.6f, zMax - 0.62f), Vector3.right, Vector3.up, 0.08f);
        }

        // ---------- Seyirciler ve görevliler ----------

        // Seyirci yerleri: yan tribünlerde ve bitiş arkasında her basamakta bir sıra (koridorlar ve tünel önleri boş kalır)
        static void BuildCrowd(Transform root, float sideInner, float zStart, float endInner, float endOuter)
        {
            var slots = new List<CrowdSlot>();

            for (int tier = 0; tier < Tiers; tier++)
            {
                float top = TierStep * (tier + 1);

                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (sideInner + TierDepth * (tier + 0.32f));
                    for (float z = zStart + 0.5f; z < endOuter - 0.5f; z += Spacing)
                    {
                        if (InAisle(z)) continue;
                        if ((tier == 2 || tier == 3) && InTunnel(z, 1.9f)) continue;
                        slots.Add(new CrowdSlot { position = new Vector3(x, top, z), facing = new Vector3(-side, 0f, 0f) });
                    }
                }

                // Bitiş arkasındaki tribün (yan tribünlerin arasında kalan kısım), pistin başına bakar
                float ez = endInner + TierDepth * (tier + 0.32f);
                for (float x = -sideInner + 0.5f; x < sideInner - 0.4f; x += Spacing)
                    slots.Add(new CrowdSlot { position = new Vector3(x, top, ez), facing = new Vector3(0f, 0f, -1f) });
            }

            CrowdBuilder.Build(root, slots, 42);
        }

        // Pist kenarındaki görevliler ve fotoğrafçılar (yerlerinde durur, zıplamaz)
        static void BuildOfficials(Transform root, float halfTrack, float trackLength)
        {
            float x = halfTrack + 0.45f;
            var slots = new List<CrowdSlot>();
            foreach (float z in new[] { -3f, 14f, 31f, 47f, trackLength - 1.2f, trackLength + 2.4f })
            {
                int side = Mathf.RoundToInt(z) % 2 == 0 ? -1 : 1;
                slots.Add(new CrowdSlot { position = new Vector3(side * x, 0f, z), facing = new Vector3(-side, 0f, 0f) });
            }
            slots.Add(new CrowdSlot { position = new Vector3(-x, 0f, trackLength + 0.4f), facing = new Vector3(1f, 0f, 0f) });
            slots.Add(new CrowdSlot { position = new Vector3(x, 0f, trackLength - 2.6f), facing = new Vector3(-1f, 0f, 0f) });
            CrowdBuilder.Build(root, slots, 7, false);
        }
    }
}
