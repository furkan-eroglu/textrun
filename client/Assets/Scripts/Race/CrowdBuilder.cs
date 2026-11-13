using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextRun.Race
{
    public struct CrowdSlot
    {
        public Vector3 position; // ayakların bastığı nokta
        public Vector3 facing;   // seyircinin baktığı yön (pist tarafı)
    }

    // Tribündeki seyirciler: erkek, kadın, kilolu, zayıf, çocuk ve yaşlı tipleri; farklı ten, saç ve kıyafet renkleri;
    // bir kısmı telefonla çekip flaş patlatır, bir kısmı kollarını kaldırıp tezahürat yapar.
    // Her seyirci küçük parçalardan (küp ve küre) kurulur; parçalar renge göre birleştirilir.
    // 4 grup halinde zıplar, flaşlar 12 grup halinde yanıp söner.
    public static class CrowdBuilder
    {
        const int BobGroups = 4;
        const int FlashGroups = 12;

        static readonly Color[] Skins =
        {
            new Color(0.96f, 0.80f, 0.68f), new Color(0.90f, 0.70f, 0.54f), new Color(0.76f, 0.55f, 0.38f),
            new Color(0.55f, 0.37f, 0.26f), new Color(0.36f, 0.24f, 0.18f)
        };

        static readonly Color[] Hairs =
        {
            new Color(0.07f, 0.06f, 0.06f), new Color(0.22f, 0.13f, 0.08f), new Color(0.42f, 0.26f, 0.13f),
            new Color(0.86f, 0.72f, 0.38f), new Color(0.62f, 0.24f, 0.12f)
        };

        static readonly Color ElderHair = new Color(0.78f, 0.78f, 0.80f);

        static readonly Color[] Shirts =
        {
            new Color(0.80f, 0.32f, 0.34f), new Color(0.90f, 0.68f, 0.28f), new Color(0.88f, 0.84f, 0.45f), new Color(0.36f, 0.70f, 0.50f),
            new Color(0.36f, 0.56f, 0.86f), new Color(0.62f, 0.46f, 0.80f), new Color(0.90f, 0.90f, 0.92f), new Color(0.88f, 0.52f, 0.68f),
            new Color(0.20f, 0.22f, 0.28f), new Color(0.95f, 0.50f, 0.25f)
        };

        static readonly Color[] Pants =
        {
            new Color(0.20f, 0.30f, 0.50f), new Color(0.10f, 0.10f, 0.13f), new Color(0.52f, 0.46f, 0.34f), new Color(0.30f, 0.30f, 0.34f)
        };

        static readonly Color PhoneColor = new Color(0.06f, 0.06f, 0.08f);
        static readonly Color FlashColor = new Color(1f, 1f, 0.92f);

        // Vücut tipleri. Boyutlar metre (yaklaşık), ağırlık: seçilme olasılığı.
        struct Kind
        {
            public float weight, torsoW, torsoH, torsoD, legH, legW, headD, scale;
            public bool female, child, elder, heavy;
        }

        static readonly Kind[] Kinds =
        {
            new Kind { weight = 24, torsoW = 0.52f, torsoH = 0.62f, torsoD = 0.34f, legH = 0.62f, legW = 0.40f, headD = 0.30f, scale = 1.00f },                 // erkek
            new Kind { weight = 10, torsoW = 0.88f, torsoH = 0.70f, torsoD = 0.58f, legH = 0.60f, legW = 0.60f, headD = 0.34f, scale = 1.00f, heavy = true },   // kilolu erkek
            new Kind { weight = 10, torsoW = 0.40f, torsoH = 0.66f, torsoD = 0.26f, legH = 0.68f, legW = 0.30f, headD = 0.28f, scale = 1.04f },                 // zayıf erkek
            new Kind { weight = 24, torsoW = 0.44f, torsoH = 0.56f, torsoD = 0.30f, legH = 0.58f, legW = 0.38f, headD = 0.28f, scale = 0.97f, female = true },  // kadın
            new Kind { weight = 8,  torsoW = 0.76f, torsoH = 0.62f, torsoD = 0.50f, legH = 0.54f, legW = 0.58f, headD = 0.31f, scale = 0.97f, female = true, heavy = true }, // kilolu kadın
            new Kind { weight = 16, torsoW = 0.44f, torsoH = 0.46f, torsoD = 0.28f, legH = 0.40f, legW = 0.30f, headD = 0.36f, scale = 0.64f, child = true },    // çocuk
            new Kind { weight = 8,  torsoW = 0.50f, torsoH = 0.58f, torsoD = 0.34f, legH = 0.56f, legW = 0.38f, headD = 0.29f, scale = 0.94f, elder = true },    // yaşlı
        };

        // animate=false: yerinde duran kişiler (pist kenarındaki görevliler); zıplamaz, flaş patlatmaz, koltuk boş kalmaz
        public static void Build(Transform parent, List<CrowdSlot> slots, int seed, bool animate = true)
        {
            var rng = new System.Random(seed);

            var parts = new Dictionary<Color, List<CombineInstance>>[BobGroups];
            for (int g = 0; g < BobGroups; g++) parts[g] = new Dictionary<Color, List<CombineInstance>>();
            var flashes = new List<CombineInstance>[FlashGroups];
            for (int f = 0; f < FlashGroups; f++) flashes[f] = new List<CombineInstance>();

            Mesh box = MeshFactory.Box();
            Mesh head = MeshFactory.Ball(6, 4);
            Mesh cap = MeshFactory.Ball(5, 3);

            foreach (var slot in slots)
            {
                if (animate && rng.NextDouble() < 0.06) continue; // boş koltuklar

                int group = rng.Next(BobGroups);
                var look = PickKind(rng);

                var origin = slot.position + new Vector3((float)(rng.NextDouble() - 0.5) * 0.3f, 0f, (float)(rng.NextDouble() - 0.5) * 0.3f);
                var root = Matrix4x4.TRS(origin, Quaternion.LookRotation(slot.facing), Vector3.one * look.scale * (0.96f + (float)rng.NextDouble() * 0.08f));

                Color skin = Skins[rng.Next(Skins.Length)];
                Color shirt = Shirts[rng.Next(Shirts.Length)];
                Color pants = Pants[rng.Next(Pants.Length)];
                Color hair = look.elder ? ElderHair : Hairs[rng.Next(Hairs.Length)];

                void Part(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 size, Color color)
                {
                    if (!parts[group].TryGetValue(color, out var list))
                    {
                        list = new List<CombineInstance>();
                        parts[group][color] = list;
                    }
                    list.Add(new CombineInstance { mesh = mesh, transform = root * Matrix4x4.TRS(pos, rot, size) });
                }

                // Gövde: bacaklar, gövde, baş
                Part(box, new Vector3(0f, look.legH / 2f, 0f), Quaternion.identity, new Vector3(look.legW, look.legH, look.torsoD * 0.9f), look.female && rng.NextDouble() < 0.5 ? shirt : pants);
                Part(box, new Vector3(0f, look.legH + look.torsoH / 2f, 0f), Quaternion.identity, new Vector3(look.torsoW, look.torsoH, look.torsoD), shirt);

                float headY = look.legH + look.torsoH + look.headD * 0.42f;
                Part(head, new Vector3(0f, headY, 0f), Quaternion.identity, new Vector3(look.headD, look.headD * 1.08f, look.headD), skin);

                // Saç: kısa (kepçe), uzun (arkada ek blok) veya kel
                bool bald = !look.child && !look.female && rng.NextDouble() < (look.elder ? 0.5 : 0.12);
                if (!bald)
                {
                    Part(cap, new Vector3(0f, headY + look.headD * 0.26f, -look.headD * 0.04f), Quaternion.identity, new Vector3(look.headD * 1.1f, look.headD * 0.7f, look.headD * 1.1f), hair);
                    if (look.female && rng.NextDouble() < 0.65)
                        Part(box, new Vector3(0f, headY - look.headD * 0.22f, -look.headD * 0.42f), Quaternion.identity, new Vector3(look.headD * 0.95f, look.headD * 1.25f, look.headD * 0.4f), hair);
                }

                // Kollar: telefonla çeken (flaşlı), tezahürat yapan (iki kol havada) ya da kollar aşağıda (gövdeyle birlikte)
                double action = animate ? rng.NextDouble() : 1.0;
                float shoulderY = look.legH + look.torsoH - 0.08f;
                float shoulderX = look.torsoW / 2f + 0.05f;

                if (action < 0.20)
                {
                    // Sağ kol ileri-yukarı, elde telefon, telefondan flaş
                    Vector3 dir = new Vector3(0.1f, 0.75f, 0.66f).normalized;
                    Vector3 shoulder = new Vector3(shoulderX, shoulderY, 0f);
                    float length = 0.46f;
                    var armRot = Quaternion.LookRotation(dir);
                    Part(box, shoulder + dir * (length / 2f), armRot, new Vector3(0.12f, 0.12f, length), skin);

                    Vector3 phonePos = shoulder + dir * (length + 0.06f);
                    Part(box, phonePos, armRot, new Vector3(0.15f, 0.23f, 0.03f), PhoneColor);

                    if (rng.NextDouble() < 0.8)
                        flashes[rng.Next(FlashGroups)].Add(new CombineInstance { mesh = head, transform = root * Matrix4x4.TRS(phonePos + dir * 0.1f, Quaternion.identity, Vector3.one * 0.30f) });
                }
                else if (action < 0.36)
                {
                    // İki kol havada
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 dir = new Vector3(side * 0.28f, 1f, 0.1f).normalized;
                        Vector3 shoulder = new Vector3(side * shoulderX, shoulderY, 0f);
                        float length = 0.5f;
                        Part(box, shoulder + dir * (length / 2f), Quaternion.LookRotation(dir), new Vector3(0.12f, 0.12f, length), skin);
                    }
                }
            }

            // Zıplayan gruplar
            for (int g = 0; g < BobGroups; g++)
            {
                var group = new GameObject("CrowdGroup" + g);
                group.transform.SetParent(parent, false);
                if (animate)
                {
                    var bob = group.AddComponent<CrowdBob>();
                    bob.phase = g * 1.3f;
                    bob.speed = 3.2f + g * 0.55f;
                }

                int index = 0;
                foreach (var pair in parts[g])
                    AddRenderer(group.transform, "Part" + index++, pair.Value, pair.Key, false);
            }

            // Flaş grupları
            for (int f = 0; f < FlashGroups; f++)
            {
                if (flashes[f].Count == 0) continue;
                var go = AddRenderer(parent, "Flash" + f, flashes[f], FlashColor, true);
                go.AddComponent<FlashBlink>();
            }
        }

        static Kind PickKind(System.Random rng)
        {
            float total = 0f;
            foreach (var k in Kinds) total += k.weight;

            float roll = (float)rng.NextDouble() * total;
            foreach (var k in Kinds)
            {
                roll -= k.weight;
                if (roll <= 0f) return k;
            }
            return Kinds[0];
        }

        static GameObject AddRenderer(Transform parent, string name, List<CombineInstance> instances, Color color, bool unlitLook)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(instances.ToArray(), true, true);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mats.Get(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
