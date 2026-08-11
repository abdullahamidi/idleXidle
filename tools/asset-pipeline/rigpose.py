"""Render the assembled rig at an arbitrary pose. Shared by the verification shots."""
import math, re, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelpng import Image, write
from derive import scale_nearest
import scene_preview as sp

R = '/mnt/c/Users/Admin/Desktop/idleXidle/'
sp._INDEX = None
_rig = open(R + 'src/ResonanceHunter.Game/HunterRig.cs').read()
_pat = re.compile(r'new HunterPart\("(\w+)",\s*(null|"\w+"),\s*"(\w+)",\s*new Vec2\(([-\d]+),\s*([-\d]+)\),\s*new Vec2\(([-\d]+),\s*([-\d]+)\),\s*(\d+)\)')
PARTS = [dict(id=m[0], parent=None if m[1] == 'null' else m[1].strip('"'), tex=m[2],
              off=(int(m[3]), int(m[4])), piv=(int(m[5]), int(m[6])), order=int(m[7]))
         for m in (x.groups() for x in _pat.finditer(_rig))]
_rnd = open(R + 'src/ResonanceHunter.Game/HunterRigRenderer.cs').read()
_gb = re.compile(r'\[GearSlot\.(\w+)\] = new\("(\w+)", ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, (\d+)(?:, ([-\d.]+)f)?\)')
GEAR = {m[0].lower(): (m[1], float(m[2]), float(m[3]), float(m[4]), int(m[5]), float(m[6] or 0))
        for m in (x.groups() for x in _gb.finditer(_rnd))}
MIRROR = {'boots': 'foot_off', 'gloves': 'hand_off'}
_BY = {p['id']: p for p in PARTS}
TEX = {p['id']: sp.load(p['tex']) for p in PARTS}

def solve(angles):
    pos, ang = {}, {}
    def rec(p):
        if p['id'] in pos: return
        a = angles.get(p['id'], 0.0)
        if p['parent'] is None:
            pos[p['id']] = (0.0, 0.0); ang[p['id']] = a
        else:
            pa = _BY[p['parent']]; rec(pa); th = ang[pa['id']]; ox, oy = p['off']
            pos[p['id']] = (pos[pa['id']][0] + ox*math.cos(th) - oy*math.sin(th),
                            pos[pa['id']][1] + ox*math.sin(th) + oy*math.cos(th))
            ang[p['id']] = th + a
    for p in PARTS: rec(p)
    return pos, ang

def rot(im, a):
    if abs(a) < 0.01: return im, (0, 0)
    ca, sa = math.cos(a), math.sin(a); w, h = im.w, im.h
    nw = int(abs(w*ca) + abs(h*sa)) + 2; nh = int(abs(w*sa) + abs(h*ca)) + 2
    o = Image(nw, nh)
    for y in range(nh):
        for x in range(nw):
            dx, dy = x - nw/2, y - nh/2
            sx = int(dx*ca + dy*sa + w/2); sy = int(-dx*sa + dy*ca + h/2)
            if 0 <= sx < w and 0 <= sy < h:
                q = (sy*w + sx)*4
                if im.px[q+3]:
                    d = (y*nw + x)*4; o.px[d:d+4] = im.px[q:q+4]
    return o, ((nw-w)/2, (nh-h)/2)

def render(angles=None, trait=None, height=400, pad=60):
    pos, ang = solve(angles or {})
    mnx = mny = 10**9; mxx = mxy = -10**9
    for p in PARTS:
        t = TEX[p['id']]; w, h = (t.w, t.h) if t else (30, 30)
        x, y = pos[p['id']]; l, tp = x - p['piv'][0], y - p['piv'][1]
        mnx = min(mnx, l); mny = min(mny, tp); mxx = max(mxx, l+w); mxy = max(mxy, tp+h)
    W, H = mxx-mnx, mxy-mny; S = height/max(W, H)
    out = Image(int(W*S)+pad*2, int(H*S)+30)
    for i in range(out.w*out.h): out.px[i*4:i*4+4] = bytes((0x24, 0x20, 0x2C, 255))
    items = [(p['order'], TEX[p['id']], pos[p['id']], p['piv'], 1.0, ang[p['id']]) for p in PARTS if TEX[p['id']]]
    if trait:
        for slot, (bone, pu, pv, gh, order, rr) in GEAR.items():
            t = sp.load(f'gear_{slot}_{trait}')
            if not t: continue
            for bn in [bone] + ([MIRROR[slot]] if slot in MIRROR else []):
                if bn in pos: items.append((order, t, pos[bn], (t.w*pu, t.h*pv), gh/t.h, ang[bn]+rr))
    for _o, t, bp, piv, sc, aa in sorted(items, key=lambda e: e[0]):
        sm = scale_nearest(t, max(1, int(t.w*S*sc)), max(1, int(t.h*S*sc)))
        px, py = piv[0]*sc*S, piv[1]*sc*S
        if aa: sm, (ox, oy) = rot(sm, aa); px += ox; py += oy
        sp.blit(out, sm, int((bp[0]-mnx)*S+pad-px), int((bp[1]-mny)*S+15-py))
    return out

def strip(frames, path):
    out = Image(sum(f.w for f in frames) + 6*len(frames), max(f.h for f in frames))
    for i in range(out.w*out.h): out.px[i*4:i*4+4] = bytes((0x0A, 0x08, 0x0C, 255))
    x = 0
    for f in frames: sp.blit(out, f, x, 0); x += f.w + 6
    write(path, out)
