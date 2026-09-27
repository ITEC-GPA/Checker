using MathNet.Numerics.LinearAlgebra;

namespace GPC.Checkers.CompositeBridge;

/// <summary>
/// The single closed cell of a box girder for St. Venant torsion (Bredt): the corners are on the mid-planes of the walls, the top wall is
/// horizontal. Lengths in mm; the thicknesses are in the units of the reference material (the concrete slab reduced by the modular ratio)
/// </summary>
/// <param name="TopWidth">The width of the top wall between the top corners</param>
/// <param name="BottomWidth">The width of the bottom wall between the bottom corners</param>
/// <param name="Height">The vertical distance between the top and the bottom walls</param>
/// <param name="TopThickness">The thickness of the top wall (slab or equivalent bracing)</param>
/// <param name="WebThickness">The thickness of each web</param>
/// <param name="BottomThickness">The thickness of the bottom wall</param>
public sealed record BoxCell(double TopWidth, double BottomWidth, double Height, double TopThickness, double WebThickness, double BottomThickness)
{
    /// <summary>The area A0 enclosed by the mid-lines of the walls</summary>
    public double Area => (TopWidth + BottomWidth) / 2 * Height;
    /// <summary>The length of each web between the corners</summary>
    public double WebLength => Math.Sqrt(Math.Pow((TopWidth - BottomWidth) / 2, 2) + Height * Height);
    /// <summary>The St. Venant torsion constant of the closed cell, J = 4 A0² / Σ(ℓ/t)</summary>
    public double TorsionConstant => 4 * Area * Area / (TopWidth / TopThickness + 2 * WebLength / WebThickness + BottomWidth / BottomThickness);
    /// <summary>The shear flow q = T / (2 A0) of a torque T (N mm → N/mm)</summary>
    public double ShearFlow(double torque) => torque / (2 * Area);
}

/// <summary>
/// The cross-section of a box girder for the distortion: the cell (corners top left, top right, bottom right, bottom left) with the walls
/// hinged at the corners for the mechanism and rigidly connected for the transverse frame, the outstands collinear with the top and the bottom
/// walls (slab cantilevers, bottom flange beyond the webs) and the areas lumped at the top corners (top flanges). The warping thicknesses and
/// areas are in the units of the reference material (the slab divided by the modular ratio); the rigidities are the plate flexural rigidities
/// D = E t³ / (12 (1 − ν²)) per unit length [N mm]
/// </summary>
public sealed record BoxDistortionSection(double TopWidth, double BottomWidth, double Height,
    double TopThickness, double WebThickness, double BottomThickness,
    double TopRigidity, double WebRigidity, double BottomRigidity,
    double TopOutstand = 0, double BottomOutstand = 0, double TopCornerArea = 0, double BottomCornerArea = 0);

/// <summary>
/// The distortion mode of a single cell normalised with the mean absolute change of the corner angles (for the rectangle the angle change γ of
/// every corner). Corners: 0 top left, 1 top right, 2 bottom right, 3 bottom left
/// </summary>
/// <param name="X">The x of the corners</param>
/// <param name="Y">The y of the corners</param>
/// <param name="U">The horizontal displacements of the corners</param>
/// <param name="V">The vertical displacements of the corners</param>
/// <param name="Warping">The warping ω at the corners [mm²], orthogonal to the axial and bending warpings</param>
/// <param name="OutstandWarping">The warping at the free ends of the outstands: top left, top right, bottom right, bottom left</param>
/// <param name="WarpingInertia">I_Dw = ∫ ω² t ds [mm⁶]</param>
/// <param name="FrameStiffness">The transverse frame stiffness K per unit length (strain energy ½ K γ²) [N]</param>
/// <param name="CornerMoments">The transverse bending moments at the corners per unit length and per unit γ [N mm/mm]</param>
/// <param name="TorqueLoad">The generalised load of a unit torque applied as a vertical couple at the top corners [1]</param>
/// <param name="AngleChanges">The changes of the corner angles</param>
public sealed record BoxDistortionMode(double[] X, double[] Y, double[] U, double[] V, double[] Warping, double[] OutstandWarping,
    double WarpingInertia, double FrameStiffness, double[] CornerMoments, double TorqueLoad, double[] AngleChanges);

