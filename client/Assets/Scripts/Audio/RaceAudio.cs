using UnityEngine;

namespace TextRun.Audio
{
    // Yarış sesleri: geri sayım tikleri, başlangıç düdüğü, yarış müziği ve bitiş jingle'ı.
    // Sesler SoundSynth ile kodla üretilir. Açık/kapalı ayarı cihazda saklanır.
    public class RaceAudio : MonoBehaviour
    {
        const string SoundKey = "textrun.sound";
        const float MusicVolume = 0.22f;
        const float CheerVolume = 0.9f;
        public const float CheerSeconds = 5.5f;  // alkış + havai fişek sesinin çalacağı süre
        const float CheerFadeSeconds = 1.5f;     // sonunda yumuşakça kısılma

        AudioSource sfx;
        AudioSource music;
        AudioSource cheer;
        float cheerStartedAt = -999f;
        float cheerLength;
        AudioClip tick, click, alertTick, alertFinal, whistle, loop, fanfarePodium, fanfareDone, applause, matchFound;

        public bool Enabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) { StopMusic(); StopCheer(); }
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

            cheer = gameObject.AddComponent<AudioSource>();
            cheer.playOnAwake = false;

            // Assets/Resources/Audio içine aynı adla bir ses dosyası (.ogg/.wav/.mp3) konursa o kullanılır;
            // yoksa kodla üretilen geçici ses çalar. Dosya adları: tick, click, alert_tick, alert_final, whistle, race_loop, finish_win, finish_done, applause, match_found
            tick = Custom("tick") ?? SoundSynth.ToClip("tick", SoundSynth.TickSamples());
            click = Custom("click") ?? SoundSynth.ToClip("click", SoundSynth.ClickSamples());
            alertTick = Custom("alert_tick") ?? SoundSynth.ToClip("alertTick", SoundSynth.AlertTickSamples(false));
            alertFinal = Custom("alert_final") ?? SoundSynth.ToClip("alertFinal", SoundSynth.AlertTickSamples(true));
            whistle = Custom("whistle") ?? SoundSynth.ToClip("whistle", SoundSynth.WhistleSamples());
            loop = Custom("race_loop") ?? SoundSynth.ToClip("raceLoop", SoundSynth.RaceLoopSamples());
            fanfarePodium = Custom("finish_win") ?? SoundSynth.ToClip("fanfarePodium", SoundSynth.FanfareSamples(true));
            applause = Custom("applause") ?? SoundSynth.ToClip("applause", SoundSynth.ApplauseSamples());
            matchFound = Custom("match_found") ?? SoundSynth.ToClip("matchFound", SoundSynth.MatchFoundSamples());
            applause.LoadAudioData(); // ilk çalışta kasma olmasın
            fanfareDone = Custom("finish_done") ?? SoundSynth.ToClip("fanfareDone", SoundSynth.FanfareSamples(false));
        }

        static AudioClip Custom(string name)
        {
            return Resources.Load<AudioClip>("Audio/" + name);
        }

        // Düğme tıklaması (ses açıkken)
        public void Click()
        {
            if (Enabled) sfx.PlayOneShot(click, 0.55f);
        }

        // Yarış bitiş geri sayımı: her saniyede gerilim tiki; son 3 saniye daha tiz ve vurgulu
        public void AlertTick(bool urgent)
        {
            if (Enabled) sfx.PlayOneShot(urgent ? alertFinal : alertTick, urgent ? 0.9f : 0.7f);
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

        // Karşılaşma bulundu uyarısı
        public void MatchFound()
        {
            if (Enabled) sfx.PlayOneShot(matchFound, 0.9f);
        }

        // Yarışı kendin bitirdiğinde (sonuçlar gelmeden): alkış. Müzik sürer, diğerleri hâlâ yarışıyor.
        // Uzun bir kayıt (örn. Resources/Audio/applause.mp3) konmuşsa yalnızca ilk CheerSeconds saniyesi çalınır ve sonunda kısılır.
        public void Applause()
        {
            if (!Enabled) return;

            cheer.clip = applause;
            cheer.volume = CheerVolume;
            cheer.Play();
            cheerStartedAt = Time.realtimeSinceStartup;
            cheerLength = Mathf.Min(CheerSeconds, applause.length);
        }

        void Update()
        {
            if (!cheer.isPlaying) return;

            float t = Time.realtimeSinceStartup - cheerStartedAt;
            if (t >= cheerLength) { cheer.Stop(); return; }
            cheer.volume = CheerVolume * Mathf.Clamp01((cheerLength - t) / CheerFadeSeconds);
        }

        public void StopCheer()
        {
            if (cheer != null) cheer.Stop();
        }

        public void StopMusic()
        {
            if (music != null) music.Stop();
        }

        // Yarış bitişi: müzik durur, jingle çalar. İlk üçe girenler için zafer fanfarı, diğerleri için kısa "bitti" sesi.
        public void Finish(bool onPodium)
        {
            StopMusic();
            StopCheer();
            if (Enabled) sfx.PlayOneShot(onPodium ? fanfarePodium : fanfareDone, 0.9f);
        }
    }
}
