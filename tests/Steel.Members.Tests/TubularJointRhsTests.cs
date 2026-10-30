using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D = Steel.Members.Tests.TubularJointTestData;

namespace Steel.Members.Tests;

/// <summary>RHS or CHS braces on RHS chords: Table7.8 field, Tables 7.11 and 7.12, Table7.10 for
/// overlaps. Sharp-corner properties are used as product values; every expected value is a hand
/// calculation written in the comment.</summary>
[TestClass]
public class TubularJointRhsTests
{
    private static readonly TubularJointSection Chord200 = D.Rhs(200, 200, 10);

    [TestMethod]
    public void TJointChordFaceUsesKnFromTheMaximumCompressiveStress()
    {
        // Chord 200x200x10: A0 = 200^2 - 180^2 = 7600 mm2, Wel = (200^4 - 180^4)/(6*200) = 458533.33 mm3 (both axes).
        // Cut A: N = -1200 kN, Mip = 20 kNm, Mop = 5 kNm; cut B: N = -1200 kN (T joint).
        // sigma0 = 1200000/7600 + 20e6/458533.33 + 5e6/458533.33 = 157.895 + 43.618 + 10.904 = 212.416 MPa (corner),
        // n = 212.416/355 = 0.598356. Brace 120x120x6 at 90: beta = eta = 0.6, kn = 1.3 - 0.4*0.598356/0.6 = 0.901096.
        // beta <= 0.85: only chord face: kn fy0 t0^2/((1-beta) sin90) (2 eta/sin90 + 4 sqrt(1-beta))
        //   = 0.901096*355*100/0.4*(1.2 + 2.529822) = 298282.33 N.
        var r = TubularJoint.Verify(D.Single(TubularJointType.T, Chord200, D.Cut(-1200000, 0, 20e6, 5e6), D.Cut(-1200000),
            D.Brace(D.Rhs(120, 120, 6), 90, -250000)));
        D.Complete(r);
        D.Close(0.598356056665674, D.Trace(r, "Chord.n"));
        D.Close(0.901095962222887, D.Trace(r, "Joint.kn"));
        D.Close(298282.329778118, D.Check(r, "BraceA.ChordFace").Resistance);
        Assert.AreEqual(1, r.Checks.Count, "Table7.11: within beta <= 0.85 only chord face failure applies.");
        StringAssert.Contains(D.Check(r, "BraceA.ChordFace").Reference, "Table7.11");
    }

    [TestMethod]
    public void YJointBetweenBeta085And1InterpolatesFaceAndSideWall()
    {
        // Chord 160x160x8 (A0 = 4864, b0/t0 = 20, 1-1/gamma = 1-16/160 = 0.9), brace 140x140x6 at 60 deg, N1 = -250 kN.
        // Cut A -200 kN, cut B -200 - 250 cos60 = -325 kN: sigma0 = 325000/4864 = 66.817, n = 0.188218; kn = 1 (1.3-0.4n/beta > 1).
        // beta = eta = 140/160 = 0.875, sin60 = 0.866025.
        // Chord face at beta 0.85: 355*64/(0.15*0.866025)*(2*0.875/0.866025 + 4 sqrt0.15) = 624374.03 N.
        // Side wall (beta 1): lambda = 3.46 (160/8 - 2) sqrt(1/0.866025)/(pi sqrt(210000/355)) = 0.875867;
        //   curve a (hot finished S355): phi = 0.5 (1 + 0.21*0.675867 + 0.767142) = 0.954536, chi = 0.749620,
        //   fb = chi fy0 = 266.115 MPa; N = fb t0/sin (2 h1/sin + 10 t0) = 266.115*8/0.866025*(323.316 + 80) = 991458.62 N.
        // Interpolation: 624374.03 + (0.875-0.85)/0.15*(991458.62 - 624374.03) = 685554.80 N.
        // Brace failure (beta >= 0.85): beff = min(140, 10/20*355*8/(355*6)*140) = 93.333; 355*6*(280 - 24 + 186.667) = 942880.0 N.
        // Punching (0.85 <= 0.875 <= 0.9): bep = min(140, 10/20*140) = 70; 355*8/(sqrt3*0.866025)*(2*140/0.866025 + 140) = 877211.91 N.
        var r = TubularJoint.Verify(D.Single(TubularJointType.Y, D.Rhs(160, 160, 8), D.Cut(-200000), D.Cut(-325000),
            D.Brace(D.Rhs(140, 140, 6), 60, -250000)));
        D.Complete(r);
        D.Close(624374.032615797, D.Trace(r, "BraceA.ChordFaceAtBeta085"));
        D.Close(0.875866557839519, D.Trace(r, "BraceA.SideWall.Slenderness"));
        D.Close(0.749620181113176, D.Trace(r, "BraceA.SideWall.Chi"));
        D.Close(991458.617723745, D.Trace(r, "BraceA.SideWallAtBeta1"));
        D.Close(685554.796800455, D.Check(r, "BraceA.ChordFaceToSideWall").Resistance);
        D.Close(942880.0, D.Check(r, "BraceA.BraceFailure").Resistance);
        D.Close(877211.912079454, D.Check(r, "BraceA.PunchingShear").Resistance);
        Assert.AreEqual(3, r.Checks.Count);
    }

