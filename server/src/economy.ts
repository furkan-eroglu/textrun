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
  chest: boolean; // false = sandıktan çıkmaz
  diamond: boolean; // true = yalnızca elmasla satılır (mağaza); sandıktan da çıkmaz
}

export const RARITIES: Rarity[] = ["common", "uncommon", "rare", "epic", "legendary"];
export const CHEST_TIERS: ChestTier[] = ["bronze", "silver", "gold"];

// Eşya listesi. Görseller istemcide aynı kimliklerle çizilir (client/Assets/Scripts/Characters/Outfits.cs);
// yeni eşya eklerken iki yerde de aynı kimlik kullanılmalıdır.
function item(id: string, category: Category, rarity: Rarity, tr: string, en: string, flags: { chest?: boolean; diamond?: boolean } = {}): Item {
  const diamond = !!flags.diamond;
  return { id, category, rarity, tr, en, chest: !diamond && flags.chest !== false, diamond };
}

const DIAMOND = { diamond: true };
const NO_CHEST = { chest: false };

export const CATALOG: Item[] = [
  // Baş (şapka ve saç aynı kategori: şapka takılınca saç görünmez)
  item("hat_cap", "head", "common", "Spor Şapka", "Sporty Cap"),
  item("hat_beanie", "head", "common", "Kışlık Bere", "Winter Beanie"),
  item("hat_bucket", "head", "common", "Balıkçı Şapkası", "Bucket Hat"),
  item("hat_cowboy", "head", "uncommon", "Kovboy Şapkası", "Cowboy Hat"),
  item("hat_street", "head", "uncommon", "Serseri Şapkası", "Street Cap"),
  item("hat_pop", "head", "rare", "Popstar Şapkası", "Popstar Hat"),
  item("hat_top", "head", "rare", "Sihirbaz Silindiri", "Magician Top Hat"),
  item("hat_pirate", "head", "epic", "Korsan Şapkası", "Pirate Hat"),
  item("hat_crown", "head", "legendary", "Kral Tacı", "King's Crown"),
  item("hair_short", "head", "common", "Düzgün Kesim", "Neat Cut"),
  item("hair_long", "head", "common", "Omuz Saçı", "Shoulder Hair"),
  item("hair_afro", "head", "uncommon", "Afro", "Afro"),
  item("hair_mohawk", "head", "rare", "Mohawk", "Mohawk"),
  item("hair_flame", "head", "epic", "Alevli Saç", "Flame Hair"),
  item("hair_rainbow", "head", "legendary", "Gökkuşağı Saçı", "Rainbow Hair"),
  // Gözlük
  item("glasses_round", "glasses", "common", "Yuvarlak Gözlük", "Round Glasses"),
  item("glasses_sun", "glasses", "uncommon", "Güneş Gözlüğü", "Sunglasses"),
  item("glasses_star", "glasses", "rare", "Yıldız Gözlük", "Star Glasses"),
  item("glasses_cyber", "glasses", "epic", "Siber Vizör", "Cyber Visor"),
  item("glasses_gold", "glasses", "legendary", "Altın Çerçeve", "Golden Frames", DIAMOND),
  // Yüz ifadesi
  item("face_smile", "face", "common", "Neşeli Yüz", "Happy Face"),
  item("face_wink", "face", "uncommon", "Göz Kırpma", "Wink"),
  item("face_cool", "face", "rare", "Havalı Bakış", "Cool Look"),
  item("face_star", "face", "epic", "Yıldız Gözler", "Star Eyes"),
  // Üst giyim
  item("top_tee", "top", "common", "Basit Tişört", "Plain Tee"),
  item("top_hoodie", "top", "uncommon", "Sokak Kapüşonu", "Street Hoodie"),
  item("top_jersey", "top", "uncommon", "Takım Forması", "Team Jersey"),
  item("top_jacket", "top", "rare", "Deri Ceket", "Leather Jacket"),
  item("top_cape", "top", "epic", "Kahraman Pelerini", "Hero Cape"),
  item("top_armor", "top", "legendary", "Şövalye Zırhı", "Knight Armor"),
  // Alt giyim
  item("bottom_jeans", "bottom", "common", "Mavi Kot", "Blue Jeans"),
  item("bottom_shorts", "bottom", "common", "Spor Şort", "Sport Shorts"),
  item("bottom_cargo", "bottom", "uncommon", "Kargo Pantolon", "Cargo Pants"),
  item("bottom_suit", "bottom", "rare", "Takım Pantolonu", "Suit Pants"),
  item("bottom_gold", "bottom", "legendary", "Altın Pantolon", "Golden Pants", DIAMOND),
  // Ayakkabı
  item("shoes_sneaker", "shoes", "common", "Spor Ayakkabı", "Sneakers"),
  item("shoes_boots", "shoes", "uncommon", "Maceracı Botu", "Explorer Boots"),
  item("shoes_neon", "shoes", "rare", "Neon Ayakkabı", "Neon Shoes"),
  item("shoes_rocket", "shoes", "epic", "Roket Ayakkabı", "Rocket Shoes"),
  item("shoes_wing", "shoes", "legendary", "Kanatlı Ayakkabı", "Winged Shoes", DIAMOND),
  // Koşu efektleri (sandıktan çıkmaz; ileride elmasla)
  item("trail_sparkle", "trail", "epic", "Parıltı İzi", "Sparkle Trail", NO_CHEST),
  item("trail_smoke", "trail", "epic", "Duman İzi", "Smoke Trail", NO_CHEST),
  item("trail_fire", "trail", "legendary", "Ateş İzi", "Fire Trail", NO_CHEST),
  item("trail_lightning", "trail", "legendary", "Şimşek İzi", "Lightning Trail", NO_CHEST),
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

// ---------- Mağaza ----------

export type Currency = "gold" | "diamond";

export const SHOP_SLOTS = 4; // her gün değişen eşya yuvaları
export const SHOP_REFRESH_DIAMONDS = 20; // mağazayı elmasla yenilemenin bedeli
export const SHOP_GOLD_CHEST: { tier: ChestTier; price: number } = { tier: "silver", price: 500 }; // altınla: yenileme başına 1 kez
export const SHOP_DIAMOND_CHEST: { tier: ChestTier; price: number } = { tier: "gold", price: 100 }; // elmasla: sınırsız

// TEST: gerçek satın alma gelene kadar her hesaba verilen elmas. Yayından önce 0 yapılmalı.
export const START_DIAMONDS = 500;

const GOLD_PRICE: { [rarity: string]: number } = { common: 150, uncommon: 400, rare: 1000, epic: 2500, legendary: 6000 };
const DIAMOND_PRICE: { [rarity: string]: number } = { common: 10, uncommon: 25, rare: 60, epic: 150, legendary: 300 };

// Eşyanın mağaza fiyatı: yalnızca elmasla satılanlar elmasla, diğerleri altınla
export function priceOf(entry: Item): { currency: Currency; price: number } {
  if (entry.diamond) return { currency: "diamond", price: DIAMOND_PRICE[entry.rarity] };
  return { currency: "gold", price: GOLD_PRICE[entry.rarity] };
}

// Mağaza her gece 00.00'da (Türkiye saati, UTC+3) yenilenir.
const SHOP_ZONE_OFFSET_MS = 3 * 60 * 60 * 1000;
const DAY_MS = 24 * 60 * 60 * 1000;

export function shopDay(now: number): number {
  return Math.floor((now + SHOP_ZONE_OFFSET_MS) / DAY_MS);
}

export function shopSecondsLeft(now: number): number {
  return Math.ceil(((shopDay(now) + 1) * DAY_MS - SHOP_ZONE_OFFSET_MS - now) / 1000);
}

// Mağazada bir eşyanın çıkma ağırlığı (nadir eşyalar daha seyrek gelir)
const SHOP_WEIGHT: { [rarity: string]: number } = { common: 10, uncommon: 8, rare: 5, epic: 3, legendary: 2 };

// Günün eşyalarını seçer: oyuncunun sahip olmadığı, birbirinden farklı en fazla SHOP_SLOTS eşya.
// Koşu efektleri şimdilik satılmaz (görselleri yok).
export function rollShop(owned: string[], rng: () => number = Math.random): string[] {
  const pool = CATALOG.filter((entry) => entry.category !== "trail" && owned.indexOf(entry.id) < 0);
  const picked: string[] = [];
  while (picked.length < SHOP_SLOTS && pool.length > 0) {
    let total = 0;
    for (const entry of pool) total += SHOP_WEIGHT[entry.rarity];
    let roll = rng() * total;
    let index = pool.length - 1;
    for (let i = 0; i < pool.length; i++) {
      roll -= SHOP_WEIGHT[pool[i].rarity];
      if (roll < 0) {
        index = i;
        break;
      }
    }
    picked.push(pool[index].id);
    pool.splice(index, 1);
  }
  return picked;
}
