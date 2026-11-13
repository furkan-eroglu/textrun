using TextRun.Common;
using UnityEngine;

namespace TextRun.Characters
{
    // Temel (giysisiz) karakteri kurar: gövde, kol ve bacaklar kesit çizgilerinden döndürülerek üretilen yumuşak ağlardır
    // (MeshKit). Eşyalar aynı kesitleri kullanarak gövdeye tam oturur (Outfits).
    public static class CharacterBuilder
    {
        const float HipHeight = 0.8f;
        public static readonly Color Skin = new Color(0.95f, 0.76f, 0.62f);
        static readonly Color SkinShade = new Color(0.87f, 0.66f, 0.53f);
        static readonly Color Brow = new Color(0.29f, 0.18f, 0.11f);
        static readonly Color EyeWhite = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color Dark = new Color(0.12f, 0.1f, 0.1f);

        // Ten renkleri: 0 açık (varsayılan), 1 kahverengi, 2 koyu. Gölge tonu burun, kulak ve boyunda kullanılır.
        public static readonly Color[] SkinTones = { Skin, new Color(0.70f, 0.48f, 0.34f), new Color(0.36f, 0.24f, 0.18f) };
        public static Color ShadeOf(Color skin) => new Color(skin.r * 0.91f, skin.g * 0.87f, skin.b * 0.86f);

        // Kadın gövdesi: daha dar omuz ve bel, hafif geniş kalça
        public static readonly float[] FemaleTorsoProfile =
        {
            0f, -0.03f, 0.13f, -0.025f, 0.212f, 0f, 0.236f, 0.06f, 0.228f, 0.14f, 0.186f, 0.29f, 0.204f, 0.4f, 0.23f, 0.5f, 0.222f, 0.585f, 0.17f, 0.625f, 0.085f, 0.645f, 0f, 0.65f
        };

        // Kesitler: (yarıçap, yükseklik) çiftleri, aşağıdan yukarı. Gövde önden arkaya TorsoDepth oranında basıktır.
        public const float TorsoDepth = 0.6f;
        public static readonly float[] TorsoProfile =
        {
            0f, -0.03f, 0.13f, -0.025f, 0.205f, 0f, 0.228f, 0.06f, 0.226f, 0.16f, 0.214f, 0.28f, 0.236f, 0.42f, 0.262f, 0.52f, 0.25f, 0.585f, 0.19f, 0.625f, 0.09f, 0.645f, 0f, 0.65f
        };
        public static readonly float[] ArmProfile =
        {
            0f, -0.615f, 0.045f, -0.605f, 0.058f, -0.56f, 0.07f, -0.3f, 0.084f, -0.05f, 0.086f, 0.02f, 0.062f, 0.065f, 0f, 0.078f
        };
        public static readonly float[] LegProfile =
        {
            0f, -0.775f, 0.06f, -0.765f, 0.08f, -0.72f, 0.094f, -0.45f, 0.114f, -0.2f, 0.12f, -0.02f, 0.09f, 0.035f, 0f, 0.045f
        };

        public static CharacterAnimator Build(string name, Color laneColor)
        {
            var root = new GameObject(name).transform;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            visual.localPosition = new Vector3(0f, HipHeight, 0f);

            Mats.MeshObject("Torso", MeshKit.Lathe(TorsoProfile), visual, Vector3.zero, new Vector3(1f, 1f, TorsoDepth), Skin);
            Mats.Primitive("Neck", PrimitiveType.Cylinder, visual, new Vector3(0f, 0.63f, 0f), new Vector3(0.17f, 0.05f, 0.17f), SkinShade);

            var head = new GameObject("Head").transform;
            head.SetParent(visual, false);
            head.localPosition = new Vector3(0f, 0.94f, 0f);
            head.localScale = Vector3.one * 1.15f; // biraz iri kafa: yüz ve şapkalar uzaktan da seçilir
            BuildHead(head);

            var leftArm = Limb("LeftArm", visual, new Vector3(-0.35f, 0.55f, 0f), ArmProfile);
            var rightArm = Limb("RightArm", visual, new Vector3(0.35f, 0.55f, 0f), ArmProfile);
            var leftLeg = Limb("LeftLeg", visual, new Vector3(-0.125f, 0f, 0f), LegProfile);
            var rightLeg = Limb("RightLeg", visual, new Vector3(0.125f, 0f, 0f), LegProfile);

            Mats.Primitive("Hand", PrimitiveType.Sphere, leftArm, new Vector3(0f, -0.63f, 0f), new Vector3(0.15f, 0.17f, 0.14f), Skin);
            Mats.Primitive("Hand", PrimitiveType.Sphere, rightArm, new Vector3(0f, -0.63f, 0f), new Vector3(0.15f, 0.17f, 0.14f), Skin);

            var foot = MeshKit.RoundedBox(0.18f, 0.1f, 0.32f, 0.05f);
            Mats.MeshObject("LeftFoot", foot, leftLeg, new Vector3(0f, -0.75f, 0.07f), Vector3.one, Skin);
            Mats.MeshObject("RightFoot", foot, rightLeg, new Vector3(0f, -0.75f, 0.07f), Vector3.one, Skin);

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
            Mats.Primitive("Nose", PrimitiveType.Sphere, head, new Vector3(0f, -0.035f, 0.295f), new Vector3(0.07f, 0.06f, 0.07f), SkinShade);

            foreach (float side in new[] { -1f, 1f })
            {
                Mats.Primitive("Ear", PrimitiveType.Sphere, head, new Vector3(0.295f * side, 0f, 0f), new Vector3(0.08f, 0.14f, 0.11f), SkinShade);
                Mats.Primitive("EyeWhite", PrimitiveType.Sphere, head, new Vector3(0.12f * side, 0.05f, 0.245f), new Vector3(0.16f, 0.17f, 0.12f), EyeWhite);
                Mats.Primitive("Pupil", PrimitiveType.Sphere, head, new Vector3(0.12f * side, 0.05f, 0.295f), new Vector3(0.09f, 0.1f, 0.05f), Dark);
                Mats.Primitive("Pupil", PrimitiveType.Sphere, head, new Vector3(0.12f * side + 0.022f, 0.075f, 0.318f), Vector3.one * 0.03f, EyeWhite);
                Mats.MeshObject("Brow", MeshKit.RoundedBox(0.13f, 0.03f, 0.025f, 0.012f, 3), head, new Vector3(0.12f * side, 0.165f, 0.268f), Vector3.one, Brow).transform.localEulerAngles = new Vector3(0f, 0f, -6f * side);
            }

            // Hafif gülümseyen ağız: aşağı bakan ince yay
            Mats.MeshObject("Mouth", MeshKit.Prism(MeshKit.Arc(0.07f, 0.035f, 200f, 340f, 8), 0.02f), head, new Vector3(0f, -0.105f, 0.272f), Vector3.one, Dark);
        }

        // Omuz veya kalça noktasında dönen, aşağı sarkan bir uzuv (kesitten döndürülür; aşağı doğru incelir)
        static Transform Limb(string name, Transform parent, Vector3 pivotPos, float[] profile)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotPos;
            Mats.MeshObject(name + "Mesh", MeshKit.Lathe(profile, 16), pivot, Vector3.zero, Vector3.one, Skin);
            return pivot;
        }
    }
}
