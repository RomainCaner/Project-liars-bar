// Scène 3D de la table. Piloté par l'app Flutter :
//   Flutter → JS : window.table3d.update(état)
//   JS → Flutter : Bridge.postMessage(JSON) — {t:'ready'} · {t:'tap', id}
import * as THREE from 'three';
import {
  CARD_H, CARD_W, TABLE_L, TABLE_Y, buildLamp, buildRoom, buildTable,
  createCard, createCharacter, createRevolver, setCardRank,
} from './models.js';
import { createLabelCanvas, drawLabel } from './textures.js';

const PHASE = { lobby: 0, playing: 1, reveal: 2, roulette: 3, gameOver: 4 };

// ---------------------------------------------------------------- rendu

const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.15;
document.body.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0d0806);
scene.fog = new THREE.FogExp2(0x0d0806, 0.075);
scene.add(new THREE.HemisphereLight(0xffd6a8, 0x2a1a10, 0.9));
// Lumière d'appoint depuis la caméra pour lire les visages.
const fill = new THREE.DirectionalLight(0xffc89a, 0.7);
fill.position.set(0, 3, 5);
scene.add(fill);

const camera = new THREE.PerspectiveCamera(74, 1, 0.01, 40);
scene.add(camera);
const CAM_TARGET = new THREE.Vector3(0, 0.55, -0.55);
const camBase = new THREE.Vector3(0, 2.3, 3.6);
const camShake = { amount: 0, roll: 0, drop: 0 };

buildRoom(scene);
buildTable(scene);
const lamp = buildLamp(scene);

// ------------------------------------------------------------ disposition

const PILE = new THREE.Vector3(0, TABLE_Y + 0.03, -0.15);
const GUN_REST = new THREE.Vector3(0.3, TABLE_Y + 0.055, 0.6);
const ME_ORIGIN = new THREE.Vector3(0, TABLE_Y + 0.3, TABLE_L / 2 + 0.3);

// Places autour de la table (sans compter la mienne, au bout côté caméra).
const SLOTS = {
  leftNear: { pos: [-0.95, 0.45], face: [0, 0.45] },
  leftFar: { pos: [-0.95, -0.7], face: [0, -0.7] },
  far: { pos: [0, -2.05], face: [0, 0] },
  rightFar: { pos: [0.95, -0.7], face: [0, -0.7] },
  rightNear: { pos: [0.95, 0.45], face: [0, 0.45] },
};
const SLOT_ORDER = {
  1: ['far'],
  2: ['leftFar', 'rightFar'],
  3: ['leftFar', 'far', 'rightFar'],
  4: ['leftNear', 'leftFar', 'rightFar', 'rightNear'],
  5: ['leftNear', 'leftFar', 'far', 'rightFar', 'rightNear'],
};

// ------------------------------------------------------------------ tweens

const tweens = [];
const easeInOut = (t) => (t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2);
const easeOut = (t) => 1 - (1 - t) ** 3;

function tween(dur, update, { delay = 0, ease = easeInOut } = {}) {
  return new Promise((resolve) => {
    tweens.push({ start: performance.now() + delay * 1000, dur: dur * 1000, update, ease, resolve });
  });
}

function runTweens(now) {
  for (let i = tweens.length - 1; i >= 0; i--) {
    const tw = tweens[i];
    if (now < tw.start) continue;
    const p = Math.min(1, (now - tw.start) / Math.max(1, tw.dur));
    tw.update(tw.ease(p));
    if (p >= 1) {
      tweens.splice(i, 1);
      tw.resolve();
    }
  }
}

/** Déplace un objet vers une position / orientation cible. */
function moveTo(obj, pos, quat, dur, opts) {
  const p0 = obj.position.clone();
  const q0 = obj.quaternion.clone();
  return tween(dur, (t) => {
    obj.position.lerpVectors(p0, pos, t);
    if (quat) obj.quaternion.slerpQuaternions(q0, quat, t);
  }, opts);
}

