// Yarışın tüm kuralları. Nakama'dan bağımsızdır; zaman (now) dışarıdan verilir, bu yüzden Node'da test edilebilir.
import type { RaceConfig } from "./config.ts";
import { nextRandom } from "./rng.ts";
import { toLowerForLang } from "./text.ts";
import { generateWords } from "./words.ts";

export type Phase = "lobby" | "countdown" | "racing" | "finished";

export interface Player {
  id: string;
  name: string;
  isBot: boolean;
  connected: boolean;
  disconnectedAt: number;
  sessionId: string; // oyuncunun şu anki bağlantısı (eski bağlantının geç gelen "ayrıldı" bildirimini ayıklamak için)
  slot: number;
  index: number; // sıradaki kelimenin numarası
  correct: number;
  errors: number;
  correctChars: number;
  finishedAt: number; // 0 = bitirmedi
  progressAt: number; // son doğru kelimenin zamanı: ilerleme eşitliğinde kim önce ulaştı
  disqualified: boolean;
  botWpm: number;
  botNextAt: number;
}

export interface RaceResult {
  userId: string;
  name: string;
  isBot: boolean;
  rank: number;
  status: "finished" | "dnf" | "disqualified";
  finishMs: number;
  wpm: number;
  accuracy: number;
  correctWords: number;
  errors: number;
}

export interface RaceState {
  lang: string;
  kind: "public" | "private";
  cfg: RaceConfig;
  phase: Phase;
  rng: number;
  createdAt: number;
  everHadHuman: boolean;
  lobbyDeadline: number;
  raceStartAt: number;
  graceDeadline: number;
  endedAt: number;
  words: string[];
  players: { [id: string]: Player };
  order: string[];
  reserved: { [id: string]: number }; // katılma izni verilmiş ama henüz girmemiş oyuncular (kimlik -> izin zamanı)
  results: RaceResult[] | null;
  code: string; // özel oda kodu (genel odada boş)
  host: string; // özel odada yarışı başlatma yetkisi olan oyuncu
  fillBots: boolean; // özel oda: başlarken boş yerleri botla doldur
  startRequested: boolean; // özel oda: kurucu başlat dedi
}

export type JoinOutcome = "joined" | "rejoined" | "closed" | "full";
export type StartOutcome = "ok" | "not_private" | "not_host" | "not_lobby" | "too_few";
export type SubmitOutcome = "ignored" | "correct" | "wrong" | "finished" | "disqualified";

export function createRaceState(lang: string, kind: "public" | "private", cfg: RaceConfig, now: number, seed: number, code = ""): RaceState {
  return {
    lang,
    kind,
    cfg,
    phase: "lobby",
    rng: seed >>> 0,
    createdAt: now,
    everHadHuman: false,
    lobbyDeadline: 0,
    raceStartAt: 0,
    graceDeadline: 0,
    endedAt: 0,
    words: [],
    players: {},
    order: [],
    reserved: {},
    results: null,
    code,
    host: "",
    fillBots: false,
    startRequested: false,
  };
}

export function playerList(state: RaceState): Player[] {
  return state.order.map((id) => state.players[id]);
}

export function humanCount(state: RaceState): number {
  return playerList(state).filter((p) => !p.isBot).length;
}

// ---------- Lobi ----------

export function addHuman(state: RaceState, id: string, name: string, now: number, sessionId = ""): JoinOutcome {
  const existing = state.players[id];
  if (existing && !existing.isBot) {
    existing.connected = true;
    existing.disconnectedAt = 0;
    existing.sessionId = sessionId;
    return "rejoined";
  }
  if (state.phase !== "lobby") return "closed";
  if (state.order.length >= state.cfg.maxPlayers) return "full";

  delete state.reserved[id]; // rezerve edilen koltuk artık gerçek koltuk
  state.players[id] = newPlayer(id, name, false, state.order.length);
  state.players[id].sessionId = sessionId;
  state.order.push(id);
  state.everHadHuman = true;
  if (state.kind === "private") {
    if (state.host === "") state.host = id; // ilk giren oda kurucusudur
  } else if (state.lobbyDeadline === 0) state.lobbyDeadline = now + state.cfg.lobbyWaitMs;
  return "joined";
}