    [TestMethod]
    public void XJointWithBetaOneTakesTheSideWallShearOfNote1()
    {
        // Chord h0 = 200, b0 = 150, t0 = 8 (A0 = 30000 - 184*134 = 5344), brace h1 = 100, b1 = 150, t1 = 6 at 45 deg: beta = 1.
        // cos45 = 0.7071 > h1/h0 = 0.5: note 1 applies. Tension brace +200 kN, chord +100 kN both cuts: fb = fy0, kn = 1.
        // Side wall buckling: 355*8/0.707107*(2*100/0.707107 + 80) = 1457309.32 N;
        // side wall shear (Table7.12, Av = 2h0t0 = 3200): 355*3200/(sqrt3*0.707107) = 927540.12 N -> governs.
        // Brace failure: beff = min(150, 10/18.75*355*8/(355*6)*150) = 106.667; 355*6*(200 - 24 + 213.333) = 829280.0 N.
        // No punching: beta = 1 > 1-1/gamma = 1-16/150 = 0.8933.
        var chord = D.Rhs(200, 150, 8); var brace = D.Rhs(100, 150, 6);
        var r = TubularJoint.Verify(D.Single(TubularJointType.X, chord, D.Cut(100000), D.Cut(100000), D.Brace(brace, 45, 200000)));
        D.Complete(r);
        D.Close(1457309.32137117, D.Trace(r, "BraceA.SideWall.fb") * 8 / Math.Sin(Math.PI / 4) * (200 / Math.Sin(Math.PI / 4) + 80));
        D.Close(927540.115933897, D.Trace(r, "BraceA.SideWall.Shear"));
        D.Close(927540.115933897, D.Check(r, "BraceA.ChordSideWall").Resistance);
        StringAssert.Contains(D.Check(r, "BraceA.ChordSideWall").Reference, "note1");
        D.Close(829280.0, D.Check(r, "BraceA.BraceFailure").Resistance);
        Assert.AreEqual(2, r.Checks.Count);
        // Compression -200 kN, chord unloaded: lambda = 3.46*(25-2)*sqrt(1/0.707107)/(pi*24.3215) = 1.238557,
        // chi (curve a) = 0.506160, fb = 0.8*0.506160*355*0.707107 = 101.646 MPa (X joint),
        // N = 101.646*8/0.707107*(282.843 + 80) = 417267.63 N < 927540.12: buckling governs.
        var c = TubularJoint.Verify(D.Single(TubularJointType.X, chord, D.Cut(0), D.Cut(0), D.Brace(brace, 45, -200000)));
        D.Complete(c);
        D.Close(0.506160163579591, D.Trace(c, "BraceA.SideWall.Chi"));
        D.Close(101.646236665548, D.Trace(c, "BraceA.SideWall.fb"));
        D.Close(417267.628661981, D.Check(c, "BraceA.ChordSideWall").Resistance);
    }

