# Teknik Kararlar

Bu dosya projede alınan önemli kararların güncel halini tutar.

## Teknoloji

| Konu | Karar | Neden |
|---|---|---|
| Oyun motoru | Unity 6 (6000.5), C# | Mobil reklam ve satın alma ekosistemi olgun, tek kod tabanıyla Android ve iOS |
| Sunucu | Nakama 3.41, TypeScript çalışma zamanı | Hesap, eşleştirme, maç, depolama, sıralama tablosu tek pakette; kural kodu Node'da test edilebiliyor |
| Veritabanı | PostgreSQL 16 (Nakama ile) | Nakama'nın desteklediği standart seçenek |
| Canlı ortam | Hetzner Cloud (Nürnberg), Docker Compose: Nakama + PostgreSQL + Caddy | Ucuz, Türkiye'ye yakın; Caddy HTTPS sertifikasını kendiliğinden alır ve yeniler |
| Klavye | Telefonun yerel (sistem) klavyesi | Oyuncu kendi alışkın klavyesiyle yarışır; tuşlar kabul/ret edilmiş harf akışına indirgenir |
| Görsel tarz | Low-poly 3B, tamamı kodla üretilen (`MeshKit`) karakter, eşya ve arena | Harici model gerektirmez, her eşya sunucudaki kimliğiyle birebir eşleşir, uygulama küçük kalır (yaklaşık 20 MB) |
| Ses | Kodla üretilen sesler (`SoundSynth`), yalnızca atıfı verilmiş iki dış ses | Telif sorunu olmaz, uygulama küçük kalır |
| Arayüz | IMGUI (geçici), uGUI'ye geçiş planlı | Oyun tasarımı oturana kadar hızlı geliştirme; taşıma ekran ekran ayrı kopyada yapılacak |
| Platform sırası | Önce Android, sonra iOS | Android'de Mac gerekmez; iOS için bulut derleme ve TestFlight |

## Temel ilkeler

- **Sunucu hakemdir.** Altın, elmas, XP, sandık, mağaza, satın alma ve yarış sonucu yalnızca sunucuda hesaplanır ve yazılır. İstemci tahmin eder, sunucu düzeltir.
- **Türkçe ve İngilizce eşleştirme kesin olarak ayrıdır.** Kural sunucuda, hem herkese açık lobilerde hem özel odalarda zorunlu tutulur.
- **Kozmetikler hiçbir oyun avantajı vermez.**
- **Tüm olasılıklar, fiyatlar ve süreler ayardır**, kodun içine dağınık gömülmez (`server/src/economy.ts`, `config.ts`).
- **Özel odalar XP ve altın vermez**, iki hesapla birbirini yenerek ödül kasılamaz.
- **Gizli değerler depoda tutulmaz.** Veritabanı şifresi, oturum ve yönetim anahtarları sunucuda üretilir (`server/deploy`).

## Oyun kararları

| Konu | Karar |
|---|---|
| Yarış | 5 kişilik; boş koltukları botlar doldurur ve bot olarak etiketlenir |
| Yazma | İki satırda birbirinden bağımsız kelimeler; yanlış tuş yok sayılır, boşlukla kelime gönderilir |
| İlerleme | Her seviye atlamada bir sandık (1-9 bronz, 10-19 gümüş, 20+ altın) |
| Para birimleri | Altın (oynayarak), elmas (ileride gerçek parayla) |
| Mağaza | Her oyuncuya özel 4 eşya, altın sandık (yenileme başına bir), elmas sandık, elmasla yenileme; her gece 00.00 (Türkiye saati) yenilenir |
| Reklam sandığı | Günde bir kez, gece 00.00'da yenilenir; envantere girmeden doğrudan açılır |
| Kozmetik | Kafa (şapka ve saç), üst, alt, ayakkabı, pelerin; her kategoride en az 10 eşya, beş nadirlik, üç ten rengi, iki beden tipi |
| Sıralama | Seviyeye göre ilk 10 ve oyuncunun kendi sırası |
| Lisans | Kaynak kod portföy için herkese açık, tüm hakları saklıdır (ticari oyun) |

## Açık konular

- Gerçek ödüllü reklam (reklam SDK'sı) ve gerçek parayla elmas satışı henüz bağlı değil; Google Play geliştirici hesabı gerekiyor.
- iPhone sürümü: Apple Developer hesabı ve bulut derleme ile hiç denenmedi.
- Alan adı alınınca canlı sunucunun geçici adresi değişecek ve sunucu anahtarı yenilenecek.
- Hesapta seçili dilin sunucuda saklanması, mağlubiyet sayısı, Google ve Apple ile giriş.
- Arayüzün uGUI'ye taşınması.
- Yayın öncesi test paralarının (yeni hesaba verilen altın ve elmas) sıfırlanması, sandık olasılıklarının oyunda gösterilmesi, Krediler ekranı.
