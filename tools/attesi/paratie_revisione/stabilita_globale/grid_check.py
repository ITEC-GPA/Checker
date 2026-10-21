"""Port of EmbeddedWallEmbedment.Prepare depth grid (EmbeddedWallEmbedment.cs:130-142) with metre inputs scaled by 1000 as ANTHEA does."""
import math
def depths(mn, mx, step):
    div = math.floor((mx - mn) / step); out = []
    for i in range(int(div) + 1):
        v = mn + i * step
        if v < mx: out.append(v)
    out.append(mx); return out
hits = []
for a in range(5, 60):
    for b in range(a + 1, 160):
        for s in (0.1, 0.2, 0.25, 0.5, 1.0):
            mn, mx, st = a / 10 * 1000, b / 10 * 1000, s * 1000
            d = depths(mn, mx, st)
            gaps = [d[i+1] - d[i] for i in range(len(d) - 1)]
            if min(gaps) < 1e-6: hits.append((a / 10, b / 10, s, d[-2], d[-1]))
print(len(hits), hits[:5])
