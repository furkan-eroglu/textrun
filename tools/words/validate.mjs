// Kelime listelerini kontrol eder: harf kümesi, uzunluk, tekrar, yasak kelime, zorluk dilimi dağılımı.
// Kullanım: node tools/words/validate.mjs
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const wordsDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'content', 'words');

export function readConfig() {
  return JSON.parse(readFileSync(join(wordsDir, 'config.json'), 'utf8'));
}

// Dosyayı kelimelere ayırır: # ile başlayan satırlar yorumdur.
export function readWords(file) {
  return readFileSync(join(wordsDir, file), 'utf8')
    .split(/\r?\n/)
    .filter((line) => !line.trimStart().startsWith('#'))
    .flatMap((line) => line.split(/\s+/))
    .filter((w) => w.length > 0);
}

function readBlocklist() {
  const path = join(wordsDir, 'blocklist.txt');
  return existsSync(path) ? new Set(readWords('blocklist.txt')) : new Set();
}

function main() {
  const config = readConfig();
  const blocklist = readBlocklist();
  let errors = 0;

  for (const lang of config.languages) {
    const words = readWords(`${lang.code}.txt`);
    const allowed = new Set(lang.allowedChars);
    const seen = new Set();
    const tierCounts = Object.fromEntries(config.tiers.map((t) => [t.name, 0]));
    let special = 0;

    for (const word of words) {
      const problems = [];
      if (word !== word.toLowerCase()) problems.push('buyuk harf');
      for (const ch of word) if (!allowed.has(ch)) { problems.push(`izin verilmeyen harf '${ch}'`); break; }
      const length = [...word].length;
      if (length < config.minLength || length > config.maxLength) problems.push(`uzunluk ${length}`);
      if (seen.has(word)) problems.push('tekrar');
      if (blocklist.has(word)) problems.push('yasak listesinde');

      if (problems.length > 0) {
        console.log(`  [${lang.code}] ${word}: ${problems.join(', ')}`);
        errors++;
      }
      seen.add(word);

      const tier = config.tiers.find((t) => length >= t.min && length <= t.max);
      if (tier) tierCounts[tier.name]++;
      if (/[^a-z]/.test(word)) special++;
    }

    console.log(`${lang.code}: ${words.length} kelime, benzersiz ${seen.size}`);
    for (const t of config.tiers) {
      const n = tierCounts[t.name];
      const warn = n < 100 ? '  <-- az (en az 100 onerilir)' : '';
      console.log(`  ${t.name} (${t.min}-${t.max} harf): ${n}${warn}`);
    }
    console.log(`  ASCII disi harf iceren: ${special} (%${((special / words.length) * 100).toFixed(0)})`);
  }

  if (errors > 0) {
    console.log(`\n${errors} sorun bulundu.`);
    process.exit(1);
  }
  console.log('\nTum listeler gecerli.');
}

if (process.argv[1] === fileURLToPath(import.meta.url)) main();
