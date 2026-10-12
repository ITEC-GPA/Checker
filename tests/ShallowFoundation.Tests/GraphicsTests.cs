using GPC.Checkers.Geotechnics.Foundations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShallowFoundation.Tests;

[TestClass]
public sealed class GraphicsTests
{
    static void Close(double expected,double actual,double tolerance=1e-7)=>Assert.AreEqual(expected,actual,tolerance*Math.Max(1,Math.Abs(expected)));
    static ShallowFoundationInput Input(double ex=0,double ey=0)=>new(){MomentY=600000*ex,MomentX=600000*ey};
    [TestMethod] public void FullContactHasIndependentAffineCornerPressures()
    {
        var r=ShallowFoundationBearing.Calculate(Input(200,100));var g=r.Graphics!;
        // p=N/(BL)(1±6ex/B±6ey/L): 20,140,180,60 kPa in counterclockwise order.
        double[] expected={20,140,180,60};for(int i=0;i<4;i++)Close(expected[i],g.Corners[i].Pressure*1000);
        Close(6e6,g.CompressedArea);Close(100,g.MeanPressure*1000);Close(4.48e6,r.EffectiveArea);
        Assert.IsTrue(g.EquilibriumError<1e-11);
    }
    [TestMethod] public void OneAxisPartialContactIsTriangularAndNotEffectiveArea()
    {
        var r=ShallowFoundationBearing.Calculate(Input(500));var g=r.Graphics!;
        // Analytic compressed length 3(B/2-e)=1.5 m; peak=2N/(1.5*3)=266.666... kPa.
        Close(4.5e6,g.CompressedArea);Close(3e6,r.EffectiveArea);Close(800d/3,g.MaximumPressure*1000);
        Close(0,g.MinimumPressure);Close(-500,g.ContactPolygon.Min(v=>v.X));Close(1000,g.ContactPolygon.Max(v=>v.X));
        Close(500,g.CentreX);Close(1000,g.EffectiveWidthX);Close(3000,g.EffectiveLengthY);
    }
    [TestMethod] public void BiaxialCornerTriangleHasIndependentBarycentricSolution()
    {
        // Normalized triangle (.5,0),(0,.5),(.5,.5), affine peak at the last vertex.
        // Pressure centroid (.375,.375), area BL/8 and peak 24 N/(BL).
        var g=ShallowFoundationBearing.Calculate(Input(750,1125)).Graphics!;
        Close(750000,g.CompressedArea);Close(2400,g.MaximumPressure*1000);Close(0,g.MinimumPressure);
        Assert.IsTrue(g.EquilibriumError<1e-11);Assert.AreEqual(3,g.ContactPolygon.Count);
    }
    [TestMethod] public void OppositeMomentsMirrorTheDistribution()
    {
        var a=ShallowFoundationBearing.Calculate(Input(650,-800)).Graphics!;
        var b=ShallowFoundationBearing.Calculate(Input(-650,800)).Graphics!;
        Close(a.CompressedArea,b.CompressedArea);Close(a.MaximumPressure,b.MaximumPressure);
        for(int i=0;i<4;i++)Close(a.Corners[i].Pressure,b.Corners[(i+2)%4].Pressure);
    }
    [TestMethod] public void SwappedEffectiveAxesDoNotRotatePlanCoordinates()
    {
        var x=Input(100,1000);x.Width=4000;x.Length=2500;
        var r=ShallowFoundationBearing.Calculate(x);var g=r.Graphics!;
        Close(500,r.EffectiveWidth);Close(3800,g.EffectiveWidthX);Close(500,g.EffectiveLengthY);
        Close(100,g.CentreX);Close(1000,g.CentreY);
    }
    [TestMethod] public void NearEdgeAndStripRemainInEquilibrium()
    {
        var x=Input(990);x.Shape=FoundationShape.Strip;
        var g=ShallowFoundationBearing.Calculate(x).Graphics!;
        Close(30000,g.CompressedArea,1e-5);Close(40,g.MaximumPressure,1e-5);
        Assert.IsTrue(g.EquilibriumError<1e-11);
    }
    [TestMethod] public void CircleIsUniformAndUndrainedReferenceMechanismIsSymmetric()
    {
        var x=Input();x.Shape=FoundationShape.Circle;x.Condition=FoundationSoilCondition.Undrained;
        var g=ShallowFoundationBearing.Calculate(x).Graphics!;
        Close(1e6*Math.PI,g.CompressedArea);Close(.6/Math.PI,g.MinimumPressure);Close(g.MinimumPressure,g.MaximumPressure);
        Assert.AreEqual(0,g.Corners.Count,"A circle has no corner pressure samples outside its footprint.");
        var m=g.MechanismX;Close(1000,m.Wedge[1].Y);Close(3000,m.Right.Last().X);Close(-3000,m.Left.Last().X);
        Assert.IsTrue(m.Right.Zip(m.Left,(a,b)=>Math.Abs(a.X+b.X)+Math.Abs(a.Y-b.Y)).All(v=>v<1e-8));
    }
}
