// Textures générées sur canvas : aucune image externe.
import * as THREE from 'three';

export const RANKS = ['king', 'queen', 'ace', 'joker'];
const RANK_SHORT = ['R', 'D', 'A', 'J'];
const RANK_LABEL = ['ROI', 'DAME', 'AS', 'JOKER'];
const RANK_COLOR = ['#b0302a', '#6a3d9a', '#1f3b5c', '#2e7d4f'];

function canvas(w, h) {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return [c, c.getContext('2d')];
}

function tex(c, { repeat, srgb = true } = {}) {
  const t = new THREE.CanvasTexture(c);
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  if (repeat) {
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.repeat.set(repeat[0], repeat[1]);
  }
  return t;
}

function roundRect(g, x, y, w, h, r) {
  g.beginPath();
  g.moveTo(x + r, y);
  g.arcTo(x + w, y, x + w, y + h, r);
  g.arcTo(x + w, y + h, x, y + h, r);
  g.arcTo(x, y + h, x, y, r);
  g.arcTo(x, y, x + w, y, r);
  g.closePath();
}

function drawEmblem(g, rank, cx, cy, s, color) {
  g.fillStyle = color;
  g.strokeStyle = color;
  if (rank === 0) {
    // Couronne
    g.beginPath();
    g.moveTo(cx - s * 0.38, cy + s * 0.28);
    g.lineTo(cx - s * 0.44, cy - s * 0.2);
    g.lineTo(cx - s * 0.18, cy + s * 0.02);
    g.lineTo(cx, cy - s * 0.32);
    g.lineTo(cx + s * 0.18, cy + s * 0.02);
    g.lineTo(cx + s * 0.44, cy - s * 0.2);
    g.lineTo(cx + s * 0.38, cy + s * 0.28);
    g.closePath();
    g.fill();
    g.fillRect(cx - s * 0.38, cy + s * 0.32, s * 0.76, s * 0.1);
    for (const [dx, dy] of [[-0.44, -0.24], [0, -0.37], [0.44, -0.24]]) {
      g.beginPath();
      g.arc(cx + dx * s, cy + dy * s, s * 0.06, 0, Math.PI * 2);
      g.fill();
    }
  } else if (rank === 1) {
    // Fleur
    for (let i = 0; i < 5; i++) {
      const a = (i / 5) * Math.PI * 2 - Math.PI / 2;
      g.beginPath();
      g.arc(cx + Math.cos(a) * s * 0.2, cy - s * 0.08 + Math.sin(a) * s * 0.2, s * 0.15, 0, Math.PI * 2);
      g.fill();
    }
    g.fillStyle = '#f4ead3';
    g.beginPath();
    g.arc(cx, cy - s * 0.08, s * 0.09, 0, Math.PI * 2);
    g.fill();
    g.fillStyle = color;
    g.lineWidth = s * 0.06;
    g.beginPath();
    g.moveTo(cx, cy + s * 0.12);
    g.quadraticCurveTo(cx - s * 0.05, cy + s * 0.3, cx, cy + s * 0.42);
    g.stroke();
  } else if (rank === 2) {
    // Étoile
    g.beginPath();
    for (let i = 0; i < 10; i++) {
      const a = (i / 10) * Math.PI * 2 - Math.PI / 2;
      const r = i % 2 === 0 ? s * 0.42 : s * 0.18;
      g.lineTo(cx + Math.cos(a) * r, cy + Math.sin(a) * r);
    }
    g.closePath();
    g.fill();
  } else {
    // Masques de théâtre
    for (const [dx, sad] of [[-0.14, false], [0.14, true]]) {
      const mx = cx + dx * s, my = cy + (sad ? -0.06 : 0.04) * s;
      g.fillStyle = color;
      g.beginPath();
      g.ellipse(mx, my, s * 0.2, s * 0.26, 0, 0, Math.PI * 2);
      g.fill();
      g.fillStyle = '#f4ead3';
      for (const ex of [-0.08, 0.08]) {
        g.beginPath();
        g.ellipse(mx + ex * s, my - s * 0.07, s * 0.04, s * 0.03, 0, 0, Math.PI * 2);
        g.fill();
      }
      g.lineWidth = s * 0.035;
      g.strokeStyle = '#f4ead3';
      g.beginPath();
      if (sad) g.arc(mx, my + s * 0.15, s * 0.07, Math.PI * 1.15, Math.PI * 1.85);
      else g.arc(mx, my + s * 0.06, s * 0.07, Math.PI * 0.15, Math.PI * 0.85);
      g.stroke();
    }
  }
}

