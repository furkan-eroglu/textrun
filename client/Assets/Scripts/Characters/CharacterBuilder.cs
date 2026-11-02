using TextRun.Common;
using UnityEngine;

namespace TextRun.Characters
{
    // Temel (giysisiz) karakteri basit şekillerden kurar. Yer tutucudur;
    // gerçek low-poly model Blender'da yapılınca bu sınıf modeli yükleyecek.
    public static class CharacterBuilder
    {
        const float HipHeight = 0.8f;
        public static readonly Color Skin = new Color(0.91f, 0.74f, 0.60f);
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
            head.localPosition = new Vector3(0f, 0.88f, 0f);
            BuildHead(head);

            var leftArm = Limb("LeftArm", visual, new Vector3(-0.35f, 0.55f, 0f), 0.6f, 0.14f);
            var rightArm = Limb("RightArm", visual, new Vector3(0.35f, 0.55f, 0f), 0.6f, 0.14f);
            var leftLeg = Limb("LeftLeg", visual, new Vector3(-0.14f, 0f, 0f), 0.8f, 0.22f);
            var rightLeg = Limb("RightLeg", visual, new Vector3(0.14f, 0f, 0f), 0.8f, 0.22f);

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
            animator.Pose(0f);
            return animator;
        }

        static void BuildHead(Transform head)
        {
            Mats.Primitive("Skull", PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 0.6f, Skin);

            foreach (float side in new[] { -1f, 1f })
            {
                Mats.Primitive("EyeWhite", PrimitiveType.Sphere, head, new Vector3(0.12f * side, 0.05f, 0.25f), Vector3.one * 0.14f, EyeWhite);
                Mats.Primitive("Pupil", PrimitiveType.Sphere, head, new Vector3(0.12f * side, 0.05f, 0.31f), Vector3.one * 0.07f, Dark);
            }

            Mats.Primitive("Mouth", PrimitiveType.Cube, head, new Vector3(0f, -0.12f, 0.272f), new Vector3(0.14f, 0.025f, 0.02f), Dark);
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
