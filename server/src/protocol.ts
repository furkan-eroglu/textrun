// İstemci ile sunucu arasındaki mesaj numaraları (op code). Ayrıntı: docs/PROTOCOL.md
export const OP = {
  // istemci -> sunucu
  SUBMIT: 1, // { w: "kelime" }

  // sunucu -> istemci
  SNAPSHOT: 100, // odaya girene mevcut durum
  ROSTER: 101, // lobide oyuncu listesi değişti
  COUNTDOWN: 102, // geri sayım başladı, kelimeler gönderildi
  GO: 103, // yarış başladı
  PROGRESS: 104, // oyuncuların ilerlemesi
  ACK: 105, // gönderilen kelimenin sonucu (yalnızca gönderene)
  RESULTS: 106, // yarış bitti, sıralama
} as const;

export const REJECT = {
  LANGUAGE_MISMATCH: "language_mismatch",
  ROOM_FULL: "room_full",
  ROOM_CLOSED: "room_closed",
} as const;
