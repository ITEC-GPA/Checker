using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Core.Coordinates;
using GPC.Model.Core.Identity;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Models;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.Queries;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using GPC.Model.Structure.Members;
using GPC.Model.Structure.Topology;

namespace GPC.Examples
{
    /// <summary>Executable Model-Checker example; analytically assigned design-action
    /// states, not an FE solver or a complete frame design. N,mm,MPa,Nmm.
    /// Two braced 3m H columns and a simply supported 4m RHS crossbar.
    /// ULS-A/B are already factored states q=10/15 N/mm; never component envelopes.
    /// See STEEL_REFERENCE_FRAME.txt for restraints, sources and coverage.</summary>
    public static class SteelReferenceFrame
    {
        public static GPC.Model.Models.Model Create(bool split = false, bool reverseSecond = false)
        {
            var model = new GPC.Model.Models.Model("Steel reference frame: analytical design-action states");
            foreach (var n in new[] { (1,0d,0d),(2,0d,1500d),(3,0d,3000d),
                (4,4000d,0d),(5,4000d,1500d),(6,4000d,3000d),(7,2000d,3000d) })
                model.NodesElements.Add(new NodeElement(new Point3d(n.Item2,0,n.Item3),id:n.Item1));
            var h = new SteelSection(new SectionH(300,12,150,18,150,18,"H300"),SteelMaterialEN1993Data.S355);
            var rhs = new SteelSection(new SectionRHS(200,100,8,8,8,8,"RHS200x100x8"),SteelMaterialEN1993Data.S355);
            model.AddProperty(h); model.AddProperty(rhs);
            void Member(string id, int first, int middle, int last, int beamId, SteelSection section, Vector3d axis1)
            {
                var parts = new List<BeamMemberPart>();
                var pieces = split ? new[] { (first,middle),(middle,last) } : new[] { (first,last) };
                for (int k=0;k<pieces.Length;k++)
                {
                    bool along = !(reverseSecond && k==1); var ends=pieces[k];
                    var beam = new BeamElement(null,null,section,id:beamId+k); model.BeamElements.Add(beam);
                    model.ConnectBeam(beam.Id,along?ends.Item1:ends.Item2,along?ends.Item2:ends.Item1);
                    beam.Assignments.Formulation=BeamFormulation.StraightTwoNode;
                    beam.Assignments.StationDomain="NodeToNode"; beam.Assignments.LogicalMember=id;
                    beam.Assignments.SectionAxes=Axes.Beam(beam.StartPoint,beam.EndPoint,axis1);
                    // Shape coordinates follow the actual element frame, including reversed I/J.
                    beam.Assignments.SectionGeometryAxes=beam.Assignments.SectionAxes;
                    beam.Assignments.ActionsAtSectionCentroidConfirmed=true;
                    beam.Assignments.Sections.Add(new BeamSectionAssignment{Start=0,End=1,Property=section});
                    parts.Add(new BeamMemberPart(beam.Id,along));
                }
                model.PhysicalMembers.Add(id,new PhysicalMemberDefinition(id,parts,"NodeToNode",source:"Analytical example: explicit full straight member"));
            }
            Member("C1",1,2,3,100,h,new Vector3d(1,0,0));
            Member("C2",4,5,6,200,h,new Vector3d(1,0,0));
            Member("B1",3,7,6,300,rhs,new Vector3d(0,1,0));
            var cases = new[]{new LoadCaseBase("ULS-A"),new LoadCaseBase("ULS-B")};
            foreach(var lc in cases)model.LoadCases.Add(lc);
            model.CaptureAnalysis(ReinforcementAnalysisRole.ExcludedFromAnalysis,"Assigned analytical qL/2 and qx(L-x)/2 design-action states; no FE solution.");
            string fp=model.AnalysisFingerprint();
            model.Datasets.Add("steel-reference",new AnalysisDataset{Id="steel-reference",Program="Analytical example",ModelRevision="r1",
                InputFingerprint=fp,NormalizedUnits="N,mm,rad",IsSynthetic=true,Semantics=AnalysisSemantics.LinearStatic});
            foreach(var definition in model.PhysicalMembers.Values)
            {
                var geometry=new PhysicalMemberGeometry(model,definition);
                foreach(var part in definition.Parts)
                {
                    var beam=model.BeamElements[part.BeamId]; var samples=new List<IBeamResultLocation>();
                    for(int c=0;c<cases.Length;c++)foreach(double xi in new[]{0d,.25,.5,.75,1d})
                    {
                        var point=new BeamReferenceGeometry(beam).PointAt(xi,"NodeToNode");
                        double x=geometry.FromElement(beam.Id,xi,"NodeToNode",SectionSide.Unspecified).Distance,q=c==0?10:15;
                        bool column=definition.Id!="B1";
                        var frame=ActionTransformations.AtPoint(geometry.Axes,point);
                        var source=new StationResultBeamForces(cases[c],new ResultBeamForces(column?-q*2000:0,0,
                            column?0:-q*(2000-x),0,column?0:q*x*(4000-x)/2,0,frame),xi) {
                            StationDomain="NodeToNode",PhysicalDistance=xi*beam.Length,Body=ActionBody.PositiveSectionFace,
                            State=new ResultState{DatasetId="steel-reference",ModelRevision="r1",InputFingerprint=fp,
                                Components=Enumerable.Repeat(ComponentAvailability.Available,6).ToArray(),Semantics=AnalysisSemantics.LinearStatic,
                                ConcomitantStateId=cases[c].Name,IsCumulative=true,IsSynthetic=true,
                                Coverage="Analytical polynomials; exported grid includes endpoints and exact bending maximum."} };
                        samples.Add(ResultOrientation.Beam(source,ActionTransformations.AtPoint(beam.Assignments.SectionAxes,point)));
                    }
                    beam.AddResult(new BeamResult(samples));
                }
            }
            return model;
        }

