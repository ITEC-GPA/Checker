"""Independent port of EmbeddedWallSeepageMesh + saturated P1 Galerkin (direct solve).
Used to check the boundary-condition classification and the physics against closed-form
sheet-pile solutions (Harr 1962). Units mm; k arbitrary (saturated, linear)."""
import math
import numpy as np

GW = 9.81e-6  # N/mm3


def subdivide(vals, step):
    s = sorted(set(vals)); out = [s[0]]
    for a, b in zip(s, s[1:]):
        n = math.ceil((b - a) / step)
        for j in range(1, n):
            out.append(a + (b - a) * j / n)
        out.append(b)
    return out


def mesh(LE, RE, bottom, wtop, wbot, t, lg, rg, lh, rh, step, layer_z=()):
    half = t / 2
    xs = subdivide([-LE, -half, 0, half, RE], step)
    mg = max(lg, rg)
    zs = subdivide([z for z in [bottom, wbot, lg, rg, lh, rh, *layer_z] if bottom <= z <= mg], step)
    idx = {}; nodes = []; tris = []

    def node(i, j):
        if (i, j) not in idx:
            idx[(i, j)] = len(nodes); nodes.append((xs[i], zs[j]))
        return idx[(i, j)]
    for j in range(len(zs) - 1):
        for i in range(len(xs) - 1):
            x = (xs[i] + xs[i + 1]) / 2; z = (zs[j] + zs[j + 1]) / 2
            if (abs(x) < half and z > wbot) or z > (lg if x < 0 else rg):
                continue
            a, b, c, e = node(i, j), node(i + 1, j), node(i, j + 1), node(i + 1, j + 1)
            if (i + j) % 2 == 0:
                tris += [(a, b, e), (a, e, c)]
            else:
                tris += [(a, b, c), (b, e, c)]
    heads = {}
    faces = []
    for k, (x, z) in enumerate(nodes):
        left = x < 0
        g = lg if left else rg; w = lh if left else rh
        if x == -LE or x == RE:
            heads[k] = w
        elif abs(x) >= half and z == g:
            if w >= g:
                heads[k] = w
            else:
                faces.append(k)
    return np.array(nodes), tris, heads, faces


class Sparse:
    def __init__(self, n, rows, cols, vals):
        self.n, self.r, self.c, self.v = n, np.array(rows), np.array(cols), np.array(vals)
        self.diag = np.zeros(n); np.add.at(self.diag, self.r[self.r == self.c], self.v[self.r == self.c])

    def __matmul__(self, x):
        y = np.zeros(self.n); np.add.at(y, self.r, self.v * x[self.c]); return y


def solve(nodes, tris, heads, kxx=1.0, kzz=1.0, kxz=0.0, tol=1e-13):
    n = len(nodes); rows, cols, vals = [], [], []
    for (a, b, c) in tris:
        (xa, za), (xb, zb), (xc, zc) = nodes[a], nodes[b], nodes[c]
        cr = (xb - xa) * (zc - za) - (xc - xa) * (zb - za)
        dx = np.array([zb - zc, zc - za, za - zb]) / cr
        dz = np.array([xc - xb, xa - xc, xb - xa]) / cr
        A = abs(cr) / 2
        ke = A * (np.outer(dx, kxx * dx + kxz * dz) + np.outer(dz, kxz * dx + kzz * dz))
        ids = [a, b, c]
        for i in range(3):
            for j in range(3):
                rows.append(ids[i]); cols.append(ids[j]); vals.append(ke[i, j])
    K = Sparse(n, rows, cols, vals)
    fixed = np.zeros(n, bool); h = np.zeros(n)
    for k, v in heads.items():
        fixed[k] = True; h[k] = v
    free = ~fixed
    # Jacobi-preconditioned CG on the free block
    r = -(K @ h); r[fixed] = 0
    x = np.zeros(n); z = r / K.diag; p = z.copy(); rz = r @ z; nb = math.sqrt(r @ r)
    for it in range(200000):
        ap = K @ p; ap[fixed] = 0
        al = rz / (p @ ap); x += al * p; r -= al * ap
        if math.sqrt(r @ r) <= tol * nb:
            break
        z = r / K.diag; z[fixed] = 0; nz = r @ z; p = z + nz / rz * p; rz = nz
    solve.iterations = it + 1
    h = h + x
    react = K @ h  # inflow positive at fixed nodes
    return h, react, K


def head_at(nodes, tris, h, x, z):
    for (a, b, c) in tris:
        (xa, za), (xb, zb), (xc, zc) = nodes[a], nodes[b], nodes[c]
        cr = (xb - xa) * (zc - za) - (xc - xa) * (zb - za)
        v = ((x - xa) * (zc - za) - (xc - xa) * (z - za)) / cr
        w = ((xb - xa) * (z - za) - (x - xa) * (zb - za)) / cr
        if v >= -1e-9 and w >= -1e-9 and v + w <= 1 + 1e-9:
            return h[a] + v * (h[b] - h[a]) + w * (h[c] - h[a])
    raise ValueError('outside')


def line_integral_u(nodes, tris, h, x0, x1, z, xs_nodes):
    """Exact integral of u=gw(h-z) along horizontal mesh line z from x0 to x1 (MPa*mm)."""
    pts = sorted(set([x0, x1] + [x for x in xs_nodes if x0 < x < x1]))
    u = [GW * (head_at(nodes, tris, h, x, z) - z) for x in pts]
    return sum((u[i] + u[i + 1]) / 2 * (pts[i + 1] - pts[i]) for i in range(len(pts) - 1))
