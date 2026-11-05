using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core;
using GPC.Model.Core.Coordinates;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Structure.Members;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelGenericShearWorkflowTest
{
    [DataTestMethod][DataRow(false,false,false)][DataRow(true,false,false)]
    [DataRow(false,true,false)][DataRow(true,true,false)]
    [DataRow(false,false,true)][DataRow(true,false,true)]
    [DataRow(false,true,true)][DataRow(true,true,true)]
    public void SameCaseGenericDiagramsKeepHighShearPolicyAcrossMeshReversalAndArchive(bool ntc,bool reverse,bool class4)
    {
        var model=SteelBeamColumnFrame.Create(true,reverse,true);
        double fd=355/(ntc?1.05:1);
        double webResistance=Math.Min(3384*fd,3168*fd*(ntc?1:1.2))/Math.Sqrt(3);
        // Parabolic physical diagram: M1=Vmax*L*x*(1-x), V2=-Vmax*(1-2x).
        // Midspan/extreme and both ends are actual exports, including reversed FE parts.
        double vmax=(class4?.65:.55)*(class4?webResistance:3384*fd/Math.Sqrt(3));
        foreach(string id in new[]{"C1","C2"}) {
            var definition=model.PhysicalMembers[id];var geometry=new PhysicalMemberGeometry(model,definition);
            foreach(var part in definition.Parts)foreach(string state in new[]{"ULS-A","ULS-B"}) {
                var beam=model.BeamElements[part.BeamId];
                foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(beam,SteelReferenceFrame.Plan(id,state).Results[0])) {
                    double x=geometry.FromElement(beam.Id,sample.ParametricDistance,"NodeToNode",SectionSide.Unspecified).Distance/3000;
                    double factor=state=="ULS-A"?1:.8;
                    var axes=ActionTransformations.AtPoint(geometry.Axes,sample.ResultBeamForces.CoordinateSystem.Origin);
                    double m1=factor*vmax*3000*x*(1-x),v2=-factor*vmax*(1-2*x);
                    double v1=class4?0:factor*.15*5400*fd/Math.Sqrt(3);
                    double m2=class4?0:v1*3000*(x-.5);
                    var source=new StationResultBeamForces(sample.Case,new ResultBeamForces(-20000,v1,v2,0,m1,m2,axes),sample.ParametricDistance) {
                        Body=ActionBody.PositiveSectionFace,StationDomain="NodeToNode",State=sample.State };
                    sample.ResultBeamForces=ResultOrientation.Beam(source,sample.ResultBeamForces.CoordinateSystem).ResultBeamForces;
                    sample.State.Coverage="Analytic parabolic M1/linear V2 and linear M2/constant V1; actual ends/midpoint cover all extrema, separately for each case.";
                }
            }
        }
        var config=SteelBeamColumnFrame.Configure(model,ntc);
        foreach(var engine in config.Engines.Where(x=>x.Kind==SteelMemberPipelineChecker.EngineId && x.Id.StartsWith("C"))) {
            var p=engine.Parameters;
            foreach(string key in new[]{"Cm1","Cm2","CmLT"})p.Remove(key);
            p["MomentFactorPreparation"]="ConservativeEnvelope";
            p["MomentFactorSource"]="Conservative Cm=1 for the exported generic diagrams";
            p["MomentDiagramCoverageSource"]="Actual endpoints and midpoint contain every analytic extremum of this case";
            p["HighShearMethod"]=class4?"ConservativeClass4WebInteraction":"ConservativeShearSuperposition";
            if(class4) {
                p.Remove("ShearBucklingExclusionSource");p["MemberClass"]="4";
                p["ClassificationSource"]="Conservative effective-section contract on reference H";
                p["HWebSupportSource"]="Same-case support stiffeners and continuous web/flange attachments independently verified";
            }
            Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        }
        var request=EngineCatalog.BuiltIn().Compile(config);var report=new Service().Verify(model,request);
        foreach(var job in report.Jobs.Take(4)) {
            var r=job.Results.Single(x=>x.Scope==CheckScope.PhysicalMember);
            Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,r.Outcome,string.Join(";",r.Diagnostics.Select(d=>d.Code+" "+d.Message)));
            string root=class4?"Class4WebInteraction":"ShearSuperposition";
            double expected=(job==report.Jobs[0]||job==report.Jobs[2])?(class4?.35:.3):(class4?.48:.44);
            Assert.AreEqual(expected,r.Details.Trace.Single(t=>t.Key=="MemberStability."+root+".NormalResistanceFactor").Value!.Value,1e-10);
            Assert.AreEqual(1d,r.Details.Trace.Single(t=>t.Key=="MemberStability.Cm1").Value);
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var archived=CheckReportArchive.Load(stream);
        Assert.AreEqual(report.Outcome,Service.CurrentOutcome(archived,model,request));
        var sampleToChange=ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[100],SteelReferenceFrame.Plan("C1","ULS-A").Results[0]).First();
        sampleToChange.ResultBeamForces.V2*=.5;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(archived,model,request));
    }
}
