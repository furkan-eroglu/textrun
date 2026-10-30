import { WORD_CONFIG, WORDS } from "./generated/words.ts";
import { nextRandom } from "./rng.ts";

export function isSupportedLang(lang: unknown): lang is string {
  return typeof lang === "string" && Object.prototype.hasOwnProperty.call(WORDS, lang);
}

function buildTiers(lang: string): string[][] {
  const tiers: string[][] = WORD_CONFIG.tiers.map(() => []);
  for (const word of WORDS[lang]) {
    for (let t = 0; t < WORD_CONFIG.tiers.length; t++) {
      if (word.length >= WORD_CONFIG.tiers[t].min && word.length <= WORD_CONFIG.tiers[t].max) {
        tiers[t].push(word);
        break;
      }
    }
  }
  return tiers;
}

// Nakama modül yüklendikten sonra global nesneleri dondurur (değiştirilemez yapar);
// bu yüzden bütün dillerin dilimleri yükleme anında, bir kez hazırlanır ve sonradan değiştirilmez.
const TIERS: { [lang: string]: string[][] } = {};
for (const lang of Object.keys(WORDS)) TIERS[lang] = buildTiers(lang);

function tiersFor(lang: string): string[][] {
  return TIERS[lang];
}

// Uzunluk dilimlerinin ağırlığına göre rastgele kelime listesi üretir; yakın zamanda kullanılan kelimeyi tekrarlamaz.
// Unity'deki WordDatabase.Generate ile aynı mantıktır. Yarışta liste sunucuda üretilip oyunculara gönderilir.
export function generateWords(lang: string, count: number, seedState: { rng: number }): string[] {
  const tiers = tiersFor(lang);
  let totalWeight = 0;
  for (let t = 0; t < tiers.length; t++) if (tiers[t].length > 0) totalWeight += WORD_CONFIG.tiers[t].weight;

  const result: string[] = [];
  const recent: string[] = [];
  const recentSet: { [word: string]: boolean } = {};

  while (result.length < count) {
    let roll = Math.floor(nextRandom(seedState) * totalWeight);
    let tier = 0;
    for (let t = 0; t < tiers.length; t++) {
      if (tiers[t].length === 0) continue;
      roll -= WORD_CONFIG.tiers[t].weight;
      if (roll < 0) {
        tier = t;
        break;
      }
    }

    const pool = tiers[tier];
    let word = pool[Math.floor(nextRandom(seedState) * pool.length)];
    for (let attempt = 0; attempt < 20 && recentSet[word]; attempt++) {
      word = pool[Math.floor(nextRandom(seedState) * pool.length)];
    }

    result.push(word);
    recent.push(word);
    recentSet[word] = true;
    if (recent.length > WORD_CONFIG.recentWindow) delete recentSet[recent.shift() as string];
  }
  return result;
}
