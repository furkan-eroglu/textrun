import assert from "node:assert/strict";
import { test } from "node:test";
import { CATALOG, SHOP_DIAMOND_CHEST, SHOP_GOLD_CHEST, SHOP_REFRESH_DIAMONDS, itemById, priceOf, rollShop, shopDay, shopSecondsLeft } from "./economy.ts";
import {
  SHOP_DIAMOND_CHEST_SLOT, SHOP_GOLD_CHEST_SLOT, adChestReady, applyRace, buyShopSlot, claimAdChest, cleanName, emptyProfile, ensureShop, equipItem, levelOf, lookOf, openChest, racePaysXp, refreshShop,
  unequipCategory, xpForNextLevel, xpForRace,
} from "./profile.ts";

// Verilen seviyeye ulaşmak için gereken toplam XP
function xpToReach(level: number): number {
  let total = 0;
  for (let l = 1; l < level; l++) total += xpForNextLevel(l);
  return total;
}

test("seviye: 0 XP seviye 1, 100 XP seviye 2", () => {
  assert.deepEqual(levelOf(0), { level: 1, xpIntoLevel: 0, xpForNext: 100 });
  assert.deepEqual(levelOf(99), { level: 1, xpIntoLevel: 99, xpForNext: 100 });
  assert.deepEqual(levelOf(100), { level: 2, xpIntoLevel: 0, xpForNext: 140 });
  assert.deepEqual(levelOf(240), { level: 3, xpIntoLevel: 0, xpForNext: 180 });
});

test("seviye: sonraki seviyeye gereken XP her seviyede artar", () => {
  for (let level = 1; level < 60; level++) assert.ok(xpForNextLevel(level + 1) > xpForNextLevel(level));
});

test("seviye: eksi ya da bozuk XP seviye 1 verir", () => {
  assert.equal(levelOf(-50).level, 1);
});

test("yarış XP'si: sıraya göre sabit, seviyeden bağımsız", () => {
  assert.equal(xpForRace(1, "finished"), 70);
  assert.equal(xpForRace(2, "finished"), 55);
  assert.equal(xpForRace(3, "finished"), 45);
  assert.equal(xpForRace(4, "finished"), 35);
  assert.equal(xpForRace(5, "finished"), 30);
  assert.equal(xpForRace(5, "dnf"), 5);
  assert.equal(xpForRace(1, "disqualified"), 0);
});

test("yarış XP'si: bitiren her zaman bitirmeyenden fazla alır", () => {
  for (let rank = 1; rank <= 5; rank++) assert.ok(xpForRace(rank, "finished") > xpForRace(rank, "dnf"));
});

test("özel oda XP vermez (kaç oyuncu olursa olsun); herkese açık yarış verir", () => {
  assert.equal(racePaysXp("private"), false);
  assert.equal(racePaysXp("public"), true);
});

test("yarış işleme: XP, yarış sayısı, galibiyet ve seviye atlama", () => {
  const profile = emptyProfile();
  let reward = applyRace(profile, 1, "finished");
  assert.deepEqual(reward, { gain: 70, xp: 70, level: 1, xpIntoLevel: 70, xpForNext: 100, leveledUp: false, gold: 35, chests: [] });
  reward = applyRace(profile, 2, "finished");
  assert.deepEqual(reward, { gain: 55, xp: 125, level: 2, xpIntoLevel: 25, xpForNext: 140, leveledUp: true, gold: 25, chests: ["bronze"] });
  applyRace(profile, 4, "dnf");
  assert.equal(profile.races, 3);
  assert.equal(profile.wins, 1);
  assert.equal(profile.xp, 130);
  assert.equal(profile.gold, 60, "bitirmeyen altın almaz");
  assert.deepEqual(profile.chests, { bronze: 1, silver: 0, gold: 0 });
});

test("her seviye atlamada tam bir sandık; sandığın türü ulaşılan seviyeye göre", () => {
  const profile = emptyProfile();
  profile.xp = 99;
  assert.deepEqual(applyRace(profile, 1, "finished").chests, ["bronze"]);

  // 9. seviyeden 10'a geçiş gümüş sandık verir
  const silver = emptyProfile();
  silver.xp = xpToReach(10) - 1;
  assert.deepEqual(applyRace(silver, 1, "finished").chests, ["silver"]);
  assert.equal(silver.chests.silver, 1);

  // seviye atlanmazsa sandık yok
  const none = emptyProfile();
  assert.deepEqual(applyRace(none, 5, "dnf").chests, []);
});

