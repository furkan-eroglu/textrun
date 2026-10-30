using UnityEngine;

namespace TextRun.Characters
{
    public enum CharacterState { Idle, Run }

    // Uzuvları kod ile sallayarak koşu ve bekleme hareketi verir.
    public class CharacterAnimator : MonoBehaviour
    {
        const float HipHeight = 0.8f;
        const float RunFrequency = 9f;
        const float IdleFrequency = 1.6f;

        public Transform visual;
        public Transform head;
        public Transform leftArm, rightArm, leftLeg, rightLeg;

        public CharacterState state = CharacterState.Idle;
        // 1 = normal koşu hızı. Daha hızlı yazan karakterin adımları da sıklaşır.
        public float runSpeed = 1f;

        float phase;

        void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            float frequency = state == CharacterState.Run
                ? RunFrequency * Mathf.Max(runSpeed, 0.3f)
                : IdleFrequency;
            phase += dt * frequency;
            Pose(phase);
        }

        public void Pose(float p)
        {
            if (state == CharacterState.Run) PoseRun(p);
            else PoseIdle(p);
        }

        void PoseRun(float p)
        {
            float s = Mathf.Sin(p);

            leftLeg.localRotation = Quaternion.Euler(s * 55f, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-s * 55f, 0f, 0f);
            leftArm.localRotation = Quaternion.Euler(-s * 60f, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(s * 60f, 0f, 0f);

            visual.localRotation = Quaternion.Euler(12f, 0f, 0f);
            visual.localPosition = new Vector3(0f, HipHeight + Mathf.Abs(s) * 0.07f, 0f);
            head.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        }

        void PoseIdle(float p)
        {
            float s = Mathf.Sin(p);

            leftLeg.localRotation = Quaternion.identity;
            rightLeg.localRotation = Quaternion.identity;
            leftArm.localRotation = Quaternion.Euler(s * 3f, 0f, 2f);
            rightArm.localRotation = Quaternion.Euler(-s * 3f, 0f, -2f);

            visual.localRotation = Quaternion.identity;
            visual.localPosition = new Vector3(0f, HipHeight, 0f);
            visual.localScale = new Vector3(1f, 1f + s * 0.012f, 1f);
            head.localRotation = Quaternion.Euler(0f, 0f, s * 2f);
        }
    }
}