const quatFromEuler = (x, y, z) => new THREE.Quaternion().setFromEuler(new THREE.Euler(x, y, z));
const FACE_DOWN = (yaw) => quatFromEuler(Math.PI / 2, 0, yaw);

// ------------------------------------------------------------------ joueurs

const seats = new Map(); // id → seat
let seatKey = '';

function clearSeats() {
  for (const s of seats.values()) {
    scene.remove(s.char, s.label.sprite, s.stack, s.ring);
  }
  seats.clear();
}

function buildSeats(state) {
  clearSeats();
  const ids = state.players.map((p) => p.id);
  const meIdx = Math.max(0, ids.indexOf(state.me));
  const others = [...ids.slice(meIdx + 1), ...ids.slice(0, meIdx)].filter((id) => id !== state.me);
  const order = SLOT_ORDER[Math.min(5, others.length)] || [];
  others.forEach((id, i) => {
    const slot = SLOTS[order[i]];
    if (!slot) return;
    const char = createCharacter(id);
    char.position.set(slot.pos[0], 0, slot.pos[1]);
    char.lookAt(slot.face[0], 0, slot.face[1]);
    scene.add(char);

    const label = createLabelCanvas();
    // Taille constante à l'écran, quelle que soit la distance.
    label.sprite = new THREE.Sprite(new THREE.SpriteMaterial({
      map: label.texture, depthTest: false, transparent: true, sizeAttenuation: false,
    }));
    label.sprite.scale.set(0.17, 0.085, 1);
    const near = slot.pos[1] > 0;
    label.sprite.position.set(slot.pos[0] * (near ? 0.75 : 0.85), near ? 1.75 : 1.95, slot.pos[1]);
    label.sprite.renderOrder = 10;
    scene.add(label.sprite);

    // Cartes en main posées devant le joueur, face cachée.
    const toward = new THREE.Vector3(slot.face[0] - slot.pos[0], 0, slot.face[1] - slot.pos[1]).normalize();
    const stackPos = new THREE.Vector3(slot.pos[0], TABLE_Y + 0.03, slot.pos[1]).addScaledVector(toward, slot.pos[0] === 0 ? 0.62 : 0.42);
    const stack = new THREE.Group();
    stack.position.copy(stackPos);
    stack.rotation.y = Math.atan2(toward.x, toward.z);
    scene.add(stack);

    const ring = new THREE.Mesh(
      new THREE.RingGeometry(0.16, 0.2, 32),
      new THREE.MeshBasicMaterial({ color: 0xd9ae52, transparent: true, opacity: 0, side: THREE.DoubleSide, toneMapped: false }),
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.copy(stackPos).setY(TABLE_Y + 0.026);
    scene.add(ring);

    seats.set(id, { id, char, label, stack, ring, toward, lastLabel: '', slumped: false });
  });
  seatKey = ids.join('|') + '#' + state.me;
}

function setStackCount(seat, n) {
  const stack = seat.stack;
  while (stack.children.length > n) stack.remove(stack.children[stack.children.length - 1]);
  while (stack.children.length < n) {
    const c = createCard(null);
    const i = stack.children.length;
    c.scale.setScalar(0.8);
    c.position.set((i - 2) * 0.035, 0.002 * i, 0);
    c.quaternion.copy(quatFromEuler(Math.PI / 2, 0, (i - 2) * 0.12));
    stack.add(c);
  }
}

function headWorld(seat) {
  return seat.char.userData.head.getWorldPosition(new THREE.Vector3());
}

function slump(seat, animate) {
  if (seat.slumped) return;
  seat.slumped = true;
  const { torso, head, coatMat, skinMat } = seat.char.userData;
  const side = Math.random() < 0.5 ? -1 : 1;
  const apply = (t) => {
    torso.rotation.x = 0.95 * t;
    torso.rotation.z = side * 0.25 * t;
    head.rotation.x = 0.5 * t;
    head.rotation.z = side * 0.5 * t;
  };
  coatMat.color.lerp(new THREE.Color(0x2a2a2a), 0.55);
  skinMat.color.lerp(new THREE.Color(0x8a8a8a), 0.45);
  if (animate) tween(0.6, apply, { ease: (t) => 1 - (1 - t) ** 4 });
  else apply(1);
}

// ------------------------------------------------------------- tas central

let pileCards = [];

function addPileCard(card) {
  const i = pileCards.length;
  const yaw = (Math.sin(i * 12.9898) * 43758.5453 % 1) * 1.2;
  const offset = new THREE.Vector3(Math.cos(i * 2.4) * 0.07, 0.003 * i, Math.sin(i * 2.4) * 0.07);
  pileCards.push(card);
  return { pos: PILE.clone().add(offset), quat: FACE_DOWN(yaw) };
}

function clearPile(animate) {
  const old = pileCards;
  pileCards = [];
  for (const c of old) {
    if (!animate) {
      scene.remove(c);
      continue;
    }
    const p0 = c.position.clone();
    tween(0.4, (t) => {
      c.position.lerpVectors(p0, PILE.clone().setY(TABLE_Y - 0.1), t);
      c.scale.setScalar(1 - t * 0.9);
    }).then(() => scene.remove(c));
  }
}

function flyToPile(fromPos, count, delayBase = 0) {
  for (let k = 0; k < count; k++) {
    const card = createCard(null);
    card.position.copy(fromPos);
    card.quaternion.copy(FACE_DOWN(0));
    scene.add(card);
    const { pos, quat } = addPileCard(card);
    const p0 = fromPos.clone();
    tween(0.55, (t) => {
      card.position.lerpVectors(p0, pos, t);
      card.position.y += Math.sin(t * Math.PI) * 0.25;
      card.quaternion.slerpQuaternions(FACE_DOWN(0), quat, t);
    }, { delay: delayBase + k * 0.12, ease: easeOut });
  }
}

// ------------------------------------------------------------------- revolver

const gun = createRevolver();
const GUN_REST_Q = quatFromEuler(0, 0.9, Math.PI / 2); // couché sur le flanc
gun.position.copy(GUN_REST);
gun.quaternion.copy(GUN_REST_Q);
scene.add(gun);
let gunHolder = null; // id du tireur

function raiseGun(seat) {
  const head = headWorld(seat);
  const right = new THREE.Vector3(-seat.toward.z, 0, seat.toward.x); // côté du joueur
  const pos = head.clone().addScaledVector(right, 0.24).add(new THREE.Vector3(0, 0.02, 0));
  const m = new THREE.Matrix4().lookAt(head, pos, new THREE.Vector3(0, 1, 0));
  const quat = new THREE.Quaternion().setFromRotationMatrix(m);
  return moveTo(gun, pos, quat, 1.0);
}

function raiseGunToMe() {
  // Le revolver se place devant la caméra, canon vers la tempe.
  camera.updateMatrixWorld();
  const pos = camera.localToWorld(new THREE.Vector3(0.17, -0.02, -0.6));
  const temple = camera.localToWorld(new THREE.Vector3(-0.15, 0.05, 0.1));
  const m = new THREE.Matrix4().lookAt(temple, pos, new THREE.Vector3(0, 1, 0));
  scaleGun(0.75);
  return moveTo(gun, pos, new THREE.Quaternion().setFromRotationMatrix(m), 1.0);
}

function scaleGun(to) {
  const s0 = gun.scale.x;
  tween(0.8, (t) => gun.scale.setScalar(s0 + (to - s0) * t));
}

function lowerGun() {
  gunHolder = null;
  scaleGun(1.6);
  return moveTo(gun, GUN_REST, GUN_REST_Q, 0.8);
}

const muzzle = new THREE.Sprite(new THREE.SpriteMaterial({
  color: 0xffd080, transparent: true, opacity: 0, depthWrite: false, toneMapped: false,
}));
muzzle.scale.setScalar(0.25);
gun.add(muzzle);
muzzle.position.set(0, 0.012, 0.16);

const screenFlash = new THREE.Mesh(
  new THREE.PlaneGeometry(4, 4),
  new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0, depthTest: false, toneMapped: false }),
);
screenFlash.position.z = -0.2;
screenFlash.renderOrder = 100;
camera.add(screenFlash);

