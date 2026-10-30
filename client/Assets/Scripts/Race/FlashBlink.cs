using UnityEngine;

namespace TextRun.Race
{
    // Kamera flaşı: seyirci telefonlarının flaşları rastgele aralıklarla çok kısa süre yanıp söner.
    // Her bileşen, birleştirilmiş küçük bir flaş grubunu yönetir (çok sayıda flaş, az çizim çağrısı).
    public class FlashBlink : MonoBehaviour
    {
        const float FlashSeconds = 0.07f;

        MeshRenderer flash;
        float nextOn;
        float offAt;

        void Awake()
        {
            flash = GetComponent<MeshRenderer>();
            flash.enabled = false;
            nextOn = Time.time + Random.Range(0f, 2f);
        }

        void Update()
        {
            if (flash.enabled)
            {
                if (Time.time < offAt) return;
                flash.enabled = false;
                nextOn = Time.time + Random.Range(0.5f, 2.2f);
            }
            else if (Time.time >= nextOn)
            {
                flash.enabled = true;
                offAt = Time.time + FlashSeconds;
            }
        }
    }
}
