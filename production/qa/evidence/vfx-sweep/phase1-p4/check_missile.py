# P1.4 done-criteria over one missile / reach trace take (python check_missile.py <take>.log)
import sys

log = sys.argv[1]
rows = []
for raw in open(log, encoding="utf-8", errors="replace"):
    f = raw.rstrip("\n").split("\t")
    if len(f) >= 4 and f[0] == "present":
        rows.append((float(f[2]), f[3], f[4:]))


def kv(xs):
    return dict(x.split("=", 1) for x in xs if "=" in x)


starts = [(ph, rest[0], kv(rest)) for ph, k, rest in rows if k == "swing-start"]
releases = [(ph, kv(rest)) for ph, k, rest in rows if k == "swing-release"]
lands = [(ph, kv(rest)) for ph, k, rest in rows if k == "swing-land"]
contacts = [(ph, kv(rest)) for ph, k, rest in rows if k == "swing-contact"]
strands = [(ph, kv(rest)) for ph, k, rest in rows if k == "swing-strand"]
swing_strikes = {int(kv(r[2])["at"]) for r in rows if r[1] == "event" and r[2][0] == "Strike" and kv(r[2])["skill"] == "False"}
skill_ms = {int(kv(r[2])["at"]) for r in rows if r[1] == "event" and r[2][0] == "Skill"}
alloc = [int(kv(rest)["alloc"]) for ph, k, rest in rows if k == "swing-draw"]

print(log)
print(f"  swing-start={len(starts)} swing-release={len(releases)} swing-land={len(lands)} swing-contact={len(contacts)} swing-strand={len(strands)}")
# the release: the planned release is beat - travel, or the clip's start (a recorded clamp); it fires on the first frame at it
bad_release = 0
for ph, r in releases:
    beat, rel, flight, clamp = int(r["beat"]), float(r["release"]), float(r["flight"]), r["clamp"]
    planned = [s for s in starts if int(s[2]["beat"]) == beat]
    start = float(planned[-1][2]["start"]) if planned else float("nan")
    ok = abs(beat - rel - flight) < 1.0 and (clamp == "1" and abs(rel - start) < 1.0 or clamp == "0") and rel <= ph < rel + 17.0
    if not ok:
        bad_release += 1
        print(f"    BAD release {r}")
print(f"  release offsets (beat - release)={sorted({int(r['beat']) - int(round(float(r['release']))) for _, r in releases})}"
      f" clamps={sum(1 for _, r in releases if r['clamp'] == '1')} bad={bad_release}")
print(f"  flights per release={sorted({r['flights'] for _, r in releases})}")
# the contact: on a swing Strike's ms (the Strike ms), one per ms
bad_contact = [c for _, c in contacts if int(c["at"]) not in swing_strikes]
cms = [int(c["at"]) for _, c in contacts]
print(f"  contact-not-on-a-swing-strike={len(bad_contact)} duplicate-contact-ms={len(cms) - len(set(cms))} cues={sorted({c['cue'] for _, c in contacts})}")
# the land: on the frame that presents the beat
bad_land = [l for ph, l in lands if not (int(l["beat"]) <= ph < int(l["beat"]) + 17.0)]
print(f"  land-not-on-the-beat-frame={len(bad_land)}")
# the strand: taut on the frame that presents its beat
if strands:
    print(f"  strand taut={sum(1 for _, s in strands if s['taut'] == '1')}/{len(strands)} at-minus-beat={sorted({int(ph - float(s['beat'])) for ph, s in strands})}")
# no strip projectile / strike stamp on a swing: every such spawn is on a Skill ms, never on a swing's release or contact ms
swing_ms = {int(round(ph)) for ph, _ in releases} | {int(round(ph)) for ph, _ in contacts} | {int(round(ph)) for ph, _ in lands}
stamps = [(ph, rest[0]) for ph, k, rest in rows if k == "vfx-spawn" and ("_projectile" in rest[0] or "_strike" in rest[0])]
on_swing = [s for s in stamps if int(round(s[0])) in swing_ms and not any(abs(s[0] - m) < 17 for m in skill_ms)]
print(f"  strip projectile/strike spawns={len(stamps)} (all on Skill ms: {all(any(abs(p - m) < 17 for m in skill_ms) for p, _ in stamps)}) on-a-swing={len(on_swing)}")
print(f"  swing-draw frames={len(alloc)} alloc>0={sum(1 for a in alloc if a)}")
# the legacy push never fires for a performed swing (the root line stays 0 at every swing contact)