test("sandık açma: sayı düşer, eşya envantere girer; sandık yoksa açılmaz", () => {
  const profile = emptyProfile();
  assert.equal(openChest(profile, "bronze", () => 0), null);

  profile.chests.bronze = 2;
  const first = openChest(profile, "bronze", () => 0)!;
  assert.equal(first.duplicate, false);
  assert.deepEqual(profile.items, [first.itemId]);
  assert.equal(profile.chests.bronze, 1);

  // aynı rastgele sayı aynı eşyayı verir: tekrar eşya envantere girmez, altın verir
  const second = openChest(profile, "bronze", () => 0)!;
  assert.equal(second.duplicate, true);
  assert.equal(profile.items.length, 1);
  assert.equal(profile.gold, second.goldRefund);
  assert.ok(second.goldRefund > 0);
  assert.equal(profile.chests.bronze, 0);
});

test("ad: geçerli adlar düzenlenerek kabul edilir", () => {
  assert.equal(cleanName("Furkan"), "Furkan");
  assert.equal(cleanName("  Ali   Veli "), "Ali Veli");
  assert.equal(cleanName("Çağrı_Şahin"), "Çağrı_Şahin");
  assert.equal(cleanName("abc"), "abc");
});

test("ad: çok kısa, çok uzun, yasak karakter ve yalnızca simge reddedilir", () => {
  assert.equal(cleanName("ab"), null);
  assert.equal(cleanName("a".repeat(13)), null);
  assert.equal(cleanName("  a  "), null);
  assert.equal(cleanName("ali<script>"), null);
  assert.equal(cleanName("😀😀😀"), null);
  assert.equal(cleanName("___"), null);
  assert.equal(cleanName(""), null);
  assert.equal(cleanName(undefined), null);
  assert.equal(cleanName(12345), null);
});

test("esya takma: yalnizca sahip olunan esya takilir, ayni kategoride yenisi eskisini cikarir", () => {
  const profile = emptyProfile();
  profile.items = ["hat_cap", "hair_afro", "glasses_sun"];
  assert.equal(equipItem(profile, "hat_crown"), false, "sahip olunmayan esya takilmaz");
  assert.equal(equipItem(profile, "yok_boyle_esya"), false);
  assert.equal(equipItem(profile, 5), false);
  assert.equal(equipItem(profile, "hat_cap"), true);
  assert.equal(equipItem(profile, "hair_afro"), true, "sapka ve sac ayni kategori: yenisi eskisini cikarir");
  assert.equal(profile.equipped.head, "hair_afro");
  assert.equal(equipItem(profile, "glasses_sun"), true, "gozluk de kafa kategorisinde: saci cikarir");
  assert.deepEqual(lookOf(profile), ["glasses_sun"]);
  profile.items.push("cape_red");
  assert.equal(equipItem(profile, "cape_red"), true);
  assert.deepEqual(lookOf(profile), ["cape_red", "glasses_sun"]);
  assert.equal(unequipCategory(profile, "head"), true);
  assert.deepEqual(lookOf(profile), ["cape_red"]);
  assert.equal(unequipCategory(profile, "bilinmeyen"), false);
  assert.equal(unequipCategory(profile, "trail"), false, "kosu efekti kategorisi kaldirildi");
});

test("magaza: gun Turkiye saatine gore degisir, geri sayim dogru", () => {
  const midnightTr = Date.UTC(2026, 10, 10, 21, 0, 0); // 11 Kasim 00.00 TR
  assert.equal(shopDay(midnightTr - 1) + 1, shopDay(midnightTr));
  assert.equal(shopSecondsLeft(midnightTr - 10 * 3600 * 1000), 10 * 3600);
  assert.equal(shopSecondsLeft(midnightTr), 24 * 3600);
});

test("magaza: 4 farkli, sahip olunmayan esya", () => {
  for (let round = 0; round < 50; round++) {
    const picked = rollShop(["hat_cap", "top_tee"]);
    assert.equal(picked.length, 4);
    assert.equal(new Set(picked).size, 4);
    for (const id of picked) {
      assert.ok(id !== "hat_cap" && id !== "top_tee");
    }
  }
  const almostAll = CATALOG.map((entry) => entry.id).slice(2);
  assert.equal(rollShop(almostAll).length, 2, "satilacak esya azsa yuva sayisi azalir");
});

