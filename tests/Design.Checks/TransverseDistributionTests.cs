using GPC.Model.Actions.Traffic;
using GPC.Design.Actions.Traffic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace DesignTests;

[TestClass]
public sealed class TransverseDistributionTests
{
    private static TransverseDistributionInput Input()=>new(){ApplicabilityConfirmed=true,InfluenceStations=11};
    private static DistributionLoadResult Total(TransverseDistributionResult r)=>r.Cases.Last();
    [TestMethod]
    public void CourbonUnequalGirdersHasExactLoadMomentAndNegativeShares()
    {
        var x=Input();x.Method=TransverseDistributionMethod.RigidCourbon;x.Tandems.Clear();x.Lanes[0].LeftM=3;x.Lanes[0].WidthM=2;
        x.Girders[1].FlexuralRigidityKnM2*=2;var r=TransverseDistribution.Calculate(x);var t=Total(r);
        // Elastic centre 0: stiffness fractions 1/4,1/2,1/4 and eccentric correction (-1/2,0,+1/2).
        double[] expected={-25,50,75};for(int i=0;i<3;i++)Assert.AreEqual(expected[i],t.Girders[i].Percentage,1e-10);
        Assert.AreEqual(9d*2*25*25/8,t.StaticMomentKnM,1e-9);Assert.AreEqual(t.StaticMomentKnM,t.CalculatedMomentKnM,1e-9);
    }
    [TestMethod]
    public void DeformableTwoGirdersWithoutTorsionMatchesLeverRule()
    {
        var x=Input();x.Girders.RemoveAt(1);foreach(var g in x.Girders)g.TorsionalRigidityKnM2=0;
        var flexible=TransverseDistribution.Calculate(x);x.Method=TransverseDistributionMethod.RigidCourbon;var rigid=TransverseDistribution.Calculate(x);
        Assert.IsTrue(flexible.Converged);foreach(var c in flexible.Cases){var expected=rigid.Cases.Single(t=>t.Name==c.Name);for(int i=0;i<2;i++)Assert.AreEqual(expected.Girders[i].Percentage,c.Girders[i].Percentage,1e-7);}
    }
    [TestMethod]
    public void CentreInfluenceMatchesIndependentThreeGirderFlexibilityOracle()
    {
        var x=Input();foreach(var g in x.Girders)g.TorsionalRigidityKnM2=0;var r=TransverseDistribution.Calculate(x);
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/transverse-independent.json")));
        var row=json.RootElement.GetProperty("rows").EnumerateArray().Single(v=>v.GetProperty("modes").GetInt32()==r.Modes);var p=r.Influence.Single(p=>Math.Abs(p.PositionM)<1e-12);
        Assert.AreEqual(row.GetProperty("outer").GetDouble(),p.MomentOrdinatesM[0],1e-9);Assert.AreEqual(row.GetProperty("centre").GetDouble(),p.MomentOrdinatesM[1],1e-9);Assert.AreEqual(p.MomentOrdinatesM[0],p.MomentOrdinatesM[2],1e-10);
    }
    [TestMethod]
    public void DefaultConvergesAndPreservesAllCasesAndStaticEquilibrium()
    {
        var x=Input();var r=TransverseDistribution.Calculate(x);Assert.IsTrue(r.Converged,"Modes="+r.Modes);Assert.AreEqual(3,r.Cases.Count);Assert.IsTrue(r.MaximumResidual<1e-10);
        foreach(var c in r.Cases){Assert.AreEqual(100,c.Girders.Sum(g=>g.Percentage),1e-9);Assert.IsTrue(c.EquilibriumRelativeError<=x.RelativeTolerance);Assert.IsTrue(c.ConvergenceRelativeError<=x.RelativeTolerance);}
        Assert.AreEqual(600,r.Cases.Single(c=>c.Name=="TS1").TotalLoadKn,1e-9);
        for(int i=0;i<3;i++)Assert.AreEqual(r.Cases[0].Girders[i].MomentKnM+r.Cases[1].Girders[i].MomentKnM,Total(r).Girders[i].MomentKnM,1e-8);
    }
    [TestMethod]
    public void ReflectionMirrorsGirderResultsAndReversesTorsion()
    {
        var x=Input();x.SectionM=9;var before=TransverseDistribution.Calculate(x);x.Lanes[0].LeftM=0;x.Tandems[0].CentreTransverseM=1.5;var after=TransverseDistribution.Calculate(x);
        for(int i=0;i<3;i++){Assert.AreEqual(Total(before).Girders[i].MomentKnM,Total(after).Girders[2-i].MomentKnM,1e-8);Assert.AreEqual(Total(before).Girders[i].TorsionKnM,-Total(after).Girders[2-i].TorsionKnM,1e-8);}
    }
    [TestMethod]
    public void LoadScalingChangesForcesButNotPercentagesOrInfluence()
    {
        var x=Input();var r=TransverseDistribution.Calculate(x);x.Lanes[0].LoadKnM2*=3;x.Tandems[0].AxleLoadKn*=3;var scaled=TransverseDistribution.Calculate(x);
        for(int i=0;i<3;i++){Assert.AreEqual(Total(r).Girders[i].Percentage,Total(scaled).Girders[i].Percentage,1e-9);Assert.AreEqual(3*Total(r).Girders[i].MomentKnM,Total(scaled).Girders[i].MomentKnM,1e-8);}
    }
    [TestMethod]
    public void NearlyRigidStripApproachesCourbonWithoutTorsion()
    {
        var x=Input();foreach(var g in x.Girders)g.TorsionalRigidityKnM2=0;x.TransverseRigidityKnM=1e13;var f=TransverseDistribution.Calculate(x);x.Method=TransverseDistributionMethod.RigidCourbon;var r=TransverseDistribution.Calculate(x);
        for(int i=0;i<3;i++)Assert.AreEqual(Total(r).Girders[i].Percentage,Total(f).Girders[i].Percentage,.01);
    }
    [TestMethod]
    public void NonConvergenceIsReportedAndNotHiddenByPercentageNormalization()
    {
        var x=Input();x.MaximumModes=32;x.RelativeTolerance=1e-7;var r=TransverseDistribution.Calculate(x);Assert.IsFalse(r.Converged);Assert.IsTrue(r.Notes.Any(n=>n.Contains("CONVERGENZA NON RAGGIUNTA")));
    }
    [TestMethod]
    public void InvalidGeometryAndUnconfirmedAssumptionsAreRejected()
    {
        var x=Input();x.ApplicabilityConfirmed=false;Assert.ThrowsException<ArgumentException>(()=>TransverseDistribution.Calculate(x));x.ApplicabilityConfirmed=true;
        x.Girders[1].PositionM=x.Girders[0].PositionM;Assert.ThrowsException<ArgumentException>(()=>TransverseDistribution.Calculate(x));x=Input();x.Tandems[0].CentreTransverseM=5;Assert.ThrowsException<ArgumentException>(()=>TransverseDistribution.Calculate(x));x=Input();x.Girders[0].FlexuralRigidityKnM2=double.NaN;Assert.ThrowsException<ArgumentException>(()=>TransverseDistribution.Calculate(x));
    }
    [TestMethod]
    public void CancellationIsHonoured()
    {var source=new CancellationTokenSource();source.Cancel();Assert.ThrowsException<OperationCanceledException>(()=>TransverseDistribution.Calculate(Input(),source.Token));}
}
