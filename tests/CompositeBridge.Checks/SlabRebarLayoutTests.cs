using GPC.Checkers.CompositeBridge;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SlabRebarLayoutTests
{
    [TestMethod]
    public void ReferenceMovesOnlyBottomBarsAndDoesNotAddThickness()
    {
        var soffit = new SlabRebarLayout(true,60,250,SlabBottomReference.SlabSoffit,45,45,true,16);
        var predalle = new SlabRebarLayout(true,60,250,SlabBottomReference.PredalleTop,45,45,true,16);
        Assert.AreEqual(250d,predalle.Height);Assert.AreEqual(205d,predalle.TopAxisY);
        Assert.AreEqual(45d,soffit.BottomAxisY);Assert.AreEqual(105d,predalle.BottomAxisY);
        var disabled=new SlabRebarLayout(false,double.NaN,250,SlabBottomReference.PredalleTop,45,45,true,16);
        Assert.AreEqual(0d,disabled.PredalleThickness);Assert.AreEqual(45d,disabled.BottomAxisY);
    }
    [TestMethod]
    public void ThicknessAndDistanceHaveExplicitBoundaries()
    {
        foreach(double h in new[]{0d,250d,300d,double.NaN})
            Assert.ThrowsException<ArgumentException>(()=>new SlabRebarLayout(true,h,250,SlabBottomReference.PredalleTop,45,45,true,16));
        Assert.AreEqual(68d,new SlabRebarLayout(true,60,250,SlabBottomReference.PredalleTop,45,8,true,16).BottomAxisY);
        Assert.ThrowsException<ArgumentException>(()=>new SlabRebarLayout(true,60,250,SlabBottomReference.PredalleTop,45,7,true,16));
        Assert.ThrowsException<ArgumentException>(()=>new SlabRebarLayout(true,60,double.NaN,SlabBottomReference.PredalleTop,45,45,true,16));
    }
}
