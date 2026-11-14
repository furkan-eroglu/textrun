// Liderlik tablosu: toplam XP'ye (yani seviyeye) göre en iyi oyuncular. Nakama'nın sıralama tablosu kullanılır;
// kayıtları yalnızca sunucu yazar (yarış sonunda ve ad değişince).
import { levelOf } from "./profile.ts";
import type { Profile } from "./profile.ts";

const LEADERBOARD_ID = "xp_rank"; // sıra numarası açık kurulan tablo
export const LEADERBOARD_TOP = 10;

// Sunucu açılırken bir kez: tablo yoksa kurulur (varsa dokunulmaz)
export function createLeaderboard(nk: nkruntime.Nakama, logger: nkruntime.Logger): void {
  try {
    nk.leaderboardCreate(LEADERBOARD_ID, true, "descending" as nkruntime.SortOrder, "set" as nkruntime.Operator, null, null, true); // son değer: sıra numarası hesaplansın
  } catch (e) {
    logger.warn("Liderlik tablosu kurulamadi: %s", String(e));
  }
}

// Oyuncunun güncel XP'sini ve adını tabloya yazar. Henüz XP'si ya da adı olmayan oyuncu tabloya girmez.
export function recordXp(nk: nkruntime.Nakama, logger: nkruntime.Logger, userId: string, profile: Profile): void {
  if (profile.xp <= 0 || !profile.name) return;
  try {
    nk.leaderboardRecordWrite(LEADERBOARD_ID, userId, profile.name, profile.xp, 0, undefined, "set" as nkruntime.OverrideOperator);
  } catch (e) {
    logger.warn("Liderlik kaydi yazilamadi: %s", String(e));
  }
}

// İlk LEADERBOARD_TOP oyuncu ve (varsa) isteyen oyuncunun kendi sırası
export function leaderboardView(nk: nkruntime.Nakama, userId: string) {
  const list = nk.leaderboardRecordsList(LEADERBOARD_ID, [userId], LEADERBOARD_TOP);
  const row = (record: nkruntime.LeaderboardRecord) => ({
    rank: record.rank,
    name: record.username,
    level: levelOf(record.score).level,
    xp: record.score,
    me: record.ownerId === userId,
  });
  const own = list.ownerRecords && list.ownerRecords.length > 0 ? row(list.ownerRecords[0]) : null;
  return { top: (list.records || []).map(row), me: own };
}
