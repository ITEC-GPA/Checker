using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

public partial class SteelBeamColumnWorkflowTest
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void Class4AllPlateMemberRouteCarriesBothShearsAndBothMoments(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model,ntc);
        foreach(var cut in C1A(model)) {
            cut.ResultBeamForces.V1=10000;cut.ResultBeamForces.V2=50000;
            cut.ResultBeamForces.M1=1e6;cut.ResultBeamForces.M2=-2e6;
        }
        p.Remove("ShearBucklingExclusionSource");
        p["MemberClass"]="4";p["ClassificationSource"]="Conservative class4 contract on compact reference H";
        p["HighShearMethod"]="ConservativeClass4ShearSuperposition";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Count>0);
        p["PlateSupportSource"]="Actual support stiffeners, continuous junctions and force introduction independently verified";
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        var r=Single(model,p,ntc);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,r.Outcome,Diagnostics(new[]{r}));
        double r1=Value(r,"MemberStability.Class4PlateInteraction.ShearRatio1"),r2=Value(r,"MemberStability.Class4PlateInteraction.ShearRatio2");
        Assert.IsTrue(r1>0&&r2>0);
        Assert.AreEqual(1-r1-r2,Value(r,"MemberStability.Class4PlateInteraction.NormalResistanceFactor"),1e-12);
        Assert.IsTrue(Value(r,"MemberStability.Interaction1")>0);
        Assert.IsTrue(Value(r,"MemberStability.Interaction2")>0);
    }
}

[TestClass]
public class SteelClass4PlateSectionWorkflowTest
{
    [DataTestMethod][DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void BoxAllPlateChecksPersistAndInvalidateWithTheirActualSupportContract(bool ntc,bool bridge)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(
            new SectionRHS(800,300,6,6,6,6,"class4 box"),SteelMaterialEN1993Data.S355));
        foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0])) {
            sample.ResultBeamForces.N=-100000;sample.ResultBeamForces.M1=1e6;sample.ResultBeamForces.M2=-1e6;
            sample.ResultBeamForces.V1=10000;sample.ResultBeamForces.V2=-50000;sample.ResultBeamForces.T=0;
        }
        plan.SectionMechanisms=new[]{CheckMechanism.UlsBiaxialSection};plan.MemberChecks=Array.Empty<MemberCheckSpecification>();
        var p=new Dictionary<string,string>{{"Unperforated","true"},{"Class4ShearInteraction","ConservativePlateSuperposition"},
            {"PlateSupportSource","Same-case diaphragms, continuous joints and force transfer verified"},
            {"SectionFingerprint",ModelValues.Fingerprint(new object[]{model.BeamElements[250].BeamProperty})}};
        Assert.AreEqual(0,SteelParameterSchemas.Section.Validate(p).Count);
        StandardEN1993p11 code=ntc?new StandardNTC2018Steel():new StandardEN1993p11();
        if(bridge)code.GammaM1=1.10;
        var request=new MultiMaterialCheckRequest();request.Jobs.Add(new MultiMaterialCheckJob {Name="Class4 all plates",Plan=plan,
            Assignments=new[]{new MaterialCheckerAssignment {Checker=new SteelPipelineMaterialChecker(code,ntc?"2018":"2005",parameters:p),
                Mechanisms=new[]{CheckMechanism.UlsBiaxialSection}}}});
        var report=new Service().Verify(model,request);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,report.Outcome,string.Join(";",report.Jobs[0].Results.SelectMany(r=>r.Diagnostics).Select(d=>d.Code+" "+d.Message)));
        foreach(var r in report.Jobs[0].Results) {
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith("PlateShearInteraction.Sum")&&t.Value>0));
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith("CheckedWalls")&&t.Value==4));
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);
        Assert.AreEqual(report.Outcome,Service.CurrentOutcome(restored,model,request));
        ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0]).First().ResultBeamForces.V1*=2;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(restored,model,request));
    }

    [TestMethod]
    public void PlateSchemaDoesNotAcceptAnUnboundOrConflictingAssessment()
    {
        var p=new Dictionary<string,string>{{"Class4ShearInteraction","ConservativePlateSuperposition"}};
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(p).Count>0);
        p["PlateSupportSource"]="verified supports";
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(p).Count>0);
        p["SectionFingerprint"]="fixture fingerprint";
        Assert.AreEqual(0,SteelParameterSchemas.Section.Validate(p).Count);
        p["ShearBucklingExcluded1"]="true";p["ShearBucklingSource1"]="external";
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(p).Count>0);
    }
}
