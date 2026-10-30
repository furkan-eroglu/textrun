using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;

namespace TextRun.Race
{
    // Pistin iki yanına basamaklı tribünler ve seyirciler kurar.
    // Yüzlerce seyirci renk gruplarına göre birleştirilir (toplam 16 çizim çağrısı); her grup kendi ritminde zıplar.
    public static class StandsBuilder
    {
        const int Tiers = 3;
        const float TierDepth = 2.2f;
        const float TierStep = 0.9f;
        const float SpectatorSpacing = 1.05f;
        const int GroupCount = 8;

        static readonly Color StandLight = new Color(0.62f, 0.64f, 0.70f);
        static readonly Color StandDark = new Color(0.52f, 0.54f, 0.60f);

        static readonly Color[] Shirts =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.95f, 0.65f, 0.20f), new Color(0.95f, 0.85f, 0.25f), new Color(0.35f, 0.75f, 0.45f),
            new Color(0.30f, 0.55f, 0.90f), new Color(0.65f, 0.40f, 0.85f), new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.55f, 0.70f)
        };

        static readonly Color[] Skins =
        {
            new Color(0.96f, 0.80f, 0.66f), new Color(0.80f, 0.60f, 0.44f), new Color(0.55f, 0.38f, 0.27f)
        };

        public static void Build(Transform parent, int laneCount, float laneWidth, float trackLength)
        {
            var root = new GameObject("Stands").transform;
            root.SetParent(parent, false);

            float halfTrack = laneCount * laneWidth / 2f;
            float zStart = -7f;
            float zEnd = trackLength + 9f;
            float zCenter = (zStart + zEnd) / 2f;

            // Basamaklar: pistten uzaklaştıkça yükselir
            for (int side = -1; side <= 1; side += 2)
            {
                for (int tier = 0; tier < Tiers; tier++)
                {
                    float height = TierStep * (tier + 1);
                    Mats.Primitive("StandTier", PrimitiveType.Cube, root,
                        new Vector3(side * TierX(halfTrack, tier), height / 2f, zCenter),
                        new Vector3(TierDepth, height, zEnd - zStart),
                        tier % 2 == 0 ? StandLight : StandDark);
                }
            }

            // Seyirciler: her biri bir gövde (küp) ve bir kafa (küre); grup başına tek mesh
            var rng = new System.Random(42);
            var bodies = new List<CombineInstance>[GroupCount];
            var heads = new List<CombineInstance>[GroupCount];
            for (int g = 0; g < GroupCount; g++)
            {
                bodies[g] = new List<CombineInstance>();
                heads[g] = new List<CombineInstance>();
            }

            Mesh box = MeshFactory.Box();
            Mesh ball = MeshFactory.Ball();

            for (int side = -1; side <= 1; side += 2)
            {
                for (int tier = 0; tier < Tiers; tier++)
                {
                    float top = TierStep * (tier + 1);
                    float x = side * TierX(halfTrack, tier);

                    for (float z = zStart + 0.5f; z < zEnd - 0.5f; z += SpectatorSpacing)
                    {
                        // Her basamakta bazı yerler boş kalsın, kalabalık daha doğal görünsün
                        if (rng.NextDouble() < 0.12) continue;

                        int g = rng.Next(GroupCount);
                        float px = x + (float)(rng.NextDouble() - 0.5) * 0.8f;
                        float pz = z + (float)(rng.NextDouble() - 0.5) * 0.5f;
                        float scale = 0.9f + (float)rng.NextDouble() * 0.25f;

                        bodies[g].Add(new CombineInstance
                        {
                            mesh = box,
                            transform = Matrix4x4.TRS(new Vector3(px, top + 0.38f * scale, pz), Quaternion.identity, new Vector3(0.5f, 0.76f, 0.38f) * scale)
                        });
                        heads[g].Add(new CombineInstance
                        {
                            mesh = ball,
                            transform = Matrix4x4.TRS(new Vector3(px, top + 0.98f * scale, pz), Quaternion.identity, Vector3.one * 0.42f * scale)
                        });
                    }
                }
            }

            for (int g = 0; g < GroupCount; g++)
            {
                var group = new GameObject("CrowdGroup" + g);
                group.transform.SetParent(root, false);
                var bob = group.AddComponent<CrowdBob>();
                bob.phase = g * 0.83f;
                bob.speed = 3.5f + (g % 3) * 0.8f;

                AddCombined(group.transform, "Bodies", bodies[g], Shirts[g % Shirts.Length]);
                AddCombined(group.transform, "Heads", heads[g], Skins[g % Skins.Length]);
            }
        }

        static float TierX(float halfTrack, int tier)
        {
            return halfTrack + 1.3f + TierDepth * (tier + 0.5f);
        }

        static void AddCombined(Transform parent, string name, List<CombineInstance> instances, Color color)
        {
            if (instances.Count == 0) return;

            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(instances.ToArray(), true, true);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(color);
        }
    }
}
