using TextRun.Characters;
using TextRun.Online;
using UnityEngine;

namespace TextRun.Race
{
    // Sunucudan gelen ilerlemeye göre beş karakteri pistte koşturur. Mantık içermez, yalnızca gösterir.
    public class OnlineRaceView : MonoBehaviour
    {
        const float MaxRunSpeed = 14f;
        const float FinishOvershoot = 4f;

        public int laneCount = 5;
        public float laneWidth = 1.4f;
        public float trackLength = 60f;

        // Kamera oyuncuya yakın durur ve oyuncunun kulvarını takip eder; en uzak kulvar da görünsün diye genişlik buna göre hesaplanır.
        static readonly Vector3 CameraOffset = new Vector3(0f, 5f, -11f);
        const float CameraSideMargin = 1.0f;
        // Kameranın oyuncunun kulvarına ne kadar kaydığı: 0 = hep pist ortası, 1 = tam oyuncunun üstü.
        // Tam takipte kenar kulvarda ekranın yarısı boş çimen kalır; kısmi takip oyuncuyu merkeze yaklaştırır, pisti de yayar.
        const float CameraFollowFactor = 0.6f;

        static readonly Color[] LaneColors =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.95f, 0.70f, 0.20f), new Color(0.30f, 0.70f, 0.40f),
            new Color(0.30f, 0.55f, 0.90f), new Color(0.65f, 0.40f, 0.85f)
        };

        CharacterAnimator[] runners;
        float[] currentZ;
        float[] targetZ;
        string[] names;
        bool[] isBot;
        bool[] active;
        bool racing;
        int localSlot = 2;
        float cameraX;
        Camera cam;
        RaceCamera raceCamera;
        GUIStyle nameStyle;

        public Camera Cam => cam;

        public void Build()
        {
            TrackBuilder.Build(laneCount, laneWidth, trackLength);

            runners = new CharacterAnimator[laneCount];
            currentZ = new float[laneCount];
            targetZ = new float[laneCount];
            names = new string[laneCount];
            isBot = new bool[laneCount];
            active = new bool[laneCount];

            for (int i = 0; i < laneCount; i++)
            {
                runners[i] = CharacterBuilder.Build("Runner" + (i + 1), LaneColors[i % LaneColors.Length]);
                runners[i].transform.position = new Vector3(TrackBuilder.LaneX(i, laneCount, laneWidth), 0f, 0f);
                runners[i].gameObject.SetActive(false);
            }

            cam = Camera.main;
            if (cam == null)
            {
                var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camObject.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.82f, 0.95f);
            raceCamera = cam.GetComponent<RaceCamera>();
            if (raceCamera == null) raceCamera = cam.gameObject.AddComponent<RaceCamera>();
            raceCamera.enabled = false; // kamerayı bu sınıf kendisi hareket ettirir
            raceCamera.offset = CameraOffset;
            // En kenar kulvardaki oyuncu için kamera en fazla yarım pist * takip oranı kadar kayar; o durumda bile pistin öbür ucu görünmeli.
            float halfTrack = (laneCount - 1) * laneWidth / 2f;
            raceCamera.halfWidthToFit = halfTrack * (1f + CameraFollowFactor) + CameraSideMargin;
            cameraX = TrackBuilder.LaneX(localSlot, laneCount, laneWidth) * CameraFollowFactor;
            raceCamera.Frame(0f, cameraX);

            // Ana kamera ekranın yalnızca üst bölgesini çizer; alt bölge (kelimeler, klavye) temizlenmezse eski kareler birikir.
            // Bu yüzden tüm ekranı her kare koyu renkle temizleyen, hiçbir şey çizmeyen bir arka plan kamerası eklenir.
            var background = new GameObject("Background Camera").AddComponent<Camera>();
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            background.cullingMask = 0;
            background.depth = cam.depth - 10f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.75f, 0.82f);
        }

        // Kameranın çizdiği ekran bölgesi (piksel). Alt kısım kelimelere ve klavyeye bırakılır.
        public void SetViewport(Rect pixelRect)
        {
            if (cam != null && pixelRect.width > 1f && pixelRect.height > 1f) cam.pixelRect = pixelRect;
        }

        public void ResetToStart()
        {
            for (int i = 0; i < laneCount; i++)
            {
                currentZ[i] = 0f;
                targetZ[i] = 0f;
                SetPosition(i, 0f);
                runners[i].state = CharacterState.Idle;
            }
            racing = false;
        }

        // Odadaki oyuncuları yerleştirir (slot = kulvar). Boş slotlar gizlenir.
        public void SetRoster(PlayerDto[] players, string localId)
        {
            for (int i = 0; i < laneCount; i++) active[i] = false;
            if (players == null) return;

            foreach (var p in players)
            {
                if (p.slot < 0 || p.slot >= laneCount) continue;
                active[p.slot] = true;
                names[p.slot] = p.name;
                isBot[p.slot] = p.isBot;
                if (p.id == localId) localSlot = p.slot;
            }
            for (int i = 0; i < laneCount; i++) runners[i].gameObject.SetActive(active[i]);
        }

        public void SetProgress(PlayerDto[] players, int wordCount)
        {
            if (players == null || wordCount <= 0) return;
            foreach (var p in players)
            {
                if (p.slot < 0 || p.slot >= laneCount) continue;
                targetZ[p.slot] = p.finished && !p.disqualified
                    ? trackLength + FinishOvershoot
                    : trackLength * Mathf.Clamp01((float)p.index / wordCount);
            }
        }

        public void SetRacing(bool value)
        {
            racing = value;
        }

        void Update()
        {
            if (runners == null) return;

            for (int i = 0; i < laneCount; i++)
            {
                if (!active[i]) continue;

                float gap = targetZ[i] - currentZ[i];
                float step = Mathf.Min(Mathf.Abs(gap), Mathf.Max(MaxRunSpeed * 0.25f, Mathf.Abs(gap) * 6f) * Time.deltaTime);
                float speed = step / Mathf.Max(Time.deltaTime, 0.0001f);
                currentZ[i] += Mathf.Sign(gap) * step;
                SetPosition(i, currentZ[i]);

                bool moving = racing && speed > 0.4f;
                runners[i].state = moving ? CharacterState.Run : CharacterState.Idle;
                runners[i].runSpeed = Mathf.Clamp(0.7f + speed / 8f, 0.7f, 1.8f);
            }

            if (cam != null && raceCamera != null)
            {
                // Kamera oyuncunun kulvarına yumuşakça kayar
                float targetX = TrackBuilder.LaneX(localSlot, laneCount, laneWidth) * CameraFollowFactor;
                cameraX = Mathf.Lerp(cameraX, targetX, 1f - Mathf.Exp(-6f * Time.deltaTime));
                raceCamera.Frame(currentZ[localSlot], cameraX);
            }
        }

        void SetPosition(int lane, float z)
        {
            runners[lane].transform.position = new Vector3(TrackBuilder.LaneX(lane, laneCount, laneWidth), 0f, z);
        }

        // Karakterlerin üstüne isim etiketi çizer. guiScale: arayüz ölçeği, you/bot: çevrilmiş etiketler.
        public void DrawLabels(float guiScale, string youLabel, string botLabel)
        {
            if (cam == null || runners == null) return;
            if (nameStyle == null) nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            for (int i = 0; i < laneCount; i++)
            {
                if (!active[i]) continue;
                Vector3 screen = cam.WorldToScreenPoint(runners[i].transform.position + Vector3.up * 2.3f);
                if (screen.z <= 0f) continue;

                string label = names[i];
                if (i == localSlot) label = youLabel;
                else if (isBot[i]) label = botLabel;

                nameStyle.normal.textColor = i == localSlot ? new Color(1f, 0.92f, 0.4f) : Color.white;
                float x = screen.x / guiScale;
                float y = (Screen.height - screen.y) / guiScale;
                GUI.Label(new Rect(x - 50f, y - 10f, 100f, 20f), label, nameStyle);
            }
        }

        // Ekran görüntüsü/test için: karakterleri doğrudan verilen konumlara koyar.
        public void SnapPositions(float[] zPositions, bool running)
        {
            racing = running;
            for (int i = 0; i < laneCount && i < zPositions.Length; i++)
            {
                currentZ[i] = zPositions[i];
                targetZ[i] = zPositions[i];
                SetPosition(i, zPositions[i]);
                runners[i].state = running ? CharacterState.Run : CharacterState.Idle;
                runners[i].Pose(i * 1.3f);
            }
            cameraX = TrackBuilder.LaneX(localSlot, laneCount, laneWidth) * CameraFollowFactor;
            raceCamera.Frame(currentZ[localSlot], cameraX);
        }

        public void SetAllActive(string[] playerNames, int local)
        {
            localSlot = local;
            for (int i = 0; i < laneCount; i++)
            {
                active[i] = true;
                names[i] = playerNames[i];
                runners[i].gameObject.SetActive(true);
            }
        }
    }
}
