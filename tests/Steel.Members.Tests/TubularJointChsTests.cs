using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Reporting;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Shared builders of the EN1993-1-8:2005 section7 tests. S355 hot finished, fu 510
/// (EN1993-1-1 Table3.1), EC factors gammaM0 1.0, gammaM2 1.25 and the recommended gammaM5 1.0.</summary>
internal static class TubularJointTestData
{
    internal const string Butt = "WPS full penetration all round, matching consumables, VT+UT";
    internal static TubularJointCode Ec(double gammaM5 = 1.0)
        => TubularJointCode.Eurocode3(ConnectionDesignCode.Eurocode3(1.0, 1.25, 1.25, "EN recommended values"), gammaM5, "EN1993-1-8 Table2.1 recommended value");
    internal static TubularJointSection Chs(double d, double t, int grade = 355, double fy = 355, double fu = 510)
        => TubularJointSection.Circular(d, t, grade, fy, fu, TubularFabrication.HotFinished);
    /// <summary>Sharp-corner area and moduli, admitted as product values (upper bounds).</summary>
    internal static TubularJointSection Rhs(double h, double b, double t, int grade = 355, double fy = 355, double fu = 510,
        TubularFabrication fabrication = TubularFabrication.HotFinished)
    {
        double hi = h - 2 * t, bi = b - 2 * t;
        return TubularJointSection.Rectangular(h, b, t, h * b - hi * bi, (b * h * h * h - bi * hi * hi * hi) / (6 * h),
            (h * b * b * b - hi * bi * bi * bi) / (6 * b), grade, fy, fu, fabrication);
    }
    internal static TubularBrace Brace(TubularJointSection s, double angle, double n, double mip = 0, double mop = 0, TubularBraceWeld? weld = null)
        => new(s, angle, n, weld ?? TubularBraceWeld.FullPenetration(Butt), mip, mop);
    internal static TubularChordActions Cut(double n, double v = 0, double mip = 0, double mop = 0) => new(n, v, mip, mop);
    internal static TubularJointInput Single(TubularJointType type, TubularJointSection chord, TubularChordActions a, TubularChordActions b,
        TubularBrace brace, TubularJointCode? code = null, string? actions = "Same ULS combination of the lattice FEM",
        string? analysis = "Pinned braces, continuous chords, L/h >= 6 (5.1.5(3))", string? throughThickness = null,
        bool multiplanar = false, bool reinforced = false, bool fatigue = false)
        => new(code ?? Ec(), type, chord, a, b, brace, null, 0, actions!, analysis!, TubularOverlappingBrace.A, false, throughThickness, multiplanar, reinforced, fatigue);
    internal static TubularJointInput Two(TubularJointType type, TubularJointSection chord, TubularChordActions a, TubularChordActions b,
        TubularBrace braceA, TubularBrace braceB, double gap, TubularOverlappingBrace overlapping = TubularOverlappingBrace.A,
        bool hiddenSeamWelded = false, TubularJointCode? code = null)
        => new(code ?? Ec(), type, chord, a, b, braceA, braceB, gap, "Same ULS combination of the lattice FEM",
            "Pinned braces, continuous chords, L/h >= 6 (5.1.5(3))", overlapping, hiddenSeamWelded);
    internal static ConnectionCheck Check(ConnectionVerification<TubularJointInput> r, string id) => r.Checks.Single(c => c.Id == id);
    internal static double Trace(ConnectionVerification<TubularJointInput> r, string name) => r.Trace.Single(v => v.Name == name).Value;
    internal static void Close(double expected, double actual, string? what = null)
        => Assert.AreEqual(expected, actual, Math.Abs(expected) * 1e-9 + 1e-9, what);
    internal static void Complete(ConnectionVerification<TubularJointInput> r)
    {
        Assert.AreEqual(0, r.Diagnostics.Count, string.Join(";", r.Diagnostics));
        Assert.IsTrue(r.IsComplete); Assert.AreEqual(ConnectionStatus.Passed, r.Status);
    }
    internal static double Sin45 => Math.Sin(Math.PI / 4);
}