const cardCache = new Map();

export function cardFaceTexture(rank) {
  if (cardCache.has(rank)) return cardCache.get(rank);
  const W = 256, H = 372;
  const [c, g] = canvas(W, H);
  roundRect(g, 0, 0, W, H, 26);
  g.fillStyle = '#f4ead3';
  g.fill();
  g.lineWidth = 4;
  g.strokeStyle = 'rgba(0,0,0,0.25)';
  g.stroke();
  const color = RANK_COLOR[rank];
  g.fillStyle = color;
  g.font = '900 56px sans-serif';
  g.textAlign = 'left';
  g.textBaseline = 'top';
  g.fillText(RANK_SHORT[rank], 20, 16);
  g.save();
  g.translate(W - 20, H - 16);
  g.rotate(Math.PI);
  g.fillText(RANK_SHORT[rank], 0, 0);
  g.restore();
  drawEmblem(g, rank, W / 2, H / 2 - 18, 150, color);
  g.font = '800 34px sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.fillStyle = color;
  g.fillText(RANK_LABEL[rank], W / 2, H / 2 + 82);
  const t = tex(c);
  cardCache.set(rank, t);
  return t;
}

let backTex;
export function cardBackTexture() {
  if (backTex) return backTex;
  const W = 256, H = 372;
  const [c, g] = canvas(W, H);
  roundRect(g, 0, 0, W, H, 26);
  const grad = g.createLinearGradient(0, 0, W, H);
  grad.addColorStop(0, '#7a1f1a');
  grad.addColorStop(1, '#3e0e0b');
  g.fillStyle = grad;
  g.fill();
  g.strokeStyle = 'rgba(217,174,82,0.75)';
  g.lineWidth = 5;
  roundRect(g, 22, 22, W - 44, H - 44, 14);
  g.stroke();
  g.save();
  g.translate(W / 2, H / 2);
  g.rotate(Math.PI / 4);
  g.lineWidth = 9;
  g.strokeRect(-40, -40, 80, 80);
  g.restore();
  // Motif discret
  g.strokeStyle = 'rgba(217,174,82,0.12)';
  g.lineWidth = 2;
  for (let i = -H; i < W + H; i += 18) {
    g.beginPath();
    g.moveTo(i, 22);
    g.lineTo(i - H, H + 22);
    g.stroke();
  }
  backTex = tex(c);
  return backTex;
}

function noise(g, w, h, amount, alpha) {
  const img = g.getImageData(0, 0, w, h);
  for (let i = 0; i < img.data.length; i += 4) {
    const n = (Math.random() - 0.5) * amount;
    img.data[i] += n;
    img.data[i + 1] += n;
    img.data[i + 2] += n;
    img.data[i + 3] = alpha ?? img.data[i + 3];
  }
  g.putImageData(img, 0, 0);
}

export function feltTexture() {
  const [c, g] = canvas(512, 512);
  const grad = g.createRadialGradient(256, 256, 30, 256, 256, 256);
  grad.addColorStop(0, '#22604a');
  grad.addColorStop(1, '#123a2b');
  g.fillStyle = grad;
  g.fillRect(0, 0, 512, 512);
  noise(g, 512, 512, 18);
  return tex(c);
}

export function woodTexture(base = '#5a3a22', dark = '#3a2414', repeat = [1, 1]) {
  const [c, g] = canvas(512, 512);
  g.fillStyle = base;
  g.fillRect(0, 0, 512, 512);
  for (let i = 0; i < 70; i++) {
    g.strokeStyle = Math.random() < 0.5 ? dark : 'rgba(255,220,170,0.06)';
    g.globalAlpha = 0.25 + Math.random() * 0.35;
    g.lineWidth = 1 + Math.random() * 3;
    const y = Math.random() * 512;
    g.beginPath();
    g.moveTo(0, y);
    for (let x = 0; x <= 512; x += 32) g.lineTo(x, y + Math.sin(x / 60 + i) * 4);
    g.stroke();
  }
  g.globalAlpha = 1;
  noise(g, 512, 512, 14);
  return tex(c, { repeat });
}

