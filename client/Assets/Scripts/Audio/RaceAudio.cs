using UnityEngine;

namespace TextRun.Audio
{
    // Yarış sesleri: geri sayım tikleri, başlangıç düdüğü, yarış müziği ve bitiş alkışı.
    // Sesler SoundSynth ile kodla üretilir. Açık/kapalı ayarı cihazda saklanır.
    public class RaceAudio : MonoBehaviour
    {
        const string SoundKey = "textrun.sound";
        const float MusicVolume = 0.32f;

        AudioSource sfx;
        AudioSource music;
        AudioClip tick, whistle, loop, applause;

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
            applause = SoundSynth.ToClip("applause", SoundSynth.ApplauseSamples());
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

        // Yarış bitişi: müzik durur, alkış çalar. İlk üçe girenler için daha coşkulu.
        public void Finish(bool onPodium)
        {
            StopMusic();
            if (Enabled) sfx.PlayOneShot(applause, onPodium ? 1f : 0.55f);
        }
    }
}
