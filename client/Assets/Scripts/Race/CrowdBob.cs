using UnityEngine;

namespace TextRun.Race
{
    // Seyirci grubunu hafifçe zıplatır. Heyecan seviyesi (Intensity) yarış sırasında yükselir.
    public class CrowdBob : MonoBehaviour
    {
        // 0 = durgun, 1 = en coşkulu
        public static float Intensity = 0.35f;

        public float phase;
        public float speed = 4f;
        public float amplitude = 0.16f;

        void Update()
        {
            float y = Mathf.Abs(Mathf.Sin(Time.time * speed + phase)) * amplitude * Intensity;
            transform.localPosition = new Vector3(0f, y, 0f);
        }
    }
}
