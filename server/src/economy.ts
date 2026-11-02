// Altın, sandık ve eşya kuralları. Saf işlevler: Nakama'dan bağımsız, Node'da test edilebilir.
// Tüm sayılar (altın miktarları, sandık oranları, seviye aralıkları) bu dosyadan ayarlanır.

export type Rarity = "common" | "uncommon" | "rare" | "epic" | "legendary";
export type ChestTier = "bronze" | "silver" | "gold";
export type Category = "head" | "glasses" | "face" | "top" | "bottom" | "shoes" | "trail";

export interface Item {
  id: string;
  category: Category;
  rarity: Rarity;
  tr: string;
  en: string;
  chest: boolean; // false = sandıktan çıkmaz (ör. koşu efektleri: ileride yalnızca elmasla)
}

export const RARITIES: Rarity[] = ["common", "uncommon", "rare", "epic", "legendary"];
export const CHEST_TIERS: ChestTier[] = ["bronze", "silver", "gold"];

// Deneme eşyaları: gerçek karakter ve görseller gelince aynı kimliklerle değiştirilir/çoğaltılır.
function item(id: string, category: Category, rarity: Rarity, tr: string, en: string, chest = true): Item {
  return { id, category, rarity, tr, en, chest };
}

export const CATALOG: Item[] = [
  item("hat_cap", "head", "common", "Şapka", "Cap"),
  item("hat_beanie", "head", "common", "Bere", "Beanie"),
  item("hat_cowboy", "head", "uncommon", "Kovboy Şapkası", "Cowboy Hat"),
  item("hat_top", "head", "rare", "Silindir Şapka", "Top Hat"),
  item("hat_crown", "head", "legendary", "Taç", "Crown"),
  item("hair_short", "head", "common", "Kısa Saç", "Short Hair"),
  item("hair_long", "head", "common", "Uzun Saç", "Long Hair"),
  item("hair_afro", "head", "uncommon", "Afro", "Afro"),
  item("hair_mohawk", "head", "rare", "Mohawk", "Mohawk"),
  item("hair_flame", "head", "epic", "Alevli Saç", "Flame Hair"),
  item("glasses_round", "glasses", "common", "Yuvarlak Gözlük", "Round Glasses"),
  item("glasses_sun", "glasses", "uncommon", "Güneş Gözlüğü", "Sunglasses"),
  item("glasses_star", "glasses", "rare", "Yıldız Gözlük", "Star Glasses"),
  item("glasses_cyber", "glasses", "epic", "Siber Gözlük", "Cyber Visor"),
  item("face_smile", "face", "common", "Gülümseme", "Smile"),
  item("face_wink", "face", "uncommon", "Göz Kırpma", "Wink"),
  item("face_cool", "face", "rare", "Havalı Bakış", "Cool Look"),
  item("face_star", "face", "epic", "Yıldız Gözler", "Star Eyes"),
  item("top_tee", "top", "common", "Tişört", "T-Shirt"),
  item("top_hoodie", "top", "uncommon", "Kapüşonlu", "Hoodie"),
  item("top_jacket", "top", "rare", "Ceket", "Jacket"),
  item("top_cape", "top", "epic", "Pelerin", "Cape"),
  item("top_armor", "top", "legendary", "Zırh", "Armor"),
  item("bottom_jeans", "bottom", "common", "Kot Pantolon", "Jeans"),
  item("bottom_shorts", "bottom", "common", "Şort", "Shorts"),
  item("bottom_cargo", "bottom", "uncommon", "Kargo Pantolon", "Cargo Pants"),
  item("bottom_suit", "bottom", "rare", "Takım Pantolon", "Suit Pants"),
  item("bottom_gold", "bottom", "legendary", "Altın Pantolon", "Golden Pants"),
  item("shoes_sneaker", "shoes", "common", "Spor Ayakkabı", "Sneakers"),
  item("shoes_boots", "shoes", "uncommon", "Bot", "Boots"),
  item("shoes_rocket", "shoes", "epic", "Roket Ayakkabı", "Rocket Shoes"),
  item("shoes_wing", "shoes", "legendary", "Kanatlı Ayakkabı", "Winged Shoes"),
  item("trail_sparkle", "trail", "epic", "Parıltı İzi", "Sparkle Trail", false),
  item("trail_smoke", "trail", "epic", "Duman İzi", "Smoke Trail", false),
  item("trail_fire", "trail", "legendary", "Ateş İzi", "Fire Trail", false),
  item("trail_lightning", "trail", "legendary", "Şimşek İzi", "Lightning Trail", false),
];

