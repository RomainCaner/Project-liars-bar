// Modèles 3D construits à partir de primitives (low-poly, légers sur mobile).
import * as THREE from 'three';
import {
  brickTexture, cardBackTexture, cardFaceTexture, feltTexture, floorTexture,
  signTexture, woodTexture,
} from './textures.js';

export const TABLE_Y = 0.8;
export const TABLE_W = 1.25;
export const TABLE_L = 3.3;
export const CARD_W = 0.15;
export const CARD_H = CARD_W * 1.45;

// ------------------------------------------------------------------ cartes

let cardGeo;
function cardGeometry() {
  if (cardGeo) return cardGeo;
  const w = CARD_W, h = CARD_H, r = w * 0.12;
  const s = new THREE.Shape();
  s.moveTo(-w / 2 + r, -h / 2);
  s.lineTo(w / 2 - r, -h / 2);
  s.quadraticCurveTo(w / 2, -h / 2, w / 2, -h / 2 + r);
  s.lineTo(w / 2, h / 2 - r);
  s.quadraticCurveTo(w / 2, h / 2, w / 2 - r, h / 2);
  s.lineTo(-w / 2 + r, h / 2);
  s.quadraticCurveTo(-w / 2, h / 2, -w / 2, h / 2 - r);
  s.lineTo(-w / 2, -h / 2 + r);
  s.quadraticCurveTo(-w / 2, -h / 2, -w / 2 + r, -h / 2);
  const geo = new THREE.ShapeGeometry(s, 4);
  const pos = geo.attributes.position, uv = geo.attributes.uv;
  for (let i = 0; i < pos.count; i++) {
    uv.setXY(i, (pos.getX(i) + w / 2) / w, (pos.getY(i) + h / 2) / h);
  }
  cardGeo = geo;
  return geo;
}

const faceMats = new Map();
let backMat, backMatUnlit;

/**
 * Carte : face vers +Z, dos vers -Z. rank = null pour une carte inconnue.
 * unlit : cartes de la main, toujours bien lisibles quel que soit l'éclairage.
 */
export function createCard(rank = null, unlit = false) {
  const g = new THREE.Group();
  if (!backMat) {
    backMat = new THREE.MeshStandardMaterial({ map: cardBackTexture(), roughness: 0.6 });
    backMatUnlit = new THREE.MeshBasicMaterial({ map: cardBackTexture() });
  }
  const bm = unlit ? backMatUnlit : backMat;
  const front = new THREE.Mesh(cardGeometry(), bm);
  const back = new THREE.Mesh(cardGeometry(), bm);
  g.userData.unlit = unlit;
  front.position.z = 0.0008;
  back.rotation.y = Math.PI;
  back.position.z = -0.0008;
  front.castShadow = back.castShadow = true;
  g.add(front, back);
  g.userData.front = front;
  setCardRank(g, rank);
  return g;
}

export function setCardRank(card, rank) {
  card.userData.rank = rank;
  const unlit = card.userData.unlit;
  if (rank == null) {
    card.userData.front.material = unlit ? backMatUnlit : backMat;
    return;
  }
  const key = `${rank}-${unlit}`;
  if (!faceMats.has(key)) {
    faceMats.set(key, unlit
      ? new THREE.MeshBasicMaterial({ map: cardFaceTexture(rank) })
      : new THREE.MeshStandardMaterial({ map: cardFaceTexture(rank), roughness: 0.55 }));
  }
  card.userData.front.material = faceMats.get(key);
}

// ------------------------------------------------------------------- salle

