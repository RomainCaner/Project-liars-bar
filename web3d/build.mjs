// Compile src/main.js (+ three.js) en un seul script et l'inline dans
// ../assets/table3d.html : l'app fonctionne hors ligne, sans serveur.
import { build } from 'esbuild';
import { readFileSync, writeFileSync } from 'node:fs';

const result = await build({
  entryPoints: ['src/main.js'],
  bundle: true,
  minify: true,
  format: 'iife',
  target: ['chrome80'],
  write: false,
  legalComments: 'none',
  loader: { '.glb': 'binary' },
});
const js = result.outputFiles[0].text.replace(/<\/script/g, '<\\/script');
const html = readFileSync('src/index.html', 'utf8').replace('/*__SCRIPT__*/', () => js);
writeFileSync('../assets/table3d.html', html);
console.log(`assets/table3d.html : ${(html.length / 1024).toFixed(0)} Ko`);
