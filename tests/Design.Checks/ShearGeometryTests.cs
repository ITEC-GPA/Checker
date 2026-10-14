using GPC.Design.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ShearGeometryTests
{
    [TestMethod]
    public void RectangleAndTeeMatchIndependentFaceCentroids()
    {
        (double X,double Y)[] outline = [(-300,-400),(300,-400),(300,400),(-300,400)];
        double area = 100 * Math.PI;
        DesignBar[] bars = [new(-210,-310,20,area),new(210,-310,20,area),new(-210,310,20,area),new(210,310,20,area)];
        var x=ShearGeometryProposal.Derive(ShearSectionShape.Rectangle,true,outline,bars,600,800,0,0,0,0,0);
        var y=ShearGeometryProposal.Derive(ShearSectionShape.Rectangle,false,outline,bars,600,800,0,0,0,0,0);
        Assert.AreEqual(800d,x.Bw);Assert.AreEqual(510d,x.Depth);Assert.AreEqual(2*area,x.SteelArea);
        Assert.AreEqual(600d,y.Bw);Assert.AreEqual(710d,y.Depth);
        Assert.AreEqual(250d,ShearGeometryProposal.Derive(ShearSectionShape.Tee,true,outline,bars,600,800,0,0,0,250,400).Bw);
        Assert.AreEqual(400d,ShearGeometryProposal.Derive(ShearSectionShape.Tee,false,outline,bars,600,800,0,0,0,250,400).Bw);
    }
    [TestMethod]
    public void ExplicitSecondLayersUseAreaWeightedCentroidsAndHolesReduceWeb()
    {
        (double X,double Y)[] outline = [(-300,-400),(300,-400),(300,400),(-300,400)];
        DesignBar[] bars = [new(-200,-310,20,300),new(200,-310,20,300),new(-200,310,20,300),new(200,310,20,300),new(0,250,16,200),new(0,-250,16,200)];
        var y=ShearGeometryProposal.Derive(ShearSectionShape.Rectangle,false,outline,bars,600,800,0,100,200,0,0,[5],[4]);
        Assert.AreEqual(500d,y.Bw);Assert.AreEqual(800d,y.SteelArea);Assert.AreEqual(695d,y.Depth);
        Assert.ThrowsException<ArgumentException>(()=>ShearGeometryProposal.Derive(ShearSectionShape.Rectangle,false,outline,bars,600,800,0,100,200,0,0,[6],null));
    }
    [TestMethod]
    public void RingUsesBothWallsAndIndependentHalfRingAreas()
    {
        (double X,double Y)[] outline = [(-500,0),(0,-500),(500,0),(0,500)];
        DesignBar[] bars=[new(-300,-300,20,300),new(300,-300,20,300),new(-300,300,20,300),new(300,300,20,300)];
        var y=ShearGeometryProposal.Derive(ShearSectionShape.Circle,false,outline,bars,1000,1000,500,600,600,0,0);
        Assert.AreEqual(400d,y.Bw);Assert.AreEqual(800d,y.Depth);Assert.AreEqual(600d,y.SteelArea);
    }
}
