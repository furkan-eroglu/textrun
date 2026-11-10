import assert from "node:assert/strict";
import { test } from "node:test";
import { CATALOG, CATEGORIES, CHEST_TIERS, CHEST_WEIGHTS, RARITIES, chestTierForLevel, duplicateGold, goldForRace, itemById, rollChest } from "./economy.ts";

test("altın: bitirmek 10, üstüne sıra bonusu; bitirmeyen ve hile yapan almaz", () => {
  assert.equal(goldForRace(1, "finished"), 35);
  assert.equal(goldForRace(2, "finished"), 25);
  assert.equal(goldForRace(3, "finished"), 20);
  assert.equal(goldForRace(4, "finished"), 15);
  assert.equal(goldForRace(5, "finished"), 10);
  assert.equal(goldForRace(1, "dnf"), 0);
  assert.equal(goldForRace(1, "disqualified"), 0);
});

test("sandık türü seviyeye göre: 1-9 bronz, 10-19 gümüş, 20+ altın", () => {
  assert.equal(chestTierForLevel(2), "bronze");
  assert.equal(chestTierForLevel(9), "bronze");
  assert.equal(chestTierForLevel(10), "silver");
  assert.equal(chestTierForLevel(19), "silver");
  assert.equal(chestTierForLevel(20), "gold");
  assert.equal(chestTierForLevel(80), "gold");
});

test("eşya listesi: kimlikler benzersiz, her nadirlikten ve sandıktan çıkan eşya var", () => {
  const ids = new Set(CATALOG.map((entry) => entry.id));
  assert.equal(ids.size, CATALOG.length);
  for (const rarity of RARITIES) assert.ok(CATALOG.some((entry) => entry.rarity === rarity && entry.chest), rarity + " nadirliğinde sandık eşyası olmalı");
  assert.equal(itemById("hat_crown")?.rarity, "legendary");
  assert.equal(itemById("yok"), undefined);
});

test("eşya listesi: 5 kategori, her birinde 10 eşya; elmasa özel eşya sandıktan çıkmaz", () => {
  for (const category of CATEGORIES) assert.equal(CATALOG.filter((entry) => entry.category === category).length, 10, category);
  assert.equal(CATALOG.length, 50);
  for (const tier of CHEST_TIERS) {
    for (let i = 0; i < 400; i++) assert.equal(itemById(rollChest(tier, [], () => Math.random()).itemId)!.diamond, false);
  }
});

test("sandık oranları: üst sandıkta nadir eşya olasılığı daha yüksek", () => {
  const rareOrBetter = (tier: "bronze" | "silver" | "gold") => {
    const w = CHEST_WEIGHTS[tier];
    return (w.rare + w.epic + w.legendary) / (w.common + w.uncommon + w.rare + w.epic + w.legendary);
  };
  assert.ok(rareOrBetter("silver") > rareOrBetter("bronze"));
  assert.ok(rareOrBetter("gold") > rareOrBetter("silver"));
});

test("sandık açma: dağılım ağırlıklara yaklaşır (bronzda efsanevi çıkmaz)", () => {
  let seed = 12345;
  const rng = () => {
    seed = (seed * 1664525 + 1013904223) % 4294967296;
    return seed / 4294967296;
  };
  const counts: { [rarity: string]: number } = {};
  const runs = 20000;
  for (let i = 0; i < runs; i++) {
    const rarity = rollChest("bronze", [], rng).rarity;
    counts[rarity] = (counts[rarity] || 0) + 1;
  }
  assert.equal(counts.legendary || 0, 0, "bronz sandıkta efsanevi yok");
  assert.ok(Math.abs(counts.common / runs - 0.7) < 0.03, "yaygın yaklaşık %70");
  assert.ok(Math.abs(counts.uncommon / runs - 0.22) < 0.03, "sıradışı yaklaşık %22");
});

test("tekrar eşya: sahip olunan eşya tekrar çıkarsa altın karşılığı verilir", () => {
  const first = rollChest("gold", [], () => 0.5);
  const again = rollChest("gold", [first.itemId], () => 0.5);
  assert.equal(again.itemId, first.itemId);
  assert.equal(again.duplicate, true);
  assert.equal(again.goldRefund, duplicateGold(again.rarity));
});
