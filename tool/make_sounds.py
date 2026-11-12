"""Synthétise les effets sonores du jeu (python3 tool/make_sounds.py).

Tout est généré par calcul : aucun enregistrement tiers, aucun problème de droits.
"""
import wave
import numpy as np

SR = 22050
rng = np.random.default_rng(7)
OUT = 'assets/sounds'


def t(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.002, decay=0.1):
    x = np.arange(n) / SR
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-x / decay)


def lowpass(x, cutoff):
    # Filtre passe-bas simple (un pôle), suffisant pour colorer du bruit.
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def place(total, clip, at):
    s = int(at * SR)
    total[s:s + len(clip)] += clip[: max(0, len(total) - s)]


def save(name, x, peak=0.9):
    x = np.asarray(x, dtype=np.float64)
    m = np.max(np.abs(x)) or 1
    data = (x / m * peak * 32767).astype('<i2')
    with wave.open(f'{OUT}/{name}.wav', 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def flick(dur=0.07, bright=3500):
    n = noise(dur)
    return highpass(lowpass(n, bright), 600) * env(len(n), 0.001, dur / 3)


# Carte posée / glissée sur le feutre.
card = flick(0.09)
save('card', card, 0.6)

# Plusieurs cartes posées (glissement).
play = np.zeros(int(SR * 0.35))
for i, at in enumerate([0.0, 0.08, 0.15]):
    place(play, flick(0.1, 3000 + i * 400), at)
save('play', play, 0.6)

# Distribution : une rafale de cartes.
deal = np.zeros(int(SR * 0.9))
for i in range(9):
    place(deal, flick(0.06, 3200), i * 0.09 + rng.uniform(0, 0.02))
save('deal', deal, 0.5)

# Coup de poing sur la table (« Menteur ! »).
x = t(0.45)
thump = np.sin(2 * np.pi * (90 - 50 * x) * x) * env(len(x), 0.002, 0.12)
thump += lowpass(noise(0.45), 900) * env(len(x), 0.001, 0.05) * 0.8
save('slam', thump)

# Barillet qui tourne : cliquetis qui ralentissent.
spin = np.zeros(int(SR * 0.95))
at, gap = 0.0, 0.035
while at < 0.85:
    c = highpass(noise(0.012), 2500) * env(int(SR * 0.012), 0.0005, 0.003)
    place(spin, c, at)
    at += gap
    gap *= 1.09
save('spin', spin, 0.5)

# Chien qui claque sur une chambre vide.
x = t(0.25)
click = highpass(noise(0.25), 3000) * env(len(x), 0.0003, 0.008)
click += np.sin(2 * np.pi * 2400 * x) * env(len(x), 0.0003, 0.02) * 0.4
click += np.sin(2 * np.pi * 1350 * x) * env(len(x), 0.0005, 0.05) * 0.25
save('click', click, 0.8)

# Coup de feu : claquement, grondement grave, réverbération.
x = t(1.6)
crack = noise(1.6) * env(len(x), 0.0005, 0.03)
boom = np.sin(2 * np.pi * (70 - 30 * np.minimum(x, 1)) * x) * env(len(x), 0.001, 0.25)
tail = lowpass(noise(1.6), 1200) * env(len(x), 0.01, 0.45) * 0.35
bang = crack + boom * 0.9 + tail
# Échos de la salle.
for d, g in [(0.045, 0.35), (0.11, 0.2), (0.19, 0.12)]:
    s = int(d * SR)
    bang[s:] += bang[:-s] * g
save('bang', bang, 0.95)

# Petit carillon : c'est ton tour.
x = t(0.6)
ping = (np.sin(2 * np.pi * 880 * x) + 0.5 * np.sin(2 * np.pi * 1320 * x)) * env(len(x), 0.003, 0.18)
ping2 = np.zeros_like(ping)
place(ping2, (np.sin(2 * np.pi * 1175 * x) * env(len(x), 0.003, 0.2))[: int(SR * 0.45)], 0.12)
save('turn', ping + ping2, 0.45)

# Vérité : deux notes montantes ; mensonge : buzzer grave.
x = t(0.5)
truth = np.zeros_like(x)
for i, f in enumerate([660, 990]):
    place(truth, (np.sin(2 * np.pi * f * x) * env(len(x), 0.003, 0.15))[: int(SR * 0.35)], i * 0.12)
save('truth', truth, 0.5)
x = t(0.6)
saw = 2 * ((x * 110) % 1) - 1
lie = lowpass(saw, 1500) * env(len(x), 0.005, 0.35) * (1 + 0.3 * np.sin(2 * np.pi * 9 * x))
save('lie', lie, 0.55)

# Victoire : arpège ; défaite : notes descendantes.
win = np.zeros(int(SR * 1.4))
for i, f in enumerate([523, 659, 784, 1046]):
    x = t(0.7)
    place(win, (np.sin(2 * np.pi * f * x) + 0.3 * np.sin(4 * np.pi * f * x)) * env(len(x), 0.004, 0.3), i * 0.13)
save('win', win, 0.5)
lose = np.zeros(int(SR * 1.3))
for i, f in enumerate([392, 330, 262]):
    x = t(0.7)
    place(lose, np.sin(2 * np.pi * f * x) * env(len(x), 0.01, 0.35), i * 0.22)
save('lose', lose, 0.45)

# Ambiance de bar (boucle de 12 s) : brouhaha sourd et verres qui tintent.
dur = 12.0
amb = lowpass(noise(dur), 350) * 0.9
amb += lowpass(noise(dur), 700) * 0.3 * (1 + 0.5 * np.sin(2 * np.pi * 0.23 * t(dur)))
for _ in range(7):
    x = t(0.5)
    f = rng.uniform(2200, 3800)
    clink = np.sin(2 * np.pi * f * x) * env(len(x), 0.001, 0.08) * 0.25
    place(amb, clink, rng.uniform(0.2, dur - 0.6))
# Fondu aux extrémités pour une boucle sans clic.
fade = int(0.3 * SR)
amb[:fade] *= np.linspace(0, 1, fade)
amb[-fade:] *= np.linspace(1, 0, fade)
save('ambience', amb, 0.5)
print('ok')
