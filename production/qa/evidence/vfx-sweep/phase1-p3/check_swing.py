# P1.3 done-criteria over one melee trace take
import sys
log = sys.argv[1]
rows = []
for raw in open(log, encoding="utf-8", errors="replace"):
    f = raw.rstrip("\n").split("\t")
    if len(f) >= 4 and f[0] == "present":
        rows.append((float(f[2]), f[3], f[4:]))
kv = lambda xs: dict(x.split("=", 1) for x in xs if "=" in x)
swing_strikes = [int(kv(r[2])["at"]) for r in rows if r[1] == "event" and r[2][0] == "Strike" and kv(r[2])["skill"] == "False"]
contacts = [int(kv(r[2])["at"]) for r in rows if r[1] == "swing-contact"]
starts = [int(kv(r[2])["beat"]) for r in rows if r[1] == "swing-start"]
bad_contact = [c for c in contacts if c not in swing_strikes]
# swing-step at every attack clip-end: the last traced step before (or at) it is 0
last_step, step_bad, ends = 0, 0, 0
for ph, k, rest in rows:
    if k == "swing-step": last_step = int(kv(rest)["x"])
    if k == "clip-end" and rest and rest[0] == "attack":
        ends += 1
        if last_step != 0: step_bad += 1
# generic thud / puff on a swing ms: the batch lines share the contact's playhead
contact_ph = {ph for ph, k, rest in rows if k == "swing-contact"}
generic = sum(1 for ph, k, rest in rows if ph in contact_ph and k == "sound" and rest[0] == "sfx_hit" and "vol=0.38" in rest)
puff = sum(1 for ph, k, rest in rows if ph in contact_ph and k == "vfx-spawn" and any("ImpactWeak" in x or "weakhit" in x.lower() for x in rest))
cues = [rest for ph, k, rest in rows if k == "swing-contact"]
alloc = [int(kv(rest)["alloc"]) for ph, k, rest in rows if k == "swing-draw"]
outl = [(ph, kv(rest)) for ph, k, rest in rows if k == "number" and "outline" in kv(rest)]
print(f"{log}")
print(f"  swing strikes={len(swing_strikes)} swing-start={len(starts)} swing-contact={len(contacts)} contact-not-on-a-swing-strike={len(bad_contact)} duplicate-contact-ms={len(contacts)-len(set(contacts))}")
print(f"  attack clip-ends={ends} with a non-zero step={step_bad}")
print(f"  generic sfx_hit 0.38 on a swing ms={generic}  ImpactWeak on a swing ms={puff}")
print(f"  cues={sorted(set(c[4] for c in cues))}  swing-draw frames={len(alloc)} alloc>0={sum(1 for a in alloc if a)}")
print(f"  outlined numbers={[(int(p), o['slot'], o['outline']) for p, o in outl]}")