    [TestMethod]
    public void KGapJointChecksFaceShearBracePunchingAndTheChordInTheGap()
    {
        // Chord 200x200x10, braces 120x120x6 at 45/45, g = 60 mm. A compression -250 kN, B tension +250 kN.
        // Cut A -300 kN, cut B -300 - 500 cos45 = -653.553 kN: sigma0 = 653553.4/7600 = 85.994, n = 0.242236,
        // beta = 480/800 = 0.6, kn = min(1, 1.3 - 0.4*0.242236/0.6) = 1, gamma = 10.
        // Chord face: 8.9*1*355*100*sqrt10/sin45*(480/800) = 847782.81 N.
        // alpha = 1/sqrt(1 + 4*3600/300) = 1/7 = 0.142857, Av = (400 + 0.142857*200)*10 = 4285.714 mm2.
        // Chord shear: 355*4285.714/(sqrt3*0.707107) = 1242241.23 N.
        // Brace failure: beff = min(120, 10/20*355*10/(355*6)*120) = 100; 355*6*(240 - 24 + 120 + 100) = 928680.0 N.
        // Punching (0.6 <= 0.9): bep = 60; 355*10/(sqrt3*0.707107)*(240/0.707107 + 120 + 60) = 1505546.17 N.
        // Gap section: Vpl = 355*4285.714/sqrt3 = 878397.20 N; VEd = min(0 + 250 sin45, 0 + 250 sin45) = 176776.70 N;
        // N0,gap = |-300 - 250 cos45| = |-653.553 + 250 cos45| = 476776.70 N;
        // N0,Rd = (7600 - 4285.714)*355 + 4285.714*355*sqrt(1 - (176776.70/878397.20)^2) = 2666871.70 N.
        // Validity: g/b0 = 0.3 in [0.2, 0.6]; e = (60 + 169.706)*0.5 - 100 = 14.853 mm, e/h0 = 0.0743.
        var r = TubularJoint.Verify(D.Two(TubularJointType.K, Chord200, D.Cut(-300000), D.Cut(-300000 - 500000 * D.Sin45),
            D.Brace(D.Rhs(120, 120, 6), 45, -250000), D.Brace(D.Rhs(120, 120, 6), 45, 250000), 60));
        D.Complete(r);
        D.Close(14.8528137423857, D.Trace(r, "Joint.Eccentricity"));
        D.Close(4285.71428571429, D.Trace(r, "Chord.ShearArea"));
        foreach (var brace in new[] { "BraceA", "BraceB" })
        {
            D.Close(847782.81298927, D.Check(r, brace + ".ChordFace").Resistance);
            D.Close(1242241.22669718, D.Check(r, brace + ".ChordShear").Resistance);
            D.Close(928680.0, D.Check(r, brace + ".BraceFailure").Resistance);
            D.Close(1505546.17391194, D.Check(r, brace + ".PunchingShear").Resistance);
        }
        D.Close(878397.195267074, D.Check(r, "ChordGap.Shear").Resistance); D.Close(176776.695296637, D.Check(r, "ChordGap.Shear").Demand);
        D.Close(2666871.69964407, D.Check(r, "ChordGap.Axial").Resistance); D.Close(476776.695296637, D.Check(r, "ChordGap.Axial").Demand);
        Assert.AreEqual(10, r.Checks.Count);
    }

