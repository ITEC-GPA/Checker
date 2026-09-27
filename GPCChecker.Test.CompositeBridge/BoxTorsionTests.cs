using GPC.Checkers.CompositeBridge;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// Torsion, distortion and diaphragms of the box girder: the distortion mode, the warping inertia, the frame and diaphragm stiffnesses and the beam
/// on elastic foundation against closed forms (rectangle, Hetényi), independent calculations of the trapezoidal cell and hand calculations of
/// the St. Venant shear flows through the section checks
/// </summary>
[TestClass]
public class BoxTorsionTests
{
    private const double B = 3000, H = 2000, T = 20, Young = 210000;

    private static BoxDistortionSection Rectangle(double top = 2e8, double web = 5e7) => new(B, B, H, T, T, T, top, web, top);

    [TestMethod]
    public void RectangleModeIsTheClosedForm()
    {
        var mode = BoxDistortion.Mode(Rectangle());
        // every corner changes its angle by γ = 1; walls translate by h/4 (top, bottom) and b/4 (webs)
        foreach (double a in mode.AngleChanges) Assert.AreEqual(1, Math.Abs(a), 1e-12);
        foreach (double u in mode.U) Assert.AreEqual(H / 4, Math.Abs(u), 1e-9);
        foreach (double v in mode.V) Assert.AreEqual(B / 4, Math.Abs(v), 1e-9);
        // warping ±bh/8, the same sign at opposite corners
        foreach (double w in mode.Warping) Assert.AreEqual(B * H / 8, Math.Abs(w), 1e-6);
        Assert.IsTrue(mode.Warping[0] * mode.Warping[2] > 0 && mode.Warping[1] * mode.Warping[3] > 0 && mode.Warping[0] * mode.Warping[1] < 0);
        Assert.AreEqual(T * (B + H) * B * B * H * H / 96, mode.WarpingInertia, 1e-9 * mode.WarpingInertia, "I_Dw = t (b + h) b² h² / 96");
        // rigid frame: K = 24 / (b / D_flanges + h / D_webs)
        Assert.AreEqual(24 / (B / 2e8 + H / 5e7), mode.FrameStiffness, 1e-9 * mode.FrameStiffness);
        // a torque as a vertical couple T / b at the top corners: generalised load T / 2
        Assert.AreEqual(.5, Math.Abs(mode.TorqueLoad), 1e-12);
    }

    [TestMethod]
    public void RectangleFrameMomentsFromTheJointRotations()
    {
        // equal rigidities: joint rotations vanish, the end moments are 6 D ρ / ℓ with ρ = γ/2 on every wall: 3 D / ℓ
        var mode = BoxDistortion.Mode(new BoxDistortionSection(B, B, B, T, T, T, 1e8, 1e8, 1e8));
        foreach (double m in mode.CornerMoments) Assert.AreEqual(3 * 1e8 / B, m, 1e-9 * m);
        Assert.AreEqual(24 / (2 * B / 1e8), mode.FrameStiffness, 1e-9 * mode.FrameStiffness);
    }

    [TestMethod]
    public void DiaphragmStiffnessOfTheRectangle()
    {
        var mode = BoxDistortion.Mode(Rectangle());
        double g = Young / 2.6, l = Math.Sqrt(B * B + H * H), ea = Young * 5000;
        Assert.AreEqual(g * 12 * B * H, BoxDistortion.PlateDiaphragmStiffness(mode, 12, Young), 1e-9 * g * 12 * B * H, "plate: K_D = G t b h");
        var (tau, vm) = BoxDistortion.PlateDiaphragmStresses(mode, 12, Young, 1e-4);
        Assert.AreEqual(g * 1e-4, tau, 1e-9, "pure shear τ = G γ");
        Assert.AreEqual(Math.Sqrt(3) * g * 1e-4, vm, 1e-9);
        double bracing = 2 * ea * B * B * H * H / Math.Pow(l, 3);
        Assert.AreEqual(bracing, BoxDistortion.BracingStiffness(mode, ea), 1e-12 * bracing, "X bracing: K_D = 2 E A b² h² / L³");
        foreach (double n in BoxDistortion.BracingForces(mode, ea, 1e-4)) Assert.AreEqual(ea / l * B * H / l * 1e-4, Math.Abs(n), 1e-12 * Math.Abs(n));
        var forces = BoxDistortion.BracingForces(mode, ea, 1e-4);
        Assert.IsTrue(forces[0] * forces[1] < 0, "one diagonal in tension, the other in compression");
    }

    /// <summary>A trapezoidal cell with different walls, outstands and lumped areas</summary>
    private static BoxDistortionSection Trapezoid() => new(2400, 1500, 2100, 25, 14, 30, 3e8, 4e7, 2.5e8, 1800, 120, 11250, 0);

