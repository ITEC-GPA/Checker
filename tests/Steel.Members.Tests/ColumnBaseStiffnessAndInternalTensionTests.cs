using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Reporting;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Opt-in extensions of ColumnBase.Verify (15/11/2026): rotational stiffness (EN1993-1-8 6.3.4, Tables 6.11
/// and 6.12), anchor tension between the flanges (Table6.6 row next to the flange with the Figure6.11 alpha, column web
/// in tension 6.2.6.3) and tensioned anchors in shear (Table3.4 combined rule). Expected values come from
/// ReferenceCalculations/column_base_stiffness_internal_tension.py, which also checks Table6.12 against an independent
/// two-spring model; the default results of ColumnBaseTests are unchanged.</summary>
public partial class ColumnBaseTests
{
    private const string Ecm = "Ecm C25/30 = 31000 MPa, EN1992-1-1 Table3.1 (NTC2018 (11.2.5) gives the same value)";
    private static ConnectionRolledISection Heb500() => new(500, 300, 14.5, 28, 27, 2 * 300 * 28 + (500 - 56) * 14.5 + 4 * (1 - Math.PI / 4) * 27 * 27, 275, 430);

    /// <summary>HEB500 S275 on a 300x600x30 plate, four M24 5.6 between the flanges (gauge 140, rows at +/-180), N=+50 kN, M1=60 kNm.</summary>
    private static Spec Internal() => new()
    {
        Column = Heb500(), Mc = 4815e3 * 275, McSource = "HEB500 S275 class 1, Wpl fy/gammaM0, V2<0.5Vpl",
        PlateWidth = 300, PlateLength = 600, Layout = ColumnBaseAnchorLayout.BetweenFlanges, Gauge = 140, Row = 180,
        Ac1Source = "Block 1200x900x800: concentric 600x1200 (factor 2) within edges and depth, EN1992-1-1 6.7(3)",
        N = 50e3, M1 = 60e6, V2 = 20e3
    };

    [TestMethod]
    public void DefaultResultsCarryNoStiffnessAndKeepTheirConditions()
    {
        var r = ColumnBase.Verify(Example1().Build());
        Assert.IsNull(r.RotationalStiffness);
        Assert.IsFalse(r.Trace.Any(v => v.Name.StartsWith("Stiffness.") || v.Name.StartsWith("Shear.Interaction.")));
        StringAssert.StartsWith(r.ExternalConditions.Last(), "The joint stiffness (EN1993-1-8 6.3.4)");
        Assert.IsFalse(r.Scope.Contains("Opt-in"));
        var report = StandardConnectionCaseReport.Create(r);
        Assert.IsFalse(report.Results.Any(v => v.Name.Contains("RotationalStiffness")));
    }

    [TestMethod]
    public void AnOptionThatDoesNotApplyLeavesEveryValueUnchanged()
    {
        // Review B8: full comparison, not only the absence of new entries. Example 3 has no anchor tension in any state, so the
        // tension-shear option cannot act: checks, trace, requirements, conditions and report values must be identical.
        var plain = ColumnBase.Verify(Example3().Build());
        var option = ColumnBase.Verify(Example3().Build().WithTensionShearInteraction("EN1992-4 interaction declared"));
        Assert.AreEqual(plain.Status, option.Status); Assert.AreEqual(plain.State, option.State);
        CollectionAssert.AreEqual(plain.MissingRequirements.ToList(), option.MissingRequirements.ToList());
        Assert.AreEqual(plain.Checks.Count, option.Checks.Count);
        for (int i = 0; i < plain.Checks.Count; i++)
        {
            var a = plain.Checks[i]; var b = option.Checks[i];
            Assert.AreEqual(a.Id, b.Id); Assert.AreEqual(a.Demand, b.Demand, a.Id); Assert.AreEqual(a.Resistance, b.Resistance, a.Id);
            Assert.AreEqual(a.Reference, b.Reference, a.Id); Assert.AreEqual(a.Unit, b.Unit, a.Id); Assert.AreEqual(a.Role, b.Role, a.Id);
        }
        CollectionAssert.AreEqual(plain.Trace.Select(v => (v.Name, v.Value, v.Unit)).ToList(), option.Trace.Select(v => (v.Name, v.Value, v.Unit)).ToList());
        CollectionAssert.AreEqual(plain.ExternalConditions.ToList(), option.ExternalConditions.ToList());
        Assert.AreEqual(plain.ShearResistance, option.ShearResistance); Assert.AreEqual(plain.AxialResistance, option.AxialResistance);
        var pr = StandardConnectionCaseReport.Create(plain); var opr = StandardConnectionCaseReport.Create(option);
        CollectionAssert.AreEqual(pr.Results.Select(v => (v.Name, v.Value)).ToList(), opr.Results.Select(v => (v.Name, v.Value)).ToList());
        CollectionAssert.AreEqual(pr.Inputs.Select(v => (v.Name, v.Value)).ToList(), opr.Inputs.Select(v => (v.Name, v.Value)).ToList());
        // Only the declaration itself shows: scope sentence and source.
        Assert.AreEqual(plain.Scope + " Opt-in: tensioned anchors in shear by the Table3.4 combined rule without prying.", option.Scope);
        CollectionAssert.AreEqual(pr.Sources.ToList(), opr.Sources.Where(s => s != "EN1992-4 interaction declared").ToList());
    }

    [TestMethod]
    public void Example1StiffnessFollowsTable612AndTheTwoSpringModel()
    {
        // Compression (Table6.11 k13, note 1: c = 1.25 tp = 37.5): beff = 17 + 37.5 + 37.5 = 92, leff = 2*min(120+37.5, 200) = 315,
        // k13 = 31000*sqrt(92*315)/(1.275*210000) = 19.709763 mm. Tension (outer row, no prying: Lb = 266.75 > Lb* = 39.50):
        // k15 = 0.425*192.05*30^3/40.4^3 = 33.421267 mm, k16 = 2.0*353/266.75 = 2.646673 mm, kT = 1/(1/k15 + 1/k16) = 2.452460 mm.
        // Table6.12 row 1 (side A = left in tension, e = M/N = -200 <= -zC): z = 170 + 110.294477 = 280.294477,
        // ek = (zC k13 - zT kT)/(kT + k13) = 79.277243, e/(e+ek) = 1.656688, Sj,ini = 1.656688*210000*z^2/(1/kT + 1/k13)
        // = 5.96155e10 Nmm/rad (two-spring model M/phi: same value). mu = (1.5*0.872837)^2.7 = 2.069980 (Table6.8 psi 2.7).
        var r = ColumnBase.Verify(Example1().Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31000, Ecm)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        var s = r.RotationalStiffness!;
        Close(19.70976312526361, Trace(r, "Stiffness.K13"), "k13"); Close(33.421267031430624, Trace(r, "Stiffness.K15"), "k15");
        Close(2.6466729147141517, Trace(r, "Stiffness.K16"), "k16"); Close(2.4524595072408966, Trace(r, "Stiffness.KT"), "kT");
        Close(2.4524595072408966, s.SideAStiffnessCoefficient, "kA"); Close(19.70976312526361, s.SideBStiffnessCoefficient, "kB");
        Close(280.2944774115439, s.LeverArm, "z"); Close(-200, s.Eccentricity!.Value, "e"); Close(79.27724293313649, s.Ek, "ek");
        // Review M2: Table6.12 row 1 with N <= 0 holds for e <= -zC,B = -110.294477 mm, i.e. M1 >= 66.18 kNm at N = -600 kN.
        Assert.IsNull(s.EccentricityMin); Close(-110.29447741154388, s.EccentricityMax!.Value, "e max");
        Close(110.29447741154388 * 600e3, s.MomentAtLowerLimit!.Value, "M1 at the limit"); Assert.IsNull(s.MomentAtUpperLimit);
        Assert.IsFalse(s.HingeBeyondRange);
        Close(59615496349.82951, s.InitialStiffness, "Sj,ini"); Close(2.0699795933422815, s.Mu, "mu");
        Close(28800040609.855324, s.Stiffness, "Sj");
        var report = StandardConnectionCaseReport.Create(r);
        Close(59615496349.82951, report.Results.Single(v => v.Name == "Joint.InitialRotationalStiffness").Value, "report");
        StringAssert.Contains(r.ExternalConditions.Last(), "6.3.4 is computed");
        Assert.IsTrue(report.Sources.Contains(Ecm));
    }

    [TestMethod]
    public void Example2NtcStiffnessUsesThePryingCoefficients()
    {
        // NTC, plate 15 mm, prying develops (Lb = 217 <= Lb* = 408.92): k15 = 0.85*200*15^3/50.4^3 = 4.481579 mm,
        // k16 = 1.6*245/217 = 1.806452 mm. Holes at y = 180 do not reach the bearing area (y <= 152.25): zC = 111.5 mm.
        // e = 25e6/50e3 = 500 >= zT: row 1, ek = 87.327354, e/(e+ek) = 0.851314, Sj,ini = 1.79363e10 Nmm/rad,
        // u = 0.897083 -> mu = (1.5u)^2.7 = 2.228919.
        var r = ColumnBase.Verify(Example2().Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31000, Ecm)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Close(4.48157933808444, Trace(r, "Stiffness.K15"), "k15"); Close(1.8064516129032258, Trace(r, "Stiffness.K16"), "k16");
        Close(14.238425237083339, Trace(r, "Stiffness.K13"), "k13");
        Close(87.3273539882477, r.RotationalStiffness!.Ek, "ek"); Close(0.851314001646184, Trace(r, "Stiffness.EccentricityFactor"), "e/(e+ek)");
        Close(17936338091.526077, r.RotationalStiffness.InitialStiffness, "Sj,ini");
        Close(2.228918715985935, r.RotationalStiffness.Mu, "mu"); Close(8047102822.94532, r.RotationalStiffness.Stiffness, "Sj");
    }

    [TestMethod]
    public void PureCompressionTakesTheSymmetricLimitOfTable612()
    {
        // Example 3 (M1 = 0, N = -500 kN): both flange T-stubs in compression, ek = 0, e/(e+ek) = 1, mu = 1 (Mj,Ed = 0).
        // fjd = 2/3*16.6667 = 11.1111 (Ac1 = Ac0), c = 20*sqrt(275/(3*11.1111)) = 57.4456: bearing y from 103-57.4456 to 120+30,
        // no hole inside, zC = (45.5544+150)/2 = 97.7772. k13: c' = 25, beff = 17+25+25 = 67, leff = 2*min(145,150) = 290.
        var r = ColumnBase.Verify(Example3().Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31000, Ecm)));
        Assert.AreEqual(ColumnBaseEquilibriumState.AxialCompressionOnly, r.State);
        double c = 20 * Math.Sqrt(275 / (3 * (2.0 / 3 * 25 / 1.5))), zc = (103 - c + 150) / 2;
        double k13 = 31000 * Math.Sqrt(67.0 * 290) / (1.275 * 210000), z = 2 * zc;
        Close(zc, r.CompressionLeverArm!.Value, "zC"); Close(k13, Trace(r, "Stiffness.K13"), "k13");
        Close(0, r.RotationalStiffness!.Ek, "ek"); Close(1, r.RotationalStiffness.Mu, "mu");
        Close(210000 * z * z / (2 / k13), r.RotationalStiffness.InitialStiffness, "Sj,ini");
        Close(r.RotationalStiffness.InitialStiffness, r.RotationalStiffness.Stiffness, "Sj = Sj,ini");
        // Review M2: valid only while both flanges stay compressed, |e| < zC = 97.7772 mm, i.e. |M1| < 48.89 kNm at N = -500 kN;
        // the anchors on the axis carry no tension, so beyond it the base is a hinge.
        Close(-zc, r.RotationalStiffness.EccentricityMin!.Value, "e min"); Close(zc, r.RotationalStiffness.EccentricityMax!.Value, "e max");
        Close(-zc * 500e3, r.RotationalStiffness.MomentAtLowerLimit!.Value, "M1 low"); Close(zc * 500e3, r.RotationalStiffness.MomentAtUpperLimit!.Value, "M1 high");
        Assert.IsTrue(r.RotationalStiffness.HingeBeyondRange);
        Assert.AreEqual(1, Trace(r, "Stiffness.HingeBeyondValidRange"));
        StringAssert.Contains(r.ExternalConditions.Last(), "hinge beyond it");
    }

    [TestMethod]
    public void StiffnessIsNotDefinedWithoutActionsAndNeverInfinite()
    {
        // Review B3: N = M1 = 0 (shear only): no Table6.12 row, a trace says so and the conditions do not claim a value.
        var none = ColumnBase.Verify((Example1() with { N = 0, M1 = 0, V2 = 20e3 }).Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31000, Ecm)));
        Assert.AreEqual(ColumnBaseEquilibriumState.NoAxialForceOrMoment, none.State);
        Assert.AreEqual(0, none.MissingRequirements.Count, string.Join(";", none.MissingRequirements));
        Assert.IsNull(none.RotationalStiffness);
        Assert.AreEqual(1, Trace(none, "Stiffness.NotDefined"));
        StringAssert.Contains(none.ExternalConditions.Last(), "requested but is not defined");
        // Pure moment (N = 0): e is null, not infinite, and the result serializes.
        var moment = ColumnBase.Verify((Example1() with { N = 0, M1 = 50e6, V2 = 50e3 }).Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31000, Ecm)));
        Assert.IsNotNull(moment.RotationalStiffness, string.Join(";", moment.MissingRequirements));
        Assert.IsNull(moment.RotationalStiffness!.Eccentricity);
        Close(1, Trace(moment, "Stiffness.EccentricityFactor"), "e/(e+ek) limit");
        Assert.IsNull(moment.RotationalStiffness.MomentAtLowerLimit);
        StringAssert.Contains(System.Text.Json.JsonSerializer.Serialize(moment.RotationalStiffness), "\"Eccentricity\":null");
        // Review B4: Ec in GPa (31) is outside the MPa field and refused, not used.
        var gpa = ColumnBase.Verify(Example1().Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31, Ecm)));
        CollectionAssert.Contains(gpa.MissingRequirements.ToList(), "CONCRETE_MODULUS_OUTSIDE_10000_TO_60000_MPA");
        Assert.IsNull(gpa.RotationalStiffness);
    }

    [TestMethod]
    public void StiffnessNeedsTheSourceOfTheConcreteModulus()
    {
        var r = ColumnBase.Verify(Example1().Build().WithStiffness(new ColumnBaseStiffnessDeclaration(31000, " ")));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        CollectionAssert.Contains(r.MissingRequirements.ToList(), "CONCRETE_MODULUS_SOURCE_REQUIRED");
        Assert.IsNull(r.RotationalStiffness);
    }

    [TestMethod]
    public void AnchorTensionBetweenTheFlangesWithADeclaredAlpha()
    {
        // HEB500, rows at +/-180 (first row 222-180 = 42 mm from the inner face of the tension flange). Table6.6 first row
        // below the flange: m = (140-14.5)/2 - 0.8*8 = 56.35, e = (300-140)/2 = 80, m2 = 42 - 0.8*12 = 32.4,
        // lambda1 = 0.413275, lambda2 = 0.237624; declared alpha 6.0 >= 4 + 1.25 e/m = 5.7746 (asymptote).
        // leff,cp = 2 pi m = 354.06, leff,nc = alpha m = 338.1 -> leff,1 = 338.1, Mpl,1 = 0.25*338.1*900*275 = 20919937.5 Nmm,
        // plate 2Mpl,1/m = 742500 N; anchors min(127080, punching, 110000) -> Mode3 = 220000 N; Lb = 266.75 > Lb* = 60.888 (no prying).
        // Column web in tension (6.2.8.3(2) -> 6.2.6.3): Avc = A - 2 b tf + (tw + 2r) tf = 8981.779 mm2, beff = 338.1,
        // omega1 = 1/sqrt(1 + 1.3 (338.1*14.5/8981.779)^2) = 0.849015, Ft,wc,Rd = 0.849015*338.1*14.5*275 = 1144619.2 N.
        // Unrestricted end pattern 2m + 0.625e = 162.7 <= 180: the two side T-stubs stay in their halves.
        // Compression under flange B: c = 60.930, y from 161.070 to 300, two whole holes (y 167..193) deducted:
        // A = 300*138.930 - 2 pi 13^2 = 40617.228, zC = 231.855991. Table6.7 row 1: FA = (N zC + M)/z = 173829.69 N,
        // u = 173829.69/220000 = 0.790135, Mj,Rd = 75936394.70 Nmm.
        var input = Internal().Build().WithInternalRowAlpha(new TStubChartAlpha(6.0, "Figure 6.11 read for lambda1 0.413, lambda2 0.238 (test input)"))
            .WithStiffness(new ColumnBaseStiffnessDeclaration(31000, Ecm));
        var r = ColumnBase.Verify(input);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements) + string.Join(";", r.Checks.Where(c => !c.Passed).Select(c => c.Id)));
        Assert.AreEqual(ColumnBaseEquilibriumState.SideATensionSideBCompression, r.State);
        Close(56.35, Trace(r, "Tension.TStub.M"), "m"); Close(32.4, Trace(r, "Tension.TStub.M2"), "m2");
        Close(0.41327466079941333, Trace(r, "Tension.TStub.Lambda1"), "lambda1"); Close(0.2376237623762376, Trace(r, "Tension.TStub.Lambda2"), "lambda2");
        Close(20919937.5, Trace(r, "Tension.TStub.Mpl1"), "Mpl1"); Close(60.88778897530865, Trace(r, "Tension.Anchor.PryingLengthLimit"), "Lb*");
        Close(162.7, Trace(r, "Tension.InternalRow.EndPatternLength"), "2m+0.625e");
        Close(8981.778955533042, Trace(r, "Tension.ColumnWeb.ShearArea"), "Avc"); Close(0.8490146011487999, Trace(r, "Tension.ColumnWeb.Omega1"), "omega1");
        Close(1144619.198635532, Check(r, "SideA.ColumnWebInTension").Resistance, "Ft,wc,Rd");
        Close(220000, r.SideTensionResistance!.Value, "FT"); Close(180, r.TensionLeverArm!.Value, "zT");
        Close(40617.228093517566, Trace(r, "Compression.FlangeTStub.EffectiveArea"), "A"); Close(231.85599136377922, r.CompressionLeverArm!.Value, "zC");
        Close(173829.69064289593, Check(r, "SideA.TStub.Mode3").Demand, "FA"); Close(75936394.7043845, r.MomentResistance!.Value, "Mj,Rd");
        StringAssert.Contains(Check(r, "SideA.TStub.Mode3").Reference, "Table6.6 first row below the flange");
        // Stiffness: k15 = 0.425*338.1*30^3/56.35^3 = 21.682837, k16 = 2.646673, k13 (beff 103, leff 300) = 20.352204;
        // e = 1200 > zT: ek = 189.080707, Sj,ini = 6.50463e10, mu = (1.5*0.790135)^2.7 = 1.582122.
        Close(21.682836940184814, Trace(r, "Stiffness.K15"), "k15"); Close(20.352204323759302, Trace(r, "Stiffness.K13"), "k13");
        Close(65046331833.32463, r.RotationalStiffness!.InitialStiffness, "Sj,ini"); Close(1.582121549235309, r.RotationalStiffness.Mu, "mu");
        Close(41113359377.959076, r.RotationalStiffness.Stiffness, "Sj");
        StringAssert.Contains(r.Scope, "anchor tension between the flanges");
    }

    [TestMethod]
    public void ColumnWebInTensionGovernsWithStrongAnchorsAndAThickPlate()
    {
        // Review M1 (16/11): same HEB500 geometry and alpha 6.0, four M36 10.9 between the flanges (As 817, hole 39, washer 5,
        // nut 29, dm 57.9), declared concrete 600 kN per anchor, plate 40 mm, V = 0, N = +50 kN, M1 = 60 kNm.
        // Anchor steel 0.9*1000*817/1.25 = 588240 N -> Mode3 = 1176480 N; plate Mpl,1 = 0.25*338.1*40^2*275 = 37191000 Nmm,
        // 2Mpl,1/m = 1320000 N; Lb = 8*36 + 30 + 40 + 5 + 29/2 = 377.5 > Lb* = 59.451 (no prying);
        // Ft,wc,Rd = omega1 beff twc fy = 0.849015*338.1*14.5*275 = 1144619.20 N governs FT (reference script, InternalWebGoverns).
        // Compression: c = 40*2.03101 = 81.240, bearing y 140.760..300 with two whole holes r = 19.5 at y = 180: zC = 222.505600.
        // FA = (N zC + M)/(zT + zC) = 176706.31 N, u = FA/FT = 0.154380, Mj,Rd = 388651380.19 Nmm.
        var spec = Internal() with { PlateThickness = 40, Diameter = 36, TensileArea = 817, Hole = 39, Fyb = 900, Fub = 1000, Washer = 5, Nut = 29, Dm = 57.9,
            AnchorSource = "EN ISO 898-1 class 10.9 rods, rolled threads to EN1090-2", ConcreteTension = 600e3, V2 = 0 };
        var r = ColumnBase.Verify(spec.Build().WithInternalRowAlpha(new TStubChartAlpha(6.0, "Figure 6.11 read for lambda1 0.413, lambda2 0.238 (test input)")));
        Assert.AreEqual(0, r.MissingRequirements.Count, string.Join(";", r.MissingRequirements));
        Close(588240, Trace(r, "Tension.Anchor.SteelTensionResistance"), "Ft,Rd anchor"); Close(1176480, Trace(r, "Tension.TStub.Mode3"), "Mode3");
        Close(1320000, Trace(r, "Tension.TStub.PlateWithoutPrying"), "plate"); Close(59.45129855729168, Trace(r, "Tension.Anchor.PryingLengthLimit"), "Lb*");
        Close(1144619.198635532, Trace(r, "Tension.ColumnWeb.FtWcRd"), "Ft,wc,Rd");
        Close(1144619.198635532, r.SideTensionResistance!.Value, "FT = Ft,wc,Rd");
        Close(222.50559998936785, r.CompressionLeverArm!.Value, "zC");
        Close(176706.31166708528, Check(r, "SideA.ColumnWebInTension").Demand, "FA");
        Close(388651380.1924613, r.MomentResistance!.Value, "Mj,Rd");
        // The web is the governing tension component of side A.
        var tension = r.Checks.Where(c => c.Id.StartsWith("SideA.")).OrderByDescending(c => c.Utilization).First();
        Assert.AreEqual("SideA.ColumnWebInTension", tension.Id);
    }

    [TestMethod]
    public void AnchorTensionBetweenTheFlangesWithTheSciAlphaInversion()
    {
        // SCI P398 Appendix G inversion at lambda1 = 0.413275, lambda2 = 0.237624: alpha = 7.212330, alpha m = 406.41 > 2 pi m:
        // leff,1 = 354.057, Mpl,1 = 21907307.32 Nmm, Lb* = 58.1436; the anchors (Mode3 = 220000 N) still govern.
        var r = ColumnBase.Verify(Internal().Build().WithSciP398InternalRowAlpha("SCI P398 Appendix G, numerical inversion accepted by the designer"));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.MissingRequirements));
        Close(7.212329657677953, Trace(r, "Tension.TStub.Alpha"), "alpha");
        Close(21907307.321185872, Trace(r, "Tension.TStub.Mpl1"), "Mpl1"); Close(58.14355553613948, Trace(r, "Tension.Anchor.PryingLengthLimit"), "Lb*");
        Close(75936394.7043845, r.MomentResistance!.Value, "Mj,Rd");
        StringAssert.Contains(Check(r, "SideA.TStub.Mode3").Reference, "SCI P398 Appendix G");
    }

    [TestMethod]
    public void InternalTensionRefusalsAreExplicit()
    {
        // Default unchanged: no alpha, no tension between the flanges.
        var plain = ColumnBase.Verify(Internal().Build());
        CollectionAssert.Contains(plain.MissingRequirements.ToList(), "ANCHOR_TENSION_BETWEEN_FLANGES_NOT_IMPLEMENTED");
        var alpha = new TStubChartAlpha(6.0, "test");
        // Rows at +/-150 < 2m + 0.625e = 162.7 with BOTH sides in tension (N = +300 kN, M1 = 10 kNm, e = 33 < zT):
        // the two side patterns would cross the column axis and share plate.
        var close = ColumnBase.Verify((Internal() with { Row = 150, N = 300e3, M1 = 10e6 }).Build().WithInternalRowAlpha(alpha));
        Assert.AreEqual(ColumnBaseEquilibriumState.BothSidesInTension, close.State);
        CollectionAssert.Contains(close.MissingRequirements.ToList(), "INTERNAL_TENSION_ROW_PATTERN_CROSSES_THE_COLUMN_AXIS");
        // Alpha below the unstiffened asymptote 5.7746 is rejected by the Table6.6 geometry.
        var low = ColumnBase.Verify(Internal().Build().WithInternalRowAlpha(new TStubChartAlpha(5.5, "test")));
        Assert.IsTrue(low.MissingRequirements.Any(m => m.Contains("AlphaBelowUnstiffenedAsymptote")), string.Join(";", low.MissingRequirements));
        var twice = ColumnBase.Verify(Internal().Build().WithInternalRowAlpha(alpha).WithSciP398InternalRowAlpha("SCI"));
        CollectionAssert.Contains(twice.MissingRequirements.ToList(), "INTERNAL_ROW_ALPHA_DECLARED_TWICE");
        var outside = ColumnBase.Verify(Example1().Build().WithInternalRowAlpha(alpha));
        CollectionAssert.Contains(outside.MissingRequirements.ToList(), "INTERNAL_ROW_ALPHA_REQUIRES_TWO_ROWS_BETWEEN_THE_FLANGES");
        foreach (var r in new[] { plain, close, low, twice, outside }) Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
    }

    [TestMethod]
    public void NonOverlapRuleAppliesOnlyToStatesThatNeedIt()
    {
        // Review B1 (16/11): rows at +/-150 < 2m + 0.625e = 162.7.
        var alpha = new TStubChartAlpha(6.0, "test");
        // Both flanges compressed (N = -500 kN, M1 = 20 kNm, e = -40 mm): no anchor in tension, same outcome as without alpha.
        var compressed = Internal() with { Row = 150, N = -500e3, M1 = 20e6 };
        var plain = ColumnBase.Verify(compressed.Build());
        var declared = ColumnBase.Verify(compressed.Build().WithInternalRowAlpha(alpha));
        Assert.AreEqual(ColumnBaseEquilibriumState.BothSidesInCompression, declared.State);
        Assert.AreEqual(plain.Status, declared.Status, string.Join(";", declared.MissingRequirements));
        CollectionAssert.AreEqual(plain.MissingRequirements.ToList(), declared.MissingRequirements.ToList());
        Assert.AreEqual(plain.Checks.Count, declared.Checks.Count);
        foreach (var c in plain.Checks)
        {
            var d = Check(declared, c.Id);
            Assert.AreEqual(c.Demand, d.Demand, c.Id); Assert.AreEqual(c.Resistance, d.Resistance, c.Id); Assert.AreEqual(c.Reference, d.Reference, c.Id);
        }
        Assert.AreEqual(plain.MomentResistance, declared.MomentResistance);
        // One side in tension (N = +50 kN, M1 = 60 kNm): its pattern reaches y = 150 - 162.7 = -12.7 mm, while the compressed
        // T-stub of side B starts at y = -(222 - 60.93) = -161.07 mm: no contact, the joint is computed.
        var oneSide = ColumnBase.Verify((Internal() with { Row = 150 }).Build().WithInternalRowAlpha(alpha));
        Assert.AreEqual(ColumnBaseEquilibriumState.SideATensionSideBCompression, oneSide.State);
        Assert.AreEqual(0, oneSide.MissingRequirements.Count, string.Join(";", oneSide.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Passed, oneSide.Status);
        // HEB240 on a 240x440x60 plate, rows at +/-60, gauge 100: c = 60 sqrt(275/(3*22.2222)) = 121.9 > (240-34)/2 = 103, so the
        // compressed T-stub of side B reaches the axis; m = 45 - 6.4 = 38.6, e = 70, 2m + 0.625e = 120.95 > 60: refused.
        var reach = ColumnBase.Verify((Example1() with { PlateWidth = 240, PlateThickness = 60, Layout = ColumnBaseAnchorLayout.BetweenFlanges,
            Gauge = 100, Row = 60, N = 50e3, M1 = 25e6, V2 = 20e3 }).Build().WithInternalRowAlpha(new TStubChartAlpha(6.5, "test")));
        CollectionAssert.Contains(reach.MissingRequirements.ToList(), "INTERNAL_TENSION_ROW_PATTERN_REACHES_THE_COMPRESSED_T_STUB");
        Assert.AreEqual(ConnectionStatus.NotSupported, reach.Status);
    }

    [TestMethod]
    public void TensionedAnchorsTakeShearByTable34WhenDeclared()
    {
        // Example 1 with V2 = 240 kN. Friction 0.2*600 = 120 kN; compressed anchors 2*min(F1, F2 = 0.35*500*353/1.25 = 49420,
        // bearing, 60000) = 98840 N -> 218840 N < 240000: without the opt-in the tensioned anchors are excluded (not supported).
        // Opt-in: Ft,Ed = FA/2 = 96012.08 N per anchor, Fv,Rd = 49420, Ft,Rd = 127080: Fv <= 49420 (1 - 96012.08/(1.4*127080))
        // = 22749.98 N per tensioned anchor (Table3.4 combined rule), Fv,Rd = 218840 + 2*22749.98 = 264339.95 N >= 240000.
        var spec = Example1() with { V2 = 240e3 };
        var without = ColumnBase.Verify(spec.Build());
        Assert.AreEqual(ConnectionStatus.NotSupported, without.Status);
        Assert.IsTrue(without.MissingRequirements.Any(m => m.StartsWith("SHEAR_EXCEEDS_RESISTANCE_WITHOUT_TENSIONED_ANCHORS")));
        var r = ColumnBase.Verify(spec.Build().WithTensionShearInteraction("EN1992-4 7.2.3 interaction of the declared group resistances, calculation AN-01 rev 2"));
        Assert.AreEqual(0, r.MissingRequirements.Count, string.Join(";", r.MissingRequirements));
        Close(96012.08352394204, Trace(r, "Shear.Interaction.SideA.AnchorTension"), "Ft,Ed");
        Close(22749.97679890499, Trace(r, "Shear.Interaction.SideA.AnchorShearResistance"), "Fv,Rd,red");
        Close(264339.95359781, r.ShearResistance!.Value, "Fv,Rd");
        var shear = Check(r, "Shear.FrictionAndAnchors");
        Assert.IsTrue(shear.Passed); StringAssert.Contains(shear.Reference, "Table3.4 combined rule");
        Assert.AreEqual(2, Trace(r, "Shear.Interaction.CreditedTensionedAnchors"));
        // The N-M1 resistance is unchanged by the shear option.
        Close(137482695.04752892, r.MomentResistance!.Value, "Mj,Rd");
    }

    [TestMethod]
    public void PryingKeepsTensionedAnchorsOutOfShear()
    {
        // Example 2 (prying, Lb <= Lb*): the anchor force is not F/2, so the opt-in credits nothing more.
        var plain = ColumnBase.Verify(Example2().Build());
        var r = ColumnBase.Verify(Example2().Build().WithTensionShearInteraction("EN1992-4 interaction declared"));
        Assert.AreEqual(1, Trace(r, "Shear.Interaction.PryingExcludesTensionedAnchors"));
        Assert.AreEqual(plain.ShearResistance, r.ShearResistance);
        Assert.IsFalse(r.Trace.Any(v => v.Name == "Shear.Interaction.CreditedTensionedAnchors"));
        // Review B2: shear beyond Fv,Rd (2 compressed anchors x F2,vb = 2*0.248*800*245/1.25 = 77772.8 N < 100 kN) with the option
        // declared: the requirement names prying, not a missing interaction model.
        var over = ColumnBase.Verify((Example2() with { V2 = 100e3 }).Build().WithTensionShearInteraction("EN1992-4 interaction declared"));
        Assert.AreEqual(ConnectionStatus.NotSupported, over.Status);
        Assert.IsTrue(over.MissingRequirements.Any(m => m.StartsWith("SHEAR_EXCEEDS_RESISTANCE:TENSIONED_ANCHORS_EXCLUDED_BY_PRYING")), string.Join(";", over.MissingRequirements));
        Assert.IsFalse(over.MissingRequirements.Any(m => m.Contains("NOT_IMPLEMENTED")));
        // Without the option the historical requirement is unchanged.
        var overPlain = ColumnBase.Verify((Example2() with { V2 = 100e3 }).Build());
        Assert.IsTrue(overPlain.MissingRequirements.Any(m => m.StartsWith("SHEAR_EXCEEDS_RESISTANCE_WITHOUT_TENSIONED_ANCHORS")));
    }
}
