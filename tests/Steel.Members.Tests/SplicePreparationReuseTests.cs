using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class SplicePreparationReuseTests
{
    [DataTestMethod]
    [DataRow(false,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.A)]
    [DataRow(false,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.B)]
    [DataRow(false,SpliceBoltCategory.C)][DataRow(true,SpliceBoltCategory.C)]
    public void CaseLocalReuseAgreesWithIndependentPublicCalls(bool ntc,SpliceBoltCategory category)
    {
        var input=WeldedIBeamSpliceStandardTests.Workflow(ntc,category);
        var r=WeldedIBeamSpliceStandard.Verify(input);
        foreach(var c in r.Cases)
        {
            var p=c.Input.Parents.Detail;
            var plates=WeldedIBeamSplicePlates.Verify(p.Plates);
            Same(WeldedIBeamSpliceBolts.Verify(p.Plates.Bolts).ComponentAssessment,c.Components.Bolts.ComponentAssessment);
            Same(WeldedIBeamSpliceDetailing.Verify(p).Detailing,c.Components.Detailing.Detailing);
            Same(WeldedIBeamSpliceStandardModel.Verify(p,input.FaceModel),c.Model);
            var extra=WeldedIBeamSpliceStandardPlates.Verify(p,false,input.FaceModel,input.PlateStability);
            Same(extra.NetSections,c.AdditionalPlates.NetSections);Same(extra.BlockHierarchy,c.AdditionalPlates.BlockHierarchy);
            CollectionAssert.AreEqual(plates.Diagnostics.ToArray(),c.Components.Plates.Diagnostics.ToArray());
            for(int i=0;i<plates.Covers.Count;i++)
            {
                Same(plates.Covers[i].Sections,c.Components.Plates.Covers[i].Sections);
                Same(plates.Covers[i].LongitudinalCompression,c.Components.Plates.Covers[i].LongitudinalCompression);
            }
            if(category==SpliceBoltCategory.C) Same(WeldedIBeamSpliceSlip.VerifyNetAxialPlastic(p.Plates),c.Components.NetAxialPlastic!);
        }
        Assert.AreNotSame(r.Cases[0].Components.Bolts.Forces,r.Cases[1].Components.Bolts.Forces);
        Assert.AreEqual(-r.Cases[0].Components.Bolts.Forces.ActionsAtJoint.Fx,r.Cases[1].Components.Bolts.Forces.ActionsAtJoint.Fx);
        Assert.AreEqual(-r.Cases[0].Components.Bolts.Forces.ActionsAtJoint.Mz,r.Cases[1].Components.Bolts.Forces.ActionsAtJoint.Mz);
    }

    [TestMethod]
    public void LaterEvaluationDoesNotMutateAnEarlierCaseOrItsReport()
    {
        var input=WeldedIBeamSpliceStandardTests.Workflow(category:SpliceBoltCategory.A);
        var first=WeldedIBeamSpliceStandard.Verify(input);
        string before=BridgeSpliceReport.FromBolted(first).ToText();
        var overloaded=WeldedIBeamSpliceStandard.Verify(WeldedIBeamSpliceStandardTests.Workflow(category:SpliceBoltCategory.A,scale:100));
        Assert.IsTrue(overloaded.HasFailedComponent);
        Assert.AreEqual(before,BridgeSpliceReport.FromBolted(first).ToText());
        Assert.AreEqual(before,BridgeSpliceReport.FromBolted(WeldedIBeamSpliceStandard.Verify(input)).ToText());
    }

    static void Same<T>(ConnectionVerification<T> a,ConnectionVerification<T> b) where T:class
    {
        Assert.AreEqual(a.Status,b.Status);CollectionAssert.AreEqual(a.Diagnostics.ToArray(),b.Diagnostics.ToArray());
        CollectionAssert.AreEqual(a.Checks.Select(c=>(c.Id,c.Demand,c.Resistance,c.Reference,c.Unit)).ToArray(),
            b.Checks.Select(c=>(c.Id,c.Demand,c.Resistance,c.Reference,c.Unit)).ToArray());
        CollectionAssert.AreEqual(a.Trace.Select(t=>(t.Name,t.Value,t.Unit)).ToArray(),b.Trace.Select(t=>(t.Name,t.Value,t.Unit)).ToArray());
    }
}
