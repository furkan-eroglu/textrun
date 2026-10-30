using UnityEngine;

namespace TextRun.Audio
{
    // Yarış sesleri: geri sayım tikleri, başlangıç düdüğü, yarış müziği ve bitiş jingle'ı.
    // Sesler SoundSynth ile kodla üretilir. Açık/kapalı ayarı cihazda saklanır.
    public class RaceAudio : MonoBehaviour
    {
        const string SoundKey = "textrun.sound";
        const float MusicVolume = 0.22f;

        AudioSource sfx;
        AudioSource music;
        AudioClip tick, whistle, loop, fanfarePodium, fanfareDone;

        public bool Enabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) StopMusic();
            }
        }

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = MusicVolume;

            tick = SoundSynth.ToClip("tick", SoundSynth.TickSamples());
            whistle = SoundSynth.ToClip("whistle", SoundSynth.WhistleSamples());
            loop = SoundSynth.ToClip("raceLoop", SoundSynth.RaceLoopSamples());
            fanfarePodium = SoundSynth.ToClip("fanfarePodium", SoundSynth.FanfareSamples(true));
            fanfareDone = SoundSynth.ToClip("fanfareDone", SoundSynth.FanfareSamples(false));
        }

        public void Tick()
        {
            if (Enabled) sfx.PlayOneShot(tick, 0.8f);
        }

        // Yarış başlangıcı: düdük + müzik
        public void Go()
        {
            if (!Enabled) return;
            sfx.PlayOneShot(whistle, 0.9f);
            music.clip = loop;
            music.Play();
        }

        public void StopMusic()
        {
            if (music != null) music.Stop();
        }

        // Yarış bitişi: müzik durur, jingle çalar. İlk üçe girenler için zafer fanfarı, diğerleri için kısa "bitti" sesi.
        public void Finish(bool onPodium)
        {
            StopMusic();
            if (Enabled) sfx.PlayOneShot(onPodium ? fanfarePodium : fanfareDone, 0.9f);
        }
    }
}
