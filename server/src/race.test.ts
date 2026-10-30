import assert from "node:assert/strict";
import { test } from "node:test";
import { DEFAULT_CONFIG, loadConfig } from "./config.ts";
import type { RaceConfig } from "./config.ts";
import {
  addHuman,
  beginRace,
  canJoin,
  createRaceState,
  humanCount,
  playerList,
  removeHuman,
  shouldStartCountdown,
  startCountdown,
  stepBots,
  submitWord,
  updateRaceEnd,
} from "./race.ts";
import type { RaceState } from "./race.ts";
import { toLowerForLang } from "./text.ts";
import { generateWords, isSupportedLang } from "./words.ts";

function makeState(lang = "tr", overrides: Partial<RaceConfig> = {}): RaceState {
  return createRaceState(lang, "public", { ...DEFAULT_CONFIG, ...overrides }, 0, 12345);
}

function fillHumans(state: RaceState, count: number, now = 0): void {
  for (let i = 1; i <= count; i++) addHuman(state, "u" + i, "Oyuncu " + i, now);
}

// Geri sayımı bitirip yarışı başlatır; yarış başlangıç zamanını döndürür.
function startRace(state: RaceState, now = 0): number {
  startCountdown(state, now);
  beginRace(state);
  return state.raceStartAt;
}

test("Turkce I/i kurali", () => {
  assert.equal(toLowerForLang("Iİ", "tr"), "ıi");
  assert.equal(toLowerForLang("ÇĞÖŞÜ", "tr"), "çğöşü");
  assert.equal(toLowerForLang("I", "en"), "i");
});

test("kelime uretimi: ayni tohum ayni liste, dile ozel, yakin tekrar yok", () => {
  const a = generateWords("tr", 200, { rng: 7 });
  const b = generateWords("tr", 200, { rng: 7 });
  const c = generateWords("tr", 200, { rng: 8 });
  assert.deepEqual(a, b);
  assert.notDeepEqual(a, c);
  assert.ok(a.some((w) => /[çğıöşü]/.test(w)), "Turkce listede ozel harf olmali");

  const en = generateWords("en", 200, { rng: 7 });
  assert.ok(en.every((w) => /^[a-z]+$/.test(w)), "Ingilizce liste sadece a-z");

  for (let i = 0; i < a.length; i++) {
    for (let j = Math.max(0, i - 30); j < i; j++) assert.notEqual(a[i], a[j], "yakin tekrar");
  }
  assert.ok(isSupportedLang("tr") && isSupportedLang("en") && !isSupportedLang("fr") && !isSupportedLang(undefined));
});

test("ayarlar: ortam degiskenleri gecerliyse uygulanir, bozuksa yok sayilir", () => {
  const cfg = loadConfig({ TEXTRUN_LOBBY_WAIT_MS: "3000", TEXTRUN_MAX_WPM: "abc", TEXTRUN_WORD_COUNT: "-5" });
  assert.equal(cfg.lobbyWaitMs, 3000);
  assert.equal(cfg.maxWpm, DEFAULT_CONFIG.maxWpm);
  assert.equal(cfg.wordCount, DEFAULT_CONFIG.wordCount);
});

test("lobi: kapasite, yeniden baglanma, kapali oda", () => {
  const state = makeState();
  fillHumans(state, 5);
  assert.equal(humanCount(state), 5);
  assert.equal(addHuman(state, "u6", "Fazla", 0), "full");
  assert.equal(canJoin(state, "u6"), "full");
  assert.equal(addHuman(state, "u1", "Oyuncu 1", 0), "rejoined");

  startRace(state);
  assert.equal(addHuman(state, "gec", "Gec", 0), "closed");
  assert.equal(canJoin(state, "u2"), "rejoined");
});