async function fire(died, shooterIsMe, seat) {
  const { cyl, hammer, flash } = gun.userData;
  const r0 = cyl.rotation.z;
  await tween(0.9, (t) => { cyl.rotation.z = r0 + t * Math.PI * 4 + Math.PI / 3; }, { ease: easeOut });
  hammer.rotation.x = 0.3;
  setTimeout(() => { hammer.rotation.x = -0.4; }, 150);
  if (!died) {
    if (seat) {
      const { head } = seat.char.userData;
      tween(0.5, (t) => { head.rotation.x = -Math.sin(t * Math.PI) * 0.2; });
    }
    return;
  }
  tween(0.35, (t) => {
    flash.intensity = 25 * (1 - t);
    muzzle.material.opacity = 1 - t;
    muzzle.scale.setScalar(0.25 + t * 0.2);
  }, { ease: (t) => t });
  camShake.amount = shooterIsMe ? 0.06 : 0.025;
  if (shooterIsMe) {
    tween(0.5, (t) => { screenFlash.material.opacity = 1 - t; }, { ease: (t) => t });
    tween(0.9, (t) => { camShake.roll = 0.5 * t; camShake.drop = 0.5 * t; }, { ease: easeOut });
    tween(1.2, (t) => { camShake.roll = 0.5 * (1 - t); camShake.drop = 0.5 * (1 - t); }, { delay: 2.4 });
  } else if (seat) {
    slump(seat, true);
  }
}

