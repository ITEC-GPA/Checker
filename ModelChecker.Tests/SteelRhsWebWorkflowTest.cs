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

[TestClass]
public class SteelRhsWebWorkflowTest
{
    [DataTestMethod][DataRow(false,1)][DataRow(false,2)][DataRow(true,1)][DataRow(true,2)]
    public void RhsComputedWebResistanceFlowsThroughModelReportAndArchive(bool ntc,int axis)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(
            new SectionRHSRoundedCorners(800,600,6,12,6,"rounded slender RHS"),SteelMaterialEN1993Data.S355));
        foreach(var cut in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0])) {
            var f=cut.ResultBeamForces;
            f.N=f.M1=f.M2=f.T=0;f.V1=axis==1?-500000:0;f.V2=axis==2?500000:0;
        }
        plan.SectionMechanisms=new[]{CheckMechanism.UlsBiaxialSection};plan.MemberChecks=Array.Empty<MemberCheckSpecification>();
        var p=new Dictionary<string,string>{{"Unperforated","true"},{"RhsWebResistance","NonRigidEndPost"},
            {"RhsWebSupportSource","Same-case support stiffeners, attachments and force introduction checked"},
            {"SectionFingerprint",ModelValues.Fingerprint(new object[]{model.BeamElements[250].BeamProperty})}};
        Assert.AreEqual(0,SteelParameterSchemas.Section.Validate(p).Count);
        var request=new MultiMaterialCheckRequest();request.Jobs.Add(new MultiMaterialCheckJob{Name="RHS single shear",Plan=plan,
            Assignments=new[]{new MaterialCheckerAssignment{Checker=new SteelPipelineMaterialChecker(
                ntc?new StandardNTC2018Steel():new StandardEN1993p11(),ntc?"2018":"2005",parameters:p),
                Mechanisms=new[]{CheckMechanism.UlsBiaxialSection}}}});
        var report=new Service().Verify(model,request);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,report.Outcome,
            string.Join(";",report.Jobs[0].Results.SelectMany(r=>r.Diagnostics).Select(d=>d.Code+" "+d.Message)));
        double area=2*6d*(800+600-12)-(4-Math.PI)*108,d=axis==1?600:800;
        double expected=area*d/1400*.83*86.4*6*Math.Sqrt(235d/355)/(d-18)*355/(Math.Sqrt(3)*(ntc?1.05:1));
        foreach(var r in report.Jobs[0].Results) {
            Assert.AreEqual(500000/expected,r.Utilization!.Value,1e-10);
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith("BucklingPanelWidth")&&t.Value==d-18));
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);
        Assert.AreEqual(report.Outcome,Service.CurrentOutcome(restored,model,request));
        ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0]).First().ResultBeamForces.N=-10000;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(restored,model,request));
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,new Service().Verify(model,request).Outcome);
    }

    [TestMethod]
    public void RhsSchemaRequiresBoundSupportEvidenceAndOneAssessmentModel()
    {
        var p=new Dictionary<string,string>{{"RhsWebResistance","NonRigidEndPost"}};
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(p).Count>0);
        p["RhsWebSupportSource"]="separate checks";
        Assert.IsTrue(SteelParameterSchemas.Section.Validate(p).Count>0);
        p["SectionFingerprint"]="fixture fingerprint";
        Assert.AreEqual(0,SteelParameterSchemas.Section.Validate(p).Count);
        foreach(var fields in new[]{new[]{"HWebResistance","NonRigidEndPost","HWebSupportSource"},
            new[]{"Class4ShearInteraction","ConservativePlateSuperposition","PlateSupportSource"},
            new[]{"ShearBucklingExcluded1","true","ShearBucklingSource1"},
            new[]{"ShearBucklingExcluded2","true","ShearBucklingSource2"}}) {
            var conflict=new Dictionary<string,string>(p){{fields[0],fields[1]},{fields[2],"separate assessment"}};
            Assert.IsTrue(SteelParameterSchemas.Section.Validate(conflict).Count>0);
            Assert.ThrowsException<ArgumentException>(()=>new SteelPipelineMaterialChecker(new StandardEN1993p11(),"2005",parameters:conflict));
        }
    }
}
