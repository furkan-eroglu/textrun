// Nakama'nın yüklediği giriş noktası. Sunucu başlarken InitModule bir kez çağrılır.
// Nakama, kaydedilen fonksiyonları isimleriyle arar; hepsi bu dosyadan import edilip aynı isimle kullanılmalıdır (bkz. build.mjs).
import { matchInit, matchJoin, matchJoinAttempt, matchLeave, matchLoop, matchSignal, matchTerminate } from "./match.ts";
import { isSupportedLang } from "./words.ts";
import { itemById } from "./economy.ts";
import type { ChestTier } from "./economy.ts";
import { cleanName, isChestTier } from "./profile.ts";
import { createLeaderboard, leaderboardView, recordXp } from "./leaderboard.ts";
import { AD_TIER, claimAdChestFor, equipFor, inventoryView, setBodyFor, isRegistered, openChestFor, profileView, readProfile, saveProfile, shopFor, shopView } from "./profileStore.ts";

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

// Beklenen hatalar (yanlış kod, kapalı oda...) 500 yerine uygun kodla döner; istemci kitaplığı 500'leri tekrar deneyip oyuncuyu bekletir.
const CODE_INVALID_ARGUMENT = 3;
const CODE_NOT_FOUND = 5;
const CODE_FAILED_PRECONDITION = 9;

function fail(message: string, code: number): never {
  throw { message: message, code: code };
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
  let lang: unknown;
  try {
    const request = JSON.parse(payload);
    code = normalizeCode(request.code);
    lang = request.lang;
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (!isSupportedLang(lang)) throw new Error("desteklenmeyen dil");
  if (code.length !== CODE_LENGTH) fail("room_not_found", CODE_NOT_FOUND);

  const records = nk.storageRead([{ collection: ROOM_COLLECTION, key: code, userId: SYSTEM_USER }]);
  const match = records.length > 0 ? nk.matchGet(records[0].value.matchId) : null;
  if (!match) fail("room_not_found", CODE_NOT_FOUND);

  let label: { lang: string; open: boolean };
  try {
    label = JSON.parse(match.label);
  } catch (e) {
    fail("room_not_found", CODE_NOT_FOUND);
  }
  if (label.lang !== lang) fail("language_mismatch", CODE_FAILED_PRECONDITION); // yalnızca oda kurucusuyla aynı oyun dilindekiler girebilir
  if (!label.open) fail("room_closed", CODE_FAILED_PRECONDITION);
  if (match.size >= MAX_PLAYERS) fail("room_full", CODE_FAILED_PRECONDITION);
  return JSON.stringify({ matchId: match.matchId, code: code, lang: label.lang });
}

// ---------- Profil ----------
export function getProfileRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");
  if (!isRegistered(nk, ctx.userId)) return JSON.stringify(profileView(readProfile(nk, ctx.userId).profile, true));
  return JSON.stringify(profileView(readProfile(nk, ctx.userId).profile, false));
}

// Oyuncu adı (ilk açılışta ve ayarlardan). Geçersizse 400 ile "name_invalid" döner.
export function setNameRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");

  let raw: unknown;
  try {
    raw = JSON.parse(payload).name;
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (!isRegistered(nk, ctx.userId)) fail("guest_account", CODE_INVALID_ARGUMENT); // misafir takma ad seçemez
  const name = cleanName(raw);
  if (name === null) fail("name_invalid", CODE_INVALID_ARGUMENT);

  const stored = readProfile(nk, ctx.userId);
  stored.profile.name = name;
  saveProfile(nk, ctx.userId, stored);
  recordXp(nk, logger, ctx.userId, stored.profile); // liderlik tablosundaki ad da güncellenir
  return JSON.stringify(profileView(stored.profile, false));
}

// Liderlik tablosu: seviyesi en yüksek ilk 10 oyuncu ve isteyenin kendi sırası. Misafir de görebilir (kendi sırası olmaz).
// Dönen değer: { top: [{ rank, name, level, xp, me }], me: { rank, name, level, xp, me } | null }
export function getLeaderboardRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");
  if (isRegistered(nk, ctx.userId)) recordXp(nk, logger, ctx.userId, readProfile(nk, ctx.userId).profile); // kendi kaydı güncel olsun
  return JSON.stringify(leaderboardView(nk, ctx.userId));
}

// Sahip olunan eşyalar (envanter ekranı). Misafirin envanteri yoktur.
export function getInventoryRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");
  if (!isRegistered(nk, ctx.userId)) return JSON.stringify({ items: [], equipped: [], skin: 0, gender: "m" });
  return JSON.stringify(inventoryView(readProfile(nk, ctx.userId).profile));
}

// Bir eşyayı takar ({"itemId"}) veya bir kategoriyi boşaltır ({"category"}). Eşya oyuncunun değilse "not_owned".
export function equipItemRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");

  let request: { itemId?: unknown; category?: unknown };
  try {
    request = JSON.parse(payload);
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (request.itemId === undefined && request.category === undefined) fail("bad_request", CODE_INVALID_ARGUMENT);
  if (!isRegistered(nk, ctx.userId)) fail("guest_account", CODE_INVALID_ARGUMENT);

  const profile = equipFor(nk, logger, ctx.userId, request);
  if (!profile) fail("not_owned", CODE_FAILED_PRECONDITION);
  return JSON.stringify(inventoryView(profile!));
}