/// <summary>The envelope of the distortion along the span (beam on elastic foundation)</summary>
/// <param name="MaxMoment">max |E I_Dw ψ''| [N mm²]</param>
/// <param name="MaxAmplitude">max |ψ| (the distortion angle)</param>
/// <param name="MaxDiaphragmAmplitude">max |ψ| at the intermediate diaphragms</param>
/// <param name="Diaphragms">The number of intermediate diaphragms</param>
/// <param name="Nodes">The number of nodes of the model</param>
public sealed record BoxDistortionEnvelope(double MaxMoment, double MaxAmplitude, double MaxDiaphragmAmplitude, int Diaphragms, int Nodes);

/// <summary>
/// Distortion of a single-cell box girder with the analogy of the beam on elastic foundation (Wright, Abdel-Samad, Robinson, ASCE 1968):
/// E I_Dw ψ'''' + K ψ = m_d, with the intermediate diaphragms as springs K_D. The distortion mode is the mechanism of the four walls with zero
/// membrane shear strain in every wall (Σ ℓ V = 0 around the cell, so the St. Venant shear flows do no work on it); its warping is linear on every
/// wall and orthogonal to the axial and bending warpings (N = Mx = My = 0). The coupling with the torsional warping is neglected, as in the analogy.
/// </summary>
public static class BoxDistortion
{
    private const double Tolerance = 1e-12;