const RESERVATION_TTL_MS = 5000;

// Katılma izni verilen oyuncu odaya gerçekten girene (matchJoin) kadar bir koltuk "rezerve" edilir.
// Aksi halde aynı anda gelen birçok oyuncunun hepsine izin verilir ve oda 5 kişiyi aşar.
export function canJoin(state: RaceState, id: string, now: number): JoinOutcome {
  const existing = state.players[id];
  if (existing && !existing.isBot) return "rejoined";
  if (state.phase !== "lobby") return "closed";
  if (state.reserved[id] !== undefined) return "joined"; // zaten rezervasyonu var, ikinci koltuk almaz

  expireReservations(state, now);
  const reservedCount = Object.keys(state.reserved).length;
  if (state.order.length + reservedCount >= state.cfg.maxPlayers) return "full";
  return "joined";
}

export function reserveSeat(state: RaceState, id: string, now: number): void {
  state.reserved[id] = now;
}

// Girmeyen oyuncunun rezervasyonu kısa sürede kalkar.
export function expireReservations(state: RaceState, now: number): void {
  for (const id of Object.keys(state.reserved)) {
    if (now - state.reserved[id] >= RESERVATION_TTL_MS) delete state.reserved[id];
  }
}

export function removeHuman(state: RaceState, id: string, now: number, sessionId = ""): void {
  const player = state.players[id];
  if (!player || player.isBot) return;
  // Oyuncu yeni bağlantısıyla çoktan geri girdiyse, eski bağlantının geç gelen "ayrıldı" bildirimi yok sayılır
  if (sessionId !== "" && player.sessionId !== "" && player.sessionId !== sessionId) return;

  if (state.phase === "lobby") {
    delete state.players[id];
    state.order = state.order.filter((x) => x !== id);
    if (humanCount(state) === 0) state.lobbyDeadline = 0;
    if (state.host === id) state.host = nextHost(state); // kurucu çıkınca yetki sıradaki oyuncuya geçer
    return;
  }
  player.connected = false;
  player.disconnectedAt = now;
}

function nextHost(state: RaceState): string {
  const humans = playerList(state).filter((p) => !p.isBot);
  return humans.length > 0 ? humans[0].id : "";
}

// Özel odada kurucu başlat diyene kadar beklenir; boş yerleri botla doldurma tercihi kurucuya aittir.
export function requestStart(state: RaceState, id: string, fillBots: boolean): StartOutcome {
  if (state.kind !== "private") return "not_private";
  if (state.phase !== "lobby") return "not_lobby";
  if (state.host !== id) return "not_host";
  if (humanCount(state) < 2 && !fillBots) return "too_few";
  state.fillBots = fillBots;
  state.startRequested = true;
  return "ok";
}

export function shouldStartCountdown(state: RaceState, now: number): boolean {
  if (state.phase !== "lobby") return false;
  const humans = humanCount(state);
  if (humans === 0) return false;
  if (state.kind === "private") return state.startRequested;
  return humans >= state.cfg.maxPlayers || now >= state.lobbyDeadline;
}

// Boş yerleri botla doldurur, kelime listesini üretir ve geri sayımı başlatır.
export function startCountdown(state: RaceState, now: number): void {
  let botNumber = 1;
  const wantBots = state.kind === "public" || state.fillBots;
  while (wantBots && state.order.length < state.cfg.maxPlayers) {
    const id = "bot-" + botNumber;
    const bot = newPlayer(id, "Bot " + botNumber, true, state.order.length);
    bot.botWpm = state.cfg.botMinWpm + nextRandom(state) * (state.cfg.botMaxWpm - state.cfg.botMinWpm);
    state.players[id] = bot;
    state.order.push(id);
    botNumber++;
  }

  state.words = generateWords(state.lang, state.cfg.wordCount, state);
  state.phase = "countdown";
  state.raceStartAt = now + state.cfg.countdownMs;
}

