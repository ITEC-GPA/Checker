using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.ReferenceMath;

namespace Steel.Members.Tests;

[TestClass]
public class WebShearBucklingTests
{
    // SCI AD368, NSC June2012 p28, UK NA eta=gammaM1=1.
    // https://www.steelconstruction.info/images/6/6f/AD-368.pdf
    [TestMethod]
    public void SciAd368PublishedNonRigidEndPostExample()
    {
        double h=380.8,t=6.4,fy=355;
        Assert.IsTrue(Ec3WebShearBuckling.RequiresCheck(h,t,fy,1));
        double actual=Ec3WebShearBuckling.NonRigidEndPostResistance(h,t,fy,1,1);
        // In the slender branch substitute eq5.5 into5.2: h cancels exactly.
        double exact=.83*86.4*t*t*Math.Sqrt(235/fy)*fy/Math.Sqrt(3);
        Close(exact,actual);
        double printed=.976*fy*h*t/Math.Sqrt(3); // published rounded chi, not SUT
        Assert.AreEqual(488000d,printed,500);
        Assert.AreEqual(488000d,actual,500+Math.Abs(exact-printed));
        var shape=new SectionH(398,6.4,141.8,8.6,141.8,8.6,"406x140x39",10.2);
        Close(actual,new Probe(shape,fy,1).Web());
        Close(actual,new Probe(shape,fy,1).Full(2));
    }

    public static IEnumerable<object[]> ReductionCases =>
        from eta in new[]{1d,1.2}
        from l in new[]{.4,.83/1.2,.83,1.08,2,5}
        select new object[]{eta,l};
    [DataTestMethod,DynamicData(nameof(ReductionCases))]
    public void NonRigidTableBranchesAndSlenderWebLimit(double eta,double lambda)
    {
        double h=lambda*86.4*10, fy=235;
        double expected=Math.Min(eta*fy*h*10/Math.Sqrt(3),.83*86.4*100*fy/Math.Sqrt(3));
        Close(expected,Ec3WebShearBuckling.NonRigidEndPostResistance(h,10,fy,1,eta));
    }

    [DataTestMethod]
    [DataRow(71.999,1d,false)] [DataRow(72d,1d,false)] [DataRow(72.001,1d,true)]
    [DataRow(59.999,1.2,false)] [DataRow(60d,1.2,false)] [DataRow(60.001,1.2,true)]
    public void UnstiffenedLimitUsesHeightOverThickness(double ratio,double eta,bool required)
        => Assert.AreEqual(required,Ec3WebShearBuckling.RequiresCheck(ratio,1,235,eta));

    [DataTestMethod]
    [DataRow(1,1d,1d)] [DataRow(1,1d,2d)] [DataRow(2,1d,1d)] [DataRow(2,1d,2d)]
    public void RhsPanelsAreCheckedIndividuallyOnBothShearAxes(int axis,double first,double second)
    {
        var shape=axis==2 ? new SectionRHS(300,200,10,10,first,second,"thin webs")
            : new SectionRHS(300,200,first,second,10,10,"thin flanges");
        var p=new Probe(shape,235,1);
        double width=axis==2?280:180;
        // Separate plate contributions: in the slender range sum(t_i^2), not (sum t_i)^2.
        double expected=.83*86.4*(first*first+second*second)*235/Math.Sqrt(3);
        Assert.IsTrue(width/second>72);
        Close(expected,p.Full(axis));
        Assert.IsTrue(p.Full(axis)<p.Plastic(axis));
    }

    [DataTestMethod]
    [DataRow("H")] [DataRow("C")] [DataRow("RHS")]
    public void StockyWebStillUsesPlasticSectionResistance(string family)
    {
        Section s=family=="H"?new SectionH(300,10,200,20,200,20,"H"):
            family=="C"?new SectionC(300,10,100,20,100,20,"C"):
            new SectionRHS(300,200,10,10,10,10,"RHS");
        var p=new Probe(s,235,1.2);
        Close(p.Plastic(1),p.Full(1)); Close(p.Plastic(2),p.Full(2));
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidEtaNeverDisablesTheApplicabilityCheck(double eta)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Ec3WebShearBuckling.RequiresCheck(100,1,235,eta));

    private sealed class Probe:EN1993p11Checker
    {
        private readonly ISteelSection section;
        public Probe(Section shape,double fy,double eta):this(new SteelSection(shape,new SteelMaterial("explicit",210000,fy,550)),eta){}
        private Probe(SteelSection s,double eta):base(new BeamElement(Point3d.Origin,new Point3d(3000,0,0),s),
            new EN1993p11Options(EN1993p11Options.LoadConditions.Constant,EN1993p11Options.SupportConditions.HingesAtEnds,
                EN1993p11Options.LateralSupportConditions.HingesAtEnds,EN1993p11Options.LateralWarpingConditions.HingesAtEnds,1,1),
            new StandardEN1993p11{GammaM0=1,GammaM1=1,NShearBucklingLowGradeOfSteel=eta}) {section=s;}
        public double Web()=>CalculateVbRd1(section);
        public double Plastic(int axis)=>axis==1?CalculateVcRd1(section):CalculateVcRd2(section);
        public double Full(int axis)=>axis==1?CalculateShear1Capacity(ReferenceProbe.Forces(),section):CalculateShear2Capacity(ReferenceProbe.Forces(),section);
    }
}
