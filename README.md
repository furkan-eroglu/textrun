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