    [TestMethod]
    public void TrapezoidModeIsAMechanismWithoutShearAndOrthogonalWarping()
    {
        var s = Trapezoid();
        var mode = BoxDistortion.Mode(s);
        double[] x = mode.X, y = mode.Y;
        int[] a = [0, 1, 2, 3], b = [1, 2, 3, 0];
        double[] t = [s.TopThickness, s.WebThickness, s.BottomThickness, s.WebThickness];
        double loop = 0;
        for (int i = 0; i < 4; i++)
        {
            double dx = x[b[i]] - x[a[i]], dy = y[b[i]] - y[a[i]], l = Math.Sqrt(dx * dx + dy * dy);
            // inextensible walls
            Assert.AreEqual(0, (dx * (mode.U[b[i]] - mode.U[a[i]]) + dy * (mode.V[b[i]] - mode.V[a[i]])) / l, 1e-9);
            // dω/ds = tangential displacement of the wall
            double tangential = (dx * mode.U[a[i]] + dy * mode.V[a[i]]) / l;
            Assert.AreEqual(mode.Warping[b[i]] - mode.Warping[a[i]], tangential * l, 1e-9 * mode.Warping.Max(Math.Abs));
            loop += tangential * l;
        }
        Assert.AreEqual(0, loop, 1e-6 * mode.Warping.Max(Math.Abs), "zero membrane shear strain: Σ ℓ V = 0");
        Assert.AreEqual(1, mode.AngleChanges.Average(Math.Abs), 1e-12);
        // N = Mx = My = 0 of the warping, integrated here on the walls, the outstands and the lumped areas
        double[] ox = [x[0] - s.TopOutstand, x[1] + s.TopOutstand, x[2] + s.BottomOutstand, x[3] - s.BottomOutstand];
        double Integral(Func<double, double, double> f)
        {
            double sum = 0;
            void Segment(double x1, double y1, double w1, double x2, double y2, double w2, double th)
            {
                double l = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                // Simpson is exact for the product of two linear functions
                sum += th * l / 6 * (w1 * f(x1, y1) + 4 * (w1 + w2) / 2 * f((x1 + x2) / 2, (y1 + y2) / 2) + w2 * f(x2, y2));
            }
            for (int i = 0; i < 4; i++) Segment(x[a[i]], y[a[i]], mode.Warping[a[i]], x[b[i]], y[b[i]], mode.Warping[b[i]], t[i]);
            for (int k = 0; k < 4; k++)
                Segment(x[k], y[k], mode.Warping[k], ox[k], y[k], mode.OutstandWarping[k], k < 2 ? s.TopThickness : s.BottomThickness);
            sum += s.TopCornerArea * (mode.Warping[0] * f(x[0], y[0]) + mode.Warping[1] * f(x[1], y[1]));
            return sum;
        }
        // scale: ∫|ω| t ds ≈ I_Dw / max|ω|; the moments are relative to the size of the cell
        double scale = mode.WarpingInertia / mode.Warping.Max(Math.Abs);
        Assert.AreEqual(0, Integral((_, _) => 1), 1e-10 * scale, "N");
        Assert.AreEqual(0, Integral((px, _) => px), 1e-10 * scale * 3000, "My");
        Assert.AreEqual(0, Integral((_, py) => py), 1e-10 * scale * 3000, "Mx");
        // the outstands continue the warping of their wall
        Assert.AreEqual(mode.Warping[0] + (mode.Warping[0] - mode.Warping[1]) / s.TopWidth * s.TopOutstand, mode.OutstandWarping[0], 1e-9 * mode.Warping.Max(Math.Abs));
        Assert.AreEqual(mode.Warping[3] + (mode.Warping[3] - mode.Warping[2]) / s.BottomWidth * s.BottomOutstand, mode.OutstandWarping[3], 1e-9 * mode.Warping.Max(Math.Abs));
        Assert.AreEqual(WarpingSquare(mode, s), mode.WarpingInertia, 1e-9 * mode.WarpingInertia);
    }

    private static double WarpingSquare(BoxDistortionMode mode, BoxDistortionSection s)
    {
        double sum = 0;
        void Segment(double l, double w1, double w2, double th) => sum += th * l / 3 * (w1 * w1 + w1 * w2 + w2 * w2);
        int[] a = [0, 1, 2, 3], b = [1, 2, 3, 0];
        double[] t = [s.TopThickness, s.WebThickness, s.BottomThickness, s.WebThickness];
        for (int i = 0; i < 4; i++)
            Segment(Math.Sqrt(Math.Pow(mode.X[b[i]] - mode.X[a[i]], 2) + Math.Pow(mode.Y[b[i]] - mode.Y[a[i]], 2)), mode.Warping[a[i]], mode.Warping[b[i]], t[i]);
        for (int k = 0; k < 4; k++) Segment(k < 2 ? s.TopOutstand : s.BottomOutstand, mode.Warping[k], mode.OutstandWarping[k], k < 2 ? s.TopThickness : s.BottomThickness);
        return sum + s.TopCornerArea * (mode.Warping[0] * mode.Warping[0] + mode.Warping[1] * mode.Warping[1]);
    }

