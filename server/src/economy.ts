// Altın, sandık ve eşya kuralları. Saf işlevler: Nakama'dan bağımsız, Node'da test edilebilir.
// Tüm sayılar (altın miktarları, sandık oranları, seviye aralıkları) bu dosyadan ayarlanır.

export type Rarity = "common" | "uncommon" | "rare" | "epic" | "legendary";
export type ChestTier = "bronze" | "silver" | "gold";
export type Category = "head" | "top" | "bottom" | "shoes" | "cape";

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
export const CATEGORIES: Category[] = ["head", "top", "bottom", "shoes", "cape"];

// Eşya listesi: her kategoride en az 10 eşya. Görseller istemcide aynı kimliklerle çizilir
// (client/Assets/Scripts/Characters/Outfits.cs); yeni eşya eklerken iki yerde de aynı kimlik kullanılmalıdır.
function item(id: string, category: Category, rarity: Rarity, tr: string, en: string, flags: { diamond?: boolean } = {}): Item {
  const diamond = !!flags.diamond;
  return { id, category, rarity, tr, en, chest: !diamond, diamond };
}

const DIAMOND = { diamond: true };

export const CATALOG: Item[] = [
  // Kafa (şapka ve saç aynı kategori: aynı anda yalnızca biri takılır; takılınca temel saçın yerini alır)
  item("hat_cap", "head", "common", "Spor Şapka", "Sporty Cap"),
  item("hat_beanie", "head", "common", "Kışlık Bere", "Winter Beanie"),
  item("hair_afro", "head", "uncommon", "Afro", "Afro"),
  item("hat_cowboy", "head", "uncommon", "Kovboy Şapkası", "Cowboy Hat"),
  item("hat_bucket", "head", "uncommon", "Balıkçı Şapkası", "Bucket Hat"),
  item("hat_street", "head", "rare", "Serseri Şapkası", "Street Cap"),
  item("hat_pop", "head", "rare", "Popstar Şapkası", "Popstar Hat"),
  item("hair_mohawk", "head", "rare", "Mohawk", "Mohawk"),
  item("hat_pirate", "head", "epic", "Korsan Şapkası", "Pirate Hat"),
  item("hat_crown", "head", "legendary", "Kral Tacı", "King's Crown"),
  item("hat_kavuk", "head", "legendary", "Padişah Kavuğu", "Sultan's Turban"),
  // Üst giyim
  item("top_tee", "top", "common", "Basit Tişört", "Plain Tee"),
  item("top_tank", "top", "common", "Spor Atlet", "Sport Tank"),
  item("top_white", "top", "common", "Beyaz Atlet", "White Undershirt"),
  item("top_hoodie", "top", "uncommon", "Sokak Kapüşonu", "Street Hoodie"),
  item("top_jersey", "top", "uncommon", "Takım Forması", "Team Jersey"),
  item("top_stripe", "top", "uncommon", "Çizgili Kazak", "Striped Sweater"),
  item("top_jacket", "top", "rare", "Deri Ceket", "Leather Jacket"),
  item("top_suit", "top", "rare", "Smokin Ceket", "Tuxedo Jacket"),
  item("top_hawaii", "top", "rare", "Hawaii Gömleği", "Hawaiian Shirt"),
  item("top_ninja", "top", "epic", "Ninja Kıyafeti", "Ninja Outfit"),
  item("top_armor", "top", "legendary", "Şövalye Zırhı", "Knight Armor"),
  // Alt giyim
  item("bottom_jeans", "bottom", "common", "Mavi Kot", "Blue Jeans"),
  item("bottom_shorts", "bottom", "common", "Spor Şort", "Sport Shorts"),
  item("bottom_track", "bottom", "common", "Eşofman Altı", "Track Pants"),
  item("bottom_cargo", "bottom", "uncommon", "Kargo Pantolon", "Cargo Pants"),
  item("bottom_camo", "bottom", "uncommon", "Kamuflaj Pantolon", "Camo Pants"),
  item("bottom_suit", "bottom", "rare", "Takım Pantolonu", "Suit Pants"),
  item("bottom_ripped", "bottom", "rare", "Yırtık Kot", "Ripped Jeans"),
  item("bottom_ninja", "bottom", "epic", "Ninja Şalvarı", "Ninja Pants"),
  item("bottom_flame", "bottom", "epic", "Alevli Pantolon", "Flame Pants"),
  item("bottom_gold", "bottom", "legendary", "Altın Pantolon", "Golden Pants", DIAMOND),
  // Ayakkabı
  item("shoes_sneaker", "shoes", "common", "Spor Ayakkabı", "Sneakers"),
  item("shoes_sandal", "shoes", "common", "Plaj Terliği", "Beach Sandals"),
  item("shoes_canvas", "shoes", "common", "Bez Ayakkabı", "Canvas Shoes"),
  item("shoes_boots", "shoes", "uncommon", "Maceracı Botu", "Explorer Boots"),
  item("shoes_hightop", "shoes", "uncommon", "Basket Ayakkabısı", "High-Tops"),
  item("shoes_neon", "shoes", "rare", "Neon Ayakkabı", "Neon Shoes"),
  item("shoes_cowboy", "shoes", "rare", "Kovboy Çizmesi", "Cowboy Boots"),
  item("shoes_rocket", "shoes", "epic", "Roket Ayakkabı", "Rocket Shoes"),
  item("shoes_skate", "shoes", "epic", "Tekerlekli Paten", "Roller Skates"),
  item("shoes_wing", "shoes", "legendary", "Kanatlı Ayakkabı", "Winged Shoes", DIAMOND),
  // Pelerin
  item("cape_red", "cape", "common", "Kırmızı Pelerin", "Red Cape"),
  item("cape_blue", "cape", "common", "Mavi Pelerin", "Blue Cape"),
  item("cape_green", "cape", "uncommon", "Orman Pelerini", "Forest Cape"),
  item("cape_striped", "cape", "uncommon", "Çizgili Pelerin", "Striped Cape"),
  item("cape_hero", "cape", "rare", "Kahraman Pelerini", "Hero Cape"),
  item("cape_night", "cape", "rare", "Gece Pelerini", "Night Cape"),
  item("cape_royal", "cape", "epic", "Kraliyet Pelerini", "Royal Cape"),
  item("cape_fire", "cape", "epic", "Alev Pelerini", "Fire Cape"),
  item("cape_wings", "cape", "legendary", "Melek Kanatları", "Angel Wings"),
  item("cape_gold", "cape", "legendary", "Altın Pelerin", "Golden Cape", DIAMOND),
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

// TEST: yeni açılan hesaba verilen altın ve elmas (denemeler için). Yayından önce ikisi de 0 yapılmalı.
export const START_GOLD = 10000;
export const START_DIAMONDS = 1000;

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
export function rollShop(owned: string[], rng: () => number = Math.random): string[] {
  const pool = CATALOG.filter((entry) => owned.indexOf(entry.id) < 0);
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

// ---------- Günlük reklam sandığı ----------

// Günde bir kez (gece 00.00'da yenilenir) reklam izleyerek açılan ücretsiz sandığın türü
export const AD_CHEST_TIER: ChestTier = "silver";
