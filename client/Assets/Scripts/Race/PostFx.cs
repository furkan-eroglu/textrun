using UnityEngine;

namespace TextRun.Race
{
    // Yarış kamerasının görüntüsüne ekran efekti uygular: parlak yerlerden (tavan ışıkları, ışık duvarı) taşan ışık,
    // biraz daha canlı renk ve kontrast, kenarlarda hafif karartma. Işık taşması çeyrek çözünürlükte hesaplanır (telefonda ucuz).
    // Gölgelendirici bulunamaz ya da cihaz desteklemezse efekt kendini kapatır; görüntü olduğu gibi kalır.
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        const string ShaderPath = "Shaders/PostFx";

        public float threshold = 0.84f;   // bu parlaklığın üstü ışık saçar
        public float intensity = 0.6f;   // saçılan ışığın gücü
        public float saturation = 1.14f;  // 1 = değişmez
        public float contrast = 1.06f;    // 1 = değişmez
        public float vignette = 0.55f;    // kenar karartma (0 = yok)
        public int blurPasses = 2;

        // Karşılaştırma ekran görüntüleri için (DevHarness)
        public static bool Disabled;

        Material material;

        void OnEnable()
        {
            var shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                enabled = false;
                return;
            }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (material != null) Destroy(material);
            material = null;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null || Disabled)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int width = Mathf.Max(1, source.width / 4), height = Mathf.Max(1, source.height / 4);
            var a = RenderTexture.GetTemporary(width, height, 0, source.format);
            var b = RenderTexture.GetTemporary(width, height, 0, source.format);
            a.filterMode = FilterMode.Bilinear;
            b.filterMode = FilterMode.Bilinear;

            material.SetFloat("_Threshold", threshold);
            Graphics.Blit(source, a, material, 0);
            for (int i = 0; i < blurPasses; i++)
            {
                material.SetVector("_Direction", new Vector4(1f + i, 0f, 0f, 0f));
                Graphics.Blit(a, b, material, 1);
                material.SetVector("_Direction", new Vector4(0f, 1f + i, 0f, 0f));
                Graphics.Blit(b, a, material, 1);
            }

            material.SetTexture("_BloomTex", a);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Saturation", saturation);
            material.SetFloat("_Contrast", contrast);
            material.SetFloat("_Vignette", vignette);
            Graphics.Blit(source, destination, material, 2);

            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
    }
}