    /// <summary>The distortion mode, the warping inertia, the frame stiffness and the load factor of a torque</summary>
    /// <param name="s">The cross-section</param>
    /// <returns>The mode normalised with the mean absolute change of the corner angles</returns>
    public static BoxDistortionMode Mode(BoxDistortionSection s)
    {
        foreach (var v in new[] { s.TopWidth, s.BottomWidth, s.Height, s.TopThickness, s.WebThickness, s.BottomThickness, s.TopRigidity, s.WebRigidity, s.BottomRigidity })
            BridgeNumbers.Require(v, "cella", strict: true);
        foreach (var v in new[] { s.TopOutstand, s.BottomOutstand, s.TopCornerArea, s.BottomCornerArea }) BridgeNumbers.Require(v, "cella");
        double[] x = [-s.TopWidth / 2, s.TopWidth / 2, s.BottomWidth / 2, -s.BottomWidth / 2], y = [s.Height, s.Height, 0, 0];
        // walls: 0 top (0→1), 1 right web (1→2), 2 bottom (2→3), 3 left web (3→0)
        int[] start = [0, 1, 2, 3], end = [1, 2, 3, 0];
        double[] length = new double[4], ex = new double[4], ey = new double[4];
        for (int i = 0; i < 4; i++)
        {
            double dx = x[end[i]] - x[start[i]], dy = y[end[i]] - y[start[i]];
            length[i] = Math.Sqrt(dx * dx + dy * dy); ex[i] = dx / length[i]; ey[i] = dy / length[i];
        }
        // mechanism with the bottom wall fixed: the top corners move normal to their webs, the top wall does not change length
        double n1x = -ey[1], n1y = ex[1], n3x = -ey[3], n3y = ex[3];
        double ratio = (ex[0] * n1x + ey[0] * n1y) / (ex[0] * n3x + ey[0] * n3y);
        var mechanism = new double[8];
        mechanism[2] = n1x; mechanism[3] = n1y; mechanism[0] = ratio * n3x; mechanism[1] = ratio * n3y;
        var rotation = new double[8];
        for (int k = 0; k < 4; k++) { rotation[2 * k] = -y[k]; rotation[2 * k + 1] = x[k]; }
        double Loop(double[] d) => Enumerable.Range(0, 4).Sum(i => length[i] * (ex[i] * d[2 * start[i]] + ey[i] * d[2 * start[i] + 1]));
        // the rigid rotation removes the circulation of the tangential displacements (zero membrane shear strain)
        double factor = Loop(mechanism) / Loop(rotation);
        var d0 = mechanism.Select((m, i) => m - factor * rotation[i]).ToArray();
        // warping of the displacement field: ω(TL) = 0, dω/ds = tangential displacement of the wall
        double[] Walk(double[] d)
        {
            var w = new double[4];
            for (int i = 0; i < 3; i++) w[end[i]] = w[start[i]] + length[i] * (ex[i] * d[2 * start[i]] + ey[i] * d[2 * start[i] + 1]);
            return w;
        }
        // outstands: collinear with the top wall at the top corners and with the bottom wall at the bottom corners
        double[] Outstands(double[] w, double[] d) => [
            w[0] - s.TopOutstand * (ex[0] * d[0] + ey[0] * d[1]), w[1] + s.TopOutstand * (ex[0] * d[2] + ey[0] * d[3]),
            w[2] - s.BottomOutstand * (ex[2] * d[4] + ey[2] * d[5]), w[3] + s.BottomOutstand * (ex[2] * d[6] + ey[2] * d[7])];
        double[] ox = [x[0] - s.TopOutstand, x[1] + s.TopOutstand, x[2] + s.BottomOutstand, x[3] - s.BottomOutstand];
        double[] oy = [y[0], y[1], y[2], y[3]];
        double[] thickness = [s.TopThickness, s.WebThickness, s.BottomThickness, s.WebThickness];
        // weighted inner product of fields linear on every segment, given by the corner and outstand values
        double Product(double[] fc, double[] fo, double[] gc, double[] go)
        {
            double Segment(double f1, double f2, double g1, double g2, double l) => l / 6 * (2 * f1 * g1 + f1 * g2 + f2 * g1 + 2 * f2 * g2);
            double sum = 0;
            for (int i = 0; i < 4; i++) sum += thickness[i] * Segment(fc[start[i]], fc[end[i]], gc[start[i]], gc[end[i]], length[i]);
            for (int k = 0; k < 4; k++)
            {
                double l = k < 2 ? s.TopOutstand : s.BottomOutstand, t = k < 2 ? s.TopThickness : s.BottomThickness;
                if (l > 0) sum += t * Segment(fc[k], fo[k], gc[k], go[k], l);
                sum += (k < 2 ? s.TopCornerArea : s.BottomCornerArea) * fc[k] * gc[k];
            }
            return sum;
        }
        double[] one = [1, 1, 1, 1];
        var fields = new[] { (one, one), (x, ox), (y, oy) };
        var w0 = Walk(d0); var o0 = Outstands(w0, d0);
        var gram = Matrix<double>.Build.Dense(3, 3, (i, j) => Product(fields[i].Item1, fields[i].Item2, fields[j].Item1, fields[j].Item2));
        var rhs = Vector<double>.Build.Dense(3, i => Product(fields[i].Item1, fields[i].Item2, w0, o0));
        var c = gram.Solve(rhs);
        // ω = ω0 − c0 − c1 x − c2 y; the in-plane field loses the translations c1 (horizontal) and c2 (vertical)
        var d = d0.Select((v, i) => v - (i % 2 == 0 ? c[1] : c[2])).ToArray();
        var w = Enumerable.Range(0, 4).Select(k => w0[k] - c[0] - c[1] * x[k] - c[2] * y[k]).ToArray();
        var o = Enumerable.Range(0, 4).Select(k => o0[k] - c[0] - c[1] * ox[k] - c[2] * oy[k]).ToArray();
        // chord rotations (counter-clockwise) and corner angle changes
        var chord = Enumerable.Range(0, 4).Select(i => (ex[i] * (d[2 * end[i] + 1] - d[2 * start[i] + 1]) - ey[i] * (d[2 * end[i]] - d[2 * start[i]])) / length[i]).ToArray();
        var angles = Enumerable.Range(0, 4).Select(k => chord[k] - chord[(k + 3) % 4]).ToArray();
        double scale = angles.Average(Math.Abs);
        if (scale < Tolerance) throw new InvalidOperationException("Modo distorsivo della cella non determinato.");
        d = d.Select(v => v / scale).ToArray(); w = w.Select(v => v / scale).ToArray(); o = o.Select(v => v / scale).ToArray();
        chord = chord.Select(v => v / scale).ToArray(); angles = angles.Select(v => v / scale).ToArray();
        double inertia = Product(w, o, w, o);
        // transverse frame with rigid corners (slope-deflection per unit length): joint rotations θ, end moments, strain energy
        double[] rigidity = [s.TopRigidity, s.WebRigidity, s.BottomRigidity, s.WebRigidity];
        var k4 = Matrix<double>.Build.Dense(4, 4); var f4 = Vector<double>.Build.Dense(4);
        for (int i = 0; i < 4; i++)
        {
            double a = 2 * rigidity[i] / length[i];
            k4[start[i], start[i]] += 2 * a; k4[end[i], end[i]] += 2 * a; k4[start[i], end[i]] += a; k4[end[i], start[i]] += a;
            f4[start[i]] += 3 * a * chord[i]; f4[end[i]] += 3 * a * chord[i];
        }
        var theta = k4.Solve(f4);
        double energy = 0; var moments = new double[4];
        for (int i = 0; i < 4; i++)
        {
            double a = 2 * rigidity[i] / length[i], ma = a * (2 * theta[start[i]] + theta[end[i]] - 3 * chord[i]), mb = a * (2 * theta[end[i]] + theta[start[i]] - 3 * chord[i]);
            energy += (ma * (theta[start[i]] - chord[i]) + mb * (theta[end[i]] - chord[i])) / 2;
            moments[start[i]] = Math.Max(moments[start[i]], Math.Abs(ma)); moments[end[i]] = Math.Max(moments[end[i]], Math.Abs(mb));
        }
        // a torque T as a vertical couple T / b at the top corners: its work on the mode
        double torqueLoad = (d[3] - d[1]) / s.TopWidth;
        return new(x, y, Enumerable.Range(0, 4).Select(k => d[2 * k]).ToArray(), Enumerable.Range(0, 4).Select(k => d[2 * k + 1]).ToArray(),
            w, o, inertia, 2 * energy, moments, torqueLoad, angles);
    }

