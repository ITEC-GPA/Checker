using System.Globalization;
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class BridgeSpliceApplicationTests
{
    internal static IEnumerable<SteelShearBucklingAssessment> Prepare(SteelVerificationInput i)
        => new[] { i.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
            ? SteelShearBucklingAssessment.EvaluateNtcHWeb(i,"Explicit application H-web assessment")
            : SteelShearBucklingAssessment.EvaluateEc3HWeb(i,1.2,"Explicit EC eta1.2") };
    internal static SpliceActionCase[] Actions(double scale=1) => new[] {
        new SpliceActionCase("ULS+",new(2e5*scale,-1e5*scale,0,0,0,50e6*scale),"User combination +"),
        new SpliceActionCase("ULS-",new(-2e5*scale,1e5*scale,0,0,0,-50e6*scale),"User combination -") };
    internal static WeldedIBeamSpliceStandardInput Create(bool ntc=false,SpliceBoltCategory category=SpliceBoltCategory.B,
        bool prepare=true,bool slip=true)
    {
        var template=WeldedIBeamSpliceStandardTests.Input(ntc);
        var section=WeldedIBeamSpliceStandardTests.Section(template).Plan.Input.Section;
        return BridgeSpliceApplication.CreateBolted(template,section,section,Actions(),WeldedIBeamSpliceStandardTests.Welds(),
            "Declared global restraint/continuity analysis",category,prepare?Prepare:null,
            slip?WeldedIBeamSpliceSlipTests.Input(ntc):null,
            slip && category==SpliceBoltCategory.B?new[]{new SpliceActionCase("SLS",new(1e5,-5e4,0,0,0,25e6),"Separate SLS combination")}:null,
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);
    }
    internal static IReadOnlyList<WeldedSpliceButtCaseResult> Welded(bool ntc=false,bool prepare=true,double scale=1,string id="U")
    {
        var d=WeldedIBeamSpliceStandardTests.Input(ntc);
        var section=WeldedIBeamSpliceStandardTests.Section(d).Plan.Input.Section;
        SpliceButtWeldDetail Weld(double t,double width)=>new(SpliceWeldPenetration.Full,t,width,355,510,"WPS qualified","Preparation/inspection records");
        return BridgeSpliceApplication.VerifyWelded(d.Plates.Policy.Code,section,section,
            new[]{new SpliceActionCase(id,new(2e5*scale,-1e5*scale,0,0,0,50e6*scale),"Concomitant actions")},
            Weld(30,400),Weld(16,320),Weld(30,400),"Aligned complete seams",prepare?Prepare:null);
    }
    [DataTestMethod][DataRow(false,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.A)]
    [DataRow(false,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.B)]
    [DataRow(false,SpliceBoltCategory.C)][DataRow(true,SpliceBoltCategory.C)]
    public void ModelBuilderMatchesTheExplicitWorkflowAndRetainsEveryCase(bool ntc,SpliceBoltCategory category)
    {
        var built=Create(ntc,category);var actual=WeldedIBeamSpliceStandard.Verify(built);
        var manual=WeldedIBeamSpliceStandard.Verify(BridgeSpliceDirectBlockTests.Direct(WeldedIBeamSpliceStandardTests.Workflow(ntc,category)));
        Assert.AreEqual(ConnectionStatus.Passed,actual.Status,string.Join(";",actual.MissingRequirements));
        CollectionAssert.AreEqual(manual.CheckSources.Select(x=>x.Utilization).ToArray(),actual.CheckSources.Select(x=>x.Utilization).ToArray());
        Assert.AreEqual(2,built.Cases.Count);
        Assert.AreEqual(-50e6,built.Cases[0].Parents.LeftSection.Plan.Input.Actions.M1);
        Assert.AreEqual("User combination +",built.Cases[0].Parents.Detail.Plates.Bolts.UltimateCombinationSource);
        if(category==SpliceBoltCategory.C)Assert.AreEqual(2,built.Envelope.SlipCases.Count);
    }
    [DataTestMethod][DataRow(SpliceBoltCategory.B)][DataRow(SpliceBoltCategory.C)]
    public void BuilderDoesNotInventMissingSlipCombinations(SpliceBoltCategory category)
    {
        var result=WeldedIBeamSpliceStandard.Verify(Create(category:category,slip:false));
        Assert.AreEqual(ConnectionStatus.NotSupported,result.Status);
        Assert.AreEqual(0,result.SlipCases.Count);
        Assert.IsTrue(result.MissingRequirements.Any(x=>x.Contains("SLIP_REQUIRED")||x.Contains("SERVICEABILITY_CASE_REQUIRED")));
    }
    [TestMethod]
    public void MissingShearPreparationRemainsVisibleInBothResultAndReport()
    {
        var result=WeldedIBeamSpliceStandard.Verify(Create(prepare:false));
        var report=BridgeSpliceReport.FromBolted(result);
        Assert.IsFalse(report.IsComplete);Assert.IsNull(report.GoverningResistance);
        Assert.IsTrue(report.Rows.Any(x=>x.SteelCheck?.Status==SteelExecutionStatus.RequiresPreparation));
        Assert.IsTrue(report.MissingRequirements.Count>0);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ReportRetainsOriginalChecksFactorsAndSeparatesGeometryFromResistance(bool ntc)
    {
        var result=WeldedIBeamSpliceStandard.Verify(Create(ntc));
        var r=BridgeSpliceReport.FromBolted(result);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        Assert.AreEqual(SpliceReportRole.Resistance,r.GoverningResistance.Role);
        foreach(var row in r.Rows.Where(x=>x.ConnectionCheck!=null&&x.Role==SpliceReportRole.Resistance))
            Assert.IsTrue(result.CheckSources.Any(x=>ReferenceEquals(x.ComponentCheck,row.ConnectionCheck)));
        Assert.IsTrue(r.Rows.Any(x=>x.Component=="Detailing"&&x.Role==SpliceReportRole.Applicability));
        Assert.IsTrue(r.Rows.Any(x=>x.Component=="BlockHierarchy"&&x.Role==SpliceReportRole.Applicability));
        Assert.IsTrue(r.Rows.Where(x=>x.SteelCheck!=null).All(x=>x.Demand==null));
        Assert.AreEqual(ntc?1.05:1,r.Cases[0].Code.Plates.GammaM0);
        StringAssert.Contains(r.ToText(),"gammaM3,ULS=1.25");
        StringAssert.Contains(r.ToText(),"mu=0.4");
        Assert.IsTrue(r.Notes.Any(x=>x.Contains("Member:NOT_SUPPLIED")));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void WeldedAndBoltedHaveTheSameReportContractWithoutInventingWeldResistance(bool ntc)
    {
        var w=Welded(ntc);var r=BridgeSpliceReport.FromWelded(w);
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsNotNull(r.GoverningResistance);
        Assert.IsTrue(r.Rows.Where(x=>x.Role==SpliceReportRole.Resistance).All(x=>x.ConnectionCheck==null));
        Assert.IsTrue(r.Rows.Any(x=>ReferenceEquals(x.SteelCheck,w[0].Result.LeftSection.Checks[0])));
        StringAssert.Contains(r.ToText(),"CJP.ConsumablePrerequisites");
    }
    [TestMethod]
    public void WeldedReportCannotMergeDifferentCodePoliciesIntoOneQualifiedSplice()
    {
        var r=BridgeSpliceReport.FromWelded(Welded(id:"EC").Concat(Welded(true,id:"NTC")));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("PHYSICAL_SPLICE_OR_POLICY_MISMATCH")));
        Assert.ThrowsException<ArgumentException>(()=>BridgeSpliceReport.FromWelded(Welded().Concat(Welded())));
    }
    [TestMethod]
    public void UnavailableWeldedChecksDoNotHideKnownOverload()
    {
        var r=BridgeSpliceReport.FromWelded(Welded(prepare:false,scale:100,id:"unprepared").Concat(Welded(scale:100,id:"overloaded")));
        Assert.IsFalse(r.IsComplete);Assert.IsTrue(r.HasKnownFailure);
        Assert.IsTrue(r.Rows.Any(x=>x.Failed));Assert.IsNull(r.GoverningResistance);
    }
    [TestMethod]
    public void ActionMappingTransportsThePoleAndRejectsEveryOutOfPlaneComponent()
    {
        var a=BridgeSpliceApplication.ToSteelActions(new(2e5,1e5,0,0,0,80e6,30,20));
        Assert.AreEqual(-79e6,a.M1);Assert.AreEqual(1e5,a.V2);
        foreach(var bad in new[]{new PlanarConnectionActions(0,0,1,0,0,0),new(0,0,0,1,0,0),new(0,0,0,0,1,0)})
            Assert.ThrowsException<NotSupportedException>(()=>BridgeSpliceApplication.ToSteelActions(bad));
    }
    [TestMethod]
    public void ReportIsCultureIndependentAndDoesNotRenderMissingValuesAsZero()
    {
        var r=BridgeSpliceReport.FromWelded(Welded());
        var old=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("it-IT");string it=r.ToText();
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");Assert.AreEqual(it,r.ToText());
            Assert.IsTrue(r.Rows.Any(x=>x.Demand==null));
        }
        finally { CultureInfo.CurrentCulture=old; }
    }
    [TestMethod]
    public void InvalidCasesAndNullReportsAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(()=>new SpliceActionCase("",new(0,0,0,0,0,0),"x"));
        Assert.ThrowsException<ArgumentException>(()=>new SpliceActionCase("id",new(0,0,0,0,0,0),""));
        Assert.ThrowsException<ArgumentNullException>(()=>BridgeSpliceReport.FromBolted(null!));
        Assert.ThrowsException<ArgumentException>(()=>BridgeSpliceReport.FromWelded(Array.Empty<WeldedSpliceButtCaseResult>()));
    }
}

