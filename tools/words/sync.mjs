// content/words içindeki listeleri Unity projesine (Resources/Words) ve sunucuya (src/generated/words.ts) kopyalar.
// Kullanım: node tools/words/sync.mjs
import { copyFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readConfig, readWords, wordsDir } from './validate.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const config = readConfig();

// Unity
const unityDir = join(root, 'client', 'Assets', 'Resources', 'Words');
mkdirSync(unityDir, { recursive: true });
for (const file of ['config.json', ...config.languages.map((l) => `${l.file}.txt`)]) {
  copyFileSync(join(wordsDir, file), join(unityDir, file));
  console.log(`unity: ${file}`);
}

// Sunucu: kelimeler koda gömülür, çünkü sunucu dosya okuyamaz.
const serverDir = join(root, 'server', 'src', 'generated');
mkdirSync(serverDir, { recursive: true });
const words = Object.fromEntries(config.languages.map((l) => [l.code, [...new Set(readWords(`${l.file}.txt`))]]));
const serverConfig = { minLength: config.minLength, maxLength: config.maxLength, recentWindow: config.recentWindow, tiers: config.tiers };
const source =
  '// OTOMATIK URETILDI: elle degistirmeyin. Kaynak: content/words, arac: node tools/words/sync.mjs\n' +
  `export const WORD_CONFIG = ${JSON.stringify(serverConfig)};\n` +
  `export const WORDS: { [lang: string]: string[] } = ${JSON.stringify(words)};\n`;
writeFileSync(join(serverDir, 'words.ts'), source);
console.log('sunucu: src/generated/words.ts');