        public static BeamCheckPlanRequest Plan(string member, string state, bool sections=true)
            => new BeamCheckPlanRequest { MemberIds=new[]{member},SectionMechanisms=sections?new[]{CheckMechanism.UlsBiaxialSection}:Array.Empty<CheckMechanism>(),
                Results=new[]{new ResultSelection{Dataset="steel-reference",Case=state,ConcomitantState=state}},
                MemberChecks=new[]{new MemberCheckSpecification{MemberId=member,MethodId=SteelMemberPipelineChecker.EngineId,
                    Context=new MemberDesignContext(effectiveLength1:member=="B1"?4000:3000,effectiveLength2:member=="B1"?4000:3000,
                        lateralTorsionalLength:member=="B1"?4000:3000,lengthSource:"Analytical full-member fork/braced idealization; independent of FE mesh",
                        assumptions:"Non-sway reference; columns twist-restrained, crossbar forks/free warping. Assigned actions include design factors.",globalStateConfirmed:true)}} };

        public static VerificationConfiguration Configure(GPC.Model.Models.Model model, bool ntc)
        {
            var engines=new List<EngineDefinition>();var jobs=new List<ConfiguredCheckJob>();
            foreach(string member in new[]{"C1","C2","B1"})foreach(string state in new[]{"ULS-A","ULS-B"})
            {
                var plan=Plan(member,state);var prepared=BeamCheckPlan.PrepareActions(model,plan).WorkItems.Single(w=>w.Scope==CheckScope.PhysicalMember);
                if(prepared.Member==null)throw new InvalidOperationException("Reference member preparation failed.");
                string name=member+"-"+state;
                var options=new Dictionary<string,string>{["AnalysisMode"]=member=="B1"?"Bending":"Compression",
                    ["MemberFingerprint"]=ModelValues.Fingerprint(new object[]{prepared.Member.Snapshot}),["Unperforated"]="true",
                    ["LoadPatternReference"]=name+"; complete concomitant analytical diagram",
                    ["ParameterSet"]="Reference frame; standard general curves; declared full-member restraints"};
                var property=(ISteelSection)prepared.Member.ActionSections[0].Property;
                if(member=="B1") {
                    // C1=1 uniform-moment fork value; warping omitted conservatively for this RHS.
                    double mcr=Math.PI/4000*Math.Sqrt(210000*property.J22*(210000/(2*1.3))*property.Jt);
                    options["McrBasis"]="UniformMoment";options["Mcr.Value"]=mcr.ToString("R",CultureInfo.InvariantCulture);
                    options["Mcr.Unit"]="Nmm";options["Mcr.Source"]="Uniform-moment fork elastic bifurcation pi/L sqrt(E I2 G It); warping neglected; E=210000MPa, nu=.3, L=4000mm.";
                } else {
                    options["CompressionCurve1"]="B";options["CompressionCurve2"]="C";options["TorsionalCurve"]="C";
                    options["TorsionalExclusionSource"]="Ideal continuous twist restraint assigned to both reference columns.";
                    options["FlexuralTorsionalExclusionSource"]="The same ideal twist restraint excludes the coupled torsional mode.";
                }
                var sectionOptions=new Dictionary<string,string>{["Unperforated"]="true"};
                if(member=="B1") {
                    sectionOptions["SectionFingerprint"]=ModelValues.Fingerprint(new object[]{property});
                    sectionOptions["ShearBucklingExcluded2"]="true";
                    sectionOptions["ShearBucklingSource2"]="Stocky reference RHS web: clear h/t=23; below 72 sqrt(235/355)/1.2=48.8, conservative unstiffened web screen.";
                }
                engines.Add(new EngineDefinition{Id=name+"-member",Kind=SteelMemberPipelineChecker.EngineId,ContextId="code",Parameters=options});
                engines.Add(new EngineDefinition{Id=name+"-section",Kind=SteelPipelineMaterialChecker.EngineId,ContextId="code",Parameters=sectionOptions});
                jobs.Add(new ConfiguredCheckJob{Name=name,Plan=plan,Routes=new[]{
                    new CheckRouteDefinition{EngineId=name+"-member",Mechanisms=new[]{CheckMechanism.Stability}},
                    new CheckRouteDefinition{EngineId=name+"-section",Mechanisms=new[]{CheckMechanism.UlsBiaxialSection}}}});
            }
            return new VerificationConfiguration { Contexts=new[]{new DesignContextDefinition{Id="code",Code=ntc?(StandardEN1993p11)new StandardNTC2018Steel():new StandardEN1993p11(),Edition=ntc?"2018":"2005"}},
                Engines=engines.ToArray(),Jobs=jobs.ToArray()};
        }
    }
}
