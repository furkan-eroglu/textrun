// src/main.ts dosyasını Nakama'nın yükleyebileceği tek bir build/index.js dosyasına çevirir.
// Nakama, InitModule ve kaydedilen RPC/hook fonksiyonlarını kodu statik okuyarak bulur;
// bu yüzden hepsi dosyanın en üst seviyesinde, sarmalayıcısız fonksiyonlar olarak kalmalıdır.
// esbuild "esm" çıktısı bunu sağlar, sadece sondaki "export" satırı (Nakama anlamaz) silinir.
import { build } from 'esbuild';
import { mkdirSync, writeFileSync } from 'node:fs';

const result = await build({
  entryPoints: ['src/main.ts'],
  bundle: true,
  format: 'esm',
  target: 'es2015',
  write: false,
  outfile: 'build/index.js',
});

const code = result.outputFiles[0].text.replace(/^export\s*\{[^}]*\};?\s*$/m, '');
mkdirSync('build', { recursive: true });
writeFileSync('build/index.js', code);
console.log('build/index.js hazir');
