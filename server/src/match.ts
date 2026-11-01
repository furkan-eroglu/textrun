// Nakama yarış odası (authoritative match). Kurallar race.ts içindedir; burada yalnızca Nakama ile bağlantı vardır.
import { loadConfig } from "./config.ts";
import { OP, REJECT } from "./protocol.ts";
import {
  addHuman,
  beginRace,
  canJoin,
  createRaceState,
  humanCount,
  playerList,
  publicPlayers,
  removeHuman,
  requestStart,
  reserveSeat,
  shouldStartCountdown,
  startCountdown,
  stepBots,
  submitWord,
  updateRaceEnd,
} from "./race.ts";
import type { RaceState } from "./race.ts";
import { racePaysXp } from "./profile.ts";
import type { Reward } from "./profile.ts";
import { awardRace, isRegistered, readProfile } from "./profileStore.ts";

const TICK_RATE = 10;
const EMPTY_LOBBY_TIMEOUT_MS = 30000;

interface MatchInitParams {
  lang: string;
  kind: string;
  code?: string;
}

const PRIVATE_LOBBY_MAX_MS = 15 * 60 * 1000; // özel oda lobisi en fazla bu kadar açık kalır

function labelOf(state: RaceState): string {
  return JSON.stringify({ lang: state.lang, kind: state.kind, open: state.phase === "lobby" });
}

function send(dispatcher: nkruntime.MatchDispatcher, op: number, body: unknown, to: nkruntime.Presence[] | null = null): void {
  dispatcher.broadcastMessage(op, JSON.stringify(body), to, null, true);
}

function snapshotOf(state: RaceState, now: number) {
  return {
    phase: state.phase,
    lang: state.lang,
    serverNow: now,
    kind: state.kind,
    code: state.code,
    host: state.host,
    lobbyEndsAt: state.phase === "lobby" && state.lobbyDeadline > 0 ? state.lobbyDeadline : 0,
    startAt: state.raceStartAt,
    endsAt: state.graceDeadline, // 0 = bitiş geri sayımı başlamadı
    words: state.phase === "lobby" ? [] : state.words,
    players: publicPlayers(state),
    results: state.results,
  };
}

// Yarış bitince gerçek oyunculara XP verilir; [{ userId, gain, xp, level, leveledUp }] döner (botlar yok)
function awardRewards(nk: nkruntime.Nakama, logger: nkruntime.Logger, state: RaceState): Array<Reward & { userId: string }> {
  const rewards: Array<Reward & { userId: string }> = [];
  const humans = (state.results || []).filter((r) => !r.isBot);
  if (!racePaysXp(state.kind, humans.length)) return rewards;
  for (const result of humans) {
    // Misafirler XP kazanmaz (seviye, para, XP yalnızca kalıcı hesapta)
    let registered = false;
    try {
      registered = isRegistered(nk, result.userId);
    } catch (e) {
      logger.warn("Hesap okunamadi: %s", String(e));
    }
    if (!registered) continue;
    const reward = awardRace(nk, logger, result.userId, result.rank, result.status);
    if (reward) rewards.push({ userId: result.userId, ...reward });
  }
  return rewards;
}

export function matchInit(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, params: { [key: string]: string }) {
  const p = params as unknown as MatchInitParams;
  const seed = Math.floor(Math.random() * 4294967296);
  const state = createRaceState(p.lang, p.kind === "private" ? "private" : "public", loadConfig(ctx.env), Date.now(), seed, p.code || "");
  logger.info("Yaris odasi acildi: dil=%s tur=%s", state.lang, state.kind);
  return { state, tickRate: TICK_RATE, label: labelOf(state) };
}

export function matchJoinAttempt(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: RaceState,
  presence: nkruntime.Presence,
  metadata: { [key: string]: any }
) {
  // Dil kuralı: oyuncunun seçtiği dil, odanın diliyle aynı olmak zorundadır.
  if (!metadata || metadata.lang !== state.lang) {
    return { state, accept: false, rejectMessage: REJECT.LANGUAGE_MISMATCH };
  }
  // Özel oda: yalnızca doğru kodu bilen girebilir (oda numarası bilinse bile)
  if (state.kind === "private" && metadata.code !== state.code) return { state, accept: false, rejectMessage: REJECT.BAD_CODE };
  const outcome = canJoin(state, presence.userId, Date.now());
  if (outcome === "full") return { state, accept: false, rejectMessage: REJECT.ROOM_FULL };
  if (outcome === "closed") return { state, accept: false, rejectMessage: REJECT.ROOM_CLOSED };
  if (outcome === "joined") reserveSeat(state, presence.userId, Date.now());
  return { state, accept: true };
}