export function buildRoom(scene) {
  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(14, 14),
    new THREE.MeshStandardMaterial({ map: floorTexture(), roughness: 0.85 }),
  );
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  scene.add(floor);

  const wallMat = new THREE.MeshStandardMaterial({ map: brickTexture(), roughness: 0.95 });
  const H = 4.2;
  for (const [x, z, ry] of [[0, -5, 0], [0, 5, Math.PI], [-5, 0, Math.PI / 2], [5, 0, -Math.PI / 2]]) {
    const wall = new THREE.Mesh(new THREE.PlaneGeometry(10, H), wallMat);
    wall.position.set(x, H / 2, z);
    wall.rotation.y = ry;
    scene.add(wall);
  }
  const ceiling = new THREE.Mesh(
    new THREE.PlaneGeometry(10, 10),
    new THREE.MeshStandardMaterial({ color: 0x140c08, roughness: 1 }),
  );
  ceiling.rotation.x = Math.PI / 2;
  ceiling.position.y = H;
  scene.add(ceiling);

  // Comptoir et étagères à bouteilles au fond.
  const wood = new THREE.MeshStandardMaterial({ map: woodTexture('#4a2c18', '#2a170c', [3, 1]), roughness: 0.6 });
  const counter = new THREE.Mesh(new THREE.BoxGeometry(5, 1.1, 0.7), wood);
  counter.position.set(0, 0.55, -4.4);
  counter.castShadow = counter.receiveShadow = true;
  scene.add(counter);
  const top = new THREE.Mesh(new THREE.BoxGeometry(5.2, 0.08, 0.85), wood);
  top.position.set(0, 1.12, -4.4);
  scene.add(top);
  const glassColors = [0x2f6b3a, 0x7a3b12, 0x9c7a2a, 0x3a2a5c, 0x6b1f1f, 0x2a4f6b];
  for (let shelf = 0; shelf < 2; shelf++) {
    const board = new THREE.Mesh(new THREE.BoxGeometry(4.4, 0.05, 0.3), wood);
    board.position.set(0, 1.9 + shelf * 0.62, -4.85);
    scene.add(board);
    for (let i = 0; i < 16; i++) {
      const h = 0.28 + Math.random() * 0.14;
      const bottle = new THREE.Mesh(
        new THREE.CylinderGeometry(0.045, 0.055, h, 8),
        new THREE.MeshStandardMaterial({
          color: glassColors[(i * 7 + shelf * 3) % glassColors.length],
          roughness: 0.15, metalness: 0.1, emissive: 0x110804,
        }),
      );
      bottle.position.set(-2.05 + i * 0.27 + Math.random() * 0.05, 1.93 + shelf * 0.62 + h / 2, -4.85);
      const neck = new THREE.Mesh(new THREE.CylinderGeometry(0.015, 0.02, 0.1, 6), bottle.material);
      neck.position.y = h / 2 + 0.05;
      bottle.add(neck);
      scene.add(bottle);
    }
  }

  // Enseigne lumineuse.
  const sign = new THREE.Mesh(
    new THREE.PlaneGeometry(2.4, 0.6),
    new THREE.MeshBasicMaterial({ map: signTexture('MENTEUR'), transparent: true, toneMapped: false }),
  );
  sign.position.set(0, 3.35, -4.95);
  scene.add(sign);
  const neon = new THREE.PointLight(0xff4a3a, 3, 6, 2);
  neon.position.set(0, 3.2, -4.4);
  scene.add(neon);

  // Appliques murales.
  for (const x of [-4.9, 4.9]) {
    const l = new THREE.PointLight(0xffa860, 2.2, 6, 2);
    l.position.set(x * 0.95, 2.6, 0);
    scene.add(l);
    const bulb = new THREE.Mesh(
      new THREE.SphereGeometry(0.08, 10, 8),
      new THREE.MeshBasicMaterial({ color: 0xffc890 }),
    );
    bulb.position.copy(l.position);
    scene.add(bulb);
  }
}

function stadium(w, l, r) {
  const s = new THREE.Shape();
  const x = w / 2, z = l / 2;
  s.moveTo(-x + r, -z);
  s.lineTo(x - r, -z);
  s.quadraticCurveTo(x, -z, x, -z + r);
  s.lineTo(x, z - r);
  s.quadraticCurveTo(x, z, x - r, z);
  s.lineTo(-x + r, z);
  s.quadraticCurveTo(-x, z, -x, z - r);
  s.lineTo(-x, -z + r);
  s.quadraticCurveTo(-x, -z, -x + r, -z);
  return s;
}