// --------------------------------------------------------------------- main

const handGroup = new THREE.Group();
camera.add(handGroup);
let handCards = new Map(); // id → card
const HAND_DIST = 0.6;
let insets = { top: 0, bottom: 0 };

function layoutHand(state, animate) {
  const visH = 2 * HAND_DIST * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2));
  const visW = visH * camera.aspect;
  const cards = state.hand;
  const n = Math.max(cards.length, 1);
  // Limité en largeur (5 cartes) et en hauteur (≤ 22 % de l'écran).
  const scale = Math.min(1, (visW * 0.86) / (n * CARD_W * 1.08), (visH * 0.22) / CARD_H);
  const w = CARD_W * scale * 1.08;
  const h = window.innerHeight || 1;
  const cardPx = (CARD_H * scale / visH) * h;
  const centerPx = insets.bottom + 18 + cardPx / 2;
  const yNdc = -1 + (2 * centerPx) / h;
  const baseY = yNdc * (visH / 2);
  const selected = new Set(state.selected || []);
  cards.forEach((c, i) => {
    const card = handCards.get(c.id);
    if (!card) return;
    const x = (i - (cards.length - 1) / 2) * w;
    const lift = selected.has(c.id) ? CARD_H * scale * 0.28 : 0;
    const target = new THREE.Vector3(x, baseY + lift, -HAND_DIST);
    const q = quatFromEuler(0, 0, -(i - (cards.length - 1) / 2) * 0.03);
    card.scale.setScalar(scale);
    card.userData.selected = selected.has(c.id);
    if (animate) moveTo(card, target, q, 0.18);
    else {
      card.position.copy(target);
      card.quaternion.copy(q);
    }
  });
}

