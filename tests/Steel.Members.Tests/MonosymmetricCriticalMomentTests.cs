using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;
[TestClass]
public class MonosymmetricCriticalMomentTests
{
    public static IEnumerable<object[]> Cases =>
        from scale in new[] { .1, 1d, 10d }
        from lengthRatio in new[] { 3d, 10d, 30d }
        from mirror in new[] { false, true }
        from sign in new[] { -1d, 1d }
        select new object[] { scale, lengthRatio, mirror, sign };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void AsymmetricHMatchesIntegratedSineMode(double scale, double lengthRatio, bool mirror, double sign)
    {
        // CISC(2002),p2,eq3: Wagner geometric coefficient. Elastic geometric
        // stiffness is integrated independently with 3x3 Gauss over 3 rectangles.
        var h = new SectionH(400*scale, 12*scale, (mirror?180:300)*scale, (mirror?16:20)*scale,
            (mirror?300:180)*scale, (mirror?20:16)*scale, "");
        double length = h.Height * lengthRatio;
        var p = new Probe(h, length);
        var rects = new[] { (h.LenghtBottomFlange, 0d, h.ThicknessBottomFlange),
            (h.ThicknessWeb, h.ThicknessBottomFlange, h.Height-h.ThicknessTopFlange),
            (h.LenghtTopFlange, h.Height-h.ThicknessTopFlange, h.Height) };
        double area = Integrate(rects, (x,y)=>1), yc = Integrate(rects, (x,y)=>y)/area;
        double ix = Integrate(rects, (x,y)=>(y-yc)*(y-yc));
        double iz = Integrate(rects, (x,y)=>x*x);
        double geo = Integrate(rects, (x,y)=>(y-yc)/ix*(x*x+Math.Pow(y-h.ShearCenter.Y,2)));
        double ncr = Math.PI*Math.PI*210000*iz/(length*length);
        double kt = 210000/2.6*h.Jt+Math.PI*Math.PI*210000*h.Jw/(length*length);
        double lo=0, hi=Math.Sqrt(ncr*kt)+Math.Abs(ncr*geo);
        for(int i=0;i<100;i++) { double m=(lo+hi)/2; if(ncr*(kt+sign*m*geo)-m*m>0)lo=m;else hi=m; }
        double expected=(lo+hi)/2;
        Assert.AreEqual(expected,p.Mcr(sign),expected*1e-10);
        Assert.AreEqual(expected,p.ReferenceMcr(sign),expected*1e-10);
    }

    [DataTestMethod]
    [DataRow(-1d)] [DataRow(1d)]
    public void TeeReferenceMomentRetainsItsWagnerTerm(double sign)
    {
        var p = new Probe(new SectionT(400,200,40,50,""),4000);
        Assert.AreEqual(p.Mcr(sign),p.ReferenceMcr(sign),1e-6);
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void ManualC1C2CannotSilentlyDiscardMonosymmetry(bool tee)
    {
        Section s = tee ? new SectionT(400,200,40,50,"") : new SectionH(400,12,300,20,180,16,"");
        Assert.ThrowsException<NotSupportedException>(()=>new Probe(s,4000).ManualMcr());
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void UnqualifiedHGeometryCannotUseSharpStrongAxisFormula(bool rounded)
    {
        var h=rounded?new SectionH(400,12,300,20,180,16,"",10):new SectionH(100,12,600,20,400,16,"");
        Assert.ThrowsException<NotSupportedException>(()=>new Probe(h,4000).Mcr(1));
    }

    private static double Integrate((double b,double y0,double y1)[] rects,Func<double,double,double> f)
    {
        double a=Math.Sqrt(3d/5),total=0;double[] x={-a,0,a},w={5d/9,8d/9,5d/9};
        foreach(var r in rects)for(int i=0;i<3;i++)for(int j=0;j<3;j++)
            total+=w[i]*w[j]*r.b*(r.y1-r.y0)/4*f(x[i]*r.b/2,(r.y0+r.y1)/2+x[j]*(r.y1-r.y0)/2);
        return total;
    }
    private sealed class Probe:EN1993p11Checker
    {
        private SteelSection S=>(SteelSection)BeamElement.BeamProperty;
        private readonly double length;
        public Probe(Section s,double length):base(new BeamElement(Point3d.Origin,new Point3d(length,0,0),
            new SteelSection(s,new SteelMaterial("S355",210000,355,510))),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded,Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,1,1),new StandardEN1993p11()){this.length=length;}
        private static ResultBeamForces Forces(double sign)=>new(0,0,0,0,sign,0,CoordinateSystem.Global);
        public double Mcr(double sign)=>CalculateMcr(S,Forces(sign),length,Opt.SupportConditions.HingesAtEnds,
            Opt.LoadConditions.NotDirectlyLoaded,Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,1,Opt.LoadApplicationPoints.ShearCenter);
        public double ReferenceMcr(double sign)=>CalculateMcr0ForInteraction(S,Forces(sign),Opt.SupportConditions.HingesAtEnds,
            Opt.LoadConditions.Constant,Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,Opt.LoadApplicationPoints.TopSection);
        public double ManualMcr()=>CalculateMcr(S,Forces(1),length,Opt.SupportConditions.HingesAtEnds,
            Opt.LoadConditions.NotDirectlyLoaded,Opt.LateralSupportConditions.HingesAtEnds,Opt.LateralWarpingConditions.HingesAtEnds,1,Opt.LoadApplicationPoints.ShearCenter,1,0);
    }
}
