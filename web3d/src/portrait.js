// Page outil : rend le buste de chaque personnage (tools/render-portraits.mjs).
import * as THREE from 'three';
import { AnimatedCharacter, SKINS, loadCharacters } from './characters.js';

const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
renderer.setSize(256, 256, false);
renderer.setPixelRatio(1);
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.setClearColor(0x000000, 0);
document.body.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.add(new THREE.HemisphereLight(0xfff0dd, 0x332211, 1.6));
const key = new THREE.DirectionalLight(0xffe2c0, 2.2);
key.position.set(1.5, 2.5, 3);
scene.add(key);
const camera = new THREE.PerspectiveCamera(30, 1, 0.01, 20);

window.renderPortrait = async (i) => {
  await loadCharacters();
  const c = new AnimatedCharacter(SKINS[i]);
  scene.add(c.root);
  c.update(0.5);
  c.model.updateMatrixWorld(true);
  const head = c.head.getWorldPosition(new THREE.Vector3());
  const target = head.clone().add(new THREE.Vector3(0, 0.28, 0));
  camera.position.set(target.x + 0.35, target.y + 0.2, target.z + 3.4);
  camera.lookAt(target);
  renderer.render(scene, camera);
  const url = renderer.domElement.toDataURL('image/png');
  scene.remove(c.root);
  return url;
};
window.portraitCount = SKINS.length;
