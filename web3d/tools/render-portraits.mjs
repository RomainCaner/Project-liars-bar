// Génère ../assets/portraits/<n>.png (avatars du salon) à partir des modèles 3D.
//   node tools/render-portraits.mjs   (nécessite Playwright + Chromium)
import { build } from 'esbuild';
import { mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const res = await build({
  entryPoints: ['src/portrait.js'], bundle: true, format: 'iife', write: false,
  loader: { '.glb': 'binary' },
});
mkdirSync('dist', { recursive: true });
writeFileSync('dist/portrait.html',
  `<!doctype html><html><body style="margin:0;background:transparent"><script>${res.outputFiles[0].text.replace(/<\/script/g, '<\\/script')}</script></body></html>`);

const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage();
page.on('pageerror', (e) => console.error(e));
await page.goto(`file://${process.cwd()}/dist/portrait.html`);
mkdirSync('../assets/portraits', { recursive: true });
const n = await page.evaluate(() => window.portraitCount);
for (let i = 0; i < n; i++) {
  const url = await page.evaluate((i) => window.renderPortrait(i), i);
  writeFileSync(`../assets/portraits/${i}.png`, Buffer.from(url.split(',')[1], 'base64'));
  console.log(`assets/portraits/${i}.png`);
}
await browser.close();
