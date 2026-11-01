// Profilin Nakama depolamasına okunup yazılması. Kurallar profile.ts içindedir.
import { applyRace, emptyProfile, levelOf } from "./profile.ts";
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

// İstemciye giden profil görünümü
export function profileView(profile: Profile) {
  const info = levelOf(profile.xp);
  return {
    name: profile.name,
    xp: profile.xp,
    level: info.level,
    xpIntoLevel: info.xpIntoLevel,
    xpForNext: info.xpForNext,
    races: profile.races,
    wins: profile.wins,
  };
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