test("lobi: 5 insan varsa hemen, yoksa sure dolunca baslar ve botla dolar", () => {
  const full = makeState();
  fillHumans(full, 5);
  assert.ok(shouldStartCountdown(full, 1), "5 insanda beklemeden baslamali");

  const solo = makeState("tr", { lobbyWaitMs: 15000 });
  addHuman(solo, "u1", "Tek", 1000);
  assert.ok(!shouldStartCountdown(solo, 15999), "sure dolmadan baslamamali");
  assert.ok(shouldStartCountdown(solo, 16000), "ilk oyuncudan 15 sn sonra baslamali");

  startCountdown(solo, 16000);
  const players = playerList(solo);
  assert.equal(players.length, 5);
  assert.equal(players.filter((p) => p.isBot).length, 4);
  assert.ok(players.filter((p) => p.isBot).every((p) => p.name.startsWith("Bot ")));
  assert.equal(solo.words.length, 40);
  assert.equal(solo.raceStartAt, 16000 + DEFAULT_CONFIG.countdownMs);
});

test("lobi: bos kalan lobide sayac sifirlanir", () => {
  const state = makeState();
  addHuman(state, "u1", "A", 500);
  assert.equal(state.lobbyDeadline, 500 + DEFAULT_CONFIG.lobbyWaitMs);
  removeHuman(state, "u1", 600);
  assert.equal(humanCount(state), 0);
  assert.equal(state.lobbyDeadline, 0);
  assert.ok(!shouldStartCountdown(state, 999999));
});

test("kelime gonderme: baslamadan ignore, yanlis ilerletmez, buyuk harf kabul", () => {
  const state = makeState("tr");
  fillHumans(state, 1);
  startCountdown(state, 0);
  beginRace(state);
  const start = state.raceStartAt;

  assert.equal(submitWord(state, "u1", state.words[0], start - 1), "ignored", "geri sayimda kabul edilmemeli");

  assert.equal(submitWord(state, "u1", "kesinlikleyanlis", start + 1000), "wrong");
  assert.equal(state.players.u1.index, 0, "yanlis kelime ilerletmemeli");
  assert.equal(state.players.u1.errors, 1);

  assert.equal(submitWord(state, "u1", state.words[0].toUpperCase(), start + 2000), "correct");
  assert.equal(state.players.u1.index, 1);

  assert.equal(submitWord(state, "bot-1", "x", start + 3000), "ignored", "bot adina gonderilemez");
  assert.equal(submitWord(state, "yok", "x", start + 3000), "ignored");
  assert.equal(submitWord(state, "u1", "", start + 3000), "ignored");
  assert.equal(submitWord(state, "u1", "a".repeat(100), start + 3000), "ignored");
});

test("Turkce: buyuk I ve I-noktali dogru kelimeye cevrilir", () => {
  const state = makeState("tr");
  fillHumans(state, 1);
  startCountdown(state, 0);
  beginRace(state);
  state.words[0] = "ısı"; // ısı
  assert.equal(submitWord(state, "u1", "ISI", state.raceStartAt + 1000), "correct");
  state.words[1] = "ilaç"; // ilaç
  assert.equal(submitWord(state, "u1", "İLAÇ", state.raceStartAt + 2000), "correct");
  state.words[2] = "ilaç";
  assert.equal(submitWord(state, "u1", "Ilac", state.raceStartAt + 3000), "wrong", "ac yerine ç gerekir, I de i degil ı olur");
});

test("hile: insan icin imkansiz hiz diskalifiye eder", () => {
  const state = makeState("en", { maxWpm: 250 });
  fillHumans(state, 2);
  const start = startRace(state);

  let outcome = "";
  for (let i = 0; i < 10 && outcome !== "disqualified"; i++) {
    outcome = submitWord(state, "u1", state.words[i], start + 1000 + i * 10);
  }
  assert.equal(outcome, "disqualified");
  assert.ok(state.players.u1.disqualified);
  assert.equal(submitWord(state, "u1", state.words[state.players.u1.index], start + 5000), "ignored");

  // Makul hız (yaklaşık 80 WPM) sorun çıkarmaz
  let t = start;
  for (let i = 0; i < 20; i++) {
    t += ((state.words[i].length + 1) / ((80 * 5) / 60000));
    assert.notEqual(submitWord(state, "u2", state.words[i], t), "disqualified");
  }
});

