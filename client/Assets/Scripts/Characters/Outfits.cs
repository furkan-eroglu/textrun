using System;
using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;

namespace TextRun.Characters
{
    // Eşyaları karaktere giydirir. Kimlikler sunucudaki liste ile aynıdır (server/src/economy.ts).
    // Modeller MeshKit ile kurulur: şapkalar kesitten döndürülür, giysiler gövde/kol/bacak kesitlerini izleyen bantlardır,
    // pelerinler kıvrımlı kumaş yüzeyidir. Eşyasız karakter çıplaktır (düz ten rengi); botlar da öyle koşar.
    public static class Outfits
    {
        const string Prefix = "Cos_";
        static readonly Color Skin = CharacterBuilder.Skin;
        static readonly float[] TorsoProfile = CharacterBuilder.TorsoProfile;
        static readonly float[] ArmProfile = CharacterBuilder.ArmProfile;
        static readonly float[] LegProfile = CharacterBuilder.LegProfile;
        const float TorsoDepth = CharacterBuilder.TorsoDepth;

        const PrimitiveType Ball = PrimitiveType.Sphere;
        const PrimitiveType Tube = PrimitiveType.Cylinder;

        sealed class Ctx
        {
            public Transform head, visual, torso;
            public Transform[] arms = new Transform[2];    // 0 sol, 1 sağ
            public Transform[] legs = new Transform[2];
            public Transform[] feet = new Transform[2];
            public Transform[] armMeshes = new Transform[2];
            public Transform[] legMeshes = new Transform[2];

            // Hazır şekil (küre, silindir)
            public GameObject Add(PrimitiveType type, Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color, float rx = 0f, float ry = 0f, float rz = 0f, float smooth = 0.1f)
            {
                var go = Mats.Primitive(Prefix + type, type, parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), color);
                go.transform.localEulerAngles = new Vector3(rx, ry, rz);
                if (smooth != 0.1f) go.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, smooth);
                return go;
            }