/// <summary>CHS braces on CHS chords: Table7.1 field, Table7.2 axial resistances, Table7.5 and
/// eq.(7.3). Every expected value is a hand calculation written in the comment, not a regenerated output.</summary>
[TestClass]
public class TubularJointChsTests
{
    private static readonly TubularJointSection Chord = TubularJointTestData.Chs(168.3, 8);

    private static TubularJointInput TJoint(double n1 = -150000, TubularJointCode? code = null, TubularBraceWeld? weld = null,
        TubularJointSection? chord = null, TubularJointSection? brace = null)
        => TubularJointTestData.Single(TubularJointType.T, chord ?? Chord, TubularJointTestData.Cut(-300000, 0, 10e6),
            TubularJointTestData.Cut(-300000, 0, 8e6), TubularJointTestData.Brace(brace ?? TubularJointTestData.Chs(114.3, 6), 90, n1, weld: weld), code);

    [TestMethod]
    public void TJointChordFaceAndPunchingMatchTable72()
    {
        // Chord CHS 168.3x8 S355: A0 = pi/4 (168.3^2 - 152.3^2) = 4028.7784 mm2,
        // Wel0 = pi (168.3^4 - 152.3^4)/(32*168.3) = 154161.757 mm3. Both cuts N = -300 kN, M = 10 and 8 kNm:
        // sigma0 = sigmap = 300000/4028.7784 + 10e6/154161.757 = 74.4643 + 64.8669 = 139.3312 MPa
        // (T joint: no brace component along the chord). np = 139.3312/355 = 0.392482,
        // kp = 1 - 0.3*0.392482*1.392482 = 0.836043. gamma = 168.3/16 = 10.51875, beta = 114.3/168.3 = 0.679144.
        // Chord face: gamma^0.2 kp fy0 t0^2 (2.8 + 14.2 beta^2)/sin90 = 1.60039*0.836043*355*64*9.34960 = 284328.94 N.
        // Punching (114.3 <= 168.3-16): fy0/sqrt3 t0 pi d1 (1+1)/(2*1) = 204.960*8*pi*114.3 = 588781.04 N.
        var r = TubularJoint.Verify(TJoint());
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(0.392482224875735, TubularJointTestData.Trace(r, "Chord.np"));
        TubularJointTestData.Close(0.836042643484258, TubularJointTestData.Trace(r, "Joint.kp"));
        TubularJointTestData.Close(284328.942942100, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(588781.039222798, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
        Assert.AreEqual(2, r.Checks.Count);
        // Governing: 150000/284328.94 = 0.527558.
        TubularJointTestData.Close(150000 / 284328.942942100, r.Utilization!.Value);
        StringAssert.Contains(TubularJointTestData.Check(r, "BraceA.ChordFace").Reference, "EN1993-1-8:2005 Table7.2");
        var summary = SteelAssessmentSummary.From(r);
        Assert.AreEqual(EngineeringOutcome.Satisfied, summary.Outcome); Assert.IsTrue(summary.ExternalConditions.Any(x => x.Contains("5.1.5")));
    }

    [TestMethod]
    public void YJointWithBraceMomentsUsesTable75AndEquation73()
    {
        // Tension brace 88.9x5 at 60 deg, N1 = +120 kN, Mip = 3 kNm, Mop = 1.5 kNm. Chord in tension:
        // side A +200 kN, side B 200 + 120 cos60 = 260 kN (joint equilibrium), Mip0 = 2 kNm on both cuts.
        // sigmap: least compressed cut 260 kN: -260000/4028.7784 + 2e6/154161.757 = -64.5359 + 12.9736 = -51.5623 < 0 -> kp = 1.
        // beta = 88.9/168.3 = 0.528223, sin60 = 0.866025, gamma = 10.51875.
        // N face = gamma^0.2*355*64/0.866025*(2.8+14.2*0.279020) = 284021.40 N
        // N punching = 355/sqrt3*8*pi*88.9*(1+0.866025)/(2*0.75) = 569686.12 N
        // Mip face = 4.85*355*64*88.9/0.866025*sqrt(10.51875)*0.528223 = 19378533.4 Nmm
        // Mop face = 355*64*88.9/0.866025*2.7/(1-0.81*0.528223) = 11006307.4 Nmm
        // Mip punching = 355*8*88.9^2/sqrt3*(1+3*0.866025)/(4*0.75) = 15542122.9 Nmm
        // Mop punching = 355*8*88.9^2/sqrt3*(3+0.866025)/(4*0.75) = 16699546.7 Nmm
        // eq.(7.3): 120000/284021.40 + (3e6/15542122.9)^2 + 1.5e6/11006307.4 = 0.422505 + 0.037258 + 0.136285 = 0.596047.
        var brace = TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 60, 120000, 3e6, 1.5e6);
        var r = TubularJoint.Verify(TubularJointTestData.Single(TubularJointType.Y, Chord, TubularJointTestData.Cut(200000, 0, 2e6),
            TubularJointTestData.Cut(260000, 0, 2e6), brace));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(1, TubularJointTestData.Trace(r, "Joint.kp"));
        TubularJointTestData.Close(284021.395772475, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(569686.121125511, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
        TubularJointTestData.Close(19378533.4333909, TubularJointTestData.Check(r, "BraceA.ChordFace.InPlaneMoment").Resistance);
        TubularJointTestData.Close(11006307.4315167, TubularJointTestData.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
        TubularJointTestData.Close(15542122.8651775, TubularJointTestData.Check(r, "BraceA.PunchingShear.InPlaneMoment").Resistance);
        TubularJointTestData.Close(16699546.7288658, TubularJointTestData.Check(r, "BraceA.PunchingShear.OutOfPlaneMoment").Resistance);
        var interaction = TubularJointTestData.Check(r, "BraceA.Interaction");
        TubularJointTestData.Close(0.596047067824605, interaction.Demand); Assert.AreEqual(1, interaction.Resistance); Assert.AreEqual("1", interaction.Unit);
        TubularJointTestData.Close(0.596047067824605, r.Utilization!.Value);
    }

    [TestMethod]
    public void XJointUsesTheXChordFaceFormula()
    {
        // Chord 139.7x5 (d0/t0 = 27.94 <= 40), coaxial braces 76.1x4 at 90 deg, N1 = -60 kN, chord -100 kN both cuts.
        // A0 = pi/4 (139.7^2 - 129.7^2) = 2115.8627; sigmap = 100000/2115.8627 = 47.2620 MPa, np = 0.133133,
        // kp = 1 - 0.3*0.133133*1.133133 = 0.954743; beta = 76.1/139.7 = 0.544739.
        // Chord face X: kp fy0 t0^2/sin90 * 5.2/(1 - 0.81 beta) = 0.954743*355*25*5.2/0.558761 = 78855.43 N.
        // Punching: 355/sqrt3*5*pi*76.1 = 245003.48 N.
        var r = TubularJoint.Verify(TubularJointTestData.Single(TubularJointType.X, TubularJointTestData.Chs(139.7, 5),
            TubularJointTestData.Cut(-100000), TubularJointTestData.Cut(-100000), TubularJointTestData.Brace(TubularJointTestData.Chs(76.1, 4), 90, -60000)));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(0.954742961124726, TubularJointTestData.Trace(r, "Joint.kp"));
        TubularJointTestData.Close(78855.4280163733, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(245003.483622348, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
        Assert.IsTrue(r.Assumptions.Any(x => x.StartsWith("X joint")));
    }

    [TestMethod]
    public void KGapJointUsesKgOnTheCompressionBraceAndPunchingOnBoth()
    {
        // Braces 88.9x5 at 45/45, A compression -200 kN, B tension +200 kN, g = 20 mm >= t1+t2 = 10.
        // Chord: side A -400 kN; equilibrium N_B = N_A + N_A,br cos45 - N_B,br cos45 = -400 - 282.843 = -682.843 kN.
        // sigmap from the less compressed cut: 400000/4028.7784 = 99.2857 MPa, np = 0.279678,
        // kp = 1 - 0.3*0.279678*1.279678 = 0.892631. gamma = 10.51875: gamma^0.2 = 1.600388, gamma^1.2 = 16.83405,
        // kg = 1.600388*(1 + 0.024*16.83405/(1 + exp(0.5*20/8 - 1.33))) = 1.937483.
        // N1,Rd (brace A, compression) = kg kp fy0 t0^2/sin45 (1.8 + 10.2*88.9/168.3) = 399423.57 N; N2,Rd = sin45/sin45 N1,Rd.
        // Punching (88.9 <= 152.3): 355/sqrt3*8*pi*88.9*(1+0.707107)/(2*0.5) = 781753.86 N each.
        // e = (g + 2*88.9/(2 sin45)) sin45^2/sin90 - 84.15 = (20 + 125.7236)*0.5 - 84.15 = -11.288 mm, e/d0 = -0.0671.
        var r = TubularJoint.Verify(TubularJointTestData.Two(TubularJointType.K, Chord, TubularJointTestData.Cut(-400000),
            TubularJointTestData.Cut(-400000 - 400000 * TubularJointTestData.Sin45), TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 45, -200000),
            TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 45, 200000), 20));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(0.279677968407643, TubularJointTestData.Trace(r, "Chord.np"));
        TubularJointTestData.Close(1.93748257674502, TubularJointTestData.Trace(r, "Joint.kg"));
        TubularJointTestData.Close(-11.2882071525159, TubularJointTestData.Trace(r, "Joint.Eccentricity"));
        TubularJointTestData.Close(399423.566142559, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(399423.566142559, TubularJointTestData.Check(r, "BraceB.ChordFace").Resistance);
        TubularJointTestData.Close(781753.859204345, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
        TubularJointTestData.Close(781753.859204345, TubularJointTestData.Check(r, "BraceB.PunchingShear").Resistance);
        Assert.AreEqual(1, TubularJointTestData.Trace(r, "Joint.Brace1IsA"));
    }

    [TestMethod]
    public void NGapJointTakesN2FromTheSineRatio()
    {
        // Brace A 88.9x5 at 90 deg (compression -150 kN), brace B 88.9x5 at 45 deg (tension 150/sin45 = 212.132 kN), g = 15 mm.
        // Chord: side A -300 kN, N_B = -300 - 212.132 cos45 = -450 kN. sigmap = 300000/4028.7784 = 74.4643, np = 0.209758,
        // kp = 1 - 0.3*0.209758*1.209758 = 0.923873. kg = 1.600388*(1 + 0.024*16.83405/(1 + exp(0.5*15/8 - 1.33))) = 1.987240.
        // N1,Rd (A, sin90) = kg kp fy0 t0^2 (1.8 + 10.2*88.9/168.3) = 299827.62 N; N2,Rd = N1,Rd sin90/sin45 = 424020.29 N.
        // Punching A: 355/sqrt3*8*pi*88.9*(1+1)/2 = 457940.81 N; B: 781753.86 N (as the K joint).
        // e = (15 + 44.45 + 62.861) sin90 sin45/sin135 - 84.15 = 38.162 mm, e/d0 = 0.2267 <= 0.25.
        var r = TubularJoint.Verify(TubularJointTestData.Two(TubularJointType.N, Chord, TubularJointTestData.Cut(-300000), TubularJointTestData.Cut(-450000),
            TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 90, -150000),
            TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 45, 150000 / TubularJointTestData.Sin45), 15));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(1.98724016761829, TubularJointTestData.Trace(r, "Joint.kg"));
        TubularJointTestData.Close(38.1617928474841, TubularJointTestData.Trace(r, "Joint.Eccentricity"));
        TubularJointTestData.Close(299827.624216897, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(424020.292541639, TubularJointTestData.Check(r, "BraceB.ChordFace").Resistance);
        TubularJointTestData.Close(457940.808284398, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
        TubularJointTestData.Close(781753.859204345, TubularJointTestData.Check(r, "BraceB.PunchingShear").Resistance);
    }

    [TestMethod]
    public void KOverlapJointUsesNegativeGapInKgAndNoPunching()
    {
        // Overlapping brace A 88.9x5 (compression -180 kN) on overlapped brace B 88.9x6.3 (tension +180 kN), 45/45.
        // lambda_ov = 50 %: p = 88.9/sin45 = 125.7236, q = 62.8618, g = -62.8618 (7.4.2(6)); hidden seam not welded:
        // limit 60 %; perpendicular components 127.28 kN on both braces (7.3.1(2) holds). 7.1.2(7): 5*355 <= 6.3*355.
        // Chord side A -350 kN, N_B = -350 - 360 cos45 = -604.558 kN; sigmap = 350000/4028.7784 = 86.8750, np = 0.244718,
        // kp = 1 - 0.3*0.244718*1.244718 = 0.908618. kg = 1.600388*(1 + 0.024*16.83405/(1 + exp(0.5*(-62.8618)/8 - 1.33))) = 2.244742.
        // N1,Rd = N2,Rd = kg kp fy0 t0^2/sin45 (1.8 + 10.2*0.528223) = 471055.43 N; Table7.2 gives no punching for overlaps.
        // e = (-62.8618 + 125.7236)*0.5 - 84.15 = -52.719 mm, e/d0 = -0.3132 >= -0.55.
        double g = -0.5 * 88.9 / TubularJointTestData.Sin45;
        var r = TubularJoint.Verify(TubularJointTestData.Two(TubularJointType.K, Chord, TubularJointTestData.Cut(-350000),
            TubularJointTestData.Cut(-350000 - 360000 * TubularJointTestData.Sin45), TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 45, -180000),
            TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 6.3), 45, 180000), g));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(50, TubularJointTestData.Trace(r, "Joint.OverlapRatio"));
        TubularJointTestData.Close(60, TubularJointTestData.Trace(r, "Joint.OverlapLimit"));
        TubularJointTestData.Close(2.24474184264144, TubularJointTestData.Trace(r, "Joint.kg"));
        TubularJointTestData.Close(471055.429208717, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(471055.429208717, TubularJointTestData.Check(r, "BraceB.ChordFace").Resistance);
        Assert.AreEqual(2, r.Checks.Count);
        TubularJointTestData.Close(-52.719103576258, TubularJointTestData.Trace(r, "Joint.Eccentricity"));
    }

    [TestMethod]
    public void NOverlapJointWithTheNarrowerVerticalBraceOverlapping()
    {
        // Brace A 76.1x4 at 90 deg overlapping (compression -100 kN), brace B 88.9x5 at 45 deg (tension 100/sin45 = 141.421 kN).
        // lambda_ov = 40 %: p = 76.1/sin90, g = -30.44 mm. 7.1.2(7) 4*355 <= 5*355, 7.1.2(8) 76.1 <= 88.9; components 100 and 100 kN.
        // Chord side A +150 kN, N_B = 150 - 141.421 cos45 = +50 kN: tension chord, sigmap = -150000/4028.7784 < 0 -> kp = 1.
        // kg = 1.600388*(1 + 0.024*16.83405/(1 + exp(0.5*(-30.44)/8 - 1.33))) = 2.223526.
        // N1,Rd (A, d1 = 76.1) = kg*355*64*(1.8 + 10.2*76.1/168.3) = 323930.84 N; N2,Rd = N1,Rd/sin45 = 458107.39 N.
        // e = (-30.44 + 38.05 + 62.861)*sin45/sin135 - 84.15 = -13.678 mm.
        var r = TubularJoint.Verify(TubularJointTestData.Two(TubularJointType.N, Chord, TubularJointTestData.Cut(150000), TubularJointTestData.Cut(50000),
            TubularJointTestData.Brace(TubularJointTestData.Chs(76.1, 4), 90, -100000),
            TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 45, 100000 / TubularJointTestData.Sin45), -30.44));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(40, TubularJointTestData.Trace(r, "Joint.OverlapRatio"));
        TubularJointTestData.Close(2.22352618273586, TubularJointTestData.Trace(r, "Joint.kg"));
        TubularJointTestData.Close(323930.840814065, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(458107.388350171, TubularJointTestData.Check(r, "BraceB.ChordFace").Resistance);
    }

    [TestMethod]
    public void GradesAbove355TakeThe09FactorOfClause7114()
    {
        // S420 chord and brace (fy 420): np = 139.3312/420 = 0.331741, kp = 1 - 0.3*0.331741*1.331741 = 0.867462.
        // Chord face = 0.9*1.600388*0.867462*420*64*9.34960 = 314127.97 N; punching = 0.9*420/sqrt3*8*pi*114.3 = 626927.42 N.
        var r = TubularJoint.Verify(TJoint(chord: TubularJointTestData.Chs(168.3, 8, 420, 420, 520), brace: TubularJointTestData.Chs(114.3, 6, 420, 420, 520)));
        TubularJointTestData.Complete(r);
        Assert.AreEqual(.9, TubularJointTestData.Trace(r, "Joint.HighGradeFactor"));
        TubularJointTestData.Close(314127.965095901, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(626927.416411881, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
        StringAssert.Contains(TubularJointTestData.Check(r, "BraceA.ChordFace").Reference, "x0.9 per 7.1.1(4)");
    }

    [TestMethod]
    public void GammaM5DividesTheResistancesAndEntersNpLiterally()
    {
        // gammaM5 = 1.1: np = (139.3312/355)/1.1 = 0.356802 (1.5(5)), kp = 1 - 0.3*0.356802*1.356802 = 0.854767;
        // chord face = 1.600388*0.854767*355*64*9.34960/1.1 = 264269.93 N; punching = 588781.04/1.1 = 535255.49 N.
        var r = TubularJoint.Verify(TJoint(code: TubularJointTestData.Ec(1.1)));
        TubularJointTestData.Complete(r);
        TubularJointTestData.Close(0.356802022614304, TubularJointTestData.Trace(r, "Chord.np"));
        TubularJointTestData.Close(264269.928581738, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        TubularJointTestData.Close(535255.490202544, TubularJointTestData.Check(r, "BraceA.PunchingShear").Resistance);
    }

    [TestMethod]
    public void NtcRouteAppliesUniEn199318ThroughChapter12WithGammaM5One()
    {
        // NTC2018 has no tubular joint rules; chapter12 + Italian NA 2.2(2) -> gammaM5 = 1.0: same joint values as the EC test.
        var r = TubularJoint.Verify(TJoint(code: TubularJointCode.Ntc2018()));
        TubularJointTestData.Complete(r);
        Assert.AreEqual(1.0, TubularJointCode.Ntc2018().GammaM5); StringAssert.Contains(TubularJointCode.Ntc2018().GammaM5Source, "DM 31/07/2012");
        TubularJointTestData.Close(284328.942942100, TubularJointTestData.Check(r, "BraceA.ChordFace").Resistance);
        StringAssert.Contains(TubularJointTestData.Check(r, "BraceA.ChordFace").Reference, "UNI EN1993-1-8:2005 Table7.2");
        StringAssert.Contains(TubularJointTestData.Check(r, "BraceA.ChordFace").Reference, "NTC2018 cap.12");
        Assert.IsTrue(r.Assumptions.Any(x => x.StartsWith("NTC route")));
        Assert.ThrowsException<ArgumentException>(() => TubularJointCode.Eurocode3(ConnectionDesignCode.Ntc2018(), 1, "x"));
        Assert.ThrowsException<ArgumentException>(() => TubularJointCode.Eurocode3(ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EN"), 1, " "));
    }

    [TestMethod]
    public void FilletWeldMustReachTheFullStrengthThroatBound()
    {
        // 7.3.1(4) with the simplified method 4.5.3.3: a >= sqrt3 betaw gammaM2 fy t/(fu gammaM0), S355 betaw = 0.9, t = 6:
        // EC (gammaM0 1.0): 1.7320508*0.9*1.25*355*6/510 = 8.1381 mm; NTC (gammaM0 1.05): 7.7506 mm.
        var fillet = TubularBraceWeld.Fillet(8, "Fillet all round, matching consumables, VT");
        var ec = TubularJoint.Verify(TJoint(weld: fillet));
        TubularJointTestData.Close(8.13809166203318, TubularJointTestData.Trace(ec, "BraceA.Weld.FullStrengthThroat"));
        Assert.AreEqual(ConnectionStatus.NotSupported, ec.Status); Assert.IsFalse(ec.IsComplete);
        Assert.IsTrue(ec.Diagnostics.Any(d => d.StartsWith("BraceA_FILLET_NOT_SHOWN_FULL_STRENGTH:7.3.1(4)")), string.Join(";", ec.Diagnostics));
        Assert.AreEqual(0, ec.Checks.Count, "Favourable partial checks are not retained.");
        var ntc = TubularJoint.Verify(TJoint(code: TubularJointCode.Ntc2018(), weld: fillet));
        TubularJointTestData.Close(7.75056348765065, TubularJointTestData.Trace(ntc, "BraceA.Weld.FullStrengthThroat"));
        TubularJointTestData.Complete(ntc);
        TubularJointTestData.Complete(TubularJoint.Verify(TJoint(weld: TubularBraceWeld.Fillet(8.2, "Fillet all round"))));
    }

    [TestMethod]
    public void OverloadIsAKnownFailureAndStaysVisible()
    {
        var r = TubularJoint.Verify(TJoint(-400000));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsTrue(r.IsComplete);
        Assert.IsFalse(TubularJointTestData.Check(r, "BraceA.ChordFace").Passed);
        TubularJointTestData.Close(400000 / 284328.942942100, r.Utilization!.Value);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, SteelAssessmentSummary.From(r).Outcome);
    }

    [TestMethod]
    public void MissingSourcesAreRequirementsWithoutHidingFailures()
    {
        var missing = TubularJoint.Verify(TubularJointTestData.Single(TubularJointType.T, Chord, TubularJointTestData.Cut(-300000),
            TubularJointTestData.Cut(-300000), TubularJointTestData.Brace(TubularJointTestData.Chs(114.3, 6), 90, -150000,
                weld: TubularBraceWeld.FullPenetration(" ")), actions: null, analysis: ""));
        Assert.AreEqual(ConnectionStatus.NotSupported, missing.Status);
        CollectionAssert.IsSubsetOf(new[] { "SAME_CASE_CHORD_AND_BRACE_FORCES_SOURCE_REQUIRED" }, missing.Diagnostics.ToArray());
        Assert.IsTrue(missing.Diagnostics.Any(d => d.StartsWith("LATTICE_GLOBAL_ANALYSIS_SOURCE_REQUIRED")));
        Assert.IsTrue(missing.Diagnostics.Any(d => d.StartsWith("BraceA_WELD_EXECUTION_SOURCE_REQUIRED")));
        var failing = TubularJoint.Verify(TubularJointTestData.Single(TubularJointType.T, Chord, TubularJointTestData.Cut(-300000),
            TubularJointTestData.Cut(-300000), TubularJointTestData.Brace(TubularJointTestData.Chs(114.3, 6), 90, -400000), analysis: null));
        Assert.AreEqual(ConnectionStatus.Failed, failing.Status); Assert.IsFalse(failing.IsComplete);
        Assert.IsTrue(failing.Checks.All(c => !c.Passed) && failing.Checks.Count == 1);
    }

    [TestMethod]
    public void ThickChordNeedsTheThroughThicknessSource()
    {
        // 7.1.1(6): chord 355.6x28 (d0/t0 = 12.7) S355 (fy 335 for 16 < t <= 40 is the caller's value).
        var chord = TubularJointTestData.Chs(355.6, 28, 355, 335);
        var brace = TubularJointTestData.Brace(TubularJointTestData.Chs(219.1, 10), 90, -500000);
        var without = TubularJoint.Verify(TubularJointTestData.Single(TubularJointType.T, chord, TubularJointTestData.Cut(-1e6), TubularJointTestData.Cut(-1e6), brace));
        CollectionAssert.Contains(without.Diagnostics.ToArray(), "CHORD_WALL_OVER_25MM_THROUGH_THICKNESS_SOURCE_REQUIRED:7.1.1(6)");
        TubularJointTestData.Complete(TubularJoint.Verify(TubularJointTestData.Single(TubularJointType.T, chord, TubularJointTestData.Cut(-1e6),
            TubularJointTestData.Cut(-1e6), brace, throughThickness: "Z25 quality to EN10164")));
    }

    [TestMethod]
    public void InvalidSectionsAndInputsThrow()
    {
        Assert.ThrowsException<ArgumentException>(() => TubularJointSection.Circular(100, 50, 355, 355, 510, TubularFabrication.HotFinished));
        Assert.ThrowsException<ArgumentException>(() => TubularJointSection.Circular(100, 5, 355, 400, 380, TubularFabrication.HotFinished));
        Assert.ThrowsException<ArgumentException>(() => TubularJointSection.Rectangular(200, 200, 10, 7700, 4e5, 4e5, 355, 355, 510, TubularFabrication.HotFinished));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 95, 0));
        Assert.ThrowsException<ArgumentException>(() => new TubularJointInput(TubularJointTestData.Ec(), TubularJointType.T, Chord,
            TubularJointTestData.Cut(0), TubularJointTestData.Cut(0), TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 90, 0), null, 10, "a", "b"));
        Assert.ThrowsException<ArgumentNullException>(() => new TubularJointInput(TubularJointTestData.Ec(), TubularJointType.K, Chord,
            TubularJointTestData.Cut(0), TubularJointTestData.Cut(0), TubularJointTestData.Brace(TubularJointTestData.Chs(88.9, 5), 45, 0), null, 10, "a", "b"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TubularBraceWeld.Fillet(0, "x"));
    }

    [TestMethod]
    public void CatalogPromotesTheTubularJointWorkflow()
    {
        var c = ConnectionCapabilityCatalog.Get("TubularJoints");
        Assert.AreEqual(ConnectionCapabilityLevel.StandardStaticVerification, c.Level);
        Assert.AreEqual("TubularJoint.Verify", c.RecommendedEntryPoint);
        Assert.AreEqual("TUBULAR_JOINTS_USAGE.txt", c.Documentation);
        foreach (string missing in new[] { "Multiplanar", "Reinforced", "I/H", "Fatigue", "EN1993-1-8:2024" })
            Assert.IsTrue(c.MissingMechanisms.Any(x => x.Contains(missing)), missing);
        StringAssert.Contains(c.Domain, "EN1993-1-8:2005");
    }
}
