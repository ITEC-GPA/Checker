using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D=Steel.Members.Tests.TubularJointTestData;

namespace Steel.Members.Tests;

[TestClass]
public class TubularJointEquilibriumTests
{
    private static TubularJointInput Gap(double va,double vb,double nb=250000,bool ntc=false)
        =>D.Two(TubularJointType.K,D.Rhs(200,200,10),D.Cut(-300000,va),D.Cut(-300000+(-250000-nb)*D.Sin45,vb),
            D.Brace(D.Rhs(120,120,6),45,-250000),D.Brace(D.Rhs(120,120,6),45,nb),60,
            code:ntc?TubularJointCode.Ntc2018():D.Ec());

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void GapShearComesFromBothFreeBodiesWithTheSameFaceConvention(bool ntc)
    {
        // Equal/opposite brace components, VA=VB=-100kN.
        // Vgap=-100+250sin45 = 76.776695kN; magnitudes alone give 276.776695.
        var p=Gap(-100000,-100000,ntc:ntc);var r=TubularJoint.VerifyEquilibrated(p);D.Complete(r);
        double expected=-100000+250000/Math.Sqrt(2);
        D.Close(expected,D.Trace(r,"ChordGap.SignedShearFromA"));D.Close(expected,D.Trace(r,"ChordGap.SignedShearFromB"));
        D.Close(expected,D.Check(r,"ChordGap.Shear").Demand);
        D.Close(0,D.Trace(r,"Chord.TransverseEquilibriumResidual"));
        D.Close(100000+250000/Math.Sqrt(2),D.Check(TubularJoint.Verify(p),"ChordGap.Shear").Demand);
        // Table7.12 with Av=30000/7, A0=7600, fy355, gammaM5=1.
        double av=30000d/7,vpl=av*355/Math.Sqrt(3);
        double nr=((7600-av)*355+av*355*Math.Sqrt(1-expected*expected/(vpl*vpl)));
        D.Close(nr,D.Check(r,"ChordGap.Axial").Resistance);
    }

    [TestMethod]
    public void UnbalancedBraceComponentsRequireTheMatchingChordShearJump()
    {
        // -250sin45+200sin45=-35.355339kN: VB-VA=+35.355339kN.
        var good=TubularJoint.VerifyEquilibrated(Gap(20000,20000+50000*D.Sin45,200000));D.Complete(good);
        D.Close(20000+250000*D.Sin45,D.Check(good,"ChordGap.Shear").Demand);
        var bad=TubularJoint.VerifyEquilibrated(Gap(20000,20000,200000));
        Assert.IsFalse(bad.IsComplete);Assert.IsNull(bad.Utilization);
        Assert.IsTrue(bad.Diagnostics.Any(d=>d.StartsWith("CHORD_AND_BRACE_TRANSVERSE_FORCES_NOT_IN_EQUILIBRIUM")));
    }

    [DataTestMethod][DataRow(90d)][DataRow(60d)]
    public void TAndYBracesTransferTheirNormalComponentIntoTheChord(double angle)
    {
        double cos=angle==90?0:Math.Cos(angle*Math.PI/180),sin=Math.Sin(angle*Math.PI/180);
        var p=D.Single(angle==90?TubularJointType.T:TubularJointType.Y,D.Chs(168.3,8),
            D.Cut(-300000,20000),D.Cut(-300000-100000*cos,20000+100000*sin),D.Brace(D.Chs(114.3,6),angle,-100000));
        D.Complete(TubularJoint.VerifyEquilibrated(p));
    }

    [TestMethod]
    public void CoaxialXBracesCancelTransverseForcesButCannotHideAShearJump()
    {
        var chord=D.Chs(168.3,8);var brace=D.Brace(D.Chs(114.3,6),90,-50000);
        D.Complete(TubularJoint.VerifyEquilibrated(D.Single(TubularJointType.X,chord,D.Cut(-100000,20000),D.Cut(-100000,20000),brace)));
        var r=TubularJoint.VerifyEquilibrated(D.Single(TubularJointType.X,chord,D.Cut(-100000,20000),D.Cut(-100000,-20000),brace));
        Assert.IsFalse(r.IsComplete);
    }

    [DataTestMethod][DataRow(.8)][DataRow(1d)][DataRow(1.25)]
    public void ConservativeStressRatioBoundsBothPrintedAndProposedDefinitions(double gamma)
    {
        // CEN collated comments 22/10/2014 GB2/DE36: remove division by gammaM5.
        // This is an explicit conservative envelope, not a silent normative correction.
        // RHS 200x200x10: A7600; sigma/fy=.8; beta=.6, kn=1.3-.4*n/.6.
        var p=D.Single(TubularJointType.T,D.Rhs(200,200,10),D.Cut(-.8*7600*355,0),D.Cut(-.8*7600*355,50000),
            D.Brace(D.Rhs(120,120,6),90,-50000),D.Ec(gamma));
        var r=TubularJoint.VerifyEquilibrated(p);D.Complete(r);
        double n=Math.Max(.8,.8/gamma),kn=1.3-.4*n/.6;
        D.Close(n,D.Trace(r,"Chord.n"));D.Close(kn,D.Trace(r,"Joint.kn"));
        D.Close(kn*355*100/.4*(1.2+4*Math.Sqrt(.4))/gamma,D.Check(r,"BraceA.ChordFace").Resistance);
        Assert.IsTrue(D.Check(r,"BraceA.ChordFace").Resistance<=D.Check(TubularJoint.Verify(p),"BraceA.ChordFace").Resistance+1e-8);
    }

    [TestMethod]
    public void CircularChordUsesTheSameConservativePolicyForNp()
    {
        var c=D.Chs(168.3,8);var p=D.Single(TubularJointType.T,c,D.Cut(-.625*c.Area*c.Fy),D.Cut(-.625*c.Area*c.Fy,20000),
            D.Brace(D.Chs(114.3,6),90,-20000),D.Ec(1.25));
        var r=TubularJoint.VerifyEquilibrated(p);D.Complete(r);
        D.Close(.625,D.Trace(r,"Chord.np"));D.Close(.5,D.Trace(r,"Chord.PrintedStressRatio"));
        D.Close(1-.3*.625*1.625,D.Trace(r,"Joint.kp"));
    }

    [TestMethod]
    public void WithinRoundingToleranceTheLargerGapDemandIsRetained()
    {
        var r=TubularJoint.VerifyEquilibrated(Gap(0,1));D.Complete(r);
        D.Close(1+250000*D.Sin45,D.Check(r,"ChordGap.Shear").Demand);
    }
}
