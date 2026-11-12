// Personnages KayKit (CC0, Kay Lousberg — www.kaylousberg.com) animés.
// Le bas du corps reste assis ; le haut du corps enchaîne des gestes.
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { clone as cloneSkinned } from 'three/examples/jsm/utils/SkeletonUtils.js';

import knightGlb from '../models/knight.glb';
import barbarianGlb from '../models/barbarian.glb';
import mageGlb from '../models/mage.glb';
import rogueGlb from '../models/rogue.glb';
import rogueHoodedGlb from '../models/rogue_hooded.glb';
import animationsGlb from '../models/animations.glb';

export const SKINS = ['knight', 'barbarian', 'mage', 'rogue', 'rogue_hooded'];
const FILES = {
  knight: knightGlb, barbarian: barbarianGlb, mage: mageGlb, rogue: rogueGlb, rogue_hooded: rogueHoodedGlb,
};

// Os du haut du corps (noms nettoyés par three.js : « upperarm.l » → « upperarml »).
const UPPER = /^(spine|chest|head|upperarm|lowerarm|wrist|hand|elbowIK|handIK)/;

let assets = null;

export function assetsReady() {
  return assets;
}

export async function loadCharacters() {
  const loader = new GLTFLoader();
  loader.setMeshoptDecoder(MeshoptDecoder);
  const parse = (bytes) => loader.parseAsync(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
  const [anim, ...models] = await Promise.all([parse(animationsGlb), ...SKINS.map((s) => parse(FILES[s]))]);

  const clips = {};
  for (const clip of anim.animations) {
    const upper = clip.clone();
    upper.tracks = upper.tracks.filter((t) => UPPER.test(t.name.split('.')[0]));
    const lower = clip.clone();
    lower.tracks = lower.tracks.filter((t) => !UPPER.test(t.name.split('.')[0]));
    clips[clip.name] = { full: clip, upper, lower };
  }
  const templates = {};
  SKINS.forEach((s, i) => {
    const scene = models[i].scene;
    scene.traverse((o) => {
      if (o.isMesh) {
        o.castShadow = true;
        o.frustumCulled = false;
        if (/Mug/.test(o.name)) o.visible = false;
      }
    });
    templates[s] = scene;
  });
  assets = { clips, templates };
  return assets;
}

/** Un personnage assis, avec ses actions d'animation. */
export class AnimatedCharacter {
  constructor(skin) {
    this.root = new THREE.Group();
    this.model = cloneSkinned(assets.templates[skin]);
    this.root.add(this.model);
    this.mixer = new THREE.AnimationMixer(this.model);
    this.bones = {};
    this.model.traverse((o) => {
      if (o.isBone) this.bones[o.name] = o;
    });
    const c = assets.clips;
    this.sitLower = this.mixer.clipAction(c.Sit_Chair_Idle.lower).play();
    this.sitUpper = this.mixer.clipAction(c.Sit_Chair_Idle.upper).play();
    // Décale les respirations pour que tout le monde ne bouge pas en rythme.
    const offset = Math.random() * c.Sit_Chair_Idle.full.duration;
    this.sitLower.time = this.sitUpper.time = offset;
    this.current = this.sitUpper;
    this.holding = false;
    this.dead = false;
    this.headYaw = 0;
    this.slump = 0;
    this.cardHold = 0;
    this.slumpSide = Math.random() < 0.5 ? -1 : 1;
    this.mixer.addEventListener('finished', (e) => {
      if (e.action === this.current && !this.holding) this.toIdle(0.35);
    });
  }

  get head() { return this.bones.head; }
  get handR() { return this.bones.handslotr; }
  get handL() { return this.bones.handslotl; }

  /** Geste du haut du corps. hold : reste sur la dernière image. */
  gesture(name, { hold = false, fade = 0.2, speed = 1 } = {}) {
    if (this.dead) return;
    const clip = assets.clips[name];
    if (!clip) return;
    const action = this.mixer.clipAction(clip.upper);
    action.reset();
    action.setLoop(hold ? THREE.LoopOnce : THREE.LoopOnce, 1);
    action.clampWhenFinished = true;
    action.timeScale = speed;
    action.enabled = true;
    action.setEffectiveWeight(1);
    action.play();
    if (this.current && this.current !== action) this.current.crossFadeTo(action, fade, false);
    this.current = action;
    this.holding = hold;
  }

  /** Geste en boucle (ex. célébration du vainqueur). */
  loop(name, fade = 0.3) {
    if (this.dead) return;
    const action = this.mixer.clipAction(assets.clips[name].upper);
    action.reset().setLoop(THREE.LoopRepeat, Infinity).play();
    if (this.current !== action) this.current.crossFadeTo(action, fade, false);
    this.current = action;
    this.holding = true;
  }

  toIdle(fade = 0.4) {
    this.holding = false;
    if (this.current === this.sitUpper) return;
    this.sitUpper.enabled = true;
    this.sitUpper.setEffectiveWeight(1);
    this.sitUpper.play();
    this.current.crossFadeTo(this.sitUpper, fade, false);
    this.current = this.sitUpper;
  }

  die() {
    this.dead = true;
    this.holding = false;
  }

  /**
   * À appeler à chaque image. lookYaw : rotation de tête souhaitée (radians).
   * gunPose : 0..1, amène la main droite à la tempe (roulette).
   */
  update(dt, { lookYaw = 0, gunPose = 0, cards = false } = {}) {
    this.mixer.update(dt);
    const b = this.bones;
    // Regard vers le joueur actif (s'ajoute à l'animation).
    this.headYaw += (lookYaw - this.headYaw) * Math.min(1, dt * 4);
    if (b.head && !this.dead) {
      b.head.rotateY(this.headYaw * 0.7);
      if (b.chest) b.chest.rotateY(this.headYaw * 0.25);
    }
    if (gunPose > 0 && b.upperarmr && b.lowerarmr && b.head) {
      // Main droite contre la tempe : petite cinématique inverse (CCD).
      this.model.updateMatrixWorld(true);
      const head = b.head.getWorldPosition(_v1);
      const right = _v2.set(-1, 0, 0).transformDirection(this.root.matrixWorld);
      const target = _v3.copy(head).addScaledVector(right, HEAD_SIDE).add(_up.set(0, HEAD_UP, 0));
      solveArm(b.upperarmr, b.lowerarmr, b.handslotr || b.handr, target, gunPose);
    }
    // Main gauche levée devant soi, cartes en éventail (quand il n'y a pas de geste).
    const wantCards = cards && !this.dead && this.current === this.sitUpper ? 1 : 0;
    this.cardHold += (wantCards - this.cardHold) * Math.min(1, dt * 5);
    if (this.cardHold > 0.01 && b.upperarml && b.lowerarml && b.chest) {
      this.model.updateMatrixWorld(true);
      const chest = b.chest.getWorldPosition(_v1);
      const fwd = _v2.set(0, 0, 1).transformDirection(this.root.matrixWorld);
      const left = _up.set(1, 0, 0).transformDirection(this.root.matrixWorld);
      const target = _v3.copy(chest).addScaledVector(fwd, 0.32).addScaledVector(left, 0.26).add(_a.set(0, -0.04, 0));
      solveArm(b.upperarml, b.lowerarml, b.handslotl || b.handl, target, this.cardHold);
    }
    if (this.dead) {
      this.slump = Math.min(1, this.slump + dt * 2.5);
      const s = 1 - (1 - this.slump) ** 3;
      if (b.spine) b.spine.rotateX(0.45 * s);
      if (b.chest) b.chest.rotateX(0.25 * s);
      if (b.head) {
        b.head.rotateX(0.3 * s);
        b.head.rotateZ(this.slumpSide * 0.5 * s);
      }
    }
  }
}

// Tête « chibi » volumineuse : la tempe est loin de l'os de la nuque.
export const HEAD_UP = 0.22;
const HEAD_SIDE = 0.34;

const _v1 = new THREE.Vector3();
const _v2 = new THREE.Vector3();
const _v3 = new THREE.Vector3();
const _up = new THREE.Vector3();
const _a = new THREE.Vector3();
const _b = new THREE.Vector3();
const _q = new THREE.Quaternion();
const _qw = new THREE.Quaternion();
const _qp = new THREE.Quaternion();

/**
 * CCD à deux articulations : oriente coude puis épaule pour amener
 * l'effecteur (la main) sur la cible, avec un poids 0..1.
 */
function solveArm(upper, lower, effector, target, weight) {
  const startU = upper.quaternion.clone();
  const startL = lower.quaternion.clone();
  for (let iter = 0; iter < 6; iter++) {
    for (const bone of [lower, upper]) {
      bone.updateMatrixWorld(true);
      const pos = bone.getWorldPosition(_a);
      const eff = effector.getWorldPosition(_b);
      const toEff = eff.sub(pos).normalize();
      const toTarget = _v1.copy(target).sub(pos).normalize();
      _q.setFromUnitVectors(toEff, toTarget);
      bone.getWorldQuaternion(_qw);
      bone.parent.getWorldQuaternion(_qp);
      bone.quaternion.copy(_qp.invert().multiply(_q.multiply(_qw)));
      bone.updateMatrixWorld(true);
    }
  }
  upper.quaternion.copy(startU.slerp(upper.quaternion, weight));
  lower.quaternion.copy(startL.slerp(lower.quaternion, weight));
  upper.updateMatrixWorld(true);
}
