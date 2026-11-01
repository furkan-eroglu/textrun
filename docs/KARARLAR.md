# Teknik Kararlar

Bu dosya projede alınan önemli kararları kaydeder. Yeni karar alındıkça eklenir.

## Onaylanan kararlar

| Konu | Karar | Neden |
|---|---|---|
| Oyun motoru | Unity 6 LTS (C#) | Mobil reklam/satın alma ekosistemi olgun, kostüm sistemi kolay |
| Sunucu | Nakama (kendi sunucumuzda) | Hesap, eşleştirme, oda, cüzdan, IAP doğrulama tek pakette |
| Veritabanı | PostgreSQL (Nakama ile) | Nakama'nın desteklediği standart seçenek |
| Klavye | Yerel (sistem) klavye | Kullanıcı kendi alışkın klavyesiyle yarışmak ister |
| Görsel tarz | Low-poly 3D | Mobilde hızlı, üretimi kolay |
| Platform sırası | Önce Android, sonra iOS | Android'de Mac gerekmez |
| iOS derleme | Bulut derleme (Mac yok), TestFlight ile test | Mac mini şimdilik alınmayacak |

## Temel ilkeler

- Sunucu hakemdir: altın, elmas, XP, sandık, satın alma ve yarış sonucu yalnızca sunucuda hesaplanır.
- Türkçe ve İngilizce eşleştirme kuyrukları kesin olarak ayrıdır.
- Kozmetikler hiçbir oyun avantajı vermez.
- Tüm olasılıklar ve fiyatlar ayar dosyalarında tutulur, kodun içine gömülmez.

## Açık konular

- Yerel klavyenin iPhone ve Xiaomi'deki davranışı prototipte test edilecek.
- Apple Developer hesabı iPhone testine gelmeden açılacak.
- Test için kullanılacak Xiaomi modeli ve klavyesi öğrenilecek.

## Klavye: oyunun kendi ekran klavyesi (2026-11-01)
Telefonun sistem klavyesinin metnini okumak gercek cihazda (Xiaomi, Gboard) guvenilmez cikti: metin otomatik duzeltme ve yazim alani yuzunden bozuluyor, kelimeler basa sariyordu. Karar: oyun kendi ekran klavyesini cizer (Turkce Q / Ingilizce QWERTY, silme tusu yok cunku yanlis harfler yok sayilir). Dokunuslar dogrudan oyuna gider. Bilgisayarda fiziksel klavye de calisir. Eski KeyboardInput yalnizca KeyboardTest prototipinde kaldi.