test("botlar yarisi bitirir ve hizlari kendi WPM degerlerine yakindir", () => {
  const state = makeState("en", { wordCount: 40 });
  fillHumans(state, 1);
  const start = startRace(state);

  let now = start;
  while (state.phase === "racing" && now - start < 400000) {
    now += 100;
    stepBots(state, now);
    updateRaceEnd(state, now);
    submitWord(state, "u1", "x", now); // insan oyuncu ilerlemiyor
    if (now - start > 150000) break;
  }
  const bots = playerList(state).filter((p) => p.isBot);
  for (const bot of bots) {
    assert.ok(bot.finishedAt > 0, bot.name + " bitirmeli");
    const measured = (bot.correctChars / 5) / ((bot.finishedAt - start) / 60000);
    assert.ok(measured > bot.botWpm * 0.5 && measured < bot.botWpm * 1.5, `bot hizi ${measured.toFixed(1)} ~ ${bot.botWpm.toFixed(1)}`);
  }
});

test("ilk 3 bitirince bekleme basar, sure dolunca bitmeyenler son siraya", () => {
  const state = makeState("en", { wordCount: 6, finishGraceMs: 20000, minWordsForWpmCheck: 1000 });
  fillHumans(state, 5);
  const start = startRace(state);

  // u1, u2, u3 sırayla bitirir; u4 3 kelimede kalır; u5 hiç yazmaz
  const finishAt: { [id: string]: number } = { u1: start + 10000, u2: start + 11000, u3: start + 12000 };
  for (const id of ["u1", "u2", "u3"]) {
    for (let i = 0; i < 6; i++) submitWord(state, id, state.words[i], finishAt[id] - (5 - i));
  }
  for (let i = 0; i < 3; i++) submitWord(state, "u4", state.words[i], start + 5000 + i);

  assert.ok(!updateRaceEnd(state, start + 13000), "bekleme suresi daha dolmadi");
  assert.equal(state.graceDeadline, start + 13000 + 20000);
  assert.ok(updateRaceEnd(state, start + 33000), "20 sn sonra bitmeli");

  const results = state.results!;
  assert.deepEqual(results.map((r) => r.userId), ["u1", "u2", "u3", "u4", "u5"]);
  assert.deepEqual(results.map((r) => r.rank), [1, 2, 3, 4, 5]);
  assert.deepEqual(results.map((r) => r.status), ["finished", "finished", "finished", "dnf", "dnf"]);
  assert.equal(results[0].accuracy, 1);
});

test("baglanti kopmasi: sure icinde geri donebilir, sure bitince yarisi kaybeder", () => {
  const state = makeState("en", { disconnectGraceMs: 30000 });
  fillHumans(state, 2);
  const start = startRace(state);

  removeHuman(state, "u1", start + 1000);
  assert.ok(!state.players.u1.connected);
  assert.ok(!updateRaceEnd(state, start + 20000), "u2 hala yaziyor, yaris surmeli");

  assert.equal(addHuman(state, "u1", "Oyuncu 1", start + 25000), "rejoined");
  assert.ok(state.players.u1.connected);
  assert.equal(submitWord(state, "u1", state.words[0], start + 26000), "correct");
});

test("tum insanlar ayrilinca (sure dolunca) yaris biter", () => {
  const state = makeState("en", { disconnectGraceMs: 30000 });
  fillHumans(state, 2);
  const start = startRace(state);

  removeHuman(state, "u1", start + 1000);
  removeHuman(state, "u2", start + 2000);
  assert.ok(!updateRaceEnd(state, start + 10000), "sure dolmadan beklemeli");
  assert.ok(updateRaceEnd(state, start + 33000), "sure dolunca bitmeli");
  assert.equal(state.phase, "finished");
});

test("yaris cok uzarsa zaman asimi ile biter", () => {
  const state = makeState("en", { raceMaxMs: 60000 });
  fillHumans(state, 1);
  const start = startRace(state);
  assert.ok(!updateRaceEnd(state, start + 59000));
  assert.ok(updateRaceEnd(state, start + 60000));
  assert.equal(state.results![0].status, "dnf");
});
