// Prépare les personnages KayKit (CC0, Kay Lousberg) pour le jeu :
//   node tools/prepare-models.mjs
// - télécharge le pack officiel (git clone) dans .cache/
// - retire armes et boucliers, garde corps, chapeaux, capes et la chope
// - sort les animations utiles dans un seul fichier partagé
// - compresse le tout (meshopt) dans models/
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, copyFileSync, statSync } from 'node:fs';
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dedup, meshopt, prune, resample, weld } from '@gltf-transform/functions';
import { MeshoptDecoder, MeshoptEncoder } from 'meshoptimizer';

const REPO = 'https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0';
const CACHE = '.cache/kaykit-adventurers';
const SRC = `${CACHE}/addons/kaykit_character_pack_adventures/Characters/gltf`;
const CHARACTERS = ['Knight', 'Barbarian', 'Mage', 'Rogue', 'Rogue_Hooded'];

// Objets tenus en main à supprimer (on garde la chope « Mug »).
const PROPS = /(Sword|Shield|Axe|Spellbook|Wand|Staff|Knife|Crossbow|Throwable)/;

// Animations conservées (toutes partagent le même squelette « Rig »).
export const CLIPS = [
  'Sit_Chair_Idle', 'Sit_Chair_Pose', 'Throw', 'Interact', 'Use_Item', 'PickUp',
  'Unarmed_Melee_Attack_Punch_A', '1H_Melee_Attack_Stab', '1H_Ranged_Aiming',
  'Hit_A', 'Hit_B', 'Cheer', 'Block',
];

if (!existsSync(SRC)) {
  mkdirSync('.cache', { recursive: true });
  execFileSync('git', ['clone', '--depth', '1', REPO, CACHE], {
    stdio: 'inherit',
    env: { ...process.env, GIT_LFS_SKIP_SMUDGE: '1' },
  });
}

await MeshoptEncoder.ready;
await MeshoptDecoder.ready;
const io = new NodeIO()
  .registerExtensions(ALL_EXTENSIONS)
  .registerDependencies({ 'meshopt.encoder': MeshoptEncoder, 'meshopt.decoder': MeshoptDecoder });

function disposeAnimation(a) {
  for (const c of a.listChannels()) c.dispose();
  // Les accesseurs peuvent être partagés entre clips : prune() retire ensuite
  // ceux qui ne servent plus.
  for (const s of a.listSamplers()) s.dispose();
  a.dispose();
}

async function finish(doc, out) {
  await doc.transform(prune({ keepLeaves: true }), dedup(), weld(), meshopt({ encoder: MeshoptEncoder, level: 'medium' }));
  await io.write(out, doc);
  console.log(`${out} : ${(statSync(out).size / 1024).toFixed(0)} Ko`);
}

mkdirSync('models', { recursive: true });

for (const name of CHARACTERS) {
  const doc = await io.read(`${SRC}/${name}.glb`);
  const root = doc.getRoot();
  for (const a of root.listAnimations()) disposeAnimation(a);
  for (const n of root.listNodes()) {
    if (n.getMesh() && PROPS.test(n.getName())) n.dispose();
  }
  await finish(doc, `models/${name.toLowerCase()}.glb`);
}

// Fichier d'animations : squelette + clips sélectionnés, sans maillage.
{
  const doc = await io.read(`${SRC}/Knight.glb`);
  const root = doc.getRoot();
  for (const a of root.listAnimations()) {
    if (!CLIPS.includes(a.getName())) disposeAnimation(a);
  }
  for (const n of root.listNodes()) {
    if (n.getMesh()) n.dispose();
  }
  for (const s of root.listSkins()) s.dispose();
  await doc.transform(resample());
  await finish(doc, 'models/animations.glb');
}

copyFileSync(`${CACHE}/LICENSE.txt`, 'models/LICENSE-KayKit.txt');