    /// <summary>
    /// The stiffness K_D of a plate diaphragm spanning the cell. The plate is connected along its edges to the walls, which move rigidly in the
    /// mode: the edge displacements are linear between the corners; the interior is free (plane stress, mesh of bilinear elements)
    /// </summary>
    /// <param name="mode">The distortion mode</param>
    /// <param name="thickness">The thickness of the plate</param>
    /// <param name="young">E</param>
    /// <param name="poisson">ν</param>
    /// <param name="divisions">The elements along each side of the plate</param>
    /// <returns>K_D [N mm] (strain energy ½ K_D γ²)</returns>
    public static double PlateDiaphragmStiffness(BoxDistortionMode mode, double thickness, double young, double poisson = .3, int divisions = 12) =>
        new DiaphragmPlate(mode, thickness, young, poisson, divisions).Stiffness;

    /// <summary>The stresses of a plate diaphragm at the distortion amplitude γ</summary>
    /// <returns>The shear stress τxy of the panel (the mean of |τxy|, not less than half its maximum, EN 1993-1-5 §7.1(5)) and the maximum
    /// von Mises stress at the Gauss points [MPa]</returns>
    public static (double Shear, double VonMises) PlateDiaphragmStresses(BoxDistortionMode mode, double thickness, double young, double amplitude, double poisson = .3)
    {
        var plate = new DiaphragmPlate(mode, thickness, young, poisson);
        return (plate.Shear * Math.Abs(amplitude), plate.VonMises * Math.Abs(amplitude));
    }

    /// <summary>The plate diaphragm with the edges moved by the mode: n × n bilinear elements in the natural coordinates of the cell</summary>
    private sealed class DiaphragmPlate
    {
        public double Stiffness { get; }
        public double Shear { get; }
        public double VonMises { get; }

