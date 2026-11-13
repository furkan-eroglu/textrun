using System.Collections.Generic;
using TextRun.Characters;
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
    // Her seyirci insan biçimli parçalardan kurulur: omuzdan bele daralan gövde, incelen kol ve bacaklar, boyun, saç,
    // gözler, ayakkabı; kimi atkılı, şapkalı ya da etekli. Parçalar az üçgenlidir ve renge göre birleştirilir.
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
            Mesh head = MeshFactory.Ball(8, 5);
            Mesh cap = MeshFactory.Ball(6, 3);
            // Birim ölçülü parçalar (ölçek verilerek kullanılır): gövde 1 birim yüksek ve 1 birim geniş;
            // uzuv tepesinden aşağı 1 birim sarkar ve uca doğru incelir; etek aşağı doğru genişler
            Mesh torso = MeshKit.Lathe(new[] { 0f, 0f, 0.40f, 0f, 0.44f, 0.12f, 0.40f, 0.38f, 0.47f, 0.70f, 0.50f, 0.86f, 0.36f, 0.96f, 0.14f, 1f, 0f, 1f }, 8);
            Mesh limb = MeshKit.Lathe(new[] { 0.36f, -1f, 0.5f, 0f }, 6);
            Mesh skirt = MeshKit.Lathe(new[] { 0.66f, -1f, 0.5f, 0f }, 8);

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

                // Uzuv parçası: "from" noktasından "dir" yönünde uzanır; ucunun konumunu döner
                Vector3 Limb(Vector3 from, Vector3 dir, float length, float width, Color color)
                {
                    dir = dir.normalized;
                    Part(limb, from, Quaternion.FromToRotation(Vector3.down, dir), new Vector3(width, length, width), color);
                    return from + dir * length;
                }

                float build = look.heavy ? 1.3f : 1f;
                bool skirted = look.female && rng.NextDouble() < 0.35;
                bool longSleeves = rng.NextDouble() < 0.4;
                bool sleeveless = !longSleeves && rng.NextDouble() < 0.12;
                Color sleeve = sleeveless ? skin : shirt;
                Color forearm = longSleeves ? shirt : skin;
                Color shoe = rng.NextDouble() < 0.3 ? new Color(0.88f, 0.88f, 0.9f) : new Color(0.10f, 0.10f, 0.12f);

                // Bacaklar (etekliyse etek + çıplak bacak), ayakkabılar
                float legGap = look.legW * 0.27f;
                float legWidth = look.legW * 0.52f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(limb, new Vector3(side * legGap, look.legH + 0.02f, 0f), Quaternion.identity, new Vector3(legWidth, look.legH, legWidth * 1.05f), skirted ? skin : pants);
                    Part(box, new Vector3(side * legGap, 0.04f, 0.05f), Quaternion.identity, new Vector3(legWidth * 0.8f, 0.08f, 0.26f), shoe);
                }
                if (skirted) Part(skirt, new Vector3(0f, look.legH + 0.06f, 0f), Quaternion.identity, new Vector3(look.torsoW * 0.92f, look.legH * 0.55f, look.torsoD * 1.05f), rng.NextDouble() < 0.5 ? shirt : pants);

                // Gövde, boyun, baş
                Part(torso, new Vector3(0f, look.legH, 0f), Quaternion.identity, new Vector3(look.torsoW, look.torsoH, look.torsoD), shirt);
                float neckY = look.legH + look.torsoH * 0.97f;
                Part(limb, new Vector3(0f, neckY + 0.1f, 0f), Quaternion.identity, new Vector3(look.headD * 0.42f, 0.14f, look.headD * 0.42f), skin);

                float headY = neckY + look.headD * 0.5f;
                Part(head, new Vector3(0f, headY, 0f), Quaternion.identity, new Vector3(look.headD * 0.92f, look.headD * 1.1f, look.headD), skin);

                // Gözler (yakındaki seyircilerde yüz boş kalmasın)
                var eye = new Color(0.10f, 0.08f, 0.08f);
                for (int side = -1; side <= 1; side += 2)
                    Part(box, new Vector3(side * look.headD * 0.19f, headY + look.headD * 0.06f, look.headD * 0.46f), Quaternion.identity, new Vector3(look.headD * 0.11f, look.headD * 0.13f, 0.03f), eye);

                // Saç ya da şapka
                double style = rng.NextDouble();
                bool bald = !look.child && !look.female && style < (look.elder ? 0.45 : 0.10);
                bool capped = !bald && !look.elder && style > 0.86;
                if (capped)
                {
                    Color capColor = Shirts[rng.Next(Shirts.Length)];
                    Part(cap, new Vector3(0f, headY + look.headD * 0.3f, 0f), Quaternion.identity, new Vector3(look.headD * 1.02f, look.headD * 0.62f, look.headD * 1.08f), capColor);
                    Part(box, new Vector3(0f, headY + look.headD * 0.3f, look.headD * 0.58f), Quaternion.identity, new Vector3(look.headD * 0.72f, 0.03f, look.headD * 0.42f), capColor);
                }
                else if (!bald)
                {
                    Part(cap, new Vector3(0f, headY + look.headD * 0.24f, -look.headD * 0.06f), Quaternion.identity, new Vector3(look.headD * 1.04f, look.headD * 0.78f, look.headD * 1.1f), hair);
                    if (look.female && style < 0.5)
                        Part(cap, new Vector3(0f, headY - look.headD * 0.2f, -look.headD * 0.3f), Quaternion.identity, new Vector3(look.headD * 1.02f, look.headD * 1.3f, look.headD * 0.6f), hair); // omza inen saç
                    else if (look.female && style < 0.72)
                        Part(cap, new Vector3(0f, headY + look.headD * 0.5f, -look.headD * 0.34f), Quaternion.identity, Vector3.one * look.headD * 0.46f, hair); // topuz
                }

                // Taraftar atkısı
                if (rng.NextDouble() < 0.18)
                {
                    Color team = rng.NextDouble() < 0.5 ? new Color(0.82f, 0.16f, 0.18f) : new Color(0.14f, 0.36f, 0.82f);
                    Part(limb, new Vector3(0f, neckY + 0.07f, 0f), Quaternion.identity, new Vector3(look.torsoW * 0.62f, 0.12f, look.torsoD * 0.95f), team);
                    Part(box, new Vector3(look.torsoW * 0.16f, neckY - look.torsoH * 0.2f, look.torsoD * 0.46f), Quaternion.identity, new Vector3(0.11f, look.torsoH * 0.42f, 0.04f), team);
                }

                // Kollar: üst kol (giysi rengi) + ön kol (uzun kolluysa giysi, değilse ten) + el.
                // Duruşlar: kollar aşağıda, iki kol havada, alkış, telefonla çekim (flaşlı), tek kolla el sallama.
                float shoulderY = look.legH + look.torsoH * 0.86f;
                float shoulderX = look.torsoW * 0.46f;
                float upperLength = look.torsoH * 0.5f, foreLength = look.torsoH * 0.48f;
                float armWidth = 0.12f * build;

                Vector3 Arm(int side, Vector3 upperDir, Vector3 foreDir)
                {
                    var elbow = Limb(new Vector3(side * shoulderX, shoulderY, 0f), upperDir, upperLength, armWidth * 1.15f, sleeve);
                    var wrist = Limb(elbow, foreDir, foreLength, armWidth, forearm);
                    Part(cap, wrist, Quaternion.identity, Vector3.one * armWidth * 1.05f, skin);
                    return wrist;
                }

                Vector3 Down(int side) => Arm(side, new Vector3(side * 0.14f, -1f, 0.04f), new Vector3(side * 0.04f, -1f, 0.16f));
                Vector3 Up(int side) => Arm(side, new Vector3(side * 0.4f, 0.85f, 0.1f), new Vector3(side * 0.08f, 1f, 0.06f));

                double action = animate ? rng.NextDouble() : 1.0;
                if (action < 0.16)
                {
                    // Telefonla çekim: sağ kol önde, elde telefon, telefondan flaş
                    var hand = Arm(1, new Vector3(0.15f, 0.15f, 0.85f), new Vector3(-0.1f, 0.85f, 0.45f));
                    Down(-1);
                    Vector3 phonePos = hand + new Vector3(0f, 0.1f, 0.03f);
                    Part(box, phonePos, Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.15f, 0.24f, 0.03f), PhoneColor);
                    if (rng.NextDouble() < 0.8)
                        flashes[rng.Next(FlashGroups)].Add(new CombineInstance { mesh = head, transform = root * Matrix4x4.TRS(phonePos + new Vector3(0f, 0.04f, 0.08f), Quaternion.identity, Vector3.one * 0.30f) });
                }
                else if (action < 0.36)
                {
                    Up(-1);
                    Up(1);
                }
                else if (action < 0.50)
                {
                    // Alkış: eller göğüs önünde buluşur
                    for (int side = -1; side <= 1; side += 2)
                        Arm(side, new Vector3(side * 0.12f, -0.8f, 0.45f), new Vector3(-side * 0.6f, 0.4f, 0.6f));
                }
                else if (action < 0.60)
                {
                    Up(1);
                    Down(-1);
                }
                else
                {
                    Down(-1);
                    Down(1);
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
