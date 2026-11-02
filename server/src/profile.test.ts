import assert from "node:assert/strict";
import { test } from "node:test";
import { applyRace, cleanName, emptyProfile, equipItem, levelOf, lookOf, openChest, racePaysXp, unequipCategory, xpForNextLevel, xpForRace } from "./profile.ts";

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
  assert.equal(equipItem(profile, "glasses_sun"), true);
  assert.deepEqual(lookOf(profile), ["glasses_sun", "hair_afro"]);
  assert.equal(unequipCategory(profile, "head"), true);
  assert.deepEqual(lookOf(profile), ["glasses_sun"]);
  assert.equal(unequipCategory(profile, "bilinmeyen"), false);
});
