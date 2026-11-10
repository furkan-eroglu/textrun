// Profilin Nakama depolamasına okunup yazılması. Kurallar profile.ts içindedir.
import { CHEST_TIERS, SHOP_DIAMOND_CHEST, SHOP_GOLD_CHEST, SHOP_REFRESH_DIAMONDS, itemById, priceOf, shopSecondsLeft } from "./economy.ts";
import type { ChestOpening, ChestTier } from "./economy.ts";
import {
  SHOP_DIAMOND_CHEST_SLOT, SHOP_GOLD_CHEST_SLOT, applyRace, buyShopSlot, emptyProfile, ensureShop, equipItem, levelOf, lookOf, openChest, refreshShop, unequipCategory,
} from "./profile.ts";
import type { Profile, Reward, ShopOutcome } from "./profile.ts";

const COLLECTION = "profile";
const KEY = "main";

interface Stored {
  profile: Profile;
  version: string;
}

function parse(value: { [key: string]: any } | undefined): Profile {
  const profile = emptyProfile();
  if (!value) return profile;
  if (typeof value.name === "string") profile.name = value.name;
  if (typeof value.xp === "number") profile.xp = value.xp;
  if (typeof value.races === "number") profile.races = value.races;
  if (typeof value.wins === "number") profile.wins = value.wins;
  if (typeof value.gold === "number") profile.gold = value.gold;
  if (value.chests && typeof value.chests === "object") {
    for (const tier of CHEST_TIERS) if (typeof value.chests[tier] === "number") profile.chests[tier] = value.chests[tier];
  }
  if (Array.isArray(value.items)) profile.items = value.items.filter((id: unknown) => typeof id === "string" && !!itemById(id as string));
  if (typeof value.diamonds === "number") profile.diamonds = value.diamonds;
  const shop = value.shop;
  if (shop && typeof shop === "object" && typeof shop.day === "number" && Array.isArray(shop.items) && Array.isArray(shop.bought)) {
    const items = shop.items.filter((id: unknown) => typeof id === "string" && !!itemById(id as string));
    if (items.length === shop.items.length) {
      profile.shop = { day: shop.day, items, bought: items.map((_: string, i: number) => shop.bought[i] === true), goldChestBought: shop.goldChestBought === true };
    }
  }
  if (value.equipped && typeof value.equipped === "object") {
    for (const category of Object.keys(value.equipped)) {
      const id = value.equipped[category];
      const entry = typeof id === "string" ? itemById(id) : undefined;
      if (entry && entry.category === category && profile.items.indexOf(id) >= 0) profile.equipped[category] = id;
    }
  }
  return profile;
}

export function readProfile(nk: nkruntime.Nakama, userId: string): Stored {
  const records = nk.storageRead([{ collection: COLLECTION, key: KEY, userId: userId }]);
  if (records.length === 0) return { profile: emptyProfile(), version: "*" };
  return { profile: parse(records[0].value), version: records[0].version };
}

function writeRequest(userId: string, stored: Stored) {
  return {
    collection: COLLECTION,
    key: KEY,
    userId: userId,
    value: stored.profile as unknown as { [key: string]: any },
    version: stored.version,
    permissionRead: 1 as const, // yalnızca sahibi okur
    permissionWrite: 0 as const, // yalnızca sunucu yazar
  };
}

export function saveProfile(nk: nkruntime.Nakama, userId: string, stored: Stored): void {
  nk.storageWrite([writeRequest(userId, stored)]);
}

// Hesap kullanıcı adı + şifre ile açıldıysa (e-posta alanı dolu) kalıcı hesaptır. Misafir (cihaz kimliği) hesabın e-postası yoktur.
export function isRegistered(nk: nkruntime.Nakama, userId: string): boolean {
  return !!nk.accountGetId(userId).email;
}

// İstemciye giden profil görünümü. Misafirin seviyesi hep 1'dir; XP, para, seviye yalnızca kalıcı hesapta vardır.
export function profileView(profile: Profile, guest: boolean) {
  if (guest) {
    return {
      guest: true, name: null, xp: 0, level: 1, xpIntoLevel: 0, xpForNext: levelOf(0).xpForNext, races: 0, wins: 0,
      gold: 0, diamonds: 0, chestBronze: 0, chestSilver: 0, chestGold: 0, itemCount: 0,
    };
  }
  const info = levelOf(profile.xp);
  return {
    guest: false,
    name: profile.name,
    xp: profile.xp,
    level: info.level,
    xpIntoLevel: info.xpIntoLevel,
    xpForNext: info.xpForNext,
    races: profile.races,
    wins: profile.wins,
    gold: profile.gold,
    diamonds: profile.diamonds,
    chestBronze: profile.chests.bronze,
    chestSilver: profile.chests.silver,
    chestGold: profile.chests.gold,
    itemCount: profile.items.length,
  };
}