/** Longue table de bar (adaptée à l'écran en portrait). */
export function buildTable(scene) {
  const g = new THREE.Group();
  const rimMat = new THREE.MeshStandardMaterial({ map: woodTexture('#6b4226', '#3c2312', [2, 2]), roughness: 0.45 });
  const top = new THREE.Mesh(
    new THREE.ExtrudeGeometry(stadium(TABLE_W, TABLE_L, 0.35), {
      depth: 0.07, bevelEnabled: true, bevelSize: 0.03, bevelThickness: 0.02, bevelSegments: 2, curveSegments: 8,
    }),
    rimMat,
  );
  top.rotation.x = Math.PI / 2;
  top.position.y = TABLE_Y;
  top.castShadow = top.receiveShadow = true;
  g.add(top);
  const feltGeo = new THREE.ShapeGeometry(stadium(TABLE_W - 0.16, TABLE_L - 0.16, 0.28), 8);
  const pos = feltGeo.attributes.position, uv = feltGeo.attributes.uv;
  for (let i = 0; i < pos.count; i++) {
    uv.setXY(i, pos.getX(i) / TABLE_W + 0.5, pos.getY(i) / TABLE_L + 0.5);
  }
  const felt = new THREE.Mesh(feltGeo, new THREE.MeshStandardMaterial({ map: feltTexture(), roughness: 0.95 }));
  felt.rotation.x = -Math.PI / 2;
  felt.position.y = TABLE_Y + 0.023;
  felt.receiveShadow = true;
  g.add(felt);
  for (const [x, z] of [[-0.5, -1.2], [0.5, -1.2], [-0.5, 1.2], [0.5, 1.2]]) {
    const leg = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.04, TABLE_Y - 0.05, 8), rimMat);
    leg.position.set(x, (TABLE_Y - 0.05) / 2, z);
    leg.castShadow = true;
    g.add(leg);
  }
  scene.add(g);
  return g;
}

/** Lampe suspendue au-dessus de la table : la principale source de lumière. */
export function buildLamp(scene) {
  const pivot = new THREE.Group();
  pivot.position.set(0, 4.2, 0);
  const cord = new THREE.Mesh(
    new THREE.CylinderGeometry(0.008, 0.008, 1.45, 4),
    new THREE.MeshBasicMaterial({ color: 0x111111 }),
  );
  cord.position.y = -0.72;
  pivot.add(cord);
  const shade = new THREE.Mesh(
    new THREE.ConeGeometry(0.34, 0.24, 24, 1, true),
    new THREE.MeshStandardMaterial({ color: 0x1f3a2a, side: THREE.DoubleSide, roughness: 0.5, metalness: 0.4 }),
  );
  shade.position.y = -1.55;
  pivot.add(shade);
  const bulb = new THREE.Mesh(
    new THREE.SphereGeometry(0.08, 12, 8),
    new THREE.MeshBasicMaterial({ color: 0xfff1d0, toneMapped: false }),
  );
  bulb.position.y = -1.65;
  pivot.add(bulb);
  const spot = new THREE.SpotLight(0xffd9a0, 55, 8, Math.PI / 3, 0.6, 1.5);
  spot.position.y = -1.63;
  spot.castShadow = true;
  spot.shadow.mapSize.set(1024, 1024);
  spot.shadow.bias = -0.0005;
  spot.shadow.camera.near = 0.3;
  spot.shadow.camera.far = 5;
  const target = new THREE.Object3D();
  target.position.y = -4.2;
  pivot.add(target);
  spot.target = target;
  pivot.add(spot);
  scene.add(pivot);
  return pivot;
}

// -------------------------------------------------------------- personnages

let chairMat;
export function createChair() {
  chairMat ??= new THREE.MeshStandardMaterial({ map: woodTexture('#4a2c18', '#2a170c'), roughness: 0.7 });
  const g = new THREE.Group();
  const seat = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.05, 0.45), chairMat);
  seat.position.set(0, 0.47, -0.05);
  seat.castShadow = true;
  g.add(seat);
  const back = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.6, 0.05), chairMat);
  back.position.set(0, 0.8, -0.28);
  back.castShadow = true;
  g.add(back);
  for (const [x, z] of [[-0.21, -0.25], [0.21, -0.25], [-0.21, 0.15], [0.21, 0.15]]) {
    const leg = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.47, 0.04), chairMat);
    leg.position.set(x, 0.235, z);
    g.add(leg);
  }
  return g;
}

