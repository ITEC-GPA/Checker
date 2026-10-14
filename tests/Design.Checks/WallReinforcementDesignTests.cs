using GPC.Design.Walls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;
[TestClass]
public sealed class WallReinforcementDesignTests
{
    [TestMethod]
    public void CatalogueOrderAndSecondarySteelMatchIndependentValues()
    {
        var visited=new List<WallDesignLayout>();
        var result=WallReinforcementDesign.Search(WallRebarZone.Stem,5,12,1,400,400,[new(0,300)],candidate=>
        {visited.Add(candidate);return [new(candidate.Diameter>=10?.5:2,false)];});
        CollectionAssert.AreEqual(new[]{(4,8d),(5,8d),(4,10d)},visited.Select(c=>(c.Count,c.Diameter)).ToArray());
        Assert.AreEqual(3,result.Evaluated);Assert.AreEqual(new WallDesignLayout(4,10,10,200),result.Selected);
        // Wall concrete ratio per face gives 2000 mm2/m for a 4000 mm stem; Ø10 allows 39.27 mm => catalogue minimum 50.
        var thick=WallReinforcementDesign.Search(WallRebarZone.StemUpper,4,8,1,400,4000,[new(0,300)],_=>[new(.5,false)]);
        Assert.AreEqual(50d,thick.Selected!.SecondarySpacing);
        Assert.AreEqual(8,WallReinforcementDesign.ContinuousStemCount(5,8));
    }
    [TestMethod]
    public void CheapBoundRejectsImpossibleMomentsBeforeSectionEvaluation()
    {
        int invoked=0;var result=WallReinforcementDesign.Search(WallRebarZone.Toe,4,8,1,400,400,[new(1e12,300)],_=>{invoked++;return [new(0,false)];});
        Assert.IsNull(result.Selected);Assert.AreEqual(0,invoked);Assert.AreEqual(0,result.Evaluated);
    }
    [TestMethod]
    public void MissingFailedOrNonFiniteChecksCannotProduceASolution()
    {
        foreach(double? ratio in new double?[]{null,double.NaN,double.PositiveInfinity,-1,1.1})Assert.IsFalse(WallReinforcementDesign.Accepts([new(ratio,false)],1));
        Assert.IsFalse(WallReinforcementDesign.Accepts([],1));
        Assert.IsTrue(WallReinforcementDesign.Accepts([new(.7,false),new(.9,true)],.75));
        Assert.IsFalse(WallReinforcementDesign.Accepts([new(.8,false)],.75));
        Assert.ThrowsException<ArgumentException>(()=>WallReinforcementDesign.Search(WallRebarZone.Heel,4,8,1,400,400,[],_=>[]));
        Assert.ThrowsException<OperationCanceledException>(()=>WallReinforcementDesign.Search(WallRebarZone.Heel,4,8,1,400,400,[new(0,300)],_=>[],new CancellationToken(true)));
        using var stop=new CancellationTokenSource();
        Assert.ThrowsException<OperationCanceledException>(()=>WallReinforcementDesign.Search(WallRebarZone.Heel,4,8,1,400,400,[new(0,300)],_=>{stop.Cancel();return [new(.5,false)];},stop.Token));
    }
}