        public DiaphragmPlate(BoxDistortionMode mode, double thickness, double young, double poisson, int divisions = 12)
        {
            BridgeNumbers.Require(thickness, "t_diaframma", strict: true); BridgeNumbers.Require(young, "E", strict: true);
            // counter-clockwise corners: bottom left, bottom right, top right, top left
            int[] corner = [3, 2, 1, 0];
            double[] cx = corner.Select(k => mode.X[k]).ToArray(), cy = corner.Select(k => mode.Y[k]).ToArray();
            double[] cu = corner.Select(k => mode.U[k]).ToArray(), cv = corner.Select(k => mode.V[k]).ToArray();
            double[] nr = [-1, 1, 1, -1], ns = [-1, -1, 1, 1];
            double Map(double[] c, double r, double s) => Enumerable.Range(0, 4).Sum(i => c[i] * (1 + nr[i] * r) * (1 + ns[i] * s) / 4);
            int n = divisions, side = n + 1, dofs = 2 * side * side;
            int Node(int i, int j) => j * side + i;
            double[] x = new double[side * side], y = new double[side * side];
            var prescribed = new double?[dofs];
            for (int j = 0; j <= n; j++) for (int i = 0; i <= n; i++)
            {
                double r = -1 + 2.0 * i / n, s = -1 + 2.0 * j / n; int p = Node(i, j);
                x[p] = Map(cx, r, s); y[p] = Map(cy, r, s);
                // the edges follow the walls: the bilinear interpolation of the corners is linear along them
                if (i == 0 || j == 0 || i == n || j == n) { prescribed[2 * p] = Map(cu, r, s); prescribed[2 * p + 1] = Map(cv, r, s); }
            }
            double f = young / (1 - poisson * poisson);
            var e = Matrix<double>.Build.DenseOfArray(new[,] { { f, f * poisson, 0 }, { f * poisson, f, 0 }, { 0, 0, f * (1 - poisson) / 2 } });
            double g = 1 / Math.Sqrt(3);
            var gauss = new[] { (-g, -g), (g, -g), (g, g), (-g, g) };
            (Matrix<double> B, double Det) Strain(int[] nodes, double r, double s)
            {
                var dr = Enumerable.Range(0, 4).Select(i => nr[i] * (1 + ns[i] * s) / 4).ToArray();
                var ds = Enumerable.Range(0, 4).Select(i => ns[i] * (1 + nr[i] * r) / 4).ToArray();
                double j11 = 0, j12 = 0, j21 = 0, j22 = 0;
                for (int i = 0; i < 4; i++) { j11 += dr[i] * x[nodes[i]]; j12 += dr[i] * y[nodes[i]]; j21 += ds[i] * x[nodes[i]]; j22 += ds[i] * y[nodes[i]]; }
                double det = j11 * j22 - j12 * j21;
                var b = Matrix<double>.Build.Dense(3, 8);
                for (int i = 0; i < 4; i++)
                {
                    double nx = (j22 * dr[i] - j12 * ds[i]) / det, ny = (-j21 * dr[i] + j11 * ds[i]) / det;
                    b[0, 2 * i] = nx; b[1, 2 * i + 1] = ny; b[2, 2 * i] = ny; b[2, 2 * i + 1] = nx;
                }
                return (b, det);
            }
            var elements = new List<int[]>();
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) elements.Add([Node(i, j), Node(i + 1, j), Node(i + 1, j + 1), Node(i, j + 1)]);
            var k = Matrix<double>.Build.Dense(dofs, dofs);
            foreach (var nodes in elements)
                foreach (var (r, s) in gauss)
                {
                    var (b, det) = Strain(nodes, r, s);
                    var ke = b.TransposeThisAndMultiply(e * b) * (thickness * det);
                    for (int a = 0; a < 8; a++) for (int c = 0; c < 8; c++) k[2 * nodes[a / 2] + a % 2, 2 * nodes[c / 2] + c % 2] += ke[a, c];
                }
            // free interior: K_ff u_f = −K_fp u_p
            var free = Enumerable.Range(0, dofs).Where(q => prescribed[q] is null).ToArray();
            var fixedDofs = Enumerable.Range(0, dofs).Where(q => prescribed[q] is not null).ToArray();
            var u = Vector<double>.Build.Dense(dofs, q => prescribed[q] ?? 0);
            if (free.Length > 0)
            {
                var kff = Matrix<double>.Build.Dense(free.Length, free.Length, (a, c) => k[free[a], free[c]]);
                var rhs = Vector<double>.Build.Dense(free.Length, a => -fixedDofs.Sum(c => k[free[a], c] * u[c]));
                var solution = kff.Cholesky().Solve(rhs);
                for (int a = 0; a < free.Length; a++) u[free[a]] = solution[a];
            }
            Stiffness = u.DotProduct(k * u);
            double area = 0, shearIntegral = 0, shearMax = 0, vonMises = 0;
            foreach (var nodes in elements)
            {
                var ue = Vector<double>.Build.Dense(8, a => u[2 * nodes[a / 2] + a % 2]);
                foreach (var (r, s) in gauss)
                {
                    var (b, det) = Strain(nodes, r, s);
                    var p = e * (b * ue);
                    area += det; shearIntegral += Math.Abs(p[2]) * det; shearMax = Math.Max(shearMax, Math.Abs(p[2]));
                    vonMises = Math.Max(vonMises, Math.Sqrt(p[0] * p[0] + p[1] * p[1] - p[0] * p[1] + 3 * p[2] * p[2]));
                }
            }
            Shear = Math.Max(shearIntegral / area, shearMax / 2); VonMises = vonMises;
        }
    }

    /// <summary>The stiffness K_D of an X cross-frame (two diagonals between opposite corners)</summary>
    /// <param name="mode">The distortion mode</param>
    /// <param name="axialStiffness">E A of each diagonal [N]</param>
    /// <returns>K_D [N mm]</returns>
    public static double BracingStiffness(BoxDistortionMode mode, double axialStiffness) =>
        Diagonals(mode).Sum(g => axialStiffness / g.Length * g.Elongation * g.Elongation);

    /// <summary>The axial forces of the two diagonals at the distortion amplitude γ (tension positive) [N]</summary>
    public static double[] BracingForces(BoxDistortionMode mode, double axialStiffness, double amplitude) =>
        Diagonals(mode).Select(g => axialStiffness / g.Length * g.Elongation * amplitude).ToArray();

    /// <summary>The lengths of the two diagonals</summary>
    public static double[] BracingLengths(BoxDistortionMode mode) => Diagonals(mode).Select(g => g.Length).ToArray();

    private static IEnumerable<(double Length, double Elongation)> Diagonals(BoxDistortionMode mode)
    {
        foreach (var (a, b) in new[] { (0, 2), (1, 3) })
        {
            double dx = mode.X[b] - mode.X[a], dy = mode.Y[b] - mode.Y[a], l = Math.Sqrt(dx * dx + dy * dy);
            yield return (l, (dx * (mode.U[b] - mode.U[a]) + dy * (mode.V[b] - mode.V[a])) / l);
        }
    }

    /// <summary>A beam on elastic foundation with Hermite elements: E I w'''' + k w = p, point springs and point loads at the nodes</summary>
    /// <param name="x">The coordinates of the nodes, increasing</param>
    /// <param name="bending">E I</param>
    /// <param name="foundation">k per unit length</param>
    /// <param name="springs">The stiffness of the springs at the nodes (null: none)</param>
    /// <param name="distributed">The uniform load p per unit length</param>
    /// <param name="pointLoads">The loads at the nodes (null: none)</param>
    /// <param name="supportedEnds">True: w = 0 at the end nodes (free rotation)</param>
    /// <returns>The deflections and the bending moments E I w'' at the nodes</returns>
    public static (double[] Deflection, double[] Moment) BeamOnFoundation(double[] x, double bending, double foundation, double[]? springs,
        double distributed, double[]? pointLoads, bool supportedEnds)
    {
        var beam = new FoundationBeam(x, bending, foundation, springs ?? new double[x.Length], supportedEnds);
        return beam.Solve(distributed, pointLoads ?? new double[x.Length]);
    }

    /// <summary>
    /// The distortion along a simply supported span with rigid end diaphragms (ψ = 0, free warping) and intermediate diaphragms at constant
    /// spacing from the left end: uniform load over the whole span plus a moving concentrated load of the same sign, envelope of the absolute values
    /// </summary>
    /// <param name="span">The span L [mm]</param>
    /// <param name="spacing">The spacing of the intermediate diaphragms (0: none)</param>
    /// <param name="bending">E I_Dw [N mm⁴]</param>
    /// <param name="frame">K [N]</param>
    /// <param name="diaphragm">K_D [N mm]</param>
    /// <param name="distributed">The generalised uniform load [N]</param>
    /// <param name="concentrated">The generalised concentrated load [N mm]</param>
    public static BoxDistortionEnvelope Envelope(double span, double spacing, double bending, double frame, double diaphragm, double distributed, double concentrated)
    {
        BridgeNumbers.Require(span, "luce", strict: true); BridgeNumbers.Require(spacing, "passo_diaframmi");
        BridgeNumbers.Require(bending, "E I_Dw", strict: true); BridgeNumbers.Require(frame, "K", strict: true); BridgeNumbers.Require(diaphragm, "K_D");
        var stations = new List<double> { 0 };
        if (spacing > 0) for (double s = spacing; s < span - 1e-6 * span; s += spacing) stations.Add(s);
        int diaphragms = stations.Count - 1;
        stations.Add(span);
        double lambda = Math.Pow(frame / (4 * bending), .25);
        var x = new List<double> { 0 }; var isDiaphragm = new List<bool> { false };
        for (int i = 0; i + 1 < stations.Count; i++)
        {
            double bay = stations[i + 1] - stations[i];
            // at least 16 elements per bay and λ h ≤ 1/8 (the Hermite elements with foundation converge with (λ h)⁴)
            int n = (int)Math.Min(400, Math.Max(16, Math.Ceiling(8 * lambda * bay)));
            for (int j = 1; j <= n; j++) { x.Add(stations[i] + bay * j / n); isDiaphragm.Add(j == n && i + 2 < stations.Count); }
        }
        var springs = isDiaphragm.Select(b => b ? diaphragm : 0).ToArray();
        var beam = new FoundationBeam(x.ToArray(), bending, frame, springs, true);
        var (wu, mu) = beam.Solve(Math.Abs(distributed), new double[x.Count]);
        double maxW = wu.Max(Math.Abs), maxM = mu.Max(Math.Abs), maxD = Enumerable.Range(0, x.Count).Where(j => isDiaphragm[j]).Select(j => Math.Abs(wu[j])).DefaultIfEmpty().Max();
        if (concentrated != 0)
        {
            var loads = new double[x.Count];
            // every node on usual models; on very fine ones the diaphragms, the middle of the bays and a regular sample
            int stride = Math.Max(1, (x.Count - 2) / 600);
            var positions = Enumerable.Range(1, x.Count - 2).Where(p => p % stride == 0 || isDiaphragm[p]
                || stations.Zip(stations.Skip(1), (a, b) => (a + b) / 2).Any(c => Math.Abs(x[p] - c) <= (x[p + 1] - x[p - 1]) / 2 + 1e-9));
            foreach (int p in positions)
            {
                Array.Clear(loads, 0, loads.Length); loads[p] = Math.Abs(concentrated);
                var (wc, mc) = beam.Solve(0, loads);
                for (int j = 0; j < x.Count; j++)
                {
                    double w = Math.Abs(wu[j] + wc[j]);
                    maxW = Math.Max(maxW, w); maxM = Math.Max(maxM, Math.Abs(mu[j] + mc[j]));
                    if (isDiaphragm[j]) maxD = Math.Max(maxD, w);
                }
            }
        }
        return new(maxM, maxW, maxD, diaphragms, x.Count);
    }

    /// <summary>The banded stiffness of the beam on elastic foundation, factorised once for several loads (Cholesky, half bandwidth 3)</summary>
    private sealed class FoundationBeam
    {
        private readonly double[] _x;
        private readonly double _bending, _foundation;
        private readonly bool _supported;
        private readonly double[,] _band; // _band[i, k] = A[i, i + k], factorised in place
        private readonly double[][,] _elements;
        private const int Width = 4;

        public FoundationBeam(double[] x, double bending, double foundation, double[] springs, bool supported)
        {
            if (x.Length < 2) throw new ArgumentException("Almeno due nodi.");
            for (int i = 1; i < x.Length; i++) if (!(x[i] > x[i - 1])) throw new ArgumentException("Nodi crescenti richiesti.");
            _x = x; _bending = bending; _foundation = foundation; _supported = supported;
            int n = 2 * x.Length;
            _band = new double[n, Width];
            _elements = Enumerable.Range(0, x.Length - 1).Select(e => Element(x[e + 1] - x[e])).ToArray();
            for (int e = 0; e + 1 < x.Length; e++)
            {
                var k = _elements[e];
                for (int a = 0; a < 4; a++) for (int b = a; b < 4; b++) _band[2 * e + a, b - a] += k[a, b];
            }
            for (int j = 0; j < x.Length; j++) _band[2 * j, 0] += springs[j];
            if (supported) foreach (int j in new[] { 0, x.Length - 1 })
            {
                int r = 2 * j;
                for (int k = 1; k < Width; k++) { if (r + k < n) _band[r, k] = 0; if (r - k >= 0) _band[r - k, k] = 0; }
                _band[r, 0] = 1;
            }
            for (int i = 0; i < n; i++)
            {
                double sum = _band[i, 0];
                for (int k = 1; k < Width && i - k >= 0; k++) sum -= _band[i - k, k] * _band[i - k, k];
                if (sum <= 0) throw new InvalidOperationException("Trave su suolo elastico labile.");
                _band[i, 0] = Math.Sqrt(sum);
                for (int j = 1; j < Width && i + j < n; j++)
                {
                    double v = _band[i, j];
                    for (int k = 1; k < Width && i - k >= 0 && k + j < Width; k++) v -= _band[i - k, k] * _band[i - k, k + j];
                    _band[i, j] = v / _band[i, 0];
                }
            }
        }

        private double[,] Element(double l)
        {
            double a = _bending / (l * l * l), f = _foundation * l / 420;
            return new[,] {
                { 12 * a + 156 * f, 6 * l * a + 22 * l * f, -12 * a + 54 * f, 6 * l * a - 13 * l * f },
                { 6 * l * a + 22 * l * f, 4 * l * l * a + 4 * l * l * f, -6 * l * a + 13 * l * f, 2 * l * l * a - 3 * l * l * f },
                { -12 * a + 54 * f, -6 * l * a + 13 * l * f, 12 * a + 156 * f, -6 * l * a - 22 * l * f },
                { 6 * l * a - 13 * l * f, 2 * l * l * a - 3 * l * l * f, -6 * l * a - 22 * l * f, 4 * l * l * a + 4 * l * l * f } };
        }

        private static double[] Load(double l, double p) => [p * l / 2, p * l * l / 12, p * l / 2, -p * l * l / 12];

        public (double[] Deflection, double[] Moment) Solve(double distributed, double[] pointLoads)
        {
            int nodes = _x.Length, n = 2 * nodes;
            var r = new double[n];
            for (int e = 0; e + 1 < nodes; e++) { var f = Load(_x[e + 1] - _x[e], distributed); for (int a = 0; a < 4; a++) r[2 * e + a] += f[a]; }
            for (int j = 0; j < nodes; j++) r[2 * j] += pointLoads[j];
            if (_supported) { r[0] = 0; r[n - 2] = 0; }
            for (int i = 0; i < n; i++)
            {
                double v = r[i];
                for (int k = 1; k < Width && i - k >= 0; k++) v -= _band[i - k, k] * r[i - k];
                r[i] = v / _band[i, 0];
            }
            for (int i = n - 1; i >= 0; i--)
            {
                double v = r[i];
                for (int k = 1; k < Width && i + k < n; k++) v -= _band[i, k] * r[i + k];
                r[i] = v / _band[i, 0];
            }
            var w = new double[nodes]; var m = new double[nodes];
            for (int j = 0; j < nodes; j++) w[j] = r[2 * j];
            // E I w'' at the element ends from the end moments of the element (the moment is continuous at the nodes)
            for (int e = 0; e + 1 < nodes; e++)
            {
                double l = _x[e + 1] - _x[e];
                var k = _elements[e]; var f = Load(l, distributed);
                double m1 = -f[1], m2 = -f[3];
                for (int b = 0; b < 4; b++) { m1 += k[1, b] * r[2 * e + b]; m2 += k[3, b] * r[2 * e + b]; }
                if (Math.Abs(m1) > Math.Abs(m[e])) m[e] = -m1;
                if (Math.Abs(m2) > Math.Abs(m[e + 1])) m[e + 1] = m2;
            }
            return (w, m);
        }
    }
}
