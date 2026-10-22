using System;
using System.Linq;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Structure.Members;

namespace GPC.Examples
{
    /// <summary>Beam-column variant of SteelReferenceFrame. Assigned linear biaxial
    /// column diagrams and their equilibrium shears; not a frame-analysis solver.
    /// N is constant, positive in tension; N,mm,Nmm. Continuous twist restraints
    /// exclude lateral-torsional and torsional compression modes in this example.</summary>
    public static class SteelBeamColumnFrame
    {
        public static GPC.Model.Models.Model Create(bool split=false,bool reverseSecond=false,bool varyMinorMoment=false)
        {
            var model=SteelReferenceFrame.Create(split,reverseSecond);
            foreach(string member in new[]{"C1","C2"})
            {
                var definition=model.PhysicalMembers[member];var geometry=new PhysicalMemberGeometry(model,definition);
                foreach(var part in definition.Parts)foreach(string state in new[]{"ULS-A","ULS-B"})
                {
                    var beam=model.BeamElements[part.BeamId];
                    foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(beam,SteelReferenceFrame.Plan(member,state).Results[0]))
                    {
                        double x=geometry.FromElement(beam.Id,sample.ParametricDistance,"NodeToNode",SectionSide.Unspecified).Distance;
                        // Optional varying M2 controls at the opposite end to M1.
                        // ULS-A has larger M2, ULS-B larger M1 and N.
                        double my=state=="ULS-A"?10e6:30e6,mz=state=="ULS-A"?8e6:5e6;
                        var axes=ActionTransformations.AtPoint(geometry.Axes,sample.ResultBeamForces.CoordinateSystem.Origin);
                        var source=new StationResultBeamForces(sample.Case,new ResultBeamForces(state=="ULS-A"?-20000:-30000,
                            varyMinorMoment?mz/3000:0,my/3000,0,my*(1-x/3000),varyMinorMoment?mz*x/3000:mz,axes),sample.ParametricDistance) {
                            Body=ActionBody.PositiveSectionFace,StationDomain="NodeToNode",State=sample.State };
                        sample.ResultBeamForces=ResultOrientation.Beam(source,sample.ResultBeamForces.CoordinateSystem).ResultBeamForces;
                        sample.State.Coverage="Assigned linear biaxial diagrams; grid contains both endpoint maxima; equilibrium shears included.";
                    }
                }
            }
            return model;
        }

        public static VerificationConfiguration Configure(GPC.Model.Models.Model model,bool ntc)
        {
            var config=SteelReferenceFrame.Configure(model,ntc);
            foreach(var engine in config.Engines.Where(e=>e.Kind==SteelMemberPipelineChecker.EngineId&&e.Id.StartsWith("C")))
            {
                var p=engine.Parameters;p["AnalysisMode"]="BeamColumn";p.Remove("TorsionalCurve");
                p["TorsionSusceptible"]="false";p["Cm1"]="1";p["Cm2"]="1";p["CmLT"]="1";
                p["MomentFactorSource"]="Conservative Cm=1 for each assigned linear column diagram; no favourable gradient used.";
                p["StabilityModelSource"]="Straight prismatic non-sway reference member; elastic local diagrams; no plastic hinges. Design actions assigned, no global FE analysis claimed.";
                p["McrBasis"]="UniformMoment";
                p["LateralTorsionalExclusionSource"]="Ideal continuous twist restraint along the reference columns; no lateral-torsional mode.";
                p["ShearBucklingExclusionSource"]="H web clear h/t=264/12=22; flange outstand c/t=69/18=3.83; stocky plates, below conservative unstiffened-web screen 72*sqrt(235/355)/1.2=48.8.";
            }
            foreach(var job in config.Jobs.Where(j=>j.Name.StartsWith("C")))
            {
                var property=BeamCheckPlan.PrepareActions(model,job.Plan).WorkItems.Single(w=>w.Scope==CheckScope.PhysicalMember).Member.ActionSections[0].Property;
                var p=config.Engines.Single(e=>e.Id==job.Name+"-section").Parameters;
                p["SectionFingerprint"]=ModelValues.Fingerprint(new object[]{property});
                p["ShearBucklingExcluded1"]="true";p["ShearBucklingExcluded2"]="true";
                p["ShearBucklingSource1"]="Stocky H flanges c/t=3.83; explicit local shear-buckling exclusion for this example.";
                p["ShearBucklingSource2"]="Stocky H web h/t=22 < 72*sqrt(235/355)/1.2=48.8; explicit unstiffened-web screen.";
            }
            return config;
        }
    }
}