// Mağaza. get_shop: günün mağazası. shop_buy {"slot"}: 0-3 eşya, 4 altınla sandık, 5 elmasla sandık. shop_refresh: elmasla yeniler.
// Hatalar: "bad_slot", "already_bought", "not_enough", misafir "guest_account". Dönen değer: { shop, profile }.
function shopRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, action: "open" | "buy" | "refresh", slot?: unknown): string {
  if (!ctx.userId) throw new Error("giris gerekli");
  if (!isRegistered(nk, ctx.userId)) fail("guest_account", CODE_INVALID_ARGUMENT);
  const result = shopFor(nk, logger, ctx.userId, action, slot);
  if (result.outcome === "bad_slot") fail("bad_slot", CODE_INVALID_ARGUMENT);
  if (result.outcome !== "ok") fail(result.outcome, CODE_FAILED_PRECONDITION);
  return JSON.stringify({ shop: shopView(result.profile, Date.now()), profile: profileView(result.profile, false) });
}

// Ten rengi (0 açık, 1 kahverengi, 2 koyu) ve cinsiyet ("m" | "f"). Geçersizse "bad_body", misafir "guest_account".
export function setBodyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");
  let request: { skin?: unknown; gender?: unknown };
  try {
    request = JSON.parse(payload);
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (!isRegistered(nk, ctx.userId)) fail("guest_account", CODE_INVALID_ARGUMENT);
  const profile = setBodyFor(nk, logger, ctx.userId, request.skin, request.gender);
  if (!profile) fail("bad_body", CODE_INVALID_ARGUMENT);
  return JSON.stringify(inventoryView(profile!));
}

// Günlük reklam sandığı: günde bir kez (gece 00.00'da yenilenir). Yanıt open_chest ile aynı biçimdedir.
// Bugün açıldıysa "ad_not_ready", misafir "guest_account".
// NOT: reklamın gerçekten izlendiği henüz doğrulanmıyor (reklam SDK'sı eklenince sunucu doğrulaması gelecek).
export function claimAdChestRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");
  if (!isRegistered(nk, ctx.userId)) fail("guest_account", CODE_INVALID_ARGUMENT);
  const result = claimAdChestFor(nk, logger, ctx.userId);
  if (!result) fail("ad_not_ready", CODE_FAILED_PRECONDITION);
  const won = itemById(result!.opening.itemId)!;
  return JSON.stringify({
    tier: AD_TIER,
    itemId: won.id,
    category: won.category,
    rarity: won.rarity,
    tr: won.tr,
    en: won.en,
    duplicate: result!.opening.duplicate,
    goldRefund: result!.opening.goldRefund,
    profile: profileView(result!.profile, false),
  });
}

export function getShopRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  return shopRpc(ctx, logger, nk, "open");
}

export function shopBuyRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  let slot: unknown;
  try {
    slot = JSON.parse(payload).slot;
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  return shopRpc(ctx, logger, nk, "buy", slot);
}

export function shopRefreshRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  return shopRpc(ctx, logger, nk, "refresh");
}

// Envanterdeki bir sandığı açar. Sandık yoksa "no_chest", geçersiz tür "bad_chest", misafirse "guest_account".
export function openChestRpc(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
  if (!ctx.userId) throw new Error("giris gerekli");

  let tier: unknown;
  try {
    tier = JSON.parse(payload).tier;
  } catch (e) {
    throw new Error("gecersiz istek");
  }
  if (!isChestTier(tier)) fail("bad_chest", CODE_INVALID_ARGUMENT);
  if (!isRegistered(nk, ctx.userId)) fail("guest_account", CODE_INVALID_ARGUMENT);

  const result = openChestFor(nk, logger, ctx.userId, tier as ChestTier);
  if (!result) fail("no_chest", CODE_FAILED_PRECONDITION);
  const won = itemById(result!.opening.itemId)!;
  return JSON.stringify({
    tier: tier,
    itemId: won.id,
    category: won.category,
    rarity: won.rarity,
    tr: won.tr,
    en: won.en,
    duplicate: result!.opening.duplicate,
    goldRefund: result!.opening.goldRefund,
    profile: profileView(result!.profile, false),
  });
}

export function InitModule(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, initializer: nkruntime.Initializer): void {
  initializer.registerRpc("ping", pingRpc);
  initializer.registerRpc("find_race", findRaceRpc);
  initializer.registerRpc("create_room", createRoomRpc);
  initializer.registerRpc("join_room", joinRoomRpc);
  initializer.registerRpc("get_profile", getProfileRpc);
  initializer.registerRpc("set_name", setNameRpc);
  initializer.registerRpc("get_inventory", getInventoryRpc);
  initializer.registerRpc("open_chest", openChestRpc);
  initializer.registerRpc("equip_item", equipItemRpc);
  initializer.registerRpc("get_shop", getShopRpc);
  initializer.registerRpc("shop_buy", shopBuyRpc);
  initializer.registerRpc("shop_refresh", shopRefreshRpc);
  initializer.registerRpc("claim_ad_chest", claimAdChestRpc);
  initializer.registerRpc("set_body", setBodyRpc);
  initializer.registerRpc("get_leaderboard", getLeaderboardRpc);
  createLeaderboard(nk, logger);
  initializer.registerMatch("race", { matchInit, matchJoinAttempt, matchJoin, matchLeave, matchLoop, matchTerminate, matchSignal });
  logger.info("TextRun modulu yuklendi");
}
