using UnityEngine;

namespace TextRun.Race
{
    // Pistin arkasında, hafif yukarıdan bakan takip kamerası. Tüm kulvarlar görünür.
    // Görüş açısı, çizilen bölgenin en/boy oranına göre ayarlanır: dikey (portre) ekranda da beş kulvar yana taşmaz.
    public class RaceCamera : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 6.5f, -15f);
        public Vector3 lookAhead = new Vector3(0f, 0.8f, 6f);
        // Geniş ekranlarda kullanılan en küçük dikey görüş açısı
        public float fieldOfView = 42f;
        // Pistin ortadan her iki yana görünmesi gereken genişliği (metre): kulvarlar + kenar boşluğu
        public float halfWidthToFit = 6.4f;
        public float maxFieldOfView = 85f;

        public Transform target;

        void LateUpdate()
        {
            if (target != null) Frame(target.position.z);
        }

        public void Frame(float z)
        {
            transform.position = new Vector3(offset.x, offset.y, z + offset.z);
            transform.LookAt(new Vector3(lookAhead.x, lookAhead.y, z + lookAhead.z));

            var cam = GetComponent<Camera>();
            float horizontalHalf = Mathf.Atan(halfWidthToFit / Mathf.Abs(offset.z));
            float neededVertical = 2f * Mathf.Atan(Mathf.Tan(horizontalHalf) / Mathf.Max(cam.aspect, 0.1f)) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(neededVertical, fieldOfView, maxFieldOfView);
        }
    }
}
