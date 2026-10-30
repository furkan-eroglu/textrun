using UnityEngine;

namespace TextRun.Race
{
    // Pistin arkasında, hafif yukarıdan bakan takip kamerası. Tüm kulvarlar görünür.
    public class RaceCamera : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 6.5f, -15f);
        public Vector3 lookAhead = new Vector3(0f, 0.8f, 6f);
        public float fieldOfView = 42f;

        public Transform target;

        void LateUpdate()
        {
            if (target != null) Frame(target.position.z);
        }

        public void Frame(float z)
        {
            transform.position = new Vector3(offset.x, offset.y, z + offset.z);
            transform.LookAt(new Vector3(lookAhead.x, lookAhead.y, z + lookAhead.z));
            GetComponent<Camera>().fieldOfView = fieldOfView;
        }
    }
}