    [TestMethod]
    public void NGapJointWithCircularBracesTakesPiOver4ExceptChordShear()
    {
        // Chord 300x200x10 (h0 in plane): A0 = 60000 - 280*180 = 9600, gamma = 10. CHS braces 88.9x5: A at 90 deg compression
        // -200 kN, B at 45 deg tension 200/sin45 = 282.843 kN; g = 70. Cut A -100 kN, V = 20 kN; cut B -100 - 282.843 cos45 = -300 kN, V = 20 kN.
        // sigma0 = 300000/9600 = 31.25, n = 0.088028; beta = 2*88.9/400 = 0.4445; kn = 1.
        // Chord face A: 8.9*355*100*sqrt10/1*(2*(88.9+88.9)/800)*pi/4 = 348802.84 N; B: /sin45 = 493281.70 N.
        // Chord shear (alpha = 0, Av = 2*300*10 = 6000, no pi/4): A 355*6000/sqrt3 = 1229756.07 N; B /sin45 = 1739137.72 N.
        // Brace failure: beff = min(88.9, 10/20*355*10/(355*5)*88.9) = 88.9; 355*5*(177.8 - 20 + 88.9 + 88.9)*pi/4 = 467853.83 N.
        // Punching: bep = 44.45; A: 355*10/sqrt3*(177.8 + 88.9 + 44.45)*pi/4 = 500872.76 N; B: 876000.75 N.
        // Gap: Vpl = 1229756.07; VEd = min(20 + 200*1, 20 + 282.843*sin45) = 220 kN; N0,gap = 100 kN;
        // N0,Rd = (9600-6000)*355 + 6000*355*sqrt(1 - (220000/1229756.07)^2) = 3373638.33 N.
        // e = (70 + 44.45 + 62.861)*sin45/sin135 - 150 = 27.312 mm (e/h0 = 0.091); g/b0 = 0.35 in [0.27775, 0.83325].
        var chord = D.Rhs(300, 200, 10);
        var r = TubularJoint.Verify(D.Two(TubularJointType.N, chord, D.Cut(-100000, 20000), D.Cut(-300000, 20000),
            D.Brace(D.Chs(88.9, 5), 90, -200000), D.Brace(D.Chs(88.9, 5), 45, 200000 / D.Sin45), 70));
        D.Complete(r);
        D.Close(348802.835191511, D.Check(r, "BraceA.ChordFace").Resistance);
        D.Close(493281.700122023, D.Check(r, "BraceB.ChordFace").Resistance);
        D.Close(1229756.0733739, D.Check(r, "BraceA.ChordShear").Resistance);
        D.Close(1739137.71737606, D.Check(r, "BraceB.ChordShear").Resistance);
        D.Close(467853.831954226, D.Check(r, "BraceA.BraceFailure").Resistance);
        D.Close(467853.831954226, D.Check(r, "BraceB.BraceFailure").Resistance);
        D.Close(500872.759061061, D.Check(r, "BraceA.PunchingShear").Resistance);
        D.Close(876000.745592948, D.Check(r, "BraceB.PunchingShear").Resistance);
        D.Close(220000, D.Check(r, "ChordGap.Shear").Demand); D.Close(100000, D.Check(r, "ChordGap.Axial").Demand);
        D.Close(3373638.32757468, D.Check(r, "ChordGap.Axial").Resistance);
        Assert.AreEqual(0, D.Trace(r, "Joint.Alpha"));
    }