const BY_ID: { [id: string]: Item } = {};
for (const entry of CATALOG) BY_ID[entry.id] = entry;

export function itemById(id: string): Item | undefined {
  return BY_ID[id];
}

// ---------- Altın ----------

// Yarıştan kazanılan altın: bitirmek 10, üstüne sıraya göre bonus. Bitirmeyen ve hile yapan almaz.
const GOLD_PLACE_BONUS = [25, 15, 10, 5, 0];

export function goldForRace(rank: number, status: "finished" | "dnf" | "disqualified"): number {
  if (status !== "finished") return 0;
  return 10 + (GOLD_PLACE_BONUS[rank - 1] ?? 0);
}

// Aynı eşya ikinci kez çıkarsa verilen altın (eşya yeniden sayılmaz)
const DUPLICATE_GOLD: { [rarity: string]: number } = { common: 10, uncommon: 25, rare: 60, epic: 150, legendary: 400 };

export function duplicateGold(rarity: Rarity): number {
  return DUPLICATE_GOLD[rarity];
}

// ---------- Sandık ----------

// Her seviye atlamada bir sandık verilir; türü ULAŞILAN seviyeye göredir.
export const SILVER_FROM_LEVEL = 10;
export const GOLD_FROM_LEVEL = 20;

export function chestTierForLevel(level: number): ChestTier {
  if (level >= GOLD_FROM_LEVEL) return "gold";
  if (level >= SILVER_FROM_LEVEL) return "silver";
  return "bronze";
}

// Sandık türüne göre nadirlik ağırlıkları (toplamın yüzdesi olarak okunur)
export const CHEST_WEIGHTS: { [tier in ChestTier]: { [rarity in Rarity]: number } } = {
  bronze: { common: 70, uncommon: 22, rare: 7, epic: 1, legendary: 0 },
  silver: { common: 45, uncommon: 33, rare: 16, epic: 5, legendary: 1 },
  gold: { common: 20, uncommon: 35, rare: 28, epic: 13, legendary: 4 },
};

export interface ChestOpening {
  itemId: string;
  rarity: Rarity;
  duplicate: boolean;
  goldRefund: number; // yalnızca tekrar eşyada
}

// Sandığı açar: nadirliği ağırlıklara göre, eşyayı o nadirlikteki sandık eşyaları arasından rastgele seçer.
// rng 0 ile 1 arasında sayı döner (testlerde sabit verilebilir).
export function rollChest(tier: ChestTier, owned: string[], rng: () => number = Math.random): ChestOpening {
  const weights = CHEST_WEIGHTS[tier];
  const pools: { [rarity: string]: Item[] } = {};
  for (const entry of CATALOG) {
    if (!entry.chest) continue;
    (pools[entry.rarity] = pools[entry.rarity] || []).push(entry);
  }

  let total = 0;
  for (const rarity of RARITIES) if (pools[rarity]) total += weights[rarity];
  let pick = rng() * total;
  let chosen: Rarity = "common";
  for (const rarity of RARITIES) {
    if (!pools[rarity] || weights[rarity] <= 0) continue;
    chosen = rarity;
    if (pick < weights[rarity]) break;
    pick -= weights[rarity];
  }

  const pool = pools[chosen];
  const won = pool[Math.min(pool.length - 1, Math.floor(rng() * pool.length))];
  const duplicate = owned.indexOf(won.id) >= 0;
  return { itemId: won.id, rarity: won.rarity, duplicate, goldRefund: duplicate ? duplicateGold(won.rarity) : 0 };
}
