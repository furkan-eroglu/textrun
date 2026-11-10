using System;
using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;

namespace TextRun.Characters
{
    // Eşyaları karaktere basit şekillerden giydirir. Kimlikler sunucudaki liste ile aynıdır (server/src/economy.ts).
    // Gerçek 3B modeller gelince yalnızca bu dosya değişir; eşya kimlikleri ve sunucu aynı kalır.
    // Eşyasız karakter çıplaktır (düz ten rengi); botlar da öyle koşar.
    public static class Outfits
    {
        const string Prefix = "Cos_";
        static readonly Color Skin = CharacterBuilder.Skin;

        sealed class Ctx
        {
            public Transform head, visual, torso;
            public Transform[] arms = new Transform[2];    // 0 sol, 1 sağ
            public Transform[] legs = new Transform[2];
            public Transform[] feet = new Transform[2];
            public Transform[] armMeshes = new Transform[2];
            public Transform[] legMeshes = new Transform[2];

            public GameObject Add(PrimitiveType type, Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color, float rx = 0f, float ry = 0f, float rz = 0f, float smooth = 0.1f)
            {
                var go = Mats.Primitive(Prefix + type, type, parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), color);
                go.transform.localEulerAngles = new Vector3(rx, ry, rz);
                if (smooth != 0.1f) go.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, smooth);
                return go;
            }

