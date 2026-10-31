// Yarışın ayarlanabilir değerleri. Canlı sunucuda sunucu ortam değişkenleriyle (TEXTRUN_...) değiştirilebilir.
export interface RaceConfig {
  maxPlayers: number;
  lobbyWaitMs: number; // ilk oyuncudan sonra botla doldurmaya kadar beklenen süre
  countdownMs: number;
  wordCount: number;
  maxWpm: number; // bunun üstü insan için şüpheli sayılır
  minWordsForWpmCheck: number;
  finishGraceAfter: number; // bu kadar oyuncu (insan ya da bot) bitirince geri sayım başlar
  finishGraceMs: number; // geri sayım süresi: bitirmeyenler bu sürede yetişebilir, süre dolunca ilerlemeye göre sıralanır
  raceMaxMs: number;
  disconnectGraceMs: number;
  resultsLingerMs: number;
  botMinWpm: number;
  botMaxWpm: number;
  botMistakeRate: number;
}

export const DEFAULT_CONFIG: RaceConfig = {
  maxPlayers: 5,
  lobbyWaitMs: 15000,
  countdownMs: 6000,
  wordCount: 40,
  maxWpm: 250,
  minWordsForWpmCheck: 5,
  finishGraceAfter: 1,
  finishGraceMs: 10000,
  raceMaxMs: 180000,
  disconnectGraceMs: 30000,
  resultsLingerMs: 15000,
  botMinWpm: 30,
  botMaxWpm: 65,
  botMistakeRate: 0.08,
};

const ENV_FIELDS: Array<[string, keyof RaceConfig]> = [
  ["TEXTRUN_LOBBY_WAIT_MS", "lobbyWaitMs"],
  ["TEXTRUN_COUNTDOWN_MS", "countdownMs"],
  ["TEXTRUN_WORD_COUNT", "wordCount"],
  ["TEXTRUN_MAX_WPM", "maxWpm"],
  ["TEXTRUN_FINISH_GRACE_MS", "finishGraceMs"],
  ["TEXTRUN_DISCONNECT_GRACE_MS", "disconnectGraceMs"],
  ["TEXTRUN_RESULTS_LINGER_MS", "resultsLingerMs"],
  ["TEXTRUN_BOT_MIN_WPM", "botMinWpm"],
  ["TEXTRUN_BOT_MAX_WPM", "botMaxWpm"],
];

export function loadConfig(env?: { [key: string]: string }): RaceConfig {
  const config: RaceConfig = { ...DEFAULT_CONFIG };
  if (!env) return config;
  for (const [key, field] of ENV_FIELDS) {
    const value = Number(env[key]);
    if (env[key] !== undefined && isFinite(value) && value > 0) config[field] = value;
  }
  return config;
}
