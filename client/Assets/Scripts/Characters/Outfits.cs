using System;
using System.Collections.Generic;
using TextRun.Common;
using UnityEngine;

namespace TextRun.Characters
{
    // Eşyaları karaktere basit şekillerden giydirir. Kimlikler sunucudaki liste ile aynıdır (server/src/economy.ts).
    // Gerçek 3B modeller gelince yalnızca bu dosya değişir; eşya kimlikleri ve sunucu aynı kalır.
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

            // Kol/bacak çevresine silindir kılıf (kısa/uzun kol, şort paçası vb.)
            public void Sleeve(Transform limb, float centerY, float halfLength, float width, Color color)
            {
                Add(PrimitiveType.Cylinder, limb, 0f, centerY, 0f, width, halfLength, width, color);
            }

            public void BothSides(Action<float, int> action)
            {
                action(-1f, 0);
                action(1f, 1);
            }
        }

        static Color C(int hex) => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);

        static readonly Dictionary<string, Action<Ctx>> Registry = new Dictionary<string, Action<Ctx>>
        {
            // ---------- Baş ----------
            { "hat_cap", c => { Dome(c, C(0xD93636), 0.64f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.13f, 0.31f, 0.34f, 0.03f, 0.2f, C(0xB02020), 8f); c.Add(PrimitiveType.Sphere, c.head, 0f, 0.37f, 0f, 0.07f, 0.07f, 0.07f, C(0xFFFFFF)); } },
            { "hat_beanie", c => { Dome(c, C(0x4A72C8), 0.68f, 0.42f); c.Add(PrimitiveType.Sphere, c.head, 0f, 0.42f, 0f, 0.13f, 0.13f, 0.13f, C(0xFFFFFF)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.1f, 0f, 0.66f, 0.03f, 0.66f, C(0xE8EDF5)); } },
            { "hat_bucket", c => { Dome(c, C(0xE0CFA0), 0.62f, 0.34f); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.16f, 0f, 0.86f, 0.015f, 0.86f, C(0xC9B67F)); } },
            { "hat_cowboy", c => { c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.17f, 0f, 0.96f, 0.02f, 0.96f, C(0x8A5A2B)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.29f, 0f, 0.42f, 0.13f, 0.42f, C(0x9C6A36)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.2f, 0f, 0.44f, 0.025f, 0.44f, C(0x4B2E14)); } },
            { "hat_street", c => { Dome(c, C(0x1E2026), 0.64f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.14f, 0.33f, 0.4f, 0.02f, 0.26f, C(0x35E060), 6f); c.Add(PrimitiveType.Cube, c.head, 0.2f, 0.26f, 0.2f, 0.05f, 0.05f, 0.05f, C(0x35E060)); } },
            { "hat_pop", c => { c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.18f, 0f, 0.86f, 0.02f, 0.86f, C(0xFF4FA3)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.31f, 0f, 0.44f, 0.12f, 0.44f, C(0xFF6FB5)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.22f, 0f, 0.46f, 0.03f, 0.46f, C(0xFFD23C)); c.Add(PrimitiveType.Cube, c.head, 0.15f, 0.34f, 0.22f, 0.1f, 0.1f, 0.02f, C(0xFFD23C), 0f, 0f, 45f, 0.6f); } },
            { "hat_top", c => { c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.18f, 0f, 0.66f, 0.02f, 0.66f, C(0x14141A)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.36f, 0f, 0.42f, 0.17f, 0.42f, C(0x1C1C24)); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.24f, 0f, 0.44f, 0.03f, 0.44f, C(0x8A3FD4)); c.Add(PrimitiveType.Sphere, c.head, 0.2f, 0.28f, 0.15f, 0.07f, 0.07f, 0.07f, C(0xFFD23C), 0f, 0f, 0f, 0.6f); } },
            { "hat_pirate", c => { Dome(c, C(0x16161C), 0.66f, 0.36f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.27f, 0f, 0.86f, 0.14f, 0.34f, C(0x1C1C24), 0f, 0f, 0f); c.Add(PrimitiveType.Sphere, c.head, 0f, 0.28f, 0.17f, 0.11f, 0.11f, 0.03f, C(0xF2F2F2)); c.Add(PrimitiveType.Cube, c.head, 0f, 0.28f, 0.19f, 0.15f, 0.025f, 0.02f, C(0xF2F2F2), 0f, 0f, 35f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.28f, 0.19f, 0.15f, 0.025f, 0.02f, C(0xF2F2F2), 0f, 0f, -35f); } },
            { "hat_crown", c => { var gold = C(0xFFCC33); c.Add(PrimitiveType.Cylinder, c.head, 0f, 0.28f, 0f, 0.5f, 0.07f, 0.5f, gold, 0f, 0f, 0f, 0.6f); for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 2f / 5f; c.Add(PrimitiveType.Cube, c.head, Mathf.Sin(a) * 0.22f, 0.4f, Mathf.Cos(a) * 0.22f, 0.07f, 0.16f, 0.07f, gold, 0f, 0f, 0f, 0.6f); c.Add(PrimitiveType.Sphere, c.head, Mathf.Sin(a) * 0.22f, 0.49f, Mathf.Cos(a) * 0.22f, 0.06f, 0.06f, 0.06f, i % 2 == 0 ? C(0xE23B3B) : C(0x3B7BE2)); } } },

            { "hair_short", c => Dome(c, C(0x3A2412), 0.64f) },
            { "hair_long", c => { Dome(c, C(0xE0B040), 0.64f); c.Add(PrimitiveType.Cube, c.head, 0f, -0.06f, -0.2f, 0.62f, 0.66f, 0.14f, C(0xE0B040)); c.Add(PrimitiveType.Cube, c.head, -0.3f, 0f, -0.02f, 0.06f, 0.5f, 0.2f, C(0xE0B040)); c.Add(PrimitiveType.Cube, c.head, 0.3f, 0f, -0.02f, 0.06f, 0.5f, 0.2f, C(0xE0B040)); } },
            { "hair_afro", c => c.Add(PrimitiveType.Sphere, c.head, 0f, 0.28f, -0.14f, 0.85f, 0.85f, 0.85f, C(0x1E140E)) },
            { "hair_mohawk", c => { var green = C(0x3BE070); float[] h = { 0.14f, 0.2f, 0.26f, 0.22f, 0.16f }; for (int i = 0; i < 5; i++) c.Add(PrimitiveType.Cube, c.head, 0f, 0.28f + h[i] / 2f - 0.02f, 0.2f - i * 0.1f, 0.07f, h[i], 0.09f, i % 2 == 0 ? green : C(0xFF4FA3)); } },
            { "hair_flame", c => { Dome(c, C(0xFF7A1A), 0.64f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.46f, 0f, 0.1f, 0.3f, 0.1f, C(0xFFD23C), 0f, 0f, 45f); c.Add(PrimitiveType.Cube, c.head, -0.14f, 0.42f, 0f, 0.09f, 0.24f, 0.09f, C(0xFF3B1A), 0f, 0f, 25f); c.Add(PrimitiveType.Cube, c.head, 0.14f, 0.42f, 0f, 0.09f, 0.24f, 0.09f, C(0xFF3B1A), 0f, 0f, -25f); } },
            { "hair_rainbow", c => { int[] cols = { 0xE23B3B, 0xFF9A1A, 0xFFE03C, 0x3BD070, 0x3B7BE2, 0x9A4FD4 }; for (int i = 0; i < 6; i++) { float a = i * Mathf.PI * 2f / 6f; c.Add(PrimitiveType.Sphere, c.head, Mathf.Sin(a) * 0.18f, 0.22f, -0.04f + Mathf.Cos(a) * 0.12f - 0.08f, 0.32f, 0.32f, 0.32f, C(cols[i])); } c.Add(PrimitiveType.Sphere, c.head, 0f, 0.3f, -0.06f, 0.4f, 0.3f, 0.4f, C(0xFFFFFF)); } },

            // ---------- Gözlük ----------
            { "glasses_round", c => { c.Glasses(C(0x1A1A1A), false); } },
            { "glasses_sun", c => { c.Add(PrimitiveType.Cube, c.head, -0.12f, 0.05f, 0.335f, 0.2f, 0.12f, 0.025f, C(0x14141A), 0f, 0f, 0f, 0.7f); c.Add(PrimitiveType.Cube, c.head, 0.12f, 0.05f, 0.335f, 0.2f, 0.12f, 0.025f, C(0x14141A), 0f, 0f, 0f, 0.7f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.07f, 0.335f, 0.06f, 0.025f, 0.025f, C(0x14141A)); c.Add(PrimitiveType.Cube, c.head, 0f, 0.07f, 0.1f, 0.6f, 0.025f, 0.025f, C(0x14141A)); } },
            { "glasses_star", c => { c.Add(PrimitiveType.Cube, c.head, 0f, 0.07f, 0.335f, 0.06f, 0.025f, 0.025f, C(0xFFD23C)); foreach (float x in new[] { -0.12f, 0.12f }) { c.Add(PrimitiveType.Cube, c.head, x, 0.05f, 0.335f, 0.17f, 0.17f, 0.03f, C(0xFF4FA3), 0f, 0f, 45f, 0.6f); c.Add(PrimitiveType.Cube, c.head, x, 0.05f, 0.34f, 0.1f, 0.1f, 0.03f, C(0xFFD23C), 0f, 0f, 45f, 0.6f); } } },
            { "glasses_cyber", c => { c.Add(PrimitiveType.Cube, c.head, 0f, 0.05f, 0.33f, 0.56f, 0.11f, 0.04f, C(0x30E0FF), 0f, 0f, 0f, 0.9f); c.Add(PrimitiveType.Cube, c.head, 0f, 0.12f, 0.335f, 0.58f, 0.02f, 0.045f, C(0x1A1F2E)); c.Add(PrimitiveType.Cube, c.head, 0f, -0.01f, 0.335f, 0.58f, 0.02f, 0.045f, C(0x1A1F2E)); c.Add(PrimitiveType.Cube, c.head, 0f, 0.07f, 0.1f, 0.6f, 0.025f, 0.025f, C(0x1A1F2E)); } },
            { "glasses_gold", c => { c.Glasses(C(0xFFCC33), true); } },

            // ---------- Yüz ifadesi ----------
            { "face_smile", c => { c.HideMouth(); c.Smile(); foreach (float x in new[] { -0.19f, 0.19f }) c.Add(PrimitiveType.Sphere, c.head, x, -0.05f, 0.24f, 0.09f, 0.06f, 0.04f, C(0xFF8A9A)); } },
            { "face_wink", c => { c.HideMouth(); c.Smile(); c.HideEye(1f); c.Add(PrimitiveType.Cube, c.head, 0.12f, 0.05f, 0.3f, 0.12f, 0.025f, 0.02f, C(0x1E1414)); } },
            { "face_cool", c => { c.HideMouth(); c.Add(PrimitiveType.Cube, c.head, -0.12f, 0.15f, 0.29f, 0.14f, 0.03f, 0.02f, C(0x1E1414), 0f, 0f, -14f); c.Add(PrimitiveType.Cube, c.head, 0.12f, 0.15f, 0.29f, 0.14f, 0.03f, 0.02f, C(0x1E1414), 0f, 0f, 14f); c.Add(PrimitiveType.Cube, c.head, 0.03f, -0.12f, 0.285f, 0.16f, 0.028f, 0.02f, C(0x1E1414), 0f, 0f, 10f); } },
            { "face_star", c => { c.HidePupils(); c.HideMouth(); foreach (float x in new[] { -0.12f, 0.12f }) { c.Add(PrimitiveType.Cube, c.head, x, 0.05f, 0.325f, 0.11f, 0.11f, 0.02f, C(0xFFD23C), 0f, 0f, 0f, 0.6f); c.Add(PrimitiveType.Cube, c.head, x, 0.05f, 0.33f, 0.11f, 0.11f, 0.02f, C(0xFFD23C), 0f, 0f, 45f, 0.6f); } c.Add(PrimitiveType.Sphere, c.head, 0f, -0.12f, 0.27f, 0.09f, 0.08f, 0.04f, C(0x1E1414)); } },

            // ---------- Üst giyim ----------
            { "top_tee", c => { c.Torso(C(0x4AA8E8)); c.ShortSleeves(C(0x4AA8E8)); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.32f, 0.145f, 0.2f, 0.2f, 0.01f, C(0xFFFFFF)); } },
            { "top_hoodie", c => { c.Torso(C(0x4A4F5E)); c.LongSleeves(C(0x4A4F5E)); c.Add(PrimitiveType.Sphere, c.visual, 0f, 0.62f, -0.12f, 0.42f, 0.22f, 0.24f, C(0x3C404D)); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.15f, 0.145f, 0.32f, 0.12f, 0.015f, C(0x383C48)); } },
            { "top_jersey", c => { c.Torso(C(0xD22E2E)); c.ShortSleeves(C(0xFFFFFF)); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.32f, 0.145f, 0.1f, 0.28f, 0.01f, C(0xFFFFFF)); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.57f, 0f, 0.52f, 0.04f, 0.3f, C(0xFFFFFF)); } },
            { "top_jacket", c => { c.Torso(C(0x15151A), 0.5f); c.LongSleeves(C(0x15151A), 0.5f); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.3f, 0.145f, 0.035f, 0.6f, 0.012f, C(0xC0C4CC), 0f, 0f, 0f, 0.8f); c.Add(PrimitiveType.Cube, c.visual, -0.18f, 0.55f, 0.14f, 0.12f, 0.1f, 0.02f, C(0x2A2A32), 0f, 0f, -20f); c.Add(PrimitiveType.Cube, c.visual, 0.18f, 0.55f, 0.14f, 0.12f, 0.1f, 0.02f, C(0x2A2A32), 0f, 0f, 20f); } },
            { "top_cape", c => { c.Torso(C(0x2A4FD0)); c.ShortSleeves(C(0x2A4FD0)); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.28f, -0.2f, 0.6f, 0.8f, 0.035f, C(0xD62828), 12f); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.34f, 0.145f, 0.16f, 0.16f, 0.012f, C(0xFFD23C), 0f, 0f, 45f, 0.6f); } },
            { "top_armor", c => { c.Torso(C(0xAEB4C2), 0.7f); c.LongSleeves(C(0x8A91A2), 0.7f); c.BothSides((s, i) => c.Add(PrimitiveType.Sphere, c.arms[i], 0f, -0.02f, 0f, 0.26f, 0.2f, 0.26f, C(0xCFD4DE), 0f, 0f, 0f, 0.7f)); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.3f, 0.145f, 0.34f, 0.42f, 0.02f, C(0xFFCC33), 0f, 0f, 0f, 0.7f); c.Add(PrimitiveType.Cube, c.visual, 0f, 0.3f, 0.155f, 0.1f, 0.3f, 0.012f, C(0xD62828)); } },

            // ---------- Alt giyim ----------
            { "bottom_jeans", c => { c.Legs(C(0x2D4F9C)); c.Belt(C(0x5A3A1E)); } },
            { "bottom_shorts", c => { c.BothSides((s, i) => c.Sleeve(c.legs[i], -0.17f, 0.17f, 0.27f, C(0xFF7A1A))); c.Belt(C(0xFFFFFF)); } },
            { "bottom_cargo", c => { c.Legs(C(0x6B7A3A)); c.Belt(C(0x3C2A14)); c.BothSides((s, i) => c.Add(PrimitiveType.Cube, c.legs[i], s * 0.12f, -0.32f, 0.02f, 0.05f, 0.16f, 0.14f, C(0x5A6830))); } },
            { "bottom_suit", c => { c.Legs(C(0x1C2036)); c.Belt(C(0x0E0E14)); c.BothSides((s, i) => c.Add(PrimitiveType.Cube, c.legs[i], 0f, -0.3f, 0.12f, 0.02f, 0.5f, 0.012f, C(0x2E3350))); } },
            { "bottom_gold", c => { c.Legs(C(0xFFCC33), 0.75f); c.Belt(C(0xFFF0A0)); } },

            // ---------- Ayakkabı ----------
            { "shoes_sneaker", c => c.Shoes(C(0xF4F4F4), C(0xD22E2E)) },
            { "shoes_boots", c => { c.Shoes(C(0x6B4220), C(0x2A1A0C)); c.BothSides((s, i) => c.Add(PrimitiveType.Cylinder, c.legs[i], 0f, -0.64f, 0f, 0.27f, 0.12f, 0.27f, C(0x6B4220))); } },
            { "shoes_neon", c => c.Shoes(C(0x15151C), C(0x35FF90), 0.8f) },
            { "shoes_rocket", c => { c.Shoes(C(0x8C93A5), C(0x4A4F5E)); c.BothSides((s, i) => { c.Add(PrimitiveType.Cylinder, c.legs[i], 0f, -0.72f, -0.17f, 0.12f, 0.1f, 0.12f, C(0xFF7A1A), 90f); c.Add(PrimitiveType.Sphere, c.legs[i], 0f, -0.72f, -0.3f, 0.12f, 0.12f, 0.2f, C(0xFFD23C)); }); } },
            { "shoes_wing", c => { c.Shoes(C(0xFFFFFF), C(0xFFCC33), 0.6f); c.BothSides((s, i) => { c.Add(PrimitiveType.Cube, c.legs[i], s * 0.15f, -0.68f, -0.05f, 0.03f, 0.2f, 0.16f, C(0xFFFFFF), 0f, 0f, s * 22f); c.Add(PrimitiveType.Cube, c.legs[i], s * 0.17f, -0.6f, -0.05f, 0.03f, 0.14f, 0.12f, C(0xFFE9A0), 0f, 0f, s * 30f); }); } },
        };

        // ---------- Ortak parçalar ----------

        // Kafayı saran yarım küre (şapka tepesi veya kısa saç)
        static void Dome(Ctx c, Color color, float width, float height = 0.34f)
        {
            c.Add(PrimitiveType.Sphere, c.head, 0f, 0.2f, -0.01f, width, height, width, color);
        }

        static void Glasses(this Ctx c, Color frame, bool shiny)
        {
            float smooth = shiny ? 0.7f : 0.1f;
            foreach (float x in new[] { -0.12f, 0.12f })
            {
                c.Add(PrimitiveType.Cube, c.head, x, 0.14f, 0.335f, 0.2f, 0.025f, 0.025f, frame, 0f, 0f, 0f, smooth);
                c.Add(PrimitiveType.Cube, c.head, x, -0.04f, 0.335f, 0.2f, 0.025f, 0.025f, frame, 0f, 0f, 0f, smooth);
                c.Add(PrimitiveType.Cube, c.head, x - 0.1f, 0.05f, 0.335f, 0.025f, 0.2f, 0.025f, frame, 0f, 0f, 0f, smooth);
                c.Add(PrimitiveType.Cube, c.head, x + 0.1f, 0.05f, 0.335f, 0.025f, 0.2f, 0.025f, frame, 0f, 0f, 0f, smooth);
            }
            c.Add(PrimitiveType.Cube, c.head, 0f, 0.08f, 0.335f, 0.06f, 0.025f, 0.025f, frame, 0f, 0f, 0f, smooth);
            c.Add(PrimitiveType.Cube, c.head, 0f, 0.1f, 0.1f, 0.6f, 0.025f, 0.025f, frame, 0f, 0f, 0f, smooth);
        }

        static void Smile(this Ctx c)
        {
            float[] ys = { -0.08f, -0.11f, -0.125f, -0.11f, -0.08f };
            for (int i = 0; i < 5; i++)
                c.Add(PrimitiveType.Cube, c.head, (i - 2) * 0.05f, ys[i], 0.28f, 0.06f, 0.03f, 0.02f, C(0x1E1414), 0f, 0f, (i - 2) * -12f);
        }

        static void HideMouth(this Ctx c) => Hide(c.head, "Mouth", 0f);
        static void HidePupils(this Ctx c) => Hide(c.head, "Pupil", 0f);
        static void HideEye(this Ctx c, float side)
        {
            Hide(c.head, "EyeWhite", side);
            Hide(c.head, "Pupil", side);
        }

        // side 0 = hepsi, 1 = yalnızca sağdakiler
        static void Hide(Transform head, string name, float side)
        {
            foreach (Transform t in head)
                if (t.name == name && (side == 0f || t.localPosition.x > 0f)) t.gameObject.SetActive(false);
        }

        static void Torso(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.Dye(c.torso, color, smooth);
        }

        static void ShortSleeves(this Ctx c, Color color)
        {
            c.BothSides((s, i) => c.Sleeve(c.arms[i], -0.1f, 0.1f, 0.2f, color));
        }

        static void LongSleeves(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.BothSides((s, i) => { c.Dye(c.armMeshes[i], color, smooth); });
        }

        static void Legs(this Ctx c, Color color, float smooth = 0.1f)
        {
            c.BothSides((s, i) => c.Dye(c.legMeshes[i], color, smooth));
        }

        static void Belt(this Ctx c, Color color)
        {
            c.Add(PrimitiveType.Cube, c.visual, 0f, 0.02f, 0f, 0.53f, 0.07f, 0.3f, color);
        }

        static void Shoes(this Ctx c, Color body, Color sole, float smooth = 0.1f)
        {
            c.BothSides((s, i) =>
            {
                c.Dye(c.feet[i], body, smooth);
                c.Add(PrimitiveType.Sphere, c.legs[i], 0f, -0.74f, 0.06f, 0.27f, 0.17f, 0.4f, body, 0f, 0f, 0f, smooth);
                c.Add(PrimitiveType.Cube, c.legs[i], 0f, -0.805f, 0.06f, 0.25f, 0.045f, 0.4f, sole, 0f, 0f, 0f, smooth);
            });
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
            foreach (Transform t in c.head)
                if (t.name == "Mouth" || t.name == "EyeWhite" || t.name == "Pupil") t.gameObject.SetActive(true);
        }
    }
}