    [TestMethod]
    public void TrapezoidFrameStiffnessFromAnIndependentFrameModel()
    {
        // global 2D frame elements (u, v, θ at both ends) with the translations of the mode imposed and free joint rotations: K = 2 U
        var s = Trapezoid();
        var mode = BoxDistortion.Mode(s);
        int[] a = [0, 1, 2, 3], b = [1, 2, 3, 0];
        double[] d = [s.TopRigidity, s.WebRigidity, s.BottomRigidity, s.WebRigidity];
        var k = new double[12, 12];
        for (int i = 0; i < 4; i++)
        {
            double dx = mode.X[b[i]] - mode.X[a[i]], dy = mode.Y[b[i]] - mode.Y[a[i]], l = Math.Sqrt(dx * dx + dy * dy), c = dx / l, sn = dy / l, ei = d[i];
            // transverse displacement w = −sn u + c v, local bending stiffness on (w1, θ1, w2, θ2)
            double[,] kb = { { 12 * ei / (l * l * l), 6 * ei / (l * l), -12 * ei / (l * l * l), 6 * ei / (l * l) }, { 6 * ei / (l * l), 4 * ei / l, -6 * ei / (l * l), 2 * ei / l },
                { -12 * ei / (l * l * l), -6 * ei / (l * l), 12 * ei / (l * l * l), -6 * ei / (l * l) }, { 6 * ei / (l * l), 2 * ei / l, -6 * ei / (l * l), 4 * ei / l } };
            var map = new (int Dof, double Factor)[][] { [(3 * a[i], -sn), (3 * a[i] + 1, c)], [(3 * a[i] + 2, 1)], [(3 * b[i], -sn), (3 * b[i] + 1, c)], [(3 * b[i] + 2, 1)] };
            for (int p = 0; p < 4; p++) for (int q = 0; q < 4; q++)
                foreach (var (dp, fp) in map[p]) foreach (var (dq, fq) in map[q]) k[dp, dq] += fp * kb[p, q] * fq;
        }
        var u = new double[12];
        for (int j = 0; j < 4; j++) { u[3 * j] = mode.U[j]; u[3 * j + 1] = mode.V[j]; }
        // K_θθ θ = −K_θu u
        var kt = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(4, 4, (p, q) => k[3 * p + 2, 3 * q + 2]);
        var f = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(4, p => -Enumerable.Range(0, 12).Where(q => q % 3 != 2).Sum(q => k[3 * p + 2, q] * u[q]));
        var theta = kt.Solve(f);
        for (int j = 0; j < 4; j++) u[3 * j + 2] = theta[j];
        double energy = 0;
        for (int p = 0; p < 12; p++) for (int q = 0; q < 12; q++) energy += u[p] * k[p, q] * u[q] / 2;
        Assert.AreEqual(2 * energy, mode.FrameStiffness, 1e-9 * mode.FrameStiffness);
        Assert.AreEqual(Math.Abs((mode.V[1] - mode.V[0]) / s.TopWidth), Math.Abs(mode.TorqueLoad), 1e-12);
    }

    [TestMethod]
    public void TrapezoidDiaphragmsAgainstTheLiterature()
    {
        // Yoo et al. (SSRC 2015): K_D = E A (b + c)² h² / (2 L³) for the X bracing and G t (b + c) h / 2 for the plate, with the distortion angle
        // γ* = (|Δ_top| b + |Δ_bottom| c) / (b + c), the change of the corner angles weighted with the widths (for the rectangle γ* = γ)
        foreach (var s in new[] { Trapezoid(), new BoxDistortionSection(3050, 1800, 1900, 20, 14, 25, 3e8, 4e7, 2.5e8), new BoxDistortionSection(2000, 1000, 2000, 20, 14, 25, 3e8, 4e7, 2.5e8) })
        {
            var mode = BoxDistortion.Mode(s);
            double b = s.TopWidth, c = s.BottomWidth, h = s.Height, l = Math.Sqrt((b + c) * (b + c) / 4 + h * h), ea = Young * 3000;
            double ratio = (Math.Abs(mode.AngleChanges[0]) * b + Math.Abs(mode.AngleChanges[2]) * c) / (b + c);
            double bracing = ea * (b + c) * (b + c) * h * h / (2 * l * l * l) * ratio * ratio;
            Assert.AreEqual(bracing, BoxDistortion.BracingStiffness(mode, ea), 1e-9 * bracing, "X bracing: exact");
            // the plate: edges moved by the walls, interior free; the formula assumes a uniform shear strain γ* (from +5% to +10% here,
            // the more the webs are inclined)
            double shear = Young / 2.6 * 12 * (b + c) / 2 * h * ratio * ratio, kd = BoxDistortion.PlateDiaphragmStiffness(mode, 12, Young);
            Assert.AreEqual(shear, kd, .12 * shear, "plate near the uniform shear");
            Assert.AreEqual(BoxDistortion.PlateDiaphragmStiffness(mode, 12, Young, .3, 24), kd, 1e-3 * kd, "converged mesh");
            Assert.AreEqual(2 * kd, BoxDistortion.PlateDiaphragmStiffness(mode, 24, Young), 1e-9 * kd, "linear in t");
            // the plate cannot be stiffer than the single bilinear element (the exact field minimises the energy with the same edges)
            Assert.IsTrue(kd <= BoxDistortion.PlateDiaphragmStiffness(mode, 12, Young, .3, 1));
        }
    }

