// Oyuncu profili: ad, XP, seviye, altın, sandık ve eşyalar. Saf kurallar burada; saklama ve RPC'ler main.ts / match.ts içindedir.
// XP, altın ve sandıklar yalnızca sunucuda hesaplanır ve saklanır; istemci bu değerleri gönderemez.
import {
  AD_CHEST_TIER, CATEGORIES, CHEST_TIERS, SHOP_DIAMOND_CHEST, SHOP_GOLD_CHEST, SHOP_REFRESH_DIAMONDS, SHOP_SLOTS, START_DIAMONDS,
  chestTierForLevel, goldForRace, itemById, priceOf, rollChest, rollShop, shopDay,
} from "./economy.ts";
import type { Category, ChestOpening, ChestTier } from "./economy.ts";

export type Chests = { [tier in ChestTier]: number };

export interface Profile {
  name: string | null; // null = oyuncu henüz ad seçmedi
  xp: number;
  races: number;
  wins: number;
  gold: number;
  chests: Chests; // açılmamış sandık sayıları
  items: string[]; // sahip olunan eşya kimlikleri
  equipped: { [category: string]: string }; // kategori -> takılı eşya kimliği
  diamonds: number;
  shop: ShopState | null; // bugünün mağazası (ilk açılışta kurulur)
  adDay: number; // reklam sandığının en son açıldığı mağaza günü (0 = hiç)
}

export interface ShopState {
  day: number; // mağaza günü (economy.shopDay)
  items: string[]; // günün eşyaları
  bought: boolean[]; // her yuva için: alındı mı
  goldChestBought: boolean;
}

export interface LevelInfo {
  level: number;
  xpIntoLevel: number; // bu seviyede biriktirilen XP
  xpForNext: number; // sonraki seviyeye geçmek için bu seviyede gereken toplam XP
}

export const NAME_MIN = 3;
export const NAME_MAX = 12;

export function emptyChests(): Chests {
  return { bronze: 0, silver: 0, gold: 0 };
}

export function emptyProfile(): Profile {
  return { name: null, xp: 0, races: 0, wins: 0, gold: 0, chests: emptyChests(), items: [], equipped: {}, diamonds: START_DIAMONDS, shop: null, adDay: 0 };
}

// Seviye 1'den 2'ye 100 XP; her seviyede gereken miktar 40 artar (100, 140, 180, ...)
export function xpForNextLevel(level: number): number {
  return 100 + 40 * (level - 1);
}

export function levelOf(xp: number): LevelInfo {
  let level = 1;
  let left = Math.max(0, Math.floor(xp));
  while (left >= xpForNextLevel(level)) {
    left -= xpForNextLevel(level);
    level++;
  }
  return { level, xpIntoLevel: left, xpForNext: xpForNextLevel(level) };
}

// Yarıştan kazanılan XP sabittir (seviyeye bağlı değil): bitirmek 20, üstüne sıraya göre bonus. Bitirmeyen 5, hile yapan 0.
const PLACE_BONUS = [50, 35, 25, 15, 10];

export function xpForRace(rank: number, status: "finished" | "dnf" | "disqualified"): number {
  if (status === "disqualified") return 0;
  if (status === "dnf") return 5;
  return 20 + (PLACE_BONUS[rank - 1] ?? 0);
}

// Yalnızca herkese açık (eşleşmeli) yarışlar XP verir. Özel oda XP vermez: iki telefonla birbirini yenerek XP kasılamaz.
export function racePaysXp(kind: string): boolean {
  return kind === "public";
}

const NAME_ALLOWED = /^[A-Za-z0-9çğıöşüÇĞİÖŞÜ _.\-]+$/;

// Geçerli adı düzenlenmiş haliyle döner (kenar boşlukları atılır, art arda boşluklar teke iner); geçersizse null.
export function cleanName(raw: unknown): string | null {
  if (typeof raw !== "string") return null;
  const name = raw.replace(/\s+/g, " ").trim();
  if (name.length < NAME_MIN || name.length > NAME_MAX) return null;
  if (!NAME_ALLOWED.test(name)) return null;
  if (!/[A-Za-z0-9çğıöşüÇĞİÖŞÜ]/.test(name)) return null; // yalnızca simgelerden oluşan ad olmaz
  return name;
}

export interface Reward {
  gain: number;
  xp: number; // yeni toplam
  level: number;
  xpIntoLevel: number; // yeni seviyede biriktirilen XP (çubuk için)
  xpForNext: number; // yeni seviyeyi geçmek için gereken XP
  leveledUp: boolean;
  gold: number; // bu yarıştan kazanılan altın
  chests: ChestTier[]; // bu yarışta atlanan her seviye için bir sandık (türü ulaşılan seviyeye göre)
}

// Yarış sonucunu profile işler; profili günceller ve ödülü döner.
export function applyRace(profile: Profile, rank: number, status: "finished" | "dnf" | "disqualified"): Reward {
  const before = levelOf(profile.xp).level;
  const gain = xpForRace(rank, status);
  profile.xp += gain;
  profile.races += 1;
  if (rank === 1 && status === "finished") profile.wins += 1;
  const after = levelOf(profile.xp);

  const gold = goldForRace(rank, status);
  profile.gold += gold;
  const chests: ChestTier[] = [];
  for (let level = before + 1; level <= after.level; level++) {
    const tier = chestTierForLevel(level);
    profile.chests[tier] += 1;
    chests.push(tier);
  }
  return { gain, xp: profile.xp, level: after.level, xpIntoLevel: after.xpIntoLevel, xpForNext: after.xpForNext, leveledUp: after.level > before, gold, chests };
}

export function isChestTier(value: unknown): value is ChestTier {
  return typeof value === "string" && (CHEST_TIERS as string[]).indexOf(value) >= 0;
}