    [TestMethod]
    public void KOverlapJointUsesTable710BraceFailureAndTheOverlappedEfficiency()
    {
        // Overlapping A 120x120x6 (Ai = 2736, compression -300 kN) on overlapped B 120x120x8 (Aj = 3584, tension +300 kN), 45/45.
        // lambda_ov = 60 %, hidden seam welded (limit 80 %): p = 120/sin45 = 169.706, g = -101.823. Cut A -200 kN, cut B -624.264 kN.
        // beff = min(120, 10/20*355*10/(355*6)*120) = 100; be,ov = min(120, 10/15*355*8/(355*6)*120) = 106.667.
        // 50 <= 60 < 80: Ni,Rd = 355*6*(100 + 106.667 + 240 - 24) = 900280.0 N.
        // Nj,Rd = Ni,Rd/(Ai fyi) * Aj fyj = 900280*3584/2736 = 1179314.15 N. e = (-101.823 + 169.706)*0.5 - 100 = -66.06 (e/h0 = -0.33).
        double g60 = -0.6 * 120 / D.Sin45;
        var r = TubularJoint.Verify(D.Two(TubularJointType.K, Chord200, D.Cut(-200000), D.Cut(-200000 - 600000 * D.Sin45),
            D.Brace(D.Rhs(120, 120, 6), 45, -300000), D.Brace(D.Rhs(120, 120, 8), 45, 300000), g60, hiddenSeamWelded: true));
        D.Complete(r);
        D.Close(60, D.Trace(r, "Joint.OverlapRatio")); D.Close(80, D.Trace(r, "Joint.OverlapLimit"));
        D.Close(900280.0, D.Check(r, "BraceA.BraceFailure").Resistance);
        D.Close(1179314.15204678, D.Check(r, "BraceB.BraceFailure").Resistance);
        Assert.AreEqual(2, r.Checks.Count);
        // lambda_ov = 40 % (25 <= 40 < 50): Ni,Rd = 355*6*(100 + 106.667 + 2*120*40/50 - 24) = 798040.0 N; Nj,Rd = 1045385.73 N.
        double g40 = -0.4 * 120 / D.Sin45;
        var r40 = TubularJoint.Verify(D.Two(TubularJointType.K, Chord200, D.Cut(-200000), D.Cut(-200000 - 600000 * D.Sin45),
            D.Brace(D.Rhs(120, 120, 6), 45, -300000), D.Brace(D.Rhs(120, 120, 8), 45, 300000), g40, hiddenSeamWelded: true));
        D.Complete(r40);
        D.Close(798040.0, D.Check(r40, "BraceA.BraceFailure").Resistance);
        D.Close(1045385.73099415, D.Check(r40, "BraceB.BraceFailure").Resistance);
    }

    [TestMethod]
    public void CircularBraceOnRhsChordMultipliesByPiOver4()
    {
        // CHS 114.3x6 at 90 deg on chord 200x200x10, tension +200 kN, chord unloaded: beta = eta = 0.5715, kn = 1.
        // 355*100/(1 - 0.5715)*(2*0.5715 + 4*sqrt(0.4285))*pi/4 = 244746.52 N.
        var r = TubularJoint.Verify(D.Single(TubularJointType.T, Chord200, D.Cut(0), D.Cut(0), D.Brace(D.Chs(114.3, 6), 90, 200000)));
        D.Complete(r);
        D.Close(244746.516504725, D.Check(r, "BraceA.ChordFace").Resistance);
        Assert.AreEqual(1, r.Checks.Count);
    }

    [TestMethod]
    public void ColdFormedChordUsesCurveCForTheSideWall()
    {
        // As the Y interpolation case with an EN10219 chord: curve c, alpha = 0.49, lambda = 0.875867:
        // phi = 0.5 (1 + 0.49*0.675867 + 0.767142) = 1.049158, chi = 1/(1.049158 + sqrt(1.100733 - 0.767142)) = 0.614729.
        var r = TubularJoint.Verify(D.Single(TubularJointType.Y, D.Rhs(160, 160, 8, fabrication: TubularFabrication.ColdFormed),
            D.Cut(-200000), D.Cut(-325000), D.Brace(D.Rhs(140, 140, 6), 60, -250000)));
        D.Complete(r);
        D.Close(0.614729433406598, D.Trace(r, "BraceA.SideWall.Chi"));
        Assert.AreEqual((int)GPC.Checkers.Steel.Inputs.SteelBucklingCurve.C, D.Trace(r, "BraceA.SideWall.Curve"));
    }
}