            public GameObject Put(Mesh mesh, Transform parent, float x, float y, float z, Color color, float rx = 0f, float ry = 0f, float rz = 0f, float smooth = 0.1f)
            {
                var go = Mats.MeshObject(Prefix + "Mesh", mesh, parent, new Vector3(x, y, z), Vector3.one, color);
                go.transform.localEulerAngles = new Vector3(rx, ry, rz);
                if (smooth != 0.1f) go.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, smooth);
                return go;
            }

            // Kesitten döndürülen biçim (yarıçap, yükseklik çiftleri; aşağıdan yukarı)
            public GameObject Lathe(Transform parent, float x, float y, float z, Color color, float smooth, params float[] profile)
            {
                return Put(MeshKit.Lathe(profile), parent, x, y, z, color, 0f, 0f, 0f, smooth);
            }

            public GameObject Box(Transform parent, float x, float y, float z, float w, float h, float d, float radius, Color color, float rx = 0f, float ry = 0f, float rz = 0f, float smooth = 0.1f)
            {
                return Put(MeshKit.RoundedBox(w, h, d, radius), parent, x, y, z, color, rx, ry, rz, smooth);
            }

            public GameObject Prism(Transform parent, float x, float y, float z, float depth, Color color, float rx, float ry, float rz, float smooth, float[] outline)
            {
                return Put(MeshKit.Prism(outline, depth), parent, x, y, z, color, rx, ry, rz, smooth);
            }

            public void Dye(Transform part, Color color, float smooth = 0.1f)
            {
                if (part != null) part.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, smooth);
            }

            public void BothSides(Action<float, int> action)
            {
                action(-1f, 0);
                action(1f, 1);
            }

            // Bir kesiti "grow" kadar dışarıdan izleyen bant (y0 < y1). Uçlar gövdeye kapanır.
            static float[] BandProfile(float[] profile, float y0, float y1, float grow)
            {
                const int steps = 7;
                var band = new float[(steps + 3) * 2];
                band[0] = Mathf.Max(0f, MeshKit.RadiusAt(profile, y0) - 0.004f); band[1] = y0;
                for (int i = 0; i <= steps; i++)
                {
                    float y = Mathf.Lerp(y0, y1, i / (float)steps);
                    band[(i + 1) * 2] = MeshKit.RadiusAt(profile, y) + grow;
                    band[(i + 1) * 2 + 1] = y;
                }
                band[(steps + 2) * 2] = Mathf.Max(0f, MeshKit.RadiusAt(profile, y1) - 0.004f); band[(steps + 2) * 2 + 1] = y1;
                return band;
            }

            // Gövdeyi saran bant (kemer, etek ucu, yaka, çizgi)
            public void TorsoBand(float y0, float y1, float grow, Color color, float smooth = 0.1f)
            {
                var go = Put(MeshKit.Lathe(BandProfile(TorsoProfile, y0, y1, grow)), visual, 0f, 0f, 0f, color, 0f, 0f, 0f, smooth);
                go.transform.localScale = new Vector3(1f, 1f, TorsoDepth);
            }

            // Kolu ya da bacağı saran bant (kol ağzı, paça, çizme koncu)
            public void LimbBand(Transform limb, float[] profile, float y0, float y1, float grow, Color color, float smooth = 0.1f)
            {
                Put(MeshKit.Lathe(BandProfile(profile, y0, y1, grow), 16), limb, 0f, 0f, 0f, color, 0f, 0f, 0f, smooth);
            }

            // Gövdenin ön yüzünün (x, y) noktasındaki derinliği
            public float Front(float x, float y)
            {
                float radius = MeshKit.RadiusAt(TorsoProfile, y);
                return TorsoDepth * Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
            }

            // Göğüste düz desen (yuvarlak köşeli)
            public void Chest(float x, float y, float w, float h, Color color, float smooth = 0.1f)
            {
                Box(visual, x, y, Front(x, y) + 0.002f, w, h, 0.018f, Mathf.Min(w, h) * 0.3f, color, 0f, 0f, 0f, smooth);
            }

            public void ChestPrism(float x, float y, Color color, float rz, float smooth, float[] outline)
            {
                Prism(visual, x, y, Front(x, y) + 0.004f, 0.016f, color, 0f, 0f, rz, smooth, outline);
            }
        }

        static Color C(int hex) => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);

        static readonly Dictionary<string, Action<Ctx>> Registry = new Dictionary<string, Action<Ctx>>
        {
            // ---------- Kafa (şapka, saç, gözlük) ----------
            { "hat_cap", c => { CapCrown(c, C(0xD93636)); c.Prism(c.head, 0f, 0.115f, 0.2f, 0.028f, C(0xB02020), 97f, 0f, 0f, 0.1f, MeshKit.Arc(0.215f, 0.27f, 0f, 180f)); c.Add(Ball, c.head, 0f, 0.392f, 0f, 0.07f, 0.035f, 0.07f, C(0xFFFFFF)); c.Box(c.head, 0f, 0.22f, 0.29f, 0.1f, 0.07f, 0.02f, 0.02f, C(0xFFFFFF), -20f); } },
            { "hat_beanie", c => { c.Lathe(c.head, 0f, 0f, 0f, C(0x4A72C8), 0.1f, 0.3f, 0.14f, 0.325f, 0.16f, 0.325f, 0.22f, 0.28f, 0.33f, 0.17f, 0.41f, 0.05f, 0.435f, 0f, 0.437f); c.Lathe(c.head, 0f, 0f, 0f, C(0xE8EDF5), 0.1f, 0.3f, 0.07f, 0.34f, 0.075f, 0.347f, 0.16f, 0.3f, 0.172f); c.Add(Ball, c.head, 0f, 0.47f, 0f, 0.15f, 0.15f, 0.15f, C(0xFFFFFF)); } },
            { "hair_afro", c => { var hair = C(0x1E140E); c.Add(Ball, c.head, 0f, 0.25f, -0.08f, 0.8f, 0.8f, 0.8f, hair); for (int i = 0; i < 22; i++) { float t = (i + 0.5f) / 22f; float up = 1f - 1.5f * t; float around = i * 2.39996f; float flat = Mathf.Sqrt(Mathf.Max(0f, 1f - up * up)); var p = new Vector3(Mathf.Cos(around) * flat, up, Mathf.Sin(around) * flat) * 0.35f + new Vector3(0f, 0.25f, -0.08f); if (p.z > 0.16f && p.y < 0.34f) continue; c.Add(Ball, c.head, p.x, p.y, p.z, 0.24f, 0.24f, 0.24f, hair); } } },
            { "hat_cowboy", c => { c.Lathe(c.head, 0f, 0f, 0f, C(0x8A5A2B), 0.1f, 0.29f, 0.165f, 0.4f, 0.15f, 0.5f, 0.175f, 0.545f, 0.24f, 0.535f, 0.255f, 0.49f, 0.2f, 0.4f, 0.175f, 0.29f, 0.19f); c.Lathe(c.head, 0f, 0f, 0f, C(0x9C6A36), 0.1f, 0.29f, 0.17f, 0.3f, 0.2f, 0.275f, 0.37f, 0.22f, 0.42f, 0.12f, 0.39f, 0f, 0.375f); c.Lathe(c.head, 0f, 0f, 0f, C(0x4B2E14), 0.1f, 0.3f, 0.195f, 0.306f, 0.2f, 0.3f, 0.25f, 0.29f, 0.25f); c.Add(Ball, c.head, 0f, 0.225f, 0.3f, 0.06f, 0.05f, 0.03f, C(0xFFCC33), 0f, 0f, 0f, 0.6f); } },
            { "glasses_sun", c => { var black = C(0x14141A); c.BothSides((s, i) => { c.Box(c.head, s * 0.122f, 0.05f, 0.338f, 0.2f, 0.135f, 0.03f, 0.045f, black, 0f, s * 8f, 0f, 0.8f); c.Box(c.head, s * 0.294f, 0.085f, 0.16f, 0.02f, 0.026f, 0.36f, 0.008f, black); c.Box(c.head, s * 0.15f, 0.085f, 0.356f, 0.05f, 0.02f, 0.008f, 0.004f, C(0xFFFFFF), 0f, s * 8f, 20f); }); c.Box(c.head, 0f, 0.075f, 0.345f, 0.08f, 0.026f, 0.026f, 0.01f, black); c.Box(c.head, 0f, 0.118f, 0.338f, 0.46f, 0.024f, 0.03f, 0.01f, black); } },
            { "hat_street", c => { CapCrown(c, C(0x1E2026)); c.Prism(c.head, 0f, 0.12f, 0.2f, 0.022f, C(0x1E2026), 91f, 0f, 0f, 0.1f, MeshKit.Arc(0.24f, 0.3f, 0f, 180f)); c.Prism(c.head, 0f, 0.108f, 0.2f, 0.01f, C(0x35E060), 91f, 0f, 0f, 0.1f, MeshKit.Arc(0.235f, 0.295f, 0f, 180f)); c.Box(c.head, 0f, 0.23f, 0.285f, 0.12f, 0.08f, 0.02f, 0.02f, C(0x35E060), -18f); c.Add(Ball, c.head, 0f, 0.392f, 0f, 0.07f, 0.035f, 0.07f, C(0x35E060)); } },
            { "hat_pop", c => { c.Lathe(c.head, 0f, 0f, 0f, C(0xFF4FA3), 0.1f, 0.29f, 0.17f, 0.43f, 0.16f, 0.45f, 0.175f, 0.43f, 0.19f, 0.29f, 0.195f); c.Lathe(c.head, 0f, 0f, 0f, C(0xFF6FB5), 0.1f, 0.29f, 0.18f, 0.295f, 0.2f, 0.265f, 0.38f, 0.2f, 0.425f, 0.1f, 0.39f, 0f, 0.38f); c.Lathe(c.head, 0f, 0f, 0f, C(0xFFD23C), 0.6f, 0.296f, 0.2f, 0.302f, 0.205f, 0.297f, 0.255f, 0.287f, 0.255f); c.Prism(c.head, 0.15f, 0.31f, 0.245f, 0.025f, C(0xFFE98A), -8f, 30f, 0f, 0.7f, MeshKit.Star(5, 0.075f)); } },
            { "hair_mohawk", c => { var fin = new List<float>(); for (int i = 0; i <= 8; i++) { float x = Mathf.Lerp(-0.26f, 0.28f, i / 8f); fin.Add(x); fin.Add(Mathf.Sqrt(0.09f - x * x) - 0.04f); } for (int i = 0; i < 6; i++) { float x = Mathf.Lerp(0.27f, -0.25f, i / 5f); float peak = 0.52f - Mathf.Abs(i - 2.2f) * 0.035f; fin.Add(x); fin.Add(peak); if (i < 5) { fin.Add(x - 0.052f); fin.Add(peak - 0.12f); } } c.Prism(c.head, 0f, 0f, 0f, 0.075f, C(0x3BE070), 0f, 90f, 0f, 0.1f, fin.ToArray()); c.Box(c.head, 0f, 0.268f, 0f, 0.1f, 0.05f, 0.5f, 0.02f, C(0xFF4FA3)); } },
            { "hat_pirate", c => { var black = C(0x1A1A22); float[] flap = { -0.52f, 0.14f, -0.47f, 0.27f, -0.3f, 0.4f, 0f, 0.47f, 0.3f, 0.4f, 0.47f, 0.27f, 0.52f, 0.14f, 0.25f, 0.11f, 0f, 0.1f, -0.25f, 0.11f }; float[] trim = { -0.5f, 0.165f, -0.3f, 0.385f, 0f, 0.452f, 0.3f, 0.385f, 0.5f, 0.165f, 0.47f, 0.25f, 0.3f, 0.36f, 0f, 0.425f, -0.3f, 0.36f, -0.47f, 0.25f }; c.Lathe(c.head, 0f, 0f, 0f, black, 0.1f, 0.3f, 0.12f, 0.312f, 0.16f, 0.27f, 0.3f, 0.15f, 0.37f, 0f, 0.385f); c.Prism(c.head, 0f, 0f, 0.2f, 0.035f, black, -12f, 0f, 0f, 0.1f, flap); c.Prism(c.head, 0f, 0f, -0.2f, 0.035f, black, 12f, 0f, 0f, 0.1f, flap); c.Add(Ball, c.head, 0f, 0.27f, 0.2f, 0.13f, 0.12f, 0.04f, C(0xF2F2F2)); c.Box(c.head, 0f, 0.26f, 0.21f, 0.22f, 0.028f, 0.02f, 0.01f, C(0xF2F2F2), -12f, 0f, 35f); c.Box(c.head, 0f, 0.26f, 0.21f, 0.22f, 0.028f, 0.02f, 0.01f, C(0xF2F2F2), -12f, 0f, -35f); c.BothSides((s, i) => c.Add(Ball, c.head, s * 0.025f, 0.28f, 0.222f, 0.03f, 0.035f, 0.02f, black)); } },
            { "hat_crown", c => { var gold = C(0xFFCC33); c.Lathe(c.head, 0f, 0f, 0f, gold, 0.7f, 0.285f, 0.19f, 0.302f, 0.195f, 0.312f, 0.3f, 0.296f, 0.3f, 0.285f, 0.2f); c.Lathe(c.head, 0f, 0f, 0f, C(0xB81E3C), 0.1f, 0.29f, 0.24f, 0.25f, 0.34f, 0.12f, 0.39f, 0f, 0.4f); for (int i = 0; i < 6; i++) { float a = i * 60f; float sin = Mathf.Sin(a * Mathf.Deg2Rad), cos = Mathf.Cos(a * Mathf.Deg2Rad); c.Prism(c.head, sin * 0.3f, 0.295f, cos * 0.3f, 0.03f, gold, 0f, a, 0f, 0.7f, new[] { -0.078f, 0f, 0.078f, 0f, 0f, 0.17f }); c.Add(Ball, c.head, sin * 0.3f, 0.47f, cos * 0.3f, 0.055f, 0.055f, 0.055f, i % 2 == 0 ? C(0xE23B3B) : C(0x3B7BE2), 0f, 0f, 0f, 0.7f); c.Add(Ball, c.head, Mathf.Sin((a + 30f) * Mathf.Deg2Rad) * 0.31f, 0.245f, Mathf.Cos((a + 30f) * Mathf.Deg2Rad) * 0.31f, 0.05f, 0.05f, 0.05f, i % 2 == 0 ? C(0x3BD070) : C(0xE23B3B), 0f, 0f, 0f, 0.7f); } } },

            // ---------- Üst giyim ----------
            { "top_tee", c => { var blue = C(0x4AA8E8); c.Torso(blue); c.ShortSleeves(blue); c.Collar(C(0x2F86C6)); c.Chest(0f, 0.36f, 0.17f, 0.17f, C(0xFFFFFF)); } },
            { "top_tank", c => { c.Torso(C(0xF2F2F2)); c.Collar(C(0xE23B3B)); c.TorsoBand(-0.03f, 0.03f, 0.012f, C(0xE23B3B)); c.Chest(0f, 0.3f, 0.06f, 0.24f, C(0xE23B3B)); c.Chest(-0.09f, 0.36f, 0.06f, 0.06f, C(0xE23B3B)); } },
            { "top_hoodie", c => { var gray = C(0x4A4F5E); var dark = C(0x383C48); c.Torso(gray); c.LongSleeves(gray); c.Cuffs(dark); c.TorsoBand(-0.03f, 0.04f, 0.014f, dark); c.Add(Ball, c.visual, 0f, 0.64f, -0.13f, 0.44f, 0.26f, 0.28f, dark); c.Chest(0f, 0.14f, 0.3f, 0.11f, dark); c.BothSides((s, i) => c.Box(c.visual, s * 0.05f, 0.47f, c.Front(0.05f, 0.47f) + 0.006f, 0.016f, 0.15f, 0.012f, 0.005f, C(0xE8EDF5))); } },
            { "top_jersey", c => { c.Torso(C(0xD22E2E)); c.ShortSleeves(C(0xFFFFFF)); c.Collar(C(0xFFFFFF)); c.TorsoBand(-0.03f, 0.02f, 0.012f, C(0xFFFFFF)); c.Chest(0f, 0.32f, 0.07f, 0.22f, C(0xFFFFFF)); c.Chest(-0.045f, 0.4f, 0.06f, 0.05f, C(0xFFFFFF)); } },
            { "top_stripe", c => { var navy = C(0x24376B); var white = C(0xF2F2F2); c.Torso(navy); c.LongSleeves(navy); c.Collar(white); foreach (float y in new[] { 0.1f, 0.27f, 0.44f }) c.TorsoBand(y, y + 0.065f, 0.01f, white); c.BothSides((s, i) => { c.LimbBand(c.arms[i], ArmProfile, -0.22f, -0.16f, 0.008f, white); c.LimbBand(c.arms[i], ArmProfile, -0.44f, -0.38f, 0.008f, white); }); } },
            { "top_jacket", c => { var black = C(0x15151A); var gray = C(0x2E2E38); c.Torso(black, 0.55f); c.LongSleeves(black, 0.55f); c.Collar(gray); c.Chest(0f, 0.28f, 0.028f, 0.5f, C(0xC0C4CC), 0.8f); c.BothSides((s, i) => { c.ChestPrism(s * 0.03f, 0.56f, gray, 0f, 0.3f, new[] { 0f, 0f, s * 0.15f, 0.04f, s * 0.05f, -0.2f }); c.Add(Ball, c.arms[i], 0f, 0.04f, 0f, 0.07f, 0.04f, 0.07f, C(0xC0C4CC), 0f, 0f, 0f, 0.8f); c.Box(c.visual, s * 0.13f, 0.12f, c.Front(0.13f, 0.12f) + 0.004f, 0.1f, 0.02f, 0.014f, 0.006f, C(0xC0C4CC), 0f, 0f, 0f, 0.8f); }); c.TorsoBand(-0.03f, 0.03f, 0.013f, gray); } },
            { "top_suit", c => { var black = C(0x16161C); c.Torso(black); c.LongSleeves(black); c.Cuffs(C(0xF4F4F4)); c.ChestPrism(0f, 0.36f, C(0xF4F4F4), 0f, 0.1f, new[] { -0.1f, 0.24f, 0.1f, 0.24f, 0f, -0.16f }); c.BothSides((s, i) => { c.ChestPrism(s * 0.02f, 0.58f, C(0x2A2A34), 0f, 0.4f, new[] { 0f, 0.02f, s * 0.13f, 0.02f, s * 0.02f, -0.36f }); c.Prism(c.visual, s * 0.006f, 0.565f, c.Front(0f, 0.565f) + 0.03f, 0.02f, C(0xD62828), 0f, 0f, 0f, 0.1f, new[] { 0f, 0f, s * 0.075f, 0.045f, s * 0.075f, -0.045f }); }); c.Add(Ball, c.visual, 0f, 0.565f, c.Front(0f, 0.565f) + 0.03f, 0.035f, 0.035f, 0.03f, C(0xA81C1C)); foreach (float y in new[] { 0.44f, 0.34f }) c.Add(Ball, c.visual, 0f, y, c.Front(0f, y) + 0.016f, 0.03f, 0.03f, 0.016f, black); } },
            { "top_hawaii", c => { var orange = C(0xFF9A3C); c.Torso(orange); c.ShortSleeves(orange); c.Collar(C(0xFFB866)); float[] fx = { -0.15f, 0.12f, -0.04f, 0.16f, -0.16f, 0.04f, 0.1f, -0.1f, 0.14f, -0.02f }; float[] fy = { 0.48f, 0.44f, 0.33f, 0.24f, 0.17f, 0.1f, 0.5f, 0.38f, 0.2f, 0.12f }; int[] fc = { 0xFF4FA3, 0xFFFFFF, 0x3BD070, 0xFF4FA3, 0xFFFFFF, 0x3B9BE2 }; for (int i = 0; i < 10; i++) { bool back = i >= 6; float z = c.Front(fx[i], fy[i]) + 0.002f; c.Prism(c.visual, fx[i], fy[i], back ? -z : z, 0.014f, C(fc[i % 6]), 0f, 0f, i * 37f, 0.1f, MeshKit.Star(5, 0.05f, 0.6f)); c.Add(Ball, c.visual, fx[i], fy[i], back ? -z - 0.006f : z + 0.006f, 0.03f, 0.03f, 0.012f, C(0xFFE03C)); } } },
            { "top_ninja", c => { var dark = C(0x20222C); var red = C(0xD62828); c.Torso(dark); c.LongSleeves(dark); c.TorsoBand(0.05f, 0.14f, 0.015f, red); c.Add(Ball, c.visual, 0.09f, 0.095f, c.Front(0.09f, 0.1f) + 0.02f, 0.07f, 0.07f, 0.05f, red); c.Box(c.visual, 0.11f, 0f, c.Front(0.11f, 0.02f) + 0.016f, 0.045f, 0.16f, 0.014f, 0.006f, red, 0f, 0f, 8f); c.TorsoBand(0.555f, 0.66f, 0.022f, red); c.Box(c.visual, 0.08f, 0.44f, -0.2f, 0.08f, 0.34f, 0.02f, 0.008f, red, 13f, 0f, -6f); c.Box(c.visual, -0.02f, 0.47f, -0.19f, 0.07f, 0.26f, 0.02f, 0.008f, C(0xB81E1E), 16f, 0f, 9f); c.BothSides((s, i) => c.LimbBand(c.arms[i], ArmProfile, -0.57f, -0.44f, 0.011f, red)); c.ChestPrism(0f, 0.4f, C(0x2E313E), 0f, 0.1f, new[] { -0.12f, 0.16f, 0.12f, 0.16f, 0f, -0.14f }); } },
            { "top_armor", c => { var steel = C(0xAEB4C2); var light = C(0xD5DAE4); var gold = C(0xFFCC33); c.Torso(steel, 0.75f); c.LongSleeves(C(0x7C8394), 0.7f); c.TorsoBand(0.27f, 0.6f, 0.02f, light, 0.8f); c.TorsoBand(0.245f, 0.275f, 0.026f, gold, 0.8f); c.TorsoBand(-0.03f, 0.06f, 0.018f, C(0x5A6072), 0.6f); c.TorsoBand(0.585f, 0.65f, 0.03f, steel, 0.75f); c.ChestPrism(0f, 0.43f, gold, 0f, 0.8f, MeshKit.Star(4, 0.1f, 0.4f)); c.Add(Ball, c.visual, 0f, 0.43f, c.Front(0f, 0.43f) + 0.03f, 0.05f, 0.05f, 0.03f, C(0xD62828), 0f, 0f, 0f, 0.8f); c.BothSides((s, i) => { c.Lathe(c.arms[i], s * 0.01f, -0.02f, 0f, light, 0.8f, 0.125f, -0.05f, 0.135f, 0f, 0.115f, 0.07f, 0.06f, 0.105f, 0f, 0.112f); c.LimbBand(c.arms[i], ArmProfile, -0.06f, -0.03f, 0.05f, gold, 0.8f); c.LimbBand(c.arms[i], ArmProfile, -0.58f, -0.38f, 0.016f, steel, 0.75f); c.LimbBand(c.arms[i], ArmProfile, -0.41f, -0.38f, 0.022f, gold, 0.8f); }); } },

            // ---------- Alt giyim ----------
            { "bottom_jeans", c => { c.Pants(C(0x2D4F9C)); c.Belt(C(0x5A3A1E)); c.BothSides((s, i) => c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.685f, 0.01f, C(0x5C7FC8))); } },
            { "bottom_shorts", c => { var orange = C(0xFF7A1A); c.Waist(orange); c.TorsoBand(0.06f, 0.1f, 0.016f, C(0xFFFFFF)); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.36f, 0.04f, 0.022f, orange); c.Box(c.legs[i], s * 0.142f, -0.16f, 0f, 0.016f, 0.34f, 0.07f, 0.006f, C(0xFFFFFF), 0f, 0f, s * -2f); }); } },
            { "bottom_track", c => { var gray = C(0x3A3F4E); c.Pants(gray); c.TorsoBand(0.06f, 0.1f, 0.016f, C(0x23262F)); c.BothSides((s, i) => { c.Box(c.legs[i], s * 0.108f, -0.37f, 0f, 0.02f, 0.68f, 0.06f, 0.008f, C(0xF2F2F2), 0f, 0f, s * -3f); c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.68f, 0.013f, C(0x23262F)); }); } },
            { "bottom_cargo", c => { var olive = C(0x6B7A3A); var dark = C(0x56642C); c.Pants(olive); c.Belt(C(0x3C2A14)); c.BothSides((s, i) => { c.Box(c.legs[i], s * 0.118f, -0.3f, 0.01f, 0.05f, 0.16f, 0.14f, 0.018f, dark); c.Box(c.legs[i], s * 0.124f, -0.235f, 0.01f, 0.05f, 0.045f, 0.15f, 0.012f, C(0x4A5726)); c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.69f, 0.012f, dark); }); } },
            { "bottom_camo", c => { c.Pants(C(0x5E6B3C)); c.Belt(C(0x2E3320)); c.BothSides((s, i) => { c.Add(Ball, c.legs[i], 0.03f, -0.2f, 0.085f, 0.12f, 0.14f, 0.07f, C(0x3A4426)); c.Add(Ball, c.legs[i], -0.04f, -0.44f, 0.07f, 0.1f, 0.12f, 0.06f, C(0x8A8F5A)); c.Add(Ball, c.legs[i], 0.02f, -0.62f, 0.06f, 0.09f, 0.1f, 0.05f, C(0x3A4426)); c.Add(Ball, c.legs[i], s * 0.085f, -0.33f, 0f, 0.07f, 0.14f, 0.12f, C(0x8A8F5A)); c.Add(Ball, c.legs[i], -0.02f, -0.3f, -0.085f, 0.12f, 0.14f, 0.06f, C(0x3A4426)); c.Add(Ball, c.legs[i], 0.03f, -0.55f, -0.065f, 0.09f, 0.11f, 0.05f, C(0x8A8F5A)); }); } },
            { "bottom_suit", c => { var navy = C(0x1C2036); c.Pants(navy); c.Belt(C(0x0E0E14)); c.BothSides((s, i) => { c.Box(c.legs[i], s * 0.106f, -0.37f, 0f, 0.014f, 0.7f, 0.04f, 0.005f, C(0x3A4066), 0f, 0f, s * -3f, 0.6f); c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.7f, 0.011f, C(0x12152A)); }); } },
            { "bottom_ripped", c => { c.Pants(C(0x6FA0DC)); c.Belt(C(0x16161C)); c.BothSides((s, i) => { c.Box(c.legs[i], 0f, -0.36f, 0.092f, 0.13f, 0.07f, 0.03f, 0.014f, Skin, -4f); c.Box(c.legs[i], 0.01f, -0.2f, 0.108f, 0.1f, 0.035f, 0.03f, 0.012f, Skin); c.Box(c.legs[i], 0f, -0.33f, 0.1f, 0.14f, 0.008f, 0.024f, 0.003f, C(0xDCE8F8)); c.Box(c.legs[i], 0f, -0.39f, 0.097f, 0.14f, 0.008f, 0.024f, 0.003f, C(0xDCE8F8)); c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.7f, 0.011f, C(0x8DB6EA)); }); } },
            { "bottom_ninja", c => { var dark = C(0x20222C); var red = C(0xD62828); c.Pants(dark); c.TorsoBand(0.04f, 0.12f, 0.018f, red); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.52f, 0.03f, 0.03f, C(0x2A2D3A)); c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.56f, 0.012f, red); c.LimbBand(c.legs[i], LegProfile, -0.69f, -0.66f, 0.016f, C(0x8A1414)); c.LimbBand(c.legs[i], LegProfile, -0.62f, -0.59f, 0.016f, C(0x8A1414)); }); } },
            { "bottom_flame", c => { c.Pants(C(0x16161C)); c.Belt(C(0xFF7A1A)); int[] fire = { 0xE23B1A, 0xFF8A1A, 0xFFD23C }; c.BothSides((s, i) => { for (int k = 0; k < 9; k++) { float a = k * 40f; float radius = MeshKit.RadiusAt(LegProfile, -0.66f) + 0.006f; c.Prism(c.legs[i], Mathf.Sin(a * Mathf.Deg2Rad) * radius, -0.755f, Mathf.Cos(a * Mathf.Deg2Rad) * radius, 0.014f, C(fire[k % 3]), 0f, a, 0f, 0.3f, new[] { -0.036f, 0f, 0.036f, 0f, 0.02f, 0.1f, 0.004f, 0.16f + (k % 3) * 0.04f, -0.018f, 0.08f }); } }); } },
            { "bottom_gold", c => { var gold = C(0xFFCC33); c.Pants(gold, 0.8f); c.TorsoBand(0.07f, 0.115f, 0.017f, C(0xFFF0A0), 0.8f); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.745f, -0.69f, 0.012f, C(0xFFF0A0), 0.8f); c.Prism(c.legs[i], s * 0.118f, -0.3f, 0f, 0.012f, C(0xFFF7CC), 0f, s * 90f, 0f, 0.8f, MeshKit.Star(4, 0.05f, 0.4f)); }); } },

            // ---------- Ayakkabı ----------
            { "shoes_sneaker", c => { c.Shoes(C(0xF4F4F4), C(0xD22E2E)); c.BothSides((s, i) => { c.Box(c.legs[i], 0f, -0.725f, 0.02f, 0.206f, 0.04f, 0.09f, 0.012f, C(0xD22E2E)); for (int k = 0; k < 3; k++) c.Box(c.legs[i], 0f, -0.672f, 0.1f + k * 0.045f, 0.1f, 0.014f, 0.022f, 0.005f, C(0xC8CCD4)); }); } },
            { "shoes_sandal", c => c.BothSides((s, i) => { c.Box(c.legs[i], 0f, -0.8f, 0.075f, 0.2f, 0.04f, 0.38f, 0.018f, C(0xC89A5A)); c.Box(c.legs[i], 0f, -0.7f, 0.15f, 0.2f, 0.035f, 0.06f, 0.014f, C(0x2E7BD6)); c.Box(c.legs[i], 0f, -0.7f, 0.02f, 0.2f, 0.035f, 0.05f, 0.014f, C(0x2E7BD6)); c.Box(c.legs[i], s * 0.095f, -0.74f, 0.085f, 0.02f, 0.09f, 0.2f, 0.008f, C(0x2E7BD6)); c.Box(c.legs[i], s * -0.095f, -0.74f, 0.085f, 0.02f, 0.09f, 0.2f, 0.008f, C(0x2E7BD6)); }) },
            { "shoes_canvas", c => { c.Shoes(C(0x2B3F7A), C(0xF4F4F4)); c.BothSides((s, i) => { c.Box(c.legs[i], 0f, -0.745f, 0.205f, 0.19f, 0.1f, 0.11f, 0.045f, C(0xF4F4F4)); c.Add(Ball, c.legs[i], s * -0.1f, -0.69f, 0.02f, 0.02f, 0.06f, 0.06f, C(0xF4F4F4)); }); } },
            { "shoes_boots", c => { var brown = C(0x6B4220); c.Shoes(brown, C(0x2A1A0C)); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.72f, -0.5f, 0.03f, brown); c.LimbBand(c.legs[i], LegProfile, -0.53f, -0.48f, 0.042f, C(0x8A5A2B)); c.Box(c.legs[i], 0f, -0.805f, 0.075f, 0.22f, 0.05f, 0.4f, 0.014f, C(0x2A1A0C)); }); } },
            { "shoes_hightop", c => { var red = C(0xD22E2E); c.Shoes(red, C(0xF4F4F4)); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.72f, -0.57f, 0.024f, red); c.LimbBand(c.legs[i], LegProfile, -0.6f, -0.57f, 0.03f, C(0xF4F4F4)); c.Box(c.legs[i], 0f, -0.745f, 0.205f, 0.19f, 0.1f, 0.11f, 0.045f, C(0xF4F4F4)); c.Prism(c.legs[i], s * 0.103f, -0.66f, 0f, 0.012f, C(0xF4F4F4), 0f, s * 90f, 0f, 0.1f, MeshKit.Star(5, 0.04f)); }); } },
            { "shoes_neon", c => { var neon = C(0x35FF90); c.Shoes(C(0x15151C), neon, 0.85f); c.BothSides((s, i) => { c.Box(c.legs[i], 0f, -0.745f, 0.09f, 0.206f, 0.02f, 0.24f, 0.008f, neon, 0f, 0f, 0f, 0.85f); c.Box(c.legs[i], 0f, -0.7f, -0.1f, 0.06f, 0.08f, 0.02f, 0.008f, neon, 0f, 0f, 0f, 0.85f); }); } },
            { "shoes_cowboy", c => { var brown = C(0x8A5A2B); c.Shoes(brown, C(0x2A1A0C)); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.72f, -0.38f, 0.028f, brown); c.LimbBand(c.legs[i], LegProfile, -0.41f, -0.36f, 0.04f, C(0xE0CFA0)); c.Box(c.legs[i], 0f, -0.79f, -0.07f, 0.15f, 0.07f, 0.09f, 0.014f, C(0x2A1A0C)); c.Add(Ball, c.legs[i], 0f, -0.75f, 0.24f, 0.13f, 0.09f, 0.12f, brown); c.Prism(c.legs[i], 0f, -0.76f, -0.14f, 0.012f, C(0xFFCC33), 0f, 90f, 0f, 0.8f, MeshKit.Star(6, 0.045f)); c.Prism(c.legs[i], s * 0.125f, -0.5f, 0f, 0.012f, C(0xE0CFA0), 0f, s * 90f, 0f, 0.1f, MeshKit.Star(5, 0.045f)); }); } },
            { "shoes_rocket", c => { c.Shoes(C(0x8C93A5), C(0x4A4F5E), 0.75f); c.BothSides((s, i) => { c.Put(MeshKit.Lathe(new[] { 0.04f, 0f, 0.075f, 0.11f, 0.06f, 0.11f, 0.03f, 0.01f }), c.legs[i], 0f, -0.735f, -0.1f, C(0x3A3F4E), -90f, 0f, 0f, 0.7f); c.Put(MeshKit.Lathe(new[] { 0f, 0.05f, 0.05f, 0.08f, 0.035f, 0.2f, 0f, 0.31f }), c.legs[i], 0f, -0.735f, -0.1f, C(0xFF7A1A), -90f); c.Put(MeshKit.Lathe(new[] { 0f, 0.07f, 0.028f, 0.1f, 0.018f, 0.17f, 0f, 0.23f }), c.legs[i], 0f, -0.735f, -0.11f, C(0xFFE03C), -90f); c.Prism(c.legs[i], s * 0.105f, -0.72f, -0.02f, 0.014f, C(0xD62828), 0f, s * 90f, 0f, 0.6f, new[] { -0.09f, -0.05f, 0.06f, -0.05f, -0.09f, 0.07f }); c.Box(c.legs[i], 0f, -0.69f, 0.12f, 0.12f, 0.02f, 0.12f, 0.008f, C(0x35C8FF), 0f, 0f, 0f, 0.9f); }); } },
            { "shoes_skate", c => { var pink = C(0xFF6FB5); c.Shoes(pink, C(0xF4F4F4)); c.BothSides((s, i) => { c.LimbBand(c.legs[i], LegProfile, -0.72f, -0.58f, 0.022f, pink); c.LimbBand(c.legs[i], LegProfile, -0.61f, -0.58f, 0.028f, C(0xF4F4F4)); foreach (float z in new[] { -0.045f, 0.2f }) foreach (float x in new[] { -0.118f, 0.118f }) { c.Add(Tube, c.legs[i], x, -0.762f, z, 0.08f, 0.016f, 0.08f, C(0xFFD23C), 0f, 0f, 90f); c.Add(Tube, c.legs[i], x, -0.762f, z, 0.035f, 0.02f, 0.035f, C(0xF4F4F4), 0f, 0f, 90f); } c.Add(Ball, c.legs[i], 0f, -0.77f, 0.28f, 0.07f, 0.07f, 0.07f, C(0x35C8FF)); }); } },
            { "shoes_wing", c => { c.Shoes(C(0xFFFFFF), C(0xFFCC33), 0.7f); float[] feather = { 0f, 0f, 0.032f, 0.06f, 0.024f, 0.17f, 0f, 0.23f, -0.024f, 0.17f, -0.032f, 0.06f }; c.BothSides((s, i) => { for (int k = 0; k < 3; k++) { var go = c.Prism(c.legs[i], s * (0.11f + k * 0.006f), -0.7f, -0.03f, 0.012f, k == 1 ? C(0xFFE9A0) : C(0xFFFFFF), 0f, 90f, -28f - k * 26f, 0.4f, feather); go.transform.localScale = new Vector3(1f, 1f - k * 0.18f, 1f); } c.Add(Ball, c.legs[i], s * 0.11f, -0.7f, -0.03f, 0.03f, 0.06f, 0.06f, C(0xFFCC33), 0f, 0f, 0f, 0.7f); }); } },

            // ---------- Pelerin ----------
            { "cape_red", c => c.Cape(0.1f, C(0xD62828)) },
            { "cape_blue", c => c.Cape(0.1f, C(0x2A5FD0)) },
            { "cape_green", c => { c.Cape(0.1f, C(0x2E7D46)); c.Add(Ball, c.visual, 0f, 0.65f, -0.13f, 0.46f, 0.26f, 0.28f, C(0x256638)); c.Add(Ball, c.visual, 0f, 0.575f, c.Front(0f, 0.575f) + 0.03f, 0.07f, 0.07f, 0.04f, C(0x8A5A2B)); } },
            { "cape_striped", c => c.Cape(0.1f, C(0xF4F4F4), C(0xD62828), C(0xF4F4F4), C(0xD62828), C(0xF4F4F4), C(0xD62828)) },
            { "cape_hero", c => { c.Cape(0.1f, C(0xD62828)); c.CapeMark(0f, 0.42f, C(0xFFD23C), 0.7f, MeshKit.Star(5, 0.13f)); c.Add(Ball, c.visual, 0f, 0.575f, c.Front(0f, 0.575f) + 0.03f, 0.08f, 0.08f, 0.04f, C(0xFFD23C), 0f, 0f, 0f, 0.7f); } },
            { "cape_night", c => { c.Cape(0.1f, C(0x161B3A)); float[] su = { -0.6f, 0.45f, -0.15f, 0.65f, -0.7f, 0.2f, -0.35f }; float[] sv = { 0.16f, 0.24f, 0.4f, 0.52f, 0.62f, 0.74f, 0.86f }; for (int i = 0; i < 7; i++) c.CapeMark(su[i], sv[i], C(0xFFE98A), 0.6f, MeshKit.Star(5, 0.032f + (i % 3) * 0.008f)); c.CapeMark(0.4f, 0.4f, C(0xF4F4F4), 0.6f, MeshKit.Arc(0.07f, 0.07f, 70f, 290f)); } },
            { "cape_royal", c => { var white = C(0xF4F4F4); c.Cape(0.1f, C(0x6A2FB8), C(0x6A2FB8), C(0x6A2FB8), C(0x6A2FB8), C(0x6A2FB8), C(0x6A2FB8), C(0x6A2FB8), C(0xFFCC33), white, white); c.TorsoBand(0.535f, 0.645f, 0.04f, white); for (int i = 0; i < 9; i++) { float a = i * 40f * Mathf.Deg2Rad; float radius = MeshKit.RadiusAt(TorsoProfile, 0.59f) + 0.04f; c.Add(Ball, c.visual, Mathf.Sin(a) * radius, 0.59f, Mathf.Cos(a) * radius * TorsoDepth, 0.035f, 0.045f, 0.03f, C(0x16161C)); } foreach (float u in new[] { -0.75f, -0.4f, -0.05f, 0.3f, 0.65f }) c.CapeMark(u, 0.9f + (u > 0f ? 0.03f : 0f), C(0x16161C), 0.1f, MeshKit.Arc(0.016f, 0.024f, 0f, 360f, 8)); } },
            { "cape_fire", c => c.Cape(0.2f, C(0xB81E1E), C(0xD62828), C(0xF05A1A), C(0xFF8A1A), C(0xFFB52E), C(0xFFE03C)) },
            { "cape_wings", c => { float[] feather = { 0f, 0f, 0.065f, 0.1f, 0.055f, 0.34f, 0f, 0.47f, -0.055f, 0.34f, -0.065f, 0.1f }; int[] tone = { 0xFFFFFF, 0xF4F8FF, 0xE9F0FF, 0xDDE7FF, 0xD0DDFA }; c.BothSides((s, i) => { for (int k = 0; k < 5; k++) { var go = c.Prism(c.visual, s * (0.06f + k * 0.012f), 0.44f - k * 0.012f, -0.185f - k * 0.008f, 0.018f, C(tone[k]), 8f, 0f, -s * (38f + k * 27f), 0.3f, feather); go.transform.localScale = new Vector3(1f, 1.05f - k * 0.11f, 1f); } c.Add(Ball, c.visual, s * 0.06f, 0.44f, -0.18f, 0.1f, 0.12f, 0.06f, C(0xFFFFFF)); }); c.TorsoBand(0.42f, 0.46f, 0.012f, C(0xFFE9A0), 0.6f); } },
            { "cape_gold", c => { var light = C(0xFFF0A0); c.Cape(0.8f, C(0xFFCC33), C(0xFFCC33), C(0xFFCC33), C(0xFFCC33), C(0xFFCC33), C(0xFFCC33), C(0xFFCC33), C(0xFFCC33), light); c.TorsoBand(0.545f, 0.635f, 0.03f, light, 0.8f); c.CapeMark(0f, 0.42f, light, 0.8f, MeshKit.Star(8, 0.12f, 0.55f)); c.Add(Ball, c.visual, 0f, 0.575f, c.Front(0f, 0.575f) + 0.04f, 0.08f, 0.08f, 0.04f, C(0xE23B3B), 0f, 0f, 0f, 0.8f); } },
        };

        // ---------- Ortak parçalar ----------

        // Spor şapka tepesi (kafayı saran kubbe)
        static void CapCrown(Ctx c, Color color)
        {
            c.Lathe(c.head, 0f, 0f, 0f, color, 0.1f, 0.3f, 0.09f, 0.318f, 0.12f, 0.315f, 0.2f, 0.27f, 0.3f, 0.17f, 0.365f, 0.06f, 0.39f, 0f, 0.392f);
        }

        static void Torso(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.Dye(c.torso, color, smooth);
        }

        static void Collar(this Ctx c, Color color)
        {
            c.TorsoBand(0.58f, 0.645f, 0.012f, color);
        }

        static void ShortSleeves(this Ctx c, Color color)
        {
            c.BothSides((s, i) => c.LimbBand(c.arms[i], ArmProfile, -0.2f, 0.072f, 0.014f, color));
        }

        static void LongSleeves(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.BothSides((s, i) => c.Dye(c.armMeshes[i], color, smooth));
        }

        static void Cuffs(this Ctx c, Color color)
        {
            c.BothSides((s, i) => c.LimbBand(c.arms[i], ArmProfile, -0.6f, -0.53f, 0.012f, color));
        }

        // Pantolonun kalçayı saran üst kısmı
        static void Waist(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.TorsoBand(-0.03f, 0.09f, 0.011f, color, smooth);
        }

        static void Pants(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.BothSides((s, i) => c.Dye(c.legMeshes[i], color, smooth));
            c.Waist(color, smooth);
        }

        static void Belt(this Ctx c, Color color)
        {
            c.TorsoBand(0.07f, 0.115f, 0.017f, color);
            c.Box(c.visual, 0f, 0.092f, c.Front(0f, 0.09f) + 0.014f, 0.07f, 0.05f, 0.02f, 0.012f, C(0xFFCC33), 0f, 0f, 0f, 0.7f);
        }

        // Ayakkabının gövdesi, tabanı ve bilek kısmı
        static void Shoes(this Ctx c, Color body, Color sole, float smooth = 0.1f)
        {
            c.BothSides((s, i) =>
            {
                c.Dye(c.feet[i], body, smooth);
                c.Box(c.legs[i], 0f, -0.735f, 0.075f, 0.2f, 0.125f, 0.36f, 0.055f, body, 0f, 0f, 0f, smooth);
                c.Box(c.legs[i], 0f, -0.797f, 0.075f, 0.21f, 0.045f, 0.38f, 0.02f, sole, 0f, 0f, 0f, smooth);
                c.LimbBand(c.legs[i], LegProfile, -0.72f, -0.665f, 0.016f, body, smooth);
            });
        }

        // Pelerin yüzeyi: omuzları sarar, aşağı doğru genişler ve hafif kıvrımlarla sırttan açılarak sarkar
        static Vector3 CapePoint(float u, float v)
        {
            const float tilt = 12f * Mathf.Deg2Rad;
            float width = 0.5f + 0.22f * v;
            float down = v * 0.84f;
            float wrap = 0.07f * u * u * (1f - v) * (1f - v);
            float folds = -0.022f * Mathf.Cos(u * Mathf.PI * 2f) * v;
            return new Vector3(u * width / 2f, 0.6f - down * Mathf.Cos(tilt), -0.172f - down * Mathf.Sin(tilt) + wrap + folds);
        }

        // Renkler üstten alta eşit şeritlerdir (tek renk verilirse düz pelerin). Yakada bant ve toka bulunur.
        static void Cape(this Ctx c, float smooth, params Color[] bands)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                float v0 = i / (float)bands.Length, v1 = (i + 1) / (float)bands.Length;
                int rows = Mathf.Max(2, Mathf.RoundToInt(12f / bands.Length));
                c.Put(MeshKit.Sheet("cape", v0, v1, 14, rows, CapePoint), c.visual, 0f, 0f, 0f, bands[i], 0f, 0f, 0f, smooth);
            }
            c.TorsoBand(0.555f, 0.63f, 0.017f, bands[0], smooth);
        }

        // Pelerinin sırt yüzünde işaret (yıldız, ay, benek)
        static void CapeMark(this Ctx c, float u, float v, Color color, float smooth, float[] outline)
        {
            var p = CapePoint(u, v);
            c.Prism(c.visual, p.x, p.y, p.z - 0.008f, 0.014f, color, 12f, 0f, 0f, smooth, outline);
        }

        // ---------- Giydirme ----------

        // Karakteri verilen eşya kimlikleriyle giydirir. Önce önceki eşyalar kaldırılır; boş liste çıplak karakterdir.
        public static void Apply(CharacterAnimator character, IList<string> look)
        {
            if (character == null) return;
            var c = Gather(character);

            foreach (var t in character.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.StartsWith(Prefix))
                {
                    t.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(t.gameObject);
                }
            }
            ResetBase(c);

            if (look == null) return;
            foreach (var id in look)
                if (id != null && Registry.TryGetValue(id, out var build)) build(c);
        }

        public static string[] AllIds()
        {
            var ids = new string[Registry.Count];
            Registry.Keys.CopyTo(ids, 0);
            return ids;
        }

        public static bool HasLook(string id) => id != null && Registry.ContainsKey(id);

        // Eşya kimliğinden kategori: kafa eşyaları (şapka, saç, gözlük) tek kategoridir
        public static string CategoryOf(string id)
        {
            if (id == null) return "";
            if (id.StartsWith("hat_") || id.StartsWith("hair_") || id.StartsWith("glasses_")) return "head";
            int cut = id.IndexOf('_');
            return cut > 0 ? id.Substring(0, cut) : "";
        }

        static Ctx Gather(CharacterAnimator a)
        {
            var c = new Ctx { head = a.head, visual = a.visual, torso = a.visual.Find("Torso") };
            c.arms[0] = a.leftArm; c.arms[1] = a.rightArm;
            c.legs[0] = a.leftLeg; c.legs[1] = a.rightLeg;
            c.armMeshes[0] = a.leftArm.Find("LeftArmMesh"); c.armMeshes[1] = a.rightArm.Find("RightArmMesh");
            c.legMeshes[0] = a.leftLeg.Find("LeftLegMesh"); c.legMeshes[1] = a.rightLeg.Find("RightLegMesh");
            c.feet[0] = a.leftLeg.Find("LeftFoot"); c.feet[1] = a.rightLeg.Find("RightFoot");
            return c;
        }

        static void ResetBase(Ctx c)
        {
            c.Dye(c.torso, Skin);
            for (int i = 0; i < 2; i++)
            {
                c.Dye(c.armMeshes[i], Skin);
                c.Dye(c.legMeshes[i], Skin);
                c.Dye(c.feet[i], Skin);
            }
        }
    }
}