    [TestMethod]
    public void InfiniteBeamOnElasticFoundationIsHetenyi()
    {
        double ei = 1e12, k = 1e3, p = 1e5, lambda = Math.Pow(k / (4 * ei), .25);
        int n = 1601;
        var x = Enumerable.Range(0, n).Select(i => (i - (n - 1) / 2.0) * 1 / (20 * lambda)).ToArray();
        var loads = new double[n]; loads[(n - 1) / 2] = p;
        var (w, m) = BoxDistortion.BeamOnFoundation(x, ei, k, null, 0, loads, false);
        Assert.AreEqual(p * lambda / (2 * k), w[(n - 1) / 2], 1e-5 * p * lambda / (2 * k), "w(0) = P λ / (2k)");
        Assert.AreEqual(p / (4 * lambda), Math.Abs(m[(n - 1) / 2]), 1e-4 * p / (4 * lambda), "M(0) = P / (4λ)");
        // uniform load far from the ends: w = p / k, no moment
        var (wu, mu) = BoxDistortion.BeamOnFoundation(x, ei, k, null, 7, null, false);
        Assert.AreEqual(7 / k, wu[(n - 1) / 2], 1e-9 * 7 / k);
        Assert.AreEqual(0, mu[(n - 1) / 2], 1e-6 * 7 / (lambda * lambda));
    }

    [TestMethod]
    public void SimplySupportedBeamOnElasticFoundation()
    {
        // Hetényi: hinged beam of length L, a = λL/2
        double ei = 3e11, k = 2e3, lambda = Math.Pow(k / (4 * ei), .25), span = 2 / lambda, a = lambda * span / 2, p = 50, load = 2e5;
        var uniform = BoxDistortion.Envelope(span, 0, ei, k, 0, p, 0);
        Assert.AreEqual(p / k * (1 - 2 * Math.Cosh(a) * Math.Cos(a) / (Math.Cosh(2 * a) + Math.Cos(2 * a))), uniform.MaxAmplitude, 1e-5 * uniform.MaxAmplitude);
        Assert.AreEqual(p / (lambda * lambda) * Math.Sinh(a) * Math.Sin(a) / (Math.Cosh(2 * a) + Math.Cos(2 * a)), uniform.MaxMoment, 1e-4 * uniform.MaxMoment);
        double l = lambda * span, denominator = Math.Cosh(l) + Math.Cos(l);
        var point = BoxDistortion.Envelope(span, 0, ei, k, 0, 0, load);
        Assert.AreEqual(load * lambda / (2 * k) * (Math.Sinh(l) - Math.Sin(l)) / denominator, point.MaxAmplitude, 1e-5 * point.MaxAmplitude, "load at midspan");
        Assert.AreEqual(load / (4 * lambda) * (Math.Sinh(l) + Math.Sin(l)) / denominator, point.MaxMoment, 1e-4 * point.MaxMoment);
        // very short beam: the simply supported beam PL/4
        var stiff = BoxDistortion.Envelope(1000, 0, 1e15, 1e-6, 0, 0, load);
        Assert.AreEqual(load * 1000 / 4, stiff.MaxMoment, 1e-6 * load * 250);
    }

