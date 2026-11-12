"""Génère l'icône de lancement Android (python3 tool/make_icon.py)."""
import math
from PIL import Image, ImageDraw

S = 1024

def icon():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # Fond feutre vert arrondi
    d.rounded_rectangle([0, 0, S, S], radius=220, fill=(24, 70, 52))
    d.rounded_rectangle([40, 40, S - 40, S - 40], radius=190, outline=(217, 174, 82), width=18)
    # Barillet
    cx, cy, r = S / 2, S / 2, 330
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(70, 75, 82))
    for i in range(6):
        a = (i + 0.5) * math.pi / 3 - math.pi / 2
        gx, gy = cx + math.cos(a) * r * 1.02, cy + math.sin(a) * r * 1.02
        g = r * 0.14
        d.ellipse([gx - g, gy - g, gx + g, gy + g], fill=(24, 70, 52))
    for i in range(6):
        a = i * math.pi / 3 - math.pi / 2
        x, y = cx + math.cos(a) * r * 0.55, cy + math.sin(a) * r * 0.55
        o, b = r * 0.24, r * 0.19
        d.ellipse([x - o, y - o, x + o, y + o], fill=(17, 18, 20))
        if i == 0:
            d.ellipse([x - b, y - b, x + b, y + b], fill=(192, 57, 43))
        else:
            d.ellipse([x - b, y - b, x + b, y + b], fill=(176, 141, 87))
    c = r * 0.15
    d.ellipse([cx - c, cy - c, cx + c, cy + c], fill=(46, 50, 54))
    return img

base = icon()
sizes = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
for name, px in sizes.items():
    base.resize((px, px), Image.LANCZOS).save(
        f"android/app/src/main/res/mipmap-{name}/ic_launcher.png")
base.resize((512, 512), Image.LANCZOS).save("docs/icon.png")