function newPlayer(id: string, name: string, isBot: boolean, slot: number): Player {
  return {
    id,
    name,
    isBot,
    connected: true,
    disconnectedAt: 0,
    sessionId: "",
    slot,
    index: 0,
    correct: 0,
    errors: 0,
    correctChars: 0,
    finishedAt: 0,
    progressAt: 0,
    disqualified: false,
    botWpm: 0,
    botNextAt: 0,
  };
}

// ---------- Yarış ----------

export function beginRace(state: RaceState): void {
  state.phase = "racing";
  for (const bot of playerList(state)) {
    if (bot.isBot) bot.botNextAt = state.raceStartAt + botWordDelay(state, bot);
  }
}

export function wpmOf(correctChars: number, elapsedMs: number): number {
  const minutes = Math.max(elapsedMs, 1000) / 60000;
  return correctChars / 5 / minutes;
}

// mistakes: istemcinin bu kelimede reddettiği yanlış tuş sayısı. Yalnızca doğruluk istatistiğine yazılır,
// yarış sonucunu (sıralama, hız sınırı) etkilemez; bu yüzden istemciye güvenmek sorun değildir. Makul bir üst sınırla kırpılır.
export function submitWord(state: RaceState, playerId: string, raw: string, now: number, mistakes = 0): SubmitOutcome {
  if (state.phase !== "racing" || now < state.raceStartAt) return "ignored";
  const player = state.players[playerId];
  if (!player || player.isBot || player.finishedAt > 0 || player.disqualified) return "ignored";
  if (typeof raw !== "string" || raw.length === 0 || raw.length > 40) return "ignored";
  const reported = typeof mistakes === "number" && isFinite(mistakes) ? Math.max(0, Math.min(50, Math.floor(mistakes))) : 0;

  const typed = toLowerForLang(raw.trim(), state.lang);
  const expected = state.words[player.index];

  // Yanlış kelime ilerletmez: oyuncu aynı kelimeyi doğru yazana kadar ilerleyemez (art arda boş gönderip bitirmeyi engeller).
  if (typed !== expected) {
    player.errors++;
    return "wrong";
  }

  player.errors += reported;
  player.progressAt = now;
  player.index++;
  player.correct++;
  player.correctChars += expected.length + 1;

  if (player.correct >= state.cfg.minWordsForWpmCheck && wpmOf(player.correctChars, now - state.raceStartAt) > state.cfg.maxWpm) {
    player.disqualified = true;
    return "disqualified";
  }
  if (player.index >= state.words.length) {
    player.finishedAt = now;
    return "finished";
  }
  return "correct";
}

function botWordDelay(state: RaceState, bot: Player): number {
  const word = state.words[Math.min(bot.index, state.words.length - 1)];
  const charsPerMs = (bot.botWpm * 5) / 60000;
  let delay = ((word.length + 1) / charsPerMs) * (0.75 + nextRandom(state) * 0.5);
  if (nextRandom(state) < state.cfg.botMistakeRate) delay *= 2; // yanlış yazıp tekrar denedi
  return delay;
}

// Botları, geçen zamana göre ilerletir. Bitiş zamanı kare hızına bağlı olmasın diye kendi planlı zamanlarını kullanır.
export function stepBots(state: RaceState, now: number): boolean {
  let changed = false;
  for (const bot of playerList(state)) {
    if (!bot.isBot || bot.finishedAt > 0) continue;
    while (bot.finishedAt === 0 && bot.botNextAt <= now) {
      const word = state.words[bot.index];
      bot.progressAt = bot.botNextAt;
      bot.index++;
      bot.correct++;
      bot.correctChars += word.length + 1;
      changed = true;
      if (bot.index >= state.words.length) bot.finishedAt = bot.botNextAt;
      else bot.botNextAt += botWordDelay(state, bot);
    }
  }
  return changed;
}

function humanStillPlaying(state: RaceState, p: Player, now: number): boolean {
  if (p.isBot || p.finishedAt > 0 || p.disqualified) return false;
  return p.connected || now - p.disconnectedAt <= state.cfg.disconnectGraceMs;
}

