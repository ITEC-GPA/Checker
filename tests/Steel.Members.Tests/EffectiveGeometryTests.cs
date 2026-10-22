using System.Text.Json;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

[TestClass]
public class EffectiveGeometryTests
{
    [DataTestMethod] [DataRow("H")] [DataRow("C")] [DataRow("T")] [DataRow("L")] [DataRow("RHS")]
    public void FullWidthsRecoverIndependentGrossSectionIntegrals(string name)
    {
        var oracle=JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!.Single(o=>o.Name==name);
        var input=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(name),false);
        var geometry=SteelEffectiveGeometry.Create(input.Section);var gross=geometry.Gross();
        Assert.AreEqual(oracle.Area,gross.Area,oracle.Area*1e-11);
        Assert.AreEqual(oracle.I1,gross.PrincipalI1,oracle.I1*1e-11);Assert.AreEqual(oracle.I2,gross.PrincipalI2,oracle.I2*1e-11);
        Assert.AreEqual(0,gross.CentroidShiftX);Assert.AreEqual(0,gross.CentroidShiftY);
        // Bounding-rectangle stress can be conservative for L, but cannot exceed
        // the true elastic resistance represented by the independent polygon oracle.
        Assert.IsTrue(gross.Modulus1<=oracle.Wel1*(1+1e-10));Assert.IsTrue(gross.Modulus2<=oracle.Wel2*(1+1e-10));
    }
    [TestMethod]
    public void AsymmetricRemovalPreservesFullCoupledTensorAndGrossCentroidEquilibrium()
    {
        var g=SteelEffectiveGeometry.Create(CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape("H"),false).Section);
        var widths=g.Gross().Widths.ToArray();int i=g.Plates.Select(p=>p.Id).ToList().IndexOf("TopRight");
        widths[2*i]=widths[2*i+1]=0;var state=g.WithWidths(widths);
        // Independent remaining geometry: bottom flange200x20, web10x260,
        // top left+web footprint105x20. Integrate with exact 2x2 Gauss rectangles,
        // as opposed to production's subtraction/parallel-axis calculation.
        var rectangles=new[]{(0d,0d,200d,20d),(95d,20d,10d,260d),(0d,280d,105d,20d)};
        var points=rectangles.SelectMany(r=>from a in new[]{-1d/Math.Sqrt(3),1d/Math.Sqrt(3)} from b in new[]{-1d/Math.Sqrt(3),1d/Math.Sqrt(3)}
            select (X:r.Item1+r.Item3*(1+a)/2,Y:r.Item2+r.Item4*(1+b)/2,A:r.Item3*r.Item4/4)).ToArray();
        double area=points.Sum(p=>p.A),x=points.Sum(p=>p.A*p.X)/area,y=points.Sum(p=>p.A*p.Y)/area;
        Assert.AreEqual(area,state.Area,1e-8);Assert.AreEqual(x,state.X,1e-10);Assert.AreEqual(y,state.Y,1e-10);
        Assert.AreEqual(points.Sum(p=>p.A*(p.Y-y)*(p.Y-y)),state.Ixx,state.Ixx*1e-12);
        Assert.AreEqual(points.Sum(p=>p.A*(p.X-x)*(p.X-x)),state.Iyy,state.Iyy*1e-12);
        Assert.AreEqual(points.Sum(p=>p.A*(p.X-x)*(p.Y-y)),state.Ixy,Math.Abs(state.Ixy)*1e-12);
        Assert.IsTrue(state.Ixy!=0);Assert.IsTrue(state.CentroidShiftX<0&&state.CentroidShiftY<0);
        foreach(var actions in new[]{(-1e5,0d,0d),(-1e5,2e6,-3e6),(0d,-2e6,3e6)}) {
            double S((double X,double Y,double A) p)=>state.Stress(p.X,p.Y,actions.Item1,actions.Item2,actions.Item3);
            Assert.AreEqual(actions.Item1,points.Sum(p=>p.A*S(p)),1e-7);
            Assert.AreEqual(actions.Item2,points.Sum(p=>p.A*S(p)*(p.Y-g.GrossY)),1e-5);
            Assert.AreEqual(actions.Item3,-points.Sum(p=>p.A*S(p)*(p.X-g.GrossX)),1e-5);
        }
        widths[0]=0;Assert.AreNotEqual(widths[0],state.Widths[0]); // immutable snapshot
    }
    [DataTestMethod] [DataRow(12d,8d)] [DataRow(6d,4d)]
    public void RoundedRhsPreservesExactCornersAndRemovesOnlyMaterial(double ro,double ri)
    {
        var shape=new SectionRHSRoundedCorners(200,120,4,ro,ri,"rounded");
        var g=SteelEffectiveGeometry.Create(CommonSectionCoverageTests.Input(shape,false).Section);
        var gross=g.Gross();Assert.AreEqual(shape.Area,gross.Area);Assert.AreEqual(shape.Jxx,gross.Ixx);
        var w=gross.Widths.Select(v=>v*.5).ToArray();var reduced=g.WithWidths(w);
        double removed=g.Plates.Sum(p=>p.Length*p.Thickness/2);
        Assert.AreEqual(shape.Area-removed,reduced.Area,1e-8);Assert.AreEqual(0,reduced.CentroidShiftX,1e-10);Assert.AreEqual(0,reduced.CentroidShiftY,1e-10);
        Assert.IsTrue(reduced.Ixx<gross.Ixx&&reduced.Iyy<gross.Iyy);
        Assert.IsTrue(g.Plates.All(p=>p.BucklingWidth>=p.Length));
    }
    [TestMethod]
    public void InvalidWidthsAndShellGeometryDoNotProduceEffectiveProperties()
    {
        var g=SteelEffectiveGeometry.Create(CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape("H"),false).Section);
        Assert.ThrowsException<ArgumentException>(()=>g.WithWidths(new double[1]));
        var w=g.Gross().Widths.ToArray();w[0]=double.NaN;Assert.ThrowsException<ArgumentOutOfRangeException>(()=>g.WithWidths(w));
        w=g.Gross().Widths.ToArray();w[0]*=3;Assert.ThrowsException<ArgumentOutOfRangeException>(()=>g.WithWidths(w));
        Assert.ThrowsException<NotSupportedException>(()=>SteelEffectiveGeometry.Create(CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape("CHS"),false).Section));
    }
}
