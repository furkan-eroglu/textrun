import assert from "node:assert/strict";
import { test } from "node:test";
import { DEFAULT_CONFIG, loadConfig } from "./config.ts";
import type { RaceConfig } from "./config.ts";
import {
  addHuman,
  beginRace,
  canJoin,
  expireReservations,
  createRaceState,
  humanCount,
  playerList,
  removeHuman,
  reserveSeat,
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
  assert.equal(canJoin(state, "u6", 0), "full");
  assert.equal(addHuman(state, "u1", "Oyuncu 1", 0), "rejoined");

  startRace(state);
  assert.equal(addHuman(state, "gec", "Gec", 0), "closed");
  assert.equal(canJoin(state, "u2", 0), "rejoined");
});

test("lobi: katilma izni verilen ama henuz girmemis oyuncular da kapasiteden sayilir (6. kisi reddedilir)", () => {
  const state = makeState();
  // 6 oyuncu AYNI ANDA katilma izni ister; hicbiri henuz matchJoin'e ulasmadi
  const outcomes = [1, 2, 3, 4, 5, 6].map((i) => {
    const outcome = canJoin(state, "u" + i, 0);
    if (outcome === "joined") reserveSeat(state, "u" + i, 0);
    return outcome;
  });
  assert.deepEqual(outcomes, ["joined", "joined", "joined", "joined", "joined", "full"]);

  // Ayni oyuncu tekrar izin isterse ikinci koltuk almaz
  assert.equal(canJoin(state, "u1", 100), "joined");
  assert.equal(canJoin(state, "u6", 100), "full");

  // Gercekten girince rezervasyon gercek koltuga donusur, sayi degismez
  for (let i = 1; i <= 5; i++) addHuman(state, "u" + i, "O" + i, 200);
  assert.equal(humanCount(state), 5);
  assert.equal(canJoin(state, "u6", 300), "full");
});