test("magaza: satin alma, yetersiz bakiye, tekrar alamama, sandiklar", () => {
  const now = Date.UTC(2026, 10, 10, 12, 0, 0);
  const profile = emptyProfile();
  profile.diamonds = 0;
  assert.equal(ensureShop(profile, now), true);
  assert.equal(ensureShop(profile, now), false, "ayni gun yeniden kurulmaz");
  const first = itemById(profile.shop!.items[0])!;
  const cost = priceOf(first);

  assert.equal(buyShopSlot(profile, 0, now), "not_enough");
  assert.equal(buyShopSlot(profile, 9, now), "bad_slot");
  assert.equal(buyShopSlot(profile, "0", now), "bad_slot");
  profile.gold = 100000;
  profile.diamonds = 1000;
  assert.equal(buyShopSlot(profile, 0, now), "ok");
  assert.ok(profile.items.indexOf(first.id) >= 0);
  assert.equal(cost.currency === "gold" ? 100000 - profile.gold : 1000 - profile.diamonds, cost.price);
  assert.equal(buyShopSlot(profile, 0, now), "already_bought");

  const gold = profile.gold;
  assert.equal(buyShopSlot(profile, SHOP_GOLD_CHEST_SLOT, now), "ok");
  assert.equal(profile.gold, gold - SHOP_GOLD_CHEST.price);
  assert.equal(profile.chests[SHOP_GOLD_CHEST.tier], 1);
  assert.equal(buyShopSlot(profile, SHOP_GOLD_CHEST_SLOT, now), "already_bought", "altin sandik yenileme basina 1 kez");

  const diamonds = profile.diamonds;
  assert.equal(buyShopSlot(profile, SHOP_DIAMOND_CHEST_SLOT, now), "ok");
  assert.equal(buyShopSlot(profile, SHOP_DIAMOND_CHEST_SLOT, now), "ok", "elmas sandik sinirsiz");
  assert.equal(profile.diamonds, diamonds - 2 * SHOP_DIAMOND_CHEST.price);
  assert.equal(profile.chests[SHOP_DIAMOND_CHEST.tier], 2);
});

test("magaza: elmasla yenileme gunu degistirmez, altin sandik hakkini geri verir; gece yenilenir", () => {
  const now = Date.UTC(2026, 10, 10, 12, 0, 0);
  const profile = emptyProfile();
  profile.gold = 100000;
  profile.diamonds = SHOP_REFRESH_DIAMONDS;
  ensureShop(profile, now);
  buyShopSlot(profile, SHOP_GOLD_CHEST_SLOT, now);
  const day = profile.shop!.day;

  assert.equal(refreshShop(profile, now), "ok");
  assert.equal(profile.diamonds, 0);
  assert.equal(profile.shop!.day, day, "yenileme 00.00 zamanini degistirmez");
  assert.equal(profile.shop!.goldChestBought, false);
  assert.equal(refreshShop(profile, now), "not_enough");

  const tomorrow = now + 24 * 3600 * 1000;
  assert.equal(ensureShop(profile, tomorrow), true, "gece 00.00'dan sonra magaza yenilenir");
  assert.equal(profile.shop!.day, day + 1);
});

test("reklam sandigi: gunde bir kez, gece 00.00'da (TR) yenilenir; esya ya da altin verir", () => {
  const now = Date.UTC(2026, 10, 10, 12, 0, 0);
  const profile = emptyProfile();
  assert.equal(adChestReady(profile, now), true);
  const first = claimAdChest(profile, now);
  assert.ok(first);
  assert.equal(profile.items.length + (first!.duplicate ? 1 : 0), 1);
  assert.equal(adChestReady(profile, now), false);
  assert.equal(claimAdChest(profile, now + 3600 * 1000), null, "ayni gun ikinci kez acilmaz");
  const midnightTr = Date.UTC(2026, 10, 10, 21, 0, 0);
  assert.equal(claimAdChest(profile, midnightTr - 1000), null);
  assert.ok(claimAdChest(profile, midnightTr + 1000), "gece 00.00'dan sonra yeniden acilir");
});
