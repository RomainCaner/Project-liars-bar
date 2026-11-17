// Exporte les faces et le dos des cartes en PNG (mêmes dessins que la scène 3D).
//   NODE_PATH=$(npm root -g) node tools/render-cards.mjs <dossier de sortie>
import { build } from 'esbuild';
import { mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';

const out = process.argv[2] || '../unity/Assets/Resources/Cards';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const res = await build({
  stdin: {
    contents: `import { cardFaceTexture, cardBackTexture } from './src/textures.js';
      window.cards = () => [0,1,2,3].map((r) => cardFaceTexture(r).image.toDataURL('image/png'))
        .concat([cardBackTexture().image.toDataURL('image/png')]);`,
    resolveDir: process.cwd(),
  },
  bundle: true, format: 'iife', write: false,
});
mkdirSync('dist', { recursive: true });
writeFileSync('dist/cards.html', `<!doctype html><html><body><script>${res.outputFiles[0].text}</script></body></html>`);
const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto(`file://${process.cwd()}/dist/cards.html`);
const urls = await page.evaluate(() => window.cards());
mkdirSync(out, { recursive: true });
['king', 'queen', 'ace', 'joker', 'back'].forEach((n, i) =>
  writeFileSync(`${out}/${n}.png`, Buffer.from(urls[i].split(',')[1], 'base64')));
console.log('cartes exportées dans', out);
await browser.close();