// Yarışın bitip bitmediğine bakar; bittiyse sonuçları hesaplar. Bittiyse true döner.
//
// Kural: İlk oyuncu (insan ya da bot) bitirince bir geri sayım (finishGraceMs) başlar. Bu sürede bitirenler bitiş
// süresiyle sıralanır; süre dolunca bitirmeyenler yazdıkları kelime sayısına göre sıralanır. Herkes bitirirse beklenmez.
// Yarışta yazmaya devam eden insan kalmadıysa (hepsi bitirdi/diskalifiye oldu) ve hiç bitiren yoksa geri sayım yine başlar.
// Odada hiç insan kalmadıysa (hepsi ayrıldı) yarış hemen biter.
export function updateRaceEnd(state: RaceState, now: number): boolean {
  if (state.phase !== "racing") return false;
  const players = playerList(state);

  const finishers = players.filter((p) => p.finishedAt > 0 && !p.disqualified).length;
  const humansRacing = players.filter((p) => humanStillPlaying(state, p, now)).length;
  const humansPresent = players.filter((p) => !p.isBot && (p.connected || now - p.disconnectedAt <= state.cfg.disconnectGraceMs)).length;

  if (state.graceDeadline === 0 && (finishers >= state.cfg.finishGraceAfter || (humansPresent > 0 && humansRacing === 0))) {
    state.graceDeadline = now + state.cfg.finishGraceMs;
  }

  const everyoneDone = players.every((p) => p.finishedAt > 0 || p.disqualified);
  const graceOver = state.graceDeadline > 0 && now >= state.graceDeadline;
  const tooLong = now - state.raceStartAt >= state.cfg.raceMaxMs;

  if (humansPresent === 0 || everyoneDone || graceOver || tooLong) {
    finishRace(state, now);
    return true;
  }
  return false;
}

function finishRace(state: RaceState, now: number): void {
  state.phase = "finished";
  state.endedAt = now;
  state.results = computeResults(state, now);
}

export function computeResults(state: RaceState, now: number): RaceResult[] {
  const players = playerList(state);
  const finished = players.filter((p) => p.finishedAt > 0 && !p.disqualified).sort((a, b) => a.finishedAt - b.finishedAt || a.slot - b.slot);
  const dnf = players.filter((p) => p.finishedAt === 0 && !p.disqualified).sort((a, b) => b.correct - a.correct || a.progressAt - b.progressAt || a.slot - b.slot);
  const cheaters = players.filter((p) => p.disqualified).sort((a, b) => a.slot - b.slot);

  const ordered = finished.concat(dnf, cheaters);
  return ordered.map((p, i) => {
    const status: RaceResult["status"] = p.disqualified ? "disqualified" : p.finishedAt > 0 ? "finished" : "dnf";
    const endMs = (p.finishedAt > 0 ? p.finishedAt : now) - state.raceStartAt;
    // Doğruluk tuş düzeyindedir: doğru tuşlar / (doğru tuşlar + reddedilen yanlış tuşlar ve yanlış kelimeler)
    const attempts = p.correctChars + p.errors;
    return {
      userId: p.id,
      name: p.name,
      isBot: p.isBot,
      rank: i + 1,
      status,
      finishMs: p.finishedAt > 0 ? Math.round(endMs) : 0,
      wpm: Math.round(wpmOf(p.correctChars, endMs) * 10) / 10,
      accuracy: attempts === 0 ? 0 : Math.round((p.correctChars / attempts) * 1000) / 1000,
      correctWords: p.correct,
      errors: p.errors,
    };
  });
}

// İstemcilere gönderilecek oyuncu özeti (kelime listesi hariç).
export function publicPlayers(state: RaceState): Array<{ id: string; name: string; isBot: boolean; slot: number; index: number; connected: boolean; finished: boolean; disqualified: boolean }> {
  return playerList(state).map((p) => ({
    id: p.id,
    name: p.name,
    isBot: p.isBot,
    slot: p.slot,
    index: p.index,
    connected: p.connected,
    finished: p.finishedAt > 0,
    disqualified: p.disqualified,
  }));
}
