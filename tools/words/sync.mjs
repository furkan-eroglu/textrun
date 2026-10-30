// content/words içindeki listeleri Unity projesine (Resources/Words) kopyalar.
// Kullanım: node tools/words/sync.mjs
import { copyFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readConfig, wordsDir } from './validate.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const target = join(root, 'client', 'Assets', 'Resources', 'Words');
mkdirSync(target, { recursive: true });

const files = ['config.json', ...readConfig().languages.map((l) => `${l.file}.txt`)];
for (const file of files) {
  copyFileSync(join(wordsDir, file), join(target, file));
  console.log(`kopyalandi: ${file}`);
}
