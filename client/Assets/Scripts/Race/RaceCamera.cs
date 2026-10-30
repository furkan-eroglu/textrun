using UnityEngine;

namespace TextRun.Race
{
    // Pistin arkasında, hafif yukarıdan bakan takip kamerası.
    // Görüş açısı, çizilen bölgenin en/boy oranına göre ayarlanır: dikey (portre) ekranda da kulvarlar yana taşmaz.
    public class RaceCamera : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 6.5f, -15f);
        public Vector3 lookAhead = new Vector3(0f, 0.8f, 6f);
        // Geniş ekranlarda kullanılan en küçük dikey görüş açısı
        public float fieldOfView = 42f;
        // Kameranın ortasından her iki yana görünmesi gereken genişlik (metre)
        public float halfWidthToFit = 6.4f;
        public float maxFieldOfView = 85f;

        public Transform target;

        void LateUpdate()
        {
            if (target != null) Frame(target.position.z);
        }

        // z: kameranın baktığı koşucunun mesafesi, x: kameranın yana kayması (oyuncunun kulvarını takip etmek için)
        public void Frame(float z, float x = 0f)
        {
            transform.position = new Vector3(x + offset.x, offset.y, z + offset.z);
            transform.LookAt(new Vector3(x + lookAhead.x, lookAhead.y, z + lookAhead.z));

            var cam = GetComponent<Camera>();
            float horizontalHalf = Mathf.Atan(halfWidthToFit / Mathf.Abs(offset.z));
            float neededVertical = 2f * Mathf.Atan(Mathf.Tan(horizontalHalf) / Mathf.Max(cam.aspect, 0.1f)) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(neededVertical, fieldOfView, maxFieldOfView);
        }
    }
}