            public void Dye(Transform part, Color color, float smooth = 0.1f)
            {
                if (part != null) part.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, smooth);
            }

            // Kol/bacak çevresine silindir kılıf (kısa kol, şort paçası, bilek bandı vb.)
            public void Sleeve(Transform limb, float centerY, float halfLength, float width, Color color, float smooth = 0.1f)
            {
                Add(PrimitiveType.Cylinder, limb, 0f, centerY, 0f, width, halfLength, width, color, 0f, 0f, 0f, smooth);
            }

            public void BothSides(Action<float, int> action)
            {
                action(-1f, 0);
                action(1f, 1);
            }
        }

        static Color C(int hex) => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);

        const PrimitiveType Cube = PrimitiveType.Cube;
        const PrimitiveType Ball = PrimitiveType.Sphere;
        const PrimitiveType Tube = PrimitiveType.Cylinder;

        static readonly Dictionary<string, Action<Ctx>> Registry = new Dictionary<string, Action<Ctx>>
        {
            // ---------- Kafa (şapka, saç, gözlük) ----------
            { "hat_cap", c => { Dome(c, C(0xD93636), 0.64f); c.Add(Cube, c.head, 0f, 0.13f, 0.31f, 0.34f, 0.03f, 0.2f, C(0xB02020), 8f); c.Add(Ball, c.head, 0f, 0.37f, 0f, 0.07f, 0.07f, 0.07f, C(0xFFFFFF)); } },
            { "hat_beanie", c => { Dome(c, C(0x4A72C8), 0.68f, 0.42f); c.Add(Ball, c.head, 0f, 0.42f, 0f, 0.13f, 0.13f, 0.13f, C(0xFFFFFF)); c.Add(Tube, c.head, 0f, 0.1f, 0f, 0.66f, 0.03f, 0.66f, C(0xE8EDF5)); } },
            { "hair_afro", c => c.Add(Ball, c.head, 0f, 0.28f, -0.14f, 0.85f, 0.85f, 0.85f, C(0x1E140E)) },
            { "hat_cowboy", c => { c.Add(Tube, c.head, 0f, 0.17f, 0f, 0.96f, 0.02f, 0.96f, C(0x8A5A2B)); c.Add(Tube, c.head, 0f, 0.29f, 0f, 0.42f, 0.13f, 0.42f, C(0x9C6A36)); c.Add(Tube, c.head, 0f, 0.2f, 0f, 0.44f, 0.025f, 0.44f, C(0x4B2E14)); } },
            { "glasses_sun", c => { c.Add(Cube, c.head, -0.12f, 0.05f, 0.335f, 0.2f, 0.12f, 0.025f, C(0x14141A), 0f, 0f, 0f, 0.7f); c.Add(Cube, c.head, 0.12f, 0.05f, 0.335f, 0.2f, 0.12f, 0.025f, C(0x14141A), 0f, 0f, 0f, 0.7f); c.Add(Cube, c.head, 0f, 0.07f, 0.335f, 0.06f, 0.025f, 0.025f, C(0x14141A)); c.Add(Cube, c.head, 0f, 0.07f, 0.1f, 0.6f, 0.025f, 0.025f, C(0x14141A)); } },
            { "hat_street", c => { Dome(c, C(0x1E2026), 0.64f); c.Add(Cube, c.head, 0f, 0.14f, 0.33f, 0.4f, 0.02f, 0.26f, C(0x35E060), 6f); c.Add(Cube, c.head, 0.2f, 0.26f, 0.2f, 0.05f, 0.05f, 0.05f, C(0x35E060)); } },
            { "hat_pop", c => { c.Add(Tube, c.head, 0f, 0.18f, 0f, 0.86f, 0.02f, 0.86f, C(0xFF4FA3)); c.Add(Tube, c.head, 0f, 0.31f, 0f, 0.44f, 0.12f, 0.44f, C(0xFF6FB5)); c.Add(Tube, c.head, 0f, 0.22f, 0f, 0.46f, 0.03f, 0.46f, C(0xFFD23C)); c.Add(Cube, c.head, 0.15f, 0.34f, 0.22f, 0.1f, 0.1f, 0.02f, C(0xFFD23C), 0f, 0f, 45f, 0.6f); } },
            { "hair_mohawk", c => { var green = C(0x3BE070); float[] h = { 0.14f, 0.2f, 0.26f, 0.22f, 0.16f }; for (int i = 0; i < 5; i++) c.Add(Cube, c.head, 0f, 0.28f + h[i] / 2f - 0.02f, 0.2f - i * 0.1f, 0.07f, h[i], 0.09f, i % 2 == 0 ? green : C(0xFF4FA3)); } },
            { "hat_pirate", c => { var black = C(0x1A1A22); Dome(c, black, 0.66f, 0.38f); c.Add(Cube, c.head, -0.2f, 0.3f, 0.02f, 0.5f, 0.2f, 0.3f, black, 0f, 0f, -18f); c.Add(Cube, c.head, 0.2f, 0.3f, 0.02f, 0.5f, 0.2f, 0.3f, black, 0f, 0f, 18f); c.Add(Cube, c.head, 0f, 0.2f, 0.02f, 0.9f, 0.035f, 0.33f, C(0xFFCC33)); c.Add(Ball, c.head, 0f, 0.32f, 0.175f, 0.13f, 0.13f, 0.04f, C(0xF2F2F2)); c.Add(Cube, c.head, 0f, 0.3f, 0.185f, 0.2f, 0.028f, 0.02f, C(0xF2F2F2), 0f, 0f, 35f); c.Add(Cube, c.head, 0f, 0.3f, 0.185f, 0.2f, 0.028f, 0.02f, C(0xF2F2F2), 0f, 0f, -35f); } },
            { "hat_crown", c => { var gold = C(0xFFCC33); c.Add(Tube, c.head, 0f, 0.28f, 0f, 0.5f, 0.07f, 0.5f, gold, 0f, 0f, 0f, 0.6f); for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 2f / 5f; c.Add(Cube, c.head, Mathf.Sin(a) * 0.22f, 0.4f, Mathf.Cos(a) * 0.22f, 0.07f, 0.16f, 0.07f, gold, 0f, 0f, 0f, 0.6f); c.Add(Ball, c.head, Mathf.Sin(a) * 0.22f, 0.49f, Mathf.Cos(a) * 0.22f, 0.06f, 0.06f, 0.06f, i % 2 == 0 ? C(0xE23B3B) : C(0x3B7BE2)); } } },

            // ---------- Üst giyim ----------
            { "top_tee", c => { c.Torso(C(0x4AA8E8)); c.ShortSleeves(C(0x4AA8E8)); c.Decal(0f, 0.32f, 0.2f, 0.2f, C(0xFFFFFF)); } },
            { "top_tank", c => { c.Torso(C(0xF2F2F2)); c.Add(Cube, c.visual, 0f, 0.585f, 0f, 0.515f, 0.04f, 0.295f, C(0xE23B3B)); c.Add(Cube, c.visual, 0f, 0.02f, 0f, 0.515f, 0.04f, 0.295f, C(0xE23B3B)); c.Decal(0f, 0.3f, 0.07f, 0.26f, C(0xE23B3B)); } },
            { "top_hoodie", c => { c.Torso(C(0x4A4F5E)); c.LongSleeves(C(0x4A4F5E)); c.Add(Ball, c.visual, 0f, 0.62f, -0.12f, 0.42f, 0.22f, 0.24f, C(0x3C404D)); c.Decal(0f, 0.15f, 0.32f, 0.12f, C(0x383C48)); } },
            { "top_jersey", c => { c.Torso(C(0xD22E2E)); c.ShortSleeves(C(0xFFFFFF)); c.Decal(0f, 0.32f, 0.1f, 0.28f, C(0xFFFFFF)); c.Add(Cube, c.visual, 0f, 0.57f, 0f, 0.52f, 0.04f, 0.3f, C(0xFFFFFF)); } },
            { "top_stripe", c => { c.Torso(C(0x24376B)); c.LongSleeves(C(0x24376B)); foreach (float y in new[] { 0.12f, 0.3f, 0.48f }) c.Add(Cube, c.visual, 0f, y, 0f, 0.515f, 0.06f, 0.295f, C(0xF2F2F2)); } },
            { "top_jacket", c => { c.Torso(C(0x15151A), 0.5f); c.LongSleeves(C(0x15151A), 0.5f); c.Decal(0f, 0.3f, 0.035f, 0.6f, C(0xC0C4CC)); c.Add(Cube, c.visual, -0.18f, 0.55f, 0.14f, 0.12f, 0.1f, 0.02f, C(0x2A2A32), 0f, 0f, -20f); c.Add(Cube, c.visual, 0.18f, 0.55f, 0.14f, 0.12f, 0.1f, 0.02f, C(0x2A2A32), 0f, 0f, 20f); } },
            { "top_suit", c => { c.Torso(C(0x16161C)); c.LongSleeves(C(0x16161C)); c.Decal(0f, 0.3f, 0.15f, 0.6f, C(0xF4F4F4)); c.Add(Cube, c.visual, -0.045f, 0.55f, 0.155f, 0.08f, 0.08f, 0.02f, C(0xD62828), 0f, 0f, 45f); c.Add(Cube, c.visual, 0.045f, 0.55f, 0.155f, 0.08f, 0.08f, 0.02f, C(0xD62828), 0f, 0f, 45f); foreach (float y in new[] { 0.4f, 0.28f, 0.16f }) c.Add(Ball, c.visual, 0f, y, 0.152f, 0.035f, 0.035f, 0.02f, C(0x16161C)); } },
            { "top_hawaii", c => { c.Torso(C(0xFF9A3C)); c.ShortSleeves(C(0xFF9A3C)); float[] fx = { -0.15f, 0.12f, -0.05f, 0.16f, -0.17f, 0.03f }; float[] fy = { 0.48f, 0.42f, 0.3f, 0.2f, 0.14f, 0.1f }; int[] fc = { 0xFF4FA3, 0xFFFFFF, 0x3BD070, 0xFF4FA3, 0xFFFFFF, 0x3B9BE2 }; for (int i = 0; i < 6; i++) c.Add(Ball, c.visual, fx[i], fy[i], 0.142f, 0.09f, 0.09f, 0.02f, C(fc[i])); } },
            { "top_ninja", c => { c.Torso(C(0x20222C)); c.LongSleeves(C(0x20222C)); c.Add(Cube, c.visual, 0f, 0.1f, 0f, 0.53f, 0.08f, 0.31f, C(0xD62828)); c.Add(Cube, c.visual, 0f, 0.6f, 0f, 0.34f, 0.07f, 0.32f, C(0xD62828)); c.Add(Cube, c.visual, 0.1f, 0.5f, -0.19f, 0.09f, 0.3f, 0.03f, C(0xD62828), 14f); c.BothSides((s, i) => c.Sleeve(c.arms[i], -0.48f, 0.05f, 0.17f, C(0xD62828))); } },
            { "top_armor", c => { c.Torso(C(0xAEB4C2), 0.7f); c.LongSleeves(C(0x8A91A2), 0.7f); c.BothSides((s, i) => c.Add(Ball, c.arms[i], 0f, -0.02f, 0f, 0.26f, 0.2f, 0.26f, C(0xCFD4DE), 0f, 0f, 0f, 0.7f)); c.Add(Cube, c.visual, 0f, 0.3f, 0.145f, 0.34f, 0.42f, 0.02f, C(0xFFCC33), 0f, 0f, 0f, 0.7f); c.Add(Cube, c.visual, 0f, 0.3f, 0.155f, 0.1f, 0.3f, 0.012f, C(0xD62828)); } },

            // ---------- Alt giyim ----------
            { "bottom_jeans", c => { c.Legs(C(0x2D4F9C)); c.Belt(C(0x5A3A1E)); } },
            { "bottom_shorts", c => { c.BothSides((s, i) => c.Sleeve(c.legs[i], -0.17f, 0.17f, 0.27f, C(0xFF7A1A))); c.Belt(C(0xFFFFFF)); } },
            { "bottom_track", c => { c.Legs(C(0x3A3F4E)); c.Belt(C(0x3A3F4E)); c.BothSides((s, i) => c.Add(Cube, c.legs[i], s * 0.105f, -0.38f, 0f, 0.025f, 0.7f, 0.07f, C(0xF2F2F2))); } },
            { "bottom_cargo", c => { c.Legs(C(0x6B7A3A)); c.Belt(C(0x3C2A14)); c.BothSides((s, i) => c.Add(Cube, c.legs[i], s * 0.12f, -0.32f, 0.02f, 0.05f, 0.16f, 0.14f, C(0x5A6830))); } },
            { "bottom_camo", c => { c.Legs(C(0x5E6B3C)); c.Belt(C(0x2E3320)); c.BothSides((s, i) => { c.Add(Ball, c.legs[i], 0.03f, -0.2f, 0.09f, 0.12f, 0.14f, 0.06f, C(0x3A4426)); c.Add(Ball, c.legs[i], -0.04f, -0.45f, 0.09f, 0.11f, 0.12f, 0.06f, C(0x8A8F5A)); c.Add(Ball, c.legs[i], 0.02f, -0.62f, 0.09f, 0.1f, 0.1f, 0.06f, C(0x3A4426)); c.Add(Ball, c.legs[i], s * 0.09f, -0.33f, 0f, 0.06f, 0.14f, 0.12f, C(0x8A8F5A)); }); } },
            { "bottom_suit", c => { c.Legs(C(0x1C2036)); c.Belt(C(0x0E0E14)); c.BothSides((s, i) => c.Add(Cube, c.legs[i], 0f, -0.3f, 0.12f, 0.02f, 0.5f, 0.012f, C(0x2E3350))); } },
            { "bottom_ripped", c => { c.Legs(C(0x6FA0DC)); c.Belt(C(0x16161C)); c.BothSides((s, i) => { c.Add(Cube, c.legs[i], 0f, -0.36f, 0.108f, 0.15f, 0.07f, 0.02f, Skin); c.Add(Cube, c.legs[i], 0.01f, -0.2f, 0.108f, 0.11f, 0.035f, 0.02f, Skin); }); } },
            { "bottom_ninja", c => { c.Legs(C(0x20222C)); c.Belt(C(0xD62828)); c.BothSides((s, i) => { c.Sleeve(c.legs[i], -0.6f, 0.05f, 0.25f, C(0xD62828)); c.Sleeve(c.legs[i], -0.25f, 0.2f, 0.27f, C(0x2A2D3A)); }); } },
            { "bottom_flame", c => { c.Legs(C(0x16161C)); c.Belt(C(0xFF7A1A)); c.BothSides((s, i) => { c.Sleeve(c.legs[i], -0.66f, 0.07f, 0.245f, C(0xE23B1A)); c.Sleeve(c.legs[i], -0.55f, 0.05f, 0.243f, C(0xFF8A1A)); c.Sleeve(c.legs[i], -0.47f, 0.035f, 0.241f, C(0xFFD23C)); }); } },
            { "bottom_gold", c => { c.Legs(C(0xFFCC33), 0.75f); c.Belt(C(0xFFF0A0)); } },

            // ---------- Ayakkabı ----------
            { "shoes_sneaker", c => c.Shoes(C(0xF4F4F4), C(0xD22E2E)) },
            { "shoes_sandal", c => c.BothSides((s, i) => { c.Add(Cube, c.legs[i], 0f, -0.805f, 0.06f, 0.22f, 0.04f, 0.38f, C(0xC89A5A)); c.Add(Cube, c.legs[i], 0f, -0.7f, 0.1f, 0.25f, 0.035f, 0.07f, C(0x2E7BD6)); c.Add(Cube, c.legs[i], 0f, -0.72f, -0.02f, 0.25f, 0.035f, 0.05f, C(0x2E7BD6)); }) },
            { "shoes_canvas", c => { c.Shoes(C(0x2B3F7A), C(0xF4F4F4)); c.BothSides((s, i) => c.Add(Ball, c.legs[i], 0f, -0.76f, 0.2f, 0.2f, 0.11f, 0.14f, C(0xF4F4F4))); } },
            { "shoes_boots", c => { c.Shoes(C(0x6B4220), C(0x2A1A0C)); c.BothSides((s, i) => c.Sleeve(c.legs[i], -0.64f, 0.12f, 0.27f, C(0x6B4220))); } },
            { "shoes_hightop", c => { c.Shoes(C(0xD22E2E), C(0xF4F4F4)); c.BothSides((s, i) => { c.Sleeve(c.legs[i], -0.66f, 0.07f, 0.26f, C(0xD22E2E)); c.Sleeve(c.legs[i], -0.6f, 0.015f, 0.265f, C(0xF4F4F4)); }); } },
            { "shoes_neon", c => c.Shoes(C(0x15151C), C(0x35FF90), 0.8f) },
            { "shoes_cowboy", c => { c.Shoes(C(0x8A5A2B), C(0x2A1A0C)); c.BothSides((s, i) => { c.Sleeve(c.legs[i], -0.56f, 0.18f, 0.27f, C(0x8A5A2B)); c.Sleeve(c.legs[i], -0.4f, 0.02f, 0.28f, C(0xE0CFA0)); c.Add(Ball, c.legs[i], 0f, -0.76f, -0.14f, 0.07f, 0.07f, 0.07f, C(0xFFCC33), 0f, 0f, 0f, 0.7f); }); } },
            { "shoes_rocket", c => { c.Shoes(C(0x8C93A5), C(0x4A4F5E)); c.BothSides((s, i) => { c.Add(Tube, c.legs[i], 0f, -0.72f, -0.17f, 0.12f, 0.1f, 0.12f, C(0xFF7A1A), 90f); c.Add(Ball, c.legs[i], 0f, -0.72f, -0.3f, 0.12f, 0.12f, 0.2f, C(0xFFD23C)); }); } },
            { "shoes_skate", c => { c.Shoes(C(0xFF6FB5), C(0xF4F4F4)); c.BothSides((s, i) => { c.Sleeve(c.legs[i], -0.66f, 0.06f, 0.26f, C(0xFF6FB5)); foreach (float z in new[] { -0.06f, 0.18f }) c.Add(Tube, c.legs[i], 0f, -0.845f, z, 0.1f, 0.06f, 0.1f, C(0xFFD23C), 0f, 0f, 90f); }); } },
            { "shoes_wing", c => { c.Shoes(C(0xFFFFFF), C(0xFFCC33), 0.6f); c.BothSides((s, i) => { c.Add(Cube, c.legs[i], s * 0.15f, -0.68f, -0.05f, 0.03f, 0.2f, 0.16f, C(0xFFFFFF), 0f, 0f, s * 22f); c.Add(Cube, c.legs[i], s * 0.17f, -0.6f, -0.05f, 0.03f, 0.14f, 0.12f, C(0xFFE9A0), 0f, 0f, s * 30f); }); } },

            // ---------- Pelerin ----------
            { "cape_red", c => c.Cape(0.1f, C(0xD62828)) },
            { "cape_blue", c => c.Cape(0.1f, C(0x2A5FD0)) },
            { "cape_green", c => { c.Cape(0.1f, C(0x2E7D46)); c.Add(Ball, c.visual, 0f, 0.64f, -0.13f, 0.44f, 0.24f, 0.26f, C(0x256638)); c.Add(Ball, c.visual, 0f, 0.56f, 0.15f, 0.08f, 0.08f, 0.04f, C(0x8A5A2B)); } },
            { "cape_striped", c => c.Cape(0.1f, C(0xF4F4F4), C(0xD62828), C(0xF4F4F4), C(0xD62828), C(0xF4F4F4), C(0xD62828)) },
            { "cape_hero", c => { c.Cape(0.1f, C(0xD62828)); c.CapeMark(0.36f, 0f, 0.2f, C(0xFFD23C), 45f); c.Add(Ball, c.visual, 0f, 0.56f, 0.15f, 0.09f, 0.09f, 0.04f, C(0xFFD23C), 0f, 0f, 0f, 0.6f); } },
            { "cape_night", c => { c.Cape(0.1f, C(0x161B3A)); float[] sx = { -0.16f, 0.12f, -0.04f, 0.18f, -0.18f, 0.05f }; float[] sd = { 0.12f, 0.2f, 0.36f, 0.46f, 0.56f, 0.68f }; for (int i = 0; i < 6; i++) c.CapeMark(sd[i], sx[i], 0.05f, C(0xFFE98A), 45f); c.CapeMark(0.3f, 0.15f, 0.1f, C(0xF4F4F4), 0f, true); } },
            { "cape_royal", c => { c.Cape(0.1f, C(0x6A2FB8)); c.Add(Cube, c.visual, 0f, 0.58f, -0.02f, 0.64f, 0.11f, 0.38f, C(0xF4F4F4)); foreach (float x in new[] { -0.22f, -0.07f, 0.08f, 0.23f }) c.Add(Ball, c.visual, x, 0.6f, 0.172f, 0.04f, 0.04f, 0.02f, C(0x16161C)); c.CapeBand(0.74f, 0.06f, C(0xFFCC33)); } },
            { "cape_fire", c => c.Cape(0.1f, C(0xB81E1E), C(0xD62828), C(0xF05A1A), C(0xFF8A1A), C(0xFFB52E), C(0xFFE03C)) },
            { "cape_wings", c => c.BothSides((s, i) => { c.Add(Cube, c.visual, s * 0.33f, 0.52f, -0.19f, 0.56f, 0.15f, 0.04f, C(0xFFFFFF), 0f, 0f, s * 28f); c.Add(Cube, c.visual, s * 0.3f, 0.36f, -0.2f, 0.46f, 0.13f, 0.04f, C(0xF2F6FF), 0f, 0f, s * 8f); c.Add(Cube, c.visual, s * 0.24f, 0.22f, -0.21f, 0.32f, 0.11f, 0.04f, C(0xE4ECFF), 0f, 0f, s * -14f); }) },
            { "cape_gold", c => { c.Cape(0.7f, C(0xFFCC33)); c.Add(Cube, c.visual, 0f, 0.58f, -0.02f, 0.6f, 0.07f, 0.36f, C(0xFFF0A0), 0f, 0f, 0f, 0.7f); c.CapeBand(0.74f, 0.06f, C(0xFFF0A0)); } },
        };

        // ---------- Ortak parçalar ----------

        // Kafayı saran yarım küre (şapka tepesi)
        static void Dome(Ctx c, Color color, float width, float height = 0.34f)
        {
            c.Add(Ball, c.head, 0f, 0.2f, -0.01f, width, height, width, color);
        }

        static void Torso(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.Dye(c.torso, color, smooth);
        }

        // Göğüsteki düz desen
        static void Decal(this Ctx c, float x, float y, float width, float height, Color color)
        {
            c.Add(Cube, c.visual, x, y, 0.145f, width, height, 0.012f, color);
        }

        static void ShortSleeves(this Ctx c, Color color)
        {
            c.BothSides((s, i) => c.Sleeve(c.arms[i], -0.1f, 0.1f, 0.2f, color));
        }

        static void LongSleeves(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.BothSides((s, i) => c.Dye(c.armMeshes[i], color, smooth));
        }

        static void Legs(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.BothSides((s, i) => c.Dye(c.legMeshes[i], color, smooth));
        }

        static void Belt(this Ctx c, Color color)
        {
            c.Add(Cube, c.visual, 0f, 0.02f, 0f, 0.53f, 0.07f, 0.3f, color);
        }

        static void Shoes(this Ctx c, Color body, Color sole, float smooth = 0.1f)
        {
            c.BothSides((s, i) =>
            {
                c.Dye(c.feet[i], body, smooth);
                c.Add(Ball, c.legs[i], 0f, -0.74f, 0.06f, 0.27f, 0.17f, 0.4f, body, 0f, 0f, 0f, smooth);
                c.Add(Cube, c.legs[i], 0f, -0.805f, 0.06f, 0.21f, 0.04f, 0.37f, sole, 0f, 0f, 0f, smooth);
            });
        }

        // Pelerin: omuzdan aşağı, sırttan hafifçe açılarak sarkan şeritler (renkler üstten alta) ve önde yaka bandı
        const float CapeLength = 0.82f;
        const float CapeTilt = 12f;
        const float CapeTop = 0.6f;
        const float CapeBack = -0.17f;

        static void Cape(this Ctx c, float smooth, params Color[] bands)
        {
            float each = CapeLength / bands.Length;
            for (int i = 0; i < bands.Length; i++) c.CapeBand(each * (i + 0.5f), each + 0.004f, bands[i], smooth);
            c.Add(Cube, c.visual, 0f, 0.585f, 0f, 0.54f, 0.05f, 0.32f, bands[0], 0f, 0f, 0f, smooth);
        }

        // Pelerinin üstünden "down" kadar aşağıda yatay bir şerit
        static void CapeBand(this Ctx c, float down, float height, Color color, float smooth = 0.1f)
        {
            float rad = CapeTilt * Mathf.Deg2Rad;
            c.Add(Cube, c.visual, 0f, CapeTop - down * Mathf.Cos(rad), CapeBack - down * Mathf.Sin(rad), 0.62f, height, 0.035f, color, CapeTilt, 0f, 0f, smooth);
        }

        // Pelerinin sırt yüzünde küçük bir işaret (kare/baklava ya da yuvarlak)
        static void CapeMark(this Ctx c, float down, float x, float size, Color color, float roll, bool round = false)
        {
            float rad = CapeTilt * Mathf.Deg2Rad;
            c.Add(round ? Ball : Cube, c.visual, x, CapeTop - down * Mathf.Cos(rad) - 0.02f * Mathf.Sin(rad), CapeBack - down * Mathf.Sin(rad) - 0.02f, size, size, 0.02f, color, CapeTilt, 0f, roll, 0.5f);
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
