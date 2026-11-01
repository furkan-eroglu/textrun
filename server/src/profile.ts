// Oyuncu profili: ad, XP ve seviye. Saf kurallar burada; saklama ve RPC'ler main.ts / match.ts içindedir.
// XP yalnızca sunucuda hesaplanır ve saklanır; istemci bir XP değeri gönderemez.

export interface Profile {
  name: string | null; // null = oyuncu henüz ad seçmedi
  xp: number;
  races: number;
  wins: number;
}

export interface LevelInfo {
  level: number;
  xpIntoLevel: number; // bu seviyede biriktirilen XP
  xpForNext: number; // sonraki seviyeye geçmek için bu seviyede gereken toplam XP
}

export const NAME_MIN = 3;
export const NAME_MAX = 12;

export function emptyProfile(): Profile {
  return { name: null, xp: 0, races: 0, wins: 0 };
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
}

// Yarış sonucunu profile işler; profili günceller ve ödülü döner.
export function applyRace(profile: Profile, rank: number, status: "finished" | "dnf" | "disqualified"): Reward {
  const before = levelOf(profile.xp).level;
  const gain = xpForRace(rank, status);
  profile.xp += gain;
  profile.races += 1;
  if (rank === 1 && status === "finished") profile.wins += 1;
  const after = levelOf(profile.xp);
  return { gain, xp: profile.xp, level: after.level, xpIntoLevel: after.xpIntoLevel, xpForNext: after.xpForNext, leveledUp: after.level > before };
}
