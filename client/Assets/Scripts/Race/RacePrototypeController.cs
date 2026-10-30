using TextRun.Characters;
using UnityEngine;

namespace TextRun.Race
{
    // Görsel prototip: beş koşucu, pist ve kamera. Gerçek yarış mantığı değildir.
    // Koşucular rastgele hızlarla koşar, bitince tekrar başlar.
    public class RacePrototypeController : MonoBehaviour
    {
        public int laneCount = 5;
        public float laneWidth = 1.7f;
        public float trackLength = 60f;
        public int playerLane = 2;

        static readonly Color[] LaneColors =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.95f, 0.70f, 0.20f), new Color(0.30f, 0.70f, 0.40f),
            new Color(0.30f, 0.55f, 0.90f), new Color(0.65f, 0.40f, 0.85f)
        };

        CharacterAnimator[] runners;
        float[] speeds;
        RaceCamera raceCamera;
        float restartTimer;

        void Start()
        {
            Build();
            StartRace();
        }

        public void Build()
        {
            TrackBuilder.Build(laneCount, laneWidth, trackLength);

            runners = new CharacterAnimator[laneCount];
            speeds = new float[laneCount];
            for (int i = 0; i < laneCount; i++)
            {
                runners[i] = CharacterBuilder.Build("Runner" + (i + 1), LaneColors[i % LaneColors.Length]);
                runners[i].transform.position = new Vector3(TrackBuilder.LaneX(i, laneCount, laneWidth), 0f, 0f);
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camObject.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.82f, 0.95f);
            raceCamera = cam.GetComponent<RaceCamera>();
            if (raceCamera == null) raceCamera = cam.gameObject.AddComponent<RaceCamera>();
            raceCamera.Frame(0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.75f, 0.82f);
        }

        void StartRace()
        {
            for (int i = 0; i < laneCount; i++)
            {
                speeds[i] = Random.Range(4.4f, 6.0f);
                var pos = runners[i].transform.position;
                runners[i].transform.position = new Vector3(pos.x, 0f, 0f);
                runners[i].state = CharacterState.Run;
            }
            raceCamera.target = runners[playerLane].transform;
            restartTimer = -1f;
        }

        void Update()
        {
            if (runners == null) return;

            bool allFinished = true;
            for (int i = 0; i < laneCount; i++)
            {
                var runner = runners[i];
                if (runner.state != CharacterState.Run) continue;

                allFinished = false;
                runner.runSpeed = speeds[i] / 5f;
                runner.transform.Translate(0f, 0f, speeds[i] * Time.deltaTime, Space.World);

                if (runner.transform.position.z >= trackLength)
                    runner.state = CharacterState.Idle;
            }

            if (allFinished)
            {
                if (restartTimer < 0f) restartTimer = 2f;
                restartTimer -= Time.deltaTime;
                if (restartTimer <= 0f) StartRace();
            }
        }

        // Editörde tek kare görüntü almak için: koşucuları farklı mesafelere koyup poz verir.
        public void PreviewPose(float baseZ)
        {
            float[] offsets = { 1.0f, -0.4f, 2.0f, 0.4f, -1.0f };
            for (int i = 0; i < laneCount; i++)
            {
                runners[i].state = CharacterState.Run;
                var pos = runners[i].transform.position;
                runners[i].transform.position = new Vector3(pos.x, 0f, baseZ + offsets[i % offsets.Length]);
                runners[i].Pose(i * 1.3f);
            }
            raceCamera.Frame(runners[playerLane].transform.position.z);
        }
    }
}
