using TextRun.Common;
using UnityEngine;

namespace TextRun.Characters
{
    // Temel (giysisiz) karakteri basit şekillerden kurar. Yer tutucudur;
    // gerçek low-poly model Blender'da yapılınca bu sınıf modeli yükleyecek.
    public static class CharacterBuilder
    {
        const float HipHeight = 0.8f;
        public static readonly Color Skin = new Color(0.95f, 0.76f, 0.62f);
        static readonly Color SkinShade = new Color(0.87f, 0.66f, 0.53f);
        static readonly Color Brow = new Color(0.29f, 0.18f, 0.11f);
        static readonly Color EyeWhite = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color Dark = new Color(0.12f, 0.1f, 0.1f);

        public static CharacterAnimator Build(string name, Color laneColor)
        {
            var root = new GameObject(name).transform;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            visual.localPosition = new Vector3(0f, HipHeight, 0f);

            Mats.Primitive("Torso", PrimitiveType.Cube, visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.28f), Skin);

            var head = new GameObject("Head").transform;
            head.SetParent(visual, false);
            head.localPosition = new Vector3(0f, 0.94f, 0f);
            head.localScale = Vector3.one * 1.15f; // biraz iri kafa: yüz ve şapkalar uzaktan da seçilir
            BuildHead(head);

            Mats.Primitive("Neck", PrimitiveType.Cylinder, visual, new Vector3(0f, 0.61f, 0f), new Vector3(0.17f, 0.05f, 0.17f), SkinShade);

            var leftArm = Limb("LeftArm", visual, new Vector3(-0.35f, 0.55f, 0f), 0.6f, 0.14f);
            var rightArm = Limb("RightArm", visual, new Vector3(0.35f, 0.55f, 0f), 0.6f, 0.14f);
            var leftLeg = Limb("LeftLeg", visual, new Vector3(-0.14f, 0f, 0f), 0.8f, 0.22f);
            var rightLeg = Limb("RightLeg", visual, new Vector3(0.14f, 0f, 0f), 0.8f, 0.22f);

            Mats.Primitive("Hand", PrimitiveType.Sphere, leftArm, new Vector3(0f, -0.61f, 0f), Vector3.one * 0.17f, Skin);
            Mats.Primitive("Hand", PrimitiveType.Sphere, rightArm, new Vector3(0f, -0.61f, 0f), Vector3.one * 0.17f, Skin);

            Mats.Primitive("LeftFoot", PrimitiveType.Sphere, leftLeg, new Vector3(0f, -0.75f, 0.06f), new Vector3(0.22f, 0.1f, 0.34f), Skin);
            Mats.Primitive("RightFoot", PrimitiveType.Sphere, rightLeg, new Vector3(0f, -0.75f, 0.06f), new Vector3(0.22f, 0.1f, 0.34f), Skin);

            // Kulvar rengini gösteren yer işareti
            Mats.Primitive("LaneMarker", PrimitiveType.Cylinder, root, new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.02f, 0.9f), laneColor);

            var animator = root.gameObject.AddComponent<CharacterAnimator>();
            animator.visual = visual;
            animator.head = head;
            animator.leftArm = leftArm;
            animator.rightArm = rightArm;
            animator.leftLeg = leftLeg;
            animator.rightLeg = rightLeg;
            if (laneColor.a > 0f) animator.baseColor = laneColor;
            animator.Pose(0f);
            return animator;
        }

        static void BuildHead(Transform head)
        {
            Mats.Primitive("Skull", PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 0.6f, Skin);
            Mats.Primitive("Nose", PrimitiveType.Sphere, head, new Vector3(0f, -0.035f, 0.295f), new Vector3(0.07f, 0.06f, 0.07f), SkinShade);

            foreach (float side in new[] { -1f, 1f })
            {
                Mats.Primitive("Ear", PrimitiveType.Sphere, head, new Vector3(0.295f * side, 0f, 0f), new Vector3(0.08f, 0.14f, 0.11f), SkinShade);
                Mats.Primitive("EyeWhite", PrimitiveType.Sphere, head, new Vector3(0.12f * side, 0.05f, 0.245f), new Vector3(0.16f, 0.17f, 0.12f), EyeWhite);
                Mats.Primitive("Pupil", PrimitiveType.Sphere, head, new Vector3(0.12f * side, 0.05f, 0.295f), new Vector3(0.09f, 0.1f, 0.05f), Dark);
                // Göz parıltısı (gözbebeğiyle birlikte gizlenebilsin diye aynı adı taşır)
                Mats.Primitive("Pupil", PrimitiveType.Sphere, head, new Vector3(0.12f * side + 0.022f, 0.075f, 0.318f), Vector3.one * 0.03f, EyeWhite);
                Mats.Primitive("Brow", PrimitiveType.Cube, head, new Vector3(0.12f * side, 0.165f, 0.268f), new Vector3(0.13f, 0.026f, 0.02f), Brow).transform.localEulerAngles = new Vector3(0f, 0f, -6f * side);
            }

            Mats.Primitive("Mouth", PrimitiveType.Cube, head, new Vector3(0f, -0.135f, 0.268f), new Vector3(0.12f, 0.026f, 0.02f), Dark);
        }

        // Omuz veya kalça noktasında dönen, aşağı sarkan bir uzuv.
        static Transform Limb(string name, Transform parent, Vector3 pivotPos, float length, float thickness)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotPos;

            // Kapsül varsayılan olarak 2 birim yüksekliğindedir.
            Mats.Primitive(name + "Mesh", PrimitiveType.Capsule, pivot, new Vector3(0f, -length / 2f, 0f), new Vector3(thickness, length / 2f, thickness), Skin);
            return pivot;
        }
    }
}
