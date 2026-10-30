// Tohumla çalışan basit rastgele sayı üreteci (mulberry32). Durumu tek bir sayıdır, bu yüzden oda durumuna kaydedilebilir.
export function nextRandom(seedState: { rng: number }): number {
  seedState.rng = (seedState.rng + 0x6d2b79f5) >>> 0;
  let t = seedState.rng;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}