const SKIN = [0xe0b48f, 0xc68c62, 0x9a6440, 0xf0cba8, 0x7a4a2c, 0xd9a47a];
const COATS = [0x6b1f1f, 0x1f3f6b, 0x3b5a2a, 0x5a3a6b, 0x6b5a1f, 0x2a5a5a, 0x4a4a4a, 0x7a3a10];

function hashStr(s) {
  let h = 0;
  for (const ch of s) h = (h * 31 + ch.charCodeAt(0)) | 0;
  return Math.abs(h);
}

/**
 * Client du bar stylisé. Le groupe est orienté vers +Z (vers la table une fois
 * placé avec lookAt). userData.torso pivote pour s'affaler.
 */
export function createCharacter(id) {
  const h = hashStr(id);
  const skinMat = new THREE.MeshStandardMaterial({ color: SKIN[h % SKIN.length], roughness: 0.7 });
  const coatMat = new THREE.MeshStandardMaterial({ color: COATS[(h >> 3) % COATS.length], roughness: 0.8 });
  const dark = new THREE.MeshStandardMaterial({ color: 0x1a1410, roughness: 0.9 });

  const root = new THREE.Group();
  root.add(createChair());

  const torso = new THREE.Group();
  torso.position.set(0, 0.5, -0.05);
  root.add(torso);

  const body = new THREE.Mesh(new THREE.CylinderGeometry(0.17, 0.23, 0.62, 12), coatMat);
  body.position.y = 0.33;
  body.castShadow = true;
  torso.add(body);
  const shoulders = new THREE.Mesh(new THREE.SphereGeometry(0.2, 14, 10), coatMat);
  shoulders.scale.set(1.25, 0.6, 0.9);
  shoulders.position.y = 0.62;
  shoulders.castShadow = true;
  torso.add(shoulders);

  // Bras posés vers la table
  for (const side of [-1, 1]) {
    const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.055, 0.36, 4, 8), coatMat);
    arm.position.set(side * 0.24, 0.48, 0.16);
    arm.rotation.x = -1.1;
    arm.rotation.z = side * -0.25;
    arm.castShadow = true;
    torso.add(arm);
    const hand = new THREE.Mesh(new THREE.SphereGeometry(0.05, 10, 8), skinMat);
    hand.position.set(side * 0.15, 0.34, 0.37);
    torso.add(hand);
  }

  const head = new THREE.Group();
  head.position.y = 0.86;
  torso.add(head);
  const skull = new THREE.Mesh(new THREE.SphereGeometry(0.15, 18, 14), skinMat);
  skull.scale.set(0.95, 1.08, 1);
  skull.castShadow = true;
  head.add(skull);
  const nose = new THREE.Mesh(new THREE.SphereGeometry(0.03, 8, 6), skinMat);
  nose.position.set(0, -0.01, 0.15);
  nose.scale.set(1, 1, 1.3);
  head.add(nose);
  for (const side of [-1, 1]) {
    const eye = new THREE.Mesh(new THREE.SphereGeometry(0.018, 8, 6), dark);
    eye.position.set(side * 0.055, 0.03, 0.135);
    head.add(eye);
    const brow = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.012, 0.012), dark);
    brow.position.set(side * 0.055, 0.07, 0.14);
    brow.rotation.z = side * ((h >> 5) % 2 ? 0.25 : -0.1);
    head.add(brow);
  }
  if ((h >> 7) % 3 === 0) {
    const mustache = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.02, 0.02), dark);
    mustache.position.set(0, -0.055, 0.14);
    head.add(mustache);
  }

  // Couvre-chef
  const hatType = (h >> 9) % 4;
  const hatMat = new THREE.MeshStandardMaterial({ color: [0x1a1a1a, 0x3a2a1a, 0x2a1a1a, 0x1a2a3a][(h >> 11) % 4], roughness: 0.7 });
  if (hatType === 0) {
    const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.24, 0.02, 20), hatMat);
    brim.position.y = 0.11;
    const crown = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.14, 0.16, 16), hatMat);
    crown.position.y = 0.2;
    head.add(brim, crown);
  } else if (hatType === 1) {
    const bowler = new THREE.Mesh(new THREE.SphereGeometry(0.155, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2), hatMat);
    bowler.position.y = 0.07;
    const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.015, 20), hatMat);
    brim.position.y = 0.075;
    head.add(bowler, brim);
  } else if (hatType === 2) {
    const cap = new THREE.Mesh(new THREE.SphereGeometry(0.155, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2), hatMat);
    cap.position.y = 0.04;
    cap.scale.y = 0.8;
    const visor = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.012, 0.12), hatMat);
    visor.position.set(0, 0.05, 0.15);
    head.add(cap, visor);
  } else {
    const hair = new THREE.Mesh(new THREE.SphereGeometry(0.158, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2.4), dark);
    hair.position.y = 0.01;
    head.add(hair);
  }

  root.userData = { torso, head, skinMat, coatMat, phase: (h % 100) / 16 };
  return root;
}