    [TestMethod]
    public void DiaphragmsReduceTheDistortion()
    {
        var mode = BoxDistortion.Mode(Rectangle());
        double ei = Young * mode.WarpingInertia, kd = BoxDistortion.PlateDiaphragmStiffness(mode, 10, Young);
        var none = BoxDistortion.Envelope(40000, 0, ei, mode.FrameStiffness, 0, 1e4, 1e8);
        var wide = BoxDistortion.Envelope(40000, 10000, ei, mode.FrameStiffness, kd, 1e4, 1e8);
        var close = BoxDistortion.Envelope(40000, 5000, ei, mode.FrameStiffness, kd, 1e4, 1e8);
        Assert.AreEqual(0, none.Diaphragms); Assert.AreEqual(3, wide.Diaphragms); Assert.AreEqual(7, close.Diaphragms);
        Assert.IsTrue(close.MaxMoment < wide.MaxMoment && wide.MaxMoment < none.MaxMoment);
        Assert.IsTrue(close.MaxAmplitude < wide.MaxAmplitude && wide.MaxAmplitude < none.MaxAmplitude);
        // linear in the loads
        var twice = BoxDistortion.Envelope(40000, 5000, ei, mode.FrameStiffness, kd, 2e4, 2e8);
        Assert.AreEqual(2 * close.MaxMoment, twice.MaxMoment, 1e-9 * close.MaxMoment);
        Assert.AreEqual(2 * close.MaxDiaphragmAmplitude, twice.MaxDiaphragmAmplitude, 1e-9 * close.MaxDiaphragmAmplitude);
        // rigid diaphragms: the distortion at the diaphragms vanishes
        var rigid = BoxDistortion.Envelope(40000, 5000, ei, mode.FrameStiffness, 1e6 * kd, 1e4, 1e8);
        Assert.IsTrue(rigid.MaxDiaphragmAmplitude < 1e-5 * rigid.MaxAmplitude);
    }

    [TestMethod]
    public void ClosedCellOfBredt()
    {
        var cell = new BoxCell(3000, 2000, 1500, 20, 15, 25);
        double a0 = 2500 * 1500, web = Math.Sqrt(500 * 500 + 1500 * 1500);
        Assert.AreEqual(a0, cell.Area, 1e-9);
        Assert.AreEqual(4 * a0 * a0 / (3000 / 20.0 + 2 * web / 15 + 2000 / 25.0), cell.TorsionConstant, 1e-9 * cell.TorsionConstant);
        Assert.AreEqual(1e9 / (2 * a0), cell.ShearFlow(1e9), 1e-12);
    }

    // the box of SectionTypeTests with torsion
    private const double Hw = 1800, Tw = 14, Tt = 25, Tb = 25, Hc = 250, St = 1800, Offset = 250;

    private static HBridgeInput Box(bool enabled = true, double t1 = 200, double t2 = 300, double t3 = 1000, BridgeBoxOptions? box = null) => new()
    {
        Materials = new(ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1993Data.S355, SteelMaterialEN1992Data.B450C),
        Geometry = new() { SlabWidth = 3000, SlabHeight = Hc, WebHeight = Hw, WebThickness = Tw, TopWidth = 450, TopThickness = Tt, BottomWidth = 1400, BottomThickness = Tb,
            SectionType = BridgeSteelSectionType.Box, WebSpacing = St, WebOffset = Offset },
        TopRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
        Options = new() { Class4 = false },
        Box = box ?? new() { Enabled = enabled, BracingThickness = enabled ? 4 : 0 },
        Phases = [new() { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 3000, ShearKN = 600, TorsionKNm = t1 },
            new() { Name = "G2", MomentKNm = 4000, ShearKN = 400, Phi = 2, PsiL = 1.1, TorsionKNm = t2 },
            new() { Name = "Q", MomentKNm = 6000, ShearKN = 1800, TorsionKNm = t3 }]
    };

    private static double Half(double y) => St / 2 + (y + Tt) * Offset / Hw;

    [TestMethod]
    public void ShearFlowsOfThePhasesByHand()
    {
        var r = HBridgeSection.Calculate(Box());
        var torsion = r.Stages.Last().Torsion!;
        double yb = -Tt - Hw - Tb / 2;
        // steel phase: cell closed by the bracing at the mid-plane of the top flanges
        double steel = (Half(-Tt / 2) + Half(yb)) * (-Tt / 2 - yb);
        // composite phases: cell closed by the slab at its mid-plane, webs extended
        double composite = (Half(Hc / 2) + Half(yb)) * (Hc / 2 - yb);
        double q1 = 200e6 / (2 * steel), q2 = 300e6 / (2 * composite), q3 = 1000e6 / (2 * composite);
        Assert.AreEqual(steel, torsion.Flows[0].CellArea, 1e-6);
        Assert.AreEqual(composite, torsion.Flows[2].CellArea, 1e-6);
        Assert.AreEqual(q1, torsion.Flows[0].Flow, 1e-9);
        Assert.AreEqual(q1 + q2 + q3, torsion.WebFlow, 1e-9);
        Assert.AreEqual(q2 + q3, torsion.SlabFlow, 1e-9);
        Assert.AreEqual(q1, torsion.BracingFlow, 1e-9);
        // J of Q: slab hc / nG with nG = n0 (1 + 0.2) / (1 + 0.3)
        double ng = r.Materials.Ea / r.Materials.Ec * 1.2 / 1.3, bt = 2 * Half(Hc / 2), bb = 2 * Half(yb), h = Hc / 2 - yb;
        double web = Math.Sqrt(Math.Pow((bt - bb) / 2, 2) + h * h);
        Assert.AreEqual(4 * composite * composite / (bt / (Hc / ng) + 2 * web / Tw + bb / Tb), torsion.Flows[2].TorsionConstant, 1e-9 * torsion.Flows[2].TorsionConstant);
        // G2 with creep: n = n0 (1 + ψ φ)
        double ng2 = r.Materials.Ea / r.Materials.Ec * (1 + 1.1 * 2) * 1.2 / 1.3;
        Assert.AreEqual(4 * composite * composite / (bt / (Hc / ng2) + 2 * web / Tw + bb / Tb), torsion.Flows[1].TorsionConstant, 1e-9 * torsion.Flows[1].TorsionConstant);
        Assert.AreEqual((q1 + q2 + q3) / Tw, torsion.Details.Single(x => x.Name == "Torsione · τ anime").Value, 1e-9);
    }

