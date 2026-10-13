using GPC.Design.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;
[TestClass]
public sealed class SectionDesignQuantitiesTests
{
    static DesignBar[] Bars()=>[new(-100,-200,20,100),new(100,-200,20,100),new(100,200,20,100),new(-100,200,20,100)];
    [TestMethod]
    public void IndependentLinkAndMainQuantitiesAndObjectives()
    {
        var bars=Bars();var geometry=new SectionDesignGeometry(false,400,600,40,400,bars);bars[0]=new(0,0,1,1);
        var metric=SectionDesignQuantities.Calculate(geometry,new(true,10,200,2,2,0,20),new(SecondaryReinforcementKind.None,0,0,0));
        // Link centreline: 310 x 510, perimeter 1640 + 200 hooks = 1840 mm; five links/metre.
        Assert.AreEqual(400*.00785,metric.LongitudinalKgPerM,1e-12);
        Assert.AreEqual(Math.PI*100/4*1840/200*.00785,metric.TransverseKgPerM,1e-12);
        Assert.AreEqual(9d,metric.PiecesPerM);Assert.AreEqual(2,metric.Diameters);
        Assert.AreEqual(metric.KgPerM*2+9*3+2*4,SectionDesignQuantities.Score(metric,new(ReinforcementObjective.Weighted,2,3,4)),1e-12);
        Assert.AreEqual(100d,geometry.Bars[0].Area);Assert.AreEqual(2d,SectionDesignQuantities.Score(metric,new(ReinforcementObjective.DiameterKinds,1,1,1)));
    }
    [TestMethod]
    public void TorsionAreaIsDistributedOverEachTributaryLength()
    {
        // Each corner in a 200 x 400 rectangle has (200+400)/2 tributary length; reserve 25 mm² each.
        Assert.AreEqual(1d,SectionDesignQuantities.TorsionDistributionRatio(Bars(),100,25),1e-12);
        Assert.AreEqual(Math.Sqrt(.75),SectionDesignQuantities.ReservedDiameterFactor(25),1e-12);
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignQuantities.TorsionDistributionRatio(Bars(),double.NaN,25));
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignQuantities.ReservedDiameterFactor(100));
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignQuantities.TorsionDistributionRatio([new(0,0,20,100),new(0,0,20,100),new(0,0,20,100)],100,25));
    }

    [TestMethod]
    public void RankingAndSamplingKeepTiesAndExcludeOnlyDominatedMetrics()
    {
        SectionDesignRankEntry Entry(int id,double kg,double pieces,int diameters)=>new(id,new(100,kg,0,4,diameters,pieces));
        var entries=new[]{Entry(4,10,10,2),Entry(1,10,10,2),Entry(3,12,8,2),Entry(2,11,11,3)};
        var ranked=SectionDesignSelection.Rank(entries,new(ReinforcementObjective.Weight,1,1,1));
        CollectionAssert.AreEqual(new[]{1,4,2,3},ranked.Select(r=>r.Id).ToArray());
        CollectionAssert.AreEquivalent(new[]{1,3,4},ranked.Where(r=>r.Pareto).Select(r=>r.Id).ToArray());
        CollectionAssert.AreEqual(new[]{0,3,6,9},SectionDesignSelection.SampleIndices(10,4).ToArray());
        CollectionAssert.AreEqual(new[]{9},SectionDesignSelection.SampleIndices(10,1).ToArray());
        Assert.AreEqual(0,SectionDesignSelection.SampleIndices(0,10).Count);
    }
    [TestMethod]
    public void SecondaryMatUsesSharedSpacingAndMinimumRules()
    {
        var geometry=new SectionDesignGeometry(false,1000,200,30,400,Bars());
        var result=SectionDesignSelection.SelectSecondary(geometry,new(false,0,0,2,2,0,20),SecondaryReinforcementKind.Slab,
            100,250,false,-100,100,20,[8,10],[200,250],3,new(ReinforcementObjective.Weight,1,1,1));
        Assert.AreEqual(8d,result.Reinforcement.Diameter);Assert.AreEqual(250d,result.Reinforcement.Spacing);
        Assert.AreEqual(2*Math.PI*64/4*1000/250,result.Reinforcement.AreaPerMetre,1e-12);
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignSelection.SelectSecondary(geometry,new(false,0,0,2,2,0,20),SecondaryReinforcementKind.Wall,
            1e8,250,false,-100,100,20,[8],[250],3,new(ReinforcementObjective.Weight,1,1,1)));
    }
}