export function matchJoin(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: RaceState,
  presences: nkruntime.Presence[]
) {
  const now = Date.now();
  for (const presence of presences) {
    // Yarışta oyuncunun seçtiği ad görünür; ad seçilmemişse (eski hesap) Nakama kullanıcı adı kalır
    let name = presence.username;
    try {
      name = readProfile(nk, presence.userId).profile.name || presence.username;
    } catch (e) {
      logger.warn("Profil okunamadi: %s", String(e));
    }
    addHuman(state, presence.userId, name, now);
    send(dispatcher, OP.SNAPSHOT, snapshotOf(state, now), [presence]);
  }
  if (state.phase === "lobby") send(dispatcher, OP.ROSTER, snapshotOf(state, now));
  return { state };
}

export function matchLeave(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: RaceState,
  presences: nkruntime.Presence[]
) {
  const now = Date.now();
  for (const presence of presences) removeHuman(state, presence.userId, now);
  if (state.phase === "lobby") send(dispatcher, OP.ROSTER, snapshotOf(state, now));
  return { state };
}

export function matchLoop(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: RaceState,
  messages: nkruntime.MatchMessage[]
) {
  const now = Date.now();
  let progressChanged = false;

  for (const message of messages) {
    if (message.opCode === OP.START) {
      let bots = false;
      try {
        bots = JSON.parse(nk.binaryToString(message.data)).bots === true;
      } catch (e) {
        // boş ya da bozuk mesaj: botsuz başlat denir
      }
      requestStart(state, message.sender.userId, bots);
      continue;
    }
    if (message.opCode !== OP.SUBMIT) continue;

    let word = "";
    let mistakes = 0;
    try {
      const body = JSON.parse(nk.binaryToString(message.data));
      word = body.w;
      mistakes = body.e;
    } catch (e) {
      continue;
    }
    const outcome = submitWord(state, message.sender.userId, word, now, mistakes);
    if (outcome === "ignored") continue;

    const player = state.players[message.sender.userId];
    send(dispatcher, OP.ACK, { ok: outcome !== "wrong", kind: outcome, index: player.index }, [message.sender]);
    if (outcome !== "wrong") progressChanged = true;
  }

  switch (state.phase) {
    case "lobby":
      if (shouldStartCountdown(state, now)) {
        startCountdown(state, now);
        dispatcher.matchLabelUpdate(labelOf(state));
        send(dispatcher, OP.COUNTDOWN, snapshotOf(state, now));
        logger.info("Geri sayim basladi: dil=%s insan=%d", state.lang, humanCount(state));
      } else if (state.everHadHuman ? humanCount(state) === 0 : now - state.createdAt > EMPTY_LOBBY_TIMEOUT_MS) {
        return null; // lobi boşaldı, oda kapanır
      } else if (state.kind === "private" && now - state.createdAt > PRIVATE_LOBBY_MAX_MS) {
        return null; // özel oda çok uzun süre başlatılmadı
      }
      break;

    case "countdown":
      if (now >= state.raceStartAt) {
        beginRace(state);
        send(dispatcher, OP.GO, { serverNow: now, startAt: state.raceStartAt });
      }
      break;

    case "racing": {
      const graceBefore = state.graceDeadline;
      if (stepBots(state, now)) progressChanged = true;
      const ended = updateRaceEnd(state, now);

      // İlk oyuncu bitirdi: herkese yarışın biteceği an bildirilir (kırmızı geri sayım)
      if (graceBefore === 0 && state.graceDeadline > 0 && !ended) {
        send(dispatcher, OP.ENDING, { serverNow: now, endsAt: state.graceDeadline });
      }

      if (ended) {
        send(dispatcher, OP.PROGRESS, { t: now, players: publicPlayers(state) });
        send(dispatcher, OP.RESULTS, { serverNow: now, results: state.results, rewards: awardRewards(nk, logger, state) });
        logger.info("Yaris bitti: dil=%s", state.lang);
      } else if (progressChanged) {
        send(dispatcher, OP.PROGRESS, { t: now, players: publicPlayers(state) });
      }
      break;
    }

    case "finished":
      if (now - state.endedAt >= state.cfg.resultsLingerMs) return null;
      break;
  }

  return { state };
}

export function matchTerminate(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: RaceState,
  graceSeconds: number
) {
  return { state };
}

export function matchSignal(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: RaceState,
  data: string
) {
  return { state, data: "" };
}

export function playerCountOf(state: RaceState): number {
  return playerList(state).length;
}
