// Nakama'nın yüklediği giriş noktası. Sunucu başlarken InitModule bir kez çağrılır.
// Nakama, kaydedilen fonksiyonları isimleriyle arar; hepsi bu dosyadan import edilip aynı isimle kullanılmalıdır (bkz. build.mjs).
import { matchInit, matchJoin, matchJoinAttempt, matchLeave, matchLoop, matchSignal, matchTerminate } from "./match.ts";
import { isSupportedLang } from "./words.ts";

export function pingRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  return JSON.stringify({ ok: true, message: "TextRun sunucusu calisiyor", time: Date.now() });
}

// Normal oyun: oyuncunun seçtiği dilin açık lobisini bulur, yoksa yeni lobi açar. Dönen matchId ile odaya katılınır.
export function findRaceRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");

  let lang: unknown;
  try {
    lang = JSON.parse(payload).lang;
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (!isSupportedLang(lang)) throw new Error("desteklenmeyen dil");

  return JSON.stringify({ matchId: findOrCreateLobby(nk, lang), lang: lang });
}

// Her dil için tek bir "açık lobi" kaydı tutulur. Nakama'nın oda arama dizini yeni odaları gecikmeyle (yaklaşık 1 sn) gördüğü için
// arama yerine bu kayıt kullanılır. Kayıt sürüm kontrolüyle yazılır: iki oyuncu aynı anda yeni lobi açarsa yalnızca biri kazanır, diğeri onunkini kullanır.
const SYSTEM_USER = "00000000-0000-0000-0000-000000000000";
const LOBBY_COLLECTION = "open_lobby";
const MAX_PLAYERS = 5;

function isJoinable(nk: nkruntime.Nakama, matchId: string): boolean {
  const match = nk.matchGet(matchId);
  if (!match) return false;
  try {
    return JSON.parse(match.label).open === true && match.size < MAX_PLAYERS;
  } catch (e) {
    return false;
  }
}

function findOrCreateLobby(nk: nkruntime.Nakama, lang: string): string {
  for (let attempt = 0; attempt < 4; attempt++) {
    const records = nk.storageRead([{ collection: LOBBY_COLLECTION, key: lang, userId: SYSTEM_USER }]);
    const record = records.length > 0 ? records[0] : null;

    if (record && isJoinable(nk, record.value.matchId)) return record.value.matchId;

    const matchId = nk.matchCreate("race", { lang: lang, kind: "public" });
    try {
      nk.storageWrite([
        {
          collection: LOBBY_COLLECTION,
          key: lang,
          userId: SYSTEM_USER,
          value: { matchId: matchId },
          version: record ? record.version : "*",
          permissionRead: 0,
          permissionWrite: 0,
        },
      ]);
      return matchId;
    } catch (e) {
      // Başka bir oyuncu aynı anda yeni lobi kaydetti; döngü başa dönüp onunkini okuyacak. Bizim boş lobimiz 30 sn sonra kapanır.
    }
  }
  throw new Error("lobi bulunamadi");
}

// ---------- Özel odalar ----------
// Kurucu oda açar, 5 harfli kodu arkadaşlarına söyler. Kod -> oda eşlemesi sistem kaydında tutulur.
const ROOM_COLLECTION = "room_code";
const CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // karışan harfler (I, L, O, 0, 1) yok
const CODE_LENGTH = 5;

function newRoomCode(): string {
  let code = "";
  for (let i = 0; i < CODE_LENGTH; i++) code += CODE_ALPHABET.charAt(Math.floor(Math.random() * CODE_ALPHABET.length));
  return code;
}

function normalizeCode(raw: unknown): string {
  return typeof raw === "string" ? raw.trim().toUpperCase().replace(/[^A-Z0-9]/g, "") : "";
}

export function createRoomRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");

  let lang: unknown;
  try {
    lang = JSON.parse(payload).lang;
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (!isSupportedLang(lang)) throw new Error("desteklenmeyen dil");

  for (let attempt = 0; attempt < 10; attempt++) {
    const code = newRoomCode();
    const existing = nk.storageRead([{ collection: ROOM_COLLECTION, key: code, userId: SYSTEM_USER }]);
    if (existing.length > 0 && nk.matchGet(existing[0].value.matchId)) continue; // kod kullanımda

    const matchId = nk.matchCreate("race", { lang: lang, kind: "private", code: code });
    nk.storageWrite([{ collection: ROOM_COLLECTION, key: code, userId: SYSTEM_USER, value: { matchId: matchId }, permissionRead: 0, permissionWrite: 0 }]);
    return JSON.stringify({ matchId: matchId, code: code, lang: lang });
  }
  throw new Error("kod uretilemedi");
}

export function joinRoomRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");

  let code = "";
  try {
    code = normalizeCode(JSON.parse(payload).code);
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (code.length !== CODE_LENGTH) throw new Error("room_not_found");

  const records = nk.storageRead([{ collection: ROOM_COLLECTION, key: code, userId: SYSTEM_USER }]);
  const match = records.length > 0 ? nk.matchGet(records[0].value.matchId) : null;
  if (!match) throw new Error("room_not_found");

  let label: { lang: string; open: boolean };
  try {
    label = JSON.parse(match.label);
  } catch (e) {
    throw new Error("room_not_found");
  }
  if (!label.open) throw new Error("room_closed");
  if (match.size >= MAX_PLAYERS) throw new Error("room_full");
  return JSON.stringify({ matchId: match.matchId, code: code, lang: label.lang });
}

export function InitModule(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, initializer: nkruntime.Initializer): void {
  initializer.registerRpc("ping", pingRpc);
  initializer.registerRpc("find_race", findRaceRpc);
  initializer.registerRpc("create_room", createRoomRpc);
  initializer.registerRpc("join_room", joinRoomRpc);
  initializer.registerMatch("race", { matchInit, matchJoinAttempt, matchJoin, matchLeave, matchLoop, matchTerminate, matchSignal });
  logger.info("TextRun modulu yuklendi");
}
