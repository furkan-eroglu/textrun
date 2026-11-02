// Profilin Nakama depolamasına okunup yazılması. Kurallar profile.ts içindedir.
import { CHEST_TIERS, itemById } from "./economy.ts";
import type { ChestOpening, ChestTier } from "./economy.ts";
import { applyRace, emptyProfile, levelOf, openChest } from "./profile.ts";
import type { Profile, Reward } from "./profile.ts";

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
      gold: 0, chestBronze: 0, chestSilver: 0, chestGold: 0, itemCount: 0,
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
