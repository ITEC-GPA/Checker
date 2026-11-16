using GPC.Checkers.Steel.Connections;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D = Steel.Members.Tests.TubularJointTestData;

namespace Steel.Members.Tests;

/// <summary>Out-of-plane brace moments on CHS K joints (15/11/2026). EN1993-1-8:2005 Table7.5 gives the chord face
/// resistance to Mop for K, N, T, X and Y joints and the punching shear for K and N gap joints; eq.(7.3) with Mip = 0.
/// In-plane moments on K/N joints stay refused (the in-plane chord face row covers T, X and Y joints only).
/// Expected values: ReferenceCalculations/tubular_k_out_of_plane_moments.py, independent of the library.</summary>
[TestClass]
public class TubularJointKMomentTests
{
    private static readonly TubularJointSection Chord = D.Chs(168.3, 8);

    private static TubularJointInput KGap(TubularJointSection a, TubularJointSection b, double mopA, double mopB, double mipA = 0)
        => D.Two(TubularJointType.K, Chord, D.Cut(-400000), D.Cut(-400000 - 400000 * D.Sin45),
            D.Brace(a, 45, -200000, mipA, mopA), D.Brace(b, 45, 200000, 0, mopB), 20);

    [TestMethod]
    public void KGapJointOutOfPlaneMomentsUseTable75AndEquation73()
    {
        // Geometry and axial resistances of KGapJointUsesKgOnTheCompressionBraceAndPunchingOnBoth: kp = 0.892631,
        // N1,Rd = N2,Rd = 399423.57 N, axial punching 781753.86 N. beta = 88.9/168.3 = 0.528223 (equal braces).
        // Mop face = fy0 t0^2 d1/sin45 * 2.7/(1 - 0.81 beta) * kp = 355*64*88.9/0.707107*2.7/0.572139*0.892631 = 12032588.88 Nmm.
        // Mop punching = fy0 t0 d1^2/sqrt3 (3 + sin45)/(4 sin45^2) = 24019631.19 Nmm (88.9 <= 168.3 - 16).
        // eq.(7.3): A 200000/399423.57 + 1.5e6/12032588.88 = 0.625383; B 200000/399423.57 + 1.0e6/12032588.88 = 0.583829.
        var r = TubularJoint.Verify(KGap(D.Chs(88.9, 5), D.Chs(88.9, 5), 1.5e6, 1.0e6));
        D.Complete(r);
        D.Close(0.528223410576352, D.Trace(r, "BraceA.MomentBeta"));
        D.Close(12032588.8837549, D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(24019631.1930799, D.Check(r, "BraceA.PunchingShear.OutOfPlaneMoment").Resistance);
        D.Close(12032588.8837549, D.Check(r, "BraceB.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(0.625383034052495, D.Check(r, "BraceA.Interaction").Demand);
        D.Close(0.583829216762598, D.Check(r, "BraceB.Interaction").Demand);
        D.Close(399423.566142559, D.Trace(r, "BraceA.NRdForInteraction"));
        D.Close(0.625383034052495, r.Utilization!.Value);
        StringAssert.Contains(D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Reference, "Table7.5 chord face failure, K, N, T, X and Y joints");
        StringAssert.Contains(D.Check(r, "BraceA.Interaction").Reference, "eq.(7.3)");
        // The axial checks of the joint are unchanged by the moments.
        D.Close(399423.566142559, D.Check(r, "BraceA.ChordFace").Resistance);
        D.Close(781753.859204345, D.Check(r, "BraceB.PunchingShear").Resistance);
    }

    [TestMethod]
    public void UnequalBracesTakeTheSmallerOfOwnAndMeanBeta()
    {
        // Brace A 88.9x5 compression, B 76.1x5 tension; 1.5(6) mean beta = (88.9+76.1)/(2*168.3) = 0.490196.
        // A: min(0.528223, 0.490196) = 0.490196 -> Mop face = 11417886.34 Nmm; B: min(0.452169, 0.490196) = 0.452169
        // -> Mop face = 355*64*76.1/0.707107*2.7/(1-0.81*0.452169)*0.892631 = 9298869.73 Nmm, punching 17600788.59 Nmm.
        // N2,Rd = N1,Rd (sin45/sin45) = 399423.57 N (Table7.2 uses d1 of the compression brace); axial punching B 669195.37 N.
        // eq.(7.3): A 0.605820, B 200000/399423.57 + 0.8e6/9298869.73 = 0.586754.
        var r = TubularJoint.Verify(KGap(D.Chs(88.9, 5), D.Chs(76.1, 5), 1.2e6, 0.8e6));
        D.Complete(r);
        D.Close(0.490196078431373, D.Trace(r, "BraceA.MomentBeta"));
        D.Close(0.452168746286393, D.Trace(r, "BraceB.MomentBeta"));
        D.Close(11417886.3385626, D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(9298869.73069063, D.Check(r, "BraceB.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(17600788.5861158, D.Check(r, "BraceB.PunchingShear.OutOfPlaneMoment").Resistance);
        D.Close(669195.373289659, D.Check(r, "BraceB.PunchingShear").Resistance);
        D.Close(0.605819843316052, D.Check(r, "BraceA.Interaction").Demand);
        D.Close(0.586753543396286, D.Check(r, "BraceB.Interaction").Demand);
    }

    [TestMethod]
    public void OverlapJointHasNoPunchingInTheMomentCheck()
    {
        // KOverlapJointUsesNegativeGapInKgAndNoPunching with Mop,A = 1 kNm: kp = 0.908618, N1,Rd = 471055.43 N.
        // Mop face = 355*64*88.9/0.707107*2.7/(1-0.81*0.528223)*0.908618 = 12248102.47 Nmm; no punching (Table7.5: gap joints).
        // eq.(7.3): 180000/471055.43 + 1e6/12248102.47 = 0.463766.
        double g = -0.5 * 88.9 / D.Sin45;
        var r = TubularJoint.Verify(D.Two(TubularJointType.K, Chord, D.Cut(-350000), D.Cut(-350000 - 360000 * D.Sin45),
            D.Brace(D.Chs(88.9, 5), 45, -180000, 0, 1e6), D.Brace(D.Chs(88.9, 6.3), 45, 180000), g));
        D.Complete(r);
        D.Close(12248102.4670315, D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
        Assert.IsFalse(r.Checks.Any(c => c.Id.StartsWith("BraceA.PunchingShear")));
        D.Close(0.463765935545685, D.Check(r, "BraceA.Interaction").Demand);
        Assert.IsFalse(r.Checks.Any(c => c.Id.StartsWith("BraceB.Interaction")));
    }

    [TestMethod]
    public void InPlaneMomentOnAKJointIsStillRefused()
    {
        var r = TubularJoint.Verify(KGap(D.Chs(88.9, 5), D.Chs(88.9, 5), 1e6, 0, mipA: 1e6));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.AreEqual(0, r.Checks.Count);
        Assert.IsTrue(r.Diagnostics.Any(d => d.StartsWith("BRACE_MOMENTS_NOT_COVERED_FOR_THIS_JOINT:BraceA") && d.Contains("in-plane moment on a K or N joint")),
            string.Join(";", r.Diagnostics));
    }

    [TestMethod]
    public void NJointUsesTheSineRatioInTheInteraction()
    {
        // Review B8 (16/11), values also in the review report: N gap joint, chord 168.3x8, braces 88.9x5, g = 15, e/d0 = 0.227;
        // A at 90 deg -150 kN, B at 45 deg +212.13 kN, cuts -400 / -550 kN, Mop = 1 kNm on both. kp = 0.892631, kg = 1.987240,
        // N1,Rd (A) = 289688.49 N, N2,Rd (B) = sin90/sin45 N1,Rd = 409681.39 N (punching 457940.81 / 781753.86 N larger).
        // Mop face A (sin90) = 8508325.19 Nmm, B (sin45) = 12032588.88 Nmm; eq.(7.3) A = 0.635330, B = 0.600905.
        var r = TubularJoint.Verify(D.Two(TubularJointType.N, Chord, D.Cut(-400000), D.Cut(-550000),
            D.Brace(D.Chs(88.9, 5), 90, -150000, 0, 1e6), D.Brace(D.Chs(88.9, 5), 45, 150000 / D.Sin45, 0, 1e6), 15));
        D.Complete(r);
        D.Close(289688.48877236, D.Trace(r, "BraceA.NRdForInteraction"));
        D.Close(409681.389685237, D.Trace(r, "BraceB.NRdForInteraction"));
        D.Close(8508325.19493298, D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(12032588.8837549, D.Check(r, "BraceB.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(12958693.9955325, D.Check(r, "BraceA.PunchingShear.OutOfPlaneMoment").Resistance);
        D.Close(0.635329529343948, D.Check(r, "BraceA.Interaction").Demand);
        D.Close(0.60090521996425, D.Check(r, "BraceB.Interaction").Demand);
    }

    [TestMethod]
    public void PunchingGovernsTheOutOfPlaneMomentOfSmallBracesOnAThickChord()
    {
        // Chord 168.3x16 (d0/t0 = 10.52), braces 42.4x4 at 45/45, A -50 kN with Mop = 0.5 kNm, B +50 kN, g = 20 (e/d0 = -0.262).
        // np = 100000/A0/355 = 0.036796, kp = 0.988555, beta = 42.4/168.3 = 0.251931.
        // Mop face = 355*256*42.4/0.707107*2.7/(1-0.81*0.251931)*0.988555 = 18274087.96 Nmm;
        // Mop punching = 355*16*42.4^2/sqrt3*(3+0.707107)/(4*0.5) = 10927593.26 Nmm governs.
        // Axial: N1,Rd face = 864899.86 N, punching 745699.97 N governs; eq.(7.3) = 50000/745699.97 + 0.5e6/10927593.26 = 0.112807.
        var r = TubularJoint.Verify(D.Two(TubularJointType.K, D.Chs(168.3, 16), D.Cut(-100000), D.Cut(-100000 - 100000 * D.Sin45),
            D.Brace(D.Chs(42.4, 4), 45, -50000, 0, 0.5e6), D.Brace(D.Chs(42.4, 4), 45, 50000), 20));
        D.Complete(r);
        D.Close(18274087.9617719, D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
        D.Close(10927593.2624013, D.Check(r, "BraceA.PunchingShear.OutOfPlaneMoment").Resistance);
        D.Close(10927593.2624013, D.Trace(r, "BraceA.MopRd"));
        D.Close(745699.969184797, D.Trace(r, "BraceA.NRdForInteraction"));
        D.Close(0.112806825075467, D.Check(r, "BraceA.Interaction").Demand);
    }

    [TestMethod]
    public void EquilibratedRouteGivesTheSameMomentChecks()
    {
        // VerifyEquilibrated on the equal-brace K gap joint: zero chord shears satisfy VB - VA + sum Ni sin(theta_i) = 0
        // (-200 sin45 + 200 sin45 = 0); with gammaM5 = 1 the n/np envelope equals the printed ratio, so the Mop checks are the
        // same as with Verify: interaction A = 0.625383, B = 0.583829.
        var input = KGap(D.Chs(88.9, 5), D.Chs(88.9, 5), 1.5e6, 1.0e6);
        var r = TubularJoint.VerifyEquilibrated(input);
        D.Complete(r);
        StringAssert.EndsWith(r.MethodId, ".EquilibratedConservative");
        D.Close(0.625383034052495, D.Check(r, "BraceA.Interaction").Demand);
        D.Close(0.583829216762598, D.Check(r, "BraceB.Interaction").Demand);
        D.Close(12032588.8837549, D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Resistance);
    }

    [TestMethod]
    public void OverloadedOutOfPlaneMomentFailsTheInteraction()
    {
        // Mop,A = 8 kNm: 200000/399423.57 + 8e6/12032588.88 = 0.500722 + 0.664861 = 1.165583 > 1.
        var r = TubularJoint.Verify(KGap(D.Chs(88.9, 5), D.Chs(88.9, 5), 8e6, 0));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        D.Close(200000 / 399423.566142559 + 8e6 / 12032588.8837549, D.Check(r, "BraceA.Interaction").Demand);
        Assert.IsTrue(D.Check(r, "BraceA.ChordFace.OutOfPlaneMoment").Passed);
    }
}
