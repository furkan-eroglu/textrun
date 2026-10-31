using System;
using System.Collections;
using System.IO;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.Game
{
    // Geliştirme aracı: oyun komut satırında "-devharness -shots <klasör>" ile açılırsa kendi kendine menüden yarışa girer
    // ve belirli anlarda ekran görüntüsü alıp kapanır. Normal oyunda hiçbir şey yapmaz (kendini kaldırır).
    // Amaç: arayüzü bilgisayarda gerçek bir derlemede, elle oynamadan doğrulamak.
    public class DevHarness : MonoBehaviour
    {
        OnlineGameController controller;
        string dir;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int shots = Array.IndexOf(args, "-shots");
            if (Array.IndexOf(args, "-devharness") < 0 || shots < 0 || shots + 1 >= args.Length)
            {
                Destroy(this);
                return;
            }

            AudioListener.volume = 0f; // otomatik testte hoparlörden ses çıkmasın
            Application.runInBackground = true; // pencere odağı kaybedince oyun durmasın
            dir = args[shots + 1];
            Directory.CreateDirectory(dir);
            controller = GetComponent<OnlineGameController>();
            StartCoroutine(Run());
        }

        IEnumerator WaitForPage(string page, float timeoutSeconds)
        {
            float started = Time.realtimeSinceStartup;
            while (controller.DevPageName != page)
            {
                if (Time.realtimeSinceStartup - started > timeoutSeconds)
                {
                    File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "Sayfa gelmedi: " + page + " (son sayfa: " + controller.DevPageName + ")");
                    Application.Quit();
                    yield break;
                }
                yield return null;
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
            yield return null;
        }

        IEnumerator Run()
        {
            var audio = GetComponent<TextRun.Audio.RaceAudio>();
            audio.Enabled = true; // önceki yarım kalan çalıştırmadan kalan ayar sonucu bozmasın

            yield return WaitForPage("Menu", 30f);
            yield return new WaitForSeconds(0.25f);
            yield return Shot("1a-menu-logo-intro"); // harfler kayarak geliyor
            yield return new WaitForSeconds(1.5f);
            yield return Shot("1-menu");

            // Ses simgesinin "kapalı" (çarpılı) görünümü
            audio.Enabled = false;
            yield return new WaitForSeconds(0.3f);
            yield return Shot("1b-menu-muted");
            audio.Enabled = true;

            controller.DevShowError();
            yield return new WaitForSeconds(0.4f);
            yield return Shot("1f-error-screen");
            controller.DevBackToMenu();
            controller.DevShowPrivateScreen("menu");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("1c-private-menu");
            controller.DevShowPrivateScreen("join");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("1d-private-join");
            controller.DevShowPrivateScreen("lobby");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("1e-private-lobby");
            controller.DevBackToMenu();
            yield return new WaitForSeconds(0.4f);

            controller.DevStartSearch();
            yield return WaitForPage("Searching", 15f);
            yield return new WaitForSeconds(2f);
            yield return Shot("2-searching");
            yield return new WaitForSeconds(7f);
            yield return Shot("2b-searching-later"); // sayacın ilerlemesini görmek için

            yield return WaitForPage("MatchFound", 40f);
            yield return new WaitForSeconds(0.9f);
            yield return Shot("2c-match-found");

            yield return WaitForPage("Countdown", 10f);
            yield return new WaitForSeconds(3.3f); // 6 sn'lik geri sayımın ortası: ~3 ışık yanık
            yield return Shot("3-countdown");

            yield return WaitForPage("Racing", 15f);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("3b-go"); // ışıklar yeşil yanıp sönmeli
            yield return new WaitForSeconds(13.8f);
            yield return Shot("4-racing");

            KeyboardInput.SimulateInEditor = true; // telefon klavyesinin kaplayacağı yeri taklit eder
            yield return new WaitForSeconds(1f);
            yield return Shot("5-racing-keyboard");

            // Bitirdin + kırmızı geri sayım görünümü
            controller.DevShowFinishedEnding();
            yield return new WaitForSeconds(1.2f); // havai fisekler patlamaya baslamis olmali
            yield return Shot("5b-finished-ending");
            yield return new WaitForSeconds(1.1f);
            yield return Shot("5c-fireworks-later");

            // Sonuç ekranı: birinci ve dördüncü için sahte sonuçla görünüm kontrolü
            controller.DevShowFakeResults(1);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("6-results-first");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("6b-results-first-later"); // konfeti daha aşağıda olmalı
            controller.DevShowFakeResults(4);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("7-results-fourth");

            // Yarışı bitiremeyen ama listede 3. olan: yine bronz kupa ve konfeti alır (ödül yalnızca sıraya bağlı)
            controller.DevShowFakeResults(3, "dnf");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("8-results-dnf-third");

            File.WriteAllText(Path.Combine(dir, "done.txt"), "tamam");
            Application.Quit();
        }
    }
}