test("lobi: girmeyen oyuncunun rezervasyonu sure dolunca kalkar", () => {
  const state = makeState();
  for (let i = 1; i <= 5; i++) reserveSeat(state, "u" + i, 0);
  assert.equal(canJoin(state, "u6", 1000), "full");
  expireReservations(state, 10000);
  assert.equal(canJoin(state, "u6", 10000), "joined", "5 sn gecince bos koltuklar tekrar acilir");
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

test("dogruluk tus duzeyinde: bildirilen yanlis tuslar hesaba katilir, sinirsiz sisirilemez", () => {
  const state = makeState("en", { minWordsForWpmCheck: 1000 });
  fillHumans(state, 1);
  startCountdown(state, 0);
  beginRace(state);
  const start = state.raceStartAt;
  state.words[0] = "abcde"; // 5 harf + bosluk = 6 dogru tus

  assert.equal(submitWord(state, "u1", "abcde", start + 1000, 3), "correct");
  assert.equal(state.players.u1.errors, 3);

  assert.equal(submitWord(state, "u1", state.words[1], start + 2000, 99999), "correct");
  assert.equal(state.players.u1.errors, 3 + 50, "tek kelimede en fazla 50 hata sayilir");

  assert.equal(submitWord(state, "u1", state.words[2], start + 3000, -7), "correct");
  assert.equal(submitWord(state, "u1", state.words[3], start + 4000, Number.NaN), "correct");
  assert.equal(state.players.u1.errors, 53, "negatif ve gecersiz degerler yok sayilir");

  state.words.length = 4; // listeyi 4 kelimeye indir: oyuncu bitirdi sayilsin
  state.players.u1.finishedAt = start + 4000;
  updateRaceEnd(state, start + 5000); // bitiren oldu: geri sayim baslar
  assert.ok(updateRaceEnd(state, start + 5000 + state.cfg.finishGraceMs), "geri sayim dolunca yaris biter");
  const result = state.results![0];
  const expected = state.players.u1.correctChars / (state.players.u1.correctChars + 53);
  assert.ok(Math.abs(result.accuracy - expected) < 0.001, "dogruluk = dogru tus / (dogru tus + hata)");
  assert.ok(result.accuracy < 1);
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
    stepBots(state, now); // yalnızca botların kendi hızı ölçülür (yarış sonu kuralı bu testin konusu değil)
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

test("ilk bitirenden sonra 10 sn geri sayim baslar; bitirenler sureye, bitirmeyenler ilerlemeye gore siralanir", () => {
  const state = makeState("en", { wordCount: 6, finishGraceMs: 10000, minWordsForWpmCheck: 1000, maxPlayers: 4 });
  fillHumans(state, 4);
  const start = startRace(state);

  // u1 en önce bitirir
  for (let i = 0; i < 6; i++) submitWord(state, "u1", state.words[i], start + 8000 - (5 - i));
  assert.ok(!updateRaceEnd(state, start + 8000), "geri sayim basladi ama yaris surmeli");
  assert.equal(state.graceDeadline, start + 8000 + 10000, "ilk bitirenden 10 sn sonra");

  // u2 geri sayim icinde bitirir, u3 4 kelimede, u4 2 kelimede kalir
  for (let i = 0; i < 6; i++) submitWord(state, "u2", state.words[i], start + 11000 - (5 - i));
  for (let i = 0; i < 4; i++) submitWord(state, "u3", state.words[i], start + 9000 + i);
  for (let i = 0; i < 2; i++) submitWord(state, "u4", state.words[i], start + 9000 + i);

  assert.ok(!updateRaceEnd(state, start + 17999), "sure dolmadan bitmemeli");
  assert.ok(updateRaceEnd(state, start + 18000), "sure dolunca bitmeli");

  const results = state.results!;
  assert.deepEqual(results.map((r) => r.userId), ["u1", "u2", "u3", "u4"]);
  assert.deepEqual(results.map((r) => r.status), ["finished", "finished", "dnf", "dnf"]);
  assert.deepEqual(results.map((r) => r.correctWords), [6, 6, 4, 2]);
});

test("ilerleme esitliginde kelimeye once ulasan one gecer", () => {
  const state = makeState("en", { wordCount: 10, finishGraceMs: 5000, minWordsForWpmCheck: 1000, maxPlayers: 3 });
  fillHumans(state, 3);
  const start = startRace(state);

  for (let i = 0; i < 10; i++) submitWord(state, "u1", state.words[i], start + 6000 - (9 - i)); // geri sayimi baslatir
  // u2 ve u3 ikisi de 4 kelimede; u3 (daha yuksek kulvar) daha ONCE ulasti
  for (let i = 0; i < 4; i++) submitWord(state, "u3", state.words[i], start + 1000 + i * 100);
  for (let i = 0; i < 4; i++) submitWord(state, "u2", state.words[i], start + 3000 + i * 100);

  updateRaceEnd(state, start + 6000);
  assert.ok(updateRaceEnd(state, start + 11000));
  assert.deepEqual(state.results!.map((r) => r.userId), ["u1", "u3", "u2"], "u3 once ulasti, u2'nin onunde olmali");
});

test("herkes bitirirse geri sayim beklenmez", () => {
  const state = makeState("en", { wordCount: 3, finishGraceMs: 10000, minWordsForWpmCheck: 1000, maxPlayers: 2 });
  fillHumans(state, 2); // 2 kisilik oda: bot yok
  const start = startRace(state);

  for (let i = 0; i < 3; i++) submitWord(state, "u1", state.words[i], start + 5000 + i);
  assert.ok(!updateRaceEnd(state, start + 5100), "biri bitirdi, digeri surdu");
  for (let i = 0; i < 3; i++) submitWord(state, "u2", state.words[i], start + 6000 + i);
  assert.ok(updateRaceEnd(state, start + 6100), "ikisi de bitirdi: sure dolmadan bitmeli");
  assert.deepEqual(state.results!.map((r) => r.userId), ["u1", "u2"]);
});

test("bot once bitirirse insan icin de geri sayim baslar ve insan yetisebilir", () => {
  const state = makeState("en", { wordCount: 3, finishGraceMs: 10000, minWordsForWpmCheck: 1000, botMinWpm: 200, botMaxWpm: 220 });
  fillHumans(state, 1);
  const start = startRace(state);

  let now = start;
  while (state.graceDeadline === 0 && now - start < 60000) {
    now += 100;
    stepBots(state, now);
    updateRaceEnd(state, now);
  }
  assert.ok(state.graceDeadline > 0, "bot bitirince geri sayim baslamali");
  assert.equal(state.phase, "racing", "insan henuz bitirmedi, yaris surmeli");
  assert.ok(state.players.u1.finishedAt === 0);

  // insan geri sayim icinde bitirir; botlar da bittiyse ya da sure dolunca yaris biter
  const human = state.players.u1;
  for (let i = 0; i < 3; i++) submitWord(state, "u1", state.words[i], now + 500 + i);
  assert.ok(human.finishedAt > 0, "insan geri sayim icinde bitirebildi");

  while (state.phase === "racing" && now - start < 120000) {
    now += 100;
    stepBots(state, now);
    updateRaceEnd(state, now);
  }
  assert.equal(state.phase, "finished");
  const me = state.results!.find((r) => r.userId === "u1")!;
  assert.equal(me.status, "finished");
});

test("yarisi surduren insan kalmadiysa (diskalifiye) geri sayim yine baslar", () => {
  const state = makeState("en", { maxWpm: 250, minWordsForWpmCheck: 3, finishGraceMs: 10000 });
  fillHumans(state, 1);
  const start = startRace(state);

  let outcome = "";
  for (let i = 0; i < 8 && outcome !== "disqualified"; i++) outcome = submitWord(state, "u1", state.words[i], start + 1000 + i * 5);
  assert.equal(outcome, "disqualified");

  assert.ok(!updateRaceEnd(state, start + 2000));
  assert.equal(state.graceDeadline, start + 2000 + 10000, "insan kalmadi: geri sayim baslar");
  assert.ok(updateRaceEnd(state, start + 12000));
  assert.equal(state.results!.find((r) => r.userId === "u1")!.status, "disqualified");
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