// Bir sandık açar: sandık sayısı düşer; eşya envantere girer, tekrar eşyaysa altın verilir. Sandık yoksa null döner.
export function openChest(profile: Profile, tier: ChestTier, rng: () => number = Math.random): ChestOpening | null {
  if (profile.chests[tier] <= 0) return null;
  const opening = rollChest(tier, profile.items, rng);
  profile.chests[tier] -= 1;
  if (opening.duplicate) profile.gold += opening.goldRefund;
  else profile.items.push(opening.itemId);
  return opening;
}

// Sahip olunan bir eşyayı takar (aynı kategorideki önceki eşya çıkar). Sahip değilse veya bilinmiyorsa false döner.
export function equipItem(profile: Profile, itemId: unknown): boolean {
  if (typeof itemId !== "string" || profile.items.indexOf(itemId) < 0) return false;
  const entry = itemById(itemId);
  if (!entry) return false;
  profile.equipped[entry.category] = entry.id;
  return true;
}

// Bir kategorideki eşyayı çıkarır. Bilinmeyen kategoride false döner.
export function unequipCategory(profile: Profile, category: unknown): boolean {
  if (typeof category !== "string") return false;
  if ((CATEGORIES as string[]).indexOf(category) < 0) return false;
  delete profile.equipped[category as Category];
  return true;
}

// Takılı eşyaların kimlikleri (yarışta diğer oyunculara gönderilir)
export function lookOf(profile: Profile): string[] {
  const ids: string[] = [];
  for (const category of Object.keys(profile.equipped)) ids.push(profile.equipped[category]);
  return ids.sort();
}

// ---------- Mağaza ----------

export const SHOP_GOLD_CHEST_SLOT = SHOP_SLOTS; // 4
export const SHOP_DIAMOND_CHEST_SLOT = SHOP_SLOTS + 1; // 5

export type ShopOutcome = "ok" | "bad_slot" | "already_bought" | "not_enough";

function newShop(profile: Profile, day: number, rng: () => number): ShopState {
  const items = rollShop(profile.items, rng);
  return { day, items, bought: items.map(() => false), goldChestBought: false };
}

// Mağaza yoksa veya gün değiştiyse (gece 00.00) yenisini kurar. Değişiklik olduysa true döner.
export function ensureShop(profile: Profile, now: number, rng: () => number = Math.random): boolean {
  const day = shopDay(now);
  if (profile.shop && profile.shop.day === day) return false;
  profile.shop = newShop(profile, day, rng);
  return true;
}

// Elmasla yenileme: eşyalar ve altın sandık hakkı yenilenir; gün aynı kalır (00.00'a kalan süre değişmez).
export function refreshShop(profile: Profile, now: number, rng: () => number = Math.random): ShopOutcome {
  ensureShop(profile, now, rng);
  if (profile.diamonds < SHOP_REFRESH_DIAMONDS) return "not_enough";
  profile.diamonds -= SHOP_REFRESH_DIAMONDS;
  profile.shop = newShop(profile, shopDay(now), rng);
  return "ok";
}

// Mağazadan alışveriş: 0-3 eşya yuvaları, 4 altınla sandık (yenileme başına 1), 5 elmasla sandık (sınırsız)
export function buyShopSlot(profile: Profile, slot: unknown, now: number, rng: () => number = Math.random): ShopOutcome {
  ensureShop(profile, now, rng);
  const shop = profile.shop!;
  if (typeof slot !== "number" || Math.floor(slot) !== slot) return "bad_slot";

  if (slot === SHOP_GOLD_CHEST_SLOT) {
    if (shop.goldChestBought) return "already_bought";
    if (profile.gold < SHOP_GOLD_CHEST.price) return "not_enough";
    profile.gold -= SHOP_GOLD_CHEST.price;
    profile.chests[SHOP_GOLD_CHEST.tier] += 1;
    shop.goldChestBought = true;
    return "ok";
  }
  if (slot === SHOP_DIAMOND_CHEST_SLOT) {
    if (profile.diamonds < SHOP_DIAMOND_CHEST.price) return "not_enough";
    profile.diamonds -= SHOP_DIAMOND_CHEST.price;
    profile.chests[SHOP_DIAMOND_CHEST.tier] += 1;
    return "ok";
  }

  if (slot < 0 || slot >= shop.items.length) return "bad_slot";
  const entry = itemById(shop.items[slot]);
  if (!entry) return "bad_slot";
  if (shop.bought[slot] || profile.items.indexOf(entry.id) >= 0) return "already_bought"; // bu arada sandıktan çıkmış olabilir
  const cost = priceOf(entry);
  if (cost.currency === "gold") {
    if (profile.gold < cost.price) return "not_enough";
    profile.gold -= cost.price;
  } else {
    if (profile.diamonds < cost.price) return "not_enough";
    profile.diamonds -= cost.price;
  }
  profile.items.push(entry.id);
  shop.bought[slot] = true;
  return "ok";
}

// ---------- Günlük reklam sandığı ----------

export function adChestReady(profile: Profile, now: number): boolean {
  return profile.adDay !== shopDay(now);
}

// Günlük reklam sandığını açar (günde bir kez; gece 00.00'da yenilenir). Bugün açıldıysa null döner.
// Sandık envantere girmez, hemen açılır: eşya eklenir, tekrar eşyaysa altın verilir.
export function claimAdChest(profile: Profile, now: number, rng: () => number = Math.random): ChestOpening | null {
  if (!adChestReady(profile, now)) return null;
  profile.adDay = shopDay(now);
  const opening = rollChest(AD_CHEST_TIER, profile.items, rng);
  if (opening.duplicate) profile.gold += opening.goldRefund;
  else profile.items.push(opening.itemId);
  return opening;
}