function syncHand(state, dealt) {
  const ids = new Set(state.hand.map((c) => c.id));
  for (const [id, card] of handCards) {
    if (!ids.has(id)) {
      handGroup.remove(card);
      handCards.delete(id);
    }
  }
  state.hand.forEach((c, i) => {
    if (handCards.has(c.id)) return;
    const card = createCard(c.r, true);
    card.userData.id = c.id;
    handGroup.add(card);
    handCards.set(c.id, card);
    if (dealt) {
      card.visible = false;
      setTimeout(() => {
        card.visible = true;
        const s = card.scale.x;
        tween(0.3, (t) => card.scale.setScalar(s * t), { ease: easeOut });
      }, 300 + i * 110);
    }
  });
  layoutHand(state, !dealt);
}

// ------------------------------------------------------------------ état

let prev = null;

function update(state) {
  if (state.insets) insets = state.insets;
  const key = state.players.map((p) => p.id).join('|') + '#' + state.me;
  const rebuilt = key !== seatKey;
  if (rebuilt) buildSeats(state);

  const newRound = !prev || rebuilt || state.round !== prev.round;
  if (newRound) {
    clearPile(!rebuilt && !!prev);
    if (gunHolder) lowerGun();
  }

  // Joueurs : étiquettes, cartes en main, morts.
  for (const p of state.players) {
    const seat = seats.get(p.id);
    if (!seat) continue;
    const isCur = state.phase === PHASE.playing && state.current === p.id;
    const labelKey = JSON.stringify([p.name, p.alive, p.cards, p.shots, p.conn, isCur]);
    if (labelKey !== seat.lastLabel) {
      drawLabel(seat.label, p, isCur);
      seat.lastLabel = labelKey;
    }
    seat.isCurrent = isCur;
    if (!newRound && prev && state.lastPlayBy === p.id && state.pile > prev.pile) {
      // Les cartes jouées partent de sa pile.
      setStackCount(seat, p.cards);
    } else if (newRound && p.alive) {
      setStackCount(seat, 0);
      const n = p.cards;
      for (let k = 1; k <= n; k++) setTimeout(() => setStackCount(seat, k), 250 + k * 110);
    } else {
      setStackCount(seat, p.alive ? p.cards : 0);
    }
    if (!p.alive && !(state.phase === PHASE.roulette && state.shooter === p.id && state.shot === true && prev && prev.shot == null)) {
      slump(seat, false);
    }
  }

  // Cartes jouées → vol vers le tas.
  if (!newRound && prev && state.pile > prev.pile && state.lastPlayBy) {
    const n = state.pile - prev.pile;
    if (state.lastPlayBy === state.me) {
      flyToPile(camera.localToWorld(new THREE.Vector3(0, -0.15, -0.6)), n);
    } else {
      const seat = seats.get(state.lastPlayBy);
      flyToPile(seat ? seat.stack.position.clone() : ME_ORIGIN, n);
    }
  }
  if (rebuilt || (newRound && state.pile > 0)) {
    for (let i = 0; i < state.pile; i++) {
      const card = createCard(null);
      const { pos, quat } = addPileCard(card);
      card.position.copy(pos);
      card.quaternion.copy(quat);
      scene.add(card);
    }
  }

  // Révélation.
  const revealNow = state.reveal && (state.phase === PHASE.reveal || state.phase === PHASE.roulette);
  if (revealNow && !(prev && prev.reveal && prev.round === state.round)) {
    const cards = state.reveal.cards;
    const taken = pileCards.slice(-cards.length);
    taken.forEach((card, i) => {
      setCardRank(card, cards[i].r);
      const x = (i - (taken.length - 1) / 2) * (CARD_W * 1.25);
      const pos = new THREE.Vector3(x, TABLE_Y + 0.32, PILE.z + 0.45);
      const quat = quatFromEuler(-0.35, 0, 0);
      moveTo(card, pos, quat, 0.6, { delay: 0.2 + i * 0.3, ease: easeOut });
    });
  }

  // Roulette.
  if (state.phase === PHASE.roulette && state.shooter && state.shooter !== gunHolder) {
    gunHolder = state.shooter;
    if (state.shooter === state.me) raiseGunToMe();
    else if (seats.get(state.shooter)) raiseGun(seats.get(state.shooter));
  }
  if (state.phase === PHASE.roulette && state.shot != null && prev && prev.shot == null) {
    fire(state.shot, state.shooter === state.me, seats.get(state.shooter));
  }
  if (state.phase === PHASE.gameOver && gunHolder) lowerGun();

  syncHand(state, newRound && !rebuilt && !!prev);
  prev = state;
}

