using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.ReferenceMath;

namespace Steel.Members.Tests;

// SCI P363 (March2015), B-22/PDF88 and D-22/PDF328; EC3 Table5.2 p42.
// Source uses UK NA gammaM0=gammaM1=1. Explicit fy355MPa. Uniform axial
// compression, fork-ended member, free warping; no eccentricity or NM claim.
[TestClass]
public class RoundedRhsCompressionTests
{
    private static SteelSection SourceSection() => Steel(new SectionRHSRoundedCorners(50,30,4,6,4,"SCI50x30x4"),355);
    private static SteelSection Steel(Section shape,double fy) => new(shape,new SteelMaterial("explicit fy",210000,fy,550),
        Section.SectionTypes.Rolled,Section.FormedTypes.HotFinished);

    [TestMethod]
    public void SourceFlatWidthsAndUniformClassAreTraceable()
    {
        var s=SourceSection(); var g=ModelCompressionGeometry.Create(s);
        // SCI explicitly uses cw=h-3t and cf=b-3t. Its geometric radii are
        // ro=1.5t,ri=t, not concentric circles; do not silently replace either.
        Assert.AreEqual(9.5,g.Parts.Single(x=>x.Id=="LeftWall").WidthOrDiameter/4,1e-12);
        Assert.AreEqual(4.5,g.Parts.Single(x=>x.Id=="TopWall").WidthOrDiameter/4,1e-12);
        var r=Ec3UniformCompressionClassification.Evaluate(g,355);
        Assert.AreEqual(1,r.NominalClass); Assert.AreEqual(1,new ReferenceProbe(s).Class(-1000,0));
        Assert.IsTrue(r.Parts.All(x=>x.Classification.Reference.Contains("Table 5.2")));
    }

    public static IEnumerable<object[]> PublishedCases
    {
        get {
            double[] lengths={1000,1500,2000,2500,3000,3500,4000,5000,6000,7000,8000,9000,10000};
            double[] y={162,113,72.1,48.5,34.6,25.8,20,13,9.10,6.73,5.18,4.11,3.34};
            double[] z={111,57.1,33.6,22,15.5,11.5,8.84,5.71,3.99,2.94,2.26,1.79,1.45};
            for(int i=0;i<lengths.Length;i++) foreach(int axis in new[]{1,2}) {
                double v=axis==1?y[i]:z[i];
                yield return new object[]{lengths[i],axis,v*1000,v>=100?500d:v>=10?50d:5d};
            }
        }
    }

    [DataTestMethod,DynamicData(nameof(PublishedCases))]
    public void PublishedCompressionAtThirteenLengthsAndBothAxes(double length,int axis,double published,double digit)
    {
        var s=SourceSection(); var p=new ReferenceProbe(s,length,1);
        Assert.AreEqual("a",p.Curve(axis));
        // Independent quarter-circle subtraction for actual radii; no SUT input
        // is used to generate the expected elastic force or resistance.
        double area=RoundedArea(50,30,6)-RoundedArea(42,22,4);
        double inertia=axis==1?RoundedI(50,30,6)-RoundedI(42,22,4):RoundedI(30,50,6)-RoundedI(22,42,4);
        double ncr=Math.PI*Math.PI*210000*inertia/(length*length);
        double exact=area*355*Reduction(Math.Sqrt(area*355/ncr),.21);
        Close(area,s.Area); Close(inertia,axis==1?s.J11:s.J22);
        Close(ncr,p.Euler(length,axis==1?s.J11:s.J22));
        int cl=Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(s),355).NominalClass!.Value;
        Close(exact,p.Nb(axis,cl));
        // P363 B-22 rounds A=5.59cm2 and i=1.72/1.13cm; notes6.2 use
        // 93.9*epsilon. Propagate the independently identified rounding gap.
        double radius=axis==1?17.2:11.3;
        double table=559*355*Reduction(length/(93.9*Math.Sqrt(235d/355)*radius),.21);
        Assert.AreEqual(published,table,digit,"SCI rounded-input calculation");
        Assert.AreEqual(published,p.Nb(axis,cl),digit+Math.Abs(table-exact),"SCI member resistance");
    }

    [TestMethod]
    public void RoundedGeometrySupportsIndependentCompressionAndPublishedShear()
    {
        var p=new ReferenceProbe(SourceSection(),gamma:1);
        double area=RoundedArea(50,30,6)-RoundedArea(42,22,4);
        Close(area*355,p.Axial());
        Close(area*30/80*355/Math.Sqrt(3),p.Shear(1));
        Close(area*50/80*355/Math.Sqrt(3),p.Shear(2));
        Assert.AreEqual(71600d,p.Shear(2),50,"SCI D-112 Vc,Rd");
    }

    public static IEnumerable<object[]> ClassCases =>
        from fy in new[]{235d,275d,355d,420d,460d}
        from row in new[]{(R:32.999,C:1),(R:33.001,C:2),(R:37.999,C:2),(R:38.001,C:3),(R:41.999,C:3),(R:42.001,C:4)}
        select new object[]{fy,row.R,row.C};

    [DataTestMethod,DynamicData(nameof(ClassCases))]
    public void ConcentricRadiiRespectCompressionTableAcrossGrades(double fy,double ratio,int expected)
    {
        double t=10, h=ratio*Math.Sqrt(235/fy)*t+40;
        var s=Steel(new SectionRHSRoundedCorners(h,80,t,20,10,"boundary"),fy);
        var r=Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(s),fy);
        Assert.AreEqual(expected,r.NominalClass);
    }

    [DataTestMethod]
    [DataRow(33d,1)] [DataRow(38d,2)] [DataRow(42d,3)]
    public void ExactlyRepresentableGeometryRetainsInclusiveLimits(double ratio,int expected)
    {
        // Avoid manufacturing a point just above a limit through floating-point
        // h=c+2ro subtraction at non-unit epsilon; adjacent cases above use margins.
        var s=Steel(new SectionRHSRoundedCorners(ratio+4,10,1,2,1,"exact boundary"),235);
        Assert.AreEqual(expected,Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(s),235).NominalClass);
    }

    [TestMethod]
    public void MutatedWallThicknessOrMissingRadiiDoNotProduceAQualifiedMapping()
    {
        var shape=new SectionRHSRoundedCorners(100,80,5,10,5,"mutated");
        shape.ThicknessBottom=6;
        Assert.ThrowsException<NotSupportedException>(()=>ModelCompressionGeometry.Create(Steel(shape,235)));
        Assert.ThrowsException<ArgumentException>(()=>ModelCompressionGeometry.Create(
            Steel(new SectionRHSRoundedCorners(100,80,5,double.NaN,5,"invalid radius"),235)));
    }

    private static double RoundedArea(double h,double b,double r) => b*h-(4-Math.PI)*r*r;
    private static double RoundedI(double h,double b,double r)
    {
        // Four corner squares minus quarter-disks, translated to section centre.
        double a=(1-Math.PI/4)*r*r, q=r*r*r/6, i=(1d/3-Math.PI/16)*Math.Pow(r,4), y=h/2-r;
        return b*Math.Pow(h,3)/12-4*(i+2*y*q+y*y*a);
    }
}