export function floorTexture() {
  const [c, g] = canvas(512, 512);
  const boards = 6;
  for (let b = 0; b < boards; b++) {
    const shade = 40 + Math.random() * 18;
    g.fillStyle = `rgb(${shade + 30},${shade + 12},${shade - 6})`;
    g.fillRect(0, (b * 512) / boards, 512, 512 / boards);
    g.fillStyle = 'rgba(0,0,0,0.55)';
    g.fillRect(0, (b * 512) / boards, 512, 3);
    const off = Math.random() * 512;
    g.fillRect(off, (b * 512) / boards, 3, 512 / boards);
  }
  noise(g, 512, 512, 22);
  return tex(c, { repeat: [5, 5] });
}

export function brickTexture() {
  const [c, g] = canvas(512, 512);
  g.fillStyle = '#2a1712';
  g.fillRect(0, 0, 512, 512);
  const bw = 64, bh = 28;
  for (let row = 0; row < 512 / bh + 1; row++) {
    for (let col = -1; col < 512 / bw + 1; col++) {
      const x = col * bw + (row % 2) * (bw / 2);
      const r = 80 + Math.random() * 30, gg = 38 + Math.random() * 14, b = 28 + Math.random() * 10;
      g.fillStyle = `rgb(${r},${gg},${b})`;
      g.fillRect(x + 2, row * bh + 2, bw - 4, bh - 4);
    }
  }
  noise(g, 512, 512, 20);
  return tex(c, { repeat: [4, 2] });
}

export function signTexture(text) {
  const [c, g] = canvas(1024, 256);
  g.fillStyle = 'rgba(0,0,0,0)';
  g.fillRect(0, 0, 1024, 256);
  g.font = '900 150px sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.shadowColor = '#ff3b2f';
  g.shadowBlur = 40;
  g.fillStyle = '#ff6a55';
  g.fillText(text, 512, 135);
  g.shadowBlur = 0;
  g.fillStyle = '#ffd9cf';
  g.fillText(text, 512, 135);
  return tex(c);
}

/** Étiquette au-dessus d'un joueur : nom, cartes, barillet. */
export function drawLabel(label, p, isCurrent) {
  const c = label.canvas;
  const g = c.getContext('2d');
  const W = c.width, H = c.height;
  g.clearRect(0, 0, W, H);
  roundRect(g, 6, 6, W - 12, H - 12, 36);
  g.fillStyle = isCurrent ? 'rgba(60,40,10,0.88)' : 'rgba(10,8,6,0.72)';
  g.fill();
  g.lineWidth = isCurrent ? 8 : 3;
  g.strokeStyle = isCurrent ? '#d9ae52' : 'rgba(255,255,255,0.25)';
  g.stroke();
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.font = '800 54px sans-serif';
  g.fillStyle = !p.alive ? '#8a7f74' : isCurrent ? '#d9ae52' : '#f4ead3';
  let name = p.name;
  while (g.measureText(name).width > W - 60 && name.length > 2) name = name.slice(0, -1);
  if (name !== p.name) name += '…';
  g.fillText(name, W / 2, 62);
  g.font = '600 38px sans-serif';
  if (!p.alive) {
    g.fillStyle = '#c0392b';
    g.fillText('Éliminé', W / 2, 128);
  } else {
    const status = !p.conn ? 'déconnecté' : `${p.cards} carte${p.cards > 1 ? 's' : ''}`;
    g.fillStyle = !p.conn ? '#e57368' : '#a89a86';
    g.fillText(status, W / 2, 120);
    for (let i = 0; i < 6; i++) {
      g.beginPath();
      g.arc(W / 2 - 75 + i * 30, 168, 10, 0, Math.PI * 2);
      g.fillStyle = i < p.shots ? '#c0392b' : 'rgba(255,255,255,0.28)';
      g.fill();
    }
  }
  label.texture.needsUpdate = true;
}

export function createLabelCanvas() {
  const [c] = canvas(400, 200);
  const t = tex(c);
  return { canvas: c, texture: t };
}
