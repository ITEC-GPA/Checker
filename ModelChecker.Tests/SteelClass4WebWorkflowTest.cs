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
    public void Class4WebMemberRouteComputesResistanceForTheSameFemEnvelope(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model,ntc);
        double fd=355/(ntc?1.05:1),vb=Math.Min(3384*fd,3168*fd*(ntc?1:1.2))/Math.Sqrt(3);
        foreach(var cut in C1A(model)) {
            cut.ResultBeamForces.M2=0;cut.ResultBeamForces.V1=0;cut.ResultBeamForces.V2=.75*vb;
        }
        p.Remove("ShearBucklingExclusionSource");
        p["MemberClass"]="4";p["ClassificationSource"]="Conservative effective-section isolation on the compact reference H";
        p["HighShearMethod"]="ConservativeClass4WebInteraction";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Count>0);
        p["HWebSupportSource"]="Same-case support stiffeners, attachments and force introduction independently verified";
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        var r=Single(model,p,ntc);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,r.Outcome,Diagnostics(new[]{r}));
        Assert.AreEqual(.75,Value(r,"MemberStability.Class4WebInteraction.ShearRatio"),1e-12);
        Assert.AreEqual(.25,Value(r,"MemberStability.Class4WebInteraction.NormalResistanceFactor"),1e-12);
        Assert.AreEqual(vb,Value(r,"MemberStability.Class4WebInteraction.VbwRd"),vb*1e-12);
    }
}

[TestClass]
public class SteelClass4WebSectionWorkflowTest
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CompressedClass4SectionAndArchiveRetainCoupledWebChecks(bool ntc)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(
            new SectionH(840,6,300,20,300,20,"slender web"),SteelMaterialEN1993Data.S355));
        double vb=.83*86.4*36*Math.Sqrt(235d/355)*355/(Math.Sqrt(3)*(ntc?1.05:1));
        foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0])) {
            sample.ResultBeamForces.N=-100000;sample.ResultBeamForces.M1=1e6;sample.ResultBeamForces.M2=0;
            sample.ResultBeamForces.V1=0;sample.ResultBeamForces.V2=.75*vb;sample.ResultBeamForces.T=0;
        }
        plan.SectionMechanisms=new[]{CheckMechanism.UlsBiaxialSection};plan.MemberChecks=Array.Empty<MemberCheckSpecification>();
        var p=new Dictionary<string,string>{{"Unperforated","true"},{"HWebResistance","NonRigidEndPost"},
            {"HWebNormalInteraction","ConservativeInPlane"},{"HWebSupportSource","Support stiffeners and attachments verified for this case"},
            {"SectionFingerprint",ModelValues.Fingerprint(new object[]{model.BeamElements[250].BeamProperty})}};
        Assert.AreEqual(0,SteelParameterSchemas.Section.Validate(p).Count);
        var request=new MultiMaterialCheckRequest();request.Jobs.Add(new MultiMaterialCheckJob {Name="Class4 in-plane web",Plan=plan,
            Assignments=new[]{new MaterialCheckerAssignment {Checker=new SteelPipelineMaterialChecker(ntc?new StandardNTC2018Steel():new StandardEN1993p11(),
                ntc?"2018":"2005",parameters:p),Mechanisms=new[]{CheckMechanism.UlsBiaxialSection}}}});
        var report=new Service().Verify(model,request);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,report.Outcome,string.Join(";",report.Jobs[0].Results.SelectMany(r=>r.Diagnostics).Select(d=>d.Code+" "+d.Message)));
        foreach(var r in report.Jobs[0].Results) {
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith("WholeWebIsSubpanel")&&t.Value==1));
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith("ShearInteractionPenalty")&&Math.Abs(t.Value!.Value-.25)<1e-10));
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        Assert.AreEqual(report.Outcome,Service.CurrentOutcome(CheckReportArchive.Load(stream),model,request));
    }
}
