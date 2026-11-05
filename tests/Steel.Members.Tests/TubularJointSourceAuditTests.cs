using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D = Steel.Members.Tests.TubularJointTestData;

namespace Steel.Members.Tests;

/// <summary>
/// Independent hand calculations from Tata Steel, Design of welded joints (2013),
/// 5.3.2.3-5.3.3, pp41-43; formulae cross-checked on EN1993-1-8:2005/AC2009 Table7.11.
/// These are new component benchmarks, not published whole-joint numerical examples.
/// See src/Steel/Connections/TUBULAR_SOURCE_AUDIT_2026_11_05.txt.
/// </summary>
[TestClass]
public class TubularJointSourceAuditTests
{
    private static TubularJointCode Code(bool ntc) => ntc ? TubularJointCode.Ntc2018() : D.Ec();

    [DataTestMethod]
    [DataRow(false, false, 1396869.6471610356)]
    [DataRow(true, false, 1396869.6471610356)]
    [DataRow(false, true, 1158512.5641435268)]
    [DataRow(true, true, 1158512.5641435268)]
    public void SideWallUsesThePublishedHotOrColdCurve(bool ntc, bool cold, double expected)
    {
        // h0=b0=200,t0=10, h1=b1=200, theta90, n=0:
        // lambda=3.46*18/(pi*sqrt(210000/355))=.8150856746504865.
        // chi=.7869688153019918 (a) or .6526831347287475 (c).
        // Nrd=chi*355*10*(400+100), gammaM5=1 for both codes.
        var chord = D.Rhs(200, 200, 10, fabrication: cold ? TubularFabrication.ColdFormed : TubularFabrication.HotFinished);
        var input = D.Single(TubularJointType.T, chord, D.Cut(0), D.Cut(0, 100000),
            D.Brace(D.Rhs(200, 200, 10), 90, -100000), Code(ntc));
        var result = TubularJoint.VerifyEquilibrated(input);
        D.Complete(result);
        D.Close(expected, D.Check(result, "BraceA.ChordSideWall").Resistance);
    }

    [DataTestMethod]
    [DataRow(false, false, 971629.4453692703, 642461.393447232)]
    [DataRow(true, false, 971629.4453692703, 642461.393447232)]
    [DataRow(false, true, 796789.1386622656, 613321.3423293978)]
    [DataRow(true, true, 796789.1386622656, 613321.3423293978)]
    public void InterpolationEvaluatesStressFactorsAtTheTwoAnchors(bool ntc, bool cold, double side, double expected)
    {
        // 160x160x8 chord A=4864; 140x140x8 brace, theta60, beta=.875, n=.8.
        // Evaluate the complete face expression at beta=.85 (kn=.9235294117647)
        // and the complete side-wall expression at beta=1 (kn=.98), keeping eta=.875.
        // Face085=576627.7830628242 N; interpolation fraction=1/6.
        var chord = D.Rhs(160, 160, 8, fabrication: cold ? TubularFabrication.ColdFormed : TubularFabrication.HotFinished);
        double nb = -.8 * 4864 * 355;
        var input = D.Single(TubularJointType.Y, chord, D.Cut(nb + 25000), D.Cut(nb, 25000 * Math.Sqrt(3)),
            D.Brace(D.Rhs(140, 140, 8), 60, -50000), Code(ntc));
        var result = TubularJoint.VerifyEquilibrated(input);
        D.Complete(result);
        D.Close(.8, D.Trace(result, "Chord.n"));
        D.Close(576627.7830628242, D.Trace(result, "BraceA.ChordFaceAtBeta085"));
        D.Close(side, D.Trace(result, "BraceA.SideWallAtBeta1"));
        D.Close(expected, D.Check(result, "BraceA.ChordFaceToSideWall").Resistance);
    }

    [DataTestMethod]
    [DataRow(false, false)] [DataRow(true, false)]
    [DataRow(false, true)] [DataRow(true, true)]
    public void XSideWallShearIsCheckedOnlyInsideThePublishedValidityRange(bool ntc, bool circular)
    {
        // Tata 5.3.2.5: alpha=0 for X => Av=2*200*8=3200 mm2.
        // theta30, b1=150=b0, cos30 > h1/h0 (100/200 or 150/200).
        // Chord shear 355*3200/(sqrt3*.5)=1311739.81159883 N.
        // A circular brace at beta=1 is outside Table7.8 (di/b0<=.8):
        // do not use the side-wall formula outside that range of validity.
        var brace = circular ? D.Chs(150, 6) : D.Rhs(100, 150, 6);
        var input = D.Single(TubularJointType.X, D.Rhs(200, 150, 8), D.Cut(100000), D.Cut(100000),
            D.Brace(brace, 30, 100000), Code(ntc));
        var result = TubularJoint.VerifyEquilibrated(input);
        if (circular)
        {
            Assert.IsFalse(result.IsComplete);
            Assert.IsTrue(result.Diagnostics.Any(x => x.Contains("BraceA.di/b0=1>0.8")));
            Assert.AreEqual(0, result.Checks.Count);
            return;
        }
        D.Complete(result);
        D.Close(1311739.81159883, D.Trace(result, "BraceA.SideWall.Shear"));
        D.Close(1311739.81159883, D.Check(result, "BraceA.ChordSideWall").Resistance);
    }
}
