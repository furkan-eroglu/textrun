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
  reserveSeat,
  shouldStartCountdown,
  startCountdown,
  stepBots,
  submitWord,
  updateRaceEnd,
} from "./race.ts";
import type { RaceState } from "./race.ts";

const TICK_RATE = 10;
const EMPTY_LOBBY_TIMEOUT_MS = 30000;

interface MatchInitParams {
  lang: string;
  kind: string;
}

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
    lobbyEndsAt: state.phase === "lobby" && state.lobbyDeadline > 0 ? state.lobbyDeadline : 0,
    startAt: state.raceStartAt,
    words: state.phase === "lobby" ? [] : state.words,
    players: publicPlayers(state),
    results: state.results,
  };
}

export function matchInit(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, params: { [key: string]: string }) {
  const p = params as unknown as MatchInitParams;
  const seed = Math.floor(Math.random() * 4294967296);
  const state = createRaceState(p.lang, p.kind === "private" ? "private" : "public", loadConfig(ctx.env), Date.now(), seed);
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
    addHuman(state, presence.userId, presence.username, now);
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
      }
      break;

    case "countdown":
      if (now >= state.raceStartAt) {
        beginRace(state);
        send(dispatcher, OP.GO, { serverNow: now, startAt: state.raceStartAt });
      }
      break;

    case "racing":
      if (stepBots(state, now)) progressChanged = true;
      if (updateRaceEnd(state, now)) {
        send(dispatcher, OP.PROGRESS, { t: now, players: publicPlayers(state) });
        send(dispatcher, OP.RESULTS, { serverNow: now, results: state.results });
        logger.info("Yaris bitti: dil=%s", state.lang);
      } else if (progressChanged) {
        send(dispatcher, OP.PROGRESS, { t: now, players: publicPlayers(state) });
      }
      break;

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