// Sahip olunan eşyaların ayrıntıları (envanter ekranı için)
export function inventoryView(profile: Profile) {
  return {
    items: profile.items.map((id) => {
      const entry = itemById(id)!;
      return { id: entry.id, category: entry.category, rarity: entry.rarity, tr: entry.tr, en: entry.en };
    }),
    equipped: lookOf(profile),
  };
}

// Bir sandık açar ve sonucu kalıcı yazar. Eşzamanlı yazma çakışırsa (sürüm uyuşmazlığı) yeniden dener.
// Sandık yoksa null döner. Başarılı sonuçla birlikte yeni profil görünümü için profil de döner.
export function openChestFor(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, tier: ChestTier): { opening: ChestOpening; profile: Profile } | null {
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const stored = readProfile(nk, userId);
      const opening = openChest(stored.profile, tier);
      if (!opening) return null;
      saveProfile(nk, userId, stored);
      return { opening, profile: stored.profile };
    } catch (e) {
      logger.warn("Sandik yazilamadi (deneme %d): %s", attempt + 1, String(e));
    }
  }
  throw new Error("sandik acilamadi");
}

// Yarış sonunda bir oyuncuya XP verir. Bir hata yarışın sonuçlarını bozmasın diye yakalanır; hata olursa ödül boş döner.
export function awardRace(
  nk: nkruntime.Nakama,
  logger: nkruntime.Logger,
  userId: string,
  rank: number,
  status: "finished" | "dnf" | "disqualified"
): Reward | null {
  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      const stored = readProfile(nk, userId);
      const reward = applyRace(stored.profile, rank, status);
      saveProfile(nk, userId, stored);
      return reward;
    } catch (e) {
      logger.warn("XP yazilamadi (deneme %d): %s", attempt + 1, String(e));
    }
  }
  return null;
}

// Oyuncunun takılı eşyaları (yarış odasında herkese gösterilir). Okunamazsa boş döner.
export function lookFor(nk: nkruntime.Nakama, userId: string): string[] {
  try {
    return lookOf(readProfile(nk, userId).profile);
  } catch (e) {
    return [];
  }
}

// Bir eşyayı takar (itemId) veya bir kategoriyi boşaltır (category). Sürüm çakışırsa yeniden dener.
// Eşya oyuncunun değilse null döner.
export function equipFor(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, change: { itemId?: unknown; category?: unknown }): Profile | null {
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const stored = readProfile(nk, userId);
      const ok = change.itemId !== undefined ? equipItem(stored.profile, change.itemId) : unequipCategory(stored.profile, change.category);
      if (!ok) return null;
      saveProfile(nk, userId, stored);
      return stored.profile;
    } catch (e) {
      logger.warn("Esya takilamadi (deneme %d): %s", attempt + 1, String(e));
    }
  }
  throw new Error("esya takilamadi");
}

// Mağazanın istemciye giden görünümü
export function shopView(profile: Profile, now: number) {
  const shop = profile.shop!;
  return {
    secondsLeft: shopSecondsLeft(now),
    refreshCost: SHOP_REFRESH_DIAMONDS,
    gold: profile.gold,
    diamonds: profile.diamonds,
    slots: shop.items.map((id, index) => {
      const entry = itemById(id)!;
      const cost = priceOf(entry);
      return {
        slot: index, itemId: entry.id, category: entry.category, rarity: entry.rarity, tr: entry.tr, en: entry.en,
        currency: cost.currency, price: cost.price,
        bought: shop.bought[index] || profile.items.indexOf(entry.id) >= 0,
      };
    }),
    goldChest: { slot: SHOP_GOLD_CHEST_SLOT, tier: SHOP_GOLD_CHEST.tier, currency: "gold", price: SHOP_GOLD_CHEST.price, bought: shop.goldChestBought },
    diamondChest: { slot: SHOP_DIAMOND_CHEST_SLOT, tier: SHOP_DIAMOND_CHEST.tier, currency: "diamond", price: SHOP_DIAMOND_CHEST.price, bought: false },
  };
}

// Mağaza işlemi: "open" yalnızca bakar (gün değiştiyse yeniler), "buy" bir yuvayı alır, "refresh" elmasla yeniler.
// Sürüm çakışırsa yeniden dener. Sonuç "ok" değilse profil yazılmaz.
export function shopFor(
  nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, action: "open" | "buy" | "refresh", slot?: unknown
): { outcome: ShopOutcome; profile: Profile } {
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const stored = readProfile(nk, userId);
      const now = Date.now();
      let outcome: ShopOutcome = "ok";
      let changed = true;
      if (action === "buy") outcome = buyShopSlot(stored.profile, slot, now);
      else if (action === "refresh") outcome = refreshShop(stored.profile, now);
      else changed = ensureShop(stored.profile, now);
      if (outcome !== "ok") {
        ensureShop(stored.profile, now);
        return { outcome, profile: stored.profile };
      }
      if (changed) saveProfile(nk, userId, stored);
      return { outcome, profile: stored.profile };
    } catch (e) {
      logger.warn("Magaza yazilamadi (deneme %d): %s", attempt + 1, String(e));
    }
  }
  throw new Error("magaza islemi yapilamadi");
}