// ------------------------------------------------------------------ revolver

/** Revolver dont le canon pointe vers +Z (pratique avec lookAt). */
export function createRevolver() {
  const metal = new THREE.MeshStandardMaterial({ color: 0x5c6066, metalness: 0.85, roughness: 0.35 });
  const darkMetal = new THREE.MeshStandardMaterial({ color: 0x2a2c30, metalness: 0.8, roughness: 0.45 });
  const grip = new THREE.MeshStandardMaterial({ map: woodTexture('#6b3a1a', '#3a1c0a'), roughness: 0.55 });
  const g = new THREE.Group();

  const barrel = new THREE.Mesh(new THREE.CylinderGeometry(0.014, 0.014, 0.2, 12), metal);
  barrel.rotation.x = Math.PI / 2;
  barrel.position.set(0, 0.02, 0.12);
  g.add(barrel);
  const rib = new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.012, 0.2), darkMetal);
  rib.position.set(0, 0.036, 0.12);
  g.add(rib);
  const frame = new THREE.Mesh(new THREE.BoxGeometry(0.035, 0.06, 0.1), darkMetal);
  frame.position.set(0, 0.01, -0.01);
  g.add(frame);

  const cyl = new THREE.Group();
  cyl.position.set(0, 0.008, 0.01);
  const cylBody = new THREE.Mesh(new THREE.CylinderGeometry(0.032, 0.032, 0.06, 6), metal);
  cylBody.rotation.x = Math.PI / 2;
  cyl.add(cylBody);
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * Math.PI * 2;
    const hole = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, 0.062, 6), darkMetal);
    hole.rotation.x = Math.PI / 2;
    hole.position.set(Math.cos(a) * 0.019, Math.sin(a) * 0.019, 0);
    cyl.add(hole);
  }
  g.add(cyl);

  const handle = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.1, 0.045), grip);
  handle.position.set(0, -0.055, -0.06);
  handle.rotation.x = 0.35;
  g.add(handle);
  const guard = new THREE.Mesh(new THREE.TorusGeometry(0.02, 0.004, 6, 12, Math.PI), darkMetal);
  guard.rotation.y = Math.PI / 2;
  guard.rotation.x = Math.PI;
  guard.position.set(0, -0.02, 0.0);
  g.add(guard);
  const hammer = new THREE.Mesh(new THREE.BoxGeometry(0.01, 0.025, 0.015), darkMetal);
  hammer.position.set(0, 0.045, -0.06);
  hammer.rotation.x = -0.4;
  g.add(hammer);

  const flash = new THREE.PointLight(0xffb060, 0, 3, 2);
  flash.position.set(0, 0.02, 0.24);
  g.add(flash);

  g.traverse((o) => { if (o.isMesh) o.castShadow = true; });
  g.scale.setScalar(1.6);
  g.userData = { cyl, flash, hammer };
  return g;
}
