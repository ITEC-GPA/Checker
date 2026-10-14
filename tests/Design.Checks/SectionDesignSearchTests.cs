using System.Collections.Concurrent;
using GPC.Design.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;
[TestClass]
public sealed class SectionDesignSearchTests
{
    static readonly SectionDesignObjective Objective=new(ReinforcementObjective.Weight,1,1,1);
    static SectionDesignCatalogueInput Catalogue()=>new(false,400,600,false,SectionStirrupMode.Optimize,true,8,150,[16,20],[3,4],[3,4],[2],[8,12],[8],[100,150]);
    static SectionDesignCheck Check(bool pass)=>new("strength","Resistance","ULS",pass?.5:2,1,pass,"test response");
    static SectionDesignPreparation<string> Prepare(int id,SectionDesignLayout layout,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(layout.Top==3)return new(null,"geometry",Check(false));
        double area=layout.Top*layout.TopPhi*layout.TopPhi+layout.Bottom*layout.BottomPhi*layout.BottomPhi;
        return new(new(id,$"{layout.Top}/{layout.TopPhi}/{layout.Bottom}/{layout.BottomPhi}",layout,new(area,area*.00785,1,layout.Top+layout.Bottom,2,10),[Check(true)],$"{layout.Top}/{layout.TopPhi}/{layout.Bottom}/{layout.BottomPhi}"));
    }
    [TestMethod]
    public void CatalogueMatchesManualCountAndDeterministicTieOrder()
    {
        var plans=SectionDesignCatalogue.Create(Catalogue(),Objective);Assert.AreEqual(32,plans.Count);
        Assert.AreEqual(new SectionDesignLayout(3,3,2,16,16,16,true,8,150),plans[0]);
        var circle=SectionDesignCatalogue.Create(Catalogue() with {Circular=true,Width=600},Objective);Assert.AreEqual(8,circle.Count);
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignCatalogue.Create(Catalogue() with {Symmetric=true,TopCounts=[3],BottomCounts=[4]},Objective));
    }
    [TestMethod]
    public void WorkerCountDoesNotAlterRankingExclusionsOrLongitudinalReuse()
    {
        var plans=SectionDesignCatalogue.Create(Catalogue(),Objective);var calls=new ConcurrentDictionary<string,int>();
        IReadOnlyList<SectionDesignCheck> Longitudinal(string key,CancellationToken token){calls.AddOrUpdate(key,1,(_,n)=>n+1);return [Check(true)];}
        var one=SectionDesignSearch.Run(plans,1000,1,Objective,Prepare,Longitudinal);
        Assert.IsTrue(one.Completed);Assert.AreEqual(32,one.Evaluated);Assert.AreEqual(16,one.Candidates.Count);Assert.AreEqual(16,one.Exclusions["geometry"]);
        Assert.AreEqual(8,one.LongitudinalCalculations);Assert.AreEqual(8,one.LongitudinalReuses);Assert.IsTrue(calls.Values.All(n=>n==1));
        calls.Clear();var four=SectionDesignSearch.Run(plans,1000,4,Objective,Prepare,Longitudinal);
        CollectionAssert.AreEqual(one.Candidates.Select(c=>(c.Id,c.Score,c.Pareto)).ToArray(),four.Candidates.Select(c=>(c.Id,c.Score,c.Pareto)).ToArray());
        Assert.AreEqual(one.ExclusionExamples["geometry"],four.ExclusionExamples["geometry"]);Assert.IsTrue(calls.Values.All(n=>n==1));
    }
    [TestMethod]
    public void BudgetAndCancellationDoNotClaimExhaustiveSearch()
    {
        var plans=SectionDesignCatalogue.Create(Catalogue(),Objective);
        var limited=SectionDesignSearch.Run(plans,1,1,Objective,Prepare,(_,_)=>[Check(true)]);
        Assert.AreEqual(1,limited.Evaluated);Assert.IsFalse(limited.Completed);
        Assert.ThrowsException<OperationCanceledException>(()=>SectionDesignSearch.Run(plans,1000,1,Objective,Prepare,(_,_)=>[],new CancellationToken(true)));
        using var stop=new CancellationTokenSource();
        var cancelled=SectionDesignSearch.Run(plans,1000,1,Objective,Prepare,(_,_)=>[Check(true)],stop.Token,new ProgressSink<string>(_=>stop.Cancel()));
        Assert.IsFalse(cancelled.Completed);Assert.AreEqual(0,cancelled.Candidates.Count);
    }
    sealed class ProgressSink<T>(Action<SectionDesignProgress<T>> action):IProgress<SectionDesignProgress<T>>
    {public void Report(SectionDesignProgress<T> value)=>action(value);}
}