    [TestMethod]
    public void TorsionAddsToTheShearOfOneWeb()
    {
        var with = HBridgeSection.Calculate(Box());
        var without = HBridgeSection.Calculate(Box(t1: 0, t2: 0, t3: 0));
        var stage = with.Stages.Last();
        var g = with.Geometry;
        double q = stage.Torsion!.WebFlow;
        var shear = stage.Shear!.Checks.Single(c => c.Name == "Anima · resistenza a taglio");
        // V/(2 cos α) + q hw / cos α on the more loaded web, brought back to the vertical total
        Assert.AreEqual(2800 + q * g.PlateLength / 1000 * 2 * Math.Cos(g.WebAngle), shear.Demand, 1e-9);
        Assert.AreEqual(without.Stages.Last().Shear!.Checks.Single(c => c.Name == "Anima · resistenza a taglio").Resistance, shear.Resistance, 1e-9);
        Assert.AreEqual(without.Stages.Last().Shear!.TauMaximum + q / Tw, stage.Shear.TauMaximum, 1e-9, "Jourawski envelope + q/tw");
        // without torsion, the checks of webs, studs and slab are those of the box without torsion checks
        var off = HBridgeSection.Calculate(Box(enabled: false, t1: 0, t2: 0, t3: 0));
        for (int s = 0; s < off.Stages.Count; s++)
        {
            var a = off.Stages[s].Shear!.Checks; var b = without.Stages[s].Shear!.Checks;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i].Name, b[i].Name); Assert.AreEqual(a[i].Demand, b[i].Demand, 0); Assert.AreEqual(a[i].Resistance, b[i].Resistance, 0); Assert.AreEqual(a[i].Note, b[i].Note); }
            Assert.IsNull(off.Stages[s].Torsion);
        }
    }

    [TestMethod]
    public void TorsionAddsToTheStudsOfOneFlange()
    {
        var studs = new BridgeStudOptions { Enabled = true, CountPerRow = 2, Diameter = 22, Height = 150, LongitudinalPitch = 200, TransversePitch = 120, Fu = 450, GammaV = 1.25,
            HeadDiameter = 35, HeadThickness = 10, RequiredCover = 20 };
        var transverse = new BridgeTransverseReinforcement { Enabled = true, TopDiameter = 12, TopPitch = 150, BottomDiameter = 12, BottomPitch = 150, CotTheta = 1.2,
            LeftFlowFraction = .5, AnchorageLength = 800, GoodBond = true, LeftConcreteEdge = 1000, RightConcreteEdge = 1000 };
        var with = HBridgeSection.Calculate(Box() with { Studs = studs, Transverse = transverse });
        var without = HBridgeSection.Calculate(Box(t1: 0, t2: 0, t3: 0) with { Studs = studs, Transverse = transverse });
        var a = with.Stages.Last(); var b = without.Stages.Last();
        double q = a.Torsion!.SlabFlow;
        Assert.AreEqual(b.Studs!.Flow, a.Studs!.Flow, 1e-9, "bending flow unchanged");
        Assert.AreEqual(Math.Abs(b.Studs.Flow) / 2 * 200 / 2 / 1000 + q * 200 / 2 / 1000, a.Studs.ForcePerStud, 1e-9, "half the flow of bending + q on one flange");
        // slab surfaces: + q on both a–a (the internal one of each flange) and on b–b
        double Demand(BridgeStage s, string name) => s.Studs!.Checks.Single(c => c.Name == name + " · puntone CLS").Demand;
        Assert.AreEqual(Demand(b, "Soletta a–a sinistra") + q, Demand(a, "Soletta a–a sinistra"), 1e-9);
        Assert.AreEqual(Demand(b, "Soletta a–a destra") + q, Demand(a, "Soletta a–a destra"), 1e-9);
        Assert.AreEqual(Demand(b, "Soletta b–b · gruppo 2") + q, Demand(a, "Soletta b–b · gruppo 2"), 1e-9);
        // longitudinal reinforcement of the torsion: q cot θ
        var longitudinal = a.Torsion.Checks.Single(c => c.Name == "Soletta · armatura longitudinale per torsione");
        Assert.AreEqual(q * 1.2, longitudinal.Demand, 1e-9);
    }

    [TestMethod]
    public void SupportTorqueAddsTheCoupleOfTheBearings()
    {
        var support = new BridgeSupportStiffener { Enabled = true, Layout = BridgeStiffenerLayout.Symmetric, Width = 200, Thickness = 20, LengthFactor = 1, LeftPanel = 2000,
            RightPanel = 2000, ReactionKN = 3000, FootprintLength = 400, FootprintWidth = 800, Location = BridgeSupportLocation.Internal };
        var options = new BridgeBoxOptions { Enabled = true, BracingThickness = 4, SupportTorqueKNm = 1500, BearingSpacing = 1300, SupportDiaphragmThickness = 15 };
        var with = HBridgeSection.Calculate(Box(t1: 0, t2: 0, t3: 0, box: options) with { Support = support });
        var without = HBridgeSection.Calculate(Box(t1: 0, t2: 0, t3: 0) with { Support = support });
        double Nst(HBridgeAnalysisResult r) => r.Stages.Last().Shear!.Details!.Single(x => x.Name == "Appoggio · Nst").Value;
        Assert.AreEqual(Nst(without) + 1500e6 / 1300 / 1000, Nst(with), 1e-9, "R/2 + T/e_b on one web");
        var diaphragm = with.Stages.Last().Torsion!.Checks.Single(c => c.Name == "Diaframma d'appoggio · taglio da torsione");
        double yb = -Tt - Hw - Tb / 2, a0 = (Half(-Tt / 2) + Half(yb)) * (-Tt / 2 - yb);
        Assert.AreEqual(1500e6 / (2 * a0 * 15), diaphragm.Demand, 1e-9);
    }

    [TestMethod]
    public void DistortionOfTheBoxWithDiaphragms()
    {
        BridgeBoxOptions Options(double spacing, BridgeDiaphragmKind kind = BridgeDiaphragmKind.Plate) => new()
        {
            Enabled = true, BracingThickness = 4, SpanLength = 40000, DiaphragmSpacing = spacing, DiaphragmKind = kind, DiaphragmThickness = 10,
            BracingArea = 3000, BracingRadius = 30, DistributedTorque = 60, ConcentratedTorque = 600
        };
        var none = HBridgeSection.Calculate(Box(box: Options(0))).Stages.Last().Torsion!.Distortion!;
        var wide = HBridgeSection.Calculate(Box(box: Options(10000))).Stages.Last().Torsion!.Distortion!;
        var closeResult = HBridgeSection.Calculate(Box(box: Options(5000)));
        var close = closeResult.Stages.Last().Torsion!.Distortion!;
        Assert.IsTrue(close.WarpingStressBottom < wide.WarpingStressBottom && wide.WarpingStressBottom < none.WarpingStressBottom);
        Assert.IsTrue(close.CornerMomentBottom < wide.CornerMomentBottom && wide.CornerMomentBottom < none.CornerMomentBottom);
        // the section of the analysis: the composite cell with the short term slab, outstands and top flanges
        var g = closeResult.Geometry; var m = closeResult.Materials;
        double yb = -Tt - Hw - Tb / 2, n0 = m.Ea / m.Ec;
        var section = new BoxDistortionSection(2 * Half(Hc / 2), 2 * Half(yb), Hc / 2 - yb, Hc / n0, Tw, Tb, m.Ec * Math.Pow(Hc, 3) / (12 * .96),
            m.Ea * Math.Pow(Tw, 3) / (12 * .91), m.Ea * Math.Pow(Tb, 3) / (12 * .91), 1500 - Half(Hc / 2), 700 - Half(yb), 450 * Tt);
        var mode = BoxDistortion.Mode(section);
        Assert.AreEqual(mode.WarpingInertia, close.WarpingInertia, 1e-9 * mode.WarpingInertia);
        Assert.AreEqual(mode.FrameStiffness, close.FrameStiffness, 1e-9 * mode.FrameStiffness);
        Assert.AreEqual(BoxDistortion.PlateDiaphragmStiffness(mode, 10, m.Ea), close.DiaphragmStiffness, 1e-9 * close.DiaphragmStiffness);
        var envelope = BoxDistortion.Envelope(40000, 5000, m.Ea * mode.WarpingInertia, mode.FrameStiffness, close.DiaphragmStiffness,
            60e3 * Math.Abs(mode.TorqueLoad), 600e6 * Math.Abs(mode.TorqueLoad));
        double warping = new[] { mode.Warping[2], mode.Warping[3], mode.OutstandWarping[2], mode.OutstandWarping[3] }.Max(Math.Abs);
        Assert.AreEqual(envelope.MaxMoment * warping / mode.WarpingInertia, close.WarpingStressBottom, 1e-9 * close.WarpingStressBottom);
        Assert.AreEqual(7, close.Diaphragms);
        var checks = closeResult.Stages.Last().Torsion!.Checks;
        Assert.IsTrue(checks.Any(c => c.Name == "Diaframma intermedio · taglio e imbozzamento" && c.Ratio > 0));
        Assert.IsTrue(checks.Any(c => c.Name == "Distorsione · nodo anima–fondo (σx, σz, τ)" && c.Ratio > 0));
        // cross-bracing
        var braced = HBridgeSection.Calculate(Box(box: Options(5000, BridgeDiaphragmKind.CrossBracing))).Stages.Last().Torsion!;
        Assert.IsTrue(braced.Checks.Any(c => c.Name == "Diaframma intermedio · diagonale compressa" && c.Ratio > 0));
        Assert.AreEqual(BoxDistortion.BracingStiffness(mode, m.Ea * 3000), braced.Distortion!.DiaphragmStiffness, 1e-9 * braced.Distortion.DiaphragmStiffness);
        // the distortion is analysed only with the slab
        Assert.IsNull(HBridgeSection.Calculate(Box(box: Options(5000))).Stages[0].Torsion!.Distortion);
    }

    [TestMethod]
    public void BottomFlangeChecksIncludeTheTorsion()
    {
        var r = HBridgeSection.Calculate(Box());
        var stage = r.Stages.Last(); var g = r.Geometry;
        double q = stage.Torsion!.BottomFlow;
        var eq = stage.Torsion.Checks.Single(c => c.Name == "Fondo · tensione equivalente (σ, τ torsione + taglio)");
        double sigma = Math.Max(Math.Abs(stage.Contributions.Sum(c => c.SteelStress(-g.Height))), Math.Abs(stage.Contributions.Sum(c => c.SteelStress(-Tt - Hw))));
        // bending shear of the bottom flange at the webs, from the gross properties of each phase
        double half = (g.WebSpacingBottom - g.WebHorizontalThickness) / 2, yb = -Tt - Hw - Tb / 2, shear = 0;
        var input = Box();
        for (int i = 0; i < 3; i++)
        {
            var p = HBridgeSection.GrossPhaseProperties(input, input.Phases[i]);
            shear += stage.Contributions[i].V * 1000 * half * (yb - p.Y) / p.Ix;
        }
        double tau = q / Tb + Math.Abs(shear);
        Assert.AreEqual(Math.Sqrt(sigma * sigma + 3 * tau * tau), eq.Demand, 1e-9 * eq.Demand);
        var buckling = stage.Torsion.Checks.Single(c => c.Name == "Fondo · imbozzamento a taglio");
        Assert.AreEqual(q / Tb + Math.Abs(shear) / 2, buckling.Demand, 1e-9);
        var panel = BridgeShearConnection.Web(g.BottomInternalWidth, Tb, 355, 210000, 1.05, 1.1, 1);
        Assert.AreEqual(panel.Resistance / panel.Area, buckling.Resistance, 1e-9);
    }

    [TestMethod]
    public void TorqueOnlyForTheBox()
    {
        var h = Box() with { Geometry = Box().Geometry with { SectionType = BridgeSteelSectionType.H, TopWidth = 500, WebSpacing = 0, WebOffset = 0 } };
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(h), "straight bending for the H");
        var inclined = h with { Geometry = h.Geometry with { SectionType = BridgeSteelSectionType.InclinedWebH, WebOffset = 200 } };
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(inclined), "straight bending for the inclined web");
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(Box(enabled: false)), "torque without the torsion checks");
        // steel phase without bracing: open section, torsion not verified
        var open = HBridgeSection.Calculate(Box(box: new() { Enabled = true })).Stages[0].Torsion!;
        Assert.IsFalse(open.Flows[0].Closed);
        Assert.IsTrue(open.Checks.Any(c => c.Name == "Torsione · G1 · cassone aperto" && c.Ratio is null));
        Assert.ThrowsException<ArgumentException>(() => HBridgeSection.Calculate(Box(box: new() { Enabled = true, DistributedTorque = 10 })), "distortion without the span");
    }
}
