# TextRun

Çok oyunculu (5 kişilik) yazı yarışı oyunu. Android ve iOS.

## Klasörler

- `client/` : Unity oyun projesi
- `server/` : Nakama sunucu mantığı ve Docker ayarları
- `content/words/` : Türkçe ve İngilizce kelime listeleri
- `docs/` : Kararlar ve belgeler

Alınan teknik kararlar için `docs/KARARLAR.md` dosyasına bakın.

## Kelime listeleri

Kaynak: `content/words/` (`tr.txt`, `en.txt`, `config.json`, isteğe bağlı `blocklist.txt`).

```
node tools/words/validate.mjs   # listeleri kontrol eder
node tools/words/sync.mjs       # Unity'ye (client/Assets/Resources/Words) kopyalar
```

Listeleri değiştirdikten sonra önce `validate`, sonra `sync` çalıştırılır.

## Sunucu (Nakama)

Yerel geliştirme sunucusu Docker ile çalışır (Docker Desktop açık olmalı):

```
cd server
npm install          # ilk seferde
npm run build        # src/main.ts -> build/index.js
docker compose up -d # Nakama + PostgreSQL (yalnızca 127.0.0.1'den erişilir)
docker compose restart nakama   # kodu değiştirip derledikten sonra
```

- API: http://127.0.0.1:7350 , Yönetim paneli: http://127.0.0.1:7351
- Sunucu kodu `server/src/main.ts`. Nakama, kaydedilen fonksiyonları kodu statik okuyarak bulur;
  `InitModule` ve RPC fonksiyonları dosyada en üst seviyede durmalıdır (bkz. `server/build.mjs`).

## Arayüzü bilgisayarda otomatik doğrulama

Windows derlemesi alınıp kendi kendine menüden yarışa girip ekran görüntüsü alabilir
(sunucu çalışıyor olmalı). Elle oynamadan arayüz kontrolü içindir:

```
Unity -batchmode -nographics -projectPath client -buildTarget Win64 -executeMethod TextRun.EditorTools.OnlineTools.BuildWindows
client/Builds/Win/TextRun.exe -devharness -shots <klasör> -screen-width 450 -screen-height 800 -screen-fullscreen 0
```

`DevHarness` yalnızca `-devharness` verilirse çalışır; normal oyunda kendini kaldırır.