// ---------------------------------------------------------------- entrées

const raycaster = new THREE.Raycaster();
renderer.domElement.addEventListener('pointerdown', (e) => {
  if (!prev || !prev.canSelect) return;
  const rect = renderer.domElement.getBoundingClientRect();
  const ndc = new THREE.Vector2(
    ((e.clientX - rect.left) / rect.width) * 2 - 1,
    -((e.clientY - rect.top) / rect.height) * 2 + 1,
  );
  raycaster.setFromCamera(ndc, camera);
  const hits = raycaster.intersectObjects([...handCards.values()], true);
  if (!hits.length) return;
  let o = hits[0].object;
  while (o && o.userData.id == null) o = o.parent;
  if (o) send({ t: 'tap', id: o.userData.id });
});

function send(msg) {
  const json = JSON.stringify(msg);
  if (window.Bridge && window.Bridge.postMessage) window.Bridge.postMessage(json);
  else console.log('bridge', json);
}

// ------------------------------------------------------------------ boucle

function resize() {
  const w = window.innerWidth || 1, h = window.innerHeight || 1;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  // Plus l'écran est étroit, plus on recule pour voir toute la table.
  const halfH = Math.atan(Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * camera.aspect);
  const dist = Math.max(2.2, 1.15 / Math.tan(halfH));
  camBase.set(0, 1.0 + dist * 0.55, 0.45 + dist * 0.85);
  camera.updateProjectionMatrix();
  if (prev) layoutHand(prev, false);
}
window.addEventListener('resize', resize);
resize();

const lookTarget = new THREE.Vector3();
function frame(now) {
  requestAnimationFrame(frame);
  runTweens(now);
  const t = now / 1000;

  lamp.rotation.z = Math.sin(t * 0.6) * 0.025;
  lamp.rotation.x = Math.cos(t * 0.45) * 0.02;

  camShake.amount *= 0.9;
  camera.position.copy(camBase);
  camera.position.y -= camShake.drop;
  camera.position.x += (Math.random() - 0.5) * camShake.amount;
  camera.position.y += (Math.random() - 0.5) * camShake.amount + Math.sin(t * 0.7) * 0.01;
  camera.lookAt(CAM_TARGET);
  camera.rotateZ(camShake.roll);

  // Les vivants respirent et regardent le joueur dont c'est le tour.
  const cur = prev && prev.phase === PHASE.roulette ? prev.shooter : prev && prev.current;
  const curSeat = cur ? seats.get(cur) : null;
  if (curSeat) lookTarget.copy(headWorld(curSeat));
  else if (cur && prev && cur === prev.me) lookTarget.copy(camera.position);
  else lookTarget.copy(PILE);
  for (const seat of seats.values()) {
    const { torso, head, phase } = seat.char.userData;
    seat.ring.material.opacity = seat.isCurrent ? 0.55 + Math.sin(t * 5) * 0.35 : 0;
    if (seat.slumped) continue;
    torso.scale.y = 1 + Math.sin(t * 1.6 + phase) * 0.012;
    if (seat === curSeat) {
      head.rotation.y *= 0.9;
      continue;
    }
    const local = seat.char.worldToLocal(lookTarget.clone());
    const yaw = THREE.MathUtils.clamp(Math.atan2(local.x, local.z), -1.1, 1.1);
    head.rotation.y += (yaw - head.rotation.y) * 0.06;
  }

  // Les cartes de la main se soulèvent légèrement à mon tour.
  renderer.render(scene, camera);
}
requestAnimationFrame(frame);

window.table3d = { update, resize };
send({ t: 'ready' });
