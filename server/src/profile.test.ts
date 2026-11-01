import assert from "node:assert/strict";
import { test } from "node:test";
import { applyRace, cleanName, emptyProfile, levelOf, racePaysXp, xpForNextLevel, xpForRace } from "./profile.ts";

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

test("özel oda: tek başına ya da yalnızca botlarla XP yok, 2 gerçek oyuncuyla var; herkese açık hep var", () => {
  assert.equal(racePaysXp("private", 1), false);
  assert.equal(racePaysXp("private", 2), true);
  assert.equal(racePaysXp("public", 1), true);
});

test("yarış işleme: XP, yarış sayısı, galibiyet ve seviye atlama", () => {
  const profile = emptyProfile();
  let reward = applyRace(profile, 1, "finished");
  assert.deepEqual(reward, { gain: 70, xp: 70, level: 1, leveledUp: false });
  reward = applyRace(profile, 2, "finished");
  assert.deepEqual(reward, { gain: 55, xp: 125, level: 2, leveledUp: true });
  applyRace(profile, 4, "dnf");
  assert.equal(profile.races, 3);
  assert.equal(profile.wins, 1);
  assert.equal(profile.xp, 130);
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
