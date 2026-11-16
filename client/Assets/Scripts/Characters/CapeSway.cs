using UnityEngine;

namespace TextRun.Characters
{
    // Pelerinin hareketi: koşarken omuz hizasından geriye doğru uçar ve dalgalanır, dururken neredeyse sarkık kalır,
    // kürsüde sevinirken hafifçe sallanır. Pelerin parçaları bu nesnenin altında durur (Outfits.Apply kurar).
    // Animasyon kapalıysa (eşya resmi çizilirken) pelerin sakin durur, resim hep aynı çıkar.
    public class CapeSway : MonoBehaviour
    {
        public CharacterAnimator animator;
        float angle;

        void Update()
        {
            float target = 0f;
            if (animator != null && animator.enabled)
            {
                float time = Time.time;
                switch (animator.state)
                {
                    case CharacterState.Run:
                        float speed = Mathf.Max(animator.runSpeed, 0.3f);
                        target = 24f + 9f * Mathf.Sin(time * 11f * speed) + 4f * Mathf.Sin(time * 6.3f * speed);
                        break;
                    case CharacterState.Cheer:
                        target = 10f + 6f * Mathf.Sin(time * 6f);
                        break;
                    default:
                        target = 1.5f * Mathf.Sin(time * 1.6f);
                        break;
                }
            }
            angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
