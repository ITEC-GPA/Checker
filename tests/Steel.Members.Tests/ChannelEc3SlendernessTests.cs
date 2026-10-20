using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model.Elements;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt=GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;
[TestClass]
public class ChannelEc3SlendernessTests
{
    public static IEnumerable<object[]> Cases =>
        from mcr in new[] { 60e6, 150e6, 400e6, 1e9 }
        from fy in new[] { 265d, 355d }
        from cl in new[] { 1, 2, 3 }
        select new object[] { mcr, fy, cl };

    [DataTestMethod,DynamicData(nameof(Cases))]
    public void StandardSlendernessDoesNotIncludeTheResearchTorsionCorrection(double mcr,double fy,int cl)
    {
        // SCI P363, PFC300x100x46: Wpl,y641cm3,Wel,y549cm3 are explicit inputs.
        // EN1993-1-1:2005 6.3.2.1(3),6.3.2.2(1),eq6.56 defines lambdaLT.
        // Class is explicitly supplied here, not a claim that this product is class3.
        var p=new Probe(fy);
        double w=cl==3?549000:641000, expected=Math.Sqrt(w*fy/mcr);
        Assert.AreEqual(expected,p.Lambda(cl,mcr),1e-12);
        double alpha=.76, phi=(1+alpha*(expected-.2)+expected*expected)/2;
        double chi=2/(2*phi+Math.Sqrt(4*phi*phi-4*expected*expected));
        Assert.AreEqual(Math.Min(1,chi),p.Reduction(cl,mcr),1e-12);
    }

    [DataTestMethod]
    [DataRow(.5)] [DataRow(.75)] [DataRow(1.14)]
    public void NoJumpAtForeignMethodPiecewiseThresholds(double lambda)
    {
        var p=new Probe(265);
        double mcr=641000*265/(lambda*lambda);
        double before=p.Lambda(1,mcr*(1+1e-8)),after=p.Lambda(1,mcr*(1-1e-8));
        Assert.IsTrue(Math.Abs(after-before)<2e-8, $"{before} -> {after}");
    }

    private sealed class Probe:EN1993p11Checker
    {
        private ReferenceSection S=>(ReferenceSection)BeamElement.BeamProperty;
        public Probe(double fy):base(new BeamElement(Point3d.Origin,new Point3d(5000,0,0),
            new ReferenceSection(new SectionC(300,9,100,16.5,100,16.5,"PFC input",15),fy)
            {Wpl1=641000,Wel1=549000}),new Opt(Opt.LoadConditions.Constant,Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,1,1),new StandardEN1993p11()){}
        public double Lambda(int cl,double mcr)=>GetLambdaSignedLTB(S,(SectionClass)cl,mcr);
        public double Reduction(int cl,double mcr)
        {double l=Lambda(cl,mcr);return GetChiLTmod(GetPhiForBuckling(GetImperfectionFactorLT(S),l,1,.2),l,1);}
    }
}
