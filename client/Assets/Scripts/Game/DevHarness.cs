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

            // Oyun açılınca karşılama gelir: misafir olarak girilir (gerçek sunucuda geçici hesap + otomatik ad)
            float waited = Time.realtimeSinceStartup;
            while (controller.DevPageName != "Welcome" && controller.DevPageName != "Menu" && Time.realtimeSinceStartup - waited < 30f) yield return null;
            if (controller.DevPageName == "Welcome")
            {
                yield return new WaitForSeconds(0.5f);
                yield return Shot("0-welcome-real");
                controller.DevContinueAsGuest();
            }
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

            foreach (var which in new[] { "welcome", "login", "register", "name", "settings", "guestsettings" })
            {
                controller.DevShowAccountScreen(which);
                yield return new WaitForSeconds(0.2f); // ekran değişiminin odak sıfırlaması geçsin
                if (which == "login") controller.DevFocusField("loginField"); // imlecin göründüğünü doğrulamak için
                yield return new WaitForSeconds(1.2f);
                yield return Shot("1h-account-" + which);
            }
            // Dolu kayıt formu: imleç yazının sonunda mı, şifre gizli/gösterilir mi, uyuşmayan şifre uyarısı
            controller.DevShowAccountScreen("register");
            controller.DevFillLogin("furkan_01", "gizlisifre1", "gizlisifre", false);
            yield return new WaitForSeconds(0.2f);
            controller.DevFocusField("passAgainField");
            yield return new WaitForSeconds(0.3f);
            while (!controller.DevCaretOn) yield return null;
            yield return Shot("1h-register-filled");
            controller.DevFillLogin("furkan_01", "gizlisifre1", "gizlisifre1", true);
            while (!controller.DevCaretOn) yield return null;
            yield return Shot("1h-register-revealed");
            // Envanter: kategori sekmeleri (resimli ızgara), sandıklar, sandık açılışı
            controller.DevShowInventory(0);
            controller.DevMarkNew("hat_pirate", "cape_wings");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("1j-inventory-head");
            controller.DevShowInventory(4);
            yield return new WaitForSeconds(2.0f);
            yield return Shot("1j-inventory-cape");
            controller.DevShowInventory(2);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("1j-inventory-bottom");
            controller.DevShowInventory(5);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("1j-inventory-chests");
            controller.DevShowChest(false);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("1k-chest-shaking");
            controller.DevShowChest(true, "epic");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("1l-chest-epic");
            controller.DevShowChest(true, "legendary");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("1m-chest-legendary");

            // Ana menü: karakter (giydikleriyle) ve reklam sandığı (açılabilir / geri sayımda)
            controller.DevShowInventory(0);
            controller.DevBackToMenu();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("1i-menu-character");
            controller.DevSetAdChestTimer(5 * 3600 + 120);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("1i-menu-ad-timer");

            controller.DevShowShop();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("1o-shop");
            controller.DevShowShop(true);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("1o-shop-confirm");
            controller.DevBackToMenu();
            yield return new WaitForSeconds(0.3f);

            // Eşya galerisi: her eşya ayrı karakterde (dokuzar dokuzar), arayüz gizli
            var gallery = new OutfitGallery();
            var ids = new System.Collections.Generic.List<string>(TextRun.Game.OnlineGameController.DevAllOutfitIds());
            controller.DevSuppressGui = true;
            for (int page = 0; page * 9 < ids.Count; page++)
            {
                gallery.Show(ids.GetRange(page * 9, Math.Min(9, ids.Count - page * 9)));
                yield return new WaitForSeconds(0.4f);
                yield return Shot("1n-gallery-" + page);
            }
            gallery.Clear();
            controller.DevSuppressGui = false;

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
            // Gerçek özel oda: kurucu kendini listede görmeli (1 / 5)
            controller.DevCreateRoom();
            yield return WaitForPage("RoomLobby", 10f);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("1g-room-real");
            if (controller.DevPlayerCount != 1)
                File.WriteAllText(Path.Combine(dir, "FAILED.txt"), "Ozel oda lobisinde oyuncu sayisi 1 olmali, " + controller.DevPlayerCount);
            controller.DevLeaveRoom();
            yield return new WaitForSeconds(0.8f);
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
            controller.DevShowFakeResults(2, "finished", true);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("6c-results-levelup");
            controller.DevShowFakeResults(1);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("6b-results-first-later"); // konfeti daha aşağıda olmalı
            controller.DevShowFakeResults(4);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("7-results-fourth");

            // Yarışı bitiremeyen ama listede 3. olan: yine bronz kupa ve konfeti alır (ödül yalnızca sıraya bağlı)
            controller.DevShowFakeResults(3, "dnf");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("8-results-dnf-third");

            // Misafir: sonuç ekranında XP yok, not var
            controller.DevShowFakeResults(2, "finished", false, true);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("9-results-guest");

            File.WriteAllText(Path.Combine(dir, "done.txt"), "tamam");
            Application.Quit();
        }
    }
}
