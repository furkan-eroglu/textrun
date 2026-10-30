# Yarış Protokolü (istemci <-> sunucu)

Unity istemcisinin Nakama ile nasıl konuşacağını anlatır. Sunucu kodu: `server/src`.

## Akış

1. Oyuncu Nakama'ya cihazla giriş yapar (jeton alır) ve WebSocket bağlantısı açar.
2. `find_race` RPC'sini `{ "lang": "tr" }` ile çağırır. Yanıt: `{ "matchId": "...", "lang": "tr" }`.
3. `joinMatch(matchId, null, { lang: "tr" })` ile odaya katılır. **Metadata'daki `lang` zorunludur**, odanın diliyle aynı değilse sunucu reddeder.
4. Sunucu mesaj yollar (aşağıdaki tablo). Oyuncu yarış başlayınca kelimeleri `SUBMIT` ile gönderir.
5. Oda dolu ya da kapalıysa katılma hata verir; istemci `find_race`'i yeniden çağırır.

## Katılma hata kodları (`rejectMessage`)

| Kod | Anlamı | Arayüz mesajı |
|---|---|---|
| `language_mismatch` | Oyuncunun dili odanın diliyle uyuşmuyor | "Bu oda başka bir dilde" |
| `room_full` | Oda 5 kişiyle dolu | yeniden ara |
| `room_closed` | Yarış başlamış, yeni oyuncu alınmıyor | yeniden ara |

## Mesajlar (op code)

Veri, UTF-8 JSON metnidir.

| Op | Yön | Ad | Gövde |
|---|---|---|---|
| 1 | istemci → sunucu | `SUBMIT` | `{ "w": "kelime" }` — oyuncu boşluğa basınca yazdığı kelime |
| 100 | sunucu → istemci | `SNAPSHOT` | Odaya girene mevcut durum (yeniden bağlanana da gider) |
| 101 | sunucu → hepsi | `ROSTER` | Lobide oyuncu listesi değişti |
| 102 | sunucu → hepsi | `COUNTDOWN` | Geri sayım başladı. **Kelime listesi burada gelir** |
| 103 | sunucu → hepsi | `GO` | Yarış başladı |
| 104 | sunucu → hepsi | `PROGRESS` | Oyuncuların ilerlemesi (her değişimde) |
| 105 | sunucu → gönderen | `ACK` | Gönderilen kelimenin sonucu |
| 106 | sunucu → hepsi | `RESULTS` | Yarış bitti, sıralama |

### Ortak gövde (`SNAPSHOT`, `ROSTER`, `COUNTDOWN`)

```json
{
  "phase": "lobby | countdown | racing | finished",
  "lang": "tr",
  "serverNow": 1793394863031,
  "lobbyEndsAt": 1793394878031,
  "startAt": 1793394866031,
  "words": ["elma", "okul", "..."],
  "players": [
    { "id": "...", "name": "Ali", "isBot": false, "slot": 0, "index": 0, "connected": true, "finished": false, "disqualified": false }
  ],
  "results": null
}
```

- `serverNow` / `startAt`: sunucu saati (ms). İstemci `startAt - serverNow` farkından kendi geri sayımını gösterir; saat farkını önemsemez.
- `lobbyEndsAt`: lobide botlarla doldurma anı (0 = henüz sayaç başlamadı).
- `words` lobide boştur; geri sayım başlayınca dolar. Herkese **aynı** liste gider.
- `players[].isBot = true` olanlar arayüzde **"Bot" etiketiyle** gösterilmelidir.
- `index`: oyuncunun sıradaki kelime numarası; ilerleme = `index / words.length`.

### `ACK`

```json
{ "ok": true, "kind": "correct | wrong | finished | disqualified", "index": 12 }
```

- **Yanlış kelime ilerletmez.** `ok:false` gelirse `index` değişmez; oyuncu aynı kelimeyi doğru yazana kadar ilerleyemez. İstemci kelimeyi kırmızı gösterip girişi temizlemelidir.
- Yarış başlamadan gönderilen kelimeler sessizce yok sayılır (ACK gelmez).

### `RESULTS`

```json
{
  "serverNow": 1793394900000,
  "results": [
    { "userId": "...", "name": "Ali", "isBot": false, "rank": 1, "status": "finished",
      "finishMs": 31200, "wpm": 64.2, "accuracy": 0.976, "correctWords": 40, "errors": 1 }
  ]
}
```

`status`: `finished` (bitirdi), `dnf` (bitiremedi), `disqualified` (insan için imkansız hız).

## Kurallar

- **Dil:** Türkçe ve İngilizce asla aynı odada olmaz. Oda dili sunucuda sabittir ve katılma sırasında doğrulanır.
- **Lobi:** İlk oyuncudan 15 sn sonra, ya da 5 insan toplanınca yarış başlar. Boş yerleri botlar doldurur.
- **Geri sayım:** 3 sn. Başlama anını sunucu belirler.
- **Yarış:** 40 kelime. İlk 3 oyuncu bitirince 20 sn beklenir, bitirmeyenler `dnf` olur. Üst sınır 180 sn.
- **Hız sınırı:** İlk 5 kelimeden sonra 250 WPM'i aşan oyuncu diskalifiye edilir.
- **Bağlantı kopması:** 30 sn içinde aynı hesapla aynı odaya `joinMatch` yapılırsa yarışa devam edilir (`SNAPSHOT` gelir). Dönmezse `dnf` olur.
- **Ayarlar** `server/src/config.ts` içindedir; canlı sunucuda `TEXTRUN_...` ortam değişkenleriyle değiştirilebilir.

## Test

Birim testleri (Nakama gerekmez): `cd server && npm test`

Uçtan uca testler (gerçek sunucuya sahte oyuncularla bağlanır). Sunucu test ayarlarıyla başlatılır:

```powershell
cd server
$env:TEXTRUN_LOBBY_WAIT_MS="3000"; $env:TEXTRUN_WORD_COUNT="8"; $env:TEXTRUN_MAX_WPM="100000"
$env:TEXTRUN_FINISH_GRACE_MS="3000"; $env:TEXTRUN_COUNTDOWN_MS="1500"
docker compose up -d --force-recreate nakama
node test/e2e.mjs
```

Hile testi için `TEXTRUN_MAX_WPM` ayarlanmadan (varsayılan 250) başlatıp `E2E_CHEAT=1 node test/e2e.mjs` çalıştırılır.
