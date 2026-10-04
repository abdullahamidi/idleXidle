# P1.4 evidence: a 2x2 sheet of arena crops from a take's frames at four playheads (ms), saved as a small JPG.
#   python stills.py <take dir> <take name> <out.jpg> <ms> <ms> <ms> <ms>
import sys

from PIL import Image

folder, name, out = sys.argv[1], sys.argv[2], sys.argv[3]
wants = [float(x) for x in sys.argv[4:8]]
shots = []
for raw in open(f"{folder}/{name}.log", encoding="utf-8", errors="replace"):
    f = raw.rstrip("\n").split("\t")
    if len(f) >= 5 and f[0] == "present" and f[3] == "shot":
        shots.append((float(f[2]), int(f[4])))
picked = []
for w in wants:
    ph, idx = min(shots, key=lambda s: abs(s[0] - w))
    picked.append((ph, idx))
crops = []
for ph, idx in picked:
    im = Image.open(f"{folder}/{name}_{idx:02d}.png" if idx < 100 else f"{folder}/{name}_{idx:03d}.png").convert("RGB")
    w, h = im.size
    crops.append(im.crop((int(w * 0.30), int(h * 0.38), int(w * 0.80), int(h * 0.80))))
cw, ch = crops[0].size
sheet = Image.new("RGB", (cw * 2, ch * 2))
for i, c in enumerate(crops):
    sheet.paste(c, ((i % 2) * cw, (i // 2) * ch))
sheet = sheet.resize((sheet.width * 2 // 3, sheet.height * 2 // 3))
sheet.save(out, quality=80)
print(out, [int(p) for p, _ in picked])
