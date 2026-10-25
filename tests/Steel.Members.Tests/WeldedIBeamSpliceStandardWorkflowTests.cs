using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

public partial class WeldedIBeamSpliceStandardTests
{
    internal static WeldedIBeamSpliceStandardInput Workflow(bool ntc=false,SpliceBoltCategory category=SpliceBoltCategory.B,
        double scale=1,bool unequal=false,bool slip=true)
    {
        var cases=new List<WeldedSpliceStandardCase>();var slips=new List<WeldedSpliceSlipCase>();
        foreach(int sign in new[]{1,-1})
        {
            string id=sign==1?"ULS+":"ULS-";
            var d=Detail(new(sign*scale*2e5,sign*scale*-1e5,0,0,0,sign*scale*50e6),unequal);
            cases.Add(new(id,Parents(Input(ntc,d))));
            if(slip&&category==SpliceBoltCategory.C)
                slips.Add(new(id+".slip",WeldedIBeamSpliceSlipTests.Input(ntc,BoltSlipLimitState.Ultimate,d),id));
        }
        if(slip&&category==SpliceBoltCategory.B)
            slips.Add(new("SLS",WeldedIBeamSpliceSlipTests.Input(ntc,detail:Detail(new(scale*1e5,scale*-5e4,0,0,0,scale*25e6),unequal))));
        return new(cases,category,slips);
    }
    [DataTestMethod][DataRow(false,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.A)]
    [DataRow(false,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.B)]
    [DataRow(false,SpliceBoltCategory.C)][DataRow(true,SpliceBoltCategory.C)]
    public void OneEntryPointCompletesTheDeclaredStaticTypology(bool ntc,SpliceBoltCategory category)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(ntc,category));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete);Assert.IsFalse(r.HasFailedComponent);
        Assert.IsNotNull(r.GoverningSource);Assert.IsFalse(r.CoversFatigue);Assert.IsFalse(r.CalculatesRotationalStiffness);
        Assert.IsFalse(r.CertifiesExecution);Assert.IsFalse(r.HasBothMemberAssessmentsForEveryCase);
        Assert.IsFalse(r.CheckSources.Any(s=>s.Component.Contains("Hierarchy")||s.Component.Contains("Domain")));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void UnequalTopAndBottomFlangesAndMomentReversalUseTheSameWorkflow(bool ntc)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,unequal:true));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.AreEqual(2,r.Cases.Count);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ACompleteOverloadedDetailIsFailedWithTheOriginalGoverningLeaf(bool ntc)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(ntc,scale:40));
        Assert.IsTrue(r.IsStaticResistanceComplete,string.Join(";",r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.HasFailedComponent);
        var g=r.GoverningSource;Assert.IsTrue(g.Failed);
        Assert.IsTrue(r.CheckSources.Contains(g));
    }
    [DataTestMethod][DataRow(SpliceBoltCategory.B)][DataRow(SpliceBoltCategory.C)]
    public void MissingSlipCannotBecomeAQualifiedJoint(SpliceBoltCategory category)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(category:category,slip:false));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsNull(r.GoverningSource);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("SLIP_REQUIRED")||x.Contains("SERVICEABILITY_CASE_REQUIRED")));
    }
    [TestMethod]
    public void AFailedSufficientHierarchyMeansAnotherMethodIsNeededNotActualCollapse()
    {
        var d=Detail();d=new(d.Section,d.Top,d.Bottom,new(280,24,355,510),d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var r=WeldedIBeamSpliceStandard.Verify(new(new[]{new WeldedSpliceStandardCase("U",Parents(Input(true,detail:d)))},SpliceBoltCategory.A));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.HasFailedComponent);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("BlockHierarchy")));
    }
    [TestMethod]
    public void IncompleteResultStillRetainsActualKnownFailures()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(category:SpliceBoltCategory.C,scale:40,slip:false));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsTrue(r.HasFailedComponent);
        Assert.IsTrue(r.CheckSources.Any(s=>s.Failed));Assert.IsNull(r.GoverningSource);
    }
    [TestMethod]
    public void WeldGeometryMustNotChangeBetweenCombinations()
    {
        var p=Workflow(category:SpliceBoltCategory.A);var old=p.Cases[1].Parents;
        var changed=new WeldedIBeamSpliceParentInput(old.Detail,old.LeftSection,old.RightSection,Welds(4),old.ContinuityAndSecondOrderAnalysisSource);
        var r=WeldedIBeamSpliceStandard.Verify(new(new[]{p.Cases[0],new WeldedSpliceStandardCase("changed",changed)},SpliceBoltCategory.A));
        Assert.IsFalse(r.IsStaticResistanceComplete);Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("WELD_DETAIL_MISMATCH")));
    }
    [TestMethod]
    public void AWrongSectionActionIsNotAggregatedIntoAQualifiedResult()
    {
        var d=Input();var parents=Parents(d,left:Section(d,new(2e5,0,-1e5,0,50e6,0)));
        var r=WeldedIBeamSpliceStandard.Verify(new(new[]{new WeldedSpliceStandardCase("U",parents)},SpliceBoltCategory.A));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("ACTION_OR_AXIS_SIGN_MISMATCH")));
    }
    [TestMethod]
    public void InputArraysAreImmutableSnapshotsAndCaseIdentifiersAreUnique()
    {
        var p=Workflow();var array=p.Cases.ToArray();var copy=new WeldedIBeamSpliceStandardInput(array,SpliceBoltCategory.A);
        array[0]=array[1];Assert.AreNotEqual(array[0].Id,copy.Cases[0].Id);
        Assert.ThrowsException<ArgumentException>(()=>new WeldedIBeamSpliceStandardInput(array,SpliceBoltCategory.A));
    }
}
