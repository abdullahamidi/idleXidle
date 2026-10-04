import sys
def load(p):
    d={}
    for l in open(p,encoding='utf-8',errors='replace'):
        f=l.rstrip('\n').split('\t')
        if len(f)>4 and f[0]=='present' and f[3] in('enemy-root','enemy-home'):
            d.setdefault(f[2],set()).add(f[5])
    return d
a,b=load(sys.argv[1]),load(sys.argv[2])
both=[k for k in a if k in b]
bad=[k for k in both if a[k]!=b[k]]
print(f"ms both={len(both)} differing={len(bad)}", bad[:5], [ (a[k],b[k]) for k in bad[:3]])
